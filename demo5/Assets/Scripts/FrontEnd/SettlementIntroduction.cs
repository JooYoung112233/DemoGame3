using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
 public sealed class SettlementIntroduction : MonoBehaviour
 {
  public Button Action; public Text ActionLabel;
  public int Step { get; private set; } = 10;
  SettlementController owner;
  readonly string[] titles={"잠시 머물 곳","남겨진 상자","쉴 자리부터","동료의 숨을 돌리기","함께 쓸 물건","첫 외출"};
  readonly string[] hints={"먼지와 잔해가 가득합니다.\n먼저 안쪽을 확인하세요.","구석에 닫힌 상자가 있습니다.\n무엇이 남았는지 살펴보세요.","침대는 망가져 있습니다.\n바닥의 잔해부터 치우세요.","잠자리에서 짧은 휴식을 배정하고\n시간을 진행해 회복시키세요.","임시 보관함의 물건을 확인하세요.\n가방에 챙긴 물건만 들고 나갑니다.","출입구에서 첫 외출을 준비하세요.\n목재·고철을 가져와 시설을 복구하세요."};
  public bool Allows(int step)=>Step>=step;
  public void Initialize(SettlementController c,bool restoring){owner=c;Action.onClick.AddListener(Act);Action.gameObject.SetActive(false);if(c.Campaign==null||restoring)return;Step=0;c.Campaign.BeginSettlementIntroduction();foreach(var m in c.CraftPanel.Materials)m.Initial=0;c.ActivityLog.Add("도착 · 빈 건물에 몸을 피했다. 망가진 시설은 외부에서 재료를 가져와 복구해야 한다.");}
  public void Restore(int step){Step=step>=6&&step<10?5:step;}
  public void Completed(string kind){if(Step==3&&kind=="rest" || Step==4&&kind=="stock")Next();}
  void Next(){Step++;owner.ActivityLog.Add(owner.Campaign.ClockText.Replace("\n"," ")+" · "+titles[Step]);}
  public void Act(){if(!owner||owner.Campaign==null||!owner.Main.interactable||owner.Campaign.Stage!=JourneyStage.Settlement||Step>2)return;int minutes=Step==2?20:10;if(Step==1){owner.CraftPanel.Materials.First(m=>m.Id=="cloth").Initial++;owner.CraftPanel.Materials.First(m=>m.Id=="water").Initial++;owner.ActivityLog.Add("현장 발견 · 헝겊 1개와 밀봉된 물 1개");}Next();owner.Campaign.AdvanceSettlementTime(minutes);owner.RefreshMembers();}
  void Set(Button b,bool show){if(b)b.gameObject.SetActive(show);}
  void LateUpdate(){
   if(!owner||owner.Campaign==null)return;
   if(Step==5&&owner.ReturnPanel.HasReport){Step=10;owner.ActivityLog.Add("첫 귀환 · 회수한 물자를 보관하고 시설을 복구하자.");owner.NoticeTitle.text="시설 복구 시작";owner.NoticeBody.text="회수품을 임시 보관함에 옮기세요.\n시설 복구에서 담당자를 배정하세요.";}
   bool main=owner.Main.gameObject.activeInHierarchy&&owner.Main.interactable&&owner.Campaign.Stage==JourneyStage.Settlement;
   Action.gameObject.SetActive(main&&Step<=2);if(!main)return;
   Set(owner.Bed,Step>=3);Set(owner.Cabinet,Step>=4);Set(owner.Workbench,Step>=5);Set(owner.Stock,Step>=5);Set(owner.Exit,Step>=5);
   Set(owner.Advance,Step>=3);Set(owner.Journal,Step>=4);Set(owner.VisitorPanel.OpenButton,Step>=10);Set(owner.CraftPanel.HousingButton,Step>=10);Set(owner.WorkPanel.Improve,Step>=5);Set(owner.CookingPanel.Improve,Step>=5);
   foreach(var m in owner.Members)if(m.BagButton)m.BagButton.gameObject.SetActive(Step>=4);
   if(Step>=10)return;owner.NoticeTitle.text=(Step+1)+" · "+titles[Step];owner.NoticeBody.text=hints[Step];ActionLabel.text=Step==0?"주변 확인 · 10분":Step==1?"남은 상자 수색 · 10분":"바닥 잠자리 정리 · 20분";
  }
 }
}
