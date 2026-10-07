using System;
using Demo6.Core.Combat;
using Demo6.Core.Loot;
using Demo6.Core.Stats;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 무기 아홉의 성격(장비 문서 3-1·3-2, 전투·보스·무기 다듬기 1차 2-3·2-4): 카드에 보이는 숫자(콤보 데이터에서 읽기만 함), 무기 종류 고유 치명,
    /// 치명 포함 단일 대상 계수 띠(1.45~1.60)와 순서, 시작 공격 지수 증가, 새 무기 고유 규칙 깃발.
    /// </summary>
    public sealed class WeaponIdentityTests
    {
        static readonly string[] Ids =
        {
            GearBaseTable.Longsword, GearBaseTable.Greatsword, GearBaseTable.Twinblades,
            GearBaseTable.Maul, GearBaseTable.Spear, GearBaseTable.Scythe, GearBaseTable.Axe, GearBaseTable.Dagger, GearBaseTable.Flail,
        };

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
            // 세 무기 콤보(기획/세-무기-우클릭-소켓-1차.md 2-3·3-4·4-4): 한 바퀴 2.12 / 3.10 / 2.55초, 동작 3 / 3 / 4개.
            Assert.AreEqual(2.12, WeaponPresets.Longsword.CycleSeconds, 1e-5);
            Assert.AreEqual(3.10, WeaponPresets.Greatsword.CycleSeconds, 1e-5);
            Assert.AreEqual(2.55, WeaponPresets.Twinblades.CycleSeconds, 1e-5);
            Assert.AreEqual(3, WeaponPresets.Longsword.combo.Length);
            Assert.AreEqual(3, WeaponPresets.Greatsword.combo.Length);
            Assert.AreEqual(4, WeaponPresets.Twinblades.combo.Length);
            // 저장 id는 그대로, 표시 이름만 바뀜(0-3의 1). 대검·쌍검 이름은 그대로.
            Assert.AreEqual("wpn_longsword", WeaponPresets.Longsword.id);
            Assert.AreEqual("한손검과 방패", WeaponPresets.Longsword.displayName);
            Assert.AreEqual("대검", WeaponPresets.Greatsword.displayName);
            Assert.AreEqual("쌍검", WeaponPresets.Twinblades.displayName);
            // 2026-10-05: 한손검은 베기 3연타(찌르기 없음), 쌍검은 우측 → 좌측 → 엇베기 → 가위 가르기.
            CollectionAssert.AreEqual(new[] { "베기", "되베기", "마무리 베기" }, Array.ConvertAll(WeaponPresets.Longsword.combo, x => x.name));
            Assert.IsFalse(Array.Exists(WeaponPresets.Longsword.combo, x => x.shape == ComboShape.Line), "한손검에 찌르기(직선) 단계가 없다");
            CollectionAssert.AreEqual(new[] { "걷어 베기", "치켜 베기", "내려 쪼개기" }, Array.ConvertAll(WeaponPresets.Greatsword.combo, x => x.name));
            CollectionAssert.AreEqual(new[] { "우측 베기", "좌측 베기", "엇베기", "가위 가르기" }, Array.ConvertAll(WeaponPresets.Twinblades.combo, x => x.name));
        }

        [TestCase("wpn_longsword", 1.42, 104, 28, "보통", 4, 6)]
        [TestCase("wpn_greatsword", 0.97, 150, 31, "강함", 7, 12)]
        [TestCase("wpn_twinblades", 1.57, 45, 24, "약함", 3, 8)]
        [TestCase("wpn_maul", 0.88, 168, 36, "강함", 3, 8)]
        [TestCase("wpn_spear", 1.34, 113, 22, "약함", 3, 6)]
        [TestCase("wpn_scythe", 1.11, 132, 16, "약함", 5, 8)]
        [TestCase("wpn_axe", 1.20, 110, 27, "보통", 3, 2)]
        [TestCase("wpn_dagger", 1.50, 62, 18, "약함", 2, 1)]
        [TestCase("wpn_flail", 1.02, 143, 23, "약함", 4, 6)]
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
            // 쌍검: 48 × 2 + 50 × 2 + 52 × 2 + 35 × 3 → 405 ÷ 9타 = 45.
            Assert.AreEqual(45.0, WeaponPresets.Twinblades.AverageHitPercent, 1e-9);
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
        [TestCase("wpn_maul", 50, 1900, 1.045)]
        [TestCase("wpn_spear", 60, 1700, 1.042)]
        [TestCase("wpn_scythe", 80, 1500, 1.040)]
        [TestCase("wpn_axe", 70, 1700, 1.049)]
        [TestCase("wpn_dagger", 90, 1600, 1.054)]
        [TestCase("wpn_flail", 60, 1800, 1.048)]
        public void WeaponIntrinsicCrit(string id, int chance, int damage, double expected)
        {
            var s = Sheet(id);
            Assert.AreEqual(chance, s.CritChancePermille);
            Assert.AreEqual(damage, s.CritDamagePermille);
            Assert.AreEqual(expected, StatCalc.ExpectedCritMultiplier(s), 1e-9);
            Assert.AreEqual(id, s.WeaponId);
            Assert.AreSame(Rule(id), s.WeaponRule);
            // 무기 공격력은 아홉 다 같다(3-2·2-1: 공격력은 다르게 주지 않음).
            Assert.AreEqual(200, s.Attack);
            // 무기 고유를 끄면 맨몸 치명.
            var off = Sheet(id, new StatOverrides { WeaponIntrinsic = false });
            Assert.AreEqual(StatBase.CritChancePermille, off.CritChancePermille);
            Assert.AreEqual(StatBase.CritDamagePermille, off.CritDamagePermille);
            Assert.AreEqual(1.025, StatCalc.ExpectedCritMultiplier(off), 1e-9);
        }

        [TestCase("wpn_longsword", 1.50)]
        [TestCase("wpn_greatsword", 1.49)]
        [TestCase("wpn_twinblades", 1.59)]
        [TestCase("wpn_maul", 1.51)]
        [TestCase("wpn_spear", 1.54)]
        [TestCase("wpn_scythe", 1.48)]
        [TestCase("wpn_axe", 1.56)]
        [TestCase("wpn_dagger", 1.58)]
        [TestCase("wpn_flail", 1.49)]
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

        [TestCase("wpn_longsword", 1.75)]
        [TestCase("wpn_greatsword", 1.20)]
        [TestCase("wpn_twinblades", 1.95)]
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

        /// <summary>All = 숫자키 1~9 차례(옛 3종 그대로 앞, 새 6종 뒤) = 장비 표 무기 줄 차례.</summary>
        [Test]
        public void AllIsLegacyThreeThenNewSixInNumberKeyOrder()
        {
            Assert.AreEqual(9, WeaponPresets.All.Length);
            CollectionAssert.AreEqual(WeaponPresets.Legacy3, new[] { WeaponPresets.All[0], WeaponPresets.All[1], WeaponPresets.All[2] });
            for (int i = 0; i < WeaponPresets.New6.Length; i++) Assert.AreSame(WeaponPresets.New6[i], WeaponPresets.All[3 + i]);
            for (int i = 0; i < Ids.Length; i++) Assert.AreEqual(Ids[i], WeaponPresets.All[i].id, "숫자키 " + (i + 1));
            var bases = GearBaseTable.ForPart(GearPart.Weapon);
            Assert.AreEqual(9, bases.Count);
            for (int i = 0; i < bases.Count; i++) Assert.AreEqual(WeaponPresets.All[i].id, bases[i].Id);
            CollectionAssert.AreEqual(new[] { "쇠망치", "창", "큰 낫", "도끼", "단검", "사슬 철퇴" }, System.Array.ConvertAll(WeaponPresets.New6, w => w.displayName));
        }

        /// <summary>
        /// 새 무기 고유 규칙 깃발(2-3): 쇠망치 ③ 벽 박기, 창 ①② 끊기, 큰 낫 모두 끌어당김, 도끼 ③ 출혈 50%, 단검 모두 등 찌르기, 사슬 철퇴 ①② 관성.
        /// 옛 3종은 깃발이 모두 꺼져 있다(M0a 데이터 그대로). 새 무기는 카드 설명 한 줄과 고유 규칙 한 줄이 있다.
        /// </summary>
        [Test]
        public void NewWeaponTraitFlagsMatchDoc()
        {
            foreach (var w in WeaponPresets.Legacy3)
            {
                Assert.IsNull(w.flavor, w.displayName);
                Assert.IsNull(w.traitLine, w.displayName);
                foreach (var s in w.combo)
                {
                    Assert.IsFalse(s.staggers || s.wallBreak || s.pull || s.backstab || s.inertia, w.displayName + " " + s.name);
                    Assert.AreEqual(0f, s.bleedPercent, w.displayName + " " + s.name);
                }
            }
            foreach (var w in WeaponPresets.New6)
            {
                Assert.IsFalse(string.IsNullOrEmpty(w.flavor), w.displayName);
                Assert.IsFalse(string.IsNullOrEmpty(w.traitLine), w.displayName);
            }
            CollectionAssert.AreEqual(new[] { false, false, true }, System.Array.ConvertAll(WeaponPresets.Maul.combo, s => s.wallBreak), "쇠망치 벽 박기");
            CollectionAssert.AreEqual(new[] { true, true, false }, System.Array.ConvertAll(WeaponPresets.Spear.combo, s => s.staggers), "창 끊기");
            CollectionAssert.AreEqual(new[] { true, true, true }, System.Array.ConvertAll(WeaponPresets.Scythe.combo, s => s.pull), "큰 낫 끌어당김");
            CollectionAssert.AreEqual(new[] { 0f, 0f, 50f }, System.Array.ConvertAll(WeaponPresets.Axe.combo, s => s.bleedPercent), "도끼 출혈");
            CollectionAssert.AreEqual(new[] { true, true, true }, System.Array.ConvertAll(WeaponPresets.Dagger.combo, s => s.backstab), "단검 등 찌르기");
            CollectionAssert.AreEqual(new[] { true, true, false }, System.Array.ConvertAll(WeaponPresets.Flail.combo, s => s.inertia), "사슬 철퇴 관성");
            // 깃발은 그 무기에만 있다.
            foreach (var w in WeaponPresets.All)
                foreach (var s in w.combo)
                {
                    if (s.wallBreak) Assert.AreSame(WeaponPresets.Maul, w);
                    if (s.staggers) Assert.AreSame(WeaponPresets.Spear, w);
                    if (s.pull) Assert.AreSame(WeaponPresets.Scythe, w);
                    if (s.bleedPercent > 0f) Assert.AreSame(WeaponPresets.Axe, w);
                    if (s.backstab) Assert.AreSame(WeaponPresets.Dagger, w);
                    if (s.inertia) Assert.AreSame(WeaponPresets.Flail, w);
                }
        }

        /// <summary>창 끊기 같은 적 1.5초, 무거운 적 움찔 0.15초. 큰 낫 끌어당김 = min(넉백 × (1 − 저항), 몸 사이 거리 − 0.15), 0 아래로 안 감.</summary>
        [Test]
        public void TraitConstantsAndPullDistance()
        {
            Assert.AreEqual(1.5f, WeaponTraitRules.StaggerCooldown);
            Assert.AreEqual(0.15f, WeaponTraitRules.HeavyFlinchSeconds);
            Assert.AreEqual(0.8f, WeaponTraitRules.PullDistance(0.8f, 0f, 3f), 1e-6f, "멀면 넉백만큼");
            Assert.AreEqual(0.4f, WeaponTraitRules.PullDistance(0.8f, 0.5f, 3f), 1e-6f, "저항 50%");
            Assert.AreEqual(0.35f, WeaponTraitRules.PullDistance(1.4f, 0f, 0.5f), 1e-6f, "몸에 겹치지 않게 틈 0.15");
            Assert.AreEqual(0f, WeaponTraitRules.PullDistance(1.4f, 0f, 0.1f), "이미 붙어 있으면 0");
            Assert.AreEqual(0f, WeaponTraitRules.PullDistance(1.4f, 1f, 3f), "넉백 저항 1(보스·돌진 중 멧돼지)은 끌리지 않음");
        }
    }
}
