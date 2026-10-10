using System;

namespace Gun.RoomRhythm
{
    public enum RecordPlaybackPhase { Selecting, LoweringArm, Loading, Playing, RaisingArm }

    // Owns the mechanical sequence; audio readiness is separate from the arm movement.
    public sealed class RecordTurntableState
    {
        public RecordPlaybackPhase Phase { get; private set; }
        public double Rotation { get; private set; }
        public double ArmProgress { get; private set; }
        public bool CanRotate => Phase == RecordPlaybackPhase.Selecting;
        public bool IsPlaying => Phase == RecordPlaybackPhase.Playing;

        public void Reset() { Phase = RecordPlaybackPhase.Selecting; Rotation = ArmProgress = 0; }
        public void Rotate(double degrees)
        {
            if (CanRotate && !double.IsNaN(degrees) && !double.IsInfinity(degrees)) Rotation = Wrap(Rotation + degrees);
        }
        public bool ToggleArm()
        {
            if (CanRotate) { Phase = RecordPlaybackPhase.LoweringArm; return true; }
            Stop(); return false;
        }
        public void Stop() => Phase = RecordPlaybackPhase.RaisingArm;
        public void AudioReady() { if (Phase == RecordPlaybackPhase.Loading) Phase = RecordPlaybackPhase.Playing; }
        // True exactly once when the needle lands: this is when audio should be requested.
        public bool Tick(double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return false;
            if (Phase == RecordPlaybackPhase.LoweringArm)
            {
                ArmProgress = Math.Min(1, ArmProgress + seconds / .28);
                if (ArmProgress == 1) { Phase = RecordPlaybackPhase.Loading; return true; }
            }
            else if (Phase == RecordPlaybackPhase.RaisingArm)
            {
                ArmProgress = Math.Max(0, ArmProgress - seconds / .28);
                if (ArmProgress == 0) Phase = RecordPlaybackPhase.Selecting;
            }
            else if (IsPlaying) Rotation = Wrap(Rotation - 36 * seconds);
            return false;
        }
        private static double Wrap(double angle) => (angle % 360 + 360) % 360;
    }
}
