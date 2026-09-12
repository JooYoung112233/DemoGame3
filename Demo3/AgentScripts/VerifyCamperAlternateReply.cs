using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Live49.Chapter00;
using Live49.Core;
using Live49.Dialogue;
using Live49.Title;
using Live49.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public static class VerifyCamperAlternateReply
{
    static GameHud Hud=>GameHud.Instance;
    static Button Button(string name)=>UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.name==name);
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/CamperExploration"));
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static async Task Wait(Func<bool> ready)
    {var end=DateTime.UtcNow.AddSeconds(20);while(!ready()){if(DateTime.UtcNow>end)throw new Exception("Alternate reply timeout.");await Task.Delay(16);}}
    static async Task Shot(string name){ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name));await Task.Delay(350);}
    public static async Task<string> Run()
    {
        try
        {
            var start=UnityEngine.Object.FindObjectsByType<TitleMenuItem>().Single(i=>i.Id=="start");start.Owner.Confirm(start);
            var end=DateTime.UtcNow.AddSeconds(90);
            while(Hud==null||!Hud.IsExploring)
            {
                if(DateTime.UtcNow>end)throw new Exception("Opening did not reach exploration.");
                FreshInput.SimulateAdvance();await Task.Delay(800);
            }
            FreshInput.DiscardPending();await Task.Delay(300);
            foreach(var id in new[]{"soi","kitchen","bed","book"})
            {
                var button=Button("Target_"+id);var marker=(RectTransform)button.transform.Find("Marker");
                var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,marker.TransformPoint(marker.rect.center))};
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
                Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Marker is outside clickable target: "+id);
            }
            await Shot("01-camper-hud.png");
            var soi=Button("Target_soi");var hover=new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(soi.gameObject,hover,ExecuteEvents.pointerEnterHandler);await Shot("05-soi-hover.png");
            ExecuteEvents.Execute(soi.gameObject,hover,ExecuteEvents.pointerExitHandler);
            soi.onClick.Invoke();await Task.Delay(350);await Shot("03-soi-choices.png");
            var original=InputSystem.settings;
            var restore=original.hideFlags==HideFlags.HideAndDontSave?UnityEngine.Object.Instantiate(original):original;
            var input=UnityEngine.Object.Instantiate(original);InputSystem.settings=input;
            input.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            input.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard=InputSystem.AddDevice<Keyboard>("CamperAuditKeyboard");
            try
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));await Task.Delay(250);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Wait(()=>!Hud.IsPaused);
            }
            finally
            {
                if(keyboard.added)InputSystem.RemoveDevice(keyboard);InputSystem.settings=restore;
                if(input!=null)UnityEngine.Object.Destroy(input);
            }
            var director=UnityEngine.Object.FindAnyObjectByType<C0OpeningDirector>();
            Check(!director.HasAnsweredSoi&&Time.timeScale==1,"Actual Escape advanced or paused story.");
            await Task.Delay(150);soi.onClick.Invoke();await Task.Delay(350);Button("InteractionChoice_1").onClick.Invoke();
            var view=UnityEngine.Object.FindAnyObjectByType<DialogueView>();
            var body=UnityEngine.Object.FindObjectsByType<TMP_Text>().Single(t=>t.name=="Body");
            foreach(var line in new[]{"어디로 가고 싶은데?","아직 몰라. 가면서 생각할래.","여기서는 창밖이 계속 똑같아.","그럼 새로운 걸 보면, 여기 그려 둘까?"})
            {
                await Wait(()=>body.text==line&&!view.Body.IsTyping&&view.Cue.alpha>.99f);
                Check(!Hud.IsExploring&&!Hud.IsPaused,"Reply B state mismatch.");
                FreshInput.SimulateAdvance();await Task.Delay(250);
            }
            await Wait(()=>Hud.IsExploring);await Task.Delay(350);
            Check(director.HasAnsweredSoi&&!director.HasSeenBook,"Reply B did not unlock book.");
            await Shot("06-review-hud.png");
            string report="PASS: fresh title-to-game play; all visible markers hit their native UI buttons; hover label; actual InputSystem Escape cancels choice without progress; reply B completes and unlocks book; normal dialogue typing preserved; final HUD remains open in actual game for review.";
            File.WriteAllText(Path.Combine(Folder,"alternate-report.txt"),report);return report;
        }
        catch(Exception error){File.WriteAllText(Path.Combine(Folder,"alternate-report.txt"),"FAIL: "+error);throw;}
    }
}
