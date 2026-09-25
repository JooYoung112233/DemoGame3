using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Ink clock of the turn sequence: an optional paper face, a wedge of the minutes that passed (from SweepFrom to the hand),
    // twelve ticks, a rim, an hour hand and a minute hand in `color` (FieldTurnReplay sweeps ClockMinutes). Never intercepts clicks.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FieldClockGlyph : MaskableGraphic
    {
        [Tooltip("테두리 두께 (px)")] [Min(1)] public float Rim = 3;
        [Tooltip("분침 · 시침 두께 (px)")] [Min(1)] public float Hand = 3;
        [Tooltip("분침 길이 (반지름 비율)")] [Range(.2f, 1)] public float MinuteLength = .72f;
        [Tooltip("시침 길이 (반지름 비율)")] [Range(.2f, 1)] public float HourLength = .45f;
        [Tooltip("시계 판 색 (알파 0이면 판 없음)")] public Color Face = new Color(0, 0, 0, 0);
        [Tooltip("흐른 시간 부채꼴 색 (알파 0이면 없음)")] public Color SweepColor = new Color(1f, .72f, .3f, 0);
        [Tooltip("12개 눈금 길이 (px · 0이면 없음)")] [Min(0)] public float TickLength;
        [Tooltip("원을 이루는 조각 수")] [Range(12, 96)] public int Segments = 36;
        [Tooltip("가리키는 시각 (자정부터 분)")] [SerializeField, Min(0)] float clockMinutes = 600;
        [Tooltip("부채꼴이 시작하는 시각 (자정부터 분 · 음수면 없음)")] [SerializeField] float sweepFrom = -1;
        public override bool raycastTarget { get => false; set { } }
        // Minutes since midnight the hands show; moving it redraws the glyph.
        public float ClockMinutes { get => clockMinutes; set { float m = Mathf.Repeat(value, 1440); if (Mathf.Approximately(m, clockMinutes)) return; clockMinutes = m; SetVerticesDirty(); } }
        // Where the passed-minutes wedge starts (negative: no wedge).
        public float SweepFrom { get => sweepFrom; set { float m = value < 0 ? -1 : Mathf.Repeat(value, 1440); if (Mathf.Approximately(m, sweepFrom)) return; sweepFrom = m; SetVerticesDirty(); } }
        // The minutes the wedge covers now (0 without a wedge) and the angle the minute hand swept over them.
        public float SweptMinutes => sweepFrom < 0 ? 0 : Mathf.Repeat(clockMinutes - sweepFrom, 1440);
        public float SweepDegrees => Mathf.Min(360, SweptMinutes * 6);
        public float MinuteHandDegrees => Mathf.Repeat(clockMinutes, 60) / 60 * 360;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; float outer = Mathf.Min(r.width, r.height) * .5f, inner = Mathf.Max(0, outer - Rim); var c = r.center;
            if (Face.a > 0) Fan(vh, c, outer, 0, 360, Face);
            if (SweepColor.a > 0 && SweptMinutes > 0) { float from = Mathf.Repeat(sweepFrom, 60) / 60 * 360; Fan(vh, c, inner, from, from + SweepDegrees, SweepColor); }
            int n = Segments;
            for (int i = 0; i <= n; i++)
            {
                float a = i * Mathf.PI * 2 / n; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); int s = vh.currentVertCount;
                vh.AddVert(c + d * outer, color, Vector2.zero); vh.AddVert(c + d * inner, color, Vector2.zero);
                if (i > 0) { vh.AddTriangle(s - 2, s, s + 1); vh.AddTriangle(s - 2, s + 1, s - 1); }
            }
            if (TickLength > 0) for (int h = 0; h < 12; h++) { var d = Dir(h * 30); Line(vh, c + d * (inner - TickLength), c + d * inner, Mathf.Max(1, Hand * .6f)); }
            Line(vh, c, c + Dir(Mathf.Repeat(clockMinutes / 60, 12) / 12 * 360) * inner * HourLength, Hand);
            Line(vh, c, c + Dir(MinuteHandDegrees) * inner * MinuteLength, Hand);
        }
        // Degrees clockwise from 12 o'clock.
        static Vector2 Dir(float degrees) { float a = (90 - degrees) * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
        void Line(VertexHelper vh, Vector2 from, Vector2 to, float width)
        {
            var d = (to - from).normalized; var side = new Vector2(-d.y, d.x) * width * .5f; var tail = -d * width * .5f; int s = vh.currentVertCount;
            vh.AddVert(from + tail - side, color, Vector2.zero); vh.AddVert(from + tail + side, color, Vector2.zero); vh.AddVert(to + side, color, Vector2.zero); vh.AddVert(to - side, color, Vector2.zero);
            vh.AddTriangle(s, s + 1, s + 2); vh.AddTriangle(s, s + 2, s + 3);
        }
        // A filled sector between two clock angles (degrees clockwise from 12).
        void Fan(VertexHelper vh, Vector2 c, float radius, float fromDeg, float toDeg, Color k)
        {
            if (radius <= 0 || toDeg <= fromDeg) return; int steps = Mathf.Max(2, Mathf.CeilToInt((toDeg - fromDeg) / 360 * Segments)); int s = vh.currentVertCount; Color32 col = k;
            vh.AddVert(c, col, Vector2.zero);
            for (int i = 0; i <= steps; i++) vh.AddVert(c + Dir(Mathf.Lerp(fromDeg, toDeg, (float)i / steps)) * radius, col, Vector2.zero);
            for (int i = 1; i <= steps; i++) vh.AddTriangle(s, s + i, s + i + 1);
        }
    }
}
