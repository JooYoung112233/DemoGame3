using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class VerifyLightSupport {
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();b.GetComponentInParent<Canvas>().worldCamera?.Render();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(100);}
 static async Task Craft(SettlementController c,string id){await Tap(c.Workbench);var recipes=c.CraftPanel.Recipes.Where(r=>r.Category==0).ToArray();int index=Array.FindIndex(recipes,r=>r.Id==id);var row=c.CraftPanel.RecipeRows[index];Canvas.ForceUpdateCanvases();c.CraftPanel.RecipeScroll.verticalNormalizedPosition=id=="flashlight"?0:1;await Task.Delay(150);await Tap(row.Button);await Tap(c.CraftPanel.WorkerRows[0].Button);await Tap(c.CraftPanel.Confirm);await Tap(c.Advance);await Tap(c.TimePanel.Choices[3]);await Tap(c.TimePanel.Confirm);await Tap(c.TimePanel.CloseButton);}
 public static async Task<string> Flow(){
  var c=Object.FindAnyObjectByType<SettlementController>();var a=c.ArrivalPanel;var s=a.Search;var inv=c.InventoryPanel;var party=c.Campaign.Party.ToArray();var target=c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall");
  await Craft(c,"nails");await Craft(c,"flashlight");Check(inv.StockCount("flashlight")==1,"Crafted light missing");Check(a.Begin(party,target),"Departure");await Task.Delay(850);await Tap(a.Objects[0]);await Tap(s.Cards[0].Button);Check(!s.Duties[2].interactable&&!a.Loot.Advance(0,1,party[0],2)&&a.Rooms.Turns==0,"Stock light permitted action");await Tap(s.Back);await Tap(a.Return);await Tap(a.ReturnConfirm);await Tap(c.ReturnPanel.Back);
  Check(inv.MoveFor(party[0],"flashlight",1,true),"Pack light");Check(a.Begin(party,target),"Depart with light");await Task.Delay(850);await Tap(a.Objects[0]);await Tap(s.Cards[0].Button);Check(!s.Duties[2].interactable,"Self light counted as support");await Tap(s.Back);await Tap(a.Cards[0].Button);await Tap(a.FieldBags.LeftRows.First(r=>r.Label.text=="손전등").Button);await Tap(a.FieldBags.Transfer);await Tap(a.FieldBags.Back);
  await Tap(a.Objects[0]);await Tap(s.Cards[0].Button);await Tap(s.Duties[2]);Check(a.Loot.LightSupport(party[0])==party[1]&&s.Choose.interactable,"Valid support blocked");for(int i=0;i<s.DropPreview.Rows.Count;i++)Check(s.DropPreview.Rows[i].Chance.text==ExpeditionLootPanel.ChanceFor(a.Loot.Sites[0].Drops[i],1,a.Loot.LightBonus)+"%","Light preview incorrect");
  await Tap(s.Choose);Check(s.ReviewBody.text.Contains("조명 · "+party[1].Name),"Review support identity");await Tap(s.Cancel);Check(a.Rooms.Turns==0&&inv.CountFor(party[1],"flashlight")==1,"Cancel consumed tool/time");await Tap(s.Choose);await Tap(s.Confirm);Check(a.Loot.State(0).Bonus==20&&a.Loot.State(0).Duty==2&&a.Loot.State(0).Progress==1&&inv.CountFor(party[1],"flashlight")==1,"Committed light support incorrect");
  int health=party[1].Health;party[1].Health=0;try{int turn=a.Rooms.Turns;Check(!a.Loot.Advance(0,1,party[0],2)&&turn==a.Rooms.Turns,"Incapacitated supporter advanced search");}finally{party[1].Health=health;}
  await Tap(s.Back);await Tap(a.Objects[0]);Check(s.Duty==2&&s.Choose.interactable&&s.DropPreview.Note.text.Contains("진행 중"),"Resume light state lost");Canvas.ForceUpdateCanvases();foreach(var t in new[]{s.Cost,s.Notice,s.ReviewBody,s.DropPreview.Summary})Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Clipped "+t.name);
  return "PASS: actual crafting, stock-only/self held blocked, field handoff activates support, shared probabilities, free cancel, reusable item, incapacity rejection, resumed state and text bounds.";
 }
}
