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
            if (Math.Abs(wall.x - line.x) < (wall.w + line.w) * .5 - 1e-10
                && Math.Abs(wall.y - line.y) < (wall.h + line.h) * .5 - 1e-10) return true;
        return false;
    }

    public static void Main(string[] args)
    {
        // An event can arrive in the next input batch with a timestamp before the previous
        // MonoBehaviour.Update clock read. Advancing beyond the input watermark loses it.
        var prematurelyAdvanced = NewRun();
        prematurelyAdvanced.Advance(2.11);
        prematurelyAdvanced.Press(MoveDirection.Up, 2.10);
        prematurelyAdvanced.Advance(2.125);
        Check(prematurelyAdvanced.Failure == FailureReason.TooLate,
            "reproduces valid event discarded after advancing past input batch");
        var batched = NewRun();
        batched.Advance(2.08); // Only commit the last completed input batch.
        batched.Press(MoveDirection.Up, 2.10); // Arrives next frame at real time 2.14.
        batched.Advance(2.14);
        Check(batched.Phase == RunPhase.Moving && batched.LastGrade == TimingGrade.Late,
            "valid event survives next-frame delivery when clock respects input watermark");
        batched = NewRun(); batched.Advance(2.08); batched.Press(MoveDirection.Up, 2.125);
        Check(batched.Failure == FailureReason.TooLate, "batch ordering does not extend the success deadline");
        batched = NewRun(); batched.Advance(2.08); batched.Advance(2.14);
        Check(batched.Failure == FailureReason.TooLate, "missing input expires when input batch reaches deadline");
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
                && timing.Judge(2 + timing.late, 2) == TimingGrade.TooLate, "inner separation and death have identical deadline");
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
        Check(shortLeadRejected, "enemy lead shorter than required reading time is rejected");
        Check(Window.Judge(0, 2) == TimingGrade.TooEarly, "arbitrarily early input is fatal");
        Check(Window.Judge(1.875, 2) == TimingGrade.Early, "early success inclusive boundary");
        Check(Window.Judge(1.96875, 2) == TimingGrade.Accurate, "accurate starts inclusively");
        Check(Window.Judge(2.03125, 2) == TimingGrade.Accurate, "accurate ends inclusively");
        Check(Window.Judge(2.0625, 2) == TimingGrade.Late, "late success");
        Check(Window.Judge(2.125, 2) == TimingGrade.TooLate, "late expiry boundary");

        var run = NewRun();
        run.Press(MoveDirection.Up, -0.1);
        Check(run.Phase == RunPhase.Dead && run.Failure == FailureReason.TooEarly, "input during scheduled audio lead-in is not ignored");
        run = NewRun();
        run.Press(MoveDirection.Left, 2);
        Check(run.Failure == FailureReason.WrongDirection, "wrong direction");
        run = NewRun();
        run.Advance(2.125);
        Check(run.Phase == RunPhase.Dead, "no input expires automatically");

        run = NewRun();
        run.Press(MoveDirection.Up, 2.0625);
        Check(run.LastGrade == TimingGrade.Late && run.Phase == RunPhase.Moving, "late input accepted");
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
        run.Advance(4.125);
        Check(run.Phase == RunPhase.Dead, "second note still requires a fresh press");

        run.Reset();
        Check(run.Phase == RunPhase.Ready && run.CompletedMoves == 0
            && run.LastGrade == TimingGrade.None && run.Failure == FailureReason.None, "reset clears all result state");
        run.Press(MoveDirection.Up, 2);
        Check(run.Phase == RunPhase.Ready, "ready state does not process movement");
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
            new RoomRun(new[] { Notes[0], new MoveNote { destinationId = "east", direction = MoveDirection.Right, time = 2.2 } }, Window, .125);
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
        Check(!doorRun.ShootDoor(0, .5) && doorRun.IsActive, "early shot is harmless");
        Check(!doorRun.ShootDoor(-1, 1) && doorRun.IsActive, "empty shot is harmless");
        Check(doorRun.ShootDoor(0, 1.5) && doorRun.LastGrade == TimingGrade.Accurate, "door on-time shot");
        Check(!doorRun.ShootDoor(0, 1.51) && doorRun.JudgmentVersion == 1, "one shot resolves door once");
        doorRun.Press(MoveDirection.Up, 2);
        doorRun.Advance(2.125);
        Check(doorRun.Phase == RunPhase.Cleared, "door followed by computed movement time");
        doorRun.Begin();
        Check(!doorRun.DoorBroken(0) && doorRun.JudgmentVersion == 0, "door state resets");
        doorRun.Advance(1.625);
        Check(doorRun.Failure == FailureReason.MissedDoor, "door expiry without clicking");
        Check(!doorRun.ShootDoor(0, 1.625), "cannot shoot at expired boundary");
        doorRun.Begin();
        doorRun.Press(MoveDirection.Up, 2);
        Check(doorRun.Phase == RunPhase.Dead && doorRun.Failure == FailureReason.MissedDoor,
            "cannot enter room through missed door");
        doorRun.Begin();
        Check(doorRun.ShootDoor(0, 1.5625) && doorRun.LastGrade == TimingGrade.Late, "door late success");
        doorRun.Advance(1.75);
        Check(doorRun.IsActive, "late door input preserved when frame ends after deadline");
        doorRun.Begin();
        Check(doorRun.ShootDoor(0, 1.375) && doorRun.LastGrade == TimingGrade.Early, "door early success boundary");

        // The selector must not know whether the closest-to-centre target is currently hittable.
        var targets = new[] { new AimCandidate(1, 3, 0), new AimCandidate(0, 1, .5) };
        Check(TargetSelection.Select(targets, 1, 0, 45) == 1, "angle beats distance and note order");
        Check(TargetSelection.Select(targets, -1, 0, 45) == -1, "outside aiming sector");
        Check(TargetSelection.Select(targets, 0, 0, 45) == -1, "zero aiming direction");
        Check(TargetSelection.Select(new[] { new AimCandidate(5, 1, 0), new AimCandidate(2, 3, 0) }, 1, 0, 45) == 2,
            "angle ties use stable note id");
        Check(TargetSelection.Select(Array.Empty<AimCandidate>(), 1, 0, 45) == -1, "empty sector");
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
        Check(rejected, "overlapping door and movement windows rejected");

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
        Check(!combat.ShootEnemy(0, 2.0625), "cannot shoot enemy during arrival animation");
        combat.Advance(2.125);
        Check(combat.EnemyAvailable(0) && combat.EnemyAvailable(1), "all room enemies appear at arrival");
        Check(combat.RoomArrivedAt == 2.125, "arrival uses event time rather than frame time");
        Check(combat.Phase == RunPhase.Waiting, "last room waits for enemies before clearing");
        Check(!combat.ShootEnemy(0, 2.5) && combat.IsActive, "early enemy shot has no penalty");

        var enemyCandidates = new[] { new AimCandidate(0, 2.2, 0), new AimCandidate(1, 1.55, 1.55) };
        int selected = TargetSelection.Select(enemyCandidates, 1, 1, 45);
        Check(selected == 1 && !combat.ShootEnemy(selected, 3) && !combat.EnemyDefeated(0),
            "central future enemy does not redirect hit to valid adjacent enemy");
        Check(combat.ShootEnemy(0, 3) && combat.LastGrade == TimingGrade.Accurate, "enemy accurate hit");
        Check(!combat.ShootEnemy(0, 3.01), "defeated enemy cannot be hit again");
        Check(combat.NextEnemyIndex() == 1, "pending enemy query skips defeated enemy");
        Check(combat.ShootEnemy(1, 3.5625) && combat.LastGrade == TimingGrade.Late, "enemy late hit");
        Check(combat.Phase == RunPhase.Cleared, "last enemy clears final room");
        combat.Begin();
        Check(!combat.EnemyDefeated(0) && !combat.EnemyDefeated(1) && combat.RoomArrivedAt == 0,
            "restart clears enemy and arrival state");
        combat.Press(MoveDirection.Up, 2.0625); combat.Advance(2.25);
        Check(combat.RoomArrivedAt == 2.1875, "late movement keeps actual animation duration");
        Check(combat.ShootEnemy(0, 3) && combat.LastGrade == TimingGrade.Accurate,
            "late arrival does not shift enemy judgment time");
        combat.Advance(3.625);
        Check(combat.Failure == FailureReason.MissedEnemy, "missed enemy expires without clicking");
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
        foreach (double invalidTime in new[] { 2.3, double.NaN, double.PositiveInfinity })
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
        Check(rejected, "enemy overlapping next movement rejected");

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
        AuthoredChartChecks.Run(args[0]);
        BeatChartChecks.Run();
        MapChartChecks.Run();
    }
}
