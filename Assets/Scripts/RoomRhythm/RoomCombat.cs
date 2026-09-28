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

        public void ValidateConfiguration(RoomChart chart, RoomBinding[] path, RoomRun run)
        {
            if (selectedEnemyDot == null || player == null || enemies == null)
                throw new InvalidOperationException("Missing combat scene references.");
            var notes = chart.enemies ?? Array.Empty<EnemyNote>();
            var lookup = new Dictionary<string, RoomEnemy>();
            foreach (RoomEnemy enemy in enemies)
            {
                if (enemy == null || string.IsNullOrWhiteSpace(enemy.Id) || !lookup.TryAdd(enemy.Id, enemy))
                    throw new InvalidOperationException("Missing or duplicate scene enemy ID.");
                enemy.ValidateReferences();
            }
            if (lookup.Count != notes.Length) throw new InvalidOperationException("Enemy bindings do not match chart.");
            for (int i = 0; i < notes.Length; i++)
            {
                if (!lookup.TryGetValue(notes[i].id, out RoomEnemy enemy))
                    throw new InvalidOperationException("Missing enemy binding: " + notes[i].id);
                if (!chart.LoopMusic && notes[i].time > chart.music.length + chart.MusicDelaySeconds)
                    throw new InvalidOperationException("Enemy window extends past the music.");
                float angle = (90 - 45 * (int)notes[i].direction) * Mathf.Deg2Rad;
                Vector3 expected = path[run.EnemyRoom(i)].Center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * chart.aimRadius;
                if (Vector3.Distance(enemy.Target, expected) > 0.01f)
                    throw new InvalidOperationException("Enemy scene position differs from chart: " + notes[i].id);
            }
        }

        public void Configure(RoomChart chart, RoomBinding[] path, RoomRun run, RoomAim aim, RoomFeedback feedback, int[] previousRoomOccurrences)
        {
            configured = false;
            this.chart = chart; this.path = path; this.run = run; this.aim = aim;
            this.feedback = feedback;
            this.previousRoomOccurrences = previousRoomOccurrences;
            deathVisible = deathFrames = null;
            if (selectedEnemyDot == null || player == null || enemies == null)
                throw new InvalidOperationException("Missing combat scene references.");
            enemyNotes = chart.enemies ?? Array.Empty<EnemyNote>();
            orderedEnemies = new RoomEnemy[enemyNotes.Length];
            var lookup = new Dictionary<string, RoomEnemy>();
            foreach (RoomEnemy enemy in enemies)
            {
                if (enemy == null || string.IsNullOrWhiteSpace(enemy.Id) || !lookup.TryAdd(enemy.Id, enemy))
                    throw new InvalidOperationException("Missing or duplicate scene enemy ID.");
                enemy.Present(false, 0);
            }
            if (lookup.Count != enemyNotes.Length) throw new InvalidOperationException("Enemy bindings do not match chart.");
            for (int i = 0; i < enemyNotes.Length; i++)
            {
                EnemyNote note = enemyNotes[i];
                if (!chart.LoopMusic && note.time > chart.music.length + chart.MusicDelaySeconds)
                    throw new InvalidOperationException("Enemy window extends past the music.");
                orderedEnemies[i] = lookup[note.id];
                orderedEnemies[i].Configure(path[run.EnemyRoom(i)].Center, note.direction, chart);
            }
            selectedEnemyDot.gameObject.SetActive(false);
            configured = true;
        }

        private int Select(Vector2 direction, Vector3 origin, double time)
        {
            candidates.Clear();
            for (int i = 0; i < orderedEnemies.Length; i++)
                if (run.EnemyAvailable(i)) AddCandidate(i, orderedEnemies[i].Target - origin, enemyNotes[i].time);
            for (int i = run.CompletedMoves; i < chart.moves.Length; i++)
            {
                MoveNote note = chart.moves[i];
                if (note.hasDoor && !run.DoorBroken(i) && time >= (note.customAppearance ? note.doorFrameStartTime : chart.RoomAppearsAt(note))
                    && previousRoomOccurrences[i + 1] < run.CompletedMoves)
                    AddCandidate(orderedEnemies.Length + i, path[i + 1].Door.Target - origin, note.doorTime);
            }
            return TargetSelection.Select(candidates, direction.x, direction.y, chart.aimHalfAngle);
        }

        private void AddCandidate(int id, Vector3 offset, double hitTime) => candidates.Add(new AimCandidate(id, offset.x, offset.y, hitTime));
        private Vector3 TargetPosition(int id) => id < orderedEnemies.Length ? orderedEnemies[id].Target
            : path[id - orderedEnemies.Length + 1].Door.Target;

        public void Shoot(Vector2 pointer, double time, Vector3 origin)
        {
            Vector2 direction = aim.DirectionAt(pointer, origin);
            int target = Select(direction, origin, time);
            aim.Fire(origin, direction, target >= 0 ? TargetPosition(target) : (Vector3?)null);
            if (target < 0) { run.MissShot(time); return; }
            bool hit = target < orderedEnemies.Length ? run.ShootEnemy(target, time)
                : run.ShootDoor(target - orderedEnemies.Length, time);
            if (hit && target < orderedEnemies.Length)
                feedback.EnemyDeath(TargetPosition(target), TargetPosition(target) - origin);
            else if (hit)
            {
                RoomDoor door = path[target - orderedEnemies.Length + 1].Door;
                feedback.DoorBreak(door.Target, door.Target - origin, door.FragmentColor);
            }
        }

        public void Present(double time)
        {
            displayedTime = time;
            int nextEnemy = run.NextPendingEnemyIndex();
            for (int i = 0; i < orderedEnemies.Length; i++)
            {
                bool visible = deathVisible != null ? deathVisible[i] : EnemyVisible(i, time);
                double appearedAt = run.EnemyVisualAppearsAt(i);
                double duration = enemyNotes[i].time - appearedAt;
                float progress = duration > 0 ? Mathf.Clamp01((float)((time - appearedAt) / duration)) : 1f;
                double frameStart = enemyNotes[i].customAppearance ? enemyNotes[i].frameStartTime : appearedAt;
                float brightness = run.EnemyRoomEntered(i) ? 1 : RoomChart.UpcomingEnemyBrightness;
                orderedEnemies[i].Present(visible, progress, time, enemyNotes[i].time,
                    deathFrames != null ? deathFrames[i] : time >= frameStart, brightness, i == nextEnemy);
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
            if (show) selectedEnemyDot.position = orderedEnemies[target].Target;
        }

        public bool HasLivingEnemies(int room)
        {
            for (int i = 0; i < orderedEnemies.Length; i++)
                if (run.EnemyRoom(i) == room && !run.EnemyDefeated(i)) return true;
            return false;
        }

        public SpriteRenderer FailureEnemy()
        {
            int index = run.FailedEnemy;
            return index >= 0 && index < orderedEnemies.Length ? orderedEnemies[index].Body : null;
        }

        public void FreezeAtDeath(double time)
        {
            deathVisible = new bool[orderedEnemies.Length]; deathFrames = new bool[orderedEnemies.Length];
            for (int i = 0; i < orderedEnemies.Length; i++)
            {
                deathVisible[i] = EnemyVisible(i, time)
                    && (run.Death != DeathPresentation.Departure || run.EnemyRoom(i) == run.CompletedMoves);
            }
        }

        private bool EnemyVisible(int index, double time)
            => run.EnemyVisible(index, time) && previousRoomOccurrences[run.EnemyRoom(index)] < run.CompletedMoves;
    }
}
