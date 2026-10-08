using System;
using UnityEngine;

namespace Gun.RoomRhythm
{
    // Only completed runs are submitted. Scene names isolate each stage's local personal best.
    internal static class StageRecordStore
    {
        private const string Prefix = "Gun.StageBestAccuracy.v1.";

        internal static bool TryGetBest(string stage, out float accuracy)
        {
            accuracy = 0;
            if (string.IsNullOrWhiteSpace(stage) || !PlayerPrefs.HasKey(Prefix + stage)) return false;
            float saved = PlayerPrefs.GetFloat(Prefix + stage);
            if (float.IsNaN(saved) || float.IsInfinity(saved) || saved < 0 || saved > 100) return false;
            accuracy = saved;
            return true;
        }

        internal static void SaveClear(string stage, float accuracy)
        {
            if (string.IsNullOrWhiteSpace(stage) || float.IsNaN(accuracy) || float.IsInfinity(accuracy)
                || accuracy < 0 || accuracy > 100) throw new ArgumentException("Invalid stage accuracy.");
            if (TryGetBest(stage, out float previous) && previous >= accuracy) return;
            PlayerPrefs.SetFloat(Prefix + stage, accuracy);
            PlayerPrefs.Save();
        }
    }
}
