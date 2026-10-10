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
        private enum SelectionPhase { Transition, Selecting, Room }

        [Serializable]
        private sealed class Stage
        {
            public RoomChart chart;
            public string sceneName;
        }

        [SerializeField] private SafeRoomController room;
        [SerializeField] private Stage[] stages;
        // Keep the map station; only its old rack and case presentation are replaced.
        [SerializeField] private CanvasGroup layout, details;
        [SerializeField] private Text title;
        [SerializeField] private RectTransform station, rack, caseDisplay, toneArm;
        [SerializeField] private AudioSource music;
        [SerializeField, Min(0)] private float revealDelay = 1.6f;
        [Header("Debug")]
        [Tooltip("START로 고른 스테이지를 출구로 입장할 때 무적 자동 진행하며 최고 기록은 저장하지 않습니다.")]
        [SerializeField] private bool debugMode;
        private StageSelectTurntable view;
        private StageMusicPreview preview;
        private int selected = -1;
        private SelectionPhase phase;
        private float volume = 1;

        private void Awake()
        {
            if (room == null || layout == null || title == null || station == null || music == null
                || stages == null || stages.Length == 0)
            {
                Debug.LogError("Stage selection needs its room, font, catalog and audio bindings.", this);
                enabled = false;
                return;
            }
            // Older scene saves may have dropped the temporarily removed bindings.
            if (rack == null) rack = station.Find("Record Rack") as RectTransform;
            if (caseDisplay == null) caseDisplay = station.Find("Case Display - front view") as RectTransform;
            if (toneArm == null) toneArm = station.Find("Turntable/04 Pickup assembly") as RectTransform;
            var root = new GameObject("Radial stage selection", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            view = root.AddComponent<StageSelectTurntable>();
            var names = new string[stages.Length];
            var summaries = new string[stages.Length];
            for (int i = 0; i < stages.Length; i++)
            {
                RoomChart chart = stages[i]?.chart;
                names[i] = chart != null ? chart.SongTitle : "COMING SOON";
                summaries[i] = chart != null && chart.music != null ? $"{Length(chart)}  ·  {chart.bpm:0} BPM" : "준비 중";
            }
            view.Build(title.font, names, summaries);
            view.Selected += SelectStage;
            view.Stopped += StopPreview;
            view.Started += StartSelectedStage;
            view.VolumeChanged += ChangeVolume;
            preview = new StageMusicPreview(music);
            preview.Ready += view.BeginPlayback;
            preview.Failed += PreviewFailed;
        }

        private void OnEnable()
        {
            if (view == null) return;
            StageSelection.IsRecordRun = false;
            StageSelection.DebugMode = false;
            StageSelection.ReturnScene = gameObject.scene.name;
            room.MovementEnabled = false; room.CloseExit();
            // Keep the authored small turntable in the map. Only the obsolete rack is hidden.
            station.gameObject.SetActive(true);
            if (rack != null) rack.gameObject.SetActive(false);
            if (caseDisplay != null) caseDisplay.gameObject.SetActive(false);
            layout.gameObject.SetActive(false);
            if (details != null) details.gameObject.SetActive(false);
            view.gameObject.SetActive(true); view.SetFade(0);
            preview.Stop();
            phase = SelectionPhase.Transition;
            selected = -1;
            view.ResetSelection();
            view.SetInput(false, false); view.SetReveal(0); view.SetBackdrop(0);
            StartCoroutine(OpenSelection(true));
        }

        private static string Length(RoomChart chart)
        {
            int seconds = chart != null && chart.music != null ? Mathf.CeilToInt(chart.music.length) : 0;
            return seconds > 0 ? $"{seconds / 60}:{seconds % 60:00}" : "--:--";
        }
        private static bool CanStart(Stage stage) => stage != null && stage.chart != null && stage.chart.music != null
            && !string.IsNullOrWhiteSpace(stage.sceneName) && Application.CanStreamedLevelBeLoaded(stage.sceneName);

        private void Update()
        {
            // Keep the preview fading while the deck closes and the player returns to the map.
            preview?.Tick(Time.unscaledDeltaTime);
            if (view == null || phase == SelectionPhase.Transition || room.IsExiting) return;
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            if (phase == SelectionPhase.Room)
            {
                bool reopen = keyboard != null && keyboard.eKey.wasPressedThisFrame
                    || pad != null && pad.buttonNorth.wasPressedThisFrame;
                if (reopen) ShowRecords();
                return;
            }
            bool previous = keyboard != null && (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                || pad != null && pad.dpad.left.wasPressedThisFrame;
            bool next = keyboard != null && (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                || pad != null && pad.dpad.right.wasPressedThisFrame;
            if (previous || next) view.StepSelection(next ? 1 : -1);
            bool confirm = keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
                || pad != null && pad.buttonSouth.wasPressedThisFrame;
            if (confirm)
            {
                if (view.IsPlaying) StartSelectedStage();
                else view.ToggleArm();
            }
        }

        public void SelectStage(int index)
        {
            if (view == null || phase != SelectionPhase.Selecting) return;
            index = (index % stages.Length + stages.Length) % stages.Length;
            selected = index; StageSelection.LastIndex = selected;
            Stage stage = stages[selected];
            RoomChart chart = stage?.chart;
            volume = StageVolumeStore.Read(stage?.sceneName);
            string best = StageRecordStore.TryGetBest(stage?.sceneName, out float accuracy) ? $"BEST {accuracy:0.00}%" : "BEST —";
            view.ShowSelection(selected, chart != null ? chart.SongTitle : "COMING SOON",
                chart != null ? $"{Length(chart)}  ·  {chart.bpm:0} BPM" : "준비 중", best, volume, CanStart(stage));
            if (chart != null && chart.music != null)
            {
                view.ShowStatus("미리듣기 준비 중…");
                preview.Play(chart.music, volume);
            }
            else
            {
                preview.Stop();
                view.PlaybackFailed();
                view.ShowStatus("아직 음원이 준비되지 않은 스테이지입니다.");
            }
        }

        private void PreviewFailed()
        {
            view.PlaybackFailed();
            view.ShowStatus("음원을 불러오지 못했습니다. 다른 구역을 선택해 주세요.");
        }

        private void ChangeVolume(float value)
        {
            if (selected < 0 || phase != SelectionPhase.Selecting) return;
            volume = Mathf.Clamp01(value);
            StageVolumeStore.Write(stages[selected]?.sceneName, volume);
            preview.SetVolume(volume);
        }

        private void StopPreview()
        {
            preview.Stop();
            if (toneArm != null) toneArm.localRotation = Quaternion.Euler(0, 0, 28);
        }

        public void StartSelectedStage()
        {
            if (view == null || phase != SelectionPhase.Selecting || !view.IsPlaying || selected < 0 || !CanStart(stages[selected])) return;
            phase = SelectionPhase.Transition;
            view.SetInput(false, false);
            StageSelection.DebugMode = debugMode;
            StageSelection.MusicVolume = volume;
            PlayerPrefs.Save();
            StartCoroutine(ReturnToRoom());
        }

        private IEnumerator AnimateSelection(bool show)
        {
            float began = Time.unscaledTime;
            while (Time.unscaledTime - began < .5f)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - began) / .5f);
                view.SetReveal(show ? t : 1 - t);
                yield return null;
            }
            view.SetReveal(show ? 1 : 0);
        }

        private IEnumerator OpenSelection(bool arriving)
        {
            if (arriving && revealDelay > 0) yield return new WaitForSecondsRealtime(revealDelay);
            yield return FadeBackdrop(true);
            yield return AnimateSelection(true);
            phase = SelectionPhase.Selecting;
            view.SetInput(true, selected >= 0 && CanStart(stages[selected]));
        }

        private IEnumerator ReturnToRoom()
        {
            if (toneArm != null) toneArm.localRotation = Quaternion.identity;
            yield return AnimateSelection(false);
            yield return FadeBackdrop(false);
            view.gameObject.SetActive(false);
            room.SetExit(stages[selected].sceneName, true);
            phase = SelectionPhase.Room;
            room.MovementEnabled = true;
        }

        private IEnumerator FadeBackdrop(bool show)
        {
            float began = Time.unscaledTime;
            while (Time.unscaledTime - began < .4f)
            {
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.unscaledTime - began) / .4f));
                view.SetBackdrop(show ? t : 1 - t);
                yield return null;
            }
            view.SetBackdrop(show ? 1 : 0);
        }

        public void ShowRecords()
        {
            if (view == null || phase != SelectionPhase.Room || !room.NearTurntable || room.IsExiting) return;
            phase = SelectionPhase.Transition;
            room.MovementEnabled = false; room.CloseExit();
            view.gameObject.SetActive(true); view.SetInput(false, false); view.SetReveal(0); view.SetBackdrop(0);
            StartCoroutine(OpenSelection(false));
        }
        private void OnDisable()
        {
            StopAllCoroutines();
            preview?.Stop();
            if (view != null) view.gameObject.SetActive(false);
            PlayerPrefs.Save();
        }
    }
}
