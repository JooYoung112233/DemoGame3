using System;

namespace Demo5.FrontEnd
{
    public sealed partial class FieldBattleState
    {
        int MovedHitBonus => PlayerTurn && Moved ? Math.Max(0, Current.Person?.Traits.MoveHitBonus ?? 0) : 0;

        int HumanDamageBonus(int target, bool ranged)
        {
            if (!PlayerTurn || target < 0 || target >= Units.Count || !Units[target].Enemy || Current.Person == null) return 0;
            var traits = Current.Person.Traits;
            int bonus = !ranged && ArmorOf(target, false) > 0 ? Math.Max(0, traits.ArmoredMeleeBonus) : 0;
            if (Units[target].Stagger > 0) bonus += Math.Max(0, traits.StaggerDamageBonus);
            return bonus;
        }

        // Same actor-dependent amount is used by the item drawer and the actual application.
        // Food affects its user; the battle UI already restricts rations to self-use.
        public int ItemRecovery(int baseRecovery, string itemId) => Current.Person?.Traits.ItemRecovery(baseRecovery, itemId) ?? Math.Max(0, baseRecovery);

        // The amount a unit would block when guarding; deliberately independent of its current pose
        // so the action card can preview it before the player chooses Guard.
        public int GuardReductionFor(int unit, bool strike)
        {
            int bonus = unit >= 0 && unit < Units.Count ? Math.Max(0, Units[unit].Person?.Traits.GuardDamageReduction ?? 0) : 0;
            return Math.Max(0, strike ? Rules.GuardStrikeReduction : Rules.GuardReduction) + bonus;
        }

        int AfterGuardDamage(int target, int damage, bool strike) => Math.Max(0, damage - (Units[target].Guarding ? GuardReductionFor(target, strike) : 0));
    }
}
