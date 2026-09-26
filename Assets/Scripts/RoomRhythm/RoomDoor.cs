using UnityEngine;

namespace Gun.RoomRhythm
{
    public sealed class RoomDoor : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer leftPanel;
        [SerializeField] private SpriteRenderer rightPanel;
        [SerializeField] private Transform judgmentFrame;
        [SerializeField] private SpriteRenderer[] frameEdges;
        private RoomChart chart;
        private float roomRadius;
        private Vector3[] frameDirections;
        public Vector3 Target => transform.position;
        public void Configure(RoomChart settings, float sideLength)
        {
            chart = settings;
            roomRadius = sideLength * 0.5f;
            if (frameDirections == null)
            {
                frameDirections = new Vector3[frameEdges.Length];
                for (int i = 0; i < frameEdges.Length; i++)
                    frameDirections[i] = frameEdges[i].transform.localPosition.normalized;
            }
            foreach (SpriteRenderer panel in new[] { leftPanel, rightPanel })
            {
                Vector3 size = panel.transform.localScale;
                size.x = chart.passageWidth * 0.5f;
                panel.transform.localScale = size;
            }
            float offset = chart.passageWidth * 0.75f;
            leftPanel.transform.localPosition = new Vector3(-offset, 0, 0);
            rightPanel.transform.localPosition = new Vector3(offset, 0, 0);
        }
        public void ValidateReferences()
        {
            if (leftPanel == null || rightPanel == null || judgmentFrame == null || frameEdges == null || frameEdges.Length != 4)
                throw new System.InvalidOperationException("Door needs panels and an authored timing square.");
            foreach (SpriteRenderer edge in frameEdges)
                if (edge == null) throw new System.InvalidOperationException("Missing door timing edge.");
        }

        public void Present(bool visible, bool future, float progress, double time, double target, double appearedAt, double frameStart = double.NaN)
        {
            leftPanel.enabled = rightPanel.enabled = visible;
            if (double.IsNaN(frameStart)) frameStart = appearedAt;
            judgmentFrame.gameObject.SetActive(visible && time >= frameStart);
            if (!visible) return;
            // Close immediately after appearance, finishing before the shooting window opens.
            double duration = System.Math.Min(chart.doorCloseDuration,
                System.Math.Max(0.001, target - chart.Timing.early - appearedAt));
            float closeProgress = Mathf.Clamp01((float)((time - appearedAt) / duration));
            float eased = closeProgress * closeProgress * (3f - 2f * closeProgress);
            float offset = Mathf.Lerp(chart.passageWidth * .75f, chart.passageWidth * .25f, eased);
            leftPanel.transform.localPosition = new Vector3(-offset, 0, 0);
            rightPanel.transform.localPosition = new Vector3(offset, 0, 0);
            Color color = future ? new Color(0.6f, 0.6f, 0.6f) : new Color(1f, 0.65f, 0.25f);
            color.a = chart.AppearanceAlpha(progress);
            leftPanel.color = rightPanel.color = color;
            judgmentFrame.localScale = Vector3.one;
            float radius = (float)ApproachGeometry.ExpandingRadius(time, target, roomRadius, chart.judgmentLineWidth, chart.Timing);
            for (int i = 0; i < frameEdges.Length; i++)
            {
                SpriteRenderer edge = frameEdges[i];
                Transform line = edge.transform;
                bool horizontal = Mathf.Abs(frameDirections[i].y) > 0.5f;
                line.localPosition = frameDirections[i] * radius;
                line.localScale = horizontal ? new Vector3(radius * 2 + chart.judgmentLineWidth, chart.judgmentLineWidth, 1)
                    : new Vector3(chart.judgmentLineWidth, radius * 2 + chart.judgmentLineWidth, 1);
                Color frameColor = color;
                frameColor.a = chart.AppearanceAlpha(target > frameStart ? (float)((time - frameStart) / (target - frameStart)) : 1);
                edge.color = frameColor;
            }
        }
    }
}
