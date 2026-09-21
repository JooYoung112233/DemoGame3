using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Live49.UI
{
    // Read-only inventory and individual status. Resource consumption belongs to gameplay.
    public class BagPanel : MonoBehaviour
    {
        static readonly Color Cream = new Color32(242,228,196,255);
        static readonly Color Muted = new Color32(185,166,135,255);
        static readonly Color Gold = new Color32(180,145,89,255);
        static readonly Color Ink = new Color32(31,28,23,255);
        static readonly Color Sage = new Color32(139,161,135,255);
        BagContents _data = new BagContents();
        TMP_FontAsset _font;
        CanvasGroup _group;
        RectTransform _grid, _status;
        TMP_Text _count, _empty, _title, _description, _category, _quantity, _pageLabel;
        Button _close, _previous, _next;
        readonly List<Button> _tabs = new List<Button>();
        readonly List<Button> _items = new List<Button>();
        Action _onClose;
        Coroutine _fade;
        int _filter, _page;
        bool _closing;
        public bool IsOpen => gameObject.activeSelf;
        public string SelectedId { get; private set; }
        public int Filter => _filter;
        public int Page => _page;
        const int PageSize = 6;

        public static BagPanel Create(Transform parent, TMP_FontAsset font, Action close)
        {
            var rt = Rect(parent,"BagPanel",0,0,1920,1080);
            rt.gameObject.AddComponent<Image>().color = new Color(0,0,0,.9f);
            var panel = rt.gameObject.AddComponent<BagPanel>();
            panel._font=font; panel._onClose=close; panel._group=rt.gameObject.AddComponent<CanvasGroup>();
            panel.Build(); rt.gameObject.SetActive(false); return panel;
        }
        void Build()
        {
            Label(transform,"함께 챙기는 것들",90,64,1100,28,18,Gold);
            Label(transform,"가방과 우리의 상태",85,103,1200,66,48,Cream);
            Label(transform,"가진 것을 살피고, 서로를 돌볼 준비를 해요.",90,179,1200,38,23,Muted);
            _close=Button(transform,"CloseBag","닫기",1680,105,150,64,_onClose);
            var bag=Box(transform,"InventoryCard",90,244,1038,712,Ink);
            Box(bag,"Rule",0,0,1038,2,Gold);
            Label(bag,"함께 보관하는 물건",32,22,650,40,27,Cream);
            _count=Label(bag,"",700,26,305,34,18,Muted,TextAlignmentOptions.MidlineRight);
            string[] names={"전체","식량 · 물","생활용품","소중한 물건"};
            for(int i=0;i<4;i++)
            {
                int filter=i;
                _tabs.Add(Button(bag,"BagFilter_"+i,names[i],32+i*244,82,232,48,()=>SetFilter(filter)));
            }
            _grid=Rect(bag,"ItemGrid",32,150,974,302);
            _empty=Label(_grid,"",30,78,914,145,26,Muted,TextAlignmentOptions.Center);
            _previous=Button(bag,"BagPrevious","이전",32,462,100,42,()=>ChangePage(-1));
            _pageLabel=Label(bag,"",144,462,686,42,17,Muted,TextAlignmentOptions.Center);
            _next=Button(bag,"BagNext","다음",906,462,100,42,()=>ChangePage(1));
            Box(bag,"DetailRule",32,525,974,1,new Color(Gold.r,Gold.g,Gold.b,.35f));
            _category=Label(bag,"물건 살펴보기",32,545,730,28,16,Gold);
            _title=Label(bag,"목록에서 물건을 골라주세요.",32,579,790,42,29,Cream);
            _quantity=Label(bag,"",825,580,180,38,23,Cream,TextAlignmentOptions.MidlineRight);
            _description=Label(bag,"선택한 물건의 설명과 보유 수량을 볼 수 있어요.",32,636,974,56,20,Muted);
            _status=Rect(transform,"MemberStatus",1160,244,670,712);
            Label(transform,"서두르지 않고, 하나씩 챙겨요.",90,983,1740,34,19,Muted);
            Label(transform,"— 아직 확인하지 않은 상태",1160,983,670,34,18,Muted,TextAlignmentOptions.MidlineRight);
        }
        public void SetContents(BagContents contents)
        {
            _data=contents??new BagContents();
            Refresh(); RenderMembers();
        }
        BagContents.Item[] VisibleItems() => !_data.InventoryKnown ? Array.Empty<BagContents.Item>() :
            (_data.Items??Array.Empty<BagContents.Item>()).Where(i=>i!=null && !string.IsNullOrEmpty(i.Id) && i.Quantity>0
                && (_filter==0 || (int)i.Group==_filter-1)).ToArray();
        public void SetFilter(int filter)
        {
            if(_closing || filter<0 || filter>3)return;
            _filter=filter;_page=0;SelectedId=null;Refresh();
        }
        void ChangePage(int delta){if(_closing)return;_page+=delta;SelectedId=null;Refresh();}
        public void Select(string id)
        {
            if(_closing)return;
            if(!VisibleItems().Skip(_page*PageSize).Take(PageSize).Any(i=>i.Id==id))return;
            SelectedId=id;RefreshDetail();
        }
        void Refresh()
        {
            foreach(var b in _items){b.gameObject.SetActive(false);Destroy(b.gameObject);} _items.Clear();
            var all=VisibleItems();int pages=Math.Max(1,(all.Length+PageSize-1)/PageSize);
            _page=Mathf.Clamp(_page,0,pages-1);
            var shown=all.Skip(_page*PageSize).Take(PageSize).ToArray();
            if(!shown.Any(i=>i.Id==SelectedId))SelectedId=null;
            _count.text=_data.InventoryKnown ? all.Length+"종류" : "소지품 확인 전";
            _empty.gameObject.SetActive(all.Length==0);
            _empty.text=!_data.InventoryKnown ? "가방을 정리하며\n가지고 있는 물건을 확인해요." : _filter==0 ? "아직 챙긴 물건이 없어요." : "이 종류의 물건은 아직 없어요.";
            for(int i=0;i<shown.Length;i++)
            {
                var item=shown[i];
                var button=Button(_grid,"BagItem_"+item.Id,"",i%3*329,i/3*156,316,144,()=>Select(item.Id));
                var icon=Rect(button.transform,"ItemIcon",19,19,34,34).gameObject.AddComponent<HudIcon>();
                icon.Symbol=item.Group==BagContents.Category.Food?HudIcon.Kind.Cooking:item.Group==BagContents.Category.Keepsakes?HudIcon.Kind.Journal:HudIcon.Kind.Bag;
                icon.color=Gold;icon.raycastTarget=false;
                Label(button.transform,"× "+item.Quantity,170,18,124,32,21,Muted,TextAlignmentOptions.MidlineRight);
                Label(button.transform,item.Name,19,76,278,53,25,Cream);
                _items.Add(button);
            }
            for(int i=0;i<_tabs.Count;i++)_tabs[i].GetComponent<Image>().color=i==_filter?new Color(Gold.r,Gold.g,Gold.b,.36f):new Color(1,1,1,.035f);
            _previous.interactable=_page>0;_next.interactable=_page+1<pages;
            _pageLabel.text=all.Length==0 ? "" : (_page+1)+" / "+pages;
            RefreshDetail();WireNavigation();
        }
        void RefreshDetail()
        {
            var item=VisibleItems().FirstOrDefault(i=>i.Id==SelectedId);
            _category.text=item==null?"물건 살펴보기":new[]{"식량 · 물","생활용품","소중한 물건"}[(int)item.Group];
            _title.text=item?.Name??"목록에서 물건을 골라주세요.";
            _description.text=item?.Description??"선택한 물건의 설명과 보유 수량을 볼 수 있어요.";
            _quantity.text=item==null?"":"보유 "+item.Quantity;
            foreach(var b in _items)b.GetComponent<Image>().color=b.name=="BagItem_"+SelectedId?new Color(Gold.r,Gold.g,Gold.b,.35f):new Color(1,1,1,.045f);
        }
        void RenderMembers()
        {
            foreach(Transform child in _status){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            var members=(_data.Members??Array.Empty<BagContents.Member>()).Where(m=>m!=null).Take(2).ToArray();
            for(int i=0;i<members.Length;i++)
            {
                var m=members[i];var card=Box(_status,"Status_"+m.Id,0,i*366,670,346,Ink);
                Box(card,"Rule",0,0,2,346,i==0?Gold:Sage);
                Label(card,string.IsNullOrWhiteSpace(m.Name)?"?":m.Name,32,22,300,48,36,Cream);
                Label(card,"몸과 마음을 살피는 시간",318,31,320,33,17,Muted,TextAlignmentOptions.MidlineRight);
                Gauge(card,"허기",m.Hunger,95,Gold);
                Gauge(card,"수분",m.Water,150,new Color32(130,163,173,255));
                Gauge(card,"체력",m.Health,205,Sage);
                Gauge(card,"스태미너",m.Stamina,260,new Color32(194,159,125,255));
            }
        }
        void Gauge(Transform parent,string title,BagContents.Gauge gauge,float y,Color color)
        {
            bool known=gauge!=null&&gauge.IsKnown;
            Label(parent,title,32,y,124,31,22,Cream);
            Label(parent,known?Mathf.Clamp(gauge.Current,0,gauge.Maximum).ToString("0.#")+" / "+gauge.Maximum.ToString("0.#"):"—",422,y,216,31,20,Muted,TextAlignmentOptions.MidlineRight);
            var track=Box(parent,"Gauge_"+title,172,y+12,236,7,new Color(1,1,1,.075f));
            if(known)Box(track,"Fill",0,0,236*Mathf.Clamp01(gauge.Current/gauge.Maximum),7,color);
            else for(int i=0;i<12;i++)Box(track,"Unknown",i*20,0,10,7,new Color(color.r,color.g,color.b,.24f));
        }
        void WireNavigation()
        {
            var buttons=new List<Button>(_tabs);buttons.AddRange(_items);
            if(_previous.interactable)buttons.Add(_previous);if(_next.interactable)buttons.Add(_next);buttons.Add(_close);
            for(int i=0;i<buttons.Count;i++)buttons[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,
                selectOnRight=buttons[(i+1)%buttons.Count],selectOnDown=buttons[(i+1)%buttons.Count],
                selectOnLeft=buttons[(i+buttons.Count-1)%buttons.Count],selectOnUp=buttons[(i+buttons.Count-1)%buttons.Count]};
            if(IsOpen)EventSystem.current?.SetSelectedGameObject(_tabs[_filter].gameObject);
        }
        public void Open()
        {gameObject.SetActive(true);transform.SetAsLastSibling();_closing=false;Refresh();_fade=StartCoroutine(Fade(0,1,.22f));}
        public void Close(Action completed)
        {if(_closing)return;_closing=true;if(_fade!=null)StopCoroutine(_fade);StartCoroutine(CloseRoutine(completed));}
        IEnumerator CloseRoutine(Action completed)
        {yield return Fade(_group.alpha,0,.16f);gameObject.SetActive(false);_closing=false;completed();}
        IEnumerator Fade(float from,float to,float duration)
        {for(float t=0;t<duration;t+=Time.unscaledDeltaTime){_group.alpha=Mathf.Lerp(from,to,Mathf.SmoothStep(0,1,t/duration));yield return null;}_group.alpha=to;}
        static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
        static RectTransform Box(Transform p,string name,float x,float y,float w,float h,Color color)
        {var r=Rect(p,name,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return r;}
        TMP_Text Label(Transform p,string text,float x,float y,float w,float h,float size,Color color,TextAlignmentOptions align=TextAlignmentOptions.MidlineLeft)
        {var t=Rect(p,"Label_"+text,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();t.font=_font;t.text=text;t.fontSize=size;t.color=color;t.alignment=align;t.raycastTarget=false;t.enableAutoSizing=true;t.fontSizeMin=size-3;t.fontSizeMax=size;return t;}
        Button Button(Transform p,string name,string text,float x,float y,float w,float h,Action action)
        {
            var r=Box(p,name,x,y,w,h,new Color(1,1,1,.045f));var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();b.targetGraphic.raycastTarget=true;
            var colors=b.colors;colors.highlightedColor=colors.selectedColor=new Color(1.5f,1.35f,1.08f);colors.pressedColor=Gold;colors.fadeDuration=.12f;b.colors=colors;
            b.onClick.AddListener(()=>{if(!_closing){Live49.Core.FreshInput.DiscardPending();action();}});
            if(text.Length>0)Label(r,text,8,0,w-16,h,23,Cream,TextAlignmentOptions.Center);
            return b;
        }
    }
}
