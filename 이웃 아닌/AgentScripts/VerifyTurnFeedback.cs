using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Cause = Demo5.FrontEnd.FieldTurnWarning.Cause;

// 턴 진행 연출 (2026-09-25): the time paper's pips drain one per turn and '남은 N턴' ticks down, a clock pops out and sweeps the minutes
// while the digits roll, and the red '무언가가 다가옵니다' strip shows exactly when something can appear next turn (site board: its
// announced step into the party's room / a reserved move meeting it; first visit: the next search's encounter chance at the
// threshold), never otherwise. Wiring(): edit or play mode after BuildTurnClockPaper.Run. Play mode after VerifyFieldTurnPlan.Enter
// (a fresh game), in this order: FirstVisit → Board → Warning (or All). Later-visit fixtures: Threat.ReviewWake with the opening
// chapter off; quiet turns are '턴 진행' with nobody placed (말 놓기: the hush turn; FieldIdleConfirm.AutoAccept, no question); a move is
// every pawn at one door (FieldPawnTest), then '턴 진행'. Stills in Temp/TurnFeedbackCapture.
public static class VerifyTurnFeedback
{
    const string ArrivalPath = "Assets/Prefabs/Settlement/ExpeditionArrivalPanel.prefab", ScreenPath = "Assets/Prefabs/Settlement/SettlementScreen.prefab";
    const float TrayTop = 776;
    static readonly Rect PlacePaper = new Rect(42, 24, 422, 124), DayPaper = new Rect(1485, 24, 400, 124), Strip = new Rect(500, 158, 580, 84), ClockPop = new Rect(1178, 30, 300, 112);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    public static string Wiring()
    {
        var done = new List<string>();
        foreach (var path in new[] { ArrivalPath, ScreenPath })
            foreach (var a in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<ExpeditionArrivalPanel>(true))
            {
                var main = a.Main.transform; var flow = main.Find("TurnFlow"); string at = Path.GetFileNameWithoutExtension(path) + ": ";
                var r = flow.GetComponent<FieldTurnReplay>(); var w = flow.GetComponent<FieldTurnWarning>();
                Check(r && r.TimeText && r.TimeText.name == "DayTime" && r.Pips && r.TurnCount && r.TurnCount.name == "TurnCount" && r.BannerClock && r.ShowSweep, at + "replay wiring: run BuildTurnClockPaper.Run");
                Check(w && w.Arrival == a && w.Strip && w.Strip.name == "TurnWarning" && w.Title && w.Line && w.Edge && w.ShrinkTarget == a.Status.rectTransform && w.LegacyBanner, at + "warning wiring");
                Check(!w.Strip.gameObject.activeSelf && !w.Strip.blocksRaycasts && !w.Strip.interactable && w.Strip.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget) && !w.Edge.raycastTarget, at + "the warning takes no clicks and starts hidden");
                Check(!a.Clock.enabled && !a.Rooms.TurnLabel.enabled && !main.Find("TurnPaper").gameObject.activeSelf, at + "the time paper mirrors Clock / TurnLabel");
                Check(main.Find("HudTray").GetComponent<FieldHudTray>().Warning == w, at + "FieldHudTray.Warning");
                // The room area (FieldRoomArea on Main since 말 놓기; the retired bubbles' list while it is not there) keeps off the strip and the clock.
                var area = a.Main.GetComponent<FieldRoomArea>(); var always = area ? area.KeepClearAlways : main.GetComponentInChildren<FieldPlanTargetMarkers>(true).KeepClearAlways;
                Check(always != null && always.Contains((RectTransform)w.Strip.transform) && always.Contains((RectTransform)r.Banner.transform), at + "the room keeps off the strip and the clock");
                Check(w.TitleText == "무언가가 다가옵니다" && r.Pips.Count == 24 && r.Pips.MarkEvery == 8, at + "texts / pips");
                done.Add(at.TrimEnd(' ', ':'));
            }
        return "PASS wiring: " + string.Join(", ", done);
    }

    // ---- helpers (as VerifyFieldTurnFlow) ----
    static async Task Tap(Button button)
    {
        for (int i = 0; i < 20 && button && !(button.IsActive() && button.IsInteractable()); i++) await Task.Delay(50); // enabled on the next LateUpdate
        Check(button && button.IsActive() && button.IsInteractable(), "Unavailable " + (button ? button.name : "null")); Hit(button);
        var r = (RectTransform)button.transform; var data = new PointerEventData(EventSystem.current) { position = Screen(r), button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler); await Task.Delay(100);
    }
    static Vector2 Screen(RectTransform r) { Canvas.ForceUpdateCanvases(); return RectTransformUtility.WorldToScreenPoint(r.GetComponentInParent<Canvas>().worldCamera, r.TransformPoint(r.rect.center)); }
    static void Hit(Selectable target)
    {
        var data = new PointerEventData(EventSystem.current) { position = Screen((RectTransform)target.transform) }; var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Selectable>() == target, "Blocked " + target.name + " by " + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
    }
    static async Task Until(Func<bool> done, int ms, string what) { var w = Stopwatch.StartNew(); while (!done()) { if (w.ElapsedMilliseconds > ms) throw new Exception("Timeout: " + what); await Task.Delay(20); } }
    static string Shots => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "TurnFeedbackCapture"));
    static Task Still(MonoBehaviour host, string name) { var done = new TaskCompletionSource<bool>(); host.StartCoroutine(StillRoutine(name, done)); return done.Task; }
    static IEnumerator StillRoutine(string name, TaskCompletionSource<bool> done)
    {
        yield return new WaitForEndOfFrame();
        var raw = new RenderTexture(UnityEngine.Screen.width, UnityEngine.Screen.height, 0); var rt = new RenderTexture(raw.width, raw.height, 0); ScreenCapture.CaptureScreenshotIntoRenderTexture(raw);
        if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(raw, rt, new Vector2(1, -1), new Vector2(0, 1)); else Graphics.Blit(raw, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new UnityEngine.Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); RenderTexture.active = prev; Object.Destroy(raw); Object.Destroy(rt);
        Directory.CreateDirectory(Shots); File.WriteAllBytes(Path.Combine(Shots, name + ".png"), tex.EncodeToPNG()); Object.Destroy(tex); done.SetResult(true);
    }
    static void Fits(params Text[] texts) { Canvas.ForceUpdateCanvases(); foreach (var t in texts) if (t && t.isActiveAndEnabled) Check(t.preferredHeight <= t.rectTransform.rect.height + 1, "Overflow " + t.name + ": " + t.text); }
    static SettlementController Owner() { var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Run VerifyFieldTurnPlan.Enter first"); return c; }
    static Rect OnMain(ExpeditionArrivalPanel a, RectTransform r)
    {
        var main = (RectTransform)a.Main.transform; var c = new Vector3[4]; r.GetWorldCorners(c);
        Vector3 lo = main.InverseTransformPoint(c[0]), hi = main.InverseTransformPoint(c[2]);
        return new Rect(lo.x - main.rect.xMin, main.rect.yMax - hi.y, hi.x - lo.x, hi.y - lo.y);
    }
    static (FieldTurnReplay r, FieldTurnWarning w, FieldHudTray hud) Parts(ExpeditionArrivalPanel a)
    {
        var flow = a.Main.transform.Find("TurnFlow"); var r = flow.GetComponent<FieldTurnReplay>(); var w = flow.GetComponent<FieldTurnWarning>(); var hud = a.Main.transform.Find("HudTray").GetComponent<FieldHudTray>();
        Check(r && w && hud && r.Pips && r.TimeText && r.TurnCount, "Run BuildTurnClockPaper.Run first"); return (r, w, hud);
    }
    static string Time(int minutes, FieldTurnReplay r) => string.Format(r.TimeFormat, minutes / 1440 + 1, minutes % 1440 / 60, minutes % 60);
    static int Minutes(SettlementController c) => (c.Campaign.Day - 1) * 1440 + c.Campaign.MinuteOfDay;
    static async Task CloseLoot(ExpeditionArrivalPanel a) { if (a.Loot.IsOpen) { await Tap(a.Loot.Back); await Task.Delay(120); if (a.Loot.LeaveReview.activeSelf) await Tap(a.Loot.LeaveConfirm); await Task.Delay(150); } }
    // The hush turn (말 놓기: the '모두 숨죽이기' button is gone): nobody placed, then '턴 진행' (AutoAccept: no idle question).
    static async Task Hush(ExpeditionArrivalPanel a) { FieldPawnTest.Clear(a); FieldIdleConfirm.AutoAccept = true; await Tap(a.Threat.Planner.TurnButton); }
    // Every pawn at the door onto `next` (the last one sets the move), then '턴 진행'.
    static async Task MoveTo(ExpeditionArrivalPanel a, int next) { await Until(() => FieldPawnTest.Ready(a), 3000, "board ready"); Check(FieldPawnTest.Gather(a, next), "Everyone at the door: " + FieldPawnTest.Describe(a)); await Tap(a.Threat.Planner.TurnButton); }
    static async Task<ExpeditionArrivalPanel> Arrived(SettlementController c)
    {
        if (c.ArrivalPanel.IsOpen) return c.ArrivalPanel;
        await Tap(c.Exit); var plan = c.ExpeditionPanel; int mall = Array.FindIndex(plan.Destinations, d => d.Id == "mall"); if (mall >= 0 && plan.Markers != null && mall < plan.Markers.Length) plan.Markers[mall].onClick.Invoke(); await Task.Delay(100);
        foreach (var card in plan.Cards) if (!card.Check.gameObject.activeSelf) await Tap(card.Button);
        await Tap(plan.Pack); await Tap(c.PackingPanel.Ready); await Tap(c.PackingPanel.Depart); await Task.Delay(1100);
        Check(c.ArrivalPanel.IsOpen && !c.ArrivalPanel.InTransit, "Arrived"); return c.ArrivalPanel;
    }
    static async Task Depart(SettlementController c)
    {
        if (c.ReturnPanel && c.ReturnPanel.IsOpen) { c.ReturnPanel.Close(); await Task.Delay(100); }
        Check(c.ArrivalPanel.Begin(c.Campaign.Party.ToArray(), c.ExpeditionPanel.Destinations.First(d => d.Id == "mall")), "Departure to the mall");
        await Until(() => c.ArrivalPanel.IsOpen && !c.ArrivalPanel.InTransit, 3000, "arrival"); await Task.Delay(250);
    }
    // A later visit in the arcade, the board reset to the given danger / noise / site clock, nothing assigned.
    static async Task<ExpeditionArrivalPanel> Board(SettlementController c, int danger, int gauge, int clock)
    {
        var a = c.ArrivalPanel; if (c.Opening) c.Opening.State.Enabled = false;
        if (a.IsOpen) { await CloseLoot(a); for (int i = 0; i < 3 && a.Search.IsOpen; i++) { a.Search.Escape(); await Task.Delay(120); } if (a.Popup.activeSelf) a.ClosePopup(); await Until(() => !a.InTransit, 6000, "walk"); }
        if (a.IsOpen && a.Encounter.IsOpen) { Check(a.FinishReturn(), "Leave the encounter fixture"); await Task.Delay(150); }
        if (!(a.IsOpen && a.Threat.Active && a.Threat.IntroAcknowledged && a.Rooms.CurrentRoom == FieldSiteState.Arcade))
        {
            if (a.IsOpen) { Check(a.FinishReturn(), "Return home"); await Task.Delay(150); }
            await Depart(c);
            if (!a.Threat.Active) { if (a.Popup.activeSelf) a.ClosePopup(); Check(a.FinishReturn(), "Return from the first visit"); await Task.Delay(150); await Depart(c); }
            await Until(() => a.Popup.activeSelf || a.Threat.IntroAcknowledged, 3000, "place-board intro");
            if (a.Popup.activeSelf) await Tap(a.PopupBack);
        }
        FieldIdleConfirm.AutoAccept = true; // quiet turns: '턴 진행' with nobody placed hushes without the question (VerifyIdleConfirm tests it)
        if (a.Rooms.HasQueuedMove) a.Rooms.CancelQueuedMove(); var board = FieldPawnTest.Board(a); if (board) board.Cancel();
        a.Threat.Planner.Clear(); a.Threat.ReviewWake(danger, gauge, clock); await Task.Delay(350); if (a.Popup.activeSelf) await Tap(a.PopupBack);
        Check(a.Threat.Planner.Active && a.Rooms.CurrentRoom == FieldSiteState.Arcade && !a.InTransit && !a.Threat.Planner.Plan.HasAssignments, "Later-visit board in the arcade");
        return a;
    }
    // What the strip must say now, from the game's own facts (independently of FieldTurnWarning).
    static bool Expected(ExpeditionArrivalPanel a, FieldTurnWarning w)
    {
        var t = a.Threat;
        if (t.Active) return a.Rooms.HasQueuedMove ? a.Rooms.QueuedMoveOutlook()?.Encounter == true : t.State.Incoming;
        var e = a.Encounter; if (e.IsOpen || e.Cooldown > 0 || !e.Warned) return false;
        return e.ChanceAt(e.Searches + 1, a.Rooms.Noise) >= w.FirstVisitChance; // by search turns (+ noise) since 2026-09-25
    }
    static void Exact(ExpeditionArrivalPanel a, FieldTurnWarning w, string when)
    {
        bool want = Expected(a, w); Check(w.Condition == want, when + ": warning condition " + w.Condition + " but the facts say " + want);
        Check(!w.StripShowing || w.Condition, when + ": the strip shows while nothing threatens");
    }
    // The strip, when up, keeps off the papers, the tray, the clock, the pawn board's pins and chips, and the door tags.
    static void StripClear(ExpeditionArrivalPanel a, FieldTurnWarning w, string when)
    {
        Canvas.ForceUpdateCanvases(); var s = OnMain(a, (RectTransform)w.Strip.transform);
        Check(!s.Overlaps(PlacePaper) && !s.Overlaps(DayPaper) && s.yMax <= TrayTop && !s.Overlaps(ClockPop), when + ": the strip " + s + " covers a paper, the tray or the clock");
        var board = FieldPawnTest.Board(a);
        if (board)
        {
            foreach (var pin in board.Pins.Where(p => p && p.Visible)) Check(!OnMain(a, pin.HitArea).Overlaps(s), when + ": the strip covers a pin");
            foreach (var row in board.ChipRows.Where(x => x && x.gameObject.activeInHierarchy)) Check(!OnMain(a, row).Overlaps(s), when + ": the strip covers a role chip row");
        }
        foreach (var t in a.Threat.DoorMarkers.Where(x => x && x.gameObject.activeInHierarchy)) Check(!OnMain(a, (RectTransform)t.transform).Overlaps(s), when + ": the strip covers the door tag " + t.Label.text);
    }
    // Watch one turn sequence: samples of the pips, the count, the clock and the time digits until it ends.
    sealed class Watch { public readonly List<float> Fill = new List<float>(), Sweep = new List<float>(), Alpha = new List<float>(), Scale = new List<float>(); public readonly List<int> Minutes = new List<int>(); public readonly List<string> Counts = new List<string>(); public bool Edge, MarkInk; }
    static async Task<Watch> Sequence(ExpeditionArrivalPanel a, FieldTurnReplay r, FieldTurnWarning w, int pip, string still)
    {
        var x = new Watch(); bool shot = false; var sw = Stopwatch.StartNew();
        await Until(() => r.Playing, 1000, "turn sequence");
        while (r.Playing && sw.ElapsedMilliseconds < 4000)
        {
            if (pip >= 0) x.Fill.Add(r.Pips.FillOf(pip));
            x.Sweep.Add(r.BannerClock.SweepDegrees); x.Alpha.Add(r.Banner.alpha); x.Minutes.Add(r.DisplayMinutes); x.Counts.Add(r.TurnCount.text); x.Scale.Add(r.TurnCount.rectTransform.localScale.x);
            x.Edge |= w.EdgePlaying; x.MarkInk |= r.TurnCount.color == r.MarkCrossed || Vector4.Distance(r.TurnCount.color, r.MarkCrossed) < .25f;
            if (!shot && still != null && FieldTurnPulse.Progress01 > .4f && FieldTurnPulse.Progress01 < .8f && (pip < 0 || x.Fill.Any(v => v > .05f && v < .95f))) { shot = true; Fits(r.BannerTitle, r.BannerDetail, r.TimeText, r.TurnCount); await Still(a, still); continue; }
            await Task.Delay(15);
        }
        Check(!r.Playing, "The sequence did not end");
        return x;
    }
    static void Monotone(List<float> v, bool down, string what)
    {
        for (int i = 1; i < v.Count; i++) Check(down ? v[i] <= v[i - 1] + .001f : v[i] >= v[i - 1] - .001f, what + " went back: " + string.Join(" ", v.Select(f => f.ToString("0.00"))));
    }

    // ---- play mode ----
    // First visit: no pips, '탐험 N턴' counts up; the clock and the rolling digits play on a move; after a loud search the strip
    // warns exactly when the next search's chance reaches the threshold, then shrinks into '이번 턴' (the risk line turns red).
    public static async Task<string> FirstVisit()
    {
        var c = Owner(); var a = await Arrived(c); var (r, w, hud) = Parts(a); var t = a.Threat; var log = new List<string>();
        Check(!t.Active && t.State.Asleep, "Needs the first visit (a fresh game: VerifyFieldTurnPlan.Enter)");
        await Task.Delay(200);
        Check(!r.Board && !r.Pips.gameObject.activeSelf && r.TurnCount.text == a.Rooms.TurnLabel.text && r.TurnCount.text.StartsWith("탐험"), "First visit: no pips, the game's own count '" + r.TurnCount.text + "'");
        Check(r.TimeText.text == Time(Minutes(c), r), "Time paper '" + r.TimeText.text + "' for " + c.Campaign.ClockText);
        int shows = w.ShowCount; Exact(a, w, "arrival"); Check(!w.Condition && !w.Strip.gameObject.activeSelf, "No warning on arrival");
        // A move: the clock pops out, sweeps 10 minutes, the digits roll, the count goes up.
        int turns = a.Rooms.Turns, from = Minutes(c); string before = r.TurnCount.text;
        await MoveTo(a, FieldSiteState.Corridor);
        var x = await Sequence(a, r, w, -1, "01-first-visit-clock");
        Check(x.Alpha.Max() > .9f && x.Sweep.Max() >= 59 && x.Sweep.Any(v => v > 5 && v < 55), "Clock: alpha " + x.Alpha.Max() + ", sweep " + string.Join(" ", x.Sweep.Select(v => v.ToString("0"))));
        Monotone(x.Sweep, false, "Clock sweep");
        Check(x.Minutes.First() <= from + 2 && x.Minutes.Any(m => m > from && m < from + a.Rooms.MinutesPerTurn) && x.Minutes.Last() == from + a.Rooms.MinutesPerTurn, "Digits roll " + from + " → " + string.Join(",", x.Minutes.Distinct()));
        await Until(() => !a.InTransit, 6000, "corridor"); await Task.Delay(200);
        Check(r.TurnCount.text == a.Rooms.TurnLabel.text && r.TurnCount.text != before && r.TimeText.text == Time(Minutes(c), r), "After the move: '" + r.TurnCount.text + "', '" + r.TimeText.text + "'");
        log.Add("move: clock swept " + x.Sweep.Max().ToString("0") + "°, digits " + Time(from, r) + " → " + r.TimeText.text + ", count '" + before + "' → '" + r.TurnCount.text + "'");
        await Until(() => FieldPawnTest.Ready(a), 3000, "corridor board"); await MoveTo(a, FieldSiteState.Arcade); await Until(() => !a.InTransit && a.Rooms.CurrentRoom == FieldSiteState.Arcade, 6000, "arcade"); await Until(() => !r.Playing, 3000, "sequence end");
        Exact(a, w, "after the moves"); Check(w.ShowCount == shows, "Moves are quiet: no strip");
        // Two searches (the crate, then the table; two pawns on each = co-op: one turn each): the encounter warns after WarnSearches search
        // turns (2026-09-25: by search turns, not noise; the old banner); the next search could meet something.
        foreach (int site in new[] { 0, 1 })
        {
            await Until(() => FieldPawnTest.Ready(a), 3000, "board ready");
            Check(FieldPawnTest.Coop(a, site), "Two pawns on " + a.ObjectNames[site] + ": " + FieldPawnTest.Describe(a));
            await Tap(t.Planner.TurnButton); await Task.Delay(300);
            Exact(a, w, "after search " + site + " (a window open)"); Check(!w.StripShowing, "Nothing shows over a window");
            await CloseLoot(a); if (a.Search.IsOpen) await Tap(a.Search.Back); await Task.Delay(200);
        }
        int chance = FieldTurnWarning.NextSearchChance(a); Exact(a, w, "after the search");
        if (w.Condition)
        {
            await Until(() => w.StripShowing, 1500, "strip");
            Check(w.Reason == Cause.FirstVisit && w.Title.text == w.TitleText && w.Line.text == w.LineFirstVisit && w.LegacyBanner.alpha == 0, "First-visit strip: " + w.Title.text + " / " + w.Line.text + " (old banner alpha " + w.LegacyBanner.alpha + ")");
            StripClear(a, w, "first visit"); Fits(w.Title, w.Line); await Task.Delay(250); await Still(a, "02-first-visit-warning");
            await Until(() => !w.StripShowing && !w.Shrinking, (int)(w.HoldSeconds * 1000) + 1500, "shrinks into '이번 턴'");
            Check(w.Condition && hud.Risk.color == hud.WarnColor, "The risk line keeps the warning in red"); await Still(a, "03-first-visit-warning-in-panel");
            log.Add("search → chance " + chance + "% ≥ " + w.FirstVisitChance + "% → '" + w.LineFirstVisit + "', shrank into '" + hud.Risk.text + "'");
        }
        else { Check(w.ShowCount == shows, "No strip below the threshold"); log.Add("search → chance " + chance + "% < " + w.FirstVisitChance + "%: no strip"); }
        return "PASS first visit · " + string.Join(" · ", log) + " · stills " + Shots;
    }

    // Site board: a hush turn ('턴 진행' with nobody placed) spends one turn: the first pip flashes and drains, '남은 24턴' → '남은 23턴' with a bump, the clock sweeps
    // 10 minutes; nothing threatens, so no strip. Crossing the 8th pip flashes the count red.
    public static async Task<string> BoardTurn()
    {
        var c = Owner(); var a = await Board(c, 0, 0, 0); var (r, w, hud) = Parts(a); var t = a.Threat; var log = new List<string>();
        await Task.Delay(200);
        Check(r.Board && r.Pips.gameObject.activeSelf && r.Pips.Count == t.Rules.TurnBudget && r.Pips.Used == 0 && Enumerable.Range(0, r.Pips.Count).All(i => r.Pips.FillOf(i) == 1), "24 full pips");
        Check(r.TurnCount.text == a.Rooms.TurnLabel.text && r.TurnCount.text == string.Format(t.TimeFormat, t.Rules.TurnBudget), "'" + r.TurnCount.text + "'");
        Check(r.Pips.IsMark(7) && r.Pips.IsMark(15) && r.Pips.IsMark(23) && !r.Pips.IsMark(0), "오래 머묾 marks every 8");
        await Still(a, "04-board-time-paper");
        int shows = w.ShowCount, from = Minutes(c); string before = r.TurnCount.text;
        await Hush(a);
        var x = await Sequence(a, r, w, 0, "05-board-pip-draining");
        Check(r.Pips.Used == 1 && r.Pips.FillOf(0) == 0 && Enumerable.Range(1, r.Pips.Count - 1).All(i => r.Pips.FillOf(i) == 1), "One pip spent");
        Check(x.Fill.First() > .99f && x.Fill.Any(v => v > .05f && v < .95f) && x.Fill.Last() < .01f, "Pip drained: " + string.Join(" ", x.Fill.Select(v => v.ToString("0.00")))); Monotone(x.Fill, true, "Pip fill");
        Check(x.Counts.First() == before && x.Counts.Last() == string.Format(t.TimeFormat, t.Rules.TurnBudget - 1) && x.Scale.Max() > 1.05f, "Count " + string.Join(" → ", x.Counts.Distinct()) + ", bump " + x.Scale.Max().ToString("0.00"));
        Check(x.Sweep.Max() >= 59 && x.Minutes.Last() == from + a.Rooms.MinutesPerTurn && x.Alpha.Max() > .9f, "Clock and digits: sweep " + x.Sweep.Max() + ", minutes " + x.Minutes.Last());
        Exact(a, w, "quiet hush"); Check(w.ShowCount == shows && !w.StripShowing, "Nothing threatens: no strip");
        await Still(a, "06-board-after-turn");
        log.Add("pip 1 drained " + string.Join(" ", x.Fill.Where((v, i) => i % 3 == 0).Select(v => v.ToString("0.0"))) + ", '" + before + "' → '" + r.TurnCount.text + "', clock " + x.Sweep.Max().ToString("0") + "°");
        // The 8th pip (오래 머묾): its drain flashes the count red.
        a = await Board(c, 0, 0, 7); await Task.Delay(200); Check(r.Pips.Used == 7, "Clock at 7: " + r.Pips.Used);
        await Hush(a); x = await Sequence(a, r, w, 7, "07-long-stay-pip");
        Check(r.CrossedMark && r.DrainFrom == 7 && r.DrainTo == 8 && x.MarkInk, "Crossing the 8th pip flashes the count");
        log.Add("8th pip crossed, count flashed red");
        return "PASS board turn · " + string.Join(" · ", log) + " · stills " + Shots;
    }

    // The strip on the board: exactly while its step into the room is announced (hush passes → '숨죽이면 지나갈 수 있습니다';
    // danger 3 → '쫓아오고 있습니다'), and while a gathered move would meet it; never on quiet turns; off the papers, tray and bubbles;
    // the status line hides while the strip says the same thing and carries it after it shrinks.
    public static async Task<string> Warning()
    {
        var c = Owner(); var log = new List<string>();
        // 1. Announced step, hushing passes (danger ≤ 2): at 위험도 2 a loud sound here draws it out of its den (it stops to listen
        //    after each step); hush until its next step is into the arcade.
        var a = await Board(c, 2, 0, 0); var (r, w, hud) = Parts(a); var t = a.Threat; int shows = w.ShowCount;
        t.HearBattle(3); await Task.Delay(150); Exact(a, w, "after the noise");
        for (int i = 0; i < 4 && !t.State.Incoming; i++) { Check(!w.StripShowing, "Quiet hush " + i + ": no strip"); await Hush(a); await Until(() => !r.Playing, 3000, "sequence"); await Task.Delay(150); Exact(a, w, "hush " + i); }
        Check(t.State.Incoming, "Fixture: its next step is into the arcade (resident " + t.State.ResidentRoom + " → " + t.State.Next + ")");
        await Until(() => w.StripShowing, 1500, "strip");
        Check(w.Reason == Cause.Incoming && w.Line.text == w.LineIncoming && w.Title.text == w.TitleText && w.ShowCount == shows + 1, "Incoming strip: " + w.Reason + " / " + w.Line.text);
        Check(w.SuppressStatus && !a.Status.enabled, "The status line steps aside while the strip says it");
        StripClear(a, w, "incoming"); Fits(w.Title, w.Line); Hit(t.Planner.TurnButton);
        bool edge = false; for (int i = 0; i < 20 && !edge; i++) { edge = w.EdgePlaying; await Task.Delay(20); } Check(edge, "The screen edge pulses");
        await Task.Delay(200); await Still(a, "08-incoming-warning");
        await Until(() => !w.StripShowing && !w.Shrinking, (int)(w.HoldSeconds * 1000) + 1500, "shrinks into '이번 턴'");
        Check(a.Status.enabled && hud.StatusWarns && a.Status.text == t.AutoStopLine && a.Status.color == hud.WarnColor && hud.StatusWarn.activeSelf && w.Condition, "The status line carries the warning: '" + a.Status.text + "'");
        await Still(a, "09-warning-in-panel"); log.Add("incoming → '" + w.LineIncoming + "', then '" + a.Status.text.Replace("\n", " / ") + "' in '이번 턴'");
        // 2. A gathered move that would meet it (every pawn at the door): the strip says so; a pawn leaving brings back the step warning.
        await Until(() => FieldPawnTest.Ready(a), 3000, "board ready"); Check(FieldPawnTest.Gather(a, FieldSiteState.Corridor), "Everyone at the door"); await Task.Delay(200);
        Check(a.Rooms.HasQueuedMove, "Move gathered"); Exact(a, w, "move gathered");
        if (w.Condition)
        {
            await Until(() => w.StripShowing, 1500, "move strip"); Check(w.Reason == Cause.MoveMeets && w.Line.text == w.LineMove, "Move strip: " + w.Line.text);
            StripClear(a, w, "move"); await Still(a, "10-move-meets-warning"); log.Add("gathered move meets it → '" + w.LineMove + "'");
        }
        else log.Add("the gathered move does not meet it: no strip");
        Check(FieldPawnTest.Unassign(a, 1), "A pawn off the door"); await Task.Delay(200);
        Check(!a.Rooms.HasQueuedMove, "Move cancelled"); Exact(a, w, "cancelled");
        // 3. Everyone hushes: it walks in and passes by; the next step leaves: nothing more to warn about.
        await Hush(a); await Until(() => !r.Playing, 3000, "sequence"); await Task.Delay(200);
        Check(t.State.PassedBy, "Passed by the hushed party"); Exact(a, w, "passed by"); Check(!w.StripShowing || t.State.Incoming, "No strip after it passed");
        log.Add("passed by → " + (w.Condition ? "still coming" : "quiet"));
        // 4. Danger 3 (it hunts): the same announced step, but hushing does not let it pass.
        a = await Board(c, 3, 0, 0); shows = w.ShowCount;
        for (int i = 0; i < 4 && !t.State.Incoming; i++) { await Hush(a); await Until(() => !r.Playing, 3000, "sequence"); await Task.Delay(150); Exact(a, w, "hunt hush " + i); if (a.Encounter.IsOpen) break; }
        Check(t.State.Incoming && !a.Encounter.IsOpen, "Fixture: hunting, its next step is into the arcade");
        await Until(() => w.StripShowing, 1500, "hunt strip");
        Check(w.Reason == Cause.Hunt && w.Line.text == w.LineHunt, "Hunt strip: " + w.Line.text); StripClear(a, w, "hunt");
        await Task.Delay(200); await Still(a, "11-hunt-warning"); log.Add("danger 3 → '" + w.LineHunt + "'");
        a.Rooms.CancelQueuedMove(); t.ReviewWake(0, 0, 0); await Task.Delay(300); Exact(a, w, "reset"); Check(!w.Condition, "A quiet board: no warning");
        return "PASS warning · " + string.Join(" · ", log) + " · stills " + Shots;
    }

    public static async Task<string> All()
    {
        var r = new List<string> { Wiring(), await FirstVisit(), await BoardTurn(), await Warning() };
        return string.Join("\n", r);
    }
}
