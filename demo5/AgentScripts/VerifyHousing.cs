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
using Object=UnityEngine.Object;
public static class VerifyHousing
{
 static SettlementController Owner()=>Object.FindAnyObjectByType<SettlementController>();
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Bounds(GameObject g){Canvas.ForceUpdateCanvases();foreach(var t in g.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height,"Text overflow "+t.name+": "+t.text);}
 static async Task Tap(Button b){await Task.Delay(60);Check(b&&b.IsActive()&&b.IsInteractable(),"Unavailable "+b?.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(90);}
 static async Task Select(SettlementController c,string id,int worker){await Tap(c.CraftPanel.HousingButton);c.CraftPanel.FocusRecipe(id);c.CraftPanel.RecipeScroll.verticalNormalizedPosition=0;await Tap(c.CraftPanel.WorkerRows[worker].Button);Bounds(c.CraftPanel.View);}
 public static async Task<string> Flow(){
  var c=Owner();var f=c.CraftPanel;Check(f.ResidentCapacity==3&&f.RoomStatus=="잠김"&&c.Campaign.Party.Count()==2,"Initial housing");Check(f.CanAcceptResidents(1)&&!f.CanAcceptResidents(2)&&!f.CanAcceptResidents(0),"Initial admission boundary");Bounds(c.Main.gameObject);
  await Tap(f.HousingButton);Check(!f.Confirm.interactable,"Missing worker should block construction");Bounds(f.View);await Tap(f.CloseButton);
  foreach(var m in f.Materials)m.Initial=20;
  await Select(c,"prepare-side-room",0);Check(!f.Confirm.interactable&&f.ConfirmLabel.text.Contains("연결"),"Prerequisite missing");f.Confirm.onClick.Invoke();Check(f.Orders.Count==0,"Prerequisite bypass");await Tap(f.CloseButton);
  int wood=f.Materials.First(m=>m.Id=="wood").Initial;await Select(c,"open-side-room",0);await Tap(f.Confirm);Check(f.RoomStatus=="통로 정리 중"&&f.ResidentCapacity==3&&f.Available("wood")==wood-2,"Opening reservation");
  c.Campaign.AdvanceSettlementTime(10);await Tap(f.HousingButton);Check(!f.Confirm.interactable,"Duplicate opening");await Tap(f.OrderRows[0].Cancel);await Tap(f.CancelYes);Check(f.RoomStatus=="잠김"&&f.Materials.First(m=>m.Id=="wood").Initial==wood,"Opening cancel");await Tap(f.CloseButton);
  await Select(c,"open-side-room",0);await Tap(f.Confirm);c.Campaign.AdvanceSettlementTime(45);Check(f.SideRoomConnected&&!f.SideRoomReady&&f.ResidentCapacity==3&&f.Materials.First(m=>m.Id=="wood").Initial==wood-2,"Connection increased capacity");
  await Tap(f.HousingButton);Check(f.DetailTitle.text=="옆방 거주 준비","Shortcut doesn't advance step");await Tap(f.WorkerRows[0].Button);await Tap(f.Confirm);Check(f.RoomStatus=="거주 준비 중"&&f.ResidentCapacity==3,"Premature capacity");c.Campaign.AdvanceSettlementTime(15);
  await Tap(f.HousingButton);await Tap(f.OrderRows[0].Cancel);await Tap(f.CancelYes);Check(f.SideRoomConnected&&!f.SideRoomReady&&f.ResidentCapacity==3,"Prepare cancellation lost connection");await Tap(f.CloseButton);
  await Select(c,"prepare-side-room",0);await Tap(f.Confirm);c.Campaign.AdvanceSettlementTime(15);
  CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/HousingVerification");var pending=CampaignPersistence.Capture(c);Check(CampaignSaveStore.Write(0,pending,c,out var error),error);var disk=CampaignSaveStore.Read(0,c);Check(disk.CanLoad,disk.Error);CampaignPersistence.Prepare(disk.Data,c);SceneManager.LoadScene("Settlement");await Task.Delay(700);c=Owner();f=c.CraftPanel;Check(f.SideRoomConnected&&!f.SideRoomReady&&f.Orders.Single().Minutes==75&&f.ResidentCapacity==3,"Partial room restore");
  var party=c.Campaign.Party.ToArray();Check(c.ArrivalPanel.Begin(new[]{party[1]},c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")),"Travel with builder at home");await Task.Delay(800);Check(c.Campaign.Party.Count()==2&&f.CanAcceptResidents(1)&&!f.CanAcceptResidents(2),"Explorer excluded from capacity");
  await Tap(c.ArrivalPanel.Return);await Tap(c.ArrivalPanel.ReturnConfirm);await Tap(c.ReturnPanel.Back);if(f.Orders.Count>0)c.Campaign.AdvanceSettlementTime(f.Orders[0].Minutes);
  Check(f.ResidentCapacity==5&&f.RoomStatus=="사용 가능"&&c.Campaign.Party.Count()==2&&f.CanAcceptResidents(3)&&!f.CanAcceptResidents(4),"Ready capacity/admission/auto recruit");int finalWood=f.Materials.First(m=>m.Id=="wood").Initial;Check(finalWood==wood-6,"Room exact cost");c.Campaign.AdvanceSettlementTime(120);Check(f.ResidentCapacity==5&&f.Materials.First(m=>m.Id=="wood").Initial==finalWood,"Duplicate capacity/cost");await Tap(f.HousingButton);Check(!f.Confirm.interactable,"Repeat habitation");Bounds(f.View);await Tap(f.CloseButton);
  var bad=CampaignPersistence.Capture(c);bad.SideRoomConnected=false;Check(!CampaignSaveStore.Write(2,bad,c,out _),"Disconnected ready save accepted");
  var wrong=CampaignPersistence.Capture(c);var recipe=f.Recipes.First(r=>r.Id=="prepare-side-room");wrong.Craft=new[]{new SavedCraft{MemberId=wrong.Members[0].Id,RecipeId=recipe.Id,Quantity=1,Minutes=1,Reserved=recipe.Costs.Select(x=>new SavedCount{Id=x.MaterialId,Count=x.Count}).ToArray()}};Check(!CampaignSaveStore.Write(2,wrong,c,out _),"Repeated saved construction");wrong.SideRoomConnected=wrong.SideRoomReady=false;Check(!CampaignSaveStore.Write(2,wrong,c,out _),"Skipped prerequisite in save");
  var older=CampaignPersistence.Capture(c);older.Version=5;older.ReturnReport=null;older.SideRoomConnected=older.SideRoomReady=false;var original=older.Members[0];older.Members=c.Roster.Candidates.Take(4).Select(p=>new SavedMember{Id=p.Id,Name=p.DisplayName,Role=original.Role,Description=original.Description,Health=original.Health,Maximum=original.Maximum,Aim=original.Aim,Capacity=original.Capacity,Bag=Array.Empty<SavedCount>()}).ToArray();CampaignPersistence.Upgrade(older,c);Check(older.Version==CampaignPersistence.CurrentVersion&&older.Members.Length==4&&!older.SideRoomReady,"Old save migration");CampaignPersistence.Prepare(older,c);SceneManager.LoadScene("Settlement");await Task.Delay(700);c=Owner();f=c.CraftPanel;Check(c.Campaign.Party.Count()==4&&f.ResidentCapacity==3&&!f.CanAcceptResidents(1),"Legacy overcrowding evicted or admitted");Bounds(c.Main.gameObject);
  return "PASS: 2/3 start, sequential jobs, reserves/cancel/exact costs, partial save restore, travel counts all residents, once-only 3->5, no automatic recruitment, admission boundaries, invalid saved states, v5 migration preserves 4/3 residents. Partial job fixture retained for restart.";
 }
 public static async Task<string> Restart(){CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/HousingVerification");SceneManager.LoadScene("StartMenu");await Task.Delay(650);await Tap(Object.FindAnyObjectByType<TitleMenuController>().ContinueButton);await Task.Delay(700);var c=Owner();var f=c.CraftPanel;Check(f.ResidentCapacity==3&&f.SideRoomConnected&&!f.SideRoomReady&&f.Orders.Single().Minutes==75,"Restart room state");c.Campaign.AdvanceSettlementTime(75);Check(f.ResidentCapacity==5&&c.Campaign.Party.Count()==2,"Restart completion");Check(CampaignSaveStore.Write(1,CampaignPersistence.Capture(c),c,out var error),error);var saved=CampaignSaveStore.Read(1,c);Check(saved.CanLoad,saved.Error);CampaignPersistence.Prepare(saved.Data,c);SceneManager.LoadScene("Settlement");await Task.Delay(700);c=Owner();Check(c.CraftPanel.ResidentCapacity==5&&c.CraftPanel.SideRoomReady&&c.CraftPanel.Orders.Count==0,"Completed room restore");Bounds(c.Main.gameObject);return "PASS: fresh Play partial room restore -> once-only ready -> completed room disk reload, 2/5 HUD ready.";}
 public static async Task<string> CompletedPreview(){var c=Owner();await Tap(c.CraftPanel.HousingButton);Bounds(c.CraftPanel.View);return "Completed habitation screen ready.";}
 public static async Task<string> PendingPreview(){var c=Owner();var disk=CampaignSaveStore.Read(0,c);Check(disk.CanLoad,disk.Error);CampaignPersistence.Prepare(disk.Data,c);SceneManager.LoadScene("Settlement");await Task.Delay(700);c=Owner();await Tap(c.CraftPanel.HousingButton);Bounds(c.CraftPanel.View);return "Pending habitation screen ready.";}
 public static string Cleanup(){CampaignSaveStore.TestDirectory=null;return "Test directory cleared.";}
}

