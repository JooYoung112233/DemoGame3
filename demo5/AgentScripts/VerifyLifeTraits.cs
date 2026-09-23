using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class VerifyLifeTraits
{
 static SettlementController Owner()=>Object.FindAnyObjectByType<SettlementController>();
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
 static void Bounds(GameObject g){Canvas.ForceUpdateCanvases();foreach(var t in g.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Overflow "+t.name+": "+t.text);}
 static async Task Tap(Button b){await Task.Delay(80);Check(b&&b.IsActive()&&b.IsInteractable(),"Unavailable "+b?.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(90);}
 static async Task Cook(int worker){var c=Owner();await Tap(c.Stock);await Tap(c.CookingPanel.WorkerRows[worker].Button);Bounds(c.CookingPanel.gameObject);}
 static async Task Craft(string id,int worker){var c=Owner();await Tap(c.Workbench);c.CraftPanel.FocusRecipe(id);await Tap(c.CraftPanel.WorkerRows[worker].Button);Bounds(c.CraftPanel.View);}
 static async Task Load(int slot){var c=Owner();var disk=CampaignSaveStore.Read(slot,c);Check(disk.CanLoad,disk.Error);CampaignPersistence.Prepare(disk.Data,c);SceneManager.LoadScene("Settlement");await Task.Delay(700);}
 public static async Task<string> Fresh(){CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/LifeTraitsVerification");CampaignPersistence.ClearPending();PartySelectionSession.Clear();SceneManager.LoadScene("PartySelection");await Task.Delay(650);var p=Object.FindAnyObjectByType<PartySelectionController>();await Tap(p.Cards[1].Button);await Tap(p.Cards[3].Button);await Tap(p.Continue);await Task.Delay(500);var h=Object.FindAnyObjectByType<HomeSelectionController>();await Tap(h.Cards[0].Button);await Tap(h.Continue);await Task.Delay(600);foreach(var m in Owner().CraftPanel.Materials)m.Initial=100;return "Mechanic and cook ready.";}
 public static async Task<string> Flow(){
  var c=Owner();var cook=c.CookingPanel;var craft=c.CraftPanel;var p=c.Campaign.Party.ToArray();var meal=cook.Meals.First(m=>m.Id=="warm");var nails=craft.Recipes.First(r=>r.Id=="nails");
  Check(cook.DurationFor(meal,1,p[0])==20&&cook.DurationFor(meal,1,p[1])==16,"Cook/base time");Check(craft.DurationFor(nails,1,p[0])==12&&craft.DurationFor(nails,1,p[1])==15,"Mechanic/base time");
  await Cook(0);Check(cook.Duration.text.Contains("20분")&&cook.TraitEffect.text.Contains("담당 기본"),"Normal worker preview");await Tap(cook.WorkerRows[1].Button);Check(cook.Duration.text.Contains("20 → 16")&&cook.TraitEffect.text.Contains("담당 −20%"),"Specialist preview");await Tap(cook.Plus);Check(cook.Duration.text.Contains("40 → 32"),"Batch preview");await Tap(cook.Minus);await Tap(cook.Tabs[1]);Check(cook.TraitEffect.text.Contains("특성 제외")&&cook.DurationFor(cook.Meals.First(m=>m.Id=="ration"),1,p[1])==10,"Packing gained trait");await Tap(cook.Tabs[2]);Check(cook.DurationFor(cook.Meals.First(m=>m.Id=="canned"),1,p[1])==0,"Zero time changed");await Tap(cook.Tabs[0]);await Tap(cook.Confirm);Check(cook.Orders.Single().Minutes==16,"Queue differs from preview");
  await Cook(0);Check(!cook.WorkerRows[1].Button.interactable,"Busy specialist selectable");await Tap(cook.OrderRows[0].Cancel);await Tap(cook.CancelYes);Check(cook.Orders.Count==0&&craft.Materials.First(m=>m.Id=="food").Initial==100,"Cancel consumed food");await Tap(cook.Back);
  var upgraded=CampaignPersistence.Capture(c);upgraded.BenchImproved=upgraded.CookerImproved=true;craft.RestoreSaved(upgraded);Check(cook.DurationFor(meal,1,p[1])==13&&cook.DurationFor(meal,3,p[1])==39,"Multiplicative single ceiling cooking");Check(craft.DurationFor(nails,3,p[0])==29,"Single ceiling crafting");
  await Cook(1);Check(cook.Duration.text.Contains("20 → 13")&&cook.TraitEffect.text.Contains("시설 −20% · 담당 −20%"),"Stack display");await Tap(cook.Confirm);await Craft("nails",0);await Tap(craft.Plus);await Tap(craft.Plus);Check(craft.Duration.text.Contains("45 → 29"),"Craft stack display");await Tap(craft.Confirm);Check(cook.Orders.Single().Minutes==13&&craft.Orders.Single().Minutes==29,"Concurrent queue");
  int food=craft.Materials.First(m=>m.Id=="food").Initial,scrap=craft.Materials.First(m=>m.Id=="scrap").Initial;var cookCost=cook.Orders.Single().Reserved.ToDictionary(x=>x.Key,x=>x.Value);var craftCost=craft.Orders.Single().Reserved.ToDictionary(x=>x.Key,x=>x.Value);int meals=craft.Materials.First(m=>m.Id=="meal").Initial,nailCount=craft.Materials.First(m=>m.Id=="nails").Initial;
  c.Campaign.AdvanceSettlementTime(5);Check(CampaignSaveStore.Write(0,CampaignPersistence.Capture(c),c,out var error),error);await Load(0);c=Owner();cook=c.CookingPanel;craft=c.CraftPanel;p=c.Campaign.Party.ToArray();Check(cook.Orders.Single().Minutes==8&&craft.Orders.Single().Minutes==24,"Partial save lost time");
  // Facility settings changed after reservation must not recalculate pending orders.
  int percent=craft.CookerTimePercent;craft.CookerTimePercent=50;Check(cook.Orders.Single().Minutes==8,"Pending conditions changed");craft.CookerTimePercent=percent;
  c.Campaign.AdvanceSettlementTime(8);Check(cook.Orders.Count==0&&craft.Materials.First(m=>m.Id=="meal").Initial==meals+2,"Output changed with speed");Check(cookCost.All(x=>craft.Materials.First(m=>m.Id==x.Key).Initial==100-x.Value),"Cooking cost changed");c.Campaign.AdvanceSettlementTime(16);Check(craft.Orders.Count==0&&craft.Materials.First(m=>m.Id=="nails").Initial==nailCount+3,"Craft completion");Check(craft.Materials.First(m=>m.Id=="scrap").Initial==scrap-craftCost["scrap"],"Craft exact cost");c.Campaign.AdvanceSettlementTime(60);Check(craft.Materials.First(m=>m.Id=="meal").Initial==meals+2&&craft.Materials.First(m=>m.Id=="nails").Initial==nailCount+3,"Duplicate output");
  await Craft("repair-bed",0);Check(craft.Duration.text.Contains("→"),"Repair benefit missing");await Tap(craft.Confirm);Check(craft.Orders.Single().Minutes==craft.DurationFor(craft.Recipes.First(r=>r.Id=="repair-bed"),1,p[0]),"Repair snapshot");await Tap(c.Workbench);await Tap(craft.OrderRows[0].Cancel);await Tap(craft.CancelYes);await Tap(craft.CloseButton);
  return "PASS: ordinary/specialist, batch, exclusions, zero-minute recipe, single ceiling combined effects, real UI selection/blocked busy/cancel, snapshot queue, partial file reload, unchanged cost/output and once-only completion, repair application.";
 }
 public static async Task<string> Restart(){CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/LifeTraitsVerification");SceneManager.LoadScene("StartMenu");await Task.Delay(650);await Tap(Object.FindAnyObjectByType<TitleMenuController>().ContinueButton);await Task.Delay(750);var c=Owner();Check(c.CookingPanel.Orders.Single().Minutes==8&&c.CraftPanel.Orders.Single().Minutes==24,"Restart snapshot changed");return "PASS: fresh Play Continue preserved trait-adjusted remaining times.";}
 public static async Task<string> Preview(){var c=Owner();if(c.CookingPanel.IsOpen)c.CookingPanel.Close();if(c.CraftPanel.IsOpen)c.CraftPanel.Close();c.Campaign.AdvanceSettlementTime(24);await Cook(1);return "Cooking trait preview ready.";}
 public static async Task<string> CraftPreview(){var c=Owner();c.CookingPanel.Close();await Craft("nails",0);await Tap(c.CraftPanel.Plus);await Tap(c.CraftPanel.Plus);Bounds(c.CraftPanel.View);return "Craft trait preview ready.";}
 public static string Cleanup(){CampaignSaveStore.TestDirectory=null;return "Test directory cleared.";}
}
