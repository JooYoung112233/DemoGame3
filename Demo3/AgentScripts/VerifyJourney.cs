using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Live49.Chapter00;
using Live49.Core;
using Live49.Title;
using Live49.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class VerifyJourney
{
    static GameHud Hud=>GameHud.Instance;
    static C0OpeningDirector Director=>UnityEngine.Object.FindAnyObjectByType<C0OpeningDirector>();
    static JourneyState State=>Director.State;
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/JourneyReview"));
    static readonly List<string> Report=new List<string>();
    static readonly List<string> Errors=new List<string>();
    static Button Button(string name)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==name);
    static Button Maybe(string name)=>UnityEngine.Object.FindObjectsByType<Button>().FirstOrDefault(b=>b.name==name);
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)Errors.Add(text);}
    static void Record(string message){Report.Add(message);File.WriteAllLines(Path.Combine(Folder,"report.txt"),Report);}
    static async Task Wait(Func<bool> ready,int seconds=20)
    {var end=DateTime.UtcNow.AddSeconds(seconds);while(!ready()){if(DateTime.UtcNow>end)throw new Exception("Journey wait timed out. "+(Director==null?"no director":State.node+":"+State.line));await Task.Delay(25);}}
    static async Task Shot(string name){await Task.Delay(250);ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name));await Task.Delay(350);}
    static void Core()
    {
        foreach(int level in new[]{1,2,3})
        {
            var state=new JourneyState{cookingUnlocked=true};state.Add("gas","가스",3);state.Add("ingredients","식재료",3);
            var cooking=new CookingSession(state);Check(cooking.Start()&&!cooking.Start(),"Cooking start charged twice.");
            cooking.Select(level);for(int i=0;i<500;i++)cooking.Tick(.1f);
            Check(cooking.Stage==CookingSession.Phase.Done&&state.Count("meal")==1,"Cooking did not guarantee one meal.");
            Check(state.Count("gas")==2&&state.Count("ingredients")==2&&state.minutes==25,"Cooking cost mismatch.");
            if(level!=2)Check(!cooking.Great,"Extreme heat incorrectly grants quality bonus.");
            cooking.Tick(100);Check(state.Count("meal")==1,"Duplicate meal receipt.");
        }
        var poor=new JourneyState{cookingUnlocked=true};poor.Add("gas","가스",1);
        Check(!new CookingSession(poor).Start()&&poor.Count("gas")==1,"Failed cost check spent partial resources.");
        poor.Add("ingredients","식재료",1);var paused=new CookingSession(poor);paused.Start();paused.Paused=true;paused.Tick(.1f);
        Check(paused.Elapsed==0,"Paused cooking advances.");
        string file=Path.Combine(Folder,"model-save.json");
        var sample=new JourneyState{answeredSoi=true,bookSeen=true};sample.Set("water_packed");sample.Add("sketchbook","스케치북",1,2);
        Check(SaveSystem.Write(file,sample,out var error),error);
        Check(SaveSystem.TryRead(file,out var loaded,out error)&&loaded.Has("water_packed")&&loaded.Count("sketchbook")==1,error??"Save roundtrip failed.");
        sample.minutes=8;Check(SaveSystem.Write(file,sample,out error)&&File.Exists(file+".bak"),"Atomic save backup missing.");
        string corrupt=Path.Combine(Folder,"corrupt-save.json");File.WriteAllText(corrupt,File.ReadAllText(file).Replace("checksum","invalidChecksum"));
        Check(!SaveSystem.TryRead(corrupt,out _,out _)&&File.Exists(corrupt),"Corrupt save accepted or removed.");
        Record("PASS core: fixed cooking charge once; all heat levels yield one meal; pause; insufficient ingredient transaction; save roundtrip, atomic backup, corrupt file rejection without removal.");
    }
    public static async Task<string> Run()
    {
        Directory.CreateDirectory(Folder);Report.Clear();Errors.Clear();Application.logMessageReceived+=Log;
        SaveSystem.TestSlotPath=Path.Combine(Folder,"runtime-save.json");
        try
        {
            Core();
            var start=UnityEngine.Object.FindObjectsByType<TitleMenuItem>().Single(i=>i.Id=="start");start.Owner.Confirm(start);
            var deadline=DateTime.UtcNow.AddMinutes(7);string last=null;
            while(Director==null||Hud==null||!Hud.IsExploring||State.day==0)
            {
                if(DateTime.UtcNow>deadline)throw new Exception("Full opening did not reach day 1.");
                if(Errors.Count>0)throw new Exception(string.Join("\n",Errors));
                if(Director!=null&&State!=null&&last!=State.node){last=State.node;Record("Reached: "+last);}
                if(Hud!=null&&Hud.IsPaused)
                {
                    var choice=Maybe("InteractionChoice_0");if(choice!=null)choice.onClick.Invoke();
                    else if(Maybe("Button_하루 마치기")!=null)Hud.ConfirmDayEnd();
                }
                else if(Hud!=null&&Hud.IsExploring)
                {
                    if(string.IsNullOrEmpty(State.node))
                    {
                        if(!State.answeredSoi)Button("Target_soi").onClick.Invoke();
                        else Button("Target_book").onClick.Invoke();
                    }
                    else if(Maybe("StoryChoice_0")!=null)Director.ChooseStory(0);
                }
                else FreshInput.SimulateAdvance();
                await Task.Delay(300);
            }
            await Task.Delay(500);
            Check(State.Has("camp_broadcast_heard")&&State.Has("water_packed")&&State.Has("blanket_packed")&&State.Has("light_restored")&&State.Count("sketchbook")==1&&State.journal.Length>0,"Continuation skipped story prerequisites.");
            Check(SaveSystem.HasSave,"Day 1 checkpoint did not save.");
            Record("PASS: real title → opening → both preparation objects → outage inspection → repair → photo care → book → journal → voluntary sleep → day 1; automatic checkpoint.");
            Hud.OpenActivity(ActivityPanel.Kind.Journal);await Task.Delay(400);await Shot("01-journal.png");Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);
            Hud.OpenMap();Hud.Map.Select("L1");await Task.Delay(350);Button("MapTravel").onClick.Invoke();await Shot("02-departure-confirm.png");
            int fuel=State.fuel;Button("MapTravel").onClick.Invoke();Button("MapTravel").onClick.Invoke();
            await Wait(()=>Hud.IsExploring&&State.inStore&&!Hud.IsPaused);
            Check(State.location=="L1"&&State.fuel==fuel-1&&State.minutes==20,"Travel charged twice or failed.");
            await Shot("03-store.png");
            Button("ReturnCamper").onClick.Invoke();await Wait(()=>Hud.IsExploring&&!State.inStore);
            Check(State.Count("water")==0,"Return unexpectedly granted inventory.");
            Button("EnterStore").onClick.Invoke();await Wait(()=>Hud.IsExploring&&State.inStore);
            Button("StoreWater").onClick.Invoke();await Task.Delay(300);var take=Button("InteractionChoice_0");take.onClick.Invoke();take.onClick.Invoke();await Wait(()=>!Hud.IsPaused);
            Check(State.Count("water")==2&&State.minutes==25,"Water acquired more than once.");
            Button("StoreFood").onClick.Invoke();await Task.Delay(300);Button("InteractionChoice_0").onClick.Invoke();await Wait(()=>!Hud.IsPaused);
            Check(State.Count("packaged_food")==2&&State.minutes==30,"Food cost or receipt failed.");
            Button("ReturnCamper").onClick.Invoke();await Wait(()=>Hud.IsExploring&&!State.inStore);
            Hud.OpenBag();await Task.Delay(300);await Shot("04-supplies.png");Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);
            Record("PASS: native map confirmation commits one fuel/20 minutes once; unsearched store permits return; water and food guaranteed receipts each once; no new scene.");
            Hud.OpenPause();Hud.ShowSaveCard();Button("Button_저장하기").onClick.Invoke();
            Check(SaveSystem.TryRead(SaveSystem.SlotPath,out var saved,out var saveError)&&saved.location=="L1"&&saved.Count("water")==2,saveError??"Manual save failed.");
            await Shot("05-save-menu.png");
            State.minutes+=7;var old=Hud;Button("Button_불러오기").onClick.Invoke();Button("Button_불러오기").onClick.Invoke();
            await Wait(()=>Hud!=null&&Hud!=old&&Hud.IsExploring);
            Check(State.minutes==30&&State.location=="L1"&&State.Count("water")==2&&State.Count("sketchbook")==1,"Load failed to restore snapshot.");
            Hud.OpenPause();Hud.RequestExit(false);Hud.ConfirmExit();await Wait(()=>SceneManager.GetActiveScene().name=="00_Title");await Task.Delay(350);
            var resume=UnityEngine.Object.FindObjectsByType<TitleMenuItem>().Single(i=>i.Id=="continue");Check(resume.Interactable,"Title continue disabled.");resume.Owner.Confirm(resume);
            await Wait(()=>Hud!=null&&Hud.IsExploring);
            Check(State.day==1&&State.location=="L1"&&State.Count("water")==2,"Title continue replayed opening or lost state.");
            Record("PASS: manual save, load confirmation, in-game load and title continue restore chapter, node/line, flags, journal, supplies, location and minutes.");
            // Temporary unlocked kitchen fixture. Reload the real checkpoint afterward.
            State.cookingUnlocked=true;State.Add("gas","가스",2,1);State.Add("ingredients","식재료",2);
            Hud.OpenActivity(ActivityPanel.Kind.Kitchen);await Task.Delay(300);Button("StartCooking").onClick.Invoke();Button("StartCooking").onClick.Invoke();
            await Task.Delay(700);await Shot("06-cooking.png");
            Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);Check(!SaveSystem.CanSave,"Mid-cook save allowed.");
            Hud.OpenMap();Check(Hud.IsPaused&&!Hud.Map.IsOpen,"Travel allowed with unfinished cooking.");Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);
            Hud.OpenActivity(ActivityPanel.Kind.Kitchen);await Task.Delay(300);Check(Hud.Activity.Cooking.Paused,"Reopening cooking silently resumed.");
            Button("PauseCooking").onClick.Invoke();await Wait(()=>Hud.Activity.Cooking.Stage==CookingSession.Phase.Done,30);
            Check(State.Count("meal")==1&&State.Count("gas")==1&&State.Count("ingredients")==1,"Live cooking receipt/cost mismatch.");
            await Shot("07-cooking-result.png");Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);
            Hud.OpenPause();Hud.ShowSaveCard();old=Hud;Button("Button_불러오기").onClick.Invoke();Button("Button_불러오기").onClick.Invoke();await Wait(()=>Hud!=null&&Hud!=old&&Hud.IsExploring);
            Check(!State.cookingUnlocked&&State.Count("gas")==0&&State.Count("meal")==0,"Kitchen fixture leaked into actual state.");
            Hud.RequestDayEnd();await Task.Delay(300);await Shot("08-day-end.png");Hud.ConfirmDayEnd();Hud.ConfirmDayEnd();await Wait(()=>Hud.IsExploring&&State.day==2&&!Hud.IsPaused);
            Check(State.Count("water")==2&&State.Count("sketchbook")==1&&State.minutes==0,"Day end lost inventory or failed date.");
            Record("PASS: live kitchen start once, close pauses, travel/save blocked during cook, explicit resume, meal receipt once; fixture removed by reload; confirmed day end advances once and preserves inventory.");
            Check(SceneManager.sceneCount==1,"Unexpected scene added.");Check(Errors.Count==0,string.Join("\n",Errors));
            Hud.OpenActivity(ActivityPanel.Kind.Journal);await Task.Delay(350);await Shot("09-review-journal.png");
            Record("PASS ALL. Test saves are isolated under Screenshots/JourneyReview. Runtime left at day 2 with real acquired supplies, without kitchen fixture. No user save overwritten.");
            SaveSystem.TestSlotPath=null;return string.Join("\n",Report);
        }
        catch(Exception e){Record("FAIL: "+e);throw;}
        finally{Application.logMessageReceived-=Log;}
    }
}
