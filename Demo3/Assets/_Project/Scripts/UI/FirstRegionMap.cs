using System.Linq;
using Live49.Core;

namespace Live49.UI
{
    public static class FirstRegionMap
    {
        public static MapPanel.Place[] Create(JourneyState state=null)=>RegionExploration.ActiveSites(state).Select(site=>new MapPanel.Place
        {
            Id=site.Id,Name=site.Name,MapLabel=site.ShortName,Point=site.Point,
            Discovered=RegionExploration.Discovered(state,site.Id),Completed=RegionExploration.Completed(state,site.Id),
            IsNew=state!=null&&site.Id!="L1"&&RegionExploration.Discovered(state,site.Id)&&!RegionExploration.Completed(state,site.Id)&&!state.Has("map.seen."+site.Id),
            Description=site.Description,PreviewResource=site.Art,
            Purpose=state==null?"":RegionExploration.PlayerPlace(state)==site.Id?"현재 장소 · 이동 비용 없음":RegionExploration.Outside(state)?"도보 이동 · 연료를 사용하지 않아요.":"이동 연료  1 / 보유 "+state.fuel,
            TravelTime=state==null?"—":RegionExploration.TravelMinutes(state,site.Id)==0?"바로 앞":(RegionExploration.Outside(state)?"도보 ":"차량 ")+RegionExploration.TravelMinutes(state,site.Id)+"분",
            TravelLabel=state!=null&&RegionExploration.PlayerPlace(state)==site.Id?"현재 장소":state!=null&&RegionExploration.Outside(state)?"걸어서 이동":"캠핑카로 출발",
            ConfirmationText=state!=null&&RegionExploration.Outside(state)?"걸어서 이동할까요?\n캠핑카는 "+RegionExploration.ParkingName(state)+"에 머물러요.":"캠핑카를 타고 이동할까요?\n도착한 장소에 새로 주차해요."
        }).ToArray();
    }
}
