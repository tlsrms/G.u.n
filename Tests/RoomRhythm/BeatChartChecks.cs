using System;
using Gun.RoomRhythm;

internal static class BeatChartChecks
{
    private static int checks;
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception("Beat chart: " + message); checks++; }
    private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-9;

    private static BeatChart Sample() => BeatChartCompiler.Import(120,
        new TimingWindow { early = .15, accurate = .05, late = .15 }, 2, 1.5,
        new[] {
            new MoveNote { destinationId = "north", direction = MoveDirection.Up, time = 4 },
            new MoveNote { destinationId = "east", direction = MoveDirection.Right, hasDoor = true, doorTime = 8, moveDelay = 1 }
        }, new[] {
            new EnemyNote { id = "north-enemy", roomId = "north", direction = EnemyDirection.UpLeft, time = 6 },
            new EnemyNote { id = "east-enemy", roomId = "east", direction = EnemyDirection.UpRight, time = 10.5 }
        });

    private static void Reject(Action<BeatChart> edit, string message)
    {
        BeatChart chart = Sample(); edit(chart); bool rejected = false;
        try { BeatChartCompiler.Compile(chart, "start"); } catch (ArgumentException) { rejected = true; }
        Check(rejected, message);
    }

    public static void Run()
    {
        BeatChart draft = Sample();
        var compiled = BeatChartCompiler.Compile(draft, "start");
        Check(compiled.Moves.Length == 2 && compiled.Enemies.Length == 2, "all three note kinds compile");
        Check(Near(compiled.Moves[0].HitTime, 4) && Near(compiled.Moves[1].doorTime, 8)
            && Near(compiled.Moves[1].HitTime, 9), "existing move and door times round trip");
        Check(Near(compiled.Enemies[1].time, 10.5) && Near(compiled.Moves[1].moveDelay, 1), "enemy and door delay round trip");
        Check(Near(compiled.Timing.early, .15) && Near(compiled.Timing.accurate, .05), "judgment tolerances round trip");
        Check(Near(compiled.Moves[0].appearTime, 2) && Near(compiled.RoomLeadSeconds, 2)
            && Near(compiled.EnemyLeadSeconds, 1.5), "lead beats derive appearance times");
        var run = new RoomRun(compiled.Moves, compiled.Timing, .18, compiled.Enemies, "start", .25, compiled.EnemyLeadSeconds);
        run.Begin(); run.Press(MoveDirection.Up, 4);
        Check(run.ShootEnemy(0, 6) && run.ShootDoor(1, 8), "compiled enemy then door is playable");
        run.Press(MoveDirection.Right, 9);
        Check(run.ShootEnemy(1, 10.5) && run.Phase == RunPhase.Cleared, "compiled chart clears");
        draft.bpm = 60; compiled = BeatChartCompiler.Compile(draft, "start");
        Check(Near(compiled.Moves[0].HitTime, 8) && Near(compiled.Moves[1].doorTime, 16)
            && Near(compiled.Enemies[1].time, 21), "BPM scales every note kind");
        Check(Near(compiled.Timing.early, .3) && Near(compiled.Timing.accurate, .1)
            && Near(compiled.RoomLeadSeconds, 4), "BPM scales windows and leads");
        draft.offsetSeconds = .375; compiled = BeatChartCompiler.Compile(draft, "start");
        Check(Near(compiled.Moves[0].HitTime, 8.375) && Near(compiled.Moves[1].doorTime, 16.375), "audio offset shifts notes");
        Check(Near(compiled.Moves[1].moveDelay, 2) && Near(compiled.Timing.early, .3), "offset does not change duration or window");
        draft.subdivision = 3;
        Check(Near(draft.Snap(.34), 1.0 / 3) && Near(draft.Snap(.68), 2.0 / 3), "triplet snap");
        draft.subdivision = 16;
        Check(Near(draft.Snap(.061), .0625), "fine subdivision snap");
        draft = Sample(); draft.notes[0].beat = 8.12345;
        compiled = BeatChartCompiler.Compile(draft, "start");
        Check(Near(compiled.Moves[0].HitTime, 4.061725), "compile preserves off-grid notes until explicit snap");
        Array.Reverse(draft.notes);
        BeatNote first = draft.notes[0]; compiled = BeatChartCompiler.Compile(draft, "start");
        Check(compiled.Moves[0].destinationId == "north" && draft.notes[0].enemyId == first.enemyId, "sorting does not mutate draft");
        Reject(c => c.bpm = 0, "zero BPM rejected");
        Reject(c => c.bpm = double.NaN, "NaN BPM rejected");
        Reject(c => c.offsetSeconds = -1, "negative offset rejected");
        Reject(c => c.notes[0].beat = -1, "negative beat rejected");
        Reject(c => c.notes[0].beat = double.PositiveInfinity, "infinite beat rejected");
        Reject(c => c.notes[2].beat = c.notes[1].beat, "door at movement time rejected");
        Reject(c => c.notes[2].roomId = "missing", "orphan door rejected");
        Reject(c => c.notes[1].roomId = "north", "duplicate room ID rejected");
        Reject(c => c.notes[1].beat = c.notes[0].beat, "simultaneous moves rejected");
        Reject(c => c.notes[4].enemyId = c.notes[3].enemyId, "duplicate enemy ID rejected");
        Reject(c => c.notes[4].roomId = "missing", "orphan enemy rejected");
        Reject(c => c.notes[4].enemyDirection = (EnemyDirection)99, "invalid enemy direction rejected");
        Reject(c => c.accurateBeats = c.toleranceBeats, "invalid judgment windows rejected");
        Reject(c => c.enemyLeadBeats = double.NaN, "invalid lead rejected");
        Reject(c => c.notes = Array.Empty<BeatNote>(), "empty chart rejected");
        Console.WriteLine($"PASS: {checks} beat editor conversion/validation checks.");
    }
}
