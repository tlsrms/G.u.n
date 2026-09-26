using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Gun.RoomRhythm;

// Reads the authored flat note lists, so integrated checks cannot silently use an obsolete chart copy.
internal static class AuthoredChartChecks
{
    private static string Field(string text, string key)
    {
        Match match = Regex.Match(text, @"(?m)^\s*(?:- )?" + key + @":\s*([^\r\n]+)");
        if (!match.Success) throw new Exception("Missing chart field: " + key);
        return match.Groups[1].Value.Trim();
    }
    private static double Number(string text, string key) => double.Parse(Field(text, key), CultureInfo.InvariantCulture);
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }

    public static void Run(string path)
    {
        string text = File.ReadAllText(path);
        var moves = new List<MoveNote>();
        foreach (Match match in Regex.Matches(text, @"(?ms)^  - destinationId:.*?(?=^  - destinationId:|^  enemyReadTime:|\z)"))
        {
            string row = match.Value;
            moves.Add(new MoveNote { destinationId = Field(row, "destinationId"), direction = (MoveDirection)Number(row, "direction"),
                time = Number(row, "time"), hasDoor = Number(row, "hasDoor") == 1,
                doorTime = Number(row, "doorTime"), moveDelay = Number(row, "moveDelay") });
        }
        var enemies = new List<EnemyNote>();
        foreach (Match match in Regex.Matches(text, @"(?ms)^  - id:.*?(?=^  - id:|\z)"))
        {
            string row = match.Value;
            enemies.Add(new EnemyNote { id = Field(row, "id"), roomId = Field(row, "roomId"),
                direction = (EnemyDirection)Number(row, "direction"), time = Number(row, "time") });
        }
        var window = new TimingWindow { early = Number(text, "early"), accurate = Number(text, "accurate"), late = Number(text, "late") }.Symmetric;
        double roomLeadTime = Number(text, "roomLeadTime");
        for (int i = 0; i < moves.Count; i++) {
            MoveNote note = moves[i]; note.appearTime = Math.Max(0, note.HitTime - roomLeadTime); moves[i] = note;
        }
        var draft = BeatChartCompiler.Import(Number(text, "bpm"), window, roomLeadTime,
            Number(text, "enemyLeadTime"), moves.ToArray(), enemies.ToArray());
        var converted = BeatChartCompiler.Compile(draft, Field(text, "startingRoomId"));
        Require(converted.Moves.Length == moves.Count && converted.Enemies.Length == enemies.Count, "Import changed note count");
        for (int i = 0; i < moves.Count; i++) {
            Require(converted.Moves[i].destinationId == moves[i].destinationId
                && converted.Moves[i].direction == moves[i].direction
                && Math.Abs(converted.Moves[i].HitTime - moves[i].HitTime) < 1e-9
                && Math.Abs(converted.Moves[i].appearTime - moves[i].appearTime) < 1e-9
                && converted.Moves[i].hasDoor == moves[i].hasDoor, "Import changed authored movement");
            if (moves[i].hasDoor) Require(Math.Abs(converted.Moves[i].doorTime - moves[i].doorTime) < 1e-9, "Import changed authored door");
        }
        for (int i = 0; i < enemies.Count; i++)
            Require(converted.Enemies[i].id == enemies[i].id && converted.Enemies[i].roomId == enemies[i].roomId
                && converted.Enemies[i].direction == enemies[i].direction
                && Math.Abs(converted.Enemies[i].time - enemies[i].time) < 1e-9, "Import changed authored enemy");
        // Play the editor-converted result, not just the original asset data.
        moves = new List<MoveNote>(converted.Moves);
        enemies = new List<EnemyNote>(converted.Enemies);
        var run = new RoomRun(moves.ToArray(), window, Number(text, "moveDuration"), enemies.ToArray(),
            Field(text, "startingRoomId"), Number(text, "enemyReadTime"), Number(text, "enemyLeadTime"));
        var events = new List<(double time, int kind, int index)>();
        for (int i = 0; i < moves.Count; i++) {
            events.Add((moves[i].HitTime, 0, i));
            if (moves[i].hasDoor) events.Add((moves[i].doorTime, 1, i));
        }
        for (int i = 0; i < enemies.Count; i++) events.Add((enemies[i].time, 2, i));
        events.Sort((a,b) => a.time.CompareTo(b.time));
        int scenarios = 0;
        foreach (double offset in new[] { -window.early + .00001, 0, window.late - .00001 })
        {
            // Replay twice to catch persistent door/enemy state after clear.
            for (int replay = 0; replay < 2; replay++) {
                run.Begin();
                foreach (var note in events) {
                    double time = note.time + offset;
                    if (note.kind == 0) run.Press(moves[note.index].direction, time);
                    else Require(note.kind == 1 ? run.ShootDoor(note.index,time) : run.ShootEnemy(note.index,time), "Authored shot failed");
                    Require(run.Phase != RunPhase.Dead, "Authored sequence died");
                }
                run.Advance(events[events.Count-1].time + window.late + Number(text, "moveDuration"));
                Require(run.Phase == RunPhase.Cleared, "Authored sequence did not clear");
                scenarios++;
            }
        }
        // Omit each individual note; every chart entry must be required for survival.
        for (int omitted = 0; omitted < events.Count; omitted++) {
            run.Begin();
            for (int i = 0; i < omitted; i++) {
                var note = events[i];
                if (note.kind == 0) run.Press(moves[note.index].direction,note.time);
                else if (note.kind == 1) run.ShootDoor(note.index,note.time);
                else run.ShootEnemy(note.index,note.time);
            }
            run.Advance(events[omitted].time + window.late);
            Require(run.Phase == RunPhase.Dead, "Omitted authored note did not kill");
            run.Reset();
            Require(run.Phase == RunPhase.Ready && run.JudgmentVersion == 0 && run.CompletedMoves == 0, "Reset after failure failed");
            for(int i=0;i<enemies.Count;i++) Require(!run.EnemyDefeated(i), "Reset retained enemy");
            for(int i=0;i<moves.Count;i++) Require(!run.DoorBroken(i), "Reset retained door");
            scenarios++;
        }
        Console.WriteLine($"PASS: {scenarios} authored chart clear/miss/reset scenarios ({events.Count} notes).");
    }
}
