using System;using System.IO;using System.Reflection;using System.Collections.Generic;using UnityEngine;using UnityEditor;using Demo6.Game;using C=Demo6.Game.TopDownCanvas;
public static class HeroV5Accessories {
 const string Root="아트/정수리/검사-v5-망토와무기",Game=Root+"/runtime-PNG";
 static readonly List<object> Manifest=new List<object>();static readonly Color Outline=new Color32(16,15,14,255);
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

 static float Polygon(Vector2 p,Vector2[] v){float d=999;bool inside=false;for(int i=0,j=v.Length-1;i<v.Length;j=i++){Vector2 a=v[j],b=v[i],e=b-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,e)/Mathf.Max(e.sqrMagnitude,.0000001f));d=Mathf.Min(d,(p-a-e*t).magnitude);if((a.y>p.y)!=(b.y>p.y) && p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;}return inside?-d:d;}
 static void Cape(){var cv=new Painter(640,448,1,"short_cape",new Vector2(.88f,.5f));var poly=new[]{new Vector2(-.12f,.22f),new Vector2(-.33f,.31f),new Vector2(-.88f,.27f),new Vector2(-.84f,.14f),new Vector2(-.94f,.07f),new Vector2(-.87f,.025f),new Vector2(-.93f,-.12f),new Vector2(-.81f,-.105f),new Vector2(-.86f,-.25f),new Vector2(-.5f,-.32f),new Vector2(-.12f,-.22f)};
 cv.Layer("01-cloth-silhouette");cv.Draw(p=>Polygon(p,poly),(p,d)=>{float fold=.86f+.17f*Mathf.Cos((p.y+.08f*p.x)*24)+.07f*C.Fbm(p,.07f,41);return C.Shade(new Color32(86,94,85,255),fold*C.Rim(d,.022f,.6f));},.008f,Outline);
 cv.Layer("02-large-folds");for(int i=-1;i<=1;i++){float side=i;cv.Draw(p=>C.Curve(p,new Vector2(-.22f,side*.13f),new Vector2(-.52f,side*.19f+.025f),new Vector2(-.80f,side*.22f+.015f),.013f,.005f),new Color32(42,48,44,140));cv.Draw(p=>C.Curve(p,new Vector2(-.26f,side*.13f+.03f),new Vector2(-.53f,side*.19f+.043f),new Vector2(-.79f,side*.22f+.033f),.007f,.003f),new Color32(118,120,98,130));}
 cv.Layer("03-worn-hem");for(int i=1;i<poly.Length-2;i++) {Vector2 a=poly[i],b=poly[i+1];cv.Draw(p=>C.Capsule(p,a*.975f,b*.975f,.004f),new Color32(133,124,96,235));}cv.ToSprite("cape",Vector2.zero);}
 static void Weapon(bool great){string id=great?"greatsword":"twinblade";float end=great?1.405f:.62f,half=great?.084f:.040f;int w=great?864:464,h=great?192:160;float pivot=great?112f:96f;var cv=new Painter(w,h,1,id,new Vector2(pivot/w,.5f));
 cv.Layer("01-hilt");cv.Draw(p=>C.Box(p,new Vector2(great?-.065f:-.03f,0),great?.13f:.08f,.025f,.006f),new Color32(61,42,31,255),.007f,Outline);cv.Draw(p=>C.Circle(p,new Vector2(great?-.198f:-.115f,0),.027f),new Color32(145,130,97,255),.006f,Outline);
 cv.Layer("02-blade-planes");var points=great?new[]{new Vector2(.12f,-half),new Vector2(end-.13f,-half*.75f),new Vector2(end,0),new Vector2(end-.13f,half*.75f),new Vector2(.12f,half)}:new[]{new Vector2(.07f,-half),new Vector2(.43f,-.051f),new Vector2(end,.035f),new Vector2(.39f,.047f),new Vector2(.07f,half)};
 cv.Draw(p=>Polygon(p,points),(p,d)=>p.y>0?new Color32(190,198,193,255):new Color32(103,119,122,255),.006f,Outline);
 cv.Layer("03-edge-and-channel");cv.Draw(p=>C.Capsule(p,new Vector2(.18f,0),new Vector2(end-.18f,.005f),great?.010f:.005f),new Color32(55,69,73,255));cv.Draw(p=>C.Capsule(p,new Vector2(.14f,half-.012f),new Vector2(end-.15f,half*.7f),.005f),new Color32(222,220,197,255));
 cv.Layer("04-guard");cv.Draw(p=>C.Box(p,new Vector2(.072f,0),.025f,great?.155f:.088f,.008f,great?0:-10),(p,d)=>C.Shade(new Color32(158,140,105,255),.85f+.2f*C.Dome(d,.02f)),.006f,Outline);cv.ToSprite(id,Vector2.zero);}
 public static object Build(){if(EditorApplication.isPlaying)throw new Exception("Edit mode required");Manifest.Clear();Cape();Weapon(true);Weapon(false);File.WriteAllText(Root+"/manifest.json",Newtonsoft.Json.JsonConvert.SerializeObject(Manifest,Newtonsoft.Json.Formatting.Indented));return new{parts=Manifest.Count,root=Root};}
}
