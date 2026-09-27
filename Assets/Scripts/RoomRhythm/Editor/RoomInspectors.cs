using System;
using UnityEditor;
using UnityEngine;

namespace Gun.RoomRhythm.Editor
{
    [CustomEditor(typeof(RoomChart))]
    public sealed class RoomChartInspector : UnityEditor.Editor
    {
        private string result;
        private MessageType resultType;
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("시각적 맵 에디터 열기")) MapChartWindow.Open((RoomChart)target);
            if (DrawDefaultInspector()) result = null;
            var currentChart = (RoomChart)target;
            EditorGUILayout.HelpBox("Room Lead Time: 방 이동 판정 몇 초 전 등장\nEnemy Lead Time: 적 판정 몇 초 전 등장 (방 도착 이후)\n맵 에디터의 문 등장 박으로 문을 독립적으로 표시합니다.\nRoom Start Brightness: 초기 밝기 / Room Reveal Start: 급격히 밝아지기 시작하는 진행률", MessageType.Info);
            EditorGUILayout.LabelField("방 축소 속도 (유닛/초)",
                ApproachGeometry.Speed(currentChart.judgmentLineWidth, currentChart.Timing).ToString("0.###"));
            EditorGUILayout.LabelField("적 축소 속도 (유닛/초)",
                ApproachGeometry.Speed(currentChart.enemyLineWidth, currentChart.Timing).ToString("0.###"));
            if (!currentChart.Timing.IsValid)
                EditorGUILayout.HelpBox("성공 허용 범위는 0보다 커야 하며 ACCURATE 범위는 그보다 작아야 합니다.", MessageType.Error);
            else
                EditorGUILayout.HelpBox($"모든 곡 공통: 정확 ±{JudgmentSettings.AccurateMs}ms / 성공 ±{JudgmentSettings.ToleranceMs}ms. 공통 설정: Project Settings > Gun > Judgment.", MessageType.Info);
            if (GUILayout.Button("공통 판정 설정 열기")) SettingsService.OpenProjectSettings("Project/Gun/Judgment");
            if (GUILayout.Button("Validate chart"))
            {
                try
                {
                    var chart = (RoomChart)target;
                    if (chart.music == null || !(chart.bpm > 0)) throw new ArgumentException("Music and positive BPM required.");
                    new RoomRun(chart.BuildMovementNotes(), chart.Timing, chart.moveDuration, chart.enemies, chart.startingRoomId, chart.enemyReadTime, chart.enemyLeadTime);
                    foreach (MoveNote move in chart.moves)
                        if (move.HitTime + chart.Timing.late + chart.moveDuration > chart.music.length)
                            throw new ArgumentException("Movement exceeds music duration.");
                    foreach (EnemyNote enemy in chart.enemies ?? Array.Empty<EnemyNote>())
                        if (enemy.time + chart.Timing.late > chart.music.length)
                            throw new ArgumentException("Enemy exceeds music duration.");
                    result = "Chart timing and sequence are valid. Validate Game Session to check scene bindings.";
                    resultType = MessageType.Info;
                }
                catch (Exception error) { result = error.Message; resultType = MessageType.Error; }
            }
            if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, resultType);
        }
    }

    [CustomEditor(typeof(RoomSession))]
    public sealed class RoomSessionInspector : UnityEditor.Editor
    {
        private string result;
        private MessageType resultType;
        public override void OnInspectorGUI()
        {
            if (DrawDefaultInspector()) result = null;
            if (GUILayout.Button("채보 · 등장 설정 열기"))
            {
                var chart = serializedObject.FindProperty("chart").objectReferenceValue;
                if (chart != null) Selection.activeObject = chart;
            }
            if (GUILayout.Button("Validate scene and chart"))
            {
                try { ((RoomSession)target).ValidateConfiguration(); result = "Scene and chart are valid."; resultType = MessageType.Info; }
                catch (Exception error) { result = error.Message; resultType = MessageType.Error; }
            }
            if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, resultType);
        }
    }
}


