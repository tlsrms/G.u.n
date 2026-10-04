using UnityEngine;

namespace Gun.RoomRhythm
{
    // Authored furniture/window; reusable cosmetic particles evaluated on the song clock.
    public sealed class MafiaOfficeSet : MonoBehaviour
    {
        [SerializeField] private Transform desk, chair, window;
        [SerializeField] private SpriteRenderer glass;
        [SerializeField] private AudioClip rifleSound, glassSound;
        private AudioSource audioSource;
        private RoomFeedback feedback;
        private readonly SpriteRenderer[] bullets = new SpriteRenderer[3], trails = new SpriteRenderer[3],
            shells = new SpriteRenderer[3], smoke = new SpriteRenderer[6];
        private readonly Vector3[] starts = new Vector3[3], targets = new Vector3[3];
        private readonly bool[] fired = new bool[3];
        private SpriteRenderer muzzleFlash;
        private bool broken;
        public void Prepare(RoomFeedback effects, Vector3 bossPosition, Vector3 windowPosition)
        {
            feedback = effects;
            desk.position = bossPosition + Vector3.left * .85f;
            chair.position = bossPosition + Vector3.right * .22f;
            window.position = windowPosition;
            if (muzzleFlash == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false; audioSource.spatialBlend = 0;
                SpriteRenderer Make(string name, int order, bool glow = false)
                {
                    var sprite = feedback.CreateVisual(transform, name, Color.white, glow);
                    sprite.sortingOrder = order;
                    return sprite;
                }
                muzzleFlash = Make("AK muzzle flash", 45, true);
                for (int i = 0; i < 3; i++)
                {
                    bullets[i] = Make("Intro bullet " + i, 43, true);
                    trails[i] = Make("Intro trail " + i, 42);
                    shells[i] = Make("AK casing " + i, 32);
                }
                for (int i = 0; i < smoke.Length; i++) smoke[i] = Make("Cigarette smoke " + i, 35, true);
            }
            ResetSet();
        }
        public void ResetSet()
        {
            broken = false; glass.enabled = true;
            for (int i = 0; i < fired.Length; i++) fired[i] = false;
            if (audioSource != null) audioSource.Stop();
            HideEffects(); gameObject.SetActive(false);
        }
        public void HideEffects()
        {
            if (muzzleFlash == null) return;
            muzzleFlash.enabled = false;
            foreach (var array in new[] { bullets, trails, shells, smoke })
                foreach (var sprite in array) sprite.enabled = false;
        }
        public void Fire(int index, Vector3 from, Vector3 target)
        {
            fired[index] = true; starts[index] = from;
            targets[index] = target + Vector3.up * ((index - 1) * .12f);
            if (rifleSound != null) audioSource.PlayOneShot(rifleSound, .42f);
        }
        public void BreakWindow()
        {
            if (broken) return;
            broken = true; glass.enabled = false;
            feedback.Shatter(window.position, glass.sprite, Vector3.up);
            feedback.DeathSpark(window.position);
            if (glassSound != null) audioSource.PlayOneShot(glassSound, .48f);
        }
        public void Present(double time, MafiaIntroTiming timing, double escape, Vector3 smokeOrigin, bool smoking)
        {
            muzzleFlash.enabled = false;
            for (int i = 0; i < 3; i++)
            {
                float age = (float)(time - timing.ShotTime(i));
                float progress = (float)timing.BulletProgress(time, i, escape);
                bool show = fired[i] && progress < 1.55f;
                bullets[i].enabled = trails[i].enabled = show;
                if (show)
                {
                    Vector3 direction = targets[i] - starts[i];
                    Vector3 at = starts[i] + direction * progress;
                    Quaternion rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                    bullets[i].transform.SetPositionAndRotation(at, rotation);
                    trails[i].transform.SetPositionAndRotation(at - direction.normalized * .22f, rotation);
                    RoomFeedback.Size(bullets[i], new Vector2(.22f, .07f));
                    RoomFeedback.Size(trails[i], new Vector2(.45f, .018f));
                    bullets[i].color = new Color(1, .94f, .7f);
                    trails[i].color = new Color(1, .85f, .5f, .55f);
                }
                shells[i].enabled = fired[i] && age >= 0 && age < .75f;
                if (shells[i].enabled)
                {
                    shells[i].transform.SetPositionAndRotation(starts[i] + new Vector3(age * .45f, -.1f - age * 1.3f, 0),
                        Quaternion.Euler(0, 0, age * 850));
                    RoomFeedback.Size(shells[i], new Vector2(.08f, .035f));
                    shells[i].color = new Color(.82f, .65f, .35f, 1 - age / .75f);
                }
                if (fired[i] && age >= 0 && age < .075f)
                {
                    muzzleFlash.enabled = true; muzzleFlash.transform.position = starts[i];
                    RoomFeedback.Size(muzzleFlash, Vector2.one * Mathf.Lerp(.7f, .08f, age / .075f));
                }
            }
            for (int i = 0; i < smoke.Length; i++)
            {
                smoke[i].enabled = smoking;
                if (!smoking) continue;
                float age = Mathf.Repeat((float)(time - timing.Start) + i * .31f, 1.8f);
                smoke[i].transform.position = smokeOrigin + new Vector3(-age * .14f + Mathf.Sin(age * 3 + i) * .04f, age * .22f, 0);
                RoomFeedback.Size(smoke[i], Vector2.one * (.07f + age * .13f));
                smoke[i].color = new Color(.8f, .8f, .8f, Mathf.Sin(age / 1.8f * Mathf.PI) * .26f);
            }
            if (!broken) glass.color = new Color(.65f, .9f, 1, time >= timing.Window - timing.BeatSeconds * 2 ? .65f : .28f);
        }
    }
}
