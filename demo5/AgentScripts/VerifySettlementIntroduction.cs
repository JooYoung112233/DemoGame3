using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Demo5.FrontEnd;
using Demo5.NightRun;
public static class VerifySettlementIntroduction
{
    static SettlementController C()=>UnityEngine.Object.FindAnyObjectByType<SettlementController>();
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static async Task Tap(Button button)
    {
        await Task.Delay(100);Canvas.ForceUpdateCanvases();Check(button.IsActive()&&button.IsInteractable(),"Unavailable "+button.name);
        var rect=(RectTransform)button.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(button.GetComponentInParent<Canvas>().worldCamera,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Blocked "+button.name);
        ExecuteEvents.Execute(button.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(120);
    }
    public static async Task<string> Fresh()
    {
        var roster=C().Roster;
        PartySelectionSession.Clear();var campaign=new CampaignState(roster.Candidates.Select(x=>new Adventurer(x.DisplayName,x.DisplayName,x.Description,x.Health,x.Aim,x.BagCapacity)).ToArray(),true);
        campaign.Toggle(0);campaign.Toggle(1);campaign.ConfirmParty();campaign.Settle(0);
        PartySelectionSession.Selected.AddRange(new[]{roster.Candidates[0].Id,roster.Candidates[1].Id});PartySelectionSession.Pending=campaign;SceneManager.LoadScene("Settlement");await Task.Delay(500);
        var c=C();Check(c.Introduction.Step==0&&c.Campaign.Supplies==0&&c.Campaign.Ammo==0&&c.CraftPanel.Materials.All(m=>m.Initial==0),"Initial stock not empty");
        Check(!c.Bed.gameObject.activeSelf&&!c.Workbench.gameObject.activeSelf&&!c.Exit.gameObject.activeSelf&&c.GameMenu.OpenButton.gameObject.activeSelf,"Initial gating");
        c.CraftPanel.Open();c.ExpeditionPanel.Open();Check(!c.CraftPanel.IsOpen&&!c.ExpeditionPanel.IsOpen,"Alternate entry bypass");
        return "PASS fresh empty stock, introduction only, menu available, locked APIs guarded; initial screenshot ready";
    }
    static async Task CompleteWork(){var c=C();await Tap(c.Advance);await Tap(c.TimePanel.Choices[3]);await Tap(c.TimePanel.Confirm);await Tap(c.TimePanel.CloseButton);}
    public static async Task<string> TextBounds()
    {
        var c=C();int previous=c.Introduction.Step;
        try{for(int step=0;step<10;step++){
            c.Introduction.Restore(step);await Task.Delay(90);Canvas.ForceUpdateCanvases();
            foreach(var text in new[]{c.NoticeTitle,c.NoticeBody,c.Introduction.ActionLabel})
                Check(text.preferredHeight<=text.rectTransform.rect.height+1,"Clipped step "+step+" "+text.name+" "+text.preferredHeight);
        }}finally{c.Introduction.Restore(previous);}
        return "PASS all introduction titles, two-line hints and action labels fit at "+Screen.width+"x"+Screen.height;
    }
    public static async Task<string> Flow()
    {
        var c=C();Check(c.Introduction.Step==0,"Fresh first");
        await Tap(c.Introduction.Action);Check(c.Introduction.Step==1&&c.CraftPanel.Materials.All(m=>m.Initial==0),"Survey grants resources");
        await Tap(c.Introduction.Action);Check(c.Introduction.Step==2&&c.CraftPanel.Available("scrap")==3&&c.CraftPanel.Available("food")==0,"Search discovery");
        // File round-trip in an isolated verification directory, never the player's slots.
        CampaignSaveStore.TestDirectory=System.IO.Path.GetFullPath("Temp/IntroductionVerification");
        Check(CampaignSaveStore.Write(0,CampaignPersistence.Capture(c),c,out var error),error);
        var read=CampaignSaveStore.Read(0,c);Check(read.CanLoad,read.Error);CampaignPersistence.Prepare(read.Data,c);SceneManager.LoadScene("Settlement");await Task.Delay(500);c=C();
        Check(c.Introduction.Step==2&&c.CraftPanel.Available("scrap")==3,"Restore replayed reward");
        await Tap(c.Introduction.Action);Check(c.Introduction.Step==3,"Bed setup");
        await Tap(c.Bed);await Tap(c.WorkPanel.Rows[0].Button);await Tap(c.WorkPanel.Confirm);Check(c.Introduction.Step==3,"Reservation completed tutorial early");
        await CompleteWork();Check(c.Introduction.Step==4&&c.Campaign.Party.First().Health==c.Campaign.Party.First().MaxHealth,"Actual rest completion");
        await Tap(c.Cabinet);await Tap(c.InventoryPanel.CloseButton);Check(c.Introduction.Step==5,"Stock review");
        await Tap(c.Introduction.Action);Check(c.Introduction.Step==6,"Workbench setup");
        await Tap(c.Workbench);Check(c.CraftPanel.RecipeRows.Count==1,"Too many initial recipes");await Tap(c.CraftPanel.RecipeRows[0].Button);await Tap(c.CraftPanel.WorkerRows[0].Button);await Tap(c.CraftPanel.Confirm);
        Check(c.Introduction.Step==6,"Craft reservation unlocked cooking");await CompleteWork();Check(c.Introduction.Step==7&&c.CraftPanel.Available("nails")==1,"Nails completion");
        await Tap(c.Introduction.Action);Check(c.Introduction.Step==8&&c.CraftPanel.Available("food")==4,"Pantry discovery");
        await Tap(c.Stock);Check(c.CookingPanel.RecipeRows.Count==1,"Too many initial meals");await Tap(c.CookingPanel.WorkerRows[0].Button);await Tap(c.CookingPanel.Confirm);await CompleteWork();
        Check(c.Introduction.Step==9&&c.CraftPanel.Available("meal")>0&&c.Exit.gameObject.activeSelf&&!c.VisitorPanel.OpenButton.gameObject.activeSelf,"Meal and expedition unlock");
        var saved=CampaignPersistence.Capture(c);saved.Version=8;saved.IntroductionStep=0;CampaignPersistence.Upgrade(saved,c);Check(saved.Version==9&&saved.IntroductionStep==10,"Legacy migration locks old player");
        return "PASS actual UI path through survey/search, file save/load without duplicate grants, rest/craft/cooking completion, staged menus, old-save migration; ready for expedition";
    }
    public static async Task<string> Return()
    {
        var c=C();var a=c.ArrivalPanel;Check(c.Introduction.Step==9,"Finish tutorial first");
        Check(a.Begin(c.Campaign.Party.ToArray(),c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")),"First expedition");
        for(int i=0;i<60&&a.InTransit;i++)await Task.Delay(100);
        await Tap(a.Return);await Tap(a.ReturnConfirm);await Tap(c.ReturnPanel.Back);
        Check(c.Introduction.Step==10&&c.VisitorPanel.OpenButton.gameObject.activeSelf&&c.CraftPanel.HousingButton.gameObject.activeSelf,"Return did not unlock settlement");
        var saved=CampaignPersistence.Capture(c);Check(CampaignSaveStore.Write(1,saved,c,out var error),error);var read=CampaignSaveStore.Read(1,c);Check(read.CanLoad,read.Error);
        CampaignPersistence.Prepare(read.Data,c);SceneManager.LoadScene("Settlement");await Task.Delay(500);Check(C().Introduction.Step==10,"Completed introduction reset");
        CampaignSaveStore.TestDirectory=null;return "PASS first expedition/return opens visitors and expansion; completed file restore retained";
    }
}
