using System;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public static class BuildLaundryStoryVisit
{
 const string P="Assets/Prefabs/Settlement/",A="Assets/Art/PartySelection/";
 static void Place(Transform t,float x,float y,float w,float h){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
 static Image Image(Transform p,string n,float x,float y,float w,float h,Sprite sprite,Color color){var g=new GameObject(n,typeof(RectTransform));g.transform.SetParent(p,false);Place(g.transform,x,y,w,h);var im=g.AddComponent<Image>();im.sprite=sprite;im.color=color;im.raycastTarget=false;return im;}
 static Text Text(Transform p,string n,Text source,float x,float y,float w,float h,string value,int size){var t=Object.Instantiate(source,p,false);t.name=n;Place(t.transform,x,y,w,h);t.text=value;t.fontSize=size;t.color=new Color(.06f,.075f,.07f);t.alignment=TextAnchor.MiddleLeft;return t;}
 static Button Button(Transform p,string n,Button source,float x,float y,float w,float h,string value){var b=Object.Instantiate(source,p,false);b.name=n;b.onClick=new Button.ButtonClickedEvent();Place(b.transform,x,y,w,h);var t=b.GetComponentInChildren<Text>();Place(t.transform,15,0,w-30,h);t.text=value;t.fontSize=26;return b;}
 static void MatchExplorationHud(GameObject view)
 {
  var c=AssetDatabase.LoadAssetAtPath<GameObject>(P+"SettlementScreen.prefab").GetComponent<SettlementController>();var source=c.ArrivalPanel.Main.transform;
  var t=view.transform;var body=t.Find("Body").GetComponent<Text>();var hud=view.AddComponent<LaundryVisitHud>();hud.MemberPrefab=c.ArrivalPanel.MemberPrefab;
  foreach(var name in new[]{"ContextPaper","HeadingPaper"})Object.DestroyImmediate(t.Find(name).gameObject);
  foreach(var name in new[]{"PlacePaper","DayPaper"}){var clone=Object.Instantiate(source.Find(name).gameObject,t,false);clone.name=name;clone.transform.SetSiblingIndex(2);}
  var map=t.Find("RouteMap");var mapFrame=new GameObject("MapViewport",typeof(RectTransform),typeof(RectMask2D));mapFrame.transform.SetParent(t,false);Place(mapFrame.transform,42,166,1843,580);mapFrame.transform.SetSiblingIndex(1);
  map.SetParent(mapFrame.transform,false);var mapImage=map.GetComponent<Image>();mapImage.preserveAspect=true;
  var aspect=map.gameObject.AddComponent<AspectRatioFitter>();aspect.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;aspect.aspectRatio=mapImage.sprite.rect.width/mapImage.sprite.rect.height;
  Place(t.Find("Heading"),68,36,380,52);t.Find("Heading").GetComponent<Text>().fontSize=34;
  hud.Route=Text(t,"Route",body,68,94,380,36,"",23);
  hud.Clock=Text(t,"DayTime",body,1512,36,346,52,"",34);
  hud.TimeNote=Text(t,"TimeNote",body,1512,96,346,36,"",22);
  // Copy the actual approved three-panel tray, removing its live mall-only behaviour.
  var tray=Object.Instantiate(source.Find("HudTray").gameObject,t,false);tray.name="HudTray";tray.transform.SetSiblingIndex(2);
  var behaviour=tray.GetComponent<FieldHudTray>();if(behaviour)Object.DestroyImmediate(behaviour);
  var group=tray.GetComponent<CanvasGroup>();if(group){group.alpha=1;group.interactable=group.blocksRaycasts=true;}
  foreach(var name in new[]{"DangerRow","StatusWarn"}){var old=tray.transform.Find(name);if(old)old.gameObject.SetActive(false);}
  hud.ContextTab=tray.transform.Find("TurnTab/Label").GetComponent<Text>();hud.ContextTab.text="현재 대화";
  var member=Object.Instantiate(source.Find("Members").gameObject,t,false);member.name="Members";
  hud.MemberScroll=member.GetComponent<ScrollRect>();hud.MemberContent=hud.MemberScroll.content;
  foreach(Transform child in hud.MemberContent)Object.DestroyImmediate(child.gameObject);
  hud.MemberCount=Text(t,"MemberCount",body,530,791,348,34,"",20);hud.MemberCount.color=new Color(.75f,.78f,.74f);hud.MemberCount.alignment=TextAnchor.MiddleRight;
  // Visible scroll rail above the cards; six members retain their original card size.
  var rail=Image(t,"MemberRail",220,806,300,9,null,new Color(.25f,.31f,.31f));var handle=Image(rail.transform,"Handle",0,0,100,9,null,new Color(.84f,.78f,.59f));
  handle.rectTransform.anchorMin=Vector2.zero;handle.rectTransform.anchorMax=Vector2.one;handle.rectTransform.pivot=new Vector2(.5f,.5f);handle.rectTransform.sizeDelta=Vector2.zero;handle.rectTransform.anchoredPosition=Vector2.zero;
  handle.raycastTarget=true;var sb=rail.gameObject.AddComponent<Scrollbar>();sb.targetGraphic=handle;sb.handleRect=handle.rectTransform;sb.direction=Scrollbar.Direction.LeftToRight;
  hud.MemberScroll.horizontalScrollbar=sb;hud.MemberScroll.horizontalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;hud.MemberScroll.scrollSensitivity=40;
  Place(t.Find("Paper"),42,516,1843,248);Place(t.Find("Portrait"),74,552,140,160);Place(t.Find("Speaker"),244,532,440,50);Place(t.Find("Body"),244,593,1570,125);
  Place(t.Find("SceneTitle"),900,532,920,50);t.Find("SceneTitle").gameObject.SetActive(false);
  Place(t.Find("Context"),944,838,372,86);t.Find("Context").GetComponent<Text>().fontSize=23;t.Find("Context").GetComponent<Text>().color=new Color(.93f,.91f,.85f);
  Place(t.Find("ReadingHint"),944,949,372,90);var hint=t.Find("ReadingHint").GetComponent<Text>();hint.fontSize=21;hint.alignment=TextAnchor.UpperLeft;
  var next=t.Find("Next");Place(next,1370,834,514,134);Place(next.Find("Label"),16,0,482,134);next.GetComponentInChildren<Text>().fontSize=32;
  var ret=t.Find("Return");Place(ret,1370,984,514,64);Place(ret.Find("Label"),16,0,482,64);ret.GetComponentInChildren<Text>().fontSize=25;ret.GetComponentInChildren<Text>().color=new Color(.93f,.91f,.85f);
  Object.DestroyImmediate(ret.GetComponent<Image>());var returnFrame=ret.gameObject.AddComponent<FieldPanelGraphic>();returnFrame.color=new Color(.227f,.275f,.275f);returnFrame.Border=new Color(.47f,.51f,.5f);ret.GetComponent<Button>().targetGraphic=returnFrame;
  foreach(var tuple in new[]{("AskWhere",834f),("AskLetter",909f)}){var b=t.Find(tuple.Item1);Place(b,1370,tuple.Item2,514,62);Place(b.Find("Label"),12,0,490,62);b.GetComponentInChildren<Text>().fontSize=22;}
 }
 public static string Run()
 {
  if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
  var paper=AssetDatabase.LoadAssetAtPath<Sprite>(A+"footer-paper.png");var map=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ExpeditionPlan/map-clean.png");
  var view=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(P+"MissingPersonStoryView.prefab"));view.name="LaundryStoryVisitView";
  var t=view.transform;var dim=t.Find("Dim").GetComponent<Image>();dim.color=new Color(.035f,.07f,.075f,1);
  Image(t,"RouteMap",80,140,1760,545,map,new Color(.45f,.48f,.43f)).transform.SetSiblingIndex(1);
  Image(t,"ContextPaper",110,185,1110,200,paper,Color.white);
  Image(t,"HeadingPaper",80,32,710,76,paper,Color.white);
  var body=t.Find("Body").GetComponent<Text>();Text(t,"Heading",body,106,38,660,64,"미란 세탁소",36);
  Text(t,"Context",body,146,212,1040,148,"",28);
  t.Find("SceneTitle").GetComponent<Text>().text="";
  var next=t.Find("Next").GetComponent<Button>();Button(t,"Return",next,1450,32,390,76,"거점으로 돌아가기");
  Button(t,"AskWhere",next,282,891,720,62,"금례 씨는 어디에 있나요?");Button(t,"AskLetter",next,1030,891,720,62,"연락을 남기셨나요?");
  MatchExplorationHud(view);
  var asset=PrefabUtility.SaveAsPrefabAsset(view,P+"LaundryStoryVisitView.prefab");Object.DestroyImmediate(view);
  var root=PrefabUtility.LoadPrefabContents(P+"SettlementScreen.prefab");
  try{
   var c=root.GetComponent<SettlementController>();var v=root.GetComponent<LaundryStoryVisit>()??root.AddComponent<LaundryStoryVisit>();v.Owner=c;c.Laundry=v;
   var old=root.transform.Find("LaundryStoryVisitView");if(old)Object.DestroyImmediate(old.gameObject);
   v.View=(GameObject)PrefabUtility.InstantiatePrefab(asset,root.transform);v.View.name="LaundryStoryVisitView";t=v.View.transform;
   v.Hud=v.View.GetComponent<LaundryVisitHud>();v.Next=t.Find("Next").GetComponent<Button>();v.Surface=t.Find("Paper").GetComponent<Button>();v.Return=t.Find("Return").GetComponent<Button>();v.AskWhere=t.Find("AskWhere").GetComponent<Button>();v.AskLetter=t.Find("AskLetter").GetComponent<Button>();
   v.Heading=t.Find("Heading").GetComponent<Text>();v.Context=t.Find("Context").GetComponent<Text>();v.Speaker=t.Find("Speaker").GetComponent<Text>();v.Body=t.Find("Body").GetComponent<Text>();v.Hint=t.Find("ReadingHint").GetComponent<Text>();v.NextLabel=v.Next.GetComponentInChildren<Text>();v.Portrait=t.Find("Portrait").GetComponent<Image>();v.View.SetActive(false);
   var p=c.ExpeditionPanel;int index=Array.FindIndex(p.Destinations,d=>d.Id==LaundryStoryVisit.DestinationId);
   if(index<0){index=p.Destinations.Length;p.Destinations=p.Destinations.Concat(new[]{new ExpeditionPlanPanel.Destination()}).ToArray();var marker=Object.Instantiate(p.Markers[1],p.Markers[1].transform.parent,false);marker.name="Marker_Laundry";p.Markers=p.Markers.Concat(new[]{marker}).ToArray();p.PinFills=p.PinFills.Concat(new[]{marker.transform.Find("Pin").GetComponent<Graphic>()}).ToArray();p.MarkerPapers=p.MarkerPapers.Concat(new[]{marker.transform.Find("NamePaper").GetComponent<Image>()}).ToArray();}
   p.Destinations[index]=new ExpeditionPlanPanel.Destination{Id="laundry",Name="미란 세탁소",Description="표찰이 가리킨 곳",Resources="소식 확인",Unknown="표찰 주소 미확인",Risk="전투 없음",Accessible=false,OneWayMinutes=20,Picture=map,ResourceIcons=Array.Empty<Sprite>(),ResourceLabels=Array.Empty<string>()};
   var markerT=p.Markers[index].transform;Place(markerT,800,295,200,139);Place(markerT.Find("NamePaper"),-33,101,200,38);Place(markerT.Find("Name"),-28,101,190,38);markerT.Find("Name").GetComponent<Text>().text="미란 세탁소";markerT.Find("Name").GetComponent<Text>().fontSize=25;
   p.PlaceName.resizeTextForBestFit=true;p.PlaceName.resizeTextMinSize=25;p.PlaceName.resizeTextMaxSize=43;
   foreach(var co in root.GetComponentsInChildren<Component>(true))if(co&&PrefabUtility.IsPartOfPrefabInstance(co))PrefabUtility.RecordPrefabInstancePropertyModifications(co);
   PrefabUtility.SaveAsPrefabAsset(root,P+"SettlementScreen.prefab");
  }finally{PrefabUtility.UnloadPrefabContents(root);}
  AssetDatabase.SaveAssets();return "PASS: laundry route pin and reusable dialogue-visit prefab wired.";
 }
}
