using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace Demo5.FrontEnd {
 public sealed partial class SettlementCookingPanel:MonoBehaviour {
  [Serializable] public sealed class Ingredient { public string Id,Name; public Sprite Icon; public int PreviewCount; }
  [Serializable] public sealed class Meal { public string Id,Name,OutputId; [TextArea]public string Description,Use; public int Category,Minutes,Servings=1; public bool WorkerSpeedAllowed; public Sprite Icon; public SettlementCraftPanel.Cost[] Costs; }
  public sealed class Order { public Meal Recipe; public Adventurer Member; public int Quantity,Minutes,OutputCount; public string OutputId; public Dictionary<string,int> Reserved; }
  readonly List<Order> orders=new List<Order>();
  readonly List<CraftOrderRow> orderRows=new List<CraftOrderRow>();
  public IReadOnlyList<Order> Orders=>orders;
  public IReadOnlyList<CraftOrderRow> OrderRows=>orderRows;
  public RectTransform OrderContent;public ScrollRect OrderScroll;public CraftOrderRow OrderPrefab;
  public CanvasGroup Workspace;public GameObject CancelPopup;public Button CancelYes,CancelNo;public Text CancelMessage;
  Order pendingCancel;
  public bool IsAssigned(Adventurer p)=>orders.Any(o=>o.Member==p);
  public int Reserved(string id)=>orders.Sum(o=>o.Reserved.TryGetValue(id,out int n)?n:0);
  public Ingredient[] Ingredients;public Meal[] Meals;
  public Button Back,Minus,Plus,Confirm,Improve;public Text FacilityHeading;public Button[] Tabs;
  public Text Title,Description,Quantity,Duration,ConfirmLabel,ResultTitle,ResultCount,ResultUse,PlanTitle,PlanBody,PlanHint;
  public Image Icon,ResultIcon;
  public RectTransform RecipeContent,WorkerContent,CostContent;
  public ScrollRect RecipeScroll,WorkerScroll,CostScroll;
  public CraftChoiceRow RecipePrefab,WorkerPrefab;public CraftCostRow CostPrefab;
  readonly List<CraftChoiceRow> recipes=new List<CraftChoiceRow>(),workers=new List<CraftChoiceRow>();readonly List<CraftCostRow> costs=new List<CraftCostRow>();
  public IReadOnlyList<CraftChoiceRow> RecipeRows=>recipes;public IReadOnlyList<CraftChoiceRow> WorkerRows=>workers;
  public int SelectedWorker=>worker;public int BatchCount=>quantity;public bool IsOpen=>gameObject.activeSelf;
  SettlementController owner;Adventurer[] people;Meal selected;int category,worker=-1,quantity=1;
  bool AvailableDuringIntro(Meal m)=>m!=null&&(!owner.Introduction||owner.Introduction.Allows(5)||m.Id=="warm");
  public void Initialize(SettlementController c){owner=c;if(Improve)Improve.onClick.AddListener(()=>{Close();owner.CraftPanel.Open();owner.CraftPanel.FocusRecipe("upgrade-cooker");});gameObject.SetActive(false);Back.onClick.AddListener(Close);Minus.onClick.AddListener(()=>{quantity=Math.Max(1,quantity-1);Refresh();});Plus.onClick.AddListener(()=>{quantity=Math.Min(99,quantity+1);Refresh();});Confirm.onClick.AddListener(Register);CancelPopup.SetActive(false);CancelYes.onClick.AddListener(CancelOrder);CancelNo.onClick.AddListener(DismissCancel);for(int i=0;i<Tabs.Length;i++){int k=i;Tabs[i].onClick.AddListener(()=>SelectCategory(k));}}
  static void Clear<T>(List<T> rows) where T:Component {foreach(var r in rows){r.gameObject.SetActive(false);Destroy(r.gameObject);}rows.Clear();}
  static void Top(ScrollRect s){Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(s.content);s.StopMovement();s.verticalNormalizedPosition=1;}
  public void Open(){if(owner.Introduction&&!owner.Introduction.Allows(5))return;if(owner.Development&&!owner.Development.State.Cooker){owner.Development.Open();owner.CraftPanel.FocusRecipe("build-cooker");return;}if(IsOpen||owner.Campaign==null||!owner.Main.interactable)return;for(int i=0;i<Tabs.Length;i++)Tabs[i].gameObject.SetActive(!owner.Introduction||owner.Introduction.Allows(5)||i==0);people=owner.Campaign.Party.ToArray();worker=-1;owner.Main.interactable=false;owner.Main.blocksRaycasts=false;gameObject.SetActive(true);Workspace.interactable=Workspace.blocksRaycasts=true;CancelPopup.SetActive(false);RefreshOrders();Clear(workers);for(int i=0;i<people.Length;i++){int k=i;var row=Instantiate(WorkerPrefab,WorkerContent);workers.Add(row);row.Button.onClick.AddListener(()=>{worker=k;Refresh();});}SelectCategory(0);Top(WorkerScroll);EventSystem.current?.SetSelectedGameObject(Tabs[0].gameObject);}
  void SelectCategory(int value){category=value;Clear(recipes);foreach(var meal in Meals.Where(m=>m.Category==value&&AvailableDuringIntro(m))){var item=meal;var row=Instantiate(RecipePrefab,RecipeContent);row.Label.text=item.Name;row.Icon.sprite=item.Icon;row.Button.onClick.AddListener(()=>Select(item));recipes.Add(row);}Select(Meals.FirstOrDefault(m=>m.Category==value&&AvailableDuringIntro(m)));Top(RecipeScroll);}
  void Select(Meal m){selected=m;quantity=1;Clear(costs);if(m!=null)foreach(var cost in m.Costs){var row=Instantiate(CostPrefab,CostContent);var ingredient=Ingredients.First(x=>x.Id==cost.MaterialId);row.Label.text=ingredient.Name;row.Icon.sprite=ingredient.Icon;var b=row.GetComponent<Button>();if(b){b.transition=Selectable.Transition.None;b.interactable=false;}costs.Add(row);}Refresh();Top(CostScroll);}
  void Refresh(){for(int i=0;i<Tabs.Length;i++)Tabs[i].GetComponent<Image>().color=i==category?new Color(1,.78f,.37f):Color.white;var visible=Meals.Where(m=>m.Category==category&&AvailableDuringIntro(m)).ToArray();for(int i=0;i<recipes.Count;i++){bool active=visible[i]==selected;recipes[i].Check.gameObject.SetActive(active);recipes[i].Paper.color=active?new Color(1,.81f,.46f):Color.white;}
   for(int i=0;i<workers.Count;i++){var p=people[i];var r=workers[i];var id=PartySelectionSession.Selected.ElementAtOrDefault(i);r.Icon.sprite=owner.Roster.Candidates.FirstOrDefault(x=>x.Id==id)?.Portrait;r.Label.text=p.Name;bool free=p.Health>0&&!owner.IsAssigned(p);r.State.text=free?"대기":p.Health<=0?"불가":"배정됨";int speed=WorkerPercent(selected,p);r.State.fontSize=26;if(free&&speed<100)r.State.text="−"+(100-speed)+"%";r.Button.interactable=free;r.Check.gameObject.SetActive(i==worker&&free);r.Paper.color=i==worker&&free?new Color(1,.81f,.46f):Color.white;}
   bool enough=selected!=null;for(int i=0;i<costs.Count;i++){var cost=selected.Costs[i];int have=owner.CraftPanel.Available(cost.MaterialId),need=cost.Count*quantity;bool ok=have>=need;enough&=ok;costs[i].Count.text=have+" / "+need;costs[i].Paper.color=ok?Color.white:new Color(1,.59f,.54f);costs[i].Count.color=ok?new Color(.13f,.32f,.2f):new Color(.48f,.06f,.04f);}
   Title.text=selected?.Name??"조리법 없음";Description.text=selected?.Description??"";Icon.sprite=selected?.Icon;Icon.enabled=Icon.sprite;Quantity.text=quantity.ToString();Duration.text="예상 시간  "+(selected==null?0:owner.CraftPanel.CookingDuration(selected.Minutes,quantity))+"분";Minus.interactable=quantity>1;Plus.interactable=quantity<99&&selected!=null;
   ResultIcon.sprite=selected?.Icon;ResultIcon.enabled=ResultIcon.sprite;ResultTitle.text=selected?.Name??"완성 음식";ResultCount.text="완성 예상  "+(selected==null?0:selected.Servings*quantity)+"인분";ResultUse.text=selected?.Use??"";
   bool valid=worker>=0&&worker<people.Length&&people[worker].Health>0&&!owner.IsAssigned(people[worker]);Confirm.interactable=enough&&valid;ConfirmLabel.text=!enough?"재료 부족":!valid?"담당자 선택":"조리 시작";PlanTitle.text="진행 중인 조리";PlanBody.text="조리 예약이 없습니다.";PlanHint.text="조리대 Lv."+owner.CraftPanel.CookerLevel+" · "+(owner.CraftPanel.CookerImproved?"시간 −"+(100-owner.CraftPanel.CookerTimePercent)+"%":"기본 조리 시간");
   RefreshTraitTime();if(FacilityHeading)FacilityHeading.text="식량 준비 · 조리대 Lv."+owner.CraftPanel.CookerLevel;
   if(owner.CraftPanel.UnderConstruction("upgrade-cooker")){Confirm.interactable=false;ConfirmLabel.text="조리대 공사 중";}
   if(Improve)Improve.GetComponentInChildren<Text>().text=owner.CraftPanel.UnderConstruction("upgrade-cooker")?"조리대 공사 확인":"조리대 개선";
  }
  public void Close(){if(!IsOpen)return;CancelPopup.SetActive(false);pendingCancel=null;gameObject.SetActive(false);owner.Main.interactable=true;owner.Main.blocksRaycasts=true;EventSystem.current?.SetSelectedGameObject(owner.Stock.gameObject);}
  void Update(){if(IsOpen&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){if(CancelPopup.activeSelf)DismissCancel();else Close();}}
 }
}
