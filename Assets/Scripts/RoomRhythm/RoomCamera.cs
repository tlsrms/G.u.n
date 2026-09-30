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
        private void Awake()
        {
            view = GetComponent<Camera>();
            if (view != null) { baseSize = view.orthographicSize; view.backgroundColor = RoomPalette.Tint(view.backgroundColor, 0); }
            if (session == null) session = FindFirstObjectByType<RoomSession>();
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
            if (view != null) view.orthographicSize = size * (effects != null ? effects.ZoomMultiplier : 1);
            if (view != null)
            {
                // Match the safe-room rig's 60 UI units against its 1600x1000,
                // 50/50 width-height CanvasScaler, including other aspect ratios.
                float scale = 60f * (2f * view.orthographicSize / 1000f)
                    * Mathf.Sqrt(view.aspect / 1.6f);
                Vector3 parentScale = player.parent != null ? player.parent.lossyScale : Vector3.one;
                player.localScale = new Vector3(scale / parentScale.x, scale / parentScale.y, 1);
            }
            position.z = transform.position.z;
            transform.position = position;
        }
    }
}
