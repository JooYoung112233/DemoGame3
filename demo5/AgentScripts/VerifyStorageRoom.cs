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
// 말 놓기 (2026-09-25, 기획/탐험-말놓기-조작-재설계.md): every pawn at a door is the move on the next '턴 진행' (a second press in the same moment
// costs nothing); the locked storage door (option A) needs the prybar in someone's bag: without it the door's place is greyed out ('{도구}
// 필요'), with it everyone gathered is '문 따고 보관실로 · 2턴'; searches are pawns on the object and '턴 진행' (FieldPawnTest).
public static class VerifyStorageRoom
{
 static SettlementController C()=>UnityEngine.Object.FindAnyObjectByType<SettlementController>();
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static async Task Tap(Button b){await Task.Delay(60);Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(90);}
 static async Task Wait(){for(int i=0;i<60&&C().ArrivalPanel.InTransit;i++)await Task.Delay(100);Check(!C().ArrivalPanel.InTransit,"Transit timeout");}
 // Best-fit texts (the member cards' action slot) shrink to their box: only fixed-size texts can overflow.
 static void Bounds(GameObject g){Canvas.ForceUpdateCanvases();foreach(var t in g.GetComponentsInChildren<Text>())if(!t.resizeTextForBestFit)Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Overflow "+t.name+": "+t.text);}
 static async Task Begin(){var c=C();Check(c.ArrivalPanel.Begin(c.Campaign.Party.ToArray(),c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")),"Begin");await Wait();}
 static async Task Ready(){var a=C().ArrivalPanel;for(int i=0;i<20&&!a.Popup.activeSelf&&!FieldPawnTest.Ready(a);i++)await Task.Delay(50);if(a.Popup.activeSelf)a.ClosePopup();/* the second visit's place-board intro */for(int i=0;i<60&&!FieldPawnTest.Ready(a);i++)await Task.Delay(50);Check(FieldPawnTest.Ready(a),"Board not ready");}
 // Everyone at the door onto `next`, then '턴 진행' twice in one moment (one turn).
 static async Task Go(int next){var a=C().ArrivalPanel;await Ready();Check(FieldPawnTest.Gather(a,next),"Everyone at the door to "+next+": "+FieldPawnTest.Describe(a));var turn=a.Threat.Planner.TurnButton;await Tap(turn);turn.onClick.Invoke();await Wait();}
 static async Task Move(){var a=C().ArrivalPanel;int turn=a.Rooms.Turns;int minute=C().Campaign.MinuteOfDay;await Go(a.Rooms.CurrentRoom==0?1:0);Check(a.Rooms.Turns==turn+1&&C().Campaign.MinuteOfDay==minute+10,"Move charged twice");}
 // One '턴 진행' with the pawns where they stand.
 static async Task Step(){await Ready();await Task.Delay(250);await Tap(C().ArrivalPanel.Threat.Planner.TurnButton);await Task.Delay(200);}
 static async Task BackRoom(){var a=C().ArrivalPanel;await Go(a.Rooms.CurrentRoom==2?1:0);}
 static async Task EnterStorage(){var a=C().ArrivalPanel;int before=C().Campaign.MinuteOfDay;await Go(2);Check(a.Rooms.CurrentRoom==2&&C().Campaign.MinuteOfDay==before+10,"Reentry cost/destination");}
 public static async Task<string> Flow(){
  FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c=C();var a=c.ArrivalPanel;var n=a.Rooms;var l=a.Loot;Check(n.Storage&&l.Sites.Length==9,"Room setup");await Begin();await Move();a.Encounter.NoiseThreshold=int.MaxValue;
  int time=c.Campaign.MinuteOfDay,turn=n.Turns;var pl=a.Threat.Planner;var board=FieldPawnTest.Board(a);await Ready();
  {var o=FieldPawnTest.Option(a,0,FieldPawnTest.DoorKey(2),FieldSpotKind.Gather);Check(o!=null&&!o.Enabled&&o.Blocked==string.Format(pl.PlaceTexts.NeedTool,n.UnlockToolName)&&!FieldPawnTest.Gather(a,2)&&!n.StorageUnlocked,"Missing tool allowed");}
  board.ShowDoorLog(2);Check(a.Popup.activeSelf&&!a.ReturnConfirm.gameObject.activeSelf,"The locked door's note (right press)");Bounds(a.Popup);await Tap(a.PopupBack);Check(c.Campaign.MinuteOfDay==time,"Missing tool charged");
  Check(c.InventoryPanel.TransferField(a.Participants[0],"prybar",1,true),"Give tool");await Ready();Check(FieldPawnTest.Place(a,0,FieldPawnTest.DoorKey(2),FieldSpotKind.Gather)&&!n.HasQueuedMove,"One pawn at the unlocked-by-tool door, not a move yet");
  Check(FieldPawnTest.Unassign(a,0)&&!n.StorageUnlocked&&time==c.Campaign.MinuteOfDay,"Cancel changed lock/time");
  Check(FieldPawnTest.Gather(a,2)&&n.HasQueuedMove&&n.QueuedTurns==1+n.UnlockTurns,"Everyone at the locked door: '문 따고 보관실로'");
  Check(c.InventoryPanel.TransferField(a.Participants[0],"prybar",1,false),"Remove tool before the turn");await Task.Delay(100);
  Check(!n.HasQueuedMove&&!n.StorageUnlocked&&time==c.Campaign.MinuteOfDay&&!a.InTransit&&FieldPawnTest.ActionOf(a,0)==FieldAction.Paused,"Stale tool accepted (the gathered pawns must wait: '문 못 엶')");
  Check(c.InventoryPanel.TransferField(a.Participants[0],"prybar",1,true),"Restore tool");await Task.Delay(100);Check(n.HasQueuedMove,"The move is back with the tool");
  {var turnButton=pl.TurnButton;await Tap(turnButton);turnButton.onClick.Invoke();await Wait();}Check(n.CurrentRoom==2&&n.StorageUnlocked&&n.StorageVisited&&n.Turns==turn+2&&c.Campaign.MinuteOfDay==time+20&&n.Noise==n.UnlockNoise&&n.UnlockNoise==3,"Unlock costs/state (the lock: noise 3)");Check(c.InventoryPanel.CountFor(a.Participants[0],"prybar")==1,"Tool consumed");
  Check(n.Background.sprite==n.Storage&&n.StorageHotspots.activeSelf&&a.Objects[6].gameObject.activeSelf&&!a.Objects[4].gameObject.activeSelf&&!a.Return.interactable,"Storage visibility");Check(!l.Advance(4,0,a.Participants[0]),"Remote corridor search");Bounds(a.Main.gameObject);
  Check(c.InventoryPanel.TransferField(a.Participants[0],"prybar",1,false),"Remove after unlocking");await BackRoom();await EnterStorage();Check(n.Noise==n.UnlockNoise,"Reentry unlock noise");
  int shelfTurns=l.Sites[6].Turns;l.Sites[6].Turns=4;/* 함께 on a 4-turn fixture: 3 turns (the old 정밀) */await Ready();Check(FieldPawnTest.Coop(a,6),"Two pawns on the shelf");await Step();l.Sites[6].Turns=shelfTurns;Check(l.State(6).Progress==1&&l.State(6).Required==3,"Partial shelf");FieldPawnTest.Clear(a);Check(FieldPawnTest.Detail(a,6),"07 of the shelf");Bounds(a.Search.View);await Tap(a.Search.Back);
  foreach(var drop in l.Sites[7].Drops)drop.Chance=100;
  await Ready();Check(FieldPawnTest.Coop(a,7),"Two pawns on the materials");/* 함께: one turn */await Step();Check(l.IsOpen,"Materials not completed");while(l.FieldRows.Count>0){await Tap(l.FieldRows[0].Button);for(int k=0;!l.Transfer.interactable&&k<l.Cards.Count;k++)await Tap(l.Cards[k].Button);await Tap(l.Max);await Tap(l.Transfer);}await Tap(l.Back);Check(l.StatusKind(7)==3&&!l.Advance(7,0,a.Participants[0]),"Materials reroll"); // 4 item kinds: switch to a member with a free slot
  Bounds(a.Main.gameObject);return "PASS: missing/stale tool, cancel, unlock charge once, tool not consumed, two-way travel without tool, room visibility, partial shelf, materials pickup/depletion.";
 }
 public static async Task<string> Persistence(){
  FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c=C();await BackRoom();await BackRoom();var a=c.ArrivalPanel;await Tap(a.Return);await Tap(a.ReturnConfirm);await Tap(c.ReturnPanel.Back);
  CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/StorageRoomVerification");Check(CampaignSaveStore.Write(0,CampaignPersistence.Capture(c),c,out var error),error);var saved=CampaignSaveStore.Read(0,c);Check(saved.CanLoad&&saved.Data.Version==CampaignPersistence.CurrentVersion,saved.Error);CampaignPersistence.Prepare(saved.Data,c);SceneManager.LoadScene("Settlement");await Task.Delay(700);c=C();a=c.ArrivalPanel;
  Check(a.Rooms.StorageUnlocked&&a.Rooms.StorageVisited&&a.Loot.State(6).Progress==1&&a.Loot.StatusKind(7)==3,"v8 restore");await Begin();await Move();await EnterStorage();
  // 전략 1차: a later visit has no random roll; the meeting is 'it' on the site board (ExpeditionSiteThreat) stepping into the party's room.
  // Fixture: it wakes at 위험도 2 remembering the storage room (a loud noise heard there). At 위험도 2 it stops to listen after each step,
  // so den → corridor → storage takes three turns: two finish the shelf, and on the third a member listens at the door (귀 대기 is not a
  // hush, so it does not pass by) as it walks in.
  var e=a.Encounter;var t=a.Threat;var l=a.Loot;t.ReviewWake(2,0,t.State.TurnsUsed);t.HearBattle(t.Rules.LoudNoise);Check(t.Active&&t.State.Remembered==FieldSiteState.Storage&&t.State.Next==FieldSiteState.Corridor,"It heads for the storage room");
  await Ready();Check(FieldPawnTest.Lead(a,1,6),"The second member's pawn on the shelf");await Step();await Step();Check(!e.IsOpen&&l.State(6).Complete&&t.State.ResidentRoom==FieldSiteState.Corridor&&t.State.Incoming,"Shelf done, it stands next door");
  for(int w=0;w<40&&!l.IsOpen;w++)await Task.Delay(50);if(l.IsOpen){await Tap(l.Back);if(l.LeaveReview.activeSelf)await Tap(l.LeaveConfirm);}
  await Ready();Check(FieldPawnTest.Listen(a,0,FieldSiteState.Corridor),"A pawn listens at the corridor door: "+FieldPawnTest.Describe(a));await Step();for(int w=0;w<40&&!e.IsOpen;w++)await Task.Delay(50);
  Check(e.IsOpen&&t.State.Encounter&&t.State.ResidentRoom==FieldSiteState.Storage,"Storage meeting");await Tap(e.Retreat);Check(e.ReviewBody.text.StartsWith("복도로"),"Wrong retreat destination");await Tap(e.Confirm);await Wait();Check(a.Rooms.CurrentRoom==1&&l.State(6).Complete,"Storage retreat");
  // It stays in the storage room a while, then goes home through the corridor: hushed, it passes by. Walk back in once it is in the den.
  for(int i=0;i<8&&t.State.ResidentRoom!=FieldSiteState.Den;i++){FieldPawnTest.Clear(a);await Step();Check(!e.IsOpen,"Met while hushing in the corridor");}
  Check(t.State.ResidentRoom==FieldSiteState.Den,"It went home: "+t.State.ResidentRoom);await EnterStorage();Check(!e.IsOpen&&l.State(6).Complete,"Reward lost after retreat");CampaignSaveStore.TestDirectory=null;return "PASS: v8 file restore, unlocked revisit without tool, saved partial/depleted state, the site board's storage meeting retreats to the corridor, completed loot preserved.";
 }
 public static async Task<string> Legacy(){
  FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c=C();CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/CorridorSearchVerification");var saved=CampaignSaveStore.Read(0,c);Check(saved.CanLoad,saved.Error);Check(saved.Data.Version==CampaignPersistence.CurrentVersion&&!saved.Data.StorageUnlocked&&!saved.Data.StorageVisited,"v7 migration");CampaignPersistence.Prepare(saved.Data,c);SceneManager.LoadScene("Settlement");await Task.Delay(700);c=C();Check(c.ArrivalPanel.Loot.StatusKind(4)==3&&c.ArrivalPanel.Loot.State(5).Progress==1&&c.ArrivalPanel.Loot.StatusKind(6)==0,"Old site state changed");CampaignSaveStore.TestDirectory=null;return "PASS: actual pre-storage v7 file migrates to v8, old corridor states preserved, storage locked and unsearched.";
 }
 public static async Task<string> FinalPreview(){
  FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var catalog=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab").GetComponent<SettlementController>();CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/StorageRoomVerification");var saved=CampaignSaveStore.Read(0,catalog);Check(saved.CanLoad,saved.Error);CampaignPersistence.Prepare(saved.Data,catalog);SceneManager.LoadScene("Settlement");await Task.Delay(700);var a=C().ArrivalPanel;Check(a.Rooms.StorageUnlocked&&a.Rooms.StorageVisited&&a.Loot.StatusKind(7)==3,"Restart restore");await Begin();await Move();await Task.Delay(100);Check(a.Rooms.StorageDoorStatus.text=="개방됨 · 보관실","Door status");Bounds(a.Main.gameObject);CampaignSaveStore.TestDirectory=null;return "PASS: Play restart from actual v8 save, door status, restored depletion. Preview at corridor.";
 }
 public static async Task<string> FinalRoom(){FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */await EnterStorage();Bounds(C().ArrivalPanel.Main.gameObject);return "Room ready";}}
