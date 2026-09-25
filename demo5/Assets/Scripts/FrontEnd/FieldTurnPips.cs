using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Code-drawn turn pips on the time paper (site board, 2nd visit on): one bar per turn of the site clock, left to right; spent
    // turns are hollow, the rest full ink; every MarkEvery-th bar (오래 머묾: the danger floor rises when it is spent) is red.
    // A turn passing drains its pips (FieldTurnReplay drives Drain: a flash, then the ink sinks). No raster art; never takes clicks.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FieldTurnPips : MaskableGraphic
    {
        [Tooltip("칸 수 (장소 시계 턴)")] [Min(1)] public int Count = 24;
        [Tooltip("쓴 턴 (왼쪽부터 빈 칸)")] [Min(0)] public int Used;
        [Tooltip("몇 칸마다 오래 머묾 표시 (0이면 없음)")] [Min(0)] public int MarkEvery = 8;
        [Tooltip("남은 칸 (색: Color) / 오래 머묾 칸")] public Color Mark = new Color(.74f, .17f, .12f, 1);
        [Tooltip("쓴 칸 / 쓴 오래 머묾 칸")] public Color Spent = new Color(.15f, .13f, .11f, .16f), MarkSpent = new Color(.74f, .17f, .12f, .24f);
        [Tooltip("비는 순간 번쩍이는 색 / 오래 머묾 칸이 빌 때")] public Color Flash = new Color(1f, .78f, .3f, 1), MarkFlash = new Color(1f, .3f, .18f, 1);
        [Tooltip("칸 사이 간격 (px)")] [Min(0)] public float Gap = 2;
        [Tooltip("번쩍임이 차지하는 앞부분 (비율)")] [Range(0, .9f)] public float FlashShare = .35f;
        public override bool raycastTarget { get => false; set { } }

        // Pips [DrainFrom, Used) are draining (0 = still full, 1 = spent); none when DrainFrom >= Used.
        public int DrainFrom { get; private set; } = int.MaxValue;
        public float Drain01 { get; private set; } = 1;
        public bool IsMark(int i) => MarkEvery > 0 && (i + 1) % MarkEvery == 0;
        // How much ink pip i holds now (1 full, 0 spent).
        public float FillOf(int i) => i >= Used ? 1 : i < DrainFrom ? 0 : 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((Drain01 - FlashShare) / Mathf.Max(.01f, 1 - FlashShare)));
        // A pip that crosses a 오래 머묾 mark in the current drain.
        public bool CrossesMark { get { for (int i = Mathf.Max(0, DrainFrom); i < Mathf.Min(Used, Count); i++) if (IsMark(i)) return true; return false; } }

        public void Set(int count, int used, int markEvery)
        {
            count = Mathf.Max(1, count); used = Mathf.Max(0, used); markEvery = Mathf.Max(0, markEvery);
            if (count == Count && used == Used && markEvery == MarkEvery) return;
            Count = count; Used = used; MarkEvery = markEvery; SetVerticesDirty();
        }
        public void Drain(int from, float progress)
        {
            progress = Mathf.Clamp01(progress); if (from == DrainFrom && Mathf.Approximately(progress, Drain01)) return;
            DrainFrom = from; Drain01 = progress; SetVerticesDirty();
        }
        public void EndDrain() { if (DrainFrom == int.MaxValue) return; DrainFrom = int.MaxValue; Drain01 = 1; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; int n = Mathf.Max(1, Count); float w = (r.width - Gap * (n - 1)) / n; if (w <= 0 || r.height <= 0) return;
            float flash = Drain01 < FlashShare ? Mathf.Sin(Mathf.Clamp01(Drain01 / Mathf.Max(.01f, FlashShare)) * Mathf.PI) : 0;
            for (int i = 0; i < n; i++)
            {
                float x0 = r.xMin + i * (w + Gap), x1 = x0 + w; bool mark = IsMark(i);
                Quad(vh, x0, r.yMin, x1, r.yMax, mark ? MarkSpent : Spent);
                float fill = FillOf(i); if (fill <= 0) continue;
                var ink = mark ? Mark : color;
                if (i >= DrainFrom && i < Used) ink = Color.Lerp(ink, mark ? MarkFlash : Flash, flash);
                Quad(vh, x0, r.yMin, x1, r.yMin + r.height * fill, ink);
            }
        }
        static void Quad(VertexHelper vh, float x0, float y0, float x1, float y1, Color c)
        {
            if (x1 - x0 <= 0 || y1 - y0 <= 0) return;
            int i = vh.currentVertCount; Color32 k = c;
            vh.AddVert(new Vector3(x0, y0), k, Vector2.zero); vh.AddVert(new Vector3(x0, y1), k, Vector2.zero);
            vh.AddVert(new Vector3(x1, y1), k, Vector2.zero); vh.AddVert(new Vector3(x1, y0), k, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
