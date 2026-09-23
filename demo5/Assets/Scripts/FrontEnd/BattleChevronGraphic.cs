using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    // Small movement arrow for enemy intent tags. Angle 180 points toward the allies.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleChevronGraphic : MaskableGraphic
    {
        [Range(-180, 180)] public float Angle = 180;
        [Range(.1f, .6f)] public float Thickness = .3f;
        public void SetAngle(float angle) { if (Mathf.Approximately(angle, Angle)) return; Angle = angle; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; float s = Mathf.Min(r.width, r.height) * .5f;
            var q = Quaternion.Euler(0, 0, Angle);
            Vector2[] p = { new Vector2(.95f, 0), new Vector2(.05f, .8f), new Vector2(.05f - Thickness, .8f - Thickness * .35f), new Vector2(.95f - Thickness * 1.3f, 0),
                new Vector2(.05f - Thickness, -.8f + Thickness * .35f), new Vector2(.05f, -.8f) };
            foreach (var v in p) vh.AddVert(r.center + (Vector2)(q * (new Vector2(v.x - .45f, v.y) * s)), color, Vector2.zero);
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(0, 2, 3); vh.AddTriangle(0, 3, 4); vh.AddTriangle(0, 4, 5);
        }
    }
}
