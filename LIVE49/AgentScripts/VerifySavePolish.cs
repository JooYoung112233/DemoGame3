using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Live49.Core;
using Live49.UI;
using Live49.Title;
using Live49.Chapter00;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public static class VerifySavePolish
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/SavePolish"));
    static GameHud Hud=>GameHud.Instance;
    static JourneyState State=>SaveSystem.Current;
    static Button B(string name)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==name);
    static void Check(bool okay,string message){if(!okay)throw new Exception(message);}
    static void Log(string message){File.AppendAllText(Path.Combine(Folder,"progress.txt"),message+"\n");}
    static async Task Wait(Func<bool> ready){var end=DateTime.UtcNow.AddSeconds(25);while(!ready()){if(DateTime.UtcNow>end)throw new Exception("Timed out");await Task.Delay(40);}}
    static async Task Click(string name){B(name).onClick.Invoke();await Task.Delay(250);}
    static async Task Escape(){Hud.HandleEscape();await Task.Delay(300);}
    static void Fits(Transform root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<TMP_Text>())Check(!t.isTextOverflowing,"Text overflow: "+t.name);}
    static async Task Shot(string name){Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));await Task.Delay(350);}
    static JourneyState Saved(){Check(SaveSystem.TryRead(SaveSystem.SlotPath,out var s,out var error),error);return s;}
    static async Task Load(JourneyState s)
    {
        for(int i=0;Hud!=null&&Hud.IsPaused&&i<6;i++)await Escape();
        Check(SaveSystem.Write(SaveSystem.SlotPath,s,out var error)&&SaveSystem.QueueLoad(out error),error);
        var op=SceneManager.LoadSceneAsync(SceneNames.Game);
        await Wait(()=>op.isDone&&Hud!=null&&Hud.IsExploring);await Task.Delay(250);
    }
    static async Task Settings()
    {
        Hud.OpenPause();await Task.Delay(250);await Click("Button_설정");
        Check(Hud.IsPaused&&Hud.Settings.IsOpen,"Settings did not retain pause");Fits(Hud.Settings.transform);
    }
    static async Task Slots(){await Settings();await Click("Button_저장·불러오기");Check(Hud.SaveLoad.IsOpen&&!Hud.Settings.IsOpen,"Settings save entry failed");}
    public static async Task<string> Run()
    {
        File.WriteAllText(Path.Combine(Folder,"progress.txt"),"");
        string real=File.ReadAllText(Path.Combine(Folder,"real-path.txt"));
        string originalBytes=File.ReadAllText(Path.Combine(Folder,"real-before.txt"));
        var original=JsonUtility.FromJson<JourneyState>(File.ReadAllText(Path.Combine(Folder,"live-before.json")));
        var old=SaveSystem.TestSlotPath;
        string isolated=Path.Combine(Folder,"run-"+Guid.NewGuid().ToString("N"),"auto.json");
        SaveSystem.TestSlotPath=isolated;
        try
        {
            await Load(new JourneyState{day=2,location="L1",fuel=3,answeredSoi=true,bookSeen=true});
            Check(!Hud.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="OpenSave"||t.name=="OpenLoad"),"Save/load remains on HUD");
            await Shot("01-clean-hud");
            await Settings();await Shot("02-settings-save-entry");await Click("Button_저장·불러오기");
            Fits(Hud.SaveLoad.transform);await Click("SaveSlot_1");await Click("SlotPrimaryAction");
            Check(SaveSlots.Read(1).Readable,"Manual slot failed");string manual=File.ReadAllText(SaveSlots.PathFor(1));
            await Escape();Check(Hud.Settings.IsOpen&&Hud.IsPaused,"Slots must return to settings");
            await Escape();Check(!Hud.Settings.IsOpen&&Hud.IsPaused,"Settings must return to pause menu");
            await Shot("03-pause-menu");await Escape();Check(!Hud.IsPaused&&Time.timeScale==1,"Pause must return to gameplay");
            string same=File.ReadAllText(isolated);Check(SaveSystem.AutoSave(State,out _),"Unchanged save failed");Check(File.ReadAllText(isolated)==same,"Unchanged checkpoint rewrites timestamp/backup");
            // Play real callbacks: leave vehicle, acquire supplies, return, share a meal and end day.
            await Click("LeaveCamper");await Wait(()=>Hud.IsExploring&&!Hud.IsPaused&&State.inStore);await Task.Delay(100);
            Check(Saved().inStore,"Arrival not saved");
            await Click("StoreWater");await Click("InteractionChoice_0");Check(Saved().Count("water")==2&&Saved().Has("water_checked"),"Water acquisition not saved");
            await Click("StoreFood");await Click("InteractionChoice_0");Check(Saved().Count("packaged_food")==2&&RegionExploration.Discovered(Saved(),"L2")&&RegionExploration.Discovered(Saved(),"L3"),"Food/unlocks not saved");
            if(Hud.IsPaused)await Escape();
            await Click("ReturnCamper");await Wait(()=>Hud.IsExploring&&!State.inStore);await Task.Delay(150);Check(!Saved().inStore,"Vehicle return not saved");
            await Shot("04-autosave-cue");
            await Click("OpenCampLife");await Click("LifeConfirm");Check(Saved().Has("first_meal_shared")&&Saved().Count("water")==1,"Meal transaction not saved");
            await Escape();await Wait(()=>!Hud.IsPaused);
            Hud.RequestDayEnd();await Task.Delay(250);Hud.ConfirmDayEnd();await Wait(()=>State.day==3&&Hud.IsExploring&&!Hud.IsPaused);await Task.Delay(100);
            Check(Saved().day==3&&Saved().minutes==0,"Next day not saved");Check(File.ReadAllText(SaveSlots.PathFor(1))==manual,"Gameplay autosave overwrote manual slot");
            // Cancel load and then restore the original manual snapshot through the settings UI.
            await Slots();await Click("LoadMode");await Click("SaveSlot_1");await Click("SlotPrimaryAction");await Click("CancelSlotAction");Check(State.day==3,"Load cancel changed progress");
            await Click("SlotPrimaryAction");await Click("ConfirmSlotAction");await Wait(()=>Hud!=null&&Hud.IsExploring&&!Hud.IsPaused);Check(State.day==2&&!State.Has("water_checked"),"Manual snapshot not restored");
            // Exit saves the current run, so Continue follows that exit checkpoint instead of older manual slots.
            State.minutes=71;
            Hud.OpenPause();await Task.Delay(250);Hud.RequestExit(false);await Task.Delay(100);Hud.ConfirmExit();
            await Wait(()=>SceneManager.GetActiveScene().name==SceneNames.Title&&UnityEngine.Object.FindAnyObjectByType<TitleController>()!=null);
            Check(Saved().minutes==71&&SaveSlots.Latest().Index==0,"Exit checkpoint not saved/latest");
            var title=UnityEngine.Object.FindAnyObjectByType<TitleController>();
            title.Confirm(UnityEngine.Object.FindObjectsByType<TitleMenuItem>().Single(i=>i.Id=="continue"));
            await Wait(()=>Hud!=null&&Hud.IsExploring&&!SceneFlow.OpeningHandoffPending);Check(State.minutes==71,"Continue lost exit checkpoint");
            // A filesystem failure must keep the player in game with retry/back actions.
            string blocked=Path.Combine(Path.GetDirectoryName(isolated),"blocked-parent");File.WriteAllText(blocked,"test fixture");
            SaveSystem.TestSlotPath=Path.Combine(blocked,"auto.json");State.minutes++;
            Hud.OpenPause();await Task.Delay(250);Hud.RequestExit(false);Hud.ConfirmExit();await Task.Delay(200);
            Check(Hud!=null&&Hud.IsPaused&&SaveSystem.AutoSaveError!=null&&B("Button_다시 시도")!=null,"Failed autosave exited or lacked retry");
            SaveSystem.TestSlotPath=isolated;await Escape();await Escape();
            Check(SaveSystem.AutoSave(State,out _),"Save recovery failed");
            // A completed prologue choice is also a real resumable checkpoint.
            await Load(new JourneyState{answeredSoi=true});
            var director=UnityEngine.Object.FindAnyObjectByType<C0OpeningDirector>();
            var routine=(System.Collections.IEnumerator)typeof(C0OpeningDirector).GetMethod("EnterExploration",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(director,new object[]{true});
            State.bookSeen=false;State.Set("checkpoint_probe");director.StartCoroutine(routine);
            await Wait(()=>Saved().Has("checkpoint_probe"));Check(Saved().answeredSoi,"Opening interaction not saved");
            Log("PASS: HUD buttons removed; settings-only entry; nested Escape/pause; manual save/load/cancel; unchanged checkpoints; actual arrival/supplies/unlocks/meal/day autosaves; exit save and Continue; write-failure retry; prologue exploration checkpoint.");
        }
        catch(Exception e){Log("FAIL: "+e);throw;}
        finally
        {
            try{SaveSystem.TestSlotPath=isolated;await Load(original);await Shot("05-restored-gameplay");}
            finally{SaveSystem.TestSlotPath=old;}
            Check((File.Exists(real)?File.ReadAllText(real):"")==originalBytes,"Existing save file changed");
            Log("RESTORED captured live progress; existing player save file unchanged.");
        }
        return "PASS gameplay save polish; isolated verification only.";
    }
}
