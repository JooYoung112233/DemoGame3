using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;
public static class VerifyExpeditionPlan {
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(80);}
 static void Bounds(GameObject root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text overflow "+t.name+": "+t.preferredHeight+" / "+t.rectTransform.rect.height);}
 public static async Task<string> Flow(){var c=Object.FindAnyObjectByType<SettlementController>();var p=c.ExpeditionPanel;int day=c.Campaign.Day,supplies=c.Campaign.Supplies,ammo=c.Campaign.Ammo;await Tap(c.Exit);Check(p.IsOpen&&!c.Main.gameObject.activeSelf&&p.Cards.Count==2&&!p.Pack.interactable,"Initial entry/party/no-selection gate");Bounds(p.View);
  await Tap(p.Markers[2]);Check(p.Current.Id=="store"&&p.Travel.text.Contains("10분"),"Destination info did not switch");await Tap(p.Markers[3]);Check(!p.Pack.interactable&&p.Hint.text.Contains("경로"),"Locked destination allowed");await Tap(p.Markers[0]);Check(!p.Pack.interactable&&p.PlaceName.text==c.Campaign.Home.Name,"Home selection wrong");await Tap(p.Markers[1]);
  p.MaximumParty=1;await Tap(p.Cards[0].Button);Check(p.Selected.Count==1&&!p.Cards[1].Button.interactable,"Party limit ignored");await Tap(p.Cards[0].Button);Check(p.Selected.Count==0&&!p.Pack.interactable,"Deselect failed");p.MaximumParty=6;p.Refresh();await Tap(p.Cards[1].Button);Check(p.Pack.interactable,"Eligible member cannot proceed");await Tap(p.Pack);
  var inv=c.InventoryPanel;var pack=c.PackingPanel;Check(pack.IsOpen&&!p.IsOpen&&pack.Cards.Count==1&&pack.BagTitle.text.StartsWith("의무관"),"Packing party scope/identity failed");var expected=c.Roster.Candidates.First(x=>x.Id=="medic").Portrait;Check(pack.Cards[0].Portrait.sprite==expected,"Filtered party portrait mismatch");await Tap(pack.StockRows.First(x=>x.Label.text=="나무 판자").Button);await Tap(pack.ToBag);await Tap(pack.Back);Check(p.IsOpen&&p.Selected.Count==1&&p.Current.Id=="mall","Draft lost on return from packing");await Tap(p.Back);Check(c.Main.gameObject.activeSelf&&c.Main.blocksRaycasts,"Back did not restore settlement");
  await Tap(c.Members[1].BagButton);Check(inv.BagCount(1,"wood")==1,"Packing lost individual ownership");await Tap(inv.BagRows.First(x=>x.Label.text=="나무 판자").Button);await Tap(inv.Move);await Tap(inv.Confirm);await Tap(inv.CloseButton);Check(!p.IsOpen&&c.Main.gameObject.activeSelf,"Normal bag retained packing callback");
  await Tap(c.Bed);await Tap(c.WorkPanel.Rows[1].Button);await Tap(c.WorkPanel.Confirm);await Tap(c.Exit);Check(!p.Cards[1].Button.interactable&&p.Selected.Count==0,"Reserved member remained selected");await Tap(p.Cards[0].Button);var scout=c.Campaign.Party.First();int health=scout.Health;scout.Health=0;p.Refresh();Check(!p.Cards[0].Button.interactable&&p.Selected.Count==0&&!p.Pack.interactable,"Unavailable member bypassed validation");scout.Health=health;p.Refresh();Bounds(p.View);await Tap(p.Back);
  Check(c.Campaign.Day==day&&c.Campaign.Supplies==supplies&&c.Campaign.Ammo==ammo&&c.Clock.text.EndsWith("09:00"),"Planning spent time/resources");return "PASS: destination switching, locked/home gates, selection limit/toggle, scoped packing identity, ownership round-trip, draft return, reserved/unavailable member exclusion, modal restore, text bounds, no time/resource changes.";
 }
 public static async Task<string> Preview(){var c=Object.FindAnyObjectByType<SettlementController>();await Tap(c.Exit);await Tap(c.ExpeditionPanel.Cards[0].Button);await Tap(c.ExpeditionPanel.Cards[1].Button);Bounds(c.ExpeditionPanel.View);return "Plan preview ready with actual two selected members.";}
}
