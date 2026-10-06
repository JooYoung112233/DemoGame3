using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    // Parallel tapered strokes: crowbar swipes, claw marks and the small intent claw icon.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleSlashGraphic : MaskableGraphic
    {
        [Range(1, 5)] public int Strokes = 3;
        [Range(-180, 180)] public float Angle = 35;
        [Range(0, 1)] public float Progress = 1;
        [Range(.02f, .5f)] public float Width = .16f;
        [Range(0, .6f)] public float Spacing = .26f;
        public void SetProgress(float value) { Progress = Mathf.Clamp01(value); SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (Progress <= 0) return;
            var r = rectTransform.rect; float size = Mathf.Min(r.width, r.height);
            var dir = new Vector2(Mathf.Cos(Angle * Mathf.Deg2Rad), Mathf.Sin(Angle * Mathf.Deg2Rad)); var side = new Vector2(-dir.y, dir.x);
            for (int s = 0; s < Strokes; s++)
            {
                float lane = s - (Strokes - 1) * .5f, length = size * (1 - Mathf.Abs(lane) * .18f);
                var start = r.center - dir * length * .5f + side * lane * Spacing * size; var end = start + dir * length * Progress;
                var mid = (start + end) * .5f; float w = Width * size * (1 - Mathf.Abs(lane) * .2f) * .5f;
                int k = vh.currentVertCount;
                vh.AddVert(start, color, Vector2.zero); vh.AddVert(mid + side * w, color, Vector2.zero);
                vh.AddVert(end, color, Vector2.zero); vh.AddVert(mid - side * w, color, Vector2.zero);
                vh.AddTriangle(k, k + 1, k + 2); vh.AddTriangle(k, k + 2, k + 3);
            }
        }
    }
}
