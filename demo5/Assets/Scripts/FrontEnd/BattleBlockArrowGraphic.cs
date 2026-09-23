using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    // Thick right-pointing arrow for the before → after treatment preview.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleBlockArrowGraphic : MaskableGraphic
    {
        [Range(.2f, .9f)] public float Shaft = .5f, HeadLength = .38f;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; float hx = r.xMax - r.width * HeadLength, sy = r.height * Shaft * .5f, cy = r.center.y;
            Vector2[] p = { new Vector2(r.xMin, cy - sy), new Vector2(r.xMin, cy + sy), new Vector2(hx, cy + sy), new Vector2(hx, r.yMax), new Vector2(r.xMax, cy), new Vector2(hx, r.yMin), new Vector2(hx, cy - sy) };
            var faded = color; faded.a *= .55f;
            for (int i = 0; i < p.Length; i++) vh.AddVert(p[i], i < 2 ? faded : color, Vector2.zero);
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(0, 2, 6); vh.AddTriangle(3, 4, 5);
        }
    }
}
