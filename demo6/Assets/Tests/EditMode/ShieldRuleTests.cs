using System;
using Demo6.Core.Combat;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 한손검과 방패 막기·패링(기획/세-무기-우클릭-소켓-1차.md 2-5~2-7, 7-4): 앞 반원 90° 경계, 몸 안에서 온 타, 종류별 감소·밀림·방패 버팀 표,
    /// 패링 창(0.179 튕김 / 0.181 막기), 다시 누르기 잠금 0.6초, 최소 0.25초, 버팀 0이면 깨짐, 보스 내려찍기 늘 깨짐, 낙석·덫, 패링 안 되는 종류, 회복 식.
    /// </summary>
    public sealed class ShieldRuleTests
    {
        const float Eps = 1e-6f;

        /// <summary>몸이 +x를 보고, 공격이 angleDeg 쪽(0 = 바로 앞)에서 거리 distance로 온 질의. 기본은 막는 중·창 닫힘·버팀 가득·일반 감소 0.25.</summary>
        static GuardQuery Query(HitKind kind, float angleDeg, float distance = 1.2f, bool hasTravel = false, bool parryOpen = false,
            float meter = ShieldRule.GuardMax, float percent = 100f, bool guarding = true, float general = 0.25f)
        {
            double r = angleDeg * Math.PI / 180.0;
            return new GuardQuery
            {
                Kind = kind,
                Guarding = guarding,
                ParryOpen = parryOpen,
                FacingX = 1f,
                FacingY = 0f,
                ToSourceX = (float)Math.Cos(r),
                ToSourceY = (float)Math.Sin(r),
                HasTravel = hasTravel,
                SourceDistance = distance,
                PatternPercent = percent,
                Meter = meter,
                GeneralDamageScale = general,
            };
        }

        static GuardOutcome Resolve(GuardQuery q) => ShieldRule.Resolve(q);

        [Test]
        public void ConstantsMatchDoc()
        {
            Assert.AreEqual(0.12f, ShieldRule.RaiseTime, Eps);
            Assert.AreEqual(0.12f, ShieldRule.LowerTime, Eps);
            Assert.AreEqual(0.06f, ShieldRule.LowerBlockTime, Eps);
            Assert.AreEqual(0.25f, ShieldRule.MinHold, Eps);
            Assert.AreEqual(0.18f, ShieldRule.ParryWindow, Eps);
            Assert.AreEqual(0.6f, ShieldRule.ParryRearm, Eps);
            Assert.AreEqual(0.15f, ShieldRule.ParryPushTime, Eps);
            Assert.AreEqual(0.12f, ShieldRule.RecoilTime, Eps);
            Assert.AreEqual(0.25f, ShieldRule.BossRecoilTime, Eps);
            Assert.AreEqual(0.15f, ShieldRule.BlockStagger, Eps);
            Assert.AreEqual(0.12f, ShieldRule.BlockInvulnerable, Eps);
            Assert.AreEqual(0.8f, ShieldRule.BreakStagger, Eps);
            Assert.AreEqual(1.5f, ShieldRule.BreakLock, Eps);
            Assert.AreEqual(0.4f, ShieldRule.HurtLock, Eps);
            Assert.AreEqual(90f, ShieldRule.FrontHalfAngle, Eps);
            Assert.AreEqual(0.4f, ShieldRule.BodyRadius, Eps);
            Assert.AreEqual(100f, ShieldRule.GuardMax, Eps);
            Assert.AreEqual(20f, ShieldRule.ParryRefund, Eps);
            Assert.AreEqual(0.35f, ShieldRule.LowMeterFraction, Eps);
            Assert.AreEqual(1.0f, ShieldRule.RiposteWindow, Eps);
            Assert.AreEqual(1.5f, ShieldRule.RiposteDamageScale, Eps);
            Assert.AreEqual(2f, ShieldRule.RipostePoiseScale, Eps);
            Assert.AreEqual(3, ShieldRule.RiposteStepNumber);
            Assert.AreEqual("마무리 베기", WeaponPresets.Longsword.combo[ShieldRule.RiposteStepNumber - 1].name, "반격 베기는 ③ 마무리 베기(찌르기 없음)");
            // 패링 창 0.18초가 들기 0.12초를 덮고 0.06초 남는다.
            Assert.AreEqual(0.06f, ShieldRule.ParryWindow - ShieldRule.RaiseTime, Eps);
            // 반격 베기 195%(③ 130% × 1.5, 0-3의 24).
            Assert.AreEqual(195f, WeaponPresets.Longsword.combo[2].hitPercent * ShieldRule.RiposteDamageScale, 1e-3f);
            // 패링 공통 효과.
            Assert.AreEqual(0.06f, ShieldRule.ParryHitStop, Eps);
            Assert.AreEqual(0.04f, ShieldRule.ParryShakeAmount, Eps);
            Assert.AreEqual(0.06f, ShieldRule.ParryShakeTime, Eps);
            Assert.AreEqual(8, ShieldRule.ParrySparks);
            // 적 쪽 패링 효과.
            Assert.AreEqual(1.2f, ShieldRule.LightStaggerSeconds, Eps);
            Assert.AreEqual(0.8f, ShieldRule.LightParryPush, Eps);
            Assert.AreEqual(0.6f, ShieldRule.MediumStaggerSeconds, Eps);
            // 패링 보상(2026-10-05): 그로기(버팀) 일반 50%·정예 35%·보스 휩쓸기 6%, 피해 공격력 30%.
            Assert.AreEqual(0.5f, ShieldRule.ParryPoiseFraction, Eps);
            Assert.AreEqual(0.35f, ShieldRule.ParryElitePoiseFraction, Eps);
            Assert.AreEqual(30f, ShieldRule.ParryDamagePercent, Eps);
            Assert.AreEqual(0.5f, ShieldRule.HeavyShakeSeconds, Eps);
            Assert.AreEqual(1.5f, ShieldRule.BoarRushStunSeconds, Eps);
            Assert.AreEqual(0.06f, ShieldRule.BossSweepPoiseFraction, Eps);
        }

        /// <summary>앞 반원 경계: 89.9° 막음, 90.1° 맞음(좌우 모두). 바로 뒤는 맞음.</summary>
        [TestCase(0f, true)]
        [TestCase(45f, true)]
        [TestCase(89.9f, true)]
        [TestCase(-89.9f, true)]
        [TestCase(90.1f, false)]
        [TestCase(-90.1f, false)]
        [TestCase(135f, false)]
        [TestCase(180f, false)]
        public void FrontHalfBoundary(float angleDeg, bool blocked)
        {
            var o = Resolve(Query(HitKind.Melee, angleDeg));
            Assert.AreEqual(blocked ? HitResult.Blocked : HitResult.Hit, o.Result);
            Assert.AreEqual(blocked, o.Stopped);
            double r = angleDeg * Math.PI / 180.0;
            Assert.AreEqual(blocked, ShieldRule.IsFront(1f, 0f, (float)Math.Cos(r), (float)Math.Sin(r)));
        }

        [Test]
        public void IsFrontUsesAnyFacingAndRejectsZeroVectors()
        {
            // 몸이 −y를 보고 있으면 −y 쪽 공격만 앞.
            Assert.IsTrue(ShieldRule.IsFront(0f, -1f, 0.2f, -1f));
            Assert.IsFalse(ShieldRule.IsFront(0f, -1f, 0f, 1f));
            // 길이는 상관없다.
            Assert.IsTrue(ShieldRule.IsFront(3f, 0f, 10f, 9.9f));
            Assert.IsFalse(ShieldRule.IsFront(0f, 0f, 1f, 0f), "보는 쪽 없음");
            Assert.IsFalse(ShieldRule.IsFront(1f, 0f, 0f, 0f), "온 쪽 없음");
        }

        /// <summary>때린 자리가 몸 반지름 0.4 안이면 방향을 알 수 없어 못 막는다. 진행 방향(돌진·화살·휩쓸기)이 있으면 거리와 상관없이 막는다.</summary>
        [Test]
        public void HitFromInsideBodyCannotBeBlocked()
        {
            Assert.AreEqual(HitResult.Hit, Resolve(Query(HitKind.Melee, 0f, distance: 0.3f)).Result);
            Assert.AreEqual(HitResult.Hit, Resolve(Query(HitKind.Melee, 0f, distance: 0.4f)).Result, "0.4는 안");
            Assert.AreEqual(HitResult.Blocked, Resolve(Query(HitKind.Melee, 0f, distance: 0.41f)).Result);
            Assert.AreEqual(HitResult.Hit, Resolve(Query(HitKind.Melee, 0f, distance: 0.3f, parryOpen: true)).Result, "창이 열려 있어도 못 막음");
            Assert.AreEqual(HitResult.Blocked, Resolve(Query(HitKind.Rush, 0f, distance: 0.1f, hasTravel: true)).Result, "진행 방향이 있으면 막음");
            Assert.AreEqual(HitResult.Hit, Resolve(Query(HitKind.Rush, 180f, distance: 0.1f, hasTravel: true)).Result, "진행 방향이 뒤면 못 막음");
        }

        [Test]
        public void NotGuardingIsPlainHit()
        {
            var o = Resolve(Query(HitKind.Melee, 0f, guarding: false, parryOpen: true, meter: 40f));
            Assert.AreEqual(HitResult.Hit, o.Result);
            Assert.AreEqual(1f, o.DamageScale);
            Assert.AreEqual(1f, o.KnockScale);
            Assert.AreEqual(40f, o.MeterAfter);
            Assert.IsFalse(o.Broke);
            Assert.IsFalse(o.Stopped);
        }

        /// <summary>2-7 표: 종류별 피해·밀림 배율과 방패 버팀 깎기(근접은 배율 ÷ 4).</summary>
        [TestCase(HitKind.Melee, 100f, 0.25f, 0.3f, 25f)]
        [TestCase(HitKind.Melee, 60f, 0.25f, 0.3f, 15f)]
        [TestCase(HitKind.Rush, 100f, 0.25f, 0.3f, 30f)]
        [TestCase(HitKind.Arrow, 100f, 0f, 0f, 10f)]
        [TestCase(HitKind.PierceArrow, 200f, 0.25f, 0.3f, 50f)]
        [TestCase(HitKind.BossSweep, 110f, 0.5f, 0.5f, 40f)]
        [TestCase(HitKind.BossSlam, 250f, 0.6f, 1.0f, 100f)]
        [TestCase(HitKind.BossRush, 150f, 0.5f, 1.0f, 50f)]
        public void KindTable(HitKind kind, float percent, float damage, float knock, float cost)
        {
            Assert.AreEqual(damage, ShieldRule.DamageScale(kind), Eps, "피해");
            Assert.AreEqual(knock, ShieldRule.KnockScale(kind), Eps, "밀림");
            Assert.AreEqual(cost, ShieldRule.GuardCost(kind, percent), Eps, "방패 버팀");
            Assert.IsTrue(ShieldRule.Blockable(kind));

            var o = Resolve(Query(kind, 10f, hasTravel: kind != HitKind.Melee, percent: percent));
            Assert.AreEqual(HitResult.Blocked, o.Result);
            Assert.AreEqual(damage, o.DamageScale, Eps);
            Assert.AreEqual(knock, o.KnockScale, Eps);
            Assert.AreEqual(Math.Max(0f, ShieldRule.GuardMax - cost), o.MeterAfter, Eps);
            Assert.AreEqual(kind == HitKind.BossSlam, o.Broke, "가득 찬 버팀에서는 내려찍기만 깨짐");
        }

        [TestCase(HitKind.FromAbove)]
        [TestCase(HitKind.Trap)]
        public void FallingRocksAndTrapsCannotBeBlocked(HitKind kind)
        {
            Assert.IsFalse(ShieldRule.Blockable(kind));
            Assert.IsFalse(ShieldRule.Parryable(kind));
            Assert.AreEqual(1f, ShieldRule.DamageScale(kind));
            Assert.AreEqual(1f, ShieldRule.KnockScale(kind));
            Assert.AreEqual(0f, ShieldRule.GuardCost(kind, 120f));
            var o = Resolve(Query(kind, 0f, parryOpen: true));
            Assert.AreEqual(HitResult.Hit, o.Result);
            Assert.AreEqual(1f, o.DamageScale);
            Assert.AreEqual(ShieldRule.GuardMax, o.MeterAfter);
        }

        [Test]
        public void GeneralDamageScaleIsTheTuningKnob()
        {
            // 일반(근접·멧돼지 돌진·꿰뚫는 화살)만 손잡이를 따른다. 보스·화살은 표 그대로.
            Assert.AreEqual(0.4f, ShieldRule.DamageScale(HitKind.Melee, 0.4f), Eps);
            Assert.AreEqual(0.4f, ShieldRule.DamageScale(HitKind.Rush, 0.4f), Eps);
            Assert.AreEqual(0.4f, ShieldRule.DamageScale(HitKind.PierceArrow, 0.4f), Eps);
            Assert.AreEqual(0f, ShieldRule.DamageScale(HitKind.Arrow, 0.4f), Eps);
            Assert.AreEqual(0.5f, ShieldRule.DamageScale(HitKind.BossSweep, 0.4f), Eps);
            Assert.AreEqual(0.6f, ShieldRule.DamageScale(HitKind.BossSlam, 0.4f), Eps);
            Assert.AreEqual(0f, Resolve(Query(HitKind.Melee, 0f, general: 0f)).DamageScale, Eps);
            Assert.AreEqual(0.5f, Resolve(Query(HitKind.Melee, 0f, general: 0.5f)).DamageScale, Eps);
        }

        /// <summary>패링 창 안 + 패링되는 종류 + 앞이면 튕김: 피해·밀림 0, 방패 버팀 +20(가득 넘지 않음).</summary>
        [TestCase(HitKind.Melee)]
        [TestCase(HitKind.Rush)]
        [TestCase(HitKind.Arrow)]
        [TestCase(HitKind.PierceArrow)]
        [TestCase(HitKind.BossSweep)]
        public void ParryableKinds(HitKind kind)
        {
            Assert.IsTrue(ShieldRule.Parryable(kind));
            var o = Resolve(Query(kind, 20f, hasTravel: kind != HitKind.Melee, parryOpen: true, meter: 50f));
            Assert.AreEqual(HitResult.Parried, o.Result);
            Assert.AreEqual(0f, o.DamageScale);
            Assert.AreEqual(0f, o.KnockScale);
            Assert.AreEqual(70f, o.MeterAfter, Eps);
            Assert.IsFalse(o.Broke);
            Assert.IsTrue(o.Stopped);
            Assert.AreEqual(ShieldRule.GuardMax, Resolve(Query(kind, 0f, hasTravel: true, parryOpen: true, meter: 95f)).MeterAfter, Eps, "가득에서 자름");
            // 뒤에서 오면 창이 열려 있어도 맞음.
            Assert.AreEqual(HitResult.Hit, Resolve(Query(kind, 180f, hasTravel: true, parryOpen: true)).Result);
        }

        /// <summary>패링 안 되는 종류: 오우거 내려찍기(막지만 늘 깨짐)·돌진(그냥 막음), 낙석·덫(못 막음).</summary>
        [Test]
        public void NonParryableKinds()
        {
            Assert.IsFalse(ShieldRule.Parryable(HitKind.BossSlam));
            Assert.IsFalse(ShieldRule.Parryable(HitKind.BossRush));
            var rush = Resolve(Query(HitKind.BossRush, 0f, hasTravel: true, parryOpen: true, percent: 150f));
            Assert.AreEqual(HitResult.Blocked, rush.Result);
            Assert.AreEqual(0.5f, rush.DamageScale, Eps);
            Assert.AreEqual(50f, rush.MeterAfter, Eps);
            Assert.IsFalse(rush.Broke);
            int parryable = 0;
            foreach (HitKind k in Enum.GetValues(typeof(HitKind)))
                if (ShieldRule.Parryable(k)) parryable++;
            Assert.AreEqual(5, parryable);
        }

        /// <summary>오우거 내려찍기는 앞이면 막지만 버팀과 상관없이 늘 깨진다(정답은 구르기). 창이 열려 있어도 튕기지 않는다.</summary>
        [Test]
        public void BossSlamAlwaysBreaks()
        {
            Assert.IsTrue(ShieldRule.AlwaysBreaks(HitKind.BossSlam));
            foreach (HitKind k in Enum.GetValues(typeof(HitKind)))
                if (k != HitKind.BossSlam) Assert.IsFalse(ShieldRule.AlwaysBreaks(k), k.ToString());
            foreach (bool open in new[] { false, true })
            {
                var o = Resolve(Query(HitKind.BossSlam, 0f, hasTravel: false, parryOpen: open, percent: 250f));
                Assert.AreEqual(HitResult.Blocked, o.Result);
                Assert.IsTrue(o.Broke);
                Assert.AreEqual(0.6f, o.DamageScale, Eps);
                Assert.AreEqual(1f, o.KnockScale, Eps);
                Assert.AreEqual(0f, o.MeterAfter, Eps);
            }
            Assert.AreEqual(HitResult.Hit, Resolve(Query(HitKind.BossSlam, 150f)).Result, "뒤에서는 그냥 맞음");
        }

        /// <summary>
        /// 오우거 내려찍기: 때린 자리(원 중심)는 오우거 앞 1.8이라 붙어 서면 몸 0.4 안이거나 몸 뒤에 온다. 진행 방향(오우거 → 플레이어)을 넘기면
        /// 거리와 상관없이 오우거 쪽이 '온 쪽'이라 오우거를 보고 서면 막고(늘 깨짐), 등지면 못 막는다(0-3의 25).
        /// </summary>
        [Test]
        public void BossSlamNearCenterUsesTravel()
        {
            Assert.AreEqual(HitResult.Hit, Resolve(Query(HitKind.BossSlam, 0f, distance: 0.3f, percent: 250f)).Result, "진행 방향이 없으면 몸 안 타라 못 막음");
            var o = Resolve(Query(HitKind.BossSlam, 0f, distance: 0.3f, hasTravel: true, percent: 250f));
            Assert.AreEqual(HitResult.Blocked, o.Result, "진행 방향이 있으면 몸 앞 0.3도 막음");
            Assert.IsTrue(o.Broke);
            Assert.AreEqual(0.6f, o.DamageScale, Eps);
            Assert.AreEqual(HitResult.Hit, Resolve(Query(HitKind.BossSlam, 180f, distance: 0f, hasTravel: true, percent: 250f)).Result, "등지면 못 막음");
        }

        /// <summary>방패 버팀이 0이 되면 막기 깨짐(그 타는 막은 것으로 친다). 굴쥐 넷이 앞에서 물면 넷째에 깨진다.</summary>
        [Test]
        public void MeterZeroBreaksGuard()
        {
            var o = Resolve(Query(HitKind.Melee, 0f, meter: 25f, percent: 100f));
            Assert.AreEqual(HitResult.Blocked, o.Result);
            Assert.AreEqual(0f, o.MeterAfter, Eps);
            Assert.IsTrue(o.Broke, "정확히 0도 깨짐");
            var o2 = Resolve(Query(HitKind.Melee, 0f, meter: 10f, percent: 100f));
            Assert.IsTrue(o2.Broke);
            Assert.AreEqual(0f, o2.MeterAfter, "0 아래로 안 감");
            var o3 = Resolve(Query(HitKind.Melee, 0f, meter: 25.5f, percent: 100f));
            Assert.IsFalse(o3.Broke);
            Assert.AreEqual(0.5f, o3.MeterAfter, Eps);

            float meter = ShieldRule.GuardMax;
            int bites = 0;
            bool broke = false;
            while (!broke && bites < 10)
            {
                var b = Resolve(Query(HitKind.Melee, 0f, meter: meter, percent: 100f));
                meter = b.MeterAfter;
                broke = b.Broke;
                bites++;
            }
            Assert.AreEqual(4, bites, "굴쥐 물기 25씩 넷째에 깨짐");
            // 범위 밖 버팀은 0~100으로 자른다.
            Assert.AreEqual(ShieldRule.GuardMax - 25f, Resolve(Query(HitKind.Melee, 0f, meter: 500f)).MeterAfter, Eps);
            Assert.AreEqual(ShieldRule.GuardMax, Resolve(Query(HitKind.Melee, 180f, meter: 500f)).MeterAfter, Eps);
        }

        /// <summary>패링 창: 들기 시작부터 0.18초까지(0.179 튕김 / 0.181 막기). 시험 손잡이 창 길이를 따른다.</summary>
        [Test]
        public void ParryWindowEdges()
        {
            Assert.IsTrue(ShieldRule.ParryOpenAt(0f, true));
            Assert.IsTrue(ShieldRule.ParryOpenAt(0.179f, true));
            Assert.IsFalse(ShieldRule.ParryOpenAt(0.181f, true));
            Assert.IsFalse(ShieldRule.ParryOpenAt(0.1f, false), "이번 들기에 창이 없으면 닫힘");
            Assert.IsFalse(ShieldRule.ParryOpenAt(-0.01f, true));
            Assert.IsTrue(ShieldRule.ParryOpenAt(0.25f, true, 0.30f), "손잡이 0.30");
            Assert.IsFalse(ShieldRule.ParryOpenAt(0.11f, true, 0.10f), "손잡이 0.10");

            // 0.179초에 받은 굴쥐 물기는 튕김, 0.181초는 그냥 막기.
            Assert.AreEqual(HitResult.Parried, Resolve(Query(HitKind.Melee, 0f, parryOpen: ShieldRule.ParryOpenAt(0.179f, true))).Result);
            Assert.AreEqual(HitResult.Blocked, Resolve(Query(HitKind.Melee, 0f, parryOpen: ShieldRule.ParryOpenAt(0.181f, true))).Result);
        }

        /// <summary>연타 막기: 직전 들기 시작에서 0.5초면 창 없음, 0.6초면 있음, 패링에 성공했으면 바로 있음. 처음 들기는 늘 있음.</summary>
        [Test]
        public void ParryRearmAfterRepeatedRaise()
        {
            Assert.IsFalse(ShieldRule.ParryArmed(0.5f, false));
            Assert.IsFalse(ShieldRule.ParryArmed(0.599f, false));
            Assert.IsTrue(ShieldRule.ParryArmed(0.6f, false));
            Assert.IsTrue(ShieldRule.ParryArmed(0.1f, true), "패링 성공 뒤 즉시");
            Assert.IsTrue(ShieldRule.ParryArmed(float.PositiveInfinity, false), "처음 들기");
            // 오우거 휩쓸기 2연(간격 0.5초): 첫 타를 튕긴 뒤 떼고(밀쳐 내기 0.15 + 내리기 0.12 = 0.27) 다시 누르면 0.32~0.5에 든 창으로 둘째도 튕긴다.
            Assert.LessOrEqual(ShieldRule.ParryPushTime + ShieldRule.LowerTime, 0.5f - ShieldRule.ParryWindow);
        }

        /// <summary>
        /// 패링한 그 누름을 계속 누르고 있어 다시 드는 것(반격 찌르기 뒤)은 창이 없다(공짜 패링 없음). 새로 누른 들기는 연타 잠금 규칙 그대로(0-3의 20).
        /// </summary>
        [Test]
        public void SamePressReRaiseHasNoParryWindow()
        {
            Assert.IsFalse(ShieldRule.ParryArmedOnRaise(true, float.PositiveInfinity, true), "같은 누름");
            Assert.IsTrue(ShieldRule.ParryArmedOnRaise(false, 0.1f, true), "새 누름, 패링 성공 뒤");
            Assert.IsFalse(ShieldRule.ParryArmedOnRaise(false, 0.5f, false), "새 누름, 연타 잠금");
            Assert.IsTrue(ShieldRule.ParryArmedOnRaise(false, 0.6f, false));
        }

        /// <summary>
        /// 밀쳐 내기가 끝날 때(0-3의 20): 반격 창 안에서 그동안 왼쪽 클릭을 눌렀으면 반격 찌르기, 아니고 그 누름을 누르고 있으면 막기를 이어 감
        /// (예전처럼 0.15~0.25초 막기가 꺼지는 구멍이 없음), 뗐으면 자유.
        /// </summary>
        [Test]
        public void ParryPushEndChoosesRiposteThenHold()
        {
            Assert.AreEqual(ParryPushEnd.Riposte, ShieldRule.AfterParryPush(true, true, true));
            Assert.AreEqual(ParryPushEnd.Riposte, ShieldRule.AfterParryPush(false, true, true));
            Assert.AreEqual(ParryPushEnd.Hold, ShieldRule.AfterParryPush(true, false, true));
            Assert.AreEqual(ParryPushEnd.Hold, ShieldRule.AfterParryPush(true, true, false), "반격 창이 없으면 왼쪽 클릭은 무시하고 막기");
            Assert.AreEqual(ParryPushEnd.Free, ShieldRule.AfterParryPush(false, false, true));
            Assert.AreEqual(ParryPushEnd.Free, ShieldRule.AfterParryPush(false, true, false));
        }

        /// <summary>톡 눌러도 0.25초는 들고, 내리는 동안 앞 0.06초까지는 막는다.</summary>
        [Test]
        public void MinimumHoldAndLowering()
        {
            Assert.IsFalse(ShieldRule.CanLower(0f));
            Assert.IsFalse(ShieldRule.CanLower(0.249f));
            Assert.IsTrue(ShieldRule.CanLower(0.25f));
            Assert.IsTrue(ShieldRule.LowerStillBlocks(0f));
            Assert.IsTrue(ShieldRule.LowerStillBlocks(0.05f));
            Assert.IsFalse(ShieldRule.LowerStillBlocks(0.07f));
            Assert.Less(ShieldRule.LowerBlockTime, ShieldRule.LowerTime);
            // 톡 누른 한 번의 막기 = 최소 0.25 + 내리기 0.12.
            Assert.AreEqual(0.37f, ShieldRule.MinHold + ShieldRule.LowerTime, Eps);
        }

        /// <summary>방어 게이지 회복: 막지 않을 때 0.5초 뒤부터 초당 50, 막는 중 1.0초 뒤부터 초당 10(아주 느리게, 0-3의 28), 가득에서 자름.</summary>
        [Test]
        public void RegenFormula()
        {
            Assert.AreEqual(40f, ShieldRule.Regen(40f, 0.49f, false, 0.1f), Eps, "0.5초 전 쉼");
            Assert.AreEqual(45f, ShieldRule.Regen(40f, 0.5f, false, 0.1f), Eps, "초당 50");
            Assert.AreEqual(40f, ShieldRule.Regen(40f, 0.99f, true, 0.1f), Eps, "막는 중 1.0초 전 쉼");
            Assert.AreEqual(41f, ShieldRule.Regen(40f, 1.0f, true, 0.1f), Eps, "막는 중 초당 10");
            Assert.AreEqual(ShieldRule.RegenIdlePerSecond / 5f, ShieldRule.RegenGuardPerSecond, Eps, "막는 중은 쉴 때의 1/5");
            Assert.AreEqual(100f, ShieldRule.Regen(99f, 5f, false, 0.1f), Eps, "가득에서 자름");
            Assert.AreEqual(40f, ShieldRule.Regen(40f, 5f, false, 0f), Eps);
            Assert.AreEqual(40f, ShieldRule.Regen(40f, 5f, false, -1f), Eps);
            // 깨진 뒤 0에서 다시 차는 시간: 막지 않으면 0.5 + 2.0 = 2.5초.
            float m = 0f;
            float t = 0f;
            const float Dt = 0.01f;
            while (m < ShieldRule.GuardMax && t < 10f)
            {
                m = ShieldRule.Regen(m, t, false, Dt);
                t += Dt;
            }
            Assert.AreEqual(2.5f, t, 0.03f);
        }
            // ── 패링 보상(2026-10-05 사용자 원문 "패링하면 상대 그로기 게이지가 까이고 데미지가 어느정도들어가게 하자") ──

        static TargetClass Target(MonsterKind kind, WeightClass weight, bool elite = false, bool boss = false) =>
            new TargetClass(kind, weight, elite, false, boss, false);

        [Test]
        public void ParryPoiseCutsGroggyGaugeByTarget()
        {
            // 굴쥐: 버팀 없음 → 0(휘청·밀림은 그대로).
            Assert.AreEqual(0.0, ShieldRule.ParryPoise(Target(MonsterKind.Rat, WeightClass.Light), MonsterRule.PoiseOf(MonsterKind.Rat)), 1e-9);
            // 궁수 30 → 15.
            Assert.AreEqual(15.0, ShieldRule.ParryPoise(Target(MonsterKind.Archer, WeightClass.Medium), MonsterRule.PoiseOf(MonsterKind.Archer)), 1e-4);
            // 멧돼지 머리치기·뒷발 90 → 45, 약한 멧돼지 70 → 35.
            Assert.AreEqual(45.0, ShieldRule.ParryPoise(Target(MonsterKind.Boar, WeightClass.Heavy), MonsterRule.PoiseOf(MonsterKind.Boar)), 1e-4);
            Assert.AreEqual(35.0, ShieldRule.ParryPoise(Target(MonsterKind.Boar, WeightClass.Heavy), MonsterRule.PoiseOf(MonsterKind.Boar, true)), 1e-4);
            // 정예는 줄인 35%: 정예 멧돼지 135 → 47.25, 굳은 정예 270 → 94.5.
            double elite = MonsterRule.PoiseOf(MonsterKind.Boar) * 1.5;
            Assert.AreEqual(47.25, ShieldRule.ParryPoise(Target(MonsterKind.Boar, WeightClass.Heavy, true), elite), 1e-4);
            Assert.AreEqual(94.5, ShieldRule.ParryPoise(Target(MonsterKind.Boar, WeightClass.Heavy, true), elite * 2), 1e-4);
            // 보스(오우거 휩쓸기) 6% 그대로: 250 / 300 / 400 → 15 / 18 / 24.
            var ogre = Target(MonsterKind.Ogre, WeightClass.Heavy, boss: true);
            Assert.AreEqual(15.0, ShieldRule.ParryPoise(ogre, BossRules.Poise(2)), 1e-4);
            Assert.AreEqual(18.0, ShieldRule.ParryPoise(ogre, BossRules.Poise(5)), 1e-4);
            Assert.AreEqual(24.0, ShieldRule.ParryPoise(ogre, BossRules.Poise(10)), 1e-4);
            Assert.AreEqual(15.0, ShieldRule.ParryPoise(Target(MonsterKind.Ogre, WeightClass.Heavy), 250, bossSweep: true), 1e-4);
            // 손잡이 0이면 버팀을 깎지 않는다(보스 6%는 손잡이 밖).
            Assert.AreEqual(0.0, ShieldRule.ParryPoise(Target(MonsterKind.Boar, WeightClass.Heavy), 90, 0f, 0f), 1e-9);
        }

        [Test]
        public void ParryDamageNeverOneShotsRatsButBitesOnEveryFloor()
        {
            // 공격력 30%, 치명 없음, 가장 높은 굴림도 1~10층 굴쥐 체력보다 작다(마무리는 반격 베기 몫). 가장 낮은 굴림도 1 이상.
            for (int floor = FloorScaling.MinFloor; floor <= FloorScaling.MaxFloor; floor++)
            {
                var b = FloorScaling.Baseline(floor);
                int ratHp = FloorScaling.MonsterHp(MonsterKind.Rat, floor);
                int max = ShieldRule.ParryDamage(b.Attack, ShieldRule.ParryDamagePercent, DamageMath.RollMax);
                int min = ShieldRule.ParryDamage(b.Attack, ShieldRule.ParryDamagePercent, DamageMath.RollMin);
                Assert.Less(max, ratHp, floor + "층 패링 피해가 굴쥐를 한 방에 죽임");
                Assert.GreaterOrEqual(min, 1, floor + "층");
                // 30% = 공격력 × 0.3 × 굴림(반올림).
                Assert.AreEqual(DamageMath.ToMonster(b.Attack, 30, false, 1.0, DamageMath.RollMax), max, floor + "층");
            }
            // 보스 피해 보너스는 받는다, 손잡이 0이면 피해 0.
            Assert.Greater(ShieldRule.ParryDamage(1000, 30f, 1.0, 0, 0.5, true), ShieldRule.ParryDamage(1000, 30f, 1.0));
            Assert.AreEqual(0, ShieldRule.ParryDamage(1000, 0f, 1.0));
        }

        [Test]
        public void ParryPlusRiposteBreaksBoarButNotElite()
        {
            // 반격 베기 버팀 = ③ 마무리 베기 38 × 2 = 76.
            double riposte = WeaponPresets.Longsword.combo[ShieldRule.RiposteStepNumber - 1].poiseDamage * ShieldRule.RipostePoiseScale;
            Assert.AreEqual(76.0, riposte, 1e-6);
            // 멧돼지(90): 패링만으로는 안 무너지고(45), 패링 + 반격이면 무너진다(121 ≥ 90).
            var boar = new PoiseMeter(MonsterRule.PoiseOf(MonsterKind.Boar));
            Assert.IsFalse(boar.Apply(ShieldRule.ParryPoise(Target(MonsterKind.Boar, WeightClass.Heavy), boar.Max), 0.0), "멧돼지 패링만");
            Assert.IsTrue(boar.Apply(riposte, 0.1), "멧돼지 패링 + 반격 베기");
            // 정예 멧돼지(135): 47.25 + 76 = 123.25 → 안 무너짐.
            var eliteBoar = new PoiseMeter(MonsterRule.PoiseOf(MonsterKind.Boar) * 1.5);
            Assert.IsFalse(eliteBoar.Apply(ShieldRule.ParryPoise(Target(MonsterKind.Boar, WeightClass.Heavy, true), eliteBoar.Max), 0.0));
            Assert.IsFalse(eliteBoar.Apply(riposte, 0.1), "정예 패링 + 반격 베기는 안 무너짐");
            // 오우거(250, 휩쓸기 두 타 튕김): 15 + 15 + 76 = 106 → 안 무너짐.
            var ogre = new PoiseMeter(BossRules.Poise(2), boss: true);
            var oc = Target(MonsterKind.Ogre, WeightClass.Heavy, boss: true);
            Assert.IsFalse(ogre.Apply(ShieldRule.ParryPoise(oc, ogre.Max), 0.0));
            Assert.IsFalse(ogre.Apply(ShieldRule.ParryPoise(oc, ogre.Max), 0.5));
            Assert.IsFalse(ogre.Apply(riposte, 0.6));
        }
    }
}
