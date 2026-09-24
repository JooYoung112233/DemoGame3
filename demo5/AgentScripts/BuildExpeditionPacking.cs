using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class BuildExpeditionPacking {
 const string P="Assets/Prefabs/Settlement/",A="Assets/Art/PartySelection/",E="Assets/Art/ExpeditionPlan/";static Font font;
 static RectTransform R(string n,Transform p,float x,float y,float w,float h){var r=new GameObject(n,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(p,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
 static Image I(string n,Transform p,float x,float y,float w,float h,string path=null){var im=R(n,p,x,y,w,h).gameObject.AddComponent<Image>();if(path!=null)im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);im.raycastTarget=false;return im;}
 static Text T(string n,Transform p,float x,float y,float w,float h,string s,int size=30,bool light=false){var t=R(n,p,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.fontStyle=FontStyle.Normal;t.fontSize=size;t.text=s;t.color=light?new Color(.96f,.93f,.85f):new Color(.045f,.065f,.06f);t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;return t;}
 static Button B(string n,Transform p,float x,float y,float w,float h,string label,int size=32){var im=I(n,p,x,y,w,h,A+"footer-paper.png");im.raycastTarget=true;var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;T("Label",im.transform,6,0,w-12,h,label,size).alignment=TextAnchor.MiddleCenter;return b;}
 static GameObject Save(GameObject g,string name){var asset=PrefabUtility.SaveAsPrefabAsset(g,P+name+".prefab");Object.DestroyImmediate(g);return asset;}
 static Sprite S(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>(E+name+".png");
 static ScrollRect Grid(Transform root,string name,float x,float y,float w,float h,int columns,out RectTransform content){
  var view=R(name,root,x,y,w,h);view.gameObject.AddComponent<Image>().color=Color.clear;view.gameObject.AddComponent<RectMask2D>();
  content=R("Content",view,0,0,w-16,h);var grid=content.gameObject.AddComponent<GridLayoutGroup>();grid.cellSize=new Vector2((w-16-(columns-1)*12)/columns,120);grid.spacing=new Vector2(12,14);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=columns;
  content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
  var scroll=view.gameObject.AddComponent<ScrollRect>();scroll.viewport=view;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=40;scroll.movementType=ScrollRect.MovementType.Clamped;
  var rail=I("Rail",view,w-7,0,7,h);rail.color=new Color(1,1,1,.12f);var handle=I("Handle",rail.transform,0,0,7,100,A+"footer-paper.png");handle.raycastTarget=true;var bar=rail.gameObject.AddComponent<Scrollbar>();bar.targetGraphic=handle;bar.handleRect=handle.rectTransform;bar.direction=Scrollbar.Direction.BottomToTop;scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
  var hint=R("MoreBelow",root,x+w-34,y+h-20,24,17);hint.gameObject.AddComponent<CanvasRenderer>();hint.gameObject.AddComponent<RoundedDownArrow>().raycastTarget=false;var visibility=hint.gameObject.AddComponent<CanvasGroup>();var indicator=hint.gameObject.AddComponent<ScrollMoreIndicator>();indicator.Scroll=scroll;indicator.Visibility=visibility;return scroll;
 }
 // STALE since 2026-09-25: this rebuild names Tab1 '보급품' (the retired supplies item; BuildRetireSupplies renamed that food/water tab '식량') and drops later packing edits; do not rerun.
 static void RefuseAfterSuppliesRetired(){var inv=AssetDatabase.LoadAssetAtPath<GameObject>(P+"InventoryPanel.prefab");var p=inv?inv.GetComponent<SettlementInventoryPanel>():null;if(p&&p.Items!=null&&!Array.Exists(p.Items,i=>i.Id=="supplies"))throw new Exception("Superseded: supplies retired 2026-09-25 (BuildRetireSupplies). BuildExpeditionPacking would bring back the '보급품' tab; do not rerun.");}
 public static string Build(){
  if(EditorApplication.isPlaying)throw new Exception("Stop Play first");RefuseAfterSuppliesRetired();
  font=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");
  var root=R("ExpeditionPackingPanel",null,0,0,1920,1080);var c=root.gameObject.AddComponent<ExpeditionPackingPanel>();c.View=root.gameObject;
  var w=R("Workspace",root,0,0,1920,1080);c.Workspace=w.gameObject.AddComponent<CanvasGroup>();
  var bg=I("Background",w,0,0,1920,1080,A+"teal-texture.png");bg.raycastTarget=true;
  I("HeadingPaper",w,80,44,500,130,A+"card-paper.png");T("Title",w,104,48,452,62,"원정 준비 · 짐 꾸리기",42);T("Subtitle",w,104,111,452,46,"필요한 물품을 챙겨주세요.",29);
  I("ClockPaper",w,1650,55,190,90,A+"count-paper.png");c.Clock=T("Clock",w,1673,61,145,78,"DAY 1\n09:00",29);
  var vp=R("MemberViewport",w,620,28,985,178);vp.gameObject.AddComponent<Image>().color=Color.clear;vp.gameObject.AddComponent<RectMask2D>();c.MemberContent=R("Content",vp,0,0,985,172);
  var hl=c.MemberContent.gameObject.AddComponent<HorizontalLayoutGroup>();hl.spacing=14;hl.childControlWidth=hl.childControlHeight=hl.childForceExpandWidth=hl.childForceExpandHeight=false;c.MemberContent.gameObject.AddComponent<ContentSizeFitter>().horizontalFit=ContentSizeFitter.FitMode.PreferredSize;
  c.MemberScroll=vp.gameObject.AddComponent<ScrollRect>();c.MemberScroll.viewport=vp;c.MemberScroll.content=c.MemberContent;c.MemberScroll.horizontal=true;c.MemberScroll.vertical=false;c.MemberScroll.movementType=ScrollRect.MovementType.Clamped;
  c.MemberPrefab=AssetDatabase.LoadAssetAtPath<ExpeditionMemberCard>(P+"ExpeditionMemberCard.prefab");var slot=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(P+"InventorySlot.prefab"));slot.name="PackingItemSlot";slot.GetComponent<InventorySlot>().Label.fontSize=20;c.SlotPrefab=Save(slot,"PackingItemSlot").GetComponent<InventorySlot>();
  var memberRail=I("MemberRail",vp,0,172,985,6);memberRail.color=new Color(1,1,1,.15f);var memberHandle=I("Handle",memberRail.transform,0,0,150,6,A+"footer-paper.png");memberHandle.raycastTarget=true;var memberBar=memberRail.gameObject.AddComponent<Scrollbar>();memberBar.targetGraphic=memberHandle;memberBar.handleRect=memberHandle.rectTransform;memberBar.direction=Scrollbar.Direction.LeftToRight;c.MemberScroll.horizontalScrollbar=memberBar;c.MemberScroll.horizontalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
  foreach(var spec in new[]{new Vector4(74,222,712,708),new Vector4(806,222,516,708),new Vector4(1342,222,502,392),new Vector4(1342,634,502,296)})I("Panel",w,spec.x,spec.y,spec.z,spec.w,A+"card-paper.png").color=new Color(.12f,.16f,.17f,.97f);
  I("StockTitlePaper",w,104,242,220,64,A+"count-paper.png");T("StockTitle",w,122,242,184,64,"공용 창고",36);T("StockHint",w,343,245,406,62,"모든 대원이 함께 쓰는 물자",25,true);
  c.Tabs=new Button[4];string[] tabs={"전체","보급품","탄약","재료"};for(int i=0;i<4;i++)c.Tabs[i]=B("Tab"+i,w,104+i*165,320,153,52,tabs[i],28);
  c.StockScroll=Grid(w,"StockViewport",104,390,652,502,5,out c.StockContent);
  I("BagTitlePaper",w,836,242,286,64,A+"count-paper.png");c.BagTitle=T("BagTitle",w,853,242,252,64,"개인 가방",31);c.Capacity=T("Capacity",w,1134,239,158,36,"0 / 3",29,true);c.Capacity.alignment=TextAnchor.MiddleRight;
  var track=I("CapacityTrack",w,1134,284,158,16);track.color=new Color(.025f,.05f,.055f);c.CapacityFill=I("CapacityFill",track.transform,1,1,156,14,A+"count-paper.png");c.CapacityFill.type=Image.Type.Filled;c.CapacityFill.fillMethod=Image.FillMethod.Horizontal;c.CapacityFill.color=new Color(.46f,.72f,.49f);
  c.BagScroll=Grid(w,"BagViewport",836,334,456,558,4,out c.BagContent);
  I("InfoTitlePaper",w,1372,242,220,55,A+"count-paper.png");T("InfoTitle",w,1390,242,184,55,"아이템 정보",31);
  I("ItemPaper",w,1372,310,438,198,A+"card-paper.png");c.ItemIcon=I("ItemIcon",w,1388,330,112,112);c.ItemIcon.preserveAspect=true;c.ItemName=T("ItemName",w,1518,317,272,43,"물건 선택",32);c.Description=T("Description",w,1518,365,272,89,"",25);
  c.Available=T("Available",w,1388,457,202,42,"보유 수량",23);c.Minus=B("Minus",w,1594,457,52,42,"−",29);c.Quantity=T("Quantity",w,1649,457,90,42,"1",29);c.Quantity.alignment=TextAnchor.MiddleCenter;c.Plus=B("Plus",w,1742,457,52,42,"+",29);
  c.ToBag=B("ToBag",w,1372,527,213,60,"가방에 넣기",30);c.ToBag.GetComponent<Image>().color=new Color(1,.8f,.43f);c.ToStock=B("ToStock",w,1597,527,213,60,"창고로",30);
  I("ChecklistTitlePaper",w,1372,654,344,53,A+"count-paper.png");T("ChecklistTitle",w,1390,654,308,53,"짐 꾸리기 체크리스트",29);
  I("ChecklistPaper",w,1372,721,438,115,A+"card-paper.png");c.Checklist=T("Checklist",w,1390,726,402,106,"",24);c.Checklist.lineSpacing=.66f;
  I("NoticePaper",w,1372,848,438,55,A+"footer-paper.png").color=new Color(.94f,.62f,.54f);c.Notice=T("Notice",w,1388,851,406,49,"",23);
  c.Back=B("Back",w,80,952,410,78,"원정 계획으로",34);I("Icon",c.Back.transform,22,22,26,34,A+"icon-left.png").preserveAspect=true;
  c.Destination=T("Destination",w,514,952,892,78,"",32,true);c.Destination.alignment=TextAnchor.MiddleCenter;
  c.Ready=B("Ready",w,1430,952,410,78,"준비 내역 확인",35);c.Ready.GetComponent<Image>().color=new Color(.76f,.86f,.68f);
  var modal=R("PackingReview",null,0,0,1920,1080);var dim=I("Dim",modal,0,0,1920,1080);dim.color=new Color(0,0,0,.76f);dim.raycastTarget=true;I("Paper",modal,460,150,1000,770,A+"card-paper.png");T("Title",modal,510,178,900,70,"원정 준비 확인",46);var sv=R("SummaryViewport",modal,510,266,900,470);sv.gameObject.AddComponent<RectMask2D>();sv.gameObject.AddComponent<Image>().color=Color.clear;var summary=T("Summary",sv,0,0,884,470,"",30);summary.alignment=TextAnchor.UpperLeft;summary.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;var sr=sv.gameObject.AddComponent<ScrollRect>();sr.viewport=sv;sr.content=summary.rectTransform;sr.horizontal=false;sr.movementType=ScrollRect.MovementType.Clamped;var close=B("ReviewBack",modal,510,790,410,78,"짐 꾸리기로",34);
  var reviewAsset=Save(modal.gameObject,"ExpeditionPackingReview");var review=(GameObject)PrefabUtility.InstantiatePrefab(reviewAsset,root);c.Review=review;c.Summary=review.transform.Find("SummaryViewport/Summary").GetComponent<Text>();c.ReviewBack=review.transform.Find("ReviewBack").GetComponent<Button>();review.SetActive(false);
  var asset=Save(root.gameObject,"ExpeditionPackingPanel");var screen=PrefabUtility.LoadPrefabContents(P+"SettlementScreen.prefab");try{var old=screen.transform.Find("ExpeditionPackingPanel");if(old)Object.DestroyImmediate(old.gameObject);var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,screen.transform);screen.GetComponent<SettlementController>().PackingPanel=go.GetComponent<ExpeditionPackingPanel>();go.SetActive(false);PrefabUtility.SaveAsPrefabAsset(screen,P+"SettlementScreen.prefab");}finally{PrefabUtility.UnloadPrefabContents(screen);}AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Packing screen + review prefab saved.";
 }
}
