using System;
using Gun.RoomRhythm;

// Substitute the Unity asset container to exercise offset storage without the editor.
namespace Gun.RoomRhythm
{
    public sealed class RoomChart
    {
        public double inputOffsetMs;
        public int Revision { get; private set; }
        public void NotifyChartChanged() => Revision++;
    }
}

internal static class SongOffsetChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void Main()
    {
        var first = new RoomChart();
        var second = new RoomChart();
        InputOffsetSettings.Save(first, 185);
        Check(InputOffsetSettings.Milliseconds(first) == 185, "First song retains its offset.");
        Check(InputOffsetSettings.Milliseconds(second) == 0, "An unconfigured song starts at zero.");
        InputOffsetSettings.Save(second, -42.5);
        Check(InputOffsetSettings.Milliseconds(first) == 185, "Saving another song leaves the first unchanged.");
        Check(InputOffsetSettings.Milliseconds(second) == -42.5, "Negative offsets remain song-specific.");
        Check(first.Revision == 1 && second.Revision == 1, "Only the changed charts are notified.");
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, -1001, 1001 })
        {
            bool rejected = false;
            try { InputOffsetSettings.Save(first, invalid); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected && first.inputOffsetMs == 185 && first.Revision == 1, "Invalid offsets cannot overwrite a song.");
        }
        Console.WriteLine("PASS: song-specific offset isolation and validation.");
    }
}
