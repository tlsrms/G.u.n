using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Gun.RoomRhythm
{
    // All room geometry and overlays are serialized scene objects. D advances through the open exit; there is no free movement.
    public sealed class SafeRoomController : MonoBehaviour
    {
        [SerializeField] private RectTransform player;
        [SerializeField] private RectTransform turntableInteractionPoint;
        [SerializeField] private RectTransform upperGate, lowerGate;
        [SerializeField] private CanvasGroup fade;
        [SerializeField] private string nextScene;
        [SerializeField] private bool exitInitiallyOpen = true;
        [SerializeField, Min(0)] private float wakeDuration = .5f;
        private float enteredAt, opening;
        private bool exitOpen, exiting, recordRun;
        private Canvas playerCanvas;
        public bool MovementEnabled { get; set; } = true;
        public bool IsExiting => exiting;
        public bool NearTurntable => player != null && turntableInteractionPoint != null
            && Vector2.Distance(player.anchoredPosition, turntableInteractionPoint.anchoredPosition) < 210;

        private void OnEnable()
        {
            if (player == null || upperGate == null || lowerGate == null || fade == null)
            {
                Debug.LogError("Safe room needs its authored player, gate and fade references.", this);
                enabled = false;
                return;
            }
            enteredAt = Time.unscaledTime;
            playerCanvas = player.GetComponentInParent<Canvas>();
            exitOpen = exitInitiallyOpen;
            opening = exitOpen ? 1 : 0;
            exiting = false;
            recordRun = false;
            fade.alpha = 1;
            UpdateGate();
        }

        public void SetExit(string destination, bool isRecordRun)
        {
            nextScene = destination;
            recordRun = isRecordRun;
            exitOpen = true;
        }

        public void CloseExit() => exitOpen = false;

        private void Update()
        {
            opening = Mathf.MoveTowards(opening, exitOpen ? 1 : 0, Time.unscaledDeltaTime * 2.5f);
            UpdateGate();
            FacePointer();
            if (exiting) return;
            float waking = wakeDuration > 0 ? Mathf.Clamp01((Time.unscaledTime - enteredAt) / wakeDuration) : 1;
            fade.alpha = 1 - Mathf.SmoothStep(0, 1, waking);
            if (!MovementEnabled || waking < 1) return;
            bool leave = Keyboard.current != null && Keyboard.current.dKey.wasPressedThisFrame
                || Gamepad.current != null && Gamepad.current.dpad.right.wasPressedThisFrame;
            if (leave && exitOpen) StartCoroutine(Exit());
        }

        private void UpdateGate()
        {
            upperGate.anchoredPosition = new Vector2(300, 27 + opening * 65);
            lowerGate.anchoredPosition = new Vector2(300, -27 - opening * 65);
        }

        private void FacePointer()
        {
            if (Mouse.current == null || playerCanvas == null) return;
            Camera camera = playerCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : playerCanvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(player.parent as RectTransform,
                Mouse.current.position.ReadValue(), camera, out Vector2 point)) return;
            Vector2 direction = point - player.anchoredPosition;
            if (direction.sqrMagnitude > .01f)
                player.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90);
        }

        private IEnumerator MoveToExit(Vector2 destination, float seconds)
        {
            Vector2 from = player.anchoredPosition;
            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < seconds)
            {
                float t = Mathf.SmoothStep(0, 1, (Time.unscaledTime - started) / seconds);
                player.anchoredPosition = Vector2.Lerp(from, destination, t);
                yield return null;
            }
            player.anchoredPosition = destination;
        }

        private IEnumerator Exit()
        {
            if (string.IsNullOrWhiteSpace(nextScene) || !Application.CanStreamedLevelBeLoaded(nextScene))
            {
                Debug.LogError("Safe room destination is not in Build Settings: " + nextScene, this);
                exitOpen = false;
                yield break;
            }
            exiting = true;
            StageSelection.IsRecordRun = recordRun;
            while (opening < .95f) yield return null;
            // Align with the passage first, so off-center opening rooms never cross a wall.
            if (Mathf.Abs(player.anchoredPosition.y) > .01f)
                yield return MoveToExit(new Vector2(player.anchoredPosition.x, 0), .12f);
            yield return MoveToExit(new Vector2(490, 0), .4f);
            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < .15f)
            {
                fade.alpha = Mathf.Clamp01((Time.unscaledTime - started) / .15f);
                yield return null;
            }
            fade.alpha = 1;
            yield return SceneManager.LoadSceneAsync(nextScene);
        }
    }
}
