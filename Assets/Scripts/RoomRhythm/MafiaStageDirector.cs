using System;
using UnityEngine;

namespace Gun.RoomRhythm
{
    // Stage1_Bpm130: guards -> entrance -> AK interception/dodge -> defeat.
    public sealed class MafiaStageDirector : StageDirector
    {
        [Serializable] private struct ShotCue { public double time; public Vector3 target; }
        [SerializeField] private Transform boss, body, rifleArm, muzzle, player;
        [SerializeField] private LineRenderer warning, shot;
        [SerializeField] private RoomFeedback feedback;
        [SerializeField] private ShotCue[] shots;
        [SerializeField] private float bpm = 130;
        [SerializeField] private double entranceBeat = 128, battleBeat = 144, finishBeat = 384;
        [SerializeField] private BossEntranceTimeline entranceTimeline;
        // Set by the validated editor migration; legacy scenes retain section playback until migrated.
        [SerializeField, HideInInspector] private bool singleChart;
        [SerializeField, HideInInspector] private string entranceRoomId;
        public string EntranceRoomId => singleChart ? entranceRoomId : null;
        private bool battleStarted;
        public BossEntranceTimeline EntranceTimeline => entranceTimeline;
        public Transform Boss => boss;
        public Transform RifleArm => rifleArm;
        public float Bpm => bpm;
        public double EntranceBeat => entranceBeat;
        public double BattleBeat => battleBeat;
        private BossPoseSnapshot entrancePose;
        private bool anchorCaptured;
        private Vector3 bodyRest, entranceAnchor;
        private Quaternion armRest;
        private SpriteRenderer[] parts;
        private Color[] colors;
        private int cue;
        private bool requested, defeated;
        private double firedAt = -100, hitAt = -100, defeatedAt;

        private void Awake()
        {
            CapturePose();
        }

        private void CapturePose()
        {
            if (entrancePose != null) return;
            entrancePose = new BossPoseSnapshot(boss);
            bodyRest = body.localPosition; armRest = rifleArm.localRotation;
            parts = boss.GetComponentsInChildren<SpriteRenderer>(true);
            colors = new Color[parts.Length];
            for (int i = 0; i < parts.Length; i++) colors[i] = parts[i].color;
        }

        public override void ResetStage()
        {
            CapturePose();
            if (entranceTimeline != null) entranceTimeline.Validate(battleBeat - entranceBeat);
            entrancePose.Restore();
            anchorCaptured = false;
            battleStarted = false;
            cue = 0; requested = defeated = false; firedAt = hitAt = -100;
            boss.gameObject.SetActive(false); warning.enabled = shot.enabled = false;
            body.localPosition = bodyRest; rifleArm.localRotation = armRest;
            for (int i = 0; i < parts.Length; i++) parts[i].color = colors[i];
        }

        public override void OnChartCompleted()
        {
            if (!singleChart && Session.CurrentSectionId == "initial") { entranceAnchor = player.position; anchorCaptured = true; }
            else { defeated = true; defeatedAt = Session.SongTime; }
        }

        public override void OnAction(RoomActionResult result)
        {
            if (singleChart ? Session.SongTime * bpm / 60 < battleBeat : Session.CurrentSectionId != "mafia") return;
            if (result.Kind == RoomActionKind.EnemyDefeated || result.Kind == RoomActionKind.DoorBroken) hitAt = Session.SongTime;
            if (result.Kind == RoomActionKind.Failed)
            {
                warning.enabled = shot.enabled = false;
                feedback.EnemyExecutionShot(muzzle.position, player.position);
            }
        }

        public override void Tick(double songTime)
        {
            double beat = songTime * bpm / 60;
            if (beat < entranceBeat) return;
            boss.gameObject.SetActive(true);
            if (singleChart ? beat < battleBeat : Session.CurrentSectionId == "initial")
            {
                if (!anchorCaptured) { entranceAnchor = player.position; anchorCaptured = true; }
                if (entranceTimeline != null)
                {
                    entrancePose.Restore(forSampling: true);
                    boss.gameObject.SetActive(true);
                    entranceTimeline.Evaluate(boss, entranceAnchor, beat - entranceBeat);
                }
                else
                {
                    float t = Mathf.SmoothStep(0, 1, (float)((beat - entranceBeat) / 8));
                    boss.position = entranceAnchor + Vector3.up * Mathf.Lerp(9, 2.8f, t);
                    boss.rotation = Quaternion.Euler(0, 0, 180);
                    rifleArm.localRotation = armRest * Quaternion.Euler(0, 0, Mathf.Lerp(-35, 0, t));
                }
                bool entranceEnded = entranceTimeline == null || beat >= entranceBeat + entranceTimeline.durationBeats;
                if (!singleChart && Session.IsChartCompleted && entranceEnded && beat >= battleBeat && !requested)
                {
                    requested = true;
                    if (!QueueSection("mafia")) Debug.LogError("Mafia section could not start.", this);
                }
                return;
            }
            if (singleChart && !battleStarted)
            {
                battleStarted = true;
                OnSectionStarted("mafia"); // Visual hand-off only; the chart, music and judgment model continue unchanged.
            }
            if (defeated)
            {
                warning.enabled = shot.enabled = false;
                float t = Mathf.Clamp01((float)((songTime - defeatedAt) / 2));
                body.localPosition = bodyRest + Vector3.down * (.2f * t);
                boss.rotation = Quaternion.Euler(0, 0, 180 + t * 75);
                for (int i = 0; i < parts.Length; i++)
                { var color = colors[i]; color.a *= 1 - t; parts[i].color = color; }
                if (beat >= finishBeat) CompleteStage();
                return;
            }
            boss.position = player.position + Vector3.up * 2.8f;
            boss.rotation = Quaternion.Euler(0, 0, 180);
            while (cue < shots.Length && shots[cue].time <= songTime)
            {
                shot.SetPosition(0, muzzle.position); shot.SetPosition(1, shots[cue].target);
                firedAt = songTime; cue++;
            }
            float recoil = Mathf.Clamp01(1 - (float)((songTime - firedAt) / .13));
            rifleArm.localRotation = armRest * Quaternion.Euler(0, 0, recoil * -10);
            body.localPosition = bodyRest + Vector3.down * (.05f * recoil);
            shot.enabled = songTime - firedAt < .07;
            bool aiming = cue < shots.Length && shots[cue].time - songTime < .3;
            warning.enabled = aiming;
            if (aiming) { warning.SetPosition(0, muzzle.position); warning.SetPosition(1, shots[cue].target); }
            float flash = Mathf.Clamp01(1 - (float)((songTime - hitAt) / .1));
            for (int i = 0; i < parts.Length; i++) parts[i].color = Color.Lerp(colors[i], Color.white, flash * .6f);
        }

        public override void OnSectionStarted(string id)
        {
            if (id != "mafia" || entranceTimeline == null) return;
            entrancePose.Restore();
            boss.gameObject.SetActive(true);
            boss.position = player.position + Vector3.up * 2.8f;
            boss.rotation = Quaternion.Euler(0, 0, 180);
        }
    }
}
