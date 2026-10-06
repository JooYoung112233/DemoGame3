using System;using System.Linq;using UnityEngine;using UnityEngine.UI;using UnityEditor;using UnityEditor.SceneManagement;using Demo5.FrontEnd;
public static class BuildTutorialClarity {
 static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
 static Text Text(string name,Transform parent,Font font,float x,float y,float w,float h,int size){var t=Rect(name,parent,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.color=new Color(.06f,.09f,.085f);t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;return t;}
 public static string Run(){
  if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
  var roster=AssetDatabase.LoadAssetAtPath<PartyRoster>("Assets/Data/PartyRoster.asset");
  foreach(var c in roster.Candidates)c.AvailableAtStart=c.Id=="scout"||c.Id=="medic";
  EditorUtility.SetDirty(roster);
  string path="Assets/Prefabs/PartySelection/PartySelectionScreen.prefab";var root=PrefabUtility.LoadPrefabContents(path);
  try{var c=root.GetComponent<PartySelectionController>();
   for(int i=0;i<c.Cards.Length;i++){var r=(RectTransform)c.Cards[i].transform;if(i<2){r.anchoredPosition=new Vector2(930+i*270,-104);r.localScale=Vector3.one*1.1f;}c.Cards[i].gameObject.SetActive(i<2);}
   c.Previous.gameObject.SetActive(false);c.NextPage.gameObject.SetActive(false);c.PageNumber.text="";
   foreach(var t in root.GetComponentsInChildren<Text>(true)){
    if(t.name=="Instruction")t.text="함께 피신한 두 사람.\n이름을 눌러보세요.";
    if(t.name=="SummaryLabel")t.text="시작 대원";
   }
   c.Continue.GetComponentInChildren<Text>().text="이 두 사람과 시작";c.Continue.GetComponentInChildren<Text>().fontSize=28;
   c.Message.alignment=TextAnchor.MiddleCenter;c.Message.fontSize=26;
   c.Message.text="인물을 누르면 소개를 볼 수 있습니다. 다른 생존자는 여정 중에 만납니다.";
   PrefabUtility.SaveAsPrefabAsset(root,path);
  }finally{PrefabUtility.UnloadPrefabContents(root);}
  path="Assets/Prefabs/Settlement/SettlementScreen.prefab";root=PrefabUtility.LoadPrefabContents(path);
  try{var c=root.GetComponent<SettlementController>();var guide=root.GetComponent<SettlementTutorialGuide>();if(!guide)guide=root.AddComponent<SettlementTutorialGuide>();guide.Owner=c;
   var old=root.transform.Find("TutorialGuidance");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
   var layer=Rect("TutorialGuidance",root.transform,0,0,1920,1080);
   guide.HeaderPosition=new Vector2(720,-22);
   var paper=Rect("NextActionPaper",layer,720,22,650,142).gameObject.AddComponent<Image>();paper.sprite=c.NoticeBody.transform.parent.GetComponent<Image>().sprite;paper.color=new Color(.93f,.9f,.79f);paper.raycastTarget=false;guide.Banner=paper.gameObject;
   guide.Title=Text("Step",paper.transform,c.NoticeTitle.font,22,10,606,42,27);
   guide.Instruction=Text("Instruction",paper.transform,c.NoticeTitle.font,22,56,606,70,22);
   guide.Instruction.lineSpacing=1.05f;
   guide.Marker=Rect("NextClick",layer,0,0,100,100).gameObject.AddComponent<TutorialTargetGraphic>();guide.Marker.raycastTarget=false;
   guide.Banner.SetActive(false);guide.Marker.gameObject.SetActive(false);
   PrefabUtility.SaveAsPrefabAsset(root,path);
  }finally{PrefabUtility.UnloadPrefabContents(root);}
  AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/PartySelection.unity");
  return "Starting roster: 윤서진/scout + 한해인/medic; remaining 4 retained for visitors. State-based single-action guide connected with click-through corner marks and facility silhouette focus.";
 }
}
