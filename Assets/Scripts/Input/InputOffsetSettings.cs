using System;

namespace Gun.RoomRhythm
{
    public static class InputOffsetSettings
    {
        public static double Milliseconds(RoomChart chart) => chart != null ? chart.inputOffsetMs : 0;
        public static void Save(RoomChart chart, double milliseconds)
        {
            if (chart == null) throw new ArgumentNullException(nameof(chart));
            if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds) || Math.Abs(milliseconds) > 1000)
                throw new ArgumentException("Input offset must be within +/-1000 ms.");
            chart.inputOffsetMs = milliseconds;
            chart.NotifyChartChanged();
        }
    }
}
