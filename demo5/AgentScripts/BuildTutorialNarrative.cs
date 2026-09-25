using System;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Uses existing paper, black portraits and the established bottom dialogue composition.
public static class BuildTutorialNarrative
{
 const string Folder="Assets/Prefabs/Settlement/";
 static Font font;static Sprite strip;static readonly Color Ink=new Color(.055f,.075f,.07f);
 static RectTransform Node(Transform p,string name,float x,float y,float w,float h){var old=p?p.Find(name):null;var r=old?(RectTransform)old:new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();if(!old&&p)r.SetParent(p,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
 static Image Image(Transform p,string name,float x,float y,float w,float h,Sprite sprite,Color color,bool hit=false){var r=Node(p,name,x,y,w,h);var im=r.GetComponent<Image>()??r.gameObject.AddComponent<Image>();im.sprite=sprite;im.color=color;im.raycastTarget=hit;return im;}
 static Text Text(Transform p,string name,float x,float y,float w,float h,string text,int size,TextAnchor align=TextAnchor.MiddleLeft){var r=Node(p,name,x,y,w,h);var t=r.GetComponent<Text>()??r.gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.text=text;t.color=Ink;t.alignment=align;t.resizeTextForBestFit=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;t.raycastTarget=false;return t;}
 static SettlementTutorialNarrative.Line L(string who,string text)=>new SettlementTutorialNarrative.Line{SpeakerId=who,Text=text};
 static SettlementTutorialNarrative.Beat B(string title,params SettlementTutorialNarrative.Line[] lines)=>new SettlementTutorialNarrative.Beat{Title=title,Lines=lines};
 static SettlementTutorialNarrative.Beat[] Content()
 {
  var beats=new[]{
   B("약속한 날, 비어 있던 집",L("","새로 머물 곳을 찾던 두 사람은 해인이 돌보던 시계 수리공, 이금례를 찾아갔다.\n약속한 날이었지만 집과 가게는 비어 있었다. 남은 것은 수리품 인수증 사본뿐이었다."),L("medic","오늘 찾아뵙기로 했는데… 어디 가셨을까.\n옛 사무소를 나온 뒤로 연락이 엇갈린 걸까."),L("scout","인수증에 폐상가 보관실이라고 적혀 있어. 예전에 배달하던 곳이야.\n맡긴 물건을 찾아보면, 누가 옮겼는지 알 수 있겠지."),L("medic","같이 가 줘서 고마워, 서진아.\n우선 여기 짐을 내려놓을 수 있는지 보자. 금례 씨 소식을 찾으려면 머물 곳도 필요하니까."),L("","두 사람은 {home}의 문을 열었다.\n인기척과 건물 상태부터 살펴보기로 했다.")),
   B("고쳐 쓸 수 있는지",L("scout","인기척은 없어. 선반하고 작업대는 손을 좀 봐야겠네.\n흔들리는 데다 무거운 짐부터 올려놓을 순 없겠어."),L("medic","저 상자에 수리할 재료가 있는지 보자.\n여기 있는 걸 먼저 확인하고, 모자란 것만 밖에서 찾으면 되잖아.")),
   B("남아 있는 것과 필요한 것",L("medic","물하고 천은 있네. 탄약도 조금 남아 있고.\n함께 쓸 물건은 한곳에 두고, 나갈 때 필요한 것만 가방에 챙기자."),L("scout","선반을 고칠 목재나 고철은 없어. 밖에서 찾아야겠어.\n그전에 바닥 한쪽만 치우자. 돌아와서 가져온 짐을 내려놓게.")),
   // Retained for saves already reading the retired rest lesson; fresh games skip this beat.
   B("짐을 둘 자리",L("medic","바닥은 정리됐네. 가져오는 물건은 여기에 모아 두자."),L("scout","선반하고 작업대는 자재를 구하면 고치자.\n먼저 어디서 찾을지 정해야겠어.")),
   B("금례의 흔적을 찾아",L("medic","짐 둘 자리는 됐어. 인수증에 적힌 폐상가로 가 보자.\n금례 씨 물건을 맡은 사람이 다른 연락처를 알지도 몰라."),L("scout","보관실 앞 복도에 적재함이 있었어. 인계 기록부터 찾아보자.\n돌아오는 길엔 선반을 고칠 목재와 고철도 챙기고."),L("medic","출입구에서 지도를 펴자. 함께 갈 사람과 가져갈 짐을 정해야겠어.")),
   B("돌아온 짐을 풀며",L("medic","{return_line}"),L("scout","{storage_line}")),
   B("작업대 · 도구와 시설 만들기",L("scout","작업대가 버티네. 여기서 재료를 써 도구를 만들고 시설도 고치자.\n그런데 서랍 안에 접힌 종이가 하나 있어."),L("medic","물자 위치를 적은 관리표 같아.\n여기 머물던 사람이 무엇을 남겼는지 읽어 보자.")),
   B("간이 지렛대 · 걸린 문을 벌릴 도구",L("medic","복도 끝 보관실에 물과 통조림을 뒀다네.\n얼마나 오래된 기록인지는 모르겠어."),L("scout","간이 지렛대는 걸린 문틈을 벌리는 도구야.\n작업대에서 목재·밧줄·못으로 만들어 가자.")),
   B("돌아올 길을 생각하며",L("scout","{door_line}"),L("medic","{carry_line}")),
   B("직접 확인한 장소",L("scout","보관실을 확인하고 돌아왔어. 이제 어디로 들어가야 하는지는 알겠네."),L("medic","이번 수색을 기록해 두자.\n무엇을 찾았고 어디를 더 살펴봐야 할지, 다음 외출 전에 꺼내 볼 수 있게.")),
   B("우리 생활을 이어갈 차례",L("medic","{living_line}"),L("scout","부족한 건 다시 나가서 찾으면 돼.\n아까 그 기록의 경고는… 잊지 말고.") )
  };
  beats[4].EndLabel="외출 준비하기";
  beats[2].FactIds=new[]{"cloth","water","ammo"};return beats;
 }
 static GameObject BuildView()
 {
  var root=Node(null,"TutorialNarrativeView",0,0,1920,1080);root.gameObject.AddComponent<PopupBackgroundHud>();
  Image(root,"Dim",0,0,1920,1080,null,new Color(.012f,.032f,.033f,.20f),true);
  var paper=Image(root,"Paper",80,724,1760,244,strip,Color.white,true);var surface=paper.gameObject.AddComponent<Button>();surface.targetGraphic=paper;surface.transition=Selectable.Transition.None;surface.navigation=new Navigation{mode=Navigation.Mode.None};
  var portrait=Image(root,"Portrait",112,755,140,160,null,Color.white);portrait.preserveAspect=true;portrait.material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PaperPortrait.mat");
  Text(root,"Speaker",282,738,350,50,"윤서진",32);
  Text(root,"SceneTitle",700,738,1080,50,"잠시 머물 곳",25,TextAnchor.MiddleRight).color=new Color(.32f,.35f,.28f);
  Text(root,"Body",282,794,1470,96,"문이 닫힌다고 안전한 건 아니니까, 안쪽부터 볼게.",30,TextAnchor.UpperLeft);
  var facts=Node(root,"Facts",282,902,1440,46);
  for(int i=0;i<3;i++){Image(facts,"Icon"+i,i*360,0,44,44,null,Color.white).preserveAspect=true;Text(facts,"Label"+i,i*360+58,0,288,46,"발견물 ×1",23);}
  facts.gameObject.SetActive(false);
  var condition=Node(root,"PartyCondition",282,890,1440,50);
  for(int i=0;i<2;i++){
   Text(condition,"HealthLabel"+i,i*440,0,400,34,"대원 · 체력 6 / 6",22);
   var bar=Image(condition,"HealthBar"+i,i*440,36,300,12,null,new Color(.43f,.63f,.4f));
   SegmentedHealthGraphic.Set(bar,6,6);
  }
  Text(condition,"Cause",920,0,520,48,"현재 대원 상태",22).color=new Color(.38f,.26f,.2f);
  condition.gameObject.SetActive(false);
  Text(root,"ReadingHint",80,986,1250,52,"대화 중에는 시간이 흐르지 않습니다.",24).color=new Color(.94f,.89f,.77f);
  var im=Image(root,"Next",1470,974,370,76,strip,Color.white,true);var next=im.gameObject.AddComponent<Button>();next.targetGraphic=im;Text(next.transform,"Label",16,0,338,76,"다음  ›",30,TextAnchor.MiddleCenter);
  var fade=Node(root,"ArrivalFade",0,0,1920,1080);var group=fade.gameObject.AddComponent<CanvasGroup>();group.alpha=1;group.blocksRaycasts=true;group.interactable=false;
  fade.gameObject.AddComponent<PopupBackgroundHud>();Image(fade,"Black",-1200,-1200,4320,3480,null,Color.black,true);fade.gameObject.SetActive(false);
  var asset=PrefabUtility.SaveAsPrefabAsset(root.gameObject,Folder+"TutorialNarrativeView.prefab");Object.DestroyImmediate(root.gameObject);return asset;
 }
 public static string Run()
 {
  if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
  var source=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"SettlementScreen.prefab").GetComponent<SettlementController>();font=source.NoticeBody.font;strip=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PartySelection/footer-paper.png");var viewAsset=BuildView();
  string path=Folder+"SettlementScreen.prefab";var root=PrefabUtility.LoadPrefabContents(path);
  try{
   var c=root.GetComponent<SettlementController>();var n=root.GetComponent<SettlementTutorialNarrative>()??root.AddComponent<SettlementTutorialNarrative>();c.Narrative=n;
   var old=root.transform.Find("TutorialNarrativeView");var view=old?old.gameObject:(GameObject)PrefabUtility.InstantiatePrefab(viewAsset,root.transform);view.name="TutorialNarrativeView";
   n.View=view;n.Facts=view.transform.Find("Facts").gameObject;n.Portrait=view.transform.Find("Portrait").GetComponent<Image>();n.SceneTitle=view.transform.Find("SceneTitle").GetComponent<Text>();n.Speaker=view.transform.Find("Speaker").GetComponent<Text>();n.Body=view.transform.Find("Body").GetComponent<Text>();n.ReadingHint=view.transform.Find("ReadingHint").GetComponent<Text>();n.Next=view.transform.Find("Next").GetComponent<Button>();n.NextLabel=n.Next.GetComponentInChildren<Text>();n.ContinueSurface=view.transform.Find("Paper").GetComponent<Button>();
   n.FactIcons=Enumerable.Range(0,3).Select(i=>n.Facts.transform.Find("Icon"+i).GetComponent<Image>()).ToArray();n.FactLabels=Enumerable.Range(0,3).Select(i=>n.Facts.transform.Find("Label"+i).GetComponent<Text>()).ToArray();n.Beats=Content();n.State=new SavedTutorialNarrative();view.SetActive(false);
   n.HealthView=view.transform.Find("PartyCondition").gameObject;n.HealthLabels=Enumerable.Range(0,2).Select(i=>n.HealthView.transform.Find("HealthLabel"+i).GetComponent<Text>()).ToArray();n.HealthBars=Enumerable.Range(0,2).Select(i=>n.HealthView.transform.Find("HealthBar"+i).GetComponent<Image>()).ToArray();
   // Keep the fade outside the dialogue view: its first frame must cover the room before a dialogue is opened.
   var fadeSource=view.transform.Find("ArrivalFade");var fadeOld=root.transform.Find("TutorialArrivalFade");if(fadeOld)Object.DestroyImmediate(fadeOld.gameObject);
   var arrival=Object.Instantiate(fadeSource.gameObject,root.transform);arrival.name="TutorialArrivalFade";arrival.SetActive(false);n.ArrivalFade=arrival.GetComponent<CanvasGroup>();n.ArrivalHold=.45f;n.ArrivalFadeSeconds=1.35f;
   PrefabUtility.SaveAsPrefabAsset(root,path);
  }finally{PrefabUtility.UnloadPrefabContents(root);}
  AssetDatabase.SaveAssets();return "PASS: authored 11 brief state-based story beats and reusable bottom dialogue prefab; existing portraits/paper, real found-item counts, no time or resource grants. Native progression verification required.";
 }
}
