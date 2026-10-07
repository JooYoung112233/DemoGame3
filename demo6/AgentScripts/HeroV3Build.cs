using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections.Generic;using UnityEngine;using UnityEditor;using Demo6.Game;using Demo6.Core.Loot;using C=Demo6.Game.TopDownCanvas;
public static class HeroV3Build {
 const string Root="아트/정수리/검사-v3", Game="Assets/Art/TopDown/PlayerV3";
 static readonly string[] Names={"leather","chain","plate"},WeightNames={"가죽","사슬","판금"};
 static readonly Color Outline=new Color32(16,15,14,255),Bone=new Color32(169,153,122,255),Iron=new Color32(101,105,104,255);
 static readonly List<object> Manifest=new List<object>();
 static int Index(ArmorWeight w)=>w==ArmorWeight.Heavy?2:w==ArmorWeight.Medium?1:0;
 static Color WeightColor(ArmorWeight w)=>Index(w)==2?new Color32(105,110,112,255):Index(w)==1?new Color32(79,84,88,255):new Color32(104,75,49,255);
 static float Foot(Vector2 p)=>C.Capsule(p,new Vector2(-.038f,0),new Vector2(.034f,0),.04f,.047f);
 sealed class Painter {
  public readonly int W,H;readonly float factor;readonly string id;readonly C all;C layer;string layerName;readonly List<string> layers=new List<string>();
  public Painter(int w,int h,float f,string name){W=w;H=h;factor=f;id=name;all=Canvas();Layer("01-material");}
  C Canvas()=>new C(-W/1024f,W/1024f,-H/1024f,H/1024f,512);
  public void Layer(string name){if(layer!=null&&draws>0){Save(layer,Root+"/layers/"+id+"/"+layerName+".png");layers.Add(layerName);}layerName=name;layer=Canvas();draws=0;}
  int draws;
  public void Draw(C.Shape shape,C.Paint paint,float outline=0,Color color=default){C.Shape s=p=>shape(p/factor)*factor;C.Paint t=(p,d)=>paint(p/factor,d/factor);all.Draw(s,t,outline*factor,color);layer.Draw(s,t,outline*factor,color);draws++;}
  public void Draw(C.Shape shape,Color paint,float outline=0,Color color=default)=>Draw(shape,(p,d)=>paint,outline,color);
  public Sprite ToSprite(string name,Vector2 pivot){Layer("finished");Save(all,Root+"/PNG/"+id+".png");SaveHalf(all,Game+"/"+id+".png");Manifest.Add(new{id,width=W,height=H,masterPPU=512,runtimePPU=256,pivot=new[]{.5f,.5f},layers=layers.ToArray()});return null;}
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
 static void BuildSleeve(ArmorWeight w){int kind=Index(w);var cv=new Painter(224,64,1,"sleeve_"+Names[kind]);var tone=WeightColor(w);cv.Draw(p=>C.Box(p,Vector2.zero,.24f,.041f,.018f),(p,d)=>C.Shade(tone,C.Dome(d,.035f)*Surface(p,kind,107)),.005f,Outline);cv.Layer("02-seams");for(int i=-2;i<=2;i++){float x=i*.067f;cv.Draw(p=>C.Box(p,new Vector2(x,0),.003f,.032f,.001f),C.Shade(tone,.58f));}cv.ToSprite("sleeve",Vector2.zero);}
 public static object Build(){if(EditorApplication.isPlaying)throw new Exception("Stop Play before importing art");Manifest.Clear();foreach(var w in new[]{ArmorWeight.Light,ArmorWeight.Medium,ArmorWeight.Heavy}){BuildPlayer(WeightColor(w),w);BuildHelm(WeightColor(w),w);BuildFist(WeightColor(w),w,true);BuildFist(WeightColor(w),w,false);BuildBoot(WeightColor(w),w);BuildSleeve(w);}Directory.CreateDirectory(Root);File.WriteAllText(Root+"/manifest.json",Newtonsoft.Json.JsonConvert.SerializeObject(Manifest,Newtonsoft.Json.Formatting.Indented));AssetDatabase.Refresh();foreach(string path in Directory.GetFiles(Game,"*.png")){var t=(TextureImporter)AssetImporter.GetAtPath(path);t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.spritePixelsPerUnit=256;t.spritePivot=new Vector2(.5f,.5f);var settings=new TextureImporterSettings();t.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Center;settings.spriteMeshType=SpriteMeshType.FullRect;t.SetTextureSettings(settings);t.alphaIsTransparency=true;t.mipmapEnabled=true;t.filterMode=FilterMode.Trilinear;t.wrapMode=TextureWrapMode.Clamp;t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=1024;t.SaveAndReimport();}
  var art=AssetDatabase.LoadAssetAtPath<CombatArtSet>("Assets/Data/Combat/CombatArtSet.asset");var p=art.topDown.player;Sprite S(string id)=>AssetDatabase.LoadAssetAtPath<Sprite>(Game+"/"+id+".png");p.scale=1;p.body=S("body_leather");p.boot=S("boot_leather");p.sleeve=S("sleeve_leather");p.fist=S("fist_leather_closed");p.armors=Names.Select(n=>new TopDownArmorArt{armorId="arm_"+n,body=S("body_"+n),sleeve=S("sleeve_"+n)}).ToArray();p.helms=Names.Select(n=>new TopDownHelmArt{helmId="hlm_"+n,sprite=S("helm_"+n)}).ToArray();p.gloves=Names.Select(n=>new TopDownGlovesArt{glovesId="glv_"+n,fistClosed=S("fist_"+n+"_closed"),fistOpen=S("fist_"+n+"_open")}).ToArray();p.boots=Names.Select(n=>new TopDownBootsArt{bootsId="bts_"+n,boot=S("boot_"+n)}).ToArray();EditorUtility.SetDirty(art);AssetDatabase.SaveAssetIfDirty(art);return new{parts=Manifest.Count,source=Root,game=Game};
 }
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
        static Sprite BuildPlayer(Color armor, ArmorWeight weight)
        {
            int kind = Index(weight);
            var cv = new Painter(640,640,.864f,"body_"+Names[kind]);
            var cloak = new Color32(47,44,40,255);
            var collar = new Color32(151,139,114,255);
            var bone = new Color(0.6f, 0.56f, 0.5f);
            var skin = new Color(0.62f, 0.47f, 0.37f);
            var hair = new Color(0.15f, 0.11f, 0.085f);
            var hairLight = new Color(0.33f, 0.25f, 0.18f);
            var strap = C.Shade(WeightColor(ArmorWeight.Light), 0.62f);

            cv.Layer("01-undercoat");
            // 웃옷(어깨 앞쪽): 무게 색과 결.
            cv.Draw(p => C.Ellipse(p, new Vector2(0.04f, 0f), 0.24f, 0.41f),
                (p, d) => C.Shade(armor, C.Dome(d, 0.2f) * Surface(p, kind, 11) * C.Rim(d, 0.03f, kind == 2 ? 0.4f : 0.25f)),
                0.014f, Outline);
            cv.Layer("02-charcoal-cloak");
            // 망토: 등 뒤 절반을 덮고 목에서 퍼지는 주름.
            cv.Draw(p => C.Ellipse(p, new Vector2(-0.19f, 0f), 0.3f, 0.46f),
                (p, d) =>
                {
                    float ang = Mathf.Atan2(p.y, p.x + 0.04f);
                    float fold = 1f + 0.11f * Mathf.Sin(ang * 9f + C.Noise(p, 0.12f, 12) * 2.5f);
                    float k = C.Dome(d, 0.24f) * fold * (0.94f + 0.12f * C.Fbm(p, 0.035f, 13)) * C.Rim(d, 0.03f, 0.3f);
                    return C.Shade(cloak, k);
                },
                0.014f, Outline);
            cv.Layer("03-seams-and-wear");
            CloakDetail(cv);
            cv.Layer("04-armor-shoulders");
            // 어깨: 가죽 = 좁고 작은 덧댐, 사슬 = 고리 어깨 덮개, 판금 = 넓은 어깨받이(두 겹 + 징). 무게가 어깨 폭으로 읽힌다.
            float sy = kind == 2 ? 0.355f : kind == 1 ? 0.33f : 0.3f;
            float rx = kind == 2 ? 0.165f : kind == 1 ? 0.13f : 0.1f;
            float ry = kind == 2 ? 0.14f : kind == 1 ? 0.105f : 0.08f;
            var pad = kind == 0 ? C.Shade(armor, 0.85f) : armor;
            for (int s = -1; s <= 1; s += 2)
            {
                float side = s;
                var at = new Vector2(0.01f, sy * side);
                cv.Draw(p => C.Ellipse(p, at, rx, ry, -12f * side),
                    (p, d) => C.Shade(pad, C.Dome(d, ry * 0.7f) * Surface(p, kind, 14) * C.Rim(d, 0.02f, kind == 2 ? 0.55f : 0.4f)),
                    0.012f, Outline);
                if (kind == 2)
                {
                    var inner = new Vector2(0.01f, (sy - 0.09f) * side);
                    cv.Draw(p => C.Ellipse(p, inner, rx * 0.72f, ry * 0.62f, -12f * side),
                        (p, d) => C.Shade(armor, 1.06f * C.Dome(d, 0.05f) * C.Rim(d, 0.015f, 0.5f)), 0.016f, Outline);
                    var rivet = C.Shade(armor, 1.35f);
                    cv.Draw(p => Mathf.Min(C.Circle(p, at + new Vector2(0.075f, 0.05f * side), 0.013f), C.Circle(p, at + new Vector2(-0.075f, 0.05f * side), 0.013f)), rivet);
                }
                else
                    cv.Draw(p => C.Circle(p, new Vector2(0.06f, sy * side), 0.014f), bone);
            }
            cv.Layer("05-leather-straps-and-steel");
            ArmorDetail(cv,kind,armor);
            if (kind == 0)
            {
                // 가죽 끈: 오른어깨에서 가슴을 가로질러 왼어깨 앞으로(머리 밑을 지나 양 끝만 보임) + 뼈색 버클.
                cv.Draw(p => C.Capsule(p, new Vector2(-0.06f, -0.31f), new Vector2(0.17f, 0.3f), 0.022f),
                    (p, d) => C.Shade(strap, 0.9f + 0.2f * Mathf.Abs(Mathf.Sin((p.x + p.y) * 90f))), 0.01f, Outline);
                cv.Draw(p => C.Box(p, new Vector2(0.135f, 0.205f), 0.026f, 0.02f, 0.005f, 69f), C.Shade(bone, 0.85f), 0.006f, Outline);
            }
            cv.Layer("06-worn-linen-collar");
            // 목 뒤 옷깃(망토 깃). 투구가 머리를 덮고 남은 뒤쪽만 보인다.
            cv.Draw(p => C.Ellipse(p, new Vector2(-0.12f, 0f), 0.15f, 0.22f),
                (p, d) => C.Shade(collar, C.Dome(d, 0.1f) * (1f + 0.1f * Mathf.Sin(p.y * 60f)) * C.Rim(d, 0.02f, 0.3f)),
                0.012f, Outline);
            cv.Layer("07-head-and-hair");
            // 이마·코끝(바라보는 쪽 표시).
            cv.Draw(p => Mathf.Min(C.Ellipse(p, new Vector2(0.14f, 0f), 0.1f, 0.12f), C.Ellipse(p, new Vector2(0.245f, 0f), 0.035f, 0.03f)),
                (p, d) => C.Shade(skin, C.Dome(d, 0.05f)),
                0.016f, Outline);
            // 정수리 머리카락: 가마에서 뻗는 결 + 윤기 고리.
            var crown = new Vector2(-0.04f, 0.02f);
            cv.Draw(p => C.Circle(p, new Vector2(0.02f, 0f), 0.178f),
                (p, d) =>
                {
                    Vector2 q = p - crown;
                    float r = q.magnitude;
                    float ang = Mathf.Atan2(q.y, q.x);
                    float strand = C.Noise(new Vector2(ang * 3.2f, r * 6f), 0.35f, 15);
                    float sheen = 1f + 0.55f * Mathf.Exp(-Mathf.Pow((r - 0.1f) / 0.035f, 2f));
                    var c = Color.Lerp(hair, hairLight, Mathf.Clamp01(strand * 0.7f * sheen - 0.15f));
                    return C.Shade(c, C.Dome(d, 0.12f) * C.Rim(d, 0.02f, 0.35f));
                },
                0.012f, Outline);
            return cv.ToSprite("정수리 검사 몸 " + WeightNames[kind], Vector2.zero);
        }
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
