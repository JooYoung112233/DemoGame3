using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections.Generic;using UnityEngine;using UnityEditor;using Demo6.Game;using Demo6.Core.Loot;using C=Demo6.Game.TopDownCanvas;
public static class HeroV4Build {
 const string Root="아트/정수리/검사-v4", Game="아트/정수리/검사-v4/runtime-PNG";
 static readonly string[] Names={"leather","chain","plate"},WeightNames={"가죽","사슬","판금"};
 static float Quad(Vector2 p,Vector2 a,Vector2 b,Vector2 c,Vector2 d)=>Mathf.Min(C.Triangle(p,a,b,c),C.Triangle(p,a,c,d));
 static readonly Color Outline=new Color32(16,15,14,255),Bone=new Color32(169,153,122,255),Iron=new Color32(101,105,104,255);
 static readonly List<object> Manifest=new List<object>();
 static int Index(ArmorWeight w)=>w==ArmorWeight.Heavy?2:w==ArmorWeight.Medium?1:0;
 static Color WeightColor(ArmorWeight w)=>Index(w)==2?new Color32(105,110,112,255):Index(w)==1?new Color32(79,84,88,255):new Color32(104,75,49,255);
 static float Foot(Vector2 p)=>C.Capsule(p,new Vector2(-.038f,0),new Vector2(.034f,0),.04f,.047f);
 sealed class Painter {
  public readonly int W,H;readonly float factor;readonly string id;readonly C all;readonly Vector2 pivot;C layer;string layerName;readonly List<string> layers=new List<string>();
  public Painter(int w,int h,float f,string name,Vector2? origin=null){W=w;H=h;factor=f;id=name;pivot=origin??new Vector2(.5f,.5f);all=Canvas();Layer("01-material");}
  C Canvas()=>new C(-W*pivot.x/512f,W*(1-pivot.x)/512f,-H*pivot.y/512f,H*(1-pivot.y)/512f,512);
  public void Layer(string name){if(layer!=null&&draws>0){Save(layer,Root+"/layers/"+id+"/"+layerName+".png");layers.Add(layerName);}layerName=name;layer=Canvas();draws=0;}
  int draws;
  public void Draw(C.Shape shape,C.Paint paint,float outline=0,Color color=default){C.Shape s=p=>shape(p/factor)*factor;C.Paint t=(p,d)=>paint(p/factor,d/factor);all.Draw(s,t,outline*factor,color);layer.Draw(s,t,outline*factor,color);draws++;}
  public void Draw(C.Shape shape,Color paint,float outline=0,Color color=default)=>Draw(shape,(p,d)=>paint,outline,color);
  public Sprite ToSprite(string name,Vector2 pivot){Layer("finished");Save(all,Root+"/PNG/"+id+".png");SaveHalf(all,Game+"/"+id+".png");Manifest.Add(new{id,width=W,height=H,masterPPU=512,runtimePPU=256,pivot=new[]{this.pivot.x,this.pivot.y},layers=layers.ToArray()});return null;}
 }
 static Texture2D Texture(C canvas){var px=(Color[])typeof(C).GetField("_px",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(canvas);var t=new Texture2D(canvas.Width,canvas.Height,TextureFormat.RGBA32,false);t.SetPixels(px);t.Apply();return t;}
 static void Save(C c,string path){Directory.CreateDirectory(Path.GetDirectoryName(path));var t=Texture(c);File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);}
 static void SaveHalf(C c,string path){Directory.CreateDirectory(Path.GetDirectoryName(path));var t=Texture(c);int w=t.width/2,h=t.height/2;var a=t.GetPixels();var b=new Color[w*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++){Color sum=Color.clear;float alpha=0;for(int j=0;j<2;j++)for(int i=0;i<2;i++){var p=a[(2*y+j)*t.width+2*x+i];sum.r+=p.r*p.a;sum.g+=p.g*p.a;sum.b+=p.b*p.a;alpha+=p.a;}b[y*w+x]=alpha>0?new Color(sum.r/alpha,sum.g/alpha,sum.b/alpha,alpha/4):Color.clear;}var o=new Texture2D(w,h,TextureFormat.RGBA32,false);o.SetPixels(b);o.Apply();File.WriteAllBytes(path,o.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);UnityEngine.Object.DestroyImmediate(o);}
 static void CloakDetail(Painter cv){
  for(int s=-1;s<=1;s+=2){float side=s;cv.Draw(p=>C.Curve(p,new Vector2(-.39f,.19f*side),new Vector2(-.28f,.33f*side),new Vector2(-.13f,.39f*side),.0045f,.004f),new Color32(94,86,70,255));
   for(int i=0;i<5;i++){float x=-.39f+i*.047f,y=(.18f+i*.041f)*side;cv.Draw(p=>C.Capsule(p,new Vector2(x,y),new Vector2(x+.011f,y-.009f*side),.0025f),new Color32(128,112,88,255));}
   cv.Draw(p=>C.Curve(p,new Vector2(-.12f,.08f*side),new Vector2(-.24f,.18f*side),new Vector2(-.43f,.14f*side),.007f,.003f),new Color32(24,24,23,255));
  }
 }
 static void ArmorDetail(Painter cv,int kind,Color armor){
  // A restrained right shoulder steel fitting preserves the original swordsman's identity.
  if(kind==0){cv.Draw(p=>C.Box(p,new Vector2(.012f,-.302f),.077f,.047f,.02f,-12),(p,d)=>C.Shade(Iron,C.Dome(d,.042f)*C.Rim(d,.008f,.35f)),.008f,Outline);
   for(int i=0;i<4;i++){float y=.26f+i*.025f;cv.Draw(p=>C.Capsule(p,new Vector2(-.044f,y),new Vector2(-.025f,y+.003f),.0028f),Bone);}}
  for(int side=-1;side<=1;side+=2){float sy=kind==2?.355f:kind==1?.33f:.3f;var at=new Vector2(.01f,sy*side);cv.Draw(p=>C.Capsule(p,at+new Vector2(-.04f,.012f),at+new Vector2(-.006f,.023f),.0027f),new Color32(151,145,125,255));}
 }
 static void HelmDetail(Painter cv,int kind){
  if(kind==0){for(int i=0;i<10;i++){float x=-.245f+i*.038f;cv.Draw(p=>C.Capsule(p,new Vector2(x,-.008f),new Vector2(x+.008f,.008f),.0025f),Bone);}}
  else {cv.Draw(p=>C.Capsule(p,new Vector2(-.08f,-.1f),new Vector2(-.04f,-.08f),.0026f),new Color32(158,159,149,255));cv.Draw(p=>C.Capsule(p,new Vector2(.04f,.11f),new Vector2(.07f,.085f),.0026f),new Color32(151,155,151,255));}
 }
 static void BuildSleeve(ArmorWeight w){int k=Index(w);var cv=new Painter(224,64,1,"sleeve_"+Names[k]);var tone=k==0?new Color32(123,97,67,255):k==1?new Color32(108,117,116,255):new Color32(133,140,139,255);cv.Draw(p=>C.Box(p,Vector2.zero,.20f,.044f,.014f),(p,d)=>C.Shade(tone,.7f+.38f*C.Dome(d,.038f)),.005f,Outline);cv.Layer("02-bracer-and-cuff");cv.Draw(p=>C.Box(p,new Vector2(.085f,0),.079f,.046f,.008f),C.Shade(tone,1.13f),.005f,Outline);cv.Draw(p=>C.Box(p,new Vector2(.16f,0),.009f,.047f,.001f),new Color32(172,152,112,255));cv.ToSprite("sleeve",Vector2.zero);}
 static void BuildSword(){var cv=new Painter(608,144,1,"longsword",new Vector2(84f/608f,.5f));
 cv.Layer("01-grip-and-pommel");cv.Draw(p=>C.Box(p,new Vector2(-.02f,0),.09f,.024f,.01f),new Color32(75,52,34,255),.007f,Outline);cv.Draw(p=>C.Circle(p,new Vector2(-.122f,0),.027f),new Color32(138,130,105,255),.006f,Outline);
 cv.Layer("02-blade-planes");cv.Draw(p=>Mathf.Min(C.Box(p,new Vector2(.49f,0),.39f,.042f),C.Triangle(p,new Vector2(.88f,-.042f),new Vector2(.985f,0),new Vector2(.88f,.042f))),(p,d)=>p.y>0?new Color32(197,205,198,255):new Color32(119,133,137,255),.005f,Outline);
 cv.Layer("03-fuller-and-edge");cv.Draw(p=>C.Capsule(p,new Vector2(.18f,-.003f),new Vector2(.82f,-.003f),.004f),new Color32(65,81,85,255));cv.Draw(p=>C.Capsule(p,new Vector2(.12f,.037f),new Vector2(.875f,.037f),.004f),new Color32(222,220,199,255));
 cv.Layer("04-crossguard");cv.Draw(p=>C.Box(p,new Vector2(.085f,0),.02f,.106f,.008f),(p,d)=>C.Shade(new Color32(153,146,120,255),.83f+.25f*C.Dome(d,.018f)),.006f,Outline);cv.ToSprite("sword",Vector2.zero);}

 public static object BuildSources(){Manifest.Clear();foreach(var w in new[]{ArmorWeight.Light,ArmorWeight.Medium,ArmorWeight.Heavy}){BuildPlayer(WeightColor(w),w);BuildHelm(WeightColor(w),w);BuildFist(WeightColor(w),w,true);BuildFist(WeightColor(w),w,false);BuildBoot(WeightColor(w),w);BuildSleeve(w);}BuildSword();Directory.CreateDirectory(Root);File.WriteAllText(Root+"/manifest.json",Newtonsoft.Json.JsonConvert.SerializeObject(Manifest,Newtonsoft.Json.Formatting.Indented));return new{parts=Manifest.Count,source=Root,runtimeStaging=Game,note="Sources only; no scene, asset import, or CombatArtSet mutation"};}
         static float Surface(Vector2 p, int kind, int seed)
        {
            switch (kind)
            {
                case 1:
                {
                    float shift = Mathf.Sin(p.x * 57.5f) > 0f ? 1.5708f : 0f;
                    float ring = Mathf.Abs(Mathf.Sin(p.x * 115f) * Mathf.Sin(p.y * 115f + shift));
                    return (0.8f + 0.32f * ring) * (0.95f + 0.1f * C.Noise(p, 0.05f, seed));
                }
                case 2:
                {
                    float f = Mathf.Repeat(p.x * 7f + 0.5f, 1f);
                    float seam = f < 0.1f ? 0.72f : 1f + 0.1f * (1f - f);
                    return seam * (0.96f + 0.08f * C.Noise(p, 0.04f, seed));
                }
                default:
                    return (0.78f + 0.30f * C.Fbm(p, 0.036f, seed)) * (0.96f + 0.08f * C.Noise(p,0.0035f,seed+2));
            }
        }
        static float FrontArc(Vector2 p, Vector2 c, float inner, float outer, float front)
        {
            float r = (p - c).magnitude;
            return Mathf.Max(Mathf.Max(r - outer, inner - r), c.x + front - p.x);
        }
        static Sprite BuildPlayer(Color armor,ArmorWeight weight){
 int k=Index(weight);var cv=new Painter(640,640,.864f,"body_"+Names[k]);var cloth=new Color32(48,50,48,255);var edge=new Color32(99,94,80,255);
 cv.Layer("01-undercoat");cv.Draw(p=>C.Box(p,new Vector2(.015f,0),.21f,.345f,.045f),(p,d)=>C.Shade(armor,.8f+.22f*C.Dome(d,.12f)),.014f,Outline);
 cv.Layer("02-tapered-cloak");cv.Draw(p=>Quad(p,new Vector2(-.12f,-.35f),new Vector2(.0f,.35f),new Vector2(-.45f,.25f),new Vector2(-.48f,-.23f)),(p,d)=>C.Shade(cloth,(.72f+.4f*C.Dome(d,.12f)+.1f*p.y)*(.9f+.2f*C.Fbm(p,.055f,311))),.014f,Outline);
 cv.Layer("03-broad-cloth-folds");for(int side=-1;side<=1;side+=2){float q=side;cv.Draw(p=>C.Triangle(p,new Vector2(-.14f,.17f*q),new Vector2(-.44f,.23f*q),new Vector2(-.38f,.075f*q)),new Color32(69,66,56,125));cv.Draw(p=>C.Capsule(p,new Vector2(-.43f,.23f*q),new Vector2(-.17f,.31f*q),.008f),edge);}
 cv.Layer("04-angular-shoulder-plates");float sy=k==2?.34f:.30f,rx=k==2?.165f:k==1?.145f:.13f,ry=k==2?.135f:.11f;
 for(int side=-1;side<=1;side+=2){float q=side;var at=new Vector2(.018f,sy*q);Color pad=k==0?(q<0?new Color32(126,132,130,255):new Color32(130,99,66,255)):k==1?new Color32(119,130,132,255):new Color32(150,157,155,255);
 cv.Draw(p=>C.Box(p,at,rx,ry,.052f,-10*q),(p,d)=>C.Shade(pad,(.68f+.3f*C.Dome(d,.075f)+.1f*(p.y-at.y)/ry)*(.93f+.14f*C.Fbm(p,.04f,312))),.013f,Outline);
 cv.Draw(p=>C.Capsule(p,at+new Vector2(-rx*.65f,ry*.55f),at+new Vector2(rx*.63f,ry*.55f),.008f),C.Shade(pad,1.32f));
 if(k>0)cv.Draw(p=>C.Capsule(p,at+new Vector2(-.05f,-.02f),at+new Vector2(.075f,-.02f),.006f),C.Shade(pad,.48f));}
 cv.Layer("05-straps-and-collar");cv.Draw(p=>C.Capsule(p,new Vector2(-.13f,-.29f),new Vector2(.11f,.28f),.026f),new Color32(60,42,29,255),.007f,Outline);
 cv.Draw(p=>Mathf.Max(C.Ellipse(p,new Vector2(-.05f,0),.205f,.253f),p.x+.04f),(p,d)=>new Color32(158,145,118,255),.009f,Outline);
 cv.Layer("06-head");cv.Draw(p=>C.Circle(p,new Vector2(.02f,0),.175f),(p,d)=>C.Shade(new Color32(71,51,35,255),.75f+.3f*C.Dome(d,.16f)),.012f,Outline);cv.Draw(p=>C.Ellipse(p,new Vector2(.164f,0),.051f,.058f),new Color32(160,121,87,255),.006f,Outline);
 return cv.ToSprite("body",Vector2.zero);}

        static Sprite BuildHelm(Color tone, ArmorWeight weight)
        {
            int kind = Index(weight);
            var cv = new Painter(384,384,.864f,"helm_"+Names[kind]);
            var head = new Vector2(0.02f, 0f);
            cv.Layer("01-helm-material");
            switch (kind)
            {
                case 1:
                {
                    cv.Draw(p => Mathf.Min(C.Circle(p, head, 0.198f), C.Ellipse(p, new Vector2(-0.1f, 0f), 0.17f, 0.2f)),
                        (p, d) => C.Shade(tone, C.Dome(d, 0.12f) * Surface(p, 1, 91) * C.Rim(d, 0.02f, 0.4f)), 0.012f, Outline);
                    var band = C.Shade(WeightColor(ArmorWeight.Light), 0.75f);
                    cv.Draw(p => FrontArc(p, head, 0.168f, 0.198f, 0.1f), (p, d) => C.Shade(band, C.Dome(d, 0.012f)), 0.008f, Outline);
                    break;
                }
                case 2:
                {
                    cv.Draw(p => C.Ellipse(p, new Vector2(-0.12f, 0f), 0.14f, 0.19f),
                        (p, d) => C.Shade(tone, 0.78f * C.Dome(d, 0.08f) * Surface(p, 2, 92) * C.Rim(d, 0.015f, 0.4f)), 0.012f, Outline);
                    cv.Draw(p => C.Circle(p, head, 0.2f),
                        (p, d) => C.Shade(tone, C.Dome(d, 0.17f) * (0.95f + 0.08f * C.Noise(p, 0.04f, 93)) * C.Rim(d, 0.02f, 0.5f)), 0.012f, Outline);
                    cv.Draw(p => FrontArc(p, head, 0.17f, 0.2f, 0.12f), (p, d) => C.Shade(tone, 0.72f * C.Dome(d, 0.012f)), 0.008f, Outline);
                    cv.Draw(p => C.Capsule(p, new Vector2(-0.2f, 0f), new Vector2(0.16f, 0f), 0.026f, 0.018f),
                        (p, d) => C.Shade(tone, 1.18f * C.Dome(d, 0.02f) * C.Rim(d, 0.008f, 0.5f)), 0.008f, Outline);
                    var rivet = C.Shade(tone, 1.4f);
                    for (int s = -1; s <= 1; s += 2)
                    for (int i = 0; i < 2; i++)
                    {
                        float a = (70f + i * 55f) * s * Mathf.Deg2Rad;
                        var at = head + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.155f;
                        cv.Draw(p => C.Circle(p, at, 0.012f), rivet, 0.005f, Outline);
                    }
                    break;
                }
                default:
                {
                    var hood = C.Shade(tone, 0.86f);
                    cv.Draw(p => Mathf.Min(C.Circle(p, head, 0.198f), C.Ellipse(p, new Vector2(-0.13f, 0f), 0.17f, 0.17f)),
                        (p, d) => C.Shade(hood, C.Dome(d, 0.12f) * Surface(p, 0, 90) * C.Rim(d, 0.022f, 0.3f)), 0.012f, Outline);
                    cv.Draw(p => C.Capsule(p, new Vector2(-0.28f, 0f), new Vector2(0.16f, 0f), 0.006f), C.Shade(hood, 0.55f));
                    cv.Draw(p => FrontArc(p, head, 0.165f, 0.198f, 0.08f), (p, d) => C.Shade(hood, 1.15f * C.Dome(d, 0.012f)), 0.008f, Outline);
                    break;
                }
            }
            cv.Layer("02-helm-stitches-wear"); HelmDetail(cv,kind);
            return cv.ToSprite("정수리 투구 " + WeightNames[kind], Vector2.zero);
        }

        static Sprite BuildFist(Color tone, ArmorWeight weight, bool closed)
        {
            int kind = Index(weight);
            const float r = 0.052f;
            var cv = new Painter(96,80,1f,"fist_"+Names[kind]+(closed?"_closed":"_open"));
            var glove = C.Shade(tone, kind == 0 ? 0.68f : kind == 1 ? 0.9f : 0.92f);
            if (kind == 2)
                cv.Draw(p => C.Box(p, new Vector2(-0.045f, 0f), 0.022f, 0.046f, 0.012f),
                    (p, d) => C.Shade(glove, 0.82f * C.Dome(d, 0.02f) * C.Rim(d, 0.008f, 0.5f)), 0.008f, Outline);
            cv.Draw(p => C.Circle(p, Vector2.zero, r),
                (p, d) =>
                {
                    float grain = kind == 0 ? 0.93f + 0.14f * C.Noise(p, 0.012f, 21) : Surface(p, kind, 21);
                    return C.Shade(glove, C.Dome(d, r) * grain * C.Rim(d, 0.01f, kind == 2 ? 0.5f : 0.3f));
                }, 0.01f, Outline);
            if (kind == 2)
                cv.Draw(p => Mathf.Max(C.Box(p, new Vector2(-0.01f, 0f), 0.0035f, r), C.Circle(p, Vector2.zero, r - 0.008f)), C.Shade(glove, 0.6f));
            cv.Layer("02-knuckles-and-thumb");
            if (closed)
            {
                cv.Draw(p => C.Circle(p, new Vector2(r * 0.55f, r * 0.55f), r * 0.38f), (p, d) => C.Shade(glove, 1.12f), 0.006f, Outline);
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    var k = new Vector2(r * 0.66f, (i - 1.5f) * r * 0.42f);
                    cv.Draw(p => C.Circle(p, k, r * 0.24f), (p, d) => C.Shade(glove, 1.1f * C.Dome(d, r * 0.2f)), 0.005f, Outline);
                }
            }
            return cv.ToSprite((closed ? "정수리 쥔 주먹 " : "정수리 빈 주먹 ") + WeightNames[kind], Vector2.zero);
        }
        static Sprite BuildBoot(Color tone, ArmorWeight weight)
        {
            int kind = Index(weight);
            var cv = new Painter(112,72,1f,"boot_"+Names[kind]);
            var shell = C.Shade(tone, kind == 0 ? 0.62f : kind == 1 ? 0.8f : 0.86f);
            cv.Draw(Foot,
                (p, d) =>
                {
                    float grain = kind == 0 ? 0.92f + 0.16f * C.Noise(p, 0.015f, 31) : Surface(p, kind, 31);
                    return C.Shade(shell, C.Dome(d, 0.035f) * grain * C.Rim(d, 0.008f, kind == 2 ? 0.5f : 0.3f));
                }, 0.008f, Outline);
            cv.Layer("02-laces-and-toecap");
            if (kind == 0)
            {
                var lace = C.Shade(shell, 1.6f);
                for (int i = 0; i < 3; i++)
                {
                    float x = -0.02f + i * 0.022f;
                    cv.Draw(p => Mathf.Max(C.Capsule(p, new Vector2(x, -0.02f), new Vector2(x, 0.02f), 0.0035f), Foot(p) + 0.006f), lace);
                }
            }
            else if (kind == 2)
            {
                var seam = C.Shade(shell, 0.6f);
                for (int i = 0; i < 2; i++)
                {
                    float x = -0.018f + i * 0.026f;
                    cv.Draw(p => Mathf.Max(C.Box(p, new Vector2(x, 0f), 0.003f, 0.06f), Foot(p) + 0.006f), seam);
                }
                cv.Draw(p => Mathf.Max(Foot(p), 0.046f - p.x), (p, d) => C.Shade(shell, 1.15f * C.Dome(d, 0.02f)), 0.004f, Outline);
            }
            return cv.ToSprite("정수리 장화 " + WeightNames[kind], Vector2.zero);
        }
}
