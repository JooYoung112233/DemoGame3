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
  public Text ReviewDestination,DepartureNotice;
  [Header("준비 확인 문구")][Tooltip("{0} 탄약 · {1} 붕대 · {2} 빈 칸 (출발 대원 가방 합계)")][TextArea] public string ChecklistFormat="탄약        {0}개\n붕대        {1}개\n빈 칸       {2}";
  public string NoticeNoBandage="창고의 붕대를 챙기지 않았습니다.",NoticeNoAmmo="창고의 탄약을 챙기지 않았습니다.",NoticeDefault="필요한 물품을 모두 챙겼는지 다시 확인하세요.";
  // Approved mock 03: one row per kind with an icon and a gauge (packed in the departing bags / packed + left in stock).
  [Serializable] public sealed class CheckRow{public string Label;public string[] Items=new string[0];public Text Name;public RectTransform Fill;}
  [Header("짐 꾸리기 체크리스트 · 막대 (가방에 챙긴 양 / 챙긴 양 + 창고에 남은 양)")] public CheckRow[] CheckRows=new CheckRow[0];
  [Tooltip("빨간 알림 종이의 제목 (아래 줄은 위 알림 문구)")] public Text NoticeTitle;public string NoticeTitleText="출발 후 창고 이용 불가";
  public ScrollRect ReviewScroll;
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
   int ammo=people.Sum(p=>Inventory.CountFor(p,"ammo")),bandage=people.Sum(p=>Inventory.CountFor(p,"bandage")),free=people.Sum(p=>p.BagCapacity-Inventory.SlotsFor(p));Checklist.text=string.Format(ChecklistFormat,ammo,bandage,free);foreach(var row in CheckRows){if(row==null||!row.Fill)continue;int packed=people.Sum(p=>row.Items.Sum(id=>Inventory.CountFor(p,id))),stock=row.Items.Sum(id=>Inventory.StockCount(id));row.Fill.anchorMax=new Vector2(packed+stock==0?0:(float)packed/(packed+stock),1);if(row.Name)row.Name.text=row.Label;}if(NoticeTitle)NoticeTitle.text=NoticeTitleText;Notice.text=bandage==0&&Inventory.StockCount("bandage")>0?NoticeNoBandage:ammo==0&&Inventory.StockCount("ammo")>0?NoticeNoAmmo:NoticeDefault;Ready.interactable=Valid();Detail();}
  void Slot(SettlementInventoryPanel.Item item,int count,bool bag){var row=Instantiate(SlotPrefab,bag?BagContent:StockContent);row.Bind(item.Icon,item.Name,count,selected==item.Id&&fromBag==bag);row.Button.onClick.AddListener(()=>{selected=item.Id;fromBag=bag;quantity=1;Refresh();});(bag?BagRows:StockRows).Add(row);}
  int Limit()=>selected==null?0:Inventory.TransferLimit(Current,selected,!fromBag);
  void Detail(){var item=Inventory.Items.FirstOrDefault(x=>x.Id==selected);ItemName.text=item?.Name??"물건 선택";Description.text=item?.Description??"창고나 가방에서\n물건을 선택하세요.";ItemIcon.sprite=item?.Icon;ItemIcon.enabled=item!=null;Available.text=item==null?"보유 수량  —":(fromBag?"가방 수량  ":"창고 수량  ")+(fromBag?Inventory.CountFor(Current,selected):Inventory.StockCount(selected));quantity=Mathf.Clamp(quantity,1,Math.Max(1,Limit()));Quantity.text=quantity.ToString();Minus.interactable=quantity>1;Plus.interactable=quantity<Limit();ToBag.interactable=item!=null&&!fromBag&&Limit()>0;ToStock.interactable=item!=null&&fromBag&&Limit()>0;if(item!=null&&!fromBag&&Limit()==0)Available.text="가방 공간 부족";}
  void Move(bool toBag){if(toBag==fromBag||!Inventory.MoveFor(Current,selected,quantity,toBag))return;quantity=1;Refresh();}
  bool Valid()=>owner.Campaign.Stage==JourneyStage.Settlement&&destination!=null&&destination.Accessible&&!destination.IsHome&&people.Length>0&&people.All(p=>p.Health>0&&!owner.IsAssigned(p)&&Inventory.SlotsFor(p)<=p.BagCapacity);
  void ShowReview(){
   if(!Valid()){Refresh();return;}
   if(ReviewDestination)ReviewDestination.text=destination.Name+" · 동행 "+people.Length+"명 · 편도 "+destination.OneWayMinutes+"분";
   Summary.text=string.Join("\n\n",people.Select(p=>{
    var items=Inventory.Items.Where(i=>Inventory.CountFor(p,i.Id)>0).Select(i=>i.Name+" "+Inventory.CountFor(p,i.Id)).ToArray();
    return p.Name+"  ·  체력 "+p.Health+" / "+p.MaxHealth+"  ·  가방 "+Inventory.SlotsFor(p)+" / "+p.BagCapacity+"\n<color=#4D5145>"+(items.Length==0?"빈 가방 · 현장에서 물건을 담을 수 있습니다.":string.Join(" · ",items))+"</color>";
   }));
   Depart.interactable=Valid()&&destination.Id=="mall";
   if(DepartureNotice)DepartureNotice.text=destination.Id=="mall"?"출발하면 "+destination.OneWayMinutes+"분 경과 · 짐은 돌아가서 조정할 수 있습니다.":"이 장소로의 출발은 아직 준비되지 않았습니다.";
   Review.SetActive(true);Workspace.interactable=false;Workspace.blocksRaycasts=false;
   Canvas.ForceUpdateCanvases();if(ReviewScroll){ReviewScroll.StopMovement();ReviewScroll.verticalNormalizedPosition=1;}
  }
  void ConfirmDeparture(){if(!IsOpen||!Review.activeSelf||!Valid()||!owner.ArrivalPanel||destination.Id!="mall")return;Depart.interactable=false;if(!owner.ArrivalPanel.Begin(people,destination)){Depart.interactable=true;return;}onBack=null;HideReview();View.SetActive(false);}
  public void HideReview(){Review.SetActive(false);Workspace.interactable=true;Workspace.blocksRaycasts=true;}
  public void Close(){if(!IsOpen)return;HideReview();View.SetActive(false);owner.Main.gameObject.SetActive(true);var callback=onBack;onBack=null;callback?.Invoke();}
  void Update(){if(IsOpen&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){if(Review.activeSelf)HideReview();else Close();}}
 }
}
