using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Demo6.Core.Dungeon;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 층 생성기(매판 새 탐험 1차 2-5, 5장 단계 1·4): 1층 씨앗 0 = 손 지도, 같은 입력 같은 지도, 씨앗 500개 모두 합격·고른 지도로 대신 2% 미만·1회 50ms 미만,
    /// 받은 한 번짜리는 다시 안 나옴, 2층 첫 공터 궁수, 분필이 판자벽 문을 가리킴, 2층 고른 씨앗 합격, 어떤 입력에도 예외 없음, 조각 슬롯이 길 위에 있음,
    /// 계단·승강장 출구·숨은 방이 한 자리에 몰리지 않음, 연이은 원정은 계단 칸이 옮겨 가고 흔적이 남음.
    /// </summary>
    public sealed class FloorGeneratorTests
    {
        const int SeedCount = 500;

        /// <summary>2층 고른 지도(FloorRecipe 2층 ChosenSeed). 생성기를 고쳐 이 지도가 바뀌면 씨앗을 다시 골라 여기와 층 예산 표를 함께 고친다.</summary>
        const string ChosenFloorTwo =
            "....A\n" +
            "....#\n" +
            "E-B-C-S\n" +
            "..|.|.#\n" +
            "H:D-F-G\n";

        [Test]
        public void FloorOneSeedZeroIsTheHandMap()
        {
            var g = FloorGenerator.Generate(new GeneratorInput { Floor = 1, Seed = 0 });
            Assert.IsTrue(g.HandMap);
            Assert.IsFalse(g.FellBack);
            Assert.AreEqual(FloorOneMap.Glyphs, g.Glyphs);
            Assert.AreEqual(Signature(FloorOneMap.Legend()), Signature(g.Legend));
            Assert.IsTrue(g.Report.Passed, g.Report.ToString());
        }

        [Test]
        public void SameInputMakesSameMap()
        {
            for (int floor = 1; floor <= FloorRecipe.MaxTestFloor; floor++)
            for (int seed = 1; seed <= 40; seed++)
            {
                var a = FloorGenerator.Generate(Input(floor, seed));
                var b = FloorGenerator.Generate(Input(floor, seed));
                Assert.AreEqual(a.Glyphs, b.Glyphs, $"{floor}층 씨앗 {seed}");
                Assert.AreEqual(a.Seed, b.Seed);
                Assert.AreEqual(Signature(a.Legend), Signature(b.Legend), $"{floor}층 씨앗 {seed}");
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        public void FiveHundredSeedsPassQuickly(int floor)
        {
            var recipe = FloorRecipe.For(floor);
            // 처음 부를 때의 JIT를 재지 않으려고 한 번 덥힌다.
            FloorGenerator.Generate(Input(floor, 999999));
            int fellBack = 0;
            double slowest = 0;
            var watch = new Stopwatch();
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                var input = Input(floor, seed);
                watch.Restart();
                var g = FloorGenerator.Generate(input);
                watch.Stop();
                slowest = Math.Max(slowest, watch.Elapsed.TotalMilliseconds);
                if (g.FellBack) fellBack++;
                var report = FloorRules.Check(g.Build(), recipe, input);
                Assert.IsTrue(report.Passed, $"{floor}층 씨앗 {seed}\n{report}\n{g.Glyphs}");
            }
            Assert.Less(fellBack * 50, SeedCount, $"고른 지도로 대신 {fellBack}번(2% 미만)");
            Assert.Less(slowest, 50.0, "1회 50ms 미만");
        }

        [TestCase(1)]
        [TestCase(2)]
        public void DeepDecayAndEveryNightStillPass(int floor)
        {
            // 최고 − 3 층을 다시 연 경우(나무 궤짝 1, 광맥 0, 강한 적 1마리 이하)와 밤 사건 넷을 모두 돈다.
            var recipe = FloorRecipe.For(floor);
            var everything = FloorRecipe.AllOnceItems().Select(o => o.Id).ToArray();
            int fellBack = 0;
            for (int seed = 1; seed <= 200; seed++)
            {
                var input = new GeneratorInput
                {
                    Floor = floor, Seed = (ulong)seed * 7919UL, FirstVisit = false, DeepestFloor = floor + 3, HasPickaxe = true,
                    OnceDone = seed % 2 == 0 ? everything : Array.Empty<string>(), Night = (NightEvent)(seed % 5),
                };
                var g = FloorGenerator.Generate(input);
                if (g.FellBack) fellBack++;
                var report = FloorRules.Check(g.Build(), recipe, input);
                Assert.IsTrue(report.Passed, $"{floor}층 씨앗 {input.Seed}\n{report}\n{g.Glyphs}");
                foreach (var f in g.Legend.SelectMany(d => d.Features).Where(f => f.Kind == FeatureKind.Group))
                    Assert.LessOrEqual(f.Boars + f.Archers, 1, "최고 − 3 이하 층은 무리마다 강한 적 1마리 이하");
            }
            Assert.Less(fellBack * 50, 200);
        }

        [Test]
        public void ReopenedFloorOneDropsReceivedPickaxeAndGuaranteedChest()
        {
            var done = new[] { "f1.H.pickaxe", "f1.H.iron" };
            for (int seed = 1; seed <= 100; seed++)
            {
                var g = FloorGenerator.Generate(new GeneratorInput
                    { Floor = 1, Seed = (ulong)seed, FirstVisit = false, DeepestFloor = 2, HasPickaxe = true, OnceDone = done });
                var features = g.Legend.SelectMany(d => d.Features).ToList();
                Assert.IsFalse(features.Any(f => f.Kind == FeatureKind.Pickaxe), $"씨앗 {seed}: 받은 곡괭이가 다시 나옴");
                Assert.IsFalse(features.Any(f => done.Contains(f.Id)), $"씨앗 {seed}: 받은 id가 다시 나옴");
                var hidden = g.Legend.Single(d => d.Piece == PieceKind.Hidden);
                Assert.IsTrue(hidden.Features.Any(f => f.Kind == FeatureKind.WoodChest), $"씨앗 {seed}: 다 받은 숨은 방은 다시 연 층에서 나무 궤짝");

                // 아직 안 받았으면 어느 칸에 숨은 방이 생기든 같은 id로 다시 나온다.
                var fresh = FloorGenerator.Generate(new GeneratorInput { Floor = 1, Seed = (ulong)seed, FirstVisit = false, DeepestFloor = 2 });
                var freshHidden = fresh.Legend.Single(d => d.Piece == PieceKind.Hidden);
                Assert.IsTrue(freshHidden.Features.Any(f => f.Id == "f1.H.pickaxe" && f.Kind == FeatureKind.Pickaxe), $"씨앗 {seed}");
                Assert.IsTrue(freshHidden.Features.Any(f => f.Id == "f1.H.iron" && f.Kind == FeatureKind.IronChest && f.Param == "rare-weapon"), $"씨앗 {seed}");
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        public void FirstClearingSleepsFacingAwayFromLanding(int floor)
        {
            var firstMix = FloorRecipe.For(floor).Groups.First(m => m.FirstClearing);
            for (int seed = 1; seed <= 100; seed++)
            {
                var map = FloorGenerator.Generate(Input(floor, seed)).Build();
                var main = FloorRules.MainPath(map);
                var landing = main[0];
                var first = main[1];
                Assert.AreEqual(1, landing.Edges.Count, $"씨앗 {seed}: 승강장 문은 하나");
                Assert.AreEqual(PieceKind.Clearing, first.Piece, $"씨앗 {seed}");
                var group = first.Features.Single(f => f.Kind == FeatureKind.Group);
                Assert.AreEqual(firstMix.Archers, group.Archers, $"씨앗 {seed}");
                Assert.AreEqual(firstMix.Boars, group.Boars, $"씨앗 {seed}");
                Assert.AreEqual(GroupState.Sleep, group.State, $"씨앗 {seed}");
                // 승강장에서 첫 칸으로 가는 방향을 본다 = 승강장을 등진다.
                var away = landing.EdgeTo(first).SideFrom(landing);
                Assert.AreEqual((int)away * 90f, group.FacingDeg, $"씨앗 {seed}");
            }
            if (floor == 2) Assert.Greater(firstMix.Archers, 0, "2층은 궁수 첫 공터");
        }

        [Test]
        public void ChalkPointsAtThePlankDoor()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                var map = FloorGenerator.Generate(Input(1, seed)).Build();
                var hidden = MapAnchors.FindHidden(map);
                Assert.AreEqual(1, hidden.Edges.Count, $"씨앗 {seed}");
                var plank = hidden.Edges[0];
                Assert.AreEqual(EdgeKind.Plank, plank.Kind, $"씨앗 {seed}");
                var cell = plank.Other(hidden);
                var door = DoorCenter(plank.SideFrom(cell));
                var chalks = map.Cells.SelectMany(c => c.Features).Where(f => f.Kind == FeatureKind.Chalk).ToList();
                Assert.AreEqual(3, chalks.Count, $"씨앗 {seed}");
                Assert.AreEqual(3, cell.Features.Count(f => f.Kind == FeatureKind.Chalk), $"씨앗 {seed}: 분필은 숨은 방 이웃 칸에");
                foreach (var f in chalks)
                {
                    double dx = door[0] - f.Local.X, dy = door[1] - f.Local.Y;
                    Assert.That(Math.Sqrt(dx * dx + dy * dy), Is.InRange(3.0, 6.0), $"씨앗 {seed} {f.Id}: 판자벽 문에서 3~6유닛");
                    double want = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                    double got = double.Parse(f.Param, CultureInfo.InvariantCulture);
                    double diff = Math.Abs(((got - want) % 360.0 + 540.0) % 360.0 - 180.0);
                    Assert.LessOrEqual(diff, 10.0, $"씨앗 {seed} {f.Id}: {got}° (문 쪽 {want:F1}°)");
                }
            }
        }

        [Test]
        public void FloorTwoChosenSeedIsAPassingFixedMap()
        {
            var recipe = FloorRecipe.For(2);
            Assert.AreNotEqual(0UL, recipe.ChosenSeed);
            var input = new GeneratorInput { Floor = 2, Seed = recipe.ChosenSeed, FirstVisit = true, DeepestFloor = 2, HasPickaxe = true };
            var g = FloorGenerator.Generate(input);
            Assert.IsFalse(g.FellBack);
            Assert.AreEqual(1, g.Attempts, "고른 씨앗은 첫 굴림에 합격해야 한다");
            Assert.AreEqual(recipe.ChosenSeed, g.Seed);
            Assert.IsTrue(FloorRules.Check(g.Build(), recipe).Passed, g.Describe());
            Assert.AreEqual(ChosenFloorTwo, g.Glyphs, g.Describe());
            // 층 첫 방문 지도는 가진 능력·밤 사건·최고 도달 층과 상관없이 같다(첫 탐험 시간표·경제 기준).
            var others = new[]
            {
                new GeneratorInput { Floor = 2, Seed = recipe.ChosenSeed, FirstVisit = true, DeepestFloor = 1 },
                new GeneratorInput { Floor = 2, Seed = recipe.ChosenSeed, FirstVisit = true, DeepestFloor = 2, Night = NightEvent.RatBurrow, HasKey = true },
                new GeneratorInput { Floor = 2, Seed = recipe.ChosenSeed, FirstVisit = true, DeepestFloor = 5, OnceDone = new[] { "f1.H.pickaxe" } },
            };
            foreach (var other in others)
            {
                var o = FloorGenerator.Generate(other);
                Assert.AreEqual(g.Glyphs, o.Glyphs);
                Assert.AreEqual(Signature(g.Legend), Signature(o.Legend));
            }
        }

        [Test]
        public void AnchorsStayFixed()
        {
            var handOffice = FloorOneMap.Build().Find("O");
            for (int seed = 1; seed <= 60; seed++)
            {
                var map = FloorGenerator.Generate(Input(1, seed)).Build();
                var landing = MapAnchors.FindLanding(map);
                Assert.AreEqual("E", landing.Id);
                Assert.AreEqual(0, landing.X);
                Assert.AreEqual(1, landing.Y);
                Assert.AreEqual(StakeRole.Landing, MapAnchors.RoleOf(landing));
                var stake = MapAnchors.LandingStake(map);
                Assert.AreEqual("f1.E.stake", stake.Id);
                Assert.AreEqual(-8f, stake.Local.X);
                Assert.AreEqual(-2f, stake.Local.Y);

                var office = MapAnchors.FindLandmark(map);
                Assert.AreEqual("O", office.Id, $"씨앗 {seed}");
                Assert.AreEqual(0, office.X);
                Assert.AreEqual(2, office.Y);
                Assert.AreEqual(EdgeKind.Locked, office.Edges.Single().Kind);
                Assert.AreNotEqual(landing, office.Edges.Single().Other(office), "자물쇠 문은 승강장이 아닌 이웃 칸으로");
                Assert.AreEqual(Side.Right, office.Edges.Single().SideFrom(office), $"씨앗 {seed}: 사무실 자물쇠 문은 손 지도처럼 늘 오른쪽(돌로 지은 방)");
                CollectionAssert.AreEqual(handOffice.Features.Select(f => f.Id + "@" + f.Local.X + "," + f.Local.Y).ToArray(),
                    office.Features.Select(f => f.Id + "@" + f.Local.X + "," + f.Local.Y).ToArray());

                var stairs = MapAnchors.FindStairsCell(map);
                Assert.AreEqual("S", stairs.Id);
                Assert.AreEqual(StakeRole.StairsFront, MapAnchors.RoleOf(stairs));
                Assert.AreEqual("f1.S.stake", MapAnchors.StakeIn(stairs).Id);
                Assert.IsTrue(stairs.Features.Any(f => f.Kind == FeatureKind.Stairs && f.Label == "2층 계단"));
                Assert.GreaterOrEqual(Math.Abs(stairs.X - landing.X) + Math.Abs(stairs.Y - landing.Y), 3, "계단은 승강장에서 격자 거리 3 이상");
            }
        }

        [Test]
        public void RatBurrowNightPutsTheExtraNestInACorridor()
        {
            int inCorridor = 0, total = 0;
            for (int seed = 1; seed <= 60; seed++)
            {
                var g = FloorGenerator.Generate(new GeneratorInput { Floor = 2, Seed = (ulong)seed, FirstVisit = false, DeepestFloor = 2, Night = NightEvent.RatBurrow });
                var nests = g.Legend.Where(d => d.Features.Any(f => f.Kind == FeatureKind.Nest)).ToList();
                Assert.AreEqual(FloorRecipe.For(2).Nests + 1, nests.Count, $"씨앗 {seed}");
                total++;
                if (nests.Any(d => d.Piece == PieceKind.Corridor && d.Name == "새 쥐굴")) inCorridor++;
            }
            Assert.Greater(inCorridor * 2, total, "새 쥐굴은 대개 통로 조각에");
        }

        /// <summary>
        /// 다시 연 층 500장(통합 검토: 2층 계단 한 자리 41%·곧은 주 길 41%·승강장 출구 늘 오른쪽, 1층 숨은 방 (1,3) 46%):
        /// 계단 한 자리가 2층 35%·1층 25%를 넘지 않고, 2층 승강장 출구는 두 방향 이상에 한쪽 70% 이하, 1층 숨은 방 한 자리 25% 이하.
        /// 1층 출구는 늘 오른쪽이다(위는 사무실, 아래 구석으로 나가면 사무실 자물쇠 문 앞 칸이 주 길에서 2칸 넘게 떨어짐).
        /// </summary>
        [TestCase(1)]
        [TestCase(2)]
        public void ReopenedFloorsSpreadStairsExitsAndHiddenRooms(int floor)
        {
            var stairs = new Dictionary<string, int>();
            var exits = new Dictionary<Side, int>();
            var hidden = new Dictionary<string, int>();
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                var map = FloorGenerator.Generate(Reopened(floor, seed)).Build();
                var main = FloorRules.MainPath(map);
                var s = MapAnchors.FindStairsCell(map);
                var h = MapAnchors.FindHidden(map);
                Bump(stairs, s.X + "," + s.Y);
                Bump(exits, main[0].EdgeTo(main[1]).SideFrom(main[0]));
                Bump(hidden, h.X + "," + h.Y);
            }
            Assert.LessOrEqual(stairs.Values.Max() * 100, (floor == 1 ? 25 : 35) * SeedCount, Describe(stairs));
            if (floor == 2)
            {
                Assert.GreaterOrEqual(exits.Count, 2, "2층 승강장 출구는 두 방향 이상");
                Assert.LessOrEqual(exits.Values.Max() * 100, 70 * SeedCount, Describe(exits));
            }
            else
            {
                Assert.LessOrEqual(hidden.Values.Max() * 100, 25 * SeedCount, Describe(hidden));
            }
        }

        /// <summary>
        /// 연이은 원정(GenerateUnlike): 지난 원정과 모양·계단 칸이 같은 지도는 나오지 않고(주 길도 따라서 다름), 흔적은 거의 늘 MinTraces개 이상이다.
        /// 같은 입력·같은 지난 지도면 같은 결과다. 지난 지도가 없으면 Generate와 같다.
        /// </summary>
        [TestCase(1)]
        [TestCase(2)]
        public void NextExpeditionMovesStairsAndLeavesTraces(int floor)
        {
            var recipe = FloorRecipe.For(floor);
            int pairs = 0, fewTraces = 0;
            for (int profile = 1; profile <= 10; profile++)
            {
                ulong salt = ExpeditionSeeds.Mix((ulong)profile * 7919UL);
                string prev = FloorGenerator.Generate(new GeneratorInput { Floor = floor, Seed = recipe.ChosenSeed, FirstVisit = true, DeepestFloor = floor }).Glyphs;
                for (int expedition = 2; expedition <= 31; expedition++)
                {
                    var input = new GeneratorInput
                    {
                        Floor = floor, Seed = ExpeditionSeeds.ForExpedition(salt, floor, expedition), FirstVisit = false, DeepestFloor = 2,
                        HasPickaxe = profile % 2 == 0, Night = ExpeditionSeeds.NightBefore(salt, expedition),
                    };
                    var g = FloorGenerator.GenerateUnlike(input, prev);
                    Assert.AreEqual(g.Glyphs, FloorGenerator.GenerateUnlike(input, prev).Glyphs, "같은 입력·같은 지난 지도면 같은 지도");
                    Assert.IsTrue(FloorRules.Check(g.Build(), recipe, input).Passed, g.Describe());
                    Assert.IsFalse(MapDiff.SameShape(prev, g.Glyphs), $"원정 {expedition}: 지난 원정과 같은 모양\n{prev}\n{g.Glyphs}");
                    Assert.AreNotEqual(MapDiff.StairsCell(prev), MapDiff.StairsCell(g.Glyphs), $"원정 {expedition}: 계단 칸이 지난 원정과 같음\n{prev}\n{g.Glyphs}");
                    pairs++;
                    if (MapDiff.Compare(prev, g.Glyphs).Count < FloorGenerator.MinTraces) fewTraces++;
                    prev = g.Glyphs;
                }
            }
            Assert.Less(fewTraces * 50, pairs, $"흔적 {FloorGenerator.MinTraces}개 미만 {fewTraces}/{pairs}(2% 미만)");
            var plain = Input(floor, 7);
            Assert.AreEqual(FloorGenerator.Generate(plain).Glyphs, FloorGenerator.GenerateUnlike(plain, null).Glyphs);
            Assert.AreEqual(0, FloorGenerator.GenerateUnlike(plain, "").FreshRerolls);
        }

        [Test]
        public void GenerateNeverThrows()
        {
            var inputs = new[]
            {
                null,
                new GeneratorInput { Floor = 0 },
                new GeneratorInput { Floor = 3, Seed = 5 },
                new GeneratorInput { Floor = 99, Seed = 1 },
                new GeneratorInput { Floor = 1, Seed = ulong.MaxValue },
                new GeneratorInput { Floor = 2, Seed = ulong.MaxValue, FirstVisit = false, OnceDone = null, Night = (NightEvent)42 },
                new GeneratorInput { Floor = 1, Seed = 7, FirstVisit = false, DeepestFloor = 10, OnceDone = FloorRecipe.AllOnceItems().Select(o => o.Id).ToList() },
                new GeneratorInput { Floor = 2, Seed = 3, FirstVisit = false, DeepestFloor = -4 },
            };
            foreach (var input in inputs)
            {
                Assert.DoesNotThrow(() =>
                {
                    var g = FloorGenerator.Generate(input);
                    Assert.IsNotNull(g);
                    Assert.IsNotEmpty(g.Glyphs);
                    Assert.IsNotNull(g.Build());
                    Assert.IsNotEmpty(g.Describe());
                    Assert.IsNotNull(g.DescribeCells());
                });
            }
        }

        [Test]
        public void DescribeShowsSeedReportAndGlyphs()
        {
            var g = FloorGenerator.Generate(Input(2, 3));
            string text = g.Describe();
            StringAssert.Contains("씨앗 " + g.Seed, text);
            StringAssert.Contains("합격", text);
            StringAssert.Contains(g.Glyphs, text);
            foreach (var d in g.Legend) StringAssert.Contains(d.Glyph + " " + d.Name, g.DescribeCells());
        }

        [Test]
        public void SlotsStayOnWalkableGround()
        {
            foreach (PieceKind piece in Enum.GetValues(typeof(PieceKind)))
            {
                for (int set = -1; set < PieceSlots.PillarSetCount; set++)
                {
                    if (piece != PieceKind.Clearing && set != -1) continue;
                    var slots = PieceSlots.SlotsFor(piece, set);
                    var pillars = PieceSlots.PillarsIn(piece, set);
                    string where = $"{piece} 기둥 {set}";
                    foreach (var s in slots)
                    {
                        string at = $"{where} {s.Kind} ({s.Local.X}, {s.Local.Y})";
                        Assert.IsTrue(OnGround(piece, s), at + ": 길 밖");
                        float clear = s.Kind == SlotKind.Group || s.Kind == SlotKind.Nest ? 2.5f : 1.5f;
                        foreach (var p in pillars)
                            Assert.GreaterOrEqual(Distance(s.Local.X, s.Local.Y, p.X, p.Y), clear - 0.001, at + ": 기둥과 겹침");
                        if (s.Kind == SlotKind.Chalk)
                        {
                            Assert.IsTrue(s.NeedsDoor, at);
                            var door = DoorCenter((Side)s.DoorSide);
                            Assert.That(Distance(s.Local.X, s.Local.Y, door[0], door[1]), Is.InRange(3.0, 6.0), at + ": 분필은 문에서 3~6");
                            continue;
                        }
                        foreach (Side side in Enum.GetValues(typeof(Side)))
                        {
                            var door = DoorCenter(side);
                            Assert.GreaterOrEqual(Distance(s.Local.X, s.Local.Y, door[0], door[1]), 3.0 - 0.001, at + ": 문 앞 길 위");
                        }
                    }
                    if (piece == PieceKind.Clearing)
                    {
                        Assert.GreaterOrEqual(slots.Count(s => s.Kind == SlotKind.Group), 4, where);
                        Assert.GreaterOrEqual(slots.Count(s => s.Kind == SlotKind.Chest), 6, where);
                        Assert.GreaterOrEqual(slots.Count(s => s.Kind == SlotKind.Lamp), 6, where);
                        foreach (Side side in Enum.GetValues(typeof(Side)))
                            Assert.GreaterOrEqual(slots.Count(s => s.Kind == SlotKind.Chalk && s.DoorSide == (int)side), 4, where + " 분필 " + side);
                    }
                }
            }
        }

        // ── 도우미 ──

        /// <summary>시험 입력: 첫 방문·다시 연 층, 밤 사건, 받은 것, 최고 도달 층을 씨앗마다 섞는다(첫 시험판에서 생길 수 있는 범위).</summary>
        static GeneratorInput Input(int floor, int seed)
        {
            bool first = seed % 2 == 0;
            var done = new List<string>();
            if (floor == 1 && !first && seed % 3 != 0)
            {
                done.Add("f1.H.pickaxe");
                done.Add("f1.H.iron");
            }
            if (floor == 1 && !first && seed % 4 == 1)
            {
                done.Add("f1.T.nameplate");
                done.Add("f1.K.note1");
            }
            return new GeneratorInput
            {
                Floor = floor,
                Seed = (ulong)seed,
                FirstVisit = first,
                DeepestFloor = first ? floor : FloorRecipe.MaxTestFloor,
                HasPickaxe = !first || floor > 1,
                OnceDone = done,
                Night = first ? NightEvent.None : (NightEvent)(seed % 5),
            };
        }

        /// <summary>다시 연 층 입력(곡괭이·밤 사건·받은 것을 씨앗마다 섞음).</summary>
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

        static void Bump<T>(Dictionary<T, int> counts, T key) => counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;

        static string Describe<T>(Dictionary<T, int> counts) =>
            string.Join(", ", counts.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + kv.Value));

        /// <summary>범례 전부(글자·id·이름·조각·기둥, 자리 표시 종류·id·좌표·글·무리)를 한 줄로.</summary>
        static string Signature(IEnumerable<CellDef> legend)
        {
            var sb = new StringBuilder();
            foreach (var d in legend)
            {
                sb.Append(d.Glyph).Append('|').Append(d.Id).Append('|').Append(d.Name).Append('|').Append(d.Piece).Append('|').Append(d.Pillars).Append(':');
                foreach (var f in d.Features)
                    sb.Append(f.Kind).Append(',').Append(f.Id).Append(',').Append(f.Local.X.ToString(CultureInfo.InvariantCulture)).Append(',')
                        .Append(f.Local.Y.ToString(CultureInfo.InvariantCulture)).Append(',').Append(f.Label).Append(',').Append(f.Param).Append(',')
                        .Append(f.Boars).Append(f.Archers).Append(f.Rats).Append(',').Append(f.State).Append(',')
                        .Append(f.FacingDeg.ToString(CultureInfo.InvariantCulture)).Append(';');
                sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>칸 가운데 기준 문 가운데(칸 28×16, DungeonWorld).</summary>
        static double[] DoorCenter(Side side)
        {
            switch (side)
            {
                case Side.Right: return new[] { 14.0, 0.0 };
                case Side.Up: return new[] { 0.0, 8.0 };
                case Side.Left: return new[] { -14.0, 0.0 };
                default: return new[] { 0.0, -8.0 };
            }
        }

        static double Distance(double ax, double ay, double bx, double by) => Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

        /// <summary>조각 안 걸을 수 있는 땅(DungeonWorld.BuildPiece와 같은 모양, 벽에서 0.4 안쪽).</summary>
        static bool OnGround(PieceKind piece, PieceSlot s)
        {
            float x = s.Local.X, y = s.Local.Y;
            switch (piece)
            {
                case PieceKind.Corridor:
                    if (!s.NeedsDoor) return Math.Abs(x) < 3f && Math.Abs(y) < 3f;
                    switch ((Side)s.DoorSide)
                    {
                        case Side.Right: return x > -3f && x <= 13.1f && Math.Abs(y) < 3f;
                        case Side.Left: return x < 3f && x >= -13.1f && Math.Abs(y) < 3f;
                        case Side.Up: return y > -3f && y <= 7.1f && Math.Abs(x) < 3f;
                        default: return y < 3f && y >= -7.1f && Math.Abs(x) < 3f;
                    }
                case PieceKind.Room:
                case PieceKind.Office:
                    return Math.Abs(x) <= 6.6f && Math.Abs(y) <= 4.1f;
                case PieceKind.Hidden:
                    return Math.Abs(x) <= 5.6f && Math.Abs(y) <= 3.6f;
                default:
                    return Math.Abs(x) <= 13.1f && Math.Abs(y) <= 7.1f;
            }
        }
    }
}
