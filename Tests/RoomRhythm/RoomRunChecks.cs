using System;
using Gun.RoomRhythm;

internal static class RoomRunChecks
{
    private static int checks;
    private static readonly TimingWindow Window = new TimingWindow { early = .125, accurate = .03125, late = .125 };
    private static readonly MoveNote[] Notes = {
        new MoveNote { destinationId = "north", direction = MoveDirection.Up, time = 2 },
        new MoveNote { destinationId = "east", direction = MoveDirection.Right, time = 4 }
    };

    private static RoomRun NewRun()
    {
        var run = new RoomRun(Notes, Window, .125);
        run.Begin();
        return run;
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        checks++;
    }

    // Independent rectangle intersection check: includes cross-edge corner intersections,
    // not just the approach centerline distance used by the production calculation.
    private static bool RoomStrokesOverlap(double radius, double width)
    {
        const double half = 3, passage = 2.8;
        double length = half - passage * .5 + width * .5;
        double center = (half + passage * .5 + width * .5) * .5;
        var walls = new System.Collections.Generic.List<(double x, double y, double w, double h)>();
        foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 }) {
            walls.Add((x * center, y * half, length, width));
            walls.Add((x * half, y * center, width, length));
        }
        var lines = new[] {
            (x: 0.0, y: radius, w: radius * 2 + width, h: width),
            (x: 0.0, y: -radius, w: radius * 2 + width, h: width),
            (x: radius, y: 0.0, w: width, h: radius * 2 + width),
            (x: -radius, y: 0.0, w: width, h: radius * 2 + width)
        };
        foreach (var wall in walls) foreach (var line in lines)
            if (Math.Abs(wall.x - line.x) <= (wall.w + line.w) * .5 + 1e-10
                && Math.Abs(wall.y - line.y) <= (wall.h + line.h) * .5 + 1e-10) return true;
        return false;
    }

    private static void CheckActionResults()
    {
        var run = new RoomRun(new[] {
            new MoveNote { destinationId = "next", direction = MoveDirection.Up,
                hasDoor = true, doorTime = 1, moveDelay = 1 }
        }, Window, .125, new[] {
            new EnemyNote { id = "target", roomId = "next", time = 3 }
        });
        run.Begin();
        Check(run.ShootDoor(0, 1), "event door accepted");
        run.Press(MoveDirection.Up, 2);
        run.Advance(2.5);
        Check(run.ShootEnemy(0, 3), "event enemy accepted");
        var kinds = new[] { RoomActionKind.DoorBroken, RoomActionKind.MoveStarted,
            RoomActionKind.MoveArrived, RoomActionKind.EnemyDefeated };
        var times = new[] { 1.0, 2.0, 2.125, 3.0 };
        for (int i = 0; i < kinds.Length; i++)
        {
            Check(run.TryDequeueResult(out var result), "all batched actions retained");
            Check(result.Kind == kinds[i] && result.Time == times[i], "ordered action and exact time");
            Check(result.TargetId == (i == 3 ? "target" : "next"), "stable action target");
        }
        Check(!run.TryDequeueResult(out _), "actions delivered once");
        Check(run.Phase == RunPhase.Cleared && run.JudgmentVersion == 3, "events preserve score and completion");
        run.Begin();
        run.Advance(1.5);
        Check(run.TryDequeueResult(out var failure) && failure.Kind == RoomActionKind.Failed
            && failure.Failure == FailureReason.MissedDoor && failure.TargetId == "next"
            && failure.Time == 1.125, "timeout event carries deadline and target");
        run.Advance(5);
        Check(!run.TryDequeueResult(out _), "failure delivered once");
        run.Begin();
        run.ShootDoor(0, 1);
        run.Reset();
        Check(!run.TryDequeueResult(out _), "reset drops stale actions");
    }

    private static void CheckAutomaticPlayback()
    {
        var moves = new[] {
            new MoveNote { destinationId = "next", direction = MoveDirection.Right,
                hasDoor = true, doorTime = 1, moveDelay = 1, time = 2 }
        };
        var enemies = new[] {
            new EnemyNote { id = "start_enemy", roomId = "start", time = .5 },
            new EnemyNote { id = "arrival_enemy", roomId = "next", time = 2.1 },
            new EnemyNote { id = "last_enemy", roomId = "next", time = 3 }
        };
        var run = new RoomRun(moves, Window, .25, enemies);
        run.Begin();
        int presentations = 0;
        run.AdvanceAutomatically(.25, _ => presentations++);
        Check(run.JudgmentVersion == 0, "auto does not consume future notes");
        run.AdvanceAutomatically(2.1, _ => presentations++);
        Check(run.Phase == RunPhase.Moving && run.EnemyDefeated(1) && run.DoorBroken(0),
            "auto shoots during movement and breaks entrance");
        run.AdvanceAutomatically(10, _ => presentations++);
        Check(run.Phase == RunPhase.Cleared && run.Failure == FailureReason.None,
            "auto survives skipped frames and clears last-room enemies");
        Check(run.JudgmentVersion == 5 && run.AccurateJudgments == 5 && presentations == 6,
            "auto delivers every action and presentation once");
        double previous = double.NegativeInfinity;
        while (run.TryDequeueResult(out var action))
        {
            Check(action.Time >= previous && action.Kind != RoomActionKind.Failed, "auto chronological success events");
            previous = action.Time;
        }
        run.AdvanceAutomatically(20, _ => presentations++);
        Check(presentations == 6, "auto completion does not duplicate effects");
        run.Begin();
        run.Advance(10);
        Check(run.Phase == RunPhase.Dead, "ordinary playback still fails without input after reset");
        run.Begin();
        run.AdvanceAutomatically(double.NaN);
        run.AdvanceAutomatically(double.PositiveInfinity);
        Check(run.JudgmentVersion == 0, "auto rejects invalid clocks");
        run.AdvanceAutomatically(10);
        Check(run.Phase == RunPhase.Cleared, "auto restarts without stale state");

        // Valid charts can require an early input to fit consecutive moves. Debug mode
        // must remain invincible when its exact-beat movement reaches the next note late.
        var overlap = new RoomRun(new[] {
            new MoveNote { destinationId = "a", time = 1 },
            new MoveNote { destinationId = "b", time = 1.2 }
        }, Window, .3);
        overlap.Begin();
        overlap.AdvanceAutomatically(10);
        Check(overlap.Phase == RunPhase.Cleared && overlap.CompletedMoves == 2,
            "auto completes overlapping movement windows without death");
    }

    public static void Main(string[] args)
    {
        CheckAutomaticPlayback();
        if (args.Length > 0)
        {
            MafiaStageChecks.Run(args[0]);
        }
        var absoluteRun = new RoomRun(new[] { new MoveNote { destinationId = "next", time = 20, direction = MoveDirection.Up } }, Window, .25);
        absoluteRun.Begin(); absoluteRun.Advance(15);
        absoluteRun.Press(MoveDirection.Up, 14);
        Check(absoluteRun.Phase == RunPhase.Waiting && absoluteRun.JudgmentVersion == 0, "out-of-order input cannot affect an advanced run");
        absoluteRun.Press(MoveDirection.Up, 20); absoluteRun.Advance(20.25);
        Check(absoluteRun.Phase == RunPhase.Cleared && absoluteRun.AccuracyPercent == 100, "run uses absolute song time");
        CheckActionResults();
        var accuracyRun = new RoomRun(new[] {
            new MoveNote { destinationId = "next", hasDoor = true, doorTime = 2, moveDelay = 1 }
        }, Window, .125, new[] {
            new EnemyNote { id = "target", roomId = "start", time = 1 }
        });
        accuracyRun.Begin();
        Check(accuracyRun.AccuracyPercent == 0, "unplayed run has no accuracy");
        accuracyRun.ShootEnemy(0, 1);
        accuracyRun.ShootDoor(0, 2.0625);
        accuracyRun.Press(MoveDirection.Up, 3);
        accuracyRun.Advance(3.125);
        Check(accuracyRun.Phase == RunPhase.Cleared && accuracyRun.JudgmentVersion == 3,
            "accuracy includes enemy, door and movement judgments");
        Check(accuracyRun.AccurateJudgments == 2 && Math.Abs(accuracyRun.AccuracyPercent - 200.0 / 3) < .00001,
            "early judgments lower accurate-hit percentage");
        accuracyRun.Begin();
        Check(accuracyRun.AccurateJudgments == 0 && accuracyRun.AccuracyPercent == 0, "retry resets accuracy");
        accuracyRun.ShootEnemy(0, 1);
        accuracyRun.ShootDoor(0, 2);
        accuracyRun.ShootDoor(0, 2);
        accuracyRun.Press(MoveDirection.Up, 3);
        accuracyRun.Advance(3.125);
        Check(accuracyRun.AccurateJudgments == 3 && accuracyRun.AccuracyPercent == 100,
            "all accurate hits score 100 without counting a repeated door shot");
        var global = JudgmentSettings.Window;
        int settingsRevision = JudgmentSettings.Revision;
        JudgmentSettings.Configure(20, 40);
        Check(JudgmentSettings.Window.accurate == .02 && JudgmentSettings.Window.late == .04,
            "project settings convert milliseconds to seconds");
        Check(JudgmentSettings.Revision != settingsRevision, "changed settings invalidate configured sessions");
        foreach (double invalid in new[] { -1.0, 40, double.NaN, double.PositiveInfinity })
        {
            bool invalidRejected = false;
            try { JudgmentSettings.Configure(invalid, 40); } catch (ArgumentException) { invalidRejected = true; }
            Check(invalidRejected && JudgmentSettings.AccurateMs == 20, "invalid settings preserve previous valid values");
        }
        JudgmentSettings.Configure(JudgmentSettings.DefaultAccurateMs, JudgmentSettings.DefaultToleranceMs);
        foreach (double target in new[] { 0.0, 2.0, 123.456 })
        {
            Check(global.Judge(target - .025, target) == TimingGrade.Accurate, "global -25ms inclusive");
            Check(global.Judge(target + .025, target) == TimingGrade.Accurate, "global +25ms inclusive");
            Check(global.Judge(target - .035, target) == TimingGrade.Early, "global -35ms inclusive");
            Check(global.Judge(target + .035, target) == TimingGrade.Late, "global +35ms inclusive");
            Check(global.Judge(target - .035001, target) == TimingGrade.TooEarly, "global early outside window");
            Check(global.Judge(target + .035001, target) == TimingGrade.TooLate, "global late outside window");
        }
        var emptyShot = NewRun(); emptyShot.MissShot(1);
        Check(emptyShot.IsActive && emptyShot.Failure == FailureReason.None && emptyShot.JudgmentVersion == 0,
            "shot without a target has no penalty or judgment");
        emptyShot.MissShot(2.126);
        Check(emptyShot.Failure == FailureReason.TooLate, "empty shots do not prevent a pending note from expiring");
        foreach (double offset in new[] { -.035001, -.035, -.025, 0, .025, .035, .035001 })
        {
            bool accepted = Math.Abs(offset) <= .035;
            var movement = new RoomRun(new[] { new MoveNote { destinationId = "next", time = 1 } }, global, .18);
            movement.Begin(); movement.Press(MoveDirection.Up, 1 + offset);
            Check((movement.Phase == RunPhase.Moving) == accepted, "global movement window " + offset);
            var door = new RoomRun(new[] { new MoveNote { destinationId = "next", hasDoor = true,
                doorTime = 1, moveDelay = 1, appearTime = 0 } }, global, .18);
            door.Begin();
            Check(door.ShootDoor(0, 1 + offset) == accepted, "global door window " + offset);
            Check((door.Phase == RunPhase.Dead) == (offset > .035), "only expired door window kills " + offset);
            var enemy = new RoomRun(new[] { new MoveNote { destinationId = "next", time = 3 } }, global, .18,
                new[] { new EnemyNote { id = "enemy", roomId = "start", time = 1 } });
            enemy.Begin();
            Check(enemy.ShootEnemy(0, 1 + offset) == accepted, "global enemy window " + offset);
            Check((enemy.Phase == RunPhase.Dead) == !accepted, "outside global enemy window kills " + offset);
        }
        // An event can arrive in the next input batch with a timestamp before the previous
        // MonoBehaviour.Update clock read. Advancing beyond the input watermark loses it.
        var prematurelyAdvanced = NewRun();
        prematurelyAdvanced.Advance(2.11);
        prematurelyAdvanced.Press(MoveDirection.Up, 2.10);
        prematurelyAdvanced.Advance(2.126);
        Check(prematurelyAdvanced.Failure == FailureReason.TooLate,
            "reproduces valid event discarded after advancing past input batch");
        var batched = NewRun();
        batched.Advance(2.08); // Only commit the last completed input batch.
        batched.Press(MoveDirection.Up, 2.10); // Arrives next frame at real time 2.14.
        batched.Advance(2.14);
        Check(batched.Phase == RunPhase.Moving && batched.LastGrade == TimingGrade.Late,
            "valid event survives next-frame delivery when clock respects input watermark");
        batched = NewRun(); batched.Advance(2.08); batched.Press(MoveDirection.Up, 2.126);
        Check(batched.Failure == FailureReason.TooLate, "batch ordering does not extend the success deadline");
        batched = NewRun(); batched.Advance(2.08); batched.Advance(2.14);
        Check(batched.Failure == FailureReason.TooLate, "missing input expires when input batch reaches deadline");
        foreach (double lead in new[] { .05, .25, 1.0, 8.0 })
        {
            double start = 10 - lead;
            double R(double time) => ApproachGeometry.FixedStartRadius(time, start, 10, 6, 3);
            Check(Math.Abs(R(start) - 6) < 1e-9, "Every room starts at the same size regardless of lead time");
            Check(Math.Abs(R(start + lead * .5) - 4.5) < 1e-9, "Each room scales using its own remaining duration");
            Check(Math.Abs(R(10) - 3) < 1e-9, "Room frame reaches the wall on the exact beat");
            Check(R(10 + lead * .1) < 3, "Room frame keeps shrinking past a missed beat");
            Check(R(start - 1) == 6, "Seeking before appearance does not inflate the fixed starting size");
        }
        Check(ApproachGeometry.FixedStartRadius(2, 2, 2, 6, 3) == 3, "Zero lead time resolves immediately without division by zero");
        Check(ApproachGeometry.FixedStartRadius(1, 0, 2, 6, 9) == 7.5,
            "A twelve-unit frame can expand toward an eighteen-unit room side");
        Check(ApproachGeometry.FixedStartRadius(2, 0, 2, 6, 9) == 9,
            "An expanding frame reaches the room edge on the exact beat");
        Check(ApproachGeometry.FixedStartRadius(1, 0, 2, 6, 6) == 6,
            "Equal starting and target sizes remain stable");
        Check(ApproachGeometry.FixedStartRadius(.1, 0, .25, 6, 3)
            < ApproachGeometry.FixedStartRadius(.1, 0, 2, 6, 3), "Short lead time shrinks faster than long lead time");
        foreach (double radius in new[] { .38, 3.0 })
        foreach (double width in new[] { .1, .2, .3 })
        foreach (var timing in new[] { Window, new TimingWindow { early = .25, accurate = .05, late = .375 }.Symmetric })
        {
            double R(double time) => ApproachGeometry.Radius(time, 2, radius, width, timing);
            Check(Math.Abs(R(2) - radius) < 1e-10, "centerlines coincide at exact timing");
            Check(Math.Abs(R(2 - timing.early) - radius - width) < 1e-10, "outer contact matches early window start");
            Check(R(2 - timing.early - .001) - radius > width, "no overlap before success window");
            Check(Math.Abs(R(2 - timing.early + .001) - radius) < width, "positive overlap just inside early window");
            Check(R(2 + timing.late * .5) < radius && radius - R(2 + timing.late * .5) < width,
                "late approach keeps shrinking inside white outline");
            Check(Math.Abs(radius - R(2 + timing.late) - width) < 1e-10
                && timing.Judge(2 + timing.late, 2) == TimingGrade.Late, "last contact is an inclusive late success");
            Check(R(2 + timing.late + .01) < R(2 + timing.late), "failure frame moves past contact instead of clamping to white outline");
            double step = .01;
            double distance = ApproachGeometry.Speed(width, timing) * step;
            foreach (double time in new[] { -3.0, 0, 2 - timing.early, 2.0, 2 + timing.late * .5 })
                Check(Math.Abs(R(time) - R(time + step) - distance) < 1e-10,
                    "constant speed through anticipation, early and late windows");
            if (radius == 3)
                foreach (double offset in new[] { -1.01, -.9999, -.8, -.2, 0, .2, .8, .9999, 1, 1.01 }) {
                    double time = 2 + offset * timing.early;
                    TimingGrade grade = timing.Judge(time, 2);
                    bool accepted = grade == TimingGrade.Early || grade == TimingGrade.Accurate || grade == TimingGrade.Late;
                    Check(RoomStrokesOverlap(R(time), width) == accepted,
                        "actual wall/approach rectangles including corners agree with judgment");
                    double growing = ApproachGeometry.ExpandingRadius(time, 2, radius, width, timing);
                    Check(RoomStrokesOverlap(growing, width) == accepted,
                        "expanding door outline overlaps for the same success window");
                }
        }
        var symmetric = new TimingWindow { early = .25, accurate = .05, late = .375 }.Symmetric;
        Check(symmetric.early == symmetric.late && symmetric.early + symmetric.late == .625,
            "symmetric conversion preserves total success window");
        var scheduledEnemies = new RoomRun(new[] {
            new MoveNote { destinationId = "north", direction = MoveDirection.Up, time = 2 }
        }, Window, .125, new[] {
            new EnemyNote { id = "scheduled", roomId = "north", direction = EnemyDirection.Up, time = 4 }
        }, enemyLeadTime: 1);
        scheduledEnemies.Begin(); scheduledEnemies.Press(MoveDirection.Up, 2); scheduledEnemies.Advance(2.2);
        Check(scheduledEnemies.EnemyAppearsAt(0) == 3 && !scheduledEnemies.EnemyAvailable(0), "enemy lead time delays display after arrival");
        Check(!scheduledEnemies.ShootEnemy(0, 2.9), "hidden enemy cannot be shot");
        scheduledEnemies.Advance(3);
        Check(scheduledEnemies.EnemyAvailable(0), "enemy becomes aimable at appearance boundary");
        Check(scheduledEnemies.ShootEnemy(0, 4), "appearance lead leaves music judgment time unchanged");
        scheduledEnemies.Begin();
        Check(!scheduledEnemies.EnemyAvailable(0), "reset hides scheduled enemy");
        bool shortLeadRejected = false;
        try { new RoomRun(new[] { new MoveNote { destinationId = "north", direction = MoveDirection.Up, time = 2 } },
            Window, .125, new[] { new EnemyNote { id = "bad", roomId = "north", direction = EnemyDirection.Up, time = 4 } },
            enemyLeadTime: .2); } catch (ArgumentException) { shortLeadRejected = true; }
        Check(!shortLeadRejected, "Short enemy lead is allowed without a mandatory reading delay");
        Check(Window.Judge(0, 2) == TimingGrade.TooEarly, "arbitrarily early input is fatal");
        Check(Window.Judge(1.875, 2) == TimingGrade.Early, "early success inclusive boundary");
        Check(Window.Judge(1.96875, 2) == TimingGrade.Accurate, "accurate starts inclusively");
        Check(Window.Judge(2.03125, 2) == TimingGrade.Accurate, "accurate ends inclusively");
        Check(Window.Judge(2.0625, 2) == TimingGrade.Late, "late success");
        Check(Window.Judge(2.125, 2) == TimingGrade.Late, "late success inclusive boundary");

        var run = NewRun();
        run.Press(MoveDirection.Up, -0.1);
        Check(run.Phase == RunPhase.Dead && run.Failure == FailureReason.TooEarly, "input during scheduled audio lead-in is not ignored");
        run = NewRun();
        run.Press(MoveDirection.Left, 2);
        Check(run.Failure == FailureReason.WrongDirection, "wrong direction");
        run = NewRun();
        run.Advance(2.126);
        Check(run.Phase == RunPhase.Dead, "no input expires automatically");

        run = NewRun();
        run.Press(MoveDirection.Up, 2.0625);
        Check(run.LastGrade == TimingGrade.Late && run.Phase == RunPhase.Moving, "late input accepted");
        Check(run.LastTimingErrorMs == 62.5, "movement records signed millisecond error");
        // A frame ends after the note's deadline, but the input occurred before it.
        run.Advance(2.25);
        Check(run.Phase == RunPhase.Waiting && run.CompletedMoves == 1, "valid timestamped input survives a long frame");
        run.Press(MoveDirection.Right, 4);
        Check(run.LastGrade == TimingGrade.Accurate, "late first move does not shift next note");
        run.Advance(4.124);
        Check(run.Phase == RunPhase.Moving, "clear waits for arrival");
        run.Advance(4.125);
        Check(run.Phase == RunPhase.Cleared && run.CompletedMoves == 2, "clear at last arrival");

        run = NewRun();
        run.Press(MoveDirection.Up, 2);
        run.Press(MoveDirection.Left, 2.01);
        run.Press(MoveDirection.Right, 2.02);
        Check(run.Phase == RunPhase.Moving && run.Failure == FailureReason.None, "all movement inputs ignored during animation");
        run.Advance(2.125);
        Check(run.Phase == RunPhase.Waiting && run.CompletedMoves == 1, "ignored input not buffered");
        run.Advance(4.126);
        Check(run.Phase == RunPhase.Dead, "second note still requires a fresh press");

        run.Reset();
        Check(run.Phase == RunPhase.Ready && run.CompletedMoves == 0
            && run.LastGrade == TimingGrade.None && run.Failure == FailureReason.None, "reset clears all result state");
        Check(!run.LastTimingErrorMs.HasValue, "reset clears timing error");
        run.Press(MoveDirection.Up, 2);
        Check(run.Phase == RunPhase.Ready, "ready state does not process movement");
        run.Advance(1000);
        Check(run.Phase == RunPhase.Ready && run.CompletedMoves == 0 && run.JudgmentVersion == 0,
            "retry can wait indefinitely in the starting room without advancing the chart");
        run.Begin();
        run.Press(MoveDirection.Up, 1.875);
        run.Advance(2);
        Check(run.CompletedMoves == 1 && run.LastGrade == TimingGrade.Early, "restart uses original timeline");

        run = NewRun();
        run.Press(MoveDirection.Up, 2);
        run.Press(MoveDirection.Left, 2.125);
        Check(run.Phase == RunPhase.Dead && run.Failure == FailureReason.WrongDirection,
            "input at animation end targets next room");

        run = NewRun();
        run.Advance(5);
        run.Press(MoveDirection.Up, 2);
        Check(run.Phase == RunPhase.Dead, "stale event cannot revive dead run");

        bool rejected = false;
        try
        {
            new RoomRun(new[] { Notes[0], new MoveNote { destinationId = "east", direction = MoveDirection.Right, time = 2.2 } }, Window, .5);
        }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "impossible consecutive movement rejected");

        foreach (MoveDirection direction in Enum.GetValues(typeof(MoveDirection)))
        {
            var single = new RoomRun(new[] { new MoveNote { destinationId = "target", direction = direction, time = 1 } }, Window, .125);
            single.Begin(); single.Press(direction, 1); single.Advance(1.125);
            Check(single.Phase == RunPhase.Cleared, "cardinal direction " + direction);
        }

        var doorNotes = new[] {
            new MoveNote { destinationId = "door-room", direction = MoveDirection.Up,
                appearTime = 0, hasDoor = true, doorTime = 1.5, moveDelay = .5 }
        };
        var doorRun = new RoomRun(doorNotes, Window, .125);
        doorRun.Begin();
        Check(!doorRun.ShootDoor(0, .5) && doorRun.IsActive, "early door shot permits retry");
        Check(!doorRun.ShootDoor(0, .6) && doorRun.IsActive, "repeated early door shots permit retry");
        doorRun.Begin();
        Check(!doorRun.ShootDoor(-1, 1) && doorRun.IsActive, "empty shot is harmless");
        Check(doorRun.ShootDoor(0, 1.5) && doorRun.LastGrade == TimingGrade.Accurate, "door on-time shot");
        Check(!doorRun.ShootDoor(0, 1.51) && doorRun.JudgmentVersion == 1, "one shot resolves door once");
        doorRun.Press(MoveDirection.Up, 2);
        doorRun.Advance(2.125);
        Check(doorRun.Phase == RunPhase.Cleared, "door followed by computed movement time");
        doorRun.Begin();
        Check(!doorRun.DoorBroken(0) && doorRun.JudgmentVersion == 0, "door state resets");
        doorRun.Advance(1.626);
        Check(doorRun.Failure == FailureReason.MissedDoor, "door expiry without clicking");
        Check(!doorRun.ShootDoor(0, 1.625), "cannot shoot at expired boundary");
        doorRun.Begin();
        doorRun.Press(MoveDirection.Up, 2);
        Check(doorRun.Phase == RunPhase.Dead && doorRun.Failure == FailureReason.DoorCollision,
            "cannot enter room through missed door");
        doorRun.Begin();
        Check(doorRun.ShootDoor(0, 1.5625) && doorRun.LastGrade == TimingGrade.Late, "door late success");
        doorRun.Advance(1.75);
        Check(doorRun.IsActive, "late door input preserved when frame ends after deadline");
        doorRun.Begin();
        Check(doorRun.ShootDoor(0, 1.375) && doorRun.LastGrade == TimingGrade.Early, "door early success boundary");
        Check(doorRun.LastTimingErrorMs == -125, "early door shot has negative timing error");

        var targets = new[] { new AimCandidate(1, 3, 0, 2), new AimCandidate(0, 1, .5, 1) };
        Check(TargetSelection.Select(targets, 1, 0, 45) == 0, "earliest note beats central aim");
        Check(TargetSelection.Select(new[] { new AimCandidate(9, 3, 1, 1), new AimCandidate(0, 1, 0, 2) }, 1, 0, 45) == 9,
            "time priority is independent of ID, distance and enemy/door ordering");
        Check(TargetSelection.Select(new[] { new AimCandidate(9, -1, 0, 1), new AimCandidate(0, 1, 0, 2) }, 1, 0, 45) == 0,
            "earliest note outside the cone is excluded");
        Array.Reverse(targets);
        Check(TargetSelection.Select(targets, 1, 0, 45) == 0, "candidate order does not change selection");
        Check(TargetSelection.Select(targets, -1, 0, 45) == -1, "outside aiming sector");
        Check(TargetSelection.Select(targets, 0, 0, 45) == -1, "zero aiming direction");
        Check(TargetSelection.Select(new[] { new AimCandidate(5, 1, 0), new AimCandidate(2, 3, 0) }, 1, 0, 45) == 2,
            "angle ties use stable note id");
        Check(TargetSelection.Select(Array.Empty<AimCandidate>(), 1, 0, 45) == -1, "empty sector");
        Check(TargetSelection.Select(new[] { new AimCandidate(3, 0, 0, 2), new AimCandidate(4, 2, 0, 3) }, 1, 0, 45) == 3,
            "projectile at player center remains interceptable on its hit beat");
        Check(TargetSelection.Select(new[] { new AimCandidate(3, 0, 2, 2) }, 1, 0, 45) == -1,
            "moving target outside current aim cone is unavailable");
        Check(TargetSelection.Select(new[] { new AimCandidate(3, 2, 0, 2) }, 1, 0, 45) == 3,
            "same moving target becomes selectable when its sampled position enters cone");
        Check(TargetSelection.Select(new[] { new AimCandidate(0, 1, .99) }, 1, 0, 45) == 0, "inside cone edge");
        Check(TargetSelection.Select(new[] { new AimCandidate(0, 1, 1) }, 1, 0, 45) == 0, "inclusive cone edge");
        Check(TargetSelection.Select(new[] { new AimCandidate(0, 1, 1.01) }, 1, 0, 45) == -1, "outside cone edge");

        rejected = false;
        try
        {
            new RoomRun(new[] { new MoveNote { destinationId = "bad", hasDoor = true,
                doorTime = 1, moveDelay = .1, direction = MoveDirection.Up } }, Window, .125);
        }
        catch (ArgumentException) { rejected = true; }
        Check(!rejected, "Overlapping door and movement windows are allowed in judgment order");
        var nearDoor = new RoomRun(new[] { new MoveNote { destinationId = "near", hasDoor = true,
            doorTime = 1, moveDelay = .1, direction = MoveDirection.Up } }, Window, .125);
        nearDoor.Begin();
        Check(nearDoor.ShootDoor(0, 1), "Door can be shot with a nearby movement judgment");
        nearDoor.Press(MoveDirection.Up, 1.1); nearDoor.Advance(1.226);
        Check(nearDoor.Phase == RunPhase.Cleared, "Overlapping door-movement chart clears");
        nearDoor.Begin(); nearDoor.Press(MoveDirection.Up, 1.1);
        Check(nearDoor.Failure == FailureReason.DoorCollision, "A movement cannot bypass an unopened door");
        nearDoor.Begin(); nearDoor.Press(MoveDirection.Up, .1);
        Check(nearDoor.Death == DeathPresentation.Collision, "closed door collision takes precedence over early movement");
        nearDoor.Begin(); nearDoor.Press(MoveDirection.Right, .1);
        Check(nearDoor.Death == DeathPresentation.Collision && nearDoor.DeathDirection == MoveDirection.Right,
            "wrong direction collides with the wall");
        nearDoor.Begin(); nearDoor.Advance(1.126);
        Check(nearDoor.Death == DeathPresentation.Execution, "unshot door expires into execution");
        var earlyExit = NewRun(); earlyExit.Press(MoveDirection.Up, .1);
        Check(earlyExit.Death == DeathPresentation.Departure, "Early movement through the next opening retains departure");
        var independentDoor = new RoomRun(new[] { new MoveNote { destinationId = "north", direction = MoveDirection.Up,
            hasDoor = true, doorTime = 2, moveDelay = 1, customAppearance = true, appearTime = 2.5,
            appearanceTime = 2.5, doorFrameStartTime = 1.95 } }, Window, .125);
        independentDoor.Begin();
        Check(!independentDoor.ShootDoor(0, 1.9), "Even an in-window shot cannot hit a door before its independent appearance");
        Check(independentDoor.ShootDoor(0, 2), "Door can be shot before the destination room appears");
        nearDoor.Reset();
        Check(nearDoor.Death == DeathPresentation.None && nearDoor.FailedEnemy == -1, "reset clears cinematic state");

        var demo = new RoomRun(new[] {
            new MoveNote { destinationId = "north", direction = MoveDirection.Up, time = 2, appearTime = 0 },
            new MoveNote { destinationId = "east", direction = MoveDirection.Right, appearTime = .5,
                hasDoor = true, doorTime = 3.5, moveDelay = .5 },
            new MoveNote { destinationId = "south", direction = MoveDirection.Down, appearTime = 1.5,
                hasDoor = true, doorTime = 5.5, moveDelay = .5 }
        }, new TimingWindow { early = .15, accurate = .05, late = .15 }, .18);
        demo.Begin();
        demo.Press(MoveDirection.Up, 2); demo.Advance(2.2);
        Check(demo.ShootDoor(1, 3.5), "demo right door");
        demo.Press(MoveDirection.Right, 4); demo.Advance(4.2);
        Check(demo.ShootDoor(2, 5.5), "demo down door");
        demo.Press(MoveDirection.Down, 6); demo.Advance(6.2);
        Check(demo.Phase == RunPhase.Cleared && demo.CompletedMoves == 3, "full authored chart clears");

        var oneMove = new[] { Notes[0] };
        var enemyNotes = new[] {
            new EnemyNote { id = "right", roomId = "north", direction = EnemyDirection.Right, time = 3 },
            new EnemyNote { id = "diagonal", roomId = "north", direction = EnemyDirection.UpRight, time = 3.5 }
        };
        var combat = new RoomRun(oneMove, Window, .125, enemyNotes);
        combat.Begin();
        Check(!combat.EnemyAvailable(0) && !combat.ShootEnemy(0, 1), "enemy hidden before entering its room");
        combat.Press(MoveDirection.Up, 2);
        combat.Advance(2.0625);
        Check(combat.EnemyAvailable(0), "destination enemies are targetable during arrival animation");
        combat.Advance(2.125);
        Check(combat.EnemyAvailable(0) && combat.EnemyAvailable(1), "all room enemies appear at arrival");
        Check(combat.RoomArrivedAt == 2.125, "arrival uses event time rather than frame time");
        Check(combat.Phase == RunPhase.Waiting, "last room waits for enemies before clearing");
        Check(!combat.ShootEnemy(0, 2.5) && combat.Phase == RunPhase.Dead, "early enemy shot is fatal");
        Check(combat.Death == DeathPresentation.Execution && combat.FailedEnemy == 0 && combat.DeathTime == 2.5,
            "early enemy shot records the retaliating enemy and time");
        combat.Begin(); combat.Press(MoveDirection.Up, 2); combat.Advance(2.125);

        var enemyCandidates = new[] { new AimCandidate(0, 2.2, 0, 3), new AimCandidate(1, 1.55, 1.55, 3.5) };
        int selected = TargetSelection.Select(enemyCandidates, 1, 1, 45);
        Check(selected == 0 && combat.ShootEnemy(selected, 3) && combat.EnemyDefeated(0),
            "earlier enemy at cone edge is shot before central future enemy");
        Check(combat.IsActive && !combat.EnemyDefeated(1), "auto aim resolves only the earliest enemy");
        combat.Begin(); combat.Press(MoveDirection.Up, 2); combat.Advance(2.125);
        Check(combat.ShootEnemy(0, 3) && combat.LastGrade == TimingGrade.Accurate, "enemy accurate hit");
        Check(!combat.ShootEnemy(0, 3.01), "defeated enemy cannot be hit again");
        Check(combat.NextEnemyIndex() == 1, "pending enemy query skips defeated enemy");
        Check(combat.ShootEnemy(1, 3.5625) && combat.LastGrade == TimingGrade.Late, "enemy late hit");
        Check(combat.LastTimingErrorMs == 62.5, "enemy shot uses its own target time");
        Check(combat.Phase == RunPhase.Cleared, "last enemy clears final room");
        combat.Begin();
        Check(!combat.EnemyDefeated(0) && !combat.EnemyDefeated(1) && combat.RoomArrivedAt == 0,
            "restart clears enemy and arrival state");
        combat.Press(MoveDirection.Up, 2.0625); combat.Advance(2.25);
        Check(combat.RoomArrivedAt == 2.1875, "late movement keeps actual animation duration");
        Check(combat.ShootEnemy(0, 3) && combat.LastGrade == TimingGrade.Accurate,
            "late arrival does not shift enemy judgment time");
        combat.Advance(3.626);
        Check(combat.Failure == FailureReason.MissedEnemy, "missed enemy expires without clicking");
        Check(combat.Death == DeathPresentation.Execution && combat.FailedEnemy == 1 && combat.DeathTime == 3.625,
            "missed enemy execution records the exact deadline");
        Check(!combat.LastTimingErrorMs.HasValue, "timeout does not show a stale successful input error");
        Check(combat.NextEnemyIndex() == 1 && !combat.EnemyDefeated(1),
            "retaliation after death selects the missed enemy, skipping defeated enemies");
        Check(!combat.ShootEnemy(1, 3.625), "enemy cannot be shot at expiry");
        combat.Begin(); combat.Press(MoveDirection.Up, 2); combat.Advance(2.2);
        Check(combat.ShootEnemy(0, 2.875) && combat.LastGrade == TimingGrade.Early, "enemy early boundary");
        combat.Advance(3.2);
        Check(combat.IsActive, "valid earlier enemy shot survives late frame advance");
        combat.Press(MoveDirection.Up, 3.25);
        Check(combat.Failure == FailureReason.WrongDirection, "WASD in final enemy room cannot index absent next room");

        foreach (EnemyDirection direction in Enum.GetValues(typeof(EnemyDirection)))
        {
            var eightWay = new RoomRun(oneMove, Window, .125, new[] {
                new EnemyNote { id = "enemy", roomId = "north", direction = direction, time = 3 }
            });
            eightWay.Begin(); eightWay.Press(MoveDirection.Up, 2); eightWay.Advance(2.2);
            Check(eightWay.ShootEnemy(0, 3) && eightWay.Phase == RunPhase.Cleared, "enemy direction " + direction);
        }
        foreach (double invalidTime in new[] { -1.0, double.NaN, double.PositiveInfinity })
        {
            rejected = false;
            try { new RoomRun(oneMove, Window, .125, new[] {
                new EnemyNote { id = "bad", roomId = "north", direction = EnemyDirection.Up, time = invalidTime }
            }); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "invalid/unreadable enemy timing rejected " + invalidTime);
        }
        rejected = false;
        try { new RoomRun(Notes, Window, .125, new[] {
            new EnemyNote { id = "late", roomId = "north", direction = EnemyDirection.Up, time = 4 }
        }); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "enemy judgment at next movement rejected");
        var overlappingEnemy = new[] {
            new EnemyNote { id = "close", roomId = "north", direction = EnemyDirection.Up, time = 3.9 }
        };
        var closeMovement = new RoomRun(Notes, Window, .125, overlappingEnemy);
        closeMovement.Begin(); closeMovement.Press(MoveDirection.Up, 2); closeMovement.Advance(2.126);
        Check(closeMovement.ShootEnemy(0, 3.9), "Overlapping windows allow an enemy hit at its exact judgment");
        closeMovement.Press(MoveDirection.Right, 4); closeMovement.Advance(4.126);
        Check(closeMovement.Phase == RunPhase.Cleared, "Enemy then movement with overlapping windows clears");
        var skipEnemy = new RoomRun(Notes, Window, .125, overlappingEnemy);
        skipEnemy.Begin(); skipEnemy.Press(MoveDirection.Up, 2); skipEnemy.Advance(2.126);
        skipEnemy.Press(MoveDirection.Right, 3.9);
        Check(skipEnemy.Phase == RunPhase.Dead && skipEnemy.Failure == FailureReason.MissedEnemy,
            "An early move cannot bypass an undefeated enemy in an overlapping window");
        var closeDoorMoves = new[] { Notes[0], new MoveNote { destinationId = "east", direction = MoveDirection.Right,
            hasDoor = true, doorTime = 4, moveDelay = .5, appearTime = 1 } };
        var closeDoor = new RoomRun(closeDoorMoves, Window, .125, overlappingEnemy);
        closeDoor.Begin(); closeDoor.Press(MoveDirection.Up, 2); closeDoor.Advance(2.126);
        Check(closeDoor.ShootEnemy(0, 3.9) && closeDoor.ShootDoor(1, 4), "Enemy then door with overlapping windows is playable");
        closeDoor.Press(MoveDirection.Right, 4.5); closeDoor.Advance(4.626);
        Check(closeDoor.Phase == RunPhase.Cleared, "Enemy-door-movement sequence clears after relaxed validation");
        var movingShot = new RoomRun(new[] {
            new MoveNote { destinationId = "next", direction = MoveDirection.Up, time = 1 }
        }, new TimingWindow { early = .2, late = .2, accurate = .035 }, .6, new[] {
            new EnemyNote { id = "arrival", roomId = "next", direction = EnemyDirection.Up, time = 1.1,
                customAppearance = true, appearanceTime = 0 }
        });
        movingShot.Begin();
        Check(!movingShot.ShootEnemy(0, .99), "previewed destination enemy cannot be shot before movement starts");
        movingShot.Press(MoveDirection.Up, 1);
        Check(movingShot.EnemyAvailable(0) && movingShot.NextEnemyIndex() == 0, "movement immediately activates destination combat");
        Check(movingShot.ShootEnemy(0, 1.1) && movingShot.LastGrade == TimingGrade.Accurate
            && movingShot.LastTimingErrorMs == 0, "exact-beat arrival shot is accepted with a 200ms window");
        Check(movingShot.Phase == RunPhase.Moving && !movingShot.ShootEnemy(0, 1.11), "successful shot neither ends movement nor allows duplicate hits");
        movingShot.Advance(1.31);
        Check(movingShot.Phase == RunPhase.Moving, "accepted arrival shot does not later become a missed enemy");
        movingShot.Advance(1.6);
        Check(movingShot.Phase == RunPhase.Cleared, "arrival completes normally after an in-transit kill");
        movingShot.Begin(); movingShot.Press(MoveDirection.Up, 1);
        Check(movingShot.ShootEnemy(0, 1.1 + .2), "late boundary remains inclusive during movement");
        movingShot.Begin(); movingShot.Press(MoveDirection.Up, 1); movingShot.Advance(1.301);
        Check(movingShot.Failure == FailureReason.MissedEnemy && movingShot.FailedEnemy == 0
            && movingShot.Death == DeathPresentation.Execution,
            "unshot destination enemy expires during movement without extending the deadline");
        var fastEnemy = new RoomRun(oneMove, Window, .125, new[] {
            new EnemyNote { id = "fast", roomId = "north", direction = EnemyDirection.Up, time = 2.3,
                customAppearance = true, appearanceTime = 0 }
        });
        fastEnemy.Begin(); fastEnemy.Press(MoveDirection.Up, 2); fastEnemy.Advance(2.126);
        Check(fastEnemy.ShootEnemy(0, 2.3), "An enemy previewed before entry needs no post-arrival reading delay");
        var repeatedDirection = new RoomRun(oneMove, Window, .125, new[] {
            new EnemyNote { id = "repeat-a", roomId = "north", direction = EnemyDirection.Up, time = 3 },
            new EnemyNote { id = "repeat-b", roomId = "north", direction = EnemyDirection.Up, time = 4 }
        });
        repeatedDirection.Begin(); repeatedDirection.Press(MoveDirection.Up, 2); repeatedDirection.Advance(2.126);
        Check(repeatedDirection.ShootEnemy(0, 3) && repeatedDirection.ShootEnemy(1, 4), "Separate enemies may reuse a direction");

        var sceneMoves = new[] {
            new MoveNote { destinationId = "north", direction = MoveDirection.Up, time = 2 },
            new MoveNote { destinationId = "east", direction = MoveDirection.Right, appearTime = .5,
                hasDoor = true, doorTime = 3.5, moveDelay = .5 },
            new MoveNote { destinationId = "south", direction = MoveDirection.Down, appearTime = 1.5,
                hasDoor = true, doorTime = 5.5, moveDelay = .5 }
        };
        var sceneEnemies = new[] {
            new EnemyNote { id = "n", roomId = "north", direction = EnemyDirection.UpLeft, time = 3 },
            new EnemyNote { id = "e", roomId = "east", direction = EnemyDirection.UpRight, time = 5 },
            new EnemyNote { id = "s1", roomId = "south", direction = EnemyDirection.Right, time = 7 },
            new EnemyNote { id = "s2", roomId = "south", direction = EnemyDirection.UpRight, time = 7.5 },
            new EnemyNote { id = "s3", roomId = "south", direction = EnemyDirection.DownLeft, time = 8 }
        };
        combat = new RoomRun(sceneMoves, new TimingWindow { early = .15, accurate = .05, late = .15 }, .18, sceneEnemies);
        combat.Begin(); combat.Press(MoveDirection.Up, 2);
        Check(combat.ShootEnemy(0, 3) && combat.ShootDoor(1, 3.5), "north enemy then door");
        combat.Press(MoveDirection.Right, 4);
        Check(combat.ShootEnemy(1, 5) && combat.ShootDoor(2, 5.5), "east enemy then door");
        combat.Press(MoveDirection.Down, 6);
        Check(combat.ShootEnemy(2, 7) && combat.ShootEnemy(3, 7.5) && combat.ShootEnemy(4, 8)
            && combat.Phase == RunPhase.Cleared, "full scene enemy-door-movement chart clears");
        Console.WriteLine($"PASS: {checks} movement timing/state checks.");
        BeatChartChecks.Run();
        MapChartChecks.Run();
        MafiaAmbushChecks.Run();
        OffsetCalibrationChecks.Run();
    }
}
