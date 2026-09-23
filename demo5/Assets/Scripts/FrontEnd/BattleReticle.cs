using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    // Four corner brackets around the aimed standee. The rect is sized by the panel; it breathes slightly.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleReticle : MaskableGraphic
    {
        [Min(1)] public float Corner = 26, Thickness = 5;
        [Range(0, .3f)] public float Breathe = .06f;
        [Min(0)] public float BreatheSpeed = 5;
        void Update() { float s = 1 + Mathf.Sin(Time.unscaledTime * BreatheSpeed) * Breathe; transform.localScale = new Vector3(s, s, 1); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; float c = Mathf.Min(Corner, Mathf.Min(r.width, r.height) * .45f), t = Thickness;
            foreach (var sx in new[] { -1, 1 })
                foreach (var sy in new[] { -1, 1 })
                {
                    var corner = new Vector2(sx < 0 ? r.xMin : r.xMax, sy < 0 ? r.yMin : r.yMax);
                    Bar(vh, corner, corner + new Vector2(-sx * c, 0), t); Bar(vh, corner, corner + new Vector2(0, -sy * c), t);
                }
        }
        void Bar(VertexHelper vh, Vector2 a, Vector2 b, float t)
        {
            var d = (b - a).normalized; var side = new Vector2(-d.y, d.x) * t * .5f; a -= d * t * .5f; int k = vh.currentVertCount;
            vh.AddVert(a + side, color, Vector2.zero); vh.AddVert(b + side, color, Vector2.zero); vh.AddVert(b - side, color, Vector2.zero); vh.AddVert(a - side, color, Vector2.zero);
            vh.AddTriangle(k, k + 1, k + 2); vh.AddTriangle(k, k + 2, k + 3);
        }
    }
}
