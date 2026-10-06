using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
public static class InspectTradeLayout {
 public static string Run(){var root=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab");var p=root.GetComponent<SettlementController>().VisitorPanel;return string.Join("\n",p.GetComponentsInChildren<RectTransform>(true).Where(t=>!t.name.StartsWith("Item")&&(!t.parent||!t.parent.name.StartsWith("Item"))).Select(t=>Path(t,p.transform)+" active="+t.gameObject.activeSelf+" xy="+t.anchoredPosition+" size="+t.sizeDelta+" text="+(t.GetComponent<Text>()?t.GetComponent<Text>().text:"")));}
 static string Path(Transform t,Transform root)=>t==root?t.name:Path(t.parent,root)+"/"+t.name;
}
