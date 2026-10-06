using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;
public static class VerifyVisitor
{
    static void Check(bool value,string text){if(!value)throw new Exception(text);}
    static SettlementController Owner()=>Object.FindAnyObjectByType<SettlementController>();
    static int Stock(SettlementController c,string id)=>c.CraftPanel.Materials.First(m=>m.Id==id).Initial;
    static void Bounds(GameObject g){Canvas.ForceUpdateCanvases();foreach(var t in g.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height,"Text overflow "+t.name+": "+t.text);}
    static async Task Tap(Button b){await Task.Delay(60);Check(b&&b.IsActive()&&b.IsInteractable(),"Unavailable "+b?.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(90);}
    static Task Pick(SettlementVisitorPanel v,bool ours,string id)=>Tap((ours?v.OurSlots:v.TheirSlots).First(s=>s.gameObject.activeSelf&&s.Label.text==Owner().CraftPanel.Materials.First(m=>m.Id==id).Name).Button);
    public static async Task<string> Flow()
    {
        var c=Owner();var v=c.VisitorPanel;Check(c.Campaign.Day==1&&c.Campaign.MinuteOfDay<720,"Need fresh settlement");
        Bounds(c.Main.gameObject);await Tap(v.OpenButton);Check(!v.IsPresent&&!v.StartTrade.interactable&&v.Status.text.Contains("DAY 1"),"Pre-arrival state");Bounds(v.View);await Tap(v.Back);Check(c.Main.alpha==1&&c.Main.interactable,"HUD restore");
        c.Campaign.AdvanceSettlementTime(720-c.Campaign.MinuteOfDay);Check(v.IsPresent&&v.StockFor("water")==4,"Arrival stock");
        await Tap(v.OpenButton);Check(c.Main.alpha==0&&!c.Main.interactable,"HUD hiding");await Tap(v.StartTrade);Bounds(v.View);
        await Pick(v,true,"wood");await Pick(v,false,"cloth");int wood=Stock(c,"wood"),cloth=Stock(c,"cloth"),time=c.Campaign.MinuteOfDay;
        await Tap(v.Offer);Bounds(v.Review);await Tap(v.Cancel);Check(Stock(c,"wood")==wood&&v.StockFor("cloth")==6,"Cancelled offer spent stock");
        await Tap(v.Offer);await Tap(v.Confirm);v.Commit();Check(Stock(c,"wood")==wood-1&&Stock(c,"cloth")==cloth+1&&v.StockFor("wood")==7&&v.StockFor("cloth")==5&&c.Campaign.MinuteOfDay==time,"Atomic once-only barter");
        await Tap(v.TradeBack);await Tap(v.Back);await Tap(v.OpenButton);await Tap(v.StartTrade);Check(v.StockFor("cloth")==5,"Reopen refilled");
        await Pick(v,true,"wood");await Pick(v,false,"food");Check(!v.Offer.interactable,"Unequal quote accepted");await Pick(v,false,"wood");Check(!v.Offer.interactable,"Same-item trade");
        await Pick(v,true,"cloth");await Pick(v,false,"water");await Tap(v.Offer);c.CraftPanel.Materials.First(m=>m.Id=="cloth").Initial=0;await Tap(v.Confirm);Check(v.StockFor("water")==4&&Stock(c,"water")==4,"Stale quote consumed");c.CraftPanel.Materials.First(m=>m.Id=="cloth").Initial=cloth+1;
        await Tap(v.TradeBack);await Tap(v.Back);
        var party=c.Campaign.Party.ToArray();Check(c.InventoryPanel.MoveFor(party[0],"cloth",1,true),"Put cloth in bag");
        await Tap(c.Workbench);c.CraftPanel.FocusRecipe("water");await Tap(c.CraftPanel.WorkerRows[0].Button);await Tap(c.CraftPanel.Confirm);
        await Tap(v.OpenButton);await Tap(v.StartTrade);Check(c.CraftPanel.Available("wood")==0&&!v.OurSlots.Any(s=>s.gameObject.activeSelf&&s.Label.text==c.CraftPanel.Materials.First(m=>m.Id=="wood").Name),"Reserved wood exposed");
        await Pick(v,true,"cloth");await Pick(v,false,"water");await Tap(v.GivePlus);await Tap(v.GivePlus);Check(!v.Offer.interactable,"Personal bag sold");await Tap(v.GiveMinus);await Tap(v.GiveMinus);
        Check(v.Offer.interactable,"Free negotiator not selected");await Tap(v.PreviousMember);Check(!v.Offer.interactable,"Busy negotiator accepted");await Tap(v.NextMember);await Tap(v.Offer);party[1].Health=0;await Tap(v.Confirm);Check(v.StockFor("water")==4,"Dead negotiator quote committed");party[1].Health=party[1].MaxHealth;
        await Tap(v.TradeBack);await Tap(v.Back);
        CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/VisitorRestartVerification");var saved=CampaignPersistence.Capture(c);Check(CampaignSaveStore.Write(0,saved,c,out var error),error);var disk=CampaignSaveStore.Read(0,c);Check(disk.CanLoad&&disk.Data.Version==CampaignPersistence.CurrentVersion&&disk.Data.Visitor.Stock.First(x=>x.Id=="cloth").Count==5,"Disk stock roundtrip "+disk.Error);
        var bad=JsonUtility.FromJson<CampaignSaveData>(JsonUtility.ToJson(saved));bad.Visitor.Stock[0].Count=-1;Check(!CampaignSaveStore.Write(1,bad,c,out _),"Negative visitor stock accepted");
        foreach(int version in new[]{1,2,3}){var old=JsonUtility.FromJson<CampaignSaveData>(JsonUtility.ToJson(saved));old.Version=version;old.Visitor=null;old.Craft=Array.Empty<SavedCraft>();if(version==1){old.Cooking=null;old.Materials=old.Materials.Where(x=>!CampaignPersistence.FoodIds.Contains(x.Id)).ToArray();}if(version<3)old.Materials=old.Materials.Where(x=>!CampaignPersistence.LifeIds.Contains(x.Id)).ToArray();CampaignPersistence.Upgrade(old,c);Check(old.Version==CampaignPersistence.CurrentVersion&&old.Visitor.VisitDay==0&&old.Visitor.Stock.Length==0,"Old visitor migration");}
        CampaignPersistence.Prepare(disk.Data,c);SceneManager.LoadScene("Settlement");await Task.Delay(700);c=Owner();v=c.VisitorPanel;Check(v.StockFor("cloth")==5&&v.StockFor("wood")==7&&c.CraftPanel.Orders.Count==1,"Restore restocked/lost work");
        await Tap(v.OpenButton);await Tap(v.StartTrade);await Pick(v,true,"cloth");await Pick(v,false,"water");await Tap(v.Offer);c.Campaign.AdvanceSettlementTime(360);await Tap(v.Confirm);Check(!v.IsPresent&&v.StockFor("water")==4,"Expired quote committed");await Tap(v.TradeBack);await Tap(v.Back);
        c.Campaign.AdvanceSettlementTime(1440);Check(!v.IsPresent&&v.VisitDay==1,"Even-day visitor");c.Campaign.AdvanceSettlementTime(1080);Check(v.IsPresent&&v.VisitDay==3&&v.StockFor("cloth")==6,"Next visit no fresh stock");
        await Tap(v.OpenButton);await Tap(v.Dismiss);await Tap(v.Cancel);Check(v.IsPresent,"Cancel dismissal ended visit");await Tap(v.Dismiss);await Tap(v.Confirm);Check(!v.IsPresent,"Dismiss failed");await Tap(v.Back);
        var dismissed=CampaignPersistence.Capture(c);CampaignPersistence.Prepare(dismissed,c);SceneManager.LoadScene("Settlement");await Task.Delay(700);c=Owner();v=c.VisitorPanel;Check(!v.IsPresent,"Dismissed visitor returned on load");
        c.Campaign.AdvanceSettlementTime(4*1440);Check(v.VisitDay==7&&v.IsPresent&&v.StockFor("water")==4,"Skipped days accumulated stock");
        Check(c.ArrivalPanel.Begin(c.Campaign.Party.ToArray(),c.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")),"Field begin");await Task.Delay(800);v.Open();Check(!v.View.activeSelf,"Visitor opened in field");await Tap(c.ArrivalPanel.Return);await Tap(c.ArrivalPanel.ReturnConfirm);await Tap(c.ReturnPanel.Back);
        return "PASS: schedule, actual raycast clicks, text bounds, HUD restore, cancel/confirm and duplicate guard, stock/value/capacity protections, reservations and bags excluded, busy/dead negotiator, disk + scene restore, v1/v2/v3 migration, expiry and dismissal, skipped days and field gate. Restart fixture saved separately.";
    }
    public static async Task<string> Restart()
    {
        CampaignSaveStore.TestDirectory=Path.GetFullPath("Temp/VisitorRestartVerification");SceneManager.LoadScene("StartMenu");await Task.Delay(650);var t=Object.FindAnyObjectByType<TitleMenuController>();await Tap(t.ContinueButton);await Task.Delay(750);var c=Owner();var v=c.VisitorPanel;
        Check(c.Campaign.Day==1&&v.IsPresent&&v.StockFor("cloth")==5&&v.StockFor("wood")==7&&c.CraftPanel.Orders.Count==1,"Fresh Play restore");await Tap(v.OpenButton);Bounds(v.View);return "PASS: fresh Play -> title Continue -> changed visitor stock + reserved craft restored, no refill. Conversation ready.";
    }
    public static async Task<string> TradePreview(){var c=Owner();var v=c.VisitorPanel;await Tap(v.StartTrade);await Pick(v,true,"cloth");await Pick(v,false,"water");Bounds(v.View);return "Trade preview ready.";}
    public static async Task<string> FinalPreview(){await Restart();await TradePreview();return "PASS: final prefab text bounds, stock restore and trade clicks; ready for screenshot.";}
    public static async Task<string> StockVisibility()
    {
        await FinalPreview();var c=Owner();var v=c.VisitorPanel;
        Check(v.OurSlots.Count(s=>s.gameObject.activeSelf)==v.Goods.Count(g=>c.CraftPanel.Available(g.Id)>0),"Our zero stock exposed");
        Check(v.TheirSlots.Count(s=>s.gameObject.activeSelf)==v.Goods.Count(g=>v.StockFor(g.Id)>0),"Merchant zero stock exposed");
        Check(v.OurSlots.TakeWhile(s=>s.gameObject.activeSelf).Count()==v.OurSlots.Count(s=>s.gameObject.activeSelf),"Grid has holes");
        await Pick(v,true,"cloth");await Pick(v,false,"water");await Tap(v.GivePlus);await Tap(v.TakePlus);await Tap(v.Offer);await Tap(v.Confirm);
        Check(Stock(c,"cloth")==0&&!v.OurSlots.Any(s=>s.gameObject.activeSelf&&s.Label.text=="천 조각"),"Sold-out selection remains");
        Check(!v.GiveDetails.text.Contains("천 조각")&&v.GiveQuantity.text=="−1","Selection not repaired");
        foreach(var m in c.CraftPanel.Materials)m.Initial=0;
        var empty=v.Export();foreach(var item in empty.Stock)item.Count=0;v.Restore(empty);v.Sync();
        Check(v.OurSlots.All(s=>!s.gameObject.activeSelf)&&v.TheirSlots.All(s=>!s.gameObject.activeSelf)&&!v.Offer.interactable&&!v.GivePlus.interactable&&!v.TakePlus.interactable,"Empty inventory controls");v.RequestOffer();Check(!v.Review.activeSelf,"Empty proposal opened");Bounds(v.View);
        await FinalPreview();return "PASS: positive stock only, compact slots with correct item clicks, sold-out item removal and reselection, empty inventory controls/text, restored preview.";
    }
    public static async Task<string> ConfirmPreview(){var v=Owner().VisitorPanel;await Tap(v.Offer);Bounds(v.Review);return "Confirmation preview ready.";}
    static async Task Escape(){UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Escape));await Task.Delay(120);UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,new UnityEngine.InputSystem.LowLevel.KeyboardState());await Task.Delay(120);}
    public static async Task<string> Edges()
    {
        var c=Owner();var v=c.VisitorPanel;Check(v.Review.activeSelf,"Need confirm preview");int cloth=Stock(c,"cloth"),water=Stock(c,"water");
        await Escape();Check(!v.Review.activeSelf&&v.Trade.activeSelf&&Stock(c,"cloth")==cloth,"Esc confirmation");await Escape();Check(v.Conversation.activeSelf&&v.View.activeSelf,"Esc trade");await Escape();Check(!v.View.activeSelf&&!c.GameMenu.View.activeSelf&&c.Main.interactable,"Esc leaked to menu");
        await Tap(v.OpenButton);await Tap(v.StartTrade);await Pick(v,true,"cloth");await Pick(v,false,"water");await Tap(v.Offer);c.CraftPanel.Materials.First(m=>m.Id=="water").Initial=1000000;await Tap(v.Confirm);Check(Stock(c,"cloth")==cloth&&v.StockFor("water")==4,"Warehouse overflow");c.CraftPanel.Materials.First(m=>m.Id=="water").Initial=water;
        await Pick(v,false,"water");await Tap(v.Offer);var state=v.Export();state.Stock.First(s=>s.Id=="water").Count=0;v.Restore(state);await Tap(v.Confirm);Check(Stock(c,"cloth")==cloth&&Stock(c,"water")==water,"Stale merchant stock");
        await Tap(v.TradeBack);await Tap(v.Dismiss);c.Campaign.AdvanceSettlementTime(2*1440);await Tap(v.Confirm);Check(v.IsPresent&&v.VisitDay==3,"Old dismissal ended new visit");await Tap(v.Back);
        return "PASS: actual Escape one-level navigation without reopening system menu, full warehouse, merchant stock changed after quote, stale dismissal cannot end next visit.";
    }
    public static string Cleanup(){CampaignSaveStore.TestDirectory=null;return "Test save redirection removed.";}
}



