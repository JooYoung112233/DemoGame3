using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    // Card-game style targeting arrow: a chain of chevrons along an arc that grow toward a head.
    // Endpoints are in the graphic's own rect space (full-screen layer, top-left pivot).
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleAimArrow : MaskableGraphic
    {
        public Vector2 From, To;
        [Range(0, 1)] public float Arc = .32f;
        [Range(4, 40)] public int Links = 16;
        [Min(1)] public float LinkSize = 16, HeadSize = 34, Thickness = .42f, FlowSpeed = 1.6f;
        float flow;
        public void Set(Vector2 from, Vector2 to) { if (from == From && to == To) return; From = from; To = to; SetVerticesDirty(); }
        void Update() { flow = (flow + Time.unscaledDeltaTime * FlowSpeed) % 1; SetVerticesDirty(); }
        Vector2 Control()
        {
            var d = To - From; var side = new Vector2(-d.y, d.x).normalized; if (side.y < 0) side = -side;
            return (From + To) * .5f + side * d.magnitude * Arc;
        }
        Vector2 At(float t, Vector2 c) => (1 - t) * (1 - t) * From + 2 * (1 - t) * t * c + t * t * To;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if ((To - From).sqrMagnitude < 400) return;
            var c = Control();
            float stop = 1 - Mathf.Clamp01(HeadSize * .8f / Mathf.Max(1, (To - From).magnitude));
            for (int i = 0; i < Links; i++)
            {
                float t = (i + flow) / Links * stop; if (t > stop) continue;
                var p = At(t, c); var dir = (At(Mathf.Min(1, t + .01f), c) - p).normalized;
                float grow = Mathf.Lerp(.45f, 1f, t / stop), fade = Mathf.Clamp01(t * 6);
                Chevron(vh, p, dir, LinkSize * grow, color * new Color(1, 1, 1, fade));
            }
            var tipDir = (To - At(.97f, c)).normalized; var back = To - tipDir * HeadSize; var side = new Vector2(-tipDir.y, tipDir.x);
            int k = vh.currentVertCount;
            vh.AddVert(To, color, Vector2.zero); vh.AddVert(back + side * HeadSize * .62f, color, Vector2.zero);
            vh.AddVert(back + tipDir * HeadSize * .28f, color, Vector2.zero); vh.AddVert(back - side * HeadSize * .62f, color, Vector2.zero);
            vh.AddTriangle(k, k + 1, k + 2); vh.AddTriangle(k, k + 2, k + 3);
        }
        void Chevron(VertexHelper vh, Vector2 p, Vector2 dir, float size, Color tint)
        {
            var side = new Vector2(-dir.y, dir.x); float t = Thickness * size;
            Vector2 tip = p + dir * size * .5f, wingA = p - dir * size * .5f + side * size * .55f, wingB = p - dir * size * .5f - side * size * .55f;
            int k = vh.currentVertCount;
            vh.AddVert(tip, tint, Vector2.zero); vh.AddVert(wingA, tint, Vector2.zero); vh.AddVert(wingA - dir * t, tint, Vector2.zero);
            vh.AddVert(tip - dir * t, tint, Vector2.zero); vh.AddVert(wingB - dir * t, tint, Vector2.zero); vh.AddVert(wingB, tint, Vector2.zero);
            vh.AddTriangle(k, k + 1, k + 2); vh.AddTriangle(k, k + 2, k + 3); vh.AddTriangle(k, k + 3, k + 4); vh.AddTriangle(k, k + 4, k + 5);
        }
    }
}
