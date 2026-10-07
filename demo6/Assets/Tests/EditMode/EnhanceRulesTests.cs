using System;
using System.Linq;
using Demo6.Core.Loot;
using Demo6.Core.Random;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 재화 쓸 곳 1차 12-1 EnhanceRulesTests(1~7): 처음 확률 표(2차 8-1), 천장·보정(2-2), 오를 수 있는 끝(일반 5·고급 이상 7),
    /// 견적 막힘 넷과 비용(GearMath.EnhanceCost), 굴림(성공·실패·확정은 난수 안 부름·막히면 아무것도 안 씀), 굴림 뒤 장비 값 보존, 평균 비용(2-3 표).
    /// </summary>
    public sealed class EnhanceRulesTests
    {
        /// <summary>정해 둔 값을 차례로 돌려주고 부른 횟수를 센다.</summary>
        sealed class ScriptedRandom : IRandom
        {
            readonly int[] _values;
            int _next;
            public int Calls;

            public ScriptedRandom(params int[] values) => _values = values;

            public double NextDouble()
            {
                Calls++;
                return 0.0;
            }

            public int NextInt(int min, int max)
            {
                Calls++;
                int v = _values.Length > 0 ? _values[Math.Min(_next, _values.Length - 1)] : min;
                _next++;
                return Math.Max(min, Math.Min(max - 1, v));
            }
        }

        /// <summary>부르면 시험이 실패하는 난수(확정이면 굴리지 않음을 본다).</summary>
        sealed class ForbiddenRandom : IRandom
        {
            public double NextDouble() => throw new AssertionException("확정 강화는 난수를 부르지 않는다");
            public int NextInt(int min, int max) => throw new AssertionException("확정 강화는 난수를 부르지 않는다");
        }

        static GearItem Sword(Grade grade, int enhance = 0, int fails = 0) =>
            new GearItem(GearBaseTable.Longsword, grade, 2, 1050, enhance, enhanceFails: fails);

        // ── 1. 처음 확률 표 ──

        [Test]
        public void BaseChancesMatchSecondDraftTable()
        {
            int[] expected = { 1000, 950, 900, 850, 800, 700, 600 };
            Assert.AreEqual(7, EnhanceRules.FirstPassMax);
            for (int target = 1; target <= EnhanceRules.FirstPassMax; target++)
            {
                Assert.AreEqual(expected[target - 1], EnhanceRules.BaseChancePermille(target), "+" + target);
                Assert.AreEqual(expected[target - 1], EnhanceRules.ChancePermille(target, 0), "+" + target + " 실패 0");
                Assert.AreEqual(target >= 6 ? 100 : 0, EnhanceRules.GaugeStepPermille(target), "+" + target + " 보정");
            }
            foreach (int outside in new[] { -1, 0, 8, 15 })
            {
                Assert.AreEqual(0, EnhanceRules.BaseChancePermille(outside), "범위 밖 " + outside);
                Assert.AreEqual(0, EnhanceRules.ChancePermille(outside, 0), "범위 밖 " + outside);
                Assert.AreEqual(0, EnhanceRules.GaugeStepPermille(outside));
                Assert.AreEqual(0, EnhanceRules.PityFails(outside));
            }
        }

        // ── 2. 천장·보정 ──

        [Test]
        public void PityAndGaugeFollowDesignTwoTwo()
        {
            Assert.AreEqual(0, EnhanceRules.PityFails(1), "+1 천장 0");
            Assert.AreEqual(1000, EnhanceRules.ChancePermille(1, 0));
            for (int target = 2; target <= 5; target++)
            {
                Assert.AreEqual(1, EnhanceRules.PityFails(target), "+" + target + " 천장 1");
                Assert.AreEqual(1000, EnhanceRules.ChancePermille(target, 1), "+" + target + " 실패 1 → 확정");
                Assert.AreEqual(1000, EnhanceRules.ChancePermille(target, 5), "천장을 넘어도 확정");
            }
            Assert.AreEqual(2, EnhanceRules.PityFails(6));
            Assert.AreEqual(2, EnhanceRules.PityFails(7));
            CollectionAssert.AreEqual(new[] { 700, 800, 1000, 1000 }, Enumerable.Range(0, 4).Select(f => EnhanceRules.ChancePermille(6, f)).ToArray(), "+6");
            CollectionAssert.AreEqual(new[] { 600, 700, 1000, 1000 }, Enumerable.Range(0, 4).Select(f => EnhanceRules.ChancePermille(7, f)).ToArray(), "+7");
            Assert.AreEqual(900, EnhanceRules.ChancePermille(3, -2), "음수 실패 수는 0");
        }

        // ── 3. 오를 수 있는 끝 ──

        [Test]
        public void MaxTargetIsGradeCapUpToSeven()
        {
            Assert.AreEqual(5, EnhanceRules.MaxTarget(Grade.Common));
            Assert.AreEqual(7, EnhanceRules.MaxTarget(Grade.Uncommon));
            Assert.AreEqual(7, EnhanceRules.MaxTarget(Grade.Rare));
            Assert.AreEqual(7, EnhanceRules.MaxTarget(Grade.Epic));
            Assert.AreEqual(7, EnhanceRules.MaxTarget(Grade.Legendary));
        }

        // ── 4. 견적 막힘과 비용 ──

        [Test]
        public void QuoteBlocksAndCost()
        {
            var none = EnhanceRules.Quote(null, 99);
            Assert.AreEqual(EnhanceBlock.NoItem, none.Block);
            Assert.IsFalse(none.CanTry);

            var common5 = EnhanceRules.Quote(Sword(Grade.Common, 5), 99);
            Assert.AreEqual(EnhanceBlock.GradeCap, common5.Block, "일반 +5");
            Assert.AreEqual(5, common5.From);
            Assert.AreEqual(5, common5.To, "상한이면 To = From");
            Assert.AreEqual(0, common5.Cost);
            Assert.AreEqual(5, common5.Cap);
            Assert.AreEqual(EnhanceBlock.GradeCap, EnhanceRules.Quote(Sword(Grade.Uncommon, 7), 99).Block, "고급 +7은 등급 상한");
            Assert.AreEqual(EnhanceBlock.FirstPassCap, EnhanceRules.Quote(Sword(Grade.Rare, 7), 99).Block, "희귀 +7은 1차 상한");
            Assert.AreEqual(EnhanceBlock.FirstPassCap, EnhanceRules.Quote(Sword(Grade.Legendary, 9), 999).Block, "전설 +9도 1차 상한");

            var poor = EnhanceRules.Quote(Sword(Grade.Rare, 3), 2);
            Assert.AreEqual(EnhanceBlock.NotEnoughStones, poor.Block);
            Assert.AreEqual(3, poor.Cost, "무기 +4 시도 3석");
            Assert.AreEqual(850, poor.ChancePermille, "모자라도 확률은 채움");
            var ok = EnhanceRules.Quote(Sword(Grade.Rare, 3), 3);
            Assert.AreEqual(EnhanceBlock.None, ok.Block);
            Assert.IsTrue(ok.CanTry);
            Assert.AreEqual(3, ok.From);
            Assert.AreEqual(4, ok.To);

            string[] ids =
            {
                GearBaseTable.Longsword, GearBaseTable.LeatherArmor, GearBaseTable.LeatherHelm, GearBaseTable.LeatherGloves,
                GearBaseTable.LeatherBoots, GearBaseTable.IronRing, GearBaseTable.AmberAmulet,
            };
            foreach (var id in ids)
            {
                for (int from = 0; from < 7; from++)
                {
                    var item = new GearItem(id, Grade.Epic, 3, 1000, from);
                    var q = EnhanceRules.Quote(item, 999);
                    string label = item.Part + " +" + from;
                    Assert.AreEqual(EnhanceBlock.None, q.Block, label);
                    Assert.AreEqual(GearMath.EnhanceCost(item.Part, from + 1), q.Cost, label + " 비용");
                    Assert.AreEqual(from, q.From, label);
                    Assert.AreEqual(from + 1, q.To, label);
                    Assert.AreEqual(7, q.Cap, label);
                    Assert.AreEqual(EnhanceRules.ChancePermille(from + 1, 0), q.ChancePermille, label);
                    Assert.AreEqual(EnhanceRules.PityFails(from + 1), q.PityFails, label);
                    Assert.AreEqual(EnhanceRules.GaugeStepPermille(from + 1), q.GaugeStepPermille, label);
                    Assert.AreEqual(from == 0, q.Guaranteed, label + " +1만 처음부터 확정");
                }
            }

            var fail1 = EnhanceRules.Quote(Sword(Grade.Rare, 5, 1), 99);
            Assert.AreEqual(800, fail1.ChancePermille, "+6 실패 1번");
            Assert.AreEqual(1, fail1.Fails);
            Assert.AreEqual(2, fail1.PityFails);
            Assert.AreEqual(100, fail1.GaugeStepPermille);
            Assert.IsFalse(fail1.Guaranteed);
            var fail2 = EnhanceRules.Quote(Sword(Grade.Rare, 5, 2), 99);
            Assert.AreEqual(1000, fail2.ChancePermille);
            Assert.IsTrue(fail2.Guaranteed, "+6 실패 2번 → 확정");
        }

        // ── 5. 굴림 ──

        [Test]
        public void TryRollsSuccessFailureAndGuaranteed()
        {
            var start = Sword(Grade.Rare, 3);
            var rng = new ScriptedRandom(849);
            var win = EnhanceRules.Try(start, 10, rng);
            Assert.IsTrue(win.Success, "849 < 850");
            Assert.AreEqual(1, rng.Calls, "난수 한 번");
            Assert.AreEqual(4, win.Item.Enhance);
            Assert.AreEqual(0, win.Item.EnhanceFails);
            Assert.AreEqual(3, win.Spent);
            Assert.IsFalse(win.Guaranteed);
            Assert.AreEqual(EnhanceBlock.None, win.Block);

            rng = new ScriptedRandom(850);
            var lose = EnhanceRules.Try(start, 10, rng);
            Assert.IsFalse(lose.Success, "850은 실패");
            Assert.AreEqual(3, lose.Item.Enhance, "단계 그대로");
            Assert.AreEqual(1, lose.Item.EnhanceFails, "실패 수 +1");
            Assert.AreEqual(3, lose.Spent, "실패해도 비용만큼 씀");
            Assert.AreNotSame(start, lose.Item, "새 장비");
            Assert.AreEqual(0, start.EnhanceFails, "받은 장비는 바뀌지 않음");

            var pity = EnhanceRules.Try(lose.Item, 10, new ForbiddenRandom());
            Assert.IsTrue(pity.Success, "+2~+5 실패 1번 뒤 확정");
            Assert.IsTrue(pity.Guaranteed);
            Assert.AreEqual(4, pity.Item.Enhance);
            Assert.AreEqual(0, pity.Item.EnhanceFails);
            Assert.AreEqual(3, pity.Spent);

            var first = EnhanceRules.Try(Sword(Grade.Common), 10, new ForbiddenRandom());
            Assert.IsTrue(first.Success, "+1은 늘 확정");
            Assert.AreEqual(2, first.Spent);

            // +6: 700 → 실패, 800 → 실패, 그다음 확정.
            var six = Sword(Grade.Epic, 5);
            var a = EnhanceRules.Try(six, 99, new ScriptedRandom(700));
            Assert.IsFalse(a.Success);
            Assert.AreEqual(1, a.Item.EnhanceFails);
            var b = EnhanceRules.Try(a.Item, 99, new ScriptedRandom(800));
            Assert.IsFalse(b.Success, "실패 1번 뒤 800‰");
            Assert.AreEqual(2, b.Item.EnhanceFails);
            var c = EnhanceRules.Try(b.Item, 99, new ForbiddenRandom());
            Assert.IsTrue(c.Success);
            Assert.AreEqual(6, c.Item.Enhance);
            Assert.AreEqual(0, c.Item.EnhanceFails);
            Assert.AreEqual(GearMath.EnhanceCost(GearPart.Weapon, 6), c.Spent);
        }

        [Test]
        public void BlockedTrySpendsNothing()
        {
            var item = Sword(Grade.Rare, 3);
            var poor = EnhanceRules.Try(item, 2, new ForbiddenRandom());
            Assert.IsFalse(poor.Success);
            Assert.AreEqual(EnhanceBlock.NotEnoughStones, poor.Block);
            Assert.AreEqual(0, poor.Spent);
            Assert.AreSame(item, poor.Item, "막히면 장비 그대로");

            var capped = Sword(Grade.Common, 5);
            var cap = EnhanceRules.Try(capped, 99, new ForbiddenRandom());
            Assert.AreEqual(EnhanceBlock.GradeCap, cap.Block);
            Assert.AreSame(capped, cap.Item);
            Assert.AreEqual(0, cap.Spent);

            var nothing = EnhanceRules.Try(null, 99, new ForbiddenRandom());
            Assert.AreEqual(EnhanceBlock.NoItem, nothing.Block);
            Assert.IsNull(nothing.Item);
            Assert.AreEqual(0, nothing.Spent);
        }

        // ── 6. 굴림 뒤 장비 값 보존 ──

        static void AssertSameExceptEnhance(GearItem before, GearItem after, string what)
        {
            Assert.AreEqual(before.BaseId, after.BaseId, what + " 종류");
            Assert.AreEqual(before.Grade, after.Grade, what + " 등급");
            Assert.AreEqual(before.ItemLevel, after.ItemLevel, what + " 아이템 레벨");
            Assert.AreEqual(before.RollPermille, after.RollPermille, what + " 굴림");
            CollectionAssert.AreEqual(before.Options.ToArray(), after.Options.ToArray(), what + " 옵션");
            Assert.AreEqual(before.LegendaryId, after.LegendaryId, what + " 전설");
            Assert.AreEqual(before.LegendaryRollPermille, after.LegendaryRollPermille, what + " 전설 굴림");
            CollectionAssert.AreEqual(before.Runes.ToArray(), after.Runes.ToArray(), what + " 룬");
        }

        [Test]
        public void TryKeepsOptionsLegendaryRunesRollAndLevel()
        {
            var item = new GearItem(GearBaseTable.Twinblades, Grade.Legendary, 7, 1080, 2,
                new[] { new GearOption(OptionKind.AttackPercent, 96, 3), new GearOption(OptionKind.CritChance, 51, 2) },
                LegendaryTable.ChainLightningId, 640, new[] { RuneTable.SuperArmorId });
            Assert.IsTrue(item.IsLegendary);
            Assert.AreEqual(1, item.Runes.Count);

            var win = EnhanceRules.Try(item, 99, new ScriptedRandom(0));
            Assert.IsTrue(win.Success);
            Assert.AreEqual(3, win.Item.Enhance);
            AssertSameExceptEnhance(item, win.Item, "성공");

            var lose = EnhanceRules.Try(item, 99, new ScriptedRandom(999));
            Assert.IsFalse(lose.Success);
            Assert.AreEqual(2, lose.Item.Enhance);
            AssertSameExceptEnhance(item, lose.Item, "실패");
        }

        // ── 7. 평균 비용(2-3 표) ──

        [Test]
        public void ExpectedStonesMatchDesignTable()
        {
            double[] attempts = { 1.00, 1.05, 1.10, 1.15, 1.20, 1.36, 1.52 };
            for (int target = 1; target <= 7; target++)
                Assert.AreEqual(attempts[target - 1], EnhanceRules.ExpectedAttempts(target), 1e-9, "+" + target + " 평균 시도 수");
            Assert.AreEqual(0.0, EnhanceRules.ExpectedAttempts(8), 1e-9, "1차 밖");

            Assert.AreEqual(15.65, EnhanceRules.ExpectedStones(GearPart.Weapon, 0, 5), 1e-9, "무기 0→5(2차 8-4의 16)");
            Assert.AreEqual(31.57, EnhanceRules.ExpectedStones(GearPart.Weapon, 0, 7), 1e-9, "무기 0→7(2차 8-4의 32)");
            Assert.AreEqual(8.95, EnhanceRules.ExpectedStones(GearPart.Armor, 0, 5), 1e-9, "갑옷 0→5");
            Assert.AreEqual(8.38, EnhanceRules.ExpectedStones(GearPart.Helm, 0, 7), 1e-9, "투구 0→7");
            Assert.AreEqual(31.57 - 15.65, EnhanceRules.ExpectedStones(GearPart.Weapon, 5, 7), 1e-9, "무기 +5→+7");
            Assert.AreEqual(0.0, EnhanceRules.ExpectedStones(GearPart.Weapon, 7, 10), 1e-9, "+8부터는 1차 밖");
        }
    }
}
