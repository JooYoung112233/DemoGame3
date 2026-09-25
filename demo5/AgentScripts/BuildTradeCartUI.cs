using System;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Additive visitor-only layout. Apply after older visitor / everyday builders.
// All geometry is serialized in the prefab; runtime changes content only.
public static class BuildTradeCartUI
{
 const string Folder="Assets/Prefabs/Settlement/";
 static Font font; static Sprite paper,button; static GameObject rowAsset;
 static readonly Color Ink=new Color(.045f,.065f,.06f),Light=new Color(.95f,.92f,.82f),Gold=new Color(1,.76f,.29f);
 static void Place(Transform t,float x,float y,float w,float h){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
 static RectTransform Node(Transform p,string name,float x,float y,float w,float h){var old=p?p.Find(name):null;var r=old?(RectTransform)old:new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();if(!old&&p)r.SetParent(p,false);Place(r,x,y,w,h);return r;}
 static Image Picture(Transform p,string name,float x,float y,float w,float h,Sprite sprite,Color color,bool raycast=false){var r=Node(p,name,x,y,w,h);var im=r.GetComponent<Image>()??r.gameObject.AddComponent<Image>();im.sprite=sprite;im.color=color;im.raycastTarget=raycast;return im;}
 static Text Label(Transform p,string name,float x,float y,float w,float h,string value,int size,Color color,TextAnchor align=TextAnchor.MiddleLeft){var r=Node(p,name,x,y,w,h);var t=r.GetComponent<Text>()??r.gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.text=value;t.color=color;t.alignment=align;t.raycastTarget=false;t.resizeTextForBestFit=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;}
 static Button Button(Transform p,string name,float x,float y,float w,float h,string value,int size=26){var im=Picture(p,name,x,y,w,h,button,Color.white,true);var b=im.GetComponent<Button>()??im.gameObject.AddComponent<Button>();b.targetGraphic=im;var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,.9f,.65f);colors.pressedColor=new Color(.8f,.68f,.38f);colors.disabledColor=new Color(.55f,.55f,.53f,.8f);b.colors=colors;Label(b.transform,"Label",4,0,w-8,h,value,size,Ink,TextAnchor.MiddleCenter);return b;}
 static void Hide(Transform parent,params string[] names){foreach(var name in names){var t=parent.Find(name);if(t)t.gameObject.SetActive(false);}}
 static void Mark(Transform root){foreach(var c in root.GetComponentsInChildren<Component>(true))if(c&&PrefabUtility.IsPartOfPrefabInstance(c))PrefabUtility.RecordPrefabInstancePropertyModifications(c);}
 static void RowPrefab()
 {
  var r=Node(null,"TradeCartRow",0,0,774,60);var row=r.gameObject.AddComponent<TradeCartRow>();
  var bg=r.gameObject.AddComponent<Image>();bg.sprite=button;bg.color=new Color(.93f,.91f,.83f);bg.raycastTarget=false;
  var layout=r.gameObject.AddComponent<LayoutElement>();layout.preferredHeight=60;layout.minHeight=60;layout.flexibleHeight=0;
  row.Icon=Picture(r,"Icon",10,6,52,48,null,Color.white);row.Icon.preserveAspect=true;
  row.NameLabel=Label(r,"Name",76,0,278,60,"소독 알코올",24,Ink);
  row.ValueLabel=Label(r,"Value",364,0,162,60,"가치 8",21,Ink,TextAnchor.MiddleRight);
  row.Minus=Button(r,"Minus",546,7,48,46,"−",28);row.CountLabel=Label(r,"Count",604,0,78,60,"2개",25,Ink,TextAnchor.MiddleCenter);row.Plus=Button(r,"Plus",696,7,48,46,"+",28);
  rowAsset=PrefabUtility.SaveAsPrefabAsset(r.gameObject,Folder+"TradeCartRow.prefab");Object.DestroyImmediate(r.gameObject);
 }
 static ScrollRect Cart(Transform parent,string name,float x,float y,float w,float h,int count,bool readOnly,out TradeCartRow[] rows)
 {
  var root=Node(parent,name,x,y,w,h);var scroll=root.GetComponent<ScrollRect>()??root.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.inertia=false;scroll.scrollSensitivity=62;
  var viewport=Node(root,"Viewport",0,0,w-38,h);if(!viewport.GetComponent<RectMask2D>())viewport.gameObject.AddComponent<RectMask2D>();var hit=viewport.GetComponent<Image>()??viewport.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
  var content=Node(viewport,"Content",0,0,w-38,0);var group=content.GetComponent<VerticalLayoutGroup>()??content.gameObject.AddComponent<VerticalLayoutGroup>();group.spacing=4;group.childControlHeight=true;group.childControlWidth=true;group.childForceExpandHeight=false;group.childForceExpandWidth=true;group.childAlignment=TextAnchor.UpperLeft;
  var fitter=content.GetComponent<ContentSizeFitter>()??content.gameObject.AddComponent<ContentSizeFitter>();fitter.horizontalFit=ContentSizeFitter.FitMode.Unconstrained;fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
  scroll.viewport=viewport;scroll.content=content;
  var track=Picture(root,"Track",w-25,34,18,h-68,null,new Color(.04f,.06f,.06f),true);var bar=track.GetComponent<Scrollbar>()??track.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;
  var sliding=Node(track.transform,"Sliding",0,0,18,h-68);var handle=Picture(sliding,"Handle",0,0,18,h-68,null,Gold,true);handle.rectTransform.anchorMin=Vector2.zero;handle.rectTransform.anchorMax=Vector2.one;handle.rectTransform.offsetMin=handle.rectTransform.offsetMax=Vector2.zero;bar.handleRect=handle.rectTransform;bar.targetGraphic=handle;scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
  var navigation=root.GetComponent<ScrollNavigation>()??root.gameObject.AddComponent<ScrollNavigation>();navigation.Scroll=scroll;navigation.Up=Button(root,"Up",w-32,0,32,28,"▲",18);navigation.Down=Button(root,"Down",w-32,h-28,32,28,"▼",18);
  rows=new TradeCartRow[count];
  for(int i=0;i<count;i++)
  {
   var existing=content.Find("Row"+i);var go=existing?existing.gameObject:(GameObject)PrefabUtility.InstantiatePrefab(rowAsset,content);go.name="Row"+i;Place(go.transform,0,i*64,w-38,60);rows[i]=go.GetComponent<TradeCartRow>();
   if(readOnly){Place(rows[i].NameLabel.transform,66,0,234,60);rows[i].NameLabel.fontSize=24;Place(rows[i].Icon.transform,8,7,46,46);Place(rows[i].CountLabel.transform,308,0,78,60);Place(rows[i].ValueLabel.transform,396,0,w-444,60);rows[i].ValueLabel.fontSize=21;rows[i].Minus.gameObject.SetActive(false);rows[i].Plus.gameObject.SetActive(false);}
   go.SetActive(false);Mark(go.transform);
  }
  return scroll;
 }
 static void Stock(SettlementVisitorPanel p,bool ours)
 {
  var slots=ours?p.OurSlots:p.TheirSlots;var root=slots[0].transform.parent;Place(root,ours?80:990,158,850,710);
  Place(root.Find("HeaderPaper"),20,6,810,58);var heading=root.Find(ours?"OurHeading":"TheirHeading").GetComponent<Text>();Place(heading.transform,36,12,778,48);heading.text=ours?"우리 창고 · 눌러서 내놓기":"방문자 물품 · 눌러서 받기";heading.fontSize=28;
  Hide(root,"DetailPaper","OfferPaper","GiveDetails","TakeDetails","Give","Take","GiveMinus","GivePlus","GiveQuantity","TakeMinus","TakePlus","TakeQuantity");
  for(int i=0;i<slots.Length;i++)
  {
   var s=slots[i];Place(s.transform,24+i%4*202,74+i/4*92,196,86);s.Paper.color=new Color(.82f,.82f,.74f);
   Place(s.Icon.transform,65,0,66,50);s.Icon.preserveAspect=true;s.Icon.raycastTarget=false;
   Place(s.Count.transform,147,0,40,34);s.Count.fontSize=22;s.Count.resizeTextForBestFit=true;s.Count.resizeTextMinSize=12;s.Count.resizeTextMaxSize=22;
   Place(s.Label.transform,6,50,184,36);s.Label.fontSize=22;s.Label.alignment=TextAnchor.MiddleCenter;s.Label.resizeTextForBestFit=false;
   Label(s.transform,"UnitValue",7,3,59,28,"가치 4",17,Ink);s.Selection.effectDistance=new Vector2(3,-3);s.Selection.effectColor=Gold;Mark(s.transform);
  }
  var previous=ours?p.OurPreviousPage:p.TheirPreviousPage;var next=ours?p.OurNextPage:p.TheirNextPage;var page=ours?p.OurPageLabel:p.TheirPageLabel;
  Place(previous.transform,24,352,98,44);Place(next.transform,728,352,98,44);Place(previous.GetComponentInChildren<Text>(true).transform,0,0,98,44);Place(next.GetComponentInChildren<Text>(true).transform,0,0,98,44);Place(page.transform,136,352,576,44);page.fontSize=22;
  Picture(root,"CartHeadingPaper",20,409,810,45,button,new Color(.84f,.80f,.62f));Label(root,"CartHeading",38,409,748,45,ours?"내가 줄 물건":"내가 받을 물건",27,Ink);
  var scroll=Cart(root,"Cart",24,464,812,188,p.Goods.Length,false,out var rows);
  var empty=Label(root,"EmptyCart",38,474,758,154,ours?"위에서 내놓을 물건을 눌러 주세요.\n예약 재료와 개인 가방은 제외됩니다.":"위에서 받고 싶은 물건을 눌러 주세요.\n여러 종류를 함께 고를 수 있습니다.",24,Light,TextAnchor.MiddleCenter).gameObject;
  var total=Label(root,"CartTotal",38,661,748,40,"합계 가치 0",26,Light,TextAnchor.MiddleRight);
  if(ours){p.GiveRows=rows;p.GiveCartScroll=scroll;p.GiveEmpty=empty;p.GiveTotal=total;}else{p.TakeRows=rows;p.TakeCartScroll=scroll;p.TakeEmpty=empty;p.TakeTotal=total;}
 }
 static void Review(SettlementVisitorPanel p)
 {
  var parent=p.ConfirmBody.transform.parent;Place(parent,300,136,1320,808);Place(parent.Find("Title"),108,24,1164,66);Place(p.ConfirmBody.transform,64,122,1192,450);
  Place(p.Cancel.transform,48,700,530,76);Place(p.Confirm.transform,742,700,530,76);
  foreach(var b in new[]{p.Cancel,p.Confirm})Place(b.transform.Find("Label"),70,0,430,76);
  var root=Node(parent,"CartReview",0,104,1320,578);p.CartReview=root.gameObject;
  Label(root,"GiveHeading",40,0,586,46,"내가 줄 물건",28,Ink);Label(root,"TakeHeading",694,0,586,46,"내가 받을 물건",28,Ink);
  Picture(root,"Divider",659,4,2,442,null,new Color(.3f,.3f,.25f,.2f));
  p.ReviewGiveScroll=Cart(root,"GiveList",40,58,586,330,p.Goods.Length,true,out var give);p.ReviewGiveRows=give;
  p.ReviewTakeScroll=Cart(root,"TakeList",694,58,586,330,p.Goods.Length,true,out var take);p.ReviewTakeRows=take;
  p.ReviewGiveTotal=Label(root,"GiveTotal",40,398,548,48,"합계 가치 0",27,Ink,TextAnchor.MiddleRight);p.ReviewTakeTotal=Label(root,"TakeTotal",694,398,548,48,"합계 가치 0",27,Ink,TextAnchor.MiddleRight);
  p.ReviewSummary=Label(root,"Summary",40,466,1240,94,"확정하면 공용 창고 물품을 교환합니다.",27,Ink);
  root.gameObject.SetActive(false);
 }
 static void Apply(SettlementVisitorPanel p)
 {
  var trade=p.Trade.transform;Stock(p,true);Stock(p,false);Hide(trade,"ExchangeArrow");
  p.TradeHint=Label(trade,"TradeHint",80,106,1760,44,"물건을 눌러 담고, 아래 교환 목록에서 수량을 조절하세요.",26,Light);
  var person=p.MemberName.transform.parent;Place(person,80,902,470,48);var background=person.GetComponent<Image>();if(background)background.enabled=false;p.PreviousMember.gameObject.SetActive(false);p.NextMember.gameObject.SetActive(false);Place(p.MemberPortrait.transform,0,0,42,42);p.MemberPortrait.color=Light;Place(p.MemberName.transform,54,0,410,44);p.MemberName.fontSize=22;p.MemberName.color=Light;
  Place(p.Balance.transform,588,880,1252,80);p.Balance.fontSize=27;p.Balance.color=Light;p.Balance.resizeTextForBestFit=false;
  p.ClearTrade=Button(trade,"ClearTrade",780,974,360,76,"선택 비우기",28);p.OfferLabel=p.Offer.GetComponentInChildren<Text>(true);p.OfferLabel.text="교환 내용 확인";Place(p.OfferLabel.transform,16,0,518,76);
  p.TradeBack.GetComponentInChildren<Text>(true).text="방문자에게 돌아가기";Review(p);Mark(p.transform);
 }
 public static string Run()
 {
  if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first.");
  var source=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"VisitorPanel.prefab").GetComponent<SettlementVisitorPanel>();font=source.GiveDetails.font;paper=source.ConfirmBody.transform.parent.GetComponent<Image>().sprite;button=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PartySelection/footer-paper.png");RowPrefab();
  foreach(var name in new[]{"VisitorPanel","SettlementScreen"}){string path=Folder+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);try{foreach(var panel in root.GetComponentsInChildren<SettlementVisitorPanel>(true))Apply(panel);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}}
  AssetDatabase.SaveAssets();return Validate();
 }
 public static string Validate()
 {
  foreach(var name in new[]{"VisitorPanel","SettlementScreen"})foreach(var p in AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+".prefab").GetComponentsInChildren<SettlementVisitorPanel>(true))
  {
   if(p.GiveRows.Length!=p.Goods.Length||p.TakeRows.Length!=p.Goods.Length||p.ReviewGiveRows.Length!=p.Goods.Length||p.ReviewTakeRows.Length!=p.Goods.Length||!p.ClearTrade||!p.CartReview)throw new InvalidOperationException("Missing cart UI: "+name);
   foreach(var row in p.GiveRows.Concat(p.TakeRows).Concat(p.ReviewGiveRows).Concat(p.ReviewTakeRows))if(!row.Icon||!row.NameLabel||!row.CountLabel||!row.ValueLabel||!row.Minus||!row.Plus)throw new InvalidOperationException("Incomplete cart row.");
  }
  return "PASS: visitor-only mixed-goods carts and scrollable icon confirmation, reusable TradeCartRow prefab, inspector-authored geometry; run native interaction review next.";
 }
}
