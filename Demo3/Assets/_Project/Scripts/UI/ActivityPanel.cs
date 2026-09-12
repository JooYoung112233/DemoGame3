using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Live49.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Live49.UI
{
    // Kitchen and journal sheets share the HUD's pause ownership.
    public partial class ActivityPanel : MonoBehaviour
    {
        public enum Kind { Kitchen, Journal, Life, Supplies, Evening }
        TMP_FontAsset _font;CanvasGroup _group;RectTransform _body;
        TMP_Text _title,_subtitle,_phase,_heatText,_status,_resource,_result;
        RectTransform _heatFill,_pot;Button _start,_close,_pauseCook;
        readonly List<Button> _heatButtons=new List<Button>();
        JourneyState _state;CookingSession _cook;Action _closed;Kind _kind;
        bool _closing;Coroutine _fade;
        public bool IsOpen=>gameObject.activeSelf;
        public bool CookingActive=>_cook!=null&&_cook.Active;
        public CookingSession Cooking=>_cook;
        public static ActivityPanel Create(Transform parent,TMP_FontAsset font,Action close)
        {
            var root=PanelUI.Box(parent,"ActivityPanel",0,0,1920,1080,new Color(0,0,0,.9f),true);
            var p=root.gameObject.AddComponent<ActivityPanel>();p._font=font;p._closed=close;p._group=root.gameObject.AddComponent<CanvasGroup>();
            PanelUI.Text(root,font,"Eyebrow","캠핑카에서 보내는 시간",90,64,1200,30,18,PanelUI.Gold);
            p._title=PanelUI.Text(root,font,"ActivityTitle","",85,103,1300,70,48);
            p._subtitle=PanelUI.Text(root,font,"ActivitySubtitle","",90,179,1300,40,23,PanelUI.Muted);
            p._close=PanelUI.Button(root,font,"CloseActivity","닫기",1680,105,150,64,()=>p.RequestClose());
            p._body=PanelUI.Rect(root,"ActivityBody",90,244,1740,730);
            root.gameObject.SetActive(false);return p;
        }
        public void Open(Kind kind,JourneyState state,string objective)
        {
            _state=state;_kind=kind;_closing=false;gameObject.SetActive(true);transform.SetAsLastSibling();
            PanelUI.Clear(_body);_heatButtons.Clear();
            if(kind==Kind.Kitchen)BuildKitchen();else if(kind==Kind.Journal)BuildJournal(objective);else BuildLife(kind==Kind.Supplies?"supplies":kind==Kind.Evening?"evening":"meal");
            EventSystem.current?.SetSelectedGameObject(_close.gameObject);
            _fade=StartCoroutine(Fade(0,1,.22f));
        }
        void BuildKitchen()
        {
            _title.text="작은 주방";_subtitle.text="불을 조절하며, 따뜻한 한 끼를 준비해요.";
            if(_cook==null||!ReferenceEquals(_cookState,_state)||_cook.Stage==CookingSession.Phase.Done){_cook=new CookingSession(_state);_cookState=_state;}
            var left=PanelUI.Box(_body,"RecipeCard",0,0,620,710,PanelUI.Ink);
            PanelUI.Text(left,_font,"RecipeHeading","가스레인지",38,28,544,35,19,PanelUI.Gold);
            PanelUI.Text(left,_font,"RecipeName","냄비 한 끼",36,86,548,68,42);
            PanelUI.Text(left,_font,"RecipeDescription","가스와 식재료로 만드는 식사.\n약불·중불·강불로 냄비의 열기를 조절해요.",38,178,544,100,24,PanelUI.Muted);
            PanelUI.Box(left,"Rule",38,295,544,1,PanelUI.Gold);
            _resource=PanelUI.Text(left,_font,"RecipeCosts","",38,326,544,126,25);
            PanelUI.Text(left,_font,"RecipeYield","완성  ·  따뜻한 식사 1\n조리 시간  ·  25분",38,480,544,88,24,PanelUI.Muted);
            _start=PanelUI.Button(left,_font,"StartCooking","조리 시작",38,605,544,68,()=>{if(_cook.Start()){PersistActivity();RefreshKitchen();}});
            var right=PanelUI.Box(_body,"Stove",654,0,1086,710,PanelUI.Ink);
            _phase=PanelUI.Text(right,_font,"CookingPhase","",40,28,1006,40,27,PanelUI.Gold);
            _pot=PanelUI.Rect(right,"Pot",380,121,326,238);
            var icon=PanelUI.Rect(_pot,"CookingIcon",0,0,326,238).gameObject.AddComponent<HudIcon>();icon.Symbol=HudIcon.Kind.Cooking;icon.color=PanelUI.Gold;icon.raycastTarget=false;
            _heatText=PanelUI.Text(right,_font,"HeatText","",40,370,1006,38,24);
            var track=PanelUI.Box(right,"HeatTrack",40,421,1006,14,new Color(1,1,1,.1f));
            PanelUI.Box(track,"IdealRange",1006*62/120f,-5,1006*16/120f,24,new Color(.5f,.65f,.5f,.24f));
            _heatFill=PanelUI.Box(track,"Fill",0,0,0,14,PanelUI.Gold);
            _status=PanelUI.Text(right,_font,"CookingStatus","",40,460,1006,61,23,PanelUI.Muted);
            for(int i=0;i<3;i++){int level=i+1;_heatButtons.Add(PanelUI.Button(right,_font,"Heat_"+level,new[]{"약불","중불","강불"}[i],40+i*224,548,208,68,()=>{_cook.Select(level);RefreshKitchen();}));}
            _pauseCook=PanelUI.Button(right,_font,"PauseCooking","잠시 멈춤",744,548,300,68,()=>{if(_cook.Active)_cook.Paused=!_cook.Paused;RefreshKitchen();});
            _result=PanelUI.Text(right,_font,"CookingResult","",40,641,1006,47,21,PanelUI.Muted);
            RefreshKitchen();
        }
        JourneyState _cookState;
        void RefreshKitchen()
        {
            if(_kind!=Kind.Kitchen||!IsOpen)return;
            bool done=_cook.Stage==CookingSession.Phase.Done;
            _resource.text="가스  1  /  보유 "+(_state.inventoryKnown?_state.Count("gas").ToString():"확인 전")+"\n식재료  1  /  보유 "+(_state.inventoryKnown?_state.Count("ingredients").ToString():"확인 전");
            _start.interactable=_cook.Stage==CookingSession.Phase.Ready&&_cook.BlockReason()==null;
            _start.GetComponentInChildren<TMP_Text>().text=done?"조리 완료":_cook.Active?"조리 중":"조리 시작";
            _phase.text=done?"불을 끄고, 그릇에 담아요.":_cook.Paused?"잠시 멈춤":_cook.Stage==CookingSession.Phase.Warming?"01  천천히 데우기":_cook.Stage==CookingSession.Phase.Simmering?"02  잔잔하게 익히기":"조리 준비";
            _heatText.text="냄비의 열기  ·  "+Mathf.RoundToInt(_cook.Heat);
            _heatFill.sizeDelta=new Vector2(1006*Mathf.Clamp01(_cook.Heat/120),14);
            _status.text=done?(_cook.Great?"잔잔하게 끓인 따뜻한 식사가 완성됐어요.":"남은 익힘을 마무리해 한 끼를 완성했어요."):_cook.Stage==CookingSession.Phase.Ready?(_cook.BlockReason()??"준비됐어요. 시작하면 재료를 한 번 사용해요."):_cook.Paused?"준비가 되면 이어서 조리해요.":_cook.Stage==CookingSession.Phase.Warming?"열기가 서서히 올라와요. 끓기 시작하면 불을 낮춰요.":"표시된 구간의 열기를 유지해요. "+Mathf.CeilToInt(10-_cook.Simmer)+"초";
            for(int i=0;i<3;i++){_heatButtons[i].interactable=_cook.Active&&!_cook.Paused;_heatButtons[i].GetComponent<Image>().color=_cook.Level==i+1?PanelUI.Gold:new Color(1,1,1,.08f);}
            _pauseCook.interactable=_cook.Active;_pauseCook.GetComponentInChildren<TMP_Text>().text=_cook.Paused?"이어서 조리":"잠시 멈춤";
            _result.text=done?"따뜻한 식사 1개를 가방에 담았어요.":_cook.Active?"창을 닫으면 조리를 멈추고, 다시 열면 이어서 할 수 있어요.":"가스와 차량의 연료는 따로 보관해요.";
        }
        void Update()
        {
            if(_closing)return;
            if(_kind==Kind.Life||_kind==Kind.Supplies||_kind==Kind.Evening){TickLife();return;}
            if(_closing||_kind!=Kind.Kitchen||_cook==null)return;
            bool cooking=_cook.Active;
            _cook.Tick(Time.unscaledDeltaTime);
            if(cooking){_lifeSaveClock+=Time.unscaledDeltaTime;if(!_cook.Active||_lifeSaveClock>=1){PersistActivity(!_cook.Active);_lifeSaveClock=0;}}
            var keyboard=Keyboard.current;
            if(keyboard!=null){if(keyboard.digit1Key.wasPressedThisFrame)_cook.Select(1);if(keyboard.digit2Key.wasPressedThisFrame)_cook.Select(2);if(keyboard.digit3Key.wasPressedThisFrame)_cook.Select(3);}
            if(_cook.Active)_pot.localScale=Vector3.one*(1+Mathf.Sin(Time.unscaledTime*3)*.006f);
            RefreshKitchen();
        }
        void OnApplicationFocus(bool focus){if(!focus&&CookingActive)_cook.Paused=true;}
        void BuildJournal(string objective)
        {
            _title.text="남겨 둔 기록";_subtitle.text="지나온 순간과, 지금 해야 할 일을 한곳에.";
            var left=PanelUI.Box(_body,"JournalPage",0,0,1038,710,new Color32(218,202,166,255));
            var ink=new Color32(67,57,42,255);
            PanelUI.Text(left,_font,"JournalDay",_state.day>0?_state.day+"일 차 · 오늘의 기록":"첫 기록 · 떠나기 전의 밤",42,32,950,40,23,ink);
            PanelUI.Text(left,_font,"JournalEntry",_state.day>0?JourneyDayLog.Summary(_state):string.IsNullOrEmpty(_state.journal)?"아직 적어 둔 문장이 없다.":_state.journal,42,125,950,300,_state.day>0?25:33,ink);
            if(_state.day>0)
            {
                PanelUI.Text(left,_font,"RegionProgress",RegionTravel.Name(RegionTravel.Current(_state))+" 탐색  "+RegionExploration.CompletedCount(_state)+" / "+RegionExploration.ActiveSites(_state).Length,42,448,950,45,25,ink);
                var names=RegionExploration.ActiveSites(_state).Where(s=>RegionExploration.Completed(_state,s.Id)).Select(s=>s.ShortName);
                PanelUI.Text(left,_font,"ExploredPlaces",string.Join(" · ",names),42,505,950,112,22,ink);
                var dates=new[]{0}.Concat((_state.days??new System.Collections.Generic.List<JourneyDayRecord>()).Select(d=>d.day)).Concat(new[]{_state.day}).Distinct().OrderBy(d=>d).ToArray();
                int page=dates.Length-1;Button previous=null,next=null;
                System.Action refresh=()=>
                {
                    int day=dates[page];
                    left.Find("JournalDay").GetComponent<TMP_Text>().text=day==0?"첫 기록 · 떠나기 전의 밤":day+"일 차 · 남겨 둔 기록";
                    left.Find("JournalEntry").GetComponent<TMP_Text>().text=day==0?(string.IsNullOrEmpty(_state.journal)?"아직 적어 둔 문장이 없다.":_state.journal):JourneyDayLog.Summary(_state,day);
                    previous.interactable=page>0;next.interactable=page<dates.Length-1;
                };
                previous=PanelUI.Button(left,_font,"PreviousJournal","이전 기록",42,640,240,44,()=>{page--;refresh();});
                next=PanelUI.Button(left,_font,"NextJournal","다음 기록",300,640,240,44,()=>{page++;refresh();});
                previous.interactable=page>0;next.interactable=false;
            }
            else PanelUI.Text(left,_font,"JournalClosing",_state.Has("water_packed")?"내일 쓸 물을 더 찾아야 한다.":"기억하고 싶은 순간을 천천히 남긴다.",42,569,950,87,24,ink);
            var right=PanelUI.Box(_body,"Objectives",1070,0,670,710,PanelUI.Ink);
            PanelUI.Text(right,_font,"ObjectiveHeading",(_state.day==0?"":_state.day+"일 차 · ")+"지금 할 일",34,28,600,38,20,PanelUI.Gold);
            PanelUI.Text(right,_font,"CurrentObjective",objective,34,87,602,113,32);
            PanelUI.Box(right,"Rule",34,220,602,1,PanelUI.Gold);
            string[] labels={"소이에게 대답하기","스케치북 살펴보기","출발 준비","실내등 복구","첫 기록 남기기","편의점에서 물 찾기"};
            bool[] completed={_state.answeredSoi,_state.bookSeen,_state.Has("water_packed")&&_state.Has("blanket_packed"),_state.Has("light_restored"),!string.IsNullOrEmpty(_state.journal),_state.Has("water_checked")};
            if(_state.day>0)
            {
                labels=new[]{"물과 식량 확보","책 곁에 색연필 놓기","첫 그림 완성","주유소에서 길 알아보기","공구 가방 전달 · 카메라 받기","첫 사진 촬영 · 앨범 확인"};
                completed=new[]{RegionExploration.Completed(_state,"L1"),_state.Has("pencils_delivered"),_state.Has(RegionTravel.Drawing),_state.Has(RegionTravel.Route),_state.Has("camera_received"),_state.Has(RegionTravel.Photo)};
            }
            for(int i=0;i<labels.Length;i++)PanelUI.Text(right,_font,"Task_"+i,(completed[i]?"완료 · ":"미완료 · ")+labels[i],34,253+i*60,602,45,24,completed[i]?PanelUI.Gold:PanelUI.Muted);
            if(_state.day>0)
            {
                var memory=PanelUI.Text(left,_font,"SavedMemory","오늘 남긴 한 장 · "+(_state.Has("memory."+_state.day)?_state.Value("memory.title."+_state.day):"선택하지 않음"),42,75,950,44,22,ink);
                var previous=left.Find("PreviousJournal")?.GetComponent<Button>();var next=left.Find("NextJournal")?.GetComponent<Button>();
                Action sync=()=>{var heading=left.Find("JournalDay").GetComponent<TMP_Text>().text;int day;if(int.TryParse(heading.Split('일')[0],out day))memory.text="남긴 한 장 · "+(_state.Has("memory."+day)?_state.Value("memory.title."+day):"선택하지 않음");else memory.text="";};
                previous?.onClick.AddListener(()=>sync());next?.onClick.AddListener(()=>sync());
            }
        }
        public void RequestClose(){if(!_closing){if(CookingActive)_cook.Paused=true;PersistActivity();_closed();}}
        public void Close(Action completed)
        {if(_closing)return;_closing=true;if(CookingActive)_cook.Paused=true;if(_fade!=null)StopCoroutine(_fade);StartCoroutine(CloseRoutine(completed));}
        IEnumerator CloseRoutine(Action completed){yield return Fade(_group.alpha,0,.16f);gameObject.SetActive(false);_closing=false;completed();}
        IEnumerator Fade(float from,float to,float duration){for(float t=0;t<duration;t+=Time.unscaledDeltaTime){_group.alpha=Mathf.Lerp(from,to,Mathf.SmoothStep(0,1,t/duration));yield return null;}_group.alpha=to;}
    }
}
