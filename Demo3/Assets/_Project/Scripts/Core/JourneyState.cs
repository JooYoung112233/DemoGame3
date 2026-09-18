using System;
using System.Collections.Generic;
using System.Linq;
using Live49.UI;
using UnityEngine;

namespace Live49.Core
{
    [Serializable] public sealed class JourneyItem
    {
        public string id, name, description;
        public int quantity, category;
    }
    [Serializable] public sealed class JourneyFlag { public string key, value; }
    [Serializable] public sealed class JourneyState
    {
        public int version=1, day, minutes;
        // Implementation tuning, not final economy: initial travel reserve 3, first route costs 1.
        public int fuel=3;
        public string location="camper", node="", journal="", savedUtc="";
        public string regionId=RegionExploration.RegionId, exploringPlace="";
        public int line;
        public string weekEvent="";
        public int weekLine;
        public CampLifeState life;
        public CookingProgress cooking;
        public BanditEncounterState bandit;
        public SearchProgress search;
        public bool answeredSoi, bookSeen, inventoryKnown, inStore, cookingUnlocked;
        public List<JourneyItem> items=new List<JourneyItem>();
        public List<JourneyFlag> flags=new List<JourneyFlag>();
        public List<string> done=new List<string>();
        public List<JourneyDayRecord> days=new List<JourneyDayRecord>();
        public bool Has(string key)=>flags.Any(f=>f.key==key && f.value!="false" && !string.IsNullOrEmpty(f.value));
        public string Value(string key)=>flags.FirstOrDefault(f=>f.key==key)?.value??"";
        public void Set(string key,string value="true")
        { var flag=flags.FirstOrDefault(f=>f.key==key);if(flag==null)flags.Add(new JourneyFlag{key=key,value=value});else flag.value=value; }
        public int Count(string id)=>items.FirstOrDefault(i=>i.id==id)?.quantity??0;
        public void Add(string id,string name,int amount,int category=0,string description="")
        {
            if(amount<=0)throw new ArgumentOutOfRangeException(nameof(amount));
            var item=items.FirstOrDefault(i=>i.id==id);
            if(item==null){item=new JourneyItem{id=id,name=name,category=category,description=description};items.Add(item);}
            item.quantity=checked(item.quantity+amount);inventoryKnown=true;JourneyDayLog.Item(this,id,name,amount);
        }
        public bool Spend(params (string id,int amount)[] costs)
        {
            var grouped=costs.GroupBy(c=>c.id).Select(g=>(id:g.Key,amount:g.Sum(c=>c.amount))).ToArray();
            if(costs.Any(c=>c.amount<0)||grouped.Any(c=>Count(c.id)<c.amount))return false;
            foreach(var cost in grouped)if(cost.amount>0)items.First(i=>i.id==cost.id).quantity-=cost.amount;
            return true;
        }
        public BagContents Bag(string suhyeok="수혁",string soi="소이")
        {
            var bag=BagContents.Opening(suhyeok,soi);bag.InventoryKnown=inventoryKnown;
            bag.Items=items.Select(i=>new BagContents.Item{Id=i.id,Name=i.name,Description=i.description,Quantity=i.quantity,Group=(BagContents.Category)i.category}).ToArray();
            return bag;
        }
        public bool Valid()=>version==1 && day>=0 && day<=49 && minutes>=0 && fuel>=0 && fuel<=1000000 && line>=0 && line<=500
            && (location=="camper"||RegionExploration.Find(location)!=null) && node!=null && journal!=null && items!=null && flags!=null && done!=null
            && items.Count<=1000 && flags.Count<=1000 && done.Count<=1000
            && items.All(i=>i!=null&&!string.IsNullOrWhiteSpace(i.id)&&i.name!=null&&i.quantity>=0&&i.quantity<1000000&&i.category>=0&&i.category<3)
            && items.Select(i=>i.id).Distinct().Count()==items.Count
            && flags.All(f=>f!=null&&!string.IsNullOrEmpty(f.key)&&f.value!=null)
            && flags.Select(f=>f.key).Distinct().Count()==flags.Count && done.All(d=>d!=null)
            && (string.IsNullOrEmpty(regionId)||regionId==RegionExploration.RegionId||regionId==RegionTravel.NextRegion)
            && (location=="camper"?RegionTravel.Current(this)==RegionExploration.RegionId:RegionExploration.Find(location).Region==RegionTravel.Current(this))
            && JourneyDayLog.Valid(days) && CampLife.Valid(this) && Chapter00.FirstWeekStory.Valid(this) && BanditEncounter.Valid(this) && SearchSession.Valid(this) && (location=="camper"||RegionExploration.Discovered(this,location))
            && (!inStore||(day>0&&location!="camper"&&RegionTravel.Current(this)==RegionExploration.RegionId&&string.IsNullOrEmpty(exploringPlace)))
            && (string.IsNullOrEmpty(exploringPlace)||(day>0&&location!="camper"&&!inStore&&exploringPlace!="L1"&&RegionExploration.Discovered(this,exploringPlace)&&RegionExploration.Find(exploringPlace).Region==RegionTravel.Current(this)));
    }

    // Port of the existing cooking prototype. Economy units remain explicit tuning values.
    public sealed class CookingSession
    {
        public enum Phase { Ready, Warming, Simmering, Done }
        public Phase Stage { get=>(Phase)_progress.stage; private set=>_progress.stage=(int)value; }
        public float Heat { get=>_progress.heat; private set=>_progress.heat=value; }
        public float Elapsed { get=>_progress.elapsed; private set=>_progress.elapsed=value; }
        public float Simmer { get=>_progress.simmer; private set=>_progress.simmer=value; }
        public int Level { get=>_progress.level; private set=>_progress.level=value; }
        public bool Paused { get=>_progress.paused; set=>_progress.paused=value; }
        public bool Great { get=>_progress.great; private set=>_progress.great=value; }
        public bool Active=>Stage==Phase.Warming||Stage==Phase.Simmering;
        public const int GasCost=1, FoodCost=1, Minutes=25;
        float _good {get=>_progress.good;set=>_progress.good=value;}
        float _spill {get=>_progress.spill;set=>_progress.spill=value;}
        bool _boiled {get=>_progress.boiled;set=>_progress.boiled=value;}
        readonly CookingProgress _progress;
        readonly JourneyState _state;
        public CookingSession(JourneyState state)
        {
            _state=state;
            if(state.cooking==null||state.cooking.stage==3)state.cooking=new CookingProgress();
            _progress=state.cooking;if(Active)Paused=true;
        }
        public string BlockReason()=>!CampLife.Home(_state)?"캠핑카에서 요리해요.":CampLife.Heating(_state)?"전자레인지의 가열을 먼저 마쳐요.":!_state.cookingUnlocked?"요리대를 먼저 손봐야 해요.":_state.Count("gas")<GasCost?"조리에 쓸 가스가 부족해요.":_state.Count("ingredients")<FoodCost?"식재료가 부족해요.":null;
        public bool Start()
        {
            if(Stage!=Phase.Ready||BlockReason()!=null)return false;
            if(!_state.Spend(("gas",GasCost),("ingredients",FoodCost)))return false;
            _state.minutes+=Minutes;Stage=Phase.Warming;return true;
        }
        public void Select(int level){if(Active&&!Paused&&level>=1&&level<=3)Level=level;}
        public void Tick(float delta)
        {
            if(!Active||Paused||float.IsNaN(delta)||float.IsInfinity(delta)||delta<=0)return;
            float dt=Mathf.Min(.1f,delta);Elapsed+=dt;
            Heat+=((Level==1?55:Level==2?84:120)-Heat)*(1-Mathf.Exp(-dt/2.8f));
            if(Heat>=78)_boiled=true;
            if(Stage==Phase.Warming){if((_boiled&&Elapsed>=5)||Elapsed>=10)Stage=Phase.Simmering;return;}
            Simmer+=dt;if(Heat>=62&&Heat<=78)_good+=dt;if(Heat>86)_spill+=dt;
            if(Simmer<10)return;
            Stage=Phase.Done;Level=0;Great=_boiled&&_good>=6&&_spill<1.2f;
            _state.Add("meal","따뜻한 식사",1,0,"조리를 마친 한 끼. 가방에 담아 두었어요.");
            _state.Set("meal_cooked");
            JourneyDayLog.Activity(_state,"냄비로 만든 따뜻한 식사");
        }
    }
}
