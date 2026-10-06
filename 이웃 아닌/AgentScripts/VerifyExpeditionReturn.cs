using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class VerifyExpeditionReturn {
 static void Check(bool ok,string msg){if(!ok)throw new Exception(msg);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();b.GetComponentInParent<Canvas>().worldCamera?.Render();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(60);}
 public static async Task<string> Preview(){
  var c=Object.FindAnyObjectByType<SettlementController>();var inv=c.InventoryPanel;var party=c.Campaign.Party.ToArray();
  Check(inv.MoveFor(party[0],"ammo",2,true)&&inv.MoveFor(party[1],"cloth",1,true),"Packing failed");
  Check(c.ArrivalPanel.Begin(party,c.ExpeditionPanel.Destinations.First(x=>x.Id=="mall")),"Departure failed");await Task.Delay(850);
  // Deterministic field inventory fixture; this test does not assert random drop/battle rules.
  Check(inv.TransferField(party[0],"wood",2,true)&&inv.TransferField(party[0],"ammo",1,false)&&inv.TransferField(party[1],"supplies",1,true),"Field transfers failed");
  party[0].Health--;c.ArrivalPanel.Rooms.SpendSearchTurn(1);
  await Tap(c.ArrivalPanel.Return);await Tap(c.ArrivalPanel.ReturnConfirm);
  Check(c.ReturnPanel.IsOpen&&c.ReturnPanel.Duration.text.Contains("50분"),"Return report/time");
  Check(c.ReturnPanel.Body.text.Contains("탄약  2 → 1")&&c.ReturnPanel.Body.text.Contains("나무 판자  0 → 2"),"Return comparison");
  return "Preview: deterministic acquisition/use/injury fixture, real return buttons.";
 }
 public static async Task<string> Revisit(){
  var c=Object.FindAnyObjectByType<SettlementController>();if(c.ReturnPanel.IsOpen)c.ReturnPanel.Close();var a=c.ArrivalPanel;var party=c.Campaign.Party.ToArray();var place=c.ExpeditionPanel.Destinations.First(x=>x.Id=="mall");
  Check(a.Begin(party,place),"Departure");await Task.Delay(850);a.Rooms.MarkInspected(0);
  int crateTurns=a.Loot.Sites[0].Turns;a.Loot.Sites[0].Turns=3;/* 함께 on a 3-turn fixture: 2 turns, one stays partial */Check(a.Loot.Advance(0,1,party[0]),"Partial search");a.Loot.Sites[0].Turns=crateTurns;var state=a.Loot.State(0);Check(state.Progress==1&&!state.Complete,"Partial progress");
  a.Rooms.AskMove();await Task.Delay(120);await Tap(a.ReturnConfirm);await Task.Delay(2200);Check(a.Rooms.CurrentRoom==1,"Corridor arrival");a.Rooms.AskMove();await Task.Delay(120);await Tap(a.ReturnConfirm);await Task.Delay(2200);
  await Tap(a.Return);await Tap(a.ReturnConfirm);await Tap(c.ReturnPanel.Back);
  Check(a.Begin(party,place),"Revisit departure");await Task.Delay(850);
  Check(a.Rooms.Turns==0&&a.Rooms.CorridorVisited&&a.Rooms.Inspected.Contains(0),"Visit knowledge reset");
  Check(ReferenceEquals(state,a.Loot.State(0))&&a.Loot.State(0).Progress==1,"Partial search reset");
  a.Loot.Advance(0,2,party[0]);a.Loot.Advance(0,2,party[0]);var remaining=string.Join(";",state.Loot.Select(x=>x.Key+":"+x.Value));
  Check(state.Complete&&!a.Loot.Advance(0,0,party[0]),"Completed search rerolled");
  await Tap(a.Return);await Tap(a.ReturnConfirm);await Tap(c.ReturnPanel.Back);Check(a.Begin(party,place),"Third visit");await Task.Delay(850);
  Check(a.Loot.State(0).Complete&&remaining==string.Join(";",a.Loot.State(0).Loot.Select(x=>x.Key+":"+x.Value)),"Remaining loot changed on revisit");
  await Tap(a.Return);await Tap(a.ReturnConfirm);return "PASS: actual two-way room move, remembered corridor/inspection, per-trip turns reset, partial progress retained, completed loot cannot reroll, remaining loot retained across returns.";
 }
 public static string Layout(){
  var p=Object.FindAnyObjectByType<SettlementController>().ReturnPanel;Canvas.ForceUpdateCanvases();
  var members=p.MemberContent.GetComponentsInChildren<ReturnMemberRow>();var items=p.ItemContent.GetComponentsInChildren<ReturnItemRow>();
  Check(members.Length==2&&items.Length==4,"Wrong visual rows");
  Check(members[0].Health.text=="3 → 2 / 3"&&members[0].Portrait.sprite!=null,"Missing portrait/health");
  foreach(var t in p.View.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text clipped: "+t.name+" "+t.preferredHeight+"/"+t.rectTransform.rect.height);
  foreach(var row in items)Check(row.Icon.sprite!=null&&Math.Abs(row.Before.transform.position.y-row.After.transform.position.y)<.01f&&Math.Abs(row.After.transform.position.y-row.Change.transform.position.y)<.01f,"Item column baseline");
  return "PASS: two portrait/health cards, four icon rows, aligned numeric columns, all visible text fits.";
 }
 public static async Task<string> Flow(){
  var c=Object.FindAnyObjectByType<SettlementController>();var panel=c.ReturnPanel;var inv=c.InventoryPanel;var people=c.Campaign.Party.ToArray();
  int ammo=c.Campaign.Ammo,supplies=c.Campaign.Supplies,minute=c.Campaign.MinuteOfDay;string report=panel.Body.text;
  await Tap(panel.InspectBags);Check(inv.IsOpen&&inv.MemberIndex==0,"Bag shortcut");await Tap(inv.CloseButton);await Tap(c.Journal);Check(panel.IsOpen&&panel.Body.text==report,"Report reopen changed snapshot");
  await Tap(panel.StoreAll);Check(people.All(p=>inv.SlotsFor(p)==0)&&!panel.StoreAll.interactable,"Unpack did not empty all bags");
  Check(inv.StockCount("wood")==4&&inv.StockCount("cloth")==2,"Material credit incorrect");Check(c.Campaign.Ammo==ammo&&c.Campaign.Supplies==supplies,"Shared resources duplicated");
  panel.StoreAll.onClick.Invoke();await Tap(panel.Back);await Tap(c.Journal);Check(panel.Body.text==report&&inv.StockCount("wood")==4&&c.Campaign.MinuteOfDay==minute,"Reopen repeated rewards/time");
  await Tap(panel.Back);Check(c.ArrivalPanel.Begin(people,c.ExpeditionPanel.Destinations.First(x=>x.Id=="mall")),"Second departure");await Task.Delay(850);await Tap(c.ArrivalPanel.Return);await Tap(c.ArrivalPanel.ReturnConfirm);Check(panel.Body.text.Contains("휴대품 없음")&&!panel.StoreAll.interactable&&panel.Duration.text.Contains("40분"),"Empty return/reset report");
  return "PASS: elapsed travel+search time, bag shortcut, frozen report, all-party material unload, no duplicate supplies/ammo/rewards/time, empty second trip.";
 }
}

