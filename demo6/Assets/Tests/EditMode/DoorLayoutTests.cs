using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demo6.Core.Dungeon;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 문 자리 비틀기(기획/시야와-문-1차.md 2-1~2-4, 11-1 시험 1~8): 한도 표, 늘 가운데인 문(돌방·판자벽·자물쇠), 범위·정수·문틈 벽 안,
    /// 자리 표시·기둥과 겹치지 않음, 같은 씨앗 같은 값·씨앗마다 다른 묶음, 퍼짐, 문 가운데 네 방향, 1층 손 지도 13개 길 표.
    /// 생성기는 고치지 않으므로 지도는 FloorGenerator가 지은 그대로 읽는다(지도를 다 지은 뒤 지도와 씨앗만 본다).
    /// </summary>
    public sealed class DoorLayoutTests
    {
        const float Eps = 1e-3f;
        const int SeedCount = 40;

        /// <summary>시험할 지도 하나(이름, 지도, 비틀기에 넘길 씨앗 = 실제로 지은 씨앗).</summary>
        sealed class Sample
        {
            public string Name;
            public FloorMap Map;
            public ulong Seed;
            /// <summary>1·2층 씨앗 1~40으로 지은 지도인가(퍼짐 시험 6이 센다).</summary>
            public bool Ranged;
        }

        List<Sample> _samples;

        /// <summary>1층 손 지도(씨앗 0), 2층 고른 씨앗, 1·2층 씨앗 1~40(첫 방문·다시 연 층), 오우거 굴을 한 번만 짓는다.</summary>
        [OneTimeSetUp]
        public void BuildSamples()
        {
            _samples = new List<Sample> { new Sample { Name = "1층 손 지도", Map = FloorOneMap.Build(), Seed = 0UL } };
            var recipe = FloorRecipe.For(2);
            var chosen = FloorGenerator.Generate(FirstVisit(2, recipe.ChosenSeed));
            _samples.Add(new Sample { Name = "2층 고른 씨앗 " + chosen.Seed, Map = chosen.Build(), Seed = chosen.Seed });
            for (int floor = 1; floor <= 2; floor++)
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                _samples.Add(Generated(FirstVisit(floor, (ulong)seed), $"{floor}층 첫 방문 씨앗 {seed}"));
                _samples.Add(Generated(Reopened(floor, seed), $"{floor}층 다시 연 층 씨앗 {seed}"));
            }
            var den = FloorGenerator.GenerateDen(OgreDen.Floor);
            _samples.Add(new Sample { Name = "오우거 굴", Map = den.Build(), Seed = den.Seed });
        }

        // ── 1. 한도 표 ──

        [Test]
        public void LimitTableFollowsDoc()
        {
            foreach (Side side in Enum.GetValues(typeof(Side)))
            {
                bool sideWall = side == Side.Left || side == Side.Right;
                float open = sideWall ? 3f : 5f;
                Assert.AreEqual(open, DoorLayout.Limit(PieceKind.Clearing, side), Eps, "공터 " + side);
                Assert.AreEqual(open, DoorLayout.Limit(PieceKind.StairsRoom, side), Eps, "계단 앞 " + side);
                foreach (var piece in new[] { PieceKind.Corridor, PieceKind.Room, PieceKind.Hidden })
                    Assert.AreEqual(1f, DoorLayout.Limit(piece, side), Eps, "통로·막다른 방·숨은 방 " + piece + " " + side);
                foreach (var piece in new[] { PieceKind.Entrance, PieceKind.Office, PieceKind.BossRoom, PieceKind.BossFront })
                    Assert.AreEqual(0f, DoorLayout.Limit(piece, side), Eps, "돌방 " + piece + " " + side);
            }

            Assert.AreEqual(3f, DoorLayout.SideWallLimit, Eps);
            Assert.AreEqual(5f, DoorLayout.EndWallLimit, Eps);
            Assert.AreEqual(1f, DoorLayout.NarrowLimit, Eps);
            Assert.AreEqual(4f, DoorLayout.DoorWidth, Eps);
            Assert.AreEqual(PieceSlots.DoorClearance, DoorLayout.Clearance, Eps, "그 밖 자리 표시 = PieceSlots.DoorClearance");

            // 길 한도 = 두 칸 한도 가운데 작은 값. 돌방에 닿음·판자벽·자물쇠는 0, 금 간 벽은 옮긴다.
            Assert.AreEqual(1f, DoorLayout.EdgeLimit(Edge(PieceKind.Clearing, PieceKind.Corridor, Side.Right, EdgeKind.Open)), Eps, "공터–통로");
            Assert.AreEqual(3f, DoorLayout.EdgeLimit(Edge(PieceKind.Clearing, PieceKind.Clearing, Side.Right, EdgeKind.Open)), Eps, "공터–공터 세로 벽");
            Assert.AreEqual(5f, DoorLayout.EdgeLimit(Edge(PieceKind.Clearing, PieceKind.Clearing, Side.Up, EdgeKind.Open)), Eps, "공터|공터 가로 벽");
            Assert.AreEqual(3f, DoorLayout.EdgeLimit(Edge(PieceKind.Clearing, PieceKind.StairsRoom, Side.Right, EdgeKind.Cracked)), Eps, "금 간 벽");
            Assert.AreEqual(1f, DoorLayout.EdgeLimit(Edge(PieceKind.Room, PieceKind.Clearing, Side.Up, EdgeKind.Cracked)), Eps, "막다른 방#공터");
            Assert.AreEqual(0f, DoorLayout.EdgeLimit(Edge(PieceKind.Entrance, PieceKind.Clearing, Side.Right, EdgeKind.Open)), Eps, "승강장");
            Assert.AreEqual(0f, DoorLayout.EdgeLimit(Edge(PieceKind.Clearing, PieceKind.Office, Side.Up, EdgeKind.Open)), Eps, "사무실");
            Assert.AreEqual(0f, DoorLayout.EdgeLimit(Edge(PieceKind.BossFront, PieceKind.BossRoom, Side.Right, EdgeKind.Open)), Eps, "굴");
            Assert.AreEqual(0f, DoorLayout.EdgeLimit(Edge(PieceKind.Hidden, PieceKind.Corridor, Side.Up, EdgeKind.Plank)), Eps, "판자벽");
            Assert.AreEqual(0f, DoorLayout.EdgeLimit(Edge(PieceKind.Clearing, PieceKind.Clearing, Side.Right, EdgeKind.Locked)), Eps, "자물쇠 문");

            Assert.IsTrue(DoorLayout.Pinned(null));
            Assert.AreEqual(0f, DoorLayout.EdgeLimit(null), Eps);
            Assert.AreEqual(0f, DoorLayout.Shift(null, null, 5UL), Eps);
            Assert.IsEmpty(DoorLayout.ShiftAll(null, 1UL));
        }

        // ── 2. 늘 가운데 ──

        [Test]
        public void StoneRoomsPlanksAndLocksStayCentered()
        {
            int pinned = 0, stone = 0, plank = 0, locked = 0;
            foreach (var s in _samples)
            {
                var shifts = DoorLayout.ShiftAll(s.Map, s.Seed);
                Assert.AreEqual(s.Map.Edges.Count, shifts.Length, s.Name + ": map.Edges 차례 배열");
                Assert.IsTrue(s.Map.Edges.Any(e => MapAnchors.IsFixed(e.A) || MapAnchors.IsFixed(e.B)), s.Name + ": 돌방에 닿은 길이 하나는 있다");
                for (int i = 0; i < shifts.Length; i++)
                {
                    var e = s.Map.Edges[i];
                    bool atStone = MapAnchors.IsFixed(e.A) || MapAnchors.IsFixed(e.B);
                    bool want = atStone || e.Kind == EdgeKind.Plank || e.Kind == EdgeKind.Locked;
                    Assert.AreEqual(want, DoorLayout.Pinned(e), $"{s.Name}: {Describe(e)}");
                    if (!want) continue;
                    pinned++;
                    if (atStone) stone++;
                    if (e.Kind == EdgeKind.Plank) plank++;
                    if (e.Kind == EdgeKind.Locked) locked++;
                    Assert.AreEqual(0f, DoorLayout.EdgeLimit(e), Eps, $"{s.Name}: {Describe(e)}");
                    Assert.AreEqual(0f, shifts[i], Eps, $"{s.Name}: 늘 가운데인 문이 비틀림 {Describe(e)}");
                }
            }
            Assert.Greater(stone, 0, "돌방에 닿은 길");
            Assert.Greater(plank, 0, "판자벽");
            Assert.Greater(locked, 0, "자물쇠 문");
            TestContext.WriteLine($"늘 가운데 {pinned}개(돌방 {stone}, 판자벽 {plank}, 자물쇠 {locked})");

            var den = _samples.Single(x => x.Name == "오우거 굴");
            Assert.AreEqual(1, den.Map.Edges.Count);
            Assert.AreEqual(0f, DoorLayout.ShiftAll(den.Map, 12345UL)[0], Eps, "굴은 씨앗과 관계없이 가운데");
        }

        // ── 3. 범위·정수·문틈 벽 안 ──

        [Test]
        public void ShiftsAreWholeWithinLimitAndInsideWall()
        {
            foreach (var s in _samples)
            {
                var shifts = DoorLayout.ShiftAll(s.Map, s.Seed);
                for (int i = 0; i < shifts.Length; i++)
                {
                    var e = s.Map.Edges[i];
                    float shift = shifts[i];
                    string at = $"{s.Name}: {Describe(e)} 비틀기 {shift}";
                    Assert.LessOrEqual(Math.Abs(shift), DoorLayout.EdgeLimit(e) + Eps, at + ": 한도 밖");
                    Assert.AreEqual(Math.Round(shift), shift, 0.0, at + ": 정수가 아님");
                    // 오른쪽 길은 세로 벽(안쪽 y ±7.5), 위쪽 길은 가로 벽(안쪽 x ±13.5). 모서리 여유 1.5.
                    float half = e.SideFromA == Side.Right ? 7.5f : 13.5f;
                    Assert.LessOrEqual(Math.Abs(shift) + DoorLayout.DoorWidth * 0.5f, half - DoorLayout.CornerMargin + Eps, at + ": 문틈이 벽 밖");
                }
            }

            // 한도는 늘 벽 안이다(세로 벽 |s| ≤ 4, 가로 벽 |s| ≤ 10).
            Assert.IsTrue(DoorLayout.InWall(Side.Right, DoorLayout.SideWallLimit));
            Assert.IsTrue(DoorLayout.InWall(Side.Left, -4f));
            Assert.IsFalse(DoorLayout.InWall(Side.Right, 4.5f));
            Assert.IsTrue(DoorLayout.InWall(Side.Up, DoorLayout.EndWallLimit));
            Assert.IsTrue(DoorLayout.InWall(Side.Down, -10f));
            Assert.IsFalse(DoorLayout.InWall(Side.Up, 10.5f));
            Assert.IsFalse(DoorLayout.Clear(null, Side.Left, 5f), "벽 밖 후보는 버린다");
            Assert.IsTrue(DoorLayout.Clear(null, Side.Down, 5f));
        }

        // ── 4. 겹침 ──

        [Test]
        public void ShiftedDoorsKeepClearOfFeaturesAndPillars()
        {
            int checkedDoors = 0;
            foreach (var s in _samples)
            {
                var shifts = DoorLayout.ShiftAll(s.Map, s.Seed);
                for (int i = 0; i < shifts.Length; i++)
                {
                    var e = s.Map.Edges[i];
                    if (DoorLayout.EdgeLimit(e) < 1f) continue;
                    string at = $"{s.Name}: {Describe(e)} 비틀기 {shifts[i]}";
                    AssertSideClear(e.A, e.SideFromA, shifts[i], at);
                    AssertSideClear(e.B, MapEdge.Opposite(e.SideFromA), shifts[i], at);
                    checkedDoors++;
                }
            }
            Assert.Greater(checkedDoors, 0);
        }

        [Test]
        public void ClearUsesDocDistances()
        {
            // 오른쪽 문 가운데 (14, 0)에서 거리만 다르게 둔 자리 표시 하나씩. 기둥 벌 4(−5, 2.5)·(5, 2.5)는 문에서 멀다.
            AssertClearAt(FeatureKind.Group, 4f);
            AssertClearAt(FeatureKind.Nest, 4f);
            AssertClearAt(FeatureKind.Chalk, 1.5f);
            AssertClearAt(FeatureKind.Scrawl, 1.5f);
            AssertClearAt(FeatureKind.WoodChest, 3f);
            AssertClearAt(FeatureKind.WallLamp, 3f);
            AssertClearAt(FeatureKind.Stake, 3f);
            AssertClearAt(FeatureKind.Boss, 3f);
            Assert.AreEqual(4f, DoorLayout.ClearanceFor(FeatureKind.Group), Eps);
            Assert.AreEqual(1.5f, DoorLayout.ClearanceFor(FeatureKind.Chalk), Eps);
            Assert.AreEqual(3f, DoorLayout.ClearanceFor(FeatureKind.Ore), Eps);

            // 공터 옆벽 등잔 (13, 4.5): 오른쪽 문 +3·+2는 등잔 위로 지나가 버리고, +1은 통과(2-4 '벽 등잔은 벽 면에 붙어 있어').
            var lamp = Cell(PieceKind.Clearing, 4, Feature(FeatureKind.WallLamp, 13f, 4.5f));
            Assert.IsFalse(DoorLayout.Clear(lamp, Side.Right, 3f));
            Assert.IsFalse(DoorLayout.Clear(lamp, Side.Right, 2f));
            Assert.IsTrue(DoorLayout.Clear(lamp, Side.Right, 1f));
            Assert.IsTrue(DoorLayout.Clear(lamp, Side.Right, -3f));

            // 기둥 검사: 모든 조각·기둥 벌·방향·벽 안 후보에서 Clear(빈 칸) = 기둥 2.5 밖.
            foreach (PieceKind piece in Enum.GetValues(typeof(PieceKind)))
            for (int set = -1; set < PieceSlots.PillarSetCount; set++)
            {
                if (piece != PieceKind.Clearing && set != -1) continue;
                var cell = Cell(piece, set);
                foreach (Side side in Enum.GetValues(typeof(Side)))
                for (int shift = -10; shift <= 10; shift++)
                {
                    if (!DoorLayout.InWall(side, shift)) continue;
                    var door = DoorLayout.DoorCenter(side, shift);
                    bool want = PieceSlots.PillarsIn(piece, set).All(p => Distance(p, door) >= DoorLayout.PillarClearance - Eps);
                    Assert.AreEqual(want, DoorLayout.Clear(cell, side, shift), $"{piece} 기둥 {set} {side} {shift}");
                }
            }
        }

        // ── 5. 같은 씨앗 같은 값, 씨앗마다 다른 묶음 ──

        [Test]
        public void SameSeedSameShiftsAndSeedsVary()
        {
            foreach (var s in _samples)
                CollectionAssert.AreEqual(DoorLayout.ShiftAll(s.Map, s.Seed), DoorLayout.ShiftAll(s.Map, s.Seed), s.Name);
            CollectionAssert.AreEqual(DoorLayout.ShiftAll(FloorOneMap.Build(), 0UL), DoorLayout.ShiftAll(FloorOneMap.Build(), 0UL), "손 지도는 늘 같은 모양으로 비튼다");

            for (int floor = 1; floor <= 2; floor++)
            {
                var patterns = new HashSet<string>();
                for (int seed = 1; seed <= 20; seed++)
                {
                    var g = FloorGenerator.Generate(FirstVisit(floor, (ulong)seed));
                    patterns.Add(g.Glyphs + "/" + Join(DoorLayout.ShiftAll(g.Build(), g.Seed)));
                }
                Assert.GreaterOrEqual(patterns.Count, 2, $"{floor}층 씨앗 20개");
            }

            // 지도가 같아도 씨앗이 다르면 문 자리가 바뀐다(다시 연 층).
            var hand = FloorOneMap.Build();
            var handPatterns = new HashSet<string>();
            for (ulong seed = 0; seed < 20; seed++) handPatterns.Add(Join(DoorLayout.ShiftAll(hand, seed)));
            Assert.GreaterOrEqual(handPatterns.Count, 2, "손 지도 씨앗 20개");
        }

        // ── 6. 퍼짐 ──

        [Test]
        public void MostMovableDoorsMove()
        {
            int movable = 0, moved = 0;
            foreach (var s in _samples.Where(x => x.Ranged))
            {
                var shifts = DoorLayout.ShiftAll(s.Map, s.Seed);
                for (int i = 0; i < shifts.Length; i++)
                {
                    if (DoorLayout.EdgeLimit(s.Map.Edges[i]) < 1f) continue;
                    movable++;
                    if (shifts[i] != 0f) moved++;
                }
            }
            TestContext.WriteLine($"1·2층 씨앗 1~{SeedCount}: 한도 1 이상 길 {movable}개 가운데 비틀린 길 {moved}개({(movable > 0 ? moved * 100 / movable : 0)}%)");
            Assert.Greater(movable, 0);
            Assert.GreaterOrEqual(moved * 100, movable * 40, $"{moved}/{movable}: 0이 아닌 몫 40% 이상");
        }

        // ── 7. 문 가운데 ──

        [Test]
        public void DoorCenterFollowsSides()
        {
            AssertOffset(14f, 2f, DoorLayout.DoorCenter(Side.Right, 2f), "오른쪽");
            AssertOffset(2f, 8f, DoorLayout.DoorCenter(Side.Up, 2f), "위");
            AssertOffset(-14f, 2f, DoorLayout.DoorCenter(Side.Left, 2f), "왼쪽");
            AssertOffset(2f, -8f, DoorLayout.DoorCenter(Side.Down, 2f), "아래");
            AssertOffset(14f, -3f, DoorLayout.DoorCenter(Side.Right, -3f), "오른쪽 −3");
            AssertOffset(-5f, -8f, DoorLayout.DoorCenter(Side.Down, -5f), "아래 −5");
            foreach (Side side in Enum.GetValues(typeof(Side)))
            {
                var zero = PieceSlots.DoorCenter(side);
                AssertOffset(zero.X, zero.Y, DoorLayout.DoorCenter(side, 0f), "0이면 지금 문 가운데 " + side);
            }
            // 한 길의 비틀기를 양쪽 칸이 함께 쓴다: 오른쪽 길의 A 오른쪽 문과 B 왼쪽 문은 같은 y, 위쪽 길의 A 위 문과 B 아래 문은 같은 x.
            Assert.AreEqual(DoorLayout.DoorCenter(Side.Right, 3f).Y, DoorLayout.DoorCenter(Side.Left, 3f).Y, Eps);
            Assert.AreEqual(DoorLayout.DoorCenter(Side.Up, -4f).X, DoorLayout.DoorCenter(Side.Down, -4f).X, Eps);
        }

        // ── 8. 1층 손 지도 표 ──

        [Test]
        public void FloorOneHandMapTable()
        {
            var map = FloorOneMap.Build();
            var shifts = DoorLayout.ShiftAll(map, 0UL);
            Assert.AreEqual(13, map.Edges.Count, "1층 손 지도 길 13개");
            int pinned = 0, narrow = 0, wide = 0;
            var sb = new StringBuilder("1층 손 지도 문 비틀기(씨앗 0, 오른쪽 길 +y · 위쪽 길 +x)\n");
            for (int i = 0; i < shifts.Length; i++)
            {
                var e = map.Edges[i];
                float limit = DoorLayout.EdgeLimit(e);
                if (limit <= 0f) pinned++;
                else if (limit <= 1f) narrow++;
                else wide++;
                sb.Append(Describe(e)).Append(" · 한도 ±").Append(limit).Append(" · 비틀기 ").Append(shifts[i]).Append('\n');
            }
            string table = sb.ToString();
            TestContext.WriteLine(table);
            // 표 값을 실제로 맞춘다(반박 검토: TestContext 글은 CLI 결과에 나오지 않아 찍기만 해서는 소금·손 지도가 바뀌어도 통과했다).
            // 고정 소금은 크게 옮길 수 있는 공터 문 셋(c1|A −3, c1-B −2, T|c1 +3)이 모두 비켜 서도록 고른 값이다(11-2 확인 1).
            string redo = "\n" + table + "손 지도 범례·자리 표시나 DoorLayout 소금을 일부러 바꿨으면 이 표(ExpectedHandShifts)를 새로 고친다.";
            CollectionAssert.AreEqual(ExpectedHandShifts, shifts, "1층 손 지도 13개 길 비틀기(map.Edges 차례)" + redo);
            Assert.AreEqual(3, pinned, "늘 가운데 3개(승강장–첫 공터, 사무실=쥐굴 공터, 분필 갈림길:숨은 방)");
            Assert.AreEqual(7, narrow, "±1 7개");
            Assert.AreEqual(3, wide, "크게(±3·±5) 3개");

            var landing = MapAnchors.FindLanding(map);
            var c1 = map.Find("c1");
            int first = IndexOf(map, landing.EdgeTo(c1));
            Assert.AreEqual(0f, shifts[first], Eps, "첫 기습을 배우는 승강장–첫 공터 문은 가운데");
            Assert.AreEqual(3f, DoorLayout.EdgeLimit(c1.EdgeTo(map.Find("B"))), Eps, "첫 공터–갈림 공터 ±3");
            Assert.AreEqual(5f, DoorLayout.EdgeLimit(c1.EdgeTo(map.Find("A"))), Eps, "첫 공터|쥐굴 공터 ±5");
            Assert.AreEqual(5f, DoorLayout.EdgeLimit(c1.EdgeTo(map.Find("T"))), Eps, "막다른 공터|첫 공터 ±5");
            Assert.AreEqual(0f, shifts[IndexOf(map, map.Find("O").EdgeTo(map.Find("A")))], Eps, "자물쇠 문");
            Assert.AreEqual(0f, shifts[IndexOf(map, map.Find("D").EdgeTo(map.Find("H")))], Eps, "판자벽");
            // 크게 옮기는 공터 문 셋은 가운데(0)로 돌아오지 않는다(손 지도에서 '공터끼리 문은 눈에 띄게 비켜 있다', 11-2 확인 1).
            foreach (var other in new[] { "A", "B", "T" })
            {
                var wideEdge = c1.EdgeTo(map.Find(other));
                Assert.AreNotEqual(0f, shifts[IndexOf(map, wideEdge)], $"크게 옮기는 공터 문이 가운데로 돌아옴: {Describe(wideEdge)}{redo}");
            }
        }

        /// <summary>
        /// 1층 손 지도(씨앗 0)의 문 비틀기 13개(map.Edges 차례, 2026-10-06 맞추기에서 Unity로 찍은 값).
        /// 차례: O=A 0 · A-c2 +1 · c2-V 0 · c1|A −3 · B|c2 −1 · D#V −1 · E-c1 0 · c1-B −2 · B-D −1 · D-S −1 · T|c1 +3 · K#B +1 · H:D 0.
        /// </summary>
        static readonly float[] ExpectedHandShifts = { 0f, 1f, 0f, -3f, -1f, -1f, 0f, -2f, -1f, -1f, 3f, 1f, 0f };

        // ── 도우미 ──

        static GeneratorInput FirstVisit(int floor, ulong seed) =>
            new GeneratorInput { Floor = floor, Seed = seed, FirstVisit = true, DeepestFloor = floor };

        /// <summary>다시 연 층 입력(곡괭이·밤 사건·받은 것을 씨앗마다 섞음, FloorGeneratorTests와 같은 꼴).</summary>
        static GeneratorInput Reopened(int floor, int seed) => new GeneratorInput
        {
            Floor = floor,
            Seed = (ulong)seed * 2654435761UL,
            FirstVisit = false,
            DeepestFloor = FloorRecipe.MaxTestFloor,
            HasPickaxe = seed % 2 == 0,
            OnceDone = seed % 2 == 0 ? new[] { "f1.H.pickaxe", "f1.H.iron" } : Array.Empty<string>(),
            Night = (NightEvent)(seed % 5),
        };

        static Sample Generated(GeneratorInput input, string name)
        {
            var g = FloorGenerator.Generate(input);
            return new Sample
            {
                Name = g.Seed != input.Seed ? $"{name}(지은 씨앗 {g.Seed})" : name,
                Map = g.Build(),
                Seed = g.Seed,
                Ranged = true,
            };
        }

        static MapCell Cell(PieceKind piece, int pillars, params CellFeature[] features) => new MapCell
        {
            Def = new CellDef { Glyph = 'Z', Id = "Z", Name = "시험 칸", Piece = piece, Pillars = pillars, Features = features },
        };

        static CellFeature Feature(FeatureKind kind, float x, float y) =>
            new CellFeature { Kind = kind, Id = "t." + kind, Local = new Offset(x, y) };

        static MapEdge Edge(PieceKind a, PieceKind b, Side sideFromA, EdgeKind kind)
        {
            var ca = Cell(a, -1);
            var cb = Cell(b, -1);
            cb.X = sideFromA == Side.Right ? 1 : 0;
            cb.Y = sideFromA == Side.Up ? 1 : 0;
            var e = new MapEdge { A = ca, B = cb, Kind = kind, SideFromA = sideFromA };
            ca.Edges.Add(e);
            cb.Edges.Add(e);
            return e;
        }

        /// <summary>오른쪽 문 가운데 (14, 0)에서 clearance보다 0.1 가까우면 버리고 0.1 멀면 통과.</summary>
        static void AssertClearAt(FeatureKind kind, float clearance)
        {
            var near = Cell(PieceKind.Clearing, 4, Feature(kind, 14f - (clearance - 0.1f), 0f));
            var far = Cell(PieceKind.Clearing, 4, Feature(kind, 14f - (clearance + 0.1f), 0f));
            Assert.IsFalse(DoorLayout.Clear(near, Side.Right, 0f), kind + " " + (clearance - 0.1f));
            Assert.IsTrue(DoorLayout.Clear(far, Side.Right, 0f), kind + " " + (clearance + 0.1f));
            // 문을 옮기면 같은 자리 표시를 비켜 간다.
            Assert.IsTrue(DoorLayout.Clear(near, Side.Right, 3f), kind + " 비틀면 비켜 감");
        }

        /// <summary>2-4 표의 거리를 따로 적어 맞춰 본다: 무리·둥지 4, 분필·긁은 글 1.5, 그 밖 3, 조각 기둥 2.5.</summary>
        static void AssertSideClear(MapCell cell, Side side, float shift, string at)
        {
            var door = DoorLayout.DoorCenter(side, shift);
            foreach (var f in cell.Features)
            {
                float need = f.Kind == FeatureKind.Group || f.Kind == FeatureKind.Nest ? 4f
                    : f.Kind == FeatureKind.Chalk || f.Kind == FeatureKind.Scrawl ? 1.5f
                    : 3f;
                Assert.GreaterOrEqual(Distance(f.Local, door), need - Eps, $"{at}: {cell.Id} {side} 문이 {f.Kind} ({f.Local.X}, {f.Local.Y})와 겹침");
            }
            foreach (var p in PieceSlots.PillarsIn(cell.Piece, cell.Def.Pillars))
                Assert.GreaterOrEqual(Distance(p, door), 2.5f - Eps, $"{at}: {cell.Id} {side} 문이 기둥 ({p.X}, {p.Y})와 겹침");
        }

        static void AssertOffset(float x, float y, Offset actual, string what)
        {
            Assert.AreEqual(x, actual.X, Eps, what + " x");
            Assert.AreEqual(y, actual.Y, Eps, what + " y");
        }

        static float Distance(Offset a, Offset b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        static int IndexOf(FloorMap map, MapEdge edge)
        {
            for (int i = 0; i < map.Edges.Count; i++)
                if (map.Edges[i] == edge) return i;
            Assert.Fail("길을 찾지 못함");
            return -1;
        }

        static string Join(float[] shifts) => string.Join(",", shifts.Select(s => ((int)s).ToString()));

        /// <summary>길 한 줄: 'A칸 이름(id) 글자 B칸 이름(id)'. 글자 '-' '|' 열린 길, ':' 판자벽, '#' 금 간 벽, '=' 자물쇠 문.</summary>
        static string Describe(MapEdge e)
        {
            char glyph;
            switch (e.Kind)
            {
                case EdgeKind.Plank: glyph = ':'; break;
                case EdgeKind.Cracked: glyph = '#'; break;
                case EdgeKind.Locked: glyph = '='; break;
                default: glyph = e.SideFromA == Side.Right ? '-' : '|'; break;
            }
            return $"{e.A.Def.Name}({e.A.Id}) {glyph} {e.B.Def.Name}({e.B.Id})";
        }
    }
}
