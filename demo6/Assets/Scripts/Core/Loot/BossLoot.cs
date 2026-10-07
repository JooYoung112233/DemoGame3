using Demo6.Core.Combat;
using Demo6.Core.Random;

namespace Demo6.Core.Loot
{
    /// <summary>
    /// 보스 보상 묶음(기획/전투-보스-무기-다듬기-1차.md 3-8 시험판 표). 결과는 Core가 먼저 확정하고(같은 씨앗이면 같은 묶음) 화면은 흩뿌리기만 한다.
    /// 첫 처치: 장비 BossRules.TrialFirstGear(3) = 희귀 이상 1 보장 + 나머지 고급 이상('일반 0'), 강화석 10, 골드 40.
    /// 다시 잡을 때: 장비 1(고급 이상), 강화석 4, 골드 15. 아이템 레벨 = 층 + 1(2층 굴 = 3). 경험치(20U)는 처치 경험치 규칙이 따로 준다.
    /// 장비는 LootRules.RollGear(아이템 레벨, rng, 가장 낮은 등급, context)로 굴린다(부위 보정·가진 전설은 context).
    /// </summary>
    public static class BossLoot
    {
        /// <summary>보상 아이템 레벨(층 + 1, 1~10).</summary>
        public static int ItemLevel(int floor) => FloorScaling.Clamp(FloorScaling.Clamp(floor) + 1);

        /// <summary>보스 보상 묶음 하나. 장비 → 강화석 → 골드(무더기) 차례로 난수를 쓴다.</summary>
        public static LootBundle Roll(int floor, bool firstClear, IRandom rng, GearRollContext context = null)
        {
            var bundle = new LootBundle();
            if (rng == null) return bundle;
            int lv = ItemLevel(floor);
            if (firstClear)
            {
                for (int i = 0; i < BossRules.TrialFirstGear; i++)
                    bundle.Gear.Add(LootRules.RollGear(lv, rng, i == 0 ? Grade.Rare : Grade.Uncommon, context));
                bundle.Stones = BossRules.TrialFirstStones;
                LootRules.SplitGold(bundle, BossRules.TrialFirstGold, lv);
            }
            else
            {
                for (int i = 0; i < BossRules.TrialRepeatGear; i++)
                    bundle.Gear.Add(LootRules.RollGear(lv, rng, Grade.Uncommon, context));
                bundle.Stones = BossRules.TrialRepeatStones;
                LootRules.SplitGold(bundle, BossRules.TrialRepeatGold, lv);
            }
            return bundle;
        }

        /// <summary>보스 보상 씨앗: 원정 번호·보스 id(LootRules.ChestSeed와 같은 모양)에 프로필 소금을 섞는다. 같은 원정·같은 프로필이면 같은 묶음.</summary>
        public static ulong Seed(ulong profileSalt, int expedition, string bossId) =>
            LootRules.ChestSeed(expedition, "boss." + (bossId ?? "")) ^ unchecked(profileSalt * 0x9E3779B97F4A7C15UL);
    }
}
