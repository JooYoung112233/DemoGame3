using Demo6.Core.Combat;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 쌍검 오른쪽 클릭 난사(기획/세-무기-우클릭-소켓-1차.md 4-6, 0-3의 8, 7-4): 판정 8개 차례·자리, 합 266%·버팀 24,
    /// 초당 2.42가 새 쌍검 콤보 계수의 1.4~1.6배, 6초 단위 +10% 이하, 치명(1.3) 모두 가벼움, 재사용 6/3초, 재사용 감소 안 받음.
    /// </summary>
    public sealed class TwinFlurryRuleTests
    {
        const float Eps = 1e-6f;

        [Test]
        public void HitTimesAscendAndFitInDuration()
        {
            Assert.AreEqual(1.10f, TwinFlurry.Duration, Eps);
            Assert.AreEqual(8, TwinFlurry.HitCount);
            Assert.AreEqual(TwinFlurry.HitTimes.Length, TwinFlurry.HitCount);
            for (int i = 1; i < TwinFlurry.HitCount; i++)
                Assert.Greater(TwinFlurry.HitTimes[i], TwinFlurry.HitTimes[i - 1], "오름차순 " + i);
            Assert.GreaterOrEqual(TwinFlurry.HitTimes[0], 0.1f, "첫 판정 ≥ 0.1");
            Assert.LessOrEqual(TwinFlurry.HitTimes[TwinFlurry.HitCount - 1] + SwingTiming.MinRecovery, TwinFlurry.Duration + Eps, "마지막 + 0.08 ≤ 1.10");
            // 작은 타 간격 0.09, 마지막 X는 0.12초 쉰 뒤.
            for (int i = 1; i < TwinFlurry.HitCount - 1; i++)
                Assert.AreEqual(0.09f, TwinFlurry.HitTimes[i] - TwinFlurry.HitTimes[i - 1], 1e-5f, "작은 타 간격 " + i);
            Assert.AreEqual(0.12f, TwinFlurry.HitTimes[7] - TwinFlurry.HitTimes[6], 1e-5f);
            CollectionAssert.AreEqual(new[] { 0.18f, 0.27f, 0.36f, 0.45f, 0.54f, 0.63f, 0.72f, 0.84f }, TwinFlurry.HitTimes);
        }

        [Test]
        public void HitsDueCountsHitTimes()
        {
            Assert.AreEqual(0, TwinFlurry.HitsDue(0f));
            Assert.AreEqual(0, TwinFlurry.HitsDue(0.179f));
            Assert.AreEqual(1, TwinFlurry.HitsDue(0.18f));
            Assert.AreEqual(4, TwinFlurry.HitsDue(0.5f));
            Assert.AreEqual(7, TwinFlurry.HitsDue(0.839f));
            Assert.AreEqual(8, TwinFlurry.HitsDue(0.84f));
            Assert.AreEqual(8, TwinFlurry.HitsDue(TwinFlurry.Duration));
            Assert.AreEqual(8, TwinFlurry.HitsDue(5f));
        }

        [Test]
        public void SmallAndFinalSteps()
        {
            for (int i = 0; i < TwinFlurry.HitCount; i++)
            {
                bool final = i == TwinFlurry.HitCount - 1;
                Assert.AreEqual(final, TwinFlurry.IsFinal(i));
                Assert.AreSame(final ? TwinFlurry.Final : TwinFlurry.Small, TwinFlurry.StepOf(i));
            }
            // 오른손 1·3·5·7번째, 왼손 2·4·6번째.
            CollectionAssert.AreEqual(new[] { true, false, true, false, true, false, true },
                new[] { TwinFlurry.RightHand(0), TwinFlurry.RightHand(1), TwinFlurry.RightHand(2), TwinFlurry.RightHand(3), TwinFlurry.RightHand(4), TwinFlurry.RightHand(5), TwinFlurry.RightHand(6) });

            // 작은 타: 앞 부채꼴 100°, 1.9, 4명, 28%, 버팀 2, 넉백 0, 히트스톱 0, 준비 동작을 끊지 않음.
            var s = TwinFlurry.Small;
            Assert.AreEqual(ComboShape.Arc, s.shape);
            Assert.AreEqual(100f, s.arcDeg, Eps);
            Assert.AreEqual(1.9f, s.size, Eps);
            Assert.AreEqual(4, s.maxTargets);
            Assert.AreEqual(28f, s.hitPercent, Eps);
            Assert.AreEqual(2f, s.poiseDamage, Eps);
            Assert.AreEqual(0f, s.knockback, Eps);
            Assert.AreEqual(0f, s.hitStop, Eps);
            Assert.AreEqual(0f, s.shake, Eps);
            Assert.IsFalse(s.staggers);
            Assert.IsFalse(s.finisher);
            Assert.AreEqual(1, s.hits);

            // 마지막 X: 부채꼴 120°, 2.0, 5명, 70%, 버팀 10, 넉백 0.8, 히트스톱 0.06, 흔들림 0.05, 끊기(창 끊어 찌르기와 같은 처리), 무너짐 처형 없음.
            var f = TwinFlurry.Final;
            Assert.AreEqual(ComboShape.Arc, f.shape);
            Assert.AreEqual(120f, f.arcDeg, Eps);
            Assert.AreEqual(2.0f, f.size, Eps);
            Assert.AreEqual(5, f.maxTargets);
            Assert.AreEqual(70f, f.hitPercent, Eps);
            Assert.AreEqual(10f, f.poiseDamage, Eps);
            Assert.AreEqual(0.8f, f.knockback, Eps);
            Assert.AreEqual(0.06f, f.hitStop, Eps);
            Assert.AreEqual(0.05f, f.shake, Eps);
            Assert.IsTrue(f.staggers);
            Assert.IsFalse(f.finisher, "마무리 보너스·무너짐 처형 없음");
            Assert.AreEqual(1, f.hits);

            // 두 단계 모두 난사 걸음 배율을 쓰고, 칼 그림 시각은 판정 시각과 같다.
            Assert.AreEqual(TwinFlurry.MoveScale, s.moveScale, Eps);
            Assert.AreEqual(TwinFlurry.MoveScale, f.moveScale, Eps);
            Assert.AreEqual(TwinFlurry.HitTimes[0], s.duration * s.hitMoment, 1e-5f);
            Assert.AreEqual(TwinFlurry.HitTimes[7], f.duration * f.hitMoment, 1e-5f);
            // 자동 조준 사거리는 작은 타 거리와 같다.
            Assert.AreEqual(TwinFlurry.AimRange, s.size, Eps);
        }

        /// <summary>적 하나당 합계 266%(28 × 7 + 70), 버팀 24(2 × 7 + 10).</summary>
        [Test]
        public void TotalsPerTarget()
        {
            Assert.AreEqual(266f, TwinFlurry.TotalPercent, 1e-3f);
            Assert.AreEqual(24f, TwinFlurry.TotalPoise, 1e-3f);
            float percent = 0f;
            for (int i = 0; i < TwinFlurry.HitCount; i++) percent += TwinFlurry.PercentOf(i);
            Assert.AreEqual(TwinFlurry.TotalPercent, percent, 1e-4f);
            Assert.AreEqual(28f, TwinFlurry.PercentOf(0), Eps);
            Assert.AreEqual(70f, TwinFlurry.PercentOf(7), Eps);
            Assert.AreEqual(2f, TwinFlurry.PoiseOf(3), Eps);
            Assert.AreEqual(10f, TwinFlurry.PoiseOf(7), Eps);
            // 버팀 초당 21.8(약함).
            Assert.AreEqual(21.8, TwinFlurry.TotalPoise / TwinFlurry.Duration, 0.05);
        }

        /// <summary>초당 2.42는 새 쌍검 콤보 계수(1.588)의 1.4~1.6배(1.52배).</summary>
        [Test]
        public void PerSecondIsOnePointFourToSixOfCombo()
        {
            double combo = WeaponPresets.Twinblades.SingleTargetCoefficient;
            Assert.AreEqual(1.588, combo, 0.001, "새 쌍검 콤보 계수");
            Assert.AreEqual(2.42, TwinFlurry.CoefficientPerSecond, 0.005);
            double ratio = TwinFlurry.CoefficientPerSecond / combo;
            Assert.That(ratio, Is.InRange(1.4, 1.6));
            Assert.AreEqual(1.52, ratio, 0.01);
        }

        /// <summary>6초 단위(재사용 한 번) 단일 대상 증가가 +10% 이하(+9.6%, 1.59 → 1.74).</summary>
        [Test]
        public void SixSecondGainUnderTenPercent()
        {
            double combo = WeaponPresets.Twinblades.SingleTargetCoefficient;
            double gain = TwinFlurry.SixSecondGain(combo);
            Assert.AreEqual(0.096, gain, 0.001);
            Assert.LessOrEqual(gain, TwinFlurry.SixSecondGainCap);
            Assert.AreEqual(0.10, TwinFlurry.SixSecondGainCap, 1e-12);
            // 같은 값을 손으로: (2.66 + 1.588 × 4.9) ÷ (1.588 × 6).
            double byHand = (TwinFlurry.TotalPercent / 100.0 + combo * (TwinFlurry.Cooldown - TwinFlurry.Duration)) / (combo * TwinFlurry.Cooldown) - 1;
            Assert.AreEqual(byHand, gain, 1e-12);
            Assert.AreEqual(1.74, combo * (1 + gain), 0.005);
            Assert.AreEqual(0.0, TwinFlurry.SixSecondGain(0), 0);
            // 재사용 감소를 줬다면(예: 30%로 4.2초) 상한을 넘는다 — 그래서 주지 않는다(0-3의 8).
            double cdr = 4.2;
            double withCdr = (TwinFlurry.TotalPercent / 100.0 + combo * (cdr - TwinFlurry.Duration)) / (combo * cdr) - 1;
            Assert.Greater(withCdr, TwinFlurry.SixSecondGainCap);
        }

        /// <summary>치명(쌍검 치명 피해 1.3): 작은 타 36%, 마지막 91%로 모두 가벼움.</summary>
        [Test]
        public void CritsAreAllLight()
        {
            Assert.AreEqual(364, CritTiers.WeightPermille(TwinFlurry.Small.hitPercent, 1.3f));
            Assert.AreEqual(910, CritTiers.WeightPermille(TwinFlurry.Final.hitPercent, 1.3f));
            for (int i = 0; i < TwinFlurry.HitCount; i++)
                Assert.AreEqual(CritTier.Light, CritTiers.Of(TwinFlurry.PercentOf(i), 1.3f), "타 " + (i + 1));
        }

        /// <summary>재사용: 시작할 때 6초, 끊기면 3초. 재사용 감소 옵션은 받지 않는다.</summary>
        [Test]
        public void CooldownSixOrThreeIgnoringReduction()
        {
            Assert.AreEqual(6.0f, TwinFlurry.Cooldown, Eps);
            Assert.AreEqual(3.0f, TwinFlurry.InterruptedCooldown, Eps);
            Assert.AreEqual(6.0f, TwinFlurry.CooldownAfter(false), Eps);
            Assert.AreEqual(3.0f, TwinFlurry.CooldownAfter(true), Eps);
            foreach (int reduction in new[] { 0, 30, 50, 300, 1000 })
            {
                Assert.AreEqual(6.0f, TwinFlurry.CooldownAfter(false, reduction), Eps, "재사용 감소 " + reduction + "‰");
                Assert.AreEqual(3.0f, TwinFlurry.CooldownAfter(true, reduction), Eps, "끊김 + 재사용 감소 " + reduction + "‰");
            }
        }

        [Test]
        public void MovementAimAndSweepConstants()
        {
            Assert.AreEqual(0.30f, TwinFlurry.MoveScale, Eps);
            Assert.AreEqual(1.27f, 4.24f * TwinFlurry.MoveScale, 0.005f);
            Assert.AreEqual(90f, TwinFlurry.TurnRateDeg, Eps);
            Assert.AreEqual(1.9f, TwinFlurry.AimRange, Eps);
            Assert.AreEqual(1.0f, TwinFlurry.MaxApproach, Eps);
            Assert.AreEqual(0.1f, TwinFlurry.ApproachTime, Eps);
            Assert.AreEqual(0.06f, TwinFlurry.KillHitStop, Eps);
            Assert.LessOrEqual(TwinFlurry.ApproachTime, TwinFlurry.HitTimes[0], "다가가기는 첫 판정 전에 끝남");

            // 쓸기 중심각: 작은 타 일곱(오른손 1·3·5·7, 왼손 2·4·6).
            CollectionAssert.AreEqual(new[] { 5f, -5f, -15f, 15f, 15f, -15f, -5f }, TwinFlurry.SweepCenterDeg);
            Assert.AreEqual(TwinFlurry.HitCount - 1, TwinFlurry.SweepCenterDeg.Length);
            Assert.AreEqual(0.035f, TwinFlurry.SweepHalfTime, Eps);
            // 빠른 엇베기(2026-10-05): 펼침 45° → 중심 → 넘김 22°(넘긴 칼이 다른 손 다음 판정까지 버텨 X).
            Assert.AreEqual(45f, TwinFlurry.SweepOpenDeg, Eps);
            Assert.AreEqual(22f, TwinFlurry.SweepPastDeg, Eps);
            // 이펙트 방향 표(그림만): 작은 타 7개가 여러 방향(서로 다른 값, 왼쪽·오른쪽 섞임), 번쩍임은 2·4·6번째와 마지막.
            CollectionAssert.AreEqual(new[] { -35f, 30f, -10f, 45f, -50f, 20f, -20f }, TwinFlurry.EffectDirDeg);
            CollectionAssert.AllItemsAreUnique(TwinFlurry.EffectDirDeg);
            for (int i = 0; i < TwinFlurry.HitCount; i++)
                Assert.AreEqual(i == 1 || i == 3 || i == 5 || i == 7, TwinFlurry.FlashOn(i), (i + 1) + "번째 번쩍임");
            // 쓸기 ±0.035초가 서로 겹치지 않는다(간격 0.09).
            for (int i = 1; i < TwinFlurry.HitCount - 1; i++)
                Assert.Less(2f * TwinFlurry.SweepHalfTime, TwinFlurry.HitTimes[i] - TwinFlurry.HitTimes[i - 1]);
            // 끊김 경직 0.35초(버팀 룬 효과 없음).
            Assert.AreEqual(0.35f, WeaponActCommon.FlinchFor(WeaponActKind.Flurry), Eps);
            Assert.IsFalse(WeaponActRules.Armored(WeaponActKind.Flurry, WeaponActPhase.Flurry, true, false));
        }
    }
}
