using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 // Code-drawn ink glyph for an assigned action (no raster art, no baked text). Shapes live in a unit square [-1,1]
 // scaled to the smaller side of the rect and drawn in `color`. Never intercepts clicks.
 [RequireComponent(typeof(CanvasRenderer))]
 public sealed class ActionGlyph : MaskableGraphic {
  public enum Kind { None, Search, Move, Listen, Observe, Watch, Light, Rest, Work, Cook, Research }
  [Tooltip("그릴 행동: 수색(돋보기) · 이동(화살표) · 귀 대기(귀) · 관찰/망보기(눈) · 조명(등) · 휴식(달) · 작업(망치) · 조리(냄비) · 연구(플라스크)")] [SerializeField] Kind glyph=Kind.Search;
  [Tooltip("선 굵기(표시 크기 대비)")] [Range(.06f,.4f)] public float Stroke=.2f;
  [Tooltip("곡선을 몇 조각으로 그릴지")] [Range(8,64)] public int Smoothness=28;
  public Kind Glyph{get=>glyph;set{if(glyph==value)return;glyph=value;SetVerticesDirty();}}
  public override bool raycastTarget{get=>false;set{}}
  Vector2 center;float half;VertexHelper v;
  protected override void OnPopulateMesh(VertexHelper vh){
   vh.Clear();var r=rectTransform.rect;center=r.center;half=Mathf.Min(r.width,r.height)*.5f;v=vh;if(half<=0)return;float s=Stroke;
   switch(glyph){
    case Kind.Search: Ring(new Vector2(-.18f,.2f),.5f,s,0,360);Line(new Vector2(.16f,-.14f),new Vector2(.82f,-.8f),s*1.45f);break;
    case Kind.Move: Line(new Vector2(-.82f,0),new Vector2(.15f,0),s*1.35f);Tri(new Vector2(0,.56f),new Vector2(.9f,0),new Vector2(0,-.56f));break;
    case Kind.Listen:
     Ring(new Vector2(.06f,.2f),.62f,s,165,-100);Line(Polar(new Vector2(.06f,.2f),.62f-s*.5f,-100),new Vector2(-.28f,-.84f),s);
     Ring(new Vector2(.06f,.2f),.27f,s*.9f,160,-40);break;
    case Kind.Observe: Eye(s);Disc(Vector2.zero,.27f);break;
    case Kind.Watch: Eye(s);Disc(Vector2.zero,.18f);Line(new Vector2(-.5f,.52f),new Vector2(-.66f,.8f),s*.8f);Line(new Vector2(0,.6f),new Vector2(0,.9f),s*.8f);Line(new Vector2(.5f,.52f),new Vector2(.66f,.8f),s*.8f);break;
    case Kind.Light:
     Disc(new Vector2(0,.12f),.4f);Quad(new Vector2(-.2f,-.34f),new Vector2(.2f,-.34f),new Vector2(.2f,-.6f),new Vector2(-.2f,-.6f));Line(new Vector2(-.13f,-.72f),new Vector2(.13f,-.72f),s*.9f);
     foreach(float a in new[]{90f,30f,150f,-10f,190f})Line(Polar(new Vector2(0,.12f),.54f,a),Polar(new Vector2(0,.12f),.8f,a),s*.75f);break;
    case Kind.Rest: Crescent(new Vector2(-.08f,0),.78f,.5f,48);break;
    case Kind.Work:
     Line(new Vector2(-.7f,-.74f),new Vector2(.2f,.16f),s*1.35f);
     {var u=new Vector2(.7071f,.7071f);var w=new Vector2(-.7071f,.7071f);var h=new Vector2(.3f,.3f);Quad(h+w*.5f+u*.17f,h-w*.5f+u*.17f,h-w*.5f-u*.17f,h+w*.5f-u*.17f);}
     break;
    case Kind.Cook:
     Quad(new Vector2(-.6f,.14f),new Vector2(.6f,.14f),new Vector2(.52f,-.6f),new Vector2(-.52f,-.6f));Quad(new Vector2(-.78f,.3f),new Vector2(.78f,.3f),new Vector2(.78f,.14f),new Vector2(-.78f,.14f));
     Quad(new Vector2(-.95f,.04f),new Vector2(-.6f,.04f),new Vector2(-.6f,-.1f),new Vector2(-.95f,-.1f));Quad(new Vector2(.6f,.04f),new Vector2(.95f,.04f),new Vector2(.95f,-.1f),new Vector2(.6f,-.1f));
     Line(new Vector2(-.22f,.44f),new Vector2(-.12f,.8f),s*.75f);Line(new Vector2(.18f,.44f),new Vector2(.28f,.8f),s*.75f);break;
    case Kind.Research:
     Quad(new Vector2(-.17f,.8f),new Vector2(.17f,.8f),new Vector2(.17f,.3f),new Vector2(-.17f,.3f));Quad(new Vector2(-.3f,.92f),new Vector2(.3f,.92f),new Vector2(.3f,.78f),new Vector2(-.3f,.78f));
     Quad(new Vector2(-.17f,.32f),new Vector2(.17f,.32f),new Vector2(.72f,-.8f),new Vector2(-.72f,-.8f));break;
   }
   v=null;
  }
  Vector3 P(Vector2 unit)=>center+unit*half;
  static Vector2 Polar(Vector2 c,float r,float degrees){float a=degrees*Mathf.Deg2Rad;return c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r;}
  void Tri(Vector2 a,Vector2 b,Vector2 c){int i=v.currentVertCount;v.AddVert(P(a),color,Vector2.zero);v.AddVert(P(b),color,Vector2.zero);v.AddVert(P(c),color,Vector2.zero);v.AddTriangle(i,i+1,i+2);}
  void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d){int i=v.currentVertCount;v.AddVert(P(a),color,Vector2.zero);v.AddVert(P(b),color,Vector2.zero);v.AddVert(P(c),color,Vector2.zero);v.AddVert(P(d),color,Vector2.zero);v.AddTriangle(i,i+1,i+2);v.AddTriangle(i,i+2,i+3);}
  void Line(Vector2 a,Vector2 b,float width){var d=b-a;if(d.sqrMagnitude<1e-6f)return;var n=new Vector2(-d.y,d.x).normalized*width*.5f;Quad(a+n,b+n,b-n,a-n);}
  // Band between radius r-w/2 and r+w/2, swept from `from` to `to` degrees (either direction).
  void Ring(Vector2 c,float r,float w,float from,float to){
   int steps=Mathf.Max(2,Mathf.CeilToInt(Mathf.Abs(to-from)/360f*Smoothness));int start=v.currentVertCount;
   for(int k=0;k<=steps;k++){float a=Mathf.Lerp(from,to,(float)k/steps);v.AddVert(P(Polar(c,r+w*.5f,a)),color,Vector2.zero);v.AddVert(P(Polar(c,Mathf.Max(0,r-w*.5f),a)),color,Vector2.zero);}
   for(int k=0;k<steps;k++){int o=start+k*2;v.AddTriangle(o,o+2,o+1);v.AddTriangle(o+1,o+2,o+3);}
  }
  void Disc(Vector2 c,float r)=>Ring(c,r*.5f,r,0,360);
  // Almond outline: two sine arches (bands of vertical thickness w) meeting at pointed corners.
  void Eye(float w){
   const int n=16;
   for(int lid=-1;lid<=1;lid+=2){
    int start=v.currentVertCount;
    for(int k=0;k<=n;k++){float t=(float)k/n,x=-.9f+1.8f*t,y=lid*.5f*Mathf.Sin(t*Mathf.PI);v.AddVert(P(new Vector2(x,y+w*.5f)),color,Vector2.zero);v.AddVert(P(new Vector2(x,y-w*.5f)),color,Vector2.zero);}
    for(int k=0;k<n;k++){int o=start+k*2;v.AddTriangle(o,o+2,o+1);v.AddTriangle(o+1,o+2,o+3);}
   }
  }
  // Moon: the outer circle minus a circle shifted right; both arcs meet at ±edge degrees on the outer circle.
  void Crescent(Vector2 c,float r,float shift,float edge){
   var tip=Polar(c,r,edge);var c2=c+new Vector2(shift,0);float r2=(tip-c2).magnitude;float inner=Mathf.Atan2(tip.y-c2.y,tip.x-c2.x)*Mathf.Rad2Deg;
   int steps=Smoothness;int start=v.currentVertCount;
   for(int k=0;k<=steps;k++){float t=(float)k/steps;v.AddVert(P(Polar(c,r,Mathf.Lerp(edge,360-edge,t))),color,Vector2.zero);v.AddVert(P(Polar(c2,r2,Mathf.Lerp(inner,360-inner,t))),color,Vector2.zero);}
   for(int k=0;k<steps;k++){int o=start+k*2;v.AddTriangle(o,o+2,o+1);v.AddTriangle(o+1,o+2,o+3);}
  }
 }
}
