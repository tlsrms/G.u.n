using System;
using System.Collections.Generic;
using Gun.RoomRhythm;

namespace UnityEngine
{
    public static class Mathf { public static float Clamp01(float value) => Math.Max(0, Math.Min(1, value)); }
    public static class PlayerPrefs
    {
        private static readonly Dictionary<string, float> values = new Dictionary<string, float>();
        public static bool HasKey(string key) => values.ContainsKey(key);
        public static float GetFloat(string key) => values[key];
        public static float GetFloat(string key, float fallback) => values.TryGetValue(key, out float value) ? value : fallback;
        public static void SetFloat(string key, float value) => values[key] = value;
        public static void Save() { }
    }
}

internal static class StageRecordStoreChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    public static void Main()
    {
        UnityEngine.PlayerPrefs.SetFloat("Gun.StageMusicVolume.v1.ExistingStage", .42f);
        Check(StageVolumeStore.Read("ExistingStage") == .42f, "existing saved volumes survive the storage refactor");
        Check(StageVolumeStore.Read("Stage01") == 1, "new stages default to full song volume");
        StageVolumeStore.Write("Stage01", 0);
        StageVolumeStore.Write("Stage02", .65f);
        Check(StageVolumeStore.Read("Stage01") == 0 && StageVolumeStore.Read("Stage02") == .65f,
            "mute and volume are retained separately for each stage");
        StageVolumeStore.Write("Stage02", float.NaN);
        Check(StageVolumeStore.Read("Stage02") == .65f, "invalid volume cannot overwrite the saved preference");
        StageVolumeStore.Write("Stage02", 2);
        Check(StageVolumeStore.Read("Stage02") == 1, "saved volume stays in the supported range");
        UnityEngine.PlayerPrefs.SetFloat("Gun.StageMusicVolume.v1.InvalidStage", float.NaN);
        Check(StageVolumeStore.Read("InvalidStage") == 1 && StageVolumeStore.Read(null) == 1,
            "missing stage or invalid stored volume uses the default");
        Check(!StageRecordStore.TryGetBest("Stage01", out _), "No record must remain distinct from zero percent.");
        StageRecordStore.SaveClear("Stage01", 0);
        Check(StageRecordStore.TryGetBest("Stage01", out float value) && value == 0, "Zero-percent clear is a real record.");
        StageRecordStore.SaveClear("Stage01", 85.5f);
        StageRecordStore.SaveClear("Stage01", 70);
        Check(StageRecordStore.TryGetBest("Stage01", out value) && value == 85.5f, "Worse clears cannot replace the best.");
        StageRecordStore.SaveClear("Stage02", 100);
        Check(StageRecordStore.TryGetBest("Stage01", out value) && value == 85.5f, "Stages retain independent records.");
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, -1, 101 })
        {
            bool rejected = false;
            try { StageRecordStore.SaveClear("Stage01", invalid); } catch (ArgumentException) { rejected = true; }
            Check(rejected && StageRecordStore.TryGetBest("Stage01", out value) && value == 85.5f, "Reject invalid results without losing the best.");
        }
        Console.WriteLine("PASS: stage records, stage volume isolation and existing preference compatibility.");
    }
}
