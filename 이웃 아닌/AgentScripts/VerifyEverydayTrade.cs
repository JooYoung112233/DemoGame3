using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Disposable new-campaign fixture. Run only in Play Mode after everyday catalog/UI builders.
// Uses real UI raycasts; no production save slots, random exploration or shared editor commands.
public static class VerifyEverydayTrade
{
    static readonly string[] NewIds={"fuel","lubricant","battery","tobacco","coffee","alcohol","electrical-parts","weapon-parts"};
    static string TestDirectory=>Path.GetFullPath("Temp/EverydayTradeVerification");
    static SettlementController C=>Object.FindAnyObjectByType<SettlementController>();
    static SettlementVisitorPanel V=>C.VisitorPanel;
    static readonly List<string> checks=new List<string>();
    static readonly List<string> shots=new List<string>();
    static void Check(bool valid,string message){if(!valid)throw new InvalidOperationException(message);}
    static int Stock(string id)=>C.CraftPanel.Materials.First(m=>m.Id==id).Initial;
    static void SetStock(string id,int value)=>C.CraftPanel.Materials.First(m=>m.Id==id).Initial=value;

    public static string Prices()
    {
        var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/VisitorPanel.prefab");
        var catalog=prefab.GetComponent<SettlementVisitorPanel>();
        var go=new GameObject("TemporaryTradePriceCheck");
        try
        {
            var v=go.AddComponent<SettlementVisitorPanel>();v.Goods=catalog.Goods;v.FirstDay=catalog.FirstDay;v.IntervalDays=catalog.IntervalDays;
            Check(NewIds.All(id=>v.Goods.Any(g=>g.Id==id)),"Missing new goods in visitor catalog.");
            for(int visit=0;visit<4;visit++)
            {
                int day=v.FirstDay+visit*Math.Max(1,v.IntervalDays);
                v.Restore(new SavedVisitor{VisitDay=day});
                Check(v.PreferredGoodId==(visit%2==0?"tobacco":"coffee"),"Demand does not alternate by visit.");
                foreach(var good in v.Goods)
                {
                    Check(v.BuyValueFor(good.Id)<=v.SellValueFor(good.Id),"Buying from and selling to the visitor increases value: "+good.Id);
                    Check(v.BuyValueFor(good.Id)==Math.Max(1,good.Value)+(good.Id==v.PreferredGoodId?1:0),"Wrong demand premium: "+good.Id);
                    foreach(var other in v.Goods.Where(g=>g.Id!=good.Id))
                    {
                        // Exact integer quantities cannot improve a two-way exchange either.
                        for(int amount=1;amount<=99;amount++)
                        {
                            int bought=amount*v.BuyValueFor(good.Id)/v.SellValueFor(other.Id);
                            int returned=bought*v.BuyValueFor(other.Id)/v.SellValueFor(good.Id);
                            Check(returned<=amount,"Profitable round trip: "+good.Id+" -> "+other.Id);
                        }
                    }
                }
            }
        }
        finally{Object.DestroyImmediate(go);}
        return "PASS: all 22 goods, four visits and integer two-way exchanges 1..99; preferred buy <= sale; demand derives from saved visit date.";
    }

    public static async Task<string> Run()
    {
        Check(Application.isPlaying,"Enter Play Mode first.");checks.Clear();shots.Clear();string failure=null;
        try
        {
            checks.Add(Prices());await Start();
            await Tap(V.StartTrade);await InspectAllPages();
            Check(V.OurPageCount==2&&V.TheirPageCount==2,"Twenty-two goods did not create two pages.");
            await Select(true,"prybar");await Select(false,"fuel");
            int beforeOur=Stock("prybar"),beforeFuel=Stock("fuel"),beforeTheir=V.StockFor("fuel");
            await Tap(V.Offer);await Tap(V.Cancel);
            Check(Stock("prybar")==beforeOur&&Stock("fuel")==beforeFuel&&V.StockFor("fuel")==beforeTheir,"Cancel moved stock.");
            foreach(string id in NewIds)
            {
                int before=Stock(id),merchant=V.StockFor(id),funding=Stock("prybar");
                await Offer("prybar",id,1,1);
                Check(V.ConfirmBody.text.Contains("매입")&&V.ConfirmBody.text.Contains("판매"),"Confirmation hides distinct buy/sell values.");
                await Tap(V.Confirm);V.Commit();
                Check(Stock(id)==before+1&&V.StockFor(id)==merchant-1&&Stock("prybar")==funding-1,"Trade did not commit exactly once: "+id);
            }
            checks.Add("All eight everyday goods exchanged through item/page/quantity/confirm buttons; cancellation and duplicate Commit preserved stock.");

            string preference=V.PreferredGoodId;int premium=V.BuyValueFor(preference);
            int ownPreferred=Stock(preference),ownWood=Stock("wood");
            await Offer(preference,"wood",1,premium);await Tap(V.Confirm);
            await Offer("wood",preference,premium,1);await Tap(V.Confirm);
            Check(Stock(preference)==ownPreferred&&Stock("wood")==ownWood,"An immediate preferred-item round trip generated goods.");
            checks.Add("Preferred demand is visible in detail and exact immediate reverse exchange produces no goods.");

            var saved=V.Export();string savedJson=JsonUtility.ToJson(saved);
            await Tap(V.TradeBack);await Tap(V.Back);V.Restore(JsonUtility.FromJson<SavedVisitor>(savedJson));V.Sync();V.Open();await Tap(V.StartTrade);
            Check(V.PreferredGoodId==preference&&JsonUtility.ToJson(V.Export())==savedJson,"Restore/reopen rerolled demand or refilled inventory.");
            await Offer("prybar","fuel",1,1);int staleOwn=Stock("prybar"),staleFuel=Stock("fuel");
            var depleted=V.Export();depleted.Stock.First(s=>s.Id=="fuel").Count=0;V.Restore(depleted);
            await Tap(V.Confirm);
            Check(Stock("prybar")==staleOwn&&Stock("fuel")==staleFuel,"Stale quote bought sold-out fuel.");
            await InspectAllPages();var remaining=await VisibleIds(false);Check(!remaining.Contains("fuel"),"Sold-out fuel remains visible.");
            checks.Add("Restore/reopen preserves stock and preferred item; sold-out stale quote does not commit and depleted items disappear.");

            var disk=CampaignPersistence.Capture(C);
            Check(CampaignSaveStore.Write(0,disk,C,out var error),error);
            var loaded=CampaignSaveStore.Read(0,C);Check(loaded.CanLoad,"Everyday trade save did not load: "+loaded.Error);
            Check(JsonUtility.ToJson(loaded.Data.Visitor)==JsonUtility.ToJson(disk.Visitor),"Disk roundtrip changed visitor stock.");
            checks.Add("Current-version disk roundtrip keeps all 22 trade stock records in an isolated verification directory.");

            await PreviewSizes();
            foreach(var good in V.Goods)SetStock(good.Id,0);
            var empty=V.Export();foreach(var count in empty.Stock)count.Count=0;V.Restore(empty);V.Sync();
            Check(V.OurSlots.All(s=>!s.gameObject.activeSelf)&&V.TheirSlots.All(s=>!s.gameObject.activeSelf)&&!V.Offer.interactable,"Empty stock remains tradable.");
            Check(V.OurPage==0&&V.TheirPage==0&&!V.OurNextPage.interactable&&!V.OurPreviousPage.interactable&&!V.TheirNextPage.interactable&&!V.TheirPreviousPage.interactable,"Empty page navigation is not repaired.");
            Check(V.OurPageLabel.text=="재고 없음"&&V.TheirPageLabel.text=="재고 없음","Empty page explanation missing.");
            checks.Add("Zero-stock lists hide every item and repair selection, pagination and trade buttons.");
            // Restore the photographed trade state so the editor remains useful for final inspection.
            foreach(var material in disk.Materials)SetStock(material.Id,material.Count);V.Restore(disk.Visitor);V.Sync();
            await Select(true,preference);await Select(false,"alcohol");
        }
        catch(Exception error){failure=error.ToString();}
        string output=Path.GetFullPath("아트/리소스검토/everyday-trade-runtime.json");
        File.WriteAllText(output,Newtonsoft.Json.JsonConvert.SerializeObject(new{utc=DateTime.UtcNow.ToString("O"),result=failure==null?"PASS":"FAIL",checks,screenshots=shots,failure},Newtonsoft.Json.Formatting.Indented));
        Check(failure==null,output+"\n"+failure);return "PASS\n"+string.Join("\n",checks)+"\n"+output;
    }
    static async Task Start()
    {
        CampaignSaveStore.TestDirectory=TestDirectory;PartySelectionSession.Clear();SceneManager.LoadScene("PartySelection");await Task.Delay(700);
        var selection=Object.FindAnyObjectByType<PartySelectionController>();Check(selection,"Party selection missing.");
        foreach(var card in selection.Cards.Where(c=>c.gameObject.activeInHierarchy&&c.Button.IsInteractable()))
        {if(PartySelectionSession.Selected.Count>=2)break;await Tap(card.Button);}
        await Tap(selection.Continue);await Task.Delay(650);
        var home=Object.FindAnyObjectByType<HomeSelectionController>();Check(home,"Home selection missing.");
        await Tap(home.Cards[0].Button);await Tap(home.Continue);await Task.Delay(900);
        C.Introduction.Restore(10);C.Development.State.Warehouse=2;
        foreach(var good in V.Goods)SetStock(good.Id,2);SetStock("prybar",18);
        C.Campaign.AdvanceSettlementTime(Math.Max(0,V.ArrivalMinute-C.Campaign.MinuteOfDay));V.Sync();Check(V.IsPresent,"First visitor has not arrived.");
        var state=V.Export();foreach(var count in state.Stock)count.Count=count.Id=="wood"?10:3;V.Restore(state);V.Open();await Task.Delay(100);
        Check(V.Goods.Length==22,"Expected 14 existing plus eight everyday goods.");
    }
    static async Task Offer(string give,string take,int giving,int taking)
    {
        await Select(true,give);await Select(false,take);
        for(int i=1;i<giving;i++)await Tap(V.GivePlus);for(int i=1;i<taking;i++)await Tap(V.TakePlus);
        Check(V.GiveDetails.text.Contains("매입")&&V.TakeDetails.text.Contains("판매"),"Details do not distinguish buying and selling.");
        await Tap(V.Offer);Check(V.Review.activeSelf,"Offer confirmation missing.");
    }
    static async Task Select(bool ours,string id)
    {
        var previous=ours?V.OurPreviousPage:V.TheirPreviousPage;while(previous.interactable)await Tap(previous);
        string name=C.CraftPanel.Materials.First(m=>m.Id==id).Name;
        for(int page=0;page<20;page++)
        {
            var slot=(ours?V.OurSlots:V.TheirSlots).FirstOrDefault(s=>s.gameObject.activeInHierarchy&&s.Label.text==name);
            if(slot){await Tap(slot.Button);return;}
            var next=ours?V.OurNextPage:V.TheirNextPage;if(!next.interactable)break;await Tap(next);
        }
        throw new InvalidOperationException("Cannot reach trade item "+id+" / ours="+ours);
    }
    static async Task<HashSet<string>> VisibleIds(bool ours)
    {
        var ids=new HashSet<string>();var previous=ours?V.OurPreviousPage:V.TheirPreviousPage;while(previous.interactable)await Tap(previous);
        for(int page=0;page<20;page++)
        {
            foreach(var slot in (ours?V.OurSlots:V.TheirSlots).Where(s=>s.gameObject.activeInHierarchy))
            {
                string id=C.CraftPanel.Materials.Single(m=>m.Name==slot.Label.text).Id;
                Check((ours?C.CraftPanel.Available(id):V.StockFor(id))>0,"Zero stock exposed: "+id);
                Check(ids.Add(id),"Duplicate trade item across pages: "+id);Fits(slot.Label);Hit(slot.Button);
            }
            var next=ours?V.OurNextPage:V.TheirNextPage;if(!next.interactable)break;await Tap(next);
        }
        return ids;
    }
    static async Task InspectAllPages()
    {
        foreach(bool ours in new[]{true,false})
        {
            var expected=V.Goods.Where(g=>(ours?C.CraftPanel.Available(g.Id):V.StockFor(g.Id))>0).Select(g=>g.Id);
            Check((await VisibleIds(ours)).SetEquals(expected),"Paged trade omits stock on "+(ours?"our":"visitor")+" side.");
        }
    }
    static void Fits(Text text)
    {
        Check(text.preferredHeight<=text.rectTransform.rect.height+2,"Trade text overflow: "+text.name+" / "+text.text+" / "+text.preferredHeight+" > "+text.rectTransform.rect.height);
    }
    static PointerEventData Hit(Button button)
    {
        Canvas.ForceUpdateCanvases();Check(button&&button.IsActive()&&button.IsInteractable(),"Unavailable button: "+button?.name);
        var canvas=button.GetComponentInParent<Canvas>();var rect=(RectTransform)button.transform;
        var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Trade hit blocked: "+button.name+" / "+(hits.Count>0?hits[0].gameObject.name:"none"));return e;
    }
    static async Task Tap(Button button){await Task.Delay(75);ExecuteEvents.Execute(button.gameObject,Hit(button),ExecuteEvents.pointerClickHandler);await Task.Delay(75);}
    static async Task PreviewSizes()
    {
        const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        var assembly=typeof(UnityEditor.Editor).Assembly;var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var sizes=typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
        var group=sizesType.GetMethod("GetGroup",flags).Invoke(sizes,new[]{sizesType.GetProperty("currentGroupType",flags).GetValue(sizes)});
        var view=UnityEditor.EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));var selected=view.GetType().GetProperty("selectedSizeIndex",flags);int original=(int)selected.GetValue(view);
        var type=assembly.GetType("UnityEditor.GameViewSize");
        try
        {
            foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,800)})
            {
                int count=(int)group.GetType().GetMethod("GetTotalCount",flags).Invoke(group,null),index=-1;
                for(int i=0;i<count;i++){var candidate=group.GetType().GetMethod("GetGameViewSize",flags).Invoke(group,new object[]{i});if((int)type.GetProperty("width",flags).GetValue(candidate)==size.x&&(int)type.GetProperty("height",flags).GetValue(candidate)==size.y){index=i;break;}}
                Check(index>=0,"Add fixed GameView size before review: "+size);selected.SetValue(view,index);view.Repaint();await Task.Delay(650);
                Check(Screen.width==size.x&&Screen.height==size.y,"GameView size did not apply.");
                await InspectAllPages();await Select(true,V.PreferredGoodId);await Select(false,"alcohol");
                foreach(var text in new[]{V.Status,V.GiveDetails,V.TakeDetails,V.Balance,V.OurPageLabel,V.TheirPageLabel})Fits(text);
                string name="everyday-trade-"+size.x+"x"+size.y;string native=Path.GetFullPath("Temp/"+name+".png");if(File.Exists(native))File.Delete(native);
                ScreenCapture.CaptureScreenshot(native);for(int i=0;i<40&&!File.Exists(native);i++)await Task.Delay(100);Check(File.Exists(native),"Capture missing.");await Task.Delay(150);
                string output=Path.GetFullPath("아트/리소스검토/"+name+".png");File.Copy(native,output,true);shots.Add(output);
            }
        }
        finally{selected.SetValue(view,original);view.Repaint();await Task.Delay(150);}
        checks.Add("Native 1920×1080 and 1280×800: both pages' item hits/names plus price/details/page text fit; screenshots saved unchanged.");
    }
    public static string Finish()
    {
        Check(!Application.isPlaying,"Stop Play Mode first.");if(CampaignSaveStore.TestDirectory==TestDirectory)CampaignSaveStore.TestDirectory=null;
        return "Everyday trade verification save override cleared.";
    }
}
