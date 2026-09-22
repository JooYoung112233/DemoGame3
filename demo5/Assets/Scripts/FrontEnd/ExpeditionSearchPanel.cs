using System;
using System.Linq;
using System.Collections.Generic;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 public sealed class ExpeditionSearchPanel:MonoBehaviour {
  [Serializable] public sealed class Assignment { public int ObjectIndex,Pace,Duty; public Adventurer Worker; }
  public GameObject View,Review; public CanvasGroup Workspace;
  public Text Title,Description,Cost,Equipment,ReviewBody,Notice;
  public Image ObjectIcon; public Sprite[] ObjectIcons;
  public Button Back,Choose,Confirm,Cancel; public Button[] Paces,Duties;
  public RectTransform MemberContent; public ExpeditionMemberCard MemberPrefab;
  public readonly List<ExpeditionMemberCard> Cards=new List<ExpeditionMemberCard>();
  public readonly Dictionary<int,Assignment> Assignments=new Dictionary<int,Assignment>();
  public bool IsOpen=>View.activeSelf;
  public int Pace{get;private set;}=1; public int Duty{get;private set;}
  public Adventurer Worker{get;private set;}
  ExpeditionArrivalPanel arrival; int site; readonly string[] hiddenNames={"ArrivalPaper","ArrivalTitle","Status","Hint","Return"}; bool[] hiddenStates; RectTransform fieldMembers; Vector2 memberPosition,memberSize;
  public void Initialize(ExpeditionArrivalPanel a){arrival=a;View.SetActive(false);Review.SetActive(false);Back.onClick.AddListener(Close);Choose.onClick.AddListener(Ask);Confirm.onClick.AddListener(Save);Cancel.onClick.AddListener(Dismiss);for(int i=0;i<3;i++){int k=i;Paces[i].onClick.AddListener(()=>{Pace=k;Refresh();});Duties[i].onClick.AddListener(()=>{Duty=k;Refresh();});}}
  public void ResetVisit(){Assignments.Clear();Worker=null;Pace=1;Duty=0;View.SetActive(false);Review.SetActive(false);}
  public void Open(int index){if(!arrival.IsOpen||arrival.InTransit||(arrival.Encounter&&arrival.Encounter.IsOpen)||arrival.Popup.activeSelf||IsOpen||arrival.Rooms.CurrentRoom!=0)return;site=index;hiddenStates=hiddenNames.Select(n=>arrival.Main.transform.Find(n).gameObject.activeSelf).ToArray();foreach(var n in hiddenNames)arrival.Main.transform.Find(n).gameObject.SetActive(false);fieldMembers=(RectTransform)arrival.MemberContent.parent;memberPosition=fieldMembers.anchoredPosition;memberSize=fieldMembers.sizeDelta;fieldMembers.anchoredPosition=new Vector2(780,-754);fieldMembers.sizeDelta=new Vector2(1060,178);View.SetActive(true);arrival.Main.interactable=arrival.Main.blocksRaycasts=false;Review.SetActive(false);Workspace.interactable=Workspace.blocksRaycasts=true;
   if(Assignments.TryGetValue(site,out var saved)){Worker=saved.Worker;Pace=saved.Pace;Duty=saved.Duty;}
   if(Worker!=null&&(!arrival.Participants.Contains(Worker)||Worker.Health<=0))Worker=null;
   foreach(var c in Cards){c.gameObject.SetActive(false);Destroy(c.gameObject);}Cards.Clear();
   foreach(var person in arrival.Participants){var card=Instantiate(MemberPrefab,MemberContent);card.Name.text=person.Name;var source=arrival.Cards[arrival.Participants.ToList().IndexOf(person)];card.Portrait.sprite=source.Portrait.sprite;card.Button.onClick.AddListener(()=>{Worker=person;Refresh();});Cards.Add(card);}
   Title.text=arrival.ObjectNames[site];Description.text=arrival.ObjectDescriptions[site];ObjectIcon.sprite=ObjectIcons[site];Notice.text="완료 전에 멈춰도 수색도는 유지됩니다.";Refresh();
  }
  void Refresh(){var progress=arrival.Loot.State(site);if(progress.Progress>0){Pace=progress.Pace;Duty=progress.Duty;}for(int i=0;i<3;i++){Paces[i].interactable=progress.Progress==0;Paces[i].GetComponent<Image>().color=i==Pace?new Color(1,.76f,.35f):Color.white;Duties[i].GetComponent<Image>().color=i==Duty?new Color(1,.76f,.35f):Color.white;Duties[i].interactable=progress.Progress==0&&(i==0||(i==1&&arrival.Participants.Count(p=>p.Health>0)>1));}
   if(progress.Progress==0&&!Duties[Duty].interactable)Duty=0;
   for(int i=0;i<Cards.Count;i++){var person=arrival.Participants[i];Cards[i].Paper.color=person==Worker?new Color(1,.76f,.35f):Color.white;Cards[i].Button.interactable=person.Health>0;}
   Equipment.text="조명 지원 · 손전등 장비 연결 예정";
   Cost.text="예상 "+(Pace+1)+"턴  ·  소음 "+new[]{"높음","보통","낮음"}[Pace]+"\n"+new[]{"눈에 띄는 곳부터 살핍니다.","주변을 고르게 살핍니다.","틈새까지 꼼꼼히 살핍니다."}[Pace];
   Cost.text="수색도 "+progress.Progress+" / "+(progress.Required>0?progress.Required:Pace+1)+"턴  ·  누적 소음 "+arrival.Rooms.Noise+"\n"+(Duty==0?"동료가 함께하면 발견 확률 +10%p":"망보기 동료가 있으면 턴당 소음 -1");Choose.GetComponentInChildren<Text>().text=progress.Progress>0?"다음 1턴 진행":"수색 시작";Choose.interactable=Worker!=null&&Worker.Health>0;
  }
  void Ask(){if(!IsOpen||!Choose.interactable||Review.activeSelf)return;ReviewBody.text=Title.text+"  ·  "+new[]{"빠른 수색","보통 수색","정밀 수색"}[Pace]+"\n\n담당  "+Worker.Name+"\n동료  "+(Duty==0?(arrival.Participants.Count>1?"함께 수색":"혼자 수색"):"망보기")+"\n이번 진행  1턴\n\n완료 시 발견물을 확인합니다. 중단해도 진행도는 유지됩니다.";Confirm.GetComponentInChildren<Text>().text="1턴 진행";Review.SetActive(true);Workspace.interactable=Workspace.blocksRaycasts=false;}
  void Save(){if(!IsOpen||!Review.activeSelf||Worker==null||Worker.Health<=0)return;Dismiss();if(!arrival.Loot.Advance(site,Pace,Worker,Duty)){Refresh();return;}Assignments[site]=new Assignment{ObjectIndex=site,Worker=Worker,Pace=Pace,Duty=Duty};if(arrival.Encounter&&arrival.Encounter.AfterSearch(site))return;if(arrival.Loot.State(site).Complete){Close();arrival.Loot.Open(site);}else{Refresh();arrival.Status.text=Title.text+" · 수색 중\n진행도 유지 · 다음 턴 대기";}}
  public void Dismiss(){Review.SetActive(false);Workspace.interactable=Workspace.blocksRaycasts=true;}
  public void Close(){Dismiss();if(hiddenStates!=null)for(int i=0;i<hiddenNames.Length;i++)arrival.Main.transform.Find(hiddenNames[i]).gameObject.SetActive(hiddenStates[i]);if(fieldMembers){fieldMembers.anchoredPosition=memberPosition;fieldMembers.sizeDelta=memberSize;}View.SetActive(false);arrival.Main.interactable=arrival.Main.blocksRaycasts=true;}
  public void Escape(){if(Review.activeSelf)Dismiss();else Close();}
 }
}





