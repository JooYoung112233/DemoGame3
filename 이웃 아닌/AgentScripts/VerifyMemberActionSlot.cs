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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Member action slot (2026-09-25, 기획/탐험-화면정리와-행동칸-1차.md · approved mocks 탐험-화면정리-시안/01~03).
// 말 놓기 (2026-09-25, 기획/탐험-말놓기-조작-재설계.md · design §4): the quick assignment (card press / drag, glowing targets, the veil,
// the hint) is retired: BuildRetireOldAssign switches it off (phase 1) and phase 2 deletes it. The card slots and the rings stay; a card
// press picks up the member's pawn on the pawn board. ClickAssign / ListenObserve / CancelPaths / Drag / TargetFirst now only check that
// the old layer stays off (their coverage moved to VerifyPawnBoard.Input · Doors · RightClick and VerifyPawnRules.Board).
// Wiring(): edit or play mode, after BuildMemberActionSlot.Run … BuildPawnBoard.Run → BuildRetireOldAssign.Run. Play mode after
// VerifyFieldTurnPlan.Enter (a fresh game): Slots (or All). Every entry starts from a real later visit in the arcade (home once after
// the sleeping first visit, the place-board intro closed) reset with Threat.ReviewWake(0, 0, 0), opening chapter off, nothing placed;
// FieldIdleConfirm.AutoAccept is on (VerifyIdleConfirm tests the question). Stills in Temp/MemberActionSlotCapture.
public static class VerifyMemberActionSlot
{
    const string CardPath = "Assets/Prefabs/Settlement/FieldMemberCard.prefab", ArrivalPath = "Assets/Prefabs/Settlement/ExpeditionArrivalPanel.prefab", ScreenPath = "Assets/Prefabs/Settlement/SettlementScreen.prefab";
    const string NodeName = "MemberActions", BoardNode = "PawnBoard";
    static readonly string[] QuickChildren = { "Veil", "BubbleHits", "Glows", "SelectHint", "DragToken" };
    const int A = FieldSiteState.Arcade, C = FieldSiteState.Corridor;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    // ---- wiring ----
    public static string Wiring()
    {
        var done = new List<string>();
        var card = AssetDatabase.LoadAssetAtPath<GameObject>(CardPath); Check(card, "Missing " + CardPath);
        var slot = card.GetComponentInChildren<FieldMemberActionSlot>(true); var mc = card.GetComponent<ExpeditionMemberCard>();
        Check(slot && slot.name == "ActionSlot" && slot.transform.parent == card.transform && !slot.gameObject.activeSelf, "Card: inactive ActionSlot child (run BuildMemberActionSlot.Run)");
        var sr = (RectTransform)slot.transform; Check(sr.anchorMin == Vector2.zero && sr.anchorMax == new Vector2(1, 0) && sr.pivot.y == 0, "ActionSlot anchored to the card bottom, stretched across");
        Check(slot.Card == mc && slot.Frame && slot.TokenFill && slot.TokenRing && slot.Glyph && slot.Title && slot.Detail && slot.SelectFrame && slot.Input, "ActionSlot wiring");
        Check(slot.SelectFrame.transform.parent == card.transform && !slot.SelectFrame.gameObject.activeSelf, "SelectFrame: an inactive card child");
        var input = slot.Input; Check(input.name == "SelectHit" && input.transform.GetSiblingIndex() == card.transform.childCount - 1 && input.Card == mc && input.Slot == slot && input.Hit && !input.Hit.raycastTarget, "SelectHit: the card's last child, raycast off in the prefab");
        Check(input.BagZones != null && input.BagZones.Length > 0 && input.BagZones.All(z => z && z.IsChildOf(card.transform)), "The bag line stays a bag zone");
        foreach (var n in new[] { "Portrait", "Name", "Role", "State", "StatePaper", "HealthTrack", "ActionTag" }) Check(card.transform.Find(n), "Existing card child kept: " + n);
        Check(mc.Action && mc.Action.name == "ActionTag", "ActionTag still the card's action text");
        foreach (var g in slot.GetComponentsInChildren<Graphic>(true)) Check(!g.raycastTarget, "Raycast target in the slot: " + g.name);
        done.Add("FieldMemberCard (ActionSlot, SelectFrame, SelectHit last)");
        foreach (var path in new[] { ArrivalPath, ScreenPath })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path); Check(root, "Missing " + path);
            foreach (var a in root.GetComponentsInChildren<ExpeditionArrivalPanel>(true))
            {
                var main = a.Main.transform; var node = main.Find(NodeName); Check(node, path + ": Main/" + NodeName + " missing");
                var board = main.Find(BoardNode); Check(board && board.GetSiblingIndex() > node.GetSiblingIndex(), path + ": Main/" + BoardNode + " above the slots (run BuildPawnBoard.Run)");
                var v = node.GetComponent<FieldMemberActionSlots>(); var b = board.GetComponent<FieldPawnBoard>();
                Check(v && v.enabled && node.gameObject.activeSelf && v.Arrival == a && b && b.Arrival == a && v.PawnBoard == b && !v.Quick, path + ": the slots read the pawn board (no quick assign)");
                // The retired quick assign: off, its own pieces off (BuildRetireOldAssign).
                var q = node.GetComponent<FieldQuickAssign>();
                if (q) { Check(!q.enabled && !q.Note, path + ": FieldQuickAssign disabled"); foreach (var n in QuickChildren) { var x = node.Find(n); Check(!x || !x.gameObject.activeSelf, path + ": MemberActions/" + n + " off"); } }
                foreach (var g in node.GetComponentsInChildren<Graphic>(false)) Check(!g.raycastTarget, path + ": a live raycast target under the slots " + g.name);
                foreach (var h in main.GetComponentsInChildren<ExplorationHotspot>(true)) Check(!h.GetComponentInChildren<FieldTargetGlow>(true) && !h.GetComponentInChildren<FieldQuickAssign>(true), path + ": something under hotspot " + h.name);
                done.Add(path.Substring(path.LastIndexOf('/') + 1) + " (slots + pawn board, quick assign off)");
            }
        }
        return "PASS wiring: " + string.Join(", ", done);
    }

    // ---- helpers (as VerifyFieldPlanMarkers) ----
    static async Task Tap(Button button) { Press(button); await Task.Delay(100); }
    static void Press(Button button)
    {
        Check(button && button.IsActive() && button.IsInteractable(), "Unavailable " + (button ? button.name : "null")); Hit(button);
        var r = (RectTransform)button.transform; var data = new PointerEventData(EventSystem.current) { position = Screen(r), button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
    }
    static Camera Cam(Component c) => c.GetComponentInParent<Canvas>().rootCanvas.worldCamera;
    static Vector2 Screen(RectTransform r) { Canvas.ForceUpdateCanvases(); return RectTransformUtility.WorldToScreenPoint(Cam(r), r.TransformPoint(r.rect.center)); }
    static void Hit(Selectable target)
    {
        var data = new PointerEventData(EventSystem.current) { position = Screen((RectTransform)target.transform) }; var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Selectable>() == target, "Blocked " + target.name + " by " + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
    }
    static Rect RectIn(RectTransform r, RectTransform space)
    {
        var c = new Vector3[4]; r.GetWorldCorners(c); var a = space.InverseTransformPoint(c[0]); var b = space.InverseTransformPoint(c[2]);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }
    static async Task Until(Func<bool> done, int ms, string what) { var w = Stopwatch.StartNew(); while (!done()) { if (w.ElapsedMilliseconds > ms) throw new Exception("Timeout: " + what); await Task.Delay(30); } }
    static string Shots => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "MemberActionSlotCapture"));
    static Task Still(MonoBehaviour host, string name) { var done = new TaskCompletionSource<bool>(); host.StartCoroutine(StillRoutine(name, done)); return done.Task; }
    static IEnumerator StillRoutine(string name, TaskCompletionSource<bool> done)
    {
        yield return new WaitForEndOfFrame();
        var raw = new RenderTexture(UnityEngine.Screen.width, UnityEngine.Screen.height, 0); var rt = new RenderTexture(raw.width, raw.height, 0); ScreenCapture.CaptureScreenshotIntoRenderTexture(raw);
        if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(raw, rt, new Vector2(1, -1), new Vector2(0, 1)); else Graphics.Blit(raw, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new UnityEngine.Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); RenderTexture.active = prev; Object.Destroy(raw); Object.Destroy(rt);
        Directory.CreateDirectory(Shots); File.WriteAllBytes(Path.Combine(Shots, name + ".png"), tex.EncodeToPNG()); Object.Destroy(tex); done.SetResult(true);
    }
    static void Fits(params Text[] texts) { Canvas.ForceUpdateCanvases(); foreach (var t in texts) if (t && t.isActiveAndEnabled) Check(t.preferredHeight <= t.rectTransform.rect.height + 1 || t.resizeTextForBestFit && t.cachedTextGenerator.fontSizeUsedForBestFit >= t.resizeTextMinSize, "Overflow " + t.name + ": " + t.text); }
    static string Label(Button b) => b.GetComponentInChildren<Text>().text;
    static SettlementController Owner() { var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Run VerifyFieldTurnPlan.Enter first"); return c; }
    static (FieldPawnBoard b, FieldMemberActionSlots v) Parts(ExpeditionArrivalPanel a)
    {
        var b = FieldPawnBoard.For(a); var v = a.Main.GetComponentInChildren<FieldMemberActionSlots>(true);
        Check(b && v && b.Arrival == a && v.Arrival == a && v.PawnBoard == b, "Run BuildMemberActionSlot.Run … BuildPawnBoard.Run first"); return (b, v);
    }
    static async Task CloseLoot(ExpeditionArrivalPanel a) { if (a.Loot.IsOpen) { await Tap(a.Loot.Back); await Task.Delay(120); if (a.Loot.LeaveReview.activeSelf) await Tap(a.Loot.LeaveConfirm); await Task.Delay(150); } }
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
    // A real later visit in the arcade, reset to a quiet board with nothing assigned and nobody chosen.
    static async Task<ExpeditionArrivalPanel> Ready(SettlementController c)
    {
        FieldIdleConfirm.AutoAccept = true; // members left idle hush without the question (VerifyIdleConfirm tests it)
        var a = c.ArrivalPanel; if (c.Opening) c.Opening.State.Enabled = false;
        if (a.IsOpen) await Settle(a);
        bool later = a.IsOpen && a.Threat.Active && a.Threat.IntroAcknowledged;
        if (later && a.Rooms.CurrentRoom != A) { Check(a.FinishReturn(), "Return home"); await Task.Delay(150); later = false; }
        if (!later)
        {
            if (a.IsOpen) { Check(a.FinishReturn(), "Return home"); await Task.Delay(150); }
            await Depart(c);
            if (!a.Threat.Active) { if (a.Popup.activeSelf) a.ClosePopup(); Check(a.FinishReturn(), "Return from the first visit"); await Task.Delay(150); await Depart(c); }
            await Until(() => a.Popup.activeSelf || a.Threat.IntroAcknowledged, 3000, "place-board intro");
            if (a.Popup.activeSelf) await Tap(a.PopupBack);
        }
        if (a.Rooms.HasQueuedMove) a.Rooms.CancelQueuedMove();
        a.Threat.ReviewWake(0, 0, 0); await Task.Delay(250); if (a.Popup.activeSelf) await Tap(a.PopupBack);
        var (b, _) = Parts(a); b.Cancel(); await Until(() => FieldPawnTest.Ready(a), 3000, "board ready");
        foreach (var p in a.Participants) Check(p.Health > 0, "Fixture: every member alive (" + p.Name + ")");
        var pl = a.Threat.Planner;
        Check(pl.Active && !pl.Plan.HasAssignments && !pl.Plan.HasGathers && !a.Rooms.HasQueuedMove && a.Rooms.CurrentRoom == A && !a.InTransit && a.Participants.Count >= 2, "Later-visit board in the arcade, two members, nothing placed");
        await Task.Delay(150);
        return a;
    }
    // An object of this room not finished yet.
    static int Open(ExpeditionArrivalPanel a, params int[] sites)
    {
        foreach (int s in sites) if (!a.Loot.Peek(s, out var st) || !st.Complete) return s;
        throw new Exception("Fixture: no unfinished object among " + string.Join(",", sites.Select(x => a.ObjectNames[x])) + " (start from VerifyFieldTurnPlan.Enter)");
    }
    // One member's pawn on the object (the lowest free, alone): placed, no time (말 놓기: the 07 window only shows).
    static async Task Place(ExpeditionArrivalPanel a, int site)
    {
        var pl = a.Threat.Planner; int m = FieldPawnTest.Free(a); if (m < 0) m = 0;
        Check(FieldPawnTest.Lead(a, m, site) && pl.Plan.Find(site) != null, "Pawn " + m + " on " + a.ObjectNames[site] + ": " + FieldPawnTest.Describe(a));
        await Task.Delay(150);
    }

    // ---- real presses ----
    static GameObject Top(Vector2 at)
    {
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = at }, hits);
        return hits.Count > 0 ? hits[0].gameObject : null;
    }
    static GameObject ClickAt(Vector2 at, PointerEventData.InputButton button = PointerEventData.InputButton.Left)
    {
        Canvas.ForceUpdateCanvases();
        var hits = new List<RaycastResult>(); var data = new PointerEventData(EventSystem.current) { position = at, pressPosition = at, button = button }; EventSystem.current.RaycastAll(data, hits);
        Check(hits.Count > 0, "Nothing under " + at);
        data.pointerCurrentRaycast = data.pointerPressRaycast = hits[0];
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
        return hits[0].gameObject;
    }
    static Vector2 CardPoint(ExpeditionMemberCard card) => Screen(card.Portrait ? card.Portrait.rectTransform : (RectTransform)card.transform);
    static async Task ClickCard(ExpeditionArrivalPanel a, FieldMemberActionSlots v, int member, PointerEventData.InputButton button = PointerEventData.InputButton.Left)
    {
        var card = a.Cards[member]; var slot = v.SlotOf(member); var at = CardPoint(card);
        var top = Top(at); Check(top == slot.Input.gameObject, "Card " + member + ": the press lands on " + (top ? top.name : "nothing") + ", not its input layer");
        Check(top.GetComponentInParent<Selectable>() == card.Button, "The input layer belongs to the card button");
        ClickAt(at, button); await Task.Delay(150);
    }
    static bool Near(Color x, Color y) => Mathf.Abs(x.r - y.r) < .01f && Mathf.Abs(x.g - y.g) < .01f && Mathf.Abs(x.b - y.b) < .01f;
    static Color TurnTint(FieldTurnPlanner pl) => pl.TurnButton.GetComponent<Image>().color;
    static int Living(ExpeditionArrivalPanel a) => a.Participants.Count(p => p.Health > 0);
    static FieldPawnGlow PawnGlow(ExpeditionArrivalPanel a, int m) => m < a.PartyPawns.Count && a.PartyPawns[m] ? a.PartyPawns[m].GetComponent<FieldPawnGlow>() : null;
    static bool Glowing(ExpeditionArrivalPanel a, int m) { var g = PawnGlow(a, m); return g && g.Shown && g.Ring && g.Ring.enabled; }
    static void SlotIs(FieldMemberActionSlots v, int m, FieldMemberActionSlot.State state, string title, string detail)
    {
        var s = v.SlotOf(m); Check(s && s.gameObject.activeInHierarchy && s.Current == state && s.TitleText == title && s.DetailText == detail, "Slot " + m + ": " + (s ? s.Current + " '" + s.TitleText + "' / '" + s.DetailText + "'" : "none") + " — expected " + state + " '" + title + "' / '" + detail + "'");
        Fits(s.Title, s.Detail);
    }

    // ---- Play mode ----
    // Slots: all free ('행동 남음', gold ring at the feet, '턴 진행' ochre 'N명 행동 남음'); one member's pawn placed on an object
    // ('<사물> 수색 · 행동 완료'); a downed member ('쓰러짐 · 행동 없음'); the old tag keeps its text for the older checks.
    public static async Task<string> Slots()
    {
        var c = Owner(); var a = await Ready(c); var (q, v) = Parts(a); var pl = a.Threat.Planner; var log = new List<string>();
        int n = Living(a); await Task.Delay(200);
        for (int m = 0; m < a.Participants.Count; m++) { SlotIs(v, m, FieldMemberActionSlot.State.Free, v.FreeTitle, v.FreeDetail); Check(Glowing(a, m), "Ring at the feet of free member " + m); }
        Check(v.FreeCount == n && FieldIdleConfirm.IdleMembers(pl).Count == n, "Free count = the idle question's count: " + v.FreeCount);
        Check(Near(TurnTint(pl), v.PendingTint) && pl.TurnSubtitle.text == string.Format(v.SubtitlePending, n) && FieldMemberActionSlots.SubtitleMatches(pl, pl.Texts.TurnHushSubtitle), "'턴 진행' ochre '" + pl.TurnSubtitle.text + "'");
        Check(a.Cards.All(x => x.Action.gameObject.activeSelf && x.Action.text == pl.Texts.TagHush && x.Action.canvasRenderer.GetAlpha() < .01f == v.HideOldTag), "The old tag keeps its text (invisible on the board)");
        foreach (var card in a.Cards) Hit(card.Button);
        await Still(a, "01-slots-free"); log.Add(n + " free · ring at feet · '" + pl.TurnSubtitle.text + "'");

        int site = Open(a, 0, 1); await Place(a, site); await Task.Delay(300);
        int lead = pl.Plan.Find(site).Lead, other = lead == 0 ? 1 : 0;
        SlotIs(v, lead, FieldMemberActionSlot.State.Used, string.Format(v.SearchLead, a.ObjectNames[site]), v.Done);
        Check(v.SlotOf(lead).GlyphKind == ActionGlyph.Kind.Search, "Search glyph");
        SlotIs(v, other, FieldMemberActionSlot.State.Free, v.FreeTitle, v.FreeDetail);
        Check(!Glowing(a, lead) && Glowing(a, other), "Only the free member keeps the ring");
        Check(v.FreeCount == n - 1 && Near(TurnTint(pl), v.PendingTint) && pl.TurnSubtitle.text == string.Format(v.SubtitlePending, n - 1), "'턴 진행' '" + pl.TurnSubtitle.text + "'");
        Check(a.Cards[lead].Action.text == pl.Texts.TagLead, "Old tag text kept: " + a.Cards[lead].Action.text);
        await Still(a, "02-one-assigned"); log.Add("pawn placed → '" + v.SlotOf(lead).TitleText + " · " + v.SlotOf(lead).DetailText + "'");

        var p = a.Participants[other]; int hp = p.Health;
        try
        {
            p.Health = 0; await Task.Delay(300);
            SlotIs(v, other, FieldMemberActionSlot.State.Down, v.DownTitle, v.DownDetail);
            Check(!Glowing(a, other) && v.FreeCount == 0 && Near(TurnTint(pl), v.ReadyTint) && pl.TurnSubtitle.text == string.Format(v.SubtitleReady, a.Rooms.MinutesPerTurn), "Nobody left: green '" + pl.TurnSubtitle.text + "'");
            await Still(a, "03-down-and-ready"); log.Add("down → '" + v.DownTitle + "', '턴 진행' green '" + pl.TurnSubtitle.text + "'");
        }
        finally { p.Health = hp; }
        await Task.Delay(200); pl.Plan.Clear(); a.Threat.Refresh(); await Task.Delay(200);
        return "PASS slots · " + string.Join(" · ", log) + " · stills " + Shots;
    }

    // ---- retired (말 놓기): the quick assignment stays off; a card press picks up the pawn on the board instead ----
    static async Task<string> Retired(string name, string movedTo)
    {
        var c = Owner(); var a = await Ready(c); var (b, v) = Parts(a); var pl = a.Threat.Planner;
        var q = a.Main.GetComponentInChildren<FieldQuickAssign>(true);
        Check(!q || !q.isActiveAndEnabled, "FieldQuickAssign still runs (run BuildRetireOldAssign.Run)");
        if (q) foreach (var n in QuickChildren) { var x = q.transform.Find(n); Check(!x || !x.gameObject.activeInHierarchy, "MemberActions/" + n + " shows"); }
        await Task.Delay(200);
        await ClickCard(a, v, 0); await Task.Delay(150);
        Check(b.Held == 0 && (!q || q.Selected < 0) && v.SlotOf(0).SelectFrame.gameObject.activeSelf && !pl.Plan.HasAssignments, "A card press picks up the pawn (held " + b.Held + "), nothing assigned");
        await ClickCard(a, v, 0); Check(b.Held < 0, "The same card again lets go");
        return "PASS " + name + " retired (말 놓기): the quick assign stays off, a card press picks up the pawn · coverage: " + movedTo;
    }
    public static Task<string> ClickAssign() => Retired("ClickAssign", "VerifyPawnBoard.Input");
    public static Task<string> ListenObserve() => Retired("ListenObserve", "VerifyPawnBoard.Doors, VerifyPawnRules.Board, VerifyPawnFlow.SecondVisit");
    public static Task<string> CancelPaths() => Retired("CancelPaths", "VerifyPawnBoard.Input (Esc, right press, a press outside)");
    public static Task<string> Drag() => Retired("Drag", "VerifyPawnBoard.Input (the pawn is carried, not the card)");
    public static Task<string> TargetFirst() => Retired("TargetFirst", "VerifyPawnFlow.SecondVisit (a press with nothing held asks for a pawn)");

    public static async Task<string> All()
    {
        var r = new List<string> { await Slots(), await ClickAssign(), await ListenObserve(), await CancelPaths(), await Drag(), await TargetFirst() };
        return string.Join("\n", r);
    }
}
