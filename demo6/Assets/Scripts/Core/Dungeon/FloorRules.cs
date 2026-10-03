using System.Collections.Generic;
using System.Text;

namespace Demo6.Core.Dungeon
{
    /// <summary>합격 검사 한 번의 결과(매판 새 탐험 1차 2-5 검사 표). 떨어진 규칙마다 한 줄.</summary>
    public sealed class RuleReport
    {
        public readonly List<string> Failures = new List<string>();
        public bool Passed => Failures.Count == 0;
        /// <summary>처음 갈 수 있는 칸 수(열린 길 + 판자벽).</summary>
        public int StartReachable;
        /// <summary>주 길 칸 수(승강장 → 계단, 열린 길만, 양 끝 포함).</summary>
        public int MainPathLength;
        /// <summary>한 바퀴 도는 길 수(열린 길 그래프의 독립 고리 수).</summary>
        public int Loops;

        public void Fail(string rule) => Failures.Add(rule);

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Passed ? "합격" : "불합격").Append($" — 처음 갈 수 있는 칸 {StartReachable}, 주 길 {MainPathLength}, 고리 {Loops}");
            foreach (var f in Failures) sb.Append("\n  · ").Append(f);
            return sb.ToString();
        }
    }

    /// <summary>
    /// 생성기 합격 조건(매판 새 탐험 1차 2-5 검사 표 = 지금 FloorMapTests 규칙의 씨앗 판 + 강제 규칙).
    /// 손 지도(FloorOneMap)는 정답 예시라 반드시 통과해야 한다. 순수 함수라 EditMode 테스트로 확인한다.
    /// 규칙: ① 열린 길과 판자벽만으로 계단 도달(승강장 말뚝 포함) ② 주 길 = 처음 갈 수 있는 칸의 40~50%
    /// ③ 막다른 칸(금 간 벽·자물쇠 문은 곡괭이·열쇠가 있을 때만 출구로 셈)은 주 길에서 2칸 이하, 보상 100% ④ 말뚝 사이 열린 길 5걸음 이하 ⑤ 능력 문 뒤에 계단·명패 없음
    /// ⑥ 주 길 위 마주침 칸 3칸 연달아 금지 ⑦ 주 길 위 새 것 없는 칸 3칸 넘게 연달아 금지 ⑧ 자리 id 겹침 없음
    /// ⑨ 개수 = 층 예산 ⑩ 격자 상한 ⑪ 고리 수. 3차 '전투 공터끼리 이웃 금지'는 손 지도도 지키지 않아 ⑥으로 바꿨다(2-5 주석).
    /// 랜드마크(자물쇠 뒤 고정 칸)는 승강장이 아닌 이웃 칸 하나와, 층 예산이 정한 쪽(LandmarkDoor)의 자물쇠 문으로만 이어져야 한다.
    /// </summary>
    public static class FloorRules
    {
        /// <summary>막다른 갈래 줄기 상한(주 길에서 떨어진 칸 수).</summary>
        public const int MaxDeadEndStem = 2;
        /// <summary>말뚝 사이 열린 길 걸음 상한.</summary>
        public const int MaxStakeSteps = 5;
        /// <summary>주 길 위 마주침 칸이 이만큼 연달아 있으면 떨어짐.</summary>
        public const int MaxEncounterRun = 2;
        /// <summary>주 길 위 새 것 없는 칸 연달음 상한.</summary>
        public const int MaxEmptyRun = 3;

        /// <summary>층 첫 방문 개수로 검사한다(손 지도·고른 지도 검사).</summary>
        public static RuleReport Check(FloorMap map, FloorRecipe recipe) => Check(map, recipe, FirstVisitInput(recipe));

        /// <summary>
        /// 모든 규칙 검사. 개수(⑨)는 이번 입력(첫 방문·다시 연 층 감쇠·밤 사건·한 번 받은 것)에 맞춘 층 예산(FloorBudget)과 비교한다.
        /// recipe가 null이면 층 예산이 필요한 규칙(⑨⑩⑪)은 건너뛴다.
        /// </summary>
        public static RuleReport Check(FloorMap map, FloorRecipe recipe, GeneratorInput input)
        {
            var report = new RuleReport();
            if (map == null)
            {
                report.Fail("지도 없음");
                return report;
            }
            var landing = MapAnchors.FindLanding(map);
            if (landing == null)
            {
                report.Fail("승강장(입구 조각) 칸 없음");
                return report;
            }
            if (input == null) input = FirstVisitInput(recipe);

            var start = map.Reachable(landing, FloorMap.StartPassable);
            var stairs = MapAnchors.FindStairsCell(map);
            var main = MainPath(map);
            report.StartReachable = start.Count;
            report.MainPathLength = main.Count;
            report.Loops = CountLoops(map);

            CheckReach(map, report, landing, stairs, start);
            CheckMainPathShare(report, main, start);
            CheckDeadEnds(map, report, landing, stairs, main, start, input);
            CheckStakes(map, report);
            CheckBehindDoors(map, report, start);
            CheckMainPathRuns(report, main);
            CheckIds(map, report, landing);
            CheckLandmark(map, report, landing, recipe);
            if (recipe != null)
            {
                CheckCounts(map, report, FloorBudget.For(recipe, input), start.Count);
                if (map.Width > recipe.MaxWidth || map.Height > recipe.MaxHeight)
                    report.Fail($"격자 {map.Width}×{map.Height}: 상한 {recipe.MaxWidth}×{recipe.MaxHeight} 넘음");
                if (report.Loops < recipe.MinLoops)
                    report.Fail($"고리 {report.Loops}개: 적어도 {recipe.MinLoops}개");
            }
            return report;
        }

        /// <summary>주 길: 승강장 → 계단 칸 가장 짧은 길(열린 길만, 양 끝 포함). 없으면 빈 목록.</summary>
        public static List<MapCell> MainPath(FloorMap map)
        {
            var landing = MapAnchors.FindLanding(map);
            var stairs = MapAnchors.FindStairsCell(map);
            if (landing == null || stairs == null) return new List<MapCell>();
            return map.ShortestPath(landing, stairs, FloorMap.OpenOnly);
        }

        /// <summary>고리 수 = 열린 길 수 − 칸 수 + 열린 길로 이어진 덩어리 수(홀로 떨어진 칸도 한 덩어리).</summary>
        public static int CountLoops(FloorMap map)
        {
            if (map == null) return 0;
            int open = 0;
            foreach (var e in map.Edges)
                if (e.Kind == EdgeKind.Open) open++;
            var seen = new HashSet<MapCell>();
            int parts = 0;
            foreach (var c in map.Cells)
            {
                if (seen.Contains(c)) continue;
                parts++;
                foreach (var r in map.Reachable(c, FloorMap.OpenOnly)) seen.Add(r);
            }
            return open - map.Cells.Count + parts;
        }

        /// <summary>막다른 곳 보상으로 치는 것(FloorMapTests.DeadEndsAlwaysReward와 같은 목록).</summary>
        public static bool IsReward(FeatureKind kind) =>
            kind == FeatureKind.WoodChest || kind == FeatureKind.IronChest || kind == FeatureKind.Nameplate ||
            kind == FeatureKind.Pickaxe || kind == FeatureKind.Safe;

        /// <summary>마주침 칸으로 치는 것(무리·둥지).</summary>
        public static bool IsEncounter(FeatureKind kind) => kind == FeatureKind.Group || kind == FeatureKind.Nest;

        /// <summary>'새 것'으로 치는 것. 등잔·말뚝·계단은 새 것으로 세지 않는다(2-5 검사 표, 3차 2-8 '등잔 +1' 꼼수 막기).</summary>
        public static bool IsNewThing(FeatureKind kind) =>
            kind != FeatureKind.WallLamp && kind != FeatureKind.Stake && kind != FeatureKind.Stairs;

        /// <summary>층 첫 방문 입력(2인자 검사가 쓰는 기준).</summary>
        public static GeneratorInput FirstVisitInput(FloorRecipe recipe)
        {
            int floor = recipe != null ? recipe.Floor : 1;
            return new GeneratorInput { Floor = floor, FirstVisit = true, DeepestFloor = floor };
        }

        /// <summary>승강장 말뚝 id(켠 승강장 영구 기록의 열쇠).</summary>
        public static string LandingStakeId(int floor) => "f" + floor + ".E.stake";

        // ① 승강장 말뚝이 있고, 열린 길 + 판자벽만으로 계단 칸에 닿는다.
        static void CheckReach(FloorMap map, RuleReport report, MapCell landing, MapCell stairs, HashSet<MapCell> start)
        {
            var stake = MapAnchors.StakeIn(landing);
            string stakeId = LandingStakeId(map.Floor);
            if (stake == null) report.Fail("승강장 말뚝 없음");
            else if (stake.Id != stakeId) report.Fail($"승강장 말뚝 id {stake.Id}: {stakeId}여야 함");
            if (stairs == null) report.Fail("계단 칸 없음");
            else if (!start.Contains(stairs)) report.Fail($"계단 칸 {stairs.Id}: 열린 길과 판자벽만으로 갈 수 없음");
        }

        // ② 주 길 칸 수 / 처음 갈 수 있는 칸 수 = 0.4~0.5(정수 비교).
        static void CheckMainPathShare(RuleReport report, List<MapCell> main, HashSet<MapCell> start)
        {
            if (main.Count == 0)
            {
                report.Fail("주 길(승강장 → 계단, 열린 길만) 없음");
                return;
            }
            int n = main.Count, total = start.Count;
            if (n * 5 < total * 2 || n * 2 > total)
                report.Fail($"주 길 {n}칸: 처음 갈 수 있는 칸 {total}의 40~50% 밖");
        }

        /// <summary>
        /// 이번 입력으로 지나갈 수 있는 문 수: 열린 길·판자벽, 곡괭이가 있으면 금 간 벽, 열쇠가 있으면 자물쇠 문까지 센다.
        /// </summary>
        public static int PassableExits(MapCell cell, GeneratorInput input)
        {
            if (cell == null) return 0;
            int n = 0;
            foreach (var e in cell.Edges)
            {
                switch (e.Kind)
                {
                    case EdgeKind.Open:
                    case EdgeKind.Plank:
                        n++;
                        break;
                    case EdgeKind.Cracked:
                        if (input != null && input.HasPickaxe) n++;
                        break;
                    case EdgeKind.Locked:
                        if (input != null && input.HasKey) n++;
                        break;
                }
            }
            return n;
        }

        // ③ 막다른 칸(승강장·계단 칸 제외): 문이 하나뿐인 칸, 또는 처음 갈 수 있는 칸 가운데 이번 입력으로 지나갈 수 있는 문이 하나뿐인 칸
        //    (금 간 벽·자물쇠 문은 곡괭이·열쇠가 있을 때만 출구로 셈 — 곡괭이 없이 금 간 벽만 보이는 막다른 곳도 빈손이면 안 됨). 주 길에서 2칸 이하, 보상 있음.
        static void CheckDeadEnds(FloorMap map, RuleReport report, MapCell landing, MapCell stairs, List<MapCell> main, HashSet<MapCell> start,
            GeneratorInput input)
        {
            var dist = main.Count > 0 ? DistanceFrom(main) : null;
            foreach (var c in map.Cells)
            {
                if (c == landing || c == stairs) continue;
                bool deadEnd = c.Edges.Count == 1 || (start.Contains(c) && PassableExits(c, input) == 1);
                if (!deadEnd) continue;
                if (dist != null)
                {
                    if (!dist.TryGetValue(c, out int d)) report.Fail($"막다른 칸 {c.Id}: 주 길과 이어지지 않음");
                    else if (d > MaxDeadEndStem) report.Fail($"막다른 칸 {c.Id}: 주 길에서 {d}칸 떨어짐({MaxDeadEndStem}칸 이하)");
                }
                bool reward = false;
                foreach (var f in c.Features)
                    if (IsReward(f.Kind)) reward = true;
                if (!reward) report.Fail($"막다른 칸 {c.Id}: 보상 없음");
            }
        }

        // ④ 말뚝이 있는 칸끼리 열린 길 5걸음 이하로 모두 이어진다(승강장 포함).
        static void CheckStakes(FloorMap map, RuleReport report)
        {
            var stakes = new List<MapCell>();
            foreach (var c in map.Cells)
                if (c.Has(FeatureKind.Stake)) stakes.Add(c);
            if (stakes.Count < 2) return;
            var joined = new HashSet<MapCell> { stakes[0] };
            var queue = new Queue<MapCell>();
            queue.Enqueue(stakes[0]);
            while (queue.Count > 0)
            {
                var s = queue.Dequeue();
                var d = StepsFrom(s);
                foreach (var t in stakes)
                {
                    if (joined.Contains(t)) continue;
                    if (d.TryGetValue(t, out int steps) && steps <= MaxStakeSteps)
                    {
                        joined.Add(t);
                        queue.Enqueue(t);
                    }
                }
            }
            if (joined.Count == stakes.Count) return;
            var sb = new StringBuilder();
            foreach (var t in stakes)
                if (!joined.Contains(t)) sb.Append(sb.Length > 0 ? ", " : "").Append(t.Id);
            report.Fail($"말뚝 사이 열린 길 {MaxStakeSteps}걸음 넘음: {stakes[0].Id}에서 {sb}");
        }

        // ⑤ 처음 갈 수 없는 칸(능력 문·자물쇠 뒤)에 계단·명패 없음.
        static void CheckBehindDoors(FloorMap map, RuleReport report, HashSet<MapCell> start)
        {
            foreach (var c in map.Cells)
            {
                if (start.Contains(c)) continue;
                if (c.Has(FeatureKind.Stairs)) report.Fail($"처음 갈 수 없는 칸 {c.Id}: 계단 있음");
                if (c.Has(FeatureKind.Nameplate)) report.Fail($"처음 갈 수 없는 칸 {c.Id}: 명패 있음");
            }
        }

        // ⑥ 주 길 위 마주침 칸 3칸 연달아 금지, ⑦ 주 길 위 새 것 없는 칸 3칸 넘게 연달아 금지.
        static void CheckMainPathRuns(RuleReport report, List<MapCell> main)
        {
            var encounterRun = new List<string>();
            var emptyRun = new List<string>();
            List<string> longestEncounter = new List<string>(), longestEmpty = new List<string>();
            foreach (var c in main)
            {
                bool encounter = false, fresh = false;
                foreach (var f in c.Features)
                {
                    if (IsEncounter(f.Kind)) encounter = true;
                    if (IsNewThing(f.Kind)) fresh = true;
                }
                if (encounter) encounterRun.Add(c.Id);
                else encounterRun.Clear();
                if (!fresh) emptyRun.Add(c.Id);
                else emptyRun.Clear();
                if (encounterRun.Count > longestEncounter.Count) longestEncounter = new List<string>(encounterRun);
                if (emptyRun.Count > longestEmpty.Count) longestEmpty = new List<string>(emptyRun);
            }
            if (longestEncounter.Count > MaxEncounterRun)
                report.Fail($"주 길 위 마주침 칸 {longestEncounter.Count}칸 연달음({string.Join("–", longestEncounter)})");
            if (longestEmpty.Count > MaxEmptyRun)
                report.Fail($"주 길 위 새 것 없는 칸 {longestEmpty.Count}칸 연달음({string.Join("–", longestEmpty)})");
        }

        // ⑧ 자리 id 겹침 없음. 한 번 받는 것 id는 그 물건(같은 종류)에만, 승강장 말뚝 id는 승강장 말뚝에만 쓴다.
        static void CheckIds(FloorMap map, RuleReport report, MapCell landing)
        {
            var seen = new HashSet<string>();
            string landingStake = LandingStakeId(map.Floor);
            foreach (var c in map.Cells)
            {
                foreach (var f in c.Features)
                {
                    if (string.IsNullOrEmpty(f.Id))
                    {
                        report.Fail($"칸 {c.Id}: {f.Kind} 자리 id 비어 있음");
                        continue;
                    }
                    if (!seen.Add(f.Id)) report.Fail($"자리 id {f.Id}: 겹침");
                    var once = FloorRecipe.FindOnceItem(f.Id);
                    if (once != null && once.Kind != f.Kind) report.Fail($"일반 자리 id {f.Id}: 한 번 받는 것 id와 겹침");
                    if (f.Id == landingStake && (f.Kind != FeatureKind.Stake || c != landing))
                        report.Fail($"일반 자리 id {f.Id}: 승강장 말뚝 id와 겹침");
                }
            }
        }

        // 랜드마크: 자물쇠 문 하나로만, 승강장이 아닌 이웃 칸과 이어진다. 층 예산이 있으면 문은 정한 쪽(LandmarkDoor)에만 난다(돌로 지은 방).
        static void CheckLandmark(FloorMap map, RuleReport report, MapCell landing, FloorRecipe recipe)
        {
            var landmark = MapAnchors.FindLandmark(map);
            if (landmark == null) return;
            bool ok = landmark.Edges.Count == 1 && landmark.Edges[0].Kind == EdgeKind.Locked && landmark.Edges[0].Other(landmark) != landing;
            if (!ok)
            {
                report.Fail($"랜드마크 {landmark.Id}: 승강장이 아닌 이웃 칸 하나와 자물쇠 문으로만 이어져야 함");
                return;
            }
            if (recipe != null && recipe.Landmark != null && landmark.Edges[0].SideFrom(landmark) != recipe.LandmarkDoor)
                report.Fail($"랜드마크 {landmark.Id}: 자물쇠 문이 {landmark.Edges[0].SideFrom(landmark)} 쪽 — {recipe.LandmarkDoor} 쪽이어야 함");
        }

        // ⑨ 개수 = 층 예산(이번 입력 기준).
        static void CheckCounts(FloorMap map, RuleReport report, FloorBudget budget, int startReachable)
        {
            int groups = 0, nests = 0, wood = 0, iron = 0, ores = 0, lamps = 0, stakes = 0, events = 0;
            foreach (var c in map.Cells)
            {
                foreach (var f in c.Features)
                {
                    switch (f.Kind)
                    {
                        case FeatureKind.Group: groups++; break;
                        case FeatureKind.Nest: nests++; break;
                        case FeatureKind.WoodChest: wood++; break;
                        case FeatureKind.IronChest: iron++; break;
                        case FeatureKind.Ore: ores++; break;
                        case FeatureKind.WallLamp: lamps++; break;
                        case FeatureKind.Stake: stakes++; break;
                        case FeatureKind.Lunchbox: events++; break;
                    }
                }
            }
            Count(report, "칸", map.Cells.Count, budget.Cells);
            Count(report, "처음 갈 수 있는 칸", startReachable, budget.StartReachable);
            Count(report, "무리", groups, budget.Groups);
            Count(report, "둥지", nests, budget.Nests);
            Count(report, "나무 궤짝", wood, budget.WoodChests);
            Count(report, "쇠 궤짝", iron, budget.IronChests);
            Count(report, "광맥", ores, budget.Ores);
            Count(report, "벽 등잔", lamps, budget.Lamps);
            Count(report, "말뚝", stakes, budget.Stakes);
            Count(report, "사건", events, budget.Events);
        }

        static void Count(RuleReport report, string what, int actual, int expected)
        {
            if (actual != expected) report.Fail($"{what} {actual}개: 층 예산 {expected}개");
        }

        /// <summary>여러 칸에서 시작한 거리(길 종류 상관없이).</summary>
        static Dictionary<MapCell, int> DistanceFrom(List<MapCell> sources)
        {
            var dist = new Dictionary<MapCell, int>();
            var queue = new Queue<MapCell>();
            foreach (var s in sources)
            {
                if (dist.ContainsKey(s)) continue;
                dist[s] = 0;
                queue.Enqueue(s);
            }
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var e in c.Edges)
                {
                    var n = e.Other(c);
                    if (dist.ContainsKey(n)) continue;
                    dist[n] = dist[c] + 1;
                    queue.Enqueue(n);
                }
            }
            return dist;
        }

        /// <summary>열린 길만 지나는 걸음 수.</summary>
        static Dictionary<MapCell, int> StepsFrom(MapCell from)
        {
            var dist = new Dictionary<MapCell, int> { [from] = 0 };
            var queue = new Queue<MapCell>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var e in c.Edges)
                {
                    if (e.Kind != EdgeKind.Open) continue;
                    var n = e.Other(c);
                    if (dist.ContainsKey(n)) continue;
                    dist[n] = dist[c] + 1;
                    queue.Enqueue(n);
                }
            }
            return dist;
        }
    }
}
