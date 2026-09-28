using UnityEngine;

namespace Gun.RoomRhythm
{
    internal static class StageSelection
    {
        internal static int LastIndex { get; set; }
        internal static string ReturnScene { get; set; } = "StageSelectScene";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            LastIndex = 0;
            ReturnScene = "StageSelectScene";
        }
    }
}
