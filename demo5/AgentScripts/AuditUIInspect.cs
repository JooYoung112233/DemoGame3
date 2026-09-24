using System;using System.IO;using System.Linq;using UnityEngine;using UnityEngine.UI;using Demo5.FrontEnd;
public static class AuditUIInspect {
 static string Path(Transform t){var s=t.name;while(t.parent){t=t.parent;s=t.name+"/"+s;}return s;}
 public static string Run(){var c=UnityEngine.Object.FindAnyObjectByType<SettlementController>();var rows=c.GetComponentsInChildren<Image>(true).Where(i=>i.name=="Dim").Select(i=>Path(i.transform)+" alpha="+i.color.a).ToList();var b=c.ArrivalPanel.Encounter.Battle;rows.Add("Battle detail icon tint="+b.Drawer.DetailIcon.color);rows.Add("Loot Message rect="+c.ArrivalPanel.Loot.Message.rectTransform.rect+" font="+c.ArrivalPanel.Loot.Message.fontSize+" overflow="+c.ArrivalPanel.Loot.Message.verticalOverflow);File.WriteAllLines("Assets/Screenshots/UIAudit/measurements.txt",rows);return string.Join("\n",rows);}
 public static string Finish(){CampaignSaveStore.TestDirectory=null;return "Audit save redirect cleared; no production save was written.";}
}
