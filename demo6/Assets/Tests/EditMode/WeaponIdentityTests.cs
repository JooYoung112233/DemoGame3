using System;
using Demo6.Core.Combat;
using Demo6.Core.Loot;
using Demo6.Core.Stats;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 무기 셋의 성격(장비 문서 3-1·3-2): 카드에 보이는 숫자(콤보 데이터에서 읽기만 함), 무기 종류 고유 치명,
    /// 치명 포함 단일 대상 계수 띠(1.45~1.60)와 순서, 시작 공격 지수 증가.
    /// </summary>
    public sealed class WeaponIdentityTests
    {
        static readonly string[] Ids = { GearBaseTable.Longsword, GearBaseTable.Greatsword, GearBaseTable.Twinblades };

        /// <summary>시작 장비에서 무기 종류만 바꾼 시트(일반 iLv1 굴림 1000‰).</summary>
        static StatSheet Sheet(string weaponId, StatOverrides overrides = null)
        {
            var l = Loadout.Starting();
            var weapon = l.Weapon.WithBase(weaponId);
            Assert.AreEqual(weaponId, weapon.BaseId);
            Assert.IsTrue(l.TryEquip(GearSlot.Weapon, weapon, out _));
            return StatCalc.Compute(l, 1, 0, overrides);
        }

        static WeaponAttackRule Rule(string id) => WeaponItem.RuleOf(id);

        [Test]
        public void ComboDataIsUntouched()
        {
            // M0a 승인 손맛: 한 바퀴 2.15 / 3.10 / 2.60초, 동작 3 / 3 / 4개.
            Assert.AreEqual(2.15, WeaponPresets.Longsword.CycleSeconds, 1e-5);
            Assert.AreEqual(3.10, WeaponPresets.Greatsword.CycleSeconds, 1e-5);
            Assert.AreEqual(2.60, WeaponPresets.Twinblades.CycleSeconds, 1e-5);
            Assert.AreEqual(3, WeaponPresets.Longsword.combo.Length);
            Assert.AreEqual(3, WeaponPresets.Greatsword.combo.Length);
            Assert.AreEqual(4, WeaponPresets.Twinblades.combo.Length);
        }

        [TestCase("wpn_longsword", 1.40, 107, 26, "보통", 5, 8)]
        [TestCase("wpn_greatsword", 0.97, 150, 31, "강함", 7, 12)]
        [TestCase("wpn_twinblades", 1.54, 46, 23, "약함", 3, 8)]
        public void CardNumbersMatchDoc(string id, double swings, int averageHit, int poise, string poiseWord, int maxTargets, int finisherMaxTargets)
        {
            var rule = Rule(id);
            Assert.AreEqual(swings, Math.Round(rule.SwingsPerSecond, 2), 1e-9, "초당 휘두르기");
            Assert.AreEqual(averageHit, (int)Math.Round(rule.AverageHitPercent), "한 방 세기");
            Assert.AreEqual(poise, (int)Math.Round(rule.PoisePerSecond), "무너뜨리기");
            Assert.AreEqual(poiseWord, rule.PoiseWord);
            Assert.AreEqual(maxTargets, rule.MaxTargets, "한 번에 최대");
            Assert.AreEqual(finisherMaxTargets, rule.FinisherMaxTargets, "마무리 최대");
            // 초당 휘두르기 = 동작 수 ÷ 한 바퀴. StatCalc 표시 함수와 같은 값.
            Assert.AreEqual(rule.combo.Length / rule.CycleSeconds, rule.SwingsPerSecond, 1e-12);
            Assert.AreEqual(rule.SwingsPerSecond, StatCalc.SwingsPerSecond(rule, 0), 1e-12);
        }

        [Test]
        public void TwinbladeAverageHitIsPerHit()
        {
            // 쌍검: 52 × 2를 세 번, 34 × 3 → 414 ÷ 9타 = 46.
            Assert.AreEqual(46.0, WeaponPresets.Twinblades.AverageHitPercent, 1e-9);
            Assert.AreEqual(450.0 / 3, WeaponPresets.Greatsword.AverageHitPercent, 1e-9);
        }

        [TestCase(24.9f, "약함")]
        [TestCase(25f, "보통")]
        [TestCase(29.9f, "보통")]
        [TestCase(30f, "강함")]
        public void PoiseWordThresholds(float poisePerSecond, string word)
        {
            var rule = new WeaponAttackRule { id = "test", combo = new[] { new ComboStep { duration = 1f, poiseDamage = poisePerSecond } } };
            Assert.AreEqual(word, rule.PoiseWord);
        }

        [TestCase("wpn_longsword", 70, 1600, 1.042)]
        [TestCase("wpn_greatsword", 50, 2000, 1.050)]
        [TestCase("wpn_twinblades", 90, 1300, 1.027)]
        public void WeaponIntrinsicCrit(string id, int chance, int damage, double expected)
        {
            var s = Sheet(id);
            Assert.AreEqual(chance, s.CritChancePermille);
            Assert.AreEqual(damage, s.CritDamagePermille);
            Assert.AreEqual(expected, StatCalc.ExpectedCritMultiplier(s), 1e-9);
            Assert.AreEqual(id, s.WeaponId);
            Assert.AreSame(Rule(id), s.WeaponRule);
            // 무기 공격력은 셋 다 같다(3-2: 공격력은 다르게 주지 않음).
            Assert.AreEqual(200, s.Attack);
            // 무기 고유를 끄면 맨몸 치명.
            var off = Sheet(id, new StatOverrides { WeaponIntrinsic = false });
            Assert.AreEqual(StatBase.CritChancePermille, off.CritChancePermille);
            Assert.AreEqual(StatBase.CritDamagePermille, off.CritDamagePermille);
            Assert.AreEqual(1.025, StatCalc.ExpectedCritMultiplier(off), 1e-9);
        }

        [TestCase("wpn_longsword", 1.51)]
        [TestCase("wpn_greatsword", 1.49)]
        [TestCase("wpn_twinblades", 1.60)]
        public void CritAdjustedCoefficientMatchesDoc(string id, double expected)
        {
            double value = CritAdjusted(id);
            Assert.AreEqual(expected, value, 0.01);
            Assert.That(value, Is.InRange(1.45, 1.60), "단일 대상 띠");
        }

        [Test]
        public void CritAdjustedOrderIsTwinLongGreat()
        {
            double longsword = CritAdjusted(GearBaseTable.Longsword);
            double greatsword = CritAdjusted(GearBaseTable.Greatsword);
            double twin = CritAdjusted(GearBaseTable.Twinblades);
            Assert.That(twin, Is.GreaterThan(longsword));
            Assert.That(longsword, Is.GreaterThan(greatsword));
        }

        /// <summary>치명 포함 계수 = 단일 대상 계수 × 기대 치명 배율 ÷ 맨몸 1.025(3-1 표 마지막 줄).</summary>
        static double CritAdjusted(string id) =>
            Rule(id).SingleTargetCoefficient * StatCalc.ExpectedCritMultiplier(Sheet(id)) / StatCalc.ExpectedCritMultiplier(StatBase.CritChancePermille, StatBase.CritDamagePermille);

        [TestCase("wpn_longsword", 0.017)]
        [TestCase("wpn_greatsword", 0.024)]
        [TestCase("wpn_twinblades", 0.002)]
        public void StartingAttackIndexGainFromWeaponCrit(string id, double gain)
        {
            double with = GearScore.Of(Sheet(id)).A;
            double without = GearScore.Of(Sheet(id, new StatOverrides { WeaponIntrinsic = false })).A;
            Assert.AreEqual(gain, with / without - 1, 0.003);
        }

        [TestCase("wpn_longsword", 1.73)]
        [TestCase("wpn_greatsword", 1.20)]
        [TestCase("wpn_twinblades", 1.91)]
        public void SwingsPerSecondAtMaxGearSpeed(string id, double swings)
        {
            // 7장: 공격 속도 7칸 최대 +24%.
            Assert.AreEqual(swings, Math.Round(StatCalc.SwingsPerSecond(Rule(id), 240), 2), 1e-9);
        }

        [Test]
        public void EveryWeaponBaseHasARule()
        {
            foreach (var id in Ids)
            {
                var b = GearBaseTable.Get(id);
                Assert.IsNotNull(b, id);
                Assert.AreSame(Rule(id), b.WeaponRule, id);
                Assert.AreEqual(Rule(id).displayName, b.Name, id);
            }
        }
    }
}
