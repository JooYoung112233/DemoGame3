using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
public static class ImportBanditArt
{
    const string AssetRoot="Assets/_Project/Resources/Live49/Encounters/Bandit/";
    static string SourceRoot=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../art/chapter01/bandit-encounter-v2"));
    static void Import(string source,string name)
    {
        string destination=AssetRoot+name+".png";Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.Copy(Path.Combine(SourceRoot,source),destination,true);AssetDatabase.ImportAsset(destination,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(destination);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
        importer.mipmapEnabled=false;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
    }
    public static string Run()
    {
        Import("sources/alley.png","alley");Import("layers/bandit-lead-native.png","lead");Import("layers/bandit-lookout-native.png","lookout");
        Import("layers/lead-contact-shadow.png","lead-shadow");Import("layers/lookout-contact-shadow.png","lookout-shadow");
        AssetDatabase.SaveAssets();return "Imported five separate bandit encounter art layers. No scene was added or changed.";
    }
    static void Image(Transform parent,string resource,float x,float y,float w,float h)
    {
        var r=(RectTransform)new GameObject(resource,typeof(RectTransform),typeof(Image)).transform;
        r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);
        var image=r.GetComponent<Image>();image.sprite=Resources.Load<Sprite>("Live49/Encounters/Bandit/"+resource);image.raycastTarget=false;
        if(image.sprite==null)throw new Exception("Missing imported art: "+resource);
    }
    public static async Task<string> Preview()
    {
        var root=new GameObject("BanditArtVerification",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        try{
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=10000;
            var scale=root.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1672,941);scale.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var frame=(RectTransform)new GameObject("ArtFrame",typeof(RectTransform)).transform;frame.SetParent(root.transform,false);frame.anchorMin=frame.anchorMax=frame.pivot=Vector2.one*.5f;frame.sizeDelta=new Vector2(1672,941);
            Image(frame,"alley",0,0,1672,941);
            var data=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(SourceRoot,"manifest.json")));
            foreach(var p in data["stage"]["placements"])
            {
                string id=(string)p["id"];var shadow=p["shadow_rect"].ToObject<int[]>();Image(frame,id+"-shadow",shadow[0],shadow[1],shadow[2],shadow[3]);
                var b=p["source_alpha_bounds"].ToObject<int[]>();var r=p["rect"].ToObject<int[]>();float factor=r[3]/(float)(b[3]-b[1]);
                Image(frame,id,r[0]-b[0]*factor,r[1]-b[1]*factor,1024*factor,1536*factor);
            }
            await Task.Delay(250);Canvas.ForceUpdateCanvases();
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/BanditArt"));Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"02-intimidating-v2.png"));await Task.Delay(350);
            return "PASS five imported layers rendered in Unity; preview canvas removed afterwards.";
        }finally{UnityEngine.Object.Destroy(root);}
    }
}
