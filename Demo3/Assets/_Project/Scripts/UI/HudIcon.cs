using UnityEngine;
using UnityEngine.UI;

namespace Live49.UI
{
    // Resolution-independent line icons; no font glyph or new raster-art dependency.
    [RequireComponent(typeof(CanvasRenderer))]
    public class HudIcon : MaskableGraphic
    {
        public enum Kind { Map, Cooking, Bag, Journal, Menu, Moon, Store, Camper }
        public Kind Symbol;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            switch (Symbol)
            {
                case Kind.Camper:
                    Path(vh,5,13,34,13,34,19,44,19,49,30,49,40,5,40,5,13);
                    Path(vh,11,19,25,19,25,29,11,29,11,19);Path(vh,35,23,41,23,45,30,35,30,35,23);
                    Path(vh,12,40,12,46,20,46,20,40);Path(vh,36,40,36,46,44,46,44,40);break;
                case Kind.Store:
                    Path(vh, 7,22, 12,9, 42,9, 47,22, 7,22);
                    Path(vh, 10,23, 10,47, 44,47, 44,23);
                    Path(vh, 17,22, 20,9); Path(vh, 29,22, 29,9); Path(vh, 39,22, 37,9);
                    Path(vh, 28,47, 28,31, 38,31, 38,47); Path(vh, 16,31, 22,31, 22,38, 16,38, 16,31); break;
                case Kind.Map:
                    Path(vh, 7,10, 20,5, 34,11, 47,6, 47,43, 34,48, 20,42, 7,47, 7,10);
                    Path(vh, 20,5, 20,42); Path(vh, 34,11, 34,48); break;
                case Kind.Cooking:
                    Path(vh, 8,24, 12,43, 42,43, 46,24, 8,24);
                    Path(vh, 5,24, 49,24); Path(vh, 21,19, 33,19);
                    Path(vh, 15,14, 13,10, 16,5); Path(vh, 29,12, 27,8, 30,3);
                    Path(vh, 8,29, 3,29, 3,35, 10,35); Path(vh, 46,29, 51,29, 51,35, 44,35); break;
                case Kind.Bag:
                    Path(vh, 11,17, 43,17, 46,47, 8,47, 11,17);
                    Path(vh, 19,21, 19,10, 23,6, 31,6, 35,10, 35,21);
                    Path(vh, 19,33, 35,33, 35,41, 19,41, 19,33); break;
                case Kind.Journal:
                    Path(vh, 11,6, 45,6, 45,48, 11,48, 11,6);
                    Path(vh, 18,6, 18,48); Path(vh, 24,19, 38,19); Path(vh, 24,27, 35,27);
                    Path(vh, 35,6, 35,14, 39,11, 42,14, 42,6); break;
                case Kind.Menu:
                    Path(vh, 8,14, 46,14); Path(vh, 8,27, 46,27); Path(vh, 8,40, 46,40); break;
                case Kind.Moon:
                    Path(vh, 34,5, 21,8, 12,18, 10,30, 16,41, 27,47, 40,44, 48,35,
                        36,36, 26,30, 23,19, 27,10, 34,5); break;
            }
        }
        void Path(VertexHelper vh, params float[] points)
        {
            var r = rectTransform.rect;
            for (int i = 0; i + 3 < points.Length; i += 2)
            {
                var a = new Vector2(r.xMin + points[i] / 54 * r.width, r.yMax - points[i + 1] / 54 * r.height);
                var b = new Vector2(r.xMin + points[i + 2] / 54 * r.width, r.yMax - points[i + 3] / 54 * r.height);
                var d = b - a;
                var n = new Vector2(-d.y, d.x).normalized * Mathf.Min(r.width, r.height) * .019f;
                int start = vh.currentVertCount;
                vh.AddVert(a - n, color, Vector2.zero); vh.AddVert(a + n, color, Vector2.zero);
                vh.AddVert(b + n, color, Vector2.zero); vh.AddVert(b - n, color, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
