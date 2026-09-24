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
public static class VerifyCorridorSearch
{
 static SettlementController C()=>UnityEngine.Object.FindAnyObjectByType<SettlementController>();
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static async Task Tap(Button b){await Task.Delay(60);Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(90);}
 static async Task Wait(){for(int i=0;i<60&&C().ArrivalPanel.InTransit;i++)await Task.Delay(100);Check(!C().ArrivalPanel.InTransit,"Transit timeout");}
 static void Bounds(GameObject g){Canvas.ForceUpdateCanvases();foreach(var t in g.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Overflow "+t.name+": "+t.text);}
 static async Task Begin(){var c=C();Check(c.ArrivalPanel.Begin(c.Campaign.Party.ToArray(),c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")),"Begin");await Wait();}
 static async Task Move(){var a=C().ArrivalPanel;int turn=a.Rooms.Turns;int minute=C().Campaign.MinuteOfDay;await Tap(a.Rooms.CurrentRoom==0?a.Objects[3]:a.Rooms.CorridorBack);await Tap(a.ReturnConfirm);a.ReturnConfirm.onClick.Invoke();await Wait();Check(a.Rooms.Turns==turn+1&&C().Campaign.MinuteOfDay==minute+10,"Move charged twice");}
 static async Task Step(){var s=C().ArrivalPanel.Search;await Tap(s.Choose);await Tap(s.Confirm);}
 public static async Task<string> Flow(){
  var c=C();var a=c.ArrivalPanel;var l=a.Loot;
  Check(l.Sites.Length>=6&&l.Sites[3].Room==-1,"Door became searchable");
  var old=CampaignPersistence.Capture(c);CampaignPersistence.Upgrade(old,c);Check(old.Version==CampaignPersistence.CurrentVersion&&old.Searches.Length==0,"Existing v7 incompatible");
  foreach(var site in l.Sites){Check(site.Drops.All(d=>c.InventoryPanel.Items.Any(i=>i.Id==d.Id)),"Unknown drop");Check(string.IsNullOrEmpty(site.RequiredTool)||c.InventoryPanel.Items.Any(i=>i.Id==site.RequiredTool),"Unknown tool");}
  await Begin();a.Encounter.NoiseThreshold=int.MaxValue;
  Check(!a.Objects[4].gameObject.activeSelf&&!l.Advance(4,0,a.Participants[0]),"Remote corridor search");a.Inspect(4);Check(!a.Search.IsOpen&&l.ExportSearches().Length==0,"Remote inspection");
  int time=c.Campaign.MinuteOfDay;await Tap(a.Objects[3]);await Tap(a.PopupBack);Check(time==c.Campaign.MinuteOfDay,"Cancel costs time");await Move();
  Check(a.Objects[4].gameObject.activeSelf&&a.Objects[5].gameObject.activeSelf&&!a.Objects[0].gameObject.activeSelf&&!a.Return.interactable,"Room visibility");Check(!l.Advance(0,0,a.Participants[0]),"Remote arcade search");
  await Tap(a.Objects[4]);await Tap(a.Search.Cards[0].Button);Check(!a.Search.Choose.interactable,"Missing tool allowed");Bounds(a.Search.View);await Tap(a.Search.Back);
  Check(c.InventoryPanel.TransferField(a.Participants[0],"prybar",1,true),"Tool fixture");foreach(var d in l.Sites[4].Drops)d.Chance=100;
  await Tap(a.Objects[4]);await Tap(a.Search.Cards[0].Button);await Tap(a.Search.Paces[1]);await Step();Check(l.State(4).Progress==1&&l.State(4).Opened,"Partial open");await Tap(a.Search.Back);
  Check(c.InventoryPanel.CountFor(a.Participants[0],"prybar")==1,"Tool consumed");Check(c.InventoryPanel.TransferField(a.Participants[0],"prybar",1,false),"Remove tool fixture");await Move();await Move();await Tap(a.Objects[4]);await Tap(a.Search.Cards[0].Button);Check(a.Search.Choose.interactable,"Opened lid needs tool again");await Step();Check(l.IsOpen&&l.State(4).Complete,"Loot not opened");
  while(l.FieldRows.Count>0){await Tap(l.FieldRows[0].Button);await Tap(l.Max);await Tap(l.Transfer);}await Tap(l.Back);Check(l.StatusKind(4)==3&&!l.Advance(4,0,a.Participants[0]),"Depleted reroll");
  await Tap(a.Objects[5]);await Tap(a.Search.Cards[0].Button);await Tap(a.Search.Paces[2]);await Step();Check(l.State(5).Progress==1&&!l.State(5).Complete,"Crate partial");Bounds(a.Search.View);await Tap(a.Search.Back);Bounds(a.Main.gameObject);
  return "PASS: room guards, cancelled/duplicate movement, missing tool, nonconsuming lid opening, resume after tool removal and room change, pickup/depletion/no reroll, partial crate, catalog IDs and v7 compatibility.";
 }
 public static async Task<string> Persistence(){
  var c=C();await Move();var a=c.ArrivalPanel;await Tap(a.Return);await Tap(a.ReturnConfirm);await Tap(c.ReturnPanel.Back);
  CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/CorridorSearchVerification");Check(CampaignSaveStore.Write(0,CampaignPersistence.Capture(c),c,out var error),error);var disk=CampaignSaveStore.Read(0,c);Check(disk.CanLoad,disk.Error);CampaignPersistence.Prepare(disk.Data,c);SceneManager.LoadScene("Settlement");await Task.Delay(700);c=C();a=c.ArrivalPanel;
  Check(a.Loot.StatusKind(4)==3&&a.Loot.State(5).Progress==1&&a.Rooms.Inspected.Contains(4)&&a.Rooms.Inspected.Contains(5),"Saved state");Check(a.Rooms.CorridorVisited&&!a.Rooms.StorageVisited&&a.Threat&&!a.Rooms.Inspected.Contains(a.Threat.DenSite),"Fixture should know arcade/corridor but not storage/office shelf");int knownSites=a.Loot.Sites.Select((s,i)=>new{s,i}).Count(x=>(x.s.Room==0||x.s.Room==1)&&x.i!=a.Threat.DenSite);Check(a.Loot.SearchSummary()=="미수색 "+(knownSites-2)+" · 진행 1\n물품 남음 0 · 비어 있음 1","Summary must exclude the door, unvisited storage and hidden office shelf");await Begin();await Move();Check(a.Loot.StatusKind(4)==3&&a.Loot.State(5).Progress==1,"Revisit reset");
  var e=a.Encounter;e.NoiseThreshold=0;e.BaseChance=e.MaximumChance=100;
  await Tap(a.Objects[5]);await Tap(a.Search.Cards[0].Button);await Step();await Step();Check(e.IsOpen,"Corridor encounter not triggered");await Tap(e.Retreat);Check(e.ReviewBody.text.StartsWith("오락실로"),"Wrong retreat destination");await Tap(e.Confirm);await Wait();Check(a.Rooms.CurrentRoom==0&&a.Loot.State(5).Complete,"Retreat lost search progress");await Move();Check(a.Loot.State(5).Complete,"Return lost loot");CampaignSaveStore.TestDirectory=null;Bounds(a.Main.gameObject);return "PASS: isolated file restore, revisit, inspection IDs, five-site summary, corridor encounter retreat and completed rewards preserved.";
 }
 public static async Task<string> SearchPreview(){var a=C().ArrivalPanel;await Tap(a.Objects[4]);Bounds(a.Loot.View);return "Depleted loot preview";}
 public static async Task<string> LegacyPreview(){
  var c=C();CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/SearchStatusVerification");var disk=CampaignSaveStore.Read(0,c);Check(disk.CanLoad,disk.Error);Check(disk.Data.Searches.All(s=>!s.Id.StartsWith("mall.corridor")),"Need pre-corridor fixture");CampaignPersistence.Prepare(disk.Data,c);SceneManager.LoadScene("Settlement");await Task.Delay(700);c=C();var a=c.ArrivalPanel;Check(a.Loot.StatusKind(0)==3&&a.Loot.State(1).Progress==1&&a.Loot.StatusKind(4)==0&&a.Loot.StatusKind(5)==0,"Legacy migration changed old state");await Begin();await Move();await Tap(a.Objects[4]);await Tap(a.Search.Cards[0].Button);Check(!a.Search.Choose.interactable,"Legacy missing tool");Bounds(a.Search.View);CampaignSaveStore.TestDirectory=null;return "PASS: existing pre-expansion v7 file preserves old depletion/progress and initializes new corridor sites unsearched.";
 }
}
