using System;

namespace Gun.RoomRhythm
{
    // Clockwise sectors in record-local coordinates, starting at twelve o'clock.
    public static class RecordSelectionGeometry
    {
        public static int SectorAt(double x, double y, int count, double innerRadius, double outerRadius)
        {
            if (count <= 0 || double.IsNaN(x) || double.IsNaN(y)) return -1;
            double radiusSquared = x * x + y * y;
            if (radiusSquared < innerRadius * innerRadius || radiusSquared > outerRadius * outerRadius) return -1;
            double clockwise = (90 - Math.Atan2(y, x) * 180 / Math.PI + 360) % 360;
            return Math.Min(count - 1, (int)(clockwise / (360.0 / count)));
        }

        public static double CenterAngle(int index, int count) => 90 - (index + .5) * 360 / count;

        public static int SectorAtRotation(double x, double y, double rotation, int count, double innerRadius, double outerRadius)
        {
            double radians = -rotation * Math.PI / 180;
            return SectorAt(x * Math.Cos(radians) - y * Math.Sin(radians),
                x * Math.Sin(radians) + y * Math.Cos(radians), count, innerRadius, outerRadius);
        }
        public static double DragDelta(double previous, double current) => ((current - previous) % 360 + 540) % 360 - 180;
    }
}
