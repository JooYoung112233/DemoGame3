using System;
namespace Live49.Core
{
    [Serializable] public sealed class BanditEncounterState
    {
        // 0: choosing, 1: result, 2: awaiting return, 3: return report, 4: finished.
        public int stage, day;
        public bool started;
        public string place="L8", outcome="";
    }
    public static class BanditEncounter
    {
        public const int GiveMinutes=5, DetourMinutes=20;
        public static bool Started(JourneyState s)=>s?.bandit!=null&&s.bandit.started;
        public static bool Active(JourneyState s)=>Started(s)&&(s.bandit.stage==0||s.bandit.stage==1||s.bandit.stage==3);
        static bool AtSite(JourneyState s)=>s.day>0&&RegionTravel.Current(s)==RegionExploration.RegionId&&s.exploringPlace=="L8"&&!s.inStore&&RegionExploration.Discovered(s,"L8");
        public static bool Eligible(JourneyState s)=>s!=null&&!Started(s)&&!SearchSession.Active(s)&&AtSite(s)&&RegionExploration.Completed(s,"L1")&&s.Has(RegionTravel.Drawing)&&string.IsNullOrEmpty(s.weekEvent)&&!CampLife.Busy(s);
        public static bool Begin(JourneyState s)
        {if(!Eligible(s))return false;s.bandit=new BanditEncounterState{day=s.day,started=true};return true;}
        public static string Block(JourneyState s,string choice)
        {
            if(!Started(s)||s.bandit.stage!=0||!AtSite(s))return "지금은 선택할 수 없어요.";
            if(choice!="food"&&choice!="water"&&choice!="detour")return "선택을 확인해주세요.";
            if(choice=="food"&&s.Count("packaged_food")<1)return "포장 식량이 없어요.";
            if(choice=="water"&&s.Count("water")<1)return "생수가 없어요.";
            return null;
        }
        public static bool Choose(JourneyState s,string choice)
        {
            if(Block(s,choice)!=null)return false;
            if(choice!="detour"&&!s.Spend((choice=="food"?"packaged_food":"water",1)))return false;
            s.minutes+=choice=="detour"?DetourMinutes:GiveMinutes;
            s.bandit.outcome=choice;s.bandit.stage=1;
            JourneyDayLog.Activity(s,choice=="detour"?"주차장 골목에서 우회":choice=="food"?"길을 막은 사람들에게 포장 식량 전달":"길을 막은 사람들에게 생수 전달");
            return true;
        }
        public static bool FinishResult(JourneyState s)
        {if(!Started(s)||s.bandit.stage!=1||!AtSite(s))return false;s.bandit.stage=2;return true;}
        public static bool BeginReturn(JourneyState s)
        {if(!Started(s)||s.bandit.stage!=2||!CampLife.Home(s)||!string.IsNullOrEmpty(s.weekEvent))return false;s.bandit.stage=3;return true;}
        public static bool FinishReturn(JourneyState s)
        {if(!Started(s)||s.bandit.stage!=3||!CampLife.Home(s))return false;s.bandit.stage=4;JourneyDayLog.Activity(s,"골목 조우 뒤 캠핑카 주변과 소지품 확인");return true;}
        public static string Result(JourneyState s)=>s.bandit.outcome=="food"?"포장 식량 1개를 건넸다. 두 사람이 옆으로 물러나자 골목을 지나갔다.\n\n포장 식량 −1  ·  5분 경과":s.bandit.outcome=="water"?"생수 1개를 건넸다. 두 사람과 거리를 유지하며 골목을 빠져나왔다.\n\n생수 −1  ·  5분 경과":"두 사람에게 다가가지 않고 왔던 길로 물러났다. 돌아가는 길로 주차장에 도착했다.\n\n물자 소모 없음  ·  20분 경과";
        public static bool Valid(JourneyState s)
        {
            var b=s.bandit;if(b==null)return true;
            // JsonUtility materializes an empty nested class even when the source field is null.
            if(!b.started)return b.stage==0&&b.day==0&&string.IsNullOrEmpty(b.outcome)&&(string.IsNullOrEmpty(b.place)||b.place=="L8");
            if(b.stage<0||b.stage>4||b.day<1||b.day>s.day||b.place!="L8"||b.outcome==null)return false;
            if(b.stage==0?b.outcome!="":b.outcome!="food"&&b.outcome!="water"&&b.outcome!="detour")return false;
            if(b.stage<=1&&(!AtSite(s)||b.day!=s.day||!RegionExploration.Completed(s,"L1")||!s.Has(RegionTravel.Drawing)))return false;
            if(b.stage==3&&!CampLife.Home(s))return false;
            return !Active(s)||(string.IsNullOrEmpty(s.weekEvent)&&!CampLife.Busy(s));
        }
    }
}
