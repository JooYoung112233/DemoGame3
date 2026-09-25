using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 말 놓기 · 옛 배정 층 끄기 (2026-09-25, 기획/탐험-말놓기-조작-재설계.md 결정 6 '정리(없앰)' · design §4 phase 1 · Group C). The old ways to
// assign are switched off in the prefabs; their code still compiles (phase 2 deletes it once every verify passes):
//  1. 수색 쪽지: Main/SearchNote inactive (its OnDisable gives the press hook back; the pawn board takes it); FieldQuickAssign.Note
//     cleared and OpenNoteAfterAssign off (Commit would open the note again).
//  2. 사물 위 말풍선: FieldPlanTargetMarkers disabled and its bubbles inactive; the node stays (older builders read it). Its room area and
//     keep-clear lists are copied to FieldRoomArea on Main. The settlement's AssignmentBubble markers are not touched.
//  3. 카드 끌기 · 빛나는 곳 · 방 가림 · 고른 대원 안내: FieldQuickAssign disabled, its Veil / BubbleHits / Glows / SelectHint / DragToken
//     inactive, FieldMemberActionSlots.Quick cleared (the slots and Main/MemberActions stay: the card slots and the rings are kept).
//  4. 문 확인창의 귀 대기 · 이동 버튼: FieldTurnPlanner.ListenButton cleared (BuildPawnRules does it; checked here); the popup confirm moves
//     at once (code), '다음 턴 이동' is everyone placed at a door.
//  5. '모두 숨죽이기': Main/Hush inactive, ExpeditionSiteThreat.Hush cleared (BuildPawnRules; checked here).
//  6. 07 창: read only (ExpeditionSearchPanel.ReadOnlyWindow on). ExpeditionSearchPanel.prefab hides the member cards, the role row and
//     the confirm; the old '담당자' heading line shows who is placed (ExpeditionSearchPanel.Placed). 돌아가기 stays bottom left.
//  7. 튜토리얼 안내 종이 (SettlementScreen → SettlementTutorialGuide.FieldPosition): top centre, off the member cards, the pawns and
//     the '이번 턴' panel (design §6: a serialized value, not code).
// Also checks that the SettlementScreen's nested arrival panel inherits all of it (reported, not overridden). Idempotent ('Already
// applied.'). Builder order: every existing builder → BuildPawnRules (B) → BuildPawnBoard (A) → BuildRetireOldAssign (this, LAST: it
// must follow BuildMemberActionSlot, BuildSearchNote, BuildFieldPlanMarkers, BuildFieldListen, BuildFieldStrategy, BuildSiteNoise and
// BuildExplorationHudTray, which re-add, re-wire or re-place what it switches off). Nothing under HudTray or Members changes, so
// BuildLaundryStoryVisit need not re-run. Checked by VerifyPawnFlow.Wiring.
public static class BuildRetireOldAssign
{
    const string P = "Assets/Prefabs/Settlement/", ArrivalPath = P + "ExpeditionArrivalPanel.prefab", ScreenPath = P + "SettlementScreen.prefab", SearchPath = P + "ExpeditionSearchPanel.prefab";
    public const string NoteNode = "SearchNote", MarkersNode = "PlanTargetMarkers", ActionsNode = "MemberActions", BoardNode = "PawnBoard", HushNode = "Hush";
    // FieldQuickAssign's own children (BuildMemberActionSlot).
    public static readonly string[] QuickChildren = { "Veil", "BubbleHits", "Glows", "SelectHint", "DragToken" };
    // The 07 window's assigning controls (ExpeditionSearchPanel.prefab, under Workspace).
    public static readonly string[] SearchHidden = { "Workers", "DutyHeading", "Duty_0", "Duty_1", "Duty_2", "Choose" };
    public const string PlacedNode = "WorkerHeading", PlacedDefault = "놓인 대원 없음 · 대원 말을 이 사물에 놓으세요";
    // The tutorial's field banner: top centre of the 1920×1080 screen (between the place paper and the time paper).
    public static readonly Vector2 GuidePosition = new Vector2(720, -24), GuideSize = new Vector2(480, 148);
    static readonly List<string> log = new List<string>(), notes = new List<string>();

    public static string Run()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Stop Play and preserve the scene first");
        log.Clear(); notes.Clear();
        Edit(SearchPath, SearchWindow);
        Edit(ArrivalPath, root => Arrival(root.GetComponent<ExpeditionArrivalPanel>() ?? throw new Exception("ExpeditionArrivalPanel missing")));
        Edit(ScreenPath, ScreenPrefab);
        AssetDatabase.SaveAssets();
        string tail = notes.Count > 0 ? " · NOTE " + string.Join("; ", notes.Distinct()) : "";
        return (log.Count == 0 ? "Already applied." : "Applied: " + string.Join("; ", log.Distinct())) + tail;
    }

    static void Edit(string path, Action<GameObject> edit)
    {
        var root = PrefabUtility.LoadPrefabContents(path); int before = log.Count;
        try { edit(root); if (log.Count > before) PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static void Off(GameObject g, string what) { if (g && g.activeSelf) { g.SetActive(false); log.Add(what + " off"); } }
    static void Disable(Behaviour b, string what) { if (b && b.enabled) { b.enabled = false; EditorUtility.SetDirty(b); log.Add(what + " disabled"); } }

    // 6. The 07 window only shows (ExpeditionSearchPanel.ReadOnly.cs).
    static void SearchWindow(GameObject root)
    {
        var s = root.GetComponent<ExpeditionSearchPanel>() ?? throw new Exception("ExpeditionSearchPanel missing on " + SearchPath);
        var work = s.Workspace ? s.Workspace.transform : root.transform.Find("Workspace"); if (!work) throw new Exception("ExpeditionSearchPanel/Workspace missing");
        foreach (var n in SearchHidden) { var t = work.Find(n); if (t) Off(t.gameObject, "07 " + n); else notes.Add("07: no Workspace/" + n); }
        if (s.Duties != null) foreach (var d in s.Duties) if (d) Off(d.gameObject, "07 " + d.name);
        if (s.Choose) Off(s.Choose.gameObject, "07 Choose");
        if (s.MemberContent && s.MemberContent.parent && s.MemberContent.parent.name != "Workspace") Off(s.MemberContent.parent.gameObject, "07 member row");
        var heading = work.Find(PlacedNode); var text = heading ? heading.GetComponent<Text>() : null;
        if (!text) throw new Exception("07: Workspace/" + PlacedNode + " (a Text) missing: run BuildSiteNoise.Run first");
        if (!heading.gameObject.activeSelf) { heading.gameObject.SetActive(true); log.Add("07 " + PlacedNode + " on (who is placed)"); }
        if (s.Placed != text) { s.Placed = text; EditorUtility.SetDirty(s); log.Add("07 Placed → " + PlacedNode); }
        if (text.text != PlacedDefault && (text.text.Contains("담당자") || string.IsNullOrEmpty(text.text))) { text.text = PlacedDefault; EditorUtility.SetDirty(text); log.Add("07 placed line default text"); }
        if (!s.ReadOnlyWindow) { s.ReadOnlyWindow = true; EditorUtility.SetDirty(s); log.Add("07 read only"); }
    }

    static void Arrival(ExpeditionArrivalPanel a)
    {
        if (!a.Main) throw new Exception("ExpeditionArrivalPanel/Main missing");
        var main = a.Main.transform;
        var board = main.Find(BoardNode); if (!board || !board.GetComponent<FieldPawnBoard>()) throw new Exception("Run BuildPawnBoard.Run first (Main/" + BoardNode + ")");
        var planner = a.GetComponent<FieldTurnPlanner>(); var threat = a.Threat;
        var actions = main.Find(ActionsNode); var quick = actions ? actions.GetComponent<FieldQuickAssign>() : main.GetComponentInChildren<FieldQuickAssign>(true);
        var slots = actions ? actions.GetComponent<FieldMemberActionSlots>() : main.GetComponentInChildren<FieldMemberActionSlots>(true);

        // 1. The search note.
        var note = main.Find(NoteNode); if (note) Off(note.gameObject, "Main/" + NoteNode);
        if (quick && (quick.Note || quick.OpenNoteAfterAssign)) { quick.Note = null; quick.OpenNoteAfterAssign = false; EditorUtility.SetDirty(quick); log.Add("FieldQuickAssign.Note cleared"); }

        // 2. The assignment bubbles over objects (their data to FieldRoomArea first).
        var markersNode = main.Find(MarkersNode); var markers = markersNode ? markersNode.GetComponent<FieldPlanTargetMarkers>() : null;
        var area = a.Main.GetComponent<FieldRoomArea>(); if (!area) { area = a.Main.gameObject.AddComponent<FieldRoomArea>(); log.Add("Main +FieldRoomArea"); }
        if (markers)
        {
            if (area.RoomArea != markers.RoomArea) { area.RoomArea = markers.RoomArea; EditorUtility.SetDirty(area); log.Add("FieldRoomArea.RoomArea ← bubbles " + markers.RoomArea); }
            var clear = (markers.KeepClear ?? new RectTransform[0]).Where(r => r).ToArray(); var always = (markers.KeepClearAlways ?? new RectTransform[0]).Where(r => r).ToArray();
            if (area.KeepClear == null || !area.KeepClear.SequenceEqual(clear)) { area.KeepClear = clear; EditorUtility.SetDirty(area); log.Add("FieldRoomArea.KeepClear ×" + clear.Length); }
            if (area.KeepClearAlways == null || !area.KeepClearAlways.SequenceEqual(always)) { area.KeepClearAlways = always; EditorUtility.SetDirty(area); log.Add("FieldRoomArea.KeepClearAlways ×" + always.Length); }
            Disable(markers, "FieldPlanTargetMarkers");
            if (markers.Bubbles != null) foreach (var b in markers.Bubbles) if (b) Off(b.gameObject, MarkersNode + "/" + b.name);
            foreach (Transform t in markersNode) Off(t.gameObject, MarkersNode + "/" + t.name);
        }
        else notes.Add("no Main/" + MarkersNode + " (FieldRoomArea keeps its own values)");

        // 3. Quick assign (card press / drag, glowing targets, veil, hint).
        if (quick)
        {
            Disable(quick, "FieldQuickAssign");
            var host = quick.transform;
            foreach (var n in QuickChildren) { var t = host.Find(n); if (t) Off(t.gameObject, ActionsNode + "/" + n); }
            if (quick.Veil) Off(quick.Veil.gameObject, "quick veil");
            if (quick.Hint) Off(quick.Hint.gameObject, "quick hint");
            if (quick.DragToken) Off(quick.DragToken.gameObject, "quick drag token");
        }
        if (slots && slots.Quick) { slots.Quick = null; EditorUtility.SetDirty(slots); log.Add("FieldMemberActionSlots.Quick cleared"); }
        if (slots && !slots.PawnBoard) notes.Add("FieldMemberActionSlots.PawnBoard is empty (BuildPawnBoard wires it)");
        if (actions && !actions.gameObject.activeSelf) { actions.gameObject.SetActive(true); log.Add("Main/" + ActionsNode + " on (the card slots stay)"); }

        // 4. The door popup's listen button.
        if (planner && (planner.ListenButton || planner.ListenTitle || planner.ListenSubtitle)) { planner.ListenButton = null; planner.ListenTitle = null; planner.ListenSubtitle = null; EditorUtility.SetDirty(planner); log.Add("FieldTurnPlanner.ListenButton cleared"); }
        if (planner && planner.AutoFillHelpers) notes.Add("FieldTurnPlanner.AutoFillHelpers is on (BuildPawnRules turns it off)");

        // 5. '모두 숨죽이기'.
        if (threat && threat.Hush) { Off(threat.Hush.gameObject, "hush button"); threat.Hush = null; EditorUtility.SetDirty(threat); log.Add("ExpeditionSiteThreat.Hush cleared"); }
        foreach (Transform t in main) if (t.name == HushNode) Off(t.gameObject, "Main/" + HushNode);

        // 6. The nested 07 window must inherit the read-only look (reported: an instance override would show the old controls).
        if (a.Search)
        {
            var work = a.Search.Workspace ? a.Search.Workspace.transform : null;
            if (work) foreach (var n in SearchHidden) { var t = work.Find(n); if (t && t.gameObject.activeSelf) notes.Add("the arrival panel's 07 overrides " + n + " (active)"); }
            if (!a.Search.ReadOnlyWindow) notes.Add("the arrival panel's 07 overrides ReadOnlyWindow (off)");
        }
    }

    // 7. SettlementScreen: the tutorial banner, and the nested arrival panel inherits every step above.
    static void ScreenPrefab(GameObject root)
    {
        var guide = root.GetComponentInChildren<SettlementTutorialGuide>(true);
        if (guide && (guide.FieldPosition != GuidePosition || guide.FieldSize != GuideSize)) { guide.FieldPosition = GuidePosition; guide.FieldSize = GuideSize; EditorUtility.SetDirty(guide); log.Add("tutorial field banner → top centre " + GuidePosition); }
        if (!guide) notes.Add("SettlementScreen has no SettlementTutorialGuide");
        foreach (var a in root.GetComponentsInChildren<ExpeditionArrivalPanel>(true))
        {
            var main = a.Main ? a.Main.transform : null; if (!main) continue;
            var note = main.Find(NoteNode); if (note && note.gameObject.activeSelf) notes.Add("SettlementScreen overrides Main/" + NoteNode + " (active)");
            var markers = main.GetComponentInChildren<FieldPlanTargetMarkers>(true); if (markers && markers.enabled) notes.Add("SettlementScreen overrides FieldPlanTargetMarkers (enabled)");
            var quick = main.GetComponentInChildren<FieldQuickAssign>(true); if (quick && quick.enabled) notes.Add("SettlementScreen overrides FieldQuickAssign (enabled)");
            var slots = main.GetComponentInChildren<FieldMemberActionSlots>(true); if (slots && slots.Quick) notes.Add("SettlementScreen overrides FieldMemberActionSlots.Quick");
            if (a.Threat && a.Threat.Hush) notes.Add("SettlementScreen overrides ExpeditionSiteThreat.Hush");
            if (!a.Main.GetComponent<FieldRoomArea>()) notes.Add("SettlementScreen's Main has no FieldRoomArea");
            if (!main.Find(BoardNode)) notes.Add("SettlementScreen's Main has no " + BoardNode);
            if (a.Search && !a.Search.ReadOnlyWindow) notes.Add("SettlementScreen overrides the 07 ReadOnlyWindow (off)");
        }
    }
}
