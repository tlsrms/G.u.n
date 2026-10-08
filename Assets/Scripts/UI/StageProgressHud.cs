using System;
using UnityEngine;
using UnityEngine.UI;

namespace Gun.RoomRhythm
{
    // Screen-space presentation only; the session remains the owner of run state.
    [DisallowMultipleComponent]
    public sealed class StageProgressHud : MonoBehaviour
    {
        private RoomSession session;
        private GameObject root;
        private RectTransform safeArea, fill;
        private Image fillImage;
        private Text percent;
        private RoomChart cachedChart;
        private int cachedRevision = -1, displayedPercent = -1;
        private double duration;
        private static readonly Color ActiveColor = new Color(.3f, 1f, .8f);
        private static readonly Color FailedColor = new Color(1f, .25f, .32f);

        public void Configure(RoomSession owner, Font font)
        {
            session = owner;
            if (root != null) return;
            root = new GameObject("Stage progress HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            safeArea = Rect("Safe area", root.transform, Vector2.zero, Vector2.one);
            var panel = Rect("Progress", safeArea, new Vector2(.2f, 1), new Vector2(.8f, 1));
            panel.pivot = new Vector2(.5f, 1);
            panel.anchoredPosition = new Vector2(0, -24);
            panel.sizeDelta = new Vector2(0, 44);
            var backing = panel.gameObject.AddComponent<Image>();
            backing.color = new Color(.025f, .04f, .045f, .85f);
            backing.raycastTarget = false;

            var track = Rect("Track", panel, new Vector2(0, .5f), new Vector2(1, .5f));
            track.offsetMin = new Vector2(16, -3);
            track.offsetMax = new Vector2(-80, 3);
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(1, 1, 1, .16f);
            trackImage.raycastTarget = false;
            fill = Rect("Fill", track, Vector2.zero, Vector2.zero);
            fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = ActiveColor;
            fillImage.raycastTarget = false;

            var label = Rect("Percent", panel, new Vector2(1, 0), Vector2.one);
            label.offsetMin = new Vector2(-76, 0);
            label.offsetMax = new Vector2(-12, 0);
            percent = label.gameObject.AddComponent<Text>();
            percent.font = font;
            percent.fontSize = 20;
            percent.alignment = TextAnchor.MiddleRight;
            percent.color = Color.white;
            percent.raycastTarget = false;
            percent.text = "0%";
        }

        private void LateUpdate()
        {
            if (root == null) return;
            bool visible = session != null && session.isActiveAndEnabled && session.Chart != null;
            root.SetActive(visible);
            if (!visible) return;

            Rect screenSafeArea = Screen.safeArea;
            safeArea.anchorMin = new Vector2(screenSafeArea.xMin / Mathf.Max(1, Screen.width),
                screenSafeArea.yMin / Mathf.Max(1, Screen.height));
            safeArea.anchorMax = new Vector2(screenSafeArea.xMax / Mathf.Max(1, Screen.width),
                screenSafeArea.yMax / Mathf.Max(1, Screen.height));

            RoomChart chart = session.Chart;
            if (cachedChart != chart || cachedRevision != chart.Revision)
            {
                cachedChart = chart;
                cachedRevision = chart.Revision;
                duration = 0;
                if (chart.moves != null)
                    foreach (var move in chart.moves)
                        duration = Math.Max(duration, move.HitTime + chart.moveDuration);
                if (chart.enemies != null)
                    foreach (var enemy in chart.enemies)
                        duration = Math.Max(duration, enemy.time);
            }

            // PresentationTime freezes at death and resets with the run, including looping songs.
            float progress = session.IsChartCompleted ? 1 : session.Phase == RunPhase.Ready || duration <= 0
                ? 0 : Mathf.Clamp((float)(session.PresentationTime / duration), 0, .99f);
            fill.anchorMax = new Vector2(progress, 1);
            fillImage.color = session.Phase == RunPhase.Dead ? FailedColor : ActiveColor;
            int value = Mathf.FloorToInt(progress * 100);
            if (value != displayedPercent)
            {
                displayedPercent = value;
                percent.text = value + "%";
            }
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private void OnDisable() { if (root != null) root.SetActive(false); }
        private void OnDestroy() { if (root != null) Destroy(root); }
    }
}
