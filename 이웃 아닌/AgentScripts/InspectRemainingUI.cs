using System;using System.Linq;using System.Text;using UnityEditor;using UnityEngine;using UnityEngine.UI;using Demo5.FrontEnd;
public static class InspectRemainingUI {
 public static string Run(){var sb=new StringBuilder();foreach(var path in new[]{"Settlement/ExpeditionReturnPanel","Settlement/ReturnMemberRow","Settlement/ReturnItemRow","Settlement/SettlementTimePanel","Settlement/TimeWorkRow"}){
  var g=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/"+path+".prefab");if(!g){sb.AppendLine("MISSING "+path);continue;}sb.AppendLine("--- "+path);
  foreach(var r in g.GetComponentsInChildren<RectTransform>(true)){
   if(r==g.transform||r.GetComponent<VerticalLayoutGroup>()||r.GetComponent<Text>()||r.GetComponent<Button>()||r.GetComponent<ScrollRect>()||r.name.Contains("Panel")||r.name.Contains("Paper")||r.name=="Dim"){
    string s=r.name;var layout=r.GetComponent<VerticalLayoutGroup>();if(layout)sb.AppendLine("LAYOUT spacing="+layout.spacing+" padding="+layout.padding.top+","+layout.padding.bottom);var p=r.parent;while(p&&p!=g.transform){s=p.name+"/"+s;p=p.parent;}
    var t=r.GetComponent<Text>();sb.AppendLine(s+" pos="+r.anchoredPosition+" size="+r.sizeDelta+(t?" font="+t.fontSize+" text="+t.text.Replace("\n","/"):""));
   }
  }
 }return sb.ToString();}
}


