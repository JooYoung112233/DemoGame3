using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Demo8;
using UnityEngine;
public static class VerifyWorldCampaign
{
    static List<string> checks;
    static void Check(bool condition, string message)
    { if (!condition) throw new Exception("FAIL: " + message); checks.Add(message); }
    static Campaign Clone(Campaign c) => JsonUtility.FromJson<Campaign>(JsonUtility.ToJson(c));
    public static string Rules()
    {
        checks = new List<string>(); var c = Campaign.New();
        Check(c.Valid() && c.provinces.Count == 48 && c.Territory(0) == 2, "48-region initial state");
        Check(Campaign.Adjacent(0, 1) && Campaign.Adjacent(1, 8) && !Campaign.Adjacent(7, 8) && !Campaign.Adjacent(0, 0), "World hex adjacency and row wrap prevention");
        int gold = c.factions[0].gold; c.Build(0, 1);
        Check(c.provinces[0].market == 2 && c.factions[0].gold == gold - 240, "Construction cost and level");
        gold = c.factions[0].gold; c.Build(0, 0); c.Build(1, 0);
        Check(c.factions[0].gold == gold && c.provinces[0].farm == 0 && c.provinces[1].farm == 0, "Build ownership and one-per-turn constraints");
        c = Campaign.New(); c.factions[0].gold = 0; c.Recruit(0, 0);
        Check(c.ArmyOf(0).infantry == 160 && c.factions[0].food == 800, "Unaffordable recruitment is atomic");
        c = Campaign.New(); c.Recruit(0, 2);
        Check(c.ArmyOf(0).cavalry == 45 && c.factions[0].food == 760, "Recruiting consumes both resources");
        c.Build(8, 2); c.Recruit(8, 0);
        Check(c.armies.Count(a => a.owner == 0) == 2 && c.At(8).actions == 0, "Independent second army created in barracks city");
        Check(c.At(8).id != c.ArmyOf(0).id && c.Valid(), "Army identifiers unique and persisted");
        c = Campaign.New(); c.Move(0, 47);
        Check(c.ArmyOf(0).province == 0 && c.ArmyOf(0).actions == 2, "Nonadjacent move rejected without action loss");
        var f = c.Preview(0, 1); int before = c.ArmyOf(0).Count; var copy = Clone(c);
        c.Move(0, 1); copy.Move(0, 1);
        Check(c.provinces[1].owner == 0 && c.provinces[1].order == 35 && c.ArmyOf(0).actions == 0, "Conquest transfers region and consumes remaining actions");
        Check(c.ArmyOf(0).Count == before - f.losses, "Predicted battle casualties equal result");
        Check(JsonUtility.ToJson(c) == JsonUtility.ToJson(copy), "Deterministic battles after serialization");
        c.Reinforce(1); c.Pacify(1); Check(c.provinces[1].garrison == 55 && c.provinces[1].order == 60, "Occupation security measures");
        c.Pacify(1); Check(c.provinces[1].order == 60, "Relief once per turn");
        c.Economy(0, out int dg, out int df); gold = c.factions[0].gold; int food = c.factions[0].food;
        c.EndTurn(); Check(c.factions[0].gold == gold + dg && c.factions[0].food == food + df && c.ArmyOf(0).actions == 2, "Economy forecast and turn reset");
        c = Campaign.New(); int income = c.GoldIncome(c.provinces[0]); c.SetTax(2);
        Check(c.GoldIncome(c.provinces[0]) > income, "Higher tax increases revenue");
        c.EndTurn(); Check(c.provinces[8].order < 85, "Higher tax damages public order");
        c = Campaign.New(); food = c.FoodIncome(c.provinces[8]); c.SetFocus(8, 1);
        Check(c.FoodIncome(c.provinces[8]) > food, "Agricultural city focus raises food production");
        c.SetFocus(8, 3); c.EndTurn(); Check(c.provinces[8].garrison == 73, "Military administration grows garrison");
        c = Campaign.New(); c.Research(2); c.Research(2); Check(c.factions[0].logistics == 1, "Research limited to one per turn");
        c.EndTurn(); c.Research(2); c.EndTurn(); Check(c.ArmyOf(0).actions == 3, "Logistics research increases movement allowance");
        c = Campaign.New(); c.provinces[8].terrain = 2; Check(c.MoveCost(8) == 2, "Mountain movement costs two");
        c.Build(8, 3); Check(c.MoveCost(8) == 1, "Road reduces mountain movement cost");
        c = Campaign.New(); c.provinces[1].owner = 0; c.provinces[2].owner = 0; c.ArmyOf(0).province = 2;
        Check(c.Supplied(0).Contains(2), "Supply traverses connected friendly regions");
        int power = c.Preview(0, 3).attack; c.provinces[1].owner = -1;
        Check(!c.Supplied(0).Contains(2) && c.Preview(0, 3).attack < power, "Cut supply route reduces attack power");
        before = c.ArmyOf(0).Count; c.EndTurn(); Check(c.ArmyOf(0).Count < before, "Isolated army suffers turn attrition");
        c = Campaign.New(); c.SetStance(0, 1);
        Check(c.ArmyOf(0).actions == 0 && c.MoveError(0, 1).Length > 0, "Entrenchment prevents marching");
        c = Campaign.New(); power = c.Preview(0, 1).attack; c.SetStance(0, 2);
        Check(c.Preview(0, 1).attack > power, "Assault stance increases attack");
        c = Campaign.New(); c.Truce(1); c.ArmyOf(0).province = 6; c.provinces[6].owner = 0;
        Check(c.MoveError(0, 7).Contains("휴전") && c.MoveError(1, 6).Contains("휴전"), "Truce prevents attacks by both parties");
        c = Campaign.New(); c.armies.RemoveAll(a => a.owner != 0);
        foreach (var p in c.provinces) { p.owner = p.id < 12 ? 0 : -1; p.order = 100; p.garrison = 65; }
        c.EndTurn(); Check(c.heldTurns == 1 && !c.Finished, "Victory requires holding territory");
        c.EndTurn(); c.EndTurn(); Check(c.outcome.StartsWith("승리"), "12 regions held three turns wins");
        string frozen = JsonUtility.ToJson(c); c.Build(0, 0); c.SetTax(0); c.Research(0); c.Move(0, 1); c.EndTurn();
        Check(JsonUtility.ToJson(c) == frozen, "Finished state is immutable to gameplay commands");
        copy = Clone(c); copy.provinces[0].owner = 900; Check(!copy.Valid(), "Invalid region save rejected");
        copy = Clone(c); copy.armies.Add(copy.armies[0]); Check(!copy.Valid(), "Duplicate army save rejected");
        c = Campaign.New(); c.provinces[0].owner = c.provinces[8].owner = -1; c.EndTurn(); Check(c.outcome.StartsWith("패배"), "Loss of all cities ends campaign");
        c = Campaign.New(); c.provinces[8].order = 1; c.provinces[8].garrison = 25; c.EndTurn(); Check(c.provinces[8].owner == -1, "Unstable city rebels");
        c = Campaign.New(); for (int i = 0; i < 8; i++) c.EndTurn(); Check(c.Territory(1) + c.Territory(2) > 4, "Enemies expand independently on world map");
        var rng = new System.Random(4812);
        for (int seed = 0; seed < 8; seed++)
        {
            c = Campaign.New();
            for (int turn = 0; turn < 50 && !c.Finished; turn++)
            {
                int id = rng.Next(48); c.Build(id, rng.Next(5)); c.Recruit(id, rng.Next(3)); c.Pacify(id); c.SetTax(rng.Next(3)); c.SetFocus(id, rng.Next(4));
                c.Move(0, rng.Next(48)); c.EndTurn(); if (!c.Valid()) throw new Exception("Invalid state in simulation " + seed + "/" + turn);
            }
        }
        Check(true, "Eight seeded 50-turn campaigns preserve invariants");
        Directory.CreateDirectory("Verification"); File.WriteAllLines("Verification/world-rules.txt", checks);
        return "PASS " + checks.Count + " world campaign checks";
    }
    public static string Playthrough()
    {
        var c = Campaign.New(); var rows = new List<string>();
        for (int t = 0; t < 100 && !c.Finished; t++)
        {
            var a = c.ArmyOf(0); if (a == null) break;
            foreach (var p in c.provinces.Where(p => p.owner == 0))
            {
                c.SetFocus(p.id, 3); if (p.order < 60) c.Pacify(p.id); if (p.garrison < 55) c.Reinforce(p.id);
                if (c.factions[0].gold > 500 && p.market < 2) c.Build(p.id, 1);
                if (c.factions[0].food < 250) c.SetFocus(p.id, 1);
            }
            while (a.Count < 460 && c.factions[0].gold > 220 && c.factions[0].food > 100) c.Recruit(a.province, 0);
            if (c.Territory(0) < Campaign.VictoryRegions)
            {
                var targets = c.provinces.Where(p => p.owner != 0 && c.MoveError(0, p.id).Length == 0).OrderBy(p => c.Preview(0, p.id).defense).ToList();
                if (targets.Count > 0 && c.Preview(0, targets[0].id).victory) c.Move(0, targets[0].id);
                else
                {
                    // Route through our territory toward an exposed border.
                    var queue = new Queue<int>(); var prev = new Dictionary<int, int>(); queue.Enqueue(a.province); prev[a.province] = -1;
                    while (queue.Count > 0)
                    {
                        int current = queue.Dequeue();
                        if (current != a.province && c.provinces.Any(p => p.owner != 0 && Campaign.Adjacent(current, p.id)))
                        { while (prev[current] != a.province) current = prev[current]; c.Move(0, current); break; }
                        foreach (var p in c.provinces.Where(p => p.owner == 0 && Campaign.Adjacent(current, p.id) && !prev.ContainsKey(p.id))) { prev[p.id] = current; queue.Enqueue(p.id); }
                    }
                }
            }
            c.EndTurn(); if (!c.Valid()) throw new Exception("Invalid playthrough state");
            rows.Add("Turn " + c.turn + " regions=" + c.Territory(0) + " gold=" + c.factions[0].gold + " food=" + c.factions[0].food + " army=" + (c.ArmyOf(0)?.Count ?? 0) + " " + c.outcome);
        }
        Directory.CreateDirectory("Verification"); File.WriteAllLines("Verification/world-playthrough.txt", rows);
        if (!c.outcome.StartsWith("승리")) throw new Exception("Playthrough failed: " + rows.Last());
        return "PASS normal world campaign victory at turn " + c.turn;
    }
    public static string Runtime()
    {
        var view = UnityEngine.Object.FindAnyObjectByType<CampaignView>();
        if (view == null || view.mapCamera == null) throw new Exception("Campaign not running");
        string path = CampaignView.SavePath, old = File.Exists(path) ? File.ReadAllText(path) : null;
        string backup = File.Exists(path + ".bak") ? File.ReadAllText(path + ".bak") : null;
        try
        {
            view.NewCampaign(); view.Act(view.state.Build(8, 2)); view.Act(view.state.Recruit(8, 0));
            view.Act(view.state.SetTax(2)); view.Act(view.state.SetFocus(8, 3)); view.Act(view.state.Research(0));
            view.Act(view.state.Move(0, 1)); view.Act(view.state.Reinforce(1)); view.Act(view.state.Pacify(1)); view.Act(view.state.EndTurn());
            view.Save(); string json = JsonUtility.ToJson(view.state); view.state.EndTurn(); view.Load();
            if (json != JsonUtility.ToJson(view.state)) throw new Exception("World save round trip changed state");
            File.WriteAllText(path, "{invalid"); var before = view.state; view.Load();
            if (!ReferenceEquals(before, view.state)) throw new Exception("Invalid save replaced live state");
        }
        finally
        {
            if (old == null) { if (File.Exists(path)) File.Delete(path); } else File.WriteAllText(path, old);
            if (backup == null) { if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); } else File.WriteAllText(path + ".bak", backup);
            view.NewCampaign();
        }
        return "PASS runtime conquest, multiple armies, policy, research, save/load and invalid-save protection";
    }
}
