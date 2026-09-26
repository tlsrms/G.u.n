using System;
using System.Collections.Generic;

namespace Gun.RoomRhythm
{
    public enum RunPhase { Ready, Waiting, Moving, Dead, Cleared }
    public enum FailureReason { None, WrongDirection, TooEarly, TooLate, MissedDoor, MissedEnemy }

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

        public RunPhase Phase { get; private set; }
        public FailureReason Failure { get; private set; }
        public TimingGrade LastGrade { get; private set; }
        public int CompletedMoves { get; private set; }
        public double MoveStartedAt { get; private set; }
        public int JudgmentVersion { get; private set; }
        public double LastJudgedAt { get; private set; }
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
            for (int i = 0; i < notes.Length; i++)
            {
                MoveNote note = notes[i];
                if (string.IsNullOrWhiteSpace(note.destinationId) || note.HitTime < 0
                    || double.IsNaN(note.HitTime) || double.IsInfinity(note.HitTime)
                    || !Enum.IsDefined(typeof(MoveDirection), notes[i].direction))
                    throw new ArgumentException("Invalid movement note.");
                if (double.IsNaN(note.appearTime) || double.IsInfinity(note.appearTime)
                    || note.appearTime < 0 || note.appearTime >= (note.hasDoor ? note.doorTime : note.HitTime) - window.early)
                    throw new ArgumentException("Room must appear before its first success window.");
                if (note.hasDoor && (!(note.moveDelay > 0) || note.doorTime + window.late > note.HitTime - window.early))
                    throw new ArgumentException("Door and movement success windows overlap.");
                double firstWindow = (note.hasDoor ? note.doorTime : note.HitTime) - window.early;
                if (i > 0 && firstWindow < notes[i - 1].HitTime + window.late + moveDuration)
                    throw new ArgumentException("Movement windows overlap the previous animation.");
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
            var positions = new HashSet<string>();
            if (!(enemyReadTime > 0) || double.IsInfinity(enemyReadTime))
                throw new ArgumentException("Invalid enemy reading time.");
            for (int i = 0; i < this.enemies.Length; i++)
            {
                EnemyNote enemy = this.enemies[i];
                if (string.IsNullOrWhiteSpace(enemy.id) || !enemyIds.Add(enemy.id)
                    || enemy.roomId == null || !roomIds.TryGetValue(enemy.roomId, out int room)
                    || !Enum.IsDefined(typeof(EnemyDirection), enemy.direction)
                    || double.IsNaN(enemy.time) || double.IsInfinity(enemy.time))
                    throw new ArgumentException("Invalid enemy ID, room, direction or time.");
                enemyRooms[i] = room;
                if (!positions.Add(enemy.roomId + ":" + enemy.direction))
                    throw new ArgumentException("Enemies in one room cannot share a direction.");
                double latestArrival = room == 0 ? 0 : notes[room - 1].HitTime + window.late + moveDuration;
                if (enemy.customAppearance && (double.IsNaN(enemy.appearanceTime) || double.IsInfinity(enemy.appearanceTime) || enemy.appearanceTime < 0))
                    throw new ArgumentException("Invalid enemy appearance time.");
                double latestAppearance = Math.Max(latestArrival, enemy.customAppearance ? enemy.appearanceTime : enemy.time - enemyLeadTime);
                if (enemy.time - window.early < latestAppearance + enemyReadTime)
                    throw new ArgumentException("Insufficient enemy reading time after latest arrival.");
                if (room < notes.Length)
                {
                    double next = notes[room].hasDoor ? notes[room].doorTime : notes[room].HitTime;
                    if (enemy.time + window.late > next - window.early)
                        throw new ArgumentException("Enemy window overlaps the next door or movement.");
                }
            }
            Reset();
        }

        public void Reset()
        {
            Phase = RunPhase.Ready;
            Failure = FailureReason.None;
            LastGrade = TimingGrade.None;
            CompletedMoves = 0;
            MoveStartedAt = 0;
            JudgmentVersion = 0;
            LastJudgedAt = 0;
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
                if (enemy >= 0 && time >= enemies[enemy].time + window.late)
                {
                    Die(FailureReason.MissedEnemy, TimingGrade.TooLate);
                    return;
                }
                if (CompletedMoves == notes.Length)
                {
                    if (enemy < 0) Phase = RunPhase.Cleared;
                    return;
                }
                MoveNote next = notes[CompletedMoves];
                if (next.hasDoor && !brokenDoors[CompletedMoves] && time >= next.doorTime + window.late)
                    Die(FailureReason.MissedDoor, TimingGrade.TooLate);
                else if (time >= next.HitTime + window.late)
                    Die(FailureReason.TooLate, TimingGrade.TooLate);
            }
        }

        public void Press(MoveDirection direction, double time)
        {
            if (time < lastTime || double.IsNaN(time)) return;
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
                Die(FailureReason.TooEarly, LastGrade);
            else
            {
                MoveStartedAt = time;
                Phase = RunPhase.Moving;
                LastJudgedAt = time;
                JudgmentVersion++;
            }
        }

        public bool DoorBroken(int index) => brokenDoors[index];

        public int EnemyRoom(int index) => enemyRooms[index];
        public bool EnemyDefeated(int index) => defeatedEnemies[index];
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
            if (grade == TimingGrade.TooEarly || grade == TimingGrade.TooLate) return false;
            defeatedEnemies[index] = true;
            LastGrade = grade;
            LastJudgedAt = time;
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
            if (!note.hasDoor || brokenDoors[index] || time < note.appearTime) return false;
            TimingGrade grade = window.Judge(time, note.doorTime);
            if (grade == TimingGrade.TooEarly || grade == TimingGrade.TooLate) return false;
            brokenDoors[index] = true;
            LastGrade = grade;
            LastJudgedAt = time;
            JudgmentVersion++;
            return true;
        }

        private void Die(FailureReason reason, TimingGrade grade)
        {
            Phase = RunPhase.Dead;
            Failure = reason;
            LastGrade = grade;
        }
    }
}
