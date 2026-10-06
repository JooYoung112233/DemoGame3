using System;
using System.Linq;
using Demo5.FrontEnd;
using Demo5.NightRun;
using UnityEditor;

// Pure disposable rule states plus read-only catalog data. No scene, save or source asset changes.
public static class VerifyCharacterBalance
{
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static PartyRoster Roster;
    static PartyCandidate Profile(string id) => Roster.Candidates.Single(c => c.Id == id);
    static Adventurer Person(string id) => Profile(id).CreateAdventurer();
    static BattleCreature Creature(CreatureAttack attack = CreatureAttack.Bite, int armor = 0, int damage = 2)
        => new BattleCreature { Id = "fixture", Name = "검수용", Health = 30, Armor = armor, Damage = damage, StartDepth = 0, Attack = attack, HitChance = 95 };
    static FieldBattleState Battle(Adventurer[] party, BattleCreature creature = null)
        => new FieldBattleState(party, 1, p => 8, p => true, () => 0,
            new FieldBattleRules { CriticalChance = 0, ReinforcementNoise = 999 }, new[] { creature ?? Creature() });

    public static string Run()
    {
        Roster = AssetDatabase.LoadAssetAtPath<PartyRoster>("Assets/Data/PartyRoster.asset");
        Check(Roster && Roster.Candidates.Length == 6, "Expected the six-person roster. Run BuildCharacterBalance first.");
        Catalog(); Movement(); ArmorAndOpening(); Healing(); Guard();
        return "PASS: six stat/role profiles and opening pair; cloned traits; saved stat preservation; 2-hit starting survival; movement hit-factor/actual match/reset; armor and stagger preview/actual; typed recovery/consume/clamp; guard bite/swarm/strike/retreat prediction match; distinct work percentages. No scene/save/assets changed.";
    }

    static void Catalog()
    {
        var ids = new[] { "scout", "medic", "cook", "mechanic", "researcher", "guard" };
        var hp = new[] { 6, 6, 7, 6, 5, 8 }; var bags = new[] { 4, 3, 4, 5, 3, 2 }; var aims = new[] { 0, -5, -10, -5, 5, 0 };
        Check(Roster.Candidates.Where(c => c.AvailableAtStart).Select(c => c.Id).OrderBy(x => x).SequenceEqual(new[] { "medic", "scout" }), "The opening pair changed.");
        Check(Roster.Candidates.Select(c => c.CombatRole).Distinct().Count() == 6, "Combat role labels are not distinct.");
        for (int i = 0; i < ids.Length; i++)
        {
            var p = Profile(ids[i]); var member = p.CreateAdventurer();
            Check(member.MaxHealth == hp[i] && member.Health == hp[i] && member.BagCapacity == bags[i] && member.Aim == aims[i], "Wrong catalog/factory stats: " + ids[i]);
            Check(!string.IsNullOrWhiteSpace(p.CombatTraitSummary) && !string.IsNullOrWhiteSpace(p.UnlockHint), "Missing meaningful profile metadata: " + ids[i]);
            Check(!ReferenceEquals(p.Traits, member.Traits), "Factory shares mutable trait data with the asset.");
            int assetMove = p.Traits.MoveHitBonus; member.Traits.MoveHitBonus = 123;
            Check(p.Traits.MoveHitBonus == assetMove, "Editing a fixture mutated the asset traits.");
            var saved = new Adventurer("custom", "role", "desc", 13, 17, 9) { Health = 4 };
            p.ApplyTraits(saved);
            Check(saved.Health == 4 && saved.MaxHealth == 13 && saved.Aim == 17 && saved.BagCapacity == 9, "ApplyTraits rewrote saved stats.");
        }
        var campaign = new CampaignState(new[] { Person("scout"), Person("medic") }, true);
        campaign.Toggle(0); campaign.Toggle(1); campaign.ConfirmParty(); campaign.Settle(0); campaign.BeginSettlementIntroduction();
        Check(campaign.Party.All(p => p.Health == p.MaxHealth && p.Health - 2 * 2 > 0), "Arrival must preserve the healthy opening pair without an authored injury.");
        Check(Profile("mechanic").WorkTimePercent(false) == 80 && Profile("mechanic").WorkTimePercent(false, true) == 100, "Mechanic bonus incorrectly affects research.");
        Check(Profile("researcher").WorkTimePercent(false) == 100 && Profile("researcher").WorkTimePercent(false, true) == 70, "Researcher bonus affects the wrong work type.");
        Check(Profile("cook").WorkTimePercent(true) == 80 && Profile("cook").WorkTimePercent(false) == 100, "Cook bonus affects crafting.");
        Check(LifeTraitTime.Calculate(31, 2, 80, Profile("researcher").WorkTimePercent(false, true)) == 35, "Research duration lost one-ceiling composition.");
        Check(new Adventurer("unknown", "", "", 6, 0).Traits.ItemRecovery(2, "bandage") == 2, "Unknown legacy character gained a trait.");
    }

    static void Movement()
    {
        var scout = Person("scout"); var plain = new Adventurer("plain", "", "", scout.MaxHealth, scout.Aim, scout.BagCapacity);
        var s = Battle(new[] { scout, Person("medic") }); var control = Battle(new[] { plain, Person("medic") });
        int enemy = s.Units.Count - 1;
        Check(s.HitChance(enemy, true) == control.HitChance(enemy, true), "Move bonus applies before moving.");
        Check(s.Move(0, 1) && control.Move(0, 1), "Movement fixture could not move.");
        Check(s.HitChance(enemy, true) == control.HitChance(enemy, true) + 10, "Move bonus is absent or applied twice.");
        Check(s.HitFactors(enemy, true).Sum(t => t.Value) == s.HitChance(enemy, true), "Hit tooltip terms differ from the actual chance.");
        int chance = s.HitChance(enemy, true), damage = s.ExpectedDamage(enemy, true);
        Check(s.Attack(enemy, true) && s.Events.Single(e => e.Kind == BattleEventKind.Attack).Chance == chance && s.Events.Single(e => e.Kind == BattleEventKind.Attack).Damage == damage, "Move attack preview differs from resolution.");
        s.Guard(); s.EnemyStep();
        Check(s.Actor == 0 && !s.Moved && !s.HitFactors(enemy, true).Any(t => t.Key == "발 빠른 대응"), "Move bonus leaked into the next turn.");
    }

    static void ArmorAndOpening()
    {
        var s = Battle(new[] { Person("mechanic") }, Creature(armor: 1)); int target = 1;
        Check(s.ExpectedDamage(target, false) == 2 && s.ExpectedDamage(target, true) == 2, "Mechanic armor bonus changed ranged damage or did not apply to melee.");
        int expected = s.ExpectedDamage(target, false);
        Check(s.Attack(target, false) && s.Events.Single(e => e.Kind == BattleEventKind.Attack).Damage == expected, "Armored melee preview differs from actual damage.");
        s = Battle(new[] { Person("mechanic") }, Creature(armor: 1)); s.Units[1].Stagger = 1;
        Check(s.ArmorOf(1) == 0 && s.ExpectedDamage(1, false) == 2, "Mechanic gained armor bonus after the armor was removed by stagger.");
        s = Battle(new[] { Person("researcher") }, Creature(armor: 2));
        Check(s.ExpectedDamage(1, false) == 0, "Opening bonus applies without an opening.");
        s.Units[1].Stagger = 1;
        Check(s.ExpectedDamage(1, false) == 3 && s.ExpectedDamage(1, true) == 4, "Opening bonus did not affect both attacks after armor removal.");
        expected = s.ExpectedDamage(1, false);
        Check(s.Attack(1, false) && s.Events.Single(e => e.Kind == BattleEventKind.Attack).Damage == expected, "Opening damage preview differs from actual damage.");
    }

    static void Healing()
    {
        foreach (string id in new[] { "medic", "cook", "scout" })
        foreach (string item in new[] { "bandage", "ration", "meal", "cloth", "water", "unknown" })
        {
            var user = Person(id); var target = Person("guard"); target.Health = 1;
            bool food = item == "ration" || item == "meal";
            if (food) user.Health = 1;
            var s = Battle(new[] { user, target });
            int bonus = id == "medic" && item == "bandage" || id == "cook" && (item == "ration" || item == "meal") ? 1 : 0;
            Check(s.ItemRecovery(2, item) == 2 + bonus && user.Traits.ItemRecovery(2, item) == 2 + bonus && s.ItemRecovery(0, item) == 0, "Item ID-specific recovery mismatch.");
            int spent = 0;
            Check(s.UseItem(food ? 0 : 1, 2, () => { spent++; return true; }, item) && (food ? user : target).Health == 3 + bonus && spent == 1 && s.ItemsUsed == 1, "Recovery/consumption differs from preview.");
        }
        var medic = Person("medic"); var patient = Person("scout"); patient.Health = patient.MaxHealth - 1;
        var capped = Battle(new[] { medic, patient }); int consumed = 0;
        Check(!capped.UseItem(1, 2, () => false, "bandage") && patient.Health == patient.MaxHealth - 1, "Failed consumption changed health.");
        Check(capped.UseItem(1, 2, () => { consumed++; return true; }, "bandage") && patient.Health == patient.MaxHealth && consumed == 1 && capped.Events.Single().Damage == 1, "Treatment did not cap at maximum health.");
    }

    static void Guard()
    {
        var s = Battle(new[] { Person("guard") });
        Check(s.GuardReductionFor(0, false) == 1 && s.GuardReductionFor(0, true) == 2, "Guard action preview has the wrong reduction.");
        s.Guard(); var predicted = s.PredictIntents().Single(); int before = s.Units[0].Health;
        Check(predicted.Kind == EnemyIntentKind.Attack && predicted.Damage == 1, "Guarded bite prediction ignored the human trait.");
        s.EnemyStep(); Check(before - s.Units[0].Health == predicted.Damage, "Guarded bite prediction differs from actual damage.");

        s = Battle(new[] { Person("guard"), Person("medic") }, Creature(CreatureAttack.Swarm));
        s.Units[1].Lane = 1;
        s.Guard(); s.Guard(); predicted = s.PredictIntents().Single();
        Check(predicted.Hits.Count == 2, "Swarm fixture did not reach both guarded allies.");
        s.EnemyStep(); var swarm = s.Events.Single(e => e.Kind == BattleEventKind.Strike);
        Check(predicted.Hits.All(p => swarm.Hits.Single(h => h.Target == p.Target).Damage == p.Damage), "Mixed guarded swarm prediction differs from actual damage.");

        s = Battle(new[] { Person("guard") }, Creature(CreatureAttack.Shove, damage: 3));
        s.Guard(); s.EnemyStep();
        Check(s.Units[1].Pending != null, "Committed-strike fixture did not wind up.");
        s.Guard(); predicted = s.PredictIntents().Single();
        Check(predicted.Kind == EnemyIntentKind.Strike && predicted.Hits.Single().Damage == 1, "Guarded strike prediction ignored the extra reduction.");
        s.EnemyStep(); var strike = s.Events.Single(e => e.Kind == BattleEventKind.Strike);
        Check(strike.Hits.Single().Damage == predicted.Hits.Single().Damage && !strike.Hits.Single().Pushed, "Guarded strike resolution differs from prediction or still pushes.");

        s = Battle(new[] { Person("guard") }); s.Units[0].Guarding = true;
        predicted = s.RetreatThreats().Single(); before = s.Units[0].Health;
        Check(predicted.Damage == 0 && s.Retreat() && s.Units[0].Health == before, "Retreat preview and guard absorption differ.");
    }
}
