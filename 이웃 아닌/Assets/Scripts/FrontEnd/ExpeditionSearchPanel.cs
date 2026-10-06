using System;
using System.Linq;
using System.Collections.Generic;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 // 속도 삭제와 사물 소음 (기획/탐험-수색쪽지와-협동-1차.md, 2026-09-25): the 07 window has no pace. An object makes its own noise on every
 // turn it is searched (shown in ObjectNoise, where the pace row was); 함께 수색 finishes one turn sooner, 망보기 lowers a noisy object's
 // noise by 1 (not offered on a silent one: '소음 없음'), 조명 지원 +LightBonus. Pace / Paces stay only so older callers and saves compile.
 // 말 놓기 (기획/탐험-말놓기-조작-재설계.md, 2026-09-25): on both visits the window is read only (ExpeditionSearchPanel.ReadOnly.cs) and opens
 // only from a right press on the object (FieldPawnBoard.RightPress → ExpeditionArrivalPanel.Inspect). The assigning paths below (first
 // visit: Ask/Save; site board: Board.cs) are unreachable while ReadOnly and are deleted in phase 2.
 public sealed partial class ExpeditionSearchPanel:MonoBehaviour {
  [Serializable] public sealed class Assignment { public int ObjectIndex,Pace=1,Duty; public Adventurer Worker; }
  public GameObject View,Review; public CanvasGroup Workspace;
  public SearchDropPreview DropPreview;
  public Text Title,Description,Cost,Equipment,ReviewBody,Notice;
  [Tooltip("사물 소음 줄 (옛 '수색 방식' 자리 · AgentScripts/BuildSiteNoise.cs가 연결)")] public Text ObjectNoise;
  public Image ToolIcon;public Image ObjectIcon; public Sprite[] ObjectIcons;
  // Paces: the old pace buttons, hidden by AgentScripts/BuildSiteNoise.cs; nothing listens to them.
  public Button Back,Choose,Confirm,Cancel; public Button[] Paces,Duties;
  public RectTransform MemberContent; public ExpeditionMemberCard MemberPrefab;
  [Header("사물 소음 줄 · 확인창 제목 (첫 방문과 장소 판 공통)")]
  [Tooltip("{0}: 사물 소음 (수색하는 턴마다) · {1}: 기본 수색 턴")] public string NoiseLine="사물 소음 {0} · 기본 {1}턴";
  [Tooltip("큰 소리 사물 (소음이 FieldSiteRules.LoudNoise 이상) 뒤에 붙음")] public string NoiseLoudSuffix=" · 큰 소리";
  [Tooltip("소음 없는 사물 ({1}: 기본 수색 턴) · 망보기를 고를 수 없는 이유")] public string NoiseQuietLine="소음 없음 · 기본 {1}턴 · 망보기 필요 없음";
  [Tooltip("확인창 제목 뒤 · {0}: 이번 턴 소음")] public string ReviewNoise="소음 {0}";
  [Tooltip("확인창 제목 뒤 · 이번 턴 소음이 없을 때")] public string ReviewQuiet="소음 없음";
  [Header("첫 방문 문구 (수색 창 · 확인창)")]
  [Tooltip("{0}: 수색도 · {1}: 필요한 턴 · {2}: 누적 소음")] public string FirstCost="수색도 {0} / {1}턴  ·  누적 소음 {2}";
  [Tooltip("함께 수색 (살아 있는 동료가 있을 때)")] public string FirstTogether="함께 수색 · 1턴 빨리";
  [Tooltip("함께 수색 · 함께할 동료가 없을 때")] public string FirstAlone="혼자 수색 · 함께할 동료 없음";
  [Tooltip("{0}: 사물 소음 · {1}: 망보기 뒤 소음")] public string FirstWatch="망보기 · 소음 {0}→{1}";
  [Tooltip("진행 중인 망보기 수색 · 망볼 동료가 쓰러졌을 때 (혼자 이어 감) · {0}: 이번 턴 소음")] public string FirstWatchAlone="망보기 · 망볼 동료 없음 · 소음 {0}";
  [Tooltip("{0}: 조명 동료 · {1}: 발견 보정 (%p)")] public string FirstLight="{0} · 조명 +{1}%p";
  public string FirstLightNobody="지원자 없음";
  public readonly List<ExpeditionMemberCard> Cards=new List<ExpeditionMemberCard>();
  public readonly Dictionary<int,Assignment> Assignments=new Dictionary<int,Assignment>();
  public bool IsOpen=>View.activeSelf;
  // Pace: legacy, always 1 for a new search (no rule reads it).
  public int Pace{get;private set;}=1; public int Duty{get;private set;}
  public Adventurer Worker{get;private set;}
  // The object the window shows (−1 before it first opened).
  public int Site=>site;
  ExpeditionArrivalPanel arrival; int site=-1; readonly string[] hiddenNames={"ArrivalPaper","ArrivalTitle","Status","Hint","Return"}; bool[] hiddenStates; RectTransform fieldMembers; Vector2 memberPosition,memberSize;bool membersWereActive;
  public void Initialize(ExpeditionArrivalPanel a){arrival=a;View.SetActive(false);Review.SetActive(false);Back.onClick.AddListener(Close);Choose.onClick.AddListener(Ask);Confirm.onClick.AddListener(Save);Cancel.onClick.AddListener(Dismiss);for(int i=0;i<3;i++){int k=i;Duties[i].onClick.AddListener(()=>OnDuty(k));}}
  public void ResetVisit(){ResetBoard();Assignments.Clear();Worker=null;Pace=1;Duty=0;View.SetActive(false);Review.SetActive(false);}
  public void Open(int index){if(!arrival.IsOpen||arrival.InTransit||(arrival.Encounter&&arrival.Encounter.IsOpen)||arrival.Popup.activeSelf||IsOpen||!arrival.Loot.IsSiteInCurrentRoom(index))return;site=index;hiddenStates=hiddenNames.Select(n=>arrival.Main.transform.Find(n).gameObject.activeSelf).ToArray();foreach(var n in hiddenNames)arrival.Main.transform.Find(n).gameObject.SetActive(false);fieldMembers=(RectTransform)arrival.MemberContent.parent;memberPosition=fieldMembers.anchoredPosition;memberSize=fieldMembers.sizeDelta;membersWereActive=fieldMembers.gameObject.activeSelf;fieldMembers.gameObject.SetActive(false);View.SetActive(true);arrival.Main.interactable=arrival.Main.blocksRaycasts=false;Review.SetActive(false);Workspace.interactable=Workspace.blocksRaycasts=true;
   bool readOnly=ReadOnly;
   if(!readOnly&&Assignments.TryGetValue(site,out var saved)){Worker=saved.Worker;Duty=saved.Duty;}
   if(Worker!=null&&(!arrival.Participants.Contains(Worker)||Worker.Health<=0))Worker=null;
   if(!readOnly&&Board)BoardOpen();
   foreach(var c in Cards){c.gameObject.SetActive(false);Destroy(c.gameObject);}Cards.Clear();
   // Read only: nobody is picked here (the member cards are the old way to assign; the builder hides their row).
   if(!readOnly)foreach(var person in arrival.Participants){var card=Instantiate(MemberPrefab,MemberContent);card.Name.text=person.Name;var source=arrival.Cards[arrival.Participants.ToList().IndexOf(person)];card.Portrait.sprite=source.Portrait.sprite;card.Button.onClick.AddListener(()=>OnCard(person));Cards.Add(card);}
   Title.text=arrival.ObjectNames[site];Description.text=arrival.ObjectDescriptions[site];ObjectIcon.sprite=ObjectIcons[site];Notice.text="완료 전에 멈춰도 수색도는 유지됩니다.";Refresh();
  }
  // The object's own noise where the pace row was (both modes): '사물 소음 N · 기본 T턴' (+ 큰 소리), or '소음 없음' (why 망보기 is not offered).
  void ShowObjectNoise(){if(!ObjectNoise)return;int n=arrival.Loot.SiteNoise(site),turns=arrival.Loot.SiteTurns(site),loud=arrival.Threat?arrival.Threat.Rules.LoudNoise:3;ObjectNoise.text=n>0?string.Format(NoiseLine,n,turns)+(n>=loud?NoiseLoudSuffix:""):string.Format(NoiseQuietLine,n,turns);}
  string NoiseLabel(int noise)=>noise>0?string.Format(ReviewNoise,noise):ReviewQuiet;
  // First visit: the role line under the cost (the same numbers Advance applies: ExpeditionLootPanel.RequiredFor / NoiseFor / BonusFor).
  // A running 망보기 search whose watcher went down goes on alone at the object's full noise (FirstWatchAlone).
  string FirstRole(int duty,int noise)=>duty==2?string.Format(FirstLight,arrival.Loot.LightSupport(Worker)?.Name??FirstLightNobody,arrival.Loot.BonusFor(site,duty))
   :duty==1?(arrival.Loot.AliveCount>1?string.Format(FirstWatch,arrival.Loot.SiteNoise(site),noise):string.Format(FirstWatchAlone,noise)):arrival.Loot.AliveCount>1?FirstTogether:FirstAlone;
  void Refresh(){if(ReadOnly){ReadOnlyRefresh();return;}if(Board){BoardRefresh();return;}var progress=arrival.Loot.State(site);if(progress.Progress>0)Duty=progress.Duty;
   // 망보기 only on a noisy object with someone to watch (a silent one has nothing to lower: '소음 없음' in ObjectNoise). Choose below needs
   // only the required helper (조명's lamp), so a running 망보기 search goes on after its watcher is down.
   for(int i=0;i<3;i++){Duties[i].GetComponent<Image>().color=i==Duty?new Color(1,.76f,.35f):Color.white;Duties[i].interactable=progress.Progress==0&&arrival.Loot.CanSupport(i,Worker)&&(i!=1||arrival.Loot.CanOfferWatch(site));}
   if(progress.Progress==0&&!Duties[Duty].interactable)Duty=0;
   ShowObjectNoise();
   int noise=arrival.Loot.NoiseFor(site,Duty),required=arrival.Loot.RequiredFor(site,Duty);
   if(DropPreview)DropPreview.Refresh(arrival,site,progress.Progress>0?progress.Pace:1,Duty,arrival.Loot.BonusFor(site,Duty),noise,required);
   for(int i=0;i<Cards.Count;i++){var person=arrival.Participants[i];Cards[i].Paper.color=person==Worker?new Color(1,.76f,.35f):Color.white;Cards[i].Button.interactable=person.Health>0;}
   string tool=arrival.Loot.Sites[site].RequiredTool;var toolItem=arrival.Inventory.Items.FirstOrDefault(i=>i.Id==tool);bool needsTool=!string.IsNullOrEmpty(tool),opened=progress.Opened;
   if(ToolIcon){ToolIcon.gameObject.SetActive(needsTool);ToolIcon.sprite=toolItem?.Icon;}
   Equipment.text=!needsTool?"도구 없이 수색 가능":opened?"덮개 개방 완료 · 도구 없이 재개 가능":(toolItem?.Name??tool)+" · "+(Worker==null?"담당자 선택":arrival.Inventory.CountFor(Worker,tool)>0?"휴대 확인 · 소모 없음":"담당자 가방에 필요");
   Notice.text=Duty==2?"손전등은 소모되지 않습니다 · 수색도는 유지됩니다.":"조명 지원: 수색 담당자 외 손전등을 가진 동료 필요";
   Cost.text=string.Format(FirstCost,progress.Progress,required,arrival.Rooms.Noise)+"\n"+FirstRole(Duty,noise);Choose.GetComponentInChildren<Text>().text=progress.Progress>0?"다음 1턴 진행":"수색 시작";Choose.interactable=arrival.Loot.CanSearch(site,Worker)&&arrival.Loot.CanSupport(Duty,Worker);if(Duty==2&&!arrival.Loot.CanSupport(Duty,Worker))Choose.GetComponentInChildren<Text>().text="손전등 동료 필요";if(needsTool&&!opened&&!arrival.Loot.CanSearch(site,Worker))Choose.GetComponentInChildren<Text>().text=Worker==null?"담당자 선택":"지렛대 필요";
  }
  // Read only: the window never assigns nor passes a turn ('턴 진행' only).
  void Ask(){if(ReadOnly||!IsOpen||!Choose.interactable||Review.activeSelf)return;if(Board){BoardAsk();return;}var st=arrival.Loot.State(site);int duty=st.Progress>0?st.Duty:Duty,noise=arrival.Loot.NoiseFor(site,duty);
   ReviewBody.text=Title.text+"  ·  "+NoiseLabel(noise)+"\n\n담당  "+Worker.Name+"\n동료  "+(duty==2?"조명 · "+(arrival.Loot.LightSupport(Worker)?.Name??"지원 불가"):FirstRole(duty,noise))+"\n이번 진행  1턴"+(!string.IsNullOrEmpty(arrival.Loot.Sites[site].RequiredTool)&&!st.Opened?"\n덮개 개방 포함 · 도구 소모 없음":"")+"\n\n완료 시 발견물을 확인합니다. 중단해도 진행도는 유지됩니다."+(arrival.Threat&&arrival.Threat.State!=null?"\n"+arrival.Threat.PreviewLine(noise):"");Confirm.GetComponentInChildren<Text>().text="1턴 진행";Review.SetActive(true);Workspace.interactable=Workspace.blocksRaycasts=false;}
  void Save(){if(ReadOnly){Dismiss();return;}if(Board){BoardSave();return;}if(!IsOpen||!Review.activeSelf||Worker==null||Worker.Health<=0)return;Dismiss();if(!arrival.Loot.Advance(site,1,Worker,Duty)){Refresh();return;}Assignments[site]=new Assignment{ObjectIndex=site,Worker=Worker,Duty=Duty};if(arrival.Encounter&&arrival.Encounter.AfterSearch(site))return;if(arrival.Loot.State(site).Complete){Close();arrival.Loot.Open(site);}else{Refresh();arrival.Status.text=Title.text+" · 수색 중\n진행도 유지 · 다음 턴 대기";}}
  public void Dismiss(){Review.SetActive(false);Workspace.interactable=Workspace.blocksRaycasts=true;}
  public void Close(){Dismiss();if(hiddenStates!=null)for(int i=0;i<hiddenNames.Length;i++)arrival.Main.transform.Find(hiddenNames[i]).gameObject.SetActive(hiddenStates[i]);if(fieldMembers){fieldMembers.gameObject.SetActive(membersWereActive);fieldMembers.anchoredPosition=memberPosition;fieldMembers.sizeDelta=memberSize;}View.SetActive(false);arrival.Main.interactable=arrival.Main.blocksRaycasts=true;}
  public void Escape(){if(Review.activeSelf)Dismiss();else Close();}
 }
}
