using UnityEngine;
using UnityEngine.EventSystems;

namespace Live49.UI
{
    public class HudButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public CanvasGroup Highlight;
        bool _hovered, _selected;
        public void OnPointerEnter(PointerEventData e) { _hovered = true; }
        public void OnPointerExit(PointerEventData e) { _hovered = false; }
        public void OnSelect(BaseEventData e) { _selected = true; }
        public void OnDeselect(BaseEventData e) { _selected = false; }
        void Update()
        {
            if (Highlight != null) Highlight.alpha = Mathf.MoveTowards(Highlight.alpha, _hovered || _selected ? 1 : 0, Time.unscaledDeltaTime * 9);
        }
        void OnDisable() { _hovered = _selected = false; if (Highlight != null) Highlight.alpha = 0; }
    }
}
