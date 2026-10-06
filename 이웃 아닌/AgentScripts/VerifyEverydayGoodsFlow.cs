using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Isolated runtime fixture. Actual mall search/loot transfer and home inventory are exercised;
// review stock and completed facilities are explicitly seeded only after that acquisition flow.
public static class VerifyEverydayGoodsFlow
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    const string Output="아트/리소스검토/";
    static readonly string[] Ids={"fuel","lubricant","battery","tobacco","coffee","alcohol","electrical-parts","weapon-parts"};
    static SettlementController C=>Object.FindAnyObjectByType<SettlementController>();
    static string TestDirectory=>Path.GetFullPath("Temp/EverydayGoodsFlowSlots");
    static readonly List<string> checks=new List<string>(),shots=new List<string>();
    static void Check(bool v,string message){if(!v)throw new Exception(message);}
    static string Name(string id)=>C.InventoryPanel.Items.Single(i=>i.Id==id).Name;
    static int Stock(string id)=>C.CraftPanel.Materials.Single(m=>m.Id==id).Initial;
    public static async Task<string> Run()
    {
        Check(Application.isPlaying,"Play Mode required.");checks.Clear();shots.Clear();string failure=null;
        try
        {
            CampaignSaveStore.TestDirectory=TestDirectory;PartySelectionSession.Clear();
            SceneManager.LoadScene("PartySelection");await Task.Delay(600);
            var selection=Object.FindAnyObjectByType<PartySelectionController>();
            foreach(var card in selection.Cards.Where(x=>x.gameObject.activeInHierarchy&&x.Button.IsInteractable()).ToArray())
            {if(PartySelectionSession.Selected.Count>=2)break;await Tap(card.Button);}
            await Tap(selection.Continue);await Task.Delay(600);
            var home=Object.FindAnyObjectByType<HomeSelectionController>();await Tap(home.Cards[0].Button);await Tap(home.Continue);await Task.Delay(900);
            Check(C&&C.Campaign!=null,"Fresh campaign did not load.");
            Check(Ids.All(id=>Stock(id)==0),"New goods were silently granted at the beginning.");
            Check(C.Campaign.Party.Count()==2,"Opening party changed.");
            C.Introduction.Restore(10);C.Opening.State.Enabled=false;
            VerifyActualDrops();
            await FieldAcquisition();
            await InventoryAndRecipes();
        }
        catch(Exception e){failure=e.ToString();}
        Directory.CreateDirectory(Output);string report=Path.GetFullPath(Output+"everyday-goods-flow.json");
        File.WriteAllText(report,Newtonsoft.Json.JsonConvert.SerializeObject(new{result=failure==null?"PASS":"FAIL",checks,screenshots=shots,failure,note="Disposable campaign. Actual seeded mall search then UI transfer, character exchange and return; catalog preview stock/facilities seeded afterwards. No production save slots."},Newtonsoft.Json.Formatting.Indented));
        Check(failure==null,failure+"\n"+report);
        return "PASS: "+report+"\n"+string.Join("\n",checks);
    }
    static void VerifyActualDrops()
    {
        var loot=C.ArrivalPanel.Loot;var roll=typeof(ExpeditionLootPanel).GetMethod("Roll",F);var rng=UnityEngine.Random.state;
        try
        {
            var found=new HashSet<string>();UnityEngine.Random.InitState(98171);
            for(int site=0;site<loot.Sites.Length;site++)for(int sample=0;sample<200;sample++)
            {
                var s=new ExpeditionLootPanel.SearchState{Pace=1};roll.Invoke(loot,new object[]{site,s});
                foreach(var id in Ids.Where(id=>s.Loot.TryGetValue(id,out int count)&&count>0))found.Add(id);
            }
            Check(found.SetEquals(Ids),"Some new goods can never drop.");
            Check(!loot.Sites[0].Drops.Any(d=>Ids.Contains(d.Id)),"Opening crate contains extra goods.");
            Check(loot.Sites[0].Drops.Where(d=>d.Id=="wood"&&d.Chance==100).Sum(d=>d.Count)>=9&&loot.Sites[0].Drops.Where(d=>d.Id=="scrap"&&d.Chance==100).Sum(d=>d.Count)>=7,"Opening guaranteed materials reduced.");
        }
        finally{UnityEngine.Random.state=rng;}
        checks.Add("Eight new goods appear through the actual weighted roll; first crate keeps its guaranteed tutorial materials.");
    }
    static async Task FieldAcquisition()
    {
        var a=C.ArrivalPanel;var loot=a.Loot;var inv=C.InventoryPanel;var people=C.Campaign.Party.ToArray();
        Check(a.Begin(people,C.ExpeditionPanel.Destinations.Single(d=>d.Id=="mall")),"Cannot begin real expedition.");await Task.Delay(1100);if(a.Popup.activeSelf)a.ClosePopup();
        // Select a reproducible valid roll containing both trade goods and exactly three old kinds.
        var roll=typeof(ExpeditionLootPanel).GetMethod("Roll",F);int seed=-1;
        for(int i=0;i<1000;i++)
        {
            UnityEngine.Random.InitState(i);var probe=new ExpeditionLootPanel.SearchState{Pace=1,Bonus=loot.BonusFor(1,0)};roll.Invoke(loot,new object[]{1,probe});
            if(probe.Loot.ContainsKey("tobacco")&&probe.Loot.ContainsKey("coffee")&&probe.Loot.Count==5){seed=i;break;}
        }
        Check(seed>=0,"Could not find deterministic real five-kind loot fixture.");UnityEngine.Random.InitState(seed);
        Check(loot.Advance(1,1,people[0],0),"First real search turn refused.");for(int turn=1;turn<3&&!loot.State(1).Complete;turn++)Check(loot.Advance(1,1,people[0],0),"Next real search turn refused.");
        Check(loot.State(1).Complete&&loot.State(1).Loot.ContainsKey("tobacco")&&loot.State(1).Loot.ContainsKey("coffee"),"Actual roll differs from displayed source.");
        loot.Open(1);await Task.Delay(150);Check(loot.IsOpen,"Found items did not open.");
        await Sizes(async wh=>{FitsSlots(loot.FieldRows);await Shot("everyday-goods-loot-"+wh.x+"x"+wh.y);});
        await Tap(loot.TakeAll);Check(loot.State(1).Loot.Values.All(x=>x==0),"Take-all lost or left obtainable new goods.");await Tap(loot.Back);
        var source=people.Single(p=>inv.CountFor(p,"tobacco")>0);var target=people.Single(p=>p!=source);
        int total=people.Sum(p=>inv.CountFor(p,"tobacco")),minutes=C.Campaign.MinuteOfDay;
        Check(inv.ExchangeField(source,target,"tobacco",1),"New item could not be given to teammate.");
        Check(people.Sum(p=>inv.CountFor(p,"tobacco"))==total&&C.Campaign.MinuteOfDay==minutes,"Teammate transfer changed quantity/time.");
        await Tap(a.Return);await Tap(a.ReturnConfirm);await Task.Delay(300);await Tap(C.ReturnPanel.Back);
        foreach(var id in new[]{"tobacco","coffee"})foreach(var p in people)
        {int count=inv.CountFor(p,id);if(count>0)Check(inv.MoveFor(p,id,count,false),"Could not put recovered goods into warehouse: "+id);}
        Check(Stock("tobacco")==1&&Stock("coffee")==1,"Recovered goods did not survive return.");
        checks.Add("Real two-turn table search, discovered icons, take-all, character transfer, return and warehouse deposit preserve tobacco/coffee and time.");
    }
    static async Task InventoryAndRecipes()
    {
        var inv=C.InventoryPanel;
        foreach(var m in C.CraftPanel.Materials)m.Initial=Ids.Contains(m.Id)?2:0;
        C.Development.State.Warehouse=2;C.Development.State.Workbench=true;C.Development.State.Cooker=true;C.Development.State.Research=true;C.Development.State.Tools=true;
        inv.Open();await Task.Delay(120);Check(inv.IsOpen,"Inventory review failed to open.");
        await Sizes(async wh=>
        {
            foreach(var id in Ids)
            {
                var row=inv.StockRows.Single(s=>s.Label.text==Name(id));await Tap(row.Button);
                Check(inv.SelectedItemId==id&&inv.DetailIcon.sprite==row.Icon.sprite&&!inv.Use.interactable,"Wrong inventory item/icon/use: "+id);
                Fits(inv.DetailTitle);Fits(inv.Description);FitsSlots(inv.StockRows);
            }
            await Tap(inv.StockRows.Single(s=>s.Label.text==Name("fuel")).Button);await Shot("everyday-goods-inventory-"+wh.x+"x"+wh.y);
        });
        await Tap(inv.CloseButton);
        foreach(var m in C.CraftPanel.Materials)m.Initial=0;
        foreach(var id in new[]{"fuel","raw-water","cloth","alcohol","battery","electrical-parts","scrap","lubricant","food","water"})C.CraftPanel.Materials.Single(m=>m.Id==id).Initial=3;
        C.CraftPanel.Open();C.CraftPanel.FocusRecipe("flashlight-parts");C.CraftPanel.RecipeScroll.verticalNormalizedPosition=0;
        await Sizes(async wh=>{Fits(C.CraftPanel.DetailTitle);Fits(C.CraftPanel.DetailDescription);await Shot("everyday-goods-crafting-"+wh.x+"x"+wh.y);});await Tap(C.CraftPanel.CloseButton);
        C.CookingPanel.Open();
        var meal=C.CookingPanel.Meals.Single(m=>m.Id=="warm-fuel");
        typeof(SettlementCookingPanel).GetMethod("SelectCategory",F).Invoke(C.CookingPanel,new object[]{meal.Category});
        C.CookingPanel.RecipeScroll.verticalNormalizedPosition=0;await Task.Delay(100);
        var mealRow=C.CookingPanel.RecipeRows.Single(r=>r.Label.text==meal.Name);await Tap(mealRow.Button);
        await Sizes(async wh=>{Fits(C.CookingPanel.Title);Fits(C.CookingPanel.Description);await Shot("everyday-goods-cooking-"+wh.x+"x"+wh.y);});await Tap(C.CookingPanel.Back);
        checks.Add("Eight actual item details/icons and non-consumable actions fit inventory; new craft/cooking recipes shown at 1920x1080 and 1280x800.");
    }
    static void Fits(Text t){Canvas.ForceUpdateCanvases();if(!t.resizeTextForBestFit)Check(t.preferredHeight<=t.rectTransform.rect.height+2,"Text clips: "+t.name+" / "+t.text+" / "+t.preferredHeight+" > "+t.rectTransform.rect.height);}
    static void FitsSlots(IEnumerable<InventorySlot> slots){foreach(var s in slots){Check(s.Icon.sprite&&s.Icon.preserveAspect,"Missing or stretched item icon: "+s.Label.text);Fits(s.Label);}}
    static async Task Tap(Button b)
    {
        await Task.Delay(75);Canvas.ForceUpdateCanvases();Check(b&&b.IsActive()&&b.IsInteractable(),"Unavailable "+b?.name);
        var canvas=b.GetComponentInParent<Canvas>();var rect=(RectTransform)b.transform;
        var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Hit blocked: "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));
        ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(100);
    }
    static async Task Sizes(Func<Vector2Int,Task> action)
    {
        var asm=typeof(Editor).Assembly;var type=asm.GetType("UnityEditor.GameViewSizes");
        var sizes=typeof(ScriptableSingleton<>).MakeGenericType(type).GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
        var group=type.GetMethod("GetGroup",F).Invoke(sizes,new[]{type.GetProperty("currentGroupType",F).GetValue(sizes)});
        var view=EditorWindow.GetWindow(asm.GetType("UnityEditor.GameView"));var selected=view.GetType().GetProperty("selectedSizeIndex",F);int original=(int)selected.GetValue(view);
        var sizeType=asm.GetType("UnityEditor.GameViewSize");var added=new List<int>();
        try
        {
            foreach(var wh in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,800)})
            {
                int count=(int)group.GetType().GetMethod("GetTotalCount",F).Invoke(group,null),index=-1;
                for(int i=0;i<count;i++)
                {var size=group.GetType().GetMethod("GetGameViewSize",F).Invoke(group,new object[]{i});if((int)sizeType.GetProperty("width",F).GetValue(size)==wh.x&&(int)sizeType.GetProperty("height",F).GetValue(size)==wh.y){index=i;break;}}
                if(index<0){var enumType=asm.GetType("UnityEditor.GameViewSizeType");var constructor=sizeType.GetConstructor(F,null,new[]{enumType,typeof(int),typeof(int),typeof(string)},null);var size=constructor.Invoke(new[]{Enum.Parse(enumType,"FixedResolution"),(object)wh.x,wh.y,"Everyday goods review"});group.GetType().GetMethod("AddCustomSize",F).Invoke(group,new[]{size});index=count;added.Add(index-(int)group.GetType().GetMethod("GetBuiltinCount",F).Invoke(group,null));}
                selected.SetValue(view,index);view.Repaint();await Task.Delay(650);Check(Screen.width==wh.x&&Screen.height==wh.y,"Wrong capture resolution.");await action(wh);
            }
        }
        finally{selected.SetValue(view,original);foreach(var index in added.OrderByDescending(i=>i))group.GetType().GetMethod("RemoveCustomSize",F).Invoke(group,new object[]{index});view.Repaint();await Task.Delay(150);}
    }
    static async Task Shot(string name)
    {
        Directory.CreateDirectory(Output);string temp=Path.GetFullPath("Temp/"+name+"-native.png");if(File.Exists(temp))File.Delete(temp);
        ScreenCapture.CaptureScreenshot(temp);for(int i=0;i<40&&!File.Exists(temp);i++)await Task.Delay(100);Check(File.Exists(temp),"Missing screenshot.");await Task.Delay(150);
        string destination=Path.GetFullPath(Output+name+".png");File.Copy(temp,destination,true);shots.Add(destination);
    }
    public static string Finish(){Check(!Application.isPlaying,"Stop Play first.");if(CampaignSaveStore.TestDirectory==TestDirectory)CampaignSaveStore.TestDirectory=null;PartySelectionSession.Clear();return "Everyday goods fixture cleared.";}
}
