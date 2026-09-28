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
        private const float OutlineRadius = 0.38f;
        private const int Segments = 128;
        public string Id => enemyId;
        public Vector3 Target => transform.position;
        public SpriteRenderer Body => body;
        public void ValidateReferences()
        {
            if (visuals == null || body == null || outline == null || judgmentFrame == null || timingRing == null)
                throw new System.InvalidOperationException("Missing enemy visuals: " + enemyId);
        }

        public void Configure(Vector3 roomCenter, EnemyDirection direction, RoomChart settings)
        {
            chart = settings;
            float angle = (90f - (int)direction * 45f) * Mathf.Deg2Rad;
            transform.position = roomCenter + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * chart.aimRadius;
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

        public void Present(bool visible, float progress, double time = 0, double target = 0, bool showFrame = true, float brightness = 1, bool next = false)
        {
            visuals.SetActive(visible);
            if (!visible) return;
            judgmentFrame.gameObject.SetActive(showFrame);
            float reveal = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.65f, 1f, progress));
            body.color = Dim(Color.Lerp(new Color(.12f, .12f, .12f, Mathf.Max(.25f, chart.appearanceStartAlpha)),
                new Color(1f, .015f, .025f, 1), reveal), brightness);
            // Timing guides stay readable while the upcoming enemy body is still dimmed or revealing.
            outline.startColor = outline.endColor = Color.white;
            SetRadius(timingRing, (float)ApproachGeometry.Radius(time, target,
                OutlineRadius, chart.enemyLineWidth, chart.Timing));
            timingRing.startColor = timingRing.endColor = TimingColor(next);
        }
        private static Color Dim(Color color, float brightness)
            => RoomPalette.Tint(new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a));
    }
}
