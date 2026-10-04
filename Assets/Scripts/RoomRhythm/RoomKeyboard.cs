using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Gun.RoomRhythm
{
    public enum RoomCommand { Continue, Up, Left, Down, Right, ShootLeft, ShootRight }

    public readonly struct TimedCommand
    {
        public readonly RoomCommand Command;
        public readonly double Time;
        public readonly long Order;
        public readonly Vector2 Pointer;
        public TimedCommand(RoomCommand command, double time, long order, Vector2 pointer)
        { Command = command; Time = time; Order = order; Pointer = pointer; }
    }

    public sealed class RoomKeyboard : MonoBehaviour
    {
        private readonly List<TimedCommand> pending = new List<TimedCommand>();
        private InputActionMap map;
        private long order;
        private InputSettings.UpdateMode previousUpdateMode;
        // Conservative watermark: inputs arriving after this instant belong to a later batch.
        public double ProcessedThroughTime { get; private set; }
        public bool HasProcessedDynamicUpdate { get; private set; }

        private void Awake()
        {
            map = new InputActionMap("Room play");
            InputAction anyPress = map.AddAction("Continue", InputActionType.PassThrough, "<Keyboard>/*");
            foreach (string button in new[] { "leftButton", "rightButton", "middleButton", "backButton", "forwardButton" })
                anyPress.AddBinding("<Mouse>/" + button);
            anyPress.performed += context => {
                bool startButton = context.control is KeyControl
                    || context.control.device is Mouse && context.control is ButtonControl;
                if (startButton && context.ReadValue<float>() > .5f)
                    pending.Add(new TimedCommand(RoomCommand.Continue, context.time, order++, Vector2.zero));
            };
            Bind(RoomCommand.Up, "w");
            Bind(RoomCommand.Left, "a");
            Bind(RoomCommand.Down, "s");
            Bind(RoomCommand.Right, "d");
            BindPath(RoomCommand.ShootLeft, "<Mouse>/leftButton");
            BindPath(RoomCommand.ShootRight, "<Mouse>/rightButton");
        }

        private void Bind(RoomCommand command, string key)
            => BindPath(command, "<Keyboard>/" + key);

        private void BindPath(RoomCommand command, string path)
        {
            InputAction action = map.AddAction(command.ToString(), InputActionType.Button,
                path, interactions: "press(behavior=0)");
            action.performed += context => pending.Add(new TimedCommand(command, context.time, order++,
                Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero));
        }

        private void OnEnable()
        {
            previousUpdateMode = InputSystem.settings.updateMode;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            HasProcessedDynamicUpdate = false;
            ProcessedThroughTime = InputState.currentTime;
            InputSystem.onBeforeUpdate += BeforeInputUpdate;
            InputSystem.onAfterUpdate += AfterInputUpdate;
            map.Enable();
        }
        private void BeforeInputUpdate()
        {
            if (InputState.currentUpdateType == InputUpdateType.Dynamic)
                ProcessedThroughTime = InputState.currentTime;
        }
        private void AfterInputUpdate()
        {
            // The Editor-to-play time offset is established during the first dynamic update.
            // Do not anchor the song's input clock in Awake/OnEnable/Start.
            if (InputState.currentUpdateType == InputUpdateType.Dynamic)
                HasProcessedDynamicUpdate = true;
        }
        private void OnDisable()
        {
            InputSystem.onBeforeUpdate -= BeforeInputUpdate;
            InputSystem.onAfterUpdate -= AfterInputUpdate;
            HasProcessedDynamicUpdate = false;
            map.Disable(); pending.Clear();
            InputSystem.settings.updateMode = previousUpdateMode;
        }
        private void OnDestroy() => map.Dispose();

        public void DrainInto(List<TimedCommand> output)
        {
            output.Clear();
            output.AddRange(pending);
            pending.Clear();
            output.Sort((a, b) => a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.Order.CompareTo(b.Order));
        }
    }
}
