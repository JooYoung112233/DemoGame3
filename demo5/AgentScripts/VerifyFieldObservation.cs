using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;

// Pure rule checks. No scene, prefab, save slot, random loot or live campaign is touched.
public static class VerifyFieldObservation
{
    sealed class Facts : IFieldPlanFacts, IFieldListenFacts, IFieldObservationFacts
    {
        public bool[] Up, Lamps;
        public readonly Dictionary<int, FieldSiteFacts> Sites = new Dictionary<int, FieldSiteFacts>();
        public readonly Dictionary<string, FieldPause> Blocks = new Dictionary<string, FieldPause>();
        public Facts(int count) { Up = Enumerable.Repeat(true, count).ToArray(); Lamps = new bool[count]; }
        public int Members => Up.Length;
        public int LightBonus => 20;
        public bool Alive(int member) => member >= 0 && member < Members && Up[member];
        public bool HasLight(int member) => Alive(member) && Lamps[member];
        public bool HasTool(int site, int member) => true;
        public FieldSiteFacts Site(int site) => Sites.TryGetValue(site, out var result) ? result : new FieldSiteFacts { InRoom = true, Searchable = true };
        public FieldPause ListenBlock(int door) => FieldPause.None;
        public FieldPause ObserveBlock(string id) => Blocks.TryGetValue(id, out var result) ? result : FieldPause.None;
    }

    static FieldOrder Search(int site, int member, int pace = 1, int duty = 0, bool solo = false, int prefer = -1)
        => new FieldOrder { Site = site, Lead = member, Pace = pace, Duty = duty, Solo = solo, Prefer = prefer };
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static string Snapshot(FieldTurnPlan p) => p.Version + ":" + string.Join("/", p.Orders.Select(o => $"{o.Site}:{o.Lead}:{o.Support}:{o.Prefer}:{o.Pace}:{o.Duty}:{o.Solo}"))
        + "|" + string.Join("/", p.Listens.Select(l => $"{l.Door}:{l.Member}")) + "|" + string.Join("/", p.Observations.Select(o => $"{o.Id}:{o.Member}"));
    static string Snapshot(FieldSiteState s) => $"{s.TurnsUsed}:{s.PartyRoom}:{s.Gauge}:{s.Danger}:{s.ResidentRoom}:{s.Next}:{s.Resident}:{s.Encounter}:{s.PassedBy}:{s.Noticed}:{s.Noted}";

    public static string Run() => Rules();
    public static string Rules()
    {
        var facts = new Facts(1); var plan = new FieldTurnPlan();
        plan.AssignObserve(0, "mall.hidden_doyun"); var k = plan.Check(facts);
        Require(k.Observations.Count == 1 && k.Actions[0] == FieldAction.Observe && k.ObserveOf[0] == "mall.hidden_doyun", "A trace must have one member action.");
        Require(k.Noise == 0 && !k.HushedAll && k.Hushing == 0 && k.Full && plan.HasAssignments, "Observation must be silent, full, and not hushing.");
        int version = plan.Version; string snap = Snapshot(plan); plan.Check(facts); plan.Check(facts);
        Require(Snapshot(plan) == snap, "Forecast mutated the observation plan.");
        var copy = plan.Clone(); copy.ReleaseObserve("mall.hidden_doyun");
        Require(plan.Observations.Count == 1 && !copy.HasAssignments, "Clone/release shares observation objects.");
        copy = plan.Clone(); copy.Observations[0].Member = 5;
        Require(plan.Observations[0].Member == 0, "Clone did not copy observation values.");
        plan.Keep(k); Require(plan.Observations.Count == 1, "Keep consumed the observation before a turn.");
        plan.Adopt(k); Require(plan.Observations.Count == 1, "Adopt consumed the observation before completion.");
        Require(plan.PruneObservations(k).Count == 1 && !plan.HasAssignments, "Successful observation was not removed exactly once.");
        version = plan.Version; Require(plan.PruneObservations(k).Count == 0 && plan.Version == version, "Pruning the same completion changed the plan twice.");

        // Direct assignments replace one another; helper preferences must never silently steal an observer.
        facts = new Facts(3); plan = new FieldTurnPlan(); plan.Assign(Search(1, 0)); plan.Keep(plan.Check(facts));
        Require(plan.Find(1).Support == 1, "Fixture: member 1 should support the search.");
        var notes = plan.AssignObserve(1, "a"); k = plan.Check(facts);
        Require(notes.Any(n => n.Kind == FieldMoveKind.SupportMoved) && plan.Find(1).Support == -1 && k.RunFor(1).Support == 2, "Observer kept their old support action.");
        plan.AssignObserve(0, "a");
        Require(plan.Orders.Count == 0 && plan.Observations.Count == 1 && plan.ObserveAt("a").Member == 0 && plan.ObserveBy(1) == null, "Reassigned trace kept a previous lead/member.");
        plan.AssignObserve(0, "b"); Require(plan.ObserveAt("a") == null && plan.ObserveAt("b").Member == 0, "One member retained two traces.");
        plan.AssignListen(0, 1); Require(plan.Observations.Count == 0 && plan.Listens.Count == 1, "Observe to listen left both actions.");
        plan.AssignObserve(0, "b"); Require(plan.Listens.Count == 0, "Listen to observe left both actions.");
        plan.Assign(Search(1, 0, solo: true)); Require(plan.Observations.Count == 0, "Observe to lead left both actions.");
        plan.AssignObserve(1, "b"); plan.Assign(Search(1, 0, prefer: 1));
        Require(plan.Check(facts).RunFor(1).Support == 2 && plan.ObserveBy(1) != null, "A support preference stole the observer.");
        version = plan.Version; plan.AssignObserve(-1, "c"); plan.AssignObserve(1, " ");
        Require(plan.Version == version, "Invalid assignment changed the plan.");
        plan.Clear(); version = plan.Version; plan.Clear(); Require(!plan.HasAssignments && plan.Version == version, "Clear did not include observations or changed twice.");

        facts = new Facts(2); facts.Lamps[1] = true; plan.Assign(Search(1, 0, duty: 2)); plan.AssignObserve(1, "lamp-busy");
        k = plan.Check(facts); Require(k.PauseFor(1) == FieldPause.NoLight && k.Observations.Count == 1, "Observing with a lamp also supported a light search.");
        facts.Sites[1] = new FieldSiteFacts { InRoom = true, Searchable = true, Opened = true, Progress = 1, Required = 2, Pace = 1, Bonus = 10 };
        plan = new FieldTurnPlan(); plan.Assign(Search(1, 0)); plan.AssignObserve(1, "a"); k = plan.Check(facts);
        Require(k.RunFor(1).Forfeits && k.RunFor(1).Bonus == 0, "An observing helper incorrectly preserved the together bonus.");

        foreach (var reason in new[] { FieldPause.OtherRoom, FieldPause.Complete, FieldPause.NoTool, FieldPause.Downed })
        {
            facts = new Facts(2); facts.Blocks["blocked"] = reason;
            if (reason == FieldPause.Downed) facts.Up[1] = false;
            plan = new FieldTurnPlan(); plan.AssignObserve(1, "blocked"); k = plan.Check(facts);
            Require(k.Observations.Count == 0 && k.ObservePauseFor(1) == reason && k.HushedAll, "Blocked observation spent a running action: " + reason);
            Require(plan.PruneObservations(k).Count == 1 && !plan.HasAssignments, "Blocked observation was not released.");
        }

        // One summed turn, not a second story clock. Observation and listening add zero to search noise.
        facts = new Facts(4); plan = new FieldTurnPlan(); plan.Assign(Search(1, 0, 0, solo: true)); plan.Assign(Search(2, 1, 1, solo: true));
        plan.AssignListen(2, FieldSiteState.Corridor); plan.AssignObserve(3, "a"); k = plan.Check(facts);
        Require(k.Runs.Count == 2 && k.Listens.Count == 1 && k.Observations.Count == 1 && k.Noise == 4 && k.Full, "Mixed actions were not summed into a single plan.");
        var site = new FieldSiteState(new FieldSiteRules(), () => 0, false, 1, 2);
        var predicted = site.Copy(); predicted.EndTurn(k.Noise, k.HushedAll);
        site.EndTurn(k.Noise, k.HushedAll);
        Require(site.TurnsUsed == 1 && Snapshot(site) == Snapshot(predicted), "Mixed plan forecast diverged or advanced two turns.");
        plan = new FieldTurnPlan(); plan.AssignObserve(0, "a"); k = plan.Check(new Facts(1));
        site = new FieldSiteState(new FieldSiteRules(), () => 0, false, 0, 2);
        site.EndTurn(k.Noise, k.HushedAll); Require(site.Gauge == 2, "Silent observation incorrectly received hush relief.");
        site = new FieldSiteState(new FieldSiteRules(), () => 0, false, 2, 0, FieldSiteState.Arcade);
        site.EndTurn(0, false); site.EndTurn(0, false); Require(site.Incoming, "Fixture: resident must be incoming.");
        var hushed = site.Copy(); hushed.EndTurn(0, true); site.EndTurn(k.Noise, k.HushedAll);
        Require(site.Encounter && !hushed.Encounter && hushed.PassedBy, "Observation incorrectly allowed a hush-only pass-by.");

        // Assignment interleavings: each living member is counted once, forecast is pure, and noise comes only from searches.
        var random = new Random(25); facts = new Facts(6); plan = new FieldTurnPlan();
        for (int iteration = 0; iteration < 1200; iteration++)
        {
            int member = random.Next(6), choice = random.Next(6);
            if (choice == 0) plan.Assign(Search(random.Next(4), member, random.Next(3), random.Next(3), random.Next(2) == 0));
            else if (choice == 1) plan.AssignListen(member, random.Next(4));
            else if (choice == 2) plan.AssignObserve(member, "trace-" + random.Next(4));
            else if (choice == 3) plan.ReleaseObserve("trace-" + random.Next(4));
            else if (choice == 4) plan.ReleaseListen(random.Next(4));
            else plan.Release(random.Next(4));
            snap = Snapshot(plan); k = plan.Check(facts); Require(Snapshot(plan) == snap, "Fuzz: forecast changed the plan.");
            var used = new HashSet<int>();
            foreach (var run in k.Runs) { Require(used.Add(run.Lead), "Fuzz: duplicate lead."); if (run.Support >= 0) Require(used.Add(run.Support), "Fuzz: duplicate helper."); }
            foreach (var listen in k.Listens) Require(used.Add(listen.Member), "Fuzz: listener spent a second action.");
            foreach (var observe in k.Observations) Require(used.Add(observe.Member), "Fuzz: observer spent a second action.");
            Require(k.Noise == k.Runs.Sum(r => r.Noise), "Fuzz: observation/listening added noise.");
            plan.Keep(k);
        }
        return "PASS: observation action, single-turn summed forecast, silent/non-hush consequences, search/listen exclusivity, helper/light/bonus handling, blocked/downed observations, clone/keep/adopt/prune/clear, and 1,200 assignment interleavings. No scene or save changed.";
    }
}
