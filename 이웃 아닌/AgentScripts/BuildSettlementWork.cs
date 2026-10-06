using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class BuildSettlementWork
{
    const string P="Assets/Prefabs/Settlement/",A="Assets/Art/PartySelection/";
    static Font font;
    static RectTransform R(string name,Transform parent,float x,float y,float w,float h){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
    static Image I(string n,Transform p,float x,float y,float w,float h,string path){var im=R(n,p,x,y,w,h).gameObject.AddComponent<Image>();if(path!=null)im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);im.raycastTarget=false;return im;}
    static Text T(string n,Transform p,float x,float y,float w,float h,string value,int size=32,bool light=false){var t=R(n,p,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.fontStyle=FontStyle.Normal;t.fontSize=size;t.text=value;t.color=light?new Color(.96f,.93f,.85f):new Color(.045f,.065f,.06f);t.raycastTarget=false;return t;}
    static Button B(string n,Transform p,float x,float y,float w,float h,string label,int size=34){var im=I(n,p,x,y,w,h,A+"footer-paper.png");im.raycastTarget=true;var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;T("Label",im.transform,8,0,w-16,h,label,size).alignment=TextAnchor.MiddleCenter;return b;}
    static void Edit(string name,Action<GameObject> action){var g=PrefabUtility.LoadPrefabContents(P+name+".prefab");try{action(g);PrefabUtility.SaveAsPrefabAsset(g,P+name+".prefab");}finally{PrefabUtility.UnloadPrefabContents(g);}}
    static void Line(Transform p,float y){I("Rule",p,24,y,612,2,null).color=new Color(.65f,.64f,.55f,.35f);}
    public static string Build(){
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene changes");
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");
        AssetDatabase.Refresh();foreach(var path in Directory.GetFiles("Assets/Art/RestWorkPanel","*.png")){var importer=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();}
        var row=I("WorkAssigneeRow",null,0,0,588,84,A+"footer-paper.png");row.raycastTarget=true;var rc=row.gameObject.AddComponent<SettlementWorkRow>();rc.Paper=row;rc.Button=row.gameObject.AddComponent<Button>();rc.Button.targetGraphic=row;
        rc.Portrait=I("Portrait",row.transform,12,6,67,72,null);rc.Portrait.preserveAspect=true;rc.Name=T("Name",row.transform,94,12,176,58,"동료",32);rc.Name.alignment=TextAnchor.MiddleLeft;
        rc.Condition=T("Condition",row.transform,270,9,151,36,"체력",24);rc.State=T("State",row.transform,424,20,105,48,"대기 중",26);rc.State.alignment=TextAnchor.MiddleCenter;
        var track=I("HealthTrack",row.transform,272,49,140,18,null);track.color=new Color(.13f,.17f,.16f);rc.Health=I("Health",track.transform,2,2,136,14,A+"count-paper.png");rc.Health.type=Image.Type.Filled;rc.Health.fillMethod=Image.FillMethod.Horizontal;rc.Health.color=new Color(.4f,.7f,.47f);
        rc.Check=I("Selected",row.transform,539,24,34,34,A+"selected-check.png");rc.Check.preserveAspect=true;
        var rowPrefab=PrefabUtility.SaveAsPrefabAsset(row.gameObject,P+"WorkAssigneeRow.prefab");Object.DestroyImmediate(row.gameObject);
        var root=R("RestWorkPanel",null,0,0,1920,1080);var c=root.gameObject.AddComponent<SettlementWorkPanel>();c.View=root.gameObject;c.RowPrefab=rowPrefab.GetComponent<SettlementWorkRow>();c.HideWhileOpen=new GameObject[0];
        var blocker=I("InputBlocker",root,0,0,1920,1080,null);blocker.color=new Color(0,0,0,.10f);blocker.raycastTarget=true;
        var panel=I("TaskPaper",root,32,130,660,918,A+"card-paper.png");panel.color=new Color(.11f,.14f,.15f,.98f);var border=panel.gameObject.AddComponent<Outline>();border.effectColor=new Color(.57f,.55f,.45f,.55f);border.effectDistance=new Vector2(1,-1);
        I("HeadingPaper",panel.transform,20,16,350,70,A+"count-paper.png");I("BedIcon",panel.transform,35,35,53,34,"Assets/Art/Settlement/icon-bed.png").preserveAspect=true;T("Heading",panel.transform,107,20,245,67,"침대 · 휴식",42);
        c.CloseButton=B("Close",panel.transform,581,16,58,58,"×",48);c.CloseButton.GetComponent<Image>().color=new Color(.95f,.92f,.85f,.1f);c.CloseButton.transform.Find("Label").GetComponent<Text>().color=new Color(.96f,.93f,.85f);
        T("Description",panel.transform,28,98,608,46,"잠시 몸을 쉬게 할 동료를 고르세요.",29,true);
        c.ShortRest=B("ShortRest",panel.transform,22,155,300,70,"짧은 휴식",36);c.Sleep=B("Sleep",panel.transform,336,155,300,70,"수면",36);c.ShortPaper=c.ShortRest.GetComponent<Image>();c.SleepPaper=c.Sleep.GetComponent<Image>();Line(panel.transform,242);
        I("Icon",c.ShortRest.transform,18,20,42,30,"Assets/Art/Settlement/icon-bed.png").preserveAspect=true;I("Icon",c.Sleep.transform,20,16,40,40,"Assets/Art/RestWorkPanel/icon-sleep.png").preserveAspect=true;
        foreach(var button in new[]{c.ShortRest,c.Sleep}){var label=(RectTransform)button.transform.Find("Label");label.anchoredPosition=new Vector2(67,0);label.sizeDelta=new Vector2(222,70);}
        T("AssigneeHeading",panel.transform,26,258,590,48,"담당자",36,true);
        var viewport=R("RosterViewport",panel.transform,24,311,612,348);viewport.gameObject.AddComponent<RectMask2D>();var viewportImage=viewport.gameObject.AddComponent<Image>();viewportImage.color=new Color(0,0,0,0);viewportImage.raycastTarget=true;
        var content=R("Rows",viewport,0,0,588,348);var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.childControlWidth=false;layout.childControlHeight=false;layout.childForceExpandHeight=false;layout.childForceExpandWidth=false;layout.spacing=8;var fit=content.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;c.Content=content;
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;c.Scroll=scroll;
        var rail=I("ScrollRail",panel.transform,626,311,9,348,null);rail.color=new Color(.85f,.81f,.7f,.16f);var handle=I("Handle",rail.transform,0,0,9,60,A+"footer-paper.png");handle.raycastTarget=true;var sb=rail.gameObject.AddComponent<Scrollbar>();sb.targetGraphic=handle;sb.handleRect=handle.rectTransform;sb.direction=Scrollbar.Direction.BottomToTop;scroll.verticalScrollbar=sb;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        Line(panel.transform,677);T("DurationLabel",panel.transform,32,697,400,46,"예상 시간",34,true);c.Duration=T("Duration",panel.transform,430,697,194,46,"약 30분",34,true);c.Duration.alignment=TextAnchor.MiddleRight;
        I("ClockIcon",panel.transform,31,705,36,36,"Assets/Art/RestWorkPanel/icon-clock.png").preserveAspect=true;((RectTransform)panel.transform.Find("DurationLabel")).anchoredPosition=new Vector2(83,-697);
        Line(panel.transform,753);T("EffectLabel",panel.transform,32,768,250,46,"휴식 종류",34,true);c.Effect=T("Effect",panel.transform,318,768,306,46,"짧은 휴식",34,true);c.Effect.alignment=TextAnchor.MiddleRight;
        c.Confirm=B("StartRest",panel.transform,28,834,604,76,"휴식 시작",44);c.Confirm.GetComponent<Image>().color=new Color(.61f,.83f,.62f);c.ConfirmLabel=c.Confirm.transform.Find("Label").GetComponent<Text>();
        var detail=I("SelectedMember",root,1460,617,420,390,A+"card-paper.png");I("HeadingPaper",detail.transform,-8,-47,232,64,A+"count-paper.png");T("Heading",detail.transform,14,-40,216,56,"선택된 대원",32);
        c.DetailPortrait=I("Portrait",detail.transform,25,45,135,151,null);c.DetailPortrait.preserveAspect=true;c.DetailName=T("Name",detail.transform,178,35,222,59,"담당자 선택",38);c.DetailState=T("State",detail.transform,178,102,210,62,"왼쪽 목록에서 선택하세요.",29);c.DetailHealth=T("Health",detail.transform,178,167,210,46,"",29);I("Rule",detail.transform,24,224,372,2,null).color=new Color(.23f,.26f,.22f,.35f);c.DetailDescription=T("Description",detail.transform,35,256,352,106,"잠시 쉬며\n숨을 돌립니다.",34);c.DetailDescription.alignment=TextAnchor.MiddleCenter;
        var asset=PrefabUtility.SaveAsPrefabAsset(root.gameObject,P+"RestWorkPanel.prefab");Object.DestroyImmediate(root.gameObject);
        Edit("SettlementScreen",g=>{var owner=g.GetComponent<SettlementController>();var old=g.transform.Find("RestWorkPanel");if(old)Object.DestroyImmediate(old.gameObject);var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset,g.transform);owner.WorkPanel=instance.GetComponent<SettlementWorkPanel>();owner.WorkPanel.HideWhileOpen=new[]{g.transform.Find("Main/Roster").gameObject,g.transform.Find("Main/ArrivalNotice").gameObject,g.transform.Find("Main/PreviousRoster").gameObject,g.transform.Find("Main/NextRoster").gameObject};PrefabUtility.RecordPrefabInstancePropertyModifications(owner.WorkPanel);instance.SetActive(false);});
        Edit("FacilityHotspot",g=>{var r=(RectTransform)g.transform.Find("Caption");r.anchorMin=r.anchorMax=new Vector2(.5f,0);r.pivot=new Vector2(.5f,1);r.anchoredPosition=new Vector2(0,-10);});
        // Place each sprite's pivot between its feet so swapping bodies preserves contact.
        foreach(var role in new[]{"scout","medic"}){
            string path="Assets/Art/Settlement/standee-"+role+".png";var tex=new Texture2D(2,2);tex.LoadImage(File.ReadAllBytes(path));var px=tex.GetPixels32();int bottom=tex.height,min=tex.width,max=0;
            for(int y=0;y<tex.height;y++)for(int x=0;x<tex.width;x++)if(px[y*tex.width+x].a>127)bottom=Math.Min(bottom,y);
            for(int y=bottom;y<Math.Min(tex.height,bottom+14);y++)for(int x=0;x<tex.width;x++)if(px[y*tex.width+x].a>127){min=Math.Min(min,x);max=Math.Max(max,x);}
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2((min+max+1)*.5f/tex.width,(bottom+3f)/tex.height);importer.SetTextureSettings(settings);importer.SaveAndReimport();Object.DestroyImmediate(tex);
        }
        Edit("SettlementWorld",g=>{for(int i=0;i<2;i++){var pawn=g.transform.Find("Standee_"+i);pawn.Find("Body").position=pawn.Find("Base").position;}});
        AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Rest panel built from approved UI 05; scrolling actual roster, registration-only flow, aligned captions and foot pivots.";
    }
}
