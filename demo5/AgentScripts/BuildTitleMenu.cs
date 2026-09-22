using System;
using System.IO;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class BuildTitleMenu
{
    const string Folder="Assets/Prefabs/FrontEnd";
    static Sprite paper;
    static GameObject buttonPrefab;
    static Color Ink=new Color(.09f,.13f,.115f), Cream=new Color(.9f,.86f,.75f);
    public static string Inspect() => string.Join("\n",Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Select(i=>{var s=UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);return s.path+" dirty="+s.isDirty;}));
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before editing the menu.");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Current scene has unsaved changes; preserve it before opening the title scene.");
        Directory.CreateDirectory(Folder);Directory.CreateDirectory("Assets/Art/FrontEnd");Directory.CreateDirectory("Assets/Scenes");
        CopySource("아트/시작화면-v1/시작배경-원본.png","Assets/Art/FrontEnd/title-rooftop-v1.png");
        CopySource("아트/시작화면-v1/종이질감-원본.png","Assets/Art/FrontEnd/paper-ivory-v1.png");
        AssetDatabase.Refresh();
        var background=Import("Assets/Art/FrontEnd/title-rooftop-v1.png",false);paper=Import("Assets/Art/FrontEnd/paper-ivory-v1.png",true);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var cameraGo=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));cameraGo.tag="MainCamera";
        var camera=cameraGo.GetComponent<Camera>();camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
        var es=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        var canvasGo=new GameObject("FrontEndCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(TitleViewport));
        var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        var scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var root=Rect("TitleMenu",canvasGo.transform,0,0,1920,1080);root.anchorMin=root.anchorMax=root.pivot=new Vector2(.5f,.5f);root.anchoredPosition=Vector2.zero;
        var viewport=canvasGo.GetComponent<TitleViewport>();viewport.DesignRoot=root;
        var controller=root.gameObject.AddComponent<TitleMenuController>();
        var bg=Image("RooftopBackground",root,0,0,1920,1080,Color.white);bg.sprite=background;bg.raycastTarget=false;
        var heading=Image("TitlePaper",root,110,80,670,185,Color.white);heading.sprite=paper;heading.type=UnityEngine.UI.Image.Type.Sliced;heading.transform.localRotation=Quaternion.Euler(0,0,1.3f);heading.raycastTarget=false;
        Text("ProjectLabel",heading.transform,30,13,600,35,"DEMO 5",22,Ink);
        Text("Title",heading.transform,30,51,615,86,"새로운 여정",62,Ink);
        Text("Subtitle",heading.transform,32,142,605,30,"다시, 살아갈 준비를 합니다.",24,Ink);
        Text("QuietCaption",root,116,942,870,65,"낯선 하루도, 함께라면.",26,Cream);
        Text("Version",root,1490,1000,320,36,"demo5  ·  0.1",18,Cream,TextAnchor.MiddleRight);
        var menu=Rect("MainMenu",root,1220,246,550,642);controller.Menu=menu.gameObject.AddComponent<CanvasGroup>();
        Text("MenuLabel",menu,0,0,550,50,"우리의 다음 이야기",27,Cream);
        var template=CreateButtonTemplate();buttonPrefab=PrefabUtility.SaveAsPrefabAsset(template,Folder+"/PaperButton.prefab");Object.DestroyImmediate(template);
        controller.NewGameButton=Button("NewGame",menu,0,86,550,86,"새 게임",true);
        controller.ContinueButton=Button("Continue",menu,0,188,550,86,"계속하기");
        controller.LoadButton=Button("Load",menu,0,290,550,86,"불러오기");
        controller.SettingsButton=Button("Settings",menu,0,392,550,86,"설정");
        controller.ExitButton=Button("Exit",menu,0,494,550,74,"게임 종료");
        Text("NoSaveHint",menu,3,590,544,40,"저장된 여정이 없습니다.",21,Cream);
        BuildSettings(root,controller);BuildLoad(root,controller);BuildConfirm(root,controller);
        var fade=Image("TransitionFade",root,0,0,1920,1080,Color.black);controller.Fade=fade.gameObject.AddComponent<CanvasGroup>();controller.Fade.alpha=0;controller.Fade.blocksRaycasts=false;
        var prefab=PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject,Folder+"/TitleMenu.prefab",InteractionMode.AutomatedAction);
        viewport.RefreshFont();
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/StartMenu.unity");
        var old=EditorBuildSettings.scenes.Where(s=>s.path!="Assets/Scenes/StartMenu.unity").ToList();
        if(!old.Any(s=>s.path=="Assets/Scenes/NightExpedition.unity"))old.Add(new EditorBuildSettingsScene("Assets/Scenes/NightExpedition.unity",true));
        var destination=old.Find(s=>s.path=="Assets/Scenes/NightExpedition.unity");destination.enabled=true;
        old.Insert(0,new EditorBuildSettingsScene("Assets/Scenes/StartMenu.unity",true));EditorBuildSettings.scenes=old.ToArray();
        AssetDatabase.SaveAssets();
        return "StartMenu scene created; editable TitleMenu, PaperButton, SettingsDialog, LoadDialog, ConfirmDialog prefabs. Existing prototype preserved.";
    }
    static void CopySource(string source,string destination){if(!File.Exists(destination))File.Copy(source,destination);}
    static Sprite Import(string p,bool panel){var t=(TextureImporter)AssetImporter.GetAtPath(p);t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.spritePixelsPerUnit=100;t.mipmapEnabled=false;t.maxTextureSize=4096;t.textureCompression=TextureImporterCompression.Uncompressed;t.alphaIsTransparency=true;t.wrapMode=TextureWrapMode.Clamp;t.spriteBorder=panel?new Vector4(24,24,24,24):Vector4.zero;t.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(p);}
    static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();Place(r,x,y,w,h);return r;}
    static void Place(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    static UnityEngine.UI.Image Image(string name,Transform parent,float x,float y,float w,float h,Color c){var r=Rect(name,parent,x,y,w,h);var im=r.gameObject.AddComponent<UnityEngine.UI.Image>();im.color=c;return im;}
    static Text Text(string name,Transform parent,float x,float y,float w,float h,string value,int size,Color color,TextAnchor alignment=TextAnchor.MiddleLeft){var r=Rect(name,parent,x,y,w,h);var t=r.gameObject.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=value;t.fontSize=size;t.color=color;t.alignment=alignment;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;}
    static GameObject CreateButtonTemplate(){var r=Rect("PaperButton",null,0,0,550,86);var im=r.gameObject.AddComponent<UnityEngine.UI.Image>();im.sprite=paper;im.type=UnityEngine.UI.Image.Type.Sliced;var b=r.gameObject.AddComponent<Button>();b.targetGraphic=im;var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,.9f,.65f);colors.selectedColor=new Color(1,.91f,.7f);colors.pressedColor=new Color(.8f,.72f,.52f);colors.disabledColor=new Color(.45f,.48f,.44f,.85f);colors.fadeDuration=.12f;b.colors=colors;
        var label=Text("Label",r,26,0,490,86,"버튼",32,Ink);label.rectTransform.anchorMax=Vector2.one;label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.offsetMin=new Vector2(28,0);label.rectTransform.offsetMax=new Vector2(-45,0);
        var arrow=Text("Arrow",r,500,0,36,86,"›",36,Ink,TextAnchor.MiddleCenter);arrow.rectTransform.anchorMin=arrow.rectTransform.anchorMax=new Vector2(1,.5f);arrow.rectTransform.pivot=new Vector2(1,.5f);arrow.rectTransform.anchoredPosition=new Vector2(-14,0);return r.gameObject;}
    static Button Button(string name,Transform parent,float x,float y,float w,float h,string label,bool primary=false){var go=(GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab,parent);go.name=name;Place(go.GetComponent<RectTransform>(),x,y,w,h);go.transform.Find("Label").GetComponent<Text>().text=label;var b=go.GetComponent<Button>();if(primary){var c=b.colors;c.normalColor=new Color(1,.87f,.57f);b.colors=c;}return b;}
    static GameObject Modal(string name,Transform root,string title,out RectTransform sheet){var layer=Image(name,root,0,0,1920,1080,new Color(.015f,.025f,.025f,.78f));sheet=Rect("Paper",layer.transform,480,265,960,550);var im=sheet.gameObject.AddComponent<UnityEngine.UI.Image>();im.sprite=paper;im.type=UnityEngine.UI.Image.Type.Sliced;Text("Title",sheet,48,36,864,74,title,42,Ink);return layer.gameObject;}
    static void BuildSettings(Transform root,TitleMenuController c){c.SettingsPanel=Modal("SettingsDialog",root,"설정",out var sheet);Text("AudioLabel",sheet,48,142,420,54,"전체 음량",28,Ink);c.VolumeValue=Text("VolumeValue",sheet,700,142,208,54,"100%",28,Ink,TextAnchor.MiddleRight);
        var sliderRoot=Rect("MasterVolume",sheet,48,222,864,42);var slider=sliderRoot.gameObject.AddComponent<Slider>();c.Volume=slider;var track=Image("Track",sliderRoot,0,12,864,18,new Color(.15f,.23f,.2f));track.raycastTarget=false;
        var fillArea=Rect("FillArea",sliderRoot,12,12,840,18);var fill=Image("Fill",fillArea,0,0,0,0,new Color(.36f,.48f,.32f));fill.rectTransform.anchoredPosition=Vector2.zero;fill.raycastTarget=false;slider.fillRect=fill.rectTransform;
        var handles=Rect("HandleArea",sliderRoot,12,0,840,42);var handle=Image("Handle",handles,0,0,26,0,Ink);handle.rectTransform.anchoredPosition=Vector2.zero;slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;slider.minValue=0;slider.maxValue=1;slider.value=1;
        var toggleRoot=Rect("Fullscreen",sheet,48,306,864,62);c.Fullscreen=toggleRoot.gameObject.AddComponent<Toggle>();var box=Image("Checkbox",toggleRoot,0,8,42,42,new Color(.18f,.25f,.21f));var mark=Image("Check",box.transform,8,8,26,26,new Color(.79f,.71f,.48f));mark.raycastTarget=false;c.Fullscreen.targetGraphic=box;c.Fullscreen.graphic=mark;Text("FullscreenLabel",toggleRoot,64,0,770,60,"전체 화면",28,Ink);
        c.SettingsCancel=Button("Cancel",sheet,48,438,410,70,"취소");c.SettingsApply=Button("Apply",sheet,502,438,410,70,"적용",true);PrefabUtility.SaveAsPrefabAssetAndConnect(c.SettingsPanel,Folder+"/SettingsDialog.prefab",InteractionMode.AutomatedAction);c.SettingsPanel.SetActive(false);}
    static void BuildLoad(Transform root,TitleMenuController c){c.LoadPanel=Modal("LoadDialog",root,"불러오기",out var sheet);c.LoadMessage=Text("EmptyState",sheet,48,155,864,165,"저장된 여정이 없습니다.",30,Ink,TextAnchor.MiddleCenter);c.LoadClose=Button("Close",sheet,502,438,410,70,"돌아가기");PrefabUtility.SaveAsPrefabAssetAndConnect(c.LoadPanel,Folder+"/LoadDialog.prefab",InteractionMode.AutomatedAction);c.LoadPanel.SetActive(false);}
    static void BuildConfirm(Transform root,TitleMenuController c){c.ConfirmPanel=Modal("ConfirmDialog",root,"게임을 종료할까요?",out var sheet);c.ConfirmTitle=sheet.Find("Title").GetComponent<Text>();c.ConfirmBody=Text("Body",sheet,48,150,864,190,"다음 여정에서 다시 만나요.",30,Ink);c.ConfirmCancel=Button("Cancel",sheet,48,438,410,70,"돌아가기",true);c.ConfirmAccept=Button("Confirm",sheet,502,438,410,70,"종료");PrefabUtility.SaveAsPrefabAssetAndConnect(c.ConfirmPanel,Folder+"/ConfirmDialog.prefab",InteractionMode.AutomatedAction);c.ConfirmPanel.SetActive(false);}
}
