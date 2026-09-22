using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;
public static class VerifyExpeditionPacking {
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(80);}
 static void Bounds(GameObject root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text overflow "+t.name+": "+t.preferredHeight+" / "+t.rectTransform.rect.height);}

 public static async Task<string> Flow(){var c=Object.FindAnyObjectByType<SettlementController>();await Tap(c.Exit);await Tap(c.ExpeditionPanel.Cards[0].Button);await Tap(c.ExpeditionPanel.Cards[1].Button);await Tap(c.ExpeditionPanel.Pack);var p=c.PackingPanel;var inv=c.InventoryPanel;var first=p.Current;int stock=inv.StockCount("supplies");Bounds(p.View);await Tap(p.StockRows.First(x=>x.Label.text==inv.Items.First(i=>i.Id=="supplies").Name).Button);await Tap(p.Plus);await Tap(p.ToBag);Check(inv.CountFor(first,"supplies")==2&&inv.StockCount("supplies")==stock-2,"Quantity allocation");await Tap(p.Cards[1].Button);Check(inv.CountFor(p.Current,"supplies")==0,"Ownership leaked");await Tap(p.Cards[0].Button);await Tap(p.BagRows.First(x=>x.Label.text==inv.Items.First(i=>i.Id=="supplies").Name).Button);await Tap(p.ToStock);Check(inv.CountFor(first,"supplies")==1,"Partial return");
 await Tap(p.StockRows.First(x=>x.Label.text==inv.Items.First(i=>i.Id=="ammo").Name).Button);await Tap(p.ToBag);await Tap(p.StockRows.First(x=>x.Label.text=="나무 판자").Button);await Tap(p.ToBag);Check(inv.SlotsFor(first)==first.BagCapacity,"Expected full bag");await Tap(p.StockRows.First(x=>x.Label.text==inv.Items.First(i=>i.Id=="rope").Name).Button);Check(!p.ToBag.interactable,"Full bag accepted new kind");await Tap(p.Ready);Check(p.Review.activeSelf&&!p.Workspace.blocksRaycasts&&p.Summary.text.Contains("폐상가"),"Review modal");Bounds(p.Review);await Tap(p.ReviewBack);await Tap(p.Back);Check(c.ExpeditionPanel.IsOpen&&c.ExpeditionPanel.Selected.Count==2,"Back lost draft");await Tap(c.ExpeditionPanel.Pack);Check(inv.CountFor(p.Current,"supplies")==1,"Reopen lost items");await Tap(p.BagRows.First(x=>x.Label.text=="나무 판자").Button);Bounds(p.View);return "PASS: real raycast clicks, partial/max quantity, per-member ownership, full bag gating, review modal, return draft, text bounds."; }
}
