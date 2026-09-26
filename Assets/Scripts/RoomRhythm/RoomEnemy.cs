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

        public void Present(bool visible, float progress, double time = 0, double target = 0, bool showFrame = true, float frameProgress = -1)
        {
            visuals.SetActive(visible);
            if (!visible) return;
            judgmentFrame.gameObject.SetActive(showFrame);
            float alpha = chart.AppearanceAlpha(progress);
            body.color = new Color(0.3f, 0.12f, 0.2f, alpha);
            outline.startColor = outline.endColor = new Color(1f, 1f, 1f, alpha);
            SetRadius(timingRing, (float)ApproachGeometry.Radius(time, target,
                OutlineRadius, chart.enemyLineWidth, chart.Timing));
            timingRing.startColor = timingRing.endColor = new Color(1f, 0.3f, 0.6f, frameProgress < 0 ? alpha : chart.AppearanceAlpha(frameProgress));
        }
    }
}
