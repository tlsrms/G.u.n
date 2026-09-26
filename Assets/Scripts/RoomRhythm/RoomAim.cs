using UnityEngine;
using UnityEngine.InputSystem;

namespace Gun.RoomRhythm
{
    // Segments are authored in the scene. Only their geometry is adjusted to the shared settings.
    public sealed class RoomAim : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform player;
        [SerializeField] private Transform arc;
        [SerializeField] private Transform[] arcSegments;
        [SerializeField] private Transform shotTrace;
        private float traceUntil;
        private Vector2 direction = Vector2.up;

        public void ValidateReferences()
        {
            if (viewCamera == null || !viewCamera.orthographic || player == null || arc == null
                || shotTrace == null || arcSegments == null || arcSegments.Length == 0)
                throw new System.InvalidOperationException("Aim needs an orthographic camera and authored geometry.");
            foreach (Transform segment in arcSegments)
                if (segment == null) throw new System.InvalidOperationException("Missing aim segment.");
        }

        public void Configure(float radius, float halfAngle)
        {
            for (int i = 0; i < arcSegments.Length; i++)
            {
                float angle = Mathf.Lerp(-halfAngle, halfAngle, (i + 0.5f) / arcSegments.Length);
                float radians = angle * Mathf.Deg2Rad;
                Transform segment = arcSegments[i];
                segment.localPosition = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
                segment.localRotation = Quaternion.Euler(0, 0, angle + 90f);
                segment.localScale = new Vector3(2f * radius * Mathf.Sin(halfAngle * Mathf.Deg2Rad / arcSegments.Length) * 1.03f, 0.035f, 1);
            }
            ResetAim();
        }

        public Vector2 DirectionAt(Vector2 screen, Vector3 origin)
        {
            Vector3 point = new Vector3(screen.x, screen.y, origin.z - viewCamera.transform.position.z);
            Vector2 delta = viewCamera.ScreenToWorldPoint(point) - origin;
            return delta.sqrMagnitude > 0.0001f ? delta.normalized : direction;
        }

        public void Fire(Vector3 origin, Vector2 aim, Vector3? target)
        {
            Vector3 end = target ?? origin + (Vector3)(aim * 3f);
            Vector3 delta = end - origin;
            shotTrace.position = (origin + end) * 0.5f;
            shotTrace.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            shotTrace.localScale = new Vector3(delta.magnitude, 0.025f, 1);
            traceUntil = Time.unscaledTime + 0.055f;
            shotTrace.gameObject.SetActive(true);
        }

        public void ResetAim() { shotTrace.gameObject.SetActive(false); traceUntil = 0; }

        private void LateUpdate()
        {
            if (Mouse.current != null) direction = DirectionAt(Mouse.current.position.ReadValue(), player.position);
            arc.position = player.position;
            arc.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            if (Time.unscaledTime >= traceUntil) shotTrace.gameObject.SetActive(false);
        }
    }
}
