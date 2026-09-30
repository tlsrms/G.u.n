using System;
using System.Collections.Generic;
using Gun.RoomRhythm;

namespace UnityEngine
{
    public static class PlayerPrefs
    {
        private static readonly Dictionary<string, float> values = new Dictionary<string, float>();
        public static bool HasKey(string key) => values.ContainsKey(key);
        public static float GetFloat(string key) => values[key];
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
        Console.WriteLine("PASS: stage record absence, zero accuracy, personal-best retention and stage isolation.");
    }
}
