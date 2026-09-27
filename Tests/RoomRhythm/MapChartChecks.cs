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
    private static void CheckTimelineEditing()
    {
        var map = Example(); map.MigrateAppearance();
        map.cameras = new[] { new MapCameraKey { roomId = "north", beat = 4, duration = 2, x = 3 } };
        map.shakes = new[] { new MapShake { roomId = "north", beat = 5, duration = 3, strength = .7f } };
        var selected = new[] { new MapTimelineItem(0, 1), new MapTimelineItem(1, 1), new MapTimelineItem(2, 0),
            new MapTimelineItem(3, 0), new MapTimelineItem(4, 0) };
        var shift = new MapTimelineShift(map, selected);
        Near(shift.Earliest, 0, "Batch origin is earliest selected start");
        shift.Apply(3, 0, 60);
        Near(map.rooms[1].appearBeat, 3, "Shared room/door start is moved once");
        Near(map.rooms[1].doorFrameBeat, 3, "Door and room starts stay synchronized");
        Near(map.rooms[1].hitBeat, 11, "Room target moves by group delta");
        Near(map.rooms[1].doorBeat, 9, "Door target moves by group delta");
        Near(map.enemies[0].appearBeat, 12, "Enemy appearance moves with its selected interval");
        Near(map.enemies[0].hitBeat - map.enemies[0].frameBeat, 3, "Enemy interval uses shared appearance and frame start");
        Near(map.cameras[0].beat, 7, "Camera moves with group");
        Near(map.cameras[0].duration, 2, "Camera duration is preserved");
        Near(map.shakes[0].beat, 8, "Shake moves with group");
        shift.Apply(1, 0, 60);
        Near(map.rooms[1].appearBeat, 1, "Repeated drag updates use original snapshot, not accumulated deltas");
        shift.Apply(-10, 0, 60);
        Near(map.rooms[1].appearBeat, 0, "Group movement clamps at map start");
        shift.Apply(100, 0, 20);
        Near(map.enemies[0].hitBeat, 20, "Group movement clamps as a unit at map end");
        var copies = MapTimelineEditing.Duplicate(map, selected);
        Check(map.rooms.Length == 4 && copies.Count == 5, "Room and its selected door duplicate as one room");
        string copyId = map.rooms[3].id;
        Check(copyId != "north" && map.enemies[1].id != map.enemies[0].id, "Duplicated objects get unique IDs");
        Check(map.enemies[1].roomId == copyId && map.cameras[1].roomId == copyId && map.shakes[1].roomId == copyId,
            "Selected dependent events reference the duplicated room");
        double original = map.rooms[1].hitBeat;
        new MapTimelineShift(map, copies).Apply(4, 0, 100);
        Near(map.rooms[1].hitBeat, original, "Moving duplicates leaves originals unchanged");
        Near(map.rooms[3].hitBeat, original + 4, "Copies can be shifted together");
        MapTimelineEditing.Duplicate(map, new[] { new MapTimelineItem(2, 0) });
        Check(map.enemies[2].roomId == "north", "An enemy copied alone keeps its room");
        var doorCopies = MapTimelineEditing.Duplicate(map, new[] { new MapTimelineItem(0, 1) });
        Check(doorCopies.Count == 2, "A duplicated door selects its new room too so they can move together");
    }
    public static void Run()
    {
        CheckTimelineEditing();
        var namedError = Example();
        namedError.enemies[0].id = "enemy_internal_test";
        namedError.enemies[0].appearBeat = 13;
        try { Compile(namedError); throw new Exception("Invalid enemy timing accepted"); }
        catch (ArgumentException exception)
        {
            Check(exception.Message.Contains("2th/12beat") && exception.Message.Contains("Right")
                && !exception.Message.Contains("enemy_internal_test"), "Enemy errors use the same beat label as the timeline");
        }
        var ownership = Example();
        Check(ownership.EnemyRoomAt(7.999) == "start", "Enemy before first room judgment belongs to start");
        Check(ownership.EnemyRoomAt(8) == "north", "Exact room boundary belongs to the entered room");
        Check(ownership.EnemyRoomAt(15.999) == "north" && ownership.EnemyRoomAt(16) == "east",
            "Enemy ownership uses half-open room judgment intervals");
        ownership.enemies[0].roomId = "start";
        Check(Compile(ownership).Enemies[0].roomId == "north", "Compilation derives room from shot judgment instead of stale room ID");
        ownership.SynchronizeEnemyRooms();
        Check(ownership.enemies[0].roomId == "north" && !ownership.NeedsEnemyRoomSynchronization, "Stored ownership follows shot judgment");
        ownership.enemies[0].appearBeat = 2;
        ownership.SynchronizeEnemyRooms();
        Check(ownership.enemies[0].roomId == "north", "Early appearance does not assign an enemy to the previous room");
        Near(ownership.enemies[0].frameBeat, 2, "Appearance and enemy frame start synchronize");
        var earlyEnemy = Compile(ownership).Enemies[0];
        Check(earlyEnemy.roomId == "north" && earlyEnemy.appearanceTime == 1 && earlyEnemy.frameStartTime == 1,
            "Compilation keeps early start while assigning the future shot room");
        var earlyChart = Compile(ownership);
        var earlyRun = new RoomRun(earlyChart.Moves, earlyChart.Timing, .18, earlyChart.Enemies, "start", .25, earlyChart.EnemyLeadSeconds);
        Check(!earlyRun.EnemyVisible(0, 1), "Ready state hides upcoming enemies");
        earlyRun.Begin(); earlyRun.Advance(1);
        Check(!earlyRun.EnemyVisible(0, .99) && earlyRun.EnemyVisible(0, 1), "Future enemy becomes visible at its authored appearance");
        Check(!earlyRun.EnemyRoomEntered(0) && !earlyRun.EnemyAvailable(0), "Upcoming enemy is dimmed and cannot be targeted before entry");
        earlyRun.ShootDoor(0, 3); earlyRun.Press(MoveDirection.Up, 4);
        Check(earlyRun.EnemyVisible(0, 4.1) && !earlyRun.EnemyRoomEntered(0), "Upcoming enemy remains visible during movement");
        earlyRun.Advance(4.181);
        Check(earlyRun.EnemyRoomEntered(0) && earlyRun.EnemyAvailable(0), "Arrival restores normal brightness and target availability");
        Near(earlyRun.EnemyVisualAppearsAt(0), 1, "Arrival does not restart appearance or timing-ring animation");
        earlyRun.ShootEnemy(0, 6);
        Check(!earlyRun.EnemyVisible(0, 6), "Defeated enemy stays hidden");
        earlyRun.Reset();
        Check(!earlyRun.EnemyVisible(0, 1), "Retry resets upcoming enemy visibility");
        ownership.enemies[0].hitBeat = 17;
        ownership.SynchronizeEnemyRooms();
        Check(ownership.enemies[0].roomId == "east", "Moving enemy judgment updates its room");
        ownership.rooms[2].hitBeat = 18;
        ownership.SynchronizeEnemyRooms();
        Check(ownership.enemies[0].roomId == "north", "Moving room judgment updates existing enemies");
        Array.Reverse(ownership.rooms);
        Check(ownership.EnemyRoomAt(17) == "north", "Room assignment is independent of storage order");
        var empty = MapChart.CreateEmpty(new BeatChart { bpm = 150, startingRoomId = "new-start" });
        Check(empty.rooms.Length == 1 && empty.rooms[0].id == "new-start", "New chart creates its own starting room");
        Check(empty.rooms[0].x == 0 && empty.rooms[0].y == 0 && !empty.rooms[0].door, "Starting room is at the origin without a door");
        Check(empty.enemies.Length == 0 && !empty.NeedsAppearanceMigration, "New chart has no enemies or legacy appearance data");
        Near(empty.settings.bpm, 150, "New chart preserves song settings");
        var fallback = MapChart.CreateEmpty(new BeatChart { startingRoomId = " " });
        Check(fallback.settings.startingRoomId == "start" && fallback.rooms[0].id == "start", "Missing start ID has a consistent default");
        empty.rooms = new[] { empty.rooms[0], new MapRoom { id = "next", x = 1, hitBeat = 8,
            individualAppearance = true, appearBeat = 0, frameBeat = 4 } };
        var freshCompiled = Compile(empty);
        Check(freshCompiled.Moves.Length == 1 && freshCompiled.Moves[0].destinationId == "next", "New chart compiles after adding a destination room");
        var map = Example(); var compiled = Compile(map);
        var delayed = Example(); delayed.settings.musicDelaySeconds = 4;
        var delayedCompiled = Compile(delayed);
        Near(delayedCompiled.Moves[0].HitTime, compiled.Moves[0].HitTime + 4, "Music delay shifts compiled movement");
        Near(delayedCompiled.Moves[0].doorTime, compiled.Moves[0].doorTime + 4, "Music delay shifts compiled door");
        Near(delayedCompiled.Enemies[0].time, compiled.Enemies[0].time + 4, "Music delay shifts compiled enemy");
        delayed.rooms[1].individualAppearance = true;
        delayed.rooms[1].appearBeat = delayed.settings.Beat(0);
        Near(Compile(delayed).Moves[0].appearanceTime, 0, "Rooms can appear during the silent introduction");
        MapChartCompiler.Compile(delayed, .18, .25, 9, .2);
        Check(true, "Audio-end validation includes music delay");
        var looped = Example(); looped.settings.loopMusic = true;
        var loopedCompiled = MapChartCompiler.Compile(looped, .18, .25, 1, .2);
        Near(loopedCompiled.Moves[1].HitTime, compiled.Moves[1].HitTime, "Looped music accepts notes beyond the first clip without wrapping judgment times");
        looped.settings.musicDelaySeconds = 2;
        Near(MapChartCompiler.Compile(looped, .18, .25, 1, .2).Enemies[0].time,
            compiled.Enemies[0].time + 2, "Looped music preserves initial delay for enemy timing");
        bool shortClipRejected = false;
        try { MapChartCompiler.Compile(Example(), .18, .25, 1, .2); }
        catch (ArgumentException) { shortClipRejected = true; }
        Check(shortClipRejected, "Non-looped music still rejects notes beyond the audio end");
        Check(map.RoomLabel(map.rooms[1], map.rooms[1].doorBeat) == "2th/6beat", "Door label uses its own exact shot beat");
        Check(map.RoomLabel(map.rooms[1], map.enemies[0].hitBeat) == "2th/12beat", "Enemy label uses its own exact shot beat");
        Near(compiled.Moves[0].appearTime, 0, "Group time was replaced by lead time");
        Near(compiled.Moves[1].appearTime, 0, "Rooms in same group must appear together");
        Near(compiled.Moves[0].frameStartTime, 0, "Room frame begins with room appearance");
        Near(compiled.Moves[0].doorFrameStartTime, 0, "Door frame begins with room appearance");
        var sharedStarts = Example();
        sharedStarts.MigrateAppearance();
        Check(!sharedStarts.NeedsRoomStartSynchronization, "Migration synchronizes all room starts");
        sharedStarts.SetRoomStart(sharedStarts.rooms[1], 2);
        Check(sharedStarts.rooms[1].appearBeat == 2 && sharedStarts.rooms[1].frameBeat == 2
            && sharedStarts.rooms[1].doorFrameBeat == 2, "Changing either timeline start updates all three starts");
        var sharedCompiled = Compile(sharedStarts);
        Near(sharedCompiled.Moves[0].appearanceTime, 1, "Shared start conversion");
        Near(sharedCompiled.Moves[0].frameStartTime, 1, "Movement line uses shared start");
        Near(sharedCompiled.Moves[0].doorFrameStartTime, 1, "Door line uses shared start");
        Near(compiled.Enemies[0].appearanceTime, 4.5, "Enemy appearance conversion");
        Near(compiled.Enemies[0].frameStartTime, 4.5, "Enemy frame begins with appearance");
        var run = new RoomRun(compiled.Moves, compiled.Timing, .18, compiled.Enemies, "start", .25, compiled.EnemyLeadSeconds);
        run.Begin(); Check(run.ShootDoor(0, 3), "Door playable"); run.Press(MoveDirection.Up, 4); run.Advance(4.5);
        Check(run.EnemyAvailable(0), "Custom appearance reaches combat availability");
        Check(run.ShootEnemy(0, 6), "Enemy playable"); run.Press(MoveDirection.Right, 8); run.Advance(8.2);
        Check(run.Phase == RunPhase.Cleared, "Visual map must compile into a clearable sequence");
        map.settings.bpm = 240; compiled = Compile(map);
        Near(compiled.Moves[0].frameStartTime, 0, "BPM preserves synchronized room start");
        Near(compiled.Moves[0].HitTime, 2, "BPM scales target");
        Near(compiled.Enemies[0].appearanceTime, 2.25, "BPM scales explicit appearance");
        Near(compiled.Timing.early, .035, "BPM preserves fixed overlap window");
        map.settings.offsetSeconds = 1; compiled = Compile(map);
        Near(compiled.Moves[1].appearTime, 1, "Offset moves group appearance");
        Near(compiled.Enemies[0].frameStartTime, 3.25, "Offset moves unified enemy start");
        Reject(m => m.rooms[2].x = 3);
        Reject(m => { m.rooms[2].x = 0; m.rooms[2].y = 1; });
        Reject(m => m.SetRoomStart(m.rooms[1], 9));
        Compile(WithLateFrame());
        Reject(m => m.groups[0].appearBeat = 7);
        Reject(m => m.enemies[0].appearBeat = 13);
        var shortReveal = Example(); shortReveal.enemies[0].appearBeat = 11.7;
        Compile(shortReveal);
        Check(true, "Late enemy appearance remains valid if it precedes judgment");
        MapChartCompiler.Compile(Example(), .18, .25, 8, .2);
        Check(true, "Final movement animation and late window may finish after audio ends");
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
        Near(released, 8.36, "Preview and validation release the tile at on-beat arrival without adding input tolerance");
        map.groups = new[] { map.groups[0], new MapGroup { id = "return", name = "Bundle", appearBeat = released } };
        compiled = Compile(map);
        Check(compiled.Moves[1].direction == MoveDirection.Down, "W then S may revisit the initial tile as a new occurrence");
        Near(compiled.Moves[1].appearanceTime, released * .5, "Reuse may start at the preview departure boundary");
        Check(map.RoomLabel(map.rooms[0]) == "1th/0beat" && map.RoomLabel(map.rooms[2]) == "3th/16beat", "Room labels contain sequence and beat only");
        run = new RoomRun(compiled.Moves, compiled.Timing, .18, compiled.Enemies, "start", .25, compiled.EnemyLeadSeconds);
        run.Begin(); run.ShootDoor(0, 3); run.Press(MoveDirection.Up, 4); run.Advance(4.4); run.ShootEnemy(0, 6);
        run.Press(MoveDirection.Down, 8); run.Advance(8.2);
        Check(run.Phase == RunPhase.Cleared, "Revisit chart plays through");
        run.Begin(); run.ShootDoor(0, 3); run.Press(MoveDirection.Up, 4.035);
        run.Advance(4.18);
        Check(run.RoomVisible(0, 4.18) && !run.RoomVisible(2, 4.18, 0),
            "Late accepted movement keeps the previous tile visible and defers its replacement");
        run.Advance(run.MoveEndsAt);
        Check(!run.RoomVisible(0, run.MoveEndsAt) && run.RoomVisible(2, run.MoveEndsAt, 0),
            "At actual departure the replacement appears without overlapping its predecessor");
        run.Reset();
        Check(run.RoomVisible(0, 0) && !run.RoomVisible(2, 100, 0), "Restart hides reused future tiles");
        map.groups[1].appearBeat = released - 1e-12;
        Compile(map);
        Check(true, "Floating point roundoff at the shared boundary does not reject the chart");
        map.groups[1].appearBeat = released - .00001;
        Compile(map);
        Near(map.VisibleAppearanceBeat(map.rooms[2], .18), released, "Reused visuals wait for departure instead of rejecting the authored start");
        map.groups[1].appearBeat = released;
        map.rooms[2].hitBeat = 20;
        Check(map.RoomLabel(map.rooms[2]) == "3th/20beat", "Room label follows edited beat without changing runtime ID");
        map.rooms = new[] { map.rooms[2], map.rooms[0], map.rooms[1] };
        Check(map.OrderedRooms()[0].id == "start" && map.OrderedRooms()[2].id == "east", "Occurrence display order does not depend on serialized array order");
        Compile(map);
        CheckAlternatingTiles();
        Console.WriteLine($"PASS: {count} visual map timing/camera/validation checks.");
    }

    private static void CheckAlternatingTiles()
    {
        var map = MapChart.CreateEmpty(new BeatChart { bpm = 121, offsetSeconds = 240.0 / 121, roomLeadBeats = 3 });
        map.rooms = new[] { map.rooms[0],
            new MapRoom { id = "right", x = 1, hitBeat = 1, door = true, doorBeat = 0 },
            new MapRoom { id = "return", x = 0, hitBeat = 2 },
            new MapRoom { id = "right-again", x = 1, hitBeat = 3 } };
        map.SetRoomStart(map.rooms[1], -2);
        map.SetRoomStart(map.rooms[2], -1);
        map.SetRoomStart(map.rooms[3], 0);
        map.ValidateRoomReuse(.18);
        var compiled = Compile(map);
        Check(compiled.Moves.Length == 3, "Screenshot sequence accepts a fourth room at beat three despite existing early visual starts");
        Near(map.AppearanceBeat(map.rooms[2]), -1, "Validation preserves the authored negative start beat");
        Near(map.VisibleAppearanceBeat(map.rooms[2], .18), 1 + .18 * 121 / 60, "Return room waits for initial room departure in preview");
        Near(map.VisibleAppearanceBeat(map.rooms[3], .18), 2 + .18 * 121 / 60, "Fourth room waits for the previous right room departure");
        var run = new RoomRun(compiled.Moves, compiled.Timing, .18);
        Check(!run.RoomFrameVisible(2, map.settings.Seconds(0)), "Ready state hides reused room frames");
        run.Begin();
        Check(!run.RoomVisible(2, map.settings.Seconds(-1), 0), "Initial occupant hides the returning room");
        Check(!run.RoomFrameVisible(2, map.settings.Seconds(-1) - .001)
            && run.RoomFrameVisible(2, map.settings.Seconds(-1)), "Reused room frame starts at its authored time while the previous room remains");
        run.ShootDoor(0, map.settings.Seconds(0));
        run.Press(MoveDirection.Right, map.settings.Seconds(1));
        run.Advance(run.MoveEndsAt);
        Check(run.RoomVisible(2, run.MoveEndsAt, 0) && !run.RoomVisible(3, run.MoveEndsAt, 1), "Only the vacated tile is revealed");
        run.Press(MoveDirection.Left, map.settings.Seconds(2));
        Check(!run.RoomFrameVisible(2, map.settings.Seconds(2)), "Successful movement consumes the returning room frame");
        run.Advance(run.MoveEndsAt);
        Check(run.RoomVisible(3, run.MoveEndsAt, 1), "Fourth room reveals after the right tile is vacated");
        run.Press(MoveDirection.Right, map.settings.Seconds(3)); run.Advance(run.MoveEndsAt);
        Check(run.Phase == RunPhase.Cleared, "Alternating-tile screenshot sequence clears");
        run.Reset();
        Check(!run.RoomFrameVisible(2, map.settings.Seconds(2)), "Reset hides previously displayed frames");
        map.rooms[3].door = true; map.rooms[3].doorBeat = 2.8;
        compiled = Compile(map);
        run = new RoomRun(compiled.Moves, compiled.Timing, .18);
        run.Begin();
        Check(!run.RoomVisible(3, map.settings.Seconds(0), 1)
            && run.RoomFrameVisible(3, map.settings.Seconds(0), true), "Reused door frame starts before its room surfaces are released");
        map.rooms[3].door = true; map.rooms[3].doorBeat = 1;
        bool rejected = false;
        try { map.ValidateRoomReuse(.18); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "A door whose entire input window expires while its tile is occupied remains invalid");
    }
}
