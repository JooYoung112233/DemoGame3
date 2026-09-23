using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Demo5.FrontEnd;
public static class VerifyWoodSalvage {
 static SettlementController C=>UnityEngine.Object.FindAnyObjectByType<SettlementController>();
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var cam=b.GetComponentInParent<Canvas>().worldCamera;cam?.Render();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(cam,r.TransformPoint(r.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(90);}
 public static async Task<string> Preview(){
  var c=C;await Tap(c.Workbench);await Tap(c.CraftPanel.Plus);await Tap(c.CraftPanel.CostRows[0].GetComponent<Button>());Check(c.CraftPanel.MaterialGuide.ActionLabel.text=="원정 계획","Wood source missing");await Tap(c.CraftPanel.MaterialGuide.Action);var p=c.ExpeditionPanel;await Tap(p.Cards[0].Button);await Tap(p.Cards[1].Button);await Tap(p.Pack);await Tap(c.PackingPanel.Ready);await Tap(c.PackingPanel.Depart);await Task.Delay(950);
  var a=c.ArrivalPanel;await Tap(a.Objects[1]);var s=a.Search;await Tap(s.Cards[0].Button);await Tap(s.Paces[2]);await Tap(s.Duties[0]);
  var row=s.DropPreview.Rows.Single(r=>r.Name.text=="나무 판자");Check(row.Quantity.text=="×2"&&row.Chance.text=="100%","Precise cooperative preview");int time=c.Campaign.MinuteOfDay;await Tap(s.Choose);await Tap(s.Cancel);Check(c.Campaign.MinuteOfDay==time&&a.Loot.State(1).Progress==0,"Cancel cost");Canvas.ForceUpdateCanvases();foreach(var t in s.View.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Clipped "+t.name);return "PASS crafting shortage → plan → packing → old table; 2 wood preview, exact modifier, free cancel, text bounds.";
 }
 static async Task Leave(ExpeditionLootPanel l){await Tap(l.Back);if(l.LeaveReview.activeSelf)await Tap(l.LeaveConfirm);}
 public static async Task<string> Complete(){
  var c=C;var a=c.ArrivalPanel;var s=a.Search;var l=a.Loot;var encounter=a.Encounter;int bas=encounter.BaseChance,max=encounter.MaximumChance,threshold=encounter.NoiseThreshold;int time=c.Campaign.MinuteOfDay;
  // Isolate resource-flow verification from random battles; restore encounter settings afterwards.
  try{encounter.BaseChance=encounter.MaximumChance=0;encounter.NoiseThreshold=10000;for(int i=0;i<3;i++){await Tap(s.Choose);await Tap(s.Confirm);}}
  finally{encounter.BaseChance=bas;encounter.MaximumChance=max;encounter.NoiseThreshold=threshold;}
  Check(l.IsOpen&&l.State(1).Complete&&l.State(1).Loot["wood"]==2,"Wood completion");Check(c.Campaign.MinuteOfDay==time+30&&a.Rooms.Turns==3,"Search time");await Tap(l.FieldRows.Single(r=>r.Label.text=="나무 판자").Button);await Tap(l.Max);await Tap(l.Transfer);Check(c.InventoryPanel.CountFor(a.Participants[0],"wood")==2&&l.State(1).Loot["wood"]==0,"Take wood");await Leave(l);await Tap(a.Return);await Tap(a.ReturnConfirm);await Tap(c.ReturnPanel.StoreAll);Check(c.InventoryPanel.StockCount("wood")==4,"Wood not stocked");await Tap(c.ReturnPanel.Back);
  Check(a.Begin(c.Campaign.Party.ToArray(),c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")),"Revisit");await Task.Delay(950);await Tap(a.Objects[1]);Check(l.IsOpen&&!s.IsOpen&&l.State(1).Loot["wood"]==0,"Wood regenerated");Check(!l.Advance(1,2,a.Participants[0]),"Completed site rerolled");await Leave(l);await Tap(a.Return);await Tap(a.ReturnConfirm);await Tap(c.ReturnPanel.Back);
  await Tap(c.Workbench);await Tap(c.CraftPanel.Plus);await Tap(c.CraftPanel.CostRows[0].GetComponent<Button>());Check(c.CraftPanel.MaterialGuide.Counts.text.Contains("사용 가능 4 / 필요 4 · 부족 0"),"Craft stock not updated");Check(!c.CraftPanel.MaterialGuide.Action.interactable,"Ready wood still asks expedition");return "PASS 3-turn/30-minute search, actual 2-wood acquisition, bag/stock transfer, crafting requirement updated, completed-site revisit cannot reroll. Encounter suppression was temporary test fixture.";
 }
}
