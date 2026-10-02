using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gun.RoomRhythm.Editor
{
    public sealed class BossEntranceWindow : EditorWindow
    {
        [SerializeField] private MafiaStageDirector director;
        [SerializeField] private RoomChart chart;
        [SerializeField] private Vector3 anchor;
        [SerializeField] private float beat = 128;
        private BossPoseSnapshot snapshot;
        private readonly MapAudioPreview audio = new MapAudioPreview();
        private bool playing;
        private string error;
        private Vector2 scroll;
        private Transform joint;
        private AnimationClip editClip;
        private BossEntranceTimeline Timeline => director != null ? director.EntranceTimeline : null;

        [MenuItem("Tools/Gun/Boss Entrance Timeline")]
        public static void Open()
        {
            var window = GetWindow<BossEntranceWindow>("Boss Entrance");
            window.minSize = new Vector2(520, 430);
            if (Selection.activeGameObject != null)
            {
                var selected = Selection.activeGameObject.GetComponent<MafiaStageDirector>();
                if (selected != null) window.SelectDirector(selected);
            }
        }

        public static void Open(MafiaStageDirector target)
        {
            Open();
            GetWindow<BossEntranceWindow>().SelectDirector(target);
        }

        private void OnEnable()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorSceneManager.sceneSaving += SceneSaving;
            EditorSceneManager.sceneClosing += SceneClosing;
            Undo.undoRedoPerformed += Stop;
        }
        private void OnDisable()
        {
            Stop();
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorSceneManager.sceneSaving -= SceneSaving;
            EditorSceneManager.sceneClosing -= SceneClosing;
            Undo.undoRedoPerformed -= Stop;
        }
        private void PlayModeChanged(PlayModeStateChange _) => Stop();
        private void SceneSaving(Scene scene, string path) => Stop();
        private void SceneClosing(Scene scene, bool removingScene) => Stop();

        private void SelectDirector(MafiaStageDirector target)
        {
            Stop(); director = target; chart = null; error = null;
            if (director == null) return;
            beat = (float)director.EntranceBeat;
            foreach (var session in Resources.FindObjectsOfTypeAll<RoomSession>())
            {
                if (session.gameObject.scene != director.gameObject.scene) continue;
                var serialized = new SerializedObject(session);
                if (serialized.FindProperty("stageDirector").objectReferenceValue != director) continue;
                chart = session.Chart;
                if (chart == null || chart.moves == null || chart.moves.Length == 0) break;
                string last = string.IsNullOrEmpty(director.EntranceRoomId)
                    ? chart.moves[chart.moves.Length - 1].destinationId : director.EntranceRoomId;
                var rooms = serialized.FindProperty("rooms");
                for (int i = 0; i < rooms.arraySize; i++)
                {
                    var room = rooms.GetArrayElementAtIndex(i).objectReferenceValue as RoomBinding;
                    if (room != null && room.Id == last) anchor = room.Center;
                }
                break;
            }
        }

        private void OnGUI()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                var selected = (MafiaStageDirector)EditorGUILayout.ObjectField("Director", director, typeof(MafiaStageDirector), true);
                if (selected != director) SelectDirector(selected);
                if (director == null)
                {
                    EditorGUILayout.HelpBox("MafiaStage01의 Mafia Stage Director를 선택하세요.", MessageType.Info);
                    return;
                }
                using (new EditorGUI.DisabledScope(snapshot != null))
                {
                    chart = (RoomChart)EditorGUILayout.ObjectField("음악 채보", chart, typeof(RoomChart), false);
                    anchor = EditorGUILayout.Vector3Field("등장 기준점 (마지막 방 중심)", anchor);
                    var next = (BossEntranceTimeline)EditorGUILayout.ObjectField("등장 타임라인", Timeline, typeof(BossEntranceTimeline), false);
                    if (next != Timeline)
                    {
                        var serialized = new SerializedObject(director);
                        serialized.FindProperty("entranceTimeline").objectReferenceValue = next;
                        serialized.ApplyModifiedProperties();
                    }
                    if (Timeline == null && GUILayout.Button("기존 등장 동작으로 타임라인·클립 만들기")) Attempt(CreateTimeline);
                }
                if (Timeline == null) return;
                EditorGUILayout.LabelField($"곡 기준 {director.EntranceBeat:0.##}–{director.BattleBeat:0.##} 박 / {director.Bpm:0.##} BPM");
                EditorGUI.BeginChangeCheck();
                float nextBeat = EditorGUILayout.Slider("곡 기준 박", beat, (float)director.EntranceBeat, (float)director.BattleBeat);
                double seconds = EditorGUILayout.DoubleField("곡 시각 (초)", nextBeat * 60.0 / director.Bpm);
                if (EditorGUI.EndChangeCheck())
                {
                    beat = Mathf.Clamp((float)(seconds * director.Bpm / 60), (float)director.EntranceBeat, (float)director.BattleBeat);
                    audio.Stop(); playing = false; Attempt(Preview);
                }
                DrawTimeline();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("이 시점 보기")) Attempt(Preview);
                    if (GUILayout.Button(playing ? "일시정지" : "음악과 재생"))
                    {
                        if (playing) { audio.Stop(); playing = false; }
                        else Attempt(() => { Preview(); if (chart == null) throw new InvalidOperationException("음악 채보가 필요합니다.");
                            audio.Start(chart.music, beat * 60.0 / director.Bpm - chart.MusicDelaySeconds); playing = true; });
                    }
                    if (GUILayout.Button("종료·원래 자세 복원")) Stop();
                }
                if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
                scroll = EditorGUILayout.BeginScrollView(scroll);
                using (new EditorGUI.DisabledScope(snapshot != null))
                {
                    EditorGUILayout.HelpBox("아래 박자는 등장 시작 기준입니다. 클립은 순서대로 배치하며 종료 자세를 유지합니다. 편집하려면 미리보기를 종료하세요.", MessageType.Info);
                    var serialized = new SerializedObject(Timeline);
                    serialized.Update();
                    foreach (string name in new[] { "durationBeats", "offsetX", "offsetY", "offsetZ", "rotationZ", "clips" })
                        EditorGUILayout.PropertyField(serialized.FindProperty(name), true);
                    serialized.ApplyModifiedProperties();
                    DrawClipEditor();
                    if (GUILayout.Button("검증·에셋 저장")) Attempt(() => { ValidatePreview(); AssetDatabase.SaveAssets(); });
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawTimeline()
        {
            var cues = Timeline.clips ?? Array.Empty<BossEntranceTimeline.ClipCue>();
            Rect area = GUILayoutUtility.GetRect(100, 35 + cues.Length * 24);
            EditorGUI.DrawRect(area, new Color(.12f, .12f, .12f));
            float span = Mathf.Max(.01f, (float)(director.BattleBeat - director.EntranceBeat));
            for (int i = 0; i <= 4; i++)
            {
                float x = area.x + area.width * i / 4;
                GUI.Label(new Rect(x, area.y, 70, 20), (director.EntranceBeat + span * i / 4).ToString("0.#"));
            }
            for (int i = 0; i < cues.Length; i++)
            {
                var cue = cues[i]; if (cue == null) continue;
                Rect bar = new Rect(area.x + cue.startBeat / span * area.width, area.y + 23 + i * 24,
                    Mathf.Max(2, cue.durationBeats / span * area.width), 20);
                EditorGUI.DrawRect(bar, new Color(.22f, .45f, .6f));
                GUI.Label(bar, cue.clip != null ? cue.clip.name : "클립 없음");
            }
            float cursor = area.x + (beat - (float)director.EntranceBeat) / span * area.width;
            EditorGUI.DrawRect(new Rect(cursor, area.y, 2, area.height), Color.yellow);
            var e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && area.Contains(e.mousePosition))
            {
                beat = (float)director.EntranceBeat + Mathf.Clamp01((e.mousePosition.x - area.x) / area.width) * span;
                audio.Stop(); playing = false; Attempt(Preview); e.Use();
            }
        }

        private void DrawClipEditor()
        {
            editClip = (AnimationClip)EditorGUILayout.ObjectField("편집 클립", editClip, typeof(AnimationClip), false);
            if (editClip == null) return;
            joint = (Transform)EditorGUILayout.ObjectField("키를 추가할 관절", joint, typeof(Transform), true);
            if (GUILayout.Button("관절 Z 회전 곡선 추가")) Attempt(() =>
            {
                if (joint == null || joint == director.Boss || !joint.IsChildOf(director.Boss))
                    throw new InvalidOperationException("보스 루트 아래의 관절을 선택하세요.");
                string path = AnimationUtility.CalculateTransformPath(joint, director.Boss);
                var binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), "localEulerAnglesRaw.z");
                if (AnimationUtility.GetEditorCurve(editClip, binding) != null) throw new InvalidOperationException("이미 해당 곡선이 있습니다.");
                Undo.RecordObject(editClip, "Add joint curve");
                AnimationUtility.SetEditorCurve(editClip, binding, AnimationCurve.Constant(0, Mathf.Max(1, editClip.length), joint.localEulerAngles.z));
                EditorUtility.SetDirty(editClip);
            });
            foreach (var binding in AnimationUtility.GetCurveBindings(editClip))
            {
                EditorGUI.BeginChangeCheck();
                var curve = EditorGUILayout.CurveField(binding.path + "/" + binding.propertyName, AnimationUtility.GetEditorCurve(editClip, binding));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(editClip, "Edit entrance clip");
                    AnimationUtility.SetEditorCurve(editClip, binding, curve); EditorUtility.SetDirty(editClip);
                }
            }
        }

        private void CreateTimeline()
        {
            if (director.Boss == null || director.RifleArm == null) throw new InvalidOperationException("Boss와 Rifle Arm 연결이 필요합니다.");
            if (!(director.Bpm > 0) || float.IsInfinity(director.Bpm)
                || !(director.BattleBeat > director.EntranceBeat) || double.IsInfinity(director.BattleBeat))
                throw new InvalidOperationException("유효한 BPM, 등장 시작·전투 시작 박자가 필요합니다.");
            string path = EditorUtility.SaveFilePanelInProject("등장 타임라인 저장", "MafiaEntrance", "asset", "새 에셋 위치를 선택하세요.");
            if (string.IsNullOrEmpty(path)) return;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("기존 에셋을 덮어쓰지 않도록 새 이름을 사용하세요.");
            var asset = CreateInstance<BossEntranceTimeline>();
            asset.durationBeats = (float)(director.BattleBeat - director.EntranceBeat);
            float movementBeats = Mathf.Min(8, asset.durationBeats);
            asset.offsetY = AnimationCurve.EaseInOut(0, 9, movementBeats, 2.8f);
            var clip = new AnimationClip { name = "MafiaRaiseRifle", frameRate = 60 };
            string jointPath = AnimationUtility.CalculateTransformPath(director.RifleArm, director.Boss);
            float rest = director.RifleArm.localEulerAngles.z;
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(jointPath, typeof(Transform), "localEulerAnglesRaw.z"),
                AnimationCurve.EaseInOut(0, rest - 35, movementBeats * 60 / director.Bpm, rest));
            string clipPath = AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.ChangeExtension(path, ".anim"));
            AssetDatabase.CreateAsset(clip, clipPath);
            asset.clips = new[] { new BossEntranceTimeline.ClipCue { clip = clip, durationBeats = movementBeats } };
            AssetDatabase.CreateAsset(asset, path);
            var serialized = new SerializedObject(director);
            serialized.FindProperty("entranceTimeline").objectReferenceValue = asset;
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets(); editClip = clip;
        }

        private void ValidatePreview()
        {
            if (director == null || director.Boss == null || !director.gameObject.scene.IsValid()
                || !director.gameObject.scene.isLoaded || EditorUtility.IsPersistent(director))
                throw new InvalidOperationException("열린 스테이지의 Director와 Boss가 필요합니다.");
            if (AnimationMode.InAnimationMode()) throw new InvalidOperationException("Animation/Timeline 창의 미리보기를 먼저 종료하세요.");
            if (Timeline == null) throw new InvalidOperationException("등장 타임라인을 연결하세요.");
            if (!(director.Bpm > 0) || float.IsInfinity(director.Bpm)) throw new InvalidOperationException("유효한 BPM이 필요합니다.");
            if (chart != null && !Mathf.Approximately(chart.bpm, director.Bpm)) throw new InvalidOperationException("채보와 Director의 BPM을 맞추세요.");
            Timeline.Validate(director.BattleBeat - director.EntranceBeat);
            foreach (var cue in Timeline.clips ?? Array.Empty<BossEntranceTimeline.ClipCue>())
            {
                if (AnimationUtility.GetAnimationEvents(cue.clip).Length > 0 || AnimationUtility.GetObjectReferenceCurveBindings(cue.clip).Length > 0)
                    throw new InvalidOperationException("등장 클립에는 관절 Transform 곡선만 사용하세요. 이벤트·오브젝트 참조 곡선은 지원하지 않습니다.");
                foreach (var binding in AnimationUtility.GetCurveBindings(cue.clip))
                    if (binding.type != typeof(Transform) || string.IsNullOrEmpty(binding.path)
                        || director.Boss.Find(binding.path) == null
                        || !(binding.propertyName.StartsWith("m_LocalPosition.") || binding.propertyName.StartsWith("m_LocalRotation.")
                            || binding.propertyName.StartsWith("m_LocalScale.") || binding.propertyName.StartsWith("localEulerAngles")))
                        throw new InvalidOperationException("클립은 보스 하위 관절의 위치·회전·크기만 편집할 수 있습니다: " + binding.path);
            }
        }

        private void Preview()
        {
            ValidatePreview();
            if (snapshot == null) snapshot = new BossPoseSnapshot(director.Boss);
            snapshot.Restore(forSampling: true);
            director.Boss.gameObject.SetActive(true);
            Timeline.Evaluate(director.Boss, anchor, beat - director.EntranceBeat);
            SceneView.RepaintAll(); Repaint();
        }
        private void Tick()
        {
            if (snapshot != null && (director == null || EditorApplication.isPlayingOrWillChangePlaymode)) { Stop(); return; }
            if (!playing) return;
            Attempt(() =>
            {
                audio.Tick();
                if (!audio.Running && !audio.Starting) { audio.Stop(); playing = false; return; }
                beat = (float)((audio.Seconds + chart.MusicDelaySeconds) * director.Bpm / 60);
                if (beat >= director.BattleBeat) { beat = (float)director.BattleBeat; audio.Stop(); playing = false; }
                Preview();
            });
        }
        private void Stop()
        {
            audio.Stop(); playing = false;
            snapshot?.Restore(); snapshot = null;
            SceneView.RepaintAll(); Repaint();
        }
        private void Attempt(Action action)
        {
            try { error = null; action(); }
            catch (Exception exception) { Stop(); error = exception.Message; }
        }
    }

    [CustomEditor(typeof(MafiaStageDirector))]
    public sealed class MafiaStageDirectorInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("등장 타임라인 편집·미리보기")) BossEntranceWindow.Open((MafiaStageDirector)target);
        }
    }
}
