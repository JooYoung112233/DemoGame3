using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Code-drawn screen-edge glow (the creature warning's short red pulse): a band of Width along each edge of the rect, `color` at
    // the edge fading to clear inward. FieldTurnWarning sets Strength (0..1) over a moment. No raster art; never takes clicks.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FieldEdgePulse : MaskableGraphic
    {
        [Tooltip("가장자리 띠 폭 (px)")] [Min(1)] public float Width = 46;
        [Tooltip("지금 세기 (0~1 · 경고 연출이 바꿉니다)")] [Range(0, 1)] [SerializeField] float strength;
        public override bool raycastTarget { get => false; set { } }
        public float Strength { get => strength; set { value = Mathf.Clamp01(value); if (Mathf.Approximately(value, strength)) return; strength = value; SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (strength <= 0) return; var r = rectTransform.rect; float w = Mathf.Min(Width, Mathf.Min(r.width, r.height) * .5f);
            var edge = new Color(color.r, color.g, color.b, color.a * strength); var clear = new Color(color.r, color.g, color.b, 0);
            // Outer and inner rectangles; four trapezoids between them.
            var o = new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin) };
            var i = new[] { new Vector2(r.xMin + w, r.yMin + w), new Vector2(r.xMin + w, r.yMax - w), new Vector2(r.xMax - w, r.yMax - w), new Vector2(r.xMax - w, r.yMin + w) };
            for (int k = 0; k < 4; k++)
            {
                int n = (k + 1) % 4, s = vh.currentVertCount;
                vh.AddVert(o[k], edge, Vector2.zero); vh.AddVert(o[n], edge, Vector2.zero); vh.AddVert(i[n], clear, Vector2.zero); vh.AddVert(i[k], clear, Vector2.zero);
                vh.AddTriangle(s, s + 1, s + 2); vh.AddTriangle(s, s + 2, s + 3);
            }
        }
    }
}
