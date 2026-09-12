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
public static class VerifyCamperTravel
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/JourneyPolish"));
    static GameHud Hud=>GameHud.Instance;
    static JourneyState State=>SaveSystem.Current;
    static Button B(string n)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==n);
    static void Check(bool b,string why){if(!b)throw new Exception(why);}
    static async Task Wait(Func<bool> ready){var end=DateTime.UtcNow.AddSeconds(15);while(!ready()){if(DateTime.UtcNow>end)throw new Exception("Timeout");await Task.Delay(25);}}
    static async Task Shot(string n){await Task.Delay(300);Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,n));await Task.Delay(300);}
    static async Task Load(JourneyState s)
    {
        for(int i=0;Hud!=null&&Hud.IsPaused&&i<5;i++){Hud.HandleEscape();await Task.Delay(350);}
        Check(SaveSystem.Write(SaveSystem.SlotPath,s,out var e)&&SaveSystem.QueueLoad(out e),e);
        var op=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>op.isDone&&Hud!=null&&Hud.IsExploring);await Task.Delay(300);
    }
    static async Task Move(string id)
    {
        if(!Hud.Map.IsOpen)Hud.OpenMap();Hud.Map.Select(id);B("MapTravel").onClick.Invoke();B("MapTravel").onClick.Invoke();
        await Wait(()=>!Hud.IsPaused&&Hud.IsExploring&&RegionExploration.PlayerPlace(State)==id);
    }
    public static string Model()
    {
        var s=new JourneyState{day=1,location="L1"};s.Set("water_checked");s.Set("food_checked");
        Check(!RegionExploration.Travel(s,"L7"),"Locked destination accepts travel");
        Check(RegionExploration.Travel(s,"L2")&&s.location=="L2"&&s.fuel==2&&!RegionExploration.Outside(s),"Vehicle move failed");
        s.exploringPlace="L2";Check(RegionExploration.Travel(s,"L1")&&s.location=="L2"&&s.inStore&&s.fuel==2&&s.Valid(),"Remote store visit invalid");
        Check(RegionExploration.Travel(s,"L3")&&s.location=="L2"&&s.exploringPlace=="L3"&&s.Valid(),"Walking moved vehicle");
        s.exploringPlace="";s.fuel=0;Check(!RegionExploration.Travel(s,"L3"),"Empty fuel drives");s.exploringPlace="L2";
        Check(RegionExploration.Travel(s,"L3"),"No fuel blocks walking");
        return "PASS model: relocation; locked travel; remote store validation; zero-fuel walking; fuel and vehicle separation.";
    }
    public static async Task<string> Run()
    {
        Directory.CreateDirectory(Folder);File.AppendAllText(Path.Combine(Folder,"progress.txt"),Model()+"\n");
        var original=JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(State));var path=SaveSystem.TestSlotPath;
        SaveSystem.TestSlotPath=Path.Combine(Folder,"isolated.json");
        try
        {
            var fixture=JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(original));fixture.day=2;fixture.location="L1";fixture.inStore=false;fixture.exploringPlace="";fixture.fuel=3;fixture.Set("water_checked");fixture.Set("food_checked");
            await Load(fixture);await Move("L2");Check(State.location=="L2"&&State.fuel==2&&!RegionExploration.Outside(State),"Runtime parking failed");
            B("LeaveCamper").onClick.Invoke();await Wait(()=>Hud.IsExploring&&State.exploringPlace=="L2");await Move("L3");
            Check(State.location=="L2"&&State.fuel==2,"Walking relocated camper");Hud.OpenMap();await Shot("01-person-and-camper.png");Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);
            Check(B("ReturnFromRegion").GetComponentInChildren<TMP_Text>().text.Contains("주유소"),"Wrong return label");
            B("ReturnFromRegion").onClick.Invoke();await Wait(()=>Hud.IsExploring&&!RegionExploration.Outside(State));
            await Move("L3");Check(State.location=="L3"&&State.fuel==1,"Second parking move failed");
            B("LeaveCamper").onClick.Invoke();await Wait(()=>Hud.IsExploring&&State.exploringPlace=="L3");await Move("L1");
            Check(State.inStore&&State.location=="L3","Remote store state failed");
            Check(SaveSystem.TryRead(SaveSystem.SlotPath,out var saved,out _)&&saved.inStore&&saved.location=="L3","Autosave parking failed");await Load(saved);
            Check(State.inStore&&State.location=="L3","Load parking failed");B("ReturnCamper").onClick.Invoke();await Wait(()=>Hud.IsExploring&&!RegionExploration.Outside(State));
            Hud.OpenMap();await Shot("02-relocated-camper.png");
            File.AppendAllText(Path.Combine(Folder,"progress.txt"),"PASS live: L1→L2 vehicle; L2→L3 walk; return L2; relocate L3; visit L1 on foot; save/load/return L3.\n");
        }
        finally{try{await Load(original);Hud.OpenMap();Hud.Map.Select("L2");}finally{SaveSystem.TestSlotPath=path;}}
        return "PASS camper travel; original progress restored.";
    }
}
