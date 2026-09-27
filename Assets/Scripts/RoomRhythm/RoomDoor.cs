using UnityEngine;

namespace Gun.RoomRhythm
{
    public sealed class RoomDoor : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer leftPanel;
        [SerializeField] private SpriteRenderer rightPanel;
        // Retained to disable timing squares in existing scenes without a YAML migration.
        [SerializeField] private Transform judgmentFrame;
        [SerializeField] private SpriteRenderer[] frameEdges;
        private RoomChart chart;
        private LineRenderer outline, timingRing;
        public const float OutlineRadius = .38f;
        public Vector3 Target => transform.position;
        public Color FragmentColor => leftPanel.color;

        public void Configure(RoomChart settings, float sideLength)
        {
            chart = settings;
            if (judgmentFrame != null) judgmentFrame.gameObject.SetActive(false);
            if (frameEdges != null) foreach (var edge in frameEdges) if (edge != null) edge.enabled = false;
            outline = Ring("Door outline"); timingRing = Ring("Door timing ring");
            foreach (var panel in new[] { leftPanel, rightPanel })
                panel.transform.localScale = new Vector3(chart.passageWidth * .5f, chart.judgmentLineWidth, 1);
        }
        private LineRenderer Ring(string name)
        {
            var child = transform.Find(name);
            var line = child != null ? child.GetComponent<LineRenderer>() : new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.transform.localPosition = Vector3.zero;
            line.transform.localRotation = Quaternion.identity;
            line.transform.localScale = Vector3.one;
            line.sharedMaterial = leftPanel.sharedMaterial;
            line.sortingLayerID = leftPanel.sortingLayerID;
            line.sortingOrder = leftPanel.sortingOrder + 2;
            line.useWorldSpace = false; line.loop = true; line.positionCount = 128;
            line.startWidth = line.endWidth = chart.enemyLineWidth;
            line.enabled = false;
            return line;
        }
        public void ValidateReferences()
        {
            if (leftPanel == null || rightPanel == null)
                throw new System.InvalidOperationException("Door needs two panels.");
        }
        public void Present(bool visible, bool future, float progress, double time, double target, double appearedAt,
            double frameStart = double.NaN, bool? showFrame = null)
        {
            if (judgmentFrame != null) judgmentFrame.gameObject.SetActive(false);
            leftPanel.enabled = rightPanel.enabled = visible;
            outline.enabled = visible;
            timingRing.enabled = visible && (showFrame ?? true);
            if (!visible) return;
            float close = CloseProgress(chart, time, appearedAt, target);
            float offset = Mathf.Lerp(chart.passageWidth * .75f, chart.passageWidth * .25f, close);
            leftPanel.transform.localPosition = new Vector3(-offset, 0, 0);
            rightPanel.transform.localPosition = new Vector3(offset, 0, 0);
            float flash = Mathf.Clamp01(1 - (float)(time - appearedAt - CloseDuration(chart, appearedAt, target)) / .12f);
            Color color = future ? Color.gray : new Color(1, .65f, .25f);
            leftPanel.color = rightPanel.color = RoomPalette.Tint(Color.Lerp(color, Color.white, close >= 1 ? flash : 0));
            SetRadius(outline, OutlineRadius, Color.white);
            SetRadius(timingRing, (float)ApproachGeometry.Radius(time, target, OutlineRadius, chart.enemyLineWidth, chart.Timing), RoomPalette.Tint(color));
        }
        public static double CloseDuration(RoomChart chart, double appearedAt, double target)
            => System.Math.Max(.001, System.Math.Min(chart.doorCloseDuration, target - appearedAt));
        public static float CloseProgress(RoomChart chart, double time, double appearedAt, double target)
        {
            float t = Mathf.Clamp01((float)((time - appearedAt) / CloseDuration(chart, appearedAt, target)));
            return 1 - (1 - t) * (1 - t) * (1 - t);
        }
        private static void SetRadius(LineRenderer line, float radius, Color color)
        {
            line.startColor = line.endColor = color;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * 2 * Mathf.PI / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
            }
        }
    }
}
