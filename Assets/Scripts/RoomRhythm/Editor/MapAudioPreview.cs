using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Gun.RoomRhythm.Editor
{
    // Unity's clip-inspector preview channel works in Edit Mode without scene AudioSources.
    // AudioUtil is internal, so isolate version-dependent access behind a small checked adapter.
    internal sealed class MapAudioPreview
    {
        private static readonly Type Utility = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        private static T Bind<T>(string name, params Type[] parameters) where T : Delegate
        {
            MethodInfo method = Utility?.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, parameters, null);
            return method == null ? null : (T)Delegate.CreateDelegate(typeof(T), method);
        }
        private static readonly Action<AudioClip, int, bool> Play = Bind<Action<AudioClip, int, bool>>("PlayPreviewClip", typeof(AudioClip), typeof(int), typeof(bool));
        private static readonly Action StopAll = Bind<Action>("StopAllPreviewClips");
        private static readonly Func<bool> IsPlaying = Bind<Func<bool>>("IsPreviewClipPlaying");
        private static readonly Func<int> SamplePosition = Bind<Func<int>>("GetPreviewClipSamplePosition");
        private AudioClip clip;
        private double startedAt;
        private bool ownsPlayback;
        private bool waiting;
        private double waitStartSeconds;
        private bool looping;
        private double playbackStartSeconds;
        public bool Running => waiting || ownsPlayback && IsPlaying != null && IsPlaying();
        public bool Starting => ownsPlayback && EditorApplication.timeSinceStartup - startedAt < .5;
        public double Seconds
        {
            get
            {
                if (waiting) return waitStartSeconds + EditorApplication.timeSinceStartup - startedAt;
                if (clip == null) return 0;
                double sampleSeconds = (double)SamplePosition() / clip.frequency;
                if (!looping) return sampleSeconds;
                // The preview sample position wraps; the chart clock must keep progressing.
                double duration = (double)clip.samples / clip.frequency;
                double expected = playbackStartSeconds + EditorApplication.timeSinceStartup - startedAt;
                return sampleSeconds + Math.Max(0, Math.Round((expected - sampleSeconds) / duration)) * duration;
            }
        }

        public void Tick()
        {
            if (!waiting) return;
            double seconds = Seconds;
            if (seconds >= 0) Start(clip, seconds, looping);
        }

        public void Start(AudioClip source, double seconds, bool loop = false)
        {
            if (Play == null || StopAll == null || IsPlaying == null || SamplePosition == null)
                throw new InvalidOperationException("현재 Unity에서 편집기 오디오 미리보기 API를 사용할 수 없습니다.");
            if (source == null || source.frequency <= 0 || source.samples <= 0)
                throw new ArgumentException("곡 설정에서 재생할 음악 소스를 지정하세요.");
            if (double.IsNaN(seconds) || double.IsInfinity(seconds))
                throw new ArgumentException("BPM과 음악 시작 오프셋을 확인하세요.");
            Stop();
            looping = loop;
            if (seconds < 0)
            {
                clip = source; waiting = true; waitStartSeconds = seconds;
                startedAt = EditorApplication.timeSinceStartup;
                return;
            }
            StopAll(); // The editor exposes a shared preview clock; ensure it belongs to this clip.
            double sampleSeconds = loop ? seconds % ((double)source.samples / source.frequency) : seconds;
            int sample = (int)Math.Max(0, Math.Min(source.samples - 1.0, Math.Round(sampleSeconds * source.frequency)));
            Play(source, sample, loop);
            playbackStartSeconds = seconds;
            clip = source; ownsPlayback = true; startedAt = EditorApplication.timeSinceStartup;
        }
        public void Stop()
        {
            if (ownsPlayback) StopAll?.Invoke();
            ownsPlayback = false; waiting = false; clip = null;
        }
    }
}
