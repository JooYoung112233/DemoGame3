using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    // Short gunshot streak between two points of its own rect space.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleTracerGraphic : MaskableGraphic
    {
        public Vector2 From, To;
        [Min(0)] public float StartWidth = 7, EndWidth = 2;
        public void Set(Vector2 from, Vector2 to) { From = from; To = to; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var d = To - From; if (d.sqrMagnitude < 1) return;
            var side = new Vector2(-d.y, d.x).normalized; var faded = color; faded.a *= .15f;
            vh.AddVert(From + side * StartWidth * .5f, color, Vector2.zero); vh.AddVert(To + side * EndWidth * .5f, faded, Vector2.zero);
            vh.AddVert(To - side * EndWidth * .5f, faded, Vector2.zero); vh.AddVert(From - side * StartWidth * .5f, color, Vector2.zero);
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(0, 2, 3);
        }
    }
}
