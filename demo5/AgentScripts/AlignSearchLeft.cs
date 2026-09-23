using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
public static class AlignSearchLeft {
 static void Apply(ExpeditionSearchPanel c){
  var title=(RectTransform)c.Workspace.transform.Find("TitlePaper");float delta=80-title.anchoredPosition.x;
  foreach(Transform child in c.Workspace.transform){var r=child as RectTransform;if(!r)continue;var p=r.anchoredPosition;if(p.x<700&&-p.y>=238&&-p.y<=932)r.anchoredPosition=p+new Vector2(delta,0);}
  c.ObjectIcon.rectTransform.anchoredPosition=new Vector2(107,-340);
  var paper=(RectTransform)c.Workspace.transform.Find("ObjectPaper");c.ObjectIcon.rectTransform.anchoredPosition=new Vector2(paper.anchoredPosition.x+(paper.rect.width-c.ObjectIcon.rectTransform.rect.width)/2,c.ObjectIcon.rectTransform.anchoredPosition.y);
 }
 public static string Run(){
  const string path="Assets/Prefabs/Settlement/ExpeditionSearchPanel.prefab";
  var g=PrefabUtility.LoadPrefabContents(path);try{Apply(g.GetComponent<ExpeditionSearchPanel>());PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}
  if(EditorApplication.isPlaying){var c=Object.FindAnyObjectByType<SettlementController>().ArrivalPanel.Search;Apply(c);c.Equipment.rectTransform.anchoredPosition=new Vector2(126,c.Equipment.rectTransform.anchoredPosition.y);}
  AssetDatabase.SaveAssets();return "Left search content aligned to HUD and back-button x=80; icon centered in paper.";
 }
}
