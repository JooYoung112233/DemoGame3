using System;
using System.Collections;
using Live49.Core;
using Live49.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Live49.Chapter00
{
    public partial class C0OpeningDirector
    {
        RectTransform _banditStage,_banditPanel;
        bool _banditSaveFailed;
        float _banditReadyAt;
        IEnumerator ShowBanditEncounter(bool newCheckpoint)
        {
            _interactionBusy=true;
            var hud=GameHud.Instance;hud.SetNarrativeMode();
            if(_journeyActions!=null)PanelUI.Clear(_journeyActions);
            _interactions.gameObject.SetActive(false);
            yield return Tween.Fade(screenFade,1,.45f);
            if(_state.bandit.stage==3)SetJourneyImage("journal");else SetBanditStage();
            _banditSaveFailed=newCheckpoint&&!SaveSystem.AutoSave(_state,out _,false);
            yield return Tween.Wait(.5f);yield return Tween.Fade(screenFade,0,.85f);
            hud.SetExplorationContext(_state.bandit.stage==3?"캠핑카":"주차장 옆 골목",_state.day+"일 차","");
            RenderBanditPanel();FreshInput.DiscardPending();_interactionBusy=false;
        }
        void SetBanditStage()
        {
            if(_journeyImage!=null)_journeyImage.gameObject.SetActive(false);
            if(_regionOverview!=null)_regionOverview.gameObject.SetActive(false);
            if(_weekDisplay!=null)_weekDisplay.gameObject.SetActive(false);
            if(_weekProp!=null)_weekProp.gameObject.SetActive(false);
            if(_banditStage==null)
            {
                _banditStage=PanelUI.Rect(present.transform,"BanditStage",0,0,1920,1080);
                _banditStage.anchorMin=_banditStage.anchorMax=_banditStage.pivot=Vector2.one*.5f;_banditStage.anchoredPosition=Vector2.zero;
                BanditImage("alley",0,0,1672,941);
                BanditImage("lookout-shadow",1103,476,134,46);
                float k=270f/1494;BanditImage("lookout",1122-234*k,229-16*k,1024*k,1536*k);
                BanditImage("lead-shadow",873,592,185,46);
                k=310f/1504;BanditImage("lead",892-130*k,305-19*k,1024*k,1536*k);
            }
            _banditStage.gameObject.SetActive(true);_banditStage.SetAsLastSibling();
        }
        void BanditImage(string name,float x,float y,float w,float h)
        {
            var image=PanelUI.Rect(_banditStage,name,x*1920/1672,y*1080/941,w*1920/1672,h*1080/941).gameObject.AddComponent<Image>();
            image.sprite=Resources.Load<Sprite>("Live49/Encounters/Bandit/"+name);image.raycastTarget=false;
            if(image.sprite==null)throw new InvalidOperationException("Missing bandit art: "+name);
        }
        void RenderBanditPanel()
        {
            if(_banditPanel==null)
            {
                _banditPanel=PanelUI.Rect(GameHud.Instance.InteractionRoot,"BanditEncounterPanel",90,260,690,570);
                _banditPanel.SetAsFirstSibling();_banditPanel.gameObject.AddComponent<ExplorationOnly>();
            }
            PanelUI.Clear(_banditPanel);_banditPanel.gameObject.SetActive(true);_banditReadyAt=Time.unscaledTime+.35f;
            PanelUI.Box(_banditPanel,"Paper",0,0,690,570,new Color32(28,29,25,247),true);
            PanelUI.Box(_banditPanel,"Edge",0,0,3,570,new Color32(153,108,73,255));
            bool choosing=_state.bandit.stage==0,home=_state.bandit.stage==3;
            PanelUI.Text(_banditPanel,Font,"Context",home?"캠핑카로 돌아온 뒤":"공영주차장 · 옆 골목",32,22,626,32,19,PanelUI.Gold);
            PanelUI.Text(_banditPanel,Font,"Title",home?"문을 닫고 숨을 고른다":choosing?"길을 막은 사람들":"골목을 벗어나며",30,64,630,58,34);
            string description=home?RegionExploration.ParkingName(_state)+"에 세운 캠핑카로 돌아왔다. 주변을 살피고 문을 잠근 뒤, 남은 물자를 확인한다.":choosing?"두 사람이 골목을 막고 물자를 요구한다.\n거리를 둔 채, 어떻게 지나갈지 결정한다.":BanditEncounter.Result(_state);
            PanelUI.Text(_banditPanel,Font,"Description",description,32,135,626,choosing?82:170,25);
            if(_banditSaveFailed)
            {
                PanelUI.Text(_banditPanel,Font,"SaveError","자동 저장을 마치지 못했어요.\n현재 선택을 보관할 수 있도록 다시 시도해주세요.",32,330,626,90,23,PanelUI.Gold);
                BanditButton("Retry","저장 다시 시도",456,true,()=>{_banditSaveFailed=!SaveSystem.AutoSave(_state,out _,false);RenderBanditPanel();});return;
            }
            if(choosing)
            {
                Choice("food","포장 식량 건네기", "포장 식량 −1 · 5분",_state.Count("packaged_food"),248);
                Choice("water","생수 건네기", "생수 −1 · 5분",_state.Count("water"),346);
                Choice("detour","돌아가는 길로 우회", "물자 소모 없음 · 20분",-1,444);
            }
            else
            {
                PanelUI.Text(_banditPanel,Font,"Record",home?"이번 일은 오늘의 활동에 남아요.\n저녁의 ‘오늘 남길 한 장’에서 선택할 수 있어요.":"캠핑카 주차 위치와 중요한 소지품은 그대로예요.\n결과를 확인한 뒤 주변 탐색을 이어가요.",32,330,626,88,22,PanelUI.Muted);
                BanditButton("Continue",home?"확인을 마치고 계속하기":"주차장 탐색 이어가기",456,true,()=>StartCoroutine(CloseBanditEncounter()));
            }
        }
        void Choice(string id,string label,string cost,int stock,float y)
        {
            string block=BanditEncounter.Block(_state,id);
            var b=BanditButton(id,"",y,block==null,()=>
            {
                if(!BanditEncounter.Choose(_state,id))return;
                _banditSaveFailed=!SaveSystem.AutoSave(_state,out _);RenderBanditPanel();
            });
            PanelUI.Text(b.transform,Font,"ChoiceLabel",label,20,8,590,32,25,block==null?PanelUI.Cream:PanelUI.Muted);
            PanelUI.Text(b.transform,Font,"Cost",block??cost+(stock>=0?"  /  보유 "+stock:""),20,44,590,25,18,PanelUI.Muted);
        }
        Button BanditButton(string id,string label,float y,bool enabled,Action action)
        {
            var b=PanelUI.Button(_banditPanel,Font,"Bandit_"+id,label,32,y,626,80,()=>
            {if(!_interactionBusy&&!GameHud.Instance.IsPaused&&Time.unscaledTime>=_banditReadyAt)action();});
            b.interactable=enabled;return b;
        }
        IEnumerator CloseBanditEncounter()
        {
            if(_interactionBusy)yield break;_interactionBusy=true;
            int stage=_state.bandit.stage;
            bool finished=stage==3?BanditEncounter.FinishReturn(_state):BanditEncounter.FinishResult(_state);
            if(!finished){_interactionBusy=false;yield break;}
            if(!SaveSystem.AutoSave(_state,out _))
            {_state.bandit.stage=stage;_banditSaveFailed=true;RenderBanditPanel();_interactionBusy=false;yield break;}
            _banditPanel.gameObject.SetActive(false);GameHud.Instance.SetNarrativeMode();
            yield return Tween.Fade(screenFade,1,.4f);
            if(AwayFromCamper)SetRegionView(_state.exploringPlace);else SetJourneyImage("journal");
            yield return Tween.Fade(screenFade,0,.65f);yield return EnterExploration(false);
        }
    }
}
