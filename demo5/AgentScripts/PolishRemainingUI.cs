using System;using System.Linq;using UnityEditor;using UnityEngine;using UnityEngine.UI;using Demo5.FrontEnd;using Object=UnityEngine.Object;
// Apply after the earlier common-frame builders. Operates on assets through the live Editor.
public static class PolishRemainingUI {
 const string P="Assets/Prefabs/";
 static readonly Color Ink=new Color(.045f,.065f,.06f),Light=new Color(.96f,.93f,.85f);
 static void R(Component c,float x,float y,float w,float h){var r=(RectTransform)c.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
 static RectTransform Child(Transform p,string name){var t=p.Find(name);if(t)return (RectTransform)t;var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(p,false);return r;}
 static Text Label(Transform p,string name,Font font,float x,float y,float w,float h,string value,int size){var r=Child(p,name);R(r,x,y,w,h);var t=r.GetComponent<Text>()??r.gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.text=value;t.alignment=TextAnchor.MiddleLeft;t.color=Ink;t.raycastTarget=false;return t;}
 static Image Image(Transform p,string name,float x,float y,float w,float h,Color color){var r=Child(p,name);R(r,x,y,w,h);var i=r.GetComponent<Image>()??r.gameObject.AddComponent<Image>();i.color=color;i.raycastTarget=false;return i;}
 static Button Arrow(Transform p,string name,float x,float y,bool up){var im=Image(p,name,x,y,30,30,Ink);im.raycastTarget=true;var b=im.GetComponent<Button>()??im.gameObject.AddComponent<Button>();b.targetGraphic=im;
  var r=Child(im.transform,"Arrow");R(r,7,8,16,14);if(!r.GetComponent<CanvasRenderer>())r.gameObject.AddComponent<CanvasRenderer>();var a=r.GetComponent<RoundedDownArrow>()??r.gameObject.AddComponent<RoundedDownArrow>();a.color=new Color(1,.8f,.32f);a.raycastTarget=false;if(up){r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(15,-15);r.localRotation=Quaternion.Euler(0,0,180);}return b;}
 static void Navigation(ScrollRect s,float width,float height){
  // Content is clipped separately so the rail and arrows stay fully visible.
  var root=(RectTransform)s.transform;root.sizeDelta=new Vector2(width,height);
  foreach(var mask in s.GetComponents<RectMask2D>())Object.DestroyImmediate(mask);
  var v=Child(root,"VisibleContent");R(v,0,0,width-44,height);if(!v.GetComponent<RectMask2D>())v.gameObject.AddComponent<RectMask2D>();Image(v,"HitArea",0,0,width-44,height,Color.clear).raycastTarget=true;v.Find("HitArea").SetAsFirstSibling();
  s.content.SetParent(v,false);s.content.anchorMin=s.content.anchorMax=s.content.pivot=new Vector2(0,1);s.content.anchoredPosition=Vector2.zero;s.content.sizeDelta=new Vector2(width-44,s.content.sizeDelta.y);s.viewport=v;s.scrollSensitivity=50;s.horizontal=false;s.vertical=true;s.movementType=ScrollRect.MovementType.Clamped;
  if(s.verticalScrollbar&&(s.verticalScrollbar.transform.parent!=root||s.verticalScrollbar.name!="ScrollRail"))s.verticalScrollbar.gameObject.SetActive(false);
  var track=Image(root,"ScrollRail",width-26,40,16,height-80,Ink);var handle=Image(track.transform,"Handle",0,0,16,60,new Color(.95f,.68f,.22f));handle.raycastTarget=true;
  var bar=track.GetComponent<Scrollbar>()??track.gameObject.AddComponent<Scrollbar>();bar.targetGraphic=handle;bar.handleRect=handle.rectTransform;bar.direction=Scrollbar.Direction.BottomToTop;handle.rectTransform.sizeDelta=Vector2.zero;s.verticalScrollbar=bar;s.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
  foreach(var old in root.GetComponentsInChildren<Scrollbar>(true))if(old!=bar)old.gameObject.SetActive(false);
  var nav=s.GetComponent<ScrollNavigation>()??s.gameObject.AddComponent<ScrollNavigation>();nav.Scroll=s;nav.Up=Arrow(root,"ScrollUp",width-33,0,true);nav.Down=Arrow(root,"ScrollDown",width-33,height-30,false);
 }
 static void Review(GameObject g){var root=g.transform;var summary=root.Find("SummaryViewport/Summary")?.GetComponent<Text>()??root.GetComponentsInChildren<Text>(true).First(t=>t.name=="Summary");var s=summary.GetComponentInParent<ScrollRect>(true);
  R(s,510,326,900,352);Navigation(s,900,352);summary.fontSize=28;summary.lineSpacing=1;summary.supportRichText=true;summary.alignment=TextAnchor.UpperLeft;
  var font=summary.font;Label(root,"Destination",font,510,254,900,54,"목적지 · 동행 대원",28);
  Image(root,"FooterRule",510,700,900,2,new Color(.3f,.32f,.27f,.35f));Label(root,"DepartureNotice",font,510,712,900,66,"출발 시 이동 시간이 흐릅니다.",24);
 }
 static void Packing(ExpeditionPackingPanel p){Review(p.Review);p.ReviewDestination=p.Review.transform.Find("Destination").GetComponent<Text>();p.DepartureNotice=p.Review.transform.Find("DepartureNotice").GetComponent<Text>();p.ReviewScroll=p.Summary.GetComponentInParent<ScrollRect>(true);p.transform.Find("Workspace/Title").GetComponent<Text>().fontSize=34;p.Checklist.fontSize=22;p.Checklist.lineSpacing=1;}
 static void Loot(ExpeditionLootPanel p){R(p.Message,1238,788,560,114);p.Message.fontSize=26;p.Message.alignment=TextAnchor.UpperLeft;}
 static void Rest(SettlementWorkPanel p){var root=p.transform;var task=root.Find("TaskPaper");var detail=root.Find("SelectedMember");R(task,250,192,660,740);R(detail,958,240,660,692);
  foreach(var t in task.GetComponentsInChildren<Text>(true).Where(t=>t.name=="Heading")){t.fontSize=36;t.text="휴식 방법과 대원";R(t,107,20,505,67);}
  var headingPaper=task.Find("HeadingPaper");if(headingPaper)R(headingPaper,20,16,620,70);
  var vp=p.Scroll;R(vp,24,311,612,396);Navigation(vp,612,396);
  // Keep the existing dark task panel and cream selected-person card.
  foreach(string name in new[]{"DurationLabel","Duration","EffectLabel","Effect"}){var t=task.Find(name);if(t)t.SetParent(detail,false);}
  foreach(var im in task.GetComponentsInChildren<Image>(true).Where(i=>i.rectTransform.rect.height<=3&&i.rectTransform.anchoredPosition.y< -600))im.gameObject.SetActive(false);
  var clock=task.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.sprite&&i.sprite.name.Contains("clock"));if(clock)clock.gameObject.SetActive(false);
  R(p.DetailPortrait,34,36,156,178);R(p.DetailName,222,34,400,62);p.DetailName.fontSize=36;R(p.DetailState,222,104,400,65);p.DetailState.fontSize=25;R(p.DetailHealth,222,177,400,50);p.DetailHealth.fontSize=29;
  foreach(var im in detail.GetComponentsInChildren<Image>(true).Where(i=>i.rectTransform.rect.height<=3))R(im,32,252,596,2);
  R(p.DetailDescription,32,278,596,140);p.DetailDescription.fontSize=28;p.DetailDescription.alignment=TextAnchor.UpperLeft;
  var dl=detail.Find("DurationLabel").GetComponent<Text>();R(dl,32,434,255,52);dl.fontSize=29;dl.color=Ink;dl.text="소요 시간";
  R(p.Duration,300,434,328,52);p.Duration.color=Ink;p.Duration.fontSize=29;
  var el=detail.Find("EffectLabel").GetComponent<Text>();R(el,32,506,255,52);el.fontSize=29;el.color=Ink;el.text="회복 결과";
  R(p.Effect,290,506,338,52);p.Effect.color=Ink;p.Effect.fontSize=29;
  Label(detail,"ReservationHint",p.DetailName.font,32,586,596,76,"휴식 시작 후 시간을 진행하면\n회복 결과가 적용됩니다.",24);
 }
 static void Bag(ExpeditionBagPanel p){var layout=p.ProfileName.transform.parent;
  R(p.Description,1248,344,560,92);R(p.UseHint,1128,440,700,76);R(p.Use,1128,522,700,58);
  R(layout.Find("RecipientsHeading"),1128,594,236,52);R(p.Minus,1374,596,48,46);R(p.Quantity,1432,596,66,46);R(p.Plus,1508,596,48,46);R(p.Max,1572,596,104,46);
  var s=p.RightMembers.GetComponentInParent<ScrollRect>(true);R(s,1128,662,700,262);Navigation(s,700,262);var group=p.RightMembers.GetComponent<VerticalLayoutGroup>();if(group){group.childControlWidth=true;group.childForceExpandWidth=true;}
  var grid=p.RightMembers.GetComponent<GridLayoutGroup>();if(grid)grid.cellSize=new Vector2(320,76);
  var hint=layout.Find("RecipientHint");if(hint)hint.gameObject.SetActive(false);
  R(p.EmptyRight,1128,690,656,100);
 }
 static void Return(ExpeditionReturnPanel p){Navigation(p.MemberScroll,456,414);Navigation(p.Scroll,924,428);var legend=p.View.transform.Find("Paper/Column_증감");if(legend)R(legend,1252,230,110,38);}
 static void Time(SettlementTimePanel p){Navigation(p.Scroll,1104,396);}
 static void System(GameObject root){
  foreach(var m in root.GetComponentsInChildren<SettlementGameMenu>(true)){var dim=m.transform.Find("View/Dim")?.GetComponent<Image>();if(dim)dim.color=new Color(0,0,0,.97f);}
  foreach(var t in root.GetComponentsInChildren<Transform>(true)){
   if(t.name=="SettingsDialog"){var im=t.GetComponent<Image>();if(im)im.color=new Color(0,0,0,.97f);}
   if(t.name=="SaveSlotPanel"){var im=t.Find("Dim")?.GetComponent<Image>();if(im)im.color=new Color(0,0,0,.97f);}
  }
 }
 static void Edit(string name,Action<GameObject> action){string path=P+name+".prefab";var g=PrefabUtility.LoadPrefabContents(path);try{action(g);PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}}
 public static string Run(){if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
  Edit("Settlement/ReturnMemberRow",g=>{var row=g.GetComponent<ReturnMemberRow>();((RectTransform)g.transform).sizeDelta=new Vector2(412,130);var le=g.GetComponent<LayoutElement>();if(le){le.preferredHeight=130;le.minHeight=130;}R(row.Portrait,18,12,86,106);R(row.Name,126,0,274,40);R(row.Health,126,40,270,38);R(row.HealthFill.transform.parent,126,82,246,12);R(row.Change,126,96,274,30);});
  Edit("Settlement/ReturnItemRow",g=>{((RectTransform)g.transform).sizeDelta=new Vector2(880,76);R(g.GetComponent<ReturnItemRow>().Change,744,0,110,76);});
  Edit("Settlement/TimeWorkRow",g=>{((RectTransform)g.transform).sizeDelta=new Vector2(1060,124);R(g.GetComponent<TimeWorkRow>().Result,778,12,256,100);});
  Edit("Settlement/ExpeditionReturnPanel",g=>Return(g.GetComponent<ExpeditionReturnPanel>()));Edit("Settlement/SettlementTimePanel",g=>Time(g.GetComponent<SettlementTimePanel>()));
  Edit("Settlement/WorkAssigneeRow",g=>{var row=g.GetComponent<SettlementWorkRow>();((RectTransform)g.transform).sizeDelta=new Vector2(568,84);R(row.State,408,20,104,48);R(row.Check,522,24,32,32);});
  Edit("Settlement/ExpeditionPackingReview",Review);Edit("Settlement/ExpeditionPackingPanel",g=>Packing(g.GetComponent<ExpeditionPackingPanel>()));Edit("Settlement/ExpeditionLootPanel",g=>Loot(g.GetComponent<ExpeditionLootPanel>()));Edit("Settlement/RestWorkPanel",g=>Rest(g.GetComponent<SettlementWorkPanel>()));Edit("Settlement/ExpeditionBagPanel",g=>Bag(g.GetComponent<ExpeditionBagPanel>()));
  foreach(string n in new[]{"FrontEnd/SettingsDialog","FrontEnd/SaveSlotPanel","Settlement/SystemMenu"})Edit(n,System);
  // Original-colour inventory art: change the image tint only, preserving combat layout and rules.
  foreach(string n in new[]{"Settlement/BattleItemDrawer","Settlement/ExpeditionBattlePanel"})Edit(n,g=>{foreach(var drawer in g.GetComponentsInChildren<BattleItemDrawer>(true))drawer.DetailIcon.color=Color.white;});
  Edit("Settlement/SettlementScreen",g=>{var c=g.GetComponent<SettlementController>();Packing(c.PackingPanel);Loot(c.ArrivalPanel.Loot);Rest(c.WorkPanel);Bag(c.ArrivalPanel.FieldBags);System(g);foreach(var t in g.GetComponentsInChildren<Text>(true))if(t.text.Contains("시설 테두리 안을 클릭"))t.text="시설 위에 마우스 · Alt: 모든 시설 강조";var guide=g.GetComponent<SettlementTutorialGuide>();guide.HeaderPosition=new Vector2(720,-22);R(guide.Banner.transform,720,22,650,142);guide.Title.rectTransform.sizeDelta=new Vector2(606,42);guide.Instruction.rectTransform.sizeDelta=new Vector2(606,70);});
  Edit("Settlement/SettlementScreen",g=>{var c=g.GetComponent<SettlementController>();Return(c.ReturnPanel);Time(c.TimePanel);});
  AssetDatabase.SaveAssets();return "Applied packing review, loot guidance, grouped rest, recipients, popup dim, return and time list navigation; no combat rules or art changed.";
 }
}




