using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Demo5.FrontEnd;
public static class ReviewDevelopment
{
 public static async Task<string> Resume(){var c=UnityEngine.Object.FindAnyObjectByType<SettlementController>();CampaignSaveStore.TestDirectory=System.IO.Path.GetFullPath("Temp/DevelopmentVerification");try{var read=CampaignSaveStore.Read(0,c);if(!read.CanLoad)throw new Exception(read.Error);CampaignPersistence.Prepare(read.Data,c);}finally{CampaignSaveStore.TestDirectory=null;}SceneManager.LoadScene("Settlement");await Task.Delay(500);c=UnityEngine.Object.FindAnyObjectByType<SettlementController>();foreach(var m in c.CraftPanel.Materials)m.Initial=Math.Min(m.Initial,2);return "Loaded isolated verification save; no player slot touched";}
 public static string CheckWorld(){var c=UnityEngine.Object.FindAnyObjectByType<SettlementController>();var world=UnityEngine.Object.FindAnyObjectByType<SettlementDevelopmentWorld>();if(!world.ResearchTable.activeInHierarchy||!world.StorageCrates.activeInHierarchy)throw new Exception("Built props hidden");foreach(var crop in UnityEngine.Object.FindObjectsByType<ForegroundOccluder>()){var sprite=crop.GetComponent<SpriteRenderer>();if(sprite.bounds.size.x>2||sprite.bounds.size.y>6)throw new Exception("Full-background occluder "+crop.name);}return "PASS restored build visuals and narrow foreground geometry";}
 public static string OpenResearch(){var c=UnityEngine.Object.FindAnyObjectByType<SettlementController>();c.Development.Open();c.CraftPanel.FocusRecipe("research-tools");return "Research detail shown";}
 public static string Close(){var c=UnityEngine.Object.FindAnyObjectByType<SettlementController>();if(c.CraftPanel.IsOpen)c.CraftPanel.Close();return "Closed";}
 public static async Task<string> Selection(){SceneManager.LoadScene("PartySelection");await Task.Delay(400);return "Named party selection ready";}
 public static string Bounds(){Canvas.ForceUpdateCanvases();var texts=UnityEngine.Object.FindObjectsByType<Text>().Where(t=>t.gameObject.activeInHierarchy&&!string.IsNullOrWhiteSpace(t.text)&&t.text.Length>2).Where(t=>t.preferredHeight>t.rectTransform.rect.height+3).Select(t=>t.name+":"+t.preferredHeight+"/"+t.rectTransform.rect.height+" "+t.text.Replace("\n"," | ")).ToArray();return string.Join("\n",texts);}
}
