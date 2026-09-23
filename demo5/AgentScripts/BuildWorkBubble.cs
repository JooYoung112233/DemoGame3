using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class BuildWorkBubble {
 const string P="Assets/Prefabs/Settlement/",A="Assets/Art/PartySelection/",E="Assets/Art/ExpeditionPlan/";static Font font;
 static RectTransform R(string n,Transform p,float x,float y,float w,float h){var r=new GameObject(n,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(p,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
 static Image I(string n,Transform p,float x,float y,float w,float h,string path=null){var im=R(n,p,x,y,w,h).gameObject.AddComponent<Image>();if(path!=null)im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);im.raycastTarget=false;return im;}
 static Text T(string n,Transform p,float x,float y,float w,float h,string s,int size=30,bool light=false){var t=R(n,p,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.fontStyle=FontStyle.Normal;t.fontSize=size;t.text=s;t.color=light?new Color(.96f,.93f,.85f):new Color(.045f,.065f,.06f);t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;return t;}
 static Button B(string n,Transform p,float x,float y,float w,float h,string label,int size=32){var im=I(n,p,x,y,w,h,A+"footer-paper.png");im.raycastTarget=true;var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;T("Label",im.transform,6,0,w-12,h,label,size).alignment=TextAnchor.MiddleCenter;return b;}
 static GameObject Save(GameObject g,string name){var asset=PrefabUtility.SaveAsPrefabAsset(g,P+name+".prefab");Object.DestroyImmediate(g);return asset;}
 static Sprite S(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>(E+name+".png");
 public static string Build(){
  if(EditorApplication.isPlaying)throw new Exception("Stop first");font=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");
  var root=R("WorkBubble",null,0,0,0,0);var c=root.gameObject.AddComponent<SettlementWorkBubble>();
  var visual=R("Visual",root,-120,-42,240,84);c.Visual=visual.gameObject;I("Paper",visual,0,0,240,84,A+"card-paper.png");
  var tail=I("Tail",visual,188,75,19,19,A+"count-paper.png");tail.rectTransform.localRotation=Quaternion.Euler(0,0,45);tail.transform.SetAsFirstSibling();
  c.ToolIcon=I("Tool",visual,14,25,34,34,"Assets/Art/Settlement/icon-work.png").rectTransform;c.ToolIcon.pivot=new Vector2(.5f,.5f);c.ToolIcon.anchoredPosition+=new Vector2(17,-17);
  c.Label=T("Label",visual,60,8,166,68,"작업 중\n예상 20분",25);var asset=Save(root.gameObject,"WorkBubble");
  var screen=PrefabUtility.LoadPrefabContents(P+"SettlementScreen.prefab");
  try{var main=screen.transform.Find("Main");var caption=(RectTransform)main.Find("Facility_work/Caption");caption.anchorMin=caption.anchorMax=caption.pivot=new Vector2(0,1);caption.anchoredPosition=new Vector2(-58,64);caption.sizeDelta=new Vector2(200,54);
   for(int i=0;i<2;i++){var old=main.Find("WorkBubble_"+i);if(old)Object.DestroyImmediate(old.gameObject);var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,main);g.name="WorkBubble_"+i;var bubble=g.GetComponent<SettlementWorkBubble>();bubble.MemberIndex=i;bubble.Offset=new Vector2(i==0?-105:100,72);PrefabUtility.RecordPrefabInstancePropertyModifications(bubble);}
   PrefabUtility.SaveAsPrefabAsset(screen,P+"SettlementScreen.prefab");
  }finally{PrefabUtility.UnloadPrefabContents(screen);}AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Work bubble prefab and workbench caption top anchor saved.";
 }
}
