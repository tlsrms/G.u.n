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
        [SerializeField, Range(0f, 2f)] private float effectStrength = 1f;
        [SerializeField, Range(0f, 2f)] private float bloodStrength = 1f;
        private float effectTime;
        private sealed class Particle
        {
            public SpriteRenderer renderer;
            public Vector3 origin, velocity;
            public Vector2 size;
            public Color tint;
            public float start, life, drag;
            public bool shrink;
        }
        private Particle[] particles;
        private int nextParticle;
        private Texture2D glowTexture;
        private Sprite glowSprite;
        public Sprite GlowSprite => glowSprite;

        public SpriteRenderer CreateVisual(Transform parent, string label, Color tint, bool glow = false)
        {
            if (glow && glowSprite == null)
            {
                glowTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                glowTexture.name = "Rhythm effect glow";
                glowTexture.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color[32 * 32];
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                {
                    float radius = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
                    float alpha = Mathf.Clamp01(1 - radius);
                    pixels[y * 32 + x] = new Color(1, 1, 1, alpha * alpha);
                }
                glowTexture.SetPixels(pixels); glowTexture.Apply();
                glowSprite = Sprite.Create(glowTexture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 32);
            }
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = glow ? glowSprite : edges[0].sprite;
            renderer.color = tint;
            renderer.sortingLayerID = edges[0].sortingLayerID;
            renderer.sortingOrder = 30;
            return renderer;
        }

        public static void Size(SpriteRenderer renderer, Vector2 size)
        {
            Vector3 bounds = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(size.x / bounds.x, size.y / bounds.y, 1);
        }

        public void PrepareEffects()
        {
            if (particles == null)
            {
                particles = new Particle[192];
                for (int i = 0; i < particles.Length; i++)
                {
                    var renderer = CreateVisual(transform, "Effect particle " + i, Color.clear, true);
                    renderer.gameObject.SetActive(false);
                    particles[i] = new Particle { renderer = renderer };
                }
            }
        }

        private void Emit(Vector3 position, Vector3 velocity, Color tint, Vector2 size, float life, float drag = 5, Sprite sprite = null, int order = 30, Quaternion? rotation = null, bool shrink = true)
        {
            if (effectStrength <= 0) return;
            PrepareEffects();
            Particle p = particles[nextParticle++ % particles.Length];
            tint = RoomPalette.Tint(tint);
            p.origin = position; p.velocity = velocity; p.tint = tint;
            p.size = size * effectStrength; p.start = effectTime; p.life = life; p.drag = drag;
            p.shrink = shrink;
            p.renderer.sprite = sprite != null ? sprite : glowSprite;
            p.renderer.sortingOrder = order;
            p.renderer.transform.position = position;
            p.renderer.transform.rotation = rotation ?? Quaternion.Euler(0, 0, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
            p.renderer.color = tint; Size(p.renderer, p.size);
            p.renderer.gameObject.SetActive(true);
        }

        public void EnemyDeath(Vector3 position, Vector3 direction)
        {
            direction = direction.normalized;
            Emit(position, Vector3.zero, Color.white, Vector2.one * .9f, .09f);
            for (int i = 0; i < 22; i++)
            {
                Vector3 velocity = Quaternion.Euler(0, 0, UnityEngine.Random.Range(-38f, 38f)) * direction * UnityEngine.Random.Range(3f, 9f);
                if (bloodStrength > 0)
                    Emit(position, velocity, new Color(.85f, .025f, .09f), new Vector2(.43f, .19f) * bloodStrength, UnityEngine.Random.Range(.2f, .48f), 3);
            }
        }

        public void ArrivalDust(Vector3 position, Vector3 direction)
        {
            direction = direction.normalized;
            if (direction.sqrMagnitude < .001f) return;
            Vector3 side = new Vector3(-direction.y, direction.x, 0);
            // Reuse the existing effect pool; one short, understated burst per arrival.
            for (int i = 0; i < 10; i++)
            {
                Vector3 origin = position + direction * .38f + side * UnityEngine.Random.Range(-.3f, .3f);
                Vector3 velocity = Quaternion.Euler(0, 0, UnityEngine.Random.Range(-32f, 32f))
                    * direction * UnityEngine.Random.Range(2f, 3.8f);
                Emit(origin, velocity, new Color(.8f, .8f, .8f, .7f),
                    Vector2.one * UnityEngine.Random.Range(.3f, .5f), UnityEngine.Random.Range(.28f, .38f), 6, null, 9);
            }
        }

        public void EnemyExecutionShot(Vector3 muzzle, Vector3 target)
        {
            Vector3 delta = target - muzzle;
            Quaternion rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            Emit((muzzle + target) * .5f, Vector3.zero, new Color(1, .92f, .8f, 1),
                new Vector2(delta.magnitude / Mathf.Max(effectStrength, .001f), .025f), .1f, 0, edges[0].sprite, 2102, rotation, false);
            Emit(muzzle, Vector3.zero, Color.white, Vector2.one * .3f, .09f, 0, null, 2103);
        }

        public void DeathSpark(Vector3 position)
            => Emit(position, Vector3.zero, Color.white, Vector2.one * 1.3f, .14f, 5, null, 2101);

        public void DeathBlood(Vector3 position, Vector3 direction)
        {
            for (int i = 0; i < 28; i++)
                Emit(position, Quaternion.Euler(0, 0, UnityEngine.Random.Range(-30f, 30f)) * direction * UnityEngine.Random.Range(3f, 10f),
                    new Color(.9f, .02f, .04f), new Vector2(.45f, .18f) * bloodStrength, .5f, 3, null, 2101);
        }

        public void Shatter(Vector3 position, Sprite sprite, Vector3 direction)
        {
            for (int i = 0; i < 32; i++)
                Emit(position, Quaternion.Euler(0, 0, UnityEngine.Random.Range(-160f, 160f)) * -direction * UnityEngine.Random.Range(2f, 7f),
                    new Color(.35f, 1, .8f), Vector2.one * UnityEngine.Random.Range(.04f, .12f), .65f, 4, sprite);
        }

        public void DoorBreak(Vector3 position, Vector3 direction, Color tint)
        {
            direction = direction.normalized;
            tint.a = 1;
            for (int i = 0; i < 26; i++)
            {
                Vector3 velocity = Quaternion.Euler(0, 0, UnityEngine.Random.Range(-75f, 75f))
                    * direction * UnityEngine.Random.Range(3f, 9f);
                float size = UnityEngine.Random.Range(.8f, 1.4f);
                Emit(position, velocity, tint, new Vector2(.43f, .19f) * size, UnityEngine.Random.Range(.22f, .48f), 3);
            }
        }

        public void ValidateReferences()
        {
            if (flash == null || circle == null || edges == null || edges.Length != 4)
                throw new InvalidOperationException("Feedback needs its authored flash and four edges.");
            foreach (SpriteRenderer edge in edges)
                if (edge == null) throw new InvalidOperationException("Missing feedback edge.");
        }

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

        public void ResetFeedback()
        {
            flash.gameObject.SetActive(false); duration = 0;
            effectTime = 0;
            if (particles != null) foreach (var p in particles) { p.life = 0; p.renderer.gameObject.SetActive(false); }
        }

        private void Update()
        {
            effectTime += Time.unscaledDeltaTime;
            if (particles != null) foreach (var p in particles)
            {
                if (p.life <= 0) continue;
                float elapsed = effectTime - p.start, t = elapsed / p.life;
                if (t >= 1) { p.life = 0; p.renderer.gameObject.SetActive(false); continue; }
                p.renderer.transform.position = p.origin + p.velocity * (p.drag > 0 ? (1 - Mathf.Exp(-p.drag * elapsed)) / p.drag : elapsed);
                Color tint = p.tint; tint.a *= (1 - t) * (1 - t); p.renderer.color = tint;
                Size(p.renderer, p.size * (p.shrink ? Mathf.Lerp(1, .35f, t) : 1));
            }
            if (duration <= 0) return;
            float progress = (Time.unscaledTime - startedAt) / duration;
            if (progress >= 1) { flash.gameObject.SetActive(false); duration = 0; return; }
            Render(progress);
        }

        private void OnDestroy()
        {
            if (glowSprite != null) Destroy(glowSprite);
            if (glowTexture != null) Destroy(glowTexture);
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
