using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Member action slot + quick assignment (2026-09-25, 기획/탐험-화면정리와-행동칸-1차.md · approved mocks 탐험-화면정리-시안/01~03):
// 1. FieldMemberCard.prefab (the arrival cards only): 'ActionSlot' anchored to the card bottom and stretched across (token circle,
//    glyph, two lines; inactive until the site board), 'SelectFrame' (gold outline, inactive) and 'SelectHit' (a transparent
//    input layer over the whole card, its last child; raycast off until the board turns it on).
// 2. ExpeditionArrivalPanel.prefab Main/'MemberActions' (the last child of Main): FieldMemberActionSlots + FieldQuickAssign with
//    the room veil (above the member tray: its bottom follows the plan markers' RoomArea), a pool of bubble press areas, a pool of
//    glowing target pins, the chosen-member hint and the drag portrait. Nothing goes under the hotspot buttons.
// SettlementScreen's nested arrival panel inherits both (checked, not edited). Idempotent: nodes are found or created by name,
// existing nodes keep their rects and Inspector values; prints what it changed or 'Already applied.'.
// Run AFTER every other builder (BuildAssignmentBubble → BuildFieldTurnFlow → BuildFieldPlanMarkers, the HUD tray/turn clock
// builders and BuildIdleConfirm), because the veil reads the markers' RoomArea and MemberActions must stay Main's last child
// (only Main/SearchNote may follow it: BuildSearchNote.Run, run after this one).
public static class BuildMemberActionSlot
{
    const string P = "Assets/Prefabs/Settlement/", CardPath = P + "FieldMemberCard.prefab", ArrivalPath = P + "ExpeditionArrivalPanel.prefab", ScreenPath = P + "SettlementScreen.prefab";
    const string Art = "Assets/Art/PartySelection/", FontPath = "Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf", PortraitMaterial = "Assets/Settings/PaperPortrait.mat";
    public const string NodeName = "MemberActions", NoteNode = "SearchNote";
    public const int GlowPool = 8, HitPool = 6;
    const float DiscY = 58; // pin head centre above the tail tip (MapPinGraphic 72×94 placed 3 px below the tip)
    static readonly Color Ink = new Color(.06f, .07f, .07f, 1), Paper = new Color(.98f, .95f, .86f, 1), Gold = new Color(.96f, .75f, .28f, 1), Cream = new Color(.95f, .91f, .8f, 1), Muted = new Color(.62f, .62f, .57f, 1);
    static readonly Vector2 Middle = new Vector2(.5f, .5f), Bottom = new Vector2(.5f, 0);
    static readonly List<string> log = new List<string>();

    public static string Run()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Stop Play and preserve the scene first");
        log.Clear(); string scene = EditorSceneManager.GetActiveScene().path;
        try { Card(); Arrival(); CheckScreen(); AssetDatabase.SaveAssets(); }
        finally
        {
            // New nodes are made in the active scene before they move into a prefab: drop that side effect (the scene was clean).
            if (EditorSceneManager.GetActiveScene().isDirty && !string.IsNullOrEmpty(scene)) { EditorSceneManager.OpenScene(scene); log.Add("장면 다시 열기(임시 노드 정리)"); }
        }
        return log.Count == 0 ? "Already applied." : "Applied: " + string.Join("; ", log);
    }

    // ---- FieldMemberCard.prefab ----
    static void Card()
    {
        var root = PrefabUtility.LoadPrefabContents(CardPath); int before = log.Count;
        try
        {
            var card = root.GetComponent<ExpeditionMemberCard>() ?? throw new Exception("FieldMemberCard has no ExpeditionMemberCard (run BuildFieldTurnPlan.Run)");
            var font = card.Name ? card.Name.font : Required<Font>(FontPath); var paper = Required<Sprite>(Art + "count-paper.png");

            var slotT = Node(root.transform, "ActionSlot", out bool made, typeof(Image));
            if (made)
            {
                var r = (RectTransform)slotT; r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(1, 0); r.pivot = Bottom; r.offsetMin = new Vector2(10, 10); r.offsetMax = new Vector2(-10, 66);
                var im = slotT.GetComponent<Image>(); im.sprite = paper; im.color = new Color(1f, .95f, .91f, 1); im.raycastTarget = false;
                slotT.gameObject.SetActive(false);
            }
            var slot = Comp<FieldMemberActionSlot>(slotT.gameObject);
            var frame = Node(slotT, "Frame", out made, typeof(FieldFrameGraphic));
            if (made) { Stretch(frame, 0); var g = frame.GetComponent<FieldFrameGraphic>(); g.Thickness = 3; g.color = new Color(.78f, .24f, .19f, 1); }
            var token = Node(slotT, "Token", out made);
            if (made) Place(token, new Vector2(0, .5f), Middle, new Vector2(27, 0), new Vector2(38, 38));
            var fill = Node(token, "Fill", out made, typeof(SegmentRingGraphic));
            if (made) { Stretch(fill, 0); var g = fill.GetComponent<SegmentRingGraphic>(); g.Segments = 0; g.Thickness = 999; g.color = new Color(0, 0, 0, 0); }
            var ring = Node(token, "Ring", out made, typeof(SegmentRingGraphic));
            if (made) { Stretch(ring, 0); var g = ring.GetComponent<SegmentRingGraphic>(); g.Segments = 12; g.Thickness = 2.5f; g.Done = g.Next = 0; g.GapDegrees = 13; g.RestColor = Color.white; g.color = new Color(.78f, .24f, .19f, 1); }
            var glyph = Node(token, "Glyph", out made, typeof(ActionGlyph));
            if (made) { Place(glyph, Middle, Middle, Vector2.zero, new Vector2(22, 22)); glyph.GetComponent<ActionGlyph>().color = Ink; glyph.gameObject.SetActive(false); }
            var title = Node(slotT, "Title", out made, typeof(Text));
            if (made) { Band(title, 52, 8, 5, 26); Style(title.GetComponent<Text>(), font, 20, 14, new Color(.74f, .2f, .15f, 1), TextAnchor.MiddleLeft, "행동 남음"); }
            var detail = Node(slotT, "Detail", out made, typeof(Text));
            if (made) { Band(detail, 52, 8, 31, 20); Style(detail.GetComponent<Text>(), font, 15, 11, new Color(.16f, .14f, .12f, 1), TextAnchor.MiddleLeft, "누르고 사물을 고르세요"); }

            var select = Node(root.transform, "SelectFrame", out made, typeof(FieldFrameGraphic));
            if (made) { Stretch(select, 0); var g = select.GetComponent<FieldFrameGraphic>(); g.Thickness = 4; g.color = Gold; select.gameObject.SetActive(false); }
            var hitT = Node(root.transform, "SelectHit", out made, typeof(Image), typeof(FieldMemberCardInput));
            if (made)
            {
                Stretch(hitT, 0); var im = hitT.GetComponent<Image>(); im.color = new Color(0, 0, 0, 0); im.raycastTarget = false;
                hitT.GetComponent<CanvasRenderer>().cullTransparentMesh = true;
            }
            // The outline over everything but the input; the input on top of the whole card.
            if (select.GetSiblingIndex() != root.transform.childCount - 2 || hitT.GetSiblingIndex() != root.transform.childCount - 1) { select.SetAsLastSibling(); hitT.SetAsLastSibling(); log.Add("카드 선택 테두리 · 입력 면 맨 위로"); }
            var input = hitT.GetComponent<FieldMemberCardInput>();

            Wire(ref slot.Card, card, "slot.Card"); Wire(ref slot.Background, (Graphic)slotT.GetComponent<Image>(), "slot.Background"); Wire(ref slot.Frame, frame.GetComponent<FieldFrameGraphic>(), "slot.Frame");
            Wire(ref slot.TokenFill, fill.GetComponent<SegmentRingGraphic>(), "slot.TokenFill"); Wire(ref slot.TokenRing, ring.GetComponent<SegmentRingGraphic>(), "slot.TokenRing"); Wire(ref slot.Glyph, glyph.GetComponent<ActionGlyph>(), "slot.Glyph");
            Wire(ref slot.Title, title.GetComponent<Text>(), "slot.Title"); Wire(ref slot.Detail, detail.GetComponent<Text>(), "slot.Detail");
            Wire(ref slot.SelectFrame, select.GetComponent<FieldFrameGraphic>(), "slot.SelectFrame"); Wire(ref slot.Input, input, "slot.Input");
            Wire(ref input.Card, card, "input.Card"); Wire(ref input.Slot, slot, "input.Slot"); Wire(ref input.Hit, (Graphic)hitT.GetComponent<Image>(), "input.Hit");
            // The bag line keeps opening the bag on the board.
            var zones = new List<RectTransform>(); var statePaper = root.transform.Find("StatePaper") as RectTransform; if (statePaper) zones.Add(statePaper); if (card.State) zones.Add(card.State.rectTransform);
            if (input.BagZones == null || input.BagZones.Length == 0) { input.BagZones = zones.ToArray(); log.Add("가방 줄 " + zones.Count + "곳"); }
            if (log.Count > before) { EditorUtility.SetDirty(slot); EditorUtility.SetDirty(input); PrefabUtility.SaveAsPrefabAsset(root, CardPath); }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ---- ExpeditionArrivalPanel.prefab Main/MemberActions ----
    static void Arrival()
    {
        var root = PrefabUtility.LoadPrefabContents(ArrivalPath); int before = log.Count;
        try
        {
            var arrival = root.GetComponent<ExpeditionArrivalPanel>(); if (!arrival || !arrival.Main) throw new Exception("ExpeditionArrivalPanel/Main missing");
            if (!arrival.Threat || !arrival.Threat.Planner) throw new Exception("Run BuildFieldTurnPlan.Run first (planner)");
            var main = arrival.Main.transform;
            var markers = main.Find(BuildMarkersNode)?.GetComponent<FieldPlanTargetMarkers>() ?? throw new Exception("Run BuildFieldPlanMarkers.Run first (Main/" + BuildMarkersNode + ")");
            var font = Required<Font>(FontPath); var portraitMat = Required<Material>(PortraitMaterial);

            var node = main.Find(NodeName);
            if (!node) { var g = new GameObject(NodeName, typeof(RectTransform)); g.layer = main.gameObject.layer; g.transform.SetParent(main, false); node = g.transform; Stretch(node, 0); log.Add("Main/" + NodeName + " 생성"); }
            // Over everything in Main: the veil takes the room's presses while a member is chosen, the dragged portrait crosses the tray.
            // Only the search note (BuildSearchNote.Run) stays above it.
            var note = main.Find(NoteNode);
            if (note ? node.GetSiblingIndex() != main.childCount - 2 || note.GetSiblingIndex() != main.childCount - 1 : node.GetSiblingIndex() != main.childCount - 1)
            { node.SetAsLastSibling(); if (note) note.SetAsLastSibling(); log.Add(NodeName + " Main 맨 위로" + (note ? " (" + NoteNode + " 아래)" : "")); }
            var slots = Comp<FieldMemberActionSlots>(node.gameObject); var quick = Comp<FieldQuickAssign>(node.gameObject);

            var veil = Node(node, "Veil", out bool made, typeof(Image));
            if (made) { var im = veil.GetComponent<Image>(); im.color = quick.VeilColor; im.raycastTarget = true; veil.gameObject.SetActive(false); }
            {
                // Full width, from the top down to the plan markers' room area bottom (above the member tray): edit RoomArea there.
                float bottom = Mathf.Clamp(markers.RoomArea.yMax + 6, 200, 1080);
                var r = (RectTransform)veil; var want = (min: new Vector2(0, -bottom), max: Vector2.zero);
                if (r.anchorMin != new Vector2(0, 1) || r.anchorMax != Vector2.one || r.pivot != new Vector2(.5f, 1) || r.offsetMin != want.min || r.offsetMax != want.max)
                { r.anchorMin = new Vector2(0, 1); r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, 1); r.offsetMin = want.min; r.offsetMax = want.max; log.Add("방 가림: 위 ~ y" + bottom + " (표식 방 영역 아래)"); }
            }
            var hitsT = Node(node, "BubbleHits", out made); if (made) Stretch(hitsT, 0);
            var hitList = new List<Graphic>();
            for (int i = 0; i < HitPool; i++)
            {
                var h = Node(hitsT, "Hit_" + i, out made, typeof(Image));
                if (made)
                {
                    Place(h, Middle, Middle, Vector2.zero, new Vector2(70, 70)); var im = h.GetComponent<Image>(); im.color = new Color(0, 0, 0, 0); im.raycastTarget = true;
                    h.GetComponent<CanvasRenderer>().cullTransparentMesh = true; h.gameObject.SetActive(false);
                }
                hitList.Add(h.GetComponent<Image>());
            }
            var glowsT = Node(node, "Glows", out made); if (made) Stretch(glowsT, 0);
            var glowList = new List<FieldTargetGlow>(); for (int i = 0; i < GlowPool; i++) glowList.Add(Glow(glowsT, "Glow_" + i));
            var hint = Hint(node, font, out var who, out var where, out var how, out var cancel);
            var token = Token(node, portraitMat, out var portrait);
            // Creation order inside the node: veil, bubble presses, glows, hint, dragged portrait (on top).
            var present = new[] { "Veil", "BubbleHits", "Glows", "SelectHint", "DragToken" }.Select(n => node.Find(n)).Where(k => k).ToList();
            bool sorted = true; for (int i = 1; i < present.Count; i++) if (present[i - 1].GetSiblingIndex() > present[i].GetSiblingIndex()) sorted = false;
            if (!sorted) { foreach (var k in present) k.SetAsLastSibling(); log.Add(NodeName + " 자식 순서"); }

            Wire(ref slots.Arrival, arrival, "slots.Arrival"); Wire(ref slots.Quick, quick, "slots.Quick");
            Wire(ref quick.Arrival, arrival, "quick.Arrival"); Wire(ref quick.Markers, markers, "quick.Markers"); Wire(ref quick.Slots, slots, "quick.Slots");
            Wire(ref quick.Veil, veil.GetComponent<Image>(), "quick.Veil");
            var keptHits = (quick.BubbleHits ?? new Graphic[0]).Where(h => h && !hitList.Contains(h)); var allHits = hitList.Concat(keptHits).ToArray();
            if (quick.BubbleHits == null || !quick.BubbleHits.SequenceEqual(allHits)) { quick.BubbleHits = allHits; log.Add("말풍선 누름 면 " + allHits.Length); }
            var keptGlows = (quick.Glows ?? new FieldTargetGlow[0]).Where(g => g && !glowList.Contains(g)); var allGlows = glowList.Concat(keptGlows).ToArray();
            if (quick.Glows == null || !quick.Glows.SequenceEqual(allGlows)) { quick.Glows = allGlows; log.Add("빛나는 곳 " + allGlows.Length); }
            Wire(ref quick.Hint, (RectTransform)hint, "quick.Hint"); Wire(ref quick.HintWho, who, "quick.HintWho"); Wire(ref quick.HintWhere, where, "quick.HintWhere"); Wire(ref quick.HintHow, how, "quick.HintHow"); Wire(ref quick.HintCancel, cancel, "quick.HintCancel");
            Wire(ref quick.DragToken, (RectTransform)token, "quick.DragToken"); Wire(ref quick.DragPortrait, portrait, "quick.DragPortrait");
            if (log.Count > before) { EditorUtility.SetDirty(slots); EditorUtility.SetDirty(quick); PrefabUtility.SaveAsPrefabAsset(root, ArrivalPath); }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    const string BuildMarkersNode = "PlanTargetMarkers";

    // A glowing target pin: gold pin, paper disc, glyph, a pulsing ring; root pivot = the tail tip.
    static FieldTargetGlow Glow(Transform parent, string name)
    {
        var t = Node(parent, name, out bool made, typeof(FieldTargetGlow));
        if (made) { Place(t, Middle, Bottom, Vector2.zero, new Vector2(88, 110)); t.gameObject.SetActive(false); }
        var halo = Node(t, "Halo", out made, typeof(SegmentRingGraphic));
        if (made) { Place(halo, Bottom, Middle, new Vector2(0, DiscY), new Vector2(84, 84)); var g = halo.GetComponent<SegmentRingGraphic>(); g.Segments = 0; g.Thickness = 5; g.color = Gold; }
        var visual = Node(t, "Visual", out made); if (made) Place(visual, Bottom, Bottom, Vector2.zero, new Vector2(88, 110));
        var pin = Node(visual, "Pin", out made, typeof(MapPinGraphic));
        if (made) { Place(pin, Bottom, Bottom, new Vector2(0, -3), new Vector2(72, 94)); var g = pin.GetComponent<MapPinGraphic>(); g.color = Gold; g.raycastTarget = false; }
        var disc = Node(visual, "Disc", out made, typeof(SegmentRingGraphic));
        if (made) { Place(disc, Bottom, Middle, new Vector2(0, DiscY), new Vector2(52, 52)); var g = disc.GetComponent<SegmentRingGraphic>(); g.Segments = 0; g.Thickness = 999; g.color = Paper; }
        var glyph = Node(visual, "Glyph", out made, typeof(ActionGlyph));
        if (made) { Place(glyph, Bottom, Middle, new Vector2(0, DiscY), new Vector2(30, 30)); glyph.GetComponent<ActionGlyph>().color = Ink; }
        var hit = Node(t, "Hit", out made); if (made) Place(hit, Bottom, Bottom, Vector2.zero, new Vector2(76, 100));
        var c = t.GetComponent<FieldTargetGlow>();
        Wire(ref c.Visual, (RectTransform)visual, name + ".Visual"); Wire(ref c.Pin, (Graphic)pin.GetComponent<MapPinGraphic>(), name + ".Pin"); Wire(ref c.Disc, (Graphic)disc.GetComponent<SegmentRingGraphic>(), name + ".Disc");
        Wire(ref c.Glyph, glyph.GetComponent<ActionGlyph>(), name + ".Glyph"); Wire(ref c.Halo, halo.GetComponent<SegmentRingGraphic>(), name + ".Halo"); Wire(ref c.HitRect, (RectTransform)hit, name + ".HitRect");
        return c;
    }
    // '<이름>은 / 어디로? / 빛나는 곳을 누르세요 / 우클릭 · 취소' (시안 02).
    static Transform Hint(Transform parent, Font font, out Text who, out Text where, out Text how, out Text cancel)
    {
        var t = Node(parent, "SelectHint", out bool made, typeof(Image));
        if (made)
        {
            Place(t, Middle, Middle, Vector2.zero, new Vector2(250, 205));
            var im = t.GetComponent<Image>(); im.color = new Color(.12f, .16f, .16f, .96f); im.raycastTarget = false; t.gameObject.SetActive(false);
        }
        var frame = Node(t, "Frame", out made, typeof(FieldFrameGraphic)); if (made) { Stretch(frame, 0); var g = frame.GetComponent<FieldFrameGraphic>(); g.Thickness = 3; g.color = Gold; }
        who = HintText(t, "Who", 20, 30, 22, Cream, "한해인은", font);
        where = HintText(t, "Where", 52, 44, 34, Gold, "어디로?", font);
        how = HintText(t, "How", 100, 58, 20, Cream, "빛나는 곳을\n누르세요", font);
        cancel = HintText(t, "Cancel", 166, 24, 16, Muted, "우클릭 · 취소", font);
        return t;
    }
    static Text HintText(Transform parent, string name, float top, float height, int size, Color color, string text, Font font)
    {
        var t = Node(parent, name, out bool made, typeof(Text));
        if (made) { Band(t, 12, 12, top, height); Style(t.GetComponent<Text>(), font, size, Mathf.Max(12, size - 6), color, TextAnchor.MiddleCenter, text); }
        return t.GetComponent<Text>();
    }
    // The portrait that follows the pointer while a card is dragged.
    static Transform Token(Transform parent, Material portraitMat, out Image portrait)
    {
        var t = Node(parent, "DragToken", out bool made, typeof(CanvasGroup));
        if (made) { Place(t, Middle, Middle, Vector2.zero, new Vector2(80, 80)); var g = t.GetComponent<CanvasGroup>(); g.interactable = g.blocksRaycasts = false; t.gameObject.SetActive(false); }
        var ring = Node(t, "Ring", out made, typeof(SegmentRingGraphic));
        if (made) { Place(ring, Middle, Middle, Vector2.zero, new Vector2(80, 80)); var g = ring.GetComponent<SegmentRingGraphic>(); g.Segments = 0; g.Thickness = 5; g.color = Gold; }
        var disc = Node(t, "Disc", out made, typeof(SegmentRingGraphic), typeof(Mask));
        if (made) { Place(disc, Middle, Middle, Vector2.zero, new Vector2(68, 68)); var g = disc.GetComponent<SegmentRingGraphic>(); g.Segments = 0; g.Thickness = 999; g.color = Paper; disc.GetComponent<Mask>().showMaskGraphic = true; }
        var p = Node(disc, "Portrait", out made, typeof(Image));
        portrait = p.GetComponent<Image>();
        if (made) { Place(p, Middle, Middle, new Vector2(0, -5), new Vector2(64, 64)); portrait.preserveAspect = true; portrait.raycastTarget = false; }
        var style = p.GetComponent<PaperPortraitStyle>(); if (!style) { style = p.gameObject.AddComponent<PaperPortraitStyle>(); log.Add("끌기 초상 종이 재질"); }
        if (style.StyleMaterial != portraitMat) style.Configure(portraitMat);
        return t;
    }

    // SettlementScreen nests the arrival panel: it must use the card variant edited here (no override of MemberPrefab).
    static void CheckScreen()
    {
        var screen = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath); if (!screen) return;
        var card = AssetDatabase.LoadAssetAtPath<GameObject>(CardPath)?.GetComponent<ExpeditionMemberCard>();
        foreach (var a in screen.GetComponentsInChildren<ExpeditionArrivalPanel>(true))
        {
            if (a.MemberPrefab != card) log.Add("경고: SettlementScreen의 도착 화면 카드가 FieldMemberCard가 아님 (" + (a.MemberPrefab ? a.MemberPrefab.name : "없음") + ")");
            var node = a.Main ? a.Main.transform.Find(NodeName) : null; if (!node) log.Add("경고: SettlementScreen 안 Main/" + NodeName + " 없음");
        }
    }

    // ---- helpers ----
    static T Required<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new Exception("Missing asset: " + path);
    static T Comp<T>(GameObject g) where T : Component { var c = g.GetComponent<T>(); if (c) return c; c = g.AddComponent<T>(); log.Add(g.name + " +" + typeof(T).Name); return c; }
    static Transform Node(Transform parent, string name, out bool made, params Type[] components)
    {
        var t = parent.Find(name); made = !t; if (t) return t;
        var g = new GameObject(name, new[] { typeof(RectTransform) }.Concat(components).ToArray()); g.layer = parent.gameObject.layer; g.transform.SetParent(parent, false);
        log.Add(parent.name + "/" + name + " 생성"); return g.transform;
    }
    static void Place(Transform t, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size) { var r = (RectTransform)t; r.anchorMin = r.anchorMax = anchor; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size; }
    static void Stretch(Transform t, float inset) { var r = (RectTransform)t; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = Middle; r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset); }
    // Stretched across the parent (left/right margins), placed by its top edge.
    static void Band(Transform t, float left, float right, float top, float height) { var r = (RectTransform)t; r.anchorMin = new Vector2(0, 1); r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, 1); r.offsetMin = new Vector2(left, -(top + height)); r.offsetMax = new Vector2(-right, -top); }
    static void Style(Text t, Font font, int size, int min, Color color, TextAnchor align, string text)
    {
        t.font = font; t.fontSize = size; t.color = color; t.alignment = align; t.text = text; t.raycastTarget = false; t.supportRichText = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate; t.resizeTextForBestFit = true; t.resizeTextMinSize = min; t.resizeTextMaxSize = size; t.lineSpacing = 1;
    }
    static void Wire<T>(ref T field, T value, string what) where T : Object { if (field == value) return; field = value; log.Add(what + " 연결"); }
}
