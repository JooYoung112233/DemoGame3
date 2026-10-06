using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
public static class AdjustSearchDim {
 static void Apply(GameObject root){root.transform.Find("Dim").GetComponent<Image>().color=new Color(0,0,0,.97f);}
 public static string Run(){
  const string path="Assets/Prefabs/Settlement/ExpeditionSearchPanel.prefab";
  var root=PrefabUtility.LoadPrefabContents(path);try{Apply(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
  if(EditorApplication.isPlaying)Apply(Object.FindAnyObjectByType<SettlementController>().ArrivalPanel.Search.gameObject);
  AssetDatabase.SaveAssets();return "Search background dim opacity 0.68 -> 0.97; UI unchanged.";
 }
}
