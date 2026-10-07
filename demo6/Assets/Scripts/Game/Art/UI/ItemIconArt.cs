using System.Collections.Generic;
using UnityEngine;
using C=Demo6.Game.TopDownCanvas;
namespace Demo6.Game {
 /// <summary>인벤토리 전용 자체 아이콘. 현재 그림 원본은 UI-정리-v2의 1254px PNG/Aseprite, 게임 PNG는 512px. 등급은 슬롯·이름이 표시한다. Build는 자산 누락 시 v1 대체 그림만 만든다.</summary>
 public static class ItemIconArt {
  public static readonly string[] Ids={"wpn_longsword","wpn_greatsword","wpn_twinblades","arm_leather","arm_chain","arm_plate","hlm_leather","hlm_chain","hlm_plate","glv_leather","glv_chain","glv_plate","bts_leather","bts_chain","bts_plate","rng_iron","rng_blood","rng_fang","amu_fang","amu_charm","amu_amber","potion","stone","gold","pickaxe","key","figure"};
  static readonly Dictionary<string,Texture2D> Cache=new Dictionary<string,Texture2D>();
  static readonly Color Ink=new Color32(12,13,16,255),Steel=new Color32(132,140,150,255),Light=new Color32(213,211,197,255),Dark=new Color32(66,72,83,255),Leather=new Color32(99,70,43,255),Brass=new Color32(144,114,66,255);
  public static Texture2D Get(string id){if(Cache.TryGetValue(id,out var tex)&&tex)return tex;tex=Resources.Load<Texture2D>("UI/Items/"+id);if(!tex)tex=Build(id,256).texture;Cache[id]=tex;return tex;}
  public static void Draw(Rect r,string id,Demo6.Core.Loot.Grade grade){if(EquipmentVisualV049.DrawIcon(r,id,grade))return;Draw(r,id);}
  public static void Draw(Rect r,string id){var color=GUI.color;GUI.color=Color.white;GUI.DrawTexture(r,Get(id),ScaleMode.ScaleToFit,true);GUI.color=color;}
  public static void DrawEmpty(Rect r,Demo6.Core.Loot.GearPart part){var color=GUI.color;GUI.color=new Color(.9f,.85f,.72f,.62f);GUI.DrawTexture(r,Get("empty_"+part.ToString().ToLowerInvariant()),ScaleMode.ScaleToFit,true);GUI.color=color;}
  public static Sprite Build(string id,float ppu=256,int layer=-1){var c=new C(-1,1,-1,1,ppu);bool main=layer!=1,detail=layer!=0;
   if(id.StartsWith("wpn_")){bool heavy=id=="wpn_greatsword",twin=id=="wpn_twinblades";int count=twin?2:1;for(int i=0;i<count;i++){
    float shift=twin?(i==0?-.29f:.29f):0f,scale=twin?.78f:1f;float width=heavy?.17f:.10f;
    Vector2 P(Vector2 p)=>C.Rotate(p,-43)/scale-new Vector2(0,shift);
    if(main){
     c.Draw(p=>C.Capsule(P(p),new Vector2(-.76f,0),new Vector2(-.38f,0),.065f),Leather,.04f,Ink);
     c.Draw(p=>C.Box(P(p),new Vector2(.10f,0),.46f,width,.02f),Steel,.035f,Ink);
     c.Draw(p=>C.Triangle(P(p),new Vector2(.54f,-width),new Vector2(.83f,0),new Vector2(.54f,width)),Steel,.02f,Ink);
     c.Draw(p=>C.Capsule(P(p),new Vector2(-.36f,-(heavy?.32f:.23f)),new Vector2(-.36f,heavy?.32f:.23f),.055f),Brass,.035f,Ink);
     c.Draw(p=>C.Circle(P(p),new Vector2(-.77f,0),.087f),Brass,.03f,Ink);
    }
    if(detail){c.Draw(p=>C.Capsule(P(p),new Vector2(-.25f,width*.53f),new Vector2(.57f,width*.25f),.018f),Light);c.Draw(p=>C.Capsule(P(p),new Vector2(-.22f,-width*.45f),new Vector2(.54f,-width*.2f),.018f),Dark);for(int j=0;j<3;j++){float x=-.68f+j*.1f;c.Draw(p=>C.Capsule(P(p),new Vector2(x,-.05f),new Vector2(x+.035f,.05f),.016f),Dark);}}
   }}
   else if(id=="potion"){
    if(main){c.Draw(p=>C.Ellipse(p,new Vector2(0,-.18f),.48f,.55f),(p,d)=>C.Shade(Dark,C.Dome(d,.4f)),.055f,Ink);c.Draw(p=>C.Box(p,new Vector2(0,.43f),.19f,.25f,.04f),Steel,.04f,Ink);c.Draw(p=>C.Box(p,new Vector2(0,.67f),.23f,.11f,.025f),Leather,.03f,Ink);}
    if(detail){c.Draw(p=>Mathf.Max(C.Ellipse(p,new Vector2(0,-.19f),.37f,.43f),p.y+.02f),new Color32(122,22,22,255));c.Draw(p=>C.Capsule(p,new Vector2(-.24f,-.20f),new Vector2(-.22f,.12f),.035f),Light);c.Draw(p=>C.Box(p,new Vector2(0,.41f),.22f,.05f,.02f),Brass);}
   }else if(id=="gold"){
    if(main)for(int i=0;i<3;i++){float y=-.30f+i*.23f;c.Draw(p=>C.Ellipse(p,new Vector2(0,y),.66f,.26f),Brass,.045f,Ink);}
    if(detail){c.Draw(p=>Mathf.Abs(C.Ellipse(p,new Vector2(0,.16f),.47f,.16f))-.018f,Light);c.Draw(p=>C.Box(p,new Vector2(0,.16f),.075f,.10f,.01f),Dark);}
   }else if(id=="stone"){
    if(main)c.Draw(p=>Mathf.Max(C.Box(p,new Vector2(0,0),.51f,.63f,.07f,18),C.Box(p,new Vector2(0,0),.59f,.65f,.08f,-18)),Dark,.06f,Ink);
    if(detail){c.Draw(p=>C.Triangle(p,new Vector2(-.38f,.33f),new Vector2(.23f,.49f),new Vector2(.06f,-.40f)),Steel);c.Draw(p=>C.Capsule(p,new Vector2(-.34f,.3f),new Vector2(.16f,.45f),.027f),Light);c.Draw(p=>C.Capsule(p,new Vector2(.08f,-.45f),new Vector2(.31f,.17f),.035f),Brass);}
   }else if(id=="pickaxe"){
    Vector2 P(Vector2 p)=>C.Rotate(p,-36);
    if(main){c.Draw(p=>C.Capsule(P(p),new Vector2(-.75f,0),new Vector2(.36f,0),.072f),Leather,.04f,Ink);c.Draw(p=>C.Curve(P(p),new Vector2(.08f,-.63f),new Vector2(.64f,.05f),new Vector2(.07f,.67f),.07f,.025f),Steel,.04f,Ink);}
    if(detail){c.Draw(p=>C.Capsule(P(p),new Vector2(.23f,-.15f),new Vector2(.27f,.24f),.04f),Light);c.Draw(p=>C.Box(P(p),new Vector2(.31f,0),.11f,.14f,.02f),Dark);}
   }else if(id=="key"){
    Vector2 P(Vector2 p)=>C.Rotate(p,-43);
    if(main){c.Draw(p=>Mathf.Abs(C.Circle(P(p),new Vector2(-.38f,0),.26f))-.08f,Brass,.035f,Ink);c.Draw(p=>C.Capsule(P(p),new Vector2(-.12f,0),new Vector2(.7f,0),.07f),Brass,.04f,Ink);for(int i=0;i<2;i++){float x=.35f+i*.22f;c.Draw(p=>C.Box(P(p),new Vector2(x,-.14f),.075f,.14f,.01f),Brass,.025f,Ink);}}
    if(detail)c.Draw(p=>C.Capsule(P(p),new Vector2(-.08f,.025f),new Vector2(.64f,.025f),.017f),Light);
   }else if(id=="figure"){
    if(main){var dim=new Color(.15f,.17f,.19f);c.Draw(p=>C.Ellipse(p,new Vector2(0,.58f),.19f,.23f),dim,.018f,Dark);c.Draw(p=>C.Box(p,new Vector2(0,.06f),.24f,.29f,.12f),dim,.018f,Dark);for(int side=-1;side<=1;side+=2){float sideX=side;c.Draw(p=>C.Capsule(p,new Vector2(sideX*.27f,.27f),new Vector2(sideX*.43f,-.30f),.085f,.06f),dim,.018f,Dark);c.Draw(p=>C.Capsule(p,new Vector2(sideX*.12f,-.19f),new Vector2(sideX*.19f,-.77f),.105f,.075f),dim,.018f,Dark);}}
    if(detail)c.Draw(p=>C.Capsule(p,new Vector2(-.22f,.34f),new Vector2(.22f,.34f),.017f),new Color(.26f,.27f,.28f));
   }
   return c.ToSprite("ui_"+id,Vector2.zero);
  }
 }
}
