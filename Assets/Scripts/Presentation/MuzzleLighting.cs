using UnityEngine;

namespace Gun.RoomRhythm
{
    // A short ground-plane light pulse. Enemy silhouettes occlude a point source
    // above the floor, so shadows stretch away from the actual muzzle position.
    public sealed class MuzzleLighting : MonoBehaviour
    {
        private const int MaxOccluders = 24;
        private const float Range = 4.5f, Duration = .075f;
        private readonly Vector4[] occluders = new Vector4[MaxOccluders];
        private RoomEnemy[] enemies;
        private RoomBinding[] rooms;
        private Mesh mesh;
        private Material material;
        private MeshRenderer lightRenderer;
        private GameObject lightObject;
        private float firedAt = -10;
        private Vector3 source;
        private Vector4 bounds;

        public void Configure()
        {
            enemies = FindObjectsByType<RoomEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            rooms = FindObjectsByType<RoomBinding>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (lightRenderer != null) { Clear(); return; }
            Shader shader = Resources.Load<Shader>("MuzzleGroundLight");
            if (shader == null)
            {
                Debug.LogError("Missing MuzzleGroundLight shader.", this);
                return;
            }
            material = new Material(shader) { name = "Muzzle light (runtime)" };
            mesh = new Mesh { name = "Muzzle ground plane" };
            mesh.vertices = new[] { new Vector3(-1, -1), new Vector3(1, -1), new Vector3(1, 1), new Vector3(-1, 1) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            lightObject = new GameObject("Muzzle ground light");
            lightObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            lightRenderer = lightObject.AddComponent<MeshRenderer>();
            lightRenderer.sharedMaterial = material;
            lightRenderer.sortingOrder = 9; // Above the floor, below character artwork and timing cues.
            lightRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lightRenderer.receiveShadows = false;
            Clear();
        }

        public void Fire(Vector3 muzzle, Vector2 direction, Vector3 playerPosition)
        {
            if (lightRenderer == null) return;
            source = muzzle;
            firedAt = Time.unscaledTime;
            bounds = new Vector4(source.x - Range, source.y - Range, source.x + Range, source.y + Range);
            // Constrain floor illumination to the shooter's room, not the adjacent previews.
            foreach (RoomBinding room in rooms)
            {
                if (room == null) continue;
                Vector2 half = room.Size * .5f;
                Vector3 center = room.Center;
                if (Mathf.Abs(playerPosition.x - center.x) > half.x || Mathf.Abs(playerPosition.y - center.y) > half.y) continue;
                bounds = new Vector4(center.x - half.x, center.y - half.y, center.x + half.x, center.y + half.y);
                break;
            }
            lightObject.transform.SetPositionAndRotation(new Vector3(source.x, source.y, 0), Quaternion.identity);
            lightObject.transform.localScale = Vector3.one * Range;
            material.SetVector("_Source", new Vector4(source.x, source.y, Range, 0));
            material.SetVector("_Forward", new Vector4(direction.x, direction.y, 0, 0));
            material.SetVector("_RoomBounds", bounds);
            lightRenderer.enabled = true;
            Render();
        }

        private void LateUpdate() => Render();

        private void Render()
        {
            if (lightRenderer == null || !lightRenderer.enabled) return;
            float age = Time.unscaledTime - firedAt;
            if (age >= Duration) { Clear(); return; }
            int count = 0;
            foreach (RoomEnemy enemy in enemies)
            {
                if (enemy == null || enemy.Body == null || !enemy.Body.enabled
                    || !enemy.Body.gameObject.activeInHierarchy || enemy.Body.color.a < .05f) continue;
                Bounds body = enemy.Body.bounds;
                Vector3 center = body.center;
                if (center.x < bounds.x || center.y < bounds.y || center.x > bounds.z || center.y > bounds.w) continue;
                if (((Vector2)(center - source)).sqrMagnitude > Range * Range) continue;
                // Torso radius is deliberately narrower than the gun-inclusive sprite bounds.
                float radius = Mathf.Clamp(Mathf.Min(body.extents.x, body.extents.y) * .65f, .08f, .45f);
                occluders[count++] = new Vector4(center.x, center.y, radius, enemy.Body.color.a);
                if (count == MaxOccluders) break;
            }
            material.SetInt("_OccluderCount", count);
            material.SetVectorArray("_Occluders", occluders);
            material.SetFloat("_Pulse", Mathf.Exp(-age * 55) * Mathf.Clamp01((Duration - age) / .02f));
        }

        public void Clear()
        {
            firedAt = -10;
            if (lightRenderer != null) lightRenderer.enabled = false;
        }

        private void OnDisable() => Clear();

        private void OnDestroy()
        {
            if (lightObject != null) Destroy(lightObject);
            if (material != null) Destroy(material);
            if (mesh != null) Destroy(mesh);
        }
    }
}
