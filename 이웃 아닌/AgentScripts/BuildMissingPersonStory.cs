using System;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class BuildMissingPersonStory
{
 const string Folder="Assets/Prefabs/Settlement/";
 static SettlementTutorialNarrative.Line L(string id,string text)=>new SettlementTutorialNarrative.Line{SpeakerId=id,Text=text};
 public static string Run()
 {
  if(EditorApplication.isPlaying)throw new Exception("Stop Play Mode first.");
  var copy=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"TutorialNarrativeView.prefab"));
  copy.name="MissingPersonStoryView";
  foreach(var hud in copy.GetComponentsInChildren<PopupBackgroundHud>(true))Object.DestroyImmediate(hud);
  foreach(var name in new[]{"Facts","PartyCondition","ArrivalFade","SkipTutorial","SkipHint"}){var t=copy.transform.Find(name);if(t)t.gameObject.SetActive(false);}
  var asset=PrefabUtility.SaveAsPrefabAsset(copy,Folder+"MissingPersonStoryView.prefab");Object.DestroyImmediate(copy);
  var root=PrefabUtility.LoadPrefabContents(Folder+"SettlementScreen.prefab");
  try{
   var c=root.GetComponent<SettlementController>();
   var s=root.GetComponent<MissingPersonStory>()??root.AddComponent<MissingPersonStory>();s.Owner=c;c.MissingPerson=s;
   var old=root.transform.Find("MissingPersonStoryView");if(old)Object.DestroyImmediate(old.gameObject);
   s.View=(GameObject)PrefabUtility.InstantiatePrefab(asset,root.transform);s.View.name="MissingPersonStoryView";
   var v=s.View.transform;s.Next=v.Find("Next").GetComponent<Button>();s.Surface=v.Find("Paper").GetComponent<Button>();
   s.Title=v.Find("SceneTitle").GetComponent<Text>();s.Speaker=v.Find("Speaker").GetComponent<Text>();s.Body=v.Find("Body").GetComponent<Text>();
   s.NextLabel=s.Next.GetComponentInChildren<Text>();s.Hint=v.Find("ReadingHint").GetComponent<Text>();s.Portrait=v.Find("Portrait").GetComponent<Image>();
   s.DiscoveryLines=new[]{L("medic","인수증과 같은 도장이야. 수리품 목록도 맞아.\n금례 씨 가게에서 맡긴 시계들이네."),L("scout","인계 표찰엔 ‘미란 세탁소’라고 적혀 있어.\n이쪽 사람이 물건을 옮겼나 봐."),L("medic","물건이 여기 왔다고 금례 씨도 계셨다는 건 아니지.\n이 이름부터 기록해 두자.")};
   s.ReturnLines=new[]{L("medic","미란 세탁소… 금례 씨와 어떤 사이인지 알아봐야겠어."),L("scout","보관표만으로는 어디 계신지 몰라.\n세탁소 위치부터 확인하자."),L("medic","다음에 물어볼 이름은 생겼네.\n인수증하고 함께 기록해 둘게.")};
   s.State=new SavedMissingPerson();s.View.SetActive(false);
   var a=c.ArrivalPanel;
   if(a.Loot.Sites[5].Room!=1||!string.IsNullOrEmpty(a.Loot.Sites[5].RequiredTool))throw new Exception("Clue site must be the accessible corridor crate.");
   a.ObjectNames[5]="복도 적재함";
   c.Opening.RecordTabs[0].GetComponentInChildren<Text>().text="금례 수색";
   foreach(var component in root.GetComponentsInChildren<Component>(true))if(component&&PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
   PrefabUtility.SaveAsPrefabAsset(root,Folder+"SettlementScreen.prefab");
  }finally{PrefabUtility.UnloadPrefabContents(root);}
  AssetDatabase.SaveAssets();return "PASS: separate story dialogue and persistent search record connected to corridor crate 5. No loot or encounter changes.";
 }
}
