using System;
using System.Linq;
using System.Collections.Generic;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 public sealed partial class ExpeditionSearchPanel:MonoBehaviour {
  [Serializable] public sealed class Assignment { public int ObjectIndex,Pace,Duty; public Adventurer Worker; }
  public GameObject View,Review; public CanvasGroup Workspace;
  public SearchDropPreview DropPreview;
  public Text Title,Description,Cost,Equipment,ReviewBody,Notice;
  public Image ToolIcon;public Image ObjectIcon; public Sprite[] ObjectIcons;
  public Button Back,Choose,Confirm,Cancel; public Button[] Paces,Duties;
  public RectTransform MemberContent; public ExpeditionMemberCard MemberPrefab;
  public readonly List<ExpeditionMemberCard> Cards=new List<ExpeditionMemberCard>();
  public readonly Dictionary<int,Assignment> Assignments=new Dictionary<int,Assignment>();
  public bool IsOpen=>View.activeSelf;
  public int Pace{get;private set;}=1; public int Duty{get;private set;}
  public Adventurer Worker{get;private set;}
  ExpeditionArrivalPanel arrival; int site; readonly string[] hiddenNames={"ArrivalPaper","ArrivalTitle","Status","Hint","Return"}; bool[] hiddenStates; RectTransform fieldMembers; Vector2 memberPosition,memberSize;bool membersWereActive;
  public void Initialize(ExpeditionArrivalPanel a){arrival=a;View.SetActive(false);Review.SetActive(false);Back.onClick.AddListener(Close);Choose.onClick.AddListener(Ask);Confirm.onClick.AddListener(Save);Cancel.onClick.AddListener(Dismiss);for(int i=0;i<3;i++){int k=i;Paces[i].onClick.AddListener(()=>{Pace=k;Refresh();});Duties[i].onClick.AddListener(()=>OnDuty(k));}}
  public void ResetVisit(){ResetBoard();Assignments.Clear();Worker=null;Pace=1;Duty=0;View.SetActive(false);Review.SetActive(false);}
  public void Open(int index){if(!arrival.IsOpen||arrival.InTransit||(arrival.Encounter&&arrival.Encounter.IsOpen)||arrival.Popup.activeSelf||IsOpen||!arrival.Loot.IsSiteInCurrentRoom(index))return;site=index;hiddenStates=hiddenNames.Select(n=>arrival.Main.transform.Find(n).gameObject.activeSelf).ToArray();foreach(var n in hiddenNames)arrival.Main.transform.Find(n).gameObject.SetActive(false);fieldMembers=(RectTransform)arrival.MemberContent.parent;memberPosition=fieldMembers.anchoredPosition;memberSize=fieldMembers.sizeDelta;membersWereActive=fieldMembers.gameObject.activeSelf;fieldMembers.gameObject.SetActive(false);View.SetActive(true);arrival.Main.interactable=arrival.Main.blocksRaycasts=false;Review.SetActive(false);Workspace.interactable=Workspace.blocksRaycasts=true;
   if(Assignments.TryGetValue(site,out var saved)){Worker=saved.Worker;Pace=saved.Pace;Duty=saved.Duty;}
   if(Worker!=null&&(!arrival.Participants.Contains(Worker)||Worker.Health<=0))Worker=null;
   if(Board)BoardOpen();
   foreach(var c in Cards){c.gameObject.SetActive(false);Destroy(c.gameObject);}Cards.Clear();
   foreach(var person in arrival.Participants){var card=Instantiate(MemberPrefab,MemberContent);card.Name.text=person.Name;var source=arrival.Cards[arrival.Participants.ToList().IndexOf(person)];card.Portrait.sprite=source.Portrait.sprite;card.Button.onClick.AddListener(()=>OnCard(person));Cards.Add(card);}
   Title.text=arrival.ObjectNames[site];Description.text=arrival.ObjectDescriptions[site];ObjectIcon.sprite=ObjectIcons[site];Notice.text="완료 전에 멈춰도 수색도는 유지됩니다.";Refresh();
  }
  void Refresh(){if(Board){BoardRefresh();return;}var progress=arrival.Loot.State(site);if(progress.Progress>0){Pace=progress.Pace;Duty=progress.Duty;}for(int i=0;i<3;i++){Paces[i].interactable=progress.Progress==0;Paces[i].GetComponent<Image>().color=i==Pace?new Color(1,.76f,.35f):Color.white;Duties[i].GetComponent<Image>().color=i==Duty?new Color(1,.76f,.35f):Color.white;Duties[i].interactable=progress.Progress==0&&arrival.Loot.CanSupport(i,Worker)&&(i!=1||Pace==0);}
   if(progress.Progress==0&&!Duties[Duty].interactable)Duty=0;
   if(DropPreview)DropPreview.Refresh(arrival,site,Pace,Duty);
   for(int i=0;i<Cards.Count;i++){var person=arrival.Participants[i];Cards[i].Paper.color=person==Worker?new Color(1,.76f,.35f):Color.white;Cards[i].Button.interactable=person.Health>0;}
   string tool=arrival.Loot.Sites[site].RequiredTool;var toolItem=arrival.Inventory.Items.FirstOrDefault(i=>i.Id==tool);bool required=!string.IsNullOrEmpty(tool),opened=progress.Opened;
   if(ToolIcon){ToolIcon.gameObject.SetActive(required);ToolIcon.sprite=toolItem?.Icon;}
   Equipment.text=!required?"도구 없이 수색 가능":opened?"덮개 개방 완료 · 도구 없이 재개 가능":(toolItem?.Name??tool)+" · "+(Worker==null?"담당자 선택":arrival.Inventory.CountFor(Worker,tool)>0?"휴대 확인 · 소모 없음":"담당자 가방에 필요");
   Notice.text=Duty==2?"손전등은 소모되지 않습니다 · 수색도는 유지됩니다.":"조명 지원: 수색 담당자 외 손전등을 가진 동료 필요";
   Cost.text="수색도 "+progress.Progress+" / "+(progress.Required>0?progress.Required:Pace+1)+"턴  ·  누적 소음 "+arrival.Rooms.Noise+"\n"+(Duty==2?(arrival.Loot.LightSupport(Worker)?.Name??"지원자 없음")+" · 조명 +"+arrival.Loot.BonusFor(site,Duty)+"%p":Duty==0?"동료가 함께하면 발견 확률 +10%p":"망보기 동료가 있으면 턴당 소음 -1");Choose.GetComponentInChildren<Text>().text=progress.Progress>0?"다음 1턴 진행":"수색 시작";Choose.interactable=arrival.Loot.CanSearch(site,Worker)&&arrival.Loot.CanSupport(Duty,Worker);if(Duty==2&&!arrival.Loot.CanSupport(Duty,Worker))Choose.GetComponentInChildren<Text>().text="손전등 동료 필요";if(required&&!opened&&!arrival.Loot.CanSearch(site,Worker))Choose.GetComponentInChildren<Text>().text=Worker==null?"담당자 선택":"지렛대 필요";
  }
  void Ask(){if(!IsOpen||!Choose.interactable||Review.activeSelf)return;if(Board){BoardAsk();return;}ReviewBody.text=Title.text+"  ·  "+new[]{"빠른 수색","보통 수색","정밀 수색"}[Pace]+"\n\n담당  "+Worker.Name+"\n동료  "+(Duty==0?(arrival.Participants.Count>1?"함께 수색":"혼자 수색"):Duty==2?"조명 · "+(arrival.Loot.LightSupport(Worker)?.Name??"지원 불가"):"망보기")+"\n이번 진행  1턴"+(!string.IsNullOrEmpty(arrival.Loot.Sites[site].RequiredTool)&&!arrival.Loot.State(site).Opened?"\n덮개 개방 포함 · 도구 소모 없음":"")+"\n\n완료 시 발견물을 확인합니다. 중단해도 진행도는 유지됩니다."+(arrival.Threat&&arrival.Threat.State!=null?"\n"+arrival.Threat.PreviewLine(arrival.Loot.NoiseFor(arrival.Loot.State(site).Progress>0?arrival.Loot.State(site).Pace:Pace,arrival.Loot.State(site).Progress>0?arrival.Loot.State(site).Duty:Duty)):"");Confirm.GetComponentInChildren<Text>().text="1턴 진행";Review.SetActive(true);Workspace.interactable=Workspace.blocksRaycasts=false;}
  void Save(){if(Board){BoardSave();return;}if(!IsOpen||!Review.activeSelf||Worker==null||Worker.Health<=0)return;Dismiss();if(!arrival.Loot.Advance(site,Pace,Worker,Duty)){Refresh();return;}Assignments[site]=new Assignment{ObjectIndex=site,Worker=Worker,Pace=Pace,Duty=Duty};if(arrival.Encounter&&arrival.Encounter.AfterSearch(site))return;if(arrival.Loot.State(site).Complete){Close();arrival.Loot.Open(site);}else{Refresh();arrival.Status.text=Title.text+" · 수색 중\n진행도 유지 · 다음 턴 대기";}}
  public void Dismiss(){Review.SetActive(false);Workspace.interactable=Workspace.blocksRaycasts=true;}
  public void Close(){Dismiss();if(hiddenStates!=null)for(int i=0;i<hiddenNames.Length;i++)arrival.Main.transform.Find(hiddenNames[i]).gameObject.SetActive(hiddenStates[i]);if(fieldMembers){fieldMembers.gameObject.SetActive(membersWereActive);fieldMembers.anchoredPosition=memberPosition;fieldMembers.sizeDelta=memberSize;}View.SetActive(false);arrival.Main.interactable=arrival.Main.blocksRaycasts=true;}
  public void Escape(){if(Review.activeSelf)Dismiss();else Close();}
 }
}





