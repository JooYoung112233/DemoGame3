using System;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Story observes completed actions. Reading never grants items, spends time or repairs a facility.
    [DefaultExecutionOrder(900)]
    public sealed class SettlementTutorialNarrative : MonoBehaviour
    {
        [Serializable] public sealed class Line { public string SpeakerId; [TextArea(1,3)] public string Text; }
        [Serializable] public sealed class Beat { public string Title,EndLabel; public Line[] Lines; public string[] FactIds=Array.Empty<string>(); public bool ShowPartyCondition; }
        public Beat[] Beats;
        public GameObject View,Facts;
        public Text SceneTitle,Speaker,Body,NextLabel,ReadingHint;
        public Image Portrait;
        public Image[] FactIcons;
        public Text[] FactLabels;
        public GameObject HealthView;
        public Text[] HealthLabels;
        public Image[] HealthBars;
        [Header("새 게임 도착 연출")]
        public CanvasGroup ArrivalFade;
        [Min(0)] public float ArrivalHold=.45f,ArrivalFadeSeconds=1.35f;
        public Button Next,ContinueSurface,Skip;
        public SavedTutorialNarrative State=new SavedTutorialNarrative();
        public bool IsOpen=>View&&View.activeSelf;
        public bool IsEntering { get; private set; }
        public bool IsBlocking=>IsEntering||IsOpen;
        public int CurrentBeat=>State.PendingBeat;
        public int CurrentLine=>State.LineIndex;
        SettlementController owner;
        GameObject previousFocus;
        int closedFrame=-1;
        float arrivalElapsed;

        public void Initialize(SettlementController value,bool restoring)
        {
            owner=value;View.SetActive(false);Next.onClick.AddListener(Advance);
            if(ContinueSurface)ContinueSurface.onClick.AddListener(Advance);
            if(Skip)Skip.onClick.AddListener(SkipTutorial);
            State=new SavedTutorialNarrative();
            if(ArrivalFade)ArrivalFade.gameObject.SetActive(false);
            IsEntering=!restoring&&owner.Campaign!=null&&owner.Introduction.Step==0&&ArrivalFade;
            if(IsEntering){
                // Apply the black cover before the first rendered frame, not on the first guide update.
                arrivalElapsed=0;ArrivalFade.alpha=1;ArrivalFade.blocksRaycasts=true;ArrivalFade.interactable=false;
                owner.Main.interactable=owner.Main.blocksRaycasts=false;
                ArrivalFade.gameObject.SetActive(true);
            }
        }
        public SavedTutorialNarrative Export()=>State.Clone();
        public void SkipTutorial()
        {
            if(!owner||owner.Campaign==null||State.Skipped||owner.Campaign.Stage!=JourneyStage.Settlement)return;
            if(!IsOpen||CurrentBeat!=0||owner.Introduction.Step!=0)return;
            // Only the dialogue button or the menu's close-then-skip callback may release input.
            if(!IsBlocking&&(!owner.Main.interactable||!owner.Main.gameObject.activeInHierarchy))return;
            Restore(new SavedTutorialNarrative{Skipped=true,SeenMask=SavedTutorialNarrative.AllSeen});
            owner.Introduction.SkipPreparation();
            owner.ActivityLog.Add(owner.Campaign.ClockText.Replace("\n"," ")+" · 튜토리얼 건너뛰기: 남은 도입 정리를 마쳤다. 시설과 단서는 직접 발견한다.");
            owner.NoticeTitle.text=owner.MissingPerson?owner.MissingPerson.GoalTitle:"자유롭게 생활하기";
            owner.NoticeBody.text=owner.MissingPerson?owner.MissingPerson.GoalBody:"출입구에서 원정을 준비할 수 있습니다.\n시설은 가져온 재료로 복구하세요.";
            owner.RefreshMembers();
            closedFrame=Time.frameCount;
            EventSystem.current?.SetSelectedGameObject(null);
        }
        public void Restore(SavedTutorialNarrative saved)
        {
            bool wasOpen=IsBlocking;
            EndArrival();
            State=saved?.Clone()??new SavedTutorialNarrative();
            if(View)View.SetActive(false);
            if(wasOpen&&owner)owner.Main.interactable=owner.Main.blocksRaycasts=true;
            closedFrame=-1;
        }
        int Have(string id)=>owner.InventoryPanel.StockCount(id)+owner.Campaign.Party.Sum(p=>owner.InventoryPanel.CountFor(p,id));
        void EndArrival()
        {
            if(ArrivalFade){ArrivalFade.alpha=0;ArrivalFade.blocksRaycasts=false;ArrivalFade.gameObject.SetActive(false);}
            if(IsEntering&&owner)owner.Main.interactable=owner.Main.blocksRaycasts=true;
            IsEntering=false;
        }
        int ContextBeat()
        {
            int step=owner.Introduction.Step;var opening=owner.Opening.State;
            if(step<4)return step;
            if(!opening.FirstReturn)return 4;
            if(opening.Complete)return 10;
            if(opening.SurveyReturned)return 9;
            if(!owner.Development.State.Workbench)return 5;
            if(!opening.ClueRead)return 6;
            if(owner.ArrivalPanel.Rooms.StorageUnlocked||Have("prybar")>0)return 8;
            return 7;
        }
        void LateUpdate()
        {
            if(IsEntering){
                arrivalElapsed+=Time.unscaledDeltaTime;
                float u=ArrivalFadeSeconds<=0?1:Mathf.Clamp01((arrivalElapsed-ArrivalHold)/ArrivalFadeSeconds);
                ArrivalFade.alpha=1-Mathf.SmoothStep(0,1,u);
                if(arrivalElapsed<ArrivalHold+ArrivalFadeSeconds)return;
                EndArrival(); // The arrival dialogue opens below in this same frame; no bare HUD frame between them.
            }
            if(!owner||owner.Campaign==null||State.Skipped||!owner.Opening||!owner.Introduction||!owner.Opening.State.Enabled||IsOpen||closedFrame==Time.frameCount)return;
            if(owner.Campaign.Stage!=JourneyStage.Settlement||!owner.Main.gameObject.activeInHierarchy||!owner.Main.interactable||owner.IsPopupOpen||owner.GameMenu.IsOpen)return;
            int beat=State.PendingBeat>=0?State.PendingBeat:ContextBeat();
            if(Beats==null||beat<0||beat>=Beats.Length||(State.SeenMask&(1<<beat))!=0)return;
            // A load or free-order play may have passed several contexts. Do not queue stale scenes.
            if(State.PendingBeat<0){State.SeenMask|=(1<<beat)-1;State.PendingBeat=beat;State.LineIndex=0;}
            previousFocus=EventSystem.current?.currentSelectedGameObject;
            owner.Main.interactable=owner.Main.blocksRaycasts=false;
            View.SetActive(true);Render();
        }
        public void Advance()
        {
            if(!IsOpen||State.PendingBeat<0||closedFrame==Time.frameCount)return;
            var beat=Beats[State.PendingBeat];
            if(State.LineIndex+1<beat.Lines.Length){State.LineIndex++;Render();return;}
            State.SeenMask|=1<<State.PendingBeat;
            owner.ActivityLog.Add(owner.Campaign.ClockText.Replace("\n"," ")+" · 정착 기록: "+beat.Title);
            State.PendingBeat=-1;State.LineIndex=0;closedFrame=Time.frameCount;
            View.SetActive(false);owner.Main.interactable=owner.Main.blocksRaycasts=true;
            EventSystem.current?.SetSelectedGameObject(previousFocus);
        }
        Adventurer Person(string id)
        {
            var people=owner.Campaign.Party.ToArray();
            if(id=="rested")return people.FirstOrDefault(p=>p.Health==p.MaxHealth)??people.FirstOrDefault(p=>p.Health>0);
            // Zero health means seriously injured, not a different speaker. Keep identity intact.
            return people.FirstOrDefault(p=>CampaignPersistence.MemberId(owner,p)==id);
        }
        string Resolve(string text)
        {
            bool materials=Have("wood")+Have("scrap")>0;
            var development=owner.Development.State;
            bool opened=owner.ArrivalPanel.Rooms.StorageUnlocked;
            return (text??"").Replace("{home}",owner.Campaign.Home.Name)
                .Replace("{return_line}",materials?"쓸 만한 자재가 있네. 가지고 있는 것부터 모아 보자.":"지금 쓸 목재와 고철이 없네. 고치려면 찾아서 가져와야겠어.")
                .Replace("{storage_line}",development.Warehouse>0?"보관할 곳은 됐으니, 작업대를 고칠 자재부터 모아 보자.":materials?"짐을 계속 바닥에 쌓아 둘 순 없지. 보관할 자리를 만들고 작업대도 고치자.":"챙길 때 쓸 가방부터 비워 두자. 이번에는 고칠 데 필요한 만큼 가져오는 거야.")
                .Replace("{door_line}",opened?"그 문은 열어 뒀어. 다음에는 곧장 보관실을 확인할 수 있겠네.":"이 도구는 문틈을 벌리는 데 쓰자. 잠금장치를 부술 만큼 튼튼하진 않아.")
                .Replace("{carry_line}",opened?"안쪽을 더 볼 때도, 돌아올 길은 놓치지 말자.":"지렛대도 놓고 가지 않게 챙겨 두자.\n안쪽을 보더라도, 돌아올 길은 놓치지 말고.")
                .Replace("{living_line}",development.Bed&&development.Cooker?"쉴 곳과 먹을 곳은 갖췄네. 이제 필요한 물자를 조금씩 채워 두자.":development.Bed?"잘 곳은 마련했으니, 이번에는 조리할 곳을 손보자.":development.Cooker?"먹을 곳은 갖췄네. 이제 침대도 손보자. 바닥에서만 버틸 순 없으니까.":"이제 쉴 곳과 먹을 곳도 제대로 손보자. 바닥에서만 버틸 순 없으니까.");
        }
        void Render()
        {
            bool initial=State.PendingBeat==0&&owner.Introduction.Step==0&&!State.Skipped;
            if(Skip)Skip.gameObject.SetActive(initial);
            var skipHint=View.transform.Find("SkipHint");if(skipHint)skipHint.gameObject.SetActive(initial);
            var beat=Beats[State.PendingBeat];State.LineIndex=Mathf.Clamp(State.LineIndex,0,beat.Lines.Length-1);var line=beat.Lines[State.LineIndex];
            var person=string.IsNullOrEmpty(line.SpeakerId)?null:Person(line.SpeakerId);
            SceneTitle.text=beat.Title;Speaker.text=person?.Name??"주변";Body.text=Resolve(line.Text);
            var candidate=person==null?null:owner.Roster.Candidates.FirstOrDefault(p=>p.Id==CampaignPersistence.MemberId(owner,person));
            Portrait.sprite=candidate?.Portrait;Portrait.enabled=Portrait.sprite;
            NextLabel.text=State.LineIndex+1<beat.Lines.Length?"다음  ›":string.IsNullOrEmpty(beat.EndLabel)?"대화 마치기":beat.EndLabel;
            if(ReadingHint)ReadingHint.text="대화 중에는 시간이 흐르지 않습니다.  ·  "+(State.LineIndex+1)+" / "+beat.Lines.Length;
            var ids=beat.FactIds??Array.Empty<string>();Facts.SetActive(ids.Length>0);
            if(HealthView){
                HealthView.SetActive(beat.ShowPartyCondition);
                if(beat.ShowPartyCondition){
                    var people=owner.Campaign.Party.ToArray();
                    for(int i=0;i<HealthLabels.Length;i++){
                        bool show=i<people.Length;HealthLabels[i].gameObject.SetActive(show);HealthBars[i].gameObject.SetActive(show);if(!show)continue;
                        var p=people[i];HealthLabels[i].text=p.Name+" · 체력 "+p.Health+" / "+p.MaxHealth;
                        SegmentedHealthGraphic.Set(HealthBars[i],p.Health,p.MaxHealth);
                    }
                }
            }
            for(int i=0;i<FactIcons.Length;i++)
            {
                bool show=i<ids.Length;FactIcons[i].gameObject.SetActive(show);FactLabels[i].gameObject.SetActive(show);if(!show)continue;
                var item=owner.InventoryPanel.Items.First(x=>x.Id==ids[i]);FactIcons[i].sprite=item.Icon;FactLabels[i].text=item.Name+" ×"+Have(item.Id);
            }
            EventSystem.current?.SetSelectedGameObject(Next.gameObject);
        }
    }
}
