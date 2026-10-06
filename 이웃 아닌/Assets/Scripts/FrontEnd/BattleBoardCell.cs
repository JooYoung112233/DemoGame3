using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleBoardCell : MaskableGraphic
    {
        public Vector2 TopLeft, TopRight, BottomRight, BottomLeft;
        public Color Edge = new Color(.85f, .83f, .72f, .8f);
        public float Thickness = 2;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Vector2[] p = { TopLeft, TopRight, BottomRight, BottomLeft };
            for (int i = 0; i < 4; i++) vh.AddVert(p[i], color, Vector2.zero);
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(0, 2, 3);
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = p[i], b = p[(i + 1) % 4], d = (b - a).normalized;
                Vector2 offset = new Vector2(-d.y, d.x) * Thickness;
                int k = vh.currentVertCount;
                vh.AddVert(a, Edge, Vector2.zero); vh.AddVert(b, Edge, Vector2.zero);
                vh.AddVert(b + offset, Edge, Vector2.zero); vh.AddVert(a + offset, Edge, Vector2.zero);
                vh.AddTriangle(k, k + 1, k + 2); vh.AddTriangle(k, k + 2, k + 3);
            }
        }
        public void Tint(Color fill) { color = fill; SetVerticesDirty(); }
        public override bool Raycast(Vector2 screenPoint, Camera eventCamera)
        {
            if (!base.Raycast(screenPoint, eventCamera)) return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var p);
            Vector2[] corners = { TopLeft, TopRight, BottomRight, BottomLeft }; float sign = 0;
            for (int i = 0; i < 4; i++)
            {
                var a = corners[i]; var b = corners[(i + 1) % 4];
                float cross = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
                if (Mathf.Abs(cross) < .01f) continue;
                if (sign != 0 && Mathf.Sign(cross) != sign) return false;
                sign = Mathf.Sign(cross);
            }
            return true;
        }
    }
}
