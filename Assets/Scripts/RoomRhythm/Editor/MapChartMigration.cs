using System;
using System.Collections.Generic;
using System.Reflection;

namespace Gun.RoomRhythm.Editor
{
    // Offline, Unity-independent migration. Never changes either input map.
    public static class MapChartMigration
    {
        public sealed class Result
        {
            public MapChart Map;
            public CompiledBeatChart Compiled;
            public string RemovedStartId, JoinedRoomId;
            public Dictionary<string, string> SecondRoomIds = new Dictionary<string, string>();
            public Dictionary<string, string> SecondGroupIds = new Dictionary<string, string>();
        }

        public static Result Merge(MapChart first, MapChart second, double duration, double readTime,
            double musicLength, double lineWidth, double boundarySeconds)
        {
            var a = MapChartCompiler.Compile(first, duration, readTime, musicLength, lineWidth);
            var b = MapChartCompiler.Compile(second, duration, readTime, musicLength, lineWidth);
            if (!Finite(boundarySeconds) || boundarySeconds <= 0) Fail("구간 경계 시각이 잘못되었습니다.");
            foreach (string field in new[] { "bpm", "offsetSeconds", "musicDelaySeconds", "loopMusic",
                "beatsPerBar", "subdivision", "roomLeadBeats", "enemyLeadBeats" })
                if (!Equals(typeof(BeatChart).GetField(field).GetValue(first.settings), typeof(BeatChart).GetField(field).GetValue(second.settings)))
                    Fail("서로 다른 곡/편집 설정: " + field);
            if (first.cameraTrack != second.cameraTrack || first.cameraSize != second.cameraSize)
                Fail("카메라 모드/기본 크기가 다릅니다. 동일 설정으로 정리한 뒤 이관하세요.");
            // Different tracked-camera baselines require a deliberate transition key, not a guessed jump.
            if (first.cameraTrack && (first.cameraX != second.cameraX || first.cameraY != second.cameraY))
                Fail("카메라 트랙의 초기 위치가 다릅니다. 카메라 전환을 먼저 명시하세요.");
            foreach (var move in a.Moves)
                if (move.HitTime + move.Duration(duration) > boundarySeconds) Fail("첫 구간 이동이 경계를 넘습니다.");
            foreach (var enemy in a.Enemies)
                if (enemy.time + a.Timing.late >= boundarySeconds) Fail("첫 구간 사격이 경계를 넘습니다.");
            if (!SectionTiming.CanEnter(b.Moves, b.Enemies, boundarySeconds, b.Timing))
                Fail("두 번째 구간의 판정창이 경계 전에 열립니다.");
            CheckCameraBoundary(first, boundarySeconds, before: true);
            CheckCameraBoundary(second, boundarySeconds, before: false);
            if (first.cameraTrack)
            {
                var pose = first.CameraAt(first.settings.Beat(boundarySeconds), first.cameraX, first.cameraY);
                if (!Near(pose.x, second.cameraX) || !Near(pose.y, second.cameraY) || !Near(pose.size, second.cameraSize))
                    Fail("첫 구간 마지막 카메라 자세가 다음 구간 초기 자세와 다릅니다. 전환 키를 먼저 정리하세요.");
            }

            var seam = first.Room(a.Moves[a.Moves.Length - 1].destinationId);
            var start = second.Room(second.settings.startingRoomId);
            if (!Near(first.WorldX(seam), second.WorldX(start)) || !Near(first.WorldY(seam), second.WorldY(start))
                || !Near(first.Width(seam), second.Width(start)) || !Near(first.Height(seam), second.Height(start)))
                Fail("첫 구간 마지막 방과 다음 시작 방의 위치/크기가 다릅니다.");
            var result = new Result { Map = Copy(first), RemovedStartId = start.id, JoinedRoomId = seam.id };
            var rooms = new List<MapRoom>(result.Map.rooms);
            var ids = new HashSet<string>(); foreach (var room in rooms) ids.Add(room.id);
            foreach (var room in second.rooms)
            {
                if (room.id == start.id) { result.SecondRoomIds.Add(room.id, seam.id); continue; }
                if (!ids.Add(room.id)) Fail("구간 사이에 중복된 방 ID가 있습니다: " + room.id);
                result.SecondRoomIds.Add(room.id, room.id);
                var copy = CopyRecord(room);
                copy.offsetX = second.WorldX(room) - first.originX - copy.x * first.roomSize;
                copy.offsetY = second.WorldY(room) - first.originY - copy.y * first.roomSize;
                copy.width = second.Width(room); copy.height = second.Height(room);
                rooms.Add(copy);
            }
            var groups = new List<MapGroup>(result.Map.groups);
            var groupIds = new HashSet<string>(); foreach (var group in groups) groupIds.Add(group.id);
            foreach (var group in second.groups ?? Array.Empty<MapGroup>())
            {
                var copy = CopyRecord(group);
                if (!groupIds.Add(copy.id)) Fail("구간 사이에 중복된 그룹 ID가 있습니다: " + copy.id);
                result.SecondGroupIds.Add(group.id, copy.id); groups.Add(copy);
            }
            var enemies = new List<MapEnemy>(result.Map.enemies);
            var enemyIds = new HashSet<string>(); foreach (var enemy in enemies) enemyIds.Add(enemy.id);
            foreach (var enemy in second.enemies)
            {
                if (!enemyIds.Add(enemy.id)) Fail("구간 사이에 중복된 적 ID가 있습니다: " + enemy.id);
                var copy = CopyRecord(enemy); copy.roomId = Remap(result, copy.roomId); enemies.Add(copy);
            }
            var cameras = new List<MapCameraKey>(result.Map.cameras);
            foreach (var key in second.cameras)
            { var copy = CopyRecord(key); copy.roomId = Remap(result, copy.roomId); cameras.Add(copy); }
            var shakes = new List<MapShake>(result.Map.shakes);
            foreach (var key in second.shakes)
            { var copy = CopyRecord(key); copy.roomId = Remap(result, copy.roomId); shakes.Add(copy); }
            result.Map.rooms = rooms.ToArray(); result.Map.enemies = enemies.ToArray(); result.Map.groups = groups.ToArray();
            result.Map.cameras = cameras.ToArray(); result.Map.shakes = shakes.ToArray();
            result.Map.settings.notes = Array.Empty<BeatNote>(); // MapChartCompiler owns the derived beat-note list.
            result.Compiled = MapChartCompiler.Compile(result.Map, duration, readTime, musicLength, lineWidth);
            var expectedMoves = new List<MoveNote>(a.Moves); expectedMoves.AddRange(b.Moves);
            var expectedEnemies = new List<EnemyNote>(a.Enemies);
            foreach (var enemy in b.Enemies) { var copy = enemy; copy.roomId = Remap(result, copy.roomId); expectedEnemies.Add(copy); }
            AssertNotesEqual(expectedMoves.ToArray(), expectedEnemies.ToArray(), result.Compiled.Moves, result.Compiled.Enemies);
            return result;
        }

        private static string Remap(Result result, string id)
            => id != null && result.SecondRoomIds.TryGetValue(id, out var mapped) ? mapped : id;

        private static void CheckCameraBoundary(MapChart map, double boundary, bool before)
        {
            foreach (var key in map.cameras)
                if (before ? map.settings.Seconds(key.beat + key.duration) > boundary : map.settings.Seconds(key.beat) < boundary)
                    Fail("카메라 키가 원래 구간 밖에 있습니다. 잘라내지 않고 이관을 중단합니다.");
            foreach (var key in map.shakes)
                if (before ? map.settings.Seconds(key.beat + key.duration) > boundary : map.settings.Seconds(key.beat) < boundary)
                    Fail("카메라 흔들림이 원래 구간 밖에 있습니다.");
        }

        public static MapChart Copy(MapChart map)
        {
            var s = map.settings;
            return new MapChart {
                settings = new BeatChart { bpm = s.bpm, startingRoomId = s.startingRoomId, offsetSeconds = s.offsetSeconds,
                    musicDelaySeconds = s.musicDelaySeconds, loopMusic = s.loopMusic, beatsPerBar = s.beatsPerBar,
                    subdivision = s.subdivision, roomLeadBeats = s.roomLeadBeats, enemyLeadBeats = s.enemyLeadBeats,
                    notes = (BeatNote[])(s.notes ?? Array.Empty<BeatNote>()).Clone() },
                roomSize = map.roomSize, originX = map.originX, originY = map.originY, cameraTrack = map.cameraTrack,
                cameraX = map.cameraX, cameraY = map.cameraY, cameraSize = map.cameraSize,
                rooms = Array.ConvertAll(map.rooms, CopyRecord), enemies = Array.ConvertAll(map.enemies, CopyRecord),
                groups = Array.ConvertAll(map.groups ?? Array.Empty<MapGroup>(), CopyRecord),
                cameras = Array.ConvertAll(map.cameras, CopyRecord), shakes = Array.ConvertAll(map.shakes, CopyRecord)
            };
        }

        // Records contain only values and strings. Reject new reference fields rather than silently sharing mutable data.
        private static T CopyRecord<T>(T source) where T : class, new()
        {
            if (source == null) throw new ArgumentException("빈 맵 항목입니다.");
            var copy = new T();
            foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!field.FieldType.IsValueType && field.FieldType != typeof(string))
                    Fail("새 맵 필드의 복사 정책이 필요합니다: " + field.Name);
                field.SetValue(copy, field.GetValue(source));
            }
            return copy;
        }

        public static void AssertNotesEqual(MoveNote[] expectedMoves, EnemyNote[] expectedEnemies, MoveNote[] moves, EnemyNote[] enemies)
        {
            Compare(Array.ConvertAll(expectedMoves, EffectiveMovement), Array.ConvertAll(moves, EffectiveMovement), n => n.destinationId);
            Compare(expectedEnemies, enemies, n => n.id);
        }

        private static MoveNote EffectiveMovement(MoveNote note)
        {
            // RoomRun uses HitTime and ignores door-only fields on a passage with no door.
            // The editable map still retains these inactive authoring values unchanged.
            note.time = note.HitTime;
            if (!note.hasDoor) { note.doorTime = 0; note.moveDelay = 0; note.doorFrameStartTime = 0; }
            return note;
        }

        private static void Compare<T>(T[] expected, T[] actual, Func<T, string> id) where T : struct
        {
            if (expected.Length != actual.Length) Fail("이관 전후 노트 개수가 다릅니다: " + typeof(T).Name);
            var lookup = new Dictionary<string, T>(); foreach (var note in actual) lookup.Add(id(note), note);
            foreach (var note in expected)
            {
                if (!lookup.TryGetValue(id(note), out var other)) Fail("노트가 사라졌습니다: " + id(note));
                foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    var a = field.GetValue(note); var b = field.GetValue(other);
                    bool equal = a is double da && b is double db ? Near(da, db)
                        : a is float fa && b is float fb ? Near(fa, fb) : Equals(a, b);
                    if (!equal) Fail("이관으로 노트가 바뀝니다: " + id(note) + "/" + field.Name);
                }
            }
        }
        private static bool Finite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
        private static bool Near(double a, double b) => Finite(a) && Finite(b) && Math.Abs(a - b) <= 1e-7;
        private static void Fail(string message) => throw new ArgumentException(message);
    }
}
