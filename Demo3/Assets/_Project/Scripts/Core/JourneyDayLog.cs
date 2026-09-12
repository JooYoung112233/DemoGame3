using System;
using System.Collections.Generic;
using System.Linq;
namespace Live49.Core
{
    [Serializable] public sealed class JourneyDayRecord
    {
        public int day,fuel;
        public List<string> visited=new List<string>(),found=new List<string>();
        public List<JourneyItem> obtained=new List<JourneyItem>();
        public List<string> activities=new List<string>();
    }
    public static class JourneyDayLog
    {
        static JourneyDayRecord Today(JourneyState state)
        {
            if(state.days==null)state.days=new List<JourneyDayRecord>();
            var record=state.days.FirstOrDefault(d=>d.day==state.day);
            if(record==null){record=new JourneyDayRecord{day=state.day};state.days.Add(record);}return record;
        }
        public static void Visit(JourneyState state,string id){if(state.day<=0)return;var list=Today(state).visited;if(!list.Contains(id))list.Add(id);}
        public static void Found(JourneyState state,IEnumerable<string> ids)
        {
            if(state.day<=0)return;foreach(var id in ids){var list=Today(state).found;if(!list.Contains(id))list.Add(id);state.Set("map.new."+id);}
        }
        public static void Item(JourneyState state,string id,string name,int amount)
        {
            if(state.day<=0)return;var list=Today(state).obtained;var item=list.FirstOrDefault(i=>i.id==id);
            if(item==null){item=new JourneyItem{id=id,name=name,description=""};list.Add(item);}item.quantity+=amount;
        }
        public static void Fuel(JourneyState state,int amount){if(state.day>0)Today(state).fuel+=amount;}
        public static void Activity(JourneyState state,string title)
        {if(state.day<=0)return;var d=Today(state);if(d.activities==null)d.activities=new List<string>();if(!d.activities.Contains(title))d.activities.Add(title);}
        static string Names(IEnumerable<string> ids)=>string.Join(" · ",ids.Select(id=>RegionExploration.Find(id)?.ShortName??id));
        public static string Summary(JourneyState state,int? day=null)
        {
            var record=state.days?.FirstOrDefault(d=>d.day==(day??state.day));
            if(record==null)return "방문한 곳  ·  아직 없어요\n\n새로 찾은 길  ·  아직 없어요\n\n챙긴 물품  ·  아직 없어요";
            var items=record.obtained.Select(i=>i.name+" "+i.quantity).ToList();if(record.fuel>0)items.Add("차량 연료 "+record.fuel);
            return "방문한 곳  ·  "+(record.visited.Count==0?"아직 없어요":Names(record.visited))+"\n\n새로 찾은 길  ·  "+(record.found.Count==0?"아직 없어요":Names(record.found))+"\n\n챙긴 물품  ·  "+(items.Count==0?"아직 없어요":string.Join(" · ",items));
        }
        public static bool Valid(List<JourneyDayRecord> days)=>days==null||(days.Count<=49&&days.All(d=>d!=null&&d.day>0&&d.day<=49&&d.fuel>=0&&d.fuel<=1000000&&d.visited!=null&&d.found!=null&&d.obtained!=null&&d.visited.Count<=100&&d.found.Count<=100&&d.obtained.Count<=100&&d.visited.All(id=>RegionExploration.Find(id)!=null)&&d.found.All(id=>RegionExploration.Find(id)!=null)&&d.obtained.All(i=>i!=null&&!string.IsNullOrEmpty(i.id)&&i.name!=null&&i.quantity>=0&&i.quantity<=1000000))&&days.Select(d=>d.day).Distinct().Count()==days.Count);
    }
}
