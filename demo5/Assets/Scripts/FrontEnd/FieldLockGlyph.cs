using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Code-drawn padlock for a place a held pawn cannot take now (시안 01 '지렛대 필요'): a shackle arc over a body with a keyhole
    // cut in the paper colour. No raster art; drawn in `color` inside the smaller side of the rect. Never intercepts clicks.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FieldLockGlyph : MaskableGraphic
    {
        [Tooltip("고리 굵기 (표시 크기 대비)")] [Range(.06f, .4f)] public float Stroke = .17f;
        [Tooltip("열쇠 구멍 색 (바탕 종이 색)")] public Color HoleColor = new Color(.86f, .85f, .82f, 1);
        [Tooltip("곡선을 몇 조각으로 그릴지")] [Range(8, 48)] public int Smoothness = 20;
        public override bool raycastTarget { get => false; set { } }
        Vector2 center; float half; VertexHelper v;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; center = r.center; half = Mathf.Min(r.width, r.height) * .5f; v = vh; if (half <= 0) return;
            // Shackle: a half ring on two short legs; body: a rounded-looking block; keyhole: a disc and a slot.
            Arc(new Vector2(0, .22f), .38f, Stroke, 0, 180);
            Quad(new Vector2(-.38f - Stroke * .5f, .22f), new Vector2(-.38f + Stroke * .5f, .22f), new Vector2(-.38f + Stroke * .5f, .02f), new Vector2(-.38f - Stroke * .5f, .02f), color);
            Quad(new Vector2(.38f - Stroke * .5f, .22f), new Vector2(.38f + Stroke * .5f, .22f), new Vector2(.38f + Stroke * .5f, .02f), new Vector2(.38f - Stroke * .5f, .02f), color);
            Quad(new Vector2(-.66f, .06f), new Vector2(.66f, .06f), new Vector2(.66f, -.86f), new Vector2(-.66f, -.86f), color);
            Disc(new Vector2(0, -.3f), .15f, HoleColor);
            Quad(new Vector2(-.06f, -.3f), new Vector2(.06f, -.3f), new Vector2(.08f, -.64f), new Vector2(-.08f, -.64f), HoleColor);
            v = null;
        }
        Vector3 P(Vector2 unit) => center + unit * half;
        void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col)
        {
            int i = v.currentVertCount; v.AddVert(P(a), col, Vector2.zero); v.AddVert(P(b), col, Vector2.zero); v.AddVert(P(c), col, Vector2.zero); v.AddVert(P(d), col, Vector2.zero);
            v.AddTriangle(i, i + 1, i + 2); v.AddTriangle(i, i + 2, i + 3);
        }
        void Arc(Vector2 c, float r, float w, float from, float to)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(to - from) / 360f * Smoothness * 2)); int start = v.currentVertCount;
            for (int k = 0; k <= steps; k++)
            {
                float a = Mathf.Lerp(from, to, (float)k / steps) * Mathf.Deg2Rad; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                v.AddVert(P(c + d * (r + w * .5f)), color, Vector2.zero); v.AddVert(P(c + d * Mathf.Max(0, r - w * .5f)), color, Vector2.zero);
            }
            for (int k = 0; k < steps; k++) { int o = start + k * 2; v.AddTriangle(o, o + 2, o + 1); v.AddTriangle(o + 1, o + 2, o + 3); }
        }
        void Disc(Vector2 c, float r, Color col)
        {
            int start = v.currentVertCount; v.AddVert(P(c), col, Vector2.zero);
            for (int k = 0; k <= Smoothness; k++) { float a = k * Mathf.PI * 2 / Smoothness; v.AddVert(P(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r), col, Vector2.zero); }
            for (int k = 0; k < Smoothness; k++) v.AddTriangle(start, start + k + 1, start + k + 2);
        }
    }
}
