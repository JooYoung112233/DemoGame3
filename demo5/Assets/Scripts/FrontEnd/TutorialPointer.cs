using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 // Tutorial arrow: a solid gold arrow with a dark rim, drawn pointing down (the guide rotates it to face the next click). Never intercepts clicks.
 [RequireComponent(typeof(CanvasRenderer))]
 public sealed class TutorialPointer : MaskableGraphic {
  public Color Rim=new Color(.08f,.07f,.06f,.9f);
  [Min(0)] public float RimWidth=4;
  public override bool raycastTarget{get=>false;set{}}
  protected override void OnPopulateMesh(VertexHelper vh){
   vh.Clear();var r=rectTransform.rect;
   Arrow(vh,r,Rim);
   var inner=new Rect(r.x+RimWidth,r.y+RimWidth*1.6f,r.width-RimWidth*2,r.height-RimWidth*2.2f);
   Arrow(vh,inner,color);
  }
  // Shaft on top, head below; the tip is the bottom centre of the rect.
  static void Arrow(VertexHelper vh,Rect r,Color c){
   float cx=r.center.x,headTop=r.yMin+r.height*.55f,shaft=r.width*.36f;
   int s=vh.currentVertCount;
   vh.AddVert(new Vector3(cx-shaft*.5f,r.yMax),c,Vector2.zero);vh.AddVert(new Vector3(cx+shaft*.5f,r.yMax),c,Vector2.zero);
   vh.AddVert(new Vector3(cx+shaft*.5f,headTop),c,Vector2.zero);vh.AddVert(new Vector3(cx-shaft*.5f,headTop),c,Vector2.zero);
   vh.AddTriangle(s,s+1,s+2);vh.AddTriangle(s,s+2,s+3);
   s=vh.currentVertCount;
   vh.AddVert(new Vector3(r.xMin,headTop),c,Vector2.zero);vh.AddVert(new Vector3(r.xMax,headTop),c,Vector2.zero);vh.AddVert(new Vector3(cx,r.yMin),c,Vector2.zero);
   vh.AddTriangle(s,s+1,s+2);
  }
 }
}
