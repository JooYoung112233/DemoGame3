using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using Demo6.Core.Progression;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>3차 초안 4-3(경험치·레벨 표·감쇠)과 4-5(스킬 1줄 4칸), 계약서 '레벨 체력' 검사.</summary>
    public sealed class ProgressionTests
    {
        [Test]
        public void LevelTableCumulativeMatchesDesign()
        {
            Assert.AreEqual(0, LevelTable.Cumulative(1));
            Assert.AreEqual(340, LevelTable.Required(1));
            Assert.AreEqual(1700, LevelTable.Cumulative(5));
            Assert.AreEqual(5880, LevelTable.Cumulative(10));
            Assert.AreEqual(14640, LevelTable.Cumulative(15));
            Assert.AreEqual(32700, LevelTable.Cumulative(20));
            Assert.AreEqual(4500, LevelTable.Required(19));
            Assert.AreEqual(0, LevelTable.Required(LevelTable.MaxLevel));
        }

        [Test]
        public void LevelTableStepsGrowAndLevelForIsConsistent()
        {
            for (int level = 1; level < LevelTable.MaxLevel; level++)
            {
                Assert.AreEqual(0, LevelTable.Required(level) % 10, "10 단위 " + level);
                Assert.AreEqual(LevelTable.Cumulative(level) + LevelTable.Required(level), LevelTable.Cumulative(level + 1));
                Assert.AreEqual(level, LevelTable.LevelFor(LevelTable.Cumulative(level)));
                Assert.AreEqual(level, LevelTable.LevelFor(LevelTable.Cumulative(level + 1) - 1));
            }
            Assert.AreEqual(1, LevelTable.LevelFor(0));
            Assert.AreEqual(LevelTable.MaxLevel, LevelTable.LevelFor(1000000));
        }

        [Test]
        public void XpUnitPerFloorMatchesDesign()
        {
            int[] expected = { 10, 12, 13, 15, 17, 20, 23, 27, 31, 35 };
            for (int floor = 1; floor <= 10; floor++)
                Assert.AreEqual(expected[floor - 1], XpRules.Unit(floor), "층 " + floor);
            Assert.AreEqual(10, XpRules.Unit(1));
            Assert.AreEqual(35, XpRules.Unit(10));
        }

        [Test]
        public void DiscoveryAndKillAmountsOnFloorOne()
        {
            Assert.AreEqual(10, XpRules.ForDiscovery(DiscoveryKind.NewCell, 1));
            Assert.AreEqual(10, XpRules.ForDiscovery(DiscoveryKind.WallLamp, 1));
            Assert.AreEqual(10, XpRules.ForDiscovery(DiscoveryKind.WoodChest, 1));
            Assert.AreEqual(20, XpRules.ForDiscovery(DiscoveryKind.IronChest, 1));
            Assert.AreEqual(30, XpRules.ForDiscovery(DiscoveryKind.Stake, 1));
            Assert.AreEqual(30, XpRules.ForDiscovery(DiscoveryKind.Shortcut, 1));
            Assert.AreEqual(30, XpRules.ForDiscovery(DiscoveryKind.Story, 1));
            Assert.AreEqual(30, XpRules.ForDiscovery(DiscoveryKind.Ability, 1));
            Assert.AreEqual(50, XpRules.ForDiscovery(DiscoveryKind.HiddenRoom, 1));
            Assert.AreEqual(100, XpRules.ForDiscovery(DiscoveryKind.Event, 1));
            Assert.AreEqual(120, XpRules.ForDiscovery(DiscoveryKind.FloorComplete, 1));
            Assert.AreEqual(0, XpRules.ForDiscovery(DiscoveryKind.Ore, 1));
            Assert.AreEqual(0, XpRules.ForDiscovery(DiscoveryKind.Safe, 1));

            Assert.AreEqual(5, XpRules.ForKill(MonsterKind.Rat, false, false, 1, 1));
            Assert.AreEqual(0, XpRules.ForKill(MonsterKind.Rat, false, true, 1, 1));
            Assert.AreEqual(20, XpRules.ForKill(MonsterKind.Archer, false, false, 1, 1));
            Assert.AreEqual(30, XpRules.ForKill(MonsterKind.Boar, false, false, 1, 1));
            Assert.AreEqual(100, XpRules.ForKill(MonsterKind.Boar, true, false, 1, 1));
            Assert.AreEqual(0, XpRules.ForKill(MonsterKind.Nest, false, false, 1, 1));
            Assert.AreEqual(40, XpRules.ForNestClear(1, 1));
        }

        [Test]
        public void KillXpDecaysThreeLevelsAboveRecommended()
        {
            Assert.AreEqual(1, XpRules.RecommendedLevel(1));
            Assert.AreEqual(16, XpRules.RecommendedLevel(10));
            Assert.AreEqual(30, XpRules.ForKill(MonsterKind.Boar, false, false, 1, 3));
            Assert.AreEqual(8, XpRules.ForKill(MonsterKind.Boar, false, false, 1, 4)); // 30 × 25% = 7.5 → 8
            Assert.AreEqual(10, XpRules.ForNestClear(1, 4));
            // 발견 경험치는 감쇠하지 않는다.
            Assert.AreEqual(10, XpRules.ForDiscovery(DiscoveryKind.NewCell, 1));
        }

        [Test]
        public void SkillEffectsAtFullRank()
        {
            Assert.AreEqual(2.6, SkillTree.WideWhirl.ValueAt(0), 1e-9);
            Assert.AreEqual(3.4, SkillTree.WideWhirl.ValueAt(4), 1e-9);
            Assert.AreEqual(0.8, SkillTree.WhirlRadiusBonus(4), 1e-9);
            Assert.AreEqual(350.0, SkillTree.SharpWind.ValueAt(0), 1e-9);
            Assert.AreEqual(450.0, SkillTree.SharpWind.ValueAt(4), 1e-9);
            Assert.AreEqual(100.0, SkillTree.WavePercentBonus(4), 1e-9);
            Assert.AreEqual(24.0, SkillTree.Finisher.ValueAt(4), 1e-9);
            Assert.AreEqual(0.24, SkillTree.FinisherDamageBonus(4), 1e-9);
            Assert.AreEqual(120, SkillTree.HpPerLevel(0));
            Assert.AreEqual(180, SkillTree.HpPerLevel(4));
            Assert.AreEqual(3.4, SkillTree.WideWhirl.ValueAt(9), 1e-9, "4랭크를 넘지 않는다");
            Assert.AreEqual("회오리 반경 3.4", SkillTree.WideWhirl.EffectText(4));
            Assert.AreEqual("검풍 피해 450%", SkillTree.SharpWind.EffectText(4));
            Assert.AreEqual("마무리 피해 +24%", SkillTree.Finisher.EffectText(4));
            Assert.AreEqual("레벨당 체력 +180", SkillTree.ToughBody.EffectText(4));
        }

        [Test]
        public void SkillRankUpNeedsPointLevelTwoAndRoom()
        {
            Assert.IsFalse(SkillTree.CanRankUp(0, 1, 1), "Lv 2부터");
            Assert.IsFalse(SkillTree.CanRankUp(0, 2, 0), "점수 없음");
            Assert.IsFalse(SkillTree.CanRankUp(4, 5, 3), "4랭크가 끝");
            Assert.IsTrue(SkillTree.CanRankUp(3, 2, 1));
            Assert.AreEqual(4, SkillTree.All.Count);
            Assert.AreEqual(SkillTree.ToughBody, SkillTree.Find("skill.tough_body"));
        }

        [Test]
        public void MaxHpAtLevelThreeWithAndWithoutToughBody()
        {
            Assert.AreEqual(2400, LevelHp.MaxHp(1, 0));
            Assert.AreEqual(2400, LevelHp.MaxHp(1, 4));
            Assert.AreEqual(2640, LevelHp.MaxHp(3, 0)); // 2,400 + 120 × 2
            Assert.AreEqual(2760, LevelHp.MaxHp(3, 4)); // 2,400 + 180 × 2 (지난 레벨에도)
            Assert.AreEqual(2400 + 120 * 19, LevelHp.MaxHp(20, 0));
        }
    }
}
