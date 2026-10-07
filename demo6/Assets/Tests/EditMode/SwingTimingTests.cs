using System;
using Demo6.Core.Combat;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 공격 속도를 콤보 타이밍에 넣는 규칙(장비 문서 3-3)과 M0a 손맛 보호.
    /// 공격 속도 0이면 예전 PlayerController 식과 float 비트까지 같아야 하고, 0~400‰에서는 규칙 1~5가 아홉 무기 모든 단계에서 지켜져야 한다
    /// (새 무기 6종도 같은 규칙, 전투·보스·무기 다듬기 1차 2-1).
    /// </summary>
    public sealed class SwingTimingTests
    {
        /// <summary>찌르기 멈춤: 판정 뒤 칼을 뻗은 채 버티는 시간(TopDownSwing.Thrust의 hit + 0.14).</summary>
        const float ThrustHold = 0.14f;
        /// <summary>내려찍기 충격: 판정 뒤 몸이 눌리는 시간(TopDownSwing.Slam의 hit + 0.24).</summary>
        const float SlamImpact = 0.24f;
        const float Eps = 1e-5f;

        static int Bits(float f) => BitConverter.ToInt32(BitConverter.GetBytes(f), 0);

        // ── 예전 PlayerController 식(StartSwing·HitTime·이월, SwingTiming으로 바꾸기 전 그대로) ──

        /// <summary>_swingDuration = Mathf.Max(0.1f, _step.duration). Mathf.Max(a, b)는 (a > b) ? a : b다.</summary>
        static float OldDuration(ComboStep step) => 0.1f > step.duration ? 0.1f : step.duration;

        /// <summary>HitTime(index) = _swingDuration * _step.hitMoment + index * _step.hitInterval.</summary>
        static float OldHitTime(float swingDuration, ComboStep step, int index) => swingDuration * step.hitMoment + index * step.hitInterval;

        /// <summary>넘친 시간 이월 상한 = _swingDuration * 0.5f.</summary>
        static float OldCarryCap(float swingDuration) => swingDuration * 0.5f;

        static bool IsThrust(ComboStep step) => step.shape == ComboShape.Line;
        static bool IsSlam(ComboStep step) => step.shape == ComboShape.Circle && step.centerOffset > 0.01f;

        [TestCase(0)]
        [TestCase(-50)]
        [TestCase(-1000)]
        public void ZeroSpeedIsBitIdenticalToOldFormula(int attackSpeedPermille)
        {
            int checkedHits = 0;
            foreach (var weapon in WeaponPresets.All)
                for (int i = 0; i < weapon.combo.Length; i++)
                {
                    var step = weapon.combo[i];
                    var plan = SwingTiming.Plan(step, attackSpeedPermille);
                    float d = OldDuration(step);
                    string where = weapon.displayName + " " + (i + 1) + "단계 " + step.name;
                    Assert.AreEqual(Bits(d), Bits(plan.Duration), where + " 동작 길이");
                    Assert.AreEqual(Bits(OldCarryCap(d)), Bits(plan.CarryCap), where + " 이월 상한");
                    Assert.AreEqual(Bits(step.hitInterval), Bits(plan.HitInterval), where + " 연타 간격");
                    Assert.AreEqual(step.hits, plan.Hits, where + " 타 수");
                    for (int k = 0; k < step.hits; k++)
                    {
                        Assert.AreEqual(Bits(OldHitTime(d, step, k)), Bits(plan.HitTime(k)), where + " 판정 " + (k + 1));
                        checkedHits++;
                    }
                    Assert.AreEqual(Bits(OldHitTime(d, step, 0)), Bits(plan.FirstHit), where + " 첫 판정");
                    Assert.AreEqual(Bits(OldHitTime(d, step, step.hits - 1)), Bits(plan.LastHit), where + " 마지막 판정");
                }

            // 한손검과 방패 3 + 대검 3 + 쌍검 (2 × 3 + 3) = 15 판정, 새 무기 쇠망치 3 + 창 3 + 큰 낫 3 + 도끼 3 + 단검 (2 × 2 + 1) + 사슬 철퇴 3 = 20 판정.
            Assert.AreEqual(35, checkedHits);
            Assert.AreEqual(Bits(1f), Bits(SwingTiming.SpeedFactor(attackSpeedPermille)));
            Assert.AreEqual(Bits(1f), Bits(SwingTiming.PoiseScale(attackSpeedPermille)));
            Assert.AreEqual(Bits(35f), Bits(SwingTiming.AimTurnRate(attackSpeedPermille)));
        }

        [Test]
        public void ZeroSpeedKeepsMinimumDuration()
        {
            var tiny = new ComboStep { duration = 0.05f, hitMoment = 0.5f };
            var plan = SwingTiming.Plan(tiny, 0);
            Assert.AreEqual(Bits(0.1f), Bits(plan.Duration));
            Assert.AreEqual(Bits(0.1f * 0.5f), Bits(plan.FirstHit));
        }

        [Test]
        public void RulesHoldForEveryStepUpToFourHundred()
        {
            int cases = 0;
            foreach (var weapon in WeaponPresets.All)
                for (int a = 0; a <= 400; a += 50)
                {
                    float s = SwingTiming.SpeedFactor(a);
                    for (int i = 0; i < weapon.combo.Length; i++)
                    {
                        var step = weapon.combo[i];
                        var plan = SwingTiming.Plan(step, a);
                        float d = OldDuration(step);
                        string where = weapon.displayName + " " + (i + 1) + "단계 " + step.name + " 공속 " + a + "‰";

                        // ① 마지막 판정 + 0.08 ≤ 동작 끝.
                        Assert.That(plan.LastHit + SwingTiming.MinRecovery, Is.LessThanOrEqualTo(plan.Duration + Eps), where + " ① 회수");
                        // ② 첫 판정 ≥ 0.1.
                        Assert.That(plan.FirstHit, Is.GreaterThanOrEqualTo(0.1f), where + " ② 첫 판정");
                        // 규칙 4 안전망은 0~400‰에서 걸리지 않는다(가장 짧은 회수 0.141초, 쌍검 ① 우측 베기). 그래서 ③은 늘 확인된다.
                        Assert.AreEqual(d / s, plan.Duration, 1e-6f, where + " 규칙 4가 걸림");
                        // ③ 한 동작 = 원래 ÷ s(±1ms).
                        Assert.AreEqual(step.duration / s, plan.Duration, 0.001f, where + " ③ 길이");
                        // ④ 판정 비율 ≥ 원래(준비가 상대적으로 길어지는 쪽으로만).
                        Assert.That(plan.FirstHit / plan.Duration, Is.GreaterThanOrEqualTo(step.hitMoment - Eps), where + " ④ 판정 비율");
                        // 규칙 2·3: 첫 판정 = d × hitMoment ÷ (1 + a/2), 연타 간격은 그대로.
                        Assert.AreEqual(d * step.hitMoment / (1f + a / 2000f), plan.FirstHit, 1e-6f, where + " 규칙 2");
                        for (int k = 1; k < step.hits; k++)
                            Assert.AreEqual(step.hitInterval, plan.HitTime(k) - plan.HitTime(k - 1), 1e-6f, where + " 규칙 3");
                        // ⑤ 찌르기 멈춤·내려찍기 충격이 동작 안.
                        if (IsThrust(step))
                            Assert.That(plan.FirstHit + ThrustHold, Is.LessThanOrEqualTo(plan.Duration + Eps), where + " ⑤ 찌르기 멈춤");
                        if (IsSlam(step))
                            Assert.That(plan.FirstHit + SlamImpact, Is.LessThanOrEqualTo(plan.Duration + Eps), where + " ⑤ 내려찍기 충격");
                        // 규칙 5: 이월 상한 = 줄어든 길이 × 0.5.
                        Assert.AreEqual(plan.Duration * 0.5f, plan.CarryCap, 1e-7f, where + " 규칙 5");
                        cases++;
                    }
                }

            // 옛 세 무기(3 + 3 + 4 단계) + 새 여섯 무기(3단계씩 18) = 28단계 × 9 공속.
            Assert.AreEqual(28 * 9, cases);
        }

        [Test]
        public void ComboCycleShrinksExactlyBySpeed()
        {
            foreach (var weapon in WeaponPresets.All)
                for (int a = 0; a <= 400; a += 50)
                {
                    float s = SwingTiming.SpeedFactor(a);
                    double cycle = 0;
                    foreach (var step in weapon.combo) cycle += SwingTiming.Plan(step, a).Duration;
                    Assert.AreEqual(weapon.CycleSeconds / s, cycle, 0.001, weapon.displayName + " 공속 " + a + "‰ 한 바퀴");
                }
        }

        [Test]
        public void ShortestRecoveryUpToFourHundredIsTwinFlurry()
        {
            float shortest = float.MaxValue;
            string which = null;
            foreach (var weapon in WeaponPresets.All)
                for (int i = 0; i < weapon.combo.Length; i++)
                {
                    var plan = SwingTiming.Plan(weapon.combo[i], 400);
                    if (plan.Recovery < shortest)
                    {
                        shortest = plan.Recovery;
                        which = weapon.displayName + " " + (i + 1) + "단계";
                    }
                }
            // 세 무기 콤보(spec.json 2차 2026-10-05): 400‰에서도 가장 짧은 회수가 0.141초(쌍검 ① 우측 베기,
            // 0.52 ÷ 1.4 − (0.156 ÷ 1.2 + 0.10))라 규칙 4(0.08초)가 걸리지 않는다.
            Assert.AreEqual(0.141f, shortest, 0.0006f, which);
            Assert.AreEqual("쌍검 1단계", which);

            // 새 무기 가운데 가장 짧은 회수는 단검 ①② 두 번 긋기 0.159초(0.6 ÷ 1.4 − (0.18 ÷ 1.2 + 0.12)).
            float newShortest = float.MaxValue;
            string newWhich = null;
            foreach (var weapon in WeaponPresets.New6)
                for (int i = 0; i < weapon.combo.Length; i++)
                {
                    var plan = SwingTiming.Plan(weapon.combo[i], 400);
                    if (plan.Recovery < newShortest)
                    {
                        newShortest = plan.Recovery;
                        newWhich = weapon.displayName + " " + (i + 1) + "단계";
                    }
                }
            Assert.AreEqual(0.159f, newShortest, 0.0006f, newWhich);
            Assert.AreEqual("단검 1단계", newWhich);
            Assert.AreEqual(newShortest, SwingTiming.Plan(WeaponPresets.Dagger.combo[1], 400).Recovery, 1e-7f, "단검 ②도 같음");
        }

        /// <summary>
        /// 문서 3-3 '상한 +30%에서 가장 빠듯한 동작' 표(초, 셋째 자리 반올림). 세 무기 콤보는 기획/세-무기-우클릭-소켓-1차.md 2-3·3-4·4-4 값:
        /// 한손검과 방패 ① 베기·③ 마무리 베기, 대검 ③ 내려 쪼개기, 쌍검 ① 우측 베기(34%)·③ 엇베기(37%)·④ 가위 가르기(37%). 찌르기 단계는 이제 창에만 있다.
        /// </summary>
        [TestCase("wpn_longsword", 0, 0.446f, 0.182f, 0.182f, 0.265f, 41)]
        [TestCase("wpn_longsword", 2, 0.708f, 0.336f, 0.336f, 0.372f, 47)]
        [TestCase("wpn_greatsword", 2, 1.000f, 0.565f, 0.565f, 0.435f, 57)]
        [TestCase("wpn_twinblades", 0, 0.400f, 0.1357f, 0.2357f, 0.1643f, 34)]
        [TestCase("wpn_twinblades", 2, 0.4462f, 0.1664f, 0.2264f, 0.2197f, 37)]
        [TestCase("wpn_twinblades", 3, 0.7154f, 0.2669f, 0.3669f, 0.3485f, 37)]
        public void PlusThirtyPercentTable(string weaponId, int stepIndex, float duration, float firstHit, float lastHit, float recovery, int hitRatioPercent)
        {
            var weapon = Rule(weaponId);
            var step = weapon.combo[stepIndex];
            var plan = SwingTiming.Plan(step, 300);
            const float Tol = 0.0006f;
            Assert.AreEqual(duration, plan.Duration, Tol, "길이");
            Assert.AreEqual(firstHit, plan.FirstHit, Tol, "첫 판정");
            Assert.AreEqual(lastHit, plan.LastHit, Tol, "마지막 판정");
            Assert.AreEqual(recovery, plan.Recovery, Tol, "회수");
            Assert.AreEqual(hitRatioPercent, (int)Math.Round(plan.FirstHit / plan.Duration * 100f), "판정 비율");
            if (IsThrust(step)) Assert.AreEqual(0.476f, plan.FirstHit + ThrustHold, Tol, "찌르기 멈춤");
            if (IsSlam(step)) Assert.AreEqual(0.805f, plan.FirstHit + SlamImpact, Tol, "내려찍기 충격");
        }

        [Test]
        public void AcceptsZeroToThousandAndClampsBeyond()
        {
            var step = WeaponPresets.Longsword.combo[0];
            var full = SwingTiming.Plan(step, 1000);
            // 한손검과 방패 ① 베기 0.58초, 판정 0.2088초: 1000‰이면 길이 0.29, 첫 판정 0.2088 ÷ 1.5 = 0.1392.
            Assert.AreEqual(0.29f, full.Duration, 1e-6f);
            Assert.AreEqual(0.2088f / 1.5f, full.FirstHit, 1e-6f);
            Assert.AreEqual(2f, SwingTiming.SpeedFactor(1000));

            var over = SwingTiming.Plan(step, 5000);
            Assert.AreEqual(Bits(full.Duration), Bits(over.Duration));
            Assert.AreEqual(Bits(full.FirstHit), Bits(over.FirstHit));
            Assert.AreEqual(2f, SwingTiming.SpeedFactor(5000));
        }

        [Test]
        public void RecoveryGuardStretchesOnlyWhenNeeded()
        {
            // 일부러 빠듯한 단계: 0.2초에 3타. 공속 +100%면 d' = 0.1인데 마지막 판정이 그보다 늦어 회수 보장(규칙 4)이 늘린다.
            var tight = new ComboStep { duration = 0.2f, hitMoment = 0.5f, hits = 3, hitInterval = 0.1f };
            var plan = SwingTiming.Plan(tight, 1000);
            Assert.AreEqual(0.1f / 1.5f, plan.FirstHit, 1e-6f);
            Assert.AreEqual(plan.LastHit + SwingTiming.MinRecovery, plan.Duration, 1e-6f);
            Assert.AreEqual(SwingTiming.MinRecovery, plan.Recovery, 1e-6f);
            Assert.AreEqual(plan.Duration * 0.5f, plan.CarryCap, 1e-7f);
        }

        [Test]
        public void AimTurnAndPoiseScale()
        {
            // 규칙 6: 35 × (1 + 공속 ÷ 2).
            Assert.AreEqual(40.25f, SwingTiming.AimTurnRate(300), 1e-4f);
            Assert.AreEqual(42f, SwingTiming.AimTurnRate(400), 1e-4f);
            // 타당 버팀 ÷ (1 + 공속): 초당 버팀 깎기가 공속과 무관하게 무기마다 고정(3-4).
            Assert.AreEqual(1f / 1.3f, SwingTiming.PoiseScale(300), 1e-6f);
            foreach (var weapon in WeaponPresets.All)
                for (int a = 0; a <= 400; a += 50)
                {
                    double poise = 0, cycle = 0;
                    foreach (var step in weapon.combo)
                    {
                        poise += step.poiseDamage * step.hits * SwingTiming.PoiseScale(a);
                        cycle += SwingTiming.Plan(step, a).Duration;
                    }
                    Assert.AreEqual(weapon.PoisePerSecond, poise / cycle, 0.02, weapon.displayName + " 공속 " + a + "‰ 초당 버팀");
                }
        }

        [Test]
        public void NullStepGivesSafePlan()
        {
            var plan = SwingTiming.Plan(null, 300);
            Assert.AreEqual(SwingTiming.MinDuration, plan.Duration);
            Assert.AreEqual(1, plan.Hits);
            Assert.AreEqual(0f, plan.LastHit);
            Assert.AreEqual(0f, default(SwingPlan).LastHit);
        }

        static WeaponAttackRule Rule(string id)
        {
            foreach (var w in WeaponPresets.All)
                if (w.id == id) return w;
            throw new ArgumentException(id);
        }
    }
}
