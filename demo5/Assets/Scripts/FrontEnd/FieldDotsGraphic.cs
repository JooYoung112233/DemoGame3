using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Code-drawn row of dots (the '이번 턴' panel's 위험도): Count discs left to right, the first Filled in `color`, up to Preview in
    // PreviewColor (this turn's plan raises it), the rest Empty; each with an ink outline. No raster art; never intercepts clicks.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FieldDotsGraphic : MaskableGraphic
    {
        [Tooltip("점 개수")] [Min(1)] public int Count = 3;
        [Tooltip("찬 점 (색: Color)")] [Min(0)] public int Filled;
        [Tooltip("이번 계획대로면 여기까지 찹니다 (Filled보다 클 때만)")] [Min(0)] public int Preview;
        [Tooltip("빈 점")] public Color Empty = new Color(.275f, .314f, .314f, 1);
        [Tooltip("미리보기 점")] public Color PreviewColor = new Color(1f, .62f, .5f, .55f);
        [Tooltip("테두리")] public Color Outline = new Color(.078f, .078f, .078f, 1);
        [Tooltip("테두리 두께 (px)")] [Min(0)] public float OutlineWidth = 2;
        [Tooltip("점 사이 간격 (px)")] [Min(0)] public float Gap = 10;
        [Tooltip("원을 이루는 조각 수")] [Range(8, 64)] public int Segments = 24;
        public override bool raycastTarget { get => false; set { } }

        public void Set(int filled, int preview)
        {
            filled = Mathf.Clamp(filled, 0, Count); preview = Mathf.Clamp(preview, 0, Count);
            if (filled == Filled && preview == Preview) return;
            Filled = filled; Preview = preview; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; int n = Mathf.Max(1, Count);
            float d = Mathf.Min(r.height, (r.width - Gap * (n - 1)) / n); if (d <= 0) return;
            for (int i = 0; i < n; i++)
            {
                var c = new Vector2(r.xMin + d * .5f + i * (d + Gap), r.center.y);
                Disc(vh, c, d * .5f, Outline);
                Disc(vh, c, Mathf.Max(0, d * .5f - OutlineWidth), i < Filled ? color : i < Preview ? PreviewColor : Empty);
            }
        }
        void Disc(VertexHelper vh, Vector2 c, float radius, Color k)
        {
            if (radius <= 0) return; int s = vh.currentVertCount; Color32 col = k;
            vh.AddVert(c, col, Vector2.zero);
            for (int i = 0; i <= Segments; i++) { float a = i * Mathf.PI * 2 / Segments; vh.AddVert(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, col, Vector2.zero); }
            for (int i = 1; i <= Segments; i++) vh.AddTriangle(s, s + i, s + i + 1);
        }
    }
}
