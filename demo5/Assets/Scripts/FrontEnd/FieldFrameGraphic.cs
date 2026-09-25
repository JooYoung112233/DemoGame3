using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Code-drawn rectangular frame (the member action slot's border, the selected card's outline): a band of Thickness along
    // the rect's inner edge, solid or dashed. No raster art; never intercepts clicks.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FieldFrameGraphic : MaskableGraphic
    {
        [Tooltip("테두리 두께 (px · 사각형 안쪽으로)")] [Min(.5f)] public float Thickness = 3;
        [Tooltip("점선 한 칸 길이 (px · 0이면 실선)")] [Min(0)] public float Dash;
        [Tooltip("점선 사이 간격 (px)")] [Min(0)] public float Gap = 6;
        public override bool raycastTarget { get => false; set { } }

        public void SetStyle(Color c, float thickness, float dash)
        {
            if (color != c) color = c;
            if (Mathf.Approximately(Thickness, thickness) && Mathf.Approximately(Dash, dash)) return;
            Thickness = thickness; Dash = dash; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; float t = Mathf.Min(Thickness, Mathf.Min(r.width, r.height) * .5f);
            if (t <= 0) return;
            Edge(vh, r.xMin, r.yMax - t, r.xMax, r.yMax, true);
            Edge(vh, r.xMin, r.yMin, r.xMax, r.yMin + t, true);
            Edge(vh, r.xMin, r.yMin + t, r.xMin + t, r.yMax - t, false);
            Edge(vh, r.xMax - t, r.yMin + t, r.xMax, r.yMax - t, false);
        }
        void Edge(VertexHelper vh, float x0, float y0, float x1, float y1, bool horizontal)
        {
            if (Dash <= 0) { Quad(vh, x0, y0, x1, y1); return; }
            float length = horizontal ? x1 - x0 : y1 - y0, step = Dash + Mathf.Max(0, Gap);
            for (float s = 0; s < length; s += step)
            {
                float e = Mathf.Min(length, s + Dash);
                if (horizontal) Quad(vh, x0 + s, y0, x0 + e, y1); else Quad(vh, x0, y0 + s, x1, y0 + e);
            }
        }
        void Quad(VertexHelper vh, float x0, float y0, float x1, float y1)
        {
            if (x1 - x0 <= 0 || y1 - y0 <= 0) return;
            int i = vh.currentVertCount; var c = (Color32)color;
            vh.AddVert(new Vector3(x0, y0), c, Vector2.zero); vh.AddVert(new Vector3(x0, y1), c, Vector2.zero);
            vh.AddVert(new Vector3(x1, y1), c, Vector2.zero); vh.AddVert(new Vector3(x1, y0), c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
