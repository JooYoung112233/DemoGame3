using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Demo5.FrontEnd;
using UnityEditor;

// Balance probe for exploration (기획/밸런스-1차.md). Pure rules, no scene: the real FieldSiteState and FieldTurnPlan.NoiseOf,
// and a copy of the first-visit random encounter check (ExpeditionEncounterPanel.AfterSearch). Two members, no prybar.
// Objects without tools: 오락실 상자(0) · 탁자(1), 복도 보관 상자(5), 관리실 선반(8, only while the den is empty).
// Loot() (SIM-3) reads the real loot tables and recipes from SettlementScreen.prefab (edit mode, AssetDatabase) and rolls them with ExpeditionLootPanel.ChanceFor.
public static class SimulateFieldSite
{
    const int A = FieldSiteState.Arcade, C = FieldSiteState.Corridor, Den = FieldSiteState.Den;
    // Noise per search turn: the table before this pass and the one in FieldTurnPlan now.
    static int OldNoise(int pace, bool watched) => Math.Max(0, 3 - pace - (watched ? 1 : 0));
    static int NewNoise(int pace, bool watched) => FieldTurnPlan.NoiseOf(pace, watched);

    // ---- first visit: the random encounter (the resident sleeps) ----
    sealed class Rates { public int Base, Max, Threshold, PerNoise, Grace; }
    static readonly Rates OldRates = new Rates { Base = 15, Max = 70, Threshold = 2, PerNoise = 7, Grace = 2 }, NewRates = new Rates { Base = 10, Max = 45, Threshold = 4, PerNoise = 4, Grace = 3 };
    // One visit: search the objects in order (each pace+1 turns); moves between rooms spend a turn without a check. Returns encounters.
    static int OneVisit(int objects, int pace, bool watched, Func<int, bool, int> noiseOf, Rates r, Random rng)
    {
        int noise = 0, searches = 0, cooldown = 0, met = 0; bool warned = false;
        for (int o = 0; o < objects; o++)
            for (int t = 0; t <= pace; t++)
            {
                noise += noiseOf(pace, watched); searches++;
                if (cooldown > 0) { cooldown--; continue; }
                if (!warned) { if (noise >= r.Threshold) warned = true; continue; }
                if (searches < 2) continue;
                int chance = Math.Max(0, Math.Min(r.Max, r.Base + noise * r.PerNoise));
                if (rng.Next(0, 100) < chance) { met++; cooldown = r.Grace; warned = false; noise += 1; } // a won fight adds its noise
            }
        return met;
    }
    public static string FirstVisit()
    {
        var sb = new StringBuilder(); var rng = new Random(7); const int runs = 20000;
        sb.AppendLine("## 첫 방문 무작위 조우 (2인, 20000회) · 1회 이상 / 2회 이상");
        var routes = new[] { ("상자만", 1), ("상자+탁자", 2), ("세 곳", 3) }; var paces = new[] { ("빠름", 0), ("보통", 1), ("정밀", 2) };
        foreach (var (label, table, rates) in new[] { ("이전", (Func<int, bool, int>)OldNoise, OldRates), ("1차", (Func<int, bool, int>)NewNoise, NewRates) })
        {
            sb.AppendLine("### " + label + " 소음표·조우 수치");
            foreach (var (route, count) in routes)
            {
                var cells = new List<string>();
                foreach (var (pace, p) in paces)
                    foreach (bool watch in p == 0 ? new[] { false, true } : new[] { false })
                    {
                        int one = 0, two = 0;
                        for (int i = 0; i < runs; i++) { int m = OneVisit(count, p, watch, table, rates, rng); if (m >= 1) one++; if (m >= 2) two++; }
                        cells.Add(pace + (watch ? "+망보기" : "") + " " + (100 * one / runs) + "%/" + (100 * two / runs) + "%");
                    }
                sb.AppendLine(route + " · " + string.Join(" · ", cells));
            }
        }
        return sb.ToString();
    }

    // ---- second visit: the site board (deterministic; each policy is one line) ----
    sealed class Run { public int Turns, Met, Hushes; public bool Shelf, Done, Timeout; public int Danger; public string Note = ""; }
    // pace: 0 fast / 1 normal / 2 precise; watch: a lookout on fast; hushWhenNeeded: hush when a step comes in, it stands here, or the next search would raise 위험도;
    // lure: fast in the arcade until it sets off, hush it past, slip to the den shelf.
    // targets: the objects to search in order (default 상자·탁자·복도 상자). Moves step one room per turn (FieldSiteState.StepToward).
    // unlockTurns > 0: the storage is still locked; the first entry forces it on the way: 1 + unlockTurns turns, unlockNoise on the first
    // (ExpeditionRoomNavigation.ConfirmMove → ExpeditionSiteThreat.PartyArrived, which stops at an encounter).
    static Run Visit(int danger, int gauge, int pace, bool watch, bool hushWhenNeeded, bool lure, Func<int, bool, int> noiseOf, int goneTurns, int budget = 40,
        IList<(int site, int room)> targets = null, int unlockTurns = 0, int unlockNoise = 0)
    {
        var s = new FieldSiteState(new FieldSiteRules { GoneTurns = goneTurns }, () => 0, false, danger, gauge, FieldSiteState.Nowhere, 0); var run = new Run();
        var todo = new List<(int site, int room, int left)>();
        if (targets == null) { todo.Add((0, A, pace + 1)); todo.Add((1, A, pace + 1)); todo.Add((5, C, pace + 1)); }
        else foreach (var target in targets) todo.Add((target.site, target.room, pace + 1));
        bool locked = unlockTurns > 0;
        int shelfLeft = 2; // the shelf at normal pace
        int room = A; s.MoveParty(A);
        void Turn(int noise, bool hushed)
        {
            s.EndTurn(noise, hushed); run.Turns++; if (hushed) run.Hushes++;
            if (s.Encounter) { run.Met++; s.Driven(); s.EndTurn(1, false); run.Turns++; } // fight, win, its noise and a turn
        }
        while (run.Turns < budget)
        {
            bool hereDone = !todo.Any(x => x.room == room);
            // Lure: in the arcade make one loud turn until it sets off, then hush until it has passed and settled; then go for the shelf.
            if (lure && !run.Shelf)
            {
                if (room == A)
                {
                    bool lured = s.Remembered == A || s.ResidentRoom != Den;
                    if (!lured) { var i = todo.FindIndex(x => x.room == A); if (i < 0) { run.Note = "no loud object left"; break; } Turn(noiseOf(0, false), false); var x0 = todo[i]; todo[i] = (x0.site, x0.room, x0.left - 1); if (todo[i].left <= 0) todo.RemoveAt(i); continue; }
                    if (s.ResidentRoom == A && s.Resident == ResidentState.Staying || s.ResidentRoom == Den && s.Remembered < 0) { s.MoveParty(C); room = C; Turn(0, false); continue; } // slip out while it stays
                    Turn(0, true); continue;
                }
                if (room == C && s.DenStaysEmpty)
                {
                    if (s.Incoming) { Turn(0, true); continue; }
                    Turn(noiseOf(1, false), false); if (--shelfLeft <= 0) run.Shelf = true; continue;
                }
                if (room == C && !s.DenEmpty && s.Incoming) { Turn(0, true); continue; }
            }
            if (todo.Count == 0) { run.Done = true; break; }
            if (hereDone)
            {
                int next = FieldSiteState.StepToward(room, todo[0].room); s.MoveParty(next); room = next;
                if (next == FieldSiteState.Storage && locked) { locked = false; int met = run.Met; for (int u = 0; u <= unlockTurns && run.Met == met; u++) Turn(u == 0 ? unlockNoise : 0, false); }
                else Turn(0, false);
                continue;
            }
            int noise = noiseOf(pace, watch && pace == 0);
            if (hushWhenNeeded && (s.Incoming || s.Visible || s.Preview(noise, false).danger > s.Danger)) { Turn(0, true); continue; }
            var k = todo.FindIndex(x => x.room == room); var t = todo[k];
            Turn(noise, false); todo[k] = (t.site, t.room, t.left - 1); if (todo[k].left <= 0) todo.RemoveAt(k);
        }
        run.Timeout = run.Turns >= budget; run.Danger = s.Danger;
        if (!run.Shelf && s.DenEmpty && run.Met > 0) run.Note += " (굴 빈 틈 있음)";
        return run;
    }
    public static string SecondVisit()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 두 번째 방문 · 운영별 (2인, 도구 없음: 상자·탁자·복도 상자) · 턴 / 조우 / 숨죽이기 / 끝 위험도 / 굴 선반");
        var starts = new[] { (0, 0), (0, 2), (1, 1), (1, 2), (2, 0) };
        var policies = new (string name, int pace, bool watch, bool hush, bool lure)[] {
            ("조심(보통+숨죽이기)", 1, false, true, false), ("대충(보통)", 1, false, false, false), ("빠름+망보기", 0, true, true, false), ("빠름(대충)", 0, false, false, false),
            ("정밀+숨죽이기", 2, false, true, false), ("꾀어내기→선반", 0, false, true, true) };
        foreach (var (label, table, gone) in new[] { ("이전", (Func<int, bool, int>)OldNoise, 3), ("1차", (Func<int, bool, int>)NewNoise, 4) })
        {
            sb.AppendLine("### " + label + " 소음표 (빠름/보통/정밀 턴당 " + table(0, false) + "/" + table(1, false) + "/" + table(2, false) + ", 망보기 빠름 " + table(0, true) + ")");
            foreach (var p in policies)
                sb.AppendLine(p.name + " · " + string.Join(" · ", starts.Select(st => { var r = Visit(st.Item1, st.Item2, p.pace, p.watch, p.hush, p.lure, table, gone); return $"({st.Item1},{st.Item2}) {r.Turns}턴 조우{r.Met} 숨{r.Hushes} 위{r.Danger}{(r.Shelf ? " 선반" : "")}{(r.Timeout ? " 시간초과" : "")}{r.Note}"; })));
        }
        return sb.ToString();
    }

    // ---- second visit: the best play (breadth-first over every turn choice, no encounter allowed) ----
    // Choices per turn: a search step on an object in this room (fast / fast with a lookout / normal; an object started keeps its pace),
    // the den shelf from the corridor while the den is empty (fast or normal), hush, or a move between the arcade and the corridor.
    static readonly FieldInfo[] StateFields = typeof(FieldSiteState).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(f => f.FieldType != typeof(FieldSiteRules) && !typeof(Delegate).IsAssignableFrom(f.FieldType)).ToArray();
    static string Key(FieldSiteState s, int[] prog) => string.Join(",", StateFields.Select(f => f.GetValue(s))) + "|" + string.Join(",", prog);
    // prog: per object (arcade crate, table, corridor crate, shelf) 0 untouched, else pace*10 + turns done + 1; 99 finished.
    static readonly int[] ObjRoom = { A, A, C, C };
    static (int shelf, int all, int states, string shelfPath) Best(int danger, int gauge, Func<int, bool, int> noiseOf, int goneTurns, int limit = 24)
    {
        var start = new FieldSiteState(new FieldSiteRules { GoneTurns = goneTurns }, () => 0, false, danger, gauge, FieldSiteState.Nowhere, 0);
        var frontier = new List<(FieldSiteState s, int[] prog, string path)> { (start, new int[4], "") }; var seen = new HashSet<string> { Key(start, new int[4]) };
        int shelf = -1, all = -1; string shelfPath = "";
        for (int turn = 1; turn <= limit && frontier.Count > 0 && (shelf < 0 || all < 0); turn++)
        {
            var next = new List<(FieldSiteState, int[], string)>();
            foreach (var (s, prog, path) in frontier)
            {
                var options = new List<(int move, int obj, int pace, bool watch, bool hush)> { (-1, -1, 0, false, true) };
                options.Add((s.PartyRoom == A ? C : A, -1, 0, false, false));
                for (int o = 0; o < 4; o++)
                {
                    if (prog[o] == 99 || ObjRoom[o] != s.PartyRoom) continue;
                    if (o == 3 && !s.DenStaysEmpty) continue;
                    if (prog[o] > 0) { int pc = (prog[o] - 1) / 10; options.Add((-1, o, pc % 3, pc >= 3, false)); continue; }
                    options.Add((-1, o, 0, false, false)); options.Add((-1, o, 1, false, false)); if (o < 3) options.Add((-1, o, 0, true, false));
                }
                foreach (var (move, obj, pace, watch, hush) in options)
                {
                    var c = s.Copy(); var pr = (int[])prog.Clone(); int noise = 0;
                    if (move >= 0) c.MoveParty(move);
                    if (obj >= 0)
                    {
                        noise = noiseOf(pace, watch);
                        int code = (pace + (watch ? 3 : 0)) * 10, done = pr[obj] == 0 ? 1 : (pr[obj] - 1) % 10 + 1;
                        pr[obj] = done >= pace + 1 ? 99 : code + done + 1;
                    }
                    c.EndTurn(noise, hush);
                    if (c.Encounter) continue;
                    string step = hush ? "숨" : move >= 0 ? (move == A ? "→오락실" : "→복도") : new[] { "상자", "탁자", "복도상자", "선반" }[obj] + (watch ? "빠름+망" : pace == 0 ? "빠름" : "보통");
                    step += $"[위{c.Danger}·게{c.Gauge}·그것 {c.ResidentRoom}{(c.PassedBy ? " 지나감" : "")}]"; string np = path + (path.Length > 0 ? " " : "") + step;
                    if (pr[3] == 99 && shelf < 0) { shelf = turn; shelfPath = np; }
                    if (pr[0] == 99 && pr[1] == 99 && pr[2] == 99 && all < 0) all = turn;
                    if (seen.Add(Key(c, pr))) next.Add((c, pr, np));
                }
            }
            frontier = next;
        }
        return (shelf, all, seen.Count, shelfPath);
    }
    public static string BestPlay()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 두 번째 방문 · 가장 잘 풀었을 때 (조우 0회로 가능한 최소 턴, 24턴 안) · 굴 선반 / 세 곳 모두");
        var starts = new[] { (0, 0), (0, 2), (1, 1), (1, 2), (2, 0), (2, 2) };
        foreach (var (label, table, gone) in new[] { ("이전", (Func<int, bool, int>)OldNoise, 3), ("1차", (Func<int, bool, int>)NewNoise, 4) })
            sb.AppendLine(label + " · " + string.Join(" · ", starts.Select(st => { var r = Best(st.Item1, st.Item2, table, gone); return $"({st.Item1},{st.Item2}) 선반 {(r.shelf < 0 ? "불가" : r.shelf + "턴")} / 세 곳 {(r.all < 0 ? "불가" : r.all + "턴")}"; })));
        foreach (var st in new[] { (0, 0), (1, 2) }) sb.AppendLine($"1차 ({st.Item1},{st.Item2}) 선반 수순 (방 0 오락실 1 복도 3 관리실 -1 떠남): " + Best(st.Item1, st.Item2, NewNoise, 4).shelfPath);
        return sb.ToString();
    }

    // ---- loot (SIM-3): expected finds from the real tables, per object, per visit and per board turn ----
    // Policies: pace 0 빠름 / 1 보통 / 2 정밀; duty 0 함께 (+TogetherBonus) / 1 망보기 (+0, 소음 -1 at fast) / 2 조명 (+LightBonus). Two members alive (ExpeditionLootPanel.BonusFor).
    static readonly (string name, int pace, int duty)[] LootPolicies = { ("빠름+망보기", 0, 1), ("빠름+함께", 0, 0), ("보통+함께", 1, 0), ("정밀+함께", 2, 0), ("정밀+조명", 2, 2) };
    static int BonusOf(int duty, int light) => duty == 0 ? FieldTurnPlan.TogetherBonus : duty == 2 ? light : 0;
    static double Expected(ExpeditionLootPanel.Site site, int pace, int bonus) => site.Drops == null ? 0 : site.Drops.Sum(d => d.Count * ExpeditionLootPanel.ChanceFor(d, pace, bonus) / 100.0);
    // The opening intro grants cloth 1 before any search (SettlementIntroduction.Act, step 1).
    const int IntroCloth = 1;
    static SettlementController LoadScreen()
    {
        var go = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab"); var c = go ? go.GetComponent<SettlementController>() : null;
        if (!c || !c.ArrivalPanel || !c.ArrivalPanel.Loot || !c.ArrivalPanel.Threat || !c.ArrivalPanel.Rooms || !c.CraftPanel) throw new Exception("SettlementScreen.prefab: loot/threat/rooms/craft references missing");
        return c;
    }
    // Raw materials a set of crafts spends; intermediates crafted inside the same plan (rope, nails) are not counted. The intro cloth is subtracted.
    static Dictionary<string, int> RawNeed(SettlementCraftPanel.Recipe[] recipes, (string id, int times)[] plan)
    {
        var made = plan.Select(x => x.id).ToArray(); var need = new Dictionary<string, int>();
        foreach (var (id, times) in plan)
        {
            var recipe = recipes.FirstOrDefault(r => r.Id == id) ?? throw new Exception("Missing recipe " + id);
            foreach (var cost in recipe.Costs) if (!made.Contains(cost.MaterialId)) need[cost.MaterialId] = (need.TryGetValue(cost.MaterialId, out int v) ? v : 0) + cost.Count * times;
        }
        if (need.ContainsKey("cloth")) need["cloth"] = Math.Max(0, need["cloth"] - IntroCloth);
        return need;
    }
    // Exact probability that independent row rolls (fixed counts) cover 'need'; counts are capped at the need, so the state space stays small.
    static double Afford(IEnumerable<ExpeditionLootPanel.Drop> rows, Func<ExpeditionLootPanel.Drop, int> chance, Dictionary<string, int> need)
    {
        var ids = need.Keys.ToArray(); var caps = ids.Select(k => need[k]).ToArray();
        long Pack(int[] v) { long k = 0; for (int i = 0; i < v.Length; i++) k = k * (caps[i] + 1) + v[i]; return k; }
        int[] Unpack(long k) { var v = new int[caps.Length]; for (int i = caps.Length - 1; i >= 0; i--) { v[i] = (int)(k % (caps[i] + 1)); k /= caps[i] + 1; } return v; }
        var dist = new Dictionary<long, double> { { 0, 1.0 } };
        foreach (var row in rows)
        {
            int m = Array.IndexOf(ids, row.Id); double p = chance(row) / 100.0; if (m < 0 || row.Count <= 0 || p <= 0) continue;
            var next = new Dictionary<long, double>();
            void Add(long k, double q) { if (q > 0) next[k] = (next.TryGetValue(k, out double o) ? o : 0) + q; }
            foreach (var kv in dist) { Add(kv.Key, kv.Value * (1 - p)); var v = Unpack(kv.Key); v[m] = Math.Min(caps[m], v[m] + row.Count); Add(Pack(v), kv.Value * p); }
            dist = next;
        }
        return dist.TryGetValue(Pack(caps), out double r) ? r : 0;
    }

    public static string Loot()
    {
        var c = LoadScreen(); var a = c.ArrivalPanel;
        return LootReport(a.Loot.Sites, a.ObjectNames, a.Loot.LightBonus, a.Threat.Rules.GoneTurns, a.Rooms.UnlockTurns, a.Rooms.UnlockNoise, c.CraftPanel.Recipes);
    }
    // Pure report over the loaded data (no Unity objects), so it can also be driven by a fixture.
    static string LootReport(ExpeditionLootPanel.Site[] sites, string[] names, int light, int gone, int unlockTurns, int unlockNoise, SettlementCraftPanel.Recipe[] recipes)
    {
        var sb = new StringBuilder();
        string Name(int i) => names != null && i < names.Length ? names[i] : "site " + i;
        string Set(int[] set) => "{" + string.Join(",", set) + "}";
        double Items(int[] set, (string name, int pace, int duty) p) => set.Sum(i => Expected(sites[i], p.pace, BonusOf(p.duty, light)));

        // 1. Per object: expected items (items per search turn = pace + 1 turns).
        sb.AppendLine("## 수색 보상 기대값 · SettlementScreen.prefab 실제 표 · ExpeditionLootPanel.ChanceFor · 2인 (함께 +" + FieldTurnPlan.TogetherBonus + " · 조명 +" + light + " · 망보기 +0)");
        sb.AppendLine("### 사물별 · 기대 개수 (수색 턴당) · 확정 = 100% 행 합계");
        sb.AppendLine("정책: " + string.Join(" · ", LootPolicies.Select(p => p.name)));
        for (int i = 0; i < sites.Length; i++)
        {
            var site = sites[i]; if (site.Room < 0 || site.Drops == null || site.Drops.Length == 0) continue;
            string cells = string.Join(" · ", LootPolicies.Select(p => { double e = Expected(site, p.pace, BonusOf(p.duty, light)); return $"{e:0.00} ({e / (p.pace + 1):0.00})"; }));
            sb.AppendLine($"[{i}] {Name(i)}{(string.IsNullOrEmpty(site.RequiredTool) ? "" : " · " + site.RequiredTool)} · {cells} · 확정 {site.Drops.Where(d => d.Chance >= 100).Sum(d => d.Count)}");
        }

        // 2. Visit totals (items found, before bag slots).
        sb.AppendLine("### 방문 합계 (찾은 물건 · 가방 칸 제한 전)");
        foreach (var set in new[] { new[] { 0, 1, 5 }, new[] { 2, 4, 6, 7 } })
            sb.AppendLine(Set(set) + " · " + string.Join(" · ", LootPolicies.Select(p => p.name + " " + Items(set, p).ToString("0.0"))));

        // 3. Second visit on the board: turns from Visit() (hush when needed, room steps, storage first entry), items per board turn.
        var starts = new[] { (0, 0), (0, 2), (1, 1), (1, 2), (2, 0) };
        sb.AppendLine($"### 두 번째 방문 · 장소판 턴 (시작 위험도·게이지 5종 평균, 필요할 때 숨죽이기, 한 턴에 한 사물, 방 이동 1턴, 보관실 첫 진입 {1 + unlockTurns}턴·소음 {unlockNoise}, 귀환 이동 제외, GoneTurns {gone})");
        int fast = Array.FindIndex(LootPolicies, p => p.pace == 0 && p.duty == 1), precise = Array.FindIndex(LootPolicies, p => p.pace == 2 && p.duty == 0);
        foreach (var set in new[] { new[] { 0, 1, 5 }, new[] { 1, 5 }, new[] { 1, 5, 6, 7 }, new[] { 2, 4, 6, 7 }, new[] { 1, 2, 4, 5, 6, 7 } })
        {
            var targets = set.Select(i => (site: i, room: sites[i].Room)).ToList(); bool storage = set.Any(i => sites[i].Room == FieldSiteState.Storage);
            var perTurn = new double[LootPolicies.Length]; var cells = new List<string>();
            for (int k = 0; k < LootPolicies.Length; k++)
            {
                var p = LootPolicies[k];
                var runs = starts.Select(st => Visit(st.Item1, st.Item2, p.pace, p.duty == 1, true, false, NewNoise, gone, 60, targets, storage ? unlockTurns : 0, storage ? unlockNoise : 0)).ToArray();
                double turns = runs.Average(r => r.Turns), met = runs.Average(r => r.Met), items = Items(set, p); perTurn[k] = items / turns;
                cells.Add($"{p.name} {turns:0.0}턴 조우{met:0.0} {items:0.0}개 턴당 {perTurn[k]:0.00}{(runs.Any(r => r.Timeout) ? " 시간초과" : "")}");
            }
            sb.AppendLine(Set(set) + (storage ? " (보관실 첫 진입)" : "") + " · " + string.Join(" · ", cells));
            sb.AppendLine($"  턴당 비 {LootPolicies[fast].name} / {LootPolicies[precise].name} = {perTurn[fast] / perTurn[precise]:0.00} (감사 목표 1.2~1.5)");
        }

        // 4. Facilities after visit 2: 창고·작업대·잠자리·조리대 + the prybar chain (needed to open the storage), from the real recipes. Same policy on both visits.
        var core = new (string id, int times)[] { ("build-stock", 1), ("build-bench", 1), ("build-bed", 1), ("build-cooker", 1), ("rope", 1), ("nails", 2), ("prybar", 1) };
        var desk = new (string id, int times)[] { ("build-stock", 1), ("build-bench", 1), ("build-bed", 1), ("build-cooker", 1), ("build-research", 1), ("rope", 1), ("nails", 2), ("prybar", 1) };
        var needCore = RawNeed(recipes, core); var needDesk = RawNeed(recipes, desk);
        string Need(Dictionary<string, int> n) => string.Join(" ", n.Select(kv => kv.Key + " " + kv.Value));
        sb.AppendLine($"### 방문 2 뒤 핵심 시설 수급 확률 (목표 80%+) · 핵심(창고·작업대·잠자리·조리대·지렛대 체인) {Need(needCore)} / 연구대 포함 {Need(needDesk)} · 도입 천 {IntroCloth} 차감 · 찾은 양 기준(가방 칸 무시)");
        var plans = new (string label, int[] first, int[] second)[] {
            ("튜토리얼 상자 → 보관실", new[] { 0 }, new[] { 6, 7 }), ("상자 → 탁자+보관실", new[] { 0 }, new[] { 1, 6, 7 }), ("상자 → 복도 상자+보관실", new[] { 0 }, new[] { 5, 6, 7 }),
            ("상자 → 탁자+복도 상자+보관실", new[] { 0 }, new[] { 1, 5, 6, 7 }), ("상자 → 지렛대 사물+보관실", new[] { 0 }, new[] { 2, 4, 6, 7 }), ("무료 세 곳 → 보관실", new[] { 0, 1, 5 }, new[] { 6, 7 }) };
        foreach (var plan in plans)
        {
            var rows = plan.first.Concat(plan.second).SelectMany(i => sites[i].Drops ?? new ExpeditionLootPanel.Drop[0]).ToArray();
            sb.AppendLine(plan.label + " " + Set(plan.first) + "→" + Set(plan.second) + " · " + string.Join(" · ", LootPolicies.Select(p =>
            {
                int bonus = BonusOf(p.duty, light); Func<ExpeditionLootPanel.Drop, int> chance = d => ExpeditionLootPanel.ChanceFor(d, p.pace, bonus);
                return $"{p.name} {Afford(rows, chance, needCore) * 100:0}% / {Afford(rows, chance, needDesk) * 100:0}%";
            })));
        }
        sb.AppendLine("(값: 핵심 / 연구대 포함. 지렛대 체인은 물자 상자 확정분으로 방문 1에 보장 — BuildBalance1 가드)");
        return sb.ToString();
    }
}
