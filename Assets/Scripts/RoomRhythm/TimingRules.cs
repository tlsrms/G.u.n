using System;

namespace Gun.RoomRhythm
{
    public static class JudgmentSettings
    {
        public const double DefaultAccurateMs = 25;
        public const double DefaultToleranceMs = 35;
        public static double AccurateMs { get; private set; } = DefaultAccurateMs;
        public static double ToleranceMs { get; private set; } = DefaultToleranceMs;
        public static int Revision { get; private set; }
        public static void Configure(double accurateMs, double toleranceMs)
        {
            if (double.IsNaN(accurateMs) || double.IsInfinity(accurateMs) || accurateMs < 0
                || double.IsNaN(toleranceMs) || double.IsInfinity(toleranceMs) || toleranceMs <= accurateMs)
                throw new ArgumentException("Accurate must be >= 0 and tolerance must be greater, in finite milliseconds.");
            if (AccurateMs == accurateMs && ToleranceMs == toleranceMs) return;
            AccurateMs = accurateMs; ToleranceMs = toleranceMs; unchecked { Revision++; }
        }
        public static TimingWindow Window => new TimingWindow {
            early = ToleranceMs / 1000, late = ToleranceMs / 1000, accurate = AccurateMs / 1000
        };
    }
    // Equal-width strokes touch when their centerlines are one stroke width apart.
    // This maps the SAME timing window used by Judge to the visible overlap.
    public static class ApproachGeometry
    {
        // Each room travels the same distance over its own authored lead time.
        // Continue past the target so a missed note does not look like a held success.
        public static double FixedStartRadius(double time, double start, double target, double startRadius, double targetRadius)
        {
            if (target <= start) return time < target ? startRadius : targetRadius;
            double progress = Math.Max(0, (time - start) / (target - start));
            return Math.Max(0, startRadius + (targetRadius - startRadius) * progress);
        }

        public static double ExpandingRadius(double time, double target, double radius, double width, TimingWindow window)
            => Math.Max(0, radius + (time - target) * Speed(width, window));

        public static double Speed(double width, TimingWindow window) => width / window.Symmetric.early;

        public static double Radius(double time, double target, double radius, double width, TimingWindow window)
        {
            // No separate anticipation phase: spawn size follows time remaining at the same speed.
            // Keep moving past separation. Clamping at radius-width leaves the two strokes
            // touching forever on the failure screen, falsely suggesting a valid overlap.
            return Math.Max(0, radius + (target - time) * Speed(width, window));
        }
    }

    public enum MoveDirection { Up, Left, Down, Right }
    public enum MovementEase { Smooth, Linear, EaseIn, EaseOut }
    public static class MovementProfile
    {
        public static double Evaluate(double progress, MovementEase ease)
        {
            double t = Math.Max(0, Math.Min(1, progress));
            switch (ease)
            {
                case MovementEase.Linear: return t;
                case MovementEase.EaseIn: return t * t;
                case MovementEase.EaseOut: return 1 - (1 - t) * (1 - t);
                default: return t * t * (3 - 2 * t);
            }
        }
    }
    public enum TimingGrade { None, TooEarly, Early, Accurate, Late, TooLate }
    public enum EnemyDirection { Up, UpRight, Right, DownRight, Down, DownLeft, Left, UpLeft }

    [Serializable]
    public struct EnemyNote
    {
        public string id;
        public string roomId;
        public EnemyDirection direction;
        public double time;
        public bool customAppearance;
        public double appearanceTime;
        public double frameStartTime;
    }

    [Serializable]
    public struct TimingWindow
    {
        public double early;
        public double accurate;
        public double late;
        public TimingWindow Symmetric => new TimingWindow
        {
            early = (early + late) * 0.5,
            accurate = accurate,
            late = (early + late) * 0.5
        };

        public bool IsValid => early > 0 && late > 0 && accurate >= 0
            && accurate < early && accurate < late
            && !double.IsInfinity(early) && !double.IsInfinity(late);

        public TimingGrade Judge(double input, double target)
        {
            if (input < target - early) return TimingGrade.TooEarly;
            if (input > target + late) return TimingGrade.TooLate;
            if (input < target - accurate) return TimingGrade.Early;
            return input <= target + accurate ? TimingGrade.Accurate : TimingGrade.Late;
        }
    }

    [Serializable]
    public struct MoveNote
    {
        // Zero preserves the chart-wide duration in older assets.
        public double duration;
        public MovementEase ease;
        public double Duration(double fallback) => duration == 0 ? fallback : duration;
        public string destinationId;
        public MoveDirection direction;
        public double time;
        // Derived from the chart's room lead time; not a second authoring control.
        [NonSerialized] public double appearTime;
        public bool customAppearance;
        public double appearanceTime;
        public double frameStartTime;
        public double doorFrameStartTime;
        public bool hasDoor;
        public double doorTime;
        public double moveDelay;
        public double HitTime => hasDoor ? doorTime + moveDelay : time;
        public double DoorAppearsAt => customAppearance ? doorFrameStartTime : appearTime;
    }
}
