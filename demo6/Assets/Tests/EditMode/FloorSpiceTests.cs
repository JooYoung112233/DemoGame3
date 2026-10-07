using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 생성 덧칠(기획/1-2층-탐험-맛-1차.md 10장 시험 1~9): 손 지도는 그대로, 고른 2층의 계단 앞 정예와 가르치는 낙석, 다시 연 층 500장 검사·상한,
    /// 정예 비율·자리, 순찰 이웃 칸, 함정 자리·수, 품삯·유품·흙 묻은 쇠 궤짝, 단서 등잔 70%, 같은 입력 같은 결과. 끝에 Play 확인용 씨앗을 적는다.
    /// </summary>
    public sealed class FloorSpiceTests
    {
        const int SeedCount = 500;

        /// <summary>FloorGeneratorTests.ChosenFloorTwo와 같은 고른 2층 글자 지도(덧칠은 글자를 바꾸지 않는다).</summary>
        const string ChosenFloorTwo =
            "....A\n" +
            "....#\n" +
            "E-B-C-S\n" +
            "..|.|.#\n" +
            "H:D-F-G\n";

        // ── 1 ──

        [Test]
        public void HandMapIsNeverSpiced()
        {
            var g = FloorGenerator.Generate(new GeneratorInput { Floor = 1, Seed = 0 });
            Assert.IsTrue(g.HandMap);
            Assert.AreEqual(Signature(FloorOneMap.Legend()), Signature(g.Legend));
            Assert.AreEqual("덧칠 없음", g.Spice.ToString());

            // 다시 연 층 입력으로 손 지도를 덧칠하려 해도 아무것도 바꾸지 않는다.
            var again = FloorSpice.Apply(g, FloorRecipe.For(1), Reopened(1, 3, NightEvent.Collapse));
            Assert.AreEqual(0, again.Elites + again.Patrols + again.Rockfalls + again.Spikes + again.WageChests + again.Keepsakes + again.Buried + again.Skipped);
            Assert.AreEqual(Signature(FloorOneMap.Legend()), Signature(g.Legend));
            Assert.AreEqual(0, FloorSpice.Apply(null, FloorRecipe.For(1), null).Rockfalls);
            Assert.AreEqual(0, FloorSpice.Apply(FloorGenerator.Generate(Reopened(2, 5, NightEvent.None)), null, null).Rockfalls, "층 예산이 없으면 빈 보고");
        }

        // ── 2 ──

        [Test]
        public void ChosenFloorTwoHasTheStairsEliteAndOneTeachingRockfall()
        {
            var recipe = FloorRecipe.For(2);
            var input = new GeneratorInput { Floor = 2, Seed = recipe.ChosenSeed, FirstVisit = true, DeepestFloor = 2, HasPickaxe = true };
            var g = FloorGenerator.Generate(input);
            Assert.AreEqual(1, g.Attempts, "고른 씨앗은 첫 굴림에 합격(등잔 5)");
            Assert.IsFalse(g.FellBack);
            Assert.AreEqual(ChosenFloorTwo, g.Glyphs, g.Describe());
            var map = g.Build();
            Assert.IsTrue(FloorRules.Check(map, recipe, input).Passed, g.Describe());
            Assert.AreEqual(5, Features(map).Count(f => f.Kind == FeatureKind.WallLamp), "2층 벽 등잔 5");

            // 셋째 무리 = 계단 앞 막다른 공터 G의 단단한 정예 돌충이 + 굴쥐 2(잠, 결정 D4).
            var gCell = map.Find("G");
            var elite = gCell.Features.Single(f => f.Kind == FeatureKind.Group);
            Assert.IsTrue(elite.Elite, g.DescribeCells());
            Assert.AreEqual((1, 0, 2), (elite.Boars, elite.Archers, elite.Rats));
            Assert.AreEqual(GroupState.Sleep, elite.State);
            Assert.AreEqual("단단한 정예 돌충이", elite.Label);
            var groups = Features(map).Where(f => f.Kind == FeatureKind.Group).ToList();
            Assert.AreEqual(1, groups.Count(f => f.Elite), "정예 1");
            Assert.AreEqual(4, groups.Sum(f => f.Archers), "궁수 4(Q8)");
            Assert.AreEqual(5, groups.Sum(f => f.Rats), "굴쥐 5");
            var second = map.Find("C").Features.Single(f => f.Kind == FeatureKind.Group);
            Assert.AreEqual((1, 2, 2, GroupState.Eat), (second.Boars, second.Archers, second.Rats, second.State), "둘째 무리 돌충이 1 + 궁수 2 + 굴쥐 2(먹는 중)");

            // 가르치는 낙석 1이 주 길 밖 칸에. 가시 덫·품삯·유품·흙 묻은 쇠 궤짝·순찰 없음.
            var main = FloorRules.MainPath(map);
            var rocks = map.Cells.Where(c => c.Has(FeatureKind.RockfallTrap)).ToList();
            Assert.AreEqual(1, rocks.Sum(c => c.Features.Count(f => f.Kind == FeatureKind.RockfallTrap)));
            Assert.IsFalse(main.Contains(rocks[0]), $"낙석 {rocks[0].Id}는 주 길 밖");
            Assert.IsFalse(Features(map).Any(f => f.Kind == FeatureKind.FloorSpikes));
            Assert.IsFalse(Features(map).Any(f => CornerLoot.IsCornerParam(f.Param)));
            Assert.IsFalse(groups.Any(f => f.State == GroupState.Patrol));
            Assert.AreEqual((1, 0, 1, 0, 0, 0, 0, 0),
                (g.Spice.Elites, g.Spice.Patrols, g.Spice.Rockfalls, g.Spice.Spikes, g.Spice.WageChests, g.Spice.Keepsakes, g.Spice.Buried, g.Spice.Skipped));

            // 층 첫 방문은 밤·곡괭이·열쇠·최고 층·받은 것과 상관없이 같은 덧칠.
            var others = new[]
            {
                new GeneratorInput { Floor = 2, Seed = recipe.ChosenSeed, FirstVisit = true, DeepestFloor = 1 },
                new GeneratorInput { Floor = 2, Seed = recipe.ChosenSeed, FirstVisit = true, DeepestFloor = 2, Night = NightEvent.Collapse, HasKey = true },
                new GeneratorInput { Floor = 2, Seed = recipe.ChosenSeed, FirstVisit = true, DeepestFloor = 5, Night = NightEvent.Rumble },
                new GeneratorInput { Floor = 2, Seed = recipe.ChosenSeed, FirstVisit = true, DeepestFloor = 2, Night = NightEvent.Upheaval, OnceDone = new[] { "f1.H.pickaxe" } },
                new GeneratorInput { Floor = 2, Seed = recipe.ChosenSeed, FirstVisit = true, DeepestFloor = 2, Night = NightEvent.RatBurrow },
            };
            foreach (var other in others)
            {
                var o = FloorGenerator.Generate(other);
                Assert.AreEqual(g.Glyphs, o.Glyphs);
                Assert.AreEqual(Signature(g.Legend), Signature(o.Legend), $"밤 {other.Night}, 최고 {other.DeepestFloor}");
            }
        }

        // ── 3 ──

        [TestCase(1)]
        [TestCase(2)]
        public void ReopenedFloorsStillPassTheChecksAndKeepCaps(int floor)
        {
            var recipe = FloorRecipe.For(floor);
            var caps = PackTable.Caps(floor);
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                var input = Reopened(floor, seed, (NightEvent)(seed % 6));
                var g = FloorGenerator.Generate(input);
                var map = g.Build();
                string at = $"{floor}층 씨앗 {input.Seed} 밤 {input.Night}\n{g.Describe()}\n{g.DescribeCells()}";
                var report = FloorRules.Check(map, recipe, input);
                Assert.IsTrue(report.Passed, at + "\n" + report);
                Assert.AreEqual(map.Cells.Sum(c => c.Features.Count), map.Cells.SelectMany(c => c.Features).Select(f => f.Id).Distinct().Count(), at + ": id 겹침");
                foreach (var c in map.Cells)
                    foreach (var f in c.Features.Where(f => f.Kind == FeatureKind.RockfallTrap || f.Kind == FeatureKind.FloorSpikes))
                        StringAssert.StartsWith($"f{floor}.{c.Id}." + (f.Kind == FeatureKind.RockfallTrap ? "rockfall" : "spikes"), f.Id, at);

                // 층 상한(4-2, 첫 공터 포함, 정예는 돌충이 1).
                var groups = Features(map).Where(f => f.Kind == FeatureKind.Group).ToList();
                Assert.AreEqual(recipe.Groups.Length, groups.Count, at);
                int boars = groups.Sum(f => f.Elite ? 1 : f.Boars), archers = groups.Sum(f => f.Archers), rats = groups.Sum(f => f.Rats);
                Assert.That(boars, Is.InRange(caps.MinBoars, caps.MaxBoars), at + ": 돌충이");
                Assert.That(archers, Is.InRange(caps.MinArchers, caps.MaxArchers), at + ": 궁수");
                Assert.LessOrEqual(boars + archers, caps.MaxStrong, at + ": 강한 적");
                Assert.That(rats, Is.InRange(caps.MinRats, caps.MaxRats), at + ": 굴쥐");
                Assert.LessOrEqual(groups.Count(f => f.State == GroupState.Patrol), 1, at + ": 순찰 1까지");
                Assert.LessOrEqual(groups.Count(f => f.Elite), 1, at + ": 정예 1까지");
                Assert.AreEqual(groups.Count(f => f.Elite), g.Spice.Elites, at);
                Assert.AreEqual(groups.Count(f => f.State == GroupState.Patrol), g.Spice.Patrols, at);

                // 첫 공터 무리는 층 예산 그대로.
                var firstMix = recipe.Groups.First(m => m.FirstClearing);
                var first = FloorRules.MainPath(map)[1].Features.Single(f => f.Kind == FeatureKind.Group);
                Assert.AreEqual((firstMix.Boars, firstMix.Archers, firstMix.Rats, GroupState.Sleep, false), (first.Boars, first.Archers, first.Rats, first.State, first.Elite), at);
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        public void DeepReopenedFloorsStillWeakenGroups(int floor)
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                var input = Reopened(floor, seed, NightEvent.Rumble);
                input.DeepestFloor = floor + 3;
                var g = FloorGenerator.Generate(input);
                Assert.IsTrue(FloorRules.Check(g.Build(), FloorRecipe.For(floor), input).Passed, g.Describe());
                foreach (var f in g.Legend.SelectMany(d => d.Features).Where(f => f.Kind == FeatureKind.Group))
                    Assert.LessOrEqual(f.Boars + f.Archers, 1, $"씨앗 {input.Seed}: 최고 − 3 이하 층은 무리마다 강한 적 1마리 이하");
            }
        }

        // ── 4 ──

        [Test]
        public void EliteRatesFollowTheFloorAndTheRumbleNight()
        {
            Assert.That(EliteShare(2, 2, null), Is.InRange(0.43, 0.57), "2층 50%");
            Assert.That(EliteShare(1, 2, null), Is.InRange(0.18, 0.32), "1층(2층을 밟은 뒤) 25%");
            Assert.AreEqual(0.0, EliteShare(1, 1, null), "2층을 아직 못 간 1층은 0%");
            Assert.AreEqual(1.0, EliteShare(1, 1, NightEvent.Rumble), "거센 울림 다음 1층 100%");
            Assert.AreEqual(1.0, EliteShare(2, 2, NightEvent.Rumble), "거센 울림 다음 2층 100%");

            Assert.AreEqual(0, FloorSpice.ElitePermille(2, new GeneratorInput { Floor = 2, FirstVisit = true, Night = NightEvent.Rumble }), "층 첫 방문은 굴리지 않음");
            Assert.AreEqual(500, FloorSpice.ElitePermille(2, Reopened(2, 1, NightEvent.Collapse)));
            Assert.AreEqual(250, FloorSpice.ElitePermille(1, Reopened(1, 1, NightEvent.None)));
            Assert.AreEqual(1000, FloorSpice.ElitePermille(1, Reopened(1, 1, NightEvent.Rumble)));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void EliteSitsOffTheMainPathNearestTheStairs(int floor)
        {
            int offMainChoices = 0;
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                var input = Reopened(floor, seed, NightEvent.Rumble);
                var g = FloorGenerator.Generate(input);
                var map = g.Build();
                var main = FloorRules.MainPath(map);
                var others = map.Cells.Where(c => c != main[1] && c.Has(FeatureKind.Group)).ToList();
                var eliteCells = map.Cells.Where(c => c.Features.Any(f => f.Kind == FeatureKind.Group && f.Elite)).ToList();
                Assert.AreEqual(1, eliteCells.Count, $"씨앗 {input.Seed}: 거센 울림 정예 1\n{g.DescribeCells()}");
                var eliteCell = eliteCells[0];
                Assert.AreNotEqual(main[1], eliteCell, "첫 공터에는 정예가 없다");
                var off = others.Where(c => !main.Contains(c)).ToList();
                var pool = off.Count > 0 ? off : others;
                if (off.Count > 0)
                {
                    offMainChoices++;
                    Assert.IsFalse(main.Contains(eliteCell), $"씨앗 {input.Seed}: 주 길 밖 무리가 있으면 정예는 주 길 밖\n{g.DescribeCells()}");
                }
                var steps = StartSteps(map, MapAnchors.FindStairsCell(map));
                int best = pool.Min(c => steps.TryGetValue(c, out int d) ? d : int.MaxValue);
                Assert.AreEqual(best, steps[eliteCell], $"씨앗 {input.Seed}: 계단에서 가장 가까운 무리 칸");
                var elite = eliteCell.Features.Single(f => f.Kind == FeatureKind.Group);
                Assert.AreEqual((1, 0, 2, GroupState.Sleep, "단단한 정예 돌충이"), (elite.Boars, elite.Archers, elite.Rats, elite.State, elite.Label));
            }
            Assert.Greater(offMainChoices, SeedCount / 2, "대개 주 길 밖 무리가 있다");
        }

        // ── 5 ──

        [TestCase(1)]
        [TestCase(2)]
        public void PatrolsWalkToAnEmptyNeighbourOrBetweenTwoOpenDoors(int floor)
        {
            int patrols = 0, toNeighbour = 0, burrowPatrols = 0, burrowCandidates = 0, burrowRatPatrols = 0;
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                var input = Reopened(floor, seed, (NightEvent)(seed % 6));
                var g = FloorGenerator.Generate(input);
                var map = g.Build();
                var landing = MapAnchors.FindLanding(map);
                var stairs = MapAnchors.FindStairsCell(map);
                var start = map.Reachable(landing, FloorMap.StartPassable);
                var patrolGroups = map.Cells.SelectMany(c => c.Features.Where(f => f.Kind == FeatureKind.Group && f.State == GroupState.Patrol).Select(f => (c, f))).ToList();
                foreach (var (cell, f) in patrolGroups)
                {
                    patrols++;
                    string at = $"{floor}층 씨앗 {input.Seed} 칸 {cell.Id}\n{g.DescribeCells()}";
                    if (string.IsNullOrEmpty(f.PatrolCell))
                    {
                        Assert.GreaterOrEqual(cell.Edges.Count(e => e.Kind == EdgeKind.Open), 2, at + ": 제 칸 순찰은 열린 문 둘 이상");
                        continue;
                    }
                    toNeighbour++;
                    var n = map.Find(f.PatrolCell);
                    Assert.IsNotNull(n, at);
                    var edge = cell.EdgeTo(n);
                    Assert.IsNotNull(edge, at + ": 이웃 칸");
                    Assert.AreEqual(EdgeKind.Open, edge.Kind, at + ": 열린 길로 이어짐");
                    Assert.IsTrue(start.Contains(n), at + ": 처음 갈 수 있는 칸");
                    Assert.IsTrue(Generic(n.Piece), at + ": 공터·통로·곁방");
                    Assert.AreNotEqual(landing, n, at);
                    Assert.AreNotEqual(stairs, n, at);
                    Assert.IsFalse(n.Has(FeatureKind.Group) || n.Has(FeatureKind.Nest) || n.Has(FeatureKind.Stake), at + ": 무리·둥지·말뚝 없음");
                }
                // 새 쥐굴 밤(4-9 ⑥): 순찰할 수 있는 굴쥐만 무리(첫 공터·정예 아님, 빈 이웃 칸이 있거나 열린 문 둘 이상)가 있으면 순찰은 정확히 1이다.
                // 굴쥐만 무리가 순찰이면 이름은 '굴쥐 순찰'이다(후보 표의 굴쥐 순찰 줄도, 순찰이 없어 덧칠이 바꾼 무리도 같다).
                if (input.Night == NightEvent.RatBurrow)
                {
                    if (patrolGroups.Count > 0) burrowPatrols++;
                    var firstCell = FloorRules.MainPath(map)[1];
                    bool candidate = map.Cells.Any(c => c != firstCell && CanPatrol(c, landing, stairs, start) &&
                                                        c.Features.Any(f => f.Kind == FeatureKind.Group && f.Boars == 0 && f.Archers == 0 && f.Rats > 0 && !f.Elite));
                    if (candidate)
                    {
                        burrowCandidates++;
                        string at = $"{floor}층 새 쥐굴 밤 씨앗 {input.Seed}\n{g.DescribeCells()}";
                        Assert.AreEqual(1, patrolGroups.Count, at + ": 순찰할 수 있는 굴쥐만 무리가 있으면 순찰은 1");
                        var patrol = patrolGroups[0].f;
                        if (patrol.Boars == 0 && patrol.Archers == 0)
                        {
                            burrowRatPatrols++;
                            Assert.AreEqual(ExploreText.PackRatPatrol, patrol.Label, at + ": 굴쥐만 순찰 무리 이름");
                        }
                    }
                }
            }
            // 층 첫 방문은 순찰을 만들지 않는다.
            for (int seed = 1; seed <= 100; seed++)
            {
                var g = FloorGenerator.Generate(new GeneratorInput { Floor = floor, Seed = (ulong)seed, FirstVisit = true, DeepestFloor = floor });
                Assert.IsFalse(g.Legend.SelectMany(d => d.Features).Any(f => f.State == GroupState.Patrol && f.Kind == FeatureKind.Group), $"첫 방문 씨앗 {seed}");
            }
            TestContext.WriteLine($"{floor}층 다시 연 층 {SeedCount}장: 순찰 {patrols}(이웃 칸 {toNeighbour}), 새 쥐굴 밤 순찰 있음 {burrowPatrols}" +
                                  $", 순찰할 수 있는 굴쥐만 무리가 있는 새 쥐굴 밤 {burrowCandidates}(굴쥐만 순찰 {burrowRatPatrols})");
            Assert.Greater(patrols, SeedCount / 10, "순찰이 나온다");
            Assert.Greater(toNeighbour, 0, "이웃 칸까지 오가는 순찰이 있다");
            Assert.Greater(burrowCandidates, 0, "순찰할 수 있는 굴쥐만 무리가 있는 새 쥐굴 밤이 나온다");
            Assert.Greater(burrowRatPatrols, 0, "새 쥐굴 밤 굴쥐만 순찰이 나온다");
        }

        // ── 5-2 ──

        [TestCase(1)]
        [TestCase(2)]
        public void SleepingPacksFaceAwayFromTheEntryDoor(int floor)
        {
            // 생성기 PutGroup 규칙: 자는 무리는 들어오는 문 반대쪽을 본다(등 뒤로 다가갈 틈). 층 첫 방문(덧칠이 무리를 건드리지 않음)으로
            // 이 시험의 들어오는 문 계산이 생성기와 같은지 보고, 다시 연 층은 덧칠이 상태를 잠으로 바꾼 무리도 지키는지 본다(첫 공터 제외).
            int firstVisitSleepers = 0, reopenedSleepers = 0;
            for (int seed = 1; seed <= 200; seed++)
            {
                var g = FloorGenerator.Generate(new GeneratorInput { Floor = floor, Seed = (ulong)seed, FirstVisit = true, DeepestFloor = floor });
                if (g.HandMap) continue;
                firstVisitSleepers += AssertSleepersFaceAway(g, false, $"첫 방문 {floor}층 씨앗 {seed}");
            }
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                var input = Reopened(floor, seed, (NightEvent)(seed % 6));
                var g = FloorGenerator.Generate(input);
                if (g.HandMap) continue;
                reopenedSleepers += AssertSleepersFaceAway(g, true, $"다시 연 {floor}층 씨앗 {input.Seed} 밤 {input.Night}");
            }
            TestContext.WriteLine($"{floor}층 자는 무리: 첫 방문 200장 {firstVisitSleepers}, 다시 연 층 {SeedCount}장 {reopenedSleepers}(첫 공터 제외)");
            Assert.Greater(firstVisitSleepers, 0);
            Assert.Greater(reopenedSleepers, SeedCount / 2, "다시 연 층에도 자는 무리가 많다");
        }

        // ── 6 ──

        [TestCase(1)]
        [TestCase(2)]
        public void TrapsSitOnSafeSlotsAndCountsFollowTheTable(int floor)
        {
            var rockCounts = new Dictionary<int, int>();
            var spikeCounts = new Dictionary<int, int>();
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                var input = Reopened(floor, seed, (NightEvent)(seed % 6));
                var g = FloorGenerator.Generate(input);
                var map = g.Build();
                string at = $"{floor}층 씨앗 {input.Seed} 밤 {input.Night}\n{g.DescribeCells()}";
                AssertTrapsSafe(map, at);
                int rocks = Features(map).Count(f => f.Kind == FeatureKind.RockfallTrap);
                int spikes = Features(map).Count(f => f.Kind == FeatureKind.FloorSpikes);
                Assert.AreEqual((rocks, spikes), (g.Spice.Rockfalls, g.Spice.Spikes), at);
                Assert.AreEqual(0, g.Spice.Skipped, at + ": 자리가 없어 건너뛴 함정");
                int collapse = input.Night == NightEvent.Collapse ? 1 : 0;
                if (floor == 1)
                {
                    Assert.That(rocks, Is.InRange(collapse, 1 + collapse), at + ": 1층 낙석 0~1(+무너짐)");
                    Assert.AreEqual(1, spikes, at + ": 1층 가시 덫 1");
                }
                else
                {
                    Assert.AreEqual(1 + collapse, rocks, at + ": 2층 낙석 1(+무너짐)");
                    Assert.That(spikes, Is.InRange(1, 2), at + ": 2층 가시 덫 1~2");
                }
                Bump(rockCounts, rocks - collapse);
                Bump(spikeCounts, spikes);
            }
            if (floor == 1) Assert.AreEqual(2, rockCounts.Count, "1층 낙석은 0과 1이 다 나온다");
            else Assert.AreEqual(2, spikeCounts.Count, "2층 가시 덫은 1과 2가 다 나온다");

            // 층 첫 방문: 1층 생성 지도는 함정 없음, 2층은 가르치는 낙석 1(주 길 밖 먼저)·가시 덫 없음.
            int offMain = 0;
            for (int seed = 1; seed <= 100; seed++)
            {
                var input = new GeneratorInput { Floor = floor, Seed = (ulong)seed, FirstVisit = true, DeepestFloor = floor, Night = (NightEvent)(seed % 6) };
                var g = FloorGenerator.Generate(input);
                var map = g.Build();
                AssertTrapsSafe(map, $"첫 방문 {floor}층 씨앗 {seed}");
                var rocks = map.Cells.Where(c => c.Has(FeatureKind.RockfallTrap)).ToList();
                Assert.AreEqual(floor == 1 ? 0 : 1, rocks.Count, $"첫 방문 {floor}층 씨앗 {seed}");
                Assert.IsFalse(map.Cells.Any(c => c.Has(FeatureKind.FloorSpikes)));
                if (rocks.Count > 0 && !FloorRules.MainPath(map).Contains(rocks[0])) offMain++;
            }
            if (floor == 2) Assert.Greater(offMain, 80, "첫 방문 낙석은 대개 주 길 밖");
        }

        // ── 7 ──

        [TestCase(1)]
        [TestCase(2)]
        public void CornerChestsAreMarkedOnlyOnReopenedFloors(int floor)
        {
            int wages = 0, keepsakes = 0, buried = 0, upheavals = 0;
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                var input = Reopened(floor, seed, (NightEvent)(seed % 6));
                var g = FloorGenerator.Generate(input);
                var map = g.Build();
                var start = map.Reachable(MapAnchors.FindLanding(map), FloorMap.StartPassable);
                string at = $"{floor}층 씨앗 {input.Seed} 밤 {input.Night}\n{g.DescribeCells()}";
                foreach (var c in map.Cells)
                {
                    bool deadEnd = start.Contains(c) && Generic(c.Piece) && c.Edges.Count(e => FloorMap.StartPassable(e.Kind)) == 1;
                    foreach (var f in c.Features)
                    {
                        if (f.Kind == FeatureKind.WoodChest)
                        {
                            if (deadEnd) Assert.AreEqual(CornerLoot.WageParam, f.Param, at + $": 막다른 칸 {c.Id}의 나무 궤짝은 품삯 궤짝");
                            else if (c.Piece == PieceKind.Hidden) Assert.AreEqual(CornerLoot.KeepsakeParam, f.Param, at + ": 숨은 방 나무 궤짝은 유품 상자");
                            else Assert.AreEqual("", f.Param, at + $": {c.Id} 나무 궤짝");
                        }
                        if (f.Param == CornerLoot.WageParam) wages++;
                        if (f.Param == CornerLoot.KeepsakeParam) keepsakes++;
                        if (f.Param == CornerLoot.BuriedParam)
                        {
                            buried++;
                            Assert.AreEqual(FeatureKind.IronChest, f.Kind, at);
                            Assert.IsTrue(start.Contains(c) && c.Piece != PieceKind.Hidden, at + ": 흙 묻은 쇠 궤짝은 처음 갈 수 있는 칸, 숨은 방 아님");
                        }
                    }
                }
                int buriedHere = Features(map).Count(f => f.Param == CornerLoot.BuriedParam);
                if (input.Night == NightEvent.Upheaval)
                {
                    upheavals++;
                    Assert.AreEqual(1, buriedHere, at + ": 드러남 밤 흙 묻은 쇠 궤짝 1");
                }
                else Assert.AreEqual(0, buriedHere, at);
                Assert.AreEqual((g.Spice.WageChests, g.Spice.Keepsakes, g.Spice.Buried),
                    (Features(map).Count(f => f.Param == CornerLoot.WageParam), Features(map).Count(f => f.Param == CornerLoot.KeepsakeParam), buriedHere), at);
            }
            Assert.Greater(wages, 0, "품삯 궤짝이 나온다");
            Assert.Greater(keepsakes, 0, "유품 상자가 나온다");
            Assert.AreEqual(upheavals, buried);
            TestContext.WriteLine($"{floor}층 다시 연 층 {SeedCount}장: 품삯 {wages}, 유품 {keepsakes}, 흙 묻은 쇠 궤짝 {buried}");

            // 층 첫 방문은 구석 표시가 없다.
            for (int seed = 1; seed <= 200; seed++)
            {
                var g = FloorGenerator.Generate(new GeneratorInput { Floor = floor, Seed = (ulong)seed, FirstVisit = true, DeepestFloor = floor, Night = NightEvent.Upheaval });
                Assert.IsFalse(g.Legend.SelectMany(d => d.Features).Any(f => CornerLoot.IsCornerParam(f.Param)), $"첫 방문 씨앗 {seed}");
            }
        }

        // ── 8 ──

        [TestCase(1)]
        [TestCase(2)]
        public void ClueLampStandsBesideTheHiddenRoomSeventyPercentOnReopenedFloors(int floor)
        {
            int counted = 0, rolled = 0, lampBeside = 0;
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                var input = Reopened(floor, seed, (NightEvent)(seed % 5));
                var g = FloorGenerator.Generate(input);
                if (g.FellBack) continue;
                counted++;
                var map = g.Build();
                var hidden = MapAnchors.FindHidden(map);
                var beside = hidden.Edges.Single().Other(hidden);
                bool lamp = beside.Has(FeatureKind.WallLamp);
                if (lamp) lampBeside++;
                if (!FloorSpice.ClueLampRoll(input, g.Seed)) continue;
                rolled++;
                Assert.IsTrue(lamp, $"{floor}층 씨앗 {g.Seed}: 굴림에 붙으면 숨은 방 이웃 칸 {beside.Id}에 단서 등잔\n{g.DescribeCells()}");
            }
            double share = rolled / (double)counted;
            TestContext.WriteLine($"{floor}층 다시 연 층 {counted}장: 단서 등잔 굴림 {share:P0}, 숨은 방 이웃 칸 등잔(다른 등잔 포함) {lampBeside / (double)counted:P0}");
            Assert.That(share, Is.InRange(0.6, 0.8), "다시 연 층 단서 등잔 약 70%");
            // 문서 10장 시험 8: 다시 연 층 숨은 방 이웃 칸 등잔 비율 60~80%(굴림에 떨어지면 남은 등잔도 이웃 칸을 비켜 감).
            Assert.That(lampBeside / (double)counted, Is.InRange(0.6, 0.8), "다시 연 층 숨은 방 이웃 칸 등잔 60~80%");

            // 층 첫 방문은 늘(굴리지 않음).
            for (int seed = 1; seed <= 100; seed++)
            {
                var input = new GeneratorInput { Floor = floor, Seed = (ulong)seed, FirstVisit = true, DeepestFloor = floor };
                Assert.IsTrue(FloorSpice.ClueLampRoll(input, (ulong)seed));
                var map = FloorGenerator.Generate(input).Build();
                var hidden = MapAnchors.FindHidden(map);
                Assert.IsTrue(hidden.Edges.Single().Other(hidden).Has(FeatureKind.WallLamp), $"첫 방문 씨앗 {seed}");
            }
            Assert.IsTrue(FloorSpice.ClueLampRoll(null, 1UL));
        }

        // ── 9 ──

        [TestCase(1)]
        [TestCase(2)]
        public void SameInputSameSpice(int floor)
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                var input = Reopened(floor, seed, (NightEvent)(seed % 6));
                var a = FloorGenerator.Generate(input);
                var b = FloorGenerator.Generate(Reopened(floor, seed, (NightEvent)(seed % 6)));
                Assert.AreEqual(a.Glyphs, b.Glyphs);
                Assert.AreEqual(Signature(a.Legend), Signature(b.Legend), $"씨앗 {input.Seed}");
                Assert.AreEqual(a.Spice.ToString(), b.Spice.ToString());
                Assert.AreEqual(a.DescribeCells(), b.DescribeCells());
                StringAssert.Contains(a.Spice.ToString(), a.Describe(), "설명 끝에 덧칠 한 줄");
            }
        }

        // ── 10: Play 확인용 씨앗 ──

        [Test]
        public void ListReopenedFloorTwoSeedsForPlayChecks()
        {
            // F1 '갱도 씨앗' → '이 씨앗으로 다시'로 다시 연 2층(첫 귀환의 밤, 최고 2층)을 열면 정예·순찰·낙석이 다 보이는 씨앗.
            var found = new List<string>();
            for (ulong seed = 1; seed <= 400 && found.Count < 5; seed++)
            {
                var input = new GeneratorInput { Floor = 2, Seed = seed, FirstVisit = false, DeepestFloor = 2, HasPickaxe = true, Night = NightEvent.FirstNight };
                var g = FloorGenerator.Generate(input);
                if (g.Seed != seed || g.Spice.Elites == 0 || g.Spice.Patrols == 0 || g.Spice.Rockfalls == 0) continue;
                var patrol = g.Legend.SelectMany(d => d.Features.Select(f => (d, f))).First(x => x.f.Kind == FeatureKind.Group && x.f.State == GroupState.Patrol);
                found.Add($"씨앗 {seed}: {g.Spice} · 순찰 {patrol.d.Id}→{(patrol.f.PatrolCell == "" ? "제 칸" : patrol.f.PatrolCell)}");
            }
            foreach (var line in found) TestContext.WriteLine("다시 연 2층 Play 확인 " + line);
            Assert.GreaterOrEqual(found.Count, 3, "정예·순찰·낙석이 다 있는 다시 연 2층 씨앗");
        }

        // ── 도우미 ──

        /// <summary>다시 연 층 입력(최고 도달 2층, 곡괭이·받은 것은 씨앗마다 섞음). 밤은 부르는 쪽이 정한다.</summary>
        static GeneratorInput Reopened(int floor, int seed, NightEvent night) => new GeneratorInput
        {
            Floor = floor,
            Seed = (ulong)seed * 2654435761UL,
            FirstVisit = false,
            DeepestFloor = FloorRecipe.MaxTestFloor,
            HasPickaxe = seed % 2 == 0,
            OnceDone = floor == 1 && seed % 3 != 0 ? new[] { "f1.H.pickaxe", "f1.H.iron" } : Array.Empty<string>(),
            Night = night,
        };

        /// <summary>다시 연 층 씨앗 500장에서 정예가 나온 몫.</summary>
        static double EliteShare(int floor, int deepest, NightEvent? night)
        {
            int elites = 0;
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                var input = Reopened(floor, seed, night ?? (NightEvent)(seed % 5));
                input.DeepestFloor = deepest;
                var g = FloorGenerator.Generate(input);
                if (g.Spice.Elites > 0) elites++;
            }
            return elites / (double)SeedCount;
        }

        static IEnumerable<CellFeature> Features(FloorMap map) => map.Cells.SelectMany(c => c.Features);

        static bool Generic(PieceKind piece) => piece == PieceKind.Clearing || piece == PieceKind.Corridor || piece == PieceKind.Room;

        /// <summary>순찰할 수 있는 칸(4-3): 열린 길로 이어진 빈 이웃 칸(처음 갈 수 있음, 공터·통로·곁방, 승강장·계단 앞 아님, 무리·둥지·말뚝 없음)이 있거나 열린 문 둘 이상.</summary>
        static bool CanPatrol(MapCell cell, MapCell landing, MapCell stairs, HashSet<MapCell> start)
        {
            if (cell.Edges.Count(e => e.Kind == EdgeKind.Open) >= 2) return true;
            return cell.Edges.Any(e =>
            {
                if (e.Kind != EdgeKind.Open) return false;
                var n = e.Other(cell);
                return n != null && n != landing && n != stairs && start.Contains(n) && Generic(n.Piece) &&
                       !(n.Has(FeatureKind.Group) || n.Has(FeatureKind.Nest) || n.Has(FeatureKind.Stake));
            });
        }

        /// <summary>자는 무리마다 바라보는 방향 = 들어오는 문 반대쪽((쪽 + 2) % 4 × 90°)인지 단언하고 센 수를 돌려준다. skipFirst면 첫 공터는 빼고 센다.</summary>
        static int AssertSleepersFaceAway(GeneratedFloor g, bool skipFirst, string at)
        {
            var map = g.Build();
            var first = FloorRules.MainPath(map)[1];
            var entry = EntrySides(map);
            int count = 0;
            foreach (var c in map.Cells)
            {
                if (skipFirst && c == first) continue;
                foreach (var f in c.Features)
                {
                    if (f.Kind != FeatureKind.Group || f.State != GroupState.Sleep) continue;
                    Assert.IsTrue(entry.ContainsKey(c), at + $": {c.Id}에 들어오는 문");
                    Assert.AreEqual(((entry[c] + 2) % 4) * 90f, f.FacingDeg, at + $": {c.Id} 자는 무리 '{f.Label}'는 들어오는 문({(Side)entry[c]}) 반대쪽\n{g.DescribeCells()}");
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 칸마다 들어오는 문 쪽((int)Side), 생성기 ReadMainPath와 같은 규칙: 승강장에서 열린 길·판자벽만 지나는 너비 우선(방향 차례 Right·Up·Left·Down),
        /// 그 길로 못 가는 칸은 방향 차례 마지막 문.
        /// </summary>
        static Dictionary<MapCell, int> EntrySides(FloorMap map)
        {
            var landing = MapAnchors.FindLanding(map);
            var entry = new Dictionary<MapCell, int>();
            var seen = new HashSet<MapCell> { landing };
            var queue = new Queue<MapCell>();
            queue.Enqueue(landing);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                for (int s = 0; s < 4; s++)
                {
                    var e = c.Edges.FirstOrDefault(x => x.SideFrom(c) == (Side)s);
                    if (e == null || !FloorMap.StartPassable(e.Kind)) continue;
                    var n = e.Other(c);
                    if (!seen.Add(n)) continue;
                    entry[n] = (s + 2) % 4;
                    queue.Enqueue(n);
                }
            }
            foreach (var c in map.Cells)
            {
                if (seen.Contains(c)) continue;
                for (int s = 3; s >= 0; s--)
                {
                    if (!c.Edges.Any(x => x.SideFrom(c) == (Side)s)) continue;
                    entry[c] = s;
                    break;
                }
            }
            return entry;
        }

        /// <summary>함정 자리 규칙(4-5·4-6): 칸 종류, 슬롯, 무리·둥지·다른 물건과 거리, 칸마다 같은 종류 하나.</summary>
        static void AssertTrapsSafe(FloorMap map, string at)
        {
            var landing = MapAnchors.FindLanding(map);
            var start = map.Reachable(landing, FloorMap.StartPassable);
            var first = FloorRules.MainPath(map)[1];
            foreach (var c in map.Cells)
            {
                var traps = c.Features.Where(f => f.Kind == FeatureKind.RockfallTrap || f.Kind == FeatureKind.FloorSpikes).ToList();
                if (traps.Count == 0) continue;
                string where = at + $" 칸 {c.Id}";
                Assert.IsTrue(start.Contains(c), where + ": 처음 갈 수 있는 칸");
                Assert.IsTrue(Generic(c.Piece), where + ": 공터·통로·곁방(승강장·계단 앞·숨은 방·랜드마크 아님)");
                Assert.AreNotEqual(first, c, where + ": 첫 공터 아님");
                Assert.IsFalse(c.Has(FeatureKind.Stake) || c.Has(FeatureKind.Stairs), where + ": 말뚝·계단 칸 아님");
                Assert.LessOrEqual(traps.Count(f => f.Kind == FeatureKind.RockfallTrap), 1, where);
                Assert.LessOrEqual(traps.Count(f => f.Kind == FeatureKind.FloorSpikes), 1, where);
                foreach (var t in traps)
                {
                    bool rock = t.Kind == FeatureKind.RockfallTrap;
                    var kinds = rock ? new[] { SlotKind.Event } : new[] { SlotKind.Small, SlotKind.Event };
                    Assert.IsTrue(PieceSlots.SlotsFor(c.Piece, c.Def.Pillars).Any(s => kinds.Contains(s.Kind) && s.Local.X == t.Local.X && s.Local.Y == t.Local.Y &&
                                                                                    PieceSlots.Usable(s, side => c.Edges.Any(e => e.SideFrom(c) == side))),
                        where + $": {t.Id}는 {(rock ? "사건" : "작은 물건·사건")} 자리 슬롯");
                    Assert.AreEqual(rock ? ExploreText.RockfallLabel : ExploreText.SpikesLabel, t.Label);
                    foreach (var f in c.Features)
                    {
                        if (f == t) continue;
                        bool big = f.Kind == FeatureKind.Group || f.Kind == FeatureKind.Nest;
                        double need = rock ? (big ? 4.0 : 2.5) : (big ? 3.0 : 1.5);
                        // 함정끼리는 두 규칙 가운데 큰 거리(가시 덫이 낙석 잔돌 안에 들지 않게).
                        if (f.Kind == FeatureKind.RockfallTrap) need = Math.Max(need, 2.5);
                        Assert.GreaterOrEqual(Distance(f.Local, t.Local), need - 1e-3, where + $": {t.Id} ↔ {f.Id}");
                    }
                }
            }
        }

        /// <summary>계단에서 열린 길·판자벽만 지나는 걸음 수.</summary>
        static Dictionary<MapCell, int> StartSteps(FloorMap map, MapCell from)
        {
            var dist = new Dictionary<MapCell, int> { [from] = 0 };
            var queue = new Queue<MapCell>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var e in c.Edges)
                {
                    if (!FloorMap.StartPassable(e.Kind)) continue;
                    var n = e.Other(c);
                    if (dist.ContainsKey(n)) continue;
                    dist[n] = dist[c] + 1;
                    queue.Enqueue(n);
                }
            }
            return dist;
        }

        static double Distance(Offset a, Offset b) => Math.Sqrt((a.X - b.X) * (double)(a.X - b.X) + (a.Y - b.Y) * (double)(a.Y - b.Y));

        static void Bump(Dictionary<int, int> counts, int key) => counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;

        /// <summary>범례 전부(FloorGeneratorTests.Signature + 정예·순찰 칸).</summary>
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
                        .Append(f.FacingDeg.ToString(CultureInfo.InvariantCulture)).Append(',').Append(f.Elite).Append(',').Append(f.PatrolCell).Append(';');
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
