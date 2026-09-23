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

public static class BuildPartySelection
{
    const string Art="Assets/Art/PartySelection/",Prefabs="Assets/Prefabs/PartySelection/";
    static Font bold,regular;static Color Ink=new Color(.06f,.095f,.095f),Cream=new Color(.87f,.82f,.7f);
    static RectTransform Rect(string name,Transform p,float x,float y,float w,float h){var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(p,false);var r=(RectTransform)g.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
    static Sprite Sprite(string n)=>AssetDatabase.LoadAssetAtPath<Sprite>(Art+n+".png");
    static Image Img(string n,Transform p,float x,float y,float w,float h,string sprite){var r=Rect(n,p,x,y,w,h);var im=r.gameObject.AddComponent<Image>();im.sprite=Sprite(sprite);im.raycastTarget=false;return im;}
    static Text Txt(string n,Transform p,float x,float y,float w,float h,string text,int size,bool strong=false,TextAnchor align=TextAnchor.UpperLeft){var r=Rect(n,p,x,y,w,h);var t=r.gameObject.AddComponent<Text>();t.font=strong?bold:regular;t.fontSize=size;t.text=text;t.color=Ink;t.alignment=align;t.raycastTarget=false;t.verticalOverflow=VerticalWrapMode.Overflow;return t;}
    static Button Footer(string n,Transform root,float x,string label){var im=Img(n,root,x,824,330,74,"footer-paper");im.raycastTarget=true;var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;var colors=b.colors;colors.highlightedColor=new Color(1,.92f,.71f);colors.selectedColor=colors.highlightedColor;colors.disabledColor=new Color(.55f,.55f,.55f,1);b.colors=colors;Txt("Label",im.transform,24,0,282,74,label,36,true,TextAnchor.MiddleCenter);return b;}
    static Button Arrow(string n,Transform root,float x,bool right){var im=Img(n,root,x,456,54,57,"arrow-paper");im.raycastTarget=true;var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;Img("Icon",im.transform,14,11,25,35,right?"icon-right":"icon-left");return b;}
    static GameObject Card()
    {
        var r=Rect("CandidateCard",null,0,0,190,298);var c=r.gameObject.AddComponent<PartyCandidateCard>();var paper=r.gameObject.AddComponent<Image>();paper.sprite=Sprite("card-paper");c.Button=r.gameObject.AddComponent<Button>();c.Button.targetGraphic=paper;var col=c.Button.colors;col.highlightedColor=new Color(1,.95f,.82f);col.selectedColor=Color.white;col.pressedColor=new Color(.88f,.81f,.68f);c.Button.colors=col;
        c.Portrait=Img("Portrait",r,15,22,160,125,"portrait-scout");c.Portrait.preserveAspect=true;
        c.Name=Txt("Role",r,12,151,166,36,"탐험가 1",32,true,TextAnchor.MiddleCenter);
        Img("BagIcon",r,23,193,25,29,"icon-bag");c.Bag=Txt("BagValue",r,57,188,26,40,"3",32,false,TextAnchor.MiddleCenter);
        Img("HeartIcon",r,109,193,29,28,"icon-heart");c.Health=Txt("HealthValue",r,143,188,26,40,"3",32,false,TextAnchor.MiddleCenter);
        c.Description=Txt("Description",r,20,237,160,58,"새로운 곳을 찾는 데\n능숙하다.",20);c.Description.lineSpacing=.98f;
        c.SelectedBorder=Img("SelectedBorder",r,-3,-3,196,304,"selected-border").gameObject;
        c.Check=Img("SelectionCheck",r,143,-3,48,51,"selected-check").gameObject;
        return r.gameObject;
    }
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Save user scene edits first");
        Directory.CreateDirectory(Art);Directory.CreateDirectory(Prefabs);Directory.CreateDirectory("Assets/Data");
        foreach(var f in Directory.GetFiles("아트/모험가선택-v1/개별-PNG","*.png"))File.Copy(f,Art+Path.GetFileName(f),true);
        AssetDatabase.Refresh();
        foreach(var f in Directory.GetFiles(Art,"*.png")){var t=(TextureImporter)AssetImporter.GetAtPath(f.Replace('\\','/'));t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.alphaIsTransparency=true;t.mipmapEnabled=false;t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=4096;t.SaveAndReimport();}
        bold=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");regular=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");
        var roster=AssetDatabase.LoadAssetAtPath<PartyRoster>("Assets/Data/PartyRoster.asset");
        if(!roster){roster=ScriptableObject.CreateInstance<PartyRoster>();AssetDatabase.CreateAsset(roster,"Assets/Data/PartyRoster.asset");
            string[] ids={"scout","mechanic","medic","cook","researcher","guard"},names={"탐험가 1","정비공","의무관","요리사","연구자","경비원"},traits={"새로운 곳을 찾는 데\n능숙하다.","망가진 것을\n고칠 수 있다.","부상을 돌보고\n지켜본다.","식재료를 조금 더\n활용한다.","새로운 정보를\n기록한다.","주변을 경계하고\n지킨다."};int[] bags={3,4,3,4,3,4},health={3,3,4,4,3,4};
            roster.Candidates=Enumerable.Range(0,6).Select(i=>new PartyCandidate{Id=ids[i],DisplayName=names[i],Description=traits[i],BagCapacity=bags[i],Health=health[i],Portrait=Sprite("portrait-"+ids[i])}).ToArray();EditorUtility.SetDirty(roster);}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var cam=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();cam.tag="MainCamera";cam.orthographic=true;cam.transform.position=new Vector3(0,0,-10);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.055f,.095f,.105f);
        new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)).GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        var canvas=new GameObject("SelectionCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(TitleViewport));var cv=canvas.GetComponent<Canvas>();cv.renderMode=RenderMode.ScreenSpaceCamera;cv.worldCamera=cam;cv.planeDistance=1;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var root=Rect("PartySelectionScreen",canvas.transform,0,0,1920,1080);root.anchorMin=root.anchorMax=root.pivot=new Vector2(.5f,.5f);root.anchoredPosition=Vector2.zero;var view=canvas.GetComponent<TitleViewport>();view.DesignRoot=root;view.FontOverride=regular;
        var c=root.gameObject.AddComponent<PartySelectionController>();c.Roster=roster;
        var bg=Img("TealPaperBackground",root,0,0,1920,1080,"teal-texture");bg.type=Image.Type.Tiled;bg.color=new Color(.76f,.81f,.81f);
        var group=Rect("SelectionHeader",root,64,304,440,364);Img("HeadingPaper",group,0,0,440,229,"heading-paper");
        var badge=Img("StepPaper",group,-20,-18,100,93,"footer-paper");badge.color=new Color(.64f,.35f,.24f);badge.transform.localEulerAngles=new Vector3(0,0,11);
        Txt("Step",badge.transform,0,0,100,93,"02",54,true,TextAnchor.MiddleCenter);
        Txt("Heading",group,85,42,345,67,"함께할 두 사람",46,true);
        Txt("Instruction",group,38,132,233,93,"이 여정을 함께할\n동료를 선택하세요.",28);
        var counter=Img("SelectionSummary",group,12,254,302,110,"count-paper");Img("PartyIcon",counter.transform,24,27,52,48,"icon-party");Txt("SummaryLabel",counter.transform,99,11,190,42,"2명 선택",34,true);c.SelectionCount=Txt("SelectionCount",counter.transform,99,53,190,47,"0 / 2",36,false,TextAnchor.MiddleLeft);
        PrefabUtility.SaveAsPrefabAssetAndConnect(group.gameObject,Prefabs+"SelectionHeader.prefab",InteractionMode.AutomatedAction);
        var card=Card();var cardPrefab=PrefabUtility.SaveAsPrefabAsset(card,Prefabs+"CandidateCard.prefab");Object.DestroyImmediate(card);c.Cards=new PartyCandidateCard[6];
        for(int i=0;i<6;i++){var go=(GameObject)PrefabUtility.InstantiatePrefab(cardPrefab,root);go.name="Candidate_"+i;var r=(RectTransform)go.transform;r.anchoredPosition=new Vector2(570+i*201,-332);c.Cards[i]=go.GetComponent<PartyCandidateCard>();var d=roster.Candidates[i];c.Cards[i].Portrait.sprite=d.Portrait;c.Cards[i].Name.text=d.DisplayName;c.Cards[i].Bag.text=d.BagCapacity.ToString();c.Cards[i].Health.text=d.Health.ToString();c.Cards[i].Description.text=d.Description;c.Cards[i].SelectedBorder.SetActive(false);c.Cards[i].Check.SetActive(false);}
        c.Previous=Arrow("PreviousPage",root,497,false);c.NextPage=Arrow("NextPage",root,1797,true);
        c.PageNumber=Txt("PageNumber",root,1080,669,200,38,"",26,false,TextAnchor.MiddleCenter);c.PageNumber.color=Cream;
        c.Message=Txt("SelectionHint",root,570,690,1210,58,"함께할 두 사람을 선택하세요.",30);c.Message.color=Cream;
        c.Back=Footer("Back",root,76,"돌아가기");c.Continue=Footer("Continue",root,1450,"다음  ›");c.Continue.GetComponent<Image>().color=new Color(.76f,.86f,.68f);c.Continue.interactable=false;
        PrefabUtility.SaveAsPrefabAssetAndConnect(c.Back.gameObject,Prefabs+"NavigationButton.prefab",InteractionMode.AutomatedAction);
        PrefabUtility.SaveAsPrefabAssetAndConnect(c.Continue.gameObject,Prefabs+"ContinueButton.prefab",InteractionMode.AutomatedAction);
        PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject,Prefabs+"PartySelectionScreen.prefab",InteractionMode.AutomatedAction);
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/PartySelection.unity");
        var scenes=EditorBuildSettings.scenes.ToList();if(!scenes.Any(s=>s.path=="Assets/Scenes/PartySelection.unity"))scenes.Insert(1,new EditorBuildSettingsScene("Assets/Scenes/PartySelection.unity",true));EditorBuildSettings.scenes=scenes.ToArray();
        var title=PrefabUtility.LoadPrefabContents("Assets/Prefabs/FrontEnd/TitleMenu.prefab");try{title.GetComponent<TitleMenuController>().NewGameScene="PartySelection";PrefabUtility.SaveAsPrefabAsset(title,"Assets/Prefabs/FrontEnd/TitleMenu.prefab");}finally{PrefabUtility.UnloadPrefabContents(title);}
        AssetDatabase.SaveAssets();return "PartySelection scene, roster asset, and 5 editable prefabs created. New Game now opens this screen.";
    }
}
