using System.Collections.Generic;
using System.Linq;
using Demo6.Core.Dungeon;
using Demo6.Core.Random;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 다시 연 층 무리 후보 표(기획/1-2층-탐험-맛-1차.md 4-2, 10장 시험): 몫 &gt; 0, 표 차례 고정(9장 이름), 상한 지킴(씨앗 2,000),
    /// 정예 자리 고정, 못 맞추면 표 첫 줄, 같은 난수면 같은 결과.
    /// </summary>
    public sealed class PackTableTests
    {
        const int SeedCount = 2000;

        [TestCase(1)]
        [TestCase(2)]
        public void EveryRowHasAShareAndTheEliteRowIsTheHardBoar(int floor)
        {
            var rows = PackTable.Options(floor);
            Assert.AreEqual(6, rows.Count, $"{floor}층 후보 6줄");
            foreach (var o in rows)
            {
                Assert.Greater(o.Weight, 0, o.Id + ": 뽑힐 몫");
                Assert.IsFalse(o.Elite, o.Id + ": 정예 줄은 표에 없고 자리에 고정");
                Assert.IsNotEmpty(o.Label, o.Id);
                Assert.IsNotEmpty(o.FallbackLabel, o.Id);
                if (o.State == GroupState.Patrol) Assert.AreNotEqual(o.Label, o.FallbackLabel, o.Id + ": 순찰을 못 하면 먹는 무리 이름");
                else Assert.AreEqual(o.Label, o.FallbackLabel, o.Id);
                Assert.Greater(o.Boars + o.Archers + o.Rats, 0, o.Id);
            }
            Assert.AreEqual(rows.Count, rows.Select(o => o.Id).Distinct().Count(), "줄 이름 겹침 없음");

            var elite = PackTable.EliteOption(floor);
            Assert.IsTrue(elite.Elite);
            Assert.AreEqual("단단한 정예 돌충이", elite.Label);
            Assert.AreEqual(1, elite.Boars);
            Assert.AreEqual(0, elite.Archers);
            Assert.AreEqual(2, elite.Rats);
            Assert.AreEqual(GroupState.Sleep, elite.State);
        }

        [Test]
        public void TableOrderFollowsTheDoc()
        {
            // 4-2 표 그대로(이름, 돌충이·궁수·굴쥐, 상태, 몫).
            AssertRows(1,
                ("굴쥐 떼", 0, 0, 4, GroupState.Eat, 3),
                ("잠든 돌충이", 1, 0, 2, GroupState.Sleep, 3),
                ("먹는 돌충이", 1, 0, 2, GroupState.Eat, 3),
                ("굴쥐 순찰", 0, 0, 4, GroupState.Patrol, 2),
                ("돌충이 둘", 2, 0, 0, GroupState.Sleep, 1),
                ("돌충이 순찰", 1, 0, 2, GroupState.Patrol, 1));
            AssertRows(2,
                ("먹는 돌충이와 궁수", 1, 1, 2, GroupState.Eat, 3),
                ("잠든 돌충이와 궁수", 1, 1, 2, GroupState.Sleep, 3),
                ("궁수 둘", 0, 2, 2, GroupState.Eat, 2),
                ("돌충이 순찰", 1, 0, 2, GroupState.Patrol, 2),
                ("궁수 순찰", 0, 1, 3, GroupState.Patrol, 1),
                ("굴쥐 떼", 0, 0, 5, GroupState.Eat, 1));
            // 부를 때마다 같은 차례·같은 값(표를 고쳐도 다음 부름에 남지 않음).
            var a = PackTable.Options(1);
            a[0].Rats = 99;
            Assert.AreEqual(4, PackTable.Options(1)[0].Rats);
        }

        [Test]
        public void CapsFollowTheDoc()
        {
            var one = PackTable.Caps(1);
            Assert.AreEqual((2, 4, 9, 13, 1, 2), (one.MinBoars, one.MaxBoars, one.MinRats, one.MaxRats, one.MaxPatrols, one.MaxSame));
            Assert.AreEqual((0, 0), (one.MinArchers, one.MaxArchers), "1층 궁수 없음");
            var two = PackTable.Caps(2);
            Assert.AreEqual((3, 5, 1, 3, 7, 4, 8, 1, 2),
                (two.MinArchers, two.MaxArchers, two.MinBoars, two.MaxBoars, two.MaxStrong, two.MinRats, two.MaxRats, two.MaxPatrols, two.MaxSame));

            // 첫 공터 무리를 넣고 세며, 정예는 돌충이 1로 센다.
            var firstTwo = FirstMix(2);
            var rows = PackTable.Options(2);
            Assert.IsTrue(PackTable.WithinCaps(2, firstTwo, new[] { PackTable.EliteOption(2), Row(rows, "two-archers") }), "정예 + 궁수 둘");
            Assert.IsFalse(PackTable.WithinCaps(2, firstTwo, new[] { Row(rows, "rat-swarm"), Row(rows, "rat-swarm") }), "궁수 2·돌충이 0");
            Assert.IsFalse(PackTable.WithinCaps(2, firstTwo, new[] { Row(rows, "boar-patrol"), Row(rows, "archer-patrol") }), "순찰 둘");
            var firstOne = FirstMix(1);
            var ones = PackTable.Options(1);
            Assert.IsTrue(PackTable.WithinCaps(1, firstOne, new[] { Row(ones, "rat-swarm"), Row(ones, "rat-swarm"), Row(ones, "boar-eat") }));
            Assert.IsFalse(PackTable.WithinCaps(1, firstOne, new[] { Row(ones, "rat-swarm"), Row(ones, "rat-swarm"), Row(ones, "rat-swarm") }), "같은 이름 셋·돌충이 1");
            Assert.IsFalse(PackTable.WithinCaps(1, firstOne, new[] { Row(ones, "two-boars"), Row(ones, "two-boars"), Row(ones, "rat-swarm") }), "돌충이 5");
            Assert.IsTrue(PackTable.WithinCaps(1, firstOne, new[] { PackTable.EliteOption(1), Row(ones, "rat-swarm"), Row(ones, "boar-sleep") }), "정예 = 돌충이 1");
        }

        [TestCase(1, 3)]
        [TestCase(2, 2)]
        public void PicksStayWithinCapsOverTwoThousandSeeds(int floor, int slots)
        {
            var first = FirstMix(floor);
            var seen = new HashSet<string>();
            int eliteRuns = 0;
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                int eliteSlot = seed % 3 == 0 ? seed % slots : -1;
                var picks = PackTable.Pick(floor, slots, first, eliteSlot, new Pcg32Random((ulong)seed, FloorSpice.Stream));
                Assert.AreEqual(slots, picks.Length);
                Assert.IsTrue(PackTable.WithinCaps(floor, first, picks), $"{floor}층 씨앗 {seed}: {Describe(picks)}");
                Assert.LessOrEqual(picks.Count(p => p.State == GroupState.Patrol), 1, $"씨앗 {seed}: 순찰 1까지");
                Assert.LessOrEqual(picks.GroupBy(p => p.Label).Max(x => x.Count()), 2, $"씨앗 {seed}: 같은 이름 2까지");
                for (int i = 0; i < slots; i++)
                {
                    Assert.AreEqual(i == eliteSlot, picks[i].Elite, $"씨앗 {seed} 자리 {i}: 정예는 정한 자리에만");
                    if (!picks[i].Elite) seen.Add(picks[i].Id);
                }
                if (eliteSlot >= 0) eliteRuns++;
            }
            Assert.Greater(eliteRuns, 0);
            CollectionAssert.AreEquivalent(PackTable.Options(floor).Select(o => o.Id).ToArray(), seen.ToArray(), "모든 줄이 한 번은 뽑힌다");
        }

        [TestCase(1, 3)]
        [TestCase(2, 2)]
        public void EliteSlotIsAlwaysTheEliteRow(int floor, int slots)
        {
            for (int slot = 0; slot < slots; slot++)
            for (int seed = 1; seed <= 50; seed++)
            {
                var picks = PackTable.Pick(floor, slots, FirstMix(floor), slot, new Pcg32Random((ulong)seed, 4));
                Assert.AreEqual("elite-boar", picks[slot].Id, $"자리 {slot} 씨앗 {seed}");
                Assert.AreEqual(1, picks.Count(p => p.Elite));
            }
            // 범위 밖 자리는 정예 없음.
            Assert.IsFalse(PackTable.Pick(floor, slots, FirstMix(floor), slots, new Pcg32Random(1UL, 4)).Any(p => p.Elite));
            Assert.IsEmpty(PackTable.Pick(floor, 0, FirstMix(floor), 0, new Pcg32Random(1UL, 4)));
        }

        [Test]
        public void ImpossibleCapsFallBackToTheFirstRow()
        {
            // 첫 공터에 돌충이 10이면 어떤 줄로도 1층 상한(돌충이 4까지)을 못 맞춘다 → 자리마다 표 첫 줄(정예 자리는 정예).
            var heavy = new GroupMix { Boars = 10, Rats = 3 };
            var picks = PackTable.Pick(1, 3, heavy, -1, new Pcg32Random(7UL, 4));
            CollectionAssert.AreEqual(new[] { "rat-swarm", "rat-swarm", "rat-swarm" }, picks.Select(p => p.Id).ToArray());
            var withElite = PackTable.Pick(2, 2, new GroupMix { Archers = 9 }, 1, new Pcg32Random(7UL, 4));
            CollectionAssert.AreEqual(new[] { "boar-archer-eat", "elite-boar" }, withElite.Select(p => p.Id).ToArray());
            // 난수가 없어도 첫 줄.
            Assert.IsTrue(PackTable.Pick(2, 2, FirstMix(2), -1, null).All(p => p.Id == "boar-archer-eat"));
            Assert.AreEqual(20, PackTable.MaxRedraws);
        }

        [TestCase(1, 3)]
        [TestCase(2, 2)]
        public void SameRandomOrderSamePicks(int floor, int slots)
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                var a = PackTable.Pick(floor, slots, FirstMix(floor), seed % 4 == 0 ? 0 : -1, new Pcg32Random((ulong)seed, 4));
                var b = PackTable.Pick(floor, slots, FirstMix(floor), seed % 4 == 0 ? 0 : -1, new Pcg32Random((ulong)seed, 4));
                CollectionAssert.AreEqual(a.Select(p => p.Id).ToArray(), b.Select(p => p.Id).ToArray(), $"씨앗 {seed}");
            }
        }

        // ── 도우미 ──

        static GroupMix FirstMix(int floor) => FloorRecipe.For(floor).Groups.First(m => m.FirstClearing);

        static PackOption Row(IReadOnlyList<PackOption> rows, string id) => rows.Single(o => o.Id == id);

        static void AssertRows(int floor, params (string label, int boars, int archers, int rats, GroupState state, int weight)[] want)
        {
            var rows = PackTable.Options(floor);
            Assert.AreEqual(want.Length, rows.Count, $"{floor}층 줄 수");
            for (int i = 0; i < want.Length; i++)
            {
                var o = rows[i];
                Assert.AreEqual(want[i], (o.Label, o.Boars, o.Archers, o.Rats, o.State, o.Weight), $"{floor}층 {i + 1}째 줄");
            }
        }

        static string Describe(IEnumerable<PackOption> picks) => string.Join(", ", picks.Select(p => p.Id));
    }
}
