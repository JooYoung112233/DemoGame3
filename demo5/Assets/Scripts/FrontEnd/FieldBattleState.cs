using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.NightRun;

namespace Demo5.FrontEnd
{
    public enum FieldBattleOutcome { Playing, Victory, Retreated, Defeat }

    // Battle time is separate from exploration time: one resolved battle costs one exploration turn.
    public sealed class FieldBattleState
    {
        public sealed class Unit
        {
            public Adventurer Person;
            public string Name;
            public int Health, Maximum, Depth, Lane;
            public bool Enemy, Guarding;
            public bool Alive => Health > 0;
        }

        public readonly List<Unit> Units = new List<Unit>();
        public FieldBattleOutcome Outcome { get; private set; }
        public int Actor { get; private set; }
        public int Round { get; private set; } = 1;
        public bool Moved { get; private set; }
        public int AmmoSpent { get; private set; }
        public int Actions { get; private set; }
        public string Message { get; private set; } = "빈 칸으로 이동하거나 행동을 선택하세요.";
        public Unit Current => Units[Actor];
        readonly Func<int> roll;
        readonly Func<Adventurer, int> ammo;
        readonly Func<Adventurer, bool> spendAmmo;

        public FieldBattleState(IEnumerable<Adventurer> party, int enemies,
            Func<Adventurer, int> ammoCount, Func<Adventurer, bool> consumeAmmo, Func<int> random)
        {
            ammo = ammoCount; spendAmmo = consumeAmmo; roll = random;
            int i = 0;
            foreach (var p in party.Where(p => p.Health > 0))
            {
                if (i >= 9) throw new ArgumentException("The initial formation holds nine units.");
                Units.Add(new Unit { Person = p, Name = p.Name, Health = p.Health, Maximum = p.MaxHealth, Depth = i / 3, Lane = (i * 2) % 3 });
                i++;
            }
            if (i == 0) throw new ArgumentException("A living party is required.");
            for (int e = 0; e < Math.Max(1, Math.Min(3, enemies)); e++)
                Units.Add(new Unit { Name = "감염자 " + (e + 1), Enemy = true, Health = 3, Maximum = 3, Depth = 0, Lane = (e * 2) % 3 });
        }

        public bool PlayerTurn => Outcome == FieldBattleOutcome.Playing && !Current.Enemy;
        public bool CanMove(int depth, int lane) => PlayerTurn && !Moved && depth >= 0 && depth < 3 && lane >= 0 && lane < 3
            && Math.Abs(Current.Depth - depth) + Math.Abs(Current.Lane - lane) == 1
            && !Units.Any(u => u.Alive && !u.Enemy && u.Depth == depth && u.Lane == lane);
        public bool Move(int depth, int lane)
        {
            if (!CanMove(depth, lane)) return false;
            Current.Depth = depth; Current.Lane = lane; Moved = true;
            Message = Current.Name + " · 위치 변경 / 행동 1회 남음";
            return true;
        }
        public int HitChance(int target, bool ranged)
        {
            if (!PlayerTurn || target < 0 || target >= Units.Count || !Units[target].Enemy || !Units[target].Alive) return 0;
            if (!ranged && (Current.Depth != 0 || Units[target].Depth != 0)) return 0;
            return Math.Max(20, Math.Min(95, (ranged ? 75 + Current.Person.Aim - Current.Depth * 5 : 90)
                - Math.Abs(Current.Lane - Units[target].Lane) * 5));
        }
        public bool CanAttack(int target, bool ranged) => HitChance(target, ranged) > 0 && (!ranged || ammo(Current.Person) > 0);
        public bool Attack(int target, bool ranged)
        {
            if (!CanAttack(target, ranged)) return false;
            int chance = HitChance(target, ranged);
            if (ranged && !spendAmmo(Current.Person)) return false;
            if (ranged) AmmoSpent++;
            var victim = Units[target]; bool hit = roll() < chance;
            if (hit) Damage(victim, ranged ? 3 : 2);
            Message = Current.Name + " → " + victim.Name + " · " + chance + "% · " + (hit ? "명중 " + (ranged ? 3 : 2) : "빗나감");
            Advance(); return true;
        }
        public bool Guard()
        {
            if (!PlayerTurn) return false;
            Current.Guarding = true; Message = Current.Name + " · 다음 차례까지 피해 1 감소";
            Advance(); return true;
        }
        public bool Retreat()
        {
            if (!PlayerTurn) return false;
            // First slice uses guaranteed withdrawal; confirm UI discloses the one-turn cost.
            Outcome = FieldBattleOutcome.Retreated; Message = "원정대가 복도로 물러납니다."; return true;
        }
        public bool EnemyStep()
        {
            if (Outcome != FieldBattleOutcome.Playing || !Current.Enemy) return false;
            var target = Units.Where(u => !u.Enemy && u.Alive).OrderBy(u => u.Depth * 3 + Math.Abs(u.Lane - Current.Lane)).First();
            bool hit = roll() < 75; int damage = target.Guarding ? 0 : 1;
            if (hit) Damage(target, damage);
            Message = Current.Name + " → " + target.Name + " · " + (hit ? (damage == 0 ? "방어" : "피해 " + damage) : "빗나감");
            Advance(); return true;
        }
        static void Damage(Unit unit, int amount)
        {
            unit.Health = Math.Max(0, unit.Health - amount);
            if (unit.Person != null) unit.Person.Health = unit.Health;
        }
        void Advance()
        {
            Actions++;
            if (!Units.Any(u => u.Enemy && u.Alive)) { Outcome = FieldBattleOutcome.Victory; return; }
            if (!Units.Any(u => !u.Enemy && u.Alive)) { Outcome = FieldBattleOutcome.Defeat; return; }
            do { Actor = (Actor + 1) % Units.Count; if (Actor == 0) Round++; } while (!Current.Alive);
            Current.Guarding = false; Moved = false;
        }
    }
}
