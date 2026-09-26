using UnityEngine;

namespace Gun.RoomRhythm
{
    public sealed class RoomBinding : MonoBehaviour
    {
        [SerializeField] private string roomId;
        [SerializeField, Min(0.1f)] private float sideLength = 6f;
        [SerializeField] private GameObject visuals;
        [SerializeField] private SpriteRenderer[] surfaces;
        [SerializeField] private Transform judgmentFrame;
        [SerializeField] private SpriteRenderer[] frameEdges;
        [SerializeField] private RoomDoor door;
        private Color[] baseColors;
        private RoomChart chart;
        public string Id => roomId;
        public Vector3 Center => transform.position;
        public RoomDoor Door => door;
        public float SideLength => sideLength;
        public void Configure(RoomChart settings)
        {
            chart = settings;
            float half = sideLength * 0.5f;
            float length = half - chart.passageWidth * 0.5f + chart.judgmentLineWidth * 0.5f;
            float center = (half + chart.passageWidth * 0.5f + chart.judgmentLineWidth * 0.5f) * 0.5f;
            for (int i = 1; i < surfaces.Length; i++)
            {
                Transform wall = surfaces[i].transform;
                Vector3 p = wall.localPosition;
                bool horizontal = wall.localScale.x > wall.localScale.y;
                wall.localPosition = horizontal ? new Vector3(Mathf.Sign(p.x) * center, Mathf.Sign(p.y) * half, 0)
                    : new Vector3(Mathf.Sign(p.x) * half, Mathf.Sign(p.y) * center, 0);
                wall.localScale = horizontal ? new Vector3(length, chart.judgmentLineWidth, 1)
                    : new Vector3(chart.judgmentLineWidth, length, 1);
            }
            if (door != null) door.Configure(chart, sideLength);
        }
        public void ValidateReferences(bool needsFrame)
        {
            if (!(sideLength > 0) || float.IsInfinity(sideLength))
                throw new System.InvalidOperationException("Invalid room size: " + roomId);
            if (visuals == null || surfaces == null || surfaces.Length == 0)
                throw new System.InvalidOperationException("Missing room visuals: " + roomId);
            foreach (SpriteRenderer surface in surfaces)
                if (surface == null) throw new System.InvalidOperationException("Missing room surface: " + roomId);
            if (needsFrame && (judgmentFrame == null || frameEdges == null || frameEdges.Length != 4))
                throw new System.InvalidOperationException("Missing room timing square: " + roomId);
            if (needsFrame) foreach (SpriteRenderer edge in frameEdges)
                if (edge == null) throw new System.InvalidOperationException("Missing timing edge: " + roomId);
            if (door != null) door.ValidateReferences();
        }

        private void Awake()
        {
            baseColors = new Color[surfaces.Length];
            for (int i = 0; i < surfaces.Length; i++) baseColors[i] = surfaces[i].color;
        }

        public void Present(bool visible, bool current, bool future, float progress, bool showFrame,
            double time, double target, float frameProgress = -1)
        {
            visuals.SetActive(visible);
            if (judgmentFrame != null) judgmentFrame.gameObject.SetActive(visible && showFrame);
            if (!visible) return;
            float alpha = current ? 1f : chart.AppearanceAlpha(progress);
            for (int i = 0; i < surfaces.Length; i++)
            {
                Color color = baseColors[i];
                if (future) color = new Color(color.grayscale * 0.65f, color.grayscale * 0.65f, color.grayscale * 0.65f, color.a);
                color.a *= alpha;
                surfaces[i].color = color;
            }
            if (judgmentFrame == null) return;
            judgmentFrame.localScale = Vector3.one;
            float radius = (float)ApproachGeometry.Radius(time, target, sideLength * 0.5f,
                chart.judgmentLineWidth, chart.Timing);
            foreach (SpriteRenderer edge in frameEdges)
            {
                Transform line = edge.transform;
                Vector3 p = line.localPosition;
                bool horizontal = line.localScale.x > line.localScale.y;
                line.localPosition = horizontal ? new Vector3(0, Mathf.Sign(p.y) * radius, 0)
                    : new Vector3(Mathf.Sign(p.x) * radius, 0, 0);
                line.localScale = horizontal ? new Vector3(radius * 2 + chart.judgmentLineWidth, chart.judgmentLineWidth, 1)
                    : new Vector3(chart.judgmentLineWidth, radius * 2 + chart.judgmentLineWidth, 1);
                float frameAlpha = frameProgress < 0 ? alpha : chart.AppearanceAlpha(frameProgress);
                edge.color = future ? new Color(0.6f, 0.6f, 0.6f, frameAlpha) : new Color(0.3f, 1f, 0.8f, frameAlpha);
            }
        }
    }
}
