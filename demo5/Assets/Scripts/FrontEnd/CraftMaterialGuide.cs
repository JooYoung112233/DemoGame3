using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 public sealed class CraftMaterialGuide:MonoBehaviour {
  public Image Icon;public Text Title,Counts,Source,ActionLabel;public Button Action;
  SettlementController owner;SettlementCraftPanel craft;SettlementCraftPanel.Recipe recipe;int quantity;string focus;int bagIndex=-1;string craftId;bool search;
  public string FocusId=>focus;
  void Awake(){Action.onClick.AddListener(Go);}
  public void Select(string id){focus=id;Refresh(owner,craft,recipe,quantity);}
  public void Refresh(SettlementController o,SettlementCraftPanel c,SettlementCraftPanel.Recipe r,int q){
   owner=o;craft=c;recipe=r;quantity=q;bagIndex=-1;craftId=null;search=false;
   if(r==null||r.Costs.Length==0){Title.text="재료 안내";Counts.text="필요한 재료가 없습니다.";Source.text="";Icon.enabled=false;Action.interactable=false;return;}
   if(!r.Costs.Any(x=>x.MaterialId==focus))focus=(r.Costs.FirstOrDefault(x=>c.Available(x.MaterialId)<x.Count*q)??r.Costs[0]).MaterialId;
   var m=c.Materials.FirstOrDefault(x=>x.Id==focus);int need=r.Costs.Where(x=>x.MaterialId==focus).Sum(x=>x.Count)*q,have=c.Available(focus),missing=Mathf.Max(0,need-have);
   Title.text="재료 안내 · "+(m?.Name??focus);Icon.sprite=m?.Icon;Icon.enabled=Icon.sprite;Counts.text="사용 가능 "+have+" / 필요 "+need+" · 부족 "+missing;
   var people=o.Campaign.Party.ToArray();int held=people.Sum(p=>o.InventoryPanel.CountFor(p,focus));
   bool blocked=c.FacilityDone(r)||(r.Category!=0&&c.Orders.Any(x=>x.Recipe.Id==r.Id));
   if(blocked){Source.text="이미 완료했거나 진행 중인 시설입니다.";ActionLabel.text="진행 상태 확인";Action.interactable=false;return;}
   var condition=c.ConstructionBlock(r);if(condition!=null){Source.text=condition;ActionLabel.text="선행 조건 확인";Action.interactable=false;return;}
   if(missing==0){Source.text="이 재료는 충분합니다.\n나머지 재료와 담당자를 확인하세요.";ActionLabel.text="재료 준비 완료";Action.interactable=false;return;}
   if(held>0){bagIndex=System.Array.FindIndex(people,p=>o.InventoryPanel.CountFor(p,focus)>0);Source.text="개인 가방에 총 "+held+"개 보관 중\n제작에 쓰려면 창고로 옮겨주세요.";ActionLabel.text="가방 정리";}
   else if(c.Recipes.Any(x=>x.Id==focus&&x!=r)){craftId=focus;Source.text="제작대에서 만들 수 있는 재료입니다.\n제작법의 재료와 시간을 확인하세요.";ActionLabel.text="제작법 보기";}
   else if(o.ArrivalPanel.Loot.MaterialAvailability(focus,out int unfinished,out int remaining)){
    search=unfinished>0||remaining>0;
    Source.text=remaining>0?"폐상가 현장에 "+remaining+"개 남겨둠\n"+(unfinished>0?"추가 수색 가능 · 미완료 "+unfinished+"곳":"다시 방문해 남은 물건을 챙길 수 있습니다."):
     unfinished>0?"폐상가 · 미완료 수색 "+unfinished+"곳\n발견 가능 재료 · 획득은 확정이 아닙니다.":"폐상가의 수색 후보를 모두 확인했습니다.\n남은 재료 없음 · 다른 획득 경로가 필요합니다.";
    ActionLabel.text=search?"원정 계획":"남은 재료 없음";
   }
   else {Source.text="현재 알려진 획득처가 없습니다.";ActionLabel.text="획득처 미확인";}
   Action.interactable=bagIndex>=0||craftId!=null||search;
  }
  void Go(){if(!craft||!craft.IsOpen||craft.CancelPopup.activeSelf||!Action.interactable)return;if(bagIndex>=0){int index=bagIndex;craft.Close();owner.InventoryPanel.Open(index);}else if(craftId!=null)craft.FocusRecipe(craftId);else if(search){craft.Close();owner.ExpeditionPanel.Open();}}
 }
}

