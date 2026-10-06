using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 수색 쪽지 (2026-09-25, 기획/탐험-수색쪽지와-협동-1차.md · 시안 탐험-수색쪽지-시안/01~02 without the pace row and the '나올 것' row the
// user removed): ExpeditionArrivalPanel.prefab Main/'SearchNote' (Main's last child, right after Main/MemberActions) with
// FieldSearchNote: the outside-press catcher (the room area down to the plan markers' RoomArea bottom, above the tray, inactive),
// the tail (FieldNoteTail, inactive) and the paper (card-paper, 520 × 300, inactive): object name, '수색도', the lead and helper
// slots (portrait with the paper portrait material, dashed empty ring and '+'), the helper hint, three role chips, the face row
// (6 faces, cloned at runtime if needed), the forecast line, '빼기' · '자세히 >' · '수색 · 1턴'. Wires FieldQuickAssign.Note.
// Paper art only (card-paper, the members' portraits); rings, frames and the tail are code-drawn. Texts come from the note's
// Inspector strings at runtime. SettlementScreen's nested arrival panel inherits the node (checked, not edited).
// Idempotent: nodes are found or created by name; existing rects, texts and Inspector values are kept; prints what it changed or
// 'Already applied.'. Run AFTER every other builder, BuildMemberActionSlot.Run included (it reads Main/MemberActions and the plan
// markers' RoomArea; SearchNote must follow MemberActions).
public static class BuildSearchNote
{
    const string P = "Assets/Prefabs/Settlement/", ArrivalPath = P + "ExpeditionArrivalPanel.prefab", ScreenPath = P + "SettlementScreen.prefab";
    const string Art = "Assets/Art/PartySelection/", FontPath = "Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf", PortraitMaterial = "Assets/Settings/PaperPortrait.mat";
    public const string NodeName = "SearchNote", MembersNode = "MemberActions", MarkersNode = "PlanTargetMarkers";
    public const int FaceCount = 6;
    public static readonly Vector2 PaperSize = new Vector2(520, 300);
    static readonly Color Ink = new Color(.1f, .09f, .08f, 1), Muted = new Color(.38f, .31f, .2f, 1), Cream = new Color(.95f, .91f, .8f, 1), Gold = new Color(.96f, .75f, .28f, 1),
        Dash = new Color(.6f, .5f, .32f, 1), Rule = new Color(.45f, .37f, .25f, .35f), Green = new Color(.62f, .8f, .5f, 1);
    static readonly Vector2 Middle = new Vector2(.5f, .5f), TopLeft = new Vector2(0, 1);
    static readonly List<string> log = new List<string>();

    public static string Run()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Stop Play and preserve the scene first");
        log.Clear(); string scene = EditorSceneManager.GetActiveScene().path;
        try { Arrival(); CheckScreen(); AssetDatabase.SaveAssets(); }
        finally
        {
            // New nodes are made in the active scene before they move into the prefab: drop that side effect (the scene was clean).
            if (EditorSceneManager.GetActiveScene().isDirty && !string.IsNullOrEmpty(scene)) { EditorSceneManager.OpenScene(scene); log.Add("장면 다시 열기(임시 노드 정리)"); }
        }
        return log.Count == 0 ? "Already applied." : "Applied: " + string.Join("; ", log);
    }

    static void Arrival()
    {
        var root = PrefabUtility.LoadPrefabContents(ArrivalPath); int before = log.Count;
        try
        {
            var arrival = root.GetComponent<ExpeditionArrivalPanel>(); if (!arrival || !arrival.Main) throw new Exception("ExpeditionArrivalPanel/Main missing");
            var main = arrival.Main.transform;
            var members = main.Find(MembersNode) ?? throw new Exception("Run BuildMemberActionSlot.Run first (Main/" + MembersNode + ")");
            var quick = members.GetComponent<FieldQuickAssign>() ?? throw new Exception("Main/" + MembersNode + " has no FieldQuickAssign (run BuildMemberActionSlot.Run)");
            var markers = main.Find(MarkersNode)?.GetComponent<FieldPlanTargetMarkers>() ?? throw new Exception("Run BuildFieldPlanMarkers.Run first (Main/" + MarkersNode + ")");
            var font = Required<Font>(FontPath); var paperSprite = Required<Sprite>(Art + "card-paper.png"); var portraitMat = Required<Material>(PortraitMaterial);

            var node = main.Find(NodeName); bool made = false;
            if (!node) { var g = new GameObject(NodeName, typeof(RectTransform)); g.layer = main.gameObject.layer; g.transform.SetParent(main, false); node = g.transform; Stretch(node); made = true; log.Add("Main/" + NodeName + " 생성"); }
            // MemberActions then SearchNote, the last two children of Main (the note over the room veil, under the popups: Main's siblings).
            if (members.GetSiblingIndex() != main.childCount - 2 || node.GetSiblingIndex() != main.childCount - 1) { members.SetAsLastSibling(); node.SetAsLastSibling(); log.Add(NodeName + " Main 맨 위로 (" + MembersNode + " 위)"); }
            var note = Comp<FieldSearchNote>(node.gameObject);
            if (made) { note.RoomArea = markers.RoomArea; log.Add("방 영역 = 말풍선 방 영역"); }

            // The outside-press catcher: full width, from the top down to the markers' room area bottom (above the member tray).
            var catcher = Node(node, "Catcher", out bool fresh, typeof(Image));
            if (fresh) { var im = catcher.GetComponent<Image>(); im.color = new Color(0, 0, 0, 0); im.raycastTarget = true; catcher.GetComponent<CanvasRenderer>().cullTransparentMesh = true; catcher.gameObject.SetActive(false); }
            {
                float bottom = Mathf.Clamp(markers.RoomArea.yMax + 6, 200, 1080);
                var r = (RectTransform)catcher; var want = (min: new Vector2(0, -bottom), max: Vector2.zero);
                if (r.anchorMin != TopLeft || r.anchorMax != Vector2.one || r.pivot != new Vector2(.5f, 1) || r.offsetMin != want.min || r.offsetMax != want.max)
                { r.anchorMin = TopLeft; r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, 1); r.offsetMin = want.min; r.offsetMax = want.max; log.Add("쪽지 밖 누름 면: 위 ~ y" + bottom); }
            }
            var tail = Node(node, "Tail", out fresh, typeof(FieldNoteTail));
            if (fresh) { Stretch(tail); tail.GetComponent<FieldNoteTail>().color = Cream; tail.gameObject.SetActive(false); }
            var paper = Node(node, "Paper", out fresh, typeof(Image));
            if (fresh)
            {
                var r = (RectTransform)paper; r.anchorMin = r.anchorMax = r.pivot = Middle; r.anchoredPosition = Vector2.zero; r.sizeDelta = PaperSize;
                var im = paper.GetComponent<Image>(); im.sprite = paperSprite; im.color = Color.white; im.raycastTarget = true; paper.gameObject.SetActive(false);
            }
            // Catcher under the tail under the paper.
            var order = new[] { catcher, tail, paper };
            bool sorted = true; for (int i = 1; i < order.Length; i++) if (order[i - 1].GetSiblingIndex() > order[i].GetSiblingIndex()) sorted = false;
            if (!sorted) { foreach (var t in order) t.SetAsLastSibling(); log.Add(NodeName + " 자식 순서"); }

            var title = TextAt(paper, "Title", 22, 14, 300, 42, font, 30, 20, Ink, TextAnchor.MiddleLeft, "물자 상자");
            var progress = TextAt(paper, "Progress", 322, 20, 176, 32, font, 20, 14, Muted, TextAnchor.MiddleRight, "수색도 0 / 2");
            var rule = Node(paper, "Rule", out fresh, typeof(Image));
            if (fresh) { Place(rule, 20, 60, 480, 2); var im = rule.GetComponent<Image>(); im.color = Rule; im.raycastTarget = false; }
            var lead = SlotAt(paper, "LeadSlot", 22, paperSprite, portraitMat, font, "담당");
            var helper = SlotAt(paper, "HelperSlot", 128, paperSprite, portraitMat, font, "협동");
            var hint = TextAt(paper, "HelperHint", 236, 72, 264, 128, font, 17, 12, Muted, TextAnchor.UpperLeft, "두 번째 대원을 이 사물에\n끌어다 놓거나 협동 칸을\n누르면 함께합니다.\n함께 수색 · 망보기 · 조명");
            var roles = new FieldSearchNote.Chip[3]; string[] roleNames = { "함께 수색", "망보기", "조명 지원" };
            for (int i = 0; i < 3; i++) roles[i] = ChipAt(paper, "Role_" + i, 236, 72 + i * 44, paperSprite, font, roleNames[i]);
            var picker = Node(paper, "Picker", out fresh); if (fresh) { Place(picker, 236, 72, 264, 132); picker.gameObject.SetActive(false); }
            var pickerTitle = TextAt(picker, "Title", 0, 0, 264, 24, font, 16, 12, Muted, TextAnchor.MiddleLeft, "담당 고르기");
            var faces = new List<FieldSearchNote.Face>(); for (int i = 0; i < FaceCount; i++) faces.Add(FaceAt(picker, "Face_" + i, i % 4 * 64, 26 + i / 4 * 54, portraitMat));
            var forecast = TextAt(paper, "Forecast", 22, 210, 478, 30, font, 19, 13, Ink, TextAnchor.MiddleLeft, "이번 턴 0/2 → 1/2 · 소음 +1");
            forecast.supportRichText = true;
            var remove = ButtonAt(paper, "Remove", 22, 250, 118, 42, paperSprite, Color.white, font, 20, Ink, "빼기", true);
            var detail = ButtonAt(paper, "Detail", 196, 250, 128, 42, null, new Color(0, 0, 0, 0), font, 18, Muted, "자세히 >", false);
            var run = ButtonAt(paper, "Run", 340, 250, 160, 42, paperSprite, Green, font, 20, Ink, "수색 · 1턴", true);

            Wire(ref note.Arrival, arrival, "note.Arrival"); Wire(ref note.Quick, quick, "note.Quick"); Wire(ref note.Markers, markers, "note.Markers");
            Wire(ref note.Paper, (RectTransform)paper, "note.Paper"); Wire(ref note.Catcher, (Graphic)catcher.GetComponent<Image>(), "note.Catcher"); Wire(ref note.Tail, tail.GetComponent<FieldNoteTail>(), "note.Tail");
            Wire(ref note.Title, title, "note.Title"); Wire(ref note.Progress, progress, "note.Progress"); Wire(ref note.HelperHint, hint, "note.HelperHint");
            WireSlot(note.Lead ?? (note.Lead = new FieldSearchNote.Slot()), lead, "note.Lead"); WireSlot(note.Helper ?? (note.Helper = new FieldSearchNote.Slot()), helper, "note.Helper");
            if (note.Roles == null || note.Roles.Length != 3 || note.Roles.Any(c => c == null || !c.Button)) { note.Roles = roles; log.Add("역할 칩 3개"); }
            Wire(ref note.Picker, (RectTransform)picker, "note.Picker"); Wire(ref note.PickerTitle, pickerTitle, "note.PickerTitle");
            var kept = (note.Faces ?? new FieldSearchNote.Face[0]).Where(f => f != null && f.Button && !faces.Any(x => x.Button == f.Button));
            var allFaces = faces.Concat(kept).ToArray();
            if (note.Faces == null || note.Faces.Length != allFaces.Length || note.Faces.Where((f, i) => f == null || f.Button != allFaces[i].Button || f.Portrait != allFaces[i].Portrait || f.Current != allFaces[i].Current).Any()) { note.Faces = allFaces; log.Add("얼굴 " + allFaces.Length); }
            Wire(ref note.Forecast, forecast, "note.Forecast"); Wire(ref note.Remove, remove, "note.Remove"); Wire(ref note.Detail, detail, "note.Detail"); Wire(ref note.Run, run, "note.Run");
            Wire(ref quick.Note, note, "quick.Note");
            if (log.Count > before) { EditorUtility.SetDirty(note); EditorUtility.SetDirty(quick); PrefabUtility.SaveAsPrefabAsset(root, ArrivalPath); }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // A member slot: paper card (the button), frame, portrait, the empty mark (dashed ring + '+'), the name and the caption under it.
    static Transform SlotAt(Transform paper, string name, float x, Sprite paperSprite, Material portraitMat, Font font, string label)
    {
        var t = Node(paper, name, out bool made, typeof(Image), typeof(Button));
        if (made)
        {
            Place(t, x, 70, 96, 112); var im = t.GetComponent<Image>(); im.sprite = paperSprite; im.color = Color.white; im.raycastTarget = true;
            var b = t.GetComponent<Button>(); b.targetGraphic = im; b.transition = Selectable.Transition.None;
        }
        var frame = Node(t, "Frame", out made, typeof(FieldFrameGraphic)); if (made) { Stretch(frame); var g = frame.GetComponent<FieldFrameGraphic>(); g.Thickness = 3; g.Dash = 8; g.Gap = 6; g.color = Dash; }
        var portrait = Node(t, "Portrait", out made, typeof(Image));
        if (made) { Place(portrait, 10, 6, 76, 76); var im = portrait.GetComponent<Image>(); im.preserveAspect = true; im.raycastTarget = false; portrait.gameObject.SetActive(false); }
        Style(portrait.GetComponent<Image>(), portraitMat);
        var empty = Node(t, "Empty", out made); if (made) Place(empty, 18, 12, 60, 60);
        var ring = Node(empty, "Ring", out made, typeof(SegmentRingGraphic));
        if (made) { Stretch(ring); var g = ring.GetComponent<SegmentRingGraphic>(); g.Segments = 12; g.Done = g.Next = 0; g.Thickness = 2.5f; g.GapDegrees = 13; g.RestColor = Color.white; g.color = Dash; }
        TextAt(empty, "Plus", 0, 0, 60, 60, font, 34, 20, Dash, TextAnchor.MiddleCenter, "+");
        TextAt(t, "Label", 0, 84, 96, 24, font, 18, 12, Ink, TextAnchor.MiddleCenter, label);
        TextAt(t, "Caption", -10, 114, 116, 18, font, 13, 10, Muted, TextAnchor.MiddleCenter, "끌어다 놓기");
        return t;
    }
    static void WireSlot(FieldSearchNote.Slot s, Transform t, string what)
    {
        Wire(ref s.Button, t.GetComponent<Button>(), what + ".Button"); Wire(ref s.Paper, t.GetComponent<Image>(), what + ".Paper");
        Wire(ref s.Frame, t.Find("Frame").GetComponent<FieldFrameGraphic>(), what + ".Frame"); Wire(ref s.Portrait, t.Find("Portrait").GetComponent<Image>(), what + ".Portrait");
        var empty = t.Find("Empty").gameObject; if (s.Empty != empty) { s.Empty = empty; log.Add(what + ".Empty 연결"); }
        Wire(ref s.Label, t.Find("Label").GetComponent<Text>(), what + ".Label"); Wire(ref s.Caption, t.Find("Caption").GetComponent<Text>(), what + ".Caption");
    }
    // A role chip: paper (the button) with the role name and a small second line.
    static FieldSearchNote.Chip ChipAt(Transform paper, string name, float x, float y, Sprite paperSprite, Font font, string title)
    {
        var t = Node(paper, name, out bool made, typeof(Image), typeof(Button));
        if (made)
        {
            Place(t, x, y, 264, 40); var im = t.GetComponent<Image>(); im.sprite = paperSprite; im.color = new Color(.99f, .96f, .88f, 1); im.raycastTarget = true;
            var b = t.GetComponent<Button>(); b.targetGraphic = im; b.transition = Selectable.Transition.None; t.gameObject.SetActive(false);
        }
        return new FieldSearchNote.Chip
        {
            Button = t.GetComponent<Button>(), Paper = t.GetComponent<Image>(),
            Title = TextAt(t, "Title", 8, 1, 248, 22, font, 19, 13, Ink, TextAnchor.MiddleCenter, title),
            Line = TextAt(t, "Line", 8, 22, 248, 16, font, 13, 10, Ink, TextAnchor.MiddleCenter, "")
        };
    }
    // A face in the picker: a transparent press area, a paper disc, the portrait, the 'now in this slot' ring.
    static FieldSearchNote.Face FaceAt(Transform picker, string name, float x, float y, Material portraitMat)
    {
        var t = Node(picker, name, out bool made, typeof(Image), typeof(Button));
        if (made)
        {
            Place(t, x, y, 50, 50); var im = t.GetComponent<Image>(); im.color = new Color(0, 0, 0, 0); im.raycastTarget = true; t.GetComponent<CanvasRenderer>().cullTransparentMesh = true;
            var b = t.GetComponent<Button>(); b.targetGraphic = im; b.transition = Selectable.Transition.None;
        }
        var disc = Node(t, "Disc", out made, typeof(SegmentRingGraphic)); if (made) { Stretch(disc); var g = disc.GetComponent<SegmentRingGraphic>(); g.Segments = 0; g.Thickness = 999; g.color = Cream; }
        var portrait = Node(t, "Portrait", out made, typeof(Image));
        if (made) { Place(portrait, 4, 2, 42, 42); var im = portrait.GetComponent<Image>(); im.preserveAspect = true; im.raycastTarget = false; }
        Style(portrait.GetComponent<Image>(), portraitMat);
        var current = Node(t, "Current", out made, typeof(SegmentRingGraphic));
        if (made) { Stretch(current); var g = current.GetComponent<SegmentRingGraphic>(); g.Segments = 0; g.Thickness = 4; g.color = Gold; current.gameObject.SetActive(false); }
        return new FieldSearchNote.Face { Button = t.GetComponent<Button>(), Portrait = portrait.GetComponent<Image>(), Current = current.GetComponent<SegmentRingGraphic>() };
    }
    static Button ButtonAt(Transform paper, string name, float x, float y, float w, float h, Sprite sprite, Color tint, Font font, int size, Color ink, string label, bool tintPaper)
    {
        var t = Node(paper, name, out bool made, typeof(Image), typeof(Button));
        if (made)
        {
            Place(t, x, y, w, h); var im = t.GetComponent<Image>(); im.sprite = sprite; im.color = tint; im.raycastTarget = true;
            if (!sprite) t.GetComponent<CanvasRenderer>().cullTransparentMesh = true;
        }
        var text = TextAt(t, "Label", 0, 0, w, h, font, size, Mathf.Max(11, size - 6), ink, TextAnchor.MiddleCenter, label);
        if (made) { var b = t.GetComponent<Button>(); b.targetGraphic = tintPaper ? (Graphic)t.GetComponent<Image>() : text; b.transition = Selectable.Transition.ColorTint; }
        return t.GetComponent<Button>();
    }
    static void Style(Image portrait, Material portraitMat)
    {
        var style = portrait.GetComponent<PaperPortraitStyle>(); if (!style) { style = portrait.gameObject.AddComponent<PaperPortraitStyle>(); log.Add(portrait.transform.parent.name + " 초상 종이 재질"); }
        if (style.StyleMaterial != portraitMat) style.Configure(portraitMat);
    }

    // SettlementScreen nests the arrival panel: it must carry the note node and its wiring (inherited, not an override).
    static void CheckScreen()
    {
        var screen = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath); if (!screen) return;
        foreach (var a in screen.GetComponentsInChildren<ExpeditionArrivalPanel>(true))
        {
            var node = a.Main ? a.Main.transform.Find(NodeName) : null; var note = node ? node.GetComponent<FieldSearchNote>() : null;
            var quick = a.Main ? a.Main.GetComponentInChildren<FieldQuickAssign>(true) : null;
            if (!note) log.Add("경고: SettlementScreen 안 Main/" + NodeName + " 없음");
            else if (note.Arrival != a || !quick || quick.Note != note) log.Add("경고: SettlementScreen 안 쪽지 연결이 덮어쓰여 있음 (VerifySearchNote.Wiring 확인)");
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
    // Top-left placement inside the parent (px, y down).
    static void Place(Transform t, float x, float y, float w, float h) { var r = (RectTransform)t; r.anchorMin = r.anchorMax = r.pivot = TopLeft; r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); }
    static void Stretch(Transform t) { var r = (RectTransform)t; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = Middle; r.offsetMin = r.offsetMax = Vector2.zero; }
    static Text TextAt(Transform parent, string name, float x, float y, float w, float h, Font font, int size, int min, Color color, TextAnchor align, string text)
    {
        var t = Node(parent, name, out bool made, typeof(Text)); var tx = t.GetComponent<Text>();
        if (made)
        {
            Place(t, x, y, w, h);
            tx.font = font; tx.fontSize = size; tx.color = color; tx.alignment = align; tx.text = text; tx.raycastTarget = false; tx.supportRichText = false;
            tx.horizontalOverflow = HorizontalWrapMode.Wrap; tx.verticalOverflow = VerticalWrapMode.Truncate; tx.resizeTextForBestFit = true; tx.resizeTextMinSize = min; tx.resizeTextMaxSize = size; tx.lineSpacing = 1;
        }
        return tx;
    }
    static void Wire<T>(ref T field, T value, string what) where T : Object { if (field == value) return; field = value; log.Add(what + " 연결"); }
}
