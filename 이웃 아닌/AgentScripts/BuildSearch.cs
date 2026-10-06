using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class BuildSearch {
 const string P="Assets/Prefabs/Settlement/",A="Assets/Art/PartySelection/";static Font font;
 static RectTransform R(string n,Transform p,float x,float y,float w,float h){var r=new GameObject(n,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(p,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
 static Image I(string n,Transform p,float x,float y,float w,float h,string path=null){var im=R(n,p,x,y,w,h).gameObject.AddComponent<Image>();if(path!=null)im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);im.raycastTarget=false;return im;}
 static Text T(string n,Transform p,float x,float y,float w,float h,string s,int size=28,bool light=true){var t=R(n,p,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.fontStyle=FontStyle.Normal;t.fontSize=size;t.text=s;t.color=light?new Color(.96f,.93f,.85f):new Color(.045f,.065f,.06f);t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;return t;}
 static Button B(string n,Transform p,float x,float y,float w,float h,string label,int size=32){var im=I(n,p,x,y,w,h,A+"footer-paper.png");im.raycastTarget=true;var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;T("Label",im.transform,8,0,w-16,h,label,size,false).alignment=TextAnchor.MiddleCenter;return b;}
 static GameObject Save(GameObject g,string name){var asset=PrefabUtility.SaveAsPrefabAsset(g,P+name+".prefab");Object.DestroyImmediate(g);return asset;}
 public static string Build(){
 if(EditorApplication.isPlaying)throw new Exception("Stop first");for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene");
 font=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");
 var member=R("SearchWorkerCard",null,0,0,132,126);var card=member.gameObject.AddComponent<ExpeditionMemberCard>();card.Paper=member.gameObject.AddComponent<Image>();card.Paper.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(A+"card-paper.png");card.Button=member.gameObject.AddComponent<Button>();card.Button.targetGraphic=card.Paper;card.Portrait=I("Portrait",member,24,7,84,77);card.Portrait.preserveAspect=true;card.Name=T("Name",member,5,86,122,34,"",26,false);card.Name.alignment=TextAnchor.MiddleCenter;var ca=Save(member.gameObject,"SearchWorkerCard");
 var root=R("ExpeditionSearchPanel",null,0,0,1920,1080);var c=root.gameObject.AddComponent<ExpeditionSearchPanel>();c.View=root.gameObject;
 var dim=I("Dim",root,0,0,1920,1080);dim.color=new Color(0,0,0,.97f);dim.raycastTarget=true;
 var main=R("Workspace",root,0,0,1920,1080);c.Workspace=main.gameObject.AddComponent<CanvasGroup>();
 I("Panel",main,28,238,654,694,A+"card-paper.png").color=new Color(.1f,.14f,.14f,.98f);
 I("TitlePaper",main,48,250,270,62,A+"footer-paper.png");c.Title=T("Title",main,68,250,230,62,"물자 상자",36,false);
 I("ObjectPaper",main,48,326,130,100,A+"card-paper.png");c.ObjectIcon=I("ObjectIcon",main,75,340,76,72);c.ObjectIcon.preserveAspect=true;c.Description=T("Description",main,198,326,452,100,"",26);
 T("PaceHeading",main,48,433,604,34,"수색 방식",29);c.Paces=new Button[3];for(int i=0;i<3;i++)c.Paces[i]=B("Pace_"+i,main,48+i*204,475,194,52,new[]{"빠름","보통","정밀"}[i]);
 T("WorkerHeading",main,48,538,604,34,"담당자 · 옆으로 넘겨 선택",29);
 var vp=R("Workers",main,48,578,606,132);vp.gameObject.AddComponent<RectMask2D>();vp.gameObject.AddComponent<Image>().color=Color.clear;c.MemberContent=R("Content",vp,0,0,606,126);var layout=c.MemberContent.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=12;layout.childControlWidth=layout.childControlHeight=layout.childForceExpandWidth=layout.childForceExpandHeight=false;c.MemberContent.gameObject.AddComponent<ContentSizeFitter>().horizontalFit=ContentSizeFitter.FitMode.PreferredSize;var scroll=vp.gameObject.AddComponent<ScrollRect>();scroll.viewport=vp;scroll.content=c.MemberContent;scroll.horizontal=true;scroll.vertical=false;scroll.movementType=ScrollRect.MovementType.Clamped;c.MemberPrefab=ca.GetComponent<ExpeditionMemberCard>();
 T("DutyHeading",main,48,716,604,32,"나머지 동료 역할",29);c.Duties=new Button[3];for(int i=0;i<3;i++)c.Duties[i]=B("Duty_"+i,main,48+i*204,755,194,48,new[]{"함께 수색","망보기","조명 지원"}[i],29);
 c.Equipment=T("Equipment",main,48,808,604,30,"",24);c.Cost=T("Cost",main,48,845,604,68,"",27);
 c.Back=B("Back",main,80,952,410,78,"돌아가기",34);c.Choose=B("Choose",main,1030,952,410,78,"배정 확인",34);c.Choose.GetComponent<Image>().color=new Color(.76f,.86f,.68f);
 c.Notice=T("Notice",main,760,925,1080,27,"",26);
 var review=R("SearchAssignmentReview",null,0,0,1920,1080);var rd=I("Dim",review,0,0,1920,1080);rd.color=new Color(0,0,0,.72f);rd.raycastTarget=true;I("Paper",review,460,205,1000,650,A+"card-paper.png");T("Title",review,510,235,900,65,"수색 배정 확인",40,false);T("Body",review,510,320,900,355,"",29,false);B("Back",review,510,727,410,78,"돌아가기");B("Confirm",review,1000,727,410,78,"배정 저장").GetComponent<Image>().color=new Color(.76f,.86f,.68f);var ra=Save(review.gameObject,"SearchAssignmentReview");var rv=(GameObject)PrefabUtility.InstantiatePrefab(ra,root);c.Review=rv;c.ReviewBody=rv.transform.Find("Body").GetComponent<Text>();c.Cancel=rv.transform.Find("Back").GetComponent<Button>();c.Confirm=rv.transform.Find("Confirm").GetComponent<Button>();rv.SetActive(false);
 c.ObjectIcons=new Sprite[3];for(int i=0;i<3;i++)c.ObjectIcons[i]=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Settlement/icon-"+new[]{"storage","work","journal"}[i]+".png");
 var prefab=Save(root.gameObject,"ExpeditionSearchPanel");var arrival=PrefabUtility.LoadPrefabContents(P+"ExpeditionArrivalPanel.prefab");try{var old=arrival.transform.Find("ExpeditionSearchPanel");if(old)Object.DestroyImmediate(old.gameObject);var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,arrival.transform);var a=arrival.GetComponent<ExpeditionArrivalPanel>();a.Search=go.GetComponent<ExpeditionSearchPanel>();go.SetActive(false);a.Fade.transform.SetAsLastSibling();PrefabUtility.SaveAsPrefabAsset(arrival,P+"ExpeditionArrivalPanel.prefab");}finally{PrefabUtility.UnloadPrefabContents(arrival);}
 EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");AssetDatabase.SaveAssets();return "Search screen, worker card and confirmation prefabs saved.";
 }
}

