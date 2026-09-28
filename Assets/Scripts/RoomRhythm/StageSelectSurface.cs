using UnityEngine;
using UnityEngine.UI;

namespace Gun.RoomRhythm
{
    // A texture-free rounded face for authored TV parts. Like Image, this only renders its own RectTransform.
    [AddComponentMenu("UI/Stage Select Surface")]
    public sealed class StageSelectSurface : MaskableGraphic
    {
        [SerializeField, Min(0)] private float cornerRadius;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float radius = Mathf.Clamp(cornerRadius, 0, Mathf.Min(rect.width, rect.height) * .5f);
            const int segments = 12;
            mesh.AddVert(rect.center, color, Vector2.zero);
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 center = new Vector2(corner < 2 ? rect.xMax - radius : rect.xMin + radius,
                    corner == 0 || corner == 3 ? rect.yMax - radius : rect.yMin + radius);
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (90 - corner * 90 - i * 90f / segments) * Mathf.Deg2Rad;
                    Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    mesh.AddVert(point, color, Vector2.zero);
                }
            }
            int count = 4 * (segments + 1);
            for (int i = 1; i <= count; i++) mesh.AddTriangle(0, i, i == count ? 1 : i + 1);
        }
    }
}
