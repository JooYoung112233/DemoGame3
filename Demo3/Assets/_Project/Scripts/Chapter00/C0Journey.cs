using System;
using System.Collections;
using System.Linq;
using Live49.Core;
using Live49.Dialogue;
using Live49.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Live49.Chapter00
{
    public partial class C0OpeningDirector
    {
        JourneyState _state;
        ContinuationGraph _graph;
        RectTransform _journeyActions;
        Image _journeyImage;
        StageImageFX _journeyFX;
        string _sceneId;
        public JourneyState State=>_state;
        TMP_FontAsset Font=>dialogue.Body.GetComponent<TMP_Text>().font;
        string Goal=>_state.day>0?JourneyTutorial.Current(_state).Title:
            string.IsNullOrEmpty(_state.node)?(!_answeredSoi?"소이에게 대답하기":!_bookSeen?"식탁의 스케치북 살펴보기":"라디오에 귀 기울이기"):_graph.Node(_state.node).title;
        IEnumerator RestoreJourney()
        {
            _answeredSoi=_state.answeredSoi;_bookSeen=_state.bookSeen;
            foreach(var reveal in _script.nameReveals)_identity.OnLineFinished(reveal.afterLine);
            gameRoot.alpha=1;_bookShot.alpha=0;photoWall.alpha=0;memoryPath.alpha=0;present.alpha=1;
            callOverlay.alpha=1;callText.Clear();bottomDim.alpha=0;dialogue.HideImmediate();
            screenFade.alpha=1;
            if(_state.day>0)
            {
                if(SearchSession.Active(_state))
                {
                    SearchSession.Finish(_state,"interrupted");SaveSystem.AutoSave(_state,out _);
                    if(AwayFromCamper)SetRegionView(_state.exploringPlace);else SetJourneyImage(_state.inStore?"store":"journal");
                    yield return Tween.Fade(screenFade,0,.7f);yield return EnterExploration(false);
                    OpenPlaceSearch(_state.search.place,_state.search.point);yield break;
                }
                if(BanditEncounter.Active(_state)){yield return EnterExploration(false);yield break;}
                if(!string.IsNullOrEmpty(_state.weekEvent)){yield return PlayWeekEvent(_state.weekEvent,true);yield break;}
                if(AwayFromCamper)SetRegionView(_state.exploringPlace);else SetJourneyImage(_state.inStore?"store":"journal");
                yield return Tween.Fade(screenFade,0,.7f);yield return EnterExploration(false);
            }
            else if(!string.IsNullOrEmpty(_state.node))yield return ContinueStory(_state.node,true);
            else if(_state.bookSeen)yield return ContinueStory("camp_radio");
            else {yield return Tween.Fade(screenFade,0,.7f);yield return EnterExploration(false);}
        }
        void RefreshJourneyHud()
        {
            var hud=GameHud.Instance;
            hud.ActivitiesChanged=()=>{RefreshJourneyHud();RefreshLifeStage();};
            hud.ConfigureBag(_state.Bag(_identity.LabelFor("suhyeok"),_identity.LabelFor("soi")));hud.ConfigureActivities();
            ConfigureRegionMap();
            hud.SetExplorationContext(AwayFromCamper?RegionExploration.Find(_state.exploringPlace).ShortName:_state.inStore?"작은 편의점":"캠핑카",_state.day==0?"밤":_state.day+"일 차 · "+(RegionExploration.Outside(_state)?"주변 탐색":_state.location!="camper"?RegionExploration.ParkingName(_state):"아침"),Goal);
            bool ready=_state.day==0?_state.node=="sleep"&&_state.line>=_graph.Node("sleep").lines.Length:_state.day<49&&!_state.inStore&&!AwayFromCamper&&_state.Has("water_checked")&&_state.Has("food_checked");
            hud.ConfigureDayEnd(_state.day==0?"출발 준비와 첫 기록":_state.day+"일 차 · 확보한 물과 식량",()=>ready?null:(_state.inStore||AwayFromCamper)?"캠핑카로 돌아온 뒤 쉬어요.":_state.day==0?"남아 있는 준비를 마친 뒤 쉬어요.":"오늘 필요한 물과 식량을 먼저 살펴봐요.",()=>{if(_state.day==0)StartCoroutine(ContinueStory("night"));else StartCoroutine(FinishDay());});
            _interactions.gameObject.SetActive(string.IsNullOrEmpty(_state.node)&&!_state.inStore&&_state.day==0);
            RenderJourneyActions();
        }
        void SetJourneyImage(string scene)
        {
            if(_banditStage!=null)_banditStage.gameObject.SetActive(false);
            if(scene=="journal"&&_state.day>0&&string.IsNullOrEmpty(_state.weekEvent))
                scene=_state.Has("dog_place_ready")?"life-E14-HUB-rest":_state.Has("life.meal."+_state.day)?"life-E03-HUB-rations-shared":scene;
            if(_weekDisplay!=null)_weekDisplay.gameObject.SetActive(false);
            if(_weekProp!=null)_weekProp.gameObject.SetActive(false);
            if(_regionOverview!=null)_regionOverview.gameObject.SetActive(false);
            var sprite=Resources.Load<Sprite>("Live49/Stages/"+scene);
            if(sprite==null)throw new InvalidOperationException("Missing continuation art: "+scene);
            if(_journeyImage==null)
            {
                var r=PanelUI.Rect(present.transform,"JourneyStage",0,0,1920,1080);
                r.gameObject.SetActive(false);
                r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.anchoredPosition=Vector2.zero;
                _journeyImage=r.gameObject.AddComponent<Image>();_journeyImage.raycastTarget=false;
                _presentLayers[0].SetBlur(0);
                _journeyFX=r.gameObject.AddComponent<StageImageFX>();_journeyFX.UseBaseMaterial(_presentLayers[0].GetComponent<Image>().material);
                r.gameObject.SetActive(true);
            }
            _journeyImage.gameObject.SetActive(true);_journeyImage.sprite=sprite;_journeyFX.SetBlur(0);_sceneId=scene;
            ShowWeekProp(scene);
        }
        string SceneFor(StoryNode node)
        {
            if(node.scene!="preparation")return node.scene;
            return _state.Has("water_packed")?(_state.Has("blanket_packed")?"packed":"water_only"):(_state.Has("blanket_packed")?"blanket_only":"current");
        }
        public void ChooseStory(int index)
        {
            if(_interactionBusy||GameHud.Instance.IsPaused||!GameHud.Instance.IsExploring||string.IsNullOrEmpty(_state.node))return;
            var node=_graph.Node(_state.node);var choices=_graph.Choices(node,_state);
            if(index<0||index>=choices.Length||_state.line<node.lines.Length)return;
            if(_state.node=="sleep"){GameHud.Instance.RequestDayEnd();return;}
            FinishNode(node);ContinuationGraph.Effects(choices[index].effects,_state);
            StartCoroutine(ContinueStory(choices[index].target));
        }
        void FinishNode(StoryNode node)
        {
            if(_state.done.Contains(node.id))return;
            ContinuationGraph.Effects(node.effects,_state);_state.done.Add(node.id);
        }
        IEnumerator ContinueStory(string id,bool resume=false)
        {
            if(_interactionBusy)yield break;_interactionBusy=true;GameHud.Instance.SetNarrativeMode();
            while(true)
            {
                id=_graph.Resolve(id,_state);var node=_graph.Node(id);
                bool restoredNode=resume&&_state.node==id;
                if(!restoredNode)_state.line=0;resume=false;_state.node=id;
                if(!restoredNode)SaveSystem.AutoSave(_state,out _,false);
                string scene=SceneFor(node);
                if(_sceneId!=scene)
                {
                    yield return dialogue.FadePanel(false,.35f);
                    yield return Tween.Fade(screenFade,1,id=="night"?.8f:.4f);
                    SetJourneyImage(scene);yield return Tween.Wait(.25f);yield return Tween.Fade(screenFade,0,.7f);
                }
                for(;_state.line<node.lines.Length;)
                {
                    var source=node.lines[_state.line];string who=source.speaker=="수혁"?"suhyeok":source.speaker=="소이"?"soi":source.speaker=="라디오"?"radio":"";
                    var line=new DialogueLine{id=node.id+"_"+_state.line,kind=who==""?"narration":"dialogue",speakerId=who,text=source.text.Replace("{journal_first_line}",_state.journal),portraits=who=="radio"||who==""?Array.Empty<string>():new[]{"suhyeok","soi"}};
                    yield return PlayLine(line,new[]{_journeyFX});
                    _state.line++;SaveSystem.AutoSave(_state,out _,false);
                }
                if(node.lines.Length>0)FinishNode(node);
                if(id=="day1")
                {
                    _state.day=1;_state.minutes=0;_state.inStore=false;
                    yield return EnterExploration();
                    yield break;
                }
                if(node.choices.Length>0||string.IsNullOrEmpty(node.next))
                {yield return EnterExploration();yield break;}
                id=node.next;
            }
        }
        void RenderJourneyActions()
        {
            if(_journeyActions==null){_journeyActions=PanelUI.Rect(GameHud.Instance.InteractionRoot,"JourneyActions",90,244,720,620);_journeyActions.SetAsFirstSibling();_journeyActions.gameObject.AddComponent<ExplorationOnly>();}
            PanelUI.Clear(_journeyActions);
            if(_state.day>0)
            {
                if(AwayFromCamper){RenderRegionActions();return;}
                if(_state.inStore)
                {
                    if(!_state.Has("water_checked"))ActionButton("StoreWater","냉장고 · 생수 살펴보기",0,()=>InspectSupplies(true));
                    if(!_state.Has("food_checked"))ActionButton("StoreFood","식품 선반 · 식량 살펴보기",1,()=>InspectSupplies(false));
                    ActionButton("OpenRegionMap","지도를 펼쳐 다음 장소 고르기",2,()=>GameHud.Instance.OpenMap());
                    ActionButton("ReturnCamper",RegionExploration.ParkingName(_state)+" 캠핑카로 돌아가기",3,()=>StartCoroutine(ReturnFromRegion()));
                }
                else
                {
                    if(RegionExploration.Find(_state.location)!=null)ActionButton("LeaveCamper","캠핑카에서 내려 "+RegionExploration.Find(_state.location).ShortName+" 둘러보기",0,()=>StartCoroutine(LeaveCamper()));
                    else ActionButton("OpenJourneyMap","주변 지도 펼치기",0,()=>GameHud.Instance.OpenMap());
                    ActionButton("OpenKitchen","주방 살펴보기",1,()=>GameHud.Instance.OpenActivity(ActivityPanel.Kind.Kitchen));
                    ActionButton("OpenJournal","오늘의 기록 보기",2,()=>GameHud.Instance.OpenActivity(ActivityPanel.Kind.Journal));
                }
                RenderWeekAction(3);
                if(!_state.inStore)ActionButton("OpenCampLife","식탁 · 설비 · 오늘의 한 장",FirstWeekStory.Action(_state)!=null||_state.Has(RegionTravel.Drawing)?4:3,()=>GameHud.Instance.OpenActivity(ActivityPanel.Kind.Life));
                else ActionButton("OpenLifeSupplies","주변 물자와 생활 흔적",4,()=>GameHud.Instance.OpenActivity(ActivityPanel.Kind.Supplies));
                return;
            }
            if(string.IsNullOrEmpty(_state.node))return;
            var node=_graph.Node(_state.node);var choices=_graph.Choices(node,_state);
            for(int i=0;i<choices.Length;i++){int index=i;ActionButton("StoryChoice_"+i,choices[i].label,i,()=>ChooseStory(index));}
        }
        void ActionButton(string name,string label,int index,Action action)
        {
            var button=PanelUI.Button(_journeyActions,Font,name,label,0,index*86,650,74,()=>{if(!_interactionBusy&&!GameHud.Instance.IsPaused)action();});
            button.GetComponent<Image>().color=new Color32(31,28,23,243);
            PanelUI.Box(button.transform,"Accent",0,0,2,74,PanelUI.Gold);
        }
        IEnumerator TravelToStore()
        {
            yield return TravelToRegion("L1");
        }
        IEnumerator EnterStore(bool enter)
        {
            if(enter)yield return LeaveCamper();else yield return ReturnFromRegion();
        }
        void InspectSupplies(bool water)=>OpenPlaceSearch("L1",water?0:1);
        void OpenPlaceSearch(string place,int point)
        {
            if(!SearchSession.Active(_state)&&!SearchSession.CanBegin(_state,place,point))return;
            GameHud.Instance.OpenSearch(place,point,back=>
            {
                RefreshJourneyHud();
                if(back)StartCoroutine(ReturnFromRegion());
            });
        }
        IEnumerator FinishDay()
        {
            if(_interactionBusy||_state.inStore||AwayFromCamper||_state.day>=49)yield break;
            _interactionBusy=true;GameHud.Instance.SetNarrativeMode();
            yield return Tween.Fade(screenFade,1,.8f);yield return Tween.Wait(1);
            _state.day++;_state.minutes=0;_state.Set("day_"+(_state.day-1)+"_completed");
            SetJourneyImage("journal");yield return Tween.Fade(screenFade,0,1);
            yield return EnterExploration();
        }
    }
}
