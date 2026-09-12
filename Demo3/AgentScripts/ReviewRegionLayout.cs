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
public static class ReviewRegionLayout
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/RegionExploration"));
    static GameHud Hud=>GameHud.Instance;
    static Button B(string name)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==name);
    static async Task Wait(Func<bool> ready){var end=DateTime.UtcNow.AddSeconds(15);while(!ready()){if(DateTime.UtcNow>end)throw new Exception("Timeout");await Task.Delay(25);}}
    static async Task Shot(string name){await Task.Delay(350);Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name));await Task.Delay(300);}
    static void Fits(Transform root){Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<TMP_Text>())if(t.isTextOverflowing)throw new Exception("Overflow: "+t.text);}
    static async Task Load(JourneyState state)
    {
        for(int i=0;Hud!=null&&Hud.IsPaused&&i<5;i++){Hud.HandleEscape();await Task.Delay(350);}
        if(!SaveSystem.Write(SaveSystem.SlotPath,state,out var error)||!SaveSystem.QueueLoad(out error))throw new Exception(error);
        var op=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>op.isDone&&Hud!=null&&Hud.IsExploring);await Task.Delay(350);
    }
    public static async Task<string> Run()
    {
        var original=JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(SaveSystem.Current));var path=SaveSystem.TestSlotPath;
        SaveSystem.TestSlotPath=Path.Combine(Folder,"layout-isolated-save.json");
        try
        {
            var full=JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(original));full.location="L1";full.inStore=false;full.exploringPlace="L7";
            full.Set("water_checked");full.Set("food_checked");foreach(var s in RegionExploration.Sites.Where(s=>s.Id!="L1"))full.Set(RegionExploration.RegionId+".explored."+s.Id);
            await Load(full);Hud.OpenMap();Hud.Map.Select("L7");await Task.Delay(200);Fits(Hud.Map.transform);await Shot("07-ten-places.png");
            foreach(var id in new[]{"L8","L2"})
            {
                Hud.Map.Select(id);B("MapTravel").onClick.Invoke();B("MapTravel").onClick.Invoke();
                await Wait(()=>Hud.IsExploring&&!Hud.IsPaused&&SaveSystem.Current.exploringPlace==id);Fits(Hud.transform);
                await Shot(id=="L8"?"05-parking-overview.png":"04-gas-station.png");Hud.OpenMap();
            }
            File.AppendAllText(Path.Combine(Folder,"progress.txt"),"PASS layout: ten-place map; parking overview; compact location HUD; no text overflow.\n");
        }
        finally
        {
            try{await Load(original);Hud.OpenMap();Hud.Map.Select("L2");await Shot("09-restored-current-map.png");}
            finally{SaveSystem.TestSlotPath=path;}
        }
        return "PASS layout; original progress and save path restored.";
    }
}
