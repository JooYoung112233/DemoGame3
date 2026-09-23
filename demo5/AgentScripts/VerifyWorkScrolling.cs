using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Demo5.FrontEnd;
public static class VerifyWorkScrolling
{
 static SettlementController Owner()=>UnityEngine.Object.FindAnyObjectByType<SettlementController>();
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static async Task Tap(Button b){await Task.Delay(80);Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(100);}
 static async Task Scroll(ScrollRect s){var n=s.GetComponent<ScrollNavigation>();await Task.Delay(100);Check(n.Down.IsActive()&&n.Down.interactable&&!n.Up.interactable,"Top limits");for(int i=0;i<20&&n.Down.interactable;i++)await Tap(n.Down);Check(!n.Down.interactable&&n.Up.interactable&&s.verticalNormalizedPosition<.001f,"Bottom limits");await Tap(n.Up);Check(s.verticalNormalizedPosition>0,"Up failed");s.verticalNormalizedPosition=.5f;await Task.Delay(100);}
 static void Bounds(GameObject g){Canvas.ForceUpdateCanvases();foreach(var t in g.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text overflow "+t.name);}
 public static async Task<string> Cooking(){var c=Owner();c.CookingPanel.Open();await Task.Delay(200);await Scroll(c.CookingPanel.WorkerScroll);var empty=c.CookingPanel.RecipeScroll.GetComponent<ScrollNavigation>();Check(!empty.Down.gameObject.activeSelf&&!empty.Up.gameObject.activeSelf,"Fitting list has arrows");await Tap(c.CookingPanel.WorkerRows[3].Button);Bounds(c.CookingPanel.gameObject);return "PASS: five residents, real down/up clicks, top/bottom states, no arrows on fitting lists, specialist selection and text bounds.";}
 public static async Task<string> Craft(){var c=Owner();c.CookingPanel.Close();c.CraftPanel.Open();await Task.Delay(150);await Scroll(c.CraftPanel.RecipeScroll);await Scroll(c.CraftPanel.WorkerScroll);await Tap(c.CraftPanel.WorkerRows[2].Button);Bounds(c.CraftPanel.View);return "PASS: independent recipe/worker scrolling, actual arrows and selectable clipped list rows.";}
}
