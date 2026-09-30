using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gun.RoomRhythm
{
    [DisallowMultipleComponent]
    public sealed class StageSelectScene : MonoBehaviour
    {
        private struct AuthoredPose
        {
            public Transform parent;
            public Vector3 position, scale;
            public Quaternion rotation;
            public int sibling;
            public AuthoredPose(Transform target)
            {
                parent = target.parent;
                position = target.localPosition;
                scale = target.localScale;
                rotation = target.localRotation;
                sibling = target.GetSiblingIndex();
            }
            public void Restore(Transform target)
            {
                target.SetParent(parent, false);
                target.localPosition = position;
                target.localScale = scale;
                target.localRotation = rotation;
                target.SetSiblingIndex(sibling);
            }
        }

        [Serializable]
        private sealed class Stage
        {
            public RoomChart chart;
            public string sceneName;
            public Button button;
            public Graphic label;
            public RectTransform sleeve, record;
            [NonSerialized] public AuthoredPose sleeveHome, recordHome;
            public void RememberSlot()
            {
                sleeveHome = new AuthoredPose(sleeve);
                recordHome = new AuthoredPose(record);
            }
            public void Restore()
            {
                sleeveHome.Restore(sleeve);
                recordHome.Restore(record);
                record.gameObject.SetActive(false);
            }
        }

        private enum Phase { Arriving, Choosing, Placing, Playing, Returning }
        [SerializeField] private SafeRoomController room;
        [SerializeField] private Stage[] stages;
        [SerializeField] private CanvasGroup layout, details;
        [SerializeField] private Text title, duration, bestAccuracy;
        [SerializeField] private Button startButton;
        [SerializeField] private RectTransform station, turntable, rack, caseDisplay, recordTransport, recordDock, toneArm;
        [SerializeField] private AudioSource music;
        [SerializeField] private Vector2 turntableSelectionPosition = new Vector2(-360, 45);
        [SerializeField] private Vector2 rackSelectionPosition = new Vector2(425, 310);
        [SerializeField, Min(.1f)] private float turntableSelectionScale = 1.52f;
        [SerializeField, Min(0)] private float revealDelay = 1.6f;
        [SerializeField, Min(.1f)] private float placementDuration = .7f;
        private Phase phase;
        private int selected, displayed = -1;
        private bool initialized, spinning;
        private double musicStartedAt;
        private AuthoredPose stationHome, turntableHome, rackHome;
        private const float ParkedArmAngle = 28;
        private static readonly Vector3 ExtractedPosition = new Vector3(-350, 0, 0);

        private void Awake()
        {
            if (!HasReferences())
            {
                Debug.LogError("Turntable scene has missing authored references.", this);
                enabled = false;
                return;
            }
            stationHome = new AuthoredPose(station);
            turntableHome = new AuthoredPose(turntable);
            rackHome = new AuthoredPose(rack);
            foreach (Stage stage in stages) stage.RememberSlot();
            initialized = true;
        }

        private void OnEnable()
        {
            if (!initialized) return;
            StageSelection.IsRecordRun = false;
            StageSelection.ReturnScene = gameObject.scene.name;
            room.MovementEnabled = false;
            room.CloseExit();
            stationHome.Restore(station);
            turntableHome.Restore(turntable);
            rackHome.Restore(rack);
            layout.alpha = details.alpha = 0;
            SetInput(false);
            music.playOnAwake = false;
            music.Stop();
            phase = Phase.Arriving;
            spinning = false;
            displayed = -1;
            toneArm.localRotation = Quaternion.Euler(0, 0, ParkedArmAngle);
            foreach (Stage stage in stages) stage.Restore();
            Highlight(Mathf.Clamp(StageSelection.LastIndex, 0, stages.Length - 1));
            StartCoroutine(Arrive());
        }

        private bool HasReferences()
        {
            if (room == null || layout == null || details == null || title == null || duration == null
                || bestAccuracy == null || startButton == null || station == null || turntable == null
                || rack == null || caseDisplay == null || recordTransport == null || recordDock == null
                || toneArm == null || music == null || stages == null || stages.Length == 0) return false;
            foreach (Stage stage in stages)
                if (stage == null || stage.button == null || stage.label == null || stage.record == null || stage.sleeve == null) return false;
            return true;
        }

        private IEnumerator Arrive()
        {
            yield return new WaitForSecondsRealtime(revealDelay);
            yield return FocusStation(true);
            BeginChoosing();
        }

        private void Update()
        {
            if (room.IsExiting) return;
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            if (phase == Phase.Choosing)
            {
                bool previous = keyboard != null && (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                    || pad != null && pad.dpad.left.wasPressedThisFrame;
                bool next = keyboard != null && (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                    || pad != null && pad.dpad.right.wasPressedThisFrame;
                if (previous) SelectStage(selected - 1);
                if (next) SelectStage(selected + 1);
                bool confirm = keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
                    || pad != null && pad.buttonSouth.wasPressedThisFrame;
                if (confirm)
                {
                    if (displayed < 0) SelectStage(selected);
                    else StartSelectedStage();
                }
            }
            else if (phase == Phase.Playing && room.NearTurntable)
            {
                bool reopen = keyboard != null && keyboard.eKey.wasPressedThisFrame
                    || pad != null && pad.buttonNorth.wasPressedThisFrame;
                if (reopen) ShowRecords();
            }
            if (spinning)
            {
                float angle = (float)((AudioSettings.dspTime - musicStartedAt) * -200) % 360;
                stages[selected].record.localRotation = Quaternion.Euler(0, 0, angle);
            }
        }

        private void SetInput(bool interactable)
        {
            foreach (Stage stage in stages) stage.button.interactable = interactable;
            layout.interactable = layout.blocksRaycasts = interactable;
            details.interactable = details.blocksRaycasts = interactable && displayed >= 0;
            startButton.interactable = interactable && displayed >= 0 && CanStart(stages[displayed]);
        }

        private static bool CanStart(Stage stage) => stage.chart != null && stage.chart.music != null
            && !string.IsNullOrWhiteSpace(stage.sceneName) && Application.CanStreamedLevelBeLoaded(stage.sceneName);

        private void BeginChoosing()
        {
            phase = Phase.Choosing;
            SetInput(true);
        }

        private void Highlight(int index)
        {
            selected = (index % stages.Length + stages.Length) % stages.Length;
            StageSelection.LastIndex = selected;
            for (int i = 0; i < stages.Length; i++)
            {
                float value = i == selected ? .92f : .4f;
                stages[i].label.color = new Color(value, value, value, 1);
            }
        }

        // Picking a case only reveals its cover and information. Starting is a separate action.
        public void SelectStage(int index)
        {
            if (phase != Phase.Choosing) return;
            if (displayed >= 0) stages[displayed].sleeveHome.Restore(stages[displayed].sleeve);
            Highlight(index);
            ShowCase(selected);
            SetInput(true);
        }

        private void ShowCase(int index)
        {
            Stage stage = stages[index];
            displayed = index;
            stage.sleeve.SetParent(caseDisplay, false);
            stage.sleeve.localPosition = Vector3.zero;
            stage.sleeve.localRotation = Quaternion.identity;
            stage.sleeve.localScale = Vector3.one;
            details.alpha = 1;
            title.text = stage.chart != null ? stage.chart.SongTitle : "NO SIGNAL";
            int seconds = stage.chart != null && stage.chart.music != null ? Mathf.CeilToInt(stage.chart.music.length) : 0;
            duration.text = seconds > 0 ? $"{seconds / 60}:{seconds % 60:00}" : "--:--";
            bestAccuracy.text = StageRecordStore.TryGetBest(stage.sceneName, out float accuracy)
                ? $"BEST {accuracy:0.00}%" : "BEST —";
        }

        public void StartSelectedStage()
        {
            if (phase != Phase.Choosing || displayed < 0) return;
            Stage stage = stages[displayed];
            if (!CanStart(stage)) return;
            phase = Phase.Placing;
            SetInput(false);
            StartCoroutine(PlaceRecord(stage));
        }

        private IEnumerator SlideOutOfCase(Stage stage, bool outward)
        {
            RectTransform record = stage.record;
            if (outward)
            {
                stage.recordHome.Restore(record);
                record.gameObject.SetActive(true);
            }
            else
            {
                record.SetParent(stage.sleeve, false);
                record.SetSiblingIndex(stage.recordHome.sibling);
                record.localScale = stage.recordHome.scale;
                record.localRotation = stage.recordHome.rotation;
            }
            Vector3 from = outward ? stage.recordHome.position : ExtractedPosition;
            Vector3 to = outward ? ExtractedPosition : stage.recordHome.position;
            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < .6f)
            {
                float t = Mathf.SmoothStep(0, 1, (Time.unscaledTime - started) / .6f);
                record.localPosition = Vector3.Lerp(from, to, t);
                yield return null;
            }
            record.localPosition = to;
            if (!outward) record.gameObject.SetActive(false);
        }

        private IEnumerator MoveRecord(Stage stage, bool toPlatter)
        {
            RectTransform record = stage.record;
            record.SetParent(recordTransport, true);
            Vector3 from = record.localPosition;
            Vector3 target = recordTransport.InverseTransformPoint(toPlatter ? recordDock.position : stage.sleeve.TransformPoint(ExtractedPosition));
            Vector3 fromScale = record.localScale;
            Vector3 worldScale = Vector3.Scale(stage.sleeve.lossyScale, stage.recordHome.scale);
            Vector3 parentScale = recordTransport.lossyScale;
            Vector3 targetScale = toPlatter ? Vector3.one
                : new Vector3(worldScale.x / parentScale.x, worldScale.y / parentScale.y, worldScale.z / parentScale.z);
            Quaternion fromRotation = record.localRotation;
            Vector3 lift = (from + target) * .5f + Vector3.up * 65;
            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < placementDuration)
            {
                float t = Mathf.SmoothStep(0, 1, (Time.unscaledTime - started) / placementDuration);
                record.localPosition = (1 - t) * (1 - t) * from + 2 * (1 - t) * t * lift + t * t * target;
                record.localScale = Vector3.Lerp(fromScale, targetScale, t);
                record.localRotation = Quaternion.Slerp(fromRotation, Quaternion.identity, t);
                yield return null;
            }
            record.localPosition = target;
            record.localScale = targetScale;
            record.localRotation = Quaternion.identity;
        }

        private IEnumerator MoveArm(float target)
        {
            Quaternion from = toneArm.localRotation, to = Quaternion.Euler(0, 0, target);
            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < .35f)
            {
                float t = Mathf.SmoothStep(0, 1, (Time.unscaledTime - started) / .35f);
                toneArm.localRotation = Quaternion.Slerp(from, to, t);
                yield return null;
            }
            toneArm.localRotation = to;
        }

        private IEnumerator PlaceRecord(Stage stage)
        {
            yield return SlideOutOfCase(stage, true);
            yield return MoveRecord(stage, true);
            yield return MoveArm(0);
            AudioClip clip = stage.chart.music;
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            float timeout = Time.unscaledTime + 10;
            while (clip.loadState == AudioDataLoadState.Loading && Time.unscaledTime < timeout) yield return null;
            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                title.text = "NO SIGNAL";
                yield return MoveArm(ParkedArmAngle);
                yield return MoveRecord(stage, false);
                yield return SlideOutOfCase(stage, false);
                BeginChoosing();
                yield break;
            }
            music.clip = clip;
            music.loop = true;
            musicStartedAt = AudioSettings.dspTime + .1;
            music.PlayScheduled(musicStartedAt);
            while (AudioSettings.dspTime < musicStartedAt) yield return null;
            spinning = true;
            yield return new WaitForSecondsRealtime(.8f);
            yield return FocusStation(false);
            stage.sleeveHome.Restore(stage.sleeve);
            details.alpha = 0;
            displayed = -1;
            room.SetExit(stage.sceneName, true);
            phase = Phase.Playing;
            room.MovementEnabled = true;
        }

        public void ShowRecords()
        {
            if (phase != Phase.Playing || !room.NearTurntable || room.IsExiting) return;
            room.MovementEnabled = false;
            room.CloseExit();
            music.Stop();
            spinning = false;
            phase = Phase.Returning;
            StartCoroutine(ReturnRecord());
        }

        private IEnumerator ReturnRecord()
        {
            yield return FocusStation(true);
            ShowCase(selected);
            yield return MoveArm(ParkedArmAngle);
            yield return MoveRecord(stages[selected], false);
            yield return SlideOutOfCase(stages[selected], false);
            BeginChoosing();
        }

        // Move the equipment independently of the backdrop; never shrink it across the room.
        private IEnumerator FocusStation(bool closeView)
        {
            Vector3 raised = Vector3.up * 1150;
            Vector3 detailsHome = details.transform.localPosition;
            if (closeView)
            {
                PlaceStationBehindLayout();
                yield return FadeLayout(1);
                station.localScale = Vector3.one;
                station.localPosition = raised;
                turntable.localPosition = turntableSelectionPosition;
                turntable.localScale = Vector3.one * turntableSelectionScale;
                rack.localPosition = rackSelectionPosition;
                rack.localScale = Vector3.one;
                station.SetSiblingIndex(layout.transform.GetSiblingIndex() + 1);
            }
            Vector3 from = station.localPosition;
            Vector3 to = closeView ? Vector3.zero : raised;
            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < .5f)
            {
                float t = Mathf.SmoothStep(0, 1, (Time.unscaledTime - started) / .5f);
                station.localPosition = Vector3.Lerp(from, to, t);
                if (!closeView)
                {
                    details.transform.localPosition = detailsHome + raised * t;
                    details.alpha = 1 - t;
                }
                yield return null;
            }
            station.localPosition = to;
            if (!closeView)
            {
                // Restore the small room objects behind the opaque panel before revealing the room.
                if (displayed >= 0) stages[displayed].sleeveHome.Restore(stages[displayed].sleeve);
                stationHome.Restore(station);
                turntableHome.Restore(turntable);
                rackHome.Restore(rack);
                PlaceStationBehindLayout();
                details.alpha = 0;
                details.transform.localPosition = detailsHome;
                yield return FadeLayout(0);
            }
        }

        private void PlaceStationBehindLayout()
        {
            int panelIndex = layout.transform.GetSiblingIndex();
            if (station.GetSiblingIndex() < panelIndex) panelIndex--;
            station.SetSiblingIndex(panelIndex);
        }

        private IEnumerator FadeLayout(float target)
        {
            float from = layout.alpha, started = Time.unscaledTime;
            while (Time.unscaledTime - started < .4f)
            {
                float t = Mathf.SmoothStep(0, 1, (Time.unscaledTime - started) / .4f);
                layout.alpha = Mathf.Lerp(from, target, t);
                yield return null;
            }
            layout.alpha = target;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (music != null) music.Stop();
        }
    }
}
