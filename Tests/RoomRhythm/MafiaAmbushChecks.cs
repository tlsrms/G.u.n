using System;
using Gun.RoomRhythm;

internal static class MafiaAmbushChecks
{
    private static int checks;
    private static void Check(bool value, string label)
    { if (!value) throw new Exception("FAIL ambush: " + label); checks++; }
    private static MafiaAmbushTiming Build(MoveNote[] notes, string id = "escape", double bpm = 120, double lead = 4)
        => new MafiaAmbushTiming(notes, id, bpm, .2, lead, .75, .72, .65, .12);
    private static void Reject(Action action, string label)
    { try { action(); } catch (ArgumentException) { checks++; return; } throw new Exception("FAIL ambush rejection: " + label); }
    public static void Run()
    {
        var notes = new[] {
            new MoveNote { destinationId = "ambush", direction = MoveDirection.Up, time = 2 },
            new MoveNote { destinationId = "escape", direction = MoveDirection.Right, time = 6 },
            new MoveNote { destinationId = "next", direction = MoveDirection.Down, time = 10 }
        };
        var t = Build(notes);
        Check(t.ExitIndex == 1 && Math.Abs(t.Arrival - 2.2) < 1e-8, "source visit is before the selected destination");
        Check(t.Open == 4 && t.Shot(.12) == 6, "lead is beats; first shot matches exact escape judgment");
        Check(Math.Abs(t.Shot(.44) - 6.32) < 1e-8, "remaining shots follow clip timing");
        Check(!t.Visible(2, 1) && t.Visible(2.2, 1), "closed door visible only after source arrival");
        Check(!t.Visible(4, 0) && t.Visible(4, 1), "future copy cannot appear in an earlier room");
        Check(t.Visible(6.4, 2) && !t.Visible(6.4, 3), "attack may finish in vacated room but cannot leak to later visits");
        Check(!t.Visible(t.End, 2), "retreat finishes and hides");
        var shifted = (MoveNote[])notes.Clone();
        for (int i = 0; i < shifted.Length; i++) shifted[i].time += 8;
        var moved = Build(shifted);
        Check(Math.Abs(moved.Open - t.Open - 8) < 1e-8 && Math.Abs(moved.Burst - t.Burst - 8) < 1e-8
            && Math.Abs(moved.End - t.End - 8) < 1e-8, "moving notes carries complete effect");
        Check(Build(notes, "next").ExitIndex == 2 && t.ExitIndex == 1, "copies bind independently");
        Check(Build(notes, "ambush", 120, 2).Arrival == 0, "starting room supported");
        Reject(() => Build(notes, "deleted"), "deleted destination");
        Reject(() => Build(notes, ""), "unassigned prefab");
        Reject(() => Build(notes, bpm: double.NaN), "invalid tempo");
        Reject(() => Build(notes, lead: double.PositiveInfinity), "invalid lead");
        Reject(() => Build(notes, lead: 20), "start before source arrival");
        Reject(() => Build(notes, lead: .25), "insufficient entry motion time");

        var enemy = new EnemyNote { id = "regular", roomId = "ambush", time = 3.5 };
        var run = new RoomRun(notes, new TimingWindow { early = .05, late = .05, accurate = .02 }, .2,
            new[] { enemy }, "start");
        for (int attempt = 0; attempt < 2; attempt++)
        {
            run.Reset(); run.Begin(); run.Press(MoveDirection.Up, 2); run.Advance(2.2);
            Check(run.Phase == RunPhase.Waiting, "source room normal after reset");
            run.Advance(3.6);
            Check(run.Phase == RunPhase.Dead && run.Failure == FailureReason.MissedEnemy, "visual shield does not bypass normal enemy failure");
        }
        run.Reset(); run.Begin(); run.AdvanceAutomatically(6.3, _ => { });
        Check(run.CompletedMoves == 2 && run.EnemyDefeated(0), "normal enemy and escape retain debug auto judgments");
        var resetTiming = Build(notes);
        Check(resetTiming.Open == t.Open && resetTiming.Shot(.12) == t.Shot(.12), "restart does not accumulate timing offsets");
        Console.WriteLine($"PASS: {checks} shield ambush timing and judgment checks.");
    }
}
