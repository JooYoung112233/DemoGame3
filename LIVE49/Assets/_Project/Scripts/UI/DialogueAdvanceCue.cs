using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Live49.UI
{
    // The director owns visibility; this component only builds and animates the ready-to-advance hint.
    public class DialogueAdvanceCue : MonoBehaviour
    {
        CanvasGroup _group;
        RectTransform _arrow;
        float _time;

        public static void Configure(CanvasGroup group, TMP_FontAsset font)
        {
            if (group.GetComponent<DialogueAdvanceCue>() != null) return;
            var view = group.gameObject.AddComponent<DialogueAdvanceCue>();
            view._group = group;
            if (group.TryGetComponent<Image>(out var oldIcon)) oldIcon.enabled = false;
            group.blocksRaycasts = false;
            group.interactable = false;
            var rt = (RectTransform)group.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(1510, -979);
            rt.sizeDelta = new Vector2(300, 32);

            view.Label("ContinueLabel", "계속", font, 185, 64, 23, new Color32(242, 228, 196, 255));
            var arrow = new GameObject("NextArrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(AdvanceArrowGraphic));
            view._arrow = (RectTransform)arrow.transform;
            view._arrow.SetParent(rt, false);
            view._arrow.anchorMin = view._arrow.anchorMax = new Vector2(0, .5f);
            view._arrow.sizeDelta = new Vector2(13, 18);
            view._arrow.anchoredPosition = new Vector2(275, 0);
            var graphic = arrow.GetComponent<AdvanceArrowGraphic>();
            graphic.color = new Color32(242, 228, 196, 255);
            graphic.raycastTarget = false;
        }

        void Label(string name, string value, TMP_FontAsset font, float x, float width, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, 0);
            rt.sizeDelta = new Vector2(width, 32);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font; text.text = value; text.fontSize = size; text.color = color;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
        }

        void LateUpdate()
        {
            if (_group == null || _arrow == null) return;
            if (_group.alpha <= .001f) _time = 0f;
            else _time += Time.unscaledDeltaTime;
            float offset = 3f * (.5f - .5f * Mathf.Cos(_time * Mathf.PI * 2f / 1.4f));
            _arrow.anchoredPosition = new Vector2(275 + offset, 0);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public class AdvanceArrowGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            vh.AddVert(new Vector3(r.xMin, r.yMin), color, Vector2.zero);
            vh.AddVert(new Vector3(r.xMin, r.yMax), color, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.center.y), color, Vector2.zero);
            vh.AddTriangle(0, 1, 2);
        }
    }
}
