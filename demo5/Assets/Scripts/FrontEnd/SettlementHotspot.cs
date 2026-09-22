using UnityEngine;
using UnityEngine.EventSystems;
namespace Demo5.FrontEnd
{
    public sealed class SettlementHotspot:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler
    {
        public GameObject Label;
        public void OnPointerEnter(PointerEventData e){Label.SetActive(true);}
        public void OnPointerExit(PointerEventData e){Label.SetActive(false);}
        public void OnSelect(BaseEventData e){Label.SetActive(true);}
        public void OnDeselect(BaseEventData e){Label.SetActive(false);}
    }
}
