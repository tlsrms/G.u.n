using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gun.RoomRhythm.Editor
{
    public sealed partial class MapChartWindow
    {
        private sealed class TimelineSpan
        {
            internal int lane, row;
            internal string label;
            internal Color color;
            internal Func<double> start, end;
            internal Action<double> setStart, setEnd;
            internal Action select;
        }
        [SerializeField] private double firstBeat;
        [SerializeField] private float visibleBeats = 16;
        private float panStartX;
        private double panStartBeat;
        private Vector2 timelineScroll;
        private TimelineSpan dragging;
        private bool draggingEnd, draggingBody, scrubbing, resumeAfterScrub;
        private double dragMouseBeat, dragStart, dragFinish;
        private static readonly string[] Lanes = { "문", "방", "적", "카메라", "흔들림" };

        private List<TimelineSpan> Events()
        {
            var spans = new List<TimelineSpan>();
            foreach (var room in Map.rooms)
            {
                if (room.id == Map.settings.startingRoomId) continue;
                spans.Add(new TimelineSpan { lane = 1, label = Map.RoomLabel(room), color = Mint,
                    start = () => room.frameBeat, end = () => room.hitBeat, setStart = b => room.frameBeat = b, setEnd = b => room.hitBeat = b, select = () => PickRoom(room, false) });
                if (room.door)
                    spans.Add(new TimelineSpan { lane = 0, label = Map.RoomLabel(room), color = Orange,
                        start = () => room.doorFrameBeat, end = () => room.doorBeat, setStart = b => room.doorFrameBeat = b, setEnd = b => room.doorBeat = b, select = () => PickRoom(room, false) });
            }
            for (int i = 0; i < Map.enemies.Length; i++)
            {
                int index = i; var enemy = Map.enemies[i]; var room = Map.Room(enemy.roomId);
                spans.Add(new TimelineSpan { lane = 2, label = (room != null ? Map.RoomLabel(room) : "?") + " · " + enemy.direction, color = Pink,
                    start = () => enemy.frameBeat, end = () => enemy.hitBeat, setStart = b => enemy.frameBeat = b, setEnd = b => enemy.hitBeat = b,
                    select = () => { if (room != null) PickRoom(room, false); selectedEnemy = index; selectedCamera = selectedShake = -1; } });
            }
            for (int i = 0; i < Map.cameras.Length; i++)
            {
                int index = i; var key = Map.cameras[i];
                spans.Add(new TimelineSpan { lane = 3, label = "Camera", color = new Color(.7f, .5f, 1), start = () => key.beat, end = () => key.beat + key.duration,
                    setStart = b => key.beat = b, setEnd = b => key.duration = Math.Max(0, b - key.beat),
                    select = () => { SelectEventRoom(key.roomId); selectedCamera = index; selectedEnemy = selectedShake = -1; } });
            }
            for (int i = 0; i < Map.shakes.Length; i++)
            {
                int index = i; var key = Map.shakes[i];
                spans.Add(new TimelineSpan { lane = 4, label = "Shake", color = Color.yellow, start = () => key.beat, end = () => key.beat + key.duration,
                    setStart = b => key.beat = b, setEnd = b => key.duration = Math.Max(1.0 / Math.Max(1, Map.settings.subdivision), b - key.beat),
                    select = () => { SelectEventRoom(key.roomId); selectedShake = index; selectedEnemy = selectedCamera = -1; } });
            }
            return spans;
        }
        private void DrawTimeline()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("전체 맵", GUILayout.Width(85))) SetView(ViewMode.Overview);
            if (GUILayout.Button("미리보기", GUILayout.Width(85))) SetView(ViewMode.Preview);
            if (GUILayout.Button(playing ? "재생 정지" : "연출 재생", GUILayout.Width(90)))
            {
                if (playing) playing = false;
                else SetView(ViewMode.Playback);
            }
            GUILayout.Label(ReadOnly ? "재생 전용 · 편집 잠금" : preview ? "미리보기 · 편집 가능" : "전체 배치 · 편집 가능", GUILayout.Width(165));
            if (preview)
            {
                EditorGUI.BeginChangeCheck(); cursor = EditorGUILayout.DoubleField("현재 박", cursor, GUILayout.Width(195));
                if (EditorGUI.EndChangeCheck()) cursor = ClampBeat(cursor);
                GUILayout.Label(Map.settings.Seconds(cursor).ToString("0.000") + "초", GUILayout.Width(80));
            }
            if (GUILayout.Button("맵 중앙", GUILayout.Width(75))) { pan = Vector2.zero; zoom = 85; }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            visibleBeats = EditorGUILayout.Slider("표시 박 수", visibleBeats, 4, 64, GUILayout.Width(280));
            if (GUILayout.Button("현재 시각으로", GUILayout.Width(105))) firstBeat = Math.Floor(cursor / visibleBeats) * visibleBeats;
            GUILayout.Label("우클릭 드래그: 좌우 탐색", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("선 왼쪽: 판정선 시작 / 오른쪽: 정확 입력 · 양 끝 드래그: 시각 수정 · 가운데 드래그: 구간 이동", EditorStyles.miniLabel);

            var spans = Events();
            spans.Sort((a, b) => a.start().CompareTo(b.start()));
            var rowEnds = new List<double>[Lanes.Length];
            for (int lane = 0; lane < rowEnds.Length; lane++) rowEnds[lane] = new List<double>();
            foreach (var span in spans)
            {
                var ends = rowEnds[span.lane]; int row = ends.FindIndex(end => end + .1 < span.start());
                if (row < 0) { row = ends.Count; ends.Add(span.end()); } else ends[row] = span.end();
                span.row = row;
            }
            var laneY = new float[Lanes.Length]; float height = 0;
            for (int lane = 0; lane < laneY.Length; lane++) { laneY[lane] = height; height += Math.Max(1, rowEnds[lane].Count) * 27 + 7; }
            Rect ruler = GUILayoutUtility.GetRect(500, 23, GUILayout.ExpandWidth(true));
            // Consume the remaining window height; a fixed estimate leaves a blank strip below.
            // GUILayout also reserves the actual height of any validation message drawn afterward.
            Rect viewport = GUILayoutUtility.GetRect(500, 100000, 120, 100000,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            float left = 90, width = Math.Max(1, viewport.width - left - 20);
            PanTimeline(ruler, viewport, width);
            Func<double, float> px = b => left + (float)((b - firstBeat) / visibleBeats) * width;
            Func<float, double> beatAt = x => firstBeat + (x - viewport.x - left) / width * visibleBeats;
            int control = GUIUtility.GetControlID("MapTimelineDrag".GetHashCode(), FocusType.Passive);
            Event e = Event.current;
            EditorGUI.DrawRect(ruler, new Color(.11f, .12f, .15f));
            EditorGUI.DrawRect(viewport, new Color(.07f, .08f, .1f));
            for (double beat = Math.Ceiling(firstBeat); beat <= firstBeat + visibleBeats; beat++)
                GUI.Label(new Rect(ruler.x + px(beat), ruler.y, 48, 20), beat.ToString("0"), EditorStyles.miniLabel);
            bool onPlot = e.mousePosition.x >= viewport.x + left && e.mousePosition.x <= viewport.x + left + width;
            bool onRuler = ruler.Contains(e.mousePosition) && onPlot;
            bool onLine = viewport.Contains(e.mousePosition) && onPlot && Math.Abs(e.mousePosition.x - (viewport.x + px(cursor))) <= 5;
            if (preview && e.type == EventType.MouseDown && e.button == 0 && (onRuler || onLine || ReadOnly && viewport.Contains(e.mousePosition) && onPlot))
            {
                scrubbing = true; resumeAfterScrub = playing; playing = false; GUIUtility.hotControl = control;
                cursor = ClampBeat(beatAt(e.mousePosition.x)); e.Use(); Repaint();
            }
            timelineScroll = GUI.BeginScrollView(viewport, timelineScroll, new Rect(0, 0, viewport.width - 16, height), false, true);
            int division = Math.Max(1, Math.Min(16, Map.settings.subdivision));
            for (double beat = Math.Ceiling(firstBeat * division) / division; beat <= firstBeat + visibleBeats; beat += 1.0 / division)
            {
                bool whole = Math.Abs(beat - Math.Round(beat)) < .001;
                EditorGUI.DrawRect(new Rect(px(beat), 0, 1, height), whole ? new Color(.23f, .26f, .29f) : new Color(.12f, .14f, .16f));
            }
            for (int lane = 0; lane < Lanes.Length; lane++) GUI.Label(new Rect(3, laneY[lane] + 5, left - 6, 24), Lanes[lane], EditorStyles.miniLabel);
            foreach (var span in spans)
            {
                double start = span.start(), end = span.end();
                if (Math.Max(start, end) < firstBeat || Math.Min(start, end) > firstBeat + visibleBeats) continue;
                float sx = px(start), ex = px(end), y = laneY[span.lane] + span.row * 27;
                float clippedStart = Mathf.Clamp(Math.Min(sx, ex), left, left + width), clippedEnd = Mathf.Clamp(Math.Max(sx, ex), left, left + width);
                var body = new Rect(clippedStart, y + 16, Math.Max(3, clippedEnd - clippedStart), 7);
                Color color = end >= start ? span.color : Color.red;
                EditorGUI.DrawRect(body, color * .65f);
                Rect startHandle = new Rect(sx - 4, y + 13, 8, 13), endHandle = new Rect(ex - 4, y + 13, 8, 13);
                bool showStart = start >= firstBeat && start <= firstBeat + visibleBeats;
                bool showEnd = end >= firstBeat && end <= firstBeat + visibleBeats;
                if (showStart) EditorGUI.DrawRect(startHandle, color);
                if (showEnd) EditorGUI.DrawRect(endHandle, span.setEnd != null ? color : Color.gray);
                GUI.Label(new Rect(clippedStart, y, Math.Max(40, clippedEnd - clippedStart), 16),
                    new GUIContent(span.label + "  " + start.ToString("0.##") + "→" + end.ToString("0.##"),
                        span.label + " : " + start.ToString("0.###") + " → " + end.ToString("0.###") + "beat"), EditorStyles.whiteMiniLabel);
                if (!ReadOnly && e.type == EventType.MouseDown && e.button == 0)
                {
                    bool atEnd = showEnd && endHandle.Contains(e.mousePosition) && span.setEnd != null;
                    bool atStart = showStart && startHandle.Contains(e.mousePosition);
                    if (atEnd || atStart || body.Contains(e.mousePosition))
                    {
                        span.select(); pendingRoom = null; dragging = span; draggingEnd = atEnd; draggingBody = !atEnd && !atStart;
                        dragStart = start; dragFinish = end;
                        dragMouseBeat = firstBeat + (e.mousePosition.x - left) / width * visibleBeats;
                        Undo.RegisterCompleteObjectUndo(chart, "채보 구간 이동"); GUIUtility.hotControl = control; e.Use(); Repaint();
                    }
                }
            }
            if (preview && cursor >= firstBeat && cursor <= firstBeat + visibleBeats)
                EditorGUI.DrawRect(new Rect(px(cursor) - 1, 0, 2, height), Color.white);
            GUI.EndScrollView();
            if (preview && cursor >= firstBeat && cursor <= firstBeat + visibleBeats)
            {
                float x = ruler.x + px(cursor);
                EditorGUI.DrawRect(new Rect(x - 5, ruler.y + 3, 10, 17), Color.white);
            }
            if (e.type == EventType.MouseDown && e.button == 0 && preview && viewport.Contains(e.mousePosition) && onPlot)
            {
                scrubbing = true; resumeAfterScrub = playing; playing = false; GUIUtility.hotControl = control;
                cursor = ClampBeat(beatAt(e.mousePosition.x)); e.Use(); Repaint();
            }
            if (e.type == EventType.MouseDrag && GUIUtility.hotControl == control)
            {
                if (scrubbing) cursor = ClampBeat(beatAt(e.mousePosition.x));
                else if (dragging != null && !ReadOnly)
                {
                    double beat = ClampBeat(Snap(beatAt(e.mousePosition.x)));
                    if (draggingBody)
                    {
                        double delta = Snap(beatAt(e.mousePosition.x) - dragMouseBeat);
                        delta = Math.Max(Map.settings.Beat(0) - dragStart, Math.Min(EndBeat() - dragFinish, delta));
                        dragging.setStart(dragStart + delta); dragging.setEnd?.Invoke(dragFinish + delta);
                    }
                    else if (draggingEnd) dragging.setEnd(Math.Max(dragging.start(), beat));
                    else
                    {
                        dragging.setStart(Math.Min(dragFinish, beat));
                        // Resizing the start preserves the opposite endpoint, including duration-based events.
                        dragging.setEnd?.Invoke(dragFinish);
                    }
                    EditorUtility.SetDirty(chart); serialized = null; message = null;
                }
                e.Use(); Repaint();
            }
            if (e.type == EventType.MouseUp && GUIUtility.hotControl == control && (scrubbing || dragging != null))
            {
                if (scrubbing) { scrubbing = false; playing = resumeAfterScrub && ReadOnly; lastTick = EditorApplication.timeSinceStartup; }
                dragging = null; GUIUtility.hotControl = 0; e.Use(); Repaint();
            }
        }
        private void PanTimeline(Rect ruler, Rect viewport, float plotWidth)
        {
            int control = GUIUtility.GetControlID("MapTimelinePan".GetHashCode(), FocusType.Passive);
            Event e = Event.current;
            bool inside = ruler.Contains(e.mousePosition) || viewport.Contains(e.mousePosition) && e.mousePosition.x < viewport.xMax - 16;
            if (e.type == EventType.MouseDown && e.button == 1 && inside && GUIUtility.hotControl == 0)
            { GUIUtility.hotControl = control; panStartX = e.mousePosition.x; panStartBeat = firstBeat; e.Use(); }
            if (GUIUtility.hotControl == control)
            {
                EditorGUIUtility.AddCursorRect(new Rect(0, 0, position.width, position.height), MouseCursor.Pan);
                if (e.type == EventType.MouseDrag)
                {
                    firstBeat = Math.Max(Map.settings.Beat(0), panStartBeat - (e.mousePosition.x - panStartX) / plotWidth * visibleBeats);
                    e.Use(); Repaint();
                }
                if (e.type == EventType.MouseUp && e.button == 1) { GUIUtility.hotControl = 0; e.Use(); Repaint(); }
            }
            if (e.type == EventType.ContextClick && inside) e.Use();
        }
        private void SelectEventRoom(string roomId)
        {
            var room = Map.Room(roomId) ?? (selection.Count > 0 ? Map.Room(selection[selection.Count - 1]) : null) ?? Map.Room(Map.settings.startingRoomId);
            if (room != null) PickRoom(room, false);
        }
        private double ClampBeat(double beat) => double.IsNaN(beat) || double.IsInfinity(beat) ? Map.settings.Beat(0)
            : Math.Max(Map.settings.Beat(0), Math.Min(EndBeat(), beat));
    }
}
