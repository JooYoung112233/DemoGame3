using System.Collections.Generic;
using Demo6.Core.Loot;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 반지 2자리와 장착 규칙(장비 문서 8-6, 4-1, 2차 #28 바꿈): G는 빈 반지 자리부터(반지 1 → 반지 2), 둘 다 차면 값이 큰 자리, 같으면 반지 1.
    /// 반지 두 자리 사이 옮기기는 맞바꿈, 벗기는 반지·목걸이만, 같은 종류 둘의 고유 합산, 계승은 반지끼리(반지 ↔ 목걸이 거부).
    /// </summary>
    public sealed class RingRuleTests
    {
        static GearItem Ring(string id, int itemLevel = 1, Grade grade = Grade.Common) => new GearItem(id, grade, itemLevel, 1000);

        [Test]
        public void ChooseSlotFillsEmptyRingFirst()
        {
            var loadout = Loadout.Starting();
            var a = Ring(GearBaseTable.IronRing);
            var b = Ring(GearBaseTable.BloodRing);
            Assert.AreEqual(GearSlot.Ring1, loadout.ChooseSlot(a));
            Assert.IsTrue(loadout.TryEquip(GearSlot.Ring1, a, out var removed));
            Assert.IsNull(removed);
            Assert.AreEqual(GearSlot.Ring2, loadout.ChooseSlot(b, l => 999));
            Assert.IsTrue(loadout.TryEquip(GearSlot.Ring2, b, out removed));
            Assert.IsNull(removed);

            // 반지 1만 비어 있으면 반지 1.
            loadout.Unequip(GearSlot.Ring1);
            Assert.AreEqual(GearSlot.Ring1, loadout.ChooseSlot(Ring(GearBaseTable.FangRing)));
            // 이미 낀 반지는 지금 자리(G로 옮기지 않음).
            Assert.AreEqual(GearSlot.Ring2, loadout.ChooseSlot(b));
            // 반지가 아니면 그 부위 자리.
            Assert.AreEqual(GearSlot.Amulet, loadout.ChooseSlot(new GearItem(GearBaseTable.CharmAmulet, Grade.Common, 1, 1000)));
            Assert.AreEqual(GearSlot.Boots, loadout.ChooseSlot(new GearItem(GearBaseTable.PlateBoots, Grade.Common, 1, 1000)));
        }

        /// <summary>둘 다 차 있으면 바꿨을 때 값이 더 큰 자리(대개 약한 반지 자리), 같으면 반지 1, value가 없으면 반지 1.</summary>
        [Test]
        public void ChooseSlotPicksBetterResultAndTiesToRingOne()
        {
            var loadout = Loadout.Starting();
            var strong = Ring(GearBaseTable.IronRing, 9, Grade.Epic);
            var weak = Ring(GearBaseTable.IronRing, 1);
            loadout.TryEquip(GearSlot.Ring1, strong, out _);
            loadout.TryEquip(GearSlot.Ring2, weak, out _);
            var candidate = Ring(GearBaseTable.BloodRing, 5, Grade.Rare);

            double Value(Loadout l)
            {
                double sum = 0;
                foreach (var item in l.Items) sum += item.Attack;
                return sum;
            }

            Assert.AreEqual(GearSlot.Ring2, loadout.ChooseSlot(candidate, Value), "약한 반지 2를 바꾸는 쪽이 더 큼");
            Assert.AreEqual(GearSlot.Ring1, loadout.ChooseSlot(candidate, l => 1.0), "같으면 반지 1");
            Assert.AreEqual(GearSlot.Ring1, loadout.ChooseSlot(candidate), "값이 없으면 반지 1");

            // 반대로 반지 1이 약하면 반지 1.
            var swapped = Loadout.Starting();
            swapped.TryEquip(GearSlot.Ring1, weak, out _);
            swapped.TryEquip(GearSlot.Ring2, strong, out _);
            Assert.AreEqual(GearSlot.Ring1, swapped.ChooseSlot(candidate, Value));
            // ChooseSlot은 장착 상태를 바꾸지 않는다.
            Assert.AreSame(weak, swapped[GearSlot.Ring1]);
            Assert.AreSame(strong, swapped[GearSlot.Ring2]);
        }

        /// <summary>Shift+G: 반지 두 자리 사이로 옮기면 맞바꾼다(벗는 장비 없음).</summary>
        [Test]
        public void MovingBetweenRingSlotsSwaps()
        {
            var loadout = Loadout.Starting();
            var a = Ring(GearBaseTable.IronRing);
            var b = Ring(GearBaseTable.FangRing);
            loadout.TryEquip(GearSlot.Ring1, a, out _);
            loadout.TryEquip(GearSlot.Ring2, b, out _);
            Assert.AreEqual(GearSlot.Ring2, GearSlots.OtherRing(loadout.SlotOf(a).Value));
            Assert.IsTrue(loadout.TryEquip(GearSlots.OtherRing(GearSlot.Ring1), a, out var removed));
            Assert.IsNull(removed);
            Assert.AreSame(b, loadout[GearSlot.Ring1]);
            Assert.AreSame(a, loadout[GearSlot.Ring2]);

            // 한쪽만 끼고 옮기면 원래 자리가 빈다.
            var single = Loadout.Starting();
            single.TryEquip(GearSlot.Ring1, a, out _);
            Assert.IsTrue(single.TryEquip(GearSlot.Ring2, a, out removed));
            Assert.IsNull(removed);
            Assert.IsNull(single[GearSlot.Ring1]);
            Assert.AreSame(a, single[GearSlot.Ring2]);

            // 같은 자리에 다시 끼면 아무 일도 없다.
            Assert.IsTrue(single.TryEquip(GearSlot.Ring2, a, out removed));
            Assert.IsNull(removed);
            Assert.AreSame(a, single[GearSlot.Ring2]);
        }

        [Test]
        public void OtherRingAndSlotRules()
        {
            Assert.AreEqual(GearSlot.Ring2, GearSlots.OtherRing(GearSlot.Ring1));
            Assert.AreEqual(GearSlot.Ring1, GearSlots.OtherRing(GearSlot.Ring2));
            Assert.AreEqual(GearSlot.Amulet, GearSlots.OtherRing(GearSlot.Amulet));
            Assert.AreEqual(GearSlot.Weapon, GearSlots.OtherRing(GearSlot.Weapon));

            var ring = Ring(GearBaseTable.IronRing);
            var amulet = new GearItem(GearBaseTable.FangAmulet, Grade.Common, 1, 1000);
            Assert.IsTrue(Loadout.CanEquip(GearSlot.Ring1, ring));
            Assert.IsTrue(Loadout.CanEquip(GearSlot.Ring2, ring));
            Assert.IsFalse(Loadout.CanEquip(GearSlot.Amulet, ring));
            Assert.IsFalse(Loadout.CanEquip(GearSlot.Ring1, amulet));
            Assert.IsFalse(Loadout.CanEquip(GearSlot.Weapon, null));

            var loadout = Loadout.Starting();
            Assert.IsFalse(loadout.TryEquip(GearSlot.Amulet, ring, out var removed), "다른 부위 거부");
            Assert.IsNull(removed);
            Assert.IsNull(loadout[GearSlot.Amulet]);

            // 벗기는 반지 1·2·목걸이만. 무기·갑옷·투구·장갑·장화는 바꾸기만.
            foreach (var slot in GearSlots.All)
                Assert.AreEqual(slot == GearSlot.Ring1 || slot == GearSlot.Ring2 || slot == GearSlot.Amulet, GearSlots.CanUnequip(slot), slot.ToString());
            var weapon = loadout.Weapon;
            Assert.IsNull(loadout.Unequip(GearSlot.Weapon));
            Assert.AreSame(weapon, loadout.Weapon);
            Assert.IsNull(loadout.Unequip(GearSlot.Armor));
            Assert.IsNotNull(loadout[GearSlot.Armor]);
            loadout.TryEquip(GearSlot.Amulet, amulet, out _);
            Assert.AreSame(amulet, loadout.Unequip(GearSlot.Amulet));
            Assert.IsNull(loadout[GearSlot.Amulet]);
            CollectionAssert.AreEqual(new List<GearSlot> { GearSlot.Ring1, GearSlot.Ring2, GearSlot.Amulet }, loadout.EmptySlots());
        }

        /// <summary>같은 종류 반지 둘을 껴도 되고 고유는 합산한다(핏빛 둘 = 치명 +40‰). 옵션도 반지 1·2 합산.</summary>
        [Test]
        public void SameKindRingsStackIntrinsic()
        {
            var loadout = Loadout.Starting();
            var a = Ring(GearBaseTable.BloodRing);
            var b = Ring(GearBaseTable.BloodRing);
            Assert.IsTrue(loadout.TryEquip(GearSlot.Ring1, a, out _));
            Assert.IsTrue(loadout.TryEquip(GearSlot.Ring2, b, out _));
            int ringCrit = a.IntrinsicTotal(OptionKind.CritChance) + b.IntrinsicTotal(OptionKind.CritChance);
            Assert.AreEqual(40, ringCrit);
            Assert.AreEqual(20 + 40, loadout.IntrinsicTotal(OptionKind.CritChance), "장검 20 + 핏빛 20 × 2");

            var withOptions = Loadout.Starting();
            var o1 = new GearItem(GearBaseTable.FangRing, Grade.Uncommon, 1, 1000, 0, new[] { new GearOption(OptionKind.AttackFlat, 15, 2) });
            var o2 = new GearItem(GearBaseTable.FangRing, Grade.Uncommon, 1, 1000, 0, new[] { new GearOption(OptionKind.AttackFlat, 12, 1) });
            withOptions.TryEquip(GearSlot.Ring1, o1, out _);
            withOptions.TryEquip(GearSlot.Ring2, o2, out _);
            Assert.AreEqual(27, withOptions.OptionTotal(OptionKind.AttackFlat));
            Assert.AreEqual(300 + 100, withOptions.IntrinsicTotal(OptionKind.CritDamage), "송곳 150 × 2 + 장검 100");
        }

        /// <summary>계승(8-4·8-6): 같은 부위끼리만. 반지 ↔ 반지(자리 무관·종류 무관)는 되고 반지 ↔ 목걸이는 안 된다.</summary>
        [Test]
        public void InheritOnlyWithinSamePart()
        {
            var iron = Ring(GearBaseTable.IronRing, 5, Grade.Rare).WithEnhance(6);
            var blood = Ring(GearBaseTable.BloodRing, 7, Grade.Epic);
            var amulet = new GearItem(GearBaseTable.AmberAmulet, Grade.Epic, 7, 1000);
            Assert.IsTrue(GearMath.CanInherit(iron, blood));
            Assert.IsTrue(GearMath.CanInherit(blood, iron));
            Assert.IsFalse(GearMath.CanInherit(iron, amulet));
            Assert.IsFalse(GearMath.CanInherit(amulet, iron));
            Assert.IsFalse(GearMath.CanInherit(iron, iron), "같은 장비");
            Assert.IsFalse(GearMath.CanInherit(null, blood));
            Assert.IsFalse(GearMath.CanInherit(iron, null));

            var longsword = new GearItem(GearBaseTable.Longsword, Grade.Rare, 7, 1000, 8);
            var greatsword = new GearItem(GearBaseTable.Greatsword, Grade.Epic, 8, 1000);
            Assert.IsTrue(GearMath.CanInherit(longsword, greatsword), "무기 종류는 달라도 됨");
            Assert.IsFalse(GearMath.CanInherit(GearItem.Starting(GearSlot.Armor), GearItem.Starting(GearSlot.Helm)), "갑옷 ↔ 투구 거부");
            Assert.IsFalse(GearMath.CanInherit(GearItem.Starting(GearSlot.Gloves), GearItem.Starting(GearSlot.Boots)), "장갑 ↔ 장화 거부");
        }
    }
}
