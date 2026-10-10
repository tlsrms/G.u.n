using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Gun.RoomRhythm
{
    [DefaultExecutionOrder(100)]
    public sealed class StageProgression : MonoBehaviour
    {
        [SerializeField] private RoomSession session;
        [SerializeField] private string nextSafeScene;
        [SerializeField] private string entrySafeScene;
        [SerializeField] private string nextTutorialScene;
        [SerializeField] private CanvasGroup fade;
        [SerializeField, Min(0)] private float clearHold = 1.2f;
        private bool transitioning, recordRun;

        private void Start()
        {
            recordRun = StageSelection.IsRecordRun;
            if (session == null || fade == null)
            {
                Debug.LogError("Stage progression needs its session and fade references.", this);
                enabled = false;
                return;
            }
        }

        private void Update()
        {
            if (transitioning) return;
            if (session.IsCleared)
            {
                if (!session.IsDebugRun)
                    StageRecordStore.SaveClear(gameObject.scene.name, session.AccuracyPercent);
                StartCoroutine(Travel(recordRun ? StageSelection.ReturnScene : nextSafeScene, clearHold,
                    !recordRun && nextSafeScene == SafeRoomTransit.SceneName ? nextTutorialScene : null, true));
                return;
            }
            bool back = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame
                || Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame;
            if (back) StartCoroutine(Travel(recordRun ? StageSelection.ReturnScene : entrySafeScene, 0,
                !recordRun && entrySafeScene == SafeRoomTransit.SceneName ? gameObject.scene.name : null, false));
            // Start can run before the Input System switches to the play-mode clock.
            // Retry from Update until ready, but never auto-start a reset run.
            else if (!session.HasStarted) session.TryBeginRun();
        }

        private IEnumerator Travel(string destination, float delay, string onward, bool showArrival)
        {
            if (!Application.CanStreamedLevelBeLoaded(destination))
            {
                Debug.LogError("Stage progression destination is not in Build Settings: " + destination, this);
                enabled = false;
                yield break;
            }
            if (destination == SafeRoomTransit.SceneName
                && (string.IsNullOrWhiteSpace(onward) || !Application.CanStreamedLevelBeLoaded(onward)))
            {
                Debug.LogError("Safe room onward destination is not in Build Settings: " + onward, this);
                enabled = false;
                yield break;
            }
            transitioning = true;
            if (delay > 0) yield return new WaitForSecondsRealtime(delay);
            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < .35f)
            {
                fade.alpha = Mathf.Clamp01((Time.unscaledTime - started) / .35f);
                yield return null;
            }
            fade.alpha = 1;
            if (destination == SafeRoomTransit.SceneName) SafeRoomTransit.Prepare(onward, showArrival);
            yield return SceneManager.LoadSceneAsync(destination);
        }
    }
}
