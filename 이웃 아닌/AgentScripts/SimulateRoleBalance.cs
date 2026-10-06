using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Demo5.FrontEnd;
using Demo5.NightRun;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

// A small, reproducible policy comparison using the shipped roster and prefab rules.
// Run in the Editor after both role builders. Does not edit any game data or start Play Mode.
public static class SimulateRoleBalance
{
    const string PartyPath = "Assets/Data/PartyRoster.asset";
    const string CreaturePath = "Assets/Data/BattleCreatures.asset";
    const string RulesPath = "Assets/Prefabs/Settlement/ExpeditionBattlePanel.prefab";
    const string OutputPath = "기획/크리쳐-역할밸런스-시뮬레이션-2026-09-25.json";
    const int RoundLimit = 50, StepLimit = 1200, BaseSeed = 250925;

    sealed class Scenario
    {
        public string Id, Label;
        public string[] Party, Creatures;
        public int[] Ammo;
        public int Injury, Infected, RegionTier;
        public bool Opening, Stress;
    }
    static readonly Scenario[] Scenarios =
    {
        new Scenario { Id="intro-infected-1", Label="시작 부상 2명 · 감염자 1", Party=new[]{"scout","medic"}, Ammo=new[]{2,0}, Injury=1, Infected=1, Opening=true },
        new Scenario { Id="intro-infected-2", Label="시작 부상 2명 · 감염자 2", Party=new[]{"scout","medic"}, Ammo=new[]{2,0}, Injury=1, Infected=2, Opening=true },
        new Scenario { Id="resident-1", Label="시작 부상 2명 · 관리실 그것 1", Party=new[]{"scout","medic"}, Ammo=new[]{2,0}, Injury=1, Creatures=new[]{"02-listener"}, Opening=true },
        new Scenario { Id="tier1-defender-assault", Label="3명 · 방어+공격", Party=new[]{"scout","medic","mechanic"}, Ammo=new[]{2,1,1}, RegionTier=1, Creatures=new[]{"01-door-bearer","04-seam-hound"} },
        new Scenario { Id="tier1-assault-disruption", Label="3명 · 공격+방해", Party=new[]{"scout","cook","guard"}, Ammo=new[]{2,1,1}, RegionTier=1, Creatures=new[]{"03-under-table","07-moth-nest"} },
        new Scenario { Id="tier2-ranged-disruption", Label="신규 3명 · 원거리+방해", Party=new[]{"mechanic","cook","researcher"}, Ammo=new[]{1,1,2}, RegionTier=2, Creatures=new[]{"06-meter-keeper","08-puddle"} },
        new Scenario { Id="tier2-three-roles", Label="4명 · 방어+방해+공격", Party=new[]{"scout","medic","mechanic","guard"}, Ammo=new[]{2,0,1,1}, RegionTier=2, Creatures=new[]{"01-door-bearer","05-laundry","10-twin-coat"} },
        new Scenario { Id="tier3-stress-support", Label="신규 4명 · 심부 지원 혼합 스트레스", Party=new[]{"mechanic","cook","researcher","guard"}, Ammo=new[]{1,1,2,2}, RegionTier=3, Creatures=new[]{"09-stairback","11-root-receiver","12-bellied-cart"}, Stress=true },
    };

    sealed class Trial
    {
        public int Seed, Rounds, Steps, DownMembers, HealthLost, Shots, Noise, Reinforcements, Dodges, Guards, Melee;
        public string Outcome;
        public bool Stalled;
        public int[] RemainingHealth;
    }
    sealed class Row
    {
        public string Scenario, Label, Policy;
        public int Seeds, Wins, Losses, Stalls, RunsWithDown, RunsWithReinforcement, MaxRounds;
        public double WinRate, LossRate, DownRate, StallRate, MeanDownMembers, MeanRounds, P95Rounds;
        public double MeanHealthLost, MeanShots, MeanNoise, MeanReinforcements, MeanDodges, MeanGuards, MeanMelee;
        public object Inputs;
        public List<Trial> ReproductionExamples = new List<Trial>();
        public List<string> Findings = new List<string>();
    }
    sealed class Report
    {
        public string CreatedUtc, Limitations, Policy, Output;
        public int SeedsPerPolicy, TotalTrials, BaseSeed, RoundLimit, StepLimit;
        public object PrefabRules, PartyProfiles, CreatureProfiles;
        public List<Row> Rows = new List<Row>();
        public List<string> Findings = new List<string>();
    }

    public static string Run(int seeds = 250)
    {
        if (seeds < 1 || seeds > 5000) throw new ArgumentOutOfRangeException(nameof(seeds));
        var party = AssetDatabase.LoadAssetAtPath<PartyRoster>(PartyPath);
        var creatures = AssetDatabase.LoadAssetAtPath<BattleCreatureRoster>(CreaturePath);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RulesPath);
        var panel = prefab ? prefab.GetComponent<ExpeditionBattlePanel>() : null;
        if (!party || !creatures || !panel || panel.Rules == null) throw new Exception("Missing applied party/creature roster or battle prefab rules.");
        if (creatures.Pool.Any()) throw new Exception("Apply BuildCreatureRoles first: opening creature pool is not empty.");
        string sharedRulesBefore = JsonConvert.SerializeObject(panel.Rules);
        var report = new Report
        {
            CreatedUtc = DateTime.UtcNow.ToString("o"), SeedsPerPolicy = seeds, BaseSeed = BaseSeed, RoundLimit = RoundLimit, StepLimit = StepLimit,
            Output = OutputPath,
            Limitations = "현재 숫자와 단순 정책의 비교 검사. 완성된 지역의 승률/전체 밸런스 검증이 아님. 아이템·치료·식사·철수·사전 탐험 피해를 사용하지 않아 해당 특성 효과는 이 수치에 포함되지 않음. 중후반 탄약량과 명시 조합은 검사 조건이며 실제 지역 보상을 확정하지 않음.",
            Policy = "양쪽 모두 근접 우선 > 사격 > 접근 > 방어. 회피 정책만 이번 적 차례의 예고 타격을 읽고 안전한 인접 칸으로 이동(없으면 방어). 예고 무시는 해당 확인만 생략. 같은 시나리오/실행 번호는 같은 초기 seed지만 행동 차이로 이후 난수 소비는 달라짐.",
            PrefabRules = JsonConvert.DeserializeObject(sharedRulesBefore),
            PartyProfiles = party.Candidates.Where(c => c != null).Select(c => new { c.Id, c.DisplayName, c.Health, c.Aim, c.BagCapacity, c.CombatRole, c.Traits }).ToArray(),
            CreatureProfiles = creatures.Creatures.Where(c => c != null).Select(c => new { c.Id, c.Name, Role=c.RoleName, c.Health, c.Damage, c.Armor, c.FrontArmor, c.ShotArmor, c.ShotEvasion, c.HitChance, Attack=c.Attack.ToString(), Gait=c.Gait.ToString(), c.StartDepth, c.HoldDepth, c.Weight, c.MinRegionTier, c.SuppressReinforcements }).ToArray()
        };
        for (int scenario = 0; scenario < Scenarios.Length; scenario++)
        {
            var spec = Scenarios[scenario];
            var candidates = spec.Party.Select(id => party.Candidates.Single(c => c != null && c.Id == id)).ToArray();
            var lineup = spec.Creatures?.Select(id => creatures.Find(id) ?? throw new Exception("Missing " + id)).ToArray();
            var rules = JsonConvert.DeserializeObject<FieldBattleRules>(sharedRulesBefore);
            bool solitary = lineup != null && lineup.Length == 1 && lineup[0].SuppressReinforcements;
            if (solitary) rules.MaxReinforcements = 0;
            var pool = creatures.PoolForTier(spec.RegionTier).ToArray();
            if (lineup != null && !solitary && lineup.Any(c => !pool.Contains(c))) throw new Exception("Scenario includes a creature locked in this region: " + spec.Id);
            var rows = new List<Row>();
            foreach (bool dodge in new[] { true, false })
            {
                var trials = new List<Trial>();
                for (int run = 0; run < seeds; run++)
                    trials.Add(Play(spec, candidates, lineup, pool, rules, dodge, BaseSeed + scenario * 10000 + run));
                var row = Summarize(spec, candidates, rules, dodge, trials);
                if (solitary && trials.Any(t => t.Reinforcements != 0)) throw new Exception("The single resident received a reinforcement.");
                rows.Add(row); report.Rows.Add(row); report.TotalTrials += trials.Count;
            }
            if (spec.Creatures != null && rows[0].MeanHealthLost > rows[1].MeanHealthLost + 1)
                report.Findings.Add(spec.Id + ": 회피 정책의 평균 피해가 무시보다 1 이상 큼. 경로/목표 선택이 단순하므로 자동으로 수치 결함으로 판단하지 말고 seed 재검토.");
        }
        if (sharedRulesBefore != JsonConvert.SerializeObject(panel.Rules)) throw new Exception("Simulation mutated the shared prefab rules.");
        report.Findings.AddRange(report.Rows.SelectMany(r => r.Findings.Select(f => r.Scenario + " / " + r.Policy + ": " + f)));
        var path = Path.GetFullPath(OutputPath);
        if (!Directory.Exists(Path.GetDirectoryName(path))) throw new Exception("Expected the existing 기획 directory.");
        File.WriteAllText(path, JsonConvert.SerializeObject(report, Formatting.Indented), new UTF8Encoding(false));
        var summary = new StringBuilder();
        summary.AppendLine("Role balance probe · " + report.TotalTrials + " trials · " + path);
        foreach (var row in report.Rows)
            summary.AppendLine(string.Format("{0} / {1}: 승 {2:P1}, 패 {3:P1}, 다운 {4:P1}, 교착 {5:P1}; 평균 {6:0.00}라운드 / 피해 {7:0.00} / 증원 {8:0.00}",
                row.Scenario, row.Policy, row.WinRate, row.LossRate, row.DownRate, row.StallRate, row.MeanRounds, row.MeanHealthLost, row.MeanReinforcements));
        summary.AppendLine("검토 표식 " + report.Findings.Count + "건. 단순 정책 비교이며 게임 전체 밸런스 판정이 아닙니다.");
        return summary.ToString();
    }

    static Trial Play(Scenario spec, PartyCandidate[] candidates, BattleCreature[] lineup, BattleCreature[] pool, FieldBattleRules rules, bool dodge, int seed)
    {
        // Never hard-code HP or construct anonymous traitless Adventurers for these simulations.
        var party = candidates.Select(c => c.CreateAdventurer()).ToArray();
        for (int i = 0; i < party.Length; i++)
        {
            party[i].Health = Math.Max(1, party[i].MaxHealth - spec.Injury);
            if (party[i].MaxHealth != candidates[i].Health || JsonConvert.SerializeObject(party[i].Traits) != JsonConvert.SerializeObject(candidates[i].Traits ?? new HumanTraits()))
                throw new Exception("CreateAdventurer did not preserve current profile/traits: " + candidates[i].Id);
        }
        var ammo = party.Select((p, i) => new { p, n = i < spec.Ammo.Length ? spec.Ammo[i] : 0 }).ToDictionary(x => x.p, x => x.n);
        var random = new Random(seed);
        var s = new FieldBattleState(party, lineup?.Length ?? spec.Infected, p => ammo[p],
            p => { if (ammo[p] <= 0) return false; ammo[p]--; return true; }, () => random.Next(0, 100), rules, lineup, pool);
        int before = party.Sum(p => p.Health);
        var trial = new Trial { Seed = seed };
        while (s.Outcome == FieldBattleOutcome.Playing && s.Round <= RoundLimit && trial.Steps < StepLimit)
        {
            trial.Steps++;
            if (s.Current.Enemy) { if (!s.EnemyStep()) throw new Exception("Enemy phase failed to advance, seed " + seed); continue; }
            bool Marked(List<EnemyIntent> plans) => plans.Any(p => p.Kind == EnemyIntentKind.Strike && p.Hits != null && p.Hits.Any(h => h.Target == s.Actor));
            if (dodge && Marked(s.PredictIntents()))
            {
                var safe = new[] { (d:0,l:1), (d:0,l:-1), (d:-1,l:0), (d:1,l:0) }
                    .Select(m => (d:s.Current.Depth+m.d, l:s.Current.Lane+m.l))
                    .Where(m => s.CanMove(m.d,m.l) && !Marked(s.PredictIntents(s.Actor,m.d,m.l)))
                    .OrderBy(m => m.d).ToArray();
                if (safe.Length > 0 && s.Move(safe[0].d,safe[0].l)) { trial.Dodges++; continue; }
                s.Guard(); trial.Guards++; continue;
            }
            var targets = Enumerable.Range(0,s.Units.Count).Where(i => s.Units[i].Enemy && s.Units[i].Alive).ToArray();
            int melee = targets.Where(i => s.InMeleeReach(i) && s.ExpectedDamage(i,false)>0)
                .OrderByDescending(i => s.Units[i].Stagger).ThenBy(i => s.Units[i].Health).DefaultIfEmpty(-1).First();
            if (melee >= 0 && s.Attack(melee,false)) { trial.Melee++; continue; }
            int shot = targets.Where(i => s.CanAttack(i,true) && s.ExpectedDamage(i,true)>0)
                .OrderByDescending(i => s.Units[i].Stagger).ThenByDescending(i => s.Units[i].Creature?.Attack == CreatureAttack.Broadcast)
                .ThenBy(i => s.Units[i].Depth).ThenBy(i => s.Units[i].Health).DefaultIfEmpty(-1).First();
            if (shot >= 0 && s.Attack(shot,true)) continue;
            bool Safe(int d,int l) => s.CanMove(d,l) && (!dodge || !Marked(s.PredictIntents(s.Actor,d,l)));
            if (s.Current.Depth>0 && Safe(s.Current.Depth-1,s.Current.Lane)) { s.Move(s.Current.Depth-1,s.Current.Lane); continue; }
            if (!s.Moved && s.Current.Depth==0)
            {
                int front=targets.Where(i=>s.Units[i].Depth==0).OrderBy(i=>Math.Abs(s.Units[i].Lane-s.Current.Lane)).DefaultIfEmpty(-1).First();
                if(front>=0)
                {
                    int step=Math.Sign(s.Units[front].Lane-s.Current.Lane);
                    if(step!=0 && Safe(0,s.Current.Lane+step)) { s.Move(0,s.Current.Lane+step); continue; }
                }
            }
            s.Guard(); trial.Guards++;
        }
        trial.Stalled=s.Outcome==FieldBattleOutcome.Playing; trial.Outcome=s.Outcome.ToString(); trial.Rounds=s.Round;
        trial.DownMembers=party.Count(p=>p.Health<=0); trial.HealthLost=before-party.Sum(p=>Math.Max(0,p.Health));
        trial.Shots=s.AmmoSpent; trial.Noise=s.ResultNoise; trial.Reinforcements=s.Reinforcements;
        trial.RemainingHealth=party.Select(p=>p.Health).ToArray();
        return trial;
    }

    static Row Summarize(Scenario spec, PartyCandidate[] candidates, FieldBattleRules rules, bool dodge, List<Trial> trials)
    {
        int n=trials.Count; var rounds=trials.Select(t=>t.Rounds).OrderBy(r=>r).ToArray();
        var row=new Row
        {
            Scenario=spec.Id, Label=spec.Label, Policy=dodge?"read-and-dodge":"ignore-warnings", Seeds=n,
            Wins=trials.Count(t=>t.Outcome==FieldBattleOutcome.Victory.ToString()), Losses=trials.Count(t=>t.Outcome==FieldBattleOutcome.Defeat.ToString()),
            Stalls=trials.Count(t=>t.Stalled), RunsWithDown=trials.Count(t=>t.DownMembers>0), RunsWithReinforcement=trials.Count(t=>t.Reinforcements>0),
            MaxRounds=rounds.Last(), MeanRounds=trials.Average(t=>t.Rounds), P95Rounds=rounds[Math.Max(0,(int)Math.Ceiling(n*.95)-1)],
            MeanDownMembers=trials.Average(t=>t.DownMembers), MeanHealthLost=trials.Average(t=>t.HealthLost), MeanShots=trials.Average(t=>t.Shots),
            MeanNoise=trials.Average(t=>t.Noise), MeanReinforcements=trials.Average(t=>t.Reinforcements), MeanDodges=trials.Average(t=>t.Dodges),
            MeanGuards=trials.Average(t=>t.Guards), MeanMelee=trials.Average(t=>t.Melee),
            Inputs=new { Party=spec.Party, MaxHealth=candidates.Select(c=>c.Health).ToArray(), StartingHealth=candidates.Select(c=>Math.Max(1,c.Health-spec.Injury)).ToArray(),
                Ammo=spec.Ammo, TotalAmmo=spec.Ammo.Sum(), spec.Creatures, spec.Infected, spec.RegionTier, spec.Stress, rules.MaxReinforcements, rules.MaxEnemies }
        };
        row.WinRate=(double)row.Wins/n; row.LossRate=(double)row.Losses/n; row.DownRate=(double)row.RunsWithDown/n; row.StallRate=(double)row.Stalls/n;
        row.ReproductionExamples.AddRange(trials.Where(t=>t.Stalled || t.Outcome==FieldBattleOutcome.Defeat.ToString()).Take(8));
        row.ReproductionExamples.AddRange(trials.Where(t=>t.DownMembers>0 && !row.ReproductionExamples.Contains(t)).OrderByDescending(t=>t.HealthLost).Take(2));
        if(row.Stalls>0) row.Findings.Add("교착/상한 초과 " + row.Stalls + "건. 재현 seed의 이동/사거리/탄약 검토 필요.");
        if(dodge && row.LossRate>.2) row.Findings.Add("예고 회피 정책의 패배율이 20% 초과.");
        if(spec.Opening && dodge && row.DownRate>.25) row.Findings.Add("도입 조건에서 회피 정책의 대원 쓰러짐이 25% 초과.");
        if(row.P95Rounds>20) row.Findings.Add("95백분위가 20라운드 초과: 장기전 원인 검토.");
        return row;
    }
}
