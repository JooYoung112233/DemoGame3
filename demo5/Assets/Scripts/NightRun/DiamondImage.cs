using UnityEngine;
using UnityEngine.UI;

namespace Demo5.NightRun
{
    // A native UI mesh; clicks respect the diamond rather than its overlapping bounds.
    public sealed class DiamondImage : Image
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; var c = color;
            vh.AddVert(new Vector3(r.xMin, r.center.y), c, Vector2.zero);
            vh.AddVert(new Vector3(r.center.x, r.yMax), c, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.center.y), c, Vector2.zero);
            vh.AddVert(new Vector3(r.center.x, r.yMin), c, Vector2.zero);
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(0, 2, 3);
        }
        public override bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, sp, eventCamera, out var p)) return false;
            var r = rectTransform.rect;
            return Mathf.Abs((p.x - r.center.x) / (r.width * .5f)) + Mathf.Abs((p.y - r.center.y) / (r.height * .5f)) <= 1;
        }
    }
}
