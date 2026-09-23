using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
public static class ConnectFirstAid {
 public static string Run(){var path="Assets/Prefabs/Settlement/InventoryPanel.prefab";var g=PrefabUtility.LoadPrefabContents(path);try{var c=g.GetComponent<SettlementInventoryPanel>();c.Use=g.transform.Find("Workspace/Use").GetComponent<Button>();c.Use.GetComponentInChildren<Text>().fontSize=26;var cloth=c.Items.First(i=>i.Id=="cloth");cloth.Recovery=1;cloth.UseCost=2;cloth.UseMinutes=10;PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}AssetDatabase.SaveAssets();return "First aid: existing cloth, existing Use button, configurable cost/recovery/time; no new artwork.";}
}
