using System;
using UnityEngine;

namespace Gun.RoomRhythm
{
    public sealed class RoomRestartTransition : MonoBehaviour
    {
        private enum Phase { Idle, Closing, Covered, Opening }
        private Phase phase;
        private float started;
        private int finishedFrame = -1;
        private Action reset;
        private Texture2D iris;
        public bool BlocksInput => phase != Phase.Idle || Time.frameCount == finishedFrame;

        public void Begin(Action resetAtBlack)
        {
            if (phase != Phase.Idle) return;
            reset = resetAtBlack;
            phase = Phase.Closing;
            started = Time.unscaledTime;
        }

        private void Update()
        {
            float elapsed = Time.unscaledTime - started;
            if (phase == Phase.Closing && elapsed >= .45f)
            {
                phase = Phase.Covered;
                started = Time.unscaledTime;
                Action callback = reset;
                reset = null;
                callback?.Invoke();
            }
            else if (phase == Phase.Covered && elapsed >= .08f)
            {
                phase = Phase.Opening;
                started = Time.unscaledTime;
            }
            else if (phase == Phase.Opening && elapsed >= .55f)
            {
                phase = Phase.Idle;
                finishedFrame = Time.frameCount;
            }
        }

        private void OnGUI()
        {
            if (phase == Phase.Idle || Event.current.type != EventType.Repaint) return;
            if (iris == null) CreateIris();
            float elapsed = Time.unscaledTime - started;
            float openness = phase == Phase.Closing ? 1 - Mathf.SmoothStep(0, 1, elapsed / .45f)
                : phase == Phase.Opening ? Mathf.SmoothStep(0, 1, elapsed / .55f) : 0;
            float width = Screen.width, height = Screen.height;
            float radius = Mathf.Sqrt(width * width + height * height) * .5f * openness;
            int oldDepth = GUI.depth;
            Color oldColor = GUI.color;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.depth = -10000;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.black;
            if (radius < .5f) GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
            else
            {
                float left = width * .5f - radius, top = height * .5f - radius;
                if (top > 0)
                {
                    GUI.DrawTexture(new Rect(0, 0, width, top), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(0, height - top, width, top), Texture2D.whiteTexture);
                }
                if (left > 0)
                {
                    GUI.DrawTexture(new Rect(0, Mathf.Max(0, top), left, Mathf.Min(height, radius * 2)), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(width - left, Mathf.Max(0, top), left, Mathf.Min(height, radius * 2)), Texture2D.whiteTexture);
                }
                GUI.DrawTexture(new Rect(left, top, radius * 2, radius * 2), iris);
            }
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
            GUI.depth = oldDepth;
        }

        private void CreateIris()
        {
            const int size = 512;
            iris = new Texture2D(size, size, TextureFormat.RGBA32, false);
            iris.name = "Restart iris mask";
            iris.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float distance = new Vector2(x + .5f - size * .5f, y + .5f - size * .5f).magnitude;
                    pixels[y * size + x] = new Color(0, 0, 0, Mathf.Clamp01(distance - (size * .5f - 1)));
                }
            iris.SetPixels(pixels);
            iris.Apply(false, true);
        }

        private void OnDisable() { phase = Phase.Idle; reset = null; }
        private void OnDestroy() { if (iris != null) Destroy(iris); }
    }
}
