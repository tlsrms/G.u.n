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
        private double lastTick;
        private string message;
        private bool error;
        private SerializedObject serialized;
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
        private void OnEnable() { tool = 0; Undo.undoRedoPerformed += Refresh; EditorApplication.update += Tick; }
        private void OnDisable() { Undo.undoRedoPerformed -= Refresh; EditorApplication.update -= Tick; playing = false; }
        private void Refresh() { serialized = null; CancelInteraction(); message = null; Repaint(); }
        private void CancelInteraction()
        {
            dragging = null; scrubbing = false; draggedCamera = -1; movingRoom = false; tool = (int)MapTool.Select;
            pendingRoom = null; GUIUtility.hotControl = 0;
        }
        private void SetView(ViewMode mode)
        { CancelInteraction(); viewMode = mode; playing = mode == ViewMode.Playback; lastTick = EditorApplication.timeSinceStartup; Repaint(); }
        private void Select(RoomChart value)
        {
            chart = value; selection.Clear(); selectedEnemy = selectedCamera = selectedShake = -1;
            viewMode = ViewMode.Overview; playing = false; cursor = 0; pan = Vector2.zero; Refresh();
        }
        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (playing && chart != null && Map != null && !EditorApplication.isPlaying)
            {
                cursor += Math.Min(.1, now - lastTick) * Map.settings.bpm / 60;
                if (cursor >= EndBeat()) { cursor = EndBeat(); playing = false; }
                Repaint();
            }
            lastTick = now;
        }
        private void Action(Action operation)
        {
            try { operation(); error = false; }
            catch (ArgumentException exception) { message = exception.Message; error = true; Repaint(); }
            catch (Exception exception) { message = exception.Message; error = true; Debug.LogException(exception); }
        }
        private void Edit(string name, Action change)
        {
            Undo.RecordObject(chart, name); change(); EditorUtility.SetDirty(chart); serialized = null; message = null; Repaint();
        }
        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var chosen = (RoomChart)EditorGUILayout.ObjectField(chart, typeof(RoomChart), false, GUILayout.Width(240));
            if (chosen != chart) Select(chosen);
            using (new EditorGUI.DisabledScope(chart == null || ReadOnly || pendingRoom != null || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("씬에서 가져오기", EditorStyles.toolbarButton)) Action(() => Edit("맵 가져오기", () =>
                {
                    MapChart imported = chart.appliedMap != null && chart.appliedMap.rooms.Length > 0
                        ? JsonUtility.FromJson<MapChart>(JsonUtility.ToJson(chart.appliedMap)) : MapSceneStore.Import(chart);
                    chart.mapDraft = imported; chart.mapDraftMusic = chart.music;
                    selection.Clear(); selectedEnemy = selectedCamera = selectedShake = -1;
                }));
                using (new EditorGUI.DisabledScope(chart == null || chart.mapDraft == null || chart.mapDraft.rooms.Length == 0))
                {
                    if (GUILayout.Button("초안만 저장", EditorStyles.toolbarButton)) { AssetDatabase.SaveAssetIfDirty(chart); message = "초안을 저장했습니다. 씬은 아직 바뀌지 않았습니다."; }
                    if (GUILayout.Button("검증", EditorStyles.toolbarButton)) Action(() => { MapSceneStore.Validate(chart); message = "채보·등장·카메라 검증 통과"; });
                    if (GUILayout.Button("저장 · 씬에 적용", EditorStyles.toolbarButton)) Action(() => { MapSceneStore.Save(chart); serialized = null; message = "채보와 씬 저장 완료. 실행 중 생성 없이 미리 배치했습니다."; });
                }
            }
            EditorGUILayout.EndHorizontal();
            if (chart == null) { EditorGUILayout.HelpBox("편집할 Room Chart를 지정하고 MainScene을 열어 주세요.", MessageType.Info); return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) { playing = false; EditorGUILayout.HelpBox("Play를 종료한 뒤 편집하세요.", MessageType.Info); return; }
            if (Map == null || Map.rooms == null || Map.rooms.Length == 0)
            { EditorGUILayout.HelpBox("‘씬에서 가져오기’로 기존 맵을 편집용 초안으로 가져오세요.", MessageType.Info); DrawMessage(); return; }
            if (Map.NeedsAppearanceMigration) Edit("방별 등장 시각으로 전환", () => Map.MigrateAppearance());
            if (serialized == null) serialized = new SerializedObject(chart);
            tab = GUILayout.Toolbar(tab, new[] { "곡 · 기본 설정", "맵 · 채보 · 연출" }, GUILayout.Height(28));
            if (tab == 0) { using (new EditorGUI.DisabledScope(ReadOnly)) DrawSettings(); } else DrawMapEditor();
            DrawMessage();
        }
        private void DrawMessage() { if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, error ? MessageType.Error : MessageType.Info); }
        private void DrawSettings()
        {
            serialized.Update(); var map = serialized.FindProperty("mapDraft"); var settings = map.FindPropertyRelative("settings");
            propertiesScroll = EditorGUILayout.BeginScrollView(propertiesScroll);
            EditorGUILayout.LabelField("곡 설정", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serialized.FindProperty("mapDraftMusic"), new GUIContent("음악 소스"));
            Field(settings, "bpm", "BPM"); Field(settings, "offsetSeconds", "첫 박 시각 (초)");
            Field(settings, "beatsPerBar", "마디당 박 수"); Division(settings);
            Field(settings, "toleranceBeats", "성공 범위 (±박)"); Field(settings, "accurateBeats", "정확 범위 (±박)");
            Field(settings, "startingRoomId", "시작 방 ID");
            EditorGUILayout.Space(); EditorGUILayout.LabelField("새 채보 기본값", EditorStyles.boldLabel);
            Field(settings, "roomLeadBeats", "방 판정선 선행 박"); Field(settings, "enemyLeadBeats", "적 등장 선행 박");
            Field(map, "roomSize", "방 한 변 (월드 유닛)"); Field(map, "originX", "격자 원점 X"); Field(map, "originY", "격자 원점 Y");
            EditorGUILayout.Space(); EditorGUILayout.LabelField("카메라", EditorStyles.boldLabel);
            Field(map, "cameraTrack", "직접 만든 이동 이벤트 사용");
            Field(map, "cameraX", "초기 위치 X"); Field(map, "cameraY", "초기 위치 Y"); Field(map, "cameraSize", "초기 크기 (세로 반높이)");
            EditorGUILayout.HelpBox("이동 이벤트를 끄면 플레이어를 따라갑니다. 흔들림 이벤트는 두 모드에서 모두 적용됩니다. 모든 시각은 음악에 고정된 박 위치입니다. 첫 박은 0박입니다.", MessageType.Info);
            if (serialized.ApplyModifiedProperties()) message = null;
            EditorGUILayout.EndScrollView();
        }
        private static void Field(SerializedProperty parent, string name, string label) => EditorGUILayout.PropertyField(parent.FindPropertyRelative(name), new GUIContent(label));
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
            EditorGUILayout.EndScrollView(); EditorGUILayout.EndVertical(); EditorGUILayout.EndHorizontal();
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
                Field(data, "appearBeat", "방 등장 박");
                Field(data, "frameBeat", "이동 판정선 시작 박"); Field(data, "hitBeat", "방 이동 정확 박");
                EditorGUILayout.Space(); Field(data, "door", "입구 문");
                if (data.FindPropertyRelative("door").boolValue)
                { Field(data, "doorFrameBeat", "문 판정선 시작 박"); Field(data, "doorBeat", "문 사격 정확 박"); }
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
            if (GUILayout.Button("방 위치 이동")) { CancelInteraction(); tool = (int)MapTool.MoveRoom; }
            bool deleteRoom;
            using (new EditorGUI.DisabledScope(room.id == Map.settings.startingRoomId))
                deleteRoom = GUILayout.Button("이 방 삭제");
            EditorGUILayout.EndHorizontal();
                if (deleteRoom)
                {
                    Edit("방 삭제", () => {
                        Map.rooms = Array.FindAll(Map.rooms, r => r.id != room.id);
                        Map.enemies = Array.FindAll(Map.enemies, e => e.roomId != room.id);
                        Map.cameras = Array.FindAll(Map.cameras, e => e.roomId != room.id);
                        Map.shakes = Array.FindAll(Map.shakes, e => e.roomId != room.id);
                        selection.Clear(); selectedEnemy = selectedCamera = selectedShake = -1;
                    });
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
                        int existing = Array.FindIndex(Map.enemies, e => e.roomId == room.id && (int)e.direction == direction);
                        if (existing >= 0) selectedEnemy = existing;
                        else Edit("방에 적 추가", () => {
                            double beat = room.id == Map.settings.startingRoomId ? 2 : room.hitBeat + 2;
                            Add(ref Map.enemies, new MapEnemy { id = "enemy_" + Guid.NewGuid().ToString("N"), roomId = room.id, direction = (EnemyDirection)direction,
                                hitBeat = beat, appearBeat = Math.Max(Map.settings.Beat(0), beat - Map.settings.enemyLeadBeats), frameBeat = Math.Max(Map.settings.Beat(0), beat - Map.settings.enemyLeadBeats) });
                            selectedEnemy = Map.enemies.Length - 1;
                        });
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            for (int i = 0; i < Map.enemies.Length; i++)
                if (Map.enemies[i].roomId == room.id && GUILayout.Button(Map.enemies[i].direction + " · " + Map.enemies[i].hitBeat.ToString("0.###") + "beat", EditorStyles.miniButton)) selectedEnemy = i;
            if (selectedEnemy >= 0 && selectedEnemy < Map.enemies.Length && Map.enemies[selectedEnemy].roomId == room.id)
            {
                if (serialized == null) serialized = new SerializedObject(chart);
                serialized.Update(); var enemy = serialized.FindProperty("mapDraft").FindPropertyRelative("enemies").GetArrayElementAtIndex(selectedEnemy);
                Field(enemy, "direction", "방향"); Field(enemy, "appearBeat", "적 등장 박"); Field(enemy, "frameBeat", "판정선 시작 박"); Field(enemy, "hitBeat", "사격 정확 박");
                if (GUILayout.Button("선택한 적 삭제")) { serialized.FindProperty("mapDraft").FindPropertyRelative("enemies").DeleteArrayElementAtIndex(selectedEnemy); selectedEnemy = -1; }
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
                selectedCamera = Map.cameras.Length - 1; selectedShake = -1; Map.cameraTrack = true;
            });
            if (GUILayout.Button("흔들림 추가")) Edit("흔들림 추가", () => {
                Add(ref Map.shakes, new MapShake { roomId = room.id, beat = room.id == Map.settings.startingRoomId ? 0 : room.hitBeat });
                selectedShake = Map.shakes.Length - 1; selectedCamera = -1;
            });
            EditorGUILayout.EndHorizontal();
            for (int i = 0; i < Map.cameras.Length; i++)
                if ((string.IsNullOrEmpty(Map.cameras[i].roomId) || Map.cameras[i].roomId == room.id) && GUILayout.Button("Camera · " + Map.cameras[i].beat.ToString("0.###") + "beat", EditorStyles.miniButton)) { selectedCamera = i; selectedShake = -1; }
            for (int i = 0; i < Map.shakes.Length; i++)
                if ((string.IsNullOrEmpty(Map.shakes[i].roomId) || Map.shakes[i].roomId == room.id) && GUILayout.Button("Shake · " + Map.shakes[i].beat.ToString("0.###") + "beat", EditorStyles.miniButton)) { selectedShake = i; selectedCamera = -1; }
            if (serialized == null) serialized = new SerializedObject(chart);
            serialized.Update(); var draft = serialized.FindProperty("mapDraft");
            if (selectedCamera >= 0 && selectedCamera < Map.cameras.Length && (string.IsNullOrEmpty(Map.cameras[selectedCamera].roomId) || Map.cameras[selectedCamera].roomId == room.id))
            {
                var key = draft.FindPropertyRelative("cameras").GetArrayElementAtIndex(selectedCamera);
                Field(key, "beat", "시작 박"); Field(key, "duration", "지속 박"); Field(key, "x", "도착 X"); Field(key, "y", "도착 Y"); Field(key, "size", "카메라 크기"); Field(key, "ease", "보간");
                EditorGUILayout.HelpBox("맵의 보라색 마커를 드래그해 도착 위치를 바꿀 수 있습니다.", MessageType.None);
                if (GUILayout.Button("이동 삭제")) { draft.FindPropertyRelative("cameras").DeleteArrayElementAtIndex(selectedCamera); selectedCamera = -1; }
            }
            if (selectedShake >= 0 && selectedShake < Map.shakes.Length && (string.IsNullOrEmpty(Map.shakes[selectedShake].roomId) || Map.shakes[selectedShake].roomId == room.id))
            {
                var key = draft.FindPropertyRelative("shakes").GetArrayElementAtIndex(selectedShake);
                Field(key, "beat", "시작 박"); Field(key, "duration", "지속 박"); Field(key, "strength", "강도"); Field(key, "frequency", "빈도");
                if (GUILayout.Button("흔들림 삭제")) { draft.FindPropertyRelative("shakes").DeleteArrayElementAtIndex(selectedShake); selectedShake = -1; }
            }
            if (serialized.ApplyModifiedProperties()) message = null;
        }
        private double Snap(double beat) => Math.Round(beat * Math.Max(1, Map.settings.subdivision), MidpointRounding.AwayFromZero) / Math.Max(1, Map.settings.subdivision);
        private double EndBeat() => chart.mapDraftMusic != null ? Math.Max(1, Map.settings.Beat(chart.mapDraftMusic.length)) : 64;
        private static void Add<T>(ref T[] array, T item) { Array.Resize(ref array, array.Length + 1); array[array.Length - 1] = item; }
        private void PickRoom(MapRoom room, bool additive)
        {
            tool = (int)MapTool.Select;
            selectedEnemy = selectedCamera = selectedShake = -1;
            if (!additive) selection.Clear();
            if (additive && selection.Contains(room.id)) selection.Remove(room.id); else if (!selection.Contains(room.id)) selection.Add(room.id);
        }
    }
}
