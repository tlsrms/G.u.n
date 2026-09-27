using System;
using System.Collections.Generic;

namespace Gun.RoomRhythm
{
    public readonly struct AimCandidate
    {
        public readonly int Id;
        public readonly double X, Y, HitTime;
        public AimCandidate(int id, double x, double y, double hitTime = 0) { Id = id; X = x; Y = y; HitTime = hitTime; }
    }

    public static class TargetSelection
    {
        // The cone limits eligibility; the earliest note wins regardless of its angle within the cone.
        public static int Select(IReadOnlyList<AimCandidate> candidates, double aimX, double aimY, double halfAngle)
        {
            double aimLength = Math.Sqrt(aimX * aimX + aimY * aimY);
            if (aimLength <= 0) return -1;
            double minimum = Math.Cos(halfAngle * Math.PI / 180);
            double best = double.PositiveInfinity;
            int selected = -1;
            foreach (AimCandidate candidate in candidates)
            {
                double length = Math.Sqrt(candidate.X * candidate.X + candidate.Y * candidate.Y);
                if (length <= 0) continue;
                double dot = (candidate.X * aimX + candidate.Y * aimY) / (length * aimLength);
                if (dot + 1e-10 < minimum) continue;
                if (candidate.HitTime < best || (candidate.HitTime == best && (selected < 0 || candidate.Id < selected)))
                { best = candidate.HitTime; selected = candidate.Id; }
            }
            return selected;
        }
    }
}
