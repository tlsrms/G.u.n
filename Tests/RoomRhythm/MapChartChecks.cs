using System;
using Gun.RoomRhythm;

internal static class MapChartChecks
{
    private static int count;
    private static void Check(bool value, string message) { count++; if (!value) throw new Exception(message); }
    private static void Near(double a, double b, string message) => Check(Math.Abs(a - b) < .00001, message);
    private static void Reject(Action<MapChart> change)
    {
        var map = Example(); change(map);
        try { Compile(map); } catch (ArgumentException) { count++; return; }
        throw new Exception("Invalid map accepted");
    }
    private static CompiledBeatChart Compile(MapChart map) => MapChartCompiler.Compile(map, .18, .25, 30, .2);
    private static MapChart Example() => new MapChart {
        settings = new BeatChart { bpm = 120, startingRoomId = "start" },
        groups = new[] { new MapGroup { id = "a", name = "둘이 함께", appearBeat = 0 } },
        rooms = new[] { new MapRoom { id = "start", groupId = "a", x = 0, y = 0, hitBeat = 0 },
            new MapRoom { id = "north", groupId = "a", x = 0, y = 1, hitBeat = 8, frameBeat = 4, door = true, doorBeat = 6, doorFrameBeat = 3 },
            new MapRoom { id = "east", groupId = "a", x = 1, y = 1, hitBeat = 16, frameBeat = 12 } },
        enemies = new[] { new MapEnemy { id = "e", roomId = "north", direction = EnemyDirection.Right, hitBeat = 12, appearBeat = 9, frameBeat = 10 } }
    };
    private static MapChart WithLateFrame() { var map = Example(); map.rooms[1].frameBeat = 7.9; return map; }
    public static void Run()
    {
        var map = Example(); var compiled = Compile(map);
        Near(compiled.Moves[0].appearTime, 0, "Group time was replaced by lead time");
        Near(compiled.Moves[1].appearTime, 0, "Rooms in same group must appear together");
        Near(compiled.Moves[0].frameStartTime, 2, "Room frame start conversion");
        Near(compiled.Moves[0].doorFrameStartTime, 1.5, "Door frame start conversion");
        Near(compiled.Enemies[0].appearanceTime, 4.5, "Enemy appearance conversion");
        Near(compiled.Enemies[0].frameStartTime, 5, "Enemy frame must be independent of appearance");
        var run = new RoomRun(compiled.Moves, compiled.Timing, .18, compiled.Enemies, "start", .25, compiled.EnemyLeadSeconds);
        run.Begin(); Check(run.ShootDoor(0, 3), "Door playable"); run.Press(MoveDirection.Up, 4); run.Advance(4.5);
        Check(run.EnemyAvailable(0), "Custom appearance reaches combat availability");
        Check(run.ShootEnemy(0, 6), "Enemy playable"); run.Press(MoveDirection.Right, 8); run.Advance(8.2);
        Check(run.Phase == RunPhase.Cleared, "Visual map must compile into a clearable sequence");
        map.settings.bpm = 240; compiled = Compile(map);
        Near(compiled.Moves[0].frameStartTime, 1, "BPM scales room start");
        Near(compiled.Moves[0].HitTime, 2, "BPM scales target");
        Near(compiled.Enemies[0].appearanceTime, 2.25, "BPM scales explicit appearance");
        Near(compiled.Timing.early, .075, "BPM scales overlap window");
        map.settings.offsetSeconds = 1; compiled = Compile(map);
        Near(compiled.Moves[1].appearTime, 1, "Offset moves group appearance");
        Near(compiled.Enemies[0].frameStartTime, 3.5, "Offset moves frame appearance");
        Reject(m => m.rooms[2].x = 3);
        Reject(m => { m.rooms[2].x = 0; m.rooms[2].y = 1; });
        Reject(m => m.rooms[1].frameBeat = 9);
        Compile(WithLateFrame());
        Reject(m => m.groups[0].appearBeat = 7);
        Reject(m => m.enemies[0].frameBeat = 13);
        Reject(m => m.enemies[0].appearBeat = 11.7);
        map = Example(); map.rooms[1].doorFrameBeat = 0;
        compiled = Compile(map);
        Near(compiled.Moves[0].doorFrameStartTime, 0, "Early door frame is supported instead of blocking save");
        double oldAppearance = map.AppearanceBeat(map.rooms[1]);
        map.MigrateAppearance();
        Check(map.groups.Length == 0 && map.rooms[1].individualAppearance, "Legacy bundles migrate into room properties");
        Near(map.AppearanceBeat(map.rooms[1]), oldAppearance, "Migration preserves appearance timing");
        map.rooms[1].appearBeat = 1;
        Near(map.AppearanceBeat(map.rooms[2]), 0, "Editing one room appearance no longer changes bundled neighbours");
        Compile(map);        Reject(m => m.settings.bpm = double.NaN);
        Reject(m => m.rooms[1].hitBeat = double.PositiveInfinity);
        Reject(m => m.cameraSize = 0);
        Reject(m => m.cameras = new[] { new MapCameraKey { beat = 1, duration = 2 }, new MapCameraKey { beat = 2 } });
        Reject(m => m.shakes = new[] { new MapShake { duration = 0 } });
        Reject(m => m.shakes = new[] { new MapShake { frequency = float.NaN } });
        Reject(m => m.cameras = new[] { new MapCameraKey { beat = 59, duration = 3 } });
        map = Example(); map.cameraTrack = true; map.cameraX = 0; map.cameraY = 0; map.cameraSize = 7;
        map.cameras = new[] { new MapCameraKey { beat = 4, duration = 2, x = 10, y = 2, size = 5, ease = CameraEase.Linear },
            new MapCameraKey { beat = 8, duration = 0, x = 20, y = 4, size = 3 } };
        Compile(map);
        Near(map.CameraAt(3, 99, 99).x, 0, "Initial camera pose");
        Near(map.CameraAt(5, 99, 99).x, 5, "Camera interpolation");
        Near(map.CameraAt(5, 99, 99).size, 6, "Camera zoom interpolation");
        Near(map.CameraAt(7, 99, 99).x, 10, "Camera holds after movement");
        Near(map.CameraAt(8, 99, 99).x, 20, "Instant key at boundary");
        Near(map.CameraAt(3, 99, 99).x, 0, "Seeking backwards is deterministic");
        map.cameras[0].ease = CameraEase.EaseIn; Near(map.CameraAt(5, 0, 0).x, 2.5, "Ease in");
        map.cameras[0].ease = CameraEase.EaseOut; Near(map.CameraAt(5, 0, 0).x, 7.5, "Ease out");
        map.cameraTrack = false; Near(map.CameraAt(5, 42, 12).x, 42, "Follow mode uses player");
        map.shakes = new[] { new MapShake { beat = 4, duration = 2, strength = 1, frequency = 1 } };
        Near(map.CameraAt(4, 0, 0).x, 0, "Shake begins without discontinuity");
        Check(Math.Abs(map.CameraAt(4.5, 0, 0).x) > .1, "Shake evaluates inside its interval");
        Near(map.CameraAt(6, 0, 0).x, 0, "Shake ends at boundary");
        Near(map.CameraAt(4.5, 0, 0).x, map.CameraAt(4.5, 0, 0).x, "Shake preview and gameplay deterministic");
        map = Example();
        map.rooms[2].x = 0; map.rooms[2].y = 0; map.rooms[2].groupId = "return";
        double released = map.DepartureBeat(map.rooms[0], .18);
        map.groups = new[] { map.groups[0], new MapGroup { id = "return", name = "Bundle", appearBeat = released } };
        compiled = Compile(map);
        Check(compiled.Moves[1].direction == MoveDirection.Down, "W then S may revisit the initial tile as a new occurrence");
        Near(compiled.Moves[1].appearanceTime, released * .5, "Reuse begins after the latest successful departure");
        Check(map.RoomLabel(map.rooms[0]) == "1th/0beat" && map.RoomLabel(map.rooms[2]) == "3th/16beat", "Room labels contain sequence and beat only");
        run = new RoomRun(compiled.Moves, compiled.Timing, .18, compiled.Enemies, "start", .25, compiled.EnemyLeadSeconds);
        run.Begin(); run.ShootDoor(0, 3); run.Press(MoveDirection.Up, 4); run.Advance(4.4); run.ShootEnemy(0, 6);
        run.Press(MoveDirection.Down, 8); run.Advance(8.2);
        Check(run.Phase == RunPhase.Cleared, "Revisit chart plays through");
        map.groups[1].appearBeat = released - .00001;
        try { Compile(map); throw new Exception("Overlapping visibility of reused tile was accepted"); }
        catch (ArgumentException) { count++; }
        map.groups[1].appearBeat = released;
        map.rooms[2].hitBeat = 20;
        Check(map.RoomLabel(map.rooms[2]) == "3th/20beat", "Room label follows edited beat without changing runtime ID");
        map.rooms = new[] { map.rooms[2], map.rooms[0], map.rooms[1] };
        Check(map.OrderedRooms()[0].id == "start" && map.OrderedRooms()[2].id == "east", "Occurrence display order does not depend on serialized array order");
        Compile(map);
        Console.WriteLine($"PASS: {count} visual map timing/camera/validation checks.");
    }
}
