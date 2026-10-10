using UnityEngine;

namespace Gun.RoomRhythm
{
    // Operates only on authored legs. Facing and upper-body animation remain independent.
    [DefaultExecutionOrder(500)]
    public sealed class TopDownLegMotion : MonoBehaviour
    {
        [SerializeField] private Transform motionSource;
        [SerializeField] private Transform leftHip, rightHip, leftKnee, rightKnee;
        [SerializeField, Min(.001f)] private float unitsPerRigUnit = 1;
        [SerializeField, Min(0)] private float stride = .32f;
        [SerializeField, Min(.1f)] private float cyclesPerSecond = 1.8f;
        [SerializeField, Min(.1f)] private float teleportDistance = 4;
        private Vector3 lastPosition, leftRest, rightRest, leftScale, rightScale;
        private Quaternion leftRotation, rightRotation, leftKneeRotation, rightKneeRotation;
        private float phase, movingTime, strideHeading, strideDirection = 1;
        private bool ready, visible = true, moving;

        private void Awake()
        {
            if (leftHip == null || rightHip == null || leftKnee == null || rightKnee == null)
            {
                Debug.LogError("Leg motion requires authored hip and knee references.", this);
                enabled = false;
                return;
            }
            if (motionSource == null) motionSource = transform;
            leftRest = leftHip.localPosition; rightRest = rightHip.localPosition;
            leftScale = leftHip.localScale; rightScale = rightHip.localScale;
            leftRotation = leftHip.localRotation; rightRotation = rightHip.localRotation;
            leftKneeRotation = leftKnee.localRotation; rightKneeRotation = rightKnee.localRotation;
            ready = true;
        }

        private void OnEnable()
        {
            if (!ready) return;
            lastPosition = motionSource.position;
            Hide();
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (!value && ready) Hide();
        }

        private void LateUpdate()
        {
            if (!ready) return;
            Vector3 position = motionSource.position;
            Vector3 delta = position - lastPosition;
            lastPosition = position;
            Vector3 local = leftHip.parent.InverseTransformVector(delta) / unitsPerRigUnit;
            local.z = 0;
            float distance = local.magnitude;
            if (!visible || Time.unscaledDeltaTime <= 0 || distance < .0001f || distance > teleportDistance)
            {
                Hide();
                return;
            }
            bool starting = !moving;
            if (starting)
            {
                phase = 0;
                movingTime = 0;
            }
            moving = true;
            leftHip.gameObject.SetActive(true); rightHip.gameObject.SetActive(true);
            // Room transitions are fast; their speed must not accelerate the gait.
            movingTime += Time.unscaledDeltaTime;
            phase = (phase + Time.unscaledDeltaTime * cyclesPerSecond * 2 * Mathf.PI) % (2 * Mathf.PI);
            float step = Mathf.Sin(phase) * Mathf.SmoothStep(0, 1, movingTime / .08f);
            // Keep the pelvis attached to the facing torso. Backpedalling reverses
            // the stride rather than turning the entire lower body through 180 degrees.
            Vector3 travel = local.normalized;
            float backward = travel.y < 0 ? -1 : 1;
            Vector3 facingTravel = travel * backward;
            float angle = Mathf.Atan2(facingTravel.y, facingTravel.x) * Mathf.Rad2Deg - 90;
            float targetHeading = Mathf.Clamp(angle, -55, 55);
            // Turning the aim across the strafe/backpedal boundary must not flip
            // the feet in one frame while a room transition is still running.
            strideHeading = starting ? targetHeading : Mathf.LerpAngle(strideHeading, targetHeading,
                1 - Mathf.Exp(-Time.unscaledDeltaTime * 22));
            strideDirection = starting ? backward : Mathf.MoveTowards(strideDirection, backward, Time.unscaledDeltaTime * 18);
            Quaternion heading = Quaternion.Euler(0, 0, strideHeading);
            // Project a forward/backward step onto the floor. Passing through zero
            // tucks the leg under the torso; the hip never leaves its attachment.
            leftHip.localPosition = leftRest;
            rightHip.localPosition = rightRest;
            float reach = step * strideDirection * Mathf.Lerp(.7f, 1, Mathf.Abs(travel.y))
                * Mathf.Clamp(stride / .265f, 0, 1.6f);
            leftHip.localScale = Vector3.Scale(leftScale, new Vector3(1, -reach, 1));
            rightHip.localScale = Vector3.Scale(rightScale, new Vector3(1, reach, 1));
            leftHip.localRotation = heading * leftRotation;
            rightHip.localRotation = heading * rightRotation;
            leftKnee.localRotation = leftKneeRotation;
            rightKnee.localRotation = rightKneeRotation;
        }

        private void Hide()
        {
            moving = false;
            leftHip.gameObject.SetActive(false); rightHip.gameObject.SetActive(false);
            leftHip.localPosition = leftRest; rightHip.localPosition = rightRest;
            leftHip.localScale = leftScale; rightHip.localScale = rightScale;
            leftHip.localRotation = leftRotation; rightHip.localRotation = rightRotation;
            leftKnee.localRotation = leftKneeRotation; rightKnee.localRotation = rightKneeRotation;
        }

        private void OnDisable()
        {
            if (ready) Hide();
        }
    }
}
