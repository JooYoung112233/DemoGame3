using System;
using System.IO;
using System.Threading.Tasks;
using Live49.Core;
using Live49.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class MapCheckpoint
{
    public static string PathName=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/CityMapReview/checkpoint.json"));
    public static string Capture()
    {
        var state=SaveSystem.Current;if(state==null)throw new Exception("No current journey.");
        var copy=JsonUtility.FromJson<JourneyState>(JsonUtility.ToJson(state));
        if(!SaveSystem.Write(PathName,copy,out var error))throw new Exception(error);
        return "Current journey copied to isolated map-review checkpoint.";
    }
    public static async Task<string> Restore()
    {
        for(int i=0;GameHud.Instance!=null&&GameHud.Instance.IsPaused&&i<5;i++){GameHud.Instance.HandleEscape();await Task.Delay(350);}
        var original=SaveSystem.TestSlotPath;
        try {SaveSystem.TestSlotPath=PathName;if(!SaveSystem.QueueLoad(out var error))throw new Exception(error);}
        finally {SaveSystem.TestSlotPath=original;}
        var load=SceneManager.LoadSceneAsync(SceneNames.Game);
        var end=DateTime.UtcNow.AddSeconds(20);
        while(!load.isDone||GameHud.Instance==null||!GameHud.Instance.IsExploring)
        {if(DateTime.UtcNow>end)throw new Exception("Restore timeout.");await Task.Delay(40);}
        GameHud.Instance.OpenMap();GameHud.Instance.Map.Select("L1");
        await Task.Delay(500);return "Journey restored; actual HUD map open.";
    }
}
