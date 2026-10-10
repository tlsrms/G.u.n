using UnityEngine;
using UnityEngine.UI;

namespace Gun.RoomRhythm
{
    // Mesh-only record artwork: the parent record owns rotation; sector graphics share that same transform.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RecordDialGraphic : MaskableGraphic
    {
        public enum Artwork { Vinyl, Platter, Sector }
        private Artwork artwork;
        private float startAngle, sweep;

        public void Configure(Artwork kind, float start = 90, float degrees = 360)
        { artwork = kind; startAngle = start; sweep = degrees; raycastTarget = false; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;
            if (artwork == Artwork.Sector)
            {
                Band(mesh, radius * .34f, radius * .95f, startAngle - 1, sweep - 2, color);
                Band(mesh, radius * .965f, radius * .972f, startAngle - 1, sweep - 2, color * new Color(1, 1, 1, 2));
                return;
            }
            if (artwork == Artwork.Platter)
            {
                Band(mesh, 0, radius, 0, 360, new Color(.17f, .17f, .17f));
                Band(mesh, radius * .982f, radius, 0, 360, new Color(.72f, .72f, .72f));
                for (int i = 0; i < 96; i++)
                    Band(mesh, radius * .948f, radius * .976f, i * 3.75f, 1.2f, new Color(.76f, .76f, .76f));
                return;
            }
            Band(mesh, 0, radius, 0, 360, new Color(.043f, .043f, .043f));
            for (int i = 0; i < 32; i++)
            {
                float r = radius * Mathf.Lerp(.33f, .99f, i / 31f);
                Band(mesh, r - .55f, r, 0, 360, new Color(.18f, .18f, .18f, .55f));
            }
            // Asymmetric highlights make playback rotation readable after the sector UI fades out.
            for (int i = 0; i < 9; i++)
            {
                float r = radius * (.42f + i * .061f);
                Band(mesh, r - .7f, r + .7f, 34 + i * 2, 34, new Color(.35f, .35f, .35f, .24f));
                Band(mesh, r - .7f, r, 213, 21, new Color(.31f, .31f, .31f, .2f));
            }
        }

        private static void Band(VertexHelper mesh, float inner, float outer, float start, float degrees, Color tint)
        {
            int segments = Mathf.Max(2, Mathf.CeilToInt(degrees / 3));
            int offset = mesh.currentVertCount;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (start - degrees * i / segments) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(direction * inner, tint, Vector2.zero);
                mesh.AddVert(direction * outer, tint, Vector2.zero);
                if (i == 0) continue;
                int v = offset + i * 2;
                mesh.AddTriangle(v - 2, v - 1, v); mesh.AddTriangle(v - 1, v + 1, v);
            }
        }
    }
}
