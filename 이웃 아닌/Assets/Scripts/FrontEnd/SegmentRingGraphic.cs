using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 // Code-drawn progress ring, filled clockwise from StartDegrees: done / this turn (Next) / rest, one gap per segment.
 // Done and Next may be fractional so callers can animate between whole steps. Segments <= 0 draws one plain ring in `color`
 // (a disc when Thickness >= radius), which the assignment bubble also uses for its paper disc and badge. Never intercepts clicks.
 [RequireComponent(typeof(CanvasRenderer))]
 public sealed class SegmentRingGraphic : MaskableGraphic {
  [Tooltip("칸 수. 0 이하면 칸 없이 한 줄 고리(두께가 반지름 이상이면 원판)로 그립니다.")] public int Segments=3;
  [Tooltip("채운 칸 수(소수 가능 · 애니메이션용)")] public float Done;
  [Tooltip("채운 칸 바로 뒤에 '이번 턴에 찰' 칸 수(소수 가능)")] public float Next;
  [Tooltip("채운 칸 색")] public Color DoneColor=new Color(.93f,.6f,.18f,1);
  [Tooltip("이번 턴에 찰 칸 색")] public Color NextColor=new Color(1,.87f,.4f,1);
  [Tooltip("남은 칸 색")] public Color RestColor=new Color(.11f,.12f,.12f,.92f);
  [Tooltip("칸 사이 간격(도)")] [Range(0,30)] public float GapDegrees=8;
  [Tooltip("고리 두께(px). 반지름 이상이면 원판")] [Min(.5f)] public float Thickness=8;
  [Tooltip("채우기 시작 각도(도 · 12시 = 90). 시계 방향으로 찹니다.")] public float StartDegrees=90;
  [Tooltip("칸이 이 수보다 많으면 간격 없이 이어진 고리로 그립니다.")] [Min(1)] public int MaxSegments=24;
  [Tooltip("한 바퀴를 몇 조각으로 그릴지(부드러움)")] [Range(12,180)] public int Smoothness=96;
  public override bool raycastTarget{get=>false;set{}}
  public void Set(int segments,float done,float next){
   if(Segments==segments&&Mathf.Approximately(Done,done)&&Mathf.Approximately(Next,next))return;
   Segments=segments;Done=done;Next=next;SetVerticesDirty();
  }
  protected override void OnPopulateMesh(VertexHelper vh){
   vh.Clear();var r=rectTransform.rect;var c=r.center;float outer=Mathf.Min(r.width,r.height)*.5f,inner=Mathf.Max(0,outer-Thickness);
   if(outer<=0)return;
   if(Segments<=0){Arc(vh,c,inner,outer,0,360,color);return;}
   int n=Segments;float d=Mathf.Clamp(Done,0,n),e=Mathf.Clamp(Done+Mathf.Max(0,Next),0,n);
   Color done=DoneColor*color,next=NextColor*color,rest=RestColor*color;
   if(n>MaxSegments||n==1&&GapDegrees<=0){
    // Too many steps to read as cells: one continuous ring split by the same three regions.
    Span(vh,c,inner,outer,0,d,n,0,360,done);Span(vh,c,inner,outer,d,e,n,0,360,next);Span(vh,c,inner,outer,e,n,n,0,360,rest);return;
   }
   float span=360f/n,gap=Mathf.Min(GapDegrees,span*.6f);
   for(int i=0;i<n;i++){
    float a0=i*span+gap*.5f,a1=(i+1)*span-gap*.5f;
    Piece(vh,c,inner,outer,i,0,d,a0,a1,done);Piece(vh,c,inner,outer,i,d,e,a0,a1,next);Piece(vh,c,inner,outer,i,e,n,a0,a1,rest);
   }
  }
  // Part of segment i covered by the unit range [lo,hi), mapped into the segment's angle range [a0,a1] (degrees clockwise from the start).
  void Piece(VertexHelper vh,Vector2 c,float inner,float outer,int i,float lo,float hi,float a0,float a1,Color col){
   float s=Mathf.Max(lo,i),t=Mathf.Min(hi,i+1);if(t-s<=.0001f)return;
   Arc(vh,c,inner,outer,a0+(s-i)*(a1-a0),a0+(t-i)*(a1-a0),col);
  }
  void Span(VertexHelper vh,Vector2 c,float inner,float outer,float lo,float hi,int n,float a0,float a1,Color col){
   if(hi-lo<=.0001f)return;Arc(vh,c,inner,outer,a0+lo/n*(a1-a0),a0+hi/n*(a1-a0),col);
  }
  // Clockwise sweep from `from` to `to` degrees measured from StartDegrees.
  void Arc(VertexHelper vh,Vector2 c,float inner,float outer,float from,float to,Color col){
   int steps=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(to-from)/360f*Smoothness));int start=vh.currentVertCount;
   for(int k=0;k<=steps;k++){
    float a=(StartDegrees-Mathf.Lerp(from,to,(float)k/steps))*Mathf.Deg2Rad;var dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
    vh.AddVert(c+dir*outer,col,Vector2.zero);vh.AddVert(c+dir*inner,col,Vector2.zero);
   }
   for(int k=0;k<steps;k++){int o=start+k*2;vh.AddTriangle(o,o+2,o+1);vh.AddTriangle(o+1,o+2,o+3);}
  }
 }
}
