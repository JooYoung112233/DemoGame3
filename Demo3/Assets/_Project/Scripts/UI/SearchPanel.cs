using System;
using Live49.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace Live49.UI
{
    public sealed class SearchPanel : MonoBehaviour
    {
        JourneyState _state;TMP_FontAsset _font;RectTransform _body;Action<bool> _close;
        string _place,_feedback="주변의 물건을 천천히 살펴봐요.";int _point,_shownCheck=-1,_frame;
        float _elapsed;bool _slow,_saveFailed;bool? _pendingClose;
        SearchDial _dial;TMP_Text _status,_risk,_progress;Image _fill;Button _strike;
        public bool IsOpen=>gameObject.activeSelf;
        public float Elapsed=>_elapsed;
        public bool SaveFailed=>_saveFailed;
        public static SearchPanel Create(Transform parent,TMP_FontAsset font)
        {
            var root=PanelUI.Box(parent,"SearchPanel",0,0,1920,1080,new Color32(12,16,14,125),true);
            var p=root.gameObject.AddComponent<SearchPanel>();p._font=font;
            p._body=PanelUI.Rect(root,"SearchBody",0,0,1920,1080);root.gameObject.SetActive(false);return p;
        }
        public void Open(JourneyState state,string place,int point,Action<bool> close)
        {_state=state;_place=place;_point=point;_close=close;_elapsed=0;_saveFailed=SearchSession.Active(state)&&!SaveSystem.AutoSave(state,out _,false);_slow=false;_pendingClose=null;gameObject.SetActive(true);transform.SetAsLastSibling();Refresh();}
        bool Allowed=>!GamePause.IsPaused&&!_saveFailed&&Time.frameCount>_frame;
        void Commit()
        {_saveFailed=!SaveSystem.AutoSave(_state,out _,_state.search?.phase=="result");Refresh();if(!_saveFailed&&_pendingClose.HasValue){bool back=_pendingClose.Value;_pendingClose=null;Close(back);}}
        Button Button(string id,string label,float y,Action action)
        {return PanelUI.Button(_body,_font,id,label,1104,y,626,64,()=>{if(Allowed)action();});}
        void Refresh()
        {
            PanelUI.Clear(_body);_dial=null;_status=null;_risk=null;_progress=null;_fill=null;_strike=null;_frame=Time.frameCount;
            EventSystem.current?.SetSelectedGameObject(null);
            PanelUI.Box(_body,"SearchCard",1040,80,750,930,new Color32(25,29,24,248));
            PanelUI.Text(_body,_font,"SearchEyebrow","주변 탐색 / "+RegionExploration.Find(_place).ShortName,100,90,860,38,22,PanelUI.Gold);
            PanelUI.Text(_body,_font,"SearchTitle",SearchSession.Label(_place,_point),96,141,880,110,43);
            PanelUI.Text(_body,_font,"SearchHint","필요한 물자와 길은 판정에 실패해도 확인할 수 있어요.\n집중 탐색에서는 추가 물자를 찾을 기회가 생겨요.",100,822,850,108,27);
            if(_saveFailed)
            {
                PanelUI.Text(_body,_font,"SaveError","탐색 기록을 저장하지 못했어요.\n진행을 멈췄어요. 저장을 다시 시도해주세요.",1104,270,626,160,30);
                PanelUI.Button(_body,_font,"RetrySearchSave","저장 다시 시도",1104,710,626,64,()=>{if(!GamePause.IsPaused)Commit();});return;
            }
            var p=_state.search;
            if(!SearchSession.Active(_state))
            {
                PanelUI.Text(_body,_font,"SearchModeTitle","어떻게 살펴볼까요?",1104,145,626,65,36);
                PanelUI.Text(_body,_font,"SearchModeInfo","빠른 탐색  ·  5분\n기본 물자와 길을 확인해요.\n\n집중 탐색  ·  10분 + 선택 5분\n약 14초 동안 세 번의 짧은 판정이 나타나요.\n중간에 확보한 물자만 챙겨 끝낼 수도 있어요.\n\n불러오면 탐색은 종료돼요.\n이미 챙긴 물자와 사용한 시간은 유지돼요.",1104,235,626,360,25,PanelUI.Muted);
                Button("QuickSearch","빠르게 살펴보기 · 5분",620,()=>Begin(false));
                var b=PanelUI.Button(_body,_font,"FocusSearch",SearchSession.FocusAvailable(_state,_place,_point)?"집중해서 살펴보기 · 10분":"집중 탐색은 이미 시도했어요",1104,704,626,64,()=>{if(Allowed)Begin(true);});b.interactable=SearchSession.FocusAvailable(_state,_place,_point);
                Button("SlowSearch",_slow?"판정 속도 · 여유":"판정 속도 · 기본",788,()=>{_slow=!_slow;Refresh();});
                Button("CancelSearch","돌아가기",872,()=>Close(false));return;
            }
            _risk=PanelUI.Text(_body,_font,"SearchRisk","소음 반응 위험 · "+Mathf.RoundToInt(p.risk*100)+"%",1104,146,626,48,26,PanelUI.Gold);
            if(p.phase=="playing")
            {
                _status=PanelUI.Text(_body,_font,"SearchStatus",_feedback,1104,225,626,120,29);
                var r=PanelUI.Rect(_body,"SkillDial",1257,358,320,320);_dial=r.gameObject.AddComponent<SearchDial>();_dial.raycastTarget=false;r.gameObject.SetActive(false);
                _progress=PanelUI.Text(_body,_font,"SearchProgress","탐색 중",1104,705,626,38,23,PanelUI.Muted);
                PanelUI.Box(_body,"ProgressTrack",1104,760,626,4,new Color32(65,70,58,255));_fill=PanelUI.Box(_body,"ProgressFill",1104,760,0,4,PanelUI.Gold).GetComponent<Image>();
                _strike=Button("StrikeSearch","타이밍 맞추기",794,Strike);_strike.gameObject.SetActive(false);
                Button("StopSearch","여기까지 살펴보기",878,()=>{SearchSession.Finish(_state,"stopped");Commit();});
            }
            else if(p.phase=="checkpoint")
            {
                PanelUI.Text(_body,_font,"CheckpointTitle","우선 챙겨 두었어요",1104,227,626,66,35);
                PanelUI.Text(_body,_font,"SecuredLoot",p.loot,1104,324,626,218,28);
                PanelUI.Text(_body,_font,"CheckpointHelp","지금 끝내도 확보한 물자는 남아요.\n더 살펴보면 5분을 쓰고 추가 물자를 찾아요.",1104,559,626,108,25,PanelUI.Muted);
                Button("ContinueSearch","조금 더 살펴보기 · 5분",734,()=>{if(SearchSession.Continue(_state))Commit();});
                Button("FinishSearch","챙긴 물자로 마무리하기",830,()=>{SearchSession.Finish(_state,"stopped");Commit();});
            }
            else if(p.phase=="result")
            {
                PanelUI.Text(_body,_font,"SearchResultTitle",p.reason=="interrupted"?"멈췄던 탐색 기록":"살펴본 자리",1104,227,626,65,35);
                PanelUI.Text(_body,_font,"SearchLoot",p.loot,1104,329,626,330,27);
                PanelUI.Text(_body,_font,"ResultHelp",p.reason=="interrupted"?"불러오기 전 사용한 시간과 확보한 물자를 유지했어요.":"확인한 흔적과 챙긴 물자는 여정에 기록돼요.",1104,672,626,90,23,PanelUI.Muted);
                Button("AcknowledgeSearch",p.noise?"주변 인기척 확인하기":"탐색 마치기",830,()=>{SearchSession.Acknowledge(_state);if(!SearchSession.Active(_state))_pendingClose=false;Commit();});
            }
            else
            {
                PanelUI.Text(_body,_font,"NoiseTitle","바깥에서 인기척이 나요",1104,238,626,75,34);
                PanelUI.Text(_body,_font,"NoiseDetail","확보한 물자는 그대로 가지고 있어요.\n계속 머무르면 다음 탐색의 기본 소음 위험이 10%p 높아져요.\n\n돌아가면 실제 주차한 캠핑카로 이동해요.",1104,365,626,244,27,PanelUI.Muted);
                Button("StayAfterSearch","주변에 조금 더 머무르기",734,()=>Decide(false));
                Button("ReturnAfterSearch","캠핑카로 돌아가기",830,()=>Decide(true));
            }
        }
        void Begin(bool focus){if(SearchSession.Begin(_state,_place,_point,focus,_slow)){_elapsed=0;_shownCheck=-1;Commit();}}
        void Decide(bool back){SearchSession.Decide(_state,!back);_pendingClose=back;Commit();}
        void Close(bool back){gameObject.SetActive(false);FreshInput.DiscardPending();_close(back);}
        public void Strike()
        {
            if(!Allowed||!SearchSession.Active(_state))return;var p=_state.search;
            if(p.phase!="playing"||p.checks>=3)return;
            float t=_elapsed-p.schedule[p.checks]-.8f, duration=p.slow?1.9f:1.25f;
            if(t<0||t>duration)return;
            Resolve(SearchSession.Grade(t/duration,p.centers[p.checks],p.slow));
        }
        void Resolve(string grade)
        {
            var p=_state.search;if(!SearchSession.Check(_state,p.checks,grade))return;
            _feedback=grade=="great"?"대성공 · 추가 물자 확률 +10%p":grade=="good"?"조용히 살펴봤어요.":"물건이 부딪혔어요. 소음 위험 +25%p";Commit();
        }
        void Update(){if(Application.isFocused)Tick(Time.unscaledDeltaTime);}
        public void Tick(float delta)
        {
            if(!Allowed||!SearchSession.Active(_state)||float.IsNaN(delta)||float.IsInfinity(delta)||delta<=0)return;
            var p=_state.search;if(p.phase!="playing")return;
            _elapsed+=Mathf.Min(.1f,delta);
            if(Keyboard.current!=null&&Keyboard.current.spaceKey.wasPressedThisFrame)Strike();
            if(_saveFailed||p.phase!="playing")return;
            int i=p.checks;float duration=p.slow?1.9f:1.25f;
            if(i<3&&(i<2||p.secured)&&_elapsed>p.schedule[i]+.8f+duration)Resolve("miss");
            if(_saveFailed)return;
            if(!p.secured&&p.checks==2&&_elapsed>=8){SearchSession.Checkpoint(_state);Commit();return;}
            if(p.secured&&p.checks==3&&_elapsed>=14){SearchSession.Finish(_state,"complete");Commit();return;}
            if(_dial==null)return;
            i=p.checks;bool cue=i<3&&(i<2||p.secured)&&_elapsed>=p.schedule[i];
            _dial.gameObject.SetActive(cue);
            _strike.gameObject.SetActive(cue);_strike.interactable=cue&&_elapsed>=p.schedule[i]+.8f;
            if(cue){float t=_elapsed-p.schedule[i];_dial.Set(p.centers[i],p.slow?.16f:.11f,Mathf.Clamp01((t-.8f)/duration),t>=.8f);if(_shownCheck!=i){_shownCheck=i;_feedback="곧 판정이 시작돼요.";}_status.text=t<.8f?"걸리는 소리 · 잠시 집중해요": "초록 구간에서 멈춰요. 금색은 대성공이에요.";}
            else _status.text=_feedback;
            _progress.text="탐색 "+Mathf.Min(100,Mathf.FloorToInt(_elapsed/14*100))+"%  ·  판정 "+p.checks+" / 3";
            _fill.rectTransform.sizeDelta=new Vector2(626*Mathf.Clamp01(_elapsed/14),4);
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SearchDial : MaskableGraphic
    {
        float _center,_width,_position;bool _running;
        public void Set(float center,float width,float position,bool running){_center=center;_width=width;_position=position;_running=running;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var c=rectTransform.rect.center;
            for(int i=0;i<160;i++)
            {
                float p=(i+.5f)/160;Color col=Math.Abs(p-_center)<=.035f?PanelUI.Gold:Math.Abs(p-_center)<=_width?new Color32(122,156,105,255):new Color32(61,72,59,255);
                Quad(vh,c,i/160f,(i+1)/160f,132,150,col);
            }
            if(_running)Quad(vh,c,_position-.006f,_position+.006f,40,157,PanelUI.Cream);
        }
        static Vector2 At(Vector2 c,float turn,float r)=>c+new Vector2(Mathf.Sin(turn*Mathf.PI*2),Mathf.Cos(turn*Mathf.PI*2))*r;
        static void Quad(VertexHelper vh,Vector2 c,float a,float b,float inner,float outer,Color color)
        {int i=vh.currentVertCount;vh.AddVert(At(c,a,inner),color,Vector2.zero);vh.AddVert(At(c,a,outer),color,Vector2.zero);vh.AddVert(At(c,b,outer),color,Vector2.zero);vh.AddVert(At(c,b,inner),color,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);}
    }
}
