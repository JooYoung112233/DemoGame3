using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Live49.Core;
using Live49.Chapter00;
using Live49.Dialogue;
using Live49.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public static class VerifyPortraitFacing
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/PortraitFacing"));
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static async Task Wait(Func<bool> predicate){var end=DateTime.UtcNow.AddSeconds(25);while(!predicate()){if(DateTime.UtcNow>end)throw new Exception("Timeout waiting for portrait");await Task.Delay(40);}}
    static async Task Shot(string name){Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));await Task.Delay(350);}
    public static async Task<string> Run()
    {
        Directory.CreateDirectory(Folder);string real=SaveSystem.SlotPath;string original=File.Exists(real)?File.ReadAllText(real):null;
        var old=SaveSystem.TestSlotPath;SaveSystem.TestSlotPath=Path.Combine(Folder,"test.json");
        try{
            var s=new JourneyState{day=3,location="L2",exploringPlace="L2",answeredSoi=true,bookSeen=true};s.Set(RegionTravel.Drawing);s.Set("water_checked");s.Set("food_checked");
            Check(FirstWeekStory.Begin(s,"meet"),"Cannot begin Sejin meeting");
            s.weekLine=1;
            Check(SaveSystem.Write(SaveSystem.SlotPath,s,out var error)&&SaveSystem.QueueLoad(out error),error);
            var op=SceneManager.LoadSceneAsync(SceneNames.Game);await Wait(()=>op.isDone&&GameHud.Instance!=null);
            await Wait(()=>UnityEngine.Object.FindAnyObjectByType<DialogueView>()?.Cue.alpha>.9f);await Task.Delay(300);
            var view=UnityEngine.Object.FindAnyObjectByType<DialogueView>();
            var sejin=GameObject.Find("Portrait_sejin").GetComponent<RectTransform>();
            var suhyeok=GameObject.Find("Portrait_Suhyeok").GetComponent<RectTransform>();
            var soi=GameObject.Find("Portrait_Soi").GetComponent<RectTransform>();
            Check(sejin.localScale.x<0&&suhyeok.localScale.x>0&&soi.localScale.x>0,"Facing regression");
            Check(view.SpeakerLabel=="?","Unintroduced name was revealed");
            await Shot("01-sejin-facing-and-scale");
            var position=sejin.anchoredPosition;var size=sejin.sizeDelta;var pivot=sejin.pivot;var scale=sejin.localScale;
            sejin.pivot=new Vector2(0,1);sejin.sizeDelta=new Vector2(700,1050);sejin.anchoredPosition=new Vector2(1100,-75);sejin.localScale=Vector3.one;
            await Shot("00-before-sejin");sejin.pivot=pivot;sejin.sizeDelta=size;sejin.anchoredPosition=position;sejin.localScale=scale;
            // A restored encounter uses the same registered layout; it never cumulatively flips/scales.
            view.AddPortrait("sejin",Resources.Load<Sprite>("Live49/Stages/week-sejin-portrait"));
            Check(sejin.sizeDelta==size&&sejin.localScale==scale,"Repeated registration changed layout");
            view.StartCoroutine(view.SetPortraits(new[]{"suhyeok","soi"},null,0));await Task.Delay(100);view.SetSpeaker("soi","소이");
            await Shot("02-soi-facing-preserved");
            File.WriteAllText(Path.Combine(Folder,"verification.txt"),"PASS facing: Suhyeok right, Sejin left, Soi already-left retained; repeat registration stable; Sejin remains ? until introduced.\nSejin rectangle "+size+" position "+position);
        }
        finally{
            var op=SceneManager.LoadSceneAsync(SceneNames.Title);await Wait(()=>op.isDone);SaveSystem.TestSlotPath=old;
            Check((File.Exists(real)?File.ReadAllText(real):null)==original,"Real save changed");
        }
        return "PASS portrait facing and registration; screenshots captured; existing save unchanged.";
    }
}
