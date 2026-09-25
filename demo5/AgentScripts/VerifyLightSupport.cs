using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
// 말 놓기 (2026-09-25, 기획/탐험-말놓기-조작-재설계.md): 조명 지원 is the role chip under the second pawn on the object (FieldPawnTest); a chip
// that cannot be chosen says why ('손전등 없음'); the 07 window (a right press) only shows the drops and who is placed; '턴 진행' runs it.
public static class VerifyLightSupport {
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();b.GetComponentInParent<Canvas>().worldCamera?.Render();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(100);}
 static async Task Craft(SettlementController c,string id){await Tap(c.Workbench);var recipes=c.CraftPanel.Recipes.Where(r=>r.Category==0).ToArray();int index=Array.FindIndex(recipes,r=>r.Id==id);var row=c.CraftPanel.RecipeRows[index];Canvas.ForceUpdateCanvases();c.CraftPanel.RecipeScroll.verticalNormalizedPosition=id=="flashlight"?0:1;await Task.Delay(150);await Tap(row.Button);await Tap(c.CraftPanel.WorkerRows[0].Button);await Tap(c.CraftPanel.Confirm);await Tap(c.Advance);await Tap(c.TimePanel.Choices[3]);await Tap(c.TimePanel.Confirm);await Tap(c.TimePanel.CloseButton);}
 static async Task Ready(ExpeditionArrivalPanel a){for(int i=0;i<40&&a.Popup.activeSelf==false&&!FieldPawnTest.Ready(a);i++)await Task.Delay(50);if(a.Popup.activeSelf)a.ClosePopup();for(int i=0;i<60&&!FieldPawnTest.Ready(a);i++)await Task.Delay(50);Check(FieldPawnTest.Ready(a),"Board not ready");}
 // The helper's 조명 chip (index 2) for two pawns on the crate: member 0 leads, member 1 helps.
 static FieldRoleChip LightChip(ExpeditionArrivalPanel a){Check(FieldPawnTest.Lead(a,0,0)&&FieldPawnTest.Join(a,1,0),"Two pawns on the crate: "+FieldPawnTest.Describe(a));var chips=FieldPawnTest.Rules(a).ChipsFor(1);Check(chips.Count==3,"Helper chips");return chips[2];}
 public static async Task<string> Flow(){
  FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c=Object.FindAnyObjectByType<SettlementController>();var a=c.ArrivalPanel;var s=a.Search;var inv=c.InventoryPanel;var party=c.Campaign.Party.ToArray();var target=c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall");
  await Craft(c,"nails");await Craft(c,"flashlight");Check(inv.StockCount("flashlight")==1,"Crafted light missing");Check(a.Begin(party,target),"Departure");await Task.Delay(850);await Ready(a);{var light=LightChip(a);Check(!light.Enabled&&light.Why==a.Threat.Planner.PlaceTexts.WhyNoLight&&!FieldPawnTest.Choose(a,1,2)&&!a.Loot.Advance(0,1,party[0],2)&&a.Rooms.Turns==0,"Stock light permitted action");}FieldPawnTest.Clear(a);await Tap(a.Return);await Tap(a.ReturnConfirm);await Tap(c.ReturnPanel.Back);
  Check(inv.MoveFor(party[0],"flashlight",1,true),"Pack light");Check(a.Begin(party,target),"Depart with light");await Task.Delay(850);await Ready(a);Check(!LightChip(a).Enabled,"Self light counted as support");FieldPawnTest.Clear(a);await Tap(a.Cards[0].Button);await Tap(a.FieldBags.LeftRows.First(r=>r.Label.text=="손전등").Button);await Tap(a.FieldBags.Transfer);await Tap(a.FieldBags.Back);
  await Ready(a);{var light=LightChip(a);Check(light.Enabled&&FieldPawnTest.Choose(a,1,2)&&FieldPawnTest.Role(a,0)==FieldAction.Light&&a.Loot.LightSupport(party[0])==party[1],"Valid support blocked");}
  Check(FieldPawnTest.Detail(a,0)&&s.ReadOnly,"07 read only");for(int i=0;i<s.DropPreview.Rows.Count;i++)Check(s.DropPreview.Rows[i].Chance.text==ExpeditionLootPanel.ChanceFor(a.Loot.Sites[0].Drops[i],a.Loot.LightBonus)+"%","Light preview incorrect");
  Check(s.PlacedLine.Contains(string.Format(s.ReadHelper,party[1].Name,s.ReadLight)),"07 names the light support: "+s.PlacedLine);await Tap(s.Back);Check(a.Rooms.Turns==0&&inv.CountFor(party[1],"flashlight")==1,"Placing consumed tool/time");
  await Task.Delay(250);await Tap(a.Threat.Planner.TurnButton);await Task.Delay(200);Check(a.Loot.State(0).Bonus==20&&a.Loot.State(0).Duty==2&&a.Loot.State(0).Progress==1&&inv.CountFor(party[1],"flashlight")==1,"Committed light support incorrect");
  int health=party[1].Health;party[1].Health=0;try{int turn=a.Rooms.Turns;Check(!a.Loot.Advance(0,1,party[0],2)&&turn==a.Rooms.Turns,"Incapacitated supporter advanced search");}finally{party[1].Health=health;}
  await Ready(a);{var chips=FieldPawnTest.Rules(a).ChipsFor(1);Check(FieldPawnTest.Role(a,0)==FieldAction.Light&&chips.Count==3&&chips[2].On&&!chips[0].Enabled&&chips[0].Why==a.Threat.Planner.PlaceTexts.WhyLocked,"Resume light state lost (the running role is fixed)");}
  Check(FieldPawnTest.Detail(a,0)&&s.DropPreview.Note.text.Contains("진행 중"),"Resume light state lost");Canvas.ForceUpdateCanvases();foreach(var t in new[]{s.Cost,s.Notice,s.DropPreview.Summary})Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Clipped "+t.name);await Tap(s.Back);
  return "PASS: actual crafting, stock-only/self held light greyed out ('손전등 없음'), field handoff activates the 조명 chip, shared probabilities, placing is free, reusable item, incapacity rejection, resumed state (role fixed) and text bounds.";
 }
}
