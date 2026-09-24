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
using Side = Demo5.FrontEnd.FieldPlanTargetMarkers.Side;
using Shown = Demo5.FrontEnd.FieldPlanTargetMarkers.Shown;

// Exploration target markers (2026-09-25): a bubble over the target each member works at this turn (temporary layout, no approved mock).
// Wiring(): edit or play mode, after BuildFieldPlanMarkers.Run. Play mode after VerifyFieldTurnPlan.Enter (a fresh game), in this order:
// Search → ListenObserve → Move → Corridor (or All). The fixture is a real later visit (home once after the sleeping first visit,
// the place-board intro closed) reset with Threat.ReviewWake(0, 0, 0), opening chapter off. Stills in Temp/FieldPlanMarkersCapture.
public static class VerifyFieldPlanMarkers
{
    const string ArrivalPath = "Assets/Prefabs/Settlement/ExpeditionArrivalPanel.prefab", ScreenPath = "Assets/Prefabs/Settlement/SettlementScreen.prefab";
    const string BubblePath = "Assets/Prefabs/Settlement/AssignmentBubble.prefab", NodeName = "PlanTargetMarkers";
    static readonly string[] HudPaths = { "DayPaper", "PlacePaper", "TurnPaper", "RoutePaper", "ResourcePaper", "RiskPaper", "ArrivalPaper", "Hint", "Return", "Hush", "TurnAdvance", "TurnFlow/AutoAdvance" };
    const string BannerPath = "TurnFlow/TurnBanner";
    const int A = FieldSiteState.Arcade, C = FieldSiteState.Corridor, D = FieldSiteState.Den;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    public static string Wiring()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(BubblePath); Check(asset && asset.GetComponent<AssignmentBubble>(), "AssignmentBubble.prefab missing: run BuildAssignmentBubble.Run");
        var done = new List<string>();
        foreach (var path in new[] { ArrivalPath, ScreenPath })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path); Check(root, "Missing " + path);
            foreach (var a in root.GetComponentsInChildren<ExpeditionArrivalPanel>(true))
            {
                var main = a.Main.transform; var node = main.Find(NodeName); Check(node, path + ": Main/" + NodeName + " missing: run BuildFieldPlanMarkers.Run");
                var mk = node.GetComponent<FieldPlanTargetMarkers>(); Check(mk && mk.Arrival == a, path + ": markers not wired to their arrival panel");
                var group = node.GetComponent<CanvasGroup>(); Check(group && !group.blocksRaycasts && !group.interactable, path + ": the markers must pass clicks");
                Check(mk.Bubbles != null && mk.Bubbles.Length >= 6 && mk.Bubbles.All(b => b && b.transform.parent == node && !b.gameObject.activeSelf && PrefabUtility.GetCorrespondingObjectFromOriginalSource(b.gameObject) == asset), path + ": six inactive AssignmentBubble instances under the node");
                Check(node.GetComponentsInChildren<Graphic>(true).All(x => !x.raycastTarget), path + ": a raycast target inside the markers");
                // Nothing under the hotspot buttons (BuildNpcStory clones Objects[0]); the node draws over the base hotspots, under the turn banner.
                foreach (var h in main.GetComponentsInChildren<ExplorationHotspot>(true)) Check(!h.GetComponentInChildren<AssignmentBubble>(true) && !h.GetComponentInChildren<FieldPlanTargetMarkers>(true), path + ": a marker under hotspot " + h.name);
                var flow = main.Find("TurnFlow"); Check(!flow || node.GetSiblingIndex() < flow.GetSiblingIndex(), path + ": markers must stay under the turn banner (TurnFlow)");
                if (path == ArrivalPath) foreach (Transform child in main) if (child != node && child.GetComponentInChildren<ExplorationHotspot>(true)) Check(child.GetSiblingIndex() < node.GetSiblingIndex(), path + ": " + child.name + " draws over the markers");
                foreach (var hud in HudPaths) { var r = main.Find(hud) as RectTransform; if (r) Check(mk.KeepClear != null && mk.KeepClear.Contains(r), path + ": keep-clear misses " + hud); }
                var banner = main.Find(BannerPath) as RectTransform; Check(banner && mk.KeepClearAlways != null && mk.KeepClearAlways.Contains(banner), path + ": the turn banner must stay free even while hidden (KeepClearAlways)");
                Check(mk.Placements != null && mk.Placements.Any(p => p.Site == 2 && p.First == Side.Right) && mk.Placements.Any(p => p.Site == 4 && p.First == Side.Right), path + ": default placements (cabinet under the banner, fuse box)");
                Check(string.Format(mk.SearchFormat, 1, 3, 2) == "수색 · 1/3 → 2/3" && mk.ObserveLabel == "흔적 관찰", path + ": texts");
                done.Add(path.Substring(path.LastIndexOf('/') + 1) + " (" + mk.Bubbles.Length + " bubbles, " + mk.KeepClear.Length + " keep-clear)");
            }
        }
        Check(FieldPlanTargetMarkers.SearchKey(1) == "search:1" && FieldPlanTargetMarkers.ListenKey(C) == "listen:1" && FieldPlanTargetMarkers.ObserveKey("x") == "observe:x", "Keys");
        return "PASS wiring: " + string.Join(", ", done) + "; no child under hotspot buttons, no raycast targets, under TurnFlow";
    }

    // ---- helpers (as VerifyFieldTurnPlan / VerifyFieldTurnFlow) ----
    static async Task Tap(Button button) { Press(button); await Task.Delay(100); }
    static void Press(Button button)
    {
        Check(button && button.IsActive() && button.IsInteractable(), "Unavailable " + (button ? button.name : "null")); Hit(button);
        var r = (RectTransform)button.transform; var data = new PointerEventData(EventSystem.current) { position = Screen(r), button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
    }
    static Vector2 Screen(RectTransform r) { Canvas.ForceUpdateCanvases(); return RectTransformUtility.WorldToScreenPoint(r.GetComponentInParent<Canvas>().worldCamera, r.TransformPoint(r.rect.center)); }
    // The topmost thing under the centre belongs to this button (bubbles must never block a click).
    static void Hit(Selectable target)
    {
        var data = new PointerEventData(EventSystem.current) { position = Screen((RectTransform)target.transform) }; var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Selectable>() == target, "Blocked " + target.name + " by " + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
    }
    static async Task Until(Func<bool> done, int ms, string what) { var w = Stopwatch.StartNew(); while (!done()) { if (w.ElapsedMilliseconds > ms) throw new Exception("Timeout: " + what); await Task.Delay(30); } }
    static string Shots => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "FieldPlanMarkersCapture"));
    static Task Still(MonoBehaviour host, string name) { var done = new TaskCompletionSource<bool>(); host.StartCoroutine(StillRoutine(name, done)); return done.Task; }
    static IEnumerator StillRoutine(string name, TaskCompletionSource<bool> done)
    {
        yield return new WaitForEndOfFrame();
        var raw = new RenderTexture(UnityEngine.Screen.width, UnityEngine.Screen.height, 0); var rt = new RenderTexture(raw.width, raw.height, 0); ScreenCapture.CaptureScreenshotIntoRenderTexture(raw);
        if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(raw, rt, new Vector2(1, -1), new Vector2(0, 1)); else Graphics.Blit(raw, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new UnityEngine.Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); RenderTexture.active = prev; Object.Destroy(raw); Object.Destroy(rt);
        Directory.CreateDirectory(Shots); File.WriteAllBytes(Path.Combine(Shots, name + ".png"), tex.EncodeToPNG()); Object.Destroy(tex); done.SetResult(true);
    }
    static string Label(Button b) => b.GetComponentInChildren<Text>().text;
    static SettlementController Owner() { var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Run VerifyFieldTurnPlan.Enter first"); return c; }
    static FieldPlanTargetMarkers Markers(ExpeditionArrivalPanel a)
    {
        var mk = a.Main.GetComponentInChildren<FieldPlanTargetMarkers>(true); Check(mk && mk.Arrival == a && mk.Bubbles != null && mk.Bubbles.Length > 0, "Run BuildFieldPlanMarkers.Run first"); return mk;
    }
    static RectTransform Marker(Component button) => button.transform.Find("ExplorationMarkerPaper") as RectTransform;
    static Sprite Portrait(ExpeditionArrivalPanel a, int member) => a.Cards[member].Portrait.sprite;
    static string Keys(FieldPlanTargetMarkers mk) => "[" + string.Join(", ", mk.Placed.Select(x => x.Key)) + "]";
    static bool NoneShown(FieldPlanTargetMarkers mk) => mk.Placed.Count == 0 && mk.Bubbles.All(b => !b || !b.Visible);
    static Shown Expect(FieldPlanTargetMarkers mk, string key) { Check(mk.TryGet(key, out var s) && s.Bubble && s.Bubble.Visible, "No bubble " + key + " · shown " + Keys(mk)); return s; }
    static Rect[] Parts(Shown s) => s.Label.width > 0 ? new[] { s.Ring, s.Label, s.Tail } : new[] { s.Ring, s.Tail };
    static bool Covers(Shown s, Rect r) => Parts(s).Any(p => p.Overlaps(r));

    // Tail tip on the marker's top edge (lifted over a door tag at most), centred on it (+ the Inspector offset).
    static void Over(FieldPlanTargetMarkers mk, Shown s, RectTransform marker, Vector2 offset)
    {
        var m = mk.RectOf(marker);
        Check(s.Side == Side.Above && s.Anchor == marker && Mathf.Abs(s.Tip.x - (m.center.x + offset.x)) < 1 && s.Tip.y >= m.yMax + offset.y - .5f && s.Tip.y <= m.yMax + offset.y + 70,
            s.Key + " not over its marker: " + s.Side + " tip " + s.Tip + " marker " + m);
    }
    // A listener stands beside the door: not in the tag row above it, off the tag, the door name and the marker, and near it.
    static void Beside(ExpeditionArrivalPanel a, FieldPlanTargetMarkers mk, Shown s, Transform door)
    {
        var marker = Marker(door); var m = mk.RectOf(marker);
        Check(s.Side != Side.Above && s.Anchor == marker, s.Key + " must stand beside its door (the row above is the door tag): " + s.Side);
        var tag = a.Threat.DoorMarkers.FirstOrDefault(x => x && x.transform.parent == door);
        if (tag && tag.gameObject.activeInHierarchy)
        {
            var T = mk.RectOf((RectTransform)tag.transform, true);
            Check(!Covers(s, T), s.Key + " covers the door tag '" + tag.Label.text + "'");
            Check(Parts(s).All(p => p.yMax <= T.yMin + .5f || p.yMin >= T.yMax - .5f), s.Key + " stands in the door-tag row: ring " + s.Ring + " tag " + T);
        }
        var caption = door.Find("Caption") as RectTransform; if (caption && caption.gameObject.activeInHierarchy) Check(!Covers(s, mk.RectOf(caption)), s.Key + " covers the door name");
        Check(!s.Ring.Overlaps(m) && !(s.Label.width > 0 && s.Label.Overlaps(m)), s.Key + " covers the door marker");
        Check(Vector2.Distance(s.Ring.center, m.center) < 240, s.Key + " too far from its door: " + Vector2.Distance(s.Ring.center, m.center));
        Check(Mathf.Abs(s.Bubble.Rect.localScale.x - mk.ListenScale * mk.BaseScale(s.Bubble).x) < .001f, s.Key + " should be the small bubble (ListenScale × its prefab scale)");
    }
    // Every bubble inside the room, off the member cards, buttons, screen papers and the other bubbles; the world stays clickable.
    static void Clear(ExpeditionArrivalPanel a, FieldPlanTargetMarkers mk)
    {
        Canvas.ForceUpdateCanvases(); var area = mk.Area; var pl = a.Threat.Planner;
        var solid = new List<(string name, Rect rect)>();
        foreach (var card in a.Cards) if (card && card.gameObject.activeInHierarchy) solid.Add((card.name, mk.RectOf((RectTransform)card.transform)));
        foreach (var hud in HudPaths) { var r = a.Main.transform.Find(hud) as RectTransform; if (r && r.gameObject.activeInHierarchy) solid.Add((hud, mk.RectOf(r))); }
        // The turn banner covers the room during every turn sequence (while the rings fill): its place stays free even when hidden.
        var banner = a.Main.transform.Find(BannerPath) as RectTransform; if (banner) solid.Add(("the turn banner", mk.RectOf(banner, true)));
        foreach (var s in mk.Placed)
        {
            foreach (var p in Parts(s)) Check(p.xMin >= area.xMin - .5f && p.xMax <= area.xMax + .5f && p.yMin >= area.yMin - .5f && p.yMax <= area.yMax + .5f, s.Key + " leaves the room area: " + p + " / " + area);
            foreach (var x in solid) Check(!Covers(s, x.rect), s.Key + " covers " + x.name);
            foreach (var o in mk.Placed) if (o.Key != s.Key) Check(!Parts(s).Any(p => Parts(o).Any(q => p.Overlaps(q))), s.Key + " overlaps " + o.Key);
            Check(s.Bubble.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), s.Key + " has a raycast target");
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = Screen(s.Bubble.Ring ? s.Bubble.Ring.rectTransform : s.Bubble.Rect) }, hits);
            Check(hits.All(h => !h.gameObject.GetComponentInParent<AssignmentBubble>()), s.Key + " takes clicks");
        }
        foreach (var target in new Selectable[] { pl.TurnButton, a.Threat.Hush, a.Return }.Concat(a.Cards.Where(x => x && x.gameObject.activeInHierarchy).Select(x => (Selectable)x.Button)))
            if (target && target.gameObject.activeInHierarchy) Hit(target);
    }
    // The ring while the turn plays: it passes through in-between values (animated, not snapped), never goes back, ends on the new step
    // with the 'this turn' cells back. One still mid-way.
    static async Task<string> RingFill(ExpeditionArrivalPanel a, FieldPlanTargetMarkers mk, string key, int from, int to, int next, string still)
    {
        var samples = new List<float>(); bool shot = false, ended = false; var w = Stopwatch.StartNew();
        while (w.ElapsedMilliseconds < 4000)
        {
            var b = mk.BubbleFor(key);
            if (b)
            {
                samples.Add(b.Progress);
                if (!shot && b.Progress > from + .2f && b.Progress < to - .2f) { shot = true; await Still(a, still); continue; }
                if (Mathf.Abs(b.Progress - to) < .001f && b.Done == to && b.Next == next) { ended = true; break; }
            }
            await Task.Delay(25);
        }
        string trace = string.Join(" ", samples.Select(x => x.ToString("0.00")));
        Check(ended, key + " ring did not settle on " + to + ": " + trace);
        Check(samples.Any(x => x > from + .05f && x < to - .05f), key + " ring snapped instead of filling: " + trace);
        for (int i = 1; i < samples.Count; i++) Check(samples[i] >= samples[i - 1] - .001f, key + " ring went back: " + trace);
        if (!shot) await Still(a, still);
        return samples.Count + " samples " + samples.First().ToString("0.00") + "→" + samples.Last().ToString("0.00");
    }
    static async Task CloseLoot(ExpeditionArrivalPanel a) { if (a.Loot.IsOpen) { await Tap(a.Loot.Back); await Task.Delay(120); if (a.Loot.LeaveReview.activeSelf) await Tap(a.Loot.LeaveConfirm); await Task.Delay(150); } }
    static async Task Door(ExpeditionArrivalPanel a, Button door) { door.onClick.Invoke(); await Task.Delay(200); Check(a.Popup.activeSelf, "Door popup " + door.name); }
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
    // A real later visit in the arcade (the trace can be offered), reset to a quiet board with nothing assigned.
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
        a.Threat.ReviewWake(0, 0, 0); await Task.Delay(250); if (a.Popup.activeSelf) await Tap(a.PopupBack);
        var pl = a.Threat.Planner;
        Check(pl.Active && a.Threat.IntroAcknowledged && !pl.Plan.HasAssignments && !a.Rooms.HasQueuedMove && a.Rooms.CurrentRoom == A && !a.InTransit, "Later-visit board in the arcade with nothing assigned");
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
    static int Fresh(ExpeditionArrivalPanel a, params int[] sites)
    {
        foreach (int s in sites) if (!a.Loot.Peek(s, out var st) || st.Progress == 0 && !st.Complete) return s;
        throw new Exception("Fixture: no unsearched object among " + string.Join(",", sites.Select(x => a.ObjectNames[x])) + " (start from VerifyFieldTurnPlan.Enter)");
    }
    // One member (the 07 window's default lead) searches the object alone at this pace; saved only, no time. The window hides the bubbles.
    static async Task Assign(ExpeditionArrivalPanel a, FieldPlanTargetMarkers mk, int site, int pace)
    {
        var s = a.Search; var pl = a.Threat.Planner;
        a.Inspect(site); await Until(() => s.IsOpen, 2000, "search " + site); await Task.Delay(150);
        Check(!mk.Free && NoneShown(mk), "Bubbles wait while the search window covers the room · shown " + Keys(mk));
        if (s.Worker == null) await Tap(s.Cards[0].Button);
        if (!a.Loot.Peek(site, out var st) || st.Progress == 0) { await Tap(s.Paces[pace]); if (!s.Solo && s.Duties[0].interactable) await Tap(s.Duties[0]); }
        await Tap(s.Choose); Check(Label(s.Confirm) == pl.Texts.ConfirmSave, "Save only: " + Label(s.Confirm)); await Tap(s.Confirm); await Task.Delay(150);
        if (s.IsOpen) await Tap(s.Back);
        Check(pl.Plan.Find(site) != null, "Assigned " + a.ObjectNames[site]);
    }

    // ---- Play mode ----
    // Search: no bubble while everyone hushes; a saved order → a bubble over that object (lead portrait, magnifier, '수색 · 0/3 → 1/3',
    // ring 0 + 1 of 3); one '턴 진행' fills the ring over the turn sequence to '1/3 → 2/3'; a window over the room hides it.
    public static async Task<string> Search()
    {
        var c = Owner(); var a = await Ready(c); var mk = Markers(a); var pl = a.Threat.Planner; var log = new List<string>();
        await Task.Delay(200); Check(mk.Free && NoneShown(mk), "Nothing assigned (everyone hushes): no bubble · shown " + Keys(mk));
        int site = Fresh(a, 0, 1); string key = FieldPlanTargetMarkers.SearchKey(site); int turns = a.Rooms.Turns;
        await Assign(a, mk, site, 2); await Task.Delay(400);
        var order = pl.Plan.Find(site); var s = Expect(mk, key); var b = s.Bubble;
        Check(a.Rooms.Turns == turns, "Assigning costs no time");
        Check(b.PortraitCount == 1 && b.PortraitAt(0) == Portrait(a, order.Lead), "Portrait = the lead (" + a.Participants[order.Lead].Name + ")");
        Check(b.GlyphKind == ActionGlyph.Kind.Search && b.LabelText == string.Format(mk.SearchFormat, 0, 3, 1) && b.CurrentTone == AssignmentBubble.Tone.Planned, "Search bubble: " + b.GlyphKind + " '" + b.LabelText + "' " + b.CurrentTone);
        Check(b.Done == 0 && b.Next == 1 && b.Total == 3 && Mathf.Abs(b.Progress) < .001f, "Ring 0 + 1 of 3: " + b.Done + "+" + b.Next + "/" + b.Total);
        Over(mk, s, Marker(a.Objects[site]), mk.OffsetFor(site)); Clear(a, mk);
        await Still(a, "01-search-over-object"); log.Add(a.ObjectNames[site] + " '" + b.LabelText + "'");

        int version = FieldTurnPulse.Version; Press(pl.TurnButton);
        Check(a.Rooms.Turns == turns + 1 && pl.LastMismatch == "", "One turn, preview = result");
        string fill = await RingFill(a, mk, key, 0, 1, 1, "02-ring-filling");
        b = Expect(mk, key).Bubble;
        Check(b.Done == 1 && b.Next == 1 && b.Total == 3 && b.LabelText == string.Format(mk.SearchFormat, 1, 3, 2), "After the turn: " + b.Done + "+" + b.Next + "/" + b.Total + " '" + b.LabelText + "'");
        bool replay = a.Main.GetComponentInChildren<FieldTurnReplay>(true); if (replay) Check(FieldTurnPulse.Version == version + 1, "One turn sequence");
        Over(mk, Expect(mk, key), Marker(a.Objects[site]), mk.OffsetFor(site)); Clear(a, mk);
        await Still(a, "03-after-turn"); log.Add("turn → ring " + fill + (replay ? " (with the turn sequence)" : " (fallback timing)") + ", '" + b.LabelText + "'");

        a.Inspect(site); await Until(() => a.Search.IsOpen, 2000, "search window"); await Task.Delay(150);
        Check(!mk.Free && NoneShown(mk), "Hidden under the search window");
        await Tap(a.Search.Back); await Task.Delay(300); Check(mk.Free && Expect(mk, key).Bubble.Visible, "Back once the window closed");
        log.Add("hidden under the 07 window");
        return "PASS search markers · " + string.Join(" · ", log) + " · stills " + Shots;
    }

    // Listen and observe: the listener's small bubble beside the door (the tag row above keeps '귀 대는 중'); observing the trace moves
    // that member's bubble over the trace.
    public static async Task<string> ListenObserve()
    {
        var c = Owner(); var a = await Continue(c); var mk = Markers(a); var t = a.Threat; var pl = t.Planner; var log = new List<string>();
        Check(a.Participants.Count >= 2 && a.Participants.Count(p => p.Health > 0) >= 2, "Needs two living members");
        int turns = a.Rooms.Turns; var door = a.Objects[3];
        await Door(a, door); await Until(() => pl.ListenButton.gameObject.activeSelf, 1000, "listen button");
        Check(!mk.Free && NoneShown(mk), "Hidden under the door popup");
        await Tap(pl.ListenButton); await Task.Delay(400);
        var listen = pl.Plan.ListenAt(C); Check(listen != null && a.Rooms.Turns == turns, "Listening at the corridor door without time");
        var s = Expect(mk, FieldPlanTargetMarkers.ListenKey(C)); var b = s.Bubble;
        Check(b.PortraitCount == 1 && b.PortraitAt(0) == Portrait(a, listen.Member) && b.GlyphKind == ActionGlyph.Kind.Listen && b.LabelText == mk.ListenLabel && b.Total == 0, "Listen bubble: " + b.GlyphKind + " '" + b.LabelText + "' ring " + b.Total);
        var tag = t.DoorMarkers.First(m => m && m.Room == A && m.From == C);
        Check(tag.gameObject.activeSelf && tag.Label.text == t.ListenPending, "The door tag keeps '" + t.ListenPending + "': " + (tag.gameObject.activeSelf ? tag.Label.text : "hidden"));
        Beside(a, mk, s, door.transform); Clear(a, mk);
        await Still(a, "04-listen-beside-door"); log.Add(a.Participants[listen.Member].Name + " listens · " + s.Side + " of the door");

        var story = a.Story;
        if (story && story.Clue && story.Clue.gameObject.activeInHierarchy && story.State.Stage == 0 && story.ObserveBlock(ExpeditionNpcStory.ObservationId) == FieldPause.None)
        {
            int who = listen.Member;
            await Tap(story.Clue); await Until(() => story.IsOpen, 2000, "trace window"); await Task.Delay(120);
            Check(!mk.Free && NoneShown(mk), "Hidden under the trace window");
            await Tap(story.Members[who]); await Tap(story.Choices[0]); await Task.Delay(400);
            var observe = pl.Plan.ObserveAt(ExpeditionNpcStory.ObservationId);
            Check(!story.IsOpen && observe != null && observe.Member == who && pl.Plan.ListenAt(C) == null && a.Rooms.Turns == turns, "Observing the trace, off the door, no time");
            s = Expect(mk, FieldPlanTargetMarkers.ObserveKey(ExpeditionNpcStory.ObservationId)); b = s.Bubble;
            Check(b.PortraitCount == 1 && b.PortraitAt(0) == Portrait(a, who) && b.GlyphKind == ActionGlyph.Kind.Observe && b.LabelText == mk.ObserveLabel, "Observe bubble: " + b.GlyphKind + " '" + b.LabelText + "'");
            Check(b.Done == 0 && b.Next == (mk.ObserveRing ? 1 : 0) && b.Total == (mk.ObserveRing ? 1 : 0) && b.CurrentTone == AssignmentBubble.Tone.Planned, "Observe ring " + b.Done + "+" + b.Next + "/" + b.Total);
            Check(mk.BubbleFor(FieldPlanTargetMarkers.ListenKey(C)) == null, "The door bubble left with the listener · shown " + Keys(mk));
            Over(mk, s, Marker(story.Clue), Vector2.zero); Clear(a, mk);
            await Still(a, "05-observe-over-trace"); log.Add("observe over the trace '" + b.LabelText + "'");
        }
        else log.Add("observe skipped: the trace is not offered (stage " + (story ? story.State.Stage : -1) + ")");
        return "PASS listen/observe markers · " + string.Join(" · ", log) + " · stills " + Shots;
    }

    // Move reservation: only the door bubble (every living member, arrow, '다음 턴 · 복도로 이동'); cancelling brings the others back;
    // the turn is the walk (hidden meanwhile) and the new room starts with no bubble.
    public static async Task<string> Move()
    {
        var c = Owner(); var a = await Continue(c); var mk = Markers(a); var t = a.Threat; var pl = t.Planner; var n = a.Rooms; var log = new List<string>();
        var door = a.Objects[3]; int turns = n.Turns; var before = mk.Placed.Select(x => x.Key).ToList();
        await Door(a, door); Check(!mk.Free && NoneShown(mk), "Hidden under the door popup");
        await Tap(a.ReturnConfirm); await Task.Delay(400);
        Check(n.HasQueuedMove && n.QueuedRoom == C && n.Turns == turns && !a.InTransit, "Move reserved without time");
        var s = Expect(mk, FieldPlanTargetMarkers.MoveKey); var b = s.Bubble;
        Check(mk.Placed.Count == 1, "Only the door bubble while a move is reserved · shown " + Keys(mk));
        var living = Enumerable.Range(0, a.Participants.Count).Where(m => a.Participants[m].Health > 0).ToList();
        Check(b.PortraitCount == Mathf.Min(3, living.Count) && Enumerable.Range(0, b.PortraitCount).All(i => b.PortraitAt(i) == Portrait(a, living[i])), "Every living member on the door: " + b.PortraitCount);
        string moveLabel = living.Count > b.Portraits.Length ? string.Format(mk.MoveCrowdFormat, n.QueuedLabel, living.Count) : n.QueuedLabel;
        Check(b.GlyphKind == ActionGlyph.Kind.Move && b.LabelText == moveLabel && b.Total == (mk.MoveRing ? n.QueuedTurns : 0), "Move bubble: " + b.GlyphKind + " '" + b.LabelText + "' ring " + b.Total);
        Over(mk, s, Marker(door), Vector2.zero); Clear(a, mk);
        await Still(a, "06-move-reserved-over-door"); log.Add("'" + b.LabelText + "' with " + b.PortraitCount + " portraits, others hidden " + string.Join("/", before));

        await Door(a, door); await Tap(a.ReturnConfirm); await Task.Delay(400);
        Check(!n.HasQueuedMove && mk.BubbleFor(FieldPlanTargetMarkers.MoveKey) == null && before.All(k => mk.BubbleFor(k)), "Cancelled: the assignment bubbles are back · shown " + Keys(mk));
        log.Add("cancel → " + Keys(mk));

        await Door(a, door); await Tap(a.ReturnConfirm); await Task.Delay(200); Check(n.HasQueuedMove, "Reserved again");
        await Tap(pl.TurnButton); Check(a.InTransit && n.Turns == turns + 1, "The turn is the move");
        await Task.Delay(150); Check(!mk.Free && NoneShown(mk), "Hidden during the walk");
        await Until(() => !a.InTransit && n.CurrentRoom == C, 6000, "corridor"); await Task.Delay(400);
        Check(!pl.Plan.HasAssignments && mk.Free && NoneShown(mk), "Arrived: the move released every assignment, no bubble · shown " + Keys(mk));
        log.Add("turn → walk (hidden) → corridor, none left");
        return "PASS move markers · " + string.Join(" · ", log) + " · stills " + Shots;
    }

    // Corridor: a search over the right crate and a listener beside the office door, clear of the den tag that is always up there;
    // one turn fills the crate ring.
    public static async Task<string> Corridor()
    {
        var c = Owner(); var a = c.ArrivalPanel; var log = new List<string>();
        if (!(a.IsOpen && a.Threat && a.Threat.Active && a.Rooms.CurrentRoom == C && !a.InTransit)) log.Add((await Move()).Split('·')[0].Trim());
        var mk = Markers(a); var t = a.Threat; var pl = t.Planner; var n = a.Rooms; await Settle(a);
        Check(n.CurrentRoom == C && pl.Active, "In the corridor on the board");
        int site = Fresh(a, 5); string key = FieldPlanTargetMarkers.SearchKey(site);
        await Assign(a, mk, site, 2); await Task.Delay(300);
        await Door(a, n.OfficeDoor); await Until(() => pl.ListenButton.gameObject.activeSelf, 1000, "den listen button"); await Tap(pl.ListenButton); await Task.Delay(400);
        var listen = pl.Plan.ListenAt(D); Check(listen != null, "Listening at the office door");
        var den = t.DoorMarkers.First(m => m && m.Room == C && m.From == D); Check(den.gameObject.activeSelf, "The den tag is up in the corridor");
        var sb = Expect(mk, key); var lb = Expect(mk, FieldPlanTargetMarkers.ListenKey(D));
        Check(sb.Bubble.LabelText == string.Format(mk.SearchFormat, 0, 3, 1) && lb.Bubble.GlyphKind == ActionGlyph.Kind.Listen && lb.Bubble.PortraitAt(0) == Portrait(a, listen.Member), "Corridor bubbles: '" + sb.Bubble.LabelText + "' / " + lb.Bubble.GlyphKind);
        Over(mk, sb, Marker(a.Objects[site]), mk.OffsetFor(site)); Beside(a, mk, lb, n.OfficeDoor.transform); Clear(a, mk);
        await Still(a, "07-corridor-search-and-den-listen"); log.Add(a.ObjectNames[site] + " + den listener (" + lb.Side + " of the door, clear of '" + den.Label.text + "')");

        int turns = n.Turns; Press(pl.TurnButton); Check(n.Turns == turns + 1 && pl.LastMismatch == "", "One turn, preview = result");
        string fill = await RingFill(a, mk, key, 0, 1, 1, "08-corridor-ring-filling");
        sb = Expect(mk, key); lb = Expect(mk, FieldPlanTargetMarkers.ListenKey(D));
        Check(sb.Bubble.LabelText == string.Format(mk.SearchFormat, 1, 3, 2), "After the turn: '" + sb.Bubble.LabelText + "'");
        Beside(a, mk, lb, n.OfficeDoor.transform); Clear(a, mk);
        await Still(a, "09-corridor-after-turn"); log.Add("turn → ring " + fill + ", '" + sb.Bubble.LabelText + "', den tag '" + den.Label.text + "'");
        return "PASS corridor markers · " + string.Join(" · ", log) + " · stills " + Shots;
    }

    public static async Task<string> All()
    {
        var r1 = await Search(); var r2 = await ListenObserve(); var r3 = await Move(); var r4 = await Corridor();
        return string.Join("\n", r1, r2, r3, r4);
    }
}
