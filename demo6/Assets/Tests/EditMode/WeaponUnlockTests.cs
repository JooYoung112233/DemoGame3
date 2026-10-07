using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Loot;
using Demo6.Core.Random;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 층별 풀림(전투·보스·무기 다듬기 1차 2-6, GearBase.UnlockFloor): 옛 3종과 방어구·장신구는 1층, 새 무기 6종은 2층부터(시험판 최대 층이 2라 모두 2층).
    /// 드랍·궤짝(RollGear)은 ForPart(부위, 층)으로 거르고, ForPart(부위)는 층과 상관없이 전부를 돌려준다.
    /// </summary>
    public sealed class WeaponUnlockTests
    {
        static readonly HashSet<string> NewIds = new HashSet<string>
        {
            GearBaseTable.Maul, GearBaseTable.Spear, GearBaseTable.Scythe, GearBaseTable.Axe, GearBaseTable.Dagger, GearBaseTable.Flail,
        };

        [Test]
        public void UnlockFloorsMatchDoc()
        {
            Assert.AreEqual(2, GearBaseTable.NewWeaponUnlockFloor);
            foreach (var w in WeaponPresets.Legacy3) Assert.AreEqual(1, GearBaseTable.Get(w.id).UnlockFloor, w.id);
            foreach (var w in WeaponPresets.New6)
            {
                Assert.IsTrue(NewIds.Contains(w.id), w.id);
                Assert.AreEqual(2, GearBaseTable.Get(w.id).UnlockFloor, w.id);
            }
            foreach (var b in GearBaseTable.All)
                if (!b.IsWeapon) Assert.AreEqual(1, b.UnlockFloor, b.Id + " 방어구·장신구는 1층");
            Assert.AreEqual(1, new GearBase("x", GearPart.Ring, "x", ArmorWeight.None, 0, 0, 0, 0).UnlockFloor, "1 미만은 1");
            Assert.IsTrue(GearBaseTable.Get(GearBaseTable.Maul).UnlockedAt(2));
            Assert.IsFalse(GearBaseTable.Get(GearBaseTable.Maul).UnlockedAt(1));
        }

        [Test]
        public void ForPartByFloorFiltersWeapons()
        {
            var floorOne = GearBaseTable.ForPart(GearPart.Weapon, 1);
            Assert.AreEqual(3, floorOne.Count);
            for (int i = 0; i < 3; i++) Assert.AreEqual(WeaponPresets.Legacy3[i].id, floorOne[i].Id);
            for (int floor = 2; floor <= 10; floor++)
            {
                var kinds = GearBaseTable.ForPart(GearPart.Weapon, floor);
                Assert.AreEqual(9, kinds.Count, floor + "층");
                for (int i = 0; i < 9; i++) Assert.AreEqual(WeaponPresets.All[i].id, kinds[i].Id, floor + "층");
            }
            Assert.AreEqual(9, GearBaseTable.ForPart(GearPart.Weapon).Count, "층 없는 ForPart는 전부");
            foreach (var part in GearSlots.Parts)
            {
                Assert.AreEqual(GearBaseTable.ForPart(part).Count, GearBaseTable.ForPart(part, 10).Count, part.ToString());
                if (part != GearPart.Weapon) Assert.AreEqual(3, GearBaseTable.ForPart(part, 1).Count, part.ToString());
            }
        }

        /// <summary>1층 드랍·궤짝·정예·금고에서는 새 종류가 나오지 않는다.</summary>
        [Test]
        public void FloorOneNeverRollsNewWeapons()
        {
            var weaponOnly = new GearRollContext { OnlyParts = new[] { GearPart.Weapon } };
            var rng = new Pcg32Random(4242);
            for (int i = 0; i < 20000; i++)
            {
                var g = LootRules.RollGear(1, rng, Grade.Common, weaponOnly);
                Assert.IsFalse(NewIds.Contains(g.BaseId), g.ToString());
                var any = LootRules.RollGear(1, rng);
                Assert.IsFalse(NewIds.Contains(any.BaseId), any.ToString());
            }
            for (ulong seed = 0; seed < 500; seed++)
            {
                var chest = LootRules.RollChest(true, 1, LootRules.RareWeaponParam, new Pcg32Random(seed));
                Assert.IsFalse(NewIds.Contains(chest.Gear[0].BaseId), chest.Gear[0].ToString());
                foreach (var g in LootRules.RollKill(KillSource.Elite, 1, new Pcg32Random(seed)).Gear) Assert.IsFalse(NewIds.Contains(g.BaseId), g.ToString());
                foreach (var g in LootRules.RollSafe(1, new Pcg32Random(seed)).Gear) Assert.IsFalse(NewIds.Contains(g.BaseId), g.ToString());
            }
        }

        /// <summary>2층부터는 새 6종이 모두 나온다(무기 안 1/9씩, ± 1.5%p). 옛 무기 굴림(RollWeapon)은 어느 층이든 옛 3종만.</summary>
        [Test]
        public void FloorTwoRollsAllNineAndLegacyRollStaysThree()
        {
            const int N = 27000;
            var weaponOnly = new GearRollContext { OnlyParts = new[] { GearPart.Weapon } };
            var rng = new Pcg32Random(2002);
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < N; i++)
            {
                var g = LootRules.RollGear(2, rng, Grade.Common, weaponOnly);
                counts.TryGetValue(g.BaseId, out int c);
                counts[g.BaseId] = c + 1;
            }
            Assert.AreEqual(9, counts.Count);
            foreach (var w in WeaponPresets.All)
            {
                counts.TryGetValue(w.id, out int c);
                Assert.That(c / (double)N, Is.InRange(1 / 9.0 - 0.015, 1 / 9.0 + 0.015), w.id);
            }

            var legacy = new Pcg32Random(77);
            for (int floor = 1; floor <= 10; floor++)
                for (int i = 0; i < 500; i++)
                {
                    var w = LootRules.RollWeapon(floor, legacy);
                    Assert.IsFalse(NewIds.Contains(w.WeaponId), w.ToString());
                }
        }

        /// <summary>새 무기도 장비 체계를 그대로 탄다: 공격 100, 이름 '등급 + 종류', 무기 옵션 풀·연쇄 번개, 옛 무기 보기 다리.</summary>
        [Test]
        public void NewWeaponsUseTheGearSystem()
        {
            foreach (var w in WeaponPresets.New6)
            {
                var item = new GearItem(w.id, Grade.Legendary, 2, 1000, 0, null, LegendaryTable.ChainLightningId, 700);
                Assert.AreEqual(w.id, item.BaseId);
                Assert.IsTrue(item.IsWeapon);
                Assert.AreSame(w, item.WeaponRule);
                Assert.AreEqual(GearMath.WeaponAttack(2, Grade.Legendary, 1000), item.Attack);
                Assert.AreEqual("전설 " + w.displayName, item.DisplayName);
                Assert.IsTrue(item.IsLegendary, "연쇄 번개는 무기 부위 효과");
                Assert.AreEqual(w.id, item.ToWeapon().WeaponId);
                Assert.AreSame(w, item.ToWeapon().Rule);
                Assert.AreSame(w, WeaponItem.RuleOf(w.id));
                var swapped = GearItem.Starting(GearSlot.Weapon).WithBase(w.id);
                Assert.AreEqual(w.id, swapped.BaseId, "시험 키로 종류만 바꾸기");
            }
        }
    }
}
