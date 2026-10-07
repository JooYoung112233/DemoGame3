using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Demo6.Game
{
 /// <summary>Runtime contact fields from the approved review. Footprints are authored in the existing prop's local world units; source sprites and character rigs remain untouched.</summary>
 public sealed class GroundContactV043:MonoBehaviour
 {
  public bool wholeScene;
  GameObject generated;Material material;SpriteRenderer characterShadow;Sprite previousCharacterShadow,characterContact;
  readonly List<Object> owned=new List<Object>();
  public int Count=>generated?generated.GetComponentsInChildren<SpriteRenderer>(true).Length:0;
  void Start(){Build();}
  public void SetVisible(bool value){if(generated)generated.SetActive(value);if(characterShadow)characterShadow.sprite=value?characterContact:previousCharacterShadow;}
  struct Segment {public Vector2 a,b;public float radius;public Segment(float ax,float ay,float bx,float by,float r){a=new Vector2(ax,ay);b=new Vector2(bx,by);radius=r;}}
  static float Distance(Vector2 p,Segment s){var ab=s.b-s.a;return Vector2.Distance(p,s.a+ab*Mathf.Clamp01(Vector2.Dot(p-s.a,ab)/Mathf.Max(.00001f,ab.sqrMagnitude)))-s.radius;}
  static float PolyDistance(Vector2 p,Vector2[] poly){bool inside=false;float d=100;for(int i=0,j=poly.Length-1;i<poly.Length;j=i++){var a=poly[j];var b=poly[i];d=Mathf.Min(d,Distance(p,new Segment(a.x,a.y,b.x,b.y,0)));if((a.y>p.y)!=(b.y>p.y)&&p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;}return inside?-d:d;}
  static Vector2[] P(params float[] values){var p=new Vector2[values.Length/2];for(int i=0;i<p.Length;i++)p[i]=new Vector2(values[i*2],values[i*2+1]);return p;}
  public void Build()
  {
   if(generated)return;
   var all=(wholeScene?gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SpriteRenderer>(true)):GetComponentsInChildren<SpriteRenderer>(true)).ToArray();
   generated=new GameObject("V040 object contact fields");generated.transform.SetParent(transform,false);
   material=new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")){name="V040 contact field",hideFlags=HideFlags.DontSave};owned.Add(material);
   var lights=(wholeScene?gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light2D>(true)):GetComponentsInChildren<Light2D>(true)).Where(l=>l.lightType==Light2D.LightType.Point).ToArray();
   foreach(var s in all)
   {
    var role=ApprovedWorldArtV043.Role(s);
    if(!new[]{"winch","broken_timber","chain","rubble","pillar_a","pillar_b","lantern"}.Contains(role))continue;
    var polygons=new List<Vector2[]>();var lines=new List<Segment>();float soft=.10f,strength=.28f,height=.15f;bool wall=role=="lantern";
    switch(role){
     case "winch": polygons.Add(P(-.70f,-.41f,.22f,-.41f,.27f,-.55f,.12f,-.60f,-.65f,-.59f,-.76f,-.53f));soft=.12f;strength=.34f;height=.34f;break;
     case "broken_timber": lines.Add(new Segment(-.52f,-.13f,.52f,.18f,.063f));lines.Add(new Segment(-.36f,.22f,.26f,-.23f,.06f));soft=.075f;strength=.25f;height=.06f;break;
     case "chain": var path=P(-.45f,.02f,-.32f,-.13f,-.15f,-.16f,.02f,-.10f,.22f,.015f,.4f,.18f);for(int i=1;i<path.Length;i++)lines.Add(new Segment(path[i-1].x,path[i-1].y,path[i].x,path[i].y,.027f));soft=.055f;strength=.24f;height=.025f;break;
     case "rubble": polygons.Add(P(-.44f,-.18f,-.27f,-.33f,-.03f,-.43f,.29f,-.40f,.48f,-.23f,.32f,-.07f,-.27f,-.07f));soft=.11f;strength=.30f;height=.14f;break;
     case "pillar_a": polygons.Add(P(-.53f,-.23f,-.50f,-.45f,-.20f,-.62f,.16f,-.61f,.48f,-.41f,.51f,-.17f,.23f,-.03f,-.24f,-.04f));soft=.14f;strength=.34f;height=.30f;break;
     case "pillar_b": polygons.Add(P(-.53f,-.23f,-.37f,-.52f,-.12f,-.60f,.23f,-.57f,.53f,-.32f,.42f,-.09f,.04f,-.04f,-.31f,-.08f));soft=.14f;strength=.34f;height=.30f;break;
     case "lantern": polygons.Add(P(-.075f,.33f,.075f,.33f,.075f,.445f,-.075f,.445f));soft=.028f;strength=.18f;height=0;break;
    }
    // Cast only a short diffuse extension where an existing point light reaches this footprint. Ambient contact stays anchored.
    Vector2 away=Vector2.zero;float best=0;
    if(!wall)foreach(var light in lights){float distance=Vector2.Distance(light.transform.position,s.transform.position);float reach=Mathf.Clamp01(1-distance/Mathf.Max(.01f,light.pointLightOuterRadius));float weight=reach*reach*light.intensity;if(weight>best){best=weight;away=(Vector2)s.transform.InverseTransformVector((s.transform.position-light.transform.position).normalized)*height*reach;}}
    float extent=1.2f;int side=256;var texture=new Texture2D(side,side,TextureFormat.RGBA32,false){name=role+" procedural contact field",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.DontSave};var pixels=new Color32[side*side];
    System.Func<Vector2,float> field=p=>{float d=100;foreach(var poly in polygons)d=Mathf.Min(d,PolyDistance(p,poly));foreach(var line in lines)d=Mathf.Min(d,Distance(p,line));return d;};
    for(int y=0;y<side;y++)for(int x=0;x<side;x++){
     var p=new Vector2(((x+.5f)/side*2-1)*extent,((y+.5f)/side*2-1)*extent);float d=field(p),cast=field(p-away);float tight=.50f*Mathf.Exp(-Mathf.Pow(Mathf.Max(0,d)/.027f,2));float ambient=strength*Mathf.Exp(-Mathf.Pow(Mathf.Max(0,d)/soft,2));float projected=best>0?.14f*Mathf.Clamp01(best/3)*Mathf.Exp(-Mathf.Pow(Mathf.Max(0,cast)/(soft*1.15f),2)):0;float alpha=1-(1-tight)*(1-ambient)*(1-projected);pixels[y*side+x]=new Color32(0,0,0,(byte)Mathf.RoundToInt(alpha*255));
    }
    texture.SetPixels32(pixels);texture.Apply(false,true);owned.Add(texture);var sprite=Sprite.Create(texture,new Rect(0,0,side,side),new Vector2(.5f,.5f),side/(extent*2),0,SpriteMeshType.FullRect);sprite.name=role+" footprint";owned.Add(sprite);
    var g=new GameObject(role+" grounded shadow");g.transform.SetParent(generated.transform,false);g.transform.position=s.transform.position;g.transform.rotation=s.transform.rotation;g.transform.localScale=s.transform.localScale;var r=g.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sharedMaterial=material;r.sortingLayerID=s.sortingLayerID;r.sortingOrder=wall?38:-970;
   }
   // Reuse the player's existing single shadow renderer. Do not add a second oval or alter the approved character rig.
   if(PlayerController.Instance){characterShadow=PlayerController.Instance.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(s=>s.name=="Shadow");if(characterShadow){previousCharacterShadow=characterShadow.sprite;int side=192;float extent=.9f;var texture=new Texture2D(side,side,TextureFormat.RGBA32,false){name="Player ground contact feather",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.DontSave};var pixels=new Color32[side*side];for(int y=0;y<side;y++)for(int x=0;x<side;x++){var p=new Vector2(((x+.5f)/side*2-1)*extent,((y+.5f)/side*2-1)*extent);float d=(Mathf.Sqrt(p.x*p.x/(.49f*.49f)+p.y*p.y/(.40f*.40f))-1)*.40f;float a=.90f*Mathf.Exp(-Mathf.Pow(Mathf.Max(0,d)/.16f,2));pixels[y*side+x]=new Color32(0,0,0,(byte)Mathf.RoundToInt(a*255));}texture.SetPixels32(pixels);texture.Apply(false,true);owned.Add(texture);characterContact=Sprite.Create(texture,new Rect(0,0,side,side),new Vector2(.5f,.5f),side/(extent*2),0,SpriteMeshType.FullRect);characterContact.name="Player grounded feather (same renderer)";owned.Add(characterContact);characterShadow.sprite=characterContact;}}
  }
  void OnDestroy(){if(characterShadow&&previousCharacterShadow)characterShadow.sprite=previousCharacterShadow;if(generated)Destroy(generated);foreach(var asset in owned)if(asset)Destroy(asset);owned.Clear();}
 }
}
