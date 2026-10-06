using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 말 놓기 판 (2026-09-25, 기획/탐험-말놓기-조작-재설계.md · 시안 탐험-말놓기-시안/01~03 · design §1, §3, §5 Group A).
// Edit or play mode, after BuildPawnRules.Run → BuildPawnBoard.Run (→ BuildRetireOldAssign.Run):
//   Wiring()  the prefab nodes, pools, raycast targets, the ghost prefab, PawnGhosts, SettlementScreen inherits.
//   Spots()   every place FieldPlacement can offer has an authored spot with enough slots, on the floor, slots apart; floors + idle.
// Play mode after VerifyFieldTurnPlan.Enter (a fresh game): Input → Walking → Facing → Doors → RightClick → Captures (or All).
// Every play entry starts from a real later visit in the arcade (home once after the sleeping first visit, the board intro
// closed) reset with Threat.ReviewWake(0, 0, 0), opening chapter off, nothing placed, FieldIdleConfirm.AutoAccept on. The old
// assignment layers (search note, quick assign) are switched off for the check only if the retire builder has not run (by name:
// this script never names their classes). Presses are real UI events: a raycast at the screen point, the click sent to the
// topmost hit (as the input module does); drags send the drag events to the pawn's handle. Stills in Temp/PawnBoardCapture.
public static class VerifyPawnBoard
{
    const string P = "Assets/Prefabs/Settlement/", ArrivalPath = P + "ExpeditionArrivalPanel.prefab", ScreenPath = P + "SettlementScreen.prefab", WorldPath = P + "ExpeditionWorld.prefab", GhostPath = P + "FieldPawnGhost.prefab";
    const string NodeName = "PawnBoard";
    const int A = FieldSiteState.Arcade, C = FieldSiteState.Corridor, S = FieldSiteState.Storage;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    // ---- wiring (edit or play) ----
    public static string Wiring()
    {
        var done = new List<string>();
        var ghost = AssetDatabase.LoadAssetAtPath<GameObject>(GhostPath); Check(ghost, "Missing " + GhostPath + " (run BuildPawnBoard.Run)");
        Check(ghost.GetComponentsInChildren<PawnGroundShadow>(true).Length == 0, "The ghost prefab has no floor shadow");
        Check(ghost.GetComponent<PawnFacing>() && ghost.transform.Find("Body") && ghost.transform.Find("Base"), "The ghost keeps the pawn's parts (PawnFacing, Body, Base)");
        done.Add("FieldPawnGhost (" + PrefabUtility.GetPrefabAssetType(ghost) + ", no shadow)");
        var world = AssetDatabase.LoadAssetAtPath<GameObject>(WorldPath); Check(world, "Missing " + WorldPath);
        var ghosts = world.transform.Find("PawnGhosts"); var pawns = world.transform.Find("Pawns");
        Check(ghosts && pawns && ghosts.parent == world.transform && !ghosts.IsChildOf(pawns), "ExpeditionWorld/PawnGhosts beside Pawns (never under it)");
        done.Add("ExpeditionWorld/PawnGhosts");
        foreach (var path in new[] { ArrivalPath, ScreenPath })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path); Check(root, "Missing " + path);
            foreach (var a in root.GetComponentsInChildren<ExpeditionArrivalPanel>(true))
            {
                var main = a.Main.transform; var node = main.Find(NodeName); Check(node, path + ": Main/" + NodeName + " missing");
                Check(main.Cast<Transform>().Skip(node.GetSiblingIndex() + 1).All(x => PrefabUtility.IsAddedGameObjectOverride(x.gameObject)), path + ": " + NodeName + " must be Main's last own child (added room hotspots may follow; it moves above them at runtime)");
                var b = node.GetComponent<FieldPawnBoard>(); Check(b && b.Arrival == a, path + ": FieldPawnBoard wired to its arrival panel");
                Check(b.Catcher && b.Catcher.raycastTarget && !b.Catcher.gameObject.activeSelf, path + ": an inactive catcher taking presses");
                Check(b.GhostPrefab == ghost && b.Roster, path + ": ghost prefab and roster");
                Check(b.Handles != null && b.Handles.Length >= 6 && b.Handles.All(h => h && !h.gameObject.activeSelf && h.Role == FieldPawnHandle.Kind.Pawn && h.Board == b && h.Button && h.GetComponent<Image>() && h.GetComponent<Image>().raycastTarget), path + ": 6 inactive pawn handles");
                Check(b.GhostHandles != null && b.GhostHandles.Length >= 8 && b.GhostHandles.All(h => h && !h.gameObject.activeSelf && h.Role == FieldPawnHandle.Kind.Ghost && h.Board == b && h.Button), path + ": 8 inactive silhouette handles");
                Check(b.Pins != null && b.Pins.Length >= 8 && b.Pins.All(g => g && !g.gameObject.activeSelf && g.Visual && g.Pin && g.Disc && g.Glyph && g.Halo && g.HitRect && g.Caption && g.CaptionPaper && g.Lock), path + ": 8 inactive pins with caption and padlock");
                Check(b.ChipRows != null && b.ChipRows.Length >= 1 && b.ChipRows.All(r => r && !r.gameObject.activeSelf && r.GetComponentsInChildren<Button>(true).Length >= 3), path + ": a chip row of 3");
                Check(b.Tags != null && b.Tags.Length >= 1 && b.Tags.All(t => t && !t.gameObject.activeSelf && t.GetComponentInChildren<Text>(true)), path + ": paper tags");
                // Order: catcher, handles, silhouettes above the pawns, pins, chips, tags.
                var order = new[] { "Catcher", "Handles", "Ghosts", "Pins", "Chips", "Tags" }.Select(n => node.Find(n)).ToList();
                Check(order.All(t => t), path + ": board layers present"); for (int i = 1; i < order.Count; i++) Check(order[i - 1].GetSiblingIndex() < order[i].GetSiblingIndex(), path + ": layer order " + order[i - 1].name + " < " + order[i].name);
                var allowed = new HashSet<Graphic> { b.Catcher }; foreach (var h in b.Handles.Concat(b.GhostHandles)) allowed.Add(h.GetComponent<Image>()); foreach (var r in b.ChipRows) foreach (var ch in r.GetComponentsInChildren<Button>(true)) allowed.Add(ch.targetGraphic);
                foreach (var g in node.GetComponentsInChildren<Graphic>(true)) Check(!g.raycastTarget || allowed.Contains(g), path + ": unexpected raycast target " + g.name);
                foreach (var h in main.GetComponentsInChildren<ExplorationHotspot>(true)) Check(!h.GetComponentInChildren<FieldPawnHandle>(true) && !h.GetComponentInChildren<FieldPawnBoard>(true), path + ": something of the board under hotspot " + h.name);
                var slots = main.GetComponentInChildren<FieldMemberActionSlots>(true); Check(slots && slots.PawnBoard == b, path + ": FieldMemberActionSlots.PawnBoard");
                // The catcher stays above the member row (the cards stay pressable while a pawn is held).
                var members = a.MemberContent ? a.MemberContent.parent as RectTransform : null;
                if (members) { var cr = RectIn(b.Catcher.rectTransform, (RectTransform)main); var mr = RectIn(members, (RectTransform)main); Check(cr.yMin >= mr.yMax - .5f, path + ": the catcher reaches into the member row (" + cr.yMin + " < " + mr.yMax + ")"); }
                done.Add(path.Substring(path.LastIndexOf('/') + 1) + " (" + b.Handles.Length + " handles, " + b.GhostHandles.Length + " silhouettes, " + b.Pins.Length + " pins)");
            }
        }
        return "PASS wiring: " + string.Join(", ", done);
    }

    // ---- spot data (edit or play) ----
    // Every place FieldPlacement offers (FieldSpotRef keys) has an authored spot: objects 2 slots (lead, helper), move doors
    // FieldPlacement.DoorSlots, the den door and the trace 1; all on the room's floor band, the slots of one place apart.
    public static string Spots()
    {
        var world = AssetDatabase.LoadAssetAtPath<GameObject>(WorldPath); Check(world, "Missing " + WorldPath);
        var p = world.GetComponent<ExplorationRoomPresentation>(); Check(p, "ExpeditionWorld has no ExplorationRoomPresentation");
        var want = new List<(int Room, string Key, int Slots)>
        {
            (A, FieldSpotRef.SearchKey(0), 2), (A, FieldSpotRef.SearchKey(1), 2), (A, FieldSpotRef.SearchKey(2), 2), (A, FieldSpotRef.DoorKey(C), FieldPlacement.DoorSlots), (A, FieldSpotRef.ObserveKey(ExpeditionNpcStory.ObservationId), 1),
            (C, FieldSpotRef.SearchKey(4), 2), (C, FieldSpotRef.SearchKey(5), 2), (C, FieldSpotRef.SearchKey(8), 2), (C, FieldSpotRef.DoorKey(A), FieldPlacement.DoorSlots), (C, FieldSpotRef.DoorKey(S), FieldPlacement.DoorSlots), (C, FieldSpotRef.DoorKey(FieldSiteState.Den), 1),
            (S, FieldSpotRef.SearchKey(6), 2), (S, FieldSpotRef.SearchKey(7), 2), (S, FieldSpotRef.DoorKey(C), FieldPlacement.DoorSlots),
        };
        var notes = new List<string>(); int slots = 0;
        foreach (var w in want)
        {
            var s = p.SpotOf(w.Room, w.Key); Check(s != null && s.Slots != null, "No spot for room " + w.Room + " " + w.Key + " (run BuildPawnBoard.Run)");
            Check(s.Slots.Length >= w.Slots, w.Room + " " + w.Key + ": " + s.Slots.Length + " slots < " + w.Slots);
            Check(!float.IsNaN(s.LookAtX) && Mathf.Abs(s.LookAtX) <= 9.6f, w.Room + " " + w.Key + ": LookAtX inside the room");
            for (int i = 0; i < s.Slots.Length; i++)
            {
                var v = s.Slots[i]; Check(p.OnFloor(w.Room, v) && Mathf.Abs(v.x) <= 8.8f, w.Room + " " + w.Key + " slot " + i + " " + v + " is off the floor band " + p.BandOf(w.Room));
                for (int j = 0; j < i; j++) Check(p.Apart(v, s.Slots[j]), w.Room + " " + w.Key + ": slots " + j + " and " + i + " overlap");
                Check(p.TrySpot(w.Room, w.Key, i, out var feet, out float look) && ((Vector2)feet - v).sqrMagnitude < 1e-6f, "TrySpot reads the authored slot " + w.Key + " " + i);
                slots++;
            }
            // A queue longer than the authored slots continues on the floor.
            Check(p.TrySpot(w.Room, w.Key, s.Slots.Length + 1, out var more, out float _) && p.OnFloor(w.Room, more), w.Key + ": an extra slot stays on the floor");
        }
        // Different places of one room that share floor: noted (tune in Play Mode, design §3), not failed.
        foreach (var room in new[] { A, C, S })
        {
            var list = want.Where(w => w.Room == room).Select(w => p.SpotOf(w.Room, w.Key)).ToList();
            for (int i = 0; i < list.Count; i++) for (int j = 0; j < i; j++)
                    foreach (var x in list[i].Slots.Take(2)) foreach (var y in list[j].Slots.Take(2)) if (!p.Apart(x, y)) notes.Add(room + " " + list[i].Key + "×" + list[j].Key);
            var f = p.FloorOf(room); Check(f != null && f.Band.width > 0 && f.Band.height > 0, "No floor band for room " + room);
            Check(f.Band.yMin >= -2.0f && f.Band.yMax <= .5f, "Room " + room + ": the band stays above the member tray and below the back wall " + f.Band);
            Check(f.Idle != null && f.Idle.Length >= 4, "Room " + room + ": at least 4 idle spots");
            for (int i = 0; i < f.Idle.Length; i++) { Check(p.OnFloor(room, f.Idle[i]), "Room " + room + " idle " + i + " off the floor"); for (int j = 0; j < i; j++) Check(p.Apart(f.Idle[i], f.Idle[j]), "Room " + room + " idle spots " + j + "/" + i + " overlap"); }
        }
        // The free-standing helpers keep clear of what is taken.
        var taken = new List<Vector3> { new Vector3(0, -1.85f, 0) };
        var near = p.NearestClear(A, new Vector3(.2f, -1.85f, 0), taken); Check(p.Apart(near, taken[0]) && p.OnFloor(A, near), "NearestClear steps aside on the floor");
        var idle = p.NearestIdle(A, new Vector3(1.08f, -1.59f, 0), taken); Check(p.Apart(idle, taken[0]), "NearestIdle avoids a taken spot");
        return "PASS spots: " + want.Count + " places, " + slots + " slots on the floor bands, floors and idle spots for 3 rooms" + (notes.Count > 0 ? " · shared floor to tune in Play Mode: " + string.Join(", ", notes.Distinct()) : "");
    }

    // ---- helpers ----
    static Camera Ui(Component c) { var canvas = c.GetComponentInParent<Canvas>().rootCanvas; return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera; }
    static Vector2 Screen(RectTransform r) { Canvas.ForceUpdateCanvases(); return RectTransformUtility.WorldToScreenPoint(Ui(r), r.TransformPoint(r.rect.center)); }
    static Rect RectIn(RectTransform r, RectTransform space)
    {
        var c = new Vector3[4]; r.GetWorldCorners(c); var a = space.InverseTransformPoint(c[0]); var b = space.InverseTransformPoint(c[2]);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }
    static Rect ScreenRect(RectTransform r)
    {
        var c = new Vector3[4]; r.GetWorldCorners(c); var cam = Ui(r); Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]), b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }
    static GameObject Top(Vector2 at)
    {
        Canvas.ForceUpdateCanvases(); var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = at }, hits);
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
    static void Hover(GameObject target, bool on)
    {
        var data = new PointerEventData(EventSystem.current) { position = Vector2.zero };
        if (on) ExecuteEvents.Execute(target, data, ExecuteEvents.pointerEnterHandler); else ExecuteEvents.Execute(target, data, ExecuteEvents.pointerExitHandler);
    }
    static async Task Until(Func<bool> done, int ms, string what) { var w = Stopwatch.StartNew(); while (!done()) { if (w.ElapsedMilliseconds > ms) throw new Exception("Timeout: " + what); await Task.Delay(30); } }
    static string Shots => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "PawnBoardCapture"));
    static Task Still(MonoBehaviour host, string name) { var done = new TaskCompletionSource<bool>(); host.StartCoroutine(StillRoutine(name, done)); return done.Task; }
    static IEnumerator StillRoutine(string name, TaskCompletionSource<bool> done)
    {
        yield return new WaitForEndOfFrame();
        var raw = new RenderTexture(UnityEngine.Screen.width, UnityEngine.Screen.height, 0); var rt = new RenderTexture(raw.width, raw.height, 0); ScreenCapture.CaptureScreenshotIntoRenderTexture(raw);
        if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(raw, rt, new Vector2(1, -1), new Vector2(0, 1)); else Graphics.Blit(raw, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new UnityEngine.Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); RenderTexture.active = prev; Object.Destroy(raw); Object.Destroy(rt);
        Directory.CreateDirectory(Shots); File.WriteAllBytes(Path.Combine(Shots, name + ".png"), tex.EncodeToPNG()); Object.Destroy(tex); done.SetResult(true);
    }
    static SettlementController Owner() { var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Run VerifyFieldTurnPlan.Enter first"); return c; }
    static FieldPawnBoard BoardOf(ExpeditionArrivalPanel a) { var b = FieldPawnBoard.For(a); Check(b && b.isActiveAndEnabled, "Run BuildPawnBoard.Run first (Main/PawnBoard)"); return b; }
    static ExplorationRoomPresentation Pres(ExpeditionArrivalPanel a) => a.World.GetComponent<ExplorationRoomPresentation>();
    // The old assignment layers, off for this check when the retire builder has not run yet (found by name, never by class).
    static void RetireOld(ExpeditionArrivalPanel a)
    {
        var note = a.Main.transform.Find("SearchNote"); if (note && note.gameObject.activeSelf) note.gameObject.SetActive(false);
        foreach (var mb in a.Main.GetComponentsInChildren<MonoBehaviour>(true)) if (mb && mb.enabled && (mb.GetType().Name == "FieldQuickAssign" || mb.GetType().Name == "FieldSearchNote")) mb.enabled = false;
    }
    static async Task CloseLoot(ExpeditionArrivalPanel a) { if (a.Loot.IsOpen) { a.Loot.Escape(); await Task.Delay(120); if (a.Loot.IsOpen && a.Loot.LeaveReview.activeSelf) a.Loot.Escape(); await Task.Delay(150); } }
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
    // A real later visit in the arcade, reset to a quiet board with nothing placed and nothing held.
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
            if (a.Popup.activeSelf) a.ClosePopup();
        }
        RetireOld(a);
        var b = BoardOf(a); b.Cancel();
        if (a.Rooms.HasQueuedMove) a.Rooms.CancelQueuedMove();
        a.Threat.ReviewWake(0, 0, 0); await Task.Delay(250); if (a.Popup.activeSelf) a.ClosePopup();
        foreach (var p in a.Participants) Check(p.Health > 0, "Fixture: every member alive (" + p.Name + ")");
        var pl = a.Threat.Planner;
        Check(pl.Placing && pl.Active && !pl.Plan.HasAssignments && !a.Rooms.HasQueuedMove && a.Rooms.CurrentRoom == A && !a.InTransit && a.Participants.Count >= 2, "Later-visit board in the arcade, two members, nothing placed");
        await Until(() => !b.IsWalking, 3000, "pawns settle"); await Task.Delay(150);
        return a;
    }
    static int Open(ExpeditionArrivalPanel a, params int[] sites)
    {
        foreach (int s in sites) if (!a.Loot.Peek(s, out var st) || !st.Complete) return s;
        throw new Exception("Fixture: no unfinished object among " + string.Join(",", sites.Select(x => a.ObjectNames[x])) + " (start from VerifyFieldTurnPlan.Enter)");
    }
    static SpriteRenderer BodyOf(ExpeditionArrivalPanel a, int m) => a.PartyPawns[m].transform.Find("Body").GetComponent<SpriteRenderer>();
    // The screen point of a pawn's body centre (the world camera draws the pawns).
    static Vector2 PawnPoint(ExpeditionArrivalPanel a, FieldPawnBoard b, int m) => Camera.main.WorldToScreenPoint(b.VisibleBounds(BodyOf(a, m)).center);
    static Vector2 GhostPoint(FieldPawnBoard b, Button ghost) { var h = ghost.GetComponent<FieldPawnHandle>(); return Camera.main.WorldToScreenPoint(b.VisibleBounds(h.Body).center); }
    static Vector2 MarkerPoint(Button b) { var m = b.transform.Find("ExplorationMarkerPaper") as RectTransform; return Screen(m && m.gameObject.activeInHierarchy ? m : (RectTransform)b.transform); }
    static async Task PressPawn(ExpeditionArrivalPanel a, FieldPawnBoard b, int m, PointerEventData.InputButton button = PointerEventData.InputButton.Left)
    {
        var at = PawnPoint(a, b, m); var top = Top(at); var h = b.HandleComponentOf(m);
        Check(h && top == h.gameObject, "Pawn " + m + ": the press lands on " + (top ? top.name : "nothing") + ", not its handle");
        ClickAt(at, button); await Task.Delay(150);
    }
    static Vector3 SpotFeet(ExpeditionArrivalPanel a, string key, int slot) { Check(Pres(a).TrySpot(a.Rooms.CurrentRoom, key, slot, out var feet, out float _), "No spot " + key + " " + slot); return feet; }
    static async Task Arrive(ExpeditionArrivalPanel a, FieldPawnBoard b, int m, Vector3 feet, string what)
    {
        // The walk starts on the next frame, so wait for the feet as well as the end of the walk.
        try { await Until(() => !b.IsWalking && ((Vector2)a.PartyPawns[m].transform.localPosition - (Vector2)feet).magnitude < .02f, 3000, "walk to " + what); } catch (Exception) { }
        var at = a.PartyPawns[m].transform.localPosition; Check(((Vector2)at - (Vector2)feet).magnitude < .02f, "Pawn " + m + " stands at " + what + ": " + at + " vs " + feet);
    }
    static bool FacesRight(ExpeditionArrivalPanel a, int m) { var f = a.PartyPawns[m].GetComponent<PawnFacing>(); return f.Body.flipX != f.ArtworkFacesRight; }
    static void FacesToward(ExpeditionArrivalPanel a, int m, float lookAtX, string what)
    {
        float dx = lookAtX - a.PartyPawns[m].transform.localPosition.x; if (Mathf.Abs(dx) <= .15f) return;
        Check(FacesRight(a, m) == dx > 0, "Pawn " + m + " faces " + (dx > 0 ? "right" : "left") + " toward " + what);
    }
    // A point of the room's floor where nothing but the board's catcher takes a press (no pawn, silhouette, pin or object).
    static Vector2 EmptyFloor(ExpeditionArrivalPanel a, FieldPawnBoard b)
    {
        Check(b.Catcher && b.Catcher.gameObject.activeInHierarchy, "The catcher must be up (a pawn held)");
        var p = Pres(a); int room = a.Rooms.CurrentRoom; var band = p.BandOf(room);
        for (float fy = .5f; fy >= .2f; fy -= .15f)
            for (float fx = .15f; fx <= .85f; fx += .05f)
            {
                var feet = new Vector3(Mathf.Lerp(band.xMin, band.xMax, fx), Mathf.Lerp(band.yMin, band.yMax, fy), 0);
                var screen = (Vector2)Camera.main.WorldToScreenPoint(p.PawnRoot.TransformPoint(feet));
                if (Top(screen) != b.Catcher.gameObject) continue;
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
                var below = hits.Select(h => h.gameObject).FirstOrDefault(g => g && !g.transform.IsChildOf(b.transform));
                if (below && (below.GetComponentInParent<ExplorationHotspot>() || below.GetComponentInParent<Selectable>())) continue;
                if (a.PartyPawns.Any(x => x && ((Vector2)x.transform.localPosition - (Vector2)feet).magnitude < 1.2f)) continue;
                return screen;
            }
        throw new Exception("No empty floor in the room");
    }
    static async Task DragPawn(ExpeditionArrivalPanel a, FieldPawnBoard b, int m, Func<Vector2> to, string still = null)
    {
        var h = b.HandleComponentOf(m); Check(h, "Handle of " + m); var start = PawnPoint(a, b, m); Check(Top(start) == h.gameObject, "Drag starts on the pawn's handle");
        var e = new PointerEventData(EventSystem.current) { position = start, pressPosition = start, button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(h.gameObject, e, ExecuteEvents.initializePotentialDrag);
        e.position = start + new Vector2(0, 40); e.delta = new Vector2(0, 40); e.dragging = true;
        ExecuteEvents.Execute(h.gameObject, e, ExecuteEvents.beginDragHandler);
        Check(b.Carrying && b.Held == m, "Picked up by dragging: carrying " + b.Carrying + ", held " + b.Held);
        await Task.Delay(150);
        var end = to(); var mid = Vector2.Lerp(start, end, .6f);
        e.delta = mid - e.position; e.position = mid; ExecuteEvents.Execute(h.gameObject, e, ExecuteEvents.dragHandler); await Task.Delay(120);
        if (still != null) await Still(a, still);
        e.delta = end - e.position; e.position = end; ExecuteEvents.Execute(h.gameObject, e, ExecuteEvents.dragHandler); await Task.Delay(60);
        ExecuteEvents.Execute(h.gameObject, e, ExecuteEvents.endDragHandler); e.dragging = false; await Task.Delay(200);
    }
    static bool Near(Color x, Color y) => Mathf.Abs(x.r - y.r) < .01f && Mathf.Abs(x.g - y.g) < .01f && Mathf.Abs(x.b - y.b) < .01f;
    static FieldPawnGlow Glow(ExpeditionArrivalPanel a, int m) => a.PartyPawns[m] ? a.PartyPawns[m].GetComponent<FieldPawnGlow>() : null;
    static FieldMemberActionSlots Slots(ExpeditionArrivalPanel a) => a.Main.GetComponentInChildren<FieldMemberActionSlots>(true);
    static string Keys(FieldPawnBoard b) => "[" + string.Join(", ", b.Shown.Select(o => o.Key + "#" + o.Spot.Slot + ":" + o.Kind + (o.Enabled ? "" : "(" + o.Blocked + ")"))) + "]";

    // ---- play mode ----
    // Input: handles over the pawns; an object with nothing held says '대원 말을 먼저 누르세요' (no plan change, no window); picking
    // up shows a silhouette + pin per enabled place (labels = FieldPlacement's) and a grey padlock pin per shut one; a silhouette
    // press places (no time) and the pawn walks there; the card picks up; a pin press joins as co-op, the chips appear under the
    // helper and change its role; right press takes a pawn off (it walks away from the work spot); a drag onto empty floor puts a
    // placed pawn down there (off its task); the ways to let go.
    public static async Task<string> Input()
    {
        var c = Owner(); var a = await Ready(c); var b = BoardOf(a); var place = b.Placement; var pl = a.Threat.Planner; var p = Pres(a); var log = new List<string>();
        int turns = a.Rooms.Turns, minute = c.Campaign.MinuteOfDay, site = Open(a, 0, 1); string key = FieldSpotRef.SearchKey(site);
        for (int m = 0; m < a.Participants.Count; m++) { var h = b.HandleComponentOf(m); Check(h && Top(PawnPoint(a, b, m)) == h.gameObject, "Handle over pawn " + m); }
        log.Add(a.Participants.Count + " handles over the pawns");

        ClickAt(MarkerPoint(a.Objects[site])); await Task.Delay(150);
        Check(!pl.Plan.HasAssignments && !a.Search.IsOpen && !a.Popup.activeSelf && a.Status.text == b.Texts.PickFirst && b.LastHint == b.Texts.PickFirst, "Nothing held: '" + a.Status.text + "'");
        Check(b.Flashing, "The free rings pulse"); log.Add("object first → '" + a.Status.text.Replace("\n", " / ") + "'");

        await PressPawn(a, b, 0); await Task.Delay(200);
        Check(b.Held == 0 && b.Catcher.gameObject.activeInHierarchy, "Pawn 0 held, the catcher up");
        var slots = Slots(a); Check(slots.SlotOf(0).Selected, "Its card slot is outlined");
        var glow = Glow(a, 0); Check(glow && glow.Shown && glow.Strong, "The held pawn wears the strong ring");
        var options = place.OptionsFor(0);
        foreach (var o in options)
        {
            var pin = b.PinOf(o.Key); Check(pin && pin.Visible, "A pin for " + o.Key + " · " + Keys(b));
            if (o.Enabled)
            {
                var ghost = b.GhostOf(o.Key, o.Spot.Slot); Check(ghost && ghost.gameObject.activeInHierarchy, "A silhouette for " + o.Key + " · " + Keys(b));
                var gh = ghost.GetComponent<FieldPawnHandle>(); var feet = p.PawnRoot.InverseTransformPoint(gh.Body.transform.parent.position);
                if (p.TrySpot(a.Rooms.CurrentRoom, o.Key, o.Spot.Slot, out var want, out float _)) Check(((Vector2)feet - (Vector2)want).magnitude < .02f, o.Key + ": the silhouette stands at its spot " + feet + " vs " + want);
                // Its body takes presses as a silhouette (another silhouette may stand in front of it).
                var over = Top(GhostPoint(b, ghost)); var oh = over ? over.GetComponent<FieldPawnHandle>() : null;
                Check(oh && oh.Role == FieldPawnHandle.Kind.Ghost, o.Key + ": the silhouette takes the press (" + (over ? over.name : "nothing") + ")");
                Check(pin.Enabled && pin.CaptionText == o.Label, o.Key + " pin caption '" + pin.CaptionText + "' = '" + o.Label + "'");
            }
            else Check(!pin.Enabled && pin.CaptionText == o.Blocked && b.GhostOf(o.Key, o.Spot.Slot) == null, o.Key + ": a grey padlock pin with '" + o.Blocked + "', no silhouette");
        }
        Check(a.Rooms.Turns == turns && !pl.Plan.HasAssignments, "Picking up costs nothing");
        await Still(a, "01-held-silhouettes"); log.Add("held → " + Keys(b));

        var g0 = b.GhostOf(key, 0); Check(g0, "Lead silhouette at " + a.ObjectNames[site]);
        var top = ClickAt(GhostPoint(b, g0)); await Task.Delay(150);
        Check(top == g0.gameObject, "The press lands on the silhouette: " + top.name);
        var k = place.CheckNow(); var run = k.RunFor(site);
        Check(run != null && run.Lead == 0 && run.Support < 0 && a.Rooms.Turns == turns && c.Campaign.MinuteOfDay == minute && b.Held == -1, "Member 0 leads " + a.ObjectNames[site] + " alone, no time, let go");
        Check(a.Status.text == place.LastStatus && a.Status.text.StartsWith(a.Participants[0].Name), "Status paper: " + a.Status.text);
        Check(a.Rooms.Inspected.Contains(site), "The object counts as looked at");
        await Arrive(a, b, 0, SpotFeet(a, key, 0), key + " slot 0"); FacesToward(a, 0, p.SpotOf(a.Rooms.CurrentRoom, key).LookAtX, a.ObjectNames[site]);
        await Still(a, "02-placed-walked"); log.Add("silhouette → lead, walked there");

        var card = a.Cards[1]; var cardPoint = Screen(card.Portrait ? card.Portrait.rectTransform : (RectTransform)card.transform);
        Check(Top(cardPoint) == slots.SlotOf(1).Input.gameObject, "Card 1: the press lands on its input layer");
        ClickAt(cardPoint); await Task.Delay(200); Check(b.Held == 1, "The card picks up pawn 1");
        var join = b.Shown.FirstOrDefault(o => o.Key == key && o.Kind == FieldSpotKind.Join); Check(join != null && join.Spot.Slot == 1, "Pawn 1 can join " + a.ObjectNames[site] + " · " + Keys(b));
        var joinPin = b.PinOf(key); ClickAt(Screen(joinPin.HitArea)); await Task.Delay(200);
        k = place.CheckNow(); run = k.RunFor(site);
        Check(run != null && run.Lead == 0 && run.Support == 1 && a.Rooms.Turns == turns && b.Held == -1, "The pin joins pawn 1 as the helper");
        await Arrive(a, b, 1, SpotFeet(a, key, 1), key + " slot 1");
        var row = b.ChipRowOf(1); Check(row && row.gameObject.activeInHierarchy, "Chips under the helper");
        var chips = place.ChipsFor(1); Check(chips.Count == 3 && chips.Count(x => x.On) == 1, "Three role chips, one on");
        for (int i = 0; i < chips.Count; i++) { var cb = b.ChipOf(1, i); Check(cb && cb.GetComponentInChildren<Text>().text == chips[i].Label, "Chip " + i + " '" + chips[i].Label + "'"); }
        var rowRect = ScreenRect(row); var catcherRect = ScreenRect(b.Catcher.rectTransform);
        Check(rowRect.yMin >= catcherRect.yMin - .5f, "The chips stay above the member tray (" + rowRect.yMin + " < " + catcherRect.yMin + ")");
        Check(slots.FreeCount == 0 && Near(pl.TurnButton.GetComponent<Image>().color, slots.ReadyTint), "Everyone placed: '턴 진행' green");
        await Still(a, "03-coop-chips"); log.Add("card → pin → co-op, chips " + string.Join("·", chips.Select(x => x.Label + (x.On ? "*" : "") + (x.Enabled ? "" : "(" + x.Why + ")"))));
        for (int i = 0; i < chips.Count; i++)
        {
            if (chips[i].On || !chips[i].Enabled) continue;
            ClickAt(Screen((RectTransform)b.ChipOf(1, i).transform)); await Task.Delay(200);
            Check(place.ChipsFor(1)[i].On && a.Rooms.Turns == turns, "Chip " + chips[i].Label + " takes the role, no time");
            log.Add("chip " + chips[i].Label); break;
        }

        ClickAt(PawnPoint(a, b, 1), PointerEventData.InputButton.Right); await Task.Delay(200);
        k = place.CheckNow(); run = k.RunFor(site);
        Check(run != null && run.Lead == 0 && run.Support < 0 && !place.TryGetSpot(1, out _), "Right press takes the helper off");
        await Until(() => !b.IsWalking, 2500, "helper walks away");
        Check(p.Apart(a.PartyPawns[1].transform.localPosition, SpotFeet(a, key, 1)) && p.Apart(a.PartyPawns[1].transform.localPosition, a.PartyPawns[0].transform.localPosition), "The free pawn stands clear of the work spot");
        log.Add("right press → off, walked clear");

        await PressPawn(a, b, 0); Check(b.Held == 0, "Placed pawn 0 held again");
        Vector2 drop = default; await DragPawn(a, b, 0, () => drop = EmptyFloor(a, b), "04-drag-to-floor");
        Check(!place.TryGetSpot(0, out _) && pl.Plan.Find(site) == null && b.Held == -1 && !b.Carrying && a.Rooms.Turns == turns, "Dropped on empty floor: off its task, let go");
        await Until(() => !b.IsWalking, 2500, "walk to the drop point");
        var dropFeet = p.PawnRoot.InverseTransformPoint(Camera.main.ScreenToWorldPoint(new Vector3(drop.x, drop.y, Mathf.Abs(Camera.main.transform.position.z))));
        Check(((Vector2)a.PartyPawns[0].transform.localPosition - (Vector2)dropFeet).magnitude < 1.3f, "It stands where it was put (or just beside it)");
        log.Add("drag to floor → off, stands there");

        // Letting go: the same pawn again, right press in the room, another pawn switches, Esc (when the Game view has focus).
        await PressPawn(a, b, 0); await PressPawn(a, b, 0); Check(b.Held == -1, "The same pawn again lets go");
        await PressPawn(a, b, 0); ClickAt(EmptyFloor(a, b), PointerEventData.InputButton.Right); await Task.Delay(150); Check(b.Held == -1 && !b.Catcher.gameObject.activeInHierarchy, "Right press lets go");
        await PressPawn(a, b, 0); await PressPawn(a, b, 1); Check(b.Held == 1, "Another pawn switches");
        var kb = Keyboard.current;
        if (kb != null && Application.isFocused)
        {
            try { InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Escape)); await Task.Delay(150); }
            finally { InputSystem.QueueStateEvent(kb, new KeyboardState()); await Task.Delay(150); }
            if (b.Held == 1 && !a.Popup.activeSelf) { log.Add("Esc not delivered (Game view focus) — skipped"); b.Cancel(); }
            else { Check(b.Held == -1 && !a.Popup.activeSelf && a.Main.interactable, "Esc lets go, the return question put away"); log.Add("Esc"); }
        }
        else { b.Cancel(); log.Add("Esc skipped (no focus)"); }
        Check(!pl.Plan.HasAssignments && a.Rooms.Turns == turns, "Nothing left placed, no time");
        return "PASS input · " + string.Join(" · ", log) + " · stills " + Shots;
    }

    // Walking: '턴 진행' never waits (TurnStarting snaps the walk); preview = result; the resident's clearance sees the pawns.
    public static async Task<string> Walking()
    {
        var c = Owner(); var a = await Ready(c); var b = BoardOf(a); var place = b.Placement; var pl = a.Threat.Planner; var p = Pres(a);
        int site = Open(a, 1, 0, 2), turns = a.Rooms.Turns; string key = FieldSpotRef.SearchKey(site);
        await PressPawn(a, b, 0); var g = b.GhostOf(key, 0); Check(g, "Lead silhouette " + key + " · " + Keys(b));
        ClickAt(GhostPoint(b, g));
        await Until(() => b.WalkerOf(0) && b.WalkerOf(0).Walking, 1000, "pawn 0 starts walking");
        pl.TurnButton.onClick.Invoke();
        var feet = SpotFeet(a, key, 0);
        Check(((Vector2)a.PartyPawns[0].transform.localPosition - (Vector2)feet).magnitude < .02f, "'턴 진행' snapped the walk: " + a.PartyPawns[0].transform.localPosition + " vs " + feet);
        Check(a.Rooms.Turns == turns + 1 && pl.LastMismatch == "", "One turn, preview = result: " + pl.LastMismatch);
        Check(p.ClearanceAt(a.Rooms.CurrentRoom, feet) < .01f, "ClearanceAt sees the pawn standing there");
        await Task.Delay(1400); await CloseLoot(a);
        return "PASS walking · snapped on '턴 진행', one turn, clearance " + p.ClearanceAt(a.Rooms.CurrentRoom, feet + new Vector3(3, 0, 0)).ToString("0.00") + " three units away";
    }

    // Facing: the lead of the crate faces it from the left (right), the helper from the right (left); their silhouettes too; the
    // handle still covers the turned body.
    public static async Task<string> Facing()
    {
        var c = Owner(); var a = await Ready(c); var b = BoardOf(a); var p = Pres(a); var log = new List<string>();
        int site = Open(a, 0, 1); string key = FieldSpotRef.SearchKey(site); var spot = p.SpotOf(A, key);
        await PressPawn(a, b, 0); var lead = b.GhostOf(key, 0); Check(lead, "Lead silhouette");
        var lh = lead.GetComponent<FieldPawnHandle>(); var ghostFacing = lh.Body.transform.parent.GetComponent<PawnFacing>();
        bool wantRight = spot.LookAtX > spot.Slots[0].x; Check((ghostFacing.Body.flipX != ghostFacing.ArtworkFacesRight) == wantRight, "The lead silhouette faces the object");
        ClickAt(GhostPoint(b, lead)); await Arrive(a, b, 0, SpotFeet(a, key, 0), "lead spot"); FacesToward(a, 0, spot.LookAtX, "the object (lead)");
        await PressPawn(a, b, 1); var help = b.GhostOf(key, 1); Check(help, "Helper silhouette · " + Keys(b));
        var hh = help.GetComponent<FieldPawnHandle>(); var hf = hh.Body.transform.parent.GetComponent<PawnFacing>();
        bool helperRight = spot.LookAtX > spot.Slots[1].x; Check((hf.Body.flipX != hf.ArtworkFacesRight) == helperRight, "The helper silhouette faces the object");
        ClickAt(GhostPoint(b, help)); await Arrive(a, b, 1, SpotFeet(a, key, 1), "helper spot"); FacesToward(a, 1, spot.LookAtX, "the object (helper)");
        Check(FacesRight(a, 0) != FacesRight(a, 1) || wantRight == helperRight, "Lead and helper face the object from both sides");
        for (int m = 0; m < 2; m++) Check(Top(PawnPoint(a, b, m)) == b.HandleComponentOf(m).gameObject, "The handle covers turned pawn " + m);
        log.Add("lead " + (FacesRight(a, 0) ? "→" : "←") + " · helper " + (FacesRight(a, 1) ? "→" : "←"));
        await Still(a, "05-facing"); a.Threat.Planner.Plan.Clear(); a.Threat.Refresh(); await Task.Delay(300);
        return "PASS facing · " + string.Join(" · ", log);
    }

    // Doors: the first pawn at the corridor door listens (awake board) and the next member is picked up at once (추가 결정); a press
    // on the door itself gathers them → the move is reserved ('턴 진행' green); a right press on a gatherer cancels it; the door's
    // log: after a turn the heard line shows on the door's hover and in its right-press popup.
    public static async Task<string> Doors()
    {
        var c = Owner(); var a = await Ready(c); var b = BoardOf(a); var place = b.Placement; var pl = a.Threat.Planner; var log = new List<string>();
        string key = FieldSpotRef.DoorKey(C); var door = FieldPlacement.DoorButton(a, A, C); Check(door, "Arcade door");
        await PressPawn(a, b, 0); var g = b.GhostOf(key, 0); Check(g, "Door silhouette · " + Keys(b));
        var first = b.Shown.First(o => o.Key == key); Check(first.Kind == FieldSpotKind.Listen && first.Label == place.Texts.Listen, "An empty door offers '귀 대기' (" + first.Kind + " '" + first.Label + "')");
        ClickAt(GhostPoint(b, g)); await Task.Delay(200);
        Check(pl.Plan.ListenAt(C) != null && pl.Plan.ListenAt(C).Member == 0, "Pawn 0 listens at the corridor door");
        Check(b.Held == 1, "The next member is picked up (" + b.Held + ")"); log.Add("door → listen, next picked up");
        var gather = b.Shown.FirstOrDefault(o => o.Key == key); Check(gather != null && gather.Kind == FieldSpotKind.Gather, "Pawn 1 gathers there · " + Keys(b));
        ClickAt(MarkerPoint(door)); await Task.Delay(250);
        Check(a.Rooms.HasQueuedMove && a.Rooms.QueuedRoom == C && b.Held == -1, "Everyone at the door: the move is reserved");
        var slots = Slots(a); Check(Near(pl.TurnButton.GetComponent<Image>().color, slots.ReadyTint), "'턴 진행' green");
        await Until(() => !b.IsWalking, 2500, "queue at the door");
        var p = Pres(a); for (int m = 0; m < 2; m++) { Check(place.TryGetSpot(m, out var s) && s.Key == key, "Pawn " + m + " at the door"); Check(((Vector2)a.PartyPawns[m].transform.localPosition - (Vector2)SpotFeet(a, key, s.Slot)).magnitude < .02f, "Pawn " + m + " in door slot " + s.Slot); }
        await Still(a, "06-gathered-door"); log.Add("door itself → gathered, move reserved");
        ClickAt(PawnPoint(a, b, 1), PointerEventData.InputButton.Right); await Task.Delay(200);
        Check(!a.Rooms.HasQueuedMove && !place.TryGetSpot(1, out _), "A right press on a gatherer cancels the move"); log.Add("right press → move cancelled");

        // A turn with the listener: the log gets its line.
        int turns = a.Rooms.Turns; FieldIdleConfirm.Pass(() => pl.TurnButton.onClick.Invoke()); await Task.Delay(400); await CloseLoot(a);
        Check(a.Rooms.Turns == turns + 1, "One turn");
        Check(pl.DoorLog != null, "The planner keeps a door log");
        Check(pl.DoorLog.TryLatest(A, C, out var latest), "The door log has what was heard");
        string line = pl.DoorLog.Line(latest, a.Threat.State.TurnsUsed);
        var hot = door.GetComponent<ExplorationHotspot>(); Hover(door.gameObject, true); await Task.Delay(200);
        Check(hot.IsExpanded && b.TagShown(line), "Hovering the door shows '" + line + "'"); Hover(door.gameObject, false);
        await Still(a, "07-door-hover-log");
        ClickAt(MarkerPoint(door), PointerEventData.InputButton.Right); await Task.Delay(200);
        Check(a.Popup.activeSelf && a.PopupTitle.text == string.Format(b.Texts.DoorLogTitle, FieldSiteState.RoomNames[C]) && a.PopupBody.text.Contains(line) && !a.ReturnConfirm.gameObject.activeSelf, "Right press: the door log popup ('" + a.PopupTitle.text + "')");
        await Still(a, "08-door-log-popup"); a.ClosePopup(); await Task.Delay(150);
        log.Add("log '" + line + "' on hover and in the popup");
        pl.Plan.Clear(); a.Threat.Refresh(); await Task.Delay(300);
        return "PASS doors · " + string.Join(" · ", log) + " · stills " + Shots;
    }

    // Right press: an object opens its 07 window (never the arcade door's move popup); the trace its note.
    public static async Task<string> RightClick()
    {
        var c = Owner(); var a = await Ready(c); var b = BoardOf(a); var log = new List<string>();
        int site = Open(a, 0, 1, 2);
        ClickAt(MarkerPoint(a.Objects[site]), PointerEventData.InputButton.Right); await Until(() => a.Search.IsOpen || a.Loot.IsOpen, 2000, "07 window");
        Check(!a.Search.IsOpen || a.Search.Title.text == a.ObjectNames[site], "The object's 07 window"); log.Add("object → 07");
        await Settle(a);
        var door = FieldPlacement.DoorButton(a, A, C);
        ClickAt(MarkerPoint(door), PointerEventData.InputButton.Right); await Task.Delay(200);
        Check(a.Popup.activeSelf && a.PopupTitle.text == string.Format(b.Texts.DoorLogTitle, FieldSiteState.RoomNames[C]) && !a.ReturnConfirm.gameObject.activeSelf, "The arcade door: its log, not the move popup ('" + a.PopupTitle.text + "')");
        a.ClosePopup(); await Task.Delay(150); log.Add("arcade door → log");
        var story = a.Story;
        if (story && story.Clue && story.Clue.gameObject.activeInHierarchy)
        {
            ClickAt(MarkerPoint(story.Clue), PointerEventData.InputButton.Right); await Task.Delay(200);
            Check(a.Popup.activeSelf && !story.IsOpen, "The trace: its note"); a.ClosePopup(); await Task.Delay(150); log.Add("trace → note");
        }
        else log.Add("trace not shown (story stage)");
        return "PASS right press · " + string.Join(" · ", log);
    }

    // Screen sizes: at 1920×1080, 1280×1024 and 2560×1080 the handles sit on the pawns and silhouettes, the pins on the screen, the
    // chips above the member tray; stills of a held pawn and of a co-op.
    public static async Task<string> Captures()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var assembly = typeof(Editor).Assembly; var st = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(st).GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        var group = st.GetMethod("GetGroup", flags).Invoke(sizes, new[] { st.GetProperty("currentGroupType", flags).GetValue(sizes) });
        var view = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView")); var selected = view.GetType().GetProperty("selectedSizeIndex", flags);
        int original = (int)selected.GetValue(view); var done = new List<string>();
        try
        {
            foreach (var wh in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 1024), new Vector2Int(2560, 1080) })
            {
                var sizeType = assembly.GetType("UnityEditor.GameViewSize"); var kind = assembly.GetType("UnityEditor.GameViewSizeType");
                int count = (int)group.GetType().GetMethod("GetTotalCount", flags).Invoke(group, null), index = -1;
                for (int i = 0; i < count; i++) { var size = group.GetType().GetMethod("GetGameViewSize", flags).Invoke(group, new object[] { i }); if ((int)sizeType.GetProperty("width", flags).GetValue(size) == wh.x && (int)sizeType.GetProperty("height", flags).GetValue(size) == wh.y) { index = i; break; } }
                if (index < 0) { var size = Activator.CreateInstance(sizeType, flags, null, new object[] { Enum.ToObject(kind, 1), wh.x, wh.y, "Review " + wh.x + "x" + wh.y }, null); group.GetType().GetMethod("AddCustomSize", flags).Invoke(group, new[] { size }); index = count; }
                selected.SetValue(view, index); view.Repaint(); await Task.Delay(500); Canvas.ForceUpdateCanvases();
                var c = Owner(); var a = await Ready(c); var b = BoardOf(a); int site = Open(a, 0, 1); string key = FieldSpotRef.SearchKey(site);
                for (int m = 0; m < 2; m++) Check(Top(PawnPoint(a, b, m)) == b.HandleComponentOf(m).gameObject, wh + ": handle over pawn " + m);
                await PressPawn(a, b, 0); await Task.Delay(250);
                foreach (var o in b.Shown.Where(o => o.Enabled))
                {
                    var ghost = b.GhostOf(o.Key, o.Spot.Slot); if (!ghost) continue;
                    var gh = ghost.GetComponent<FieldPawnHandle>(); var body = (Vector2)Camera.main.WorldToScreenPoint(b.VisibleBounds(gh.Body).center); var hr = ScreenRect(gh.Rect);
                    Check(hr.Contains(body), wh + ": the silhouette handle of " + o.Key + " covers its body");
                    var pin = b.PinOf(o.Key); var pr = ScreenRect(pin.HitArea); Check(pr.xMin >= -1 && pr.xMax <= wh.x + 1 && pr.yMin >= -1 && pr.yMax <= wh.y + 1, wh + ": the pin of " + o.Key + " is on the screen " + pr);
                }
                await Still(a, "10-held-" + wh.x + "x" + wh.y);
                var lead = b.GhostOf(key, 0); ClickAt(GhostPoint(b, lead)); await Task.Delay(150);
                await PressPawn(a, b, 1); var help = b.GhostOf(key, 1); Check(help, wh + ": helper silhouette"); ClickAt(GhostPoint(b, help)); await Until(() => !b.IsWalking, 2500, "co-op walk");
                await Task.Delay(200); var row = b.ChipRowOf(1); Check(row, wh + ": chips");
                Check(ScreenRect(row).yMin >= ScreenRect(b.Catcher.rectTransform).yMin - .5f, wh + ": the chips stay above the member tray");
                await Still(a, "11-coop-" + wh.x + "x" + wh.y);
                a.Threat.Planner.Plan.Clear(); a.Threat.Refresh(); await Task.Delay(300);
                done.Add(wh.x + "×" + wh.y + " PASS");
            }
        }
        finally { selected.SetValue(view, original); view.Repaint(); }
        return "PASS captures · " + string.Join(", ", done) + " · original Game View size restored · stills " + Shots;
    }

    // 사물 위 수색 진행 칸 (시안 02): none over an untouched object with nobody on it; a pawn on it → its turns with this turn's cell lit;
    // co-op (함께) → one turn sooner; hidden while a pawn is held; after '턴 진행' with nobody left there → the done cell stays.
    public static async Task<string> Progress()
    {
        FieldIdleConfirm.AutoAccept = true;
        var c = Owner(); var a = await Ready(c); var b = BoardOf(a); var log = new List<string>();
        int site = new[] { 0, 1 }.Where(i => !a.Loot.Peek(i, out var st) || st.Progress == 0 && !st.Complete).DefaultIfEmpty(-1).First();
        Check(site >= 0, "Fixture: no untouched object among the crate and the table (start from VerifyFieldTurnPlan.Enter)");
        Check(StripNear(a, b, site) == null, "No strip over an untouched object with nobody on it");
        Check(FieldPawnTest.Lead(a, 0, site), "Pawn on " + a.ObjectNames[site]); await Task.Delay(200);
        var p = Cells(a, b, site); int turns = a.Loot.SiteTurns(site);
        Check(p.Count == turns && p.Done == 0 && p.Next == 1, "Lead: " + Desc(p)); log.Add("lead " + Desc(p));
        await PressPawn(a, b, 1); await Task.Delay(150); Check(StripNear(a, b, site) == null, "Hidden while a pawn is held"); b.Cancel(); await Task.Delay(150);
        Check(FieldPawnTest.Join(a, 1, site), "Helper joins: " + FieldPawnTest.Describe(a)); await Task.Delay(200);
        p = Cells(a, b, site); Check(p.Count == FieldTurnPlan.RequiredOf(turns, true) && p.Next == 1, "Co-op: " + Desc(p)); log.Add("co-op " + Desc(p));
        await Still(a, "12-progress-coop");
        Check(FieldPawnTest.Unassign(a, 1), "Helper off"); await Task.Delay(100);
        var pl = a.Threat.Planner; int before = a.Rooms.Turns; pl.TurnButton.onClick.Invoke();
        await Until(() => a.Rooms.Turns == before + 1 && !pl.Resolving, 4000, "one turn"); await Task.Delay(300); await CloseLoot(a);
        FieldPawnTest.Clear(a); a.Threat.Refresh(); await Task.Delay(300);
        p = Cells(a, b, site); Check(p.Count == turns && p.Done == 1 && p.Next == 0, "Half searched, nobody on it: " + Desc(p)); log.Add("half " + Desc(p));
        await Still(a, "13-progress-half");
        return "PASS progress · " + string.Join(" · ", log) + " · stills " + Shots;
    }
    static RectTransform StripNear(ExpeditionArrivalPanel a, FieldPawnBoard b, int site)
    {
        var node = b.transform.Find("Progress"); Check(node, "Run BuildPawnBoard.Run first (Progress)");
        var mark = a.Objects[site].transform.Find("ExplorationMarkerPaper") as RectTransform; float x = Screen(mark && mark.gameObject.activeInHierarchy ? mark : (RectTransform)a.Objects[site].transform).x;
        foreach (Transform t in node) if (t.gameObject.activeInHierarchy && Mathf.Abs(Screen((RectTransform)t).x - x) < 30) return (RectTransform)t;
        return null;
    }
    static FieldSearchPips Cells(ExpeditionArrivalPanel a, FieldPawnBoard b, int site)
    {
        var s = StripNear(a, b, site); Check(s, "A strip over " + a.ObjectNames[site]); var p = s.GetComponentInChildren<FieldSearchPips>(true); Check(p, "Cells in the strip"); return p;
    }
    static string Desc(FieldSearchPips p) => p.Done + "+" + p.Next + "/" + p.Count;

    public static async Task<string> All()
    {
        var r = new List<string> { Wiring(), Spots(), await Input(), await Walking(), await Facing(), await Doors(), await RightClick(), await Captures(), await Progress() };
        return string.Join("\n", r);
    }
}
