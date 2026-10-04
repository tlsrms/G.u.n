using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gun.RoomRhythm
{
    public sealed class RoomSession : MonoBehaviour
    {
        [SerializeField] private RoomChart chart;
        [SerializeField] private StageDirector stageDirector;
        [SerializeField] private StageResetState resetState;
        private bool stageCleared, chartCompletionSent, clearPresented;
        [SerializeField] private SongTimeline timeline;
        [SerializeField] private RoomKeyboard keyboard;
        [SerializeField] private Transform player;
        [SerializeField] private SpriteRenderer playerSprite;
        [SerializeField] private GeometricPlayerRig playerRig;
        [SerializeField] private RoomBinding[] rooms;
        [SerializeField] private TextMesh status;
        [SerializeField] private RoomAim aim;
        [SerializeField] private RoomCombat combat;
        [SerializeField] private RoomFeedback feedback;
        [SerializeField] private DebugTimingBar timingBar;
        private RunPhase lastFeedbackPhase;
        private int presentedMoves;
        private readonly List<TimedCommand> commands = new List<TimedCommand>();
        private RoomBinding[] path;
        private Vector3[] playerStops;
        private Vector2[] playerPassages;
        private int[] previousRoomOccurrences;
        private RoomRun run;
        private int configuredRevision;
        private int configuredJudgmentRevision;
        private double presentationTime;
        private RoomCinematics cinematics;
        private RoomRestartTransition restartTransition;
        // Survives run resets so debug auto-start only applies to the first entry.
        private bool hasStarted;
        public bool HasStarted => hasStarted;
        private bool[] deathRooms, deathFrames, deathDoorFrames;
        public RoomCinematics Cinematics => cinematics;
        public double PresentationTime => presentationTime;
        public RoomChart Chart => chart;
        public bool IsCleared => stageCleared;
        public bool IsChartCompleted => run != null && run.Phase == RunPhase.Cleared;
        public double SongTime => timeline != null ? timeline.Time : 0;
        public float AccuracyPercent => run == null || run.JudgmentVersion == 0 ? 0
            : run.AccurateJudgments * 100f / run.JudgmentVersion;
        [SerializeField] private bool restartOnClear = true;
        [SerializeField] private bool logInputTiming = true;
        [Header("Debug")]
        [Tooltip("시작 시 적용: 무적 자동 진행으로 이동·문 파괴·사격을 처리합니다. 최고 기록은 저장하지 않습니다.")]
        [SerializeField] private bool debugMode;
        public bool IsDebugRun { get; private set; }
        private readonly Color alive = Color.white;

        private void Start()
        {
            restartTransition = GetComponent<RoomRestartTransition>();
            if (restartTransition == null) restartTransition = gameObject.AddComponent<RoomRestartTransition>();
            if (resetState != null) resetState.Capture();
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
                stageCleared = chartCompletionSent = clearPresented = false;
                if (stageDirector != null) stageDirector.Bind(this);
                ConfigureText(status);
                run = BuildValidatedRun();
                ConfigureScene();
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

        private void ConfigureScene()
        {
            foreach (var room in rooms) room.gameObject.SetActive(true);
            for (int i = 0; i < path.Length; i++)
                path[i].Configure(chart, i < chart.moves.Length ? chart.moves[i].direction : (MoveDirection?)null,
                    i > 0 ? (MoveDirection)(((int)chart.moves[i - 1].direction + 2) % 4) : (MoveDirection?)null);
            aim.Configure(chart.aimRadius, chart.aimHalfAngle, feedback);
            previousRoomOccurrences = new int[path.Length];
            for (int i = 0; i < path.Length; i++)
            {
                previousRoomOccurrences[i] = -1;
                for (int j = i - 1; j >= 0; j--)
                    if (path[i].Overlaps(path[j]))
                    { previousRoomOccurrences[i] = j; break; }
            }
            combat.Configure(chart, path, run, aim, feedback, previousRoomOccurrences);
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
            var sceneLayout = new MapChart { roomSize = chart.appliedMap?.roomSize ?? path[0].SideLength };
            MapRoom Geometry(RoomBinding binding) => new MapRoom {
                width = binding.Size.x, height = binding.Size.y,
                offsetX = binding.Center.x, offsetY = binding.Center.y
            };
            var visited = new HashSet<string> { chart.startingRoomId };
            for (int i = 0; i < chart.moves.Length; i++)
            {
                MoveNote note = chart.moves[i];
                if (!visited.Add(note.destinationId)) throw new InvalidOperationException("Room ID reused.");
                path[i + 1] = lookup[note.destinationId];
                bool customDoor = combat.FindOverride(StageTargetRole.Breakthrough, note.destinationId) != null;
                if (note.hasDoor && !customDoor && path[i + 1].Door == null)
                    throw new InvalidOperationException("Door note is missing its scene door.");
                var previous = Geometry(path[i]); var next = Geometry(path[i + 1]);
                if (!sceneLayout.Connection(previous, next, out var direction) || direction != note.direction)
                    throw new InvalidOperationException("Next room must share an edge with the current room.");
                var passage = sceneLayout.PassagePosition(previous, next);
                if (note.hasDoor && !customDoor && Vector3.Distance(path[i + 1].Door.Target,
                    new Vector3(passage.x, passage.y, path[i].Center.z)) > .01f)
                    throw new InvalidOperationException("Door must be at the shared room entrance.");
            }
            playerStops = new Vector3[path.Length];
            playerPassages = new Vector2[chart.moves.Length];
            for (int i = 0; i < path.Length; i++)
            {
                var anchor = sceneLayout.PlayerAnchor(Geometry(path[i]), i + 1 < path.Length ? Geometry(path[i + 1]) : null);
                playerStops[i] = new Vector3(anchor.x, anchor.y, path[i].Center.z);
                if (i + 1 < path.Length)
                {
                    var passage = sceneLayout.PassagePosition(Geometry(path[i]), Geometry(path[i + 1]));
                    playerPassages[i] = new Vector2(passage.x, passage.y);
                }
            }
            if (!(chart.aimRadius > 0) || !(chart.aimHalfAngle > 0 && chart.aimHalfAngle < 90))
                throw new InvalidOperationException("Invalid aiming geometry.");
            if (!(chart.judgmentLineWidth > 0 && chart.judgmentLineWidth < 0.38f))
                throw new InvalidOperationException("Judgment line width must be between 0 and the enemy radius (0.38).");
            if (!(chart.enemyLineWidth > 0 && chart.enemyLineWidth < 0.38f))
                throw new InvalidOperationException("Enemy line width must be between 0 and the enemy radius (0.38).");
            foreach (RoomBinding room in path)
                if (!(chart.passageWidth > 0 && chart.passageWidth < Mathf.Min(room.Size.x, room.Size.y) - chart.judgmentLineWidth))
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
                if (continuePressed || debugMode && !hasStarted)
                {
                    TryBeginRun();
                }
                Present();
                return;
            }
            bool restartAfterDeath = run.Phase == RunPhase.Dead && (cinematics.Complete || continuePressed);
            if (restartAfterDeath || IsCleared && restartOnClear && continuePressed)
            {
                restartTransition.Begin(InitializeRun);
                return;
            }
            double frameTime = run.Phase == RunPhase.Ready ? 0 : timeline.FromInputTime(keyboard.ProcessedThroughTime);
            foreach (TimedCommand command in commands)
            {
                if (IsDebugRun) continue;
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
                if (IsDebugRun) run.AdvanceAutomatically(frameTime, PresentDebugAction);
                else run.Advance(frameTime);
            }
            else if (commands.Count > 0 && lastFeedbackPhase != run.Phase)
                presentationTime = frameTime;
            DispatchStage();
            if (run.Phase != lastFeedbackPhase && run.Phase == RunPhase.Dead)
            {
                FreezePresentation();
                cinematics.BeginDeath(run, path[run.CompletedMoves], chart, PlayerPosition(presentationTime), combat.FailureEnemy());
                aim.SetPresentation(false);
            }
            if (run.Phase == RunPhase.Dead)
                presentationTime = run.DeathTime;
            cinematics.SetCombatFocus(run.IsActive && run.Phase != RunPhase.Moving
                && combat.HasVisibleEnemies(run.CompletedMoves, presentationTime));
            lastFeedbackPhase = run.Phase;
            Present();
            if (stageDirector != null && run.Phase != RunPhase.Dead && !IsCleared)
                stageDirector.Tick(SongTime);
            if (IsCleared && !clearPresented)
            {
                clearPresented = true;
                feedback.Outcome(PlayerPosition(presentationTime), true);
            }
            if (run.Phase == RunPhase.Dead || IsCleared) timeline.Stop();
        }

        private void ResetRun()
        {
            timeline.ResetTimeline();
            run.Reset();
            IsDebugRun = false;
            presentationTime = 0;
            aim.ResetAim();
            feedback.ResetFeedback();
            cinematics.ResetPresentation();
            deathRooms = deathFrames = deathDoorFrames = null;
            lastFeedbackPhase = RunPhase.Ready;
            presentedMoves = 0;
            stageCleared = chartCompletionSent = clearPresented = false;
            if (resetState != null) resetState.Restore();
            foreach (var room in rooms) room.gameObject.SetActive(true);
            if (stageDirector != null) stageDirector.RestartStage();
            combat.ResetStageTargets();
            Present();
        }

        private void DispatchStage()
        {
            while (run.TryDequeueResult(out RoomActionResult result))
                if (stageDirector != null) stageDirector.OnAction(result);
            if (IsChartCompleted && !chartCompletionSent)
            {
                chartCompletionSent = true;
                if (stageDirector != null) stageDirector.OnChartCompleted();
                else CompleteStage();
            }
        }

        public bool CompleteStage()
        {
            if (!IsChartCompleted || stageCleared) return false;
            stageCleared = true;
            return true;
        }

        public bool TryBeginRun()
        {
            if (run == null || run.Phase != RunPhase.Ready || !keyboard.HasProcessedDynamicUpdate
                || restartTransition != null && restartTransition.BlocksInput)
                return false;
            timeline.Begin(chart.music, chart.MusicDelaySeconds, chart.LoopMusic, InputOffsetSettings.Milliseconds(chart));
            IsDebugRun = debugMode || StageSelection.IsRecordRun && StageSelection.DebugMode;
            run.Begin();
            hasStarted = true;
            if (stageDirector != null) stageDirector.BeginStage();
            return true;
        }

        private void PresentDebugAction(RoomActionResult result)
        {
            combat.PresentDebugAction(result, PlayerPosition(result.Time));
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
            Vector3 position = playerStops[run.CompletedMoves];
            if (run.Phase == RunPhase.Moving)
            {
                MoveNote move = chart.moves[run.CompletedMoves];
                float t = (float)MovementProfile.Evaluate((time - run.MoveStartedAt) / move.Duration(chart.moveDuration), move.ease);
                Vector3 target = playerStops[run.CompletedMoves + 1];
                Vector2 passage = playerPassages[run.CompletedMoves];
                var point = MapChart.PlayerMovePosition((position.x, position.y), (passage.x, passage.y), (target.x, target.y), t);
                position = new Vector3(point.x, point.y, Mathf.Lerp(position.z, target.z, t));
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
                    time, note.HitTime, Progress(time, frameStart, note.HitTime), isCurrent && run.Death == DeathPresentation.Collision,
                    frameStart, isCurrent ? run.RoomArrivedAt : double.NegativeInfinity);
                if (path[i].Door != null)
                    path[i].Door.Present(i > current && note.hasDoor && !run.DoorBroken(i - 1)
                        && combat.FindOverride(StageTargetRole.Breakthrough, note.destinationId) == null
                        && run.Phase != RunPhase.Ready && time >= note.DoorAppearsAt && previousRoomOccurrences[i] < current,
                        future, Progress(time, note.DoorAppearsAt, note.doorTime), time, note.doorTime, note.DoorAppearsAt,
                        note.DoorAppearsAt,
                        deathDoorFrames != null ? deathDoorFrames[i] : run.RoomFrameVisible(i, time, true));
            }
            player.position = run.Phase == RunPhase.Dead ? cinematics.DeathPosition : PlayerPosition(time);
            combat.Present(time);
            Color playerColor = RoomPalette.Tint(alive);
            playerColor.a *= cinematics.PlayerAlpha;
            playerSprite.color = playerColor;
            playerSprite.enabled = cinematics.PlayerVisible;
            if (playerRig != null) playerRig.SetPresentation(cinematics.PlayerVisible, cinematics.PlayerAlpha);
            if (run.Phase == RunPhase.Dead && cinematics.Death == DeathPresentation.Execution)
                aim.FadeWithPlayer(cinematics.PlayerAlpha);
            if (current > presentedMoves && run.Phase != RunPhase.Dead)
                feedback.ArrivalDust(playerStops[current], playerStops[current] - playerStops[current - 1]);
            presentedMoves = current;
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
