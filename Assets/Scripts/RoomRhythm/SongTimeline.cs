using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace Gun.RoomRhythm
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class SongTimeline : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField, Min(0.05f)] private float leadIn = 0.15f;
        private double dspOrigin;
        private double inputOrigin;
        private double frozenTime;
        private bool playing;
        public double Time => playing ? AudioSettings.dspTime - dspOrigin : frozenTime;

        public void ValidateReferences()
        {
            if (source == null || !(leadIn > 0) || float.IsInfinity(leadIn))
                throw new System.InvalidOperationException("Timeline needs an audio source and a finite positive lead-in.");
        }

        public void Begin(AudioClip clip)
        {
            source.Stop();
            source.clip = clip;
            double dspNow = AudioSettings.dspTime;
            dspOrigin = dspNow + leadIn;
            // InputAction callback times and DSP times have distinct origins.
            inputOrigin = InputState.currentTime + (dspOrigin - dspNow);
            source.PlayScheduled(dspOrigin);
            playing = true;
        }

        public double FromInputTime(double timestamp) => timestamp - inputOrigin;

        public void Stop()
        {
            if (!playing) return;
            frozenTime = Time;
            playing = false;
            source.Stop();
        }

        public void ResetTimeline()
        {
            playing = false;
            frozenTime = 0;
            source.Stop();
        }
    }
}
