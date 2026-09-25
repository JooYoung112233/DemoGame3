using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
// 말 놓기 (2026-09-25, 기획/탐험-말놓기-조작-재설계.md): the roles are the chips under the second pawn on the object (FieldPawnTest); the 07
// window (a right press) shows the drop preview for the plan as placed; '턴 진행' runs it.
 public static class VerifySearchPreview {
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();b.GetComponentInParent<Canvas>().worldCamera?.Render();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(100);}
 static async Task Ready(ExpeditionArrivalPanel a){for(int i=0;i<60&&!FieldPawnTest.Ready(a);i++)await Task.Delay(50);Check(FieldPawnTest.Ready(a),"Board not ready");}
 public static async Task<string> Flow(){
  FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c=Object.FindAnyObjectByType<SettlementController>();var a=c.ArrivalPanel;var s=a.Search;
  Check(a.Begin(c.Campaign.Party.ToArray(),c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")),"Departure");await Task.Delay(850);await Ready(a);
  Check(FieldPawnTest.Lead(a,0,0)&&FieldPawnTest.Join(a,1,0),"Two pawns on the crate: "+FieldPawnTest.Describe(a));
  int minute=c.Campaign.MinuteOfDay,crateTurns=a.Loot.Sites[0].Turns;var rng=UnityEngine.Random.state;
  for(int duty=0;duty<3;duty++){
   var chip=FieldPawnTest.Rules(a).ChipsFor(1)[duty];
   if(!chip.Enabled){Check(duty==2||duty==1&&!a.Loot.CanWatch(0),"Role "+duty+" not offered ("+chip.Why+")");continue;}
   Check(FieldPawnTest.Choose(a,1,duty),"Role "+duty);Check(FieldPawnTest.Detail(a,0),"07");var preview=s.DropPreview;
   Check(preview.Rows.Count==a.Loot.Sites[0].Drops.Length,"Rows");for(int i=0;i<preview.Rows.Count;i++)Check(preview.Rows[i].Chance.text==ExpeditionLootPanel.ChanceFor(a.Loot.Sites[0].Drops[i],duty==2?a.Loot.LightBonus:0)+"%","Chance mismatch");
   int turns=FieldTurnPlan.RequiredOf(a.Loot.SiteTurns(0),duty==0&&a.Participants.Count(p=>p.Health>0)>1);Check(preview.Summary.text.Contains(turns*a.Rooms.MinutesPerTurn+"분")&&s.Cost.text.Contains("/ "+turns+"턴"),"Time mismatch: "+preview.Summary.text+" / "+s.Cost.text);
   await Tap(s.Back);await Ready(a);
  }
  Check(c.Campaign.MinuteOfDay==minute&&a.Rooms.Turns==0&&UnityEngine.Random.state.Equals(rng)&&a.Loot.State(0).Loot.Count==0,"Preview mutated time/random/loot");
  a.Loot.Sites[0].Turns=3;/* a 3-turn fixture: 함께 makes it 2, so one turn leaves it partial */Check(FieldPawnTest.Choose(a,1,0),"함께");Check(a.Rooms.Turns==0,"Choosing spent a turn");
  await Task.Delay(250);await Tap(a.Threat.Planner.TurnButton);await Task.Delay(200);await Ready(a);
  var locked=FieldPawnTest.Rules(a).ChipsFor(1);Check(a.Loot.State(0).Progress==1&&locked.Count==3&&!locked[0].Enabled&&locked[0].Why==a.Threat.Planner.PlaceTexts.WhyLocked,"Partial progress (the role is fixed)");
  Check(FieldPawnTest.Detail(a,0)&&s.DropPreview.Summary.text.Contains("남은 1턴"),"Partial preview: "+s.DropPreview.Summary.text);await Tap(s.Back);await Ready(a);Check(FieldPawnTest.Detail(a,0)&&s.DropPreview.Note.text.Contains("진행 중"),"Reopen lost preview state");
  Canvas.ForceUpdateCanvases();foreach(var t in s.DropPreview.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text clipped "+t.name);var original=a.Loot.Sites[0].Drops;try{a.Loot.Sites[0].Drops=original.Concat(original).Concat(original).ToArray();s.DropPreview.Refresh(a,0,0);var scroll=s.DropPreview.Content.parent.GetComponent<ScrollRect>();Check(s.DropPreview.Content.rect.height>scroll.viewport.rect.height,"Expected overflowing list");scroll.verticalNormalizedPosition=0;await Task.Delay(100);Check(scroll.verticalNormalizedPosition<.01f,"Scroll failed");scroll.verticalNormalizedPosition=1;}finally{a.Loot.Sites[0].Drops=original;s.DropPreview.Refresh(a,0,0);}
  a.Loot.Sites[0].Turns=crateTurns;return "PASS: every role chip's displayed probabilities share the roll formula, time/noise preview, no RNG/time/loot changes on inspection, choosing is free, progress/reopen consistency (role fixed), text bounds and scroll.";
 }
}
