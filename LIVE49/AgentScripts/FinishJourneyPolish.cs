using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Live49.Core;
using Live49.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public static class FinishJourneyPolish
{
    public static async Task<string> Run()
    {
        var hud=GameHud.Instance;var state=SaveSystem.Current;
        if(!string.IsNullOrEmpty(SaveSystem.TestSlotPath))throw new Exception("Test save override not restored");
        if(hud.IsPaused){hud.HandleEscape();await Task.Delay(400);}
        hud.OpenActivity(ActivityPanel.Kind.Journal);await Task.Delay(350);
        var previous=hud.Activity.GetComponentsInChildren<Button>().Single(b=>b.name=="PreviousJournal");
        previous.onClick.Invoke();await Task.Delay(100);
        var heading=hud.Activity.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="JournalDay");
        if(!heading.text.Contains("첫 기록"))throw new Exception("First journal inaccessible");
        hud.Activity.GetComponentsInChildren<Button>().Single(b=>b.name=="NextJournal").onClick.Invoke();await Task.Delay(100);
        if(!heading.text.Contains(state.day+"일 차"))throw new Exception("Current journal inaccessible");
        foreach(var t in hud.Activity.GetComponentsInChildren<TMP_Text>())if(t.isTextOverflowing)throw new Exception("Journal overflow: "+t.text);
        hud.HandleEscape();await Task.Delay(400);hud.OpenMap();await Task.Delay(1800);
        foreach(var t in hud.Map.GetComponentsInChildren<TMP_Text>())if(t.isTextOverflowing)throw new Exception("Map overflow: "+t.text);
        var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/JourneyPolish"));
        ScreenCapture.CaptureScreenshot(Path.Combine(folder,"12-restored-current-map.png"));await Task.Delay(350);
        File.AppendAllText(Path.Combine(folder,"progress.txt"),"PASS final: previous/next journal; first entry retained; map legend/text fits; actual save path restored.\n");
        return "PASS final: original journey day="+state.day+", parking="+state.location+", explored="+RegionExploration.CompletedCount(state)+"; no test save override; map open.";
    }
}
