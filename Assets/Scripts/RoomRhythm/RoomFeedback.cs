using System;
using UnityEngine;

namespace Gun.RoomRhythm
{
    public sealed class RoomFeedback : MonoBehaviour
    {
        [SerializeField] private Transform flash;
        [SerializeField] private SpriteRenderer[] edges;
        [SerializeField] private SpriteRenderer circle;
        private float startedAt;
        private float duration;
        private Color color;

        public void ValidateReferences()
        {
            if (flash == null || circle == null || edges == null || edges.Length != 4)
                throw new InvalidOperationException("Feedback needs its authored flash and four edges.");
            foreach (SpriteRenderer edge in edges)
                if (edge == null) throw new InvalidOperationException("Missing feedback edge.");
        }

        public void Hit(Vector3 position, TimingGrade grade, bool isEnemy) => Play(position, JudgmentPresentation.Tint(grade), 0.18f, isEnemy);

        public void Outcome(Vector3 position, bool success) => Play(position,
            success ? new Color(0.3f, 1f, 0.8f) : new Color(1f, 0.2f, 0.3f), 0.4f, true);

        private void Play(Vector3 position, Color tint, float seconds, bool round)
        {
            circle.gameObject.SetActive(round);
            foreach (SpriteRenderer edge in edges) edge.gameObject.SetActive(!round);
            flash.position = position;
            color = tint;
            duration = seconds;
            startedAt = Time.unscaledTime;
            flash.gameObject.SetActive(true);
            Render(0);
        }

        public void ResetFeedback() { flash.gameObject.SetActive(false); duration = 0; }

        private void Update()
        {
            if (duration <= 0) return;
            float progress = (Time.unscaledTime - startedAt) / duration;
            if (progress >= 1) { ResetFeedback(); return; }
            Render(progress);
        }

        private void Render(float progress)
        {
            flash.localScale = Vector3.one * Mathf.Lerp(1f, 2.4f, progress);
            Color tint = color; tint.a = 1f - progress;
            foreach (SpriteRenderer edge in edges) edge.color = tint;
            circle.color = tint;
        }
    }
}
