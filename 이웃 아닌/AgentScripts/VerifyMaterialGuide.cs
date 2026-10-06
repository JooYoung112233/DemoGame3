using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Demo5.FrontEnd;
public static class VerifyMaterialGuide {
 static SettlementController C=>UnityEngine.Object.FindAnyObjectByType<SettlementController>();
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var cam=b.GetComponentInParent<Canvas>().worldCamera;cam?.Render();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(cam,r.TransformPoint(r.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(70);}
 static void Bounds(){Canvas.ForceUpdateCanvases();foreach(var t in C.CraftPanel.MaterialGuide.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Guide clipped "+t.name);}
 public static async Task<string> Flow(){
  var c=C;var p=c.CraftPanel;int time=c.Campaign.MinuteOfDay;var person=c.Campaign.Party.First();
  await Tap(c.Workbench);var g=p.MaterialGuide;Check(g.FocusId=="nails"&&g.ActionLabel.text=="제작법 보기","Default shortage");Bounds();await Tap(p.WorkerRows[0].Button);await Tap(g.Action);Check(p.DetailTitle.text=="간이 못"&&p.Confirm.interactable,"Recipe jump lost worker");
  await Tap(p.RecipeRows[0].Button);await Tap(p.Plus);await Tap(p.CostRows[0].GetComponent<Button>());Check(g.FocusId=="wood"&&g.Counts.text.Contains("부족 2")&&g.ActionLabel.text=="원정 계획","Quantity/source: "+g.FocusId+" | "+g.Counts.text+" | "+g.ActionLabel.text);Bounds();await Tap(p.CloseButton);
  Check(c.InventoryPanel.MoveFor(person,"wood",1,true),"Pack existing wood");await Tap(c.Workbench);await Tap(p.Plus);await Tap(p.CostRows[0].GetComponent<Button>());Check(g.ActionLabel.text=="가방 정리"&&g.Source.text.Contains("1개"),"Bag source");await Tap(g.Action);var inv=c.InventoryPanel;Check(inv.IsOpen&&!p.IsOpen&&inv.MemberIndex==0,"Bag route");await Tap(inv.BagRows.First(r=>r.Label.text=="나무 판자").Button);await Tap(inv.Move);await Tap(inv.Confirm);await Tap(inv.CloseButton);Check(inv.StockCount("wood")==2,"Stock return");
  await Tap(c.Workbench);await Tap(p.RecipeRows[2].Button);await Tap(p.Plus);await Tap(p.CostRows[0].GetComponent<Button>());Check(g.FocusId=="cloth"&&g.ActionLabel.text=="원정 계획","Cloth source");Bounds();await Tap(g.Action);Check(c.ExpeditionPanel.IsOpen&&!p.IsOpen,"Plan route");await Tap(c.ExpeditionPanel.Back);
  Check(c.Campaign.MinuteOfDay==time&&p.Orders.Count==0&&inv.StockCount("wood")==2&&p.Available("nails")==0,"Inspection consumed resources/time");
  await Tap(c.Workbench);Bounds();return "PASS actual cost row clicks, live quantity shortage, recipe route preserves worker, correct bag route and return, expedition plan route, no time/resource/order side effects, text bounds.";
 }
}
