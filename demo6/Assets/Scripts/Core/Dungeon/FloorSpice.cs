using System;
using System.Collections.Generic;
using System.Text;
using Demo6.Core.Loot;
using Demo6.Core.Random;

namespace Demo6.Core.Dungeon
{
    /// <summary>생성 덧칠 한 번의 결과(FloorSpice.Apply). 콘솔·시험 패널(GeneratedFloor.Describe)과 시험이 읽는다.</summary>
    public sealed class SpiceReport
    {
        /// <summary>정예 무리 수(덧칠 뒤 지도 전체).</summary>
        public int Elites;
        /// <summary>순찰 무리 수(덧칠 뒤 지도 전체).</summary>
        public int Patrols;
        /// <summary>놓은 낙석 자리 수.</summary>
        public int Rockfalls;
        /// <summary>놓은 가시 덫 수.</summary>
        public int Spikes;
        /// <summary>광부 품삯 궤짝으로 바꾼 나무 궤짝 수.</summary>
        public int WageChests;
        /// <summary>광부 유품 상자로 바꾼 나무 궤짝 수.</summary>
        public int Keepsakes;
        /// <summary>흙 묻은 쇠 궤짝으로 바꾼 쇠 궤짝 수(드러남 밤 0~1).</summary>
        public int Buried;
        /// <summary>자리가 없어 건너뛴 함정 수.</summary>
        public int Skipped;

        /// <summary>한 줄: 0이 아닌 것만 '덧칠: 정예 1 · 낙석 1 …', 아무것도 없으면 '덧칠 없음'.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            Part(sb, "정예", Elites);
            Part(sb, "순찰", Patrols);
            Part(sb, "낙석", Rockfalls);
            Part(sb, "가시 덫", Spikes);
            Part(sb, "품삯 궤짝", WageChests);
            Part(sb, "유품 상자", Keepsakes);
            Part(sb, "흙 묻은 쇠 궤짝", Buried);
            Part(sb, "못 놓은 덫", Skipped);
            return sb.Length == 0 ? "덧칠 없음" : "덧칠: " + sb;
        }

        static void Part(StringBuilder sb, string name, int count)
        {
            if (count <= 0) return;
            if (sb.Length > 0) sb.Append(" · ");
            sb.Append(name).Append(' ').Append(count);
        }
    }

    /// <summary>
    /// 생성 덧칠(기획/1-2층-탐험-맛-1차.md 2장 원칙, 4-1~4-3·4-5~4-9). 검사에 합격한 생성 지도 위에 정예·순찰·낙석·가시 덫·구석 보상 표시를 얹는다.
    /// 새 난수 흐름(Pcg32Random(씨앗, Stream = 4))만 쓰고 차례가 고정이라 같은 원정이면 같은 결과이고, 지도·내용물·장식 흐름(1~3)은 하나도 더 뽑지 않는다
    /// (고른 2층 지도와 생성기 시험이 그대로 선다). 손 지도(1층 씨앗 0)는 덧칠하지 않는다(2장 3, 처음 하는 사람이 기습을 배우는 곳).
    /// 무리 수·궤짝 수·등잔 수와 자리 id는 그대로라 덧칠 뒤에도 층 검사(FloorRules.Check)가 같다. 함정은 숨은 위험이라 개수 검사·발견 주머니에 들지 않는다.
    /// 덧칠은 범례의 CellDef(지도 칸과 같은 물체)를 고치므로 뒤에 짓는 FloorMap과 문 비틀기(DoorLayout, 놓인 자리 표시를 읽어 비켜 감)가 그대로 본다.
    /// 층 첫 방문은 층 예산의 가르치는 낙석(FirstVisitRockfalls, 주 길 밖 먼저)만 놓는다(밤·곡괭이·최고 층을 보지 않음).
    /// </summary>
    public static class FloorSpice
    {
        /// <summary>덧칠 난수 흐름(지도 1·내용물 2·장식 3 다음, ExpeditionSeeds).</summary>
        public const ulong Stream = 4;
        /// <summary>숨은 방 옆 단서 등잔 70% 굴림 흐름(4-8). 생성기 안에서 내용물을 놓기 전에 굴린다.</summary>
        public const ulong ClueStream = 5;
        /// <summary>다시 연 층 단서 등잔 확률(‰, 4-8).</summary>
        public const int ClueLampPermille = 700;
        /// <summary>다시 연 1층 정예 확률(‰): 2층을 밟은 적이 있을 때만(4-2, 12장 질문 4 '가').</summary>
        public const int EliteFloorOnePermille = 250;
        /// <summary>다시 연 2층 정예 확률(‰, 4-2).</summary>
        public const int EliteFloorTwoPermille = 500;

        /// <summary>숨은 방 이웃 칸에 단서 등잔을 두는가: 층 첫 방문은 늘(굴리지 않음), 다시 연 층은 흐름 5의 첫 굴림 ‰ &lt; 700.</summary>
        public static bool ClueLampRoll(GeneratorInput input, ulong seed)
        {
            if (input == null || input.FirstVisit) return true;
            return new Pcg32Random(seed, ClueStream).NextInt(0, 1000) < ClueLampPermille;
        }

        /// <summary>
        /// 정예 무리가 나올 확률(‰, 4-2·4-9): 층 첫 방문 0(2층 첫 방문 정예는 층 예산에 고정), 거센 울림 밤 다음 1000,
        /// 1층은 최고 도달 층 2 이상이면 250 아니면 0, 2층(과 그 아래)은 500.
        /// </summary>
        public static int ElitePermille(int floor, GeneratorInput input)
        {
            if (input == null || input.FirstVisit) return 0;
            if (input.Night == NightEvent.Rumble) return 1000;
            if (floor <= 1) return input.DeepestFloor >= 2 ? EliteFloorOnePermille : 0;
            return EliteFloorTwoPermille;
        }

        /// <summary>
        /// 덧칠한다(예외 없이 쓰도록 짰지만, 생성기는 TryBuild 안에서 부르므로 예외가 나도 그 씨앗만 실패로 친다). 차례(난수 흐름 4):
        /// 층 첫 방문 = 가르치는 낙석만. 다시 연 층 = 정예 굴림 → 정예 자리 → 첫 공터 뺀 무리에 후보 표(PackTable.Pick) → 깊은 층 강한 적 줄이기
        /// → 순찰 이웃 칸 → 새 쥐굴 밤 굴쥐 순찰 → 품삯 궤짝 → 유품 상자 → 드러남 밤 흙 묻은 쇠 궤짝 → 낙석 → 가시 덫.
        /// 손 지도이거나 recipe가 없으면 빈 보고.
        /// </summary>
        public static SpiceReport Apply(GeneratedFloor g, FloorRecipe recipe, GeneratorInput input)
        {
            var report = new SpiceReport();
            if (g == null || g.HandMap || recipe == null) return report;
            if (input == null) input = FloorRules.FirstVisitInput(recipe);
            var map = g.Build();
            var s = new Spicer(g, map, recipe, input, report);
            if (!s.Ready) return report;
            if (input.FirstVisit) s.PlaceRockfalls(Math.Max(0, recipe.FirstVisitRockfalls), true);
            else s.Reopened();
            s.Finish();
            return report;
        }

        /// <summary>한 번의 덧칠(지도·난수·보고를 들고 다닌다).</summary>
        sealed class Spicer
        {
            /// <summary>거리 비교 여유(같은 거리는 통과).</summary>
            const float Eps = 1e-4f;
            /// <summary>가시 덫: 품삯 궤짝에서 이 거리 안을 먼저(4-6).</summary>
            const float WageNearMin = 1.5f;
            const float WageNearMax = 4f;

            readonly FloorMap _map;
            readonly FloorRecipe _recipe;
            readonly GeneratorInput _input;
            readonly SpiceReport _report;
            readonly Pcg32Random _rng;
            readonly int _floor;
            readonly MapCell _landing;
            readonly MapCell _stairs;
            readonly MapCell _first;
            readonly HashSet<MapCell> _onMain = new HashSet<MapCell>();
            readonly HashSet<MapCell> _start;
            readonly HashSet<string> _ids = new HashSet<string>();
            readonly Dictionary<CellDef, int> _legendIndex = new Dictionary<CellDef, int>();

            public bool Ready { get; }

            public Spicer(GeneratedFloor g, FloorMap map, FloorRecipe recipe, GeneratorInput input, SpiceReport report)
            {
                _map = map;
                _recipe = recipe;
                _input = input;
                _report = report;
                _floor = recipe.Floor;
                _rng = new Pcg32Random(g.Seed, Stream);
                for (int i = 0; i < g.Legend.Length; i++)
                    if (g.Legend[i] != null) _legendIndex[g.Legend[i]] = i;
                _landing = MapAnchors.FindLanding(map);
                _stairs = MapAnchors.FindStairsCell(map);
                var main = FloorRules.MainPath(map);
                if (_landing == null || _stairs == null || main.Count < 2)
                {
                    _start = new HashSet<MapCell>();
                    return;
                }
                _first = main[1];
                foreach (var c in main) _onMain.Add(c);
                _start = map.Reachable(_landing, FloorMap.StartPassable);
                foreach (var c in map.Cells)
                    foreach (var f in c.Features)
                        if (!string.IsNullOrEmpty(f.Id)) _ids.Add(f.Id);
                Ready = true;
            }

            /// <summary>다시 연 층 차례(4-2·4-3·4-7·4-9·4-5·4-6).</summary>
            public void Reopened()
            {
                // ① 정예 굴림(4-2) — 확률이 0이나 1000이어도 늘 한 번 굴려 뒤 차례가 밀리지 않게.
                bool elite = _rng.NextInt(0, 1000) < ElitePermille(_floor, _input);

                // ② 첫 공터를 뺀 무리(범례 차례). 정예 자리: 주 길 밖 무리 칸 가운데 계단에서 열린 길·판자벽 걸음이 가장 가까운 칸(같으면 범례 차례),
                //    주 길 밖 무리가 없으면 주 길 위에서 같은 규칙.
                var groups = new List<GroupSpot>();
                foreach (var c in Ordered(_map.Cells))
                {
                    if (c == _first) continue;
                    foreach (var f in c.Features)
                        if (f.Kind == FeatureKind.Group) groups.Add(new GroupSpot { Cell = c, Feature = f });
                }
                int eliteSlot = elite ? EliteSlot(groups) : -1;

                // ③ 후보 표에서 구성·상태를 고른다(첫 공터는 그대로, 다른 무리의 정예 표시는 고른 줄로 다시 씀).
                //    자는 무리는 생성기 PutGroup 규칙대로 들어오는 문 반대쪽을 본다(등 뒤로 다가갈 틈, 물결 2 고침). 난수를 쓰지 않아 흐름 4 차례는 그대로다.
                //    먹는 중·순찰은 생성기 방향 그대로 둔다(생성기의 깬 무리 방향은 문 쪽만 피하고, 잠의 반대쪽도 깬 무리 방향으로 맞다).
                var picks = PackTable.Pick(_floor, groups.Count, FirstMix(_recipe), eliteSlot, _rng);
                var entry = EntrySides();
                for (int i = 0; i < groups.Count; i++)
                {
                    var o = picks[i];
                    var f = groups[i].Feature;
                    groups[i].Option = o;
                    f.Boars = o.Boars;
                    f.Archers = o.Archers;
                    f.Rats = o.Rats;
                    f.State = o.State;
                    f.Label = o.Label;
                    f.Elite = o.Elite;
                    f.PatrolCell = "";
                    if (o.State == GroupState.Sleep && entry.TryGetValue(groups[i].Cell, out int side)) f.FacingDeg = ((side + 2) % 4) * 90f;
                }

                // ④ 최고 − 3 이하 다시 연 층: 무리마다 강한 적 1마리 이하(생성기 PutGroup과 같은 규칙).
                if (FloorBudget.For(_recipe, _input).WeakGroups)
                {
                    foreach (var spot in groups)
                    {
                        var f = spot.Feature;
                        if (f.Boars + f.Archers <= 1) continue;
                        if (f.Boars > 0)
                        {
                            f.Boars = 1;
                            f.Archers = 0;
                        }
                        else f.Archers = 1;
                    }
                }

                // ⑤ 순찰 이웃 칸(4-3). 이웃이 없으면 열린 문 둘 이상이면 제 칸 안, 그것도 아니면 먹는 중으로 바꾼다.
                foreach (var spot in groups)
                    if (spot.Feature.State == GroupState.Patrol) ResolvePatrol(spot);

                // ⑥ 새 쥐굴 밤(4-9): 순찰이 없으면 굴쥐만 있는 무리 하나(순찰할 수 있는 것)를 순찰로.
                if (_input.Night == NightEvent.RatBurrow && !AnyPatrol(groups))
                {
                    var rats = new List<GroupSpot>();
                    foreach (var spot in groups)
                    {
                        var f = spot.Feature;
                        if (f.Boars == 0 && f.Archers == 0 && f.Rats > 0 && !f.Elite && CanPatrol(spot.Cell)) rats.Add(spot);
                    }
                    if (rats.Count > 0)
                    {
                        var spot = rats[_rng.NextInt(0, rats.Count)];
                        spot.Feature.State = GroupState.Patrol;
                        spot.Feature.Label = ExploreText.PackRatPatrol;
                        ResolvePatrol(spot);
                    }
                }

                // ⑦ 광부 품삯 궤짝(4-7 ①): 처음 갈 수 있는 일반 칸 가운데 열린 길·판자벽이 하나뿐인 칸의 일반 나무 궤짝 모두.
                foreach (var c in Ordered(_map.Cells))
                {
                    if (!_start.Contains(c) || !IsGeneric(c.Piece) || PassableDoors(c) != 1) continue;
                    foreach (var f in c.Features)
                    {
                        if (f.Kind != FeatureKind.WoodChest || !string.IsNullOrEmpty(f.Param)) continue;
                        f.Param = CornerLoot.WageParam;
                        _report.WageChests++;
                    }
                }

                // ⑧ 광부 유품 상자(4-7 ②): 숨은 방의 일반 나무 궤짝.
                foreach (var c in Ordered(_map.Cells))
                {
                    if (c.Piece != PieceKind.Hidden) continue;
                    foreach (var f in c.Features)
                    {
                        if (f.Kind != FeatureKind.WoodChest || !string.IsNullOrEmpty(f.Param)) continue;
                        f.Param = CornerLoot.KeepsakeParam;
                        _report.Keepsakes++;
                    }
                }

                // ⑨ 드러남 밤(4-9): 처음 갈 수 있고 숨은 방이 아닌 칸의 일반 쇠 궤짝 하나를 흙 묻은 쇠 궤짝으로.
                if (_input.Night == NightEvent.Upheaval)
                {
                    var irons = new List<CellFeature>();
                    foreach (var c in Ordered(_map.Cells))
                    {
                        if (!_start.Contains(c) || c.Piece == PieceKind.Hidden) continue;
                        foreach (var f in c.Features)
                            if (f.Kind == FeatureKind.IronChest && string.IsNullOrEmpty(f.Param)) irons.Add(f);
                    }
                    if (irons.Count > 0)
                    {
                        irons[_rng.NextInt(0, irons.Count)].Param = CornerLoot.BuriedParam;
                        _report.Buried++;
                    }
                }

                // ⑩ 낙석(4-5), ⑪ 가시 덫(4-6).
                PlaceRockfalls(TrapRules.RockfallCount(_floor, false, _input.Night, _rng.NextInt(0, 1000), _recipe), false);
                PlaceSpikes(TrapRules.SpikeCount(_floor, false, _rng.NextInt(0, 1000)));
            }

            /// <summary>정예·순찰 수를 덧칠 뒤 지도 전체에서 센다.</summary>
            public void Finish()
            {
                foreach (var c in _map.Cells)
                    foreach (var f in c.Features)
                    {
                        if (f.Kind != FeatureKind.Group) continue;
                        if (f.Elite) _report.Elites++;
                        if (f.State == GroupState.Patrol) _report.Patrols++;
                    }
            }

            // ── 정예·순찰 ──

            sealed class GroupSpot
            {
                public MapCell Cell;
                public CellFeature Feature;
                public PackOption Option;
            }

            int EliteSlot(List<GroupSpot> groups)
            {
                if (groups.Count == 0) return -1;
                var steps = StepsFrom(_stairs);
                int best = -1;
                for (int pass = 0; pass < 2 && best < 0; pass++)
                {
                    bool wantOffMain = pass == 0;
                    int bestSteps = int.MaxValue;
                    for (int i = 0; i < groups.Count; i++)
                    {
                        if (_onMain.Contains(groups[i].Cell) == wantOffMain) continue;
                        int d = steps.TryGetValue(groups[i].Cell, out int v) ? v : int.MaxValue;
                        // groups는 범례 차례라 같은 걸음이면 먼저 나온 칸이 남는다.
                        if (best >= 0 && d >= bestSteps) continue;
                        best = i;
                        bestSteps = d;
                    }
                }
                return best;
            }

            void ResolvePatrol(GroupSpot spot)
            {
                var f = spot.Feature;
                var near = PatrolNeighbors(spot.Cell);
                if (near.Count > 0)
                {
                    f.PatrolCell = near[_rng.NextInt(0, near.Count)].Id;
                    return;
                }
                f.PatrolCell = "";
                if (OpenDoors(spot.Cell) >= 2) return;
                f.State = GroupState.Eat;
                if (spot.Option != null && !string.IsNullOrEmpty(spot.Option.FallbackLabel)) f.Label = spot.Option.FallbackLabel;
            }

            bool CanPatrol(MapCell cell) => PatrolNeighbors(cell).Count > 0 || OpenDoors(cell) >= 2;

            /// <summary>
            /// 순찰이 오갈 이웃 칸(4-3): 열린 길로 이어진 처음 갈 수 있는 칸, 조각 공터·통로·곁방, 무리·둥지·말뚝 없음,
            /// 승강장·계단 앞·숨은 방·랜드마크 아님. 범례 차례.
            /// </summary>
            List<MapCell> PatrolNeighbors(MapCell cell)
            {
                var list = new List<MapCell>();
                foreach (var e in cell.Edges)
                {
                    if (e.Kind != EdgeKind.Open) continue;
                    var n = e.Other(cell);
                    if (n == null || n == _landing || n == _stairs || !_start.Contains(n) || !IsGeneric(n.Piece)) continue;
                    if (n.Has(FeatureKind.Group) || n.Has(FeatureKind.Nest) || n.Has(FeatureKind.Stake)) continue;
                    if (!list.Contains(n)) list.Add(n);
                }
                return Ordered(list);
            }

            static bool AnyPatrol(List<GroupSpot> groups)
            {
                foreach (var spot in groups)
                    if (spot.Feature.State == GroupState.Patrol) return true;
                return false;
            }

            // ── 함정(4-5·4-6) ──

            /// <summary>
            /// 낙석 count개. 칸 차례: 통로 → 무리·둥지 없는 공터 → 나머지(각각 섞음). offMainFirst(층 첫 방문)면 주 길 밖 칸에서 먼저 같은 차례로 고른다.
            /// 자리: 사건 슬롯, 무리·둥지 가운데에서 TrapRules.RockMinGroupGap, 다른 물건에서 RockMinThingGap 이상. 자리가 없으면 건너뛰고 Skipped.
            /// </summary>
            public void PlaceRockfalls(int count, bool offMainFirst)
            {
                for (int k = 0; k < count; k++)
                {
                    var corridors = new List<MapCell>();
                    var quiet = new List<MapCell>();
                    var rest = new List<MapCell>();
                    var offCorridors = new List<MapCell>();
                    var offQuiet = new List<MapCell>();
                    var offRest = new List<MapCell>();
                    foreach (var c in Ordered(_map.Cells))
                    {
                        if (!TrapCell(c, FeatureKind.RockfallTrap)) continue;
                        bool off = offMainFirst && !_onMain.Contains(c);
                        if (c.Piece == PieceKind.Corridor) (off ? offCorridors : corridors).Add(c);
                        else if (c.Piece == PieceKind.Clearing && !c.Has(FeatureKind.Group) && !c.Has(FeatureKind.Nest)) (off ? offQuiet : quiet).Add(c);
                        else (off ? offRest : rest).Add(c);
                    }
                    var tiers = new[] { offCorridors, offQuiet, offRest, corridors, quiet, rest };
                    if (PlaceInTiers(tiers, FeatureKind.RockfallTrap)) _report.Rockfalls++;
                    else _report.Skipped++;
                }
            }

            /// <summary>
            /// 가시 덫 count개. 칸 차례: 품삯 궤짝이 있는 칸 → 통로 → 나머지(각각 섞음).
            /// 자리: 작은 물건 → 사건 슬롯, 무리·둥지 가운데에서 TrapRules.SpikeMinGroupGap, 다른 물건에서 SpikeMinThingGap 이상.
            /// 품삯 칸이면 그 궤짝에서 1.5~4 안 자리를 먼저. 자리가 없으면 건너뛰고 Skipped.
            /// </summary>
            void PlaceSpikes(int count)
            {
                for (int k = 0; k < count; k++)
                {
                    var wage = new List<MapCell>();
                    var corridors = new List<MapCell>();
                    var rest = new List<MapCell>();
                    foreach (var c in Ordered(_map.Cells))
                    {
                        if (!TrapCell(c, FeatureKind.FloorSpikes)) continue;
                        if (WageChests(c).Count > 0) wage.Add(c);
                        else if (c.Piece == PieceKind.Corridor) corridors.Add(c);
                        else rest.Add(c);
                    }
                    if (PlaceInTiers(new[] { wage, corridors, rest }, FeatureKind.FloorSpikes)) _report.Spikes++;
                    else _report.Skipped++;
                }
            }

            bool PlaceInTiers(List<MapCell>[] tiers, FeatureKind kind)
            {
                foreach (var tier in tiers)
                {
                    Shuffle(tier, _rng);
                    foreach (var c in tier)
                        if (TryPlace(c, kind)) return true;
                }
                return false;
            }

            /// <summary>
            /// 함정을 둘 수 있는 칸: 처음 갈 수 있는 칸, 조각 공터·통로·곁방, 주 길[1](첫 공터) 아님, 말뚝·계단 없음, 같은 종류 함정 없음
            /// (승강장·계단 앞·숨은 방·랜드마크는 조각에서 빠진다).
            /// </summary>
            bool TrapCell(MapCell c, FeatureKind kind) =>
                _start.Contains(c) && c != _first && IsGeneric(c.Piece) &&
                !c.Has(FeatureKind.Stake) && !c.Has(FeatureKind.Stairs) && !c.Has(kind);

            bool TryPlace(MapCell c, FeatureKind kind)
            {
                bool rock = kind == FeatureKind.RockfallTrap;
                float groupGap = rock ? TrapRules.RockMinGroupGap : TrapRules.SpikeMinGroupGap;
                float thingGap = rock ? TrapRules.RockMinThingGap : TrapRules.SpikeMinThingGap;
                var slotOrder = rock ? new[] { SlotKind.Event } : new[] { SlotKind.Small, SlotKind.Event };
                var wage = rock ? new List<CellFeature>() : WageChests(c);
                // 품삯 칸의 가시 덫: 궤짝 앞 1.5~4 자리를 먼저(작은 물건 → 사건), 없으면 칸 어디든(작은 물건 → 사건).
                for (int pass = wage.Count > 0 ? 0 : 1; pass < 2; pass++)
                {
                    foreach (var slotKind in slotOrder)
                    {
                        var options = new List<PieceSlot>();
                        foreach (var slot in PieceSlots.SlotsFor(c.Piece, c.Def.Pillars))
                        {
                            if (slot.Kind != slotKind || !PieceSlots.Usable(slot, side => HasDoor(c, side))) continue;
                            if (!Clear(c, slot.Local, groupGap, thingGap)) continue;
                            if (pass == 0 && !NearAny(slot.Local, wage)) continue;
                            options.Add(slot);
                        }
                        if (options.Count == 0) continue;
                        var pick = options[_rng.NextInt(0, options.Count)];
                        Add(c, kind, pick.Local, rock ? "rockfall" : "spikes", rock ? ExploreText.RockfallLabel : ExploreText.SpikesLabel);
                        return true;
                    }
                }
                return false;
            }

            /// <summary>
            /// 무리·둥지 가운데에서 groupGap, 다른 물건에서 thingGap 이상인가. 이미 놓인 함정과는 두 함정 규칙 가운데 큰 거리를 지킨다
            /// (가시 덫이 낙석 잔돌 2.2 안에 들지 않게 — 낙석에서 2.5).
            /// </summary>
            static bool Clear(MapCell c, Offset p, float groupGap, float thingGap)
            {
                foreach (var f in c.Features)
                {
                    float need = f.Kind == FeatureKind.Group || f.Kind == FeatureKind.Nest ? groupGap : thingGap;
                    if (f.Kind == FeatureKind.RockfallTrap) need = Math.Max(need, TrapRules.RockMinThingGap);
                    else if (f.Kind == FeatureKind.FloorSpikes) need = Math.Max(need, TrapRules.SpikeMinThingGap);
                    if (Distance(f.Local, p) < need - Eps) return false;
                }
                return true;
            }

            static bool NearAny(Offset p, List<CellFeature> anchors)
            {
                foreach (var a in anchors)
                {
                    float d = Distance(a.Local, p);
                    if (d >= WageNearMin - Eps && d <= WageNearMax + Eps) return true;
                }
                return false;
            }

            static List<CellFeature> WageChests(MapCell c)
            {
                var list = new List<CellFeature>();
                foreach (var f in c.Features)
                    if (f.Kind == FeatureKind.WoodChest && f.Param == CornerLoot.WageParam) list.Add(f);
                return list;
            }

            /// <summary>자리 표시를 칸 범례 끝에 더한다(Features를 새 배열로 바꿈). id는 'f{층}.{칸 id}.{이름}', 겹치면 2, 3…</summary>
            void Add(MapCell c, FeatureKind kind, Offset local, string name, string label)
            {
                var feature = new CellFeature { Kind = kind, Local = local, Id = NewId(c, name), Label = label ?? "" };
                var old = c.Def.Features ?? Array.Empty<CellFeature>();
                var features = new CellFeature[old.Length + 1];
                Array.Copy(old, features, old.Length);
                features[old.Length] = feature;
                c.Def.Features = features;
            }

            string NewId(MapCell c, string name)
            {
                string baseId = "f" + _floor + "." + c.Id + "." + name;
                string id = baseId;
                for (int k = 2; _ids.Contains(id) || FloorRecipe.IsOnceItem(id) || id == FloorRules.LandingStakeId(_floor); k++) id = baseId + k;
                _ids.Add(id);
                return id;
            }

            // ── 칸 도우미 ──

            /// <summary>함정·품삯·순찰 이웃에 쓰는 일반 칸 조각(공터·통로·곁방).</summary>
            static bool IsGeneric(PieceKind piece) =>
                piece == PieceKind.Clearing || piece == PieceKind.Corridor || piece == PieceKind.Room;

            static bool HasDoor(MapCell c, Side side)
            {
                foreach (var e in c.Edges)
                    if (e.SideFrom(c) == side) return true;
                return false;
            }

            static int OpenDoors(MapCell c)
            {
                int n = 0;
                foreach (var e in c.Edges)
                    if (e.Kind == EdgeKind.Open) n++;
                return n;
            }

            /// <summary>열린 길·판자벽 수(곡괭이·열쇠 없이 지나갈 문, 막다른 곳 판정).</summary>
            static int PassableDoors(MapCell c)
            {
                int n = 0;
                foreach (var e in c.Edges)
                    if (FloorMap.StartPassable(e.Kind)) n++;
                return n;
            }

            /// <summary>
            /// 칸마다 들어오는 문 쪽((int)Side). 생성기 ReadMainPath와 같은 규칙: 승강장에서 열린 길·판자벽만 지나는 너비 우선,
            /// 칸마다 방향 차례 Right·Up·Left·Down으로 처음 닿은 문. 그 길로 못 가는 칸은 방향 차례 마지막 문. 승강장과 문 없는 칸은 넣지 않는다.
            /// </summary>
            Dictionary<MapCell, int> EntrySides()
            {
                var entry = new Dictionary<MapCell, int>();
                var seen = new HashSet<MapCell> { _landing };
                var queue = new Queue<MapCell>();
                queue.Enqueue(_landing);
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    for (int s = 0; s < 4; s++)
                    {
                        var e = EdgeOn(c, (Side)s);
                        if (e == null || !FloorMap.StartPassable(e.Kind)) continue;
                        var n = e.Other(c);
                        if (!seen.Add(n)) continue;
                        entry[n] = (s + 2) % 4;
                        queue.Enqueue(n);
                    }
                }
                foreach (var c in _map.Cells)
                {
                    if (seen.Contains(c)) continue;
                    for (int s = 3; s >= 0; s--)
                    {
                        if (EdgeOn(c, (Side)s) == null) continue;
                        entry[c] = s;
                        break;
                    }
                }
                return entry;
            }

            static MapEdge EdgeOn(MapCell c, Side side)
            {
                foreach (var e in c.Edges)
                    if (e.SideFrom(c) == side) return e;
                return null;
            }

            /// <summary>열린 길·판자벽만 지나는 걸음 수.</summary>
            Dictionary<MapCell, int> StepsFrom(MapCell from)
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

            /// <summary>범례 차례로 늘어놓은 새 목록.</summary>
            List<MapCell> Ordered(IEnumerable<MapCell> cells)
            {
                var list = new List<MapCell>(cells);
                list.Sort((a, b) => LegendIndex(a).CompareTo(LegendIndex(b)));
                return list;
            }

            int LegendIndex(MapCell c) => c != null && c.Def != null && _legendIndex.TryGetValue(c.Def, out int i) ? i : int.MaxValue;

            static float Distance(Offset a, Offset b)
            {
                float dx = a.X - b.X, dy = a.Y - b.Y;
                return (float)Math.Sqrt(dx * dx + dy * dy);
            }

            static void Shuffle(List<MapCell> list, IRandom rng)
            {
                for (int i = list.Count - 1; i > 0; i--)
                {
                    int j = rng.NextInt(0, i + 1);
                    var t = list[i];
                    list[i] = list[j];
                    list[j] = t;
                }
            }

            /// <summary>첫 공터 무리(층 예산의 FirstClearing, 없으면 첫 무리). 생성기와 같은 규칙.</summary>
            static GroupMix FirstMix(FloorRecipe recipe)
            {
                foreach (var m in recipe.Groups)
                    if (m != null && m.FirstClearing) return m;
                return recipe.Groups.Length > 0 ? recipe.Groups[0] : null;
            }
        }
    }
}
