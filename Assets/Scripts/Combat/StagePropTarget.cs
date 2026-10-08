using System;
using UnityEngine;

namespace Gun.RoomRhythm
{
    // Authored obstacle/projectile. The chart appearance-to-hit interval drives its path.
    public sealed class StagePropTarget : StageActionTarget
    {
        [SerializeField] private GameObject visuals;
        [SerializeField] private LineRenderer outline, timingRing;
        [SerializeField, Min(.01f)] private float radius = .38f;
        [SerializeField] private bool moving;
        [SerializeField] private Transform pathStart, pathEnd;
        [SerializeField] private MovementEase ease = MovementEase.Linear;
        private Vector3 initialPosition, startPosition, endPosition;
        private bool captured;
        private double appearedAt, hitAt;
        private RoomChart chart;
        private const int Segments = 64;

        public override void ValidateReferences()
        {
            if (visuals == null || visuals == gameObject || !visuals.transform.IsChildOf(transform)
                || outline == null || timingRing == null || outline == timingRing
                || !outline.transform.IsChildOf(visuals.transform) || !timingRing.transform.IsChildOf(visuals.transform)
                || !(radius > 0) || float.IsInfinity(radius) || !Enum.IsDefined(typeof(MovementEase), ease))
                throw new InvalidOperationException("Stage target needs authored child visuals and two rings: " + Id);
            if (moving && (pathStart == null || pathEnd == null || pathStart.IsChildOf(transform) || pathEnd.IsChildOf(transform)))
                throw new InvalidOperationException("Moving target needs two external path anchors: " + Id);
        }

        public override void ResetTarget(RoomChart settings, double appearsAt, double hitTime)
        {
            if (!captured) { initialPosition = transform.position; captured = true; }
            chart = settings; appearedAt = appearsAt; hitAt = hitTime;
            startPosition = moving ? pathStart.position : initialPosition;
            endPosition = moving ? pathEnd.position : initialPosition;
            transform.position = startPosition;
            foreach (var line in new[] { outline, timingRing })
            {
                line.useWorldSpace = false; line.loop = true; line.positionCount = Segments;
                line.transform.localScale = Vector3.one;
                line.startWidth = line.endWidth = settings.enemyLineWidth;
            }
            SetRing(outline, radius);
            visuals.SetActive(false);
        }

        public override void ValidateOutside(Transform rebuiltRoot)
        {
            base.ValidateOutside(rebuiltRoot);
            if (moving && (pathStart != null && pathStart.IsChildOf(rebuiltRoot)
                || pathEnd != null && pathEnd.IsChildOf(rebuiltRoot)))
                throw new InvalidOperationException("Place path anchors outside editor-rebuilt rooms: " + Id);
        }

        public override Vector3 PositionAt(double chartTime)
        {
            double progress = hitAt > appearedAt ? (chartTime - appearedAt) / (hitAt - appearedAt) : chartTime < hitAt ? 0 : 1;
            return Vector3.LerpUnclamped(startPosition, endPosition, (float)MovementProfile.Evaluate(progress, ease));
        }

        public override void Present(bool visible, bool showFrame, double time, int shotPriority)
        {
            transform.position = PositionAt(time);
            visuals.SetActive(visible);
            if (!visible) return;
            outline.enabled = true;
            SetRing(outline, radius);
            timingRing.enabled = showFrame;
            SetRing(timingRing, (float)ApproachGeometry.Radius(time, hitAt, radius, chart.enemyLineWidth, chart.Timing));
            ActionCueStyle.ApplyShotRings(outline, timingRing, shotPriority, chart.enemyLineWidth);
        }

        public override void OnHit(double time) => visuals.SetActive(false);

        private void SetRing(LineRenderer line, float size)
        {
            Vector3 center = line.transform.InverseTransformPoint(transform.position);
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * 2 * Mathf.PI / Segments;
                // Keep ring radius in world units, independent of the prop artwork scale.
                Vector3 offset = line.transform.InverseTransformVector(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * size);
                line.SetPosition(i, center + offset);
            }
        }
    }
}
