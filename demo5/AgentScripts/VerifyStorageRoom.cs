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
public static class VerifyStorageRoom
{
 static SettlementController C()=>UnityEngine.Object.FindAnyObjectByType<SettlementController>();
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static async Task Tap(Button b){await Task.Delay(60);Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(90);}
 static async Task Wait(){for(int i=0;i<60&&C().ArrivalPanel.InTransit;i++)await Task.Delay(100);Check(!C().ArrivalPanel.InTransit,"Transit timeout");}
 static void Bounds(GameObject g){Canvas.ForceUpdateCanvases();foreach(var t in g.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Overflow "+t.name+": "+t.text);}
 static async Task Begin(){var c=C();Check(c.ArrivalPanel.Begin(c.Campaign.Party.ToArray(),c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")),"Begin");await Wait();}
 static async Task Move(){var a=C().ArrivalPanel;int turn=a.Rooms.Turns;int minute=C().Campaign.MinuteOfDay;await Tap(a.Rooms.CurrentRoom==0?a.Objects[3]:a.Rooms.CorridorBack);await Tap(a.ReturnConfirm);a.ReturnConfirm.onClick.Invoke();await Wait();Check(a.Rooms.Turns==turn+1&&C().Campaign.MinuteOfDay==minute+10,"Move charged twice");}
 static async Task Step(){var s=C().ArrivalPanel.Search;await Tap(s.Choose);await Tap(s.Confirm);}
 static async Task BackRoom(){var a=C().ArrivalPanel;await Tap(a.Rooms.CurrentRoom==2?a.Rooms.StorageBack:a.Rooms.CorridorBack);await Tap(a.ReturnConfirm);await Wait();}
 static async Task EnterStorage(){var a=C().ArrivalPanel;int before=C().Campaign.MinuteOfDay;await Tap(a.Rooms.LockedDoor);await Tap(a.ReturnConfirm);a.ReturnConfirm.onClick.Invoke();await Wait();Check(a.Rooms.CurrentRoom==2&&C().Campaign.MinuteOfDay==before+10,"Reentry cost/destination");}
 public static async Task<string> Flow(){
  var c=C();var a=c.ArrivalPanel;var n=a.Rooms;var l=a.Loot;Check(n.Storage&&l.Sites.Length==8,"Room setup");await Begin();await Move();a.Encounter.NoiseThreshold=int.MaxValue;
  int time=c.Campaign.MinuteOfDay,turn=n.Turns;await Tap(n.LockedDoor);Check(!a.ReturnConfirm.gameObject.activeSelf&&!n.StorageUnlocked,"Missing tool allowed");Bounds(a.Popup);await Tap(a.PopupBack);Check(c.Campaign.MinuteOfDay==time,"Missing tool charged");
  Check(c.InventoryPanel.TransferField(a.Participants[0],"prybar",1,true),"Give tool");await Tap(n.LockedDoor);Bounds(a.Popup);await Tap(a.PopupBack);Check(!n.StorageUnlocked&&time==c.Campaign.MinuteOfDay,"Cancel changed lock/time");
  await Tap(n.LockedDoor);Check(c.InventoryPanel.TransferField(a.Participants[0],"prybar",1,false),"Remove tool before confirm");await Tap(a.ReturnConfirm);Check(!n.StorageUnlocked&&time==c.Campaign.MinuteOfDay&&!a.InTransit,"Stale tool accepted");Check(c.InventoryPanel.TransferField(a.Participants[0],"prybar",1,true),"Restore tool");
  await Tap(n.LockedDoor);await Tap(a.ReturnConfirm);a.ReturnConfirm.onClick.Invoke();await Wait();Check(n.CurrentRoom==2&&n.StorageUnlocked&&n.StorageVisited&&n.Turns==turn+2&&c.Campaign.MinuteOfDay==time+20&&n.Noise==2,"Unlock costs/state");Check(c.InventoryPanel.CountFor(a.Participants[0],"prybar")==1,"Tool consumed");
  Check(n.Background.sprite==n.Storage&&n.StorageHotspots.activeSelf&&a.Objects[6].gameObject.activeSelf&&!a.Objects[4].gameObject.activeSelf&&!a.Return.interactable,"Storage visibility");Check(!l.Advance(4,0,a.Participants[0]),"Remote corridor search");Bounds(a.Main.gameObject);
  Check(c.InventoryPanel.TransferField(a.Participants[0],"prybar",1,false),"Remove after unlocking");await BackRoom();await EnterStorage();Check(n.Noise==2,"Reentry unlock noise");
  await Tap(a.Objects[6]);await Tap(a.Search.Cards[0].Button);await Tap(a.Search.Paces[2]);await Step();Check(l.State(6).Progress==1,"Partial shelf");Bounds(a.Search.View);await Tap(a.Search.Back);
  foreach(var drop in l.Sites[7].Drops)drop.Chance=100;
  await Tap(a.Objects[7]);await Tap(a.Search.Cards[0].Button);await Tap(a.Search.Paces[0]);await Step();Check(l.IsOpen,"Materials not completed");while(l.FieldRows.Count>0){await Tap(l.FieldRows[0].Button);await Tap(l.Max);await Tap(l.Transfer);}await Tap(l.Back);Check(l.StatusKind(7)==3&&!l.Advance(7,0,a.Participants[0]),"Materials reroll");
  Bounds(a.Main.gameObject);return "PASS: missing/stale tool, cancel, unlock charge once, tool not consumed, two-way travel without tool, room visibility, partial shelf, materials pickup/depletion.";
 }
 public static async Task<string> Persistence(){
  var c=C();await BackRoom();await BackRoom();var a=c.ArrivalPanel;await Tap(a.Return);await Tap(a.ReturnConfirm);await Tap(c.ReturnPanel.Back);
  CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/StorageRoomVerification");Check(CampaignSaveStore.Write(0,CampaignPersistence.Capture(c),c,out var error),error);var saved=CampaignSaveStore.Read(0,c);Check(saved.CanLoad&&saved.Data.Version==8,saved.Error);CampaignPersistence.Prepare(saved.Data,c);SceneManager.LoadScene("Settlement");await Task.Delay(700);c=C();a=c.ArrivalPanel;
  Check(a.Rooms.StorageUnlocked&&a.Rooms.StorageVisited&&a.Loot.State(6).Progress==1&&a.Loot.StatusKind(7)==3,"v8 restore");await Begin();await Move();await EnterStorage();
  a.Encounter.NoiseThreshold=0;a.Encounter.BaseChance=a.Encounter.MaximumChance=100;await Tap(a.Objects[6]);await Tap(a.Search.Cards[1].Button);await Step();await Step();Check(a.Encounter.IsOpen,"Storage encounter");await Tap(a.Encounter.Retreat);Check(a.Encounter.ReviewBody.text.StartsWith("복도로"),"Wrong retreat destination");await Tap(a.Encounter.Confirm);await Wait();Check(a.Rooms.CurrentRoom==1&&a.Loot.State(6).Complete,"Storage retreat");await EnterStorage();Check(a.Loot.State(6).Complete,"Reward lost after retreat");CampaignSaveStore.TestDirectory=null;return "PASS: v8 file restore, unlocked revisit without tool, saved partial/depleted state, storage encounter retreats to corridor, completed loot preserved.";
 }
 public static async Task<string> Legacy(){
  var c=C();CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/CorridorSearchVerification");var saved=CampaignSaveStore.Read(0,c);Check(saved.CanLoad,saved.Error);Check(saved.Data.Version==8&&!saved.Data.StorageUnlocked&&!saved.Data.StorageVisited,"v7 migration");CampaignPersistence.Prepare(saved.Data,c);SceneManager.LoadScene("Settlement");await Task.Delay(700);c=C();Check(c.ArrivalPanel.Loot.StatusKind(4)==3&&c.ArrivalPanel.Loot.State(5).Progress==1&&c.ArrivalPanel.Loot.StatusKind(6)==0,"Old site state changed");CampaignSaveStore.TestDirectory=null;return "PASS: actual pre-storage v7 file migrates to v8, old corridor states preserved, storage locked and unsearched.";
 }
 public static async Task<string> FinalPreview(){
  var catalog=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab").GetComponent<SettlementController>();CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/StorageRoomVerification");var saved=CampaignSaveStore.Read(0,catalog);Check(saved.CanLoad,saved.Error);CampaignPersistence.Prepare(saved.Data,catalog);SceneManager.LoadScene("Settlement");await Task.Delay(700);var a=C().ArrivalPanel;Check(a.Rooms.StorageUnlocked&&a.Rooms.StorageVisited&&a.Loot.StatusKind(7)==3,"Restart restore");await Begin();await Move();await Task.Delay(100);Check(a.Rooms.StorageDoorStatus.text=="개방됨 · 보관실","Door status");Bounds(a.Main.gameObject);CampaignSaveStore.TestDirectory=null;return "PASS: Play restart from actual v8 save, door status, restored depletion. Preview at corridor.";
 }
 public static async Task<string> FinalRoom(){await EnterStorage();Bounds(C().ArrivalPanel.Main.gameObject);return "Room ready";}}
