using System.Linq;
using Demo6.Core.Dungeon;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 생성기 합격 검사(매판 새 탐험 1차 2-5 검사 표): 손 지도(정답 예시)는 합격, 규칙 하나씩 어긴 손 글자 지도는 그 규칙으로 불합격.
    /// 층 예산(FloorBudget)이 첫 방문·다시 연 층·밤 사건에 맞게 세는지도 본다.
    /// </summary>
    public sealed class FloorRulesTests
    {
        [Test]
        public void HandMapPassesEveryRule()
        {
            var report = FloorRules.Check(FloorOneMap.Build(), FloorRecipe.For(1));
            Assert.IsTrue(report.Passed, report.ToString());
            Assert.AreEqual(10, report.StartReachable);
            Assert.AreEqual(5, report.MainPathLength);
            Assert.AreEqual(1, report.Loops);
        }

        [Test]
        public void StairsOutOfReachFails()
        {
            // 계단 칸이 어느 길과도 이어져 있지 않다.
            var report = Check("E-A.S", Landing(), Clearing('A', Wood("A.wood")), Stairs());
            AssertFails(report, "계단 칸 S");
        }

        [Test]
        public void StairsBehindAbilityDoorFails()
        {
            var report = Check("E-A-B#S", Landing(), Clearing('A', Group("A.group")), Clearing('B', Wood("B.wood")), Stairs());
            AssertFails(report, "계단 칸 S");
            AssertFails(report, "처음 갈 수 없는 칸 S: 계단");
        }

        [Test]
        public void NameplateBehindAbilityDoorFails()
        {
            var report = Check(
                "E-A-B-S\n" +
                "..#\n" +
                "..N",
                Landing(), Clearing('A', Group("A.group")), Clearing('B', Wood("B.wood")), Stairs(),
                Room('N', new CellFeature { Kind = FeatureKind.Nameplate, Id = "f1.T.nameplate" }));
            AssertFails(report, "처음 갈 수 없는 칸 N: 명패");
        }

        [Test]
        public void EmptyDeadEndFails()
        {
            const string glyphs =
                "E-A-B-S\n" +
                "..|\n" +
                "..C";
            var empty = Check(glyphs, Landing(), Clearing('A', Group("A.group")), Clearing('B'), Stairs(), Room('C'));
            AssertFails(empty, "막다른 칸 C: 보상 없음");
            var rewarded = Check(glyphs, Landing(), Clearing('A', Group("A.group")), Clearing('B'), Stairs(), Room('C', Wood("C.wood")));
            Assert.IsFalse(rewarded.Failures.Any(f => f.Contains("막다른 칸")), rewarded.ToString());
        }

        [Test]
        public void CrackedOrLockedDoorDoesNotEndADeadEnd()
        {
            // C는 열린 길 하나 + 금 간 벽(D로), F는 열린 길 하나 + 자물쇠 문(G로). 곡괭이·열쇠가 없으면 둘 다 눈에 막다른 곳이라 빈손이면 떨어진다.
            const string glyphs =
                "E-A-B-S\n" +
                "..|.|\n" +
                "F-C#D\n" +
                "=\n" +
                "G";
            var legend = new[]
            {
                Landing(), Clearing('A', Group("A.group")), Clearing('B', Wood("B.wood")), Stairs(), Clearing('C'), Room('D', Wood("D.wood")),
                Clearing('F'), Room('G', Wood("G.wood")),
            };
            var map = FloorMap.Parse(1, "시험", glyphs + "\n", legend);
            var none = FloorRules.Check(map, null, new GeneratorInput { Floor = 1, HasPickaxe = false, HasKey = false });
            AssertFails(none, "막다른 칸 F: 보상 없음");
            Assert.IsFalse(none.Failures.Any(f => f.Contains("막다른 칸 C")), "C는 F와 이어져 있어 지나갈 문이 둘\n" + none);
            var cut = FloorMap.Parse(1, "시험", glyphs.Replace("F-C", "F.C") + "\n", legend);
            AssertFails(FloorRules.Check(cut, null, new GeneratorInput { Floor = 1 }), "막다른 칸 C: 보상 없음");
            Assert.IsFalse(FloorRules.Check(cut, null, new GeneratorInput { Floor = 1, HasPickaxe = true }).Failures.Any(f => f.Contains("막다른 칸 C")),
                "곡괭이가 있으면 금 간 벽도 출구");
            var keyed = FloorRules.Check(map, null, new GeneratorInput { Floor = 1, HasKey = true });
            Assert.IsFalse(keyed.Failures.Any(f => f.Contains("막다른 칸 F")), "열쇠가 있으면 자물쇠 문도 출구\n" + keyed);
        }

        [Test]
        public void LandmarkDoorMustFaceTheRecipeSide()
        {
            // 손 지도 사무실은 오른쪽(A) 자물쇠 문. 층 예산이 위쪽을 정하면 떨어진다.
            var one = FloorRecipe.For(1);
            Assert.AreEqual(Side.Right, one.LandmarkDoor);
            var up = new FloorRecipe
            {
                Floor = 1, Name = one.Name, Cells = one.Cells, StartReachable = one.StartReachable, Landmark = one.Landmark,
                LandmarkX = one.LandmarkX, LandmarkY = one.LandmarkY, LandmarkDoor = Side.Up, Groups = one.Groups, Nests = one.Nests,
                WoodChests = one.WoodChests, IronChests = one.IronChests, Ores = one.Ores, Lamps = one.Lamps, Stakes = one.Stakes, Events = one.Events,
            };
            AssertFails(FloorRules.Check(FloorOneMap.Build(), up), "랜드마크 O: 자물쇠 문이 Right 쪽");
        }

        [Test]
        public void LongDeadEndStemFails()
        {
            var report = Check(
                "E-A-S\n" +
                "..|\n" +
                "..B\n" +
                "..|\n" +
                "..C\n" +
                "..|\n" +
                "..D",
                Landing(), Clearing('A', Group("A.group")), Stairs(), Clearing('B'), Clearing('C'), Room('D', Wood("D.wood")));
            AssertFails(report, "막다른 칸 D: 주 길에서 3칸");
        }

        [Test]
        public void ThreeEncountersInARowOnMainPathFails()
        {
            const string glyphs = "E-A-B-C-S";
            var crowded = Check(glyphs, Landing(), Clearing('A', Group("A.group")), Clearing('B', Group("B.group")), Clearing('C', Group("C.group")), Stairs());
            AssertFails(crowded, "마주침 칸 3칸");
            var spaced = Check(glyphs, Landing(), Clearing('A', Group("A.group")), Clearing('B', Wood("B.wood")), Clearing('C', Group("C.group")), Stairs());
            Assert.IsFalse(spaced.Failures.Any(f => f.Contains("마주침")), spaced.ToString());
        }

        [Test]
        public void LongEmptyRunOnMainPathFails()
        {
            // 승강장·계단 앞은 말뚝·등잔뿐이라 새 것으로 세지 않는다: E A B C S 다섯 칸이 비었다.
            var report = Check("E-A-B-C-S", Landing(), Clearing('A'), Corridor('B'), Clearing('C'), Stairs());
            AssertFails(report, "새 것 없는 칸 5칸");
        }

        [Test]
        public void StakesTooFarApartFail()
        {
            var report = Check("E-A-B-C-D-F-S", Landing(), Clearing('A', Group("A.group")), Corridor('B', Wood("B.wood")), Clearing('C', Group("C.group")),
                Corridor('D', Wood("D.wood")), Clearing('F', Group("F.group")), Stairs());
            AssertFails(report, "말뚝 사이");
        }

        [Test]
        public void IdsMustBeUniqueAndKeepOnceIdsForTheirItems()
        {
            var report = Check("E-A-B-S", Landing(),
                Clearing('A', Group("A.group"), new CellFeature { Kind = FeatureKind.WoodChest, Id = "f1.H.iron" }),
                Clearing('B', Wood("B.wood"), Wood("B.wood")), Stairs());
            AssertFails(report, "f1.H.iron: 한 번 받는 것 id와 겹침");
            AssertFails(report, "f1.B.wood: 겹침");
        }

        [Test]
        public void RecipeLimitsCountsGridAndLoops()
        {
            var one = FloorRecipe.For(1);
            var strict = new FloorRecipe
            {
                Floor = 1, Name = one.Name, MaxWidth = 4, MaxHeight = 4, Cells = one.Cells, StartReachable = one.StartReachable,
                Landmark = one.Landmark, LandmarkX = one.LandmarkX, LandmarkY = one.LandmarkY, LandingX = one.LandingX, LandingY = one.LandingY,
                Groups = one.Groups, Nests = one.Nests, WoodChests = one.WoodChests + 1, IronChests = one.IronChests, Ores = one.Ores,
                Lamps = one.Lamps, Stakes = one.Stakes, Events = one.Events, MinLoops = 2,
            };
            var report = FloorRules.Check(FloorOneMap.Build(), strict);
            AssertFails(report, "격자 5×3");
            AssertFails(report, "고리 1개");
            AssertFails(report, "나무 궤짝 3개: 층 예산 4개");
        }

        [Test]
        public void BudgetFirstVisitIsTheRecipe()
        {
            var one = FloorRecipe.For(1);
            var b = FloorBudget.For(one, new GeneratorInput { Floor = 1, FirstVisit = true, DeepestFloor = 9, Night = NightEvent.Upheaval });
            Assert.AreEqual(one.WoodChests, b.WoodChests);
            Assert.AreEqual(one.IronChests, b.IronChests);
            Assert.AreEqual(one.Ores, b.Ores);
            Assert.AreEqual(one.Nests, b.Nests);
            Assert.AreEqual(1, b.Gated);
            Assert.AreEqual(0, b.ExtraDeadEnds);
        }

        [Test]
        public void BudgetDecaysOnReopenedFloors()
        {
            var one = FloorRecipe.For(1);
            var near = FloorBudget.For(one, new GeneratorInput { Floor = 1, FirstVisit = false, DeepestFloor = 2 });
            Assert.AreEqual(3, near.WoodChests, "최고 도달 층 바로 위층은 예산 그대로");
            Assert.AreEqual(1, near.Ores);
            Assert.AreEqual(2, near.IronChests, "능력 문 뒤 1 + 아직 안 받은 보장 상자 1");
            var far = FloorBudget.For(one, new GeneratorInput { Floor = 1, FirstVisit = false, DeepestFloor = 3, OnceDone = new[] { "f1.H.iron" } });
            Assert.AreEqual(1, far.WoodChests, "최고 − 2 이하 층은 나무 궤짝 1개");
            Assert.AreEqual(0, far.Ores, "최고 − 2 이하 층은 광맥 없음");
            Assert.AreEqual(1, far.IronChests, "보장 상자를 받았으면 능력 문 뒤 1개만");
            Assert.IsFalse(far.WeakGroups);
            Assert.IsTrue(FloorBudget.For(one, new GeneratorInput { Floor = 1, FirstVisit = false, DeepestFloor = 4 }).WeakGroups);
        }

        [Test]
        public void NightEventsAdjustReopenedBudgets()
        {
            var two = FloorRecipe.For(2);
            GeneratorInput Night(NightEvent n) => new GeneratorInput { Floor = 2, FirstVisit = false, DeepestFloor = 2, Night = n };
            var burrow = FloorBudget.For(two, Night(NightEvent.RatBurrow));
            Assert.AreEqual(two.Nests + 1, burrow.Nests);
            Assert.IsTrue(burrow.BurrowCorridor);
            Assert.AreEqual(2, FloorBudget.For(two, Night(NightEvent.Upheaval)).IronChests, "능력 문 뒤 1 + 드러남 1");
            Assert.AreEqual(1, FloorBudget.For(two, Night(NightEvent.Collapse)).ExtraDeadEnds);
            Assert.AreEqual(two.Cells, burrow.Cells, "칸 수는 늘지 않는다");
            var upheavalFirst = FloorBudget.For(two, new GeneratorInput { Floor = 2, FirstVisit = true, Night = NightEvent.Upheaval });
            Assert.AreEqual(two.IronChests, upheavalFirst.IronChests, "층 첫 방문(고른 지도)은 밤 사건을 받지 않는다");
        }

        [Test]
        public void RevisitBudgetChecksTheHandMapIronCount()
        {
            // 손 지도의 쇠 궤짝 2 = 능력 문 뒤 1 + 보장 상자 1. 드러남 밤이면 예산이 3이라 떨어진다.
            var input = new GeneratorInput { Floor = 1, FirstVisit = false, DeepestFloor = 2, Night = NightEvent.Upheaval };
            AssertFails(FloorRules.Check(FloorOneMap.Build(), FloorRecipe.For(1), input), "쇠 궤짝 2개: 층 예산 3개");
            input.Night = NightEvent.None;
            Assert.IsTrue(FloorRules.Check(FloorOneMap.Build(), FloorRecipe.For(1), input).Passed);
        }

        // ── 손 글자 지도 도우미(층 예산 없이 구조 규칙만 본다) ──

        static RuleReport Check(string glyphs, params CellDef[] legend) => FloorRules.Check(FloorMap.Parse(1, "시험", glyphs + "\n", legend), null);

        static void AssertFails(RuleReport report, string expected) =>
            Assert.IsTrue(report.Failures.Any(f => f.Contains(expected)), $"'{expected}' 실패가 있어야 한다\n{report}");

        static CellDef Cell(char glyph, PieceKind piece, CellFeature[] features) =>
            new CellDef { Glyph = glyph, Id = glyph.ToString(), Name = glyph.ToString(), Piece = piece, Features = features };

        static CellDef Landing() => Cell('E', PieceKind.Entrance, new[] { F(FeatureKind.Stake, "E.stake"), F(FeatureKind.WallLamp, "E.lamp") });

        static CellDef Stairs() =>
            Cell('S', PieceKind.StairsRoom, new[] { F(FeatureKind.Stake, "S.stake"), F(FeatureKind.WallLamp, "S.lamp"), F(FeatureKind.Stairs, "S.stairs") });

        static CellDef Clearing(char glyph, params CellFeature[] features) => Cell(glyph, PieceKind.Clearing, features);

        static CellDef Corridor(char glyph, params CellFeature[] features) => Cell(glyph, PieceKind.Corridor, features);

        static CellDef Room(char glyph, params CellFeature[] features) => Cell(glyph, PieceKind.Room, features);

        static CellFeature F(FeatureKind kind, string id) => new CellFeature { Kind = kind, Id = "f1." + id };

        static CellFeature Group(string id) => new CellFeature { Kind = FeatureKind.Group, Id = "f1." + id, Boars = 1, Rats = 2 };

        static CellFeature Wood(string id) => F(FeatureKind.WoodChest, id);
    }
}
