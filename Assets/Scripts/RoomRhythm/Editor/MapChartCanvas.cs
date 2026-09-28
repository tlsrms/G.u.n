using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Gun.RoomRhythm.Editor
{
    public sealed partial class MapChartWindow
    {
        private bool movingRoom;
        private int deathPreview;
        private float deathPreviewElapsed;
        private MoveDirection deathPreviewDirection;

        private string draggedRoom;
        private Vector2Int draggedCell;
        private int draggedCamera = -1;
        private Vector2 cameraDrop;
        private MapRoom pendingRoom;
        private string pendingBeat = "";
        private bool focusPending;
        private readonly List<(Rect rect, MapRoom room)> roomLabels = new List<(Rect, MapRoom)>();
        private Vector2 ScreenPoint(Vector2 world, Rect canvas)
            => canvas.size * .5f + pan + new Vector2((world.x - Map.originX) * zoom / Map.roomSize, -(world.y - Map.originY) * zoom / Map.roomSize);
        private Vector2 WorldPoint(Vector2 point, Rect canvas)
        {
            Vector2 p = (point - canvas.size * .5f - pan) * Map.roomSize / zoom;
            return new Vector2(Map.originX + p.x, Map.originY - p.y);
        }
        private Vector2 RoomPoint(MapRoom room, Rect canvas) => ScreenPoint(new Vector2(Map.WorldX(room), Map.WorldY(room)), canvas);
        private Vector2 EnemyPoint(MapEnemy enemy, Rect canvas)
        {
            var room = Map.Room(enemy.roomId); if (room == null) return new Vector2(-10000, -10000);
            float angle = (90 - 45 * (int)enemy.direction) * Mathf.Deg2Rad;
            return ScreenPoint(new Vector2(Map.WorldX(room), Map.WorldY(room)) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * chart.aimRadius, canvas);
        }
        private static void Stroke(Color color, float width, params Vector3[] points)
        { Handles.color = color; Handles.DrawAAPolyLine(Mathf.Max(1, width), points); }
        private static void Square(Vector2 center, float radius, Color color, float width)
        {
            Stroke(color, width, center + new Vector2(-radius, -radius), center + new Vector2(radius, -radius),
                center + new Vector2(radius, radius), center + new Vector2(-radius, radius), center + new Vector2(-radius, -radius));
        }
        private static void Circle(Vector2 center, float radius, Color color, float width)
        {
            var points = new Vector3[65];
            for (int i = 0; i < points.Length; i++) points[i] = center + new Vector2(Mathf.Cos(i * Mathf.PI / 32), Mathf.Sin(i * Mathf.PI / 32)) * radius;
            Stroke(color, width, points);
        }
        private TimingWindow PreviewTiming => JudgmentSettings.Window;
        private float Fade(double start, double hit) => chart.AppearanceAlpha(hit > start ? (float)((cursor - start) / (hit - start)) : 1);
        private MapRoom[] OrderedRooms()
        {
            var rooms = Array.FindAll(Map.rooms, r => r.id != Map.settings.startingRoomId);
            Array.Sort(rooms, (a, b) => a.hitBeat.CompareTo(b.hitBeat)); return rooms;
        }
        private Vector2 IdealPlayer(out string currentRoom)
        {
            var start = Map.Room(Map.settings.startingRoomId) ?? Map.rooms[0];
            currentRoom = start.id;
            Vector2 point = new Vector2(Map.WorldX(start), Map.WorldY(start));
            double duration = chart.moveDuration * Map.settings.bpm / 60;
            foreach (var room in OrderedRooms())
            {
                if (cursor < room.hitBeat) break;
                Vector2 target = new Vector2(Map.WorldX(room), Map.WorldY(room));
                if (cursor < room.hitBeat + duration)
                {
                    float t = (float)((cursor - room.hitBeat) / duration); return Vector2.Lerp(point, target, t * t * (3 - 2 * t));
                }
                point = target; currentRoom = room.id;
            }
            return point;
        }
        private void DrawCanvas(Rect canvas)
        {
            if (!(Map.roomSize > 0 && Map.settings.bpm > 0 && Map.settings.toleranceBeats > 0)
                || float.IsInfinity(Map.roomSize) || double.IsInfinity(Map.settings.bpm))
            { GUI.Label(canvas, "곡 설정에서 양수인 방 크기, BPM, 허용 범위를 설정하세요."); return; }
            EditorGUI.DrawRect(canvas, new Color(.045f, .06f, .075f));
            GUI.BeginGroup(canvas); Handles.BeginGUI();
            Vector2 origin = ScreenPoint(new Vector2(Map.originX, Map.originY), canvas);
            for (float x = origin.x % zoom; x < canvas.width; x += zoom)
                Stroke(new Color(.13f, .16f, .2f), 1, new Vector2(x, 0), new Vector2(x, canvas.height));
            for (float y = origin.y % zoom; y < canvas.height; y += zoom)
                Stroke(new Color(.13f, .16f, .2f), 1, new Vector2(0, y), new Vector2(canvas.width, y));
            Vector2 player = IdealPlayer(out string current);
            foreach (var room in Map.rooms) DrawRoom(room, canvas, current);
            foreach (var room in Map.rooms) DrawDoor(room, canvas, current);
            if (preview) foreach (var room in Map.rooms) DrawRoomFrames(room, canvas, current);
            if (!preview) DrawRoute(canvas);
            foreach (var enemy in Map.enemies) DrawEnemy(enemy, canvas, current);
            DrawRoomLabels(canvas, current);
            if (!ReadOnly && selection.Count > 0)
            {
                string roomId = selection[selection.Count - 1];
                for (int direction = 0; direction < 8; direction++)
                    Circle(EnemyPoint(new MapEnemy { roomId = roomId, direction = (EnemyDirection)direction }, canvas), 5, new Color(1, .4f, .7f, .6f), 1);
            }
            if (preview)
            {
                DrawPlayerPreview(player, current, canvas);
                var pose = Map.CameraAt(cursor, player.x, player.y); CameraRect(canvas, pose.x, pose.y, pose.size, new Color(.2f, .7f, 1));
            }
            if (!ReadOnly && selectedCamera >= 0)
            {
                for (int i = 0; i < Map.cameras.Length; i++)
                {
                    var key = Map.cameras[i]; Vector2 p = ScreenPoint(new Vector2(key.x, key.y), canvas);
                    EditorGUI.DrawRect(new Rect(p - Vector2.one * 6, Vector2.one * 12), selectedCamera == i ? Color.white : new Color(.7f, .5f, 1));
                    GUI.Label(new Rect(p + Vector2.one * 6, new Vector2(100, 20)), "CAM " + key.beat.ToString("0.##"));
                    if (selectedCamera == i) CameraRect(canvas, key.x, key.y, key.size, new Color(.7f, .5f, 1));
                }
            }
            if (movingRoom)
                Square(ScreenPoint(new Vector2(Map.originX + draggedCell.x * Map.roomSize, Map.originY + draggedCell.y * Map.roomSize), canvas), zoom / 2, Color.yellow, 2);
            if (draggedCamera >= 0) CameraRect(canvas, cameraDrop.x, cameraDrop.y, Map.cameras[draggedCamera].size, Color.yellow);
            if (pendingRoom != null)
            {
                var point = RoomPoint(pendingRoom, canvas); Square(point, zoom / 2, Color.yellow, 2);
                GUI.Label(new Rect(point.x - 45, point.y - 10, 120, 20), "박자 입력 대기", EditorStyles.whiteMiniLabel);
            }
            Handles.EndGUI(); GUI.EndGroup();
            if (preview)
            {
                Rect controls = new Rect(canvas.x + 8, canvas.y + 8, 190, 18);
                deathPreview = EditorGUI.Popup(controls, deathPreview, new[] { "정상 재생", "사망: 벽 충돌", "사망: 이른 통로 이탈", "사망: 무입력 붉은 섬광" });
                if (deathPreview != 0)
                {
                    controls.y += 22;
                    deathPreviewElapsed = EditorGUI.Slider(controls, deathPreviewElapsed, 0, 1.3f);
                    controls.y += 22;
                    deathPreviewDirection = (MoveDirection)EditorGUI.EnumPopup(controls, deathPreviewDirection);
                }
            }
            CanvasInput(canvas);
        }
        private void DrawPlayerPreview(Vector2 player, string current, Rect canvas)
        {
            var room = Map.Room(current);
            Vector2 center = room != null ? RoomPoint(room, canvas) : ScreenPoint(player, canvas);
            Vector2 direction = deathPreviewDirection == MoveDirection.Up ? Vector2.down
                : deathPreviewDirection == MoveDirection.Down ? Vector2.up
                : deathPreviewDirection == MoveDirection.Left ? Vector2.left : Vector2.right;
            Vector2 p = deathPreview == 0 ? ScreenPoint(player, canvas) : center;
            bool visible = true;
            if (deathPreview == 1)
            {
                p += direction * (zoom * .5f - .28f * zoom / Map.roomSize) * Mathf.Clamp01(deathPreviewElapsed / .13f);
                visible = deathPreviewElapsed < .13f;
                if (!visible && deathPreviewElapsed < .5f)
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * Mathf.PI / 4;
                        Vector2 fragment = p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (deathPreviewElapsed - .13f) * 45;
                        EditorGUI.DrawRect(new Rect(fragment, Vector2.one * 2), new Color(Mint.r, Mint.g, Mint.b, 1 - Mathf.InverseLerp(.13f, .5f, deathPreviewElapsed)));
                    }
            }
            else if (deathPreview == 2)
            {
                p += direction * zoom * Mathf.Clamp01(deathPreviewElapsed / .28f);
                visible = deathPreviewElapsed < .28f;
                if (!visible) Square(center, zoom * .5f, Color.white, chart.judgmentLineWidth * zoom / Map.roomSize);
            }
            else if (deathPreview == 3) visible = deathPreviewElapsed < .24f;
            if (visible) EditorGUI.DrawRect(new Rect(p - Vector2.one * 4, Vector2.one * 8), Mint);
            if (deathPreview == 3)
                EditorGUI.DrawRect(new Rect(center - Vector2.one * zoom * .5f, Vector2.one * zoom), RoomCinematics.DeathFlash(deathPreviewElapsed));
        }
        private static void SquareRect(Rect r, Color color)
            => Stroke(color, 1, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin));
        private void CameraRect(Rect canvas, float x, float y, float size, Color color)
        {
            var center = ScreenPoint(new Vector2(x, y), canvas); float half = size * zoom / Map.roomSize;
            SquareRect(new Rect(center - new Vector2(half * 16 / 9, half), new Vector2(half * 32 / 9, half * 2)), color);
        }
        private void DrawRoom(MapRoom room, Rect canvas, string current)
        {
            double appears = Map.AppearanceBeat(room);
            if (!RoomVisible(room, current)) return;
            var center = RoomPoint(room, canvas); float half = zoom / 2;
            float progress = room.hitBeat > appears ? Mathf.Clamp01((float)((cursor - appears) / (room.hitBeat - appears))) : 1;
            float alpha = preview && room.id != current ? Mathf.Lerp(chart.appearanceStartAlpha, 1, chart.RoomReveal(progress)) : 1;
            float brightness = preview && room.id != current ? chart.RoomBrightness(progress) : 1;
            EditorGUI.DrawRect(new Rect(center - Vector2.one * half, Vector2.one * zoom), new Color(.16f * brightness, .16f * brightness, .16f * brightness, alpha));
            float width = chart.judgmentLineWidth * zoom / Map.roomSize;
            float passage = chart.passageWidth * zoom / Map.roomSize / 2;
            Color wall = new Color(brightness, brightness, brightness, alpha);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    Stroke(wall, width, center + new Vector2(sx * passage, sy * half), center + new Vector2(sx * half, sy * half), center + new Vector2(sx * half, sy * passage));
            var route = Map.OrderedRooms();
            int index = Array.IndexOf(route, room);
            Vector2 exit = index + 1 < route.Length ? (RoomPoint(route[index + 1], canvas) - center).normalized : Vector2.zero;
            Vector2 entry = index > 0 ? (RoomPoint(route[index - 1], canvas) - center).normalized : Vector2.zero;
            foreach (Vector2 normal in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            {
                if (!(preview && deathPreview == 1 && room.id == current)
                    && (normal == exit || room.id != current && normal == entry)) continue;
                Vector2 tangent = new Vector2(-normal.y, normal.x), middle = center + normal * half;
                Stroke(wall, width, middle - tangent * passage, middle + tangent * passage);
            }
            if (!ReadOnly && selection.Contains(room.id)) Square(center, half + 4, Color.cyan, 2);
        }
        private void DrawDoor(MapRoom room, Rect canvas, string current)
        {
            if (!room.door || room.id == Map.settings.startingRoomId
                || preview && (cursor < room.doorFrameBeat || cursor >= room.doorBeat)) return;
            var route = Map.OrderedRooms();
            int index = Array.IndexOf(route, room);
            if (index <= 0) return;
            if (preview)
                for (int i = 0; i < index; i++)
                    if (route[i].x == room.x && route[i].y == room.y && cursor < Map.DepartureBeat(route[i], chart.moveDuration)) return;
            Vector2 center = RoomPoint(room, canvas), normal = (RoomPoint(route[index - 1], canvas) - center).normalized;
            Vector2 tangent = new Vector2(-normal.y, normal.x), entrance = center + normal * zoom / 2;
            float passage = chart.passageWidth * zoom / Map.roomSize / 2, width = chart.judgmentLineWidth * zoom / Map.roomSize;
            double time = Map.settings.Seconds(cursor), start = Map.settings.Seconds(room.doorFrameBeat), target = Map.settings.Seconds(room.doorBeat);
            float close = preview ? RoomDoor.CloseProgress(chart, time, start, target) : 1;
            float offset = passage * (1 - close);
            float flash = close >= 1 && preview ? Mathf.Clamp01(1 - (float)(time - start - RoomDoor.CloseDuration(chart, start, target)) / .12f) : 0;
            Color color = Color.Lerp(Orange, Color.white, flash);
            Stroke(color, width, entrance - tangent * (passage + offset), entrance - tangent * offset);
            Stroke(color, width, entrance + tangent * offset, entrance + tangent * (passage + offset));
            float ringWidth = chart.enemyLineWidth * zoom / Map.roomSize;
            Circle(entrance, RoomDoor.OutlineRadius * zoom / Map.roomSize, Color.white, ringWidth);
            if (preview)
                Circle(entrance, (float)ApproachGeometry.Radius(time, target, RoomDoor.OutlineRadius, chart.enemyLineWidth, PreviewTiming) * zoom / Map.roomSize, Orange, ringWidth);
        }
        private void DrawRoomFrames(MapRoom room, Rect canvas, string current)
        {
            if (room.id == Map.settings.startingRoomId || room.id == current) return;
            Vector2 center = RoomPoint(room, canvas);
            float width = chart.judgmentLineWidth * zoom / Map.roomSize;
            double start = Map.AppearanceBeat(room);
            if (cursor >= start && cursor < room.hitBeat)
            {
                float radius = (float)ApproachGeometry.FixedStartRadius(Map.settings.Seconds(cursor), Map.settings.Seconds(start),
                    Map.settings.Seconds(room.hitBeat), chart.roomFrameStartSize * .5, Map.roomSize * .5) * zoom / Map.roomSize;
                Color color = Mint; color.a = Fade(start, room.hitBeat); Square(center, radius, color, width);
            }
        }
        private void DrawEnemy(MapEnemy enemy, Rect canvas, string current)
        {
            if (preview && (cursor < enemy.appearBeat || cursor >= enemy.hitBeat)) return;
            Vector2 p = EnemyPoint(enemy, canvas); float radius = .38f * zoom / Map.roomSize, width = chart.enemyLineWidth * zoom / Map.roomSize;
            Circle(p, radius, Color.white, width);
            if (selectedEnemy >= 0 && selectedEnemy < Map.enemies.Length && Map.enemies[selectedEnemy] == enemy && !preview) Circle(p, radius + 4, Pink, 2);
            if (preview && cursor >= enemy.frameBeat)
            {
                float r = (float)ApproachGeometry.Radius(Map.settings.Seconds(cursor), Map.settings.Seconds(enemy.hitBeat), .38, chart.enemyLineWidth, PreviewTiming) * zoom / Map.roomSize;
                MapEnemy next = null;
                foreach (var candidate in Map.enemies)
                    if (candidate.hitBeat > cursor && (next == null || candidate.hitBeat < next.hitBeat)) next = candidate;
                Circle(p, r, RoomEnemy.TimingColor(enemy == next), width);
            }
        }
        private bool RoomVisible(MapRoom room, string current)
        {
            if (!preview || room.id == current) return true;
            double start = Map.VisibleAppearanceBeat(room, chart.moveDuration);
            double end = Map.DepartureBeat(room, chart.moveDuration);
            return cursor >= start && cursor < end;
        }
        private void DrawRoomLabels(Rect canvas, string current)
        {
            roomLabels.Clear(); if (ReadOnly) return;
            var counts = new Dictionary<string, int>();
            foreach (var room in Map.OrderedRooms())
            {
                if (!RoomVisible(room, current)) continue;
                string cell = room.x + ":" + room.y; counts.TryGetValue(cell, out int row); counts[cell] = row + 1;
                Vector2 center = RoomPoint(room, canvas);
                Rect rect = new Rect(center.x - zoom / 2 + 4, center.y - zoom / 2 + 6 + row * 18, Math.Max(95, zoom - 8), 18);
                EditorGUI.DrawRect(rect, selection.Contains(room.id) ? new Color(.05f, .3f, .35f, .95f) : new Color(.05f, .08f, .12f, .85f));
                GUI.Label(rect, Map.RoomLabel(room), EditorStyles.whiteMiniLabel); roomLabels.Add((rect, room));
            }
        }
        private void DrawRoute(Rect canvas)
        {
            var route = Map.OrderedRooms();
            var repetitions = new Dictionary<string, int>();
            for (int i = 1; i < route.Length; i++)
            {
                Vector2 a = RoomPoint(route[i - 1], canvas), b = RoomPoint(route[i], canvas);
                Vector2 direction = (b - a).normalized; if (direction == Vector2.zero) continue;
                string key = route[i - 1].x + ":" + route[i - 1].y + ":" + route[i].x + ":" + route[i].y;
                repetitions.TryGetValue(key, out int n); repetitions[key] = n + 1;
                Vector2 side = new Vector2(-direction.y, direction.x), offset = side * (4 + n * 5);
                a += direction * zoom * .25f + offset; b -= direction * zoom * .25f; b += offset;
                Color color = selection.Contains(route[i].id) ? Color.yellow : new Color(.35f, .75f, .9f);
                Stroke(color, 2, a, b); Stroke(color, 2, b - direction * 8 + side * 4, b, b - direction * 8 - side * 4);
            }
        }
        private void CanvasInput(Rect canvas)
        {
            int moveControl = GUIUtility.GetControlID("MapRoomMove".GetHashCode(), FocusType.Passive);
            // Splitter, playhead and timeline drags keep ownership even when crossing the map.
            if (GUIUtility.hotControl != 0 && GUIUtility.hotControl != moveControl) return;
            Event e = Event.current; Vector2 point = e.mousePosition - canvas.position;
            if (canvas.Contains(e.mousePosition) && e.type == EventType.ScrollWheel)
            {
                Vector2 anchor = WorldPoint(point, canvas); zoom = Mathf.Clamp(zoom * Mathf.Exp(-e.delta.y * .05f), 30, 220);
                pan += point - ScreenPoint(anchor, canvas); e.Use(); Repaint();
            }
            if (canvas.Contains(e.mousePosition) && e.type == EventType.MouseDrag && (e.button == 1 || e.button == 2))
            { pan += e.delta; e.Use(); Repaint(); }
            if (ReadOnly) return;
            if (e.type == EventType.MouseDrag && e.button == 0 && movingRoom)
            {
                var world = WorldPoint(point, canvas);
                draggedCell = new Vector2Int(Mathf.RoundToInt((world.x - Map.originX) / Map.roomSize), Mathf.RoundToInt((world.y - Map.originY) / Map.roomSize));
                e.Use(); Repaint();
            }
            if (e.type == EventType.MouseDrag && draggedCamera >= 0) { cameraDrop = WorldPoint(point, canvas); e.Use(); Repaint(); }
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                if (draggedCamera >= 0)
                {
                    int index = draggedCamera;
                    Edit("카메라 위치 이동", () => { Map.cameras[index].x = cameraDrop.x; Map.cameras[index].y = cameraDrop.y; });
                    draggedCamera = -1; e.Use();
                }
                if (movingRoom)
                {
                    // Temporal overlaps are checked on save; sharing a coordinate is legal.
                    Edit("방 위치 이동", () => { var room = Map.Room(draggedRoom); room.x = draggedCell.x; room.y = draggedCell.y; });
                    movingRoom = false; draggedRoom = null; tool = (int)MapTool.Select;
                    GUIUtility.hotControl = 0; e.Use(); Repaint();
                }
            }
            if (!canvas.Contains(e.mousePosition) || e.type != EventType.MouseDown || e.button != 0) return;
            var worldPoint = WorldPoint(point, canvas);
            int x = Mathf.RoundToInt((worldPoint.x - Map.originX) / Map.roomSize), y = Mathf.RoundToInt((worldPoint.y - Map.originY) / Map.roomSize);
            if (ActiveTool == MapTool.MoveRoom)
            {
                // The panel already chose the occurrence. Do not resolve an overlapping tile again.
                var target = Map.Room(draggedRoom);
                bool onTarget = target != null && target.x == x && target.y == y;
                foreach (var label in roomLabels)
                    if (label.room == target && label.rect.Contains(point)) onTarget = true;
                if (onTarget)
                {
                    movingRoom = true;
                    draggedCell = new Vector2Int(target.x, target.y);
                    GUIUtility.hotControl = moveControl;
                    message = null;
                }
                e.Use(); Repaint(); return;
            }
            IdealPlayer(out string currentRoom);
            MapRoom hit = null;
            foreach (var label in roomLabels) if (label.rect.Contains(point)) { hit = label.room; break; }
            if (hit == null)
            {
                var candidates = Array.FindAll(Map.OrderedRooms(), r => r.x == x && r.y == y && RoomVisible(r, currentRoom));
                if (candidates.Length > 0)
                {
                    if (candidates.Length == 1) hit = candidates[0];
                    else message = "겹친 방은 표시된 이름 중 하나를 눌러 선택하세요.";
                }
            }
            if (ActiveTool == MapTool.Select && selectedCamera >= 0 && selectedCamera < Map.cameras.Length)
            {
                var key = Map.cameras[selectedCamera];
                if (Vector2.Distance(ScreenPoint(new Vector2(key.x, key.y), canvas), point) < 8)
                { draggedCamera = selectedCamera; cameraDrop = new Vector2(key.x, key.y); e.Use(); return; }
            }
            // Room actions are one-shot operations requested by the selected room's panel.
            switch (ActiveTool)
            {
                case MapTool.Select:
                    if (hit != null) PickRoom(hit, false);
                    else if (timelineSelection.Count > 0
                        && !Array.Exists(Map.rooms, room => room.x == x && room.y == y && RoomVisible(room, currentRoom)))
                        ClearTimelineSelection();
                    break;
            }
            e.Use(); Repaint();
        }
        private void BeginRoomPlacement(int x, int y)
        {
            if (ReadOnly) return;
            var previous = selection.Count > 0 ? Map.Room(selection[selection.Count - 1]) : null;
            if (previous == null || Math.Abs(x - previous.x) + Math.Abs(y - previous.y) != 1)
            { message = "연결할 방을 선택한 뒤 바로 옆 칸을 누르세요. 이전에 방문한 좌표에도 놓을 수 있습니다."; return; }
            pendingRoom = new MapRoom { x = x, y = y };
            pendingBeat = ""; focusPending = true; message = null;
        }
        private void DrawPendingRoom()
        {
            EditorGUILayout.LabelField("새 방 · 박자 지정", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("이 방으로 이동할 정확한 박자를 입력하세요. 타임라인 커서 위치는 사용하지 않습니다.", MessageType.Info);
            GUI.SetNextControlName("NewRoomBeat"); pendingBeat = EditorGUILayout.TextField("정확한 이동 박", pendingBeat);
            if (focusPending) { EditorGUI.FocusTextInControl("NewRoomBeat"); focusPending = false; }
            TrackFocusedInput("NewRoomBeat");
            bool enter = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return;
            if (GUILayout.Button("이 박자로 배치") || enter)
            {
                if (enter) Event.current.Use();
                Action(CommitRoomPlacement);
            }
            if (GUILayout.Button("취소")) { pendingRoom = null; message = null; }
        }
        private void CommitRoomPlacement()
        {
            if (!double.TryParse(pendingBeat, NumberStyles.Float, CultureInfo.CurrentCulture, out double beat)
                && !double.TryParse(pendingBeat, NumberStyles.Float, CultureInfo.InvariantCulture, out beat))
                throw new ArgumentException("이동 박자를 숫자로 입력하세요.");
            if (double.IsNaN(beat) || double.IsInfinity(beat) || beat <= Map.settings.Beat(0) || beat > EndBeat())
                throw new ArgumentException("음악 범위 안의 유효한 박자를 입력하세요.");
            var ordered = Map.OrderedRooms(); MapRoom previous = ordered[0], next = null;
            foreach (var room in ordered)
            {
                if (room.id == Map.settings.startingRoomId) continue;
                if (Math.Abs(room.hitBeat - beat) < 1e-9) throw new ArgumentException("같은 박에 두 방으로 이동할 수 없습니다.");
                if (room.hitBeat < beat) previous = room; else { next = room; break; }
            }
            var candidate = pendingRoom;
            if (Math.Abs(candidate.x - previous.x) + Math.Abs(candidate.y - previous.y) != 1
                || next != null && Math.Abs(candidate.x - next.x) + Math.Abs(candidate.y - next.y) != 1)
                throw new ArgumentException("입력한 박자의 이전/다음 방과 바로 옆 칸으로 연결되어야 합니다.");
            double frame = Math.Max(Map.settings.Beat(0), beat - Map.settings.roomLeadBeats);
            candidate.id = "room_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            candidate.hitBeat = beat;
            var trial = new MapChart { settings = Map.settings, rooms = (MapRoom[])Map.rooms.Clone(), groups = Map.groups };
            Add(ref trial.rooms, candidate);
            candidate.individualAppearance = true; candidate.appearBeat = frame; candidate.hitBeat = beat; candidate.frameBeat = frame;
            candidate.doorBeat = beat - 1; candidate.doorFrameBeat = frame;
            trial.ValidateRoomReuse(chart.moveDuration);
            Edit("방 배치", () => { Add(ref Map.rooms, candidate); PickRoom(candidate, false); pendingRoom = null; tool = (int)MapTool.Select; });
        }
    }
}

