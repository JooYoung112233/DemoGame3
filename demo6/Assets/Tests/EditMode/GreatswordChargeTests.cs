using Demo6.Core.Combat;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 대검 오른쪽 클릭 기 모으기 → 놓아 베기(기획/세-무기-우클릭-소켓-1차.md 3-6~3-8, 7-4): 단계 경계, 일찍 놓기, 2.5초 저절로 놓기, 걸음 배율 넷,
    /// 놓아 베기 값, 초당 계수 1.47/1.58/1.65(상한 1.67), 치명 무게 모두 무거움, 끊김 표.
    /// </summary>
    public sealed class GreatswordChargeTests
    {
        const float Eps = 1e-6f;

        [TestCase(0f, 0)]
        [TestCase(0.39f, 0)]
        [TestCase(0.40f, 1)]
        [TestCase(0.89f, 1)]
        [TestCase(0.90f, 2)]
        [TestCase(1.49f, 2)]
        [TestCase(1.50f, 3)]
        [TestCase(2.5f, 3)]
        [TestCase(9f, 3)]
        public void LevelBoundaries(float held, int level)
        {
            Assert.AreEqual(level, GreatswordCharge.LevelAt(held));
        }

        [Test]
        public void LevelTimesAndHoldLimits()
        {
            Assert.AreEqual(0.40f, GreatswordCharge.L1, Eps);
            Assert.AreEqual(0.90f, GreatswordCharge.L2, Eps);
            Assert.AreEqual(1.50f, GreatswordCharge.L3, Eps);
            Assert.AreEqual(GreatswordCharge.L1, GreatswordCharge.LevelTime(1));
            Assert.AreEqual(GreatswordCharge.L2, GreatswordCharge.LevelTime(2));
            Assert.AreEqual(GreatswordCharge.L3, GreatswordCharge.LevelTime(3));
            Assert.AreEqual(GreatswordCharge.L1, GreatswordCharge.LevelTime(0), "범위 밖은 자름");
            Assert.AreEqual(GreatswordCharge.L3, GreatswordCharge.LevelTime(7), "범위 밖은 자름");
            Assert.AreEqual(2.5f, GreatswordCharge.MaxHold, Eps);
            Assert.AreEqual(2.2f, GreatswordCharge.WarnAt, Eps);
            Assert.Less(GreatswordCharge.L3, GreatswordCharge.WarnAt);
            Assert.Less(GreatswordCharge.WarnAt, GreatswordCharge.MaxHold);
            Assert.AreEqual(120f, GreatswordCharge.TurnDegPerSec, Eps);
            Assert.AreEqual(30f, GreatswordCharge.AimCone, Eps);
        }

        /// <summary>0.40초 전에 놓으면 0.40초까지 마저 모은 뒤 1단계로 나간다(취소 불가). 그 뒤는 놓은 순간 그 단계.</summary>
        [Test]
        public void EarlyReleaseWaitsForLevelOne()
        {
            Assert.AreEqual(1, GreatswordCharge.ReleaseLevel(0.1f));
            Assert.AreEqual(0.40f, GreatswordCharge.ReleaseStartAt(0.1f), Eps);
            Assert.AreEqual(1, GreatswordCharge.ReleaseLevel(0f));
            Assert.AreEqual(0.40f, GreatswordCharge.ReleaseStartAt(0f), Eps);
            Assert.AreEqual(1, GreatswordCharge.ReleaseLevel(0.5f));
            Assert.AreEqual(0.5f, GreatswordCharge.ReleaseStartAt(0.5f), Eps);
            Assert.AreEqual(2, GreatswordCharge.ReleaseLevel(1.2f));
            Assert.AreEqual(3, GreatswordCharge.ReleaseLevel(2.0f));
        }

        /// <summary>너무 오래 누르면 2.5초에 3단계로 저절로 놓는다(벌 없음).</summary>
        [Test]
        public void AutoReleaseAtTwoAndHalf()
        {
            Assert.IsFalse(GreatswordCharge.AutoRelease(2.49f));
            Assert.IsTrue(GreatswordCharge.AutoRelease(2.5f));
            Assert.IsTrue(GreatswordCharge.AutoRelease(3f));
            Assert.AreEqual(3, GreatswordCharge.ReleaseLevel(GreatswordCharge.MaxHold));
        }

        /// <summary>걸음 배율(4.24에 곱함): 1단계 전 0.16(0.68), 1단계 0.13(0.55), 2단계 0.11(0.47), 3단계 0.09(0.38). 굴쥐(4.2)에게서 도망칠 수 없다.</summary>
        [TestCase(0, 0.16f, 0.68f)]
        [TestCase(1, 0.13f, 0.55f)]
        [TestCase(2, 0.11f, 0.47f)]
        [TestCase(3, 0.09f, 0.38f)]
        public void MoveScaleByLevel(int level, float scale, float speed)
        {
            Assert.AreEqual(scale, GreatswordCharge.MoveScale(level), Eps);
            Assert.AreEqual(speed, 4.24f * GreatswordCharge.MoveScale(level), 0.005f);
            Assert.Less(4.24f * GreatswordCharge.MoveScale(level), 4.2f);
        }

        [Test]
        public void MoveScaleOutOfRangeClamps()
        {
            Assert.AreEqual(GreatswordCharge.MoveBefore, GreatswordCharge.MoveScale(-1), Eps);
            Assert.AreEqual(GreatswordCharge.Move3, GreatswordCharge.MoveScale(5), Eps);
        }

        /// <summary>놓아 베기 3-7 표(공격 속도 무관). 판정 순간 0.1705 / 0.17 / 0.192초.</summary>
        [Test]
        public void ReleaseStepsMatchDoc()
        {
            var s1 = GreatswordCharge.Release(1);
            var s2 = GreatswordCharge.Release(2);
            var s3 = GreatswordCharge.Release(3);

            AssertStep(s1, "모아 베기", 150f, 2.5f, 7, 0.62f, 0.275f, 150f, 1.0f, 0.15f, 0f, false, 0.06f, 0.05f, 26f);
            AssertStep(s2, "힘껏 베기", 170f, 2.8f, 9, 0.68f, 0.25f, 250f, 1.6f, 0.10f, 0.30f, true, 0.08f, 0.10f, 50f);
            AssertStep(s3, "온 힘 베기", 200f, 3.1f, 12, 0.80f, 0.24f, 380f, 2.2f, 0.05f, 0.45f, true, 0.11f, 0.16f, 85f);

            Assert.AreEqual(0.1705f, s1.duration * s1.hitMoment, 1e-5f);
            Assert.AreEqual(0.17f, s2.duration * s2.hitMoment, 1e-5f);
            Assert.AreEqual(0.192f, s3.duration * s3.hitMoment, 1e-5f);

            // 범위 밖은 자르고, 같은 인스턴스를 돌려준다.
            Assert.AreSame(s1, GreatswordCharge.Release(0));
            Assert.AreSame(s1, GreatswordCharge.Release(-3));
            Assert.AreSame(s3, GreatswordCharge.Release(9));
            Assert.AreSame(s2, GreatswordCharge.Release(2));
            // 콤보 프리셋 단계와 섞이지 않는다.
            foreach (var step in WeaponPresets.Greatsword.combo)
            {
                Assert.AreNotSame(s1, step);
                Assert.AreNotSame(s2, step);
                Assert.AreNotSame(s3, step);
            }
            // 단계가 오를수록 넓고 세고 무겁다.
            Assert.Less(s1.hitPercent, s2.hitPercent);
            Assert.Less(s2.hitPercent, s3.hitPercent);
            Assert.Less(s1.poiseDamage, s2.poiseDamage);
            Assert.Less(s2.poiseDamage, s3.poiseDamage);
            Assert.Less(s1.arcDeg, s2.arcDeg);
            Assert.Less(s2.arcDeg, s3.arcDeg);
            // 새 무기 고유 깃발은 없다(마지막 X·끊기 따위는 난사 몫).
            foreach (var s in new[] { s1, s2, s3 })
            {
                Assert.AreEqual(1, s.hits);
                Assert.AreEqual(ComboShape.Arc, s.shape);
                Assert.IsFalse(s.staggers || s.wallBreak || s.pull || s.backstab || s.inertia || s.blunt, s.name);
            }
        }

        static void AssertStep(ComboStep s, string name, float arc, float size, int maxTargets, float duration, float hitMoment, float percent,
            float knockback, float moveScale, float advance, bool finisher, float hitStop, float shake, float poise)
        {
            Assert.AreEqual(name, s.name);
            Assert.AreEqual(arc, s.arcDeg, Eps, name + " 부채꼴");
            Assert.AreEqual(size, s.size, Eps, name + " 범위");
            Assert.AreEqual(maxTargets, s.maxTargets, name + " 최대");
            Assert.AreEqual(duration, s.duration, Eps, name + " 길이");
            Assert.AreEqual(hitMoment, s.hitMoment, Eps, name + " 판정 비율");
            Assert.AreEqual(percent, s.hitPercent, Eps, name + " 배율");
            Assert.AreEqual(knockback, s.knockback, Eps, name + " 넉백");
            Assert.AreEqual(moveScale, s.moveScale, Eps, name + " 걸음");
            Assert.AreEqual(advance, s.advance, Eps, name + " 내딛기");
            Assert.AreEqual(finisher, s.finisher, name + " 마무리");
            Assert.AreEqual(hitStop, s.hitStop, Eps, name + " 히트스톱");
            Assert.AreEqual(shake, s.shake, Eps, name + " 흔들림");
            Assert.AreEqual(poise, s.poiseDamage, Eps, name + " 버팀");
        }

        /// <summary>단일 대상 초당 계수(모은 시간 포함) 1.47 / 1.58 / 1.65, 상한 1.45 × 1.15 = 1.67. 치명 포함(대검 기대 치명 1.05) 1.51 / 1.62 / 1.69.</summary>
        [TestCase(1, 1.47, 1.51)]
        [TestCase(2, 1.58, 1.62)]
        [TestCase(3, 1.65, 1.69)]
        public void CoefficientPerSecondUnderCap(int level, double expected, double critAdjusted)
        {
            double c = GreatswordCharge.CoefficientPerSecond(level);
            Assert.AreEqual(expected, c, 0.005);
            Assert.LessOrEqual(c, GreatswordCharge.CoefficientCap);
            Assert.AreEqual(1.6675, GreatswordCharge.CoefficientCap, 1e-9);
            // 모아서 놓는 쪽이 콤보(1.452)보다 낫되, 단계가 오를수록 조금씩만 낫다.
            Assert.Greater(c, WeaponPresets.Greatsword.SingleTargetCoefficient);
            double crit = 1 + 0.05 * (2.0 - 1);
            Assert.AreEqual(critAdjusted, c * crit / 1.025, 0.005);
        }

        [Test]
        public void CoefficientRisesWithLevel()
        {
            Assert.Less(GreatswordCharge.CoefficientPerSecond(1), GreatswordCharge.CoefficientPerSecond(2));
            Assert.Less(GreatswordCharge.CoefficientPerSecond(2), GreatswordCharge.CoefficientPerSecond(3));
            // 초당 버팀(모은 시간 포함) 25.5 / 31.6 / 37.0.
            double[] poise = { 25.5, 31.6, 37.0 };
            for (int level = 1; level <= 3; level++)
            {
                var s = GreatswordCharge.Release(level);
                Assert.AreEqual(poise[level - 1], s.poiseDamage / (GreatswordCharge.LevelTime(level) + s.duration), 0.05, level + "단계 초당 버팀");
            }
        }

        /// <summary>
        /// 놓아 베기는 마무리 일격 피해 보너스를 받지 않는다(0-3의 22): 버팀 룬으로 끊기지 않아도 마무리 일격 4랭크(+24%)에서 초당 계수가 상한 1.67 안.
        /// 받았다면 3단계가 380 × 1.24 ÷ 2.3 = 2.05였다. 2·3단계는 그대로 마무리(무너짐 처형)다.
        /// </summary>
        [Test]
        public void FinisherBonusDoesNotLiftReleaseOverCap()
        {
            Assert.IsFalse(GreatswordCharge.TakesFinisherDamageBonus);
            for (int level = 1; level <= 3; level++)
            {
                foreach (double bonus in new[] { 0.0, 0.06, 0.24, 1.0 })
                {
                    double c = GreatswordCharge.CoefficientPerSecond(level, bonus);
                    Assert.LessOrEqual(c, GreatswordCharge.CoefficientCap, level + "단계 보너스 " + bonus);
                    Assert.AreEqual(GreatswordCharge.CoefficientPerSecond(level), c, 1e-12);
                }
            }
            Assert.IsFalse(GreatswordCharge.Release(1).finisher);
            Assert.IsTrue(GreatswordCharge.Release(2).finisher);
            Assert.IsTrue(GreatswordCharge.Release(3).finisher);
        }

        /// <summary>치명(대검 치명 피해 2.0): 300 / 500 / 760%로 모두 무거움.</summary>
        [Test]
        public void ReleaseCritsAreAllHeavy()
        {
            int[] weights = { 3000, 5000, 7600 };
            for (int level = 1; level <= 3; level++)
            {
                var s = GreatswordCharge.Release(level);
                Assert.AreEqual(weights[level - 1], CritTiers.WeightPermille(s.hitPercent, 2.0f));
                Assert.AreEqual(CritTier.Heavy, CritTiers.Of(s.hitPercent, 2.0f), s.name);
            }
        }

        /// <summary>끊김 표(3-8): 모으는 동안·놓은 뒤 판정 전은 끊김, 판정 뒤 회수는 보통 피격, 버팀 룬이면 언제나 안 끊김.</summary>
        [Test]
        public void BreaksOnHitTable()
        {
            Assert.IsTrue(GreatswordCharge.BreaksOnHit(WeaponActPhase.Charging, 0, false));
            Assert.IsTrue(GreatswordCharge.BreaksOnHit(WeaponActPhase.Release, 0, false), "놓은 뒤 판정 전");
            Assert.IsFalse(GreatswordCharge.BreaksOnHit(WeaponActPhase.Release, 1, false), "판정 뒤 회수");
            Assert.IsFalse(GreatswordCharge.BreaksOnHit(WeaponActPhase.Charging, 0, true), "버팀 룬");
            Assert.IsFalse(GreatswordCharge.BreaksOnHit(WeaponActPhase.Release, 0, true), "버팀 룬");
            Assert.IsFalse(GreatswordCharge.BreaksOnHit(WeaponActPhase.Flinch, 0, false), "이미 끊김");
            Assert.IsFalse(GreatswordCharge.BreaksOnHit(WeaponActPhase.None, 0, false));
            Assert.IsFalse(GreatswordCharge.BreaksOnHit(WeaponActPhase.Hold, 0, false), "방패 단계는 대상 아님");
            Assert.AreEqual(0.45f, WeaponActCommon.FlinchFor(WeaponActKind.Charge), Eps);
        }

        [Test]
        public void GlowAndPitchTables()
        {
            CollectionAssert.AreEqual(new[] { 0.15f, 0.35f, 0.60f, 0.90f }, GreatswordCharge.GlowByLevel);
            CollectionAssert.AreEqual(new[] { 0.8f, 0.95f, 1.1f }, GreatswordCharge.LevelPitch);
            Assert.AreEqual(0.4f, GreatswordCharge.GlowFlash, Eps);
            Assert.AreEqual(0.08f, GreatswordCharge.GlowFlashTime, Eps);
        }
    }
}
