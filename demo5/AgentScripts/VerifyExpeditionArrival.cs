using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;
public static class VerifyExpeditionArrival {
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(80);}
 static void Bounds(GameObject root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text overflow "+t.name+": "+t.preferredHeight+" / "+t.rectTransform.rect.height);}


 public static async Task<string> Flow(){
 var c=Object.FindAnyObjectByType<SettlementController>();await Tap(c.Exit);await Tap(c.ExpeditionPanel.Cards[1].Button);await Tap(c.ExpeditionPanel.Pack);var pack=c.PackingPanel;var person=pack.Current;var inv=c.InventoryPanel;await Tap(pack.StockRows.First(x=>x.Label.text==inv.Items.First(i=>i.Id=="supplies").Name).Button);await Tap(pack.ToBag);int start=c.Campaign.MinuteOfDay;int supplies=c.Campaign.Supplies;await Tap(pack.Ready);Bounds(pack.Review);await Tap(pack.ReviewBack);Check(c.Campaign.MinuteOfDay==start,"Cancel spent travel time");await Tap(pack.Ready);await Tap(pack.Depart);pack.Depart.onClick.Invoke();await Task.Delay(950);
 var a=c.ArrivalPanel;Check(a.IsOpen&&!pack.IsOpen&&!a.InTransit&&c.Campaign.IsFieldExpedition,"Arrival failed");Check(c.Campaign.MinuteOfDay==start+20,"Travel charged twice");Check(a.Participants.Count==1&&a.Participants[0]==person&&a.Cards[0].Name.text==person.Name,"Wrong selected party");Check(inv.CountFor(person,"supplies")==1&&c.Campaign.Supplies==supplies,"Lost inventory");Check(!inv.MoveFor(person,"supplies",1,false),"Remote stock accessible");inv.Open();Check(!inv.IsOpen,"Remote stock opened");Bounds(a.View);
 await Tap(a.Cards[0].Button);Check(a.PopupBody.text.Contains("보급품")&&!a.Main.blocksRaycasts,"Field bag/modal isolation");await Tap(a.PopupBack);foreach(var b in a.Objects){await Tap(b);Check(a.Popup.activeSelf,"Object did not open");Bounds(a.Popup);await Tap(a.PopupBack);}Check(c.Campaign.MinuteOfDay==start+20,"Inspect spent time");
 await Tap(a.Return);await Tap(a.PopupBack);Check(c.Campaign.IsFieldExpedition,"Return cancelled but mutated");await Tap(a.Return);await Tap(a.ReturnConfirm);a.ReturnConfirm.onClick.Invoke();Check(!a.IsOpen&&c.Main.gameObject.activeSelf&&!c.Campaign.IsFieldExpedition&&c.Campaign.MinuteOfDay==start+40,"Return state/time");Check(inv.CountFor(person,"supplies")==1,"Return lost bag");Check(inv.MoveFor(person,"supplies",1,false),"Stock not restored");
 return "PASS: departure cancel/confirm/double click, single travel charge each way, selected-party identity, carried inventory, remote stock lock, objects/modal/bag, return cancel/confirm, text bounds.";
 }
 public static async Task<string> Preview(){var c=Object.FindAnyObjectByType<SettlementController>();await Tap(c.Exit);foreach(var card in c.ExpeditionPanel.Cards)if(card.Button.interactable&&!c.ExpeditionPanel.Selected.Contains(c.Campaign.Party.ElementAt(c.ExpeditionPanel.Cards.ToList().IndexOf(card))))await Tap(card.Button);await Tap(c.ExpeditionPanel.Pack);await Tap(c.PackingPanel.Ready);await Tap(c.PackingPanel.Depart);await Task.Delay(950);return "Arrival preview ready.";}
}
