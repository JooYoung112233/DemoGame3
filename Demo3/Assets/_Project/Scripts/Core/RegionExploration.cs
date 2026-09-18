using System;
using System.Linq;
using UnityEngine;

namespace Live49.Core
{
    // Region-scoped completion flags keep discovery separate from visiting and vehicle parking.
    public static class RegionExploration
    {
        public const string RegionId="region01";
        public sealed class Site
        {
            public string Id,Name,ShortName,Description,Finding,Art;
            public string Region=RegionId;
            public Vector2 Point;
            public string[] Parents=Array.Empty<string>();
            public bool RequireAll;
        }
        public static readonly Site[] Sites={
            new Site{Id="L1",Name="작은 편의점",ShortName="편의점",Point=new Vector2(734,395),Art="store",Description="가게 앞에 캠핑카를 세울 수 있어요.\n물과 식량을 먼저 살펴봐요.",Finding="물과 식량을 찾으며 주변 가게로 이어지는 길을 확인했어요."},
            new Site{Id="L2",Name="주유소 정비·주차 구역",ShortName="주유소",Point=new Vector2(1033,330),Art="region-L2",Parents=new[]{"L1"},Description="정비 구역과 주변 진입로를 살펴볼 곳이에요.",Finding="정비 구역 너머 수리점과 공영주차장으로 가는 길을 찾았어요."},
            new Site{Id="L3",Name="생활 잡화점",ShortName="잡화점",Point=new Vector2(493,680),Art="region-L3",Parents=new[]{"L1"},Description="생활용품 가게 주변의 골목을 살펴봐요.",Finding="가게 뒤편 골목에서 낡은 집과 세탁소로 이어지는 길을 확인했어요."},
            new Site{Id="L4",Name="철물·전기 수리점",ShortName="수리점",Point=new Vector2(953,678),Art="region-L4",Parents=new[]{"L2"},Description="작업대와 주변 도로를 살펴볼 곳이에요.",Finding="주변 길을 따라 강변 쉼터와 동네 약국의 위치를 확인했어요."},
            new Site{Id="L5",Name="낡은 집",ShortName="낡은 집",Point=new Vector2(220,417),Art="region-L5",Parents=new[]{"L3"},Description="집 주변의 생활 흔적과 골목을 살펴봐요.",Finding="골목 바깥으로 이어지는 강변 쉼터와 동네 약국의 길을 찾았어요."},
            new Site{Id="L6",Name="강변 쉼터",ShortName="강변 쉼터",Point=new Vector2(536,198),Art="region-L6",Parents=new[]{"L4","L5"},Description="강변 산책로와 바깥으로 이어지는 길을 확인해요.",Finding="강변 쪽 길을 확인했어요. 약국 주변 길도 확인하면 고갯길 진입로를 찾을 수 있어요."},
            new Site{Id="L7",Name="고갯길 입구",ShortName="고갯길",Point=new Vector2(1136,716),Art="region-L7",Parents=new[]{"L6","L10"},RequireAll=true,Description="다음 지역으로 이어지는 진입로를 살펴봐요.",Finding="고갯길 진입로를 확인했어요. 다음 지역으로 떠날 준비를 할 수 있어요."},
            new Site{Id="L8",Art="region-L8",Name="공영주차장",ShortName="주차장",Point=new Vector2(395,590),Parents=new[]{"L2"},Description="주차 구획과 주변 출입로를 둘러봐요.",Finding="주차 구획과 돌아가는 길을 기록했어요. 주변 탐색을 마쳤어요."},
            new Site{Id="L9",Art="region-L9",Name="작은 세탁소",ShortName="세탁소",Point=new Vector2(305,682),Parents=new[]{"L3"},Description="세탁소 앞과 주변 골목을 살펴봐요.",Finding="가게 앞과 골목을 둘러보고 생활의 흔적을 기록했어요."},
            new Site{Id="L10",Art="region-L10",Name="동네 약국",ShortName="약국",Point=new Vector2(195,324),Parents=new[]{"L4","L5"},Description="약국 주변에서 바깥으로 이어지는 길을 확인해요.",Finding="약국 주변 길을 확인했어요. 강변 쪽 길도 확인하면 고갯길 진입로를 찾을 수 있어요."}
        };
        public static readonly Site[] NextSites={new Site{Id=RegionTravel.Entry,Region=RegionTravel.NextRegion,Name="고개 너머 진입로",ShortName="진입로",Point=new Vector2(610,460),Art="region-L7",Description="고개를 넘어 도착한 진입 구간이에요.\n주변을 살피고 여정을 정리해요.",Finding="진입로와 돌아갈 길을 확인했어요."}};
        public static Site[] ForRegion(string region)=>region==RegionTravel.NextRegion?NextSites:Sites;
        public static Site[] ActiveSites(JourneyState state)=>ForRegion(state==null?RegionId:RegionTravel.Current(state));
        public static Site Find(string id)=>Array.Find(Sites,s=>s.Id==id)??Array.Find(NextSites,s=>s.Id==id);
        public static bool Outside(JourneyState state)=>state.inStore||!string.IsNullOrEmpty(state.exploringPlace);
        public static string PlayerPlace(JourneyState state)=>state.inStore?"L1":string.IsNullOrEmpty(state.exploringPlace)?state.location:state.exploringPlace;
        public static string ParkingName(JourneyState state)=>Find(state.location)?.ShortName+" 앞";
        public static Vector2 ParkingPoint(string id)=>id=="camper"?new Vector2(395,590):id=="L1"?new Vector2(765,437):(Find(id)?.Point??Vector2.zero)+new Vector2(20,20);
        public static int TravelMinutes(JourneyState state,string id)=>PlayerPlace(state)==id?0:Outside(state)?10:20;
        public static int TravelFuel(JourneyState state)=>Outside(state)?0:1;
        public static bool Travel(JourneyState state,string id)
        {
            if(TravelBlock(state,id)!=null)return false;
            bool walking=Outside(state);state.minutes+=TravelMinutes(state,id);state.fuel-=TravelFuel(state);
            if(!walking){state.location=id;state.inStore=false;state.exploringPlace="";}
            else{state.inStore=id=="L1";state.exploringPlace=id=="L1"?"":id;}
            JourneyDayLog.Visit(state,id);
            return true;
        }
        public static bool Completed(JourneyState state,string id)=>state!=null&&Find(id)!=null&&(id=="L1"?state.Has("water_checked")&&state.Has("food_checked"):state.Has(Find(id).Region+".explored."+id));
        public static bool Discovered(JourneyState state,string id)
        {
            var site=Find(id);if(site==null)return false;if(site.Region==RegionTravel.NextRegion&&(state==null||!state.Has(RegionTravel.NextRegion+".opened")))return false;if(site.Parents.Length==0)return true;
            return state!=null&&state.Has("story.discovered."+id)||(site.RequireAll?site.Parents.All(p=>Completed(state,p)):site.Parents.Any(p=>Completed(state,p)));
        }
        public static int CompletedCount(JourneyState s)=>ActiveSites(s).Count(p=>Completed(s,p.Id));
        public static string[] Visible(JourneyState s)=>ActiveSites(s).Where(p=>Discovered(s,p.Id)).Select(p=>p.Id).ToArray();
        public static bool Complete(JourneyState state,string id,out string[] revealed)
        {
            revealed=Array.Empty<string>();
            if(state==null||state.day==0||id=="L1"||state.exploringPlace!=id||!Discovered(state,id)||Completed(state,id))return false;
            if(PlaceInspection.Count(state,id)!=PlaceInspection.Points(id).Length)return false;
            var before=Visible(state);state.Set(Find(id).Region+".explored."+id);
            revealed=Visible(state).Except(before).ToArray();JourneyDayLog.Found(state,revealed);return true;
        }
        public static string TravelBlock(JourneyState state,string id)
        {
            if(SearchSession.Active(state))return "탐색 결과와 주변 인기척을 먼저 확인해요.";
            if(BanditEncounter.Active(state))return "골목에서의 일을 마친 뒤 이동해요.";
            if(state.day==0)return "출발 준비를 마치고 아침에 이동해요.";
            if(!Discovered(state,id))return "아직 가는 길을 확인하지 못했어요.";
            if(Find(id).Region!=RegionTravel.Current(state))return "지역의 진입로에서 캠핑카로 이동해요.";
            if(PlayerPlace(state)==id)return Outside(state)?"지금 이곳을 탐색하고 있어요.":"이곳에 주차했어요. 캠핑카에서 내려 둘러봐요.";
            if(!Outside(state)&&state.fuel<TravelFuel(state))return "이동할 연료가 부족해요. 내려서 도보로 탐색할 수 있어요.";
            return null;
        }
    }
}
