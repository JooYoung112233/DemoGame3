using System.Linq;using UnityEngine;using Demo5.FrontEnd;
public static class InspectMergedRuntime {
 public static string Run(){return string.Join("\n",Object.FindObjectsByType<SettlementController>(FindObjectsInactive.Include).Select(c=>"controller="+c.name+" scene="+c.gameObject.scene.name+" active="+c.gameObject.activeInHierarchy+" campaign="+(c.Campaign!=null)+" opening="+(c.Opening?c.Opening.name:"null")+" state="+(c.Opening&&c.Opening.State!=null?JsonUtility.ToJson(c.Opening.State):"null")));}
}
