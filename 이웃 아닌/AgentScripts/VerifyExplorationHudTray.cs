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

// 탐험 화면 정리 · 배치 (2026-09-25): the exploration HUD against the approved mock 기획/탐험-화면정리-시안/01-정리안.png (two top papers,
// a tray with '대원' · '이번 턴' · '행동'). Wiring(): edit or play mode, after BuildExplorationHudTray.Run + BuildTurnClockPaper.Run.
// Play mode after VerifyFieldTurnPlan.Enter (a fresh game), in this order: FirstVisit → Board → Six (or All; Six recruits four
// residents into the save, so run it last). Rects are Main space (1920×1080, y down), tolerance 1.5 px; the mock's own rects are
// in the comments (the tray starts 36 px higher: the cards are 230 tall with the member action slot row). Stills in
// Temp/ExplorationHudTrayCapture.
// 말 놓기 (2026-09-25, 기획/탐험-말놓기-조작-재설계.md): both visits show '턴 진행', its chip and '계속 진행'; '모두 숨죽이기' is gone (its node stays
// hidden); the tutorial banner sits at the top centre (off the cards, the pawns and '이번 턴'); a placed pawn replaces the assignment
// bubble; the room area is FieldRoomArea on Main (BuildRetireOldAssign). Run after the whole builder chain (… → BuildRetireOldAssign).
public static class VerifyExplorationHudTray
{
    const string ArrivalPath = "Assets/Prefabs/Settlement/ExpeditionArrivalPanel.prefab", ScreenPath = "Assets/Prefabs/Settlement/SettlementScreen.prefab", EncounterPath = "Assets/Prefabs/Settlement/ExpeditionEncounterPanel.prefab";
    // Same numbers as BuildExplorationHudTray / BuildTurnClockPaper (each agent script compiles alone).
    const float TrayTop = 776, CardW = 272, CardH = 230;
    static readonly Rect PlacePaper = new Rect(42, 24, 422, 124), DayPaper = new Rect(1485, 24, 400, 124);          // mock (41, 23, 424, 125) · (1485, 23, 400, 126)
    static readonly Rect Tray = new Rect(0, TrayTop, 1920, 1080 - TrayTop);
    static readonly Rect MembersPanel = new Rect(20, 790, 880, 278), TurnPanel = new Rect(920, 790, 410, 278), ActionPanel = new Rect(1350, 790, 550, 278); // mock x 20/920/1350, w 881/411/551
    static readonly Rect Members = new Rect(28, 812, 864, 250), FirstCard = new Rect(34, 830, CardW, CardH);            // mock card (43, 872, 264, 177)
    static readonly Rect Resources = new Rect(1100, 796, 214, 28), Risk = new Rect(944, 840, 372, 36), Hint = new Rect(944, 890, 372, 68), Status = new Rect(946, 974, 376, 62);
    static readonly Rect Turn = new Rect(1370, 834, 300, 130), Auto = new Rect(1688, 834, 196, 60), Hush = new Rect(1688, 904, 196, 60), Return = new Rect(1370, 984, 514, 64); // mock (1373,875,295,109) (1688,874,191,49) (1688,936,191,49) (1371,1000,509,49)
    static readonly Vector2 GuidePosition = new Vector2(720, -24), GuideSize = new Vector2(480, 148); // top centre (BuildRetireOldAssign / BuildExplorationHudTray)
    static readonly string[] Hidden = { "ArrivalPaper", "ArrivalTitle", "ResourcePaper", "RiskPaper", "RoutePaper", "TurnPaper" };
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    // ---- edit or play: the prefabs ----
    public static string Wiring()
    {
        var done = new List<string>();
        foreach (var path in new[] { ArrivalPath, ScreenPath })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path); Check(root, "Missing " + path);
            foreach (var a in root.GetComponentsInChildren<ExpeditionArrivalPanel>(true))
            {
                var main = a.Main.transform; string at = Path.GetFileNameWithoutExtension(path) + ": ";
                var tray = main.Find("HudTray") as RectTransform; Check(tray && tray.GetSiblingIndex() == 0, at + "Main/HudTray missing or not under everything: run BuildExplorationHudTray.Run");
                var hud = tray.GetComponent<FieldHudTray>(); Check(hud && hud.Arrival == a && hud.Tray == tray.GetComponent<CanvasGroup>() && hud.PlaceTitle && hud.DangerRow && hud.DangerDots && hud.Risk && hud.Status == a.Status && hud.StatusWarn, at + "FieldHudTray wiring");
                Check(hud.Warning, at + "FieldHudTray.Warning: run BuildTurnClockPaper.Run");
                Near(Local(main, tray), Tray, at + "tray");
                foreach (var (name, r) in new[] { ("MembersPanel", MembersPanel), ("TurnPanel", TurnPanel), ("ActionPanel", ActionPanel) }) Near(Local(main, (RectTransform)tray.Find(name)), r, at + name);
                foreach (var tab in new[] { ("MembersTab", "대원"), ("TurnTab", "이번 턴"), ("ActionTab", "행동") }) Check(tray.Find(tab.Item1 + "/Label").GetComponent<Text>().text == tab.Item2, at + "tab " + tab.Item2);
                Near(Local(main, (RectTransform)main.Find("PlacePaper")), PlacePaper, at + "place paper"); Near(Local(main, (RectTransform)main.Find("DayPaper")), DayPaper, at + "time paper");
                Near(Local(main, (RectTransform)main.Find("Members")), Members, at + "members"); Near(Local(main, a.Resources.rectTransform), Resources, at + "resources");
                Near(Local(main, (RectTransform)main.Find("Risk")), Risk, at + "risk"); Near(Local(main, (RectTransform)main.Find("Hint")), Hint, at + "hint"); Near(Local(main, a.Status.rectTransform), Status, at + "status");
                Near(Local(main, (RectTransform)main.Find("Hint/PlanChip")), Hint, at + "plan chip");
                Near(Local(main, (RectTransform)main.Find("TurnAdvance")), Turn, at + "turn"); Near(Local(main, (RectTransform)main.Find("Hush")), Hush, at + "hush");
                Near(Local(main, (RectTransform)main.Find("TurnFlow/AutoAdvance")), Auto, at + "keep going"); Near(Local(main, (RectTransform)a.Return.transform), Return, at + "return");
                foreach (var n in Hidden) Check(!main.Find(n).gameObject.activeSelf, at + n + " should be hidden");
                Check(!a.Place.enabled && main.Find("PlaceTitle") && !main.Find("Hush/Subtitle").gameObject.activeSelf, at + "place title mirror / hush title only");
                Check(!main.Find("Hush").gameObject.activeSelf && !a.Threat.Hush, at + "'모두 숨죽이기' is gone (BuildPawnRules): nobody placed is the hush turn");
                var layout = a.MemberContent.GetComponent<HorizontalLayoutGroup>(); Check(layout.childControlHeight && layout.childForceExpandHeight && !layout.childControlWidth && Mathf.Approximately(Members.height - layout.padding.top - layout.padding.bottom, CardH), at + "cards " + CardW + "×" + CardH);
                Check(Mathf.Approximately(((RectTransform)a.MemberPrefab.transform).sizeDelta.x, CardW), at + "card width " + ((RectTransform)a.MemberPrefab.transform).sizeDelta.x);
                // The room area (FieldRoomArea on Main since 말 놓기; the retired bubbles' values while it is not there).
                var area = a.Main.GetComponent<FieldRoomArea>(); var mk = main.GetComponentInChildren<FieldPlanTargetMarkers>(true); Check(area || mk, at + "room area (run BuildRetireOldAssign.Run)");
                var keep = area ? area.KeepClear : mk.KeepClear; var room = area ? area.RoomArea : mk.RoomArea;
                Check(keep != null && keep.Contains(tray) && keep.Contains((RectTransform)main.Find("PlacePaper")) && keep.Contains((RectTransform)main.Find("DayPaper")), at + "keep-clear misses the tray or a paper");
                Check(room.yMax <= TrayTop, at + "room area reaches the tray: " + room);
                // Every panel's content stays in its panel; panels, papers and tabs never overlap.
                var inTurn = new[] { a.Resources.rectTransform, (RectTransform)main.Find("Risk"), (RectTransform)main.Find("Hint"), a.Status.rectTransform, (RectTransform)tray.Find("DangerRow") };
                foreach (var r in inTurn) Check(Inside(Local(main, r), TurnPanel), at + r.name + " leaves '이번 턴'");
                foreach (var r in new[] { "TurnAdvance", "Hush", "TurnFlow/AutoAdvance" }) Check(Inside(Local(main, (RectTransform)main.Find(r)), ActionPanel), at + r + " leaves '행동'");
                Check(Inside(Local(main, (RectTransform)a.Return.transform), ActionPanel) && Inside(Local(main, (RectTransform)main.Find("Members")), MembersPanel), at + "return / members leave their panel");
                var blocks = new List<(string, Rect)> { ("PlacePaper", PlacePaper), ("DayPaper", DayPaper), ("MembersPanel", MembersPanel), ("TurnPanel", TurnPanel), ("ActionPanel", ActionPanel) };
                for (int i = 0; i < blocks.Count; i++) for (int j = i + 1; j < blocks.Count; j++) Check(!blocks[i].Item2.Overlaps(blocks[j].Item2), at + blocks[i].Item1 + " overlaps " + blocks[j].Item1);
                done.Add(at.TrimEnd(' ', ':'));
            }
            if (path == ScreenPath)
            {
                var g = root.GetComponentInChildren<SettlementTutorialGuide>(true); Check(g && g.FieldPosition == GuidePosition && g.FieldSize == GuideSize, "Tutorial field banner position " + (g ? g.FieldPosition + " " + g.FieldSize : "none"));
                var banner = new Rect(g.FieldPosition.x, -g.FieldPosition.y, g.FieldSize.x, g.FieldSize.y);
                Check(banner.yMax <= TrayTop && !banner.Overlaps(PlacePaper) && !banner.Overlaps(DayPaper) && Mathf.Abs(banner.center.x - 960) < 1, "Tutorial banner at the top centre, clear of the tray (cards, '이번 턴', '행동') and the papers: " + banner);
            }
        }
        var enc = AssetDatabase.LoadAssetAtPath<GameObject>(EncounterPath).GetComponent<ExpeditionEncounterPanel>();
        Check(((RectTransform)enc.Members.parent).rect.height >= CardH, "The encounter's member row clips the " + CardH + " px cards");
        return "PASS wiring: " + string.Join(", ", done) + "; panels, papers, tabs and buttons at the mock layout; hidden duplicates; tutorial banner at the top centre; encounter row fits the cards";
    }
    // A prefab node's rect in Main space from its anchors (no layout pass on assets): every node on the way is top-left anchored,
    // or stretched over its parent with zero offsets (TurnFlow).
    static Rect Local(Transform main, RectTransform r)
    {
        Check(r, "Missing node under " + main.name); float x = 0, y = 0;
        for (var t = r; t && t != main; t = t.parent as RectTransform)
        {
            if (t.anchorMin == new Vector2(0, 1) && t.anchorMax == new Vector2(0, 1)) { x += t.anchoredPosition.x - t.pivot.x * t.sizeDelta.x; y += -t.anchoredPosition.y - (1 - t.pivot.y) * t.sizeDelta.y; }
            else Check(t.anchorMin == Vector2.zero && t.anchorMax == Vector2.one && t.offsetMin == Vector2.zero && t.offsetMax == Vector2.zero, "Unexpected anchors on " + t.name);
        }
        return new Rect(x, y, r.sizeDelta.x, r.sizeDelta.y);
    }
    static void Near(Rect r, Rect want, string what) => Check(Mathf.Abs(r.x - want.x) < 1.5f && Mathf.Abs(r.y - want.y) < 1.5f && Mathf.Abs(r.width - want.width) < 1.5f && Mathf.Abs(r.height - want.height) < 1.5f, what + " rect " + r + " ≠ " + want);
    static bool Inside(Rect r, Rect box) => r.xMin >= box.xMin - .5f && r.xMax <= box.xMax + .5f && r.yMin >= box.yMin - .5f && r.yMax <= box.yMax + .5f;

    // ---- play-mode helpers (as VerifyFieldTurnFlow) ----
    static async Task Tap(Button button)
    {
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
    static async Task Until(Func<bool> done, int ms, string what) { var w = Stopwatch.StartNew(); while (!done()) { if (w.ElapsedMilliseconds > ms) throw new Exception("Timeout: " + what); await Task.Delay(30); } }
    static string Shots => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "ExplorationHudTrayCapture"));
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
    // A live rect in Main space (1920×1080, y down).
    static Rect OnMain(ExpeditionArrivalPanel a, RectTransform r)
    {
        var main = (RectTransform)a.Main.transform; var c = new Vector3[4]; r.GetWorldCorners(c);
        Vector3 lo = main.InverseTransformPoint(c[0]), hi = main.InverseTransformPoint(c[2]);
        return new Rect(lo.x - main.rect.xMin, main.rect.yMax - hi.y, hi.x - lo.x, hi.y - lo.y);
    }
    // A world renderer's bounds in Main space (the room camera → screen → the canvas).
    static Rect WorldOnMain(ExpeditionArrivalPanel a, Bounds b)
    {
        var main = (RectTransform)a.Main.transform; var cam = main.GetComponentInParent<Canvas>().worldCamera; var world = Camera.main ? Camera.main : cam;
        Vector2 P(Vector3 w) { RectTransformUtility.ScreenPointToLocalPointInRectangle(main, RectTransformUtility.WorldToScreenPoint(world, w), cam, out var l); return new Vector2(l.x - main.rect.xMin, main.rect.yMax - l.y); }
        Vector2 p0 = P(b.min), p1 = P(b.max); return Rect.MinMaxRect(Mathf.Min(p0.x, p1.x), Mathf.Min(p0.y, p1.y), Mathf.Max(p0.x, p1.x), Mathf.Max(p0.y, p1.y));
    }
    static void Fits(IEnumerable<Text> texts)
    {
        Canvas.ForceUpdateCanvases();
        foreach (var t in texts) if (t && t.isActiveAndEnabled && !string.IsNullOrEmpty(t.text) && !t.resizeTextForBestFit)
                Check(t.preferredHeight <= t.rectTransform.rect.height + 1 && (t.horizontalOverflow == HorizontalWrapMode.Wrap || t.preferredWidth <= t.rectTransform.rect.width + 1), "Overflow " + t.name + ": " + t.text + " (" + t.preferredHeight + " > " + t.rectTransform.rect.height + ")");
    }
    static IEnumerable<Text> HudTexts(ExpeditionArrivalPanel a)
    {
        var main = a.Main.transform;
        foreach (var n in new[] { "PlaceTitle", "RouteLabel", "DayTime", "TurnCount", "Resources", "Risk", "Hint", "Hint/PlanChip", "Status", "HudTray/DangerRow/Label" }) { var t = main.Find(n); if (t) yield return t.GetComponent<Text>(); }
        foreach (var b in new[] { "TurnAdvance", "Hush", "TurnFlow/AutoAdvance", "Return", "HudTray/MembersTab", "HudTray/TurnTab", "HudTray/ActionTab" }) { var t = main.Find(b); if (t) foreach (var x in t.GetComponentsInChildren<Text>()) yield return x; }
    }
    // Visible HUD pieces (never overlapping one another), in Main space.
    static List<(string name, Rect rect)> Pieces(ExpeditionArrivalPanel a)
    {
        var main = a.Main.transform; var list = new List<(string, Rect)>();
        void Add(string name, Transform t) { if (t && t.gameObject.activeInHierarchy) { var g = t.GetComponent<Graphic>(); if (!g || g.enabled) list.Add((name, OnMain(a, (RectTransform)t))); } }
        foreach (var n in new[] { "PlacePaper", "DayPaper", "Resources", "Risk", "HudTray/DangerRow", "Status", "TurnAdvance", "Hush", "TurnFlow/AutoAdvance", "Return", "HudTray/MembersTab", "HudTray/TurnTab", "HudTray/ActionTab" }) Add(n, main.Find(n));
        var hint = main.Find("Hint"); var chip = main.Find("Hint/PlanChip"); if (chip && chip.gameObject.activeInHierarchy) Add("PlanChip", chip); else if (hint && hint.GetComponent<Text>().enabled) Add("Hint", hint);
        var viewport = OnMain(a, (RectTransform)main.Find("Members"));
        foreach (var c in a.Cards) if (c && c.gameObject.activeInHierarchy) { var r = OnMain(a, (RectTransform)c.transform); if (r.Overlaps(viewport) && r.xMin >= viewport.xMin - .5f && r.xMax <= viewport.xMax + .5f) list.Add(("card " + c.Name.text, r)); }
        return list;
    }
    static void NoOverlap(ExpeditionArrivalPanel a, string when)
    {
        Canvas.ForceUpdateCanvases(); var p = Pieces(a);
        for (int i = 0; i < p.Count; i++) for (int j = i + 1; j < p.Count; j++) Check(!p[i].rect.Overlaps(p[j].rect), when + ": " + p[i].name + " " + p[i].rect + " overlaps " + p[j].name + " " + p[j].rect);
        var panels = new[] { MembersPanel, TurnPanel, ActionPanel };
        foreach (var x in p.Where(x => x.rect.yMin >= TrayTop - 12)) Check(panels.Any(b => Inside(x.rect, new Rect(b.x, b.y - 10, b.width, b.height + 10))), when + ": " + x.name + " " + x.rect + " is outside the three panels");
    }
    // The room stays above the tray: the party's standees (base and body), the searchable objects and doors.
    static void RoomAbove(ExpeditionArrivalPanel a, string when)
    {
        Canvas.ForceUpdateCanvases();
        foreach (var pawn in a.PartyPawns) if (pawn && pawn.activeInHierarchy) foreach (var r in pawn.GetComponentsInChildren<SpriteRenderer>().Where(x => x.name == "Base" || x.name == "Body")) { var b = WorldOnMain(a, r.bounds); Check(b.yMax <= TrayTop, when + ": standee " + pawn.name + "/" + r.name + " reaches the tray: " + b); }
        foreach (var h in a.Main.GetComponentsInChildren<ExplorationHotspot>()) { var r = OnMain(a, (RectTransform)h.transform); Check(r.yMax <= TrayTop, when + ": hotspot " + h.name + " reaches the tray: " + r); }
    }
    static void Layout(ExpeditionArrivalPanel a, string when)
    {
        var main = a.Main.transform;
        Near(OnMain(a, (RectTransform)main.Find("PlacePaper")), PlacePaper, when + " place paper"); Near(OnMain(a, (RectTransform)main.Find("DayPaper")), DayPaper, when + " time paper");
        Near(OnMain(a, (RectTransform)main.Find("HudTray")), Tray, when + " tray"); Near(OnMain(a, (RectTransform)a.Return.transform), Return, when + " return");
        foreach (var n in Hidden) Check(!main.Find(n).gameObject.activeSelf, when + ": " + n + " should be hidden");
        Check(main.Find("HudTray").GetComponent<CanvasGroup>().alpha == 1 && a.Main.alpha == 1, when + ": tray visible");
        var cards = a.Cards.Where(c => c).ToList(); Check(cards.Count == a.Participants.Count, when + ": one card per member");
        for (int i = 0; i < cards.Count; i++) Near(OnMain(a, (RectTransform)cards[i].transform), new Rect(FirstCard.x + i * (CardW + 14) - ScrollX(a), FirstCard.y, CardW, CardH), when + " card " + i);
        Check(main.Find("PlaceTitle").GetComponent<Text>().text == main.Find("HudTray").GetComponent<FieldHudTray>().Title(a.Place.text) && !main.Find("PlaceTitle").GetComponent<Text>().text.Contains("\n"), when + ": one-line place title");
        Fits(HudTexts(a)); NoOverlap(a, when); RoomAbove(a, when);
        foreach (var target in new Selectable[] { a.Return }.Concat(cards.Where(c => { var r = OnMain(a, (RectTransform)c.transform); return r.xMin >= Members.xMin - .5f && r.xMax <= Members.xMax + .5f; }).Select(c => (Selectable)c.Button))) if (target.IsActive() && target.IsInteractable()) Hit(target);
    }
    static float ScrollX(ExpeditionArrivalPanel a) => -a.MemberContent.anchoredPosition.x;
    static async Task CloseLoot(ExpeditionArrivalPanel a) { if (a.Loot.IsOpen) { await Tap(a.Loot.Back); await Task.Delay(120); if (a.Loot.LeaveReview.activeSelf) await Tap(a.Loot.LeaveConfirm); await Task.Delay(150); } }
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
    // A later visit in the arcade (home once after the sleeping first visit, the place-board intro closed), opening chapter off.
    static async Task<ExpeditionArrivalPanel> Later(SettlementController c)
    {
        var a = c.ArrivalPanel; if (c.Opening) c.Opening.State.Enabled = false;
        if (a.IsOpen) { await CloseLoot(a); for (int i = 0; i < 3 && a.Search.IsOpen; i++) { a.Search.Escape(); await Task.Delay(120); } if (a.Popup.activeSelf) a.ClosePopup(); await Until(() => !a.InTransit, 6000, "walk"); }
        if (!(a.IsOpen && a.Threat.Active && a.Threat.IntroAcknowledged && a.Rooms.CurrentRoom == FieldSiteState.Arcade))
        {
            if (a.IsOpen) { Check(a.FinishReturn(), "Return home"); await Task.Delay(150); }
            await Depart(c);
            if (!a.Threat.Active) { if (a.Popup.activeSelf) a.ClosePopup(); Check(a.FinishReturn(), "Return from the first visit"); await Task.Delay(150); await Depart(c); }
            await Until(() => a.Popup.activeSelf || a.Threat.IntroAcknowledged, 3000, "place-board intro");
            if (a.Popup.activeSelf) await Tap(a.PopupBack);
        }
        if (a.Rooms.HasQueuedMove) a.Rooms.CancelQueuedMove();
        a.Threat.ReviewWake(1, 0, 0); await Task.Delay(300); if (a.Popup.activeSelf) await Tap(a.PopupBack);
        Check(a.Threat.Planner.Active && a.Rooms.CurrentRoom == FieldSiteState.Arcade && !a.InTransit, "Later-visit board in the arcade");
        return a;
    }

    // ---- play mode ----
    // The board's own pieces that show now (role chips, tags, pins) stay in the room: off the tray and the papers.
    static void BoardPieces(ExpeditionArrivalPanel a, string when)
    {
        var b = FieldPawnTest.Board(a); if (!b) return; Canvas.ForceUpdateCanvases();
        var tray = new Rect(0, TrayTop, 1920, 1080 - TrayTop); var list = new List<(string, Rect)>();
        foreach (var r in b.ChipRows.Where(x => x && x.gameObject.activeInHierarchy)) list.Add(("chips", OnMain(a, r)));
        foreach (var r in b.Tags.Where(x => x && x.gameObject.activeInHierarchy)) list.Add(("tag", OnMain(a, r)));
        foreach (var p in b.Pins.Where(x => x && x.Visible)) list.Add(("pin", OnMain(a, p.HitArea)));
        foreach (var (name, r) in list) Check(!r.Overlaps(tray) && !r.Overlaps(PlacePaper) && !r.Overlaps(DayPaper), when + ": a " + name + " " + r + " covers the tray or a paper");
    }
    // First visit (the tutorial chapter of a fresh game): the same layout and the same controls as a later visit (말 놓기: '턴 진행', its
    // chip, '계속 진행'); only the danger dots and the pips wait for the awake board. The tutorial banner at the top centre points at a
    // visible target; the 07 window (read only) hides the HUD and gives it back unchanged.
    public static async Task<string> FirstVisit()
    {
        var c = Owner(); var a = await Arrived(c); var main = a.Main.transform; var t = a.Threat; var pl = t.Planner; var log = new List<string>();
        Check(!pl.Active && pl.Placing && t.State != null && t.State.Asleep, "Needs the first visit (a fresh game: VerifyFieldTurnPlan.Enter)");
        await Task.Delay(300); Layout(a, "first visit");
        var hud = main.Find("HudTray").GetComponent<FieldHudTray>();
        Check(!hud.DangerRow.activeSelf && hud.Risk.gameObject.activeSelf && !main.Find("TurnPips").gameObject.activeSelf, "Danger dots and pips wait for the awake board; the risk line shows");
        Check(pl.TurnButton.gameObject.activeSelf && main.Find("TurnFlow/AutoAdvance").gameObject.activeSelf && !main.Find("Hush").gameObject.activeSelf, "'턴 진행' and '계속 진행' on the first visit, no hush button");
        Check(!pl.Hint.enabled && pl.Chip.gameObject.activeSelf, "The plan chip in the forecast slot");
        Near(OnMain(a, (RectTransform)pl.TurnButton.transform), Turn, "turn"); Near(OnMain(a, pl.Chip.rectTransform), Hint, "chip");
        var guide = Object.FindAnyObjectByType<SettlementTutorialGuide>();
        if (guide && guide.Banner && guide.Banner.activeInHierarchy)
        {
            var b = OnMain(a, (RectTransform)guide.Banner.transform);
            Check(b.yMax <= TrayTop && !b.Overlaps(PlacePaper) && !b.Overlaps(DayPaper) && a.Cards.All(x => !b.Overlaps(OnMain(a, (RectTransform)x.transform))), "Tutorial banner at the top, off the tray and the cards: " + b);
            foreach (var pawn in a.PartyPawns) if (pawn && pawn.activeInHierarchy) { var body = pawn.transform.Find("Body"); if (body) Check(!b.Overlaps(WorldOnMain(a, body.GetComponent<SpriteRenderer>().bounds)), "The banner covers a pawn: " + b); }
            Check(guide.Target && guide.Target.IsActive(), "The tutorial points at a visible target"); Hit(guide.Target);
            log.Add("tutorial banner " + b + " → " + guide.Target.name);
        }
        else log.Add("no tutorial banner (opening chapter off)");
        await Still(a, "01-first-visit");
        // The 07 window (read only) covers everything; closing it gives the same HUD back.
        a.Inspect(0); await Until(() => a.Search.IsOpen, 2000, "search window"); await Task.Delay(150);
        Check(a.Main.alpha == 0 && a.Search.ReadOnly, "The read-only 07 window hides the room HUD"); await Still(a, "02-first-visit-search-window");
        await Tap(a.Search.Back); await Task.Delay(250); Layout(a, "after the search window");
        log.Add("07 window hides and restores the HUD");
        return "PASS first visit · layout at the mock, the same controls as the board, texts fit, nothing overlaps, standees and objects above the tray · " + string.Join(" · ", log) + " · stills " + Shots;
    }

    // Site board: the dots, the forecast chip and the buttons; a placed pawn stands in the room (never on the tray) and its role chips
    // and tags stay in the room; a turn writes the status line into '이번 턴'; the door's log popup covers the HUD as the others do.
    public static async Task<string> Board()
    {
        FieldIdleConfirm.AutoAccept = true; // one member searches, the other hushes: no idle question (VerifyIdleConfirm tests it)
        var c = Owner(); var a = await Later(c); var main = a.Main.transform; var t = a.Threat; var pl = t.Planner; var log = new List<string>();
        var hud = main.Find("HudTray").GetComponent<FieldHudTray>();
        await Task.Delay(300); Layout(a, "board");
        Check(hud.DangerRow.activeSelf && !hud.Risk.gameObject.activeSelf && hud.DangerDots.Filled == t.State.Danger && pl.TurnButton.gameObject.activeSelf && !main.Find("Hush").gameObject.activeSelf && main.Find("TurnFlow/AutoAdvance").gameObject.activeSelf, "Board parts shown (no hush button)");
        Check(!pl.Hint.enabled && pl.Chip.gameObject.activeSelf, "The plan chip in the forecast slot");
        Near(OnMain(a, (RectTransform)pl.TurnButton.transform), Turn, "turn"); Near(OnMain(a, pl.Chip.rectTransform), Hint, "chip");
        Near(OnMain(a, (RectTransform)main.Find("TurnFlow/AutoAdvance")), Auto, "keep going");
        foreach (var target in new Selectable[] { pl.TurnButton, main.Find("TurnFlow/AutoAdvance").GetComponent<Button>() }) Hit(target);
        await Still(a, "03-board"); log.Add("board layout, dots " + hud.DangerDots.Filled + "/" + hud.DangerDots.Count);
        // Two pawns on an object (co-op): they walk there, above the tray; the helper's role chips stay in the room.
        int site = Enumerable.Range(0, 2).First(i => !a.Loot.Peek(i, out var st) || !st.Complete);
        await Until(() => FieldPawnTest.Ready(a), 3000, "board ready");
        Check(FieldPawnTest.Coop(a, site), "Two pawns on " + a.ObjectNames[site] + ": " + FieldPawnTest.Describe(a));
        var board = FieldPawnTest.Board(a); await Until(() => !board.IsWalking, 3000, "walk"); await Task.Delay(300);
        Check(board.TagText(0).Contains(a.ObjectNames[site]), "The pawn's tag names the object: " + board.TagText(0));
        Layout(a, "board with placed pawns"); BoardPieces(a, "board with placed pawns");
        await Still(a, "04-board-placed"); log.Add("placed pawns above the tray, chips in the room");
        // One turn: the status line lands in '이번 턴'.
        int turns = a.Rooms.Turns; await Tap(pl.TurnButton); await Until(() => a.Rooms.Turns == turns + 1, 2000, "turn"); await Task.Delay(1400);
        await CloseLoot(a); await Task.Delay(200);
        Check(!string.IsNullOrEmpty(a.Status.text) && a.Status.enabled, "Status line after the turn: '" + a.Status.text + "'");
        Layout(a, "after a turn"); await Still(a, "05-board-after-turn"); log.Add("status '" + a.Status.text.Replace("\n", " / ") + "'");
        // The door's log (a right press) keeps the HUD under its dim; the tray is not in the way of its back button.
        board.ShowDoorLog(FieldSiteState.Corridor); await Task.Delay(200); Check(a.Popup.activeSelf && !a.ReturnConfirm.gameObject.activeSelf, "Door log popup"); Hit(a.PopupBack); await Still(a, "06-door-popup"); await Tap(a.PopupBack);
        log.Add("door log popup over the HUD");
        return "PASS board · " + string.Join(" · ", log) + " · stills " + Shots;
    }

    // Six members: the cards keep their size, three show at once and the row scrolls to the sixth; nothing else moves.
    public static async Task<string> Six()
    {
        var c = Owner(); var a = c.ArrivalPanel;
        if (a.IsOpen) { await CloseLoot(a); if (a.Search.IsOpen) a.Search.Escape(); if (a.Popup.activeSelf) a.ClosePopup(); await Until(() => !a.InTransit, 6000, "walk"); Check(a.FinishReturn(), "Return home"); await Task.Delay(150); }
        if (c.ReturnPanel && c.ReturnPanel.IsOpen) { c.ReturnPanel.Close(); await Task.Delay(100); }
        foreach (var data in c.Roster.Candidates.Where(p => !PartySelectionSession.Selected.Contains(p.Id)).ToArray())
        {
            if (c.Campaign.Party.Count() >= 6) break;
            Check(c.Campaign.AddResident(new Demo5.NightRun.Adventurer(data.DisplayName, data.RoleTitle, data.Description, data.Health, data.Aim, data.BagCapacity), 6), "Six-member fixture recruitment");
            PartySelectionSession.Selected.Add(data.Id);
        }
        c.RefreshMembers(); Check(c.Campaign.Party.Count() == 6, "Six members: " + c.Campaign.Party.Count());
        await Depart(c); if (a.Popup.activeSelf) a.ClosePopup(); await Task.Delay(300);
        Check(a.Cards.Count == 6, "Six cards");
        var scroll = a.MemberContent.GetComponentInParent<ScrollRect>(); scroll.horizontalNormalizedPosition = 0; await Task.Delay(100);
        Layout(a, "six at the start");
        var viewport = OnMain(a, (RectTransform)a.Main.transform.Find("Members"));
        int shown = a.Cards.Count(x => { var r = OnMain(a, (RectTransform)x.transform); return r.xMin >= viewport.xMin - .5f && r.xMax <= viewport.xMax + .5f; });
        Check(shown == 3, "Three whole cards in the row: " + shown);
        await Still(a, "07-six-members");
        scroll.horizontalNormalizedPosition = 1; await Task.Delay(150);
        var last = OnMain(a, (RectTransform)a.Cards[5].transform); Check(last.xMin >= viewport.xMin - .5f && last.xMax <= viewport.xMax + .5f, "The sixth card scrolls into view: " + last + " / " + viewport);
        Layout(a, "six scrolled"); await Still(a, "08-six-scrolled");
        scroll.horizontalNormalizedPosition = 0;
        return "PASS six members: cards " + CardW + "×" + CardH + ", three at a time, the row scrolls to the sixth, texts fit, nothing overlaps · stills " + Shots;
    }

    public static async Task<string> All()
    {
        var r = new List<string> { Wiring(), await FirstVisit(), await Board(), await Six() };
        return string.Join("\n", r);
    }
}
