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
        int giveIndex,takeIndex=1,giveQuantity=1,takeQuantity=1,memberIndex;
        int[] ourVisible,theirVisible;
        GameObject returnFocus;
        Quote pending;
        bool dismissReview;
        int dismissDay;
        sealed class Quote { public int Day, Give, Take, GiveValue, TakeValue; public string GiveId,TakeId; public Adventurer Member; }
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
            GiveMinus.onClick.AddListener(()=>Adjust(true,-1));GivePlus.onClick.AddListener(()=>Adjust(true,1));
            TakeMinus.onClick.AddListener(()=>Adjust(false,-1));TakePlus.onClick.AddListener(()=>Adjust(false,1));
            PreviousMember.onClick.AddListener(()=>ChangeMember(-1));NextMember.onClick.AddListener(()=>ChangeMember(1));
            ourVisible=new int[OurSlots.Length];theirVisible=new int[TheirSlots.Length];
            for(int i=0;i<OurSlots.Length;i++){int slot=i;OurSlots[i].Button.onClick.AddListener(()=>Select(true,ourVisible[slot]));}
            for(int i=0;i<TheirSlots.Length;i++){int slot=i;TheirSlots[i].Button.onClick.AddListener(()=>Select(false,theirVisible[slot]));}
        }
        public void Sync()
        {
            var c=owner?.Campaign;if(c==null)return;
            int latest=c.Day;
            if(c.MinuteOfDay<ArrivalMinute)latest--;
            if(latest>=FirstDay){latest=FirstDay+(latest-FirstDay)/Math.Max(1,IntervalDays)*Math.Max(1,IntervalDays);
                if(latest>state.VisitDay){state=new SavedVisitor{VisitDay=latest,RecruitId=NextRecruitId()};stock=Goods.ToDictionary(g=>g.Id,g=>g.Initial);}}
            OpenLabel.text=IsPresent?"방문자 · 머무는 중":"방문자 · 다음 방문";
            if(View.activeSelf)Refresh();
        }
        public SavedVisitor Export(){Sync();return new SavedVisitor{VisitDay=state.VisitDay,Dismissed=state.Dismissed,RecruitId=state.RecruitId,Recruited=state.Recruited,Stock=stock.OrderBy(x=>x.Key).Select(x=>new SavedCount{Id=x.Key,Count=x.Value}).ToArray()};}
        public void Restore(SavedVisitor saved){state=new SavedVisitor{VisitDay=saved.VisitDay,Dismissed=saved.Dismissed,RecruitId=saved.RecruitId,Recruited=saved.Recruited};stock=saved.Stock.ToDictionary(x=>x.Id,x=>x.Count);}
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
            pending=null;recruitPending=null;RecruitPage.SetActive(false);Review.SetActive(false);Workspace.interactable=true;Workspace.blocksRaycasts=true;Conversation.SetActive(true);Trade.SetActive(false);View.SetActive(true);Refresh();Focus(Back);
        }
        public void Close(){pending=null;recruitPending=null;RecruitPage.SetActive(false);Review.SetActive(false);View.SetActive(false);owner.Main.interactable=true;owner.Main.blocksRaycasts=true;EventSystem.current?.SetSelectedGameObject(returnFocus);}
        public void OpenTrade()
        {
            if(!IsPresent)return;giveQuantity=takeQuantity=1;memberIndex=0;
            var party=owner.Campaign.Party.ToArray();int free=Array.FindIndex(party,p=>p.Health>0&&!owner.IsAssigned(p));if(free>=0)memberIndex=free;
            Conversation.SetActive(false);Trade.SetActive(true);Refresh();Focus(TradeBack);
        }
        public void ReturnToConversation(){pending=null;RecruitPage.SetActive(false);Trade.SetActive(false);Conversation.SetActive(true);Refresh();Focus(Back);}
        void Select(bool ours,int index){if(ours){giveIndex=index;giveQuantity=1;}else{takeIndex=index;takeQuantity=1;}Refresh();}
        void Adjust(bool ours,int delta){if(ours)giveQuantity=Mathf.Clamp(giveQuantity+delta,1,99);else takeQuantity=Mathf.Clamp(takeQuantity+delta,1,99);Refresh();}
        void ChangeMember(int delta){int n=owner.Campaign.Party.Count();memberIndex=(memberIndex+delta+n)%n;Refresh();}
        Quote Draft()=>giveIndex<0||takeIndex<0?null:new Quote{Day=state.VisitDay,GiveId=Goods[giveIndex].Id,TakeId=Goods[takeIndex].Id,Give=giveQuantity,Take=takeQuantity,GiveValue=Goods[giveIndex].Value,TakeValue=Goods[takeIndex].Value,Member=owner.Campaign.Party.ElementAt(memberIndex)};
        string Block(Quote q)
        {
            if(q==null)return "교환할 물품이 없습니다.";
            if(owner.Campaign.Stage!=JourneyStage.Settlement||!IsPresent||q.Day!=state.VisitDay)return "방문이 끝났습니다.";
            if(!owner.Campaign.Party.Contains(q.Member)||q.Member.Health<=0||owner.IsAssigned(q.Member))return "대기 중인 건강한 대원이 필요합니다.";
            if(q.GiveId==q.TakeId)return "서로 다른 물품을 골라주세요.";
            if(q.Give<1||q.Give>99||q.Take<1||q.Take>99)return "수량을 확인해주세요.";
            if(owner.CraftPanel.Available(q.GiveId)<q.Give)return "내놓을 물품이 부족합니다. 예약 재료는 제외됩니다.";
            if(StockFor(q.TakeId)<q.Take)return "방문자의 남은 물품이 부족합니다.";
            if(q.GiveValue*q.Give<q.TakeValue*q.Take)return "내놓을 물품의 가치가 부족합니다.";
            if(owner.Development&&q.Take>q.Give&&owner.Development.StockUsed-q.Give+q.Take>owner.Development.Capacity)return "창고가 부족합니다. 가방으로 옮기거나 창고를 확장하세요.";
            if((long)Material(q.TakeId).Initial+q.Take>1000000 || (long)StockFor(q.GiveId)+q.Give>1000000)return "더 보관할 공간이 없습니다.";
            return null;
        }
        SettlementCraftPanel.Material Material(string id)=>owner.CraftPanel.Materials.First(m=>m.Id==id);
        string ItemName(string id)=>Material(id).Name;
        public void RequestOffer()
        {
            var q=Draft();string reason=Block(q);if(reason!=null){Balance.text=reason;return;}
            pending=q;dismissReview=false;
            ShowReview("내놓기  −"+q.Give+" "+ItemName(q.GiveId)+"\n받기  +"+q.Take+" "+ItemName(q.TakeId)+"\n가치 "+q.Give*q.GiveValue+" → "+q.Take*q.TakeValue+" · 담당 "+q.Member.Name+"\n확정하면 공용 창고 물품을 맞바꿉니다.");
        }
        void ShowReview(string body){ConfirmBody.text=body;Workspace.interactable=false;Workspace.blocksRaycasts=false;Review.SetActive(true);Focus(Cancel);}
        public void CancelReview(){pending=null;recruitPending=null;dismissReview=false;Review.SetActive(false);Workspace.interactable=true;Workspace.blocksRaycasts=true;Focus(RecruitPage.activeSelf?RecruitBack:Trade.activeSelf?Offer:Back);}
        public void Commit()
        {
            if(!Review.activeSelf)return;
            var q=pending;bool dismiss=dismissReview;var recruit=recruitPending;int visit=recruitVisit;CancelReview();
            if(recruit!=null){CommitRecruit(recruit,visit);return;}
            if(dismiss){if(IsPresent&&dismissDay==state.VisitDay){state.Dismissed=true;ReturnToConversation();Sync();}return;}
            if(q==null)return;string reason=Block(q);if(reason!=null){Refresh();Balance.text=reason;return;}
            Material(q.GiveId).Initial-=q.Give;Material(q.TakeId).Initial+=q.Take;stock[q.GiveId]=StockFor(q.GiveId)+q.Give;stock[q.TakeId]-=q.Take;
            owner.ActivityLog.Add(owner.Campaign.ClockText.Replace("\n"," ")+" · 물물교환: "+ItemName(q.GiveId)+" −"+q.Give+" / "+ItemName(q.TakeId)+" +"+q.Take);
            Refresh();Balance.text="교환 완료 · "+ItemName(q.GiveId)+" −"+q.Give+" / "+ItemName(q.TakeId)+" +"+q.Take;
        }
        void Refresh()
        {
            int next=state.VisitDay==0?FirstDay:state.VisitDay+Math.Max(1,IntervalDays);
            Status.text=IsPresent?"오늘 "+Clock(DepartureMinute)+"까지 · 한정 재고":"다음 방문  DAY "+next+" · "+Clock(ArrivalMinute);
            Dialogue.text=IsPresent?"?\n\n쓸 만한 물건이 좀 있어.\n서로 필요한 걸 바꾸면 어떨까?":"지금은 찾아온 사람이 없습니다.\n\n방문 시간이 되면 물품을 살펴볼 수 있습니다.";
            StartTrade.interactable=Dismiss.interactable=IsPresent;
            RefreshRecruitment();
            if(!Trade.activeSelf)return;
            BindStock(true,OurSlots,ourVisible,ref giveIndex,ref giveQuantity);
            BindStock(false,TheirSlots,theirVisible,ref takeIndex,ref takeQuantity);
            GiveDetails.text=giveIndex<0?"교환할 수 있는 창고 물품이 없습니다.":ItemName(Goods[giveIndex].Id)+" · 가치 "+Goods[giveIndex].Value+"\n사용 가능 "+owner.CraftPanel.Available(Goods[giveIndex].Id)+"개";
            TakeDetails.text=takeIndex<0?"방문자에게 남은 물품이 없습니다.":ItemName(Goods[takeIndex].Id)+" · 가치 "+Goods[takeIndex].Value+"\n남은 재고 "+StockFor(Goods[takeIndex].Id)+"개";
            GiveQuantity.text=giveIndex<0?"—":"−"+giveQuantity;TakeQuantity.text=takeIndex<0?"—":"+"+takeQuantity;
            GiveMinus.interactable=giveIndex>=0&&giveQuantity>1;TakeMinus.interactable=takeIndex>=0&&takeQuantity>1;GivePlus.interactable=giveIndex>=0&&giveQuantity<99;TakePlus.interactable=takeIndex>=0&&takeQuantity<99;
            var q=Draft();var reason=Block(q);Balance.text=q==null?reason:"내놓기 "+q.Give*q.GiveValue+"  →  받기 "+q.Take*q.TakeValue+"\n"+(reason??"교환 가능 · 확정 전에는 물품이 이동하지 않습니다.");Offer.interactable=reason==null;
            var member=owner.Campaign.Party.ElementAt(memberIndex);
            MemberName.text=member.Name+" · "+(member.Health<=0?"행동 불가":owner.IsAssigned(member)?"작업 예약 중":"교섭 대기");
            string id=CampaignPersistence.MemberId(owner,member);MemberPortrait.sprite=owner.Roster.Candidates.First(c=>c.Id==id).Portrait;
        }
        void BindStock(bool ours,InventorySlot[] slots,int[] mapping,ref int selected,ref int quantity)
        {
            var visible=Enumerable.Range(0,Goods.Length).Where(i=>(ours?owner.CraftPanel.Available(Goods[i].Id):StockFor(Goods[i].Id))>0).ToArray();
            if(!visible.Contains(selected)){selected=visible.Length>0?visible[0]:-1;quantity=1;}
            for(int i=0;i<slots.Length;i++){
                bool show=i<visible.Length;slots[i].gameObject.SetActive(show);mapping[i]=show?visible[i]:-1;if(!show)continue;
                int index=visible[i];var m=Material(Goods[index].Id);
                slots[i].Bind(m.Icon,m.Name,ours?owner.CraftPanel.Available(m.Id):StockFor(m.Id),selected==index);
            }
        }
        static string Clock(int m)=>(m/60).ToString("00")+":"+(m%60).ToString("00");
        static void Focus(Button b){EventSystem.current?.SetSelectedGameObject(b.gameObject);}
        void Update(){if(View.activeSelf&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){if(Review.activeSelf)CancelReview();else if(Trade.activeSelf||RecruitPage.activeSelf)ReturnToConversation();else Close();}}
    }
}



