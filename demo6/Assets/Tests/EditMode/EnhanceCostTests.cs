using Demo6.Core.Loot;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 강화 비용(장비 문서 8-4): 2차 시도 비용 × 부위 비용 배율(무기 1000·갑옷 500·투구 200·장갑·장화 150·반지 300·목걸이 400‰), 반올림, 최소 1석.
    /// 아래 6줄은 문서 표를 그대로 옮겼다.
    /// </summary>
    public sealed class EnhanceCostTests
    {
        static readonly int[] Weapon = { 2, 2, 3, 3, 4, 5, 6, 10, 12, 14, 18, 22, 46, 59, 72 };
        static readonly int[] Armor = { 1, 1, 2, 2, 2, 3, 3, 5, 6, 7, 9, 11, 23, 30, 36 };
        static readonly int[] Amulet = { 1, 1, 1, 1, 2, 2, 2, 4, 5, 6, 7, 9, 18, 24, 29 };
        static readonly int[] Ring = { 1, 1, 1, 1, 1, 2, 2, 3, 4, 4, 5, 7, 14, 18, 22 };
        static readonly int[] Helm = { 1, 1, 1, 1, 1, 1, 1, 2, 2, 3, 4, 4, 9, 12, 14 };
        static readonly int[] GlovesBoots = { 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 3, 3, 7, 9, 11 };

        static void AssertRow(GearPart part, int[] row)
        {
            Assert.AreEqual(GearMath.MaxEnhance, row.Length);
            for (int target = 1; target <= GearMath.MaxEnhance; target++)
                Assert.AreEqual(row[target - 1], GearMath.EnhanceCost(part, target), part + " +" + target);
        }

        [Test]
        public void CostTableMatchesDesignEightFour()
        {
            AssertRow(GearPart.Weapon, Weapon);
            AssertRow(GearPart.Armor, Armor);
            AssertRow(GearPart.Amulet, Amulet);
            AssertRow(GearPart.Ring, Ring);
            AssertRow(GearPart.Helm, Helm);
            AssertRow(GearPart.Gloves, GlovesBoots);
            AssertRow(GearPart.Boots, GlovesBoots);
        }

        [Test]
        public void PartCostMultipliersMatchDesign()
        {
            int[] expected = { 1000, 500, 200, 150, 150, 300, 400 };
            foreach (var part in GearSlots.Parts) Assert.AreEqual(expected[(int)part], GearSlots.EnhanceCostPermille(part), part.ToString());
            for (int target = 1; target <= GearMath.MaxEnhance; target++)
                Assert.AreEqual(GearMath.EnhanceBaseCostOf(target), GearMath.EnhanceCost(GearPart.Weapon, target), "무기 = 2차 비용 그대로");
        }

        /// <summary>최소 1석: 0.3석(장갑 +1)도 1석. 범위 밖 단계는 0.</summary>
        [Test]
        public void CostIsAtLeastOneStone()
        {
            foreach (var part in GearSlots.Parts)
            {
                for (int target = 1; target <= GearMath.MaxEnhance; target++) Assert.GreaterOrEqual(GearMath.EnhanceCost(part, target), 1, part + " +" + target);
                Assert.AreEqual(0, GearMath.EnhanceCost(part, 0));
                Assert.AreEqual(0, GearMath.EnhanceCost(part, GearMath.MaxEnhance + 1));
            }
            Assert.AreEqual(1, GearMath.ScaleCost(GearPart.Gloves, 1), "계승·시도 비용은 최소 1석");
            Assert.AreEqual(0, GearMath.ScaleCost(GearPart.Gloves, 1, 0), "환급은 최소 0");
            Assert.AreEqual(3, GearMath.ScaleCost(GearPart.Armor, 6, 0));
            Assert.AreEqual(0, GearMath.ScaleCost(GearPart.Weapon, 0));
        }

        /// <summary>강화 상한(등급)·누적 상승(2차 8-1)과 강화를 넣은 기본 능력치. 강화 0이면 M0b 공식과 같다.</summary>
        [Test]
        public void EnhanceCapsAndBaseStat()
        {
            int[] caps = { 5, 7, 10, 12, 15 };
            for (int g = 0; g < GradeRules.Count; g++) Assert.AreEqual(caps[g], GearMath.EnhanceCap((Grade)g));
            Assert.AreEqual(0, GearMath.EnhancePermille(0));
            Assert.AreEqual(690, GearMath.EnhancePermille(10));
            Assert.AreEqual(1490, GearMath.EnhancePermille(15));
            Assert.AreEqual(GearMath.WeaponAttack(7, Grade.Epic, 1040), GearMath.BaseStat(100, 7, Grade.Epic, 1040, 0));
            // 1,718 = 120 × 2.5 × 2.3 × 2.49 (판금 갑옷 전설 iLv11 +15).
            Assert.AreEqual(1718, GearMath.BaseStat(120, 11, Grade.Legendary, 1000, 15));
            Assert.AreEqual(5, new GearItem(GearBaseTable.LeatherHelm, Grade.Common, 1, 1000, 9).Enhance, "등급 상한으로 자름");
        }
    }
}
