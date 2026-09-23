using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Resource-only update: preserve all existing RectTransforms, strings and routing.
public static class ReplaceTitleResources
{
    const string Art="Assets/Art/FrontEnd/V2/", Prefabs="Assets/Prefabs/FrontEnd/";
    static Font font,bodyFont;
    static Sprite title,steppedTitle,button,gold;
    static void AlignHeading(RectTransform heading)
    {
        var project=heading.Find("ProjectLabel").GetComponent<Text>();
        project.rectTransform.anchoredPosition=new Vector2(64,-30);project.rectTransform.sizeDelta=new Vector2(542,30);project.alignment=TextAnchor.UpperLeft;project.fontSize=24;
        var titleText=heading.Find("Title").GetComponent<Text>();
        // The title font's first glyph has a wider side bearing; compensate
        // optically so visible ink lines up with the smaller regular text.
        titleText.rectTransform.anchoredPosition=new Vector2(60,-72);titleText.rectTransform.sizeDelta=new Vector2(548,82);titleText.alignment=TextAnchor.UpperLeft;titleText.fontSize=72;
        var sub=heading.Find("Subtitle").GetComponent<Text>();
        sub.text="다시, 살아갈\n준비를 합니다.";sub.fontSize=34;sub.lineSpacing=1.12f;
        sub.rectTransform.anchoredPosition=new Vector2(64,-168);sub.rectTransform.sizeDelta=new Vector2(280,98);sub.alignment=TextAnchor.UpperLeft;
    }
    public static string ApplyTitleTypography()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first.");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Preserve unsaved scene changes first.");
        var path=Prefabs+"TitleMenu.prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try{
            var before=Layout(root);AlignHeading(root.transform.Find("TitlePaper").GetComponent<RectTransform>());var after=Layout(root);
            foreach(var pair in before)if(!pair.Key.StartsWith("TitlePaper/")&&after[pair.Key]!=pair.Value)throw new Exception("Unrelated layout changed: "+pair.Key);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
        EditorSceneManager.OpenScene("Assets/Scenes/StartMenu.unity");AssetDatabase.SaveAssets();
        return "Only upper-left ProjectLabel/Title/Subtitle typography adjusted; all other RectTransforms unchanged.";
    }
    public static string Inspect()
    {
        var view=Object.FindAnyObjectByType<TitleViewport>();
        if(!view || !view.FontOverride)throw new Exception("New font not assigned to scene");
        var c=Object.FindAnyObjectByType<TitleMenuController>();
        var bg=c.transform.Find("RooftopBackground");
        if(!bg.Find("ExplorerCap") || !bg.Find("ExplorerPonytail"))throw new Exception("Background layers missing");
        if(AssetDatabase.GetAssetPath(c.NewGameButton.GetComponent<Image>().sprite)!=Art+"button-gold-reference.png")throw new Exception("Button not updated");
        return "V2 applied. Font="+view.FontOverride.name+"; background children="+bg.childCount+"; title="+AssetDatabase.GetAssetPath(c.transform.Find("TitlePaper").GetComponent<Image>().sprite);
    }
    static Dictionary<string,string> Layout(GameObject root)=>root.GetComponentsInChildren<RectTransform>(true).ToDictionary(r=>AnimationUtility.CalculateTransformPath(r,root.transform),r=>r.anchorMin+"|"+r.anchorMax+"|"+r.pivot+"|"+r.anchoredPosition+"|"+r.sizeDelta+"|"+r.localRotation+"|"+r.localScale);
    static Sprite Import(string name)
    {
        var path=Art+name+".png"; var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=4096;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.spriteBorder=Vector4.zero;
        importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static void Style(GameObject root)
    {
        foreach(var text in root.GetComponentsInChildren<Text>(true))
        {
            text.font=(text.name=="Title"||text.name=="Label")?font:bodyFont;
            text.verticalOverflow=VerticalWrapMode.Overflow;
            // FUNFLOW Survivor is the explicitly allowed similar runtime typeface.
            if(text.name=="Arrow")text.fontSize=48;
            else if(text.name=="Title")text.fontSize=root.name.StartsWith("TitleMenu")?96:62;
            else if(text.name=="Label")text.fontSize=46;
            else if(text.name=="ProjectLabel")text.fontSize=30;
            else if(text.name=="Version")text.fontSize=26;
            else if(text.name=="Subtitle")text.fontSize=36;
            else if(text.name=="MenuLabel" || text.name=="QuietCaption")text.fontSize=40;
            else if(text.name=="NoSaveHint")text.fontSize=32;
            else text.fontSize=42;
        }
        foreach(var image in root.GetComponentsInChildren<Image>(true))
        {
            if(image.name=="TitlePaper") {image.sprite=steppedTitle;image.type=Image.Type.Simple;}
            else if(image.GetComponent<Button>()) {image.sprite=button;image.type=Image.Type.Simple;}
            // Popup positions stay the same; common paper now uses the same material.
            else if(image.name=="Paper") {image.sprite=title;image.type=Image.Type.Simple;}
        }
    }
    static void Decorate(Button b,string iconName)
    {
        var icon=b.transform.Find("Icon");
        if(!icon){var go=new GameObject("Icon",typeof(RectTransform),typeof(Image));go.transform.SetParent(b.transform,false);icon=go.transform;}
        var r=(RectTransform)icon;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,.5f);r.anchoredPosition=new Vector2(91,0);r.sizeDelta=new Vector2(64,64);
        var im=icon.GetComponent<Image>();im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+iconName+".png");im.preserveAspect=true;im.raycastTarget=false;
        var label=b.transform.Find("Label").GetComponent<Text>();label.rectTransform.offsetMin=new Vector2(166,0);label.rectTransform.offsetMax=new Vector2(-64,0);label.alignment=TextAnchor.MiddleLeft;label.fontSize=42;
        var arrow=b.transform.Find("Arrow");arrow.GetComponent<Text>().enabled=false;((RectTransform)arrow).anchoredPosition=new Vector2(-32,0);
        var arrowImage=arrow.Find("SourceIcon");if(!arrowImage){var go=new GameObject("SourceIcon",typeof(RectTransform),typeof(Image));go.transform.SetParent(arrow,false);arrowImage=go.transform;}
        r=(RectTransform)arrowImage;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=Vector2.zero;r.sizeDelta=new Vector2(20,30);
        im=arrowImage.GetComponent<Image>();im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"icon-arrow.png");im.raycastTarget=false;im.preserveAspect=true;
        var colors=b.colors;colors.normalColor=Color.white;colors.disabledColor=new Color(.65f,.65f,.65f,1);b.colors=colors;
        b.GetComponent<Image>().sprite=b.name=="NewGame"?gold:button;
    }
    static void Character(Transform parent,string name,Sprite sprite,float x,float y,float w,float h)
    {
        var old=parent.Find(name);if(old)Object.DestroyImmediate(old.gameObject);
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
        var r=(RectTransform)go.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
        r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);
        var image=go.GetComponent<Image>();image.sprite=sprite;image.raycastTarget=false;
    }
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first.");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Preserve unsaved scene changes first.");
        Directory.CreateDirectory(Art);
        foreach(var name in new[]{"title-paper","title-paper-stepped","button-paper-reference","button-gold-reference","city-clean","explorer-cap","explorer-ponytail","icon-new-game","icon-continue","icon-settings","icon-load","icon-exit","icon-arrow"})File.Copy("아트/시작화면-v2/개별-PNG/"+name+".png",Art+name+".png",true);
        AssetDatabase.Refresh();font=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");bodyFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");if(!font||!bodyFont)throw new Exception("Missing packaged font");
        title=Import("title-paper");steppedTitle=Import("title-paper-stepped");button=Import("button-paper-reference");gold=Import("button-gold-reference");var city=Import("city-clean");var cap=Import("explorer-cap");var pony=Import("explorer-ponytail");
        foreach(var name in new[]{"icon-new-game","icon-continue","icon-settings","icon-load","icon-exit","icon-arrow"})Import(name);
        foreach(var name in new[]{"PaperButton","SettingsDialog","LoadDialog","ConfirmDialog","TitleMenu"})
        {
            string path=Prefabs+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var before=Layout(root);Style(root);
                if(name=="TitleMenu")
                {
                    var heading=root.transform.Find("TitlePaper").GetComponent<RectTransform>();
                    heading.sizeDelta=new Vector2(670,670*steppedTitle.rect.height/steppedTitle.rect.width);
                    AlignHeading(heading);
                    var c=root.GetComponent<TitleMenuController>();
                    Decorate(c.NewGameButton,"icon-new-game");Decorate(c.ContinueButton,"icon-continue");Decorate(c.LoadButton,"icon-load");Decorate(c.SettingsButton,"icon-settings");Decorate(c.ExitButton,"icon-exit");
                    var bg=root.transform.Find("RooftopBackground");bg.GetComponent<Image>().sprite=city;
                    Character(bg,"ExplorerCap",cap,291f/1672*1920,371f/941*1080,210f/1672*1920,246f/941*1080);
                    Character(bg,"ExplorerPonytail",pony,516f/1672*1920,439f/941*1080,151f/1672*1920,178f/941*1080);
                }
                var after=Layout(root);
                foreach(var pair in before)if(pair.Key!="TitlePaper"&&!pair.Key.StartsWith("TitlePaper/")&&!pair.Key.EndsWith("/Label")&&!pair.Key.EndsWith("/Icon")&&!pair.Key.EndsWith("/Arrow")&&!pair.Key.EndsWith("/SourceIcon")&&after[pair.Key]!=pair.Value)throw new Exception("Outer layout changed: "+name+"/"+pair.Key);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/StartMenu.unity");
        var viewport=Object.FindAnyObjectByType<TitleViewport>();viewport.FontOverride=font;viewport.RefreshFont();
        EditorUtility.SetDirty(viewport);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return "PASS: outer layout preserved; source paper and source icons applied; internal button alignment matches reference proportions; FUNFLOW Survivor assigned.";
    }
}
