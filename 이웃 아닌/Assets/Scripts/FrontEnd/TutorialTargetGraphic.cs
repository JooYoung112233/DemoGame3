using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 // Code-native frame for the next click: a full outline plus thick corner marks, each with a dark rim so it reads on paper and on dark rooms.
 // Never intercepts clicks and never covers the control's label (it is drawn outside the control).
 [RequireComponent(typeof(CanvasRenderer))]
 public sealed class TutorialTargetGraphic : MaskableGraphic {
  [Min(0)] public float Outline=3, Corner=7, CornerLength=34, RimWidth=2;
  public Color Rim=new Color(.08f,.07f,.06f,.85f);
  public override bool raycastTarget{get=>false;set{}}
  protected override void OnPopulateMesh(VertexHelper vh){
   vh.Clear();var r=rectTransform.rect;float n=Mathf.Min(CornerLength,Mathf.Min(r.width,r.height)/2.5f);
   Frame(vh,r,Outline+RimWidth*2,Mathf.Min(r.width,r.height)/2,Rim,-RimWidth);Frame(vh,r,Outline,Mathf.Min(r.width,r.height)/2,color,0);
   Corners(vh,r,Corner+RimWidth*2,n+RimWidth,Rim,-RimWidth);Corners(vh,r,Corner,n,color,0);
  }
  // A rectangular ring 't' thick along the rect (grown outward by 'grow').
  void Frame(VertexHelper vh,Rect r,float t,float cap,Color c,float grow){
   r=new Rect(r.x+grow,r.y+grow,r.width-grow*2,r.height-grow*2);t=Mathf.Min(t,cap);
   Bar(vh,r.xMin,r.yMin,r.width,t,c);Bar(vh,r.xMin,r.yMax-t,r.width,t,c);Bar(vh,r.xMin,r.yMin+t,t,r.height-t*2,c);Bar(vh,r.xMax-t,r.yMin+t,t,r.height-t*2,c);
  }
  void Corners(VertexHelper vh,Rect r,float t,float n,Color c,float grow){
   r=new Rect(r.x+grow,r.y+grow,r.width-grow*2,r.height-grow*2);
   Bar(vh,r.xMin,r.yMin,n,t,c);Bar(vh,r.xMin,r.yMin,t,n,c);
   Bar(vh,r.xMax-n,r.yMin,n,t,c);Bar(vh,r.xMax-t,r.yMin,t,n,c);
   Bar(vh,r.xMin,r.yMax-t,n,t,c);Bar(vh,r.xMin,r.yMax-n,t,n,c);
   Bar(vh,r.xMax-n,r.yMax-t,n,t,c);Bar(vh,r.xMax-t,r.yMax-n,t,n,c);
  }
  static void Bar(VertexHelper vh,float x,float y,float w,float h,Color c){if(w<=0||h<=0)return;int start=vh.currentVertCount;vh.AddVert(new Vector3(x,y),c,Vector2.zero);vh.AddVert(new Vector3(x,y+h),c,Vector2.zero);vh.AddVert(new Vector3(x+w,y+h),c,Vector2.zero);vh.AddVert(new Vector3(x+w,y),c,Vector2.zero);vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);}
 }
}
