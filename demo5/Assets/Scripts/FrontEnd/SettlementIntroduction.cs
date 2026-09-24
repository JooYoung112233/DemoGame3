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
  readonly string[] titles={"주변 확인","남은 물건 찾기","쉴 자리 마련","동료 쉬게 하기","물건 보관하기","첫 외출"};
  readonly string[] hints={"위의 ‘주변 확인’ 버튼을 누르세요.\n안전하게 머물 공간부터 살펴봅니다.","위의 ‘남은 상자 수색’ 버튼을 누르세요.\n건물 안에 남은 물건을 찾습니다.","위의 ‘잠자리 정리’ 버튼을 누르세요.\n잔해를 치워 쉴 공간을 만듭니다.","빛나는 잠자리를 눌러 여세요.\n쉴 대원을 고른 뒤 ‘휴식 시작’.","위의 ‘보관함 열기’ 버튼을 누르세요.\n공용 물자와 개인 가방을 확인합니다.","위의 ‘첫 원정 준비’ 버튼을 누르세요.\n폐상가에서 목재·고철을 찾습니다."};
  public bool Allows(int step)=>Step>=step;
  public void Initialize(SettlementController c,bool restoring){owner=c;Action.onClick.AddListener(Act);Action.gameObject.SetActive(false);if(c.Campaign==null||restoring)return;Step=0;c.Campaign.BeginSettlementIntroduction();foreach(var m in c.CraftPanel.Materials)m.Initial=0;c.ActivityLog.Add("도착 · 빈 건물에 몸을 피했다. 망가진 시설은 외부에서 재료를 가져와 복구해야 한다.");}
  public void Restore(int step){Step=step>=6&&step<10?5:step;}
  public void Completed(string kind){if(Step==3&&kind=="rest" || Step==4&&kind=="stock")Next();}
  void Next(){Step++;owner.ActivityLog.Add(owner.Campaign.ClockText.Replace("\n"," ")+" · "+titles[Step]);}
  public void Act(){if(!owner||owner.Campaign==null||!owner.Main.interactable||owner.Campaign.Stage!=JourneyStage.Settlement )return;if(Step>=5){if(owner.Opening)owner.Opening.Act();return;}if(Step==3){if(owner.WorkPanel.Orders.Count>0)owner.TimePanel.Open();else owner.WorkPanel.Open();return;}if(Step==4){owner.InventoryPanel.Open();return;}int minutes=Step==2?20:10;if(Step==1){owner.CraftPanel.Materials.First(m=>m.Id=="cloth").Initial++;owner.CraftPanel.Materials.First(m=>m.Id=="water").Initial++;bool ammo=FoundAmmo>0&&owner.Campaign.AddSettlementAmmo(FoundAmmo);owner.RefreshResources();owner.ActivityLog.Add(ammo?"현장 발견 · 헝겊 1개, 밀봉된 물 1개, 탄약 "+FoundAmmo+"발":"현장 발견 · 헝겊 1개와 밀봉된 물 1개");}Next();owner.Campaign.AdvanceSettlementTime(minutes);owner.RefreshMembers();}
  void Set(Button b,bool show){if(b)b.gameObject.SetActive(show);}
  void LateUpdate(){
   if(!owner||owner.Campaign==null)return;
   if(Step==5&&owner.ReturnPanel.HasReport){Step=10;owner.ActivityLog.Add("첫 귀환 · 회수한 물자를 보관하고 시설을 복구하자.");owner.NoticeTitle.text="시설 복구 시작";owner.NoticeBody.text="회수품을 임시 보관함에 옮기세요.\n시설 복구에서 담당자를 배정하세요.";}
   bool main=owner.Main.gameObject.activeInHierarchy&&owner.Main.interactable&&owner.Campaign.Stage==JourneyStage.Settlement;
   Action.gameObject.SetActive(main&&Step<=4);if(!main)return;
   Set(owner.Bed,Step>=3);Set(owner.Cabinet,Step>=4);Set(owner.Workbench,Step>=5&&(!owner.Opening.Active||owner.Opening.State.FirstReturn));Set(owner.Stock,Step>=5&&(!owner.Opening.Active||owner.Development.State.Cooker));Set(owner.Exit,Step>=5);
   Set(owner.Advance,Step>3||Step==3&&owner.WorkPanel.Orders.Count>0);Set(owner.Journal,Step>=10);Set(owner.VisitorPanel.OpenButton,Step>=10&&(!owner.Opening.Active||owner.Opening.State.Complete));Set(owner.CraftPanel.HousingButton,Step>=10&&(!owner.Opening.Active||owner.Opening.State.Complete));Set(owner.WorkPanel.Improve,Step>=5);Set(owner.CookingPanel.Improve,Step>=5);
   foreach(var m in owner.Members)if(m.BagButton)m.BagButton.gameObject.SetActive(Step>=4);
   if(Step>=5&&owner.Opening&&owner.Opening.Active){owner.Opening.RefreshGuidance();return;}
   if(Step>=10)return;owner.NoticeTitle.text=Step<5?"첫 정착 "+(Step+1)+" / 5 · "+titles[Step]:titles[Step];owner.NoticeBody.text=hints[Step];ActionLabel.text=Step==0?"주변 확인 · 10분":Step==1?"남은 상자 수색 · 10분":Step==2?"잠자리 정리 · 20분":Step==3?(owner.WorkPanel.Orders.Count>0?"시간 진행 열기":"잠자리 열기"):"보관함 열기";
   if(Step==3&&owner.WorkPanel.Orders.Count>0){owner.NoticeTitle.text="첫 정착 4 / 5 · 휴식 완료하기";owner.NoticeBody.text="휴식이 예약되었습니다.\n시간을 진행하면 체력이 회복됩니다.";}
  }
 }
}
