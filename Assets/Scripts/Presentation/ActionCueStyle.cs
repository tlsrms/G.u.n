using UnityEngine;

namespace Gun.RoomRhythm
{
    public enum ActionCueKind { Move, Shot }

    public static class ActionCueStyle
    {
        // Visual thickness only; authored width still controls approach motion.
        public static float ShotLineWidth(float width) => width * 1.08f;

        public static Color Accent(ActionCueKind kind) => kind == ActionCueKind.Move
            ? new Color(.3f, 1f, .8f, 1f) : new Color(1f, .16f, .24f, 1f);

        public static Color Timing(ActionCueKind kind, int priority, double time, double target, TimingWindow window)
        {
            if (priority > 0)
            {
                // The first preview is less than half as bright as the active white cue.
                float gray = .06f + .38f / (1f + .65f * (priority - 1));
                return new Color(gray, gray, gray, 1f);
            }
            TimingGrade grade = window.Judge(time, target);
            return grade == TimingGrade.Early || grade == TimingGrade.Accurate || grade == TimingGrade.Late
                ? Accent(kind) : Color.white;
        }

        public static void ApplyShotRings(LineRenderer outline, LineRenderer timingRing, int priority,
            double time, double target, TimingWindow window, float width)
        {
            // The held timing ring must remain visible when both centerlines coincide.
            timingRing.sortingLayerID = outline.sortingLayerID;
            timingRing.sortingOrder = outline.sortingOrder + 1;
            outline.startColor = outline.endColor = Color.white;
            timingRing.startColor = timingRing.endColor = Timing(ActionCueKind.Shot, priority, time, target, window);
            outline.startWidth = outline.endWidth = timingRing.startWidth = timingRing.endWidth = ShotLineWidth(width);
        }
    }
}
