using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Demo5.NightRun;
namespace Demo5.FrontEnd
{
    public sealed partial class SettlementWorkPanel:MonoBehaviour
    {
        public GameObject View;
        public RectTransform Content;
        public ScrollRect Scroll;
        public SettlementWorkRow RowPrefab;
        public Button CloseButton,ShortRest,Sleep,Confirm,Improve;
        public Text FacilityHeading;
        public Image DetailPortrait,ShortPaper,SleepPaper;
        public Text DetailName,DetailState,DetailHealth,DetailDescription,Duration,Effect,ConfirmLabel;
        public GameObject[] HideWhileOpen;
        public int ShortMinutes=30,SleepMinutes=120;
        public int ShortRecovery=1,SleepRecovery=3;
        public sealed class Order { public Adventurer Member; public int Minutes,Recovery; public string Name; }
        readonly List<Order> orders=new List<Order>();
        readonly List<SettlementWorkRow> rows=new List<SettlementWorkRow>();
        public IReadOnlyList<Order> Orders=>orders;
        public IReadOnlyList<SettlementWorkRow> Rows=>rows;
        SettlementController owner;
        Adventurer[] people;
        bool[] hiddenStates;
        int selected=-1;
        bool sleeping;
        public bool IsOpen=>View&&View.activeSelf;
        public void Initialize(SettlementController controller){owner=controller;if(Improve)Improve.onClick.AddListener(()=>{Close();owner.CraftPanel.Open();owner.CraftPanel.FocusRecipe(owner.CraftPanel.BedLevel==0?"build-bed":"repair-bed");});View.SetActive(false);CloseButton.onClick.AddListener(Close);ShortRest.onClick.AddListener(()=>{sleeping=false;Refresh();});Sleep.onClick.AddListener(()=>{sleeping=true;Refresh();});Confirm.onClick.AddListener(Register);}
        public void Open(){
            if(owner.Introduction&&!owner.Introduction.Allows(3))return;
            if(IsOpen||owner.IsPopupOpen||(owner.CraftPanel&&owner.CraftPanel.IsOpen))return;
            people=owner.Campaign?.Party.ToArray()??new Adventurer[0];selected=-1;sleeping=false;
            foreach(var row in rows)Destroy(row.gameObject);rows.Clear();
            for(int i=0;i<people.Length;i++){int index=i;var row=Instantiate(RowPrefab,Content);rows.Add(row);row.Button.onClick.AddListener(()=>{selected=index;Refresh();});}
            hiddenStates=HideWhileOpen.Select(g=>g.activeSelf).ToArray();foreach(var g in HideWhileOpen)g.SetActive(false);
            owner.Main.interactable=false;owner.Main.blocksRaycasts=false;View.SetActive(true);Scroll.verticalNormalizedPosition=1;Refresh();EventSystem.current?.SetSelectedGameObject(ShortRest.gameObject);
        }
        PartyCandidate Data(int index){var id=PartySelectionSession.Selected.ElementAtOrDefault(index);return owner.Roster.Candidates.FirstOrDefault(c=>c.Id==id);}
        void Refresh(){
            if(FacilityHeading)FacilityHeading.text=owner.CraftPanel.BedLevel==0?"바닥 잠자리 · 임시 휴식":"침대 · Lv."+owner.CraftPanel.BedLevel; Sleep.interactable=owner.CraftPanel.BedLevel>0; if(!Sleep.interactable)sleeping=false;
            if(Improve)Improve.GetComponentInChildren<Text>().text=owner.CraftPanel.UnderConstruction("repair-bed")?"침대 공사 확인":(owner.CraftPanel.BedLevel==0?"침대 복구":"침대 개선");
            ShortPaper.color=sleeping?Color.white:new Color(1,.78f,.37f);SleepPaper.color=sleeping?new Color(1,.78f,.37f):Color.white;
            for(int i=0;i<people.Length;i++){var p=people[i];var r=rows[i];bool busy=owner.IsAssigned(p);r.Name.text=p.Name;r.Portrait.sprite=Data(i)?.Portrait;r.Condition.text="체력 "+p.Health+" / "+p.MaxHealth;SegmentedHealthGraphic.Set(r.Health,p.Health,p.MaxHealth);r.State.text=busy?"배정됨":p.Health>0?"대기 중":"중상";r.Button.interactable=!busy||orders.Any(o=>o.Member==p);r.Check.gameObject.SetActive(i==selected);r.Paper.color=i==selected?new Color(1,.78f,.37f):Color.white;}
            bool valid=selected>=0&&selected<people.Length&&!owner.IsAssigned(people[selected]);
            DetailPortrait.gameObject.SetActive(valid);DetailName.text=valid?people[selected].Name:"담당자 선택";DetailPortrait.sprite=valid?Data(selected)?.Portrait:null;DetailState.text=valid?(people[selected].Health>0?"대기 중":"중상 · 휴식하면 체력 1로 회복"):"왼쪽 목록에서 선택하세요.";DetailHealth.text=valid?"체력 "+people[selected].Health+" / "+people[selected].MaxHealth:"";
            DetailDescription.text=sleeping?"충분히 잠을 자며\n몸을 쉬게 합니다.":"잠시 쉬며\n숨을 돌립니다.";
            Duration.text="약 "+(sleeping?SleepMinutes:(owner.CraftPanel.BedLevel==0?ShortMinutes*2:ShortMinutes))+"분";int recovery=(sleeping?SleepRecovery:ShortRecovery)+(owner.CraftPanel.BedRepaired?1:0);Effect.text="완료 시 체력 +"+(valid?(people[selected].Health<=0?1:System.Math.Min(recovery,people[selected].MaxHealth-people[selected].Health)):recovery);
            Confirm.interactable=valid&&people[selected].Health<people[selected].MaxHealth;ConfirmLabel.text=valid&&people[selected].Health>=people[selected].MaxHealth?"회복 불필요":"휴식 시작";
            if(owner.CraftPanel.UnderConstruction("repair-bed")||owner.CraftPanel.UnderConstruction("build-bed")){Confirm.interactable=false;ConfirmLabel.text="침대 공사 중";}
            var current=selected>=0?orders.FirstOrDefault(o=>o.Member==people[selected]):null;
            if(current!=null){DetailName.text=current.Member.Name;DetailDescription.text="남은 시간 "+current.Minutes+"분\n중단하면 회복 효과를 받지 않습니다.";Duration.text="진행 중";Effect.text="완료 시 체력 +"+System.Math.Max(0,System.Math.Min(current.Recovery,current.Member.MaxHealth-current.Member.Health));Confirm.interactable=true;ConfirmLabel.text="휴식 중단";}
        }
        void Register(){
            if(selected<0||selected>=people.Length||sleeping&&owner.CraftPanel.BedLevel==0)return;var member=people[selected];if(Cancel(member)){Close();return;}if(owner.CraftPanel.UnderConstruction("repair-bed")||owner.CraftPanel.UnderConstruction("build-bed")||member.Health>=member.MaxHealth||owner.IsAssigned(member))return;
            orders.Add(new Order{Member=member,Minutes=sleeping?SleepMinutes:(owner.CraftPanel.BedLevel==0?ShortMinutes*2:ShortMinutes),Recovery=(sleeping?SleepRecovery:ShortRecovery)+(owner.CraftPanel.BedRepaired?1:0),Name=sleeping?"수면":"짧은 휴식"});
            owner.NoticeTitle.text="휴식 배정";owner.NoticeBody.text=member.Name+" · "+(sleeping?"수면":"짧은 휴식")+"\n“"+owner.DataFor(member)?.RestLine+"”";
            Close();foreach(var card in owner.Members)if(card.gameObject.activeSelf&&card.Name.text==member.Name)card.Status.text="예약";
        }
        public List<string> AdvanceTime(int minutes){
            var results=new List<string>();
            foreach(var order in orders.ToArray()){
                order.Minutes=System.Math.Max(0,order.Minutes-minutes);if(order.Minutes>0)continue;
                int before=order.Member.Health;order.Member.Health=before<=0?1:System.Math.Min(order.Member.MaxHealth,before+order.Recovery); // a downed member only comes back to 1
                orders.Remove(order);results.Add(order.Member.Name+" · "+order.Name+" 완료 (체력 +"+(order.Member.Health-before)+")");
            }if(results.Count>0&&owner.Introduction)owner.Introduction.Completed("rest");return results;
        }
        public bool Cancel(Adventurer member){var order=orders.FirstOrDefault(o=>o.Member==member);if(order==null)return false;orders.Remove(order);owner.RefreshMembers();return true;}
        public string Summary()=>orders.Count==0?"현재 진행 중인 작업은 없습니다.\n\n지금은 정착지를 둘러보세요.":"예약한 휴식\n\n"+string.Join("\n",orders.Select(o=>o.Member.Name+" · "+o.Name+" · "+o.Minutes+"분"));
        public string StateFor(Adventurer member)=>orders.Any(o=>o.Member==member)?"휴식 예약":owner.CraftPanel&&owner.CraftPanel.IsAssigned(member)?"제작 예약":owner.CookingPanel&&owner.CookingPanel.IsAssigned(member)?"조리 예약":member.Health>0?"대기":"회복 필요";
        public void Close(){if(!IsOpen)return;View.SetActive(false);owner.Main.interactable=true;owner.Main.blocksRaycasts=true;for(int i=0;i<HideWhileOpen.Length;i++)HideWhileOpen[i].SetActive(hiddenStates[i]);EventSystem.current?.SetSelectedGameObject(owner.Bed.gameObject);}
        void Update(){if(IsOpen&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)Close();}
    }
}
