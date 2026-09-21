using Live49.Chapter00;
using Live49.Core;
using Live49.Dialogue;
using Live49.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public static class VerifyFirstWeek
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/Revalidation-2026-09-13/FirstWeek"));
    static GameHud Hud=>GameHud.Instance;
    static JourneyState State=>SaveSystem.Current;
    static DialogueView View=>UnityEngine.Object.FindFirstObjectByType<DialogueView>();
    static Button B(string n)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==n);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static JourneyState Copy(JourneyState s)=>JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(s));
    static void Log(string s){Directory.CreateDirectory(Folder);File.AppendAllText(Path.Combine(Folder,"progress.txt"),s+"\n");}
    static async Task Wait(Func<bool> ready){var end=DateTime.UtcNow.AddSeconds(20);while(!ready()){if(DateTime.UtcNow>end)throw new Exception("Timeout: "+State?.weekEvent+"/"+State?.weekLine);await Task.Delay(35);}}
    static void Fit(Transform root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<TMP_Text>())Check(!t.isTextOverflowing,"Overflow: "+t.text);}
    static async Task Shot(string n){await Task.Delay(250);Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,n+".png"));await Task.Delay(300);}
    static JourneyState Fixture(){var s=new JourneyState{day=2,location="L1",bookSeen=true,answeredSoi=true,fuel=2};s.Set("water_checked");s.Set("food_checked");s.Set("first_meal_shared");s.Add("sketchbook","스케치북",1,2);return s;}
    static void Event(JourneyState s,string id)
    {
        Check(FirstWeekStory.Begin(s,id),"Cannot begin "+id);Check(!FirstWeekStory.Finish(s),"Premature finish "+id);
        while(s.weekLine<WeekScript.Find(id).lines.Length){Check(s.Valid(),"Invalid mid-event "+id+"/"+s.weekLine);FirstWeekStory.ConfirmLine(s);s=RoundtripInto(s);}
        Check(FirstWeekStory.Finish(s),"Cannot finish "+id);Check(!FirstWeekStory.Finish(s)&&!FirstWeekStory.Begin(s,id),"Duplicate event "+id);
    }
    static JourneyState RoundtripInto(JourneyState s){var loaded=Copy(s);Check(loaded.Valid(),"Saved checkpoint invalid "+s.weekEvent+"/"+s.weekLine);return s;}
    public static string Model()
    {
        var s=Fixture();s.exploringPlace="L3";Event(s,"pencils");Check(s.Count("colored_pencils")==1,"Pencils not guaranteed");s.exploringPlace="";Event(s,"request");Event(s,"deliver");Event(s,"draw");Check(s.Has(RegionTravel.Drawing)&&s.Count("sketchbook")==1,"Drawing/book mismatch");
        s.exploringPlace="L2";Check(FirstWeekStory.Begin(s,"meet"),"Meet blocked");FirstWeekStory.ConfirmLine(s);Check(!s.Has("name.sejin"),"Early name reveal");FirstWeekStory.ConfirmLine(s);Check(s.Has("name.sejin"),"Name reveal missing");while(s.weekLine<8)FirstWeekStory.ConfirmLine(s);FirstWeekStory.Finish(s);Event(s,"accept");Check(RegionExploration.Discovered(s,"L4"),"Quest did not reveal L4");
        s.exploringPlace="L4";Event(s,"bag");s.exploringPlace="L2";FirstWeekStory.Begin(s,"handover");FirstWeekStory.ConfirmLine(s);var invalid=Copy(s);invalid.items.RemoveAll(i=>i.id=="sejin_toolbag");Check(!invalid.Valid(),"Missing handover bag accepted");FirstWeekStory.ConfirmLine(s);s=Copy(s);Check(s.Valid()&&s.Count("camera")==1&&s.Count("sejin_toolbag")==0,"Atomic camera exchange invalid");while(s.weekLine<4)FirstWeekStory.ConfirmLine(s);FirstWeekStory.Finish(s);Check(s.Count("camera")==1&&RegionExploration.Discovered(s,"L6"),"Camera duplicate/L6 missing");
        s.exploringPlace="L6";s.Spend(("tutorial_film",1));FirstWeekStory.Begin(s,"shoot");FirstWeekStory.ConfirmLine(s);Check(!s.Has("first_photo_taken"),"Photo before shutter");FirstWeekStory.ConfirmLine(s);s=Copy(s);Check(s.Valid()&&s.Has("first_photo_taken")&&!s.Has(RegionTravel.Photo),"Photo incorrectly completes album");FirstWeekStory.ConfirmLine(s);FirstWeekStory.Finish(s);Event(s,"album");Check(s.Has(RegionTravel.Photo)&&s.Count("first_travel_photo")==1&&s.Count("tutorial_film")==0,"Photo/film repeat");
        var early=Fixture();early.Set("story.discovered.L4");early.exploringPlace="L4";Event(early,"noticebag");Check(early.Count("sejin_toolbag")==0,"Early bag stolen");early.exploringPlace="";early.Add("pencils","기존 색연필",1,2);Event(early,"request");Check(FirstWeekStory.Action(early)=="deliver","Existing pencils ignored");
        Check(Fixture().Valid(),"Legacy save invalid");var bad=Fixture();bad.weekEvent="draw";Check(!bad.Valid(),"Invalid pending event accepted");
        Log("PASS model: pre-owned/pre-collected pencils; no new book; each checkpoint validates; name reveal after introduction; optional quest; atomic camera exchange; corrupt bag rejected; guaranteed first film; shutter vs album completion; no duplicate rewards.");return "PASS first-week model";
    }
    static async Task Load(JourneyState s)
    {
        for(int i=0;Hud!=null&&Hud.IsPaused&&i<5;i++){Hud.HandleEscape();await Task.Delay(350);}
        Check(SaveSystem.Write(SaveSystem.SlotPath,s,out var error)&&SaveSystem.QueueLoad(out error),error);
        var op=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>op.isDone&&Hud!=null&&(string.IsNullOrEmpty(s.weekEvent)?Hud.IsExploring:State?.weekEvent==s.weekEvent));await Task.Delay(500);
    }
    static async Task Start(string id)
    {
        Check(FirstWeekStory.Action(State)==id,"Expected "+id+", found "+FirstWeekStory.Action(State));B("WeekAction").onClick.Invoke();await Wait(()=>UnityEngine.Object.FindObjectsByType<Button>().Any(b=>b.name=="InteractionChoice_0"));await Task.Delay(250);B("InteractionChoice_0").onClick.Invoke();await Wait(()=>State.weekEvent==id);
    }
    static async Task Pump(string id,int stopAt=-1,bool replay=false)
    {
        var end=DateTime.UtcNow.AddSeconds(90);
        while(!(Hud.IsExploring&&string.IsNullOrEmpty(State.weekEvent)))
        {
            if(DateTime.UtcNow>end)throw new Exception("Dialogue timeout "+id+"/"+State.weekLine);
            if(stopAt>=0&&State.weekLine==stopAt&&View!=null&&View.Cue.alpha>.98f){await Task.Delay(100);return;}
            if(View!=null){if(View.Body.IsTyping)View.Body.Complete();if(View.Cue.alpha>.98f)FreshInput.SimulateAdvance();}
            await Task.Delay(80);
        }
        Check(stopAt<0,"Missed requested line "+stopAt);Log("played "+id);
    }
    static async Task Play(string id){await Start(id);await Pump(id);}
    static async Task Go(string id)
    {
        Hud.OpenMap();Hud.Map.Select(id);await Task.Delay(300);Check(B("MapTravel").interactable,"Travel blocked "+id);B("MapTravel").onClick.Invoke();await Task.Delay(350);B("MapTravel").onClick.Invoke();await Wait(()=>Hud.IsExploring&&RegionExploration.PlayerPlace(State)==id&&!Hud.IsPaused);Log("arrived "+id);
    }
    static async Task Inspect()
    {
        for(int i=0;i<2;i++){B("InspectRegion_"+i).onClick.Invoke();await Task.Delay(300);B("InteractionChoice_0").onClick.Invoke();await Task.Delay(600);Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);}
    }
    static async Task Return(){B("ReturnFromRegion").onClick.Invoke();await Wait(()=>Hud.IsExploring&&!RegionExploration.Outside(State));}
    static async Task Leave(){B("LeaveCamper").onClick.Invoke();await Wait(()=>Hud.IsExploring&&RegionExploration.Outside(State));}
    public static async Task<string> Run()
    {
        Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"progress.txt"),"");Model();
        // Read the captured player journey; all validation writes go to an isolated slot.
        Check(SaveSystem.TryRead(Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/Revalidation-2026-09-13/CityMapReview/checkpoint.json")),out var original,out var error),error);
        var oldPath=SaveSystem.TestSlotPath;SaveSystem.TestSlotPath=Path.Combine(Folder,"isolated.json");
        try
        {
            await Load(Fixture());Fit(Hud.InteractionRoot);B("WeekAction").onClick.Invoke();await Task.Delay(300);Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);Check(State.weekEvent==""&&!State.Has("drawing_requested"),"Cancel starts story");
            await Play("request");await Leave();await Go("L3");await Shot("01-pencils-location");await Play("pencils");await Return();await Play("deliver");
            await Start("draw");await Pump("draw",3);Check(!State.Has(RegionTravel.Drawing),"Drawing flag before reaction");await Shot("02-first-drawing");await Pump("draw");
            await Leave();await Go("L2");await Start("meet");await Pump("meet",1);Check(View.SpeakerLabel=="?","NPC already named");await Shot("03-sejin-unknown");Hud.HandleEscape();await Task.Delay(300);int line=State.weekLine;FreshInput.SimulateAdvance();await Task.Delay(400);Check(State.weekLine==line,"Paused dialogue advances");Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);
            await Pump("meet",4);Check(View.SpeakerLabel=="세진"&&State.Has("name.sejin"),"Name not revealed");await Shot("04-sejin-introduced");await Pump("meet");await Play("accept");
            await Go("L4");await Play("bag");await Inspect();await Go("L2");await Start("handover");await Pump("handover",2);Check(SaveSystem.TryRead(SaveSystem.SlotPath,out var saved,out error),error);Check(saved.weekLine==2&&saved.Count("camera")==1&&saved.Count("sejin_toolbag")==0,"Exchange checkpoint invalid");await Load(saved);await Pump("handover");Check(State.Count("camera")==1,"Camera duplicated by load");
            await Go("L6");await Start("shoot");await Pump("shoot",2);Check(State.Has("first_photo_taken")&&!State.Has(RegionTravel.Photo),"Shutter gate wrong");await Shot("05-first-photograph");Check(SaveSystem.TryRead(SaveSystem.SlotPath,out saved,out error),error);await Load(saved);await Pump("shoot");Check(State.Count("first_travel_photo")==1,"Photo duplicated by load");
            await Start("album");await Pump("album",1);await Shot("06-first-album");await Pump("album");await Inspect();await Go("L10");await Inspect();await Go("L7");await Inspect();await Return();
            int photos=State.Count("first_travel_photo"),minutes=State.minutes;B("WeekMemories").onClick.Invoke();await Task.Delay(300);B("InteractionChoice_1").onClick.Invoke();await Wait(()=>!Hud.IsExploring);await Pump("album replay",-1,true);Check(State.minutes==minutes&&State.Count("first_travel_photo")==photos,"Replay grants reward/time");
            Hud.OpenActivity(ActivityPanel.Kind.Journal);await Task.Delay(400);Fit(Hud.Activity.transform);await Shot("07-recorded-milestones");Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);
            await Go("L7");Check(RegionTravel.Block(State,"region02")==null,"Natural story does not unlock departure");Hud.OpenMap();B("MapRegions").onClick.Invoke();await Task.Delay(400);B("InteractionChoice_1").onClick.Invoke();await Task.Delay(400);B("InteractionChoice_0").onClick.Invoke();await Wait(()=>Hud.IsExploring&&State.regionId=="region02");
            Check(SaveSystem.TryRead(SaveSystem.SlotPath,out saved,out error),error);await Load(saved);Check(State.regionId=="region02"&&State.Count("camera")==1&&State.Count("first_travel_photo")==1,"Departure save lost story");await Shot("08-next-region-after-story");Log("PASS live: cancelled action unchanged; UI-only acquisition and all story steps; pause; unknown/known name; camera/photo mid-event reload; album and memory replay; journal fit; ambient route checks; actual next-region drive and reload.");
        }
        catch(Exception e){Log("FAIL "+e);throw;}
        finally{try{await Load(original);await Shot("09-original-progress-restored");Log("RESTORED original journey; real slot untouched.");}finally{SaveSystem.TestSlotPath=oldPath;}}
        return "PASS first-week story and departure; original player progress restored.";
    }
    public static async Task<string> Edges()
    {
        Check(SaveSystem.TryRead(Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/Revalidation-2026-09-13/CityMapReview/checkpoint.json")),out var original,out var error),error);
        var oldPath=SaveSystem.TestSlotPath;SaveSystem.TestSlotPath=Path.Combine(Folder,"edge-isolated.json");
        try
        {
            var fixture=Fixture();fixture.Set("story.discovered.L8");await Load(fixture);await Play("request");
            foreach(var group in UnityEngine.Object.FindObjectsByType<CanvasGroup>().Where(g=>g.name.StartsWith("Portrait")))Check(group.alpha==0,"Portrait remains after event: "+group.name);
            await Leave();await Go("L3");Check(GameObject.Find("WeekPencils")!=null,"Pencils absent");await Go("L8");Check(GameObject.Find("WeekPencils")==null,"Pencils leaked into map overview");
            Log("PASS final transition regression: dialogue portraits hidden on return; uncollected pencil prop hidden when walking to an overview location.");
        }
        finally{try{await Load(original);Check(FirstWeekStory.Action(State)==null&&FirstWeekStory.Goal(State).Contains("식탁"),"Original journey not ready for first meal");await Shot("09-original-progress-restored");}finally{SaveSystem.TestSlotPath=oldPath;}}
        Check(string.IsNullOrEmpty(SaveSystem.TestSlotPath),"Save override remains");return "PASS final transitions; original journey restored, ready for book action.";
    }
}

public static class VerifyRegionTravel
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/Revalidation-2026-09-13/JourneyPolish"));
    static GameHud Hud=>GameHud.Instance;
    static JourneyState State=>SaveSystem.Current;
    static Button B(string n)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==n);
    static void Check(bool b,string why){if(!b)throw new Exception(why);}
    static async Task Wait(Func<bool> ready){var end=DateTime.UtcNow.AddSeconds(15);while(!ready()){if(DateTime.UtcNow>end)throw new Exception("Timeout");await Task.Delay(25);}}
    static async Task Shot(string n){await Task.Delay(300);Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,n));await Task.Delay(300);}
    static void Fits(Transform root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<TMP_Text>())Check(!t.isTextOverflowing,"Overflow: "+t.text);}
    static JourneyState Fixture()
    {
        var s=new JourneyState{day=3,location="L7",fuel=3};s.Set("water_checked");s.Set("food_checked");
        foreach(var site in RegionExploration.Sites.Where(p=>p.Id!="L1"))s.Set("region01.explored."+site.Id);
        return s;
    }
    public static string Model()
    {
        var s=Fixture();Check(s.Valid(),"Fixture invalid");Check(!RegionTravel.Travel(s,"region02"),"Tutorial gate bypassed");
        s.Set(RegionTravel.Drawing);s.Set(RegionTravel.Photo);Check(!RegionTravel.Travel(s,"region02"),"Route knowledge bypassed");s.Set(RegionTravel.Route);
        s.exploringPlace="L7";Check(!RegionTravel.Travel(s,"region02"),"Walking crosses region");s.exploringPlace="";
        Check(RegionTravel.Travel(s,"region02")&&s.location==RegionTravel.Entry&&s.Valid(),"Crossing invalid");
        Check(!RegionExploration.Travel(s,"L2"),"Local map teleports between regions");
        s.exploringPlace=RegionTravel.Entry;PlaceInspection.Inspect(s,RegionTravel.Entry,0,out _);PlaceInspection.Inspect(s,RegionTravel.Entry,1,out _);s.exploringPlace="";
        Check(RegionExploration.CompletedCount(s)==1,"Region progress not scoped");Check(RegionTravel.Travel(s,"region01")&&RegionExploration.CompletedCount(s)==10&&s.Valid(),"Return loses first region");
        s.regionId="region02";Check(!s.Valid(),"Mismatched parking region accepted");
        return "PASS model: drawing/photo/route prerequisite; no walking cross; no local teleport; next entry; region-scoped completion; return/save validation.";
    }
    static async Task Load(JourneyState s)
    {
        for(int i=0;Hud!=null&&Hud.IsPaused&&i<5;i++){Hud.HandleEscape();await Task.Delay(350);}
        Check(SaveSystem.Write(SaveSystem.SlotPath,s,out var e)&&SaveSystem.QueueLoad(out e),e);
        var op=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>op.isDone&&Hud!=null&&Hud.IsExploring);await Task.Delay(300);
    }
    static async Task ChooseRegion(int index)
    {
        Hud.OpenMap();B("MapRegions").onClick.Invoke();await Wait(()=>UnityEngine.Object.FindObjectsByType<Button>().Any(b=>b.name=="InteractionChoice_1"));
        B("InteractionChoice_"+index).onClick.Invoke();await Task.Delay(400);
    }
    public static async Task<string> Run()
    {
        Directory.CreateDirectory(Folder);File.AppendAllText(Path.Combine(Folder,"progress.txt"),Model()+"\n");
        var original=JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(State));var path=SaveSystem.TestSlotPath;SaveSystem.TestSlotPath=Path.Combine(Folder,"region-isolated.json");
        try
        {
            var s=Fixture();await Load(s);await ChooseRegion(1);Fits(Hud.transform.Find("HUDViewport/PauseOverlay"));await Shot("08-narrative-departure-gate.png");Check(State.regionId=="region01","UI bypasses story gate");
            s.Set(RegionTravel.Drawing);s.Set(RegionTravel.Photo);s.Set(RegionTravel.Route);await Load(s);
            await ChooseRegion(1);B("InteractionChoice_0").onClick.Invoke();await Wait(()=>Hud.IsExploring&&State.regionId=="region02");
            Check(State.location==RegionTravel.Entry,"Camper did not cross");Hud.OpenMap();await Task.Delay(1400);Fits(Hud.Map.transform);await Shot("09-next-region-entry.png");Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);
            B("LeaveCamper").onClick.Invoke();await Wait(()=>Hud.IsExploring&&RegionExploration.Outside(State));
            for(int i=0;i<2;i++){B("InspectRegion_"+i).onClick.Invoke();await Task.Delay(250);B("InteractionChoice_0").onClick.Invoke();await Task.Delay(450);Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);}
            Check(RegionExploration.Completed(State,RegionTravel.Entry),"Next-region inspection failed");B("ReturnFromRegion").onClick.Invoke();await Wait(()=>Hud.IsExploring&&!RegionExploration.Outside(State));
            Check(SaveSystem.TryRead(SaveSystem.SlotPath,out var saved,out _),"Next-region save missing");await Load(saved);Check(State.regionId=="region02"&&RegionExploration.CompletedCount(State)==1,"Next-region reload failed");
            await ChooseRegion(0);B("InteractionChoice_0").onClick.Invoke();await Wait(()=>Hud.IsExploring&&State.regionId=="region01");Check(State.location=="L7"&&RegionExploration.CompletedCount(State)==10&&RegionExploration.Completed(State,RegionTravel.Entry),"Round trip loses progress");
            foreach(var site in RegionExploration.Sites){JourneyDayLog.Visit(State,site.Id);JourneyDayLog.Found(State,new[]{site.Id});}State.Add("water","생수",2);State.Add("packaged_food","포장 식량",2);State.Add("meal","따뜻한 식사",1);JourneyDayLog.Fuel(State,2);
            Hud.RequestDayEnd();await Task.Delay(300);Fits(Hud.transform.Find("HUDViewport/PauseOverlay"));await Shot("10-full-day-summary.png");Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);
            Hud.OpenActivity(ActivityPanel.Kind.Journal);await Task.Delay(300);Fits(Hud.Activity.transform);await Shot("11-daily-journal.png");
            File.AppendAllText(Path.Combine(Folder,"progress.txt"),"PASS live: narrative gate; vehicle crosses region; next-entry exploration/save/reload; return preserves both regions; maximum day summary and journal fit.\n");
        }
        finally{try{await Load(original);Hud.OpenMap();await Task.Delay(1600);await Shot("12-restored-current-map.png");}finally{SaveSystem.TestSlotPath=path;}}
        return "PASS region framework; original progress restored. Story milestones were fixture-only.";
    }
}

public static class VerifyCampLife
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/Revalidation-2026-09-13/CampLife"));
    static GameHud Hud=>GameHud.Instance;
    static JourneyState State=>SaveSystem.Current;
    static Button B(string id)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==id);
    static void Check(bool b,string message){if(!b)throw new Exception(message);}
    static JourneyState Copy(JourneyState s)=>JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(s));
    static void Log(string text){Directory.CreateDirectory(Folder);File.AppendAllText(Path.Combine(Folder,"progress.txt"),text+"\n");}
    static void Act(JourneyState s,string section,string id){Check(CampLife.Apply(s,section,id,out var e),id+": "+e);Check(Copy(s).Valid(),"Invalid after "+id);}
    static JourneyState Fixture()
    {var s=new JourneyState{day=2,location="L1",fuel=2,bookSeen=true,answeredSoi=true};s.Set("water_checked");s.Set("food_checked");s.Add("water","생수",2);s.Add("packaged_food","포장 식량",2);return s;}
    public static string Model()
    {
        var s=Fixture();Check(CampLife.Memories(s).All(x=>!x.Key.Contains("photo")),"Unearned photo memory");Check(FirstWeekStory.Action(s)==null,"First meal bypassed");Act(s,"meal","eat.packaged_food");Check(FirstWeekStory.Action(s)=="request","Meal did not unlock book");Check(!CampLife.Apply(s,"meal","eat.packaged_food",out _)&&s.Count("water")==1,"Repeated meal consumed twice");
        s.inStore=true;Act(s,"supplies","supply.L1");Check(!CampLife.Apply(s,"supplies","supply.L1",out _),"Repeated cache");s.inStore=false;s.exploringPlace="L3";Act(s,"supplies","supply.L3");s.Set("story.discovered.L4");s.exploringPlace="L4";Act(s,"supplies","supply.L4");s.exploringPlace="";
        Check(!CampLife.Apply(s,"equipment","stove.repair",out _),"Repair without inspection");Act(s,"equipment","stove.inspect");Act(s,"equipment","stove.repair");Act(s,"equipment","manual.repair");
        var cook=new CookingSession(s);int minutes=s.minutes;Check(cook.Start()&&!cook.Start(),"Duplicate cooking start");for(int i=0;i<25;i++)cook.Tick(.1f);s=Copy(s);cook=new CookingSession(s);Check(cook.Paused&&s.Count("gas")==2,"Cooking reload not paused/cost twice");cook.Paused=false;for(int i=0;i<500;i++)cook.Tick(.1f);Check(s.Count("meal")==1&&s.minutes==minutes+25,"Cooking lost/duplicated result");
        Act(s,"meal","microwave");s=Copy(s);for(int i=0;i<100;i++)CampLife.TickHeating(s,.1f);Check(s.Count("meal")==2&&s.Count("ready_meal")==1&&s.life.battery==20,"Microwave cost/reload mismatch");
        s.fuel=1;Act(s,"energy","generate");Check(s.fuel==0&&s.life.battery==60,"Generator not shared fuel");Act(s,"energy","manual");Check(s.fuel==0&&s.life.battery==70,"Manual consumes fuel");s.exploringPlace="L2";Act(s,"supplies","emergency.fuel");Check(s.fuel==2,"Emergency route absent");
        s.Set("story.discovered.L5");s.exploringPlace="L5";Act(s,"supplies","dog.look");Act(s,"supplies","dog.offer.water");s=Copy(s);Check(s.Count("water")==0&&!CampLife.Apply(s,"supplies","dog.offer.water",out _),"Dog charges again after return");Check(!CampLife.Apply(s,"supplies","dog.join",out _),"Dog skips waiting");Act(s,"supplies","dog.wait");Act(s,"supplies","dog.join");s.exploringPlace="";Act(s,"dog","dog.bed");
        int cloth=s.Count("cloth");var memory=CampLife.Memories(s).First();Act(s,"evening","memory."+memory.Key);Check(s.Count("cloth")==cloth&&!CampLife.Apply(s,"evening","memory."+memory.Key,out _),"Memory consumes/repeats");s.day++;Check(!CampLife.Memories(s).Any(),"Yesterday leaks into memories");Check(s.Has("memory.2")&&s.Has("dog_joined")&&s.Valid(),"Daily transition loses state");
        s.life.battery=-1;Check(!s.Valid(),"Negative battery accepted");s.life=null;s.cooking=null;Check(s.Valid(),"Legacy fields fail");
        Log("PASS model: meal-to-book gate; one-time supplies; inspected repair; cooking and microwave reload; no duplicate resource/result; shared fuel and manual alternative; zero-fuel recovery; staged dog care/adoption; daily earned memories; old saves and invalid battery.");return "PASS camp life model";
    }
    static async Task Wait(Func<bool> ready,int seconds=25){var end=DateTime.UtcNow.AddSeconds(seconds);while(!ready()){if(DateTime.UtcNow>end)throw new Exception("Timeout");await Task.Delay(35);}}
    static async Task Shot(string name){await Task.Delay(250);Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));await Task.Delay(300);}
    static void Fits(Transform root){Canvas.ForceUpdateCanvases();foreach(var text in root.GetComponentsInChildren<TMP_Text>())Check(!text.isTextOverflowing,"Overflow: "+text.name+": "+text.text);}
    static async Task Load(JourneyState s)
    {
        for(int i=0;Hud!=null&&Hud.IsPaused&&i<5;i++){Hud.HandleEscape();await Task.Delay(350);}
        Check(SaveSystem.Write(SaveSystem.SlotPath,s,out var error)&&SaveSystem.QueueLoad(out error),error);
        var op=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>op.isDone&&Hud!=null&&Hud.IsExploring);await Task.Delay(350);
    }
    static async Task Close(){Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);await Task.Delay(100);}
    static async Task Open(ActivityPanel.Kind kind){Hud.OpenActivity(kind);await Wait(()=>Hud.Activity!=null&&Hud.Activity.IsOpen);await Task.Delay(300);}
    static async Task Tab(string id){B("LifeTab_"+id).onClick.Invoke();await Task.Delay(100);}
    static async Task Select(string id){B("LifeChoice_"+id).onClick.Invoke();await Task.Delay(100);}
    static async Task Choose(string id)
    {await Select(id);Check(B("LifeConfirm").interactable,"Action blocked "+id);B("LifeConfirm").onClick.Invoke();await Task.Delay(150);Fits(Hud.Activity.transform);Log("UI "+id);}
    static async Task Go(string id)
    {Hud.OpenMap();Hud.Map.Select(id);await Task.Delay(250);Check(B("MapTravel").interactable,"Travel blocked "+id);B("MapTravel").onClick.Invoke();await Task.Delay(350);B("MapTravel").onClick.Invoke();await Wait(()=>Hud.IsExploring&&!Hud.IsPaused&&RegionExploration.PlayerPlace(State)==id);}
    static async Task Inspect()
    {for(int i=0;i<2;i++){B("InspectRegion_"+i).onClick.Invoke();await Task.Delay(250);B("InteractionChoice_0").onClick.Invoke();await Task.Delay(550);await Close();}}
    static async Task Return(){B("ReturnFromRegion").onClick.Invoke();await Wait(()=>Hud.IsExploring&&!RegionExploration.Outside(State));}
    static async Task Leave(){B("LeaveCamper").onClick.Invoke();await Wait(()=>Hud.IsExploring&&RegionExploration.Outside(State));}
    public static async Task<string> Run()
    {
        Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"progress.txt"),"");Model();
        Check(SaveSystem.TryRead(Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/Revalidation-2026-09-13/CityMapReview/checkpoint.json")),out var original,out var error),error);
        var old=SaveSystem.TestSlotPath;SaveSystem.TestSlotPath=Path.Combine(Folder,"isolated.json");
        try
        {
            await Load(Copy(original));await Open(ActivityPanel.Kind.Life);await Select("eat.packaged_food");await Close();Check(!State.Has("first_meal_shared")&&State.Count("water")==2,"Selection alone spends");
            await Open(ActivityPanel.Kind.Life);await Choose("eat.packaged_food");Check(State.Count("water")==1&&FirstWeekStory.Action(State)=="request","Meal gate/cost incorrect");await Shot("01-shared-meal");await Tab("equipment");await Choose("stove.inspect");await Select("stove.repair");Check(!B("LifeConfirm").interactable,"Missing parts not blocked");await Close();
            await Leave();await Open(ActivityPanel.Kind.Supplies);await Choose("supply.L1");await Close();await Go("L3");await Open(ActivityPanel.Kind.Supplies);await Choose("supply.L3");await Close();await Inspect();await Go("L2");await Inspect();await Go("L4");await Open(ActivityPanel.Kind.Supplies);await Choose("supply.L4");await Shot("02-supplies");await Close();await Return();
            await Open(ActivityPanel.Kind.Life);await Tab("equipment");await Choose("stove.repair");await Choose("manual.repair");await Choose("cabinet");await Shot("03-equipment");await Tab("energy");B("EnergyDestination").onClick.Invoke();await Task.Delay(150);await Choose("generate");Check(State.fuel==3&&State.life.battery==70,"Shared fuel mismatch");await Shot("04-energy");await Close();
            await Open(ActivityPanel.Kind.Kitchen);B("StartCooking").onClick.Invoke();await Task.Delay(1700);await Close();Check(SaveSystem.TryRead(SaveSystem.SlotPath,out var saved,out error),error);await Load(saved);Check(CampLife.Cooking(State),"Cooking checkpoint lost");Hud.RequestDayEnd();await Task.Delay(300);Check(!B("Button_하루 마치기").interactable,"Day ends during unfinished cooking");await Close();await Open(ActivityPanel.Kind.Kitchen);Check(Hud.Activity.Cooking.Paused,"Loaded cooking not paused");B("PauseCooking").onClick.Invoke();await Task.Delay(1200);await Shot("05-resumed-cooking");await Wait(()=>Hud.Activity.Cooking.Stage==CookingSession.Phase.Done,35);Check(State.Count("meal")==1&&State.Count("gas")==2,"Cooking duplicate/lost meal");await Close();
            await Open(ActivityPanel.Kind.Life);await Choose("microwave");await Close();Check(CampLife.Heating(State),"Microwave did not pause on close");Check(SaveSystem.TryRead(SaveSystem.SlotPath,out saved,out error),error);await Load(saved);await Open(ActivityPanel.Kind.Life);await Wait(()=>!CampLife.Heating(State));Check(State.Count("meal")==2&&State.life.battery==60&&State.Count("ready_meal")==1,"Microwave repeated cost/result");await Shot("06-microwave");await Close();
            await Leave();await Go("L5");await Open(ActivityPanel.Kind.Supplies);await Choose("dog.look");await Choose("dog.offer.water");await Shot("07-dog-care");await Close();Check(SaveSystem.TryRead(SaveSystem.SlotPath,out saved,out error),error);await Load(saved);await Open(ActivityPanel.Kind.Supplies);Check(State.Count("water")==0&&CampLife.Choices(State,"supplies").All(c=>!c.Id.StartsWith("dog.offer")),"Dog care repeated after load");await Choose("dog.wait");await Choose("dog.join");await Close();await Return();await Open(ActivityPanel.Kind.Life);await Tab("dog");await Choose("dog.bed");await Shot("08-dog-rest");await Tab("evening");Check(CampLife.Memories(State).All(m=>!m.Key.Contains("photo")),"Unacquired photo offered");await Choose(CampLife.Choices(State,"evening").First().Id);B("LifeNext").onClick.Invoke();await Task.Delay(150);Fits(Hud.Activity.transform);await Shot("09-evening-memory");await Close();
            Hud.RequestDayEnd();await Task.Delay(300);Fits(Hud.transform.Find("HUDViewport/PauseOverlay"));await Shot("10-day-end");Hud.ConfirmDayEnd();await Wait(()=>Hud.IsExploring&&State.day==3);Check(State.Has("memory.2")&&State.Has("dog_joined")&&State.Has("dog_place_ready"),"Day end loses life state");await Open(ActivityPanel.Kind.Evening);Check(CampLife.Memories(State).Count()==0,"Old memories on new day");await Close();await Open(ActivityPanel.Kind.Journal);B("PreviousJournal").onClick.Invoke();await Task.Delay(200);Fits(Hud.Activity.transform);await Shot("11-journal-retained");await Close();Log("PASS live: cancelled selection; real stock collection, repair, shared fuel; cooking and heating save/load; day-end busy guard; multi-step dog care/adoption/reload; earned memory pagination and previous-day journal.");
        }
        catch(Exception e){Log("FAIL "+e);throw;}
        finally{try{await Load(original);await Shot("12-original-restored");Log("RESTORED original journey without overwriting real save.");}finally{SaveSystem.TestSlotPath=old;}}
        return "PASS life flow; original journey restored. Dialogue and music unchanged.";
    }
    public static async Task<string> Finish()
    {
        Check(SaveSystem.TryRead(Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/Revalidation-2026-09-13/CityMapReview/checkpoint.json")),out var original,out var error),error);
        var old=SaveSystem.TestSlotPath;SaveSystem.TestSlotPath=Path.Combine(Folder,"final-isolated.json");
        try
        {
            var s=Copy(original);s.fuel=0;await Load(s);await Leave();await Go("L2");await Open(ActivityPanel.Kind.Supplies);await Choose("emergency.fuel");Check(State.fuel==2,"Zero-fuel UI recovery failed");await Close();await Return();await Open(ActivityPanel.Kind.Life);await Tab("energy");B("EnergyDestination").onClick.Invoke();B("EnergyDestination").onClick.Invoke();await Task.Delay(150);Fits(Hud.Activity.transform);await Shot("13-energy-refined");await Close();
            var cross=Copy(State);cross.regionId=RegionTravel.NextRegion;cross.location=RegionTravel.Entry;cross.Set(RegionTravel.NextRegion+".opened");cross.life.energyDestination="L1";
            Check(CampLife.Choices(cross,"energy").First(c=>c.Id=="generate").Detail.Contains("먼저 선택"),"Stale destination promises impossible travel");
            int fuel=State.fuel,food=State.Count("packaged_food");Hud.RequestDayEnd();await Task.Delay(250);Hud.ConfirmDayEnd();await Wait(()=>Hud.IsExploring&&State.day==3);Check(!State.Has("memory.2")&&State.fuel==fuel&&State.Count("packaged_food")==food,"Skipping memory punished player");Log("PASS final: zero-fuel walking recovery through UI; regional destination check; text fits; skip evening record without extra costs.");
        }
        finally{try{await Load(original);await Open(ActivityPanel.Kind.Life);Fits(Hud.Activity.transform);await Shot("14-current-life-panel");await Close();}finally{SaveSystem.TestSlotPath=old;}}
        Check(string.IsNullOrEmpty(SaveSystem.TestSlotPath),"Test override remains");return "PASS final life checks; original progress and real save path restored.";
    }
    public static async Task<string> BookTransition()
    {
        Check(SaveSystem.TryRead(Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/Revalidation-2026-09-13/CityMapReview/checkpoint.json")),out var original,out var error),error);
        var old=SaveSystem.TestSlotPath;SaveSystem.TestSlotPath=Path.Combine(Folder,"book-isolated.json");
        try
        {
            var s=Copy(original);Act(s,"meal","eat.packaged_food");await Load(s);B("WeekAction").onClick.Invoke();await Task.Delay(300);B("InteractionChoice_0").onClick.Invoke();await Wait(()=>State.weekEvent=="request");await Task.Delay(2000);
            Check(GameObject.Find("JourneyStage").GetComponent<Image>().sprite.name=="journal","Meal stage obscures the book scene");Log("PASS meal-to-story stage: first book scene restored while narrative is running.");
        }
        finally{try{await Load(original);}finally{SaveSystem.TestSlotPath=old;}}
        return "PASS book transition; original journey restored.";
    }
}

public static class VerifyTutorialStructure
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/Revalidation-2026-09-13/Tutorial"));
    static GameHud Hud=>GameHud.Instance;static JourneyState State=>SaveSystem.Current;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Log(string message){Directory.CreateDirectory(Folder);File.AppendAllText(Path.Combine(Folder,"verification.txt"),message+"\n");}
    static JourneyState Copy(JourneyState s)=>JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(s));
    static JourneyState Fixture()=>new JourneyState{day=1,location="L1",inStore=true,answeredSoi=true,bookSeen=true,fuel=3};
    static void Guide(JourneyState s,string expected){Check(Copy(s).Valid(),"Invalid checkpoint "+expected);Check(JourneyTutorial.Current(s).Id==expected,"Expected "+expected+", got "+JourneyTutorial.Current(s).Id);}
    static void Event(JourneyState s,string id){Check(FirstWeekStory.Begin(s,id),"Cannot begin "+id);while(s.weekLine<WeekScript.Find(id).lines.Length)FirstWeekStory.ConfirmLine(s);Check(FirstWeekStory.Finish(s),"Cannot finish "+id);}
    static void Inspect(JourneyState s,string id){s.exploringPlace=id;for(int i=0;i<PlaceInspection.Points(id).Length;i++)if(!PlaceInspection.Checked(s,id,i))Check(PlaceInspection.Inspect(s,id,i,out _),"Cannot inspect "+id);}
    static JourneyState CompletedPhoto()
    {var s=Fixture();s.inStore=false;s.Set("water_checked");s.Set("food_checked");s.Set(RegionTravel.Drawing);s.Set("camera_received");s.Set("first_photo_taken");s.Set(RegionTravel.Photo);s.Set(RegionTravel.Route);s.Set("story.discovered.L4");s.Set("story.discovered.L6");return s;}
    public static string Model()
    {
        Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"verification.txt"),"");
        Check(JourneyTutorial.Current(new JourneyState()).Id=="opening","Opening guidance");
        var s=Fixture();Guide(s,"supplies");s.Set("water_checked");Guide(s,"supplies");s.Set("food_checked");s.Add("water","생수",2);s.Add("packaged_food","포장 식량",2);Guide(s,"return.meal");
        s.inStore=false;Guide(s,"meal");Check(CampLife.Apply(s,"meal","eat.packaged_food",out _),"First meal");Guide(s,"book");Event(s,"request");Guide(s,"go.L3");
        s.exploringPlace="L3";Guide(s,"pencils");Event(s,"pencils");Guide(s,"return.deliver");s.exploringPlace="";Guide(s,"deliver");Event(s,"deliver");Guide(s,"draw");Event(s,"draw");Guide(s,"go.L2");
        s.exploringPlace="L2";Guide(s,"meet");Event(s,"meet");Guide(s,"accept");Event(s,"accept");Guide(s,"go.L4");s.exploringPlace="L4";Guide(s,"bag");Event(s,"bag");Guide(s,"go.L2");
        s.exploringPlace="L2";Guide(s,"handover");Event(s,"handover");Guide(s,"go.L6");s.exploringPlace="L6";Guide(s,"photo");Event(s,"shoot");Guide(s,"album");Event(s,"album");
        Guide(s,"inspect.L6");Inspect(s,"L6");Guide(s,"go.L4");Inspect(s,"L4");Guide(s,"go.L10");Inspect(s,"L10");Guide(s,"go.L7");Inspect(s,"L7");Guide(s,"return.depart");
        s.exploringPlace="";Guide(s,"park.gate");Check(RegionExploration.Travel(s,"L7"),"Vehicle to gate");Guide(s,"depart");Check(RegionTravel.Travel(s,RegionTravel.NextRegion),"Departure");Guide(s,"complete");Check(JourneyTutorial.Completed(s).All(x=>x),"Tutorial not completed by actual actions");
        var shortage=Fixture();shortage.Set("water_checked");shortage.Set("food_checked");Guide(shortage,"extra.food");Check(CampLife.Apply(shortage,"supplies","emergency.food",out _),"No supply recovery");Guide(shortage,"return.meal");
        var ready=CompletedPhoto();ready.fuel=0;Guide(ready,"walk.L6");ready.exploringPlace="L6";Inspect(ready,"L6");Inspect(ready,"L4");Inspect(ready,"L10");Inspect(ready,"L7");Guide(ready,"go.L2");ready.exploringPlace="L2";Guide(ready,"fuel");
        ready=CompletedPhoto();ready.exploringPlace="L4";ready.Set(RegionTravel.Photo,"false");Guide(ready,"return.album");
        var existing=Fixture();existing.inStore=false;existing.Set("water_checked");existing.Set("food_checked");existing.Set("drawing_requested");existing.Add("pencils","색연필",1,2);Guide(existing,"deliver");
        var graph=ContinuationGraph.Load();foreach(var node in graph.nodes){if(node.scene!="preparation")Check(Resources.Load<Sprite>("Live49/Stages/"+node.scene)!=null,"Missing story art "+node.scene);if(!string.IsNullOrEmpty(node.next))Check(graph.nodes.Any(n=>n.id==node.next),"Missing story next "+node.next);foreach(var c in node.choices)Check(graph.nodes.Any(n=>n.id==c.target),"Missing choice target "+c.target);}
        var week=JsonUtility.FromJson<WeekScript>(Resources.Load<TextAsset>("Live49/FirstWeek/story").text);foreach(var line in week.events.SelectMany(e=>e.lines))Check(Resources.Load<Sprite>("Live49/Stages/"+line.art)!=null,"Missing first-week art "+line.art);
        foreach(var site in RegionExploration.Sites.Where(p=>!string.IsNullOrEmpty(p.Art)))Check(Resources.Load<Sprite>("Live49/Stages/"+site.Art)!=null,"Missing site art "+site.Id);
        var scenes=EditorBuildSettings.scenes.Where(x=>x.enabled).Select(x=>Path.GetFileNameWithoutExtension(x.path)).ToArray();Check(scenes.Length==2&&scenes.Contains(SceneNames.Title)&&scenes.Contains(SceneNames.Game),"Unexpected build scenes");
        var origin=Fixture();origin.location="camper";origin.inStore=false;origin.fuel=0;Guide(origin,"origin.fuel");int minutes=origin.minutes;
        Check(CampLife.Apply(origin,"energy","emergency.origin",out _)&&origin.fuel==2&&origin.minutes==minutes+60,"Origin fuel recovery failed");
        Check(!CampLife.Apply(origin,"energy","emergency.origin",out _),"Origin recovery granted twice");Guide(origin,"go.L1");Check(RegionExploration.Travel(origin,"L1"),"Recovered origin cannot depart");
        Log("PASS model: actionable stages from first supplies to next region, actual first-week effects and two-point inspections, route prerequisites, pre-owned tools, zero-fuel origin recovery without duplicate grant, low fuel and food recovery, album separate from photo, legacy JSON, resource/graph references, exactly Title+Game scenes.");
        return "PASS tutorial progression and asset audit";
    }
    static async Task Wait(Func<bool> predicate,string why){var until=DateTime.UtcNow.AddSeconds(20);while(!predicate()){if(DateTime.UtcNow>until)throw new Exception("Timeout "+why);await Task.Delay(40);}}
    static Button B(string n)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==n);
    static async Task Load(JourneyState s){Check(SaveSystem.Write(SaveSystem.SlotPath,s,out var error)&&SaveSystem.QueueLoad(out error),error);var op=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>op.isDone&&Hud!=null&&Hud.IsExploring,"load");await Task.Delay(350);}
    static void Fit(Transform root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<TMP_Text>())Check(!t.isTextOverflowing,"Text overflow "+t.name+": "+t.text);}
    static async Task Shot(string name){await Task.Delay(200);ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));await Task.Delay(300);}
    static async Task CloseGuide(){Hud.HandleEscape();await Task.Delay(250);Check(Hud.IsPaused&&!Hud.Tutorial.IsOpen,"Guide closed two levels");Hud.HandleEscape();await Wait(()=>!Hud.IsPaused,"resume");}
    public static async Task<string> Run()
    {
        Model();var original=State==null?null:Copy(State);string old=SaveSystem.TestSlotPath,real=SaveSystem.SlotPath;byte[] bytes=File.Exists(real)?File.ReadAllBytes(real):null;SaveSystem.TestSlotPath=Path.Combine(Folder,"isolated.json");
        try
        {
            var s=Fixture();s.inStore=false;await Load(s);Fit(Hud.transform);Check(GameObject.Find("PauseButton")==null,"Permanent menu button returned");
            string before=JsonUtility.ToJson(State);Hud.OpenPause();await Task.Delay(250);B("Button_플레이 안내").onClick.Invoke();
            for(int i=0;i<7;i++){B("GuideChapter_"+i).onClick.Invoke();await Task.Delay(50);Fit(Hud.Tutorial.transform);}
            Check(JsonUtility.ToJson(State)==before,"Reading guide changed journey");B("GuideChapter_1").onClick.Invoke();await Shot("01-first-exploration");await CloseGuide();
            Check(Time.timeScale==1&&!GamePause.IsPaused,"Guide left clock paused");
            await Load(CompletedPhoto());Hud.OpenTutorial();await Task.Delay(250);Fit(Hud.Tutorial.transform);await Shot("02-next-route");await CloseGuide();
            Check(SaveSystem.QueueLoad(out var error),error);var op=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>op.isDone&&Hud!=null&&Hud.IsExploring,"reload");await Task.Delay(350);Check(Hud.Objective==JourneyTutorial.Current(State).Title,"Reload lost current goal");
            var poor=Fixture();poor.Set("water_checked");poor.Set("food_checked");await Load(poor);Hud.OpenTutorial();Fit(Hud.Tutorial.transform);await Shot("03-food-recovery");await CloseGuide();
            var b=CompletedPhoto();b.exploringPlace="L8";b.Set("story.discovered.L8");await Load(b);await Wait(()=>BanditEncounter.Active(State)&&Hud.IsExploring,"bandit");Hud.OpenTutorial();Fit(Hud.Tutorial.transform);await Shot("04-encounter-guide");await CloseGuide();Check(State.bandit.stage==0,"Guide consumed encounter");
            var cooking=CompletedPhoto();cooking.cookingUnlocked=true;cooking.Add("gas","가스",2,1);cooking.Add("ingredients","식재료",2);var session=new CookingSession(cooking);Check(session.Start(),"Cooking fixture");await Load(cooking);Hud.OpenTutorial();Fit(Hud.Tutorial.transform);await Shot("05-resume-cooking");await CloseGuide();
            var origin=Fixture();origin.location="camper";origin.inStore=false;origin.fuel=0;await Load(origin);Hud.OpenTutorial();Fit(Hud.Tutorial.transform);await Shot("06-origin-fuel-guide");await CloseGuide();
            Hud.OpenActivity(ActivityPanel.Kind.Life);await Wait(()=>Hud.Activity!=null&&Hud.Activity.IsOpen,"life");await Task.Delay(250);B("LifeTab_energy").onClick.Invoke();await Task.Delay(150);Fit(Hud.Activity.transform);await Shot("07-origin-fuel-recovery");B("LifeConfirm").onClick.Invoke();await Task.Delay(150);Check(State.fuel==2,"Origin recovery UI failed");Hud.HandleEscape();await Wait(()=>!Hud.Activity.IsOpen,"close life");
            Log("PASS live: all seven help pages fit at 1920x1080; read-only help; ESC returns one level; HUD hidden while paused; no permanent menu; state-based goal after reload; resource recovery; bandit and paused cooking guidance; zero-fuel origin recovery through actual life UI.");
        }
        catch(Exception e){Log("FAIL "+e);throw;}
        finally
        {
            try{if(Hud!=null&&Hud.Tutorial!=null&&Hud.Tutorial.IsOpen)await CloseGuide();if(Hud!=null&&Hud.IsPaused){Hud.Resume();await Task.Delay(300);}if(original!=null)await Load(original);else{var op=SceneManager.LoadSceneAsync(SceneNames.Title);await Wait(()=>op.isDone,"restore title");SaveSystem.Current=null;}}
            finally{SaveSystem.TestSlotPath=old;Check(bytes==null?!File.Exists(real):File.Exists(real)&&bytes.SequenceEqual(File.ReadAllBytes(real)),"Real save changed");Log("RESTORED original session; real autosave unchanged.");}
        }
        return "PASS tutorial UI verification";
    }
}

public static class VerifySearchRuntime
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/Revalidation-2026-09-13/SearchRuntime"));
    static GameHud Hud=>GameHud.Instance;static JourneyState State=>SaveSystem.Current;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static JourneyState Copy(JourneyState s)=>JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(s));
    static JourneyState Fixture()=>new JourneyState{day=1,location="L1",inStore=true,answeredSoi=true,bookSeen=true};
    static void Valid(JourneyState s){Check(s.Valid()&&Copy(s).Valid(),"Search checkpoint invalid: "+s.search?.phase);}
    static void Log(string s){Directory.CreateDirectory(Folder);File.AppendAllText(Path.Combine(Folder,"verification.txt"),s+"\n");}
    static void CheckTwo(JourneyState s,string grade){Check(SearchSession.Check(s,0,grade)&&SearchSession.Check(s,1,grade),"Check pair");Check(!SearchSession.Check(s,1,grade),"Repeated check");Check(SearchSession.Checkpoint(s),"Checkpoint");Valid(s);}
    public static string Model()
    {
        Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"verification.txt"),"");
        var s=Fixture();Check(SearchSession.Begin(s,"L1",0,true,false,()=>.99f),"Begin focus");Valid(s);Check(s.minutes==10&&s.Count("water")==0,"Premature loot");CheckTwo(s,"miss");Check(s.Count("water")==2,"Miss lost mandatory water");
        Check(!SearchSession.Checkpoint(s)&&!SearchSession.Check(s,2,"great"),"Checkpoint repeated / early third check");
        Check(SearchSession.Continue(s)&&!SearchSession.Continue(s)&&s.minutes==15,"Repeated time cost");SearchSession.Check(s,2,"miss");Check(SearchSession.Finish(s,"complete"),"Finish");Check(s.search.risk==.95f&&s.Count("water")==2,"Failure risk / failed extra roll");Check(!SearchSession.Finish(s,"complete"),"Duplicate reward");Valid(s);SearchSession.Acknowledge(s);Check(!SearchSession.Active(s),"Quiet result close");
        Check(SearchSession.Begin(s,"L1",1,false,false,()=>.99f),"Quick food");Check(s.Count("packaged_food")==2&&s.minutes==20&&RegionExploration.Discovered(s,"L2")&&RegionExploration.Discovered(s,"L3"),"Food or map guaranteed flow");Valid(s);SearchSession.Acknowledge(s);
        Check(!SearchSession.Begin(s,"L1",0,true),"Completed spot reopened");
        s=Fixture();SearchSession.Begin(s,"L1",0,true,false,()=>.59f);CheckTwo(s,"great");SearchSession.Continue(s);SearchSession.Check(s,2,"great");SearchSession.Finish(s,"complete");Check(s.Count("water")==3,"Great bonus not applied");Valid(s);
        s=Fixture();SearchSession.Begin(s,"L1",0,true,false,()=>0);CheckTwo(s,"good");Check(SearchSession.Finish(s,"stopped")&&s.minutes==10&&s.Count("water")==2,"Checkpoint stop loss or extras");SearchSession.Acknowledge(s);Check(s.search.phase=="warning","Noise missing");Check(!RegionExploration.Travel(s,"L2"),"Travel during noise");SearchSession.Decide(s,true);Check(s.Value("search.caution.region01")=="1","Caution not recorded");Valid(s);
        s=Fixture();SearchSession.Begin(s,"L1",0,true,false,()=>0);s=Copy(s);SearchSession.Finish(s,"interrupted");Check(s.Count("water")==0&&s.minutes==10&&!s.Has("water_checked"),"Interrupted early paid loot");SearchSession.Acknowledge(s);SearchSession.Decide(s,false);Check(!SearchSession.FocusAvailable(s,"L1",0)&&SearchSession.Begin(s,"L1",0,false),"Interrupted recovery blocked");Check(s.Count("water")>=2,"Quick recovery lost required water");Valid(s);
        s=Fixture();SearchSession.Begin(s,"L1",0,true,false,()=>.99f);CheckTwo(s,"good");s=Copy(s);SearchSession.Finish(s,"interrupted");Check(s.Count("water")==2&&s.minutes==10,"Interrupted secured item lost");Valid(s);
        var bad=Copy(s);bad.search.rolls[0]=float.NaN;Check(!bad.Valid(),"NaN accepted");bad=Copy(s);bad.search.centers=null;Check(!bad.Valid(),"Missing centers accepted");
        Check(SearchSession.Grade(.60f,.60f,false)=="great"&&SearchSession.Grade(.69f,.60f,false)=="good"&&SearchSession.Grade(.74f,.60f,false)=="miss"&&SearchSession.Grade(.74f,.60f,true)=="good","Grading/slow mode");
        s=Fixture();s.Set("water_checked");s.Set("food_checked");s.inStore=false;s.exploringPlace="L2";SearchSession.Begin(s,"L2",0,false,false,()=>.99f);Check(s.fuel==5&&s.minutes==5,"L2 fuel/cost mismatch");SearchSession.Acknowledge(s);SearchSession.Begin(s,"L2",1,false,false,()=>.99f);Check(RegionExploration.Completed(s,"L2")&&RegionExploration.Discovered(s,"L4")&&s.minutes==10,"Region unlock mismatch");Valid(s);
        Log("PASS model: guaranteed first water/food, quick and focus time, optional loot bonus, misses and noise, checkpoint stop, duplicate prevention, interruption before/after securing, quick recovery, region fuel and discovery, malformed saves, slow grading.");return "PASS search model";
    }
    static async Task Wait(Func<bool> f,string why){var end=DateTime.UtcNow.AddSeconds(20);while(!f()){if(DateTime.UtcNow>end)throw new Exception("Timeout "+why);await Task.Delay(30);}}
    static Button B(string n)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==n);
    static async Task Click(string n){B(n).onClick.Invoke();await Task.Delay(90);}
    static async Task Load(JourneyState s){Check(SaveSystem.Write(SaveSystem.SlotPath,s,out var error)&&SaveSystem.QueueLoad(out error),error);var op=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>op.isDone&&Hud!=null&&Hud.IsExploring,"load");await Task.Delay(500);}
    static void Fit(Transform root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<TMP_Text>())Check(!t.isTextOverflowing,"Overflow "+t.name+": "+t.text);}
    static async Task Shot(string n){await Task.Delay(120);ScreenCapture.CaptureScreenshot(Path.Combine(Folder,n+".png"));await Task.Delay(250);}
    static async Task AdvanceTo(float t){while(Hud.Search.Elapsed<t&&State.search.phase=="playing"){Hud.Search.Tick(.1f);await Task.Delay(18);}}
    public static async Task<string> Run()
    {
        Model();var original=State==null?null:Copy(State);string old=SaveSystem.TestSlotPath,real=SaveSystem.SlotPath;byte[] bytes=File.Exists(real)?File.ReadAllBytes(real):null;SaveSystem.TestSlotPath=Path.Combine(Folder,"isolated.json");
        try
        {
            await Load(Fixture());await Click("StoreWater");Check(Hud.SearchOpen,"Store action not connected");Fit(Hud.Search.transform);await Shot("01-search-choice");
            await Click("FocusSearch");State.search.noiseRoll=.99f;State.search.rolls=new[]{.99f,.99f};
            float elapsed=Hud.Search.Elapsed;Hud.Search.Strike();Check(State.search.checks==0,"Input outside check counted");Hud.HandleEscape();await Task.Delay(250);Hud.Search.Tick(1);Check(Hud.IsPaused&&Hud.Search.Elapsed==elapsed,"ESC failed to freeze");Check(B("Button_설정")!=null,"Pause settings unavailable");Hud.HandleEscape();await Wait(()=>!Hud.IsPaused,"resume");await Task.Delay(150);
            await AdvanceTo(State.search.schedule[0]+.4f);Check(GameObject.Find("SkillDial")!=null&&GameObject.Find("SkillDial").GetComponent<CanvasRenderer>()!=null,"No rendered preview ring");Fit(Hud.Search.transform);await Shot("02-skill-preview");
            await AdvanceTo(State.search.schedule[0]+.8f+(State.search.slow?1.9f:1.25f)*State.search.centers[0]);Hud.Search.Strike();await Task.Delay(120);Check(State.search.checks==1,"Strike not registered");await Shot("03-check-feedback");
            await AdvanceTo(8.2f);await Wait(()=>State.search.phase=="checkpoint","checkpoint");Check(State.Count("water")==2&&State.minutes==10,"Checkpoint water/cost");Fit(Hud.Search.transform);await Shot("04-secured-water");
            Check(SaveSystem.TryRead(SaveSystem.SlotPath,out var saved,out var error),error);await Load(saved);Check(Hud.SearchOpen&&State.search.phase=="result"&&State.search.reason=="interrupted"&&State.Count("water")==2,"Reload duplicated/lost checkpoint");Fit(Hud.Search.transform);await Shot("05-restored-search");await Click("AcknowledgeSearch");if(SearchSession.Active(State))await Click("StayAfterSearch");Check(!Hud.SearchOpen&&!Hud.IsPaused,"Result did not close");
            await Click("StoreFood");await Click("QuickSearch");Fit(Hud.Search.transform);Check(State.Has("food_checked")&&RegionExploration.Discovered(State,"L2"),"Quick food progression");await Shot("06-quick-food");await Click("AcknowledgeSearch");if(SearchSession.Active(State))await Click("StayAfterSearch");
            var noisy=Fixture();noisy.Set("water_checked");noisy.Set("food_checked");noisy.inStore=false;noisy.exploringPlace="L2";SearchSession.Begin(noisy,"L2",0,false,false,()=>0);await Load(noisy);await Click("AcknowledgeSearch");Fit(Hud.Search.transform);await Shot("07-noise-choice");await Click("ReturnAfterSearch");await Wait(()=>!RegionExploration.Outside(State)&&Hud.IsExploring,"return actual camper");Check(State.location=="L1"&&State.fuel==5,"Camper moved during return");
            var region=Fixture();region.Set("water_checked");region.Set("food_checked");region.inStore=false;region.exploringPlace="L3";await Load(region);await Click("InspectRegion_0");await Click("SlowSearch");await Click("FocusSearch");State.search.noiseRoll=.999f;State.search.rolls=new[]{.99f,.99f};
            await AdvanceTo(8.8f);await Wait(()=>State.search.phase=="checkpoint","region checkpoint");await Click("ContinueSearch");await AdvanceTo(14.2f);Check(State.search.phase=="result"&&State.search.checks==3&&State.minutes==15&&PlaceInspection.Checked(State,"L3",0),"Full focus region flow");Fit(Hud.Search.transform);await Shot("08-full-focus-result");await Click("AcknowledgeSearch");
            await Load(Fixture());await Click("StoreWater");string goodPath=SaveSystem.TestSlotPath;SaveSystem.TestSlotPath=Folder;await Click("QuickSearch");Check(Hud.Search.SaveFailed,"Save error did not stop play");int stock=State.Count("water");SaveSystem.TestSlotPath=goodPath;await Click("RetrySearchSave");Check(!Hud.Search.SaveFailed&&State.Count("water")==stock,"Save retry granted twice");
            Log("PASS live: actual store buttons, choice/preview/checkpoint/result/noise layout, timed strike and ignored early input, ESC pause/settings/resume, real save reload after checkpoint, quick food unlock, walking return to original parking, save failure/retry without duplicate loot. Timer advanced through the same Tick method via CLI; human timing difficulty remains playtest work.");
        }
        catch(Exception e){Log("FAIL "+e);throw;}
        finally
        {
            try{SaveSystem.TestSlotPath=Path.Combine(Folder,"isolated.json");if(Hud!=null&&Hud.IsPaused){Hud.Resume();await Task.Delay(350);}if(original!=null)await Load(original);else{var op=SceneManager.LoadSceneAsync(SceneNames.Title);await Wait(()=>op.isDone,"restore title");SaveSystem.Current=null;}}
            finally{SaveSystem.TestSlotPath=old;Check(bytes==null?!File.Exists(real):File.Exists(real)&&bytes.SequenceEqual(File.ReadAllBytes(real)),"Real autosave changed");Log("RESTORED original session; real autosave unchanged.");}
        }
        return "PASS search runtime";
    }
}

public static class VerifyKitchenPresentation
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/Revalidation-2026-09-13/KitchenPresentation"));
    static GameHud Hud=>GameHud.Instance;static JourneyState State=>SaveSystem.Current;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static JourneyState Copy(JourneyState s)=>JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(s));
    static void Log(string s){Directory.CreateDirectory(Folder);File.AppendAllText(Path.Combine(Folder,"verification.txt"),s+"\n");}
    static JourneyState Fixture(){var s=new JourneyState{day=2,location="L1",answeredSoi=true,bookSeen=true,cookingUnlocked=true,fuel=3};s.Set("water_checked");s.Set("food_checked");s.Add("gas","가스",3,1);s.Add("ingredients","식재료",3);return s;}
    static async Task Wait(Func<bool> f,string why,int seconds=25){var end=DateTime.UtcNow.AddSeconds(seconds);while(!f()){if(DateTime.UtcNow>end)throw new Exception("Timeout "+why);await Task.Delay(30);}}
    static Button B(string n)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==n);
    static async Task Click(string n){B(n).onClick.Invoke();await Task.Delay(100);}
    static async Task Load(JourneyState s){Check(SaveSystem.Write(SaveSystem.SlotPath,s,out var error)&&SaveSystem.QueueLoad(out error),error);var op=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>op.isDone&&Hud!=null&&Hud.IsExploring,"load");await Task.Delay(350);}
    static async Task Open(){Hud.OpenActivity(ActivityPanel.Kind.Kitchen);await Wait(()=>Hud.Activity!=null&&Hud.Activity.IsOpen,"kitchen");await Task.Delay(300);Fit();}
    static async Task Close(){Hud.HandleEscape();await Wait(()=>!Hud.Activity.IsOpen&&!Hud.IsPaused,"close");}
    static void Fit(){Canvas.ForceUpdateCanvases();foreach(var t in Hud.Activity.GetComponentsInChildren<TMP_Text>())Check(!t.isTextOverflowing,"Overflow "+t.name+": "+t.text);}
    static async Task Shot(string n){Fit();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,n+".png"));await Task.Delay(250);}
    static void Dial(int level,bool drag=true)
    {
        var dial=Hud.Activity.Dial;var rt=dial.rectTransform;float a=(level-2)*60*Mathf.Deg2Rad;
        var pos=RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(rt.rect.center+new Vector2(Mathf.Sin(a),Mathf.Cos(a))*70));
        var e=new PointerEventData(EventSystem.current){position=pos,button=PointerEventData.InputButton.Left};
        if(drag)ExecuteEvents.Execute(dial.gameObject,e,ExecuteEvents.dragHandler);else ExecuteEvents.Execute(dial.gameObject,e,ExecuteEvents.pointerDownHandler);
    }
    public static async Task<string> Run()
    {
        Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"verification.txt"),"");var original=State==null?null:Copy(State);string old=SaveSystem.TestSlotPath,real=SaveSystem.SlotPath;byte[] bytes=File.Exists(real)?File.ReadAllBytes(real):null;SaveSystem.TestSlotPath=Path.Combine(Folder,"isolated.json");
        try
        {
            var s=Fixture();s.cookingUnlocked=false;await Load(s);await Open();Check(!B("StartCooking").interactable,"Unrepaired stove cooks");await Shot("01-repair-needed");await Close();
            await Load(Fixture());await Open();Check(Hud.Activity.transform.Find("ActivityBody/KitchenStage/Pot").GetComponent<Image>().sprite.rect.width==714,"Pot original missing");await Shot("02-ready");
            await Click("StartCooking");Dial(3,false);Check(Hud.Activity.Cooking.Level==3,"Pointer down did not select high heat");Dial(1);Check(Hud.Activity.Cooking.Level==1,"Drag did not select low heat");Dial(2);Check(Hud.Activity.Cooking.Level==2&&State.Count("gas")==2&&State.Count("ingredients")==2&&State.fuel==3&&State.minutes==25,"Control changed cost or fuel");
            await Task.Delay(1500);await Click("PauseCooking");float heat=Hud.Activity.Cooking.Heat;Dial(3);await Task.Delay(200);Check(Hud.Activity.Cooking.Paused&&Hud.Activity.Cooking.Level==2&&Hud.Activity.Cooking.Heat==heat,"Paused dial or heat changed");await Shot("03-paused");await Close();
            Check(SaveSystem.TryRead(SaveSystem.SlotPath,out var saved,out var error),error);await Load(saved);await Open();Check(Hud.Activity.Cooking.Paused&&State.Count("gas")==2&&State.minutes==25,"Reload spent or resumed automatically");await Shot("04-restored");await Click("PauseCooking");Dial(3);
            await Wait(()=>Hud.Activity.Cooking.Heat>=85,"boiling");await Shot("05-boiling");Dial(1);await Task.Delay(120);Check(Hud.Activity.Cooking.Heat>78,"Dial changed temperature instantly");
            await Wait(()=>Hud.Activity.Cooking.Stage==CookingSession.Phase.Done,"meal",25);Fit();Check(State.Count("meal")==1&&State.Count("gas")==2&&State.Count("ingredients")==2&&State.minutes==25&&Hud.Activity.Cooking.Level==0,"Completion duplicated meal or cost");await Shot("06-finished");await Task.Delay(300);Check(State.Count("meal")==1,"Repeated completion");await Close();
            await Load(Fixture());await Open();string good=SaveSystem.TestSlotPath;SaveSystem.TestSlotPath=Folder;await Click("StartCooking");Check(Hud.Activity.KitchenSaveFailed&&Hud.Activity.Cooking.Paused,"Save failure did not freeze kitchen");await Shot("07-save-retry");SaveSystem.TestSlotPath=good;await Click("PauseCooking");Check(!Hud.Activity.KitchenSaveFailed&&Hud.Activity.Cooking.Paused&&State.Count("gas")==2,"Retry spent again or resumed silently");await Close();
            Log("PASS: original 714x523 pot, unrepaired block, real pointer-down/drag levels, paused input/heat, gradual heat response, cooking reload, one-time cost and meal, vehicle fuel unchanged, automatic flame off, save-failure freeze/retry. Text fit at 1920x1080. Actual cooking completed using elapsed game frames.");
        }
        catch(Exception e){Log("FAIL "+e);throw;}
        finally
        {
            try{SaveSystem.TestSlotPath=Path.Combine(Folder,"isolated.json");if(Hud?.Activity!=null&&Hud.Activity.IsOpen)await Close();if(original!=null)await Load(original);else{var op=SceneManager.LoadSceneAsync(SceneNames.Title);await Wait(()=>op.isDone,"restore");SaveSystem.Current=null;}}
            finally{SaveSystem.TestSlotPath=old;Check(bytes==null?!File.Exists(real):File.Exists(real)&&bytes.SequenceEqual(File.ReadAllBytes(real)),"Real save changed");Log("RESTORED original session; real autosave unchanged.");}
        }
        return "PASS kitchen presentation";
    }
}


public static class VerifyBanditEncounter
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/Revalidation-2026-09-13/BanditEncounter"));
    static JourneyState State=>SaveSystem.Current;
    static GameHud Hud=>GameHud.Instance;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static JourneyState Copy(JourneyState s)=>JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(s));
    static JourneyState Fixture(string parking="L3",bool outside=true)
    {
        var s=new JourneyState{day=3,location=parking,exploringPlace=outside?"L8":"",answeredSoi=true,bookSeen=true,fuel=2};
        s.Set("water_checked");s.Set("food_checked");s.Set(RegionTravel.Drawing);s.Set("story.discovered.L8");
        s.Add("water","생수",2);s.Add("packaged_food","포장 식량",2);s.Add("colored_pencils","색연필",1,2);s.Add("sejin_toolbag","공구 가방",1,2);
        return s;
    }
    static void Log(string value){Directory.CreateDirectory(Folder);File.AppendAllText(Path.Combine(Folder,"verification.txt"),value+"\n");}
    public static string Model()
    {
        Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"verification.txt"),"");
        var legacy=Fixture();Check(legacy.Valid()&&Copy(legacy).Valid()&&!BanditEncounter.Started(Copy(legacy)),"Legacy save incompatible");
        var early=Fixture();early.Set(RegionTravel.Drawing,"false");Check(!BanditEncounter.Begin(early),"Before drawing trigger");
        early=Fixture();early.Set("food_checked","false");Check(!BanditEncounter.Begin(early),"Before supplies trigger");
        early=Fixture("L8",false);Check(!BanditEncounter.Begin(early),"Triggered inside parked camper");
        early=Fixture();early.exploringPlace="L2";Check(!BanditEncounter.Begin(early),"Triggered on essential route");
        foreach(string parking in new[]{"L1","L3","L8"})foreach(string choice in new[]{"food","water","detour"})
        {
            var s=Fixture(parking);Check(BanditEncounter.Begin(s)&&!BanditEncounter.Begin(s)&&s.Valid(),"Begin invalid/repeat");
            Check(RegionExploration.TravelBlock(s,"L1")!=null&&FirstWeekStory.Action(s)==null,"Encounter bypass");
            Check(!PlaceInspection.Inspect(s,"L8",0,out _)&&SaveSlots.Progress(s).Contains("선택")&&SaveSlots.ArtResource(s)=="Live49/Encounters/Bandit/alley","Inspection bypass/save summary");
            Check(!CampLife.Apply(s,"supplies","emergency.food",out _),"Life action bypass");
            s=Copy(s);Check(s.Valid(),"Choice checkpoint");int fuel=s.fuel;
            Check(BanditEncounter.Choose(s,choice)&&!BanditEncounter.Choose(s,choice),"Repeated result");
            s=Copy(s);Check(s.Valid()&&s.bandit.stage==1,"Result checkpoint invalid");
            Check(s.Count("water")== (choice=="water"?1:2)&&s.Count("packaged_food")== (choice=="food"?1:2),"Wrong supplies");
            Check(s.minutes==(choice=="detour"?20:5)&&s.location==parking&&s.fuel==fuel,"Parking/fuel/time");
            Check(s.Count("colored_pencils")==1&&s.Count("sejin_toolbag")==1,"Story items consumed");
            Check(BanditEncounter.FinishResult(s)&&!BanditEncounter.FinishResult(s)&&!BanditEncounter.BeginReturn(s),"Result transition invalid");
            s.exploringPlace="";s.day++;Check(BanditEncounter.BeginReturn(s)&&s.Valid(),"Return at variable parking/day invalid");
            s=Copy(s);Check(BanditEncounter.FinishReturn(s)&&!BanditEncounter.FinishReturn(s)&&s.Valid(),"Return repeat");
            s.exploringPlace="L8";Check(!BanditEncounter.Begin(s),"Repeat encounter on revisit");
        }
        var poor=Fixture();poor.items.Clear();poor.fuel=0;BanditEncounter.Begin(poor);
        Check(!BanditEncounter.Choose(poor,"water")&&!BanditEncounter.Choose(poor,"food")&&!BanditEncounter.Choose(poor,"bad"),"Unaffordable/unknown choice allowed");
        Check(BanditEncounter.Choose(poor,"detour")&&poor.Valid(),"No free escape");
        var bad=Copy(poor);bad.bandit.outcome="bad";Check(!bad.Valid(),"Corrupt outcome accepted");
        bad=Copy(poor);bad.exploringPlace="L2";Check(!bad.Valid(),"Wrong encounter place accepted");
        bad=Copy(poor);bad.bandit.stage=3;Check(!bad.Valid(),"Return checkpoint outdoors accepted");
        bad=Copy(poor);bad.bandit.day=5;Check(!bad.Valid(),"Future event accepted");
        Log("PASS model: legacy saves; first-supplies/drawing gates; optional location; camper interior safe; three parking locations x three outcomes; exact costs; critical items intact; idempotency; return on later day; no-resource escape; malformed checkpoints rejected.");
        return "PASS bandit model";
    }
    static Button B(string name)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==name);
    static async Task Wait(Func<bool> condition,string reason)
    {var until=DateTime.UtcNow.AddSeconds(20);while(!condition()){if(DateTime.UtcNow>until)throw new Exception("Timeout "+reason);await Task.Delay(40);}}
    static async Task Load(JourneyState state)
    {
        if(Hud!=null&&Hud.IsPaused){Hud.Resume();await Wait(()=>!Hud.IsPaused,"resume before load");}
        Check(SaveSystem.Write(SaveSystem.SlotPath,state,out var error)&&SaveSystem.QueueLoad(out error),error);
        var op=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>op.isDone&&Hud!=null&&Hud.IsExploring,"load");await Task.Delay(500);
    }
    static async Task ReloadAuto()
    {Check(SaveSystem.TryRead(SaveSystem.SlotPath,out var saved,out var error),error);await Load(saved);}
    static async Task Shot(string name)
    {
        await Task.Delay(250);Canvas.ForceUpdateCanvases();
        var panel=GameObject.Find("BanditEncounterPanel");
        if(panel!=null)foreach(var text in panel.GetComponentsInChildren<TMP_Text>())Check(!text.isTextOverflowing,"Overflow "+text.name);
        ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));await Task.Delay(300);
    }
    public static async Task<string> Run()
    {
        Model();var original=State==null?null:Copy(State);string oldPath=SaveSystem.TestSlotPath,real=SaveSystem.SlotPath;
        byte[] realBytes=File.Exists(real)?File.ReadAllBytes(real):null;
        SaveSystem.TestSlotPath=Path.Combine(Folder,"isolated.json");
        try
        {
            await Load(Fixture());Check(State.bandit?.stage==0&&SaveSystem.CanSave,"Live trigger/save unavailable");
            await Shot("01-choice");Hud.OpenMap();Hud.OpenActivity(ActivityPanel.Kind.Supplies);Hud.RequestDayEnd();
            Check(!Hud.IsPaused&&State.bandit.stage==0,"Navigation bypass");
            Hud.OpenPause();await Task.Delay(250);Hud.OpenSettings();await Task.Delay(300);
            Check(Hud.Settings.IsOpen,"Settings not open");Check(SaveSlots.Save(1,out var error),"Manual save "+error);
            Hud.ShowSaveCard();await Task.Delay(450);await Shot("02-settings-save");
            Hud.HandleEscape();await Task.Delay(350);if(Hud.Settings.IsOpen){Hud.Settings.Close();await Task.Delay(350);}Hud.Resume();await Wait(()=>!Hud.IsPaused,"resume");
            await ReloadAuto();Check(State.bandit.stage==0,"Choice checkpoint not restored");
            int minutes=State.minutes;var choose=B("Bandit_food");choose.onClick.Invoke();choose.onClick.Invoke();
            Check(State.bandit.stage==1&&State.Count("packaged_food")==1&&State.minutes==minutes+5,"Repeated input cost");
            await Shot("03-result");await ReloadAuto();Check(State.bandit.stage==1&&State.Count("packaged_food")==1,"Result reload cost");
            B("Bandit_Continue").onClick.Invoke();await Wait(()=>State.bandit.stage==2&&Hud.IsExploring&&GameObject.Find("BanditStage")==null,"finish result");await Task.Delay(500);
            B("ReturnFromRegion").onClick.Invoke();await Wait(()=>State.bandit.stage==3&&Hud.IsExploring,"return report");await Task.Delay(500);
            Check(State.location=="L3"&&!RegionExploration.Outside(State),"Returned to wrong camper");
            await Shot("04-return");await ReloadAuto();Check(State.bandit.stage==3,"Return reload");
            B("Bandit_Continue").onClick.Invoke();await Wait(()=>State.bandit.stage==4&&Hud.IsExploring&&GameObject.Find("BanditEncounterPanel")==null,"return finished");await Task.Delay(500);
            Check(CampLife.Memories(State).Any(m=>m.Value.Contains("골목 조우")),"Return absent from memory options");
            // Zero resources still permit leaving the encounter, including after loading.
            var poor=Fixture("L1");poor.items.Clear();poor.fuel=0;await Load(poor);
            Check(!B("Bandit_food").interactable&&!B("Bandit_water").interactable&&B("Bandit_detour").interactable,"Poor state trapped");
            await Shot("05-no-supplies");B("Bandit_detour").onClick.Invoke();await ReloadAuto();Check(State.bandit.outcome=="detour"&&State.minutes==20,"Detour persistence");
            // A failed checkpoint must expose retry; confirming again must not repeat the cost.
            await Load(Fixture());string workingPath=SaveSystem.TestSlotPath;
            string blocker=Path.Combine(Folder,"blocked-parent");File.WriteAllText(blocker,"test");SaveSystem.TestSlotPath=Path.Combine(blocker,"save.json");
            B("Bandit_water").onClick.Invoke();Check(State.bandit.stage==1&&State.Count("water")==1,"Failure lost in-memory result");
            Check(B("Bandit_Retry")!=null,"No save retry");await Shot("06-save-retry");SaveSystem.TestSlotPath=workingPath;
            B("Bandit_Retry").onClick.Invoke();await Task.Delay(450);await ReloadAuto();Check(State.bandit.stage==1&&State.Count("water")==1,"Retry repeated cost");
            // Trigger through real map travel from another parking location.
            var walking=Fixture("L3",false);walking.exploringPlace="L3";await Load(walking);Hud.OpenMap();Hud.Map.Select("L8");await Task.Delay(300);
            B("MapTravel").onClick.Invoke();await Task.Delay(350);B("MapTravel").onClick.Invoke();
            await Wait(()=>BanditEncounter.Started(State)&&State.bandit.stage==0&&Hud.IsExploring,"map arrival trigger");await Task.Delay(500);
            Check(State.location=="L3"&&State.exploringPlace=="L8"&&State.fuel==2,"Walk moved camper");await Shot("07-map-arrival");
            Log("PASS live: natural map arrival; choice/result/return reload; ESC settings and manual save; blocked activity bypass; repeat input; fixed costs; variable parking; zero-resource escape; save failure/retry; memory activity; no text overflow.");
        }
        catch(Exception e){Log("FAIL "+e);throw;}
        finally
        {
            try
            {
                SaveSystem.TestSlotPath=Path.Combine(Folder,"restore.json");
                if(original!=null)await Load(original);else{if(Hud!=null&&Hud.IsPaused){Hud.Resume();await Task.Delay(300);}var op=SceneManager.LoadSceneAsync(SceneNames.Title);await Wait(()=>op.isDone,"title restore");SaveSystem.Current=null;}
            }
            finally{SaveSystem.TestSlotPath=oldPath;Check(realBytes==null?!File.Exists(real):File.Exists(real)&&realBytes.SequenceEqual(File.ReadAllBytes(real)),"Real save modified");Log("RESTORED original session; real autosave unchanged.");}
        }
        return "PASS bandit encounter runtime verification";
    }
}


// Bundled with the existing verification classes by tools/build-revalidation.ps1.
public static class RevalidateFirstPass
{
    static string Folder=>Path.GetFullPath(Application.dataPath+"/../Screenshots/Revalidation-2026-09-13");
    static GameHud Hud=>GameHud.Instance;
    static JourneyState S=>SaveSystem.Current;
    static List<string> failures=new List<string>(),runtimeErrors=new List<string>();
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Log(string m)=>File.AppendAllText(Folder+"/results.txt",DateTime.UtcNow.ToString("s")+" "+m+"\n");
    static string Hash(string p){if(!File.Exists(p))return "absent";using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p)));}
    static async Task Wait(Func<bool> f){var end=DateTime.UtcNow.AddSeconds(20);while(!f()){if(DateTime.UtcNow>end)throw new Exception("UI timeout");await Task.Delay(50);}}
    static Button B(string id)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==id);
    static async Task Click(string id){Check(B(id).interactable,"Disabled "+id);B(id).onClick.Invoke();await Task.Delay(250);}
    static async Task Load(JourneyState s){Check(SaveSystem.Write(SaveSystem.SlotPath,s,out var e)&&SaveSystem.QueueLoad(out e),e);var op=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>op.isDone&&Hud!=null&&Hud.IsExploring);await Task.Delay(400);}
    static async Task Title(){var op=SceneManager.LoadSceneAsync(SceneNames.Title);await Wait(()=>op.isDone);SaveSystem.Current=null;}
    static async Task Shot(string n){Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Folder+"/"+n+".png");await Task.Delay(250);}
    static void Fit(Transform r){Canvas.ForceUpdateCanvases();foreach(var t in r.GetComponentsInChildren<TMP_Text>())Check(!t.isTextOverflowing,"Overflow "+t.name+": "+t.text);}
    static async Task Suite(string name,Func<Task<string>> run)
    {
        Log("START "+name);
        try{Log("PASS "+name+" — "+await run());}
        catch(Exception e){failures.Add(name+": "+e.Message);Log("FAIL "+name+" — "+e);}
    }
    static void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)runtimeErrors.Add(type+": "+message+"\n"+stack);}
    static async Task<string> SaveAndArt()
    {
        var old=SaveSystem.TestSlotPath;SaveSystem.TestSlotPath=Folder+"/save-ui/"+Guid.NewGuid().ToString("N")+"/auto.json";
        try
        {
            var natural=JsonUtility.FromJson<JourneyState>(File.ReadAllText(Path.GetFullPath(Application.dataPath+"/../Screenshots/FirstPassJourney/completed-state.json")));
            await Load(natural);Hud.HandleEscape();await Task.Delay(250);await Click("Button_설정");Hud.Settings.RequestSaveLoad();await Task.Delay(250);await Click("SaveMode");await Click("SaveSlot_1");
            string autoHash=Hash(SaveSystem.SlotPath);await Click("SlotPrimaryAction");Check(SaveSlots.Read(1).Readable&&Hash(SaveSystem.SlotPath)==autoHash,"Manual/automatic separation");
            string first=Hash(SaveSlots.PathFor(1));await Click("SlotPrimaryAction");Check(Hash(SaveSlots.PathFor(1))==first,"Overwrite before confirmation");await Click("CancelSlotAction");Check(Hash(SaveSlots.PathFor(1))==first,"Cancel changed save");await Click("SlotPrimaryAction");await Click("ConfirmSlotAction");Check(Hash(SaveSlots.PathFor(1)+".bak")==first,"Backup missing");
            await Click("SaveSlot_0");Check(!B("SlotPrimaryAction").interactable,"Manual overwrite of autosave enabled");
            await Click("LoadMode");await Click("SaveSlot_1");await Click("SlotPrimaryAction");await Click("CancelSlotAction");Check(S.regionId=="region02"&&S.Count("camera")==1,"Load cancel changes state");await Click("SlotPrimaryAction");await Click("ConfirmSlotAction");await Wait(()=>Hud!=null&&Hud.IsExploring&&!Hud.IsPaused);await Task.Delay(500);Check(JourneyTutorial.Completed(S).All(x=>x)&&S.bandit.stage==4&&S.Count("first_travel_photo")==1,"Loaded milestones lost");
            string slot=SaveSlots.PathFor(1);File.WriteAllText(slot,"deliberately invalid isolated verification file");Check(SaveSlots.Read(1).Recoverable,"Backup recovery not offered");Hud.OpenSaveLoad(false);await Task.Delay(300);await Click("SaveSlot_1");Fit(Hud.SaveLoad.transform);await Shot("save-backup-recovery");await Click("SlotPrimaryAction");await Click("ConfirmSlotAction");await Wait(()=>Hud!=null&&Hud.IsExploring&&!Hud.IsPaused);Check(File.ReadAllText(slot).StartsWith("deliberately invalid"),"Recovery overwrites damaged original");
            var before=S;Check(!SaveSystem.QueueLoad(slot,out _)&&ReferenceEquals(S,before)&&SaveSystem.Pending==null,"Invalid save changes live state");
            foreach(string id in new[]{"L8","L9","L10"})
            {
                var s=JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(natural));s.regionId="region01";s.location="L1";s.exploringPlace=id;s.inStore=false;await Load(s);
                var stage=GameObject.Find("JourneyStage").GetComponent<Image>();Check(stage.sprite.name=="region-"+id,"Missing/wrong stage "+id);Check(Hud.InteractionRoot.Find("Location").GetComponentInChildren<HudIcon>().Symbol==HudIcon.Kind.Map,"Outside location icon");Fit(Hud.InteractionRoot);await Shot(id+"-stage");
                Hud.OpenSaveLoad(false);await Task.Delay(250);await Click("SaveSlot_0");Check(GameObject.Find("SaveIllustration").GetComponent<Image>().sprite==stage.sprite,"Save thumbnail "+id);Fit(Hud.SaveLoad.transform);await Title();
            }
            return "settings manual save/load, overwrite/load cancellation, checksum rejection, backup recovery, six milestones and 3 stage/thumbnail identities";
        }
        finally{await Title();SaveSystem.TestSlotPath=old;}
    }
    public static async Task<string> Run()
    {
        Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/results.txt","");failures.Clear();runtimeErrors.Clear();
        Check(SceneManager.GetActiveScene().name==SceneNames.Title&&S==null&&string.IsNullOrEmpty(SaveSystem.TestSlotPath),"Start from fresh Title play mode");
        var paths=Enumerable.Range(0,4).Select(SaveSlots.PathFor).SelectMany(p=>new[]{p,p+".bak"}).ToArray();var hashes=paths.ToDictionary(p=>p,Hash);
        Application.logMessageReceived+=OnLog;
        try
        {
            await Suite("first-week model",()=>Task.FromResult(VerifyFirstWeek.Model()));
            await Suite("region travel model",()=>Task.FromResult(VerifyRegionTravel.Model()));
            await Suite("camp life model",()=>Task.FromResult(VerifyCampLife.Model()));
            await Suite("tutorial model + UI",VerifyTutorialStructure.Run);
            await Suite("search model + UI",VerifySearchRuntime.Run);
            await Suite("kitchen runtime",VerifyKitchenPresentation.Run);
            await Suite("bandit model + UI",VerifyBanditEncounter.Run);
            await Suite("save recovery + art",SaveAndArt);
        }
        finally
        {
            await Title();SaveSystem.TestSlotPath=null;Application.logMessageReceived-=OnLog;
            foreach(var p in paths)if(Hash(p)!=hashes[p])failures.Add("Real save changed: "+p);
            File.WriteAllText(Folder+"/runtime-errors.txt",string.Join("\n\n",runtimeErrors));
            if(runtimeErrors.Count>0)failures.Add(runtimeErrors.Count+" runtime error logs");
            Log("RESTORED Title, slot override cleared; checked automatic/manual slots and backups");
            Log(failures.Count==0?"COMPLETE PASS — 8 suites, no runtime error logs, all 8 real slot/backup hashes unchanged":"COMPLETE FAIL — "+string.Join("; ",failures));
        }
        return failures.Count==0?"PASS 8 revalidation suites":string.Join("; ",failures);
    }
}
