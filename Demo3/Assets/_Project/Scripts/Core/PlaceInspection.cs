using System;
using System.Linq;
namespace Live49.Core
{
    public static class PlaceInspection
    {
        public sealed class Point
        {
            public string Label,Description,Finding;
            public Point(string label,string description,string finding){Label=label;Description=description;Finding=finding;}
        }
        public static Point[] Points(string id)
        {
            switch(id)
            {
                case RegionTravel.Entry:return new[]{new Point("진입 구간 살펴보기","고개 너머에 도착한 자리와 주변을 살펴봐요.","진입 구간의 상태를 기록했어요."),new Point("돌아갈 길 확인하기","첫 동네로 돌아가는 길과 주차 위치를 확인해요.","고개를 되넘어가는 길을 기록했어요.")};
                case "L2":return new[]{new Point("정비 구역 살펴보기","주유기 옆 정비 구역과 남아 있는 물건을 확인해요.","이동에 쓸 수 있는 밀봉 연료를 챙겼어요. 차량 연료 +2"),new Point("진입로 확인하기","주유소 뒤편 차량 진입로를 따라 주변 길을 확인해요.","수리점과 주차장으로 이어지는 길을 기록했어요.")};
                case "L3":return new[]{new Point("진열대 살펴보기","생활용품 진열대와 남아 있는 포장을 살펴봐요.","진열대의 물건 상태를 기록했어요."),new Point("가게 뒤 골목 확인하기","가게 옆으로 난 골목의 출입구를 확인해요.","낡은 집과 세탁소로 이어지는 골목을 찾았어요.")};
                case "L4":return new[]{new Point("작업대 살펴보기","공구가 놓였던 작업대와 전기 부품의 상태를 확인해요.","쓸 만한 부품을 찾을 작업 공간을 기록했어요."),new Point("가게 앞 도로 확인하기","수리점 앞에서 갈라지는 도로를 살펴봐요.","강변과 약국 쪽으로 이어지는 길을 기록했어요.")};
                case "L5":return new[]{new Point("현관 주변 살펴보기","문 앞의 생활 흔적을 조용히 살펴봐요.","현관 주변에 남은 흔적을 기록했어요."),new Point("담장 옆 골목 확인하기","담장 바깥으로 이어지는 골목을 확인해요.","강변과 약국으로 나가는 길을 찾았어요.")};
                case "L6":return new[]{new Point("쉼터 주변 살펴보기","강변에서 잠시 머물 수 있는 자리를 찾아봐요.","다시 쉬러 올 수 있는 자리를 기록했어요."),new Point("산책로 끝 확인하기","산책로 끝에서 고개 방향의 연결 길을 확인해요.","강변 쪽 진입로를 확인했어요.")};
                case "L7":return new[]{new Point("고갯길 노면 확인하기","캠핑카가 지나갈 길의 노면과 폭을 살펴봐요.","고개를 넘을 차량 통행 구간을 확인했어요."),new Point("진입로 표식 살펴보기","고개 너머로 이어지는 방향과 돌아올 길을 기록해요.","다음 지역으로 출발할 진입로를 기록했어요.")};
                case "L8":return new[]{new Point("주차 구획 살펴보기","캠핑카를 세울 공간과 바닥 상태를 살펴봐요.","차를 세울 수 있는 구획을 기록했어요."),new Point("차량 출입구 확인하기","주차장 양쪽 출입구와 돌아 나갈 길을 확인해요.","출입구를 확인하고 주차장 탐색을 마쳤어요.")};
                case "L9":return new[]{new Point("가게 앞 살펴보기","세탁소 앞에 남은 생활의 흔적을 살펴봐요.","가게 앞의 흔적을 기록했어요."),new Point("옆 골목 확인하기","세탁소 옆 좁은 골목과 큰길의 연결을 확인해요.","돌아갈 길을 확인하고 세탁소 탐색을 마쳤어요.")};
                case "L10":return new[]{new Point("약국 앞 살펴보기","약국 앞에 남은 표식과 주변 상태를 살펴봐요.","약국 주변의 상태를 기록했어요."),new Point("바깥길 확인하기","약국을 지나 고개 쪽으로 이어지는 길을 확인해요.","약국 쪽 진입로를 확인했어요.")};
                default:return Array.Empty<Point>();
            }
        }
        static string Key(string id,int index)=>(RegionExploration.Find(id)?.Region??RegionExploration.RegionId)+".inspected."+id+"."+index;
        public static bool Checked(JourneyState state,string id,int index)=>RegionExploration.Completed(state,id)||state.Has(Key(id,index));
        public static int Count(JourneyState state,string id)=>Enumerable.Range(0,Points(id).Length).Count(i=>Checked(state,id,i));
        public static bool Inspect(JourneyState state,string id,int index,out string[] revealed)
        {
            revealed=Array.Empty<string>();var points=Points(id);
            if(BanditEncounter.Active(state))return false;
            if(state.day==0||!RegionExploration.Outside(state)||RegionExploration.PlayerPlace(state)!=id||!RegionExploration.Discovered(state,id)||index<0||index>=points.Length||Checked(state,id,index))return false;
            state.Set(Key(id,index));state.minutes+=5;JourneyDayLog.Visit(state,id);
            if(id=="L2"&&index==0){state.fuel=Math.Min(1000000,state.fuel+2);JourneyDayLog.Fuel(state,2);}
            if(Count(state,id)==points.Length)RegionExploration.Complete(state,id,out revealed);
            return true;
        }
    }
}
