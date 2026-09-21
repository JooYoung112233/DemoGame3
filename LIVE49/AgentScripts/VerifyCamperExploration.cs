using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Live49.Chapter00;
using Live49.Core;
using Live49.Dialogue;
using Live49.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Run after AuditOpeningPacing has reached the final present-day question.
public static class VerifyCamperExploration
{
    static GameHud Hud => GameHud.Instance;
    static DialogueView View => UnityEngine.Object.FindAnyObjectByType<DialogueView>();
    static Button Button(string name) => UnityEngine.Object.FindObjectsByType<Button>().Single(b => b.name == name);
    static TMP_Text Body => UnityEngine.Object.FindObjectsByType<TMP_Text>().Single(t => t.name == "Body");
    static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/CamperExploration"));
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static async Task Wait(Func<bool> condition, int seconds = 15)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (!condition()) { if(DateTime.UtcNow>end) throw new Exception("Exploration wait timed out."); await Task.Delay(16); }
    }
    static async Task Shot(string name)
    { ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name)); await Task.Delay(350); }
    static async Task Back()
    { Hud.HandleEscape(); await Wait(()=>!Hud.IsPaused); await Task.Delay(100); Check(Time.timeScale==1,"Clock was not restored."); }
    static async Task Conversation(string[] lines)
    {
        foreach (var line in lines)
        {
            await Wait(()=>Body.text==line && !View.Body.IsTyping && View.Cue.alpha>.99f);
            Check(!Hud.IsExploring && !Hud.IsPaused,"Dialogue leaked exploration or pause state.");
            FreshInput.SimulateAdvance();
            await Task.Delay(250);
        }
        await Wait(()=>Hud.IsExploring);
        await Task.Delay(250);
    }
    public static async Task<string> Run()
    {
        Directory.CreateDirectory(Folder);
        try
        {
            Check(Body.text=="내일도 여기 있어?" && !Hud.IsExploring,"Start at the final question.");
            await Task.Delay(600);
            Check(!Hud.IsExploring,"Final question did not hold for fresh input.");
            FreshInput.SimulateAdvance();
            await Wait(()=>Hud.IsExploring); await Task.Delay(400);
            Check(SceneManager.sceneCount==1 && SceneManager.GetActiveScene().name=="01_Game","Unexpected scene transition.");
            Check(View.Cue.transform.parent.GetComponent<CanvasGroup>().alpha==0,"Dialogue remains over exploration.");
            foreach(var id in new[]{"soi","kitchen","bed","book"})
            {
                var button=Button("Target_"+id); var rt=(RectTransform)button.transform;
                var data=new PointerEventData(EventSystem.current) { position=RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(rt.rect.center)) };
                var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(data,hits);
                Check(hits.Count>0 && hits[0].gameObject.GetComponentInParent<Button>()==button,"Target cannot receive pointer input: "+id);
            }
            await Shot("01-camper-hud.png");
            Button("Target_book").onClick.Invoke(); await Task.Delay(350);
            Check(Hud.IsPaused && Time.timeScale==0,"Book prerequisite panel failed."); await Back();
            Button("Target_kitchen").onClick.Invoke(); await Task.Delay(350);
            await Shot("02-kitchen.png"); await Back();
            Button("Target_bed").onClick.Invoke(); await Task.Delay(350);
            Check(!Button("Button_하루 마치기").interactable,"Day end bypassed story prerequisites."); await Back();
            Button("Action_Map").onClick.Invoke(); await Task.Delay(350);
            Check(Hud.Map.IsOpen && Hud.IsPaused,"Map was not connected."); await Back();
            Hud.HandleEscape(); await Task.Delay(300); Check(Hud.IsPaused,"Pause failed."); await Back();
            Button("Target_soi").onClick.Invoke(); await Task.Delay(350);
            Check(Button("InteractionChoice_0").interactable && Button("InteractionChoice_1").interactable,"Missing response choice.");
            await Shot("03-soi-choices.png"); await Back();
            var director=UnityEngine.Object.FindAnyObjectByType<C0OpeningDirector>();
            Check(!director.HasAnsweredSoi,"Cancelling a choice advanced the story.");
            Button("Target_soi").onClick.Invoke(); await Task.Delay(300);
            var choice=Button("InteractionChoice_0"); choice.onClick.Invoke(); choice.onClick.Invoke();
            await Conversation(new[]{"다른 데 가 보고 싶어?","응. 다른 것도 보고 싶어.","아침에도 저 표지판이 보였잖아.","그럼 새로운 걸 보면, 여기 그려 둘까?"});
            Check(director.HasAnsweredSoi && !director.HasSeenBook,"Reply progression failed.");
            Button("Target_book").onClick.Invoke();
            await Conversation(new[]{"여기는 비워 뒀네.","응. 나중에 그릴 거야.","모서리가 많이 닳았네.","그래도 여기는 깨끗해.","아직 그릴 데가 많네."});
            Check(director.HasSeenBook && Time.timeScale==1,"Book progression failed.");
            await Shot("04-after-book.png");
            var report="PASS: final question waits for fresh input; exploration HUD enters in existing game scene; all four targets receive raycasts; book prerequisite, kitchen, bed, map and pause return without clock leaks; two response buttons; cancellation preserves progress; repeated selection dispatches once; reply A and book dialogue play at normal speed; HUD returns after each conversation. Reply B text/data verified separately. No new scene.\n";
            File.WriteAllText(Path.Combine(Folder,"report.txt"),report); return report;
        }
        catch(Exception error) { File.WriteAllText(Path.Combine(Folder,"report.txt"),"FAIL: "+error); throw; }
    }
}
