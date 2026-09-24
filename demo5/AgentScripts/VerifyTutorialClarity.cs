using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Threading.Tasks;using UnityEngine;using UnityEngine.UI;using UnityEngine.SceneManagement;using UnityEngine.EventSystems;using Demo5.FrontEnd;using Demo5.NightRun;using Object=UnityEngine.Object;
public static class VerifyTutorialClarity {
 static SettlementController C=>Object.FindAnyObjectByType<SettlementController>();
 static SettlementTutorialGuide G=>C?C.GetComponent<SettlementTutorialGuide>():null;
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static async Task Tap(Button b){Check(b&&b.IsActive()&&b.IsInteractable(),"Unavailable button "+b?.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var cam=b.GetComponentInParent<Canvas>().worldCamera;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(cam,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);
  if(b.GetComponent<SettlementFacilityFocus>()){for(int y=1;y<10&&(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=b);y++)for(int x=1;x<10;x++){e.position=RectTransformUtility.WorldToScreenPoint(cam,r.TransformPoint(new Vector2(r.rect.xMin+r.rect.width*x/10,r.rect.yMin+r.rect.height*y/10)));hits.Clear();EventSystem.current.RaycastAll(e,hits);if(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b)break;}}
  Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+hits.FirstOrDefault().gameObject?.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(250);
 }
 static async Task Next(){await Task.Delay(60);Check(G.Target,"No next target: "+G.Guidance);await Tap(G.Target);}
 static async Task Shot(string name){Directory.CreateDirectory("Assets/Screenshots/TutorialClarity");ScreenCapture.CaptureScreenshot("Assets/Screenshots/TutorialClarity/"+name+".png");await Task.Delay(400);if(G&&G.Banner.activeSelf){Check(G.Title.preferredHeight<=G.Title.rectTransform.rect.height+1,"Guide title clipped");Check(G.Instruction.preferredHeight<=G.Instruction.rectTransform.rect.height+1,"Guide text clipped: "+G.Instruction.text);}}
 public static async Task<string> Start(){
  PartySelectionSession.Clear();SceneManager.LoadScene("PartySelection");await Task.Delay(500);
  var p=Object.FindAnyObjectByType<PartySelectionController>();Check(p.Cards.Count(c=>c.gameObject.activeSelf)==2,"Roster is not restricted");Check(PartySelectionSession.Selected.SequenceEqual(new[]{"scout","medic"}),"Wrong opening pair");
  p.Toggle("mechanic");p.Preview("guard");Check(PartySelectionSession.Selected.SequenceEqual(new[]{"scout","medic"})&&p.FocusedCandidateId=="scout","Hidden starter bypass");
  Check(!p.Previous.gameObject.activeSelf&&!p.NextPage.gameObject.activeSelf,"Unnecessary pagination");
  await Tap(p.Cards[1].Button);Check(p.FocusedCandidateId=="medic"&&p.SelectedCount==2,"Click must preview without removing mandatory pair");await Shot("01-starting-pair");
  await Tap(p.Continue);var h=Object.FindAnyObjectByType<HomeSelectionController>();await Tap(h.Cards[1].Button);await Tap(h.Continue);await Task.Delay(400);
  Check(C.Introduction.Step==0&&G.Target==C.Introduction.Action,"First action missing");await Shot("02-first-action");
  await Next();await Next();Check(C.Introduction.Step==2,"Initial actions did not progress");
  var save=CampaignPersistence.Capture(C);CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/TutorialClaritySave");try{Check(CampaignSaveStore.Write(0,save,C,out var error),error);var read=CampaignSaveStore.Read(0,C);Check(read.CanLoad,read.Error);CampaignPersistence.Prepare(read.Data,C);}finally{CampaignSaveStore.TestDirectory=null;}
  // Wait for the reloaded scene instead of a fixed delay (a busy editor can take longer than 0.5 s to load and run the guide once).
  SceneManager.LoadScene("Settlement");await Task.Delay(300);for(int i=0;i<60&&!(C&&C.Introduction&&C.Introduction.Step==2&&G&&G.Target==C.Introduction.Action);i++)await Task.Delay(100);Check(C.Introduction.Step==2&&G.Target==C.Introduction.Action,"Reload lost tutorial step");
  await Next();Check(C.Introduction.Step==3&&G.Target==C.Bed,"Rest entry not highlighted");Check(!C.Workbench.gameObject.activeSelf&&!C.Stock.gameObject.activeSelf&&!C.Advance.gameObject.activeSelf,"Unlearned facilities shown");await Shot("03-rest-target");
  await Next();Check(C.WorkPanel.IsOpen&&G.Target==C.WorkPanel.Rows[0].Button,"Rest worker target");await Shot("04-rest-worker");
  await Next();Check(G.Target==C.WorkPanel.Confirm,"Rest confirm target");await Next();Check(C.WorkPanel.Orders.Count==1&&G.Target==C.Advance,"After reservation must guide time");await Shot("05-time-target");
  await Next();Check(G.Target==C.TimePanel.Choices[3],"Next completion target");await Next();Check(G.Target==C.TimePanel.Confirm,"Time confirmation target");await Shot("06-time-confirm");await Next();Check(C.Introduction.Step==4&&G.Target==C.TimePanel.CloseButton,"Completion guide");await Next();
  await Next();Check(C.InventoryPanel.IsOpen&&G.Target==C.InventoryPanel.CloseButton,"Stock lesson");await Next();Check(C.Introduction.Step==5&&G.Target==C.Introduction.Action,"First expedition goal");
  await Next();for(int i=0;i<6&&!C.PackingPanel.IsOpen;i++)await Next();Check(C.PackingPanel.IsOpen&&C.ExpeditionPanel.Selected.Count==2,"Both survivors must be selected");await Shot("07-packing");
  return "PASS only scout/medic start, hidden candidate cannot be selected/previewed, 2-card preview, real scene transitions, disk save/load step 2, one-next-click flow through rest/time/storage/expedition, both survivors selected. Paused at packing.";
 }
 public static async Task<string> FirstTrip(){
  Check(C.PackingPanel.IsOpen,"Start test first");await Next();await Next();for(int i=0;i<60&&C.ArrivalPanel.InTransit;i++)await Task.Delay(100);await Task.Delay(200);
  Check(G.Target==C.ArrivalPanel.Objects[0],"First search target");await Next();await Shot("08-search-worker");
  await Next();Check(G.Target==C.ArrivalPanel.Search.Choose,"Search proceed");
  for(int i=0;i<20&&!C.ArrivalPanel.Loot.IsOpen;i++){
   var a=C.ArrivalPanel;
   if(a.Encounter.IsOpen){await Tap(a.Encounter.Wait);await Tap(a.Encounter.Confirm);}
   else if(a.Search.IsOpen)await Next();else await Tap(a.Objects[0]);
  }
  Check(C.ArrivalPanel.Loot.IsOpen,"Search did not reach loot");await Shot("09-found-items");
  var loot=C.ArrivalPanel.Loot;int safety=0;
  while(loot.FieldRows.Count>0&&safety++<40){await Tap(loot.FieldRows[0].Button);if(!loot.Transfer.IsInteractable())await Tap(loot.Cards[1].Button);await Tap(loot.Max);await Tap(loot.Transfer);}
  Check(loot.FieldRows.Count==0,"Loot did not fit");await Next();await Next();await Next();Check(C.ReturnPanel.IsOpen,"No return summary");await Next();await Next();
  Check(C.Opening.State.FirstReturn,"First return not recorded");await Shot("10-return-goal");
  return "PASS first expedition next-click guide, search assignment/turn confirmation, discovered items, actual loot transfer, return and next restoration goal.";
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
