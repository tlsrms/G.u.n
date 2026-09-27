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
        private bool completed;
        private double flashAt = -10;
        public Vector3 Target => transform.position;
        public Color FragmentColor => leftPanel.color;
        public double CompletionTime(double target, double appearedAt) => System.Math.Max(appearedAt, target - chart.Timing.early);
        public void Configure(RoomChart settings, float sideLength)
        {
            chart = settings;
            completed = false; flashAt = -10;
            roomRadius = sideLength * 0.5f;
            RefreshFrameDirections();
            foreach (SpriteRenderer panel in new[] { leftPanel, rightPanel })
            {
                Vector3 size = panel.transform.localScale;
                size.x = chart.passageWidth * 0.5f;
                size.y = chart.judgmentLineWidth;
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

        public void Present(bool visible, bool future, float progress, double time, double target, double appearedAt, double frameStart = double.NaN, bool? showFrame = null)
        {
            leftPanel.enabled = rightPanel.enabled = visible;
            if (double.IsNaN(frameStart)) frameStart = appearedAt;
            bool frameVisible = showFrame ?? (visible && time >= frameStart);
            judgmentFrame.gameObject.SetActive(frameVisible);
            if (!visible && !frameVisible) return;
            // Hold open, then snap shut in the final portion before the shooting window.
            double end = CompletionTime(target, appearedAt);
            double duration = System.Math.Min(chart.doorCloseDuration, (end - appearedAt) * .35);
            float closeProgress = duration > 0 ? Mathf.Clamp01((float)((time - (end - duration)) / duration)) : 1f;
            if (Application.isPlaying && closeProgress >= 1 && !completed)
            { completed = true; flashAt = time; }
            float flash = Mathf.Clamp01((float)(1 - (time - flashAt) / .12));
            float eased = closeProgress * closeProgress * (3f - 2f * closeProgress);
            float offset = Mathf.Lerp(chart.passageWidth * .75f, chart.passageWidth * .25f, eased);
            leftPanel.transform.localPosition = new Vector3(-offset, 0, 0);
            rightPanel.transform.localPosition = new Vector3(offset, 0, 0);
            Color color = future ? new Color(0.6f, 0.6f, 0.6f) : new Color(1f, 0.65f, 0.25f);
            color.a = chart.AppearanceAlpha(progress);
            color = RoomPalette.Tint(Color.Lerp(color, Color.white, flash));
            leftPanel.color = rightPanel.color = color;
            judgmentFrame.localScale = Vector3.one;
            float radius = (float)ApproachGeometry.ExpandingRadius(time, target, roomRadius, chart.judgmentLineWidth, chart.Timing);
            if (frameDirections == null || frameDirections.Length != frameEdges.Length) RefreshFrameDirections();
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
                frameColor = Color.Lerp(frameColor, Color.white, flash);
                edge.color = frameColor;
            }
        }

        public void RefreshFrameDirections()
        {
            if (frameEdges == null) return;
            frameDirections = new Vector3[frameEdges.Length];
            for (int i = 0; i < frameEdges.Length; i++)
                frameDirections[i] = FrameDirection(frameEdges[i], i);
        }

        private static Vector3 FrameDirection(SpriteRenderer edge, int index)
        {
            if (edge != null)
            {
                Vector3 direction = edge.transform.localPosition;
                if (direction.sqrMagnitude > 0.0001f)
                    return Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                        ? new Vector3(Mathf.Sign(direction.x), 0, 0)
                        : new Vector3(0, Mathf.Sign(direction.y), 0);
            }
            switch (index % 4)
            {
                case 0: return Vector3.up;
                case 1: return Vector3.right;
                case 2: return Vector3.down;
                default: return Vector3.left;
            }
        }
    }
}
