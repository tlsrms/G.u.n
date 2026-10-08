using System;
using UnityEngine;

namespace Gun.RoomRhythm
{
    [Serializable]
    public sealed class JudgmentSettingsConfig
    {
        public const string AssetPath = "Assets/Resources/JudgmentSettings.json";
        public double accurateMs = JudgmentSettings.DefaultAccurateMs;
        public double toleranceMs = JudgmentSettings.DefaultToleranceMs;

        public void Apply() => JudgmentSettings.Configure(accurateMs, toleranceMs);

        public static JudgmentSettingsConfig Parse(string json)
        {
            var config = new JudgmentSettingsConfig();
            JsonUtility.FromJsonOverwrite(json, config);
            return config;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadRuntime()
        {
            JudgmentSettings.Configure(JudgmentSettings.DefaultAccurateMs, JudgmentSettings.DefaultToleranceMs);
            var asset = Resources.Load<TextAsset>("JudgmentSettings");
            if (asset == null) return;
            try { Parse(asset.text).Apply(); }
            catch (Exception exception) { Debug.LogError("Invalid global judgment settings: " + exception.Message); }
        }
    }
}
