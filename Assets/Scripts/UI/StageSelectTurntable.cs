using System;
using UnityEngine;
using UnityEngine.UI;

namespace Gun.RoomRhythm
{
    public sealed partial class StageSelectTurntable : MonoBehaviour
    {
        private static readonly Vector2 DiscCenter = new Vector2(-125, 35);
        private static readonly Vector2 ArmPivot = new Vector2(345, 255);
        private const float DiscRadius = 327, InnerRadius = DiscRadius * .34f;
        private RectTransform board, record, armRoot;
        private RecordDiscDrag drag;
        private RecordDialGraphic[] sectors;
        private Text[] sectorLabels;
        private Text songTitle, songInfo, best, cue, volumeLabel, startLabel;
        private Button start;
        private Slider volume;
        private Image curtain;
        private CanvasGroup group;
        private CanvasGroup content;
        private StageSelectSurface backdrop;
        private int selected = -1, pendingIndex, highlighted = -2;
        private bool interactive, available;
        private static readonly Vector2 ArmEnd = new Vector2(7, -420);
        private const float PlayingArmAngle = -35;
        private readonly RecordTurntableState mechanics = new RecordTurntableState();
        private readonly System.Collections.Generic.List<Button> armButtons = new System.Collections.Generic.List<Button>();
        private CanvasGroup sectorContent, centerInfo;
        private Text centerHint;
        private RectTransform needleMarker;
        public bool IsPlaying => mechanics.IsPlaying;
        public event Action Stopped;
        public event Action<int> Selected;
        public event Action Started;
        public event Action<float> VolumeChanged;

        private Vector2 LandingPoint => ArmPivot + (Vector2)(Quaternion.Euler(0, 0, PlayingArmAngle) * (Vector3)ArmEnd);
        private int Candidate
        {
            get
            {
                Vector2 contact = LandingPoint - DiscCenter;
                return RecordSelectionGeometry.SectorAtRotation(contact.x, contact.y, mechanics.Rotation,
                    sectors.Length, InnerRadius, DiscRadius * .95f);
            }
        }
        public void ToggleArm()
        {
            if (!interactive || mechanics.Phase == RecordPlaybackPhase.RaisingArm) return;
            pendingIndex = Candidate;
            if (mechanics.CanRotate && pendingIndex < 0) return;
            drag.Cancel();
            bool engaging = mechanics.ToggleArm();
            if (engaging) cue.text = "톤암을 내리는 중…";
            else { Stopped?.Invoke(); selected = -1; available = false; cue.text = "톤암을 되돌리는 중…"; }
            RefreshVisuals(); RefreshInput();
        }
        public void StepSelection(int direction)
        {
            if (!interactive || !mechanics.CanRotate) return;
            int index = (Candidate + direction + sectors.Length) % sectors.Length;
            Vector2 contact = LandingPoint - DiscCenter;
            double contactAngle = Math.Atan2(contact.y, contact.x) * 180 / Math.PI;
            mechanics.Rotate(contactAngle - RecordSelectionGeometry.CenterAngle(index, sectors.Length) - mechanics.Rotation);
            RefreshVisuals();
        }
        public void SetInput(bool enabled, bool canStart)
        { interactive = enabled; available = canStart; group.interactable = enabled; RefreshInput(); }
        private void RefreshInput()
        {
            drag.Interactable = interactive && mechanics.CanRotate;
            foreach (Button button in armButtons) button.interactable = interactive && mechanics.Phase != RecordPlaybackPhase.RaisingArm;
            volume.interactable = interactive && selected >= 0 && mechanics.IsPlaying;
            start.interactable = interactive && available && mechanics.IsPlaying;
        }
        public void SetFade(float value) => curtain.color = new Color(0, 0, 0, Mathf.Clamp01(value));
        public void SetReveal(float progress)
        {
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress));
            content.alpha = t; board.anchoredPosition = new Vector2(0, 25 + (1 - t) * 1150);
        }
        public void SetBackdrop(float opacity) => backdrop.color = Gray(.028f, Mathf.Clamp01(opacity));
        public void ResetSelection()
        {
            mechanics.Reset(); selected = -1; highlighted = -2;
            songTitle.text = songInfo.text = best.text = "";
            cue.text = "판을 드래그해 선택   ·   톤암 클릭으로 재생";
            startLabel.text = "톤암을 눌러 재생하세요";
            volume.SetValueWithoutNotify(1); ShowVolume(1);
            RefreshVisuals(); SetInput(true, false);
        }
        public void ShowSelection(int index, string title, string info, string record, float gain, bool canStart)
        {
            selected = index; songTitle.text = title; songInfo.text = info; best.text = record;
            volume.SetValueWithoutNotify(gain); ShowVolume(gain);
            available = canStart; startLabel.text = canStart ? "START  /  게임 시작" : "준비 중";
            RefreshVisuals(); RefreshInput();
        }
        public void BeginPlayback()
        {
            mechanics.AudioReady(); RefreshInput();
            cue.text = "톤암을 다시 누르면 선택으로 복귀   ·   START: 게임 시작";
        }
        public void PlaybackFailed()
        {
            mechanics.Stop(); selected = -1; available = false; RefreshInput();
        }
        public void ShowStatus(string message) => cue.text = message;
        private void ShowVolume(float value) => volumeLabel.text = $"{Mathf.RoundToInt(value * 100)}%";
        private void Highlight(int index)
        {
            if (sectors == null || highlighted == index) return;
            highlighted = index;
            for (int i = 0; i < sectors.Length; i++)
            {
                if (sectors[i] != null) sectors[i].color = i == index ? Gray(1, .18f) : Gray(1, .025f);
                if (sectorLabels[i] != null) sectorLabels[i].color = i == index ? Color.white : Gray(.53f);
            }
        }
        private void Update()
        {
            if (record == null) return;
            var before = mechanics.Phase;
            bool landed = mechanics.Tick(Time.unscaledDeltaTime);
            if (landed) Selected?.Invoke(pendingIndex);
            if (mechanics.CanRotate && before == RecordPlaybackPhase.RaisingArm)
            {
                cue.text = "판을 드래그해 선택   ·   톤암 클릭으로 재생";
                startLabel.text = "톤암을 눌러 재생하세요";
            }
            RefreshVisuals(); RefreshInput();
        }
        private void RefreshVisuals()
        {
            record.localRotation = Quaternion.Euler(0, 0, (float)mechanics.Rotation);
            // Labels orbit with their sectors, but cancel the record's rotation for readability.
            Quaternion upright = Quaternion.Inverse(record.localRotation);
            foreach (Text label in sectorLabels) label.rectTransform.localRotation = upright;
            float arm = Mathf.SmoothStep(0, 1, (float)mechanics.ArmProgress);
            armRoot.localRotation = Quaternion.Euler(0, 0, PlayingArmAngle * arm);
            sectorContent.alpha = 1 - arm;
            centerInfo.alpha = selected >= 0 ? arm : 0;
            centerHint.color = Gray(.87f, 1 - arm);
            needleMarker.gameObject.SetActive(mechanics.CanRotate);
            Highlight(mechanics.CanRotate ? Candidate : pendingIndex);
        }
    }
}
