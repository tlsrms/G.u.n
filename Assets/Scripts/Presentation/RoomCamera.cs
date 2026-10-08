using System.Collections.Generic;
using UnityEngine;

namespace Gun.RoomRhythm
{
    [DefaultExecutionOrder(-100)]
    public sealed class RoomCamera : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private RoomSession session;
        private Camera view;
        private float baseSize;
        private Vector3 stageCenter;
        private float stageSize, stageWeight;
        private struct HudPose { public Transform node; public Vector3 position, scale; }
        private readonly List<HudPose> hud = new List<HudPose>();
        public void SetStageFraming(Vector3 center, float size, float weight)
        { stageCenter = center; stageSize = Mathf.Max(.1f, size); stageWeight = Mathf.Clamp01(weight); }
        public void ReleaseStageFraming() => stageWeight = 0;
        private void Awake()
        {
            view = GetComponent<Camera>();
            if (view != null) { baseSize = view.orthographicSize; view.backgroundColor = RoomPalette.Tint(view.backgroundColor, 0); }
            if (session == null) session = FindFirstObjectByType<RoomSession>();
            // These are authored world-space HUDs under the camera, not gameplay geometry.
            // Capture once so zoom compensation never accumulates across frames/retries.
            foreach (var bar in GetComponentsInChildren<DebugTimingBar>(true)) CaptureHud(bar.transform, true);
            foreach (var text in GetComponentsInChildren<TextMesh>(true))
                if (text.GetComponentInParent<DebugTimingBar>() == null) CaptureHud(text.transform);
        }
        private void CaptureHud(Transform node, bool timingBar = false)
        {
            // Only camera-relative roots: child labels inherit their root's correction.
            if (node.parent != transform) return;
            Vector3 position = node.localPosition;
            // Older scene bars sit below the default view (-7.1 at a size of 7).
            // Leave space for their labels as well as the bar itself.
            if (timingBar) position.y = Mathf.Clamp(position.y, -baseSize * .85f, baseSize * .85f);
            hud.Add(new HudPose { node = node, position = position, scale = node.localScale });
        }
        private void FitHud()
        {
            if (view == null || baseSize <= 0) return;
            float ratio = view.orthographicSize / baseSize;
            foreach (var pose in hud)
            {
                if (pose.node == null) continue;
                pose.node.localPosition = new Vector3(pose.position.x * ratio, pose.position.y * ratio, pose.position.z);
                pose.node.localScale = pose.scale * ratio;
            }
        }
        private void LateUpdate()
        {
            Vector3 position = player.position;
            RoomCinematics effects = session != null ? session.Cinematics : null;
            if (effects != null && effects.CameraLocked)
            {
                position = effects.CameraCenter;
                position.z = transform.position.z;
                transform.position = position;
                FitHud();
                return;
            }
            float size = baseSize;
            MapChart map = session != null && session.Chart != null ? session.Chart.appliedMap : null;
            if (map != null && map.rooms != null && map.rooms.Length > 0)
            {
                MapCameraPose pose = map.CameraAt(map.settings.Beat(session.PresentationTime), position.x, position.y);
                position.x = pose.x; position.y = pose.y;
                size = pose.size;
            }
            float gameplaySize = size * (effects != null ? effects.ZoomMultiplier : 1);
            if (view != null) view.orthographicSize = Mathf.Lerp(gameplaySize, stageSize, stageWeight);
            position = Vector3.Lerp(position, stageCenter, stageWeight);
            if (view != null)
            {
                // Match the safe-room rig's 60 UI units against its 1600x1000,
                // 50/50 width-height CanvasScaler, including other aspect ratios.
                float scale = 60f * (2f * gameplaySize / 1000f)
                    * Mathf.Sqrt(view.aspect / 1.6f);
                Vector3 parentScale = player.parent != null ? player.parent.lossyScale : Vector3.one;
                player.localScale = new Vector3(scale / parentScale.x, scale / parentScale.y, 1);
            }
            position.z = transform.position.z;
            transform.position = position;
            FitHud();
        }
    }
}
