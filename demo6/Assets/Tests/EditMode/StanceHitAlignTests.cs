using System;
using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Combat.Stance;
using Demo6.Core.Loot;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 세 무기 자세 맞춤(기획/세-무기-우클릭-소켓-1차.md 7-4): 판정 순간 맞춤이 공격 속도 0·300·400‰에서 정확히 h′,
    /// 시작·끝이 쉬는 자세(±0.001), 출력 길이 ≥ 0, 쌍검 날 각 범위, 설계 시간 = WeaponPresets, 매 호출 할당 없음.
    /// 여기서 맞추는 것은 '때'뿐이다. 칼 궤적이 판정 범위(부채꼴 각·사거리)를 덮는지는 재지 않는다(동작과 판정은 따로, 문서 0-1의 10).
    /// '쌍검 날 각 범위'는 판정이 아니라 칼이 몸·머리 위로 돌지 않게 하는 그림 범위다.
    /// </summary>
    public sealed class StanceHitAlignTests
    {
        const StanceWeapon S = StanceWeapon.SwordShield, G = StanceWeapon.Greatsword, T = StanceWeapon.Twinblades;
        static readonly int[] Speeds = { 0, 300, 400 };
        const float Eps = 1e-3f;

        static float Wrap(float deg) => StanceMath.WrapDeg(deg);

        static SwingPlan Plan(float duration, float hit, int speed) => SwingTiming.Plan(StanceMotionCatalog.DesignStep(duration, hit), speed);

        // ── 판정 순간 맞춤 ──

        [Test]
        public void SwordShieldHitsLineUpAtEverySpeed()
        {
            foreach (int a in Speeds)
            {
                // 베기 3연타(찌르기 없음, 2026-10-05): 세 단계 모두 판정 순간 날 0°(칼이 앞을 가로지름), 길이 1.
                for (int i = 0; i < SwordShieldPoses.StepCount; i++)
                {
                    SwordShieldPoses.DesignTime(i, out float d, out float h);
                    var plan = Plan(d, h, a);
                    var hit = WeaponStances.Combo(S, i, plan.FirstHit, plan.Duration, plan.FirstHit);
                    Assert.AreEqual(0f, Wrap(hit.Right.Angle), Eps, (i + 1) + "단계 날 공속 " + a);
                    Assert.GreaterOrEqual(hit.Right.Length, 0.95f, (i + 1) + "단계 길이 공속 " + a);
                    // 베기 방향: ①③ 오른 → 왼(판정 앞 날이 음수 쪽), ② 왼 → 오른(판정 앞 날이 양수 쪽).
                    var before = WeaponStances.Combo(S, i, plan.FirstHit * 0.8f, plan.Duration, plan.FirstHit);
                    if (i == 1) Assert.Greater(Wrap(before.Right.Angle), 5f, "② 되베기는 왼쪽에서 옴 공속 " + a);
                    else Assert.Less(Wrap(before.Right.Angle), -5f, (i + 1) + "단계는 오른쪽에서 옴 공속 " + a);
                }

                // 패링 반격 베기: ③ 마무리 베기와 같은 판정 자세(막기 자세에서 시작).
                SwordShieldPoses.DesignTime(2, out float d3, out float h3);
                var p3 = Plan(d3, h3, a);
                var hit3 = WeaponStances.Combo(S, 2, p3.FirstHit, p3.Duration, p3.FirstHit);
                var rip = WeaponStances.Riposte(S, p3.FirstHit, p3.Duration, p3.FirstHit);
                Assert.AreEqual(hit3.Right.X, rip.Right.X, 1e-5f);
                Assert.AreEqual(hit3.Right.Y, rip.Right.Y, 1e-5f);
                Assert.AreEqual(hit3.Right.Angle, rip.Right.Angle, 1e-4f);
                var ripStart = WeaponStances.Riposte(S, 0f, p3.Duration, p3.FirstHit);
                Assert.AreEqual(SwordShieldPoses.Guard.Right.X, ripStart.Right.X, 1e-5f, "반격은 막기 자세에서 시작");
            }
        }

        [Test]
        public void GreatswordHitsLineUpAtEverySpeed()
        {
            foreach (int a in Speeds)
                for (int i = 0; i < GreatswordPoses.StepCount; i++)
                {
                    GreatswordPoses.ComboDesign(i, out float d, out float h);
                    var plan = Plan(d, h, a);
                    var hit = WeaponStances.Combo(G, i, plan.FirstHit, plan.Duration, plan.FirstHit);
                    if (i == 0) Assert.AreEqual(0f, Wrap(hit.Right.Angle), 1f, "① 공속 " + a);
                    else
                    {
                        Assert.LessOrEqual(Math.Abs(Wrap(hit.Right.Angle)), 5f, (i + 1) + " 날 공속 " + a);
                        Assert.LessOrEqual(Math.Abs(hit.Right.Tilt), 5f, (i + 1) + " 기울기(앞 수평) 공속 " + a);
                    }
                    // 판정 자세는 공격 속도와 상관없이 같다.
                    var base0 = WeaponStances.Combo(G, i, h, d, h);
                    Assert.AreEqual(base0.Right.X, hit.Right.X, 1e-4f);
                    Assert.AreEqual(base0.Right.Angle, hit.Right.Angle, 1e-3f);
                }
        }

        [Test]
        public void GreatswordReleaseHitsFaceFront()
        {
            for (int lv = 1; lv <= 3; lv++)
            {
                GreatswordPoses.ReleaseDesign(lv, out float d, out float h);
                var step = GreatswordCharge.Release(lv);
                Assert.AreEqual(step.duration, d, 1e-5f);
                Assert.AreEqual(step.duration * step.hitMoment, h, 1e-4f);
                var hit = WeaponStances.Act(G, StanceMotionCatalog.Phase(WeaponActPhase.Release, h, release: lv));
                Assert.AreEqual(0f, Wrap(hit.Right.Angle), 1f, lv + "단계");
                Assert.LessOrEqual(Math.Abs(hit.Right.Tilt), 5f, lv + "단계 기울기");
                // 빛 줄은 판정 + 0.1초까지 그 단계 세기, 0.15초 뒤에는 꺼짐.
                Assert.AreEqual(GreatswordCharge.GlowByLevel[lv], hit.Glow, 1e-5f);
                Assert.AreEqual(GreatswordCharge.GlowByLevel[lv], WeaponStances.Act(G, StanceMotionCatalog.Phase(WeaponActPhase.Release, h + 0.1f, release: lv)).Glow, 1e-5f);
                Assert.AreEqual(0f, WeaponStances.Act(G, StanceMotionCatalog.Phase(WeaponActPhase.Release, h + 0.26f, release: lv)).Glow, 1e-5f);
            }
        }

        [Test]
        public void TwinbladeCutsCrossFrontAtEveryHitAndSpeed()
        {
            foreach (int a in Speeds)
                for (int i = 0; i < TwinbladePoses.StepCount; i++)
                {
                    var plan = StanceMotionCatalog.TwinPlan(i, a);
                    for (int k = 0; k < plan.Hits; k++)
                    {
                        float t = plan.HitTime(k);
                        var p = WeaponStances.Combo(T, i, t, plan.Duration, plan.FirstHit);
                        string what = $"{i + 1}단계 {k + 1}타 공속 {a}";
                        if (i == 0) Assert.AreEqual(0f, Wrap(p.Right.Angle), 1f, "① 우측 베기 오른칼 " + what);
                        else if (i == 1) Assert.AreEqual(0f, Wrap(p.Left.Angle), 1f, "② 좌측 베기 왼칼 " + what);
                        else if (k < 2)
                        {
                            // ③ 엇베기·④ 가위 가르기: 1타 오른칼, 2타 왼칼이 0°를 지남.
                            float blade = k == 0 ? p.Right.Angle : p.Left.Angle;
                            Assert.AreEqual(0f, Wrap(blade), 1f, what);
                        }
                        else
                        {
                            // ④ 셋째 타: ±24° 꽉 닫힌 X(바깥에서 안으로 닫힘, 오른칼 +, 왼칼 −).
                            Assert.AreEqual(TwinbladePoses.ScissorCrossDeg, Wrap(p.Right.Angle), 1f, what);
                            Assert.AreEqual(-TwinbladePoses.ScissorCrossDeg, Wrap(p.Left.Angle), 1f, what);
                        }
                    }
                }
        }

        [Test]
        public void FlurryHitsFollowSweepCenters()
        {
            for (int k = 0; k < TwinFlurry.HitCount; k++)
            {
                var p = TwinbladePoses.Flurry(TwinFlurry.HitTimes[k]);
                if (TwinFlurry.IsFinal(k))
                {
                    Assert.AreEqual(0f, Wrap(p.Right.Angle), 1f, "마지막 X 오른칼");
                    Assert.AreEqual(0f, Wrap(p.Left.Angle), 1f, "마지막 X 왼칼");
                }
                else
                {
                    float blade = TwinFlurry.RightHand(k) ? p.Right.Angle : p.Left.Angle;
                    Assert.AreEqual(TwinFlurry.SweepCenterDeg[k], Wrap(blade), 1f, (k + 1) + "타");
                }
            }
        }

        [Test]
        public void ExecutionsHitFrontWithoutOverheadSlam()
        {
            var sword = WeaponStances.Execution(S, 0.16f, 0.4f, 0.16f);
            Assert.AreEqual(0f, Wrap(sword.Right.Angle), Eps);
            // 3차: 처형도 칼 배율 없음(세 무기, 칼 그림은 늘 제 크기).
            foreach (var w in new[] { S, G, T })
                for (int i = 0; i <= 8; i++)
                    Assert.AreEqual(1f, WeaponStances.Execution(w, 0.05f * i, 0.4f, 0.16f).BladeScale, 1e-5f, w + " 칼 배율 " + (0.05f * i));
            var great = WeaponStances.Execution(G, 0.16f, 0.4f, 0.16f);
            Assert.LessOrEqual(Math.Abs(Wrap(great.Right.Angle)), 5f);
            Assert.LessOrEqual(Math.Abs(great.Right.Tilt), 1f);
            var twin = WeaponStances.Execution(T, 0.16f, 0.4f, 0.16f);
            Assert.AreEqual(0f, Wrap(twin.Right.Angle), Eps);
            Assert.AreEqual(0f, Wrap(twin.Left.Angle), Eps);
        }

        // ── 시작·끝 쉬는 자세 ──

        /// <param name="sizeTol">왼손 자리 허용 차(3차부터 크기·길이가 늘 1이라 보통 Eps).</param>
        static void AssertRest(StanceWeapon w, StancePose p, string what, bool checkScale = true, float sizeTol = Eps)
        {
            var r = WeaponStances.Rest(w);
            Assert.AreEqual(r.Right.X, p.Right.X, Eps, what + " 오른손 x");
            Assert.AreEqual(r.Right.Y, p.Right.Y, Eps, what + " 오른손 y");
            Assert.AreEqual(0f, Wrap(p.Right.Angle - r.Right.Angle), Eps, what + " 오른손 날");
            Assert.AreEqual(r.Right.Tilt, p.Right.Tilt, 0.01f, what + " 오른손 기울기");
            Assert.AreEqual(r.Right.Length, p.Right.Length, Eps, what + " 오른손 길이");
            Assert.AreEqual(r.Right.Size, p.Right.Size, sizeTol, what + " 오른손 크기");
            Assert.AreEqual(r.Left.X, p.Left.X, Math.Max(Eps, sizeTol * 0.2f), what + " 왼손 x");
            Assert.AreEqual(r.Left.Y, p.Left.Y, Math.Max(Eps, sizeTol * 0.2f), what + " 왼손 y");
            if (w != StanceWeapon.Greatsword) Assert.AreEqual(0f, Wrap(p.Left.Angle - r.Left.Angle), Eps, what + " 왼손 각");
            if (w == StanceWeapon.SwordShield)
            {
                Assert.AreEqual(0f, Wrap(p.ShieldAngle - r.ShieldAngle), Eps, what + " 방패각");
                Assert.AreEqual(r.ShieldDepth, p.ShieldDepth, Eps, what + " 방패깊이");
            }
            Assert.AreEqual(r.Flip, p.Flip, Eps, what + " 세움");
            Assert.AreEqual(0f, Wrap(p.Twist), Eps, what + " 비틀기");
            Assert.AreEqual(0f, p.LeanX, Eps, what + " 앞뒤");
            if (checkScale)
            {
                Assert.AreEqual(1f, p.ScaleX, Eps, what + " 몸 가로");
                Assert.AreEqual(1f, p.ScaleY, Eps, what + " 몸 세로");
            }
        }

        [Test]
        public void CombosStartAndEndAtRest()
        {
            foreach (int a in Speeds)
            {
                for (int i = 0; i < SwordShieldPoses.StepCount; i++)
                {
                    SwordShieldPoses.DesignTime(i, out float d, out float h);
                    var p = Plan(d, h, a);
                    AssertRest(S, WeaponStances.Combo(S, i, 0f, p.Duration, p.FirstHit), $"한손검 {i + 1} 시작 공속 {a}");
                    AssertRest(S, WeaponStances.Combo(S, i, p.Duration, p.Duration, p.FirstHit), $"한손검 {i + 1} 끝 공속 {a}");
                }
                for (int i = 0; i < GreatswordPoses.StepCount; i++)
                {
                    GreatswordPoses.ComboDesign(i, out float d, out float h);
                    var p = Plan(d, h, a);
                    AssertRest(G, WeaponStances.Combo(G, i, 0f, p.Duration, p.FirstHit), $"대검 {i + 1} 시작 공속 {a}");
                    AssertRest(G, WeaponStances.Combo(G, i, p.Duration, p.Duration, p.FirstHit), $"대검 {i + 1} 끝 공속 {a}");
                }
                for (int i = 0; i < TwinbladePoses.StepCount; i++)
                {
                    var p = StanceMotionCatalog.TwinPlan(i, a);
                    AssertRest(T, WeaponStances.Combo(T, i, 0f, p.Duration, p.FirstHit), $"쌍검 {i + 1} 시작 공속 {a}");
                    // ④ 가위 가르기도 쉬는 자세(앞 겨눔)로 끝난다(회전 가르기는 없앰).
                    AssertRest(T, WeaponStances.Combo(T, i, p.Duration, p.Duration, p.FirstHit), $"쌍검 {i + 1} 끝 공속 {a}");
                }
            }
        }

        [Test]
        public void WeaponActsStartAndEndAtRest()
        {
            AssertRest(S, WeaponStances.Act(S, StanceMotionCatalog.Phase(WeaponActPhase.Raise, 0f)), "들기 시작");
            AssertRest(S, WeaponStances.Act(S, StanceMotionCatalog.Phase(WeaponActPhase.Lower, ShieldRule.LowerTime)), "내리기 끝");
            AssertRest(S, WeaponStances.Act(S, StanceMotionCatalog.Phase(WeaponActPhase.Break, ShieldRule.BreakStagger)), "깨짐 끝");
            var guard = SwordShieldPoses.Guard;
            foreach (var ph in new[] { WeaponActPhase.Recoil, WeaponActPhase.ParryPush })
            {
                float end = ph == WeaponActPhase.Recoil ? ShieldRule.RecoilTime : ShieldRule.ParryPushTime;
                var p = WeaponStances.Act(S, StanceMotionCatalog.Phase(ph, end));
                Assert.AreEqual(guard.Left.X, p.Left.X, Eps, ph + " 끝은 막기 자세");
                Assert.AreEqual(guard.ShieldAngle, p.ShieldAngle, Eps);
            }
            var bossEnd = WeaponStances.Act(S, StanceMotionCatalog.Phase(WeaponActPhase.Recoil, ShieldRule.BossRecoilTime, boss: true));
            Assert.AreEqual(guard.Left.X, bossEnd.Left.X, Eps);

            AssertRest(G, WeaponStances.Act(G, StanceMotionCatalog.Phase(WeaponActPhase.Charging, 0f, held: 0f)), "기 모으기 시작");
            for (int lv = 1; lv <= 3; lv++)
            {
                GreatswordPoses.ReleaseDesign(lv, out float d, out _);
                AssertRest(G, WeaponStances.Act(G, StanceMotionCatalog.Phase(WeaponActPhase.Release, d, release: lv)), lv + "단계 놓기 끝");
            }
            AssertRest(G, WeaponStances.Act(G, StanceMotionCatalog.Phase(WeaponActPhase.Flinch, GreatswordPoses.InterruptDuration, held: 1f, flinch: WeaponActKind.Charge)), "끊김 끝");

            AssertRest(T, WeaponStances.Act(T, StanceMotionCatalog.Phase(WeaponActPhase.Flurry, 0f)), "난사 시작");
            AssertRest(T, WeaponStances.Act(T, StanceMotionCatalog.Phase(WeaponActPhase.Flurry, TwinFlurry.Duration)), "난사 끝");

            // 끊김(막기·난사)은 맞음 자세에서 시작해 0.2초에 쉬는 자세로.
            AssertRest(S, WeaponStances.Act(S, StanceMotionCatalog.Phase(WeaponActPhase.Flinch, 0.2f, flinch: WeaponActKind.Guard)), "막기 끊김 끝");
            AssertRest(T, WeaponStances.Act(T, StanceMotionCatalog.Phase(WeaponActPhase.Flinch, 0.2f, flinch: WeaponActKind.Flurry)), "난사 끊김 끝");
        }

        [Test]
        public void ChargeStartsReleaseFromItsLevelPose()
        {
            // 놓아 베기 1~3은 그 단계 모으는 자세(0.40·0.90·1.50초)에서 시작한다.
            for (int lv = 1; lv <= 3; lv++)
            {
                var charge = GreatswordPoses.Charge(GreatswordCharge.LevelTime(lv));
                var start = GreatswordPoses.Release(lv, 0f);
                Assert.AreEqual(charge.Right.X, start.Right.X, Eps, lv + "단계");
                Assert.AreEqual(charge.Right.Y, start.Right.Y, Eps, lv + "단계");
                Assert.AreEqual(0f, Wrap(charge.Right.Angle - start.Right.Angle), 0.01f, lv + "단계");
                Assert.AreEqual(charge.Twist, start.Twist, Eps);
            }
            // 떨림: 3단계 ±1.5°, 2.2초부터 ±3°. 빛 줄은 단계마다 0.35·0.60·0.90.
            var l3 = GreatswordPoses.Charge(GreatswordCharge.L3);
            float maxSmall = 0f, maxBig = 0f;
            for (int i = 0; i < 200; i++)
            {
                float t1 = GreatswordCharge.L3 + 0.6f * i / 200f;
                float t2 = GreatswordCharge.WarnAt + 0.29f * i / 200f;
                maxSmall = Math.Max(maxSmall, Math.Abs(Wrap(GreatswordPoses.Charge(t1).Right.Angle - l3.Right.Angle)));
                maxBig = Math.Max(maxBig, Math.Abs(Wrap(GreatswordPoses.Charge(t2).Right.Angle - l3.Right.Angle)));
            }
            Assert.AreEqual(GreatswordPoses.TrembleDeg, maxSmall, 0.05f);
            Assert.AreEqual(GreatswordPoses.TrembleWarnDeg, maxBig, 0.05f);
            Assert.AreEqual(0.35f, GreatswordPoses.ChargeGlow(0.6f), 1e-5f);
            Assert.AreEqual(0.60f, GreatswordPoses.ChargeGlow(1.2f), 1e-5f);
            Assert.AreEqual(0.35f + GreatswordCharge.GlowFlash, GreatswordPoses.ChargeGlow(GreatswordCharge.L1 + 0.01f), 1e-5f);
            Assert.AreEqual(0f, GreatswordPoses.ChargeGlow(0f), 1e-5f);
        }

        // ── 출력 꼴 ──

        [Test]
        public void OutputLengthIsNeverNegative()
        {
            foreach (var set in new[] { StanceMotionCatalog.SpecMotions(), StanceMotionCatalog.ExtraMotions() })
                foreach (var m in set)
                    foreach (var p in m.Frames)
                    {
                        Assert.GreaterOrEqual(p.Right.Length, 0f, m.Name);
                        Assert.GreaterOrEqual(p.Left.Length, 0f, m.Name);
                        if (m.Weapon == S) Assert.GreaterOrEqual(p.ShieldDepth, SwordShieldPoses.MinShieldDepth - 1e-6f, m.Name);
                    }
        }

        [Test]
        public void TwinbladeAnglesStayInRange()
        {
            // 오른칼 −90° ~ +40°, 왼칼 −40° ~ +90°(회오리 밖). 콤보 ①~④(공속 0·300·400), 난사, 검풍, 처형. 몸 둘레를 돌지 않는다.
            var frames = new List<StancePose>();
            foreach (int a in Speeds)
                for (int i = 0; i < TwinbladePoses.StepCount; i++)
                {
                    var plan = StanceMotionCatalog.TwinPlan(i, a);
                    int step = i;
                    frames.AddRange(StanceMotionCatalog.Sample(plan.Duration, t => TwinbladePoses.Combo(step, t, plan.Duration, plan.FirstHit)));
                }
            frames.AddRange(StanceMotionCatalog.Sample(TwinFlurry.Duration, TwinbladePoses.Flurry));
            frames.AddRange(StanceMotionCatalog.Sample(TwinbladePoses.WaveDuration, t => TwinbladePoses.Wave(t, TwinbladePoses.WaveDuration)));
            frames.AddRange(StanceMotionCatalog.Sample(TwinbladePoses.ExecutionDuration, t => TwinbladePoses.Execution(t, 0.4f, 0.16f)));
            foreach (var p in frames)
            {
                Assert.That(p.Right.Angle, Is.InRange(TwinbladePoses.RightMinDeg - 1e-3f, TwinbladePoses.RightMaxDeg + 1e-3f));
                Assert.That(p.Left.Angle, Is.InRange(TwinbladePoses.LeftMinDeg - 1e-3f, TwinbladePoses.LeftMaxDeg + 1e-3f));
            }
        }

        [Test]
        public void WhirlAndDodgeTurnWithTheBody()
        {
            foreach (var w in new[] { S, G, T })
            {
                float open = WeaponStances.WhirlOpenTime(w);
                var a = WeaponStances.Whirl(w, 0f, open);
                var b = WeaponStances.Whirl(w, 137f, open);
                Assert.AreEqual(137f, b.Twist, 1e-4f);
                StanceMath.Rotate(a.Right.X, a.Right.Y, 137f, out float x, out float y);
                Assert.AreEqual(x, b.Right.X, 1e-4f);
                Assert.AreEqual(y, b.Right.Y, 1e-4f);
                Assert.AreEqual(0f, Wrap(b.Right.Angle - a.Right.Angle - 137f), 1e-3f);
                var d = WeaponStances.Dodge(w, 90f);
                Assert.AreEqual(90f, d.Twist, 1e-4f);
            }
            // 회오리 팔 벌림 끝(spec 2차): 한손검 칼 (0, −0.68) −90°·방패 (0, 0.56) 깊이 0.14(방패각 = 회전 + 90), 대검 (0.40, −0.56) −75°, 쌍검 (0, ∓0.62) ∓90°.
            var sw = WeaponStances.Whirl(S, 30f, SwordShieldPoses.WhirlOpenTime);
            Assert.AreEqual(0.68f, (float)Math.Sqrt(sw.Right.X * sw.Right.X + sw.Right.Y * sw.Right.Y), Eps);
            Assert.AreEqual(120f, sw.ShieldAngle, Eps);
            Assert.AreEqual(SwordShieldPoses.MinShieldDepth, sw.ShieldDepth, Eps);
            var gs = WeaponStances.Whirl(G, 0f, GreatswordPoses.WhirlOpenTime);
            Assert.AreEqual(0.40f, gs.Right.X, Eps);
            Assert.AreEqual(-0.56f, gs.Right.Y, Eps);
            Assert.AreEqual(-75f, gs.Right.Angle, Eps);
            var tw = WeaponStances.Whirl(T, 0f, TwinbladePoses.WhirlOpenTime);
            Assert.AreEqual(0.62f, -tw.Right.Y, Eps);
            Assert.AreEqual(0.62f, tw.Left.Y, Eps);
            // 벌림 시작은 쉬는 자세.
            foreach (var w in new[] { S, G, T }) AssertRest(w, WeaponStances.Whirl(w, 0f, 0f), w + " 회오리 시작");
        }

        [Test]
        public void HurtMovesHandsWithTheBody()
        {
            foreach (var w in new[] { S, G, T })
            {
                var rest = WeaponStances.Rest(w);
                var h = WeaponStances.Hurt(w, 1f, 0.06f, -0.03f);
                Assert.AreEqual(rest.Right.X + 0.06f, h.Right.X, 1e-5f);
                Assert.AreEqual(rest.Right.Y - 0.03f, h.Right.Y, 1e-5f);
                Assert.AreEqual(rest.Right.Angle - 10f, h.Right.Angle, 1e-4f);
                Assert.AreEqual(0.06f, h.LeanX, 1e-6f);
                Assert.AreEqual(-0.03f, h.LeanY, 1e-6f);
                Assert.AreEqual(0.96f, h.ScaleX, 1e-5f);
                // 몸 틀로 옮기면 쉬는 자세와 같은 여유(손이 몸에 닿지 않음).
                var r0 = WeaponKeepOut.Check(w, WeaponStances.Hurt(w, 0f, 0f, 0f), ArmorWeight.Heavy);
                var r1 = WeaponKeepOut.Check(w, WeaponStances.Hurt(w, 0f, 0.06f, 0.06f), ArmorWeight.Heavy);
                Assert.AreEqual(r0.Blade, r1.Blade, 1e-4f);
                Assert.AreEqual(r0.Hilt, r1.Hilt, 1e-4f);
            }
        }

        [Test]
        public void NoneWeaponFallsBackToOldRest()
        {
            Assert.AreEqual(StanceWeapon.None, WeaponStances.Of("wpn_maul"));
            Assert.AreEqual(StanceWeapon.None, WeaponStances.Of(null));
            Assert.AreEqual(StanceWeapon.SwordShield, WeaponStances.Of("wpn_longsword"));
            Assert.AreEqual(StanceWeapon.Greatsword, WeaponStances.Of("wpn_greatsword"));
            Assert.AreEqual(StanceWeapon.Twinblades, WeaponStances.Of("wpn_twinblades"));
            var p = WeaponStances.Combo(StanceWeapon.None, 0, 0.1f, 0.5f, 0.2f);
            Assert.AreEqual(0.18f, p.Right.X, 1e-6f);
            Assert.AreEqual(-28f, p.Right.Angle, 1e-6f);
        }

        // ── 설계 시간 = WeaponPresets(꾸러미 ① 규칙이 프리셋을 바꾼 뒤 통과) ──

        [Test]
        public void DesignTimesMatchWeaponPresets()
        {
            var sword = WeaponPresets.Longsword.combo;
            Assert.AreEqual(SwordShieldPoses.StepCount, sword.Length, "한손검과 방패 단계 수");
            for (int i = 0; i < sword.Length; i++)
            {
                SwordShieldPoses.DesignTime(i, out float d, out float h);
                Assert.AreEqual(d, sword[i].duration, 1e-5f, "한손검 길이 " + (i + 1));
                Assert.AreEqual(h, sword[i].duration * sword[i].hitMoment, 1e-4f, "한손검 판정 " + (i + 1));
                Assert.AreEqual(1, sword[i].hits);
            }
            var great = WeaponPresets.Greatsword.combo;
            Assert.AreEqual(GreatswordPoses.StepCount, great.Length, "대검 단계 수");
            for (int i = 0; i < great.Length; i++)
            {
                GreatswordPoses.ComboDesign(i, out float d, out float h);
                Assert.AreEqual(d, great[i].duration, 1e-5f, "대검 길이 " + (i + 1));
                Assert.AreEqual(h, great[i].duration * great[i].hitMoment, 1e-4f, "대검 판정 " + (i + 1));
                Assert.AreEqual(1, great[i].hits);
            }
            var twin = WeaponPresets.Twinblades.combo;
            Assert.AreEqual(TwinbladePoses.StepCount, twin.Length, "쌍검 단계 수");
            for (int i = 0; i < twin.Length; i++)
            {
                Assert.AreEqual(TwinbladePoses.StepDuration[i], twin[i].duration, 1e-5f, "쌍검 길이 " + (i + 1));
                Assert.AreEqual(TwinbladePoses.StepHitMoment[i], twin[i].hitMoment, 1e-5f, "쌍검 판정 비율 " + (i + 1));
                Assert.AreEqual(TwinbladePoses.StepHits[i], twin[i].hits, "쌍검 타수 " + (i + 1));
                Assert.AreEqual(TwinbladePoses.StepInterval[i], twin[i].hitInterval, 1e-5f, "쌍검 간격 " + (i + 1));
            }
        }

        // ── 매 호출 할당 없음 ──

        [Test]
        public void PosesDoNotAllocate()
        {
            var probe = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
            if (probe == null) Assert.Ignore("이 런타임은 스레드 할당 바이트를 재지 못한다");
            var allocated = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), probe);
            var weapons = new[] { S, G, T };
            var act = new ActStance();
            float sink = 0f;
            for (int pass = 0; pass < 2; pass++)
            {
                long before = allocated();
                for (int i = 0; i < 50; i++)
                {
                    float t = i * 0.017f;
                    for (int wi = 0; wi < weapons.Length; wi++)
                    {
                        var w = weapons[wi];
                        for (int s = 0; s < 4; s++) sink += WeaponStances.Combo(w, s, t, 0.9f, 0.35f).Right.X;
                        sink += WeaponStances.Riposte(w, t, 0.9f, 0.35f).Right.X;
                        sink += WeaponStances.Wave(w, t * 0.3f, 0.25f).Right.X;
                        sink += WeaponStances.Whirl(w, t * 600f, t * 0.2f).Right.X;
                        sink += WeaponStances.ReturnPose(w, WeaponStances.Combo(w, 1, t, 0.9f, 0.35f), WeaponStances.Rest(w), t).Right.X;
                        sink += WeaponStances.Execution(w, t * 0.5f, 0.4f, 0.16f).Right.X;
                        sink += WeaponStances.Dodge(w, t * 400f).Right.X;
                        sink += WeaponStances.Hurt(w, 1f - t, 0.02f, 0.01f).Right.X;
                        sink += WeaponStances.Down(w, t).Right.X;
                        for (int ph = 0; ph <= (int)WeaponActPhase.Flinch; ph++)
                        {
                            act.Phase = (WeaponActPhase)ph;
                            act.PhaseTime = t;
                            act.ActTime = t;
                            act.HeldTime = t * 3f;
                            act.ChargeLevel = GreatswordCharge.LevelAt(act.HeldTime);
                            act.ReleaseLevel = 1 + (i % 3);
                            act.FlinchKind = (WeaponActKind)(i % 4);
                            sink += WeaponStances.Act(w, act).Right.X;
                        }
                    }
                }
                long used = allocated() - before;
                // 첫 번은 정적 키 표 만들기(한 번)를 포함할 수 있어 둘째 번만 잰다.
                if (pass == 1) Assert.AreEqual(0L, used, "자세 계산이 매 호출 할당한다");
            }
            Assert.IsFalse(float.IsNaN(sink));
        }
    }
}
