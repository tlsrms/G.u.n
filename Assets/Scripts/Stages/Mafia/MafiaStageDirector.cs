using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gun.RoomRhythm
{
    // One chart, one song clock. The window still uses normal Door/Move judgments.
    public sealed class MafiaStageDirector : StageDirector
    {
        [Header("채보 연결 (방 ID)")]
        [SerializeField] private string corridorRoomId = "room_fe5599bc";
        [SerializeField] private string officeRoomId = "room_5b48893d";
        [SerializeField] private string escapeRoomId = "mafia_office_escape";
        [Header("씬 연결")]
        [SerializeField] private Transform boss, muzzle, player;
        [SerializeField] private RoomFeedback feedback;
        [SerializeField] private RoomCombat combat;
        [SerializeField] private RoomCamera stageCamera;
        [SerializeField] private MafiaOfficeSet officeSet;
        [Header("직접 편집할 모션")]
        [SerializeField] private AnimationClip seatedSmoke, drawAK, aimIdle, fireBurst;
        [Header("연출 강도")]
        [SerializeField, Range(0, .18f)] private float letterboxHeight = .1f;
        [SerializeField, Min(1)] private float officeCameraSize = 4.8f;
        [SerializeField, Min(1)] private float bulletCameraSize = 3.8f;
        [SerializeField, Range(0, .25f)] private float cameraKick = .065f;
        private struct Pose { public Transform node; public Vector3 position, scale; public Quaternion rotation; public bool active; }
        private Pose[] rest;
        private readonly List<SpriteRenderer> corridorPortals = new List<SpriteRenderer>();
        private Transform smokeOrigin;
        private RoomDoor windowDoor;
        private MafiaIntroTiming timing;
        private Vector3 bossPosition, playerAnchor, officeCenter;
        private bool configured, entered, failed, escaped, completed;
        private double actualEscape = double.NaN, actualEnd = double.NaN;
        private float bars;
        private int fired;

        private void CapturePose()
        {
            if (rest != null || boss == null) return;
            var nodes = boss.GetComponentsInChildren<Transform>(true);
            rest = new Pose[nodes.Length];
            for (int i = 0; i < nodes.Length; i++) rest[i] = new Pose {
                node = nodes[i], position = nodes[i].localPosition, rotation = nodes[i].localRotation, scale = nodes[i].localScale,
                active = nodes[i].gameObject.activeSelf };
            smokeOrigin = boss.Find("Motion/Body/Neck/Mouth/CigarettePivot/AshPivot/SmokeOrigin");
        }
        public override void ResetStage()
        {
            CapturePose();
            configured = entered = failed = escaped = completed = false;
            fired = 0; bars = 0; actualEscape = actualEnd = double.NaN;
            if (stageCamera != null) stageCamera.ReleaseStageFraming();
            if (rest != null) foreach (var pose in rest)
            { pose.node.localPosition = pose.position; pose.node.localRotation = pose.rotation; pose.node.localScale = pose.scale; pose.node.gameObject.SetActive(pose.active); }
            if (boss != null) boss.gameObject.SetActive(false);
            if (officeSet != null) officeSet.ResetSet();
            if (windowDoor != null) windowDoor.SetExternalPanels(false);
            windowDoor = null;
            foreach (var portal in corridorPortals) if (portal != null) { portal.enabled = false; Destroy(portal.gameObject); }
            corridorPortals.Clear();
            var chart = Session.Chart;
            var map = chart.appliedMap;
            var office = map?.Room(officeRoomId); var exit = map?.Room(escapeRoomId);
            int entrance = Array.FindIndex(chart.moves, n => n.destinationId == officeRoomId);
            int escape = Array.FindIndex(chart.moves, n => n.destinationId == escapeRoomId);
            if (office == null || exit == null || entrance < 0 || escape != entrance + 1)
            { Debug.LogWarning("마피아 연출: 적용한 맵의 사무실/탈출 방 ID를 확인하세요.", this); return; }
            if (boss == null || muzzle == null || player == null || feedback == null || combat == null || stageCamera == null
                || officeSet == null || seatedSmoke == null || drawAK == null || aimIdle == null || fireBurst == null)
                throw new InvalidOperationException("마피아 사무실 연출의 씬/클립 연결이 누락되었습니다.");
            MoveNote entryNote = chart.moves[entrance], exitNote = chart.moves[escape];
            if (!exitNote.hasDoor || exitNote.direction != MoveDirection.Up)
                throw new InvalidOperationException("사무실 탈출에는 위쪽 문 사격과 이동 채보가 필요합니다.");
            timing = new MafiaIntroTiming(entryNote.HitTime + entryNote.Duration(chart.moveDuration), exitNote.doorTime,
                exitNote.HitTime, exitNote.HitTime + exitNote.Duration(chart.moveDuration), 60.0 / chart.bpm);
            officeCenter = new Vector3(map.WorldX(office), map.WorldY(office), 0);
            bossPosition = officeCenter + Vector3.right * (map.Width(office) * .25f);
            var anchor = map.PlayerAnchor(office, exit);
            playerAnchor = new Vector3(anchor.x, anchor.y, 0);
            var passage = map.PassagePosition(office, exit);
            officeSet.Prepare(feedback, bossPosition, new Vector3(passage.x, passage.y, 0));
            // Map reapplication replaces rooms: bind by stable IDs on each run.
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var room in root.GetComponentsInChildren<RoomBinding>(true))
                    if (room.Id == escapeRoomId) windowDoor = room.Door;
            if (windowDoor == null) throw new InvalidOperationException("탈출 창문의 기본 문 연결이 없습니다.");
            windowDoor.SetExternalPanels(true);
            var corridor = map.Room(corridorRoomId);
            if (corridor != null)
            {
                var center = new Vector3(map.WorldX(corridor), map.WorldY(corridor), 0);
                combat.SetCorridorEntrances(corridorRoomId, center, map.Height(corridor));
                foreach (var note in chart.enemies)
                {
                    if (note.roomId != corridorRoomId) continue;
                    var offset = note.placement.Offset(note.direction, chart.aimRadius);
                    var portal = feedback.CreateVisual(transform, "Corridor alcove " + note.id, new Color(.035f, .04f, .045f));
                    portal.sortingOrder = 12;
                    portal.transform.position = center + new Vector3((float)offset.x, Mathf.Sign((float)offset.y) * map.Height(corridor) * .5f, 0);
                    RoomFeedback.Size(portal, new Vector2(1.1f, .9f));
                    portal.enabled = false; corridorPortals.Add(portal);
                }
            }
            configured = true;
        }
        public override void OnAction(RoomActionResult result)
        {
            if (!configured) return;
            if (result.Kind == RoomActionKind.Failed)
            {
                failed = true; bars = 0;
                stageCamera.ReleaseStageFraming(); officeSet.HideEffects();
                return;
            }
            if (result.Kind == RoomActionKind.MoveArrived && result.TargetId == officeRoomId) entered = true;
            if (result.Kind == RoomActionKind.DoorBroken && result.TargetId == escapeRoomId) officeSet.BreakWindow();
            if (result.Kind == RoomActionKind.MoveStarted && result.TargetId == escapeRoomId) actualEscape = Session.SongTime;
            if (result.Kind == RoomActionKind.MoveArrived && result.TargetId == escapeRoomId)
            { escaped = true; actualEnd = Session.SongTime; }
        }
        public override void OnChartCompleted() => completed = true;
        public override void Tick(double songTime)
        {
            if (!configured) { if (completed) CompleteStage(); return; }
            if (failed) return;
            var corridor = Session.Chart.appliedMap.Room(corridorRoomId);
            double corridorStart = corridor == null ? 0 : Session.Chart.appliedMap.settings.Seconds(corridor.appearBeat);
            foreach (var portal in corridorPortals) portal.enabled = songTime >= corridorStart && !entered;
            if (!entered) return;
            float exitBlend = escaped ? Mathf.Clamp01((float)((songTime - actualEnd) / .3)) : 0;
            if (exitBlend >= 1)
            {
                bars = 0; stageCamera.ReleaseStageFraming(); boss.gameObject.SetActive(false); officeSet.gameObject.SetActive(false);
                if (completed) CompleteStage();
                return;
            }
            boss.gameObject.SetActive(true); officeSet.gameObject.SetActive(true);
            float entryBlend = Mathf.SmoothStep(0, 1, (float)((songTime - timing.Start) / .7));
            bars = letterboxHeight * entryBlend * (1 - exitBlend);
            boss.position = bossPosition;
            float facing = Mathf.Atan2(playerAnchor.y - bossPosition.y, playerAnchor.x - bossPosition.x) * Mathf.Rad2Deg - 90;
            float turn = Mathf.SmoothStep(0, 1, (float)((songTime - timing.Start - timing.BeatSeconds * 2) / (timing.BeatSeconds * 2)));
            boss.rotation = Quaternion.Euler(0, 0, facing + Mathf.Lerp(-24, 0, turn));
            SampleMotion(songTime);
            while (fired < 3 && songTime >= timing.ShotTime(fired))
            {
                SampleMotion(timing.ShotTime(fired));
                officeSet.Fire(fired, muzzle.position, playerAnchor); fired++;
            }
            SampleMotion(songTime);
            officeSet.Present(songTime, timing, actualEscape, smokeOrigin != null ? smokeOrigin.position : boss.position,
                songTime < timing.BurstStart - drawAK.length - .18);
            float focus = Mathf.SmoothStep(0, 1, (float)((songTime - timing.ShotTime(0) - .12) / .35));
            Vector3 focusCenter = Vector3.Lerp(officeCenter, Vector3.Lerp(playerAnchor, officeCenter, .35f) + Vector3.up * .7f, focus);
            float kick = 0;
            for (int i = 0; i < fired; i++)
            { float age = (float)(songTime - timing.ShotTime(i)); if (age < .14f) kick += Mathf.Sin(age * 90) * (1 - age / .14f) * cameraKick; }
            focusCenter += new Vector3(kick, -kick * .4f, 0);
            // Hand framing back during the upward leap, so the player never leaves the zoomed view.
            float leap = double.IsNaN(actualEscape) ? 0 : Mathf.Clamp01((float)((songTime - actualEscape) / (timing.End - timing.Escape)));
            float cameraBlend = entryBlend * (1 - exitBlend) * (1 - Mathf.SmoothStep(0, 1, leap));
            stageCamera.SetStageFraming(focusCenter, Mathf.Lerp(officeCameraSize, bulletCameraSize, focus), cameraBlend);
        }
        private void SampleMotion(double time)
        {
            // Editable visibility keys belong to one clip; skipped frames must not carry hidden limbs into the next clip.
            foreach (var pose in rest) if (pose.node != boss) pose.node.gameObject.SetActive(pose.active);
            double drawStart = timing.BurstStart - drawAK.length - .18;
            if (time < drawStart) seatedSmoke.SampleAnimation(boss.gameObject, Mathf.Repeat((float)(time - timing.Start), seatedSmoke.length));
            else if (time < timing.BurstStart) drawAK.SampleAnimation(boss.gameObject, Mathf.Clamp((float)(time - drawStart), 0, drawAK.length));
            else if (time <= timing.BurstStart + fireBurst.length) fireBurst.SampleAnimation(boss.gameObject, (float)(time - timing.BurstStart));
            else aimIdle.SampleAnimation(boss.gameObject, Mathf.Repeat((float)(time - timing.BurstStart - fireBurst.length), aimIdle.length));
        }
        private void OnGUI()
        {
            if (bars <= 0 || Event.current.type != EventType.Repaint) return;
            Color previous = GUI.color; GUI.color = Color.black;
            float height = Screen.height * bars;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, Screen.height - height, Screen.width, height), Texture2D.whiteTexture);
            GUI.color = previous;
        }
        private void OnDisable()
        { bars = 0; if (stageCamera != null) stageCamera.ReleaseStageFraming(); }
    }
}
