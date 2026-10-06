using System.Linq;
using UnityEngine;
namespace Demo5.FrontEnd
{
    public sealed partial class SettlementCraftPanel
    {
        public bool CookerImproved{get;private set;}
        [Range(1,100)] public int CookerTimePercent=80;
        public int BedLevel=>owner.Development&&!owner.Development.State.Bed?0:BedRepaired?2:1;
        public int BenchLevel=>owner.Development&&!owner.Development.State.Workbench?0:BenchImproved?2:1;
        public int CookerLevel=>owner.Development&&!owner.Development.State.Cooker?0:CookerImproved?2:1;
        public bool UnderConstruction(string id)=>orders.Any(o=>o.Recipe.Id==id);
        public string ConstructionBlock(Recipe recipe)
        {
            if(recipe!=null&&!IsRecipeUnlocked(recipe))return "시설 복구·연구 필요";
            if(recipe==null)return null; var development=owner.Development?owner.Development.Block(recipe):null; if(development!=null)return development;
            var housing=HousingBlock(recipe);if(housing!=null)return housing;
            if(recipe.Category==0&&UnderConstruction("upgrade-bench"))return "작업대 공사 중";
            if(recipe.Id=="repair-bed"&&owner.WorkPanel.Orders.Count>0)return "휴식 완료 후 개선 가능";
            if(recipe.Id=="upgrade-bench"&&orders.Any(o=>o.Recipe.Category==0))return "제작 완료 후 개선 가능";
            if(recipe.Id=="upgrade-cooker"&&owner.CookingPanel.Orders.Count>0)return "조리 완료 후 개선 가능";
            return null;
        }
        public int CookingDuration(int minutes,int quantity)=>minutes==0?0:Mathf.Max(1,Mathf.CeilToInt(minutes*quantity*(CookerImproved?CookerTimePercent:100)/100f));
        string FacilityDescription(Recipe r)
        {
            if(r.Id=="repair-bed")return "침대 Lv."+BedLevel+(BedRepaired?"":" → Lv.2")+"\n휴식 회복 +1";
            if(r.Id=="upgrade-bench")return "작업대 Lv."+BenchLevel+(BenchImproved?"":" → Lv.2")+"\n제작 시간 −20%";
            if(r.Id=="upgrade-cooker")return "조리대 Lv."+CookerLevel+(CookerImproved?"":" → Lv.2")+"\n조리 시간 −"+(100-CookerTimePercent)+"%";
            if(r.Id=="open-side-room")return "옆방 · "+RoomStatus+"\n통로만 연결 · 한도 유지";
            if(r.Id=="prepare-side-room")return "옆방 · "+RoomStatus+"\n"+(SideRoomReady?"수용 한도 "+ResidentCapacity+"명":"수용 한도 3 → "+(3+Mathf.Max(1,SideRoomPlaces))+"명");
            return r.Description;
        }
    }
}


