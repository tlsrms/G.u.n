using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gun.RoomRhythm.Editor
{
    [InitializeOnLoad]
    internal static class JudgmentSettingsProvider
    {
        private static string error;

        static JudgmentSettingsProvider()
        {
            EditorApplication.delayCall += Reload;
            EditorApplication.projectChanged += Reload;
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredEditMode) Reload();
            };
        }

        private static void Reload()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(JudgmentSettingsConfig.AssetPath);
                (asset != null ? JudgmentSettingsConfig.Parse(asset.text) : new JudgmentSettingsConfig()).Apply();
                error = null;
            }
            catch (Exception exception) { error = exception.Message; Debug.LogError(error); }
        }

        [SettingsProvider]
        public static SettingsProvider CreateAudio()
        {
            RoomChart chart = null;
            return new SettingsProvider("Project/Gun/Audio", SettingsScope.Project) {
                label = "Audio",
                keywords = new HashSet<string> { "offset", "오프셋", "calibration", "audio" },
                guiHandler = _ => {
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    {
                        chart = (RoomChart)EditorGUILayout.ObjectField("채보", chart, typeof(RoomChart), false);
                        if (chart == null) return;
                        double offset = EditorGUILayout.DoubleField("입력 보정 오프셋 (ms)", InputOffsetSettings.Milliseconds(chart));
                        bool valid = !double.IsNaN(offset) && !double.IsInfinity(offset) && Math.Abs(offset) <= 1000;
                        using (new EditorGUI.DisabledScope(!valid))
                            if (offset != InputOffsetSettings.Milliseconds(chart))
                            {
                                Undo.RecordObject(chart, "Change song input offset");
                                InputOffsetSettings.Save(chart, offset);
                                EditorUtility.SetDirty(chart);
                                AssetDatabase.SaveAssetIfDirty(chart);
                            }
                        if (!valid) EditorGUILayout.HelpBox("-1000~1000 ms 범위의 유한한 값을 입력하세요.", MessageType.Error);
                    }
                    EditorGUILayout.HelpBox("OffsetScene에서 측정한 값을 사용합니다. 선택한 채보 에셋에만 저장됩니다. 양수는 늦게 들어오는 입력에서 해당 시간을 빼서 보정합니다. 곡의 첫 박 위치와는 별개입니다.", MessageType.Info);
                }
            };
        }

        [SettingsProvider]
        public static SettingsProvider Create()
        {
            double accurate = JudgmentSettings.AccurateMs, tolerance = JudgmentSettings.ToleranceMs;
            return new SettingsProvider("Project/Gun/Judgment", SettingsScope.Project) {
                label = "Judgment",
                keywords = new HashSet<string> { "판정", "정확", "ms", "timing", "judgment" },
                activateHandler = (_, __) => { Reload(); accurate = JudgmentSettings.AccurateMs; tolerance = JudgmentSettings.ToleranceMs; },
                guiHandler = _ => {
                    EditorGUILayout.LabelField("모든 곡 공통 판정", EditorStyles.boldLabel);
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    {
                        accurate = EditorGUILayout.DoubleField("정확 범위 (±ms)", accurate);
                        tolerance = EditorGUILayout.DoubleField("성공 범위 (±ms)", tolerance);
                        bool valid = !double.IsNaN(accurate) && !double.IsInfinity(accurate) && accurate >= 0
                            && !double.IsNaN(tolerance) && !double.IsInfinity(tolerance) && tolerance > accurate;
                        if (!valid) EditorGUILayout.HelpBox("정확 범위는 0 이상, 성공 범위는 정확 범위보다 큰 유한한 값이어야 합니다.", MessageType.Error);
                        using (new EditorGUI.DisabledScope(!valid))
                        {
                            if (GUILayout.Button("저장"))
                            {
                                try
                                {
                                    var config = new JudgmentSettingsConfig { accurateMs = accurate, toleranceMs = tolerance };
                                    Directory.CreateDirectory(Path.GetDirectoryName(JudgmentSettingsConfig.AssetPath));
                                    File.WriteAllText(JudgmentSettingsConfig.AssetPath, JsonUtility.ToJson(config, true) + "\n");
                                    AssetDatabase.ImportAsset(JudgmentSettingsConfig.AssetPath);
                                    config.Apply(); error = null;
                                }
                                catch (Exception exception) { error = exception.Message; }
                            }
                        }
                        if (GUILayout.Button("기본값 불러오기"))
                        { accurate = JudgmentSettings.DefaultAccurateMs; tolerance = JudgmentSettings.DefaultToleranceMs; }
                    }
                    EditorGUILayout.HelpBox("정확 범위 바깥부터 성공 범위까지는 빠름/느림이며, 성공 범위를 벗어나면 사망합니다. 경계값은 포함됩니다. 저장한 값은 모든 곡과 빌드에 적용됩니다.", MessageType.Info);
                    if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
                }
            };
        }
    }
}
