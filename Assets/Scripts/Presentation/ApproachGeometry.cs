using System;

namespace Gun.RoomRhythm
{
    // Geometry reaches a small visible gap at Early, the reference at the beat,
    // then holds through Late. Color and gameplay share the unmodified TimingWindow.
    public static class ApproachGeometry
    {
        // Equal-width strokes touch at one width of centerline separation.
        // Leave a small gap, including the shot rings' 8% visual thickness boost.
        public static double EntryDistance(double width) => width * 1.2;

        public static double FixedStartRadius(double time, double start, double target,
            double startRadius, double targetRadius, TimingWindow window, double width)
        {
            double direction = Math.Sign(startRadius - targetRadius);
            double gap = Math.Min(EntryDistance(width), Math.Abs(startRadius - targetRadius));
            double opens = target - window.early;
            if (time < opens)
            {
                if (opens <= start) return startRadius;
                double progress = Math.Max(0, (time - start) / (opens - start));
                double entryRadius = targetRadius + direction * gap;
                return Math.Max(0, startRadius + (entryRadius - startRadius) * progress);
            }
            if (time < target)
                return targetRadius + direction * gap * ((target - time) / window.early);
            if (time <= target + window.late) return targetRadius;
            double duration = Math.Max(window.early, opens - start);
            return Math.Max(0, targetRadius - direction * Math.Abs(startRadius - targetRadius)
                * (time - (target + window.late)) / duration);
        }

        public static double ExpandingRadius(double time, double target, double radius, double width, TimingWindow window)
            => Math.Max(0, radius - ApproachOffset(time, target, width, window));

        public static double Speed(double width, TimingWindow window) => width / window.Symmetric.early;

        public static double Radius(double time, double target, double radius, double width, TimingWindow window)
            => Math.Max(0, radius + ApproachOffset(time, target, width, window));

        private static double ApproachOffset(double time, double target, double width, TimingWindow window)
        {
            double opens = target - window.early;
            double gap = EntryDistance(width);
            if (time < opens) return gap + (opens - time) * Speed(width, window);
            if (time < target) return gap * ((target - time) / window.early);
            if (time <= target + window.late) return 0;
            return -(time - (target + window.late)) * Speed(width, window);
        }
    }
}
