using UnityEditor;
using UnityEngine;
using Demo5.FrontEnd;
public static class FixArrivalStatus {
 static void Fix(ExpeditionArrivalPanel c){c.Status.fontSize=24;c.Status.rectTransform.sizeDelta=new Vector2(432,72);}
 public static string Run(){var path="Assets/Prefabs/Settlement/ExpeditionArrivalPanel.prefab";var g=PrefabUtility.LoadPrefabContents(path);try{Fix(g.GetComponent<ExpeditionArrivalPanel>());PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}var c=Object.FindAnyObjectByType<SettlementController>();if(c)Fix(c.ArrivalPanel);AssetDatabase.SaveAssets();return "Arrival status fits two lines with panel inset.";}
}
