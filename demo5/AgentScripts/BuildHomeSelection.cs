using System;
using System.IO;
using System.Linq;
using Demo5.FrontEnd;
using Demo5.NightRun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Object=UnityEngine.Object;
public static class BuildHomeSelection
{
    const string P="Assets/Prefabs/HomeSelection/";
    static Font font;static Color ink=new Color(.06f,.095f,.095f),cream=new Color(.87f,.82f,.7f);
    static RectTransform R(string name,Transform parent,float x,float y,float w,float h){var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);var r=(RectTransform)g.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
    static Image I(string name,Transform parent,float x,float y,float w,float h,string sprite){var im=R(name,parent,x,y,w,h).gameObject.AddComponent<Image>();im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(sprite);im.raycastTarget=false;return im;}
    static string Art(string name)=>"Assets/Art/PartySelection/"+name+".png";
    static Text T(string name,Transform parent,float x,float y,float w,float h,string text,int size){var t=R(name,parent,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.text=text;t.fontSize=size;t.color=ink;t.raycastTarget=false;t.lineSpacing=1.08f;return t;}
    static Button B(string name,Transform parent,float x,float y,float w,string text){var im=I(name,parent,x,y,w,78,Art("footer-paper"));im.raycastTarget=true;var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;var colors=b.colors;colors.highlightedColor=new Color(1,.94f,.8f);colors.selectedColor=Color.white;colors.disabledColor=new Color(.6f,.6f,.6f);b.colors=colors;T("Label",im.transform,15,0,w-30,78,text,32).alignment=TextAnchor.MiddleCenter;return b;}
    public static string RefreshArtwork(){File.Copy("아트/정착지선택-v1/개별-PNG/garage.png","Assets/Art/HomeSelection/garage.png",true);AssetDatabase.ImportAsset("Assets/Art/HomeSelection/garage.png",ImportAssetOptions.ForceUpdate);return "Removed source check-badge edge from garage crop; layout unchanged.";}
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Preserve unsaved scene edits first");
        Directory.CreateDirectory(P);Directory.CreateDirectory("Assets/Art/HomeSelection");
        foreach(var f in Directory.GetFiles("아트/정착지선택-v1/개별-PNG","*.png"))File.Copy(f,"Assets/Art/HomeSelection/"+Path.GetFileName(f),true);
        AssetDatabase.Refresh();foreach(var f in Directory.GetFiles("Assets/Art/HomeSelection","*.png")){var t=(TextureImporter)AssetImporter.GetAtPath(f.Replace('\\','/'));t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.alphaIsTransparency=true;t.mipmapEnabled=false;t.textureCompression=TextureImporterCompression.Uncompressed;t.SaveAndReimport();}
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.095f,.105f);
        new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)).GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        var canvas=new GameObject("HomeSelectionCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(TitleViewport));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceCamera;canvas.GetComponent<Canvas>().worldCamera=camera;canvas.GetComponent<Canvas>().planeDistance=1;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var root=R("HomeSelectionScreen",canvas.transform,0,0,1920,1080);root.anchorMin=root.anchorMax=root.pivot=new Vector2(.5f,.5f);root.anchoredPosition=Vector2.zero;
        var viewport=canvas.GetComponent<TitleViewport>();viewport.DesignRoot=root;viewport.FontOverride=font;
        var c=root.gameObject.AddComponent<HomeSelectionController>();c.Roster=AssetDatabase.LoadAssetAtPath<PartyRoster>("Assets/Data/PartyRoster.asset");
        var bg=I("Background",root,0,0,1920,1080,Art("teal-texture"));bg.type=Image.Type.Tiled;bg.color=new Color(.76f,.81f,.81f);
        var heading=I("HomeHeader",root,64,94,395,229,Art("heading-paper"));T("Heading",heading.transform,30,40,340,65,"정착지 선택",46);T("Instruction",heading.transform,34,126,225,95,"처음으로 머물 곳을\n선택하세요.",28);PrefabUtility.SaveAsPrefabAssetAndConnect(heading.gameObject,P+"HomeHeader.prefab",InteractionMode.AutomatedAction);
        var card=I("HomeCandidateCard",null,0,0,330,350,Art("card-paper"));card.raycastTarget=true;var view=card.gameObject.AddComponent<HomeCandidateCard>();view.Button=card.gameObject.AddComponent<Button>();view.Button.targetGraphic=card;
        var colors=view.Button.colors;colors.highlightedColor=new Color(1,.96f,.85f);colors.selectedColor=Color.white;view.Button.colors=colors;
        I("LocationArt",card.transform,16,16,298,132,"Assets/Art/HomeSelection/garage.png");view.Name=T("Name",card.transform,24,156,282,50,"",36);
        string[] labels={"보급품","탄약","휴식 회복"};Text[] values=new Text[3];
        for(int i=0;i<3;i++){T("StatLabel_"+i,card.transform,18+i*104,216,99,32,labels[i],21).alignment=TextAnchor.MiddleCenter;values[i]=T("StatValue_"+i,card.transform,18+i*104,246,99,38,"",30);values[i].alignment=TextAnchor.MiddleCenter;}
        view.Supplies=values[0];view.Ammo=values[1];view.Recovery=values[2];view.Description=T("Description",card.transform,22,286,286,62,"",22);
        view.SelectedBorder=I("SelectedBorder",card.transform,-3,-3,336,356,Art("selected-border")).gameObject;view.Check=I("Check",card.transform,279,-5,51,54,Art("selected-check")).gameObject;
        var cardPrefab=PrefabUtility.SaveAsPrefabAsset(card.gameObject,P+"HomeCandidateCard.prefab");Object.DestroyImmediate(card.gameObject);
        c.Cards=new HomeCandidateCard[3];var sites=new CampaignState().Sites;string[] ids={"garage","house","clinic"};
        for(int i=0;i<3;i++){var go=(GameObject)PrefabUtility.InstantiatePrefab(cardPrefab,root);go.name="Location_"+i;((RectTransform)go.transform).anchoredPosition=new Vector2(490+i*356,-122);c.Cards[i]=go.GetComponent<HomeCandidateCard>();c.Cards[i].Bind(sites[i],false,()=>{});go.transform.Find("LocationArt").GetComponent<Image>().sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/HomeSelection/"+ids[i]+".png");}
        c.Continue=B("StartHere",root,1570,259,290,"이곳에서 시작");c.Continue.GetComponent<Image>().color=new Color(.76f,.86f,.68f);c.Continue.interactable=false;PrefabUtility.SaveAsPrefabAssetAndConnect(c.Continue.gameObject,P+"StartHereButton.prefab",InteractionMode.AutomatedAction);
        c.Hint=T("Hint",root,490,489,1370,50,"처음 머물 정착지를 선택하세요.",30);c.Hint.color=cream;
        var details=I("HomeDetails",root,76,565,1784,327,Art("count-paper"));
        T("PartyLabel",details.transform,42,24,450,45,"함께할 동료",29);c.PartyNames=new Text[2];c.PartyPortraits=new Image[2];
        for(int i=0;i<2;i++){c.PartyPortraits[i]=I("PartyPortrait_"+i,details.transform,42+i*204,90,140,116,null);c.PartyPortraits[i].preserveAspect=true;c.PartyPortraits[i].enabled=false;c.PartyNames[i]=T("PartyName_"+i,details.transform,42+i*204,221,180,45,"동료 미선택",28);}
        c.LocationName=T("LocationName",details.transform,530,38,630,58,"돌아올 곳을 정해주세요",38);c.LocationDescription=T("LocationDescription",details.transform,530,118,620,162,"장소 카드를 누르면 시작 자원과\n정착지의 특징을 확인할 수 있습니다.",30);
        T("ResourceTitle",details.transform,1220,35,510,45,"시작 조건",29);c.Resources=T("Resources",details.transform,1220,103,510,193,"장소마다 시작 보급품과 탄약,\n휴식 시 회복량이 다릅니다.",30);
        foreach(float x in new[]{482f,1174f}){var im=I("Divider",details.transform,x,35,2,247,null);im.color=new Color(.18f,.22f,.18f,.22f);}
        PrefabUtility.SaveAsPrefabAssetAndConnect(details.gameObject,P+"HomeDetails.prefab",InteractionMode.AutomatedAction);
        c.Back=B("BackToParty",root,76,944,370,"모험가 다시 선택");PrefabUtility.SaveAsPrefabAssetAndConnect(c.Back.gameObject,P+"BackButton.prefab",InteractionMode.AutomatedAction);
        PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject,P+"HomeSelectionScreen.prefab",InteractionMode.AutomatedAction);EditorSceneManager.SaveScene(scene,"Assets/Scenes/HomeSelection.unity");
        var scenes=EditorBuildSettings.scenes.ToList();if(!scenes.Any(s=>s.path=="Assets/Scenes/HomeSelection.unity"))scenes.Insert(Math.Min(2,scenes.Count),new EditorBuildSettingsScene("Assets/Scenes/HomeSelection.unity",true));EditorBuildSettings.scenes=scenes.ToArray();
        var party=PrefabUtility.LoadPrefabContents("Assets/Prefabs/PartySelection/PartySelectionScreen.prefab");try{party.GetComponent<PartySelectionController>().DestinationScene="HomeSelection";PrefabUtility.SaveAsPrefabAsset(party,"Assets/Prefabs/PartySelection/PartySelectionScreen.prefab");}finally{PrefabUtility.UnloadPrefabContents(party);}
        AssetDatabase.SaveAssets();return "HomeSelection built with 6 modular prefabs; party -> home selection -> existing settlement connected.";
    }
}
