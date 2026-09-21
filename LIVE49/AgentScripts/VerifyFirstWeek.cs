using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Live49.Core;
using Live49.Chapter00;
using Live49.Dialogue;
using Live49.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public static class VerifyFirstWeek
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/FirstWeek"));
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
        Check(SaveSystem.TryRead(Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/CityMapReview/checkpoint.json")),out var original,out var error),error);
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
        Check(SaveSystem.TryRead(Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/CityMapReview/checkpoint.json")),out var original,out var error),error);
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
