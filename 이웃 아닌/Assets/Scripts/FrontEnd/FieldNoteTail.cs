using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // The search note's tail (FieldSearchNote): one code-drawn triangle in `color` from a base on the paper's edge to a tip toward
    // the object's marker. Points are in this rect's local space (the node stretches over the note's parent, so they are the note
    // layer's own coordinates). No raster art; never intercepts clicks.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FieldNoteTail : MaskableGraphic
    {
        [Tooltip("밑변 가운데 (쪽지 가장자리 안쪽)")] public Vector2 Base;
        [Tooltip("밑변 방향 (단위 벡터)")] public Vector2 Along = Vector2.up;
        [Tooltip("밑변 절반 (px)")] [Min(0)] public float HalfWidth = 16;
        [Tooltip("꼭짓점 (사물 표식 쪽)")] public Vector2 Tip;
        public override bool raycastTarget { get => false; set { } }

        public void Set(Vector2 at, Vector2 along, float half, Vector2 tip)
        {
            if (at == Base && along == Along && Mathf.Approximately(half, HalfWidth) && tip == Tip) return;
            Base = at; Along = along; HalfWidth = half; Tip = tip; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (HalfWidth <= 0 || (Tip - Base).sqrMagnitude < 1) return;
            var side = Along.sqrMagnitude > 0 ? Along.normalized * HalfWidth : Vector2.up * HalfWidth; Color32 c = color;
            vh.AddVert(Base + side, c, Vector2.zero); vh.AddVert(Base - side, c, Vector2.zero); vh.AddVert(Tip, c, Vector2.zero);
            vh.AddTriangle(0, 1, 2);
        }
    }
}
