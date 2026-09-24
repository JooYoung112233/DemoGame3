using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class PolishSettlementHud
{
 const string P="Assets/Prefabs/Settlement/",A="Assets/Art/PartySelection/";
 static Font Font=>AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");
 static void Rect(Transform t,float x,float y,float w,float h){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
 static void Pos(Transform t,string n,float x,float y,float w,float h)=>Rect(t.Find(n),x,y,w,h);
 static Text Style(Transform root,string n,int size,TextAnchor anchor=TextAnchor.MiddleLeft){var t=root.Find(n).GetComponent<Text>();t.font=Font;t.fontStyle=FontStyle.Normal;t.fontSize=size;t.lineSpacing=1;t.alignment=anchor;return t;}
 static GameObject Node(Transform p,string n){var old=p.Find(n);if(old)return old.gameObject;var g=new GameObject(n,typeof(RectTransform));g.transform.SetParent(p,false);return g;}
 static Text Label(Transform p,string n,float x,float y,float w,float h,int size){var g=Node(p,n);Rect(g.transform,x,y,w,h);var t=g.GetComponent<Text>()??g.AddComponent<Text>();t.font=Font;t.fontSize=size;t.fontStyle=FontStyle.Normal;t.lineSpacing=1;t.color=new Color(.045f,.065f,.06f);t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;return t;}
 static Image Icon(Transform p,string n,float x,float y,float w,float h,string sprite){var g=Node(p,n);Rect(g.transform,x,y,w,h);var im=g.GetComponent<Image>()??g.AddComponent<Image>();im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(sprite);im.preserveAspect=true;im.raycastTarget=false;return im;}
 static void Edit(string name,Action<GameObject> action){var g=PrefabUtility.LoadPrefabContents(P+name+".prefab");try{action(g);PrefabUtility.SaveAsPrefabAsset(g,P+name+".prefab");}finally{PrefabUtility.UnloadPrefabContents(g);}}
 static void Arrow(Button b,bool right){
  Rect(b.transform,right?1844:924,934,44,52);var old=b.transform.Find("Label");if(old)old.gameObject.SetActive(false);
  var paper=b.GetComponent<Image>();paper.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(A+"card-paper.png");
  var icon=Icon(b.transform,"ArrowIcon",13,14,18,24,A+(right?"icon-right.png":"icon-left.png"));
  // A CanvasGroup fades both the paper and glyph at page boundaries; normal Button tint only affects its paper.
  var fade=b.GetComponent<CanvasGroup>();if(!fade)fade=b.gameObject.AddComponent<CanvasGroup>();
  var visual=b.GetComponent<RosterArrowVisual>()??b.gameObject.AddComponent<RosterArrowVisual>();visual.Button=b;visual.Group=fade;
  b.transition=Selectable.Transition.ColorTint;var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,.82f,.49f);colors.selectedColor=Color.white;colors.disabledColor=Color.white;b.colors=colors;
 }
 // STALE since 2026-09-25: the ResourceHud has 2 slots at 1430,24,278,72 (BuildRetireSupplies); Apply loops Value_0..2 and moves the HUD to 1338/410. Do not rerun.
 static void RefuseAfterSuppliesRetired(){var hud=AssetDatabase.LoadAssetAtPath<GameObject>(P+"ResourceHud.prefab");if(hud&&!hud.transform.Find("Value_0"))throw new Exception("Superseded: supplies retired 2026-09-25 (BuildRetireSupplies). PolishSettlementHud expects the old 3-slot ResourceHud; do not rerun.");}
 public static string Apply(){
  if(EditorApplication.isPlaying)throw new Exception("Stop first");RefuseAfterSuppliesRetired();for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene");
  Edit("DayPanel",g=>{Rect(g.transform,0,0,196,104);Pos(g.transform,"Clock",20,8,156,88);Style(g.transform,"Clock",28);});
  Edit("AdvanceButton",g=>{Rect(g.transform,0,0,220,56);Pos(g.transform,"AdvanceIcon",18,14,28,28);Pos(g.transform,"Label",58,0,148,56);Style(g.transform,"Label",26,TextAnchor.MiddleCenter);});
  Edit("ResourceHud",g=>{Rect(g.transform,0,0,410,72);for(int i=0;i<3;i++){Pos(g.transform,"Icon_"+i,18+i*132,22,28,28);Pos(g.transform,"Value_"+i,56+i*132,10,70,52);var t=Style(g.transform,"Value_"+i,28);t.resizeTextForBestFit=true;t.resizeTextMinSize=20;t.resizeTextMaxSize=28;}});
  Edit("JournalButton",g=>{Rect(g.transform,0,0,128,72);Pos(g.transform,"Icon",16,20,28,32);Pos(g.transform,"Label",52,0,64,72);Style(g.transform,"Label",24,TextAnchor.MiddleCenter);});
  Edit("ArrivalNotice",g=>{Rect(g.transform,0,0,556,112);Pos(g.transform,"Icon",22,34,30,38);Pos(g.transform,"Title",72,10,460,39);Pos(g.transform,"Body",72,55,460,40);Style(g.transform,"Title",26);Style(g.transform,"Body",24);});
  Edit("MemberCard",g=>{
   Rect(g.transform,0,0,202,180);var c=g.GetComponent<SettlementMemberCard>();Pos(g.transform,"Name",12,8,140,38);Style(g.transform,"Name",24);
   Pos(g.transform,"Portrait",14,58,78,94);Pos(g.transform,"Status",108,61,82,36);Style(g.transform,"Status",22,TextAnchor.MiddleCenter);
   Pos(g.transform,"HealthTrack",108,139,82,14);Pos(g.transform,"HealthTrack/Health",2,2,78,10);
   c.HealthValue=Label(g.transform,"HealthValue",108,102,82,29,18);c.HealthValue.alignment=TextAnchor.MiddleCenter;c.HealthValue.text="3 / 3";
   Rect(c.BagButton.transform,158,8,36,40);var hit=c.BagButton.GetComponent<Image>();hit.sprite=null;hit.color=Color.clear;hit.raycastTarget=true;
   var icon=Icon(c.BagButton.transform,"Glyph",7,8,22,24,A+"icon-bag.png");c.BagButton.targetGraphic=icon;
  });
  Edit("RosterStrip",g=>{Rect(g.transform,0,0,844,180);var layout=g.GetComponent<HorizontalLayoutGroup>();layout.spacing=12;layout.childAlignment=TextAnchor.MiddleRight;foreach(var card in g.GetComponentsInChildren<SettlementMemberCard>(true))Rect(card.transform,0,0,202,180);});
  Edit("SettlementScreen",g=>{
   var c=g.GetComponent<SettlementController>();var main=c.Main.transform;
   Pos(main,"DayPanel",28,24,196,104);Rect(c.Advance.transform,240,24,220,56);Rect(c.Location.transform,240,90,430,38);Style(main,"Location",25);
   Pos(main,"ResourceHud",1338,24,410,72);Rect(c.Journal.transform,1764,24,128,72);Pos(main,"ArrivalNotice",28,938,556,112);
   var roster=main.Find("Roster");Rect(roster,984,870,844,180);Arrow(c.Previous,false);Arrow(c.Next,true);
   c.RosterHeading=Label(roster,"RosterHeading",0,-38,600,32,21);c.RosterHeading.color=new Color(.96f,.93f,.84f);c.RosterHeading.text="정착민";
   c.RosterPage=Label(roster,"RosterPage",624,-38,220,32,20);c.RosterPage.alignment=TextAnchor.MiddleRight;c.RosterPage.color=new Color(.91f,.9f,.83f);
   foreach(var t in new[]{c.RosterHeading,c.RosterPage}){var le=t.GetComponent<LayoutElement>()??t.gameObject.AddComponent<LayoutElement>();le.ignoreLayout=true;}
  });
  AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Aligned shelter HUD; compact 44x52 roster arrows with 18x24 icons; four stable slots and range indicator.";
 }
}

