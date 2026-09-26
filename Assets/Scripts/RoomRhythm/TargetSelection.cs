using System;
using System.Collections.Generic;

namespace Gun.RoomRhythm
{
    public readonly struct AimCandidate
    {
        public readonly int Id;
        public readonly double X, Y;
        public AimCandidate(int id, double x, double y) { Id = id; X = x; Y = y; }
    }

    public static class TargetSelection
    {
        // No timing filter here: the aimed-at target is chosen before its time is judged.
        public static int Select(IReadOnlyList<AimCandidate> candidates, double aimX, double aimY, double halfAngle)
        {
            double aimLength = Math.Sqrt(aimX * aimX + aimY * aimY);
            if (aimLength <= 0) return -1;
            double minimum = Math.Cos(halfAngle * Math.PI / 180);
            double best = double.NegativeInfinity;
            int selected = -1;
            foreach (AimCandidate candidate in candidates)
            {
                double length = Math.Sqrt(candidate.X * candidate.X + candidate.Y * candidate.Y);
                if (length <= 0) continue;
                double dot = (candidate.X * aimX + candidate.Y * aimY) / (length * aimLength);
                if (dot + 1e-10 < minimum) continue;
                if (dot > best + 1e-10 || (Math.Abs(dot - best) <= 1e-10 && (selected < 0 || candidate.Id < selected)))
                { best = dot; selected = candidate.Id; }
            }
            return selected;
        }
    }
}
