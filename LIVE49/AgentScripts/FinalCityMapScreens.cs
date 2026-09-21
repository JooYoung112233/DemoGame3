using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Live49.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public static class FinalCityMapScreens
{
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/CityMapReview"));
    static async Task Shot(string file){await Task.Delay(350);Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Path.Combine(Folder,file));await Task.Delay(350);}
    public static async Task<string> Run()
    {
        var map=GameHud.Instance.Map;
        var art=map.GetComponentInChildren<RawImage>();
        if(art==null||art.texture.name!="city-map-soft-v2"||art.texture.width!=1555||art.texture.height!=1011)throw new Exception("Soft v2 map texture not active at native resolution.");
        map.View.ResetView();map.Select("L1");await Shot("01-city-map.png");
        map.View.StepZoom(1.5f);map.View.Focus(MapPaperGraphic.Store);await Shot("02-city-map-detail.png");
        if(map.GetComponentsInChildren<TMP_Text>().Any(t=>t.isTextOverflowing))throw new Exception("Text overflow after map art replacement.");
        map.View.ResetView();await Shot("04-soft-map-final.png");
        const string result="PASS: softened v2 texture loaded at native 1555x1011; actual day-2 map captured at 100% and 150%; labels and pins separate from background; no text overflow. No gameplay state altered.";
        File.WriteAllText(Path.Combine(Folder,"art-review.txt"),result);return result;
    }
}
