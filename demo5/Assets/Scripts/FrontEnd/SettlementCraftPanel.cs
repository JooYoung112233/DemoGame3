using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace Demo5.FrontEnd
{
    public sealed partial class SettlementCraftPanel:MonoBehaviour
    {
        [Serializable] public sealed class Material { public string Id,Name; [Min(0)] public int Initial; public Sprite Icon; }
        [Serializable] public sealed class Cost { public string MaterialId; [Min(1)] public int Count=1; }
        [Serializable] public sealed class Recipe {
            public string Id,Name,OutputId,UnlockProject;
            [Min(1)] public int OutputCount=1;
            [TextArea]public string Description; [Range(0,2)]public int Category; [Min(1)]public int Minutes=30; public Sprite Icon; public Cost[] Costs;
            public string ProducedId=>string.IsNullOrEmpty(OutputId)?Id:OutputId;
            public int ProducedCount=>Math.Max(1,OutputCount);
        }
        public sealed class Order { public Recipe Recipe; public Adventurer Member; public int Quantity,Minutes; public Dictionary<string,int> Reserved; }
        public bool BedRepaired{get;private set;}
        public bool BenchImproved{get;private set;}
        public bool FacilityDone(Recipe r)=>r!=null&&((owner&&owner.Development&&owner.Development.IsDone(r.Id))||(r.Id=="repair-bed"&&BedRepaired)||(r.Id=="upgrade-bench"&&BenchImproved)||(r.Id=="upgrade-cooker"&&CookerImproved)||(r.Id=="open-side-room"&&SideRoomConnected)||(r.Id=="prepare-side-room"&&SideRoomReady));
        public int DurationFor(Recipe r,int count)=>Math.Max(1,BenchImproved?(r.Minutes*80+99)/100:r.Minutes)*count;
        public string ProductSummary(Recipe r,int count)=>r.Category!=0?r.Name+" ×"+count:(Materials.FirstOrDefault(m=>m.Id==r.ProducedId)?.Name??r.Name)+" ×"+(count*r.ProducedCount);
        public Material[] Materials;
        public Recipe[] Recipes;
        public CraftMaterialGuide MaterialGuide;
        public GameObject View,CancelPopup;
        public CanvasGroup Workspace;
        public GameObject[] HideWhileOpen;
        public Button CloseButton,Minus,Plus,Confirm,CancelYes,CancelNo;
        public Button[] Tabs;
        public Text DetailTitle,DetailDescription,QuantityText,Duration,ConfirmLabel,OrderCount,EmptyOrders,CancelMessage;
        public Image DetailIcon;
        public RectTransform RecipeContent,WorkerContent,CostContent,OrderContent;
        public ScrollRect WorkerScroll,OrderScroll,RecipeScroll,CostScroll,CancelMessageScroll;
        public CraftChoiceRow RecipePrefab,WorkerPrefab;
        public CraftCostRow CostPrefab;
        public CraftOrderRow OrderPrefab;
        readonly List<Order> orders=new List<Order>();
        readonly List<CraftChoiceRow> recipeRows=new List<CraftChoiceRow>(),workerRows=new List<CraftChoiceRow>();
        readonly List<CraftCostRow> costs=new List<CraftCostRow>();
        readonly List<CraftOrderRow> orderRows=new List<CraftOrderRow>();
        public IReadOnlyList<Order> Orders=>orders;
        public IReadOnlyList<CraftChoiceRow> RecipeRows=>recipeRows;
        public IReadOnlyList<CraftChoiceRow> WorkerRows=>workerRows;
        public IReadOnlyList<CraftOrderRow> OrderRows=>orderRows;
        public IReadOnlyList<CraftCostRow> CostRows=>costs;
        SettlementController owner;
        public string SelectedRecipeId=>selected?.Id;
        Adventurer[] people=Array.Empty<Adventurer>();
        Recipe selected;
        int category,worker=-1,quantity=1;
        Order pendingCancel;
        bool[] hiddenStates;
        public bool IsOpen=>View&&View.activeSelf;
        public bool IsAssigned(Adventurer member)=>orders.Any(o=>o.Member==member);
        public int Available(string id)=>Math.Max(0,(Materials.FirstOrDefault(m=>m.Id==id)?.Initial??0)-orders.Sum(o=>o.Reserved.TryGetValue(id,out int count)?count:0)-(owner&&owner.CookingPanel?owner.CookingPanel.Reserved(id):0));
        public void Initialize(SettlementController controller){owner=controller;if(HousingButton)HousingButton.onClick.AddListener(OpenHousing);View.SetActive(false);CancelPopup.SetActive(false);CloseButton.onClick.AddListener(Close);Minus.onClick.AddListener(()=>{quantity=Math.Max(1,quantity-1);Refresh();});Plus.onClick.AddListener(()=>{quantity=Math.Min(99,quantity+1);Refresh();});Confirm.onClick.AddListener(Register);CancelYes.onClick.AddListener(CancelOrder);CancelNo.onClick.AddListener(DismissCancel);for(int i=0;i<Tabs.Length;i++){int index=i;Tabs[i].onClick.AddListener(()=>SelectCategory(index));}}
        static void Clear<T>(List<T> list) where T:Component {foreach(var row in list){row.gameObject.SetActive(false);Destroy(row.gameObject);}list.Clear();}
        static void ResetScroll(ScrollRect scroll){if(!scroll)return;Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);scroll.StopMovement();scroll.verticalNormalizedPosition=1;}
        public void Open(){
            if(owner.Introduction&&!owner.Introduction.Allows(5))return;
            for(int i=0;i<Tabs.Length;i++)Tabs[i].gameObject.SetActive(true);
            if(IsOpen||owner.IsPopupOpen||(owner.WorkPanel&&owner.WorkPanel.IsOpen))return;
            people=owner.Campaign?.Party.ToArray()??Array.Empty<Adventurer>();worker=-1;hiddenStates=HideWhileOpen.Select(g=>g.activeSelf).ToArray();foreach(var g in HideWhileOpen)g.SetActive(false);
            owner.Main.interactable=false;owner.Main.blocksRaycasts=false;View.SetActive(true);Workspace.interactable=true;Workspace.blocksRaycasts=true;CancelPopup.SetActive(false);pendingCancel=null;
            Clear(workerRows);for(int i=0;i<people.Length;i++){int index=i;var row=Instantiate(WorkerPrefab,WorkerContent);workerRows.Add(row);row.Button.onClick.AddListener(()=>{worker=index;Refresh();});}
            WorkerScroll.verticalNormalizedPosition=1;OrderScroll.verticalNormalizedPosition=1;SelectCategory(owner.Development&&!owner.Development.State.Workbench?1:0);RefreshOrders();EventSystem.current?.SetSelectedGameObject(Tabs[0].gameObject);
        }
        void SelectCategory(int value){category=value;selected=null;quantity=1;Clear(recipeRows);foreach(var recipe in Recipes.Where(r=>r.Category==category&&AvailableDuringIntro(r))){var item=recipe;var row=Instantiate(RecipePrefab,RecipeContent);row.Label.text=item.Name;row.Icon.sprite=item.Icon;row.Button.onClick.AddListener(()=>SelectRecipe(item));recipeRows.Add(row);}
            SelectRecipe(Recipes.FirstOrDefault(r=>r.Category==category&&AvailableDuringIntro(r)));ResetScroll(RecipeScroll);}
        void SelectRecipe(Recipe recipe){selected=recipe;if(worker>=people.Length||(worker>=0&&(people[worker].Health<=0||owner.IsAssigned(people[worker]))))worker=-1;quantity=1;Clear(costs);if(selected!=null)foreach(var cost in selected.Costs){var row=Instantiate(CostPrefab,CostContent);var mat=Materials.FirstOrDefault(m=>m.Id==cost.MaterialId);row.Label.text=mat?.Name??cost.MaterialId;row.Icon.sprite=mat?.Icon;costs.Add(row);string materialId=cost.MaterialId;var button=row.GetComponent<Button>();if(button&&MaterialGuide)button.onClick.AddListener(()=>MaterialGuide.Select(materialId));}Refresh();ResetScroll(CostScroll);}
        void Refresh(){
            for(int i=0;i<Tabs.Length;i++)Tabs[i].GetComponent<Image>().color=i==category?new Color(1,.78f,.37f):Color.white;
            var visible=Recipes.Where(r=>r.Category==category&&AvailableDuringIntro(r)).ToArray();for(int i=0;i<recipeRows.Count;i++){bool chosen=visible[i]==selected;recipeRows[i].Paper.color=chosen?new Color(1,.81f,.46f):Color.white;recipeRows[i].Check.gameObject.SetActive(chosen);}
            for(int i=0;i<people.Length;i++){var r=workerRows[i];var p=people[i];var id=PartySelectionSession.Selected.ElementAtOrDefault(i);var data=owner.Roster.Candidates.FirstOrDefault(d=>d.Id==id)??owner.Roster.Candidates.FirstOrDefault(d=>d.DisplayName==p.Name);r.Label.text=p.Name;r.Icon.sprite=data?.Portrait;r.State.text=owner.IsAssigned(p)?"배정됨":p.Health>0?"대기":"불가";int speed=owner.LifeTimePercent(p,false);r.State.fontSize=26;if(p.Health>0&&!owner.IsAssigned(p)&&speed<100)r.State.text="−"+(100-speed)+"%";r.Button.interactable=p.Health>0&&!owner.IsAssigned(p);r.Check.gameObject.SetActive(i==worker);r.Paper.color=i==worker?new Color(1,.78f,.37f):Color.white;}
            DetailTitle.text=selected?.Name??"작업 선택";DetailDescription.text=selected?.Description??"가능한 작업이 없습니다.";if(selected!=null&&selected.Category!=0)DetailDescription.text=FacilityDescription(selected);DetailIcon.sprite=selected?.Icon;DetailIcon.gameObject.SetActive(selected!=null);QuantityText.text=quantity.ToString();Duration.text=selected==null?"":"예상 시간  "+DurationFor(selected,quantity)+"분";
            bool enough=selected!=null;for(int i=0;i<costs.Count;i++){var cost=selected.Costs[i];int available=Available(cost.MaterialId),needed=cost.Count*quantity;bool ok=available>=needed;enough&=ok;costs[i].Count.text=available+" / "+needed;costs[i].Paper.color=ok?Color.white:new Color(1,.59f,.54f);costs[i].Count.color=ok?new Color(.13f,.32f,.2f):new Color(.48f,.06f,.04f);}
            bool valid=owner.Campaign!=null&&worker>=0&&worker<people.Length&&people[worker].Health>0&&!owner.IsAssigned(people[worker]);Confirm.interactable=enough&&valid;ConfirmLabel.text=!enough?"재료 부족":!valid?"담당자 선택":"작업 시작";Minus.interactable=quantity>1;Plus.interactable=quantity<99&&selected!=null&&selected.Category==0;
            if(selected!=null&&selected.Category!=0&&(FacilityDone(selected)||orders.Any(o=>o.Recipe.Id==selected.Id))){Confirm.interactable=false;ConfirmLabel.text=FacilityDone(selected)?"개선 완료":"진행 중";}
            var blocked=ConstructionBlock(selected);if(blocked!=null){Confirm.interactable=false;ConfirmLabel.text=blocked;}
            RefreshTraitTime();if(MaterialGuide)MaterialGuide.Refresh(owner,this,selected,quantity);
        }
        public void FocusRecipe(string id){var r=Recipes.FirstOrDefault(x=>x.Id==id);if(r==null||!AvailableDuringIntro(r)||!IsOpen||CancelPopup.activeSelf)return;SelectCategory(r.Category);SelectRecipe(r);}
        public bool IsRecipeUnlocked(Recipe r)=>r!=null&&(string.IsNullOrEmpty(r.UnlockProject)||(owner&&owner.Development&&owner.Development.IsDone(r.UnlockProject)));
        bool AvailableDuringIntro(Recipe r)=>IsRecipeUnlocked(r)&&(!owner.Introduction||owner.Introduction.Allows(5));
        void Register(){
            if(!AvailableDuringIntro(selected))return;
            if(owner.Introduction&&!owner.Introduction.Allows(5))return;
            if(selected!=null&&selected.Category!=0)quantity=1;
            if(selected==null||ConstructionBlock(selected)!=null||FacilityDone(selected)||(selected.Category!=0&&orders.Any(o=>o.Recipe.Id==selected.Id))||owner.Campaign==null||worker<0||worker>=people.Length||people[worker].Health<=0||owner.IsAssigned(people[worker]))return;
            var reservation=selected.Costs.GroupBy(c=>c.MaterialId).ToDictionary(g=>g.Key,g=>g.Sum(c=>c.Count)*quantity);if(reservation.Any(c=>Available(c.Key)<c.Value))return;
            orders.Add(new Order{Recipe=selected,Member=people[worker],Quantity=quantity,Minutes=DurationFor(selected,quantity,people[worker]),Reserved=reservation});owner.NoticeTitle.text="작업 예약";owner.NoticeBody.text=selected.Name+" ×"+quantity+" 예약\n“"+owner.DataFor(people[worker])?.WorkLine+"”";Close();owner.RefreshMembers();
        }
        void RefreshOrders(){Clear(orderRows);foreach(var order in orders){var item=order;var row=Instantiate(OrderPrefab,OrderContent);row.Icon.sprite=item.Recipe.Icon;row.Title.text=item.Recipe.Name+" ×"+item.Quantity;row.Member.text=item.Member.Name;row.Status.text="예약 · "+item.Minutes+"분";row.Cancel.onClick.AddListener(()=>AskCancel(item));orderRows.Add(row);}OrderCount.text=orders.Count+"건";EmptyOrders.gameObject.SetActive(orders.Count==0);}
        void AskCancel(Order order){pendingCancel=order;CancelMessage.text=order.Recipe.Name+" ×"+order.Quantity+" 예약을 취소할까요?\n\n예약 재료를 모두 돌려놓습니다.\n"+string.Join(" · ",order.Reserved.Select(c=>(Materials.FirstOrDefault(m=>m.Id==c.Key)?.Name??c.Key)+" "+c.Value));Workspace.interactable=false;Workspace.blocksRaycasts=false;CancelPopup.SetActive(true);ResetScroll(CancelMessageScroll);EventSystem.current?.SetSelectedGameObject(CancelNo.gameObject);}
        void CancelOrder(){if(pendingCancel!=null)orders.Remove(pendingCancel);DismissCancel();RefreshOrders();Refresh();owner.RefreshMembers();}
        void DismissCancel(){pendingCancel=null;CancelPopup.SetActive(false);Workspace.interactable=true;Workspace.blocksRaycasts=true;EventSystem.current?.SetSelectedGameObject(CloseButton.gameObject);}
        public void Close(){if(!IsOpen)return;CancelPopup.SetActive(false);pendingCancel=null;View.SetActive(false);owner.Main.interactable=true;owner.Main.blocksRaycasts=true;for(int i=0;i<HideWhileOpen.Length;i++)HideWhileOpen[i].SetActive(hiddenStates[i]);EventSystem.current?.SetSelectedGameObject(owner.Workbench.gameObject);}
        public List<string> AdvanceTime(int minutes){
            var results=new List<string>();
            foreach(var order in orders.ToArray()){
                order.Minutes=Math.Max(0,order.Minutes-minutes);if(order.Minutes>0)continue;
                foreach(var cost in order.Reserved)Materials.First(m=>m.Id==cost.Key).Initial-=cost.Value;
                orders.Remove(order);
                if(owner.Development&&owner.Development.Complete(order.Recipe.Id)){} else if(order.Recipe.Id=="repair-bed")BedRepaired=true;
                else if(order.Recipe.Id=="upgrade-bench")BenchImproved=true;
                else if(order.Recipe.Id=="upgrade-cooker")CookerImproved=true;
                else if(order.Recipe.Id=="open-side-room")SideRoomConnected=true;
                else if(order.Recipe.Id=="prepare-side-room")SideRoomReady=true;
                else {var material=Materials.FirstOrDefault(m=>m.Id==order.Recipe.ProducedId);if(material==null){material=new Material{Id=order.Recipe.ProducedId,Name=order.Recipe.Name,Icon=order.Recipe.Icon};Materials=Materials.Concat(new[]{material}).ToArray();}material.Initial+=order.Quantity*order.Recipe.ProducedCount;}
                results.Add(order.Member.Name+" · "+ProductSummary(order.Recipe,order.Quantity)+" 완료");
                if(owner.Introduction)owner.Introduction.Completed(order.Recipe.Id);
            }return results;
        }
        public string Summary()=>orders.Count==0?"제작 예약 없음":string.Join("\n",orders.Select(o=>o.Member.Name+" · "+o.Recipe.Name+" ×"+o.Quantity+" · "+o.Minutes+"분"));
        void Update(){if(IsOpen&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){if(CancelPopup.activeSelf)DismissCancel();else Close();}}
    }
}
