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
  [Tooltip("첫 방문 무작위 조우의 최대 마리 수")][Min(1)]public int MaxRandomEnemies=1;
  public bool IsOpen=>View.activeSelf;public bool Warned{get;private set;}public int Cooldown{get;private set;}public int Rolls{get;private set;}
  Text risk; ExpeditionArrivalPanel arrival; System.Collections.Generic.IList<BattleCreature> pendingLineup; ExpeditionSiteThreat Threat=>arrival?arrival.Threat:null;int lastTurn=-1,searches,site,choice=-1,waitFailures;bool choosing;
  public void Initialize(ExpeditionArrivalPanel a){arrival=a;risk=a.Main.transform.Find("Risk").GetComponent<Text>();View.SetActive(false);Review.SetActive(false);Banner.SetActive(false);Fight.interactable=Battle!=null;if(Battle){Battle.Initialize(a,this);Fight.onClick.AddListener(()=>{if(IsOpen&&!Review.activeSelf&&!choosing){if(pendingLineup!=null)Battle.NextLineup=pendingLineup;Battle.Begin(EnemyCount);}});}Wait.onClick.AddListener(()=>Ask(0));Retreat.onClick.AddListener(()=>Ask(1));Cancel.onClick.AddListener(CancelChoice);Confirm.onClick.AddListener(Resolve);}
  public void ResetVisit(){risk.text="위험 · 미확인";lastTurn=-1;searches=Rolls=0;Cooldown=0;Warned=false;View.SetActive(false);Review.SetActive(false);Banner.SetActive(false);}
  public bool AfterSearch(int index){if(IsOpen||arrival.Rooms.Turns==lastTurn)return IsOpen;lastTurn=arrival.Rooms.Turns;searches++;if(Threat&&Threat.Active)return false;
   if(Cooldown>0){Cooldown--;return false;}
   if(!Warned){if(arrival.Rooms.Noise>=NoiseThreshold){Warned=true;risk.text="위험 · 기척";Banner.SetActive(true);BannerText.text="… 가까운 곳에서 발소리가 들립니다.";}return false;}
   if(searches<2)return false;Rolls++;int chance=Mathf.Clamp(BaseChance+arrival.Rooms.Noise*ChancePerNoise,0,MaximumChance);if(UnityEngine.Random.Range(0,100)>=chance)return false;
   OpenView(index);Body.text="가까운 곳에서 무언가 움직입니다.\n수색을 멈추고 몸을 낮춥니다.";EnemyCount=UnityEngine.Random.Range(1,MaxRandomEnemies+1);EnemyLabel.text="무언가 "+EnemyCount;pendingLineup=null;Refresh();return true;
  }
  bool Board=>Threat&&Threat.Active;
  string RetreatTitle=>arrival.Rooms.CurrentRoom==0?"출구로 빠져나가기":arrival.Rooms.RetreatRoomName+"로 물러나기";
  void OpenView(int index){if(arrival.FieldBags&&arrival.FieldBags.IsOpen)arrival.FieldBags.Close();risk.text="위험 · 조우 중";site=index;waitFailures=0;choice=-1;choosing=false;if(arrival.Search.IsOpen)arrival.Search.Close();Banner.SetActive(false);hiddenStates=hiddenNames.Select(n=>arrival.Main.transform.Find(n).gameObject.activeSelf).ToArray();foreach(var n in hiddenNames)arrival.Main.transform.Find(n).gameObject.SetActive(false);foreach(Transform old in Members){old.gameObject.SetActive(false);Destroy(old.gameObject);}foreach(var source in arrival.Cards){var card=Instantiate(source,Members);card.Button.interactable=false;card.Role.text="조우 중";card.State.text="대기 중";}View.SetActive(true);Review.SetActive(false);Workspace.interactable=Workspace.blocksRaycasts=true;arrival.Main.interactable=arrival.Main.blocksRaycasts=false;}
  // The site board's meeting: the thing and the party stand in the same room. Always one of it; the battle gets that creature.
  public bool OpenThreat(string body,string label,System.Collections.Generic.IList<BattleCreature> lineup){if(IsOpen)return true;lastTurn=arrival.Rooms.Turns;OpenView(-1);Body.text=body;EnemyCount=1;EnemyLabel.text=label;pendingLineup=lineup;Refresh();return true;}
  int CurrentWaitChance=>Mathf.Max(20,WaitChance-waitFailures*15);
  void Refresh(){Hint.text="선택 전에는 시간이 흐르지 않습니다.\n숨어 기다리기 · 1턴 / "+(Board?"위험도 2 이하면 지나감":"이탈 확률 "+CurrentWaitChance+"%");
   var labels=Retreat.GetComponentsInChildren<Text>(true).OrderByDescending(t=>t.fontSize).ToArray();if(labels.Length>0)labels[0].text=RetreatTitle;if(labels.Length>1)labels[1].text=arrival.Rooms.CurrentRoom==0?"거점으로 귀환 · 챙긴 물건 유지":"원정대 전체 1턴 · 수색도와 물건 보존";}
  void Ask(int value){if(!IsOpen||Review.activeSelf||choosing)return;choice=value;ReviewBody.text=value==0?(Board?"숨죽여 기다리기 · 1턴\n\n위험도 2 이하면 무언가가 지나갑니다.\n위험도 3(추적)이면 지나가지 않습니다.\n소음 없음 · 게이지 −1":"숨어 기다리기 · 1턴\n\n무언가 지나갈 확률 "+CurrentWaitChance+"%\n실패하면 조우 상태가 유지됩니다.\n이번 행동에서는 부상이나 물자 손실이 없습니다.")
    :arrival.Rooms.CurrentRoom==0?"출구로 빠져나가 거점으로 돌아갑니다.\n\n수색도와 발견물은 이곳에 남습니다.\n챙긴 물건은 그대로 가져갑니다.":(arrival.Rooms.RetreatRoomName+"로")+" 물러나기 · 원정대 전체 1턴\n\n수색도와 발견물은 이곳에 남습니다.\n가방은 그대로 챙겨 이전 방으로 이동합니다.";Review.SetActive(true);Workspace.interactable=Workspace.blocksRaycasts=false;}
  public void CancelChoice(){choice=-1;Review.SetActive(false);Workspace.interactable=Workspace.blocksRaycasts=true;}
  void Resolve(){if(!IsOpen||!Review.activeSelf||choice<0||choosing)return;choosing=true;int action=choice;CancelChoice();
   // Site board: hiding is one hushed turn on the board (no dice). It passes by at 위험도 2 or below; hunting (3) it does not.
   if(action==0&&Board){arrival.Rooms.SpendTurn(0,true);var st=Threat.State;if(st!=null&&st.Encounter){waitFailures++;Body.text="무언가가 원정대를 쫓고 있습니다.\n숨죽여도 지나가지 않습니다.";choosing=false;Refresh();return;}}
   else if(action==0){int chance=CurrentWaitChance;arrival.Rooms.SpendSearchTurn(-2);if(UnityEngine.Random.Range(0,100)>=chance){waitFailures++;Body.text="발소리가 가까운 곳에 멈췄습니다.\n아직 움직이기 어렵습니다.";choosing=false;Refresh();return;}}
   Cooldown=GraceSearches;Warned=false;risk.text="위험 · 경계";View.SetActive(false);Banner.SetActive(false);for(int i=0;i<hiddenNames.Length;i++)arrival.Main.transform.Find(hiddenNames[i]).gameObject.SetActive(hiddenStates[i]);arrival.Main.interactable=arrival.Main.blocksRaycasts=true;choosing=false;pendingLineup=null;
   if(action==0&&Threat&&!Board)Threat.OnHidden();
   if(action==1){if(arrival.Rooms.CurrentRoom==0){arrival.FinishReturn();return;}arrival.Rooms.AskMove();arrival.Rooms.ConfirmMove();return;}
   if(Threat)Threat.Refresh();if(site<0)return;
   if(arrival.Loot.State(site).Complete)arrival.Loot.Open(site);else arrival.Search.Open(site);
  }
  // Battle noise (gunshots plus the fight itself) carries into later encounter rolls.
  public void FinishBattle(bool retreat,int noise=2){
   if(!IsOpen)return;if(!retreat&&Threat)Threat.OnDriven();pendingLineup=null;Cooldown=GraceSearches;Warned=false;risk.text="위험 · 경계";View.SetActive(false);Banner.SetActive(false);
   for(int i=0;i<hiddenNames.Length;i++)arrival.Main.transform.Find(hiddenNames[i]).gameObject.SetActive(hiddenStates[i]);
   arrival.Main.interactable=arrival.Main.blocksRaycasts=true;choosing=false;
   // A retreat to the next room: the battle noise also reaches the site board (the result says '소음 +N'); leaving through the exit ends the visit instead.
   if(retreat){arrival.Rooms.AddNoise(noise);if(arrival.Rooms.CurrentRoom==0){arrival.FinishReturn();return;}if(Threat)Threat.HearBattle(noise);arrival.Rooms.AskMove();arrival.Rooms.ConfirmMove();return;}
   arrival.Rooms.SpendSearchTurn(noise);
   if(site<0)return;
   if(arrival.Loot.State(site).Complete)arrival.Loot.Open(site);else arrival.Search.Open(site);
  }
  public void Escape(){if(Battle&&Battle.IsOpen){Battle.Escape();return;}if(Review.activeSelf)CancelChoice();}
 }
}

