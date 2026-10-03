using System.Collections.Generic;
using Demo6.Core.Loot;
using Demo6.Core.Random;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 장비 굴림 LootRules.RollGear(장비 문서 8-1 순서·8-2 부위 보정·빈 자리 채우기, 4-3 종류 1/3, 5-1 등급별 줄 수).
    /// 비율 시험은 5σ 안팎의 여유를 둔다.
    /// </summary>
    public sealed class GearRollTests
    {
        /// <summary>8-1 묶음 340/330/330 = 묶음 안 부위 합(방어구 105·75·75·75, 장신구 180·150). 4-1 몫 500/200/150/150.</summary>
        [Test]
        public void GroupSharesAreSumsOfPartShares()
        {
            int total = 0;
            foreach (GearGroup group in System.Enum.GetValues(typeof(GearGroup)))
            {
                int sum = 0;
                foreach (var part in GearSlots.PartsOf(group))
                {
                    Assert.AreEqual(group, GearSlots.GroupOf(part));
                    sum += GearSlots.PartDropPermille(part);
                }
                Assert.AreEqual(GearSlots.GroupDropPermille(group), sum, group.ToString());
                total += sum;
            }
            Assert.AreEqual(1000, total);
            CollectionAssert.AreEqual(new[] { 340, 330, 330 },
                new[] { GearSlots.GroupDropPermille(GearGroup.Weapon), GearSlots.GroupDropPermille(GearGroup.Armor), GearSlots.GroupDropPermille(GearGroup.Accessory) });
            CollectionAssert.AreEqual(new[] { GearPart.Ring, GearPart.Amulet }, GearSlots.PartsOf(GearGroup.Accessory));
            Assert.AreEqual(500, GearSlots.BaseSharePermille(GearPart.Armor));
            Assert.AreEqual(200, GearSlots.BaseSharePermille(GearPart.Helm));
            Assert.AreEqual(150, GearSlots.BaseSharePermille(GearPart.Gloves));
            Assert.AreEqual(150, GearSlots.BaseSharePermille(GearPart.Boots));
            Assert.AreEqual(30, GearSlots.CompareThresholdPermille(GearPart.Weapon));
            Assert.AreEqual(20, GearSlots.CompareThresholdPermille(GearPart.Armor));
            Assert.AreEqual(10, GearSlots.CompareThresholdPermille(GearPart.Ring));
        }

        /// <summary>
        /// 4-3 표 21종: 부위마다 3종, 시작 장비 id 5개(장검 + 가죽 한 벌)의 합 = 시작 수치(방어 120, 체력 400, 이동 +60‰, 공격 100).
        /// </summary>
        [Test]
        public void BaseTableMatchesDesignFourThree()
        {
            Assert.AreEqual(21, GearBaseTable.All.Count);
            var ids = new HashSet<string>();
            foreach (var b in GearBaseTable.All) Assert.IsTrue(ids.Add(b.Id), b.Id);
            foreach (var part in GearSlots.Parts) Assert.AreEqual(3, GearBaseTable.ForPart(part).Count, part.ToString());
            CollectionAssert.AreEqual(
                new[] { GearBaseTable.Longsword, GearBaseTable.LeatherArmor, GearBaseTable.LeatherHelm, GearBaseTable.LeatherGloves, GearBaseTable.LeatherBoots },
                GearBaseTable.StartingIds);
            var start = Loadout.Starting();
            int def = 0, hp = 0, atk = 0;
            foreach (var item in start.Items)
            {
                def += item.Defense;
                hp += item.Hp;
                atk += item.Attack;
            }
            Assert.AreEqual(120, def);
            Assert.AreEqual(400, hp);
            Assert.AreEqual(100, atk);
            Assert.AreEqual(60, start.IntrinsicTotal(OptionKind.MoveSpeed));
            Assert.AreEqual(20, start.IntrinsicTotal(OptionKind.CritChance));
            Assert.AreEqual(100, start.IntrinsicTotal(OptionKind.CritDamage));
            // 한 벌 합 = 2차 갑옷 하나: 사슬 160·600, 판금 240·400·−60‰.
            AssertSet(new[] { GearBaseTable.ChainArmor, GearBaseTable.ChainHelm, GearBaseTable.ChainGloves, GearBaseTable.ChainBoots }, 160, 600, 0);
            AssertSet(new[] { GearBaseTable.PlateArmor, GearBaseTable.PlateHelm, GearBaseTable.PlateGloves, GearBaseTable.PlateBoots }, 240, 400, -60);
        }

        /// <summary>시험 키 1·2·3(8-6 아래): 종류만 바꾸고 등급·iLv·굴림·강화·옵션·전설은 그대로. 다른 부위 종류로는 바꾸지 않는다.</summary>
        [Test]
        public void WithBaseKeepsOptionsAndLegendary()
        {
            var options = new[] { new GearOption(OptionKind.CritChance, 55, 3), new GearOption(OptionKind.AttackSpeed, 101, 2) };
            var sword = new GearItem(GearBaseTable.Longsword, Grade.Legendary, 9, 1070, 4, options, LegendaryTable.ChainLightningId, 640);
            var great = sword.WithBase(GearBaseTable.Greatsword);
            Assert.AreEqual(GearBaseTable.Greatsword, great.BaseId);
            Assert.AreEqual(sword.Grade, great.Grade);
            Assert.AreEqual(sword.ItemLevel, great.ItemLevel);
            Assert.AreEqual(sword.RollPermille, great.RollPermille);
            Assert.AreEqual(sword.Enhance, great.Enhance);
            CollectionAssert.AreEqual(sword.Options, great.Options);
            Assert.AreEqual(sword.LegendaryId, great.LegendaryId);
            Assert.AreEqual(sword.LegendaryRollPermille, great.LegendaryRollPermille);
            Assert.AreSame(sword, sword.WithBase(GearBaseTable.PlateArmor), "다른 부위로는 바꾸지 않음");
            Assert.AreSame(sword, sword.WithBase("nope"));

            var legacy = new WeaponItem(WeaponItem.TwinbladesId, Grade.Epic, 6, 940);
            var bridged = GearItem.FromWeapon(legacy);
            Assert.AreEqual(legacy.Attack, bridged.Attack);
            Assert.AreEqual(0, bridged.Options.Count);
            var back = bridged.ToWeapon();
            Assert.AreEqual(legacy.WeaponId, back.WeaponId);
            Assert.AreEqual(legacy.Grade, back.Grade);
            Assert.AreEqual(legacy.ItemLevel, back.ItemLevel);
            Assert.AreEqual(legacy.RollPermille, back.RollPermille);
            Assert.IsNull(GearItem.Starting(GearSlot.Armor).ToWeapon());
            Assert.IsNull(GearItem.Starting(GearSlot.Ring1));
            Assert.IsNull(GearItem.Starting(GearSlot.Amulet));
            Assert.AreEqual(GearBaseTable.Longsword, new GearItem("unknown", Grade.Common, 1, 1000).BaseId, "모르는 id는 장검");
        }

        static void AssertSet(string[] ids, int defense, int hp, int move)
        {
            int d = 0, h = 0, m = 0;
            foreach (var id in ids)
            {
                var b = GearBaseTable.Get(id);
                d += b.Defense;
                h += b.Hp;
                m += b.IntrinsicOf(OptionKind.MoveSpeed);
            }
            Assert.AreEqual(defense, d);
            Assert.AreEqual(hp, h);
            Assert.AreEqual(move, m);
        }

        /// <summary>부위 안 종류는 1/3씩(부위마다 3만 회, ± 1.5%p).</summary>
        [Test]
        public void KindIsOneThirdInsideEachPart()
        {
            const int N = 30000;
            foreach (var part in GearSlots.Parts)
            {
                var ctx = new GearRollContext { OnlyParts = new[] { part } };
                var rng = new Pcg32Random(1000UL + (ulong)part);
                var counts = new Dictionary<string, int>();
                for (int i = 0; i < N; i++)
                {
                    var g = LootRules.RollGear(3, rng, Grade.Common, ctx);
                    Assert.AreEqual(part, g.Part);
                    counts.TryGetValue(g.BaseId, out int c);
                    counts[g.BaseId] = c + 1;
                }
                var kinds = GearBaseTable.ForPart(part);
                Assert.AreEqual(3, kinds.Count, part.ToString());
                foreach (var b in kinds)
                {
                    counts.TryGetValue(b.Id, out int c);
                    Assert.That(c / (double)N, Is.InRange(1 / 3.0 - 0.015, 1 / 3.0 + 0.015), b.Id);
                }
            }
        }

        /// <summary>등급별 옵션 줄 수 0/1/2/3/3, 한 장비 안 중복 없음, 옵션은 그 부위 풀에서만.</summary>
        [Test]
        public void OptionCountFollowsGradeWithoutDuplicates()
        {
            var rng = new Pcg32Random(2027);
            var seenGrades = new HashSet<Grade>();
            for (int i = 0; i < 40000; i++)
            {
                var g = LootRules.RollGear(10, rng);
                seenGrades.Add(g.Grade);
                Assert.AreEqual(OptionTable.CountFor(g.Grade), g.Options.Count, g.ToString());
                var pool = new HashSet<OptionKind>();
                foreach (var r in OptionTable.ForPart(g.Part)) pool.Add(r.Kind);
                var seen = new HashSet<OptionKind>();
                foreach (var o in g.Options)
                {
                    Assert.IsTrue(pool.Contains(o.Kind), g.ToString());
                    Assert.IsTrue(seen.Add(o.Kind), "중복 옵션: " + g);
                }
                Assert.AreEqual(g.Grade == Grade.Legendary, g.IsLegendary, g.ToString());
                Assert.AreEqual(10, g.ItemLevel);
                Assert.AreEqual(0, g.Enhance);
            }
            Assert.AreEqual(GradeRules.Count, seenGrades.Count, "10층에서는 다섯 등급이 모두 나온다");
            int[] expected = { 0, 1, 2, 3, 3 };
            for (int grade = 0; grade < GradeRules.Count; grade++) Assert.AreEqual(expected[grade], OptionTable.CountFor((Grade)grade));
        }

        /// <summary>반지 풀에는 치명 피해가 없다(대검 치명 피해 상한 보호, 5-3). 반지의 치명 피해는 송곳 반지 고유로만 온다.</summary>
        [Test]
        public void RingPoolHasNoCritDamage()
        {
            foreach (var r in OptionTable.ForPart(GearPart.Ring)) Assert.AreNotEqual(OptionKind.CritDamage, r.Kind);
            var ctx = new GearRollContext { OnlyParts = new[] { GearPart.Ring } };
            var rng = new Pcg32Random(31);
            for (int i = 0; i < 20000; i++)
            {
                var g = LootRules.RollGear(10, rng, Grade.Rare, ctx);
                Assert.AreEqual(GearPart.Ring, g.Part);
                Assert.AreEqual(0, g.OptionTotal(OptionKind.CritDamage), g.ToString());
            }
            Assert.AreEqual(150, GearBaseTable.Get(GearBaseTable.FangRing).IntrinsicOf(OptionKind.CritDamage));
        }

        /// <summary>정예 승격(8-1): 같은 씨앗에서 일반이 나오면 고급으로 올리고 옵션 1줄을 굴린다. 부위·종류·굴림은 그대로.</summary>
        [Test]
        public void ElitePromotionAddsOneOption()
        {
            var promote = new GearRollContext { PromoteCommon = true };
            int promoted = 0;
            for (ulong seed = 0; seed < 4000; seed++)
            {
                var plain = LootRules.RollGear(1, new Pcg32Random(seed));
                var elite = LootRules.RollGear(1, new Pcg32Random(seed), Grade.Common, promote);
                Assert.AreNotEqual(Grade.Common, elite.Grade);
                if (plain.Grade != Grade.Common)
                {
                    Assert.AreEqual(plain.ToString(), elite.ToString(), "일반이 아니면 그대로");
                    continue;
                }
                promoted++;
                Assert.AreEqual(Grade.Uncommon, elite.Grade);
                Assert.AreEqual(1, elite.Options.Count);
                Assert.AreEqual(0, plain.Options.Count);
                Assert.AreEqual(plain.BaseId, elite.BaseId);
                Assert.AreEqual(plain.RollPermille, elite.RollPermille);
            }
            Assert.Greater(promoted, 3000, "1층은 일반이 82%");
        }

        /// <summary>OnlyParts: 그 안에서만, 부위 비율대로(투구 75 : 반지 180 ± 1%p).</summary>
        [Test]
        public void OnlyPartsLimitsPartsByDropShare()
        {
            var ctx = new GearRollContext { OnlyParts = new[] { GearPart.Helm, GearPart.Ring } };
            var rng = new Pcg32Random(8);
            const int N = 40000;
            int helm = 0;
            for (int i = 0; i < N; i++)
            {
                var g = LootRules.RollGear(2, rng, Grade.Common, ctx);
                Assert.IsTrue(g.Part == GearPart.Helm || g.Part == GearPart.Ring, g.ToString());
                if (g.Part == GearPart.Helm) helm++;
            }
            Assert.That(helm / (double)N, Is.InRange(75 / 255.0 - 0.01, 75 / 255.0 + 0.01));
        }

        /// <summary>
        /// BoostedParts(8-2): 희귀 이상이면 그 부위 가중치 ×3(장갑·장화 225/1300 = 17.3%). 일반·고급은 보정하지 않는다(7.5%).
        /// </summary>
        [Test]
        public void BoostedPartsTripleOnlyForRareOrBetter()
        {
            var ctx = new GearRollContext { BoostedParts = new[] { GearPart.Gloves, GearPart.Boots } };
            const int N = 60000;
            var rng = new Pcg32Random(64);
            int gloves = 0, boots = 0;
            for (int i = 0; i < N; i++)
            {
                var g = LootRules.RollGear(1, rng, Grade.Rare, ctx); // 1층 희귀 이상 = 늘 희귀
                Assert.AreEqual(Grade.Rare, g.Grade);
                if (g.Part == GearPart.Gloves) gloves++;
                if (g.Part == GearPart.Boots) boots++;
            }
            double boosted = 225 / 1300.0;
            Assert.That(gloves / (double)N, Is.InRange(boosted - 0.01, boosted + 0.01));
            Assert.That(boots / (double)N, Is.InRange(boosted - 0.01, boosted + 0.01));

            rng = new Pcg32Random(65);
            int commons = 0, commonGloves = 0;
            for (int i = 0; i < N; i++)
            {
                var g = LootRules.RollGear(1, rng, Grade.Common, ctx);
                if (g.Grade != Grade.Common) continue;
                commons++;
                if (g.Part == GearPart.Gloves) commonGloves++;
            }
            Assert.That(commonGloves / (double)commons, Is.InRange(0.075 - 0.01, 0.075 + 0.01));
        }

        /// <summary>같은 씨앗이면 같은 장비(등급·부위·종류·굴림·옵션·전설 모두).</summary>
        [Test]
        public void SameSeedGivesSameGear()
        {
            var ctx = new GearRollContext
            {
                BoostedParts = new[] { GearPart.Ring, GearPart.Amulet },
                OwnedLegendaries = new List<LegendaryEffect> { LegendaryEffect.ChainBlast },
            };
            var distinct = new HashSet<string>();
            for (ulong seed = 0; seed < 2000; seed++)
            for (int floor = 1; floor <= 10; floor += 3)
            {
                var a = LootRules.RollGear(floor, new Pcg32Random(seed, 9), Grade.Common, ctx);
                var b = LootRules.RollGear(floor, new Pcg32Random(seed, 9), Grade.Common, ctx);
                Assert.AreEqual(a.BaseId, b.BaseId);
                Assert.AreEqual(a.Grade, b.Grade);
                Assert.AreEqual(a.ItemLevel, b.ItemLevel);
                Assert.AreEqual(a.RollPermille, b.RollPermille);
                CollectionAssert.AreEqual(a.Options, b.Options);
                Assert.AreEqual(a.LegendaryId, b.LegendaryId);
                Assert.AreEqual(a.LegendaryRollPermille, b.LegendaryRollPermille);
                distinct.Add(a.ToString());

                var chestA = LootRules.RollChest(true, floor, "", new Pcg32Random(seed), ctx);
                var chestB = LootRules.RollChest(true, floor, "", new Pcg32Random(seed), ctx);
                Assert.AreEqual(chestA.Gear[0].ToString(), chestB.Gear[0].ToString());
            }
            Assert.Greater(distinct.Count, 1000, "씨앗이 다르면 대개 다른 장비");
        }

        /// <summary>전설(6장): 효과가 붙고 옵션 3줄, 이미 가진 효과면 세기 500~1000.</summary>
        [Test]
        public void LegendaryGearCarriesEffectAndOwnedRollsHigh()
        {
            var owned = new GearRollContext
            {
                OwnedLegendaries = new List<LegendaryEffect> { LegendaryEffect.ChainLightning, LegendaryEffect.FlameSteps, LegendaryEffect.ChainBlast },
            };
            var rng = new Pcg32Random(5150);
            int low = 0;
            for (int i = 0; i < 6000; i++)
            {
                var g = LootRules.RollGear(10, rng, Grade.Legendary);
                Assert.AreEqual(Grade.Legendary, g.Grade);
                Assert.IsTrue(g.IsLegendary);
                Assert.AreEqual(3, g.Options.Count);
                Assert.IsTrue(LegendaryTable.CanAppearOn(g.Legendary.Value, g.Part), g.ToString());
                Assert.That(g.LegendaryRollPermille, Is.InRange(0, 1000));
                if (g.LegendaryRollPermille < 500) low++;

                var again = LootRules.RollGear(10, rng, Grade.Legendary, owned);
                Assert.That(again.LegendaryRollPermille, Is.InRange(500, 1000), again.ToString());
            }
            Assert.Greater(low, 2000, "처음 나온 효과는 0~1000 전체에서 굴린다");
        }

        /// <summary>장착 상태 → 부위 점수 → 가장 약한 2부위(8-2). 시작 장비면 빈 반지·목걸이가 가장 약하다.</summary>
        [Test]
        public void WeakestPartsComeFromLoadoutScores()
        {
            var start = Loadout.Starting();
            var scores = LootRules.PartScores(start);
            Assert.AreEqual(GearSlots.PartCount, scores.Length);
            Assert.AreEqual(100.0, scores[(int)GearPart.Weapon], 1e-9, "시작 장검 점수 100");
            Assert.AreEqual(0.0, scores[(int)GearPart.Ring]);
            Assert.AreEqual(0.0, scores[(int)GearPart.Amulet]);
            CollectionAssert.AreEqual(new[] { GearPart.Ring, GearPart.Amulet }, LootRules.WeakestParts(scores));
            CollectionAssert.AreEqual(new[] { GearPart.Ring, GearPart.Amulet }, LootRules.EmptyParts(start));

            // 반지는 두 자리 중 약한 쪽: 반지 1만 끼면 여전히 0.
            start.TryEquip(GearSlot.Ring1, new GearItem(GearBaseTable.IronRing, Grade.Epic, 5, 1000), out _);
            Assert.AreEqual(0.0, LootRules.PartScores(start)[(int)GearPart.Ring]);
            CollectionAssert.AreEqual(new[] { GearPart.Ring, GearPart.Amulet }, LootRules.EmptyParts(start));
            start.TryEquip(GearSlot.Ring2, new GearItem(GearBaseTable.BloodRing, Grade.Common, 1, 1000), out _);
            start.TryEquip(GearSlot.Amulet, new GearItem(GearBaseTable.AmberAmulet, Grade.Rare, 3, 1000), out _);
            Assert.AreEqual(100.0, LootRules.PartScores(start)[(int)GearPart.Ring], 1e-9, "약한 반지(일반 iLv1) 점수");
            Assert.AreEqual(0, LootRules.EmptyParts(start).Length);

            // 같은 점수면 부위 비율이 큰 쪽(갑옷 105 > 투구 75 > 장갑·장화 75는 앞 차례).
            var tie = new double[] { 100, 100, 100, 100, 100, 100, 100 };
            CollectionAssert.AreEqual(new[] { GearPart.Weapon, GearPart.Ring }, LootRules.WeakestParts(tie));
            var armorTie = new double[] { 200, 50, 50, 50, 50, 200, 200 };
            CollectionAssert.AreEqual(new[] { GearPart.Armor, GearPart.Helm }, LootRules.WeakestParts(armorTie));
        }

        /// <summary>2차 6-6 장비 점수: 100 × iLv 배율 × 등급 배율 × (1 + 누적 강화) + 옵션 줄마다 (10 + 10 × 품질) + 전설 50.</summary>
        [Test]
        public void ItemScoreFollowsDesignSixSix()
        {
            Assert.AreEqual(0.0, GearMath.ItemScore(null));
            Assert.AreEqual(100.0, GearMath.ItemScore(GearItem.Starting(GearSlot.Weapon)), 1e-9);
            var enhanced = new GearItem(GearBaseTable.Greatsword, Grade.Rare, 4, 1100, 5);
            Assert.AreEqual(100 * 1.45 * 1.55 * 1.25, GearMath.ItemScore(enhanced), 1e-6, "굴림은 점수에 넣지 않음");

            OptionTable.TryGetRule(GearPart.Weapon, OptionKind.AttackPercent, out var atk);
            OptionTable.TryGetRule(GearPart.Weapon, OptionKind.CritChance, out var crit);
            OptionTable.TryGetRule(GearPart.Weapon, OptionKind.AttackFlat, out var flat);
            var options = new[]
            {
                OptionTable.FromRaw(atk, Grade.Legendary, 10, atk.Max * OptionTable.RawScale),
                OptionTable.FromRaw(crit, Grade.Legendary, 10, crit.Min * OptionTable.RawScale),
                OptionTable.FromRaw(flat, Grade.Legendary, 10, 30 * OptionTable.RawScale),
            };
            var legendary = new GearItem(GearBaseTable.Longsword, Grade.Legendary, 10, 1000, 0, options, LegendaryTable.ChainLightningId, 700);
            double expected = 100 * 2.35 * 2.30 + (10 + 10) + (10 + 0) + (10 + 5) + 50;
            Assert.AreEqual(expected, GearMath.ItemScore(legendary), 0.05);
        }
    }
}
