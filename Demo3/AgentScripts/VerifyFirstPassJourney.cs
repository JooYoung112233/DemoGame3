using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Live49.Core;
using Live49.Chapter00;
using Live49.Title;
using Live49.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
public static class VerifyFirstPassJourney
{
    static string Folder=>Path.GetFullPath(Application.dataPath+"/../Screenshots/FirstPassJourney");
    static GameHud Hud=>GameHud.Instance;
    static JourneyState S=>SaveSystem.Current;
    static Button B(string n)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==n);
    static bool Has(string n)=>UnityEngine.Object.FindObjectsByType<Button>().Any(b=>b.name==n&&b.interactable);
    static void Check(bool ok,string m){if(!ok)throw new Exception(m);}
    static void Log(string m){File.AppendAllText(Folder+"/progress.txt",DateTime.UtcNow.ToString("s")+" "+m+"\n");}
    static async Task Wait(Func<bool> p,string why="transition",int seconds=25){var end=DateTime.UtcNow.AddSeconds(seconds);while(!p()){if(DateTime.UtcNow>end)throw new Exception("Timeout "+why+" "+Status());await Task.Delay(60);}}
    public static string Status()=>JsonUtility.ToJson(S)+"\nButtons: "+string.Join(",",UnityEngine.Object.FindObjectsByType<Button>().Where(b=>b.interactable).Select(b=>b.name));
    static async Task Click(string n){Check(B(n).interactable,"Disabled "+n);B(n).onClick.Invoke();await Task.Delay(350);}
    static void Advance(){foreach(var t in UnityEngine.Object.FindObjectsByType<Typewriter>())if(t.IsTyping)t.Complete();if(UnityEngine.Object.FindObjectsByType<DialogueAdvanceCue>().Any(c=>c.GetComponent<CanvasGroup>().alpha>.98f))FreshInput.SimulateAdvance();}
    static async Task Pump(Func<bool> end,string why,int seconds=100){var until=DateTime.UtcNow.AddSeconds(seconds);while(!end()){if(DateTime.UtcNow>until)throw new Exception("Pump timeout "+why+" "+Status());Advance();await Task.Delay(85);}await Task.Delay(400);Log("played "+why);}
    static async Task Shot(string n){await Task.Delay(350);Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Folder+"/"+n+".png");await Task.Delay(300);}
    static string Hash(string path){if(!File.Exists(path))return "absent";using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path)));}
    public static async Task<string> Begin()
    {
        Check(SceneManager.GetActiveScene().name==SceneNames.Title,"Begin from Title");Check(string.IsNullOrEmpty(SaveSystem.TestSlotPath),"Existing slot override");
        Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/progress.txt","");File.WriteAllText(Folder+"/real-path.txt",SaveSystem.SlotPath);File.WriteAllText(Folder+"/real-hash.txt",Hash(SaveSystem.SlotPath));
        SaveSystem.TestSlotPath=Folder+"/isolated.json";
        var start=UnityEngine.Object.FindObjectsByType<TitleMenuItem>().Single(i=>i.Id=="start");start.Owner.Confirm(start);
        await Pump(()=>Hud!=null&&Hud.IsExploring,"opening");await Shot("01-first-camper");
        await Click("Target_soi");await Click("InteractionChoice_0");await Pump(()=>Hud.IsExploring,"answer Soi");
        await Click("Target_book");await Pump(()=>Has("StoryChoice_0"),"book and departure");
        return "PASS opening; night choices ready";
    }
    public static async Task<string> Night()
    {
        var until=DateTime.UtcNow.AddSeconds(170);string last="";
        while(S.day==0)
        {
            if(DateTime.UtcNow>until)throw new Exception("Night timeout "+Status());
            if(last!=S.node){last=S.node;Log("node "+last);}
            if(Hud.IsExploring&&!Hud.IsPaused&&Has("StoryChoice_0"))await Click("StoryChoice_0");
            else if(Hud.IsPaused&&Has("Button_하루 마치기"))await Click("Button_하루 마치기");
            else Advance();
            await Task.Delay(90);
        }
        await Wait(()=>Hud.IsExploring);Check(S.day==1&&S.Valid(),"Day 1 invalid");await Shot("02-first-morning");Log("PASS continuous first night -> day 1");return "PASS first night";
    }
    static async Task Go(string id){Hud.OpenMap();Hud.Map.Select(id);await Task.Delay(300);await Click("MapTravel");await Click("MapTravel");await Wait(()=>Hud.IsExploring&&RegionExploration.PlayerPlace(S)==id&&!Hud.IsPaused,"travel "+id);await Task.Delay(500);Log("arrived "+id);}
    static async Task Leave(){await Click("LeaveCamper");await Wait(()=>Hud.IsExploring&&RegionExploration.Outside(S));}
    static async Task Return(){await Click(Has("ReturnCamper")?"ReturnCamper":"ReturnFromRegion");await Wait(()=>Hud.IsExploring&&!RegionExploration.Outside(S));}
    static async Task Search(string button){await Click(button);await Click("QuickSearch");await Wait(()=>S.search?.phase=="result");await Click("AcknowledgeSearch");if(Has("StayAfterSearch"))await Click("StayAfterSearch");await Wait(()=>!SearchSession.Active(S));Check(S.Valid(),"Invalid search result");}
    static async Task Inspect(){await Search("InspectRegion_0");await Search("InspectRegion_1");}
    static async Task Week(string id){Check(FirstWeekStory.Action(S)==id,"Expected "+id+" found "+FirstWeekStory.Action(S));await Click("WeekAction");await Click("InteractionChoice_0");await Pump(()=>Hud.IsExploring&&string.IsNullOrEmpty(S.weekEvent),id);}
    static async Task Life(string tab,string choice){Hud.OpenActivity(tab=="supplies"?ActivityPanel.Kind.Supplies:ActivityPanel.Kind.Life);await Task.Delay(300);await Click("LifeTab_"+tab);await Click("LifeChoice_"+choice);await Click("LifeConfirm");Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);}
    public static async Task<string> Supplies()
    {
        await Go("L1");await Leave();await Search("StoreWater");await Search("StoreFood");await Life("supplies","supply.L1");await Return();await Life("meal","eat.packaged_food");
        Check(S.Has("first_meal_shared"),"Meal not shared");await Week("request");await Leave();await Go("L3");await Week("pencils");await Inspect();await Life("supplies","supply.L3");await Return();await Week("deliver");await Week("draw");Check(S.Has(RegionTravel.Drawing),"Drawing missing");await Shot("03-drawing-complete");Log("PASS natural supplies/meal/pencils/drawing");return "PASS supplies and drawing";
    }
    public static async Task<string> Sejin()
    {
        await Leave();await Go("L2");await Week("meet");await Week("accept");await Inspect();await Go("L4");await Week("bag");await Inspect();await Life("supplies","supply.L4");await Go("L2");await Week("handover");Check(S.Count("camera")==1&&S.Has("name.sejin"),"Camera/name missing");await Shot("04-camera-received");Log("PASS Sejin quest without fixture flags/items");return "PASS camera quest";
    }
    public static async Task<string> Bandit()
    {
        await Go("L8");await Wait(()=>Has("Bandit_detour"));await Task.Delay(650);await Shot("05-bandit");await Click("Bandit_detour");await Task.Delay(650);await Click("Bandit_Continue");await Task.Delay(1800);await Shot("06-L8-parking");await Inspect();await Return();await Wait(()=>Has("Bandit_Continue"));await Task.Delay(650);await Click("Bandit_Continue");await Task.Delay(600);Log("PASS bandit encounter and camper return");return "PASS bandit";
    }
    public static async Task<string> Reload()
    {
        Check(SaveSystem.AutoSave(S,out var error),error);Check(SaveSystem.TryRead(SaveSystem.SlotPath,out var before,out error)&&SaveSystem.QueueLoad(out error),error);
        var serialized=JsonUtility.ToJson(before);var op=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>op.isDone&&Hud!=null&&Hud.IsExploring);await Task.Delay(650);Check(JsonUtility.ToJson(S)==serialized,"Reload changed completed state");Log("PASS natural checkpoint reload");return "PASS reload";
    }
    static void Dial(int level)
    {
        var dial=Hud.Activity.Dial;var rt=dial.rectTransform;float a=(level-2)*60*Mathf.Deg2Rad;
        var pos=RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(rt.rect.center+new Vector2(Mathf.Sin(a),Mathf.Cos(a))*70));
        ExecuteEvents.Execute(dial.gameObject,new PointerEventData(EventSystem.current){position=pos,button=PointerEventData.InputButton.Left},ExecuteEvents.dragHandler);
    }
    public static async Task<string> KitchenAndDay()
    {
        await Life("equipment","stove.inspect");await Life("equipment","stove.repair");await Life("equipment","manual.repair");await Life("energy","manual");
        int gas=S.Count("gas"),food=S.Count("ingredients"),meal=S.Count("meal"),fuel=S.fuel,day=S.day;
        Hud.OpenActivity(ActivityPanel.Kind.Kitchen);await Task.Delay(350);await Click("StartCooking");Dial(3);await Wait(()=>Hud.Activity.Cooking.Heat>=85,"boil");Dial(1);await Shot("07-cooking-natural-supplies");await Wait(()=>Hud.Activity.Cooking.Stage==CookingSession.Phase.Done,"cooked",30);
        Check(S.Count("gas")==gas-1&&S.Count("ingredients")==food-1&&S.Count("meal")==meal+1&&S.fuel==fuel,"Natural cooking cost/reward");Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);
        Hud.OpenActivity(ActivityPanel.Kind.Evening);await Task.Delay(350);await Click("LifeConfirm");Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);Check(S.Has("memory."+day),"Evening memory missing");
        Hud.RequestDayEnd();await Task.Delay(350);Hud.ConfirmDayEnd();await Wait(()=>Hud.IsExploring&&S.day==day+1);Check(S.Has("memory."+day)&&S.minutes==0,"Day rollover lost memory/time");await Shot("08-next-morning");Log("PASS collected supplies -> repairs/manual charge/cook/evening/day rollover");return "PASS kitchen and day";
    }
    public static async Task<string> SideSites()
    {
        await Leave();await Go("L9");await Shot("09-L9-laundry");await Inspect();Check(SaveSlots.Art(S)=="region-L9","Laundry save art");await Go("L5");await Inspect();await Shot("10-house");Log("PASS laundry/house inspection");return "PASS side sites";
    }
    public static async Task<string> PhotoAndGate()
    {
        await Go("L6");await Week("shoot");await Week("album");await Inspect();Check(S.Has(RegionTravel.Photo)&&S.Count("first_travel_photo")==1,"Photo missing");await Go("L10");await Shot("11-L10-pharmacy");await Inspect();Check(SaveSlots.Art(S)=="region-L10","Pharmacy save art");await Go("L7");await Inspect();Check(RegionExploration.CompletedCount(S)==10,"Not all ten places completed");await Return();await Go("L7");Check(S.location=="L7"&&!RegionExploration.Outside(S),"Camper not driven to exit");
        Check(RegionTravel.Block(S,"region02")==null,"Natural journey cannot leave region");await Shot("12-region-ready");Log("PASS all 10 places, drawing/route/photo gates; camper moved from L1 to L7");return "PASS photo and gate";
    }
    public static async Task<string> Depart()
    {
        Hud.OpenMap();await Click("MapRegions");await Click("InteractionChoice_1");await Click("InteractionChoice_0");await Wait(()=>Hud.IsExploring&&S.regionId=="region02");await Reload();Check(S.regionId=="region02"&&S.Count("camera")==1&&S.Count("first_travel_photo")==1,"Region save lost quest");await Shot("13-next-region-restored");Log("PASS continuous Title -> region 02; no injected flags/resources; reload preserved milestones");return "PASS entire first-pass journey";
    }
    public static async Task<string> Remaining()
    {
        try
        {
            await Wait(()=>File.ReadAllText(Folder+"/progress.txt").Contains("PASS continuous first night"),"first night",160);
            await Supplies();await Sejin();await Bandit();await Reload();await KitchenAndDay();await Reload();await SideSites();await PhotoAndGate();await Depart();
            File.WriteAllText(Folder+"/completed-state.json",JsonUtility.ToJson(S,true));return "PASS complete first-pass route";
        }
        catch(Exception e){Log("FAIL "+e);File.WriteAllText(Folder+"/failure-state.json",JsonUtility.ToJson(S,true));throw;}
    }
    public static async Task<string> SaveUi()
    {
        Check(S.regionId=="region02","Run after completed journey");
        Hud.HandleEscape();await Task.Delay(350);await Click("Button_설정");
        UnityEngine.Object.FindAnyObjectByType<SettingsPanel>().RequestSaveLoad();await Task.Delay(350);await Click("SaveMode");await Click("SaveSlot_1");await Click("SlotPrimaryAction");if(Has("ConfirmSlotAction"))await Click("ConfirmSlotAction");
        var saved=SaveSlots.Read(1);Check(saved.Readable&&saved.State.Count("camera")==1&&saved.State.regionId=="region02","Settings manual save failed");await Shot("14-settings-save");
        await Click("LoadMode");await Click("SaveSlot_1");await Click("SlotPrimaryAction");await Click("ConfirmSlotAction");await Wait(()=>Hud!=null&&Hud.IsExploring&&!Hud.IsPaused);await Task.Delay(1000);Check(S.regionId=="region02"&&S.Count("first_travel_photo")==1&&S.bandit.stage==4,"Manual UI load lost progress");
        Check(JourneyTutorial.Completed(S).All(x=>x),"Tutorial not complete after reload");Log("PASS ESC -> settings -> manual slot save/load; six tutorial milestones retained");return "PASS settings save/load";
    }
    public static async Task<string> VisualRegression()
    {
        Check(string.IsNullOrEmpty(SaveSystem.TestSlotPath),"Existing slot override");
        SaveSystem.TestSlotPath=Folder+"/visual-isolated.json";
        try
        {
            foreach(string id in new[]{"L8","L9","L10"})
            {
                var s=JsonUtility.FromJson<JourneyState>(File.ReadAllText(Folder+"/completed-state.json"));s.regionId="region01";s.location="L1";s.exploringPlace=id;s.inStore=false;
                Check(SaveSystem.Write(SaveSystem.SlotPath,s,out var error)&&SaveSystem.QueueLoad(out error),error);var op=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>op.isDone&&Hud!=null&&Hud.IsExploring);await Task.Delay(500);
                var stage=GameObject.Find("JourneyStage").GetComponent<Image>();Check(stage.sprite.name=="region-"+id,"Wrong stage art "+id);
                Check(Hud.InteractionRoot.Find("Location").GetComponentInChildren<HudIcon>().Symbol==HudIcon.Kind.Map,"Outside icon");await Shot("final-"+id);
                Hud.OpenSaveLoad(false);await Task.Delay(400);await Click("SaveSlot_0");var art=GameObject.Find("SaveIllustration").GetComponent<Image>();Check(art.sprite==stage.sprite,"Save preview mismatch "+id);Hud.HandleEscape();await Task.Delay(300);Hud.HandleEscape();await Task.Delay(300);Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);
            }
            await Return();Check(Hud.InteractionRoot.Find("Location").GetComponentInChildren<HudIcon>().Symbol==HudIcon.Kind.Camper,"Camper icon");
            var previous=S;SaveSystem.Current=new JourneyState();Hud.SetExplorationContext("캠핑카","밤","");Check(Hud.InteractionRoot.Find("Location").GetComponentInChildren<HudIcon>().Symbol==HudIcon.Kind.Moon,"Opening icon");SaveSystem.Current=previous;
            Log("PASS visual regression: all three final stage sprites/save preview identity; outside map, camper and first-night moon icons");return "PASS final artwork and context icons";
        }
        finally{await Finish();}
    }
    public static async Task<string> Finish()
    {
        Check(Hash(File.ReadAllText(Folder+"/real-path.txt"))==File.ReadAllText(Folder+"/real-hash.txt"),"Real autosave changed");
        var op=SceneManager.LoadSceneAsync(SceneNames.Title);await Wait(()=>op.isDone);SaveSystem.Current=null;SaveSystem.TestSlotPath=null;Log("RESTORED Title; real player autosave hash unchanged");return "PASS restored Title and save slot";
    }
}
