using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Live49.UI;
using TMPro;
using UnityEngine;
public static class InspectSaveSettings
{
    public static async Task<string> Run()
    {
        GameHud.Instance.OpenPause();await Task.Delay(250);GameHud.Instance.OpenSettings();await Task.Delay(300);
        Canvas.ForceUpdateCanvases();
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/SavePolish/settings-inspect.png")));
        var texts=GameHud.Instance.Settings.GetComponentsInChildren<TMP_Text>();
        return string.Join("\n",texts.Where(t=>t.isTextOverflowing).Select(t=>t.name+" size="+t.rectTransform.rect.size+" preferred="+t.preferredWidth+","+t.preferredHeight));
    }
}
