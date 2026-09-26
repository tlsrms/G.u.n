using System;
using UnityEngine;

namespace Gun.RoomRhythm
{
    // All geometry is authored in MainScene. This diagnostic UI is hidden in release builds.
    public sealed class DebugTimingBar : MonoBehaviour
    {
        [SerializeField] private bool showDuringDevelopment = true;
        [SerializeField] private GameObject visuals;
        [SerializeField] private Transform earlyZone;
        [SerializeField] private Transform accurateZone;
        [SerializeField] private Transform lateZone;
        [SerializeField] private Transform marker;
        [SerializeField] private float width = 8f;

        public void ValidateReferences()
        {
            if (visuals == null || earlyZone == null || accurateZone == null || lateZone == null || marker == null || !(width > 0))
                throw new InvalidOperationException("Missing debug timing bar geometry.");
        }

        public void Hide() => visuals.SetActive(false);

        public void Present(double time, double target, TimingWindow window)
        {
            bool show = showDuringDevelopment && (Application.isEditor || Debug.isDebugBuild);
            visuals.SetActive(show);
            if (!show) return;
            double halfSpan = Math.Max(0.5, Math.Max(window.early, window.late) * 2);
            float unitsPerSecond = width / (float)(halfSpan * 2);
            Zone(earlyZone, -window.early, -window.accurate, unitsPerSecond);
            Zone(accurateZone, -window.accurate, window.accurate, unitsPerSecond);
            Zone(lateZone, window.accurate, window.late, unitsPerSecond);
            marker.localPosition = new Vector3(Mathf.Clamp((float)(time - target) * unitsPerSecond, -width * .5f, width * .5f), 0, 0);
        }

        private static void Zone(Transform zone, double from, double to, float unitsPerSecond)
        {
            zone.localPosition = new Vector3((float)((from + to) * .5) * unitsPerSecond, 0, 0);
            zone.localScale = new Vector3((float)(to - from) * unitsPerSecond, .22f, 1);
        }
    }
}
