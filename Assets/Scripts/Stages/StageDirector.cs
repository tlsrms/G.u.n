using UnityEngine;

namespace Gun.RoomRhythm
{
    // Author a stage-specific subclass on a scene object and assign it to RoomSession.
    // Callbacks are dispatched after the input batch, never from inside a judgment.
    public abstract class StageDirector : MonoBehaviour
    {
        protected RoomSession Session { get; private set; }
        internal void Bind(RoomSession session) => Session = session;
        public virtual void ResetStage() { }
        public virtual void BeginStage() { }
        public virtual void OnAction(RoomActionResult result) { }
        public virtual void OnChartCompleted() { }
        public virtual void Tick(double songTime) { }
        protected bool CompleteStage() => Session != null && Session.CompleteStage();
        internal void RestartStage()
        {
            StopAllCoroutines();
            ResetStage();
        }
    }
}
