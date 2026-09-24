using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
 public static class VerifySearchPreview {
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();b.GetComponentInParent<Canvas>().worldCamera?.Render();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(100);}
 public static async Task<string> Flow(){
  var c=Object.FindAnyObjectByType<SettlementController>();var a=c.ArrivalPanel;var s=a.Search;
  Check(a.Begin(c.Campaign.Party.ToArray(),c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")),"Departure");await Task.Delay(850);await Tap(a.Objects[0]);await Tap(s.Cards[0].Button);
  int minute=c.Campaign.MinuteOfDay;var rng=UnityEngine.Random.state;
  for(int pace=0;pace<3;pace++){await Tap(s.Paces[pace]);for(int duty=0;duty<2;duty++){if(duty==1&&pace!=0){Check(!s.Duties[1].interactable,"Lookout offered at pace "+pace);continue;}await Tap(s.Duties[duty]);var preview=s.DropPreview;Check(preview.Rows.Count==a.Loot.Sites[0].Drops.Length,"Rows");for(int i=0;i<preview.Rows.Count;i++)Check(preview.Rows[i].Chance.text==ExpeditionLootPanel.ChanceFor(a.Loot.Sites[0].Drops[i],pace,duty==0?10:0)+"%","Chance mismatch");Check(preview.Summary.text.Contains((pace+1)*a.Rooms.MinutesPerTurn+"분"),"Time mismatch");}}
  Check(c.Campaign.MinuteOfDay==minute&&a.Rooms.Turns==0&&UnityEngine.Random.state.Equals(rng)&&a.Loot.State(0).Loot.Count==0,"Preview mutated time/random/loot");
  await Tap(s.Paces[2]);await Tap(s.Duties[0]);await Tap(s.Choose);await Tap(s.Cancel);Check(a.Rooms.Turns==0,"Cancel spent turn");await Tap(s.Choose);await Tap(s.Confirm);Check(a.Loot.State(0).Progress==1&&s.DropPreview.Summary.text.Contains("남은 2턴")&&!s.Paces[0].interactable,"Partial progress");await Tap(s.Back);await Tap(a.Objects[0]);Check(s.DropPreview.Note.text.Contains("진행 중"),"Reopen lost preview state");
  Canvas.ForceUpdateCanvases();foreach(var t in s.DropPreview.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text clipped "+t.name);var original=a.Loot.Sites[0].Drops;try{a.Loot.Sites[0].Drops=original.Concat(original).Concat(original).ToArray();s.DropPreview.Refresh(a,0,2,0);var scroll=s.DropPreview.Content.parent.GetComponent<ScrollRect>();Check(s.DropPreview.Content.rect.height>scroll.viewport.rect.height,"Expected overflowing list");scroll.verticalNormalizedPosition=0;await Task.Delay(100);Check(scroll.verticalNormalizedPosition<.01f,"Scroll failed");scroll.verticalNormalizedPosition=1;}finally{a.Loot.Sites[0].Drops=original;s.DropPreview.Refresh(a,0,2,0);}
  return "PASS: all pace/duty displayed probabilities share roll formula, time/noise preview, no RNG/time/loot changes on inspection, cancel free, progress/reopen consistency, text bounds and scroll.";
 }
}
