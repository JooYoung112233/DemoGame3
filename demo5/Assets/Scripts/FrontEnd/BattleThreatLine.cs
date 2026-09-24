using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    // Dashed arc from an enemy to the ally it will hit next. Dashes creep toward the target.
    // Head 0 (the default) ends it in a small dot instead of an arrowhead, so it never reads like the player's aim arrow.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleThreatLine : MaskableGraphic
    {
        public Vector2 From, To;
        [Min(0)] public float Arc = 70, Width = 4, Dash = 16, Gap = 10, Head = 0, Dot = 6, CreepSpeed = 22;
        float creep;
        public void Set(Vector2 from, Vector2 to) { if (from == From && to == To) return; From = from; To = to; SetVerticesDirty(); }
        void Update() { if (CreepSpeed <= 0) return; creep = (creep + Time.unscaledDeltaTime * CreepSpeed) % (Dash + Gap); SetVerticesDirty(); }
        Vector2 At(float t) { var c = (From + To) * .5f + Vector2.up * Arc; return (1 - t) * (1 - t) * From + 2 * (1 - t) * t * c + t * t * To; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if ((To - From).sqrMagnitude < 4) return;
            const int samples = 48; var points = new Vector2[samples + 1]; var lengths = new float[samples + 1];
            for (int i = 0; i <= samples; i++) { points[i] = At(i / (float)samples); if (i > 0) lengths[i] = lengths[i - 1] + Vector2.Distance(points[i - 1], points[i]); }
            float total = lengths[samples] - (Head > 0 ? Head * .8f : Dot * 1.6f);
            for (float s = creep - Dash; s < total; s += Dash + Gap)
            {
                float a = Mathf.Max(0, s), b = Mathf.Min(total, s + Dash); if (b <= a) continue;
                Segment(vh, Sample(points, lengths, a), Sample(points, lengths, b));
            }
            var tip = points[samples]; int k = vh.currentVertCount;
            if (Head <= 0)
            {
                // A round marker on the target, like a pin on the board.
                if (Dot <= 0) return;
                vh.AddVert(tip, color, Vector2.zero);
                for (int i = 0; i <= 12; i++) { float a = i / 12f * Mathf.PI * 2; vh.AddVert(tip + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Dot, color, Vector2.zero); if (i > 0) vh.AddTriangle(k, k + i, k + i + 1); }
                return;
            }
            var back = Sample(points, lengths, lengths[samples] - Head); var dir = (tip - back).normalized; var side = new Vector2(-dir.y, dir.x);
            vh.AddVert(tip, color, Vector2.zero); vh.AddVert(back + side * Head * .55f, color, Vector2.zero); vh.AddVert(back - side * Head * .55f, color, Vector2.zero);
            vh.AddTriangle(k, k + 1, k + 2);
        }
        static Vector2 Sample(Vector2[] p, float[] l, float at)
        {
            for (int i = 1; i < p.Length; i++) if (l[i] >= at) return Vector2.Lerp(p[i - 1], p[i], Mathf.InverseLerp(l[i - 1], l[i], at));
            return p[p.Length - 1];
        }
        void Segment(VertexHelper vh, Vector2 a, Vector2 b)
        {
            var side = new Vector2(-(b - a).y, (b - a).x).normalized * Width * .5f; int k = vh.currentVertCount;
            vh.AddVert(a + side, color, Vector2.zero); vh.AddVert(b + side, color, Vector2.zero); vh.AddVert(b - side, color, Vector2.zero); vh.AddVert(a - side, color, Vector2.zero);
            vh.AddTriangle(k, k + 1, k + 2); vh.AddTriangle(k, k + 2, k + 3);
        }
    }
}
