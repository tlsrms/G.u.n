using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Gun.RoomRhythm;

internal static class MafiaStageChecks
{
    private static int checks;
    private static void Check(bool value, string label)
    { checks++; if (!value) throw new Exception("FAIL Mafia full chart: " + label); }
    // Restricted reader for the current stage asset; no scene or prefab traversal.
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
    private static MapChart Read(string path, string key)
    {
        string text = File.ReadAllText(path);
        string body = Regex.Match(text, @"(?ms)^  " + key + @":\r?\n(.*?)(?=^  \w|\z)").Groups[1].Value;
        if (body.Length == 0) throw new Exception("Missing applied map fixture: " + path);
        var map = Fields<MapChart>(body, 4); map.settings = Fields<BeatChart>(body, 6);
        map.rooms = Rows<MapRoom>(body, "rooms"); map.enemies = Rows<MapEnemy>(body, "enemies");
        map.groups = Rows<MapGroup>(body, "groups"); map.cameras = Rows<MapCameraKey>(body, "cameras"); map.shakes = Rows<MapShake>(body, "shakes");
        return map;
    }
    private static CompiledBeatChart Compile(MapChart map) => MapChartCompiler.Compile(map, .28, .25, 200, .1);

    public static void Run(string root)
    {
        string path = Path.Combine(root, "Assets/RoomChart/Stage1_Full.asset");
        var map = Read(path, "appliedMap");
        var chart = Compile(map);
        var draft = Compile(Read(path, "mapDraft"));
        CompareNotes(chart.Moves, chart.Enemies, draft.Moves, draft.Enemies);
        CompareAuthoredNotes(path, chart);
        Check(map.rooms.Length == 43 && chart.Moves.Length == 42 && chart.Enemies.Length == 132
            && Array.FindAll(chart.Moves, n => n.hasDoor).Length == 9, "authored stage has 183 actions and one connected map");
        Check(map.settings.startingRoomId == "guard0" && map.Room("guard15") != null
            && !Array.Exists(map.rooms, r => r.id == "mafia0"), "duplicate start room removed");
        Check(Array.Find(chart.Enemies, n => n.id == "mafia_shot_0_0").roomId == "guard15", "boss targets share the entrance room");
        foreach (double offset in new[] { 0.0, -.03, .03 })
        {
            var events = Replay(chart, map.settings.startingRoomId, offset);
            Check(events.Count == 225 && !events.Exists(e => e.Kind == RoomActionKind.Failed), "all judgments and arrivals survive the entrance gap");
        }
        foreach (double delta in new[] { -.2, .2 })
        {
            var events = Replay(chart, map.settings.startingRoomId, 0, "mafia_shot_0_0", delta);
            Check(events.FindAll(e => e.Kind == RoomActionKind.Failed).Count == 1, "boss early/late input still fails once");
            Check(events.FindAll(e => e.Kind == RoomActionKind.EnemyDefeated).Count == 48, "guard score survives boss failure");
        }
        var run = NewRun(chart, map.settings.startingRoomId);
        run.AdvanceAutomatically(140 * 60.0 / 130);
        Check(run.Phase == RunPhase.Waiting && run.CompletedMoves == 15 && run.JudgmentVersion == 66, "entrance gap preserves progress without clearing");
        run.AdvanceAutomatically(190);
        Check(run.Phase == RunPhase.Cleared && run.JudgmentVersion == 183 && run.AccuracyPercent == 100, "debug completes every action");
        run.Begin(); run.AdvanceAutomatically(140 * 60.0 / 130);
        Check(run.JudgmentVersion == 66 && run.Failure == FailureReason.None, "restart resets score and playback state");
        run.AdvanceAutomatically(190);
        Check(run.JudgmentVersion == 183, "restart completes without accumulating previous score");
        run.Begin(); run.Advance(190);
        Check(run.Phase == RunPhase.Dead, "manual playback still requires input");
        Console.WriteLine("PASS: " + checks + " full Mafia chart checks; saved map/notes, early/late inputs, failure, entrance gap, debug and restart.");
    }

    private static void CompareAuthoredNotes(string path, CompiledBeatChart compiled)
    {
        string text = File.ReadAllText(path);
        var moves = Rows<MoveNote>(text, "moves", 2);
        var enemies = Rows<EnemyNote>(text, "enemies", 2);
        double lead = double.Parse(Regex.Match(text, @"(?m)^  roomLeadTime: ([^\r\n]+)").Groups[1].Value, CultureInfo.InvariantCulture);
        for (int i = 0; i < moves.Length; i++) moves[i].appearTime = moves[i].customAppearance ? moves[i].appearanceTime : Math.Max(0, moves[i].HitTime - lead);
        CompareNotes(moves, enemies, compiled.Moves, compiled.Enemies);
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
    private static void CompareNotes(MoveNote[] a, EnemyNote[] b, MoveNote[] moves, EnemyNote[] enemies)
    {
        Compare(Array.ConvertAll(a, EffectiveMovement), Array.ConvertAll(moves, EffectiveMovement));
        Compare(b, enemies);
    }
    private static MoveNote EffectiveMovement(MoveNote note)
    {
        note.time = note.HitTime;
        if (!note.hasDoor) { note.doorTime = 0; note.moveDelay = 0; note.doorFrameStartTime = 0; }
        return note;
    }
    private static void Compare<T>(T[] expected, T[] actual) where T : struct
    {
        Check(expected.Length == actual.Length, "saved and compiled note counts agree");
        for (int i = 0; i < expected.Length; i++)
            foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object a = field.GetValue(expected[i]), b = field.GetValue(actual[i]);
                Check(a is double da && b is double db ? Math.Abs(da - db) < 1e-7 : Equals(a, b),
                    "saved and compiled " + typeof(T).Name + "[" + i + "]." + field.Name);
            }
    }
}
