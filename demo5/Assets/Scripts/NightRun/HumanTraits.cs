using System;

namespace Demo5.NightRun
{
    // Catalog-authored, ID-bound capabilities. No cooldown or permanent state belongs in this object.
    [Serializable] public sealed class HumanTraits
    {
        public int MoveHitBonus, BandageRecoveryBonus, FoodRecoveryBonus;
        public int ArmoredMeleeBonus, StaggerDamageBonus, GuardDamageReduction;

        public HumanTraits Copy() => (HumanTraits)MemberwiseClone();
        public int ItemRecovery(int baseRecovery, string itemId)
        {
            if (baseRecovery <= 0) return 0;
            int bonus = itemId == "bandage" ? BandageRecoveryBonus
                : itemId == "ration" || itemId == "meal" ? FoodRecoveryBonus : 0;
            return baseRecovery + Math.Max(0, bonus);
        }
    }
}
