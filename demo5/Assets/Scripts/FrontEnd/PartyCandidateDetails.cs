using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    public sealed class PartyCandidateDetails : MonoBehaviour
    {
        public Image Portrait;
        public Text Name, Stats, SelectionState, TraitTitle, TraitDescription, Characteristics;
        public void Bind(PartyCandidate candidate,bool selected)
        {
            Portrait.sprite=candidate.Portrait;
            Name.text=candidate.DisplayName;
            Stats.text="가방 "+candidate.BagCapacity+"칸   ·   체력 "+candidate.Health;
            SelectionState.text=selected?"함께할 동료로 선택됨":"살펴보는 동료";
            TraitTitle.text=string.IsNullOrWhiteSpace(candidate.RoleTitle)?candidate.TraitTitle:candidate.RoleTitle;
            TraitDescription.text=candidate.TraitDescription;
            Characteristics.text=candidate.Characteristics+"\n“"+candidate.FirstLine+"”";
        }
    }
}
