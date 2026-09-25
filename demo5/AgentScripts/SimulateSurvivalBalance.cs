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

// Follow-up only: preserves the original 4,000-trial report and all source game data.
// Three fights share HP and two rounds of ammunition; no rest, items, resupply or revival.
public static class SimulateSurvivalBalance
{
    const string Output = "기획/크리쳐-역할밸런스-3연전-2026-09-25.json";
    const int BaseSeed = 250926, RoundLimit = 50, StepLimit = 1200;
    static readonly string[][] Waves =
    {
        new[] { "01-door-bearer", "04-seam-hound" },
        new[] { "06-meter-keeper", "08-puddle" },
        new[] { "05-laundry", "10-twin-coat" }
    };
    sealed class Squad
    {
        public string Id;
        public string[] Members;
    }
    static readonly Squad[] Squads =
    {
        new Squad { Id="mid-two", Members=new[]{"scout","medic"} },
        new Squad { Id="mid-three", Members=new[]{"scout","medic","mechanic"} },
        new Squad { Id="mid-three-new", Members=new[]{"cook","researcher","guard"} }
    };
    sealed class Fight
    {
        public int Wave, Rounds, Steps, HealthBefore, HealthAfter, Damage, AmmoBefore, AmmoAfter, Shots, Reinforcements, NewDownMembers;
        public int Guards, Dodges, Melee, EnemyActions, Windups, Strikes, Recoveries, MaxConsecutiveRecovery, KilledBeforeFirstAttack;
        public bool Win, Lost, Stalled;
        public int[] EndHealth;
    }
    sealed class Trip
    {
        public int Seed, Wins, DownMembers, Damage, Shots, Rounds, Reinforcements;
        public bool Complete, Defeated, Stalled;
        public int[] EndHealth;
        public List<Fight> Fights = new List<Fight>();
    }
    sealed class Row
    {
        public string Squad, Policy;
        public object Inputs;
        public int Trips, Completed, Defeated, Stalls, TripsWithDown, MaxConsecutiveRecovery;
        public double CompletionRate, DefeatRate, DownRate, MeanWins, MeanDamage, MeanShots, MeanRounds, P95Rounds, MeanReinforcements;
        public double MeanGuards, MeanDodges, MeanEnemyWindups, MeanEnemyStrikes, MeanEnemyRecoveries, MeanKilledBeforeFirstAttack;
        public object[] ByWave;
        public List<Trip> Reproduction = new List<Trip>();
        public List<string> Findings = new List<string>();
    }
    sealed class Report
    {
        public string CreatedUtc, Purpose, InjuryRule, Policy, Limitations, Output;
        public int SeedsPerPolicy, TotalTrips, TotalBattles, BaseSeed, RoundLimit, StepLimit;
        public object Rules, PartyProfiles, CreatureProfiles, Waves;
        public List<Row> Rows = new List<Row>();
    }

    public static string Run(int seeds = 250)
    {
        if (seeds<1 || seeds>5000) throw new ArgumentOutOfRangeException(nameof(seeds));
        var roster=AssetDatabase.LoadAssetAtPath<PartyRoster>("Assets/Data/PartyRoster.asset");
        var creatures=AssetDatabase.LoadAssetAtPath<BattleCreatureRoster>("Assets/Data/BattleCreatures.asset");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/ExpeditionBattlePanel.prefab");
        var panel=prefab ? prefab.GetComponent<ExpeditionBattlePanel>() : null;
        if(!roster || !creatures || !panel) throw new Exception("Applied roster and prefab required.");
        string ruleSource=JsonConvert.SerializeObject(panel.Rules);
        var pool=creatures.PoolForTier(2).ToArray();
        var lineups=Waves.Select(w=>w.Select(id=>creatures.Find(id) ?? throw new Exception("Missing "+id)).ToArray()).ToArray();
        if(lineups.SelectMany(x=>x).Any(c=>!pool.Contains(c))) throw new Exception("Apply current regional roles before this probe.");
        var report=new Report
        {
            CreatedUtc=DateTime.UtcNow.ToString("o"), SeedsPerPolicy=seeds, BaseSeed=BaseSeed, RoundLimit=RoundLimit, StepLimit=StepLimit, Output=Output,
            Purpose="같은 탐험 안 3연전의 누적 체력/탄약 부담과 예고 회피 효과를 비교하는 한정 검사.",
            InjuryRule="시작 체력 = 최대 체력 - ceil(최대 체력 × 0.25), 최소 1. 정수 칸 올림으로 6→4, 7→5, 5→3, 8→6. 원래 전체 HP가 그대로 유지되며 임시 부상만 준다.",
            Policy="이전 검사와 동일한 근접 > 사격 > 접근 > 방어. 회피는 예고 칸을 읽고 안전한 이웃 칸으로 이동하거나 방어. 무시는 예고만 읽지 않으며 공격할 수 없으면 방어한다. 최초 첫 대원에게 탄약2, 이후 보충 없음.",
            Limitations="수동 플레이나 완성 지역 난이도를 대표하지 않음. 치료/식량/휴식/철수/재료 보상 없음. 이동은 단순 탐욕 정책으로 측면 공략·동료 길 비키기·탄약 분배를 최적화하지 않는다. 단일 전투가 끝나면 전투 소음/방어/상태이상은 실제 새 전투처럼 초기화되지만 HP/탄약/다운은 유지. 탐험 위험도는 모형 밖이다.",
            Rules=JsonConvert.DeserializeObject(ruleSource), Waves=Waves,
            PartyProfiles=roster.Candidates.Where(c=>c!=null).Select(c=>new { c.Id,c.Health,c.Aim,c.Traits }).ToArray(),
            CreatureProfiles=creatures.Creatures.Select(c=>new { c.Id,Role=c.RoleName,c.Health,c.Damage,c.Armor,c.FrontArmor,c.ShotArmor,c.HitChance,c.Weight,c.MinRegionTier }).ToArray()
        };
        for(int squad=0;squad<Squads.Length;squad++)
        {
            var spec=Squads[squad];
            var candidates=spec.Members.Select(id=>roster.Candidates.Single(c=>c!=null&&c.Id==id)).ToArray();
            foreach(bool dodge in new[]{true,false})
            {
                var trials=new List<Trip>();
                for(int n=0;n<seeds;n++)
                    trials.Add(Play(candidates,lineups,pool,JsonConvert.DeserializeObject<FieldBattleRules>(ruleSource),dodge,BaseSeed+squad*10000+n));
                report.Rows.Add(Summarize(spec,candidates,dodge,trials));
                report.TotalTrips+=trials.Count; report.TotalBattles+=trials.Sum(t=>t.Fights.Count);
            }
        }
        if(ruleSource!=JsonConvert.SerializeObject(panel.Rules)) throw new Exception("Shared prefab rules were modified.");
        string path=Path.GetFullPath(Output);
        if(!Directory.Exists(Path.GetDirectoryName(path))) throw new Exception("Existing 기획 directory required.");
        File.WriteAllText(path,JsonConvert.SerializeObject(report,Formatting.Indented),new UTF8Encoding(false));
        var summary=new StringBuilder(); summary.AppendLine(report.TotalTrips+" trips / "+report.TotalBattles+" battles · "+path);
        foreach(var r in report.Rows)
            summary.AppendLine(string.Format("{0}/{1}: 3연전완주 {2:P1} 패배 {3:P1} 다운 {4:P1} 교착 {5} · 피해 {6:0.00} 탄약 {7:0.00}/2 라운드 {8:0.00} 증원 {9:0.00} · 경직연속최대 {10}",r.Squad,r.Policy,r.CompletionRate,r.DefeatRate,r.DownRate,r.Stalls,r.MeanDamage,r.MeanShots,r.MeanRounds,r.MeanReinforcements,r.MaxConsecutiveRecovery));
        return summary.ToString();
    }

    static int Injured(int max) => Math.Max(1,max-(int)Math.Ceiling(max*.25));
    static Trip Play(PartyCandidate[] candidates,BattleCreature[][] waves,BattleCreature[] pool,FieldBattleRules rules,bool dodge,int seed)
    {
        var party=candidates.Select(c=>c.CreateAdventurer()).ToArray();
        foreach(var p in party) p.Health=Injured(p.MaxHealth);
        var ammo=party.Select((p,i)=>new {p,n=i==0?2:0}).ToDictionary(x=>x.p,x=>x.n);
        int initial=party.Sum(p=>p.Health); var random=new Random(seed); var trip=new Trip{Seed=seed};
        for(int wave=0;wave<waves.Length;wave++)
        {
            if(!party.Any(p=>p.Health>0)) {trip.Defeated=true;break;}
            var s=new FieldBattleState(party,waves[wave].Length,p=>ammo[p],p=>{if(ammo[p]<=0)return false;ammo[p]--;return true;},()=>random.Next(0,100),rules,waves[wave],pool);
            var f=new Fight{Wave=wave+1,HealthBefore=party.Sum(p=>Math.Max(0,p.Health)),AmmoBefore=ammo.Where(x=>x.Key.Health>0).Sum(x=>x.Value)};
            int downBefore=party.Count(p=>p.Health<=0);
            var attacks=new Dictionary<int,int>();var recoveryStreak=new Dictionary<int,int>();
            while(s.Outcome==FieldBattleOutcome.Playing&&s.Round<=RoundLimit&&f.Steps<StepLimit)
            {
                f.Steps++;
                if(s.Current.Enemy)
                {
                    int actor=s.Actor;
                    if(!s.EnemyStep()) throw new Exception("Enemy did not act, seed "+seed);
                    f.EnemyActions++;
                    bool attack=s.Events.Any(e=>(e.Kind==BattleEventKind.Attack||e.Kind==BattleEventKind.Strike)&&e.Actor==actor);
                    bool recover=s.Events.Any(e=>e.Kind==BattleEventKind.Recover&&e.Actor==actor);
                    if(attack) {attacks[actor]=(attacks.TryGetValue(actor,out int old)?old:0)+1;f.Strikes++;}
                    if(s.Events.Any(e=>e.Kind==BattleEventKind.Windup&&e.Actor==actor))f.Windups++;
                    if(recover)f.Recoveries++;
                    recoveryStreak[actor]=recover?(recoveryStreak.TryGetValue(actor,out int previous)?previous:0)+1:0;
                    f.MaxConsecutiveRecovery=Math.Max(f.MaxConsecutiveRecovery,recoveryStreak[actor]);
                    continue;
                }
                bool Marked(List<EnemyIntent> plans)=>plans.Any(p=>p.Kind==EnemyIntentKind.Strike&&p.Hits!=null&&p.Hits.Any(h=>h.Target==s.Actor));
                if(dodge&&Marked(s.PredictIntents()))
                {
                    var safe=new[]{(d:0,l:1),(d:0,l:-1),(d:-1,l:0),(d:1,l:0)}.Select(m=>(d:s.Current.Depth+m.d,l:s.Current.Lane+m.l))
                        .Where(m=>s.CanMove(m.d,m.l)&&!Marked(s.PredictIntents(s.Actor,m.d,m.l))).OrderBy(m=>m.d).ToArray();
                    if(safe.Length>0&&s.Move(safe[0].d,safe[0].l)){f.Dodges++;continue;}
                    s.Guard();f.Guards++;continue;
                }
                var targets=Enumerable.Range(0,s.Units.Count).Where(i=>s.Units[i].Enemy&&s.Units[i].Alive).ToArray();
                int melee=targets.Where(i=>s.InMeleeReach(i)&&s.ExpectedDamage(i,false)>0).OrderByDescending(i=>s.Units[i].Stagger).ThenBy(i=>s.Units[i].Health).DefaultIfEmpty(-1).First();
                if(melee>=0&&s.Attack(melee,false)){f.Melee++;if(!s.Units[melee].Alive&&!attacks.ContainsKey(melee))f.KilledBeforeFirstAttack++;continue;}
                int shot=targets.Where(i=>s.CanAttack(i,true)&&s.ExpectedDamage(i,true)>0).OrderByDescending(i=>s.Units[i].Stagger).ThenByDescending(i=>s.Units[i].Creature?.Attack==CreatureAttack.Broadcast)
                    .ThenBy(i=>s.Units[i].Depth).ThenBy(i=>s.Units[i].Health).DefaultIfEmpty(-1).First();
                if(shot>=0&&s.Attack(shot,true)){if(!s.Units[shot].Alive&&!attacks.ContainsKey(shot))f.KilledBeforeFirstAttack++;continue;}
                bool Safe(int d,int l)=>s.CanMove(d,l)&&(!dodge||!Marked(s.PredictIntents(s.Actor,d,l)));
                if(s.Current.Depth>0&&Safe(s.Current.Depth-1,s.Current.Lane)){s.Move(s.Current.Depth-1,s.Current.Lane);continue;}
                if(!s.Moved&&s.Current.Depth==0)
                {
                    int front=targets.Where(i=>s.Units[i].Depth==0).OrderBy(i=>Math.Abs(s.Units[i].Lane-s.Current.Lane)).DefaultIfEmpty(-1).First();
                    if(front>=0){int step=Math.Sign(s.Units[front].Lane-s.Current.Lane);if(step!=0&&Safe(0,s.Current.Lane+step)){s.Move(0,s.Current.Lane+step);continue;}}
                }
                s.Guard();f.Guards++;
            }
            f.Win=s.Outcome==FieldBattleOutcome.Victory;f.Lost=s.Outcome==FieldBattleOutcome.Defeat;f.Stalled=s.Outcome==FieldBattleOutcome.Playing;f.Rounds=s.Round;
            f.HealthAfter=party.Sum(p=>Math.Max(0,p.Health));f.Damage=f.HealthBefore-f.HealthAfter;f.AmmoAfter=ammo.Where(x=>x.Key.Health>0).Sum(x=>x.Value);
            f.Shots=s.AmmoSpent;f.Reinforcements=s.Reinforcements;f.NewDownMembers=party.Count(p=>p.Health<=0)-downBefore;f.EndHealth=party.Select(p=>p.Health).ToArray();
            trip.Fights.Add(f);if(f.Win)trip.Wins++;trip.Rounds+=f.Rounds;trip.Shots+=f.Shots;trip.Reinforcements+=f.Reinforcements;
            if(f.Lost||f.Stalled){trip.Defeated=f.Lost;trip.Stalled=f.Stalled;break;}
        }
        trip.Complete=trip.Wins==waves.Length;trip.DownMembers=party.Count(p=>p.Health<=0);trip.Damage=initial-party.Sum(p=>Math.Max(0,p.Health));trip.EndHealth=party.Select(p=>p.Health).ToArray();
        return trip;
    }

    static Row Summarize(Squad squad,PartyCandidate[] candidates,bool dodge,List<Trip> trials)
    {
        int n=trials.Count;var rounds=trials.Select(t=>t.Rounds).OrderBy(x=>x).ToArray();var fights=trials.SelectMany(t=>t.Fights).ToArray();
        var row=new Row
        {
            Squad=squad.Id,Policy=dodge?"read-and-dodge":"ignore-warnings",Trips=n,Completed=trials.Count(t=>t.Complete),Defeated=trials.Count(t=>t.Defeated),Stalls=trials.Count(t=>t.Stalled),TripsWithDown=trials.Count(t=>t.DownMembers>0),
            MeanWins=trials.Average(t=>t.Wins),MeanDamage=trials.Average(t=>t.Damage),MeanShots=trials.Average(t=>t.Shots),MeanRounds=trials.Average(t=>t.Rounds),MeanReinforcements=trials.Average(t=>t.Reinforcements),P95Rounds=rounds[Math.Max(0,(int)Math.Ceiling(n*.95)-1)],
            MeanGuards=trials.Average(t=>t.Fights.Sum(f=>f.Guards)),MeanDodges=trials.Average(t=>t.Fights.Sum(f=>f.Dodges)),MeanEnemyWindups=trials.Average(t=>t.Fights.Sum(f=>f.Windups)),MeanEnemyStrikes=trials.Average(t=>t.Fights.Sum(f=>f.Strikes)),MeanEnemyRecoveries=trials.Average(t=>t.Fights.Sum(f=>f.Recoveries)),MeanKilledBeforeFirstAttack=trials.Average(t=>t.Fights.Sum(f=>f.KilledBeforeFirstAttack)),MaxConsecutiveRecovery=fights.Max(f=>f.MaxConsecutiveRecovery),
            Inputs=new {Members=squad.Members,MaxHealth=candidates.Select(c=>c.Health).ToArray(),StartingHealth=candidates.Select(c=>Injured(c.Health)).ToArray(),TotalAmmo=2,AmmoCarrier=squad.Members[0],RegionTier=2,RestBetweenFights=false,HealingItems=0}
        };
        row.CompletionRate=(double)row.Completed/n;row.DefeatRate=(double)row.Defeated/n;row.DownRate=(double)row.TripsWithDown/n;
        row.ByWave=Enumerable.Range(1,Waves.Length).Select(w=>
        {
            var f=fights.Where(x=>x.Wave==w).ToArray();int count=f.Length;
            return (object)new {Wave=w,Reached=count,Wins=f.Count(x=>x.Win),Losses=f.Count(x=>x.Lost),Stalls=f.Count(x=>x.Stalled),ConditionalWinRate=count>0?(double)f.Count(x=>x.Win)/count:0,
                AmmoEmptyAtEntry=f.Count(x=>x.AmmoBefore==0),NewDownRuns=f.Count(x=>x.NewDownMembers>0),MeanEntryHp=count>0?f.Average(x=>x.HealthBefore):0,MeanExitHp=count>0?f.Average(x=>x.HealthAfter):0,
                MeanDamage=count>0?f.Average(x=>x.Damage):0,MeanShots=count>0?f.Average(x=>x.Shots):0,MeanRounds=count>0?f.Average(x=>x.Rounds):0,MeanReinforcements=count>0?f.Average(x=>x.Reinforcements):0,
                MeanEntryAmmo=count>0?f.Average(x=>x.AmmoBefore):0,MeanExitAmmo=count>0?f.Average(x=>x.AmmoAfter):0};
        }).ToArray();
        row.Reproduction.AddRange(trials.Where(t=>t.Stalled).Take(4));
        row.Reproduction.AddRange(trials.Where(t=>t.Defeated&&!row.Reproduction.Contains(t)).Take(4));
        row.Reproduction.AddRange(trials.Where(t=>t.DownMembers>0&&!row.Reproduction.Contains(t)).OrderByDescending(t=>t.Damage).Take(2));
        if(row.Stalls>0)row.Findings.Add("교착/상한초과 seed를 따로 확인. 단순 정책의 통로 막힘을 전투 엔진 오류로 단정하지 않는다.");
        if(row.MaxConsecutiveRecovery>1)row.Findings.Add("같은 개체 연속 회복차례가 2 이상: 경직 루프 재현 확인 필요.");
        if(dodge&&row.CompletionRate<.7)row.Findings.Add("회피 정책의 무보급 3연전 완주가 70% 미만. 두 명 편성/초기 부상/치료부재를 함께 해석.");
        if(dodge&&row.DownRate<.05&&row.CompletionRate>.99)row.Findings.Add("제한 탄약/부상/3연전에도 위험 낮음. 자원/행동경제나 후속 적 조합 검토 근거.");
        return row;
    }
}
