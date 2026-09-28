using UnityEngine;

namespace Gun.RoomRhythm
{
    // Only animates objects authored in the scene. The screen and routes are ordinary UI children.
    public sealed class StageSignal : MonoBehaviour
    {
        [SerializeField] private CanvasGroup staticOverlay;
        [SerializeField] private RectTransform scanBand;
        [SerializeField, Min(1)] private float travel = 330;
        private float tunedAt;

        public void Tune() => tunedAt = Time.unscaledTime;

        private void Update()
        {
            if (staticOverlay != null)
                staticOverlay.alpha = Mathf.Lerp(.025f, .35f, Mathf.Clamp01(1 - (Time.unscaledTime - tunedAt) / .2f));
            if (scanBand != null)
            {
                Vector2 position = scanBand.anchoredPosition;
                position.y = Mathf.Repeat(Time.unscaledTime * 12, travel) - travel * .5f;
                scanBand.anchoredPosition = position;
            }
        }
    }
}
