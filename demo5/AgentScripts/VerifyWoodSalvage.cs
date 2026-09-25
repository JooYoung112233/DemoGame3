using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Demo5.FrontEnd;
// 말 놓기 (2026-09-25, 기획/탐험-말놓기-조작-재설계.md): two pawns on the table (함께) and '턴 진행' run the search (FieldPawnTest); the 07
// window and a finished object's finds open with a right press (FieldPawnTest.Detail); a left press on a finished, empty object opens nothing.
public static class VerifyWoodSalvage {
 static SettlementController C=>UnityEngine.Object.FindAnyObjectByType<SettlementController>();
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var cam=b.GetComponentInParent<Canvas>().worldCamera;cam?.Render();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(cam,r.TransformPoint(r.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(90);}
 public static async Task<string> Preview(){
  FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c=C;await Tap(c.Workbench);await Tap(c.CraftPanel.Plus);await Tap(c.CraftPanel.CostRows[0].GetComponent<Button>());Check(c.CraftPanel.MaterialGuide.ActionLabel.text=="원정 계획","Wood source missing");await Tap(c.CraftPanel.MaterialGuide.Action);var p=c.ExpeditionPanel;await Tap(p.Cards[0].Button);await Tap(p.Cards[1].Button);await Tap(p.Pack);await Tap(c.PackingPanel.Ready);await Tap(c.PackingPanel.Depart);await Task.Delay(950);
  var a=c.ArrivalPanel;await Ready(a);var s=a.Search;Check(FieldPawnTest.Coop(a,1),"Two pawns on the table");Check(FieldPawnTest.Detail(a,1)&&s.ReadOnly,"07 of the table");
  var row=s.DropPreview.Rows.Single(r=>r.Name.text=="나무 판자");Check(row.Quantity.text=="×2"&&row.Chance.text==ExpeditionLootPanel.ChanceFor(a.Loot.Sites[1].Drops.First(d=>d.Id=="wood"),a.Loot.BonusFor(1,0))+"%"&&s.DropPreview.Summary.text.Contains("남은 "+a.Loot.RequiredFor(1,0)+"턴"),"Cooperative preview (함께: one turn sooner, no bonus)");int time=c.Campaign.MinuteOfDay;Check(c.Campaign.MinuteOfDay==time&&a.Loot.State(1).Progress==0,"Placing cost");Canvas.ForceUpdateCanvases();foreach(var t in s.View.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Clipped "+t.name);return "PASS crafting shortage → plan → packing → old table; two pawns (함께), 2 wood preview, exact modifier, placing free, text bounds. 07 open.";
 }
 static async Task Ready(ExpeditionArrivalPanel a){for(int i=0;i<20&&!a.Popup.activeSelf&&!FieldPawnTest.Ready(a);i++)await Task.Delay(50);if(a.Popup.activeSelf)a.ClosePopup();for(int i=0;i<60&&!FieldPawnTest.Ready(a);i++)await Task.Delay(50);Check(FieldPawnTest.Ready(a),"Board not ready");}
 static async Task Leave(ExpeditionLootPanel l){await Tap(l.Back);if(l.LeaveReview.activeSelf)await Tap(l.LeaveConfirm);}
 public static async Task<string> Complete(){
  FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c=C;var a=c.ArrivalPanel;var s=a.Search;var l=a.Loot;var encounter=a.Encounter;int bas=encounter.BaseChance,max=encounter.MaximumChance,threshold=encounter.NoiseThreshold;int time=c.Campaign.MinuteOfDay;
  // Isolate resource-flow verification from random battles; restore encounter settings afterwards.
  var wood=l.Sites[1].Drops.First(d=>d.Id=="wood");int woodChance=wood.Chance;
  try{encounter.BaseChance=encounter.MaximumChance=0;encounter.NoiseThreshold=10000;wood.Chance=100;/* pinned: 정밀 +15 and 함께 +10 are gone */if(s.IsOpen)await Tap(s.Back);for(int i=0;i<3&&!l.State(1).Complete;i++){await Ready(a);if(FieldPawnTest.Check(a).RunFor(1)==null)Check(FieldPawnTest.Coop(a,1),"Two pawns on the table");await Task.Delay(250);await Tap(a.Threat.Planner.TurnButton);await Task.Delay(200);}}
  finally{encounter.BaseChance=bas;encounter.MaximumChance=max;encounter.NoiseThreshold=threshold;wood.Chance=woodChance;}
  Check(l.IsOpen&&l.State(1).Complete&&l.State(1).Loot["wood"]==2,"Wood completion");Check(c.Campaign.MinuteOfDay==time+l.State(1).Required*a.Rooms.MinutesPerTurn&&a.Rooms.Turns==l.State(1).Required,"Search time");await Tap(l.FieldRows.Single(r=>r.Label.text=="나무 판자").Button);await Tap(l.Max);await Tap(l.Transfer);Check(c.InventoryPanel.CountFor(a.Participants[0],"wood")==2&&l.State(1).Loot["wood"]==0,"Take wood");await Leave(l);await Tap(a.Return);await Tap(a.ReturnConfirm);await Tap(c.ReturnPanel.StoreAll);Check(c.InventoryPanel.StockCount("wood")==4,"Wood not stocked");await Tap(c.ReturnPanel.Back);
  Check(a.Begin(c.Campaign.Party.ToArray(),c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")),"Revisit");await Task.Delay(950);await Ready(a);a.Objects[1].onClick.Invoke();await Task.Delay(100);Check(!l.IsOpen&&!s.IsOpen,"A left press on the finished empty table opens nothing");Check(FieldPawnTest.Detail(a,1)&&l.IsOpen&&!s.IsOpen&&l.State(1).Loot["wood"]==0,"Wood regenerated");Check(!l.Advance(1,2,a.Participants[0]),"Completed site rerolled");await Leave(l);await Tap(a.Return);await Tap(a.ReturnConfirm);await Tap(c.ReturnPanel.Back);
  await Tap(c.Workbench);await Tap(c.CraftPanel.Plus);await Tap(c.CraftPanel.CostRows[0].GetComponent<Button>());Check(c.CraftPanel.MaterialGuide.Counts.text.Contains("사용 가능 4 / 필요 4 · 부족 0"),"Craft stock not updated");Check(!c.CraftPanel.MaterialGuide.Action.interactable,"Ready wood still asks expedition");return "PASS the table's search turns (함께: one turn sooner), actual 2-wood acquisition, bag/stock transfer, crafting requirement updated, completed-site revisit cannot reroll. Encounter suppression was temporary test fixture.";
 }
}
