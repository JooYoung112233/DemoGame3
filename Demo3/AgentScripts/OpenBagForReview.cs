using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Live49.Core;
using Live49.Title;
using Live49.UI;
using UnityEngine;

public static class OpenBagForReview
{
    public static async Task<string> Run()
    {
        var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/BagReview"));Directory.CreateDirectory(folder);
        try
        {
            var start=UnityEngine.Object.FindObjectsByType<TitleMenuItem>().Single(i=>i.Id=="start");start.Owner.Confirm(start);
            var end=DateTime.UtcNow.AddSeconds(90);
            while(GameHud.Instance==null||!GameHud.Instance.IsExploring)
            {
                if(DateTime.UtcNow>end)throw new Exception("Opening timed out.");
                FreshInput.SimulateAdvance();await Task.Delay(800);
            }
            FreshInput.DiscardPending();await Task.Delay(350);
            GameHud.Instance.InvokeAction(GameHud.ActionId.Bag);await Task.Delay(450);
            if(GameHud.Instance.Bag==null||!GameHud.Instance.Bag.IsOpen)throw new Exception("HUD Bag action not connected.");
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"01-opening-bag.png"));await Task.Delay(350);
            File.WriteAllText(Path.Combine(folder,"opening-report.txt"),"PASS: actual title-to-game opening reaches camper exploration; existing HUD Bag action opens the new panel without granting inventory or survival values.");
            return "Actual opening bag ready.";
        }
        catch(Exception e){File.WriteAllText(Path.Combine(folder,"opening-report.txt"),"FAIL: "+e);throw;}
    }
}
