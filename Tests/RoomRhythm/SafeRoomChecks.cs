using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Gun.RoomRhythm;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Execute the real controllers; only engine time, input, coroutines and scene loading are doubles.
internal static class SafeRoomChecks
{
    private static int checks;
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; }
    private static void Set(object target, string field, object value)
        => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Call(object target, string method)
        => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    private static void Advance(MonoBehaviour target, float seconds)
    {
        for (int i = 0; i < (int)Math.Ceiling(seconds / .01f); i++)
        {
            Time.unscaledDeltaTime = .01f; Time.unscaledTime += .01f;
            Call(target, "Update"); target.AdvanceCoroutines();
            Keyboard.current.dKey.wasPressedThisFrame = false;
            Keyboard.current.escapeKey.wasPressedThisFrame = false;
        }
    }
    private static void Reset()
    {
        SafeRoomTransit.Reset(); StageSelection.IsRecordRun = false;
        StageSelection.ReturnScene = "StageSelectScene";
        SceneManager.Loaded = null; Time.unscaledTime = 0;
        Keyboard.current = new Keyboard(); StageRecordStore.Saves = 0;
    }
    private static StageProgression Progression(int stage, bool cleared)
    {
        var result = new StageProgression();
        result.gameObject.scene = new Scene { name = $"Stage{stage:00}" };
        Set(result, "session", new RoomSession { IsCleared = cleared });
        Set(result, "fade", new CanvasGroup());
        Set(result, "clearHold", .1f);
        Set(result, "nextSafeScene", "SafeRoom");
        Set(result, "entrySafeScene", stage == 1 ? "AwakeningScene" : "SafeRoom");
        Set(result, "nextTutorialScene", stage < 5 ? $"Stage{stage+1:00}" : "StageSelectScene");
        Call(result, "Start");
        return result;
    }
    private sealed class RoomRig
    {
        public readonly SafeRoomController Controller = new SafeRoomController();
        public readonly RectTransform Player = new RectTransform();
        public readonly RectTransform Upper = new RectTransform(), Lower = new RectTransform();
        public readonly RectTransform ExitUpper = new RectTransform(), ExitLower = new RectTransform();
        public RoomRig(bool shared = true)
        {
            Set(Controller, "player", Player);
            Set(Controller, "upperGate", ExitUpper); Set(Controller, "lowerGate", ExitLower);
            Set(Controller, "fade", new CanvasGroup());
            Set(Controller, "nextScene", "Stage02");
            Set(Controller, "useSharedRoute", shared);
            Set(Controller, "entranceUpperGate", Upper); Set(Controller, "entranceLowerGate", Lower);
            Set(Controller, "entrancePoint", new RectTransform { anchoredPosition = new Vector2(-490, 0) });
            Call(Controller, "OnEnable");
        }
    }
    public static void Main()
    {
        for (int stage = 1; stage <= 5; stage++)
        {
            Reset();
            var progression = Progression(stage, true);
            Advance(progression, .7f);
            Check(SceneManager.Loaded == "SafeRoom", "every tutorial clear enters the shared room");
            Check(StageRecordStore.Saves == 1, "transition saves a clear only once");
            SceneManager.Loaded = null;
            var rig = new RoomRig();
            Check(rig.Player.anchoredPosition.x == -490 && rig.Upper.anchoredPosition.y == 92,
                "arrival begins in the left passage with its gate open");
            Check(!SafeRoomTransit.TryConsume(out _, out _), "arrival consumes its route exactly once");
            Keyboard.current.dKey.wasPressedThisFrame = true;
            Advance(rig.Controller, .65f);
            Check(SceneManager.Loaded == null && !rig.Controller.IsExiting, "D cannot skip arrival");
            Check(rig.Upper.anchoredPosition.y == 92 && rig.ExitUpper.anchoredPosition.y == 27,
                "entrance stays open while the player crosses and exit stays closed");
            Advance(rig.Controller, .45f);
            Check(rig.Player.anchoredPosition.x >= -160, "player clears the doorway before it closes");
            Check(rig.Upper.anchoredPosition.y < 92 && rig.Upper.anchoredPosition.y > 27,
                "gate visibly closes after the crossing");
            Advance(rig.Controller, .7f);
            Check(Vector2.Distance(rig.Player.anchoredPosition, Vector2.zero) < .001f,
                "arrival finishes at the authored resting point");
            Check(rig.Upper.anchoredPosition.y == 27 && rig.Lower.anchoredPosition.y == -27,
                "left entrance is sealed after arrival");
            Keyboard.current.dKey.wasPressedThisFrame = true;
            Advance(rig.Controller, 1);
            Check(SceneManager.Loaded == (stage < 5 ? $"Stage{stage+1:00}" : "StageSelectScene"),
                "the shared exit leads to the next tutorial, or the hub after tutorial five");
        }
        Reset();
        var retry = Progression(3, false);
        Keyboard.current.escapeKey.wasPressedThisFrame = true;
        Advance(retry, .6f);
        Check(SceneManager.Loaded == "SafeRoom" && SafeRoomTransit.TryConsume(out string destination, out bool arrival)
            && destination == "Stage03" && !arrival, "escape returns to a room that retries this tutorial without a clear entrance");
        Check(StageRecordStore.Saves == 0, "escape is not a recorded clear");
        Reset();
        var first = Progression(1, false);
        Keyboard.current.escapeKey.wasPressedThisFrame = true;
        Advance(first, .6f);
        Check(SceneManager.Loaded == "AwakeningScene", "tutorial one retains its opening-scene back route");
        Reset(); StageSelection.IsRecordRun = true;
        Advance(Progression(3, true), .7f);
        Check(SceneManager.Loaded == "StageSelectScene" && !SafeRoomTransit.TryConsume(out _, out _),
            "record-run clears return to the hub without leaking a tutorial route");
        Reset(); StageSelection.IsRecordRun = true;
        var record = Progression(3, false);
        Keyboard.current.escapeKey.wasPressedThisFrame = true;
        Advance(record, .6f);
        Check(SceneManager.Loaded == "StageSelectScene", "record-run escape still returns to the hub");
        Reset();
        var direct = new RoomRig();
        Check(direct.Player.anchoredPosition.x == 0 && direct.Upper.anchoredPosition.y == 27,
            "direct Editor launch starts inside a closed entrance without stale arrival state");
        Advance(direct.Controller, .6f);
        Keyboard.current.dKey.wasPressedThisFrame = true;
        Advance(direct.Controller, 1);
        Check(SceneManager.Loaded == "Stage02", "direct launch uses the authored fallback destination");
        Reset();
        SafeRoomTransit.Prepare("Stage04", true);
        SafeRoomTransit.Reset();
        Check(!SafeRoomTransit.TryConsume(out _, out bool stale) && !stale, "play-session reset clears the handoff");
        Console.WriteLine($"PASS: {checks} shared safe room arrival, door order, input, tutorial routing and hub return checks.");
    }
}

namespace UnityEngine
{
    public sealed class SerializeField : Attribute { }
    public sealed class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public sealed class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int value) { } }
    public class MonoBehaviour
    {
        public bool enabled = true;
        public GameObject gameObject = new GameObject();
        private readonly List<Routine> routines = new List<Routine>();
        protected void StartCoroutine(IEnumerator value) { var routine = new Routine(value); if (routine.Step()) routines.Add(routine); }
        public void AdvanceCoroutines() { for (int i = routines.Count-1; i >= 0; i--) if (!routines[i].Step()) routines.RemoveAt(i); }
        private sealed class Routine
        {
            private readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
            private float resumeAt;
            public Routine(IEnumerator value) { stack.Push(value); }
            public bool Step()
            {
                if (Time.unscaledTime < resumeAt) return true;
                while (stack.Count > 0)
                {
                    var top = stack.Peek();
                    if (!top.MoveNext()) { stack.Pop(); continue; }
                    if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                    if (top.Current is WaitForSecondsRealtime wait) resumeAt = Time.unscaledTime + wait.Seconds;
                    return true;
                }
                return false;
            }
        }
    }
    public sealed class GameObject { public Scene scene; }
    public class Transform { public Transform parent; public Quaternion localRotation; }
    public sealed class RectTransform : Transform
    {
        public Vector2 anchoredPosition;
        public T GetComponentInParent<T>() where T : new() => new T();
    }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2();
        public float sqrMagnitude => x*x + y*y;
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x-b.x, a.y-b.y);
        public static float Distance(Vector2 a, Vector2 b) => (float)Math.Sqrt((a-b).sqrMagnitude);
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => new Vector2(a.x+(b.x-a.x)*t, a.y+(b.y-a.y)*t);
    }
    public struct Quaternion { public static Quaternion Euler(float x, float y, float z) => default; }
    public sealed class Camera { }
    public enum RenderMode { ScreenSpaceOverlay }
    public sealed class Canvas { public RenderMode renderMode; public Camera worldCamera; }
    public sealed class CanvasGroup { public float alpha; }
    public static class RectTransformUtility
    { public static bool ScreenPointToLocalPointInRectangle(RectTransform rect, Vector2 point, Camera camera, out Vector2 local) { local = point; return true; } }
    public static class Time { public static float unscaledTime, unscaledDeltaTime; }
    public static class Mathf
    {
        public const float Rad2Deg = 57.29578f;
        public static float Clamp01(float t) => Math.Max(0, Math.Min(1, t));
        public static float Max(float a, float b) => Math.Max(a,b);
        public static float Abs(float a) => Math.Abs(a);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y,x);
        public static float SmoothStep(float a, float b, float t) { t=Clamp01(t); return a+(b-a)*t*t*(3-2*t); }
        public static float MoveTowards(float a, float b, float delta) => Math.Abs(b-a)<=delta ? b : a+Math.Sign(b-a)*delta;
    }
    public static class Application { public static bool CanStreamedLevelBeLoaded(string scene) => !string.IsNullOrWhiteSpace(scene); }
    public static class Debug { public static void LogError(string error, object context) => throw new Exception(error); }
    public sealed class WaitForSecondsRealtime { public float Seconds; public WaitForSecondsRealtime(float seconds) { Seconds=seconds; } }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { public string name; }
    public static class SceneManager { public static string Loaded; public static object LoadSceneAsync(string name) { Loaded=name; return null; } }
}
namespace UnityEngine.InputSystem
{
    public sealed class Button { public bool wasPressedThisFrame; }
    public sealed class Keyboard { public static Keyboard current; public Button dKey=new Button(), escapeKey=new Button(); }
    public sealed class Dpad { public Button right=new Button(); }
    public sealed class Gamepad { public static Gamepad current; public Dpad dpad=new Dpad(); public Button selectButton=new Button(); }
    public sealed class Position { public Vector2 ReadValue() => default; }
    public sealed class Mouse { public static Mouse current; public Position position=new Position(); }
}
namespace Gun.RoomRhythm
{
    public static class StageSelection { public static bool IsRecordRun; public static string ReturnScene; }
    public static class StageRecordStore { public static int Saves; public static void SaveClear(string scene, float value) { Saves++; } }
    public sealed class RoomSession
    {
        public bool IsCleared, IsDebugRun, HasStarted;
        public float AccuracyPercent = 100;
        public void TryBeginRun() { HasStarted = true; }
    }
}
