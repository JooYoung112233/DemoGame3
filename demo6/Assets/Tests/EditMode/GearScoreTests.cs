using Demo6.Core.Combat;
using Demo6.Core.Loot;
using Demo6.Core.Stats;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>판단 지수 A·S·M과 종합 변화율, 부위 문턱 ▲ = ▼(장비 문서 2-2·8-5, 2차 6-6).</summary>
    public sealed class GearScoreTests
    {
        static StatSheet Equip(GearSlot slot, GearItem item)
        {
            var l = Loadout.Starting();
            Assert.IsTrue(l.TryEquip(slot, item, out _));
            return StatCalc.Compute(l, 1, 0);
        }

        static GearItem Item(string id, Grade grade = Grade.Common, params GearOption[] options) => new GearItem(id, grade, 1, 1000, 0, options);

        [Test]
        public void StartingIndices()
        {
            var g = GearScore.Of(StatCalc.Starting());
            double coef = WeaponPresets.Longsword.SingleTargetCoefficient;
            // A = 200 × 1.042 × [0.65 × 1.472 ÷ 1.49 + 0.35 × 1 ÷ 1] × 1(시작 무기 한손검과 방패, 기획/세-무기-우클릭-소켓-1차.md 2-3).
            double a = 200 * (1 + 0.07 * (1.6 - 1)) * (0.65 * coef / 1.49 + 0.35);
            Assert.AreEqual(a, g.A, 1e-9);
            Assert.AreEqual(206.74, g.A, 0.01);
            // S = 2,400 × (1 + 120 ÷ 1000), M = 1 + 0.06.
            Assert.AreEqual(2688.0, g.S, 1e-9);
            Assert.AreEqual(1.06, g.M, 1e-12);
            Assert.AreEqual(GearScore.ComboCoefficientBase, 1.49);
        }

        [Test]
        public void IndicesFollowFormulaForRichSheet()
        {
            var l = Loadout.Starting();
            Assert.IsTrue(l.TryEquip(GearSlot.Weapon, Item(GearBaseTable.Twinblades, Grade.Rare,
                new GearOption(OptionKind.AttackSpeed, 60, 2), new GearOption(OptionKind.LifeSteal, 6, 3)), out _));
            Assert.IsTrue(l.TryEquip(GearSlot.Helm, Item(GearBaseTable.PlateHelm, Grade.Uncommon, new GearOption(OptionKind.CooldownReduction, 30, 2)), out _));
            Assert.IsTrue(l.TryEquip(GearSlot.Amulet, Item(GearBaseTable.AmberAmulet, Grade.Uncommon, new GearOption(OptionKind.SkillDamage, 40, 2)), out _));
            Assert.IsTrue(l.TryEquip(GearSlot.Boots, Item(GearBaseTable.ChainBoots, Grade.Uncommon, new GearOption(OptionKind.HpRegen, 10, 3)), out _));
            Assert.IsTrue(l.TryEquip(GearSlot.Armor, Item(GearBaseTable.ChainArmor, Grade.Uncommon, new GearOption(OptionKind.OnKillHeal, 15, 3)), out _));
            var s = StatCalc.Compute(l, 4, 1);
            var g = GearScore.Of(s);

            double coef = WeaponPresets.Twinblades.SingleTargetCoefficient;
            double crit = 1 + s.CritChancePermille / 1000.0 * (s.CritDamagePermille / 1000.0 - 1);
            double a = s.Attack * crit
                       * (0.65 * coef * (1 + s.AttackSpeedPermille / 1000.0) / 1.49 + 0.35 * (1 + s.SkillDamagePermille / 1000.0) / (1 - s.CooldownReductionPermille / 1000.0))
                       * (1 + 0.2 * s.BossDamagePermille / 1000.0);
            double sv = s.MaxHp * (1 + s.Defense / 1000.0) + s.LifeStealPermille / 1000.0 * a * 10 + s.HpRegen * 10 + s.OnKillHeal * 5;
            Assert.AreEqual(60, s.AttackSpeedPermille);
            Assert.AreEqual(6, s.LifeStealPermille);
            Assert.AreEqual(30, s.CooldownReductionPermille);
            Assert.AreEqual(140, s.SkillDamagePermille);
            Assert.AreEqual(50, s.BossDamagePermille);
            Assert.AreEqual(10, s.HpRegen);
            Assert.AreEqual(15, s.OnKillHeal);
            Assert.AreEqual(a, g.A, 1e-9);
            Assert.AreEqual(sv, g.S, 1e-9);
            Assert.AreEqual(1 + s.MoveSpeedPermille / 1000.0, g.M, 1e-12);
        }

        [Test]
        public void SameSheetIsZero()
        {
            var a = StatCalc.Starting();
            var b = StatCalc.Starting();
            Assert.AreEqual(0.0, GearScore.Composite(GearScore.Of(a), GearScore.Of(b)));
            Assert.AreEqual(0, GearScore.CompositePermille(a, b));
            Assert.AreEqual(0, GearScore.CompositePermille(a, a));
            foreach (var part in GearSlots.Parts)
                Assert.AreEqual(CompareMark.Same, GearScore.Mark(GearScore.CompositePermille(a, b), part), part.ToString());
        }

        [Test]
        public void CompositeWeightsAreSixThreeOne()
        {
            var one = new GearIndices(1, 1, 1);
            Assert.AreEqual(0.06, GearScore.Composite(one, new GearIndices(1.1, 1, 1)), 1e-12);
            Assert.AreEqual(0.03, GearScore.Composite(one, new GearIndices(1, 1.1, 1)), 1e-12);
            Assert.AreEqual(0.01, GearScore.Composite(one, new GearIndices(1, 1, 1.1)), 1e-12);
            Assert.AreEqual(-60, GearScore.CompositePermille(one, new GearIndices(0.9, 1, 1)));
            Assert.AreEqual(100, GearScore.CompositePermille(one, new GearIndices(1.1, 1.1, 1.1)));
        }

        [TestCase(GearPart.Weapon, 30)]
        [TestCase(GearPart.Armor, 20)]
        [TestCase(GearPart.Helm, 10)]
        [TestCase(GearPart.Gloves, 10)]
        [TestCase(GearPart.Boots, 10)]
        [TestCase(GearPart.Ring, 10)]
        [TestCase(GearPart.Amulet, 10)]
        public void MarkThresholdBoundaries(GearPart part, int threshold)
        {
            Assert.AreEqual(threshold, GearSlots.CompareThresholdPermille(part));
            Assert.AreEqual(CompareMark.Up, GearScore.Mark(threshold, part), "문턱 이상 ▲");
            Assert.AreEqual(CompareMark.Up, GearScore.Mark(threshold + 50, part));
            Assert.AreEqual(CompareMark.Same, GearScore.Mark(threshold - 1, part), "문턱 안 =");
            Assert.AreEqual(CompareMark.Same, GearScore.Mark(0, part));
            Assert.AreEqual(CompareMark.Same, GearScore.Mark(-threshold + 1, part));
            Assert.AreEqual(CompareMark.Down, GearScore.Mark(-threshold, part), "−문턱 이하 ▼");
            Assert.AreEqual(CompareMark.Down, GearScore.Mark(-threshold - 50, part));
        }

        [Test]
        public void IronRingInEmptySlotIsUp()
        {
            // 공격 200 → 225(+12.5%), 생존·이동 그대로: 종합 0.6 × 12.5% = 75‰.
            var before = StatCalc.Starting();
            var after = Equip(GearSlot.Ring1, Item(GearBaseTable.IronRing));
            int c = GearScore.CompositePermille(before, after);
            Assert.AreEqual(75, c);
            Assert.AreEqual(CompareMark.Up, GearScore.Mark(c, GearPart.Ring));
            Assert.AreEqual(CompareMark.Down, GearScore.Mark(GearScore.CompositePermille(after, before), GearPart.Ring));
        }

        [Test]
        public void ChainGlovesBeatLeatherGloves()
        {
            // 사슬 장갑: 고유 공속 +30‰, 방어 24·체력 90(가죽 18·60). 공격 +1.9%, 생존 +1.8% → 종합 약 +17‰ ▲.
            var before = StatCalc.Starting();
            var after = Equip(GearSlot.Gloves, Item(GearBaseTable.ChainGloves));
            Assert.AreEqual(30, after.AttackSpeedPermille);
            int c = GearScore.CompositePermille(before, after);
            Assert.That(c, Is.InRange(15, 19));
            Assert.AreEqual(CompareMark.Up, GearScore.Mark(c, GearPart.Gloves));
        }

        [Test]
        public void PlateBootsTradeMoveForDefenseIsSame()
        {
            // 가죽 장화 → 판금 장화: 방어 +18이지만 이동 −60‰(가죽 장화 +30 → 판금 −30). 종합이 문턱(10‰) 안이라 =.
            var before = StatCalc.Starting();
            var after = Equip(GearSlot.Boots, Item(GearBaseTable.PlateBoots));
            int c = GearScore.CompositePermille(before, after);
            Assert.That(c, Is.InRange(-9, 9));
            Assert.AreEqual(CompareMark.Same, GearScore.Mark(c, GearPart.Boots));
        }

        [Test]
        public void NullSheetIsNeutral()
        {
            var g = GearScore.Of(null);
            Assert.AreEqual(1.0, g.A);
            Assert.AreEqual(1.0, g.S);
            Assert.AreEqual(1.0, g.M);
        }
    }
}
