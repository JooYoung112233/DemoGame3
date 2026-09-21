using System;
using System.Collections;
using System.Linq;
using Live49.Core;
using Live49.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Live49.Chapter00
{
    public partial class C0OpeningDirector
    {
        RawImage _regionOverview;
        RectTransform _regionFocus;
        bool AwayFromCamper=>!string.IsNullOrEmpty(_state.exploringPlace);
        string RegionGoal=>FirstWeekStory.Goal(_state)??(!RegionExploration.Completed(_state,"L1")?(_state.Has("water_checked")?"편의점에서 식량 확보하기":"편의점에서 물 확보하기"):
            AwayFromCamper&&!RegionExploration.Completed(_state,_state.exploringPlace)?RegionExploration.Find(_state.exploringPlace).ShortName+" 주변 살펴보기":
            RegionExploration.CompletedCount(_state)==RegionExploration.ActiveSites(_state).Length?"캠핑카에서 다음 여정 준비하기":"지도에서 새로 열린 장소 탐색하기");
        void ConfigureRegionMap()
        {
            var places=FirstRegionMap.Create(_state);
            GameHud.Instance.ConfigureMap(places,id=>RegionExploration.TravelBlock(_state,id),id=>StartCoroutine(TravelToRegion(id)));
            GameHud.Instance.Map.SetLocations(_state.location,RegionExploration.PlayerPlace(_state),RegionExploration.Outside(_state));
            GameHud.Instance.Map.ConfigureDiscovery(id=>{_state.Set("map.seen."+id);SaveSystem.AutoSave(_state,out _,false);});
            GameHud.Instance.Map.ConfigureRegion(RegionTravel.Current(_state),()=>GameHud.Instance.CloseMap(OpenRegionSelection));
        }
        void OpenRegionSelection()
        {
            string current=RegionTravel.Current(_state);
            GameHud.Instance.ShowInteraction("지나온 길과 다음 길","현재 지역 · "+RegionTravel.Name(current)+"\n지역 이동은 캠핑카로 진입로에서 출발해요.",new[]{"첫 동네"+(current==RegionExploration.RegionId?" · 현재 지역":"로 돌아가기"),"고개 너머"+(current==RegionTravel.NextRegion?" · 현재 지역":" · 출발 준비")},index=>
            {
                var target=index==0?RegionExploration.RegionId:RegionTravel.NextRegion;
                if(target==current){GameHud.Instance.OpenMap();return;}
                var blocked=RegionTravel.Block(_state,target);
                if(blocked!=null){GameHud.Instance.ShowInteraction("출발 전에 확인해요",blocked,context:"지역 이동");return;}
                GameHud.Instance.ShowInteraction(RegionTravel.Name(target)+"로 이동","차량 이동 · 40분 / 연료 1\n탐색 기록과 소지품은 이어서 가져가요.",new[]{"준비를 마치고 출발"},_=>StartCoroutine(CrossRegion(target)),"지역 이동");
            },"지역 이동");
        }
        IEnumerator CrossRegion(string target)
        {
            if(_interactionBusy||RegionTravel.Block(_state,target)!=null)yield break;
            _interactionBusy=true;GameHud.Instance.SetNarrativeMode();yield return Tween.Fade(screenFade,1,.8f);
            RegionTravel.Travel(_state,target);SetJourneyImage("journal");
            yield return GameHud.Instance.ShowArrival(RegionTravel.Name(target),"캠핑카와 기록을 함께 가져왔어요");
            yield return Tween.Fade(screenFade,0,.85f);yield return EnterExploration();
        }
        void SetRegionView(string id)
        {
            if(_banditStage!=null)_banditStage.gameObject.SetActive(false);
            if(_weekDisplay!=null)_weekDisplay.gameObject.SetActive(false);
            if(_weekProp!=null)_weekProp.gameObject.SetActive(false);
            var site=RegionExploration.Find(id);
            if(id=="L5"&&!_state.Has("dog_joined")&&_state.Has("dog_seen"))
            {SetJourneyImage("life-"+(_state.Has("dog_relaxed")?"03-L5-relaxed":_state.Has("dog_care_offered")?(_state.Value("dog_care_kind")=="water"?"02-L5-water-placed":"E12-L5-food-alternative"):"01-L5-wary"));return;}
            if(id=="L2"&&_state.Has(RegionTravel.Drawing)){SetJourneyImage("week-E07-L2-sejin-repair");return;}
            if(id=="L4"){SetJourneyImage(_state.Has("toolbag_collected")?"week-L4-toolbag-collected":"week-L4-toolbag-available");return;}
            if(!string.IsNullOrEmpty(site.Art)){SetJourneyImage(site.Art);return;}
            // Keep a map fallback for future locations without authored stage art.
            if(_regionOverview==null)
            {
                var rt=PanelUI.Rect(present.transform,"RegionOverview",0,0,1920,1080);
                rt.anchorMin=rt.anchorMax=rt.pivot=Vector2.one*.5f;rt.anchoredPosition=Vector2.zero;
                _regionOverview=rt.gameObject.AddComponent<RawImage>();_regionOverview.raycastTarget=false;
                _regionOverview.texture=Resources.Load<Texture2D>("Live49/Maps/city-map-soft-v2");
                _regionOverview.color=new Color(.65f,.65f,.63f,1);
                _regionFocus=PanelUI.Rect(rt,"ExplorationFocus",0,0,0,0);
                PanelUI.Box(_regionFocus,"Top",-42,-34,84,2,PanelUI.Gold);
                PanelUI.Box(_regionFocus,"Bottom",-42,32,84,2,PanelUI.Gold);
                PanelUI.Box(_regionFocus,"Left",-42,-34,2,68,PanelUI.Gold);
                PanelUI.Box(_regionFocus,"Right",40,-34,2,68,PanelUI.Gold);
            }
            var p=site.Point;
            var uv=new Rect(Mathf.Clamp(p.x/1220-.23f,0,.54f),Mathf.Clamp(1-p.y/792-.2f,0,.6f),.46f,.4f);
            _regionOverview.uvRect=uv;
            _regionFocus.anchoredPosition=new Vector2((p.x/1220-uv.x)/uv.width*1920,-(1-(1-p.y/792-uv.y)/uv.height)*1080);
            _regionOverview.gameObject.SetActive(true);_regionOverview.transform.SetAsLastSibling();
            if(_journeyImage!=null)_journeyImage.gameObject.SetActive(false);
        }
        IEnumerator TravelToRegion(string id)
        {
            if(_interactionBusy||RegionExploration.TravelBlock(_state,id)!=null)yield break;
            _interactionBusy=true;GameHud.Instance.SetNarrativeMode();
            yield return Tween.Fade(screenFade,1,.55f);yield return Tween.Wait(.3f);
            RegionExploration.Travel(_state,id);
            if(_state.inStore)SetJourneyImage("store");else if(AwayFromCamper)SetRegionView(id);else SetJourneyImage("journal");
            yield return GameHud.Instance.ShowArrival(RegionExploration.Find(id).ShortName,RegionExploration.Outside(_state)?"걸어서 도착했어요":"캠핑카를 새로 주차했어요");
            yield return Tween.Fade(screenFade,0,.7f);yield return EnterExploration();
        }
        void SaveJourney(){SaveSystem.AutoSave(_state,out _);}
        IEnumerator LeaveCamper()
        {
            if(_interactionBusy||RegionExploration.Outside(_state)||RegionExploration.Find(_state.location)==null)yield break;
            _interactionBusy=true;GameHud.Instance.SetNarrativeMode();yield return Tween.Fade(screenFade,1,.4f);
            _state.inStore=_state.location=="L1";_state.exploringPlace=_state.inStore?"":_state.location;
            JourneyDayLog.Visit(_state,_state.location);
            if(_state.inStore)SetJourneyImage("store");else SetRegionView(_state.location);
            yield return Tween.Fade(screenFade,0,.6f);yield return EnterExploration();
        }
        IEnumerator ReturnFromRegion()
        {
            if(_interactionBusy||!RegionExploration.Outside(_state))yield break;
            _interactionBusy=true;GameHud.Instance.SetNarrativeMode();
            yield return Tween.Fade(screenFade,1,.4f);_state.minutes+=RegionExploration.PlayerPlace(_state)==_state.location?0:10;_state.exploringPlace="";_state.inStore=false;
            SetJourneyImage("journal");yield return Tween.Fade(screenFade,0,.6f);yield return EnterExploration();
        }
        void RenderRegionActions()
        {
            var site=RegionExploration.Find(_state.exploringPlace);
            var points=PlaceInspection.Points(site.Id);
            for(int i=0;i<points.Length;i++){int index=i;ActionButton("InspectRegion_"+i,(PlaceInspection.Checked(_state,site.Id,i)?"확인 · ":"")+points[i].Label,i,()=>InspectRegion(index));}
            ActionButton("OpenRegionMap","지도를 펼쳐 다음 장소 고르기",2,()=>GameHud.Instance.OpenMap());
            ActionButton("ReturnFromRegion",RegionExploration.ParkingName(_state)+" 캠핑카로 돌아가기",3,()=>StartCoroutine(ReturnFromRegion()));
            RenderWeekAction(4);
            ActionButton("OpenLifeSupplies","주변 물자와 생활 흔적",FirstWeekStory.Action(_state)!=null?5:4,()=>GameHud.Instance.OpenActivity(ActivityPanel.Kind.Supplies));
        }
        void InspectRegion(int index)
        {
            if(!AwayFromCamper||_interactionBusy||GameHud.Instance.IsPaused)return;
            var id=_state.exploringPlace;var site=RegionExploration.Find(id);var point=PlaceInspection.Points(id)[index];
            if(PlaceInspection.Checked(_state,id,index)){GameHud.Instance.ShowInteraction(point.Label,point.Finding,context:site.ShortName);return;}
            OpenPlaceSearch(id,index);
        }
        void ShowRegionResult(string id,string[] revealed)
        {
            SaveJourney();
            var site=RegionExploration.Find(id);
            string message=revealed.Length>0?"새로 탐색할 수 있는 곳\n"+string.Join(" · ",revealed.Select(p=>RegionExploration.Find(p).ShortName)):site.Finding;
            GameHud.Instance.ShowInteraction(revealed.Length>0?"새로운 길을 찾았어요":"탐색 완료",message,new[]{"지도에서 확인하기"},_=>GameHud.Instance.OpenMap(),site.ShortName);
        }
    }
}
