using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;

public static class BuildVisitor
{
    const string Home="Assets/Prefabs/Settlement/";
    static Font font;static Sprite paper,button;
    static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
    static Image Image(string name,Transform p,float x,float y,float w,float h,Sprite s,Color c){var im=Rect(name,p,x,y,w,h).gameObject.AddComponent<Image>();im.sprite=s;im.color=c;return im;}
    static Text Text(string name,Transform p,float x,float y,float w,float h,string value,int size=28,TextAnchor align=TextAnchor.MiddleLeft){var t=Rect(name,p,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.text=value;t.fontSize=size;t.color=new Color(.06f,.085f,.075f);t.alignment=align;t.raycastTarget=false;return t;}
    static Button Button(string name,Transform p,float x,float y,float w,float h,string value){var im=Image(name,p,x,y,w,h,button,Color.white);var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;Text("Label",im.transform,16,0,w-32,h,value,28,TextAnchor.MiddleCenter);return b;}
    static Text White(string name,Transform p,float x,float y,float w,float h,string value,int size=28){var t=Text(name,p,x,y,w,h,value,size);t.color=new Color(.92f,.88f,.78f);return t;}
    static void Place(Transform t,float x,float y,float w,float h){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    static InventorySlot Slot(Transform p,int i)
    {
        var obj=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Home+"InventorySlot.prefab"),p);
        obj.name="Item"+i;Place(obj.transform,38+(i%4)*183,84+(i/4)*98,174,90);var s=obj.GetComponent<InventorySlot>();
        s.Paper.color=new Color(.73f,.76f,.71f);Place(s.Icon.transform,10,5,48,44);s.Icon.raycastTarget=false;
        Place(s.Count.transform,80,0,78,44);s.Count.fontSize=26;
        Place(s.Label.transform,8,47,158,40);s.Label.fontSize=21;
        s.Selection.effectColor=new Color(1,.75f,.22f);s.Selection.effectDistance=new Vector2(3,-3);
        PrefabUtility.RecordPrefabInstancePropertyModifications(s.Paper);PrefabUtility.RecordPrefabInstancePropertyModifications(s.Label);
        return s;
    }
    public static string Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Preserve dirty scene first");
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");
        paper=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FrontEnd/SettingsDialog.prefab").transform.Find("Paper").GetComponent<Image>().sprite;
        button=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PartySelection/footer-paper.png");
        var root=Rect("VisitorPanel",null,0,0,1920,1080);var c=root.gameObject.AddComponent<SettlementVisitorPanel>();
        string[] ids={"wood","cloth","food","water","raw-water","can","scrap","rope","nails","bandage","meal","ration","prybar","flashlight"};
        int[] values={1,1,2,1,1,3,1,2,1,3,2,3,6,6};int[] counts={6,6,6,4,4,2,4,2,2,2,0,0,0,0};
        c.Goods=ids.Select((id,i)=>new SettlementVisitorPanel.Good{Id=id,Value=values[i],Initial=counts[i]}).ToArray();
        var view=Rect("View",root,0,0,1920,1080);c.View=view.gameObject;view.gameObject.AddComponent<PopupBackgroundHud>();Image("Dim",view,0,0,1920,1080,null,new Color(.01f,.02f,.02f,.97f));
        var workspace=Rect("Workspace",view,0,0,1920,1080);c.Workspace=workspace.gameObject.AddComponent<CanvasGroup>();
        var heading=Image("Heading",workspace,80,32,610,72,button,Color.white);Text("Title",heading.transform,24,0,560,72,"방문자 · 물물교환",34);
        c.Status=White("VisitStatus",workspace,1050,32,790,72,"방문 시간");c.Status.alignment=TextAnchor.MiddleRight;
        var conversation=Rect("Conversation",workspace,0,0,1920,1080);c.Conversation=conversation.gameObject;
        var sheet=Image("DialoguePaper",conversation,140,190,1050,680,paper,Color.white);c.Dialogue=Text("Dialogue",sheet.transform,72,60,900,280,"?",38);
        c.StartTrade=Button("StartTrade",sheet.transform,72,382,900,90,"가지고 온 물품을 살펴본다");c.Dismiss=Button("Dismiss",sheet.transform,72,500,900,90,"이번 방문을 마무리한다");
        var silhouette=Image("VisitorSilhouette",conversation,1260,240,440,600,AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Settlement/standee-scout.png"),new Color(.28f,.32f,.28f));silhouette.preserveAspect=true;silhouette.raycastTarget=false;
        c.Back=Button("Back",conversation,80,974,550,76,"돌아가기");
        var trade=Rect("Trade",workspace,0,0,1920,1080);c.Trade=trade.gameObject;
        var card=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PartySelection/card-paper.png");
        var left=Image("OurStock",trade,80,150,800,680,card,new Color(.11f,.14f,.15f,.97f));var right=Image("VisitorStock",trade,1040,150,800,680,card,new Color(.11f,.14f,.15f,.97f));
        foreach(var panel in new[]{left,right}){Image("HeaderPaper",panel.transform,20,6,760,68,button,Color.white);Image("DetailPaper",panel.transform,22,484,756,92,button,Color.white);Image("OfferPaper",panel.transform,22,584,756,80,button,Color.white);}
        Text("OurHeading",left.transform,38,18,724,54,"우리 창고 · 예약 재료와 개인 가방 제외",28);Text("TheirHeading",right.transform,38,18,724,54,"방문자 물품 · 이번 방문의 남은 재고",28);
        c.OurSlots=new InventorySlot[ids.Length];c.TheirSlots=new InventorySlot[ids.Length];
        for(int i=0;i<ids.Length;i++){c.OurSlots[i]=Slot(left.transform,i);c.TheirSlots[i]=Slot(right.transform,i);}
        c.GiveDetails=Text("GiveDetails",left.transform,42,492,716,76,"내놓을 물품",25);c.TakeDetails=Text("TakeDetails",right.transform,42,492,716,76,"받을 물품",25);
        Text("Give",left.transform,42,592,220,60,"내놓기",28);Text("Take",right.transform,42,592,220,60,"받기",28);
        c.GiveMinus=Button("GiveMinus",left.transform,280,587,90,64,"−");c.GivePlus=Button("GivePlus",left.transform,646,587,90,64,"+");c.GiveQuantity=Text("GiveQuantity",left.transform,390,587,236,64,"−1",36,TextAnchor.MiddleCenter);
        c.TakeMinus=Button("TakeMinus",right.transform,280,587,90,64,"−");c.TakePlus=Button("TakePlus",right.transform,646,587,90,64,"+");c.TakeQuantity=Text("TakeQuantity",right.transform,390,587,236,64,"+1",36,TextAnchor.MiddleCenter);
        White("ExchangeArrow",trade,886,425,148,120,"⇄",60).alignment=TextAnchor.MiddleCenter;
        var person=Image("Negotiator",trade,80,850,650,96,button,Color.white);
        c.PreviousMember=Button("Previous",person.transform,12,12,64,72,"‹");c.MemberPortrait=Image("Portrait",person.transform,92,8,80,80,null,Color.white);c.MemberPortrait.preserveAspect=true;
        c.MemberName=Text("Member",person.transform,190,8,362,80,"교섭 담당",26);c.NextMember=Button("Next",person.transform,574,12,64,72,"›");
        c.Balance=White("Balance",trade,775,846,1065,102,"교환 조건",26);
        c.TradeBack=Button("TradeBack",trade,80,974,550,76,"방문자에게 돌아가기");c.Offer=Button("Offer",trade,1290,974,550,76,"교환 제안 확인");
        var review=Rect("ConfirmExchange",view,0,0,1920,1080);c.Review=review.gameObject;Image("Dim",review,0,0,1920,1080,null,new Color(0,0,0,.85f));
        var confirm=Image("Paper",review,450,255,1020,530,paper,Color.white);Text("Title",confirm.transform,48,24,924,66,"교환 / 방문 확인",38);
        c.ConfirmBody=Text("Body",confirm.transform,48,104,924,232,"교환 확인",30);
        c.Cancel=Button("Cancel",confirm.transform,48,410,410,76,"취소");c.Confirm=Button("Confirm",confirm.transform,562,410,410,76,"확정");
        review.gameObject.SetActive(false);trade.gameObject.SetActive(false);view.gameObject.SetActive(false);
        var asset=PrefabUtility.SaveAsPrefabAsset(root.gameObject,Home+"VisitorPanel.prefab");Object.DestroyImmediate(root.gameObject);
        var home=PrefabUtility.LoadPrefabContents(Home+"SettlementScreen.prefab");
        try{
            var owner=home.GetComponent<SettlementController>();if(owner.VisitorPanel)Object.DestroyImmediate(owner.VisitorPanel.gameObject);
            var old=owner.Main.transform.Find("VisitorButton");if(old)Object.DestroyImmediate(old.gameObject);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset,home.transform);owner.VisitorPanel=instance.GetComponent<SettlementVisitorPanel>();
            owner.VisitorPanel.OpenButton=Button("VisitorButton",owner.Main.transform,780,26,328,66,"방문자 · 다음 방문");owner.VisitorPanel.OpenLabel=owner.VisitorPanel.OpenButton.GetComponentInChildren<Text>();owner.VisitorPanel.OpenLabel.fontSize=25;
            PrefabUtility.SaveAsPrefabAsset(home,Home+"SettlementScreen.prefab");
        }finally{PrefabUtility.UnloadPrefabContents(home);}
        AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/StartMenu.unity");return "Visitor conversation and barter prefabs connected using existing editable art; provisional schedule and values serialized.";
    }
}


