using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class BuildBattleResultSummary {
 static Font font;static Sprite paper;static Color ink=new Color(.07f,.10f,.10f),muted=new Color(.29f,.31f,.27f);
 static RectTransform R(string name,Transform parent,float x,float y,float w,float h){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;Place(r,x,y,w,h);return r;}
 static void Place(Transform t,float x,float y,float w,float h){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
 static Image I(string name,Transform p,float x,float y,float w,float h,Color color,Sprite sprite=null){var i=R(name,p,x,y,w,h).gameObject.AddComponent<Image>();i.color=color;i.sprite=sprite;i.raycastTarget=false;return i;}
 static Text T(string name,Transform p,float x,float y,float w,float h,string text,int size=23){var t=R(name,p,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.color=ink;t.text=text;t.alignment=TextAnchor.MiddleLeft;t.verticalOverflow=VerticalWrapMode.Overflow;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.raycastTarget=false;return t;}
 static RectTransform List(string name,Transform p,float x,float y,float w,float h,float row){
 var root=R(name,p,x,y,w,h);var scroll=root.gameObject.AddComponent<ScrollRect>();var vp=R("Viewport",root,0,0,w-22,h);vp.gameObject.AddComponent<RectMask2D>();var hit=vp.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
 var content=R("Content",vp,0,0,w-22,h);var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=12;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;var fitter=content.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
 var track=I("ScrollTrack",root,w-14,0,12,h,new Color(.17f,.24f,.22f,.2f));var bar=track.gameObject.AddComponent<Scrollbar>();var handle=I("Handle",track.transform,0,0,12,h,new Color(.27f,.39f,.32f));handle.rectTransform.sizeDelta=Vector2.zero;bar.handleRect=handle.rectTransform;bar.targetGraphic=handle;bar.direction=Scrollbar.Direction.BottomToTop;handle.raycastTarget=true;scroll.viewport=vp;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;scroll.scrollSensitivity=30;return content;
 }
 public static string TuneLive(){
 var summary=Object.FindAnyObjectByType<BattleResultSummary>(FindObjectsInactive.Include);var root=summary.transform.Find("SummaryContent");
 Place(root.Find("Used"),1096,379,500,232);Place(root.Find("Used/Viewport"),0,0,478,232);Place(root.Find("Used/ScrollTrack"),486,0,12,232);
 Place(root.Find("GainedHeading"),1096,635,478,42);Place(root.Find("Gained"),1096,687,500,66);Place(root.Find("Gained/Viewport"),0,0,478,66);Place(root.Find("Gained/ScrollTrack"),486,0,12,66);Place(root.Find("GainedEmpty"),1096,687,460,66);
 foreach(var bar in summary.GetComponentsInChildren<Scrollbar>(true))bar.handleRect.sizeDelta=Vector2.zero;
 Canvas.ForceUpdateCanvases();return "Live scroll tracks and three-row usage list corrected.";
 }
 public static string Run(){
 const string path="Assets/Prefabs/Settlement/BattleResultPanel.prefab";var go=PrefabUtility.LoadPrefabContents(path);
 try{
 font=go.transform.Find("Title").GetComponent<Text>().font;paper=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PartySelection/footer-paper.png");
 var headingOld=go.transform.Find("PopupHeading");if(headingOld)Object.DestroyImmediate(headingOld.gameObject);
 var heading=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/PopupHeading.prefab"),go.transform);heading.name="PopupHeading";
 heading.GetComponentInChildren<Text>().text="탐험 · 전투 결과";PrefabUtility.RecordPrefabInstancePropertyModifications(heading.GetComponentInChildren<Text>());
 if(!go.GetComponent<PopupBackgroundHud>())go.AddComponent<PopupBackgroundHud>();
 var old=go.transform.Find("SummaryContent");if(old)Object.DestroyImmediate(old.gameObject);
 var root=R("SummaryContent",go.transform,0,0,1920,1080);var summary=go.GetComponent<BattleResultSummary>();if(!summary)summary=go.AddComponent<BattleResultSummary>();
 Place(go.transform.Find("Paper"),230,120,1460,800);go.transform.Find("Dim").GetComponent<Image>().color=new Color(0,0,0,.97f);
 Place(go.transform.Find("Title"),310,157,1280,64);go.transform.Find("Title").GetComponent<Text>().fontSize=38;
 go.transform.Find("Body").gameObject.SetActive(false);
 I("StatsPaper",root,306,243,1308,61,Color.white,paper);summary.Stats=T("Stats",root,328,248,1260,50,"",25);
 T("MembersHeading",root,310,325,700,42,"대원 상태",29);T("UsedHeading",root,1096,325,478,42,"사용한 물품",29);
 I("Divider",root,1068,332,2,414,new Color(.3f,.32f,.26f,.28f));
 summary.Members=List("Members",root,306,379,744,374,170);summary.Used=List("Used",root,1096,379,500,232,62);
 T("GainedHeading",root,1096,635,478,42,"이번 전투 획득",29);summary.Gained=List("Gained",root,1096,687,500,66,62);
 summary.UsedEmpty=T("UsedEmpty",root,1096,388,450,52,"",22);summary.UsedEmpty.color=muted;
 summary.GainedEmpty=T("GainedEmpty",root,1096,687,460,66,"",21);summary.GainedEmpty.color=muted;
 I("FootDivider",root,310,777,1288,2,new Color(.3f,.32f,.26f,.28f));summary.Consequences=T("Consequences",root,310,792,1288,78,"",22);summary.Consequences.color=muted;
 Place(go.transform.Find("Continue"),80,974,410,76);var label=go.transform.Find("Continue").GetComponentInChildren<Text>();Place(label.transform,12,0,386,76);label.fontSize=30;label.alignment=TextAnchor.MiddleCenter;
 go.transform.Find("Continue").GetComponent<Image>().sprite=paper;
 var card=R("MemberTemplate",root,0,0,722,176);card.gameObject.AddComponent<LayoutElement>().preferredHeight=176;
 I("CardPaper",card,0,0,722,176,Color.white,paper);
 var portrait=I("Portrait",card,18,31,100,110,Color.white);portrait.preserveAspect=true;var fit=portrait.gameObject.AddComponent<BattlePortraitFit>();fit.Crops=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/BattleTurnCard.prefab").GetComponent<BattleTurnCard>().Portrait.GetComponent<BattlePortraitFit>().Crops;
 T("Name",card,142,10,300,43,"",27);T("Change",card,532,12,170,39,"",21);T("Health",card,142,54,360,35,"",23);
 I("BarBack",card,142,97,552,16,new Color(.07f,.1f,.08f));I("Bar",card,144,99,548,12,new Color(.42f,.64f,.44f));
 T("State",card,445,54,255,35,"",20);T("Used",card,142,123,552,45,"",20);
 summary.MemberTemplate=card.gameObject;card.gameObject.SetActive(false);
 var item=R("ItemTemplate",root,0,0,478,62);item.gameObject.AddComponent<LayoutElement>().preferredHeight=62;I("Icon",item,0,8,44,44,Color.white).preserveAspect=true;T("Name",item,60,0,308,34,"",23);T("Owner",item,60,33,308,28,"",18).color=muted;var amount=T("Amount",item,376,7,80,46,"",28);amount.alignment=TextAnchor.MiddleRight;summary.ItemTemplate=item.gameObject;item.gameObject.SetActive(false);
 heading.transform.SetAsLastSibling();go.transform.Find("Title").SetAsLastSibling();go.transform.Find("Continue").SetAsLastSibling();
 PrefabUtility.SaveAsPrefabAsset(go,path);AssetDatabase.SaveAssets();return "Saved readable result prefab: scrollable member cards, consumed/acquired rows, consequences and return button.";
 }finally{PrefabUtility.UnloadPrefabContents(go);}
 }
}
