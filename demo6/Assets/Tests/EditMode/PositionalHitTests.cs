using System;
using Demo6.Core.Combat;
using Demo6.Core.Stats;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 백어택·헤드어택(PositionalHitRule). 백어택 = 등 뒤 ±60°(기습 처형·단검 등 찌르기와 같은 BackstabRule.IsBehind)에서 맞힘: 피해 +20%, 치명 +100‰(상한 밖,
    /// 단검 등 찌르기 +400‰와는 큰 쪽 하나). 헤드어택 = 정면 ±45° 치명: 버팀 × 1.5. 둥지·허수아비는 듣지 않고 보스는 듣는다. 처형이 난 타는 글자가 없다.
    /// </summary>
    public sealed class PositionalHitTests
    {
        static readonly TargetClass Rat = new TargetClass(MonsterKind.Rat, WeightClass.Light, false, false, false, false);
        static readonly TargetClass Boar = new TargetClass(MonsterKind.Boar, WeightClass.Heavy, false, false, false, false);
        static readonly TargetClass Boss = new TargetClass(MonsterKind.Ogre, WeightClass.Heavy, false, false, true, false);
        static readonly TargetClass Nest = new TargetClass(MonsterKind.Nest, WeightClass.Heavy, false, false, false, false);
        static readonly TargetClass Dummy = new TargetClass(MonsterKind.Rat, WeightClass.Light, false, false, false, true);

        /// <summary>적이 +x를 볼 때 (적 → 공격자) 방향이 보는 방향과 이루는 각(도)에서의 자리.</summary>
        static HitSide SideAt(TargetClass target, float degreesFromFacing, bool backOn = true, bool headOn = true)
        {
            double r = degreesFromFacing * Math.PI / 180.0;
            return PositionalHitRule.Side(target, 1f, 0f, (float)Math.Cos(r), (float)Math.Sin(r), backOn, headOn);
        }

        [Test]
        public void BackIsRearOneHundredTwentyAndFrontIsNinety()
        {
            Assert.AreEqual(60f, PositionalHitRule.BackHalfAngle);
            Assert.AreEqual(45f, PositionalHitRule.FrontHalfAngle);
            Assert.AreEqual(HitSide.Back, SideAt(Rat, 180f), "바로 뒤");
            Assert.AreEqual(HitSide.Back, SideAt(Rat, 120.5f), "등 뒤 ±60° 경계 안");
            Assert.AreEqual(HitSide.Back, SideAt(Rat, -120.5f));
            Assert.AreEqual(HitSide.None, SideAt(Rat, 119.5f), "등 뒤 경계 밖은 옆");
            Assert.AreEqual(HitSide.None, SideAt(Rat, 90f), "옆");
            Assert.AreEqual(HitSide.None, SideAt(Rat, -90f));
            Assert.AreEqual(HitSide.None, SideAt(Rat, 45.5f), "정면 ±45° 경계 밖");
            Assert.AreEqual(HitSide.Front, SideAt(Rat, 44.5f), "정면 경계 안");
            Assert.AreEqual(HitSide.Front, SideAt(Rat, -44.5f));
            Assert.AreEqual(HitSide.Front, SideAt(Rat, 0f), "바로 앞");
        }

        [Test]
        public void BackSharesTheAmbushAndDaggerAngleFunction()
        {
            Assert.AreEqual(BackstabRule.HalfAngle, PositionalHitRule.BackHalfAngle);
            Assert.AreEqual(ExecutionRule.AmbushHalfAngle, PositionalHitRule.BackHalfAngle);
            for (int d = -180; d <= 180; d += 3)
            {
                double r = d * Math.PI / 180.0;
                float x = (float)Math.Cos(r), y = (float)Math.Sin(r);
                bool behind = BackstabRule.IsBehind(1f, 0f, x, y);
                Assert.AreEqual(behind, PositionalHitRule.Side(Boar, 1f, 0f, x, y) == HitSide.Back, $"{d}°: 기습 처형·단검과 같은 판정");
                // 등 뒤와 정면은 겹치지 않는다.
                Assert.IsFalse(behind && PositionalHitRule.IsFront(1f, 0f, x, y), $"{d}°");
            }
        }

        [Test]
        public void LengthDoesNotMatterAndZeroVectorIsNothing()
        {
            Assert.AreEqual(HitSide.Back, PositionalHitRule.Side(Rat, 0f, 5f, 0f, -0.01f), "길이와 상관없음");
            Assert.AreEqual(HitSide.Front, PositionalHitRule.Side(Rat, 0.2f, 0.2f, 30f, 30f));
            Assert.AreEqual(HitSide.None, PositionalHitRule.Side(Rat, 1f, 0f, 0f, 0f), "같은 자리면 아무것도 아님");
            Assert.AreEqual(HitSide.None, PositionalHitRule.Side(Rat, 0f, 0f, 1f, 0f), "보는 방향이 없으면 아무것도 아님");
            Assert.IsFalse(PositionalHitRule.IsFront(0f, 0f, 1f, 0f));
        }

        [Test]
        public void NestAndDummyIgnoreButBossListens()
        {
            Assert.IsFalse(PositionalHitRule.Applies(Nest));
            Assert.IsFalse(PositionalHitRule.Applies(Dummy));
            Assert.IsTrue(PositionalHitRule.Applies(Boss));
            Assert.AreEqual(HitSide.None, SideAt(Nest, 180f), "둥지 등 뒤");
            Assert.AreEqual(HitSide.None, SideAt(Dummy, 180f), "허수아비 등 뒤(초당 피해 측정을 흔들지 않음)");
            Assert.AreEqual(HitSide.None, SideAt(Dummy, 0f), "허수아비 정면");
            Assert.AreEqual(HitSide.Back, SideAt(Boss, 180f), "보스 등 뒤");
            Assert.AreEqual(HitSide.Front, SideAt(Boss, 0f), "보스 정면");
        }

        [Test]
        public void TogglesTurnEachSideOff()
        {
            Assert.AreEqual(HitSide.None, SideAt(Rat, 180f, backOn: false), "백어택 끔");
            Assert.AreEqual(HitSide.Front, SideAt(Rat, 0f, backOn: false), "백어택만 끄면 정면은 그대로");
            Assert.AreEqual(HitSide.None, SideAt(Rat, 0f, headOn: false), "헤드어택 끔");
            Assert.AreEqual(HitSide.Back, SideAt(Rat, 180f, headOn: false));
            Assert.IsTrue(PositionalHitRule.BackDefaultOn);
            Assert.IsTrue(PositionalHitRule.HeadDefaultOn);
        }

        [Test]
        public void BackDamageIsTwentyPercentOnTheHitPercent()
        {
            Assert.AreEqual(200, PositionalHitRule.BackDamageBonusPermille);
            Assert.AreEqual(120.0, PositionalHitRule.DamagePercent(100.0, true));
            // 200‰는 회피 반격 × 1.2와 같은 double이다(둘이 겹치면 × 1.44).
            Assert.AreEqual(CounterRule.DamagePercent(140.0, true), PositionalHitRule.DamagePercent(140.0, true));
            Assert.AreEqual(140.0 * 1.2 * 1.2, PositionalHitRule.DamagePercent(CounterRule.DamagePercent(140.0, true), true), 1e-9);
            // 아니면 곱하지 않아 예전과 비트까지 같다.
            double odd = 90.0 * (1f + 0.15f);
            Assert.AreEqual(odd, PositionalHitRule.DamagePercent(odd, false));
            Assert.AreEqual(odd, PositionalHitRule.DamagePercent(odd, true, 0), "보너스 0이면 그대로");
            Assert.AreEqual(150.0, PositionalHitRule.DamagePercent(100.0, true, 500), "손잡이 500‰");
            // 피해 공식에 넣으면 반올림은 한 번(공격 1,000, 굴림 1.0, 방어 0).
            Assert.AreEqual(1200, DamageMath.ToMonster(1000, PositionalHitRule.DamagePercent(100.0, true), false, 1.5, 1.0));
            Assert.AreEqual(1800, DamageMath.ToMonster(1000, PositionalHitRule.DamagePercent(100.0, true), true, 1.5, 1.0));
        }

        [Test]
        public void BackCritAddsOutsideCapAndDaggerTakesTheLargerOne()
        {
            Assert.AreEqual(100, PositionalHitRule.BackCritBonusPermille);
            Assert.AreEqual(150, PositionalHitRule.CritChancePermille(50, true, false), "맨몸 5% + 10%p");
            Assert.AreEqual(600, PositionalHitRule.CritChancePermille(StatCaps.CritChancePermille, true, false), "상한 500 밖에서 더함");
            Assert.AreEqual(90, PositionalHitRule.CritChancePermille(90, false, false), "아무것도 아니면 그대로");
            Assert.AreEqual(-30, PositionalHitRule.CritChancePermille(-30, false, false));
            Assert.AreEqual(100, PositionalHitRule.CritChancePermille(-30, true, false), "음수 시트는 0으로 보고 더함");
            // 단검 등 찌르기(+400)와 백어택(+100)은 더하지 않고 큰 쪽 하나: 단검의 등 뒤 치명은 예전과 같다.
            Assert.AreEqual(400, PositionalHitRule.CritBonusPermille(true, true));
            Assert.AreEqual(BackstabRule.CritChancePermille(90, true), PositionalHitRule.CritChancePermille(90, true, true), "단검 + 백어택 = 단검 그대로(490)");
            Assert.AreEqual(BackstabRule.CritChancePermille(90, true), PositionalHitRule.CritChancePermille(90, false, true), "허수아비 등 뒤 단검(백어택 아님)");
            Assert.AreEqual(1000, PositionalHitRule.CritChancePermille(800, true, true), "1000에서 자름");
            Assert.AreEqual(1000, PositionalHitRule.CritChancePermille(950, true, false));
            Assert.AreEqual(500, PositionalHitRule.CritChancePermille(50, true, true, 450), "백어택 손잡이(450)가 더 크면 그것 하나(50 + 450)");
            Assert.AreEqual(50, PositionalHitRule.CritChancePermille(50, true, false, 0), "손잡이 0");
        }

        [Test]
        public void HeadAttackNeedsFrontCritAndPoiseLeft()
        {
            Assert.IsTrue(PositionalHitRule.IsHeadAttack(HitSide.Front, true, true, false));
            Assert.IsFalse(PositionalHitRule.IsHeadAttack(HitSide.Front, false, true, false), "치명이 아니면 아님");
            Assert.IsFalse(PositionalHitRule.IsHeadAttack(HitSide.Back, true, true, false), "등 뒤 치명은 백어택");
            Assert.IsFalse(PositionalHitRule.IsHeadAttack(HitSide.None, true, true, false), "옆");
            Assert.IsFalse(PositionalHitRule.IsHeadAttack(HitSide.Front, true, false, false), "버팀이 없는 적(M0a 값)");
            Assert.IsFalse(PositionalHitRule.IsHeadAttack(HitSide.Front, true, true, true), "이미 무너진 적은 버팀을 깎지 않음");
            Assert.AreEqual(1.5f, PositionalHitRule.HeadPoiseScale);
            Assert.AreEqual(1.5f, PositionalHitRule.PoiseScale(true));
            Assert.AreEqual(1f, PositionalHitRule.PoiseScale(false));
            Assert.AreEqual(1f, PositionalHitRule.PoiseScale(true, 0f), "0 이하 손잡이는 1");
            Assert.AreEqual(2f, PositionalHitRule.PoiseScale(true, 2f));
        }

        [Test]
        public void TagSkipsExecutionsAndHeadWins()
        {
            Assert.AreEqual(HitTag.Back, PositionalHitRule.TagOf(true, false, false));
            Assert.AreEqual(HitTag.Head, PositionalHitRule.TagOf(false, true, false));
            Assert.AreEqual(HitTag.None, PositionalHitRule.TagOf(false, false, false));
            Assert.AreEqual(HitTag.None, PositionalHitRule.TagOf(true, false, true), "기습 처형·무너짐 처형이 난 타는 글자 없음");
            Assert.AreEqual(HitTag.None, PositionalHitRule.TagOf(false, true, true));
            Assert.AreEqual("백어택", PositionalHitRule.Word(HitTag.Back));
            Assert.AreEqual("헤드어택", PositionalHitRule.Word(HitTag.Head));
            Assert.AreEqual("", PositionalHitRule.Word(HitTag.None));
        }

        [Test]
        public void HeadAttackCannotBreakTheBossInOneHit()
        {
            // 보스 무너뜨리기 창(3-4): 기습(70% 상한)·기둥 박기·압박으로 무너진다. 헤드어택 한 방(치명 × 1.5 × 1.5)이나
            // 회피 반격 첫 타(구르면 콤보가 1단계로 돌아가므로 1단계 × 2)와 겹쳐도 시험판 버팀 250을 한 번에 다 깎지 못한다.
            double bossPoise = BossRules.Poise(2);
            Assert.AreEqual(250.0, bossPoise);
            const double crit = 1.5;
            double head = PositionalHitRule.HeadPoiseScale;
            foreach (var w in WeaponPresets.All)
            {
                foreach (var s in w.combo)
                    Assert.Less(s.poiseDamage * crit * head, bossPoise, $"{w.displayName} {s.name}");
                Assert.Less(w.combo[0].poiseDamage * crit * head * CounterRule.PoiseMultiplier, bossPoise, $"{w.displayName} 회피 반격 첫 타");
            }
        }

        [Test]
        public void ExpectedFrontPoiseGainStaysSmall()
        {
            // 정면 타의 기대 버팀 배율(치명 확률 p): ((1 − p) + p × 1.5 × 1.5) ÷ ((1 − p) + p × 1.5).
            Assert.AreEqual(1.0, PositionalHitRule.ExpectedFrontPoiseGain(0), 1e-12);
            Assert.AreEqual(1.0714, PositionalHitRule.ExpectedFrontPoiseGain(100), 1e-4, "치명 10%: 정면 압박 +7%");
            Assert.AreEqual(1.1667, PositionalHitRule.ExpectedFrontPoiseGain(250), 1e-4, "치명 25%: +17%");
            Assert.AreEqual(1.3, PositionalHitRule.ExpectedFrontPoiseGain(StatCaps.CritChancePermille), 1e-9, "치명 상한 50%: +30%");
            Assert.AreEqual(1.0, PositionalHitRule.ExpectedFrontPoiseGain(250, 1f), 1e-12, "배율 1이면 그대로");
        }
    }
}
