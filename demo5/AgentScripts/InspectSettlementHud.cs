using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
public static class InspectSettlementHud {
 public static string Run(){var g=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Settlement/SettlementScreen.prefab");try{var c=g.GetComponent<SettlementController>();return string.Join("\n",c.Main.GetComponentsInChildren<RectTransform>(true).Where(t=>t.parent==c.Main.transform||t.name=="Roster"||t.name=="MemberCard"||t.name=="Clock").Select(t=>t.name+" pos="+t.anchoredPosition+" size="+t.sizeDelta))+"\nMEMBERS="+c.Members.Length;}finally{PrefabUtility.UnloadPrefabContents(g);}}
}
