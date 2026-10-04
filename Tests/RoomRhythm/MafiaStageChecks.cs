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
            if (field.FieldType == typeof(EnemyPlacement) || field.FieldType.IsArray || !field.FieldType.IsValueType && field.FieldType != typeof(string)) continue;
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
        {
            string data = prefix + "  " + row.Groups[1].Value;
            object item = Fields<T>(data, indentation + 2);
            if (item is MapEnemy enemy) enemy.placement = Fields<EnemyPlacement>(data, indentation + 4);
            if (item is EnemyNote note) { note.placement = Fields<EnemyPlacement>(data, indentation + 4); item = note; }
            rows.Add((T)item);
        }
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
        // Unapplied map drafts may intentionally differ while the user is authoring.
        var map = Read(path, "appliedMap");
        var chart = Compile(map);
        CompareAuthoredNotes(path, chart);
        int doors = Array.FindAll(chart.Moves, n => n.hasDoor).Length;
        int judgments = chart.Moves.Length + chart.Enemies.Length + doors;
        int expectedEvents = judgments + chart.Moves.Length;
        foreach (double offset in new[] { 0.0, -.03, .03 })
        {
            var events = Replay(chart, map.settings.startingRoomId, offset);
            Check(events.Count == expectedEvents && !events.Exists(e => e.Kind == RoomActionKind.Failed),
                "all judgments and arrivals survive corridor/office playback");
        }
        string firstGuard = Array.Find(chart.Enemies, n => n.roomId == "room_fe5599bc").id;
        foreach (double delta in new[] { -.2, .2 })
        {
            var events = Replay(chart, map.settings.startingRoomId, 0, firstGuard, delta);
            Check(events.FindAll(e => e.Kind == RoomActionKind.Failed).Count == 1, "corridor early/late shot fails once");
        }
        var entry = Array.Find(chart.Moves, n => n.destinationId == "room_5b48893d");
        var exit = Array.Find(chart.Moves, n => n.destinationId == "mafia_office_escape");
        var timing = new MafiaIntroTiming(entry.HitTime + entry.Duration(.28), exit.doorTime,
            exit.HitTime, exit.HitTime + exit.Duration(.28), 60.0 / map.settings.bpm);
        Check(exit.hasDoor && exit.direction == MoveDirection.Up, "window keeps ordinary upward Door/Move notes");
        Check(map.Width(map.Room(entry.destinationId)) == map.roomSize * 2, "office width is two rooms");
        Check(Math.Abs((timing.End - timing.Start) / timing.BeatSeconds - 15) < .001, "authored intro lasts 15 beats between arrivals");
        Check(timing.ShotTime(2) < timing.Window, "all three shots precede window judgment");
        for (int shot = 0; shot < 3; shot++)
        {
            double previous = 0;
            for (double t = timing.ShotTime(shot); t < timing.Escape + .4; t += .005)
            {
                double progress = timing.BulletProgress(t, shot, double.NaN);
                Check(progress >= previous - 1e-8, "bullet advances without jumping backward");
                previous = progress;
            }
            Check(timing.BulletProgress(timing.Window, shot, double.NaN) < 1, "slow bullet leaves time to shoot window");
            Check(timing.BulletProgress(timing.Escape + .3, shot, timing.Escape) > 1, "bullet passes old position after dodge");
        }
        var run = NewRun(chart, map.settings.startingRoomId);
        run.AdvanceAutomatically(timing.BurstStart);
        Check(run.Phase == RunPhase.Waiting && run.CompletedMoves == Array.FindIndex(chart.Moves, n => n.destinationId == entry.destinationId) + 1, "intro gap keeps player in office");
        int before = run.JudgmentVersion;
        run.AdvanceAutomatically(timing.Window - .1);
        Check(run.JudgmentVersion == before, "cinematic shots create no extra judgments");
        run.Advance(timing.Window + .1);
        Check(run.Phase == RunPhase.Dead && run.Failure == FailureReason.MissedDoor, "missing window shot retains the normal door failure");
        run.Begin();
        run.AdvanceAutomatically(190);
        Check(run.Phase == RunPhase.Cleared && run.JudgmentVersion == judgments && run.AccuracyPercent == 100, "debug completes full chart");
        run.Begin(); run.AdvanceAutomatically(190);
        Check(run.Phase == RunPhase.Cleared && run.JudgmentVersion == judgments, "restart does not accumulate prior score");
        run.Begin(); run.Advance(190);
        Check(run.Phase == RunPhase.Dead, "manual play still requires input");
        Console.WriteLine("PASS: " + checks + " Mafia chart/intro checks; saved notes, manual/debug, 3 cosmetic shots, window, continuation and restart.");
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
