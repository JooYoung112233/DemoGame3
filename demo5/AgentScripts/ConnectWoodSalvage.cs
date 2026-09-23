using System.Linq;
using UnityEditor;
using UnityEngine;
using Demo5.FrontEnd;
public static class ConnectWoodSalvage {
 const string P="Assets/Prefabs/Settlement/";
 public static string Run(){if(EditorApplication.isPlaying)throw new System.Exception("Stop first");
  var path=P+"ExpeditionLootPanel.prefab";var g=PrefabUtility.LoadPrefabContents(path);try{var site=g.GetComponent<ExpeditionLootPanel>().Sites[1];var wood=site.Drops.FirstOrDefault(d=>d.Id=="wood");if(wood==null){wood=new ExpeditionLootPanel.Drop{Id="wood"};site.Drops=site.Drops.Concat(new[]{wood}).ToArray();}wood.Count=2;wood.Chance=75;PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}
  path=P+"ExpeditionArrivalPanel.prefab";g=PrefabUtility.LoadPrefabContents(path);try{g.GetComponent<ExpeditionArrivalPanel>().ObjectDescriptions[1]="헐거워진 판자를 회수할 수 있을 듯하다.\n탁자 주변의 천과 못도 살펴볼 수 있다.";PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}
  AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Old table now offers 2 wood at 75% base, using existing persistent search/loot flow.";
 }
}
