using UnityEngine;
using UnityEngine.UI;

namespace Live49.UI
{
    // Native UI gradient: editable independently of the source artwork and panel sprites.
    [RequireComponent(typeof(CanvasRenderer))]
    public class ReadabilityGradient : MaskableGraphic
    {
        public static void AddTo(Transform parent)
        {
            if (parent.Find("ReadabilityGradient") != null) return;
            var go = new GameObject("ReadabilityGradient", typeof(RectTransform), typeof(CanvasRenderer), typeof(ReadabilityGradient));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.SetAsFirstSibling();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.GetComponent<ReadabilityGradient>().raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float[] y = { 0f, .11f, .24f, .34f, .46f, 1f };
            float[] a = { .80f, .76f, .64f, .30f, 0f, 0f };
            for (int i = 0; i < y.Length; i++)
            {
                Color c = new Color(.035f, .026f, .018f, a[i]);
                float py = Mathf.Lerp(r.yMin, r.yMax, y[i]);
                vh.AddVert(new Vector3(r.xMin, py), c, Vector2.zero);
                vh.AddVert(new Vector3(r.xMax, py), c, Vector2.one);
                if (i == 0) continue;
                int k = (i - 1) * 2;
                vh.AddTriangle(k, k + 2, k + 1);
                vh.AddTriangle(k + 1, k + 2, k + 3);
            }
        }
    }
}
