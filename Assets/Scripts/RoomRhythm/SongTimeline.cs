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
        private double inputOffset;
        public double Time => playing ? AudioSettings.dspTime - dspOrigin : frozenTime;

        public void ValidateReferences()
        {
            if (source == null || !(leadIn > 0) || float.IsInfinity(leadIn))
                throw new System.InvalidOperationException("Timeline needs an audio source and a finite positive lead-in.");
        }

        public void Begin(AudioClip clip, double musicDelaySeconds = 0, bool loopMusic = false, double inputOffsetMs = 0)
        {
            if (double.IsNaN(inputOffsetMs) || double.IsInfinity(inputOffsetMs) || System.Math.Abs(inputOffsetMs) > 1000)
                throw new System.ArgumentException("Input offset must be within +/-1000 ms.");
            if (double.IsNaN(musicDelaySeconds) || double.IsInfinity(musicDelaySeconds) || musicDelaySeconds < 0)
                throw new System.ArgumentException("음악 재생 전 대기 시간은 0 이상의 유한한 값이어야 합니다.");
            source.Stop();
            source.clip = clip;
            inputOffset = inputOffsetMs / 1000;
            source.loop = loopMusic;
            double dspNow = AudioSettings.dspTime;
            dspOrigin = dspNow + leadIn;
            // InputAction callback times and DSP times have distinct origins.
            inputOrigin = InputState.currentTime + (dspOrigin - dspNow);
            source.PlayScheduled(dspOrigin + musicDelaySeconds);
            playing = true;
        }

        public double FromInputTime(double timestamp) => timestamp - inputOrigin - inputOffset;

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
