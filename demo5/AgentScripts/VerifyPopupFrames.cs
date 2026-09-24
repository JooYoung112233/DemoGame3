using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class VerifyPopupFrames {
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();b.GetComponentInParent<Canvas>().worldCamera?.Render();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(100);}
 static SettlementController C=>Object.FindAnyObjectByType<SettlementController>();
 static string Audit(GameObject view,CanvasGroup hud,params Button[] buttons){Check(hud.alpha==0,"Background HUD visible");Check(view.transform.Find("PopupHeading").gameObject.activeInHierarchy,"Missing header");Canvas.ForceUpdateCanvases();foreach(var t in view.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text clipped: "+t.name+" "+t.text);foreach(var b in buttons){var r=(RectTransform)b.transform;Check(Math.Abs(-r.anchoredPosition.y+r.rect.height-1050)<.1f,"Footer baseline: "+b.name);}return "PASS: header, hidden background HUD, text bounds, footer baseline.";}
 public static async Task<string> Time(){await Tap(C.Advance);return Audit(C.TimePanel.View,C.Main,C.TimePanel.CloseButton,C.TimePanel.Confirm);}
 public static async Task<string> Search(){await Tap(C.TimePanel.CloseButton);Check(C.Main.alpha==1,"Settlement HUD restore");var a=C.ArrivalPanel;C.InventoryPanel.MoveFor(C.Campaign.Party.First(),"supplies",1,true);Check(a.Begin(C.Campaign.Party.ToArray(),C.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")),"Departure");await Task.Delay(850);await Tap(a.Objects[0]);await Tap(a.Search.Cards[0].Button);return Audit(a.Search.View,a.Main,a.Search.Back,a.Search.Choose);}
 public static async Task<string> Bags(){var a=C.ArrivalPanel;await Tap(a.Search.Back);Check(a.Main.alpha==1,"Field HUD restore");await Tap(a.Cards[0].Button);return Audit(a.FieldBags.View,a.Main,a.FieldBags.Back,a.FieldBags.Transfer);}
 public static async Task<string> Loot(){var a=C.ArrivalPanel;await Tap(a.FieldBags.Back);Check(a.Main.alpha==1,"Bag close HUD restore");await Tap(a.Objects[0]);await Tap(a.Search.Cards[0].Button);await Tap(a.Search.Paces[0]);await Tap(a.Search.Choose);await Tap(a.Search.Confirm);Check(a.Loot.IsOpen,"Loot didn't open");return Audit(a.Loot.View,a.Main,new[]{a.Loot.Back,a.Loot.Transfer,a.Loot.TakeAll}.Where(b=>b!=null).ToArray());}
 public static async Task<string> Return(){var a=C.ArrivalPanel;await Tap(a.Loot.Back);if(a.Loot.LeaveReview.activeSelf)await Tap(a.Loot.LeaveConfirm);Check(a.Main.alpha==1,"Loot close HUD restore");await Tap(a.Return);await Tap(a.ReturnConfirm);return Audit(C.ReturnPanel.View,C.Main,C.ReturnPanel.Back,C.ReturnPanel.InspectBags,C.ReturnPanel.StoreAll);}
 public static async Task<string> Close(){await Tap(C.ReturnPanel.Back);Check(C.Main.alpha==1,"Return close HUD restore");return "PASS all five popup open/close transitions.";}
}
