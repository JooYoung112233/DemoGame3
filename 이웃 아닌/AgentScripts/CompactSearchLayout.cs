using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
public static class CompactSearchLayout {
 static void R(Component c,float x,float y,float w,float h){var r=(RectTransform)c.transform;r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
 static void Apply(ExpeditionSearchPanel c){
  var p=c.DropPreview;R(p,738,238,1134,694);R(p.transform.Find("Paper"),0,0,1134,694);R(p.transform.Find("Title"),28,14,1078,48);R(p.Summary,28,66,1078,72);
  R(p.transform.Find("ItemHeading"),106,142,600,30);R(p.transform.Find("CountHeading"),800,142,90,30);R(p.transform.Find("ChanceHeading"),950,142,120,30);
  var viewport=(RectTransform)p.Content.parent;R(viewport,28,182,1078,430);p.Content.sizeDelta=new Vector2(1058,p.Content.sizeDelta.y);R(p.Note,28,638,1078,40);
  R(c.Back,80,974,410,76);R(c.Choose,1442,974,410,76);R(c.Notice,520,978,892,68);c.Notice.fontSize=22;c.Notice.alignment=TextAnchor.MiddleCenter;
  foreach(var row in p.Rows)AlignRow(row);
 }
 static void AlignRow(SearchDropRow r){((RectTransform)r.transform).sizeDelta=new Vector2(1058,64);R(r.Name,78,0,690,64);R(r.Quantity,772,0,90,64);R(r.Chance,922,0,120,64);}
 public static string Run(){
  string path="Assets/Prefabs/Settlement/SearchDropRow.prefab";var g=PrefabUtility.LoadPrefabContents(path);try{AlignRow(g.GetComponent<SearchDropRow>());PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}
  path="Assets/Prefabs/Settlement/ExpeditionSearchPanel.prefab";g=PrefabUtility.LoadPrefabContents(path);try{Apply(g.GetComponent<ExpeditionSearchPanel>());PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}
  if(EditorApplication.isPlaying){var a=Object.FindAnyObjectByType<SettlementController>().ArrivalPanel;Apply(a.Search);if(a.Search.IsOpen)a.MemberContent.parent.gameObject.SetActive(false);Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(a.Search.DropPreview.Content);}
  AssetDatabase.SaveAssets();return "24px panel gap, shared panel top/bottom, same 410x76 footer buttons at y=974; notice centered in footer.";
 }
}
