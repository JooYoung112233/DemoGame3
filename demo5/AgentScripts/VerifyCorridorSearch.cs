using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Demo5.FrontEnd;
// 말 놓기 (2026-09-25, 기획/탐험-말놓기-조작-재설계.md): searches are members' pawns on the object and '턴 진행' (FieldPawnTest); the move is
// every pawn at the door, then '턴 진행' (a second press in the same moment costs nothing); a missing tool greys out the object's place
// ('{도구} 필요'); the 07 window and a finished object's finds open with a right press (FieldPawnTest.Detail).
public static class VerifyCorridorSearch
{
 static SettlementController C()=>UnityEngine.Object.FindAnyObjectByType<SettlementController>();
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static async Task Tap(Button b){await Task.Delay(60);Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(90);}
 static async Task Wait(){for(int i=0;i<60&&C().ArrivalPanel.InTransit;i++)await Task.Delay(100);Check(!C().ArrivalPanel.InTransit,"Transit timeout");}
 // Best-fit texts (the member cards' action slot) shrink to their box: only fixed-size texts can overflow.
 static void Bounds(GameObject g){Canvas.ForceUpdateCanvases();foreach(var t in g.GetComponentsInChildren<Text>())if(!t.resizeTextForBestFit)Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Overflow "+t.name+": "+t.text);}
 static async Task Begin(){var c=C();Check(c.ArrivalPanel.Begin(c.Campaign.Party.ToArray(),c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")),"Begin");await Wait();}
 static async Task Ready(){var a=C().ArrivalPanel;for(int i=0;i<20&&!a.Popup.activeSelf&&!FieldPawnTest.Ready(a);i++)await Task.Delay(50);if(a.Popup.activeSelf)a.ClosePopup();/* the second visit's place-board intro */for(int i=0;i<60&&!FieldPawnTest.Ready(a);i++)await Task.Delay(50);Check(FieldPawnTest.Ready(a),"Board not ready");}
 static async Task Move(){var a=C().ArrivalPanel;await Ready();int turn=a.Rooms.Turns;int minute=C().Campaign.MinuteOfDay;Check(FieldPawnTest.Gather(a,a.Rooms.CurrentRoom==0?1:0),"Everyone at the door: "+FieldPawnTest.Describe(a));var turnButton=a.Threat.Planner.TurnButton;await Tap(turnButton);turnButton.onClick.Invoke();await Wait();Check(a.Rooms.Turns==turn+1&&C().Campaign.MinuteOfDay==minute+10,"Move charged twice");}
 // One '턴 진행' with the pawns where they stand.
 static async Task Step(){await Ready();await Task.Delay(250);await Tap(C().ArrivalPanel.Threat.Planner.TurnButton);await Task.Delay(200);}
 static FieldPlaceOption Lead(int m,int site)=>FieldPawnTest.Option(C().ArrivalPanel,m,FieldPawnTest.SearchKey(site),FieldSpotKind.Lead);
 public static async Task<string> Flow(){
  FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c=C();var a=c.ArrivalPanel;var l=a.Loot;
  Check(l.Sites.Length>=6&&l.Sites[3].Room==-1,"Door became searchable");
  var old=CampaignPersistence.Capture(c);CampaignPersistence.Upgrade(old,c);Check(old.Version==CampaignPersistence.CurrentVersion&&old.Searches.Length==0,"Existing v7 incompatible");
  foreach(var site in l.Sites){Check(site.Drops.All(d=>c.InventoryPanel.Items.Any(i=>i.Id==d.Id)),"Unknown drop");Check(string.IsNullOrEmpty(site.RequiredTool)||c.InventoryPanel.Items.Any(i=>i.Id==site.RequiredTool),"Unknown tool");}
  await Begin();a.Encounter.NoiseThreshold=int.MaxValue;
  Check(!a.Objects[4].gameObject.activeSelf&&!l.Advance(4,0,a.Participants[0]),"Remote corridor search");a.Inspect(4);Check(!a.Search.IsOpen&&l.ExportSearches().Length==0,"Remote inspection");
  int time=c.Campaign.MinuteOfDay;await Ready();FieldPawnTest.Board(a).ShowDoorLog(1);await Tap(a.PopupBack);Check(time==c.Campaign.MinuteOfDay,"Looking at the door costs time");await Move();
  Check(a.Objects[4].gameObject.activeSelf&&a.Objects[5].gameObject.activeSelf&&!a.Objects[0].gameObject.activeSelf&&!a.Return.interactable,"Room visibility");Check(!l.Advance(0,0,a.Participants[0]),"Remote arcade search");
  await Ready();{var o=Lead(0,4);Check(o!=null&&!o.Enabled&&o.Blocked==string.Format(a.Threat.Planner.PlaceTexts.NeedTool,a.Threat.Planner.ToolName(4))&&!FieldPawnTest.Lead(a,0,4),"Missing tool allowed");}Check(FieldPawnTest.Detail(a,4)&&a.Search.ReadOnly,"07 read only");Bounds(a.Search.View);await Tap(a.Search.Back);
  Check(c.InventoryPanel.TransferField(a.Participants[0],"prybar",1,true),"Tool fixture");foreach(var d in l.Sites[4].Drops)d.Chance=100;
  await Ready();Check(FieldPawnTest.Coop(a,4,1,0),"Two pawns on the power box, 망보기: "+FieldPawnTest.Describe(a));/* 망보기 on the noisy power box: 2 turns */await Step();Check(l.State(4).Progress==1&&l.State(4).Opened&&l.State(4).Duty==1,"Partial open");
  Check(c.InventoryPanel.CountFor(a.Participants[0],"prybar")==1,"Tool consumed");Check(c.InventoryPanel.TransferField(a.Participants[0],"prybar",1,false),"Remove tool fixture");await Move();await Move();await Ready();Check(Lead(0,4)?.Enabled==true&&FieldPawnTest.Lead(a,0,4),"Opened lid needs tool again");await Step();for(int w=0;w<60&&!l.IsOpen;w++)await Task.Delay(50);/* the finds open after the turn sequence */Check(l.IsOpen&&l.State(4).Complete,"Loot not opened");
  while(l.FieldRows.Count>0){await Tap(l.FieldRows[0].Button);await Tap(l.Max);await Tap(l.Transfer);}await Tap(l.Back);Check(l.StatusKind(4)==3&&!l.Advance(4,0,a.Participants[0]),"Depleted reroll");
  int crateTurns=l.Sites[5].Turns;l.Sites[5].Turns=3;/* 망보기 on a 3-turn fixture: the old 정밀 3 turns */await Ready();Check(FieldPawnTest.Coop(a,5,1),"Two pawns on the crate, 망보기");await Step();l.Sites[5].Turns=crateTurns;Check(l.State(5).Progress==1&&!l.State(5).Complete&&l.State(5).Required==3,"Crate partial");FieldPawnTest.Clear(a);Check(FieldPawnTest.Detail(a,5),"07 of the crate");Bounds(a.Search.View);await Tap(a.Search.Back);Bounds(a.Main.gameObject);
  return "PASS: room guards, cancelled/duplicate movement, missing tool, nonconsuming lid opening, resume after tool removal and room change, pickup/depletion/no reroll, partial crate, catalog IDs and v7 compatibility.";
 }
 public static async Task<string> Persistence(){
  FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c=C();await Move();var a=c.ArrivalPanel;await Tap(a.Return);await Tap(a.ReturnConfirm);await Tap(c.ReturnPanel.Back);
  CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/CorridorSearchVerification");Check(CampaignSaveStore.Write(0,CampaignPersistence.Capture(c),c,out var error),error);var disk=CampaignSaveStore.Read(0,c);Check(disk.CanLoad,disk.Error);CampaignPersistence.Prepare(disk.Data,c);SceneManager.LoadScene("Settlement");await Task.Delay(700);c=C();a=c.ArrivalPanel;
  Check(a.Loot.StatusKind(4)==3&&a.Loot.State(5).Progress==1&&a.Rooms.Inspected.Contains(4)&&a.Rooms.Inspected.Contains(5),"Saved state");Check(a.Rooms.CorridorVisited&&!a.Rooms.StorageVisited&&a.Threat&&!a.Rooms.Inspected.Contains(a.Threat.DenSite),"Fixture should know arcade/corridor but not storage/office shelf");int knownSites=a.Loot.Sites.Select((s,i)=>new{s,i}).Count(x=>(x.s.Room==0||x.s.Room==1)&&x.i!=a.Threat.DenSite);Check(a.Loot.SearchSummary()=="미수색 "+(knownSites-2)+" · 진행 1\n물품 남음 0 · 비어 있음 1","Summary must exclude the door, unvisited storage and hidden office shelf");await Begin();await Move();Check(a.Loot.StatusKind(4)==3&&a.Loot.State(5).Progress==1,"Revisit reset");
  // 전략 1차: a later visit has no random roll; the meeting is 'it' on the site board (ExpeditionSiteThreat) stepping into the party's room.
  // Fixture: after one search turn it wakes at 위험도 2 remembering the corridor (a loud noise heard there), so it leaves the den on the
  // crate's last search turn; 망보기 keeps that turn silent (the crate's noise 1 − 1) so the gauge does not tip it into the hunt.
  var e=a.Encounter;var t=a.Threat;
  await Ready();Check(FieldPawnTest.Lead(a,0,5),"A pawn on the running crate");await Step();Check(!e.IsOpen&&a.Loot.State(5).Progress==2&&!a.Loot.State(5).Complete,"Crate one turn short: "+a.Loot.State(5).Progress);
  t.ReviewWake(2,0,t.State.TurnsUsed);t.HearBattle(t.Rules.LoudNoise);Check(t.Active&&t.State.Remembered==FieldSiteState.Corridor&&t.State.Next==FieldSiteState.Corridor&&t.State.Incoming,"It heads for the corridor next turn");
  await Ready();Check(FieldPawnTest.Coop(a,5,1),"Two pawns on the crate, 망보기: "+FieldPawnTest.Describe(a));await Step();for(int w=0;w<40&&!e.IsOpen;w++)await Task.Delay(50);
  Check(e.IsOpen&&t.State.Encounter&&t.State.ResidentRoom==FieldSiteState.Corridor&&a.Loot.State(5).Complete,"Corridor meeting on the crate's last search turn");if(a.Loot.IsOpen)await Tap(a.Loot.Back);
  await Tap(e.Retreat);Check(e.ReviewBody.text.StartsWith("오락실로"),"Wrong retreat destination");await Tap(e.Confirm);await Wait();Check(a.Rooms.CurrentRoom==0&&a.Loot.State(5).Complete,"Retreat lost search progress");
  // It stays in the corridor a while, then heads home: hush in the arcade until it is back in the den, then walk back in.
  for(int i=0;i<8&&t.State.ResidentRoom!=FieldSiteState.Den;i++){FieldPawnTest.Clear(a);await Step();Check(!e.IsOpen,"Met while hushing in the arcade");}
  Check(t.State.ResidentRoom==FieldSiteState.Den,"It went home: "+t.State.ResidentRoom);await Move();Check(!e.IsOpen&&a.Loot.State(5).Complete,"Return lost loot");CampaignSaveStore.TestDirectory=null;Bounds(a.Main.gameObject);return "PASS: isolated file restore, revisit, inspection IDs, five-site summary, the site board's corridor meeting retreats to the arcade and completed rewards are preserved.";
 }
 public static async Task<string> SearchPreview(){FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var a=C().ArrivalPanel;await Ready();Check(FieldPawnTest.Detail(a,4)&&a.Loot.IsOpen,"A right press opens the finished box's finds");Bounds(a.Loot.View);return "Depleted loot preview";}
 public static async Task<string> LegacyPreview(){
  FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c=C();CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/SearchStatusVerification");var disk=CampaignSaveStore.Read(0,c);Check(disk.CanLoad,disk.Error);Check(disk.Data.Searches.All(s=>!s.Id.StartsWith("mall.corridor")),"Need pre-corridor fixture");CampaignPersistence.Prepare(disk.Data,c);SceneManager.LoadScene("Settlement");await Task.Delay(700);c=C();var a=c.ArrivalPanel;Check(a.Loot.StatusKind(0)==3&&a.Loot.State(1).Progress==1&&a.Loot.StatusKind(4)==0&&a.Loot.StatusKind(5)==0,"Legacy migration changed old state");await Begin();await Move();await Ready();Check(Lead(0,4)?.Enabled==false,"Legacy missing tool");Check(FieldPawnTest.Detail(a,4),"07 of the power box");Bounds(a.Search.View);CampaignSaveStore.TestDirectory=null;return "PASS: existing pre-expansion v7 file preserves old depletion/progress and initializes new corridor sites unsearched.";
 }
}
