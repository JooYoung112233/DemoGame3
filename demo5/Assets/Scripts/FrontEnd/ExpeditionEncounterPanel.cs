using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 public sealed class ExpeditionEncounterPanel:MonoBehaviour {
  public ExpeditionBattlePanel Battle; public int EnemyCount{get;private set;}
  public GameObject View,Review,Banner;public CanvasGroup Workspace;
  public Text Body,EnemyLabel,Hint,ReviewBody,BannerText;
  public Button Fight,Wait,Retreat,Confirm,Cancel; public RectTransform Members;
  readonly string[] hiddenNames={"ArrivalPaper","ArrivalTitle","Status","Hint","Return","Members"};bool[] hiddenStates;
  [Range(0,100)] public int BaseChance=15,MaximumChance=70,WaitChance=65;
  [Min(0)]public int NoiseThreshold=2,ChancePerNoise=7,GraceSearches=2;
  public bool IsOpen=>View.activeSelf;public bool Warned{get;private set;}public int Cooldown{get;private set;}public int Rolls{get;private set;}
  Text risk; ExpeditionArrivalPanel arrival;int lastTurn=-1,searches,site,choice=-1,waitFailures;bool choosing;
  public void Initialize(ExpeditionArrivalPanel a){arrival=a;risk=a.Main.transform.Find("Risk").GetComponent<Text>();View.SetActive(false);Review.SetActive(false);Banner.SetActive(false);Fight.interactable=Battle!=null;if(Battle){Battle.Initialize(a,this);Fight.onClick.AddListener(()=>{if(IsOpen&&!Review.activeSelf&&!choosing)Battle.Begin(EnemyCount);});}Wait.onClick.AddListener(()=>Ask(0));Retreat.onClick.AddListener(()=>Ask(1));Cancel.onClick.AddListener(CancelChoice);Confirm.onClick.AddListener(Resolve);}
  public void ResetVisit(){risk.text="위험 · 미확인";lastTurn=-1;searches=Rolls=0;Cooldown=0;Warned=false;View.SetActive(false);Review.SetActive(false);Banner.SetActive(false);}
  public bool AfterSearch(int index){if(IsOpen||arrival.Rooms.Turns==lastTurn)return IsOpen;lastTurn=arrival.Rooms.Turns;searches++;
   if(Cooldown>0){Cooldown--;return false;}
   if(!Warned){if(arrival.Rooms.Noise>=NoiseThreshold){Warned=true;risk.text="위험 · 기척";Banner.SetActive(true);BannerText.text="… 가까운 곳에서 발소리가 들립니다.";}return false;}
   if(searches<2)return false;Rolls++;int chance=Mathf.Clamp(BaseChance+arrival.Rooms.Noise*ChancePerNoise,0,MaximumChance);if(UnityEngine.Random.Range(0,100)>=chance)return false;
   risk.text="위험 · 조우 중";site=index;waitFailures=0;choice=-1;choosing=false;if(arrival.Search.IsOpen)arrival.Search.Close();Banner.SetActive(false);hiddenStates=hiddenNames.Select(n=>arrival.Main.transform.Find(n).gameObject.activeSelf).ToArray();foreach(var n in hiddenNames)arrival.Main.transform.Find(n).gameObject.SetActive(false);foreach(Transform old in Members){old.gameObject.SetActive(false);Destroy(old.gameObject);}foreach(var source in arrival.Cards){var card=Instantiate(source,Members);card.Button.interactable=false;card.Role.text="조우 중";card.State.text="대기 중";}View.SetActive(true);Review.SetActive(false);Workspace.interactable=Workspace.blocksRaycasts=true;arrival.Main.interactable=arrival.Main.blocksRaycasts=false;
   Body.text="가까운 곳에서 무언가 움직입니다.\n수색을 멈추고 몸을 낮춥니다.";EnemyCount=UnityEngine.Random.Range(1,3);EnemyLabel.text="무언가 "+EnemyCount;Refresh();return true;
  }
  int CurrentWaitChance=>Mathf.Max(20,WaitChance-waitFailures*15);
  void Refresh(){Hint.text="선택 전에는 시간이 흐르지 않습니다.\n숨어 기다리기 · 1턴 / 이탈 확률 "+CurrentWaitChance+"%";}
  void Ask(int value){if(!IsOpen||Review.activeSelf||choosing)return;choice=value;ReviewBody.text=value==0?"숨어 기다리기 · 1턴\n\n감염자가 지나갈 확률 "+CurrentWaitChance+"%\n실패하면 조우 상태가 유지됩니다.\n이번 행동에서는 부상이나 물자 손실이 없습니다.":(arrival.Rooms.RetreatRoomName+"로")+" 물러나기 · 원정대 전체 1턴\n\n수색도와 발견물은 이곳에 남습니다.\n가방은 그대로 챙겨 이전 방으로 이동합니다.";Review.SetActive(true);Workspace.interactable=Workspace.blocksRaycasts=false;}
  public void CancelChoice(){choice=-1;Review.SetActive(false);Workspace.interactable=Workspace.blocksRaycasts=true;}
  void Resolve(){if(!IsOpen||!Review.activeSelf||choice<0||choosing)return;choosing=true;int action=choice;CancelChoice();
   if(action==0){int chance=CurrentWaitChance;arrival.Rooms.SpendSearchTurn(-2);if(UnityEngine.Random.Range(0,100)>=chance){waitFailures++;Body.text="발소리가 가까운 곳에 멈췄습니다.\n아직 움직이기 어렵습니다.";choosing=false;Refresh();return;}}
   Cooldown=GraceSearches;Warned=false;risk.text="위험 · 경계";View.SetActive(false);Banner.SetActive(false);for(int i=0;i<hiddenNames.Length;i++)arrival.Main.transform.Find(hiddenNames[i]).gameObject.SetActive(hiddenStates[i]);arrival.Main.interactable=arrival.Main.blocksRaycasts=true;choosing=false;
   if(action==1){arrival.Rooms.AskMove();arrival.Rooms.ConfirmMove();return;}
   if(arrival.Loot.State(site).Complete)arrival.Loot.Open(site);else arrival.Search.Open(site);
  }
  // Battle noise (gunshots plus the fight itself) carries into later encounter rolls.
  public void FinishBattle(bool retreat,int noise=2){
   if(!IsOpen)return;Cooldown=GraceSearches;Warned=false;risk.text="위험 · 경계";View.SetActive(false);Banner.SetActive(false);
   for(int i=0;i<hiddenNames.Length;i++)arrival.Main.transform.Find(hiddenNames[i]).gameObject.SetActive(hiddenStates[i]);
   arrival.Main.interactable=arrival.Main.blocksRaycasts=true;choosing=false;
   if(retreat){arrival.Rooms.AddNoise(noise);arrival.Rooms.AskMove();arrival.Rooms.ConfirmMove();return;}
   arrival.Rooms.SpendSearchTurn(noise);
   if(arrival.Loot.State(site).Complete)arrival.Loot.Open(site);else arrival.Search.Open(site);
  }
  public void Escape(){if(Battle&&Battle.IsOpen){Battle.Escape();return;}if(Review.activeSelf)CancelChoice();}
 }
}

