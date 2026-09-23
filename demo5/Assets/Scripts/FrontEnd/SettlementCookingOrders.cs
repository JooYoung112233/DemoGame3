using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Demo5.FrontEnd {
 public sealed partial class SettlementCookingPanel {
  void Register(){if(owner.Development&&!owner.Development.State.Cooker)return;if(owner.Introduction&&(!owner.Introduction.Allows(5)||!AvailableDuringIntro(selected)))return;
   if(owner.CraftPanel.UnderConstruction("upgrade-cooker")||!IsOpen||CancelPopup.activeSelf||owner.Campaign==null||selected==null||worker<0||worker>=people.Length||people[worker].Health<=0||owner.IsAssigned(people[worker]))return;
   if(quantity<1||quantity>99||selected.Minutes<0||selected.Servings<1||!owner.CraftPanel.Materials.Any(m=>m.Id==selected.OutputId))return;
   var reserved=selected.Costs.GroupBy(c=>c.MaterialId).ToDictionary(g=>g.Key,g=>g.Sum(c=>c.Count)*quantity);
   if(reserved.Count==0||reserved.Any(c=>c.Value<1||owner.CraftPanel.Available(c.Key)<c.Value))return;
   var order=new Order{Recipe=selected,Member=people[worker],Quantity=quantity,Minutes=DurationFor(selected,quantity,people[worker]),OutputId=selected.OutputId,OutputCount=selected.Servings*quantity,Reserved=reserved};
   orders.Add(order);owner.NoticeTitle.text="조리 예약";owner.NoticeBody.text=selected.Name+" ×"+quantity+"\n“"+owner.DataFor(people[worker])?.WorkLine+"”";
   if(order.Minutes==0){Complete(order);owner.ActivityLog.Add(owner.Campaign.ClockText.Replace("\n"," ")+" · "+Result(order));owner.NoticeTitle.text="조리 완료";}
   Close();owner.RefreshMembers();
  }
  string Result(Order o)=>o.Member.Name+" · "+o.Recipe.Name+" · "+o.OutputCount+"인분 완료";
  void Complete(Order o){foreach(var c in o.Reserved)owner.CraftPanel.Materials.First(m=>m.Id==c.Key).Initial-=c.Value;owner.CraftPanel.Materials.First(m=>m.Id==o.OutputId).Initial+=o.OutputCount;orders.Remove(o);if(owner.Introduction)owner.Introduction.Completed(o.Recipe.Id);}
  public List<string> AdvanceTime(int minutes){var result=new List<string>();if(minutes<=0)return result;foreach(var o in orders.ToArray()){o.Minutes=Math.Max(0,o.Minutes-minutes);if(o.Minutes>0)continue;Complete(o);result.Add(Result(o));}return result;}
  void RefreshOrders(){Clear(orderRows);foreach(var o in orders){var order=o;var r=Instantiate(OrderPrefab,OrderContent);r.Icon.sprite=o.Recipe.Icon;r.Title.text=o.Recipe.Name+" · "+o.OutputCount+"인분";r.Member.text=o.Member.Name;r.Status.text="예약 · "+o.Minutes+"분";r.Cancel.onClick.AddListener(()=>AskCancel(order));orderRows.Add(r);}PlanBody.gameObject.SetActive(orders.Count==0);Top(OrderScroll);}
  void AskCancel(Order o){if(!orders.Contains(o))return;pendingCancel=o;CancelMessage.text=o.Recipe.Name+" 예약을 취소할까요?\n\n예약 재료를 모두 돌려놓습니다.\n진행한 시간은 돌아오지 않습니다.";Workspace.interactable=Workspace.blocksRaycasts=false;CancelPopup.SetActive(true);EventSystem.current?.SetSelectedGameObject(CancelNo.gameObject);}
  void CancelOrder(){if(pendingCancel!=null)orders.Remove(pendingCancel);DismissCancel();RefreshOrders();Refresh();owner.RefreshMembers();}
  void DismissCancel(){pendingCancel=null;CancelPopup.SetActive(false);Workspace.interactable=Workspace.blocksRaycasts=true;EventSystem.current?.SetSelectedGameObject(Back.gameObject);}
  public void RestoreSaved(CampaignSaveData data){orders.Clear();foreach(var r in data.Cooking)orders.Add(new Order{Member=CampaignPersistence.Resolve(owner,r.MemberId),Recipe=Meals.First(m=>m.Id==r.RecipeId),Quantity=r.Quantity,Minutes=r.Minutes,OutputId=r.OutputId,OutputCount=r.OutputCount,Reserved=r.Reserved.ToDictionary(x=>x.Id,x=>x.Count)});}
 }
}

