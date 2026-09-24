using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // '계속 진행' glyph drawn in code (no font glyph needed): ▶▶ while off, two bars (멈추기) while on. Never intercepts clicks.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FieldForwardGlyph : MaskableGraphic
    {
        [Tooltip("두 삼각형(또는 막대) 사이 간격 (폭 비율)")] [Range(0, .4f)] public float Gap = .08f;
        [Tooltip("두 막대 (멈추기)로 그림 · 계속 진행이 켜지고 꺼질 때 FieldAutoAdvance가 바꿉니다")] [SerializeField] bool bars;
        public override bool raycastTarget { get => false; set { } }
        public bool Bars { get => bars; set { if (bars == value) return; bars = value; SetVerticesDirty(); } }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; float gap = r.width * Gap, w = (r.width - gap) * .5f;
            for (int k = 0; k < 2; k++)
            {
                float x = r.xMin + k * (w + gap); int s = vh.currentVertCount;
                if (bars)
                {
                    float inset = w * .18f;
                    vh.AddVert(new Vector3(x + inset, r.yMin), color, Vector2.zero); vh.AddVert(new Vector3(x + inset, r.yMax), color, Vector2.zero);
                    vh.AddVert(new Vector3(x + w - inset, r.yMax), color, Vector2.zero); vh.AddVert(new Vector3(x + w - inset, r.yMin), color, Vector2.zero);
                    vh.AddTriangle(s, s + 1, s + 2); vh.AddTriangle(s, s + 2, s + 3);
                }
                else
                {
                    vh.AddVert(new Vector3(x, r.yMin), color, Vector2.zero); vh.AddVert(new Vector3(x, r.yMax), color, Vector2.zero); vh.AddVert(new Vector3(x + w, r.center.y), color, Vector2.zero);
                    vh.AddTriangle(s, s + 1, s + 2);
                }
            }
        }
    }
}
