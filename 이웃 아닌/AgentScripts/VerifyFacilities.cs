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
public static class VerifyFacilities
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static SettlementController Owner()=>Object.FindAnyObjectByType<SettlementController>();
 static void Bounds(GameObject g){Canvas.ForceUpdateCanvases();foreach(var t in g.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height,"Overflow "+t.name+": "+t.text);}
 static async Task Tap(Button b){await Task.Delay(60);Check(b&&b.IsActive()&&b.IsInteractable(),"Unavailable "+b?.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(90);}
 static async Task Recipe(SettlementController c,string id,int worker){await Tap(c.Workbench);c.CraftPanel.FocusRecipe(id);await Tap(c.CraftPanel.WorkerRows[worker].Button);Bounds(c.CraftPanel.View);}
 static async Task Cook(SettlementController c,int worker){await Tap(c.Stock);await Tap(c.CookingPanel.WorkerRows[worker].Button);await Tap(c.CookingPanel.Confirm);}
 public static async Task<string> Flow(){
  var c=Owner();var f=c.CraftPanel;var party=c.Campaign.Party.ToArray();foreach(var m in f.Materials)m.Initial=20;
  Check(f.BedLevel==1&&f.BenchLevel==1&&f.CookerLevel==1,"Initial facility levels");party[0].Health=1;
  await Tap(c.Bed);await Tap(c.WorkPanel.Rows[0].Button);await Tap(c.WorkPanel.Confirm);
  await Recipe(c,"repair-bed",1);Check(!f.Confirm.interactable,"Upgrade over rest");f.Confirm.onClick.Invoke();Check(!f.UnderConstruction("repair-bed"),"Rest collision bypass");await Tap(f.CloseButton);c.WorkPanel.Cancel(party[0]);
  await Recipe(c,"nails",0);await Tap(f.Confirm);await Recipe(c,"upgrade-bench",1);Check(!f.Confirm.interactable,"Upgrade over craft");await Tap(f.CloseButton);c.Campaign.AdvanceSettlementTime(15);
  await Cook(c,0);await Tap(c.Stock);await Tap(c.CookingPanel.Improve);await Tap(f.WorkerRows[1].Button);Check(!f.Confirm.interactable,"Upgrade over cooking");await Tap(f.CloseButton);
  await Tap(c.Stock);await Tap(c.CookingPanel.OrderRows[0].Cancel);await Tap(c.CookingPanel.CancelYes);await Tap(c.CookingPanel.Back);
  int wood=f.Materials.First(m=>m.Id=="wood").Initial;
  await Recipe(c,"upgrade-cooker",0);Check(!f.Plus.interactable,"Construction quantity");await Tap(f.Confirm);c.Campaign.AdvanceSettlementTime(10);
  await Tap(c.Stock);await Tap(c.CookingPanel.WorkerRows[1].Button);Check(!c.CookingPanel.Confirm.interactable,"Cooking during construction");c.CookingPanel.Confirm.onClick.Invoke();Check(c.CookingPanel.Orders.Count==0,"Construction bypass");await Tap(c.CookingPanel.Improve);Check(!f.Confirm.interactable,"Duplicate construction");await Tap(f.OrderRows[0].Cancel);await Tap(f.CancelYes);Check(f.Materials.First(m=>m.Id=="wood").Initial==wood&&f.CookerLevel==1,"Cancel consumed materials/improved");await Tap(f.CloseButton);
  await Recipe(c,"upgrade-cooker",0);await Tap(f.Confirm);c.Campaign.AdvanceSettlementTime(10);
  CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/FacilityVerification");var data=CampaignPersistence.Capture(c);Check(CampaignSaveStore.Write(0,data,c,out var error),error);var disk=CampaignSaveStore.Read(0,c);Check(disk.CanLoad,disk.Error);
  CampaignPersistence.Prepare(disk.Data,c);SceneManager.LoadScene("Settlement");await Task.Delay(700);c=Owner();f=c.CraftPanel;party=c.Campaign.Party.ToArray();Check(f.UnderConstruction("upgrade-cooker")&&f.Orders.Single().Minutes==50,"Pending construction restore");c.Campaign.AdvanceSettlementTime(50);Check(f.CookerLevel==2&&f.Materials.First(m=>m.Id=="wood").Initial==wood-3,"Construction completion cost");c.Campaign.AdvanceSettlementTime(10);Check(f.Materials.First(m=>m.Id=="wood").Initial==wood-3,"Repeated cost");
  await Recipe(c,"upgrade-cooker",0);Check(!f.Confirm.interactable&&f.ConfirmLabel.text=="개선 완료","Repeat upgrade");await Tap(f.CloseButton);
  await Cook(c,0);var order=c.CookingPanel.Orders.Single();int expected=f.CookingDuration(order.Recipe.Minutes,order.Quantity);Check(order.Minutes==expected&&expected<order.Recipe.Minutes,"Cooking benefit");c.Campaign.AdvanceSettlementTime(expected);
  await Recipe(c,"repair-bed",0);await Tap(f.Confirm);await Tap(c.Bed);await Tap(c.WorkPanel.Rows[1].Button);Check(!c.WorkPanel.Confirm.interactable,"Rest during construction");c.WorkPanel.Confirm.onClick.Invoke();Check(c.WorkPanel.Orders.Count==0,"Rest bypass");await Tap(c.WorkPanel.CloseButton);c.Campaign.AdvanceSettlementTime(45);Check(f.BedLevel==2,"Bed level");
  party[1].Health=1;await Tap(c.Bed);await Tap(c.WorkPanel.Rows[1].Button);Check(c.WorkPanel.Effect.text.EndsWith("+2"),"Bed recovery preview");await Tap(c.WorkPanel.Confirm);c.Campaign.AdvanceSettlementTime(30);Check(party[1].Health==3,"Bed effect");
  await Recipe(c,"upgrade-bench",0);await Tap(f.Confirm);await Recipe(c,"nails",1);Check(!f.Confirm.interactable,"Craft during construction");f.Confirm.onClick.Invoke();Check(f.Orders.Count==1,"Craft bypass");await Tap(f.CloseButton);
  await Cook(c,1);int remaining=c.CookingPanel.Orders.Single().Minutes;c.Campaign.AdvanceSettlementTime(10);Check(c.CookingPanel.Orders.Single().Minutes==remaining-10,"Other facility stalled");c.Campaign.AdvanceSettlementTime(80);Check(f.BenchLevel==2&&f.DurationFor(f.Recipes.First(r=>r.Id=="nails"),1)==12,"Bench benefit");
  var old=CampaignPersistence.Capture(c);old.Version=4;old.CookerImproved=false;CampaignPersistence.Upgrade(old,c);Check(old.Version==CampaignPersistence.CurrentVersion&&old.BedRepaired&&old.BenchImproved&&!old.CookerImproved,"v4 migration resets old facilities");
  await Cook(c,0);c.Campaign.AdvanceSettlementTime(5);var pending=CampaignPersistence.Capture(c);Check(CampaignSaveStore.Write(1,pending,c,out error),error);
  var bad=CampaignPersistence.Capture(c);var recipe=f.Recipes.First(r=>r.Id=="upgrade-cooker");bad.Craft=new[]{new SavedCraft{MemberId=bad.Members[1].Id,RecipeId=recipe.Id,Quantity=1,Minutes=1,Reserved=recipe.Costs.Select(x=>new SavedCount{Id=x.MaterialId,Count=x.Count}).ToArray()}};Check(!CampaignSaveStore.Write(2,bad,c,out _),"Completed facility pending again accepted");
  await Tap(c.Stock);Bounds(c.CookingPanel.gameObject);await Tap(c.CookingPanel.Back);
  return "PASS: Lv1 defaults; construction/work exclusion in both directions + forced listener checks; cancellation; exact costs/once-only completion; construction disk restore; Lv2 cooking/rest/craft benefits; other-facility concurrency; v4 migration; invalid duplicate saved construction. Restart fixture slot 1.";
 }
 public static async Task<string> Restart(){CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/FacilityVerification");SceneManager.LoadScene("StartMenu");await Task.Delay(650);await Tap(Object.FindAnyObjectByType<TitleMenuController>().ContinueButton);await Task.Delay(750);var c=Owner();Check(c.CraftPanel.CookerLevel==2&&c.CraftPanel.BedLevel==2&&c.CraftPanel.BenchLevel==2&&c.CookingPanel.Orders.Count==1,"Restart facilities");var o=c.CookingPanel.Orders.Single();int count=c.CraftPanel.Materials.First(m=>m.Id==o.OutputId).Initial;int output=o.OutputCount;c.Campaign.AdvanceSettlementTime(o.Minutes);Check(c.CraftPanel.Materials.First(m=>m.Id==o.OutputId).Initial==count+output,"Restored cooking output");await Tap(c.Stock);await Tap(c.CookingPanel.WorkerRows[0].Button);Bounds(c.CookingPanel.gameObject);return "PASS: fresh Play title Continue restores three Lv2 facilities and partial cooking; one output; cooking preview ready.";}
 public static async Task<string> UpgradePreview(){var c=Owner();c.CookingPanel.Close();await Tap(c.Workbench);c.CraftPanel.FocusRecipe("upgrade-cooker");Bounds(c.CraftPanel.View);return "Completed upgrade preview ready.";}
 public static async Task<string> RestPreview(){var c=Owner();c.CraftPanel.Close();await Tap(c.Bed);await Tap(c.WorkPanel.Rows[0].Button);Bounds(c.WorkPanel.View);return "Level 2 rest preview ready.";}
 public static string Cleanup(){CampaignSaveStore.TestDirectory=null;return "Isolated test save path cleared.";}
}

