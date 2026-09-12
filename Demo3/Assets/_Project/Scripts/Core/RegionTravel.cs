namespace Live49.Core
{
    public static class RegionTravel
    {
        public const string NextRegion="region02",Entry="R2_GATE";
        // Narrative milestones are supplied by the first drawing/photo/route events, not exploration clicks.
        public const string Drawing="first_drawing_completed",Photo="first_photo_album_confirmed",Route="sejin_route_known";
        public static string Current(JourneyState state)=>string.IsNullOrEmpty(state.regionId)?RegionExploration.RegionId:state.regionId;
        public static string Name(string region)=>region==NextRegion?"고개 너머":"첫 동네";
        public static string Block(JourneyState state,string target)
        {
            if(target!=RegionExploration.RegionId&&target!=NextRegion)return "아직 확인하지 못한 지역이에요.";
            if(target==Current(state))return "지금 머무는 지역이에요.";
            if(state.day<=0)return "출발 준비를 먼저 마쳐요.";
            if(RegionExploration.Outside(state))return "주차한 캠핑카로 돌아와 출발해요.";
            if(Current(state)==RegionExploration.RegionId)
            {
                if(!RegionExploration.Completed(state,"L7"))return "고갯길의 두 확인 지점을 먼저 살펴봐요.";
                if(!state.Has(Drawing)||!state.Has(Photo))return "첫 그림과 첫 사진·앨범 기록을 마친 뒤 떠나요.";
                if(!state.Has(Route))return "세진에게 다음 길 정보를 확인한 뒤 떠나요.";
                if(state.location!="L7")return "캠핑카를 고갯길 입구로 옮겨 출발해요.";
            }
            else if(state.location!=Entry)return "고개 너머 진입로에 캠핑카를 세워 출발해요.";
            if(state.fuel<1)return "지역을 이동할 연료 1이 필요해요.";
            return null;
        }
        public static bool Travel(JourneyState state,string target)
        {
            if(Block(state,target)!=null)return false;
            state.Set("parking."+Current(state),state.location);
            state.regionId=target;state.location=target==NextRegion?Entry:"L7";
            state.Set(target+".opened");state.Set("parking."+target,state.location);
            state.exploringPlace="";state.inStore=false;state.minutes+=40;state.fuel--;
            JourneyDayLog.Visit(state,state.location);return true;
        }
    }
}
