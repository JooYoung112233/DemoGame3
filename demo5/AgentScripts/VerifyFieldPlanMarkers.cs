using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// 사물 위 배정 말풍선 · RETIRED (말 놓기, 2026-09-25, 기획/탐험-말놓기-조작-재설계.md 결정 6 · design §4): the bubbles over exploration
// objects are switched off by BuildRetireOldAssign (phase 1: FieldPlanTargetMarkers disabled, its bubbles inactive, the node kept because
// older builders read it); phase 2 deletes FieldPlanTargetMarkers, BuildFieldPlanMarkers and this script. A pawn standing at the object is
// the assignment now (VerifyPawnBoard.Walking / Doors). The room area and its keep-clear lists moved to FieldRoomArea on Main.
// The settlement's own AssignmentBubble markers stay (VerifyAssignmentMarkers). These entries guard the retirement: no bubble shows
// whatever is placed (a search, a listener, a gathered move, a corridor search).
// Wiring(): edit or play mode, after BuildRetireOldAssign.Run. Play mode after VerifyFieldTurnPlan.Enter: Search → ListenObserve → Move →
// Corridor (or All).
public static class VerifyFieldPlanMarkers
{
    const string ArrivalPath = "Assets/Prefabs/Settlement/ExpeditionArrivalPanel.prefab", ScreenPath = "Assets/Prefabs/Settlement/SettlementScreen.prefab";
    const int A = FieldSiteState.Arcade, C = FieldSiteState.Corridor;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    public static string Wiring()
    {
        var done = new List<string>();
        foreach (var path in new[] { ArrivalPath, ScreenPath })
            foreach (var a in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<ExpeditionArrivalPanel>(true))
            {
                var mk = a.Main.GetComponentInChildren<FieldPlanTargetMarkers>(true); var area = a.Main.GetComponent<FieldRoomArea>();
                Check(area, path + ": FieldRoomArea on Main (run BuildRetireOldAssign.Run)");
                if (mk)
                {
                    Check(!mk.enabled && mk.GetComponentsInChildren<AssignmentBubble>(true).All(b => !b.gameObject.activeSelf), path + ": the bubbles over objects are off");
                    Check(area.RoomArea == mk.RoomArea && area.KeepClear.SequenceEqual(mk.KeepClear.Where(r => r)), path + ": FieldRoomArea carries the bubbles' room area and keep-clear list");
                }
                done.Add(path.Substring(path.LastIndexOf('/') + 1));
            }
        var screen = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath);
        Check(screen.GetComponentsInChildren<SettlementAssignmentMarkers>(true).All(m => m.enabled), "The settlement's own assignment bubbles stay");
        return "PASS retired: exploration bubbles off, FieldRoomArea on Main, settlement bubbles kept (" + string.Join(", ", done) + ")";
    }

    static SettlementController Owner() { var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Run VerifyFieldTurnPlan.Enter first"); return c; }
    static async Task Until(Func<bool> done, int ms, string what) { var w = Stopwatch.StartNew(); while (!done()) { if (w.ElapsedMilliseconds > ms) throw new Exception("Timeout: " + what); await Task.Delay(30); } }
    // A later visit in the arcade with nothing placed (home once after the sleeping first visit, the intro closed).
    static async Task<ExpeditionArrivalPanel> Later(SettlementController c)
    {
        FieldIdleConfirm.AutoAccept = true; var a = c.ArrivalPanel; if (c.Opening) c.Opening.State.Enabled = false;
        async Task Go() { if (c.ReturnPanel && c.ReturnPanel.IsOpen) { c.ReturnPanel.Close(); await Task.Delay(100); } Check(a.Begin(c.Campaign.Party.ToArray(), c.ExpeditionPanel.Destinations.First(d => d.Id == "mall")), "Departure"); await Until(() => a.IsOpen && !a.InTransit, 3000, "arrival"); await Task.Delay(200); }
        if (a.IsOpen) { if (a.Loot.IsOpen) a.Loot.Escape(); if (a.Search.IsOpen) a.Search.Close(); if (a.Popup.activeSelf) a.ClosePopup(); await Until(() => !a.InTransit, 6000, "walk"); }
        if (a.IsOpen && !(a.Threat.Active && a.Rooms.CurrentRoom == A)) { Check(a.FinishReturn(), "Home"); await Task.Delay(150); }
        if (!a.IsOpen) await Go();
        if (!a.Threat.Active) { if (a.Popup.activeSelf) a.ClosePopup(); Check(a.FinishReturn(), "Home after the first visit"); await Task.Delay(150); await Go(); }
        await Until(() => a.Popup.activeSelf || a.Threat.IntroAcknowledged, 3000, "intro"); if (a.Popup.activeSelf) a.ClosePopup();
        a.Threat.ReviewWake(0, 0, 0); await Task.Delay(250); if (a.Popup.activeSelf) a.ClosePopup();
        await Until(() => FieldPawnTest.Ready(a), 3000, "board ready"); return a;
    }
    static async Task NoBubble(ExpeditionArrivalPanel a, string when)
    {
        await Task.Delay(700); // longer than the bubbles' poll interval
        var mk = a.Main.GetComponentInChildren<FieldPlanTargetMarkers>(true);
        Check(!mk || !mk.isActiveAndEnabled && mk.GetComponentsInChildren<AssignmentBubble>(false).Length == 0, when + ": a bubble shows over the room");
    }
    public static async Task<string> Search()
    {
        var c = Owner(); var a = await Later(c); int site = Enumerable.Range(0, 3).First(i => string.IsNullOrEmpty(a.Loot.Sites[i].RequiredTool) && !(a.Loot.Peek(i, out var s) && s.Complete));
        Check(FieldPawnTest.Coop(a, site), "Two pawns on " + a.ObjectNames[site]); await NoBubble(a, "search");
        FieldPawnTest.Clear(a); return "PASS retired: no bubble over a search (" + a.ObjectNames[site] + ")";
    }
    public static async Task<string> ListenObserve()
    {
        var c = Owner(); var a = await Later(c);
        Check(FieldPawnTest.Listen(a, 0, C), "A pawn at the exit"); await NoBubble(a, "listen");
        var story = a.Story; bool observed = story && story.Clue && story.Clue.gameObject.activeInHierarchy && story.State.Stage == 0 && FieldPawnTest.Observe(a, 1);
        if (observed) await NoBubble(a, "observe");
        FieldPawnTest.Clear(a); return "PASS retired: no bubble over a listener" + (observed ? " or the trace" : "");
    }
    public static async Task<string> Move()
    {
        var c = Owner(); var a = await Later(c);
        Check(FieldPawnTest.Gather(a, C) && a.Rooms.HasQueuedMove, "Everyone at the door"); await NoBubble(a, "gathered move");
        FieldPawnTest.Clear(a); return "PASS retired: no bubble over a gathered move";
    }
    public static async Task<string> Corridor()
    {
        var c = Owner(); var a = await Later(c);
        Check(await FieldPawnTest.Move(a, C), "Into the corridor"); await Until(() => FieldPawnTest.Ready(a), 3000, "board ready");
        int site = Enumerable.Range(0, a.Loot.Sites.Length).FirstOrDefault(i => a.Loot.IsSiteInCurrentRoom(i) && string.IsNullOrEmpty(a.Loot.Sites[i].RequiredTool) && (!a.Threat || i != a.Threat.DenSite) && !(a.Loot.Peek(i, out var s) && s.Complete));
        if (site > 0) { Check(FieldPawnTest.Lead(a, 0, site), "A pawn on " + a.ObjectNames[site]); await NoBubble(a, "corridor search"); }
        FieldPawnTest.Clear(a); Check(await FieldPawnTest.Move(a, A), "Back to the arcade");
        return "PASS retired: no bubble in the corridor";
    }
    public static async Task<string> All() => string.Join("\n", new[] { Wiring(), await Search(), await ListenObserve(), await Move(), await Corridor() });
}
