using System;
using System.IO;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
public static class BuildSettlement
{
    const string P="Assets/Prefabs/Settlement/",A="Assets/Art/Settlement/";
    static Font font;static Color ink=new Color(.06f,.095f,.095f);
    static string Paper=>"Assets/Art/PartySelection/count-paper.png";
    static RectTransform R(string n,Transform p,float x,float y,float w,float h){var g=new GameObject(n,typeof(RectTransform));g.transform.SetParent(p,false);var r=(RectTransform)g.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
    static Image I(string n,Transform p,float x,float y,float w,float h,string path){var im=R(n,p,x,y,w,h).gameObject.AddComponent<Image>();if(path!=null)im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);im.raycastTarget=false;return im;}
    static Text T(string n,Transform p,float x,float y,float w,float h,string value,int size){var t=R(n,p,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.text=value;t.color=ink;t.raycastTarget=false;t.lineSpacing=1.1f;return t;}
    static Button B(string n,Transform p,float x,float y,float w,float h,string value,int size=30){var im=I(n,p,x,y,w,h,"Assets/Art/PartySelection/footer-paper.png");im.raycastTarget=true;var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;var color=b.colors;color.highlightedColor=new Color(1,.86f,.56f);color.selectedColor=color.highlightedColor;b.colors=color;T("Label",im.transform,10,0,w-20,h,value,size).alignment=TextAnchor.MiddleCenter;return b;}
    static void Save(GameObject g,string name)=>PrefabUtility.SaveAsPrefabAssetAndConnect(g,P+name+".prefab",InteractionMode.AutomatedAction);
    static Vector3 W(float x,float y)=>new Vector3((x-960)/100,(540-y)/100,0);
    static SpriteRenderer Sprite(string n,Transform p,string path,Material mat,int order,float x,float y,float width,float height){var g=new GameObject(n,typeof(SpriteRenderer));g.transform.SetParent(p,false);g.transform.position=W(x,y);var sr=g.GetComponent<SpriteRenderer>();sr.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);sr.sharedMaterial=mat;sr.sortingOrder=order;g.transform.localScale=new Vector3(width/100/sr.sprite.bounds.size.x,height/100/sr.sprite.bounds.size.y,1);return sr;}
    static void Light(string n,Transform p,Light2D.LightType type,float x,float y,float intensity,Color color,float radius){var g=new GameObject(n,typeof(Light2D));g.transform.SetParent(p,false);g.transform.position=W(x,y);var l=g.GetComponent<Light2D>();l.lightType=type;l.intensity=intensity;l.color=color;l.pointLightOuterRadius=radius;l.pointLightInnerRadius=.2f;}
    static void EditPrefab(string path,Action<GameObject> edit){var g=PrefabUtility.LoadPrefabContents(path);try{edit(g);PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}}
    public static string RefreshArtwork(){File.Copy("아트/정착지첫화면-v1/개별-PNG/icon-work.png",A+"icon-work.png",true);AssetDatabase.ImportAsset(A+"icon-work.png",ImportAssetOptions.ForceUpdate);return "Workbench source-circle remnants removed.";}
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop first");for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Preserve unsaved scene changes");
        EditPrefab("Assets/Prefabs/HomeSelection/BackButton.prefab",g=>{((RectTransform)g.transform).sizeDelta=new Vector2(410,78);((RectTransform)g.transform.Find("Label")).sizeDelta=new Vector2(330,78);});
        EditPrefab("Assets/Prefabs/HomeSelection/HomeSelectionScreen.prefab",g=>{var c=g.GetComponent<HomeSelectionController>();((RectTransform)c.Back.transform).sizeDelta=new Vector2(410,78);((RectTransform)c.Continue.transform).sizeDelta=new Vector2(410,78);c.DestinationScene="Settlement";});
        Directory.CreateDirectory(P);Directory.CreateDirectory(A);
        foreach(var f in Directory.GetFiles("아트/정착지첫화면-v1/개별-PNG","*.png"))File.Copy(f,A+Path.GetFileName(f),true);
        AssetDatabase.Refresh();foreach(var f in Directory.GetFiles(A,"*.png")){var t=(TextureImporter)AssetImporter.GetAtPath(f.Replace('\\','/'));t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.alphaIsTransparency=true;t.mipmapEnabled=false;t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=4096;t.SaveAndReimport();}
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/FrontEnd/Fonts/Gaegu/Gaegu-Bold.ttf");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=5.4f;camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.07f,.08f);camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)).GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        var canvas=new GameObject("SettlementCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(TitleViewport));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceCamera;canvas.GetComponent<Canvas>().worldCamera=camera;canvas.GetComponent<Canvas>().planeDistance=1;canvas.GetComponent<Canvas>().sortingOrder=2000;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var root=R("SettlementScreen",canvas.transform,0,0,1920,1080);root.anchorMin=root.anchorMax=root.pivot=new Vector2(.5f,.5f);root.anchoredPosition=Vector2.zero;canvas.GetComponent<TitleViewport>().DesignRoot=root;canvas.GetComponent<TitleViewport>().FontOverride=font;
        var c=root.gameObject.AddComponent<SettlementController>();c.Roster=AssetDatabase.LoadAssetAtPath<PartyRoster>("Assets/Data/PartyRoster.asset");
        var main=R("Main",root,0,0,1920,1080);c.Main=main.gameObject.AddComponent<CanvasGroup>();
        var day=I("DayPanel",main,28,24,204,111,Paper);c.Clock=T("Clock",day.transform,30,14,155,90,"DAY 1\n09:00",36);Save(day.gameObject,"DayPanel");
        c.Advance=B("AdvanceTime",main,253,33,220,62,"시간 진행");I("AdvanceIcon",c.Advance.transform,20,18,32,28,A+"icon-advance.png");var adv=(RectTransform)c.Advance.transform.Find("Label");adv.anchoredPosition=new Vector2(60,0);adv.sizeDelta=new Vector2(150,62);Save(c.Advance.gameObject,"AdvanceButton");
        c.Location=T("Location",main,260,106,430,52,"정착지",30);c.Location.color=new Color(.92f,.87f,.76f);
        var resources=I("ResourceHud",main,1410,24,365,78,Paper);string[] paths={"Assets/Art/HomeSelection/icon-supplies.png","Assets/Art/HomeSelection/icon-ammo.png","Assets/Art/PartySelection/icon-party.png"};var vals=new Text[3];for(int i=0;i<3;i++){I("Icon_"+i,resources.transform,20+i*116,24,30,30,paths[i]).preserveAspect=true;vals[i]=T("Value_"+i,resources.transform,63+i*116,13,50,54,"0",32);}c.SupplyCount=vals[0];c.AmmoCount=vals[1];c.PartyCount=vals[2];Save(resources.gameObject,"ResourceHud");
        c.Journal=B("Journal",main,1800,24,88,105,"기록",25);I("Icon",c.Journal.transform,29,12,30,34,A+"icon-journal.png");var jr=(RectTransform)c.Journal.transform.Find("Label");jr.anchoredPosition=new Vector2(8,-50);jr.sizeDelta=new Vector2(72,48);Save(c.Journal.gameObject,"JournalButton");
        var dot=I("FacilityHotspot",null,0,0,70,70,"Assets/Art/PartySelection/arrow-paper.png");dot.raycastTarget=true;var btn=dot.gameObject.AddComponent<Button>();btn.targetGraphic=dot;var colors=btn.colors;colors.highlightedColor=new Color(1,.79f,.36f);colors.selectedColor=colors.highlightedColor;btn.colors=colors;I("Icon",dot.transform,17,17,36,36,A+"icon-bed.png").preserveAspect=true;
        var label=I("Caption",dot.transform,-55,76,180,48,"Assets/Art/PartySelection/footer-paper.png");T("Text",label.transform,4,0,172,48,"침대",25).alignment=TextAnchor.MiddleCenter;dot.gameObject.AddComponent<SettlementHotspot>().Label=label.gameObject;label.gameObject.SetActive(false);
        var hotspot=PrefabUtility.SaveAsPrefabAsset(dot.gameObject,P+"FacilityHotspot.prefab");Object.DestroyImmediate(dot.gameObject);
        string[] names={"침대","비축 물자","작업대","보관함","출입구"},keys={"bed","water","work","storage","exit"};float[] xs={485,860,1190,1500,1775},ys={257,281,258,171,298};var buttons=new Button[5];
        for(int i=0;i<5;i++){var go=(GameObject)PrefabUtility.InstantiatePrefab(hotspot,main);go.name="Facility_"+keys[i];((RectTransform)go.transform).anchoredPosition=new Vector2(xs[i],-ys[i]);go.transform.Find("Icon").GetComponent<Image>().sprite=AssetDatabase.LoadAssetAtPath<Sprite>(A+"icon-"+keys[i]+".png");go.transform.Find("Caption/Text").GetComponent<Text>().text=names[i];buttons[i]=go.GetComponent<Button>();}
        c.Bed=buttons[0];c.Stock=buttons[1];c.Workbench=buttons[2];c.Cabinet=buttons[3];c.Exit=buttons[4];
        var notice=I("ArrivalNotice",main,42,920,555,113,Paper);I("Icon",notice.transform,22,37,38,43,"Assets/Art/HomeSelection/icon-location.png").preserveAspect=true;c.NoticeTitle=T("Title",notice.transform,84,14,445,40,"정착 완료",32);c.NoticeBody=T("Body",notice.transform,84,57,445,42,"시설을 눌러 정착지를 살펴보세요.",25);Save(notice.gameObject,"ArrivalNotice");
        var member=I("MemberCard",null,0,0,194,205,"Assets/Art/PartySelection/card-paper.png");member.raycastTarget=true;var mc=member.gameObject.AddComponent<SettlementMemberCard>();mc.Button=member.gameObject.AddComponent<Button>();mc.Button.targetGraphic=member;mc.Name=T("Name",member.transform,12,14,134,38,"동료",27);
        mc.Portrait=I("Portrait",member.transform,12,62,90,126,null);mc.Portrait.preserveAspect=true;mc.Status=T("Status",member.transform,110,91,80,41,"대기",25);
        var bar=I("HealthTrack",member.transform,110,149,72,14,null);bar.color=new Color(.14f,.18f,.17f);mc.HealthFill=I("Health",bar.transform,2,2,68,10,"Assets/Art/PartySelection/count-paper.png");mc.HealthFill.color=new Color(.44f,.7f,.45f);mc.HealthFill.type=Image.Type.Filled;mc.HealthFill.fillMethod=Image.FillMethod.Horizontal;
        var bag=I("Bag",member.transform,155,17,28,32,"Assets/Art/PartySelection/icon-bag.png");bag.raycastTarget=true;mc.BagButton=bag.gameObject.AddComponent<Button>();mc.BagButton.targetGraphic=bag;
        var memberPrefab=PrefabUtility.SaveAsPrefabAsset(member.gameObject,P+"MemberCard.prefab");Object.DestroyImmediate(member.gameObject);
        var strip=R("Roster",main,1030,827,820,205);var layout=strip.gameObject.AddComponent<HorizontalLayoutGroup>();layout.childAlignment=TextAnchor.MiddleRight;layout.spacing=14;layout.childControlHeight=false;layout.childControlWidth=false;layout.childForceExpandHeight=false;layout.childForceExpandWidth=false;c.Members=new SettlementMemberCard[4];for(int i=0;i<4;i++){var go=(GameObject)PrefabUtility.InstantiatePrefab(memberPrefab,strip);c.Members[i]=go.GetComponent<SettlementMemberCard>();}Save(strip.gameObject,"RosterStrip");
        c.Previous=B("PreviousRoster",main,981,896,40,78,"‹",34);c.Next=B("NextRoster",main,1863,896,40,78,"›",34);
        var popup=R("InformationPopup",root,0,0,1920,1080);var dim=I("Dim",popup,0,0,1920,1080,null);dim.color=new Color(0,0,0,.57f);dim.raycastTarget=true;var sheet=I("Paper",popup,555,260,810,545,"Assets/Art/PartySelection/card-paper.png");c.PopupTitle=T("Title",sheet.transform,48,34,710,68,"施設",42);c.PopupBody=T("Body",sheet.transform,48,117,710,300,"",30);c.PopupClose=B("Close",sheet.transform,468,449,280,64,"닫기");Save(popup.gameObject,"InformationPopup");c.Popup=popup.gameObject;popup.gameObject.SetActive(false);
        var world=new GameObject("SettlementWorld");var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/StandeeSpriteLit.mat");Sprite("Shelter",world.transform,A+"shelter-unlit.png",mat,0,960,540,1920,1080);
        Light("Ambient",world.transform,Light2D.LightType.Global,0,0,.93f,Color.white,1);Light("WallLamp",world.transform,Light2D.LightType.Point,1075,230,.48f,new Color(1,.83f,.58f),2.4f);Light("ExitLamp",world.transform,Light2D.LightType.Point,1783,365,.35f,new Color(1,.86f,.63f),2);
        c.StandeeObjects=new GameObject[2];c.StandeeBodies=new SpriteRenderer[2];c.ScoutBody=AssetDatabase.LoadAssetAtPath<Sprite>(A+"standee-scout.png");c.MedicBody=AssetDatabase.LoadAssetAtPath<Sprite>(A+"standee-medic.png");
        for(int i=0;i<2;i++){var pawn=new GameObject("Standee_"+i);pawn.transform.SetParent(world.transform,false);var bs=Sprite("Base",pawn.transform,A+"base-white.png",mat,10+i*2,815+i*175,668+i*18,100,28);bs.color=i==0?new Color(.74f,.84f,.87f):new Color(.84f,.8f,.63f);c.StandeeBodies[i]=Sprite("Body",pawn.transform,A+(i==0?"standee-scout.png":"standee-medic.png"),mat,11+i*2,815+i*175,568+i*18,112,199);c.StandeeObjects[i]=pawn;}
        var worldPrefab=PrefabUtility.SaveAsPrefabAssetAndConnect(world,"Assets/Prefabs/Settlement/SettlementWorld.prefab",InteractionMode.AutomatedAction);
        // World references stay on the scene instance; keep UI prefab independent.
        var bodies=c.StandeeBodies;var pawns=c.StandeeObjects;c.StandeeBodies=new SpriteRenderer[0];c.StandeeObjects=new GameObject[0];Save(root.gameObject,"SettlementScreen");c.StandeeBodies=bodies;c.StandeeObjects=pawns;PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/Settlement.unity");var scenes=EditorBuildSettings.scenes.ToList();if(!scenes.Any(s=>s.path=="Assets/Scenes/Settlement.unity"))scenes.Insert(Math.Min(3,scenes.Count),new EditorBuildSettingsScene("Assets/Scenes/Settlement.unity",true));EditorBuildSettings.scenes=scenes.ToArray();AssetDatabase.SaveAssets();return "Home footer buttons both 410x78; Settlement first screen with URP lights, actual party/resources, modular UI and facility inspection.";
    }
}
