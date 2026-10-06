using UnityEditor;
using UnityEngine;
using Demo5.FrontEnd;
public static class AlignPopupAlert {
 static void Apply(ExpeditionEncounterPanel c){var r=(RectTransform)c.Banner.transform;r.anchoredPosition=new Vector2(850,-32);r.sizeDelta=new Vector2(990,72);}
 public static string Run(){const string path="Assets/Prefabs/Settlement/ExpeditionArrivalPanel.prefab";var g=PrefabUtility.LoadPrefabContents(path);try{Apply(g.GetComponent<ExpeditionArrivalPanel>().Encounter);PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}if(EditorApplication.isPlaying)Apply(Object.FindAnyObjectByType<SettlementController>().ArrivalPanel.Encounter);AssetDatabase.SaveAssets();return "Warning banner moved to top-right header row, clear of modal content.";}
}
