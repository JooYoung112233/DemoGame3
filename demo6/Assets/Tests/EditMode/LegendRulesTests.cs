using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Random;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>전설 고유 효과 3종의 순수 규칙(장비 문서 6장, 12장 단계 4, 수치는 2차 6-5).</summary>
    public sealed class LegendRulesTests
    {
        // ── 세기 보간 ──

        [Test]
        public void StrengthInterpolatesBetweenRangeEnds()
        {
            Assert.AreEqual(0.30, LegendRules.LightningChance(0), 1e-9);
            Assert.AreEqual(0.35, LegendRules.LightningChance(500), 1e-9);
            Assert.AreEqual(0.40, LegendRules.LightningChance(1000), 1e-9);
            Assert.AreEqual(110, LegendRules.LightningPercent(0), 1e-9);
            Assert.AreEqual(130, LegendRules.LightningPercent(1000), 1e-9);
            Assert.AreEqual(50, LegendRules.FlamePercent(0), 1e-9);
            Assert.AreEqual(60, LegendRules.FlamePercent(1000), 1e-9);
            Assert.AreEqual(150, LegendRules.BlastPercent(0), 1e-9);
            Assert.AreEqual(165, LegendRules.BlastPercent(500), 1e-9, "이미 가진 효과의 아래끝(위쪽 절반, 2차 165~180%)");
            Assert.AreEqual(180, LegendRules.BlastPercent(1000), 1e-9);
        }

        [Test]
        public void StrengthOutsideRangeIsClamped()
        {
            Assert.AreEqual(0.30, LegendRules.LightningChance(-1), 1e-9);
            Assert.AreEqual(0.40, LegendRules.LightningChance(5000), 1e-9);
            Assert.AreEqual(150, LegendRules.BlastPercent(-200), 1e-9);
            Assert.AreEqual(60, LegendRules.FlamePercent(1001), 1e-9);
        }

        [Test]
        public void NumbersMatchDesignTable()
        {
            Assert.AreEqual(0.2, LegendRules.LightningCooldown, 1e-9);
            Assert.AreEqual(4, LegendRules.LightningMaxBounces);
            Assert.AreEqual(4.0, LegendRules.LightningRange, 1e-9);
            Assert.AreEqual(1.2, LegendRules.FlameStepDistance, 1e-9);
            Assert.AreEqual(3.0, LegendRules.CombatWindow, 1e-9);
            Assert.AreEqual(0.8, LegendRules.FlameRadius, 1e-9);
            Assert.AreEqual(2.5, LegendRules.FlameLifetime, 1e-9);
            Assert.AreEqual(0.5, LegendRules.FlameTickInterval, 1e-9);
            Assert.AreEqual(0.7, LegendRules.FlameSlowFactor, 1e-9);
            Assert.AreEqual(2.5, LegendRules.BlastRadius, 1e-9);
            Assert.AreEqual(12, LegendRules.BlastMaxChain);
            Assert.AreEqual(0.1, LegendRules.BlastInterval, 1e-9);
            Assert.IsTrue(LegendRules.LightningCanCrit);
            Assert.IsFalse(LegendRules.BlastCanCrit);
        }

        // ── 연쇄 번개 ──

        [Test]
        public void LightningRollsOncePerAction()
        {
            var gate = new LightningGate();
            // 동작 1: 첫 사건에서 굴려 실패하면 같은 동작의 다음 타(쌍검 연타·여러 판정)는 굴리지 않는다.
            Assert.IsTrue(gate.IsRollMoment(1, 0.0));
            Assert.IsFalse(gate.IsRollMoment(1, 0.05));
            Assert.IsFalse(gate.TryProc(1, 0.1, 0.0, 1000), "같은 동작은 난수가 0이어도 다시 굴리지 않는다");
            // 동작 2: 번호가 바뀐 첫 사건에서만.
            Assert.IsTrue(gate.TryProc(2, 0.6, 0.0, 0));
            Assert.IsFalse(gate.TryProc(2, 0.7, 0.0, 0));
        }

        [Test]
        public void LightningFailedRollConsumesTheAction()
        {
            var gate = new LightningGate();
            Assert.IsFalse(gate.TryProc(5, 0.0, 0.99, 1000), "40%보다 큰 난수는 실패");
            Assert.IsFalse(gate.TryProc(5, 0.1, 0.0, 1000), "실패한 동작은 다음 타에서 다시 굴리지 않는다");
            Assert.IsTrue(gate.TryProc(6, 0.5, 0.39, 1000), "세기 1000이면 40% 아래는 성공");
            Assert.IsFalse(gate.TryProc(7, 1.0, 0.30, 0), "세기 0이면 30% 이상은 실패");
        }

        [Test]
        public void LightningWaitsPointTwoSecondsAfterProc()
        {
            var gate = new LightningGate();
            Assert.IsTrue(gate.TryProc(1, 1.0, 0.0, 0));
            Assert.AreEqual(1.2, gate.ReadyAt, 1e-9);
            Assert.IsFalse(gate.TryProc(2, 1.1, 0.0, 0), "0.2초 안의 다음 동작은 굴리지 않는다");
            Assert.IsFalse(gate.TryProc(2, 1.25, 0.0, 0), "대기에 걸린 동작은 그 동작 안에서 다시 굴리지 않는다");
            Assert.IsTrue(gate.TryProc(3, 1.2, 0.0, 0), "0.2초가 지나면 다시 발동");
        }

        [Test]
        public void LightningChanceHoldsOverManyActions()
        {
            foreach (int strength in new[] { 0, 1000 })
            {
                var gate = new LightningGate();
                var rng = new Pcg32Random(77, 3);
                int procs = 0;
                const int actions = 100000;
                for (int i = 0; i < actions; i++)
                {
                    double t = i * 0.5;
                    // 동작마다 판정 둘(쌍검 연타처럼): 굴림은 첫 사건 한 번뿐이다.
                    if (gate.TryProc(i, t, rng.NextDouble(), strength)) procs++;
                    if (gate.TryProc(i, t + 0.1, rng.NextDouble(), strength)) procs++;
                }
                double expected = LegendRules.LightningChance(strength);
                Assert.AreEqual(expected, procs / (double)actions, 0.01, $"세기 {strength}");
            }
        }

        [Test]
        public void ChainBouncesAtMostFourTimes()
        {
            var points = new List<ChainPoint>();
            for (int i = 0; i < 8; i++) points.Add(new ChainPoint(i * 1.0, 0));
            var chain = LegendRules.PickChain(points, 0, LegendRules.LightningMaxBounces, LegendRules.LightningRange);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, chain);
        }

        [Test]
        public void ChainNeverHitsTheSameEnemyTwice()
        {
            // 셋이 한데 모여 있으면 왔다 갔다 하지 않고, 처음 맞은 적(시작)도 다시 고르지 않는다.
            var points = new List<ChainPoint>
            {
                new ChainPoint(0, 0),
                new ChainPoint(0.5, 0),
                new ChainPoint(-0.6, 0),
            };
            var chain = LegendRules.PickChain(points, 0, LegendRules.LightningMaxBounces, LegendRules.LightningRange);
            CollectionAssert.AreEqual(new[] { 1, 2 }, chain);
            CollectionAssert.DoesNotContain(chain, 0);
            Assert.AreEqual(chain.Count, new HashSet<int>(chain).Count);
        }

        [Test]
        public void ChainUsesRangeToBodyEdgeAndSkipsBlocked()
        {
            var points = new List<ChainPoint>
            {
                new ChainPoint(0, 0),
                new ChainPoint(5, 0),                 // 가장자리 5: 너무 멀다
                new ChainPoint(0, 5, 1.2),            // 가장자리 3.8: 닿는다
                new ChainPoint(1, 0, 0, false),       // 쓰러짐: 건너뜀
                new ChainPoint(-2, 0),                // 벽 너머(canLink false)
            };
            var chain = LegendRules.PickChain(points, 0, 4, LegendRules.LightningRange, (a, b) => b != 4);
            CollectionAssert.AreEqual(new[] { 2 }, chain);
        }

        [Test]
        public void ChainIsEmptyWithoutOtherTargets()
        {
            var points = new List<ChainPoint> { new ChainPoint(0, 0) };
            Assert.AreEqual(0, LegendRules.PickChain(points, 0, 4, 4).Count);
            Assert.AreEqual(0, LegendRules.PickChain(points, 3, 4, 4).Count, "시작 번호가 없으면 빈 목록");
            Assert.AreEqual(0, LegendRules.PickChain(null, 0, 4, 4).Count);
        }

        // ── 불꽃 발자국 ──

        [Test]
        public void FlameStepsEveryOnePointTwoUnits()
        {
            var c = new FlameStepCounter();
            var offsets = new List<double>();
            Assert.AreEqual(0, c.Advance(0.5, true, offsets));
            Assert.AreEqual(0, c.Advance(0.5, true, offsets));
            Assert.AreEqual(1, c.Advance(0.5, true, offsets));
            Assert.AreEqual(0.2, offsets[0], 1e-9, "1.2를 넘는 자리(이번 걸음 시작에서 0.2)");
            Assert.AreEqual(0.3, c.Carry, 1e-9);
            Assert.AreEqual(1, c.Advance(1.9, true, offsets));
            Assert.AreEqual(0.9, offsets[0], 1e-9);
            Assert.AreEqual(1.0, c.Carry, 1e-9);
            Assert.AreEqual(2, c.Advance(1.9, true, offsets), "한 번에 두 칸을 넘으면 둘");
            CollectionAssert.AreEqual(new[] { 0.2, 1.4 }, offsets.ConvertAll(o => System.Math.Round(o, 9)));
        }

        [Test]
        public void FlameStepsFollowDistanceNotTime()
        {
            // 걷기 5.3이든 장화 이동 +22%(6.47)든, 프레임이 30이든 144든 12유닛을 걸으면 불길 10개(간격이 끊기지 않음).
            foreach (double speed in new[] { 5.3, 6.466 })
            foreach (double fps in new[] { 30.0, 60.0, 144.0 })
            {
                var c = new FlameStepCounter();
                int count = 0;
                double walked = 0;
                while (walked < 12.0 - 1e-12)
                {
                    double step = System.Math.Min(speed / fps, 12.0 - walked);
                    count += c.Advance(step, true);
                    walked += step;
                }
                Assert.AreEqual(10, count, $"속도 {speed}, 초당 {fps}프레임");
            }
        }

        [Test]
        public void FlameStepsOnlyInCombat()
        {
            Assert.IsTrue(LegendRules.InCombat(10.0, 7.0), "마지막 공격·피격에서 3초까지는 전투 중");
            Assert.IsFalse(LegendRules.InCombat(10.0, 6.99));
            Assert.IsFalse(LegendRules.InCombat(10.0, -999));

            var c = new FlameStepCounter();
            Assert.AreEqual(0, c.Advance(1.0, true));
            Assert.AreEqual(0, c.Advance(1.0, false), "전투가 아니면 깔지 않고 모은 거리를 비운다");
            Assert.AreEqual(0.0, c.Carry, 1e-9);
            Assert.AreEqual(0, c.Advance(1.0, true));
            Assert.AreEqual(1, c.Advance(0.2, true));
        }

        [Test]
        public void FlameIgnoresTeleport()
        {
            var c = new FlameStepCounter();
            c.Advance(1.0, true);
            Assert.AreEqual(0, c.Advance(5.0, true), "순간 이동(2유닛 넘게)은 걸음이 아니다");
            Assert.AreEqual(0.0, c.Carry, 1e-9);
        }

        [Test]
        public void FlameBurnsEachEnemyEveryHalfSecond()
        {
            Assert.IsTrue(LegendRules.CanBurn(0.0, double.NegativeInfinity), "처음은 바로 탄다");
            Assert.IsFalse(LegendRules.CanBurn(0.49, 0.0));
            Assert.IsTrue(LegendRules.CanBurn(0.5, 0.0));
            // 불길 둘이 겹쳐도 같은 적의 마지막 탄 시각은 하나라 0.5초에 한 번.
            double last = double.NegativeInfinity;
            int burns = 0;
            for (int frame = 0; frame <= 150; frame++)
            {
                double t = frame / 60.0;
                for (int patch = 0; patch < 2; patch++)
                {
                    if (!LegendRules.CanBurn(t, last)) continue;
                    last = t;
                    burns++;
                }
            }
            Assert.AreEqual(6, burns, "2.5초 동안 0, 0.5, 1.0, 1.5, 2.0, 2.5");
        }

        [Test]
        public void SlowKeepsStrongestAndLongest()
        {
            LegendRules.MergeSlow(0.7, 1.0, 0.9, 2.0, 0.5, out double f, out double u);
            Assert.AreEqual(0.7, f, 1e-9);
            Assert.AreEqual(2.0, u, 1e-9);
            LegendRules.MergeSlow(0.9, 3.0, 0.7, 1.0, 0.5, out f, out u);
            Assert.AreEqual(0.7, f, 1e-9);
            Assert.AreEqual(3.0, u, 1e-9);
            LegendRules.MergeSlow(0.5, 1.0, 0.9, 2.0, 1.5, out f, out u);
            Assert.AreEqual(0.9, f, 1e-9, "끝난 느려짐은 남지 않는다");
            Assert.AreEqual(2.0, u, 1e-9);
            LegendRules.MergeSlow(1.0, 0.0, 1.5, 2.0, 0.0, out f, out _);
            Assert.AreEqual(1.0, f, 1e-9, "배율은 1을 넘지 않는다");
        }

        // ── 연쇄 폭발 ──

        [Test]
        public void BlastOnlyFromNonBossKills()
        {
            Assert.IsTrue(LegendRules.TriggersBlast(false));
            Assert.IsFalse(LegendRules.TriggersBlast(true));
        }

        [Test]
        public void BlastChainCapsAtTwelve()
        {
            var chain = new BlastChain();
            int scheduled = 0;
            for (int i = 0; i < 30; i++)
                if (chain.TrySchedule(0.0, out double at))
                {
                    Assert.AreEqual(scheduled * 0.1, at, 1e-9);
                    scheduled++;
                }
            Assert.AreEqual(12, scheduled);
            Assert.IsTrue(chain.Full);
            Assert.IsFalse(chain.TrySchedule(5.0, out _));
        }

        [Test]
        public void BlastChainFlowsEveryPointOneSecond()
        {
            var chain = new BlastChain();
            Assert.IsTrue(chain.TrySchedule(1.0, out double a));
            Assert.AreEqual(1.0, a, 1e-9, "첫 폭발은 처치 순간");
            Assert.IsTrue(chain.TrySchedule(1.0, out double b));
            Assert.AreEqual(1.1, b, 1e-9);
            Assert.IsTrue(chain.TrySchedule(1.15, out double c));
            Assert.AreEqual(1.2, c, 1e-9, "앞 폭발 + 0.1초");
            Assert.IsTrue(chain.TrySchedule(2.0, out double d));
            Assert.AreEqual(2.0, d, 1e-9, "늦게 온 처치는 바로");
        }

        [Test]
        public void BlastJuiceOnlyOncePerChain()
        {
            var chain = new BlastChain();
            Assert.IsTrue(chain.TakeJuice());
            Assert.IsFalse(chain.TakeJuice());
            Assert.IsFalse(chain.TakeJuice());
            Assert.IsTrue(new BlastChain().TakeJuice(), "새 연쇄는 다시 한 번");
        }

        [Test]
        public void LongRowOfEnemiesStopsAtTwelveBlasts()
        {
            // 굴쥐 30마리가 2유닛 간격으로 한 줄: 폭발마다 옆 한 마리가 죽어 다시 터진다. 연쇄는 12번에서 멈춘다.
            const int n = 30;
            var dead = new bool[n];
            var chain = new BlastChain();
            var queue = new Queue<(int index, double at)>();
            double now = 0;
            dead[0] = true;
            Assert.IsTrue(chain.TrySchedule(now, out double first));
            queue.Enqueue((0, first));
            int explosions = 0;
            int juice = 0;
            double lastAt = -1;
            while (queue.Count > 0)
            {
                var (index, at) = queue.Dequeue();
                Assert.GreaterOrEqual(at - lastAt, lastAt < 0 ? 0 : 0.1 - 1e-9, "폭발 사이 0.1초");
                lastAt = at;
                now = at;
                explosions++;
                int kills = 0;
                for (int j = 0; j < n; j++)
                {
                    if (dead[j] || System.Math.Abs(j - index) * 2.0 > LegendRules.BlastRadius) continue;
                    dead[j] = true;
                    kills++;
                    if (chain.TrySchedule(now, out double next)) queue.Enqueue((j, next));
                }
                if (kills > 0 && chain.TakeJuice()) juice++;
            }
            Assert.AreEqual(12, explosions);
            Assert.AreEqual(1, juice, "여러 마리 처치 연출은 연쇄 하나에 처음 한 번");
            Assert.AreEqual(1.1, lastAt, 1e-9, "12번째 폭발은 1.1초");
        }
    }
}
