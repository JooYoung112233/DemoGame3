using System;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
 // A dialogue visit, with its own lifecycle. Never initializes the mall's rooms, loot or resident.
 public sealed class LaundryStoryVisit:MonoBehaviour
 {
  public const string DestinationId="laundry";
  public SettlementController Owner;
  public GameObject View;
  public Button Next,Surface,Return,AskWhere,AskLetter;
  public Text Heading,Context,Speaker,Body,NextLabel,Hint;
  public Image Portrait;
  public LaundryVisitHud Hud;
  public bool IsOpen=>View&&View.activeSelf;
  public bool IsVisit=>IsOpen&&!routeReview;
  SavedMissingPerson State=>Owner.MissingPerson.State;
  Adventurer[] people=Array.Empty<Adventurer>();
  ExpeditionPlanPanel.Destination destination;
  bool HasHaein=>people.Any(p=>CampaignPersistence.MemberId(Owner,p)=="medic");
  bool routeReview,choices,metThisVisit;int line,question,closedFrame=-1;
  struct Line{public string Who,Text;public Line(string who,string text){Who=who;Text=text;}}
  Line[] lines;
  void Start(){View.SetActive(false);Next.onClick.AddListener(Advance);Surface.onClick.AddListener(Advance);Return.onClick.AddListener(GoHome);AskWhere.onClick.AddListener(()=>Ask(1));AskLetter.onClick.AddListener(()=>Ask(2));}
  public void ReviewRoute()
  {
   if(IsOpen||!State.Discussed||State.RouteKnown||Owner.Campaign.Stage!=JourneyStage.Settlement)return;
   Owner.ExpeditionPanel.Close();routeReview=true;people=Owner.Campaign.Party.ToArray();
   Owner.Main.interactable=Owner.Main.blocksRaycasts=false;
   Open(new[]{new Line("","표찰을 뒤집자 작은 인계처 주소가 보인다.\n‘미란 세탁소 · 느티길 12, 동쪽 골목’"),new Line("player","이 주소를 지도에 표시해 두자.\n물건을 옮긴 분에게 금례 씨 소식을 물어볼 수 있겠어.")});
  }
  public bool Begin(Adventurer[] party,ExpeditionPlanPanel.Destination target)
  {
   if(IsOpen||!State.RouteKnown||target==null||target.Id!=DestinationId||party==null||party.Length==0||party.Any(p=>Owner.IsAssigned(p)||Owner.InventoryPanel.SlotsFor(p)>p.BagCapacity))return false;
   int started=(Owner.Campaign.Day-1)*1440+Owner.Campaign.MinuteOfDay;
   if(!Owner.Campaign.BeginFieldExpedition(target.Name,party,target.OneWayMinutes))return false;
   people=party.ToArray();destination=target;routeReview=false;metThisVisit=false;
   Owner.ReturnPanel.Begin(people,target.Name,started);Owner.Main.gameObject.SetActive(false);
   Open(State.MetMiran?new[]{new Line("miran",State.ReunionComplete?"어서 오세요. 금례 씨 안부 보러 오셨어요?":State.MetGeumrye?"다시 오셨네요. 금례 씨는 안쪽에 계세요.":"다시 오셨네요. 금례 씨 일로 더 물어볼 게 있나요?")}:new[]{
    new Line("","표찰의 주소를 따라 세탁소 앞에 도착했다.\n문을 두드리자 앞치마 차림의 여자가 문틈으로 내다본다."),
    new Line("unknown","무슨 일이세요?"),new Line("player","이금례 씨를 찾고 있습니다. 맡기신 시계 상자에서 이 표찰을 봤어요."),
    new Line("miran","아, 그 시계들. 제가 옮겨 드렸어요.\n저는 여기 세탁소 하는 황미란이에요.")});
   return true;
  }
  void Open(Line[] content){lines=content;line=0;question=0;choices=false;View.SetActive(true);if(Hud)Hud.Bind(Owner,people,routeReview);Render();}
  void Render()
  {
   var entry=lines[line];if(entry.Who=="miran"&&!State.MetMiran){State.MetMiran=true;Owner.ActivityLog.Add("인물 만남 · 세탁소 주인 황미란이 금례의 수리품을 옮겼다고 말했다.");}
   if(question==3&&line>=1){metThisVisit=true;if(!State.MetGeumrye){State.MetGeumrye=true;Owner.ActivityLog.Add("직접 확인 · 세탁소 안쪽 방에서 이금례를 만났다.");}}
   var person=people.FirstOrDefault(p=>CampaignPersistence.MemberId(Owner,p)=="medic")??people.FirstOrDefault();
   Speaker.text=entry.Who=="player"?person?.Name??"원정대":entry.Who=="miran"?"황미란":entry.Who=="geumrye"?"이금례":entry.Who=="unknown"?"?":"주변";
   var candidate=entry.Who=="player"&&person!=null?Owner.Roster.Candidates.FirstOrDefault(p=>p.Id==CampaignPersistence.MemberId(Owner,person)):null;
   Portrait.sprite=candidate?.Portrait;Portrait.enabled=Portrait.sprite;
   Heading.text=routeReview?"인계 표찰":"미란 세탁소";
   Context.text=routeReview?"표찰 뒷면을 확인하고\n지도에 경로를 표시합니다.":(State.ReunionComplete?"목적 · 금례 안부 방문\n귀환 시 ":"목적 · 금례의 소식 확인\n귀환 시 ")+destination.OneWayMinutes+"분 경과";
   Body.text=entry.Text;Hint.text="읽기·대화 · 시간 소모 없음";
   AskWhere.gameObject.SetActive(false);AskLetter.gameObject.SetActive(false);Next.gameObject.SetActive(true);
   NextLabel.text=line+1<lines.Length?"다음  ›":routeReview?"지도에 표시":question!=0?"질문으로 돌아가기":"물어보기";
   Return.gameObject.SetActive(!routeReview);EventSystem.current?.SetSelectedGameObject(Next.gameObject);
   if(Hud&&!routeReview)Hud.Route.text="느티길 12 · 문 앞";
   if(question==3){Context.text="금례와 직접 나누는 대화\n귀환 시 "+destination.OneWayMinutes+"분 경과";if(Hud)Hud.Route.text="느티길 12 · 안쪽 방";}
   if(question==3&&line==lines.Length-1)NextLabel.text="대화 마치기";
  }
  public void Advance()
  {
   if(!IsOpen||choices||closedFrame==Time.frameCount)return;
   if(line+1<lines.Length){line++;Render();return;}
   if(routeReview){State.RouteKnown=true;Owner.ActivityLog.Add("이동 경로 · 인계 표찰의 느티길 12 주소를 확인하고 미란 세탁소를 지도에 표시했다.");View.SetActive(false);Owner.Main.interactable=Owner.Main.blocksRaycasts=true;closedFrame=Time.frameCount;Owner.ExpeditionPanel.Open();return;}
   CompleteAnswer();question=0;
   Choices();
  }
  void Choices()
  {
   choices=true;Speaker.text="대화 선택";Portrait.enabled=false;Body.text=State.ReunionComplete?"금례에게 안부를 묻거나 미란과 이야기를 나눌 수 있습니다.":"미란에게 무엇을 물어볼까요?";Next.gameObject.SetActive(false);
   if(Hud)Hud.Route.text="느티길 12 · 문 앞";
   Context.text=State.ReunionComplete?"금례와 재회 · 연락처 전달\n귀환 시 "+destination.OneWayMinutes+"분 경과":(State.ReunionComplete?"목적 · 금례 안부 방문\n귀환 시 ":"목적 · 금례의 소식 확인\n귀환 시 ")+destination.OneWayMinutes+"분 경과";
   AskWhere.gameObject.SetActive(true);AskLetter.gameObject.SetActive(true);
   AskWhere.GetComponentInChildren<Text>().text=(State.MiranQuestions&1)==0?"금례 씨는 어디에 있나요?":State.ReunionComplete?"금례 씨 안부 묻기":"금례 씨 만나기";
   AskLetter.GetComponentInChildren<Text>().text=((State.MiranQuestions&2)!=0?"다시 듣기 · ":"")+"연락을 남기셨나요?";
   Hint.text="들은 내용만 기록\n같은 질문도 다시 할 수 있습니다.";EventSystem.current?.SetSelectedGameObject(AskWhere.gameObject);
  }
  void Ask(int id)
  {
   if(!IsVisit||!choices)return;
   if(id==1&&(State.MiranQuestions&1)!=0){MeetGeumrye();return;}
   lines=id==1?new[]{new Line("miran","안쪽 방에서 쉬고 계세요. 가게 천장에서 물이 새서 침구까지 젖었더라고요.\n회복 중인데 거기 더 계실 수는 없잖아요."),new Line("miran","옷가지는 이쪽으로, 손님들 시계는 폐상가 보관실로 나눠 옮겼어요.\n금례 씨가 그 보관실에서 지내신 건 아니에요.")}:new[]{new Line("miran","금례 씨가 해인 씨에게 전해 달라며 쪽지를 써 주셨어요.\n전에 계시던 돌봄 사무소 수신함에 넣었는데… 못 받으셨대요?"),new Line("player",people.Any(p=>CampaignPersistence.MemberId(Owner,p)=="medic")?"그 사무소가 문을 닫아서, 저는 그전에 나왔어요.\n금례 씨를 뵈면 새 연락처를 말씀드리려고 했는데…":"해인 씨는 그 사무소가 문을 닫은 뒤 나왔다고 했어요.\n쪽지가 옛 주소로 간 거군요.")};
   line=0;question=id;choices=false;Render();
  }
  public void GoHome()
  {
   if(!IsVisit||!Owner.Campaign.EndFieldExpedition(destination.OneWayMinutes))return;
   // A heard answer remains evidence even if the player leaves before its final Next.
   CompleteAnswer();
   if(!HasHaein&&metThisVisit)Owner.MissingPerson.QueueNews(CampaignPersistence.MemberId(Owner,people[0]));
   View.SetActive(false);closedFrame=Time.frameCount;Owner.Main.gameObject.SetActive(true);Owner.Main.interactable=Owner.Main.blocksRaycasts=true;
   Owner.Clock.text=Owner.Campaign.ClockText;Owner.RefreshMembers();Owner.RefreshResources();Owner.ReturnPanel.Complete();
  }
  void Update(){if(IsOpen&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)Advance();}
  void CompleteAnswer()
  {
   if(question==1||question==2)State.MiranQuestions|=question;
   if(question==3&&line==lines.Length-1&&HasHaein&&!State.ReunionComplete){State.ReunionComplete=true;Owner.ActivityLog.Add("돌아올 사람 · 해인과 금례가 다시 만났다. 옛 사무소로 간 쪽지의 사정을 알고 현재 거처를 나눴다.");}
  }
  void MeetGeumrye()
  {
   question=3;line=0;choices=false;
   lines=State.ReunionComplete?new[]{new Line("miran","안쪽에 계세요. 들어가 보세요."),new Line("geumrye",HasHaein?"왔구나. 적어 준 주소는 잘 챙겨 뒀어.\n이번엔 서로 어디 있는지 아니까 마음이 놓이네.":"와 줘서 고마워요. 해인이가 적어 준 주소는 잘 챙겨 뒀어요.\n잘 지내고 있다고 전해 줘요.")}:HasHaein?new[]{
    new Line("miran","금례 씨, 해인 씨가 왔어요. 들어가도 괜찮죠?"),
    new Line("","안쪽 방에서 대답이 들린다. 미란이 문을 열어 준다.\n침대에 앉아 있던 금례가 해인을 보고 손을 내민다."),
    new Line("geumrye","해인아. 쪽지 못 받았니?"),new Line("player","어디로 보내셨어요?"),
    new Line("geumrye","전에 있던 사무실. 미란이가 수신함에 넣어 줬는데.\n못 받았을 줄은 몰랐지."),
    new Line("player","저, 그전에 나왔어요. 오늘 뵈면 말씀드리려고 했는데…\n집이 비어 있어서, 무슨 일 생기신 줄 알았어요."),
    new Line("geumrye","물이 새서 잠깐 옮긴 거야. 너한테도 알려야 한다고 생각했어.\n약속한 날인데 그냥 기다리게 할 수는 없잖니."),
    new Line("player","이게 지금 머무는 곳이에요. 주소 적어 드릴게요.\n다음에 거처가 바뀌면 먼저 소식 전할게요."),
    new Line("geumrye","그래. 나도 옮기게 되면 꼭 전할게.\n찾아와 줘서 고맙다.")}:new[]{
    new Line("miran","금례 씨, 해인 씨 동료분들이 찾아오셨어요."),
    new Line("","안쪽 방에서 대답이 들린다. 금례가 문가를 바라본다.\n원정대는 해인이 금례를 찾고 있다는 소식을 전한다."),
    new Line("geumrye","해인이가 걱정했겠네. 나는 여기 잘 있다고 전해 줘요.\n다음에 같이 오면 얼굴도 보고 싶고."),
    new Line("player","직접 뵀다고 전할게요. 해인 씨도 안심할 거예요.")};
   Render();
  }
 }
}
