using System;
using System.Linq;
using UnityEngine;
namespace Live49.Core
{
    [Serializable] public sealed class SearchProgress
    {
        public bool started,focus,slow,secured,noise;
        public string place,phase="playing",reason="",loot="";
        public int point,checks,great,miss;
        public float risk,noiseRoll;
        public float[] rolls,schedule,centers;
    }
    // Optional loot rolls are fixed before play. Required supplies and map discovery never depend on timing.
    public static class SearchSession
    {
        public static bool Active(JourneyState s)=>s?.search!=null&&s.search.started;
        public static string Label(string place,int point)=>place=="L1"?(point==0?"냉장고 · 생수":"식품 선반 · 식량"):PlaceInspection.Points(place)[point].Label;
        public static bool Checked(JourneyState s,string place,int point)=>place=="L1"?s.Has(point==0?"water_checked":"food_checked"):PlaceInspection.Checked(s,place,point);
        static string Used(string place,int point)=>"search.used."+place+"."+point;
        public static bool FocusAvailable(JourneyState s,string place,int point)=>!s.Has(Used(place,point));
        public static bool CanBegin(JourneyState s,string place,int point)=>s!=null&&!Active(s)&&!BanditEncounter.Active(s)&&string.IsNullOrEmpty(s.weekEvent)&&!CampLife.Busy(s)&&s.day>0&&RegionExploration.Outside(s)&&RegionExploration.PlayerPlace(s)==place&&RegionExploration.Discovered(s,place)&&point>=0&&point<(place=="L1"?2:PlaceInspection.Points(place).Length)&&!Checked(s,place,point);
        public static bool Begin(JourneyState s,string place,int point,bool focus,bool slow=false,Func<float> random=null)
        {
            if(!CanBegin(s,place,point)||(focus&&!FocusAvailable(s,place,point)))return false;
            random=random??(()=>UnityEngine.Random.value);
            float R()=>Mathf.Clamp(random(),0,.999999f);
            int.TryParse(s.Value("search.caution."+RegionTravel.Current(s)),out int caution);
            s.search=new SearchProgress{started=true,place=place,point=point,focus=focus,slow=slow,risk=Math.Min(.65f,(place=="L1"?(point==0?.25f:.18f):.25f)+caution*.1f),noiseRoll=R(),rolls=new[]{R(),R()},schedule=new[]{1.8f+R()*.5f,5.4f+R()*.5f,10+R()*.5f},centers=new[]{.5f+R()*.22f,.5f+R()*.22f,.5f+R()*.22f}};
            s.minutes+=focus?10:5;
            if(!focus){Secure(s);Finish(s,"complete");}
            return true;
        }
        public static string Grade(float position,float center,bool slow)
        {float d=Math.Abs(position-center);return d<=.035f?"great":d<=(slow?.16f:.11f)?"good":"miss";}
        public static bool Check(JourneyState s,int index,string grade)
        {
            if(!Active(s))return false;var p=s.search;
            if(!p.focus||p.phase!="playing"||index!=p.checks||index>2||(index==2&&!p.secured)||(grade!="great"&&grade!="good"&&grade!="miss"))return false;
            p.checks++;if(grade=="great")p.great++;if(grade=="miss"){p.miss++;p.risk=Math.Min(.95f,p.risk+.25f);}return true;
        }
        static void Secure(JourneyState s)
        {
            var p=s.search;if(p.secured)return;
            var before=RegionExploration.Visible(s);
            if(p.place=="L1")
            {
                bool water=p.point==0;s.Add(water?"water":"packaged_food",water?"생수":"포장 식량",2);
                s.Set(water?"water_checked":"food_checked");p.loot=(water?"생수":"포장 식량")+" 2개 확보";JourneyDayLog.Visit(s,p.place);
                JourneyDayLog.Found(s,RegionExploration.Visible(s).Except(before).ToArray());
            }
            else
            {
                if(!PlaceInspection.Inspect(s,p.place,p.point,out _))throw new InvalidOperationException("Search checkpoint no longer matches location");
                s.minutes-=5; // The search paid its full cost at Begin/Continue.
                p.loot=p.place=="L2"&&p.point==0?"공용 연료 2 확보":"주변 흔적과 길 확인";
            }
            p.secured=true;
            var revealed=RegionExploration.Visible(s).Except(before).ToArray();
            if(revealed.Length>0)p.loot+="\n열린 길 · "+string.Join(" · ",revealed.Select(id=>RegionExploration.Find(id).ShortName));
        }
        public static bool Checkpoint(JourneyState s)
        {if(!Active(s)||s.search.phase!="playing"||s.search.checks!=2||s.search.secured)return false;Secure(s);s.search.phase="checkpoint";return true;}
        public static bool Continue(JourneyState s)
        {if(!Active(s)||s.search.phase!="checkpoint")return false;s.minutes+=5;s.search.phase="playing";return true;}
        static void Extras(JourneyState s)
        {
            var p=s.search;float bonus=Math.Min(.2f,p.great*.1f);
            string[] ids=p.place=="L1"?(p.point==0?new[]{"water","ready_meal"}:new[]{"packaged_food","ingredients"}):p.place=="L4"?new[]{"repair_parts","cloth"}:new[]{"cloth","ingredients"};
            string[] names=p.place=="L1"?(p.point==0?new[]{"추가 생수","간편식"}:new[]{"추가 포장 식량","식재료"}):p.place=="L4"?new[]{"수리 부품","천"}:new[]{"천","식재료"};
            bool found=false;
            for(int i=0;i<2;i++)if(p.rolls[i]<(i==0?.45f:.3f)+bonus){s.Add(ids[i],names[i],1,ids[i]=="cloth"||ids[i]=="repair_parts"?1:0);p.loot+="\n"+names[i]+" 1개 확보";found=true;}
            if(!found)p.loot+="\n추가로 쓸 만한 물자는 없었어요.";
        }
        public static bool Finish(JourneyState s,string reason)
        {
            if(!Active(s))return false;var p=s.search;
            if(p.phase!="playing"&&p.phase!="checkpoint")return false;
            if(reason!="complete"&&reason!="stopped"&&reason!="interrupted")return false;
            if(reason=="complete"&&p.focus&&(!p.secured||p.checks!=3))return false;
            if(reason=="complete")Extras(s);
            p.phase="result";p.reason=reason;p.noise=p.noiseRoll<p.risk;s.Set(Used(p.place,p.point));
            if(!p.secured)p.loot="확보한 물자는 없어요. 이 지점은 빠른 탐색으로 다시 확인할 수 있어요.";
            JourneyDayLog.Activity(s,Label(p.place,p.point)+" 탐색");return true;
        }
        public static bool Acknowledge(JourneyState s)
        {if(!Active(s)||s.search.phase!="result")return false;s.search.phase=s.search.noise?"warning":"closed";if(!s.search.noise)s.search=null;return true;}
        public static bool Decide(JourneyState s,bool stay)
        {if(!Active(s)||s.search.phase!="warning")return false;if(stay){string key="search.caution."+RegionTravel.Current(s);int.TryParse(s.Value(key),out int n);s.Set(key,Math.Min(5,n+1).ToString());}s.search=null;return true;}
        static bool Unit(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v)&&v>=0&&v<=1;
        public static bool Valid(JourneyState s)
        {
            var p=s.search;if(p==null||!p.started)return true;
            return s.day>0&&RegionExploration.Outside(s)&&RegionExploration.PlayerPlace(s)==p.place&&RegionExploration.Find(p.place)!=null&&p.point>=0&&p.point<(p.place=="L1"?2:PlaceInspection.Points(p.place).Length)
                &&new[]{"playing","checkpoint","result","warning"}.Contains(p.phase)&&p.checks>=0&&p.checks<=3&&p.great>=0&&p.miss>=0&&p.great+p.miss<=p.checks&&Unit(p.risk)&&Unit(p.noiseRoll)
                &&p.rolls!=null&&p.rolls.Length==2&&p.rolls.All(Unit)&&p.centers!=null&&p.centers.Length==3&&p.centers.All(v=>Unit(v)&&v>=.5f&&v<=.72f)
                &&p.schedule!=null&&p.schedule.Length==3&&p.schedule.Select((v,i)=>!float.IsNaN(v)&&v>=(i==0?1.8f:i==1?5.4f:10)&&v<=(i==0?2.3f:i==1?5.9f:10.5f)).All(v=>v)
                &&p.loot!=null&&p.reason!=null&&(!p.secured||Checked(s,p.place,p.point))&&(p.phase!="checkpoint"||(p.focus&&p.secured&&p.checks==2));
        }
    }
}
