using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

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
        private AudioClip waveformClip;
        internal sealed class WaveformData
        {
            public float[] peaks, rms;
            public float reference;
            private readonly double[] energy;
            private readonly int[] sampleCounts;
            public WaveformData(int bins)
            {
                peaks = new float[bins]; rms = new float[bins];
                energy = new double[bins]; sampleCounts = new int[bins];
            }
            public void AddSamples(float[] samples, int firstFrame, int channels, int totalFrames)
            {
                for (int frame = 0; frame < samples.Length / channels; frame++)
                {
                    int bin = (int)((long)(firstFrame + frame) * peaks.Length / totalFrames);
                    for (int channel = 0; channel < channels; channel++)
                    {
                        float value = samples[frame * channels + channel];
                        energy[bin] += (double)value * value; sampleCounts[bin]++;
                        peaks[bin] = Math.Max(peaks[bin], Math.Abs(value));
                    }
                }
            }
            public void Complete()
            {
                for (int i = 0; i < energy.Length; i++)
                {
                    rms[i] = sampleCounts[i] > 0 ? (float)Math.Sqrt(energy[i] / sampleCounts[i]) : 0;
                    reference = Math.Max(reference, rms[i]);
                }
            }
        }
        private WaveformData waveform;
        private UnityWebRequest waveformRequest;
        private AudioClip decodedClip;
        private bool ownsDecodedClip;
        private float[] sampleBuffer;
        private int readFrame;
        public bool WaveformLoading => waveformRequest != null || decodedClip != null;
        public string WaveformError { get; private set; }
        private Hash128 waveformHash;
        private double waveformCheckedAt = double.NegativeInfinity;
        public void ClearWaveform()
        {
            waveformRequest?.Dispose(); waveformRequest = null;
            ReleaseDecodedClip();
            waveformClip = null; waveform = null; WaveformError = null;
        }
        private void ReleaseDecodedClip()
        {
            if (ownsDecodedClip && decodedClip != null) UnityEngine.Object.DestroyImmediate(decodedClip);
            decodedClip = null; ownsDecodedClip = false; sampleBuffer = null;
        }
        // Analyze PCM in bounded chunks; never change the imported clip's playback settings.
        public WaveformData Waveform(AudioClip source)
        {
            if (source == null) { ClearWaveform(); return null; }
            try
            {
                if (source != waveformClip || EditorApplication.timeSinceStartup - waveformCheckedAt >= 1)
                {
                    waveformCheckedAt = EditorApplication.timeSinceStartup;
                    string path = AssetDatabase.GetAssetPath(source);
                    var hash = AssetDatabase.GetAssetDependencyHash(path);
                    if (source != waveformClip || hash != waveformHash)
                    {
                        ClearWaveform(); waveformClip = source; waveformHash = hash;
                        if (source.loadType == AudioClipLoadType.DecompressOnLoad)
                        { decodedClip = source; source.LoadAudioData(); }
                        else
                        {
                            string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
                            AudioType type = extension == ".wav" ? AudioType.WAV : extension == ".ogg" ? AudioType.OGGVORBIS
                                : extension == ".mp3" ? AudioType.MPEG : extension == ".aif" || extension == ".aiff" ? AudioType.AIFF : AudioType.UNKNOWN;
                            if (type == AudioType.UNKNOWN) throw new InvalidOperationException("이 음원 형식은 파형 분석을 지원하지 않습니다.");
                            string uri = new Uri(System.IO.Path.GetFullPath(path)).AbsoluteUri;
                            waveformRequest = UnityWebRequestMultimedia.GetAudioClip(uri, type);
                            var handler = (DownloadHandlerAudioClip)waveformRequest.downloadHandler;
                            handler.compressed = false; handler.streamAudio = false;
                            waveformRequest.SendWebRequest();
                        }
                    }
                }
                if (waveformRequest != null)
                {
                    if (!waveformRequest.isDone) return null;
                    if (waveformRequest.result != UnityWebRequest.Result.Success)
                        throw new InvalidOperationException("음원 파일을 파형 분석용으로 읽지 못했습니다.");
                    decodedClip = DownloadHandlerAudioClip.GetContent(waveformRequest); ownsDecodedClip = true;
                    waveformRequest.Dispose(); waveformRequest = null;
                    if (decodedClip == null) throw new InvalidOperationException("음원 디코딩에 실패했습니다.");
                }
                if (decodedClip != null)
                {
                    if (decodedClip.loadState == AudioDataLoadState.Failed) throw new InvalidOperationException("음원 데이터를 읽지 못했습니다.");
                    if (decodedClip.loadState != AudioDataLoadState.Loaded) return null;
                    if (waveform == null)
                    {
                        int bins = Math.Max(1, (int)Math.Ceiling((double)decodedClip.samples / decodedClip.frequency * 200));
                        waveform = new WaveformData(bins); readFrame = 0;
                    }
                    int frames = Math.Min(65536, decodedClip.samples - readFrame), channels = decodedClip.channels;
                    if (sampleBuffer == null || sampleBuffer.Length != frames * channels) sampleBuffer = new float[frames * channels];
                    if (!decodedClip.GetData(sampleBuffer, readFrame)) throw new InvalidOperationException("파형 분석용 샘플을 읽지 못했습니다.");
                    waveform.AddSamples(sampleBuffer, readFrame, channels, decodedClip.samples);
                    readFrame += frames;
                    if (readFrame < decodedClip.samples) return null;
                    waveform.Complete();
                    ReleaseDecodedClip();
                }
            }
            catch (Exception exception)
            {
                ClearWaveform(); waveformClip = source; WaveformError = exception.Message;
            }
            return waveform;
        }
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
