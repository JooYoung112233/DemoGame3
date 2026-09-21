using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Live49.Core;
using Live49.UI;
using Live49.Chapter00;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public static class VerifyRegionExploration
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/RegionExploration"));
    static GameHud Hud=>GameHud.Instance;
    static JourneyState State=>SaveSystem.Current;
    static Button B(string name)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==name);
    static void Check(bool value,string error){if(!value)throw new Exception(error);}
    static void Progress(string message){Directory.CreateDirectory(Folder);File.AppendAllText(Path.Combine(Folder,"progress.txt"),message+"\n");}
    static async Task Wait(Func<bool> ready)
    {var end=DateTime.UtcNow.AddSeconds(15);while(!ready()){if(DateTime.UtcNow>end)throw new Exception("Wait timed out.");await Task.Delay(25);}}
    static async Task Shot(string name){await Task.Delay(350);Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name));await Task.Delay(300);}
    static void Fits(Transform root)
    {Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<TMP_Text>())Check(!t.isTextOverflowing,"Text overflow: "+t.text);}
    static async Task Load(JourneyState state)
    {
        for(int i=0;Hud!=null&&Hud.IsPaused&&i<5;i++){Hud.HandleEscape();await Task.Delay(350);}
        Check(SaveSystem.Write(SaveSystem.SlotPath,state,out var error),error);
        Check(SaveSystem.QueueLoad(out error),error);var load=SceneManager.LoadSceneAsync(SceneNames.Game);
        await Wait(()=>load.isDone&&Hud!=null&&Hud.IsExploring);await Task.Delay(350);
    }
    public static string Model()
    {
        var state=new JourneyState{day=1,location="L1"};
        Check(RegionExploration.Sites.Length==10&&RegionExploration.Sites.Select(s=>s.Id).Distinct().Count()==10,"Ten unique places required.");
        Check(RegionExploration.Visible(state).SequenceEqual(new[]{"L1"}),"Initial map leaks destinations.");
        state.exploringPlace="L2";Check(!RegionExploration.Complete(state,"L2",out _)&&!state.Valid(),"Locked destination can complete/save.");state.exploringPlace="";
        state.Set("water_checked");Check(RegionExploration.Visible(state).Length==1,"First supply prematurely unlocks.");
        state.Set("food_checked");Check(RegionExploration.Visible(state).Length==3,"Store should reveal two.");
        foreach(var id in new[]{"L2","L3","L4","L8","L5","L9","L6","L10","L7"})
        {
            state.exploringPlace=id;Check(RegionExploration.Discovered(state,id),"Unreachable: "+id);
            int minutes=state.minutes;Check(RegionExploration.Complete(state,id,out var reveal),"Complete failed: "+id);
            if(id=="L2"||id=="L3"||id=="L4")Check(reveal.Length==2,"Branch should reveal two: "+id);
            if(id=="L6")Check(!RegionExploration.Discovered(state,"L7"),"Gate bypassed dual prerequisite.");
            Check(!RegionExploration.Complete(state,id,out _)&&state.minutes==minutes+10,"Duplicate completion charged time.");
            Check(state.Valid(),"State invalid: "+id);
            state=JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(state));
        }
        Check(RegionExploration.CompletedCount(state)==10,"All ten must be completable.");
        var alternate=new JourneyState{day=1,location="L1"};alternate.Set("water_checked");alternate.Set("food_checked");
        foreach(var id in new[]{"L3","L5"}){alternate.exploringPlace=id;Check(RegionExploration.Complete(alternate,id,out _),"Alternative branch broken.");}
        Check(RegionExploration.Discovered(alternate,"L6")&&RegionExploration.Discovered(alternate,"L10"),"House branch does not converge.");
        return "PASS model: ten unique sites; initial 1; store opens 2; branches open 2; alternative convergence; final AND gate; no duplicate completion; JSON persistence.";
    }
    public static async Task<string> Run()
    {
        Progress(Model());
        var original=JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(State));var originalPath=SaveSystem.TestSlotPath;
        SaveSystem.TestSlotPath=Path.Combine(Folder,"isolated-save.json");
        try
        {
            // A legacy checkpoint with both supply flags must immediately expose its next two sites.
            var fresh=JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(original));
            fresh.location="L1";fresh.inStore=true;fresh.exploringPlace="";
            fresh.flags.RemoveAll(f=>f.key.StartsWith(RegionExploration.RegionId+".")||f.key=="water_checked"||f.key=="food_checked");
            await Load(fresh);Hud.OpenMap();await Task.Delay(300);
            Check(Hud.Map.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("MapPlace_"))==1,"Fresh runtime exposes locked pins.");
            Fits(Hud.Map.transform);await Shot("01-first-place.png");Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);
            foreach(var button in new[]{"StoreWater","StoreFood"})
            {B(button).onClick.Invoke();await Task.Delay(250);B("InteractionChoice_0").onClick.Invoke();await Task.Delay(400);}
            Check(RegionExploration.Visible(State).Length==3,"Real supplies did not open next two.");
            Fits(Hud.transform.Find("HUDViewport/PauseOverlay"));await Shot("02-first-two-revealed.png");
            B("InteractionChoice_0").onClick.Invoke();await Wait(()=>Hud.Map.IsOpen);await Task.Delay(300);await Shot("03-first-branch-map.png");
            int fuel=State.fuel;int scenes=SceneManager.sceneCount;
            foreach(var id in new[]{"L2","L3","L4","L8","L5","L9","L6","L10","L7"})
            {
                Hud.Map.Select(id);await Task.Delay(100);Check(Hud.Map.SelectedId==id,"Cannot select "+id);
                B("MapTravel").onClick.Invoke();Fits(Hud.Map.transform);B("MapTravel").onClick.Invoke();
                await Wait(()=>Hud.IsExploring&&!Hud.IsPaused&&State.exploringPlace==id);
                await Task.Delay(250);Check(!RegionExploration.Completed(State,id),"Arrival completes exploration prematurely.");
                if(id=="L2")
                {
                    B("SurveyRegion").onClick.Invoke();await Task.Delay(250);Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);
                    Check(!RegionExploration.Completed(State,id),"Cancel still completes exploration.");
                    await Shot("04-gas-station.png");
                }
                if(id=="L8")await Shot("05-parking-overview.png");
                B("SurveyRegion").onClick.Invoke();await Task.Delay(250);Fits(Hud.transform.Find("HUDViewport/PauseOverlay"));
                B("InteractionChoice_0").onClick.Invoke();await Task.Delay(400);
                Check(RegionExploration.Completed(State,id),"Survey did not complete "+id);
                Check(SaveSystem.TryRead(SaveSystem.SlotPath,out var saved,out _)&&RegionExploration.Completed(saved,id)&&saved.exploringPlace==id,"Auto-save lost exploration "+id);
                Fits(Hud.transform.Find("HUDViewport/PauseOverlay"));
                if(id=="L10")await Shot("06-final-path-unlocked.png");
                B("InteractionChoice_0").onClick.Invoke();await Wait(()=>Hud.Map.IsOpen);await Task.Delay(250);
                Fits(Hud.Map.transform);Progress("PASS live "+id+" complete; visible "+RegionExploration.Visible(State).Length);
            }
            Check(State.fuel==fuel,"Local walking consumes camper fuel.");Check(SceneManager.sceneCount==scenes,"Exploration adds scenes.");
            Check(RegionExploration.CompletedCount(State)==10,"Live tour incomplete.");await Shot("07-ten-places.png");
            Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);B("ReturnFromRegion").onClick.Invoke();await Wait(()=>Hud.IsExploring&&!Hud.IsPaused&&string.IsNullOrEmpty(State.exploringPlace));
            Check(State.location=="L1"&&!State.inStore,"Camper return lost parked location.");
            await Load(JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(State)));
            Check(RegionExploration.CompletedCount(State)==10,"Reload loses exploration.");
            Hud.OpenActivity(ActivityPanel.Kind.Journal);await Task.Delay(350);Fits(Hud.Activity.transform);await Shot("08-exploration-journal.png");
            Progress("PASS ALL: ten-site actual travel/survey/return, branch reveals, cancel, autosave, reload, fuel preservation, text fits, no new scenes.");
        }
        catch(Exception error){Progress("FAIL: "+error);throw;}
        finally
        {
            try{await Load(original);Hud.OpenMap();Hud.Map.Select("L2");await Shot("09-restored-current-map.png");}
            finally{SaveSystem.TestSlotPath=originalPath;}
        }
        return "PASS ALL; original journey and actual save path restored, new next-two destinations visible from existing completed store.";
    }
}
