using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class ConnectRecruitment
{
 static Font font;static Sprite paper,button;
 static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);Place(r,x,y,w,h);return r;}
 static void Place(Transform t,float x,float y,float w,float h){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
 static Image Image(string name,Transform p,float x,float y,float w,float h,Sprite s){var im=Rect(name,p,x,y,w,h).gameObject.AddComponent<Image>();im.sprite=s;return im;}
 static Text Text(string name,Transform p,float x,float y,float w,float h,string value,int size=28){var t=Rect(name,p,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.text=value;t.fontSize=size;t.color=new Color(.06f,.085f,.075f);t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;return t;}
 static Button Button(string name,Transform p,float x,float y,float w,float h,string value){var im=Image(name,p,x,y,w,h,button);var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;Text("Label",im.transform,16,0,w-32,h,value).alignment=TextAnchor.MiddleCenter;return b;}
 static void Icon(Transform p,string name,string path,float x,float y,float size,Color color){var old=p.Find(name);if(old)Object.DestroyImmediate(old.gameObject);var im=Image(name,p,x,y,size,size,AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/"+path+".png"));im.preserveAspect=true;im.color=color;im.raycastTarget=false;}
 static void ButtonIcon(Button b,string path){var r=(RectTransform)b.transform;Icon(b.transform,"ActionIcon",path,24,(r.rect.height-32)/2,32,Color.black);Place(b.GetComponentInChildren<Text>().transform,70,0,r.rect.width-100,r.rect.height);}
 public static string Run(){
  if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
  for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Preserve dirty scene first");
  font=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");paper=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FrontEnd/SettingsDialog.prefab").transform.Find("Paper").GetComponent<Image>().sprite;button=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PartySelection/footer-paper.png");
  string path="Assets/Prefabs/Settlement/VisitorPanel.prefab";var g=PrefabUtility.LoadPrefabContents(path);
  try{
   var c=g.GetComponent<SettlementVisitorPanel>();if(c.RecruitPage)Object.DestroyImmediate(c.RecruitPage);if(c.InspectRecruit)Object.DestroyImmediate(c.InspectRecruit.gameObject);
   Place(c.Dialogue.transform,72,50,900,240);c.Dialogue.fontSize=32;Place(c.StartTrade.transform,72,310,900,80);Place(c.Dismiss.transform,72,510,900,80);
   foreach(var b in new[]{c.StartTrade,c.Dismiss})Place(b.GetComponentInChildren<Text>().transform,16,0,868,80);
   c.InspectRecruit=Button("InspectRecruit",c.StartTrade.transform.parent,72,410,900,80,"함께 지낼 의향을 묻는다");
   ButtonIcon(c.InspectRecruit,"PartySelection/icon-party");ButtonIcon(c.StartTrade,"Settlement/icon-storage");ButtonIcon(c.Dismiss,"FrontEnd/V2/icon-exit");
   c.Workspace.transform.Find("Heading/Title").GetComponent<Text>().text="방문자";
   c.Review.transform.Find("Paper/Title").GetComponent<Text>().text="방문자 · 선택 확인";
   var page=Rect("RecruitPage",c.Workspace.transform,0,0,1920,1080);c.RecruitPage=page.gameObject;
   var left=Image("PortraitPaper",page,140,200,500,650,paper);c.RecruitPortrait=Image("Portrait",left.transform,70,45,360,340,null);c.RecruitPortrait.preserveAspect=true;c.RecruitPortrait.raycastTarget=false;
   c.RecruitName=Text("Name",left.transform,52,398,396,66,"정착민 후보",40);c.RecruitName.alignment=TextAnchor.MiddleCenter;
   Icon(left.transform,"HealthIcon","PartySelection/icon-heart",64,494,34,new Color(.65f,.18f,.16f));c.RecruitHealth=Text("Health",left.transform,120,482,330,58,"체력",30);
   Icon(left.transform,"BagIcon","PartySelection/icon-bag",64,556,34,new Color(.42f,.3f,.13f));c.RecruitBag=Text("Bag",left.transform,120,544,330,58,"개인 가방",30);
   var right=Image("InformationPaper",page,700,200,1100,650,paper);
   Icon(right.transform,"DescriptionIcon","Settlement/icon-journal",64,64,36,Color.black);Text("IntroductionTitle",right.transform,124,44,912,74,"동료 소개",38);
   c.RecruitDetails=Text("Details",right.transform,124,150,912,170,"후보 정보",32);c.RecruitDetails.alignment=TextAnchor.UpperLeft;
   var divider=Image("SectionDivider",right.transform,64,350,972,2,null);divider.color=new Color(.22f,.17f,.1f,.2f);divider.raycastTarget=false;
   Icon(right.transform,"HousingIcon","PartySelection/icon-party",64,400,38,new Color(.13f,.32f,.2f));c.RecruitHousing=Text("HousingCount",right.transform,124,388,912,62,"거주 인원",32);
   c.RecruitHint=Text("Housing",right.transform,124,480,912,108,"거주 공간",28);
   c.RecruitBack=Button("Back",page,80,974,550,76,"방문자에게 돌아가기");c.RecruitAction=Button("Recruit",page,1290,974,550,76,"합류 제안 확인");page.gameObject.SetActive(false);
   ButtonIcon(c.RecruitBack,"PartySelection/icon-left");ButtonIcon(c.RecruitAction,"PartySelection/icon-party");ButtonIcon(c.Cancel,"PartySelection/icon-left");ButtonIcon(c.Confirm,"PartySelection/icon-right");
   var reviewTitle=c.Review.transform.Find("Paper/Title");Place(reviewTitle,108,24,864,66);Icon(reviewTitle.parent,"ChoiceIcon","PartySelection/icon-party",48,42,36,Color.black);
   PrefabUtility.SaveAsPrefabAsset(g,path);
  }finally{PrefabUtility.UnloadPrefabContents(g);}
  path="Assets/Prefabs/Settlement/SettlementWorld.prefab";g=PrefabUtility.LoadPrefabContents(path);
  try{
   var m=g.GetComponent<SettlementPawnMotion>();var feet=new[]{new Vector3(2,-1.5f,0),new Vector3(-1.8f,-2.6f,0),new Vector3(0,-2.8f,0),new Vector3(1.8f,-2.9f,0)};
   for(int i=2;i<6;i++){var p=g.transform.Find("Standee_"+i);if(!p){p=Object.Instantiate(m.Pawns[0],g.transform);p.name="Standee_"+i;p.position+=feet[i-2]-p.Find("Base").position;foreach(var sr in p.GetComponentsInChildren<SpriteRenderer>(true))sr.sortingOrder+=i;}p.gameObject.SetActive(false);var stop=g.transform.Find("WorkbenchStop_"+i);if(!stop){stop=new GameObject("WorkbenchStop_"+i).transform;stop.SetParent(g.transform,false);}stop.position=p.Find("Base").position;}
   m.Pawns=Enumerable.Range(0,6).Select(i=>g.transform.Find("Standee_"+i)).ToArray();m.Bodies=m.Pawns.Select(p=>p.Find("Body")).ToArray();m.Bases=m.Pawns.Select(p=>p.Find("Base")).ToArray();m.WorkBadges=m.Pawns.Select(p=>p.Find("WorkBadge")).ToArray();m.WorkPositions=Enumerable.Range(0,6).Select(i=>g.transform.Find("WorkbenchStop_"+i)).ToArray();PrefabUtility.SaveAsPrefabAsset(g,path);
  }finally{PrefabUtility.UnloadPrefabContents(g);}
  EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");var owner=Object.FindAnyObjectByType<SettlementController>();var motion=Object.FindAnyObjectByType<SettlementPawnMotion>();owner.StandeeObjects=motion.Pawns.Select(p=>p.gameObject).ToArray();owner.StandeeBodies=motion.Bodies.Select(p=>p.GetComponent<SpriteRenderer>()).ToArray();PrefabUtility.RecordPrefabInstancePropertyModifications(owner);EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);EditorSceneManager.SaveScene(owner.gameObject.scene);AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/StartMenu.unity");return "Recruitment UI, confirmation, six editable resident standees connected. Additional standees reuse placeholder art.";
 }
}
