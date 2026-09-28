using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gun.RoomRhythm
{
    [DisallowMultipleComponent]
    public sealed class StageSelectScene : MonoBehaviour
    {
        [Serializable]
        private sealed class Stage
        {
            public RoomChart chart;
            public string sceneName;
            public GameObject preview;
            public Button button;
            public Graphic face;
            public Text number;
        }

        [SerializeField] private Stage[] stages;
        [SerializeField] private Text channel;
        [SerializeField] private Text title;
        [SerializeField] private Text duration;
        [SerializeField] private Button previous;
        [SerializeField] private Button next;
        [SerializeField] private Button play;
        [SerializeField] private Text playLabel;
        [SerializeField] private StageSignal signal;
        private int selected;
        private bool loading;

        private void OnEnable()
        {
            if (!HasReferences())
            {
                Debug.LogError("Stage Select scene has missing authored UI references.", this);
                enabled = false;
                return;
            }
            loading = false;
            previous.onClick.AddListener(Previous);
            next.onClick.AddListener(Next);
            play.onClick.AddListener(Play);
            SelectStage(Mathf.Clamp(StageSelection.LastIndex, 0, stages.Length - 1));
        }

        private bool HasReferences()
        {
            if (stages == null || stages.Length == 0 || channel == null || title == null || duration == null
                || previous == null || next == null || play == null
                || playLabel == null || signal == null) return false;
            foreach (Stage stage in stages)
                if (stage == null || stage.button == null || stage.face == null || stage.number == null || stage.preview == null)
                    return false;
            return true;
        }

        private void Update()
        {
            if (loading) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) Previous();
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) Next();
                if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame) Play();
            }
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.dpad.left.wasPressedThisFrame) Previous();
                if (pad.dpad.right.wasPressedThisFrame) Next();
                if (pad.buttonSouth.wasPressedThisFrame) Play();
            }
            float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0;
            if (scroll > 0) Previous(); else if (scroll < 0) Next();
        }

        public void SelectStage(int index)
        {
            if (loading || stages == null || stages.Length == 0) return;
            selected = (index % stages.Length + stages.Length) % stages.Length;
            StageSelection.LastIndex = selected;
            for (int i = 0; i < stages.Length; i++)
            {
                bool active = i == selected;
                stages[i].preview.SetActive(active);
                stages[i].face.color = Gray(active ? .84f : .15f);
                stages[i].number.color = Gray(active ? .09f : .82f);
            }
            RoomChart chart = stages[selected].chart;
            channel.text = "CH " + (selected + 1).ToString("00");
            title.text = chart != null ? chart.name.Replace('_', ' ').ToUpperInvariant() : "NO SIGNAL";
            float seconds = chart != null && chart.music != null ? chart.music.length : 0;
            duration.text = $"{(int)seconds / 60:00}:{(int)seconds % 60:00}";
            playLabel.text = "PLAY  ▷";
            play.interactable = chart != null && !string.IsNullOrWhiteSpace(stages[selected].sceneName)
                && Application.CanStreamedLevelBeLoaded(stages[selected].sceneName);
            signal.Tune();
        }

        private static Color Gray(float value) => new Color(value, value, value, 1);
        private void Previous() => SelectStage(selected - 1);
        private void Next() => SelectStage(selected + 1);

        private void Play()
        {
            if (loading || !play.interactable) return;
            loading = true;
            playLabel.text = "TUNING…";
            play.interactable = previous.interactable = next.interactable = false;
            foreach (Stage stage in stages) stage.button.interactable = false;
            StageSelection.ReturnScene = gameObject.scene.name;
            SceneManager.LoadSceneAsync(stages[selected].sceneName);
        }

        private void OnDisable()
        {
            if (previous != null) previous.onClick.RemoveListener(Previous);
            if (next != null) next.onClick.RemoveListener(Next);
            if (play != null) play.onClick.RemoveListener(Play);
        }
    }
}
