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
        private SpriteRenderer player, roomFlash;
        private readonly SpriteRenderer[] shutters = new SpriteRenderer[8];
        private Vector3 origin, center, direction;
        private float side, passage, thickness, started, lastTrail;
        private bool combatFocus;
        private float focusChangedAt = -10, zoomFrom = 1;
        private bool impact;
        public DeathPresentation Death { get; private set; }
        public bool CameraLocked => Death != DeathPresentation.None;
        public Vector3 CameraCenter => center;
        public float DeathElapsed => Death == DeathPresentation.None ? 0 : Time.unscaledTime - started;
        public bool Complete => Death != DeathPresentation.None && DeathElapsed >= 1.3f;
        public float PlayerAlpha => Death == DeathPresentation.Execution && DeathElapsed >= .24f ? 0 : 1;
        public static Color DeathFlash(float elapsed)
        {
            float alpha = elapsed < .06f ? Mathf.Clamp01(elapsed / .06f) : 1 - Mathf.InverseLerp(.06f, .24f, elapsed);
            return new Color(.52f, .27f, .28f, alpha * .85f);
        }
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
            if (roomFlash == null)
            {
                roomFlash = feedback.CreateVisual(transform, "Room death flash", Color.white);
                roomFlash.sortingOrder = 2100;
                for (int i = 0; i < shutters.Length; i++)
                    shutters[i] = feedback.CreateVisual(transform, "Sealing door " + i, Color.white);
            }
            ResetPresentation();
        }

        public void ResetPresentation()
        {
            Death = DeathPresentation.None; impact = false; lastTrail = -10;
            combatFocus = false; focusChangedAt = -10; zoomFrom = 1;
            if (roomFlash == null) return;
            roomFlash.gameObject.SetActive(false);

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

        }

        private void LateUpdate()
        {
            if (Death == DeathPresentation.None) return;
            float t = DeathElapsed;
            if (Death == DeathPresentation.Execution)
            {
                roomFlash.gameObject.SetActive(t < .24f);
                roomFlash.transform.position = center;
                RoomFeedback.Size(roomFlash, Vector2.one * side);
                roomFlash.color = DeathFlash(t);
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

