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
 // 말 놓기 (2026-09-25): a left press on an object or door with no pawn held asks for a pawn (no window, no time); a right press opens
 // the read-only 07 window (Inspect) or the door's log; the move is every pawn at the door, then '턴 진행' (a double press = one turn).
 static async Task Ready(ExpeditionArrivalPanel a){for(int i=0;i<60&&!FieldPawnTest.Ready(a);i++)await Task.Delay(50);}
 static async Task Move(ExpeditionArrivalPanel a,int next,bool twice=false){await Ready(a);Check(FieldPawnTest.Gather(a,next),"Everyone at the door to "+next+": "+FieldPawnTest.Describe(a));var turn=a.Threat.Planner.TurnButton;await Tap(turn);if(twice)turn.onClick.Invoke();}
 static void Bounds(GameObject root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text overflow "+t.name+": "+t.preferredHeight+" / "+t.rectTransform.rect.height);}



 public static async Task<string> Flow(){
 FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c=Object.FindAnyObjectByType<SettlementController>();await Tap(c.Exit);await Tap(c.ExpeditionPanel.Cards[0].Button);await Tap(c.ExpeditionPanel.Cards[1].Button);await Tap(c.ExpeditionPanel.Pack);
 var p=c.PackingPanel;await Tap(p.StockRows.First(x=>x.Label.text==c.InventoryPanel.Items.First(i=>i.Id=="supplies").Name).Button);await Tap(p.ToBag);var person=p.Current;await Tap(p.Ready);await Tap(p.Depart);await Task.Delay(950);
 var a=c.ArrivalPanel;var n=a.Rooms;var b=FieldPawnTest.Board(a);int minute=c.Campaign.MinuteOfDay;await Ready(a);
 a.Objects[0].onClick.Invoke();await Task.Delay(100);Check(!a.Search.IsOpen&&!a.Popup.activeSelf&&n.Turns==0&&b&&b.LastHint==b.Texts.PickFirst,"Left press without a pawn: no window, no time");
 Check(FieldPawnTest.Detail(a,0)&&a.Search.IsOpen&&a.Search.ReadOnly,"Right press: the read-only 07 window");await Tap(a.Search.Back);Check(n.Inspected.Contains(0),"Inspection state missing");
 b.ShowDoorLog(1);await Task.Delay(100);Check(a.Popup.activeSelf&&!a.ReturnConfirm.gameObject.activeSelf&&n.CurrentRoom==0&&!n.CorridorVisited&&n.Turns==0,"Door log revealed room or spent turn");await Tap(a.PopupBack);Check(n.Turns==0,"Cancel charged");
 await Move(a,1,true);Check(a.InTransit&&!a.Main.blocksRaycasts&&n.Turns==1,"Move lock / duplicate charge");await Task.Delay(2400);
 Check(n.CurrentRoom==1&&n.CorridorVisited&&!a.Return.interactable&&n.CorridorHotspots.activeSelf&&!a.InTransit,"Corridor state");Bounds(a.View);Check(c.InventoryPanel.CountFor(person,"supplies")==1&&a.Participants.Count==2,"Lost bags/party");await Ready(a);{var locked=FieldPawnTest.Option(a,0,FieldPawnTest.DoorKey(2),FieldSpotKind.Gather);Check(locked!=null&&!locked.Enabled&&locked.Blocked.Contains(n.UnlockToolName)&&!FieldPawnTest.Place(a,0,locked.Key,FieldSpotKind.Gather),"Locked door enterable");}
 await Move(a,0);await Task.Delay(2400);Check(n.CurrentRoom==0&&n.Turns==2&&n.Inspected.Contains(0)&&a.Return.interactable,"Return to visited room failed");Check(c.Campaign.MinuteOfDay==minute+2*n.MinutesPerTurn,"Room travel time must advance once per move");Bounds(a.View);
 await Move(a,1);await Task.Delay(2400);return "PASS: hidden room before entry, left press asks for a pawn, right press = read-only 07 / door log (zero cost), one turn per group move (everyone at the door), double-press guard, input lock, corridor/return, locked door greyed without the prybar, preserved bag/party/inspection, text bounds. Preview left in corridor.";
 }
}

