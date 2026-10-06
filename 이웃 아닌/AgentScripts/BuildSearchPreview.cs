using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class BuildSearchPreview {
 const string P="Assets/Prefabs/Settlement/",A="Assets/Art/PartySelection/";static Font font;
 static RectTransform R(string n,Transform p,float x,float y,float w,float h){var r=new GameObject(n,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(p,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
 static Image I(string n,Transform p,float x,float y,float w,float h,string path=null){var im=R(n,p,x,y,w,h).gameObject.AddComponent<Image>();if(path!=null)im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);im.raycastTarget=false;return im;}
 static Text T(string n,Transform p,float x,float y,float w,float h,string s,int size=28,bool light=true){var t=R(n,p,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.fontStyle=FontStyle.Normal;t.fontSize=size;t.text=s;t.color=light?new Color(.96f,.93f,.85f):new Color(.045f,.065f,.06f);t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;return t;}
 static Button B(string n,Transform p,float x,float y,float w,float h,string label,int size=32){var im=I(n,p,x,y,w,h,A+"footer-paper.png");im.raycastTarget=true;var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;T("Label",im.transform,8,0,w-16,h,label,size,false).alignment=TextAnchor.MiddleCenter;return b;}
 static GameObject Save(GameObject g,string name){var asset=PrefabUtility.SaveAsPrefabAsset(g,P+name+".prefab");Object.DestroyImmediate(g);return asset;}
 public static string Run(){
  if(EditorApplication.isPlaying)throw new Exception("Stop first");
  font=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");
  var row=I("SearchDropRow",null,0,0,592,64,A+"footer-paper.png");var rc=row.gameObject.AddComponent<SearchDropRow>();rc.Icon=I("Icon",row.transform,12,7,50,50);rc.Icon.preserveAspect=true;rc.Name=T("Name",row.transform,78,0,282,64,"물건",25,false);rc.Quantity=T("Quantity",row.transform,360,0,90,64,"",25,false);rc.Chance=T("Chance",row.transform,452,0,120,64,"",27,false);rc.Chance.alignment=TextAnchor.MiddleRight;var le=row.gameObject.AddComponent<LayoutElement>();le.preferredHeight=64;var ra=Save(row.gameObject,"SearchDropRow");
  var root=R("SearchDropPreview",null,1180,248,688,482);var c=root.gameObject.AddComponent<SearchDropPreview>();I("Paper",root,0,0,688,482,A+"card-paper.png").color=new Color(.1f,.14f,.14f,.98f);T("Title",root,28,14,620,48,"발견 가능한 물건",32);c.Summary=T("Summary",root,28,66,630,72,"",24);T("ItemHeading",root,106,142,282,30,"물건",20);T("CountHeading",root,388,142,90,30,"수량",20);T("ChanceHeading",root,480,142,120,30,"확률",20).alignment=TextAnchor.MiddleRight;
  var vp=R("Scroll",root,28,182,630,230);vp.gameObject.AddComponent<RectMask2D>();vp.gameObject.AddComponent<Image>().color=Color.clear;c.Content=R("Content",vp,0,0,610,230);var layout=c.Content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=8;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;c.Content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;var sc=vp.gameObject.AddComponent<ScrollRect>();sc.viewport=vp;sc.content=c.Content;sc.horizontal=false;sc.movementType=ScrollRect.MovementType.Clamped;sc.scrollSensitivity=36;c.RowPrefab=ra.GetComponent<SearchDropRow>();var hint=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(P+"ScrollMoreHint.prefab"),vp);hint.GetComponent<ScrollMoreIndicator>().Scroll=sc;PrefabUtility.RecordPrefabInstancePropertyModifications(hint.GetComponent<ScrollMoreIndicator>());c.Note=T("Note",root,28,426,630,40,"",21);var asset=Save(root.gameObject,"SearchDropPreview");
  var g=PrefabUtility.LoadPrefabContents(P+"ExpeditionSearchPanel.prefab");try{var s=g.GetComponent<ExpeditionSearchPanel>();var old=s.Workspace.transform.Find("SearchDropPreview");if(old)Object.DestroyImmediate(old.gameObject);s.DropPreview=((GameObject)PrefabUtility.InstantiatePrefab(asset,s.Workspace.transform)).GetComponent<SearchDropPreview>();PrefabUtility.SaveAsPrefabAsset(g,P+"ExpeditionSearchPanel.prefab");}finally{PrefabUtility.UnloadPrefabContents(g);}AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Search preview and individual probability rows saved.";
 }
}
