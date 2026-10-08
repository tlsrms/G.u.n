using System;
using System.Collections.Generic;

namespace Gun.RoomRhythm
{
    // Two distinct pending shot times. Simultaneous targets share the same color.
    public struct ShotPreview
    {
        private const double SimultaneousTolerance = .000001;
        private double first, second;
        public static ShotPreview Empty => new ShotPreview {
            first = double.PositiveInfinity, second = double.PositiveInfinity
        };
        public void Include(double time)
        {
            if (time < first - SimultaneousTolerance) { second = first; first = time; }
            else if (time > first + SimultaneousTolerance && time < second) second = time;
        }
        // 0: red, 1: orange, 2: yellow. This never limits target visibility.
        public int Priority(double time) => Math.Abs(time - first) <= SimultaneousTolerance ? 0
            : Math.Abs(time - second) <= SimultaneousTolerance ? 1 : 2;
    }

    public enum RunPhase { Ready, Waiting, Moving, Dead, Cleared }
    public enum FailureReason { None, WrongDirection, TooEarly, TooLate, MissedDoor, MissedEnemy, DoorCollision }
    public enum DeathPresentation { None, Execution, Collision, Departure }

    public enum RoomActionKind { MoveStarted, MoveArrived, DoorBroken, EnemyDefeated, Failed }

    public readonly struct RoomActionResult
    {
        public readonly RoomActionKind Kind;
        public readonly string TargetId;
        public readonly double Time;
        public readonly TimingGrade Grade;
        public readonly FailureReason Failure;

        public RoomActionResult(RoomActionKind kind, string targetId, double time,
            TimingGrade grade = TimingGrade.None, FailureReason failure = FailureReason.None)
        { Kind = kind; TargetId = targetId; Time = time; Grade = grade; Failure = failure; }
    }

    // Pure state machine: caller submits timestamped input before advancing to frame time.
    public sealed class RoomRun
    {
        private ShotPreview deathPreview = ShotPreview.Empty;
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
        private readonly Queue<RoomActionResult> results = new Queue<RoomActionResult>();
        public bool TryDequeueResult(out RoomActionResult result)
        {
            if (results.Count > 0) { result = results.Dequeue(); return true; }
            result = default;
            return false;
        }
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
        public int AccurateJudgments { get; private set; }
        public double AccuracyPercent => JudgmentVersion == 0 ? 0 : AccurateJudgments * 100.0 / JudgmentVersion;
        public double LastJudgedAt { get; private set; }
        public double? LastTimingErrorMs { get; private set; }
        public double RoomArrivedAt { get; private set; }
        public double MoveEndsAt => MoveStartedAt + notes[Math.Min(CompletedMoves, notes.Length - 1)].Duration(moveDuration);
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
                if (double.IsNaN(note.duration) || double.IsInfinity(note.duration) || note.duration < 0
                    || !Enum.IsDefined(typeof(MovementEase), note.ease))
                    throw new ArgumentException("Invalid movement profile.");
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
                earliestArrivals[i + 1] = earliestInput + note.Duration(moveDuration);
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
                    || !enemy.placement.IsValid
                    || enemy.time < 0 || double.IsNaN(enemy.time) || double.IsInfinity(enemy.time))
                    throw new ArgumentException("Invalid enemy ID, room, direction, position or time.");
                enemyRooms[i] = room;
                if (enemy.customAppearance && (double.IsNaN(enemy.appearanceTime) || double.IsInfinity(enemy.appearanceTime) || enemy.appearanceTime < 0 || enemy.appearanceTime > enemy.time))
                    throw new ArgumentException("Invalid enemy appearance time.");
                double earliestEntry = room == 0 ? 0 : earliestArrivals[room] - notes[room - 1].Duration(moveDuration);
                double earliestShot = Math.Max(earliestEntry, enemy.customAppearance ? enemy.appearanceTime : enemy.time - enemyLeadTime);
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
            results.Clear();
            Phase = RunPhase.Ready;
            Failure = FailureReason.None;
            Death = DeathPresentation.None; FailedEnemy = -1; DeathTime = 0; movementInput = null;
            LastGrade = TimingGrade.None;
            CompletedMoves = 0;
            MoveStartedAt = 0;
            JudgmentVersion = 0;
            AccurateJudgments = 0;
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

        // Debug playback consumes scheduled actions before committing the frame clock.
        // It deliberately bypasses failure windows, including notes reached late after a long move.
        public void AdvanceAutomatically(double time, Action<RoomActionResult> presentAction = null)
        {
            if (!IsActive || time < lastTime || double.IsNaN(time) || double.IsInfinity(time)) return;
            while (IsActive)
            {
                int enemy = NextEnemyIndex();
                double enemyTime = enemy >= 0 ? Math.Max(enemies[enemy].time, EnemyAppearsAt(enemy)) : double.PositiveInfinity;
                double nextTime = Phase == RunPhase.Moving ? MoveEndsAt : double.PositiveInfinity;
                RoomActionKind kind = RoomActionKind.MoveArrived;
                if (Phase == RunPhase.Waiting && CompletedMoves < notes.Length)
                {
                    var note = notes[CompletedMoves];
                    kind = note.hasDoor && !brokenDoors[CompletedMoves] ? RoomActionKind.DoorBroken : RoomActionKind.MoveStarted;
                    nextTime = kind == RoomActionKind.DoorBroken ? note.doorTime : note.HitTime;
                    // Clear the current room before leaving, even for overlapping debug timings.
                    if (enemy >= 0) nextTime = Math.Max(nextTime, enemyTime);
                }
                if (enemyTime <= nextTime && enemy >= 0)
                {
                    nextTime = enemyTime;
                    kind = RoomActionKind.EnemyDefeated;
                }
                if (double.IsPositiveInfinity(nextTime))
                {
                    Phase = RunPhase.Cleared;
                    break;
                }
                nextTime = Math.Max(lastTime, nextTime);
                if (nextTime > time) break;
                lastTime = nextTime;
                string target;
                switch (kind)
                {
                    case RoomActionKind.EnemyDefeated:
                        defeatedEnemies[enemy] = true;
                        target = enemies[enemy].id;
                        break;
                    case RoomActionKind.DoorBroken:
                        brokenDoors[CompletedMoves] = true;
                        target = notes[CompletedMoves].destinationId;
                        break;
                    case RoomActionKind.MoveStarted:
                        target = notes[CompletedMoves].destinationId;
                        MoveStartedAt = nextTime;
                        Phase = RunPhase.Moving;
                        break;
                    default:
                        target = notes[CompletedMoves].destinationId;
                        RoomArrivedAt = nextTime;
                        CompletedMoves++;
                        Phase = RunPhase.Waiting;
                        break;
                }
                var grade = kind == RoomActionKind.MoveArrived ? TimingGrade.None : TimingGrade.Accurate;
                if (grade != TimingGrade.None)
                {
                    LastGrade = grade;
                    LastJudgedAt = nextTime;
                    LastTimingErrorMs = 0;
                    JudgmentVersion++;
                    AccurateJudgments++;
                }
                var result = new RoomActionResult(kind, target, nextTime, grade);
                results.Enqueue(result);
                presentAction?.Invoke(result);
            }
            lastTime = time;
        }

        public void Advance(double time)
        {
            if (!IsActive || time < lastTime || double.IsNaN(time)) return;
            lastTime = time;
            if (Phase == RunPhase.Moving && time >= MoveEndsAt)
            {
                RoomArrivedAt = MoveEndsAt;
                results.Enqueue(new RoomActionResult(RoomActionKind.MoveArrived,
                    notes[CompletedMoves].destinationId, RoomArrivedAt));
                CompletedMoves++;
                Phase = RunPhase.Waiting;
            }
            int enemy = NextEnemyIndex();
            if (enemy >= 0 && time > enemies[enemy].time + window.late)
            {
                Die(FailureReason.MissedEnemy, TimingGrade.TooLate, fatalAt: enemies[enemy].time + window.late);
                return;
            }
            if (Phase == RunPhase.Waiting)
            {
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
                if (LastGrade == TimingGrade.Accurate) AccurateJudgments++;
                results.Enqueue(new RoomActionResult(RoomActionKind.MoveStarted, note.destinationId, time, LastGrade));
            }
        }

        public bool DoorBroken(int index) => brokenDoors[index];

        public ShotPreview PreviewShots()
        {
            var preview = ShotPreview.Empty;
            if (Phase == RunPhase.Dead) return deathPreview;
            if (Phase == RunPhase.Ready || Phase == RunPhase.Cleared) return preview;
            for (int i = CompletedMoves; i < notes.Length; i++)
            {
                if (notes[i].hasDoor && !brokenDoors[i]) preview.Include(notes[i].doorTime);
            }
            for (int i = 0; i < enemies.Length; i++)
                if (enemyRooms[i] >= CompletedMoves && !defeatedEnemies[i]) preview.Include(enemies[i].time);
            return preview;
        }

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
        // Movement commits the destination for combat immediately; only the visual arrival is delayed.
        private int CombatRoom => CompletedMoves + (Phase == RunPhase.Moving ? 1 : 0);
        public double EnemyAppearsAt(int index) => Math.Max(Phase == RunPhase.Moving ? MoveStartedAt : RoomArrivedAt, EnemyVisualAppearsAt(index));
        public bool EnemyAvailable(int index) => IsActive
            && enemyRooms[index] == CombatRoom && !defeatedEnemies[index] && lastTime >= EnemyAppearsAt(index);

        public int NextEnemyIndex()
        {
            int next = -1;
            for (int i = 0; i < enemies.Length; i++)
                if (enemyRooms[i] == CombatRoom && !defeatedEnemies[i]
                    && (next < 0 || enemies[i].time < enemies[next].time)) next = i;
            return next;
        }

        public int NextPendingEnemyIndex()
        {
            int next = -1;
            for (int i = 0; i < enemies.Length; i++)
                if (enemyRooms[i] >= CompletedMoves && !defeatedEnemies[i]
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
            if (LastGrade == TimingGrade.Accurate) AccurateJudgments++;
            results.Enqueue(new RoomActionResult(RoomActionKind.EnemyDefeated, enemies[index].id, time, LastGrade));
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
            if (LastGrade == TimingGrade.Accurate) AccurateJudgments++;
            results.Enqueue(new RoomActionResult(RoomActionKind.DoorBroken, note.destinationId, time, LastGrade));
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
            Death = FailedEnemy >= 0 ? DeathPresentation.Execution : earlyExit ? DeathPresentation.Departure
                : movementInput.HasValue ? DeathPresentation.Collision : DeathPresentation.Execution;
            deathPreview = PreviewShots();
            Phase = RunPhase.Dead;
            Failure = reason;
            LastGrade = grade;
            LastTimingErrorMs = errorMs;
            string target = FailedEnemy >= 0 ? enemies[FailedEnemy].id
                : CompletedMoves < notes.Length ? notes[CompletedMoves].destinationId : null;
            results.Enqueue(new RoomActionResult(RoomActionKind.Failed, target, DeathTime, grade, reason));
        }

        public void MissShot(double time)
        {
            Advance(time);
        }

    }
}
