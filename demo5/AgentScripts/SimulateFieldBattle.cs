using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demo5.FrontEnd;
using Demo5.NightRun;

// Balance probe for the provisional field-battle rules. Pure rules state; no scene, no UI.
// Policies: "greedy" melee in reach > shoot nearest > step to the front > guard.
// "melee" never shoots. "tank" guards whenever the forecast says it will be bitten and it cannot finish a foe.
public static class SimulateFieldBattle
{
    sealed class Tally { public int Runs, Wins, Losses, Knockouts, Reinforced, Rounds, HealthLost, Shots, Noise, Stalls, MaxRounds; }
    enum Policy { Greedy, Melee, Tank }
    static void Add(Tally to, Tally from)
    {
        to.Runs += from.Runs; to.Wins += from.Wins; to.Losses += from.Losses; to.Stalls += from.Stalls; to.Knockouts += from.Knockouts; to.Reinforced += from.Reinforced;
        to.Rounds += from.Rounds; to.HealthLost += from.HealthLost; to.Shots += from.Shots; to.Noise += from.Noise; to.MaxRounds = Math.Max(to.MaxRounds, from.MaxRounds);
    }
    static void Finish(Tally t, FieldBattleState s, Adventurer[] party, int before)
    {
        t.Runs++; if (s.Outcome == FieldBattleOutcome.Victory) t.Wins++; if (s.Outcome == FieldBattleOutcome.Defeat) t.Losses++; if (s.Outcome == FieldBattleOutcome.Playing) t.Stalls++;
        if (party.Any(p => p.Health <= 0)) t.Knockouts++; if (s.Reinforcements > 0) t.Reinforced++;
        t.Rounds += s.Round; t.MaxRounds = Math.Max(t.MaxRounds, s.Round); t.HealthLost += before - party.Sum(p => Math.Max(0, p.Health)); t.Shots += s.AmmoSpent; t.Noise += s.ResultNoise;
    }
    // Rounds each member carries: the same count for everyone, or one entry per member (missing entries carry none).
    static int[] Each(int ammoEach, int members) => Enumerable.Repeat(ammoEach, members).ToArray();
    static Dictionary<Adventurer, int> Stock(Adventurer[] party, int[] ammo) => party.Select((p, i) => (p: p, n: ammo != null && i < ammo.Length ? Math.Max(0, ammo[i]) : 0)).ToDictionary(x => x.p, x => x.n);
    // Per-member stock rows: both carry 2, one member carries the 2 real starting rounds (found in the intro), nobody carries any.
    static readonly (string Label, int[] Ammo)[] AmmoRows = { ("탄약 2·2", new[] { 2, 2 }), ("탄약 2·0 (실제 시작 재고)", new[] { 2, 0 }), ("탄약 0·0", new[] { 0, 0 }) };

    static Tally Play(FieldBattleRules rules, int enemies, int[] health, int[] maximum, int ammoEach, Policy policy, int runs, int seed)
        => Play(rules, enemies, health, maximum, Each(ammoEach, health.Length), policy, runs, seed);
    static Tally Play(FieldBattleRules rules, int enemies, int[] health, int[] maximum, int[] ammoPerMember, Policy policy, int runs, int seed)
    {
        var random = new Random(seed); var t = new Tally();
        for (int r = 0; r < runs; r++)
        {
            var party = health.Select((h, i) => new Adventurer("A" + i, "", "", maximum[i], 0) { Health = h }).ToArray();
            var ammo = Stock(party, ammoPerMember);
            var s = new FieldBattleState(party, enemies, p => ammo[p], p => { if (ammo[p] <= 0) return false; ammo[p]--; return true; }, () => random.Next(0, 100), rules);
            int before = party.Sum(p => p.Health), guard = 0;
            while (s.Outcome == FieldBattleOutcome.Playing && guard++ < 600)
            {
                if (s.Current.Enemy) { s.EnemyStep(); continue; }
                var targets = Enumerable.Range(0, s.Units.Count).Where(i => s.Units[i].Enemy && s.Units[i].Alive).ToList();
                int melee = targets.Where(i => s.InMeleeReach(i)).OrderBy(i => s.Units[i].Health).DefaultIfEmpty(-1).First();
                if (policy == Policy.Tank && s.PredictIntents().Any(p => p.Kind == EnemyIntentKind.Attack && p.Target == s.Actor)
                    && !(melee >= 0 && s.Units[melee].Health <= s.DamageFor(false))) { s.Guard(); continue; }
                if (melee >= 0 && s.Attack(melee, false)) continue;
                int near = targets.OrderBy(i => s.Units[i].Depth).ThenBy(i => s.Units[i].Health).First();
                if (policy != Policy.Melee && s.CanAttack(near, true) && s.Attack(near, true)) continue;
                if (!s.Moved && s.Current.Depth > 0 && s.CanMove(s.Current.Depth - 1, s.Current.Lane)) { s.Move(s.Current.Depth - 1, s.Current.Lane); continue; }
                if (!s.Moved && s.Current.Depth == 0)
                {
                    var front = targets.Where(i => s.Units[i].Depth == 0).OrderBy(i => Math.Abs(s.Units[i].Lane - s.Current.Lane)).DefaultIfEmpty(-1).First();
                    if (front >= 0) { int step = Math.Sign(s.Units[front].Lane - s.Current.Lane); if (step != 0 && s.CanMove(0, s.Current.Lane + step)) { s.Move(0, s.Current.Lane + step); continue; } }
                }
                s.Guard();
            }
            Finish(t, s, party, before);
        }
        return t;
    }
    static string Row(string label, Tally t) => string.Format("{0,-26} 승 {1,4:P0} 패 {2,3:P0} 교착 {3,3:P0} · 쓰러짐 {4,4:P0} · 증원 {5,4:P0} · 라운드 {6,4:0.0} (최장 {10,2}) · 잃은 체력 {7,4:0.00} · 사격 {8,3:0.0} · 소음 +{9,3:0.0}",
        label, (double)t.Wins / t.Runs, (double)t.Losses / t.Runs, (double)t.Stalls / t.Runs, (double)t.Knockouts / t.Runs, (double)t.Reinforced / t.Runs, (double)t.Rounds / t.Runs, (double)t.HealthLost / t.Runs, (double)t.Shots / t.Runs, (double)t.Noise / t.Runs, t.MaxRounds);
    static string RulesLine(FieldBattleRules rules) => "규칙 · 증원 기준 소음 " + rules.ReinforcementNoise + " · 사격 소음 " + rules.ShotNoise + " · 최대 증원 " + rules.MaxReinforcements;

    // Default rules as shipped (Inspector values match the code defaults).
    public static string Run() => Report(new FieldBattleRules());
    // Side-by-side variants used to pick the shipped numbers.
    public static string Variants()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 체력 4, 첫 감염자 중열"); sb.Append(Report(new FieldBattleRules { EnemyHealth = 4 }));
        sb.AppendLine("## 체력 5, 첫 감염자 중열"); sb.Append(Report(new FieldBattleRules { EnemyHealth = 5 }));
        sb.AppendLine("## 체력 4, 첫 감염자 전열"); sb.Append(Report(new FieldBattleRules { EnemyHealth = 4, FirstEnemyDepth = 0 }));
        sb.AppendLine("## 체력 5, 첫 감염자 전열"); sb.Append(Report(new FieldBattleRules { EnemyHealth = 5, FirstEnemyDepth = 0 }));
        return sb.ToString();
    }
    static string Report(FieldBattleRules rules)
    {
        const int runs = 3000; var sb = new StringBuilder();
        // Intro start: members arrive at max-1 (e.g. 2/3 and 3/4). Each carries 2 rounds.
        int[] intro = { 2, 3 }, max = { 3, 4 };
        foreach (int enemies in new[] { 1, 2 })
            foreach (Policy policy in Enum.GetValues(typeof(Policy)))
                sb.AppendLine(Row("감염자 " + enemies + " · " + (policy == Policy.Greedy ? "사격 포함" : policy == Policy.Melee ? "근접만" : "방어 우선"), Play(rules, enemies, intro, max, 2, policy, runs, 17 + enemies * 7 + (int)policy)));
        return sb.ToString();
    }

    // ---- creatures (Assets/Data/BattleCreatures.asset) ----
    // "피함": steps out of marked cells when the preview says the new cell is safe, then fights (melee > shot > step in > guard).
    // "무시": fights the same way but never reads the marks.
    static Tally PlayCreatures(FieldBattleRules rules, BattleCreature[] lineup, BattleCreature[] pool, int[] health, int[] maximum, int ammoEach, bool dodge, int runs, int seed)
        => PlayCreatures(rules, lineup, pool, health, maximum, Each(ammoEach, health.Length), dodge, runs, seed);
    static Tally PlayCreatures(FieldBattleRules rules, BattleCreature[] lineup, BattleCreature[] pool, int[] health, int[] maximum, int[] ammoPerMember, bool dodge, int runs, int seed)
    {
        var random = new Random(seed); var t = new Tally();
        for (int r = 0; r < runs; r++)
        {
            var party = health.Select((h, i) => new Adventurer("A" + i, "", "", maximum[i], 0) { Health = h }).ToArray();
            var ammo = Stock(party, ammoPerMember);
            var s = new FieldBattleState(party, lineup.Length, p => ammo[p], p => { if (ammo[p] <= 0) return false; ammo[p]--; return true; }, () => random.Next(0, 100), rules, lineup, pool);
            int before = party.Sum(p => p.Health), guard = 0;
            while (s.Outcome == FieldBattleOutcome.Playing && guard++ < 600)
            {
                if (s.Current.Enemy) { s.EnemyStep(); continue; }
                bool Marked(List<EnemyIntent> plans) => plans.Any(p => p.Kind == EnemyIntentKind.Strike && p.Hits != null && p.Hits.Any(h => h.Target == s.Actor));
                if (dodge && !s.Moved && Marked(s.PredictIntents()))
                {
                    var safe = new[] { (0, 1), (0, -1), (-1, 0), (1, 0) }.Select(m => (d: s.Current.Depth + m.Item1, l: s.Current.Lane + m.Item2))
                        .Where(m => s.CanMove(m.d, m.l) && !Marked(s.PredictIntents(s.Actor, m.d, m.l))).OrderBy(m => m.d).ToList();
                    if (safe.Count > 0) { s.Move(safe[0].d, safe[0].l); continue; }
                    if (Marked(s.PredictIntents())) { s.Guard(); continue; }
                }
                var targets = Enumerable.Range(0, s.Units.Count).Where(i => s.Units[i].Enemy && s.Units[i].Alive).ToList();
                int melee = targets.Where(i => s.InMeleeReach(i) && s.ExpectedDamage(i, false) > 0).OrderByDescending(i => s.Units[i].Stagger).ThenBy(i => s.Units[i].Health).DefaultIfEmpty(-1).First();
                if (melee >= 0 && s.Attack(melee, false)) continue;
                int shot = targets.Where(i => s.CanAttack(i, true) && s.ExpectedDamage(i, true) > 0).OrderByDescending(i => s.Units[i].Stagger).ThenByDescending(i => s.Units[i].Creature?.Attack == CreatureAttack.Broadcast).ThenBy(i => s.Units[i].Depth).ThenBy(i => s.Units[i].Health).DefaultIfEmpty(-1).First();
                if (shot >= 0 && s.Attack(shot, true)) continue;
                if (!s.Moved && s.Current.Depth > 0 && s.CanMove(s.Current.Depth - 1, s.Current.Lane) && (!dodge || !Marked(s.PredictIntents(s.Actor, s.Current.Depth - 1, s.Current.Lane)))) { s.Move(s.Current.Depth - 1, s.Current.Lane); continue; }
                if (!s.Moved && s.Current.Depth == 0)
                {
                    var front = targets.Where(i => s.Units[i].Depth == 0).OrderBy(i => Math.Abs(s.Units[i].Lane - s.Current.Lane)).DefaultIfEmpty(-1).First();
                    if (front >= 0) { int step = Math.Sign(s.Units[front].Lane - s.Current.Lane); if (step != 0 && s.CanMove(0, s.Current.Lane + step) && (!dodge || !Marked(s.PredictIntents(s.Actor, 0, s.Current.Lane + step)))) { s.Move(0, s.Current.Lane + step); continue; } }
                }
                s.Guard();
            }
            Finish(t, s, party, before);
        }
        return t;
    }
    // Every pool creature alone, then random pairs, once per ammo row ({2,2}, {2,0} = the real start, {0,0}).
    // Pass lines: one member's 2 shots call a reinforcement at most 10%, a lone creature never stalls, the longest fight stays within 7 rounds.
    public static string Creatures()
    {
        var roster = UnityEditor.AssetDatabase.LoadAssetAtPath<BattleCreatureRoster>("Assets/Data/BattleCreatures.asset");
        if (!roster) throw new Exception("Run BuildCreatureBattle.Run first");
        var rules = new FieldBattleRules(); var pool = roster.Pool.ToArray(); var sb = new StringBuilder(); const int runs = 400;
        int[] full = { 3, 4 }, intro = { 2, 3 };
        sb.AppendLine(RulesLine(rules));
        var random = new Random(5); var pairs = Enumerable.Range(0, 24).Select(_ => pool.OrderBy(x => random.Next()).Take(2).ToArray()).ToList();
        var summary = new List<string>();
        foreach (var (tag, ammo) in AmmoRows)
        {
            sb.AppendLine("## 1마리 · 대원 3/3·4/4 · " + tag);
            Tally singleDodge = new Tally(), singleNaive = new Tally();
            foreach (var c in pool)
            {
                var d = PlayCreatures(rules, new[] { c }, pool, full, full, ammo, true, runs, 11); var n = PlayCreatures(rules, new[] { c }, pool, full, full, ammo, false, runs, 12);
                sb.AppendLine(Row(c.Name + " · 피함", d)); sb.AppendLine(Row(c.Name + " · 무시", n));
                Add(singleDodge, d); Add(singleNaive, n);
            }
            sb.AppendLine(Row("1마리 평균 · 피함", singleDodge)); sb.AppendLine(Row("1마리 평균 · 무시", singleNaive));
            sb.AppendLine("## 2마리 무작위 · 도입 체력 2/3·3/4 · " + tag);
            Tally both = new Tally(), naive = new Tally();
            foreach (var pair in pairs) { Add(both, PlayCreatures(rules, pair, pool, intro, full, ammo, true, 120, 21)); Add(naive, PlayCreatures(rules, pair, pool, intro, full, ammo, false, 120, 22)); }
            sb.AppendLine(Row("2마리 평균 · 피함", both)); sb.AppendLine(Row("2마리 평균 · 무시", naive));
            summary.Add(Row("요약 1마리 · " + tag, Merge(singleDodge, singleNaive))); summary.Add(Row("요약 2마리 · " + tag, Merge(both, naive)));
        }
        sb.AppendLine("## 탄약별 요약 (피함+무시)"); foreach (var line in summary) sb.AppendLine(line);
        return sb.ToString();
    }
    static Tally Merge(params Tally[] parts) { var t = new Tally(); foreach (var p in parts) Add(t, p); return t; }
    // The site resident (weight 0, not in the random pool): the one fight of the site board, alone, as the board sends it.
    public static string Resident()
    {
        var roster = UnityEditor.AssetDatabase.LoadAssetAtPath<BattleCreatureRoster>("Assets/Data/BattleCreatures.asset");
        var it = roster.Find("02-listener") ?? throw new Exception("No resident"); var rules = new FieldBattleRules(); var pool = roster.Pool.ToArray(); var sb = new StringBuilder(); const int runs = 600;
        int[] full = { 3, 4 }, intro = { 2, 3 };
        sb.AppendLine(RulesLine(rules));
        sb.AppendLine("## 그것(" + it.Name + ") · 체력 " + it.Health + " · 명중 " + it.HitChance + " · 시작 줄 " + it.StartDepth);
        foreach (var (label, hp) in new[] { ("만충", full), ("도입 체력", intro) })
            foreach (var (tag, ammo) in AmmoRows)
            {
                sb.AppendLine(Row(label + " · " + tag + " · 피함", PlayCreatures(rules, new[] { it }, pool, hp, full, ammo, true, runs, 31)));
                sb.AppendLine(Row(label + " · " + tag + " · 무시", PlayCreatures(rules, new[] { it }, pool, hp, full, ammo, false, runs, 32)));
            }
        return sb.ToString();
    }
}
