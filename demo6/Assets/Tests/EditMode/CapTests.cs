using System.Collections.Generic;
using Demo6.Core.Loot;
using Demo6.Core.Stats;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 2차 11-11 #39를 7칸으로 다시 함(장비 문서 7장): 모든 자리가 iLv11 전설 +15, 굴림 100%, 옵션 최대일 때의 합이 StatCaps 안에 든다.
    /// 각 칸의 종류 고유는 그 능력치에 가장 유리한 종류 하나, 옵션은 그 부위 풀에 있을 때만 위끝 × 1.5 × 옵션 세기 0.8(× 고정값이면 iLv11 2.5)로 센다.
    /// 7장 표(옵션 ×0.8 뒤, 12장 밸런스 결정): 치명 27.4%, 치명 피해 288%(대검), 공격 속도 +19.8%, 이동 +19.6%, 재사용 20%, 흡수 1.5%, 방어 3,705.
    /// </summary>
    public sealed class CapTests
    {
        const int MaxLevel = 11;

        /// <summary>자리 8개에 해당하는 부위(반지 둘).</summary>
        static readonly GearPart[] SlotParts =
        {
            GearPart.Weapon, GearPart.Armor, GearPart.Helm, GearPart.Gloves, GearPart.Boots, GearPart.Ring, GearPart.Ring, GearPart.Amulet,
        };

        /// <summary>그 부위에서 그 종류 옵션의 최대(전설 iLv11 위끝). 풀에 없으면 0.</summary>
        static int MaxOption(GearPart part, OptionKind kind) =>
            OptionTable.TryGetRule(part, kind, out var rule) ? OptionTable.MaxValue(rule, Grade.Legendary, MaxLevel) : 0;

        /// <summary>그 부위 종류 고유 가운데 가장 큰 값(가장 유리한 종류 하나).</summary>
        static int MaxIntrinsic(GearPart part, OptionKind kind)
        {
            int best = int.MinValue;
            foreach (var b in GearBaseTable.ForPart(part)) best = System.Math.Max(best, b.IntrinsicOf(kind));
            return best;
        }

        /// <summary>그 부위 종류 고유 가운데 가장 작은 값(아래 상한 점검).</summary>
        static int MinIntrinsic(GearPart part, OptionKind kind)
        {
            int worst = int.MaxValue;
            foreach (var b in GearBaseTable.ForPart(part)) worst = System.Math.Min(worst, b.IntrinsicOf(kind));
            return worst;
        }

        /// <summary>맨몸 + 자리 8개의 (가장 유리한 고유 + 옵션 최대) 합.</summary>
        static int MaxSum(OptionKind kind, int bare)
        {
            int sum = bare;
            foreach (var part in SlotParts) sum += MaxIntrinsic(part, kind) + MaxOption(part, kind);
            return sum;
        }

        [Test]
        public void CritChanceMaxStaysUnderCap()
        {
            int max = MaxSum(OptionKind.CritChance, StatBase.CritChancePermille);
            Assert.AreEqual(274, max, "5 + 쌍검 4 + 무기 4.8 + 장갑 4.8 + 반지 2.4×2 + 핏빛 2×2");
            Assert.LessOrEqual(max, StatCaps.CritChancePermille);
        }

        [Test]
        public void CritDamageMaxStaysUnderCapEvenWithGreatsword()
        {
            int max = MaxSum(OptionKind.CritDamage, StatBase.CritDamagePermille);
            Assert.AreEqual(2880, max, "150 + 대검 50 + 무기 24 + 목걸이 24 + 송곳 15×2 + 판금 장갑 10");
            Assert.LessOrEqual(max, StatCaps.CritDamagePermille);
            // 장검 248%, 쌍검 218%(무기 고유만 바꿈).
            int longsword = max - 500 + GearBaseTable.Get(GearBaseTable.Longsword).IntrinsicOf(OptionKind.CritDamage);
            int twin = max - 500 + GearBaseTable.Get(GearBaseTable.Twinblades).IntrinsicOf(OptionKind.CritDamage);
            Assert.AreEqual(2480, longsword);
            Assert.AreEqual(2180, twin);
            // 아래끝: 쌍검 −20%만 붙어도 맨몸 150% → 130%로, 치명 피해 아래 상한 100% 위다.
            int min = StatBase.CritDamagePermille;
            foreach (var part in SlotParts) min += System.Math.Min(0, MinIntrinsic(part, OptionKind.CritDamage));
            Assert.AreEqual(1300, min);
            Assert.GreaterOrEqual(min, StatCaps.CritDamageMinPermille);
        }

        [Test]
        public void AttackSpeedMaxStaysUnderCap()
        {
            int max = MaxSum(OptionKind.AttackSpeed, 0);
            Assert.AreEqual(198, max, "무기 9.6 + 장갑 7.2 + 사슬 장갑 3");
            Assert.LessOrEqual(max, StatCaps.AttackSpeedPermille);
        }

        [Test]
        public void MoveSpeedMaxStaysUnderCap()
        {
            int max = MaxSum(OptionKind.MoveSpeed, 0);
            Assert.AreEqual(196, max, "가죽 갑옷 3 + 가죽 장화 3 + 장화 옵션 9.6 + 부적 목걸이 4");
            Assert.LessOrEqual(max, StatCaps.MoveSpeedPermille);
            int min = 0;
            foreach (var part in SlotParts) min += System.Math.Min(0, MinIntrinsic(part, OptionKind.MoveSpeed));
            Assert.AreEqual(-60, min, "판금 한 벌이면 −6%");
            Assert.GreaterOrEqual(min, -StatCaps.MoveSpeedPermille);
        }

        [Test]
        public void CooldownReductionMaxStaysUnderCap()
        {
            int max = MaxSum(OptionKind.CooldownReduction, 0);
            Assert.AreEqual(200, max, "사슬 두건 3 + 투구 4.8 + 이빨 목걸이 5 + 목걸이 7.2");
            Assert.LessOrEqual(max, StatCaps.CooldownReductionPermille);
        }

        [Test]
        public void LifeStealMaxStaysUnderCap()
        {
            int max = MaxSum(OptionKind.LifeSteal, 0);
            Assert.AreEqual(15, max, "무기 0.96 → 1.0 + 장갑 0.48 → 0.5");
            Assert.LessOrEqual(max, StatCaps.LifeStealPermille);
        }

        /// <summary>판금 한 벌 iLv11 전설 +15 굴림 100%(3,435) + 방어+ 옵션(갑옷 72 + 장화 36) × 2.5 = 3,705 &lt; 4,000.</summary>
        [Test]
        public void DefenseMaxStaysUnderCap()
        {
            var loadout = new Loadout();
            var plate = new[] { GearBaseTable.PlateArmor, GearBaseTable.PlateHelm, GearBaseTable.PlateGloves, GearBaseTable.PlateBoots };
            foreach (var id in plate)
            {
                var b = GearBaseTable.Get(id);
                var options = new List<GearOption>();
                int def = MaxOption(b.Part, OptionKind.DefenseFlat);
                if (def > 0) options.Add(new GearOption(OptionKind.DefenseFlat, def, 4));
                var item = new GearItem(id, Grade.Legendary, MaxLevel, 1000, GearMath.MaxEnhance, options);
                Assert.AreEqual(GearMath.MaxEnhance, item.Enhance);
                Assert.IsTrue(loadout.TryEquip(GearSlots.FirstSlotOf(b.Part), item, out _));
            }
            int total = loadout.OptionTotal(OptionKind.DefenseFlat);
            foreach (var item in loadout.Items) total += item.Defense;
            Assert.AreEqual(180 + 90, loadout.OptionTotal(OptionKind.DefenseFlat), "갑옷 60 × 1.5 × 0.8 × 2.5, 장화 30 × 1.5 × 0.8 × 2.5");
            Assert.That(total, Is.InRange(3705 - 3, 3705 + 3));
            Assert.LessOrEqual(total, StatCaps.Defense);
            // 방어+ 옵션은 갑옷·장화에만 있다(5-3).
            foreach (var part in GearSlots.Parts)
                Assert.AreEqual(part == GearPart.Armor || part == GearPart.Boots, MaxOption(part, OptionKind.DefenseFlat) > 0, part.ToString());
        }

        /// <summary>5-2 부위 전용 옵션(상한 지키기). 이 표가 깨지면 위 합이 상한을 넘는다.</summary>
        [Test]
        public void CappedOptionsOnlyOnTheirParts()
        {
            AssertOnly(OptionKind.MoveSpeed, GearPart.Boots);
            AssertOnly(OptionKind.AttackSpeed, GearPart.Weapon, GearPart.Gloves);
            AssertOnly(OptionKind.CritChance, GearPart.Weapon, GearPart.Gloves, GearPart.Ring);
            AssertOnly(OptionKind.CritDamage, GearPart.Weapon, GearPart.Amulet);
            AssertOnly(OptionKind.CooldownReduction, GearPart.Helm, GearPart.Amulet);
            AssertOnly(OptionKind.HpPercent, GearPart.Armor);
            AssertOnly(OptionKind.LifeSteal, GearPart.Weapon, GearPart.Gloves);
        }

        static void AssertOnly(OptionKind kind, params GearPart[] parts)
        {
            foreach (var part in GearSlots.Parts)
            {
                bool expected = System.Array.IndexOf(parts, part) >= 0;
                Assert.AreEqual(expected, OptionTable.TryGetRule(part, kind, out _), kind + " @ " + part);
            }
        }
    }
}
