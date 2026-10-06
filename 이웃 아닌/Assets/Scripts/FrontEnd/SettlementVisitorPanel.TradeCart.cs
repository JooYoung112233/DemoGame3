using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    public sealed partial class SettlementVisitorPanel
    {
        public TradeCartRow[] GiveRows,TakeRows;
        public Text GiveTotal,TakeTotal,TradeHint,OfferLabel;
        public GameObject GiveEmpty,TakeEmpty;
        public ScrollRect GiveCartScroll,TakeCartScroll;
        public Button ClearTrade;
        public GameObject CartReview;
        public TradeCartRow[] ReviewGiveRows,ReviewTakeRows;
        public Text ReviewGiveTotal,ReviewTakeTotal,ReviewSummary;
        public ScrollRect ReviewGiveScroll,ReviewTakeScroll;

        readonly Dictionary<string,int> giveCart=new Dictionary<string,int>();
        readonly Dictionary<string,int> takeCart=new Dictionary<string,int>();
        int cartRevision;
        string tradeMessage;

        sealed class QuoteLine
        {
            public string Id;
            public int Count,Value;
        }
        sealed class Quote
        {
            public int Day,Revision;
            public CampaignState Campaign;
            public Adventurer Member;
            public QuoteLine[] Give,Take;
            public long GiveValue=>Give.Sum(x=>(long)x.Count*x.Value);
            public long TakeValue=>Take.Sum(x=>(long)x.Count*x.Value);
        }

        public int CartCount(bool ours,string id)=>id!=null&&(ours?giveCart:takeCart).TryGetValue(id,out int count)?count:0;
        public int CartValue(bool ours)=>(int)(ours?giveCart:takeCart).Sum(x=>(long)x.Value*(ours?BuyValueFor(x.Key):SellValueFor(x.Key)));

        void InitializeTradeCart()
        {
            if(ClearTrade)ClearTrade.onClick.AddListener(ClearOffer);
            if(CartReview)CartReview.SetActive(false);
            ResetCart();
        }
        void ResetCart()
        {
            giveCart.Clear();takeCart.Clear();cartRevision++;tradeMessage=null;
            if(GiveCartScroll)GiveCartScroll.verticalNormalizedPosition=1;
            if(TakeCartScroll)TakeCartScroll.verticalNormalizedPosition=1;
        }
        public void ClearOffer()
        {
            if(!Trade.activeInHierarchy||Review.activeSelf)return;
            ResetCart();Refresh();
        }
        public void ChangeCartQuantity(bool ours,string id,int delta)
        {
            if(!Trade.activeInHierarchy||Review.activeSelf||!Workspace.interactable||!IsPresent||delta==0)return;
            if(id==null||!Goods.Any(g=>g.Id==id))return;
            if(delta>0&&CartCount(!ours,id)>0){SetTradeMessage(ItemName(id)+"은 반대쪽 목록에 담겨 있습니다. 먼저 빼주세요.");return;}
            var cart=ours?giveCart:takeCart;int current=CartCount(ours,id);
            int available=ours?owner.CraftPanel.Available(id):StockFor(id);
            int next=delta<0?(int)Math.Max(0L,(long)current+delta):(int)Math.Min(Math.Min(99,available),(long)current+delta);
            // Changed reservations or stock must not make an add action silently remove
            // an existing line. Its minus button remains usable to repair that draft.
            if(delta>0&&next<=current){SetTradeMessage(current>=99?"한 물품은 99개까지 담을 수 있습니다.":ours?"사용 가능한 수량을 모두 담았습니다. 예약 재료는 제외됩니다.":"방문자의 남은 수량을 모두 담았습니다.");return;}
            if(next==current)return;
            if(next==0)cart.Remove(id);else cart[id]=next;
            cartRevision++;tradeMessage=null;Refresh();
        }

        QuoteLine[] Lines(bool ours)=>(Goods??Array.Empty<Good>()).Where(g=>CartCount(ours,g.Id)>0)
            .Select(g=>new QuoteLine{Id=g.Id,Count=CartCount(ours,g.Id),Value=ours?BuyValueFor(g.Id):SellValueFor(g.Id)}).ToArray();
        Adventurer Negotiator()=>owner?.Campaign?.Party.FirstOrDefault(p=>p.Health>0&&!owner.IsAssigned(p));
        Quote Draft()=>new Quote{Day=state.VisitDay,Revision=cartRevision,Campaign=owner?.Campaign,Member=Negotiator(),Give=Lines(true),Take=Lines(false)};

        string Block(Quote q)
        {
            if(q==null||owner?.Campaign==null||owner.Campaign.Stage!=JourneyStage.Settlement||!IsPresent||q.Day!=state.VisitDay||q.Campaign!=owner.Campaign)return "방문이 끝났습니다.";
            if(q.Revision!=cartRevision)return "교환 목록이 바뀌었습니다. 다시 확인해주세요.";
            if(q.Give.Length==0&&q.Take.Length==0)return "물품을 눌러 내놓을 것과 받을 것을 담아주세요.";
            if(q.Take.Length==0)return "받을 물품을 담아주세요.";
            if(q.Give.Length==0)return "가치 부족 "+q.TakeValue+" · 내놓을 물품을 담아주세요.";
            if(q.Member==null||!owner.Campaign.Party.Contains(q.Member)||q.Member.Health<=0||owner.IsAssigned(q.Member))return "교섭할 수 있는 대기 대원이 필요합니다.";
            if(q.Give.Any(g=>q.Take.Any(t=>g.Id==t.Id)))return "같은 물품은 양쪽 목록에 담을 수 없습니다.";
            foreach(var line in q.Give.Concat(q.Take))
                if(line.Count<1||line.Count>99||!Goods.Any(g=>g.Id==line.Id)||!owner.CraftPanel.Materials.Any(m=>m.Id==line.Id))return "교환 물품이나 수량을 확인해주세요.";
            foreach(var line in q.Give)
            {
                if(line.Value!=BuyValueFor(line.Id))return "교환 가치가 바뀌었습니다. 다시 확인해주세요.";
                if(owner.CraftPanel.Available(line.Id)<line.Count)return ItemName(line.Id)+"이 부족합니다. 예약 재료는 제외됩니다.";
                if((long)StockFor(line.Id)+line.Count>1000000)return "방문자가 "+ItemName(line.Id)+"을 더 보관할 수 없습니다.";
            }
            foreach(var line in q.Take)
            {
                if(line.Value!=SellValueFor(line.Id))return "교환 가치가 바뀌었습니다. 다시 확인해주세요.";
                if(StockFor(line.Id)<line.Count)return "방문자의 "+ItemName(line.Id)+" 재고가 부족합니다.";
                if((long)Material(line.Id).Initial+line.Count>1000000)return ItemName(line.Id)+"을 더 보관할 수 없습니다.";
            }
            if(q.GiveValue<q.TakeValue)return "가치 부족 "+(q.TakeValue-q.GiveValue)+" · 내놓을 물품을 더 담거나 받을 물품을 줄여주세요.";
            int giveCount=q.Give.Sum(x=>x.Count),takeCount=q.Take.Sum(x=>x.Count);
            if(owner.Development&&takeCount>giveCount&&(long)owner.Development.StockUsed-giveCount+takeCount>owner.Development.Capacity)
                return "교환 후 창고 공간이 부족합니다. 받을 물품을 줄여주세요.";
            return null;
        }

        void RefreshTradeCart()
        {
            var q=Draft();var reason=Block(q);
            BindCartRows(true,GiveRows,q.Give,false);BindCartRows(false,TakeRows,q.Take,false);
            if(GiveEmpty)GiveEmpty.SetActive(q.Give.Length==0);
            if(TakeEmpty)TakeEmpty.SetActive(q.Take.Length==0);
            if(GiveTotal)GiveTotal.text="내놓는 가치  "+q.GiveValue;
            if(TakeTotal)TakeTotal.text="받는 가치  "+q.TakeValue;
            if(TradeHint)TradeHint.text="물품을 누르면 1개 담깁니다. 담은 목록에서 + / −로 수량을 바꿉니다.";
            if(ClearTrade)ClearTrade.interactable=q.Give.Length+q.Take.Length>0;
            Offer.interactable=reason==null;
            long difference=q.GiveValue-q.TakeValue;
            if(OfferLabel)OfferLabel.text=reason==null?"교환 확인":difference<0?"가치 "+(-difference)+" 부족":q.Give.Length==0||q.Take.Length==0?"물품을 담아주세요":"교환할 수 없음";
            string balance=reason??(difference==0?"교환 가능 · 가치가 같습니다.":"교환 가능 · 가치 "+difference+" 초과 (차액은 돌려받지 못합니다)");
            if(Balance)Balance.text=tradeMessage??balance;
            if(MemberName)MemberName.text=q.Member==null?"교섭 가능한 대원 없음":"교섭 담당 · "+q.Member.Name+" (자동)";
            if(MemberPortrait)
            {
                string id=q.Member==null?null:CampaignPersistence.MemberId(owner,q.Member);
                MemberPortrait.sprite=owner.Roster.Candidates.FirstOrDefault(c=>c.Id==id)?.Portrait;
                MemberPortrait.enabled=MemberPortrait.sprite;
            }
        }
        void BindCartRows(bool ours,TradeCartRow[] rows,QuoteLine[] lines,bool readOnly)
        {
            if(rows==null)return;
            for(int i=0;i<rows.Length;i++)
            {
                var row=rows[i];if(!row)continue;
                row.gameObject.SetActive(i<lines.Length);if(i>=lines.Length)continue;
                var line=lines[i];var material=Material(line.Id);
                int available=ours?owner.CraftPanel.Available(line.Id):StockFor(line.Id);
                row.Bind(line.Id,ours,material.Icon,material.Name,line.Count,line.Value,!readOnly&&line.Count<Math.Min(99,available),
                    readOnly?null:(Action)(()=>ChangeCartQuantity(ours,line.Id,-1)),readOnly?null:(Action)(()=>ChangeCartQuantity(ours,line.Id,1)));
            }
        }
        void SetTradeMessage(string message){tradeMessage=message;if(Balance)Balance.text=message;}

        string LineText(QuoteLine line)=>ItemName(line.Id)+" ×"+line.Count;
        string ReviewText(Quote q)
        {
            long excess=q.GiveValue-q.TakeValue;
            return "내놓을 물품\n"+string.Join(" · ",q.Give.Select(LineText))+"\n받을 물품\n"+string.Join(" · ",q.Take.Select(LineText))+
                "\n가치 "+q.GiveValue+" → "+q.TakeValue+"\n"+(excess>0?"초과 가치 "+excess+"은 돌려받지 못합니다.":"동일한 가치로 교환합니다.")+"\n확정하면 공용 창고 물품이 교환됩니다.";
        }
        void ShowCartReview(Quote q)
        {
            if(!CartReview)return;
            BindCartRows(true,ReviewGiveRows,q.Give,true);BindCartRows(false,ReviewTakeRows,q.Take,true);
            if(ReviewGiveTotal)ReviewGiveTotal.text="내놓는 가치  "+q.GiveValue;
            if(ReviewTakeTotal)ReviewTakeTotal.text="받는 가치  "+q.TakeValue;
            long excess=q.GiveValue-q.TakeValue;
            if(ReviewSummary)ReviewSummary.text=(excess>0?"가치 "+excess+"을 더 내놓습니다. 차액은 돌려받지 못합니다.":"가치가 같습니다.")+"\n확정하면 공용 창고의 물품이 교환됩니다.";
            ConfirmBody.gameObject.SetActive(false);CartReview.SetActive(true);
            Canvas.ForceUpdateCanvases();
            if(ReviewGiveScroll)ReviewGiveScroll.verticalNormalizedPosition=1;
            if(ReviewTakeScroll)ReviewTakeScroll.verticalNormalizedPosition=1;
        }
        string TradeSummary(Quote q)=>"내놓음 "+string.Join(", ",q.Give.Select(LineText))+" / 받음 "+string.Join(", ",q.Take.Select(LineText));
        string ShortLineText(QuoteLine[] lines)=>string.Join(", ",lines.Take(3).Select(LineText))+(lines.Length>3?" 외 "+(lines.Length-3)+"종":"");
        string CompletionText(Quote q)=>"교환 완료 · 내놓음 "+ShortLineText(q.Give)+"\n받음 "+ShortLineText(q.Take);
    }
}
