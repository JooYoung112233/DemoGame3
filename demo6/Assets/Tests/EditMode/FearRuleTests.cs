using Demo6.Core.Combat;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>무리 공포(기획/전투-보스-무기-다듬기-1차.md 4-2 [2], FearRule).</summary>
    public sealed class FearRuleTests
    {
        static TargetClass Of(MonsterKind kind, WeightClass weight, bool elite = false, bool boss = false, bool dummy = false) =>
            new TargetClass(kind, weight, elite, false, boss, dummy);

        [Test]
        public void HeavyEliteAndBossDeathsTrigger()
        {
            Assert.IsTrue(FearRule.Triggers(Of(MonsterKind.Boar, WeightClass.Heavy)), "멧돼지");
            Assert.IsTrue(FearRule.Triggers(Of(MonsterKind.Boar, WeightClass.Heavy, elite: true)), "정예 멧돼지");
            Assert.IsTrue(FearRule.Triggers(Of(MonsterKind.Ogre, WeightClass.Heavy, boss: true)), "보스");
            // 무게와 상관없이 정예·보스면 부른다.
            Assert.IsTrue(FearRule.Triggers(Of(MonsterKind.Archer, WeightClass.Medium, elite: true)));
            Assert.IsTrue(FearRule.Triggers(Of(MonsterKind.Ogre, WeightClass.Medium, boss: true)));
        }

        [Test]
        public void SmallFoesNestAndDummiesDoNotTrigger()
        {
            Assert.IsFalse(FearRule.Triggers(Of(MonsterKind.Rat, WeightClass.Light)), "굴쥐");
            Assert.IsFalse(FearRule.Triggers(Of(MonsterKind.Archer, WeightClass.Medium)), "궁수");
            // 둥지는 Enemy에서 무거움이지만 공포를 부르지 않는다.
            Assert.IsFalse(FearRule.Triggers(Of(MonsterKind.Nest, WeightClass.Heavy)), "둥지");
            Assert.IsFalse(FearRule.Triggers(Of(MonsterKind.Rat, WeightClass.Light, dummy: true)), "쥐 허수아비");
            Assert.IsFalse(FearRule.Triggers(Of(MonsterKind.Boar, WeightClass.Heavy, dummy: true)), "나무 허수아비(무거움)");
        }

        [Test]
        public void SecondsForEachCase()
        {
            Assert.AreEqual(1.5f, FearRule.Seconds(false, false, false), 1e-6f, "무거운 적·정예 처치");
            Assert.AreEqual(2.5f, FearRule.Seconds(true, false, false), 1e-6f, "처형");
            Assert.AreEqual(4.0f, FearRule.Seconds(false, true, false), 1e-6f, "무리 거느린 정예의 졸개");
            Assert.AreEqual(4.0f, FearRule.Seconds(true, true, false), 1e-6f, "졸개는 처형이어도 4초");
            Assert.AreEqual(1.0f, FearRule.Seconds(false, false, true), 1e-6f, "둥지 굴쥐");
            Assert.AreEqual(1.0f, FearRule.Seconds(true, false, true), 1e-6f, "둥지 굴쥐는 처형이어도 짧게");
        }

        [Test]
        public void ConstantsMatchDocument()
        {
            Assert.AreEqual(6f, FearRule.Radius, 1e-6f);
            Assert.AreEqual(0.5f, FearRule.PackVanishChance, 1e-6f);
            Assert.AreEqual(1.1f, FearRule.FleeSpeedScale, 1e-6f);
            Assert.IsTrue(FearRule.DefaultOn);
        }
    }
}
