using Demo6.Core.Combat;
using Demo6.Core.Loot;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 오른쪽 클릭 무기 행동 공통 규칙(기획/세-무기-우클릭-소켓-1차.md 0-3·5장·6-7): 무기 9종의 행동 종류, 이름·카드 줄, 걸음 배율·돌기 속도,
    /// 버팀 룬(대검 기 모으기·놓아 베기만, 시험 손잡이), 맞으면 끊김 표(5-5), 끊김 경직 0.35/0.45초.
    /// </summary>
    public sealed class WeaponActRuleTests
    {
        const float Eps = 1e-6f;

        [TestCase("wpn_longsword", WeaponActKind.Guard)]
        [TestCase("wpn_greatsword", WeaponActKind.Charge)]
        [TestCase("wpn_twinblades", WeaponActKind.Flurry)]
        [TestCase("wpn_maul", WeaponActKind.None)]
        [TestCase("wpn_spear", WeaponActKind.None)]
        [TestCase("wpn_scythe", WeaponActKind.None)]
        [TestCase("wpn_axe", WeaponActKind.None)]
        [TestCase("wpn_dagger", WeaponActKind.None)]
        [TestCase("wpn_flail", WeaponActKind.None)]
        public void KindOfEveryWeapon(string weaponId, WeaponActKind kind)
        {
            Assert.AreEqual(kind, WeaponActRules.KindOf(weaponId));
        }

        [Test]
        public void KindOfCoversAllNineAndUnknownIsNone()
        {
            int withAct = 0;
            foreach (var w in WeaponPresets.All)
                if (WeaponActRules.KindOf(w.id) != WeaponActKind.None) withAct++;
            Assert.AreEqual(3, withAct, "행동이 있는 무기는 세 무기뿐(0-1의 6)");
            Assert.AreEqual(WeaponActKind.None, WeaponActRules.KindOf(null));
            Assert.AreEqual(WeaponActKind.None, WeaponActRules.KindOf(""));
            Assert.AreEqual(WeaponActKind.None, WeaponActRules.KindOf("wpn_unknown"));
            Assert.AreEqual(WeaponActKind.None, WeaponActRules.KindOf("WPN_LONGSWORD"), "id는 글자 그대로");

            // 저장 id는 그대로(0-3의 1): 장비 표·콤보 프리셋과 같은 글자.
            Assert.AreEqual(GearBaseTable.Longsword, WeaponActRules.SwordShieldId);
            Assert.AreEqual(GearBaseTable.Greatsword, WeaponActRules.GreatswordId);
            Assert.AreEqual(GearBaseTable.Twinblades, WeaponActRules.TwinbladesId);
            Assert.AreEqual(WeaponPresets.Longsword.id, WeaponActRules.SwordShieldId);
            Assert.AreEqual(WeaponPresets.Greatsword.id, WeaponActRules.GreatswordId);
            Assert.AreEqual(WeaponPresets.Twinblades.id, WeaponActRules.TwinbladesId);
        }

        [Test]
        public void NamesAndCardLines()
        {
            Assert.AreEqual("막기", WeaponActRules.Name(WeaponActKind.Guard));
            Assert.AreEqual("기 모으기", WeaponActRules.Name(WeaponActKind.Charge));
            Assert.AreEqual("난사", WeaponActRules.Name(WeaponActKind.Flurry));
            Assert.AreEqual("없음", WeaponActRules.Name(WeaponActKind.None));

            // 카드 행동 줄(2-1·3-1·4-1). 행동이 없으면 줄을 넣지 않는다(null). 가방 카드에서 잘리지 않게 짧게, 키 이름은 HUD와 같은 '우클릭'(0-3의 27).
            Assert.AreEqual("우클릭: 막기 · 제때 들면 튕김", WeaponActRules.CardLine(WeaponActKind.Guard));
            Assert.AreEqual("우클릭: 기 모으기 · 놓으면 강타", WeaponActRules.CardLine(WeaponActKind.Charge));
            Assert.AreEqual("우클릭: 난사 · 6초에 한 번", WeaponActRules.CardLine(WeaponActKind.Flurry));
            Assert.IsNull(WeaponActRules.CardLine(WeaponActKind.None));
            foreach (var k in new[] { WeaponActKind.Guard, WeaponActKind.Charge, WeaponActKind.Flurry })
            {
                StringAssert.StartsWith(WeaponActCommon.ActKeyLabel + ":", WeaponActRules.CardLine(k));
                Assert.LessOrEqual(WeaponActRules.CardLine(k).Length, 20, "카드 너비 " + k);
            }

            foreach (var w in WeaponPresets.All)
                Assert.AreEqual(WeaponActRules.CardLine(WeaponActRules.KindOf(w.id)), WeaponActRules.CardLine(w.id), w.displayName);
            Assert.IsNull(WeaponActRules.CardLine((string)null));
            Assert.IsNull(WeaponActRules.CardLine("wpn_dagger"));
        }

        /// <summary>걸음 배율(0-3의 9, 2-5, 3-6·3-7, 4-6). 시작 걸음 4.24에 곱한다.</summary>
        [TestCase(WeaponActKind.Guard, WeaponActPhase.Raise, 0, 0.5f)]
        [TestCase(WeaponActKind.Guard, WeaponActPhase.Hold, 0, 0.5f)]
        [TestCase(WeaponActKind.Guard, WeaponActPhase.Lower, 0, 0.5f)]
        [TestCase(WeaponActKind.Guard, WeaponActPhase.Recoil, 0, 0.5f)]
        [TestCase(WeaponActKind.Guard, WeaponActPhase.ParryPush, 0, 0.2f)]
        [TestCase(WeaponActKind.Guard, WeaponActPhase.Break, 0, 0f)]
        [TestCase(WeaponActKind.Guard, WeaponActPhase.Flinch, 0, 0f)]
        [TestCase(WeaponActKind.Charge, WeaponActPhase.Charging, 0, 0.16f)]
        [TestCase(WeaponActKind.Charge, WeaponActPhase.Charging, 1, 0.13f)]
        [TestCase(WeaponActKind.Charge, WeaponActPhase.Charging, 2, 0.11f)]
        [TestCase(WeaponActKind.Charge, WeaponActPhase.Charging, 3, 0.09f)]
        [TestCase(WeaponActKind.Charge, WeaponActPhase.Release, 1, 0.15f)]
        [TestCase(WeaponActKind.Charge, WeaponActPhase.Release, 2, 0.10f)]
        [TestCase(WeaponActKind.Charge, WeaponActPhase.Release, 3, 0.05f)]
        [TestCase(WeaponActKind.Charge, WeaponActPhase.Flinch, 2, 0f)]
        [TestCase(WeaponActKind.Flurry, WeaponActPhase.Flurry, 0, 0.30f)]
        [TestCase(WeaponActKind.Flurry, WeaponActPhase.Flinch, 0, 0f)]
        [TestCase(WeaponActKind.None, WeaponActPhase.None, 0, 1f)]
        public void MoveScaleTable(WeaponActKind kind, WeaponActPhase phase, int level, float expected)
        {
            Assert.AreEqual(expected, WeaponActRules.MoveScale(kind, phase, level), Eps);
        }

        [Test]
        public void MoveScaleMatchesSubRules()
        {
            // 막기 4.24 → 2.12(웅크림 2.33보다 느림), 기 모으기 0.68 → 0.38, 난사 1.27.
            const float Walk = 4.24f;
            Assert.AreEqual(2.12f, Walk * WeaponActRules.MoveScale(WeaponActKind.Guard, WeaponActPhase.Hold, 0), 0.005f);
            Assert.AreEqual(0.68f, Walk * WeaponActRules.MoveScale(WeaponActKind.Charge, WeaponActPhase.Charging, 0), 0.005f);
            Assert.AreEqual(0.38f, Walk * WeaponActRules.MoveScale(WeaponActKind.Charge, WeaponActPhase.Charging, 3), 0.005f);
            Assert.AreEqual(1.27f, Walk * WeaponActRules.MoveScale(WeaponActKind.Flurry, WeaponActPhase.Flurry, 0), 0.005f);
            for (int level = 0; level <= 3; level++)
                Assert.AreEqual(GreatswordCharge.MoveScale(level), WeaponActRules.MoveScale(WeaponActKind.Charge, WeaponActPhase.Charging, level), Eps);
            for (int level = 1; level <= 3; level++)
                Assert.AreEqual(GreatswordCharge.Release(level).moveScale, WeaponActRules.MoveScale(WeaponActKind.Charge, WeaponActPhase.Release, level), Eps);
            Assert.AreEqual(TwinFlurry.MoveScale, WeaponActRules.MoveScale(WeaponActKind.Flurry, WeaponActPhase.Flurry, 0), Eps);
            Assert.AreEqual(ShieldRule.GuardMoveScale, WeaponActRules.GuardMoveScale, Eps);
            Assert.AreEqual(ShieldRule.ParryPushMoveScale, WeaponActRules.ParryPushMoveScale, Eps);
            // 단계마다 더 느려진다('엄청 느려짐').
            for (int level = 1; level <= 3; level++)
                Assert.Less(GreatswordCharge.MoveScale(level), GreatswordCharge.MoveScale(level - 1));
            Assert.Less(GreatswordCharge.MoveScale(0), WeaponActRules.FlurryMoveScale);
            Assert.Less(WeaponActRules.FlurryMoveScale, WeaponActRules.GuardMoveScale);
        }

        [Test]
        public void TurnRates()
        {
            Assert.AreEqual(300f, WeaponActRules.TurnRate(WeaponActKind.Guard));
            Assert.AreEqual(120f, WeaponActRules.TurnRate(WeaponActKind.Charge));
            Assert.AreEqual(90f, WeaponActRules.TurnRate(WeaponActKind.Flurry));
            Assert.AreEqual(0f, WeaponActRules.TurnRate(WeaponActKind.None), "행동이 없으면 제한 없음");
            Assert.AreEqual(ShieldRule.GuardTurnDegPerSec, WeaponActRules.TurnRate(WeaponActKind.Guard));
            Assert.AreEqual(GreatswordCharge.TurnDegPerSec, WeaponActRules.TurnRate(WeaponActKind.Charge));
            Assert.AreEqual(TwinFlurry.TurnRateDeg, WeaponActRules.TurnRate(WeaponActKind.Flurry));
            // 막기 뒤돌기 180° = 0.6초.
            Assert.AreEqual(0.6f, 180f / WeaponActRules.TurnRate(WeaponActKind.Guard), Eps);
        }

        /// <summary>버팀 룬(0-3의 11, 6-7): 대검 기 모으기·놓아 베기만. 방패·난사는 시험 손잡이(SuperArmorAllActs)로만.</summary>
        [Test]
        public void SuperArmorRuneOnlyForGreatswordChargeAndRelease()
        {
            // 룬 있음, 시험 손잡이 꺼짐.
            Assert.IsTrue(WeaponActRules.Armored(WeaponActKind.Charge, WeaponActPhase.Charging, true, false));
            Assert.IsTrue(WeaponActRules.Armored(WeaponActKind.Charge, WeaponActPhase.Release, true, false));
            Assert.IsFalse(WeaponActRules.Armored(WeaponActKind.Charge, WeaponActPhase.Flinch, true, false), "끊김 경직 중에는 늘 아님");
            Assert.IsFalse(WeaponActRules.Armored(WeaponActKind.Charge, WeaponActPhase.None, true, false));
            foreach (WeaponActPhase phase in System.Enum.GetValues(typeof(WeaponActPhase)))
            {
                Assert.IsFalse(WeaponActRules.Armored(WeaponActKind.Guard, phase, true, false), "방패 " + phase);
                Assert.IsFalse(WeaponActRules.Armored(WeaponActKind.Flurry, phase, true, false), "난사 " + phase);
                Assert.IsFalse(WeaponActRules.Armored(WeaponActKind.None, phase, true, true), "행동 없음 " + phase);
                // 룬이 없으면 시험 손잡이가 켜져 있어도 아니다.
                Assert.IsFalse(WeaponActRules.Armored(WeaponActKind.Charge, phase, false, false), "룬 없음 " + phase);
                Assert.IsFalse(WeaponActRules.Armored(WeaponActKind.Charge, phase, false, true), "룬 없음 + 시험 손잡이 " + phase);
            }

            // 시험 손잡이 SuperArmorAllActs: 세 행동 모두. 끊김 경직·막기 깨짐은 그래도 아니다.
            Assert.IsTrue(WeaponActRules.Armored(WeaponActKind.Guard, WeaponActPhase.Hold, true, true));
            Assert.IsTrue(WeaponActRules.Armored(WeaponActKind.Guard, WeaponActPhase.Raise, true, true));
            Assert.IsTrue(WeaponActRules.Armored(WeaponActKind.Flurry, WeaponActPhase.Flurry, true, true));
            Assert.IsTrue(WeaponActRules.Armored(WeaponActKind.Charge, WeaponActPhase.Charging, true, true));
            Assert.IsFalse(WeaponActRules.Armored(WeaponActKind.Guard, WeaponActPhase.Break, true, true));
            Assert.IsFalse(WeaponActRules.Armored(WeaponActKind.Flurry, WeaponActPhase.Flinch, true, true));
        }

        /// <summary>맞으면 끊김(5-5 표). interruptible = 무기 행동 중 끊길 수 있는 순간, armored = 버팀 룬, blocked = 방패로 앞에서 막음·튕김.</summary>
        [Test]
        public void InterruptTable()
        {
            const int MaxHp = 2400;
            // 굴쥐 물기·화살·멧돼지·오우거, 막기 중 옆·뒤·위·덫·몸 안 타: 끊김.
            Assert.IsTrue(WeaponActRules.Interrupts(true, false, false, 129, MaxHp, 0));
            Assert.IsTrue(WeaponActRules.Interrupts(true, false, false, 1, MaxHp, 0), "크기 문턱 없음");
            // 시험 무적(피해 0)도 맞음.
            Assert.IsTrue(WeaponActRules.Interrupts(true, false, false, 0, MaxHp, 0), "시험 무적 피해 0도 끊김");
            // 방패로 앞 반원에서 막음·튕김: 끊기지 않음.
            Assert.IsFalse(WeaponActRules.Interrupts(true, false, true, 500, MaxHp, 0));
            // 버팀 룬 + 대검 기 모으기·놓기: 피해는 받지만 안 끊김.
            Assert.IsFalse(WeaponActRules.Interrupts(true, true, false, 500, MaxHp, 0));
            // 기본 공격·회오리·검풍 중, 대검 놓아 베기 판정 뒤: 끊길 수 있는 순간이 아님.
            Assert.IsFalse(WeaponActRules.Interrupts(false, false, false, 500, MaxHp, 0));

            // 시험 손잡이 WeaponActInterruptMinPermille(최대 체력 대비 ‰): 30‰ = 72.
            Assert.IsFalse(WeaponActRules.Interrupts(true, false, false, 71, MaxHp, 30));
            Assert.IsTrue(WeaponActRules.Interrupts(true, false, false, 72, MaxHp, 30));
            Assert.IsFalse(WeaponActRules.Interrupts(true, false, false, 0, MaxHp, 30), "문턱이 있으면 피해 0은 안 끊김");
            Assert.IsTrue(WeaponActRules.Interrupts(true, false, false, 0, MaxHp, -5), "음수는 문턱 없음");
            // 막음·버팀은 문턱과 상관없이 안 끊김.
            Assert.IsFalse(WeaponActRules.Interrupts(true, false, true, 2400, MaxHp, 30));
            Assert.IsFalse(WeaponActRules.Interrupts(true, true, false, 2400, MaxHp, 30));
        }

        /// <summary>대검 기 모으기 끊김 표가 공통 끊김 규칙과 맞물린다(3-8): 모으기·판정 전은 끊길 수 있음, 판정 뒤는 보통 피격, 버팀 룬이면 아님.</summary>
        [Test]
        public void GreatswordInterruptibleFeedsCommonRule()
        {
            bool rune = WeaponActRules.Armored(WeaponActKind.Charge, WeaponActPhase.Charging, true, false);
            Assert.IsTrue(WeaponActRules.Interrupts(GreatswordCharge.BreaksOnHit(WeaponActPhase.Charging, 0, false), false, false, 100, 2400, 0));
            Assert.IsTrue(WeaponActRules.Interrupts(GreatswordCharge.BreaksOnHit(WeaponActPhase.Release, 0, false), false, false, 100, 2400, 0));
            Assert.IsFalse(WeaponActRules.Interrupts(GreatswordCharge.BreaksOnHit(WeaponActPhase.Release, 1, false), false, false, 100, 2400, 0));
            Assert.IsFalse(WeaponActRules.Interrupts(GreatswordCharge.BreaksOnHit(WeaponActPhase.Charging, 0, rune), rune, false, 100, 2400, 0));
        }

        [Test]
        public void FlinchSecondsAndCommonConstants()
        {
            Assert.AreEqual(0.35f, WeaponActCommon.FlinchFor(WeaponActKind.Guard), Eps);
            Assert.AreEqual(0.35f, WeaponActCommon.FlinchFor(WeaponActKind.Flurry), Eps);
            Assert.AreEqual(0.45f, WeaponActCommon.FlinchFor(WeaponActKind.Charge), Eps);
            Assert.AreEqual(0.35f, WeaponActCommon.FlinchSeconds, Eps);
            Assert.AreEqual(0.45f, WeaponActCommon.ChargeFlinchSeconds, Eps);
            Assert.AreEqual(0.1f, WeaponActCommon.FlinchKnockSeconds, Eps);
            Assert.AreEqual(0.15f, WeaponActCommon.InputBuffer, Eps);
            // 경직은 피격 무적 0.5초보다 짧아 경직이 끝난 뒤 구르기로 빠질 틈이 있다(0-3의 10): 막기·난사 0.15초, 대검 0.05초.
            const float HurtInvulnerable = 0.5f;
            Assert.AreEqual(0.15f, HurtInvulnerable - WeaponActCommon.FlinchSeconds, Eps);
            Assert.AreEqual(0.05f, HurtInvulnerable - WeaponActCommon.ChargeFlinchSeconds, Eps);
            Assert.Less(WeaponActCommon.FlinchKnockSeconds, WeaponActCommon.FlinchSeconds);

            Assert.AreEqual(2, WeaponActCommon.NoActHintTimes);
            Assert.AreEqual("rmb_none", WeaponActCommon.NoActHintKey);
            Assert.AreEqual("이 무기는 우클릭 행동이 없다 · 회오리는 E", WeaponActCommon.NoActHintText);
            Assert.AreEqual("끊김", WeaponActCommon.InterruptedWord);
            Assert.AreEqual("버팀", WeaponActCommon.SuperArmorWord);
            Assert.AreEqual("튕겨 냄!", WeaponActCommon.ParriedWord);
            Assert.AreEqual("우클릭", WeaponActCommon.ActKeyLabel);
            Assert.AreEqual("E", WeaponActCommon.WhirlKeyLabel);
        }

        /// <summary>
        /// 휘두르기 첫 판정 뒤 오른쪽 클릭으로 끊고 들어가기는 막기만(0-3의 19). 대검 ① 판정(0.352초) 뒤 끊고 1단계 놓기를 되풀이하면
        /// (115 + 150)% ÷ (0.352 + 0.40 + 0.62)초 = 초당 1.93으로 상한 1.67을 넘으므로 기 모으기는 ① 전체(0.88초)가 끝나야 시작한다: 초당 1.39로 콤보 1.452 아래.
        /// </summary>
        [Test]
        public void OnlyGuardCutsSwing()
        {
            Assert.IsTrue(WeaponActRules.CutsSwing(WeaponActKind.Guard));
            Assert.IsFalse(WeaponActRules.CutsSwing(WeaponActKind.Charge));
            Assert.IsFalse(WeaponActRules.CutsSwing(WeaponActKind.Flurry));
            Assert.IsFalse(WeaponActRules.CutsSwing(WeaponActKind.None));

            var first = WeaponPresets.Greatsword.combo[0];
            var level1 = GreatswordCharge.Release(1);
            double cut = (first.hitPercent + level1.hitPercent) / 100.0 / (first.duration * first.hitMoment + GreatswordCharge.L1 + level1.duration);
            double whole = (first.hitPercent + level1.hitPercent) / 100.0 / (first.duration + GreatswordCharge.L1 + level1.duration);
            Assert.Greater(cut, GreatswordCharge.CoefficientCap, "끊고 들어가면 상한을 넘는다(그래서 막음)");
            Assert.Less(whole, WeaponPresets.Greatsword.SingleTargetCoefficient, "끝까지 휘두른 뒤면 콤보보다 낮다");
        }

        /// <summary>끊김 경직 마지막 0.15초(입력 버퍼) 안에 누른 구르기·E·Q는 남겨 경직 뒤에 나간다(0-3의 21). 대검 경직 0.45초도 빠질 틈이 생긴다.</summary>
        [Test]
        public void FlinchKeepsInputsInLastBuffer()
        {
            Assert.IsFalse(WeaponActCommon.KeepsBufferedInputs(0.16f));
            Assert.IsFalse(WeaponActCommon.KeepsBufferedInputs(0.45f));
            Assert.IsTrue(WeaponActCommon.KeepsBufferedInputs(0.15f));
            Assert.IsTrue(WeaponActCommon.KeepsBufferedInputs(0.01f));
            Assert.IsTrue(WeaponActCommon.KeepsBufferedInputs(0f));
        }

        /// <summary>계약 차례: 옛 호출 기본값(Melee), 처음 결과(None), 단계·종류 None이 0번.</summary>
        [Test]
        public void EnumDefaults()
        {
            Assert.AreEqual(HitKind.Melee, default(HitKind));
            Assert.AreEqual(HitResult.None, default(HitResult));
            Assert.AreEqual(WeaponActKind.None, default(WeaponActKind));
            Assert.AreEqual(WeaponActPhase.None, default(WeaponActPhase));
            Assert.AreEqual(9, System.Enum.GetValues(typeof(HitKind)).Length);
            Assert.AreEqual(5, System.Enum.GetValues(typeof(HitResult)).Length);
        }
    }
}
