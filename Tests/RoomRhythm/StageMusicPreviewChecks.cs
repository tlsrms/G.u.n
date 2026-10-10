using System;
using Gun.RoomRhythm;
using UnityEngine;

// Engine boundaries only: loading completes under test control, with the real preview player.
namespace UnityEngine
{
    public enum AudioDataLoadState { Unloaded, Loading, Loaded, Failed }
    public sealed class AudioClip
    {
        public AudioDataLoadState loadState;
        public AudioDataLoadState loadResult = AudioDataLoadState.Loading;
        public int loadRequests;
        public bool LoadAudioData()
        {
            loadRequests++;
            loadState = loadResult;
            return loadState != AudioDataLoadState.Failed;
        }
    }
    public sealed class AudioSource
    {
        public bool playOnAwake, loop;
        public float spatialBlend, volume;
        public AudioClip clip;
        public bool isPlaying;
        public int playCount;
        public void Play() { isPlaying = true; playCount++; }
        public void Stop() { isPlaying = false; }
    }
    public static class Mathf
    {
        public static float Clamp01(float value) => Math.Max(0, Math.Min(1, value));
        public static float MoveTowards(float current, float target, float delta)
            => Math.Abs(target - current) <= delta ? target : current + Math.Sign(target - current) * delta;
    }
}

internal static class StageMusicPreviewChecks
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    private static bool Near(float actual, float expected) => Math.Abs(actual - expected) < .0001f;
    private static AudioClip Loaded() => new AudioClip { loadState = AudioDataLoadState.Loaded };

    public static void Main()
    {
        FadeAndVolume();
        CancelAndReplace();
        LoadFailureAndRetry();
        TonearmPlayback();
        Console.WriteLine($"PASS: {checks} preview loading, cancellation, fade, volume and tonearm playback checks.");
    }

    private static void FadeAndVolume()
    {
        var source = new AudioSource { playOnAwake = true, spatialBlend = 1 };
        var preview = new StageMusicPreview(source);
        int ready = 0;
        preview.Ready += () => ready++;
        Check(!source.playOnAwake && source.loop && source.spatialBlend == 0, "preview is a looping 2D source");
        AudioClip clip = Loaded();
        preview.Play(clip, .6f);
        Check(source.isPlaying && source.clip == clip && ready == 1, "loaded song is ready immediately");
        Check(source.volume == 0 && clip.loadRequests == 0, "start silently without reloading an available clip");
        preview.Tick(.075f);
        Check(Near(source.volume, .3f), "half the fade applies half of the stage volume");
        preview.SetVolume(.8f);
        Check(Near(source.volume, .4f), "moving the slider during the fade preserves the envelope");
        preview.Tick(.075f);
        Check(Near(source.volume, .8f), "fade reaches the selected stage volume in 150ms");
        preview.Tick(20);
        Check(source.playCount == 1 && ready == 1, "updates after playback never restart the clip or notify twice");
        preview.SetVolume(0);
        Check(source.volume == 0 && source.isPlaying, "mute preserves playback position");
        preview.SetVolume(2);
        Check(source.volume == 1, "volume is clamped");
        preview.SetVolume(float.NaN);
        preview.Tick(float.PositiveInfinity);
        Check(source.volume == 1, "invalid input cannot corrupt the audio gain");
        preview.Stop();
        preview.Tick(1);
        Check(!source.isPlaying && source.clip == null && source.volume == 0, "stopping releases playback and its fade");
    }

    private static void CancelAndReplace()
    {
        var source = new AudioSource();
        var preview = new StageMusicPreview(source);
        int ready = 0, failed = 0;
        preview.Ready += () => ready++;
        preview.Failed += () => failed++;
        var cancelled = new AudioClip();
        preview.Play(cancelled, 1);
        preview.Tick(2);
        Check(cancelled.loadRequests == 1 && source.playCount == 0, "unloaded songs wait for one load request");
        preview.Stop();
        cancelled.loadState = AudioDataLoadState.Loaded;
        preview.Tick(20);
        Check(source.playCount == 0 && ready == 0 && failed == 0, "late completion after cancellation stays silent");

        var previous = new AudioClip();
        var current = new AudioClip();
        preview.Play(previous, .8f);
        preview.Tick(8);
        preview.Play(current, .25f);
        previous.loadState = AudioDataLoadState.Loaded;
        preview.Tick(3);
        Check(source.playCount == 0 && failed == 0, "replacement owns loading and receives a fresh timeout");
        current.loadState = AudioDataLoadState.Loaded;
        preview.Tick(.15f);
        Check(source.clip == current && ready == 1 && Near(source.volume, .25f), "only the latest selected song plays at its own volume");
        var next = new AudioClip();
        preview.Play(next, 1);
        Check(!source.isPlaying && source.clip == null, "a new selection stops the previous song while loading");
    }

    private static void LoadFailureAndRetry()
    {
        var source = new AudioSource();
        var preview = new StageMusicPreview(source);
        int ready = 0, failed = 0;
        preview.Ready += () => ready++;
        preview.Failed += () => failed++;
        var slow = new AudioClip();
        preview.Play(slow, 1);
        preview.Tick(9);
        Check(failed == 0, "loading remains pending before the timeout");
        preview.Tick(1);
        Check(failed == 1 && !source.isPlaying, "ten seconds of loading reports one failure");
        slow.loadState = AudioDataLoadState.Loaded;
        preview.Tick(1);
        Check(ready == 0 && failed == 1 && source.playCount == 0, "timed-out songs cannot start later");
        preview.Play(new AudioClip { loadResult = AudioDataLoadState.Failed }, 1);
        Check(failed == 2, "a rejected load reports failure immediately");
        preview.Play(null, 1);
        Check(failed == 3, "a missing song stays silent");
        preview.Play(Loaded(), .5f);
        preview.Tick(.15f);
        Check(ready == 1 && source.isPlaying && Near(source.volume, .5f), "another song can play after a failed load");

        preview.Ready += preview.Stop;
        preview.Play(Loaded(), 1);
        preview.Tick(1);
        Check(!source.isPlaying && source.volume == 0, "a ready callback can cancel playback without a lingering fade");
    }

    private static void TonearmPlayback()
    {
        var source = new AudioSource();
        var preview = new StageMusicPreview(source);
        var arm = new RecordTurntableState();
        preview.Ready += arm.AudioReady;
        preview.Failed += arm.Stop;
        var clip = new AudioClip();
        arm.ToggleArm();
        Check(arm.Tick(.3), "the arm reaches the record before starting a load");
        preview.Play(clip, 1);
        arm.Tick(1);
        Check(!arm.IsPlaying && arm.Rotation == 0 && !source.isPlaying, "record and audio wait together while loading");
        clip.loadState = AudioDataLoadState.Loaded;
        preview.Tick(.15f);
        arm.Tick(.5);
        Check(arm.IsPlaying && arm.Rotation != 0 && source.isPlaying, "record spin and music begin from the same ready event");
        arm.ToggleArm();
        preview.Stop();
        arm.Tick(.3);
        Check(arm.CanRotate && !source.isPlaying, "raising the arm returns to selection and stops the song");

        arm.ToggleArm();
        arm.Tick(.3);
        preview.Play(new AudioClip(), 1);
        preview.Tick(10);
        Check(arm.Phase == RecordPlaybackPhase.RaisingArm, "a failed load returns the tonearm instead of leaving it on the record");
        arm.Tick(.3);
        Check(arm.CanRotate, "selection recovers after a preview failure");
    }
}
