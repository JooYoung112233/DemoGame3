using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Code-drawn HUD panel (탐험 화면 정리, 2026-09-25): a flat fill in `color` with a border band of Thickness inside the rect
    // in Border. The exploration tray, its three panels ('대원', '이번 턴', '행동') and the dark '거점으로 귀환' frame use it.
    // No raster art. Raycast Target (Inspector) decides whether it takes clicks (the tray floor does: the room behind stays unclicked).
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FieldPanelGraphic : MaskableGraphic
    {
        [Tooltip("테두리 색 (알파 0이면 테두리 없음)")] public Color Border = new Color(.275f, .33f, .33f, 1);
        [Tooltip("테두리 두께 (px · 사각형 안쪽으로)")] [Min(0)] public float Thickness = 2;
        [Tooltip("안쪽 채우기 (끄면 테두리만)")] public bool Fill = true;

        public void SetBorder(Color c) { if (Border == c) return; Border = c; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; float t = Mathf.Min(Thickness, Mathf.Min(r.width, r.height) * .5f);
            if (Fill && color.a > 0) Quad(vh, r.xMin + t, r.yMin + t, r.xMax - t, r.yMax - t, color);
            if (t <= 0 || Border.a <= 0) return;
            Quad(vh, r.xMin, r.yMax - t, r.xMax, r.yMax, Border); Quad(vh, r.xMin, r.yMin, r.xMax, r.yMin + t, Border);
            Quad(vh, r.xMin, r.yMin + t, r.xMin + t, r.yMax - t, Border); Quad(vh, r.xMax - t, r.yMin + t, r.xMax, r.yMax - t, Border);
        }
        static void Quad(VertexHelper vh, float x0, float y0, float x1, float y1, Color c)
        {
            if (x1 - x0 <= 0 || y1 - y0 <= 0) return;
            int i = vh.currentVertCount; Color32 k = c;
            vh.AddVert(new Vector3(x0, y0), k, Vector2.zero); vh.AddVert(new Vector3(x0, y1), k, Vector2.zero);
            vh.AddVert(new Vector3(x1, y1), k, Vector2.zero); vh.AddVert(new Vector3(x1, y0), k, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
