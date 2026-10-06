using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;
// 말 놓기 (2026-09-25, 기획/탐험-말놓기-조작-재설계.md): who searches is a member's pawn on the object (FieldPawnTest: the board's own rules);
// the 07 window (a right press) only shows the object and who is placed; the helper's roles are the chips under the second pawn.
public static class VerifySearch {
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 // A window that just opened lays out over a few frames: wait (≤1 s) until the button is up and takes the press.
 static async Task Settle(Button b){for(int w=0;w<20;w++){if(b&&b.IsActive()&&b.IsInteractable()){Canvas.ForceUpdateCanvases();var rr=(RectTransform)b.transform;var ee=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,rr.TransformPoint(rr.rect.center))};var hh=new List<RaycastResult>();EventSystem.current.RaycastAll(ee,hh);if(hh.Count>0&&hh[0].gameObject.GetComponentInParent<Button>()==b)return;}await Task.Delay(50);}}
 static async Task Tap(Button b){await Settle(b);Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(80);}
 static void Bounds(GameObject root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text overflow "+t.name+": "+t.preferredHeight+" / "+t.rectTransform.rect.height);}
 static async Task Ready(ExpeditionArrivalPanel a){for(int i=0;i<60&&!FieldPawnTest.Ready(a);i++)await Task.Delay(50);Check(FieldPawnTest.Ready(a),"Board not ready");}
 public static async Task<string> Flow(){
 FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c=Object.FindAnyObjectByType<SettlementController>();await Tap(c.Exit);await Tap(c.ExpeditionPanel.Cards[0].Button);await Tap(c.ExpeditionPanel.Cards[1].Button);await Tap(c.ExpeditionPanel.Pack);await Tap(c.PackingPanel.Ready);await Tap(c.PackingPanel.Depart);await Task.Delay(950);
 var a=c.ArrivalPanel;var s=a.Search;var pl=a.Threat.Planner;int minute=c.Campaign.MinuteOfDay,turn=a.Rooms.Turns;await Ready(a);
 // The 07 window: read only, nobody placed, the room behind blocked while it is open.
 Check(FieldPawnTest.Detail(a,0)&&s.IsOpen&&s.ReadOnly&&!a.Main.blocksRaycasts&&s.PlacedLine==s.ReadNobody&&!(s.Choose.IsActive()&&s.Choose.IsInteractable()),"Open/read-only gate");Bounds(s.View);await Tap(s.Back);
 // The second member's pawn leads the crate, the first joins: 함께; 망보기 greyed out (silent crate), 조명 greyed out (no flashlight).
 Check(FieldPawnTest.Lead(a,1,0)&&FieldPawnTest.Join(a,0,0),"Two pawns on the crate: "+FieldPawnTest.Describe(a));
 var chips=FieldPawnTest.Rules(a).ChipsFor(0);Check(chips.Count==3&&chips[0].On&&!chips[1].Enabled&&chips[1].Why==pl.PlaceTexts.WhyNoNoise&&!a.Loot.CanWatch(0),"Lookout offered on the silent crate");
 Check(!chips[2].Enabled&&chips[2].Why==pl.PlaceTexts.WhyNoLight,"Unavailable equipment enabled");
 Check(FieldPawnTest.Choose(a,0,0)&&FieldPawnTest.Check(a).RunFor(0).Lead==1,"The lead stays when the helper's role is chosen again");
 Check(FieldPawnTest.Detail(a,0)&&s.PlacedLine.Contains(a.Participants[1].Name)&&s.PlacedLine.Contains(a.Participants[0].Name),"Stored placement lost: "+s.PlacedLine);Bounds(s.View);await Tap(s.Back);
 Check(FieldPawnTest.Detail(a,1)&&s.PlacedLine==s.ReadNobody,"Another object shows its own (nobody): "+s.PlacedLine);await Tap(s.Back);
 Check(a.Rooms.Turns==turn&&c.Campaign.MinuteOfDay==minute,"Placing or looking spent time");
 // The plan belongs to the room: a move (everyone at the door) takes the pawns off the crate; coming back finds nobody placed.
 Check(await FieldPawnTest.Move(a,1),"To the corridor");await Ready(a);Check(await FieldPawnTest.Move(a,0),"Back to the arcade");await Ready(a);
 Check(FieldPawnTest.Check(a).RunFor(0)==null&&!pl.Plan.HasAssignments&&(!a.Loot.Peek(0,out var crate)||crate.Progress==0),"Room roundtrip left pawns placed or searched");
 Check(FieldPawnTest.Detail(a,0),"Preview");Bounds(s.View);
 EventSystem.current.SetSelectedGameObject(null);return "PASS: read-only 07 open gate, pawns on the crate (함께), lookout greyed on the silent crate, equipment greyed without a flashlight, the lead kept when the role is chosen, placement shown in 07 per object, no time for placing or looking, a room roundtrip clears the room's plan, text bounds. Preview open.";
 }
}
