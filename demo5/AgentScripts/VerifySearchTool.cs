using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class VerifySearchTool {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();b.GetComponentInParent<Canvas>().worldCamera?.Render();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(100);}
 static async Task Craft(SettlementController c,int recipe,int count){await Tap(c.Workbench);await Tap(c.CraftPanel.RecipeRows[recipe].Button);await Tap(c.CraftPanel.WorkerRows[0].Button);for(int i=1;i<count;i++)await Tap(c.CraftPanel.Plus);await Tap(c.CraftPanel.Confirm);await Tap(c.Advance);await Tap(c.TimePanel.Choices[3]);await Tap(c.TimePanel.Confirm);await Tap(c.TimePanel.CloseButton);}
 public static async Task<string> Preview(){
  var c=Object.FindAnyObjectByType<SettlementController>();var a=c.ArrivalPanel;var people=c.Campaign.Party.ToArray();var destination=c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall");
  await Craft(c,1,2);await Craft(c,0,1);Check(c.InventoryPanel.StockCount("prybar")==1,"Crafted tool missing");
  Check(a.Begin(people,destination),"Departure");await Task.Delay(850);await Tap(a.Objects[2]);await Tap(a.Search.Cards[0].Button);int turn=a.Rooms.Turns,minute=c.Campaign.MinuteOfDay;
  Check(!a.Search.Choose.interactable&&!a.Loot.Advance(2,1,people[0]),"Stock-only tool allowed search");Check(a.Rooms.Turns==turn&&c.Campaign.MinuteOfDay==minute&&!a.Loot.State(2).Opened,"Blocked action spent time/opened");
  await Tap(a.Search.Back);await Tap(a.Return);await Tap(a.ReturnConfirm);await Tap(c.ReturnPanel.Back);
  Check(c.InventoryPanel.MoveFor(people[0],"prybar",1,true),"Packing tool");Check(a.Begin(people,destination),"Second departure");await Task.Delay(850);await Tap(a.Objects[2]);await Tap(a.Search.Cards[1].Button);Check(!a.Search.Choose.interactable,"Wrong worker can use another bag");await Tap(a.Search.Cards[0].Button);Check(a.Search.Choose.interactable&&a.Search.ToolIcon.sprite!=null,"Carrier not eligible");
  Canvas.ForceUpdateCanvases();Check(a.Search.Equipment.preferredHeight<=a.Search.Equipment.rectTransform.rect.height+1,"Tool line clipped");Check(a.Search.Cost.preferredHeight<=a.Search.Cost.rectTransform.rect.height+1,"Search cost clipped");return "PASS: crafted nails then prybar, stock-only blocked without time cost, actual carrier packing and worker gating; ready preview.";
 }
 public static async Task<string> Flow(){
  var c=Object.FindAnyObjectByType<SettlementController>();var a=c.ArrivalPanel;var people=c.Campaign.Party.ToArray();int before=a.Rooms.Turns;
  await Tap(a.Search.Choose);await Tap(a.Search.Cancel);Check(a.Rooms.Turns==before&&!a.Loot.State(2).Opened,"Cancelled preview mutated state");
  await Tap(a.Search.Choose);await Tap(a.Search.Confirm);Check(a.Loot.State(2).Opened&&a.Loot.State(2).Progress==1&&a.Rooms.Turns==before+1,"Opening/first search must be one turn");Check(c.InventoryPanel.CountFor(people[0],"prybar")==1,"Reusable tool consumed");await Tap(a.Search.Back);
  await Tap(a.Return);await Tap(a.ReturnConfirm);await Tap(c.ReturnPanel.StoreAll);await Tap(c.ReturnPanel.Back);
  Check(a.Begin(people,c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")),"Revisit");await Task.Delay(850);await Tap(a.Objects[2]);await Tap(a.Search.Cards[1].Button);Check(a.Search.Choose.interactable&&a.Search.Equipment.text.Contains("개방 완료"),"Opened access did not persist without tool");
  // Avoid random combat in this non-combat UI regression, then restore the inspector setting.
  int maximum=a.Encounter.MaximumChance;try{a.Encounter.MaximumChance=0;await Tap(a.Search.Choose);await Tap(a.Search.Confirm);}finally{a.Encounter.MaximumChance=maximum;}
  Check(a.Loot.State(2).Complete&&a.Loot.IsOpen&&!a.Loot.Advance(2,0,people[1]),"Completion/reward reroll gate");return "PASS: cancel is free, tool reused, first opening included in search turn, return/revisit without tool resumes, completion opens loot and cannot reroll.";
 }
}

