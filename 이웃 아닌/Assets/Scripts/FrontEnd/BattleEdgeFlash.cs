using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    // Screen-edge wash when an ally is hurt. Outer edge carries the color, inner edge fades to clear.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleEdgeFlash : MaskableGraphic
    {
        [Min(1)] public float Thickness = 110;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; var clear = color; clear.a = 0; float t = Thickness;
            Vector2[] outer = { new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin) };
            Vector2[] inner = { new Vector2(r.xMin + t, r.yMin + t), new Vector2(r.xMin + t, r.yMax - t), new Vector2(r.xMax - t, r.yMax - t), new Vector2(r.xMax - t, r.yMin + t) };
            foreach (var p in outer) vh.AddVert(p, color, Vector2.zero);
            foreach (var p in inner) vh.AddVert(p, clear, Vector2.zero);
            for (int i = 0; i < 4; i++) { int j = (i + 1) % 4; vh.AddTriangle(i, j, 4 + j); vh.AddTriangle(i, 4 + j, 4 + i); }
        }
    }
}
