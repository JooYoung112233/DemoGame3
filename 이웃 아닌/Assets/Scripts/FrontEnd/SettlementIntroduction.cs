using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
 public sealed class SettlementIntroduction : MonoBehaviour
 {
  public Button Action; public Text ActionLabel;
  [Tooltip("2단계 ‘남은 상자 수색’에서 찾는 탄약. 창고에 들어가고 활동 기록에 남는다.")][Min(0)] public int FoundAmmo=2;
  public int Step { get; private set; } = 10;
  SettlementController owner;
  readonly string[] titles={"머물 수 있는 곳인지 확인","남겨진 물자 확인","가져올 물건을 둘 자리","외출 준비","보관함과 개인 가방","출입구 · 외출 준비"};
  readonly string[] hints={"물자를 모아 둘 곳이 필요합니다.\n먼저 안에 사람이 있는지 봅시다.","고칠 재료가 있는지 확인합니다.\n남겨진 상자를 살펴봅시다.","가져올 짐을 바닥에 둘 겁니다.\n내려놓을 자리를 먼저 치웁시다.","필요한 자재를 찾으러 나갈 준비를 합니다.","공용 보관함은 함께 쓸 물자를 둡니다.\n개인 가방의 물건만 들고 나갑니다.","출입구에서 목적지와 동행을 정합니다.\n폐상가에서 시설 복구 자재를 찾아요."};
  public bool Allows(int step)=>Step>=step;
  public void Initialize(SettlementController c,bool restoring){owner=c;Action.onClick.AddListener(Act);Action.gameObject.SetActive(false);if(c.Campaign==null||restoring)return;Step=0;c.Campaign.BeginSettlementIntroduction();foreach(var m in c.CraftPanel.Materials)m.Initial=0;c.ActivityLog.Add("도착 · "+c.Campaign.Home.Name+"에 들어왔다. 수색한 물건을 모아 둘 곳으로 쓸 수 있을지 안쪽부터 확인하기로 했다.");}
  // Retired rest/inventory lessons must not trap old saves or mutate their health and active jobs.
  public void Restore(int step){Step=step==3||step==4||step>=6&&step<10?5:step;}
  public void Completed(string kind){} // Existing callers report optional actions; none gates initial departure.
  void Next(){Step=Step==2?5:Step+1;owner.ActivityLog.Add(owner.Campaign.ClockText.Replace("\n"," ")+" · "+titles[Step]);}
  public void Act(){if(!owner||owner.Campaign==null||!owner.Main.interactable||owner.Campaign.Stage!=JourneyStage.Settlement )return;if(Step>=5){if(owner.Opening)owner.Opening.Act();return;}AdvancePreparation();}
  void AdvancePreparation(){
   int minutes=Step==2?20:10;
   if(Step==1){
    owner.CraftPanel.Materials.First(m=>m.Id=="cloth").Initial++;
    owner.CraftPanel.Materials.First(m=>m.Id=="water").Initial++;
    bool ammo=FoundAmmo>0&&owner.Campaign.AddSettlementAmmo(FoundAmmo);
    owner.RefreshResources();
    owner.ActivityLog.Add(ammo?"현장 발견 · 헝겊 1개, 밀봉된 물 1개, 탄약 "+FoundAmmo+"발":"현장 발견 · 헝겊 1개와 밀봉된 물 1개");
   }
   Next();owner.Campaign.AdvanceSettlementTime(minutes);owner.RefreshMembers();
  }
  // Reuse actual preparation once; completed discovery never grants goods again.
  public void SkipPreparation(){
   if(!owner||owner.Campaign==null||!owner.TutorialSkipped||owner.Campaign.Stage!=JourneyStage.Settlement)return;
   while(Step<3)AdvancePreparation();
   Step=10;Action.gameObject.SetActive(false);
  }
  void Set(Button b,bool show){if(b)b.gameObject.SetActive(show);}
  void LateUpdate(){
   if(!owner||owner.Campaign==null)return;
   if(Step==5&&owner.ReturnPanel.HasReport){Step=10;owner.ActivityLog.Add("첫 귀환 · 가져온 자재를 확인하고 시설을 고칠 준비를 하자.");owner.NoticeTitle.text="머물 곳을 고칠 준비";owner.NoticeBody.text="가져온 자재부터 확인합시다.\n보관할 자리와 작업대가 필요합니다.";}
   bool main=owner.Main.gameObject.activeInHierarchy&&owner.Main.interactable&&owner.Campaign.Stage==JourneyStage.Settlement;
   Action.gameObject.SetActive(main&&Step<=4);if(!main)return;
   Set(owner.Bed,Step>=3);Set(owner.Cabinet,Step>=4);Set(owner.Workbench,Step>=5&&(owner.TutorialSkipped||!owner.Opening.Active||owner.Opening.State.FirstReturn));Set(owner.Stock,Step>=5&&(owner.TutorialSkipped||!owner.Opening.Active||owner.Development.State.Cooker));Set(owner.Exit,Step>=5);
   Set(owner.Advance,Step>3||Step==3&&owner.WorkPanel.Orders.Count>0);Set(owner.Journal,Step>=10||owner.MissingPerson);Set(owner.VisitorPanel.OpenButton,Step>=10&&(owner.TutorialSkipped||!owner.Opening.Active||owner.Opening.State.Complete));Set(owner.CraftPanel.HousingButton,Step>=10&&(owner.TutorialSkipped||!owner.Opening.Active||owner.Opening.State.Complete));Set(owner.WorkPanel.Improve,Step>=5);Set(owner.CookingPanel.Improve,Step>=5);
   foreach(var m in owner.Members)if(m.BagButton)m.BagButton.gameObject.SetActive(Step>=4);
   if(Step>=5&&owner.Opening&&owner.Opening.Active){owner.Opening.RefreshGuidance();return;}
   if(Step>=10)return;owner.NoticeTitle.text=titles[Step];owner.NoticeBody.text=hints[Step];ActionLabel.text=Step==0?"주변 살피기 · 10분":Step==1?"상자 살펴보기 · 10분":Step==2?"짐 둘 자리 치우기 · 20분":"외출 준비";
  }
 }
}
