using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    public sealed partial class SettlementCraftPanel
    {
        public bool SideRoomConnected{get;private set;}
        public bool SideRoomReady{get;private set;}
        [Min(1)] public int SideRoomPlaces=2;
        public Button HousingButton;
        public Text HousingLabel;
        public int ResidentCapacity=>3+(SideRoomReady?Mathf.Max(1,SideRoomPlaces):0);
        // Residents on expeditions still belong to the settlement. Never evict old saves.
        public bool CanAcceptResidents(int count)=>owner.Campaign!=null&&count>0&&(long)owner.Campaign.Party.Count()+count<=ResidentCapacity;
        public string RoomStatus=>SideRoomReady?"사용 가능":UnderConstruction("prepare-side-room")?"거주 준비 중":SideRoomConnected?"연결 완료":UnderConstruction("open-side-room")?"통로 정리 중":"잠김";
        public void RefreshHousing()
        {
            if(HousingLabel)HousingLabel.text="거주 공간  "+(owner.Campaign?.Party.Count()??0)+" / "+ResidentCapacity;
        }
        void OpenHousing()
        {
            if(owner.Campaign==null||!owner.Main.interactable||owner.Campaign.Stage!=Demo5.NightRun.JourneyStage.Settlement)return;
            Open();FocusRecipe(SideRoomConnected?"prepare-side-room":"open-side-room");RecipeScroll.verticalNormalizedPosition=0;
        }
        string HousingBlock(Recipe recipe)=>recipe.Id=="prepare-side-room"&&!SideRoomConnected?"옆방 통로 연결 필요":null;
    }
}

