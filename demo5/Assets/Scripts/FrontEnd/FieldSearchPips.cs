using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // 사물 위 수색 진행 칸 (기획/탐험-말놓기-조작-재설계.md · 시안 02 '상자 위 칸 = 수색 진행 (노랑 = 이번 턴)'): Count cells left to
    // right, the first Done in `color`, the next Next in NextColor (this turn's plan fills them), the rest Empty; each with an ink edge.
    // Code-drawn, never takes clicks. FieldPawnBoard sets it from the object's search state and the plan.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FieldSearchPips : MaskableGraphic
    {
        [Tooltip("칸 수 (수색에 드는 턴)")] [Min(1)] public int Count = 2;
        [Tooltip("끝낸 칸 (색: Color)")] [Min(0)] public int Done;
        [Tooltip("이번 턴에 찰 칸")] [Min(0)] public int Next;
        [Tooltip("이번 턴 칸")] public Color NextColor = new Color(1f, .84f, .47f, 1);
        [Tooltip("빈 칸")] public Color Empty = new Color(.31f, .3f, .27f, 1);
        [Tooltip("테두리")] public Color Outline = new Color(.08f, .07f, .06f, 1);
        [Tooltip("테두리 두께 (px)")] [Min(0)] public float OutlineWidth = 1.5f;
        [Tooltip("칸 폭 (px)")] [Min(1)] public float CellWidth = 22;
        [Tooltip("칸 사이 간격 (px)")] [Min(0)] public float Gap = 6;
        public override bool raycastTarget { get => false; set { } }

        // The width the cells need (FieldPawnBoard sizes the paper around it).
        public float PreferredWidth => Count * CellWidth + (Count - 1) * Gap;

        public void Set(int count, int done, int next)
        {
            count = Mathf.Max(1, count); done = Mathf.Clamp(done, 0, count); next = Mathf.Clamp(next, 0, count - done);
            if (count == Count && done == Done && next == Next) return;
            Count = count; Done = done; Next = next; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; int n = Mathf.Max(1, Count);
            float w = Mathf.Min(CellWidth, (r.width - Gap * (n - 1)) / n); if (w <= 0 || r.height <= 0) return;
            float x0 = r.center.x - (n * w + (n - 1) * Gap) * .5f;
            for (int i = 0; i < n; i++)
            {
                var cell = new Rect(x0 + i * (w + Gap), r.yMin, w, r.height);
                Quad(vh, cell, Outline);
                var inner = new Rect(cell.x + OutlineWidth, cell.y + OutlineWidth, cell.width - 2 * OutlineWidth, cell.height - 2 * OutlineWidth);
                Quad(vh, inner, i < Done ? color : i < Done + Next ? NextColor : Empty);
            }
        }

        static void Quad(VertexHelper vh, Rect q, Color k)
        {
            if (q.width <= 0 || q.height <= 0) return;
            int i = vh.currentVertCount; var v = UIVertex.simpleVert; v.color = k;
            v.position = new Vector3(q.xMin, q.yMin); vh.AddVert(v); v.position = new Vector3(q.xMin, q.yMax); vh.AddVert(v);
            v.position = new Vector3(q.xMax, q.yMax); vh.AddVert(v); v.position = new Vector3(q.xMax, q.yMin); vh.AddVert(v);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i + 2, i + 3, i);
        }
    }
}
