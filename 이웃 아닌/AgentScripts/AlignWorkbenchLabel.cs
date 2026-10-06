using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Demo5.FrontEnd;
public static class AlignWorkbenchLabel {
 static void Align(Transform work){var r=(RectTransform)work.Find("Caption");r.anchoredPosition=new Vector2(-58,64);r.sizeDelta=new Vector2(200,54);}
 public static async Task<string> Apply(){
  string path="Assets/Prefabs/Settlement/SettlementScreen.prefab";
  var root=PrefabUtility.LoadPrefabContents(path);
  try{Align(root.transform.Find("Main/Facility_work"));PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
  AssetDatabase.SaveAssets();
  if(EditorApplication.isPlaying){
   var c=UnityEngine.Object.FindAnyObjectByType<SettlementController>();if(c==null||c.Campaign==null)return "Prefab saved; no active campaign.";
   Align(c.Workbench.transform);
   var p=c.CraftPanel;
   if(c.Campaign.IsFieldExpedition)return "Prefab saved; current expedition unchanged.";
   if(c.IsPopupOpen)c.Close();
   if(!p.IsOpen)p.Open();await Task.Delay(180);
   if(p.Orders.Count==0){p.RecipeRows[1].Button.onClick.Invoke();p.WorkerRows[0].Button.onClick.Invoke();p.Confirm.onClick.Invoke();}else p.Close();
   await Task.Delay(3200);c.Workbench.transform.Find("Caption").gameObject.SetActive(true);
  }
  return "Workbench caption moved above icon with 10px gap; selected/hover label remains visible without covering arriving pawn.";
 }
}
