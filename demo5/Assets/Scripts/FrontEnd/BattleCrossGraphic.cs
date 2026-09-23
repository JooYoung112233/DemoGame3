using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    // Plus sign: the treatment mark, or rotated 45° as the locked-slot X in the item grid.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleCrossGraphic : MaskableGraphic
    {
        [Range(.05f, .6f)] public float Thickness = .32f;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; float s = Mathf.Min(r.width, r.height) * .5f, t = s * Thickness; var c = r.center;
            Rect(vh, c + new Vector2(-s, -t), c + new Vector2(s, t)); Rect(vh, c + new Vector2(-t, -s), c + new Vector2(t, -t)); Rect(vh, c + new Vector2(-t, t), c + new Vector2(t, s));
        }
        void Rect(VertexHelper vh, Vector2 min, Vector2 max)
        {
            int k = vh.currentVertCount;
            vh.AddVert(new Vector2(min.x, min.y), color, Vector2.zero); vh.AddVert(new Vector2(min.x, max.y), color, Vector2.zero);
            vh.AddVert(new Vector2(max.x, max.y), color, Vector2.zero); vh.AddVert(new Vector2(max.x, min.y), color, Vector2.zero);
            vh.AddTriangle(k, k + 1, k + 2); vh.AddTriangle(k, k + 2, k + 3);
        }
    }
}
