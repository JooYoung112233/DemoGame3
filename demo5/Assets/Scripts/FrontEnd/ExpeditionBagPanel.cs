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
  public ExpeditionMemberCard MemberPrefab,RecipientPrefab;public InventorySlot SlotPrefab;
  public Text LeftTitle,RightTitle,LeftCapacity,RightCapacity,DetailTitle,Description,Quantity,Message,EmptyLeft,EmptyRight,UseHint;
  public PartyRoster Roster;public Text ProfileName,ProfileRole,ProfileHealth,ProfileState,ProfileTrait,ProfileCharacteristics;public Image ProfilePortrait,ProfileHealthBar;
  public Image DetailIcon;public Button Back,Minus,Plus,Max,Transfer,Use;
  public readonly List<ExpeditionMemberCard> LeftCards=new List<ExpeditionMemberCard>(),RightCards=new List<ExpeditionMemberCard>();
  public readonly List<InventorySlot> LeftRows=new List<InventorySlot>(),RightRows=new List<InventorySlot>();
  ExpeditionArrivalPanel arrival;int left,right=-1,quantity=1;string selected;
  public bool IsOpen=>View.activeSelf;
  public Adventurer Source=>Person(left);public Adventurer Target=>Person(right);
  Adventurer Person(int index)=>index>=0&&index<arrival.Participants.Count?arrival.Participants[index]:null;
  public void Initialize(ExpeditionArrivalPanel a){arrival=a;View.SetActive(false);Back.onClick.AddListener(Close);
   Minus.onClick.AddListener(()=>{quantity=Math.Max(1,quantity-1);Detail();});Plus.onClick.AddListener(()=>{quantity=Math.Min(Held(),quantity+1);Detail();});Max.onClick.AddListener(()=>{quantity=Math.Max(1,Held());Detail();});
   Use.onClick.AddListener(()=>{if(IsOpen&&arrival.Inventory.UseFieldItem(Source,selected)){Refresh();Message.text=Source.Name+"이(가) 물품을 사용했습니다.";}else Detail();});
  }
  public void Open(int index){if(!arrival.IsOpen||arrival.InTransit||arrival.Popup.activeSelf||IsOpen||(arrival.Search&&arrival.Search.IsOpen)||(arrival.Loot&&arrival.Loot.IsOpen)||(arrival.Encounter&&arrival.Encounter.IsOpen))return;
   left=Mathf.Clamp(index,0,arrival.Participants.Count-1);right=-1;selected=null;quantity=1;View.SetActive(true);arrival.Main.interactable=arrival.Main.blocksRaycasts=false;Refresh();
   var tabs=LeftMembers.GetComponentInParent<ScrollRect>();Canvas.ForceUpdateCanvases();if(tabs)tabs.horizontalNormalizedPosition=arrival.Participants.Count<=1?0:(float)left/(arrival.Participants.Count-1);
  }
  static void Clear<T>(List<T> list)where T:Component{foreach(var c in list){c.gameObject.SetActive(false);Destroy(c.gameObject);}list.Clear();}
  void Tabs(){Clear(LeftCards);for(int i=0;i<arrival.Participants.Count;i++){int k=i;var p=Person(i);var c=Instantiate(MemberPrefab,LeftMembers);c.Name.text=p.Name;c.Portrait.sprite=arrival.Cards[i].Portrait.sprite;c.Paper.color=i==left?new Color(1,.78f,.37f):Color.white;c.Button.interactable=true;c.Button.onClick.AddListener(()=>{left=k;right=-1;selected=null;quantity=1;Refresh();});LeftCards.Add(c);}}
  void Recipients(){Clear(RightCards);for(int i=0;i<arrival.Participants.Count;i++){if(i==left)continue;int k=i;var p=Person(i);var c=Instantiate(RecipientPrefab,RightMembers);int limit=selected==null?0:arrival.Inventory.FieldExchangeLimit(Source,p,selected);c.Name.text=p.Name;c.Portrait.sprite=arrival.Cards[i].Portrait.sprite;c.Button.interactable=limit>=quantity&&selected!=null;
   c.State.text=selected==null?"물품을 먼저 선택":Source.Health<=0||p.Health<=0?"행동 불능":limit>=quantity?quantity+"개 전달":limit==0?"공간 또는 물품 부족":"수량을 줄여주세요";
   c.Button.onClick.AddListener(()=>Send(k));RightCards.Add(c);}
   EmptyRight.gameObject.SetActive(RightCards.Count==0);EmptyRight.text="함께 온 동료가 없습니다.";
  }
  void Items(){Clear(LeftRows);foreach(var item in arrival.Inventory.Items){int count=arrival.Inventory.CountFor(Source,item.Id);if(count<=0)continue;var data=item;var row=Instantiate(SlotPrefab,LeftItems);row.Bind(item.Icon,item.Name,count,selected==item.Id);row.Button.onClick.AddListener(()=>{selected=data.Id;quantity=1;Refresh();});LeftRows.Add(row);}}
  void Profile(){var p=Source;var data=Roster?Roster.Candidates.FirstOrDefault(c=>c.DisplayName==p.Name):null;ProfileName.text=p.Name;ProfileRole.text=data?.RoleTitle??p.Role;ProfilePortrait.sprite=arrival.Cards[left].Portrait.sprite;ProfileHealth.text="체력  "+p.Health+" / "+p.MaxHealth;ProfileState.text=p.Health<=0?"행동 불능":p.Health==1?"위험 · 회복 필요":p.Health<p.MaxHealth?"부상 · 행동 가능":"양호 · 행동 가능";SegmentedHealthGraphic.Set(ProfileHealthBar,p.Health,p.MaxHealth);ProfileTrait.text=(data?.TraitTitle??p.Role)+"\n"+(data?.TraitDescription??p.Description);ProfileCharacteristics.text=data?.Characteristics??"";}
  public void Refresh(){Tabs();Items();Profile();LeftTitle.text=Source.Name+" · 가방";LeftCapacity.text=arrival.Inventory.SlotsFor(Source)+" / "+Source.BagCapacity+"칸";EmptyLeft.gameObject.SetActive(LeftRows.Count==0);Detail();Canvas.ForceUpdateCanvases();foreach(var r in new[]{LeftMembers,RightMembers,LeftItems})LayoutRebuilder.ForceRebuildLayoutImmediate(r);}
  int Held()=>selected==null?0:arrival.Inventory.CountFor(Source,selected);
  void Detail(){var item=arrival.Inventory.Items.FirstOrDefault(i=>i.Id==selected);DetailTitle.text=item?.Name??"물품 선택";Description.text=item?.Description??"가방에서 물품을 선택하세요.\n동료 이름을 누르면 바로 전달합니다.";DetailIcon.sprite=item?.Icon;DetailIcon.enabled=item!=null;
   int held=Held();quantity=Mathf.Clamp(quantity,1,Math.Max(1,held));Quantity.text=quantity.ToString();Minus.interactable=quantity>1;Plus.interactable=quantity<held;Max.interactable=held>0;
   Use.gameObject.SetActive(item!=null&&item.FieldUsable);Use.interactable=IsOpen&&arrival.Inventory.FieldUseBlock(Source,selected)==null;
   Use.GetComponentInChildren<Text>().text=item!=null&&item.FieldUsable?Source.Name+" · "+item.UseVerb:"";
   UseHint.text=item==null?"":!item.FieldUsable?"직접 사용하는 물품이 아닙니다. 동료에게 전달할 수 있습니다.":Source.Name+" · 체력 "+Source.Health+" → "+Math.Min(Source.MaxHealth,Source.Health+item.Recovery)+"  |  "+item.UseCost+"개 · "+arrival.Rooms.MinutesPerTurn+"분\n"+(arrival.Inventory.FieldUseBlock(Source,selected)??"선택한 가방 주인이 사용합니다. 소음 없음");
   Message.text=selected==null?"대원 탭으로 가방을 바꾸고, 전달할 물품을 선택하세요.":"받을 동료의 이름을 누르면 "+quantity+"개 전달 · 시간과 소음 소모 없음";
   Recipients();Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(RightMembers);
  }
  void Send(int index){if(!IsOpen||selected==null)return;right=index;var item=arrival.Inventory.Items.First(i=>i.Id==selected);int sent=quantity;string sender=Source.Name,recipient=Target.Name;if(!arrival.Inventory.ExchangeField(Source,Target,selected,sent)){Detail();return;}Refresh();Message.text=sender+" → "+recipient+" · "+item.Name+" "+sent+"개 전달 완료";}
  public void Close(){if(!IsOpen)return;View.SetActive(false);arrival.Main.interactable=arrival.Main.blocksRaycasts=true;arrival.RefreshFieldBags();}
 }
}
