using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Live49.Title
{
    // One title menu row: a surface sprite per state plus a runtime label (colors from design/ui/ch00-01/manifest.json).
    public class TitleMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [SerializeField] string itemId;
        [SerializeField] Image surface;
        [SerializeField] TMP_Text label;
        [SerializeField] Sprite normalSprite;
        [SerializeField] Sprite focusSprite;
        [SerializeField] Sprite pressedSprite;
        [SerializeField] Sprite disabledSprite;
        [SerializeField] Color normalColor = new Color32(0xC7, 0xB6, 0x95, 0xFF);
        [SerializeField] Color focusColor = new Color32(0xF2, 0xE4, 0xC4, 0xFF);
        [SerializeField] Color pressedColor = new Color32(0xFF, 0xF0, 0xD2, 0xFF);
        [SerializeField] Color disabledColor = new Color32(0x86, 0x7A, 0x64, 0xFF);
        [SerializeField] bool interactable = true;

        bool _focused;
        bool _pressed;

        public string Id => itemId;
        public void Configure(string id,string text){itemId=id;label.text=text;}
        public TitleController Owner { get; set; }

        public bool Interactable
        {
            get => interactable;
            set { interactable = value; Refresh(); }
        }

        void Awake() => Refresh();

        public void SetFocused(bool focused) { _focused = focused; Refresh(); }
        public void SetPressed(bool pressed) { _pressed = pressed; Refresh(); }

        void Refresh()
        {
            if (!interactable) Apply(disabledSprite, disabledColor);
            else if (_pressed) Apply(pressedSprite, pressedColor);
            else if (_focused) Apply(focusSprite, focusColor);
            else Apply(normalSprite, normalColor);
        }

        void Apply(Sprite sprite, Color color)
        {
            if (surface != null) surface.sprite = sprite;
            if (label != null) label.color = color;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (interactable && Owner != null) Owner.Focus(this);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (interactable && eventData.button == PointerEventData.InputButton.Left) SetPressed(true);
        }

        public void OnPointerUp(PointerEventData eventData) => SetPressed(false);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (interactable && eventData.button == PointerEventData.InputButton.Left && Owner != null) Owner.Confirm(this);
        }
    }
}
