using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Gun.RoomRhythm;

// Exercises the serialized stage notes, rather than a second hand-copied chart.
internal static class MafiaStageChecks
{
    private static string Value(string text, string key) => Regex.Match(text,
        @"(?m)^\s*" + key + @":\s*([^\r\n]+)").Groups[1].Value.Trim().Trim('"');
    private static double Number(string text, string key) => double.Parse(Value(text, key), CultureInfo.InvariantCulture);
    private static List<string> Rows(string text, string key)
    {
        string section = Regex.Match(text, @"(?ms)^  " + key + @":\r?\n(.*?)(?=^  \w|\z)").Groups[1].Value;
        var rows = new List<string>();
        foreach (Match match in Regex.Matches(section, @"(?ms)^  - (.*?)(?=^  - |\z)")) rows.Add("    " + match.Groups[1].Value);
        return rows;
    }

    public static void Run(string root)
    {
        foreach (string section in new[] { "Guards", "Mafia" })
        {
            string text = File.ReadAllText(Path.Combine(root, "Assets/RoomChart/Stage1_" + section + ".asset"));
            var moves = new List<MoveNote>();
            var enemies = new List<EnemyNote>();
            foreach (string row in Rows(text, "moves")) moves.Add(new MoveNote {
                destinationId = Value(row, "destinationId"), direction = (MoveDirection)Number(row, "direction"),
                time = Number(row, "time"), duration = Number(row, "duration"), ease = (MovementEase)Number(row, "ease"),
                customAppearance = true, appearanceTime = Number(row, "appearanceTime"), appearTime = Number(row, "appearanceTime"),
                frameStartTime = Number(row, "frameStartTime"), doorFrameStartTime = Number(row, "doorFrameStartTime"),
                hasDoor = Number(row, "hasDoor") == 1, doorTime = Number(row, "doorTime"), moveDelay = Number(row, "moveDelay") });
            foreach (string row in Rows(text, "enemies")) enemies.Add(new EnemyNote {
                id = Value(row, "id"), roomId = Value(row, "roomId"), direction = (EnemyDirection)Number(row, "direction"),
                time = Number(row, "time"), customAppearance = true, appearanceTime = Number(row, "appearanceTime"),
                frameStartTime = Number(row, "frameStartTime") });
            var window = new TimingWindow { early = .125, accurate = .03125, late = .125 };
            var run = new RoomRun(moves.ToArray(), window, Number(text, "moveDuration"), enemies.ToArray(), Value(text, "startingRoomId"));
            var actions = new List<(double time, Action action)>();
            for (int i = 0; i < moves.Count; i++) {
                int index = i; MoveNote move = moves[i];
                if (move.hasDoor) actions.Add((move.doorTime, () => run.ShootDoor(index, move.doorTime)));
                actions.Add((move.HitTime, () => run.Press(move.direction, move.HitTime)));
            }
            for (int i = 0; i < enemies.Count; i++) {
                int index = i; double time = enemies[i].time;
                actions.Add((time, () => run.ShootEnemy(index, time)));
            }
            actions.Sort((a, b) => a.time.CompareTo(b.time));
            if (section == "Mafia" && !SectionTiming.CanEnter(moves.ToArray(), enemies.ToArray(), 144 * 60.0 / 130, window))
                throw new Exception("Mafia entry overlaps a judgment window.");
            run.Begin();
            foreach (var action in actions) { run.Advance(action.time); action.action(); }
            run.Advance(actions[actions.Count - 1].time + 1);
            int expected = section == "Guards" ? 66 : 117;
            if (run.Phase != RunPhase.Cleared || run.JudgmentVersion != expected || run.AccurateJudgments != expected)
                throw new Exception(section + " cannot be cleared with all perfect inputs: " + run.Phase);
            Console.WriteLine("PASS: Stage1 " + section + " serialized chart, " + expected + " perfect actions.");
            run.Begin();
            run.AdvanceAutomatically(actions[actions.Count - 1].time + 10);
            if (run.Phase != RunPhase.Cleared || run.JudgmentVersion != expected || run.Failure != FailureReason.None)
                throw new Exception(section + " debug playback did not complete every action.");
            Console.WriteLine("PASS: Stage1 " + section + " invincible automatic playback.");
        }
    }
}
