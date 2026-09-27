using UnityEngine;

namespace Gun.RoomRhythm
{
    public static class RoomPalette
    {
        public static Color Tint(Color color, float saturation = .7f)
        {
            float gray = color.grayscale;
            return Color.Lerp(new Color(gray, gray, gray, color.a), color, saturation);
        }
    }

    public sealed class RoomCinematics : MonoBehaviour
    {
        private RoomFeedback feedback;
        private SpriteRenderer player, trace, traceCore;
        private readonly SpriteRenderer[] shutters = new SpriteRenderer[8];
        private Vector3 origin, center, direction, shooter;
        private float side, passage, thickness, started, lastTrail;
        private bool combatFocus;
        private float focusChangedAt = -10, zoomFrom = 1;
        private bool impact;
        public DeathPresentation Death { get; private set; }
        public bool CameraLocked => Death != DeathPresentation.None;
        public Vector3 CameraCenter => center;
        public float DeathElapsed => Death == DeathPresentation.None ? 0 : Time.unscaledTime - started;
        public bool Complete => Death != DeathPresentation.None && DeathElapsed >= 1.3f;
        private float HitAt => .49f + .08f * Vector3.Distance(shooter, origin) / (Vector3.Distance(shooter, origin) + side);
        public float PlayerAlpha => Death == DeathPresentation.Execution
            ? 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(HitAt, HitAt + .7f, DeathElapsed)) : 1;
        public float ZoomMultiplier
        {
            get
            {
                float t = Mathf.Clamp01((Time.unscaledTime - focusChangedAt) / (combatFocus ? .12f : .35f));
                return Mathf.Lerp(zoomFrom, combatFocus ? .82f : 1f, Mathf.SmoothStep(0, 1, t));
            }
        }
        public bool PlayerVisible => Death == DeathPresentation.None
            || Death == DeathPresentation.Execution && PlayerAlpha > 0
            || Death == DeathPresentation.Collision && DeathElapsed < .13f
            || Death == DeathPresentation.Departure && DeathElapsed < .28f;
        public Vector3 DeathPosition => Death == DeathPresentation.Collision
            ? Vector3.Lerp(origin, center + direction * (side * .5f - .28f), Mathf.Clamp01(DeathElapsed / .13f))
            : Death == DeathPresentation.Departure ? origin + direction * side * Mathf.Clamp01(DeathElapsed / .28f) : origin;

        public void Configure(SpriteRenderer body, RoomFeedback effects)
        {
            player = body; feedback = effects;
            if (trace == null)
            {
                trace = feedback.CreateVisual(transform, "Piercing shot", Color.white, true);
                traceCore = feedback.CreateVisual(transform, "Piercing shot core", Color.white);
                trace.sortingOrder = 2100;
                traceCore.sortingOrder = 2101;
                for (int i = 0; i < shutters.Length; i++)
                    shutters[i] = feedback.CreateVisual(transform, "Sealing door " + i, Color.white);
            }
            ResetPresentation();
        }

        public void ResetPresentation()
        {
            Death = DeathPresentation.None; impact = false; lastTrail = -10;
            combatFocus = false; focusChangedAt = -10; zoomFrom = 1;
            if (trace == null) return;
            trace.gameObject.SetActive(false);
            traceCore.gameObject.SetActive(false);
            foreach (var shutter in shutters) shutter.gameObject.SetActive(false);
            player.enabled = true;
        }

        public void SetCombatFocus(bool active)
        {
            if (active != combatFocus)
            {
                zoomFrom = ZoomMultiplier;
                focusChangedAt = Time.unscaledTime;
                combatFocus = active;
            }
        }
        public void Trail()
        {
            if (Time.unscaledTime - lastTrail < .025f) return;
            lastTrail = Time.unscaledTime; feedback.Afterimage(player);
        }

        public void BeginDeath(RoomRun run, RoomBinding room, RoomChart chart, Vector3 position, SpriteRenderer enemy)
        {
            Death = run.Death; started = Time.unscaledTime; impact = false;
            origin = position; center = room.Center; side = room.SideLength;
            passage = chart.passageWidth; thickness = chart.judgmentLineWidth;
            direction = run.DeathDirection == MoveDirection.Up ? Vector3.up : run.DeathDirection == MoveDirection.Down ? Vector3.down
                : run.DeathDirection == MoveDirection.Left ? Vector3.left : Vector3.right;
            shooter = enemy != null ? enemy.transform.position : origin + Vector3.up * side * .45f;
        }

        private void LateUpdate()
        {
            if (Death == DeathPresentation.None) return;
            float t = DeathElapsed;
            if (Death == DeathPresentation.Execution)
            {
                float alpha = Mathf.Clamp01(1 - (t - .62f) / .28f);
                bool show = t >= .49f && t < .9f;
                trace.gameObject.SetActive(show);
                traceCore.gameObject.SetActive(show);
                Vector3 ray = (origin - shooter).normalized;
                Vector3 end = Vector3.Lerp(shooter, origin + ray * side, Mathf.Clamp01((t - .49f) / .08f));
                trace.transform.position = (shooter + end) * .5f;
                trace.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(ray.y, ray.x) * Mathf.Rad2Deg);
                float length = Vector3.Distance(shooter, end);
                RoomFeedback.Size(trace, new Vector2(length, .28f));
                trace.color = new Color(1, .8f, .3f, alpha);
                traceCore.transform.SetPositionAndRotation(trace.transform.position, trace.transform.rotation);
                RoomFeedback.Size(traceCore, new Vector2(length, .065f));
                traceCore.color = new Color(1, 1, .95f, alpha);
                if (!impact && t >= HitAt)
                {
                    impact = true; feedback.DeathBlood(origin, ray);
                    feedback.DeathSpark(shooter);
                }
            }
            else if (Death == DeathPresentation.Collision)
            {
                if (!impact && t >= .13f)
                { impact = true; feedback.Shatter(DeathPosition, player.sprite, direction); }
            }
            else
            {
                float close = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.28f, .44f, t));
                for (int i = 0; i < shutters.Length; i++)
                {
                    var shutter = shutters[i]; shutter.gameObject.SetActive(t >= .28f);
                    Quaternion rotation = Quaternion.Euler(0, 0, (i / 2) * 90);
                    float offset = Mathf.Lerp(passage * .75f, passage * .25f, close) * (i % 2 == 0 ? -1 : 1);
                    shutter.transform.position = center + rotation * new Vector3(offset, side * .5f, 0);
                    shutter.transform.rotation = rotation;
                    RoomFeedback.Size(shutter, new Vector2(passage * .5f, thickness));
                    shutter.color = Color.Lerp(Color.white, new Color(.75f, .75f, .75f), Mathf.Clamp01((t - .44f) / .15f));
                }
            }
        }

        private void OnDisable() => ResetPresentation();
    }
}
