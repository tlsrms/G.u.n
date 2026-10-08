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
        [SerializeField, Min(.075f)] private float bulletDuration = .075f;
        private float FlightDuration => Mathf.Max(.075f, bulletDuration);
        private SpriteRenderer trailCore, trailGlow;
        [SerializeField, Min(0f)] private float recoilAngle = 12f;
        [SerializeField] private Transform gun;
        [SerializeField] private GeometricPlayerRig characterRig;
        [SerializeField] private SpriteRenderer gunBody, gunBarrel;
        [SerializeField] private SpriteRenderer muzzle, bullet, bulletGlow;
        private SpriteRenderer playerBody;
        private Vector3 bulletStart, bulletEnd;
        private float firedAt = -10;
        private bool presentationVisible = true;
        private Color gunBodyColor, gunBarrelColor;

        public void ValidateReferences()
        {
            if (viewCamera == null || !viewCamera.orthographic || player == null || arc == null
                || shotTrace == null || arcSegments == null || arcSegments.Length == 0
                || gun == null || gunBody == null || gunBarrel == null || muzzle == null || bullet == null || bulletGlow == null
                || (characterRig == null && player.GetComponent<SpriteRenderer>() == null)
                || (characterRig != null && (characterRig.Grip == null || characterRig.Muzzle == null)))
                throw new System.InvalidOperationException("Aim needs an orthographic camera and authored geometry.");
            foreach (Transform segment in arcSegments)
                if (segment == null) throw new System.InvalidOperationException("Missing aim segment.");
        }

        public void Configure(float radius, float halfAngle, RoomFeedback feedback)
        {
            feedback.PrepareEffects();
            muzzle.sprite = bullet.sprite = bulletGlow.sprite = feedback.GlowSprite;
            shotTrace.localScale = Vector3.one;
            var legacyTrace = shotTrace.GetComponent<SpriteRenderer>();
            if (legacyTrace != null) legacyTrace.enabled = false;
            if (trailCore == null)
            {
                trailGlow = feedback.CreateVisual(shotTrace, "Shot trail glow", Color.white, true);
                trailCore = feedback.CreateVisual(shotTrace, "Shot trail core", Color.white);
                trailGlow.sortingOrder = 39; trailCore.sortingOrder = 40;
            }
            for (int i = 0; i < arcSegments.Length; i++)
            {
                float angle = Mathf.Lerp(-halfAngle, halfAngle, (i + 0.5f) / arcSegments.Length);
                float radians = angle * Mathf.Deg2Rad;
                Transform segment = arcSegments[i];
                var renderer = segment.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    Color tint = ActionCueStyle.Shot(0);
                    tint.a = .55f;
                    renderer.color = tint;
                }
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
            Vector3 gunOrigin = GripPosition(origin);
            bulletEnd = target ?? origin + (Vector3)(aim * 3f);
            Vector3 delta = bulletEnd - gunOrigin;
            direction = delta.sqrMagnitude > .0001f ? (Vector2)delta.normalized : aim;
            firedAt = Time.unscaledTime;
            traceUntil = firedAt + FlightDuration + .065f;
            shotTrace.gameObject.SetActive(true);
            PoseWeapon(origin);
            bulletStart = BarrelTip();
            RenderWeapon();
        }

        public void ResetAim()
        {
            SetPresentation(true);
            if (gunBody != null) gunBody.color = gunBodyColor;
            if (gunBarrel != null) gunBarrel.color = gunBarrelColor;
            shotTrace.gameObject.SetActive(false); traceUntil = 0; firedAt = -10;
            if (muzzle != null) muzzle.gameObject.SetActive(false);
            if (bullet != null) bullet.gameObject.SetActive(false);
            if (bulletGlow != null) bulletGlow.gameObject.SetActive(false);
        }

        private void RenderWeapon()
        {
            if (gun == null) return;
            float elapsed = Mathf.Max(0, Time.unscaledTime - firedAt);
            PoseWeapon(player.position);
            muzzle.gameObject.SetActive(elapsed < .04f);
            bool show = Time.unscaledTime < traceUntil;
            bullet.gameObject.SetActive(show); bulletGlow.gameObject.SetActive(show);
            shotTrace.gameObject.SetActive(show);
            if (!show) return;
            // Keep the line attached while the player moves or turns after firing.
            bulletStart = BarrelTip();
            float t = Mathf.Clamp01(elapsed / FlightDuration);
            float alpha = 1 - Mathf.Clamp01((elapsed - FlightDuration) / .065f);
            Vector3 delta = bulletEnd - bulletStart;
            Vector3 point = Vector3.Lerp(bulletStart, bulletEnd, t);
            Quaternion rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            bullet.transform.SetPositionAndRotation(point, rotation);
            bulletGlow.transform.SetPositionAndRotation(point, rotation);
            RoomFeedback.Size(bullet, new Vector2(.12f, .035f));
            RoomFeedback.Size(bulletGlow, new Vector2(.22f, .07f));
            if (t >= 1) { bullet.gameObject.SetActive(false); bulletGlow.gameObject.SetActive(false); }
            bullet.color = new Color(1, 1, .95f, alpha);
            bulletGlow.color = new Color(1, .8f, .3f, alpha);
            Vector3 midpoint = (bulletStart + bulletEnd) * .5f;
            trailCore.transform.SetPositionAndRotation(midpoint, rotation);
            trailGlow.transform.SetPositionAndRotation(midpoint, rotation);
            float length = Vector3.Distance(bulletStart, bulletEnd);
            RoomFeedback.Size(trailCore, new Vector2(length, .018f));
            RoomFeedback.Size(trailGlow, new Vector2(length, .05f));
            trailCore.color = new Color(1, 1, .95f, alpha * .9f);
            trailGlow.color = new Color(1, .75f, .25f, alpha * .8f);
        }

        private void LateUpdate()
        {
            if (!presentationVisible || gun == null || player == null) return;
            Vector2 arcDirection = Mouse.current != null ? DirectionAt(Mouse.current.position.ReadValue(), player.position) : direction;
            arc.position = player.position;
            arc.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(arcDirection.y, arcDirection.x) * Mathf.Rad2Deg);
            if (Mouse.current != null)
                direction = DirectionAt(Mouse.current.position.ReadValue(), characterRig != null ? player.position : GripPosition(player.position));
            if (Time.unscaledTime >= traceUntil) shotTrace.gameObject.SetActive(false);
            RenderWeapon();
        }

        public void SetPresentation(bool visible)
        {
            presentationVisible = visible;
            if (gun != null) gun.gameObject.SetActive(visible);
            if (arc != null) arc.gameObject.SetActive(visible);
            if (!visible)
            {
                shotTrace.gameObject.SetActive(false);
                if (bullet != null) bullet.gameObject.SetActive(false);
                if (bulletGlow != null) bulletGlow.gameObject.SetActive(false);
            }
        }

        private void Awake()
        {
            if (player != null) playerBody = player.GetComponent<SpriteRenderer>();
            if (gunBody != null) gunBodyColor = gunBody.color;
            if (gunBarrel != null) gunBarrelColor = gunBarrel.color;
        }

        public void FadeWithPlayer(float alpha)
        {
            gun.gameObject.SetActive(alpha > 0);
            Color body = gunBodyColor, barrel = gunBarrelColor;
            body.a *= alpha; barrel.a *= alpha;
            gunBody.color = body; gunBarrel.color = barrel;
            muzzle.gameObject.SetActive(false);
            if (alpha > 0) PoseWeapon(player.position);
        }

        private Vector3 GripPosition(Vector3 origin)
        {
            if (characterRig != null) return origin + characterRig.Grip.position - player.position;
            if (playerBody == null) playerBody = player.GetComponent<SpriteRenderer>();
            Bounds bounds = playerBody.bounds;
            return origin + new Vector3(bounds.max.x, bounds.center.y, bounds.center.z) - player.position;
        }

        private Vector3 RearPoint()
        {
            Bounds bounds = gunBody.sprite.bounds;
            return gunBody.transform.TransformPoint(new Vector3(bounds.min.x, bounds.center.y, bounds.center.z));
        }

        private Vector3 BarrelTip()
        {
            if (characterRig != null) return characterRig.Muzzle.position;
            Bounds bounds = gunBarrel.sprite.bounds;
            return gunBarrel.transform.TransformPoint(new Vector3(bounds.max.x, bounds.center.y, bounds.center.z));
        }

        private void PoseWeapon(Vector3 origin)
        {
            if (characterRig != null)
            {
                player.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90);
                muzzle.transform.position = BarrelTip();
                return;
            }
            float recoil = Mathf.Exp(-Mathf.Max(0, Time.unscaledTime - firedAt) * 28);
            gun.rotation = Quaternion.identity;
            Vector3 forward = BarrelTip() - RearPoint();
            float authoredAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
            gun.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - authoredAngle + recoilAngle * recoil);
            gun.position += GripPosition(origin) - RearPoint();
            muzzle.transform.position = BarrelTip() + (BarrelTip() - RearPoint()).normalized * .12f;
        }
    }
}
