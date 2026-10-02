using UnityEngine;

namespace Gun.RoomRhythm
{
    public enum StageTargetRole { Shot, Breakthrough }

    // Overrides the presentation and geometry of an existing chart target.
    // Timing, score and availability remain owned by RoomRun.
    public abstract class StageActionTarget : MonoBehaviour
    {
        [SerializeField] private string targetId;
        [SerializeField] private StageTargetRole role;
        public string Id => targetId;
        public StageTargetRole Role => role;
        public virtual void ValidateOutside(Transform rebuiltRoot)
        {
            if (transform.IsChildOf(rebuiltRoot))
                throw new System.InvalidOperationException("Place stage targets outside editor-rebuilt rooms: " + Id);
        }
        public abstract void ValidateReferences();
        public abstract void ResetTarget(RoomChart chart, double appearsAt, double hitTime);
        // Must be a side-effect-free evaluation: input can refer to an earlier frame.
        public abstract Vector3 PositionAt(double chartTime);
        public abstract void Present(bool visible, bool showFrame, double time, bool next);
        public virtual void OnHit(double time) { }
        public virtual void OnFailure(double time) { }
    }
}
