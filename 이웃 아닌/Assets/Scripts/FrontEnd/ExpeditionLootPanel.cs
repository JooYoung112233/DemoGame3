using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 public sealed partial class ExpeditionLootPanel:MonoBehaviour {
  [Serializable] public sealed class Drop {public string Id;public int Count=1;[Range(0,100)]public int Chance=70;}
  // Noise: this object's noise on every turn it is searched (0 silent … 3 loud: the resident remembers the room). Turns: base search turns
  // (0 = FieldTurnPlan.DefaultTurns). Data written by AgentScripts/BuildSiteNoise.cs; not saved (a running search keeps its stored Required).
  [Serializable] public sealed class Site {public Drop[] Drops;public string RequiredTool;public int Room;[Tooltip("수색하는 턴마다 나는 소음 (0 조용함 · 3 큰 소리: 그것이 그 방을 기억)")][Range(0,3)]public int Noise;[Tooltip("기본 수색 턴 (0 = 2턴) · 함께 수색이면 1턴 빨리 (최소 1턴)")][Range(0,3)]public int Turns;}
  public sealed class SearchState {public int Progress,Required,Pace,Bonus,Duty;public bool Complete,Opened;public readonly Dictionary<string,int> Loot=new Dictionary<string,int>();}
  public Site[] Sites;public GameObject View,LeaveReview;public CanvasGroup Workspace;
  [Range(0,100)]public int LightBonus=20;
  public RectTransform Members,FieldContent,BagContent;public ExpeditionMemberCard MemberPrefab;public InventorySlot SlotPrefab;
  public Text Title,BagTitle,Capacity,DetailTitle,Description,Quantity,Message,Empty,LeaveBody;
  public Image DetailIcon;public Button Back,Minus,Plus,Max,Transfer,LeaveCancel,LeaveConfirm;
  [Tooltip("현장 물건을 종류별로 통째로 담기: 이미 가진 대원 먼저, 없으면 빈칸이 있는 대원 (시간·소음 없음)")]public Button TakeAll;
  [Header("발견물 문구")]
  [TextArea(2,5)]public string LeaveText="아직 챙기지 않은 물건이 있습니다.\n이 사물에 남겨두고 돌아갈까요?\n\n남긴 물건은 이 자리에 남아 다음에도 챙길 수 있습니다.";
  [Tooltip("관리실 선반에서 나갈 때 LeaveText 마지막 줄 대신 표시")][TextArea(1,3)]public string DenLeaveNote="관리실 선반은 안이 비었을 때만 다시 열 수 있습니다.";
  [Tooltip("{0}: 대원별 담은 종류 수")][TextArea(1,3)]public string TakeAllDone="모두 담았습니다.\n{0}";
  [Tooltip("{0}: 남긴 종류 수")][TextArea(1,3)]public string TakeAllLeft="빈칸이 부족해 {0}종을 남겼습니다.\n남긴 물건은 이 자리에 그대로 남습니다.";
  [TextArea(1,3)]public string TakeAllNone="모든 가방에 빈칸이 없습니다.\n남은 물건은 이 자리에 둘 수 있습니다.";
  public readonly List<ExpeditionMemberCard> Cards=new List<ExpeditionMemberCard>();public readonly List<InventorySlot> FieldRows=new List<InventorySlot>(),BagRows=new List<InventorySlot>();
  readonly Dictionary<int,SearchState> states=new Dictionary<int,SearchState>();
  readonly string[] hiddenNames={"ArrivalPaper","ArrivalTitle","Status","Hint","Return","Members","TurnPaper","TurnLabel","RoutePaper","RouteLabel"};bool[] hiddenStates; ExpeditionArrivalPanel arrival;int site,member,quantity=1;string selected;bool fromBag;
  public bool IsOpen=>View.activeSelf;public Adventurer Current=>arrival.Participants[member];
  public SearchState State(int index){if(!states.TryGetValue(index,out var s)){s=new SearchState();states.Add(index,s);}return s;}
  // Read-only guidance: do not create search state or roll unknown rewards.
  public bool MaterialAvailability(string id,out int unfinished,out int remaining){
   unfinished=remaining=0;bool known=false;
   for(int i=0;i<Sites.Length;i++){
    bool candidate=Sites[i].Drops.Any(d=>d.Id==id&&d.Chance>0&&d.Count>0);known|=candidate;
    states.TryGetValue(i,out var s);
    if(s!=null&&s.Complete){if(s.Loot.TryGetValue(id,out int count))remaining+=Math.Max(0,count);}
    else if(candidate)unfinished++;
   }
   return known||remaining>0;
  }
  public void Initialize(ExpeditionArrivalPanel a){arrival=a;View.SetActive(false);LeaveReview.SetActive(false);Back.onClick.AddListener(AskClose);Minus.onClick.AddListener(()=>{quantity=Math.Max(1,quantity-1);Detail();});Plus.onClick.AddListener(()=>{quantity=Math.Min(Limit(),quantity+1);Detail();});Max.onClick.AddListener(()=>{quantity=Limit();Detail();});Transfer.onClick.AddListener(Move);if(TakeAll)TakeAll.onClick.AddListener(TakeAllItems);LeaveCancel.onClick.AddListener(Dismiss);LeaveConfirm.onClick.AddListener(Close);}
  public bool IsSiteInCurrentRoom(int index)=>index>=0&&index<Sites.Length&&Sites[index].Room>=0&&arrival.Rooms.CurrentRoom==Sites[index].Room;
  public bool CanSearch(int index,Adventurer worker){if(!IsSiteInCurrentRoom(index)||worker==null||worker.Health<=0||!arrival.Participants.Contains(worker))return false;if(arrival.Threat&&!arrival.Threat.CanSearchSite(index))return false;return State(index).Opened||string.IsNullOrEmpty(Sites[index].RequiredTool)||arrival.Inventory.CountFor(worker,Sites[index].RequiredTool)>0;}
  // pace: legacy (±15 only for a search started before 2026-09-25; new searches store 1). Use ChanceFor(drop,bonus) for new code.
  public static int ChanceFor(Drop drop,int bonus)=>ChanceFor(drop,1,bonus);
  public static int ChanceFor(Drop drop,int pace,int bonus)=>drop.Chance>=100?100:Mathf.Clamp(drop.Chance+(pace-1)*15+bonus,0,100);
  public Adventurer LightSupport(Adventurer worker)=>worker==null?null:arrival.Participants.FirstOrDefault(p=>p!=worker&&p.Health>0&&arrival.Inventory.CountFor(p,"flashlight")>0);
  // Whether a role can run now (2026-09-25): only 조명 needs its helper, a flashlight holder, also on a running search. 함께 and 망보기 are
  // optional as on the site board (FieldTurnPlan.Need): with no second living member the search goes on alone (a running 망보기 search at
  // the object's full noise, NoiseFor). Whether the 07 window offers 망보기 for a new search: CanOfferWatch.
  public bool CanSupport(int duty,Adventurer worker)=>duty==0||duty==1||duty==2&&LightSupport(worker)!=null;
  // First visit (the 07 window, the note's '수색 · 1턴'): the rules of FieldTurnPlan on the site board. 함께: a second living member, one turn
  // sooner; 망보기: a second living member on a noisy object, noise -1; 조명: a flashlight helper, +LightBonus. A running search keeps its own.
  public int AliveCount=>arrival.Participants.Count(p=>p.Health>0);
  public int SiteNoise(int index)=>index>=0&&index<Sites.Length?Mathf.Max(0,Sites[index].Noise):0;
  public int SiteTurns(int index)=>index>=0&&index<Sites.Length&&Sites[index].Turns>0?Sites[index].Turns:FieldTurnPlan.DefaultTurns;
  // 망보기 is offered only on a noisy object (otherwise '소음 없음').
  public bool CanWatch(int index)=>SiteNoise(index)>0;
  // A new 망보기 search: a noisy object and someone to watch (the 07 window's role button, the note's first-visit run).
  public bool CanOfferWatch(int index)=>CanWatch(index)&&AliveCount>1;
  public int RequiredFor(int index,int duty)=>State(index).Progress>0?State(index).Required:FieldTurnPlan.RequiredOf(SiteTurns(index),duty==0&&AliveCount>1);
  public int BonusFor(int index,int duty)=>State(index).Progress>0?State(index).Bonus:duty==2?LightBonus:0;
  // One search turn's noise on this object (2026-09-25: the first argument is the site index, no longer a pace).
  public int NoiseFor(int index,int duty)=>FieldTurnPlan.SearchNoise(SiteNoise(index),duty==1&&AliveCount>1);
  // pace: legacy, ignored (new searches store 1; one started under the old rules keeps its stored pace, turns and bonus).
  public bool Advance(int index,int pace,Adventurer worker,int duty=0){if(!arrival.IsOpen||arrival.InTransit||(arrival.Encounter&&arrival.Encounter.IsOpen)||!IsSiteInCurrentRoom(index)||worker==null||worker.Health<=0||!arrival.Participants.Contains(worker))return false;var s=State(index);if(s.Complete||!CanSearch(index,worker)||!CanSupport(s.Progress>0?s.Duty:duty,worker))return false;s.Opened=true;if(s.Progress==0){s.Pace=1;s.Duty=duty;s.Required=RequiredFor(index,duty);s.Bonus=BonusFor(index,duty);}s.Progress++;arrival.Rooms.SpendSearchTurn(NoiseFor(index,s.Duty));
   if(s.Progress>=s.Required){s.Complete=true;Roll(index,s);}return true;}
  void Roll(int index,SearchState s){foreach(var drop in Sites[index].Drops){int chance=ChanceFor(drop,s.Pace,s.Bonus);if(UnityEngine.Random.Range(0,100)<chance)s.Loot[drop.Id]=(s.Loot.TryGetValue(drop.Id,out int n)?n:0)+drop.Count;}}
  public void Open(int index){if(!arrival.IsOpen||arrival.InTransit||(arrival.Encounter&&arrival.Encounter.IsOpen)||IsOpen||!IsSiteInCurrentRoom(index)||!State(index).Complete)return;if(arrival.MissingPerson)arrival.MissingPerson.Discover(index);site=index;hiddenStates=hiddenNames.Select(n=>arrival.Main.transform.Find(n).gameObject.activeSelf).ToArray();foreach(var n in hiddenNames)arrival.Main.transform.Find(n).gameObject.SetActive(false);member=0;selected=null;fromBag=false;quantity=1;View.SetActive(true);LeaveReview.SetActive(false);Workspace.interactable=Workspace.blocksRaycasts=true;arrival.Main.interactable=arrival.Main.blocksRaycasts=false;Title.text=arrival.ObjectNames[site]+" · 발견한 물건";Rebuild();}
  static void Clear<T>(List<T> rows)where T:Component{foreach(var r in rows){r.gameObject.SetActive(false);UnityEngine.Object.Destroy(r.gameObject);}rows.Clear();}
  public void Rebuild(){var inv=arrival.Inventory;Clear(Cards);for(int i=0;i<arrival.Participants.Count;i++){int k=i;var p=arrival.Participants[i];var c=Instantiate(MemberPrefab,Members);c.Name.text=p.Name;c.Portrait.sprite=arrival.Cards[i].Portrait.sprite;c.Paper.color=member==i?new Color(1,.78f,.37f):Color.white;c.Button.interactable=p.Health>0;c.Button.onClick.AddListener(()=>{member=k;quantity=1;Rebuild();});Cards.Add(c);}
   Clear(FieldRows);Clear(BagRows);foreach(var item in inv.Items){if(State(site).Loot.TryGetValue(item.Id,out int n)&&n>0)Row(item,n,false,FieldContent,FieldRows);int owned=inv.CountFor(Current,item.Id);if(owned>0)Row(item,owned,true,BagContent,BagRows);}
   BagTitle.text=Current.Name+" · 개인 가방";Capacity.text=inv.SlotsFor(Current)+" / "+Current.BagCapacity;Empty.gameObject.SetActive(FieldRows.Count==0);Detail();arrival.RefreshFieldBags();}
  void Row(SettlementInventoryPanel.Item item,int count,bool bag,Transform parent,List<InventorySlot> rows){var row=Instantiate(SlotPrefab,parent);row.Bind(item.Icon,item.Name,count,item.Id==selected&&bag==fromBag);row.Button.onClick.AddListener(()=>{selected=item.Id;fromBag=bag;quantity=1;Rebuild();});rows.Add(row);}
  int Available(){if(selected==null)return 0;return fromBag?arrival.Inventory.CountFor(Current,selected):State(site).Loot.TryGetValue(selected,out int n)?n:0;}
  int Limit(){int n=Available();if(n<=0||Current.Health<=0)return 0;if(!fromBag&&arrival.Inventory.CountFor(Current,selected)==0&&arrival.Inventory.SlotsFor(Current)>=Current.BagCapacity)return 0;return n;}
  void Detail(){var item=arrival.Inventory.Items.FirstOrDefault(i=>i.Id==selected);DetailTitle.text=item?.Name??"물건 선택";Description.text=item?.Description??"현장이나 가방의 물건을\n눌러 확인하세요.";DetailIcon.sprite=item?.Icon;DetailIcon.enabled=item!=null;int limit=Limit();quantity=Mathf.Clamp(quantity,1,Math.Max(1,limit));Quantity.text=quantity.ToString();Minus.interactable=limit>0&&quantity>1;Plus.interactable=quantity<limit;Max.interactable=limit>0;Transfer.interactable=limit>0;Transfer.GetComponentInChildren<Text>().text=fromBag?"현장에 두기":"선택 가져오기";bool all=CanTakeAll();if(TakeAll)TakeAll.interactable=all;Message.text=item==null?(!all&&State(site).Loot.Values.Any(n=>n>0)?TakeAllNone:"대원별 가방에 나누어 담으세요."):limit==0&&Available()>0?"빈칸이 부족합니다.\n다른 대원을 선택하세요.":fromBag?"가방 → 현장 보관함":"현장 → "+Current.Name+" 가방";}
  void Move(){if(!IsOpen||LeaveReview.activeSelf||quantity<1||quantity>Limit())return;int count=quantity;if(!arrival.Inventory.TransferField(Current,selected,count,!fromBag))return;var loot=State(site).Loot;loot[selected]=(loot.TryGetValue(selected,out int n)?n:0)+(fromBag?count:-count);Rebuild();}
  // Take-all carrier for one kind, scanning from the selected member and wrapping: a holder first (no new slot), else the first free slot. Same rule as the tutorial's CanCarry.
  Adventurer Carrier(string id){var inv=arrival.Inventory;var people=arrival.Participants;Adventurer free=null;for(int k=0;k<people.Count;k++){var p=people[(member+k)%people.Count];if(p.Health<=0)continue;if(inv.CountFor(p,id)>0)return p;if(free==null&&inv.SlotsFor(p)<p.BagCapacity)free=p;}return free;}
  public bool CanTakeAll(){if(!IsOpen||!states.TryGetValue(site,out var s))return false;return arrival.Inventory.Items.Any(i=>s.Loot.TryGetValue(i.Id,out int n)&&n>0&&Carrier(i.Id)!=null);}
  // Whole stacks only, through the same TransferField as a single transfer (ammo adjusts the campaign count there); no time or noise, like Move.
  void TakeAllItems(){if(!IsOpen||LeaveReview.activeSelf)return;var inv=arrival.Inventory;var loot=State(site).Loot;var took=new Dictionary<Adventurer,int>();int left=0;
   foreach(var item in inv.Items){if(!loot.TryGetValue(item.Id,out int n)||n<=0)continue;var carrier=Carrier(item.Id);if(carrier==null||!inv.TransferField(carrier,item.Id,n,true)){left++;continue;}loot[item.Id]-=n;took[carrier]=(took.TryGetValue(carrier,out int k)?k:0)+1;}
   selected=null;fromBag=false;quantity=1;Rebuild();if(took.Count==0){if(left>0)Message.text=TakeAllNone;return;}
   var carriers=arrival.Participants.Where(took.ContainsKey).ToList();string who=carriers.Count<=2?string.Join(" · ",carriers.Select(p=>p.Name+" "+took[p]+"종")):"대원 "+carriers.Count+"명 · "+took.Values.Sum()+"종";
   Message.text=left>0?string.Format(TakeAllLeft,left):string.Format(TakeAllDone,who);}
  public void AskClose(){if(!IsOpen)return;if(State(site).Loot.Values.Any(n=>n>0)){string body=LeaveText;if(arrival.Threat&&site==arrival.Threat.DenSite){int cut=body.LastIndexOf('\n');body=(cut>=0?body.Substring(0,cut+1):"")+DenLeaveNote;}LeaveBody.text=body;LeaveReview.SetActive(true);Workspace.interactable=Workspace.blocksRaycasts=false;}else Close();}
  public void Dismiss(){LeaveReview.SetActive(false);Workspace.interactable=Workspace.blocksRaycasts=true;}
  void Close(){Dismiss();View.SetActive(false);if(hiddenStates!=null)for(int i=0;i<hiddenNames.Length;i++)arrival.Main.transform.Find(hiddenNames[i]).gameObject.SetActive(hiddenStates[i]);arrival.Main.interactable=arrival.Main.blocksRaycasts=true;arrival.RefreshFieldBags();}
  public void Escape(){if(LeaveReview.activeSelf)Dismiss();else AskClose();}
 }
}



