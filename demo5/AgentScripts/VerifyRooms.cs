using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;
public static class VerifyRooms {
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();b.GetComponentInParent<Canvas>().worldCamera?.Render();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(80);}
 static void Bounds(GameObject root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text overflow "+t.name+": "+t.preferredHeight+" / "+t.rectTransform.rect.height);}



 public static async Task<string> Flow(){
 var c=Object.FindAnyObjectByType<SettlementController>();await Tap(c.Exit);await Tap(c.ExpeditionPanel.Cards[0].Button);await Tap(c.ExpeditionPanel.Cards[1].Button);await Tap(c.ExpeditionPanel.Pack);
 var p=c.PackingPanel;await Tap(p.StockRows.First(x=>x.Label.text==c.InventoryPanel.Items.First(i=>i.Id=="supplies").Name).Button);await Tap(p.ToBag);var person=p.Current;await Tap(p.Ready);await Tap(p.Depart);await Task.Delay(950);
 var a=c.ArrivalPanel;var n=a.Rooms;int minute=c.Campaign.MinuteOfDay;await Tap(a.Objects[0]);if(a.Search&&a.Search.IsOpen)await Tap(a.Search.Back);else await Tap(a.PopupBack);Check(n.Inspected.Contains(0),"Inspection state missing");await Tap(a.Objects[3]);Check(n.CurrentRoom==0&&!n.CorridorVisited&&n.Turns==0,"Preview revealed room or spent turn");await Tap(a.PopupBack);Check(n.Turns==0,"Cancel charged");
 await Tap(a.Objects[3]);await Tap(a.ReturnConfirm);a.ReturnConfirm.onClick.Invoke();Check(a.InTransit&&!a.Main.blocksRaycasts&&n.Turns==1,"Move lock / duplicate charge");await Task.Delay(2400);
 Check(n.CurrentRoom==1&&n.CorridorVisited&&!a.Return.interactable&&n.CorridorHotspots.activeSelf&&!a.InTransit,"Corridor state");Bounds(a.View);Check(c.InventoryPanel.CountFor(person,"supplies")==1&&a.Participants.Count==2,"Lost bags/party");await Tap(n.LockedDoor);Check(!a.ReturnConfirm.gameObject.activeSelf,"Locked door enterable");await Tap(a.PopupBack);
 await Tap(n.CorridorBack);await Tap(a.ReturnConfirm);await Task.Delay(2400);Check(n.CurrentRoom==0&&n.Turns==2&&n.Inspected.Contains(0)&&a.Return.interactable,"Return to visited room failed");Check(c.Campaign.MinuteOfDay==minute+2*n.MinutesPerTurn,"Room travel time must advance once per move");Bounds(a.View);
 await Tap(a.Objects[3]);await Tap(a.ReturnConfirm);await Task.Delay(2400);return "PASS: hidden room before entry, cancel costs zero, one turn per group move, double-click guard, input lock, corridor/return, locked door, preserved bag/party/inspection, text bounds. Preview left in corridor.";
 }
}

