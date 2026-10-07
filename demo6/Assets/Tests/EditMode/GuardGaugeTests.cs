using Demo6.Core.Combat;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 방어 게이지(기획/세-무기-우클릭-소켓-1차.md 2-5, 0-3의 28, 2026-10-05 사용자 "방어했을때는 방어 게이지도 있으면 좋을것 같고"):
    /// 막은 공격 세기만큼 줄어듦, 쉬는 시간 뒤 회복(막는 중은 아주 느리게), 0이면 깨짐, 패링 환급, 깨진 뒤 절반까지 차야 다시 듦,
    /// 최대치·회복 빠르기·환급 손잡이, 막대 보이기·깎인 꼬리. 시간은 0.01초씩 직접 흘린다(Time 없음).
    /// </summary>
    public sealed class GuardGaugeTests
    {
        const float Eps = 1e-3f;
        const float Dt = 0.01f;

        /// <summary>몸이 +x를 보고 바로 앞(거리 1.2)에서 온 막을 수 있는 타의 질의. 게이지 값·최대치는 그 게이지 것.</summary>
        static GuardQuery Front(GuardGauge g, HitKind kind, float percent = 100f, bool parry = false, float? refund = null) => new GuardQuery
        {
            Kind = kind,
            Guarding = true,
            ParryOpen = parry,
            FacingX = 1f,
            FacingY = 0f,
            ToSourceX = 1f,
            ToSourceY = 0f,
            HasTravel = false,
            SourceDistance = 1.2f,
            PatternPercent = percent,
            Meter = g.Value,
            GeneralDamageScale = 0.25f,
            MeterMax = g.Max,
            Refund = refund,
        };

        /// <summary>막기 가르기 → 게이지에 넣기(PlayerController.BlockHit·ParryHit와 같은 길).</summary>
        static GuardOutcome Take(GuardGauge g, float now, HitKind kind, float percent = 100f, bool parry = false, float? refund = null)
        {
            var o = ShieldRule.Resolve(Front(g, kind, percent, parry, refund));
            g.Apply(o, now);
            return o;
        }

        /// <summary>t에서 until까지 Dt씩 흘린다.</summary>
        static void Run(GuardGauge g, ref float t, float until, bool guarding, float rateScale = 1f)
        {
            while (t < until - 1e-4f)
            {
                t += Dt;
                g.Tick(t, Dt, guarding, rateScale);
            }
        }

        // ── 줄어듦 ──

        /// <summary>막을 때마다 막은 공격의 세기만큼 줄어든다: 굴쥐 물기 25, 화살 10, 멧돼지 돌진 30, 오우거 휩쓸기 40. 못 막은 타는 그대로.</summary>
        [Test]
        public void ShrinksByBlockedAttackStrength()
        {
            var g = new GuardGauge();
            Assert.AreEqual(100f, g.Value, Eps);
            Assert.AreEqual(1f, g.Fraction, Eps);

            Assert.AreEqual(HitResult.Blocked, Take(g, 0f, HitKind.Melee, 100f).Result);
            Assert.AreEqual(75f, g.Value, Eps, "굴쥐 물기 100% → 25");
            Take(g, 0f, HitKind.Melee, 60f);
            Assert.AreEqual(60f, g.Value, Eps, "멧돼지 머리치기 60% → 15");
            Take(g, 0f, HitKind.Arrow);
            Assert.AreEqual(50f, g.Value, Eps, "화살 10");
            Take(g, 0f, HitKind.Rush);
            Assert.AreEqual(20f, g.Value, Eps, "멧돼지 돌진 30");
            Assert.IsFalse(g.Recovering);

            // 뒤에서 온 타(못 막음)는 게이지를 바꾸지 않는다.
            var back = Front(g, HitKind.Melee);
            back.ToSourceX = -1f;
            var hit = ShieldRule.Resolve(back);
            Assert.AreEqual(HitResult.Hit, hit.Result);
            g.Apply(hit, 0f);
            Assert.AreEqual(20f, g.Value, Eps);

            var boss = new GuardGauge();
            Take(boss, 0f, HitKind.BossSweep, 110f);
            Assert.AreEqual(60f, boss.Value, Eps, "오우거 휩쓸기 40");
        }

        // ── 지연 회복 ──

        /// <summary>막지 않으면 마지막 막기 0.5초 뒤부터 초당 50, 막는 중이면 1.0초 뒤부터 초당 10(아주 느리게). 가득에서 멈춘다.</summary>
        [Test]
        public void RefillsAfterShortDelayAndBarelyWhileGuarding()
        {
            var g = new GuardGauge();
            float t = 0f;
            Take(g, t, HitKind.Melee);
            Take(g, t, HitKind.Melee);
            Assert.AreEqual(50f, g.Value, Eps);

            Run(g, ref t, 0.49f, false);
            Assert.AreEqual(50f, g.Value, Eps, "0.5초 전에는 그대로");
            Run(g, ref t, 0.7f, false);
            Assert.AreEqual(60f, g.Value, 0.6f, "0.5초부터 초당 50");
            Run(g, ref t, 1.6f, false);
            Assert.AreEqual(100f, g.Value, Eps, "가득에서 멈춤");

            // 막는 중: 1.0초 쉬고 초당 10.
            var h = new GuardGauge();
            float u = 0f;
            Take(h, u, HitKind.Melee);
            Run(h, ref u, 0.99f, true);
            Assert.AreEqual(75f, h.Value, Eps, "막는 중 1.0초 전에는 그대로");
            Run(h, ref u, 2.0f, true);
            Assert.AreEqual(85f, h.Value, 0.2f, "막는 중 초당 10");
            Assert.Less(ShieldRule.RegenGuardPerSecond * 5f, ShieldRule.RegenIdlePerSecond + Eps, "막는 중은 쉴 때의 1/5 이하");

            // 계속 막아 맞는 동안에는 쉬는 시간이 다시 시작돼 차지 않는다(굴쥐가 0.8초마다 물면 순수하게 줄기만 함).
            var k = new GuardGauge();
            float s = 0f;
            for (int i = 0; i < 3; i++)
            {
                Take(k, s, HitKind.Melee);
                Run(k, ref s, s + 0.8f, true);
            }
            Assert.AreEqual(25f, k.Value, Eps);
        }

        /// <summary>회복 빠르기 손잡이는 두 빠르기에 곱하고 쉬는 시간은 그대로다. 최대치 손잡이는 자르는 값만 바꾼다.</summary>
        [Test]
        public void RegenKnobsScaleRateAndMax()
        {
            Assert.AreEqual(155f, ShieldRule.Regen(150f, 5f, false, 0.1f, 200f, 1f), Eps, "최대치 200이면 100 위로도 참");
            Assert.AreEqual(200f, ShieldRule.Regen(250f, 5f, false, 0.1f, 200f, 1f), Eps, "최대치 위면 내림");
            Assert.AreEqual(55f, ShieldRule.Regen(40f, 5f, false, 0.1f, 100f, 3f), Eps, "빠르기 × 3");
            Assert.AreEqual(41.5f, ShieldRule.Regen(40f, 5f, true, 0.1f, 100f, 1.5f), Eps, "막는 중도 곱함");
            Assert.AreEqual(40f, ShieldRule.Regen(40f, 0.49f, false, 0.1f, 100f, 3f), Eps, "쉬는 시간은 그대로");
            Assert.AreEqual(40f, ShieldRule.Regen(40f, 5f, false, 0.1f, 100f, 0f), Eps, "빠르기 0이면 안 참");
            Assert.AreEqual(ShieldRule.Regen(40f, 5f, false, 0.1f), ShieldRule.Regen(40f, 5f, false, 0.1f, 0f, 1f), Eps, "최대치 0 이하면 기본 100");

            var g = new GuardGauge();
            float t = 0f;
            Take(g, t, HitKind.Melee);
            Take(g, t, HitKind.Melee);
            Run(g, ref t, 0.7f, false, 2f);
            Assert.AreEqual(70f, g.Value, 1.1f, "빠르기 × 2: 0.5초부터 초당 100");
        }

        // ── 깨짐 ──

        /// <summary>0이 되면 막기 깨짐: 굴쥐 물기 넷째에 깨지고 0에 머문다. 내려찍기는 최대치와 상관없이 늘 깨진다. 최대치 200이면 여덟째.</summary>
        [Test]
        public void BreaksAtZero()
        {
            var g = new GuardGauge();
            GuardOutcome o = default;
            int bites = 0;
            while (!g.Recovering && bites < 20)
            {
                o = Take(g, 0f, HitKind.Melee);
                bites++;
            }
            Assert.AreEqual(4, bites);
            Assert.IsTrue(o.Broke);
            Assert.AreEqual(0f, g.Value, Eps);
            Assert.IsTrue(g.Recovering);
            Assert.IsFalse(g.CanRaise);

            var big = new GuardGauge(200f);
            bites = 0;
            while (!big.Recovering && bites < 20)
            {
                Take(big, 0f, HitKind.Melee);
                bites++;
            }
            Assert.AreEqual(8, bites, "최대치를 올려도 깎는 양은 같다");

            foreach (float max in new[] { 50f, 100f, 200f })
            {
                var slam = new GuardGauge(max);
                var s = Take(slam, 0f, HitKind.BossSlam, 250f);
                Assert.AreEqual(HitResult.Blocked, s.Result);
                Assert.IsTrue(s.Broke, "내려찍기는 늘 깨짐 " + max);
                Assert.AreEqual(0f, s.MeterAfter, Eps);
                Assert.AreEqual(0f, slam.Value, Eps);
                Assert.IsTrue(slam.Recovering);
            }
        }

        // ── 패링 환급 ──

        /// <summary>
        /// 패링 성공은 깎지 않고 20을 돌려준다(최대치에서 자름). 같은 굴쥐 물기를 막으면 −25, 튕기면 +20이라 정확히 막는 쪽이 45 이득이다.
        /// 환급 손잡이(0~50)를 따르고, 튕긴 순간 0.2초 번쩍인다. 튕김도 회복 쉬는 시간을 다시 센다.
        /// </summary>
        [Test]
        public void ParryRefundsGauge()
        {
            var g = new GuardGauge();
            Take(g, 0f, HitKind.Melee);
            Take(g, 0f, HitKind.Melee);
            Take(g, 0f, HitKind.Melee);
            Assert.AreEqual(25f, g.Value, Eps);
            var p = Take(g, 1f, HitKind.Melee, parry: true);
            Assert.AreEqual(HitResult.Parried, p.Result);
            Assert.AreEqual(45f, g.Value, Eps, "환급 20");
            Assert.AreEqual(1f, g.ParryFlash(1f), Eps);
            Assert.AreEqual(0.5f, g.ParryFlash(1.1f), Eps);
            Assert.AreEqual(0f, g.ParryFlash(1.25f), Eps);
            Assert.AreEqual(1f, g.LastDrainTime, Eps, "튕김도 쉬는 시간을 다시 셈");

            var blocked = new GuardGauge();
            var parried = new GuardGauge();
            Take(blocked, 0f, HitKind.Melee);
            Take(blocked, 0f, HitKind.Melee);
            Take(parried, 0f, HitKind.Melee);
            Take(parried, 0f, HitKind.Melee);
            Take(blocked, 0f, HitKind.Melee);
            Take(parried, 0f, HitKind.Melee, parry: true);
            Assert.AreEqual(45f, parried.Value - blocked.Value, Eps, "정확히 막으면 45 이득");

            var full = new GuardGauge();
            Take(full, 0f, HitKind.Arrow);
            Take(full, 0f, HitKind.Arrow, parry: true);
            Assert.AreEqual(100f, full.Value, Eps, "가득에서 자름");

            var knob = new GuardGauge();
            Take(knob, 0f, HitKind.Melee);
            Take(knob, 0f, HitKind.Melee, parry: true, refund: 0f);
            Assert.AreEqual(75f, knob.Value, Eps, "환급 0");
            Take(knob, 0f, HitKind.Melee, parry: true, refund: 50f);
            Assert.AreEqual(100f, knob.Value, Eps, "환급 50");
            Take(knob, 0f, HitKind.Melee);
            Take(knob, 0f, HitKind.Melee, parry: true, refund: -5f);
            Assert.AreEqual(75f, knob.Value, Eps, "음수 환급은 0");

            var big = new GuardGauge(200f);
            Take(big, 0f, HitKind.Melee);
            Take(big, 0f, HitKind.Melee, parry: true);
            Assert.AreEqual(195f, big.Value, Eps, "최대치 200이면 100 위로도 돌려받음");
        }

        // ── 깨진 뒤 규칙 ──

        /// <summary>
        /// 깨진 뒤에는 게이지가 최대치의 절반까지 다시 차야 들 수 있다. 기본 빠르기에서 그 순간은 0.5 + 50 ÷ 50 = 1.5초로 시간 잠금(BreakLock)과 같다.
        /// 빠르기 × 0.5면 2.5초, 최대치 200이면 문턱 100이라 2.5초. 채움(다시 섬)은 바로 풀어 준다. 깨진 동안 막대는 보인다.
        /// </summary>
        [Test]
        public void AfterBreakWaitsForHalfGauge()
        {
            Assert.AreEqual(0.5f, ShieldRule.RaiseAfterBreakFraction, Eps);
            Assert.AreEqual(ShieldRule.BreakLock, ShieldRule.RegenIdleDelay + ShieldRule.GuardMax * ShieldRule.RaiseAfterBreakFraction / ShieldRule.RegenIdlePerSecond, Eps,
                "기본값에서 문턱에 닿는 순간 = 시간 잠금");

            Assert.AreEqual(1.5f, TimeToRaise(100f, 1f), 0.03f);
            Assert.AreEqual(2.5f, TimeToRaise(100f, 0.5f), 0.03f);
            Assert.AreEqual(2.5f, TimeToRaise(200f, 1f), 0.03f);

            var g = new GuardGauge();
            g.Break(0f);
            Assert.IsFalse(g.CanRaise);
            Assert.AreEqual(50f, g.RaiseThreshold, Eps);
            float t = 0f;
            Run(g, ref t, 1.2f, false);
            Assert.IsFalse(g.CanRaise, "1.2초에는 아직(게이지 약 35)");
            Assert.AreEqual(1f, g.Alpha(t), Eps, "깨진 뒤에는 막대가 보임");
            g.Fill();
            Assert.IsTrue(g.CanRaise, "채움은 바로 풂");
            Assert.AreEqual(100f, g.Value, Eps);
        }

        static float TimeToRaise(float max, float rateScale)
        {
            var g = new GuardGauge(max);
            g.Break(0f);
            float t = 0f;
            while (!g.CanRaise && t < 10f)
            {
                t += Dt;
                g.Tick(t, Dt, false, rateScale);
            }
            return t;
        }

        // ── 최대치 손잡이 ──

        /// <summary>최대치를 바꾸면 넘친 값만 내리고 모자란 값은 회복으로 찬다. 하한 10.</summary>
        [Test]
        public void MaxKnobClampsAndRefills()
        {
            var g = new GuardGauge();
            g.SetMax(60f);
            Assert.AreEqual(60f, g.Max, Eps);
            Assert.AreEqual(60f, g.Value, Eps, "넘친 값은 내림");
            g.SetMax(120f);
            Assert.AreEqual(60f, g.Value, Eps, "올리면 그대로");
            float t = 10f;
            Run(g, ref t, 11.3f, false);
            Assert.AreEqual(120f, g.Value, Eps, "회복으로 참");
            g.SetMax(0f);
            Assert.AreEqual(GuardGauge.MinMax, g.Max, Eps);
            Assert.LessOrEqual(GuardGauge.KnobMaxLow, ShieldRule.GuardMax);
            Assert.GreaterOrEqual(GuardGauge.KnobMaxHigh, ShieldRule.GuardMax);
            Assert.LessOrEqual(GuardGauge.KnobRegenLow, 1f);
            Assert.GreaterOrEqual(GuardGauge.KnobRegenHigh, 1f);
            Assert.GreaterOrEqual(GuardGauge.KnobRefundHigh, ShieldRule.ParryRefund);
        }

        // ── 보이기·깎인 꼬리 ──

        /// <summary>
        /// 막대는 처음(가득·막지 않음)에는 안 보이고, 막는 동안·차는 동안 보이며, 가득 차고 막기를 내린 뒤 0.6초 머물다 0.25초에 사라진다.
        /// </summary>
        [Test]
        public void ShowsWhileGuardingOrRefillingThenFades()
        {
            var g = new GuardGauge();
            Assert.AreEqual(0f, g.Alpha(0f), Eps, "한 번도 안 막았으면 안 보임");
            float t = 0f;
            Run(g, ref t, 0.5f, false);
            Assert.AreEqual(0f, g.Alpha(t), Eps);

            Run(g, ref t, 1.0f, true);
            Assert.AreEqual(1f, g.Alpha(t), Eps, "막는 동안 보임");
            float lowered = t;
            Run(g, ref t, lowered + GuardGauge.ShowLinger, false);
            Assert.AreEqual(1f, g.Alpha(t), Eps, "내린 뒤 0.6초 머묾");
            Assert.AreEqual(0.5f, g.Alpha(lowered + GuardGauge.ShowLinger + GuardGauge.FadeTime * 0.5f), 0.05f);
            Assert.AreEqual(0f, g.Alpha(lowered + GuardGauge.ShowLinger + GuardGauge.FadeTime + 0.01f), Eps, "그 뒤 사라짐");

            // 막고 내린 뒤 차는 동안은 계속 보인다(0.5 쉼 + 25 ÷ 50 = 1.0초에 가득).
            var h = new GuardGauge();
            float u = 0f;
            Take(h, u, HitKind.Melee);
            Run(h, ref u, 0.95f, false);
            Assert.Less(h.Value, 100f);
            Assert.AreEqual(1f, h.Alpha(u), Eps, "차는 동안 보임");
            Run(h, ref u, 1.1f, false);
            Assert.AreEqual(100f, h.Value, Eps);
            Assert.AreEqual(1f, h.Alpha(u + 0.4f), Eps, "가득 찬 직후에도 잠깐 보임");
            Assert.AreEqual(0f, h.Alpha(u + 1.0f), Eps);
        }

        /// <summary>깎인 몫은 0.3초 그대로 남았다가 줄어 지금 값을 따라잡는다. 회복으로 값이 꼬리보다 커지면 꼬리는 값과 같다.</summary>
        [Test]
        public void TrailHoldsThenCatchesUp()
        {
            var g = new GuardGauge();
            float t = 0f;
            Take(g, t, HitKind.Melee);
            Assert.AreEqual(1f, g.TrailFraction, Eps, "막은 순간 꼬리는 막기 전 값");
            Assert.AreEqual(0.75f, g.Fraction, Eps);
            Run(g, ref t, 0.29f, false);
            Assert.AreEqual(1f, g.TrailFraction, Eps, "0.3초 머묾");
            Run(g, ref t, 0.6f, false);
            Assert.AreEqual(g.Fraction, g.TrailFraction, Eps, "그 뒤 따라잡음");
            Assert.GreaterOrEqual(g.TrailFraction, g.Fraction);

            var b = new GuardGauge();
            Take(b, 0f, HitKind.Melee);
            Take(b, 0f, HitKind.Melee);
            Take(b, 0f, HitKind.Melee);
            Take(b, 0f, HitKind.Melee);
            Assert.IsTrue(b.Recovering);
            Assert.AreEqual(0f, b.Fraction, Eps);
            Assert.AreEqual(1f, b.TrailFraction, Eps, "깨진 순간 꼬리는 처음 값(같은 순간 네 번 막음)");
        }

        // ── Resolve의 손잡이 값 ──

        /// <summary>Resolve는 질의의 최대치·환급을 쓴다(비어 있으면 100·20). 깨지면 게이지 0.</summary>
        [Test]
        public void ResolveReadsMaxAndRefund()
        {
            var q = new GuardQuery
            {
                Kind = HitKind.Melee,
                Guarding = true,
                ParryOpen = true,
                FacingX = 1f,
                ToSourceX = 1f,
                SourceDistance = 1.2f,
                PatternPercent = 100f,
                Meter = 190f,
                GeneralDamageScale = 0.25f,
                MeterMax = 200f,
            };
            Assert.AreEqual(200f, ShieldRule.Resolve(q).MeterAfter, Eps, "최대치 200에서 자름, 환급 기본 20");
            q.Meter = 150f;
            q.Refund = 30f;
            Assert.AreEqual(180f, ShieldRule.Resolve(q).MeterAfter, Eps);
            q.MeterMax = 0f;
            q.Refund = null;
            Assert.AreEqual(100f, ShieldRule.Resolve(q).MeterAfter, Eps, "최대치 0이면 기본 100(150은 100으로 자른 뒤 + 20 → 100)");
            q.ParryOpen = false;
            q.Meter = 30f;
            q.MeterMax = 200f;
            Assert.AreEqual(5f, ShieldRule.Resolve(q).MeterAfter, Eps);
            q.Meter = 20f;
            var o = ShieldRule.Resolve(q);
            Assert.IsTrue(o.Broke);
            Assert.AreEqual(0f, o.MeterAfter, Eps);
        }
    }
}
