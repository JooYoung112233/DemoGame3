using System;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    [Serializable] public sealed class SavedMissingPerson
    {
        public bool Found, Discussed;
        public int ReturnLine;
        public bool RouteKnown, MetMiran;
        public int MiranQuestions;
        public bool MetGeumrye, ReunionComplete;
        public bool NewsShared;
        public string NewsMessengerId;
        public int NewsLine;
        public SavedMissingPerson Clone()=>new SavedMissingPerson{Found=Found,Discussed=Discussed,ReturnLine=ReturnLine,RouteKnown=RouteKnown,MetMiran=MetMiran,MiranQuestions=MiranQuestions,MetGeumrye=MetGeumrye,ReunionComplete=ReunionComplete,NewsShared=NewsShared,NewsMessengerId=NewsMessengerId,NewsLine=NewsLine};
    }

    // Story facts are independent of tutorial suppression and of random loot rolls.
    [DefaultExecutionOrder(800)]
    public sealed class MissingPersonStory:MonoBehaviour
    {
        public const int SiteIndex=5;
        public SettlementController Owner;
        public GameObject View;
        public Button Next,Surface;
        public Text Title,Speaker,Body,NextLabel,Hint;
        public Image Portrait;
        public SettlementTutorialNarrative.Line[] DiscoveryLines,ReturnLines;
        public SavedMissingPerson State=new SavedMissingPerson();
        public bool IsOpen=>View&&View.activeSelf;
        public string GoalTitle=>State.ReunionComplete?"돌아올 사람 · 다시 만난 약속":State.MetGeumrye?"금례의 거처를 확인했다":State.MetMiran?"미란에게 들은 소식":State.Found?"금례의 흔적 · 미란 세탁소":"금례의 흔적을 찾아";
        public string GoalBody=>State.ReunionComplete?"금례와 다시 만나 현재 거처를 전했습니다.\n엇갈렸던 연락의 사정은 기록에 남겨 두었습니다.":State.MetGeumrye?"금례가 지내는 곳을 직접 확인했습니다.\n해인과 함께 쪽지와 연락처에 관한 대화를 마쳐 보세요.":State.MetMiran?"미란에게 들은 내용을 수색 기록에서 확인하세요.\n금례와의 직접 만남은 아직 남아 있습니다.":State.RouteKnown?"출입구 지도에서 미란 세탁소를 선택하세요.\n물품을 옮긴 사람에게 금례의 소식을 물어봅시다.":State.Discussed?"출입구 지도에서 ‘미란 세탁소’를 선택하세요.\n표찰 뒷면의 주소를 확인할 수 있습니다.":State.Found?"보관표에 ‘미란 세탁소’가 적혀 있습니다.\n금례의 행방은 아직 확인하지 못했습니다.":"금례의 수리품이 폐상가로 옮겨졌습니다.\n복도 적재함의 인계 기록을 살펴보세요.";
        public string VisitSummary=>State.ReunionComplete?"재회 완료 · 안부 방문\n대화 방문 · 수색 없음":State.MetGeumrye?"금례 거처 확인 · 재회 대기\n해인과 함께 방문":(State.MiranQuestions&1)!=0?"금례는 안쪽 방에 있다고 합니다\n해인과 함께 방문":State.RouteKnown?"느티길 12 · 주소 확인\n대화 방문 · 수색 없음":"표찰 주소 미확인\n아래 버튼으로 확인";
        public string VisitDescription=>State.ReunionComplete?"금례 안부 방문":State.MetGeumrye?"해인과 재회할 곳":(State.MiranQuestions&1)!=0?"금례가 머무는 곳":"표찰이 가리킨 곳";
        public string VisitPartyHint(bool hasHaein)=>State.ReunionComplete?"재회 완료 · 안부 방문은 누구나 가능합니다.":hasHaein?"해인 동행 · 금례와 직접 이야기할 수 있습니다.":"해인 없이도 방문 가능 · 재회 대화는 해인 동행 필요";
        public string RecordBody=>State.MetGeumrye?"<size=23>직접 만난 사람 · 이금례</size>\n미란 세탁소 안쪽 방에서 지내는 것을 확인했다.\n\n"+(State.ReunionComplete?"<size=23>엇갈린 연락</size>\n금례가 보낸 쪽지는 해인이 떠난 옛 사무소로 갔다.\n서로 약속을 버린 것이 아니었다.\n\n해인은 현재 거처를 적어 금례에게 건넸다.\n앞으로 거처가 바뀌면 먼저 소식을 전하기로 했다.":"금례의 거처를 직접 확인했다.\n\n쪽지와 연락처에 관한 대화는 아직 마치지 않았다."):"<size=23>찾는 사람 · 이금례</size>\n해인이 돌보던 시계 수리공. 약속한 날 가게가 비어 있었다.\n\n"
            +(State.MetMiran?"<size=23>만난 사람 · 황미란</size>\n미란 세탁소 주인. 금례의 인계 표찰을 알아봤다.\n"+((State.MiranQuestions&1)!=0?"금례는 세탁소 안쪽 방에 있다는 미란의 설명.\n":"금례의 행방은 아직 묻지 않았다.\n")+((State.MiranQuestions&2)!=0?"쪽지는 해인의 옛 사무소 수신함에 넣었다고 한다.\n":"연락이 엇갈린 사정은 아직 묻지 않았다.\n")+"\n금례와 직접 만나지는 않았다.":State.Found?"<size=23>확인한 단서 · 복도 적재함</size>\n보관표의 도장·수리품 목록이 인수증과 일치한다.\n인계 표찰: ‘미란 세탁소’\n\n금례가 그곳에 있는지는 아직 모른다.\n"+(State.RouteKnown?"표찰 주소를 지도에 표시했다. 출입구에서 방문 준비.":State.Discussed?"출입구의 지도에서 표찰 주소를 확인할 수 있다.":"귀환해서 인계 표찰을 다시 살펴보자."):"<size=23>가지고 온 단서 · 수리품 인수증</size>\n가게에 남은 사본에 ‘폐상가 보관실’이 적혀 있다.\n\n잠긴 보관실 바깥의 복도 적재함부터 확인한다.");
        bool pendingDiscovery,returning,sharingNews;
        SettlementTutorialNarrative.Line[] CurrentLines=>sharingNews?new[]{
            new SettlementTutorialNarrative.Line{SpeakerId=State.NewsMessengerId,Text="해인아, 금례 씨를 직접 뵀어.\n미란 세탁소 안쪽 방에 계시더라."},
            new SettlementTutorialNarrative.Line{SpeakerId="medic",Text="거기 계셨구나… 어디로 가셨는지 몰라서 걱정했어.\n다녀와 줘서 고마워."},
            new SettlementTutorialNarrative.Line{SpeakerId="medic",Text="다음엔 나도 같이 갈게.\n지금 머무는 곳도 직접 말씀드려야겠어."}
        }:returning?ReturnLines:DiscoveryLines;
        public void QueueNews(string messengerId)
        {
            if(!State.MetGeumrye||State.ReunionComplete||State.NewsShared||!string.IsNullOrEmpty(State.NewsMessengerId)||string.IsNullOrEmpty(messengerId)||messengerId=="medic")return;
            State.NewsMessengerId=messengerId;State.NewsLine=0;
        }
        int line,closedFrame=-1;
        CanvasGroup hud;
        float hudAlpha;
        GameObject focus;
        void Start(){View.SetActive(false);Next.onClick.AddListener(Advance);if(Surface)Surface.onClick.AddListener(Advance);}
        public void Discover(int index)
        {
            if(index!=SiteIndex||State.Found||!Owner||Owner.Campaign?.Stage!=JourneyStage.Expedition||!Owner.ArrivalPanel.Loot.State(index).Complete)return;
            State.Found=true;pendingDiscovery=true;
            Owner.ActivityLog.Add("인물 단서 · 금례의 수리품 보관표에서 ‘미란 세탁소’ 인계 표찰을 확인했다. 금례의 행방은 아직 모른다.");
        }
        public SavedMissingPerson Export()=>State.Clone();
        public void Restore(SavedMissingPerson saved){State=saved?.Clone()??new SavedMissingPerson();pendingDiscovery=false;}
        void LateUpdate()
        {
            if(!Owner||Owner.Campaign==null||IsOpen||closedFrame==Time.frameCount)return;
            var a=Owner.ArrivalPanel;
            if(Owner.Campaign.Stage==JourneyStage.Expedition){
                if(pendingDiscovery&&a.Main.interactable&&!a.InTransit&&!a.Popup.activeSelf&&!a.Search.IsOpen&&!a.Loot.IsOpen&&!a.FieldBags.IsOpen&&!a.Encounter.IsOpen&&!(a.Story&&a.Story.IsOpen))Open(false);
            }else if(Owner.Main.gameObject.activeInHierarchy&&Owner.Main.interactable&&!Owner.ReturnPanel.IsOpen&&!Owner.IsPopupOpen&&!Owner.GameMenu.IsOpen){
                if(State.Found&&!State.Discussed&&Owner.ReturnPanel.HasReport)Open(true);
                else if(!string.IsNullOrEmpty(State.NewsMessengerId)&&Owner.Campaign.Party.Any(p=>CampaignPersistence.MemberId(Owner,p)=="medic"))Open(true,true);
            }
        }
        void Open(bool home,bool news=false)
        {
            returning=home;sharingNews=news;line=news?State.NewsLine:home?State.ReturnLine:0;
            hud=home?Owner.Main:Owner.ArrivalPanel.Main;hudAlpha=hud.alpha;
            focus=EventSystem.current?.currentSelectedGameObject;
            hud.interactable=hud.blocksRaycasts=false;hud.alpha=0;View.SetActive(true);Render();
        }
        void Render()
        {
            var lines=CurrentLines;line=Mathf.Clamp(line,0,lines.Length-1);var text=lines[line];
            var people=returning?Owner.Campaign.Party.ToArray():Owner.ArrivalPanel.Participants.ToArray();
            var person=people.FirstOrDefault(p=>CampaignPersistence.MemberId(Owner,p)==text.SpeakerId)??people.FirstOrDefault();
            var candidate=person==null?null:Owner.Roster.Candidates.FirstOrDefault(p=>p.Id==CampaignPersistence.MemberId(Owner,person));
            Speaker.text=person?.Name??"수색 기록";Portrait.sprite=candidate?.Portrait;Portrait.enabled=Portrait.sprite;
            Title.text=sharingNews?"돌아와서 전한 소식":returning?"다음에 확인할 곳":"수리품에 묶인 표찰";Body.text=text.Text;
            NextLabel.text=line+1<lines.Length?"다음  ›":"기록하고 돌아가기";
            Hint.text="대화 중에는 시간이 흐르지 않습니다.  ·  "+(line+1)+" / "+lines.Length;
            EventSystem.current?.SetSelectedGameObject(Next.gameObject);
        }
        public void Advance()
        {
            if(!IsOpen||closedFrame==Time.frameCount)return;
            var lines=CurrentLines;
            if(line+1<lines.Length){line++;if(sharingNews)State.NewsLine=line;else if(returning)State.ReturnLine=line;Render();return;}
            if(sharingNews){State.NewsShared=true;State.NewsMessengerId=null;State.NewsLine=0;Owner.ActivityLog.Add("전한 소식 · 해인에게 금례를 직접 만났다고 알렸다. 다음에는 함께 찾아가기로 했다.");}
            else if(returning){State.Discussed=true;State.ReturnLine=0;Owner.ActivityLog.Add("수색 목표 · 미란 세탁소를 확인할 단서를 모은다. 금례의 현재 위치는 미확인.");}
            else pendingDiscovery=false;
            View.SetActive(false);hud.alpha=hudAlpha;hud.interactable=hud.blocksRaycasts=true;
            closedFrame=Time.frameCount;EventSystem.current?.SetSelectedGameObject(focus);
            if(returning){Owner.NoticeTitle.text=GoalTitle;Owner.NoticeBody.text=GoalBody;}
        }
    }
}
