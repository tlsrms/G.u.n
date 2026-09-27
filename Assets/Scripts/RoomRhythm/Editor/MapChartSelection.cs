using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gun.RoomRhythm.Editor
{
    public sealed partial class MapChartWindow
    {
        private readonly HashSet<MapTimelineItem> timelineSelection = new HashSet<MapTimelineItem>();
        private MapTimelineShift selectionDrag;
        private void ClearTimelineSelection()
        {
            timelineSelection.Clear(); selection.Clear();
            selectedEnemy = selectedCamera = selectedShake = -1;
            roomToReveal = null; selectionDrag = null;
            Repaint();
        }

        private void SelectTimelineItem(TimelineSpan span, bool additive)
        {
            if (additive)
            {
                if (!timelineSelection.Add(span.item)) timelineSelection.Remove(span.item);
            }
            else if (!timelineSelection.Contains(span.item))
            { timelineSelection.Clear(); timelineSelection.Add(span.item); }
            if (timelineSelection.Count == 1)
            {
                var selected = Events().Find(item => timelineSelection.Contains(item.item));
                selected?.select();
            }
            else
            {
                selection.Clear(); selectedEnemy = selectedCamera = selectedShake = -1;
                foreach (var item in Events())
                    if (timelineSelection.Contains(item.item) && !string.IsNullOrEmpty(item.roomId) && !selection.Contains(item.roomId))
                        selection.Add(item.roomId);
            }
            pendingRoom = null;
        }
        private bool DrawMultipleSelection()
        {
            if (timelineSelection.Count < 2) return false;
            EditorGUILayout.LabelField("선택한 채보 " + timelineSelection.Count + "개", EditorStyles.boldLabel);
            var shift = new MapTimelineShift(Map, timelineSelection);
            GUI.SetNextControlName("TimelineSelectionStart");
            EditorGUI.BeginChangeCheck();
            double beat = EditorGUILayout.DelayedDoubleField("가장 이른 시작 박", shift.Earliest);
            TrackFocusedInput("TimelineSelectionStart");
            if (EditorGUI.EndChangeCheck())
                Edit("선택 채보 일괄 이동", () => shift.Apply(beat - shift.Earliest, Map.settings.Beat(0), SelectionEndBeat()));
            EditorGUILayout.HelpBox("항목 간 박자 간격과 길이를 유지해 이동합니다. Shift 클릭: 선택 추가/해제 · Ctrl+D: 복제. 방과 문은 시작 박을 공유합니다.", MessageType.Info);
            return true;
        }
        private double SelectionEndBeat() => Map.settings.loopMusic ? double.PositiveInfinity : EndBeat();
        private void DeleteTimelineSelection()
        {
            Edit("선택 채보 삭제", () => {
                var rooms = new HashSet<string>();
                var enemies = new HashSet<MapEnemy>();
                var cameras = new HashSet<MapCameraKey>();
                var shakes = new HashSet<MapShake>();
                foreach (var item in timelineSelection)
                {
                    if (item.Lane == 0) Map.rooms[item.Index].door = false;
                    else if (item.Lane == 1 && Map.rooms[item.Index].id != Map.settings.startingRoomId) rooms.Add(Map.rooms[item.Index].id);
                    else if (item.Lane == 2) enemies.Add(Map.enemies[item.Index]);
                    else if (item.Lane == 3) cameras.Add(Map.cameras[item.Index]);
                    else if (item.Lane == 4) shakes.Add(Map.shakes[item.Index]);
                }
                Map.rooms = Array.FindAll(Map.rooms, room => !rooms.Contains(room.id));
                Map.enemies = Array.FindAll(Map.enemies, enemy => !enemies.Contains(enemy) && !rooms.Contains(enemy.roomId));
                Map.cameras = Array.FindAll(Map.cameras, camera => !cameras.Contains(camera) && !rooms.Contains(camera.roomId));
                Map.shakes = Array.FindAll(Map.shakes, shake => !shakes.Contains(shake) && !rooms.Contains(shake.roomId));
                timelineSelection.Clear(); selection.Clear(); selectedEnemy = selectedCamera = selectedShake = -1;
                CancelInteraction();
            });
        }
        private void HandleTimelineDuplicate()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode != KeyCode.D || !(e.control || e.command)
                || tab != 1 || ReadOnly || timelineSelection.Count == 0 || pendingRoom != null
                || GUIUtility.hotControl != 0 || GUIUtility.keyboardControl != 0 || EditorGUIUtility.editingTextField) return;
            Edit("선택 채보 복제", () => {
                var copies = MapTimelineEditing.Duplicate(Map, timelineSelection);
                timelineSelection.Clear(); timelineSelection.UnionWith(copies);
                selection.Clear(); selectedEnemy = selectedCamera = selectedShake = -1;
                foreach (var span in Events())
                    if (timelineSelection.Contains(span.item) && !string.IsNullOrEmpty(span.roomId) && !selection.Contains(span.roomId))
                        selection.Add(span.roomId);
                if (copies.Count == 1) Events().Find(span => timelineSelection.Contains(span.item))?.select();
            });
            message = "같은 박에 복제했습니다. 선택된 복제본을 드래그하거나 시작 박을 바꿔 배치하세요. 문은 소속 방과 함께 복제됩니다.";
            e.Use();
        }
    }
}
