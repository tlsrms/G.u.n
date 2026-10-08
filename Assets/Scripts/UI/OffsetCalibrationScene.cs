using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Gun.RoomRhythm
{
    public sealed class OffsetCalibrationScene : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip audioClip;
        [SerializeField] private RoomChart targetChart;
        [SerializeField, Range(30, 300)] private float bpm = 120;
        [SerializeField, Min(0)] private float firstBeatSeconds;
        [SerializeField, Min(.5f)] private float startDelay = 3;
        private OffsetCalibration measurement;
        private InputAction tap, restart;
        private double inputOrigin, dspOrigin;
        private float loadingStartedAt;
        private string error;
        private bool saved, scheduled;
        private GUIStyle titleStyle, valueStyle, labelStyle;
        private InputSettings.UpdateMode previousMode;

        private void Awake()
        {
            tap = new InputAction("Calibration tap", InputActionType.Button);
            tap.AddBinding("<Keyboard>/space"); tap.AddBinding("<Mouse>/leftButton");
            tap.performed += context => {
                if (measurement == null || measurement.Finished || !scheduled) return;
                measurement.Tap(context.time - inputOrigin);
                if (measurement.Finished) StopAudio();
            };
            restart = new InputAction("Restart calibration", InputActionType.Button, "<Keyboard>/r");
            restart.performed += _ => Begin();
        }

        private void OnEnable()
        {
            previousMode = InputSystem.settings.updateMode;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            tap.Enable(); restart.Enable();
        }
        private void Start() => Begin();

        private void Begin()
        {
            StopAudio(); measurement = null; error = null; saved = false;
            loadingStartedAt = Time.realtimeSinceStartup;
            try
            {
                if (source == null || audioClip == null) throw new ArgumentException("Assign Audio Clip on Offset Calibration.");
                if (!(startDelay >= .5f) || float.IsInfinity(startDelay)) throw new ArgumentException("Start Delay must be at least 0.5 seconds.");
                measurement = new OffsetCalibration(bpm, audioClip.length, firstBeatSeconds);
                if (audioClip.loadState == AudioDataLoadState.Unloaded) audioClip.LoadAudioData();
                // Update schedules playback only once the decoded audio is ready.
            }
            catch (Exception exception) { error = exception.Message; }
        }

        private void Update()
        {
            if (measurement == null || measurement.Finished || error != null) return;
            if (!scheduled)
            {
                if (audioClip.loadState == AudioDataLoadState.Failed) { error = "Audio could not be loaded."; return; }
                if (audioClip.loadState != AudioDataLoadState.Loaded)
                {
                    if (Time.realtimeSinceStartup - loadingStartedAt > 15) error = "Audio loading timed out.";
                    return;
                }
                double now = AudioSettings.dspTime;
                dspOrigin = now + startDelay;
                inputOrigin = InputState.currentTime + (dspOrigin - now);
                source.clip = audioClip; source.loop = true; source.playOnAwake = false; source.spatialBlend = 0;
                source.pitch = 1; source.PlayScheduled(dspOrigin); scheduled = true;
            }
            measurement.Tick(AudioSettings.dspTime - dspOrigin);
            if (measurement.Finished) StopAudio();
        }

        private void StopAudio() { if (source != null) source.Stop(); scheduled = false; }
        private void OnApplicationFocus(bool focused)
        {
            if (!focused && measurement != null && !measurement.Finished)
            { error = "Measurement interrupted. Restart when focused."; StopAudio(); }
        }
        private void OnDisable()
        {
            StopAudio(); tap.Disable(); restart.Disable();
            InputSystem.settings.updateMode = previousMode;
        }
        private void OnDestroy() { tap.Dispose(); restart.Dispose(); }

        private static string Ms(double value) => value.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " ms";

        private void OnGUI()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                valueStyle = new GUIStyle(titleStyle) { fontSize = 32 };
                labelStyle = new GUIStyle(titleStyle) { fontSize = 16 };
            }
            float width = Mathf.Min(620, Screen.width - 32), x = (Screen.width - width) * .5f;
            float y = Mathf.Max(12, (Screen.height - 360) * .5f);
            GUILayout.BeginArea(new Rect(x, y, width, Screen.height - y - 12));
            GUILayout.Label("OFFSET CALIBRATION", titleStyle);
            GUILayout.Space(16);
            if (error != null) GUILayout.Label(error, labelStyle);
            else if (measurement == null || !scheduled && !measurement.Finished) GUILayout.Label("Loading audio", labelStyle);
            else if (!measurement.Finished)
            {
                double elapsed = AudioSettings.dspTime - dspOrigin;
                string phase = elapsed < 0 ? "Ready in " + Math.Ceiling(-elapsed) : elapsed < measurement.WarmupEnd ? "Warm-up" : "Measuring";
                GUILayout.Label(phase, valueStyle);
                GUILayout.Label(measurement.SampleCount + " / " + OffsetCalibration.MaximumSamples + " samples", labelStyle);
                GUILayout.Label(Math.Max(0, Math.Ceiling(measurement.Deadline - elapsed)) + " s remaining", labelStyle);
                Rect bar = GUILayoutUtility.GetRect(width, 5);
                GUI.color = new Color(.15f, .18f, .2f); GUI.DrawTexture(bar, Texture2D.whiteTexture);
                bar.width *= (float)measurement.SampleCount / OffsetCalibration.MaximumSamples;
                GUI.color = new Color(.3f, 1, .8f); GUI.DrawTexture(bar, Texture2D.whiteTexture); GUI.color = Color.white;
            }
            else
            {
                GUILayout.Label(measurement.Reliable ? "Recommended offset" : "Unstable measurement", labelStyle);
                GUILayout.Label(measurement.SampleCount > 0 ? Ms(measurement.RecommendedMs) : "No samples", valueStyle);
                GUILayout.Label(measurement.InlierCount + " valid / " + measurement.Attempts + " taps", labelStyle);
                GUILayout.Label("Spread: " + measurement.SpreadMs.ToString("F1", CultureInfo.InvariantCulture) + " ms", labelStyle);
                GUI.enabled = measurement.Reliable && !saved && targetChart != null;
                if (GUILayout.Button(saved ? "Applied" : "Apply offset", GUILayout.Height(36)))
                {
                    InputOffsetSettings.Save(targetChart, measurement.RecommendedMs);
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(targetChart);
                    UnityEditor.AssetDatabase.SaveAssetIfDirty(targetChart);
#endif
                    saved = true;
                }
                GUI.enabled = true;
            }
            GUILayout.Space(12);
            GUILayout.Label(targetChart != null ? "Chart: " + targetChart.name : "Assign Target Chart to apply the result", labelStyle);
            GUILayout.Label("Current offset: " + Ms(InputOffsetSettings.Milliseconds(targetChart)), labelStyle);
            if (GUILayout.Button("Restart", GUILayout.Height(36))) Begin();
            GUILayout.EndArea();
        }
    }
}
