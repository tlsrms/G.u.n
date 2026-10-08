using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gun.RoomRhythm
{
    [DefaultExecutionOrder(200)]
    public sealed class RoomCombat : MonoBehaviour
    {
        [SerializeField] private RoomEnemy[] enemies;
        [SerializeField] private StageActionTarget[] stageTargets = Array.Empty<StageActionTarget>();
        private StageActionTarget[] shotOverrides, doorOverrides;
        public StageActionTarget FindOverride(StageTargetRole role, string id)
            => Array.Find(stageTargets ?? Array.Empty<StageActionTarget>(), t => t != null && t.Role == role && t.Id == id);

        public void ValidateStageTargetOwnership(RoomBinding[] rebuiltRooms)
        {
            foreach (var target in stageTargets ?? Array.Empty<StageActionTarget>())
            {
                if (target == null) throw new InvalidOperationException("Missing stage target reference.");
                foreach (var room in rebuiltRooms)
                    if (room != null) target.ValidateOutside(room.transform);
                foreach (var enemy in enemies ?? Array.Empty<RoomEnemy>())
                    if (enemy != null) target.ValidateOutside(enemy.transform);
            }
        }
        [SerializeField] private Transform selectedEnemyDot;
        [SerializeField] private Transform player;
        private readonly List<AimCandidate> candidates = new List<AimCandidate>();
        private RoomEnemy[] orderedEnemies;
        private EnemyNote[] enemyNotes;
        private RoomChart chart;
        private RoomBinding[] path;
        private int[] previousRoomOccurrences;
        private RoomRun run;
        private RoomAim aim;
        private RoomFeedback feedback;
        private double displayedTime;
        private bool configured;
        private bool[] deathVisible, deathFrames;

        public void Suspend()
        {
            configured = false;
            if (selectedEnemyDot != null) selectedEnemyDot.gameObject.SetActive(false);
        }

        private Dictionary<string, RoomEnemy> EnemyLookup()
        {
            var lookup = new Dictionary<string, RoomEnemy>();
            foreach (var enemy in enemies)
                if (enemy == null || string.IsNullOrWhiteSpace(enemy.Id) || !lookup.TryAdd(enemy.Id, enemy))
                    throw new InvalidOperationException("Missing or duplicate scene enemy ID.");
            return lookup;
        }

        public void ValidateConfiguration(RoomChart chart, RoomBinding[] path, RoomRun run)
        {
            if (selectedEnemyDot == null || player == null || enemies == null)
                throw new InvalidOperationException("Missing combat scene references.");
            var notes = chart.enemies ?? Array.Empty<EnemyNote>();
            var keys = new HashSet<(StageTargetRole, string)>();
            foreach (var target in stageTargets ?? Array.Empty<StageActionTarget>())
            {
                if (target == null || string.IsNullOrWhiteSpace(target.Id)
                    || !Enum.IsDefined(typeof(StageTargetRole), target.Role) || !keys.Add((target.Role, target.Id)))
                    throw new InvalidOperationException("Missing or duplicate stage target ID/role.");
                bool exists = target.Role == StageTargetRole.Shot ? Array.Exists(notes, n => n.id == target.Id)
                    : Array.Exists(chart.moves, n => n.hasDoor && n.destinationId == target.Id);
                if (!exists) throw new InvalidOperationException("Stage target has no matching chart action: " + target.Id);
                if (target.gameObject.scene != gameObject.scene)
                    throw new InvalidOperationException("Stage target belongs to another scene: " + target.Id);
                target.ValidateReferences();
            }
            var lookup = EnemyLookup();
            foreach (var pair in lookup)
            {
                if (!Array.Exists(notes, n => n.id == pair.Key))
                    throw new InvalidOperationException("Enemy binding has no chart note: " + pair.Key);
                pair.Value.ValidateReferences();
            }
            for (int i = 0; i < notes.Length; i++)
            {
                if (!chart.LoopMusic && notes[i].time > chart.music.length + chart.MusicDelaySeconds)
                    throw new InvalidOperationException("Enemy window extends past the music.");
                if (FindOverride(StageTargetRole.Shot, notes[i].id) != null) continue;
                if (!lookup.TryGetValue(notes[i].id, out RoomEnemy enemy))
                    throw new InvalidOperationException("Missing enemy or stage target: " + notes[i].id);
                Vector3 expected = RoomEnemy.Position(path[run.EnemyRoom(i)].Center, notes[i].direction, notes[i].placement, chart.aimRadius);
                if (Vector3.Distance(enemy.PlacementPosition, expected) > .01f)
                    throw new InvalidOperationException("Enemy scene position differs from chart: " + notes[i].id);
            }
        }

        public void Configure(RoomChart chart, RoomBinding[] path, RoomRun run, RoomAim aim, RoomFeedback feedback, int[] previousRoomOccurrences)
        {
            configured = false;
            this.chart = chart; this.path = path; this.run = run; this.aim = aim;
            this.feedback = feedback; this.previousRoomOccurrences = previousRoomOccurrences;
            deathVisible = deathFrames = null;
            enemyNotes = chart.enemies ?? Array.Empty<EnemyNote>();
            orderedEnemies = new RoomEnemy[enemyNotes.Length];
            shotOverrides = new StageActionTarget[enemyNotes.Length];
            doorOverrides = new StageActionTarget[chart.moves.Length];
            var lookup = EnemyLookup();
            foreach (var enemy in enemies) enemy.Present(false, 0);
            for (int i = 0; i < enemyNotes.Length; i++)
            {
                EnemyNote note = enemyNotes[i];
                shotOverrides[i] = FindOverride(StageTargetRole.Shot, note.id);
                if (shotOverrides[i] == null)
                {
                    orderedEnemies[i] = lookup[note.id];
                    orderedEnemies[i].Configure(path[run.EnemyRoom(i)].Center, note.direction, chart, note.placement);
                }
            }
            for (int i = 0; i < chart.moves.Length; i++)
            {
                var note = chart.moves[i];
                doorOverrides[i] = FindOverride(StageTargetRole.Breakthrough, note.destinationId);
            }
            selectedEnemyDot.gameObject.SetActive(false);
            configured = true;
        }

        private int Select(Vector2 direction, Vector3 origin, double time)
        {
            candidates.Clear();
            for (int i = 0; i < orderedEnemies.Length; i++)
                if (run.EnemyAvailable(i))
                    AddCandidate(i, TargetPosition(i, time) - origin, enemyNotes[i].time);
            for (int i = run.CompletedMoves; i < chart.moves.Length; i++)
            {
                MoveNote note = chart.moves[i];
                if (note.hasDoor && !run.DoorBroken(i) && time >= (note.customAppearance ? note.doorFrameStartTime : chart.RoomAppearsAt(note))
                    && previousRoomOccurrences[i + 1] < run.CompletedMoves)
                    AddCandidate(orderedEnemies.Length + i, TargetPosition(orderedEnemies.Length + i, time) - origin, note.doorTime);
            }
            return TargetSelection.Select(candidates, direction.x, direction.y, chart.aimHalfAngle);
        }

        private void AddCandidate(int id, Vector3 offset, double hitTime) => candidates.Add(new AimCandidate(id, offset.x, offset.y, hitTime));
        public void ResetStageTargets()
        {
            foreach (var target in stageTargets ?? Array.Empty<StageActionTarget>())
                if (target != null) target.gameObject.SetActive(true);
            for (int i = 0; i < shotOverrides.Length; i++)
                if (shotOverrides[i] != null) shotOverrides[i].ResetTarget(chart, run.EnemyVisualAppearsAt(i), enemyNotes[i].time);
            for (int i = 0; i < doorOverrides.Length; i++)
                if (doorOverrides[i] != null)
                {
                    var note = chart.moves[i];
                    doorOverrides[i].ResetTarget(chart, note.customAppearance ? note.DoorAppearsAt : chart.RoomAppearsAt(note), note.doorTime);
                }
        }
        private StageActionTarget Override(int id) => id < orderedEnemies.Length ? shotOverrides[id] : doorOverrides[id - orderedEnemies.Length];
        private Vector3 TargetPosition(int id, double time)
        {
            var target = Override(id);
            return target != null ? target.PositionAt(time) : id < orderedEnemies.Length ? orderedEnemies[id].TargetAt(time)
                : path[id - orderedEnemies.Length + 1].Door.Target;
        }

        public void SetCorridorEntrances(string roomId, Vector3 center, float height)
        {
            for (int i = 0; i < enemyNotes.Length; i++)
            {
                if (enemyNotes[i].roomId != roomId || orderedEnemies[i] == null) continue;
                var enemy = orderedEnemies[i];
                Vector3 rest = enemy.TargetAt(double.PositiveInfinity);
                float side = rest.y >= center.y ? 1 : -1;
                Vector3 offset = new Vector3(0, center.y + side * (height * .5f + .7f) - rest.y, 0);
                double start = run.EnemyVisualAppearsAt(i);
                double duration = Math.Min(.34, Math.Max(.01, (enemyNotes[i].time - start) * .4));
                enemy.SetEntrance(offset, start, duration);
            }
        }

        public void Shoot(Vector2 pointer, double time, Vector3 origin)
        {
            Vector2 direction = aim.DirectionAt(pointer, origin);
            int target = Select(direction, origin, time);
            aim.Fire(origin, direction, target >= 0 ? TargetPosition(target, time) : (Vector3?)null);
            if (target < 0) { run.MissShot(time); return; }
            bool hit = target < orderedEnemies.Length ? run.ShootEnemy(target, time)
                : run.ShootDoor(target - orderedEnemies.Length, time);
            if (hit) PresentHit(target, time, origin);
        }

        public void PresentDebugAction(RoomActionResult result, Vector3 origin)
        {
            int target;
            if (result.Kind == RoomActionKind.EnemyDefeated)
                target = Array.FindIndex(enemyNotes, note => note.id == result.TargetId);
            else if (result.Kind == RoomActionKind.DoorBroken)
            {
                int door = Array.FindIndex(chart.moves, note => note.destinationId == result.TargetId);
                target = door < 0 ? -1 : orderedEnemies.Length + door;
            }
            else return;
            if (target < 0) return;
            Vector3 position = TargetPosition(target, result.Time);
            aim.Fire(origin, (Vector2)(position - origin).normalized, position);
            PresentHit(target, result.Time, origin);
        }

        private void PresentHit(int target, double time, Vector3 origin)
        {
            if (Override(target) != null)
            {
                Override(target).OnHit(time);
                return;
            }
            if (target < orderedEnemies.Length)
            {
                Vector3 position = TargetPosition(target, time);
                orderedEnemies[target].Defeat(time, position - origin);
                feedback.EnemyDeath(position, position - origin);
            }
            else
            {
                RoomDoor door = path[target - orderedEnemies.Length + 1].Door;
                feedback.DoorBreak(door.Target, door.Target - origin, door.FragmentColor);
            }
        }

        public void Present(double time) => Present(time, run.PreviewShots());

        public void Present(double time, ShotPreview preview)
        {
            displayedTime = time;
            for (int i = 0; i < orderedEnemies.Length; i++)
            {
                int priority = preview.Priority(enemyNotes[i].time);
                bool visible = deathVisible != null ? deathVisible[i] : EnemyVisible(i, time);
                double appearedAt = run.EnemyVisualAppearsAt(i);
                double duration = enemyNotes[i].time - appearedAt;
                float progress = duration > 0 ? Mathf.Clamp01((float)((time - appearedAt) / duration)) : 1f;
                double frameStart = enemyNotes[i].customAppearance ? enemyNotes[i].frameStartTime : appearedAt;
                bool showFrame = deathFrames != null ? deathFrames[i] : time >= frameStart;
                if (shotOverrides[i] != null)
                {
                    shotOverrides[i].Present(visible, showFrame, time, priority);
                    continue;
                }
                orderedEnemies[i].Present(visible, progress, time, enemyNotes[i].time,
                    showFrame, priority,
                    deathVisible != null ? deathVisible[i] : DefeatedEnemyVisible(i, time));
                orderedEnemies[i].AimAt(player.position);
            }
            for (int i = 0; i < doorOverrides.Length; i++)
            {
                if (doorOverrides[i] == null) continue;
                var note = chart.moves[i];
                bool visible = run.Phase != RunPhase.Ready && i >= run.CompletedMoves && !run.DoorBroken(i)
                    && time >= (note.customAppearance ? note.DoorAppearsAt : chart.RoomAppearsAt(note))
                    && previousRoomOccurrences[i + 1] < run.CompletedMoves;
                int priority = preview.Priority(note.doorTime);
                doorOverrides[i].Present(visible, run.Phase != RunPhase.Dead, time, priority);
            }
            if (!run.IsActive) selectedEnemyDot.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!configured || run == null || !run.IsActive || Mouse.current == null)
            {
                if (selectedEnemyDot != null) selectedEnemyDot.gameObject.SetActive(false);
                return;
            }
            int target = Select(aim.DirectionAt(Mouse.current.position.ReadValue(), player.position), player.position, displayedTime);
            bool show = target >= 0 && target < orderedEnemies.Length;
            selectedEnemyDot.gameObject.SetActive(show);
            if (show) selectedEnemyDot.position = TargetPosition(target, displayedTime);
        }

        public bool HasVisibleEnemies(int room, double time)
        {
            for (int i = 0; i < orderedEnemies.Length; i++)
                if (run.EnemyRoom(i) == room && EnemyVisible(i, time)) return true;
            return false;
        }

        public RoomEnemy FailureEnemy()
        {
            int index = run.FailedEnemy;
            return index >= 0 && index < orderedEnemies.Length ? orderedEnemies[index] : null;
        }

        public void FreezeAtDeath(double time)
        {
            int failed = run.FailedEnemy;
            if (failed >= 0 && shotOverrides[failed] != null) shotOverrides[failed].OnFailure(time);
            else if ((run.Failure == FailureReason.MissedDoor || run.Failure == FailureReason.DoorCollision)
                && run.CompletedMoves < doorOverrides.Length && doorOverrides[run.CompletedMoves] != null)
                doorOverrides[run.CompletedMoves].OnFailure(time);
            deathVisible = new bool[orderedEnemies.Length]; deathFrames = new bool[orderedEnemies.Length];
            for (int i = 0; i < orderedEnemies.Length; i++)
            {
                deathVisible[i] = (EnemyVisible(i, time) || DefeatedEnemyVisible(i, time))
                    && (run.Death != DeathPresentation.Departure || run.EnemyRoom(i) == run.CompletedMoves);
            }
        }

        private bool EnemyVisible(int index, double time)
            => run.EnemyVisible(index, time) && previousRoomOccurrences[run.EnemyRoom(index)] < run.CompletedMoves;

        private bool DefeatedEnemyVisible(int index, double time)
        {
            int room = run.EnemyRoom(index);
            return run.EnemyDefeated(index) && run.RoomVisible(room, time, previousRoomOccurrences[room]);
        }
    }
}
