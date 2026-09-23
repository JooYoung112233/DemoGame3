using System;
using System.Linq;
using System.Collections.Generic;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 public sealed class ExpeditionBagPanel:MonoBehaviour {
  public GameObject View;public CanvasGroup Workspace;
  public RectTransform LeftMembers,RightMembers,LeftItems,RightItems;
  public ExpeditionMemberCard MemberPrefab;public InventorySlot SlotPrefab;
  public Text LeftTitle,RightTitle,LeftCapacity,RightCapacity,DetailTitle,Description,Quantity,Message,EmptyLeft,EmptyRight;
  public Image DetailIcon;public Button Back,Minus,Plus,Max,Transfer;
  public readonly List<ExpeditionMemberCard> LeftCards=new List<ExpeditionMemberCard>(),RightCards=new List<ExpeditionMemberCard>();
  public readonly List<InventorySlot> LeftRows=new List<InventorySlot>(),RightRows=new List<InventorySlot>();
  ExpeditionArrivalPanel arrival;int left,right,quantity=1;string selected;bool fromRight;
  public bool IsOpen=>View.activeSelf;
  public Adventurer Source=>Person(fromRight?right:left);public Adventurer Target=>Person(fromRight?left:right);
  Adventurer Person(int index)=>index>=0&&index<arrival.Participants.Count?arrival.Participants[index]:null;
  public void Initialize(ExpeditionArrivalPanel a){arrival=a;View.SetActive(false);Back.onClick.AddListener(Close);Minus.onClick.AddListener(()=>{quantity=Math.Max(1,quantity-1);Detail();});Plus.onClick.AddListener(()=>{quantity=Math.Min(Limit(),quantity+1);Detail();});Max.onClick.AddListener(()=>{quantity=Limit();Detail();});Transfer.onClick.AddListener(Move);}
  public void Open(int index){if(!arrival.IsOpen||arrival.InTransit||arrival.Popup.activeSelf||IsOpen||(arrival.Search&&arrival.Search.IsOpen)||(arrival.Loot&&arrival.Loot.IsOpen)||(arrival.Encounter&&arrival.Encounter.IsOpen))return;left=Mathf.Clamp(index,0,arrival.Participants.Count-1);right=arrival.Participants.Count>1?(left+1)%arrival.Participants.Count:-1;selected=null;quantity=1;fromRight=false;View.SetActive(true);arrival.Main.interactable=arrival.Main.blocksRaycasts=false;Refresh();}
  static void Clear<T>(List<T> list)where T:Component{foreach(var c in list){c.gameObject.SetActive(false);Destroy(c.gameObject);}list.Clear();}
  void Members(bool rhs){var list=rhs?RightCards:LeftCards;Clear(list);for(int i=0;i<arrival.Participants.Count;i++){int k=i;var p=Person(i);var c=Instantiate(MemberPrefab,rhs?RightMembers:LeftMembers);c.Name.text=p.Name;c.Portrait.sprite=arrival.Cards[i].Portrait.sprite;c.Paper.color=i==(rhs?right:left)?new Color(1,.78f,.37f):Color.white;c.Button.interactable=i!=(rhs?left:right);c.Button.onClick.AddListener(()=>{if(rhs)right=k;else left=k;selected=null;quantity=1;Refresh();});list.Add(c);}}
  void Items(bool rhs){var rows=rhs?RightRows:LeftRows;Clear(rows);var p=Person(rhs?right:left);if(p==null)return;foreach(var item in arrival.Inventory.Items){int count=arrival.Inventory.CountFor(p,item.Id);if(count<=0)continue;var data=item;var row=Instantiate(SlotPrefab,rhs?RightItems:LeftItems);row.Bind(item.Icon,item.Name,count,selected==item.Id&&fromRight==rhs);row.Button.onClick.AddListener(()=>{selected=data.Id;fromRight=rhs;quantity=1;Refresh();});rows.Add(row);}}
  public void Refresh(){Members(false);Members(true);Items(false);Items(true);LeftTitle.text=(Person(left)?.Name??"대원")+" · 가방";RightTitle.text=Person(right)!=null?Person(right).Name+" · 가방":"동행 대원 없음";LeftCapacity.text=Capacity(Person(left));RightCapacity.text=Capacity(Person(right));EmptyLeft.gameObject.SetActive(LeftRows.Count==0);EmptyRight.gameObject.SetActive(RightRows.Count==0);EmptyRight.text=Person(right)==null?"혼자 탐험 중입니다.":"가방이 비어 있습니다.";Detail();Canvas.ForceUpdateCanvases();foreach(var r in new[]{LeftMembers,RightMembers,LeftItems,RightItems})LayoutRebuilder.ForceRebuildLayoutImmediate(r);}
  string Capacity(Adventurer p)=>p==null?"":arrival.Inventory.SlotsFor(p)+" / "+p.BagCapacity;
  int Limit()=>selected==null?0:arrival.Inventory.FieldExchangeLimit(Source,Target,selected);
  void Detail(){var item=arrival.Inventory.Items.FirstOrDefault(i=>i.Id==selected);DetailTitle.text=item?.Name??"물건 선택";Description.text=item?.Description??"양쪽 가방에서 물건을 눌러\n동료에게 건넬 수 있습니다.";DetailIcon.sprite=item?.Icon;DetailIcon.enabled=item!=null;int limit=Limit();quantity=Mathf.Clamp(quantity,1,Math.Max(1,limit));Quantity.text=quantity.ToString();Minus.interactable=limit>0&&quantity>1;Plus.interactable=quantity<limit;Max.interactable=limit>0;Transfer.interactable=limit>0;Transfer.GetComponentInChildren<Text>().text=fromRight?"← 왼쪽 대원에게":"오른쪽 대원에게 →";
   Message.text=item==null?"창고는 거점에서 이용할 수 있습니다.":Target==null?"건넬 동행 대원이 없습니다.":Source.Health<=0||Target.Health<=0?"행동 가능한 대원끼리 건넬 수 있습니다.":limit==0?(arrival.Inventory.CountFor(Source,selected)==0?"선택한 물건을 모두 건넸습니다.":"받는 대원의 가방에 빈칸이 없습니다."):Source.Name+" → "+Target.Name+"\n건네기 · 시간과 소음 소모 없음";
  }
  void Move(){if(!IsOpen||selected==null||!arrival.Inventory.ExchangeField(Source,Target,selected,quantity)){Detail();return;}Refresh();}
  public void Close(){if(!IsOpen)return;View.SetActive(false);arrival.Main.interactable=arrival.Main.blocksRaycasts=true;arrival.RefreshFieldBags();}
 }
}
