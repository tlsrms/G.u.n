using System;
using Gun.RoomRhythm;

internal static class OffsetCalibrationChecks
{
    private static int count;
    private static void Check(bool value, string message) { count++; if (!value) throw new Exception("Calibration: " + message); }

    public static void Run()
    {
        foreach (double offset in new[] { -.08, 0, .065 })
        {
            var model = new OffsetCalibration(120, 4, 0);
            Check(!model.Tap(1), "warm-up is ignored");
            for (int beat = 5; !model.Finished && beat < 60; beat++) model.Tap(beat * .5 + offset);
            Check(model.Finished && model.Reliable && model.SampleCount == 24, "stable data stops at minimum");
            Check(Math.Abs(model.RecommendedMs - offset * 1000) < .0001, "positive/negative recommendation uses raw latency");
            Check(!model.Tap(40) && model.SampleCount == 24, "completed measurement ignores later taps");
        }
        var loop = new OffsetCalibration(120, 2.3, .1);
        for (int cycle = 2; !loop.Finished && cycle < 20; cycle++)
            for (int beat = 0; beat < 5; beat++) loop.Tap(cycle * 2.3 + .1 + beat * .5 - .08);
        Check(loop.Reliable && Math.Abs(loop.RecommendedMs + 80) < .0001, "non-grid loop length and early input across loop boundary");
        var outliers = new OffsetCalibration(120, 4, 0);
        for (int beat = 5; !outliers.Finished && beat < 80; beat++)
            outliers.Tap(beat * .5 + (beat % 10 == 0 ? .19 : .06 + (beat % 3 - 1) * .002));
        Check(outliers.Reliable && Math.Abs(outliers.RecommendedMs - 60) <= 2, "robust estimate excludes isolated outliers");
        Check(outliers.InlierCount < outliers.SampleCount, "outliers reported separately");
        var unstable = new OffsetCalibration(120, 4, 0);
        for (int beat = 5; beat < 100 && !unstable.Finished; beat++) unstable.Tap(beat * .5 + (beat % 2 == 0 ? .12 : -.12));
        Check(unstable.Finished && !unstable.Reliable && unstable.SampleCount == 48, "unstable data stops at maximum without reliable result");
        var duplicate = new OffsetCalibration(120, 4, 0);
        duplicate.Tap(3.04); duplicate.Tap(3.05);
        Check(duplicate.SampleCount == 1, "same beat cannot be counted twice");
        for (int i = 0; i < 100 && !duplicate.Finished; i++) duplicate.Tap(3.06);
        Check(duplicate.Finished && duplicate.Attempts == 64 && !duplicate.Reliable, "tap spam is bounded");
        var timeout = new OffsetCalibration(120, 4, 0);
        timeout.Tick(timeout.Deadline);
        Check(timeout.Finished && !timeout.Reliable && timeout.SampleCount == 0, "silence times out without recommendation");
        var drift = new OffsetCalibration(120, 4, 0);
        for (int beat = 5; beat < 29; beat++) drift.Tap(beat * .5 + (beat - 5) * .002);
        Check(!drift.Finished && !drift.Reliable, "systematic drift does not look like stable latency");
        bool rejected = false;
        try { new OffsetCalibration(0, 4, 0); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "invalid beat configuration rejected");
        Console.WriteLine($"PASS: {count} offset calibration checks.");
    }
}
