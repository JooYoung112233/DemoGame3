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

public static class VerifyBanditEncounter
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/BanditEncounter"));
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
