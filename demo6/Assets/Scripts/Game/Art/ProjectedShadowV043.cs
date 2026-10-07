using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Demo6.Game
{
 /// <summary>Runtime 2.5D floor projection adapted from the approved review. Heights are explicit proxies, not 3D geometry or URP shadow-caster heights.</summary>
 [DefaultExecutionOrder(12000)]
 public sealed class ProjectedShadowV043 : MonoBehaviour
 {
  public bool wholeScene;
  public const float MaxLength=3.2f;
  public const float TorsoHeight=1.05f, HeadHeight=1.75f;
  sealed class Shape { public Transform root; public SpriteRenderer renderer,head,torso,right,left; public string name; public bool player; public float height; public Vector2 center,radius; public readonly List<Cast> casts=new List<Cast>(); }
  sealed class Cast { public Light2D light; public Mesh mesh; public MeshRenderer renderer; public float alpha; }
  readonly List<Shape> shapes=new List<Shape>();
  readonly List<Object> owned=new List<Object>();
  readonly List<Vector2> points=new List<Vector2>();
  GameObject generated; Material material; Light2D[] lights;
  bool visible=true;
  public int ShapeCount=>shapes.Count;
  public void SetVisible(bool value){visible=value;if(generated)generated.SetActive(value);}
  void Start(){Build();}
  void LateUpdate(){Build();Tick(false);}
  public void Build()
  {
   if(generated)return;
   var all=(wholeScene?gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SpriteRenderer>(true)):GetComponentsInChildren<SpriteRenderer>(true)).ToArray();
   lights=(wholeScene?gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light2D>(true)):GetComponentsInChildren<Light2D>(true)).Where(l=>l.lightType==Light2D.LightType.Point).ToArray();
   material=new Material(Shader.Find("Demo6/ReviewProjectedShadowV041")){hideFlags=HideFlags.DontSave};owned.Add(material);
   generated=new GameObject("V041 height projected floor shadows");generated.transform.SetParent(transform,false);generated.SetActive(visible);
   foreach(var r in all){var role=ApprovedWorldArtV043.Role(r);var s=new Shape{root=r.transform,renderer=r,name=role};switch(role){
    case "winch":s.height=1.25f;s.center=new Vector2(-.24f,-.50f);s.radius=new Vector2(.49f,.10f);break;
    case "pillar_a":case "pillar_b":s.height=1.65f;s.center=new Vector2(0,-.34f);s.radius=new Vector2(.47f,.22f);break;
    case "rubble":s.height=.35f;s.center=new Vector2(0,-.25f);s.radius=new Vector2(.42f,.15f);break;
    case "broken_timber":s.height=.12f;s.center=Vector2.zero;s.radius=new Vector2(.52f,.13f);break;
    case "chain":s.height=.035f;s.center=Vector2.zero;s.radius=new Vector2(.44f,.06f);break;
    default:continue;
   }Add(s);}
   if(PlayerController.Instance){var rs=PlayerController.Instance.GetComponentsInChildren<SpriteRenderer>(true);var shadow=rs.FirstOrDefault(r=>r.name=="Shadow");Add(new Shape{root=shadow?shadow.transform:PlayerController.Instance.transform,name="player",player=true,height=HeadHeight,torso=rs.FirstOrDefault(r=>r.name=="Body"),head=rs.FirstOrDefault(r=>r.name=="CuteHead"),right=rs.FirstOrDefault(r=>r.name=="CuteShoulderR"),left=rs.FirstOrDefault(r=>r.name=="CuteShoulderL")});}
   Tick(true);
  }
  void Add(Shape s){shapes.Add(s);for(int slot=0;slot<2;slot++){Light2D light=null;var g=new GameObject(s.name+" projected light slot "+slot);g.transform.SetParent(generated.transform,false);var mesh=new Mesh{name=g.name,hideFlags=HideFlags.DontSave};mesh.MarkDynamic();owned.Add(mesh);g.AddComponent<MeshFilter>().sharedMesh=mesh;var r=g.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.sortingLayerID=s.renderer?s.renderer.sortingLayerID:0;r.sortingOrder=-990;r.receiveShadows=false;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;s.casts.Add(new Cast{light=light,mesh=mesh,renderer=r});}}
  public static Vector2 Project(Vector2 p,float height,Vector2 light,float lightHeight)
  {
   // Keep the ray denominator bounded even for an unusually low light, and cap the cast continuously.
   float h=Mathf.Min(Mathf.Max(0,height),Mathf.Max(.1f,lightHeight)*.8f);
   return p+Vector2.ClampMagnitude((p-light)*(h/Mathf.Max(.35f,lightHeight-h)),MaxLength);
  }
  public static float Weight(Light2D l,Vector2 p){if(!l||!l.isActiveAndEnabled||l.intensity<=0)return 0;float range=Mathf.Max(.01f,l.pointLightOuterRadius);float t=Mathf.Clamp01(1-Vector2.Distance(p,l.transform.position)/range);return l.intensity*t*t;}
  public void Tick(bool immediate)
  {
   if(!generated)return;
   foreach(var s in shapes){if(!s.root)continue;Vector2 origin=s.root.TransformPoint(s.center);float sum=0,best=0,second=0;Light2D firstLight=null,secondLight=null;foreach(var light in lights){float w=Weight(light,origin);sum+=w;if(w>best){second=best;secondLight=firstLight;best=w;firstLight=light;}else if(w>second){second=w;secondLight=light;}}s.casts[0].light=firstLight;s.casts[1].light=secondLight;
    foreach(var c in s.casts){float w=Weight(c.light,origin);float target=w>0&&w>=second?.43f*(1-Mathf.Exp(-sum*.8f))*w/Mathf.Max(.001f,sum):0;float t=immediate?1:1-Mathf.Exp(-Mathf.Max(0,Time.unscaledDeltaTime)*14);c.alpha=Mathf.Lerp(c.alpha,target,t);c.renderer.enabled=c.light&&c.alpha>.001f&&s.root.gameObject.activeInHierarchy&&(!s.renderer||s.renderer.enabled);if(!c.renderer.enabled)continue;points.Clear();var lp=(Vector2)c.light.transform.position;float lh=Mathf.Max(.4f,c.light.normalMapDistance);
     if(s.player){Ellipse(s.root,Vector2.zero,new Vector2(.32f,.26f),0,lp,lh);Part(s.torso,new Vector2(.35f,.39f),TorsoHeight,lp,lh);Part(s.right,new Vector2(.19f,.18f),1.25f,lp,lh);Part(s.left,new Vector2(.19f,.18f),1.25f,lp,lh);Part(s.head,new Vector2(.28f,.30f),HeadHeight,lp,lh);}
     else {Ellipse(s.root,s.center,s.radius,0,lp,lh);Ellipse(s.root,s.center,s.radius*.85f,s.height,lp,lh);}
     var hull=Hull(points);Fill(c.mesh,hull,c.alpha,.045f+.055f*s.height);
    }
   }
  }
  void Part(SpriteRenderer r,Vector2 radii,float h,Vector2 lp,float lh){var art=TopDownView.PlayerRig?.FirstAttackArt;if(r&&art!=null&&art.Active){var visible=art.ShadowPart(r.name);if(visible)r=visible;}if(r&&r.enabled&&r.gameObject.activeInHierarchy)Ellipse(r.transform,r.sprite?r.sprite.bounds.center:Vector3.zero,radii,h,lp,lh);}
  void Ellipse(Transform tr,Vector2 center,Vector2 radii,float height,Vector2 light,float lh){for(int i=0;i<12;i++){float a=i*Mathf.PI/6;Vector2 p=tr.TransformPoint(center+new Vector2(Mathf.Cos(a)*radii.x,Mathf.Sin(a)*radii.y));points.Add(Project(p,height,light,lh));}}
  static float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
  static List<Vector2> Hull(List<Vector2> ps){var p=ps.Distinct().OrderBy(v=>v.x).ThenBy(v=>v.y).ToArray();var h=new List<Vector2>();foreach(var v in p){while(h.Count>=2&&Cross(h[h.Count-2],h[h.Count-1],v)<=0)h.RemoveAt(h.Count-1);h.Add(v);}int lower=h.Count;for(int i=p.Length-2;i>=0;i--){var v=p[i];while(h.Count>lower&&Cross(h[h.Count-2],h[h.Count-1],v)<=0)h.RemoveAt(h.Count-1);h.Add(v);}if(h.Count>1)h.RemoveAt(h.Count-1);return h;}
  void Fill(Mesh mesh,List<Vector2> h,float alpha,float feather)
  {
   mesh.Clear();int n=h.Count;if(n<3)return;Vector2 center=Vector2.zero;foreach(var p in h)center+=p;center/=n;var v=new Vector3[1+n*2];var col=new Color[1+n*2];var tri=new int[n*9];v[0]=generated.transform.InverseTransformPoint(center);col[0]=new Color(0,0,0,alpha);
   for(int i=0;i<n;i++){int j=(i+1)%n;v[1+i]=generated.transform.InverseTransformPoint(h[i]);var a=(h[i]-h[(i+n-1)%n]).normalized;var b=(h[j]-h[i]).normalized;Vector2 outward=(new Vector2(a.y,-a.x)+new Vector2(b.y,-b.x)).normalized;v[1+n+i]=generated.transform.InverseTransformPoint(h[i]+outward*feather);col[1+i]=col[0];col[1+n+i]=Color.clear;int t=i*9;tri[t]=0;tri[t+1]=1+i;tri[t+2]=1+j;tri[t+3]=1+i;tri[t+4]=1+n+i;tri[t+5]=1+n+j;tri[t+6]=1+i;tri[t+7]=1+n+j;tri[t+8]=1+j;}
   mesh.vertices=v;mesh.colors=col;mesh.triangles=tri;mesh.RecalculateBounds();
  }
  public object[] Snapshot()=>shapes.Select(s=>new{name=s.name,height=s.height,basePosition=new[]{s.root.position.x,s.root.position.y},casts=s.casts.Select(c=>new{light=c.light?c.light.name:null,alpha=c.alpha,weight=Weight(c.light,s.root.TransformPoint(s.center)),effectiveLightHeight=c.light?c.light.normalMapDistance:0,vertexCount=c.mesh.vertexCount,boundsCenter=new[]{c.mesh.bounds.center.x,c.mesh.bounds.center.y}}).ToArray()}).Cast<object>().ToArray();
  void OnDestroy(){if(generated)Destroy(generated);foreach(var o in owned)if(o)Destroy(o);owned.Clear();}
 }
}
