using System;
using System.Collections.Generic;

namespace Gun.RoomRhythm
{
    public enum BeatNoteKind { Move, Door, Enemy }

    [Serializable]
    public struct BeatNote
    {
        public BeatNoteKind kind;
        public double beat;
        public string roomId;
        public string enemyId;
        public MoveDirection moveDirection;
        public EnemyDirection enemyDirection;
    }

    [Serializable]
    public sealed class BeatChart
    {
        public double bpm = 120;
        public string startingRoomId = "start";
        public double offsetSeconds;
        public double musicDelaySeconds;
        public bool loopMusic;
        public int beatsPerBar = 4;
        public int subdivision = 4;
        public double toleranceBeats => JudgmentSettings.Window.early * bpm / 60;
        public double accurateBeats => JudgmentSettings.Window.accurate * bpm / 60;
        public double roomLeadBeats = 4;
        public double enemyLeadBeats = 3;
        public BeatNote[] notes = Array.Empty<BeatNote>();

        public double Seconds(double beat) => musicDelaySeconds + offsetSeconds + beat * 60.0 / bpm;
        public double Beat(double seconds) => (seconds - musicDelaySeconds - offsetSeconds) * bpm / 60.0;
        public double Snap(double beat) => Math.Round(beat * subdivision, MidpointRounding.AwayFromZero) / subdivision;
    }

    public sealed class CompiledBeatChart
    {
        public MoveNote[] Moves;
        public EnemyNote[] Enemies;
        public TimingWindow Timing;
        public double RoomLeadSeconds;
        public double EnemyLeadSeconds;
    }

    public static class BeatChartCompiler
    {
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public static CompiledBeatChart Compile(BeatChart chart, string startingRoomId)
        {
            if (chart == null || !Finite(chart.bpm) || chart.bpm <= 0 || chart.bpm > float.MaxValue || (float)chart.bpm == 0 || !Finite(chart.offsetSeconds)
                || chart.offsetSeconds < 0 || !Finite(chart.musicDelaySeconds) || chart.musicDelaySeconds < 0
                || chart.beatsPerBar < 1 || chart.subdivision < 1)
                throw new ArgumentException("BPM, 시작 오프셋, 박자/분할 설정을 확인하세요.");
            if (string.IsNullOrWhiteSpace(startingRoomId)) throw new ArgumentException("시작 방 ID가 필요합니다.");
            if (!Finite(chart.roomLeadBeats) || !Finite(chart.enemyLeadBeats) || chart.roomLeadBeats <= 0 || chart.enemyLeadBeats <= 0)
                throw new ArgumentException("방/적 등장 선행 박 수는 양수여야 합니다.");
            var ordered = new List<BeatNote>(chart.notes ?? Array.Empty<BeatNote>());
            ordered.Sort((a, b) => a.beat.CompareTo(b.beat));
            var rooms = new HashSet<string> { startingRoomId };
            var moves = new List<BeatNote>();
            var doors = new Dictionary<string, BeatNote>();
            var enemies = new List<EnemyNote>();
            var enemyIds = new HashSet<string>();
            foreach (BeatNote note in ordered)
            {
                if (!Finite(note.beat) || chart.Seconds(note.beat) < 0 || !Finite(chart.Seconds(note.beat)) || string.IsNullOrWhiteSpace(note.roomId)
                    || !Enum.IsDefined(typeof(BeatNoteKind), note.kind))
                    throw new ArgumentException("노트의 박 위치, 종류와 방 ID를 확인하세요.");
                if (note.kind == BeatNoteKind.Move)
                {
                    if (!rooms.Add(note.roomId)) throw new ArgumentException("중복된 이동 방 ID: " + note.roomId);
                    if (!Enum.IsDefined(typeof(MoveDirection), note.moveDirection)) throw new ArgumentException("잘못된 이동 방향: " + note.roomId);
                    if (moves.Count > 0 && moves[moves.Count - 1].beat == note.beat)
                        throw new ArgumentException("같은 박자에 두 방으로 이동할 수 없습니다.");
                    moves.Add(note);
                }
                else if (note.kind == BeatNoteKind.Door)
                {
                    if (doors.ContainsKey(note.roomId)) throw new ArgumentException("한 방 입구에 문이 중복되었습니다: " + note.roomId);
                    doors.Add(note.roomId, note);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(note.enemyId) || !enemyIds.Add(note.enemyId))
                        throw new ArgumentException("적 ID가 없거나 중복되었습니다: " + note.enemyId);
                    if (!Enum.IsDefined(typeof(EnemyDirection), note.enemyDirection)) throw new ArgumentException("잘못된 적 방향: " + note.enemyId);
                    enemies.Add(new EnemyNote { id = note.enemyId, roomId = note.roomId, direction = note.enemyDirection, time = chart.Seconds(note.beat) });
                }
            }
            if (moves.Count == 0) throw new ArgumentException("방 이동 노트를 하나 이상 배치하세요.");
            foreach (var pair in doors)
                if (!rooms.Contains(pair.Key) || pair.Key == startingRoomId) throw new ArgumentException("문이 연결될 이동 방이 없습니다: " + pair.Key);
            foreach (EnemyNote enemy in enemies)
                if (!rooms.Contains(enemy.roomId)) throw new ArgumentException("적의 방이 없습니다: " + enemy.roomId);
            double secondsPerBeat = 60 / chart.bpm;
            var result = new CompiledBeatChart {
                Moves = new MoveNote[moves.Count], Enemies = enemies.ToArray(),
                Timing = JudgmentSettings.Window,
                RoomLeadSeconds = chart.roomLeadBeats * secondsPerBeat, EnemyLeadSeconds = chart.enemyLeadBeats * secondsPerBeat
            };
            if (!result.Timing.IsValid || !Finite(result.RoomLeadSeconds) || !Finite(result.EnemyLeadSeconds)
                || result.RoomLeadSeconds > float.MaxValue || result.EnemyLeadSeconds > float.MaxValue)
                throw new ArgumentException("BPM과 허용 범위/등장 선행 박 수가 유효한 초 단위 범위를 벗어났습니다.");
            for (int i = 0; i < moves.Count; i++)
            {
                BeatNote move = moves[i];
                bool hasDoor = doors.TryGetValue(move.roomId, out BeatNote door);
                if (hasDoor && door.beat >= move.beat) throw new ArgumentException("문은 해당 방 이동보다 먼저 배치해야 합니다: " + move.roomId);
                double hit = chart.Seconds(move.beat);
                result.Moves[i] = new MoveNote { destinationId = move.roomId, direction = move.moveDirection, time = hit,
                    hasDoor = hasDoor, doorTime = hasDoor ? chart.Seconds(door.beat) : 0,
                    moveDelay = hasDoor ? (move.beat - door.beat) * secondsPerBeat : 0,
                    appearTime = Math.Max(0, hit - result.RoomLeadSeconds) };
            }
            return result;
        }

        public static BeatChart Import(double bpm, TimingWindow timing, double roomLead, double enemyLead,
            MoveNote[] moves, EnemyNote[] enemies)
        {
            if (!Finite(bpm) || bpm <= 0) throw new ArgumentException("BPM은 양수여야 합니다.");
            var chart = new BeatChart { bpm = bpm, roomLeadBeats = roomLead * bpm / 60, enemyLeadBeats = enemyLead * bpm / 60 };
            var notes = new List<BeatNote>();
            foreach (MoveNote move in moves ?? Array.Empty<MoveNote>())
            {
                notes.Add(new BeatNote { kind = BeatNoteKind.Move, beat = chart.Beat(move.HitTime), roomId = move.destinationId, moveDirection = move.direction });
                if (move.hasDoor) notes.Add(new BeatNote { kind = BeatNoteKind.Door, beat = chart.Beat(move.doorTime), roomId = move.destinationId });
            }
            foreach (EnemyNote enemy in enemies ?? Array.Empty<EnemyNote>())
                notes.Add(new BeatNote { kind = BeatNoteKind.Enemy, beat = chart.Beat(enemy.time), roomId = enemy.roomId, enemyId = enemy.id, enemyDirection = enemy.direction });
            chart.notes = notes.ToArray();
            return chart;
        }
    }
}
