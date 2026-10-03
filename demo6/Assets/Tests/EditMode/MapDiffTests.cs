using System.Linq;
using Demo6.Core.Dungeon;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 매판 새 탐험 1차 2-5 차례 13 흔적 비교: 지난 원정 지도와 이번 지도를 칸 자리로 견준다.
    /// '1층 예: 두 번째 원정' 지도를 첫 원정 손 지도와 견주면 문서 흔적 목록(흙더미 3자리, 갓 판 굴 2, 깨진 돌 1)이 나와야 한다.
    /// </summary>
    public sealed class MapDiffTests
    {
        /// <summary>문서 2-5 '1층 예: 두 번째 원정' 글자 지도.</summary>
        const string SecondExpedition =
            "..X-S\n" +
            "..|\n" +
            "O=A-C\n" +
            "..|.|\n" +
            "E-1-B-V:H\n" +
            "....|.#\n" +
            "....T.K\n";

        static string[] Sorted(System.Collections.Generic.IEnumerable<TraceSpot> spots) =>
            spots.Select(s => s.ToString()).OrderBy(s => s, System.StringComparer.Ordinal).ToArray();

        [Test]
        public void DocExampleGivesSixTraces()
        {
            var spots = MapDiff.Compare(FloorOneMap.Glyphs, SecondExpedition);
            var expected = new[]
            {
                // 흙더미: 1 아래(옛 T로 가는 길), C 오른쪽(옛 c2–V 길), V 위(옛 V–D 금 간 벽).
                new TraceSpot(1, 1, Side.Down, TraceKind.Rubble),
                new TraceSpot(2, 2, Side.Right, TraceKind.Rubble),
                new TraceSpot(3, 1, Side.Up, TraceKind.Rubble),
                // 갓 판 굴: A–X, X–S.
                new TraceSpot(1, 2, Side.Up, TraceKind.FreshDig),
                new TraceSpot(1, 3, Side.Right, TraceKind.FreshDig),
                // 깨진 돌: B–T(옛 B–K 금 간 벽 자리).
                new TraceSpot(2, 0, Side.Up, TraceKind.BrokenStone),
            };
            Assert.AreEqual(6, spots.Count, string.Join(", ", spots));
            CollectionAssert.AreEqual(Sorted(expected), Sorted(spots));
        }

        [Test]
        public void SameMapGivesNoTraces()
        {
            Assert.IsEmpty(MapDiff.Compare(FloorOneMap.Glyphs, FloorOneMap.Glyphs));
            Assert.IsEmpty(MapDiff.Compare(SecondExpedition, SecondExpedition));
        }

        [Test]
        public void MissingPreviousMapGivesNoTraces()
        {
            Assert.IsEmpty(MapDiff.Compare(null, FloorOneMap.Glyphs));
            Assert.IsEmpty(MapDiff.Compare("", FloorOneMap.Glyphs));
            Assert.IsEmpty(MapDiff.Compare(FloorOneMap.Glyphs, null));
        }

        [Test]
        public void NoTraceAtCurrentPlankDoor()
        {
            // 열린 길이 판자벽이 되어도, 없던 자리에 판자벽이 생겨도 흔적을 두지 않는다(숨은 방을 들키지 않게).
            Assert.IsEmpty(MapDiff.Compare("A-B\n", "A:B\n"));
            Assert.IsEmpty(MapDiff.Compare("A.B\n", "A:B\n"));
            Assert.IsEmpty(MapDiff.Compare("A\n.\nB\n", "A\n:\nB\n"));
            // 문서 예시의 V:H 판자벽 자리(옛 D–S 열린 길)에도 흔적이 없다.
            var spots = MapDiff.Compare(FloorOneMap.Glyphs, SecondExpedition);
            Assert.IsFalse(spots.Any(s => s.Y == 1 && (s.X == 3 && s.Side == Side.Right || s.X == 4 && s.Side == Side.Left)));
        }

        [Test]
        public void BlockedOldPathWithBothCellsGivesTwoRubble()
        {
            var spots = MapDiff.Compare("A-B\n", "A.B\n");
            CollectionAssert.AreEqual(
                Sorted(new[] { new TraceSpot(0, 0, Side.Right, TraceKind.Rubble), new TraceSpot(1, 0, Side.Left, TraceKind.Rubble) }),
                Sorted(spots));
            // 금 간 벽이 없어져도 같다(세로 길).
            spots = MapDiff.Compare("A\n#\nB\n", "A\n.\nB\n");
            CollectionAssert.AreEqual(
                Sorted(new[] { new TraceSpot(0, 0, Side.Up, TraceKind.Rubble), new TraceSpot(0, 1, Side.Down, TraceKind.Rubble) }),
                Sorted(spots));
        }

        [Test]
        public void ShapeStairsAndDifference()
        {
            // 칸 글자만 다르면 같은 모양, 길 하나라도 다르면 다른 모양. 끝 빈 줄·빈칸은 보지 않는다.
            Assert.IsTrue(MapDiff.SameShape("E-A-S\n", "E-B-S\n"));
            Assert.IsTrue(MapDiff.SameShape("E-A-S\n..|\n..B\n", "E-C-S\n..|\n..D"));
            Assert.IsFalse(MapDiff.SameShape("E-A-S\n", "E-A#S\n"));
            Assert.IsFalse(MapDiff.SameShape(null, "E-A-S\n"));
            // 계단 칸은 글자 'S'(맨 아랫줄 y 0). 손 지도는 (4, 1).
            Assert.AreEqual((4, 1), MapDiff.StairsCell(FloorOneMap.Glyphs));
            Assert.AreEqual((2, 3), MapDiff.StairsCell(SecondExpedition));
            Assert.IsNull(MapDiff.StairsCell("E-A\n"));
            // 달라진 정도: 모양이 같으면 -1, 계단이 옮겨 가면 StairsMovedScore + 흔적 수.
            Assert.AreEqual(-1, MapDiff.Difference(FloorOneMap.Glyphs, FloorOneMap.Glyphs));
            Assert.AreEqual(MapDiff.StairsMovedScore + 6, MapDiff.Difference(FloorOneMap.Glyphs, SecondExpedition));
            Assert.AreEqual(2, MapDiff.Difference("E-A-S\n", "E.A-S\n"), "계단 그대로, 흙더미 2");
        }

        [Test]
        public void NewOpenPathsAndLockedDoors()
        {
            // 없던 길 → 갓 판 흙, 금 간 벽 → 열린 길 = 깨진 돌, 자물쇠는 어느 쪽이든 건너뜀, 범례 없는 글자도 읽는다.
            CollectionAssert.AreEqual(new[] { "FreshDig (0,0) Right" }, Sorted(MapDiff.Compare("P.Q\n", "P-Q\n")));
            CollectionAssert.AreEqual(new[] { "BrokenStone (0,0) Up" }, Sorted(MapDiff.Compare("P\n#\nQ\n", "P\n|\nQ\n")));
            Assert.IsEmpty(MapDiff.Compare("P=Q\n", "P.Q\n"));
            Assert.IsEmpty(MapDiff.Compare("P.Q\n", "P=Q\n"));
            Assert.IsEmpty(MapDiff.Compare("P:Q\n", "P-Q\n"));
            // 높이가 달라도 맨 아랫줄끼리 맞춘다: 위에 한 줄 더한 지도와 견주면 아래 길은 그대로라 흔적이 없다.
            Assert.IsEmpty(MapDiff.Compare("P-Q\n", "R\n.\nP-Q\n"));
        }
    }
}
