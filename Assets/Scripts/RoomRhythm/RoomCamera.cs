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
        [Header("Analog Video")]
        [SerializeField, Range(0, 1)] private float videoStrength = .7f;
        private Material videoMaterial;
        private void Awake()
        {
            view = GetComponent<Camera>();
            if (view != null) { baseSize = view.orthographicSize; view.backgroundColor = RoomPalette.Tint(view.backgroundColor, 0); }
            if (session == null) session = FindFirstObjectByType<RoomSession>();
            Shader shader = Resources.Load<Shader>("RetroVideo");
            if (shader != null && shader.isSupported)
                videoMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (videoMaterial == null || videoStrength <= 0) { Graphics.Blit(source, destination); return; }
            videoMaterial.SetFloat("_Strength", videoStrength);
            videoMaterial.SetFloat("_VideoTime", Time.unscaledTime);
            Graphics.Blit(source, destination, videoMaterial);
        }
        private void OnDestroy() { if (videoMaterial != null) Destroy(videoMaterial); }
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
            position.z = transform.position.z;
            transform.position = position;
        }
    }
}
