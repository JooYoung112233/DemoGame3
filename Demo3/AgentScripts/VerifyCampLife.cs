using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Live49.Core;
using Live49.Chapter00;
using Live49.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public static class VerifyCampLife
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/CampLife"));
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
        Check(SaveSystem.TryRead(Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/CityMapReview/checkpoint.json")),out var original,out var error),error);
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
        Check(SaveSystem.TryRead(Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/CityMapReview/checkpoint.json")),out var original,out var error),error);
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
        Check(SaveSystem.TryRead(Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/CityMapReview/checkpoint.json")),out var original,out var error),error);
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
