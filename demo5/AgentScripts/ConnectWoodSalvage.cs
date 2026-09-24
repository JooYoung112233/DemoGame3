using System.Linq;
using UnityEditor;
using UnityEngine;
using Demo5.FrontEnd;
public static class ConnectWoodSalvage {
 const string P="Assets/Prefabs/Settlement/";
 public static string Run(){if(EditorApplication.isPlaying||UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty)throw new System.Exception("Stop Play and preserve the scene first");
  // Base table only: SettlementScreen overrides Sites[1] (BuildBalance1.cs, which also runs after this in any re-apply chain).
  var path=P+"ExpeditionLootPanel.prefab";var g=PrefabUtility.LoadPrefabContents(path);try{var site=g.GetComponent<ExpeditionLootPanel>().Sites[1];var wood=site.Drops.FirstOrDefault(d=>d.Id=="wood");if(wood==null){wood=new ExpeditionLootPanel.Drop{Id="wood"};site.Drops=site.Drops.Concat(new[]{wood}).ToArray();}wood.Count=2;wood.Chance=75;PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}
  // Description [1] is owned by BuildBalance1.cs.
  AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Old table now offers 2 wood at 75% base, using existing persistent search/loot flow.";
 }
}
