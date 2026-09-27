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
        [SerializeField] private RoomAim aim;
        [SerializeField] private RoomCombat combat;
        [SerializeField] private RoomFeedback feedback;
        [SerializeField] private DebugTimingBar timingBar;
        private RunPhase lastFeedbackPhase;
        private readonly List<TimedCommand> commands = new List<TimedCommand>();
        private RoomBinding[] path;
        private int[] previousRoomOccurrences;
        private RoomRun run;
        private int configuredRevision;
        private int configuredJudgmentRevision;
        private double presentationTime;
        private RoomCinematics cinematics;
        private RoomRestartTransition restartTransition;
        private bool[] deathRooms, deathFrames, deathDoorFrames;
        public RoomCinematics Cinematics => cinematics;
        public double PresentationTime => presentationTime;
        public RoomChart Chart => chart;
        [SerializeField] private bool logInputTiming = true;
        private readonly Color alive = new Color(0.3f, 1f, 0.8f);

        private void Start()
        {
            restartTransition = GetComponent<RoomRestartTransition>();
            if (restartTransition == null) restartTransition = gameObject.AddComponent<RoomRestartTransition>();
            InitializeRun();
        }

        private void InitializeRun()
        {
            if (timeline != null) timeline.Stop();
            if (combat != null) combat.Suspend();
            configuredRevision = chart != null ? chart.Revision : 0;
            configuredJudgmentRevision = JudgmentSettings.Revision;
            try
            {
                ConfigureText(status);
                run = BuildValidatedRun();
                foreach (RoomBinding room in path) room.Configure(chart);
                aim.Configure(chart.aimRadius, chart.aimHalfAngle, feedback);
                previousRoomOccurrences = new int[path.Length];
                for (int i = 0; i < path.Length; i++)
                {
                    previousRoomOccurrences[i] = -1;
                    for (int j = i - 1; j >= 0; j--)
                        if ((path[i].Center - path[j].Center).sqrMagnitude < .0001f)
                        { previousRoomOccurrences[i] = j; break; }
                }
                combat.Configure(chart, path, run, aim, feedback, previousRoomOccurrences);
                cinematics = GetComponent<RoomCinematics>();
                if (cinematics == null) cinematics = gameObject.AddComponent<RoomCinematics>();
                cinematics.Configure(playerSprite, feedback);
                ResetRun();
            }
            catch (Exception error)
            {
                Debug.LogError("Room chart configuration: " + error.Message, this);
                if (status != null) status.text = "";
                run = null;
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
                || player == null || playerSprite == null || status == null || aim == null || combat == null || feedback == null)
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
            if (!chart.LoopMusic && chart.moves[chart.moves.Length - 1].HitTime > chart.music.length + chart.MusicDelaySeconds)
                throw new InvalidOperationException("마지막 방의 정확 판정 시각이 음원 종료 이후입니다.");
        }

        private void Update()
        {
            // The model copies timing and notes, while visuals read the chart. Rebuild BOTH
            // when the Inspector changes the asset; never mix old judgments with new visuals.
            if (chart != null && (chart.Revision != configuredRevision || JudgmentSettings.Revision != configuredJudgmentRevision))
            {
                InitializeRun();
                keyboard.DrainInto(commands);
                return;
            }
            if (run == null) return;
            keyboard.DrainInto(commands);
            if (restartTransition.BlocksInput) return;
            bool continuePressed = commands.Exists(command => command.Command == RoomCommand.Continue);
            if (run.Phase == RunPhase.Ready)
            {
                if (continuePressed)
                {
                    timeline.Begin(chart.music, chart.MusicDelaySeconds, chart.LoopMusic, InputOffsetSettings.Milliseconds(chart));
                    run.Begin();
                }
                Present();
                return;
            }
            if ((run.Phase == RunPhase.Dead || run.Phase == RunPhase.Cleared) && continuePressed)
            {
                restartTransition.Begin(InitializeRun);
                return;
            }
            double frameTime = run.Phase == RunPhase.Ready ? 0 : timeline.FromInputTime(keyboard.ProcessedThroughTime);
            foreach (TimedCommand command in commands)
            {
                if (command.Command == RoomCommand.Continue) continue;
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
            if (run.Phase != lastFeedbackPhase && (run.Phase == RunPhase.Dead || run.Phase == RunPhase.Cleared))
            {
                if (run.Phase == RunPhase.Dead)
                {
                    FreezePresentation();
                    cinematics.BeginDeath(run, path[run.CompletedMoves], chart, PlayerPosition(presentationTime), combat.FailureEnemy());
                    aim.SetPresentation(false);
                }
                else feedback.Outcome(PlayerPosition(presentationTime), true);
            }
            if (run.Phase == RunPhase.Dead || run.Phase == RunPhase.Cleared) timeline.Stop();
            if (run.Phase == RunPhase.Dead)
                presentationTime = run.DeathTime;
            cinematics.SetCombatFocus(run.IsActive && run.Phase != RunPhase.Moving && combat.HasLivingEnemies(run.CompletedMoves));
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
            cinematics.ResetPresentation();
            deathRooms = deathFrames = deathDoorFrames = null;
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

        private void FreezePresentation()
        {
            double time = run.DeathTime;
            int current = run.CompletedMoves;
            deathRooms = new bool[path.Length];
            deathFrames = new bool[path.Length];
            deathDoorFrames = new bool[path.Length];
            for (int i = 0; i < path.Length; i++)
            {
                deathRooms[i] = (i == current || run.Death != DeathPresentation.Departure)
                    && run.RoomVisible(i, time, previousRoomOccurrences[i]);
            }
            combat.FreezeAtDeath(time);
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
                bool visible = run.RoomVisible(i, time, previousRoomOccurrences[i]);
                if (deathRooms != null) visible = deathRooms[i];
                bool future = i > current + 1;
                bool movingInto = run.Phase == RunPhase.Moving && i == current + 1;
                float progress = isCurrent || movingInto ? 1f : Progress(time, note.appearTime, note.HitTime);
                double frameStart = note.customAppearance ? note.frameStartTime : note.appearTime;
                path[i].Present(visible, isCurrent, future, progress, deathFrames != null ? deathFrames[i] : run.RoomFrameVisible(i, time),
                    time, note.HitTime, Progress(time, frameStart, note.HitTime));
                if (path[i].Door != null)
                    path[i].Door.Present(visible && i > current && note.hasDoor && !run.DoorBroken(i - 1),
                        future, Progress(time, note.appearTime, note.doorTime), time, note.doorTime, note.appearTime,
                        note.customAppearance ? note.doorFrameStartTime : note.appearTime,
                        deathDoorFrames != null ? deathDoorFrames[i] : run.RoomFrameVisible(i, time, true));
            }
            player.position = run.Phase == RunPhase.Dead ? cinematics.DeathPosition : PlayerPosition(time);
            combat.Present(time);
            Color playerColor = RoomPalette.Tint(alive);
            playerColor.a *= cinematics.PlayerAlpha;
            playerSprite.color = playerColor;
            playerSprite.enabled = cinematics.PlayerVisible;
            if (run.Phase == RunPhase.Dead && cinematics.Death == DeathPresentation.Execution)
                aim.FadeWithPlayer(cinematics.PlayerAlpha);
            if (run.Phase == RunPhase.Moving) cinematics.Trail();
            bool showGrade = run.JudgmentVersion > 0 && time - run.LastJudgedAt < 0.9;
            status.color = Color.white;
            if (timingBar != null && run.Phase != RunPhase.Waiting)
                timingBar.Hide();
            if (timingBar != null && run.Phase == RunPhase.Waiting)
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
            bool retainGrade = run.Phase == RunPhase.Dead || run.Phase == RunPhase.Cleared || run.Phase == RunPhase.Moving;
            status.text = run.Phase != RunPhase.Ready && (showGrade || retainGrade)
                ? JudgmentPresentation.Text(run.LastGrade, run.LastTimingErrorMs) : "";
            status.color = JudgmentPresentation.Tint(run.LastGrade);
        }

        private static float Progress(double time, double start, double end)
            => end > start ? Mathf.Clamp01((float)((time - start) / (end - start))) : 1f;

        private void OnDisable() { if (timeline != null) timeline.Stop(); }
    }
}
