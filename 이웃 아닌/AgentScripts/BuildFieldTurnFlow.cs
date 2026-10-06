using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Live turn flow (2026-09-25, user: '한 번 + 계속 진행', '다음 턴에 이동 예약'): under the arrival panel's Main a 'TurnFlow' node
// with the turn sequence (FieldTurnReplay: banner, '+10분' tick, noise rings), '계속 진행 ▶▶' (FieldAutoAdvance, just above the
// member row on the '턴 진행' column) with its stop stamp, and the move-reservation texts (FieldMoveQueueView).
// Base ExpeditionArrivalPanel.prefab only (SettlementScreen's nested instance inherits the new nodes). In the real screen
// the objects SettlementScreen adds to Main (CorridorSearch_0/1, StorageHotspots, StorageSearch_0/1) come after TurnFlow and
// draw over it: the banner, the stamp and the toggle are placed clear of their markers; a noise ring for those objects starts
// under their 40 px marker (a ring starts at 42 px) and shows once it grows past it.
// Idempotent: nodes are found or created by name; rects are set only when a node is created (tune them in the prefab);
// no existing node is renamed, reordered or re-rected. Run after BuildExplorationRoomPresentation, BuildExplorationHotspots,
// ReviewExplorationPolish.ApplyHint and BuildNpcStory. Temporary layout: there is no approved mock for these elements.
public static class BuildFieldTurnFlow
{
    const string ArrivalPath = "Assets/Prefabs/Settlement/ExpeditionArrivalPanel.prefab";
    const int RippleCount = 8;
    static readonly Color Ink = new Color(.12f, .11f, .09f, 1), BannerPaper = new Color(.95f, .92f, .83f, 1), TickPaper = new Color(1f, .79f, .39f, 1),
        StampPaper = new Color(.97f, .9f, .82f, 1), StampInk = new Color(.58f, .16f, .1f, 1), Ring = new Color(1f, .79f, .39f, .95f);
    static readonly List<string> log = new List<string>();

    public static string Run()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Stop Play and preserve the scene first");
        log.Clear();
        var root = PrefabUtility.LoadPrefabContents(ArrivalPath);
        try
        {
            var arrival = root.GetComponent<ExpeditionArrivalPanel>(); if (!arrival) throw new Exception("ExpeditionArrivalPanel missing");
            var planner = root.GetComponent<FieldTurnPlanner>(); if (!planner || !planner.TurnButton) throw new Exception("Run BuildFieldTurnPlan first (FieldTurnPlanner.TurnButton)");
            var main = arrival.Main.transform;
            var turn = main.Find("TurnAdvance") ?? throw new Exception("Main/TurnAdvance missing (BuildFieldTurnPlan)");
            var style = turn.Find("Title").GetComponent<Text>(); var paperSource = turn.GetComponent<Image>(); var buttonSource = turn.GetComponent<Button>();

            // Container: full Main, no graphic (never takes clicks itself).
            var flow = Child(main, "TurnFlow", out bool made);
            if (made) { var r = (RectTransform)flow; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, .5f); r.offsetMin = r.offsetMax = Vector2.zero; }

            // Banner '<N>턴 · 10분 흐름' at the top centre, right of the route paper (ends x 763) and above every object marker
            // (arcade SPACE 오락기 y 252, storage shelf x 1387): an exploration strip, not the battle banner prefab.
            var banner = Child(flow, "TurnBanner", out made, typeof(Image), typeof(CanvasGroup));
            if (made) { Place(banner, 780, 150, 600, 84); Paper(banner, paperSource, BannerPaper); banner.gameObject.SetActive(false); }
            var bannerGroup = Group(banner);
            var clock = Child(banner, "Clock", out made, typeof(FieldClockGlyph));
            if (made) { Place(clock, 26, 18, 48, 48); clock.GetComponent<FieldClockGlyph>().color = Ink; }
            var title = Child(banner, "Title", out made, typeof(Text));
            if (made) { Place(title, 90, 6, 486, 46); Txt(title, style, 28, "1턴 · 10분 흐름", TextAnchor.MiddleLeft, Ink); }
            var detail = Child(banner, "Detail", out made, typeof(Text));
            if (made) { Place(detail, 90, 48, 486, 32); Txt(detail, style, 20, "소음 +1", TextAnchor.MiddleLeft, new Color(Ink.r, Ink.g, Ink.b, .8f)); }

            // '+10분' beside the clock on the day paper.
            var tick = Child(flow, "TurnTick", out made, typeof(Image), typeof(CanvasGroup));
            if (made) { Place(tick, 208, 100, 72, 32); Paper(tick, paperSource, TickPaper); tick.gameObject.SetActive(false); }
            var tickGroup = Group(tick);
            var tickText = Child(tick, "Text", out made, typeof(Text));
            if (made) { Stretch(tickText); Txt(tickText, style, 20, "+10분", TextAnchor.MiddleCenter, Ink); }

            // Noise rings (positioned on the searched objects at run time).
            var rings = Child(flow, "NoiseRipples", out made);
            if (made) { var r = (RectTransform)rings; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, .5f); r.offsetMin = r.offsetMax = Vector2.zero; }
            var ripples = new FieldNoiseRipple[RippleCount];
            for (int i = 0; i < RippleCount; i++)
            {
                var ring = Child(rings, "Ripple_" + i, out made, typeof(FieldNoiseRipple));
                if (made) { var r = (RectTransform)ring; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.sizeDelta = new Vector2(120, 120); r.anchoredPosition = Vector2.zero; ring.GetComponent<FieldNoiseRipple>().color = Ring; ring.gameObject.SetActive(false); }
                ripples[i] = ring.GetComponent<FieldNoiseRipple>();
            }

            // '계속 진행 ▶▶' right-aligned with '턴 진행', above the member row (the space between the cards and the button is 20 px).
            var toggle = Child(flow, "AutoAdvance", out made, typeof(Image), typeof(Button));
            if (made)
            {
                Place(toggle, 1664, 694, 176, 52); Paper(toggle, paperSource, new Color(.93f, .9f, .8f));
                var b = toggle.GetComponent<Button>(); b.transition = buttonSource.transition; b.colors = buttonSource.colors; b.targetGraphic = toggle.GetComponent<Image>();
                toggle.GetComponent<Image>().raycastTarget = true; toggle.gameObject.SetActive(false);
            }
            var label = Child(toggle, "Label", out made, typeof(Text));
            if (made) { Place(label, 10, 0, 118, 52); Txt(label, style, 22, "계속 진행", TextAnchor.MiddleCenter, Ink, true); }
            var glyph = Child(toggle, "Glyph", out made, typeof(FieldForwardGlyph));
            if (made) { Place(glyph, 132, 16, 30, 20); glyph.GetComponent<FieldForwardGlyph>().color = Ink; }

            // Stop stamp '멈춤 · <이유>' in the top row between the place paper (ends x 628) and the resources (x 1190): clear of the
            // banner, the object markers and the door tags (tilted 2° it spans y 42–120).
            var stamp = Child(flow, "AutoStopStamp", out made, typeof(Image), typeof(CanvasGroup));
            if (made) { Place(stamp, 650, 60, 520, 60); Paper(stamp, paperSource, StampPaper); stamp.localEulerAngles = new Vector3(0, 0, 2); stamp.gameObject.SetActive(false); }
            var stampGroup = Group(stamp);
            var stampText = Child(stamp, "Text", out made, typeof(Text));
            if (made) { Stretch(stampText); Txt(stampText, style, 24, "멈춤 · 맡긴 일이 없습니다", TextAnchor.MiddleCenter, StampInk); }

            // Components on the container (always active: the toggle and the stamp come and go).
            var replay = Component<FieldTurnReplay>(flow);
            Set(replay, x => x.Arrival, arrival, (x, v) => x.Arrival = v);
            Set(replay, x => x.Banner, bannerGroup, (x, v) => x.Banner = v);
            Set(replay, x => x.BannerTitle, title.GetComponent<Text>(), (x, v) => x.BannerTitle = v);
            Set(replay, x => x.BannerDetail, detail.GetComponent<Text>(), (x, v) => x.BannerDetail = v);
            Set(replay, x => x.BannerClock, clock.GetComponent<FieldClockGlyph>(), (x, v) => x.BannerClock = v);
            Set(replay, x => x.Tick, tickGroup, (x, v) => x.Tick = v);
            Set(replay, x => x.TickLabel, tickText.GetComponent<Text>(), (x, v) => x.TickLabel = v);
            if (replay.Ripples == null || !replay.Ripples.SequenceEqual(ripples)) { replay.Ripples = ripples; EditorUtility.SetDirty(replay); log.Add("replay rings"); }
            var auto = Component<FieldAutoAdvance>(flow);
            Set(auto, x => x.Arrival, arrival, (x, v) => x.Arrival = v);
            Set(auto, x => x.Replay, replay, (x, v) => x.Replay = v);
            Set(auto, x => x.Toggle, toggle.GetComponent<Button>(), (x, v) => x.Toggle = v);
            Set(auto, x => x.Paper, toggle.GetComponent<Image>(), (x, v) => x.Paper = v);
            Set(auto, x => x.Label, label.GetComponent<Text>(), (x, v) => x.Label = v);
            Set(auto, x => x.Glyph, glyph.GetComponent<FieldForwardGlyph>(), (x, v) => x.Glyph = v);
            Set(auto, x => x.Stamp, stampGroup, (x, v) => x.Stamp = v);
            Set(auto, x => x.StampLabel, stampText.GetComponent<Text>(), (x, v) => x.StampLabel = v);
            var view = Component<FieldMoveQueueView>(flow);
            Set(view, x => x.Arrival, arrival, (x, v) => x.Arrival = v);
            if (log.Count > 0) PrefabUtility.SaveAsPrefabAsset(root, ArrivalPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return log.Count == 0 ? "Already applied." : "Applied: " + string.Join("; ", log);
    }

    static Transform Child(Transform parent, string name, out bool made, params Type[] components)
    {
        var t = parent.Find(name); made = !t; if (t) return t;
        var types = new List<Type> { typeof(RectTransform) };
        if (components.Any(c => typeof(Graphic).IsAssignableFrom(c))) types.Add(typeof(CanvasRenderer));
        types.AddRange(components);
        var g = new GameObject(name, types.ToArray()); g.transform.SetParent(parent, false); g.layer = parent.gameObject.layer;
        log.Add(parent.name + "/" + name + " created");
        return g.transform;
    }
    static T Component<T>(Transform t) where T : Component { var c = t.GetComponent<T>(); if (!c) { c = t.gameObject.AddComponent<T>(); log.Add(t.name + " +" + typeof(T).Name); } return c; }
    static void Set<TC, TV>(TC owner, Func<TC, TV> get, TV value, Action<TC, TV> set) where TC : Object where TV : Object
    {
        if (get(owner) == value) return; set(owner, value); EditorUtility.SetDirty(owner); log.Add(owner.GetType().Name + " → " + (value ? value.name : "null"));
    }
    static CanvasGroup Group(Transform t)
    {
        var g = t.GetComponent<CanvasGroup>(); if (!g) { g = t.gameObject.AddComponent<CanvasGroup>(); log.Add(t.name + " +CanvasGroup"); }
        if (g.interactable || g.blocksRaycasts) { g.interactable = g.blocksRaycasts = false; g.alpha = 0; log.Add(t.name + " passes clicks"); }
        return g;
    }
    // Top-left placement in Main space (1920×1080, y down) or in the parent's space for children.
    static void Place(Transform t, float x, float y, float w, float h)
    {
        var r = (RectTransform)t; r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
    }
    static void Stretch(Transform t) { var r = (RectTransform)t; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, .5f); r.offsetMin = new Vector2(6, 0); r.offsetMax = new Vector2(-6, 0); }
    // The same paper as the '턴 진행' button, tinted.
    static void Paper(Transform t, Image source, Color color)
    {
        var img = t.GetComponent<Image>(); img.sprite = source ? source.sprite : null; img.type = source ? source.type : Image.Type.Simple;
        img.pixelsPerUnitMultiplier = source ? source.pixelsPerUnitMultiplier : 1; img.color = color; img.raycastTarget = false;
    }
    // Single-line labels: rect heights leave room for the font's 1.48 line height, so they fit without best-fit.
    static void Txt(Transform t, Text style, int size, string text, TextAnchor align, Color color, bool overflow = false)
    {
        var x = t.GetComponent<Text>(); x.font = style.font; x.fontStyle = style.fontStyle; x.fontSize = size; x.alignment = align; x.color = color; x.text = text;
        x.lineSpacing = 1; x.supportRichText = true; x.raycastTarget = false; x.resizeTextForBestFit = false;
        x.horizontalOverflow = overflow ? HorizontalWrapMode.Overflow : HorizontalWrapMode.Wrap; x.verticalOverflow = VerticalWrapMode.Truncate;
    }
}
