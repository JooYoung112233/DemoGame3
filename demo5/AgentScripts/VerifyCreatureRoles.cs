using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using Demo5.NightRun;
using UnityEditor;

// Uses the applied data and real combat calculations. Root runs this in Unity after BuildCreatureRoles.Run.
public static class VerifyCreatureRoles
{
    static void Check(bool value, string why) { if (!value) throw new Exception("Creature roles: " + why); }
    static FieldBattleRules Quiet() => new FieldBattleRules { CriticalChance = 0, MaxReinforcements = 0 };
    static FieldBattleState Board(BattleCreature creature, int enemyDepth = 0, int allyDepth = 0, int health = 6)
    {
        var s = new FieldBattleState(new[] { new Adventurer("검수", "", "", health, 0) }, 1,
            p => 9, p => true, () => 0, Quiet(), creature != null ? new[] { creature } : null);
        s.Units[0].Depth = allyDepth; s.Units[0].Lane = 1;
        s.Units[1].Depth = enemyDepth; s.Units[1].Lane = 1;
        return s;
    }
    static void Pass(FieldBattleState s)
    {
        while (s.PlayerTurn) { int at = s.Actor; s.Guard(); s.Units[at].Guarding = false; }
    }
    static BattleEvent Landed(FieldBattleState s, string name)
    {
        Pass(s); s.EnemyStep();
        Check(s.Units[1].Pending != null, name + " must warn before damage");
        Pass(s); s.EnemyStep();
        var strike = s.Events.LastOrDefault(e => e.Kind == BattleEventKind.Strike);
        Check(strike != null, name + " must resolve its warning");
        return strike;
    }
    static int Hits(FieldBattleState s, bool shot)
    {
        int damage = s.ExpectedDamage(1, shot);
        Check(damage > 0, "No ordinary creature should require critical hits to be damaged");
        return (s.Units[1].Health + damage - 1) / damage;
    }

    public static string Run()
    {
        var roster = AssetDatabase.LoadAssetAtPath<BattleCreatureRoster>("Assets/Data/BattleCreatures.asset");
        Check(roster != null && roster.Creatures.Count == 12, "12 profiles required");
        var done = new List<string>();
        BattleCreature C(string id) => roster.Find(id) ?? throw new Exception("Missing " + id);

        // The public Begin path treats an empty lineup as infected, and FieldBattleState itself also supports it.
        var opening = roster.Lineup(3, () => 0);
        Check(opening.Count == 0 && !roster.Pool.Any(), "Opening pool must not expose future creatures");
        var start = new FieldBattleState(new[] { new Adventurer("도입", "", "", 6, 0) }, 2,
            p => 0, p => false, () => 0, Quiet(), opening);
        Check(start.Units.Count == 3 && start.Units.Skip(1).All(u => u.Creature == null && u.Maximum == 4), "Empty lineup must spawn the requested infected count with HP4");
        Check(CreatureRoles.For(null) == CreatureRole.Melee && start.DamageOf(1) == 1, "Infected role and damage fallback");
        done.Add("opening: infected fallback, original count/HP4/damage1");

        int[] sizes = { 0, 4, 8, 11 };
        for (int tier = 0; tier < sizes.Length; tier++)
        {
            var pool = roster.PoolForTier(tier).ToArray();
            Check(pool.Length == sizes[tier], "Tier " + tier + " population");
            Check(pool.All(c => c.MinRegionTier <= tier && c.Weight > 0), "Locked creature leaked into tier " + tier);
            foreach (int seed in Enumerable.Range(0, 100))
                Check(roster.Lineup(3, () => seed, tier).All(pool.Contains), "Lineup leaked a future creature");
        }
        Check(!new BattleCreature().AvailableInRegion(0), "New profiles must be safe by default");
        var listener = C("02-listener");
        Check(listener.Weight == 0 && listener.SuppressReinforcements && roster.Creatures.Count(c => c.SuppressReinforcements) == 1, "Resident is explicit-only and singular");
        Check(!roster.PoolForTier(99).Contains(listener), "Resident must not enter random pools at a later tier");
        done.Add("regional pools 0/4/8/11; 400 seeded lineups; scripted resident preserved");

        // These are successful, non-critical hits, not expected rounds; range/misses/cover remain real costs.
        var door = Board(C("01-door-bearer"));
        Check(Hits(door, false) == 8 && Hits(door, true) == 4, "Door front durability");
        door.Units[0].Lane = 0;
        Check(Hits(door, false) == 4 && Hits(door, true) == 3, "Door flank must halve melee actions");
        var stairs = Board(C("09-stairback"));
        Check(Hits(stairs, false) == 8 && Hits(stairs, true) == 4, "Armored defender durability");
        stairs.Units[1].Stagger = 1;
        Check(Hits(stairs, false) == 4 && Hits(stairs, true) == 3, "Stairback opening removes armor");
        foreach (var c in roster.Creatures)
        {
            var board = Board(c);
            Check(board.ExpectedDamage(1, false) >= 1 && board.ExpectedDamage(1, true) >= 1, c.Id + " cannot be immune to basic attacks");
            int maximumHit = c.Damage + (c.Attack == CreatureAttack.Ram || c.Attack == CreatureAttack.Shove ? board.Rules.CollisionDamage : 0);
            Check(maximumHit <= 3, c.Id + " must leave a full HP4 survivor alive after one unguarded hit");
            Check(c.Health >= 4 && c.Health <= 8 && !string.IsNullOrWhiteSpace(c.RoleName) && !string.IsNullOrWhiteSpace(c.Counter), c.Id + " HP/readability bounds");
            if (c.Role == CreatureRole.Disruptor) Check(c.Damage == 1, "Control effects must trade away burst damage");
        }
        done.Add("defender front/flank/opening breakpoints; no melee immunity or full-health one-hit knockdown");

        // Strong attacks retain their warning; failed avoidance is dangerous but a single hit is survivable.
        var hound = Board(C("04-seam-hound"), 1, 0, 4);
        Check(Landed(hound, "hound").Hits.Single().Damage == 3 && hound.Units[0].Health == 1 && hound.Units[1].Stagger == 1, "Hound: 3 damage then an opening");
        var cart = Board(C("12-bellied-cart"), 1, 2, 4);
        var ram = Landed(cart, "cart").Hits.Single();
        Check(ram.Collided && ram.Damage == 3 && cart.Units[0].Health == 1, "Cart: 2 plus collision, survivable from HP4");
        var puddle = Board(C("08-puddle"));
        var grab = Landed(puddle, "puddle").Hits.Single();
        Check(grab.Damage == 1 && grab.Bound, "Puddle exchanges damage for movement denial");
        done.Add("actual attacks: hound3 + opening, ram2+collision1, puddle1 + bind");

        var quiet = Board(listener);
        Pass(quiet); quiet.EnemyStep();
        Check(quiet.Events.Single(e => e.Kind == BattleEventKind.Attack).Damage == 1 && quiet.Units[1].Pending == null, "A quiet party must face the listener's weak melee, not ranged slams");
        var loud = Board(listener, 2, 2);
        loud.Units[0].Loud = 1;
        Check(Landed(loud, "listener").Hits.Single().Damage == 2, "Listener: sound-triggered range across the board");
        var meter = Board(C("06-meter-keeper"), 1, 2);
        Check(Landed(meter, "meter").Hits.Single().Damage == 2, "Meter attacks a distant marked pair from the middle row");
        done.Add("ranged distinction: listener reacts to gunfire, meter reaches marked cells");

        return "PASS · " + string.Join("\n", done);
    }
}
