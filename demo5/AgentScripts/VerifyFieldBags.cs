using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
// 말 놓기 (2026-09-25): whose pawn may stand at a locked object follows the tool in their own bag (the place is greyed out otherwise).
public static class VerifyFieldBags {
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();b.GetComponentInParent<Canvas>().worldCamera?.Render();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(100);}
 public static async Task<string> ToolFlow(){
  FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c=Object.FindAnyObjectByType<SettlementController>();var a=c.ArrivalPanel;var bag=a.FieldBags;var inv=c.InventoryPanel;var people=c.Campaign.Party.ToArray();
  if(a.Search.IsOpen)await Tap(a.Search.Back);FieldPawnTest.Clear(a);await Tap(a.Cards[0].Button);await Tap(bag.LeftRows.First(r=>r.Label.text==inv.Items.First(i=>i.Id=="prybar").Name).Button);await Tap(bag.Transfer);
  Check(inv.CountFor(people[0],"prybar")==0&&inv.CountFor(people[1],"prybar")==1,"Tool did not move");
  await Tap(bag.Back);for(int i=0;i<60&&!FieldPawnTest.Ready(a);i++)await Task.Delay(50);
  Check(FieldPawnTest.Option(a,0,FieldPawnTest.SearchKey(2),FieldSpotKind.Lead)?.Enabled==false,"Previous holder retained access");Check(FieldPawnTest.Option(a,1,FieldPawnTest.SearchKey(2),FieldSpotKind.Lead)?.Enabled==true,"Recipient did not gain access");
  Check(FieldPawnTest.Detail(a,2)&&!inv.ExchangeField(people[1],people[0],"prybar",1),"Exchange during search accepted");await Tap(a.Search.Back);await Tap(a.Cards[1].Button);await Tap(bag.LeftRows.First(r=>r.Label.text==inv.Items.First(i=>i.Id=="prybar").Name).Button);
  return "PASS: UI tool handoff changes whose pawn may search the locked object; exchange blocked while its 07 window is open; ready tool preview.";
 }
 public static async Task<string> Flow(){
  FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c=Object.FindAnyObjectByType<SettlementController>();var a=c.ArrivalPanel;var bag=a.FieldBags;var inv=c.InventoryPanel;var people=c.Campaign.Party.ToArray();
  Check(inv.MoveFor(people[0],"supplies",2,true)&&inv.MoveFor(people[0],"scrap",1,true)&&inv.MoveFor(people[1],"ammo",2,true),"Packing");
  Check(inv.MoveFor(people[1],"cloth",1,true),"Fill recipient bag");Check(a.Begin(people,c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")),"Departure");await Task.Delay(850);await Tap(a.Cards[0].Button);Check(bag.IsOpen&&!a.Main.blocksRaycasts,"Modal gate");
  int minute=c.Campaign.MinuteOfDay,turn=a.Rooms.Turns,noise=a.Rooms.Noise,supply=c.Campaign.Supplies,ammo=c.Campaign.Ammo,scrapStock=inv.StockCount("scrap");
  await Tap(bag.LeftRows[0].Button);await Tap(bag.Max);await Tap(bag.Transfer);Check(inv.CountFor(people[0],"supplies")==0&&inv.CountFor(people[1],"supplies")==2,"Full stack transfer");
  await Tap(bag.RightRows.First(r=>r.Label.text==inv.Items.First(i=>i.Id=="supplies").Name).Button);await Tap(bag.Transfer);Check(inv.CountFor(people[0],"supplies")==1&&inv.CountFor(people[1],"supplies")==1,"Reverse partial transfer");
  Check(inv.SlotsFor(people[1])==people[1].BagCapacity,"Recipient must be full");{
   Check(!inv.ExchangeField(people[0],people[1],"scrap",1),"Full bag accepted new type");Check(inv.ExchangeField(people[0],people[1],"supplies",1),"Full bag refused existing stack");
  }
  Check(!inv.ExchangeField(people[0],people[0],"scrap",1)&&!inv.ExchangeField(people[0],people[1],"scrap",0)&&!inv.ExchangeField(people[0],people[1],"scrap",2),"Invalid exchange accepted");
  Check(c.Campaign.Supplies==supply&&c.Campaign.Ammo==ammo&&inv.StockCount("scrap")==scrapStock&&c.Campaign.MinuteOfDay==minute&&a.Rooms.Turns==turn&&a.Rooms.Noise==noise,"Exchange changed global totals/time/stock");
  bag.Refresh();await Tap(bag.LeftRows[0].Button);Canvas.ForceUpdateCanvases();foreach(var text in bag.GetComponentsInChildren<Text>())Check(text.preferredHeight<=text.rectTransform.rect.height+1,"Text clipped: "+text.name+" "+text.text);
  Check(((RectTransform)bag.Back.transform).rect.size==((RectTransform)bag.Transfer.transform).rect.size,"Footer mismatch");
  await Tap(bag.Back);Check(a.Main.blocksRaycasts&&!bag.IsOpen,"Close gate");await Tap(a.Return);await Tap(a.ReturnConfirm);Check(!inv.ExchangeField(people[1],people[0],"supplies",1),"Settlement exchange accepted");await Tap(c.ReturnPanel.Back);
  Check(a.Begin(new[]{people[0]},c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")),"Solo departure");await Task.Delay(850);await Tap(a.Cards[0].Button);Check(bag.RightRows.Count==0&&!bag.Transfer.interactable,"Solo exchange enabled");await Tap(bag.Back);await Tap(a.Return);await Tap(a.ReturnConfirm);await Tap(c.ReturnPanel.Back);
  Check(a.Begin(people,c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")),"Preview departure");await Task.Delay(850);await Tap(a.Cards[0].Button);await Tap(bag.LeftRows[0].Button);
  return "PASS: real UI forward/all and reverse/partial exchange, full bag rejection and existing-stack allowance, self/zero/overdraw rejection, no stock/resource/time/noise change, modal/close, solo party and return/revisit; ready screenshot.";
 }
}
