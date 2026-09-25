using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Plays a short sequence whenever an exploration turn passes (first visit and site board alike). 2026-09-25 (user: '턴 진행 눌렀을 때
    // 우상단 턴 칸수가 내려가고, 시계 같은 타이머가 나와서 돌아가는 연출'): a clock pops out of the top-right time paper, its minute hand
    // sweeps the minutes that passed (a wedge fills behind it) while the paper's time digits roll to the new time, then it tucks back;
    // on the site board the spent turn pip flashes and drains and '남은 N턴' ticks down with a small bump (a 오래 머묾 pip flashes red).
    // Also a '+10분' tick and noise rings at the objects searched this turn. The time paper's one-line time and turn count are
    // drawn here (TimeText / TurnCount mirror the arrival Clock and the room navigation's TurnLabel, which other code keeps writing).
    // Presentation only: the turn has already resolved, nothing waits for it, and windows open at once (everything simply fades
    // while one covers the room). A new turn while playing restarts it. Writes FieldTurnPulse.
    [DefaultExecutionOrder(100)]
    public sealed class FieldTurnReplay : MonoBehaviour
    {
        public ExpeditionArrivalPanel Arrival;
        [Header("시계 · 시간 종이 옆으로 튀어나옴 (TurnBanner)")]
        public CanvasGroup Banner;
        public Text BannerTitle, BannerDetail;
        public FieldClockGlyph BannerClock;
        [Tooltip("시계 옆 제목 ({0}: 이번 방문 턴, {1}: 흐른 분)")] public string BannerFormat = "{0}턴 · {1}분 흐름";
        [Tooltip("둘째 줄: 소음 ({0}: 이번 턴 소음)")] public string DetailNoise = "소음 +{0}";
        [Tooltip("둘째 줄: 소음이 없을 때")] public string DetailQuiet = "소리 없이";
        [Tooltip("둘째 줄: 원정대가 방을 옮길 때")] public string DetailMove = "원정대 이동";
        [Tooltip("튀어나올 때 시작 크기")] [Range(.3f, 1)] public float BannerPop = .92f;
        [Tooltip("튀어나오기 전 · 들어갈 때 자리 (쉬는 자리 기준 px · 시간 종이 쪽)")] public Vector2 PopFrom = new Vector2(80, 0);
        [Tooltip("흐른 분을 시계 판에 부채꼴로 표시")] public bool ShowSweep = true;
        [Header("시간 종이 · 우상단")]
        [Tooltip("한 줄 날짜·시각 (Clock의 두 줄을 대신 보여 줍니다 · 턴 연출 중 숫자가 굴러감)")] public Text TimeText;
        [Tooltip("날짜·시각 ({0}: DAY, {1}: 시, {2}: 분)")] public string TimeFormat = "DAY {0}   {1:00}:{2:00}";
        [Tooltip("장소 판 남은 턴 칸 (첫 방문에는 숨김)")] public FieldTurnPips Pips;
        [Tooltip("턴 글 (TurnLabel을 대신 보여 줍니다 · 칸이 비는 순간 새 값으로 바뀜)")] public Text TurnCount;
        [Tooltip("턴 글이 바뀔 때 톡 튀는 크기")] [Range(0, .6f)] public float CountBump = .22f;
        [Tooltip("톡 튀는 시간 (초)")] [Min(.05f)] public float BumpSeconds = .35f;
        [Tooltip("오래 머묾 칸을 지날 때 턴 글 색")] public Color MarkCrossed = new Color(.74f, .17f, .12f, 1);
        [Tooltip("칸이 비는 구간 (연출 비율 0~1: 시작, 끝)")] public Vector2 DrainWindow = new Vector2(.2f, .75f);
        [Tooltip("시각 숫자와 바늘이 도는 구간 (연출 비율 0~1: 시작, 끝)")] public Vector2 RollWindow = new Vector2(.1f, .7f);
        [Header("시각 옆 +분")]
        public CanvasGroup Tick;
        public Text TickLabel;
        [Tooltip("시각 옆 표시 ({0}: 흐른 분)")] public string TickFormat = "+{0}분";
        [Tooltip("떠오르는 높이 (px)")] public float TickRise = 18;
        [Header("소음 고리 · 이번 턴 수색한 사물")]
        public FieldNoiseRipple[] Ripples;
        [Tooltip("사물 하나에 소음 1마다 고리 하나, 최대 개수")] [Min(1)] public int RingsPerSite = 3;
        [Tooltip("고리 사이 시작 간격 (초)")] [Min(0)] public float RingStagger = .14f;
        [Tooltip("고리 하나가 퍼지는 시간 (초)")] [Min(.05f)] public float RingDuration = .62f;
        [Tooltip("고리 시작 크기 배율")] public float RingStartScale = .35f;
        [Tooltip("고리 끝 크기 배율")] public float RingEndScale = 1.6f;
        [Tooltip("고리 색 (알파 = 처음 진하기)")] public Color RingColor = new Color(1f, .79f, .39f, .95f);
        [Header("시간 · 초")]
        [Tooltip("한 턴 연출 전체 길이")] [Min(.1f)] public float Duration = 1f;
        [Tooltip("시계가 튀어나오는 시간")] [Min(0)] public float BannerIn = .2f;
        [Tooltip("시계가 들어가는 시간")] [Min(0)] public float BannerOut = .24f;
        [Tooltip("+분 표시가 떠올라 사라지는 시간")] [Min(.05f)] public float TickDuration = .7f;
        [Tooltip("배속: 모든 시간을 이 값으로 나눕니다 (검수용으로 빠르게)")] [Min(.1f)] public float Speed = 1;

        public bool Playing { get; private set; }
        // Unscaled time the last sequence ended (FieldAutoAdvance waits a gap after it).
        public float LastEnd { get; private set; } = -100;
        public int RipplesPlaying { get { int n = 0; foreach (var r in rings) if (r.Ripple && r.Ripple.gameObject.activeSelf) n++; return n; } }
        // The time paper now: the site board shows pips; the minutes (from day 1 00:00) the time text shows; the sequence's roll.
        public bool Board { get; private set; }
        public int DisplayMinutes { get; private set; }
        public int RollFrom { get; private set; }
        public int RollTo { get; private set; }
        // The pips the current sequence drains ([DrainFrom, DrainTo)) and whether it crossed a 오래 머묾 mark.
        public int DrainFrom { get; private set; }
        public int DrainTo { get; private set; }
        public bool CrossedMark { get; private set; }

        struct Ring { public FieldNoiseRipple Ripple; public float Delay; }
        readonly List<Ring> rings = new List<Ring>();
        static readonly Regex TimePattern = new Regex(@"(\d{1,2}):(\d{2})"), DayPattern = new Regex(@"DAY\s*(\d+)");
        FieldPlanCheck seen; int lastTurns = -1, lastNoise, idleMinutes = -1, idleUsed = -1, expectUsed = -1; float started, clockFrom, minutes, bumpAt = -100;
        Vector2 tickHome, bannerHome; bool homeKnown, bannerKnown, stale = true, bumpMark, inkKnown; string prevCount; Color countInk;

        void OnDisable() { if (Playing) { Playing = false; LastEnd = Time.unscaledTime; FieldTurnPulse.Playing = false; FieldTurnPulse.Progress01 = 1; } stale = true; }

        void LateUpdate()
        {
            if (stale) { stale = false; HideAll(); }
            var a = Arrival;
            if (!a || !a.IsOpen || !a.Rooms) { if (Playing) Finish(); lastTurns = -1; expectUsed = -1; return; }
            int turns = a.Rooms.Turns; var planner = a.Threat ? a.Threat.Planner : null;
            // A new visit resets the count: take it as the baseline.
            if (lastTurns < 0 || turns < lastTurns) { lastTurns = turns; lastNoise = a.Rooms.Noise; seen = planner ? planner.LastCheck : null; expectUsed = -1; }
            else if (turns > lastTurns) { Begin(turns - lastTurns, planner); lastTurns = turns; }
            if (Playing) Animate();
            TimePaper(a);
        }

        void Begin(int passed, FieldTurnPlanner planner)
        {
            var a = Arrival; int noise = Mathf.Max(0, a.Rooms.Noise - lastNoise); lastNoise = a.Rooms.Noise;
            var check = planner ? planner.LastCheck : null; bool fresh = check != null && check != seen; seen = check;
            minutes = passed * a.Rooms.MinutesPerTurn; started = Time.unscaledTime; Playing = true;
            FieldTurnPulse.Playing = true; FieldTurnPulse.Progress01 = 0; FieldTurnPulse.Turn = a.Rooms.Turns; FieldTurnPulse.Version++;
            // The time paper rolls from what it showed to the clock's new value (a clock that could not advance does not roll).
            int now = Absolute(a.Clock ? a.Clock.text : null, -1);
            RollFrom = idleMinutes >= 0 ? idleMinutes : now >= 0 ? now - (int)minutes : 0; RollTo = now >= 0 ? now : RollFrom + (int)minutes; idleMinutes = RollTo;
            float swept = Mathf.Max(0, RollTo - RollFrom) > 0 ? RollTo - RollFrom : minutes;
            clockFrom = Mathf.Repeat(RollFrom, 1440); minutes = swept;
            // Site board: the pips of the turns just spent drain (a move's site turns land after the walk: expect them meanwhile).
            var t = a.Threat; bool board = t && t.Active && t.State != null;
            DrainFrom = DrainTo = 0; CrossedMark = false;
            if (board)
            {
                DrainFrom = idleUsed >= 0 ? idleUsed : Mathf.Max(0, t.State.TurnsUsed - passed); DrainTo = Mathf.Max(t.State.TurnsUsed, DrainFrom + passed);
                expectUsed = DrainTo; idleUsed = DrainTo;
                if (Pips) for (int i = DrainFrom; i < DrainTo; i++) if (Pips.IsMark(i)) CrossedMark = true;
            }
            if (BannerTitle) BannerTitle.text = string.Format(BannerFormat, a.Rooms.Turns, passed * a.Rooms.MinutesPerTurn);
            if (BannerDetail) BannerDetail.text = a.InTransit ? DetailMove : noise > 0 ? string.Format(DetailNoise, noise) : DetailQuiet;
            if (Banner)
            {
                if (!bannerKnown) { bannerHome = ((RectTransform)Banner.transform).anchoredPosition; bannerKnown = true; }
                Banner.alpha = 0; if (!Banner.gameObject.activeSelf) Banner.gameObject.SetActive(true);
            }
            if (BannerClock) { BannerClock.ClockMinutes = clockFrom; BannerClock.SweepFrom = ShowSweep ? clockFrom : -1; }
            if (Tick)
            {
                if (!homeKnown) { tickHome = ((RectTransform)Tick.transform).anchoredPosition; homeKnown = true; }
                if (TickLabel) TickLabel.text = string.Format(TickFormat, passed * a.Rooms.MinutesPerTurn);
                Tick.alpha = 0; if (!Tick.gameObject.activeSelf) Tick.gameObject.SetActive(true);
            }
            foreach (var r in rings) if (r.Ripple) r.Ripple.gameObject.SetActive(false);
            rings.Clear();
            if (fresh && Ripples != null)
                foreach (var run in check.Runs)
                {
                    var anchor = Anchor(run.Site); if (!anchor || run.Noise <= 0) continue;
                    for (int k = 0; k < Mathf.Min(run.Noise, RingsPerSite) && rings.Count < Ripples.Length; k++)
                    {
                        var ripple = Ripples[rings.Count]; if (!ripple) break;
                        ripple.transform.position = anchor.TransformPoint(anchor.rect.center); ripple.color = Faded(0);
                        ripple.transform.localScale = Vector3.one * RingStartScale; ripple.gameObject.SetActive(true);
                        rings.Add(new Ring { Ripple = ripple, Delay = k * RingStagger });
                    }
                }
            Animate();
        }

        float T => (Time.unscaledTime - started) * Mathf.Max(.1f, Speed);
        float Window(Vector2 w) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(w.x, Mathf.Max(w.x + .01f, w.y), FieldTurnPulse.Progress01));
        void Animate()
        {
            var a = Arrival; float t = T, total = Mathf.Max(.1f, Duration);
            FieldTurnPulse.Progress01 = Mathf.Clamp01(t / total);
            // Windows open at once: the clock, tick and rings simply fade while one covers the room.
            bool covered = a.Popup.activeSelf || a.Search && a.Search.IsOpen || a.Loot && a.Loot.IsOpen || a.FieldBags && a.FieldBags.IsOpen || a.Encounter && a.Encounter.IsOpen || a.Story && a.Story.IsOpen;
            if (Banner)
            {
                float fin = Mathf.Clamp01(t / Mathf.Max(.001f, BannerIn)), fout = t > total - BannerOut ? Mathf.Clamp01((total - t) / Mathf.Max(.001f, BannerOut)) : 1;
                Banner.alpha = covered ? 0 : Mathf.Min(fin, fout);
                // Out of the time paper (ease out with a little overshoot), back into it at the end.
                float outOf = Mathf.Min(1 - (1 - fin) * (1 - fin), fout * (2 - fout));
                ((RectTransform)Banner.transform).anchoredPosition = bannerHome + PopFrom * (1 - outOf);
                Banner.transform.localScale = Vector3.one * Mathf.LerpUnclamped(BannerPop, 1, outOf + .1f * Mathf.Sin(fin * Mathf.PI));
            }
            float roll = Window(RollWindow);
            if (BannerClock) BannerClock.ClockMinutes = clockFrom + minutes * roll;
            if (Tick)
            {
                float u = Mathf.Clamp01(t / TickDuration);
                Tick.alpha = covered ? 0 : u < .2f ? u / .2f : 1 - Mathf.Clamp01((u - .6f) / .4f);
                ((RectTransform)Tick.transform).anchoredPosition = tickHome + new Vector2(0, TickRise * Mathf.SmoothStep(0, 1, u));
            }
            foreach (var r in rings)
            {
                if (!r.Ripple) continue; float u = (t - r.Delay) / RingDuration;
                if (u >= 1) { if (r.Ripple.gameObject.activeSelf) r.Ripple.gameObject.SetActive(false); continue; }
                if (u < 0) { r.Ripple.color = Faded(0); continue; }
                r.Ripple.transform.localScale = Vector3.one * Mathf.Lerp(RingStartScale, RingEndScale, 1 - (1 - u) * (1 - u));
                r.Ripple.color = Faded(covered ? 0 : RingColor.a * (1 - u));
            }
            if (t >= total && t >= TickDuration) Finish();
        }

        // The top-right paper every frame: one-line time (rolling while a turn plays), the pips (site board only) and the turn count.
        void TimePaper(ExpeditionArrivalPanel a)
        {
            var t = a.Threat; bool board = t && t.Active && t.State != null; Board = board;
            if (TimeText)
            {
                int shown; string s;
                if (Playing) { shown = RollFrom + Mathf.RoundToInt((RollTo - RollFrom) * Window(RollWindow)); s = Format(shown); }
                else { shown = Absolute(a.Clock ? a.Clock.text : null, -1); s = shown >= 0 ? Format(shown) : (a.Clock ? a.Clock.text.Replace("\n", "   ") : ""); idleMinutes = shown; }
                DisplayMinutes = shown; if (TimeText.text != s) TimeText.text = s;
            }
            if (Pips)
            {
                if (Pips.gameObject.activeSelf != board) Pips.gameObject.SetActive(board);
                if (board)
                {
                    int used = t.State.TurnsUsed; if (a.InTransit && expectUsed > used) used = expectUsed; else if (!a.InTransit) expectUsed = -1;
                    Pips.Set(t.Rules.TurnBudget, used, t.Rules.LingerEvery);
                    if (Playing && DrainTo > DrainFrom) Pips.Drain(DrainFrom, Window(DrainWindow)); else Pips.EndDrain();
                    if (!Playing) idleUsed = used;
                }
                else { idleUsed = -1; Pips.EndDrain(); }
            }
            var label = a.Rooms.TurnLabel; string source = label ? label.text : "";
            if (TurnCount)
            {
                if (!inkKnown) { countInk = TurnCount.color; inkKnown = true; }
                // Until the pip drains, the count still says what it said before the turn; then it ticks to the new value with a bump.
                bool before = Playing && prevCount != null && FieldTurnPulse.Progress01 < (DrainWindow.x + DrainWindow.y) * .5f;
                string want = before ? prevCount : source;
                if (TurnCount.text != want) { if (!string.IsNullOrEmpty(TurnCount.text)) { bumpAt = Time.unscaledTime; bumpMark = Playing && CrossedMark; } TurnCount.text = want; }
                float u = (Time.unscaledTime - bumpAt) / BumpSeconds; bool bump = u >= 0 && u < 1;
                TurnCount.rectTransform.localScale = Vector3.one * (bump ? 1 + CountBump * Mathf.Sin(u * Mathf.PI) : 1);
                var ink = bump && bumpMark ? Color.Lerp(MarkCrossed, countInk, u * u) : countInk; if (TurnCount.color != ink) TurnCount.color = ink;
            }
            if (!Playing) prevCount = source;
        }
        string Format(int absolute) { absolute = Mathf.Max(0, absolute); return string.Format(TimeFormat, absolute / 1440 + 1, absolute % 1440 / 60, absolute % 60); }
        // 'DAY d\nHH:MM' (CampaignState.ClockText) → minutes since day 1 00:00; fallback when it cannot be read.
        static int Absolute(string clock, int fallback)
        {
            if (string.IsNullOrEmpty(clock)) return fallback; var m = TimePattern.Match(clock); if (!m.Success) return fallback;
            var d = DayPattern.Match(clock); int day = d.Success ? int.Parse(d.Groups[1].Value) : 1;
            return (Mathf.Max(1, day) - 1) * 1440 + int.Parse(m.Groups[1].Value) * 60 + int.Parse(m.Groups[2].Value);
        }
        Color Faded(float alpha) => new Color(RingColor.r, RingColor.g, RingColor.b, alpha);

        void Finish()
        {
            Playing = false; LastEnd = Time.unscaledTime; FieldTurnPulse.Playing = false; FieldTurnPulse.Progress01 = 1;
            HideAll();
        }
        void HideAll()
        {
            if (Banner)
            {
                Banner.alpha = 0; Banner.transform.localScale = Vector3.one; if (bannerKnown) ((RectTransform)Banner.transform).anchoredPosition = bannerHome;
                if (Banner.gameObject.activeSelf) Banner.gameObject.SetActive(false);
            }
            if (BannerClock) BannerClock.SweepFrom = -1;
            if (Tick) { Tick.alpha = 0; if (homeKnown) ((RectTransform)Tick.transform).anchoredPosition = tickHome; if (Tick.gameObject.activeSelf) Tick.gameObject.SetActive(false); }
            if (Ripples != null) foreach (var r in Ripples) if (r && r.gameObject.activeSelf) r.gameObject.SetActive(false);
            if (Pips) Pips.EndDrain();
            rings.Clear();
        }
        // The searched object's 40px marker (the den shelf has no button: its office door).
        RectTransform Anchor(int site)
        {
            var a = Arrival; Transform t = null;
            if (a.Threat && site == a.Threat.DenSite) t = a.Rooms.OfficeDoor ? a.Rooms.OfficeDoor.transform : null;
            else if (a.Objects != null && site >= 0 && site < a.Objects.Length && a.Objects[site]) t = a.Objects[site].transform;
            if (!t || !t.gameObject.activeInHierarchy) return null;
            var marker = t.Find("ExplorationMarkerPaper");
            return (marker ? marker : t) as RectTransform;
        }
    }
}
