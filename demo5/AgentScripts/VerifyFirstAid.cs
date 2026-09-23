using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Demo5.FrontEnd;
public static class VerifyFirstAid {
 static SettlementController C=>UnityEngine.Object.FindAnyObjectByType<SettlementController>();
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var cam=b.GetComponentInParent<Canvas>().worldCamera;cam?.Render();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(cam,r.TransformPoint(r.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(70);}
 static async Task SelectCloth(bool bag=false){var p=C.InventoryPanel;await Tap((bag?p.BagRows:p.StockRows).First(r=>r.Label.text=="천 조각").Button);}
 public static async Task<string> Flow(){
  var c=C;var p=c.InventoryPanel;var person=c.Campaign.Party.First();var other=c.Campaign.Party.Last();int start=c.Campaign.MinuteOfDay;
  await Tap(c.Cabinet);await SelectCloth();Check(!p.Use.interactable&&!p.UseSelected(),"Full health consumes treatment");Check(p.StockCount("cloth")==2&&c.Campaign.MinuteOfDay==start,"Rejected action mutated state");
  person.Health=person.MaxHealth-1;p.Refresh();await SelectCloth();await Tap(p.Move);Check(!p.UseSelected(),"Nested transfer allowed treatment");await Tap(p.Cancel);await Tap(p.Use);Check(person.Health==person.MaxHealth&&p.StockCount("cloth")==0&&c.Campaign.MinuteOfDay==start+10,"Stock treatment accounting");Check(!p.UseSelected(),"Double use allowed");await Tap(p.CloseButton);
  // Fixture restores two cloth to exercise a distinct ownership path.
  var mat=c.CraftPanel.Materials.First(m=>m.Id=="cloth");mat.Initial=2;Check(p.MoveFor(person,"cloth",2,true),"Pack fixture");person.Health=person.MaxHealth-1;await Tap(c.Cabinet);await Tap(p.MemberCards[1].Button);Check(p.BagCount(1,"cloth")==0,"Ownership leak");await Tap(p.MemberCards[0].Button);await SelectCloth(true);person.Health=0;p.Refresh();Check(!p.UseSelected(),"Revival allowed");person.Health=person.MaxHealth-1;p.Refresh();await SelectCloth(true);await Tap(p.Use);Check(p.CountFor(person,"cloth")==0&&mat.Initial==0&&person.Health==person.MaxHealth,"Bag treatment accounting");await Tap(p.CloseButton);
  mat.Initial=2;person.Health=person.MaxHealth-1;await Tap(c.Workbench);await Tap(c.CraftPanel.RecipeRows[2].Button);await Tap(c.CraftPanel.WorkerRows[1].Button);await Tap(c.CraftPanel.Confirm);await Tap(c.Cabinet);Check(p.StockCount("cloth")==0,"Reserved cloth exposed");await Tap(p.CloseButton);
  await Tap(c.Workbench);await Tap(c.CraftPanel.OrderRows[0].Cancel);await Tap(c.CraftPanel.CancelYes);await Tap(c.CraftPanel.CloseButton);
  await Tap(c.Bed);await Tap(c.WorkPanel.Rows[0].Button);await Tap(c.WorkPanel.Confirm);await Tap(c.Cabinet);await SelectCloth();Check(!p.UseSelected(),"Busy patient treated");await Tap(p.CloseButton);await Tap(c.Bed);await Tap(c.WorkPanel.Rows[0].Button);await Tap(c.WorkPanel.Confirm);
  // Leave a real usable selection on screen; one stock treatment remains available.
  await Tap(c.Cabinet);await SelectCloth();Canvas.ForceUpdateCanvases();foreach(var t in p.View.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Clipped "+t.name);
  Check(other.Health==other.MaxHealth,"Other member altered");return "PASS stock/bag costs, +1 cap, 10 minutes, free rejection, no revival, full health, busy patient, reservation exclusion, nested modal, ownership, text bounds. Damage/materials in test are fixtures.";
 }
 public static async Task<string> Finish(){await Tap(C.InventoryPanel.Use);Check(C.InventoryPanel.Notice.text.Contains("응급 처치"),"Receipt missing");return "PASS real Use click and receipt.";}
}
