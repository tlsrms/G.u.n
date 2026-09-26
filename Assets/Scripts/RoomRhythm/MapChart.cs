using System;
using System.Collections.Generic;

namespace Gun.RoomRhythm
{
    [Serializable] public sealed class MapRoom
    {
        public string id;
        public int x, y;
        public double hitBeat = 8, frameBeat = 4;
        // Legacy fields are retained only to migrate existing saved drafts.
        public string groupId;
        public bool individualAppearance;
        public double appearBeat;
        public bool door;
        public double doorBeat = 6, doorFrameBeat = 4;
    }
    [Serializable] public sealed class MapEnemy
    {
        public string id, roomId;
        public EnemyDirection direction;
        public double hitBeat = 10, appearBeat = 8, frameBeat = 8;
    }
    [Serializable] public sealed class MapGroup
    {
        public string id, name;
        public double appearBeat;
    }
    public enum CameraEase { Linear, Smooth, EaseIn, EaseOut }
    [Serializable] public sealed class MapCameraKey
    {
        public string roomId;
        public double beat, duration = 1;
        public float x, y, size = 7;
        public CameraEase ease = CameraEase.Smooth;
    }
    [Serializable] public sealed class MapShake
    {
        public string roomId;
        public double beat, duration = 1;
        public float strength = .2f, frequency = 12;
    }
    public struct MapCameraPose { public float x, y, size; }

    // Editor draft and saved performance share this data and evaluator. No Unity dependencies.
    [Serializable] public sealed class MapChart
    {
        public BeatChart settings = new BeatChart();
        public float roomSize = 6;
        public float originX, originY;
        public float cameraX, cameraY, cameraSize = 7;
        public bool cameraTrack;
        public MapRoom[] rooms = Array.Empty<MapRoom>();
        public MapEnemy[] enemies = Array.Empty<MapEnemy>();
        public MapGroup[] groups = Array.Empty<MapGroup>();
        public MapCameraKey[] cameras = Array.Empty<MapCameraKey>();
        public MapShake[] shakes = Array.Empty<MapShake>();

        public MapRoom Room(string id) => Array.Find(rooms, r => r.id == id);
        public double AppearanceBeat(MapRoom room)
        {
            if (room.individualAppearance) return room.appearBeat;
            var group = Array.Find(groups ?? Array.Empty<MapGroup>(), g => g.id == room.groupId);
            return group != null ? group.appearBeat : room.frameBeat;
        }
        public bool NeedsAppearanceMigration => Array.Exists(rooms, r => !r.individualAppearance);
        public void MigrateAppearance()
        {
            foreach (var room in rooms)
            {
                if (!room.individualAppearance) room.appearBeat = AppearanceBeat(room);
                room.individualAppearance = true; room.groupId = null;
            }
            groups = Array.Empty<MapGroup>();
        }
        public float WorldX(MapRoom room) => originX + room.x * roomSize;
        public float WorldY(MapRoom room) => originY + room.y * roomSize;
        public MapRoom[] OrderedRooms()
        {
            var ordered = (MapRoom[])rooms.Clone();
            Array.Sort(ordered, (a, b) => a.id == settings.startingRoomId ? (b.id == a.id ? 0 : -1)
                : b.id == settings.startingRoomId ? 1 : a.hitBeat.CompareTo(b.hitBeat));
            return ordered;
        }
        public string RoomLabel(MapRoom room)
        {
            int order = Array.IndexOf(OrderedRooms(), room) + 1;
            double beat = room.id == settings.startingRoomId ? settings.Beat(0) : room.hitBeat;
            return order + "th/" + beat.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "beat";
        }
        // A distinct room occurrence can reuse a tile after its previous occupant has disappeared.
        // Include the latest successful input and movement animation in the occupied interval.
        public double DepartureBeat(MapRoom room, double movementSeconds)
        {
            var ordered = OrderedRooms(); int index = Array.IndexOf(ordered, room);
            return index >= 0 && index + 1 < ordered.Length
                ? ordered[index + 1].hitBeat + settings.toleranceBeats + movementSeconds * settings.bpm / 60
                : double.PositiveInfinity;
        }
        public void ValidateRoomReuse(double movementSeconds)
        {
            var ordered = OrderedRooms();
            for (int i = 1; i < ordered.Length; i++)
                for (int j = 0; j < i; j++)
                {
                    if (ordered[i].x != ordered[j].x || ordered[i].y != ordered[j].y) continue;
                    double free = DepartureBeat(ordered[j], movementSeconds);
                    if (AppearanceBeat(ordered[i]) < free)
                        throw new ArgumentException(RoomLabel(ordered[i]) + ": 같은 위치의 이전 방이 사라지는 " + free.ToString("0.###") + "박 이후에 방이 등장해야 합니다.");
                }
        }

        public MapCameraPose CameraAt(double beat, float fallbackX, float fallbackY)
        {
            var pose = new MapCameraPose { x = cameraTrack ? cameraX : fallbackX, y = cameraTrack ? cameraY : fallbackY, size = cameraSize };
            if (cameraTrack)
            {
                // Non-overlapping keys allow direct, allocation-free seeking in either direction.
                MapCameraKey key = null, previous = null;
                foreach (var candidate in cameras)
                    if (candidate.beat <= beat && (key == null || candidate.beat > key.beat)) key = candidate;
                if (key != null)
                {
                    foreach (var candidate in cameras)
                        if (candidate.beat < key.beat && (previous == null || candidate.beat > previous.beat)) previous = candidate;
                    if (previous != null) pose = new MapCameraPose { x = previous.x, y = previous.y, size = previous.size };
                    float t = key.duration <= 0 ? 1 : (float)Math.Min(1, (beat - key.beat) / key.duration);
                    if (key.ease == CameraEase.Smooth) t = t * t * (3 - 2 * t);
                    else if (key.ease == CameraEase.EaseIn) t *= t;
                    else if (key.ease == CameraEase.EaseOut) t = 1 - (1 - t) * (1 - t);
                    pose.x += (key.x - pose.x) * t; pose.y += (key.y - pose.y) * t; pose.size += (key.size - pose.size) * t;
                }
            }
            foreach (var shake in shakes)
            {
                double elapsed = (beat - shake.beat) * 60 / settings.bpm;
                if (beat < shake.beat || beat >= shake.beat + shake.duration) continue;
                double envelope = 1 - (beat - shake.beat) / shake.duration;
                pose.x += (float)(Math.Sin(elapsed * shake.frequency * Math.PI * 2) * shake.strength * envelope);
                pose.y += (float)(Math.Sin(elapsed * shake.frequency * Math.PI * 2 * 1.37) * shake.strength * envelope);
            }
            return pose;
        }
    }

    public static class MapChartCompiler
    {
        private static bool Finite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
        private static void Require(bool condition, string message) { if (!condition) throw new ArgumentException(message); }
        private static void Time(BeatChart s, double beat, string label)
            => Require(Finite(beat) && Finite(s.Seconds(beat)) && s.Seconds(beat) >= 0, label + ": 음악 시작 이후의 유효한 시각이 필요합니다.");
        private static void Frame(BeatChart s, double appearance, double start, double hit, string label)
        {
            Time(s, appearance, label); Time(s, start, label); Time(s, hit, label);
            Require(appearance <= hit && start <= hit, label + ": 등장과 판정선 시작은 정확 입력 이후일 수 없습니다.");
        }
        public static CompiledBeatChart Compile(MapChart map, double moveDuration, double readTime, double musicLength, double lineWidth)
        {
            Require(map != null && map.settings != null && map.rooms != null && map.enemies != null && map.cameras != null && map.shakes != null, "맵 데이터가 없습니다.");
            var s = map.settings;
            Require(Finite(map.roomSize) && map.roomSize > 0 && Finite(map.originX) && Finite(map.originY), "방 크기/원점이 잘못되었습니다.");
            var rooms = new Dictionary<string, MapRoom>();
            foreach (var room in map.rooms)
            {
                Require(room != null && !string.IsNullOrWhiteSpace(room.id) && !rooms.ContainsKey(room.id), "방 ID가 없거나 중복됩니다.");
                rooms.Add(room.id, room);
            }
            Require(rooms.ContainsKey(s.startingRoomId ?? ""), "시작 방을 지정하세요.");
            var moves = new List<MapRoom>();
            foreach (var room in map.rooms) if (room.id != s.startingRoomId) moves.Add(room);
            moves.Sort((a, b) => a.hitBeat.CompareTo(b.hitBeat));
            var notes = new List<BeatNote>();
            MapRoom previous = rooms[s.startingRoomId];
            foreach (var room in moves)
            {
                long dx = (long)room.x - previous.x, dy = (long)room.y - previous.y;
                Require(Math.Abs(dx) + Math.Abs(dy) == 1, "시간순 다음 방은 바로 옆 칸이어야 합니다: " + room.id);
                var direction = dx == 1 ? MoveDirection.Right : dx == -1 ? MoveDirection.Left : dy == 1 ? MoveDirection.Up : MoveDirection.Down;
                Frame(s, map.AppearanceBeat(room), room.frameBeat, room.hitBeat, "방 " + map.RoomLabel(room));
                notes.Add(new BeatNote { kind = BeatNoteKind.Move, roomId = room.id, beat = room.hitBeat, moveDirection = direction });
                if (room.door)
                {
                    Frame(s, map.AppearanceBeat(room), room.doorFrameBeat, room.doorBeat, "문 " + map.RoomLabel(room));
                    notes.Add(new BeatNote { kind = BeatNoteKind.Door, roomId = room.id, beat = room.doorBeat });
                }
                previous = room;
            }
            foreach (var enemy in map.enemies)
            {
                Require(enemy != null, "빈 적 데이터입니다.");
                Frame(s, enemy.appearBeat, enemy.frameBeat, enemy.hitBeat, "적 " + enemy.id);
                notes.Add(new BeatNote { kind = BeatNoteKind.Enemy, roomId = enemy.roomId, enemyId = enemy.id, enemyDirection = enemy.direction, beat = enemy.hitBeat });
            }
            var beat = new BeatChart { bpm = s.bpm, offsetSeconds = s.offsetSeconds, beatsPerBar = s.beatsPerBar, subdivision = s.subdivision,
                toleranceBeats = s.toleranceBeats, accurateBeats = s.accurateBeats, roomLeadBeats = s.roomLeadBeats, enemyLeadBeats = s.enemyLeadBeats, notes = notes.ToArray() };
            var result = BeatChartCompiler.Compile(beat, s.startingRoomId);
            for (int i = 0; i < result.Moves.Length; i++)
            {
                var room = rooms[result.Moves[i].destinationId];
                result.Moves[i].customAppearance = true;
                result.Moves[i].appearanceTime = result.Moves[i].appearTime = s.Seconds(map.AppearanceBeat(room));
                result.Moves[i].frameStartTime = s.Seconds(room.frameBeat);
                result.Moves[i].doorFrameStartTime = s.Seconds(room.doorFrameBeat);
                Require(result.Moves[i].HitTime + result.Timing.late + moveDuration <= musicLength, "음원 종료 이후의 방 이동: " + room.id);
            }
            for (int i = 0; i < result.Enemies.Length; i++)
            {
                var enemy = Array.Find(map.enemies, e => e.id == result.Enemies[i].id);
                result.Enemies[i].customAppearance = true;
                result.Enemies[i].appearanceTime = s.Seconds(enemy.appearBeat);
                result.Enemies[i].frameStartTime = s.Seconds(enemy.frameBeat);
                Require(result.Enemies[i].time + result.Timing.late <= musicLength, "음원 종료 이후의 적: " + enemy.id);
            }
            ValidateCamera(map, musicLength);
            new RoomRun(result.Moves, result.Timing, moveDuration, result.Enemies, s.startingRoomId, readTime, result.EnemyLeadSeconds);
            map.ValidateRoomReuse(moveDuration);
            return result;
        }
        private static void ValidateCamera(MapChart map, double length)
        {
            Require(Finite(map.cameraX) && Finite(map.cameraY) && Finite(map.cameraSize) && map.cameraSize > 0, "초기 카메라 설정이 잘못되었습니다.");
            var keys = (MapCameraKey[])map.cameras.Clone();
            foreach (var key in keys)
            {
                Require(key != null, "빈 카메라 이벤트입니다."); Time(map.settings, key.beat, "카메라");
                Require(Finite(key.duration) && key.duration >= 0 && Finite(key.x) && Finite(key.y) && Finite(key.size) && key.size > 0
                    && Enum.IsDefined(typeof(CameraEase), key.ease) && map.settings.Seconds(key.beat + key.duration) <= length, "카메라 위치/시간/크기를 확인하세요.");
            }
            Array.Sort(keys, (a, b) => a.beat.CompareTo(b.beat));
            for (int i = 1; i < keys.Length; i++) Require(keys[i].beat > keys[i - 1].beat && keys[i].beat >= keys[i - 1].beat + keys[i - 1].duration, "카메라 이동 이벤트가 겹칩니다.");
            foreach (var shake in map.shakes)
            {
                Require(shake != null, "빈 흔들림 이벤트입니다."); Time(map.settings, shake.beat, "흔들림");
                Require(Finite(shake.duration) && shake.duration > 0 && Finite(shake.strength) && shake.strength >= 0
                    && Finite(shake.frequency) && shake.frequency > 0 && map.settings.Seconds(shake.beat + shake.duration) <= length, "흔들림 시간/강도/빈도를 확인하세요.");
            }
        }
    }
}
