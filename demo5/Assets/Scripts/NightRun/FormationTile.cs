using UnityEngine;
using UnityEngine.UI;
namespace Demo5.NightRun
{
    // Native UI mesh: scalable formation tile; no baked light or background pixels.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FormationTile : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;var c=color;
            Vector2[] pts={new Vector2(r.xMin+13,r.yMin),new Vector2(r.xMax,r.yMin),new Vector2(r.xMax-13,r.yMax),new Vector2(r.xMin,r.yMax)};
            for(int i=0;i<4;i++)vh.AddVert(pts[i],c,Vector2.zero);vh.AddTriangle(0,1,2);vh.AddTriangle(0,2,3);
            for(int i=0;i<4;i++){var a=pts[i];var b=pts[(i+1)%4];var n=(b-a).normalized;var off=new Vector2(-n.y,n.x)*1.3f;int k=vh.currentVertCount;vh.AddVert(a, new Color(.84f,.81f,.71f,.7f),Vector2.zero);vh.AddVert(b,new Color(.84f,.81f,.71f,.7f),Vector2.zero);vh.AddVert(b+off,new Color(.84f,.81f,.71f,.7f),Vector2.zero);vh.AddVert(a+off,new Color(.84f,.81f,.71f,.7f),Vector2.zero);vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);}
        }
    }
}
