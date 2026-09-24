using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace Demo5.FrontEnd {
 public sealed partial class SettlementInventoryPanel:MonoBehaviour {
  [Serializable] public sealed class Item { public string Id,Name; public string UseVerb="응급 처치";public bool FieldUsable; [TextArea]public string Description; public Sprite Icon; public int Category; [Min(0)] public int Recovery; [Min(1)] public int UseCost=1,UseMinutes=10; }
  public Item[] Items;
  public GameObject View,QuantityPopup; public GameObject[] HideWhileOpen;
  public CanvasGroup Workspace;
  public Text HeaderClock,HeaderLocation;
  public Button Use;
  public Button CloseButton,ToBag,ToStock,Move,Minus,Plus,Max,Confirm,Cancel,PrevMember,NextMember;
  public Button[] Tabs; public InventorySlot[] MemberCards;
  public RectTransform StockContent,BagContent; public ScrollRect StockScroll,BagScroll;
  public InventorySlot SlotPrefab; public Text BagTitle,Capacity,DetailTitle,Description,Location,Notice,QuantityTitle,QuantityValue,QuantityHint,EmptyStock; public Image DetailIcon;
  public IReadOnlyList<InventorySlot> StockRows=>stockRows; public IReadOnlyList<InventorySlot> BagRows=>bagRows;
  readonly List<InventorySlot> stockRows=new List<InventorySlot>(),bagRows=new List<InventorySlot>();
  readonly Dictionary<Adventurer,Dictionary<string,int>> bags=new Dictionary<Adventurer,Dictionary<string,int>>();
  SettlementController owner; Adventurer[] people=Array.Empty<Adventurer>(); bool[] hidden; GameObject returnFocus;
  Adventurer[] expeditionPeople; Action returnToPlan;
  int member,category,page,quantity=1; string selected; bool fromBag;
  public bool IsOpen=>View&&View.activeSelf; public int MemberIndex=>member;
  public string SelectedItemId=>selected;
  public bool SelectedFromBag=>fromBag;
  public Adventurer SelectedMember=>member>=0&&member<people.Length?people[member]:null;
  public void Initialize(SettlementController c){owner=c;if(Use)Use.onClick.AddListener(()=>UseSelected());View.SetActive(false);QuantityPopup.SetActive(false);CloseButton.onClick.AddListener(Close);ToBag.onClick.AddListener(()=>{if(!fromBag)AskMove();});ToStock.onClick.AddListener(()=>{if(fromBag)AskMove();});Move.onClick.AddListener(AskMove);Minus.onClick.AddListener(()=>{quantity=Math.Max(1,quantity-1);RefreshQuantity();});Plus.onClick.AddListener(()=>{quantity=Math.Min(Maximum(),quantity+1);RefreshQuantity();});Max.onClick.AddListener(()=>{quantity=Maximum();RefreshQuantity();});Confirm.onClick.AddListener(Transfer);Cancel.onClick.AddListener(Dismiss);PrevMember.onClick.AddListener(()=>{page--;RefreshMembers();});NextMember.onClick.AddListener(()=>{page++;RefreshMembers();});for(int i=0;i<Tabs.Length;i++){int index=i;Tabs[i].onClick.AddListener(()=>{category=index;selected=null;Refresh();Reset(StockScroll);});}}
  Dictionary<string,int> Bag(Adventurer p){if(!bags.TryGetValue(p,out var bag)){bag=new Dictionary<string,int>();bags.Add(p,bag);}return bag;}
  public int BagCount(int index,string id)=>index>=0&&index<people.Length&&Bag(people[index]).TryGetValue(id,out int n)?n:0;
  public int CountFor(Adventurer person,string id)=>Bag(person).TryGetValue(id,out int n)?n:0;
  public int SlotsFor(Adventurer person)=>Bag(person).Count(x=>x.Value>0);
  public int TransferLimit(Adventurer person,string id,bool toBag){if(owner.Campaign.Stage!=JourneyStage.Settlement||person==null||!owner.Campaign.Party.Contains(person)||!Items.Any(x=>x.Id==id))return 0;if(!toBag)return owner.Development&&!CampaignCounted(id)?Math.Min(CountFor(person,id),owner.Development.FreeSpace):CountFor(person,id);return CountFor(person,id)==0&&SlotsFor(person)>=person.BagCapacity?0:StockCount(id);}
  public bool MoveFor(Adventurer person,string id,int count,bool toBag){if(count<1||count>TransferLimit(person,id,toBag))return false;if(!CampaignCounted(id)){var material=owner.CraftPanel.Materials.FirstOrDefault(x=>x.Id==id);if(material==null)return false;material.Initial+=toBag?-count:count;}Bag(person)[id]=CountFor(person,id)+(toBag?count:-count);return true;}
  public bool TransferField(Adventurer person,string id,int count,bool take){if(!owner.Campaign.IsFieldExpedition||!owner.ArrivalPanel.Participants.Contains(person)||person.Health<=0||count<=0||!Items.Any(i=>i.Id==id))return false;if(take&&CountFor(person,id)==0&&SlotsFor(person)>=person.BagCapacity)return false;if(!take&&CountFor(person,id)<count)return false;if(CampaignCounted(id)&&!owner.Campaign.AdjustFieldResource(id,take?count:-count))return false;Bag(person)[id]=CountFor(person,id)+(take?count:-count);return true;}
  int Allocated(string id)=>bags.Values.Sum(b=>b.TryGetValue(id,out int n)?n:0);
  public int FieldExchangeLimit(Adventurer source,Adventurer target,string id){
   var a=owner.ArrivalPanel;
   if(!owner.Campaign.IsFieldExpedition||!a.IsOpen||a.InTransit||a.Popup.activeSelf||(a.Encounter&&a.Encounter.IsOpen)||(a.Search&&a.Search.IsOpen)||(a.Loot&&a.Loot.IsOpen)||source==null||target==null||source==target||source.Health<=0||target.Health<=0||!a.Participants.Contains(source)||!a.Participants.Contains(target)||!Items.Any(i=>i.Id==id))return 0;
   return CountFor(target,id)==0&&SlotsFor(target)>=target.BagCapacity?0:CountFor(source,id);
  }
  public bool ExchangeField(Adventurer source,Adventurer target,string id,int count){
   if(count<1||count>FieldExchangeLimit(source,target,id))return false;
   // Moving between bags does not change campaign totals, stock, time or noise.
   int held=CountFor(source,id),received=CountFor(target,id);if(received>int.MaxValue-count)return false;
   Bag(source)[id]=held-count;Bag(target)[id]=received+count;owner.ArrivalPanel.RefreshFieldBags();return true;
  }
  // Ammo is a campaign counter (Campaign.Ammo), not a craft material; the supplies counter was retired (2026-09-25, save v14).
  static bool CampaignCounted(string id)=>id=="ammo";
  public int StockCount(string id){if(id=="ammo")return Math.Max(0,(owner.Campaign?.Ammo??0)-Allocated(id));return owner.CraftPanel?owner.CraftPanel.Available(id):0;}
  public int UsedSlots(int index)=>Bag(people[index]).Count(k=>k.Value>0);
  public void Open(int index=0){if(owner.Introduction&&!owner.Introduction.Allows(4))return;if(owner.Campaign?.Stage!=JourneyStage.Settlement||IsOpen||owner.IsPopupOpen||(owner.WorkPanel&&owner.WorkPanel.IsOpen)||(owner.CraftPanel&&owner.CraftPanel.IsOpen))return;people=expeditionPeople??owner.Campaign?.Party.ToArray()??Array.Empty<Adventurer>();member=Mathf.Clamp(index,0,Math.Max(0,people.Length-1));page=member/MemberCards.Length;category=0;selected=null;if(HeaderClock)HeaderClock.text=owner.Clock.text;if(HeaderLocation)HeaderLocation.text=owner.Location.text;returnFocus=EventSystem.current?.currentSelectedGameObject;hidden=HideWhileOpen.Select(g=>g.activeSelf).ToArray();foreach(var g in HideWhileOpen)g.SetActive(false);owner.Main.interactable=false;owner.Main.blocksRaycasts=false;View.SetActive(true);Workspace.interactable=true;Workspace.blocksRaycasts=true;Notice.text=owner.Development?owner.Development.StockSummary:"거점에서만 창고와 이동 가능.";Refresh();Reset(StockScroll);Reset(BagScroll);EventSystem.current?.SetSelectedGameObject(CloseButton.gameObject);}
  public void OpenForExpedition(Adventurer[] participants,string destination,Action onReturn){if(IsOpen)return;expeditionPeople=participants;returnToPlan=onReturn;Open();if(!IsOpen){expeditionPeople=null;returnToPlan=null;return;}Notice.text=destination+" · 원정대 짐 확인";CloseButton.transform.Find("Label").GetComponent<Text>().text="원정 계획으로";}
  static void Reset(ScrollRect s){Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(s.content);s.StopMovement();s.verticalNormalizedPosition=1;}
  static void Clear(List<InventorySlot> rows){foreach(var r in rows){r.gameObject.SetActive(false);Destroy(r.gameObject);}rows.Clear();}
  public void Refresh(){if(owner.Development)Notice.text=owner.Development.StockSummary+" · 초과 물품은 가방에 보관";RefreshMembers();Clear(stockRows);Clear(bagRows);for(int i=0;i<Tabs.Length;i++)Tabs[i].GetComponent<Image>().color=i==category?new Color(1,.78f,.37f):new Color(.7f,.71f,.66f);
   foreach(var item in Items.Where(i=>(category==0||i.Category==category)&&StockCount(i.Id)>0))AddSlot(item,StockCount(item.Id),false,StockContent,stockRows);
   int cap=people.Length>0?people[member].BagCapacity:0;var held=Items.Where(i=>BagCount(member,i.Id)>0).ToArray();foreach(var item in held)AddSlot(item,BagCount(member,item.Id),true,BagContent,bagRows);
   for(int i=held.Length;i<cap;i++){var slot=Instantiate(SlotPrefab,BagContent);slot.Bind(null,"",0,false);slot.Paper.color=new Color(.25f,.29f,.29f,.7f);slot.Button.interactable=false;bagRows.Add(slot);}
   if(EmptyStock)EmptyStock.gameObject.SetActive(stockRows.Count==0);BagTitle.text=people.Length>0?people[member].Name+" · 개인 가방":"개인 가방";Capacity.text=held.Length+" / "+cap;RefreshDetail();
  }
  void AddSlot(Item item,int count,bool bag,RectTransform content,List<InventorySlot> rows){var row=Instantiate(SlotPrefab,content);row.Bind(item.Icon,item.Name,count,selected==item.Id&&fromBag==bag);row.Button.onClick.AddListener(()=>{selected=item.Id;fromBag=bag;RefreshSelection();});rows.Add(row);}
  void RefreshSelection(){var visible=Items.Where(i=>(category==0||i.Category==category)&&StockCount(i.Id)>0).ToArray();for(int i=0;i<stockRows.Count;i++)stockRows[i].Selection.enabled=!fromBag&&visible[i].Id==selected;var held=Items.Where(i=>BagCount(member,i.Id)>0).ToArray();for(int i=0;i<bagRows.Count;i++)bagRows[i].Selection.enabled=fromBag&&i<held.Length&&held[i].Id==selected;RefreshDetail();}
  void RefreshMembers(){for(int i=0;i<MemberCards.Length;i++){int index=page*MemberCards.Length+i;var card=MemberCards[i];card.gameObject.SetActive(index<people.Length);if(index>=people.Length)continue;int originalIndex=Array.IndexOf(owner.Campaign.Party.ToArray(),people[index]);var id=PartySelectionSession.Selected.ElementAtOrDefault(originalIndex);var data=owner.Roster.Candidates.FirstOrDefault(x=>x.Id==id)??owner.Roster.Candidates.FirstOrDefault(x=>x.DisplayName==people[index].Name);card.Bind(data?.Portrait,people[index].Name,0,index==member);card.Paper.color=index==member?new Color(1,.8f,.43f):Color.white;card.Button.onClick.RemoveAllListeners();card.Button.onClick.AddListener(()=>{member=index;selected=null;Refresh();Reset(BagScroll);});}PrevMember.gameObject.SetActive(page>0);NextMember.gameObject.SetActive((page+1)*MemberCards.Length<people.Length);}
  void RefreshDetail(){var item=Items.FirstOrDefault(i=>i.Id==selected);DetailTitle.text=item?.Name??"물건 선택";DetailIcon.sprite=item?.Icon;DetailIcon.enabled=item!=null;Description.text=item?.Description??"창고나 가방의 물건을\n눌러 확인하세요.";Location.text=item==null?"":fromBag?"보관 위치 · 개인 가방":"보관 위치 · 공용 창고";ToBag.interactable=item!=null&&!fromBag&&Maximum()>0;ToStock.interactable=item!=null&&fromBag&&Maximum()>0;Move.interactable=item!=null&&Maximum()>0;Move.transform.Find("Label").GetComponent<Text>().text=fromBag?"창고로 옮기기":"가방에 넣기";if(item!=null&&!fromBag&&Maximum()==0)Location.text="가방이 가득 찼습니다.";RefreshUse(item);}
  string UseBlock(Item item){
   if(owner.Introduction&&!owner.Introduction.Allows(5))return "물건 확인을 마친 뒤 사용할 수 있습니다.";
   if(item==null||item.Recovery<=0)return "사용할 수 없는 물건입니다.";
   if(!IsOpen||QuantityPopup.activeSelf||owner.Campaign?.Stage!=JourneyStage.Settlement||member<0||member>=people.Length)return "정착지에서만 사용할 수 있습니다.";
   var p=people[member];if(p.Health<=0)return "행동 불능 · 사용 불가";
   if(p.Health>=p.MaxHealth)return "체력이 가득 찼습니다.";
   if(owner.IsAssigned(p))return "작업 종료 후 사용 가능";
   if(item.UseCost<1||item.UseMinutes<1||item.UseMinutes>10080)return "사용 설정을 확인하세요.";
   if((fromBag?CountFor(p,item.Id):StockCount(item.Id))<item.UseCost)return "재료 부족 · 보관 위치 확인";
   if(!fromBag&&!owner.CraftPanel.Materials.Any(m=>m.Id==item.Id))return "사용할 재료가 없습니다.";
   return null;
  }
  void RefreshUse(Item item){
   if(!Use)return;Use.interactable=UseBlock(item)==null;Use.GetComponentInChildren<Text>().text=item!=null&&item.Recovery>0?item.UseVerb:"사용";
   if(item==null||item.Recovery<=0||people.Length==0)return;
   var p=people[member];Description.text=item.Name+" "+item.UseCost+"개 · "+item.UseMinutes+"분\n체력 "+p.Health+" → "+Math.Min(p.MaxHealth,p.Health+item.Recovery)+" / "+p.MaxHealth;
   Location.text=UseBlock(item)??(fromBag?"개인 가방 재료 사용":"공용 창고 재료 사용");
  }
  public bool UseSelected(){
   if(owner.Introduction&&!owner.Introduction.Allows(5))return false;
   var item=Items.FirstOrDefault(i=>i.Id==selected);if(UseBlock(item)!=null)return false;
   var p=people[member];int before=p.Health;
   if(fromBag)Bag(p)[item.Id]=CountFor(p,item.Id)-item.UseCost;
   else owner.CraftPanel.Materials.First(m=>m.Id==item.Id).Initial-=item.UseCost;
   p.Health=Math.Min(p.MaxHealth,p.Health+item.Recovery);
   owner.Campaign.AdvanceSettlementTime(item.UseMinutes);
   string result=p.Name+" · "+item.UseVerb+" · 체력 "+before+" → "+p.Health;
   owner.ActivityLog.Add(owner.Campaign.ClockText.Replace("\n"," ")+" · "+result);
   Notice.text=result+" · "+item.Name+" -"+item.UseCost+" · "+item.UseMinutes+"분";
   owner.RefreshMembers();Refresh();return true;
  }
  int Maximum(){if(selected==null||people.Length==0)return 0;if(fromBag)return TransferLimit(people[member],selected,false);if(BagCount(member,selected)==0&&UsedSlots(member)>=people[member].BagCapacity)return 0;return StockCount(selected);}
  void AskMove(){if(Maximum()<1)return;quantity=1;QuantityTitle.text=Items.First(i=>i.Id==selected).Name+" 옮기기";QuantityPopup.SetActive(true);Workspace.interactable=false;Workspace.blocksRaycasts=false;RefreshQuantity();EventSystem.current?.SetSelectedGameObject(Confirm.gameObject);}
  void RefreshQuantity(){int maximum=Maximum();quantity=Mathf.Clamp(quantity,0,maximum);QuantityValue.text=quantity.ToString();QuantityHint.text=(fromBag?"개인 가방 → 공용 창고":"공용 창고 → "+people[member].Name+" 가방")+"\n이동 가능 "+maximum+"개";Minus.interactable=quantity>1;Plus.interactable=quantity<maximum;Max.interactable=maximum>0;Confirm.interactable=quantity>0;}
  void Transfer(){int count=quantity;if(count<1||count>Maximum()){RefreshQuantity();return;}var bag=Bag(people[member]);int before=BagCount(member,selected);if(!CampaignCounted(selected)){var mat=owner.CraftPanel.Materials.FirstOrDefault(m=>m.Id==selected);if(mat==null)return;mat.Initial+=fromBag?count:-count;}bag[selected]=before+(fromBag?-count:count);Notice.text=Items.First(i=>i.Id==selected).Name+" "+count+"개를 "+(fromBag?"창고로 옮겼습니다.":people[member].Name+"에게 옮겼습니다.");Dismiss();Refresh();}
  void Dismiss(){QuantityPopup.SetActive(false);Workspace.interactable=true;Workspace.blocksRaycasts=true;EventSystem.current?.SetSelectedGameObject(Move.gameObject);}
  public void Close(){if(!IsOpen)return;if(owner.Introduction)owner.Introduction.Completed("stock");QuantityPopup.SetActive(false);View.SetActive(false);owner.Main.interactable=true;owner.Main.blocksRaycasts=true;for(int i=0;i<HideWhileOpen.Length;i++)HideWhileOpen[i].SetActive(hidden[i]);EventSystem.current?.SetSelectedGameObject(returnFocus);expeditionPeople=null;var callback=returnToPlan;returnToPlan=null;CloseButton.transform.Find("Label").GetComponent<Text>().text="돌아가기";callback?.Invoke();}
  void Update(){if(IsOpen&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){if(QuantityPopup.activeSelf)Dismiss();else Close();}}
 }
}

