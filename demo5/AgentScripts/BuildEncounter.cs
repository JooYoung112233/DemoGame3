using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class BuildEncounter {
 const string P="Assets/Prefabs/Settlement/",A="Assets/Art/PartySelection/";static Font font;
 static RectTransform R(string n,Transform p,float x,float y,float w,float h){var r=new GameObject(n,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(p,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
 static Image I(string n,Transform p,float x,float y,float w,float h,string path=null){var im=R(n,p,x,y,w,h).gameObject.AddComponent<Image>();if(path!=null)im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);im.raycastTarget=false;return im;}
 static Text T(string n,Transform p,float x,float y,float w,float h,string s,int size=28,bool light=true){var t=R(n,p,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.fontStyle=FontStyle.Normal;t.fontSize=size;t.text=s;t.color=light?new Color(.96f,.93f,.85f):new Color(.045f,.065f,.06f);t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;return t;}
 static Button B(string n,Transform p,float x,float y,float w,float h,string label,int size=32){var im=I(n,p,x,y,w,h,A+"footer-paper.png");im.raycastTarget=true;var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;T("Label",im.transform,8,0,w-16,h,label,size,false).alignment=TextAnchor.MiddleCenter;return b;}
 static GameObject Save(GameObject g,string name){var asset=PrefabUtility.SaveAsPrefabAsset(g,P+name+".prefab");Object.DestroyImmediate(g);return asset;}

 public static string Build(){
 if(EditorApplication.isPlaying)throw new Exception("Stop first");for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene");
 font=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");
 var root=R("ExpeditionEncounterPanel",null,0,0,1920,1080);var c=root.gameObject.AddComponent<ExpeditionEncounterPanel>();c.View=root.gameObject;
 var dim=I("Dim",root,0,0,1920,1080);dim.color=new Color(0,0,0,.52f);dim.raycastTarget=true;
 var main=R("Workspace",root,0,0,1920,1080);c.Workspace=main.gameObject.AddComponent<CanvasGroup>();
 I("Panel",main,28,238,720,692,A+"card-paper.png").color=new Color(.1f,.14f,.14f);
 I("EventPaper",main,48,254,680,256,A+"card-paper.png");T("Heading",main,72,268,580,66,"가까운 발소리",43,false);c.Body=T("Body",main,72,348,485,100,"",29,false);c.EnemyLabel=T("Enemy",main,72,453,435,42,"감염자",32,false);
 I("EnemySilhouette",main,575,312,114,184,"Assets/Art/Tokens/infected-body.png").preserveAspect=true;
 c.Fight=B("Fight",main,48,530,680,100,"교전",35);c.Wait=B("Wait",main,48,645,680,100,"숨어 기다리기",35);c.Retreat=B("Retreat",main,48,760,680,100,"복도로 물러나기",35);
 string[] desc={"전투 연결 예정","1턴 · 감염자가 지나가길 기다립니다.","원정대 전체 1턴 · 수색도와 물건 보존"};int n=0;foreach(var b in new[]{c.Fight,c.Wait,c.Retreat}){var label=b.GetComponentInChildren<Text>();label.rectTransform.sizeDelta=new Vector2(640,51);label.rectTransform.anchoredPosition=new Vector2(20,0);T("Description",b.transform,20,50,640,44,desc[n++],25,false).alignment=TextAnchor.MiddleCenter;}
 c.Fight.interactable=false;c.Hint=T("Hint",main,52,872,675,54,"",24);
 var vp=R("Members",main,1030,774,810,180);vp.gameObject.AddComponent<RectMask2D>();vp.gameObject.AddComponent<Image>().color=Color.clear;c.Members=R("Content",vp,0,0,810,172);var layout=c.Members.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=14;layout.childControlWidth=layout.childControlHeight=layout.childForceExpandWidth=layout.childForceExpandHeight=false;c.Members.gameObject.AddComponent<ContentSizeFitter>().horizontalFit=ContentSizeFitter.FitMode.PreferredSize;var sc=vp.gameObject.AddComponent<ScrollRect>();sc.viewport=vp;sc.content=c.Members;sc.horizontal=true;sc.vertical=false;sc.movementType=ScrollRect.MovementType.Clamped;
 var review=R("EncounterChoiceReview",null,0,0,1920,1080);var rd=I("Dim",review,0,0,1920,1080);rd.color=new Color(0,0,0,.72f);rd.raycastTarget=true;I("Paper",review,460,250,1000,550,A+"card-paper.png");T("Title",review,510,285,900,65,"행동 확인",40,false);T("Body",review,510,380,900,250,"",30,false);B("Cancel",review,510,680,410,78,"선택으로");B("Confirm",review,1000,680,410,78,"진행 · 1턴");var rp=Save(review.gameObject,"EncounterChoiceReview");var rv=(GameObject)PrefabUtility.InstantiatePrefab(rp,root);c.Review=rv;c.ReviewBody=rv.transform.Find("Body").GetComponent<Text>();c.Cancel=rv.transform.Find("Cancel").GetComponent<Button>();c.Confirm=rv.transform.Find("Confirm").GetComponent<Button>();rv.SetActive(false);
 var asset=Save(root.gameObject,"ExpeditionEncounterPanel");var a=PrefabUtility.LoadPrefabContents(P+"ExpeditionArrivalPanel.prefab");try{var old=a.transform.Find("ExpeditionEncounterPanel");if(old)Object.DestroyImmediate(old.gameObject);var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,a.transform);var arrival=a.GetComponent<ExpeditionArrivalPanel>();arrival.Encounter=go.GetComponent<ExpeditionEncounterPanel>();var existingBattle=a.transform.Find("ExpeditionBattlePanel");if(existingBattle){arrival.Encounter.Battle=existingBattle.GetComponent<ExpeditionBattlePanel>();arrival.Encounter.Fight.interactable=true;arrival.Encounter.Fight.transform.Find("Description").GetComponent<Text>().text="진형을 펼치고 교전합니다.";PrefabUtility.RecordPrefabInstancePropertyModifications(arrival.Encounter.Fight);PrefabUtility.RecordPrefabInstancePropertyModifications(arrival.Encounter.Fight.transform.Find("Description").GetComponent<Text>());}go.SetActive(false);
 old=a.transform.Find("WarningBanner");if(old)Object.DestroyImmediate(old.gameObject);var banner=R("WarningBanner",a.transform,850,165,990,70);I("Paper",banner,0,0,990,70,A+"footer-paper.png").color=new Color(.89f,.63f,.49f);arrival.Encounter.Banner=banner.gameObject;arrival.Encounter.BannerText=T("Text",banner,25,0,940,70,"",31,false);banner.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(arrival.Encounter);arrival.Fade.transform.SetAsLastSibling();PrefabUtility.SaveAsPrefabAsset(a,P+"ExpeditionArrivalPanel.prefab");}finally{PrefabUtility.UnloadPrefabContents(a);}
 EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");AssetDatabase.SaveAssets();return "Encounter and choice confirmation prefabs saved; warning banner wired.";
 }
}
