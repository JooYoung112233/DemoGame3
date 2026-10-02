using System.Linq;
using Demo6.Core.Dungeon;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>3차 초안 2-1 지도 검사: 주 길 길이, 능력 문 뒤에 계단·명패 없음, 말뚝 사이 5칸 이하, 막다른 갈래 보상.</summary>
    public sealed class FloorMapTests
    {
        static FloorMap Map => FloorOneMap.Build();

        [Test]
        public void FloorOneParsesTwelveCellsAndThirteenEdges()
        {
            var map = Map;
            Assert.AreEqual(12, map.Cells.Count);
            Assert.AreEqual(13, map.Edges.Count);
            Assert.AreEqual(5, map.Width);
            Assert.AreEqual(3, map.Height);
            Assert.AreEqual("E", map.At(0, 1).Id);
            Assert.AreEqual("O", map.At(0, 2).Id);
            Assert.AreEqual("H", map.At(3, 0).Id);
            var vd = map.Find("V").EdgeTo(map.Find("D"));
            Assert.AreEqual(EdgeKind.Cracked, vd.Kind);
            Assert.AreEqual(EdgeKind.Plank, map.Find("D").EdgeTo(map.Find("H")).Kind);
            Assert.AreEqual(EdgeKind.Locked, map.Find("O").EdgeTo(map.Find("A")).Kind);
            Assert.AreEqual(Side.Down, map.Find("D").EdgeTo(map.Find("H")).SideFrom(map.Find("D")));
            Assert.AreEqual(Side.Left, map.Find("A").EdgeTo(map.Find("O")).SideFrom(map.Find("A")));
        }

        [Test]
        public void MainPathIsHalfOfStartReachableCells()
        {
            var map = Map;
            var path = map.ShortestPath(map.Find("E"), map.Find("S"), FloorMap.OpenOnly);
            CollectionAssert.AreEqual(new[] { "E", "c1", "B", "D", "S" }, path.Select(c => c.Id).ToArray());
            var start = map.Reachable(map.Find("E"), FloorMap.StartPassable);
            Assert.AreEqual(10, start.Count);
            Assert.That(path.Count / (double)start.Count, Is.InRange(0.4, 0.5));
        }

        [Test]
        public void NothingRequiredBehindAbilityDoors()
        {
            var map = Map;
            var start = map.Reachable(map.Find("E"), FloorMap.StartPassable);
            foreach (var cell in map.Cells.Where(c => !start.Contains(c)))
            {
                Assert.IsFalse(cell.Has(FeatureKind.Stairs), cell.Id);
                Assert.IsFalse(cell.Has(FeatureKind.Nameplate), cell.Id);
            }
            var withPickaxe = map.Reachable(map.Find("E"), k => k != EdgeKind.Locked);
            Assert.AreEqual(11, withPickaxe.Count);
            Assert.IsTrue(map.Find("H").Has(FeatureKind.Pickaxe), "곡괭이는 판자벽 뒤(처음부터 갈 수 있음)");
        }

        [Test]
        public void StakesAreAtMostFiveCellsApart()
        {
            var map = Map;
            var stakes = map.Cells.Where(c => c.Has(FeatureKind.Stake)).ToList();
            Assert.AreEqual(2, stakes.Count);
            var path = map.ShortestPath(stakes[0], stakes[1], FloorMap.OpenOnly);
            Assert.That(path.Count - 1, Is.LessThanOrEqualTo(5));
        }

        [Test]
        public void DeadEndsAlwaysReward()
        {
            var map = Map;
            foreach (var cell in map.Cells.Where(c => c.Edges.Count == 1 && !c.Has(FeatureKind.Stake)))
            {
                bool reward = cell.Features.Any(f => f.Kind == FeatureKind.WoodChest || f.Kind == FeatureKind.IronChest ||
                                                     f.Kind == FeatureKind.Nameplate || f.Kind == FeatureKind.Pickaxe || f.Kind == FeatureKind.Safe);
                Assert.IsTrue(reward, cell.Id);
            }
        }

        [Test]
        public void FeatureIdsAreUnique()
        {
            var ids = Map.Cells.SelectMany(c => c.Features).Select(f => f.Id).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count());
        }

        [Test]
        public void FloorOneCountsMatchPlan()
        {
            var all = Map.Cells.SelectMany(c => c.Features).ToList();
            Assert.AreEqual(4, all.Count(f => f.Kind == FeatureKind.Group));
            Assert.AreEqual(1, all.Count(f => f.Kind == FeatureKind.Nest));
            Assert.AreEqual(3, all.Count(f => f.Kind == FeatureKind.WoodChest));
            Assert.AreEqual(2, all.Count(f => f.Kind == FeatureKind.IronChest));
            Assert.AreEqual(1, all.Count(f => f.Kind == FeatureKind.Ore));
            Assert.AreEqual(4, all.Count(f => f.Kind == FeatureKind.WallLamp));
            Assert.AreEqual(1, all.Count(f => f.Kind == FeatureKind.Lunchbox));
            Assert.AreEqual(3, all.Where(f => f.Kind == FeatureKind.Group).Sum(f => f.Boars));
            Assert.AreEqual(11, all.Where(f => f.Kind == FeatureKind.Group).Sum(f => f.Rats));
        }
    }
}
