using UnityEngine;

namespace Gun.RoomRhythm
{
    [DefaultExecutionOrder(-100)]
    public sealed class RoomCamera : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private RoomSession session;
        private Camera view;
        private void Awake()
        {
            view = GetComponent<Camera>();
            if (session == null) session = FindFirstObjectByType<RoomSession>();
        }
        private void LateUpdate()
        {
            Vector3 position = player.position;
            MapChart map = session != null && session.Chart != null ? session.Chart.appliedMap : null;
            if (map != null && map.rooms != null && map.rooms.Length > 0)
            {
                MapCameraPose pose = map.CameraAt(map.settings.Beat(session.PresentationTime), position.x, position.y);
                position.x = pose.x; position.y = pose.y;
                if (view != null) view.orthographicSize = pose.size;
            }
            position.z = transform.position.z;
            transform.position = position;
        }
    }
}
