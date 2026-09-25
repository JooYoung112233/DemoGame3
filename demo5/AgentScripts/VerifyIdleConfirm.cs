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
using Mode = Demo5.FrontEnd.FieldIdleConfirm.Mode;

// 할 일이 없는 대원 확인 (2026-09-25, 기획/탐험-화면정리와-행동칸-1차.md §4): '턴 진행' asks when a living member has nothing to do.
// Wiring(): edit or play mode, after BuildIdleConfirm.Run and the FieldTurnPlanner.Run hook (patch 01).
// Play mode after VerifyFieldTurnPlan.Enter (a fresh game, two members), in this order: Ask → KeepGoing → NoAsk (or All).
// Fixture as VerifyFieldPlanMarkers: a real later visit (home once after the sleeping first visit, the place-board intro closed)
// reset with Threat.ReviewWake(0, 0, 0), opening chapter off; two unsearched tool-free objects in the arcade (crate, table).
// FieldIdleConfirm.AutoAccept is off while these run (restored after). Stills in Temp/IdleConfirmCapture.
// 말 놓기 (2026-09-25): members are placed as pawns (FieldPawnTest: the board's own rules); nobody placed is the hush turn (the question's
// all-idle text is the user's); a move is every living pawn at one door, which never asks.
public static class VerifyIdleConfirm
{
    const string ArrivalPath = "Assets/Prefabs/Settlement/ExpeditionArrivalPanel.prefab", ScreenPath = "Assets/Prefabs/Settlement/SettlementScreen.prefab";
    const string PlannerSource = "Assets/Scripts/FrontEnd/FieldTurnPlanner.cs";
    const int A = FieldSiteState.Arcade, C = FieldSiteState.Corridor;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    public static string Wiring()
    {
        var done = new List<string>();
        foreach (var path in new[] { ArrivalPath, ScreenPath })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path); Check(root, "Missing " + path); int n = 0;
            foreach (var a in root.GetComponentsInChildren<ExpeditionArrivalPanel>(true))
            {
                var ic = a.GetComponent<FieldIdleConfirm>(); Check(ic, path + ": FieldIdleConfirm missing on the arrival panel: run BuildIdleConfirm.Run");
                var pl = a.GetComponent<FieldTurnPlanner>(); Check(pl && pl.TurnButton, path + ": FieldTurnPlanner must sit on the same object (the Run hook finds the question there)");
                var auto = a.Main ? a.Main.GetComponentInChildren<FieldAutoAdvance>(true) : null;
                Check(ic.Arrival == a && auto && ic.Auto == auto && auto.IdleConfirm == ic, path + ": FieldIdleConfirm.Arrival/Auto or FieldAutoAdvance.IdleConfirm not wired");
                Check(a.Popup && a.PopupTitle && a.PopupBody && a.ReturnConfirm && a.PopupBack, path + ": the popup frame (title, body, confirm, back)");
                Check(new[] { ic.Title, ic.BodyAll, ic.ConfirmLabel, ic.BackLabel, ic.KeepGoingBodyAll, ic.KeepGoingConfirmLabel, ic.NameSeparator }.All(s => !string.IsNullOrEmpty(s)), path + ": an empty text");
                Check(Formats(ic.BodyFormat, 2) && Formats(ic.KeepGoingBodyFormat, 2) && Formats(ic.MoreFormat, 2), path + ": a text format ({0} names, {1} 이/가 or count)");
                n++;
            }
            Check(n > 0, path + ": no arrival panel"); done.Add(path.Substring(path.LastIndexOf('/') + 1) + " ×" + n);
        }
        // The hook (patch 01): after the reserved-move line, so a reserved move never asks.
        var src = File.ReadAllText(PlannerSource); int queued = src.IndexOf("arrival.Rooms.RunQueuedMove()", StringComparison.Ordinal), hook = src.IndexOf("FieldIdleConfirm.AskFirst(this)", StringComparison.Ordinal);
        Check(hook > 0 && queued > 0 && hook > queued, "FieldTurnPlanner.Run hook missing or before the reserved-move line: apply patches/01-turn-planner-idle-confirm.py");
        var ic0 = AssetDatabase.LoadAssetAtPath<GameObject>(ArrivalPath).GetComponent<FieldIdleConfirm>();
        return "PASS wiring: " + string.Join(", ", done) + "; hook after the reserved move · '" + ic0.Title + "' / '" + ic0.ConfirmLabel + "' / '" + ic0.BackLabel + "' / keep going '" + ic0.KeepGoingConfirmLabel + "'";
    }
    static bool Formats(string f, int args) { try { var x = string.Format(f, "윤서진", "이"); return !string.IsNullOrEmpty(x) && f.Contains("{0}") && (args < 2 || f.Contains("{1}")); } catch (FormatException) { return false; } }

    // ---- helpers (as VerifyFieldTurnFlow / VerifyFieldPlanMarkers) ----
    static async Task Tap(Button button)
    {
        Check(button && button.IsActive() && button.IsInteractable(), "Unavailable " + (button ? button.name : "null")); Hit(button);
        var r = (RectTransform)button.transform; var data = new PointerEventData(EventSystem.current) { position = Screen(r), button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler); await Task.Delay(100);
    }
    static Vector2 Screen(RectTransform r) { Canvas.ForceUpdateCanvases(); return RectTransformUtility.WorldToScreenPoint(r.GetComponentInParent<Canvas>().worldCamera, r.TransformPoint(r.rect.center)); }
    // The topmost thing under the centre belongs to this button (no click).
    static void Hit(Selectable target)
    {
        var data = new PointerEventData(EventSystem.current) { position = Screen((RectTransform)target.transform) }; var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Selectable>() == target, "Blocked " + target.name + " by " + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
    }
    static async Task Until(Func<bool> done, int ms, string what) { var w = Stopwatch.StartNew(); while (!done()) { if (w.ElapsedMilliseconds > ms) throw new Exception("Timeout: " + what); await Task.Delay(30); } }
    static string Shots => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "IdleConfirmCapture"));
    static Task Still(MonoBehaviour host, string name) { var done = new TaskCompletionSource<bool>(); host.StartCoroutine(StillRoutine(name, done)); return done.Task; }
    static IEnumerator StillRoutine(string name, TaskCompletionSource<bool> done)
    {
        yield return new WaitForEndOfFrame();
        var raw = new RenderTexture(UnityEngine.Screen.width, UnityEngine.Screen.height, 0); var rt = new RenderTexture(raw.width, raw.height, 0); ScreenCapture.CaptureScreenshotIntoRenderTexture(raw);
        if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(raw, rt, new Vector2(1, -1), new Vector2(0, 1)); else Graphics.Blit(raw, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new UnityEngine.Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); RenderTexture.active = prev; Object.Destroy(raw); Object.Destroy(rt);
        Directory.CreateDirectory(Shots); File.WriteAllBytes(Path.Combine(Shots, name + ".png"), tex.EncodeToPNG()); Object.Destroy(tex); done.SetResult(true);
    }
    static void Fits(params Text[] texts) { Canvas.ForceUpdateCanvases(); foreach (var t in texts) if (t && t.isActiveAndEnabled && !t.resizeTextForBestFit) Check(t.preferredHeight <= t.rectTransform.rect.height + 1 && (t.horizontalOverflow == HorizontalWrapMode.Wrap || t.preferredWidth <= t.rectTransform.rect.width + 1), "Overflow " + t.name + ": " + t.text); }
    static Text LabelText(Button b) => b.GetComponentInChildren<Text>(true);
    static string Label(Button b) => LabelText(b).text;
    static SettlementController Owner() { var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Run VerifyFieldTurnPlan.Enter first"); return c; }
    static FieldIdleConfirm Confirm(ExpeditionArrivalPanel a) { var ic = a.GetComponent<FieldIdleConfirm>(); Check(ic && ic.isActiveAndEnabled, "Run BuildIdleConfirm.Run first"); return ic; }
    static FieldAutoAdvance Auto(ExpeditionArrivalPanel a) { var f = a.Main.GetComponentInChildren<FieldAutoAdvance>(true); Check(f && f.Toggle, "Run BuildFieldTurnFlow.Run first"); return f; }
    static async Task CloseLoot(ExpeditionArrivalPanel a) { if (a.Loot.IsOpen) { await Tap(a.Loot.Back); await Task.Delay(120); if (a.Loot.LeaveReview.activeSelf) await Tap(a.Loot.LeaveConfirm); await Task.Delay(150); } }
    // Every living pawn at the door onto `next` (the last one sets the move). No time.
    static async Task Gather(ExpeditionArrivalPanel a, int next) { await Until(() => FieldPawnTest.Ready(a), 3000, "board ready"); Check(FieldPawnTest.Gather(a, next), "Everyone at the door: " + FieldPawnTest.Describe(a)); await Task.Delay(150); }
    static async Task Settle(ExpeditionArrivalPanel a)
    {
        Check(!(a.Encounter && a.Encounter.IsOpen), "An encounter is open: finish it before this check");
        if (a.Story && a.Story.IsOpen) a.Story.Close();
        await CloseLoot(a);
        for (int i = 0; i < 3 && a.Search.IsOpen; i++) { a.Search.Escape(); await Task.Delay(120); }
        if (a.FieldBags && a.FieldBags.IsOpen) a.FieldBags.Close();
        if (a.Popup.activeSelf) a.ClosePopup();
        await Until(() => !a.InTransit, 6000, "walk");
    }
    static async Task Depart(SettlementController c)
    {
        if (c.ReturnPanel && c.ReturnPanel.IsOpen) { c.ReturnPanel.Close(); await Task.Delay(100); }
        var mall = c.ExpeditionPanel.Destinations.First(d => d.Id == "mall");
        Check(c.ArrivalPanel.Begin(c.Campaign.Party.ToArray(), mall), "Departure to the mall");
        await Until(() => c.ArrivalPanel.IsOpen && !c.ArrivalPanel.InTransit, 3000, "arrival"); await Task.Delay(200);
    }
    // A real later visit in the arcade, reset to a quiet board with nothing assigned.
    static async Task<ExpeditionArrivalPanel> Ready(SettlementController c)
    {
        var a = c.ArrivalPanel; if (c.Opening) c.Opening.State.Enabled = false;
        if (a.IsOpen) await Settle(a);
        bool later = a.IsOpen && a.Threat.Active && a.Threat.IntroAcknowledged && a.Rooms.CurrentRoom == A;
        if (!later)
        {
            if (a.IsOpen) { Check(a.FinishReturn(), "Return home"); await Task.Delay(150); }
            await Depart(c);
            // The first visit sleeps (no board): home once more and back for a later visit.
            if (!a.Threat.Active) { if (a.Popup.activeSelf) a.ClosePopup(); Check(a.FinishReturn(), "Return from the first visit"); await Task.Delay(150); await Depart(c); }
            await Until(() => a.Popup.activeSelf || a.Threat.IntroAcknowledged, 3000, "place-board intro");
            if (a.Popup.activeSelf) await Tap(a.PopupBack);
        }
        if (a.Rooms.HasQueuedMove) a.Rooms.CancelQueuedMove();
        await Quiet(a);
        return a;
    }
    // The next step builds on the arcade board the previous one left; otherwise a fresh Ready().
    static async Task<ExpeditionArrivalPanel> Continue(SettlementController c)
    {
        var a = c.ArrivalPanel; if (c.Opening) c.Opening.State.Enabled = false;
        if (a.IsOpen) await Settle(a);
        if (a.IsOpen && a.Threat.Active && a.Threat.IntroAcknowledged && a.Rooms.CurrentRoom == A && !a.InTransit) { if (a.Rooms.HasQueuedMove) a.Rooms.CancelQueuedMove(); await Task.Delay(100); return a; }
        return await Ready(c);
    }
    // A quiet board (위험도 0, noise 0, site clock 0) with nothing assigned; the searched objects keep their progress.
    static async Task Quiet(ExpeditionArrivalPanel a)
    {
        var board = FieldPawnTest.Board(a); if (board) board.Cancel();
        a.Threat.ReviewWake(0, 0, 0); await Task.Delay(250); if (a.Popup.activeSelf) await Tap(a.PopupBack);
        var pl = a.Threat.Planner; await Until(() => FieldPawnTest.Ready(a), 3000, "board ready");
        Check(pl.Active && a.Threat.IntroAcknowledged && !pl.Plan.HasAssignments && !pl.Plan.HasGathers && !a.Rooms.HasQueuedMove && a.Rooms.CurrentRoom == A && !a.InTransit, "Later-visit board in the arcade with nothing placed");
    }
    // An unsearched object in this room that needs no tool.
    static int Fresh(ExpeditionArrivalPanel a, int except)
    {
        for (int i = 0; i < a.ObjectNames.Length && i < a.Loot.Sites.Length; i++)
        {
            if (i == except || i == 3 || !a.Loot.IsSiteInCurrentRoom(i) || !string.IsNullOrEmpty(a.Loot.Sites[i].RequiredTool) || !a.Threat.CanSearchSite(i)) continue;
            if (!a.Loot.Peek(i, out var s) || s.Progress == 0 && !s.Complete) return i;
        }
        throw new Exception("Fixture: no unsearched object in this room (start from VerifyFieldTurnPlan.Enter)");
    }
    // Turns an order still needs (a new one here is alone: the object's base turns; no pace since 2026-09-25).
    static int Left(ExpeditionArrivalPanel a, FieldOrder o) => a.Loot.Peek(o.Site, out var s) && s.Progress > 0 ? s.Required - s.Progress : a.Loot.SiteTurns(o.Site);
    // One member (the lowest free) searches the object alone: a pawn placed there (말 놓기); no time. pace: unused (no pace since 2026-09-25).
    static async Task Assign(ExpeditionArrivalPanel a, int site, int pace)
    {
        var pl = a.Threat.Planner; await Until(() => FieldPawnTest.Ready(a), 3000, "board ready");
        int m = FieldPawnTest.Free(a); if (m < 0) m = 0;
        Check(FieldPawnTest.Lead(a, m, site) && pl.Plan.Find(site) != null, "Pawn " + m + " on " + a.ObjectNames[site] + ": " + FieldPawnTest.Describe(a));
        await Task.Delay(120);
    }
    // Everything a question must not change.
    static string Clock(ExpeditionArrivalPanel a, SettlementController c) => $"turns {a.Rooms.Turns} minute {c.Campaign.MinuteOfDay} site {a.Threat.State.TurnsUsed} gauge {a.Threat.State.Gauge} noise {a.Rooms.Noise} transit {a.InTransit} orders {a.Threat.Planner.Plan.Orders.Count}";
    // The popup frame: title, body, confirm and back readable and clickable; no listen offer; the room behind takes no click.
    static void Frame(ExpeditionArrivalPanel a, FieldTurnPlanner pl, FieldIdleConfirm ic, string confirm)
    {
        Check(a.ReturnConfirm.gameObject.activeInHierarchy && a.PopupBack.gameObject.activeInHierarchy && Label(a.ReturnConfirm) == confirm && Label(a.PopupBack) == ic.BackLabel, "Buttons: '" + Label(a.ReturnConfirm) + "' / '" + Label(a.PopupBack) + "'");
        Check(!pl.ListenButton || !pl.ListenButton.gameObject.activeSelf, "No listen offer in the question");
        Check(!a.Main.interactable && !a.Main.blocksRaycasts, "The room behind takes no click");
        Hit(a.ReturnConfirm); Hit(a.PopupBack); Fits(a.PopupTitle, a.PopupBody, LabelText(a.ReturnConfirm), LabelText(a.PopupBack));
    }
    static async Task<string> Asking(Func<Task<string>> run)
    {
        Check(Application.isPlaying, "Play first"); bool was = FieldIdleConfirm.AutoAccept; FieldIdleConfirm.AutoAccept = false;
        try { return await run(); } finally { FieldIdleConfirm.AutoAccept = was; }
    }

    // ---- Play mode ----
    // '턴 진행' with everyone idle asks (all line), '돌아가기' costs nothing; with one member searching the other is named; two presses
    // in one frame ask once; '숨죽이고 진행' is exactly one turn, as the button.
    public static Task<string> Ask() => Asking(async () =>
    {
        var c = Owner(); var a = await Ready(c); var t = a.Threat; var pl = t.Planner; var ic = Confirm(a); var log = new List<string>();
        var living = Enumerable.Range(0, a.Participants.Count).Where(m => a.Participants[m].Health > 0).ToList();
        Check(living.Count >= 2, "Needs two living members");
        Check(ic.Particle("윤서진") == ic.ParticleConsonant && ic.Particle("장도윤 외 2명") == ic.ParticleConsonant && ic.Particle("김소이") == ic.ParticleVowel && ic.Particle("Kim") == ic.ParticleOther, "이/가 after the last syllable");
        string backLabel = Label(a.PopupBack), clock = Clock(a, c); int asked = ic.Asked;
        // 1. Nobody placed: everyone would hush (the user's all-idle text).
        await Tap(pl.TurnButton);
        Check(a.Popup.activeSelf && ic.Asking == Mode.Turn && ic.Asked == asked + 1, "'턴 진행' asks (asking " + ic.Asking + ", popup " + a.Popup.activeSelf + ")");
        Check(ic.LastIdle.SequenceEqual(living) && a.PopupTitle.text == ic.Title && a.PopupBody.text == ic.BodyAll && ic.BodyAll == "아무도 할 일이 없습니다.\n모두 숨죽이고 진행할까요?", "All idle: " + a.PopupTitle.text + " / " + a.PopupBody.text);
        Frame(a, pl, ic, ic.ConfirmLabel);
        Check(Clock(a, c) == clock, "Asking costs nothing: " + clock + " → " + Clock(a, c));
        await Still(a, "01-ask-nobody-assigned"); log.Add("all idle '" + a.PopupBody.text.Replace("\n", " ") + "'");
        await Tap(a.PopupBack); await Task.Delay(150);
        Check(!a.Popup.activeSelf && ic.Asking == Mode.None && Clock(a, c) == clock && Label(a.PopupBack) == backLabel, "Back: closed, no time: " + Clock(a, c));
        log.Add("'" + ic.BackLabel + "' = no time");

        // 2. One member searches alone (the object's 2 turns): the others are named.
        int x = Fresh(a, -1); await Assign(a, x, 2); int lead = pl.Plan.Find(x).Lead;
        var idle = FieldIdleConfirm.IdleMembers(pl);
        Check(idle.SequenceEqual(living.Where(m => m != lead)), "Idle = everyone but the lead: " + string.Join(",", idle));
        string names = string.Join(ic.NameSeparator, idle.Take(ic.MaxNames).Select(m => a.Participants[m].Name));
        if (idle.Count > ic.MaxNames) names = string.Format(ic.MoreFormat, names, idle.Count - ic.MaxNames);
        string body = string.Format(ic.BodyFormat, names, ic.Particle(names));
        clock = Clock(a, c); asked = ic.Asked;
        await Tap(pl.TurnButton);
        Check(a.Popup.activeSelf && ic.Asking == Mode.Turn && ic.Asked == asked + 1 && ic.LastIdle.SequenceEqual(idle) && a.PopupBody.text == body, "Named: '" + a.PopupBody.text + "' want '" + body + "'");
        Frame(a, pl, ic, ic.ConfirmLabel);
        await Still(a, "02-ask-named"); log.Add("'" + a.PopupBody.text.Replace("\n", " ") + "'");
        await Tap(a.PopupBack); await Task.Delay(150);
        Check(!a.Popup.activeSelf && Clock(a, c) == clock && pl.Plan.Find(x) != null, "Back again: no time, the order kept");

        // 3. Two presses in one frame: one question, no turn.
        await Task.Delay(100); asked = ic.Asked;
        pl.TurnButton.onClick.Invoke(); pl.TurnButton.onClick.Invoke(); await Task.Delay(150);
        Check(a.Popup.activeSelf && ic.Asking == Mode.Turn && ic.Asked == asked + 1 && Clock(a, c) == clock, "Two presses in one frame: one question, no turn");
        log.Add("double press → one question");

        // 4. '숨죽이고 진행': exactly one turn, as the button (the search runs, the idle member hushes).
        int turns = a.Rooms.Turns, minute = c.Campaign.MinuteOfDay, used = t.State.TurnsUsed, version = FieldTurnPulse.Version;
        await Tap(a.ReturnConfirm); await Task.Delay(250);
        Check(!a.Popup.activeSelf && ic.Asking == Mode.None && a.Rooms.Turns == turns + 1 && c.Campaign.MinuteOfDay == minute + a.Rooms.MinutesPerTurn && t.State.TurnsUsed == used + 1, "Confirm = one turn: " + Clock(a, c));
        a.Loot.Peek(x, out var sx);
        Check(sx != null && sx.Progress == 1 && sx.Required == a.Loot.SiteTurns(x) && pl.LastMismatch == "" && pl.Plan.Find(x) != null, "The search ran once, preview = result " + pl.LastMismatch);
        Check(ic.Asked == asked + 1 && Label(a.PopupBack) == backLabel, "No second question; the back label restored");
        if (a.Main.GetComponentInChildren<FieldTurnReplay>(true)) Check(FieldTurnPulse.Version == version + 1, "One turn sequence");
        await Task.Delay(150); await Still(a, "03-confirmed-one-turn"); log.Add("'" + ic.ConfirmLabel + "' → one turn (" + a.ObjectNames[x] + " 1/" + sx.Required + ")");
        return "PASS ask · " + string.Join(" · ", log) + " · stills " + Shots;
    });

    // '계속 진행' asks when switched on ('돌아가기' leaves it off, no time); after '숨죽이고 계속' it runs turn after turn until the
    // search completes and never asks again.
    public static Task<string> KeepGoing() => Asking(async () =>
    {
        var c = Owner(); var a = await Continue(c); var t = a.Threat; var pl = t.Planner; var ic = Confirm(a); var auto = Auto(a); var log = new List<string>();
        Check(!auto.On, "'계속 진행' off");
        // Fixture: a solo search with 2+ turns left and a member with nothing to do (Ask leaves one; otherwise a fresh one).
        var order = pl.Plan.Orders.FirstOrDefault(o => Left(a, o) >= 2);
        if (order == null || FieldIdleConfirm.IdleMembers(pl).Count == 0) { await Quiet(a); int s0 = Fresh(a, -1); await Assign(a, s0, 2); order = pl.Plan.Find(s0); }
        int site = order.Site, left = Left(a, order); var idle = FieldIdleConfirm.IdleMembers(pl, out int living);
        Check(left >= 2 && idle.Count > 0, "Fixture: " + left + " turns left, idle " + idle.Count);
        string body = ic.BodyFor(idle, idle.Count >= living, true), clock = Clock(a, c); int asked = ic.Asked;
        // 1. Switching it on asks first.
        await Tap(auto.Toggle);
        Check(a.Popup.activeSelf && ic.Asking == Mode.KeepGoing && ic.Asked == asked + 1 && !auto.On, "Switching on asks first (on " + auto.On + ", asking " + ic.Asking + ")");
        Check(a.PopupTitle.text == ic.Title && a.PopupBody.text == body && ic.LastIdle.SequenceEqual(idle), "Keep-going question: " + a.PopupBody.text);
        Frame(a, pl, ic, ic.KeepGoingConfirmLabel);
        await Still(a, "04-keep-going-ask"); log.Add("on → '" + a.PopupBody.text.Replace("\n", " ") + "'");
        await Tap(a.PopupBack); await Task.Delay(400);
        Check(!a.Popup.activeSelf && !auto.On && auto.AutoTurns == 0 && Clock(a, c) == clock, "Back: still off, no time: " + Clock(a, c));
        log.Add("'" + ic.BackLabel + "' → off");
        // 2. On again, '숨죽이고 계속': turn after turn until the search completes, never asking again.
        await Tap(auto.Toggle); Check(ic.Asking == Mode.KeepGoing && ic.Asked == asked + 2, "Asked again after '" + ic.BackLabel + "'");
        int turns = a.Rooms.Turns; bool again = false;
        bool Watch() { if (ic.Asking != Mode.None || a.Popup.activeSelf && a.PopupTitle.text == ic.Title) again = true; return again; }
        await Tap(a.ReturnConfirm);
        Check(auto.On && !a.Popup.activeSelf, "On after '" + ic.KeepGoingConfirmLabel + "' (stop '" + auto.LastStop + "')");
        await Until(() => { Watch(); return auto.AutoTurns >= 1 || !auto.On; }, 5000, "first auto turn"); await Task.Delay(150);
        await Still(a, "05-keep-going-running");
        await Until(() => { Watch(); return !auto.On; }, 5000 * left, "keep going");
        Check(!again && ic.Asked == asked + 2, "Never asked per turn (asked " + (ic.Asked - asked) + ")");
        Check(a.Rooms.Turns == turns + left && auto.AutoTurns == left && auto.LastStop == string.Format(auto.StopSearchDone, a.ObjectNames[site]), "Ran " + (a.Rooms.Turns - turns) + "/" + left + " turns, stop: " + auto.LastStop);
        await CloseLoot(a); log.Add("'" + ic.KeepGoingConfirmLabel + "' → " + left + " turns without asking, stop '" + auto.LastStop + "'");
        return "PASS keep going · " + string.Join(" · ", log) + " · stills " + Shots;
    });

    // No question when every living member has a job (a pawn searching, a pawn listening at the exit), when every pawn is at one door
    // ('턴 진행' is the move: nobody is idle), or when '계속 진행' starts with everyone at a door.
    public static Task<string> NoAsk() => Asking(async () =>
    {
        var c = Owner(); var a = await Continue(c); var t = a.Threat; var pl = t.Planner; var n = a.Rooms; var ic = Confirm(a); var auto = Auto(a); var log = new List<string>();
        await Quiet(a); int asked = ic.Asked;
        // 1. Everyone has a job: one searches, the other's pawn listens at the exit.
        int y = Fresh(a, -1); await Assign(a, y, 2); int other = FieldPawnTest.Free(a);
        Check(other >= 0 && FieldPawnTest.Listen(a, other, C), "The other pawn listens at the exit: " + FieldPawnTest.Describe(a));
        Check(pl.Plan.Listens.Count == 1 && FieldIdleConfirm.IdleMembers(pl).Count == 0, "Fixture: every living member has a job (a two-member party from VerifyFieldTurnPlan.Enter)");
        int turns = n.Turns;
        await Tap(pl.TurnButton); await Task.Delay(200);
        Check(!a.Popup.activeSelf && ic.Asked == asked && n.Turns == turns + 1 && pl.LastMismatch == "", "All placed: no question, one turn (" + Clock(a, c) + ")");
        await CloseLoot(a); await Still(a, "06-all-assigned-no-ask"); log.Add("all placed → one turn, no question");
        // 2. The listener taken off (someone idle again), then every pawn at the door: '턴 진행' is the move, no question.
        Check(FieldPawnTest.Unassign(a, other) && pl.Plan.Listens.Count == 0 && FieldIdleConfirm.IdleMembers(pl).Count > 0, "Listener off: someone has nothing to do");
        await Gather(a, C);
        Check(n.HasQueuedMove && !a.Popup.activeSelf && FieldIdleConfirm.IdleMembers(pl).Count == 0, "Everyone at the door: nobody is idle");
        turns = n.Turns; await Tap(pl.TurnButton);
        Check(ic.Asked == asked && !a.Popup.activeSelf && a.InTransit && n.Turns == turns + 1, "Gathered move: no question, the turn is the move");
        await Still(a, "07-gathered-move-no-ask");
        await Until(() => !a.InTransit && n.CurrentRoom == C, 6000, "to the corridor"); await Task.Delay(300);
        log.Add("gathered move → no question");
        // 3. '계속 진행' with everyone at the back door: no question; it makes the move and stops.
        await Gather(a, A);
        Check(n.HasQueuedMove && n.QueuedRoom == A, "Everyone at the back door");
        turns = n.Turns; await Tap(auto.Toggle);
        Check(ic.Asked == asked && ic.Asking == Mode.None && !(a.Popup.activeSelf && a.PopupTitle.text == ic.Title), "Keep going with a gathered move: no question");
        await Until(() => !auto.On, 8000, "auto move");
        Check(n.CurrentRoom == A && n.Turns == turns + 1 && auto.LastStop == auto.StopRoom && ic.Asked == asked, "Auto move: " + auto.LastStop + " room " + n.CurrentRoom);
        await Until(() => !a.InTransit, 6000, "walk");
        log.Add("keep going + gathered move → no question, stop '" + auto.LastStop + "'");
        return "PASS no question · " + string.Join(" · ", log) + " · stills " + Shots;
    });

    public static async Task<string> All()
    {
        var r = new List<string> { await Ask(), await KeepGoing(), await NoAsk() };
        return string.Join("\n", r);
    }
}
