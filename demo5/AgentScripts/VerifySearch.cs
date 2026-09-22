using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;
public static class VerifySearch {
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(80);}
 static void Bounds(GameObject root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text overflow "+t.name+": "+t.preferredHeight+" / "+t.rectTransform.rect.height);}



 public static async Task<string> Flow(){
 var c=Object.FindAnyObjectByType<SettlementController>();await Tap(c.Exit);await Tap(c.ExpeditionPanel.Cards[0].Button);await Tap(c.ExpeditionPanel.Cards[1].Button);await Tap(c.ExpeditionPanel.Pack);await Tap(c.PackingPanel.Ready);await Tap(c.PackingPanel.Depart);await Task.Delay(950);
 var a=c.ArrivalPanel;var s=a.Search;int minute=c.Campaign.MinuteOfDay,turn=a.Rooms.Turns;
 await Tap(a.Objects[0]);Check(s.IsOpen&&!a.Main.blocksRaycasts&&!s.Choose.interactable,"Open/empty worker gate");await Tap(s.Cards[1].Button);var worker=s.Worker;await Tap(s.Paces[2]);Check(s.Worker==worker&&s.Pace==2,"Worker reset on pace change");await Tap(s.Duties[1]);Check(!s.Duties[2].interactable,"Unavailable equipment enabled");Bounds(s.View);
 await Tap(s.Choose);Check(s.Review.activeSelf&&!s.Workspace.blocksRaycasts,"Review gate");Bounds(s.Review);await Tap(s.Cancel);Check(s.Assignments.Count==0,"Cancel saved");await Tap(s.Choose);await Tap(s.Confirm);s.Confirm.onClick.Invoke();Check(!s.IsOpen&&a.Main.blocksRaycasts&&s.Assignments.Count==1,"Save/duplicate guard");
 await Tap(a.Objects[0]);Check(s.Worker==worker&&s.Pace==2&&s.Duty==1,"Stored assignment lost");await Tap(s.Back);await Tap(a.Objects[1]);Check(s.Worker==worker,"Object change reset worker");await Tap(s.Back);Check(a.Rooms.Turns==turn&&c.Campaign.MinuteOfDay==minute,"UI spent time");
 await Tap(a.Objects[3]);await Tap(a.ReturnConfirm);await Task.Delay(2400);await Tap(a.Rooms.CorridorBack);await Tap(a.ReturnConfirm);await Task.Delay(2400);await Tap(a.Objects[0]);Check(s.Pace==2&&s.Worker==worker,"Room roundtrip lost assignment");Bounds(s.View);await Tap(s.Paces[1]);await Tap(s.Cards[0].Button);
 EventSystem.current.SetSelectedGameObject(null);return "PASS: real click open/worker/pace/duty, equipment disabled, confirmation cancel/save, duplicate save guard, object & room retention, no time/resource charge, text bounds. Preview open.";
 }
}
