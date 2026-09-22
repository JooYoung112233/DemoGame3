using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace Demo5.FrontEnd
{
    public sealed class PartyCandidateCard : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        public Button Button;
        public Image Portrait;
        public Text Name,Bag,Health,Description;
        public GameObject SelectedBorder,Check;
        public string CandidateId {get;private set;}
        UnityEngine.Events.UnityAction preview;
        public void OnPointerEnter(PointerEventData e)=>preview?.Invoke();
        public void OnSelect(BaseEventData e)=>preview?.Invoke();
        public void Bind(PartyCandidate candidate,bool selected,UnityEngine.Events.UnityAction onClick,UnityEngine.Events.UnityAction onPreview=null)
        {
            preview=onPreview;
            CandidateId=candidate.Id;Portrait.sprite=candidate.Portrait;
            Name.text=candidate.DisplayName;Bag.text=candidate.BagCapacity.ToString();Health.text=candidate.Health.ToString();Description.text=candidate.Description;
            SelectedBorder.SetActive(selected);Check.SetActive(selected);
            Button.onClick.RemoveAllListeners();Button.onClick.AddListener(onClick);
        }
    }
}
