using System.Collections.Generic;
using Demo6.Core.Loot;
using Demo6.Core.Random;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 전설 3종과 부위 묶음(장비 문서 6장): 효과 1/3 → 효과의 부위 묶음 안 비율. 부위마다 효과 1개 이상(에셋 검사 50번),
    /// 전설 부위 분포 무기 23% · 장화 20% · 목걸이 17% · 갑옷 13% · 장갑 10% · 투구 10% · 반지 7%, 겹침(높은 굴림 하나만, 같으면 먼저 낀 자리).
    /// </summary>
    public sealed class LegendaryGroupTests
    {
        [Test]
        public void IdsAndGroupsMatchDesign()
        {
            Assert.AreEqual(3, LegendaryTable.Count);
            Assert.AreEqual("leg_chain_lightning", LegendaryTable.ChainLightningId);
            Assert.AreEqual("leg_flame_steps", LegendaryTable.FlameStepsId);
            Assert.AreEqual("leg_chain_blast", LegendaryTable.ChainBlastId);
            Assert.AreEqual(LegendaryEffect.ChainLightning, LegendaryTable.Get(LegendaryTable.ChainLightningId).Effect);
            Assert.AreEqual(LegendaryEffect.FlameSteps, LegendaryTable.Get(LegendaryTable.FlameStepsId).Effect);
            Assert.AreEqual(LegendaryEffect.ChainBlast, LegendaryTable.Get(LegendaryTable.ChainBlastId).Effect);
            Assert.IsNull(LegendaryTable.Get("leg_unknown"));

            AssertShares(LegendaryEffect.ChainLightning, (GearPart.Weapon, 700), (GearPart.Gloves, 300));
            AssertShares(LegendaryEffect.FlameSteps, (GearPart.Boots, 600), (GearPart.Armor, 400));
            AssertShares(LegendaryEffect.ChainBlast, (GearPart.Amulet, 500), (GearPart.Helm, 300), (GearPart.Ring, 200));
        }

        static void AssertShares(LegendaryEffect effect, params (GearPart part, int permille)[] expected)
        {
            var parts = LegendaryTable.Get(effect).Parts;
            Assert.AreEqual(expected.Length, parts.Length, effect.ToString());
            int sum = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                Assert.AreEqual(expected[i].part, parts[i].Part, effect.ToString());
                Assert.AreEqual(expected[i].permille, parts[i].Permille, effect.ToString());
                sum += parts[i].Permille;
            }
            Assert.AreEqual(1000, sum, effect.ToString());
        }

        /// <summary>에셋 검사 50번 '부위마다 전설 효과 1개 이상'.</summary>
        [Test]
        public void EveryPartHasAtLeastOneEffect()
        {
            foreach (var part in GearSlots.Parts)
            {
                var effects = LegendaryTable.EffectsOn(part);
                Assert.GreaterOrEqual(effects.Count, 1, part.ToString());
                foreach (var e in effects) Assert.IsTrue(LegendaryTable.CanAppearOn(e, part));
            }
            Assert.IsFalse(LegendaryTable.CanAppearOn(LegendaryEffect.ChainLightning, GearPart.Ring));
            Assert.IsFalse(LegendaryTable.CanAppearOn(LegendaryEffect.ChainBlast, GearPart.Weapon));
        }

        /// <summary>효과 1/3 ± 1%p(10만 회).</summary>
        [Test]
        public void EffectIsOneThird()
        {
            const int N = 100000;
            var rng = new Pcg32Random(303);
            var counts = new int[LegendaryTable.Count];
            for (int i = 0; i < N; i++) counts[(int)LegendaryTable.RollEffect(rng)]++;
            foreach (int c in counts) Assert.That(c / (double)N, Is.InRange(1 / 3.0 - 0.01, 1 / 3.0 + 0.01));
        }

        /// <summary>전설 장비 부위 분포(RollGear 전체 경로, 10만 회): 23.3 / 20 / 16.7 / 13.3 / 10 / 10 / 6.7% ± 1%p, 효과도 1/3씩.</summary>
        [Test]
        public void LegendaryPartDistributionMatchesDesign()
        {
            const int N = 100000;
            var rng = new Pcg32Random(2310);
            var parts = new int[GearSlots.PartCount];
            var effects = new int[LegendaryTable.Count];
            for (int i = 0; i < N; i++)
            {
                var g = LootRules.RollGear(10, rng, Grade.Legendary);
                Assert.IsTrue(g.IsLegendary);
                parts[(int)g.Part]++;
                effects[(int)g.Legendary.Value]++;
            }
            var expected = new Dictionary<GearPart, double>
            {
                { GearPart.Weapon, 700 / 3000.0 },
                { GearPart.Boots, 600 / 3000.0 },
                { GearPart.Amulet, 500 / 3000.0 },
                { GearPart.Armor, 400 / 3000.0 },
                { GearPart.Gloves, 300 / 3000.0 },
                { GearPart.Helm, 300 / 3000.0 },
                { GearPart.Ring, 200 / 3000.0 },
            };
            foreach (var kv in expected)
                Assert.That(parts[(int)kv.Key] / (double)N, Is.InRange(kv.Value - 0.01, kv.Value + 0.01), kv.Key.ToString());
            foreach (int c in effects) Assert.That(c / (double)N, Is.InRange(1 / 3.0 - 0.01, 1 / 3.0 + 0.01));
            // 문서 반올림 표: 23 / 20 / 17 / 13 / 10 / 10 / 7%.
            Assert.AreEqual(23, (int)System.Math.Round(expected[GearPart.Weapon] * 100));
            Assert.AreEqual(17, (int)System.Math.Round(expected[GearPart.Amulet] * 100));
            Assert.AreEqual(7, (int)System.Math.Round(expected[GearPart.Ring] * 100));
        }

        /// <summary>OnlyParts가 있으면 그 부위에 나올 수 있는 효과 가운데 1/k, 부위는 OnlyParts 안에서만.</summary>
        [Test]
        public void OnlyPartsNarrowsEffectsAndParts()
        {
            var rng = new Pcg32Random(17);
            var ringOnly = new GearRollContext { OnlyParts = new[] { GearPart.Ring } };
            for (int i = 0; i < 3000; i++)
            {
                var g = LootRules.RollGear(10, rng, Grade.Legendary, ringOnly);
                Assert.AreEqual(GearPart.Ring, g.Part);
                Assert.AreEqual(LegendaryEffect.ChainBlast, g.Legendary.Value);
            }

            var weaponOrArmor = new GearRollContext { OnlyParts = new[] { GearPart.Weapon, GearPart.Armor } };
            const int N = 30000;
            int lightning = 0;
            for (int i = 0; i < N; i++)
            {
                var g = LootRules.RollGear(10, rng, Grade.Legendary, weaponOrArmor);
                if (g.Legendary.Value == LegendaryEffect.ChainLightning)
                {
                    lightning++;
                    Assert.AreEqual(GearPart.Weapon, g.Part, "번개는 무기·장갑 가운데 무기만 남음");
                }
                else
                {
                    Assert.AreEqual(LegendaryEffect.FlameSteps, g.Legendary.Value);
                    Assert.AreEqual(GearPart.Armor, g.Part, "발자국은 장화·갑옷 가운데 갑옷만 남음");
                }
            }
            Assert.That(lightning / (double)N, Is.InRange(0.5 - 0.015, 0.5 + 0.015), "효과 2개 가운데 1/2");
        }

        /// <summary>이미 가진 효과면 위쪽 절반(500~1000), 아니면 0~1000.</summary>
        [Test]
        public void OwnedEffectRollsUpperHalf()
        {
            var rng = new Pcg32Random(44);
            int below = 0;
            for (int i = 0; i < 10000; i++)
            {
                Assert.That(LegendaryTable.RollStrength(true, rng), Is.InRange(500, 1000));
                int fresh = LegendaryTable.RollStrength(false, rng);
                Assert.That(fresh, Is.InRange(0, 1000));
                if (fresh < 500) below++;
            }
            Assert.That(below / 10000.0, Is.InRange(0.47, 0.53));
        }

        static GearItem Legend(string baseId, string legendId, int roll) =>
            new GearItem(baseId, Grade.Legendary, 8, 1000, 0, null, legendId, roll);

        /// <summary>겹침(6장): 같은 효과 둘이면 굴림이 높은 하나만, 같으면 먼저 낀 자리. 다른 효과끼리는 함께 작동한다.</summary>
        [Test]
        public void OverlapKeepsHighestAndFirstOnTie()
        {
            var loadout = Loadout.Starting();
            var ring = Legend(GearBaseTable.IronRing, LegendaryTable.ChainBlastId, 400);
            var amulet = Legend(GearBaseTable.FangAmulet, LegendaryTable.ChainBlastId, 700);
            var weapon = Legend(GearBaseTable.Greatsword, LegendaryTable.ChainLightningId, 250);
            Assert.IsTrue(loadout.TryEquip(GearSlot.Ring1, ring, out _));
            Assert.IsTrue(loadout.TryEquip(GearSlot.Amulet, amulet, out _));
            Assert.IsTrue(loadout.TryEquip(GearSlot.Weapon, weapon, out _));

            var active = loadout.ActiveLegendaries();
            Assert.AreEqual(3, active.Length);
            Assert.AreEqual(250, active[(int)LegendaryEffect.ChainLightning]);
            Assert.AreEqual(-1, active[(int)LegendaryEffect.FlameSteps]);
            Assert.AreEqual(700, active[(int)LegendaryEffect.ChainBlast]);
            Assert.IsTrue(LegendaryTable.IsSuppressed(ring, loadout.Items), "낮은 굴림은 겹침");
            Assert.IsFalse(LegendaryTable.IsSuppressed(amulet, loadout.Items));
            Assert.IsFalse(LegendaryTable.IsSuppressed(weapon, loadout.Items), "다른 효과는 함께 작동");

            // 같은 굴림: 자리 차례에서 먼저인 반지 1이 작동하고 목걸이가 꺼진다.
            var tieRing = Legend(GearBaseTable.BloodRing, LegendaryTable.ChainBlastId, 700);
            Assert.IsTrue(loadout.TryEquip(GearSlot.Ring1, tieRing, out var removed));
            Assert.AreSame(ring, removed);
            Assert.AreEqual(700, loadout.ActiveLegendaries()[(int)LegendaryEffect.ChainBlast]);
            Assert.IsFalse(LegendaryTable.IsSuppressed(tieRing, loadout.Items));
            Assert.IsTrue(LegendaryTable.IsSuppressed(amulet, loadout.Items));

            // 반지 두 개의 폭발도 같은 규칙.
            var ring2 = Legend(GearBaseTable.FangRing, LegendaryTable.ChainBlastId, 900);
            Assert.IsTrue(loadout.TryEquip(GearSlot.Ring2, ring2, out _));
            Assert.AreEqual(900, loadout.ActiveLegendaries()[(int)LegendaryEffect.ChainBlast]);
            Assert.IsTrue(LegendaryTable.IsSuppressed(tieRing, loadout.Items));
            Assert.IsFalse(LegendaryTable.IsSuppressed(ring2, loadout.Items));

            Assert.IsFalse(LegendaryTable.IsSuppressed(GearItem.Starting(GearSlot.Weapon), loadout.Items), "전설이 아니면 겹침 없음");
            var none = LegendaryTable.Active(null);
            foreach (int v in none) Assert.AreEqual(-1, v);
        }
    }
}
