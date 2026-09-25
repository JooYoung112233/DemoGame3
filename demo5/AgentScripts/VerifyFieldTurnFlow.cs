using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Live turn flow (2026-09-25): the turn sequence, '계속 진행' and the next-turn move on the real screen.
// 말 놓기 (2026-09-25, 기획/탐험-말놓기-조작-재설계.md): members are placed as pawns (FieldPawnTest: the board's own rules), the move is
// every living pawn at one door (then '턴 진행'), on both visits; the door popup reserves nothing.
// Play mode after VerifyFieldTurnPlan.Enter (a fresh game), in this order: FirstVisit → Replay → KeepGoing → Reservation (or All).
// Later-visit fixtures use Threat.ReviewWake with the opening chapter off. Stills in Temp/FieldTurnFlowCapture.
// Temporary layout: none of these elements has an approved mock.
public static class VerifyFieldTurnFlow
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    const int A = FieldSiteState.Arcade, C = FieldSiteState.Corridor, S = FieldSiteState.Storage;

    // ---- helpers (as VerifyFieldTurnPlan) ----
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
    static string Shots => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "FieldTurnFlowCapture"));
    static Task Still(MonoBehaviour host, string name) { var done = new TaskCompletionSource<bool>(); host.StartCoroutine(StillRoutine(name, done)); return done.Task; }
    static IEnumerator StillRoutine(string name, TaskCompletionSource<bool> done)
    {
        yield return new WaitForEndOfFrame();
        var raw = new RenderTexture(UnityEngine.Screen.width, UnityEngine.Screen.height, 0); var rt = new RenderTexture(raw.width, raw.height, 0); ScreenCapture.CaptureScreenshotIntoRenderTexture(raw);
        if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(raw, rt, new Vector2(1, -1), new Vector2(0, 1)); else Graphics.Blit(raw, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new UnityEngine.Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); RenderTexture.active = prev; Object.Destroy(raw); Object.Destroy(rt);
        Directory.CreateDirectory(Shots); File.WriteAllBytes(Path.Combine(Shots, name + ".png"), tex.EncodeToPNG()); Object.Destroy(tex); done.SetResult(true);
    }
    static void Fits(params Text[] texts) { Canvas.ForceUpdateCanvases(); foreach (var t in texts) if (t && t.isActiveAndEnabled) Check(t.preferredHeight <= t.rectTransform.rect.height + 1 && (t.horizontalOverflow == HorizontalWrapMode.Wrap || t.preferredWidth <= t.rectTransform.rect.width + 1), "Overflow " + t.name + ": " + t.text); }
    static string Label(Button b) => b.GetComponentInChildren<Text>().text;
    static SettlementController Owner() { var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Run VerifyFieldTurnPlan.Enter first"); return c; }
    static async Task<ExpeditionArrivalPanel> Arrived(SettlementController c)
    {
        if (c.ArrivalPanel.IsOpen) return c.ArrivalPanel;
        await Tap(c.Exit); var plan = c.ExpeditionPanel; int mall = Array.FindIndex(plan.Destinations, d => d.Id == "mall"); if (mall >= 0 && plan.Markers != null && mall < plan.Markers.Length) plan.Markers[mall].onClick.Invoke(); await Task.Delay(100);
        foreach (var card in plan.Cards) if (!card.Check.gameObject.activeSelf) await Tap(card.Button);
        await Tap(plan.Pack); await Tap(c.PackingPanel.Ready); await Tap(c.PackingPanel.Depart); await Task.Delay(1100);
        Check(c.ArrivalPanel.IsOpen && !c.ArrivalPanel.InTransit, "Arrived"); return c.ArrivalPanel;
    }
    static (FieldTurnReplay replay, FieldAutoAdvance auto, FieldMoveQueueView view) Flow(ExpeditionArrivalPanel a)
    {
        var r = a.Main.GetComponentInChildren<FieldTurnReplay>(true); var f = a.Main.GetComponentInChildren<FieldAutoAdvance>(true); var v = a.Main.GetComponentInChildren<FieldMoveQueueView>(true);
        Check(r && f && v && r.Banner && r.Tick && f.Toggle && f.Stamp && f.Replay == r && v.Arrival == a, "Run BuildFieldTurnFlow.Run first");
        return (r, f, v);
    }
    static async Task CloseLoot(ExpeditionArrivalPanel a) { if (a.Loot.IsOpen) { await Tap(a.Loot.Back); await Task.Delay(120); if (a.Loot.LeaveReview.activeSelf) await Tap(a.Loot.LeaveConfirm); await Task.Delay(150); } }
    // Every living pawn at the door onto `next` (the last one sets the move). No time.
    static async Task Gather(ExpeditionArrivalPanel a, int next) { Check(FieldPawnTest.Gather(a, next), "Everyone at the door to " + FieldSiteState.RoomNames[next] + ": " + FieldPawnTest.Describe(a)); await Task.Delay(150); }
    // Later-visit fixture: the opening chapter is over and the board is awake with nothing placed (nothing held).
    static async Task Board(SettlementController c, ExpeditionArrivalPanel a, int danger, int gauge, int clock)
    {
        if (c.Opening) c.Opening.State.Enabled = false;
        await CloseLoot(a); if (a.Search.IsOpen) await Tap(a.Search.Back); if (a.Popup.activeSelf) await Tap(a.PopupBack);
        var board = FieldPawnTest.Board(a); if (board) board.Cancel();
        a.Threat.ReviewWake(danger, gauge, clock); await Task.Delay(250); if (a.Popup.activeSelf) await Tap(a.PopupBack);
        await Until(() => FieldPawnTest.Ready(a), 3000, "board ready");
        Check(a.Threat.Planner.Active && !a.Threat.Planner.Plan.HasAssignments && !a.Threat.Planner.Plan.HasGathers && !a.Rooms.HasQueuedMove, "Board awake, nothing placed");
    }
    // One member (the lowest free) searches the object alone: a pawn placed there (말 놓기), no time. pace: unused (no pace since 2026-09-25).
    static async Task Assign(ExpeditionArrivalPanel a, int site, int pace)
    {
        var pl = a.Threat.Planner; int m = FieldPawnTest.Free(a); if (m < 0) m = 0; int turns = a.Rooms.Turns;
        Check(FieldPawnTest.Lead(a, m, site), "Pawn " + m + " on " + a.ObjectNames[site] + ": " + FieldPawnTest.Describe(a));
        await Task.Delay(120);
        Check(pl.Plan.Find(site) != null && a.Rooms.Turns == turns, "Placed on " + a.ObjectNames[site] + " without time");
    }
    static string Snap(ExpeditionArrivalPanel a, SettlementController c)
    {
        var s = a.Threat.State; var pl = a.Threat.Planner;
        string Site(int i) => a.Loot.Peek(i, out var x) ? x.Progress + "/" + x.Required + (x.Complete ? "!" : "") : "-";
        return $"{s.PartyRoom},{s.TurnsUsed},{s.Gauge},{s.Danger},{s.Remembered},{s.ResidentRoom},{s.Resident},{s.Next},{s.Encounter},{s.PassedBy},{s.LastNoise} | room {a.Rooms.CurrentRoom} turns {a.Rooms.Turns} noise {a.Rooms.Noise} minute {c.Campaign.MinuteOfDay} | "
            + string.Join(";", pl.Plan.Orders.Select(o => o.Site + ":" + o.Lead + "," + o.Pace + "," + o.Duty + "," + o.Solo)) + " | " + string.Join(" ", Enumerable.Range(0, 3).Select(Site));
    }

    // First visit (it sleeps): the same controls as a later visit (말 놓기): '계속 진행' shows, the move is both pawns at the door and
    // '턴 진행' (one turn), and the turn sequence plays.
    public static async Task<string> FirstVisit()
    {
        var c = Owner(); var a = await Arrived(c); var (replay, auto, _) = Flow(a); var t = a.Threat; var pl = t.Planner; var n = a.Rooms;
        Check(!pl.Active && pl.Placing && t.State != null && t.State.Asleep, "Needs the first visit (a fresh game: VerifyFieldTurnPlan.Enter)");
        Check(n.CurrentRoom == A, "Start in the arcade"); await Until(() => FieldPawnTest.Ready(a), 3000, "board ready");
        Check(auto.Toggle.gameObject.activeSelf && !auto.On && pl.TurnButton.gameObject.activeSelf && !replay.Playing, "The first visit has '턴 진행' and '계속 진행'");
        int turns = n.Turns, minute = c.Campaign.MinuteOfDay, version = FieldTurnPulse.Version;
        await Gather(a, C); Check(n.HasQueuedMove && n.QueuedRoom == C && n.Turns == turns && !a.InTransit && !a.Popup.activeSelf, "Both pawns at the door: the move waits for '턴 진행', no time, no popup");
        await Tap(pl.TurnButton);
        Check(a.InTransit && n.Turns == turns + 1 && c.Campaign.MinuteOfDay == minute + n.MinutesPerTurn, "'턴 진행' = the move (one turn)");
        await Until(() => FieldTurnPulse.Version != version, 1000, "turn sequence");
        Check(replay.Playing && FieldTurnPulse.Turn == turns + 1 && replay.BannerTitle.text == string.Format(replay.BannerFormat, turns + 1, n.MinutesPerTurn) && replay.BannerDetail.text == replay.DetailMove, "Move banner: " + replay.BannerTitle.text + " / " + replay.BannerDetail.text);
        await Task.Delay(120); await Still(a, "01-first-visit-move-sequence");
        await Until(() => !a.InTransit && n.CurrentRoom == C, 6000, "to corridor"); await Task.Delay(300); await Until(() => FieldPawnTest.Ready(a), 3000, "board ready");
        await Gather(a, A); await Tap(pl.TurnButton); Check(a.InTransit && !n.HasQueuedMove && n.Turns == turns + 2, "Back by gathering");
        await Until(() => !a.InTransit && n.CurrentRoom == A, 6000, "to arcade"); await Until(() => !replay.Playing, 3000, "sequence end");
        Check(!FieldTurnPulse.Playing && FieldTurnPulse.Progress01 == 1 && !replay.Banner.gameObject.activeSelf && !replay.Tick.gameObject.activeSelf, "Sequence ended");
        return "PASS first visit: '계속 진행' shown, both pawns at the door + '턴 진행' = the move (+1 turn, +10분) with the banner '" + replay.BannerTitle.text + " · " + replay.DetailMove + "'";
    }

    // Site board: one '턴 진행' plays one sequence (banner, +10분, a ring at the searched object) and changes nothing itself.
    public static async Task<string> Replay()
    {
        FieldIdleConfirm.AutoAccept = true; // members left idle hush without the question (VerifyIdleConfirm tests it)
        var c = Owner(); var a = await Arrived(c); var (replay, auto, _) = Flow(a); var pl = a.Threat.Planner;
        Check(a.Rooms.CurrentRoom == A, "Start in the arcade");
        await Board(c, a, 0, 0, 0);
        Check(auto.Toggle.gameObject.activeSelf && !auto.On && auto.Label.text == auto.OffLabel && !auto.Glyph.Bars, "'계속 진행' shown on the board");
        Hit(auto.Toggle); Hit(pl.TurnButton); Fits(auto.Label);
        // 오락기 뒤판 (noise 3, prybar fixture): the table is silent now, the banner and the ring need a noisy object.
        if (c.InventoryPanel.CountFor(a.Participants[0], "prybar") == 0) Check(c.InventoryPanel.TransferField(a.Participants[0], "prybar", 1, true), "Prybar fixture");
        await Assign(a, 2, 2);
        int version = FieldTurnPulse.Version, turns = a.Rooms.Turns;
        await Tap(pl.TurnButton);
        Check(a.Rooms.Turns == turns + 1 && FieldTurnPulse.Version == version + 1 && FieldTurnPulse.Turn == turns + 1 && replay.Playing && FieldTurnPulse.Playing, "One turn, one sequence");
        Check(replay.Banner.gameObject.activeSelf && replay.Banner.alpha > 0 && replay.BannerTitle.text == string.Format(replay.BannerFormat, turns + 1, a.Rooms.MinutesPerTurn) && replay.BannerDetail.text == string.Format(replay.DetailNoise, a.Loot.SiteNoise(2)), "Banner: " + replay.BannerTitle.text + " / " + replay.BannerDetail.text);
        Check(replay.RipplesPlaying >= 1 && replay.Tick.gameObject.activeSelf && replay.TickLabel.text == string.Format(replay.TickFormat, a.Rooms.MinutesPerTurn), "Ring at the machine and the tick");
        Fits(replay.BannerTitle, replay.BannerDetail, replay.TickLabel);
        string during = Snap(a, c); await Still(a, "02-turn-sequence");
        await Until(() => !replay.Playing, 3000, "sequence end"); await Task.Delay(50);
        Check(Snap(a, c) == during, "The sequence changed nothing: " + during + " → " + Snap(a, c));
        Check(!FieldTurnPulse.Playing && FieldTurnPulse.Progress01 == 1 && !replay.Banner.gameObject.activeSelf && replay.RipplesPlaying == 0 && pl.LastMismatch == "", "Ended cleanly");
        await Still(a, "03-after-sequence");
        return "PASS replay: 1 turn → 1 sequence ('" + replay.BannerTitle.text + "', +" + a.Rooms.MinutesPerTurn + "분, ring), state unchanged by it: " + during;
    }

    // '계속 진행': stops at once with nothing assigned; the player can switch it off; it runs turn after turn until a search
    // completes (finds open at once); a warning stops it; it never runs a turn whose preview raises danger.
    public static async Task<string> KeepGoing()
    {
        FieldIdleConfirm.AutoAccept = true; // members left idle hush without the question (VerifyIdleConfirm tests it)
        var c = Owner(); var a = await Arrived(c); var (replay, auto, _) = Flow(a); var t = a.Threat; var pl = t.Planner; var log = new List<string>();
        Check(a.Rooms.CurrentRoom == A, "Start in the arcade");
        // 1. Nothing assigned.
        await Board(c, a, 0, 0, 0); int turns = a.Rooms.Turns;
        await Tap(auto.Toggle); Check(!auto.On && auto.LastStop == auto.StopNothing && a.Rooms.Turns == turns, "Nothing assigned: " + auto.LastStop);
        await Until(() => auto.StampShowing, 1000, "stamp"); Check(auto.StampLabel.text == string.Format(auto.StampFormat, auto.StopNothing), "Stamp: " + auto.StampLabel.text);
        Fits(auto.StampLabel); await Still(a, "04-stop-nothing-assigned");
        log.Add("nothing → '" + auto.StampLabel.text + "'");
        // 2. The crate (alone, a 3-turn fixture as the old 정밀; restored after 3.): on, one turn plays, the player switches it off.
        a.Loot.Peek(0, out var crate0); Check(crate0 == null || crate0.Progress == 0, "Fixture: the crate is unsearched");
        int crateTurns = a.Loot.Sites[0].Turns; a.Loot.Sites[0].Turns = 3;
        await Assign(a, 0, 2); turns = a.Rooms.Turns;
        await Tap(auto.Toggle); Check(auto.On && auto.Label.text == auto.OnLabel && auto.Glyph.Bars, "On");
        await Until(() => auto.AutoTurns >= 1 && replay.Playing, 3000, "first auto turn"); await Task.Delay(150); await Still(a, "05-keep-going-on");
        await Tap(auto.Toggle); Check(!auto.On && auto.LastStop == "" && a.Rooms.Turns == turns + 1, "Player switched it off after one turn");
        await Task.Delay(2200); Check(a.Rooms.Turns == turns + 1 && !auto.StampShowing, "No more turns, no stamp");
        log.Add("player off after 1");
        // 3. On again: turn after turn until the crate completes; the finds open at once, during that turn's sequence.
        a.Loot.Peek(0, out var crate); int left = crate.Required - crate.Progress; turns = a.Rooms.Turns;
        Check(left >= 2, "Fixture: the crate needs 2+ turns (" + crate.Progress + "/" + crate.Required + ")");
        bool lootDuring = false, bannerHidden = false;
        await Tap(auto.Toggle);
        await Until(() => { if (a.Loot.IsOpen && FieldTurnPulse.Playing && !lootDuring) { lootDuring = true; bannerHidden = replay.Banner.alpha == 0; } return !auto.On; }, 4000 * left, "keep going");
        Check(a.Rooms.Turns == turns + left && auto.AutoTurns == left && auto.LastStop == string.Format(auto.StopSearchDone, a.ObjectNames[0]), "Ran " + (a.Rooms.Turns - turns) + "/" + left + " turns, stop: " + auto.LastStop);
        Check(a.Loot.IsOpen && lootDuring && bannerHidden, "Finds opened at once, the banner stepped aside");
        await Still(a, "06-finds-open-during-sequence");
        await CloseLoot(a); await Until(() => auto.StampShowing, 1500, "stamp after the finds");
        Check(auto.StampLabel.text == string.Format(auto.StampFormat, auto.LastStop), "Stamp: " + auto.StampLabel.text);
        Fits(auto.StampLabel); await Still(a, "07-stop-search-done");
        log.Add(left + " turns → '" + auto.StampLabel.text + "'");
        a.Loot.Sites[0].Turns = crateTurns; // the fixture back (the crate is complete)
        // 4. A new warning stops it: the site clock at 6, the next turn brings '오래 머묾' one turn ahead.
        await Board(c, a, 0, 0, 6); await Assign(a, 1, 2); turns = a.Rooms.Turns;
        await Tap(auto.Toggle); await Until(() => !auto.On, 6000, "until the warning");
        a.Loot.Peek(1, out var table);
        Check(a.Rooms.Turns == turns + 1 && auto.LastStop == auto.StopLinger && t.AutoStopKind() == FieldAutoStop.Linger && a.Status.text == t.LingerLine && !table.Complete, "Linger stop after " + (a.Rooms.Turns - turns) + ": " + auto.LastStop + " / " + a.Status.text);
        await Until(() => auto.StampShowing, 1500, "stamp"); await Still(a, "08-stop-warning");
        log.Add("warning → '" + auto.StampLabel.text + "'");
        // 5. The next turn would raise danger (오래 머묾): it does not run it.
        turns = a.Rooms.Turns;
        await Tap(auto.Toggle); Check(!auto.On && auto.LastStop == auto.StopDangerAhead && a.Rooms.Turns == turns, "Forecast stop: " + auto.LastStop);
        await Until(() => auto.StampShowing && auto.StampLabel.text == string.Format(auto.StampFormat, auto.StopDangerAhead), 1500, "forecast stamp"); await Still(a, "09-stop-forecast");
        log.Add("forecast → '" + auto.StampLabel.text + "'");
        return "PASS keep going · " + string.Join(" · ", log) + " · stills " + Shots;
    }

    // Site board: every pawn at a door sets the move (no time; the first pawn there listens), any pawn leaving cancels it, all at another
    // door replaces it; '턴 진행' makes the move (one turn); '계속 진행' makes a gathered move and stops in the new room.
    public static async Task<string> Reservation()
    {
        var c = Owner(); var a = await Arrived(c); var (replay, auto, view) = Flow(a); var t = a.Threat; var pl = t.Planner; var n = a.Rooms; var log = new List<string>(); var rules = FieldPawnTest.Rules(a);
        await Board(c, a, 0, 0, 0); Check(n.CurrentRoom == A, "Start in the arcade");
        int turns = n.Turns, minute = c.Campaign.MinuteOfDay, clock = t.State.TurnsUsed; string before = Snap(a, c);
        // 1. The first pawn at the arcade door listens (no move yet); the second makes the move: no time passes.
        Check(FieldPawnTest.Listen(a, 0, C) && !n.HasQueuedMove, "One pawn at the door listens, no move");
        var last = FieldPawnTest.Option(a, 1, FieldPawnTest.DoorKey(C), FieldSpotKind.Gather);
        Check(last != null && last.Enabled && last.Label == string.Format(pl.PlaceTexts.GatherLast, FieldSiteState.RoomNames[C]), "The last pawn's pin: " + (last != null ? last.Label : "none"));
        Check(FieldPawnTest.Place(a, 1, last.Key, FieldSpotKind.Gather), "Second pawn at the door");
        Check(!a.Popup.activeSelf && !a.InTransit && Snap(a, c) == before, "Gathering costs nothing: " + Snap(a, c));
        Check(n.HasQueuedMove && n.QueuedRoom == C && n.QueuedDoor == (RectTransform)a.Objects[3].transform && n.QueuedLabel == string.Format(n.QueueLabelFormat, FieldSiteState.RoomNames[C]) && n.QueuedTurns == 1, "Move API: " + n.QueuedRoom + " / " + n.QueuedLabel);
        await Task.Delay(120);
        Check(pl.TurnSubtitle.text == string.Format(view.TurnSubtitleFormat, FieldSiteState.RoomNames[C], 1) && pl.Chip.text.StartsWith(string.Format(view.ChipMove, FieldSiteState.RoomNames[C], 1)) && rules.LastStatus == string.Format(pl.PlaceTexts.StatusGathered, FieldSiteState.RoomNames[C]), "Gathered texts: " + pl.TurnSubtitle.text + " / " + pl.Chip.text + " / " + rules.LastStatus);
        Check(!pl.Chip.text.Contains(view.ChipReleases), "Nothing outside the door is dropped: " + pl.Chip.text);
        Fits(pl.TurnSubtitle, a.Status); await Still(a, "11-move-gathered");
        log.Add("gathered '" + n.QueuedLabel + "' free");
        // 2. A pawn taken off the door cancels the move (the listener's first gatherer listens on).
        Check(FieldPawnTest.Unassign(a, 0), "Pawn 0 off the door");
        Check(!n.HasQueuedMove && n.QueuedRoom == -1 && n.QueuedDoor == null && n.QueuedLabel == "" && Snap(a, c) == before && rules.LastStatus.Contains(pl.PlaceTexts.StatusMoveCancelled), "Cancelled without time: " + rules.LastStatus);
        Check(FieldPawnTest.ActionOf(a, 1) == FieldAction.Listen, "The one left at the door listens");
        await Task.Delay(150); Check(pl.TurnSubtitle.text != string.Format(view.TurnSubtitleFormat, FieldSiteState.RoomNames[C], 1), "Move texts gone: " + pl.TurnSubtitle.text);
        await Still(a, "12-move-cancelled");
        log.Add("cancel free");
        // 3. Gather again; '턴 진행' is the move: one turn, the walk and the sequence.
        await Gather(a, C); Check(n.HasQueuedMove, "Gathered again");
        int version = FieldTurnPulse.Version;
        await Tap(pl.TurnButton);
        Check(a.InTransit && !n.HasQueuedMove && n.Turns == turns + 1 && c.Campaign.MinuteOfDay == minute + n.MinutesPerTurn && FieldTurnPulse.Version == version + 1 && replay.BannerDetail.text == replay.DetailMove, "The turn is the move");
        await Still(a, "13-gathered-move-walk");
        await Until(() => !a.InTransit && n.CurrentRoom == C, 6000, "to corridor"); await Task.Delay(300); await Until(() => FieldPawnTest.Ready(a), 3000, "board ready");
        Check(t.State.TurnsUsed == clock + 1 && t.State.PartyRoom == C && !pl.Plan.HasAssignments && !pl.Plan.HasGathers && pl.LastMismatch == "", "Arrived after one site turn");
        log.Add("turn → corridor (+1 turn)");
        // 4. All at another door replaces the move; the lock needs the prybar in a bag (option A: 1 + UnlockTurns turns); a pawn leaving cancels.
        var p0 = a.Participants[0]; bool gave = !n.StorageUnlocked && c.InventoryPanel.CountFor(p0, n.UnlockTool) == 0 && c.InventoryPanel.TransferField(p0, n.UnlockTool, 1, true);
        try
        {
            if (!n.StorageUnlocked) Check(c.InventoryPanel.CountFor(p0, n.UnlockTool) > 0, "Prybar fixture");
            await Gather(a, A); Check(n.QueuedRoom == A && n.QueuedDoor == (RectTransform)n.CorridorBack.transform, "Back door gathered");
            await Gather(a, S);
            int cost = 1 + (n.StorageUnlocked ? 0 : n.UnlockTurns); string storage = FieldSiteState.RoomNames[S];
            Check(n.QueuedRoom == S && n.QueuedDoor == (RectTransform)n.LockedDoor.transform && n.QueuedTurns == cost && n.QueuedLabel == (n.StorageUnlocked ? string.Format(n.QueueLabelFormat, storage) : string.Format(n.QueueUnlockFormat, storage, cost)), "Replaced: " + n.QueuedLabel);
            await Task.Delay(120); Check(pl.TurnSubtitle.text == string.Format(view.TurnSubtitleFormat, storage, cost) && n.Turns == turns + 1, "Replaced subtitle: " + pl.TurnSubtitle.text);
            await Still(a, "14-move-replaced");
            Check(FieldPawnTest.Unassign(a, 1) && !n.HasQueuedMove && n.CurrentRoom == C && n.Turns == turns + 1, "A pawn off the storage door cancels");
        }
        finally { if (gave) c.InventoryPanel.TransferField(p0, n.UnlockTool, 1, false); FieldPawnTest.Clear(a); }
        log.Add("replace + cancel");
        // 5. '계속 진행' with everyone at the back door: it makes the move and stops in the new room.
        await Gather(a, A); Check(n.QueuedRoom == A, "Gathered at the arcade door");
        int t0 = n.Turns; await Tap(auto.Toggle);
        await Until(() => !auto.On, 8000, "auto move");
        Check(n.CurrentRoom == A && n.Turns == t0 + 1 && auto.AutoTurns == 1 && auto.LastStop == auto.StopRoom && !n.HasQueuedMove, "Auto move: " + auto.LastStop + " turns " + (n.Turns - t0));
        await Until(() => auto.StampShowing, 1500, "stamp"); await Still(a, "15-auto-move-stop");
        log.Add("keep going made the move → '" + auto.StampLabel.text + "'");
        return "PASS gathering · " + string.Join(" · ", log) + " · stills " + Shots;
    }

    public static async Task<string> All()
    {
        var r = new List<string> { await FirstVisit(), await Replay(), await KeepGoing(), await Reservation() };
        return string.Join("\n", r);
    }
}
