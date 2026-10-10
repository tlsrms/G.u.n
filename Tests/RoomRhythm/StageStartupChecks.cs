using System;
using System.Collections.Generic;
using System.Reflection;
using Gun.RoomRhythm;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

// Exercises the real progression, input callbacks and song timeline without launching Unity.
// Test doubles supply engine clocks, audio and the session boundary (see the companion file).
internal static class StageStartupChecks
{
    private static int checks;
    private static readonly List<TimedCommand> commands = new List<TimedCommand>();
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        checks++;
    }
    private static void Near(double actual, double expected, string message)
        => Check(Math.Abs(actual - expected) < .00001, message + $" ({actual} vs {expected})");
    internal static void Set(object target, string name, object value)
        => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Call(object target, string name)
        => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    private sealed class Rig : IDisposable
    {
        public readonly RoomKeyboard Keyboard = new RoomKeyboard();
        public readonly SongTimeline Timeline = new SongTimeline();
        public readonly AudioSource Audio = new AudioSource();
        public readonly RoomSession Session;
        public readonly StageProgression Progression = new StageProgression();
        public readonly InputActionMap Map;
        public Rig()
        {
            Call(Keyboard, "Awake");
            Map = InputActionMap.LastCreated;
            Call(Keyboard, "OnEnable");
            Set(Timeline, "source", Audio);
            Session = new RoomSession(Keyboard, Timeline);
            Set(Progression, "session", Session);
            Set(Progression, "fade", new CanvasGroup());
            Call(Progression, "Start");
        }
        public void Tick() => Call(Progression, "Update");
        public void Dispose()
        {
            Call(Keyboard, "OnDisable");
            Call(Keyboard, "OnDestroy");
        }
    }

    public static void Main()
    {
        using (var rig = new Rig())
        {
            StageSelection.IsRecordRun = true; StageSelection.MusicVolume = .37f;
            rig.Timeline.Begin(new AudioClip());
            Near(rig.Audio.volume, .37, "selected stage volume reaches gameplay audio");
            StageSelection.MusicVolume = 0;
            rig.Timeline.Begin(new AudioClip());
            Near(rig.Audio.volume, 0, "muted preview stays muted in gameplay");
            StageSelection.IsRecordRun = false; rig.Audio.volume = .6f;
            rig.Timeline.Begin(new AudioClip());
            Near(rig.Audio.volume, .6, "non-record runs retain authored audio volume");
            StageSelection.MusicVolume = 1;
        }
        // Editor time and play time have different origins, just like the reported -13,218 s input.
        InputState.currentTime = 13218;
        AudioSettings.dspTime = 200;
        using (var rig = new Rig())
        {
            Check(!rig.Audio.isPlaying && rig.Session.Run.Phase == RunPhase.Ready,
                "scene Start does not schedule audio on the editor input clock");
            rig.Tick();
            Check(!rig.Audio.isPlaying, "early Update waits for the first dynamic input batch");
            InputSystem.Before(InputUpdateType.Editor, 13218);
            InputSystem.After(InputUpdateType.Editor, 13218);
            rig.Tick();
            Check(!rig.Keyboard.HasProcessedDynamicUpdate && !rig.Audio.isPlaying,
                "editor input updates cannot unlock stage startup");
            InputSystem.Before(InputUpdateType.Fixed, 13218);
            InputSystem.After(InputUpdateType.Fixed, 13218);
            Check(!rig.Keyboard.HasProcessedDynamicUpdate, "fixed input is not the gameplay batch");
            InputSystem.Before(InputUpdateType.Dynamic, 13218);
            Check(!rig.Keyboard.HasProcessedDynamicUpdate, "before-update clock is not ready yet");
            InputSystem.After(InputUpdateType.Dynamic, .025);
            rig.Tick();
            Check(rig.Audio.isPlaying && rig.Session.Run.Phase == RunPhase.Waiting,
                "first usable Update starts both song and chart");
            Near(rig.Audio.ScheduledTime, 200.15, "audio retains the scheduled lead-in");
            Near(rig.Timeline.FromInputTime(2.025), 1.85, "input timestamps use the play-mode origin");

            // Continue bindings must not replace normal shooting or accept motion/release as start.
            var start = rig.Map.Actions["Continue"];
            foreach (string button in new[] { "leftButton", "rightButton", "middleButton", "backButton", "forwardButton" })
                Check(start.Bindings.Contains("<Mouse>/" + button), "start binds mouse " + button);
            Check(start.Bindings.Contains("<Keyboard>/*"), "start binds keyboard keys");
            Check(!start.Bindings.Contains("<Mouse>/position") && !start.Bindings.Contains("<Mouse>/scroll"),
                "mouse motion and scroll are not start bindings");
            var key = new KeyControl { device = new Keyboard() };
            var mouse = new Mouse();
            Mouse.current = mouse;
            var buttonControl = new ButtonControl { device = mouse };
            start.Emit(key, 0, 1);
            start.Emit(buttonControl, 0, 1);
            rig.Keyboard.DrainInto(commands);
            Check(commands.Count == 0, "key and mouse releases do not start a run");
            start.Emit(key, 1, 2.025);
            start.Emit(buttonControl, 1, 2.026);
            rig.Map.Actions["ShootLeft"].Emit(buttonControl, 1, 2.026);
            rig.Keyboard.DrainInto(commands);
            Check(commands.Count == 3 && commands[0].Command == RoomCommand.Continue
                && commands[1].Command == RoomCommand.Continue && commands[2].Command == RoomCommand.ShootLeft,
                "keyboard/mouse start commands coexist with gameplay shooting in timestamp order");

            InputSystem.Before(InputUpdateType.Dynamic, 2.025);
            InputSystem.After(InputUpdateType.Dynamic, 2.03);
            Near(rig.Keyboard.ProcessedThroughTime, 2.025, "timeout watermark stays at the batch start");
            rig.Session.Run.Press(MoveDirection.Up, rig.Timeline.FromInputTime(2.025));
            rig.Session.Run.Advance(rig.Timeline.FromInputTime(2.225));
            Check(rig.Session.Run.CompletedMoves == 1, "chart movement advances alongside the song");

            rig.Session.Run.Advance(10);
            Check(rig.Session.Run.Phase == RunPhase.Dead, "missed next note still kills the run");
            rig.Session.ResetAtBlack();
            rig.Session.BlockedByFade = true;
            rig.Tick();
            rig.Session.BlockedByFade = false;
            for (int i = 0; i < 4; i++) rig.Tick();
            Check(!rig.Audio.isPlaying && rig.Audio.PlayCount == 1 && rig.Session.Run.Phase == RunPhase.Ready
                && rig.Session.Run.CompletedMoves == 0,
                "fade reset stays silent in the first room after the fade opens");
            InputState.currentTime = 50;
            AudioSettings.dspTime = 250;
            Check(rig.Session.TryBeginRun(), "a fresh start request can begin the retry");
            Near(rig.Timeline.FromInputTime(52), 1.85, "retry rebases the song and input clocks together");
            rig.Tick();
            Check(rig.Audio.PlayCount == 2, "progression does not reschedule a manually started retry");
            Call(rig.Keyboard, "OnDisable");
            InputSystem.Before(InputUpdateType.Dynamic, 51);
            InputSystem.After(InputUpdateType.Dynamic, 51);
            Check(!rig.Keyboard.HasProcessedDynamicUpdate, "disabled keyboard unsubscribes its readiness callback");
            Call(rig.Keyboard, "OnEnable");
            Check(!rig.Keyboard.HasProcessedDynamicUpdate, "reenabling input waits for a fresh dynamic batch");
        }

        using (var rig = new Rig())
        {
            InputSystem.Before(InputUpdateType.Dynamic, 100);
            InputSystem.After(InputUpdateType.Dynamic, 100);
            // Debug mode may begin in RoomSession.Update before StageProgression.Update.
            Check(rig.Session.TryBeginRun(), "session can start before progression's first Update");
            rig.Tick();
            rig.Session.ResetAtBlack();
            rig.Tick();
            Check(rig.Audio.PlayCount == 1 && rig.Session.Run.Phase == RunPhase.Ready,
                "a session-started first run cannot trigger progression auto-start after reset");
        }
        Console.WriteLine($"PASS: {checks} stage startup/input clock/retry checks (engine test doubles; no Unity play mode).");
    }
}
