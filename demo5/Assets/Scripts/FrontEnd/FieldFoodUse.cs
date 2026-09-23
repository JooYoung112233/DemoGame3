using System;
using System.Linq;
using Demo5.NightRun;
namespace Demo5.FrontEnd {
 public sealed partial class SettlementInventoryPanel {
  public string FieldUseBlock(Adventurer person,string id){
   var a=owner.ArrivalPanel;var item=Items.FirstOrDefault(i=>i.Id==id);
   if(item==null||!item.FieldUsable||item.Recovery<=0||item.UseCost<1)return "현장에서 사용할 수 없습니다.";
   if(!owner.Campaign.IsFieldExpedition||!a.IsOpen||!a.FieldBags.IsOpen||a.InTransit||a.Popup.activeSelf||a.Encounter.IsOpen||a.Search.IsOpen||a.Loot.IsOpen)return "지금은 사용할 수 없습니다.";
   if(person==null||!a.Participants.Contains(person)||person.Health<=0)return "행동 가능한 대원만 사용할 수 있습니다.";
   if(person.Health>=person.MaxHealth)return "체력이 가득 찼습니다.";
   if(CountFor(person,id)<item.UseCost)return "가방에 물건이 부족합니다.";
   return null;
  }
  public bool UseFieldFood(Adventurer person,string id)=>id=="ration"&&UseFieldItem(person,id);
  public bool UseFieldItem(Adventurer person,string id){
   if(FieldUseBlock(person,id)!=null)return false;var item=Items.First(i=>i.Id==id);
   Bag(person)[id]=CountFor(person,id)-item.UseCost;person.Health=Math.Min(person.MaxHealth,person.Health+item.Recovery);
   owner.ArrivalPanel.Rooms.SpendSearchTurn(0);owner.ArrivalPanel.RefreshFieldBags();return true;
  }
 }
}
