using UnityEngine;using UnityEngine.UI;using UnityEditor;using Demo5.FrontEnd;
public static class FixRecordsLabels {
 static void Fix(Transform root){var heading=(RectTransform)root.Find("ListHeading");heading.sizeDelta=new Vector2(280,56);var status=(RectTransform)root.Find("Status");status.anchoredPosition=new Vector2(720,-790);status.sizeDelta=new Vector2(816,48);}
 public static string Run(){const string p="Assets/Prefabs/Settlement/SettlementRecords.prefab";var root=PrefabUtility.LoadPrefabContents(p);try{Fix(root.transform);PrefabUtility.SaveAsPrefabAsset(root,p);}finally{PrefabUtility.UnloadPrefabContents(root);}AssetDatabase.SaveAssets();var c=UnityEngine.Object.FindAnyObjectByType<SettlementController>();Fix(c.Opening.RecordsView.transform);return "Record heading and footer given sufficient glyph height.";}
}
