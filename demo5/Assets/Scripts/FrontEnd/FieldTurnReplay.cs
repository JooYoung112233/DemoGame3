using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Plays a short sequence whenever an exploration turn passes (first visit and site board alike): a banner
    // '<N>턴 · 10분 흐름', a '+10분' tick at the clock and noise rings at the objects searched this turn.
    // Presentation only: the turn has already resolved, nothing waits for it, and windows open at once
    // (the banner only fades while one covers the room). A new turn while playing restarts it. Writes FieldTurnPulse.
    [DefaultExecutionOrder(100)]
    public sealed class FieldTurnReplay : MonoBehaviour
    {
        public ExpeditionArrivalPanel Arrival;
        [Header("턴 띠")]
        public CanvasGroup Banner;
        public Text BannerTitle, BannerDetail;
        public FieldClockGlyph BannerClock;
        [Tooltip("띠 제목 ({0}: 이번 방문 턴, {1}: 흐른 분)")] public string BannerFormat = "{0}턴 · {1}분 흐름";
        [Tooltip("띠 둘째 줄: 소음 ({0}: 이번 턴 소음)")] public string DetailNoise = "소음 +{0}";
        [Tooltip("띠 둘째 줄: 소음이 없을 때")] public string DetailQuiet = "소리 없이";
        [Tooltip("띠 둘째 줄: 원정대가 방을 옮길 때")] public string DetailMove = "원정대 이동";
        [Tooltip("띠가 나타날 때 커지는 시작 배율")] [Range(.5f, 1)] public float BannerPop = .92f;
        [Header("시계 옆 +분")]
        public CanvasGroup Tick;
        public Text TickLabel;
        [Tooltip("시계 옆 표시 ({0}: 흐른 분)")] public string TickFormat = "+{0}분";
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
        [Tooltip("띠가 나타나는 시간")] [Min(0)] public float BannerIn = .2f;
        [Tooltip("띠가 사라지는 시간")] [Min(0)] public float BannerOut = .24f;
        [Tooltip("+분 표시가 떠올라 사라지는 시간")] [Min(.05f)] public float TickDuration = .7f;
        [Tooltip("배속: 모든 시간을 이 값으로 나눕니다 (검수용으로 빠르게)")] [Min(.1f)] public float Speed = 1;

        public bool Playing { get; private set; }
        // Unscaled time the last sequence ended (FieldAutoAdvance waits a gap after it).
        public float LastEnd { get; private set; } = -100;
        public int RipplesPlaying { get { int n = 0; foreach (var r in rings) if (r.Ripple && r.Ripple.gameObject.activeSelf) n++; return n; } }

        struct Ring { public FieldNoiseRipple Ripple; public float Delay; }
        readonly List<Ring> rings = new List<Ring>();
        FieldPlanCheck seen; int lastTurns = -1, lastNoise; float started, clockFrom, minutes; Vector2 tickHome; bool homeKnown, stale = true;

        void OnDisable() { if (Playing) { Playing = false; LastEnd = Time.unscaledTime; FieldTurnPulse.Playing = false; FieldTurnPulse.Progress01 = 1; } stale = true; }

        void LateUpdate()
        {
            if (stale) { stale = false; HideAll(); }
            var a = Arrival;
            if (!a || !a.IsOpen || !a.Rooms) { if (Playing) Finish(); lastTurns = -1; return; }
            int turns = a.Rooms.Turns; var planner = a.Threat ? a.Threat.Planner : null;
            // A new visit resets the count: take it as the baseline.
            if (lastTurns < 0 || turns < lastTurns) { lastTurns = turns; lastNoise = a.Rooms.Noise; seen = planner ? planner.LastCheck : null; }
            else if (turns > lastTurns) { Begin(turns - lastTurns, planner); lastTurns = turns; }
            if (Playing) Animate();
        }

        void Begin(int passed, FieldTurnPlanner planner)
        {
            var a = Arrival; int noise = Mathf.Max(0, a.Rooms.Noise - lastNoise); lastNoise = a.Rooms.Noise;
            var check = planner ? planner.LastCheck : null; bool fresh = check != null && check != seen; seen = check;
            minutes = passed * a.Rooms.MinutesPerTurn; started = Time.unscaledTime; Playing = true;
            FieldTurnPulse.Playing = true; FieldTurnPulse.Progress01 = 0; FieldTurnPulse.Turn = a.Rooms.Turns; FieldTurnPulse.Version++;
            if (BannerTitle) BannerTitle.text = string.Format(BannerFormat, a.Rooms.Turns, minutes);
            if (BannerDetail) BannerDetail.text = a.InTransit ? DetailMove : noise > 0 ? string.Format(DetailNoise, noise) : DetailQuiet;
            if (Banner) { Banner.alpha = 0; if (!Banner.gameObject.activeSelf) Banner.gameObject.SetActive(true); }
            if (BannerClock) { clockFrom = (ClockOf(a.Clock, out int now) ? now : a.Rooms.Turns * a.Rooms.MinutesPerTurn) - minutes; BannerClock.ClockMinutes = clockFrom; }
            if (Tick)
            {
                if (!homeKnown) { tickHome = ((RectTransform)Tick.transform).anchoredPosition; homeKnown = true; }
                if (TickLabel) TickLabel.text = string.Format(TickFormat, minutes);
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

        void Animate()
        {
            var a = Arrival; float t = (Time.unscaledTime - started) * Mathf.Max(.1f, Speed), total = Mathf.Max(.1f, Duration);
            FieldTurnPulse.Progress01 = Mathf.Clamp01(t / total);
            // Windows open at once: the banner, tick and rings simply fade while one covers the room.
            bool covered = a.Popup.activeSelf || a.Search && a.Search.IsOpen || a.Loot && a.Loot.IsOpen || a.FieldBags && a.FieldBags.IsOpen || a.Encounter && a.Encounter.IsOpen || a.Story && a.Story.IsOpen;
            if (Banner)
            {
                float fade = t < BannerIn ? t / Mathf.Max(.001f, BannerIn) : t > total - BannerOut ? (total - t) / Mathf.Max(.001f, BannerOut) : 1;
                Banner.alpha = covered ? 0 : Mathf.Clamp01(fade);
                Banner.transform.localScale = Vector3.one * Mathf.Lerp(BannerPop, 1, Mathf.Clamp01(t / Mathf.Max(.001f, BannerIn)));
            }
            if (BannerClock) BannerClock.ClockMinutes = clockFrom + minutes * Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / (total * .7f)));
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
        Color Faded(float alpha) => new Color(RingColor.r, RingColor.g, RingColor.b, alpha);
        // The day paper's 'HH:MM' (already advanced by this turn), for the banner clock's hands only.
        static bool ClockOf(Text clock, out int minutes)
        {
            minutes = 0; var m = clock ? Regex.Match(clock.text, @"(\d{1,2}):(\d{2})") : Match.Empty;
            if (!m.Success) return false; minutes = int.Parse(m.Groups[1].Value) * 60 + int.Parse(m.Groups[2].Value); return true;
        }

        void Finish()
        {
            Playing = false; LastEnd = Time.unscaledTime; FieldTurnPulse.Playing = false; FieldTurnPulse.Progress01 = 1;
            HideAll();
        }
        void HideAll()
        {
            if (Banner) { Banner.alpha = 0; Banner.transform.localScale = Vector3.one; if (Banner.gameObject.activeSelf) Banner.gameObject.SetActive(false); }
            if (Tick) { Tick.alpha = 0; if (homeKnown) ((RectTransform)Tick.transform).anchoredPosition = tickHome; if (Tick.gameObject.activeSelf) Tick.gameObject.SetActive(false); }
            if (Ripples != null) foreach (var r in Ripples) if (r && r.gameObject.activeSelf) r.gameObject.SetActive(false);
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
