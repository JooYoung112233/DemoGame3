using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Threading.Tasks;using UnityEngine;using UnityEngine.UI;using UnityEngine.SceneManagement;using UnityEngine.EventSystems;using Demo5.FrontEnd;using Demo5.NightRun;using Object=UnityEngine.Object;
public static class VerifyTutorialClarity {
 static SettlementController C=>Object.FindAnyObjectByType<SettlementController>();
 static SettlementTutorialGuide G=>C?C.GetComponent<SettlementTutorialGuide>():null;
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static async Task Tap(Button b){Check(b&&b.IsActive()&&b.IsInteractable(),"Unavailable button "+b?.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var cam=b.GetComponentInParent<Canvas>().worldCamera;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(cam,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);
  if(b.GetComponent<SettlementFacilityFocus>()){for(int y=1;y<10&&(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=b);y++)for(int x=1;x<10;x++){e.position=RectTransformUtility.WorldToScreenPoint(cam,r.TransformPoint(new Vector2(r.rect.xMin+r.rect.width*x/10,r.rect.yMin+r.rect.height*y/10)));hits.Clear();EventSystem.current.RaycastAll(e,hits);if(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b)break;}}
  Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+hits.FirstOrDefault().gameObject?.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(250);
 }
 // 말 놓기 (2026-09-25): on the room screen the guide's next press is a pawn, its silhouette, the second pawn, the co-op silhouette or '턴 진행'.
 static string Kind(ExpeditionArrivalPanel a,Button t){var h=t?t.GetComponent<FieldPawnHandle>():null;if(h)return h.Role==FieldPawnHandle.Kind.Pawn?"pawn":"ghost";return t&&t==a.Threat.Planner.TurnButton?"turn":t?t.name:"-";}
 // The story panel (SettlementTutorialNarrative) is not what this checks: mark it read so its Dim does not cover the guide.
 static void Hush(){var s=C?C.GetComponent<SettlementTutorialNarrative>():null;if(s)s.Restore(new SavedTutorialNarrative{SeenMask=SavedTutorialNarrative.AllSeen,PendingBeat=-1});}
 static async Task Next(){await Task.Delay(60);Check(G.Target,"No next target: "+G.Guidance);await Tap(G.Target);}
 static async Task Shot(string name){Directory.CreateDirectory("Assets/Screenshots/TutorialClarity");ScreenCapture.CaptureScreenshot("Assets/Screenshots/TutorialClarity/"+name+".png");await Task.Delay(400);if(G&&G.Banner.activeSelf){Check(G.Title.preferredHeight<=G.Title.rectTransform.rect.height+1,"Guide title clipped");Check(G.Instruction.preferredHeight<=G.Instruction.rectTransform.rect.height+1,"Guide text clipped: "+G.Instruction.text);}}
 public static async Task<string> Start(){
  PartySelectionSession.Clear();SceneManager.LoadScene("PartySelection");await Task.Delay(500);
  var p=Object.FindAnyObjectByType<PartySelectionController>();Check(p.Cards.Count(c=>c.gameObject.activeSelf)==2,"Roster is not restricted");Check(PartySelectionSession.Selected.SequenceEqual(new[]{"scout","medic"}),"Wrong opening pair");
  p.Toggle("mechanic");p.Preview("guard");Check(PartySelectionSession.Selected.SequenceEqual(new[]{"scout","medic"})&&p.FocusedCandidateId=="scout","Hidden starter bypass");
  Check(!p.Previous.gameObject.activeSelf&&!p.NextPage.gameObject.activeSelf,"Unnecessary pagination");
  await Tap(p.Cards[1].Button);Check(p.FocusedCandidateId=="medic"&&p.SelectedCount==2,"Click must preview without removing mandatory pair");await Shot("01-starting-pair");
  await Tap(p.Continue);var h=Object.FindAnyObjectByType<HomeSelectionController>();await Tap(h.Cards[1].Button);await Tap(h.Continue);await Task.Delay(400);Hush();await Task.Delay(200);
  Check(C.Introduction.Step==0&&G.Target==C.Introduction.Action,"First action missing");await Shot("02-first-action");
  await Next();await Next();Check(C.Introduction.Step==2,"Initial actions did not progress");
  var save=CampaignPersistence.Capture(C);CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/TutorialClaritySave");try{Check(CampaignSaveStore.Write(0,save,C,out var error),error);var read=CampaignSaveStore.Read(0,C);Check(read.CanLoad,read.Error);CampaignPersistence.Prepare(read.Data,C);}finally{CampaignSaveStore.TestDirectory=null;}
  // Wait for the reloaded scene instead of a fixed delay (a busy editor can take longer than 0.5 s to load and run the guide once).
  SceneManager.LoadScene("Settlement");await Task.Delay(300);for(int i=0;i<60&&!(C&&C.Introduction&&C.Introduction.Step==2&&G&&G.Target==C.Introduction.Action);i++)await Task.Delay(100);Check(C.Introduction.Step==2&&G.Target==C.Introduction.Action,"Reload lost tutorial step");
  // 2026-09-25 (the story tutorial): the rest/time/storage lessons were retired; step 2 leads straight to the first outing (step 5),
  // and the guide points at the exit.
  await Next();Check(C.Introduction.Step==5&&G.Target,"First expedition goal after clearing a spot: "+G.Guidance);await Shot("03-first-outing");
  await Next();for(int i=0;i<6&&!C.PackingPanel.IsOpen;i++)await Next();Check(C.PackingPanel.IsOpen&&C.ExpeditionPanel.Selected.Count==2,"Both survivors must be selected");await Shot("07-packing");
  return "PASS only scout/medic start, hidden candidate cannot be selected/previewed, 2-card preview, real scene transitions, disk save/load step 2, one-next-click flow to the expedition, both survivors selected. Paused at packing.";
 }
 public static async Task<string> FirstTrip(){
  Check(C.PackingPanel.IsOpen,"Start test first");await Next();await Next();for(int i=0;i<60&&C.ArrivalPanel.InTransit;i++)await Task.Delay(100);await Task.Delay(200);
  var a=C.ArrivalPanel;var pl=a.Threat.Planner;Check(FieldPawnBoard.For(a),"Run BuildPawnBoard.Run first (the pawn board)");
  // The crate: pick up a pawn, its silhouette, the second pawn, the co-op silhouette, '턴 진행' (one turn), then the finds.
  var kinds=new List<string>();int turns=a.Rooms.Turns;
  for(int i=0;i<8&&!a.Loot.IsOpen;i++){
   await Task.Delay(120);Check(G.Target,"No next target: "+G.Guidance);var k=Kind(a,G.Target);kinds.Add(k);
   if(i==0)await Shot("08-pick-pawn");if(k=="ghost"&&kinds.Count(x=>x=="ghost")==2)await Shot("08b-coop-silhouette");
   int before=a.Rooms.Turns;await Tap(G.Target);if(k=="turn")Check(a.Rooms.Turns==before+1,"'턴 진행' = one turn");
   Check(!a.Encounter.IsOpen,"The guided crate met something");
  }
  Check(string.Join(",",kinds)=="pawn,ghost,pawn,ghost,turn","Guide order on the crate: "+string.Join(",",kinds));
  Check(a.Loot.IsOpen&&a.Rooms.Turns==turns+1&&pl.LastCheck!=null&&pl.LastCheck.Runs.Count==1&&pl.LastCheck.Runs[0].Support>=0,"The co-op crate took one turn");await Shot("09-found-items");
  var loot=a.Loot;int safety=0;
  while(loot.FieldRows.Count>0&&safety++<40){await Tap(loot.FieldRows[0].Button);if(!loot.Transfer.IsInteractable())await Tap(loot.Cards[1].Button);await Tap(loot.Max);await Tap(loot.Transfer);}
  Check(loot.FieldRows.Count==0,"Loot did not fit");
  // Then only the guide's next press to the return report (doors by gathering, the corridor crate co-op, the missing person's story).
  var story=C.MissingPerson;var trace=new List<string>();
  for(int i=0;i<120&&!C.ReturnPanel.IsOpen;i++){
   await Task.Delay(90);
   if(a.IsOpen&&a.InTransit){for(int w=0;w<80&&a.InTransit;w++)await Task.Delay(100);continue;}
   Check(!(a.IsOpen&&a.Encounter.IsOpen),"The guided route met something: "+string.Join(" > ",trace.TakeLast(8)));
   if(story&&story.IsOpen){trace.Add("story");await Tap(story.Next);continue;}
   Check(G.Target,"No next target: "+G.Guidance+" after "+string.Join(" > ",trace.TakeLast(8)));
   string k=a.IsOpen?Kind(a,G.Target):G.Target.name;trace.Add(k);
   if(a.IsOpen&&k=="turn"){bool move=a.Rooms.HasQueuedMove;int before=a.Rooms.Turns,cost=move?a.Rooms.QueuedTurns:1;await Tap(G.Target);if(move)for(int w=0;w<80&&a.InTransit;w++)await Task.Delay(100);Check(a.Rooms.Turns==before+cost,"'턴 진행' = "+cost+" turn(s)");continue;}
   await Tap(G.Target);
  }
  Check(C.ReturnPanel.IsOpen,"No return summary: "+string.Join(" > ",trace.TakeLast(10)));await Next();await Next();
  Check(C.Opening.State.FirstReturn,"First return not recorded");await Shot("10-return-goal");
  return "PASS first expedition by the guide only: pawn → silhouette → second pawn → co-op silhouette → '턴 진행' (one turn), discovered items, actual loot transfer, doors by gathering, return and next restoration goal ("+trace.Count+" more presses).";
 }
 public static async Task<string> Progression(){
  var c=C;Check(!c.CraftPanel.IsOpen&&!c.InventoryPanel.IsOpen,"Close panels first");
  // Backup state; shortage and recruitment probes must not become real saved progress.
  var saved=CampaignPersistence.Capture(c);
  c.Opening.State.FirstReturn=true;c.Development.State.Warehouse=0;
  foreach(var m in c.CraftPanel.Materials)m.Initial=0;
  foreach(var person in c.Campaign.Party)foreach(var item in c.InventoryPanel.Items){int n=c.InventoryPanel.CountFor(person,item.Id);if(n>0)c.InventoryPanel.MoveFor(person,item.Id,n,false);}
  foreach(var m in c.CraftPanel.Materials)m.Initial=0;
  c.Opening.Evaluate();Check(c.Opening.ActionText=="부족한 재료 수색","Shortage points at unusable craft button");
  CampaignPersistence.Prepare(saved,c);SceneManager.LoadScene("Settlement");await Task.Delay(500);c=C;
  c.Campaign.AdvanceSettlementTime(Math.Max(0,720-c.Campaign.MinuteOfDay));c.VisitorPanel.Sync();Check(c.VisitorPanel.RecruitId=="mechanic","Hidden candidate not available as visitor");
  c.VisitorPanel.Open();await Task.Delay(250);await Tap(c.VisitorPanel.InspectRecruit);await Tap(c.VisitorPanel.RecruitAction);await Tap(c.VisitorPanel.Confirm);c.VisitorPanel.Close();Check(PartySelectionSession.Selected.Contains("mechanic"),"Hidden candidate could not join in game");
  var joined=CampaignPersistence.Capture(c);CampaignPersistence.Prepare(joined,c);SceneManager.LoadScene("Settlement");await Task.Delay(500);
  Check(PartySelectionSession.Selected.Contains("mechanic")&&C.Campaign.Party.Count()==3,"Joined hidden character lost on reload");
  C.Opening.State.Complete=true;C.Introduction.Restore(10);await Task.Delay(200);Check(!G.Banner.activeSelf&&!G.Target,"Completed tutorial reappeared");
  CampaignPersistence.Prepare(saved,C);SceneManager.LoadScene("Settlement");await Task.Delay(500);
  return "PASS missing materials route to exploration; hidden mechanic still visits/joins and survives reload; completed tutorial hides; pre-probe progress restored.";
 }
 public static async Task<string> Restoration(){
  var trace=new List<string>();
  for(int i=0;i<65&&!C.Opening.State.ClueRead;i++){
   await Task.Delay(80);trace.Add(G.Guidance);
   Check(G.Target,"No restoration target after "+string.Join(" > ",trace.TakeLast(8)));
   await Next();
   if(C.CraftPanel.IsOpen&&G.Target==C.CraftPanel.Confirm)await Shot("11-restoration-confirm");
  }
  Check(C.Development.State.Warehouse==1&&C.Development.State.Workbench&&C.Opening.State.ClueRead,"Guide restoration loop: "+string.Join(" > ",trace.TakeLast(12)));
  return "PASS first-return inventory overflow -> enough materials -> warehouse -> workbench -> clue, using only the guided next button ("+trace.Count+" clicks).";
 }
 public static async Task<string> PrepareTool(){
  var trace=new List<string>();
  for(int i=0;i<100&&!C.PackingPanel.IsOpen;i++){
   await Task.Delay(80);trace.Add(G.Guidance);Check(G.Target,"No tool preparation target: "+string.Join(" > ",trace.TakeLast(10)));await Next();
  }
  Check(C.PackingPanel.IsOpen&&C.Campaign.Party.Any(p=>C.InventoryPanel.CountFor(p,"prybar")>0),"Tool preparation loop: "+string.Join(" > ",trace.TakeLast(15)));
  await Shot("13-tool-packed");return "PASS management note -> rope/nails/prybar crafting -> carry actual tool -> second-trip preparation, using only guided clicks ("+trace.Count+").";
 }
 public static string Snapshot(){if(C.InventoryPanel.IsOpen)C.InventoryPanel.Close();Directory.CreateDirectory("Temp/TutorialClaritySave");File.WriteAllText("Temp/TutorialClaritySave/resume.json",JsonUtility.ToJson(CampaignPersistence.Capture(C)));return "Saved test fixture only.";}
 public static async Task<string> Resume(){SceneManager.LoadScene("Settlement");await Task.Delay(400);var saved=JsonUtility.FromJson<CampaignSaveData>(File.ReadAllText("Temp/TutorialClaritySave/resume.json"));CampaignPersistence.Prepare(saved,C);SceneManager.LoadScene("Settlement");await Task.Delay(500);return "Restored test fixture; "+G.Guidance;}
 public static async Task<string> InventoryFrame(){if(C&&C.Opening.IsOpen)C.Opening.Close();await Resume();await Task.Delay(150);await Next();Check(C.InventoryPanel.IsOpen,"Expected inventory fixture");await Shot("12-needed-material");return "Captured needed-material guidance.";}
}
