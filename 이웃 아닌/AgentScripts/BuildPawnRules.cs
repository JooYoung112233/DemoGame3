using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// 말 놓기 · 규칙 (기획/탐험-말놓기-조작-재설계.md, 2026-09-25 · Group B): the prefab side of the pawn rules on ExpeditionArrivalPanel.prefab.
//  1. '모두 숨죽이기' is gone (user decision 2: a turn with nobody placed hushes, FieldIdleConfirm asks). ExpeditionSiteThreat.Hush is
//     cleared and its node stays, inactive, as the single 'Main/Hush' (BuildExplorationHudTray lays it out and BuildFieldPlanMarkers
//     keeps it clear by that path); extra copies a re-run of BuildFieldStrategy made are removed.
//  2. The door popup's listen button (FieldTurnPlanner.ListenButton / ListenTitle / ListenSubtitle) is cleared: listening is a pawn at a
//     door now (phase 1 of its removal; the popup node itself stays inactive, BuildFieldListen made it so).
//  3. FieldTurnPlanner.AutoFillHelpers off: a helper is only a pawn the player placed.
//  4. The second-visit introduction's last line (ExpeditionSiteThreat.IntroBody) speaks of pawns, not assignments (only while it is
//     the old default line; an Inspector-edited text is kept and reported).
//  5. FieldAutoAdvance.Warning → the FieldTurnWarning on the same panel (the first-visit encounter threshold it stops at).
//  6. The idle question's all-idle body is the user's text (reported, never overwritten).
// Idempotent ('Already applied.'). Run after every existing builder (after BuildIdleConfirm, BuildExplorationHudTray, BuildTurnClockPaper,
// BuildMemberActionSlot, BuildSearchNote, BuildSiteNoise, BuildAssignmentBubble, BuildFieldTurnFlow, BuildFieldPlanMarkers, BuildFieldListen,
// BuildFieldStrategy), then BuildPawnBoard (A), then BuildRetireOldAssign (C). Re-running BuildFieldStrategy or BuildFieldListen needs a
// re-run of this one; BuildFieldTurnPlan requires the hush button, so run the chain from BuildFieldStrategy when rebuilding it.
// Checked by VerifyPawnRules.Wiring.
public static class BuildPawnRules
{
    const string P = "Assets/Prefabs/Settlement/", ArrivalPath = P + "ExpeditionArrivalPanel.prefab", ScreenPath = P + "SettlementScreen.prefab";
    public const string OldIntroLine = "대원마다 행동을 배정하고 '턴 진행'을 누르세요.";
    public const string NewIntroLine = "대원 말을 사물이나 문에 놓고 '턴 진행'을 누르세요.";
    public const string IdleBodyAll = "아무도 할 일이 없습니다.\n모두 숨죽이고 진행할까요?";

    public static string Run()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Stop Play and preserve the scene first");
        var log = new List<string>(); var notes = new List<string>();
        var root = PrefabUtility.LoadPrefabContents(ArrivalPath);
        try
        {
            var arrival = root.GetComponent<ExpeditionArrivalPanel>(); if (!arrival || !arrival.Main) throw new Exception("ExpeditionArrivalPanel/Main missing");
            var planner = root.GetComponent<FieldTurnPlanner>(); if (!planner || !planner.TurnButton) throw new Exception("Run BuildFieldTurnPlan first (FieldTurnPlanner on the panel root with its TurnButton)");
            var threat = arrival.Threat; if (!threat || threat.Planner != planner) throw new Exception("ExpeditionSiteThreat missing or not wired to the planner (BuildFieldStrategy / BuildFieldTurnPlan)");
            var idle = root.GetComponent<FieldIdleConfirm>(); if (!idle) throw new Exception("Run BuildIdleConfirm first");

            // 1. The hush button: one inactive 'Hush' node, no reference.
            var main = arrival.Main.transform; var hushes = new List<GameObject>();
            foreach (Transform t in main) if (t.name == "Hush") hushes.Add(t.gameObject);
            if (threat.Hush && !hushes.Contains(threat.Hush.gameObject)) hushes.Add(threat.Hush.gameObject);
            if (threat.Hush) { threat.Hush = null; EditorUtility.SetDirty(threat); log.Add("ExpeditionSiteThreat.Hush cleared ('모두 숨죽이기' removed)"); }
            var keep = hushes.FirstOrDefault(h => h.transform.parent == main);
            foreach (var h in hushes)
            {
                if (h == keep) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(h)) { if (h.activeSelf) { h.SetActive(false); log.Add("extra hush copy off (" + h.name + ")"); } continue; }
                Object.DestroyImmediate(h); log.Add("extra hush copy removed");
            }
            if (keep && keep.activeSelf) { keep.SetActive(false); log.Add("Main/Hush inactive"); }

            // 2. The door popup's listen button.
            if (planner.ListenButton || planner.ListenTitle || planner.ListenSubtitle)
            {
                planner.ListenButton = null; planner.ListenTitle = null; planner.ListenSubtitle = null; EditorUtility.SetDirty(planner); log.Add("FieldTurnPlanner.ListenButton cleared (door popup listen off)");
            }
            // 3. Helpers are only placed pawns.
            if (planner.AutoFillHelpers) { planner.AutoFillHelpers = false; EditorUtility.SetDirty(planner); log.Add("FieldTurnPlanner.AutoFillHelpers off"); }

            // 4. The introduction's last line.
            if (!string.IsNullOrEmpty(threat.IntroBody) && threat.IntroBody.Contains(OldIntroLine))
            {
                threat.IntroBody = threat.IntroBody.Replace(OldIntroLine, NewIntroLine); EditorUtility.SetDirty(threat); log.Add("IntroBody: " + NewIntroLine);
            }
            else if (string.IsNullOrEmpty(threat.IntroBody) || !threat.IntroBody.Contains(NewIntroLine)) notes.Add("IntroBody edited in the Inspector (kept): check its last line speaks of pawns");

            // 5. '계속 진행' reads the warning strip's first-visit threshold.
            var auto = arrival.Main.GetComponentInChildren<FieldAutoAdvance>(true); var warning = arrival.Main.GetComponentInChildren<FieldTurnWarning>(true);
            if (!auto) throw new Exception("Run BuildFieldTurnFlow.Run first (Main/TurnFlow FieldAutoAdvance)");
            if (warning && auto.Warning != warning) { auto.Warning = warning; EditorUtility.SetDirty(auto); log.Add("FieldAutoAdvance.Warning → " + warning.name); }
            if (!warning) notes.Add("no FieldTurnWarning under Main (BuildTurnClockPaper): '계속 진행' uses 25%");

            // 6. The all-idle question (user decision 2) is the user's text.
            if (idle.BodyAll != IdleBodyAll) notes.Add("FieldIdleConfirm.BodyAll edited in the Inspector (kept): '" + idle.BodyAll.Replace("\n", " / ") + "'");

            if (log.Count > 0) PrefabUtility.SaveAsPrefabAsset(root, ArrivalPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        // The nested instance in the settlement screen inherits all of it; say so if an instance override hides it.
        var screen = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath);
        if (screen)
            foreach (var a in screen.GetComponentsInChildren<ExpeditionArrivalPanel>(true))
            {
                var pl = a.GetComponent<FieldTurnPlanner>(); var t = a.Threat;
                if (t && t.Hush) notes.Add("SettlementScreen instance overrides ExpeditionSiteThreat.Hush (the hush button would come back)");
                if (pl && pl.ListenButton) notes.Add("SettlementScreen instance overrides FieldTurnPlanner.ListenButton");
                if (pl && pl.AutoFillHelpers) notes.Add("SettlementScreen instance overrides FieldTurnPlanner.AutoFillHelpers (on)");
            }
        string tail = notes.Count > 0 ? " · NOTE " + string.Join("; ", notes.Distinct()) : "";
        return (log.Count == 0 ? "Already applied." : "Applied: " + string.Join("; ", log)) + tail;
    }
}
