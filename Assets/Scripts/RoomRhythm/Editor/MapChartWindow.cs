using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gun.RoomRhythm.Editor
{
    public sealed partial class MapChartWindow : EditorWindow
    {
        [SerializeField] private RoomChart chart;
        [SerializeField] private List<string> selection = new List<string>();
        [SerializeField] private int tab, tool;
        [SerializeField] private double cursor;
        [SerializeField] private Vector2 pan;
        [SerializeField] private float zoom = 85;
        private int selectedEnemy = -1, selectedCamera = -1, selectedShake = -1;
        private Vector2 propertiesScroll;
        [SerializeField] private float mapPanelRatio = .65f;
        private float splitStartY, splitStartHeight;
        private enum ViewMode { Overview, Preview, Playback }
        private ViewMode viewMode;
        private bool playing;
        private bool preview => viewMode != ViewMode.Overview;
        private bool ReadOnly => viewMode == ViewMode.Playback;
        private enum MapTool { Select, MoveRoom }
        private MapTool ActiveTool => (MapTool)tool;
        private readonly MapAudioPreview musicPreview = new MapAudioPreview();
        private bool importAttempted;
        private string message;
        private bool error;
        private SerializedObject serialized;
        private Rect? focusedInputBounds;
        private MapChart Map => chart.mapDraft;
        private static readonly Color Mint = new Color(.3f, 1f, .8f), Pink = new Color(1, .3f, .6f), Orange = new Color(1, .65f, .25f);
        private static readonly int[] Divisions = { 1, 2, 3, 4, 6, 8, 12, 16 };
        private static readonly string[] DivisionNames = { "1박", "1/2박", "1/3박", "1/4박", "1/6박", "1/8박", "1/12박", "1/16박" };

        [MenuItem("Window/Gun/시각적 맵 에디터")]
        public static void Open() => Open(Selection.activeObject as RoomChart);
        public static void Open(RoomChart chart)
        {
            var window = GetWindow<MapChartWindow>("시각적 맵 에디터"); window.minSize = new Vector2(1050, 800);
            if (chart != null) window.Select(chart); window.Show();
        }
        private void OnEnable() { tool = 0; Undo.undoRedoPerformed += Refresh; EditorApplication.update += Tick; EditorApplication.playModeStateChanged += OnPlayModeChanged; }
        private void OnDisable() { Undo.undoRedoPerformed -= Refresh; EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= OnPlayModeChanged; StopMusic(); }
        private void OnPlayModeChanged(PlayModeStateChange state) { StopMusic(); }
        private void Refresh() { StopMusic(); serialized = null; CancelInteraction(); timelineSelection.Clear(); message = null; Repaint(); }
        private void StopMusic() { musicPreview.Stop(); playing = false; }
        private void StartMusic()
        {
            if (!preview || chart == null || Map == null) return;
            cursor = ClampBeat(cursor);
            if (cursor >= EndBeat()) { StopMusic(); return; }
            try { musicPreview.Start(chart.mapDraftMusic, Map.settings.Seconds(cursor) - Map.settings.musicDelaySeconds, Map.settings.loopMusic); playing = true; }
            catch (Exception exception) { StopMusic(); message = exception.Message; error = true; }
        }
        private void CancelInteraction()
        {
            dragging = null; scrubbing = false; draggedCamera = -1; movingRoom = false; draggedRoom = null; tool = (int)MapTool.Select;
            pendingRoom = null; GUIUtility.hotControl = 0;
        }
        private void SetView(ViewMode mode)
        {
            bool fromOverview = viewMode == ViewMode.Overview;
            StopMusic(); CancelInteraction(); viewMode = mode;
            if (mode != ViewMode.Overview)
            {
                if (fromOverview || cursor >= EndBeat()) cursor = Map.settings.Beat(0);
                StartMusic();
            }
            Repaint();
        }
        private void Select(RoomChart value)
        {
            StopMusic(); importAttempted = false;
            chart = value; selection.Clear(); selectedEnemy = selectedCamera = selectedShake = -1;
            viewMode = ViewMode.Overview; playing = false; cursor = 0; pan = Vector2.zero; Refresh();
        }
        private void Tick()
        {
            if (playing && chart != null && Map != null && !EditorApplication.isPlaying)
            {
                // The audio sample position is the clock, not elapsed editor repaint time.
                try { musicPreview.Tick(); }
                catch (Exception exception) { StopMusic(); message = exception.Message; error = true; Repaint(); return; }
                if (musicPreview.Running) cursor = ClampBeat(Map.settings.Beat(musicPreview.Seconds + Map.settings.musicDelaySeconds));
                else if (!musicPreview.Starting) { cursor = EndBeat(); StopMusic(); }
                if (cursor >= EndBeat()) StopMusic();
                Repaint();
            }
        }
        private void Action(Action operation)
        {
            try { operation(); error = false; }
            catch (ArgumentException exception) { message = exception.Message; error = true; Repaint(); }
            catch (Exception exception) { message = exception.Message; error = true; Debug.LogException(exception); }
        }
        private void Edit(string name, Action change)
        {
            Undo.RecordObject(chart, name); change();
            if (Map != null) Map.SynchronizeEnemyRooms();
            EditorUtility.SetDirty(chart); serialized = null; message = null; Repaint();
        }
        private void SynchronizeEnemyOwnership()
        {
            if (Map.NeedsEnemyRoomSynchronization)
            {
                Undo.RecordObject(chart, "적 소속 방 갱신");
                Map.SynchronizeEnemyRooms();
                EditorUtility.SetDirty(chart); serialized = null; Repaint();
            }
            if (selectedEnemy >= 0 && selectedEnemy < Map.enemies.Length && timelineSelection.Count <= 1)
            {
                string roomId = Map.enemies[selectedEnemy].roomId;
                if (selection.Count != 1 || selection[0] != roomId)
                { selection.Clear(); selection.Add(roomId); Repaint(); }
            }
        }
        private void OnGUI()
        {
            // Passive canvas controls and toolbar buttons do not release IMGUI text focus.
            // Use the last painted field bounds so the field itself still handles caret clicks.
            if (Event.current.type == EventType.MouseDown &&
                (!focusedInputBounds.HasValue || !focusedInputBounds.Value.Contains(GUIUtility.GUIToScreenPoint(Event.current.mousePosition))))
            {
                GUI.FocusControl(null);
                GUIUtility.keyboardControl = 0;
                EditorGUIUtility.editingTextField = false;
            }
            if (Event.current.type == EventType.Repaint) focusedInputBounds = null;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var chosen = (RoomChart)EditorGUILayout.ObjectField(chart, typeof(RoomChart), false, GUILayout.Width(240));
            if (chosen != chart) Select(chosen);
            using (new EditorGUI.DisabledScope(chart == null || ReadOnly || pendingRoom != null || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("저장", EditorStyles.toolbarButton))
                    Action(() => { MapSceneStore.SaveDraft(chart); message = "채보 저장 완료."; });
                using (new EditorGUI.DisabledScope(chart == null || chart.mapDraft == null || chart.mapDraft.rooms.Length == 0))
                    if (GUILayout.Button("씬에 적용", EditorStyles.toolbarButton))
                        Action(() => { MapSceneStore.ApplyToScene(chart); serialized = null; message = "씬 적용 및 저장 완료."; });
            }
            EditorGUILayout.EndHorizontal();
            if (chart == null) { EditorGUILayout.HelpBox("편집할 Room Chart를 지정하고 MainScene을 열어 주세요.", MessageType.Info); return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) { playing = false; EditorGUILayout.HelpBox("Play를 종료한 뒤 편집하세요.", MessageType.Info); return; }
            if (Map == null || Map.rooms == null || Map.rooms.Length == 0)
            {
                if (!importAttempted)
                {
                    importAttempted = true;
                    Action(() => Edit("맵 자동 불러오기", () => {
                        if (chart.appliedMap != null && chart.appliedMap.rooms != null && chart.appliedMap.rooms.Length > 0)
                            chart.mapDraft = JsonUtility.FromJson<MapChart>(JsonUtility.ToJson(chart.appliedMap));
                        else if ((chart.moves == null || chart.moves.Length == 0) && (chart.enemies == null || chart.enemies.Length == 0))
                        {
                            var settings = BeatChartCompiler.Import(chart.bpm, chart.Timing, chart.roomLeadTime,
                                chart.enemyLeadTime, chart.moves, chart.enemies);
                            settings.startingRoomId = chart.startingRoomId;
                            chart.mapDraft = MapChart.CreateEmpty(settings);
                        }
                        else chart.mapDraft = MapSceneStore.Import(chart);
                        chart.mapDraftMusic = chart.music;
                    }));
                }
                if (Map == null || Map.rooms == null || Map.rooms.Length == 0)
                { EditorGUILayout.HelpBox("MainScene의 Room Session에 연결된 채보를 선택하세요. 씬을 연 뒤 채보를 다시 선택하면 자동으로 불러옵니다.", MessageType.Info); DrawMessage(); return; }
            }            if (Map.NeedsAppearanceMigration) Edit("방별 등장 시각으로 전환", () => Map.MigrateAppearance());
            if (Map.NeedsRoomStartSynchronization) Edit("방과 판정선 시작 박 통일", () => Map.SynchronizeRoomStarts());
            SynchronizeEnemyOwnership();
            HandleDeleteKey();
            HandleTimelineDuplicate();
            if (serialized == null) serialized = new SerializedObject(chart);
            tab = GUILayout.Toolbar(tab, new[] { "곡 · 기본 설정", "맵 · 채보 · 연출" }, GUILayout.Height(28));
            if (tab == 0) { using (new EditorGUI.DisabledScope(ReadOnly)) DrawSettings(); } else DrawMapEditor();
            SynchronizeEnemyOwnership();
            DrawMessage();
        }
        private void DrawMessage() { if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, error ? MessageType.Error : MessageType.Info); }
        private void HandleDeleteKey()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode != KeyCode.Delete || tab != 1 || ReadOnly
                || pendingRoom != null || GUIUtility.hotControl != 0 || GUIUtility.keyboardControl != 0
                || EditorGUIUtility.editingTextField || (selection.Count == 0 && timelineSelection.Count == 0)) return;
            if (timelineSelection.Count > 0) DeleteTimelineSelection();
            else if (selectedEnemy >= 0 && selectedEnemy < Map.enemies.Length
                && selection.Contains(Map.enemies[selectedEnemy].roomId)) DeleteEnemy(selectedEnemy);
            else DeleteRooms(selection);
            e.Use();
        }
        private void DeleteEnemy(int index)
        {
            if (index < 0 || index >= Map.enemies.Length) return;
            string id = Map.enemies[index].id;
            Edit("적 삭제", () => {
                Map.enemies = Array.FindAll(Map.enemies, enemy => enemy.id != id);
                timelineSelection.Clear();
                selectedEnemy = -1;
                CancelInteraction();
            });
        }
        private void DeleteRooms(IEnumerable<string> roomIds)
        {
            var removed = new HashSet<string>(roomIds);
            removed.Remove(Map.settings.startingRoomId);
            removed.RemoveWhere(id => Map.Room(id) == null);
            if (removed.Count == 0) return;
            Edit("방 삭제", () => {
                Map.rooms = Array.FindAll(Map.rooms, room => !removed.Contains(room.id));
                Map.enemies = Array.FindAll(Map.enemies, enemy => !removed.Contains(enemy.roomId));
                Map.cameras = Array.FindAll(Map.cameras, camera => !removed.Contains(camera.roomId));
                Map.shakes = Array.FindAll(Map.shakes, shake => !removed.Contains(shake.roomId));
                selection.RemoveAll(id => removed.Contains(id));
                selectedEnemy = selectedCamera = selectedShake = -1;
                timelineSelection.Clear();
                roomToReveal = null;
                CancelInteraction();
            });
        }
        private void DrawSettings()
        {
            serialized.Update(); var map = serialized.FindProperty("mapDraft"); var settings = map.FindPropertyRelative("settings");
            propertiesScroll = EditorGUILayout.BeginScrollView(propertiesScroll);
            EditorGUILayout.LabelField("곡 설정", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serialized.FindProperty("roomStartBrightness"), new GUIContent("방 초기 밝기"));
            EditorGUILayout.PropertyField(serialized.FindProperty("roomRevealStart"), new GUIContent("방 급등장 시작 비율"));
            EditorGUILayout.PropertyField(serialized.FindProperty("appearanceStartAlpha"), new GUIContent("등장 초기 불투명도"));
            EditorGUILayout.PropertyField(serialized.FindProperty("doorCloseDuration"), new GUIContent("문 닫힘 시간 (초)"));
            EditorGUILayout.PropertyField(serialized.FindProperty("mapDraftMusic"), new GUIContent("음악 소스"));
            double previousBpm = settings.FindPropertyRelative("bpm").doubleValue;
            GUI.SetNextControlName("MapInput:BPM");
            double enteredBpm = EditorGUILayout.DelayedDoubleField("BPM", previousBpm);
            TrackFocusedInput("MapInput:BPM");
            if (ValidBpm(enteredBpm)) settings.FindPropertyRelative("bpm").doubleValue = enteredBpm;
            double bpm = settings.FindPropertyRelative("bpm").doubleValue;
            if (ValidBpm(previousBpm) && ValidBpm(bpm) && previousBpm != bpm)
            {
                // Retain the authored beat counts when changing tempo. Existing assets store seconds.
                settings.FindPropertyRelative("offsetSeconds").doubleValue *= previousBpm / bpm;
            }
            GUI.SetNextControlName("MapInput:InputOffsetMs");
            EditorGUI.BeginChangeCheck();
            var offsetProperty = serialized.FindProperty("inputOffsetMs");
            double inputOffset = EditorGUILayout.DelayedDoubleField("입력 보정 오프셋 (ms)", offsetProperty.doubleValue);
            if (EditorGUI.EndChangeCheck())
            {
                if (!double.IsNaN(inputOffset) && !double.IsInfinity(inputOffset) && Math.Abs(inputOffset) <= 1000)
                {
                    offsetProperty.doubleValue = inputOffset;
                    message = null;
                }
                else { message = "입력 보정 오프셋은 -1000~1000 ms 범위의 유한한 값이어야 합니다."; error = true; }
            }
            TrackFocusedInput("MapInput:InputOffsetMs");
            Field(settings, "loopMusic", "곡 반복 재생");
            using (new EditorGUI.DisabledScope(!ValidBpm(bpm)))
                BeatDurationField(settings, "offsetSeconds", "음원 내 첫 박 시각 (박)", bpm);
            EditorGUILayout.HelpBox("입력 보정 오프셋은 OffsetScene에서 측정한 ms 값을 입력합니다. 현재 채보에만 저장되며 BPM과 무관합니다. 양수는 입력 시각에서 해당 시간을 빼서 보정합니다. 첫 박 시각은 음원 내 0박 위치이며 BPM 기준 박 수입니다.", MessageType.Info);
            Field(settings, "beatsPerBar", "마디당 박 수"); Division(settings);
            Field(settings, "startingRoomId", "시작 방 ID");
            EditorGUILayout.Space(); EditorGUILayout.LabelField("새 채보 기본값", EditorStyles.boldLabel);
            Field(settings, "roomLeadBeats", "방 판정선 선행 박"); Field(settings, "enemyLeadBeats", "적 등장 선행 박");
            Field(map, "roomSize", "방 한 변 (월드 유닛)"); Field(map, "originX", "격자 원점 X"); Field(map, "originY", "격자 원점 Y");
            EditorGUILayout.Space(); EditorGUILayout.LabelField("카메라", EditorStyles.boldLabel);
            Field(map, "cameraTrack", "직접 만든 이동 이벤트 사용");
            Field(map, "cameraX", "초기 위치 X"); Field(map, "cameraY", "초기 위치 Y"); Field(map, "cameraSize", "초기 크기 (세로 반높이)");
            EditorGUILayout.HelpBox("이동 이벤트를 끄면 플레이어를 따라갑니다. 흔들림 이벤트는 두 모드에서 모두 적용됩니다. 모든 시각은 음악에 고정된 박 위치입니다. 첫 박은 0박입니다.", MessageType.Info);
            if (serialized.ApplyModifiedProperties()) { StopMusic(); message = null; firstBeat = Map.settings.Beat(0); cursor = ClampBeat(cursor); }
            EditorGUILayout.EndScrollView();
            ClipFocusedInput();
        }
        private void Field(SerializedProperty parent, string name, string label)
        {
            var property = parent.FindPropertyRelative(name);
            string controlName = "MapInput:" + property.propertyPath;
            GUI.SetNextControlName(controlName);
            EditorGUILayout.PropertyField(property, new GUIContent(label));
            TrackFocusedInput(controlName);
        }
        private static bool ValidBpm(double bpm) => bpm > 0 && !double.IsInfinity(bpm);
        private void BeatDurationField(SerializedProperty settings, string name, string label, double bpm)
        {
            var seconds = settings.FindPropertyRelative(name);
            double scale = ValidBpm(bpm) ? bpm / 60 : 1;
            string controlName = "MapInput:" + seconds.propertyPath;
            GUI.SetNextControlName(controlName);
            EditorGUI.BeginChangeCheck();
            double beats = EditorGUILayout.DoubleField(label, seconds.doubleValue * scale);
            if (EditorGUI.EndChangeCheck()) seconds.doubleValue = beats / scale;
            TrackFocusedInput(controlName);
        }
        private void TrackFocusedInput(string controlName)
        {
            if (Event.current.type != EventType.Repaint || GUI.GetNameOfFocusedControl() != controlName) return;
            Rect rect = GUILayoutUtility.GetLastRect();
            rect.position = GUIUtility.GUIToScreenPoint(rect.position);
            focusedInputBounds = rect;
        }
        private void ClipFocusedInput()
        {
            if (Event.current.type != EventType.Repaint || !focusedInputBounds.HasValue) return;
            // Scrolled-out fields must not retain focus when the toolbar or timeline is clicked.
            Rect viewport = GUILayoutUtility.GetLastRect();
            viewport.position = GUIUtility.GUIToScreenPoint(viewport.position);
            Rect field = focusedInputBounds.Value;
            float left = Mathf.Max(field.xMin, viewport.xMin), top = Mathf.Max(field.yMin, viewport.yMin);
            float right = Mathf.Min(field.xMax, viewport.xMax), bottom = Mathf.Min(field.yMax, viewport.yMax);
            focusedInputBounds = right > left && bottom > top ? Rect.MinMaxRect(left, top, right, bottom) : (Rect?)null;
        }
        private static void Division(SerializedProperty settings)
        {
            var value = settings.FindPropertyRelative("subdivision"); int index = Math.Max(0, Array.IndexOf(Divisions, value.intValue));
            int next = EditorGUILayout.Popup("박자 분할", index, DivisionNames); value.intValue = Divisions[next];
        }
        private void DrawMapEditor()
        {
            EditorGUILayout.LabelField("방 클릭: 설정 · 겹친 방: 이름 클릭 · 휠: 확대 · 우클릭 드래그: 화면 이동", EditorStyles.miniLabel);
            if (ActiveTool == MapTool.MoveRoom)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("선택한 방을 원하는 위치로 드래그하세요.");
                if (GUILayout.Button("취소", GUILayout.Width(60))) { CancelInteraction(); tool = (int)MapTool.Select; }
                EditorGUILayout.EndHorizontal();
            }
            float panelHeight = Math.Max(350, position.height - 290);
            float mapHeight = Mathf.Clamp(panelHeight * mapPanelRatio, 180, panelHeight - 120);
            EditorGUILayout.BeginHorizontal();
            Rect canvas = GUILayoutUtility.GetRect(500, mapHeight, GUILayout.ExpandWidth(true));
            DrawCanvas(canvas);
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            propertiesScroll = EditorGUILayout.BeginScrollView(propertiesScroll, GUILayout.Height(canvas.height));
            using (new EditorGUI.DisabledScope(ReadOnly)) DrawProperties();
            EditorGUILayout.EndScrollView(); ClipFocusedInput(); EditorGUILayout.EndVertical(); EditorGUILayout.EndHorizontal();
            DrawPanelSplitter(panelHeight, mapHeight);
            DrawTimeline();
        }
        private void DrawPanelSplitter(float available, float mapHeight)
        {
            Rect rect = GUILayoutUtility.GetRect(1, 8, GUILayout.ExpandWidth(true));
            int control = GUIUtility.GetControlID("MapTimelineSplitter".GetHashCode(), FocusType.Passive);
            EditorGUI.DrawRect(rect, new Color(.12f, .15f, .18f));
            EditorGUI.DrawRect(new Rect(rect.center.x - 24, rect.center.y - 1, 48, 2), new Color(.5f, .6f, .65f));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition) && GUIUtility.hotControl == 0)
            { GUIUtility.hotControl = control; splitStartY = e.mousePosition.y; splitStartHeight = mapHeight; e.Use(); }
            if (GUIUtility.hotControl != control) return;
            if (e.type == EventType.MouseDrag)
            {
                mapPanelRatio = Mathf.Clamp(splitStartHeight + e.mousePosition.y - splitStartY, 180, available - 120) / available;
                e.Use(); Repaint();
            }
            if (e.type == EventType.MouseUp && e.button == 0) { GUIUtility.hotControl = 0; e.Use(); Repaint(); }
        }
        private void DrawProperties()
        {
            if (DrawMultipleSelection()) return;
            if (pendingRoom != null) { DrawPendingRoom(); return; }
            var room = selection.Count > 0 ? Map.Room(selection[selection.Count - 1]) : null;
            if (room == null)
            {
                EditorGUILayout.HelpBox("방을 클릭하면 그 방의 등장·문·적·카메라를 설정할 수 있습니다. 방이 겹친 곳은 이름을 눌러 선택하세요.", MessageType.Info);
                return;
            }
            if (serialized == null) serialized = new SerializedObject(chart);
            serialized.Update(); var draft = serialized.FindProperty("mapDraft");
            var data = draft.FindPropertyRelative("rooms").GetArrayElementAtIndex(Array.IndexOf(Map.rooms, room));
            EditorGUILayout.LabelField(Map.RoomLabel(room), EditorStyles.boldLabel);
            if (room.id != Map.settings.startingRoomId)
            {
                Field(data, "appearBeat", "방·판정선 시작 박");
                data.FindPropertyRelative("frameBeat").doubleValue = data.FindPropertyRelative("appearBeat").doubleValue;

                Field(data, "hitBeat", "방 이동 정확 박");
                EditorGUILayout.Space(); Field(data, "door", "입구 문");
                if (data.FindPropertyRelative("door").boolValue)
                { Field(data, "doorFrameBeat", "문 등장 박"); Field(data, "doorBeat", "문 사격 정확 박"); }
            }
            else EditorGUILayout.HelpBox("시작 방은 처음부터 표시됩니다.", MessageType.None);
            if (serialized.ApplyModifiedProperties()) message = null;
            EditorGUILayout.Space(); EditorGUILayout.LabelField("이 방 옆에 방 추가", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            string[] arrows = { "↑", "→", "↓", "←" };
            int[] dx = { 0, 1, 0, -1 }, dy = { 1, 0, -1, 0 };
            for (int direction = 0; direction < 4; direction++)
                if (GUILayout.Button(arrows[direction]))
                {
                    CancelInteraction();
                    BeginRoomPlacement(room.x + dx[direction], room.y + dy[direction]);
                }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("방 위치 이동"))
            {
                CancelInteraction();
                draggedRoom = room.id;
                tool = (int)MapTool.MoveRoom;
            }
            bool deleteRoom;
            using (new EditorGUI.DisabledScope(room.id == Map.settings.startingRoomId))
                deleteRoom = GUILayout.Button("이 방 삭제");
            EditorGUILayout.EndHorizontal();
            if (deleteRoom)
            {
                DeleteRooms(new[] { room.id });
                return;
            }
            EditorGUILayout.Space(); EditorGUILayout.LabelField("이 방의 적", EditorStyles.boldLabel);
            for (int row = 0; row < 2; row++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int column = 0; column < 4; column++)
                {
                    int direction = row * 4 + column;
                    if (GUILayout.Button(new[] { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" }[direction]))
                    {
                        timelineSelection.Clear(); selectedCamera = selectedShake = -1;
                        int existing = Array.FindIndex(Map.enemies, e => e.roomId == room.id && (int)e.direction == direction);
                        if (existing >= 0) selectedEnemy = existing;
                        else Edit("방에 적 추가", () => {
                            double beat = room.id == Map.settings.startingRoomId ? 2 : room.hitBeat + 2;
                            double arrival = room.id == Map.settings.startingRoomId ? Map.settings.Beat(0) : room.hitBeat;
                            double appearance = Math.Max(arrival, beat - Map.settings.enemyLeadBeats);
                            Add(ref Map.enemies, new MapEnemy { id = "enemy_" + Guid.NewGuid().ToString("N"), roomId = room.id, direction = (EnemyDirection)direction,
                                hitBeat = beat, appearBeat = appearance, frameBeat = appearance });
                            selectedEnemy = Map.enemies.Length - 1;
                        });
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            for (int i = 0; i < Map.enemies.Length; i++)
                if (Map.enemies[i].roomId == room.id && GUILayout.Button(Map.enemies[i].direction + " · " + Map.enemies[i].hitBeat.ToString("0.###") + "beat", EditorStyles.miniButton))
                { timelineSelection.Clear(); selectedEnemy = i; selectedCamera = selectedShake = -1; }
            if (selectedEnemy >= 0 && selectedEnemy < Map.enemies.Length && Map.enemies[selectedEnemy].roomId == room.id)
            {
                if (serialized == null) serialized = new SerializedObject(chart);
                serialized.Update(); var enemy = serialized.FindProperty("mapDraft").FindPropertyRelative("enemies").GetArrayElementAtIndex(selectedEnemy);
                Field(enemy, "direction", "방향"); Field(enemy, "appearBeat", "적·판정선 시작 박");
                enemy.FindPropertyRelative("frameBeat").doubleValue = enemy.FindPropertyRelative("appearBeat").doubleValue;
                Field(enemy, "hitBeat", "사격 정확 박");
                EditorGUILayout.HelpBox("소속 방은 사격 정확 박으로 결정됩니다. 시작 박이 방 이동보다 빨라도 소속은 바뀌지 않습니다.", MessageType.Info);
                if (GUILayout.Button("선택한 적 삭제"))
                {
                    serialized.ApplyModifiedProperties();
                    DeleteEnemy(selectedEnemy);
                    return;
                }
                if (serialized.ApplyModifiedProperties()) message = null;
            }
            DrawCameraActions(room);
        }
        private void DrawCameraActions(MapRoom room)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("이 방의 카메라", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("카메라 이동 추가")) Edit("카메라 이동 추가", () => {
                Add(ref Map.cameras, new MapCameraKey { roomId = room.id, beat = room.id == Map.settings.startingRoomId ? 0 : room.hitBeat, x = Map.WorldX(room), y = Map.WorldY(room), size = Map.cameraSize });
                timelineSelection.Clear(); selectedCamera = Map.cameras.Length - 1; selectedEnemy = selectedShake = -1; Map.cameraTrack = true;
            });
            if (GUILayout.Button("흔들림 추가")) Edit("흔들림 추가", () => {
                Add(ref Map.shakes, new MapShake { roomId = room.id, beat = room.id == Map.settings.startingRoomId ? 0 : room.hitBeat });
                timelineSelection.Clear(); selectedShake = Map.shakes.Length - 1; selectedEnemy = selectedCamera = -1;
            });
            EditorGUILayout.EndHorizontal();
            for (int i = 0; i < Map.cameras.Length; i++)
                if ((string.IsNullOrEmpty(Map.cameras[i].roomId) || Map.cameras[i].roomId == room.id) && GUILayout.Button("Camera · " + Map.cameras[i].beat.ToString("0.###") + "beat", EditorStyles.miniButton)) { timelineSelection.Clear(); selectedCamera = i; selectedEnemy = selectedShake = -1; }
            for (int i = 0; i < Map.shakes.Length; i++)
                if ((string.IsNullOrEmpty(Map.shakes[i].roomId) || Map.shakes[i].roomId == room.id) && GUILayout.Button("Shake · " + Map.shakes[i].beat.ToString("0.###") + "beat", EditorStyles.miniButton)) { timelineSelection.Clear(); selectedShake = i; selectedEnemy = selectedCamera = -1; }
            if (serialized == null) serialized = new SerializedObject(chart);
            serialized.Update(); var draft = serialized.FindProperty("mapDraft");
            if (selectedCamera >= 0 && selectedCamera < Map.cameras.Length && (string.IsNullOrEmpty(Map.cameras[selectedCamera].roomId) || Map.cameras[selectedCamera].roomId == room.id))
            {
                var key = draft.FindPropertyRelative("cameras").GetArrayElementAtIndex(selectedCamera);
                Field(key, "beat", "시작 박"); Field(key, "duration", "지속 박"); Field(key, "x", "도착 X"); Field(key, "y", "도착 Y"); Field(key, "size", "카메라 크기"); Field(key, "ease", "보간");
                EditorGUILayout.HelpBox("맵의 보라색 마커를 드래그해 도착 위치를 바꿀 수 있습니다.", MessageType.None);
                if (GUILayout.Button("이동 삭제")) { timelineSelection.Clear(); draft.FindPropertyRelative("cameras").DeleteArrayElementAtIndex(selectedCamera); selectedCamera = -1; }
            }
            if (selectedShake >= 0 && selectedShake < Map.shakes.Length && (string.IsNullOrEmpty(Map.shakes[selectedShake].roomId) || Map.shakes[selectedShake].roomId == room.id))
            {
                var key = draft.FindPropertyRelative("shakes").GetArrayElementAtIndex(selectedShake);
                Field(key, "beat", "시작 박"); Field(key, "duration", "지속 박"); Field(key, "strength", "강도"); Field(key, "frequency", "빈도");
                if (GUILayout.Button("흔들림 삭제")) { timelineSelection.Clear(); draft.FindPropertyRelative("shakes").DeleteArrayElementAtIndex(selectedShake); selectedShake = -1; }
            }
            if (serialized.ApplyModifiedProperties()) message = null;
        }
        private double Snap(double beat) => Math.Round(beat * Math.Max(1, Map.settings.subdivision), MidpointRounding.AwayFromZero) / Math.Max(1, Map.settings.subdivision);
        private double EndBeat()
        {
            double end = chart.mapDraftMusic != null
                ? Math.Max(1, Map.settings.Beat(chart.mapDraftMusic.length + Map.settings.musicDelaySeconds)) : 64;
            if (!Map.settings.loopMusic) return end;
            // Repeated audio allows authored notes beyond one clip; leave editing room after them.
            double last = Map.settings.Beat(0);
            foreach (var room in Map.rooms) last = Math.Max(last, room.hitBeat);
            foreach (var enemy in Map.enemies) last = Math.Max(last, enemy.hitBeat);
            foreach (var camera in Map.cameras) last = Math.Max(last, camera.beat + camera.duration);
            foreach (var shake in Map.shakes) last = Math.Max(last, shake.beat + shake.duration);
            return Math.Max(end, last + Math.Max(Map.settings.beatsPerBar, (chart.moveDuration + chart.Timing.late) * Map.settings.bpm / 60));
        }
        private static void Add<T>(ref T[] array, T item) { Array.Resize(ref array, array.Length + 1); array[array.Length - 1] = item; }
        private void PickRoom(MapRoom room, bool additive, bool revealTimeline = true)
        {
            if (revealTimeline) timelineSelection.Clear();
            tool = (int)MapTool.Select;
            selectedEnemy = selectedCamera = selectedShake = -1;
            if (!additive) selection.Clear();
            if (additive && selection.Contains(room.id)) selection.Remove(room.id); else if (!selection.Contains(room.id)) selection.Add(room.id);
            if (revealTimeline && selection.Contains(room.id)) roomToReveal = room.id;
        }
    }
}

