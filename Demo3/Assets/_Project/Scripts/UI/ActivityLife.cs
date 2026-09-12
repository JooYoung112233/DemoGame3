using System;
using System.Linq;
using Live49.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Live49.UI
{
    public partial class ActivityPanel
    {
        string _lifeSection,_lifeSelected,_lifeNotice="",_lifeArt;
        int _lifePage;
        float _lifeSaveClock;
        RectTransform _lifeList,_lifeDetail;
        TMP_Text _lifePower;
        void BuildLife(string section)
        {
            _lifeSection=section;_lifePage=0;_lifeSelected=null;_lifeNotice="";_lifeArt=null;
            _title.text=section=="supplies"?"주변에서 챙기는 것들":"캠핑카에서의 생활";
            _subtitle.text=section=="supplies"?"필요한 물자와 아직 남아 있는 흔적을 살펴봐요.":"한 끼를 준비하고, 쉴 곳을 돌보고, 오늘을 남겨요.";
            string[] tabs=section=="supplies"?new[]{"supplies"}:new[]{"meal","equipment","energy","evening","dog"};
            string[] labels=section=="supplies"?new[]{"주변 물자·흔적"}:new[]{"식탁","설비와 수리","전력","오늘 남길 한 장","동행"};
            for(int i=0;i<tabs.Length;i++)
            {
                string tab=tabs[i];PanelUI.Button(_body,_font,"LifeTab_"+tab,labels[i],i*280,0,266,52,()=>{_lifeSection=tab;_lifePage=0;_lifeSelected=null;_lifeArt=null;_lifeNotice="";RefreshLife();});
            }
            _lifeList=PanelUI.Box(_body,"LifeChoices",0,80,600,630,PanelUI.Ink);
            _lifeDetail=PanelUI.Box(_body,"LifeDetail",634,80,1106,630,PanelUI.Ink);
            RefreshLife();
        }
        void RefreshLife()
        {
            PanelUI.Clear(_lifeList);PanelUI.Clear(_lifeDetail);
            var data=CampLife.Data(_state);var options=CampLife.Choices(_state,_lifeSection);
            _lifePage=Mathf.Clamp(_lifePage,0,Mathf.Max(0,(options.Count-1)/5));
            var chosen=options.FirstOrDefault(c=>c.Id==_lifeSelected)??options.FirstOrDefault();_lifeSelected=chosen?.Id;
            foreach(Transform t in _body)if(t.name.StartsWith("LifeTab_"))t.GetComponent<Image>().color=new Color(PanelUI.Gold.r,PanelUI.Gold.g,PanelUI.Gold.b,t.name=="LifeTab_"+_lifeSection?.48f:.12f);
            _lifePower=PanelUI.Text(_lifeList,_font,"LifeResources",ResourceText(),28,20,544,114,23,PanelUI.Gold);
            for(int i=0;i<5&&_lifePage*5+i<options.Count;i++)
            {
                var c=options[_lifePage*5+i];var id=c.Id;
                var button=PanelUI.Button(_lifeList,_font,"LifeChoice_"+id,c.Title,28,148+i*77,544,64,()=>{_lifeSelected=id;RefreshLife();});
                button.GetComponent<Image>().color=new Color(PanelUI.Gold.r,PanelUI.Gold.g,PanelUI.Gold.b,id==_lifeSelected?.28f:.08f);
                if(id==_lifeSelected)PanelUI.Box(button.transform,"Selection",0,0,3,64,PanelUI.Gold);
            }
            if(options.Count>5)
            {
                var prev=PanelUI.Button(_lifeList,_font,"LifePrevious","이전",28,554,160,52,()=>{_lifePage--;RefreshLife();});prev.interactable=_lifePage>0;
                PanelUI.Text(_lifeList,_font,"LifePage",(_lifePage+1)+" / "+((options.Count+4)/5),246,554,130,52,21);
                var next=PanelUI.Button(_lifeList,_font,"LifeNext","다음",412,554,160,52,()=>{_lifePage++;RefreshLife();});next.interactable=(_lifePage+1)*5<options.Count;
            }
            string art=_lifeArt??CurrentLifeArt();
            if(!string.IsNullOrEmpty(art))
            {
                var r=PanelUI.Rect(_lifeDetail,"LifeIllustration",24,22,640,360);var img=r.gameObject.AddComponent<Image>();
                img.sprite=Resources.Load<Sprite>("Live49/Stages/"+(art.StartsWith("@")?art.Substring(1):"life-"+art));img.preserveAspect=true;img.raycastTarget=false;
            }
            PanelUI.Text(_lifeDetail,_font,"LifeDetailTitle",chosen?.Title??(_lifeSection=="dog"?"아직 동행이 없어요.":_lifeSection=="evening"?"오늘 남길 장면을 찾아봐요.":"지금 챙길 물자가 없어요."),28,393,636,74,29);
            PanelUI.Text(_lifeDetail,_font,"LifeDetailText",chosen?.Detail??(_lifeSection=="evening"?"오늘 방문한 곳과 챙긴 물건이 후보가 돼요.\n기록을 생략하고 바로 쉬어도 괜찮아요.":"이곳을 둘러보거나 여행을 이어가요."),700,25,374,299,24,PanelUI.Muted);
            if(_lifeSection=="energy")
            {
                var sites=RegionExploration.ActiveSites(_state).Where(p=>RegionExploration.Discovered(_state,p.Id)).ToArray();
                PanelUI.Button(_lifeDetail,_font,"EnergyDestination","이동할 곳 선택",700,337,374,60,()=>
                {
                    int i=Array.FindIndex(sites,p=>p.Id==data.energyDestination);data.energyDestination=sites[(i+1)%sites.Length].Id;PersistActivity();RefreshLife();
                });
                PanelUI.Text(_lifeDetail,_font,"EnergyRouteNote","지역 밖 출발에는 별도 연료 1이 더 필요해요.",700,409,374,66,19,PanelUI.Muted);
            }
            if(chosen!=null)
            {
                string id=chosen.Id;var confirm=PanelUI.Button(_lifeDetail,_font,"LifeConfirm",chosen.Block??"확인하고 진행하기",28,494,1046,68,()=>
                {
                    if(CampLife.Apply(_state,_lifeSection,id,out var error))
                    {
                        _lifeNotice=chosen.Title+" · 기록했어요.";_lifeArt=chosen.Art??CurrentLifeArt();PersistActivity();RefreshLife();
                    }
                    else{_lifeNotice=error;RefreshLife();}
                });confirm.interactable=chosen.Block==null;
            }
            string note=_lifeNotice;
            if(_lifeSection=="evening"&&_state.Has("memory."+_state.day))note="오늘 남긴 한 장 · "+_state.Value("memory.title."+_state.day);
            PanelUI.Text(_lifeDetail,_font,"LifeResult",note,28,577,1046,35,21,PanelUI.Gold);
        }
        string ResourceText()
        {
            var data=CampLife.Data(_state);
            return "생수 "+_state.Count("water")+"   포장 식량 "+_state.Count("packaged_food")+"   식사 "+_state.Count("meal")+"\n배터리 "+data.battery+" / 100   공용 연료 "+_state.fuel+"\n"+(CampLife.Heating(_state)?"전자레인지 · 가열 중 "+Mathf.CeilToInt(data.microwaveLeft)+"초":_lifeSection=="meal"?"간편식 "+_state.Count("ready_meal")+"   오늘의 식사 "+(_state.Has("life.meal."+_state.day)?"완료":"준비 전"):_lifeSection=="equipment"?"수리 부품 "+_state.Count("repair_parts")+"   천 "+_state.Count("cloth"):_lifeSection=="evening"?"오늘 겪은 일 가운데 한 가지를 골라요.":"확인 후에만 물자와 시간이 사용돼요.");
        }
        string CurrentLifeArt()
        {
            if(_lifeSection=="evening")return _state.Has("dog_place_ready")?"E24-HUB-memories-with-dog":"E24-HUB-memories";
            if(_lifeSection=="dog")return _state.Has("dog_place_ready")?"E14-HUB-rest":"@journal";
            if(_lifeSection=="supplies")
            {
                string p=RegionExploration.PlayerPlace(_state);
                if(p=="L5"&&!_state.Has("dog_joined"))return _state.Has("dog_relaxed")?"03-L5-relaxed":_state.Has("dog_care_offered")?(_state.Value("dog_care_kind")=="water"?"02-L5-water-placed":"E12-L5-food-alternative"):"01-L5-wary";
                var site=RegionExploration.Find(p);return string.IsNullOrEmpty(site?.Art)?null:"@"+site.Art;
            }
            return _lifeSection=="meal"&&_state.Has("life.meal."+_state.day)?"E03-HUB-rations-shared":_lifeSection=="equipment"||_lifeSection=="energy"?"E04-HUB-stove-off":"@journal";
        }
        void TickLife()
        {
            if(!CampLife.Home(_state)||!CampLife.Heating(_state))return;
            bool completed=CampLife.TickHeating(_state,Time.unscaledDeltaTime);_lifeSaveClock+=Time.unscaledDeltaTime;
            if(_lifePower!=null)_lifePower.text=ResourceText();
            if(completed){_lifeNotice="따뜻한 식사 1개를 가방에 담았어요.";_lifeArt="E17-HUB-warm-meal";RefreshLife();}
            if(completed||_lifeSaveClock>=1){PersistActivity(completed);_lifeSaveClock=0;}
        }
        void PersistActivity(bool notify=true)
        {
            if(_state==null)return;
            if(!SaveSystem.AutoSave(_state,out var error,notify))_lifeNotice=error;
        }
    }
}
