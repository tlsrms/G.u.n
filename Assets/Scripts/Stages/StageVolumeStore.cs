using UnityEngine;

namespace Gun.RoomRhythm
{
    internal static class StageVolumeStore
    {
        private const string KeyPrefix = "Gun.StageMusicVolume.v1.";

        internal static float Read(string stage)
        {
            if (string.IsNullOrWhiteSpace(stage)) return 1;
            float value = PlayerPrefs.GetFloat(KeyPrefix + stage, 1);
            return float.IsNaN(value) || float.IsInfinity(value) ? 1 : Mathf.Clamp01(value);
        }

        internal static void Write(string stage, float value)
        {
            if (!string.IsNullOrWhiteSpace(stage) && !float.IsNaN(value) && !float.IsInfinity(value))
                PlayerPrefs.SetFloat(KeyPrefix + stage, Mathf.Clamp01(value));
        }
    }
}
