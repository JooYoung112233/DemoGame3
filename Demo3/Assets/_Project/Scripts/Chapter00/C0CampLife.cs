using Live49.Core;
namespace Live49.Chapter00
{
    public partial class C0OpeningDirector
    {
        void RefreshLifeStage()
        {
            if(_state.day<=0)return;
            if(AwayFromCamper)SetRegionView(_state.exploringPlace);
            else if(!_state.inStore)
                SetJourneyImage(_state.Has("dog_place_ready")?"life-E14-HUB-rest":_state.Has("life.meal."+_state.day)?"life-E03-HUB-rations-shared":"journal");
        }
    }
}
