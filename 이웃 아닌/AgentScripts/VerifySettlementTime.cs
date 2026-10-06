using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class VerifySettlementTime {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();b.GetComponentInParent<Canvas>().worldCamera?.Render();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(60);}
 static async Task Craft(SettlementController c,int recipe,int worker,int qty=1){await Tap(c.Workbench);await Tap(c.CraftPanel.RecipeRows[recipe].Button);await Tap(c.CraftPanel.WorkerRows[worker].Button);for(int i=1;i<qty;i++)await Tap(c.CraftPanel.Plus);await Tap(c.CraftPanel.Confirm);}
 public static async Task<string> Preview(){var c=Object.FindAnyObjectByType<SettlementController>();c.Campaign.Party.Last().Health=1;await Craft(c,1,0,2);await Tap(c.Bed);await Tap(c.WorkPanel.Rows[1].Button);await Tap(c.WorkPanel.Confirm);await Tap(c.Advance);return "Two jobs and time preview ready.";}
 public static async Task<string> Facilities(){
  var c=Object.FindAnyObjectByType<SettlementController>();var p=c.CraftPanel;if(c.TimePanel.IsOpen)c.TimePanel.Close();
  foreach(var m in p.Materials)m.Initial=20;
  await Tap(c.Workbench);await Tap(p.Tabs[1]);await Tap(p.WorkerRows[0].Button);Check(!p.Plus.interactable,"Facility quantity allowed");await Tap(p.Confirm);
  c.Campaign.AdvanceSettlementTime(45);Check(p.BedRepaired,"Bed repair did not apply");await Tap(c.Workbench);await Tap(p.Tabs[1]);await Tap(p.WorkerRows[0].Button);Check(!p.Confirm.interactable,"Repeated facility repair allowed");await Tap(p.CloseButton);
  await Tap(c.Workbench);await Tap(p.Tabs[2]);await Tap(p.WorkerRows[0].Button);await Tap(p.Confirm);c.Campaign.AdvanceSettlementTime(90);Check(p.BenchImproved&&p.DurationFor(p.Recipes.First(r=>r.Id=="nails"),1)==12,"Bench duration effect");
  var person=c.Campaign.Party.Last();person.Health=1;await Tap(c.Bed);await Tap(c.WorkPanel.Rows[1].Button);await Tap(c.WorkPanel.Confirm);c.Campaign.AdvanceSettlementTime(30);Check(person.Health==3,"Repaired bed recovery");
  for(int i=0;i<6;i++)c.ActivityLog.Add("스크롤 검증 · 작업 완료 기록 "+i);await Tap(c.Advance);Canvas.ForceUpdateCanvases();Check(c.TimePanel.Scroll.content.rect.height>c.TimePanel.Scroll.viewport.rect.height,"Long log not scrollable");
  c.TimePanel.Scroll.verticalNormalizedPosition=0;await Task.Delay(100);return "PASS: facility single quantity, repeat prevention, repair recovery bonus, bench duration reduction, long completion log scrolling. Materials were test fixture only.";
 }
 public static async Task<string> Flow(){
  var c=Object.FindAnyObjectByType<SettlementController>();var p=c.CraftPanel;var time=c.TimePanel;var people=c.Campaign.Party.ToArray();
  Check(time.IsOpen&&p.Orders.Count==1&&c.WorkPanel.Orders.Count==1,"Prepare Preview first");
  int start=c.Campaign.MinuteOfDay;await Tap(time.CloseButton);Check(c.Campaign.MinuteOfDay==start,"Preview close advanced time");await Tap(c.Advance);await Tap(time.Choices[0]);await Tap(time.Confirm);
  Check(c.Campaign.MinuteOfDay==start+15&&p.Orders[0].Minutes==15&&c.WorkPanel.Orders[0].Minutes==15,"Parallel progress incorrect");Check(p.Available("nails")==0&&people[1].Health==1,"Early reward");
  await Tap(time.Choices[3]);await Tap(time.Confirm);Check(c.Campaign.MinuteOfDay==start+30&&p.Orders.Count==0&&c.WorkPanel.Orders.Count==0,"Completion");Check(p.Available("nails")==2&&p.Available("scrap")==1&&people[1].Health==2,"Rewards/costs");await Tap(time.Confirm);Check(p.Available("nails")==2&&people[1].Health==2,"Duplicate reward");await Tap(time.CloseButton);
  await Craft(c,0,0);await Tap(c.Advance);await Tap(time.Choices[3]);await Tap(time.Confirm);await Tap(time.CloseButton);
  Check(c.InventoryPanel.StockCount("prybar")==1,"Crafted tool missing from inventory");Check(c.InventoryPanel.MoveFor(people[0],"prybar",1,true),"Crafted tool cannot enter bag");Check(c.InventoryPanel.MoveFor(people[0],"prybar",1,false),"Crafted tool cannot return to stock");
  await Craft(c,2,0);await Tap(c.Advance);await Tap(time.Choices[0]);await Tap(time.Confirm);await Tap(time.CloseButton);await Tap(c.Workbench);await Tap(p.OrderRows[0].Cancel);await Tap(p.CancelYes);await Tap(p.CloseButton);Check(p.Available("cloth")==2,"Partial cancellation lost materials");
  // Background craft must consume travel/search time once while its worker stays home.
  await Craft(c,2,0);var target=c.ExpeditionPanel.Destinations.First(x=>x.Id=="mall");Check(c.ArrivalPanel.Begin(new[]{people[1]},target),"Departure failed");await Task.Delay(850);Check(p.Orders.Count==0&&p.Available("cloth")==0,"Travel did not complete home job");
  int before=c.Campaign.MinuteOfDay;c.ArrivalPanel.Rooms.SpendSearchTurn(0);Check(c.Campaign.MinuteOfDay==before+10&&c.ArrivalPanel.Clock.text==c.Campaign.ClockText,"Search time mismatch");
  await Tap(c.ArrivalPanel.Return);await Tap(c.ArrivalPanel.ReturnConfirm);Check(c.Clock.text==c.Campaign.ClockText,"Return clock mismatch");if(c.ReturnPanel&&c.ReturnPanel.IsOpen)await Tap(c.ReturnPanel.Back);
  await Tap(c.Bed);await Tap(c.WorkPanel.Rows[1].Button);await Tap(c.WorkPanel.Confirm);await Tap(c.Bed);await Tap(c.WorkPanel.Rows[1].Button);await Tap(c.WorkPanel.Confirm);Check(c.WorkPanel.Orders.Count==0,"Rest cancellation");
  int day=c.Campaign.Day;int toMidnight=1440-c.Campaign.MinuteOfDay;Check(c.Campaign.AdvanceSettlementTime(toMidnight),"Midnight advance rejected");Check(c.Campaign.Day==day+1&&c.Campaign.MinuteOfDay==0,"Day rollover");
  Check(!c.Campaign.AdvanceSettlementTime(0)&&!c.Campaign.AdvanceSettlementTime(-5),"Invalid time allowed");
  await Tap(c.Advance);return "PASS: actual UI preview/cancel, parallel progress, no early/duplicate rewards, exact costs, crafted tool bag transfers, partial cancel, travel/background job, search clock, return, rest cancellation, date rollover.";
 }
}

