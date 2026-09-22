using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
namespace Demo5.NightRun
{
    public sealed partial class NightRunView : MonoBehaviour
    {
        public GameObject PanelPrefab,ButtonPrefab,TilePrefab,TokenPrefab,PropPrefab;
        public GameObject FormationTilePrefab,PaperGrainPrefab;
        public WorldStage World;
        public Vector2 ReferenceResolution=new Vector2(1920,1080);
        public RunState State{get;private set;}
        public int Selected{get;private set;}
        public Order SelectedOrder{get;private set;}=Order.Move;
        Font font;RectTransform root;
        GameObject battlePage,overlay;
        Text status,intent,stats,objective,overlayTitle,overlayBody;
        Image lightFill,noiseFill;
        Text bagLabel;
        Button overlayButton,endTurn;
        readonly Button[] cells=new Button[16],cards=new Button[2],orders=new Button[7];
        readonly Text[] cellLabels=new Text[16],cardLabels=new Text[2];
        readonly string[] orderLabels={"이동","탐색","근접","사격","유인","방어","철수"};
        readonly string[] orderDetails={"1 AP","1 AP · 소음 +1","1 AP · 피해 2","2 AP · 피해 3","1 AP · 소음 +1","1 AP · 피해 -1","1 AP · 출구"};
        static readonly Color Cream=Hex("DED2B7"),Ink=Hex("262B27"),Muted=Hex("ADBAAF"),Gold=Hex("DBC281"),Teal=Hex("79ABA0"),Red=Hex("BD7567");
        static readonly Color PanelColor=new Color(.065f,.09f,.085f,.92f),Background=new Color(.03f,.05f,.045f,.72f);
        static Color Hex(string s){ColorUtility.TryParseHtmlString("#"+s,out var c);return c;}
        void Awake()
        {
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Apple SD Gothic Neo","Noto Sans CJK KR","Arial"},28);
            if(font==null)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");Build();Restart();
            var prepared=Demo5.FrontEnd.PartySelectionSession.Take();if(prepared!=null){Campaign=prepared;Refresh();}
        }
        public void Restart(){Visit=null;Campaign=new CampaignState();State=new RunState();Selected=0;SelectedOrder=Order.Move;Refresh();}
        public void Begin(){if(Campaign.Stage!=JourneyStage.Expedition)return;State.Start();AutoSelect();Refresh();}
        public void Select(int index){if(State.Squad[index].Active)Selected=index;Refresh();}
        public void Choose(Order order)
        {
            SelectedOrder=order;
            if(order==Order.Guard||order==Order.Extract){var p=State.Squad[Selected];State.Act(Selected,order,p.X,p.Y);AutoSelect();SelectedOrder=Order.Move;}
            Refresh();
        }
        public void ClickCell(int x,int y){State.Act(Selected,SelectedOrder,x,y);AutoSelect();Refresh();}
        public void FinishTurn(){State.EndTurn();AutoSelect();Refresh();}
        void AutoSelect(){if(!State.Squad[Selected].Active)for(int i=0;i<2;i++)if(State.Squad[i].Active){Selected=i;break;}}
        void Build()
        {
            var canvas=new GameObject("Paper UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.transform.SetParent(transform,false);
            var c=canvas.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=Camera.main;c.planeDistance=1;c.sortingOrder=5000;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=ReferenceResolution;scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            root=canvas.GetComponent<RectTransform>();
            if(FindAnyObjectByType<EventSystem>()==null){var es=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));es.transform.SetParent(transform,false);}
            battlePage=Group(root,"Battle");
            Box(battlePage.transform,"LocationPaper",17,21,340,88,Cream);Label(battlePage.transform,"폐상가",36,26,310,43,32,Ink);
            objective=Label(battlePage.transform,"",36,72,310,30,18,Ink);
            Box(battlePage.transform,"TurnPaper",1600,18,201,93,Cream);stats=Label(battlePage.transform,"",1615,27,176,80,20,Ink);
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)
            {
                int xx=x,yy=y,i=y*4+x;var p=WorldStage.CellPoint(x,y);
                cells[i]=MakeButton(battlePage.transform,"Cell_"+x+"_"+y,p.x-74,p.y-25,148,55,"",()=>ClickCell(xx,yy));
                cellLabels[i]=Label(cells[i].transform,"",0,5,148,46,17,Cream,TextAnchor.MiddleCenter);
            }
            Box(battlePage.transform,"BottomBand",0,734,1920,346,Hex("171D1B"));
            Box(battlePage.transform,"SquadPanel",16,770,704,286,PanelColor);Box(battlePage.transform,"SquadTab",22,758,160,39,Cream);Label(battlePage.transform,"원정대",32,760,140,34,24,Ink);
            for(int i=0;i<2;i++){int id=i;cards[i]=MakeButton(battlePage.transform,"Survivor_"+i,28+i*235,805,219,227,"",()=>Select(id));Portrait(cards[i].transform,i,10,29,80);cardLabels[i]=Label(cards[i].transform,"",13,8,197,209,18,Ink);}
            Label(battlePage.transform,"두 사람이 함께\n돌아오는 것이 목표",510,851,185,140,21,Muted);
            Box(battlePage.transform,"SurvivalPanel",734,770,549,286,PanelColor);Box(battlePage.transform,"SurvivalTab",742,757,167,39,Cream);Label(battlePage.transform,"생존 정보",752,761,148,34,24,Ink);
            Label(battlePage.transform,"빛",767,814,210,30,24,Cream);
            Box(battlePage.transform,"LightTrack",767,850,244,22,Ink);lightFill=Box(battlePage.transform,"LightFill",770,853,238,16,Gold);
            Label(battlePage.transform,"소음",767,894,210,30,24,Cream);
            Box(battlePage.transform,"NoiseTrack",767,930,244,22,Ink);noiseFill=Box(battlePage.transform,"NoiseFill",770,933,238,16,Teal);
            Box(battlePage.transform,"Divider",1030,797,1,234,Muted);
            bagLabel=Label(battlePage.transform,"",1052,797,210,36,22,Cream);
            for(int i=0;i<6;i++)Box(battlePage.transform,"BagSlot_"+i,1052+(i%3)*70,842+(i/3)*72,63,64,Ink);
            intent=Label(battlePage.transform,"",1057,850,194,126,17,Cream);status=Label(battlePage.transform,"",752,980,512,63,17,Muted);
            Box(battlePage.transform,"OrderPanel",1296,770,608,286,PanelColor);Box(battlePage.transform,"EncounterTab",1305,757,173,39,Cream);Label(battlePage.transform,"조우 / 행동",1314,761,157,34,24,Ink);
            Label(battlePage.transform,"주변을 살피고 행동을 선택하자.",1493,774,385,54,18,Cream);
            int[] encounterOrders={5,4,3,6};
            for(int slot=0;slot<4;slot++){int id=encounterOrders[slot];float x=1310+(slot%2)*287,y=839+(slot/2)*101;orders[id]=MakeButton(battlePage.transform,"Order_"+(Order)id,x,y,274,89,orderLabels[id]+"\n"+orderDetails[id],()=>Choose((Order)id));}
            // Traversal commands are distinct from the reference's four encounter cards.
            for(int i=0;i<3;i++){int id=i;orders[i]=MakeButton(battlePage.transform,"Order_"+(Order)i,22+i*155,691,145,36,orderLabels[i],()=>Choose((Order)id));}
            endTurn=MakeButton(battlePage.transform,"EndTurn",1690,691,208,36,"턴 마치기 →",FinishTurn);
            overlay=Box(root,"BriefingAndDebrief",0,0,1920,1080,Background).gameObject;Box(overlay.transform,"MissionPaper",380,220,1160,650,Cream);
            overlayTitle=Label(overlay.transform,"",425,268,1070,66,43,Ink);overlayBody=Label(overlay.transform,"",425,365,1070,360,26,Ink);
            overlayButton=MakeButton(overlay.transform,"Deploy",425,763,1070,68,"출발",()=>{if(State.Phase==RunPhase.Briefing)Begin();else{Campaign.Return();Refresh();}});BuildJourney();BuildVisit();
        }
        void Refresh()
        {
            bool visiting=Campaign.Stage==JourneyStage.Expedition&&Visit!=null;
            visitPage.SetActive(visiting);
            if(visiting){journeyRoot.SetActive(false);battlePage.SetActive(false);overlay.SetActive(false);RefreshVisit();return;}
            World.Show(Campaign,State,Selected,SelectedOrder);bool expedition=Campaign.Stage==JourneyStage.Expedition;
            journeyRoot.SetActive(!expedition);battlePage.SetActive(expedition);
            if(!expedition){overlay.SetActive(false);RefreshJourney();return;}
            bool playing=State.Phase==RunPhase.Expedition;
            objective.text="보급품 "+State.Supplies+"/2 · 귀환 "+State.Squad.Count(p=>p.Extracted)+"/2";
            stats.text="밤 "+Campaign.Day+" · 턴 "+State.Round+"\n빛 "+State.Light+"%";
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)
            {
                int i=y*4+x;bool valid=playing&&State.Validate(Selected,SelectedOrder,x,y).Length==0;
                var enemy=State.EnemyAt(x,y);int cache=RunState.CacheAt(x,y),prop=RunState.PropAt(x,y);
                cells[i].GetComponent<Image>().color=new Color(.1f,.15f,.13f,0);
                string label=cache>=0?(State.Searched[cache]?"수색 완료":"보급품 탐색"):prop>=0?"상자 / 엄폐":RunState.ExitAt(x,y)?"← 철수 지점":"";
                if(enemy!=null&&valid&&(SelectedOrder==Order.Shoot||SelectedOrder==Order.Melee))label="명중 "+State.HitChance(State.Squad[Selected],enemy,SelectedOrder==Order.Shoot)+"%";
                cellLabels[i].text=label;cells[i].interactable=playing;
            }
            for(int i=0;i<2;i++)
            {
                var p=State.Squad[i];cards[i].GetComponent<Image>().color=Selected==i?Gold:Cream;
                cardLabels[i].text=p.Name+" / "+p.Role+"\n\n\n\n"+(p.Extracted?"귀환 완료":p.Health==0?"전투 불능":"체력 "+p.Health+"/"+p.MaxHealth+" · 행동 "+p.Actions+"/2")+"\n"+(p.Guarding?"방어 중":Selected==i?"● 선택됨":"선택하기");cards[i].interactable=playing&&p.Active;
            }
            lightFill.rectTransform.sizeDelta=new Vector2(238*State.Light/100f,16);noiseFill.rectTransform.sizeDelta=new Vector2(238*Mathf.Clamp01(State.Noise/6f),16);
            bagLabel.text="소지품  ·  "+State.Supplies+" / 2";intent.text="탄약    보급품\n  "+State.Ammo+"          "+State.Supplies;status.text=State.LastMessage;
            for(int i=0;i<7;i++){Color cardColor=i==5?Hex("9BAA87"):i==3?Hex("BD8875"):i==6?Hex("B8B6A9"):Cream;orders[i].GetComponent<Image>().color=(int)SelectedOrder==i?Gold:cardColor;orders[i].interactable=playing&&State.Squad[Selected].Active&&State.Squad[Selected].Actions>=(i==3?2:1);}
            endTurn.interactable=playing;overlay.SetActive(!playing);
            if(State.Phase==RunPhase.Briefing){overlayTitle.text="폐상가에서 보급품을 회수하자";overlayBody.text="대원 선택 → 행동 선택 → 바닥의 대상 칸 클릭.\n각 대원은 턴마다 행동력 2를 사용한다.\n\n초록색으로 표시된 칸으로 이동할 수 있다.\n보급품 두 곳을 수색하고 철수 지점으로 돌아오자.\n사격은 확률로 명중한다. 빗나가도 탄약과 소음은 소모된다.\n소음 6 이상이면 증원 발생. 미끼로 길을 만들 수도 있다.";overlayButton.GetComponentInChildren<Text>().text="수색 시작 →";}
            else{overlayTitle.text=State.Result;overlayBody.text="회수한 보급품 "+State.Supplies+" · 남은 탄약 "+State.Ammo+"\n\n"+string.Join("\n",State.Squad.Select(p=>p.Name+"  "+(p.Extracted?"귀환 · 체력 "+p.Health:"미귀환")))+"\n\n정착지로 돌아가면 보급품과 부상이 반영된다.";overlayButton.GetComponentInChildren<Text>().text="정착지로 돌아가기 →";}
        }
        GameObject Group(Transform parent,string name){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);Place(go.GetComponent<RectTransform>(),0,0,1920,1080);return go;}
        GameObject Create(GameObject prefab,Transform parent,string name,float x,float y,float w,float h){var go=prefab!=null?Instantiate(prefab,parent,false):new GameObject(name,typeof(RectTransform));go.name=name;if(prefab==null)go.transform.SetParent(parent,false);Place(go.GetComponent<RectTransform>(),x,y,w,h);return go;}
        void Paper(Transform parent,float w,float h){var go=PaperGrainPrefab!=null?Instantiate(PaperGrainPrefab,parent,false):new GameObject("PaperGrain",typeof(RectTransform),typeof(PaperGrain));if(go.transform.parent==null)go.transform.SetParent(parent,false);Place(go.GetComponent<RectTransform>(),0,0,w,h);go.GetComponent<PaperGrain>().raycastTarget=false;}
        Image Box(Transform parent,string name,float x,float y,float w,float h,Color color){var go=Create(PanelPrefab,parent,name,x,y,w,h);var img=go.GetComponent<Image>()??go.AddComponent<Image>();img.color=color;if(color==Cream||name.EndsWith("Panel"))Paper(go.transform,w,h);return img;}
        Text Label(Transform parent,string text,float x,float y,float w,float h,int size,Color color,TextAnchor alignment=TextAnchor.UpperLeft){var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);Place(go.GetComponent<RectTransform>(),x,y,w,h);var t=go.GetComponent<Text>();t.font=font;t.text=text;t.fontSize=size;t.color=color;t.alignment=alignment;t.raycastTarget=false;t.supportRichText=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;}
        Button MakeButton(Transform parent,string name,float x,float y,float w,float h,string text,UnityEngine.Events.UnityAction callback){var go=Create(ButtonPrefab,parent,name,x,y,w,h);var img=go.GetComponent<Image>()??go.AddComponent<Image>();img.color=Cream;var b=go.GetComponent<Button>()??go.AddComponent<Button>();b.targetGraphic=img;var nav=b.navigation;nav.mode=Navigation.Mode.None;b.navigation=nav;b.onClick.AddListener(callback);if(!name.StartsWith("Cell_")&&!name.StartsWith("Formation_")&&!name.StartsWith("EnemyTarget_"))Paper(go.transform,w,h);if(text.Length>0)Label(go.transform,text,8,2,w-16,h-4,23,Ink,TextAnchor.MiddleCenter);return b;}
        static void Place(RectTransform rt,float x,float y,float w,float h){rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);}
    }
}
