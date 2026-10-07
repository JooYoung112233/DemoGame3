using Demo6.Core.Time;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 멈춤 우선순위(기획/전투-보스-무기-다듬기-1차.md 4-3): 느린 화면이 겹치면 높은 순위만, 보스 순간(2단계·처치) 앞뒤 1.5초 안의 낮은 순위는 무시,
    /// 순위 없는 요청(예전 겹쳐 받기)은 그대로, 보스 처치 히트스톱 잠금.
    /// </summary>
    public sealed class SlowPriorityTests
    {
        [Test]
        public void PriorityOrderMatchesDoc()
        {
            Assert.Greater(SlowPriority.BossKill, SlowPriority.BossPhase);
            Assert.Greater(SlowPriority.BossPhase, SlowPriority.Execution);
            Assert.Greater(SlowPriority.Execution, SlowPriority.Counter);
            Assert.Greater(SlowPriority.Counter, SlowPriority.Normal);
            Assert.AreEqual(1.5, TimeScaleArbiter.BossMomentGuard, 1e-12);
        }

        [Test]
        public void HigherPriorityReplacesLower()
        {
            var t = new TimeScaleArbiter();
            Assert.IsTrue(t.RequestSlowMotion(0.12, 0.4, SlowPriority.Counter));
            Assert.AreEqual(0.4, t.Effective, 1e-9);
            Assert.IsTrue(t.RequestSlowMotion(0.3, 0.35, SlowPriority.Execution), "처형이 회피 반격을 덮어쓴다");
            Assert.AreEqual(0.35, t.Effective, 1e-9);
            Assert.AreEqual(SlowPriority.Execution, t.SlowPriorityNow);
            Assert.AreEqual(0.3, t.SlowRemaining, 1e-9);
        }

        [Test]
        public void HigherPriorityReplacesEvenWhenShorterOrFaster()
        {
            var t = new TimeScaleArbiter();
            t.RequestSlowMotion(1.0, 0.2, SlowPriority.Normal);
            Assert.IsTrue(t.RequestSlowMotion(0.12, 0.4, SlowPriority.Counter), "순위가 높으면 짧고 덜 느려도 그 값을 쓴다");
            Assert.AreEqual(0.4, t.Effective, 1e-9);
            Assert.AreEqual(0.12, t.SlowRemaining, 1e-9);
        }

        [Test]
        public void LowerPriorityIsIgnoredWhileHigherRuns()
        {
            var t = new TimeScaleArbiter();
            t.RequestSlowMotion(0.3, 0.35, SlowPriority.Execution);
            Assert.IsFalse(t.RequestSlowMotion(0.5, 0.2, SlowPriority.Counter));
            Assert.IsFalse(t.RequestSlowMotion(0.5, 0.2, SlowPriority.Normal));
            Assert.IsFalse(t.RequestSlowMotion(0.5, 0.2), "순위 없는 요청은 Normal");
            Assert.AreEqual(0.35, t.Effective, 1e-9);
            Assert.AreEqual(0.3, t.SlowRemaining, 1e-9);
            // 끝나면 낮은 순위도 다시 받는다.
            t.Advance(0.3);
            Assert.AreEqual(1.0, t.Effective, 1e-9);
            Assert.AreEqual(SlowPriority.Normal, t.SlowPriorityNow);
            Assert.IsTrue(t.RequestSlowMotion(0.12, 0.4, SlowPriority.Counter));
            Assert.AreEqual(0.4, t.Effective, 1e-9);
        }

        [Test]
        public void SamePriorityUsesOldMergeRule()
        {
            var t = new TimeScaleArbiter();
            t.RequestSlowMotion(0.3, 0.35, SlowPriority.Execution);
            t.Advance(0.1);
            Assert.IsTrue(t.RequestSlowMotion(0.3, 0.5, SlowPriority.Execution), "더 길면 받음(배율도 새 값)");
            Assert.AreEqual(0.3, t.SlowRemaining, 1e-9);
            Assert.AreEqual(0.5, t.Effective, 1e-9);
            Assert.IsTrue(t.RequestSlowMotion(0.1, 0.3, SlowPriority.Execution), "짧아도 더 느리면 배율만 바꾸고 남은 시간은 긴 쪽");
            Assert.AreEqual(0.3, t.SlowRemaining, 1e-9);
            Assert.AreEqual(0.3, t.Effective, 1e-9);
            Assert.IsFalse(t.RequestSlowMotion(0.1, 0.9, SlowPriority.Execution), "짧고 덜 느리면 버림");
            Assert.AreEqual(0.3, t.Effective, 1e-9);
        }

        [Test]
        public void UnrankedOverloadKeepsContractBehaviour()
        {
            // 순위를 쓰지 않는 예전 호출(여러 마리 처치 0.18초 × 0.35)만 있으면 예전 겹쳐 받기와 같다.
            var a = new TimeScaleArbiter();
            a.RequestSlowMotion(0.18, 0.35);
            a.Advance(0.05);
            a.RequestSlowMotion(0.1, 0.5);
            Assert.AreEqual(0.35, a.Effective, 1e-9, "짧고 덜 느린 요청은 버림");
            Assert.AreEqual(0.13, a.SlowRemaining, 1e-9);
            a.RequestSlowMotion(0.2, 0.5);
            Assert.AreEqual(0.5, a.Effective, 1e-9, "더 길면 받음");
            Assert.AreEqual(0.2, a.SlowRemaining, 1e-9);
            a.Advance(0.2);
            Assert.AreEqual(1.0, a.Effective, 1e-9);
            Assert.AreEqual(0.0, a.SlowRemaining, 1e-9);

            var b = new TimeScaleArbiter();
            b.RequestSlowMotion(0.18, 0.35, SlowPriority.Normal);
            Assert.AreEqual(0.35, b.Effective, 1e-9, "순위 없는 요청 = Normal");
            b.RequestSlowMotion(0, 0.1);
            b.RequestSlowMotion(-1, 0.1, SlowPriority.BossKill);
            Assert.AreEqual(0.35, b.Effective, 1e-9, "0초 이하 요청은 무시");
            Assert.IsFalse(b.InBossGuard, "0초 이하 보스 요청은 보스 순간으로 적지 않는다");
        }

        [Test]
        public void BossMomentOverridesRunningLowerSlow()
        {
            // 앞쪽 1.5초: 진행 중인 처형 느린 화면은 보스 2단계 전환이 바로 덮어쓴다.
            var t = new TimeScaleArbiter();
            t.RequestSlowMotion(0.3, 0.35, SlowPriority.Execution);
            t.Advance(0.1);
            Assert.IsTrue(t.RequestSlowMotion(0.6, 0.5, SlowPriority.BossPhase));
            Assert.AreEqual(0.5, t.Effective, 1e-9);
            Assert.AreEqual(SlowPriority.BossPhase, t.SlowPriorityNow);
            Assert.IsTrue(t.InBossGuard);
        }

        [Test]
        public void LowerPriorityIgnoredForOnePointFiveSecondsAfterBossMoment()
        {
            var t = new TimeScaleArbiter();
            t.RequestSlowMotion(0.6, 0.5, SlowPriority.BossPhase);
            t.Advance(0.3);
            Assert.IsFalse(t.RequestSlowMotion(0.12, 0.4, SlowPriority.Counter), "보스 느린 화면 동안은 순위로 버림");
            t.Advance(0.3);
            Assert.AreEqual(1.0, t.Effective, 1e-9, "보스 느린 화면은 끝났다(0.6초)");
            Assert.IsTrue(t.InBossGuard);
            Assert.IsFalse(t.RequestSlowMotion(0.12, 0.4, SlowPriority.Counter), "끝났어도 끝난 뒤 1.5초 안이면 무시");
            Assert.IsFalse(t.RequestSlowMotion(0.3, 0.35, SlowPriority.Execution));
            Assert.IsFalse(t.RequestSlowMotion(0.18, 0.35), "여러 마리 처치(Normal)도 무시");
            Assert.AreEqual(1.0, t.Effective, 1e-9);
            t.Advance(1.45);
            Assert.IsFalse(t.RequestSlowMotion(0.12, 0.4, SlowPriority.Counter), "끝난 뒤 1.45초");
            t.Advance(0.1);
            Assert.IsFalse(t.InBossGuard);
            Assert.IsTrue(t.RequestSlowMotion(0.12, 0.4, SlowPriority.Counter), "끝난 뒤 1.55초: 다시 받음");
            Assert.AreEqual(0.4, t.Effective, 1e-9);
        }

        [Test]
        public void BossGuardCountsFromRealEndAfterHitStop()
        {
            // 보스 느린 화면 중에 히트스톱이 끼면 느린 화면은 그만큼 늦게 끝나고, 가드도 실제로 끝난 때부터 센다.
            var t = new TimeScaleArbiter();
            t.RequestSlowMotion(1.0, 0.3, SlowPriority.BossKill);
            Assert.AreEqual(0.125, t.RequestHitStop(0.125), 1e-12);
            t.Advance(1.125);
            Assert.AreEqual(1.0, t.Effective, 1e-9, "히트스톱 0.125 + 느린 화면 1.0");
            t.Advance(1.45);
            Assert.IsTrue(t.InBossGuard, "끝(1.125초)에서 1.45초");
            t.Advance(0.1);
            Assert.IsFalse(t.InBossGuard, "끝에서 1.55초");
        }

        [Test]
        public void PausedTimeDoesNotEndBossSlow()
        {
            var t = new TimeScaleArbiter();
            t.RequestSlowMotion(0.6, 0.5, SlowPriority.BossPhase);
            t.Paused = true;
            t.Advance(5.0);
            t.Paused = false;
            Assert.AreEqual(0.5, t.Effective, 1e-9, "멈춘 동안 보스 느린 화면은 흐르지 않는다");
            Assert.IsFalse(t.RequestSlowMotion(0.12, 0.4, SlowPriority.Counter));
        }

        [Test]
        public void BossMomentsStillStackByPriority()
        {
            var t = new TimeScaleArbiter();
            t.RequestSlowMotion(0.6, 0.5, SlowPriority.BossPhase);
            t.Advance(0.2);
            Assert.IsTrue(t.RequestSlowMotion(1.0, 0.3, SlowPriority.BossKill), "보스 처치는 2단계 전환보다 위");
            Assert.AreEqual(0.3, t.Effective, 1e-9);
            Assert.AreEqual(1.0, t.SlowRemaining, 1e-9);
            Assert.IsFalse(t.RequestSlowMotion(0.6, 0.5, SlowPriority.BossPhase), "처치 중에는 2단계 요청을 버림");
            t.Advance(1.0);
            // 가드는 마지막 보스 느린 화면(처치, 1.2초에 끝남)이 끝난 때부터 1.5초다.
            t.Advance(1.4);
            Assert.IsFalse(t.RequestSlowMotion(0.3, 0.35, SlowPriority.Execution));
            Assert.IsTrue(t.RequestSlowMotion(0.6, 0.5, SlowPriority.BossPhase), "가드 안이어도 보스 순간끼리는 받는다");
        }

        [Test]
        public void HitStopStillWinsOverSlowAndPauseOverBoth()
        {
            var t = new TimeScaleArbiter();
            t.RequestSlowMotion(1.0, 0.3, SlowPriority.BossKill);
            t.RequestHitStop(0.1);
            Assert.AreEqual(0.0, t.Effective, 1e-9);
            t.Advance(0.1);
            Assert.AreEqual(0.3, t.Effective, 1e-9);
            t.Paused = true;
            Assert.AreEqual(0.0, t.Effective, 1e-9);
        }

        [Test]
        public void ResetClearsPriorityAndGuard()
        {
            var t = new TimeScaleArbiter();
            t.RequestSlowMotion(1.0, 0.3, SlowPriority.BossKill);
            t.RequestHitStopLocked(0.2, 0.5);
            t.Reset();
            Assert.AreEqual(1.0, t.Effective, 1e-9);
            Assert.AreEqual(SlowPriority.Normal, t.SlowPriorityNow);
            Assert.IsFalse(t.InBossGuard);
            Assert.IsFalse(t.HitStopLocked);
            Assert.IsTrue(t.RequestSlowMotion(0.12, 0.4, SlowPriority.Counter));
            Assert.AreEqual(0.05, t.RequestHitStop(0.05), 1e-9);
        }

        // ── 보스 처치 히트스톱 잠금 ──

        [Test]
        public void LockedHitStopIgnoresOthersUntilLockEnds()
        {
            var t = new TimeScaleArbiter();
            Assert.AreEqual(0.2, t.RequestHitStopLocked(0.2, 0.5), 1e-9);
            Assert.IsTrue(t.HitStopLocked);
            Assert.AreEqual(0.0, t.RequestHitStop(0.3), 1e-9, "잠금 중 다른 요청은 무시");
            t.Advance(0.2);
            Assert.AreEqual(1.0, t.Effective, 1e-9);
            Assert.AreEqual(0.0, t.RequestHitStop(0.04), 1e-9, "멈춤은 끝났어도 잠금 0.5초 안");
            t.Advance(0.31);
            Assert.IsFalse(t.HitStopLocked);
            // 잠금은 풀렸지만 1초 예산 0.2초는 처치 멈춤이 다 썼다.
            Assert.AreEqual(0.0, t.RequestHitStop(0.04), 1e-9);
            t.Advance(0.5);
            Assert.AreEqual(0.04, t.RequestHitStop(0.04), 1e-9, "1초가 지나면 다시 받는다");
        }

        [Test]
        public void LockedHitStopStaysInsideBudget()
        {
            var t = new TimeScaleArbiter();
            Assert.AreEqual(0.08, t.RequestHitStop(0.08), 1e-9);
            t.Advance(0.08);
            Assert.AreEqual(0.12, t.RequestHitStopLocked(0.3, 0.4), 1e-9, "예산에 남은 0.12초까지만");
            Assert.IsTrue(t.HitStopLocked);
            Assert.AreEqual(0.0, t.Effective, 1e-9);
        }
    }
}
