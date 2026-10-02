using Demo6.Core.Combat;
using Demo6.Core.Loot;
using Demo6.Core.Random;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 장비 규칙(2차 6-1 수치 공식, 6-2 등급표, 7-2 등급 확률)과 3차 초안 2-5·4-6 보상 규칙 검사.
    /// 기대값은 가능한 한 규칙표에서 읽어 계산한다(밸런스를 고쳐도 성질 테스트는 그대로).
    /// </summary>
    public sealed class GearRulesTests
    {
        [Test]
        public void GradeRowsSumToThousandOnEveryFloor()
        {
            Assert.AreEqual(10, GradeRules.TableFloors);
            for (int floor = 1; floor <= 10; floor++)
            {
                int sum = 0;
                foreach (int p in GradeRules.FloorRow(floor))
                {
                    Assert.GreaterOrEqual(p, 0, "층 " + floor);
                    sum += p;
                }
                Assert.AreEqual(1000, sum, "층 " + floor);
            }
        }

        [Test]
        public void LegendaryOnlyFromFloorFive()
        {
            for (int floor = 1; floor <= 4; floor++)
                Assert.AreEqual(0, GradeRules.FloorPermille(floor, Grade.Legendary), "층 " + floor);
            for (int floor = 5; floor <= 10; floor++)
                Assert.Greater(GradeRules.FloorPermille(floor, Grade.Legendary), 0, "층 " + floor);
        }

        [Test]
        public void FloorOneCommonWeaponAttackIsNinetyToHundredTen()
        {
            Assert.AreEqual(90, GearMath.WeaponAttack(1, Grade.Common, GearMath.RollMinPermille));
            Assert.AreEqual(110, GearMath.WeaponAttack(1, Grade.Common, GearMath.RollMaxPermille));
            for (int roll = GearMath.RollMinPermille; roll <= GearMath.RollMaxPermille; roll++)
                Assert.That(GearMath.WeaponAttack(1, Grade.Common, roll), Is.InRange(90, 110), "굴림 " + roll);
        }

        [Test]
        public void RolledWeaponsStayInsideRollRange()
        {
            var rng = new Pcg32Random(12345);
            for (int i = 0; i < 5000; i++)
            {
                var w = LootRules.RollWeapon(1, rng);
                Assert.That(w.RollPermille, Is.InRange(GearMath.RollMinPermille, GearMath.RollMaxPermille));
                Assert.AreEqual(1, w.ItemLevel);
                if (w.Grade == Grade.Common) Assert.That(w.Attack, Is.InRange(90, 110));
                Assert.IsTrue(w.WeaponId == WeaponItem.LongswordId || w.WeaponId == WeaponItem.GreatswordId || w.WeaponId == WeaponItem.TwinbladesId, w.WeaponId);
            }
        }

        /// <summary>2차 6-1 표: 일반 +0 100~235, 전설 +0 230~541(굴림 100%).</summary>
        [Test]
        public void AttackTableMatchesDesignSixOne()
        {
            int[] common = { 100, 115, 130, 145, 160, 175, 190, 205, 220, 235 };
            int[] legendary = { 230, 265, 299, 334, 368, 403, 437, 472, 506, 541 };
            for (int floor = 1; floor <= 10; floor++)
            {
                Assert.AreEqual(common[floor - 1], GearMath.WeaponAttack(floor, Grade.Common, 1000), "일반 층 " + floor);
                Assert.AreEqual(legendary[floor - 1], GearMath.WeaponAttack(floor, Grade.Legendary, 1000), "전설 층 " + floor);
            }
            Assert.AreEqual(250, GearMath.WeaponAttack(11, Grade.Common, 1000));
            Assert.AreEqual(575, GearMath.WeaponAttack(11, Grade.Legendary, 1000));
        }

        [Test]
        public void FloorTenLegendaryRollThousandIs541()
        {
            Assert.AreEqual(541, new WeaponItem(WeaponItem.GreatswordId, Grade.Legendary, 10, 1000).Attack);
            Assert.AreEqual(230, new WeaponItem(WeaponItem.GreatswordId, Grade.Legendary, 1, 1000).Attack);
        }

        [Test]
        public void StartingWeaponIsCommonLongswordHundred()
        {
            var w = WeaponItem.Starting();
            Assert.AreEqual(WeaponItem.LongswordId, w.WeaponId);
            Assert.AreEqual(Grade.Common, w.Grade);
            Assert.AreEqual(1, w.ItemLevel);
            Assert.AreEqual(1000, w.RollPermille);
            Assert.AreEqual(100, w.Attack);
            Assert.AreEqual(200, w.PlayerAttack);
            Assert.AreSame(WeaponPresets.Longsword, w.Rule);
            Assert.AreEqual("일반 장검", w.DisplayName);
        }

        [Test]
        public void DisplayNameUsesGradeAndWeaponName()
        {
            Assert.AreEqual("희귀 대검", new WeaponItem(WeaponItem.GreatswordId, Grade.Rare, 1, 1000).DisplayName);
            Assert.AreEqual("전설 쌍검", new WeaponItem(WeaponItem.TwinbladesId, Grade.Legendary, 1, 1000).DisplayName);
            Assert.AreEqual("고급", GradeRules.Name(Grade.Uncommon));
            Assert.AreEqual("영웅", GradeRules.Name(Grade.Epic));
        }

        [Test]
        public void RareWeaponChestIsAlwaysRareOrBetter()
        {
            for (int floor = 1; floor <= 10; floor++)
            for (ulong seed = 0; seed < 400; seed++)
            {
                var rng = new Pcg32Random(seed * 7919UL + (ulong)floor);
                var bundle = LootRules.RollChest(true, floor, LootRules.RareWeaponParam, rng);
                Assert.AreEqual(1, bundle.Gear.Count);
                Assert.GreaterOrEqual((int)bundle.Gear[0].Grade, (int)Grade.Rare, "층 " + floor + " 시드 " + seed);
                Assert.AreEqual(floor, bundle.Gear[0].ItemLevel);
            }
        }

        [Test]
        public void IronChestGivesOneGearStonesAndGold()
        {
            for (int floor = 1; floor <= 10; floor++)
            {
                var bundle = LootRules.RollChest(true, floor, "", new Pcg32Random((ulong)floor));
                Assert.AreEqual(1, bundle.Gear.Count);
                Assert.AreEqual(System.Math.Max(1, floor - 2), bundle.Stones);
                Assert.AreEqual(6 + 3 * floor, bundle.Gold);
                Assert.IsFalse(bundle.RatChest);
            }
        }

        [Test]
        public void WoodChestGoldIsFixedAndGearAtMostOne()
        {
            var rng = new Pcg32Random(99);
            int gear = 0;
            int rats = 0;
            const int N = 20000;
            for (int i = 0; i < N; i++)
            {
                var bundle = LootRules.RollChest(false, 1, "", rng);
                Assert.AreEqual(6, bundle.Gold);
                Assert.LessOrEqual(bundle.Gear.Count, 1);
                Assert.That(bundle.Stones, Is.InRange(0, 1));
                gear += bundle.Gear.Count;
                if (bundle.RatChest) rats++;
            }
            // 10% ± 5σ(σ = √(0.1 × 0.9 / N) ≈ 0.21%p).
            Assert.That(gear / (double)N, Is.InRange(0.0894, 0.1106));
            Assert.That(rats / (double)N, Is.InRange(0.0894, 0.1106));
        }

        [Test]
        public void EliteDropsTwoAndNeverCommon()
        {
            var rng = new Pcg32Random(7);
            for (int i = 0; i < 3000; i++)
            {
                var bundle = LootRules.RollKill(KillSource.Elite, 1, rng);
                Assert.AreEqual(2, bundle.Gear.Count);
                foreach (var w in bundle.Gear) Assert.AreNotEqual(Grade.Common, w.Grade);
                Assert.AreEqual(3, bundle.Stones);
                Assert.AreEqual(15, bundle.Gold);
            }
        }

        [Test]
        public void FloorMultiplierAndGoldPile()
        {
            Assert.AreEqual(1000, LootRules.FloorMultiplierPermille(1));
            Assert.AreEqual(2350, LootRules.FloorMultiplierPermille(10));
            Assert.AreEqual(3, LootRules.GoldPileValue(1));
            Assert.AreEqual(3, LootRules.GoldPileValue(2));
            Assert.AreEqual(4, LootRules.GoldPileValue(3));
            Assert.AreEqual(7, LootRules.GoldPileValue(10));
        }

        /// <summary>2차 11-11 #15: 1층 일반 82% ± 0.43%p, 10층 전설 3% ± 0.19%p (20만 회).</summary>
        [Test]
        public void GradeDistributionMatchesTable()
        {
            const int N = 200000;
            var rng = new Pcg32Random(2026);
            int common1 = 0;
            for (int i = 0; i < N; i++)
                if (GradeRules.Roll(1, rng) == Grade.Common) common1++;
            Assert.That(common1 / (double)N, Is.InRange(0.82 - 0.0043, 0.82 + 0.0043));
            int legendary10 = 0;
            for (int i = 0; i < N; i++)
                if (GradeRules.Roll(10, rng) == Grade.Legendary) legendary10++;
            Assert.That(legendary10 / (double)N, Is.InRange(0.03 - 0.0019, 0.03 + 0.0019));
        }

        [Test]
        public void StableHashIsDeterministic()
        {
            Assert.AreEqual(LootRules.StableHash("f1.H.iron"), LootRules.StableHash("f1.H.iron"));
            Assert.AreNotEqual(LootRules.StableHash("f1.H.iron"), LootRules.StableHash("f1.K.iron"));
            Assert.AreEqual(2166136261u, LootRules.StableHash(""));
        }
    }
}
