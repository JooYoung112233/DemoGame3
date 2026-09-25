using System;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
 public sealed partial class SettlementController
 {
  public bool RecruitResident(string id)
  {
   var data=Roster.Candidates.FirstOrDefault(p=>p.Id==id);
   if(data==null||Campaign==null||Campaign.Stage!=JourneyStage.Settlement||Campaign.IsFieldExpedition||!IsCharacterUnlocked(id)||PartySelectionSession.Selected.Contains(id)||PartySelectionSession.Selected.Count!=Campaign.Party.Count()||!CraftPanel.CanAcceptResidents(1))return false;
   var member=data.CreateAdventurer();
   if(!Campaign.AddResident(member,CraftPanel.ResidentCapacity))return false;
   PartySelectionSession.Selected.Add(id);page=(Campaign.Party.Count()-1)/Members.Length;RefreshMembers();
   ActivityLog.Add(Campaign.ClockText.Replace("\n"," ")+" · "+data.DisplayName+" 합류");NoticeTitle.text="새 정착민";NoticeBody.text=data.DisplayName+"이 함께 생활합니다.";return true;
  }
  void RefreshStandees(){var people=Campaign?.Party.ToArray()??Array.Empty<Adventurer>();for(int i=0;i<StandeeObjects.Length;i++){StandeeObjects[i].SetActive(i<people.Length);if(i<people.Length)Roster.ApplyBody(StandeeBodies[i],PartySelectionSession.Selected.ElementAtOrDefault(i),ScoutBody,MedicBody);}}
 }
 public sealed partial class SettlementVisitorPanel
 {
  public GameObject RecruitPage;
  public Button InspectRecruit,RecruitBack,RecruitAction;
  public Text RecruitName,RecruitDetails,RecruitHint;
  public Text RecruitHealth,RecruitBag,RecruitHousing;
  public Image RecruitPortrait;
  string recruitPending;int recruitVisit;
  public string RecruitId=>state.RecruitId;
  public bool HasRecruited=>state.Recruited;
  PartyCandidate RecruitData=>owner.Roster.Candidates.FirstOrDefault(p=>p.Id==state.RecruitId);
  string NextRecruitId()=>owner.Roster.Candidates.Where(p=>!p.AvailableAtStart&&!PartySelectionSession.Selected.Contains(p.Id)&&owner.IsCharacterUnlocked(p.Id)).OrderBy(p=>p.UnlockOrder).FirstOrDefault()?.Id;
  void InitializeRecruitment(){RecruitPage.SetActive(false);InspectRecruit.onClick.AddListener(OpenRecruit);RecruitBack.onClick.AddListener(ReturnToConversation);RecruitAction.onClick.AddListener(RequestRecruit);}
  string RecruitBlock(string id,int visit)
  {
   if(owner.Campaign==null||owner.Campaign.Stage!=JourneyStage.Settlement||!IsPresent||visit!=state.VisitDay)return "방문이 끝났습니다.";
   if(state.Recruited||id!=state.RecruitId||string.IsNullOrEmpty(id)||PartySelectionSession.Selected.Contains(id))return "합류할 후보가 없습니다.";
   if(!owner.IsCharacterUnlocked(id))return "아직 만날 조건이 갖춰지지 않았습니다.";
   if(!owner.CraftPanel.CanAcceptResidents(1))return "거주 공간이 부족합니다. 옆방 거주 준비가 필요합니다.";
   return null;
  }
  public void OpenRecruit(){if(!IsPresent||RecruitData==null||state.Recruited)return;Conversation.SetActive(false);Trade.SetActive(false);RecruitPage.SetActive(true);RefreshRecruitment();Focus(RecruitBack);}
  void RefreshRecruitment()
  {
   var data=RecruitData;InspectRecruit.interactable=IsPresent&&data!=null&&!state.Recruited;
   InspectRecruit.GetComponentInChildren<Text>().text=state.Recruited?"정착민으로 합류했습니다":data==null?"합류할 후보가 없습니다":"함께 지낼 의향을 묻는다";
   if(!RecruitPage.activeSelf)return;
   RecruitName.text=data?.DisplayName??"방문자";RecruitPortrait.sprite=data?.Portrait;RecruitPortrait.enabled=RecruitPortrait.sprite;
   RecruitHealth.text=data==null?"체력  —":"체력  "+data.Health+" / "+data.Health;
   RecruitBag.text=data==null?"개인 가방  —":"개인 가방  "+data.BagCapacity+"칸";
   RecruitDetails.text=data==null?"소개할 후보가 없습니다.":data.RoleTitle+"\n"+data.TraitDescription+"\n“"+data.FirstLine+"”";
   var reason=RecruitBlock(state.RecruitId,state.VisitDay);RecruitAction.interactable=reason==null;
   RecruitHousing.text="거주 인원  "+owner.Campaign.Party.Count()+" / "+owner.CraftPanel.ResidentCapacity+"명";
   RecruitHousing.color=reason==null?new Color(.13f,.32f,.2f):new Color(.55f,.15f,.1f);
   RecruitHint.text=reason??"빈 가방으로 합류합니다.\n합류하면 이번 거래는 종료됩니다.";
  }
  public void RequestRecruit()
  {
   string reason=RecruitBlock(state.RecruitId,state.VisitDay);if(reason!=null){RecruitHint.text=reason;return;}
   recruitPending=state.RecruitId;recruitVisit=state.VisitDay;pending=null;dismissReview=false;
   ShowReview(RecruitData.DisplayName+"을 정착민으로 받아들일까요?\n인원 "+owner.Campaign.Party.Count()+" → "+(owner.Campaign.Party.Count()+1)+" / "+owner.CraftPanel.ResidentCapacity+"명\n빈 가방으로 합류 · 이번 거래 종료\n방문자 거래 재고는 창고에 추가되지 않습니다.");
  }
  void CommitRecruit(string id,int visit)
  {
   var reason=RecruitBlock(id,visit);if(reason!=null){Refresh();RecruitHint.text=reason;return;}
   if(!owner.RecruitResident(id)){RefreshRecruitment();return;}
   state.Recruited=true;state.Dismissed=true;ReturnToConversation();Sync();Dialogue.text=RecruitData.DisplayName+"이 정착민으로 합류했습니다.\n\n새 동료의 가방과 작업 배정을 확인하세요.";
  }
  public void ValidateRecruitment(SavedVisitor saved,SavedMember[] members,PartyRoster roster)
  {
   bool hasId=!string.IsNullOrEmpty(saved.RecruitId);
   if(hasId&&!roster.Candidates.Any(p=>p.Id==saved.RecruitId))throw new InvalidOperationException("알 수 없는 영입 후보입니다.");
   bool joined=hasId&&members.Any(p=>p.Id==saved.RecruitId);
   if(saved.VisitDay==0&&(hasId||saved.Recruited)||saved.Recruited&&(!joined||!saved.Dismissed)||!saved.Recruited&&joined)throw new InvalidOperationException("방문자 영입 기록이 일치하지 않습니다.");
  }
 }
}
namespace Demo5.NightRun
{
 public sealed partial class CampaignState
 {
  public bool AddResident(Adventurer member,int capacity){if(Stage!=JourneyStage.Settlement||IsFieldExpedition||member==null||member.Health<=0||Chosen.Count>=capacity||Party.Contains(member))return false;int index=Candidates.Length;Candidates=Candidates.Concat(new[]{member}).ToArray();Chosen.Add(index);return true;}
 }
}
