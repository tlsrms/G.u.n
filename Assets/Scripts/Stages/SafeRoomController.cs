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
        [SerializeField] private bool useSharedRoute;
        [SerializeField] private RectTransform entranceUpperGate, entranceLowerGate, entrancePoint;
        [SerializeField, Min(.01f)] private float arrivalDuration = .48f;
        [SerializeField, Min(.01f)] private float entranceCloseDuration = .22f;
        private float enteredAt, opening;
        private bool exitOpen, exiting, recordRun, arriving;
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
            if (useSharedRoute && (entranceUpperGate == null || entranceLowerGate == null || entrancePoint == null))
            {
                Debug.LogError("Shared safe room needs its authored entrance gates and arrival point.", this);
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
            arriving = false;
            SetEntranceGate(0);
            if (useSharedRoute && SafeRoomTransit.TryConsume(out string destination, out bool showArrival))
            {
                nextScene = destination;
                if (showArrival)
                {
                    arriving = true;
                    exitOpen = false;
                    opening = 0;
                    StartCoroutine(Arrive());
                }
            }
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
            if (exiting) return;
            if (!arriving) FacePointer();
            float waking = wakeDuration > 0 ? Mathf.Clamp01((Time.unscaledTime - enteredAt) / wakeDuration) : 1;
            fade.alpha = 1 - Mathf.SmoothStep(0, 1, waking);
            if (arriving || !MovementEnabled || waking < 1) return;
            bool leave = Keyboard.current != null && Keyboard.current.dKey.wasPressedThisFrame
                || Gamepad.current != null && Gamepad.current.dpad.right.wasPressedThisFrame;
            if (leave && exitOpen) StartCoroutine(Exit());
        }

        private void UpdateGate()
        {
            upperGate.anchoredPosition = new Vector2(300, 27 + opening * 65);
            lowerGate.anchoredPosition = new Vector2(300, -27 - opening * 65);
        }

        private void SetEntranceGate(float amount)
        {
            if (entranceUpperGate == null || entranceLowerGate == null) return;
            entranceUpperGate.anchoredPosition = new Vector2(-300, 27 + amount * 65);
            entranceLowerGate.anchoredPosition = new Vector2(-300, -27 - amount * 65);
        }

        private IEnumerator Arrive()
        {
            Vector2 restingPoint = player.anchoredPosition;
            player.anchoredPosition = entrancePoint.anchoredPosition;
            player.localRotation = Quaternion.Euler(0, 0, -90);
            SetEntranceGate(1);
            while (Time.unscaledTime - enteredAt < wakeDuration) yield return null;
            // Clear the doorway with the whole character before the panels close behind them.
            yield return MovePlayer(new Vector2(-160, 0), Mathf.Max(.01f, arrivalDuration));
            float began = Time.unscaledTime;
            float duration = Mathf.Max(.01f, entranceCloseDuration);
            while (Time.unscaledTime - began < duration)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - began) / duration);
                SetEntranceGate(1 - t * t);
                yield return null;
            }
            SetEntranceGate(0);
            yield return MovePlayer(restingPoint, .28f);
            arriving = false;
            exitOpen = true;
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

        private IEnumerator MovePlayer(Vector2 destination, float seconds)
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
            player.localRotation = Quaternion.Euler(0, 0, -90);
            StageSelection.IsRecordRun = recordRun;
            while (opening < .95f) yield return null;
            // Align with the passage first, so off-center opening rooms never cross a wall.
            if (Mathf.Abs(player.anchoredPosition.y) > .01f)
                yield return MovePlayer(new Vector2(player.anchoredPosition.x, 0), .12f);
            yield return MovePlayer(new Vector2(490, 0), .4f);
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
