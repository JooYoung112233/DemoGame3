using System.Collections.Generic;
using Demo6.Core.Loot;
using Demo6.Core.Random;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 옵션 굴림(장비 문서 5-1·5-2·5-3·5-4): 범위 × 등급 옵션 배율 × 옵션 세기 0.8(12장 밸런스 결정) × (고정값이면 iLv 배율),
    /// ‰ 정수 저장, 반올림 한 번, 단계 1/4 경계, 전설 최대 = 위끝 × 1.5 × 0.8, 등급별 줄 수, 가중치 뽑기.
    /// </summary>
    public sealed class OptionRollTests
    {
        /// <summary>NextInt가 늘 아래끝(Low) 또는 위끝(High) 또는 정한 값(Value, 범위 안으로 자름)을 돌려주는 난수.</summary>
        sealed class EdgeRandom : IRandom
        {
            public enum Mode { Low, High, Value }

            readonly Mode _mode;
            readonly int _value;

            public EdgeRandom(Mode mode, int value = 0)
            {
                _mode = mode;
                _value = value;
            }

            public double NextDouble() => _mode == Mode.High ? 0.999999 : 0.0;

            public int NextInt(int min, int max)
            {
                if (max <= min) return min;
                switch (_mode)
                {
                    case Mode.High: return max - 1;
                    case Mode.Value: return System.Math.Max(min, System.Math.Min(max - 1, _value));
                    default: return min;
                }
            }
        }

        static readonly Grade[] OptionGrades = { Grade.Uncommon, Grade.Rare, Grade.Epic, Grade.Legendary };

        /// <summary>5-3 표 '전설 최대 (iLv1)' 열 × 옵션 세기 0.8(32줄, 표 차례. 흡수 9.6 → 10, 4.8 → 5, 재생 14.4 → 14).</summary>
        static readonly int[] LegendaryMaxAtItemLevelOne =
        {
            48, 96, 48, 240, 96, 144, 10, 48,
            144, 96, 72, 24,
            72, 48, 72, 24,
            72, 48, 24, 5,
            96, 72, 36, 14,
            24, 36, 24, 120,
            240, 72, 72, 120,
        };

        /// <summary>옵션 세기(12장 밸런스 결정: 모든 옵션 ×0.8).</summary>
        static readonly decimal Strength = OptionTable.ScalePermille / 1000m;

        /// <summary>0.5 올림(소수는 decimal로 정확히 셈).</summary>
        static int Round(decimal v) => (int)System.Math.Floor(v + 0.5m);

        [Test]
        public void TableHasThirtyTwoRowsAndFifteenKinds()
        {
            Assert.AreEqual(32, OptionTable.All.Count);
            Assert.AreEqual(15, OptionKinds.Count);
            Assert.AreEqual(15, System.Enum.GetValues(typeof(OptionKind)).Length);
            int[] perPart = { 8, 4, 4, 4, 4, 4, 4 };
            foreach (var part in GearSlots.Parts)
            {
                var pool = OptionTable.ForPart(part);
                Assert.AreEqual(perPart[(int)part], pool.Count, part.ToString());
                var seen = new HashSet<OptionKind>();
                foreach (var r in pool)
                {
                    Assert.IsTrue(seen.Add(r.Kind), "부위 안 종류 중복: " + part + " " + r.Kind);
                    Assert.Less(r.Min, r.Max);
                    Assert.Greater(r.Weight, 0);
                    Assert.AreEqual(!OptionKinds.IsPercent(r.Kind), r.ScalesWithItemLevel, "고정값만 iLv 배율: " + r.Kind);
                }
            }
        }

        [Test]
        public void CountAndValueByGradeMatchDesign()
        {
            int[] count = { 0, 1, 2, 3, 3 };
            int[] value = { 0, 1000, 1150, 1300, 1500 };
            for (int g = 0; g < GradeRules.Count; g++)
            {
                Assert.AreEqual(count[g], OptionTable.CountFor((Grade)g));
                Assert.AreEqual(value[g], OptionTable.ValuePermilleFor((Grade)g));
            }
            Assert.AreEqual(800, OptionTable.ScalePermille, "12장 밸런스 결정: 모든 옵션 ×0.8");
        }

        /// <summary>5-3 '전설 최대 = 위끝 × 1.5'(iLv1) × 옵션 세기 0.8. % 옵션은 iLv와 무관하다.</summary>
        [Test]
        public void LegendaryTopIsUpperBoundTimesOneAndHalf()
        {
            var top = new EdgeRandom(EdgeRandom.Mode.High);
            for (int i = 0; i < OptionTable.All.Count; i++)
            {
                var rule = OptionTable.All[i];
                var o = OptionTable.Roll(rule, Grade.Legendary, 1, top);
                Assert.AreEqual(LegendaryMaxAtItemLevelOne[i], o.Value, rule.Part + " " + rule.Kind);
                Assert.AreEqual(Round(rule.Max * 1.5m * Strength), o.Value);
                Assert.AreEqual(4, o.Tier);
                Assert.AreEqual(o.Value, OptionTable.MaxValue(rule, Grade.Legendary, 1));
                if (!rule.ScalesWithItemLevel)
                    Assert.AreEqual(o.Value, OptionTable.Roll(rule, Grade.Legendary, 10, top).Value, "% 옵션은 iLv와 무관");
            }
        }

        /// <summary>범위 × 등급 × 옵션 세기 × iLv: 양 끝 값이 공식 그대로이고, 무작위 값은 그 사이에 든다.</summary>
        [Test]
        public void RolledValuesStayInsideRangeTimesGradeTimesItemLevel()
        {
            var low = new EdgeRandom(EdgeRandom.Mode.Low);
            var high = new EdgeRandom(EdgeRandom.Mode.High);
            var rng = new Pcg32Random(55);
            int[] itemLevels = { 1, 4, 8, 10, 11 };
            foreach (var rule in OptionTable.All)
            foreach (var grade in OptionGrades)
            foreach (int ilv in itemLevels)
            {
                decimal scale = OptionTable.ValuePermilleFor(grade) / 1000m * Strength;
                if (rule.ScalesWithItemLevel) scale *= GearMath.ItemLevelPermille(ilv) / 1000m;
                int lo = Round(rule.Min * scale);
                int hi = Round(rule.Max * scale);
                string label = rule.Part + " " + rule.Kind + " " + grade + " iLv" + ilv;
                Assert.AreEqual(lo, OptionTable.Roll(rule, grade, ilv, low).Value, label);
                Assert.AreEqual(hi, OptionTable.Roll(rule, grade, ilv, high).Value, label);
                Assert.AreEqual(lo, OptionTable.MinValue(rule, grade, ilv), label);
                Assert.AreEqual(hi, OptionTable.MaxValue(rule, grade, ilv), label);
                for (int i = 0; i < 40; i++)
                {
                    var o = OptionTable.Roll(rule, grade, ilv, rng);
                    Assert.AreEqual(rule.Kind, o.Kind);
                    Assert.That(o.Value, Is.InRange(lo, hi), label);
                    Assert.That(o.Tier, Is.InRange(1, 4), label);
                }
            }
        }

        /// <summary>
        /// 5-2 '반올림은 마지막에 한 번': 장화 재생 위끝 12 × 영웅 1.30 × 옵션 세기 0.8 = 12.48 → 12(어느 순서로든 중간에 반올림하면 13),
        /// 아래끝 6 × 고급 × 0.8 × iLv6 1.75 = 8.4 → 8(중간에 반올림하면 9).
        /// </summary>
        [Test]
        public void RoundingHappensOnceAtTheEnd()
        {
            OptionTable.TryGetRule(GearPart.Boots, OptionKind.HpRegen, out var regen);
            Assert.AreEqual(12, OptionTable.Roll(regen, Grade.Epic, 1, new EdgeRandom(EdgeRandom.Mode.High)).Value);
            Assert.AreEqual(8, OptionTable.Roll(regen, Grade.Uncommon, 6, new EdgeRandom(EdgeRandom.Mode.Low)).Value);
            OptionTable.TryGetRule(GearPart.Boots, OptionKind.DefenseFlat, out var def);
            Assert.AreEqual(32, OptionTable.Roll(def, Grade.Legendary, 6, new EdgeRandom(EdgeRandom.Mode.Low)).Value, "15 × 1.5 × 0.8 × 1.75 = 31.5는 올림");
            // 카드 예(5-4): 희귀 대검 iLv8 공격력% 원값 55.0‰ × 1.15 × 0.8 = 50.6 → 51(+5.1%, 단계 Ⅱ).
            OptionTable.TryGetRule(GearPart.Weapon, OptionKind.AttackPercent, out var atkPct);
            var card = OptionTable.Roll(atkPct, Grade.Rare, 8, new EdgeRandom(EdgeRandom.Mode.Value, 55000));
            Assert.AreEqual(51, card.Value);
            Assert.AreEqual(2, card.Tier);
            Assert.AreEqual("+5.1% 공격력", OptionKinds.Format(card.Kind, card.Value));
        }

        /// <summary>5-2 저장: % 옵션은 ‰ 정수, 고정값은 10배 단위 정수. 화면 글은 ‰를 소수 한 자리 %로 보인다.</summary>
        [Test]
        public void PercentOptionsAreStoredInPermille()
        {
            OptionTable.TryGetRule(GearPart.Weapon, OptionKind.CritChance, out var crit);
            var o = OptionTable.Roll(crit, Grade.Uncommon, 1, new EdgeRandom(EdgeRandom.Mode.High));
            Assert.AreEqual(32, o.Value, "치명 4% × 0.8 = 3.2% = 32‰");
            Assert.AreEqual("+3.2% 치명타 확률", OptionKinds.Format(o.Kind, o.Value));
            Assert.AreEqual("+6.2% 공격력", OptionKinds.Format(OptionKind.AttackPercent, 62));
            Assert.AreEqual("+0.4% 체력 흡수", OptionKinds.Format(OptionKind.LifeSteal, 4));
            OptionTable.TryGetRule(GearPart.Weapon, OptionKind.AttackFlat, out var atk);
            var flat = OptionTable.Roll(atk, Grade.Uncommon, 8, new EdgeRandom(EdgeRandom.Mode.High));
            Assert.AreEqual(66, flat.Value, "공격력+ 40 × 0.8 × iLv8 2.05 = 65.6");
            Assert.AreEqual("+66 공격력", OptionKinds.Format(flat.Kind, flat.Value));
            Assert.IsTrue(OptionKinds.IsPercent(OptionKind.MoveSpeed));
            Assert.IsFalse(OptionKinds.IsPercent(OptionKind.HpRegen));
        }

        /// <summary>단계 Ⅰ~Ⅳ = 원값이 범위의 몇 번째 1/4인가. 경계 값은 위 구간에 든다.</summary>
        [Test]
        public void TierBoundariesAreQuarters()
        {
            OptionTable.TryGetRule(GearPart.Weapon, OptionKind.CritChance, out var rule); // 20~40 → 원값 20,000~40,000
            int[] raw = { 20000, 24999, 25000, 29999, 30000, 34999, 35000, 40000 };
            int[] tier = { 1, 1, 2, 2, 3, 3, 4, 4 };
            for (int i = 0; i < raw.Length; i++)
            {
                Assert.AreEqual(tier[i], OptionTable.TierOf(rule, raw[i]), "원값 " + raw[i]);
                Assert.AreEqual(tier[i], OptionTable.Roll(rule, Grade.Rare, 1, new EdgeRandom(EdgeRandom.Mode.Value, raw[i])).Tier);
            }
            Assert.AreEqual(1, OptionTable.TierOf(rule, 0), "범위 밖은 끝 단계로");
            Assert.AreEqual(4, OptionTable.TierOf(rule, 99999));
        }

        /// <summary>단계는 값을 제한하지 않는다(5-4): 1층·iLv1에서도 Ⅳ가 나오고, 네 단계가 거의 1/4씩 나온다.</summary>
        [Test]
        public void TiersAreUniformAndUnlockedAtItemLevelOne()
        {
            OptionTable.TryGetRule(GearPart.Ring, OptionKind.HpFlat, out var rule);
            var rng = new Pcg32Random(404);
            var counts = new int[5];
            const int N = 40000;
            for (int i = 0; i < N; i++) counts[OptionTable.Roll(rule, Grade.Uncommon, 1, rng).Tier]++;
            for (int t = 1; t <= 4; t++) Assert.That(counts[t] / (double)N, Is.InRange(0.24, 0.26), "단계 " + t);
        }

        /// <summary>등급별 줄 수 0/1/2/3/3, 한 장비 안 중복 없음, 모두 그 부위 풀에서 나옴.</summary>
        [Test]
        public void RollAllGivesGradeCountWithoutDuplicates()
        {
            var rng = new Pcg32Random(77);
            foreach (var part in GearSlots.Parts)
            {
                var pool = new HashSet<OptionKind>();
                foreach (var r in OptionTable.ForPart(part)) pool.Add(r.Kind);
                for (int g = 0; g < GradeRules.Count; g++)
                for (int i = 0; i < 500; i++)
                {
                    var options = OptionTable.RollAll(part, (Grade)g, 5, rng);
                    Assert.AreEqual(OptionTable.CountFor((Grade)g), options.Count);
                    var seen = new HashSet<OptionKind>();
                    foreach (var o in options)
                    {
                        Assert.IsTrue(pool.Contains(o.Kind), part + " 풀 밖 옵션 " + o.Kind);
                        Assert.IsTrue(seen.Add(o.Kind), part + " 중복 " + o.Kind);
                    }
                }
            }
        }

        /// <summary>줄 고르기는 가중치대로(무기 고급 1줄: 공격력+ 10/58, 흡수 4/58 ± 1%p).</summary>
        [Test]
        public void RollAllPicksByWeight()
        {
            var rng = new Pcg32Random(91);
            const int N = 50000;
            var counts = new Dictionary<OptionKind, int>();
            for (int i = 0; i < N; i++)
            {
                var o = OptionTable.RollAll(GearPart.Weapon, Grade.Uncommon, 1, rng)[0];
                counts.TryGetValue(o.Kind, out int c);
                counts[o.Kind] = c + 1;
            }
            int total = 0;
            foreach (var r in OptionTable.ForPart(GearPart.Weapon)) total += r.Weight;
            Assert.AreEqual(58, total);
            foreach (var r in OptionTable.ForPart(GearPart.Weapon))
            {
                counts.TryGetValue(r.Kind, out int c);
                double expected = r.Weight / (double)total;
                Assert.That(c / (double)N, Is.InRange(expected - 0.01, expected + 0.01), r.Kind.ToString());
            }
        }

        /// <summary>같은 씨앗이면 같은 옵션.</summary>
        [Test]
        public void RollAllIsDeterministic()
        {
            for (ulong seed = 1; seed < 200; seed++)
            {
                var a = OptionTable.RollAll(GearPart.Gloves, Grade.Epic, 7, new Pcg32Random(seed));
                var b = OptionTable.RollAll(GearPart.Gloves, Grade.Epic, 7, new Pcg32Random(seed));
                CollectionAssert.AreEqual(a, b);
            }
        }
    }
}
