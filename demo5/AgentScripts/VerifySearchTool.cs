using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
// 말 놓기 (2026-09-25, 기획/탐험-말놓기-조작-재설계.md): who may open a locked object is whose pawn can stand there — the object's place for
// a member without the tool in their own bag is greyed out ('{도구} 필요'); the 07 window (a right press) shows the tool line; '턴 진행' runs
// the search (the lid opening included).
public static class VerifySearchTool {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 // A facility takes a press only inside its drawn outline (SettlementFacilityFocus), which can miss the rect centre: aim inside it (as
 // VerifyFacilityContours does).
 static Vector2 At(Button b,Camera cam){var r=(RectTransform)b.transform;var pos=RectTransformUtility.WorldToScreenPoint(cam,r.TransformPoint(r.rect.center));var f=b.GetComponent<SettlementFacilityFocus>();if(!f||f.IsRaycastLocationValid(pos,cam))return pos;for(int y=1;y<10;y++)for(int x=1;x<10;x++){var test=RectTransformUtility.WorldToScreenPoint(cam,r.TransformPoint(new Vector3(r.rect.xMin+r.rect.width*x/10,r.rect.yMax-r.rect.height*y/10,0)));if(f.IsRaycastLocationValid(test,cam))return test;}throw new Exception("No hit outline "+b.name);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();b.GetComponentInParent<Canvas>().worldCamera?.Render();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=At(b,b.GetComponentInParent<Canvas>().worldCamera),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(100);}
 // Fixture (2026-09-26, run after VerifyFieldTurnPlan.Enter): a new game starts with no workbench and an empty stock (도입 0→1→2→5; the
 // bench comes after the opening chapter's first return). These checks are about later play: the opening chapter is off, the stock room
 // and the workbench are restored and the old starting stock is granted (wood 2 · rope 3 · scrap 3 · cloth 2 · food 6 · water 4 · can 2 ·
 // raw water 2).
 static async Task LaterPlay(SettlementController c){if(c.Opening)c.Opening.State.Enabled=false;var d=c.Development.State;d.Warehouse=Math.Max(1,d.Warehouse);d.Workbench=true;foreach(var (id,count) in new[]{("wood",2),("rope",3),("scrap",3),("cloth",2),("food",6),("water",4),("can",2),("raw-water",2)}){var m=c.CraftPanel.Materials.First(x=>x.Id==id);m.Initial=Math.Max(m.Initial,count);}c.RefreshResources();await Task.Delay(200);/* the settlement buttons follow on the next LateUpdate */}
 static async Task Craft(SettlementController c,int recipe,int count){await Tap(c.Workbench);await Tap(c.CraftPanel.RecipeRows[recipe].Button);await Tap(c.CraftPanel.WorkerRows[0].Button);for(int i=1;i<count;i++)await Tap(c.CraftPanel.Plus);await Tap(c.CraftPanel.Confirm);await Tap(c.Advance);await Tap(c.TimePanel.Choices[3]);await Tap(c.TimePanel.Confirm);await Tap(c.TimePanel.CloseButton);}
 static async Task Ready(ExpeditionArrivalPanel a){for(int i=0;i<20&&!a.Popup.activeSelf&&!FieldPawnTest.Ready(a);i++)await Task.Delay(50);if(a.Popup.activeSelf)a.ClosePopup();for(int i=0;i<60&&!FieldPawnTest.Ready(a);i++)await Task.Delay(50);Check(FieldPawnTest.Ready(a),"Board not ready");}
 static FieldPlaceOption Machine(ExpeditionArrivalPanel a,int m)=>FieldPawnTest.Option(a,m,FieldPawnTest.SearchKey(2),FieldSpotKind.Lead);
 static async Task Turn(ExpeditionArrivalPanel a){await Task.Delay(250);await Tap(a.Threat.Planner.TurnButton);await Task.Delay(200);}
 public static async Task<string> Preview(){
  FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c=Object.FindAnyObjectByType<SettlementController>();var a=c.ArrivalPanel;var people=c.Campaign.Party.ToArray();var destination=c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall");
  await LaterPlay(c);await Craft(c,1,2);await Craft(c,0,1);Check(c.InventoryPanel.StockCount("prybar")==1,"Crafted tool missing");
  Check(a.Begin(people,destination),"Departure");await Task.Delay(850);await Ready(a);int turn=a.Rooms.Turns,minute=c.Campaign.MinuteOfDay;var tx=a.Threat.Planner.PlaceTexts;
  {var o=Machine(a,0);Check(o!=null&&!o.Enabled&&o.Blocked==string.Format(tx.NeedTool,a.Threat.Planner.ToolName(2))&&!FieldPawnTest.Lead(a,0,2)&&!a.Loot.Advance(2,1,people[0]),"Stock-only tool allowed search");}
  Check(a.Rooms.Turns==turn&&c.Campaign.MinuteOfDay==minute&&!a.Loot.State(2).Opened,"Blocked action spent time/opened");
  Check(FieldPawnTest.Detail(a,2)&&a.Search.ReadOnly,"07 of the machine");await Tap(a.Search.Back);await Tap(a.Return);await Tap(a.ReturnConfirm);await Tap(c.ReturnPanel.Back);
  Check(c.InventoryPanel.MoveFor(people[0],"prybar",1,true),"Packing tool");Check(a.Begin(people,destination),"Second departure");await Task.Delay(850);await Ready(a);
  Check(Machine(a,1)?.Enabled==false,"Wrong worker can use another bag");Check(Machine(a,0)?.Enabled==true&&FieldPawnTest.Lead(a,0,2),"Carrier not eligible");
  Check(FieldPawnTest.Detail(a,2)&&a.Search.ToolIcon.sprite!=null&&a.Search.Equipment.text==string.Format(a.Search.ReadToolHeld,c.InventoryPanel.Items.First(i=>i.Id=="prybar").Name),"The carrier's tool line: "+a.Search.Equipment.text);
  Canvas.ForceUpdateCanvases();Check(a.Search.Equipment.preferredHeight<=a.Search.Equipment.rectTransform.rect.height+1,"Tool line clipped");Check(a.Search.Cost.preferredHeight<=a.Search.Cost.rectTransform.rect.height+1,"Search cost clipped");return "PASS: crafted nails then prybar, stock-only greyed out without time cost, actual carrier packing and whose pawn may stand there; ready preview (07 open).";
 }
 public static async Task<string> Flow(){
  FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c=Object.FindAnyObjectByType<SettlementController>();var a=c.ArrivalPanel;var people=c.Campaign.Party.ToArray();if(a.Search.IsOpen)await Tap(a.Search.Back);await Ready(a);int before=a.Rooms.Turns;
  if(FieldPawnTest.Check(a).RunFor(2)==null)Check(FieldPawnTest.Lead(a,0,2),"The carrier's pawn on the machine");
  Check(FieldPawnTest.Join(a,1,2,1),"The second pawn watches");/* 망보기 on the loud machine: 2 turns */var run=FieldPawnTest.Check(a).RunFor(2);
  Check(run!=null&&run.Role==FieldAction.Watch&&run.Required==2&&a.Rooms.Turns==before&&!a.Loot.State(2).Opened,"Choosing the role mutated state");
  await Turn(a);Check(a.Loot.State(2).Opened&&a.Loot.State(2).Progress==1&&a.Rooms.Turns==before+1,"Opening/first search must be one turn");Check(c.InventoryPanel.CountFor(people[0],"prybar")==1,"Reusable tool consumed");
  await Tap(a.Return);await Tap(a.ReturnConfirm);await Tap(c.ReturnPanel.StoreAll);await Tap(c.ReturnPanel.Back);
  Check(a.Begin(people,c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")),"Revisit");await Task.Delay(850);await Ready(a);
  Check(Machine(a,1)?.Enabled==true&&FieldPawnTest.Lead(a,1,2),"Opened access did not persist without tool");Check(FieldPawnTest.Detail(a,2)&&a.Search.Equipment.text==a.Search.ReadToolOpened,"Opened line: "+a.Search.Equipment.text);await Tap(a.Search.Back);
  // Avoid random combat in this non-combat UI regression, then restore the inspector setting.
  int maximum=a.Encounter.MaximumChance;try{a.Encounter.MaximumChance=0;await Ready(a);await Turn(a);}finally{a.Encounter.MaximumChance=maximum;}
  Check(a.Loot.State(2).Complete&&a.Loot.IsOpen&&!a.Loot.Advance(2,0,people[1]),"Completion/reward reroll gate");return "PASS: choosing a role is free, tool reused, first opening included in the search turn, return/revisit without tool resumes, completion opens loot and cannot reroll.";
 }
}
