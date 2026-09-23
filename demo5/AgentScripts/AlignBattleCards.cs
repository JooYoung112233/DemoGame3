using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class AlignBattleCards {
 const string Dir="Assets/Prefabs/Settlement/";
 static BattlePortraitFit.Crop[] crops;
 static void Rect(Transform t,float x,float y,float w,float h){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
 static BattlePortraitFit.Crop Crop(string path,bool bust){
 var tex=new Texture2D(2,2);tex.LoadImage(File.ReadAllBytes(path));var pixels=tex.GetPixels32();int l=tex.width,b=tex.height,r=0,t=0;
 for(int y=0;y<tex.height;y++)for(int x=0;x<tex.width;x++)if(pixels[y*tex.width+x].a>32){l=Math.Min(l,x);r=Math.Max(r,x+1);b=Math.Min(b,y);t=Math.Max(t,y+1);}
 if(bust)b=t-Mathf.RoundToInt((t-b)*.52f);
 var result=new BattlePortraitFit.Crop{Sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path),Area=new Rect((float)l/tex.width,(float)b/tex.height,(float)(r-l)/tex.width,(float)(t-b)/tex.height)};Object.DestroyImmediate(tex);return result;
 }
 static void Fit(Image image){var fit=image.GetComponent<BattlePortraitFit>();if(!fit)fit=image.gameObject.AddComponent<BattlePortraitFit>();fit.Crops=crops;image.preserveAspect=false;image.raycastTarget=false;}
 static void Details(Transform root){
 Rect(root.Find("PortraitPaper"),20,20,150,185);Rect(root.Find("Portrait"),35,47,120,131);Fit(root.Find("Portrait").GetComponent<Image>());
 }
 public static string Run(){
 crops=Directory.GetFiles("Assets/Art/PartySelection","portrait-*.png").Select(p=>Crop(p.Replace('\\','/'),false)).Concat(new[]{Crop("Assets/Art/Tokens/infected-body.png",true)}).ToArray();
 foreach(var name in new[]{"BattleTurnCard","BattleUnitDetails","BattleTargetDetails","ExpeditionBattlePanel"}){
 var path=Dir+name+".prefab";var go=PrefabUtility.LoadPrefabContents(path);
 try{
 if(name=="BattleTurnCard") {var card=go.GetComponent<BattleTurnCard>();Rect(card.Portrait.transform,16,8,64,64);Fit(card.Portrait);Rect(card.Label.transform,4,73,88,30);card.Label.alignment=TextAnchor.MiddleCenter;card.Label.fontSize=17;card.Label.verticalOverflow=VerticalWrapMode.Overflow;}
 else if(name=="ExpeditionBattlePanel") {
 var panel=go.GetComponent<ExpeditionBattlePanel>();Details(panel.ActorPortrait.transform.parent);Details(panel.TargetPortrait.transform.parent);
 var viewport=(RectTransform)panel.TurnContent.parent;Rect(viewport,773,20,852,108);
 var content=panel.TurnContent;content.anchorMin=content.anchorMax=new Vector2(.5f,.5f);content.pivot=new Vector2(.5f,.5f);content.anchoredPosition=Vector2.zero;
 var layout=content.GetComponent<HorizontalLayoutGroup>();layout.childAlignment=TextAnchor.MiddleCenter;layout.spacing=12;layout.padding=new RectOffset();
 }else Details(go.transform);
 PrefabUtility.SaveAsPrefabAsset(go,path);
 }finally{PrefabUtility.UnloadPrefabContents(go);}}
 AssetDatabase.SaveAssets();return "Aligned turn viewport, centered ink crops for 6 allies + infected bust, identical bottom portrait frames; saved 4 prefabs.";
 }
}
