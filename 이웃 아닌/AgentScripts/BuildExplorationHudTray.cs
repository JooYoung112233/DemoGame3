using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 탐험 화면 정리 · 배치 (2026-09-25, 기획/탐험-화면정리와-행동칸-1차.md; approved mock 기획/탐험-화면정리-시안/01-정리안.png, which follows
// 아트/승인시안/폐상가-보드게임-원본.png): the exploration room HUD becomes two papers at the top and a dark tray with three panels.
// - Top left: one paper with the place title (a one-line mirror 'PlaceTitle' of Place, drawn by FieldHudTray) and the route line
//   (RouteLabel moved in; RoutePaper hidden). The top right is the time paper (BuildTurnClockPaper, run next).
// - 'HudTray' (first child of Main, under everything): the band, three framed panels and their heading-paper tabs '대원', '이번 턴',
//   '행동'; the 위험도 dots row and the status warning icon. Existing nodes keep their names and move onto it:
//   대원 = Members (cards 272×230 = the card lines + the member action slot row, left to right, scroll past three);
//   이번 턴 = Resources (small, top right), Risk (first-visit line) / 위험도 dots (site board), Hint + PlanChip (forecast), Status;
//   행동 = TurnAdvance (big), TurnFlow/AutoAdvance, Hush (title only), Return (dark frame as in the mock).
//   ArrivalPaper, ArrivalTitle, ResourcePaper, RiskPaper, RoutePaper are hidden (duplicates / description papers the mock drops).
// - The room keeps everything above the tray: FieldPlanTargetMarkers.RoomArea ends above it and the tray is on its keep-clear list;
//   the first-visit tutorial banner (SettlementTutorialGuide.FieldPosition) sits at the top centre, off the member cards, the pawns and
//   the '이번 턴' panel (말 놓기 2026-09-25; BuildRetireOldAssign sets the same value); the encounter view's
//   member row grows to the taller cards.
// Cards are taller than the mock's (230 vs 177: the lead's card size keeps today's card lines above the action slot), so the tray
// starts at y 776 instead of 812; panel widths, order and grouping follow the mock.
// Base ExpeditionArrivalPanel.prefab first, then the same pass over SettlementScreen's nested instance (no new overrides; its old
// RouteLabel font-size override is set to the new size), then ExpeditionEncounterPanel.prefab. Idempotent: found or created by
// name; a second run prints 'Already applied.'. Run after every other exploration builder, including BuildAssignmentBubble →
// BuildFieldTurnFlow → BuildFieldPlanMarkers; then BuildTurnClockPaper.Run.
public static class BuildExplorationHudTray
{
    const string P = "Assets/Prefabs/Settlement/", ArrivalPath = P + "ExpeditionArrivalPanel.prefab", ScreenPath = P + "SettlementScreen.prefab", EncounterPath = P + "ExpeditionEncounterPanel.prefab", Art = "Assets/Art/PartySelection/";
    // Main space: 1920×1080, x right, y down from the top-left (the mock image minus its 64 px caption strip).
    public const float CardW = 272, CardH = 230, CardGap = 14, CardTopPad = 18, CardLeftPad = 6;
    public const float TrayTop = 776, PanelTop = 790, PanelH = 278;
    public static readonly Rect Tray = new Rect(0, TrayTop, 1920, 1080 - TrayTop);
    public static readonly Rect MembersPanel = new Rect(20, PanelTop, 880, PanelH), TurnPanel = new Rect(920, PanelTop, 410, PanelH), ActionPanel = new Rect(1350, PanelTop, 550, PanelH);
    public static readonly Rect MembersTab = new Rect(35, PanelTop - 9, 104, 40), TurnTab = new Rect(935, PanelTop - 9, 163, 40), ActionTab = new Rect(1365, PanelTop - 9, 104, 40);
    public static readonly Rect PlacePaper = new Rect(42, 24, 422, 124), PlaceTitle = new Rect(68, 36, 380, 52), Route = new Rect(68, 94, 380, 36);
    public static readonly Rect Members = new Rect(28, 812, 864, 250);
    public static readonly Rect Resources = new Rect(1100, 796, 214, 28), Risk = new Rect(944, 840, 372, 36), DangerRow = new Rect(944, 841, 210, 34),
        Hint = new Rect(944, 890, 372, 68), Status = new Rect(946, 974, 376, 62), StatusWarn = new Rect(924, 980, 20, 18);
    public static readonly Rect Turn = new Rect(1370, 834, 300, 130), Auto = new Rect(1688, 834, 196, 60), Hush = new Rect(1688, 904, 196, 60), Return = new Rect(1370, 984, 514, 64);
    public static readonly Vector2 GuidePosition = new Vector2(720, -24), GuideSize = new Vector2(480, 148);
    public static readonly Rect RoomArea = new Rect(16, 8, 1888, TrayTop - 24);
    static readonly Color BandTint = new Color(.58f, .52f, .52f, 1), PanelFill = new Color(.133f, .18f, .188f, 1), PanelBorder = new Color(.275f, .33f, .33f, 1),
        Ink = new Color(.045f, .065f, .06f, 1), RouteInk = new Color(.32f, .3f, .27f, 1), Light = new Color(.93f, .91f, .85f, 1), Grey = new Color(.62f, .66f, .64f, 1),
        Warn = new Color(1f, .62f, .5f, 1), DangerInk = new Color(.86f, .36f, .26f, 1), DotEmpty = new Color(.275f, .314f, .314f, 1), DotOutline = new Color(.078f, .078f, .078f, 1),
        ReturnFill = new Color(.227f, .275f, .275f, 1), ReturnBorder = new Color(.47f, .51f, .5f, 1);
    static readonly List<string> log = new List<string>();
    static Font font; static string where;

    public static string Run()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Stop Play and preserve the scene first");
        log.Clear();
        // 1. The encounter view copies the arrival cards: its member row takes the taller cards.
        Edit(EncounterPath, root =>
        {
            where = "encounter: "; var e = root.GetComponent<ExpeditionEncounterPanel>() ?? throw new Exception("ExpeditionEncounterPanel missing");
            // Members is the row's content; its parent is the masked viewport.
            var content = e.Members ?? throw new Exception("Encounter Members missing"); var viewport = content.parent as RectTransform;
            if (viewport && viewport.sizeDelta.y < CardH + 2) { var p = TopLeft(viewport); Place(viewport, p.x, p.y, viewport.sizeDelta.x, CardH + 2, "Members viewport"); }
            if (content.sizeDelta.y < CardH) { content.sizeDelta = new Vector2(content.sizeDelta.x, CardH); Changed("Members/Content height"); }
        });
        // 2. The base panel, then SettlementScreen's nested instance (inherits the new nodes; the same pass removes stale overrides).
        Edit(ArrivalPath, root => { where = ""; Configure(root.GetComponent<ExpeditionArrivalPanel>() ?? throw new Exception("ExpeditionArrivalPanel missing"), false); });
        Edit(ScreenPath, root =>
        {
            where = "screen: ";
            foreach (var a in root.GetComponentsInChildren<ExpeditionArrivalPanel>(true)) Configure(a, true);
            var guide = root.GetComponentInChildren<SettlementTutorialGuide>(true);
            if (guide && (guide.FieldPosition != GuidePosition || guide.FieldSize != GuideSize)) { guide.FieldPosition = GuidePosition; guide.FieldSize = GuideSize; EditorUtility.SetDirty(guide); Changed("tutorial field banner at the top centre"); }
        });
        AssetDatabase.SaveAssets();
        return log.Count == 0 ? "Already applied." : "Applied: " + string.Join("; ", log.Distinct());
    }

    static void Edit(string path, Action<GameObject> edit)
    {
        var root = PrefabUtility.LoadPrefabContents(path); int before = log.Count;
        try { edit(root); if (log.Count > before) PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void Configure(ExpeditionArrivalPanel arrival, bool nested)
    {
        var main = arrival.Main ? arrival.Main.transform : throw new Exception("Main missing");
        RectTransform Need(string path) => main.Find(path) as RectTransform ?? throw new Exception("Main/" + path + " missing (run the earlier exploration builders first)");
        font = arrival.Place.font;

        // ---- top-left paper: place title + route ----
        var placePaper = Need("PlacePaper"); Place(placePaper, PlacePaper, "PlacePaper"); Paper(placePaper.GetComponent<Image>(), "count-paper", Color.white, "PlacePaper");
        // Place keeps its two lines for other screens (the battle HUD copies it); hidden, its rect still holds them (text-bounds checks).
        var place = arrival.Place; Place(place.rectTransform, new Rect(PlaceTitle.x, PlaceTitle.y - 6, PlaceTitle.width, 104), "Place"); Enabled(place, false, "Place (mirrored by PlaceTitle)");
        var title = Child(main, "PlaceTitle", nested, typeof(Text)); if (Made) title.SetSiblingIndex(place.transform.GetSiblingIndex() + 1);
        Place(title, PlaceTitle, "PlaceTitle"); var titleText = title.GetComponent<Text>(); if (Made) titleText.text = "폐상가 · 1F 오락실";
        Style(titleText, 34, Ink, TextAnchor.MiddleLeft, 24, "PlaceTitle");
        var route = Need("RouteLabel"); Place(route, Route, "RouteLabel"); Style(route.GetComponent<Text>(), 23, RouteInk, TextAnchor.MiddleLeft, 0, "RouteLabel");
        Active(Need("RoutePaper"), false, "RoutePaper");

        // ---- the tray (under everything in Main) ----
        var tray = Child(main, "HudTray", nested, typeof(Image), typeof(CanvasGroup)); if (Made) tray.SetSiblingIndex(0);
        Place(tray, Tray, "HudTray");
        var band = tray.GetComponent<Image>(); Paper(band, "teal-texture", BandTint, "HudTray band", Image.Type.Tiled);
        if (!band.raycastTarget) { band.raycastTarget = true; Changed("tray floor takes clicks"); }
        Panel(tray, "MembersPanel", MembersPanel, nested); Panel(tray, "TurnPanel", TurnPanel, nested); Panel(tray, "ActionPanel", ActionPanel, nested);
        Tab(tray, "MembersTab", MembersTab, "대원", nested); Tab(tray, "TurnTab", TurnTab, "이번 턴", nested); Tab(tray, "ActionTab", ActionTab, "행동", nested);
        // 위험도 row (site board) and the status warning icon: tray decorations under the texts.
        var row = Child(tray, "DangerRow", nested); PlaceIn(Tray, row, DangerRow, "DangerRow");
        var rowLabel = Child(row, "Label", nested, typeof(Text)); PlaceIn(DangerRow, rowLabel, new Rect(DangerRow.x, DangerRow.y, 92, DangerRow.height), "DangerRow/Label");
        var rowText = rowLabel.GetComponent<Text>(); if (Made) rowText.text = "위험도"; Style(rowText, 22, Light, TextAnchor.MiddleLeft, 0, "DangerRow/Label");
        var dots = Child(row, "Dots", nested, typeof(FieldDotsGraphic)); PlaceIn(DangerRow, dots, new Rect(DangerRow.x + 96, DangerRow.y + 5, 104, 24), "DangerRow/Dots");
        var dg = dots.GetComponent<FieldDotsGraphic>();
        if (Made) { dg.Count = 3; dg.color = DangerInk; dg.Empty = DotEmpty; dg.Outline = DotOutline; dg.PreviewColor = new Color(Warn.r, Warn.g, Warn.b, .55f); dg.Gap = 10; EditorUtility.SetDirty(dg); }
        Active(row, false, "DangerRow (site board only)");
        var warn = Child(tray, "StatusWarn", nested, typeof(WarningTriangle)); PlaceIn(Tray, warn, StatusWarn, "StatusWarn");
        if (Made) warn.GetComponent<WarningTriangle>().color = Warn;
        var mark = Child(warn, "Mark", nested, typeof(Text)); Stretch(mark);
        var markText = mark.GetComponent<Text>(); if (Made) markText.text = "!"; Style(markText, 12, Ink, TextAnchor.LowerCenter, 0, "StatusWarn/Mark");
        Active(warn, false, "StatusWarn");
        var hud = tray.GetComponent<FieldHudTray>(); if (!hud) { hud = tray.gameObject.AddComponent<FieldHudTray>(); Changed("+FieldHudTray"); }

        // ---- 대원: member cards left to right (scroll past three, as before) ----
        var members = Need("Members"); Place(members, Members, "Members");
        var content = arrival.MemberContent ?? throw new Exception("MemberContent missing");
        if (content.anchorMin != new Vector2(0, 1) || content.anchorMax != new Vector2(0, 1) || content.pivot != new Vector2(0, 1) || content.anchoredPosition != Vector2.zero || !Mathf.Approximately(content.sizeDelta.y, Members.height))
        { content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(content.sizeDelta.x, Members.height); Changed("Members/Content"); }
        var layout = content.GetComponent<HorizontalLayoutGroup>() ?? throw new Exception("Members/Content layout missing");
        var pad = new RectOffset((int)CardLeftPad, (int)CardLeftPad, (int)CardTopPad, (int)(Members.height - CardTopPad - CardH));
        if (layout.padding.left != pad.left || layout.padding.right != pad.right || layout.padding.top != pad.top || layout.padding.bottom != pad.bottom || !Mathf.Approximately(layout.spacing, CardGap)
            || layout.childAlignment != TextAnchor.UpperLeft || layout.childControlWidth || !layout.childControlHeight || layout.childForceExpandWidth || !layout.childForceExpandHeight)
        {
            layout.padding = pad; layout.spacing = CardGap; layout.childAlignment = TextAnchor.UpperLeft; layout.childControlWidth = false; layout.childForceExpandWidth = false;
            layout.childControlHeight = layout.childForceExpandHeight = true; EditorUtility.SetDirty(layout); Changed("cards " + CardW + "×" + CardH + " (layout height)");
        }

        // ---- 이번 턴: resources, risk / dots, forecast, status ----
        Active(Need("ArrivalPaper"), false, "ArrivalPaper"); Active(Need("ArrivalTitle"), false, "ArrivalTitle");
        Active(Need("ResourcePaper"), false, "ResourcePaper"); Active(Need("RiskPaper"), false, "RiskPaper");
        var res = arrival.Resources; Place(res.rectTransform, Resources, "Resources"); Style(res, 18, Grey, TextAnchor.MiddleRight, 0, "Resources");
        var risk = Need("Risk").GetComponent<Text>(); Place(risk.rectTransform, Risk, "Risk"); Style(risk, 22, Light, TextAnchor.MiddleLeft, 0, "Risk");
        var hint = Need("Hint").GetComponent<Text>(); Place(hint.rectTransform, Hint, "Hint"); Style(hint, 21, Light, TextAnchor.MiddleLeft, 0, "Hint");
        var chip = hint.transform.Find("PlanChip") as RectTransform ?? throw new Exception("Hint/PlanChip missing (BuildFieldTurnPlan)");
        PlaceIn(Hint, chip, Hint, "PlanChip"); var chipText = chip.GetComponent<Text>(); Style(chipText, 22, Light, TextAnchor.MiddleLeft, 17, "PlanChip");
        var status = arrival.Status; Place(status.rectTransform, Status, "Status"); Style(status, 20, Light, TextAnchor.UpperLeft, 0, "Status");

        // ---- 행동: turn, keep going, hush, return ----
        var turn = Need("TurnAdvance"); Place(turn, Turn, "TurnAdvance");
        Band(turn, "Title", 20, 54, 36, "TurnAdvance"); Band(turn, "Subtitle", 78, 34, 20, "TurnAdvance");
        var hush = Need("Hush"); Place(hush, Hush, "Hush"); Band(hush, "Title", 8, 44, 25, "Hush");
        var hushSub = hush.Find("Subtitle"); if (hushSub) Active(hushSub, false, "Hush/Subtitle (title only, as in the mock)");
        var auto = Need("TurnFlow/AutoAdvance"); Place(auto, Auto, "AutoAdvance");
        var autoLabel = auto.Find("Label") as RectTransform; if (autoLabel) { PlaceIn(Auto, autoLabel, new Rect(Auto.x + 14, Auto.y, 130, Auto.height), "AutoAdvance/Label"); Style(autoLabel.GetComponent<Text>(), 23, null, TextAnchor.MiddleCenter, 0, "AutoAdvance/Label"); }
        var glyph = auto.Find("Glyph") as RectTransform; if (glyph) PlaceIn(Auto, glyph, new Rect(Auto.x + 150, Auto.y + 20, 30, 20), "AutoAdvance/Glyph");
        var ret = (RectTransform)arrival.Return.transform; Place(ret, Return, "Return");
        var retImage = ret.GetComponent<Image>();
        if (retImage.sprite || retImage.color != ReturnFill) { retImage.sprite = null; retImage.type = Image.Type.Simple; retImage.color = ReturnFill; Changed("Return dark"); }
        var frame = Child(ret, "Frame", nested, typeof(FieldPanelGraphic)); if (Made) frame.SetSiblingIndex(0);
        Stretch(frame, 0); var fg = frame.GetComponent<FieldPanelGraphic>();
        if (fg.Fill || fg.Border != ReturnBorder || !Mathf.Approximately(fg.Thickness, 2) || fg.raycastTarget) { fg.Fill = false; fg.Border = ReturnBorder; fg.Thickness = 2; fg.raycastTarget = false; fg.SetVerticesDirty(); EditorUtility.SetDirty(fg); Changed("Return frame"); }
        var retLabel = ret.Find("Label") as RectTransform; if (retLabel) { PlaceIn(Return, retLabel, new Rect(Return.x + 6, Return.y, Return.width - 12, Return.height), "Return/Label"); Style(retLabel.GetComponent<Text>(), 28, Light, TextAnchor.MiddleCenter, 0, "Return/Label"); }
        var retIcon = ret.Find("Icon") as RectTransform;
        if (retIcon) { PlaceIn(Return, retIcon, new Rect(Return.x + 22, Return.y + (Return.height - 34) / 2, 26, 34), "Return/Icon"); var ic = retIcon.GetComponent<Image>(); if (ic && ic.color != Light) { ic.color = Light; Changed("Return/Icon light"); } }

        // ---- wiring ----
        Set(hud, x => x.Arrival, arrival, (x, v) => x.Arrival = v); Set(hud, x => x.Tray, tray.GetComponent<CanvasGroup>(), (x, v) => x.Tray = v);
        Set(hud, x => x.PlaceTitle, titleText, (x, v) => x.PlaceTitle = v); Set(hud, x => x.DangerRow, row.gameObject, (x, v) => x.DangerRow = v);
        Set(hud, x => x.DangerDots, dg, (x, v) => x.DangerDots = v); Set(hud, x => x.Risk, risk, (x, v) => x.Risk = v);
        Set(hud, x => x.Status, status, (x, v) => x.Status = v); Set(hud, x => x.StatusWarn, warn.gameObject, (x, v) => x.StatusWarn = v);
        if (hud.TextColor != Light || hud.WarnColor != Warn) { hud.TextColor = Light; hud.WarnColor = Warn; EditorUtility.SetDirty(hud); Changed("tray colours"); }
        var floating = new Graphic[] { res, risk };
        if (hud.Floating == null || !hud.Floating.SequenceEqual(floating)) { hud.Floating = floating; EditorUtility.SetDirty(hud); Changed("texts hidden with the tray under the encounter"); }

        // ---- the room stays above the tray: bubbles never cover it ----
        var markers = main.GetComponentInChildren<FieldPlanTargetMarkers>(true);
        if (markers)
        {
            if (markers.RoomArea != RoomArea) { markers.RoomArea = RoomArea; EditorUtility.SetDirty(markers); Changed("bubble room area above the tray"); }
            var clear = (markers.KeepClear ?? new RectTransform[0]).Where(r => r).ToList(); int n = clear.Count;
            foreach (var r in new[] { (RectTransform)tray, placePaper }) if (!clear.Contains(r)) clear.Add(r);
            if (clear.Count != n || markers.KeepClear == null || markers.KeepClear.Length != clear.Count) { markers.KeepClear = clear.ToArray(); EditorUtility.SetDirty(markers); Changed("keep-clear + HudTray"); }
        }
    }

    // ---- helpers ----
    static bool Made;
    static void Changed(string what) => log.Add(where + what);
    static Transform Child(Transform parent, string name, bool nested, params Type[] components)
    {
        var t = parent.Find(name); Made = !t; if (t) return t;
        if (nested) throw new Exception(parent.name + "/" + name + " missing in the nested instance (the base prefab save did not reach SettlementScreen)");
        var types = new List<Type> { typeof(RectTransform) };
        if (components.Any(c => typeof(Graphic).IsAssignableFrom(c))) types.Add(typeof(CanvasRenderer));
        types.AddRange(components);
        var g = new GameObject(name, types.ToArray()); g.transform.SetParent(parent, false); g.layer = parent.gameObject.layer;
        Changed(parent.name + "/" + name + " created"); return g.transform;
    }
    static Vector2 TopLeft(RectTransform r) => new Vector2(r.anchoredPosition.x - r.pivot.x * r.sizeDelta.x, -(r.anchoredPosition.y + (1 - r.pivot.y) * r.sizeDelta.y));
    static bool Near(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < .0001f;
    // Top-left anchored rect in the parent's space (x right, y down), whatever the pivot.
    static void Place(Transform t, float x, float y, float w, float h, string what)
    {
        var r = (RectTransform)t; var anchor = new Vector2(0, 1); var size = new Vector2(w, h); var pos = new Vector2(x + r.pivot.x * w, -(y + (1 - r.pivot.y) * h));
        if (r.anchorMin == anchor && r.anchorMax == anchor && Near(r.sizeDelta, size) && Near(r.anchoredPosition, pos)) return;
        r.anchorMin = r.anchorMax = anchor; r.sizeDelta = size; r.anchoredPosition = pos; Changed(what + " rect");
    }
    static void Place(Transform t, Rect m, string what) => Place(t, m.x, m.y, m.width, m.height, what);
    // A child placed by its Main-space rect inside a parent whose Main-space rect is given.
    static void PlaceIn(Rect parent, Transform t, Rect m, string what) => Place(t, m.x - parent.x, m.y - parent.y, m.width, m.height, what);
    static void Stretch(Transform t, float inset = 0)
    {
        var r = (RectTransform)t; var lo = new Vector2(inset, inset); var hi = new Vector2(-inset, -inset);
        if (r.anchorMin == Vector2.zero && r.anchorMax == Vector2.one && Near(r.offsetMin, lo) && Near(r.offsetMax, hi)) return;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, .5f); r.offsetMin = lo; r.offsetMax = hi; Changed(t.name + " stretched");
    }
    // A band child (anchored across the top of its button) at top/h with a font size.
    static void Band(Transform button, string child, float top, float h, int size, string what)
    {
        var r = button.Find(child) as RectTransform; if (!r) return; var text = r.GetComponent<Text>();
        var min = new Vector2(0, 1); var max = new Vector2(1, 1); var pos = new Vector2(0, -top); var sz = new Vector2(-12, h);
        if (r.anchorMin != min || r.anchorMax != max || r.pivot != new Vector2(.5f, 1) || !Near(r.anchoredPosition, pos) || !Near(r.sizeDelta, sz))
        { r.anchorMin = min; r.anchorMax = max; r.pivot = new Vector2(.5f, 1); r.anchoredPosition = pos; r.sizeDelta = sz; Changed(what + "/" + child + " band"); }
        if (text && text.fontSize != size) { text.fontSize = size; text.resizeTextForBestFit = false; EditorUtility.SetDirty(text); Changed(what + "/" + child + " size"); }
    }
    static void Active(Transform t, bool on, string what) { if (t.gameObject.activeSelf == on) return; t.gameObject.SetActive(on); Changed(what + (on ? " shown" : " hidden")); }
    static void Enabled(Behaviour b, bool on, string what) { if (b.enabled == on) return; b.enabled = on; EditorUtility.SetDirty(b); Changed(what + (on ? " on" : " off")); }
    static void Paper(Image img, string sprite, Color color, string what, Image.Type type = Image.Type.Simple)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(Art + sprite + ".png") ?? throw new Exception("Missing " + Art + sprite + ".png");
        if (img.sprite == s && img.color == color && img.type == type && !img.preserveAspect) return;
        img.sprite = s; img.color = color; img.type = type; img.preserveAspect = false; EditorUtility.SetDirty(img); Changed(what + " paper");
    }
    // Single style for HUD texts: font, size, colour (null keeps it), alignment, wrap + truncate, best fit down to bestMin (0 = off).
    static void Style(Text t, int size, Color? color, TextAnchor align, int bestMin, string what)
    {
        bool best = bestMin > 0; var c = color ?? t.color;
        if (t.font == font && t.fontSize == size && t.color == c && t.alignment == align && t.horizontalOverflow == HorizontalWrapMode.Wrap && t.verticalOverflow == VerticalWrapMode.Truncate
            && t.resizeTextForBestFit == best && (!best || t.resizeTextMinSize == bestMin && t.resizeTextMaxSize == size) && Mathf.Approximately(t.lineSpacing, 1) && !t.raycastTarget && t.supportRichText) return;
        t.font = font; t.fontSize = size; t.color = c; t.alignment = align; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextForBestFit = best; if (best) { t.resizeTextMinSize = bestMin; t.resizeTextMaxSize = size; }
        t.lineSpacing = 1; t.raycastTarget = false; t.supportRichText = true; EditorUtility.SetDirty(t); Changed(what + " text");
    }
    static void Panel(Transform tray, string name, Rect m, bool nested)
    {
        var t = Child(tray, name, nested, typeof(FieldPanelGraphic)); PlaceIn(Tray, t, m, name);
        var g = t.GetComponent<FieldPanelGraphic>();
        if (g.color != PanelFill || g.Border != PanelBorder || !Mathf.Approximately(g.Thickness, 2) || !g.Fill || g.raycastTarget)
        { g.color = PanelFill; g.Border = PanelBorder; g.Thickness = 2; g.Fill = true; g.raycastTarget = false; g.SetVerticesDirty(); EditorUtility.SetDirty(g); Changed(name + " look"); }
    }
    static void Tab(Transform tray, string name, Rect m, string label, bool nested)
    {
        var t = Child(tray, name, nested, typeof(Image)); PlaceIn(Tray, t, m, name);
        var img = t.GetComponent<Image>(); Paper(img, "heading-paper", Color.white, name); if (img.raycastTarget) { img.raycastTarget = false; Changed(name + " passes clicks"); }
        var l = Child(t, "Label", nested, typeof(Text)); PlaceIn(m, l, new Rect(m.x + 14, m.y, m.width - 18, m.height), name + "/Label");
        var text = l.GetComponent<Text>(); if (Made) text.text = label; Style(text, 24, Ink, TextAnchor.MiddleLeft, 0, name + "/Label");
    }
    static void Set<TC, TV>(TC owner, Func<TC, TV> get, TV value, Action<TC, TV> set) where TC : Object where TV : Object
    {
        if (get(owner) == value) return; set(owner, value); EditorUtility.SetDirty(owner); Changed(owner.GetType().Name + " → " + (value ? value.name : "null"));
    }
}
