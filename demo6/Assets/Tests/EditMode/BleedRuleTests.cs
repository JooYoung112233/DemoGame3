using Demo6.Core.Combat;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 도끼 출혈(전투·보스·무기 다듬기 1차 2-3, BleedRule): 3초 동안 0.5초마다 6틱, 틱 = 합 ÷ 6(50%면 8.333…%), 치명 없음.
    /// 출혈은 계수에 넣는다(도끼 1.526, 빼면 1.325로 1.45~1.60 띠 밖). 한 방 세기(카드)에는 넣지 않는다.
    /// </summary>
    public sealed class BleedRuleTests
    {
        [Test]
        public void SixTicksEveryHalfSecondForThreeSeconds()
        {
            Assert.AreEqual(6, BleedRule.Ticks);
            Assert.AreEqual(0.5f, BleedRule.TickInterval);
            Assert.AreEqual(3f, BleedRule.Duration);
            Assert.AreEqual(BleedRule.Duration, BleedRule.Ticks * BleedRule.TickInterval, 1e-6f, "마지막 틱이 3초째");
            Assert.IsFalse(BleedRule.CanCrit);
        }

        [Test]
        public void TickIsTotalOverSix()
        {
            Assert.AreEqual(50.0 / 6.0, BleedRule.TickPercent(50), 1e-12);
            Assert.AreEqual(8.333, BleedRule.TickPercent(50), 0.0005);
            Assert.AreEqual(50.0, BleedRule.TickPercent(50) * BleedRule.Ticks, 1e-9, "틱 합 = 합");
            Assert.AreEqual(0.0, BleedRule.TickPercent(0), 1e-12);
            // 1층 기준 공격 200: 틱 16.67 → 17(반올림), 여섯 틱 102 ≈ 공격의 50%.
            int tick = DamageMath.ToMonster(200, BleedRule.TickPercent(50), false, 1.0, 1.0);
            Assert.AreEqual(17, tick);
            Assert.AreEqual(102, tick * BleedRule.Ticks);
            // 보스 피해 +10%는 받는다.
            Assert.AreEqual(18, DamageMath.ToMonster(200, BleedRule.TickPercent(50), false, 1.0, 1.0, 0, 0, false, 0.1, true));
        }

        [Test]
        public void AxeCoefficientCountsBleedButCardDoesNot()
        {
            var axe = WeaponPresets.Axe;
            var split = axe.combo[2];
            Assert.AreEqual(50f, split.bleedPercent);
            Assert.AreEqual(190f, split.PercentPerTarget, 1e-4f, "쪼개기 140 + 출혈 50");
            Assert.AreEqual(1.526, axe.SingleTargetCoefficient, 0.0015);
            Assert.That(axe.SingleTargetCoefficient, Is.InRange(1.45, 1.60));

            double withoutBleed = 0;
            foreach (var s in axe.combo) withoutBleed += s.hits * s.hitPercent;
            withoutBleed = withoutBleed / 100.0 / axe.CycleSeconds;
            Assert.AreEqual(1.325, withoutBleed, 0.0015, "출혈을 빼면 띠 밖");
            Assert.Less(withoutBleed, 1.45);

            Assert.AreEqual(110.0, axe.AverageHitPercent, 1e-9, "한 방 세기는 출혈을 빼고 (95 + 95 + 140) ÷ 3");
            // 출혈은 도끼 ③에만.
            Assert.AreEqual(0f, axe.combo[0].bleedPercent);
            Assert.AreEqual(0f, axe.combo[1].bleedPercent);
        }

        [Test]
        public void BleedFreeStepKeepsOldPercent()
        {
            foreach (var w in WeaponPresets.All)
                foreach (var s in w.combo)
                    if (s.bleedPercent == 0f) Assert.AreEqual(s.hits * s.hitPercent, s.PercentPerTarget, w.displayName + " " + s.name);
        }
    }
}
