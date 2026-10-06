using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Demo5.FrontEnd;
public static class InspectBattleUi {
 public static string Run(){
 var b=Object.FindAnyObjectByType<ExpeditionBattlePanel>(FindObjectsInactive.Include);
 return "Open="+b.IsOpen+" background="+AssetDatabase.GetAssetPath(b.BattleBackground)+"\n"+
 string.Join("\n",b.AimTooltip.GetComponentsInChildren<Text>(true).Select(t=>t.name+" text="+t.text+" rect="+t.rectTransform.anchoredPosition+" size="+t.rectTransform.sizeDelta+" anchor="+t.rectTransform.anchorMin+" active="+t.gameObject.activeSelf+" color="+t.color))+"\n"+
 string.Join("\n",b.AimTooltip.GetComponentsInChildren<RectTransform>(true).Select(t=>t.name+" pos="+t.anchoredPosition+" size="+t.sizeDelta));
 }
}
