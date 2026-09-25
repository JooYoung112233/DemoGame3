using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 턴 진행 연출 · 시간 종이 (2026-09-25, user: '턴 진행 눌렀을 때 우상단 턴 칸수가 내려가고, 시계 같은 타이머가 나와서 돌아가는 연출이랑,
// 다음 턴에 크리쳐가 등장할 수 있으면 경고'; approved mock 기획/탐험-화면정리-시안/01-정리안.png for the top-right paper):
// - Time paper (top right): DayPaper becomes the one paper with 'DAY 1   10:00' (DayTime, drawn by FieldTurnReplay from the
//   arrival Clock, which keeps its two lines for other screens) and, on the site board, 24 turn pips (TurnPips, a 오래 머묾 mark
//   every 8 in red) with '남은 N턴' (TurnCount mirrors the room navigation's TurnLabel; first visit: '탐험 N턴', no pips).
//   TurnPaper is hidden; Clock and TurnLabel keep being written but draw nothing.
// - Turn sequence: TurnFlow/TurnBanner becomes the clock that pops out beside the time paper ('<N>턴 · 10분 흐름', a paper clock
//   face whose hand sweeps the minutes); TurnTick '+10분' sits by the time digits.
// - Creature warning: TurnFlow/TurnWarning (red paper strip with a WarningTriangle, top centre under the papers, left of the
//   corridor's locked-door tag) and TurnFlow/EdgePulse (screen-edge glow), run by FieldTurnWarning on TurnFlow; the first visit's
//   old banner (root/WarningBanner) moves to the strip's place and steps aside while the strip speaks for it.
// Base ExpeditionArrivalPanel.prefab, then the same pass over SettlementScreen's nested instance (no new overrides).
// Idempotent (found or created by name; 'Already applied.' on a second run). Run after BuildExplorationHudTray.Run.
public static class BuildTurnClockPaper
{
    const string P = "Assets/Prefabs/Settlement/", ArrivalPath = P + "ExpeditionArrivalPanel.prefab", ScreenPath = P + "SettlementScreen.prefab", Art = "Assets/Art/PartySelection/";
    // Main space (1920×1080, y down from the top-left).
    public static readonly Rect DayPaper = new Rect(1485, 24, 400, 124), DayTime = new Rect(1512, 36, 262, 52), Pips = new Rect(1514, 100, 216, 18), Count = new Rect(1742, 90, 124, 38);
    public static readonly Rect Banner = new Rect(1178, 30, 300, 112), Tick = new Rect(1782, 40, 86, 34), Warning = new Rect(500, 158, 580, 84);
    static readonly Color Ink = new Color(.045f, .065f, .06f, 1), PipInk = new Color(.15f, .13f, .11f, 1), ClockInk = new Color(.12f, .11f, .09f, 1), Face = new Color(.99f, .97f, .9f, 1),
        Sweep = new Color(1f, .72f, .3f, .6f), WarnPaper = new Color(.74f, .24f, .18f, 1), WarnIcon = new Color(1f, .84f, .36f, 1), WarnTitle = new Color(1f, .96f, .9f, 1),
        WarnLine = new Color(1f, .9f, .84f, 1), Edge = new Color(.85f, .12f, .08f, .85f);
    static readonly List<string> log = new List<string>();
    static Font font; static string where; static bool Made;

    public static string Run()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Stop Play and preserve the scene first");
        log.Clear();
        Edit(ArrivalPath, root => { where = ""; Configure(root.GetComponent<ExpeditionArrivalPanel>() ?? throw new Exception("ExpeditionArrivalPanel missing"), false); });
        Edit(ScreenPath, root => { where = "screen: "; foreach (var a in root.GetComponentsInChildren<ExpeditionArrivalPanel>(true)) Configure(a, true); });
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
        RectTransform Need(Transform parent, string path) => parent.Find(path) as RectTransform ?? throw new Exception(parent.name + "/" + path + " missing (run the earlier builders first)");
        font = arrival.Place.font;
        var tray = Need(main, "HudTray").GetComponent<FieldHudTray>() ?? throw new Exception("Run BuildExplorationHudTray.Run first");
        var flow = Need(main, "TurnFlow"); var replay = flow.GetComponent<FieldTurnReplay>() ?? throw new Exception("Run BuildFieldTurnFlow.Run first");

        // ---- the time paper ----
        var paper = Need(main, "DayPaper"); Place(paper, DayPaper, "DayPaper"); Paper(paper.GetComponent<Image>(), "count-paper", Color.white, "DayPaper");
        // Clock and TurnLabel keep being written (the battle HUD copies Clock); hidden, their rects still hold their text (text-bounds checks).
        var clock = arrival.Clock; Place(clock.rectTransform, new Rect(DayTime.x, DayTime.y - 6, DayTime.width, 100), "Clock"); Enabled(clock, false, "Clock (mirrored by DayTime)");
        var day = Child(main, "DayTime", nested, typeof(Text)); if (Made) day.SetSiblingIndex(clock.transform.GetSiblingIndex() + 1);
        Place(day, DayTime, "DayTime"); var dayText = day.GetComponent<Text>(); if (Made) dayText.text = "DAY 1   10:00"; Style(dayText, 34, Ink, TextAnchor.MiddleLeft, "DayTime");
        var pips = Child(main, "TurnPips", nested, typeof(FieldTurnPips)); if (Made) pips.SetSiblingIndex(day.GetSiblingIndex() + 1);
        Place(pips, Pips, "TurnPips"); var pg = pips.GetComponent<FieldTurnPips>();
        if (Made) { pg.color = PipInk; pg.Count = 24; pg.MarkEvery = 8; pg.Gap = 2; EditorUtility.SetDirty(pg); }
        Active(pips, false, "TurnPips (site board only)");
        Active(Need(main, "TurnPaper"), false, "TurnPaper");
        var label = arrival.Rooms ? arrival.Rooms.TurnLabel : null; if (!label) throw new Exception("Rooms.TurnLabel missing");
        Place(label.rectTransform, new Rect(Count.x, Count.y - 4, Count.width, 46), "TurnLabel"); Enabled(label, false, "TurnLabel (mirrored by TurnCount)");
        var count = Child(main, "TurnCount", nested, typeof(Text)); if (Made) { count.SetSiblingIndex(pips.GetSiblingIndex() + 1); ((RectTransform)count).pivot = new Vector2(.5f, .5f); }
        Place(count, Count, "TurnCount"); var countText = count.GetComponent<Text>(); if (Made) countText.text = "남은 24턴"; Style(countText, 22, Ink, TextAnchor.MiddleRight, "TurnCount");

        // ---- the clock that pops out beside the paper (the old turn banner) and the '+10분' tick ----
        var banner = Need(flow, "TurnBanner"); Place(banner, Banner, "TurnBanner");
        var bc = Need(banner, "Clock"); PlaceIn(Banner, bc, new Rect(Banner.x + 198, Banner.y + 8, 96, 96), "TurnBanner/Clock");
        var glyph = bc.GetComponent<FieldClockGlyph>() ?? throw new Exception("TurnBanner/Clock is not a FieldClockGlyph");
        if (glyph.color != ClockInk || glyph.Face != Face || glyph.SweepColor != Sweep || !Mathf.Approximately(glyph.Rim, 4) || !Mathf.Approximately(glyph.Hand, 4) || !Mathf.Approximately(glyph.TickLength, 7))
        { glyph.color = ClockInk; glyph.Face = Face; glyph.SweepColor = Sweep; glyph.Rim = 4; glyph.Hand = 4; glyph.TickLength = 7; glyph.MinuteLength = .74f; glyph.HourLength = .48f; glyph.SetVerticesDirty(); EditorUtility.SetDirty(glyph); Changed("clock face"); }
        var bt = Need(banner, "Title"); PlaceIn(Banner, bt, new Rect(Banner.x + 8, Banner.y + 20, 186, 40), "TurnBanner/Title"); Style(bt.GetComponent<Text>(), 24, ClockInk, TextAnchor.MiddleRight, "TurnBanner/Title");
        var bd = Need(banner, "Detail"); PlaceIn(Banner, bd, new Rect(Banner.x + 8, Banner.y + 60, 186, 30), "TurnBanner/Detail"); Style(bd.GetComponent<Text>(), 19, new Color(ClockInk.r, ClockInk.g, ClockInk.b, .8f), TextAnchor.MiddleRight, "TurnBanner/Detail");
        Place(Need(flow, "TurnTick"), Tick, "TurnTick");

        // ---- the creature warning strip and the edge glow ----
        var warn = Child(flow, "TurnWarning", nested, typeof(CanvasGroup)); if (Made) ((RectTransform)warn).pivot = new Vector2(.5f, .5f);
        Place(warn, Warning, "TurnWarning"); var group = warn.GetComponent<CanvasGroup>();
        if (group.interactable || group.blocksRaycasts) { group.interactable = group.blocksRaycasts = false; Changed("warning passes clicks"); }
        var wp = Child(warn, "Paper", nested, typeof(Image)); Stretch(wp); var wpi = wp.GetComponent<Image>(); Paper(wpi, "footer-paper", WarnPaper, "TurnWarning/Paper"); if (wpi.raycastTarget) { wpi.raycastTarget = false; Changed("warning paper passes clicks"); }
        var icon = Child(warn, "Icon", nested, typeof(WarningTriangle)); PlaceIn(Warning, icon, new Rect(Warning.x + 18, Warning.y + 16, 54, 48), "TurnWarning/Icon");
        if (Made) icon.GetComponent<WarningTriangle>().color = WarnIcon;
        var bang = Child(icon, "Mark", nested, typeof(Text)); Stretch(bang); var bangText = bang.GetComponent<Text>(); if (Made) bangText.text = "!"; Style(bangText, 30, Ink, TextAnchor.LowerCenter, "TurnWarning/Icon/Mark");
        var wt = Child(warn, "Title", nested, typeof(Text)); PlaceIn(Warning, wt, new Rect(Warning.x + 84, Warning.y + 4, 484, 44), "TurnWarning/Title");
        var wtt = wt.GetComponent<Text>(); if (Made) wtt.text = "무언가가 다가옵니다"; Style(wtt, 29, WarnTitle, TextAnchor.MiddleLeft, "TurnWarning/Title");
        var wl = Child(warn, "Line", nested, typeof(Text)); PlaceIn(Warning, wl, new Rect(Warning.x + 84, Warning.y + 48, 484, 30), "TurnWarning/Line");
        var wlt = wl.GetComponent<Text>(); if (Made) wlt.text = "다음 턴에 이 방으로 들어옵니다 · 숨죽이면 지나갈 수 있습니다"; Style(wlt, 18, WarnLine, TextAnchor.MiddleLeft, "TurnWarning/Line");
        Active(warn, false, "TurnWarning");
        var edge = Child(flow, "EdgePulse", nested, typeof(FieldEdgePulse)); Stretch(edge); var eg = edge.GetComponent<FieldEdgePulse>();
        if (eg.color != Edge || !Mathf.Approximately(eg.Width, 46)) { eg.color = Edge; eg.Width = 46; EditorUtility.SetDirty(eg); Changed("edge glow"); }
        Active(edge, false, "EdgePulse");
        // The first visit's old warning banner (the encounter's) moves to the strip's place, with a group to step aside.
        var legacy = arrival.transform.Find("WarningBanner") as RectTransform; CanvasGroup legacyGroup = null;
        if (legacy)
        {
            Place(legacy, Warning.x, Warning.y, Warning.width, 72, "WarningBanner");
            var lp = legacy.Find("Paper"); if (lp) PlaceIn(new Rect(Warning.x, Warning.y, Warning.width, 72), lp, new Rect(Warning.x, Warning.y, Warning.width, 72), "WarningBanner/Paper");
            var lt = legacy.Find("Text"); if (lt) PlaceIn(new Rect(Warning.x, Warning.y, Warning.width, 72), lt, new Rect(Warning.x + 22, Warning.y, Warning.width - 44, 72), "WarningBanner/Text");
            legacyGroup = legacy.GetComponent<CanvasGroup>(); if (!legacyGroup) { legacyGroup = legacy.gameObject.AddComponent<CanvasGroup>(); Changed("WarningBanner +CanvasGroup"); }
            if (legacyGroup.interactable || legacyGroup.blocksRaycasts) { legacyGroup.interactable = legacyGroup.blocksRaycasts = false; Changed("WarningBanner passes clicks"); }
        }

        // ---- wiring ----
        var w = flow.GetComponent<FieldTurnWarning>(); if (!w) { w = flow.gameObject.AddComponent<FieldTurnWarning>(); Changed("TurnFlow +FieldTurnWarning"); }
        Set(w, x => x.Arrival, arrival, (x, v) => x.Arrival = v); Set(w, x => x.Strip, group, (x, v) => x.Strip = v);
        Set(w, x => x.Title, wtt, (x, v) => x.Title = v); Set(w, x => x.Line, wlt, (x, v) => x.Line = v); Set(w, x => x.Edge, eg, (x, v) => x.Edge = v);
        Set(w, x => x.LegacyBanner, legacyGroup, (x, v) => x.LegacyBanner = v);
        Set(w, x => x.ShrinkTarget, arrival.Status.rectTransform, (x, v) => x.ShrinkTarget = v);
        Set(w, x => x.ShrinkTargetFirstVisit, Need(main, "Risk"), (x, v) => x.ShrinkTargetFirstVisit = v);
        Set(tray, x => x.Warning, w, (x, v) => x.Warning = v);
        Set(replay, x => x.TimeText, dayText, (x, v) => x.TimeText = v); Set(replay, x => x.Pips, pg, (x, v) => x.Pips = v); Set(replay, x => x.TurnCount, countText, (x, v) => x.TurnCount = v);
        if (!Mathf.Approximately(replay.BannerPop, .6f) || replay.PopFrom != new Vector2(90, 0) || !replay.ShowSweep) { replay.BannerPop = .6f; replay.PopFrom = new Vector2(90, 0); replay.ShowSweep = true; EditorUtility.SetDirty(replay); Changed("clock pop-out"); }
        // Bubbles keep off the strip even while it is hidden (it comes up right after a turn, when the rings fill).
        var markers = main.GetComponentInChildren<FieldPlanTargetMarkers>(true);
        if (markers)
        {
            var always = (markers.KeepClearAlways ?? new RectTransform[0]).Where(r => r).ToList(); int n = always.Count;
            if (!always.Contains((RectTransform)warn)) always.Add((RectTransform)warn);
            if (always.Count != n || markers.KeepClearAlways == null || markers.KeepClearAlways.Length != always.Count) { markers.KeepClearAlways = always.ToArray(); EditorUtility.SetDirty(markers); Changed("keep-clear-always + TurnWarning"); }
            var clear = (markers.KeepClear ?? new RectTransform[0]).Where(r => r).ToList(); n = clear.Count;
            if (!clear.Contains(paper)) clear.Add(paper);
            if (clear.Count != n || markers.KeepClear == null || markers.KeepClear.Length != clear.Count) { markers.KeepClear = clear.ToArray(); EditorUtility.SetDirty(markers); Changed("keep-clear + DayPaper"); }
        }
    }

    // ---- helpers (as BuildExplorationHudTray) ----
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
    static bool Near(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < .0001f;
    static void Place(Transform t, float x, float y, float w, float h, string what)
    {
        var r = (RectTransform)t; var anchor = new Vector2(0, 1); var size = new Vector2(w, h); var pos = new Vector2(x + r.pivot.x * w, -(y + (1 - r.pivot.y) * h));
        if (r.anchorMin == anchor && r.anchorMax == anchor && Near(r.sizeDelta, size) && Near(r.anchoredPosition, pos)) return;
        r.anchorMin = r.anchorMax = anchor; r.sizeDelta = size; r.anchoredPosition = pos; Changed(what + " rect");
    }
    static void Place(Transform t, Rect m, string what) => Place(t, m.x, m.y, m.width, m.height, what);
    static void PlaceIn(Rect parent, Transform t, Rect m, string what) => Place(t, m.x - parent.x, m.y - parent.y, m.width, m.height, what);
    static void Stretch(Transform t)
    {
        var r = (RectTransform)t;
        if (r.anchorMin == Vector2.zero && r.anchorMax == Vector2.one && Near(r.offsetMin, Vector2.zero) && Near(r.offsetMax, Vector2.zero)) return;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, .5f); r.offsetMin = r.offsetMax = Vector2.zero; Changed(t.name + " stretched");
    }
    static void Active(Transform t, bool on, string what) { if (t.gameObject.activeSelf == on) return; t.gameObject.SetActive(on); Changed(what + (on ? " shown" : " hidden")); }
    static void Enabled(Behaviour b, bool on, string what) { if (b.enabled == on) return; b.enabled = on; EditorUtility.SetDirty(b); Changed(what + (on ? " on" : " off")); }
    static void Paper(Image img, string sprite, Color color, string what)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(Art + sprite + ".png") ?? throw new Exception("Missing " + Art + sprite + ".png");
        if (img.sprite == s && img.color == color && img.type == Image.Type.Simple && !img.preserveAspect) return;
        img.sprite = s; img.color = color; img.type = Image.Type.Simple; img.preserveAspect = false; EditorUtility.SetDirty(img); Changed(what + " paper");
    }
    // Single-line HUD text: wrap + truncate, no best fit (rect heights leave room for the font's 1.48 line height).
    static void Style(Text t, int size, Color color, TextAnchor align, string what)
    {
        if (t.font == font && t.fontSize == size && t.color == color && t.alignment == align && t.horizontalOverflow == HorizontalWrapMode.Wrap && t.verticalOverflow == VerticalWrapMode.Truncate
            && !t.resizeTextForBestFit && Mathf.Approximately(t.lineSpacing, 1) && !t.raycastTarget && t.supportRichText) return;
        t.font = font; t.fontSize = size; t.color = color; t.alignment = align; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextForBestFit = false; t.lineSpacing = 1; t.raycastTarget = false; t.supportRichText = true; EditorUtility.SetDirty(t); Changed(what + " text");
    }
    static void Set<TC, TV>(TC owner, Func<TC, TV> get, TV value, Action<TC, TV> set) where TC : Object where TV : Object
    {
        if (get(owner) == value) return; set(owner, value); EditorUtility.SetDirty(owner); Changed(owner.GetType().Name + " → " + (value ? value.name : "null"));
    }
}
