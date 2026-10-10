using UnityEngine;

namespace Gun.RoomRhythm
{
    internal static class StageSelection
    {
        internal static int LastIndex { get; set; }
        internal static bool IsRecordRun { get; set; }
        internal static bool DebugMode { get; set; }
        internal static float MusicVolume { get; set; } = 1;
        internal static string ReturnScene { get; set; } = "StageSelectScene";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            SafeRoomTransit.Reset();
            LastIndex = 0;
            IsRecordRun = false;
            DebugMode = false;
            MusicVolume = 1;
            ReturnScene = "StageSelectScene";
        }
    }
}
