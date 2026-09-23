using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object = UnityEngine.Object;

public static class BuildFieldBattle
{
    const string P = "Assets/Prefabs/Settlement/", A = "Assets/Art/PartySelection/";
    static Font font;
    static Color Ink = new Color(.045f,.065f,.06f), Cream = new Color(.95f,.92f,.82f);
    static RectTransform R(string n, Transform p, float x, float y, float w, float h)
    {
        var r = new GameObject(n, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(p,false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0,1); r.anchoredPosition = new Vector2(x,-y); r.sizeDelta = new Vector2(w,h); return r;
    }
    static Image I(string n, Transform p, float x,float y,float w,float h,string path=null)
    {
        var im=R(n,p,x,y,w,h).gameObject.AddComponent<Image>(); if(path!=null)im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path); im.raycastTarget=false; return im;
    }
    static Text T(string n, Transform p,float x,float y,float w,float h,string s,int size=28,bool light=false)
    {
        var t=R(n,p,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.fontStyle=FontStyle.Normal;t.text=s;t.color=light?Cream:Ink;
        t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;
        while(t.fontSize>16&&t.preferredHeight>h+.5f)t.fontSize--;
        return t;
    }
    static Button B(string n,Transform p,float x,float y,float w,float h,string s,int size=28)
    {
        var im=I(n,p,x,y,w,h,A+"footer-paper.png");im.raycastTarget=true;var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;
        T("Label",im.transform,12,0,w-24,h,s,size).alignment=TextAnchor.MiddleCenter;return b;
    }
    static Image Bar(string n,Transform p,float x,float y,float w,float h,Color c)
    {
        I(n+"Background",p,x,y,w,h).color=new Color(.025f,.04f,.035f,.95f);
        var im=I(n,p,x+2,y+2,w-4,h-4,A+"paper-texture.png");im.type=Image.Type.Filled;im.fillMethod=Image.FillMethod.Horizontal;im.color=c;return im;
    }
    static GameObject Save(GameObject root,string n){var asset=PrefabUtility.SaveAsPrefabAsset(root,P+n+".prefab");Object.DestroyImmediate(root);return asset;}
    static GameObject Instance(GameObject asset,Transform p,float x,float y)
    {
        var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,p);((RectTransform)g.transform).anchoredPosition=new Vector2(x,-y);return g;
    }
    static void PaperPanel(Transform p,float w,float h)
    {
        I("Border",p,0,0,w,h,A+"card-paper.png").color=new Color(.34f,.38f,.33f);
        I("DarkPaper",p,3,3,w-6,h-6,A+"teal-texture.png").color=new Color(.85f,.9f,.9f);
    }
    static Vector2 Vertex(int side,int col,int row)
    {
        var p=ExpeditionBattlePanel.BoardVertex(side,col,row);
        return new Vector2(p.x,-p.y);
    }
    static ExpeditionBattlePanel.PawnBounds InkBounds(string path)
    {
        var tex=new Texture2D(2,2);tex.LoadImage(File.ReadAllBytes(path));var pixels=tex.GetPixels32();
        int left=tex.width,bottom=tex.height,right=0,top=0;
        for(int y=0;y<tex.height;y++)for(int x=0;x<tex.width;x++)if(pixels[y*tex.width+x].a>32){left=Math.Min(left,x);right=Math.Max(right,x+1);bottom=Math.Min(bottom,y);top=Math.Max(top,y+1);}
        Object.DestroyImmediate(tex);
        return new ExpeditionBattlePanel.PawnBounds{Sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path),Pixels=new Rect(left,bottom,right-left,top-bottom)};
    }
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop first");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene");
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");
        var turn=R("BattleTurnCard",null,0,0,96,108);var tc=turn.gameObject.AddComponent<BattleTurnCard>();
        tc.Paper=I("Paper",turn,0,0,96,108,A+"card-paper.png");tc.Portrait=I("Portrait",turn,16,7,64,63,A+"portrait-scout.png");tc.Portrait.preserveAspect=true;
        tc.Label=T("Name",turn,4,70,88,33,"탐험가 1",17);tc.Label.alignment=TextAnchor.MiddleCenter;
        var turnAsset=Save(turn.gameObject,"BattleTurnCard");

        var unit=R("BattleUnitDetails",null,0,0,582,250);PaperPanel(unit,582,250);
        I("HeadingPaper",unit,0,-56,280,52,A+"footer-paper.png");T("Name",unit,20,-54,240,48,"탐험가 1",31);
        I("PortraitPaper",unit,20,20,150,185,A+"card-paper.png");I("Portrait",unit,35,31,120,160,A+"portrait-scout.png").preserveAspect=true;
        T("Info",unit,196,20,354,142,"체력  3 / 3\n가방  0 / 3\n휴대 탄약  0",27,true);
        Bar("Health",unit,196,166,354,15,new Color(.44f,.67f,.45f));
        var unitAsset=Save(unit.gameObject,"BattleUnitDetails");

        var target=R("BattleTargetDetails",null,0,0,582,250);PaperPanel(target,582,250);
        I("HeadingPaper",target,0,-56,280,52,A+"footer-paper.png");T("Title",target,20,-54,240,48,"대상 정보",31);
        I("PortraitPaper",target,20,20,150,185,A+"card-paper.png");I("Portrait",target,42,35,106,155,"Assets/Art/Tokens/infected-body.png").preserveAspect=true;
        T("Name",target,196,20,354,44,"감염자 1",28,true);
        T("Info",target,196,70,354,84,"체력  3 / 3\n예상 피해  2",25,true);
        Bar("Health",target,196,166,354,15,new Color(.76f,.4f,.32f));
        T("Chance",target,196,188,354,51,"명중률  90%",29,true).color=new Color(1,.8f,.42f);
        var targetAsset=Save(target.gameObject,"BattleTargetDetails");

        var action=R("BattleActionCard",null,0,0,235,92);var ab=I("Paper",action,0,0,235,92,A+"card-paper.png");ab.raycastTarget=true;var actionButton=action.gameObject.AddComponent<Button>();actionButton.targetGraphic=ab;
        I("Icon",action,14,16,56,57,"Assets/Art/CraftWorkPanel/prybar.png").preserveAspect=true;
        T("Label",action,82,5,142,41,"근접",27);T("Description",action,82,48,142,34,"전열 · 피해 2",18);
        var actionAsset=Save(action.gameObject,"BattleActionCard");

        var result=R("BattleResultPanel",null,0,0,1920,1080);var dim=I("Dim",result,0,0,1920,1080);dim.color=new Color(0,0,0,.74f);dim.raycastTarget=true;
        I("Paper",result,420,208,1080,666,A+"card-paper.png");T("Title",result,476,240,968,76,"주변이 조용해졌습니다",42);
        T("Body",result,478,339,964,366,"전투 1라운드 · 사용 탄약 0\n\n탐험가 1  체력 3 → 3 / 3\n의무관  체력 4 → 4 / 4\n\n교전 처리 · 탐험 1턴 / 소음 +2\n발견물과 수색도를 유지하고 이어갑니다.",27);
        B("Continue",result,478,749,560,79,"수색으로 돌아가기",31);
        var resultAsset=Save(result.gameObject,"BattleResultPanel");

        var review=R("BattleRetreatReview",null,0,0,1920,1080);dim=I("Dim",review,0,0,1920,1080);dim.color=new Color(0,0,0,.74f);dim.raycastTarget=true;
        I("Paper",review,460,260,1000,550,A+"card-paper.png");T("Title",review,510,295,900,66,"교전을 중단할까요?",38);
        T("Body",review,510,383,900,236,"복도로 이동 · 원정대 전체 1턴\n\n현재 체력과 사용한 탄약은 유지됩니다.\n가방과 수색 진행도는 잃지 않습니다.\n이번 시제품의 철수는 반드시 성공합니다.",27);
        B("Cancel",review,510,677,410,80,"계속 교전");B("Confirm",review,1000,677,410,80,"철수 · 1턴");
        var reviewAsset=Save(review.gameObject,"BattleRetreatReview");

        var root=R("ExpeditionBattlePanel",null,0,0,1920,1080);var c=root.gameObject.AddComponent<ExpeditionBattlePanel>();c.View=root.gameObject;c.Frame=root;
        var blocker=I("InputBlocker",root,0,0,1920,1080);blocker.color=Color.clear;blocker.raycastTarget=true;
        var workspace=R("Workspace",root,0,0,1920,1080);c.Workspace=workspace.gameObject.AddComponent<CanvasGroup>();
        var top=I("TopBand",workspace,0,0,1920,143,A+"teal-texture.png");top.color=Color.white;
        I("LocationPaper",workspace,28,20,370,108,A+"card-paper.png");c.Place=T("Location",workspace,54,26,322,97,"폐상가\n1F · 오락실",29);
        I("ClockPaper",workspace,1668,20,222,108,A+"card-paper.png");c.Clock=T("Clock",workspace,1694,26,171,97,"DAY 1\n09:20",29);
        c.Round=T("Round",workspace,440,33,308,64,"현재 차례 · 1라운드",25,true);
        var viewport=R("TurnViewport",workspace,773,18,852,111);viewport.gameObject.AddComponent<RectMask2D>();var bg=viewport.gameObject.AddComponent<Image>();bg.color=Color.clear;
        c.TurnContent=R("Turns",viewport,0,0,852,108);var layout=c.TurnContent.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=12;layout.childControlWidth=layout.childControlHeight=layout.childForceExpandWidth=layout.childForceExpandHeight=false;
        c.TurnContent.gameObject.AddComponent<ContentSizeFitter>().horizontalFit=ContentSizeFitter.FitMode.PreferredSize;
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=c.TurnContent;scroll.horizontal=true;scroll.vertical=false;scroll.movementType=ScrollRect.MovementType.Clamped;c.TurnPrefab=turnAsset.GetComponent<BattleTurnCard>();

        c.Cells=new BattleBoardCell[18];c.CellButtons=new Button[18];
        for(int side=0;side<2;side++)for(int lane=0;lane<3;lane++)for(int depth=0;depth<3;depth++)
        {
            int i=side*9+lane*3+depth, col=side==0?2-depth:depth;
            var cell=R((side==0?"Ally":"Enemy")+"Cell_"+depth+"_"+lane,workspace,0,0,1920,1080).gameObject.AddComponent<BattleBoardCell>();
            cell.TopLeft=Vertex(side,col,lane);cell.TopRight=Vertex(side,col+1,lane);cell.BottomRight=Vertex(side,col+1,lane+1);cell.BottomLeft=Vertex(side,col,lane+1);
            cell.Edge=side==0?new Color(.84f,.86f,.76f,.85f):new Color(.87f,.49f,.4f,.9f);cell.color=Color.clear;cell.raycastTarget=true;
            var button=cell.gameObject.AddComponent<Button>();button.targetGraphic=cell;button.transition=Selectable.Transition.None;c.Cells[i]=cell;c.CellButtons[i]=button;
        }
        I("FeedbackPaper",workspace,376,654,1168,38,A+"footer-paper.png").color=new Color(.08f,.13f,.13f,.94f);
        c.Feedback=T("Feedback",workspace,400,654,1120,38,"빈 칸으로 이동하거나 행동을 선택하세요.",24,true);c.Feedback.alignment=TextAnchor.MiddleCenter;
        var footer=I("BottomBand",workspace,0,738,1920,342,A+"teal-texture.png");footer.color=Color.white;
        var u=Instance(unitAsset,workspace,28,808);c.ActorName=u.transform.Find("Name").GetComponent<Text>();c.ActorInfo=u.transform.Find("Info").GetComponent<Text>();c.ActorPortrait=u.transform.Find("Portrait").GetComponent<Image>();c.ActorHealth=u.transform.Find("Health").GetComponent<Image>();
        var t=Instance(targetAsset,workspace,634,808);c.TargetName=t.transform.Find("Name").GetComponent<Text>();c.TargetInfo=t.transform.Find("Info").GetComponent<Text>();c.Chance=t.transform.Find("Chance").GetComponent<Text>();c.TargetPortrait=t.transform.Find("Portrait").GetComponent<Image>();c.TargetHealth=t.transform.Find("Health").GetComponent<Image>();
        I("ActionHeading",workspace,1240,752,280,52,A+"footer-paper.png");T("ActionTitle",workspace,1260,754,240,48,"행동 선택",31);
        string[] names={"Melee","Shoot","Guard","Items"},labels={"근접","사격","방어","아이템"},descs={"전열 · 피해 2","탄약 1 · 피해 3","다음 피해 -1","다음 화면 예정"};
        string[] icons={"Assets/Art/CraftWorkPanel/prybar.png","Assets/Art/HomeSelection/icon-ammo.png",A+"icon-heart.png",A+"icon-bag.png"};
        var buttons=new Button[4];
        for(int i=0;i<4;i++){var g=Instance(actionAsset,workspace,1240+(i%2)*247,808+(i/2)*102);g.name=names[i];g.transform.Find("Label").GetComponent<Text>().text=labels[i];g.transform.Find("Description").GetComponent<Text>().text=descs[i];g.transform.Find("Icon").GetComponent<Image>().sprite=AssetDatabase.LoadAssetAtPath<Sprite>(icons[i]);buttons[i]=g.GetComponent<Button>();}
        c.Melee=buttons[0];c.Shoot=buttons[1];c.Guard=buttons[2];c.Items=buttons[3];c.Items.interactable=false;
        var guardIcon=c.Guard.transform.Find("Icon");Object.DestroyImmediate(guardIcon.GetComponent<Image>());var shield=guardIcon.gameObject.AddComponent<BattleShieldIcon>();shield.color=Ink;shield.raycastTarget=false;
        c.Retreat=B("Retreat",workspace,1746,808,146,194,"철수",30);c.Retreat.GetComponent<Image>().sprite=AssetDatabase.LoadAssetAtPath<Sprite>(A+"card-paper.png");var rt=c.Retreat.GetComponentInChildren<Text>();rt.rectTransform.anchoredPosition=new Vector2(12,-74);rt.rectTransform.sizeDelta=new Vector2(122,74);
        I("Icon",c.Retreat.transform,49,23,48,48,A+"icon-left.png").preserveAspect=true;T("Cost",c.Retreat.transform,12,140,122,47,"복도 1턴",20).alignment=TextAnchor.MiddleCenter;
        c.Execute=B("Execute",workspace,1240,1014,652,44,"근접 공격 확정",25);c.ExecuteLabel=c.Execute.GetComponentInChildren<Text>();
        c.Hint=T("Hint",workspace,34,698,1852,32,"빈 칸으로 위치 변경 1회 + 행동 1회 · 적 칸을 눌러 대상 선택",21,true);c.Hint.alignment=TextAnchor.MiddleCenter;
        var r=Instance(resultAsset,root,0,0);c.Result=r;c.ResultTitle=r.transform.Find("Title").GetComponent<Text>();c.ResultBody=r.transform.Find("Body").GetComponent<Text>();c.ResultContinue=r.transform.Find("Continue").GetComponent<Button>();c.ResultContinueLabel=c.ResultContinue.GetComponentInChildren<Text>();r.SetActive(false);
        var v=Instance(reviewAsset,root,0,0);c.RetreatReview=v;c.RetreatCancel=v.transform.Find("Cancel").GetComponent<Button>();c.RetreatConfirm=v.transform.Find("Confirm").GetComponent<Button>();v.SetActive(false);
        c.BattleBackground=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Backgrounds/arcade-unlit-v1.png");
        c.VisibleBounds=new[]{InkBounds("Assets/Art/Tokens/infected-body.png"),InkBounds("Assets/Art/Settlement/standee-scout.png"),InkBounds("Assets/Art/Settlement/standee-medic.png")};
        c.EnemyPortrait=c.EnemyBody=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Tokens/infected-body.png");c.PawnPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(P+"FieldPawn.prefab");
        var asset=Save(root.gameObject,"ExpeditionBattlePanel");
        var a=PrefabUtility.LoadPrefabContents(P+"ExpeditionArrivalPanel.prefab");
        try
        {
            var old=a.transform.Find("ExpeditionBattlePanel");if(old)Object.DestroyImmediate(old.gameObject);
            var go=Instance(asset,a.transform,0,0);go.SetActive(false);
            var arrival=a.GetComponent<ExpeditionArrivalPanel>();arrival.Encounter.Battle=go.GetComponent<ExpeditionBattlePanel>();
            arrival.Encounter.Fight.interactable=true;arrival.Encounter.Fight.transform.Find("Description").GetComponent<Text>().text="진형을 펼치고 교전합니다.";
            PrefabUtility.RecordPrefabInstancePropertyModifications(arrival.Encounter);
            PrefabUtility.RecordPrefabInstancePropertyModifications(arrival.Encounter.Fight);
            PrefabUtility.RecordPrefabInstancePropertyModifications(arrival.Encounter.Fight.transform.Find("Description").GetComponent<Text>());
            arrival.Fade.transform.SetAsLastSibling();PrefabUtility.SaveAsPrefabAsset(a,P+"ExpeditionArrivalPanel.prefab");
        }
        finally{PrefabUtility.UnloadPrefabContents(a);}
        AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");
        return "Battle UI, unit/target/action/turn/result/retreat prefabs created. Existing URP field pawn and background reused.";
    }
}
