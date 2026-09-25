using System;
using System.Linq;
using System.Collections.Generic;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Demo5.FrontEnd
{
    [Serializable] public sealed class SavedVisitor
    {
        public int VisitDay;
        public bool Dismissed, Recruited;
        public string RecruitId;
        public SavedCount[] Stock=Array.Empty<SavedCount>();
    }

    // Schedule and values are provisional content, editable on the prefab.
    public sealed partial class SettlementVisitorPanel:MonoBehaviour
    {
        [Serializable] public sealed class Good { public string Id; public int Value=1, Initial; }
        public Good[] Goods;
        public int FirstDay=1, IntervalDays=2, ArrivalMinute=720, DepartureMinute=1080;
        public GameObject View, Conversation, Trade, Review;
        public CanvasGroup Workspace;
        public Button OpenButton, Back, StartTrade, Dismiss, TradeBack, Offer, Cancel, Confirm;
        public Button GiveMinus, GivePlus, TakeMinus, TakePlus, PreviousMember, NextMember;
        public Text OpenLabel, Dialogue, Status, GiveDetails, TakeDetails, GiveQuantity, TakeQuantity, Balance, MemberName, ConfirmBody;
        public Image MemberPortrait;
        public InventorySlot[] OurSlots, TheirSlots;
        SettlementController owner;
        SavedVisitor state=new SavedVisitor();
        Dictionary<string,int> stock=new Dictionary<string,int>();
        int[] ourVisible,theirVisible;
        GameObject returnFocus;
        Quote pending;
        bool dismissReview;
        int dismissDay;
        public int VisitDay=>state.VisitDay;
        public bool IsPresent=>owner?.Campaign!=null && state.VisitDay==owner.Campaign.Day && !state.Dismissed && owner.Campaign.MinuteOfDay>=ArrivalMinute && owner.Campaign.MinuteOfDay<DepartureMinute;
        public int StockFor(string id)=>stock.TryGetValue(id,out int n)?n:0;
        public void Initialize(SettlementController value)
        {
            owner=value;InitializeRecruitment();View.SetActive(false);Review.SetActive(false);
            OpenButton.onClick.AddListener(Open);Back.onClick.AddListener(Close);
            StartTrade.onClick.AddListener(OpenTrade);TradeBack.onClick.AddListener(ReturnToConversation);
            Dismiss.onClick.AddListener(()=>{if(!IsPresent)return;dismissReview=true;dismissDay=state.VisitDay;pending=null;ShowReview("이번 방문을 마무리할까요?\n다음 방문까지 물물교환을 할 수 없습니다.");});
            Offer.onClick.AddListener(RequestOffer);Cancel.onClick.AddListener(CancelReview);Confirm.onClick.AddListener(Commit);
            ourVisible=new int[OurSlots.Length];theirVisible=new int[TheirSlots.Length];
            for(int i=0;i<OurSlots.Length;i++){int slot=i;OurSlots[i].Button.onClick.AddListener(()=>Select(true,ourVisible[slot]));}
            for(int i=0;i<TheirSlots.Length;i++){int slot=i;TheirSlots[i].Button.onClick.AddListener(()=>Select(false,theirVisible[slot]));}
            InitializeTradePages();
            InitializeTradeCart();
        }
        public void Sync()
        {
            var c=owner?.Campaign;if(c==null)return;
            owner.RefreshCharacterUnlocks();
            int latest=c.Day;
            if(c.MinuteOfDay<ArrivalMinute)latest--;
            if(latest>=FirstDay){latest=FirstDay+(latest-FirstDay)/Math.Max(1,IntervalDays)*Math.Max(1,IntervalDays);
                if(latest>state.VisitDay){ResetCart();state=new SavedVisitor{VisitDay=latest,RecruitId=NextRecruitId()};stock=Goods.ToDictionary(g=>g.Id,g=>g.Initial);}}
            // An empty visit can gain its first eligible candidate when a milestone is
            // completed during that visit. Never replace a person already introduced.
            if(IsPresent&&!state.Recruited&&string.IsNullOrEmpty(state.RecruitId))state.RecruitId=NextRecruitId();
            OpenLabel.text=IsPresent?"방문자 · 머무는 중":"방문자 · 다음 방문";
            if(View.activeSelf)Refresh();
        }
        public SavedVisitor Export(){Sync();return new SavedVisitor{VisitDay=state.VisitDay,Dismissed=state.Dismissed,RecruitId=state.RecruitId,Recruited=state.Recruited,Stock=stock.OrderBy(x=>x.Key).Select(x=>new SavedCount{Id=x.Key,Count=x.Value}).ToArray()};}
        public void Restore(SavedVisitor saved){ResetCart();state=new SavedVisitor{VisitDay=saved.VisitDay,Dismissed=saved.Dismissed,RecruitId=saved.RecruitId,Recruited=saved.Recruited};stock=saved.Stock.ToDictionary(x=>x.Id,x=>x.Count);}
        public void ValidateSaved(SavedVisitor saved,int day,int minute)
        {
            if(saved==null || saved.VisitDay<0 || saved.VisitDay>day || saved.Stock==null)throw new InvalidOperationException("방문 기록이 올바르지 않습니다.");
            if(saved.VisitDay==0){if(saved.Dismissed||saved.Stock.Length!=0)throw new InvalidOperationException("빈 방문 기록이 올바르지 않습니다.");return;}
            if(saved.VisitDay<FirstDay || (saved.VisitDay-FirstDay)%Math.Max(1,IntervalDays)!=0 || (saved.VisitDay==day&&minute<ArrivalMinute) || saved.Stock.Length!=Goods.Length || saved.Stock.Any(x=>x==null||x.Count<0||x.Count>1000000||!Goods.Any(g=>g.Id==x.Id)) || saved.Stock.Select(x=>x.Id).Distinct().Count()!=Goods.Length)throw new InvalidOperationException("방문 재고 또는 날짜가 올바르지 않습니다.");
        }
        public void Open()
        {
            if(owner.Introduction&&!owner.Introduction.Allows(10))return;
            if(owner.Campaign==null||owner.Campaign.Stage!=JourneyStage.Settlement||!owner.Main.interactable)return;
            Sync();returnFocus=EventSystem.current?.currentSelectedGameObject;owner.Main.interactable=false;owner.Main.blocksRaycasts=false;
            ResetCart();pending=null;dismissReview=false;recruitPending=null;RecruitPage.SetActive(false);Review.SetActive(false);Workspace.interactable=true;Workspace.blocksRaycasts=true;Conversation.SetActive(true);Trade.SetActive(false);View.SetActive(true);Refresh();Focus(Back);
        }
        public void Close(){ResetCart();pending=null;dismissReview=false;recruitPending=null;RecruitPage.SetActive(false);Review.SetActive(false);View.SetActive(false);owner.Main.interactable=true;owner.Main.blocksRaycasts=true;EventSystem.current?.SetSelectedGameObject(returnFocus);}
        public void OpenTrade()
        {
            if(!IsPresent||Review.activeSelf)return;ResetCart();ourPage=theirPage=0;
            Conversation.SetActive(false);Trade.SetActive(true);Refresh();Focus(TradeBack);
        }
        public void ReturnToConversation(){ResetCart();pending=null;RecruitPage.SetActive(false);Trade.SetActive(false);Conversation.SetActive(true);Refresh();Focus(Back);}
        void Select(bool ours,int index){if(index>=0&&Goods!=null&&index<Goods.Length)ChangeCartQuantity(ours,Goods[index].Id,1);}
        SettlementCraftPanel.Material Material(string id)=>owner.CraftPanel.Materials.First(m=>m.Id==id);
        string ItemName(string id)=>Material(id).Name;
        public void RequestOffer()
        {
            if(!Trade.activeInHierarchy||Review.activeSelf)return;
            var q=Draft();string reason=Block(q);if(reason!=null){SetTradeMessage(reason);return;}
            pending=q;dismissReview=false;
            ShowReview(ReviewText(q));
            ShowCartReview(q);
        }
        void ShowReview(string body){if(CartReview)CartReview.SetActive(false);ConfirmBody.gameObject.SetActive(true);ConfirmBody.text=body;Workspace.interactable=false;Workspace.blocksRaycasts=false;Review.SetActive(true);Focus(Cancel);}
        public void CancelReview(){pending=null;recruitPending=null;dismissReview=false;Review.SetActive(false);Workspace.interactable=true;Workspace.blocksRaycasts=true;Focus(RecruitPage.activeSelf?RecruitBack:Trade.activeSelf?Offer:Back);}
        public void Commit()
        {
            if(!Review.activeSelf)return;
            var q=pending;bool dismiss=dismissReview;var recruit=recruitPending;int visit=recruitVisit;CancelReview();
            if(recruit!=null){CommitRecruit(recruit,visit);return;}
            if(dismiss){if(IsPresent&&dismissDay==state.VisitDay){state.Dismissed=true;ReturnToConversation();Sync();}return;}
            if(q==null)return;string reason=Block(q);if(reason!=null){Refresh();SetTradeMessage(reason);return;}
            // Resolve everything before the first mutation. All availability, prices,
            // reservation and capacity checks cover the complete immutable quote.
            var giving=q.Give.Select(x=>new {Line=x,Material=Material(x.Id),Stock=StockFor(x.Id)}).ToArray();
            var taking=q.Take.Select(x=>new {Line=x,Material=Material(x.Id),Stock=StockFor(x.Id)}).ToArray();
            foreach(var entry in giving){entry.Material.Initial-=entry.Line.Count;stock[entry.Line.Id]=entry.Stock+entry.Line.Count;}
            foreach(var entry in taking){entry.Material.Initial+=entry.Line.Count;stock[entry.Line.Id]=entry.Stock-entry.Line.Count;}
            string summary=TradeSummary(q);
            owner.ActivityLog.Add(owner.Campaign.ClockText.Replace("\n"," ")+" · 물물교환: "+summary);
            ResetCart();Refresh();SetTradeMessage(CompletionText(q));
        }
        void Refresh()
        {
            int next=state.VisitDay==0?FirstDay:state.VisitDay+Math.Max(1,IntervalDays);
            Status.text=IsPresent?"오늘 "+Clock(DepartureMinute)+"까지 · 한정 재고":"다음 방문  DAY "+next+" · "+Clock(ArrivalMinute);
            if(IsPresent&&!string.IsNullOrEmpty(PreferredGoodId))Status.text+="\n"+ItemName(PreferredGoodId)+"를 더 쳐줍니다 · 1개 가치 "+BuyValueFor(PreferredGoodId);
            Dialogue.text=(IsPresent?"?\n물건을 바꾸거나 함께 지낼 이야기를 나눕니다.":"지금은 찾아온 사람이 없습니다.")+"\n\n새 동료 단서\n"+owner.NextCharacterClue();
            StartTrade.interactable=Dismiss.interactable=IsPresent;
            RefreshRecruitment();
            if(!Trade.activeSelf)return;
            BindStock(true,OurSlots,ourVisible);
            BindStock(false,TheirSlots,theirVisible);
            RefreshTradeCart();
        }
        void BindStock(bool ours,InventorySlot[] slots,int[] mapping)
        {
            var visible=VisibleStock(ours);
            int page=Mathf.Clamp(ours?ourPage:theirPage,0,PageCount(visible.Length,slots.Length)-1);
            if(ours)ourPage=page;else theirPage=page;
            UpdateTradePage(ours,page,visible.Length,slots.Length);
            for(int i=0;i<slots.Length;i++){
                int offset=page*slots.Length+i;
                bool show=offset<visible.Length;slots[i].gameObject.SetActive(show);mapping[i]=show?visible[offset]:-1;if(!show)continue;
                int index=visible[offset];var m=Material(Goods[index].Id);
                slots[i].Bind(m.Icon,m.Name,ours?owner.CraftPanel.Available(m.Id):StockFor(m.Id),CartCount(ours,m.Id)>0);
                slots[i].Count.text=Math.Max(0,(ours?owner.CraftPanel.Available(m.Id):StockFor(m.Id))-CartCount(ours,m.Id)).ToString();
                var price=slots[i].transform.Find("UnitValue")?.GetComponent<Text>();
                if(price)price.text=(ours&&m.Id==PreferredGoodId?"선호 ":"가치 ")+(ours?BuyValueFor(m.Id):SellValueFor(m.Id));
                slots[i].Button.interactable=IsPresent;
            }
        }
        static string Clock(int m)=>(m/60).ToString("00")+":"+(m%60).ToString("00");
        static void Focus(Button b){if(b)EventSystem.current?.SetSelectedGameObject(b.gameObject);}
        void Update(){if(View.activeSelf&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){if(Review.activeSelf)CancelReview();else if(Trade.activeSelf||RecruitPage.activeSelf)ReturnToConversation();else Close();}}
    }
}



