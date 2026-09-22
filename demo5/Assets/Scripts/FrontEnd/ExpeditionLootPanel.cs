using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 public sealed class ExpeditionLootPanel:MonoBehaviour {
  [Serializable] public sealed class Drop {public string Id;public int Count=1;[Range(0,100)]public int Chance=70;}
  [Serializable] public sealed class Site {public Drop[] Drops;}
  public sealed class SearchState {public int Progress,Required,Pace,Bonus,Duty;public bool Complete;public readonly Dictionary<string,int> Loot=new Dictionary<string,int>();}
  public Site[] Sites;public GameObject View,LeaveReview;public CanvasGroup Workspace;
  public RectTransform Members,FieldContent,BagContent;public ExpeditionMemberCard MemberPrefab;public InventorySlot SlotPrefab;
  public Text Title,BagTitle,Capacity,DetailTitle,Description,Quantity,Message,Empty,LeaveBody;
  public Image DetailIcon;public Button Back,Minus,Plus,Max,Transfer,LeaveCancel,LeaveConfirm;
  public readonly List<ExpeditionMemberCard> Cards=new List<ExpeditionMemberCard>();public readonly List<InventorySlot> FieldRows=new List<InventorySlot>(),BagRows=new List<InventorySlot>();
  readonly Dictionary<int,SearchState> states=new Dictionary<int,SearchState>();
  readonly string[] hiddenNames={"ArrivalPaper","ArrivalTitle","Status","Hint","Return","Members","TurnPaper","TurnLabel","RoutePaper","RouteLabel"};bool[] hiddenStates; ExpeditionArrivalPanel arrival;int site,member,quantity=1;string selected;bool fromBag;
  public bool IsOpen=>View.activeSelf;public Adventurer Current=>arrival.Participants[member];
  public SearchState State(int index){if(!states.TryGetValue(index,out var s)){s=new SearchState();states.Add(index,s);}return s;}
  public void Initialize(ExpeditionArrivalPanel a){arrival=a;View.SetActive(false);LeaveReview.SetActive(false);Back.onClick.AddListener(AskClose);Minus.onClick.AddListener(()=>{quantity=Math.Max(1,quantity-1);Detail();});Plus.onClick.AddListener(()=>{quantity=Math.Min(Limit(),quantity+1);Detail();});Max.onClick.AddListener(()=>{quantity=Limit();Detail();});Transfer.onClick.AddListener(Move);LeaveCancel.onClick.AddListener(Dismiss);LeaveConfirm.onClick.AddListener(Close);}
  public bool Advance(int index,int pace,Adventurer worker,int duty=0){if(!arrival.IsOpen||arrival.InTransit||(arrival.Encounter&&arrival.Encounter.IsOpen)||arrival.Rooms.CurrentRoom!=0||index<0||index>=Sites.Length||pace<0||pace>2||worker==null||worker.Health<=0||!arrival.Participants.Contains(worker))return false;var s=State(index);if(s.Complete)return false;if(s.Progress==0){s.Pace=pace;s.Duty=duty;s.Required=pace+1;s.Bonus=duty==0&&arrival.Participants.Count(p=>p.Health>0)>1?10:0;}s.Progress++;arrival.Rooms.SpendSearchTurn(Math.Max(0,3-s.Pace-(s.Duty==1&&arrival.Participants.Count(p=>p.Health>0)>1?1:0)));
   if(s.Progress>=s.Required){s.Complete=true;foreach(var drop in Sites[index].Drops){int chance=Mathf.Clamp(drop.Chance+(s.Pace-1)*15+s.Bonus,0,100);if(UnityEngine.Random.Range(0,100)<chance)s.Loot[drop.Id]=(s.Loot.TryGetValue(drop.Id,out int n)?n:0)+drop.Count;}}return true;}
  public void Open(int index){if(!arrival.IsOpen||arrival.InTransit||(arrival.Encounter&&arrival.Encounter.IsOpen)||IsOpen||!State(index).Complete)return;site=index;hiddenStates=hiddenNames.Select(n=>arrival.Main.transform.Find(n).gameObject.activeSelf).ToArray();foreach(var n in hiddenNames)arrival.Main.transform.Find(n).gameObject.SetActive(false);member=0;selected=null;fromBag=false;quantity=1;View.SetActive(true);LeaveReview.SetActive(false);Workspace.interactable=Workspace.blocksRaycasts=true;arrival.Main.interactable=arrival.Main.blocksRaycasts=false;Title.text=arrival.ObjectNames[site]+" · 발견한 물건";Rebuild();}
  static void Clear<T>(List<T> rows)where T:Component{foreach(var r in rows){r.gameObject.SetActive(false);UnityEngine.Object.Destroy(r.gameObject);}rows.Clear();}
  public void Rebuild(){var inv=arrival.Inventory;Clear(Cards);for(int i=0;i<arrival.Participants.Count;i++){int k=i;var p=arrival.Participants[i];var c=Instantiate(MemberPrefab,Members);c.Name.text=p.Name;c.Portrait.sprite=arrival.Cards[i].Portrait.sprite;c.Paper.color=member==i?new Color(1,.78f,.37f):Color.white;c.Button.interactable=p.Health>0;c.Button.onClick.AddListener(()=>{member=k;quantity=1;Rebuild();});Cards.Add(c);}
   Clear(FieldRows);Clear(BagRows);foreach(var item in inv.Items){if(State(site).Loot.TryGetValue(item.Id,out int n)&&n>0)Row(item,n,false,FieldContent,FieldRows);int owned=inv.CountFor(Current,item.Id);if(owned>0)Row(item,owned,true,BagContent,BagRows);}
   BagTitle.text=Current.Name+" · 개인 가방";Capacity.text=inv.SlotsFor(Current)+" / "+Current.BagCapacity;Empty.gameObject.SetActive(FieldRows.Count==0);Detail();arrival.RefreshFieldBags();}
  void Row(SettlementInventoryPanel.Item item,int count,bool bag,Transform parent,List<InventorySlot> rows){var row=Instantiate(SlotPrefab,parent);row.Bind(item.Icon,item.Name,count,item.Id==selected&&bag==fromBag);row.Button.onClick.AddListener(()=>{selected=item.Id;fromBag=bag;quantity=1;Rebuild();});rows.Add(row);}
  int Available(){if(selected==null)return 0;return fromBag?arrival.Inventory.CountFor(Current,selected):State(site).Loot.TryGetValue(selected,out int n)?n:0;}
  int Limit(){int n=Available();if(n<=0||Current.Health<=0)return 0;if(!fromBag&&arrival.Inventory.CountFor(Current,selected)==0&&arrival.Inventory.SlotsFor(Current)>=Current.BagCapacity)return 0;return n;}
  void Detail(){var item=arrival.Inventory.Items.FirstOrDefault(i=>i.Id==selected);DetailTitle.text=item?.Name??"물건 선택";Description.text=item?.Description??"현장이나 가방의 물건을\n눌러 확인하세요.";DetailIcon.sprite=item?.Icon;DetailIcon.enabled=item!=null;int limit=Limit();quantity=Mathf.Clamp(quantity,1,Math.Max(1,limit));Quantity.text=quantity.ToString();Minus.interactable=limit>0&&quantity>1;Plus.interactable=quantity<limit;Max.interactable=limit>0;Transfer.interactable=limit>0;Transfer.GetComponentInChildren<Text>().text=fromBag?"현장에 두기":"선택 가져오기";Message.text=item==null?"대원별 가방에 나누어 담으세요.":limit==0&&Available()>0?"빈칸이 부족합니다.\n다른 대원을 선택하세요.":fromBag?"가방 → 현장 보관함":"현장 → "+Current.Name+" 가방";}
  void Move(){if(!IsOpen||LeaveReview.activeSelf||quantity<1||quantity>Limit())return;int count=quantity;if(!arrival.Inventory.TransferField(Current,selected,count,!fromBag))return;var loot=State(site).Loot;loot[selected]=(loot.TryGetValue(selected,out int n)?n:0)+(fromBag?count:-count);Rebuild();}
  public void AskClose(){if(!IsOpen)return;if(State(site).Loot.Values.Any(n=>n>0)){LeaveBody.text="아직 챙기지 않은 물건이 있습니다.\n이 사물에 남겨두고 돌아갈까요?\n\n이번 플레이 중 다시 찾아올 수 있습니다.";LeaveReview.SetActive(true);Workspace.interactable=Workspace.blocksRaycasts=false;}else Close();}
  public void Dismiss(){LeaveReview.SetActive(false);Workspace.interactable=Workspace.blocksRaycasts=true;}
  void Close(){Dismiss();View.SetActive(false);if(hiddenStates!=null)for(int i=0;i<hiddenNames.Length;i++)arrival.Main.transform.Find(hiddenNames[i]).gameObject.SetActive(hiddenStates[i]);arrival.Main.interactable=arrival.Main.blocksRaycasts=true;arrival.RefreshFieldBags();}
  public void Escape(){if(LeaveReview.activeSelf)Dismiss();else AskClose();}
 }
}



