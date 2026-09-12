using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Live49.UI
{
    // Scene-space targets in the same 1920x1080 frame as the art. Only the focused target gets a label.
    public class CamperInteractions : MonoBehaviour
    {
        CanvasGroup _group;
        Action<string> _interact;
        public static CamperInteractions Create(GameHud hud, TMP_FontAsset font, Action<string> interact)
        {
            var root = new GameObject("CamperInteractions", typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)root.transform;
            rt.SetParent(hud.InteractionRoot, false); rt.SetAsFirstSibling();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var view = root.AddComponent<CamperInteractions>(); view._group = root.GetComponent<CanvasGroup>(); view._interact = interact;
            view._group.alpha = 0;
            view._group.interactable = view._group.blocksRaycasts = false;
            view.Target("soi", "소이 · 대화하기", 1120, 407, 112, 124, font);
            view.Target("kitchen", "주방 · 살펴보기", 874, 400, 148, 100, font);
            view.Target("bed", "침대 · 쉬기", 1475, 573, 276, 142, font);
            view.Target("book", "스케치북 · 살펴보기", 1095, 568, 162, 101, font);
            return view;
        }
        void Update()
        {
            bool show = GameHud.Instance != null && GameHud.Instance.IsExploring && !GameHud.Instance.IsPaused;
            _group.alpha = show ? 1 : 0;
            _group.interactable = _group.blocksRaycasts = show;
        }
        void Target(string id, string label, float x, float y, float width, float height, TMP_FontAsset font)
        {
            var rt = MakeRect(transform, "Target_" + id, x-width/2, y-height/2, width, height);
            var image = rt.gameObject.AddComponent<Image>(); image.color = Color.clear;
            var button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => Activate(id));
            var ring = MakeRect(rt, "Marker", width/2-8, 8, 16, 16).gameObject.AddComponent<Image>();
            ring.color = new Color32(236,205,148,235);
            ring.raycastTarget = false;
            ring.rectTransform.localRotation = Quaternion.Euler(0,0,45);
            var inner = MakeRect(ring.transform, "Center", 3, 3, 10, 10).gameObject.AddComponent<Image>();
            inner.color = new Color32(37,31,23,255); inner.raycastTarget = false;
            var tag = MakeRect(rt, "HoverLabel", width/2-152, -71, 304, 43);
            tag.gameObject.AddComponent<Image>().color = new Color32(30,27,23,247);
            tag.GetComponent<Image>().raycastTarget = false;
            var textRt = MakeRect(tag, "Text", 10, 0, 284, 43);
            var text = textRt.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.text = label; text.fontSize = 21; text.color = new Color32(242,228,196,255);
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            var feedback = rt.gameObject.AddComponent<CamperTargetFeedback>();
            feedback.Label = tag.gameObject; feedback.Image = image;
            tag.gameObject.SetActive(false);
        }
        public void Activate(string id)
        {
            if (GameHud.Instance == null || !GameHud.Instance.IsExploring || GameHud.Instance.IsPaused) return;
            Live49.Core.FreshInput.DiscardPending();
            _interact(id);
        }
        static RectTransform MakeRect(Transform p, string name,float x,float y,float w,float h)
        {
            var rt = (RectTransform)new GameObject(name,typeof(RectTransform)).transform;
            rt.SetParent(p,false);rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);
            rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);return rt;
        }
    }
    public class CamperTargetFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public GameObject Label;
        public Image Image;
        bool _hovered;
        public void OnPointerEnter(PointerEventData e) { _hovered=true; Refresh(); }
        public void OnPointerExit(PointerEventData e) { _hovered=false; Refresh(); }
        void Update() => Refresh();
        void Refresh()
        {
            bool show = _hovered && GameHud.Instance != null && GameHud.Instance.IsExploring && !GameHud.Instance.IsPaused;
            Label.SetActive(show);
            Image.color = show ? new Color(.8f,.65f,.35f,.08f) : Color.clear;
        }
    }
}
