using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using WorkSpot = Demo5.FrontEnd.ExplorationRoomPresentation.WorkSpot;
using RoomFloor = Demo5.FrontEnd.ExplorationRoomPresentation.RoomFloor;

// 말 놓기 판 (2026-09-25, 기획/탐험-말놓기-조작-재설계.md · 시안 탐험-말놓기-시안/01~03 · design §1, §3, §5 Group A):
// 1. Assets/Prefabs/Settlement/FieldPawnGhost.prefab: a variant of FieldPawn.prefab without PawnGroundShadow (a silhouette is the
//    held pawn's body see-through: alpha through vertex colour; the floor shadow mesh cannot fade).
// 2. ExpeditionWorld.prefab: 'PawnGhosts' beside 'Pawns' (never under it: the resident and the shadow refresh read the pawn root),
//    and ExplorationRoomPresentation Spots / Floors — only entries that are missing are added (tuned Inspector values are kept).
//    Checks that Settlement.unity's ExpeditionWorld instance does not override Spots / Floors.
// 3. ExpeditionArrivalPanel.prefab Main/'PawnBoard' (Main's last child): FieldPawnBoard with Catcher (the room above the member tray,
//    off), Handles/Handle_0..5, Ghosts/GhostHit_0..7 (transparent Buttons + FieldPawnHandle), Pins/Pin_0..7 (FieldTargetGlow badge +
//    caption + padlock), Chips/ChipRow_0 (3 chip buttons), Tags/Tag_0..3 (paper + text); FieldMemberActionSlots.PawnBoard; the card
//    slot and '턴 진행' texts of the mocks (only while they still hold the old defaults).
// SettlementScreen's nested arrival panel inherits all of it (checked, not edited). Idempotent: nodes are found or created by name,
// existing nodes keep their rects and Inspector values; prints what it changed or 'Already applied.'.
// Order: after every existing builder and BuildPawnRules (B); before BuildRetireOldAssign (C); then BuildLaundryStoryVisit only if
// HudTray or Members changed (this builder adds nothing under them).
public static class BuildPawnBoard
{
    const string P = "Assets/Prefabs/Settlement/", PawnPath = P + "FieldPawn.prefab", GhostPath = P + "FieldPawnGhost.prefab", WorldPath = P + "ExpeditionWorld.prefab";
    const string ArrivalPath = P + "ExpeditionArrivalPanel.prefab", ScreenPath = P + "SettlementScreen.prefab", ScenePath = "Assets/Scenes/Settlement.unity";
    const string Art = "Assets/Art/PartySelection/", FontPath = "Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf", RosterPath = "Assets/Data/PartyRoster.asset";
    public const string NodeName = "PawnBoard", GhostNode = "PawnGhosts";
    public const int HandlePool = 6, GhostPool = 8, PinPool = 8, TagPool = 4, Chips = 3, ProgressPool = 6;
    static readonly Color Ink = new Color(.06f, .07f, .07f, 1), Paper = new Color(.98f, .95f, .86f, 1), Gold = new Color(.96f, .75f, .28f, 1), Cream = new Color(.97f, .94f, .86f, 1), Dark = new Color(.075f, .11f, .105f, .96f);
    static readonly Vector2 Middle = new Vector2(.5f, .5f), Bottom = new Vector2(.5f, 0);
    static readonly List<string> log = new List<string>();

    public static string Run()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Stop Play and preserve the scene first");
        log.Clear(); string scene = EditorSceneManager.GetActiveScene().path;
        try { Ghost(); World(); CheckScene(); Arrival(); CheckScreen(); AssetDatabase.SaveAssets(); }
        finally
        {
            // New nodes are made in the active scene before they move into a prefab: drop that side effect (the scene was clean).
            if (EditorSceneManager.GetActiveScene().isDirty && !string.IsNullOrEmpty(scene)) { EditorSceneManager.OpenScene(scene); log.Add("장면 다시 열기(임시 노드 정리)"); }
        }
        return log.Count == 0 ? "Already applied." : "Applied: " + string.Join("; ", log);
    }

    // ---- 1. FieldPawnGhost.prefab ----
    static void Ghost()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(GhostPath);
        if (existing)
        {
            if (existing.GetComponentsInChildren<PawnGroundShadow>(true).Length == 0) return;
            var root = PrefabUtility.LoadPrefabContents(GhostPath);
            try { foreach (var s in root.GetComponentsInChildren<PawnGroundShadow>(true)) Object.DestroyImmediate(s); PrefabUtility.SaveAsPrefabAsset(root, GhostPath); log.Add("FieldPawnGhost 바닥 그림자 제거"); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return;
        }
        var pawn = Required<GameObject>(PawnPath);
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(pawn, preview); inst.name = "FieldPawnGhost";
            try
            {
                // A variant: removing the shadow is its only override, so the pawn's base, sorting and facing follow FieldPawn.
                foreach (var s in inst.GetComponentsInChildren<PawnGroundShadow>(true)) Object.DestroyImmediate(s);
                PrefabUtility.SaveAsPrefabAsset(inst, GhostPath, out bool ok); if (!ok) throw new Exception("variant not saved");
                log.Add("FieldPawnGhost.prefab (FieldPawn 변형 · 바닥 그림자 없음)");
            }
            catch (Exception e)
            {
                // A component of a prefab instance could not be removed here: save a plain copy instead (the same parts).
                if (inst) Object.DestroyImmediate(inst);
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(pawn, preview); copy.name = "FieldPawnGhost";
                PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var s in copy.GetComponentsInChildren<PawnGroundShadow>(true)) Object.DestroyImmediate(s);
                PrefabUtility.SaveAsPrefabAsset(copy, GhostPath, out bool ok); if (!ok) throw new Exception("FieldPawnGhost not saved: " + e.Message);
                log.Add("FieldPawnGhost.prefab (FieldPawn 복사본 · 변형 저장 실패: " + e.Message + ")");
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    // ---- 2. ExpeditionWorld.prefab: ghost node, spots, floors ----
    static void World()
    {
        var root = PrefabUtility.LoadPrefabContents(WorldPath); int before = log.Count;
        try
        {
            var p = root.GetComponent<ExplorationRoomPresentation>() ?? throw new Exception("Run BuildExplorationRoomPresentation.Run first");
            var pawns = root.transform.Find("Pawns") ?? throw new Exception("ExpeditionWorld/Pawns missing");
            var ghosts = root.transform.Find(GhostNode);
            if (!ghosts)
            {
                var g = new GameObject(GhostNode); g.layer = pawns.gameObject.layer; g.transform.SetParent(root.transform, false);
                g.transform.localPosition = pawns.localPosition; g.transform.localRotation = pawns.localRotation; g.transform.localScale = pawns.localScale;
                g.transform.SetSiblingIndex(pawns.GetSiblingIndex() + 1); log.Add("ExpeditionWorld/" + GhostNode + " 생성");
            }
            var spots = (p.Spots ?? new WorkSpot[0]).Where(s => s != null).ToList(); int had = spots.Count;
            foreach (var d in DefaultSpots()) if (!spots.Any(s => s.Room == d.Room && s.Key == d.Key)) { spots.Add(d); log.Add("자리 " + d.Room + " " + d.Key); }
            if (spots.Count != had || p.Spots == null || p.Spots.Length != spots.Count) p.Spots = spots.ToArray();
            var floors = (p.Floors ?? new RoomFloor[0]).Where(f => f != null).ToList(); had = floors.Count;
            foreach (var d in DefaultFloors()) if (!floors.Any(f => f.Room == d.Room)) { floors.Add(d); log.Add("바닥 " + d.Room + " " + d.Label); }
            if (floors.Count != had || p.Floors == null || p.Floors.Length != floors.Count) p.Floors = floors.ToArray();
            if (log.Count > before) { EditorUtility.SetDirty(p); PrefabUtility.SaveAsPrefabAsset(root, WorldPath); }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    // Feet in the 19.2 × 10.8 room frame. Objects: slot0 = the painted bottom centre − (0.6, 0.8), slot1 = slot0 + (1.25, −0.08)
    // (bases 1.02 wide; a y step for the sort tie-break), clamped onto the floor; hand-tuned where the painting needs it (the
    // arcade table's legs reach −2.0, the SPACE cabinet stands behind the left table). Doors: slot0 = the route's floor waypoint
    // + (0, 0.4), then a queue inward 1.1 apart (a central door alternates sides). The office door holds door:3 and search:8.
    static IEnumerable<WorkSpot> DefaultSpots()
    {
        WorkSpot S(int room, string key, string label, float look, params float[] xy)
        {
            var slots = new Vector2[xy.Length / 2]; for (int i = 0; i < slots.Length; i++) slots[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return new WorkSpot { Room = room, Key = key, Label = label, LookAtX = look, Slots = slots };
        }
        const int A = FieldSiteState.Arcade, C = FieldSiteState.Corridor, St = FieldSiteState.Storage;
        yield return S(A, "search:0", "오락실 · 물자 상자", 1.68f, 1.08f, -1.59f, 2.33f, -1.67f);
        yield return S(A, "search:1", "오락실 · 오른쪽 탁자 (다리 −2.0 · 왼쪽 옆과 앞)", 5.72f, 3.75f, -1.30f, 4.75f, -1.90f);
        yield return S(A, "search:2", "오락실 · SPACE 오락기 (앞 탁자 너머)", -2.21f, -2.80f, -1.05f, -1.55f, -1.13f);
        yield return S(A, "door:1", "오락실 → 복도 문", 8.77f, 8.05f, -1.55f, 6.95f, -1.90f, 5.85f, -1.85f, 4.75f, -1.80f, 3.65f, -1.75f, 2.55f, -1.70f);
        yield return S(A, "observe:" + ExpeditionNpcStory.ObservationId, "오락실 · 오락기 뒤 흔적 (장도윤 자리 −7.1 옆)", -8.2f, -5.9f, -1.45f);
        yield return S(C, "search:4", "복도 · 두꺼비집", -6.80f, -7.40f, -.20f, -6.15f, -.28f);
        yield return S(C, "search:5", "복도 · 오른쪽 상자", 4.94f, 4.34f, -.61f, 5.59f, -.69f);
        yield return S(C, "door:0", "복도 → 오락실 문", -8.70f, -7.75f, -1.20f, -6.65f, -1.55f, -5.55f, -1.50f, -4.45f, -1.45f, -3.35f, -1.40f, -2.25f, -1.35f);
        yield return S(C, "door:2", "복도 → 보관실 철문", 2.16f, 2.20f, -1.05f, 1.10f, -1.40f, 3.30f, -1.40f, .00f, -1.35f, 4.40f, -1.35f, -1.10f, -1.30f);
        yield return S(C, "door:3", "복도 · 관리실 문 (귀 대기만)", 7.28f, 7.90f, -.90f);
        yield return S(C, "search:8", "복도 · 관리실 선반 (굴이 빈 동안)", 7.28f, 6.70f, -.95f, 5.90f, -1.50f);
        yield return S(St, "search:6", "보관실 · 선반", 3.23f, 2.63f, -.81f, 3.88f, -.89f);
        yield return S(St, "search:7", "보관실 · 판자와 공구함", 7.17f, 6.57f, -.90f, 7.82f, -.98f);
        yield return S(St, "door:1", "보관실 → 복도 문", -8.57f, -7.75f, -1.20f, -6.65f, -1.55f, -5.55f, -1.50f, -4.45f, -1.45f, -3.35f, -1.40f, -2.25f, -1.35f);
    }
    // The feet band: from the travel aisle (−1.95, above the member tray) up to below each room's back-wall line; free members wait
    // on the aisle, clear of the work spots.
    static IEnumerable<RoomFloor> DefaultFloors()
    {
        Vector2[] Row(params float[] x) => x.Select(v => new Vector2(v, -1.85f)).ToArray();
        yield return new RoomFloor { Room = FieldSiteState.Arcade, Label = "오락실", Band = new Rect(-8.8f, -1.95f, 17.4f, 1.65f), Idle = Row(-.6f, -1.9f, -3.2f, -4.5f, -5.8f, -7.1f) };
        yield return new RoomFloor { Room = FieldSiteState.Corridor, Label = "복도", Band = new Rect(-8.8f, -1.95f, 17.6f, 1.75f), Idle = Row(-1.2f, -2.5f, -3.8f, .1f, 4.7f, 7.2f) };
        yield return new RoomFloor { Room = FieldSiteState.Storage, Label = "보관실", Band = new Rect(-8.8f, -1.95f, 17.6f, 1.55f), Idle = Row(.1f, -1.2f, 1.4f, -2.5f, 2.7f, 4f) };
    }
    // The scene's ExpeditionWorld instance must take Spots / Floors from the prefab (no override): read as text, nothing opened.
    static void CheckScene()
    {
        if (!File.Exists(ScenePath)) return;
        var text = File.ReadAllText(ScenePath);
        foreach (var field in new[] { "Spots", "Floors" })
            if (text.Contains("propertyPath: " + field + ".") || text.Contains("propertyPath: " + field + "\n") || text.Contains("propertyPath: " + field + "\r"))
                log.Add("경고: " + ScenePath + "가 ExplorationRoomPresentation." + field + "를 덮어씀 (프리팹 값이 안 보임)");
    }

    // ---- 3. ExpeditionArrivalPanel.prefab Main/PawnBoard ----
    static void Arrival()
    {
        var root = PrefabUtility.LoadPrefabContents(ArrivalPath); int before = log.Count;
        try
        {
            var arrival = root.GetComponent<ExpeditionArrivalPanel>(); if (!arrival || !arrival.Main) throw new Exception("ExpeditionArrivalPanel/Main missing");
            if (!arrival.Threat || !arrival.Threat.Planner) throw new Exception("Run BuildFieldTurnPlan.Run first (planner)");
            var main = arrival.Main.transform; var font = Required<Font>(FontPath);
            var dark = Required<Sprite>(Art + "footer-paper.png"); var chipPaper = Required<Sprite>(Art + "count-paper.png");

            var node = main.Find(NodeName);
            if (!node) { var g = new GameObject(NodeName, typeof(RectTransform)); g.layer = main.gameObject.layer; g.transform.SetParent(main, false); node = g.transform; Stretch(node); log.Add("Main/" + NodeName + " 생성"); }
            if (node.GetSiblingIndex() != main.childCount - 1) { node.SetAsLastSibling(); log.Add(NodeName + " Main 맨 위로"); }
            var board = Comp<FieldPawnBoard>(node.gameObject);

            // The catcher: full width, from the top down to the room area's bottom (above the member tray; its cards stay pressable).
            var catcher = Node(node, "Catcher", out bool made, typeof(Image));
            if (made) { var im = catcher.GetComponent<Image>(); im.color = new Color(0, 0, 0, 0); im.raycastTarget = true; catcher.GetComponent<CanvasRenderer>().cullTransparentMesh = true; catcher.gameObject.SetActive(false); }
            {
                float bottom = Mathf.Clamp(RoomBottom(main) + 6, 200, 1080);
                var r = (RectTransform)catcher; var want = (min: new Vector2(0, -bottom), max: Vector2.zero);
                if (r.anchorMin != new Vector2(0, 1) || r.anchorMax != Vector2.one || r.pivot != new Vector2(.5f, 1) || r.offsetMin != want.min || r.offsetMax != want.max)
                { r.anchorMin = new Vector2(0, 1); r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, 1); r.offsetMin = want.min; r.offsetMax = want.max; log.Add("말 받는 면: 위 ~ y" + bottom); }
            }
            var handlesT = Node(node, "Handles", out made); if (made) Stretch(handlesT);
            var handles = new List<FieldPawnHandle>(); for (int i = 0; i < HandlePool; i++) handles.Add(Handle(handlesT, "Handle_" + i, FieldPawnHandle.Kind.Pawn, board));
            var ghostsT = Node(node, "Ghosts", out made); if (made) Stretch(ghostsT);
            var ghostHits = new List<FieldPawnHandle>(); for (int i = 0; i < GhostPool; i++) ghostHits.Add(Handle(ghostsT, "GhostHit_" + i, FieldPawnHandle.Kind.Ghost, board));
            var pinsT = Node(node, "Pins", out made); if (made) Stretch(pinsT);
            var pins = new List<FieldTargetGlow>(); for (int i = 0; i < PinPool; i++) pins.Add(Pin(pinsT, "Pin_" + i, font, dark));
            var chipsT = Node(node, "Chips", out made); if (made) Stretch(chipsT);
            var row = ChipRow(chipsT, "ChipRow_0", font, chipPaper);
            var tagsT = Node(node, "Tags", out made); if (made) Stretch(tagsT);
            var tags = new List<RectTransform>(); for (int i = 0; i < TagPool; i++) tags.Add(Tag(tagsT, "Tag_" + i, font, dark));
            var progT = Node(node, "Progress", out made); if (made) Stretch(progT);
            var strips = new List<RectTransform>(); for (int i = 0; i < ProgressPool; i++) strips.Add(ProgressStrip(progT, "Progress_" + i, dark));
            // Draw / press order inside the node: catcher, pawns, silhouettes (above the pawns), progress strips, pins, chips, tags (on top).
            var present = new[] { "Catcher", "Handles", "Ghosts", "Progress", "Pins", "Chips", "Tags" }.Select(n => node.Find(n)).Where(k => k).ToList();
            bool sorted = true; for (int i = 1; i < present.Count; i++) if (present[i - 1].GetSiblingIndex() > present[i].GetSiblingIndex()) sorted = false;
            if (!sorted) { foreach (var k in present) k.SetAsLastSibling(); log.Add(NodeName + " 자식 순서"); }

            Wire(ref board.Arrival, arrival, "board.Arrival"); Wire(ref board.Catcher, catcher.GetComponent<Image>(), "board.Catcher");
            Wire(ref board.Roster, AssetDatabase.LoadAssetAtPath<PartyRoster>(RosterPath), "board.Roster");
            Wire(ref board.GhostPrefab, AssetDatabase.LoadAssetAtPath<GameObject>(GhostPath), "board.GhostPrefab");
            Pool(ref board.Handles, handles, "대원 말 누름 영역"); Pool(ref board.GhostHandles, ghostHits, "실루엣 누름 영역"); Pool(ref board.Pins, pins, "핀");
            Pool(ref board.ChipRows, new List<RectTransform> { row }, "역할 칩 줄"); Pool(ref board.Tags, tags, "이름표"); Pool(ref board.ProgressStrips, strips, "수색 진행 칸");

            // The card slots and '턴 진행' read the mocks (only while the old defaults are still there: an edited text is kept).
            var slots = main.GetComponentInChildren<FieldMemberActionSlots>(true);
            if (slots)
            {
                Wire(ref slots.PawnBoard, board, "slots.PawnBoard");
                Retext(ref slots.FreeDetail, "누르고 사물을 고르세요", "말을 누르고 사물을 누르세요", "행동 남음 둘째 줄");
                Retext(ref slots.SubtitlePending, "{0}명 행동 남음 · 숨죽임", "{0}명 행동 남음", "'턴 진행' 부제");
                Retext(ref slots.SearchTogether, "{0} 함께 수색", "{0} 협동", "협동 칸");
                if (log.Count > before) EditorUtility.SetDirty(slots);
            }
            else log.Add("경고: FieldMemberActionSlots 없음 (BuildMemberActionSlot.Run 먼저)");
            if (log.Count > before) { EditorUtility.SetDirty(board); PrefabUtility.SaveAsPrefabAsset(root, ArrivalPath); }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    // The room area's bottom (px from Main's top): whatever component in Main keeps a 'RoomArea' Rect (the plan markers today,
    // FieldRoomArea after the retirement); 760 otherwise.
    static float RoomBottom(Transform main)
    {
        foreach (var mb in main.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!mb) continue; var prop = new SerializedObject(mb).FindProperty("RoomArea");
            if (prop != null && prop.propertyType == SerializedPropertyType.Rect) return prop.rectValue.yMax;
        }
        return 760;
    }
    // A transparent press area over a world pawn / silhouette (the board moves and sizes it every frame).
    static FieldPawnHandle Handle(Transform parent, string name, FieldPawnHandle.Kind role, FieldPawnBoard board)
    {
        var t = Node(parent, name, out bool made, typeof(Image), typeof(Button), typeof(FieldPawnHandle));
        if (made)
        {
            Place(t, Middle, Middle, Vector2.zero, new Vector2(100, 200));
            var im = t.GetComponent<Image>(); im.color = new Color(0, 0, 0, 0); im.raycastTarget = true; t.GetComponent<CanvasRenderer>().cullTransparentMesh = true;
            var b = t.GetComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = im; var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            t.gameObject.SetActive(false);
        }
        var h = t.GetComponent<FieldPawnHandle>();
        if (h.Role != role) { h.Role = role; log.Add(name + " 역할 " + role); }
        Wire(ref h.Board, board, name + ".Board"); Wire(ref h.Button, t.GetComponent<Button>(), name + ".Button");
        return h;
    }
    // A place badge (시안 01): a gold ring on a paper disc with the action glyph (a grey one with a padlock: the place is shut now),
    // a pulsing ring, and the short caption above it. Root pivot = its bottom centre (the board puts it just above the object's mark).
    static FieldTargetGlow Pin(Transform parent, string name, Font font, Sprite paper)
    {
        const float C = 30; // badge centre above the root
        var t = Node(parent, name, out bool made, typeof(FieldTargetGlow));
        if (made) { Place(t, Middle, Bottom, Vector2.zero, new Vector2(60, 60)); t.gameObject.SetActive(false); }
        var halo = Node(t, "Halo", out made, typeof(SegmentRingGraphic));
        if (made) { Place(halo, Bottom, Middle, new Vector2(0, C), new Vector2(66, 66)); var g = halo.GetComponent<SegmentRingGraphic>(); g.Segments = 0; g.Thickness = 5; g.color = Gold; }
        var visual = Node(t, "Visual", out made); if (made) Place(visual, Bottom, Bottom, Vector2.zero, new Vector2(60, 60));
        var ring = Node(visual, "Ring", out made, typeof(SegmentRingGraphic));
        if (made) { Place(ring, Bottom, Middle, new Vector2(0, C), new Vector2(56, 56)); var g = ring.GetComponent<SegmentRingGraphic>(); g.Segments = 0; g.Thickness = 6; g.color = Gold; }
        var disc = Node(visual, "Disc", out made, typeof(SegmentRingGraphic));
        if (made) { Place(disc, Bottom, Middle, new Vector2(0, C), new Vector2(46, 46)); var g = disc.GetComponent<SegmentRingGraphic>(); g.Segments = 0; g.Thickness = 999; g.color = Paper; }
        var glyph = Node(visual, "Glyph", out made, typeof(ActionGlyph));
        if (made) { Place(glyph, Bottom, Middle, new Vector2(0, C), new Vector2(28, 28)); glyph.GetComponent<ActionGlyph>().color = Ink; }
        var lockT = Node(visual, "Lock", out made, typeof(FieldLockGlyph));
        if (made) { Place(lockT, Bottom, Middle, new Vector2(0, C), new Vector2(26, 26)); lockT.GetComponent<FieldLockGlyph>().color = new Color(.24f, .24f, .23f, 1); lockT.gameObject.SetActive(false); }
        var cap = Node(visual, "Caption", out made, typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        if (made)
        {
            Place(cap, Bottom, Bottom, new Vector2(0, 2 * C + 6), new Vector2(120, 32));
            var im = cap.GetComponent<Image>(); im.sprite = paper; im.color = Dark; im.raycastTarget = false;
            var h = cap.GetComponent<HorizontalLayoutGroup>(); h.padding = new RectOffset(12, 12, 4, 4); h.childAlignment = TextAnchor.MiddleCenter; h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = h.childForceExpandHeight = false;
            var f = cap.GetComponent<ContentSizeFitter>(); f.horizontalFit = f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            cap.gameObject.SetActive(false);
        }
        SlicedFit(cap.GetComponent<Image>(), name + ".Caption");
        var text = Node(cap, "Text", out made, typeof(Text));
        if (made) { Style(text.GetComponent<Text>(), font, 20, Cream, TextAnchor.MiddleCenter, "수색 2턴 · 소음 0"); text.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow; }
        var hit = Node(t, "Hit", out made); if (made) Place(hit, Bottom, Middle, new Vector2(0, C), new Vector2(60, 60));
        var c = t.GetComponent<FieldTargetGlow>();
        Wire(ref c.Visual, (RectTransform)visual, name + ".Visual"); Wire(ref c.Pin, (Graphic)ring.GetComponent<SegmentRingGraphic>(), name + ".Pin"); Wire(ref c.Disc, (Graphic)disc.GetComponent<SegmentRingGraphic>(), name + ".Disc");
        Wire(ref c.Glyph, glyph.GetComponent<ActionGlyph>(), name + ".Glyph"); Wire(ref c.Halo, halo.GetComponent<SegmentRingGraphic>(), name + ".Halo"); Wire(ref c.HitRect, (RectTransform)hit, name + ".HitRect");
        Wire(ref c.Caption, text.GetComponent<Text>(), name + ".Caption"); Wire(ref c.CaptionPaper, (Graphic)cap.GetComponent<Image>(), name + ".CaptionPaper"); Wire(ref c.Lock, (Graphic)lockT.GetComponent<FieldLockGlyph>(), name + ".Lock");
        return c;
    }
    // 함께 · 망보기 · 조명 (시안 03): a row of small paper buttons under the second pawn (the board shows 2 or 3 of them).
    static RectTransform ChipRow(Transform parent, string name, Font font, Sprite paper)
    {
        var t = Node(parent, name, out bool made, typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        if (made)
        {
            Place(t, Middle, Bottom, Vector2.zero, new Vector2(300, 34));
            var h = t.GetComponent<HorizontalLayoutGroup>(); h.spacing = 4; h.childAlignment = TextAnchor.MiddleCenter; h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = h.childForceExpandHeight = false;
            var f = t.GetComponent<ContentSizeFitter>(); f.horizontalFit = f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            t.gameObject.SetActive(false);
        }
        string[] labels = { "함께", "망보기", "조명" };
        for (int i = 0; i < Chips; i++)
        {
            var chip = Node(t, "Chip_" + i, out made, typeof(Image), typeof(Button), typeof(LayoutElement));
            if (made)
            {
                var im = chip.GetComponent<Image>(); im.sprite = paper; im.color = new Color(.93f, .9f, .82f, 1); im.raycastTarget = true;
                var b = chip.GetComponent<Button>(); b.targetGraphic = im; b.transition = Selectable.Transition.None; var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
                var le = chip.GetComponent<LayoutElement>(); le.preferredWidth = 96; le.preferredHeight = 34; le.minWidth = 80;
            }
            var text = Node(chip, "Text", out made, typeof(Text));
            if (made) { Stretch(text); Style(text.GetComponent<Text>(), font, 20, Ink, TextAnchor.MiddleCenter, labels[i]); }
        }
        return (RectTransform)t;
    }
    // 사물 위 수색 진행 칸 (시안 02): a dark paper sized to its cells (FieldPawnBoard) with the code-drawn cells. Root pivot = bottom centre.
    static RectTransform ProgressStrip(Transform parent, string name, Sprite paper)
    {
        var t = Node(parent, name, out bool made, typeof(Image));
        if (made)
        {
            Place(t, Middle, Bottom, Vector2.zero, new Vector2(72, 30));
            var im = t.GetComponent<Image>(); im.sprite = paper; im.type = Image.Type.Sliced; im.color = Dark; im.raycastTarget = false;
            t.gameObject.SetActive(false);
        }
        var cells = Node(t, "Cells", out made, typeof(FieldSearchPips));
        if (made) { var r = (RectTransform)cells; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = Middle; cells.GetComponent<FieldSearchPips>().color = Gold; }
        // Size (2026-09-26, read against 시안 02 on the real screen): a 36 px paper, 20 px tall cells 26 px wide.
        var rt = (RectTransform)t; if (Mathf.Abs(rt.sizeDelta.y - 36) > .1f) { rt.sizeDelta = new Vector2(rt.sizeDelta.x, 36); log.Add(name + " 높이 36"); }
        var cr = (RectTransform)cells; if (cr.offsetMin != new Vector2(8, 8) || cr.offsetMax != new Vector2(-8, -8)) { cr.offsetMin = new Vector2(8, 8); cr.offsetMax = new Vector2(-8, -8); log.Add(name + " 칸 여백"); }
        var pips = cells.GetComponent<FieldSearchPips>(); if (pips.CellWidth != 26) { pips.CellWidth = 26; EditorUtility.SetDirty(pips); log.Add(name + " 칸 폭 26"); }
        return (RectTransform)t;
    }
    // A paper name tag ('윤서진 · 물자 상자 수색 1/2', '2턴 전 · 안에 없음', a chip's reason): sized to its text.
    static RectTransform Tag(Transform parent, string name, Font font, Sprite paper)
    {
        var t = Node(parent, name, out bool made, typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        if (made)
        {
            Place(t, Middle, Bottom, Vector2.zero, new Vector2(200, 32));
            var im = t.GetComponent<Image>(); im.sprite = paper; im.color = Dark; im.raycastTarget = false;
            var h = t.GetComponent<HorizontalLayoutGroup>(); h.padding = new RectOffset(12, 12, 4, 4); h.childAlignment = TextAnchor.MiddleCenter; h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = h.childForceExpandHeight = false;
            var f = t.GetComponent<ContentSizeFitter>(); f.horizontalFit = f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            t.gameObject.SetActive(false);
        }
        SlicedFit(t.GetComponent<Image>(), name);
        var text = Node(t, "Text", out made, typeof(Text));
        if (made) { Style(text.GetComponent<Text>(), font, 20, Cream, TextAnchor.MiddleCenter, "윤서진 · 행동 남음"); text.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow; }
        return (RectTransform)t;
    }

    // SettlementScreen nests the arrival panel: it must carry the board (inherited, never edited here).
    static void CheckScreen()
    {
        var screen = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath); if (!screen) return;
        foreach (var a in screen.GetComponentsInChildren<ExpeditionArrivalPanel>(true))
        {
            var node = a.Main ? a.Main.transform.Find(NodeName) : null; var board = node ? node.GetComponent<FieldPawnBoard>() : null;
            if (!board) { log.Add("경고: SettlementScreen 안 Main/" + NodeName + " 없음"); continue; }
            if (board.Arrival != a) log.Add("경고: SettlementScreen의 판 Arrival 연결이 다름");
            if (PrefabUtility.GetPropertyModifications(a.gameObject)?.Any(m => m.target is FieldPawnBoard && (m.propertyPath.StartsWith("Handles") || m.propertyPath.StartsWith("Pins") || m.propertyPath.StartsWith("GhostHandles"))) == true)
                log.Add("경고: SettlementScreen이 판의 칸 목록을 덮어씀");
        }
    }

    // ---- helpers ----
    static T Required<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new Exception("Missing asset: " + path);
    static T Comp<T>(GameObject g) where T : Component { var c = g.GetComponent<T>(); if (c) return c; c = g.AddComponent<T>(); log.Add(g.name + " +" + typeof(T).Name); return c; }
    // A paper sized to its text (2026-09-26 SlicedFit): Sliced, so the sprite's own width (footer paper 397 px) is never the
    // preferred width of the strip — the caption and tags hug their words as in 시안 01.
    static void SlicedFit(Image im, string what) { if (im && im.type != Image.Type.Sliced) { im.type = Image.Type.Sliced; log.Add(what + " 종이 글 폭에 맞춤"); } }
    static Transform Node(Transform parent, string name, out bool made, params Type[] components)
    {
        var t = parent.Find(name); made = !t; if (t) return t;
        var g = new GameObject(name, new[] { typeof(RectTransform) }.Concat(components).ToArray()); g.layer = parent.gameObject.layer; g.transform.SetParent(parent, false);
        log.Add(parent.name + "/" + name + " 생성"); return g.transform;
    }
    static void Place(Transform t, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size) { var r = (RectTransform)t; r.anchorMin = r.anchorMax = anchor; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size; }
    static void Stretch(Transform t) { var r = (RectTransform)t; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = Middle; r.offsetMin = r.offsetMax = Vector2.zero; }
    static void Style(Text t, Font font, int size, Color color, TextAnchor align, string text)
    {
        t.font = font; t.fontSize = size; t.color = color; t.alignment = align; t.text = text; t.raycastTarget = false; t.supportRichText = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.resizeTextForBestFit = false; t.lineSpacing = 1;
    }
    static void Wire<T>(ref T field, T value, string what) where T : Object { if (field == value) return; field = value; log.Add(what + " 연결"); }
    static void Pool<T>(ref T[] field, List<T> made, string what) where T : Object
    {
        var kept = (field ?? new T[0]).Where(x => x && !made.Contains(x)); var all = made.Concat(kept).ToArray();
        if (field == null || !field.SequenceEqual(all)) { field = all; log.Add(what + " " + all.Length); }
    }
    static void Retext(ref string field, string old, string now, string what) { if (field != old) return; field = now; log.Add(what + " '" + now + "'"); }
}
