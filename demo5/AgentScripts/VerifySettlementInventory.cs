using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;
public static class VerifySettlementInventory {
 static void Check(bool b,string why){if(!b)throw new Exception(why);}
 static void Click(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);}
 static InventorySlot Row(System.Collections.Generic.IReadOnlyList<InventorySlot> list,string name)=>list.First(x=>x.Label.text==name);
 static void Bounds(GameObject obj){Canvas.ForceUpdateCanvases();foreach(var t in obj.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text overflow "+t.name+": "+t.preferredHeight+"/"+t.rectTransform.rect.height);}
 static async Task Tap(Button b){Click(b);await Task.Delay(70);}
 public static async Task<string> Flow(){var c=Object.FindAnyObjectByType<SettlementController>();var p=c.InventoryPanel;int total=c.Campaign.Supplies;
  await Tap(c.Cabinet);await Task.Delay(150);Check(p.IsOpen&&!c.Main.blocksRaycasts&&p.MemberCards.Count(m=>m.gameObject.activeSelf)==2,"Inventory entry / modal / party");Check(p.StockCount("supplies")==total&&p.BagCount(0,"supplies")==0,"Wrong initial ownership");Bounds(p.View);
  await Tap(Row(p.StockRows,"보급품").Button);await Tap(p.ToBag);Check(p.QuantityPopup.activeSelf&&!p.Workspace.blocksRaycasts,"Nested quantity gate");Bounds(p.QuantityPopup);await Tap(p.Cancel);Check(p.StockCount("supplies")==total,"Cancel moved items");await Tap(p.Move);await Tap(p.Confirm);Check(p.BagCount(0,"supplies")==1&&p.StockCount("supplies")==total-1&&c.Campaign.Supplies==total,"Transfer conservation");
  await Tap(p.MemberCards[1].Button);Check(p.MemberIndex==1&&p.BagCount(1,"supplies")==0,"Bags shared accidentally");await Tap(p.MemberCards[0].Button);Check(p.BagCount(0,"supplies")==1,"Member switch lost bag");
  foreach(var name in new[]{"탄약","나무 판자"}){await Tap(Row(p.StockRows,name).Button);await Tap(p.Move);await Tap(p.Confirm);}Check(p.UsedSlots(0)==3,"Expected three occupied slots");await Tap(Row(p.StockRows,"밧줄").Button);Check(!p.Move.interactable&&!p.ToBag.interactable,"Full bag accepted new stack");await Tap(Row(p.StockRows,"보급품").Button);Check(p.Move.interactable,"Full bag rejected existing stack");await Tap(p.Move);await Tap(p.Max);await Tap(p.Confirm);Check(p.StockCount("supplies")==0&&p.BagCount(0,"supplies")==total,"Max transfer lost stock");
  await Tap(Row(p.BagRows,"나무 판자").Button);await Tap(p.ToStock);await Tap(p.Confirm);Check(p.StockCount("wood")==2&&p.BagCount(0,"wood")==0,"Material return failed");
  await Tap(p.Tabs[2]);Check(p.StockRows.All(r=>r.Label.text=="탄약"),"Category filter");await Tap(p.Tabs[0]);Bounds(p.View);await Tap(p.CloseButton);Check(c.Main.blocksRaycasts&&!p.IsOpen,"Close did not restore input");
  await Tap(c.Members[1].BagButton);Check(p.MemberIndex==1&&p.BagCount(1,"supplies")==0,"Direct member bag route");await Tap(p.CloseButton);
  await Tap(c.Workbench);await Task.Delay(100);var craft=c.CraftPanel;await Tap(craft.RecipeRows[1].Button);await Tap(craft.WorkerRows[0].Button);await Tap(craft.Confirm);await Tap(c.Stock);await Tap(p.MemberCards[1].Button);await Task.Delay(80);Check(p.StockCount("scrap")==2,"Reserved materials available");await Tap(Row(p.StockRows,"고철").Button);await Tap(p.Move);await Tap(p.Max);await Tap(p.Confirm);Check(craft.Available("scrap")==0&&p.BagCount(1,"scrap")==2,"Reservation stolen during transfer");await Tap(p.CloseButton);
  await Tap(c.Workbench);await Task.Delay(100);await Tap(craft.OrderRows[0].Cancel);await Tap(craft.CancelYes);Check(craft.Available("scrap")==1,"Cancel refund lost or duplicated");await Tap(craft.CloseButton);await Tap(c.Members[1].BagButton);await Tap(Row(p.BagRows,"고철").Button);await Tap(p.Move);await Tap(p.Max);await Tap(p.Confirm);Check(craft.Available("scrap")==3,"Material conservation after refund/return");await Tap(p.CloseButton);
  await Tap(c.Members[0].BagButton);foreach(var name in new[]{"보급품","탄약"}){await Tap(Row(p.BagRows,name).Button);await Tap(p.Move);await Tap(p.Max);await Tap(p.Confirm);}Check(p.StockCount("supplies")==total&&p.StockCount("ammo")==c.Campaign.Ammo&&p.UsedSlots(0)==0,"Round-trip conservation");await Tap(p.CloseButton);
  return "PASS: modal/raycast routes, real party, individual ownership, quantity cancellation/partial/max, capacity/new-stack gating, category filter, material reservation exclusion/refund, round-trip conservation, text bounds.";
 }
 public static async Task<string> Preview(){var c=Object.FindAnyObjectByType<SettlementController>();var p=c.InventoryPanel;await Tap(c.Cabinet);await Task.Delay(120);foreach(var name in new[]{"보급품","탄약"}){await Tap(Row(p.StockRows,name).Button);await Tap(p.Move);await Tap(p.Confirm);}await Tap(Row(p.StockRows,"나무 판자").Button);Bounds(p.View);return "Preview: actual stock transferred to selected member; no extra items granted.";}
}
