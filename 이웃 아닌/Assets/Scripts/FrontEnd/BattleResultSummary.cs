using System.Collections.Generic;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 public sealed class BattleResultSummary : MonoBehaviour {
  public Text Stats, Consequences, UsedEmpty, GainedEmpty;
  public RectTransform Members, Used, Gained;
  public GameObject MemberTemplate, ItemTemplate;
  [Header("결과 문구")]
  [Tooltip("사격이 있었을 때만 소음 뒤에 붙인다")] public string GunfireNote=" (총성 포함)";
  [Tooltip("출구(오락실)에서 물러나 거점으로 돌아갈 때 첫 줄. 탐험 턴을 쓰지 않는다")] public string HomeLine="거점으로 귀환";
  [Tooltip("첫 줄 뒤에 붙는 이동 시간. {0}에 편도 분")] public string HomeTravel="  ·  이동 {0}분";
  [Tooltip("출구에서 물러날 때 둘째 줄")] public string HomeBody="출구로 빠져나가 거점으로 돌아갑니다. 챙긴 물건은 그대로 가져갑니다.";
  readonly Dictionary<Adventurer,Dictionary<string,int>> before=new Dictionary<Adventurer,Dictionary<string,int>>();
  readonly List<GameObject> rows=new List<GameObject>();
  public void Capture(ExpeditionArrivalPanel arrival){before.Clear();foreach(var p in arrival.Participants)before[p]=arrival.Inventory.Items.ToDictionary(x=>x.Id,x=>arrival.Inventory.CountFor(p,x.Id));}
  // home: the retreat left through the exit and ends the expedition (travel minutes, no exploration turn). minutes < 0 hides the travel time.
  public void Show(ExpeditionBattlePanel battle,ExpeditionArrivalPanel arrival,Dictionary<Adventurer,int> health,string retreat,bool home=false,int minutes=-1){
   foreach(var row in rows){row.SetActive(false);Destroy(row);}rows.Clear();
   var s=battle.State;Stats.text="전투  "+s.Round+" 라운드     |     처치  "+s.Kills+"     |     사격  "+s.AmmoSpent+"발     |     아이템  "+s.ItemsUsed+"회";
   bool defeat=s.Outcome==FieldBattleOutcome.Defeat;
   Consequences.text=defeat?"원정대가 행동 불능 상태입니다. 이번 원정은 여기서 끝납니다."
    :home&&s.Outcome==FieldBattleOutcome.Retreated?HomeLine+(minutes>=0?string.Format(HomeTravel,minutes):"")+"\n"+HomeBody
    :"탐험 1턴 경과  ·  소음 +"+s.ResultNoise+(s.AmmoSpent>0?GunfireNote:"")+"\n"+(s.Outcome==FieldBattleOutcome.Retreated?retreat+"로 물러납니다. 기존 수색 물품과 진행도는 유지됩니다.":"기존 수색 물품과 진행도는 유지됩니다. 이어서 수색할 수 있습니다.");
   int used=0,gained=0;
   for(int i=0;i<arrival.Participants.Count;i++){
    var p=arrival.Participants[i];int delta=p.Health-health[p];var row=Instantiate(MemberTemplate,Members);row.SetActive(true);rows.Add(row);
    row.transform.Find("Name").GetComponent<Text>().text=p.Name;
    var portrait=row.transform.Find("Portrait").GetComponent<Image>();portrait.sprite=arrival.Cards[i].Portrait.sprite;
    row.transform.Find("Health").GetComponent<Text>().text="체력  "+health[p]+" → "+p.Health+" / "+p.MaxHealth;
    var change=row.transform.Find("Change").GetComponent<Text>();change.text=delta==0?"변화 없음":delta>0?"+"+delta+" 회복":delta+" 감소";change.color=delta<0?new Color(.65f,.2f,.13f):new Color(.22f,.4f,.26f);
    row.transform.Find("State").GetComponent<Text>().text=p.Health<=0?"중상 · 거점 휴식으로 회복":p.Health==1?"위험 · 회복 필요":p.Health<p.MaxHealth?"부상 · 행동 가능":"양호 · 행동 가능";
    SegmentedHealthGraphic.Set(row.transform.Find("Bar").GetComponent<Image>(),p.Health,p.MaxHealth);
    var personal=new List<string>();
    foreach(var item in arrival.Inventory.Items){int old=before.TryGetValue(p,out var bag)&&bag.TryGetValue(item.Id,out int n)?n:arrival.Inventory.CountFor(p,item.Id);int diff=arrival.Inventory.CountFor(p,item.Id)-old;if(diff==0)continue;
     var entry=Instantiate(ItemTemplate,diff<0?Used:Gained);entry.SetActive(true);rows.Add(entry);entry.transform.Find("Icon").GetComponent<Image>().sprite=item.Icon;
     entry.transform.Find("Name").GetComponent<Text>().text=item.Name;entry.transform.Find("Owner").GetComponent<Text>().text=p.Name;
     var amount=entry.transform.Find("Amount").GetComponent<Text>();amount.text=(diff>0?"+":"−")+Mathf.Abs(diff);amount.color=diff<0?new Color(.65f,.2f,.13f):new Color(.22f,.4f,.26f);
     if(diff<0){used++;personal.Add(item.Name+" "+(-diff));}else gained++;
    }
    row.transform.Find("Used").GetComponent<Text>().text=personal.Count==0?"사용한 물품 없음":"사용  "+string.Join(" · ",personal);
   }
   UsedEmpty.gameObject.SetActive(used==0);GainedEmpty.gameObject.SetActive(gained==0);
   UsedEmpty.text="사용한 물품 없음";GainedEmpty.text="이번 전투에서 획득한 물품 없음\n기존 수색 물품은 그대로 유지";
   foreach(var content in new[]{Members,Used,Gained}){Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(content);var scroll=content.GetComponentInParent<ScrollRect>();if(scroll){scroll.StopMovement();scroll.verticalNormalizedPosition=1;}}
  }
 }
}
