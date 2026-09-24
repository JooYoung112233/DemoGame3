using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    /// <summary>
    /// A room object remains the click target. Only its small paper mark is shown
    /// at rest; details appear on pointer hover, keyboard focus, or held Alt.
    /// FieldThreatMarker children remain independent and are never hidden here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExplorationHotspot : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler,
        ICanvasRaycastFilter
    {
        public ExpeditionArrivalPanel Arrival;
        public Button Button;
        public Image HitArea, Marker;
        public CanvasGroup Caption, SearchStatus, DoorStatus, FocusCorners;
        public bool IsDoor;
        [Tooltip("Measured against the original 1672 × 941 room painting, top-left coordinates.")]
        public Rect SourceObjectPixels;
        public Color RestPaper = new Color(.94f, .89f, .77f, 1f);
        public Color FocusPaper = new Color(1f, .79f, .39f, 1f);

        bool pointerInside, keyboardFocus;
        public bool IsExpanded { get; private set; }
        public bool WorldInputAllowed
        {
            get
            {
                if (!Arrival || !Arrival.IsOpen || Arrival.InTransit || !Button || !Button.IsInteractable()) return false;
                if (!Arrival.Main || !Arrival.Main.interactable || !Arrival.Main.blocksRaycasts) return false;
                if (Arrival.Popup && Arrival.Popup.activeInHierarchy) return false;
                if (Arrival.Search && Arrival.Search.IsOpen || Arrival.Loot && Arrival.Loot.IsOpen ||
                    Arrival.FieldBags && Arrival.FieldBags.IsOpen) return false;
                if (Arrival.Encounter && (Arrival.Encounter.IsOpen ||
                    Arrival.Encounter.Battle && Arrival.Encounter.Battle.IsOpen)) return false;
                return true;
            }
        }

        void OnEnable() { pointerInside = keyboardFocus = false; RefreshPresentation(); }
        void OnDisable() { pointerInside = keyboardFocus = false; Apply(false); }
        void LateUpdate() { RefreshPresentation(); }

        public void OnPointerEnter(PointerEventData eventData) { pointerInside = true; RefreshPresentation(); }
        public void OnPointerExit(PointerEventData eventData) { pointerInside = false; RefreshPresentation(); }
        public void OnSelect(BaseEventData eventData) { keyboardFocus = true; RefreshPresentation(); }
        public void OnDeselect(BaseEventData eventData) { keyboardFocus = false; RefreshPresentation(); }
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) => WorldInputAllowed;

        public void RefreshPresentation()
        {
            bool allowed = WorldInputAllowed;
            var keyboard = Keyboard.current;
            bool alt = keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
            bool selected = keyboardFocus && EventSystem.current && EventSystem.current.currentSelectedGameObject == gameObject;
            if (HitArea) HitArea.raycastTarget = allowed;
            Apply(allowed && (pointerInside || selected || alt));
        }

        void Apply(bool expanded)
        {
            IsExpanded = expanded;
            SetVisible(Caption, IsDoor || expanded);
            SetVisible(SearchStatus, expanded);
            SetVisible(DoorStatus, expanded);
            SetVisible(FocusCorners, expanded);
            if (Marker) Marker.color = expanded ? FocusPaper : RestPaper;
        }

        static void SetVisible(CanvasGroup group, bool visible)
        {
            if (!group) return;
            group.alpha = visible ? 1 : 0;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
    }
}
