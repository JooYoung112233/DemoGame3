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
public static class VerifyRegionTravel
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/JourneyPolish"));
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
