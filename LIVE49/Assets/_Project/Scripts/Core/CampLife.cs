using System;
using System.Collections.Generic;
using System.Linq;
namespace Live49.Core
{
    [Serializable] public sealed class CampLifeState
    {
        public int battery=30;
        public float microwaveLeft;
        public string energyDestination="";
    }
    [Serializable] public sealed class CookingProgress
    {
        public int stage,level=2;
        public float heat=20,elapsed,simmer,good,spill;
        public bool boiled,great,paused;
    }
    public sealed class LifeChoice
    {
        public string Id,Title,Detail,Art,Block;
        public Action Apply;
    }
    // Tuning values are explicit. Story dialogue and music are owned by their existing data.
    public static class CampLife
    {
        public const int Capacity=100,Charge=40,MicrowaveCost=10;
        public static CampLifeState Data(JourneyState s)=>s.life??(s.life=new CampLifeState());
        public static bool Home(JourneyState s)=>s.day>0&&!RegionExploration.Outside(s);
        public static bool Cooking(JourneyState s)=>s.cooking!=null&&(s.cooking.stage==1||s.cooking.stage==2);
        public static bool Heating(JourneyState s)=>s.life!=null&&s.life.microwaveLeft>0;
        public static bool Busy(JourneyState s)=>Cooking(s)||Heating(s);
        static string At(JourneyState s)=>RegionExploration.Outside(s)?RegionExploration.PlayerPlace(s):"";
        static string Need(JourneyState s,string id,int n)=>s.Count(id)>=n?null:"필요한 물자가 부족해요.";
        static void Mark(JourneyState s,string id,string title,int minutes)
        {s.Set(id);s.minutes+=minutes;JourneyDayLog.Activity(s,title);}
        static string Done(JourneyState s,string id)=>s.Has(id)?"이미 마쳤어요.":null;
        static LifeChoice C(string id,string title,string detail,string art,string blocked,Action run)=>new LifeChoice{Id=id,Title=title,Detail=detail,Art=art,Block=blocked,Apply=run};
        public static List<LifeChoice> Choices(JourneyState s,string section)
        {
            var list=new List<LifeChoice>();if(s.day<=0)return list;var data=Data(s);bool home=Home(s);string place=At(s);
            if(home&&section=="meal")
            {
                foreach(string food in new[]{"packaged_food","meal"})
                {
                    string id=food;string title=id=="meal"?"따뜻한 식사 나누기":"포장 식량 나누기";
                    list.Add(C("eat."+id,title,"수혁이 두 자리의 식사를 준비해요.\n생수 1 + "+(id=="meal"?"따뜻한 식사":"포장 식량")+" 1 · 15분\n먹고 남는 재고는 가방에 보관해요.","E03-HUB-rations-shared",Done(s,"life.meal."+s.day)??Need(s,"water",1)??Need(s,id,1),()=>{s.Spend(("water",1),(id,1));s.Set("first_meal_shared");Mark(s,"life.meal."+s.day,"식탁에서 나눈 식사",15);}));
                }
                list.Add(C("microwave","간편식 데우기","간편식 1 + 배터리 10 · 5분\n가열이 끝나면 따뜻한 식사 1개가 생겨요.\n닫으면 멈추고 다시 열면 이어서 가열해요.","E17-HUB-warm-meal",Busy(s)?"진행 중인 조리를 먼저 마쳐요.":data.battery<MicrowaveCost?"배터리를 충전해주세요.":Need(s,"ready_meal",1),()=>{s.Spend(("ready_meal",1));data.battery-=MicrowaveCost;data.microwaveLeft=3;s.minutes+=5;}));
            }
            if(home&&section=="equipment")
            {
                list.Add(C("stove.inspect","요리대 상태 확인","불이 붙지 않는 부분과 연결부를 확인해요.\n5분 · 물자는 사용하지 않아요.","E04-HUB-stove-off",Done(s,"stove_inspected"),()=>Mark(s,"stove_inspected","요리대 상태 확인",5)));
                list.Add(C("stove.repair","요리대 복구","수리 부품 1 · 20분\n수리점의 보급품에서 부품을 챙길 수 있어요.\n복구 뒤 가스와 식재료로 직접 요리해요.","E17-HUB-cooking",s.cookingUnlocked?"요리대를 사용할 수 있어요.":!s.Has("stove_inspected")?"먼저 요리대를 살펴봐요.":Need(s,"repair_parts",1),()=>{s.Spend(("repair_parts",1));s.cookingUnlocked=true;Mark(s,"stove_repaired","요리대 복구",20);}));
                list.Add(C("manual.repair","수동 발전 장치 연결","수리점에서 챙긴 수동 발전 장치 1 · 15분\n연료 대신 시간을 써 최소 전력을 얻어요.","E04-HUB-stove-off",Done(s,"manual_generator_ready")??Need(s,"hand_generator",1),()=>{s.Spend(("hand_generator",1));Mark(s,"manual_generator_ready","수동 발전 장치 연결",15);}));
                list.Add(C("cabinet","찬장 고정","천 1 · 10분\n움직이는 물건 사이에 받침을 넣어요.","E18-HUB-cabinet-secured",Done(s,"cabinet_secured")??Need(s,"cloth",1),()=>{s.Spend(("cloth",1));Mark(s,"cabinet_secured","찬장 고정",10);}));
                if(s.Has("camera_received"))list.Add(C("pouch","카메라 주머니 만들기","천 1 · 10분\n카메라 보관용 주머니를 마련해요.","E21-HUB-camera-protected",Done(s,"camera_protected")??Need(s,"cloth",1),()=>{s.Spend(("cloth",1));Mark(s,"camera_protected","카메라 주머니 마련",10);}));
            }
            if(home&&section=="energy")
            {
                if(s.location=="camper"&&s.fuel==0)
                    list.Add(C("emergency.origin","출발 지점 주변에서 비상 연료 찾기","주변 수색 60분 · 공용 연료 +2\n첫 출발 전에 연료를 모두 썼을 때 이동을 다시 준비해요.",null,null,()=>{s.fuel+=2;JourneyDayLog.Fuel(s,2);Mark(s,"emergency_fuel_found","출발 지점 비상 연료 확보",60);}));
                int gain=Math.Min(Charge,Capacity-data.battery);
                var site=RegionExploration.Find(data.energyDestination);
                bool planned=site!=null&&site.Region==RegionTravel.Current(s)&&RegionExploration.Discovered(s,site.Id);
                string destination=!planned?"이동할 곳을 먼저 선택해요.":"선택한 곳 · "+site.ShortName+"\n"+(site.Id==s.location?"현재 주차한 장소예요.":s.fuel-1>=1?"발전 후 차량 이동 가능":"발전 후 차량 연료 부족");
                list.Add(C("generate","공용 연료로 충전","공용 연료 1 · 15분\n배터리 +"+gain+" / 남을 연료 "+Math.Max(0,s.fuel-1)+"\n\n"+destination+"\n\n가동 후 주변 반응을 확인해요.","E04-HUB-stove-off",s.fuel<1?"공용 연료가 부족해요.":gain==0?"배터리가 가득 찼어요.":null,()=>{s.fuel--;data.battery+=gain;s.Set("generator_noise_pending");Mark(s,"generator_used","발전기로 충전",15);}));
                list.Add(C("manual","손으로 최소 전력 충전","30분 · 배터리 +10 (최대 100)\n공용 연료는 사용하지 않아요.","E04-HUB-stove-off",!s.Has("manual_generator_ready")?"설비에서 수동 발전 장치를 먼저 연결해요.":data.battery==Capacity?"배터리가 가득 찼어요.":null,()=>{data.battery=Math.Min(Capacity,data.battery+10);Mark(s,"manual_generator_used","수동 발전",30);}));
                if(s.Has("generator_noise_pending"))list.Add(C("noise.wait","주변 소리 확인하며 기다리기","5분 · 물자 사용 없음\n발전을 마친 뒤 주변 반응을 살펴봐요.",null,null,()=>{s.Set("generator_noise_pending","false");Mark(s,"generator_noise_checked","발전 뒤 주변 확인",5);}));
            }
            if(section=="supplies"&&!home)
            {
                if(place=="L1")Supply(list,s,"L1","주방용 식품 챙기기","식재료 3 · 간편식 2",()=>{s.Add("ingredients","식재료",3);s.Add("ready_meal","간편식",2);});
                if(place=="L3")Supply(list,s,"L3","생활 재료 챙기기","가스 3 · 천 4",()=>{s.Add("gas","조리용 가스",3,1);s.Add("cloth","마른 천",4,1);});
                if(place=="L4")Supply(list,s,"L4","수리용 보급품 챙기기","수리 부품 2 · 수동 발전 장치 1",()=>{s.Add("repair_parts","수리 부품",2,1);s.Add("hand_generator","수동 발전 장치",1,1);});
                if(place=="L2"||place==RegionTravel.Entry)
                    list.Add(C("emergency.fuel","도보로 비상 연료 확보","공용 연료가 바닥났을 때 이용해요.\n도보 수색 60분 · 연료 +2\n같은 지역에서 차량 이동을 다시 준비해요.","E23-L2-fuel-collected",s.fuel>0?"공용 연료가 남아 있어요.":null,()=>{s.fuel+=2;JourneyDayLog.Fuel(s,2);Mark(s,"emergency_fuel_found","도보 비상 연료 확보",60);}));
                if(place=="L1")list.Add(C("emergency.food","남은 식수와 식량 찾기","먹을 물자가 모자랄 때 추가 수색해요.\n도보 수색 45분 · 생수 1 / 포장 식량 1\n확률 실패 없이 최소 식사를 확보해요.","E03-HUB-rations-shared",s.Count("water")>0&&(s.Count("packaged_food")>0||s.Count("meal")>0)?"한 끼를 나눌 물자가 남아 있어요.":null,()=>{s.Add("water","생수",1);s.Add("packaged_food","포장 식량",1);Mark(s,"emergency_food_found","추가 식수·식량 확보",45);}));
                if(place=="L5")Dog(list,s,false);
            }
            if(home&&section=="dog")Dog(list,s,true);
            if(home&&section=="evening")
            {
                foreach(var memory in Memories(s))
                {
                    var m=memory;list.Add(C("memory."+m.Key,m.Value,"오늘 겪은 일 가운데 한 가지를 남겨요.\n선택해도 물건이나 사진은 사라지지 않아요.\n하루에 한 장 · 기록 없이 쉬어도 괜찮아요.",m.Key=="item.first_travel_photo"?"@week-first-photo":m.Key=="item.first_drawing"?"@week-E06-complete":null,s.Has("memory."+s.day)?"오늘의 한 장을 이미 남겼어요.":null,()=>{s.Set("memory."+s.day,m.Key);s.Set("memory.title."+s.day,m.Value);}));
                }
            }
            return list;
        }
        static void Supply(List<LifeChoice> list,JourneyState s,string id,string title,string content,Action grant)
        {list.Add(C("supply."+id,title,content+"\n10분 · 이 보급품은 한 번만 챙길 수 있어요.",null,Done(s,"supply."+id),()=>{grant();Mark(s,"supply."+id,title,10);}));}
        static void Dog(List<LifeChoice> list,JourneyState s,bool home)
        {
            if(home)
            {
                if(!s.Has("dog_joined"))return;
                list.Add(C("dog.bed","함께 쉴 자리 마련","천 1 · 10분\n기존 러그 위에 깔개를 놓아요.\n자리가 없어도 동행이 취소되지는 않아요.","E14-HUB-rest",Done(s,"dog_place_ready")??Need(s,"cloth",1),()=>{s.Spend(("cloth",1));Mark(s,"dog_place_ready","강아지의 쉴 자리 마련",10);}));return;
            }
            if(s.Has("dog_joined"))return;
            if(!s.Has("dog_seen")){list.Add(C("dog.look","입구의 강아지 살펴보기","잠시 거리를 두고 모습을 살펴봐요.\n물자 사용 없음 · 동행은 나중에 선택해요.","01-L5-wary",null,()=>Mark(s,"dog_seen","낡은 집의 강아지 발견",0)));return;}
            if(!s.Has("dog_care_offered"))
            {
                foreach(string item in new[]{"water","packaged_food"})
                {string id=item;list.Add(C("dog.offer."+id,id=="water"?"물을 놓고 물러서기":"먹을 것을 놓고 물러서기",(id=="water"?"생수":"포장 식량")+" 1 · 5분\n수혁이 내려놓고 거리를 둬요.\n떠나도 돌봄 기록은 유지돼요.",id=="water"?"E12-L5-place-water":"E12-L5-food-alternative",Need(s,id,1),()=>{s.Spend((id,1));s.Set("dog_care_kind",id);Mark(s,"dog_care_offered","강아지를 위한 돌봄",5);}));}return;
            }
            if(!s.Has("dog_relaxed")){list.Add(C("dog.wait","조금 떨어져 기다리기","10분 · 물자를 다시 내지 않아요.\n가까이 오도록 기다린 뒤 동행을 결정해요.","03-L5-relaxed",null,()=>Mark(s,"dog_relaxed","강아지 곁에서 기다림",10)));return;}
            list.Add(C("dog.join","캠핑카까지 함께 가기","동행은 선택이에요. 지금 떠나도 다음에 결정할 수 있어요.\n확인하면 강아지가 캠핑카에서 기다려요.","03-L5-relaxed",null,()=>Mark(s,"dog_joined","강아지와 동행",0)));
        }
        public static IEnumerable<KeyValuePair<string,string>> Memories(JourneyState s)
        {
            var day=s.days?.FirstOrDefault(d=>d.day==s.day);if(day==null)yield break;
            foreach(var i in day.obtained)yield return new KeyValuePair<string,string>("item."+i.id,i.name);
            foreach(var id in day.visited)yield return new KeyValuePair<string,string>("place."+id,RegionExploration.Find(id).ShortName+"에서의 시간");
            for(int i=0;i<(day.activities?.Count??0);i++)yield return new KeyValuePair<string,string>("activity."+i,day.activities[i]);
        }
        public static bool Apply(JourneyState s,string section,string id,out string error)
        {
            if(SearchSession.Active(s)){error="진행 중인 탐색을 먼저 마쳐요.";return false;}
            if(BanditEncounter.Active(s)){error="진행 중인 일을 먼저 마쳐요.";return false;}
            var option=Choices(s,section).FirstOrDefault(c=>c.Id==id);error=option==null?"지금은 할 수 없는 행동이에요.":option.Block;
            if(error!=null)return false;option.Apply();return true;
        }
        public static bool TickHeating(JourneyState s,float dt)
        {
            if(!Heating(s)||float.IsNaN(dt)||float.IsInfinity(dt)||dt<=0)return false;
            s.life.microwaveLeft=Math.Max(0,s.life.microwaveLeft-Math.Min(dt,.1f));if(s.life.microwaveLeft>0)return false;
            s.Add("meal","따뜻한 식사",1);JourneyDayLog.Activity(s,"간편식 데우기");return true;
        }
        public static bool Valid(JourneyState s)
        {
            var l=s.life;var c=s.cooking;
            bool finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n)&&n>=0&&n<1000000;
            return (l==null||(l.battery>=0&&l.battery<=Capacity&&finite(l.microwaveLeft)&&l.microwaveLeft<=3&&(string.IsNullOrEmpty(l.energyDestination)||RegionExploration.Find(l.energyDestination)!=null)))
                &&(s.days==null||s.days.All(d=>d.activities==null||(d.activities.Count<=100&&d.activities.All(a=>!string.IsNullOrWhiteSpace(a)&&a.Length<=200))))
                &&(c==null||(c.stage>=0&&c.stage<=3&&c.level>=0&&c.level<=3&&finite(c.heat)&&c.heat<=120&&finite(c.elapsed)&&finite(c.simmer)&&finite(c.good)&&finite(c.spill)))
                &&!(Cooking(s)&&Heating(s));
        }
    }
}
