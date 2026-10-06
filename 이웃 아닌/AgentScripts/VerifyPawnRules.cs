using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 말 놓기 · 규칙 (기획/탐험-말놓기-조작-재설계.md, 2026-09-25 · Group B). Entry points:
//   Rules()      edit mode, pure: FieldTurnPlan with fake facts (AutoFill off, Unassign, gathering, bag use, a running 함께 search),
//                FieldDoorLog, and the first-visit encounter chance of the tutorial routes (prefab values).
//   Wiring()     edit mode, after BuildPawnRules: the prefab data it sets (no hush button, no popup listen button, no auto helpers,
//                the introduction line, '계속 진행' reads the warning strip), and the SettlementScreen instance inherits it.
//   FirstVisit() play mode, after VerifyFieldTurnPlan.Enter (a new game in the settlement): the first visit on the board — '턴 진행'
//                shows, one co-op search = one turn with the old RunFromNote numbers (compared while RunFromNote still exists),
//                doors only gather, two searches in one turn = one search turn, the random encounter through '턴 진행' clears the
//                plan and, after it, the completed object's finds open (no 07, no note).
//   Board()      play mode, after VerifyFieldTurnPlan.Enter: the awake board — a hush turn with nobody placed (the idle question's text),
//                listening every turn into the door log ('방금' / 'N턴 전'), gathering with the listener, the pawn rules on leaving,
//                the locked storage door (option A), a bag item as the member's next turn, and preview = result on every turn.
// FieldIdleConfirm.AutoAccept is set by the play-mode entries (their turns run with idle members).
public static class VerifyPawnRules
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    const int A = FieldSiteState.Arcade, C = FieldSiteState.Corridor, S = FieldSiteState.Storage, D = FieldSiteState.Den;
    const string NewIntroLine = "대원 말을 사물이나 문에 놓고 '턴 진행'을 누르세요.", IdleBodyAll = "아무도 할 일이 없습니다.\n모두 숨죽이고 진행할까요?";

    sealed class Fake : IFieldPlanFacts, IFieldListenFacts, IFieldGatherFacts, IFieldUseFacts
    {
        public bool[] Up, Lamp; public readonly Dictionary<int, FieldSiteFacts> Sites = new Dictionary<int, FieldSiteFacts>();
        public readonly Dictionary<int, FieldPause> DoorBlock = new Dictionary<int, FieldPause>(); public readonly Dictionary<(int, string), FieldPause> UseBlocks = new Dictionary<(int, string), FieldPause>();
        public bool Asleep;
        public Fake(int n) { Up = Enumerable.Repeat(true, n).ToArray(); Lamp = new bool[n]; }
        public int Members => Up.Length;
        public int LightBonus => 20;
        public bool Alive(int m) => m >= 0 && m < Up.Length && Up[m];
        public bool HasLight(int m) => Alive(m) && Lamp[m];
        public bool HasTool(int site, int m) => true;
        public FieldSiteFacts Site(int site) => Sites.TryGetValue(site, out var f) ? f : new FieldSiteFacts { InRoom = true, Searchable = true };
        public FieldPause ListenBlock(int door) => Asleep ? FieldPause.OtherRoom : FieldPause.None;
        public FieldPause GatherBlock(int door) => DoorBlock.TryGetValue(door, out var p) ? p : door == D ? FieldPause.OtherRoom : FieldPause.None;
        public FieldPause UseBlock(int m, string item) => UseBlocks.TryGetValue((m, item), out var p) ? p : FieldPause.None;
    }
    static FieldOrder O(int site, int lead, int duty = 0, bool solo = false, int prefer = -1) => new FieldOrder { Site = site, Lead = lead, Duty = duty, Solo = solo, Prefer = prefer };
    static FieldTurnPlan Pawns() => new FieldTurnPlan { AutoFill = false }; // the planner's plan: helpers are placed pawns only
    static string Snap(FieldTurnPlan p) => p.Version + ":" + p.AutoFill + ":" + string.Join("|", p.Orders.Select(o => $"{o.Site},{o.Lead},{o.Support},{o.Prefer},{o.Duty},{o.Solo}"))
        + "|L" + string.Join(",", p.Listens.Select(l => l.Door + ":" + l.Member)) + "|G" + string.Join(",", p.Gathers.Select(g => g.Door + ":" + g.Member)) + "|U" + string.Join(",", p.Uses.Select(u => u.Item + ":" + u.Member))
        + "|O" + string.Join(",", p.Observations.Select(o => o.Id + ":" + o.Member));

    // ---- edit mode ----
    public static string Rules()
    {
        var done = new List<string>();
        // 1. AutoFill off: a helper is only a placed (Prefer) or kept (Support) pawn; on (a raw plan, the old windows) the lowest free member.
        var f = new Fake(2); var plan = Pawns(); plan.Assign(O(1, 0)); var k = plan.Check(f); var r = k.RunFor(1);
        Check(r.Support < 0 && k.Actions[1] == FieldAction.Hush && r.Required == 2 && !k.Full, "AutoFill off: nobody bound without a pawn");
        plan.Assign(O(1, 0, prefer: 1)); k = plan.Check(f); r = k.RunFor(1);
        Check(r.Support == 1 && r.Role == FieldAction.Together && r.Required == 1 && k.Full, "Placed helper: 함께, one turn sooner");
        var legacy = new FieldTurnPlan(); legacy.Assign(O(1, 0)); Check(legacy.AutoFill && legacy.Check(f).RunFor(1).Support == 1, "Raw plan still fills (07 / note)");
        Check(!plan.Clone().AutoFill && legacy.Clone().AutoFill, "Clone keeps AutoFill");
        Check(plan.CanOfferHelper(0, 1, 0, 1, f) && !plan.CanOfferHelper(0, 1, 0, 0, f), "CanOfferHelper");
        done.Add("auto fill off");

        // 2. A helper leaving a new order leaves it solo (a light order does not pause for its lamp).
        f = new Fake(2); f.Lamp[1] = true; plan = Pawns(); plan.Assign(O(1, 0, duty: 2, prefer: 1)); plan.Keep(plan.Check(f));
        Check(plan.Check(f).RunFor(1).Support == 1 && plan.Find(1).Support == 1, "Light helper kept");
        plan.AssignListen(1, C); k = plan.Check(f); var o1 = plan.Find(1);
        Check(o1.Solo && o1.Duty == 0 && o1.Support < 0 && k.RunFor(1) != null && k.RunFor(1).Support < 0 && k.PauseFor(1) == FieldPause.None && k.Actions[1] == FieldAction.Listen, "Helper to a door: solo, not paused");
        plan = Pawns(); plan.Assign(O(1, 0, prefer: 1)); plan.Keep(plan.Check(f)); plan.AssignGather(1, C); o1 = plan.Find(1);
        Check(o1.Solo && o1.Support < 0 && plan.Check(f).Actions[1] == FieldAction.Gather, "Helper to gather: solo");
        plan = Pawns(); plan.Assign(O(1, 0, prefer: 1)); plan.Keep(plan.Check(f)); plan.Assign(O(2, 1)); o1 = plan.Find(1);
        Check(o1.Solo && o1.Support < 0 && plan.Find(2).Lead == 1 && !plan.Find(2).Solo, "Helper to lead elsewhere: solo; another order untouched");
        done.Add("helper leaves → solo");

        // 3. Unassign (the pawn rules): a lead's helper leads on (new: solo), a helper leaves solo, nothing → false.
        f = new Fake(2); plan = Pawns(); plan.Assign(O(1, 0, prefer: 1)); plan.Keep(plan.Check(f));
        Check(plan.Unassign(0, f) && plan.Find(1).Lead == 1 && plan.Find(1).Solo && plan.Find(1).Support < 0 && plan.Check(f).Actions[0] == FieldAction.Hush, "Lead leaves: helper leads alone");
        plan = Pawns(); plan.Assign(O(1, 0, prefer: 1)); plan.Keep(plan.Check(f));
        Check(plan.Unassign(1, f) && plan.Find(1).Lead == 0 && plan.Find(1).Solo && plan.Check(f).RunFor(1).Support < 0 && plan.Check(f).Actions[1] == FieldAction.Hush, "Helper leaves: solo, not re-bound");
        Check(!plan.Unassign(1, f), "Nothing to leave: false");
        // A running 함께 search (a 3-turn object started with its helper: Required 2 < 3) keeps its helper bound on the board; the raw plan does not.
        f = new Fake(2); f.Sites[1] = new FieldSiteFacts { InRoom = true, Searchable = true, Opened = true, Progress = 1, Required = 2, Duty = 0, Turns = 3, Pace = 1 };
        plan = Pawns(); plan.Assign(O(1, 0, prefer: 1)); plan.Keep(plan.Check(f)); k = plan.Check(f);
        Check(k.RunFor(1).Support == 1 && k.Actions[1] == FieldAction.Together && k.RunFor(1).Required == 2 && !k.RunFor(1).Forfeits, "Running 함께 keeps its helper (board)");
        legacy = new FieldTurnPlan(); legacy.Assign(O(1, 0, solo: true)); Check(legacy.Check(f).RunFor(1).Support < 0, "Running 함께 binds nobody (raw plan)");
        Check(plan.Unassign(0, f) && plan.Find(1).Lead == 1 && plan.Check(f).RunFor(1).Support < 0 && plan.Check(f).RunFor(1).Required == 2, "Running: the helper leads, stored turns kept");
        // A running light search whose lead leaves: the helper leads, the lamp is gone, it pauses.
        f = new Fake(2); f.Lamp[1] = true; f.Sites[2] = new FieldSiteFacts { InRoom = true, Searchable = true, Opened = true, Progress = 1, Required = 2, Duty = 2, Bonus = 20, Turns = 2, Pace = 1 };
        plan = Pawns(); plan.Assign(O(2, 0, duty: 2, prefer: 1)); plan.Keep(plan.Check(f)); Check(plan.Check(f).RunFor(2).Support == 1, "Running light bound");
        plan.Unassign(0, f); k = plan.Check(f); Check(plan.Find(2).Lead == 1 && k.PauseFor(2) == FieldPause.NoLight && k.Actions[1] == FieldAction.Paused, "Running light without its lamp pauses");
        // The lamp comes back (FieldPlacement.LampOption · DarkLead): a lead without a flashlight swaps with the waiting lead who has one;
        // a lead alone waits paused and the member with the flashlight joins it. The stored role, bonus and turns stay.
        var swap = plan.Clone(); var waiting = swap.Find(2).Copy(); waiting.Prefer = waiting.Lead; waiting.Lead = 0; waiting.Support = -1; swap.Assign(waiting); r = swap.Check(f).RunFor(2);
        Check(r != null && r.Lead == 0 && r.Support == 1 && r.Role == FieldAction.Light && r.Bonus == 20 && r.Required == 2, "Lamp back: the lead swaps with the waiting lamp");
        plan = Pawns(); plan.Assign(new FieldOrder { Site = 2, Lead = 0, Duty = 2, Pace = 1 }); k = plan.Check(f);
        Check(k.RunFor(2) == null && k.Paused.Any(p => p.Site == 2 && p.Lead == 0 && p.Reason == FieldPause.NoLight) && k.Actions[0] == FieldAction.Paused, "A lead alone on a started light search waits");
        waiting = plan.Find(2).Copy(); waiting.Prefer = 1; waiting.Support = -1; plan.Assign(waiting); r = plan.Check(f).RunFor(2);
        Check(r != null && r.Lead == 0 && r.Support == 1 && r.Role == FieldAction.Light && r.Bonus == 20 && r.Required == 2, "Lamp back: the flashlight joins the waiting lead");
        done.Add("unassign rules");

        // 4. Gathering: silent, not idle, never a helper; everyone at one door (its listener counts, a lone listener does not) = the move.
        f = new Fake(2); plan = Pawns(); plan.AssignGather(0, C); k = plan.Check(f);
        Check(k.Actions[0] == FieldAction.Gather && k.GatherOf[0] == C && k.HushedAll && !k.Full && k.GatheredDoor < 0 && k.Noise == 0 && k.Hushing == 1, "One gatherer of two");
        plan.AssignGather(1, C); k = plan.Check(f); Check(k.GatheredDoor == C && k.Full && k.HushedAll && k.Gathers.Count == 2, "Both gathered");
        Check(!plan.HasAssignments && plan.HasGathers, "Gathers are not assignments");
        plan = Pawns(); plan.AssignListen(0, C); plan.AssignGather(1, C); Check(plan.Check(f).GatheredDoor == C && plan.Check(f).Listens.Count == 1, "Listener counts");
        f.Up[1] = false; plan = Pawns(); plan.AssignListen(0, C); Check(plan.Check(f).GatheredDoor < 0, "Lone listener only listens");
        plan.AssignGather(0, C); Check(plan.Check(f).GatheredDoor == C && plan.ListenBy(0) == null, "Lone gatherer moves"); f.Up[1] = true;
        plan = Pawns(); plan.AssignGather(0, C); plan.AssignGather(1, A); Check(plan.Check(f).GatheredDoor < 0, "Two doors: no move");
        f.DoorBlock[S] = FieldPause.NoTool; plan = Pawns(); plan.AssignGather(0, S); plan.AssignGather(1, S); k = plan.Check(f);
        Check(k.GatheredDoor < 0 && k.GatherPaused.Count == 2 && k.Actions[0] == FieldAction.Paused && k.GatherPauseFor(1) == FieldPause.NoTool && k.GatherOf[1] == S, "Lock without its tool: paused");
        Check(plan.PruneGathers(k).Count == 2 && !plan.HasGathers, "Paused gathers pruned"); f.DoorBlock.Clear();
        legacy = new FieldTurnPlan(); legacy.AssignGather(1, C); legacy.Assign(O(1, 0)); k = legacy.Check(f);
        Check(k.RunFor(1).Support < 0 && k.Actions[1] == FieldAction.Gather, "A gatherer is never auto-filled");
        plan = Pawns(); plan.AssignGather(0, C); plan.Assign(O(1, 0)); Check(plan.GatherBy(0) == null, "Leading takes the pawn off the door");
        // The listener leaves: the first gatherer there listens instead (not on the first visit, not alone).
        f = new Fake(3); plan = Pawns(); plan.AssignListen(0, C); plan.AssignGather(1, C); plan.AssignGather(2, C); Check(plan.Check(f).GatheredDoor == C, "Three at the door");
        plan.Unassign(0, f); k = plan.Check(f); Check(plan.ListenAt(C).Member == 1 && plan.GatherBy(1) == null && k.GatheredDoor < 0 && k.Actions[2] == FieldAction.Gather, "First gatherer listens");
        f.Asleep = true; plan = Pawns(); plan.AssignListen(0, C); plan.AssignGather(1, C); plan.Unassign(0, f);
        Check(plan.ListenAt(C) == null && plan.GatherBy(1) != null, "Asleep: nobody starts listening"); f.Asleep = false;
        plan = Pawns(); plan.AssignGather(0, C); plan.AssignGather(1, C); Check(plan.ReleaseGathers() && !plan.HasGathers && plan.Check(f).GatheredDoor < 0, "ReleaseGathers");
        done.Add("gathering");

        // 5. Bag use: the member's action (silent, not a hush, never a helper); one that cannot happen leaves them hushing.
        f = new Fake(2); plan = Pawns(); plan.Assign(O(1, 0)); plan.AssignUse(0, "bandage"); k = plan.Check(f);
        Check(plan.Find(1) == null && k.Actions[0] == FieldAction.Use && k.UseOf[0] == "bandage" && !k.HushedAll && k.Uses.Count == 1 && plan.HasAssignments, "Use replaces the lead");
        plan.AssignGather(1, C); k = plan.Check(f); Check(k.Full && k.GatheredDoor < 0, "Use + gather: full, no move (not everyone at the door)");
        f.UseBlocks[(0, "bandage")] = FieldPause.Complete; k = plan.Check(f); Check(k.Actions[0] == FieldAction.Hush && k.UsePaused.Count == 1 && k.Uses.Count == 0, "Blocked use hushes");
        var gone = plan.PruneUses(k); Check(gone.Count == 1 && gone[0].Reason == FieldPause.Complete && plan.UseBy(0) == null, "Uses are one-shot"); f.UseBlocks.Clear();
        legacy = new FieldTurnPlan(); legacy.AssignUse(0, "bandage"); legacy.Assign(O(1, 1)); Check(legacy.Check(f).RunFor(1).Support < 0, "A user is never auto-filled");
        plan = Pawns(); plan.AssignUse(0, "bandage"); Check(plan.Unassign(0, f) && plan.UseBy(0) == null, "Unassign drops the use");
        done.Add("bag use");

        // 6. Check is pure; Clone copies everything; Clear clears everything.
        f = new Fake(3); f.Lamp[2] = true; plan = Pawns(); plan.Assign(O(1, 0, duty: 2, prefer: 2)); plan.AssignListen(1, C); plan.AssignUse(2, "ration"); plan.AssignGather(1, A);
        string before = Snap(plan); plan.Check(f); plan.Check(f); Check(Snap(plan) == before, "Check mutated the plan");
        Check(Snap(plan.Clone()) == before, "Clone"); plan.Clear(); Check(!plan.HasAssignments && !plan.HasGathers && plan.Uses.Count == 0, "Clear");
        done.Add("pure/clone/clear");

        // 7. Fuzz: every living member has exactly one action; a helper is never a lead / listener / observer / gatherer / user; GatheredDoor
        //    is exactly 'everyone at one door'; HushedAll exactly 'no search, listen, observation or bag use'.
        var rng = new System.Random(7); int cases = 0;
        for (int t = 0; t < 3000; t++)
        {
            int n = rng.Next(1, 5); f = new Fake(n); for (int m = 0; m < n; m++) { f.Up[m] = rng.Next(6) > 0; f.Lamp[m] = rng.Next(3) == 0; }
            for (int site = 0; site < 4; site++) f.Sites[site] = new FieldSiteFacts { InRoom = rng.Next(8) > 0, Searchable = true, Noise = rng.Next(4), Turns = rng.Next(4), Opened = true };
            if (rng.Next(4) == 0) f.DoorBlock[S] = FieldPause.NoTool;
            plan = rng.Next(3) == 0 ? new FieldTurnPlan() : Pawns();
            for (int j = rng.Next(0, 7); j > 0; j--)
            {
                int m = rng.Next(n);
                switch (rng.Next(6))
                {
                    case 0: case 1: plan.Assign(O(rng.Next(4), m, rng.Next(3), rng.Next(3) == 0, rng.Next(-1, n))); break;
                    case 2: plan.AssignListen(m, new[] { A, C, S }[rng.Next(3)]); break;
                    case 3: plan.AssignGather(m, new[] { A, C, S }[rng.Next(3)]); break;
                    case 4: plan.AssignUse(m, "bandage"); break;
                    default: plan.Unassign(m, f); break;
                }
                if (rng.Next(3) == 0) plan.Keep(plan.Check(f));
            }
            before = Snap(plan); k = plan.Check(f); Check(Snap(plan) == before, "Fuzz: Check mutated " + t);
            int alive = Enumerable.Range(0, n).Count(f.Alive);
            for (int m = 0; m < n; m++) Check((k.Actions[m] == FieldAction.Down) == !f.Alive(m), "Fuzz: down " + t);
            var sup = k.Runs.Where(x => x.Support >= 0).Select(x => x.Support).ToList();
            Check(sup.Distinct().Count() == sup.Count && sup.All(m => !plan.Orders.Any(o => o.Lead == m) && !k.Listens.Any(l => l.Member == m) && !k.Gathers.Any(g => g.Member == m) && !k.Uses.Any(u => u.Member == m)), "Fuzz: helpers " + t);
            Check(!plan.AutoFill ? k.Runs.All(x => x.Support < 0 || x.Support == plan.Find(x.Site).Support || x.Support == plan.Find(x.Site).Prefer) : true, "Fuzz: board binds only placed pawns " + t);
            var busy = k.Runs.Select(x => x.Lead).Concat(sup).Concat(k.Listens.Select(l => l.Member)).Concat(k.Gathers.Select(g => g.Member)).Concat(k.Uses.Select(u => u.Member)).Concat(k.Observations.Select(x => x.Member)).ToList();
            Check(busy.Distinct().Count() == busy.Count, "Fuzz: one action each " + t);
            Check(k.HushedAll == (k.Runs.Count == 0 && k.Listens.Count == 0 && k.Observations.Count == 0 && k.Uses.Count == 0), "Fuzz: hushed " + t);
            int want = -1;
            foreach (int door in k.Gathers.Select(g => g.Door).Distinct()) { int there = k.GatherersAt(door) + (alive > 1 && k.ListenerAt(door) >= 0 ? 1 : 0); if (alive > 0 && there >= alive) { want = door; break; } }
            Check(k.GatheredDoor == want && (want < 0 || k.Gathers.All(g => g.Door == want)), "Fuzz: gathered door " + t);
            cases++;
        }
        done.Add("fuzz " + cases);

        // 8. The door log: newest first, at most Max per door, per (room, door); ages '방금' / 'N턴 전'; an old 'something inside' says '그때'.
        var log = new FieldDoorLog(); int version = log.Version;
        for (int turn = 1; turn <= 10; turn++) log.Add(new FieldDoorReport(C, A, turn, -1, -1, false, turn == 3, ResidentState.Gone), 0);
        log.Add(new FieldDoorReport(C, S, 4, C, C, true, false, ResidentState.Staying), 1);
        var list = log.For(A, C);
        Check(list.Count == log.Max && list[0].R.Turn == 10 && list.Last().R.Turn == 10 - log.Max + 1 && log.For(S, C).Count == 1 && log.Version > version, "Log per door, capped, newest first");
        Check(log.TryLatest(A, C, out var latest) && latest.R.Turn == 10 && !log.TryLatest(C, A, out _), "Latest");
        Check(log.Line(latest, 10).StartsWith(log.AgeNow) && log.Line(list[2], 10).StartsWith(string.Format(log.AgeFormat, 2)) && log.Line(latest, 10).Contains(log.Quiet), "Ages: " + log.Line(list[2], 10));
        var inside = log.For(S, C)[0]; string now = log.Line(inside, 4), later = log.Line(inside, 6);
        Check(now.Contains(string.Format(log.Present, log.NextStay)) && !now.Contains(log.StalePrefix) && later.Contains(log.StalePrefix), "Present: " + now + " / " + later);
        log.Clear(); Check(log.Count == 0, "Clear");
        done.Add("door log");

        // 9. The first visit's random encounter with the prefab values (SimulateFieldSite.FirstVisit reports all routes): the guided tutorial
        //    route (crate co-op, then the corridor crate co-op) never meets it; one pawn per object on it does (about 70%).
        var (guided, solo) = TutorialChances(); Check(guided == 0 && solo > guided, $"Tutorial chances {guided:0.0}% / {solo:0.0}%");
        done.Add($"tutorial encounter guided {guided:0.0}% · one pawn per object {solo:0.0}%");
        return "PASS: " + string.Join("; ", done);
    }
    static (double guided, double solo) TutorialChances()
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab"); var c = go ? go.GetComponent<SettlementController>() : null;
        Check(c && c.ArrivalPanel && c.ArrivalPanel.Encounter && c.ArrivalPanel.Loot, "SettlementScreen.prefab references missing");
        var e = c.ArrivalPanel.Encounter; var sites = c.ArrivalPanel.Loot.Sites;
        List<int> Noises(bool coop) { var l = new List<int>(); foreach (int site in new[] { 0, 5 }) for (int t = 0; t < FieldTurnPlan.RequiredOf(sites[site].Turns, coop); t++) l.Add(Math.Max(0, sites[site].Noise)); return l; }
        double Meet(List<int> noises)
        {
            // At least one meeting: until the first one, a search turn warns (no roll) or rolls ChanceAt(search turn, noise so far).
            double none = 1; bool warned = false; int noise = 0;
            for (int t = 0; t < noises.Count; t++)
            {
                int search = t + 1; noise += noises[t];
                if (!warned) { warned = ExpeditionEncounterPanel.WarnsAt(search, noise, e.WarnSearches, e.NoiseThreshold); continue; }
                if (search < 2) continue;
                none *= 1 - ExpeditionEncounterPanel.ChanceAt(search, noise, e.WarnSearches, e.BaseChance, e.ChancePerSearch, e.ChancePerNoise, e.MaximumChance) / 100.0;
            }
            return (1 - none) * 100;
        }
        return (Meet(Noises(true)), Meet(Noises(false)));
    }

    public static string Wiring()
    {
        var done = new List<string>();
        void Panel(ExpeditionArrivalPanel a, string where)
        {
            var pl = a.GetComponent<FieldTurnPlanner>(); var t = a.Threat; var idle = a.GetComponent<FieldIdleConfirm>();
            Check(pl && t && idle && t.Planner == pl, where + ": planner / threat / idle confirm");
            Check(!t.Hush, where + ": ExpeditionSiteThreat.Hush cleared (run BuildPawnRules)");
            var hush = a.Main.transform.Find("Hush"); Check(!hush || !hush.gameObject.activeSelf, where + ": Main/Hush inactive");
            Check(a.Main.transform.Cast<Transform>().Count(x => x.name == "Hush") <= 1, where + ": one Main/Hush");
            Check(!pl.ListenButton && !pl.ListenTitle && !pl.ListenSubtitle, where + ": door popup listen button cleared");
            Check(!pl.AutoFillHelpers, where + ": AutoFillHelpers off");
            Check(t.IntroBody != null && t.IntroBody.Contains(NewIntroLine), where + ": IntroBody last line: " + t.IntroBody);
            var auto = a.Main.GetComponentInChildren<FieldAutoAdvance>(true); var warning = a.Main.GetComponentInChildren<FieldTurnWarning>(true);
            Check(auto && (!warning || auto.Warning == warning), where + ": FieldAutoAdvance.Warning");
            Check(idle.BodyAll == IdleBodyAll, where + ": idle question text '" + idle.BodyAll + "'");
            Check(pl.PlaceTexts != null && pl.DoorLog != null && pl.DoorLog.Max >= 1, where + ": PlaceTexts / DoorLog");
        }
        var arrival = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/ExpeditionArrivalPanel.prefab"); Check(arrival, "ExpeditionArrivalPanel.prefab missing");
        Panel(arrival.GetComponent<ExpeditionArrivalPanel>(), "prefab"); done.Add("arrival prefab");
        var screen = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab"); Check(screen, "SettlementScreen.prefab missing");
        foreach (var a in screen.GetComponentsInChildren<ExpeditionArrivalPanel>(true)) { Panel(a, "SettlementScreen"); done.Add("screen instance"); }
        return "PASS: " + string.Join("; ", done);
    }

    // ---- play mode ----
    static async Task Tap(Button button)
    {
        Check(button && button.IsActive() && button.IsInteractable(), "Unavailable " + (button ? button.name : "null")); Hit(button);
        var r = (RectTransform)button.transform; var data = new PointerEventData(EventSystem.current) { position = Screen(r), button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler); await Task.Delay(100);
    }
    static Vector2 Screen(RectTransform r) { Canvas.ForceUpdateCanvases(); return RectTransformUtility.WorldToScreenPoint(r.GetComponentInParent<Canvas>().worldCamera, r.TransformPoint(r.rect.center)); }
    static void Hit(Selectable target)
    {
        var data = new PointerEventData(EventSystem.current) { position = Screen((RectTransform)target.transform) }; var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Selectable>() == target, "Blocked " + target.name + " by " + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
    }
    static async Task Until(Func<bool> done, int ms, string what) { var w = Stopwatch.StartNew(); while (!done()) { if (w.ElapsedMilliseconds > ms) throw new Exception("Timeout: " + what); await Task.Delay(30); } }
    static async Task<ExpeditionArrivalPanel> Depart(SettlementController c)
    {
        await Tap(c.Exit); foreach (var card in c.ExpeditionPanel.Cards) if (!card.Check.gameObject.activeSelf) await Tap(card.Button);
        await Tap(c.ExpeditionPanel.Pack); await Tap(c.PackingPanel.Ready); await Tap(c.PackingPanel.Depart); await Task.Delay(1100);
        Check(c.ArrivalPanel.IsOpen && !c.ArrivalPanel.InTransit, "Arrived"); return c.ArrivalPanel;
    }
    static async Task CloseLoot(ExpeditionArrivalPanel a) { if (a.Loot.IsOpen) { await Tap(a.Loot.Back); await Task.Delay(120); if (a.Loot.LeaveReview.activeSelf) await Tap(a.Loot.LeaveConfirm); } }
    static SettlementController Settlement() { var c = Object.FindAnyObjectByType<SettlementController>(); Check(Application.isPlaying && c && c.Campaign != null, "Play mode, then VerifyFieldTurnPlan.Enter first"); return c; }
    // One '턴 진행' press (the button, as the player): exactly one turn for a member turn, the preview equal to the result.
    static async Task<int> Turn(ExpeditionArrivalPanel a, string what)
    {
        var pl = a.Threat.Planner; int turns = a.Rooms.Turns; bool move = a.Rooms.HasQueuedMove; int cost = move ? a.Rooms.QueuedTurns : 1;
        await Tap(pl.TurnButton); await Task.Delay(250);
        if (move) { await Until(() => !a.InTransit, 8000, what + " (move)"); await Task.Delay(300); }
        Check(a.Rooms.Turns == turns + cost, what + ": turns " + turns + " → " + a.Rooms.Turns + " (want +" + cost + ")");
        if (!move) Check(pl.LastMismatch == "", what + ": preview ≠ result " + pl.LastMismatch);
        return cost;
    }
    static FieldPlaceOption Option(FieldPlacement place, int m, FieldSpotKind kind, int site = -1, int door = -1)
    {
        var o = place.OptionsFor(m).FirstOrDefault(x => x.Kind == kind && (site < 0 || x.Site == site) && (door < 0 || x.Door == door));
        Check(o != null, $"No {kind} option for member {m} (site {site}, door {door}): " + string.Join(", ", place.OptionsFor(m).Select(x => x.Kind + "/" + x.Site + "/" + x.Door + (x.Enabled ? "" : " off " + x.Blocked))));
        return o;
    }
    static void Place(FieldPlacement place, FieldPlaceOption o, string what) { Check(o.Enabled && place.Place(o), "Place " + what + ": " + place.LastStatus); }
    // Objects of this room that are open, not done, and need no tool.
    static List<int> OpenSites(ExpeditionArrivalPanel a) => Enumerable.Range(0, a.Loot.Sites.Length).Where(i => a.Loot.IsSiteInCurrentRoom(i) && string.IsNullOrEmpty(a.Loot.Sites[i].RequiredTool)
        && !(a.Loot.Peek(i, out var s) && (s.Complete || s.Progress > 0)) && (!a.Threat || i != a.Threat.DenSite)).ToList();
    static (int turns, int minute, int noise, int searches, int clock) Clock(SettlementController c, ExpeditionArrivalPanel a) => (a.Rooms.Turns, c.Campaign.MinuteOfDay, a.Rooms.Noise, a.Encounter.Searches, a.Threat.State.TurnsUsed);

    public static async Task<string> FirstVisit()
    {
        FieldIdleConfirm.AutoAccept = true; var c = Settlement(); var a = await Depart(c); var t = a.Threat; var pl = t.Planner; var e = a.Encounter; var log = new List<string>();
        var place = FieldPlacement.Of(a); await Until(() => place.Ready, 3000, "board ready");
        Check(t.State.Asleep && pl.Placing && !pl.Active && pl.TurnButton.gameObject.activeSelf && (!t.Hush || !t.Hush.gameObject.activeSelf), "First visit: '턴 진행' shows, no hush button");
        var sites = OpenSites(a); Check(sites.Count >= 2, "Two open arcade objects: " + string.Join(",", sites));
        int s0 = sites[0], s1 = sites.Skip(1).FirstOrDefault(i => a.Loot.SiteNoise(i) == a.Loot.SiteNoise(s0) && a.Loot.SiteTurns(i) == a.Loot.SiteTurns(s0)); if (s1 == 0 && s0 != 0 || s1 == s0) s1 = sites[1];

        // A. One co-op search = one turn, the old one-search-one-turn numbers (RunFromNote, while it exists).
        (int turns, int minute, int noise, int searches, int clock) oldDelta = default; bool haveOld = false;
        var runFromNote = typeof(ExpeditionSearchPanel).GetMethod("RunFromNote", BindingFlags.Public | BindingFlags.Instance);
        if (runFromNote != null && a.Loot.SiteNoise(s1) == a.Loot.SiteNoise(s0) && a.Loot.SiteTurns(s1) == a.Loot.SiteTurns(s0))
        {
            var b0 = Clock(c, a); bool ran = (bool)runFromNote.Invoke(a.Search, new object[] { s0, a.Participants[0], 1, 0 }); var b1 = Clock(c, a);
            Check(ran, "RunFromNote ran"); oldDelta = (b1.turns - b0.turns, b1.minute - b0.minute, b1.noise - b0.noise, b1.searches - b0.searches, b1.clock - b0.clock); haveOld = true;
            await Task.Delay(200); await CloseLoot(a); await Until(() => place.Ready, 3000, "board after the old search");
        }
        int s = haveOld ? s1 : s0;
        Place(place, Option(place, 0, FieldSpotKind.Lead, s), "lead"); var join = Option(place, 1, FieldSpotKind.Join, s);
        Check(join.Label.Contains("협동"), "Co-op pin: " + join.Label); Place(place, join, "join");
        var k = pl.Current; var run = k.RunFor(s);
        Check(run != null && run.Lead == 0 && run.Support == 1 && run.Role == FieldAction.Together && run.Required == FieldTurnPlan.RequiredOf(a.Loot.SiteTurns(s), true) && a.Rooms.Inspected.Contains(s), "Co-op planned on the first visit");
        var n0 = Clock(c, a); await Turn(a, "co-op search"); var n1 = Clock(c, a);
        var newDelta = (n1.turns - n0.turns, n1.minute - n0.minute, n1.noise - n0.noise, n1.searches - n0.searches, n1.clock - n0.clock);
        Check(newDelta.Item1 == 1 && newDelta.Item2 == a.Rooms.MinutesPerTurn && newDelta.Item3 == FieldTurnPlan.SearchNoise(a.Loot.SiteNoise(s), false) && newDelta.Item4 == 1 && newDelta.Item5 == 1, "One search turn: " + newDelta);
        Check(a.Loot.Peek(s, out var st) && st.Progress == 1 && st.Required == run.Required && st.Complete == run.Completes, "Progress as planned");
        if (haveOld) Check(oldDelta.Equals(newDelta), "Old (RunFromNote) " + oldDelta + " ≠ board " + newDelta);
        log.Add(haveOld ? "co-op = RunFromNote " + newDelta : "co-op one turn " + newDelta + " (RunFromNote retired: formula only)");
        await Task.Delay(200); await CloseLoot(a); await Until(() => place.Ready, 3000, "board after the loot");

        // B. Doors only gather on the first visit; the next member is offered to pick up; everyone there = the move next turn.
        var doorOptions = place.OptionsFor(0).Where(x => x.Door >= 0).ToList();
        Check(doorOptions.Count == 1 && doorOptions[0].Kind == FieldSpotKind.Gather && doorOptions[0].Door == C, "Arcade door: gather only (" + string.Join(",", doorOptions.Select(x => x.Kind)) + ")");
        Place(place, doorOptions[0], "gather 0"); Check(!a.Rooms.HasQueuedMove && place.NextAfter(doorOptions[0]) == 1 && pl.Current.Actions[0] == FieldAction.Gather, "One of two at the door");
        var last = Option(place, 1, FieldSpotKind.Gather, door: C); Check(last.Label.Contains(FieldSiteState.RoomNames[C]), "Last pin names the room: " + last.Label);
        Place(place, last, "gather 1"); Check(a.Rooms.HasQueuedMove && a.Rooms.QueuedRoom == C && a.Rooms.QueuedTurns == 1 && place.NextAfter(last) < 0, "Everyone at the door: move reserved");
        await Turn(a, "move"); Check(a.Rooms.CurrentRoom == C && !pl.Plan.HasAssignments && !pl.Plan.HasGathers, "Moved; plan empty");
        await Until(() => place.Ready, 3000, "corridor ready"); log.Add("doors gather, move next turn");

        // C. Two searches in one turn = one search turn, noise summed (the random roll held at 0 for this turn).
        var corridor = OpenSites(a); int prybarSite = Enumerable.Range(0, a.Loot.Sites.Length).FirstOrDefault(i => a.Loot.IsSiteInCurrentRoom(i) && a.Loot.Sites[i].RequiredTool == "prybar" && !(a.Loot.Peek(i, out var ps) && ps.Complete));
        if (corridor.Count < 2 && prybarSite > 0 && (c.InventoryPanel.CountFor(a.Participants[0], "prybar") > 0 || c.InventoryPanel.TransferField(a.Participants[0], "prybar", 1, true))) corridor.Insert(0, prybarSite);
        int rate = e.BaseChance, max = e.MaximumChance, wait = e.WaitChance;
        try
        {
            if (corridor.Count >= 2)
            {
                int x0 = corridor[0], x1 = corridor[1];
                Place(place, Option(place, 0, FieldSpotKind.Lead, x0), "lead x0"); Place(place, Option(place, 1, FieldSpotKind.Lead, x1), "lead x1");
                k = pl.Current; Check(k.Runs.Count == 2 && k.Noise == a.Loot.SiteNoise(x0) + a.Loot.SiteNoise(x1), "Two searches planned: noise " + k.Noise);
                int chance = pl.EncounterChance(k); if (chance > 0) Check(pl.Chip.text.Contains(string.Format(pl.Texts.ChipEncounter, chance)), "Chip shows the chance: " + pl.Chip.text);
                e.BaseChance = e.MaximumChance = 0;
                var p0 = Clock(c, a); await Turn(a, "parallel"); var p1 = Clock(c, a);
                Check(p1.turns == p0.turns + 1 && p1.searches == p0.searches + 1 && p1.noise == p0.noise + a.Loot.SiteNoise(x0) + a.Loot.SiteNoise(x1) && !e.IsOpen, "Parallel = one search turn");
                Check(a.Loot.Peek(x0, out var q0) && q0.Progress == 1 && a.Loot.Peek(x1, out var q1) && q1.Progress == 1, "Both progressed");
                await Task.Delay(200); await CloseLoot(a); log.Add("parallel = 1 search turn, noise " + (p1.noise - p0.noise) + (chance > 0 ? ", chip " + chance + "%" : ""));

                // D. The random encounter through '턴 진행' (forced): the plan ends; after hiding, the completed object's finds open.
                await Until(() => place.Ready, 3000, "board before the meeting");
                // The leads stay on their objects after the turn (a running search keeps its order); place again only what went.
                foreach (var (m, x) in new[] { (0, x0), (1, x1) }) if (!(a.Loot.Peek(x, out var qs) && qs.Complete) && (pl.Plan.Find(x) == null || pl.Plan.Find(x).Lead != m)) Place(place, Option(place, m, FieldSpotKind.Lead, x), "lead again " + x);
                k = pl.Current; int first = k.Runs.FirstOrDefault(r => r.Completes)?.Site ?? (k.Runs.Count > 0 ? k.Runs[0].Site : -1); Check(first >= 0, "Something to search");
                bool cool = e.Cooldown > 0 || !e.Warned; e.BaseChance = e.MaximumChance = 100; e.WaitChance = 100;
                int before = a.Rooms.Turns; await Tap(pl.TurnButton); await Task.Delay(300);
                Check(a.Rooms.Turns == before + 1, "One turn into the meeting");
                if (cool) log.Add("meeting not forced (warning/cooldown this turn)");
                else
                {
                    Check(e.IsOpen && !pl.Plan.HasAssignments && !a.Loot.IsOpen && !pl.TurnButton.gameObject.activeSelf, "Meeting: plan cleared, no loot, turn button hidden");
                    await Tap(e.Wait); await Tap(e.Confirm); await Task.Delay(300);
                    bool complete = a.Loot.Peek(first, out var fs) && fs.Complete;
                    Check(!e.IsOpen && !a.Search.IsOpen && a.Loot.IsOpen == complete, "After the meeting: finds " + (complete ? "open" : "stay") + ", no 07");
                    await CloseLoot(a); log.Add("meeting via '턴 진행' → plan cleared → " + (complete ? "finds" : "board"));
                }
            }
            else log.Add("parallel skipped (corridor objects " + string.Join(",", corridor) + ")");
        }
        finally { e.BaseChance = rate; e.MaximumChance = max; e.WaitChance = wait; }
        return "PASS first visit · " + string.Join(" · ", log);
    }

    public static async Task<string> Board()
    {
        FieldIdleConfirm.AutoAccept = true; var c = Settlement(); var a = await Depart(c); var t = a.Threat; var pl = t.Planner; var log = new List<string>();
        if (c.Opening) c.Opening.State.Enabled = false; // a later visit comes after the opening chapter
        t.ReviewWake(0, 2, 0); await Task.Delay(250); var place = FieldPlacement.Of(a); await Until(() => place.Ready, 3000, "board ready");
        Check(pl.Active && pl.Placing && (!t.Hush || !t.Hush.gameObject.activeSelf), "Awake board, no hush button");

        // A. Nobody placed: the idle question (the user's text), then a hush turn (gauge −1).
        var idle = a.GetComponent<FieldIdleConfirm>(); FieldIdleConfirm.AutoAccept = false; int gauge = t.State.Gauge, turns = a.Rooms.Turns;
        await Tap(pl.TurnButton); await Task.Delay(150);
        Check(idle.Asking == FieldIdleConfirm.Mode.Turn && a.PopupBody.text == idle.BodyAll && a.Rooms.Turns == turns, "All idle asks: " + a.PopupBody.text);
        await Tap(a.ReturnConfirm); await Task.Delay(300); FieldIdleConfirm.AutoAccept = true;
        Check(a.Rooms.Turns == turns + 1 && t.State.Gauge == Math.Max(0, gauge - t.Rules.HushRelief) && pl.LastCheck.HushedAll, "Hush turn: gauge " + gauge + " → " + t.State.Gauge);
        log.Add("hush turn by the question");

        // B. A pawn at the door listens every turn; the door log keeps each turn ('방금', '1턴 전'); the fresh report is this turn's only.
        var listen = Option(place, 0, FieldSpotKind.Listen, door: C); Place(place, listen, "listen"); Check(place.NextAfter(listen) == 1, "Next member offered after a door pawn");
        await Turn(a, "listen 1"); var logged = pl.DoorLog.For(A, C); Check(logged.Count == 1 && pl.DoorLog.Line(logged[0], t.State.TurnsUsed).StartsWith(pl.DoorLog.AgeNow) && pl.TryReport(C, out _, out int who) && who == 0, "First entry: 방금");
        await Turn(a, "listen 2"); logged = pl.DoorLog.For(A, C);
        Check(logged.Count == 2 && pl.DoorLog.Line(logged[1], t.State.TurnsUsed).StartsWith(string.Format(pl.DoorLog.AgeFormat, 1)) && pl.Plan.ListenAt(C) != null, "Second entry, the pawn stays: " + pl.DoorLog.Line(logged[1], t.State.TurnsUsed));
        log.Add("listen ×2 → log " + logged.Count);

        // C. Gathering with the listener = the move; the listener leaving → the gatherer listens, no move; back again; move (the log stays).
        var gather = Option(place, 1, FieldSpotKind.Gather, door: C); Place(place, gather, "gather"); Check(a.Rooms.HasQueuedMove && a.Rooms.QueuedRoom == C, "Listener + gatherer = move");
        Check(place.Unassign(0) && !a.Rooms.HasQueuedMove && pl.Current.Actions[1] == FieldAction.Listen && place.LastStatus.Contains(pl.PlaceTexts.StatusMoveCancelled), "Listener off: gatherer listens, move cancelled");
        Place(place, Option(place, 0, FieldSpotKind.Gather, door: C), "gather again"); Check(a.Rooms.HasQueuedMove, "Move again");
        int entries = pl.DoorLog.Count; await Turn(a, "move to the corridor");
        Check(a.Rooms.CurrentRoom == C && !pl.Plan.HasAssignments && !pl.Plan.HasGathers && pl.DoorLog.Count == entries, "Moved; the log stays for the visit");
        await Until(() => place.Ready, 3000, "corridor ready"); log.Add("gather/unassign/move");

        // D. The locked storage door (option A): without a prybar a grey pin; with one in any bag everyone gathers → 문 따고 · 1 + UnlockTurns.
        if (!a.Rooms.StorageUnlocked)
        {
            bool had = a.Participants.Any(p => c.InventoryPanel.CountFor(p, "prybar") > 0);
            if (!had)
            {
                var grey = place.OptionsFor(0).FirstOrDefault(x => x.Kind == FieldSpotKind.Gather && x.Door == S); Check(grey != null && !grey.Enabled && grey.Blocked.Length > 0, "Grey lock pin");
                had = c.InventoryPanel.TransferField(a.Participants[1], "prybar", 1, true);
            }
            if (!had) return "PASS board (the lock skipped: no prybar in stock) · " + string.Join(" · ", log);
            Place(place, Option(place, 0, FieldSpotKind.Gather, door: S), "lock 0"); var unlock = Option(place, 1, FieldSpotKind.Gather, door: S);
            Check(unlock.Label.Contains((1 + a.Rooms.UnlockTurns).ToString()), "Unlock pin: " + unlock.Label); Place(place, unlock, "lock 1");
            Check(a.Rooms.HasQueuedMove && a.Rooms.QueuedRoom == S && a.Rooms.QueuedTurns == 1 + a.Rooms.UnlockTurns, "Unlock move reserved");
            await Turn(a, "unlock"); Check(a.Rooms.StorageUnlocked && a.Rooms.CurrentRoom == S, "Storage open");
            await Until(() => place.Ready || a.Encounter.IsOpen, 3000, "storage ready"); log.Add("lock option A " + (1 + a.Rooms.UnlockTurns) + " turns");
        }
        if (a.Encounter.IsOpen) return "PASS (stopped at a meeting after the lock) · " + string.Join(" · ", log);

        // E. A bag item is the member's action for the next turn: nothing changes until '턴 진행', then exactly one turn.
        var p0 = a.Participants[0]; int hp = p0.Health; p0.Health = Math.Max(1, p0.MaxHealth - 1); a.RefreshFieldBags();
        if (c.InventoryPanel.CountFor(p0, "bandage") > 0 || c.InventoryPanel.TransferField(p0, "bandage", 1, true))
        {
            int bandages = c.InventoryPanel.CountFor(p0, "bandage"), health = p0.Health; turns = a.Rooms.Turns;
            string line = pl.QueueUse(0, "bandage"); Check(line.Length > 0 && pl.Current.Actions[0] == FieldAction.Use && a.Rooms.Turns == turns && p0.Health == health, "Queued, nothing yet: " + line);
            Check(place.Describe(0).Contains(string.Format(pl.PlaceTexts.TaskUse, "")), "Hover says the use: " + place.Describe(0));
            await Turn(a, "bag use"); Check(p0.Health > health && c.InventoryPanel.CountFor(p0, "bandage") < bandages && pl.Plan.UseBy(0) == null, "Used at the turn");
            log.Add("bag use at '턴 진행'");
        }
        else log.Add("bag use skipped (no bandage in stock)");
        p0.Health = Math.Max(p0.Health, hp); a.RefreshFieldBags();

        // F. Co-op pawn rules here: lead + join; the helper leaving leaves it solo (nobody re-bound); the lead leaving → the helper leads.
        var open = OpenSites(a);
        if (open.Count > 0)
        {
            int x = open[0]; Place(place, Option(place, 0, FieldSpotKind.Lead, x), "lead"); Place(place, Option(place, 1, FieldSpotKind.Join, x), "join");
            var chips = place.ChipsFor(1); Check(chips.Count == 3 && chips[0].On && chips[0].Enabled, "Helper chips 함께 · 망보기 · 조명");
            Check(place.Unassign(1) && pl.Current.RunFor(x).Support < 0 && pl.Current.Actions[1] == FieldAction.Hush && pl.Plan.Find(x).Solo, "Helper off: solo, not re-bound");
            Place(place, Option(place, 1, FieldSpotKind.Join, x), "join again"); Check(place.Unassign(0) && pl.Plan.Find(x).Lead == 1 && pl.Current.Actions[0] == FieldAction.Hush, "Lead off: helper leads");
            place.Unassign(1); log.Add("co-op pawn rules");
        }
        else log.Add("co-op skipped (no open object here)");
        return "PASS board · " + string.Join(" · ", log);
    }
}
