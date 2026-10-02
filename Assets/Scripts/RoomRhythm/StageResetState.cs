using System.Collections.Generic;
using UnityEngine;

namespace Gun.RoomRhythm
{
    // Opt-in snapshots for authored boss/prop hierarchies; no runtime object creation.
    public sealed class StageResetState : MonoBehaviour
    {
        [SerializeField] private Transform[] roots;
        private readonly Dictionary<Transform, Pose> poses = new Dictionary<Transform, Pose>();
        private readonly Dictionary<SpriteRenderer, Color> colors = new Dictionary<SpriteRenderer, Color>();
        private readonly Dictionary<Renderer, bool> renderers = new Dictionary<Renderer, bool>();
        private struct Pose
        {
            public Transform parent;
            public Vector3 position, scale;
            public Quaternion rotation;
            public bool active;
        }
        public void Capture()
        {
            poses.Clear(); colors.Clear(); renderers.Clear();
            foreach (var root in roots ?? System.Array.Empty<Transform>())
            {
                if (root == null) continue;
                foreach (var node in root.GetComponentsInChildren<Transform>(true))
                    poses[node] = new Pose { parent = node.parent, position = node.localPosition,
                        rotation = node.localRotation, scale = node.localScale, active = node.gameObject.activeSelf };
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderers[renderer] = renderer.enabled;
                foreach (var sprite in root.GetComponentsInChildren<SpriteRenderer>(true)) colors[sprite] = sprite.color;
            }
        }
        public void Restore()
        {
            foreach (var pair in poses)
            {
                if (pair.Key == null) continue;
                pair.Key.SetParent(pair.Value.parent, false);
                pair.Key.localPosition = pair.Value.position;
                pair.Key.localRotation = pair.Value.rotation;
                pair.Key.localScale = pair.Value.scale;
            }
            foreach (var pair in renderers) if (pair.Key != null) pair.Key.enabled = pair.Value;
            foreach (var pair in colors) if (pair.Key != null) pair.Key.color = pair.Value;
            foreach (var pair in poses) if (pair.Key != null) pair.Key.gameObject.SetActive(pair.Value.active);
        }
    }
}
