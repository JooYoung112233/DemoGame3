using UnityEngine;
using UnityEngine.UI;
namespace Demo5.NightRun
{
    // Resolution-independent UI decoration. The background and type remain editable.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PaperGrain : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;uint seed=79;
            System.Func<float> sample=()=>{seed=1664525*seed+1013904223;return (seed&65535)/65535f;};
            int count=Mathf.Clamp(Mathf.RoundToInt(r.width*r.height/65),40,2200);
            for(int i=0;i<count;i++){float x=r.xMin+sample()*r.width,y=r.yMin+sample()*r.height,w=1+sample()*3,h=1+sample()*2;Add(vh,x,y,Mathf.Min(w,r.xMax-x),Mathf.Min(h,r.yMax-y),new Color(.13f,.12f,.09f,.03f+sample()*.09f));}
            for(int i=0;i<r.width;i+=7){float d=1+sample()*3;Add(vh,r.xMin+i,r.yMin,Mathf.Min(7,r.width-i),d,new Color(.12f,.14f,.12f,.2f));Add(vh,r.xMin+i,r.yMax-d,Mathf.Min(7,r.width-i),d,new Color(.12f,.14f,.12f,.2f));}
        }
        static void Add(VertexHelper vh,float x,float y,float w,float h,Color c){int k=vh.currentVertCount;vh.AddVert(new Vector3(x,y),c,Vector2.zero);vh.AddVert(new Vector3(x+w,y),c,Vector2.zero);vh.AddVert(new Vector3(x+w,y+h),c,Vector2.zero);vh.AddVert(new Vector3(x,y+h),c,Vector2.zero);vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);}
    }
}
