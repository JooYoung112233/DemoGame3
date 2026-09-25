using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 할 일이 없는 대원 확인 (2026-09-25, 기획/탐험-화면정리와-행동칸-1차.md §4): FieldIdleConfirm on the ExpeditionArrivalPanel.prefab
// root, next to FieldTurnPlanner (whose Run asks it first through the one-line hook), wired to the panel and to '계속 진행'
// (Main/TurnFlow FieldAutoAdvance, which asks it once when switched on). No node and no art: the question uses the panel's
// existing popup (the door popup frame: title, body, confirm, back). Base prefab only: SettlementScreen's nested instance
// inherits the component and its references (reported here, checked by VerifyIdleConfirm.Wiring).
// Idempotent (the second run says 'Already applied.'); Inspector texts are kept. Run after BuildFieldTurnPlan and
// BuildFieldTurnFlow.Run; its place among the later layout builders does not matter (it moves nothing).
public static class BuildIdleConfirm
{
    const string ArrivalPath = "Assets/Prefabs/Settlement/ExpeditionArrivalPanel.prefab", ScreenPath = "Assets/Prefabs/Settlement/SettlementScreen.prefab";

    public static string Run()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Stop Play and preserve the scene first");
        var log = new List<string>();
        var root = PrefabUtility.LoadPrefabContents(ArrivalPath);
        try
        {
            var arrival = root.GetComponent<ExpeditionArrivalPanel>(); if (!arrival || !arrival.Main) throw new Exception("ExpeditionArrivalPanel/Main missing");
            if (!arrival.Popup || !arrival.PopupTitle || !arrival.PopupBody || !arrival.ReturnConfirm || !arrival.PopupBack) throw new Exception("The arrival panel's popup frame (Popup, PopupTitle, PopupBody, ReturnConfirm, PopupBack) is not wired");
            var planner = root.GetComponent<FieldTurnPlanner>(); if (!planner || !planner.TurnButton) throw new Exception("Run BuildFieldTurnPlan first (FieldTurnPlanner on the panel root with its TurnButton)");
            var auto = arrival.Main.GetComponentInChildren<FieldAutoAdvance>(true); if (!auto) throw new Exception("Run BuildFieldTurnFlow.Run first (Main/TurnFlow FieldAutoAdvance)");
            var confirm = root.GetComponent<FieldIdleConfirm>(); if (!confirm) { confirm = root.AddComponent<FieldIdleConfirm>(); log.Add("+FieldIdleConfirm on " + root.name); }
            if (confirm.Arrival != arrival) { confirm.Arrival = arrival; EditorUtility.SetDirty(confirm); log.Add("FieldIdleConfirm.Arrival"); }
            if (confirm.Auto != auto) { confirm.Auto = auto; EditorUtility.SetDirty(confirm); log.Add("FieldIdleConfirm.Auto → " + auto.name); }
            if (auto.IdleConfirm != confirm) { auto.IdleConfirm = confirm; EditorUtility.SetDirty(auto); log.Add("FieldAutoAdvance.IdleConfirm"); }
            if (log.Count > 0) PrefabUtility.SaveAsPrefabAsset(root, ArrivalPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        // The nested instance in the settlement screen inherits it; say so if an instance override hides it.
        var screen = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath); var notes = new List<string>();
        if (screen)
            foreach (var a in screen.GetComponentsInChildren<ExpeditionArrivalPanel>(true))
            {
                var c = a.GetComponent<FieldIdleConfirm>();
                if (!(c && c.Arrival == a && c.Auto && c.Auto.IdleConfirm == c)) notes.Add("SettlementScreen instance NOT wired (" + (c ? "references overridden" : "component missing") + "): check VerifyIdleConfirm.Wiring");
            }
        string tail = notes.Count > 0 ? " · WARNING " + string.Join(", ", notes.Distinct()) : "";
        return (log.Count == 0 ? "Already applied." : "Applied: " + string.Join("; ", log)) + tail;
    }
}
