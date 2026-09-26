using UnityEditor;

namespace Gun.RoomRhythm.Editor
{
    // Keep old menu/bookmarks working while routing all authoring through the visual editor.
    public static class BeatChartWindow
    {
        [MenuItem("Window/Gun/박자 채보 편집기")]
        public static void Open() => MapChartWindow.Open();
        public static void Open(RoomChart chart) => MapChartWindow.Open(chart);
    }
}