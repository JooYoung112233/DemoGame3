using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Small ink clock on the turn banner: a rim, an hour hand and a minute hand (FieldTurnReplay sweeps ClockMinutes). Never intercepts clicks.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FieldClockGlyph : MaskableGraphic
    {
        [Tooltip("테두리 두께 (px)")] [Min(1)] public float Rim = 3;
        [Tooltip("분침 · 시침 두께 (px)")] [Min(1)] public float Hand = 3;
        [Tooltip("분침 길이 (반지름 비율)")] [Range(.2f, 1)] public float MinuteLength = .72f;
        [Tooltip("시침 길이 (반지름 비율)")] [Range(.2f, 1)] public float HourLength = .45f;
        [Tooltip("가리키는 시각 (자정부터 분)")] [SerializeField, Min(0)] float clockMinutes = 600;
        public override bool raycastTarget { get => false; set { } }
        // Minutes since midnight the hands show; moving it redraws the glyph.
        public float ClockMinutes { get => clockMinutes; set { float m = Mathf.Repeat(value, 1440); if (Mathf.Approximately(m, clockMinutes)) return; clockMinutes = m; SetVerticesDirty(); } }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; float outer = Mathf.Min(r.width, r.height) * .5f, inner = Mathf.Max(0, outer - Rim); var c = r.center; const int n = 36;
            for (int i = 0; i <= n; i++)
            {
                float a = i * Mathf.PI * 2 / n; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                vh.AddVert(c + d * outer, color, Vector2.zero); vh.AddVert(c + d * inner, color, Vector2.zero);
                if (i > 0) { int s = i * 2; vh.AddTriangle(s - 2, s, s + 1); vh.AddTriangle(s - 2, s + 1, s - 1); }
            }
            Line(vh, c, Mathf.Repeat(clockMinutes / 60, 12) / 12 * 360, inner * HourLength);
            Line(vh, c, Mathf.Repeat(clockMinutes, 60) / 60 * 360, inner * MinuteLength);
        }
        // A hand from the centre; degrees clockwise from 12 o'clock.
        void Line(VertexHelper vh, Vector2 c, float degrees, float length)
        {
            float a = (90 - degrees) * Mathf.Deg2Rad; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); var side = new Vector2(-d.y, d.x) * Hand * .5f; var tail = -d * Hand * .5f;
            int s = vh.currentVertCount;
            vh.AddVert(c + tail - side, color, Vector2.zero); vh.AddVert(c + tail + side, color, Vector2.zero); vh.AddVert(c + d * length + side, color, Vector2.zero); vh.AddVert(c + d * length - side, color, Vector2.zero);
            vh.AddTriangle(s, s + 1, s + 2); vh.AddTriangle(s, s + 2, s + 3);
        }
    }
}
