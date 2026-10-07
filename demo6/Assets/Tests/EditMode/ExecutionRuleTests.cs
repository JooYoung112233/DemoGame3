using System;
using Demo6.Core.Combat;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 처형·기습 처형·회피 반격(기획/전투-보스-무기-다듬기-1차.md 4-2 [3][4], 결정 ③ 웅크리기 + 기습 처형, 8-1 #5 회피 반격 켜기).
    /// 문턱 30/20/12/10%, 무너짐 처형 경계, 기습 처형 결과 표, 등 각도 ±60°(단검 등 찌르기와 같은 판정), 회피 반격 피해 +20%(반올림 한 번).
    /// </summary>
    public sealed class ExecutionRuleTests
    {
        static readonly TargetClass Rat = new TargetClass(MonsterKind.Rat, WeightClass.Light, false, false, false, false);
        static readonly TargetClass Archer = new TargetClass(MonsterKind.Archer, WeightClass.Medium, false, false, false, false);
        static readonly TargetClass Boar = new TargetClass(MonsterKind.Boar, WeightClass.Heavy, false, false, false, false);
        /// <summary>정예는 종류와 관계없이 무거움(Enemy.MakeElite).</summary>
        static readonly TargetClass EliteRat = new TargetClass(MonsterKind.Rat, WeightClass.Heavy, true, false, false, false);
        static readonly TargetClass EliteBoar = new TargetClass(MonsterKind.Boar, WeightClass.Heavy, true, false, false, false);
        static readonly TargetClass NamedBoar = new TargetClass(MonsterKind.Boar, WeightClass.Heavy, true, true, false, false);
        static readonly TargetClass Ogre = new TargetClass(MonsterKind.Ogre, WeightClass.Heavy, false, false, true, false);
        static readonly TargetClass Nest = new TargetClass(MonsterKind.Nest, WeightClass.Heavy, false, false, false, false);
        static readonly TargetClass WoodDummy = new TargetClass(MonsterKind.Boar, WeightClass.Heavy, false, false, false, true);
        static readonly TargetClass RatDummy = new TargetClass(MonsterKind.Rat, WeightClass.Light, false, false, false, true);

        /// <summary>적이 보는 방향(오른쪽)에서 deg도 돌린 쪽에 공격자가 있다(180 = 바로 등 뒤).</summary>
        static bool BehindAt(float deg)
        {
            double rad = deg * Math.PI / 180.0;
            return BackstabRule.IsBehind(1f, 0f, (float)Math.Cos(rad), (float)Math.Sin(rad));
        }

        // ── 무너짐 처형 문턱 ──

        [Test]
        public void ThresholdsMatchDoc()
        {
            Assert.AreEqual(0.30f, ExecutionRule.Threshold(Archer), 1e-6, "보통 무게(궁수) 30%");
            Assert.AreEqual(0.20f, ExecutionRule.Threshold(Boar), 1e-6, "무거움(멧돼지) 20%");
            Assert.AreEqual(0.12f, ExecutionRule.Threshold(EliteBoar), 1e-6, "정예 12%");
            Assert.AreEqual(0.12f, ExecutionRule.Threshold(EliteRat), 1e-6, "정예 굴쥐도 정예 문턱");
            Assert.AreEqual(0.10f, ExecutionRule.Threshold(NamedBoar), 1e-6, "이름난 정예 10%");
            Assert.AreEqual(0f, ExecutionRule.Threshold(Rat), "가벼운 적(굴쥐)은 무너지지 않아 처형 없음");
            Assert.AreEqual(0f, ExecutionRule.Threshold(Ogre), "보스는 처형하지 않는다");
            Assert.AreEqual(0f, ExecutionRule.Threshold(Nest), "둥지는 처형하지 않는다");
            Assert.AreEqual(0f, ExecutionRule.Threshold(WoodDummy), "허수아비는 처형하지 않는다");
        }

        [Test]
        public void ThresholdsFollowTuningArguments()
        {
            // 시험 패널 손잡이(Tuning.ExecuteThreshold*)는 보통·무거움·정예 세 값만 바꾼다. 이름난 정예 10%는 고정.
            Assert.AreEqual(0.25f, ExecutionRule.Threshold(Archer, 0.25f, 0.15f, 0.08f), 1e-6);
            Assert.AreEqual(0.15f, ExecutionRule.Threshold(Boar, 0.25f, 0.15f, 0.08f), 1e-6, "12초 아래로 떨어지면 15%로 내리는 손잡이");
            Assert.AreEqual(0.08f, ExecutionRule.Threshold(EliteBoar, 0.25f, 0.15f, 0.08f), 1e-6);
            Assert.AreEqual(0.10f, ExecutionRule.Threshold(NamedBoar, 0.25f, 0.15f, 0.08f), 1e-6);
            Assert.AreEqual(0f, ExecutionRule.Threshold(Ogre, 0.9f, 0.9f, 0.9f), "보스는 손잡이와 관계없이 0");
        }

        [Test]
        public void ShouldExecuteAtThresholdButNotAbove()
        {
            Assert.IsTrue(ExecutionRule.ShouldExecute(Boar, true, true, false, 0.20), "문턱과 같으면 처형");
            Assert.IsFalse(ExecutionRule.ShouldExecute(Boar, true, true, false, 0.2001), "문턱 위면 처형 없음");
            Assert.IsTrue(ExecutionRule.ShouldExecute(Archer, true, true, false, 0.30));
            Assert.IsFalse(ExecutionRule.ShouldExecute(Archer, true, true, false, 0.3001));
            Assert.IsTrue(ExecutionRule.ShouldExecute(EliteBoar, true, true, false, 0.12));
            Assert.IsFalse(ExecutionRule.ShouldExecute(EliteBoar, true, true, false, 0.1201));
            Assert.IsTrue(ExecutionRule.ShouldExecute(NamedBoar, true, true, false, 0.10));
            Assert.IsFalse(ExecutionRule.ShouldExecute(NamedBoar, true, true, false, 0.1001));
            // 1층 멧돼지(체력 2,000)는 문턱 20% = 400 이하에서 처형된다.
            Assert.IsTrue(ExecutionRule.ShouldExecute(Boar, true, true, false, 400.0 / 2000.0));
            Assert.IsFalse(ExecutionRule.ShouldExecute(Boar, true, true, false, 401.0 / 2000.0));
        }

        [Test]
        public void ShouldExecuteNeedsBrokenAndFinisherOrWave()
        {
            Assert.IsFalse(ExecutionRule.ShouldExecute(Boar, false, true, false, 0.05), "맞기 전에 무너져 있지 않았으면 없음");
            Assert.IsFalse(ExecutionRule.ShouldExecute(Boar, true, false, false, 0.05), "마무리·검풍이 아니면 없음");
            Assert.IsFalse(ExecutionRule.ShouldExecute(Boar, false, false, true, 0.0));
        }

        [Test]
        public void KillingBlowOnBrokenTargetIsExecution()
        {
            Assert.IsTrue(ExecutionRule.ShouldExecute(Boar, true, true, true, 0.0), "그 타로 죽으면 처형");
            Assert.IsTrue(ExecutionRule.ShouldExecute(Archer, true, true, true, 0.0));
            Assert.IsFalse(ExecutionRule.ShouldExecute(Rat, true, true, true, 0.0), "굴쥐는 처형 대상이 아니다");
            Assert.IsFalse(ExecutionRule.ShouldExecute(Ogre, true, true, true, 0.0), "보스 처치는 처형이 아니다(보스 처치 연출이 따로)");
            Assert.IsFalse(ExecutionRule.ShouldExecute(Nest, true, true, true, 0.0));
            Assert.IsFalse(ExecutionRule.ShouldExecute(WoodDummy, true, true, true, 0.0));
        }

        // ── 기습 처형 ──

        [Test]
        public void AmbushOutcomeTable()
        {
            // 웅크린 채, 잠들고 들키지 않은 적의 등 뒤에서 친 근접 기본공격 첫 타.
            Assert.AreEqual(AmbushOutcome.Kill, ExecutionRule.Ambush(Rat, true, false, true, true, true), "굴쥐 즉사");
            Assert.AreEqual(AmbushOutcome.Kill, ExecutionRule.Ambush(Archer, true, false, true, true, true), "궁수 즉사");
            Assert.AreEqual(AmbushOutcome.Break, ExecutionRule.Ambush(Boar, true, false, true, true, true), "멧돼지는 바로 무너짐(피해 2배 없음)");
            Assert.AreEqual(AmbushOutcome.PoiseOnly, ExecutionRule.Ambush(EliteRat, true, false, true, true, true), "정예는 지금 기습 규칙");
            Assert.AreEqual(AmbushOutcome.PoiseOnly, ExecutionRule.Ambush(EliteBoar, true, false, true, true, true));
            Assert.AreEqual(AmbushOutcome.PoiseOnly, ExecutionRule.Ambush(NamedBoar, true, false, true, true, true));
            Assert.AreEqual(AmbushOutcome.PoiseOnly, ExecutionRule.Ambush(Ogre, true, false, true, true, true), "보스도 지금 기습 규칙");
            Assert.AreEqual(AmbushOutcome.None, ExecutionRule.Ambush(Nest, true, false, true, true, true), "둥지 없음");
            Assert.AreEqual(AmbushOutcome.None, ExecutionRule.Ambush(WoodDummy, true, false, true, true, true), "허수아비 없음");
            Assert.AreEqual(AmbushOutcome.None, ExecutionRule.Ambush(RatDummy, true, false, true, true, true));
        }

        [Test]
        public void AmbushNeedsEveryCondition()
        {
            foreach (var t in new[] { Rat, Archer, Boar, EliteBoar, Ogre })
            {
                Assert.AreEqual(AmbushOutcome.None, ExecutionRule.Ambush(t, false, false, true, true, true), "깨어 있음");
                Assert.AreEqual(AmbushOutcome.None, ExecutionRule.Ambush(t, true, true, true, true, true), "들킴('!' 0.5초·무리 반응 0.8초 기다리는 중)");
                Assert.AreEqual(AmbushOutcome.None, ExecutionRule.Ambush(t, true, false, false, true, true), "서서 침(웅크리지 않음)");
                Assert.AreEqual(AmbushOutcome.None, ExecutionRule.Ambush(t, true, false, true, false, true), "근접 기본공격이 아님(검풍·회오리)");
                Assert.AreEqual(AmbushOutcome.None, ExecutionRule.Ambush(t, true, false, true, true, false), "등 뒤가 아님(앞·옆)");
            }
        }

        [Test]
        public void AmbushUsesBackstabHalfAngle()
        {
            Assert.AreEqual(BackstabRule.HalfAngle, ExecutionRule.AmbushHalfAngle, "기습 처형과 단검 등 찌르기는 같은 ±60°");
            Assert.AreEqual(60f, BackstabRule.HalfAngle);
            // 바로 등 뒤(180°)와 등 뒤 축에서 ±59.5°(120.5°)는 등 뒤, ±60.5°(119.5°)는 아님.
            Assert.IsTrue(BehindAt(180f));
            Assert.IsTrue(BehindAt(120.5f));
            Assert.IsTrue(BehindAt(-120.5f));
            Assert.IsFalse(BehindAt(119.5f));
            Assert.IsFalse(BehindAt(-119.5f));
            Assert.IsFalse(BehindAt(90f), "옆");
            Assert.IsFalse(BehindAt(0f), "바로 앞");
            // 같은 각도 판정이 기습 처형 결과와 단검 치명 확률을 함께 정한다.
            Assert.AreEqual(AmbushOutcome.Kill, ExecutionRule.Ambush(Rat, true, false, true, true, BehindAt(-121f)));
            Assert.AreEqual(AmbushOutcome.None, ExecutionRule.Ambush(Rat, true, false, true, true, BehindAt(-119f)));
            Assert.AreEqual(470, BackstabRule.CritChancePermille(70, BehindAt(150f)), "등 뒤: 시트 70‰ + 400‰");
            Assert.AreEqual(70, BackstabRule.CritChancePermille(70, BehindAt(100f)), "등 뒤가 아니면 그대로");
            Assert.AreEqual(900, BackstabRule.CritChancePermille(500, true), "상한 500‰ 밖에서 더함");
            Assert.AreEqual(1000, BackstabRule.CritChancePermille(700, true), "1000‰에서 자름");
        }

        [Test]
        public void AmbushIgnoresZeroVectors()
        {
            // 적과 겹친 자리(방향 없음)나 바라보는 방향이 없는 적은 등 뒤로 보지 않는다.
            Assert.IsFalse(BackstabRule.IsBehind(1f, 0f, 0f, 0f));
            Assert.IsFalse(BackstabRule.IsBehind(0f, 0f, -1f, 0f));
        }

        // ── 연출 값·느린 화면 조건 ──

        [Test]
        public void PresentationValuesMatchDoc()
        {
            Assert.AreEqual(0.6f, ExecutionRule.PullDistance);
            Assert.AreEqual(0.06f, ExecutionRule.PullSeconds);
            Assert.AreEqual(1.25f, ExecutionRule.SwordScale);
            Assert.AreEqual(0.10f, ExecutionRule.HitStop);
            Assert.AreEqual(0.3f, ExecutionRule.SlowSeconds);
            Assert.AreEqual(0.35f, ExecutionRule.SlowScale);
            Assert.AreEqual(0.03f, ExecutionRule.HealFraction);
            Assert.IsFalse(ExecutionRule.HealDefaultOn, "처형 회복은 기본 끔(8-1 #6)");
            Assert.IsTrue(ExecutionRule.DefaultOn);
            Assert.IsTrue(ExecutionRule.AmbushDefaultOn, "기습 처형 넣기(결정 ③)");
            Assert.AreEqual(0f, ExecutionRule.SleeperTurnDefault, "뒤척임 기본 0%");
            Assert.AreEqual(12f, ExecutionRule.LastEnemyRange);
            // 처형 0.10은 무너짐 0.08과 더해도 1초 예산 0.2 안이다.
            Assert.LessOrEqual(0.08 + ExecutionRule.HitStop, Demo6.Core.Time.TimeScaleArbiter.HitStopBudget + 1e-9);
            Assert.Greater(ExecutionRule.PoseSeconds, ExecutionRule.PullSeconds, "자세는 당겨 붙기보다 길다");
        }

        [Test]
        public void PullStopsAtBodyGap()
        {
            Assert.AreEqual(0.6f, ExecutionRule.PullTo(1.5f), 1e-6, "멀면 0.6");
            Assert.AreEqual(0.3f, ExecutionRule.PullTo(0.3f), 1e-6, "가까우면 틈까지만");
            Assert.AreEqual(0f, ExecutionRule.PullTo(0f));
            Assert.AreEqual(0f, ExecutionRule.PullTo(-0.2f), "겹쳐 있으면 당기지 않음");
        }

        [Test]
        public void SlowMotionOnlyForLastEnemyOrElite()
        {
            Assert.IsTrue(ExecutionRule.UsesSlowMotion(Boar, true), "마주침의 마지막 적");
            Assert.IsFalse(ExecutionRule.UsesSlowMotion(Boar, false), "아직 남은 적이 있으면 히트스톱과 피로만");
            Assert.IsFalse(ExecutionRule.UsesSlowMotion(Archer, false));
            Assert.IsTrue(ExecutionRule.UsesSlowMotion(EliteBoar, false), "정예는 늘");
            Assert.IsTrue(ExecutionRule.UsesSlowMotion(NamedBoar, false));
            Assert.IsFalse(ExecutionRule.UsesSlowMotion(Ogre, true), "보스는 처형하지 않는다(보스 처치 느린 화면이 따로)");
        }

        // ── 회피 반격 ──

        [Test]
        public void CounterIsOnByDefault()
        {
            Assert.IsTrue(CounterRule.DefaultOn, "결정 8-1 #5 '켜기'");
            Assert.AreEqual(1.0f, CounterRule.Window);
            Assert.AreEqual(0.2f, CounterRule.DamageBonus);
            Assert.AreEqual(1.0 + CounterRule.DamageBonus, CounterRule.DamageScale, 1e-6, "피해 배율 = 1 + 0.2");
            Assert.AreEqual(0.12f, CounterRule.SlowSeconds);
            Assert.AreEqual(0.4f, CounterRule.SlowScale);
            Assert.AreEqual(0.3f, CounterRule.AfterimageSeconds);
            Assert.AreEqual(0.15f, CounterRule.FlashSeconds);
            Assert.AreEqual(2f, CounterRule.PoiseScale(true));
            Assert.AreEqual(1f, CounterRule.PoiseScale(false));
        }

        [Test]
        public void CounterDamageIsTwentyPercentRoundedOnce()
        {
            Assert.AreEqual(168.0, CounterRule.DamagePercent(140.0, true), 1e-9);
            // 창이 닫혀 있으면 배율을 그대로 돌려준다(예전 피해와 비트까지 같음).
            Assert.AreEqual(90f, (float)CounterRule.DamagePercent(90f, false));
            Assert.AreEqual(BitConverter.DoubleToInt64Bits(90.0), BitConverter.DoubleToInt64Bits(CounterRule.DamagePercent(90.0, false)));
            // 공격 105 × 90%: 두 번 반올림하면 round(94.5) = 95 → × 1.2 = 114. 한 번이면 105 × 108% = 113.4 → 113.
            Assert.AreEqual(113, DamageMath.ToMonster(105, CounterRule.DamagePercent(90.0, true), false, 1.5, 1.0));
            Assert.AreEqual(95, DamageMath.ToMonster(105, CounterRule.DamagePercent(90.0, false), false, 1.5, 1.0));
        }
    }
}
