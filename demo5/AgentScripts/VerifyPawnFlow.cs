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

// 말 놓기 · 끝까지 (2026-09-25, 기획/탐험-말놓기-조작-재설계.md · design §4 VerifyPawnFlow, Group C): the retired layers and the whole flow
// with real presses (the board's pawn and silhouette Buttons, '턴 진행', right presses on hotspots).
//   Wiring()      edit or play, after BuildPawnRules → BuildPawnBoard → BuildRetireOldAssign: the old layers are off in the prefabs, the
//                 07 window is read only, FieldRoomArea is on Main, the tutorial banner is at the top centre, the settlement's own
//                 assignment bubbles are untouched, and SettlementScreen inherits all of it.
//   Tutorial()    play, after VerifyTutorialClarity.Start (a new game paused at packing): only the guide's next press, to the return report.
//                 The crate and the corridor crate each take the co-op path (pawn → silhouette → second pawn → co-op silhouette →
//                 '턴 진행' = one turn), doors are reached by gathering, no meeting ever opens (0% on the guided route), the missing
//                 person is found. Stills in Temp/PawnFlowCapture.
//   SecondVisit() play, after VerifyFieldTurnPlan.Enter or Tutorial: a later visit in the arcade. A left press with nothing held asks for a
//                 pawn; right presses open the read-only 07 window and the door log (never the move popup); a pawn tapped onto a door
//                 listens (the log fills, '방금' then '1턴 전'); the next member is picked up by itself and its silhouette makes the move;
//                 the locked storage door is a grey padlock without the prybar and '문 따고 보관실로' with it (everyone tapped onto it);
//                 the trace (before anyone looked at it) asks for a pawn, and a pawn beside it + '턴 진행' starts the conversation.
//                 The den shelf (a lure first) is VerifyFieldStrategy.Showcase; the pure rules are VerifyPawnRules.
// FieldIdleConfirm.AutoAccept is set where turns pass with idle members.
public static class VerifyPawnFlow
{
    const string P = "Assets/Prefabs/Settlement/", ArrivalPath = P + "ExpeditionArrivalPanel.prefab", ScreenPath = P + "SettlementScreen.prefab", SearchPath = P + "ExpeditionSearchPanel.prefab";
    const int A = FieldSiteState.Arcade, C = FieldSiteState.Corridor, S = FieldSiteState.Storage;
    static readonly string[] QuickChildren = { "Veil", "BubbleHits", "Glows", "SelectHint", "DragToken" };
    static readonly string[] SearchHidden = { "Workers", "DutyHeading", "Duty_0", "Duty_1", "Duty_2", "Choose" };
    static readonly Vector2 GuideTop = new Vector2(720, -24);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    // ---- wiring (edit or play) ----
    public static string Wiring()
    {
        var done = new List<string>();
        var search = AssetDatabase.LoadAssetAtPath<GameObject>(SearchPath); Check(search, "Missing " + SearchPath);
        var s = search.GetComponent<ExpeditionSearchPanel>(); var work = s.Workspace.transform;
        foreach (var n in SearchHidden) { var t = work.Find(n); Check(!t || !t.gameObject.activeSelf, "07: Workspace/" + n + " is still on (run BuildRetireOldAssign.Run)"); }
        Check(s.ReadOnlyWindow && s.Placed && s.Placed.gameObject.activeSelf && s.Back && s.Back.gameObject.activeSelf, "07: read only, the placed line and 돌아가기");
        done.Add("07 read only");
        foreach (var path in new[] { ArrivalPath, ScreenPath })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path); Check(root, "Missing " + path); string where = path.Substring(path.LastIndexOf('/') + 1);
            foreach (var a in root.GetComponentsInChildren<ExpeditionArrivalPanel>(true))
            {
                var main = a.Main.transform;
                Check(main.Find("PawnBoard") && main.Find("PawnBoard").GetComponent<FieldPawnBoard>(), where + ": Main/PawnBoard (BuildPawnBoard.Run)");
                var note = main.Find("SearchNote"); Check(!note || !note.gameObject.activeSelf, where + ": the search note is off");
                var markers = main.GetComponentInChildren<FieldPlanTargetMarkers>(true);
                if (markers) { Check(!markers.enabled && markers.GetComponentsInChildren<AssignmentBubble>(true).All(x => !x.gameObject.activeSelf), where + ": the bubbles over objects are off"); }
                var quick = main.GetComponentInChildren<FieldQuickAssign>(true);
                if (quick)
                {
                    Check(!quick.enabled && !quick.Note && !quick.OpenNoteAfterAssign, where + ": quick assign off");
                    foreach (var n in QuickChildren) { var t = quick.transform.Find(n); Check(!t || !t.gameObject.activeSelf, where + ": MemberActions/" + n + " off"); }
                }
                var slots = main.GetComponentInChildren<FieldMemberActionSlots>(true); Check(slots && slots.enabled && slots.gameObject.activeSelf, where + ": the card slots stay");
                Check(!slots.Quick && slots.PawnBoard, where + ": slots read the pawn board, not the quick assign");
                var pl = a.GetComponent<FieldTurnPlanner>(); Check(pl && !pl.ListenButton && !pl.AutoFillHelpers, where + ": no popup listen button, helpers only placed");
                Check(!a.Threat.Hush, where + ": no hush button reference"); foreach (Transform t in main) if (t.name == "Hush") Check(!t.gameObject.activeSelf, where + ": Main/Hush off");
                var area = a.Main.GetComponent<FieldRoomArea>(); Check(area && area.RoomArea.width > 0 && area.RoomArea.yMax <= 800, where + ": FieldRoomArea on Main above the tray (" + (area ? area.RoomArea.ToString() : "none") + ")");
                if (markers) Check(area.RoomArea == markers.RoomArea, where + ": FieldRoomArea copies the bubbles' room area");
                Check(a.Search && a.Search.ReadOnlyWindow, where + ": the nested 07 is read only");
                Check(a.GetComponent<FieldMoveQueueView>() || main.GetComponentInChildren<FieldMoveQueueView>(true), where + ": the move texts view stays");
                done.Add(where);
            }
        }
        var screen = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath);
        var guide = screen.GetComponentInChildren<SettlementTutorialGuide>(true); Check(guide && guide.FieldPosition == GuideTop, "Tutorial field banner at the top centre " + GuideTop + " (" + (guide ? guide.FieldPosition.ToString() : "none") + ")");
        // The banner (top centre) stays clear of the member row and the '이번 턴' / '행동' panels (y from 776 down).
        Check(-guide.FieldPosition.y + guide.FieldSize.y < 776, "The banner stays above the tray");
        done.Add("guide top centre");
        // The settlement keeps its own assignment bubbles (only the exploration ones retired).
        var marks = screen.GetComponentsInChildren<SettlementAssignmentMarkers>(true);
        Check(marks.Length > 0 && marks.All(m => m.enabled), "The settlement's assignment markers stay (" + marks.Length + ")");
        done.Add("settlement bubbles kept");
        return "PASS wiring: " + string.Join(", ", done);
    }

    // ---- play helpers ----
    static SettlementController Owner() { var c = Object.FindAnyObjectByType<SettlementController>(); Check(Application.isPlaying && c && c.Campaign != null, "Play mode, then VerifyFieldTurnPlan.Enter (or VerifyTutorialClarity.Start) first"); return c; }
    static SettlementTutorialGuide Guide(SettlementController c) => c.GetComponent<SettlementTutorialGuide>();
    static Vector2 Screen(RectTransform r) { Canvas.ForceUpdateCanvases(); return RectTransformUtility.WorldToScreenPoint(r.GetComponentInParent<Canvas>().worldCamera, r.TransformPoint(r.rect.center)); }
    // A real press: the topmost thing under the button's centre (or, for a pawn / silhouette, under its body) must belong to it.
    static async Task Tap(Button button, int wait = 150)
    {
        Check(button && button.IsActive() && button.IsInteractable(), "Unavailable " + (button ? button.name : "null"));
        Canvas.ForceUpdateCanvases();
        var h = button.GetComponent<FieldPawnHandle>(); Vector2 at = Screen((RectTransform)button.transform);
        if (h && h.Body && h.Board) { var b = h.Board.VisibleBounds(h.Body); at = Camera.main.WorldToScreenPoint(b.center); }
        var data = new PointerEventData(EventSystem.current) { position = at, pressPosition = at, button = PointerEventData.InputButton.Left }; var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, "Blocked " + button.name + " by " + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler); await Task.Delay(wait);
    }
    // A right press on a room hotspot (the Button ignores it; ExplorationHotspot sends it to the pawn board).
    static async Task Right(Button target)
    {
        var hot = target ? target.GetComponent<ExplorationHotspot>() : null; Check(hot, "No hotspot on " + (target ? target.name : "null"));
        var data = new PointerEventData(EventSystem.current) { position = Screen((RectTransform)target.transform), button = PointerEventData.InputButton.Right };
        ExecuteEvents.Execute(target.gameObject, data, ExecuteEvents.pointerClickHandler); await Task.Delay(200);
    }
    static async Task Until(Func<bool> done, int ms, string what) { var w = Stopwatch.StartNew(); while (!done()) { if (w.ElapsedMilliseconds > ms) throw new Exception("Timeout: " + what); await Task.Delay(30); } }
    static string Shots => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "PawnFlowCapture"));
    static Task Still(MonoBehaviour host, string name) { var done = new TaskCompletionSource<bool>(); host.StartCoroutine(StillRoutine(name, done)); return done.Task; }
    static IEnumerator StillRoutine(string name, TaskCompletionSource<bool> done)
    {
        yield return new WaitForEndOfFrame();
        var raw = new RenderTexture(UnityEngine.Screen.width, UnityEngine.Screen.height, 0); var rt = new RenderTexture(raw.width, raw.height, 0); ScreenCapture.CaptureScreenshotIntoRenderTexture(raw);
        if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(raw, rt, new Vector2(1, -1), new Vector2(0, 1)); else Graphics.Blit(raw, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new UnityEngine.Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); RenderTexture.active = prev; Object.Destroy(raw); Object.Destroy(rt);
        Directory.CreateDirectory(Shots); File.WriteAllBytes(Path.Combine(Shots, name + ".png"), tex.EncodeToPNG()); Object.Destroy(tex); done.SetResult(true);
    }
    static string Kind(ExpeditionArrivalPanel a, Button t)
    {
        var h = t ? t.GetComponent<FieldPawnHandle>() : null; if (h) return h.Role == FieldPawnHandle.Kind.Pawn ? "pawn" : "ghost";
        return t && t == a.Threat.Planner.TurnButton ? "turn" : t ? t.name : "-";
    }
    static async Task CloseLoot(ExpeditionArrivalPanel a) { if (a.Loot.IsOpen) { a.Loot.Escape(); await Task.Delay(150); if (a.Loot.IsOpen && a.Loot.LeaveReview.activeSelf) a.Loot.Escape(); await Task.Delay(150); if (a.Loot.IsOpen) { await Tap(a.Loot.Back); if (a.Loot.LeaveReview.activeSelf) await Tap(a.Loot.LeaveConfirm); } } }

    // ---- the guided first trip (only the guide's next press) ----
    public static async Task<string> Tutorial()
    {
        var c = Owner(); var g = Guide(c); Check(g && c.PackingPanel.IsOpen, "Run VerifyTutorialClarity.Start first (paused at packing)");
        var a = c.ArrivalPanel; var story = c.MissingPerson; var trace = new List<string>(); var kinds = new List<string>(); int turns = 0, moves = 0, stills = 0;
        for (int i = 0; i < 160 && !c.ReturnPanel.IsOpen; i++)
        {
            await Task.Delay(90);
            if (a.IsOpen && a.InTransit) { await Until(() => !a.InTransit, 8000, "walk"); continue; }
            Check(!(a.IsOpen && a.Encounter.IsOpen), "The guided route met something (it must stay at 0%): " + string.Join(" > ", trace.TakeLast(10)));
            if (story && story.IsOpen) { trace.Add("story"); await Tap(story.Next); continue; }
            var t = g.Target; Check(t, "No next press: " + g.Guidance + " after " + string.Join(" > ", trace.TakeLast(10)));
            string kind = a.IsOpen ? Kind(a, t) : t.name; trace.Add(kind + "(" + g.Title.text + ")");
            if (a.IsOpen && (kind == "pawn" || kind == "ghost" || kind == "turn")) kinds.Add(kind);
            if (a.IsOpen && kind == "turn")
            {
                bool move = a.Rooms.HasQueuedMove; int before = a.Rooms.Turns, cost = move ? a.Rooms.QueuedTurns : 1;
                await Tap(t, 250); if (move) { await Until(() => !a.InTransit, 8000, "move"); moves++; } else turns++;
                Check(a.Rooms.Turns == before + cost, "'턴 진행' = " + cost + " turn(s): " + before + " → " + a.Rooms.Turns);
                if (stills++ < 3) await Still(a, "tutorial-turn-" + stills);
                continue;
            }
            if (a.IsOpen && kind == "ghost" && kinds.Count(x => x == "ghost") <= 2) await Still(a, "tutorial-ghost-" + kinds.Count(x => x == "ghost"));
            await Tap(t);
        }
        Check(c.ReturnPanel.IsOpen, "The guide did not reach home: " + string.Join(" > ", trace.TakeLast(12)));
        // Each searched object: pawn, silhouette, second pawn, co-op silhouette, '턴 진행'.
        string seq = string.Join(",", kinds); int coop = CountOf(seq, "pawn,ghost,pawn,ghost,turn");
        Check(coop >= 2, "Both tutorial objects the co-op way (pawn,ghost,pawn,ghost,turn ×2): " + seq);
        Check(turns >= 2 && moves >= 2, "Searched and moved with '턴 진행' only (turns " + turns + ", moves " + moves + ")");
        if (story) Check(story.State.Found, "The missing person's clue was found on the guided route");
        return "PASS tutorial by the guide only: " + trace.Count + " presses, co-op ×" + coop + ", turns " + turns + ", moves " + moves + ", no meeting · stills " + Shots;
    }
    static int CountOf(string s, string part) { int n = 0, at = 0; while ((at = s.IndexOf(part, at, StringComparison.Ordinal)) >= 0) { n++; at += part.Length; } return n; }

    // ---- a later visit with real presses ----
    static async Task<ExpeditionArrivalPanel> Later(SettlementController c)
    {
        FieldIdleConfirm.AutoAccept = true; var a = c.ArrivalPanel; if (c.Opening) c.Opening.State.Enabled = false;
        if (a.IsOpen)
        {
            if (a.Story && a.Story.IsOpen) a.Story.Close(); await CloseLoot(a); if (a.Search.IsOpen) a.Search.Close(); if (a.Popup.activeSelf) a.ClosePopup();
            await Until(() => !a.InTransit, 8000, "walk");
            if (!(a.Threat.Active && a.Rooms.CurrentRoom == A)) { Check(a.FinishReturn(), "Home"); await Task.Delay(200); }
        }
        async Task Go()
        {
            if (c.ReturnPanel && c.ReturnPanel.IsOpen) { c.ReturnPanel.Close(); await Task.Delay(100); }
            var mall = c.ExpeditionPanel.Destinations.First(d => d.Id == "mall"); Check(a.Begin(c.Campaign.Party.ToArray(), mall), "Departure"); await Until(() => a.IsOpen && !a.InTransit, 3000, "arrival"); await Task.Delay(200);
        }
        if (!a.IsOpen) await Go();
        if (!a.Threat.Active) { if (a.Popup.activeSelf) a.ClosePopup(); Check(a.FinishReturn(), "Home after the first visit"); await Task.Delay(200); await Go(); }
        await Until(() => a.Popup.activeSelf || a.Threat.IntroAcknowledged, 3000, "board intro"); if (a.Popup.activeSelf) a.ClosePopup();
        a.Threat.ReviewWake(0, 0, 0); await Task.Delay(250); if (a.Popup.activeSelf) a.ClosePopup();
        var b = FieldPawnTest.Board(a); Check(b && b.isActiveAndEnabled, "Run BuildPawnBoard.Run first"); b.Cancel();
        foreach (var p in a.Participants) Check(p.Health > 0, "Fixture: every member alive");
        Check(a.Threat.Planner.Active && a.Rooms.CurrentRoom == A && !a.Threat.Planner.Plan.HasAssignments && a.Participants.Count >= 2, "A later visit in the arcade, nothing placed");
        await Until(() => FieldPawnTest.Ready(a) && !b.IsWalking, 3000, "board ready"); return a;
    }
    // Tap the member's pawn, then the silhouette of `key` (slot < 0: any). The board must show that silhouette.
    static async Task PawnTo(ExpeditionArrivalPanel a, int m, string key, int slot = -1)
    {
        var b = FieldPawnTest.Board(a);
        if (b.Held != m) { await Until(() => FieldPawnTest.Handle(a, m), 1500, "pawn " + m + " handle"); await Tap(FieldPawnTest.Handle(a, m)); }
        Check(b.Held == m, "Pawn " + m + " picked up (held " + b.Held + ")");
        await Until(() => FieldPawnTest.Ghost(a, key, slot), 1500, "silhouette " + key);
        await Tap(FieldPawnTest.Ghost(a, key, slot), 200);
    }

    public static async Task<string> SecondVisit()
    {
        var c = Owner(); var a = await Later(c); var pl = a.Threat.Planner; var b = FieldPawnTest.Board(a); var log = new List<string>(); var rooms = a.Rooms;
        int turns = rooms.Turns, minute = c.Campaign.MinuteOfDay;

        // 1. Nothing held: a left press on an object asks for a pawn and changes nothing; a right press opens the read-only 07 window.
        int site = Enumerable.Range(0, 3).FirstOrDefault(i => a.Loot.IsSiteInCurrentRoom(i) && !(a.Loot.Peek(i, out var s) && s.Complete));
        a.Objects[site].onClick.Invoke(); await Task.Delay(150); Check(!a.Search.IsOpen && b.LastHint == b.Texts.PickFirst && a.Status.text == b.Texts.PickFirst && !pl.Plan.HasAssignments, "Left press, nothing held: '" + a.Status.text + "'");
        await Right(a.Objects[site]); Check(a.Search.IsOpen && a.Search.ReadOnly && a.Search.PlacedLine == a.Search.ReadNobody, "Right press: the read-only 07 (" + a.Search.PlacedLine + ")");
        Check(!a.Search.Choose.IsActive() || !a.Search.Choose.IsInteractable(), "07 cannot assign"); await Still(a, "20-07-read-only");
        await Tap(a.Search.Back); Check(rooms.Turns == turns && c.Campaign.MinuteOfDay == minute, "No time for looking");
        log.Add("left asks for a pawn, right = 07 read only");

        // 2. Right press on the arcade door: its heard log (never the move popup).
        await Right(a.Objects[3]); Check(a.Popup.activeSelf && !a.ReturnConfirm.gameObject.activeSelf && a.PopupTitle.text == string.Format(b.Texts.DoorLogTitle, FieldSiteState.RoomNames[C]) && rooms.PendingRoom < 0, "Door log popup: " + a.PopupTitle.text);
        a.ClosePopup(); await Task.Delay(100);

        // 3. A pawn tapped onto the door listens every turn: the log fills ('방금', then '1턴 전').
        await PawnTo(a, 0, FieldSpotRef.DoorKey(C), 0);
        Check(pl.Current.Actions[0] == FieldAction.Listen && !rooms.HasQueuedMove && b.Held == 1, "Pawn 0 listens; the next member is picked up (Q5): held " + b.Held);
        b.Cancel(); await Task.Delay(100);
        Check(await FieldPawnTest.Turn(a), "Turn 1"); var entries = pl.DoorLog.For(A, C);
        Check(entries.Count == 1 && pl.DoorLog.Line(entries[0], a.Threat.State.TurnsUsed).StartsWith(pl.DoorLog.AgeNow), "Log: " + (entries.Count > 0 ? pl.DoorLog.Line(entries[0], a.Threat.State.TurnsUsed) : "none"));
        Check(await FieldPawnTest.Turn(a), "Turn 2"); entries = pl.DoorLog.For(A, C);
        Check(entries.Count == 2 && pl.DoorLog.Line(entries[0], a.Threat.State.TurnsUsed).StartsWith(pl.DoorLog.AgeNow) && pl.DoorLog.Line(entries[1], a.Threat.State.TurnsUsed).StartsWith(string.Format(pl.DoorLog.AgeFormat, 1)), "Newest first, the older one aged");
        await Right(a.Objects[3]); Check(a.Popup.activeSelf && a.PopupBody.text.Contains(pl.DoorLog.AgeNow), "The door log lists what was heard: " + a.PopupBody.text); await Still(a, "21-door-log"); a.ClosePopup(); await Task.Delay(100);
        log.Add("listen ×2 → log");

        // 4. The second pawn to the same door: everyone there = the move next turn; '턴 진행' moves.
        await PawnTo(a, 1, FieldSpotRef.DoorKey(C));
        Check(rooms.HasQueuedMove && rooms.QueuedRoom == C && b.Held < 0, "Everyone at the door: move reserved, nothing held");
        Check(pl.TurnSubtitle.text.Contains(FieldSiteState.RoomNames[C]), "'턴 진행' names the move: " + pl.TurnSubtitle.text); await Still(a, "22-gathered");
        int t0 = rooms.Turns; Check(await FieldPawnTest.Turn(a), "The move"); await Until(() => !a.InTransit, 8000, "walk");
        Check(rooms.CurrentRoom == C && rooms.Turns == t0 + 1 && !pl.Plan.HasAssignments && !pl.Plan.HasGathers && pl.DoorLog.For(A, C).Count == 2, "In the corridor after one turn; the log stays for the visit");
        log.Add("gather → move");

        // 5. The locked storage door (option A): without the prybar in any bag its pin is a grey padlock and no silhouette shows; with it,
        //    both pawns tapped onto the door (the second picked up by itself) make '문 따고 보관실로' (1 + UnlockTurns turns).
        await Until(() => FieldPawnTest.Ready(a) && !b.IsWalking, 3000, "corridor board");
        if (!rooms.StorageUnlocked)
        {
            var p0 = a.Participants[0]; var inv = c.InventoryPanel; bool had = a.Participants.Any(p => inv.CountFor(p, rooms.UnlockTool) > 0);
            if (!had)
            {
                b.Hold(0); await Task.Delay(300);
                var pin = b.PinOf(FieldSpotRef.DoorKey(S));
                Check(pin && pin.Visible && !FieldPawnTest.Ghost(a, FieldSpotRef.DoorKey(S)), "A grey padlock pin at the locked door, no silhouette");
                await Still(a, "24-locked-door-pin"); b.Cancel(); await Task.Delay(100);
                had = inv.TransferField(p0, rooms.UnlockTool, 1, true);
            }
            if (had)
            {
                int t1 = rooms.Turns;
                await PawnTo(a, 0, FieldSpotRef.DoorKey(S)); Check(b.Held == 1, "The second pawn is picked up for the door");
                await Tap(FieldPawnTest.Ghost(a, FieldSpotRef.DoorKey(S)), 200);
                Check(rooms.HasQueuedMove && rooms.QueuedRoom == S && rooms.QueuedTurns == 1 + rooms.UnlockTurns, "Everyone at the locked door: '문 따고 보관실로'");
                Check(await FieldPawnTest.Turn(a), "Unlock and move"); await Until(() => !a.InTransit, 8000, "walk");
                Check(rooms.StorageUnlocked && rooms.CurrentRoom == S && rooms.Turns == t1 + 1 + rooms.UnlockTurns, "In the storage after " + (1 + rooms.UnlockTurns) + " turns");
                log.Add("locked door by gathering");
                if (a.Encounter.IsOpen) return "PASS second visit (stopped at a meeting after the lock) · " + string.Join(" · ", log);
                Check(await FieldPawnTest.Move(a, C), "Back to the corridor");
            }
            else log.Add("locked door skipped (no prybar in stock)");
        }

        // 6. Back to the arcade (gathering), then the trace: before anyone looked at it, a press asks for a pawn; a pawn beside it and
        //    '턴 진행' look at it and the conversation opens.
        Check(await FieldPawnTest.Move(a, A), "Back to the arcade");
        var story = a.Story;
        if (story && story.Clue && story.Clue.gameObject.activeInHierarchy && story.State.Stage == 0)
        {
            await Until(() => FieldPawnTest.Ready(a), 3000, "board");
            story.Clue.onClick.Invoke(); await Task.Delay(150);
            Check(!story.IsOpen && a.Status.text == b.Texts.PickFirst && !pl.Plan.HasAssignments, "Trace press with nothing held asks for a pawn: " + a.Status.text);
            await Right(story.Clue); Check(a.Popup.activeSelf && !story.IsOpen, "Right press: the trace's note"); a.ClosePopup(); await Task.Delay(100);
            await PawnTo(a, 0, FieldSpotRef.ObserveKey(ExpeditionNpcStory.ObservationId));
            Check(pl.Current.Actions[0] == FieldAction.Observe, "Pawn beside the trace");
            Check(await FieldPawnTest.Turn(a), "Look"); await Until(() => story.IsOpen, 3000, "conversation");
            Check(story.State.Stage >= 1, "Observed"); await Still(a, "23-trace-talk"); story.Close(); await Task.Delay(150);
            log.Add("trace by a pawn → conversation");
        }
        else log.Add("trace skipped (stage " + (story ? story.State.Stage : -1) + ")");
        return "PASS second visit · " + string.Join(" · ", log) + " · stills " + Shots;
    }
}
