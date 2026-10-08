using System;
using UnityEngine;

namespace Gun.RoomRhythm
{
    [DisallowMultipleComponent]
    public sealed class MafiaShieldAmbush : MonoBehaviour
    {
        [SerializeField] private RoomSession session;
        [SerializeField] private string escapeRoomId;
        [SerializeField, Min(.25f)] private float leadBeats = 4;
        [SerializeField] private MoveDirection entranceSide = MoveDirection.Up;
        [SerializeField, Min(.1f)] private float entryDistance = 1.3f;
        [SerializeField] private Transform visuals, actors, boss, leftDoor, rightDoor;
        [SerializeField] private AnimationClip enter, idle, burst, withdraw;
        [SerializeField] private Vector3 shotOffsets = new Vector3(.12f, .28f, .44f);
        [SerializeField] private SpriteRenderer muzzleFlash;
        [SerializeField] private SpriteRenderer[] tracers;
        [SerializeField] private AudioClip shotSound;
        [SerializeField, Range(0, 1)] private float volume = .5f;
        private AudioSource sound;
        private Transform muzzle;
        private MafiaAmbushTiming timing;
        private bool configured, failed, targetLocked;
        private int fired;
        private Vector3 aimTarget;
        private readonly Vector3[] shotOrigins = new Vector3[3];
        private readonly Vector3[] shotTargets = new Vector3[3];
        private Pose[] rest;
        private struct Pose { public Transform node; public Vector3 position, scale; public Quaternion rotation; public bool active; }
        public RoomSession Session => session;

        public RoomSession FindSession()
        {
            if (session != null) return session;
            RoomSession found = null;
            if (!gameObject.scene.IsValid()) return null;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<RoomSession>(true))
                { if (found != null) return null; found = candidate; }
            return found;
        }

        public string ConfigurationError(RoomSession owner)
        {
            if (owner == null || owner.Chart == null) return "이 씬의 Game Session을 연결하세요.";
            if (owner.Player == null) return "Game Session의 플레이어 연결이 없습니다.";
            if (visuals == null || actors == null || boss == null || leftDoor == null || rightDoor == null
                || muzzleFlash == null || tracers == null || tracers.Length != 3 || Array.Exists(tracers, t => t == null))
                return "프리팹의 문·배우·효과 연결이 누락되었습니다.";
            if (enter == null || idle == null || burst == null || withdraw == null || idle.length <= 0)
                return "등장·대기·난사·퇴장 모션 클립을 연결하세요.";
            if (!(shotOffsets.x > 0 && shotOffsets.y > shotOffsets.x && shotOffsets.z > shotOffsets.y && shotOffsets.z < burst.length))
                return "발사 시각은 난사 클립 길이 안에서 첫 발 < 두 번째 < 세 번째 순서여야 합니다.";
            try { Resolve(owner); return null; }
            catch (ArgumentException error) { return error.Message; }
        }

        private MafiaAmbushTiming Resolve(RoomSession owner) => new MafiaAmbushTiming(owner.Chart.moves,
            escapeRoomId, owner.Chart.bpm, owner.Chart.moveDuration, leadBeats, enter.length, burst.length, withdraw.length, shotOffsets.x);

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            session = FindSession();
            if (session == null) { Debug.LogWarning("고기방패 연출: Game Session 연결이 필요합니다.", this); Hide(); return; }
            Capture();
            session.RunReset += ResetPresentation;
            session.ActionPresented += OnAction;
            ResetPresentation();
        }

        private void Capture()
        {
            if (rest != null || boss == null) return;
            var nodes = boss.GetComponentsInChildren<Transform>(true);
            rest = new Pose[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                rest[i] = new Pose { node = node, position = node.localPosition, rotation = node.localRotation,
                    scale = node.localScale, active = node.gameObject.activeSelf };
                if (node.name == "Muzzle") muzzle = node;
            }
            foreach (var animator in boss.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false; sound.spatialBlend = 0;
        }

        private void RestorePose()
        {
            if (rest == null) return;
            foreach (var pose in rest)
            {
                pose.node.localPosition = pose.position; pose.node.localRotation = pose.rotation;
                pose.node.localScale = pose.scale; pose.node.gameObject.SetActive(pose.active);
            }
        }

        private void ResetPresentation()
        {
            configured = failed = targetLocked = false; fired = 0;
            RestorePose(); Hide();
            if (sound != null) sound.Stop();
            if (actors != null) { actors.localPosition = Vector3.zero; actors.localRotation = Quaternion.identity; }
            if (leftDoor != null) leftDoor.localPosition = new Vector3(-.65f, 0, 0);
            if (rightDoor != null) rightDoor.localPosition = new Vector3(.65f, 0, 0);
            string error = ConfigurationError(session);
            if (error != null) { Debug.LogWarning("고기방패 연출: " + error, this); return; }
            if (muzzle == null) { Debug.LogWarning("고기방패 연출: 보스 총구 Muzzle이 없습니다.", this); return; }
            timing = Resolve(session); configured = true;
        }

        private void OnAction(RoomActionResult result)
        {
            if (!configured) return;
            if (result.Kind == RoomActionKind.Failed)
            { failed = true; HideEffects(); if (sound != null) sound.Stop(); }
            if (result.Kind == RoomActionKind.MoveStarted && result.TargetId == escapeRoomId)
            {
                // Aim into the vacated room, never chase the player into the safe room.
                aimTarget = session.Player.position; targetLocked = true;
            }
        }

        private void LateUpdate()
        {
            if (!configured || failed || session == null || session.Phase == RunPhase.Dead) return;
            if (!session.isActiveAndEnabled) { Hide(); if (sound != null) sound.Stop(); return; }
            if (session.Phase == RunPhase.Ready || session.IsCleared) { Hide(); return; }
            double time = session.SongTime;
            if (!timing.Visible(time, session.CompletedMoves)) { Hide(); return; }
            visuals.gameObject.SetActive(true);
            // Prefab origin is the door centre; local +Y points into the room.
            visuals.localRotation = Quaternion.Euler(0, 0, 180 + (int)entranceSide * 90);
            if (!targetLocked) aimTarget = session.Player.position;
            PoseAt(time);
            while (fired < 3 && time >= timing.Shot(shotOffsets[fired]))
            {
                double shotTime = timing.Shot(shotOffsets[fired]);
                PoseAt(shotTime);
                shotOrigins[fired] = muzzle.position;
                shotTargets[fired] = aimTarget;
                // Consume crossed shots, but do not replay stale audio after a long frame.
                if (time - shotTime < .12 && shotSound != null) sound.PlayOneShot(shotSound, volume);
                fired++;
            }
            PoseAt(time); PresentEffects(time);
        }

        private void PoseAt(double time)
        {
            float opening = Mathf.SmoothStep(0, 1, (float)((time - timing.Open) / .22));
            float retreat = Mathf.SmoothStep(0, 1, (float)((time - timing.Withdraw) / withdraw.length));
            float doorOpen = opening * (1 - retreat);
            leftDoor.localPosition = new Vector3(-.65f - 1.05f * doorOpen, 0, 0);
            rightDoor.localPosition = new Vector3(.65f + 1.05f * doorOpen, 0, 0);
            bool appearing = time >= timing.Open && time < timing.End;
            actors.gameObject.SetActive(appearing);
            if (!appearing) return;
            float advance = Mathf.SmoothStep(0, 1, (float)((time - timing.Open) / enter.length));
            actors.localPosition = new Vector3(0, Mathf.Lerp(-.45f, entryDistance, advance * (1 - retreat)), 0);
            Vector3 delta = aimTarget - actors.position;
            if (delta.sqrMagnitude > .001f) actors.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90);
            RestorePose();
            if (time < timing.Open + enter.length) enter.SampleAnimation(boss.gameObject, (float)(time - timing.Open));
            else if (time < timing.Burst) idle.SampleAnimation(boss.gameObject, Mathf.Repeat((float)(time - timing.Open - enter.length), idle.length));
            else if (time < timing.Withdraw) burst.SampleAnimation(boss.gameObject, (float)(time - timing.Burst));
            else withdraw.SampleAnimation(boss.gameObject, Mathf.Clamp((float)(time - timing.Withdraw), 0, withdraw.length));
        }

        private void PresentEffects(double time)
        {
            muzzleFlash.enabled = false;
            for (int i = 0; i < 3; i++)
            {
                double age = time - timing.Shot(shotOffsets[i]);
                bool visible = i < fired && age >= 0 && age < .22;
                var tracer = tracers[i]; tracer.enabled = visible;
                if (!visible) continue;
                Vector3 delta = shotTargets[i] - shotOrigins[i];
                Vector3 tip = shotOrigins[i] + delta * Mathf.Clamp01((float)age / .12f);
                tracer.transform.position = tip;
                tracer.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                tracer.color = new Color(1, .82f, .4f, 1 - Mathf.Clamp01((float)age / .22f));
                if (age < .065)
                {
                    muzzleFlash.enabled = true; muzzleFlash.transform.position = shotOrigins[i];
                    muzzleFlash.transform.rotation = Quaternion.Euler(0, 0, (float)age * 900);
                }
            }
        }

        private void HideEffects()
        {
            if (muzzleFlash != null) muzzleFlash.enabled = false;
            if (tracers != null) foreach (var tracer in tracers) if (tracer != null) tracer.enabled = false;
        }
        private void Hide() { if (visuals != null) visuals.gameObject.SetActive(false); HideEffects(); }
        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            if (session != null) { session.RunReset -= ResetPresentation; session.ActionPresented -= OnAction; }
            configured = false; Hide(); if (sound != null) sound.Stop();
        }
    }
}
