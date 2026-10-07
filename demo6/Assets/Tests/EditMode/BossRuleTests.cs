using Demo6.Core.Combat;
using Demo6.Core.Progression;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 첫 보스 '갱도 오우거'(기획/전투-보스-무기-다듬기-1차.md 3장): 층별 체력, 3차 예고 규칙(2·5·10층, 2단계 포함), 걸어서 피하기 셈(걷기 배율 인자),
    /// 패턴 고르기 표('다가가며 A' 포함), 반원·등 각도 경계, 보스 버팀 250 → 500 상한.
    /// </summary>
    public sealed class BossRuleTests
    {
        static readonly BossPattern[] Patterns = { BossPattern.Slam, BossPattern.Charge, BossPattern.Sweep, BossPattern.Roar };

        [Test]
        public void HpAttackPoiseByFloor()
        {
            Assert.AreEqual(26416, BossRules.Hp(2), "시험판 2층 = 32,000 × 1.27 × 0.65");
            Assert.AreEqual(45785, BossRules.Hp(5), "5층 = 32,000 × 1.27⁴ × 0.55");
            Assert.AreEqual(275032, BossRules.Hp(10), "10층 = 32,000 × 1.27⁹");
            Assert.AreEqual(298, BossRules.Attack(2), "약 300 = 300 × 1.24 × 0.8");
            // 5층 이상은 3차 식: 300 × 층 배율 × 레벨 체력 보정((장비 기준 체력 + 120 × (권장 Lv − 1)) ÷ 장비 기준 체력).
            Assert.AreEqual(4636.0 / 3796.0, BossRules.AttackFactor(5), 1e-9, "5층 레벨 체력 보정 (3,796 + 120 × 7) ÷ 3,796");
            Assert.AreEqual(8876.0 / 7076.0, BossRules.AttackFactor(10), 1e-9, "10층 (7,076 + 120 × 15) ÷ 7,076");
            Assert.AreEqual(0.8, BossRules.AttackFactor(2), 1e-9, "시험판은 문서 계수 0.8");
            Assert.AreEqual(866, BossRules.Attack(5), "5층 = 300 × 1.24⁴ × 1.221");
            Assert.AreEqual(2608, BossRules.Attack(10), "약 2,600 = 300 × 1.24⁹ × 1.254");
            Assert.AreEqual(250, BossRules.Poise(2), 1e-9);
            Assert.AreEqual(300, BossRules.Poise(5), 1e-9);
            Assert.AreEqual(400, BossRules.Poise(10), 1e-9);
            Assert.AreEqual(3, BossRules.PlateLevel(2), "이름표 Lv = 층 + 1");
            Assert.AreEqual(2, BossRules.SpawnFloor(1), "시험장 1층이어도 보스는 2층 값");
            Assert.AreEqual(5, BossRules.SpawnFloor(5));
        }

        [Test]
        public void MonsterRuleAndXp()
        {
            var ogre = MonsterRule.Of(MonsterKind.Ogre, CombatRuleset.V3);
            Assert.AreEqual(BossRules.DisplayName, ogre.DisplayName);
            Assert.AreEqual(2.8f, ogre.MoveSpeed, 1e-6f);
            Assert.AreEqual(2.4f, ogre.Diameter, 1e-6f);
            Assert.AreEqual(1f, ogre.KnockbackResist, 1e-6f, "넉백 저항 100%");
            Assert.AreEqual(250, MonsterRule.PoiseOf(MonsterKind.Ogre), 1e-9);
            Assert.AreEqual(20.0, XpRules.KillUnits(MonsterKind.Ogre, false, false), 1e-9, "3-8 첫 처치 20U");
            Assert.AreEqual(0.0, XpRules.KillUnits(MonsterKind.Ogre, false, true), 1e-9, "전투 시험장(보상 없음)은 0");
        }

        [Test]
        public void ReferencePlayerIsTrialLevel3()
        {
            Assert.AreEqual(2640, BossRules.ReferencePlayerHp(2), "시험판 Lv 3 체력 2,640");
            Assert.AreEqual(120, BossRules.ReferencePlayerDefense(2));
            Assert.AreEqual(0.893, DamageMath.DefenseFactor(120), 0.001, "방어 120 → 피해 × 0.893");
            // 3-3 표의 시험판 한 방(문서는 공격 300으로 어림해 0.2%p 높다).
            Assert.AreEqual(0.254, BossRules.HitFraction(BossPattern.Slam, 2), 0.005);
            Assert.AreEqual(0.152, BossRules.HitFraction(BossPattern.Charge, 2), 0.005);
            Assert.AreEqual(0.112, BossRules.HitFraction(BossPattern.Sweep, 2), 0.005);
            Assert.AreEqual(0.122, BossRules.HitFraction(BossPattern.Roar, 2), 0.005);
        }

        [TestCase(2)]
        [TestCase(5)]
        [TestCase(10)]
        public void TelegraphsMeetThirdDraftRule(int floor)
        {
            foreach (bool phase2 in new[] { false, true })
            {
                foreach (var p in Patterns)
                {
                    float min = BossRules.MinTelegraph(BossRules.HitFraction(p, floor));
                    Assert.GreaterOrEqual(BossRules.Telegraph(p, phase2, floor), min, $"{floor}층 {p} 2단계={phase2}");
                }
                Assert.GreaterOrEqual(BossRules.Telegraph(BossPattern.Slam, phase2, floor), 0.9f, "20% 넘는 A는 0.9초 이상");
            }
            float chargeMin = BossRules.MinTelegraph(BossRules.HitFraction(BossPattern.Charge, floor));
            Assert.GreaterOrEqual(BossRules.ChargeReaimTelegraph(floor), chargeMin, "B 다시 조준");
            float sweepMin = BossRules.MinTelegraph(BossRules.HitFraction(BossPattern.Sweep, floor));
            Assert.GreaterOrEqual(BossRules.SweepSecondTelegraph(floor), sweepMin, "D 2타");
            Assert.AreEqual(BossRules.SweepInterval, BossRules.SweepWindup - BossRules.SweepSecondLead, 1e-6f, "D 판정 간격 0.5 = 0.6 − 0.1");
        }

        [Test]
        public void TrialTelegraphsAreDocValues()
        {
            Assert.AreEqual(1.0f, BossRules.Telegraph(BossPattern.Slam, false, 2), 1e-6f);
            Assert.AreEqual(0.9f, BossRules.Telegraph(BossPattern.Slam, true, 2), 1e-6f);
            Assert.AreEqual(0.7f, BossRules.Telegraph(BossPattern.Charge, false, 2), 1e-6f);
            Assert.AreEqual(0.6f, BossRules.Telegraph(BossPattern.Charge, true, 2), 1e-6f);
            // 다시 조준은 0.7(문서 바탕값 0.6에서 올림: 가속을 넣은 걸어서 비키기, EscapeTableAtPaceB).
            Assert.AreEqual(0.7f, BossRules.ChargeReaimTelegraph(2), 1e-6f);
            Assert.AreEqual(0.6f, BossRules.Telegraph(BossPattern.Sweep, true, 2), 1e-6f, "2단계 감소는 A·B 첫 조준에만");
            Assert.AreEqual(0.6f, BossRules.SweepSecondTelegraph(2), 1e-6f);
            Assert.AreEqual(1.0f, BossRules.Telegraph(BossPattern.Roar, true, 2), 1e-6f);
            // 5층·10층 B는 한 방 16~20%(5층 18.7%, 10층 17.0%)라 3차 규칙상 0.8초로 올라간다(문서 3-3은 시험판 숫자로만 셈, 사용자 판정 ②).
            foreach (int floor in new[] { 5, 10 })
            {
                Assert.Greater(BossRules.HitFraction(BossPattern.Charge, floor), 0.16, $"{floor}층 B 한 방");
                Assert.LessOrEqual(BossRules.HitFraction(BossPattern.Charge, floor), 0.20, $"{floor}층 B 한 방");
                Assert.AreEqual(0.8f, BossRules.Telegraph(BossPattern.Charge, false, floor), 1e-6f);
                Assert.AreEqual(0.8f, BossRules.Telegraph(BossPattern.Charge, true, floor), 1e-6f);
                Assert.AreEqual(0.8f, BossRules.ChargeReaimTelegraph(floor), 1e-6f);
            }
            Assert.AreEqual(0.187, BossRules.HitFraction(BossPattern.Charge, 5), 0.002);
        }

        /// <summary>
        /// 걸어서 피하기 셈(3-3 표): 걷기 배율을 인자로, 멈춘 데서 반응 0.25초·가속 0.16초를 넣어 셈. 공정 규칙이 깨지면 걸음을 바꾼 탓이 바로 드러난다.
        /// B(첫 조준은 거리 4에서, 다시 조준은 바로 옆에서)·C는 걸어서, A는 구르기로, D는 오우거가 멈추는 2.2에서 걷기 또는 구르기(재사용 5초 ≥ 구르기 2초).
        /// </summary>
        [TestCase(0.80f)]
        [TestCase(0.86f)]
        [TestCase(1.0f)]
        public void EscapesStayFair(float walkScale)
        {
            float walk = BossRules.ReferenceWalk * walkScale;
            foreach (bool phase2 in new[] { false, true })
            {
                float slam = BossRules.Telegraph(BossPattern.Slam, phase2, 2);
                Assert.IsTrue(BossRules.WalkEscapes(walk, slam, BossRules.SlamEscape) || BossRules.DodgeEscapes(BossRules.SlamEscape), "A는 구르기 3.5로 빠져나감");
                float firstAim = BossRules.Telegraph(BossPattern.Charge, phase2, 2) + BossRules.ChargeArrival(BossRules.ChargeMinDistance);
                Assert.IsTrue(BossRules.WalkEscapes(walk, firstAim, BossRules.ChargeEscape), "B 첫 조준(거리 4 이상) 옆으로 1.4");
            }
            // 다시 조준은 거리 조건이 없어 바로 옆(돌진이 닿기까지 0초)이 최악이고, 2단계 3연 간격은 구르기 재사용보다 짧아 걸어서 비켜야 한다.
            Assert.AreEqual(0f, BossRules.ChargeArrival(BossRules.Diameter * 0.5f + BossRules.PlayerRadius), 1e-6f);
            Assert.IsTrue(BossRules.WalkEscapes(walk, BossRules.ChargeReaimTelegraph(2) + BossRules.ChargeArrival(0f), BossRules.ChargeEscape), "B 다시 조준(바로 옆) 옆으로 1.4");
            float sweepNear = BossRules.SweepEscape(BossRules.StopDistance);
            Assert.IsTrue(BossRules.WalkEscapes(walk, BossRules.Telegraph(BossPattern.Sweep, false, 2), sweepNear) || BossRules.DodgeEscapes(sweepNear), "D 2.2에서 걷기 또는 구르기");
            Assert.GreaterOrEqual(BossRules.SweepCooldown, BossRules.DodgeCooldown, "D 재사용 5초 ≥ 구르기 재사용 2초");
            Assert.IsTrue(BossRules.WalkEscapes(walk, BossRules.Telegraph(BossPattern.Sweep, false, 2), BossRules.SweepEscape()), "D 고르는 거리 끝 2.5에서는 걸어서 1.1");
            Assert.IsTrue(BossRules.WalkEscapes(walk, BossRules.Telegraph(BossPattern.Roar, false, 2), BossRules.RockEscape), "C 발밑 원 걸어 나가기 1.6");
            Assert.GreaterOrEqual(BossRules.SlamCooldown, BossRules.DodgeCooldown, "A 재사용 4초 ≥ 구르기 재사용 2초");
        }

        [Test]
        public void WalkDistanceIncludesAcceleration()
        {
            // 가속 0이면 예전 식(걷기 × (예고 − 반응)).
            Assert.AreEqual(4.24f * 0.35f, BossRules.WalkDistance(4.24f, 0.6f, 0.25f, 0f), 1e-5f);
            // 다 빨라진 뒤: 걷기 × (t − 가속 ÷ 2). 0.6초 예고면 4.24 × (0.35 − 0.08) = 1.145(통합 검토 ①의 50Hz 모사 1.145와 같음).
            Assert.AreEqual(1.1448f, BossRules.WalkDistance(4.24f, 0.6f), 1e-3f);
            // 다 빨라지기 전: 걷기 × t² ÷ (2 × 가속).
            Assert.AreEqual(4.24f * 0.1f * 0.1f / 0.32f, BossRules.WalkDistance(4.24f, 0.35f), 1e-5f);
            Assert.AreEqual(0f, BossRules.WalkDistance(4.24f, 0.2f), 1e-6f, "반응 전에는 못 감");
            // 이어짐: t = 가속에서 두 식이 같다(0.5 × 걷기 × 가속).
            Assert.AreEqual(4.24f * 0.08f, BossRules.WalkDistance(4.24f, 0.25f + BossRules.WalkAccelTime), 1e-5f);
            Assert.AreEqual(2.2f, BossRules.StopDistance, 1e-6f);
            Assert.AreEqual(1.4f, BossRules.SweepEscape(BossRules.StopDistance), 1e-5f);
            Assert.AreEqual(0.218f, BossRules.ChargeArrival(BossRules.ChargeMinDistance), 0.001f, "첫 조준 거리 4: (4 − 1.6) ÷ 11");
        }

        [Test]
        public void EscapeTableAtPaceB()
        {
            const float pace = 0.80f;
            float walk = BossRules.ReferenceWalk * pace;
            Assert.AreEqual(4.24f, walk, 1e-4f);
            Assert.AreEqual(3.4f, BossRules.SlamEscape, 1e-6f);
            Assert.AreEqual(1.4f, BossRules.ChargeEscape, 1e-6f);
            Assert.AreEqual(1.1f, BossRules.SweepEscape(), 1e-5f);
            Assert.AreEqual(1.6f, BossRules.RockEscape, 1e-6f);

            // A: 걸어서 못 나감(1단계 2.84, 2단계 2.42 < 3.4) → 구르기 3.5 ≥ 3.4가 정답.
            Assert.AreEqual(2.84f, BossRules.WalkDistance(walk, BossRules.Telegraph(BossPattern.Slam, false, 2)), 0.01f);
            Assert.AreEqual(2.42f, BossRules.WalkDistance(walk, BossRules.Telegraph(BossPattern.Slam, true, 2)), 0.01f);
            Assert.IsFalse(BossRules.WalkEscapes(walk, BossRules.Telegraph(BossPattern.Slam, false, 2), BossRules.SlamEscape));
            Assert.IsFalse(BossRules.WalkEscapes(walk, BossRules.Telegraph(BossPattern.Slam, true, 2), BossRules.SlamEscape));
            Assert.IsTrue(BossRules.DodgeEscapes(BossRules.SlamEscape));

            // B 다시 조준(바로 옆): 0.6초였다면 1.14 < 1.4(걸어서 못 비킴), 0.7초라 1.57 ≥ 1.4.
            Assert.AreEqual(1.14f, BossRules.WalkDistance(walk, 0.6f), 0.01f);
            Assert.IsFalse(BossRules.WalkEscapes(walk, 0.6f, BossRules.ChargeEscape), "예전 0.6초는 바로 옆 다시 조준을 걸어서 못 비킴");
            Assert.AreEqual(1.57f, BossRules.WalkDistance(walk, BossRules.ChargeReaimTelegraph(2)), 0.01f);
            Assert.IsTrue(BossRules.WalkEscapes(walk, BossRules.ChargeReaimTelegraph(2), BossRules.ChargeEscape));
            // 2단계 3연 간격(돌진 0.82 + 발 끌기 0.35 + 다시 조준 0.7 ≈ 1.87초)은 구르기 재사용 2.0초보다 짧다 → 다시 조준은 걸어서 비켜야 한다.
            float gap = BossRules.ChargeLength / BossRules.ChargeSpeed + BossRules.ChargeDrag + BossRules.ChargeReaim;
            Assert.Less(gap, BossRules.DodgeCooldown);

            // D: 고르는 거리 끝 2.5에서는 걸어서 1.14 ≥ 1.1, 오우거가 멈추는 2.2에서는 1.14 < 1.4라 구르기(3.5)가 정답(A와 같음).
            Assert.IsTrue(BossRules.WalkEscapes(walk, BossRules.Telegraph(BossPattern.Sweep, false, 2), BossRules.SweepEscape()));
            Assert.IsFalse(BossRules.WalkEscapes(walk, BossRules.Telegraph(BossPattern.Sweep, false, 2), BossRules.SweepEscape(BossRules.StopDistance)));
            Assert.IsTrue(BossRules.DodgeEscapes(BossRules.SweepEscape(BossRules.StopDistance)));
            // C 발밑 2.84 ≥ 1.6.
            Assert.IsTrue(BossRules.WalkEscapes(walk, BossRules.Telegraph(BossPattern.Roar, false, 2), BossRules.RockEscape));

            // 알려진 위험(7장 #5): 판금 한 벌 3.76이면 B 다시 조준 바로 옆 1.39 < 1.4. 첫 돌진 뒤 미리 옆으로 걸어야 한다.
            float plate = BossRules.PlateWalk * pace;
            Assert.AreEqual(3.76f, plate, 1e-4f);
            Assert.AreEqual(1.39f, BossRules.WalkDistance(plate, BossRules.ChargeReaimTelegraph(2)), 0.01f);
            Assert.IsFalse(BossRules.WalkEscapes(plate, BossRules.ChargeReaimTelegraph(2), BossRules.ChargeEscape), "알려진 위험: 판금 B 다시 조준");
        }

        static BossPickInput Input(float distance, bool phase2 = false, bool slam = true, bool charge = true, bool sweep = true, bool roar = true, BossPattern? last = null) =>
            new BossPickInput { Distance = distance, Phase2 = phase2, SlamReady = slam, ChargeReady = charge, SweepReady = sweep, RoarReady = roar, Last = last };

        [Test]
        public void PickTable()
        {
            // ① 2단계이고 C 준비 → C(거리와 상관없이 가장 먼저).
            Assert.AreEqual(BossPattern.Roar, BossRules.Pick(Input(2f, phase2: true)).Pattern);
            Assert.AreEqual(BossPattern.Roar, BossRules.Pick(Input(8f, phase2: true)).Pattern);
            // 1단계에서는 C를 쓰지 않는다.
            Assert.AreEqual(BossPattern.Charge, BossRules.Pick(Input(8f, roar: true)).Pattern);
            // ② 거리 4 이상이고 B 준비 → B.
            Assert.AreEqual(BossPattern.Charge, BossRules.Pick(Input(4f)).Pattern);
            // ③ 거리 2.5 안이고 D 준비(직전이 D 아님) → D.
            Assert.AreEqual(BossPattern.Sweep, BossRules.Pick(Input(2.5f)).Pattern);
            Assert.AreEqual(BossPattern.Slam, BossRules.Pick(Input(2f, last: BossPattern.Sweep)).Pattern, "D는 두 번 잇지 않는다");
            // ④ 거리 3.0 안 → A.
            Assert.AreEqual(BossPattern.Slam, BossRules.Pick(Input(2.8f)).Pattern);
            Assert.IsFalse(BossRules.Pick(Input(2.8f)).ApproachThenSlam);
            // ⑤ 거리 3.0~4.0이고 B 재사용 중 → 0.4초 다가간 뒤 A(창 뒷걸음 찌르기 꼼수 막기).
            var approach = BossRules.Pick(Input(3.5f, charge: false));
            Assert.AreEqual(BossPattern.Slam, approach.Pattern);
            Assert.IsTrue(approach.ApproachThenSlam);
            // ⑥ 그 밖에는 걸어서 다가감.
            Assert.IsFalse(BossRules.Pick(Input(3.5f)).Pattern.HasValue, "3.0~4.0인데 B 준비면 걷는다(B는 4 이상)");
            Assert.IsFalse(BossRules.Pick(Input(6f, charge: false)).Pattern.HasValue);
            Assert.IsFalse(BossRules.Pick(Input(2f, slam: false, sweep: false)).Pattern.HasValue);
            Assert.IsFalse(BossRules.Pick(Input(3.5f, slam: false, charge: false)).Pattern.HasValue, "A 재사용 중이면 다가가며 A도 없음");
        }

        [Test]
        public void ForcedPatternWaitsForRangeOnlyForMelee()
        {
            Assert.AreEqual(BossPattern.Slam, BossRules.PickForced(BossPattern.Slam, Input(2.9f, slam: false)).Pattern, "강제는 재사용을 보지 않는다");
            Assert.IsFalse(BossRules.PickForced(BossPattern.Slam, Input(3.5f)).Pattern.HasValue, "A는 3.0 안까지 걸어간다");
            Assert.AreEqual(BossPattern.Sweep, BossRules.PickForced(BossPattern.Sweep, Input(2f, last: BossPattern.Sweep)).Pattern);
            Assert.IsFalse(BossRules.PickForced(BossPattern.Sweep, Input(2.8f)).Pattern.HasValue);
            Assert.AreEqual(BossPattern.Charge, BossRules.PickForced(BossPattern.Charge, Input(1f, charge: false)).Pattern);
            Assert.AreEqual(BossPattern.Roar, BossRules.PickForced(BossPattern.Roar, Input(5f)).Pattern, "C 강제는 1단계에서도 쓴다");
        }

        [Test]
        public void RestAndPhaseNumbers()
        {
            Assert.AreEqual(0.9f, BossRules.Rest(false, 0f), 1e-6f);
            Assert.AreEqual(1.3f, BossRules.Rest(false, 1f), 1e-6f);
            Assert.AreEqual(0.6f, BossRules.Rest(true, 0f), 1e-6f);
            Assert.AreEqual(1.0f, BossRules.Rest(true, 1f), 1e-6f);
            Assert.AreEqual(2, BossRules.Charges(false));
            Assert.AreEqual(3, BossRules.Charges(true));
            Assert.AreEqual(3.36f, BossRules.WalkSpeed * BossRules.Phase2SpeedScale, 1e-5f);
            Assert.AreEqual(160f, BossRules.Turn(true), 1e-6f);
            // 3연 돌진 간격 약 1.9초(0.82 + 0.35 + 다시 조준 0.7)라 구르기 재사용 2.0초로는 셋 다 못 받는다 → 걸어서 비키게 폭 2.0, 다시 조준 0.7.
            float gap = BossRules.ChargeLength / BossRules.ChargeSpeed + BossRules.ChargeDrag + BossRules.ChargeReaim;
            Assert.Less(gap, BossRules.DodgeCooldown);
            Assert.AreEqual(BossRules.TransitionTime, 1.2f, 1e-6f);
            Assert.Less(BossRules.TransitionSlowAt, BossRules.TransitionRoarAt);
        }

        [Test]
        public void HalfDiscBoundary()
        {
            const float r = 3.2f;
            // 바라보는 쪽 +x: 반경 경계와 반경 + pad.
            Assert.IsTrue(SectorMath.InHalfDisc(0f, 0f, 1f, 0f, r, 3.2f, 0f));
            Assert.IsFalse(SectorMath.InHalfDisc(0f, 0f, 1f, 0f, r, 3.21f, 0f));
            Assert.IsTrue(SectorMath.InHalfDisc(0f, 0f, 1f, 0f, r, 3.6f, 0f, 0.4f));
            Assert.IsFalse(SectorMath.InHalfDisc(0f, 0f, 1f, 0f, r, 3.61f, 0f, 0.4f));
            // 반각 90°: 옆(90°)은 안, 조금이라도 뒤면 밖. pad면 곧은 가장자리에서 0.4까지 안.
            Assert.IsTrue(SectorMath.InHalfDisc(0f, 0f, 1f, 0f, r, 0f, 3f));
            Assert.IsFalse(SectorMath.InHalfDisc(0f, 0f, 1f, 0f, r, -0.01f, 3f));
            Assert.IsTrue(SectorMath.InHalfDisc(0f, 0f, 1f, 0f, r, -0.39f, 3f, 0.4f));
            Assert.IsFalse(SectorMath.InHalfDisc(0f, 0f, 1f, 0f, r, -0.41f, 3f, 0.4f));
            // 모서리는 넉넉하게 보지 않는다: 곧은 가장자리 끝 (0, 3.2)에서 0.49 떨어진 점은 반경 + pad 안이어도 밖.
            Assert.IsFalse(SectorMath.InHalfDisc(0f, 0f, 1f, 0f, r, -0.35f, 3.55f, 0.4f));
            Assert.IsTrue(SectorMath.InHalfDisc(0f, 0f, 1f, 0f, r, -0.2f, 3.3f, 0.4f));
            // 뒤쪽·가운데·다른 방향.
            Assert.IsFalse(SectorMath.InHalfDisc(0f, 0f, 1f, 0f, r, -1f, 0f));
            Assert.IsTrue(SectorMath.InHalfDisc(0f, 0f, 1f, 0f, r, -0.3f, 0f, 0.4f), "몸 가운데 pad 안은 늘 안");
            Assert.IsTrue(SectorMath.InHalfDisc(5f, 5f, 0f, 2f, r, 5f, 8f));
            Assert.IsFalse(SectorMath.InHalfDisc(5f, 5f, 0f, 2f, r, 5f, 4f));
        }

        [Test]
        public void SectorEdgeWithPadIsDistanceToEdge()
        {
            // 반각 45° 부채꼴(+x): 옆 경계선 바깥 0.3에 있는 점은 pad 0.4면 안, 0.5면 밖.
            float a = 45f * (float)System.Math.PI / 180f;
            float ex = 2f * (float)System.Math.Cos(a), ey = 2f * (float)System.Math.Sin(a);
            float nx = -(float)System.Math.Sin(a), ny = (float)System.Math.Cos(a);
            Assert.IsTrue(SectorMath.InSector(0f, 0f, 1f, 0f, 3f, 45f, ex + nx * 0.3f, ey + ny * 0.3f, 0.4f));
            Assert.IsFalse(SectorMath.InSector(0f, 0f, 1f, 0f, 3f, 45f, ex + nx * 0.5f, ey + ny * 0.5f, 0.4f));
            Assert.IsTrue(SectorMath.InSector(0f, 0f, 1f, 0f, 3f, 180f, -2.9f, 0f), "반각 180°는 원 전체");
        }

        [Test]
        public void BehindSixtyDegrees()
        {
            // 바라보는 쪽 +x, 등 뒤 ±60°(뒤쪽 120°).
            Assert.IsTrue(SectorMath.IsBehind(1f, 0f, -1f, 0f, 60f));
            Assert.IsTrue(SectorMath.IsBehind(1f, 0f, Cos(121f), Sin(121f), 60f));
            Assert.IsFalse(SectorMath.IsBehind(1f, 0f, Cos(119f), Sin(119f), 60f));
            Assert.IsTrue(SectorMath.IsBehind(1f, 0f, Cos(-121f), Sin(-121f), 60f));
            Assert.IsFalse(SectorMath.IsBehind(1f, 0f, Cos(-119f), Sin(-119f), 60f));
            Assert.IsFalse(SectorMath.IsBehind(1f, 0f, 1f, 0f, 60f));
            Assert.IsFalse(SectorMath.IsBehind(1f, 0f, 0f, 0f, 60f), "같은 자리는 등 뒤가 아님");
            Assert.AreEqual(SectorMath.IsBehind(0f, 1f, Cos(-60f), Sin(-60f), 60f), BackstabRule.IsBehind(0f, 1f, Cos(-60f), Sin(-60f)), "기습 처형·단검이 같은 식을 쓴다");
        }

        [Test]
        public void BossPoiseGrowsToDouble()
        {
            var m = new PoiseMeter(BossRules.Poise(2), boss: true);
            Assert.AreEqual(250, m.Max, 1e-9);
            double[] expect = { 312.5, 375, 437.5, 500, 500 };
            foreach (double e in expect)
            {
                m.Apply(m.Current, 0);
                m.Recover();
                Assert.AreEqual(e, m.Max, 1e-9);
                Assert.AreEqual(m.Max, m.Current, 1e-9);
            }
            // 기습 첫 타: 버팀 × 3, 한 번에 최대치의 70%(175)까지.
            Assert.AreEqual(175, PoiseMeter.AmbushDamage(60, true, 250), 1e-9);
            Assert.AreEqual(90, PoiseMeter.AmbushDamage(30, true, 250), 1e-9);
        }

        static float Cos(float deg) => (float)System.Math.Cos(deg * System.Math.PI / 180.0);
        static float Sin(float deg) => (float)System.Math.Sin(deg * System.Math.PI / 180.0);
    }
}
