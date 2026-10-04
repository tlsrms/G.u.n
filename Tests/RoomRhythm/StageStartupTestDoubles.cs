using System;
using System.Collections;
using System.Collections.Generic;

// Minimal engine boundaries for StageStartupChecks, not replacements in the Unity project.
namespace UnityEngine
{
    public sealed class SerializeField : Attribute { }
    public sealed class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public sealed class RequireComponent : Attribute { public RequireComponent(Type type) { } }
    public sealed class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int order) { } }
    public class MonoBehaviour
    {
        public bool enabled = true;
        public readonly GameObject gameObject = new GameObject();
        protected void StartCoroutine(IEnumerator routine) => throw new Exception("Unexpected scene transition in startup check.");
    }
    public sealed class GameObject { public SceneManagement.Scene scene = new SceneManagement.Scene(); }
    public struct Vector2 { public static Vector2 zero => default; }
    public sealed class CanvasGroup { public float alpha; }
    public sealed class AudioClip { }
    public sealed class AudioSource
    {
        public AudioClip clip;
        public bool loop, isPlaying;
        public int PlayCount;
        public double ScheduledTime;
        public void Stop() => isPlaying = false;
        public void PlayScheduled(double time) { ScheduledTime = time; isPlaying = true; PlayCount++; }
    }
    public static class AudioSettings { public static double dspTime; }
    public static class Time { public static float unscaledTime; }
    public static class Mathf { public static float Clamp01(float value) => Math.Max(0, Math.Min(1, value)); }
    public static class Application { public static bool CanStreamedLevelBeLoaded(string scene) => true; }
    public static class Debug { public static void LogError(string error, object context) => throw new Exception(error); }
    public sealed class WaitForSecondsRealtime { public WaitForSecondsRealtime(float time) { } }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { public string name => "MafiaStage01"; }
    public static class SceneManager { public static object LoadSceneAsync(string name) => null; }
}
namespace UnityEngine.InputSystem.LowLevel
{
    public enum InputUpdateType { None, Editor, Dynamic, Fixed }
    public static class InputState { public static double currentTime; public static InputUpdateType currentUpdateType; }
}
namespace UnityEngine.InputSystem
{
    public enum InputActionType { PassThrough, Button }
    public class InputDevice { }
    public class InputControl { public InputDevice device; }
    public sealed class Keyboard : InputDevice
    {
        public static Keyboard current;
        public Controls.KeyControl escapeKey = new Controls.KeyControl();
    }
    public sealed class Mouse : InputDevice
    {
        public static Mouse current;
        public Controls.Vector2Control position = new Controls.Vector2Control();
    }
    public sealed class Gamepad : InputDevice
    {
        public static Gamepad current;
        public Controls.ButtonControl selectButton = new Controls.ButtonControl();
    }
    public sealed class InputSettings
    {
        public enum UpdateMode { ProcessEventsInDynamicUpdate, ProcessEventsInFixedUpdate }
        public UpdateMode updateMode;
    }
    public static class InputSystem
    {
        public static readonly InputSettings settings = new InputSettings();
        public static event Action onBeforeUpdate, onAfterUpdate;
        public static void Before(LowLevel.InputUpdateType type, double time)
        { LowLevel.InputState.currentUpdateType = type; LowLevel.InputState.currentTime = time; onBeforeUpdate?.Invoke(); }
        public static void After(LowLevel.InputUpdateType type, double time)
        { LowLevel.InputState.currentUpdateType = type; LowLevel.InputState.currentTime = time; onAfterUpdate?.Invoke(); }
    }
    public sealed class InputActionMap : IDisposable
    {
        public static InputActionMap LastCreated;
        public readonly Dictionary<string, InputAction> Actions = new Dictionary<string, InputAction>();
        public InputActionMap(string name) => LastCreated = this;
        public InputAction AddAction(string name, InputActionType type, string binding, string interactions = null)
        { var action = new InputAction(); action.AddBinding(binding); Actions.Add(name, action); return action; }
        public void Enable() { }
        public void Disable() { }
        public void Dispose() { }
    }
    public sealed class InputAction
    {
        public readonly List<string> Bindings = new List<string>();
        public event Action<CallbackContext> performed;
        public void AddBinding(string binding) => Bindings.Add(binding);
        public void Emit(InputControl control, float value, double time)
            => performed?.Invoke(new CallbackContext { control = control, value = value, time = time });
        public struct CallbackContext
        {
            public InputControl control;
            public float value;
            public double time;
            public T ReadValue<T>() => (T)(object)value;
        }
    }
}
namespace UnityEngine.InputSystem.Controls
{
    public class ButtonControl : InputControl { public bool wasPressedThisFrame; }
    public sealed class KeyControl : ButtonControl { }
    public sealed class Vector2Control : InputControl { public Vector2 ReadValue() => default; }
}
namespace Gun.RoomRhythm
{
    public static class StageSelection { public static bool IsRecordRun; public static string ReturnScene; }
    public static class StageRecordStore { public static void SaveClear(string scene, float accuracy) { } }

    // Session boundary double: actual chart state and music, without room meshes, combat or scene bindings.
    // The real RoomSession and all engine APIs are also compiled by Run-Checks against Unity references.
    public sealed class RoomSession
    {
        private readonly RoomKeyboard keyboard;
        private readonly SongTimeline timeline;
        public readonly RoomRun Run = new RoomRun(new[] {
            new MoveNote { destinationId = "next", direction = MoveDirection.Up, time = 1.85 },
            new MoveNote { destinationId = "last", direction = MoveDirection.Right, time = 4 }
        }, new TimingWindow { early = .1, accurate = .03, late = .1 }, .1);
        public RoomSession(RoomKeyboard keyboard, SongTimeline timeline) { this.keyboard = keyboard; this.timeline = timeline; }
        public bool HasStarted { get; private set; }
        public bool BlockedByFade;
        public bool IsCleared => Run.Phase == RunPhase.Cleared;
        public bool IsDebugRun => false;
        public float AccuracyPercent => 0;
        public bool TryBeginRun()
        {
            if (Run.Phase != RunPhase.Ready || !keyboard.HasProcessedDynamicUpdate || BlockedByFade) return false;
            timeline.Begin(new UnityEngine.AudioClip());
            Run.Begin();
            HasStarted = true;
            return true;
        }
        public void ResetAtBlack() { timeline.ResetTimeline(); Run.Reset(); }
    }
}
