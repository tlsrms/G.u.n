using System;
using System.Collections.Generic;

namespace Gun.RoomRhythm
{
    public enum RunPhase { Ready, Waiting, Moving, Dead, Cleared }
    public enum FailureReason { None, WrongDirection, TooEarly, TooLate, MissedDoor, MissedEnemy, DoorCollision }
    public enum DeathPresentation { None, Execution, Collision, Departure }

    // Pure state machine: caller submits timestamped input before advancing to frame time.
    public sealed class RoomRun
    {
        private readonly MoveNote[] notes;
        private readonly TimingWindow window;
        private readonly double moveDuration;
        private readonly double enemyLeadTime;
        private double lastTime;
        private readonly bool[] brokenDoors;
        private readonly EnemyNote[] enemies;
        private readonly int[] enemyRooms;
        private readonly bool[] defeatedEnemies;
        private MoveDirection? movementInput;
        public DeathPresentation Death { get; private set; }
        public MoveDirection DeathDirection { get; private set; }
        public int FailedEnemy { get; private set; } = -1;
        public double DeathTime { get; private set; }

        public RunPhase Phase { get; private set; }
        public FailureReason Failure { get; private set; }
        public TimingGrade LastGrade { get; private set; }
        public int CompletedMoves { get; private set; }
        public double MoveStartedAt { get; private set; }
        public int JudgmentVersion { get; private set; }
        public double LastJudgedAt { get; private set; }
        public double? LastTimingErrorMs { get; private set; }
        public double RoomArrivedAt { get; private set; }
        public double MoveEndsAt => MoveStartedAt + moveDuration;
        public bool IsActive => Phase == RunPhase.Waiting || Phase == RunPhase.Moving;

        public RoomRun(MoveNote[] notes, TimingWindow window, double moveDuration,
            EnemyNote[] enemies = null, string startingRoomId = "start", double enemyReadTime = 0.25,
            double enemyLeadTime = double.PositiveInfinity)
        {
            if (!(enemyLeadTime > 0)) throw new ArgumentException("Invalid enemy appearance lead time.");
            this.enemyLeadTime = enemyLeadTime;
            if (notes == null || notes.Length == 0 || !window.IsValid
                || !(moveDuration > 0) || double.IsInfinity(moveDuration))
                throw new ArgumentException("Invalid movement chart or timing settings.");
            var earliestArrivals = new double[notes.Length + 1];
            for (int i = 0; i < notes.Length; i++)
            {
                MoveNote note = notes[i];
                if (string.IsNullOrWhiteSpace(note.destinationId) || note.HitTime < 0
                    || double.IsNaN(note.HitTime) || double.IsInfinity(note.HitTime)
                    || !Enum.IsDefined(typeof(MoveDirection), notes[i].direction))
                    throw new ArgumentException("Invalid movement note.");
                if (double.IsNaN(note.appearTime) || double.IsInfinity(note.appearTime)
                    || note.appearTime < 0 || note.appearTime > note.HitTime)
                    throw new ArgumentException("방 등장은 첫 정확 판정 이후일 수 없습니다.");
                if (note.hasDoor && (!(note.moveDelay > 0) || double.IsNaN(note.doorTime) || double.IsInfinity(note.doorTime) || note.doorTime < 0))
                    throw new ArgumentException("문 정확 판정은 이동보다 앞서야 합니다.");
                if (note.hasDoor && (double.IsNaN(note.DoorAppearsAt) || double.IsInfinity(note.DoorAppearsAt)
                    || note.DoorAppearsAt < 0 || note.DoorAppearsAt > note.doorTime))
                    throw new ArgumentException("문 등장은 문 판정 이전의 유효한 시각이어야 합니다.");
                if (i > 0 && note.HitTime <= notes[i - 1].HitTime)
                    throw new ArgumentException("방 이동 정확 판정은 시간순으로 배치해야 합니다.");
                double earliestInput = Math.Max(earliestArrivals[i], Math.Max(note.appearTime, note.HitTime - window.early));
                if (note.hasDoor) earliestInput = Math.Max(earliestInput, note.doorTime - window.early);
                if (earliestInput >= note.HitTime + window.late)
                    throw new ArgumentException("이전 이동 애니메이션이 끝난 뒤 다음 이동을 입력할 수 있는 시간이 없습니다.");
                earliestArrivals[i + 1] = earliestInput + moveDuration;
            }
            this.notes = (MoveNote[])notes.Clone();
            this.window = window;
            this.moveDuration = moveDuration;
            brokenDoors = new bool[notes.Length];
            this.enemies = enemies == null ? Array.Empty<EnemyNote>() : (EnemyNote[])enemies.Clone();
            enemyRooms = new int[this.enemies.Length];
            defeatedEnemies = new bool[this.enemies.Length];
            var roomIds = new Dictionary<string, int> { { startingRoomId, 0 } };
            for (int i = 0; i < notes.Length; i++) roomIds.Add(notes[i].destinationId, i + 1);
            var enemyIds = new HashSet<string>();
            for (int i = 0; i < this.enemies.Length; i++)
            {
                EnemyNote enemy = this.enemies[i];
                if (string.IsNullOrWhiteSpace(enemy.id) || !enemyIds.Add(enemy.id)
                    || enemy.roomId == null || !roomIds.TryGetValue(enemy.roomId, out int room)
                    || !Enum.IsDefined(typeof(EnemyDirection), enemy.direction)
                    || enemy.time < 0 || double.IsNaN(enemy.time) || double.IsInfinity(enemy.time))
                    throw new ArgumentException("Invalid enemy ID, room, direction or time.");
                enemyRooms[i] = room;
                if (enemy.customAppearance && (double.IsNaN(enemy.appearanceTime) || double.IsInfinity(enemy.appearanceTime) || enemy.appearanceTime < 0 || enemy.appearanceTime > enemy.time))
                    throw new ArgumentException("Invalid enemy appearance time.");
                double earliestShot = Math.Max(earliestArrivals[room], enemy.customAppearance ? enemy.appearanceTime : enemy.time - enemyLeadTime);
                if (earliestShot >= enemy.time + window.late)
                    throw new ArgumentException("입장 및 적 등장 이후 사격할 수 있는 시간이 없습니다.");
                if (room < notes.Length)
                {
                    double next = notes[room].hasDoor ? notes[room].doorTime : notes[room].HitTime;
                    if (enemy.time >= next)
                        throw new ArgumentException("Enemy judgment must precede the next door or movement.");
                }
            }
            Reset();
        }

        public void Reset()
        {
            Phase = RunPhase.Ready;
            Failure = FailureReason.None;
            Death = DeathPresentation.None; FailedEnemy = -1; DeathTime = 0; movementInput = null;
            LastGrade = TimingGrade.None;
            CompletedMoves = 0;
            MoveStartedAt = 0;
            JudgmentVersion = 0;
            LastJudgedAt = 0;
            LastTimingErrorMs = null;
            RoomArrivedAt = 0;
            Array.Clear(brokenDoors, 0, brokenDoors.Length);
            Array.Clear(defeatedEnemies, 0, defeatedEnemies.Length);
            lastTime = double.NegativeInfinity;
        }

        public void Begin()
        {
            Reset();
            Phase = RunPhase.Waiting;
        }

        public void Advance(double time)
        {
            if (!IsActive || time < lastTime || double.IsNaN(time)) return;
            lastTime = time;
            if (Phase == RunPhase.Moving && time >= MoveEndsAt)
            {
                RoomArrivedAt = MoveEndsAt;
                CompletedMoves++;
                Phase = RunPhase.Waiting;
            }
            if (Phase == RunPhase.Waiting)
            {
                int enemy = NextEnemyIndex();
                if (enemy >= 0 && time > enemies[enemy].time + window.late)
                {
                    Die(FailureReason.MissedEnemy, TimingGrade.TooLate, fatalAt: enemies[enemy].time + window.late);
                    return;
                }
                if (CompletedMoves == notes.Length)
                {
                    if (enemy < 0) Phase = RunPhase.Cleared;
                    return;
                }
                MoveNote next = notes[CompletedMoves];
                if (next.hasDoor && !brokenDoors[CompletedMoves] && time > next.doorTime + window.late)
                    Die(FailureReason.MissedDoor, TimingGrade.TooLate, fatalAt: next.doorTime + window.late);
                else if (time > next.HitTime + window.late)
                    Die(FailureReason.TooLate, TimingGrade.TooLate, fatalAt: next.HitTime + window.late);
            }
        }

        public void Press(MoveDirection direction, double time)
        {
            movementInput = direction;
            try { PressMovement(direction, time); }
            finally { movementInput = null; }
        }

        private void PressMovement(MoveDirection direction, double time)
        {
            if (time < lastTime || double.IsNaN(time)) return;
            if (Phase == RunPhase.Waiting && CompletedMoves < notes.Length)
            {
                MoveNote entrance = notes[CompletedMoves];
                if (entrance.direction == direction && entrance.hasDoor && !brokenDoors[CompletedMoves])
                {
                    lastTime = time;
                    Die(FailureReason.DoorCollision, window.Judge(time, entrance.HitTime), (time - entrance.HitTime) * 1000);
                    return;
                }
            }
            Advance(time);
            if (Phase != RunPhase.Waiting) return;
            if (CompletedMoves == notes.Length)
            {
                Die(FailureReason.WrongDirection, TimingGrade.None);
                return;
            }
            MoveNote note = notes[CompletedMoves];
            if (direction != note.direction)
            {
                Die(FailureReason.WrongDirection, TimingGrade.None);
                return;
            }
            LastGrade = window.Judge(time, note.HitTime);
            if (LastGrade == TimingGrade.TooEarly)
                Die(FailureReason.TooEarly, LastGrade, (time - note.HitTime) * 1000);
            else
            {
                if (NextEnemyIndex() >= 0)
                {
                    Die(FailureReason.MissedEnemy, TimingGrade.TooLate);
                    return;
                }
                if (note.hasDoor && !brokenDoors[CompletedMoves])
                {
                    Die(FailureReason.DoorCollision, LastGrade, (time - note.HitTime) * 1000);
                    return;
                }
                MoveStartedAt = time;
                Phase = RunPhase.Moving;
                LastJudgedAt = time;
                LastTimingErrorMs = (time - note.HitTime) * 1000;
                JudgmentVersion++;
            }
        }

        public bool DoorBroken(int index) => brokenDoors[index];

        public bool RoomVisible(int index, double time, int previousOccurrence = -1)
        {
            if (index == CompletedMoves) return true;
            return index > CompletedMoves && index <= notes.Length && Phase != RunPhase.Ready
                && previousOccurrence < CompletedMoves && time >= notes[index - 1].appearTime;
        }

        public bool RoomFrameVisible(int index, double time, bool door = false)
        {
            if (Phase == RunPhase.Ready || index <= CompletedMoves || index > notes.Length) return false;
            MoveNote note = notes[index - 1];
            if (door && (!note.hasDoor || brokenDoors[index - 1])) return false;
            if (!door && Phase == RunPhase.Moving && index == CompletedMoves + 1) return false;
            double start = note.customAppearance ? (door ? note.doorFrameStartTime : note.frameStartTime) : note.appearTime;
            return time >= start;
        }

        public int EnemyRoom(int index) => enemyRooms[index];
        public bool EnemyDefeated(int index) => defeatedEnemies[index];
        public double EnemyVisualAppearsAt(int index) => enemies[index].customAppearance
            ? enemies[index].appearanceTime : enemies[index].time - enemyLeadTime;
        public bool EnemyVisible(int index, double time) => Phase != RunPhase.Ready
            && enemyRooms[index] >= CompletedMoves && !defeatedEnemies[index] && time >= EnemyVisualAppearsAt(index);
        public bool EnemyRoomEntered(int index) => enemyRooms[index] == CompletedMoves;
        public double EnemyAppearsAt(int index) => Math.Max(RoomArrivedAt, enemies[index].customAppearance ? enemies[index].appearanceTime : enemies[index].time - enemyLeadTime);
        public bool EnemyAvailable(int index) => Phase == RunPhase.Waiting
            && enemyRooms[index] == CompletedMoves && !defeatedEnemies[index] && lastTime >= EnemyAppearsAt(index);

        public int NextEnemyIndex()
        {
            int next = -1;
            for (int i = 0; i < enemies.Length; i++)
                if (enemyRooms[i] == CompletedMoves && !defeatedEnemies[i]
                    && (next < 0 || enemies[i].time < enemies[next].time)) next = i;
            return next;
        }

        public bool ShootEnemy(int index, double time)
        {
            if (time < lastTime || double.IsNaN(time)) return false;
            Advance(time);
            if (index < 0 || index >= enemies.Length || !EnemyAvailable(index)) return false;
            TimingGrade grade = window.Judge(time, enemies[index].time);
            if (grade == TimingGrade.TooEarly || grade == TimingGrade.TooLate)
            { FailedEnemy = index; Die(grade == TimingGrade.TooEarly ? FailureReason.TooEarly : FailureReason.TooLate, grade, (time - enemies[index].time) * 1000); return false; }
            defeatedEnemies[index] = true;
            LastGrade = grade;
            LastJudgedAt = time;
            LastTimingErrorMs = (time - enemies[index].time) * 1000;
            JudgmentVersion++;
            Advance(time);
            return true;
        }

        public bool ShootDoor(int index, double time)
        {
            if (time < lastTime || double.IsNaN(time)) return false;
            Advance(time);
            if (!IsActive || index < CompletedMoves || index >= notes.Length) return false;
            MoveNote note = notes[index];
            if (!note.hasDoor || brokenDoors[index] || time < note.DoorAppearsAt) return false;
            TimingGrade grade = window.Judge(time, note.doorTime);
            if (grade == TimingGrade.TooEarly || grade == TimingGrade.TooLate)
                return false;
            brokenDoors[index] = true;
            LastGrade = grade;
            LastJudgedAt = time;
            LastTimingErrorMs = (time - note.doorTime) * 1000;
            JudgmentVersion++;
            return true;
        }

        private void Die(FailureReason reason, TimingGrade grade, double? errorMs = null, double? fatalAt = null)
        {
            DeathTime = fatalAt ?? lastTime;
            if (reason == FailureReason.MissedEnemy && FailedEnemy < 0) FailedEnemy = NextEnemyIndex();
            DeathDirection = movementInput ?? MoveDirection.Up;
            bool earlyExit = movementInput.HasValue && reason == FailureReason.TooEarly
                && CompletedMoves < notes.Length && movementInput.Value == notes[CompletedMoves].direction
                && (!notes[CompletedMoves].hasDoor || brokenDoors[CompletedMoves]);
            Death = earlyExit ? DeathPresentation.Departure
                : movementInput.HasValue ? DeathPresentation.Collision : DeathPresentation.Execution;
            Phase = RunPhase.Dead;
            Failure = reason;
            LastGrade = grade;
            LastTimingErrorMs = errorMs;
        }

        public void MissShot(double time)
        {
            Advance(time);
        }

    }
}
