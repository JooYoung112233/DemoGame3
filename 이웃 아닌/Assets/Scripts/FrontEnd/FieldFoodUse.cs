using System;
using System.Linq;
using Demo5.NightRun;
namespace Demo5.FrontEnd {
 public sealed partial class SettlementInventoryPanel {
  public int ItemRecovery(Adventurer person,string id){var item=Items.FirstOrDefault(i=>i.Id==id);return item==null?0:person?.Traits.ItemRecovery(item.Recovery,id)??Math.Max(0,item.Recovery);}
  public string FieldUseBlock(Adventurer person,string id){
   var a=owner.ArrivalPanel;var item=Items.FirstOrDefault(i=>i.Id==id);
   if(item==null||!item.FieldUsable||item.Recovery<=0||item.UseCost<1)return "현장에서 사용할 수 없습니다.";
   if(!owner.Campaign.IsFieldExpedition||!a.IsOpen||!a.FieldBags.IsOpen||a.InTransit||a.Popup.activeSelf||a.Encounter.IsOpen||a.Search.IsOpen||a.Loot.IsOpen)return "지금은 사용할 수 없습니다.";
   return FieldItemBlock(person,id);
  }
  // What stops this member using the item at all, whatever window is open (the bag window's use, and the member's queued use when
  // '턴 진행' resolves: FieldTurnPlanner.QueueUse · IFieldUseFacts). Null = it can be used.
  public string FieldItemBlock(Adventurer person,string id){
   var a=owner.ArrivalPanel;var item=Items.FirstOrDefault(i=>i.Id==id);
   if(item==null||!item.FieldUsable||item.Recovery<=0||item.UseCost<1)return "현장에서 사용할 수 없습니다.";
   if(person==null||!a||!a.Participants.Contains(person)||person.Health<=0)return "행동 가능한 대원만 사용할 수 있습니다.";
   if(person.Health>=person.MaxHealth)return "체력이 가득 찼습니다.";
   if(CountFor(person,id)<item.UseCost)return "가방에 물건이 부족합니다.";
   return null;
  }
  public bool UseFieldFood(Adventurer person,string id)=>id=="ration"&&UseFieldItem(person,id);
  // Use now and spend one turn (no site board to plan on: FieldTurnPlanner.Placing is off). On the board the bag window queues the use
  // as the member's action for the next '턴 진행' instead (ExpeditionBagPanel → FieldTurnPlanner.QueueUse → ApplyFieldItem).
  public bool UseFieldItem(Adventurer person,string id){
   if(FieldUseBlock(person,id)!=null)return false;ApplyItem(person,id);
   owner.ArrivalPanel.Rooms.SpendSearchTurn(0);owner.ArrivalPanel.RefreshFieldBags();return true;
  }
  // The member's queued use, applied while '턴 진행' resolves (the turn itself is the planner's): the bag loses the cost, health rises.
  public bool ApplyFieldItem(Adventurer person,string id){if(FieldItemBlock(person,id)!=null)return false;ApplyItem(person,id);return true;}
  void ApplyItem(Adventurer person,string id){var item=Items.First(i=>i.Id==id);Bag(person)[id]=CountFor(person,id)-item.UseCost;person.Health=Math.Min(person.MaxHealth,person.Health+ItemRecovery(person,id));}
 }
}
