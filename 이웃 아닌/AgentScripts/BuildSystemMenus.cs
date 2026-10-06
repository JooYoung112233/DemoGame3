using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;

public static class BuildSystemMenus
{
    const string Front="Assets/Prefabs/FrontEnd/", Home="Assets/Prefabs/Settlement/";
    static Font font;
    static Sprite paper,buttonPaper;
    static Color ink=new Color(.06f,.085f,.075f);
    static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
    }
    static void Place(Transform value,float x,float y,float w,float h){var r=(RectTransform)value;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    static Image Image(string name,Transform parent,float x,float y,float w,float h,Sprite sprite,Color color)
    {
        var r=Rect(name,parent,x,y,w,h);var im=r.gameObject.AddComponent<Image>();im.sprite=sprite;im.color=color;return im;
    }
    static Text Text(string name,Transform parent,float x,float y,float w,float h,string value,int size=30,TextAnchor align=TextAnchor.MiddleLeft)
    {
        var r=Rect(name,parent,x,y,w,h);var t=r.gameObject.AddComponent<Text>();t.text=value;t.font=font;t.fontSize=size;t.alignment=align;t.color=ink;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;
    }
    static Button Button(string name,Transform parent,float x,float y,float w,float h,string value)
    {
        var im=Image(name,parent,x,y,w,h,buttonPaper,Color.white);var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;
        Text("Label",im.transform,20,0,w-40,h,value,30,TextAnchor.MiddleCenter);return b;
    }
    static GameObject Root(string name)
    {
        var r=Rect(name,null,0,0,1920,1080);Image("Dim",r,0,0,1920,1080,null,new Color(.015f,.025f,.025f,.86f));return r.gameObject;
    }
    static Text Heading(Transform parent,string label)
    {
        var im=Image("HeadingPaper",parent,80,32,610,72,buttonPaper,Color.white);return Text("Heading",im.transform,24,0,562,72,label,34);
    }
    static GameObject SaveSlots()
    {
        var g=Root("SaveSlotPanel");var c=g.AddComponent<SaveSlotPanel>();
        var work=Rect("Workspace",g.transform,0,0,1920,1080);c.Workspace=work.gameObject.AddComponent<CanvasGroup>();c.Heading=Heading(work,"생활 불러오기");
        c.Slots=new Button[3];c.SlotHeadings=new Text[3];c.SlotDetails=new Text[3];
        for(int i=0;i<3;i++)
        {
            float x=80+i*600;var im=Image("Record"+(i+1),work,x,230,560,500,paper,Color.white);var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;c.Slots[i]=b;
            c.SlotHeadings[i]=Text("Heading",im.transform,36,38,488,66,"기록 "+(i+1)+" · 빈 기록",38);
            Image("Rule",im.transform,36,126,488,2,null,new Color(.16f,.19f,.17f,.3f)).raycastTarget=false;
            c.SlotDetails[i]=Text("Details",im.transform,36,160,488,260,"아직 저장한 생활이 없습니다.",29,TextAnchor.UpperLeft);
        }
        c.Hint=Text("Hint",work,80,806,1760,90,"이어갈 기록을 선택하세요.",28);c.Hint.color=new Color(.92f,.88f,.78f);
        c.Back=Button("Back",work,80,974,550,76,"돌아가기");c.Action=Button("Action",work,1290,974,550,76,"선택한 생활 불러오기");c.ActionLabel=c.Action.GetComponentInChildren<Text>();
        var review=Root("ConfirmOverwriteOrLoad");review.transform.SetParent(g.transform,false);c.Review=review;
        var sheet=Image("Paper",review.transform,450,285,1020,450,paper,Color.white);
        Text("Heading",sheet.transform,48,32,924,66,"기록 확인",38);
        c.ConfirmBody=Text("Body",sheet.transform,48,128,924,130,"선택한 기록을 사용할까요?",30);
        c.Cancel=Button("Cancel",sheet.transform,48,332,410,76,"취소");c.Confirm=Button("Confirm",sheet.transform,562,332,410,76,"확인");review.SetActive(false);
        var asset=PrefabUtility.SaveAsPrefabAsset(g,Front+"SaveSlotPanel.prefab");Object.DestroyImmediate(g);return asset;
    }
    static GameObject Settings()
    {
        var g=PrefabUtility.LoadPrefabContents(Front+"SettingsDialog.prefab");
        try
        {
            var c=g.GetComponent<GameSettingsDialog>()??g.AddComponent<GameSettingsDialog>();
            c.Volume=g.GetComponentInChildren<Slider>(true);c.Fullscreen=g.GetComponentInChildren<Toggle>(true);
            c.VolumeValue=g.GetComponentsInChildren<Text>(true).First(t=>t.name=="VolumeValue");
            c.Apply=g.GetComponentsInChildren<Button>(true).First(b=>b.name=="Apply");c.Cancel=g.GetComponentsInChildren<Button>(true).First(b=>b.name=="Cancel");
            g.GetComponent<Image>().color=new Color(.015f,.025f,.025f,.86f);
            var oldHeading=g.transform.Find("HeadingPaper");if(oldHeading)Object.DestroyImmediate(oldHeading.gameObject);Heading(g.transform,"설정");
            var sheet=g.transform.Find("Paper");Place(sheet,480,230,960,550);sheet.Find("Title").GetComponent<Text>().text="소리와 화면";
            c.Cancel.transform.SetParent(g.transform,false);Place(c.Cancel.transform,80,974,550,76);c.Apply.transform.SetParent(g.transform,false);Place(c.Apply.transform,1290,974,550,76);
            c.Cancel.GetComponentInChildren<Text>().text="돌아가기";c.Apply.GetComponentInChildren<Text>().text="설정 적용";
            foreach(var b in new[]{c.Cancel,c.Apply}){var label=b.GetComponentInChildren<Text>();label.fontSize=30;label.alignment=TextAnchor.MiddleCenter;}
            return PrefabUtility.SaveAsPrefabAsset(g,Front+"SettingsDialog.prefab");
        }
        finally{PrefabUtility.UnloadPrefabContents(g);}
    }
    static GameObject Menu(GameObject slots,GameObject settings)
    {
        var g=Rect("SystemMenu",null,0,0,1920,1080).gameObject;var c=g.AddComponent<SettlementGameMenu>();
        var view=Root("View");view.transform.SetParent(g.transform,false);c.View=view;view.AddComponent<PopupBackgroundHud>();
        var page=Rect("Page",view.transform,0,0,1920,1080);c.Page=page.gameObject;Heading(page,"이웃 아닌 · 메뉴");
        var panel=Image("Paper",page,510,172,900,686,paper,Color.white);Text("Heading",panel.transform,55,30,790,66,"이어가는 생활",40);
        c.Save=Button("Save",panel.transform,85,130,730,90,"생활 저장");c.Load=Button("Load",panel.transform,85,244,730,90,"생활 불러오기");
        c.Settings=Button("Settings",panel.transform,85,358,730,90,"설정");c.Title=Button("Title",panel.transform,85,472,730,90,"시작 화면으로");
        c.Hint=Text("Hint",page,80,866,1760,70,"정착지에서 생활을 저장하고 이어갈 수 있습니다.",27);c.Hint.color=new Color(.92f,.88f,.78f);
        c.Resume=Button("Resume",page,80,974,550,76,"돌아가기");
        var sg=(GameObject)PrefabUtility.InstantiatePrefab(settings,view.transform);c.SettingsDialog=sg.GetComponent<GameSettingsDialog>();sg.SetActive(false);
        var sl=(GameObject)PrefabUtility.InstantiatePrefab(slots,view.transform);c.SavePanel=sl.GetComponent<SaveSlotPanel>();sl.SetActive(false);
        var review=Root("ReturnToTitle");review.transform.SetParent(view.transform,false);c.Review=review;
        var sheet=Image("Paper",review.transform,450,285,1020,450,paper,Color.white);Text("Heading",sheet.transform,48,32,924,66,"시작 화면으로 돌아갈까요?",38);
        Text("Body",sheet.transform,48,128,924,130,"저장하지 않은 진행은 사라집니다.\n현재 생활을 남기려면 돌아가서 저장해 주세요.",30);
        c.Cancel=Button("Cancel",sheet.transform,48,332,410,76,"취소");c.Confirm=Button("Confirm",sheet.transform,562,332,410,76,"시작 화면으로");review.SetActive(false);view.SetActive(false);
        var asset=PrefabUtility.SaveAsPrefabAsset(g,Home+"SystemMenu.prefab");Object.DestroyImmediate(g);return asset;
    }
    public static string Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("현재 씬의 저장되지 않은 변경을 먼저 보존해야 합니다.");
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");
        paper=AssetDatabase.LoadAssetAtPath<GameObject>(Front+"SettingsDialog.prefab").transform.Find("Paper").GetComponent<Image>().sprite;buttonPaper=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PartySelection/footer-paper.png");
        var slots=SaveSlots();var settings=Settings();var menu=Menu(slots,settings);
        var home=PrefabUtility.LoadPrefabContents(Home+"SettlementScreen.prefab");
        try
        {
            var c=home.GetComponent<SettlementController>();if(c.GameMenu)Object.DestroyImmediate(c.GameMenu.gameObject);
            var old=c.Main.transform.Find("SystemMenuButton");if(old)Object.DestroyImmediate(old.gameObject);
            var m=(GameObject)PrefabUtility.InstantiatePrefab(menu,home.transform);c.GameMenu=m.GetComponent<SettlementGameMenu>();
            c.GameMenu.OpenButton=Button("SystemMenuButton",c.Main.transform,1128,26,184,66,"메뉴");
            PrefabUtility.SaveAsPrefabAsset(home,Home+"SettlementScreen.prefab");
        }
        finally{PrefabUtility.UnloadPrefabContents(home);}
        var title=PrefabUtility.LoadPrefabContents(Front+"TitleMenu.prefab");
        try
        {
            var c=title.GetComponent<TitleMenuController>();Object.DestroyImmediate(c.LoadPanel);Object.DestroyImmediate(c.SettingsPanel);
            c.LoadPanel=(GameObject)PrefabUtility.InstantiatePrefab(slots,title.transform);c.SaveSlots=c.LoadPanel.GetComponent<SaveSlotPanel>();c.LoadClose=c.SaveSlots.Back;c.LoadMessage=c.SaveSlots.Hint;
            c.SettingsPanel=(GameObject)PrefabUtility.InstantiatePrefab(settings,title.transform);c.SettingsDialog=c.SettingsPanel.GetComponent<GameSettingsDialog>();
            c.Volume=c.SettingsDialog.Volume;c.Fullscreen=c.SettingsDialog.Fullscreen;c.VolumeValue=c.SettingsDialog.VolumeValue;c.SettingsApply=c.SettingsDialog.Apply;c.SettingsCancel=c.SettingsDialog.Cancel;
            c.SaveCatalog=AssetDatabase.LoadAssetAtPath<GameObject>(Home+"SettlementScreen.prefab").GetComponent<SettlementController>();
            c.LoadPanel.SetActive(false);c.SettingsPanel.SetActive(false);c.ConfirmPanel.transform.SetAsLastSibling();c.Fade.transform.SetAsLastSibling();
            title.transform.Find("TitlePaper/Title").GetComponent<Text>().text="이웃 아닌";
            title.transform.Find("TitlePaper/ProjectLabel").GetComponent<Text>().text="생존 · 탐험";
            var hint=title.GetComponentsInChildren<Text>(true).FirstOrDefault(t=>t.name=="NoSaveHint");if(hint)hint.text="저장한 생활은 계속하기로 이어갑니다.";
            var version=title.transform.Find("Version");if(version)version.GetComponent<Text>().text="이웃 아닌 · 개발 중";
            PrefabUtility.SaveAsPrefabAsset(title,Front+"TitleMenu.prefab");
        }
        finally{PrefabUtility.UnloadPrefabContents(title);}
        PlayerSettings.productName="이웃 아닌";AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/StartMenu.unity");
        return "Created reusable save slots, shared settings and settlement menu; title and product name updated. Existing art reused; no image assets generated.";
    }
}
