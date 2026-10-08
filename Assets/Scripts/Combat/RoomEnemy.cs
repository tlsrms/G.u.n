using UnityEngine;

namespace Gun.RoomRhythm
{
    public sealed class RoomEnemy : MonoBehaviour
    {
        [SerializeField] private string enemyId;
        [SerializeField] private GameObject visuals;
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private LineRenderer outline;
        [SerializeField] private Transform judgmentFrame;
        [SerializeField] private LineRenderer timingRing;
        private RoomChart chart;
        private Vector3 placementPosition, entranceOffset;
        private double entranceStart, entranceDuration;
        private bool hasBodyPose, defeated;
        private Vector3 bodyPosition, bodyScale, defeatPosition, defeatDirection;
        private Quaternion bodyRotation, defeatRotation;
        private Color bodyColor;
        private Sprite standingSprite;
        private static Sprite fallenSprite;
        private float defeatedAt, defeatTurn;
        private const float FallDuration = .32f, FadeStartsAt = 1.05f, DefeatDuration = 1.45f;
        private const float OutlineRadius = 0.38f;
        private const int Segments = 128;
        public string Id => enemyId;
        public Vector3 Target => transform.position;
        // Scene validation checks the configured placement, not a temporary entrance/death pose.
        // Before runtime configuration (and in the editor), still validate the actual authored transform.
        public Vector3 PlacementPosition => Application.isPlaying && chart != null ? placementPosition : transform.position;
        public Vector3 TargetAt(double time)
        {
            if (entranceDuration <= 0) return placementPosition;
            float t = Mathf.Clamp01((float)((time - entranceStart) / entranceDuration));
            // A quick step out of cover, a small overshoot, then an exact settle.
            float u = t - 1;
            float reveal = 1 + 2.1f * u * u * u + 1.1f * u * u;
            return placementPosition + entranceOffset * (1 - reveal);
        }
        public void SetEntrance(Vector3 offset, double startsAt, double duration)
        { entranceOffset = offset; entranceStart = startsAt; entranceDuration = System.Math.Max(0, duration); }
        public SpriteRenderer Body => body;
        [SerializeField] private Vector2 muzzlePoint = new Vector2(.215f, .65f);
        public Vector3 Muzzle => body.transform.TransformPoint(muzzlePoint);

        public void AimAt(Vector3 target)
        {
            if (defeated) return;
            Vector2 direction = target - body.transform.position;
            if (direction.sqrMagnitude < .001f) return;
            // Compensate for the one-handed pistol's sideways offset.
            float offset = muzzlePoint.x * body.transform.lossyScale.x;
            float correction = Mathf.Asin(Mathf.Clamp(offset / direction.magnitude, -.95f, .95f)) * Mathf.Rad2Deg;
            body.transform.rotation = Quaternion.Euler(0, 0,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90 + correction);
        }
        public void ValidateReferences()
        {
            if (visuals == null || body == null || outline == null || judgmentFrame == null || timingRing == null)
                throw new System.InvalidOperationException("Missing enemy visuals: " + enemyId);
        }

        public static Vector3 Position(Vector3 roomCenter, EnemyDirection direction, EnemyPlacement placement, float radius)
        {
            var offset = placement.Offset(direction, radius);
            return roomCenter + new Vector3(offset.x, offset.y, 0);
        }

        public void Configure(Vector3 roomCenter, EnemyDirection direction, RoomChart settings, EnemyPlacement placement = default)
        {
            if (!hasBodyPose)
            {
                bodyPosition = body.transform.localPosition;
                bodyRotation = body.transform.localRotation;
                bodyScale = body.transform.localScale;
                bodyColor = body.color;
                standingSprite = body.sprite;
                hasBodyPose = true;
            }
            if (fallenSprite == null) fallenSprite = Resources.Load<Sprite>("RegularEnemyFallen");
            defeated = false;
            body.sprite = standingSprite;
            body.transform.localPosition = bodyPosition;
            body.transform.localRotation = bodyRotation;
            body.transform.localScale = bodyScale;
            body.color = bodyColor;
            chart = settings;
            placementPosition = Position(roomCenter, direction, placement, chart.aimRadius);
            transform.position = placementPosition;
            entranceOffset = Vector3.zero; entranceDuration = 0;
            judgmentFrame.localScale = Vector3.one;
            foreach (LineRenderer line in new[] { outline, timingRing })
            {
                line.transform.localScale = Vector3.one;
                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = Segments;
                line.startWidth = line.endWidth = chart.enemyLineWidth;
            }
            SetRadius(outline, OutlineRadius);
            outline.enabled = timingRing.enabled = true;
        }

        public void Defeat(double hitTime, Vector3 direction)
        {
            if (defeated) return;
            // Freeze the entrance at the actual hit position. Only the sprite falls;
            // the chart placement and the now-defeated gameplay target remain unchanged.
            transform.position = TargetAt(hitTime);
            defeated = true;
            defeatedAt = Time.unscaledTime;
            defeatPosition = body.transform.position;
            defeatRotation = body.transform.localRotation;
            direction.z = 0;
            defeatDirection = direction.sqrMagnitude > .0001f ? direction.normalized : -body.transform.up;
            defeatTurn = Vector3.Dot(body.transform.right, defeatDirection) >= 0 ? -8f : 8f;
            judgmentFrame.gameObject.SetActive(false);
            outline.enabled = timingRing.enabled = false;
            PresentDefeat(true);
        }

        private void PresentDefeat(bool visible)
        {
            // Keep playing after music stops (e.g. the player dies on the following note).
            float elapsed = Mathf.Max(0, Time.unscaledTime - defeatedAt);
            visuals.SetActive(visible && elapsed < DefeatDuration);
            if (!visible || elapsed >= DefeatDuration) return;
            // The standing art is a top view: spinning it cannot read as lying down.
            // Under the hit flash, unfold a full-body fallen pose toward the floor.
            float fall = Mathf.Clamp01((elapsed - .07f) / FallDuration);
            float settle = Mathf.SmoothStep(0, 1, fall);
            float kick = 1 - Mathf.Pow(1 - Mathf.Clamp01(elapsed / .24f), 3);
            float impact = Mathf.Sin(Mathf.Clamp01((elapsed - .35f) / .12f) * Mathf.PI);
            bool lying = elapsed >= .07f && fallenSprite != null;
            body.sprite = lying ? fallenSprite : standingSprite;
            body.transform.position = defeatPosition + defeatDirection * (.30f * kick);
            body.transform.localRotation = defeatRotation * Quaternion.Euler(0, 0, defeatTurn * settle);
            body.transform.localScale = Vector3.Scale(bodyScale,
                new Vector3(1 + .035f * impact, lying ? .32f + .68f * settle - .04f * impact
                    : 1 - .15f * Mathf.Clamp01(elapsed / .07f), 1));
            Color tint = Color.Lerp(bodyColor, new Color(.66f, .66f, .69f, bodyColor.a), settle);
            tint = Color.Lerp(tint, Color.white, 1 - Mathf.Clamp01(elapsed / .09f));
            tint.a = bodyColor.a * (1 - Mathf.SmoothStep(0, 1,
                Mathf.InverseLerp(FadeStartsAt, DefeatDuration, elapsed)));
            body.color = tint;
        }

        private static void SetRadius(LineRenderer line, float radius)
        {
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * 2f * Mathf.PI / Segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        public static Color TimingColor(bool next) => next ? Color.red : RoomPalette.Tint(new Color(1f, .3f, .6f, 1f));

        public void Present(bool visible, float progress, double time = 0, double target = 0, bool showFrame = true, float brightness = 1, bool next = false, bool showDefeat = false)
        {
            if (defeated) { PresentDefeat(showDefeat); return; }
            visuals.SetActive(visible);
            transform.position = TargetAt(time);
            if (!visible) return;
            judgmentFrame.gameObject.SetActive(showFrame);
            body.color = Color.white;
            // Appearance timing controls visibility, never opacity or brightness.
            outline.startColor = outline.endColor = Color.white;
            SetRadius(timingRing, (float)ApproachGeometry.Radius(time, target,
                OutlineRadius, chart.enemyLineWidth, chart.Timing));
            timingRing.startColor = timingRing.endColor = TimingColor(next);
        }
    }
}
