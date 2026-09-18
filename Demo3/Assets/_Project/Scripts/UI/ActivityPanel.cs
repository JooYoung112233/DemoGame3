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
            PanelUI.Clear(_body);
            if(kind==Kind.Kitchen)BuildKitchen();else if(kind==Kind.Journal)BuildJournal(objective);else BuildLife(kind==Kind.Supplies?"supplies":kind==Kind.Evening?"evening":"meal");
            EventSystem.current?.SetSelectedGameObject(_close.gameObject);
            _fade=StartCoroutine(Fade(0,1,.22f));
        }
        void Update()
        {
            if(_closing)return;
            if(_kind==Kind.Life||_kind==Kind.Supplies||_kind==Kind.Evening){TickLife();return;}
            if(_closing||_kind!=Kind.Kitchen||_cook==null)return;
            bool cooking=_cook.Active;
            _cook.Tick(Time.unscaledDeltaTime);
            if(cooking&&!_cook.Paused){_kitchenClock+=Time.unscaledDeltaTime;_lifeSaveClock+=Time.unscaledDeltaTime;if(!_cook.Active||_lifeSaveClock>=1){SaveKitchen(!_cook.Active);_lifeSaveClock=0;}}
            var keyboard=Keyboard.current;
            if(keyboard!=null){if(keyboard.digit1Key.wasPressedThisFrame)SelectKitchenHeat(1);if(keyboard.digit2Key.wasPressedThisFrame)SelectKitchenHeat(2);if(keyboard.digit3Key.wasPressedThisFrame)SelectKitchenHeat(3);}
            RefreshKitchen();
        }
        void OnApplicationFocus(bool focus){if(!focus&&CookingActive){_cook.Paused=true;if(_kind==Kind.Kitchen){SaveKitchen();RefreshKitchen();}}}
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
        public void RequestClose(){if(!_closing){if(CookingActive)_cook.Paused=true;if(_kind==Kind.Kitchen){SaveKitchen();if(_kitchenSaveFailed){RefreshKitchen();return;}}else PersistActivity();_closed();}}
        public void Close(Action completed)
        {if(_closing)return;_closing=true;if(CookingActive)_cook.Paused=true;if(_fade!=null)StopCoroutine(_fade);StartCoroutine(CloseRoutine(completed));}
        IEnumerator CloseRoutine(Action completed){yield return Fade(_group.alpha,0,.16f);gameObject.SetActive(false);_closing=false;completed();}
        IEnumerator Fade(float from,float to,float duration){for(float t=0;t<duration;t+=Time.unscaledDeltaTime){_group.alpha=Mathf.Lerp(from,to,Mathf.SmoothStep(0,1,t/duration));yield return null;}_group.alpha=to;}
    }
}
