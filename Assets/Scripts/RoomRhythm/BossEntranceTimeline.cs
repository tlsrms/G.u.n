using System;
using UnityEngine;

namespace Gun.RoomRhythm
{
    [CreateAssetMenu(menuName = "Gun/Boss Entrance Timeline")]
    public sealed class BossEntranceTimeline : ScriptableObject
    {
        [Serializable]
        public sealed class ClipCue
        {
            public AnimationClip clip;
            [Min(0)] public float startBeat;
            [Min(.01f)] public float durationBeats = 8;
        }

        [Min(.01f)] public float durationBeats = 16;
        public AnimationCurve offsetX = AnimationCurve.Constant(0, 16, 0);
        public AnimationCurve offsetY = AnimationCurve.EaseInOut(0, 9, 8, 2.8f);
        public AnimationCurve offsetZ = AnimationCurve.Constant(0, 16, 0);
        public AnimationCurve rotationZ = AnimationCurve.Constant(0, 16, 180);
        public ClipCue[] clips = Array.Empty<ClipCue>();

        public void Validate(double availableBeats)
        {
            if (!(availableBeats > 0) || double.IsInfinity(availableBeats)
                || !(durationBeats > 0) || float.IsInfinity(durationBeats) || durationBeats > availableBeats)
                throw new InvalidOperationException("등장 길이는 0보다 크고 전투 시작 이전에 끝나야 합니다.");
            foreach (var curve in new[] { offsetX, offsetY, offsetZ, rotationZ })
            {
                if (curve == null || curve.length == 0) throw new InvalidOperationException("루트 이동·회전 곡선이 필요합니다.");
                foreach (var key in curve.keys)
                    if (float.IsNaN(key.time) || float.IsInfinity(key.time) || float.IsNaN(key.value) || float.IsInfinity(key.value))
                        throw new InvalidOperationException("곡선 키는 유한한 값이어야 합니다.");
            }
            float end = 0;
            foreach (var cue in clips ?? Array.Empty<ClipCue>())
            {
                if (cue == null || cue.clip == null || !(cue.clip.length > 0) || cue.clip.isHumanMotion || cue.clip.events.Length > 0
                    || !(cue.startBeat >= end) || !(cue.durationBeats > 0)
                    || !(cue.startBeat + cue.durationBeats <= durationBeats))
                    throw new InvalidOperationException("클립은 시간순으로 겹치지 않게 배치하세요. 유효한 비 Humanoid 클립과 등장 범위 내 길이가 필요합니다.");
                end = cue.startBeat + cue.durationBeats;
            }
        }

        // Call after restoring the authored pose. No gameplay callbacks or accumulated clock.
        public void Evaluate(Transform root, Vector3 anchor, double relativeBeat)
        {
            float beat = Mathf.Clamp((float)relativeBeat, 0, durationBeats);
            foreach (var cue in clips ?? Array.Empty<ClipCue>())
            {
                if (cue == null || cue.clip == null || beat < cue.startBeat) continue;
                float t = Mathf.Clamp01((beat - cue.startBeat) / cue.durationBeats);
                cue.clip.SampleAnimation(root.gameObject, cue.clip.length * t);
            }
            // Root belongs to the timeline; clips own child joint transforms only.
            root.position = anchor + new Vector3(offsetX.Evaluate(beat), offsetY.Evaluate(beat), offsetZ.Evaluate(beat));
            root.rotation = Quaternion.Euler(0, 0, rotationZ.Evaluate(beat));
        }
    }

    // A reusable snapshot for deterministic seeking and restoring the authored rig.
    public sealed class BossPoseSnapshot
    {
        private readonly Transform[] nodes;
        private readonly Vector3[] positions, scales;
        private readonly Quaternion[] rotations;
        private readonly bool[] active;
        private readonly SpriteRenderer[] sprites;
        private readonly Color[] colors;
        private readonly bool[] enabled;
        private readonly Animator[] animators;
        private readonly bool[] animatorEnabled;

        public BossPoseSnapshot(Transform root)
        {
            nodes = root.GetComponentsInChildren<Transform>(true);
            positions = new Vector3[nodes.Length]; scales = new Vector3[nodes.Length];
            rotations = new Quaternion[nodes.Length]; active = new bool[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                positions[i] = nodes[i].localPosition; rotations[i] = nodes[i].localRotation;
                scales[i] = nodes[i].localScale; active[i] = nodes[i].gameObject.activeSelf;
            }
            sprites = root.GetComponentsInChildren<SpriteRenderer>(true);
            colors = new Color[sprites.Length]; enabled = new bool[sprites.Length];
            for (int i = 0; i < sprites.Length; i++) { colors[i] = sprites[i].color; enabled[i] = sprites[i].enabled; }
            animators = root.GetComponentsInChildren<Animator>(true);
            animatorEnabled = new bool[animators.Length];
            for (int i = 0; i < animators.Length; i++) animatorEnabled[i] = animators[i].enabled;
        }

        public void Restore(bool forSampling = false)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] == null) continue;
                nodes[i].localPosition = positions[i]; nodes[i].localRotation = rotations[i]; nodes[i].localScale = scales[i];
                if (!forSampling && nodes[i].gameObject.activeSelf != active[i]) nodes[i].gameObject.SetActive(active[i]);
            }
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i] != null) { sprites[i].color = colors[i]; sprites[i].enabled = enabled[i]; }
            for (int i = 0; i < animators.Length; i++)
                if (animators[i] != null) animators[i].enabled = !forSampling && animatorEnabled[i];
        }
    }
}
