using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Gun.RoomRhythm;
using Gun.RoomRhythm.Editor;

internal static class MapChartMigrationChecks
{
    private static int checks;
    private static void Check(bool value, string label)
    { checks++; if (!value) throw new Exception("FAIL migration: " + label); }
    private static void Reject(Action action, string label)
    { try { action(); } catch (ArgumentException) { checks++; return; } throw new Exception("FAIL migration accepts: " + label); }

    // Restricted reader for the two authored chart fixtures; no scene or prefab traversal.
    private static T Fields<T>(string text, int indentation) where T : new()
    {
        object result = new T();
        foreach (var field in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            if (field.FieldType.IsArray || !field.FieldType.IsValueType && field.FieldType != typeof(string)) continue;
            var match = Regex.Match(text, @"(?m)^ {" + indentation + "}" + field.Name + @": ([^\r\n]*)");
            if (!match.Success) continue;
            string value = match.Groups[1].Value.Trim().Trim('"');
            object converted = field.FieldType == typeof(string) ? value : field.FieldType == typeof(bool) ? (object)(value == "1")
                : field.FieldType.IsEnum ? Enum.ToObject(field.FieldType, int.Parse(value, CultureInfo.InvariantCulture))
                : Convert.ChangeType(value, field.FieldType, CultureInfo.InvariantCulture);
            field.SetValue(result, converted);
        }
        return (T)result;
    }
    private static T[] Rows<T>(string map, string key, int indentation = 4) where T : new()
    {
        string prefix = new string(' ', indentation);
        string body = Regex.Match(map, @"(?ms)^" + prefix + key + @":\r?\n(.*?)(?=^" + prefix + @"\w|\z)").Groups[1].Value;
        var rows = new List<T>();
        foreach (Match row in Regex.Matches(body, @"(?ms)^" + prefix + @"- (.*?)(?=^" + prefix + @"- |\z)"))
            rows.Add(Fields<T>(prefix + "  " + row.Groups[1].Value, indentation + 2));
        return rows.ToArray();
    }
    private static MapChart Read(string path)
    {
        string text = File.ReadAllText(path);
        string body = Regex.Match(text, @"(?ms)^  appliedMap:\r?\n(.*?)(?=^  \w|\z)").Groups[1].Value;
        if (body.Length == 0) throw new Exception("Missing applied map fixture: " + path);
        var map = Fields<MapChart>(body, 4); map.settings = Fields<BeatChart>(body, 6);
        map.rooms = Rows<MapRoom>(body, "rooms"); map.enemies = Rows<MapEnemy>(body, "enemies");
        map.groups = Rows<MapGroup>(body, "groups"); map.cameras = Rows<MapCameraKey>(body, "cameras"); map.shakes = Rows<MapShake>(body, "shakes");
        return map;
    }
    private static MapChartMigration.Result Merge(MapChart a, MapChart b)
        => MapChartMigration.Merge(a, b, .28, .25, 200, .1, 144 * 60.0 / 130);
    private static CompiledBeatChart Compile(MapChart map) => MapChartCompiler.Compile(map, .28, .25, 200, .1);

    public static void Run(string root)
    {
        var first = Read(Path.Combine(root, "Assets/RoomChart/Stage1_Guards.asset"));
        var second = Read(Path.Combine(root, "Assets/RoomChart/Stage1_Mafia.asset"));
        var a = Compile(first); var b = Compile(second); var result = Merge(first, second);
        CompareAuthoredNotes(Path.Combine(root, "Assets/RoomChart/Stage1_Guards.asset"), a);
        CompareAuthoredNotes(Path.Combine(root, "Assets/RoomChart/Stage1_Mafia.asset"), b);
        Check(result.Map.rooms.Length == first.rooms.Length + second.rooms.Length - 1, "exactly one duplicate start removed");
        Check(result.Compiled.Moves.Length == a.Moves.Length + b.Moves.Length, "no synthetic movement");
        Check(result.Compiled.Enemies.Length == a.Enemies.Length + b.Enemies.Length, "all enemies retained");
        Check(result.RemovedStartId == "mafia0" && result.JoinedRoomId == "guard15", "actual seam mapping");
        var snapshot = MapChartMigration.Copy(result.Map);
        result.Map.rooms[0].offsetX += 1; result.Map.enemies[0].hitBeat += 1;
        Check(result.Map.rooms[0].offsetX != first.rooms[0].offsetX && result.Map.enemies[0].hitBeat != first.enemies[0].hitBeat,
            "output edits do not mutate source");
        result.Map = snapshot;
        foreach (double offset in new[] { 0.0, -.03, .03 })
        {
            var oldEvents = Replay(a, first.settings.startingRoomId, offset);
            oldEvents.AddRange(Replay(b, second.settings.startingRoomId, offset));
            var newEvents = Replay(result.Compiled, result.Map.settings.startingRoomId, offset);
            Check(oldEvents.Count == newEvents.Count, "same event count with input offset " + offset);
            for (int i = 0; i < oldEvents.Count; i++)
            {
                var old = oldEvents[i]; var current = newEvents[i];
                Check(old.Kind == current.Kind && old.TargetId == current.TargetId && old.Grade == current.Grade
                    && old.Failure == current.Failure && Math.Abs(old.Time - current.Time) < 1e-7, "event meaning and time preserved");
            }
        }
        foreach (double failureDelta in new[] { -.2, .2 })
        {
            string target = b.Enemies[0].id;
            var expected = Replay(a, first.settings.startingRoomId, 0);
            expected.AddRange(Replay(b, second.settings.startingRoomId, 0, target, failureDelta));
            var actual = Replay(result.Compiled, result.Map.settings.startingRoomId, 0, target, failureDelta);
            Check(expected.Count == actual.Count, "same boss failure event count");
            for (int i = 0; i < expected.Count; i++)
                Check(expected[i].Kind == actual[i].Kind && expected[i].TargetId == actual[i].TargetId
                    && expected[i].Failure == actual[i].Failure && expected[i].Grade == actual[i].Grade
                    && Math.Abs(expected[i].Time - actual[i].Time) < 1e-7, "same boss early/late failure trace");
        }
        var run = NewRun(result.Compiled, result.Map.settings.startingRoomId);
        run.AdvanceAutomatically(140 * 60.0 / 130);
        Check(run.Phase == RunPhase.Waiting && run.CompletedMoves == a.Moves.Length, "entrance gap does not clear the chart");
        run.AdvanceAutomatically(190);
        Check(run.Phase == RunPhase.Cleared && run.JudgmentVersion == 183, "single chart debug completes all 183 judgments");
        run.Begin(); run.AdvanceAutomatically(190);
        Check(run.JudgmentVersion == 183, "restart drops prior score and playback state");
        run.Begin(); run.Advance(190);
        Check(run.Phase == RunPhase.Dead, "manual play still fails on missed input");
        CompareFirstFailure(a, result.Compiled, first.settings.startingRoomId);
        var bad = MapChartMigration.Copy(second); bad.rooms[0].offsetX += 1;
        Reject(() => Merge(first, bad), "different seam geometry");
        bad = MapChartMigration.Copy(second); bad.settings.musicDelaySeconds = .1;
        Reject(() => Merge(first, bad), "different music clocks");
        bad = MapChartMigration.Copy(second); bad.enemies[0].id = first.enemies[0].id;
        Reject(() => Merge(first, bad), "duplicate enemy identity");
        bad = MapChartMigration.Copy(second); bad.rooms[1].id = first.rooms[1].id;
        Reject(() => Merge(first, bad), "duplicate movement identity");

        // Camera/shake and group data must survive even though the current authored stage has none.
        var extraA = MapChartMigration.Copy(first); var extraB = MapChartMigration.Copy(second);
        extraA.groups = new[] { new MapGroup { id = "guards_group", name = "Guards", appearBeat = 0 } };
        extraB.groups = new[] { new MapGroup { id = "boss_group", name = "Boss", appearBeat = 144 } };
        extraB.cameras = new[] { new MapCameraKey { roomId = "mafia0", beat = 145, duration = 1, x = 72, y = 18, size = 7 } };
        extraB.shakes = new[] { new MapShake { roomId = "mafia0", beat = 146, duration = 1 } };
        var extras = Merge(extraA, extraB);
        Check(extras.Map.groups.Length == 2 && extras.Map.cameras[0].roomId == "guard15" && extras.Map.shakes[0].roomId == "guard15",
            "groups camera and shake references survive seam remapping");
        extras.Map.cameras[0].x = -1;
        Check(extraB.cameras[0].x == 72, "camera copy is independent");
        Console.WriteLine("PASS: " + checks + " chart migration checks; full map, early/late event equivalence, gap, debug, restart and rejection cases.");
    }

    private static void CompareAuthoredNotes(string path, CompiledBeatChart compiled)
    {
        string text = File.ReadAllText(path);
        var moves = Rows<MoveNote>(text, "moves", 2);
        var enemies = Rows<EnemyNote>(text, "enemies", 2);
        double lead = double.Parse(Regex.Match(text, @"(?m)^  roomLeadTime: ([^\r\n]+)").Groups[1].Value, CultureInfo.InvariantCulture);
        for (int i = 0; i < moves.Length; i++) moves[i].appearTime = moves[i].customAppearance ? moves[i].appearanceTime : Math.Max(0, moves[i].HitTime - lead);
        MapChartMigration.AssertNotesEqual(moves, enemies, compiled.Moves, compiled.Enemies);
        checks++;
    }

    private static RoomRun NewRun(CompiledBeatChart chart, string start)
    {
        var run = new RoomRun(chart.Moves, chart.Timing, .28, chart.Enemies, start);
        run.Begin(); return run;
    }
    private static List<RoomActionResult> Replay(CompiledBeatChart chart, string start, double offset, string failureTarget = null, double failureDelta = 0)
    {
        var run = NewRun(chart, start);
        var commands = new List<(double time, Action action)>();
        foreach (var note in chart.Moves)
        {
            var move = note; int index = Array.FindIndex(chart.Moves, n => n.destinationId == move.destinationId);
            if (move.hasDoor) commands.Add((move.doorTime + offset, () => run.ShootDoor(index, move.doorTime + offset)));
            commands.Add((move.HitTime + offset, () => run.Press(move.direction, move.HitTime + offset)));
        }
        for (int i = 0; i < chart.Enemies.Length; i++)
        { int index = i; double time = chart.Enemies[i].time + (chart.Enemies[i].id == failureTarget ? failureDelta : offset);
            commands.Add((time, () => run.ShootEnemy(index, time))); }
        commands.Sort((a, b) => a.time.CompareTo(b.time));
        foreach (var command in commands) { run.Advance(command.time); command.action(); }
        run.Advance(commands[commands.Count - 1].time + 1);
        Check(run.Phase == (failureTarget == null ? RunPhase.Cleared : RunPhase.Dead), "manual replay reaches expected end state");
        int expected = chart.Moves.Length + chart.Enemies.Length + Array.FindAll(chart.Moves, n => n.hasDoor).Length;
        if (failureTarget == null)
            Check(run.JudgmentVersion == expected && (offset == 0 ? run.AccurateJudgments == expected : run.AccurateJudgments == 0), "same score semantics");
        var events = new List<RoomActionResult>(); while (run.TryDequeueResult(out var item)) events.Add(item);
        return events;
    }
    private static void CompareFirstFailure(CompiledBeatChart first, CompiledBeatChart merged, string start)
    {
        foreach (double time in new[] { 0.0, 100.0 })
        {
            var before = NewRun(first, start); var after = NewRun(merged, start);
            before.Press(MoveDirection.Down, time); after.Press(MoveDirection.Down, time);
            Check(before.Phase == after.Phase && before.Failure == after.Failure && before.DeathTime == after.DeathTime,
                "wrong/late input failure invariant");
        }
    }
}
