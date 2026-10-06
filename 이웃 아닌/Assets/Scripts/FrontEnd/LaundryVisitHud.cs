using System.Collections.Generic;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
 // Shares the exploration HUD's papers, member cards and framed tray; no simulated combat turns.
 public sealed class LaundryVisitHud:MonoBehaviour
 {
  public Text Clock,Route,TimeNote,ContextTab,MemberCount;
  public RectTransform MemberContent;
  public ScrollRect MemberScroll;
  public ExpeditionMemberCard MemberPrefab;
  public readonly List<ExpeditionMemberCard> Cards=new List<ExpeditionMemberCard>();
  public void Bind(SettlementController owner,Adventurer[] people,bool reviewing)
  {
   foreach(var card in Cards){card.gameObject.SetActive(false);Destroy(card.gameObject);}Cards.Clear();
   foreach(var person in people){
    var card=Instantiate(MemberPrefab,MemberContent);var data=owner.Roster.Candidates.FirstOrDefault(c=>c.Id==CampaignPersistence.MemberId(owner,person));
    card.Name.text=person.Name;card.Portrait.sprite=data?.Portrait;card.Role.text=reviewing?"거점 대원":"대화 동행";
    card.Health.text=person.Health+" / "+person.MaxHealth;SegmentedHealthGraphic.Set(card.HealthFill,person.Health,person.MaxHealth);
    card.State.text="가방 "+owner.InventoryPanel.SlotsFor(person)+" / "+person.BagCapacity;
    card.Check.gameObject.SetActive(false);card.SetAction(null);card.Button.enabled=false;Cards.Add(card);
   }
   Clock.text=owner.Campaign.ClockText.Replace("\n","   ");
   Route.text=reviewing?"거점 · 표찰 주소 확인":"느티길 12 · 문 앞";
   TimeNote.text=reviewing?"주소 확인 · 시간 소모 없음":"대화 중 · 시간 정지";
   ContextTab.text=reviewing?"현재 조사":"현재 대화";
   MemberCount.text=people.Length+"명"+(people.Length>3?" · 옆으로 스크롤":"");
   Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(MemberContent);MemberScroll.horizontalNormalizedPosition=0;
  }
 }
}
