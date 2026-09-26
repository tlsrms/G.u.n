using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gun.RoomRhythm
{
    public sealed class RoomSession : MonoBehaviour
    {
        [SerializeField] private RoomChart chart;
        [SerializeField] private SongTimeline timeline;
        [SerializeField] private RoomKeyboard keyboard;
        [SerializeField] private Transform player;
        [SerializeField] private SpriteRenderer playerSprite;
        [SerializeField] private RoomBinding[] rooms;
        [SerializeField] private TextMesh status;
        [SerializeField] private TextMesh cue;
        [SerializeField] private RoomAim aim;
        [SerializeField] private RoomCombat combat;
        [SerializeField] private RoomFeedback feedback;
        [SerializeField] private DebugTimingBar timingBar;
        private RunPhase lastFeedbackPhase;
        private readonly List<TimedCommand> commands = new List<TimedCommand>();
        private RoomBinding[] path;
        private RoomRun run;
        private int configuredRevision;
        private double presentationTime;
        public double PresentationTime => presentationTime;
        public RoomChart Chart => chart;
        [SerializeField] private bool logInputTiming = true;
        private readonly Color alive = new Color(0.3f, 1f, 0.8f);

        private void Start() => InitializeRun();

        private void InitializeRun()
        {
            if (timeline != null) timeline.Stop();
            if (combat != null) combat.Suspend();
            configuredRevision = chart != null ? chart.Revision : 0;
            try
            {
                ConfigureText(status);
                ConfigureText(cue);
                run = BuildValidatedRun();
                foreach (RoomBinding room in path) room.Configure(chart);
                aim.Configure(chart.aimRadius, chart.aimHalfAngle);
                combat.Configure(chart, path, run, aim, feedback);
                ResetRun();
            }
            catch (Exception error)
            {
                Debug.LogError("Room chart configuration: " + error.Message, this);
                if (status != null) status.text = "CHART SETUP ERROR";
                run = null;
                if (cue != null) cue.text = "FIX CHART SETTINGS";
            }
        }

        // Called by the editor inspector as well as startup. Does not play audio or change scene visuals.
        public void ValidateConfiguration() => BuildValidatedRun();

        private static void ConfigureText(TextMesh text)
        {
            if (text == null || text.font == null)
                throw new InvalidOperationException("HUD needs an assigned font asset.");
            // Use the imported font atlas material, not an unrelated built-in material reference.
            MeshRenderer renderer = text.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = text.font.material;
            renderer.sortingOrder = 200;
        }

        private RoomRun BuildValidatedRun()
        {
            ValidateBindings();
            var validated = new RoomRun(chart.BuildMovementNotes(), chart.Timing, chart.moveDuration,
                chart.enemies, chart.startingRoomId, chart.enemyReadTime, chart.enemyLeadTime);
            aim.ValidateReferences();
            timeline.ValidateReferences();
            feedback.ValidateReferences();
            if (timingBar != null) timingBar.ValidateReferences();
            for (int i = 0; i < path.Length; i++) path[i].ValidateReferences(i > 0);
            combat.ValidateConfiguration(chart, path, validated);
            return validated;
        }

        private void ValidateBindings()
        {
            if (chart == null || chart.music == null || timeline == null || keyboard == null
                || player == null || playerSprite == null || status == null || cue == null || aim == null || combat == null || feedback == null)
                throw new InvalidOperationException("Missing required scene reference.");
            if (rooms == null || chart.moves == null || chart.moves.Length == 0 || !(chart.bpm > 0))
                throw new InvalidOperationException("Chart needs rooms, movement notes and a positive BPM.");
            var lookup = new Dictionary<string, RoomBinding>();
            foreach (RoomBinding room in rooms)
                if (room == null || string.IsNullOrWhiteSpace(room.Id) || !lookup.TryAdd(room.Id, room))
                    throw new InvalidOperationException("Missing or duplicate room ID.");
            path = new RoomBinding[chart.moves.Length + 1];
            if (lookup.Count != path.Length) throw new InvalidOperationException("Room bindings do not match chart.");
            path[0] = lookup[chart.startingRoomId];
            var visited = new HashSet<string> { chart.startingRoomId };
            for (int i = 0; i < chart.moves.Length; i++)
            {
                MoveNote note = chart.moves[i];
                if (!visited.Add(note.destinationId)) throw new InvalidOperationException("Room ID reused.");
                path[i + 1] = lookup[note.destinationId];
                if (note.hasDoor && path[i + 1].Door == null)
                    throw new InvalidOperationException("Door note is missing its scene door.");
                if (note.hasDoor && Vector3.Distance(path[i + 1].Door.Target, (path[i].Center + path[i + 1].Center) * 0.5f) > 0.01f)
                    throw new InvalidOperationException("Door must be at the shared room entrance.");
                Vector2 delta = path[i + 1].Center - path[i].Center;
                Vector2 expected = note.direction == MoveDirection.Up ? Vector2.up
                    : note.direction == MoveDirection.Left ? Vector2.left
                    : note.direction == MoveDirection.Down ? Vector2.down : Vector2.right;
                if (delta.sqrMagnitude < 0.01f || Vector2.Dot(delta.normalized, expected) < 0.999f)
                    throw new InvalidOperationException("Room positions disagree with chart direction.");
                float spacing = (path[i].SideLength + path[i + 1].SideLength) * 0.5f;
                if (Mathf.Abs(delta.magnitude - spacing) > 0.01f)
                    throw new InvalidOperationException("Next room must share an edge with the current room.");
            }
            if (!(chart.aimRadius > 0) || !(chart.aimHalfAngle > 0 && chart.aimHalfAngle < 90))
                throw new InvalidOperationException("Invalid aiming geometry.");
            if (!(chart.judgmentLineWidth > 0 && chart.judgmentLineWidth < 0.38f))
                throw new InvalidOperationException("Judgment line width must be between 0 and the enemy radius (0.38).");
            if (!(chart.enemyLineWidth > 0 && chart.enemyLineWidth < 0.38f))
                throw new InvalidOperationException("Enemy line width must be between 0 and the enemy radius (0.38).");
            foreach (RoomBinding room in path)
                if (!(chart.passageWidth > 0 && chart.passageWidth < room.SideLength - chart.judgmentLineWidth))
                    throw new InvalidOperationException("Invalid central passage width.");
            if (chart.moves[chart.moves.Length - 1].HitTime + chart.Timing.late + chart.moveDuration > chart.music.length)
                throw new InvalidOperationException("Music ends before the last movement finishes.");
        }

        private void Update()
        {
            // The model copies timing and notes, while visuals read the chart. Rebuild BOTH
            // when the Inspector changes the asset; never mix old judgments with new visuals.
            if (chart != null && chart.Revision != configuredRevision)
            {
                InitializeRun();
                keyboard.DrainInto(commands);
                return;
            }
            if (run == null) return;
            keyboard.DrainInto(commands);
            double frameTime = run.Phase == RunPhase.Ready ? 0 : timeline.FromInputTime(keyboard.ProcessedThroughTime);
            foreach (TimedCommand command in commands)
            {
                if (command.Command == RoomCommand.Reset) { InitializeRun(); return; }
                if (command.Command == RoomCommand.Start)
                {
                    if (run.Phase == RunPhase.Ready)
                    {
                        timeline.Begin(chart.music); run.Begin();
                        frameTime = timeline.FromInputTime(keyboard.ProcessedThroughTime);
                    }
                    continue;
                }
                if (!run.IsActive) continue;
                double time = timeline.FromInputTime(command.Time);
                frameTime = Math.Max(frameTime, time);
                if (command.Command == RoomCommand.ShootLeft || command.Command == RoomCommand.ShootRight)
                    Shoot(command.Pointer, time);
                else
                {
                    double target = run.CompletedMoves < chart.moves.Length ? chart.moves[run.CompletedMoves].HitTime : time;
                    RunPhase before = run.Phase;
                    run.Press((MoveDirection)((int)command.Command - (int)RoomCommand.Up), time);
                    if (logInputTiming && (Application.isEditor || Debug.isDebugBuild))
                        Debug.Log($"[Rhythm Input] {command.Command}: {(time - target) * 1000:+0.0;-0.0;0.0} ms, event={time:F4}s, target={target:F4}s, {before}->{run.Phase}, grade={run.LastGrade}", this);
                }
            }
            // Do not commit a timeout beyond the input batch we have actually received.
            // Visuals use that identical clock sample, rather than a later DSP read.
            if (run.IsActive)
            {
                presentationTime = frameTime;
                run.Advance(frameTime);
            }
            else if (commands.Count > 0 && lastFeedbackPhase != run.Phase)
                presentationTime = frameTime;
            if (run.Phase == RunPhase.Dead || run.Phase == RunPhase.Cleared) timeline.Stop();
            if (run.Phase != lastFeedbackPhase && (run.Phase == RunPhase.Dead || run.Phase == RunPhase.Cleared))
                feedback.Outcome(PlayerPosition(presentationTime), run.Phase == RunPhase.Cleared);
            lastFeedbackPhase = run.Phase;
            Present();
        }

        private void ResetRun()
        {
            timeline.ResetTimeline();
            run.Reset();
            presentationTime = 0;
            aim.ResetAim();
            feedback.ResetFeedback();
            lastFeedbackPhase = RunPhase.Ready;
            Present();
        }

        private void Shoot(Vector2 pointer, double time)
        {
            run.Advance(time);
            if (!run.IsActive) return;
            Vector3 origin = PlayerPosition(time);
            combat.Shoot(pointer, time, origin);
        }

        private Vector3 PlayerPosition(double time)
        {
            Vector3 position = path[run.CompletedMoves].Center;
            if (run.Phase == RunPhase.Moving)
            {
                float t = Mathf.Clamp01((float)((time - run.MoveStartedAt) / chart.moveDuration));
                position = Vector3.Lerp(position, path[run.CompletedMoves + 1].Center, t * t * (3f - 2f * t));
            }
            return position;
        }

        private void Present()
        {
            int current = run.CompletedMoves;
            double time = presentationTime;
            for (int i = 0; i < path.Length; i++)
            {
                bool isCurrent = i == current;
                MoveNote note = i > 0 ? chart.moves[i - 1] : default;
                note.appearTime = i > 0 ? chart.RoomAppearsAt(note) : 0;
                bool visible = isCurrent || (i > current && run.Phase != RunPhase.Ready && time >= note.appearTime);
                bool future = i > current + 1;
                bool movingInto = run.Phase == RunPhase.Moving && i == current + 1;
                float progress = isCurrent || movingInto ? 1f : Progress(time, note.appearTime, note.HitTime);
                double frameStart = note.customAppearance ? note.frameStartTime : note.appearTime;
                path[i].Present(visible, isCurrent, future, progress, i > current && !movingInto && time >= frameStart,
                    time, note.HitTime, Progress(time, frameStart, note.HitTime));
                if (path[i].Door != null)
                    path[i].Door.Present(visible && i > current && note.hasDoor && !run.DoorBroken(i - 1),
                        future, Progress(time, note.appearTime, note.doorTime), time, note.doorTime, note.appearTime,
                        note.customAppearance ? note.doorFrameStartTime : note.appearTime);
            }
            player.position = PlayerPosition(time);
            combat.Present(time);
            playerSprite.color = run.Phase == RunPhase.Dead ? new Color(1f, 0.3f, 0.35f) : alive;
            bool showGrade = run.JudgmentVersion > 0 && time - run.LastJudgedAt < 0.9;
            status.color = Color.white;
            cue.color = Color.white;
            if (timingBar != null && run.Phase != RunPhase.Waiting && run.Phase != RunPhase.Dead)
                timingBar.Hide();
            if (timingBar != null && (run.Phase == RunPhase.Waiting || run.Phase == RunPhase.Dead))
            {
                int nextEnemy = run.NextEnemyIndex();
                if (nextEnemy >= 0) timingBar.Present(time, chart.enemies[nextEnemy].time, chart.Timing);
                else if (current < chart.moves.Length)
                {
                    MoveNote next = chart.moves[current];
                    timingBar.Present(time, next.hasDoor && !run.DoorBroken(current) ? next.doorTime : next.HitTime, chart.Timing);
                }
                else timingBar.Hide();
            }
            if (run.Phase == RunPhase.Ready)
            {
                status.text = "SPACE : START     R : RESET";
                cue.text = "WASD : MOVE / AIM + CLICK : SHOOT";
            }
            else if (run.Phase == RunPhase.Dead)
            {
                status.text = run.Failure == FailureReason.WrongDirection ? "WRONG DIRECTION"
                    : JudgmentPresentation.Text(run.LastGrade);
                status.color = JudgmentPresentation.Tint(run.Failure == FailureReason.WrongDirection
                    ? TimingGrade.TooLate : run.LastGrade);
                cue.text = (run.Failure == FailureReason.MissedEnemy ? "MISSED ENEMY / "
                    : run.Failure == FailureReason.MissedDoor ? "MISSED DOOR / " : "") + "R : RETRY";
            }
            else if (run.Phase == RunPhase.Cleared)
            {
                status.text = "CLEAR / " + JudgmentPresentation.Text(run.LastGrade);
                status.color = JudgmentPresentation.Tint(run.LastGrade);
                cue.text = "R : RETRY";
            }
            else if (run.Phase == RunPhase.Moving)
            {
                status.text = JudgmentPresentation.Text(run.LastGrade);
                status.color = JudgmentPresentation.Tint(run.LastGrade);
                cue.text = "";
            }
            else
            {
                status.text = showGrade ? JudgmentPresentation.Text(run.LastGrade) : "WASD : MOVE     CLICK : SHOOT     R : RESET";
                if (showGrade) status.color = JudgmentPresentation.Tint(run.LastGrade);
                int enemy = run.NextEnemyIndex();
                MoveNote note = current < chart.moves.Length ? chart.moves[current] : default;
                bool doorNext = enemy < 0 && note.hasDoor && !run.DoorBroken(current);
                string key = enemy >= 0 ? "SHOOT ENEMY" : doorNext ? "SHOOT DOOR" : KeyText(note.direction);
                cue.text = key;
            }
        }

        private static float Progress(double time, double start, double end)
            => end > start ? Mathf.Clamp01((float)((time - start) / (end - start))) : 1f;

        private static string KeyText(MoveDirection direction) => direction == MoveDirection.Up ? "W"
            : direction == MoveDirection.Left ? "A" : direction == MoveDirection.Down ? "S" : "D";
        private void OnDisable() { if (timeline != null) timeline.Stop(); }
    }
}
