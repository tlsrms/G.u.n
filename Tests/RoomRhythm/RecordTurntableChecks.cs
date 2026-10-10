using System;
using Gun.RoomRhythm;

internal static class RecordTurntableChecks
{
    private static int checks;
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception("FAIL: " + message); checks++; }

    public static void Run()
    {
        Check(RecordSelectionGeometry.DragDelta(179, -179) == 2, "record drag crosses the angle seam without a full turn");
        Check(RecordSelectionGeometry.DragDelta(-179, 179) == -2, "reverse drag crosses the angle seam without a jump");
        foreach (int count in new[] { 1, 5, 6, 8 })
        for (int index = 0; index < count; index++)
        foreach (double turns in new[] { -720.0, 0, 360, 1080 })
        {
            const double contactAngle = -28, radius = 268;
            double radians = contactAngle * Math.PI / 180;
            double rotation = contactAngle - RecordSelectionGeometry.CenterAngle(index, count) + turns;
            Check(RecordSelectionGeometry.SectorAtRotation(Math.Cos(radians) * radius, Math.Sin(radians) * radius,
                rotation, count, 110, 310) == index, "the fixed needle selects the rotated sector under it");
        }

        var state = new RecordTurntableState();
        state.Reset(); state.Tick(10);
        Check(state.Rotation == 0 && state.CanRotate, "selection record is stationary until dragged");
        state.Rotate(73); state.Tick(1);
        Check(state.Rotation == 73, "selection retains the manually rotated angle");
        Check(state.ToggleArm(), "first tonearm click engages playback");
        state.Rotate(100);
        Check(state.Rotation == 73 && !state.CanRotate, "record drag is locked while the needle lowers");
        Check(!state.Tick(.1) && state.ArmProgress > 0 && state.ArmProgress < 1, "needle moves before playback is requested");
        Check(state.Tick(.2) && state.Phase == RecordPlaybackPhase.Loading, "needle landing requests audio exactly once");
        Check(!state.Tick(1) && state.Rotation == 73 && state.ArmProgress == 1, "waiting for audio cannot move the needle or spin the record");
        state.AudioReady(); state.Tick(1);
        Check(state.IsPlaying && state.Rotation == 37, "record starts spinning when audio is ready");
        Check(!state.ToggleArm(), "second tonearm click stops playback");
        state.AudioReady(); state.Tick(.3);
        Check(state.CanRotate && state.ArmProgress == 0 && state.Rotation == 37,
            "parked arm restores selection without losing the final record angle");
        state.ToggleArm(); state.Tick(.1); state.ToggleArm();
        state.AudioReady();
        Check(!state.Tick(1) && state.CanRotate && !state.IsPlaying, "rapid re-click cancels an in-flight playback request");
        state.ToggleArm(); state.Tick(1); state.Stop(); state.AudioReady(); state.Tick(1);
        Check(state.CanRotate && !state.IsPlaying, "late audio completion cannot restart a stopped record");
        state.Reset();
        Check(state.ArmProgress == 0 && state.Rotation == 0 && state.CanRotate, "re-entry reset returns to the parked selection state");
        Console.WriteLine($"PASS: {checks} rotating record selection and tonearm playback checks.");
    }
}
