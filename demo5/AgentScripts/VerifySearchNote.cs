using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// 수색 쪽지 · RETIRED (말 놓기, 2026-09-25, 기획/탐험-말놓기-조작-재설계.md 결정 6 · design §4): the search note is switched off by
// BuildRetireOldAssign (phase 1) and is deleted in phase 2 together with FieldSearchNote, FieldNoteTail, ExpeditionSearchPanel.Note.cs,
// BuildSearchNote and this script. What it covered moved: the role chips (함께 · 망보기 · 조명) → VerifyPawnRules.Board / VerifyPawnBoard;
// the first visit's one search = one turn → VerifyPawnRules.FirstVisit (it compares with RunFromNote while that exists); the guide →
// VerifyPawnFlow.Tutorial. These entries now guard the retirement: the note stays off and an object press never opens it.
// Wiring(): edit or play mode, after BuildRetireOldAssign.Run. FirstVisit() / Board(): play mode after VerifyFieldTurnPlan.Enter.
public static class VerifySearchNote
{
    const string ArrivalPath = "Assets/Prefabs/Settlement/ExpeditionArrivalPanel.prefab", ScreenPath = "Assets/Prefabs/Settlement/SettlementScreen.prefab";
    const string NodeName = "SearchNote";
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    public static string Wiring()
    {
        var done = new List<string>();
        foreach (var path in new[] { ArrivalPath, ScreenPath })
            foreach (var a in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<ExpeditionArrivalPanel>(true))
            {
                var node = a.Main.transform.Find(NodeName); var quick = a.Main.GetComponentInChildren<FieldQuickAssign>(true);
                Check(!node || !node.gameObject.activeSelf, path + ": Main/" + NodeName + " is on (run BuildRetireOldAssign.Run)");
                Check(!quick || !quick.Note && !quick.OpenNoteAfterAssign, path + ": the quick assign would open the note again");
                done.Add(path.Substring(path.LastIndexOf('/') + 1));
            }
        return "PASS retired: the search note is off in " + string.Join(", ", done);
    }

    static SettlementController Owner() { var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Run VerifyFieldTurnPlan.Enter first"); return c; }
    static async Task Until(Func<bool> done, int ms, string what) { var w = Stopwatch.StartNew(); while (!done()) { if (w.ElapsedMilliseconds > ms) throw new Exception("Timeout: " + what); await Task.Delay(30); } }
    static async Task<ExpeditionArrivalPanel> Arrive(SettlementController c, bool later)
    {
        var a = c.ArrivalPanel; if (c.Opening) c.Opening.State.Enabled = !later && c.Opening.State.Enabled;
        async Task Go() { if (c.ReturnPanel && c.ReturnPanel.IsOpen) { c.ReturnPanel.Close(); await Task.Delay(100); } Check(a.Begin(c.Campaign.Party.ToArray(), c.ExpeditionPanel.Destinations.First(d => d.Id == "mall")), "Departure"); await Until(() => a.IsOpen && !a.InTransit, 3000, "arrival"); await Task.Delay(200); }
        if (!a.IsOpen) await Go();
        if (later && !a.Threat.Active) { if (a.Popup.activeSelf) a.ClosePopup(); Check(a.FinishReturn(), "Home"); await Task.Delay(150); await Go(); }
        if (a.Popup.activeSelf) a.ClosePopup(); await Task.Delay(150);
        return a;
    }
    // Every object of the room pressed with no pawn held: the note never opens, nor the 07 window; no time passes.
    static async Task<string> Presses(ExpeditionArrivalPanel a, string when)
    {
        var note = FieldSearchNote.For(a); int turns = a.Rooms.Turns, pressed = 0;
        await Until(() => FieldPawnTest.Ready(a), 3000, "board ready");
        for (int i = 0; i < a.Objects.Length && i < a.Loot.Sites.Length; i++)
        {
            if (i == 3 || !a.Objects[i].gameObject.activeInHierarchy || a.Loot.Peek(i, out var s) && s.Complete) continue;
            a.Objects[i].onClick.Invoke(); await Task.Delay(120); pressed++;
            Check(!(note && note.IsOpen) && !a.Search.IsOpen && a.Rooms.Turns == turns, when + ": pressing " + a.ObjectNames[i] + " opened the note or the 07 window");
        }
        return when + " " + pressed + " objects";
    }
    public static async Task<string> FirstVisit()
    {
        FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c = Owner(); var a = await Arrive(c, false); Check(a.Threat.State != null && a.Threat.State.Asleep, "Needs the first visit (a fresh game)");
        return "PASS retired: " + await Presses(a, "first visit") + ", no note";
    }
    public static async Task<string> Board()
    {
        FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c = Owner(); var a = await Arrive(c, true); Check(a.Threat.Active, "Needs a later visit");
        return "PASS retired: " + await Presses(a, "later visit") + ", no note";
    }
    public static async Task<string> All() => string.Join("\n", new[] { Wiring(), await FirstVisit(), await Board() });
}
