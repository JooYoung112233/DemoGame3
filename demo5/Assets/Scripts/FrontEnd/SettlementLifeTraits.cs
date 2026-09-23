using System;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
 public sealed partial class SettlementController
 {
  public PartyCandidate DataFor(Adventurer member){int i=Array.IndexOf(Campaign.Party.ToArray(),member);string id=PartySelectionSession.Selected.ElementAtOrDefault(i);return Roster.Candidates.FirstOrDefault(p=>p.Id==id);}
  public int LifeTimePercent(Adventurer member,bool cooking){var p=member==null?null:DataFor(member);int n=p==null?100:cooking?p.CookingTimePercent:p.CraftTimePercent;return n<=0?100:Mathf.Clamp(n,1,100);}
 }
 public static class LifeTraitTime
 {
  // Multiply both effects before a single whole-minute ceiling, using integer arithmetic.
  public static int Calculate(int minutes,int count,int facility,int worker){if(minutes<=0)return 0;long value=(long)minutes*count*facility*worker;return (int)Math.Max(1,(value+9999)/10000);}
  public static string Effects(int facility,int worker)=>"시설 "+(facility==100?"기본":"−"+(100-facility)+"%")+" · 담당 "+(worker==100?"기본":"−"+(100-worker)+"%");
  public static string Compare(int original,int actual)=>original==actual?"예상 시간  "+actual+"분":"시간  "+original+" → "+actual+"분";
 }
 public sealed partial class SettlementCookingPanel
 {
  public Text TraitEffect;
  public int WorkerPercent(Meal meal,Adventurer member)=>meal!=null&&meal.WorkerSpeedAllowed?owner.LifeTimePercent(member,true):100;
  public int DurationFor(Meal meal,int count,Adventurer member)=>LifeTraitTime.Calculate(meal.Minutes,count,owner.CraftPanel.CookerImproved?owner.CraftPanel.CookerTimePercent:100,WorkerPercent(meal,member));
  void RefreshTraitTime(){var person=worker>=0&&worker<people.Length&&people[worker].Health>0&&!owner.IsAssigned(people[worker])?people[worker]:null;int facility=owner.CraftPanel.CookerImproved?owner.CraftPanel.CookerTimePercent:100;Duration.text=selected==null?"":LifeTraitTime.Compare(selected.Minutes*quantity,DurationFor(selected,quantity,person));TraitEffect.text=LifeTraitTime.Effects(facility,WorkerPercent(selected,person));if(selected!=null&&!selected.WorkerSpeedAllowed)TraitEffect.text+=" · 특성 제외";}
 }
 public sealed partial class SettlementCraftPanel
 {
  public Text TraitEffect;
  public int DurationFor(Recipe recipe,int count,Adventurer member)=>LifeTraitTime.Calculate(recipe.Minutes,count,BenchImproved?80:100,owner.LifeTimePercent(member,false));
  void RefreshTraitTime(){var person=worker>=0&&worker<people.Length&&people[worker].Health>0&&!owner.IsAssigned(people[worker])?people[worker]:null;Duration.text=selected==null?"":LifeTraitTime.Compare(selected.Minutes*quantity,DurationFor(selected,quantity,person));TraitEffect.text=LifeTraitTime.Effects(BenchImproved?80:100,owner.LifeTimePercent(person,false));}
 }
}
