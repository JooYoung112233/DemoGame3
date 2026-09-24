using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace Demo5.FrontEnd
{
    public sealed partial class SettlementController:MonoBehaviour
    {
        public PartyRoster Roster;
        public Text Clock,Location,AmmoCount,PartyCount,NoticeTitle,NoticeBody;
        public Button Bed,Stock,Workbench,Cabinet,Exit,Advance,Journal,PopupClose,Previous,Next;
        public SettlementMemberCard[] Members;
        public Text RosterHeading,RosterPage;
        public GameObject[] StandeeObjects;
        public SpriteRenderer[] StandeeBodies;
        public Sprite ScoutBody,MedicBody;
        public CanvasGroup Main;
        public GameObject Popup;
        public Text PopupTitle,PopupBody;
        public SettlementTimePanel TimePanel;
        public ExpeditionReturnPanel ReturnPanel;
        public readonly System.Collections.Generic.List<string> ActivityLog=new System.Collections.Generic.List<string>();
        public SettlementWorkPanel WorkPanel;
        public SettlementCraftPanel CraftPanel;
        public SettlementCookingPanel CookingPanel;
        public SettlementInventoryPanel InventoryPanel;
        public ExpeditionPlanPanel ExpeditionPanel;
        public ExpeditionPackingPanel PackingPanel;
        public ExpeditionArrivalPanel ArrivalPanel;
        public SettlementGameMenu GameMenu;
        public SettlementVisitorPanel VisitorPanel;
        public SettlementIntroduction Introduction; public SettlementDevelopment Development;
        public SettlementOpeningChapter Opening;
        public CampaignState Campaign{get;private set;}
        int page;GameObject returnFocus;
        public bool IsPopupOpen=>Popup.activeSelf||(Opening&&Opening.IsOpen);
        public void Awake()
        {
            Campaign=PartySelectionSession.Take();if(Campaign!=null&&Campaign.Stage!=JourneyStage.Settlement)Campaign=null;
            if(TimePanel)TimePanel.Initialize(this);
            if(ReturnPanel)ReturnPanel.Initialize(this);
            if(Campaign!=null)Campaign.TimeAdvanced+=OnTimeAdvanced;
            if(WorkPanel)WorkPanel.Initialize(this);
            if(CraftPanel)CraftPanel.Initialize(this);
            if(CookingPanel)CookingPanel.Initialize(this);
            if(InventoryPanel)InventoryPanel.Initialize(this);
            if(ExpeditionPanel)ExpeditionPanel.Initialize(this);
            if(PackingPanel)PackingPanel.Initialize(this);
            if(ArrivalPanel)ArrivalPanel.Initialize(this);
            if(VisitorPanel)VisitorPanel.Initialize(this);
            if(Development)Development.Initialize(this); if(Opening)Opening.Initialize(this,CampaignPersistence.Pending!=null); if(Introduction)Introduction.Initialize(this,CampaignPersistence.Pending!=null);
            CampaignPersistence.ApplyPending(this);
            if(VisitorPanel)VisitorPanel.Sync();
            if(GameMenu)GameMenu.Initialize(this);
            Bed.onClick.AddListener(()=>{if(WorkPanel)WorkPanel.Open();else Show("침대","잠시 몸을 눕힐 수 있는 자리입니다.\n\n현재 쉬고 있는 동료는 없습니다.");});
            Stock.onClick.AddListener(()=>{if(CookingPanel)CookingPanel.Open();else if(InventoryPanel)InventoryPanel.Open();else Show("비축 물자",StockText());});Cabinet.onClick.AddListener(()=>{if(InventoryPanel)InventoryPanel.Open();else Show("공용 보관함",StockText());});
            Workbench.onClick.AddListener(()=>{if(CraftPanel)CraftPanel.Open();else Show("작업대","도구와 재료를 펼칠 수 있는 공간입니다.\n\n현재 진행 중인 작업은 없습니다.");});
            Exit.onClick.AddListener(()=>{if(ExpeditionPanel)ExpeditionPanel.Open();else Show("출입구","이곳에서 외부 탐색을 준비합니다.\n\n나서기 전 동료의 상태와 비축 물자를 확인하세요.");});
            Advance.onClick.AddListener(()=>{if(TimePanel)TimePanel.Open();});
            Journal.onClick.AddListener(()=>{if(Opening&&Opening.RecordsView&&Campaign!=null){Opening.OpenRecords();return;}if(ReturnPanel&&ReturnPanel.HasReport){ReturnPanel.Open();return;}Show("기록",Campaign==null?"아직 정착한 기록이 없습니다.":"DAY "+Campaign.Day+"\n"+Campaign.Home.Name+"에 도착했다.\n\n"+string.Join(" · ",Campaign.Party.Select(p=>p.Name))+"\n두 사람이 머물 곳을 마련했다.");});
            PopupClose.onClick.AddListener(Close);Previous.onClick.AddListener(()=>{page=Mathf.Max(0,page-1);RefreshMembers();});Next.onClick.AddListener(()=>{page++;RefreshMembers();});
            Popup.SetActive(false);Clock.text=Campaign?.ClockText??"DAY 1\n09:00";Location.text=Campaign?.Home.Name??"정착지";
            RefreshResources();
            NoticeTitle.text=Campaign==null?"동료 선택 필요":"정착 완료";NoticeBody.text=Campaign==null?"모험가 선택부터 시작해주세요.":"시설을 눌러 정착지를 살펴보세요.";
            RefreshMembers();
            var ids=PartySelectionSession.Selected;
            for(int i=0;i<StandeeObjects.Length;i++){
                bool visible=Campaign!=null&&i<Campaign.Party.Count();StandeeObjects[i].SetActive(visible);
                if(visible)Roster.ApplyBody(StandeeBodies[i],ids.ElementAtOrDefault(i),ScoutBody,MedicBody);
            }
        }
        void OnDestroy(){if(Campaign!=null)Campaign.TimeAdvanced-=OnTimeAdvanced;}
        void OnTimeAdvanced(int minutes){
            var done=new System.Collections.Generic.List<string>();if(WorkPanel)done.AddRange(WorkPanel.AdvanceTime(minutes));if(CraftPanel)done.AddRange(CraftPanel.AdvanceTime(minutes));if(CookingPanel)done.AddRange(CookingPanel.AdvanceTime(minutes));
            foreach(var entry in done)ActivityLog.Add(Campaign.ClockText.Replace("\n"," ")+" · "+entry);
            Clock.text=Campaign.ClockText;RefreshMembers();if(VisitorPanel)VisitorPanel.Sync();
            if(done.Count>0){NoticeTitle.text="작업 완료 · "+done.Count+"건";NoticeBody.text=done[0];}
        }
        public void RefreshResources(){if(AmmoCount)AmmoCount.text=(Campaign?.Ammo??0).ToString();if(PartyCount)PartyCount.text=(Campaign?.Party.Count()??0).ToString();}
        string StockText()=>"탄약  "+(Campaign?.Ammo??0)+"\n\n동료들이 함께 사용하는 물자입니다.";
        public bool IsAssigned(Adventurer member)=>(WorkPanel&&WorkPanel.Orders.Any(o=>o.Member==member))||(CraftPanel&&CraftPanel.IsAssigned(member))||(CookingPanel&&CookingPanel.IsAssigned(member));
        public void RefreshMembers()
        {
            RefreshStandees();
            if(CraftPanel)CraftPanel.RefreshHousing();
            var people=Campaign?.Party.ToArray()??new Adventurer[0];page=Mathf.Clamp(page,0,Mathf.Max(0,(people.Length-1)/Members.Length));
            for(int i=0;i<Members.Length;i++){
                int index=page*Members.Length+i;Members[i].gameObject.SetActive(index<people.Length);if(index>=people.Length)continue;
                var member=people[index];var id=PartySelectionSession.Selected.ElementAtOrDefault(index);var data=Roster.Candidates.FirstOrDefault(c=>c.Id==id);
                Members[i].Bind(member,data?.Portrait,()=>Show(member.Name,"체력  "+member.Health+" / "+member.MaxHealth+"\n상태  "+(WorkPanel?WorkPanel.StateFor(member):"대기")+"\n\n"+(data?.TraitTitle??member.Role)+"\n"+(data?.TraitDescription??member.Description)+"\n\n"+(data?.Characteristics??"")+"\n“"+(data?.FirstLine??"")+"”"),()=>{if(InventoryPanel)InventoryPanel.Open(index);else Show(member.Name+" · 개인 가방","가방 용량  "+member.BagCapacity+"칸\n\n현재 지급된 물자는 공용 보관함에 있습니다.");});
                if(IsAssigned(member))Members[i].Status.text="예약";
            }
            bool paged=people.Length>Members.Length;
            Previous.gameObject.SetActive(paged);Next.gameObject.SetActive(paged);Previous.interactable=page>0;Next.interactable=(page+1)*Members.Length<people.Length;
            if(PartyCount)PartyCount.text=people.Length.ToString();
            if(RosterHeading){RosterHeading.text="정착민 · "+people.Length+" / "+(CraftPanel?CraftPanel.ResidentCapacity:3)+"명";RosterHeading.alignment=paged?TextAnchor.MiddleLeft:TextAnchor.MiddleRight;}
            if(RosterPage){RosterPage.gameObject.SetActive(paged);RosterPage.text=(page*Members.Length+1)+"–"+Mathf.Min(people.Length,(page+1)*Members.Length)+" / "+people.Length+"명";}
            if(Members.Length>0){var layout=Members[0].transform.parent.GetComponent<HorizontalLayoutGroup>();if(layout)layout.childAlignment=paged?TextAnchor.MiddleLeft:TextAnchor.MiddleRight;}
        }
        public void Show(string title,string body){if(IsPopupOpen)return;returnFocus=EventSystem.current?.currentSelectedGameObject;PopupTitle.text=title;PopupBody.text=body;Main.interactable=false;Main.blocksRaycasts=false;Popup.SetActive(true);EventSystem.current?.SetSelectedGameObject(PopupClose.gameObject);}
        public void Close(){Popup.SetActive(false);Main.interactable=true;Main.blocksRaycasts=true;EventSystem.current?.SetSelectedGameObject(returnFocus);}
        void Update(){if(IsPopupOpen&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){if(Opening&&Opening.IsOpen)Opening.Close();else Close();}}
    }
}

