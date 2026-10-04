using System;
using System.Reflection;
using Gun.RoomRhythm;
using UnityEngine;

// The production RoomEnemy/RoomCombat/RoomRun are compiled with minimal engine boundaries below.
internal static class EnemyRestartChecks
{
    private static int checks;
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception("FAIL: " + message); checks++; }
    private static void Set(object target, string name, object value)
        => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Near(Vector3 actual, Vector3 expected, string message)
        => Check(Vector3.Distance(actual, expected) < .0001f, message);
    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (InvalidOperationException error)
        { Check(error.Message == "Enemy scene position differs from chart: mafia_corridor_01", message); return; }
        throw new Exception("FAIL: " + message);
    }
    public static void Main()
    {
        Application.isPlaying = true;
        var chart = new RoomChart {
            moves = new[] {
                new MoveNote { destinationId = "corridor", direction = MoveDirection.Right, time = 2 },
                new MoveNote { destinationId = "office", direction = MoveDirection.Right, time = 10 }
            },
            enemies = new[] { new EnemyNote { id = "mafia_corridor_01", roomId = "corridor", time = 5,
                placement = new EnemyPlacement { useCoordinates = true, x = 2.2f, y = 1.8f } } }
        };
        RoomRun NewRun() => new RoomRun(chart.moves, chart.Timing, .1, chart.enemies, "start", .25, 1);
        var path = new[] { new RoomBinding(), new RoomBinding(), new RoomBinding() };
        path[1].transform.position = new Vector3(15, -6, 0);
        var enemy = new RoomEnemy();
        Set(enemy, "enemyId", "mafia_corridor_01");
        var visuals = new GameObject();
        var body = new SpriteRenderer();
        var outline = new LineRenderer();
        var frame = new Transform();
        var ring = new LineRenderer();
        Set(enemy, "visuals", visuals);
        Set(enemy, "body", body);
        Set(enemy, "outline", outline);
        Set(enemy, "judgmentFrame", frame);
        Set(enemy, "timingRing", ring);
        var combat = new RoomCombat();
        Set(combat, "enemies", new[] { enemy });
        Set(combat, "selectedEnemyDot", new Transform());
        Set(combat, "player", new Transform());
        Vector3 placed = RoomEnemy.Position(path[1].Center, chart.enemies[0].direction, chart.enemies[0].placement, chart.aimRadius);
        var run = NewRun();
        enemy.transform.position = placed + new Vector3(1, 0, 0);
        Reject(() => combat.ValidateConfiguration(chart, path, run), "initial incorrect scene placement is rejected");
        enemy.transform.position = placed;
        combat.ValidateConfiguration(chart, path, run);

        foreach (double deathTime in new[] { 0.0, 4.1, 4.3, 5.0 })
        {
            combat.Configure(chart, path, run, new RoomAim(), new RoomFeedback(), new[] { -1, -1, -1 });
            Near(enemy.Target, placed, "configuration restores the chart placement");
            combat.SetCorridorEntrances("corridor", path[1].Center, 6);
            run.Begin();
            if (deathTime >= 2) { run.Press(MoveDirection.Right, 2); run.Advance(2.1); }
            combat.Present(deathTime);
            Near(enemy.Target, enemy.TargetAt(deathTime), "displayed enemy follows its entrance path");
            if (deathTime < 4.3)
                Check(Vector3.Distance(enemy.Target, placed) > .1f, "hidden or entering enemy differs from chart position");
            Near(enemy.TargetAt(5), placed, "entrance settles on the exact judgment position");
            run.Advance(20);
            Check(run.Phase == RunPhase.Dead, "test reaches the dead state");
            combat.FreezeAtDeath(deathTime);
            combat.Present(deathTime);
            combat.Suspend();
            Vector3 frozen = enemy.Target;
            run = NewRun();
            // Same order as RoomSession.InitializeRun: validate BEFORE combat.Configure resets visuals.
            combat.ValidateConfiguration(chart, path, run);
            Near(enemy.Target, frozen, "restart validation does not mutate the death/entrance pose");
            Check(run.Phase == RunPhase.Ready, "recreated run remains ready for a new start input");
        }

        foreach (bool automatic in new[] { false, true })
        {
            run = NewRun();
            var feedback = new RoomFeedback();
            combat.Configure(chart, path, run, new RoomAim(), feedback, new[] { -1, -1, -1 });
            combat.SetCorridorEntrances("corridor", path[1].Center, 6);
            Time.unscaledTime = 10;
            Check(!combat.HasVisibleEnemies(1, 5), "ready state does not enable combat zoom");
            run.Begin();
            Check(!combat.HasVisibleEnemies(1, 3.9), "hidden future enemy does not enable combat zoom");
            Check(combat.HasVisibleEnemies(1, 4), "combat zoom starts at enemy appearance");
            Check(!combat.HasVisibleEnemies(0, 4), "enemy in another room does not zoom the current room");
            if (automatic)
                run.AdvanceAutomatically(5, result => combat.PresentDebugAction(result, Vector3.zero));
            else
            {
                run.Press(MoveDirection.Right, 2); run.Advance(5);
                combat.Shoot(placed, 5, Vector3.zero);
            }
            combat.Present(5);
            Check(run.EnemyDefeated(0) && !run.EnemyAvailable(0), "falling enemy immediately leaves gameplay targeting");
            Check(!combat.HasVisibleEnemies(1, 5), "defeated enemy does not hold combat zoom");
            Check(feedback.EnemyDeaths == 1, "manual and automatic hits preserve the existing death burst");
            Check(visuals.activeSelf, "defeated enemy remains visible to fall");
            Check(!outline.enabled && !ring.enabled && !frame.gameObject.activeSelf, "death hides every judgment indicator");
            Vector3 hitPosition = enemy.Target;
            Vector3 hitBodyPosition = body.transform.position;
            Time.unscaledTime = 10.2f;
            combat.Present(5);
            Check(Vector3.Distance(body.transform.position, hitBodyPosition) > .2f, "body is knocked back along the shot");
            Check(body.transform.localScale.y < .85f && Math.Abs(body.transform.localRotation.zDegrees) > 30,
                "sprite turns and collapses while chart time is frozen");
            Near(enemy.Target, hitPosition, "fall moves the body without moving the target root");
            combat.ValidateConfiguration(chart, path, run);
            float fallenAngle = body.transform.localRotation.zDegrees;
            enemy.AimAt(new Vector3(-100, 20, 0));
            Check(body.transform.localRotation.zDegrees == fallenAngle, "corpse never aims at the player");
            run.Advance(20);
            combat.FreezeAtDeath(run.DeathTime);
            Time.unscaledTime = 10.75f;
            combat.Present(run.DeathTime);
            Check(visuals.activeSelf && body.color.a > 0 && body.color.a < 1,
                "corpse fades even if the player dies and music stops");
            enemy.Defeat(5, new Vector3(-1, 0, 0));
            Time.unscaledTime = 10.95f;
            combat.Present(run.DeathTime);
            Check(!visuals.activeSelf, "corpse expires and duplicate death calls cannot restart it");
            run = NewRun();
            combat.ValidateConfiguration(chart, path, run);
            combat.Configure(chart, path, run, new RoomAim(), new RoomFeedback(), new[] { -1, -1, -1 });
            Near(body.transform.localPosition, Vector3.zero, "retry restores the sprite position");
            Near(body.transform.localScale, Vector3.one, "retry restores sprite size");
            Check(Math.Abs(body.transform.localRotation.zDegrees) < .001f && body.color.a == 1 && outline.enabled && ring.enabled,
                "retry restores rotation, opacity and judgment renderers");
            combat.Present(0);
            Check(!visuals.activeSelf, "old corpses do not appear in the ready state");
        }

        run = NewRun();
        combat.Configure(chart, path, run, new RoomAim(), new RoomFeedback(), new[] { -1, -1, -1 });
        Time.unscaledTime = 20;
        run.Begin(); run.Press(MoveDirection.Right, 2); run.Advance(5);
        combat.Shoot(placed, 5, Vector3.zero);
        Time.unscaledTime = 20.1f;
        run.Press(MoveDirection.Right, 10); run.Advance(10.2);
        combat.Present(10.2);
        Check(!visuals.activeSelf, "corpse disappears with its departed room even before the fade ends");

        // A quick retry can reach the covered reset while the corpse is still falling.
        run = NewRun();
        combat.Configure(chart, path, run, new RoomAim(), new RoomFeedback(), new[] { -1, -1, -1 });
        Time.unscaledTime = 30;
        run.Begin();
        run.AdvanceAutomatically(5, result => combat.PresentDebugAction(result, Vector3.zero));
        Time.unscaledTime = 30.15f;
        combat.Present(5);
        Check(visuals.activeSelf && body.transform.localScale.y < 1, "quick retry begins during an unfinished fall");
        combat.Suspend();
        run = NewRun();
        combat.ValidateConfiguration(chart, path, run);
        combat.Configure(chart, path, run, new RoomAim(), new RoomFeedback(), new[] { -1, -1, -1 });
        combat.Present(0);
        Near(body.transform.localPosition, Vector3.zero, "quick retry removes the in-progress knockback");
        Near(body.transform.localScale, Vector3.one, "quick retry removes the in-progress collapse");
        Check(!visuals.activeSelf && body.color.a == 1 && body.transform.localRotation.zDegrees == 0,
            "quick retry returns to ready with an intact hidden enemy");

        var changedNote = chart.enemies[0];
        changedNote.placement.x += 1;
        chart.enemies[0] = changedNote;
        Reject(() => combat.ValidateConfiguration(chart, path, run), "genuine chart/placement mismatch is still rejected");
        changedNote.placement.x -= 1;
        chart.enemies[0] = changedNote;
        // An editor drag after Configure must be validated from the transform, not the runtime cache.
        Application.isPlaying = false;
        enemy.transform.position = placed + new Vector3(1, 0, 0);
        Reject(() => combat.ValidateConfiguration(chart, path, run), "editor scene edits are not hidden by cached placement");
        enemy.transform.position = placed;
        combat.ValidateConfiguration(chart, path, run);
        Check(true, "corrected editor placement validates");
        Console.WriteLine($"PASS: {checks} enemy entrance/fall/death/restart checks (engine test doubles; no Unity play mode).");
    }
}

namespace UnityEngine
{
    public sealed class SerializeField : Attribute { }
    public sealed class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int order) { } }
    public sealed class GameObject
    {
        public int scene;
        public bool activeSelf = true;
        public void SetActive(bool value) => activeSelf = value;
    }
    public sealed class Transform
    {
        public readonly GameObject gameObject = new GameObject();
        public Vector3 position, localScale = Vector3.one, lossyScale = Vector3.one;
        public Vector3 localPosition { get => position; set => position = value; }
        public Quaternion localRotation;
        public Quaternion rotation { get => localRotation; set => localRotation = value; }
        public Vector3 up => new Vector3(0, 1, 0);
        public Vector3 right => new Vector3(1, 0, 0);
        public Vector3 TransformPoint(Vector3 point) => position + point;
    }
    public class MonoBehaviour
    {
        public bool enabled = true;
        public readonly Transform transform = new Transform();
        public GameObject gameObject => transform.gameObject;
    }
    public sealed class SpriteRenderer : MonoBehaviour { public Color color = Color.white; }
    public sealed class LineRenderer : MonoBehaviour
    {
        public bool useWorldSpace, loop;
        public int positionCount;
        public float startWidth, endWidth;
        public Color startColor, endColor;
        public void SetPosition(int index, Vector3 value) { }
    }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float sqrMagnitude => x * x + y * y;
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public Vector2 normalized => magnitude > 0 ? new Vector2(x / magnitude, y / magnitude) : default;
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y);
        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0);
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z = 0) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => default;
        public static Vector3 one => new Vector3(1, 1, 1);
        public float sqrMagnitude => x * x + y * y + z * z;
        public static Vector3 operator -(Vector3 v) => v * -1;
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        public Vector3 normalized
        { get { float length = Distance(this, zero); return length > 0 ? this * (1 / length) : zero; } }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float b) => new Vector3(a.x * b, a.y * b, a.z * b);
        public static float Distance(Vector3 a, Vector3 b)
        { var d = a - b; return (float)Math.Sqrt(d.x * d.x + d.y * d.y + d.z * d.z); }
    }
    public struct Quaternion
    {
        public float zDegrees;
        public static Quaternion Euler(float x, float y, float z) => new Quaternion { zDegrees = z };
        public static Quaternion operator *(Quaternion a, Quaternion b) => Euler(0, 0, a.zDegrees + b.zDegrees);
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color red => new Color(1, 0, 0, 1);
        public static Color white => new Color(1, 1, 1, 1);
        public static Color Lerp(Color a, Color b, float t) => new Color(Mathf.Lerp(a.r, b.r, t),
            Mathf.Lerp(a.g, b.g, t), Mathf.Lerp(a.b, b.b, t), Mathf.Lerp(a.a, b.a, t));
    }
    public static class Mathf
    {
        public const float PI = (float)Math.PI, Rad2Deg = 180 / PI;
        public static float Clamp(float v, float a, float b) => Math.Max(a, Math.Min(b, v));
        public static float Clamp01(float v) => Clamp(v, 0, 1);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Asin(float v) => (float)Math.Asin(v);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Pow(float value, float exponent) => (float)Math.Pow(value, exponent);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float InverseLerp(float a, float b, float value) => Clamp01((value - a) / (b - a));
        public static float SmoothStep(float a, float b, float t) { t = Clamp01(t); return Lerp(a, b, t * t * (3 - 2 * t)); }
    }
    public static class Time { public static float unscaledTime; }
    public static class Application { public static bool isPlaying; }
    public sealed class AudioClip { public float length = 100; }
}
namespace UnityEngine.InputSystem
{
    public sealed class Mouse
    {
        public static Mouse current;
        public readonly PositionControl position = new PositionControl();
        public sealed class PositionControl { public Vector2 ReadValue() => default; }
    }
}
namespace Gun.RoomRhythm
{
    public sealed class RoomChart
    {
        public MoveNote[] moves;
        public EnemyNote[] enemies;
        public AudioClip music = new AudioClip();
        public bool LoopMusic;
        public double MusicDelaySeconds;
        public float aimRadius = 2, aimHalfAngle = 45, enemyLineWidth = .04f;
        public const float UpcomingEnemyBrightness = 1;
        public TimingWindow Timing => new TimingWindow { early = .1, accurate = .03, late = .1 };
        public double RoomAppearsAt(MoveNote note) => note.appearTime;
    }
    public sealed class RoomBinding : MonoBehaviour { public Vector3 Center => transform.position; public RoomDoor Door; }
    public sealed class RoomDoor { public Vector3 Target; public Color FragmentColor; }
    public sealed class RoomAim
    {
        public Vector2 DirectionAt(Vector2 pointer, Vector3 origin) => (pointer - (Vector2)origin).normalized;
        public void Fire(Vector3 origin, Vector2 direction, Vector3? target) { }
    }
    public sealed class RoomFeedback
    {
        public int EnemyDeaths;
        public void EnemyDeath(Vector3 position, Vector3 direction) { EnemyDeaths++; }
        public void DoorBreak(Vector3 position, Vector3 direction, Color color) { }
    }
    public static class RoomPalette { public static Color Tint(Color color) => color; }
    public enum StageTargetRole { Shot, Breakthrough }
    public sealed class StageActionTarget : MonoBehaviour
    {
        public string Id;
        public StageTargetRole Role;
        public void ValidateOutside(Transform node) { }
        public void ValidateReferences() { }
        public void ResetTarget(RoomChart chart, double appearance, double target) { }
        public Vector3 PositionAt(double time) => default;
        public void OnHit(double time) { }
        public void OnFailure(double time) { }
        public void Present(bool visible, bool frame, double time, bool next) { }
    }
}
