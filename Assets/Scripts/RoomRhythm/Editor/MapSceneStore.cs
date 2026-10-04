using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gun.RoomRhythm.Editor
{
    // Only explicitly bound map objects are replaced. Player, HUD and other scene content stay intact.
    internal static class MapSceneStore
    {
        internal static RoomSession Session(RoomChart chart)
        {
            RoomSession match = null;
            foreach (var session in UnityEngine.Object.FindObjectsByType<RoomSession>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (session.Chart != chart) continue;
                if (match != null) throw new ArgumentException("이 채보를 사용하는 Session이 여러 개입니다. 편집할 씬 하나만 열어 주세요.");
                match = session;
            }
            if (match == null) throw new ArgumentException("열린 씬의 Room Session에 이 채보를 연결하세요.");
            return match;
        }
        private static RoomSession ApplicationSession(RoomChart chart)
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var sessions = new List<RoomSession>();
            if (activeScene.IsValid() && activeScene.isLoaded)
                foreach (var root in activeScene.GetRootGameObjects())
                    sessions.AddRange(root.GetComponentsInChildren<RoomSession>(true));
            if (sessions.Count == 1) return sessions[0];
            if (sessions.Count == 0) return Session(chart);
            var matches = sessions.FindAll(session => session.Chart == chart);
            if (matches.Count == 1) return matches[0];
            throw new ArgumentException("활성 씬에 Room Session이 여러 개여서 적용 대상을 정할 수 없습니다. 적용할 Session이 하나인 씬을 활성화하세요.");
        }
        private static T[] References<T>(UnityEngine.Object owner, string name) where T : UnityEngine.Object
        {
            var array = new SerializedObject(owner).FindProperty(name);
            var result = new T[array.arraySize];
            for (int i = 0; i < result.Length; i++) result[i] = array.GetArrayElementAtIndex(i).objectReferenceValue as T;
            return result;
        }
        private static void References(UnityEngine.Object owner, string name, UnityEngine.Object[] values)
        {
            var serialized = new SerializedObject(owner);
            var array = serialized.FindProperty(name); array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedProperties();
        }
        private static T Reference<T>(UnityEngine.Object owner, string name) where T : UnityEngine.Object
            => new SerializedObject(owner).FindProperty(name).objectReferenceValue as T;
        private static void Id(UnityEngine.Object owner, string field, string value)
        {
            var serialized = new SerializedObject(owner); serialized.FindProperty(field).stringValue = value; serialized.ApplyModifiedProperties();
        }

        internal static MapChart Import(RoomChart chart)
        {
            var session = Session(chart);
            var bindings = References<RoomBinding>(session, "rooms");
            var start = Array.Find(bindings, r => r != null && r.Id == chart.startingRoomId);
            if (start == null) throw new ArgumentException("시작 방 연결이 없습니다.");
            var map = new MapChart { settings = BeatChartCompiler.Import(chart.bpm, chart.Timing, chart.roomLeadTime, chart.enemyLeadTime, chart.moves, chart.enemies),
                roomSize = start.SideLength, originX = start.Center.x, originY = start.Center.y };
            map.settings.startingRoomId = chart.startingRoomId;
            var rooms = new List<MapRoom>(); var groups = new List<MapGroup>(); var enemies = new List<MapEnemy>();
            foreach (var binding in bindings)
            {
                if (binding == null) throw new ArgumentException("빈 방 연결입니다.");
                var note = Array.Find(chart.moves, n => n.destinationId == binding.Id);
                double appearance = binding == start ? 0 : chart.RoomAppearsAt(note);
                string groupId = "group_" + binding.Id;
                groups.Add(new MapGroup { id = groupId, name = "Bundle", appearBeat = map.settings.Beat(appearance) });
                float gx = (binding.Center.x - start.Center.x) / map.roomSize, gy = (binding.Center.y - start.Center.y) / map.roomSize;
                rooms.Add(new MapRoom { id = binding.Id, x = Mathf.RoundToInt(gx), y = Mathf.RoundToInt(gy), groupId = groupId,
                    width = binding.Size.x, height = binding.Size.y,
                    offsetX = (gx - Mathf.Round(gx)) * map.roomSize, offsetY = (gy - Mathf.Round(gy)) * map.roomSize,
                    moveDuration = note.duration, moveEase = note.ease,
                    hitBeat = map.settings.Beat(note.HitTime), frameBeat = map.settings.Beat(note.customAppearance ? note.frameStartTime : appearance), door = note.hasDoor,
                    doorBeat = map.settings.Beat(note.doorTime), doorFrameBeat = map.settings.Beat(note.customAppearance ? note.doorFrameStartTime : appearance) });
            }
            foreach (var note in chart.enemies ?? Array.Empty<EnemyNote>())
            {
                double appearance = note.customAppearance ? note.appearanceTime : Math.Max(0, note.time - chart.enemyLeadTime);
                enemies.Add(new MapEnemy { id = note.id, roomId = note.roomId, direction = note.direction, placement = note.placement, hitBeat = map.settings.Beat(note.time),
                    appearBeat = map.settings.Beat(appearance), frameBeat = map.settings.Beat(note.customAppearance ? note.frameStartTime : appearance) });
            }
            map.rooms = rooms.ToArray(); map.groups = groups.ToArray(); map.enemies = enemies.ToArray();
            map.MigrateAppearance();
            foreach (var camera in session.gameObject.scene.GetRootGameObjects())
                foreach (var component in camera.GetComponentsInChildren<RoomCamera>(true))
                {
                    map.cameraX = component.transform.position.x; map.cameraY = component.transform.position.y;
                    var view = component.GetComponent<Camera>(); if (view != null) map.cameraSize = view.orthographicSize;
                }
            return map;
        }

        internal static CompiledBeatChart Validate(RoomChart chart)
        {
            if (chart.mapDraftMusic == null) throw new ArgumentException("곡 설정 탭에서 음악을 지정하세요.");
            var map = chart.mapDraft;
            var compiled = MapChartCompiler.Compile(map, chart.moveDuration, chart.enemyReadTime, chart.mapDraftMusic.length, chart.judgmentLineWidth);
            if (!(chart.roomFrameStartSize > 0) || float.IsInfinity(chart.roomFrameStartSize))
                throw new ArgumentException("방 판정선 시작 크기는 유한한 양수여야 합니다.");
            foreach (var room in map.rooms)
            {
                if (!(chart.passageWidth > 0 && chart.passageWidth < Mathf.Min(map.Width(room), map.Height(room)) - chart.judgmentLineWidth))
                    throw new ArgumentException("방 크기는 통로보다 충분히 커야 합니다.");
                bool usesDirection = Array.Exists(compiled.Enemies, e => e.roomId == room.id && !e.placement.useCoordinates);
                if (usesDirection && !(chart.aimRadius + .38f + chart.enemyLineWidth / 2 < Mathf.Min(map.Width(room), map.Height(room)) / 2))
                    throw new ArgumentException("8방향 적이 있는 방은 적 배치 반경보다 충분히 커야 합니다.");
            }
            return compiled;
        }

        internal static void SaveDraft(RoomChart chart)
        {
            if (chart == null) throw new ArgumentException("저장할 채보를 선택하세요.");
            if (chart.mapDraft != null && chart.mapDraft.NeedsEnemyRoomSynchronization)
            {
                Undo.RecordObject(chart, "적 소속 방 갱신");
                chart.mapDraft.SynchronizeEnemyRooms();
            }
            if (chart.mapDraft != null && chart.mapDraft.NeedsRoomStartSynchronization)
            {
                Undo.RecordObject(chart, "방과 판정선 시작 박 통일");
                chart.mapDraft.SynchronizeRoomStarts();
            }
            // Persist unfinished authoring independently of gameplay validation or scene bindings.
            EditorUtility.SetDirty(chart);
            AssetDatabase.SaveAssetIfDirty(chart);
        }

        internal static void ApplyToScene(RoomChart chart)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new ArgumentException("Play 종료 후 저장하세요.");
            var compiled = Validate(chart);
            var session = ApplicationSession(chart);
            var scene = session.gameObject.scene;
            if (string.IsNullOrEmpty(scene.path) || !scene.isLoaded) throw new ArgumentException("먼저 대상 씬을 파일로 저장하세요.");
            var oldRooms = References<RoomBinding>(session, "rooms");
            var combat = Reference<RoomCombat>(session, "combat");
            if (combat == null) throw new ArgumentException("Room Combat 연결이 없습니다.");
            combat.ValidateStageTargetOwnership(oldRooms);
            var oldEnemies = References<RoomEnemy>(combat, "enemies");
            var template = Array.Find(oldRooms, r => r != null && r.Door != null && Reference<Transform>(r, "judgmentFrame") != null);
            if (template == null) throw new ArgumentException("씬에 문과 판정선을 갖춘 방 템플릿이 하나 필요합니다.");
            template.ValidateReferences(true);
            EnsureOwnedReferences(template, template.transform);
            EnsureOwnedReferences(template.Door, template.transform);
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/RegularEnemy.prefab");
            var enemyTemplate = enemyPrefab != null ? enemyPrefab.GetComponent<RoomEnemy>() : null;
            if (enemyTemplate == null) throw new ArgumentException("RegularEnemy 프리팹이 필요합니다.");
            enemyTemplate.ValidateReferences();
            EnsureOwnedReferences(enemyTemplate, enemyTemplate.transform);
            foreach (var room in oldRooms)
                if (room == null || room.gameObject.scene != scene) throw new ArgumentException("다른 씬 또는 누락된 방 연결이 있습니다.");
            foreach (var enemy in oldEnemies)
                if (enemy == null || enemy.gameObject.scene != scene) throw new ArgumentException("다른 씬 또는 누락된 적 연결이 있습니다.");
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("시각적 맵 씬 적용");
            try
            {
                Undo.RecordObject(chart, "게임 채보 적용");
                Undo.RecordObject(session, "Room Session 채보 연결");
                var sessionFields = new SerializedObject(session);
                sessionFields.FindProperty("chart").objectReferenceValue = chart;
                sessionFields.ApplyModifiedProperties();
                var map = chart.mapDraft;
                map.SynchronizeRoomStarts();
                map.SynchronizeEnemyRooms();
                chart.music = chart.mapDraftMusic; chart.bpm = (float)map.settings.bpm; chart.startingRoomId = map.settings.startingRoomId;
                chart.moves = compiled.Moves; chart.enemies = compiled.Enemies;
                chart.roomLeadTime = (float)compiled.RoomLeadSeconds; chart.enemyLeadTime = (float)compiled.EnemyLeadSeconds;
                chart.appliedMap = JsonUtility.FromJson<MapChart>(JsonUtility.ToJson(map));
                var newRooms = new List<RoomBinding>(); var newEnemies = new List<RoomEnemy>();
                foreach (var room in map.rooms)
                {
                    var clone = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
                    Undo.RegisterCreatedObjectUndo(clone, "방 배치"); clone.name = map.RoomLabel(room); clone.SetActive(true);
                    // Existing rooms contain their authored enemies. Those are rebuilt separately from the draft.
                    foreach (var embedded in clone.GetComponentsInChildren<RoomEnemy>(true)) Undo.DestroyObjectImmediate(embedded.gameObject);
                    clone.transform.SetPositionAndRotation(new Vector3(map.WorldX(room), map.WorldY(room), 0), Quaternion.identity);
                    var binding = clone.GetComponent<RoomBinding>(); Id(binding, "roomId", room.id);
                    var fields = new SerializedObject(binding); fields.FindProperty("sideLength").floatValue = map.roomSize;
                    fields.FindProperty("dimensions").vector2Value = new Vector2(map.Width(room), map.Height(room)); fields.ApplyModifiedProperties();
                    var route = map.OrderedRooms();
                    int routeIndex = Array.IndexOf(route, room);
                    MoveDirection? exit = routeIndex + 1 < route.Length
                        ? Array.Find(compiled.Moves, n => n.destinationId == route[routeIndex + 1].id).direction : (MoveDirection?)null;
                    MoveNote entranceNote = Array.Find(compiled.Moves, n => n.destinationId == room.id);
                    binding.Configure(chart, exit, routeIndex > 0 ? (MoveDirection)(((int)entranceNote.direction + 2) % 4) : (MoveDirection?)null);
                    binding.Present(true, routeIndex == 0, false, 1, false, 0, 0);
                    var surfaces = References<SpriteRenderer>(binding, "surfaces");
                    if (surfaces.Length > 0) surfaces[0].transform.localScale = new Vector3(map.Width(room), map.Height(room), 1);
                    Reference<GameObject>(binding, "visuals").SetActive(true);
                    Transform frame = Reference<Transform>(binding, "judgmentFrame");
                    frame.gameObject.SetActive(false); Square(References<SpriteRenderer>(binding, "frameEdges"), binding.Size, chart.judgmentLineWidth);
                    binding.RefreshFrameDirections();
                    MoveNote note = Array.Find(compiled.Moves, n => n.destinationId == room.id);
                    Vector2 toward = Direction(note.direction);
                    var door = binding.Door;
                    door.transform.position = binding.Center - (Vector3)(toward * binding.Extent(note.direction) * .5f);
                    if (routeIndex > 0)
                    {
                        var passage = map.PassagePosition(route[routeIndex - 1], room);
                        door.transform.position = new Vector3(passage.x, passage.y, binding.Center.z);
                    }
                    door.transform.rotation = Quaternion.Euler(0, 0, toward.x != 0 ? 90 : 0);
                    door.Present(room.door && room.id != map.settings.startingRoomId, false, 1, chart.doorCloseDuration, note.doorTime, 0, showFrame: false);
                    newRooms.Add(binding);
                }
                foreach (var enemy in map.enemies)
                {
                    var parent = newRooms.Find(r => r.Id == enemy.roomId).transform;
                    var clone = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, parent);
                    Undo.RegisterCreatedObjectUndo(clone, "적 배치"); clone.name = "Enemy - " + map.EnemyLabel(enemy); clone.SetActive(true);
                    var binding = clone.GetComponent<RoomEnemy>(); Id(binding, "enemyId", enemy.id);
                    var room = map.Room(enemy.roomId);
                    binding.Configure(new Vector3(map.WorldX(room), map.WorldY(room), 0), enemy.direction, chart, enemy.placement);
                    binding.Present(true, 1, 0, 0, false);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(clone);
                    foreach (var component in clone.GetComponentsInChildren<Component>(true))
                        if (component != null) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                    newEnemies.Add(binding);
                }
                if (newEnemies.Count == 0 && oldEnemies.Length > 0)
                {
                    bool retainedAlready = Array.Exists(scene.GetRootGameObjects(), r => r.name == "Map Editor Templates" && r.GetComponentInChildren<RoomEnemy>(true) != null);
                    if (!retainedAlready)
                    {
                        var root = new GameObject("Map Editor Templates"); Undo.RegisterCreatedObjectUndo(root, "템플릿 보존");
                        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                        var retained = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, root.transform);
                        Undo.RegisterCreatedObjectUndo(retained, "적 템플릿 보존"); root.SetActive(false);
                    }
                }
                References(session, "rooms", newRooms.ToArray());
                References(combat, "enemies", newEnemies.ToArray());
                var player = Reference<Transform>(session, "player");
                if (player != null)
                {
                    Undo.RecordObject(player, "시작 위치"); var start = map.Room(map.settings.startingRoomId);
                    var route = map.OrderedRooms();
                    var anchor = map.PlayerAnchor(start, route.Length > 1 ? route[1] : null);
                    player.position = new Vector3(anchor.x, anchor.y, player.position.z);
                }
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var camera in root.GetComponentsInChildren<RoomCamera>(true))
                    {
                        if (Reference<Transform>(camera, "player") != player) continue;
                        var fields = new SerializedObject(camera); fields.FindProperty("session").objectReferenceValue = session; fields.ApplyModifiedProperties();
                        var pose = map.CameraAt(map.settings.Beat(0), player.position.x, player.position.y);
                        Undo.RecordObject(camera.transform, "초기 카메라 위치"); camera.transform.position = new Vector3(pose.x, pose.y, camera.transform.position.z);
                        var view = camera.GetComponent<Camera>(); if (view != null) { Undo.RecordObject(view, "초기 카메라 크기"); view.orthographicSize = pose.size; }
                    }
                session.ValidateConfiguration(); // Original objects still exist until the complete replacement is valid.
                foreach (var room in oldRooms) Undo.DestroyObjectImmediate(room.gameObject);
                foreach (var enemy in oldEnemies) if (enemy != null) Undo.DestroyObjectImmediate(enemy.gameObject);
                chart.NotifyChartChanged(); EditorUtility.SetDirty(chart);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("씬 저장에 실패했습니다.");
                AssetDatabase.SaveAssetIfDirty(chart);
                Undo.CollapseUndoOperations(undo);
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }
        internal static Vector2 Direction(MoveDirection direction) => direction == MoveDirection.Up ? Vector2.up : direction == MoveDirection.Down ? Vector2.down : direction == MoveDirection.Left ? Vector2.left : Vector2.right;
        private static void EnsureOwnedReferences(Component component, Transform owner)
        {
            var iterator = new SerializedObject(component).GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                var reference = iterator.objectReferenceValue;
                var transform = reference is GameObject go ? go.transform : reference is Component c ? c.transform : null;
                if (transform != null && !transform.IsChildOf(owner))
                    throw new ArgumentException("템플릿 바깥을 참조합니다: " + component.name + "/" + iterator.propertyPath);
            }
        }
        private static void Square(SpriteRenderer[] edges, Vector2 size, float width)
        {
            for (int i = 0; i < edges.Length; i++)
            {
                var edge = edges[i];
                var p = edge.transform.localPosition;
                bool collapsed = p.sqrMagnitude <= 0.0001f;
                bool horizontal = collapsed ? i % 2 == 0 : Mathf.Abs(p.y) > Mathf.Abs(p.x);
                float side = collapsed ? (i < 2 ? 1 : -1) : horizontal ? Mathf.Sign(p.y) : Mathf.Sign(p.x);
                edge.transform.localPosition = horizontal ? new Vector3(0, side * size.y / 2, 0) : new Vector3(side * size.x / 2, 0, 0);
                edge.transform.localScale = horizontal ? new Vector3(size.x + width, width, 1) : new Vector3(width, size.y + width, 1);
            }
        }
    }
}
