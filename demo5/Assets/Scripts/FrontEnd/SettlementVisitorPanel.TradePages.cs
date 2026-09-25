using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    public sealed partial class SettlementVisitorPanel
    {
        public Button OurPreviousPage,OurNextPage,TheirPreviousPage,TheirNextPage;
        public Text OurPageLabel,TheirPageLabel;
        int ourPage,theirPage;
        public int OurPage=>ourPage;
        public int TheirPage=>theirPage;
        public int OurPageCount=>PageCount(VisibleStock(true).Length,OurSlots.Length);
        public int TheirPageCount=>PageCount(VisibleStock(false).Length,TheirSlots.Length);

        // The visit date already belongs to the save. Reopening or loading cannot reroll demand.
        public string PreferredGoodId
        {
            get
            {
                if(state.VisitDay<FirstDay||Goods==null)return null;
                int visit=(state.VisitDay-FirstDay)/Math.Max(1,IntervalDays);
                string id=visit%2==0?"tobacco":"coffee";
                return Goods.Any(g=>g.Id==id)?id:null;
            }
        }
        public int BuyValueFor(string id)
        {
            var good=Goods.First(g=>g.Id==id);
            return Math.Max(1,good.Value)+(id==PreferredGoodId?1:0);
        }
        public int SellValueFor(string id)
        {
            var good=Goods.First(g=>g.Id==id);
            // Demand goods cost the preferred buying price even on their off visit.
            // Every item's buy value stays <= its sale value, preventing round-trip profit.
            return Math.Max(1,good.Value)+(id=="tobacco"||id=="coffee"?1:0);
        }
        int[] VisibleStock(bool ours)=>Goods==null?Array.Empty<int>():Enumerable.Range(0,Goods.Length)
            .Where(i=>(ours?owner.CraftPanel.Available(Goods[i].Id):StockFor(Goods[i].Id))>0).ToArray();
        static int PageCount(int count,int size)=>Math.Max(1,(count+Math.Max(1,size)-1)/Math.Max(1,size));
        void InitializeTradePages()
        {
            if(OurPreviousPage)OurPreviousPage.onClick.AddListener(()=>ChangeStockPage(true,-1));
            if(OurNextPage)OurNextPage.onClick.AddListener(()=>ChangeStockPage(true,1));
            if(TheirPreviousPage)TheirPreviousPage.onClick.AddListener(()=>ChangeStockPage(false,-1));
            if(TheirNextPage)TheirNextPage.onClick.AddListener(()=>ChangeStockPage(false,1));
        }
        public void ChangeStockPage(bool ours,int direction)
        {
            if(!Trade.activeInHierarchy||Review.activeSelf)return;
            var visible=VisibleStock(ours);int size=ours?OurSlots.Length:TheirSlots.Length;
            if(size<1||visible.Length==0)return;
            int old=ours?ourPage:theirPage;
            int page=Mathf.Clamp(old+direction,0,PageCount(visible.Length,size)-1);
            if(page==old)return;
            if(ours)ourPage=page;
            else theirPage=page;
            Refresh();
            // A boundary button may just have become disabled; keep keyboard focus on a real item.
            Focus((ours?OurSlots:TheirSlots)[0].Button);
        }
        void UpdateTradePage(bool ours,int page,int count,int size)
        {
            int pages=PageCount(count,size);var previous=ours?OurPreviousPage:TheirPreviousPage;
            var next=ours?OurNextPage:TheirNextPage;var label=ours?OurPageLabel:TheirPageLabel;
            if(previous)previous.interactable=page>0;
            if(next)next.interactable=page+1<pages;
            if(label)label.text=count==0?"재고 없음":"물품 "+(page*size+1)+"–"+Math.Min(count,(page+1)*size)+" / "+count+"종 · "+(page+1)+" / "+pages+"쪽";
        }
    }
}
