using UnityEngine;

namespace Gun.RoomRhythm
{
    public static class ActionCueStyle
    {
        // Pending shots (enemies and doors) share red -> orange -> yellow order.
        public static Color Shot(int priority) => priority == 0 ? new Color(1f, .16f, .24f, 1f)
            : priority == 1 ? new Color(1f, .5f, .08f, 1f) : new Color(1f, .9f, .15f, 1f);

        public static void ApplyShotRings(LineRenderer outline, LineRenderer timingRing, int priority, float width)
        {
            outline.startColor = outline.endColor = Color.white;
            timingRing.startColor = timingRing.endColor = Shot(priority);
            outline.startWidth = outline.endWidth = timingRing.startWidth = timingRing.endWidth = width;
        }
    }
}
