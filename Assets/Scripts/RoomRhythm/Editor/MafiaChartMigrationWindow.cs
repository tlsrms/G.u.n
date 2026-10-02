using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gun.RoomRhythm.Editor
{
    public sealed class MafiaChartMigrationWindow : EditorWindow
    {
        [SerializeField] private RoomSession session;
        private string message;
        private Vector2 scroll;

        [MenuItem("Tools/Gun/Migrate Mafia To Single Chart")]
        private static void Open()
        {
            var window = GetWindow<MafiaChartMigrationWindow>("Single Chart Migration");
            if (Selection.activeGameObject != null) window.session = Selection.activeGameObject.GetComponent<RoomSession>();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("두 채보를 검사한 뒤 새 에셋으로 합칩니다. 원본 채보는 보존하며 씬은 Undo 가능한 변경만 적용합니다. 실행 전 씬과 에셋을 저장하세요.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                session = (RoomSession)EditorGUILayout.ObjectField("Room Session", session, typeof(RoomSession), true);
                if (GUILayout.Button("1. 이관 검사·보고서 미리보기")) Run(false);
                if (GUILayout.Button("2. 백업·새 채보 생성·열린 씬 연결")) Run(true);
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.SelectableLabel(message ?? "", EditorStyles.wordWrappedLabel, GUILayout.MinHeight(300));
            EditorGUILayout.EndScrollView();
        }

        private void Run(bool apply)
        {
            try
            {
                var source = Analyze(session);
                message = Report(source);
                if (!apply) return;
                string path = EditorUtility.SaveFilePanelInProject("새 전체 채보", "Stage1_Full", "asset", "기존 파일과 다른 경로를 선택하세요.");
                if (string.IsNullOrEmpty(path)) return;
                string backup = Apply(source, path);
                message += "\n적용 완료: " + path + "\n복원 사본·보고서: " + backup
                    + "\n씬은 아직 저장하지 않았습니다. 검토 후 Ctrl+S로 저장하세요. 즉시 Undo로 씬 연결을 복원할 수 있습니다.";
            }
            catch (Exception error) { message = "이관 중단: " + error.Message; Debug.LogException(error); }
        }

        private sealed class Source
        {
            public RoomSession session;
            public MafiaStageDirector director;
            public RoomChart first, second;
            public RoomCombat combat, secondCombat;
            public RoomBinding[] rooms, secondRooms;
            public RoomEnemy[] enemies, secondEnemies;
            public StageActionTarget[] targets, secondTargets;
            public MapChartMigration.Result result;
        }

        private static T Reference<T>(UnityEngine.Object owner, string property) where T : UnityEngine.Object
            => new SerializedObject(owner).FindProperty(property).objectReferenceValue as T;
        private static T[] References<T>(UnityEngine.Object owner, string property) where T : UnityEngine.Object
        {
            var items = new SerializedObject(owner).FindProperty(property);
            var result = new T[items.arraySize];
            for (int i = 0; i < result.Length; i++) result[i] = items.GetArrayElementAtIndex(i).objectReferenceValue as T;
            return result;
        }
        private static void SetReferences(UnityEngine.Object owner, string property, UnityEngine.Object[] values)
        {
            var serialized = new SerializedObject(owner); var items = serialized.FindProperty(property);
            items.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) items.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedProperties();
        }

        private static Source Analyze(RoomSession session)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || session == null || EditorUtility.IsPersistent(session))
                throw new ArgumentException("Play 종료 후 열린 MafiaStage01의 Room Session을 선택하세요.");
            var scene = session.gameObject.scene;
            if (!scene.isLoaded || string.IsNullOrEmpty(scene.path) || scene.isDirty)
                throw new ArgumentException("먼저 씬을 저장하세요. 저장된 상태를 복원 사본으로 사용합니다.");
            var fields = new SerializedObject(session);
            var sections = fields.FindProperty("sections");
            if (sections.arraySize != 1 || sections.GetArrayElementAtIndex(0).FindPropertyRelative("id").stringValue != "mafia")
                throw new ArgumentException("initial + mafia 두 구간 구성만 지원합니다. 이미 이관된 씬에는 재적용하지 마세요.");
            var source = new Source { session = session, first = session.Chart,
                director = Reference<MafiaStageDirector>(session, "stageDirector"),
                combat = Reference<RoomCombat>(session, "combat"), rooms = References<RoomBinding>(session, "rooms") };
            if (source.director == null) throw new ArgumentException("Mafia Stage Director 연결이 없습니다.");
            source.second = sections.GetArrayElementAtIndex(0).FindPropertyRelative("chart").objectReferenceValue as RoomChart;
            var section = session.SectionFor(source.second);
            if (source.first == null || source.second == null || source.first == source.second || section == null)
                throw new ArgumentException("서로 다른 두 채보가 필요합니다.");
            source.secondCombat = section.combat; source.secondRooms = section.rooms;
            if (source.combat == null || source.secondCombat == null) throw new ArgumentException("두 Combat 연결이 필요합니다.");
            session.ValidateConfiguration(); session.ValidateChartConfiguration(source.second);
            ValidateSettings(source.first, source.second);
            ValidateSourceMap(source.first); ValidateSourceMap(source.second);
            if (!Mathf.Approximately(source.director.Bpm, source.first.bpm)) throw new ArgumentException("Director와 채보 BPM이 다릅니다.");
            source.result = MapChartMigration.Merge(source.first.appliedMap, source.second.appliedMap,
                source.first.moveDuration, source.first.enemyReadTime, source.first.music.length,
                source.first.judgmentLineWidth, source.director.BattleBeat * 60 / source.director.Bpm);
            source.enemies = References<RoomEnemy>(source.combat, "enemies");
            source.secondEnemies = References<RoomEnemy>(source.secondCombat, "enemies");
            source.targets = References<StageActionTarget>(source.combat, "stageTargets");
            source.secondTargets = References<StageActionTarget>(source.secondCombat, "stageTargets");
            // Combined bindings are validated again after the Undo-backed mutation, before the scene can be saved.
            return source;
        }

        private static void ValidateSourceMap(RoomChart chart)
        {
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(chart))) throw new ArgumentException(chart.name + ": 저장된 채보 에셋이 필요합니다.");
            if (EditorUtility.IsDirty(chart)) throw new ArgumentException(chart.name + ": 먼저 에셋을 저장하세요.");
            if (chart.appliedMap == null || chart.mapDraft == null || chart.mapDraftMusic != chart.music
                || JsonUtility.ToJson(chart.mapDraft) != JsonUtility.ToJson(chart.appliedMap))
                throw new ArgumentException(chart.name + ": 미적용 맵 편집이 있습니다. 원본에서 적용·검증한 뒤 이관하세요.");
            if (chart.appliedMap.settings.startingRoomId != chart.startingRoomId || chart.appliedMap.settings.bpm != chart.bpm)
                throw new ArgumentException(chart.name + ": 맵 원본과 실행 설정이 다릅니다.");
            var compiled = MapChartCompiler.Compile(chart.appliedMap, chart.moveDuration, chart.enemyReadTime, chart.music.length, chart.judgmentLineWidth);
            MapChartMigration.AssertNotesEqual(chart.BuildMovementNotes(), chart.enemies, compiled.Moves, compiled.Enemies);
        }

        private static void ValidateSettings(RoomChart a, RoomChart b)
        {
            if (a.music == null || a.music != b.music) throw new ArgumentException("동일한 음원을 사용해야 합니다.");
            foreach (var field in typeof(RoomChart).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                // Presentation and input parameters must agree; merging must not silently restyle one half.
                if (field.FieldType != typeof(float) && field.FieldType != typeof(double)) continue;
                if (!Equals(field.GetValue(a), field.GetValue(b))) throw new ArgumentException("채보 설정이 다릅니다: " + field.Name);
            }
        }

        private static string Report(Source s)
        {
            var r = s.result; var text = new StringBuilder();
            text.AppendLine(s.first.name + " + " + s.second.name);
            text.AppendLine("시각·방향·등장·문·적 판정 데이터 비교 통과. 원본 변경 없음.");
            text.AppendLine("시작 방 통합: " + r.RemovedStartId + " → " + r.JoinedRoomId);
            text.AppendLine($"방 {r.Map.rooms.Length}, 이동 {r.Compiled.Moves.Length}, 적 {r.Compiled.Enemies.Length}");
            text.AppendLine("두 번째 구간 방 ID 매핑:");
            foreach (var pair in r.SecondRoomIds) text.AppendLine(pair.Key + " → " + pair.Value);
            text.AppendLine($"클립/연출: 기존 Mafia Stage Director 연결 유지. 표적 {s.targets.Length + s.secondTargets.Length}개 보존.");
            return text.ToString();
        }

        private static string Apply(Source s, string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(path)) throw new ArgumentException("기존 에셋을 덮어쓸 수 없습니다.");
            // Dedicated backup outside Assets: Unity must not import duplicated scene/asset GUIDs.
            string backup = Path.Combine("MigrationBackups", "Mafia-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(backup);
            Backup(s.session.gameObject.scene.path, Path.Combine(backup, "scene"));
            Backup(AssetDatabase.GetAssetPath(s.first), Path.Combine(backup, "first"));
            Backup(AssetDatabase.GetAssetPath(s.second), Path.Combine(backup, "second"));
            File.WriteAllText(Path.Combine(backup, "report.txt"), Report(s) + "\nOutput: " + path);
            var merged = UnityEngine.Object.Instantiate(s.first);
            merged.name = Path.GetFileNameWithoutExtension(path);
            merged.mapDraft = MapChartMigration.Copy(s.result.Map); merged.appliedMap = MapChartMigration.Copy(s.result.Map);
            merged.moves = s.result.Compiled.Moves; merged.enemies = s.result.Compiled.Enemies;
            merged.beatChart = null; merged.beatChartMusic = null;
            AssetDatabase.CreateAsset(merged, path); AssetDatabase.SaveAssetIfDirty(merged);
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Mafia single chart migration");
            try
            {
                var rooms = new List<RoomBinding>(s.rooms);
                var seam = Array.Find(s.rooms, r => r.Id == s.result.JoinedRoomId);
                var removed = Array.Find(s.secondRooms, r => r.Id == s.result.RemovedStartId);
                foreach (var room in s.secondRooms) if (room != removed) rooms.Add(room);
                foreach (var enemy in s.secondEnemies)
                    if (enemy.transform.IsChildOf(removed.transform)) Undo.SetTransformParent(enemy.transform, seam.transform, "Preserve seam enemy");
                SetReferences(s.session, "rooms", rooms.ToArray());
                var enemies = new List<RoomEnemy>(s.enemies); enemies.AddRange(s.secondEnemies);
                SetReferences(s.combat, "enemies", enemies.ToArray());
                var targets = new List<StageActionTarget>(s.targets); targets.AddRange(s.secondTargets);
                SetReferences(s.combat, "stageTargets", targets.ToArray());
                var fields = new SerializedObject(s.session); fields.FindProperty("chart").objectReferenceValue = merged;
                fields.FindProperty("sections").arraySize = 0; fields.ApplyModifiedProperties();
                var director = new SerializedObject(s.director); director.FindProperty("singleChart").boolValue = true;
                director.FindProperty("entranceRoomId").stringValue = s.result.JoinedRoomId; director.ApplyModifiedProperties();
                Undo.RecordObject(s.secondCombat, "Suspend legacy combat"); s.secondCombat.enabled = false;
                Undo.RecordObject(removed.gameObject, "Hide duplicate starting room"); removed.gameObject.SetActive(false);
                s.session.ValidateConfiguration();
                EditorSceneManager.MarkSceneDirty(s.session.gameObject.scene);
                Undo.CollapseUndoOperations(undo);
                File.AppendAllText(Path.Combine(backup, "report.txt"), "\nScene bindings validated. Scene save pending.\n");
                return backup;
            }
            catch
            {
                Undo.RevertAllDownToGroup(undo);
                File.AppendAllText(Path.Combine(backup, "report.txt"), "\nScene mutation reverted after failure; generated asset retained for inspection.\n");
                throw;
            }
        }

        private static void Backup(string path, string directory)
        {
            Directory.CreateDirectory(directory);
            File.Copy(path, Path.Combine(directory, Path.GetFileName(path)), overwrite: false);
            // Preserve import identity for an explicit restore; never parse unrelated metadata.
            if (File.Exists(path + ".meta")) File.Copy(path + ".meta", Path.Combine(directory, Path.GetFileName(path) + ".meta"), overwrite: false);
        }
    }
}
