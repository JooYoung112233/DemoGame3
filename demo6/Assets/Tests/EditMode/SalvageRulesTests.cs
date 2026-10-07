using Demo6.Core.Loot;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 재화 쓸 곳 1차 12-1 SalvageRulesTests(8~11): 등급 기본값(1·2·6·15·40), 강화 환급 = 2차 9-2 표 × 부위 비용 배율(반올림, 최소 0),
    /// 분해 강화석 예(일반 장검 +5 = 4, 희귀 갑옷 +7 = 9), 한 번 더 묻기(희귀 이상 또는 강화 +1 이상)와 일괄 분해 대상(일반 +0만).
    /// </summary>
    public sealed class SalvageRulesTests
    {
        static GearItem Gear(string id, Grade grade, int enhance = 0) => new GearItem(id, grade, 1, 1000, enhance);

        // ── 8. 등급 기본값 ──

        [Test]
        public void BaseStonesPerGrade()
        {
            Assert.AreEqual(1, SalvageRules.BaseStones(Grade.Common));
            Assert.AreEqual(2, SalvageRules.BaseStones(Grade.Uncommon));
            Assert.AreEqual(6, SalvageRules.BaseStones(Grade.Rare));
            Assert.AreEqual(15, SalvageRules.BaseStones(Grade.Epic));
            Assert.AreEqual(40, SalvageRules.BaseStones(Grade.Legendary));
            Assert.AreEqual(1, SalvageRules.Stones(Gear(GearBaseTable.Longsword, Grade.Common)), "강화 0은 기본값만");
            Assert.AreEqual(40, SalvageRules.Stones(Gear(GearBaseTable.AmberAmulet, Grade.Legendary)));
            Assert.AreEqual(0, SalvageRules.Stones(null));
        }

        // ── 9. 강화 환급 ──

        static void AssertRefundRow(GearPart part, int[] row)
        {
            for (int enhance = 1; enhance <= row.Length; enhance++)
                Assert.AreEqual(row[enhance - 1], GearMath.ScaleCost(part, SalvageRules.RefundBase(enhance), 0), part + " +" + enhance);
        }

        [Test]
        public void RefundIsSecondDraftTableTimesPartMultiplier()
        {
            int[] table = { 0, 1, 1, 2, 3, 4, 6, 10, 16, 25, 37, 56, 82, 121, 178 };
            for (int enhance = 1; enhance <= 15; enhance++)
                Assert.AreEqual(table[enhance - 1], SalvageRules.RefundBase(enhance), "2차 9-2 +" + enhance);
            Assert.AreEqual(0, SalvageRules.RefundBase(0));
            Assert.AreEqual(0, SalvageRules.RefundBase(-3));

            // 문서 4-1 표(+1~+7).
            AssertRefundRow(GearPart.Weapon, new[] { 0, 1, 1, 2, 3, 4, 6 });
            AssertRefundRow(GearPart.Armor, new[] { 0, 1, 1, 1, 2, 2, 3 });
            AssertRefundRow(GearPart.Amulet, new[] { 0, 0, 0, 1, 1, 2, 2 });
            AssertRefundRow(GearPart.Ring, new[] { 0, 0, 0, 1, 1, 1, 2 });
            AssertRefundRow(GearPart.Helm, new[] { 0, 0, 0, 0, 1, 1, 1 });
            AssertRefundRow(GearPart.Gloves, new[] { 0, 0, 0, 0, 0, 1, 1 });
            AssertRefundRow(GearPart.Boots, new[] { 0, 0, 0, 0, 0, 1, 1 });

            // 장비로 본 환급 = 분해 강화석 − 등급 기본값.
            Assert.AreEqual(3, SalvageRules.Stones(Gear(GearBaseTable.Longsword, Grade.Common, 5)) - 1, "무기 +5");
            Assert.AreEqual(2, SalvageRules.Stones(Gear(GearBaseTable.LeatherArmor, Grade.Common, 5)) - 1, "갑옷 +5");
            Assert.AreEqual(0, SalvageRules.Stones(Gear(GearBaseTable.LeatherGloves, Grade.Common, 5)) - 1, "장갑 +5");
            Assert.AreEqual(1, SalvageRules.Stones(Gear(GearBaseTable.LeatherGloves, Grade.Uncommon, 7)) - 2, "장갑 +7");
        }

        // ── 10. 분해 강화석 예 ──

        [Test]
        public void SalvageStoneExamples()
        {
            Assert.AreEqual(4, SalvageRules.Stones(Gear(GearBaseTable.Longsword, Grade.Common, 5)), "일반 장검 +5 = 1 + 3");
            Assert.AreEqual(9, SalvageRules.Stones(Gear(GearBaseTable.LeatherArmor, Grade.Rare, 7)), "희귀 갑옷 +7 = 6 + 3");
            Assert.AreEqual(6, SalvageRules.Stones(Gear(GearBaseTable.Greatsword, Grade.Rare)), "희귀 대검 +0");
            double refund = SalvageRules.Stones(Gear(GearBaseTable.Longsword, Grade.Rare, 7)) - SalvageRules.BaseStones(Grade.Rare);
            Assert.Less(refund, EnhanceRules.ExpectedStones(GearPart.Weapon, 0, 7), "강화한 뒤 분해로는 이득이 없다");
        }

        // ── 11. 한 번 더 묻기·일괄 대상 ──

        [Test]
        public void ConfirmAndBulkTargets()
        {
            Assert.IsFalse(SalvageRules.NeedsConfirm(Gear(GearBaseTable.Longsword, Grade.Common)));
            Assert.IsFalse(SalvageRules.NeedsConfirm(Gear(GearBaseTable.Longsword, Grade.Uncommon)));
            Assert.IsTrue(SalvageRules.NeedsConfirm(Gear(GearBaseTable.Longsword, Grade.Common, 1)), "강화 +1 이상");
            Assert.IsTrue(SalvageRules.NeedsConfirm(Gear(GearBaseTable.Longsword, Grade.Uncommon, 3)));
            Assert.IsTrue(SalvageRules.NeedsConfirm(Gear(GearBaseTable.Longsword, Grade.Rare)), "희귀 이상");
            Assert.IsTrue(SalvageRules.NeedsConfirm(Gear(GearBaseTable.Longsword, Grade.Epic)));
            Assert.IsTrue(SalvageRules.NeedsConfirm(Gear(GearBaseTable.Longsword, Grade.Legendary)), "전설은 늘 묻는다");
            Assert.IsFalse(SalvageRules.NeedsConfirm(null));

            Assert.IsTrue(SalvageRules.InBulkCommon(Gear(GearBaseTable.LeatherBoots, Grade.Common)));
            Assert.IsFalse(SalvageRules.InBulkCommon(Gear(GearBaseTable.LeatherBoots, Grade.Common, 1)), "강화한 일반은 빼기");
            Assert.IsFalse(SalvageRules.InBulkCommon(Gear(GearBaseTable.LeatherBoots, Grade.Uncommon)));
            Assert.IsFalse(SalvageRules.InBulkCommon(Gear(GearBaseTable.LeatherBoots, Grade.Rare)));
            Assert.IsFalse(SalvageRules.InBulkCommon(null));
        }
    }
}
