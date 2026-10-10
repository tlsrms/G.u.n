using System;

namespace Gun.RoomRhythm
{
    // One-use handoff: the shared room never infers progression from a scene's build index.
    internal static class SafeRoomTransit
    {
        internal const string SceneName = "SafeRoom";
        private static string destination;
        private static bool walkIn;

        internal static void Prepare(string nextScene, bool showArrival)
        {
            if (string.IsNullOrWhiteSpace(nextScene)) throw new ArgumentException("A safe room needs its onward destination.");
            destination = nextScene;
            walkIn = showArrival;
        }

        internal static bool TryConsume(out string nextScene, out bool showArrival)
        {
            nextScene = destination;
            showArrival = walkIn;
            Reset();
            return !string.IsNullOrWhiteSpace(nextScene);
        }

        internal static void Reset() { destination = null; walkIn = false; }
    }
}
