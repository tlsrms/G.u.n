using System;
using UnityEngine;

namespace Gun.RoomRhythm
{
    // The scene owns this player's lifetime and advances it with unscaled time.
    internal sealed class StageMusicPreview
    {
        private const float LoadTimeout = 10;
        private const float FadeDuration = .15f;
        private readonly AudioSource source;
        private AudioClip pending;
        private float loadingElapsed, gain, volume = 1;
        private bool playing;

        internal event Action Ready;
        internal event Action Failed;

        internal StageMusicPreview(AudioSource source)
        {
            this.source = source;
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0;
        }

        internal void Play(AudioClip clip, float requestedVolume)
        {
            Stop();
            SetVolume(requestedVolume);
            if (clip == null)
            {
                Failed?.Invoke();
                return;
            }
            pending = clip;
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            Tick(0);
        }

        internal void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0) return;
            if (pending != null)
            {
                loadingElapsed += deltaTime;
                if (pending.loadState == AudioDataLoadState.Loading && loadingElapsed < LoadTimeout) return;
                if (pending.loadState != AudioDataLoadState.Loaded)
                {
                    pending = null;
                    Failed?.Invoke();
                    return;
                }

                source.clip = pending;
                pending = null;
                source.volume = 0;
                source.Play();
                playing = true;
                Ready?.Invoke();
            }
            if (!playing) return;
            gain = Mathf.MoveTowards(gain, 1, deltaTime / FadeDuration);
            source.volume = volume * gain;
        }

        internal void SetVolume(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            volume = Mathf.Clamp01(value);
            source.volume = volume * gain;
        }

        internal void Stop()
        {
            // A clip may finish loading after cancellation; it must no longer own playback.
            pending = null;
            loadingElapsed = gain = 0;
            playing = false;
            source.Stop();
            source.clip = null;
            source.volume = 0;
        }
    }
}
