using System;
using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Combat.Stance;
using Demo6.Core.Loot;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 세 무기 움직임 목록(spec.json '검사_요약'의 51개 + 덧붙인 확인). 시험 두 벌(StanceKeepOutTests·StanceHitAlignTests)이 같이 쓴다.
    /// 움직임마다 1/480초 간격으로 자세를 만든다(규칙 자세는 spec과 같은 몇 장). 쌍검 콤보는 공격 속도 0·300·400‰.
    /// </summary>
    public static class StanceMotionCatalog
    {
        public sealed class Motion
        {
            public string Name;
            public StanceWeapon Weapon;
            public bool Execution;
            /// <summary>spec '검사_요약' 51개 안의 움직임인가(덧붙인 확인은 false).</summary>
            public bool Spec;
            public List<StancePose> Frames;
        }

        public const float SampleStep = 1f / 480f;

        /// <summary>0 ~ d를 1/480초 이하 간격으로(양 끝 포함).</summary>
        public static List<StancePose> Sample(float d, Func<float, StancePose> pose)
        {
            int n = Math.Max(1, (int)Math.Ceiling(d / SampleStep - 1e-4));
            var list = new List<StancePose>(n + 1);
            for (int j = 0; j <= n; j++) list.Add(pose(d * j / n));
            return list;
        }

        public static ActStance Phase(WeaponActPhase phase, float t, bool boss = false, float held = 0f, int release = 0, WeaponActKind flinch = WeaponActKind.None) =>
            new ActStance { Phase = phase, ActTime = t, PhaseTime = t, BossRecoil = boss, HeldTime = held, ReleaseLevel = release, FlinchKind = flinch };

        /// <summary>쌍검 콤보 단계의 공격 속도 시간표(SwingTiming과 같은 식, 설계 값으로 만든 단계).</summary>
        public static SwingPlan TwinPlan(int step, int attackSpeedPermille) => SwingTiming.Plan(TwinStep(step), attackSpeedPermille);

        public static ComboStep TwinStep(int step) => new ComboStep
        {
            duration = TwinbladePoses.StepDuration[step],
            hitMoment = TwinbladePoses.StepHitMoment[step],
            hits = TwinbladePoses.StepHits[step],
            hitInterval = TwinbladePoses.StepInterval[step],
        };

        public static ComboStep DesignStep(float duration, float hit) => new ComboStep { duration = duration, hitMoment = hit / duration, hits = 1 };

        static readonly float[] Bobs = { -4f, 0f, 4f };
        static readonly float[] Twists = { -6f, 0f, 6f };
        static readonly float[,] Pushes = { { 0.06f, 0f }, { -0.06f, 0f }, { 0f, 0.06f }, { 0f, -0.06f } };

        /// <summary>
        /// 걷기: 쉬는 자세에서 비틀기 ±6°, 오른손 날 ±4°(방패·왼칼은 반대 부호, 같은 위상). 3차: 대검 두 손 앞뒤 ±0.02, 쌍검 좌우 반대 ±0.03
        /// (게임은 비틀기 ÷ 6 = 걸음 위상으로 넣지만 두 부호를 다 잰다, spec과 같음).
        /// </summary>
        static List<StancePose> Walk(StanceWeapon w)
        {
            var list = new List<StancePose>();
            float sway = w == StanceWeapon.Greatsword ? GreatswordPoses.WalkSway : w == StanceWeapon.Twinblades ? TwinbladePoses.WalkSway : 0f;
            for (int i = 0; i < 3; i++)
                for (int sgn = 1; sgn >= (sway > 0f ? -1 : 1); sgn -= 2)
                {
                    var p = WeaponStances.Rest(w);
                    float sx = sway * sgn * Twists[i] / 6f;
                    p.Twist = Twists[i];
                    p.Right.Angle += Bobs[i];
                    if (w == StanceWeapon.SwordShield)
                    {
                        p.Left.Angle -= Bobs[i];
                        p.ShieldAngle -= Bobs[i];
                    }
                    else if (w == StanceWeapon.Twinblades)
                    {
                        p.Left.Angle -= Bobs[i];
                        p.Right.X += sx;
                        p.Left.X -= sx;
                    }
                    else if (w == StanceWeapon.Greatsword)
                    {
                        p.Right.X += sx;
                        p.Left = GreatswordPoses.SecondHand(p.Right);
                    }
                    list.Add(p);
                }
            return list;
        }

        /// <summary>맞음(k = 1): 날 ±10°(두 부호 모두), 손·몸 0.06 밀림(앞·뒤·옆), 몸 0.96배.</summary>
        static List<StancePose> Hurt(StanceWeapon w)
        {
            var list = new List<StancePose>();
            for (int b = 0; b < 2; b++)
                for (int i = 0; i < 4; i++)
                {
                    var p = WeaponStances.Hurt(w, 1f, Pushes[i, 0], Pushes[i, 1]);
                    if (b == 1)
                    {
                        // 반대 부호(날 +10°, 방패·왼칼 −10°)도 잰다(spec과 같음).
                        p.Right.Angle += 20f;
                        if (w != StanceWeapon.Greatsword) p.Left.Angle -= 20f;
                        if (w == StanceWeapon.SwordShield) p.ShieldAngle -= 20f;
                    }
                    list.Add(p);
                }
            return list;
        }

        static StancePose Scaled(StancePose p, float sx, float sy)
        {
            p.ScaleX = sx;
            p.ScaleY = sy;
            return p;
        }

        static Motion M(string name, StanceWeapon w, List<StancePose> frames, bool spec = true, bool execution = false) =>
            new Motion { Name = name, Weapon = w, Frames = frames, Spec = spec, Execution = execution };

        /// <summary>회오리 팔 벌림(시작부터 t초의 키, 0 ~ 0.12초). 몸 틀로 잼(회전 0).</summary>
        static List<StancePose> WhirlOpen(StanceWeapon w) => Sample(0.12f, t => WeaponStances.Whirl(w, 0f, t));

        /// <summary>spec '검사_요약'의 51개 움직임(한손검 17, 대검 15, 쌍검 19).</summary>
        public static List<Motion> SpecMotions()
        {
            var all = new List<Motion>();
            const StanceWeapon S = StanceWeapon.SwordShield, G = StanceWeapon.Greatsword, T = StanceWeapon.Twinblades;

            // ── 한손검과 방패 ──
            all.Add(M("한손검 쉬는 자세(걷기)", S, Walk(S)));
            for (int i = 0; i < SwordShieldPoses.StepCount; i++)
            {
                SwordShieldPoses.DesignTime(i, out float d, out float h);
                int step = i;
                all.Add(M("한손검 콤보 " + (i + 1), S, Sample(d, t => WeaponStances.Combo(S, step, t, d, h))));
            }
            all.Add(M("한손검 들기", S, Sample(ShieldRule.RaiseTime, t => WeaponStances.Act(S, Phase(WeaponActPhase.Raise, t)))));
            all.Add(M("한손검 막기 자세", S, new List<StancePose> { WeaponStances.Act(S, Phase(WeaponActPhase.Hold, 0f)) }));
            all.Add(M("한손검 내리기", S, Sample(ShieldRule.LowerTime, t => WeaponStances.Act(S, Phase(WeaponActPhase.Lower, t)))));
            all.Add(M("한손검 막기 반동(일반)", S, Sample(ShieldRule.RecoilTime, t => WeaponStances.Act(S, Phase(WeaponActPhase.Recoil, t)))));
            all.Add(M("한손검 막기 반동(보스)", S, Sample(ShieldRule.BossRecoilTime, t => WeaponStances.Act(S, Phase(WeaponActPhase.Recoil, t, true)))));
            all.Add(M("한손검 패링 밀쳐 내기", S, Sample(ShieldRule.ParryPushTime, t => WeaponStances.Act(S, Phase(WeaponActPhase.ParryPush, t)))));
            all.Add(M("한손검 막기 깨짐", S, Sample(ShieldRule.BreakStagger, t => WeaponStances.Act(S, Phase(WeaponActPhase.Break, t)))));
            all.Add(M("한손검 반격 베기", S, Sample(SwordShieldPoses.Step3Duration,
                t => WeaponStances.Riposte(S, t, SwordShieldPoses.Step3Duration, SwordShieldPoses.Step3Hit))));
            all.Add(M("한손검 검풍(Q)", S, Sample(SwordShieldPoses.WaveDuration, t => WeaponStances.Wave(S, t, SwordShieldPoses.WaveDuration))));
            all.Add(M("한손검 회오리(E)", S, WhirlOpen(S)));
            all.Add(M("한손검 처형", S, Sample(SwordShieldPoses.ExecutionDuration,
                t => WeaponStances.Execution(S, t, SwordShieldPoses.ExecutionDuration, SwordShieldPoses.ExecutionHit)), true, true));
            all.Add(M("한손검 구르기", S, new List<StancePose> { Scaled(WeaponStances.Dodge(S, 0f), 0.8f, 0.68f) }));
            all.Add(M("한손검 맞음", S, Hurt(S)));

            // ── 대검 ──
            all.Add(M("대검 쉬는 자세(걷기)", G, Walk(G)));
            for (int i = 0; i < GreatswordPoses.StepCount; i++)
            {
                GreatswordPoses.ComboDesign(i, out float d, out float h);
                int step = i;
                all.Add(M("대검 콤보 " + (i + 1), G, Sample(d, t => WeaponStances.Combo(G, step, t, d, h))));
            }
            all.Add(M("대검 기 모으기", G, Sample(GreatswordCharge.L3, t => WeaponStances.Act(G, Phase(WeaponActPhase.Charging, t, held: t)))));
            all.Add(M("대검 기 모으기 큰 떨림", G, Sample(GreatswordCharge.MaxHold, t => WeaponStances.Act(G, Phase(WeaponActPhase.Charging, t, held: t)))));
            for (int lv = 1; lv <= 3; lv++)
            {
                GreatswordPoses.ReleaseDesign(lv, out float d, out _);
                int level = lv;
                all.Add(M("대검 놓기 " + lv + "단계", G, Sample(d, t => WeaponStances.Act(G, Phase(WeaponActPhase.Release, t, release: level)))));
            }
            all.Add(M("대검 끊김", G, Sample(GreatswordPoses.InterruptDuration,
                t => WeaponStances.Act(G, Phase(WeaponActPhase.Flinch, t, held: GreatswordCharge.L3, flinch: WeaponActKind.Charge)))));
            all.Add(M("대검 검풍(Q)", G, Sample(GreatswordPoses.WaveDuration, t => WeaponStances.Wave(G, t, GreatswordPoses.WaveDuration))));
            all.Add(M("대검 처형", G, Sample(GreatswordPoses.ExecutionDuration,
                t => WeaponStances.Execution(G, t, GreatswordPoses.ExecutionDuration, GreatswordPoses.ExecutionHit)), true, true));
            all.Add(M("대검 회오리(E)", G, new List<StancePose> { WeaponStances.Whirl(G, 0f, 1f) }));
            all.Add(M("대검 구르기", G, new List<StancePose> { Scaled(WeaponStances.Dodge(G, 0f), 0.8f, 0.68f) }));
            all.Add(M("대검 맞음", G, Hurt(G)));

            // ── 쌍검 ──
            all.Add(M("쌍검 쉬는 자세(걷기)", T, Walk(T)));
            int[] speeds = { 0, 300, 400 };
            for (int i = 0; i < TwinbladePoses.StepCount; i++)
                foreach (int a in speeds)
                {
                    var plan = TwinPlan(i, a);
                    int step = i;
                    float d = plan.Duration, h = plan.FirstHit;
                    all.Add(M("쌍검 콤보 " + (i + 1) + " 공속" + a, T, Sample(d, t => WeaponStances.Combo(T, step, t, d, h))));
                }
            all.Add(M("쌍검 난사", T, Sample(TwinFlurry.Duration, t => WeaponStances.Act(T, Phase(WeaponActPhase.Flurry, t)))));
            all.Add(M("쌍검 검풍(Q)", T, Sample(TwinbladePoses.WaveDuration, t => WeaponStances.Wave(T, t, TwinbladePoses.WaveDuration))));
            all.Add(M("쌍검 처형", T, Sample(TwinbladePoses.ExecutionDuration,
                t => WeaponStances.Execution(T, t, TwinbladePoses.ExecutionDuration, TwinbladePoses.ExecutionHit)), true, true));
            all.Add(M("쌍검 회오리(E)", T, WhirlOpen(T)));
            var roll = new List<StancePose>();
            for (int i = 0; i < 3; i++)
            {
                float s = i * 0.5f;
                roll.Add(Scaled(WeaponStances.Dodge(T, 0f), 1f - 0.2f * s, 1f - 0.32f * s));
            }
            all.Add(M("쌍검 구르기", T, roll));
            all.Add(M("쌍검 맞음", T, Hurt(T)));
            return all;
        }

        /// <summary>
        /// spec 51개 밖에서 게임이 실제로 지나는 자세: 쌍검 끊김 따라가기(난사·콤보 어느 순간에서 맞음 자세로 가까운 각 따라가기 0.4초, 리그 FollowRate 16),
        /// 대검 회오리 팔 벌림, 대검 끊김(여러 모은 시간·놓아 베기 단계에서), 맞음 세기 k 사이 값, 구르기 도중.
        /// </summary>
        public static List<Motion> ExtraMotions()
        {
            var all = new List<Motion>();
            const StanceWeapon S = StanceWeapon.SwordShield, G = StanceWeapon.Greatsword, T = StanceWeapon.Twinblades;
            all.Add(M("쌍검 끊김 따라가기", T, TwinFollow(), false));
            all.Add(M("대검 회오리 팔 벌림", G, WhirlOpen(G), false));
            foreach (float held in new[] { 0f, 0.2f, 0.4f, 0.9f, 2.2f, 2.5f })
            {
                float h0 = held;
                all.Add(M("대검 끊김(모은 " + held + "초)", G, Sample(GreatswordPoses.InterruptDuration,
                    t => WeaponStances.Act(G, Phase(WeaponActPhase.Flinch, t, held: h0, flinch: WeaponActKind.Charge))), false));
            }
            for (int lv = 1; lv <= 3; lv++)
            {
                int level = lv;
                all.Add(M("대검 끊김(놓기 " + lv + "단계)", G, Sample(GreatswordPoses.InterruptDuration,
                    t => WeaponStances.Act(G, Phase(WeaponActPhase.Flinch, t, held: GreatswordCharge.LevelTime(level), release: level, flinch: WeaponActKind.Charge))), false));
            }
            foreach (var w in new[] { S, G, T })
            {
                var hurt = new List<StancePose>();
                for (int i = 0; i <= 10; i++) hurt.Add(WeaponStances.Hurt(w, i / 10f, 0f, 0f));
                all.Add(M(w + " 맞음 세기", w, hurt, false));
                var dodge = Sample(0.22f, t =>
                {
                    float u = t / 0.22f;
                    float bump = (float)Math.Sin(Math.PI * u);
                    return Scaled(WeaponStances.Dodge(w, 360f * StanceMath.Smooth(u)), 1f - 0.2f * bump, 1f - 0.32f * bump);
                });
                all.Add(M(w + " 구르기 도중", w, dodge, false));
                var flinch = Sample(0.35f, t => WeaponStances.Act(w, Phase(WeaponActPhase.Flinch, t,
                    flinch: w == G ? WeaponActKind.Charge : w == S ? WeaponActKind.Guard : WeaponActKind.Flurry, held: 0.6f)));
                all.Add(M(w + " 끊김", w, flinch, false));
            }
            return all;
        }

        /// <summary>쌍검 끊김 따라가기: 난사·콤보 ①~③을 0.01초마다 끊고 맞음 자세(쉬는 자세 ∓10°)로 가까운 각을 따라 0.4초(60프레임 k = 1 − e^(−16/60)).</summary>
        public static List<StancePose> TwinFollow()
        {
            var sources = new List<StancePose>();
            for (int j = 0; j <= 110; j++) sources.Add(TwinbladePoses.Flurry(j / 100f));
            for (int i = 0; i < 3; i++)
            {
                var plan = TwinPlan(i, 0);
                for (int j = 0; j <= (int)(plan.Duration * 100f); j++)
                    sources.Add(TwinbladePoses.Combo(i, j / 100f, plan.Duration, plan.FirstHit));
            }
            var target = TwinbladePoses.Hurt(1f, 0f, 0f);
            float k = 1f - (float)Math.Exp(-16.0 / 60.0);
            var frames = new List<StancePose>();
            foreach (var src in sources)
            {
                var p = src;
                p.ScaleX = p.ScaleY = 0.96f;
                p.LeanX = 0f;
                for (int f = 0; f < 24; f++)
                {
                    p.Right = Follow(p.Right, target.Right, k);
                    p.Left = Follow(p.Left, target.Left, k);
                    p.Twist += StanceMath.WrapDeg(0f - p.Twist) * k;
                    frames.Add(p);
                }
            }
            return frames;
        }

        /// <summary>리그 TopDownHand.Follow와 같은 식(각은 가까운 쪽으로).</summary>
        public static HandPose Follow(HandPose a, HandPose b, float k) => new HandPose
        {
            X = a.X + (b.X - a.X) * k,
            Y = a.Y + (b.Y - a.Y) * k,
            Angle = a.Angle + StanceMath.WrapDeg(b.Angle - a.Angle) * k,
            Length = a.Length + (b.Length - a.Length) * k,
            Size = a.Size + (b.Size - a.Size) * k,
        };
    }

    /// <summary>
    /// 규칙 ② 검사(기획/세-무기-우클릭-소켓-1차.md 0-2, 7-4): 세 무기 모든 움직임을 가죽·사슬·판금에서 1/480초로 잰다(spec.json 2차 게임 몸 모형).
    /// spec 3차 '검사_요약'의 51개가 모두 통과하고 가장 작은 여유가 날 0.019·자루 0.002·머리 0.030 근처(설계는 설계 곡선과 spec 키 보간 둘 중 작은 값이라 ±0.003)여야 한다.
    /// 덧붙인 확인과 쉬는 자세로 되돌아가기(한손검·쌍검 0.2초 곧게, 대검 칼끝 땅 경유 0.25초)도 모두 통과해야 한다.
    /// </summary>
    public sealed class StanceKeepOutTests
    {
        static readonly ArmorWeight[] Weights = { ArmorWeight.Light, ArmorWeight.Medium, ArmorWeight.Heavy };

        /// <summary>움직임 하나의 가장 나쁜 값(세 무게 모두).</summary>
        public struct Worst
        {
            public float Head, Blade, Hilt, Arm, SecondArm, Size;
            public bool Pass;
            public string Fail;
        }

        public static Worst Measure(StanceMotionCatalog.Motion m)
        {
            var w = new Worst { Head = 9f, Blade = 9f, Hilt = 9f, Pass = true };
            for (int i = 0; i < m.Frames.Count; i++)
            {
                var pose = m.Frames[i];
                foreach (var weight in Weights)
                {
                    var r = WeaponKeepOut.Check(m.Weapon, pose, weight, m.Execution);
                    w.Head = Math.Min(w.Head, r.Head);
                    w.Blade = Math.Min(w.Blade, r.Blade);
                    w.Hilt = Math.Min(w.Hilt, r.Hilt);
                    w.Arm = Math.Max(w.Arm, r.Arm);
                    w.SecondArm = Math.Max(w.SecondArm, r.SecondArm);
                    w.Size = Math.Max(w.Size, r.WeaponSize);
                    if (w.Pass && !r.Pass(m.Execution))
                    {
                        w.Pass = false;
                        w.Fail = $"{weight} 프레임 {i}: 머리 {r.Head:0.000} 날 {r.Blade:0.000} 자루 {r.Hilt:0.000} 팔 {r.Arm:0.000} 둘째 팔 {r.SecondArm:0.000} 크기 {r.WeaponSize:0.000}";
                    }
                }
            }
            return w;
        }

        [Test]
        public void SpecHasFiftyOneMotions()
        {
            var list = StanceMotionCatalog.SpecMotions();
            Assert.AreEqual(51, list.Count);
            int sword = 0, great = 0, twin = 0;
            foreach (var m in list)
            {
                if (m.Weapon == StanceWeapon.SwordShield) sword++;
                else if (m.Weapon == StanceWeapon.Greatsword) great++;
                else twin++;
            }
            Assert.AreEqual(17, sword);
            Assert.AreEqual(15, great);
            Assert.AreEqual(19, twin);
        }

        [Test]
        public void AllSpecMotionsPassInThreeArmorWeights()
        {
            var fails = new List<string>();
            foreach (var m in StanceMotionCatalog.SpecMotions())
            {
                var w = Measure(m);
                if (!w.Pass) fails.Add(m.Name + " — " + w.Fail);
            }
            Assert.IsEmpty(fails, string.Join("\n", fails));
        }

        [Test]
        public void SmallestMarginsMatchSpecSummary()
        {
            float head = 9f, blade = 9f, hilt = 9f;
            string wh = "", wb = "", wt = "";
            foreach (var m in StanceMotionCatalog.SpecMotions())
            {
                var w = Measure(m);
                if (w.Head < head) { head = w.Head; wh = m.Name; }
                if (w.Blade < blade) { blade = w.Blade; wb = m.Name; }
                if (w.Hilt < hilt) { hilt = w.Hilt; wt = m.Name; }
            }
            // spec 3차 검사_요약: 날 0.019, 자루 0.002, 머리 0.030(설계 곡선·spec 키 보간 둘 다 잰 가장 작은 값). 게임은 설계 곡선만 읽으므로 ±0.003 안이고 기준 이상.
            UnityEngine.Debug.Log($"규칙 ② 가장 작은 여유: 날 {blade:0.0000}({wb}) 자루 {hilt:0.0000}({wt}) 머리 {head:0.0000}({wh})");
            Assert.AreEqual(0.019, blade, 0.003, $"날 {blade:0.0000} ({wb})");
            Assert.AreEqual(0.002, hilt, 0.003, $"자루 {hilt:0.0000} ({wt})");
            Assert.AreEqual(0.030, head, 0.003, $"머리 {head:0.0000} ({wh})");
            Assert.GreaterOrEqual(blade, WeaponKeepOut.BladeMarginMin);
            Assert.GreaterOrEqual(hilt, WeaponKeepOut.HiltMarginMin);
            Assert.GreaterOrEqual(head, WeaponKeepOut.HeadMarginMin);
        }

        [Test]
        public void ExtraMotionsAlsoPass()
        {
            var fails = new List<string>();
            foreach (var m in StanceMotionCatalog.ExtraMotions())
            {
                var w = Measure(m);
                if (!w.Pass) fails.Add(m.Name + " — " + w.Fail);
            }
            Assert.IsEmpty(fails, string.Join("\n", fails));
        }

        /// <summary>
        /// 쉬는 자세로 되돌아가기(spec 검사_요약 '되돌아가기'): 세 무기 모든 콤보·행동·검풍·처형·회오리 벌림을 1/60초마다 끊고 그 자세에서 쉬는 자세로 가는 길
        /// (WeaponStances.ReturnPose, 25점)을 잰다. 한손검·쌍검은 몸 틀 Smooth 곧게 0.2초, 대검은 경유 길 0.25초(쉬는 자세 가까이면 곧게),
        /// 회오리 뒤는 팔 벌림 키를 거꾸로(ReturnFromWhirl). spec 설계 검사는 움직임 앞부분(t = d × j ÷ 60)만 표집해 회오리 뒤 곧게 섞기 미달을 놓쳤다.
        /// </summary>
        [Test]
        public void ReturnToRestPassesFromEveryMotionSample()
        {
            var fails = new List<string>();
            int checkedCount = 0;
            foreach (var m in StanceMotionCatalog.SpecMotions())
            {
                if (m.Frames.Count < 3) continue;
                var rest = WeaponStances.Rest(m.Weapon);
                bool whirl = m.Name.Contains("회오리");
                int stride = Math.Max(1, (int)Math.Round(480.0 / 60.0));
                for (int i = 0; i < m.Frames.Count; i += stride)
                {
                    var from = m.Frames[i];
                    // 회오리 벌림 표집은 0 ~ 0.12초 고른 간격(WhirlOpen): 그 시각에서 벌림 키를 거꾸로 거둔다.
                    float whirlT = 0.12f * i / Math.Max(1, m.Frames.Count - 1);
                    for (int j = 0; j <= 24; j++)
                    {
                        var p = whirl
                            ? WeaponStances.ReturnFromWhirl(m.Weapon, whirlT, from.Twist, rest, j / 24f)
                            : WeaponStances.ReturnPose(m.Weapon, from, rest, j / 24f);
                        p.BladeScale = 1f;
                        foreach (var weight in new[] { ArmorWeight.Light, ArmorWeight.Medium, ArmorWeight.Heavy })
                        {
                            var r = WeaponKeepOut.Check(m.Weapon, p, weight);
                            checkedCount++;
                            if (!r.Pass(false) && fails.Count < 12)
                                fails.Add($"{m.Name} 끊은 프레임 {i} 되돌아가기 {j}/24 {weight}: 머리 {r.Head:0.000} 날 {r.Blade:0.000} 자루 {r.Hilt:0.000} 팔 {r.Arm:0.000}/{r.SecondArm:0.000}");
                        }
                    }
                }
            }
            Assert.Greater(checkedCount, 10000);
            Assert.IsEmpty(fails, string.Join("\n", fails));
        }

        [Test]
        public void GreatswordBladeIsTwoThirdsOfBodyWidth()
        {
            // 사용자 원문(2026-10-05) "대검은 두께이야기한건데 길이말고": 날 폭 = 몸 폭(어깨받이 바깥 ~ 바깥 0.99)의 2/3 ≈ 0.66, 길이는 원화 1.63으로 되돌림.
            Assert.AreEqual(GreatswordPoses.BodyWidth * 2f / 3f, 2f * GreatswordPoses.BladeHalfWidth, 0.005f);
            Assert.AreEqual(1.635f, WeaponKeepOut.GreatswordTotalLength, 0.002f);
            // 둘째 손은 날 방향 뒤로 0.18 × max(cos 기울기, 0.65): 칼이 서도 두 주먹(지름 0.12)이 붙지 않는다.
            var up = HandPose.Tilted(0.2f, -0.5f, 0f, 80f);
            var g2 = GreatswordPoses.SecondHand(up);
            Assert.AreEqual(0.2f - 0.18f * 0.65f, g2.X, 1e-5f);
            float gap = (float)Math.Sqrt(Math.Pow(up.X - g2.X, 2) + Math.Pow(up.Y - g2.Y, 2));
            Assert.Greater(gap, 2f * WeaponKeepOut.FistRadius - 0.01f);
            var flat = GreatswordPoses.SecondHand(HandPose.Tilted(0.2f, -0.5f, 0f, 0f));
            Assert.AreEqual(0.2f - 0.18f, flat.X, 1e-5f);
            // 쉬는 자세: 칼끝을 땅 쪽으로(기울기 −56), 칼 제 크기.
            var rest = GreatswordPoses.Rest;
            Assert.AreEqual(-56f, rest.Right.Tilt, 1e-4f);
            Assert.AreEqual(1f, rest.Right.Length, 1e-6f);
            Assert.AreEqual(1f, rest.Right.Size, 1e-6f);
            Assert.AreEqual(0f, rest.Flip, 1e-6f);
        }

        /// <summary>
        /// 칼 그림 늘이기·줄이기 없음(3차, 사용자 원문 2026-10-05 "지금보니 검들이 다늘어나고 압축되는데 이부분이 제일어색한것같다"):
        /// 세 무기 모든 움직임(spec 51개 + 덧붙인 확인)에서 칼 길이·크기 배율 1, 칼 배율 1(처형 포함), 세움 0. 기울기는 −90 ~ 90°.
        /// </summary>
        [Test]
        public void BladesAreNeverStretchedOrSquashed()
        {
            var fails = new List<string>();
            foreach (var set in new[] { StanceMotionCatalog.SpecMotions(), StanceMotionCatalog.ExtraMotions() })
                foreach (var m in set)
                    for (int i = 0; i < m.Frames.Count; i++)
                    {
                        var p = m.Frames[i];
                        bool bad = Math.Abs(p.Right.Length - 1f) > 1e-4f || Math.Abs(p.Right.Size - 1f) > 1e-4f || Math.Abs(p.BladeScale - 1f) > 1e-4f
                                   || Math.Abs(p.Flip) > 1e-4f || Math.Abs(p.Right.Tilt) > 90f + 1e-3f;
                        if (m.Weapon == StanceWeapon.Twinblades)
                            bad |= Math.Abs(p.Left.Length - 1f) > 1e-4f || Math.Abs(p.Left.Size - 1f) > 1e-4f || Math.Abs(p.Left.Tilt) > 1e-4f;
                        if (m.Weapon != StanceWeapon.Greatsword) bad |= Math.Abs(p.Right.Tilt) > 1e-4f;
                        if (bad && fails.Count < 10)
                            fails.Add($"{m.Name} 프레임 {i}: 길이 {p.Right.Length:0.000} 크기 {p.Right.Size:0.000} 칼 배율 {p.BladeScale:0.000} 세움 {p.Flip:0.000} 기울기 {p.Right.Tilt:0.0}");
                    }
            Assert.IsEmpty(fails, string.Join("\n", fails));
        }

        /// <summary>
        /// 대검 ③ 내려 쪼개기(사용자 원문 2026-10-05 "대검을 세로로내려찍을때표현이약간 부족한것같긴하네"): 준비 0.45초 동안 칼을 카메라 쪽으로 곧게 세우고(기울기 90),
        /// 0.53 ~ 0.65초(0.12초, 준비의 1/3 미만)에 보이는 날 각을 앞에 둔 채 눕혀 판정 순간에 바닥(기울기 0). 손 높이(그림자)는 정점 0.9 → 닿을 때 0.15.
        /// 정점은 머리 위가 아니라 몸 앞·오른쪽(몸 틀 x &gt; 0.4, y &lt; −0.3). 처형도 판정 순간 기울기 0.
        /// </summary>
        [Test]
        public void GreatswordSlamStandsUpThenLaysDownFast()
        {
            GreatswordPoses.ComboDesign(2, out float d, out float h);
            var top = WeaponStances.Combo(StanceWeapon.Greatsword, 2, 0.49f, d, h);
            Assert.GreaterOrEqual(top.Right.Tilt, 88f, "정점에서 칼이 섬");
            Assert.AreEqual(0.9f, top.Height, 0.02f, "정점 손 높이");
            var body = StanceKeys.Rel(top.Right, top.Twist, top.LeanX);
            Assert.Greater(body.X, 0.4f, "몸 앞");
            Assert.Less(body.Y, -0.3f, "몸 오른쪽");
            var hit = WeaponStances.Combo(StanceWeapon.Greatsword, 2, h, d, h);
            Assert.AreEqual(0f, hit.Right.Tilt, 0.01f, "닿는 순간 바닥에 누움");
            Assert.AreEqual(0.15f, hit.Height, 0.01f);
            // 내려치기 동안 보이는 날 각(몸 틀)은 앞(−6°)에 고정: 오른쪽 옆을 휘돌지 않는다.
            for (int i = 0; i <= 12; i++)
            {
                float t = 0.53f + 0.12f * i / 12f;
                var p = WeaponStances.Combo(StanceWeapon.Greatsword, 2, t, d, h);
                var b = StanceKeys.Rel(p.Right, p.Twist, p.LeanX);
                Assert.AreEqual(-6f, StanceMath.WrapDeg(b.Angle), 0.5f, "내려치기 날 각 " + t);
                if (i > 0)
                {
                    var q = WeaponStances.Combo(StanceWeapon.Greatsword, 2, 0.53f + 0.12f * (i - 1) / 12f, d, h);
                    Assert.LessOrEqual(p.Right.Tilt, q.Right.Tilt + 1e-3f, "내려치기는 기울기가 줄기만 함");
                }
            }
            // 몸이 앞으로 숙음(앞뒤 −0.065 → +0.10).
            Assert.Greater(hit.LeanX, 0.09f);
            Assert.Less(top.LeanX, -0.05f);
            var ex = WeaponStances.Execution(StanceWeapon.Greatsword, 0.16f, 0.4f, 0.16f);
            Assert.AreEqual(0f, ex.Right.Tilt, 0.01f, "처형 판정 순간 바닥");
            Assert.GreaterOrEqual(WeaponStances.Execution(StanceWeapon.Greatsword, 0.07f, 0.4f, 0.16f).Right.Tilt, 80f, "처형 정점");
        }

        [Test]
        public void TiltPictureBinsFollowTheRule()
        {
            // 기울기별 그림 칸(원화·임시 같은 차례): 15° 밑 0°, 15 ~ 37.5° 30°, ~ 52.5° 45°, ~ 70° 60°, 70° 위 80°(내릴 때도 같은 간격).
            float[] want = { 0f, 0f, 30f, 30f, 45f, 60f, 60f, 80f, 80f, -30f, -60f, -80f };
            float[] tilt = { 0f, 14f, 16f, 37f, 45f, 53f, 69f, 71f, 90f, -20f, -58f, -75f };
            for (int i = 0; i < tilt.Length; i++)
                Assert.AreEqual(want[i], GreatswordPoses.TiltBins[GreatswordPoses.TiltBinOf(tilt[i])], 1e-6f, "기울기 " + tilt[i]);
            Assert.GreaterOrEqual(GreatswordPoses.TiltBins.Length, 4, "원화 칸은 4장 이상");
        }

        [Test]
        public void HeadHasNoPointAboveItInAnyMotion()
        {
            // 머리 위(머리 원 안)로 지나가는 점이 없다: 가장 작은 머리 여유가 0.02 이상(0-2).
            foreach (var m in StanceMotionCatalog.SpecMotions())
                Assert.GreaterOrEqual(Measure(m).Head, WeaponKeepOut.HeadMarginMin - 1e-6f, m.Name);
        }

        [Test]
        public void CheckSeesAPommelInsideThePauldron()
        {
            // 검사 자체 확인: 옛 장검 쉬는 자세 (0.18, −0.34) −28°는 손잡이 끝이 판금 어깨받이를 뚫는다(0-2 '이 기준으로 걸린 것').
            var old = StancePose.Of(HandPose.At(0.18f, -0.34f, -28f), HandPose.At(0.23f, 0.38f, 32f, 0.34f));
            old.ShieldAngle = 32f;
            old.ShieldDepth = 0.34f;
            var r = WeaponKeepOut.Check(StanceWeapon.SwordShield, old, ArmorWeight.Heavy);
            Assert.Less(r.Hilt, 0f);
            Assert.IsFalse(r.Pass(false));
            // 머리 원 한가운데를 지나는 칼은 머리 여유가 음수.
            var over = StancePose.Of(HandPose.At(0.2f, -0.3f, 120f), HandPose.At(0.23f, 0.38f, 32f, 0.34f));
            over.ShieldAngle = 32f;
            over.ShieldDepth = 0.34f;
            Assert.Less(WeaponKeepOut.Check(StanceWeapon.SwordShield, over, ArmorWeight.Light).Head, 0f);
            // None 무게는 가죽과 같다.
            var rest = SwordShieldPoses.Rest;
            Assert.AreEqual(WeaponKeepOut.Check(StanceWeapon.SwordShield, rest, ArmorWeight.Light).Hilt,
                WeaponKeepOut.Check(StanceWeapon.SwordShield, rest, ArmorWeight.None).Hilt);
        }
    }
}
