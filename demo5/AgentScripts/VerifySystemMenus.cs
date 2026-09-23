using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;

public static class VerifySystemMenus
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static async Task Tap(Button b)
    {
        // Let the normal render/layout loop update newly enabled canvases before raycasting.
        await Task.Delay(60);
        Check(b && b.IsActive() && b.IsInteractable(),"Unavailable "+(b?b.name:"null"));Canvas.ForceUpdateCanvases();
        var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);
        Check(hits.Count>0 && hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(90);
    }
    static void Bounds(GameObject g)
    {
        Canvas.ForceUpdateCanvases();foreach(var t in g.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text overflow: "+t.name+" "+t.preferredHeight+"/"+t.rectTransform.rect.height);
    }
    static string Comparable(CampaignSaveData data){data.SavedUtc="2000-01-01T00:00:00.0000000Z";return JsonUtility.ToJson(data);}
    static SettlementController Owner()=>Object.FindAnyObjectByType<SettlementController>();
    public static async Task<string> RoundTrip()
    {
        CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/SystemMenuVerification");Directory.CreateDirectory(CampaignSaveStore.TestDirectory);
        // This fixed test directory is isolated from all player files.
        foreach(var name in new[]{"slot-1.json","slot-2.json","slot-3.json","slot-1.json.bak","slot-2.json.bak","slot-3.json.bak"}){string file=Path.Combine(CampaignSaveStore.TestDirectory,name);if(File.Exists(file))File.Delete(file);}
        var c=Owner();Check(c && c.GameMenu,"Initialize with VerifySettlement.Flow first");var party=c.Campaign.Party.ToArray();
        Check(c.InventoryPanel.MoveFor(party[0],"ammo",2,true),"Pack ammo");
        Check(c.ArrivalPanel.Begin(party,c.ExpeditionPanel.Destinations.First(x=>x.Id=="mall")),"Start expedition");await Task.Delay(900);
        bool refused=false;try{CampaignPersistence.Capture(c);}catch(InvalidOperationException){refused=true;}Check(refused,"Field save accepted");c.GameMenu.Open();Check(!c.GameMenu.IsOpen,"Menu opened over expedition");
        c.ArrivalPanel.Rooms.MarkInspected(0);Check(c.ArrivalPanel.Loot.Advance(0,2,party[0]),"Partial search");Check(c.ArrivalPanel.Loot.Advance(1,0,party[0]),"Complete table search");
        Check(c.InventoryPanel.TransferField(party[0],"wood",2,true),"Field wood");party[0].Health=1;
        await Tap(c.ArrivalPanel.Return);await Tap(c.ArrivalPanel.ReturnConfirm);Check(c.ReturnPanel.HasReport,"Return report");await Tap(c.ReturnPanel.Back);
        await Tap(c.Bed);await Tap(c.WorkPanel.Rows[0].Button);await Tap(c.WorkPanel.Confirm);
        await Tap(c.Workbench);c.CraftPanel.FocusRecipe("nails");await Tap(c.CraftPanel.WorkerRows[1].Button);await Tap(c.CraftPanel.Plus);await Tap(c.CraftPanel.Confirm);
        c.Campaign.AdvanceSettlementTime(10);Check(c.WorkPanel.Orders.Count==1 && c.CraftPanel.Orders.Count==1,"Pending jobs");
        await Tap(c.GameMenu.OpenButton);Check(c.Main.alpha==0 && !c.Main.interactable,"Menu HUD/input gate");Bounds(c.GameMenu.Page);
        await Tap(c.GameMenu.Save);var panel=c.GameMenu.SavePanel;Check(!panel.Action.interactable,"No selection save enabled");Bounds(panel.gameObject);
        await Tap(panel.Slots[0]);await Tap(panel.Action);Check(panel.Hint.text.Contains("저장했습니다"),"Save failed: "+panel.Hint.text);
        string before=Comparable(CampaignPersistence.Capture(c));string originalFile=File.ReadAllText(CampaignSaveStore.SlotPath(0));
        await Tap(panel.Action);Check(panel.Review.activeSelf,"No overwrite confirmation");await Tap(panel.Cancel);Check(File.ReadAllText(CampaignSaveStore.SlotPath(0))==originalFile,"Cancel overwrote file");
        await Tap(panel.Action);await Tap(panel.Confirm);Check(File.Exists(CampaignSaveStore.SlotPath(0)+".bak"),"Atomic replacement backup missing");
        Check(CampaignSaveStore.Read(0,c).CanLoad,"Written file unreadable");Bounds(panel.gameObject);await Tap(panel.Back);await Tap(c.GameMenu.Resume);
        c.Campaign.AdvanceSettlementTime(60);Check(c.WorkPanel.Orders.Count==0 && c.CraftPanel.Orders.Count==0,"Fixture completion");
        await Tap(c.GameMenu.OpenButton);await Tap(c.GameMenu.Load);await Tap(panel.Slots[0]);await Tap(panel.Action);await Tap(panel.Confirm);await Task.Delay(650);
        c=Owner();Check(Comparable(CampaignPersistence.Capture(c))==before,"Round-trip state differs");Check(c.WorkPanel.Orders[0].Member==c.Campaign.Party.First(),"Rest member reference mismatch");
        Check(c.ArrivalPanel.Loot.State(0).Progress==1 && c.ArrivalPanel.Loot.State(1).Complete,"Search progress lost");Check(c.ReturnPanel.HasReport,"Report lost");
        int nails=c.CraftPanel.Available("nails"),scrap=c.CraftPanel.Materials.First(m=>m.Id=="scrap").Initial;
        c.Campaign.AdvanceSettlementTime(20);Check(c.CraftPanel.Available("nails")==nails+2 && c.CraftPanel.Materials.First(m=>m.Id=="scrap").Initial==scrap-2,"Restore duplicated or lost craft reward/cost");
        c.Campaign.AdvanceSettlementTime(20);Check(c.CraftPanel.Available("nails")==nails+2,"Duplicate completion");
        await Tap(c.Journal);Check(c.ReturnPanel.IsOpen,"Restored report open");await Tap(c.ReturnPanel.Back);
        // Corrupt and incompatible snapshots never replace the live campaign or enable loading.
        File.WriteAllText(CampaignSaveStore.SlotPath(1),"broken-json");var bad=CampaignSaveStore.Read(1,c);Check(bad.Exists && !bad.CanLoad && bad.Error!=null,"Corrupt file accepted");
        var invalid=CampaignPersistence.Capture(c);invalid.Version=99;Check(!CampaignSaveStore.Write(2,invalid,c,out _) && !File.Exists(CampaignSaveStore.SlotPath(2)),"Future version accepted");
        invalid=CampaignPersistence.Capture(c);invalid.Members[0].Bag=new[]{new SavedCount{Id="ammo",Count=99999}};Check(!CampaignSaveStore.Write(2,invalid,c,out _),"Inconsistent inventory accepted");
        await Tap(c.GameMenu.OpenButton);await Tap(c.GameMenu.Load);panel=c.GameMenu.SavePanel;await Tap(panel.Slots[1]);Check(!panel.Action.interactable,"Corrupt slot load enabled");Bounds(panel.gameObject);await Tap(panel.Back);
        await Tap(c.GameMenu.Title);await Tap(c.GameMenu.Cancel);Check(c.GameMenu.Page.activeSelf,"Title cancel");await Tap(c.GameMenu.Title);await Tap(c.GameMenu.Confirm);await Task.Delay(600);
        var title=Object.FindAnyObjectByType<TitleMenuController>();Check(title.ContinueButton.interactable,"Continue not enabled");await Tap(title.ContinueButton);await Task.Delay(900);c=Owner();Check(Comparable(CampaignPersistence.Capture(c))==before,"Continue selected wrong state");
        await Tap(c.GameMenu.OpenButton);await Tap(c.GameMenu.Save);await Tap(c.GameMenu.SavePanel.Slots[0]);Bounds(c.GameMenu.SavePanel.gameObject);
        return "PASS: actual UI save/overwrite/cancel/load/continue; full state equivalence including partial jobs, bags, shared stock, partial/completed searches and report; once-only costs/rewards; field save refusal; corrupt/future/inconsistent data rejection; menu input gate and visible text bounds. Isolated Temp/SystemMenuVerification slots retained for restart check.";
    }
    public static async Task<string> Settings()
    {
        var c=Owner();if(c.GameMenu.SavePanel.IsOpen)c.GameMenu.SavePanel.Close();if(!c.GameMenu.IsOpen)c.GameMenu.Open();
        var d=c.GameMenu.SettingsDialog;
        bool hv=PlayerPrefs.HasKey(GameSettings.VolumeKey),hf=PlayerPrefs.HasKey(GameSettings.FullscreenKey);float stored=PlayerPrefs.GetFloat(GameSettings.VolumeKey),live=AudioListener.volume;int full=PlayerPrefs.GetInt(GameSettings.FullscreenKey);bool screen=Screen.fullScreen;
        try
        {
            await Tap(c.GameMenu.Settings);d.Volume.value=.21f;Check(Mathf.Abs(AudioListener.volume-.21f)<.001f,"Preview volume");Bounds(d.gameObject);await Tap(d.Cancel);Check(Mathf.Abs(AudioListener.volume-live)<.001f,"Cancel volume");
            await Tap(c.GameMenu.Settings);d.Volume.value=.43f;await Tap(d.Apply);Check(Mathf.Abs(PlayerPrefs.GetFloat(GameSettings.VolumeKey)-.43f)<.001f,"Persist volume");
            await Tap(c.GameMenu.Settings);Check(Mathf.Abs(d.Volume.value-.43f)<.001f,"Reopen volume");await Tap(d.Cancel);
            await Tap(c.GameMenu.Title);await Tap(c.GameMenu.Confirm);await Task.Delay(600);var t=Object.FindAnyObjectByType<TitleMenuController>();await Tap(t.SettingsButton);Check(Mathf.Abs(t.Volume.value-.43f)<.001f,"Shared title setting");Bounds(t.SettingsPanel);await Tap(t.SettingsCancel);
            await Tap(t.LoadButton);Check(t.SaveSlots.IsOpen,"Title slots");Bounds(t.LoadPanel);await Tap(t.LoadClose);
            await Tap(t.NewGameButton);await Task.Delay(750);Check(SceneManager.GetActiveScene().name=="PartySelection" && PartySelectionSession.Selected.Count==0 && CampaignPersistence.Pending==null,"New game inherited save");
            Check(CampaignSaveStore.Read(0,UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab").GetComponent<SettlementController>()).CanLoad,"New game deleted saved slot");
            return "PASS: in-game settings preview/cancel/apply/persistence/title sharing, title load slots, fresh new game without deleting saves. Original preferences restored.";
        }
        finally
        {
            if(hv)PlayerPrefs.SetFloat(GameSettings.VolumeKey,stored);else PlayerPrefs.DeleteKey(GameSettings.VolumeKey);
            if(hf)PlayerPrefs.SetInt(GameSettings.FullscreenKey,full);else PlayerPrefs.DeleteKey(GameSettings.FullscreenKey);PlayerPrefs.Save();AudioListener.volume=live;Screen.fullScreen=screen;
        }
    }
    public static async Task<string> Restart()
    {
        CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/SystemMenuVerification");SceneManager.LoadScene("StartMenu");await Task.Delay(650);
        var t=Object.FindAnyObjectByType<TitleMenuController>();Check(t.ContinueButton.interactable,"Restart did not discover save");await Tap(t.ContinueButton);await Task.Delay(800);
        var c=Owner();Check(c.WorkPanel.Orders.Count==1 && c.CraftPanel.Orders.Count==1 && c.ArrivalPanel.Loot.State(0).Progress==1 && c.ReturnPanel.HasReport,"Restart restoration incomplete");
        await Tap(c.GameMenu.OpenButton);await Tap(c.GameMenu.Save);await Tap(c.GameMenu.SavePanel.Slots[0]);return "PASS: new Play session restores disk save, unfinished jobs, search progress and return report. Save screen ready for capture.";
    }
    public static string ShowSettings(){var c=Owner();if(c.GameMenu.SavePanel.IsOpen)c.GameMenu.SavePanel.Close();c.GameMenu.Settings.onClick.Invoke();return "Settings ready.";}
    public static string ShowMenu(){var c=Owner();if(c.GameMenu.SettingsDialog.gameObject.activeSelf)c.GameMenu.SettingsDialog.CancelChanges();if(c.GameMenu.SavePanel.IsOpen)c.GameMenu.SavePanel.Close();c.GameMenu.Open();return "Menu ready.";}
    public static string ShowSlots(){ShowMenu();var c=Owner();c.GameMenu.Save.onClick.Invoke();c.GameMenu.SavePanel.Select(0);return "Save slots ready. Slot 2 is deliberately corrupt verification data, not a player save.";}
    public static string ShowCleanSlots()
    {
        Check(CampaignSaveStore.TestDirectory==Path.GetFullPath("Temp/SystemMenuVerification"),"Only isolated verification slots may be cleared");
        string file=CampaignSaveStore.SlotPath(1);if(File.Exists(file))File.Delete(file);
        ShowSlots();return "Saved and empty slot preview ready; isolated corrupt fixture removed.";
    }
    public static string Cleanup(){CampaignSaveStore.TestDirectory=null;return "Test path override cleared; player storage restored.";}
    public static async Task<string> EscapeFlow()
    {
        var c=Owner();if(c.GameMenu.SettingsDialog.gameObject.activeSelf)c.GameMenu.SettingsDialog.CancelChanges();if(c.GameMenu.SavePanel.IsOpen)c.GameMenu.SavePanel.Close();if(c.GameMenu.IsOpen)c.GameMenu.Close();await Task.Delay(100);
        await Escape();Check(c.GameMenu.IsOpen,"Escape did not open menu");await Tap(c.GameMenu.Settings);await Escape();Check(c.GameMenu.IsOpen && c.GameMenu.Page.activeSelf && !c.GameMenu.SettingsDialog.gameObject.activeSelf,"Escape escaped two settings levels");
        await Tap(c.GameMenu.Load);await Tap(c.GameMenu.SavePanel.Slots[0]);await Tap(c.GameMenu.SavePanel.Action);await Escape();Check(c.GameMenu.SavePanel.IsOpen && !c.GameMenu.SavePanel.Review.activeSelf,"Escape skipped load confirmation");
        await Escape();Check(c.GameMenu.IsOpen && c.GameMenu.Page.activeSelf,"Escape skipped save list parent");await Escape();Check(!c.GameMenu.IsOpen && c.Main.interactable && c.Main.alpha==1,"Escape menu close");
        await Tap(c.Bed);await Escape();Check(!c.WorkPanel.IsOpen && !c.GameMenu.IsOpen && c.Main.interactable,"Closing work panel opened menu on same Escape");
        return "PASS: queued keyboard Escape opens/closes one menu level, cancels confirmation, restores HUD, and does not reopen menu after another panel closes.";
    }
    static async Task Escape()
    {
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Escape));await Task.Delay(100);
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,new UnityEngine.InputSystem.LowLevel.KeyboardState());await Task.Delay(100);
    }
}
