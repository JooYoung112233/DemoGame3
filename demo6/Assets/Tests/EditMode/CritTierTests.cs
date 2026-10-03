using Demo6.Core.Combat;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>치명 연출 3단계(장비 문서 3-4): 한 방 무게 = 그 타 배율 × 치명 피해, 가벼움 &lt; 100% ≤ 보통 &lt; 200% ≤ 무거움, 무거움은 0.5초에 한 번.</summary>
    public sealed class CritTierTests
    {
        /// <summary>회오리 타 배율(PlayerController.WhirlPercent 90%).</summary>
        const double WhirlPercent = 90;
        /// <summary>검풍 배율(350%, 장검 치명 피해 1.6이면 560%).</summary>
        const double WavePercent = 350;

        [TestCase(90, 1.6, CritTier.Normal)] // 장검 횡베기 144% 보통
        [TestCase(140, 1.6, CritTier.Heavy)] // 장검 찌르기 224% 무거움
        [TestCase(115, 2.0, CritTier.Heavy)] // 대검 큰 베기 230% 무거움
        [TestCase(220, 2.0, CritTier.Heavy)] // 대검 내려찍기 440% 무거움
        [TestCase(52, 1.3, CritTier.Light)] // 쌍검 연타 68% 가벼움
        [TestCase(34, 1.3, CritTier.Light)] // 쌍검 회전베기 44% 가벼움
        [TestCase(WhirlPercent, 1.6, CritTier.Normal)] // 회오리 1타 144% 보통
        [TestCase(WavePercent, 1.6, CritTier.Heavy)] // 검풍 560% 무거움
        [TestCase(90, 2.4, CritTier.Heavy)] // 치명 피해 240% 횡베기 216% 무거움
        public void DocExamples(double hitPercent, double critDamage, CritTier tier)
        {
            Assert.AreEqual(tier, CritTiers.Of(hitPercent, critDamage));
        }

        [Test]
        public void WeightsInPermille()
        {
            Assert.AreEqual(1440, CritTiers.WeightPermille(90, 1.6));
            Assert.AreEqual(2240, CritTiers.WeightPermille(140, 1.6));
            Assert.AreEqual(4400, CritTiers.WeightPermille(220, 2.0));
            Assert.AreEqual(676, CritTiers.WeightPermille(52, 1.3));
            Assert.AreEqual(442, CritTiers.WeightPermille(34, 1.3));
            Assert.AreEqual(5600, CritTiers.WeightPermille(WavePercent, 1.6));
            Assert.AreEqual(2160, CritTiers.WeightPermille(90, 2.4));
            // 게임 쪽 float 값(1.6f 등)을 그대로 넣어도 같은 무게.
            Assert.AreEqual(1440, CritTiers.WeightPermille(90f, 1.6f));
            Assert.AreEqual(676, CritTiers.WeightPermille(52f, 1.3f));
        }

        [Test]
        public void ComboDataWithStartingCritDamage()
        {
            // 무기 종류 고유 치명 피해: 장검 1.6, 대검 2.0, 쌍검 1.3.
            var l = WeaponPresets.Longsword.combo;
            Assert.AreEqual(CritTier.Normal, CritTiers.Of(l[0].hitPercent, 1.6f));
            Assert.AreEqual(CritTier.Normal, CritTiers.Of(l[1].hitPercent, 1.6f));
            Assert.AreEqual(CritTier.Heavy, CritTiers.Of(l[2].hitPercent, 1.6f));
            foreach (var step in WeaponPresets.Greatsword.combo)
                Assert.AreEqual(CritTier.Heavy, CritTiers.Of(step.hitPercent, 2.0f), "대검 " + step.name);
            foreach (var step in WeaponPresets.Twinblades.combo)
                Assert.AreEqual(CritTier.Light, CritTiers.Of(step.hitPercent, 1.3f), "쌍검 " + step.name);
        }

        [TestCase(999, CritTier.Light)]
        [TestCase(1000, CritTier.Normal)]
        [TestCase(1999, CritTier.Normal)]
        [TestCase(2000, CritTier.Heavy)]
        [TestCase(0, CritTier.Light)]
        public void Boundaries(int weightPermille, CritTier tier)
        {
            Assert.AreEqual(tier, CritTiers.OfWeight(weightPermille));
        }

        [Test]
        public void BoundariesThroughHitPercent()
        {
            Assert.AreEqual(CritTier.Normal, CritTiers.Of(100, 1.0));
            Assert.AreEqual(CritTier.Light, CritTiers.Of(99.9, 1.0));
            Assert.AreEqual(CritTier.Heavy, CritTiers.Of(125, 1.6));
            Assert.AreEqual(CritTier.Heavy, CritTiers.Of(125f, 1.6f));
            Assert.AreEqual(CritTier.Normal, CritTiers.Of(124, 1.6));
        }

        [Test]
        public void MaxPicksHeaviest()
        {
            Assert.AreEqual(CritTier.Heavy, CritTiers.Max(CritTier.Light, CritTier.Heavy));
            Assert.AreEqual(CritTier.Heavy, CritTiers.Max(CritTier.Heavy, CritTier.Normal));
            Assert.AreEqual(CritTier.Light, CritTiers.Max(CritTier.None, CritTier.Light));
            Assert.AreEqual(CritTier.Normal, CritTiers.Max(CritTier.Normal, CritTier.Normal));
            Assert.AreEqual(CritTier.None, CritTiers.Max(CritTier.None, CritTier.None));
            // 한 동작에서 여러 대상: 쌍검 연타(가벼움)와 같은 동작의 무거움이 섞이면 무거움 하나.
            var tier = CritTier.None;
            foreach (var t in new[] { CritTier.Light, CritTier.Normal, CritTier.Heavy, CritTier.Light }) tier = CritTiers.Max(tier, t);
            Assert.AreEqual(CritTier.Heavy, tier);
        }

        [Test]
        public void HeavyOnlyOncePerHalfSecond()
        {
            double last = CritTiers.NoHeavyYet;
            Assert.AreEqual(double.NegativeInfinity, last);
            Assert.AreEqual(CritTier.Heavy, CritTiers.Gate(CritTier.Heavy, 0.0, ref last, false), "처음은 무거움");
            Assert.AreEqual(0.0, last);
            Assert.AreEqual(CritTier.Normal, CritTiers.Gate(CritTier.Heavy, 0.3, ref last, false), "0.5초 안은 보통");
            Assert.AreEqual(0.0, last, "낮춘 무거움은 시각을 바꾸지 않음");
            Assert.AreEqual(CritTier.Normal, CritTiers.Gate(CritTier.Heavy, 0.49, ref last, false));
            Assert.AreEqual(CritTier.Heavy, CritTiers.Gate(CritTier.Heavy, 0.5, ref last, false), "0.5초가 지나면 다시 무거움");
            Assert.AreEqual(0.5, last);
        }

        [Test]
        public void FinisherIsExemptAndResetsTheClock()
        {
            double last = CritTiers.NoHeavyYet;
            Assert.AreEqual(CritTier.Heavy, CritTiers.Gate(CritTier.Heavy, 1.0, ref last, false));
            Assert.AreEqual(CritTier.Heavy, CritTiers.Gate(CritTier.Heavy, 1.1, ref last, true), "마무리는 0.5초 안이어도 무거움");
            Assert.AreEqual(1.1, last);
            Assert.AreEqual(CritTier.Normal, CritTiers.Gate(CritTier.Heavy, 1.5, ref last, false), "마무리 무거움 뒤 0.5초 안");
            Assert.AreEqual(CritTier.Heavy, CritTiers.Gate(CritTier.Heavy, 1.7, ref last, false));
        }

        [Test]
        public void GateLeavesOtherTiersAlone()
        {
            double last = 10.0;
            Assert.AreEqual(CritTier.Normal, CritTiers.Gate(CritTier.Normal, 10.1, ref last, false));
            Assert.AreEqual(CritTier.Light, CritTiers.Gate(CritTier.Light, 10.1, ref last, true));
            Assert.AreEqual(CritTier.None, CritTiers.Gate(CritTier.None, 10.1, ref last, false));
            Assert.AreEqual(10.0, last);
        }
    }
}
