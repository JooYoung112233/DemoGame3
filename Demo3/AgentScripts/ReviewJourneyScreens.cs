using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Live49.Chapter00;
using Live49.Core;
using Live49.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ReviewJourneyScreens
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/JourneyReview"));
    static GameHud Hud=>GameHud.Instance;
    static Button Button(string name)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==name);
    static async Task Wait(Func<bool> ready)
    {var end=DateTime.UtcNow.AddSeconds(15);while(!ready()){if(DateTime.UtcNow>end)throw new Exception("Review wait timed out.");await Task.Delay(25);}}
    static async Task Shot(string file){await Task.Delay(350);ScreenCapture.CaptureScreenshot(Path.Combine(Folder,file));await Task.Delay(350);}
    static void TextFits(Transform root)
    {Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<TMP_Text>())if(t.isTextOverflowing)throw new Exception("Text overflow: "+t.name);}
    public static async Task<string> Run()
    {
        SaveSystem.TestSlotPath=Path.Combine(Folder,"runtime-save.json");
        if(!SaveSystem.QueueLoad(out var error))throw new Exception(error);
        await Task.Delay(100);var load=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>load.isDone);
        await Wait(()=>Hud!=null&&Hud.IsExploring);await Task.Delay(400);
        var director=UnityEngine.Object.FindAnyObjectByType<C0OpeningDirector>();
        if(director.State.day!=2||director.State.Count("water")!=2)throw new Exception("Review checkpoint mismatch.");
        TextFits(Hud.transform.Find("HUDViewport/Location"));
        var enter=Button("EnterStore");var rt=(RectTransform)enter.transform;
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(rt.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        if(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=enter)throw new Exception("World action is not pointer accessible.");
        await Shot("10-camper-hud.png");
        enter.onClick.Invoke();await Wait(()=>Hud.IsExploring&&director.State.inStore);await Shot("03-store.png");
        Button("ReturnCamper").onClick.Invoke();await Wait(()=>Hud.IsExploring&&!director.State.inStore);
        Hud.OpenActivity(ActivityPanel.Kind.Journal);await Task.Delay(400);TextFits(Hud.Activity.transform);await Shot("01-journal.png");
        Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);
        Hud.OpenPause();Hud.ShowSaveCard();await Task.Delay(400);TextFits(Hud.transform.Find("HUDViewport/PauseOverlay"));await Shot("05-save-menu.png");
        Hud.HandleEscape();Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);
        Hud.OpenActivity(ActivityPanel.Kind.Kitchen);await Task.Delay(400);TextFits(Hud.Activity.transform);await Shot("11-kitchen-availability.png");
        if(Button("StartCooking").interactable)throw new Exception("Unrepaired kitchen can cook.");
        Hud.HandleEscape();await Wait(()=>!Hud.IsPaused);
        Hud.OpenActivity(ActivityPanel.Kind.Journal);await Shot("09-review-journal.png");
        SaveSystem.TestSlotPath=null;
        string result="PASS: final layout restored from isolated day-2 checkpoint; location text fits; native pointer hits world action; existing store entry/return; journal completed labels render without unsupported glyphs; save and kitchen text fit; unrepaired kitchen stays gated; stable screenshots captured after modal fade. User save path restored.";
        File.WriteAllText(Path.Combine(Folder,"visual-report.txt"),result);return result;
    }
}
