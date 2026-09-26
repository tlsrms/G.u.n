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
            serializedObject.Update();
            SerializedProperty timing = serializedObject.FindProperty("timing");
            var early = timing.FindPropertyRelative("early");
            var late = timing.FindPropertyRelative("late");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("판정 범위", EditorStyles.boldLabel);
            double tolerance = EditorGUILayout.DoubleField(new GUIContent("성공 허용 범위 (±초)",
                "정확한 시각 전후로 입력을 허용하는 시간입니다. 값이 클수록 쉬워지며 판정선 축소 속도도 자동 조정됩니다."), (early.doubleValue + late.doubleValue) * 0.5);
            EditorGUILayout.PropertyField(timing.FindPropertyRelative("accurate"), new GUIContent("ACCURATE 범위 (±초)",
                "이 범위 안은 ACCURATE, 바깥부터 성공 허용 경계까지는 EARLY 또는 LATE입니다."));
            if (EditorGUI.EndChangeCheck())
            {
                early.doubleValue = late.doubleValue = tolerance;
                serializedObject.ApplyModifiedProperties();
                result = null;
            }
            var currentChart = (RoomChart)target;
            EditorGUILayout.HelpBox("Room Lead Time: 방 이동 판정 몇 초 전 등장\nEnemy Lead Time: 적 판정 몇 초 전 등장 (방 도착 이후)\n문은 방과 함께 등장합니다. 문 판정보다 충분히 먼저 방이 나타나도록 설정하세요.", MessageType.Info);
            EditorGUILayout.LabelField("방 축소 속도 (유닛/초)",
                ApproachGeometry.Speed(currentChart.judgmentLineWidth, currentChart.Timing).ToString("0.###"));
            EditorGUILayout.LabelField("적 축소 속도 (유닛/초)",
                ApproachGeometry.Speed(currentChart.enemyLineWidth, currentChart.Timing).ToString("0.###"));
            if (!currentChart.Timing.IsValid)
                EditorGUILayout.HelpBox("성공 허용 범위는 0보다 커야 하며 ACCURATE 범위는 그보다 작아야 합니다.", MessageType.Error);
            else
                EditorGUILayout.HelpBox($"성공: ±{currentChart.Timing.early * 1000:0.#}ms / ACCURATE: ±{currentChart.Timing.accurate * 1000:0.#}ms. Play 중 변경하면 판정·표시를 함께 갱신하고 시작 대기로 돌아갑니다.", MessageType.Info);
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
            if (GUILayout.Button("채보 · 등장 · 판정 설정 열기"))
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
