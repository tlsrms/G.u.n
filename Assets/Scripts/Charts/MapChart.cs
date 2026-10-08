using System;
using System.Collections.Generic;

namespace Gun.RoomRhythm
{
    [Serializable] public sealed class MapRoom
    {
        public string id;
        public int x, y;
        public float width, height, offsetX, offsetY;
        public double moveDuration;
        public MovementEase moveEase;
        public double Duration(double fallback) => moveDuration == 0 ? fallback : moveDuration;
        public double hitBeat = 8, frameBeat = 4;
        // Legacy fields are retained only to migrate existing saved drafts.
        public string groupId;
        public bool individualAppearance;
        public double appearBeat;
        public bool door;
        public double doorBeat = 6, doorFrameBeat = 4;
        internal MapRoom Copy() => (MapRoom)MemberwiseClone();
    }
    [Serializable] public sealed class MapEnemy
    {
        public string id, roomId;
        public EnemyDirection direction;
        public EnemyPlacement placement;
        public double hitBeat = 10, appearBeat = 8, frameBeat = 8;
        public string PositionLabel => placement.useCoordinates
            ? string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##})", placement.x, placement.y)
            : direction.ToString();
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

        public static MapChart CreateEmpty(BeatChart settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrWhiteSpace(settings.startingRoomId)) settings.startingRoomId = "start";
            double startBeat = settings.Beat(0);
            return new MapChart {
                settings = settings,
                rooms = new[] { new MapRoom {
                    id = settings.startingRoomId, individualAppearance = true,
                    appearBeat = startBeat, hitBeat = startBeat, frameBeat = startBeat,
                    doorBeat = startBeat, doorFrameBeat = startBeat
                } }
            };
        }

        public MapRoom Room(string id) => Array.Find(rooms, r => r.id == id);
        public string EnemyRoomAt(double judgmentBeat, string preferredRoomId = null)
        {
            string result = settings.startingRoomId;
            double latest = settings.Beat(0);
            foreach (var room in rooms)
            {
                if (room.id == settings.startingRoomId || room.hitBeat > judgmentBeat || room.hitBeat < latest) continue;
                // Same-beat duplicates retain their explicit owner until the author separates their timings.
                if (room.hitBeat == latest && room.id != preferredRoomId) continue;
                latest = room.hitBeat; result = room.id;
            }
            return result;
        }
        public bool NeedsEnemyRoomSynchronization => Array.Exists(enemies,
            enemy => enemy != null && (enemy.roomId != EnemyRoomAt(enemy.hitBeat, enemy.roomId) || enemy.frameBeat != enemy.appearBeat));
        public void SynchronizeEnemyRooms()
        {
            foreach (var enemy in enemies)
                if (enemy != null) { enemy.roomId = EnemyRoomAt(enemy.hitBeat, enemy.roomId); enemy.frameBeat = enemy.appearBeat; }
        }
        public double AppearanceBeat(MapRoom room)
        {
            if (room.individualAppearance) return room.appearBeat;
            var group = Array.Find(groups ?? Array.Empty<MapGroup>(), g => g.id == room.groupId);
            return group != null ? group.appearBeat : room.frameBeat;
        }
        public bool NeedsAppearanceMigration => Array.Exists(rooms, r => !r.individualAppearance);
        public bool NeedsRoomStartSynchronization => Array.Exists(rooms,
            room => room.frameBeat != AppearanceBeat(room));
        public void SetRoomStart(MapRoom room, double beat)
        {
            room.individualAppearance = true;
            room.appearBeat = room.frameBeat = beat;
        }
        public void SynchronizeRoomStarts()
        {
            foreach (var room in rooms) SetRoomStart(room, AppearanceBeat(room));
        }
        public void MigrateAppearance()
        {
            foreach (var room in rooms)
            {
                SetRoomStart(room, AppearanceBeat(room));
                room.groupId = null;
            }
            groups = Array.Empty<MapGroup>();
        }
        public float WorldX(MapRoom room) => originX + room.x * roomSize + room.offsetX;
        public float WorldY(MapRoom room) => originY + room.y * roomSize + room.offsetY;
        public float Width(MapRoom room) => room.width == 0 ? roomSize : room.width;
        public float Height(MapRoom room) => room.height == 0 ? roomSize : room.height;
        public bool Connected(MapRoom a, MapRoom b) => Connection(a, b, out _);
        public bool Connection(MapRoom previous, MapRoom room, out MoveDirection direction)
        {
            float dx = WorldX(room) - WorldX(previous), dy = WorldY(room) - WorldY(previous);
            float halfWidth = (Width(previous) + Width(room)) * .5f, halfHeight = (Height(previous) + Height(room)) * .5f;
            bool horizontal = Math.Abs(Math.Abs(dx) - halfWidth) < .001f && Math.Abs(dy) < halfHeight - .001f;
            bool vertical = Math.Abs(Math.Abs(dy) - halfHeight) < .001f && Math.Abs(dx) < halfWidth - .001f;
            direction = horizontal ? (dx > 0 ? MoveDirection.Right : MoveDirection.Left) : (dy > 0 ? MoveDirection.Up : MoveDirection.Down);
            return horizontal || vertical;
        }
        // Snap to slots along each face. One default-size tile must share the face where sizes permit.
        public (float x, float y) AttachedPosition(MapRoom room, MapRoom previous, float desiredX, float desiredY, MoveDirection? face = null)
        {
            float px = WorldX(previous), py = WorldY(previous);
            float horizontal = (Width(previous) + Width(room)) * .5f;
            float vertical = (Height(previous) + Height(room)) * .5f;
            float x = px + FaceSlot(desiredX - px, Width(previous), Width(room));
            float y = py + FaceSlot(desiredY - py, Height(previous), Height(room));
            var best = (x: x, y: py + vertical);
            double distance = double.PositiveInfinity;
            void Consider(MoveDirection side, float candidateX, float candidateY)
            {
                if (face.HasValue && face.Value != side) return;
                double dx = candidateX - desiredX, dy = candidateY - desiredY;
                double next = dx * dx + dy * dy;
                if (next < distance) { distance = next; best = (candidateX, candidateY); }
            }
            Consider(MoveDirection.Up, x, py + vertical); Consider(MoveDirection.Right, px + horizontal, y);
            Consider(MoveDirection.Down, x, py - vertical); Consider(MoveDirection.Left, px - horizontal, y);
            return best;
        }
        private float FaceSlot(float desired, float previousSize, float size)
        {
            float limit = (previousSize + size) * .5f - Math.Min(roomSize, Math.Min(previousSize, size));
            return Math.Max(-limit, Math.Min(limit, -limit + (float)Math.Round((desired + limit) / roomSize, MidpointRounding.AwayFromZero) * roomSize));
        }
        public void SetPosition(MapRoom room, float x, float y)
        {
            room.x = (int)Math.Round((x - originX) / roomSize, MidpointRounding.AwayFromZero);
            room.y = (int)Math.Round((y - originY) / roomSize, MidpointRounding.AwayFromZero);
            room.offsetX = x - originX - room.x * roomSize;
            room.offsetY = y - originY - room.y * roomSize;
        }

        // Validate a detached layout before committing: failed insertion must not move existing rooms.
        public void InsertRoom(MapRoom candidate, double movementSeconds, bool moveFollowing = true)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.id) || Room(candidate.id) != null
                || double.IsNaN(candidate.hitBeat) || double.IsInfinity(candidate.hitBeat)
                || candidate.hitBeat <= settings.Beat(0))
                throw new ArgumentException("새 방의 ID와 이동 박자를 확인하세요.");
            var ordered = OrderedRooms();
            if (!(Width(candidate) > 0) || !(Height(candidate) > 0)
                || float.IsInfinity(Width(candidate)) || float.IsInfinity(Height(candidate)))
                throw new ArgumentException("방 크기는 유한한 양수여야 합니다.");
            MapRoom previous = ordered[0], next = null;
            foreach (var room in ordered)
            {
                if (room.id == settings.startingRoomId) continue;
                if (Math.Abs(room.hitBeat - candidate.hitBeat) < 1e-9)
                    throw new ArgumentException("같은 박에 두 방으로 이동할 수 없습니다.");
                if (room.hitBeat < candidate.hitBeat) previous = room;
                else { next = room; break; }
            }
            if (!Connected(previous, candidate))
                throw new ArgumentException("새 방은 입력한 박자의 시간순 이전 방 옆에 연결해야 합니다.");
            var trial = new MapChart { settings = settings, roomSize = roomSize, originX = originX, originY = originY,
                groups = groups, rooms = new MapRoom[rooms.Length + 1] };
            for (int i = 0; i < rooms.Length; i++) trial.rooms[i] = rooms[i].Copy();
            trial.rooms[rooms.Length] = candidate.Copy();
            if (next != null && !Connected(candidate, next))
            {
                if (!moveFollowing)
                    throw new ArgumentException("다음 방과 연결되지 않습니다. '뒤쪽 방을 함께 밀어서 연결'을 켜세요.");
                if (!Connection(previous, next, out var direction))
                    throw new ArgumentException("기존 이전/다음 방의 연결을 먼저 확인하세요.");
                var attached = AttachedPosition(next, candidate, WorldX(next), WorldY(next), direction);
                float dx = attached.x - WorldX(next), dy = attached.y - WorldY(next);
                foreach (var room in trial.rooms)
                    if (room.id != candidate.id && room.id != settings.startingRoomId && room.hitBeat >= next.hitBeat)
                        trial.SetPosition(room, trial.WorldX(room) + dx, trial.WorldY(room) + dy);
            }
            trial.ValidateRoomReuse(movementSeconds);
            var result = new MapRoom[rooms.Length + 1];
            for (int i = 0; i < rooms.Length; i++)
            {
                rooms[i].x = trial.rooms[i].x; rooms[i].y = trial.rooms[i].y;
                rooms[i].offsetX = trial.rooms[i].offsetX; rooms[i].offsetY = trial.rooms[i].offsetY;
                result[i] = rooms[i];
            }
            result[rooms.Length] = candidate;
            rooms = result;
        }
        public (float x, float y) PassagePosition(MapRoom previous, MapRoom room)
        {
            Connection(previous, room, out var direction);
            bool horizontal = direction == MoveDirection.Left || direction == MoveDirection.Right;
            float x = (Math.Max(WorldX(previous) - Width(previous) * .5f, WorldX(room) - Width(room) * .5f)
                + Math.Min(WorldX(previous) + Width(previous) * .5f, WorldX(room) + Width(room) * .5f)) * .5f;
            float y = (Math.Max(WorldY(previous) - Height(previous) * .5f, WorldY(room) - Height(room) * .5f)
                + Math.Min(WorldY(previous) + Height(previous) * .5f, WorldY(room) + Height(room) * .5f)) * .5f;
            return horizontal ? (WorldX(previous) + (direction == MoveDirection.Right ? 1 : -1) * Width(previous) * .5f, y)
                : (x, WorldY(previous) + (direction == MoveDirection.Up ? 1 : -1) * Height(previous) * .5f);
        }
        // A room's movement destination is the tile beside its next exit, not the rectangle's center.
        public (float x, float y) PlayerAnchor(MapRoom room, MapRoom next)
        {
            float x = WorldX(room), y = WorldY(room);
            if (next == null || !Connection(room, next, out var direction)) return (x, y);
            var passage = PassagePosition(room, next);
            float CellCenter(float coordinate, float center, float size)
            {
                float first = center - size * .5f + Math.Min(roomSize, size) * .5f;
                float last = center + size * .5f - Math.Min(roomSize, size) * .5f;
                return Math.Max(first, Math.Min(last, first + (float)Math.Round((coordinate - first) / roomSize,
                    MidpointRounding.AwayFromZero) * roomSize));
            }
            if (direction == MoveDirection.Left || direction == MoveDirection.Right)
                return (x + (direction == MoveDirection.Right ? 1 : -1) * (Width(room) - Math.Min(roomSize, Width(room))) * .5f,
                    CellCenter(passage.y, y, Height(room)));
            return (CellCenter(passage.x, x, Width(room)),
                y + (direction == MoveDirection.Up ? 1 : -1) * (Height(room) - Math.Min(roomSize, Height(room))) * .5f);
        }
        public static (float x, float y) PlayerMovePosition((float x, float y) from, (float x, float y) passage,
            (float x, float y) to, double progress)
        {
            double Distance((float x, float y) a, (float x, float y) b)
            {
                double dx = b.x - a.x, dy = b.y - a.y;
                return Math.Sqrt(dx * dx + dy * dy);
            }
            double first = Distance(from, passage), second = Distance(passage, to);
            double distance = Math.Max(0, Math.Min(1, progress)) * (first + second);
            var start = distance <= first ? from : passage;
            var end = distance <= first ? passage : to;
            double length = distance <= first ? first : second;
            double t = length > 0 ? (distance <= first ? distance : distance - first) / length : 1;
            return ((float)(start.x + (end.x - start.x) * t), (float)(start.y + (end.y - start.y) * t));
        }
        public bool Overlaps(MapRoom a, MapRoom b) =>
            Math.Abs(WorldX(a) - WorldX(b)) < (Width(a) + Width(b)) * .5f - .001f
            && Math.Abs(WorldY(a) - WorldY(b)) < (Height(a) + Height(b)) * .5f - .001f;
        public void Attach(MapRoom room, MapRoom previous, MoveDirection direction)
        {
            float dx = direction == MoveDirection.Right ? 1 : direction == MoveDirection.Left ? -1 : 0;
            float dy = direction == MoveDirection.Up ? 1 : direction == MoveDirection.Down ? -1 : 0;
            room.offsetX = WorldX(previous) + dx * (Width(previous) + Width(room)) * .5f - originX - room.x * roomSize;
            room.offsetY = WorldY(previous) + dy * (Height(previous) + Height(room)) * .5f - originY - room.y * roomSize;
        }
        public MapRoom[] OrderedRooms()
        {
            var ordered = (MapRoom[])rooms.Clone();
            Array.Sort(ordered, (a, b) => a.id == settings.startingRoomId ? (b.id == a.id ? 0 : -1)
                : b.id == settings.startingRoomId ? 1 : a.hitBeat.CompareTo(b.hitBeat));
            return ordered;
        }
        public string RoomLabel(MapRoom room, double? judgmentBeat = null)
        {
            int order = Array.IndexOf(OrderedRooms(), room) + 1;
            double beat = judgmentBeat ?? (room.id == settings.startingRoomId ? settings.Beat(0) : room.hitBeat);
            return order + "th/" + beat.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "beat";
        }
        public string EnemyLabel(MapEnemy enemy)
        {
            var room = Room(EnemyRoomAt(enemy.hitBeat, enemy.roomId));
            return (room != null ? RoomLabel(room, enemy.hitBeat) : "?/" + enemy.hitBeat.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "beat")
                + " · " + enemy.PositionLabel;
        }
        // A distinct room occurrence can reuse a tile after its previous occupant has disappeared.
        // Authoring and preview use on-beat movement. Runtime defers reuse until actual departure.
        public double DepartureBeat(MapRoom room, double movementSeconds)
        {
            var ordered = OrderedRooms(); int index = Array.IndexOf(ordered, room);
            return index >= 0 && index + 1 < ordered.Length
                ? ordered[index + 1].hitBeat + ordered[index + 1].Duration(movementSeconds) * settings.bpm / 60
                : double.PositiveInfinity;
        }
        public void ValidateRoomReuse(double movementSeconds)
        {
            var ordered = OrderedRooms();
            for (int i = 1; i < ordered.Length; i++)
                for (int j = 0; j < i; j++)
                {
                    if (!Overlaps(ordered[i], ordered[j])) continue;
                    // An early visual start is legal: the occupied tile postpones its rendering.
                    // Reject only when even the earliest departure misses the next judgment window.
                    double earliestFree = DepartureBeat(ordered[j], movementSeconds) - settings.toleranceBeats;
                    double deadline = (ordered[i].door ? ordered[i].doorBeat : ordered[i].hitBeat) + settings.toleranceBeats;
                    if (earliestFree > deadline + 1e-9)
                        throw new ArgumentException(RoomLabel(ordered[i]) + ": 좌표 (" + ordered[i].x + ", " + ordered[i].y
                            + ")의 이전 방에서 가장 빨리 나와도 " + earliestFree.ToString("0.######")
                            + "박입니다. 새 방의 " + (ordered[i].door ? "문 사격" : "이동") + " 허용 시각 "
                            + deadline.ToString("0.######") + "박보다 늦습니다. 정확 판정 박을 뒤로 옮기세요.");
                }
        }

        public double VisibleAppearanceBeat(MapRoom room, double movementSeconds)
        {
            double start = AppearanceBeat(room);
            foreach (var previous in OrderedRooms())
            {
                if (previous == room) break;
                if (Overlaps(previous, room))
                    start = Math.Max(start, DepartureBeat(previous, movementSeconds));
            }
            return start;
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
            => Require(Finite(beat) && Finite(s.Seconds(beat)) && s.Seconds(beat) >= 0, label + ": 맵 시작 이후의 유효한 시각이 필요합니다.");
        private static void Frame(BeatChart s, double appearance, double start, double hit, string label)
        {
            Time(s, appearance, label); Time(s, start, label); Time(s, hit, label);
            Require(appearance <= hit && start <= hit, label + ": 등장과 판정선 시작은 정확 입력 이후일 수 없습니다.");
        }
        public static CompiledBeatChart Compile(MapChart map, double moveDuration, double readTime, double musicLength, double lineWidth)
        {
            Require(map != null && map.settings != null && map.rooms != null && map.enemies != null && map.cameras != null && map.shakes != null, "맵 데이터가 없습니다.");
            var s = map.settings;
            musicLength += s.musicDelaySeconds;
            if (s.loopMusic) musicLength = double.PositiveInfinity;
            Require(Finite(map.roomSize) && map.roomSize > 0 && Finite(map.originX) && Finite(map.originY), "방 크기/원점이 잘못되었습니다.");
            var rooms = new Dictionary<string, MapRoom>();
            foreach (var room in map.rooms)
            {
                Require(room != null && !string.IsNullOrWhiteSpace(room.id) && !rooms.ContainsKey(room.id), "방 ID가 없거나 중복됩니다.");
                rooms.Add(room.id, room);
                Require(Finite(map.Width(room)) && map.Width(room) > 0 && Finite(map.Height(room)) && map.Height(room) > 0
                    && Finite(map.WorldX(room)) && Finite(map.WorldY(room)), "Invalid room dimensions/position: " + room.id);
                Require(Finite(room.moveDuration) && room.moveDuration >= 0 && Enum.IsDefined(typeof(MovementEase), room.moveEase), "Invalid movement profile: " + room.id);
            }
            Require(rooms.ContainsKey(s.startingRoomId ?? ""), "시작 방을 지정하세요.");
            var moves = new List<MapRoom>();
            foreach (var room in map.rooms) if (room.id != s.startingRoomId) moves.Add(room);
            moves.Sort((a, b) => a.hitBeat.CompareTo(b.hitBeat));
            var notes = new List<BeatNote>();
            MapRoom previous = rooms[s.startingRoomId];
            foreach (var room in moves)
            {
                Require(map.Connection(previous, room, out var direction), "시간순 다음 방은 변의 일부가 맞닿아야 합니다: " + room.id);
                Frame(s, map.AppearanceBeat(room), map.AppearanceBeat(room), room.hitBeat, "방 " + map.RoomLabel(room));
                notes.Add(new BeatNote { kind = BeatNoteKind.Move, roomId = room.id, beat = room.hitBeat, moveDirection = direction });
                if (room.door)
                {
                    Frame(s, room.doorFrameBeat, room.doorFrameBeat, room.doorBeat, "문 " + map.RoomLabel(room));
                    notes.Add(new BeatNote { kind = BeatNoteKind.Door, roomId = room.id, beat = room.doorBeat });
                }
                previous = room;
            }
            var enemyIds = new HashSet<string>();
            foreach (var enemy in map.enemies)
            {
                Require(enemy != null, "빈 적 데이터입니다.");
                string label = "적 " + map.EnemyLabel(enemy);
                Require(!string.IsNullOrWhiteSpace(enemy.id) && enemyIds.Add(enemy.id), label + ": 적 ID가 없거나 중복되었습니다.");
                Require(Enum.IsDefined(typeof(EnemyDirection), enemy.direction), label + ": 잘못된 적 방향입니다.");
                Require(enemy.placement.IsValid, label + ": 적 좌표는 유한한 값이어야 합니다.");
                Frame(s, enemy.appearBeat, enemy.appearBeat, enemy.hitBeat, label);
                notes.Add(new BeatNote { kind = BeatNoteKind.Enemy, roomId = map.EnemyRoomAt(enemy.hitBeat), enemyId = enemy.id,
                    enemyDirection = enemy.direction, enemyPlacement = enemy.placement, beat = enemy.hitBeat });
            }
            var beat = new BeatChart { bpm = s.bpm, offsetSeconds = s.offsetSeconds, musicDelaySeconds = s.musicDelaySeconds, loopMusic = s.loopMusic, beatsPerBar = s.beatsPerBar, subdivision = s.subdivision,
                roomLeadBeats = s.roomLeadBeats, enemyLeadBeats = s.enemyLeadBeats, notes = notes.ToArray() };
            var result = BeatChartCompiler.Compile(beat, s.startingRoomId);
            for (int i = 0; i < result.Moves.Length; i++)
            {
                var room = rooms[result.Moves[i].destinationId];
                result.Moves[i].duration = room.moveDuration;
                result.Moves[i].ease = room.moveEase;
                result.Moves[i].customAppearance = true;
                result.Moves[i].appearanceTime = result.Moves[i].appearTime = s.Seconds(map.AppearanceBeat(room));
                result.Moves[i].frameStartTime = result.Moves[i].appearanceTime;
                result.Moves[i].doorFrameStartTime = s.Seconds(room.doorFrameBeat);
                Require(result.Moves[i].HitTime <= musicLength, "음원 종료 이후의 방 이동: " + room.id);
            }
            for (int i = 0; i < result.Enemies.Length; i++)
            {
                var enemy = Array.Find(map.enemies, e => e.id == result.Enemies[i].id);
                result.Enemies[i].customAppearance = true;
                result.Enemies[i].appearanceTime = s.Seconds(enemy.appearBeat);
                result.Enemies[i].frameStartTime = result.Enemies[i].appearanceTime;
                Require(result.Enemies[i].time <= musicLength, "음원 종료 이후의 적: " + map.EnemyLabel(enemy));
                string owner = map.EnemyRoomAt(enemy.hitBeat);
                int nextIndex = owner == s.startingRoomId ? 0 : moves.FindIndex(room => room.id == owner) + 1;
                if (nextIndex < moves.Count)
                {
                    var next = moves[nextIndex];
                    double nextBeat = next.door ? next.doorBeat : next.hitBeat;
                    Require(enemy.hitBeat < nextBeat, "적 " + map.EnemyLabel(enemy) + ": 사격 정확 박은 다음 "
                        + (next.door ? "문 " : "방 ") + map.RoomLabel(next, nextBeat) + "보다 앞서야 합니다.");
                }
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
