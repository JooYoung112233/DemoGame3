using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;
public static class VerifyLoot {
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(80);}
 static void Bounds(GameObject root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text overflow "+t.name+": "+t.preferredHeight+" / "+t.rectTransform.rect.height);}




 static async Task<SettlementController> Depart(){var c=Object.FindAnyObjectByType<SettlementController>();await Tap(c.Exit);foreach(var card in c.ExpeditionPanel.Cards)if(!card.Check.gameObject.activeSelf)await Tap(card.Button);await Tap(c.ExpeditionPanel.Pack);await Tap(c.PackingPanel.Ready);await Tap(c.PackingPanel.Depart);await Task.Delay(950);return c;}
 static async Task Tick(ExpeditionSearchPanel s){await Tap(s.Choose);await Tap(s.Confirm);s.Confirm.onClick.Invoke();}
 static async Task Select(ExpeditionLootPanel l,string name,bool bag=false){await Tap((bag?l.BagRows:l.FieldRows).First(r=>r.Label.text==name).Button);}
 public static async Task<string> Flow(){
 var c=await Depart();var a=c.ArrivalPanel;var s=a.Search;var l=a.Loot;int minute=c.Campaign.MinuteOfDay,stock=c.InventoryPanel.StockCount("supplies"),total=c.Campaign.Supplies;
 int savedMaximum=a.Encounter.MaximumChance;a.Encounter.MaximumChance=0;int[] chances=l.Sites[0].Drops.Select(d=>d.Chance).ToArray();foreach(var d in l.Sites[0].Drops)d.Chance=100;
 try{
 await Tap(a.Objects[0]);await Tap(s.Cards[0].Button);await Tap(s.Paces[1]);await Tap(s.Duties[0]);await Tap(s.Choose);await Tap(s.Cancel);Check(a.Rooms.Turns==0,"Cancel charged turn");await Tick(s);Check(a.Rooms.Turns==1&&!l.State(0).Complete&&l.State(0).Loot.Count==0,"Early loot/duplicate turn");Check(!s.Paces[0].interactable,"Pace change exploit");await Tap(s.Back);
 await Tap(a.Objects[3]);await Tap(a.ReturnConfirm);await Task.Delay(2400);await Tap(a.Rooms.CorridorBack);await Tap(a.ReturnConfirm);await Task.Delay(2400);await Tap(a.Objects[0]);Check(l.State(0).Progress==1,"Interrupted progress lost");await Tick(s);Check(l.IsOpen&&l.State(0).Complete&&a.Rooms.Turns==4&&l.FieldRows.Count==4,"Search completion");Check(c.Campaign.MinuteOfDay==minute,"Invented minute conversion");Bounds(l.View);
 await Select(l,"보급품");await Tap(l.Max);await Tap(l.Transfer);Check(c.InventoryPanel.CountFor(l.Current,"supplies")==2&&c.Campaign.Supplies==total+2&&c.InventoryPanel.StockCount("supplies")==stock,"Resource ownership accounting");
 await Select(l,"고철");await Tap(l.Transfer);await Select(l,"천 조각");await Tap(l.Transfer);Check(c.InventoryPanel.SlotsFor(l.Current)==3,"Bag slots");await Select(l,"밧줄");Check(!l.Transfer.interactable&&l.Message.text.Contains("부족"),"Overfull accepted");
 await Select(l,"고철");Check(l.Transfer.interactable,"Stack blocked in full bag");await Tap(l.Transfer);
 await Select(l,"보급품",true);await Tap(l.Max);await Tap(l.Transfer);Check(c.Campaign.Supplies==total&&c.InventoryPanel.StockCount("supplies")==stock,"Field drop duplicated stock");await Select(l,"보급품");await Tap(l.Max);await Tap(l.Transfer);Check(c.Campaign.Supplies==total+2,"Reacquire duplicated");
 await Select(l,"밧줄");await Tap(l.Cards[1].Button);await Tap(l.Transfer);Check(c.InventoryPanel.CountFor(a.Participants[1],"rope")==1,"Other member allocation failed");
 await Tap(l.Back);Check(l.LeaveReview.activeSelf,"Unclaimed loot not confirmed");Bounds(l.LeaveReview);await Tap(l.LeaveCancel);Check(l.IsOpen,"Cancel closed loot");await Tap(l.Back);await Tap(l.LeaveConfirm);int remaining=l.State(0).Loot.Values.Sum();await Tap(a.Objects[0]);Check(l.State(0).Loot.Values.Sum()==remaining&&a.Rooms.Turns==4,"Repeat loot rerolled");Check(!l.Advance(0,1,a.Participants[0]),"Completed search repeat");await Tap(l.Back);await Tap(l.LeaveConfirm);
 await Tap(a.Return);await Tap(a.ReturnConfirm);Check(c.Campaign.Supplies==total+2&&c.InventoryPanel.StockCount("supplies")==stock,"Return lost inventory");Check(c.SupplyCount.text==(total+2).ToString(),"Return HUD stale");
 await Tap(c.Exit);foreach(var card in c.ExpeditionPanel.Cards)if(!card.Check.gameObject.activeSelf)await Tap(card.Button);await Tap(c.ExpeditionPanel.Pack);await Tap(c.PackingPanel.Ready);await Tap(c.PackingPanel.Depart);await Task.Delay(950);await Tap(a.Objects[0]);Check(l.IsOpen&&l.State(0).Loot.Values.Sum()==remaining,"New visit reset finite loot");
 return "PASS: cancel/one turn/double confirm, interruption & room return, fixed pace, no early loot, finite reward, capacity/stack/quantity, member allocation, field drop/reacquire totals, unclaimed close confirmation, return & revisit persistence, text bounds. Deterministic drop fixture restored.";
 }finally{a.Encounter.MaximumChance=savedMaximum;for(int i=0;i<chances.Length;i++)l.Sites[0].Drops[i].Chance=chances[i];}
 }

 public static async Task<string> Edges(){var c=Object.FindAnyObjectByType<SettlementController>();var a=c.ArrivalPanel;var l=a.Loot;await Tap(l.Back);await Tap(l.LeaveConfirm);int[] old=l.Sites[1].Drops.Select(d=>d.Chance).ToArray();foreach(var d in l.Sites[1].Drops)d.Chance=0;
 try{await Tap(a.Objects[1]);await Tap(a.Search.Cards[0].Button);await Tap(a.Search.Paces[1]);await Tap(a.Search.Duties[1]);await Tick(a.Search);await Tick(a.Search);Check(l.Empty.gameObject.activeSelf&&l.FieldRows.Count==0,"Empty outcome missing");Check(a.Rooms.Noise==2,"Watch noise not applied");
 foreach(var item in c.InventoryPanel.Items)l.State(1).Loot[item.Id]=1;l.Rebuild();await Task.Delay(100);var sc=l.FieldContent.GetComponentInParent<ScrollRect>();var hint=sc.viewport.GetComponentInChildren<ScrollMoreIndicator>();Canvas.ForceUpdateCanvases();hint.Refresh();Check(hint.HasMoreBelow,"Overflow hint missing");sc.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();hint.Refresh();Check(!hint.HasMoreBelow,"Hint remains at bottom");
 l.State(1).Loot.Clear();l.Rebuild();await Tap(l.Back);Check(!l.IsOpen&&!l.LeaveReview.activeSelf,"Empty close asked warning");Check(!c.InventoryPanel.TransferField(a.Participants[0],"invalid",1,true),"Unknown item accepted");return "PASS: empty search, lookout noise, scrolling indicator at top/bottom, empty close, invalid item rejection.";
 }finally{for(int i=0;i<old.Length;i++)l.Sites[1].Drops[i].Chance=old[i];}}
 public static async Task<string> Preview(){var c=await Depart();var a=c.ArrivalPanel;await Tap(a.Objects[0]);await Tap(a.Search.Cards[0].Button);await Tap(a.Search.Duties[0]);await Tick(a.Search);await Tick(a.Search);var l=a.Loot;if(l.FieldRows.Count>0)await Tap(l.FieldRows[0].Button);Bounds(l.View);EventSystem.current.SetSelectedGameObject(null);return "Actual default-rate search complete; loot preview open.";}
}



