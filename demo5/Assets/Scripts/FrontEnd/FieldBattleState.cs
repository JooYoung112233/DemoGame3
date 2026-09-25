using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;

namespace Demo5.FrontEnd
{
    public enum FieldBattleOutcome { Playing, Victory, Retreated, Defeat }
    public enum BattleEventKind { Move, Attack, Guard, Advance, Shift, Wait, Alarm, Reinforce, Item, Windup, Strike, Recover }
    public enum EnemyIntentKind { None, Attack, Advance, Shift, Wait, Windup, Strike, Recover }

    // Provisional combat numbers. Edit them on ExpeditionBattlePanel > Rules in the Inspector.
    // The 감염자 block is the fallback type (no creature data). Creature types live in Assets/Data/BattleCreatures.asset.
    [Serializable] public sealed class FieldBattleRules
    {
        [Header("감염자")]
        [Min(1)] public int EnemyHealth = 4;
        [Tooltip("처음 감염자 / 나머지 / 증원이 등장하는 줄 (0 전열 ~ 2 후열)")]
        [Range(0, 2)] public int FirstEnemyDepth = 1, OtherEnemyDepth = 2, ReinforcementDepth = 2;
        [Tooltip("대원 공격의 피해를 이만큼 줄인다. 조준 정보에 방어력으로 표시한다.")]
        [Min(0)] public int EnemyArmor = 0;
        [Range(0, 100)] public int EnemyHitChance = 70;
        [Min(0)] public int EnemyDamage = 1;
        [Tooltip("전열에서 한 줄 뒤의 대원을 물 때마다 떨어지는 명중률")]
        [Range(0, 50)] public int EnemyDepthPenalty = 10;
        [Header("근접 · 전열끼리")]
        [Min(0)] public int MeleeDamage = 2;
        [Range(0, 100)] public int MeleeHitChance = 85;
        [Tooltip("근접이 닿는 좌우 줄 차이. 감염자의 물기에도 같은 값을 쓴다.")]
        [Range(0, 2)] public int LaneReach = 1;
        [Header("사격")]
        [Min(0)] public int ShotDamage = 3;
        [Range(0, 100)] public int ShotHitChance = 75;
        [Range(0, 30)] public int ShotRowPenalty = 5, ShotLanePenalty = 5;
        [Min(0)] public int ShotNoise = 2;
        [Header("급소 · 대원 공격만")]
        [Range(0, 100)] public int CriticalChance = 10;
        [Min(0)] public int CriticalBonus = 1;
        [Tooltip("근접 급소가 살아남은 감염자를 한 줄 밀어낸다")]
        public bool CriticalMeleePush = true;
        [Header("방어 · 다음 자기 차례까지")]
        [Tooltip("방어 중인 대원을 무는 확률에서 빼는 값")]
        [Range(0, 100)] public int GuardHitPenalty = 40;
        [Tooltip("방어 중 받는 피해 감소. 물기 피해 이상이면 완전히 막는다.")]
        [Min(0)] public int GuardReduction = 0;
        [Header("소음과 증원")]
        [Tooltip("전투 소음이 이 값 × (증원 수 + 1)에 닿으면 다음 라운드에 증원. 사격 소음 2 기준: 두 명이 1발씩(소음 4)은 증원 없음, 3발째(소음 6)에 증원")]
        [Min(1)] public int ReinforcementNoise = 5;
        [Min(0)] public int MaxReinforcements = 1;
        [Range(1, 9)] public int MaxEnemies = 3;
        [Tooltip("승리 후 탐험 소음 = 이 값 + 전투 소음 × 전달 비율")]
        [Min(0)] public int VictoryNoise = 1;
        [Tooltip("전투 중 소음(총성) 가운데 탐험 소음으로 이어지는 비율")]
        [Range(0, 100)] public int CarriedNoisePercent = 50;
        [Header("철수 · 등 뒤 공격은 체력 1을 남긴다")]
        [Range(0, 100)] public int RetreatHitChance = 50;
        [Header("확률 범위")]
        [Range(0, 100)] public int MinimumChance = 20;
        [Range(0, 100)] public int MaximumChance = 95;
        [Header("크리쳐 공통 · 예고 공격과 빈틈")]
        [Tooltip("예고 공격을 방어 자세로 받으면 줄어드는 피해. 방어 중이면 밀려나지도 않는다.")]
        [Min(0)] public int GuardStrikeReduction = 1;
        [Tooltip("밀려난 대원이 벽이나 동료에 부딪히면 더 받는 피해")]
        [Min(0)] public int CollisionDamage = 1;
        [Tooltip("공격 뒤 빈틈이 생긴 크리쳐를 칠 때 명중률 보너스. 빈틈 동안 방어력 0")]
        [Range(0, 60)] public int StaggerHitBonus = 20;
        [Tooltip("시야가 가려진 대원의 다음 공격 명중률 감소")]
        [Range(0, 60)] public int VeilPenalty = 30;
        [Tooltip("문짝 뒤에 가려진 적을 쏠 때 명중률 감소")]
        [Range(0, 60)] public int CoverPenalty = 30;
        [Tooltip("수신목 방송 한 번의 소음")]
        [Min(0)] public int BroadcastNoise = 3;
    }

    // One creature hit on one ally, with the board change it caused.
    public sealed class BattleHit
    {
        public int Target = -1, Chance = 100, Damage, Health, FromDepth, FromLane, ToDepth, ToLane;
        public bool Hit = true, Killed, Braced, Pushed, Collided, Bound, Veiled;
    }

    // Already applied to the state; the presentation replays them in order.
    public sealed class BattleEvent
    {
        public BattleEventKind Kind;
        public int Actor = -1, Target = -1, Chance, Damage, Health;
        public bool Ranged, Hit, Critical, Blocked, Killed, Pushed, Retreating;
        public int FromDepth, FromLane, ToDepth, ToLane;
        // Creature actions: the attack kind, the ally cells it covers, one entry per ally it reached.
        public CreatureAttack Attack;
        public List<int> Cells = new List<int>();
        public List<BattleHit> Hits = new List<BattleHit>();
        public int Side, Noise;
        public bool Whiff, Staggered, Resting;
    }

    public struct EnemyIntent
    {
        public EnemyIntentKind Kind;
        public int Enemy, Target, Chance, Depth, Lane, Damage, Side;
        // Fast creatures pass through this cell before Depth/Lane (-1 when they take a single step).
        public int ViaDepth, ViaLane;
        public CreatureAttack Attack;
        // Ally cells (lane*3+depth) the action covers: committed for Strike, planned for Windup.
        public List<int> Cells;
        // Who it would reach on the current (or previewed) board, with damage. Attack lists the rolled targets.
        public List<BattleHit> Hits;
        public bool Resting;
        public int HitCount => Hits == null ? 0 : Hits.Count;
    }

    // A committed strike: marked on one enemy turn, resolved on the next.
    public sealed class PendingStrike
    {
        public CreatureAttack Attack;
        public int Lane = -1, Side;
        public List<int> Cells = new List<int>();
    }

    // Battle time is separate from exploration time: one resolved battle costs one exploration turn.
    public sealed partial class FieldBattleState
    {
        public sealed class Unit
        {
            public Adventurer Person;
            public BattleCreature Creature;
            public string Name;
            public int Health, Maximum, Depth, Lane;
            public bool Enemy, Guarding, Reinforcement;
            // Creature: committed strike, turns of opening (skips its turn, armor 0), resting after a slow step.
            public PendingStrike Pending;
            public int Stagger;
            // Tired: a slow creature rests after a step. Recharging: after a discharge the meter steps closer before wiring again.
            public bool Tired, Recharging;
            // Ally: cannot move / aims worse on its next turn; order of the last shot this round (0 = quiet).
            public int Bound, Veiled, Loud;
            public bool Alive => Health > 0;
        }

        public readonly List<Unit> Units = new List<Unit>();
        public readonly List<BattleEvent> Events = new List<BattleEvent>();
        public readonly FieldBattleRules Rules;
        public FieldBattleOutcome Outcome { get; private set; }
        public int Actor { get; private set; }
        public int Round { get; private set; } = 1;
        public bool Moved { get; private set; }
        public int AmmoSpent { get; private set; }
        public int Actions { get; private set; }
        public int Kills { get; private set; }
        public int ItemsUsed { get; private set; }
        public int Noise { get; private set; }
        public int Reinforcements { get; private set; }
        public bool ReinforcementPending { get; private set; }
        public string Message { get; private set; } = "빈 칸으로 이동하거나 행동을 선택하세요.";
        public Unit Current => Units[Actor];
        public int NextReinforcementNoise => Rules.ReinforcementNoise * (Reinforcements + 1);
        public bool ReinforcementsLeft => Reinforcements < Rules.MaxReinforcements;
        public int ResultNoise => (Noise * Rules.CarriedNoisePercent + 50) / 100 + (Outcome == FieldBattleOutcome.Victory ? Rules.VictoryNoise : 0);
        readonly Func<int> roll;
        readonly Func<Adventurer, int> ammo;
        readonly Func<Adventurer, bool> spendAmmo;
        readonly IList<BattleCreature> pool;
        int spawned, loudness;
        // Ally HP as the enemy phase began. Targeting reads this so an earlier bite in the same phase
        // cannot silently retarget a later infected away from what the forecast showed.
        int[] phaseHealth;

        // Positions and health the planner reasons about: the live board, or a copy with a previewed move or earlier strikes applied.
        sealed class Board
        {
            public int[] D, L, H;
            public static Board From(List<Unit> units) => new Board { D = units.Select(u => u.Depth).ToArray(), L = units.Select(u => u.Lane).ToArray(), H = units.Select(u => u.Health).ToArray() };
        }

        // lineup: creature types to spawn (null = the rules' 감염자). pool: reinforcement types (null = 감염자).
        public FieldBattleState(IEnumerable<Adventurer> party, int enemies,
            Func<Adventurer, int> ammoCount, Func<Adventurer, bool> consumeAmmo, Func<int> random, FieldBattleRules rules = null,
            IList<BattleCreature> lineup = null, IList<BattleCreature> reinforcementPool = null)
        {
            ammo = ammoCount; spendAmmo = consumeAmmo; roll = random; Rules = rules ?? new FieldBattleRules(); pool = reinforcementPool;
            int i = 0;
            foreach (var p in party.Where(p => p.Health > 0))
            {
                if (i >= 9) throw new ArgumentException("The initial formation holds nine units.");
                Units.Add(new Unit { Person = p, Name = p.Name, Health = p.Health, Maximum = p.MaxHealth, Depth = i / 3, Lane = (i * 2) % 3 });
                i++;
            }
            if (i == 0) throw new ArgumentException("A living party is required.");
            // The first creature holds the middle row, the rest the back row, unless a type prefers its own row.
            int count = Math.Max(1, Math.Min(Math.Min(3, Rules.MaxEnemies), lineup != null && lineup.Count > 0 ? lineup.Count : enemies));
            for (int e = 0; e < count; e++)
            {
                var creature = lineup != null && e < lineup.Count ? lineup[e] : null;
                var unit = Spawn(e == 0 ? Rules.FirstEnemyDepth : Rules.OtherEnemyDepth, false, creature);
                if (unit != null) Units.Add(unit);
            }
        }

        public bool PlayerTurn => Outcome == FieldBattleOutcome.Playing && !Current.Enemy;
        public int DamageFor(bool ranged) => ranged ? Rules.ShotDamage : Rules.MeleeDamage;
        public static int CellOf(int depth, int lane) => lane * 3 + depth;
        public int DamageOf(int enemy) => enemy >= 0 && enemy < Units.Count && Units[enemy].Creature != null ? Units[enemy].Creature.Damage : Rules.EnemyDamage;
        // A listener with nobody to hear only gropes: its long-arm damage is for the slam.
        public int BiteDamage(int enemy) => enemy >= 0 && enemy < Units.Count && Units[enemy].Creature?.Attack == CreatureAttack.Listen ? Rules.EnemyDamage : DamageOf(enemy);
        // Armor against the current ally's attack: base, a door facing the same lane, extra against shots; none during an opening.
        public int ArmorOf(int unit, bool ranged = false)
        {
            if (unit < 0 || unit >= Units.Count || !Units[unit].Enemy) return 0;
            var u = Units[unit]; var c = u.Creature;
            if (c == null) return Rules.EnemyArmor;
            if (u.Stagger > 0) return 0;
            int armor = c.Armor + (ranged ? c.ShotArmor : 0);
            if (c.FrontArmor > 0 && PlayerTurn && Current.Lane == u.Lane) armor += c.FrontArmor;
            return armor;
        }
        // Damage an ally attack deals to this target after its armor.
        public int ExpectedDamage(int target, bool ranged, bool critical = false) => Math.Max(0, DamageFor(ranged) + HumanDamageBonus(target, ranged) + (critical ? Rules.CriticalBonus : 0) - ArmorOf(target, ranged));
        // The terms behind HitChance, for the aim tooltip. Values are percentage points.
        public List<KeyValuePair<string, int>> HitFactors(int target, bool ranged)
        {
            var list = new List<KeyValuePair<string, int>>();
            if (!PlayerTurn || target < 0 || target >= Units.Count || !Units[target].Enemy) return list;
            var t = Units[target];
            if (!ranged) { list.Add(new KeyValuePair<string, int>("근접", Rules.MeleeHitChance)); AddModifiers(list, target, false); return Clamped(list, target, false); }
            int aim = Current.Person?.Aim ?? 0;
            list.Add(new KeyValuePair<string, int>("기본", Rules.ShotHitChance));
            if (aim != 0) list.Add(new KeyValuePair<string, int>("조준", aim));
            if (Current.Depth > 0) list.Add(new KeyValuePair<string, int>("내 줄", -Current.Depth * Rules.ShotRowPenalty));
            if (t.Depth > 0) list.Add(new KeyValuePair<string, int>("대상 줄", -t.Depth * Rules.ShotRowPenalty));
            int lanes = Math.Abs(Current.Lane - t.Lane); if (lanes > 0) list.Add(new KeyValuePair<string, int>("가로", -lanes * Rules.ShotLanePenalty));
            AddModifiers(list, target, true);
            return Clamped(list, target, true);
        }
        void AddModifiers(List<KeyValuePair<string, int>> list, int target, bool ranged)
        {
            var t = Units[target];
            if (MovedHitBonus > 0) list.Add(new KeyValuePair<string, int>("발 빠른 대응", MovedHitBonus));
            if (t.Stagger > 0) list.Add(new KeyValuePair<string, int>("빈틈", Rules.StaggerHitBonus));
            if (Current.Veiled > 0) list.Add(new KeyValuePair<string, int>("시야 가림", -Rules.VeilPenalty));
            if (ranged && t.Creature != null && t.Creature.ShotEvasion > 0) list.Add(new KeyValuePair<string, int>("흩어짐", -t.Creature.ShotEvasion));
            if (ranged && Covered(target)) list.Add(new KeyValuePair<string, int>("엄폐", -Rules.CoverPenalty));
        }
        // The listed terms must add up to the shown chance: add the floor/ceiling adjustment when it applies.
        List<KeyValuePair<string, int>> Clamped(List<KeyValuePair<string, int>> list, int target, bool ranged)
        {
            int sum = list.Sum(f => f.Value), shown = HitChance(target, ranged);
            if (shown > 0 && shown != sum) list.Add(new KeyValuePair<string, int>(shown > sum ? "하한" : "상한", shown - sum));
            return list;
        }
        int Modifiers(int target, bool ranged)
        {
            var t = Units[target]; int m = MovedHitBonus;
            if (t.Stagger > 0) m += Rules.StaggerHitBonus;
            if (Current.Veiled > 0) m -= Rules.VeilPenalty;
            if (ranged && t.Creature != null) m -= t.Creature.ShotEvasion;
            if (ranged && Covered(target)) m -= Rules.CoverPenalty;
            return m;
        }
        // A creature with Cover (the door) shields whoever stands behind it in its lane from shots.
        public bool Covered(int target)
        {
            var t = Units[target];
            return Units.Any(k => k != t && k.Enemy && k.Alive && k.Creature != null && k.Creature.Cover && k.Lane == t.Lane && k.Depth < t.Depth);
        }
        int Clamp(int chance) => Math.Max(Rules.MinimumChance, Math.Min(Rules.MaximumChance, chance));
        int BaseBite(int enemy) => enemy >= 0 && enemy < Units.Count && Units[enemy].Creature != null ? Units[enemy].Creature.HitChance : Rules.EnemyHitChance;
        public int BiteChance(int targetDepth, bool guarding = false, int enemy = -1) => Clamp(BaseBite(enemy) - targetDepth * Rules.EnemyDepthPenalty - (guarding ? Rules.GuardHitPenalty : 0));

        public bool CanMove(int depth, int lane) => PlayerTurn && !Moved && Current.Bound == 0 && InBoard(depth, lane)
            && Math.Abs(Current.Depth - depth) + Math.Abs(Current.Lane - lane) == 1 && Free(false, depth, lane);
        public bool Move(int depth, int lane)
        {
            if (!CanMove(depth, lane)) return false;
            Events.Clear();
            Events.Add(new BattleEvent { Kind = BattleEventKind.Move, Actor = Actor, FromDepth = Current.Depth, FromLane = Current.Lane, ToDepth = depth, ToLane = lane });
            Current.Depth = depth; Current.Lane = lane; Moved = true;
            Message = Current.Name + " · 위치 변경 / 행동 1회 남음";
            return true;
        }

        public bool InMeleeReach(int target)
        {
            if (!PlayerTurn || target < 0 || target >= Units.Count) return false;
            var t = Units[target];
            return t.Enemy && t.Alive && Current.Depth == 0 && t.Depth == 0 && Math.Abs(Current.Lane - t.Lane) <= Rules.LaneReach;
        }
        public int HitChance(int target, bool ranged)
        {
            if (!PlayerTurn || target < 0 || target >= Units.Count || !Units[target].Enemy || !Units[target].Alive) return 0;
            if (!ranged) return InMeleeReach(target) ? Clamp(Rules.MeleeHitChance + Modifiers(target, false)) : 0;
            var t = Units[target];
            return Clamp(Rules.ShotHitChance + (Current.Person?.Aim ?? 0) - (Current.Depth + t.Depth) * Rules.ShotRowPenalty
                - Math.Abs(Current.Lane - t.Lane) * Rules.ShotLanePenalty + Modifiers(target, true));
        }
        public bool CanAttack(int target, bool ranged) => HitChance(target, ranged) > 0 && (!ranged || ammo(Current.Person) > 0);
        public bool Attack(int target, bool ranged)
        {
            if (!CanAttack(target, ranged)) return false;
            int chance = HitChance(target, ranged), armorDamage = ExpectedDamage(target, ranged), criticalDamage = ExpectedDamage(target, ranged, true);
            if (ranged && !spendAmmo(Current.Person)) return false;
            Events.Clear();
            if (ranged) { AmmoSpent++; Current.Loud = ++loudness; }
            var victim = Units[target];
            bool hit = roll() < chance, critical = hit && roll() < Rules.CriticalChance;
            int damage = hit ? (critical ? criticalDamage : armorDamage) : 0;
            var ev = new BattleEvent { Kind = BattleEventKind.Attack, Actor = Actor, Target = target, Ranged = ranged, Chance = chance, Hit = hit, Critical = critical, Damage = damage,
                FromDepth = victim.Depth, FromLane = victim.Lane, ToDepth = victim.Depth, ToLane = victim.Lane };
            if (hit) Damage(victim, damage);
            ev.Killed = !victim.Alive; ev.Health = victim.Health;
            if (ev.Killed) { Kills++; victim.Pending = null; victim.Stagger = 0; }
            if (critical && !ranged && Rules.CriticalMeleePush && victim.Alive && Free(true, victim.Depth + 1, victim.Lane))
            {
                victim.Depth++; ev.Pushed = true; ev.ToDepth = victim.Depth;
            }
            Events.Add(ev);
            Message = Current.Name + " → " + victim.Name + " · " + chance + "% · "
                + (!hit ? "빗나감" : (critical ? "급소 " : "명중 ") + damage + (ev.Killed ? " · 쓰러뜨림" : ev.Pushed ? " · 밀려남" : ""));
            if (ranged) AddNoise(Rules.ShotNoise);
            Advance(); return true;
        }
        public bool Guard()
        {
            if (!PlayerTurn) return false;
            Events.Clear();
            Current.Guarding = true; Message = Current.Name + " · 방어 자세 · 물릴 확률 -" + Rules.GuardHitPenalty + "%p · 예고 공격 피해 -" + GuardReductionFor(Actor, true);
            Events.Add(new BattleEvent { Kind = BattleEventKind.Guard, Actor = Actor });
            Advance(); return true;
        }

        // Battle items: the actor spends its action to treat a living, wounded ally (or itself).
        public bool CanTreat(int target) => PlayerTurn && target >= 0 && target < Units.Count && !Units[target].Enemy && Units[target].Alive && Units[target].Health < Units[target].Maximum;
        public bool UseItem(int target, int heal, Func<bool> consume, string itemId = null)
        {
            if (heal <= 0 || !CanTreat(target) || consume == null || !consume()) return false;
            Events.Clear();
            var t = Units[target]; int before = t.Health;
            t.Health = Math.Min(t.Maximum, t.Health + ItemRecovery(heal, itemId)); if (t.Person != null) t.Person.Health = t.Health;
            Events.Add(new BattleEvent { Kind = BattleEventKind.Item, Actor = Actor, Target = target, Damage = t.Health - before, Health = t.Health });
            ItemsUsed++;
            Message = Current.Name + (target == Actor ? "" : " → " + t.Name) + " · 치료 +" + (t.Health - before);
            Advance(); return true;
        }

        // Parting blows while backing away: anyone about to bite or strike someone swings once. They never knock anyone out.
        public List<EnemyIntent> RetreatThreats()
        {
            var result = new List<EnemyIntent>();
            if (Outcome != FieldBattleOutcome.Playing) return result;
            for (int i = 0; i < Units.Count; i++)
            {
                if (!Units[i].Enemy || !Units[i].Alive) continue;
                var plan = Plan(i, Board.From(Units), Healths());
                int target = plan.Kind == EnemyIntentKind.Attack ? plan.Target : plan.Kind == EnemyIntentKind.Strike && plan.HitCount > 0 ? plan.Hits[0].Target : -1;
                if (target >= 0) result.Add(new EnemyIntent { Kind = EnemyIntentKind.Attack, Enemy = i, Target = target, Chance = Rules.RetreatHitChance, Damage = Math.Min(AfterGuardDamage(target, Rules.EnemyDamage, false), Math.Max(0, Units[target].Health - 1)), Attack = plan.Attack });
            }
            return result;
        }
        public bool Retreat()
        {
            if (!PlayerTurn) return false;
            Events.Clear();
            foreach (var threat in RetreatThreats()) Bite(threat.Enemy, threat.Target, threat.Chance, true);
            // The rules do not know the room: the arcade retreat leaves through the exit, the corridor one falls back to the arcade.
            Outcome = FieldBattleOutcome.Retreated; Message = "원정대가 물러납니다."; return true;
        }

        public bool EnemyStep()
        {
            if (Outcome != FieldBattleOutcome.Playing || !Current.Enemy) return false;
            Events.Clear();
            var me = Current; int e = Actor; var c = me.Creature;
            var plan = Plan(e, Board.From(Units), TargetHealth());
            switch (plan.Kind)
            {
                case EnemyIntentKind.Recover:
                    me.Stagger--; Events.Add(new BattleEvent { Kind = BattleEventKind.Recover, Actor = e, Attack = plan.Attack });
                    Message = me.Name + " · 자세를 추스릅니다"; break;
                case EnemyIntentKind.Strike: Strike(e); break;
                case EnemyIntentKind.Windup:
                    me.Pending = new PendingStrike { Attack = plan.Attack, Lane = plan.Lane, Side = plan.Side, Cells = plan.Cells.ToList() }; me.Tired = false;
                    Events.Add(new BattleEvent { Kind = BattleEventKind.Windup, Actor = e, Attack = plan.Attack, Cells = plan.Cells.ToList(), Side = plan.Side, ToLane = plan.Lane });
                    Message = me.Name + " · " + (c?.WindupName ?? "준비") + " · 다음 차례에 " + (c?.AttackName ?? "공격");
                    break;
                case EnemyIntentKind.Attack:
                    me.Tired = false;
                    if (plan.Attack == CreatureAttack.Swarm) Swarm(e, plan); else Bite(e, plan.Target, plan.Chance, false);
                    break;
                case EnemyIntentKind.Advance:
                case EnemyIntentKind.Shift:
                    if (plan.ViaDepth >= 0) Step(me, e, plan.ViaDepth, plan.ViaLane);
                    Step(me, e, plan.Depth, plan.Lane);
                    me.Tired = c != null && c.Gait == CreatureGait.Slow; me.Recharging = false;
                    Message = me.Name + (plan.Kind == EnemyIntentKind.Advance ? " · 다가옵니다" : " · 옆 줄로 파고듭니다");
                    break;
                default:
                    Events.Add(new BattleEvent { Kind = BattleEventKind.Wait, Actor = e, Resting = plan.Resting });
                    Message = me.Name + (plan.Resting ? " · 숨을 고릅니다" : " · 틈을 노립니다");
                    if (plan.Resting) me.Tired = false; else me.Recharging = false;
                    break;
            }
            Advance(); return true;
        }
        void Step(Unit me, int e, int depth, int lane)
        {
            Events.Add(new BattleEvent { Kind = depth < me.Depth ? BattleEventKind.Advance : BattleEventKind.Shift, Actor = e,
                FromDepth = me.Depth, FromLane = me.Lane, ToDepth = depth, ToLane = lane });
            me.Depth = depth; me.Lane = lane;
        }

        // Live forecast of the enemy phase for the current board. Optionally pretends one ally stands elsewhere.
        // Earlier creatures' moves, pushes and knockouts carry into later forecasts, as they will when the phase plays.
        public List<EnemyIntent> PredictIntents(int movedUnit = -1, int depthOverride = -1, int laneOverride = -1)
        {
            var result = new List<EnemyIntent>();
            if (Outcome != FieldBattleOutcome.Playing) return result;
            var board = Board.From(Units);
            if (movedUnit >= 0 && movedUnit < Units.Count) { board.D[movedUnit] = depthOverride; board.L[movedUnit] = laneOverride; }
            var health = TargetHealth();
            // Allies are listed first, so enemies still to act this round follow in list order.
            for (int i = Current.Enemy ? Actor : 0; i < Units.Count; i++)
            {
                if (!Units[i].Enemy || !Units[i].Alive) continue;
                var plan = Plan(i, board, health); result.Add(plan);
                if (plan.Kind == EnemyIntentKind.Advance || plan.Kind == EnemyIntentKind.Shift) { board.D[i] = plan.Depth; board.L[i] = plan.Lane; }
            }
            return result;
        }
        // Who a creature's marked strike reaches on the board as it stands now (empty when it has none).
        public List<BattleHit> PendingHits(int e) => e >= 0 && e < Units.Count && Units[e].Enemy && Units[e].Alive && Units[e].Pending != null ? Resolve(e, Units[e].Pending, Board.From(Units)) : new List<BattleHit>();
        // Every ally cell a committed strike will land on (the marked danger cells).
        public IEnumerable<int> DangerCells() => Units.Where(u => u.Enemy && u.Alive && u.Pending != null).SelectMany(u => u.Pending.Cells).Distinct();

        // ---- planning ----
        EnemyIntent Plan(int e, Board b, int[] health)
        {
            var me = Units[e]; var c = me.Creature; var attack = c?.Attack ?? CreatureAttack.Bite;
            var intent = new EnemyIntent { Kind = EnemyIntentKind.Wait, Enemy = e, Target = -1, Depth = b.D[e], Lane = b.L[e], ViaDepth = -1, ViaLane = -1, Attack = attack, Damage = DamageOf(e) };
            if (me.Stagger > 0) { intent.Kind = EnemyIntentKind.Recover; return intent; }
            if (me.Pending != null)
            {
                intent.Kind = EnemyIntentKind.Strike; intent.Attack = me.Pending.Attack; intent.Cells = me.Pending.Cells; intent.Side = me.Pending.Side; intent.Lane = me.Pending.Lane;
                intent.Hits = Resolve(e, me.Pending, b);
                if (intent.Hits.Count > 0) { intent.Target = intent.Hits[0].Target; intent.Chance = 100; intent.Damage = intent.Hits[0].Damage; }
                intent.Depth = b.D[e]; intent.Lane = me.Pending.Lane;
                return intent;
            }
            int bite = BiteTarget(e, b, health, out int goal);
            switch (attack)
            {
                case CreatureAttack.Bite:
                    if (bite >= 0) return Bite(intent, e, bite, b);
                    return Walk(intent, e, b, goal, false);
                case CreatureAttack.Swarm:
                    if (bite >= 0)
                    {
                        intent = Bite(intent, e, bite, b);
                        foreach (int side in new[] { -1, 1 })
                        {
                            int n = At(b.D[bite], b.L[bite] + side, b);
                            if (n >= 0) intent.Hits.Add(new BattleHit { Target = n, Chance = BiteChance(b.D[n], Units[n].Guarding, e), Damage = AfterGuardDamage(n, DamageOf(e), false), FromDepth = b.D[n], FromLane = b.L[n], ToDepth = b.D[n], ToLane = b.L[n] });
                        }
                        return intent;
                    }
                    return Walk(intent, e, b, goal, false);
                case CreatureAttack.Listen:
                {
                    // Whoever shot this round gives itself away; a quiet party only gets groped at like a bite.
                    int loud = -1;
                    for (int i = 0; i < Units.Count; i++) if (!Units[i].Enemy && Alive(i, b) && Units[i].Loud > 0 && (loud < 0 || Units[i].Loud > Units[loud].Loud)) loud = i;
                    if (loud >= 0) return Windup(intent, CellOf(b.D[loud], b.L[loud]));
                    if (bite >= 0) return Bite(intent, e, bite, b);
                    return Walk(intent, e, b, goal, false);
                }
                case CreatureAttack.Pounce:
                {
                    int best = -1, bestKey = int.MaxValue;
                    for (int i = 0; i < Units.Count; i++)
                    {
                        if (Units[i].Enemy || !Alive(i, b)) continue;
                        int diff = Math.Abs(b.L[i] - b.L[e]);
                        if (b.D[e] + b.D[i] > 2 || diff > Rules.LaneReach) continue;
                        int key = diff * 1000 + b.D[i] * 100 + Math.Min(9, health[i]) * 10 + Math.Min(9, i);
                        if (key < bestKey) { bestKey = key; best = i; }
                    }
                    if (best >= 0) return Windup(intent, CellOf(b.D[best], b.L[best]));
                    return Walk(intent, e, b, goal, false);
                }
                case CreatureAttack.Grab:
                    if (bite >= 0) return Windup(intent, CellOf(b.D[bite], b.L[bite]));
                    return Walk(intent, e, b, goal, false);
                case CreatureAttack.Twin:
                    if (bite >= 0) return Windup(intent, CellOf(b.D[bite], b.L[bite]), TwinSecond(bite, b));
                    return Walk(intent, e, b, goal, false);
                case CreatureAttack.Shove:
                    if (b.D[e] <= (c?.HoldDepth ?? 0) && FrontMost(b.L[e], b) >= 0) { intent.Lane = b.L[e]; return Windup(intent, LaneCells(b.L[e])); }
                    return Walk(intent, e, b, goal, false);
                case CreatureAttack.Charge:
                case CreatureAttack.Ram:
                    if (FrontMost(b.L[e], b) >= 0) { intent.Lane = b.L[e]; return Windup(intent, LaneCells(b.L[e])); }
                    return Walk(intent, e, b, goal, true);
                case CreatureAttack.Veil:
                    if (b.D[e] <= (c?.HoldDepth ?? 0))
                    {
                        int lane = -1, most = 0;
                        foreach (int l in new[] { b.L[e], b.L[e] - 1, b.L[e] + 1 })
                        {
                            if (l < 0 || l > 2) continue;
                            int n = Enumerable.Range(0, Units.Count).Count(i => !Units[i].Enemy && Alive(i, b) && b.L[i] == l);
                            if (n > most) { most = n; lane = l; }
                        }
                        if (lane >= 0) { intent.Lane = lane; return Windup(intent, LaneCells(lane)); }
                    }
                    return Walk(intent, e, b, goal, false);
                case CreatureAttack.Wire:
                    if (!me.Recharging && b.D[e] <= 1)
                    {
                        var pair = WirePair(e, b, health);
                        if (pair != null) return Windup(intent, pair.ToArray());
                    }
                    return Walk(intent, e, b, goal, false);
                case CreatureAttack.Collapse:
                    if (b.D[e] == 0)
                    {
                        // Topple toward the side that catches more people; ties fall toward the middle lane.
                        int pick = 0, most = 0, l = b.L[e], inward = l == 2 ? -1 : 1;
                        foreach (int side in new[] { inward, -inward })
                        {
                            if (l + side < 0 || l + side > 2) continue;
                            int n = (At(0, l, b) >= 0 ? 1 : 0) + (At(0, l + side, b) >= 0 ? 1 : 0);
                            if (n > most) { most = n; pick = side; }
                        }
                        if (most > 0) { intent.Side = pick; return Windup(intent, CellOf(0, l), CellOf(0, l + pick)); }
                    }
                    return Walk(intent, e, b, goal, false);
                case CreatureAttack.Broadcast:
                    return Windup(intent);
            }
            return intent;
        }
        EnemyIntent Bite(EnemyIntent intent, int e, int target, Board b)
        {
            intent.Kind = EnemyIntentKind.Attack; intent.Target = target; intent.Chance = BiteChance(b.D[target], Units[target].Guarding, e);
            intent.Damage = AfterGuardDamage(target, BiteDamage(e), false);
            intent.Hits = new List<BattleHit> { new BattleHit { Target = target, Chance = intent.Chance, Damage = intent.Damage, FromDepth = b.D[target], FromLane = b.L[target], ToDepth = b.D[target], ToLane = b.L[target] } };
            return intent;
        }
        static EnemyIntent Windup(EnemyIntent intent, params int[] cells)
        {
            intent.Kind = EnemyIntentKind.Windup; intent.Cells = cells.ToList(); return intent;
        }
        // Infected bite the foremost ally of a lane within reach, preferring the same lane and then the weakest.
        int BiteTarget(int e, Board b, int[] health, out int goal)
        {
            int best = -1, bestKey = int.MaxValue, goalKey = int.MaxValue; goal = b.L[e];
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i]; if (u.Enemy || !Alive(i, b) || !Exposed(i, b)) continue;
                int diff = Math.Abs(b.L[i] - b.L[e]);
                int key = diff * 100000 + (100 - BiteChance(b.D[i], u.Guarding, e)) * 100 + Math.Min(9, health[i]) * 10 + Math.Min(9, i);
                if (key < goalKey) { goalKey = key; goal = b.L[i]; }
                if (b.D[e] == 0 && diff <= Rules.LaneReach && key < bestKey) { bestKey = key; best = i; }
            }
            return best;
        }
        // Movement by gait: rooted creatures hold, slow ones rest after each move, fast ones take two steps.
        EnemyIntent Walk(EnemyIntent intent, int e, Board b, int goal, bool laneFirst)
        {
            var c = Units[e].Creature;
            if (c != null && c.Gait == CreatureGait.Rooted) return intent;
            if (c != null && c.Gait == CreatureGait.Slow && Units[e].Tired) { intent.Resting = true; return intent; }
            int hold = c?.HoldDepth ?? 0;
            if (!NextStep(e, b.D[e], b.L[e], b, goal, laneFirst, hold, out int d, out int l)) return intent;
            intent.Kind = d < b.D[e] ? EnemyIntentKind.Advance : EnemyIntentKind.Shift; intent.Depth = d; intent.Lane = l;
            if (c != null && c.Gait == CreatureGait.Fast && NextStep(e, d, l, b, goal, laneFirst, hold, out int d2, out int l2))
            {
                intent.ViaDepth = d; intent.ViaLane = l; intent.Depth = d2; intent.Lane = l2;
                if (d2 < d) intent.Kind = EnemyIntentKind.Advance;
            }
            return intent;
        }
        bool NextStep(int e, int d, int l, Board b, int goal, bool laneFirst, int hold, out int nd, out int nl)
        {
            nd = d; nl = l;
            if (laneFirst && goal != l && Free(true, d, l + Math.Sign(goal - l), b, e)) { nl = l + Math.Sign(goal - l); return true; }
            if (d > hold)
            {
                if (Free(true, d - 1, l, b, e)) { nd = d - 1; return true; }
                // Blocked ahead: sidestep only into a lane whose front cell is open, nearest the goal. Otherwise hold; never ping-pong.
                int pick = -1;
                foreach (int side in new[] { -1, 1 })
                {
                    int x = l + side;
                    if (Free(true, d, x, b, e) && Free(true, d - 1, x, b, e) && (pick < 0 || Math.Abs(goal - x) < Math.Abs(goal - pick))) pick = x;
                }
                if (pick >= 0) { nl = pick; return true; }
                return false;
            }
            int step = Math.Sign(goal - l);
            if (step != 0 && Free(true, d, l + step, b, e)) { nl = l + step; return true; }
            return false;
        }
        // The second head covers the cell the first victim would step into, or a neighbour who stands beside it.
        int TwinSecond(int target, Board b)
        {
            int d = b.D[target], l = b.L[target];
            var options = new List<int>();
            if (d < 2) options.Add(CellOf(d + 1, l));
            int inward = l == 0 ? 1 : l == 2 ? -1 : 1;
            foreach (int side in new[] { inward, -inward }) if (l + side >= 0 && l + side <= 2) options.Add(CellOf(d, l + side));
            foreach (int cell in options) if (At(cell % 3, cell / 3, b) >= 0) return cell;
            return options[0];
        }
        // Two touching ally cells that catch the most people (then the weakest, then the nearest lane).
        List<int> WirePair(int e, Board b, int[] health)
        {
            List<int> best = null; int bestScore = int.MinValue;
            for (int d = 0; d < 3; d++)
                for (int l = 0; l < 3; l++)
                    foreach (var o in new[] { new[] { d, l + 1 }, new[] { d + 1, l } })
                    {
                        if (o[0] > 2 || o[1] > 2) continue;
                        int a = At(d, l, b), z = At(o[0], o[1], b), people = (a >= 0 ? 1 : 0) + (z >= 0 ? 1 : 0);
                        if (people == 0) continue;
                        int hp = (a >= 0 ? health[a] : 0) + (z >= 0 ? health[z] : 0);
                        int score = people * 1000 - hp * 10 - Math.Abs(l - b.L[e]) - Math.Abs(o[1] - b.L[e]);
                        if (score > bestScore) { bestScore = score; best = new List<int> { CellOf(d, l), CellOf(o[0], o[1]) }; }
                    }
            return best;
        }
        static int[] LaneCells(int lane) => new[] { CellOf(0, lane), CellOf(1, lane), CellOf(2, lane) };
        int At(int depth, int lane, Board b)
        {
            if (!InBoard(depth, lane)) return -1;
            for (int i = 0; i < Units.Count; i++) if (!Units[i].Enemy && Alive(i, b) && b.D[i] == depth && b.L[i] == lane) return i;
            return -1;
        }
        int FrontMost(int lane, Board b)
        {
            for (int d = 0; d < 3; d++) { int i = At(d, lane, b); if (i >= 0) return i; }
            return -1;
        }

        // ---- committed strikes ----
        // Who the strike reaches on this board, applying pushes, deaths and the creature's own move to the board as it goes.
        List<BattleHit> Resolve(int e, PendingStrike p, Board b)
        {
            var hits = new List<BattleHit>(); int damage = DamageOf(e);
            switch (p.Attack)
            {
                case CreatureAttack.Shove: { int v = FrontMost(p.Lane, b); if (v >= 0) hits.Add(Land(v, damage, b, true, true)); break; }
                case CreatureAttack.Charge:
                {
                    int v = FrontMost(p.Lane, b); if (v >= 0) hits.Add(Land(v, damage, b, true, false));
                    if (Free(true, 0, p.Lane, b, e)) { b.D[e] = 0; b.L[e] = p.Lane; }
                    break;
                }
                case CreatureAttack.Ram:
                {
                    // Push from the back so the whole lane slides; the one against the wall takes the knock.
                    var lane = Enumerable.Range(0, 3).Select(d => At(d, p.Lane, b)).Where(i => i >= 0).ToList();
                    for (int k = lane.Count - 1; k >= 0; k--) hits.Add(Land(lane[k], damage, b, true, true));
                    hits.Reverse();
                    if (Free(true, 0, p.Lane, b, e)) { b.D[e] = 0; b.L[e] = p.Lane; }
                    break;
                }
                case CreatureAttack.Veil:
                {
                    var lane = Enumerable.Range(0, 3).Select(d => At(d, p.Lane, b)).Where(i => i >= 0).ToList();
                    for (int k = 0; k < lane.Count; k++) { var h = Land(lane[k], k == 0 ? damage : 0, b, false, false); if (!h.Killed) h.Veiled = true; hits.Add(h); }
                    break;
                }
                case CreatureAttack.Broadcast: break;
                default:
                    foreach (int cell in p.Cells)
                    {
                        int v = At(cell % 3, cell / 3, b); if (v < 0) continue;
                        var h = Land(v, damage, b, false, false);
                        if (p.Attack == CreatureAttack.Grab && !h.Killed) h.Bound = true;
                        hits.Add(h);
                    }
                    // The leap carries it forward: it lands in its own front row, within reach of a blade.
                    if (p.Attack == CreatureAttack.Pounce && Free(true, 0, b.L[e], b, e)) b.D[e] = 0;
                    break;
            }
            return hits;
        }
        BattleHit Land(int v, int damage, Board b, bool push, bool collide)
        {
            bool guard = Units[v].Guarding;
            var h = new BattleHit { Target = v, FromDepth = b.D[v], FromLane = b.L[v], ToDepth = b.D[v], ToLane = b.L[v] };
            int d = damage;
            if (guard && d > 0) { d = Math.Max(0, d - GuardReductionFor(v, true)); h.Braced = true; }
            if (push && !guard && b.H[v] - d > 0)
            {
                if (Free(false, b.D[v] + 1, b.L[v], b, v)) { b.D[v]++; h.Pushed = true; h.ToDepth = b.D[v]; }
                else if (collide) { d += Rules.CollisionDamage; h.Collided = true; }
            }
            h.Damage = d; b.H[v] = Math.Max(0, b.H[v] - d); h.Health = b.H[v]; h.Killed = d > 0 && b.H[v] == 0;
            return h;
        }
        void Strike(int e)
        {
            var me = Units[e]; var p = me.Pending; var c = me.Creature; me.Pending = null; me.Tired = false;
            var board = Board.From(Units); var hits = Resolve(e, p, board);
            var ev = new BattleEvent { Kind = BattleEventKind.Strike, Actor = e, Attack = p.Attack, Cells = p.Cells.ToList(), Side = p.Side, Hits = hits,
                FromDepth = me.Depth, FromLane = me.Lane, ToDepth = board.D[e], ToLane = board.L[e], Whiff = hits.Count == 0 && p.Attack != CreatureAttack.Broadcast };
            foreach (var h in hits)
            {
                var u = Units[h.Target]; Damage(u, h.Damage); u.Depth = h.ToDepth; u.Lane = h.ToLane;
                if (h.Bound) u.Bound = 1; if (h.Veiled) u.Veiled = 1;
            }
            me.Depth = board.D[e]; me.Lane = board.L[e];
            // Big committed moves, and any committed strike that met nobody, leave the creature open: it skips its next turn with no armor.
            if (p.Attack == CreatureAttack.Charge || p.Attack == CreatureAttack.Ram || p.Attack == CreatureAttack.Collapse || ev.Whiff)
            { me.Stagger = 1; ev.Staggered = true; }
            if (p.Attack == CreatureAttack.Wire) me.Recharging = true;
            Events.Add(ev);
            string name = c?.AttackName ?? "공격";
            if (p.Attack == CreatureAttack.Broadcast) { ev.Noise = Rules.BroadcastNoise; Message = me.Name + " · " + name + " · 소음 +" + ev.Noise; AddNoise(ev.Noise); return; }
            Message = me.Name + " · " + name + (ev.Whiff ? " · 빗나감" : " → " + string.Join(", ", hits.Select(h => Units[h.Target].Name + (h.Damage > 0 ? " 피해 " + h.Damage : h.Braced ? " 버팀" : "")
                + (h.Killed ? " 행동 불능" : h.Pushed ? " 밀려남" : h.Collided ? " 부딪힘" : h.Bound ? " 묶임" : h.Veiled ? " 시야 가림" : ""))))
                + (ev.Staggered ? " · 빈틈" : "");
        }
        void Swarm(int e, EnemyIntent plan)
        {
            var me = Units[e];
            var ev = new BattleEvent { Kind = BattleEventKind.Strike, Actor = e, Attack = CreatureAttack.Swarm, FromDepth = me.Depth, FromLane = me.Lane, ToDepth = me.Depth, ToLane = me.Lane };
            foreach (var target in plan.Hits)
            {
                var t = Units[target.Target]; if (!t.Alive) continue;
                bool hit = roll() < target.Chance; int damage = hit ? AfterGuardDamage(target.Target, DamageOf(e), false) : 0;
                if (damage > 0) Damage(t, damage);
                ev.Hits.Add(new BattleHit { Target = target.Target, Chance = target.Chance, Hit = hit, Damage = damage, Health = t.Health, Killed = !t.Alive,
                    Braced = t.Guarding && (!hit || damage == 0), FromDepth = t.Depth, FromLane = t.Lane, ToDepth = t.Depth, ToLane = t.Lane });
            }
            ev.Cells = ev.Hits.Select(h => CellOf(h.FromDepth, h.FromLane)).ToList();
            Events.Add(ev);
            Message = me.Name + " · " + (me.Creature?.AttackName ?? "뒤덮기") + " → " + string.Join(", ", ev.Hits.Select(h => Units[h.Target].Name + " " + (!h.Hit ? "빗나감" : h.Braced ? "막음" : "피해 " + h.Damage)));
        }

        int[] Healths() => Units.Select(u => u.Health).ToArray();
        int[] TargetHealth() => Current.Enemy && phaseHealth != null && phaseHealth.Length == Units.Count ? phaseHealth : Healths();
        public bool Exposed(int unit) => Exposed(unit, Board.From(Units));
        bool Alive(int unit, Board b) => Units[unit].Enemy ? Units[unit].Alive : b.H[unit] > 0;
        bool Exposed(int unit, Board b)
        {
            var u = Units[unit];
            for (int k = 0; k < Units.Count; k++)
                if (k != unit && Alive(k, b) && Units[k].Enemy == u.Enemy && b.L[k] == b.L[unit] && b.D[k] < b.D[unit]) return false;
            return true;
        }
        static bool InBoard(int depth, int lane) => depth >= 0 && depth < 3 && lane >= 0 && lane < 3;
        bool Free(bool enemySide, int depth, int lane) => InBoard(depth, lane) && !Units.Any(u => u.Alive && u.Enemy == enemySide && u.Depth == depth && u.Lane == lane);
        bool Free(bool enemySide, int depth, int lane, Board b, int except = -1)
        {
            if (!InBoard(depth, lane)) return false;
            for (int k = 0; k < Units.Count; k++) if (k != except && Units[k].Enemy == enemySide && Alive(k, b) && b.D[k] == depth && b.L[k] == lane) return false;
            return true;
        }

        void Bite(int attacker, int target, int chance, bool retreating)
        {
            var a = Units[attacker]; var t = Units[target];
            bool hit = roll() < chance;
            int damage = hit ? AfterGuardDamage(target, retreating ? Rules.EnemyDamage : BiteDamage(attacker), false) : 0;
            // A guarding ally who is missed, or whose guard soaks the whole bite, reads as a block.
            bool blocked = t.Guarding && (!hit || damage == 0);
            if (retreating) damage = Math.Min(damage, Math.Max(0, t.Health - 1));
            if (damage > 0) Damage(t, damage);
            var ev = new BattleEvent { Kind = BattleEventKind.Attack, Actor = attacker, Target = target, Chance = chance, Hit = hit, Damage = damage, Attack = a.Creature?.Attack ?? CreatureAttack.Bite,
                Blocked = blocked, Killed = !t.Alive, Health = t.Health, Retreating = retreating, FromDepth = t.Depth, FromLane = t.Lane, ToDepth = t.Depth, ToLane = t.Lane };
            Events.Add(ev);
            Message = a.Name + " → " + t.Name + " · " + (!hit ? "빗나감" : blocked ? "방어로 막음" : damage == 0 ? "간신히 버팀" : "피해 " + damage + (ev.Killed ? " · 행동 불능" : ""));
        }
        void AddNoise(int amount)
        {
            if (amount <= 0) return;
            Noise += amount; RaiseAlarm();
        }
        // Warn one round ahead, and only when the promise can be kept: infected still fighting and a free slot.
        void RaiseAlarm()
        {
            int alive = Units.Count(u => u.Enemy && u.Alive);
            if (ReinforcementPending || !ReinforcementsLeft || Noise < NextReinforcementNoise || alive == 0 || alive >= Rules.MaxEnemies) return;
            ReinforcementPending = true;
            Events.Add(new BattleEvent { Kind = BattleEventKind.Alarm });
        }
        Unit Spawn(int preferredDepth, bool reinforcement, BattleCreature creature = null)
        {
            if (creature != null && creature.StartDepth >= 0) preferredDepth = creature.StartDepth;
            var taken = Units.Where(u => u.Enemy && u.Alive).Select(u => u.Lane).ToList();
            for (int d = preferredDepth, tries = 0; tries < 3; tries++, d = (d + 2) % 3)
            {
                var lanes = Enumerable.Range(0, 3).Where(l => Free(true, d, l)).ToList();
                if (lanes.Count == 0) continue;
                var open = lanes.Where(l => !taken.Contains(l)).ToList(); if (open.Count > 0) lanes = open;
                int lane = lanes[Math.Abs(roll()) % lanes.Count];
                string name;
                if (creature == null) name = "감염자 " + (++spawned);
                else { int same = Units.Count(u => u.Creature == creature); name = same == 0 ? creature.Name : creature.Name + " " + (same + 1); }
                int health = creature?.Health ?? Rules.EnemyHealth;
                return new Unit { Name = name, Creature = creature, Enemy = true, Health = health, Maximum = health, Depth = d, Lane = lane, Reinforcement = reinforcement };
            }
            return null;
        }
        static void Damage(Unit unit, int amount)
        {
            unit.Health = Math.Max(0, unit.Health - amount);
            if (unit.Person != null) unit.Person.Health = unit.Health;
        }
        void BeginRound()
        {
            // A new round: whoever shot last round has gone quiet again.
            foreach (var u in Units) if (!u.Enemy) u.Loud = 0;
            if (ReinforcementPending && Units.Count(u => u.Enemy && u.Alive) < Rules.MaxEnemies)
            {
                var unit = Spawn(Rules.ReinforcementDepth, true, pool != null && pool.Count > 0 ? BattleCreatureRoster.Pick(pool, roll) : null);
                if (unit != null)
                {
                    Units.Add(unit); Reinforcements++; ReinforcementPending = false;
                    Events.Add(new BattleEvent { Kind = BattleEventKind.Reinforce, Actor = Units.Count - 1, ToDepth = unit.Depth, ToLane = unit.Lane });
                }
            }
            RaiseAlarm();
        }
        // Anything that can still hurt someone, or a broadcaster that can still call help.
        // A call is only possible while a reinforcement is left and the enemy cap has room (the same test RaiseAlarm makes).
        bool Hostile(Unit u) => u.Creature == null || u.Creature.Attack != CreatureAttack.Broadcast || ReinforcementPending
            || (ReinforcementsLeft && Units.Count(x => x.Enemy && x.Alive) < Rules.MaxEnemies);
        void Advance()
        {
            Actions++;
            // Binding and veiling last for the ally's own next turn.
            if (!Current.Enemy) { if (Current.Bound > 0) Current.Bound--; if (Current.Veiled > 0) Current.Veiled--; }
            if (!Units.Any(u => u.Enemy && u.Alive && Hostile(u)))
            {
                Outcome = FieldBattleOutcome.Victory;
                if (Units.Any(u => u.Enemy && u.Alive)) Message = "남은 것은 잡음뿐입니다 · 더 부를 것이 없어 교전을 끝냅니다";
                return;
            }
            if (!Units.Any(u => !u.Enemy && u.Alive)) { Outcome = FieldBattleOutcome.Defeat; return; }
            bool allyActed = !Current.Enemy;
            do { Actor = (Actor + 1) % Units.Count; if (Actor == 0) { Round++; BeginRound(); } } while (!Current.Alive);
            if (allyActed && Current.Enemy) phaseHealth = Healths();
            Current.Guarding = false; Moved = false;
        }
    }
}
