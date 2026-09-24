using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Code-drawn ring for the noise a searched object made this turn (FieldTurnReplay scales and fades it). Never intercepts clicks.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FieldNoiseRipple : MaskableGraphic
    {
        [Tooltip("고리 두께 (px, 원래 크기 기준)")] [Min(1)] public float Thickness = 5;
        [Tooltip("고리를 이루는 조각 수")] [Range(12, 96)] public int Segments = 48;
        public override bool raycastTarget { get => false; set { } }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; float outer = Mathf.Min(r.width, r.height) * .5f, inner = Mathf.Max(0, outer - Thickness); var c = r.center;
            for (int i = 0; i <= Segments; i++)
            {
                float a = i * Mathf.PI * 2 / Segments; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                vh.AddVert(c + d * outer, color, Vector2.zero); vh.AddVert(c + d * inner, color, Vector2.zero);
                if (i > 0) { int s = i * 2; vh.AddTriangle(s - 2, s, s + 1); vh.AddTriangle(s - 2, s + 1, s - 1); }
            }
        }
    }
}
