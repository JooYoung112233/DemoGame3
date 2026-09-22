using System;
using System.Linq;
using System.Collections.Generic;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
namespace Demo5.FrontEnd {
 public sealed class ExpeditionPackingPanel:MonoBehaviour {
  public GameObject View,Review; public CanvasGroup Workspace;
  public Button Depart;
  public Button Back,Ready,ReviewBack,Minus,Plus,ToBag,ToStock; public Button[] Tabs;
  public Text Clock,BagTitle,Capacity,ItemName,Description,Available,Quantity,Checklist,Notice,Summary,Destination;
  public Image ItemIcon,CapacityFill; public RectTransform MemberContent,StockContent,BagContent;
  public ScrollRect MemberScroll,StockScroll,BagScroll; public InventorySlot SlotPrefab;public ExpeditionMemberCard MemberPrefab;
  public readonly List<InventorySlot> StockRows=new List<InventorySlot>(),BagRows=new List<InventorySlot>();
  public readonly List<ExpeditionMemberCard> Cards=new List<ExpeditionMemberCard>();
  public bool IsOpen=>View.activeSelf; public Adventurer Current=>people.Length>0?people[member]:null;
  SettlementController owner;SettlementInventoryPanel Inventory=>owner.InventoryPanel;
  Adventurer[] people=Array.Empty<Adventurer>();ExpeditionPlanPanel.Destination destination;Action onBack;
  int member,category,quantity=1;string selected;bool fromBag;
  public void Initialize(SettlementController c){owner=c;View.SetActive(false);Review.SetActive(false);Depart.onClick.AddListener(ConfirmDeparture);Back.onClick.AddListener(Close);Ready.onClick.AddListener(ShowReview);ReviewBack.onClick.AddListener(HideReview);Minus.onClick.AddListener(()=>{quantity=Math.Max(1,quantity-1);Detail();});Plus.onClick.AddListener(()=>{quantity=Math.Min(Limit(),quantity+1);Detail();});ToBag.onClick.AddListener(()=>Move(true));ToStock.onClick.AddListener(()=>Move(false));for(int i=0;i<Tabs.Length;i++){int k=i;Tabs[i].onClick.AddListener(()=>{category=k;Refresh();StockScroll.verticalNormalizedPosition=1;});}}
  public void Open(Adventurer[] participants,ExpeditionPlanPanel.Destination target,Action back){if(IsOpen||participants==null||participants.Length==0)return;people=participants.ToArray();destination=target;onBack=back;member=0;category=0;selected=null;owner.Main.gameObject.SetActive(false);View.SetActive(true);HideReview();Clock.text=owner.Clock.text;Destination.text=target.Name+"  ·  "+people.Length+"명";foreach(var c in Cards){c.gameObject.SetActive(false);Destroy(c.gameObject);}Cards.Clear();for(int i=0;i<people.Length;i++){int k=i;var card=Instantiate(MemberPrefab,MemberContent);card.Button.onClick.AddListener(()=>{member=k;selected=null;Refresh();BagScroll.verticalNormalizedPosition=1;});int original=Array.IndexOf(owner.Campaign.Party.ToArray(),people[i]);var data=owner.Roster.Candidates.FirstOrDefault(x=>x.Id==PartySelectionSession.Selected.ElementAtOrDefault(original));card.Portrait.sprite=data?.Portrait;Cards.Add(card);}Refresh();Canvas.ForceUpdateCanvases();MemberScroll.horizontalNormalizedPosition=0;StockScroll.verticalNormalizedPosition=BagScroll.verticalNormalizedPosition=1;}
  void Clear(List<InventorySlot> rows){foreach(var r in rows){r.gameObject.SetActive(false);Destroy(r.gameObject);}rows.Clear();}
  public void Refresh(){for(int i=0;i<Cards.Count;i++){var c=Cards[i];var p=people[i];c.Name.text=p.Name;c.Role.text="개인 가방";c.Health.text=Inventory.SlotsFor(p)+" / "+p.BagCapacity;c.HealthFill.fillAmount=(float)Inventory.SlotsFor(p)/Math.Max(1,p.BagCapacity);c.State.text=i==member?"짐 꾸리는 중":"가방 보기";c.Check.gameObject.SetActive(i==member);c.Paper.color=i==member?new Color(1,.8f,.43f):Color.white;}
   Clear(StockRows);Clear(BagRows);foreach(var item in Inventory.Items){if(Inventory.StockCount(item.Id)>0&&(category==0||category==item.Category))Slot(item,Inventory.StockCount(item.Id),false);if(Inventory.CountFor(Current,item.Id)>0)Slot(item,Inventory.CountFor(Current,item.Id),true);}for(int i=BagRows.Count;i<Current.BagCapacity;i++){var row=Instantiate(SlotPrefab,BagContent);row.Bind(null,"",0,false);row.Paper.color=new Color(.23f,.28f,.28f,.8f);row.Button.interactable=false;BagRows.Add(row);}for(int i=0;i<Tabs.Length;i++)Tabs[i].GetComponent<Image>().color=i==category?new Color(1,.8f,.43f):Color.white;
   BagTitle.text=Current.Name+"의 가방";Capacity.text=Inventory.SlotsFor(Current)+" / "+Current.BagCapacity;CapacityFill.fillAmount=(float)Inventory.SlotsFor(Current)/Math.Max(1,Current.BagCapacity);
   int supplies=people.Sum(p=>Inventory.CountFor(p,"supplies")),ammo=people.Sum(p=>Inventory.CountFor(p,"ammo")),free=people.Sum(p=>p.BagCapacity-Inventory.SlotsFor(p));Checklist.text="보급품     "+supplies+"개\n\n탄약        "+ammo+"개\n\n빈 공간     "+free;Notice.text=supplies==0?"보급품을 챙기지 않았습니다.":"출발 후에는 창고를 이용할 수 없습니다.";Ready.interactable=Valid();Detail();}
  void Slot(SettlementInventoryPanel.Item item,int count,bool bag){var row=Instantiate(SlotPrefab,bag?BagContent:StockContent);row.Bind(item.Icon,item.Name,count,selected==item.Id&&fromBag==bag);row.Button.onClick.AddListener(()=>{selected=item.Id;fromBag=bag;quantity=1;Refresh();});(bag?BagRows:StockRows).Add(row);}
  int Limit()=>selected==null?0:Inventory.TransferLimit(Current,selected,!fromBag);
  void Detail(){var item=Inventory.Items.FirstOrDefault(x=>x.Id==selected);ItemName.text=item?.Name??"물건 선택";Description.text=item?.Description??"창고나 가방에서\n물건을 선택하세요.";ItemIcon.sprite=item?.Icon;ItemIcon.enabled=item!=null;Available.text=item==null?"보유 수량  —":(fromBag?"가방 수량  ":"창고 수량  ")+(fromBag?Inventory.CountFor(Current,selected):Inventory.StockCount(selected));quantity=Mathf.Clamp(quantity,1,Math.Max(1,Limit()));Quantity.text=quantity.ToString();Minus.interactable=quantity>1;Plus.interactable=quantity<Limit();ToBag.interactable=item!=null&&!fromBag&&Limit()>0;ToStock.interactable=item!=null&&fromBag&&Limit()>0;if(item!=null&&!fromBag&&Limit()==0)Available.text="가방 공간 부족";}
  void Move(bool toBag){if(toBag==fromBag||!Inventory.MoveFor(Current,selected,quantity,toBag))return;quantity=1;Refresh();}
  bool Valid()=>owner.Campaign.Stage==JourneyStage.Settlement&&destination!=null&&destination.Accessible&&!destination.IsHome&&people.Length>0&&people.All(p=>p.Health>0&&!owner.IsAssigned(p)&&Inventory.SlotsFor(p)<=p.BagCapacity);
  void ShowReview(){if(!Valid()){Refresh();return;}Summary.text=destination.Name+" · 편도 "+destination.OneWayMinutes+"분\n\n"+string.Join("\n\n",people.Select(p=>p.Name+"  ("+Inventory.SlotsFor(p)+" / "+p.BagCapacity+")\n"+string.Join(" · ",Inventory.Items.Where(i=>Inventory.CountFor(p,i.Id)>0).Select(i=>i.Name+" "+Inventory.CountFor(p,i.Id)))))+"\n\n돌아가서 짐을 다시 조정할 수 있습니다.";Depart.interactable=Valid()&&destination.Id=="mall";Summary.text+="\n\n"+(destination.Id=="mall"?"출발 시 "+destination.OneWayMinutes+"분이 흐릅니다.":"이 장소로의 출발은 아직 준비되지 않았습니다.");Review.SetActive(true);Workspace.interactable=false;Workspace.blocksRaycasts=false;}
  void ConfirmDeparture(){if(!IsOpen||!Review.activeSelf||!Valid()||!owner.ArrivalPanel||destination.Id!="mall")return;Depart.interactable=false;if(!owner.ArrivalPanel.Begin(people,destination)){Depart.interactable=true;return;}onBack=null;HideReview();View.SetActive(false);}
  public void HideReview(){Review.SetActive(false);Workspace.interactable=true;Workspace.blocksRaycasts=true;}
  public void Close(){if(!IsOpen)return;HideReview();View.SetActive(false);owner.Main.gameObject.SetActive(true);var callback=onBack;onBack=null;callback?.Invoke();}
  void Update(){if(IsOpen&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){if(Review.activeSelf)HideReview();else Close();}}
 }
}
