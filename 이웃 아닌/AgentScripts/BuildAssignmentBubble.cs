using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Assignment markers 1차 (2026-09-25): a shared pin bubble (AssignmentBubble.prefab: code-drawn pin, segmented ring, up to 3 portraits,
// action badge, label strip) and the settlement facility markers under SettlementScreen/Main/AssignmentMarkers. The old head markers
// (WorkBubble_0/1) are deactivated here; the pawn WorkBadge is hidden at runtime by SettlementAssignmentMarkers.HideHeadBadges.
// Temporary layout from planning mockups, no approved UI mock. Idempotent: finds nodes by name, never re-rects or reorders existing
// nodes, keeps Inspector offsets; prints what it changed or 'Already applied.'. Run after the other settlement/exploration builders.
public static class BuildAssignmentBubble
{
    const string BubblePath = "Assets/Prefabs/Settlement/AssignmentBubble.prefab", ScreenPath = "Assets/Prefabs/Settlement/SettlementScreen.prefab";
    const string Art = "Assets/Art/PartySelection/", FontPath = "Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf", PortraitMaterial = "Assets/Settings/PaperPortrait.mat";
    static readonly Color Ink = new Color(.055f, .075f, .07f, 1), Paper = new Color(.94f, .89f, .77f, 1), Accent = new Color(.95f, .64f, .24f, 1);
    static readonly Vector2 Bottom = new Vector2(.5f, 0), Middle = new Vector2(.5f, .5f);
    const float DiscY = 69; // disc centre above the tail tip (MapPinGraphic 88×114 placed 4 px below the tip)
    static readonly List<string> log = new List<string>();

    public static string Run()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Stop Play and preserve the scene first");
        log.Clear(); string scene = EditorSceneManager.GetActiveScene().path;
        try
        {
            var asset = Bubble();
            Markers(asset);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            // New nodes are made in the active scene before they move into a prefab. The scene was clean above, so reopening it only
            // drops that side effect and keeps the dirty-scene guard of the later builders (and of this one) passing.
            if (EditorSceneManager.GetActiveScene().isDirty && !string.IsNullOrEmpty(scene)) { EditorSceneManager.OpenScene(scene); log.Add("장면 다시 열기(임시 노드 정리)"); }
        }
        return log.Count == 0 ? "Already applied." : "Applied: " + string.Join("; ", log);
    }

    // ---- AssignmentBubble.prefab ----
    static GameObject Bubble()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BubblePath); bool fresh = !existing;
        var root = fresh ? new GameObject("AssignmentBubble", typeof(RectTransform)) : PrefabUtility.LoadPrefabContents(BubblePath);
        int before = log.Count;
        try
        {
            if (fresh) { root.layer = LayerMask.NameToLayer("UI"); Place(root.transform, Middle, Bottom, Vector2.zero, new Vector2(110, 160)); log.Add("AssignmentBubble.prefab 생성"); }
            var b = Comp<AssignmentBubble>(root);
            Comp<CanvasGroup>(root, g => { g.interactable = false; g.blocksRaycasts = false; });
            var font = Required<Font>(FontPath); var strip = Required<Sprite>(Art + "count-paper.png"); var portraitMat = Required<Material>(PortraitMaterial);

            var visual = Node(root.transform, "Visual", out bool made); if (made) Place(visual, Bottom, Bottom, Vector2.zero, new Vector2(110, 160));
            // Pin first: the ring (band 42–50 px, outside the 38 px pin head) then draws over the tail instead of under it.
            var pin = Node(visual, "Pin", out made, typeof(MapPinGraphic));
            if (made) { Place(pin, Bottom, Bottom, new Vector2(0, -4), new Vector2(88, 114)); var g = pin.GetComponent<MapPinGraphic>(); g.color = Ink; g.raycastTarget = false; }
            var ring = Node(visual, "Ring", out made, typeof(SegmentRingGraphic)); if (made) Place(ring, Bottom, Middle, new Vector2(0, DiscY), new Vector2(100, 100));
            var disc = Node(visual, "Disc", out made, typeof(SegmentRingGraphic), typeof(Mask));
            if (made) { Place(disc, Bottom, Middle, new Vector2(0, DiscY), new Vector2(66, 66)); var g = disc.GetComponent<SegmentRingGraphic>(); g.Segments = 0; g.Thickness = 999; g.color = Paper; disc.GetComponent<Mask>().showMaskGraphic = true; }
            // Portrait_0 (the lead) is the last child, so it overlaps the others.
            var portraits = new Image[3];
            for (int i = 2; i >= 0; i--)
            {
                var p = Node(disc, "Portrait_" + i, out made, typeof(Image));
                var im = p.GetComponent<Image>();
                if (made) { Place(p, Middle, Middle, new Vector2(0, -5), new Vector2(62, 62)); im.preserveAspect = true; im.raycastTarget = false; p.gameObject.SetActive(false); }
                var style = p.GetComponent<PaperPortraitStyle>(); if (!style) { style = p.gameObject.AddComponent<PaperPortraitStyle>(); log.Add(p.name + " 종이 초상 재질"); }
                if (style.StyleMaterial != portraitMat) style.Configure(portraitMat);
                portraits[i] = im;
            }
            // Lower right, just outside the ring (centre 68 px from the disc centre > 50 + 16), clear of the tail.
            var badge = Node(visual, "Badge", out made); if (made) Place(badge, Bottom, Middle, new Vector2(56, DiscY - 39), new Vector2(32, 32));
            var rim = Node(badge, "Rim", out made, typeof(SegmentRingGraphic)); if (made) { Place(rim, Middle, Middle, Vector2.zero, new Vector2(32, 32)); var g = rim.GetComponent<SegmentRingGraphic>(); g.Segments = 0; g.Thickness = 999; g.color = Ink; }
            var fill = Node(badge, "Fill", out made, typeof(SegmentRingGraphic)); if (made) { Place(fill, Middle, Middle, Vector2.zero, new Vector2(26, 26)); var g = fill.GetComponent<SegmentRingGraphic>(); g.Segments = 0; g.Thickness = 999; g.color = Accent; }
            var glyph = Node(badge, "Glyph", out made, typeof(ActionGlyph)); if (made) { Place(glyph, Middle, Middle, Vector2.zero, new Vector2(18, 18)); glyph.GetComponent<ActionGlyph>().color = Ink; }
            var label = Node(visual, "Label", out made, typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            if (made)
            {
                Place(label, Bottom, Bottom, new Vector2(0, 124), new Vector2(180, 34));
                var im = label.GetComponent<Image>(); im.sprite = strip; im.raycastTarget = false;
                var h = label.GetComponent<HorizontalLayoutGroup>(); h.padding = new RectOffset(0, 12, 0, 0); h.spacing = 8; h.childAlignment = TextAnchor.MiddleLeft;
                h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = true;
                label.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                label.gameObject.SetActive(false);
            }
            // Sliced: a Simple image reports the sprite's full width as its preferred width, and the size fitter would take that over the text.
            { var im = label.GetComponent<Image>(); if (im.type != Image.Type.Sliced) { im.type = Image.Type.Sliced; log.Add("이름표 종이 늘림(글 폭에 맞춤)"); } }
            var accent = Node(label, "Accent", out made, typeof(Image), typeof(LayoutElement));
            if (made) { var im = accent.GetComponent<Image>(); im.color = Accent; im.raycastTarget = false; var le = accent.GetComponent<LayoutElement>(); le.minWidth = le.preferredWidth = 6; }
            var text = Node(label, "Text", out made, typeof(Text));
            if (made)
            {
                var t = text.GetComponent<Text>(); t.font = font; t.fontSize = 22; t.color = Ink; t.alignment = TextAnchor.MiddleLeft; t.text = "수색 · 1/3 → 2/3";
                t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.supportRichText = false; t.raycastTarget = false;
            }

            Wire(ref b.Visual, (RectTransform)visual, "Visual"); Wire(ref b.Ring, ring.GetComponent<SegmentRingGraphic>(), "Ring");
            if (b.Portraits == null || !b.Portraits.SequenceEqual(portraits)) { b.Portraits = portraits; log.Add("초상 3칸 연결"); }
            Wire(ref b.Badge, badge.gameObject, "Badge"); Wire(ref b.BadgeFill, (Graphic)fill.GetComponent<SegmentRingGraphic>(), "BadgeFill"); Wire(ref b.Glyph, glyph.GetComponent<ActionGlyph>(), "Glyph");
            Wire(ref b.LabelRoot, label.gameObject, "LabelRoot"); Wire(ref b.Label, text.GetComponent<Text>(), "Label"); Wire(ref b.LabelAccent, (Graphic)accent.GetComponent<Image>(), "LabelAccent");
            if (log.Count > before || fresh) { EditorUtility.SetDirty(b); PrefabUtility.SaveAsPrefabAsset(root, BubblePath); }
        }
        finally { if (fresh) Object.DestroyImmediate(root); else PrefabUtility.UnloadPrefabContents(root); }
        return AssetDatabase.LoadAssetAtPath<GameObject>(BubblePath) ?? throw new Exception("AssignmentBubble.prefab missing after save");
    }

    // ---- SettlementScreen/Main/AssignmentMarkers ----
    static readonly (SettlementAssignmentMarkers.Place place, Vector2 offset)[] Defaults = {
        // Bed: over the right bed, clear of the '시간 진행' button and of the resting piece's head.
        (SettlementAssignmentMarkers.Place.Bed, new Vector2(60, -10)), (SettlementAssignmentMarkers.Place.Stock, new Vector2(0, -8)),
        (SettlementAssignmentMarkers.Place.Workbench, new Vector2(0, -20)),
        // Floor props: a working piece stands in front of them, so the tail goes to the right end, beside its head.
        (SettlementAssignmentMarkers.Place.Cabinet, new Vector2(90, 0)), (SettlementAssignmentMarkers.Place.Research, new Vector2(100, 10)) };

    static void Markers(GameObject asset)
    {
        var root = PrefabUtility.LoadPrefabContents(ScreenPath); int before = log.Count;
        try
        {
            var c = root.GetComponentInChildren<SettlementController>(true) ?? throw new Exception("SettlementController missing");
            var main = c.Main ? c.Main.transform : root.transform.Find("Main") ?? throw new Exception("Main missing");
            var node = main.Find("AssignmentMarkers");
            if (!node)
            {
                var g = new GameObject("AssignmentMarkers", typeof(RectTransform)); g.layer = main.gameObject.layer; g.transform.SetParent(main, false); node = g.transform;
                var r = (RectTransform)node; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = Middle; r.offsetMin = r.offsetMax = Vector2.zero;
                // Right after the last facility: above the facilities and the HUD before them (DayPanel, AdvanceTime, Location, ResourceHud,
                // Journal: default offsets keep clear of them, VerifyAssignmentMarkers checks it), below the roster cards and buttons after them.
                var last = main.Cast<Transform>().Where(t => t != node && t.name.StartsWith("Facility_")).OrderBy(t => t.GetSiblingIndex()).LastOrDefault();
                if (last) node.SetSiblingIndex(last.GetSiblingIndex() + 1);
                log.Add("Main/AssignmentMarkers 생성");
            }
            var mk = Comp<SettlementAssignmentMarkers>(node.gameObject);
            if (mk.Owner != c) { mk.Owner = c; log.Add("마커 컨트롤러 연결"); }
            var slots = (mk.Slots ?? new SettlementAssignmentMarkers.Slot[0]).Where(s => s != null).ToList(); bool slotChange = false;
            foreach (var (place, offset) in Defaults)
            {
                var slot = slots.FirstOrDefault(s => s.Facility == place);
                if (slot == null) { slot = new SettlementAssignmentMarkers.Slot { Facility = place, Offset = offset }; slots.Add(slot); slotChange = true; log.Add(place + " 칸"); }
                var button = ButtonFor(c, place);
                if (!button) log.Add("경고: " + place + " 버튼 없음");
                else if (slot.Button != button) { slot.Button = button; slotChange = true; log.Add(place + " 버튼 연결"); }
                var t = node.Find("Bubble_" + place);
                if (!t)
                {
                    var g = (GameObject)PrefabUtility.InstantiatePrefab(asset, node); g.name = "Bubble_" + place; g.SetActive(false); t = g.transform;
                    ((RectTransform)t).localPosition = Vector3.zero; log.Add("Bubble_" + place + " 생성");
                }
                var bubble = t.GetComponent<AssignmentBubble>();
                if (slot.Bubble != bubble) { slot.Bubble = bubble; slotChange = true; log.Add(place + " 말풍선 연결"); }
            }
            if (slotChange || mk.Slots == null || mk.Slots.Length != slots.Count) mk.Slots = slots.ToArray();
            // The head bubbles are replaced by the facility markers.
            for (int i = 0; i < 2; i++) { var wb = main.Find("WorkBubble_" + i); if (wb && wb.gameObject.activeSelf) { wb.gameObject.SetActive(false); log.Add("WorkBubble_" + i + " 끄기"); } }
            if (log.Count > before) { EditorUtility.SetDirty(mk); PrefabUtility.SaveAsPrefabAsset(root, ScreenPath); }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Button ButtonFor(SettlementController c, SettlementAssignmentMarkers.Place place)
    {
        switch (place)
        {
            case SettlementAssignmentMarkers.Place.Bed: return c.Bed;
            case SettlementAssignmentMarkers.Place.Stock: return c.Stock;
            case SettlementAssignmentMarkers.Place.Workbench: return c.Workbench;
            case SettlementAssignmentMarkers.Place.Cabinet: return c.Cabinet;
            default: return c.Development ? c.Development.ResearchButton : null;
        }
    }

    // ---- helpers ----
    static T Required<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new Exception("Missing asset: " + path);
    static T Comp<T>(GameObject g, Action<T> init = null) where T : Component
    {
        var c = g.GetComponent<T>(); if (c) return c;
        c = g.AddComponent<T>(); init?.Invoke(c); log.Add(g.name + " " + typeof(T).Name + " 추가"); return c;
    }
    static Transform Node(Transform parent, string name, out bool made, params Type[] components)
    {
        var t = parent.Find(name); made = !t; if (t) return t;
        var g = new GameObject(name, new[] { typeof(RectTransform) }.Concat(components).ToArray()); g.layer = parent.gameObject.layer; g.transform.SetParent(parent, false);
        log.Add(parent.name + "/" + name + " 생성"); return g.transform;
    }
    static void Place(Transform t, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var r = (RectTransform)t; r.anchorMin = r.anchorMax = anchor; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
    }
    static void Wire<T>(ref T field, T value, string what) where T : Object { if (field == value) return; field = value; log.Add(what + " 연결"); }
}
