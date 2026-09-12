using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Live49.Core;
using Live49.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public static class VerifyJourneyPolish
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/JourneyPolish"));
    static GameHud Hud=>GameHud.Instance;
    static JourneyState State=>SaveSystem.Current;
    static Button B(string n)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==n);
    static void Check(bool b,string why){if(!b)throw new Exception(why);}
    static async Task Wait(Func<bool> ready){var end=DateTime.UtcNow.AddSeconds(15);while(!ready()){if(DateTime.UtcNow>end)throw new Exception("Timeout");await Task.Delay(25);}}
    static async Task Shot(string n){await Task.Delay(300);Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,n));await Task.Delay(300);}
    static void Fits(Transform root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<TMP_Text>())Check(!t.isTextOverflowing,"Overflow: "+t.text);}
    static async Task Load(JourneyState s)
    {
        for(int i=0;Hud!=null&&Hud.IsPaused&&i<5;i++){Hud.HandleEscape();await Task.Delay(350);}
        Check(SaveSystem.Write(SaveSystem.SlotPath,s,out var e)&&SaveSystem.QueueLoad(out e),e);
        var op=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>op.isDone&&Hud!=null&&Hud.IsExploring);await Task.Delay(300);
    }
    public static string Model()
    {
        var s=new JourneyState{day=2,location="L1"};s.Set("water_checked");s.Set("food_checked");
        foreach(var id in new[]{"L2","L3","L4","L8","L5","L9","L6","L10","L7"})
        {
            s.exploringPlace=id;int minutes=s.minutes;Check(!RegionExploration.Complete(s,id,out _),"Completion bypasses inspections");
            Check(PlaceInspection.Inspect(s,id,0,out var first)&&first.Length==0&&!RegionExploration.Completed(s,id),"First inspection completes place");
            int fuel=s.fuel;Check(!PlaceInspection.Inspect(s,id,0,out _)&&s.fuel==fuel,"Duplicate grants reward");
            Check(PlaceInspection.Inspect(s,id,1,out var reveal)&&RegionExploration.Completed(s,id)&&s.minutes==minutes+10,"Second inspection failed "+id);
            if(id=="L2"||id=="L3"||id=="L4")Check(reveal.Length==2,"Two-site reveal failed");
            if(id=="L6")Check(!RegionExploration.Discovered(s,"L7"),"AND gate bypassed");
        }
        Check(RegionExploration.CompletedCount(s)==10&&s.Valid(),"All ten completion invalid");
        Check(JourneyDayLog.Summary(s).Contains("차량 연료 2"),"Fuel summary absent");
        var saved=JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(s));Check(saved.Valid()&&JourneyDayLog.Summary(saved)==JourneyDayLog.Summary(s),"Daily summary serialization failed");
        s.day++;Check(JourneyDayLog.Summary(s).Contains("아직 없어요"),"Daily summary leaks previous day");
        return "PASS model: 18 distinct inspections; partial progress; two-route reveals; final AND; once-only fuel; per-day log and save.";
    }
    public static async Task<string> Run()
    {
        Directory.CreateDirectory(Folder);File.AppendAllText(Path.Combine(Folder,"progress.txt"),Model()+"\n");
        var original=JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(State));var path=SaveSystem.TestSlotPath;SaveSystem.TestSlotPath=Path.Combine(Folder,"polish-isolated.json");
        try
        {
            var s=JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(original));s.location="L1";s.inStore=false;s.exploringPlace="";s.day=2;s.fuel=3;s.days.Clear();s.Set("water_checked");s.Set("food_checked");s.flags.RemoveAll(f=>f.key.StartsWith("region01.")||f.key.StartsWith("map."));
            await Load(s);Hud.OpenMap();Hud.Map.Select("L2");B("MapTravel").onClick.Invoke();await Shot("03-departure.png");B("MapTravel").onClick.Invoke();
            await Wait(()=>GameObject.Find("ArrivalCard")!=null);await Shot("04-arrival.png");await Wait(()=>Hud.IsExploring&&State.location=="L2");
            B("LeaveCamper").onClick.Invoke();await Wait(()=>Hud.IsExploring&&State.exploringPlace=="L2");await Shot("05-place-actions.png");
            B("InspectRegion_0").onClick.Invoke();await Task.Delay(250);Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);Check(!PlaceInspection.Checked(State,"L2",0),"Cancel inspects");
            B("InspectRegion_0").onClick.Invoke();await Task.Delay(250);B("InteractionChoice_0").onClick.Invoke();await Task.Delay(500);Check(PlaceInspection.Count(State,"L2")==1&&!RegionExploration.Completed(State,"L2"),"Partial inspection invalid");
            Fits(Hud.transform.Find("HUDViewport/PauseOverlay"));Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);
            Check(SaveSystem.TryRead(SaveSystem.SlotPath,out var saved,out _),"Partial autosave missing");await Load(saved);Check(PlaceInspection.Count(State,"L2")==1,"Partial load failed");
            B("InspectRegion_1").onClick.Invoke();await Task.Delay(250);B("InteractionChoice_0").onClick.Invoke();await Task.Delay(500);B("InteractionChoice_0").onClick.Invoke();await Wait(()=>Hud.Map.IsOpen);await Task.Delay(1800);Fits(Hud.Map.transform);await Shot("06-new-paths.png");
            Check(Hud.Map.transform.Find("CityMapViewport/CityMapContent/MapPlace_L4/NewDiscovery").gameObject.activeSelf,"New badge absent");Hud.Map.Select("L4");Check(State.Has("map.seen.L4"),"Selection not remembered");
            Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);B("ReturnFromRegion").onClick.Invoke();await Wait(()=>Hud.IsExploring&&!RegionExploration.Outside(State));
            Hud.RequestDayEnd();await Task.Delay(300);Fits(Hud.transform.Find("HUDViewport/PauseOverlay"));await Shot("07-day-summary.png");Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);Check(State.day==2,"Cancel ends day");
            Hud.RequestDayEnd();await Task.Delay(250);Hud.ConfirmDayEnd();await Wait(()=>Hud.IsExploring&&State.day==3);Check(State.location=="L2"&&State.days.Any(d=>d.day==2),"Day end loses parking/history");
            File.AppendAllText(Path.Combine(Folder,"progress.txt"),"PASS live: departure/arrival; cancel; partial autosave/reload; second inspection; new pins and seen state; day summary/cancel/advance with parking preserved.\n");
        }
        finally{try{await Load(original);Hud.OpenMap();Hud.Map.Select("L2");}finally{SaveSystem.TestSlotPath=path;}}
        return "PASS exploration polish; original progress restored.";
    }
}
