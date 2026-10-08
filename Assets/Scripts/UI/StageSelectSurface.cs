using UnityEngine;
using UnityEngine.UI;

namespace Gun.RoomRhythm
{
    // A rounded face or outline for authored UI parts. This only renders its own RectTransform.
    [AddComponentMenu("UI/Stage Select Surface")]
    public sealed class StageSelectSurface : MaskableGraphic
    {
        [SerializeField, Min(0)] private float cornerRadius;
        [SerializeField, Min(0)] private float outlineWidth;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float radius = Mathf.Clamp(cornerRadius, 0, Mathf.Min(rect.width, rect.height) * .5f);
            const int segments = 12;
            bool outline = outlineWidth > 0;
            float width = Mathf.Min(outlineWidth, Mathf.Min(rect.width, rect.height) * .5f);
            if (!outline) mesh.AddVert(rect.center, color, Vector2.zero);
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 center = new Vector2(corner < 2 ? rect.xMax - radius : rect.xMin + radius,
                    corner == 0 || corner == 3 ? rect.yMax - radius : rect.yMin + radius);
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (90 - corner * 90 - i * 90f / segments) * Mathf.Deg2Rad;
                    Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    mesh.AddVert(point, color, Vector2.zero);
                    if (outline)
                    {
                        Vector2 innerCenter = new Vector2(corner < 2 ? rect.xMax - Mathf.Max(radius, width) : rect.xMin + Mathf.Max(radius, width),
                            corner == 0 || corner == 3 ? rect.yMax - Mathf.Max(radius, width) : rect.yMin + Mathf.Max(radius, width));
                        mesh.AddVert(innerCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Max(0, radius - width), color, Vector2.zero);
                    }
                }
            }
            int count = 4 * (segments + 1);
            if (outline)
            {
                for (int i = 0; i < count; i++)
                {
                    int current = i * 2, next = (i + 1) % count * 2;
                    mesh.AddTriangle(current, next, current + 1);
                    mesh.AddTriangle(next, next + 1, current + 1);
                }
            }
            else for (int i = 1; i <= count; i++) mesh.AddTriangle(0, i, i == count ? 1 : i + 1);
        }
    }
}
