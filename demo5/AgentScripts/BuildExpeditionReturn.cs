using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class BuildExpeditionReturn {
 const string P="Assets/Prefabs/Settlement/",A="Assets/Art/PartySelection/";
 static Font font;
 static RectTransform R(string n,Transform p,float x,float y,float w,float h){var r=new GameObject(n,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(p,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
 static Image I(string n,Transform p,float x,float y,float w,float h,string sprite){var im=R(n,p,x,y,w,h).gameObject.AddComponent<Image>();if(sprite!=null)im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(sprite);im.raycastTarget=false;return im;}
 static Text T(string n,Transform p,float x,float y,float w,float h,string text,int size){var t=R(n,p,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.color=new Color(.045f,.065f,.06f);t.text=text;t.raycastTarget=false;return t;}
 static Button B(string n,Transform p,float x,float y,float w,float h,string text){var im=I(n,p,x,y,w,h,A+"footer-paper.png");im.raycastTarget=true;var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;T("Label",im.transform,8,0,w-16,h,text,26).alignment=TextAnchor.MiddleCenter;return b;}
 static ScrollRect List(string name,Transform parent,float x,float y,float w,float h,out RectTransform content){
  var root=R(name,parent,x,y,w,h);var scroll=root.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=40;
  var viewport=I("Viewport",root,0,0,w,h,null);viewport.color=new Color(1,1,1,.015f);viewport.raycastTarget=true;viewport.gameObject.AddComponent<RectMask2D>();scroll.viewport=viewport.rectTransform;
  content=R("Content",viewport.transform,0,0,w,h);scroll.content=content;var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=12;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=false;content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
  var hint=R("MoreBelow",root,w-34,h-24,26,16);hint.gameObject.AddComponent<RoundedDownArrow>().color=new Color(.1f,.12f,.1f,.4f);var fade=hint.gameObject.AddComponent<CanvasGroup>();fade.blocksRaycasts=false;var more=hint.gameObject.AddComponent<ScrollMoreIndicator>();more.Scroll=scroll;more.Visibility=fade;return scroll;
 }
 static GameObject SaveRow(GameObject go,string name,float width,float height){var layout=go.AddComponent<LayoutElement>();layout.preferredWidth=width;layout.preferredHeight=height;var saved=PrefabUtility.SaveAsPrefabAsset(go,P+name+".prefab");Object.DestroyImmediate(go);return saved;}
 public static string Build(){
  if(EditorApplication.isPlaying)throw new Exception("Stop first");
  font=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");
  var member=I("ReturnMemberRow",null,0,0,420,146,A+"footer-paper.png");var mr=member.gameObject.AddComponent<ReturnMemberRow>();
  mr.Portrait=I("Portrait",member.transform,18,22,86,106,A+"portrait-scout.png");mr.Portrait.preserveAspect=true;
  mr.Name=T("Name",member.transform,126,12,274,40,"대원",27);mr.Health=T("Health",member.transform,126,50,270,42,"3 → 2 / 3",22);
  var track=I("HealthTrack",member.transform,126,94,246,12,null);track.color=new Color(.16f,.21f,.18f);mr.HealthFill=I("HealthFill",track.transform,2,2,242,8,A+"count-paper.png");mr.HealthFill.color=new Color(.38f,.57f,.35f);mr.HealthFill.type=Image.Type.Filled;mr.HealthFill.fillMethod=Image.FillMethod.Horizontal;
  mr.Change=T("Change",member.transform,126,112,274,30,"체력 -1",20);var memberPrefab=SaveRow(member.gameObject,"ReturnMemberRow",420,146);
  var item=I("ReturnItemRow",null,0,0,924,76,A+"footer-paper.png");var ir=item.gameObject.AddComponent<ReturnItemRow>();ir.Icon=I("Icon",item.transform,20,16,44,44,"Assets/Art/HomeSelection/icon-supplies.png");ir.Icon.preserveAspect=true;
  ir.Name=T("Name",item.transform,84,0,350,76,"물자",26);ir.Name.alignment=TextAnchor.MiddleLeft;
  ir.Before=T("Before",item.transform,492,0,100,76,"0",26);ir.After=T("After",item.transform,634,0,100,76,"2",28);ir.Change=T("Change",item.transform,786,0,110,76,"+2",28);foreach(var text in new[]{ir.Before,ir.After,ir.Change})text.alignment=TextAnchor.MiddleCenter;
  var itemPrefab=SaveRow(item.gameObject,"ReturnItemRow",924,76);
  var root=R("ExpeditionReturnPanel",null,0,0,1920,1080);var c=root.gameObject.AddComponent<ExpeditionReturnPanel>();c.View=root.gameObject;c.MemberPrefab=memberPrefab.GetComponent<ReturnMemberRow>();c.ItemPrefab=itemPrefab.GetComponent<ReturnItemRow>();
  var dim=I("Dim",root,0,0,1920,1080,null);dim.color=new Color(0,0,0,.76f);dim.raycastTarget=true;
  var paper=I("Paper",root,220,120,1480,804,A+"card-paper.png");
  c.Heading=T("Title",paper.transform,48,28,1000,60,"원정 귀환",40);c.Overview=T("Overview",paper.transform,48,94,1040,44,"",26);
  var durationPaper=I("DurationPaper",paper.transform,1176,42,256,72,A+"footer-paper.png");c.Duration=T("Duration",durationPaper.transform,8,0,240,72,"50분 경과",28);c.Duration.alignment=TextAnchor.MiddleCenter;
  T("MembersHeading",paper.transform,48,174,310,44,"돌아온 대원",28);c.MemberCount=T("MemberCount",paper.transform,358,174,110,44,"2명",26);c.MemberCount.alignment=TextAnchor.UpperRight;
  T("ItemsHeading",paper.transform,508,174,660,44,"휴대품 정산",28);c.ItemCount=T("ItemCount",paper.transform,1322,174,110,44,"4종",26);c.ItemCount.alignment=TextAnchor.UpperRight;
  T("HealthLegend",paper.transform,48,230,420,38,"체력 · 출발 → 귀환",22);
  T("ItemLegend",paper.transform,592,230,350,38,"물자",22);
  foreach(var pair in new[]{(1000f,"출발"),(1142f,"귀환"),(1294f,"증감")}){var label=T("Column_"+pair.Item2,paper.transform,pair.Item1,230,pair.Item2=="증감"?110:100,38,pair.Item2,22);label.alignment=TextAnchor.MiddleCenter;}
  c.MemberScroll=List("Members",paper.transform,48,284,420,414,out c.MemberContent);c.Scroll=List("Items",paper.transform,508,284,924,414,out c.ItemContent);
  c.EmptyItems=T("EmptyItems",paper.transform,508,370,924,120,"가져온 물건이 없습니다.",28);c.EmptyItems.alignment=TextAnchor.MiddleCenter;
  c.Body=T("ReportData",root,0,0,1,1,"",12);c.Body.gameObject.SetActive(false);
  c.Notice=T("Notice",paper.transform,48,724,1384,36,"",22);T("DeltaLegend",paper.transform,48,758,1384,28,"증감은 획득·사용·현장에 둔 물건을 합산한 값입니다.",18);
  c.Back=B("Back",root,28,974,320,76,"돌아가기");c.InspectBags=B("InspectBags",root,1218,974,320,76,"가방별 정리");c.StoreAll=B("StoreAll",root,1572,974,320,76,"휴대품 모두 보관");
  var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,P+"ExpeditionReturnPanel.prefab");Object.DestroyImmediate(root.gameObject);
  var screen=PrefabUtility.LoadPrefabContents(P+"SettlementScreen.prefab");try{var old=screen.transform.Find("ExpeditionReturnPanel");if(old)Object.DestroyImmediate(old.gameObject);var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,screen.transform);var owner=screen.GetComponent<SettlementController>();owner.ReturnPanel=go.GetComponent<ExpeditionReturnPanel>();owner.ReturnPanel.HideWhileOpen=new[]{owner.Main.transform.Find("Roster").gameObject,owner.Main.transform.Find("ArrivalNotice").gameObject,owner.Previous.gameObject,owner.Next.gameObject};PrefabUtility.RecordPrefabInstancePropertyModifications(owner.ReturnPanel);go.SetActive(false);PrefabUtility.SaveAsPrefabAsset(screen,P+"SettlementScreen.prefab");}finally{PrefabUtility.UnloadPrefabContents(screen);}
  AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Portrait/health cards and icon quantity table; separate scroll areas and reusable row prefabs.";
 }
}

