using System;
using UnityEditor;
using UnityEngine;

namespace Gun.RoomRhythm.Editor
{
    [CustomEditor(typeof(MafiaShieldAmbush))]
    public sealed class MafiaShieldAmbushInspector : UnityEditor.Editor
    {
        private bool references;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var effect = (MafiaShieldAmbush)target;
            EditorGUILayout.HelpBox("문 중심을 원하는 벽에 배치하세요. 아래 이동 채보의 출발 방에서 보스가 등장합니다. 일반 적 채보는 그대로 사용할 수 있습니다.", MessageType.Info);
            Field("session", "Game Session (비워두면 같은 씬에서 찾기)");
            var session = effect.FindSession();
            var id = serializedObject.FindProperty("escapeRoomId");
            var chart = session != null ? session.Chart : null;
            if (chart != null && chart.moves != null)
            {
                var labels = new string[chart.moves.Length + 1];
                labels[0] = string.IsNullOrEmpty(id.stringValue) ? "탈출할 방을 선택하세요" : "연결 누락: " + id.stringValue;
                int selected = 0;
                for (int i = 0; i < chart.moves.Length; i++)
                {
                    var move = chart.moves[i];
                    double beat = chart.appliedMap?.settings?.Beat(move.HitTime) ?? move.HitTime * chart.bpm / 60;
                    labels[i + 1] = $"{i + 1}번째 방 → {i + 2}번째 방 / {beat:0.##}박 / {move.destinationId}";
                    if (move.destinationId == id.stringValue) selected = i + 1;
                }
                EditorGUI.BeginChangeCheck();
                int next = EditorGUILayout.Popup("피신할 이동 채보", selected, labels);
                if (EditorGUI.EndChangeCheck()) id.stringValue = next == 0 ? "" : chart.moves[next - 1].destinationId;
            }
            else EditorGUILayout.PropertyField(id, new GUIContent("탈출 목적지 방 ID"));
            var side = serializedObject.FindProperty("entranceSide");
            EditorGUI.BeginChangeCheck();
            side.enumValueIndex = EditorGUILayout.Popup("보스가 나오는 벽", side.enumValueIndex, new[] { "위쪽 벽", "왼쪽 벽", "아래쪽 벽", "오른쪽 벽" });
            bool directionChanged = EditorGUI.EndChangeCheck();
            Field("leadBeats", "이동 몇 박 전 등장");
            Field("entryDistance", "문에서 들어오는 거리");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("직접 편집할 모션", EditorStyles.boldLabel);
            Field("enter", "등장"); Field("idle", "조준 대기"); Field("burst", "3발 난사"); Field("withdraw", "퇴장");
            Field("shotOffsets", "클립 내 발사 시각 (초)");
            EditorGUILayout.HelpBox("첫 발을 이동의 정확 박자에 맞춥니다. X/Y/Z는 난사 클립 안의 세 발 시각입니다. 모션을 바꿨다면 반동 시각도 맞춰 주세요.", MessageType.None);
            Field("shotSound", "총성"); Field("volume", "총성 크기");
            references = EditorGUILayout.Foldout(references, "프리팹 내부 연결", true);
            if (references)
                foreach (string field in new[] { "visuals", "actors", "boss", "leftDoor", "rightDoor", "muzzleFlash", "tracers" })
                    Field(field, field);
            serializedObject.ApplyModifiedProperties();
            if (directionChanged && !Application.isPlaying)
            {
                var visualRoot = serializedObject.FindProperty("visuals").objectReferenceValue as Transform;
                if (visualRoot != null)
                {
                    Undo.RecordObject(visualRoot, "고기방패 등장 방향 변경");
                    visualRoot.localRotation = Quaternion.Euler(0, 0, 180 + side.enumValueIndex * 90);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(visualRoot);
                }
            }
            string error = effect.ConfigurationError(effect.FindSession());
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
            EditorGUILayout.HelpBox("맵 에디터의 적용으로 생성되는 방 계층 밖에 두세요. Ctrl+D로 복제한 다음 위치와 이동 채보를 바꿉니다. 방 위치를 바꾸면 이 오브젝트도 직접 옮겨 주세요.", MessageType.Info);
        }
        private void Field(string name, string label) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label), true);

        private void OnSceneGUI()
        {
            var effect = (MafiaShieldAmbush)target;
            var data = new SerializedObject(effect);
            int side = data.FindProperty("entranceSide").enumValueIndex;
            float distance = data.FindProperty("entryDistance").floatValue;
            var direction = effect.transform.rotation * Quaternion.Euler(0, 0, 180 + side * 90) * Vector3.up;
            Handles.color = new Color(1, .7f, .2f);
            Handles.DrawLine(effect.transform.position, effect.transform.position + direction * distance);
            Handles.Label(effect.transform.position, "  고기방패 등장 문");
            Handles.Label(effect.transform.position + direction * distance, "  등장 방향");
        }
    }
}
