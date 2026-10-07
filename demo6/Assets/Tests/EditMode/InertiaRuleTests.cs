using System;
using Demo6.Core.Combat;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 사슬 철퇴 관성(전투·보스·무기 다듬기 1차 2-3, InertiaRule): 마무리가 끝난 뒤 0.5초 안에 다시 치면 ①② 동작 길이 × 0.9(계수 1.46 → 1.55).
    /// 스킬·구르기를 쓰면 끊긴다. 배율 1이면 SwingTiming.Plan이 예전과 비트까지 같고, 줄어든 길이로도 0~400‰ 규칙 ①~⑤를 지킨다.
    /// </summary>
    public sealed class InertiaRuleTests
    {
        const float Eps = 1e-5f;

        static int Bits(float f) => BitConverter.ToInt32(BitConverter.GetBytes(f), 0);

        [Test]
        public void WindowIsHalfSecondAfterFinisher()
        {
            Assert.AreEqual(0.5f, InertiaRule.Window);
            Assert.AreEqual(0.9f, InertiaRule.DurationScale);
            Assert.IsTrue(InertiaRule.Active(true, 0f, false), "마무리 끝 바로");
            Assert.IsTrue(InertiaRule.Active(true, 0.5f, false), "0.5초 경계 안");
            Assert.IsFalse(InertiaRule.Active(true, 0.501f, false), "0.5초 넘음");
            Assert.IsFalse(InertiaRule.Active(true, -0.01f, false), "마무리가 아직 안 끝남");
            Assert.IsFalse(InertiaRule.Active(false, 0.2f, false), "관성 단계가 아님(마무리)");
            Assert.IsFalse(InertiaRule.Active(true, 0.2f, true), "스킬·구르기로 끊김");
        }

        [Test]
        public void ScaleIsExactlyOneWhenBroken()
        {
            Assert.AreEqual(0.9f, InertiaRule.Scale(true, 0.3f, false));
            Assert.AreEqual(Bits(1f), Bits(InertiaRule.Scale(true, 0.3f, true)), "끊기면 정확히 1");
            Assert.AreEqual(Bits(1f), Bits(InertiaRule.Scale(true, 0.6f, false)));
            Assert.AreEqual(Bits(1f), Bits(InertiaRule.Scale(false, 0.1f, false)));
        }

        /// <summary>Plan(step, a, 1f)는 Plan(step, a)와 비트까지 같다(아홉 무기 모든 단계, 공속 −50~1000‰). 0 이하 배율도 무시한다.</summary>
        [Test]
        public void ScaleOneIsBitIdenticalToPlainPlan()
        {
            int checkedSteps = 0;
            foreach (var weapon in WeaponPresets.All)
                foreach (var step in weapon.combo)
                    foreach (int a in new[] { -50, 0, 50, 100, 150, 200, 250, 300, 350, 400, 1000 })
                    {
                        var plain = SwingTiming.Plan(step, a);
                        foreach (float scale in new[] { 1f, 0f, -1f })
                        {
                            var scaled = SwingTiming.Plan(step, a, scale);
                            string where = weapon.displayName + " " + step.name + " 공속 " + a + " 배율 " + scale;
                            Assert.AreEqual(Bits(plain.Duration), Bits(scaled.Duration), where);
                            Assert.AreEqual(Bits(plain.FirstHit), Bits(scaled.FirstHit), where);
                            Assert.AreEqual(Bits(plain.CarryCap), Bits(scaled.CarryCap), where);
                            for (int k = 0; k < step.hits; k++) Assert.AreEqual(Bits(plain.HitTime(k)), Bits(scaled.HitTime(k)), where);
                        }
                        checkedSteps++;
                    }
            Assert.AreEqual(28 * 11, checkedSteps);
            var none = SwingTiming.Plan(null, 300, 0.9f);
            Assert.AreEqual(SwingTiming.MinDuration, none.Duration);
        }

        [Test]
        public void FlailInertiaCoefficientIsAboutOnePointFiveFive()
        {
            var flail = WeaponPresets.Flail;
            Assert.AreEqual(1.458, flail.SingleTargetCoefficient, 0.0015, "관성 없이");
            double cycle = 0, percent = 0;
            foreach (var step in flail.combo)
            {
                cycle += SwingTiming.Plan(step, 0, InertiaRule.Scale(step.inertia, 0.2f, false)).Duration;
                percent += step.PercentPerTarget;
            }
            Assert.AreEqual(2.77, cycle, 1e-5, "0.855 + 0.765 + 1.15");
            double coefficient = percent / 100.0 / cycle;
            Assert.AreEqual(1.55, coefficient, 0.01);
            Assert.That(coefficient, Is.InRange(1.45, 1.60), "관성이 붙어도 단일 대상 띠 안");
        }

        /// <summary>관성으로 줄어든 단계도 0~400‰에서 규칙 ①~⑤(회수 0.08, 첫 판정 0.1, 길이 ÷ s, 판정 비율, 찌르기 멈춤·내려찍기 충격)와 이월 상한을 지킨다.</summary>
        [Test]
        public void InertiaStepsKeepRulesUpToFourHundred()
        {
            int cases = 0;
            foreach (var weapon in WeaponPresets.All)
                foreach (var step in weapon.combo)
                {
                    if (!step.inertia) continue;
                    float d = Math.Max(SwingTiming.MinDuration, step.duration * InertiaRule.DurationScale);
                    for (int a = 0; a <= 400; a += 50)
                    {
                        float s = SwingTiming.SpeedFactor(a);
                        var plan = SwingTiming.Plan(step, a, InertiaRule.DurationScale);
                        string where = weapon.displayName + " " + step.name + " 관성 공속 " + a + "‰";
                        Assert.That(plan.LastHit + SwingTiming.MinRecovery, Is.LessThanOrEqualTo(plan.Duration + Eps), where + " ① 회수");
                        Assert.That(plan.FirstHit, Is.GreaterThanOrEqualTo(0.1f), where + " ② 첫 판정");
                        Assert.AreEqual(d / s, plan.Duration, 1e-6f, where + " ③ 길이(규칙 4가 걸리지 않음)");
                        Assert.That(plan.FirstHit / plan.Duration, Is.GreaterThanOrEqualTo(step.hitMoment - Eps), where + " ④ 판정 비율");
                        Assert.AreEqual(d * step.hitMoment / (1f + a / 2000f), plan.FirstHit, 1e-6f, where + " 규칙 2");
                        if (step.shape == ComboShape.Line)
                            Assert.That(plan.FirstHit + 0.14f, Is.LessThanOrEqualTo(plan.Duration + Eps), where + " ⑤ 찌르기 멈춤");
                        if (step.shape == ComboShape.Circle && step.centerOffset > 0.01f)
                            Assert.That(plan.FirstHit + 0.24f, Is.LessThanOrEqualTo(plan.Duration + Eps), where + " ⑤ 내려찍기 충격");
                        Assert.AreEqual(plan.Duration * 0.5f, plan.CarryCap, 1e-7f, where + " 이월 상한");
                        cases++;
                    }
                }
            // 사슬 철퇴 ①② × 9 공속.
            Assert.AreEqual(2 * 9, cases);
        }
    }
}
