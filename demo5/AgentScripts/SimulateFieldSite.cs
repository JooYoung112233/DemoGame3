using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Demo5.FrontEnd;
using UnityEditor;

// Balance probe for exploration (기획/밸런스-1차.md; 기획/탐험-수색쪽지와-협동-1차.md '속도 삭제와 사물 소음' 2026-09-25). Edit mode, no scene:
// the real FieldSiteState, FieldTurnPlan.SearchNoise / RequiredOf and ExpeditionEncounterPanel.WarnsAt / ChanceAt, with the objects' noise,
// turns, tools and loot, the site rules and the encounter values read from SettlementScreen.prefab (AssetDatabase). Two members.
// No search pace any more: an object makes its own noise (Site.Noise) on each of its turns (Site.Turns, 0 = 2). Roles: 함께 (one turn sooner),
// 망보기 (noise -1, full turns; only on a noisy object — on a silent one the helper searches 함께), 조명 (+LightBonus, full turns), 혼자
// (one member, full turns). A prybar opens 오락기 뒤판(2) · 배전함(4) and the storage lock (UnlockNoise). Items never refill: every object and
// the den shelf pay once per campaign, so each report is one visit. Luring needs a loud object (오락기 뒤판 3, prybar); there is no '소리 내기'.
// 말 놓기 (기획/탐험-말놓기-조작-재설계.md '추가 결정', 2026-09-25): the first visit runs on the room board too, so one turn may search
// several objects (each lead advances 1, a second pawn on the same object is co-op); a turn with at least one search is one search turn
// and its noise is the sum. With two members parallel solo moves as fast as co-op, so the chance still counts search turns only.
// Entry points: FirstVisit (the first-visit encounter with the prefab values: tune here), FirstVisitTune (a grid over those values),
// SecondVisit, BestPlay, Loot.
public static class SimulateFieldSite
{
    const int A = FieldSiteState.Arcade, C = FieldSiteState.Corridor, S = FieldSiteState.Storage, DenRoom = FieldSiteState.Den;

    // ---- data: what the game reads (SettlementScreen.prefab) ----
    sealed class Rates
    {
        public int Warn, Base, PerSearch, PerNoise, Max, Threshold, Grace;
        public Rates With(int warn, int b, int perSearch, int perNoise, int max) { var r = (Rates)MemberwiseClone(); r.Warn = warn; r.Base = b; r.PerSearch = perSearch; r.PerNoise = perNoise; r.Max = max; return r; }
        public override string ToString() => $"경고 {Warn}턴 · 기본 {Base}% · 수색당 +{PerSearch} · 소음당 +{PerNoise} · 최대 {Max}% · 소음 문턱 {Threshold} · 유예 {Grace}";
    }
    sealed class World
    {
        public ExpeditionLootPanel.Site[] Sites; public string[] Names; public int[] Noise, Turns; public int Light, UnlockTurns, UnlockNoise, Strip = 25, DenSite = 8;
        public FieldSiteRules Rules; public Rates Encounter; public SettlementCraftPanel.Recipe[] Recipes; public string Note = "";
        public int Room(int i) => Sites[i].Room;
        public bool Tool(int i) => !string.IsNullOrEmpty(Sites[i].RequiredTool);
        public string Name(int i) => Names != null && i < Names.Length ? Names[i] : "site " + i;
    }
    // The planned noise table (BuildSiteNoise.Noise), used only while the prefab has no noise at all (BuildSiteNoise.Run not applied yet).
    static readonly int[] PlannedNoise = { 0, 0, 3, 0, 2, 1, 0, 1, 1 };
    static readonly Dictionary<int, string> Short = new Dictionary<int, string> { { 0, "상자" }, { 1, "탁자" }, { 2, "오락기" }, { 4, "배전함" }, { 5, "복도상자" }, { 6, "보관선반" }, { 7, "자재" }, { 8, "선반" } };
    static World Load()
    {
        var go = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab"); var c = go ? go.GetComponent<SettlementController>() : null;
        if (!c || !c.ArrivalPanel || !c.ArrivalPanel.Loot || !c.ArrivalPanel.Threat || !c.ArrivalPanel.Rooms || !c.ArrivalPanel.Encounter || !c.CraftPanel) throw new Exception("SettlementScreen.prefab: loot/threat/rooms/encounter/craft references missing");
        var a = c.ArrivalPanel; var e = a.Encounter; var sites = a.Loot.Sites;
        var w = new World
        {
            Sites = sites, Names = a.ObjectNames, Light = a.Loot.LightBonus, UnlockTurns = a.Rooms.UnlockTurns, UnlockNoise = a.Rooms.UnlockNoise, Rules = a.Threat.Rules, DenSite = a.Threat.DenSite, Recipes = c.CraftPanel.Recipes,
            Noise = sites.Select(s => Math.Max(0, s.Noise)).ToArray(), Turns = sites.Select(s => s.Turns).ToArray(),
            Encounter = new Rates { Warn = e.WarnSearches, Base = e.BaseChance, PerSearch = e.ChancePerSearch, PerNoise = e.ChancePerNoise, Max = e.MaximumChance, Threshold = e.NoiseThreshold, Grace = e.GraceSearches }
        };
        var warning = go.GetComponentInChildren<FieldTurnWarning>(true); if (warning) w.Strip = warning.FirstVisitChance;
        if (w.Noise.All(n => n == 0) && sites.Length == PlannedNoise.Length) { w.Noise = PlannedNoise.ToArray(); w.Note = " · 프리팹 소음이 모두 0이라 계획표 사용 (BuildSiteNoise.Run 전)"; }
        return w;
    }
    static string ObjectLine(World w) => "사물 소음·턴: " + string.Join(" · ", Enumerable.Range(0, w.Sites.Length).Where(i => w.Room(i) >= 0).Select(i => $"[{i}]{(Short.TryGetValue(i, out var n) ? n : w.Name(i))} {w.Noise[i]}/{FieldTurnPlan.RequiredOf(w.Turns[i], false)}턴{(w.Tool(i) ? "(지렛대)" : "")}"))
        + $" · 잠긴 문 {w.UnlockNoise} · 큰 소리 {w.Rules.LoudNoise} · 게이지 {w.Rules.GaugeSize}" + w.Note;

    // Roles: 0 함께 (a second member, one turn sooner) · 1 망보기 (noisy object: noise -1, full turns; silent: 함께) · 2 조명 (+Light) · 3 혼자 (one member).
    static readonly string[] RoleNames = { "함께", "망보기", "조명", "혼자" };
    static (int turns, int noise) Plan(World w, int site, int role)
    {
        int n = w.Noise[site]; if (role == 1 && n == 0) role = 0;
        return (FieldTurnPlan.RequiredOf(w.Turns[site], role == 0), FieldTurnPlan.SearchNoise(n, role == 1));
    }
    static List<int> TurnNoises(World w, IEnumerable<int> sites, int role)
    {
        var list = new List<int>(); foreach (int site in sites) { var (turns, noise) = Plan(w, site, role); for (int t = 0; t < turns; t++) list.Add(noise); }
        return list;
    }

    // ---- first visit: the random encounter (the resident sleeps), exact over every roll ----
    // A won fight's noise (ExpeditionEncounterPanel.FinishBattle's default; the real value is the battle's ResultNoise).
    const int BattleNoise = 2;
    struct Outcome { public double One, Two, Warned, Strip, Strip2; public int Turns; }
    // Exactly ExpeditionEncounterPanel.AfterSearch over one visit's search turns (each with its object's noise; moves between rooms are not
    // searches and make no noise): a cooling-down search only counts down; an unwarned one may warn (no roll); a warned one rolls
    // ChanceAt(search turn, noise so far). The strip (FieldTurnWarning) shows after a search when the next search's chance reaches 'strip'.
    static Outcome Encounters(IList<int> noises, Rates r, int strip)
    {
        var dist = new Dictionary<(bool warned, int cool, int noise, int met, bool seen, int strips), double> { { (false, 0, 0, 0, false, 0), 1.0 } };
        for (int t = 0; t < noises.Count; t++)
        {
            int search = t + 1; var next = new Dictionary<(bool, int, int, int, bool, int), double>();
            void Add((bool, int, int, int, bool, int) k, double p) { if (p > 0) next[k] = (next.TryGetValue(k, out double o) ? o : 0) + p; }
            int Strips(int shown, bool warned, int cool, int noise) => warned && cool == 0 && ExpeditionEncounterPanel.ChanceAt(search + 1, noise, r.Warn, r.Base, r.PerSearch, r.PerNoise, r.Max) >= strip ? Math.Min(2, shown + 1) : shown;
            foreach (var kv in dist)
            {
                var (warned, cool, noise, met, seen, shown) = kv.Key; double p = kv.Value; noise += noises[t];
                if (cool > 0) { Add((warned, cool - 1, noise, met, seen, Strips(shown, warned, cool - 1, noise)), p); continue; }
                if (!warned) { bool warn = ExpeditionEncounterPanel.WarnsAt(search, noise, r.Warn, r.Threshold); Add((warn, 0, noise, met, seen || warn, Strips(shown, warn, 0, noise)), p); continue; }
                if (search < 2) { Add((true, 0, noise, met, seen, Strips(shown, true, 0, noise)), p); continue; }
                double c = ExpeditionEncounterPanel.ChanceAt(search, noise, r.Warn, r.Base, r.PerSearch, r.PerNoise, r.Max) / 100.0;
                Add((true, 0, noise, met, seen, Strips(shown, true, 0, noise)), p * (1 - c));
                Add((false, r.Grace, noise + BattleNoise, Math.Min(2, met + 1), seen, shown), p * c);
            }
            dist = next;
        }
        var o = new Outcome { Turns = noises.Count };
        foreach (var kv in dist)
        {
            if (kv.Key.met >= 1) o.One += kv.Value; if (kv.Key.met >= 2) o.Two += kv.Value; if (kv.Key.seen) o.Warned += kv.Value;
            if (kv.Key.strips >= 1) o.Strip += kv.Value; if (kv.Key.strips >= 2) o.Strip2 += kv.Value;
        }
        return o;
    }
    // Routes of the first visit (no prybar): the tutorial crate, then the free objects. The last is the tutorial's real route: the crate,
    // then the corridor crate where the missing person is found (MissingPersonStory.SiteIndex 5, SettlementTutorialGuide).
    static readonly (string name, int[] sites)[] FirstRoutes = { ("상자만 (튜토리얼)", new[] { 0 }), ("상자+탁자", new[] { 0, 1 }), ("세 곳", new[] { 0, 1, 5 }), ("세 곳 · 복도 먼저", new[] { 5, 0, 1 }), ("튜토리얼 · 상자→복도 상자", new[] { 0, 5 }) };
    const int TutorialRoute = 4;
    // Objects searched side by side in the same turns (one pawn each): per turn their noises add up; the longer lane sets the length.
    static List<int> Zip(params List<int>[] lanes)
    {
        int n = lanes.Max(l => l.Count); var z = new List<int>();
        for (int t = 0; t < n; t++) z.Add(lanes.Sum(l => t < l.Count ? l[t] : 0));
        return z;
    }
    static List<int> Then(params List<int>[] steps) => steps.SelectMany(x => x).ToList();
    // The board rule on the first visit (two members): the guided co-op route, one pawn per object, parallel solo, and the old sequences.
    static (string name, List<int> noises)[] BoardRoutes(World w) => new[]
    {
        ("튜토리얼 안내 · 상자 함께 → 복도 상자 함께", Then(TurnNoises(w, new[] { 0 }, 0), TurnNoises(w, new[] { 5 }, 0))),
        ("튜토리얼 · 한 곳에 한 명 (상자 혼자 → 복도 상자 혼자)", Then(TurnNoises(w, new[] { 0 }, 3), TurnNoises(w, new[] { 5 }, 3))),
        ("세 곳 함께 차례로", TurnNoises(w, new[] { 0, 1, 5 }, 0)),
        ("상자∥탁자 혼자 → 복도 상자 함께", Then(Zip(TurnNoises(w, new[] { 0 }, 3), TurnNoises(w, new[] { 1 }, 3)), TurnNoises(w, new[] { 5 }, 0))),
        ("상자∥탁자 혼자 → 복도 상자 혼자", Then(Zip(TurnNoises(w, new[] { 0 }, 3), TurnNoises(w, new[] { 1 }, 3)), TurnNoises(w, new[] { 5 }, 3))),
        ("세 곳 혼자 차례로", TurnNoises(w, new[] { 0, 1, 5 }, 3)),
    };
    // Targets (기획/밸런스-1차.md §4-5, 기획/탐험-수색쪽지와-협동-1차.md '추가 결정'): crate only ≤10% whatever the role; three objects 40-60% with
    // 함께 (the default of two members); two or more ≤5%; the guided tutorial path (crate, 함께) shows the warning strip at most once.
    // The chance is by search turns (the user's rule), so the roles that do not shorten a search (망보기: the corridor crate 2 turns instead
    // of 1; 조명 and 혼자: every object 2 turns) meet it more often on the three objects. Shown as '참고', not a pass condition: none of the
    // value sets that meet the targets keeps a 3-turn and a 6-turn route both in 40-60% (FirstVisitTune counts them). Open question for the user.
    // 말 놓기 (추가 결정): the guided tutorial route (crate co-op, then the corridor crate co-op) meets nothing and shows the strip at most once;
    // one pawn per object on that route (about 70%) is reported, not a pass condition — the guide leads to the co-op silhouettes.
    static (bool pass, string text) Targets(World w, Rates r)
    {
        double crate = Enumerable.Range(0, 4).Max(role => Encounters(TurnNoises(w, FirstRoutes[0].sites, role), r, w.Strip).One);
        double three = Encounters(TurnNoises(w, FirstRoutes[2].sites, 0), r, w.Strip).One;
        double two = FirstRoutes.SelectMany(x => Enumerable.Range(0, 4).Select(role => Encounters(TurnNoises(w, x.sites, role), r, w.Strip).Two)).Max();
        double tutorial = Encounters(TurnNoises(w, FirstRoutes[0].sites, 0), r, w.Strip).Strip2;
        var guided = Encounters(TurnNoises(w, FirstRoutes[TutorialRoute].sites, 0), r, w.Strip); var solo = Encounters(TurnNoises(w, FirstRoutes[TutorialRoute].sites, 3), r, w.Strip);
        bool a = crate <= .10 + 1e-9, b = three >= .40 - 1e-9 && three <= .60 + 1e-9, c = two <= .05 + 1e-9, d = tutorial <= 1e-9, e = guided.One <= 1e-9 && guided.Strip2 <= 1e-9;
        string others = string.Join(" · ", Enumerable.Range(1, 3).Select(role => { var o = Encounters(TurnNoises(w, FirstRoutes[2].sites, role), r, w.Strip); return $"{RoleNames[role]} {o.One * 100:0}% ({o.Turns}턴)"; }));
        return (a && b && c && d && e, $"상자만 ≤10%: {crate * 100:0}% {(a ? "통과" : "실패")} · 세 곳 함께 40~60%: {three * 100:0}% {(b ? "통과" : "실패")} · 두 번 이상 ≤5%: {two * 100:0}% {(c ? "통과" : "실패")} · 튜토리얼 경고 띠 ≤1회: {(d ? "통과" : "실패 " + (tutorial * 100).ToString("0") + "% 두 번")}"
            + $" · 튜토리얼 안내(상자→복도 상자 함께) 조우 0%·띠 ≤1회: {guided.One * 100:0}% 띠 두 번 {guided.Strip2 * 100:0}% {(e ? "통과" : "실패")}"
            + $" · 참고 (합격선 아님, 턴이 늘어나는 역할) 세 곳 {others} · 튜토리얼 한 곳에 한 명 {solo.One * 100:0.0}% ({solo.Turns}턴 · 안내가 협동으로 이끎)");
    }
    // Every role on the three objects (함께 · 망보기 · 조명 · 혼자) within 40-60%.
    static bool AllRolesInBand(World w, Rates r) => Enumerable.Range(0, 4).All(role => { double p = Encounters(TurnNoises(w, FirstRoutes[2].sites, role), r, w.Strip).One; return p >= .40 - 1e-9 && p <= .60 + 1e-9; });
    public static string FirstVisit() { var w = Load(); return FirstVisitReport(w, w.Encounter); }
    static string FirstVisitReport(World w, Rates r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 첫 방문 무작위 조우 (지렛대 없음 · 정확 계산) · " + r + " · 경고 띠 " + w.Strip + "% 이상");
        sb.AppendLine(ObjectLine(w));
        sb.AppendLine("값: 조우 1회 이상 / 2회 이상 · 경고(발소리) · 경고 띠 1회 이상 / 2회 이상 · 수색 턴");
        foreach (var (name, sites) in FirstRoutes)
            sb.AppendLine(name + " {" + string.Join(",", sites) + "} · " + string.Join(" · ", Enumerable.Range(0, 4).Select(role =>
            {
                var o = Encounters(TurnNoises(w, sites, role), r, w.Strip);
                return $"{RoleNames[role]} {o.One * 100:0}%/{o.Two * 100:0}% 경고 {o.Warned * 100:0}% 띠 {o.Strip * 100:0}%/{o.Strip2 * 100:0}% {o.Turns}턴";
            })));
        sb.AppendLine("### 말 놓기 · 한 턴에 여러 곳 (한 턴에 수색이 하나라도 있으면 수색 1턴, 소음은 합) · 조우 1회 이상 · 경고 띠 1회/2회 · 턴별 소음");
        foreach (var (name, noises) in BoardRoutes(w))
        {
            var o = Encounters(noises, r, w.Strip);
            sb.AppendLine($"{name} · 조우 {o.One * 100:0.0}% · 띠 {o.Strip * 100:0}%/{o.Strip2 * 100:0}% · {o.Turns}턴 [{string.Join(",", noises)}]");
        }
        sb.AppendLine("합격선 · " + Targets(w, r).text);
        return sb.ToString();
    }
    // A grid over the encounter values (the prefab's noise threshold and grace): the sets that meet every target, closest to 50% for three
    // objects with 함께 first, then the lowest for three objects alone. Put the chosen set into BuildSiteNoise.cs and re-run it.
    public static string FirstVisitTune() => FirstVisitTuneReport(Load());
    static string FirstVisitTuneReport(World w)
    {
        var r0 = w.Encounter; var hits = new List<(double score, double alone, Rates r, string text)>(); int tried = 0, allRoles = 0;
        foreach (int warn in new[] { 1, 2, 3 })
            foreach (int b in Enumerable.Range(0, 13).Select(i => i * 5))
                foreach (int perSearch in new[] { 0, 5, 10, 15, 20, 25 })
                    foreach (int perNoise in new[] { 0, 5, 10 })
                        foreach (int max in new[] { 45, 50, 60 })
                        {
                            var r = r0.With(warn, b, perSearch, perNoise, max); tried++; var (pass, text) = Targets(w, r); if (!pass) continue;
                            if (AllRolesInBand(w, r)) allRoles++;
                            double three = Encounters(TurnNoises(w, FirstRoutes[2].sites, 0), r, w.Strip).One, watch = Encounters(TurnNoises(w, FirstRoutes[2].sites, 1), r, w.Strip).One, alone = Encounters(TurnNoises(w, FirstRoutes[2].sites, 3), r, w.Strip).One;
                            hits.Add((Math.Abs(three - .5), alone, r, $"세 곳 함께 {three * 100:0}% · 망보기 {watch * 100:0}% · 혼자 {alone * 100:0}%"));
                        }
        var sb = new StringBuilder();
        sb.AppendLine($"## 첫 방문 조우 수치 탐색 · {tried}조합 중 합격 {hits.Count} · 그중 세 곳 모든 역할 40~60% {allRoles} · 현재 프리팹: {r0}");
        sb.AppendLine("현재 · " + Targets(w, r0).text);
        foreach (var h in hits.OrderBy(h => Math.Round(h.score, 2)).ThenBy(h => h.alone).Take(12)) sb.AppendLine(h.r + " → " + h.text);
        return sb.ToString();
    }

    // ---- a board visit (deterministic; each policy is one line) ----
    sealed class Run { public int Turns, Met, Hushes, Noise, Loud, Danger; public bool Shelf, Done, Timeout; public string Note = ""; }
    // role for every object (Plan). hushWhenNeeded: hush when a step comes in, it stands here, or the next search would raise 위험도.
    // lure: in the arcade search 오락기 뒤판 alone (prybar; its full turns are the only loud turns there) until it sets off, hush it past,
    // slip to the corridor and search the den shelf while the den stays empty. targets: sites in order (without a prybar the tool objects
    // and the storage are skipped). Moves step one room per turn; the storage lock: 1 + UnlockTurns turns, UnlockNoise on the first
    // (ExpeditionRoomNavigation.ConfirmMove). A met fight is won: its noise 1 and a turn.
    static Run Visit(World w, int danger, int gauge, int role, bool hushWhenNeeded, bool lure, IList<int> targets, bool prybar, int budget = 40)
    {
        var s = new FieldSiteState(w.Rules, () => 0, false, danger, gauge, FieldSiteState.Nowhere, 0); var run = new Run();
        var todo = new List<(int site, int room, int left, int noise)>();
        foreach (int t in targets)
        {
            if (!prybar && (w.Tool(t) || w.Room(t) == S)) { run.Note += " " + (Short.TryGetValue(t, out var n) ? n : w.Name(t)) + " 지렛대 없음"; continue; }
            var (turns, noise) = Plan(w, t, role); todo.Add((t, w.Room(t), turns, noise));
        }
        bool locked = prybar && todo.Any(x => x.room == S);
        int lureLeft = prybar ? FieldTurnPlan.RequiredOf(w.Turns[2], false) : 0, lureNoise = w.Noise[2];
        var (shelfLeft, shelfNoise) = Plan(w, w.DenSite, role == 1 ? 1 : 0);
        int room = A; s.MoveParty(A);
        void Turn(int noise, bool hushed)
        {
            s.EndTurn(noise, hushed); run.Turns++; run.Noise += noise; if (hushed) run.Hushes++; if (noise >= w.Rules.LoudNoise) run.Loud++;
            if (s.Encounter) { run.Met++; s.Driven(); s.EndTurn(1, false); run.Turns++; run.Noise++; }
        }
        while (run.Turns < budget)
        {
            if (lure && !run.Shelf)
            {
                if (room == A)
                {
                    bool lured = s.Remembered == A || s.ResidentRoom != DenRoom;
                    if (!lured) { if (lureLeft <= 0) { run.Note += prybar ? " 큰 소리 남지 않음" : " 지렛대 없음 · 꾀어내기 불가"; break; } Turn(lureNoise, false); lureLeft--; continue; }
                    if (s.ResidentRoom == A && s.Resident == ResidentState.Staying || s.ResidentRoom == DenRoom && s.Remembered < 0) { s.MoveParty(C); room = C; Turn(0, false); continue; } // slip out while it stays
                    Turn(0, true); continue;
                }
                if (room == C && s.DenStaysEmpty) { if (s.Incoming) { Turn(0, true); continue; } Turn(shelfNoise, false); if (--shelfLeft <= 0) run.Shelf = true; continue; }
                if (room == C && !s.DenEmpty && s.Incoming) { Turn(0, true); continue; }
            }
            if (todo.Count == 0) { run.Done = true; break; }
            if (!todo.Any(x => x.room == room))
            {
                int next = FieldSiteState.StepToward(room, todo[0].room); s.MoveParty(next); room = next;
                if (next == S && locked) { locked = false; int met = run.Met; for (int u = 0; u <= w.UnlockTurns && run.Met == met; u++) Turn(u == 0 ? w.UnlockNoise : 0, false); }
                else Turn(0, false);
                continue;
            }
            int k = todo.FindIndex(x => x.room == room); var t = todo[k];
            if (hushWhenNeeded && (s.Incoming || s.Visible || s.Preview(t.noise, false).danger > s.Danger)) { Turn(0, true); continue; }
            Turn(t.noise, false); todo[k] = (t.site, t.room, t.left - 1, t.noise); if (todo[k].left <= 0) todo.RemoveAt(k);
        }
        run.Timeout = run.Turns >= budget; run.Danger = s.Danger;
        if (!run.Shelf && s.DenEmpty && run.Met > 0) run.Note += " (굴 빈 틈 있음)";
        return run;
    }
    static readonly (int danger, int gauge)[] Starts = { (0, 0), (0, 2), (1, 1), (1, 2), (2, 0) };
    public static string SecondVisit() => SecondVisitReport(Load());
    static string SecondVisitReport(World w)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 두 번째 방문 · 운영별 (2인) · 턴 / 조우 / 숨죽이기 / 큰 소리 턴 / 끝 위험도 / 굴 선반 · 시작 (위험도,게이지)");
        sb.AppendLine(ObjectLine(w));
        var routes = new (string name, int[] sites, bool prybar)[] { ("도구 없음 · 상자·탁자·복도 상자", new[] { 0, 1, 5 }, false), ("지렛대 · 오락기·배전함·복도 상자·보관실 선반·자재", new[] { 2, 4, 5, 6, 7 }, true) };
        var policies = new (string name, int role, bool hush, bool lure)[] {
            ("조심(함께+숨죽이기)", 0, true, false), ("대충(함께)", 0, false, false), ("망보기+숨죽이기", 1, true, false), ("조명+숨죽이기", 2, true, false),
            ("혼자(1인)+숨죽이기", 3, true, false), ("꾀어내기→선반 (오락기 혼자)", 0, true, true) };
        foreach (var route in routes)
        {
            sb.AppendLine("### " + route.name);
            foreach (var p in policies)
            {
                // The lure spends 오락기 itself; the route's other objects follow once the shelf is done.
                var targets = p.lure ? route.sites.Where(x => x != 2).ToArray() : route.sites;
                sb.AppendLine(p.name + " · " + string.Join(" · ", Starts.Select(st =>
                {
                    var r = Visit(w, st.danger, st.gauge, p.role, p.hush, p.lure, targets, route.prybar);
                    return $"({st.danger},{st.gauge}) {r.Turns}턴 조우{r.Met} 숨{r.Hushes} 큰{r.Loud} 위{r.Danger}{(r.Shelf ? " 선반" : "")}{(r.Timeout ? " 시간초과" : "")}{r.Note}";
                })));
            }
        }
        return sb.ToString();
    }

    // ---- a board visit: the best play (breadth-first over every turn choice, no encounter allowed) ----
    // Choices per turn: a search step on an unfinished object in this room (a new one as 함께, or 망보기 on a noisy object; a started one keeps
    // its role), the den shelf from the corridor while the den stays empty, hush, or a move between the arcade and the corridor.
    static readonly FieldInfo[] StateFields = typeof(FieldSiteState).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(f => f.FieldType != typeof(FieldSiteRules) && !typeof(Delegate).IsAssignableFrom(f.FieldType)).ToArray();
    static string Key(FieldSiteState s, int[] prog) => string.Join(",", StateFields.Select(f => f.GetValue(s))) + "|" + string.Join(",", prog);
    // prog per object: 0 untouched, 1 + role × 10 + turns done while running, 99 finished.
    static (int shelf, int all, int states, string shelfPath) Best(World w, int danger, int gauge, int[] objs, int limit = 24)
    {
        var start = new FieldSiteState(w.Rules, () => 0, false, danger, gauge, FieldSiteState.Nowhere, 0);
        var frontier = new List<(FieldSiteState s, int[] prog, string path)> { (start, new int[objs.Length], "") }; var seen = new HashSet<string> { Key(start, new int[objs.Length]) };
        int shelf = -1, all = -1; string shelfPath = ""; bool hasShelf = objs.Contains(w.DenSite);
        for (int turn = 1; turn <= limit && frontier.Count > 0 && (hasShelf && shelf < 0 || all < 0); turn++)
        {
            var next = new List<(FieldSiteState, int[], string)>();
            foreach (var (s, prog, path) in frontier)
            {
                var options = new List<(int move, int obj, int role, bool hush)> { (-1, -1, 0, true), (s.PartyRoom == A ? C : A, -1, 0, false) };
                for (int o = 0; o < objs.Length; o++)
                {
                    int site = objs[o]; if (prog[o] == 99 || w.Room(site) != s.PartyRoom) continue;
                    if (site == w.DenSite && !s.DenStaysEmpty) continue;
                    if (prog[o] > 0) { options.Add((-1, o, (prog[o] - 1) / 10, false)); continue; }
                    options.Add((-1, o, 0, false)); if (w.Noise[site] > 0) options.Add((-1, o, 1, false));
                }
                foreach (var (move, obj, role, hush) in options)
                {
                    var c = s.Copy(); var pr = (int[])prog.Clone(); int noise = 0;
                    if (move >= 0) c.MoveParty(move);
                    if (obj >= 0)
                    {
                        var (turns, n) = Plan(w, objs[obj], role); noise = n;
                        int done = (pr[obj] == 0 ? 0 : (pr[obj] - 1) % 10) + 1;
                        pr[obj] = done >= turns ? 99 : 1 + role * 10 + done;
                    }
                    c.EndTurn(noise, hush);
                    if (c.Encounter) continue;
                    string step = hush ? "숨" : move >= 0 ? (move == A ? "→오락실" : "→복도") : (Short.TryGetValue(objs[obj], out var nm) ? nm : w.Name(objs[obj])) + RoleNames[role];
                    step += $"[위{c.Danger}·게{c.Gauge}·그것 {c.ResidentRoom}{(c.PassedBy ? " 지나감" : "")}]"; string np = path + (path.Length > 0 ? " " : "") + step;
                    int shelfIndex = Array.IndexOf(objs, w.DenSite);
                    if (shelfIndex >= 0 && pr[shelfIndex] == 99 && shelf < 0) { shelf = turn; shelfPath = np; }
                    if (Enumerable.Range(0, objs.Length).All(i => i == shelfIndex || pr[i] == 99) && all < 0) all = turn;
                    if (seen.Add(Key(c, pr))) next.Add((c, pr, np));
                }
            }
            frontier = next;
        }
        return (shelf, all, seen.Count, shelfPath);
    }
    public static string BestPlay() => BestPlayReport(Load());
    static string BestPlayReport(World w)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 두 번째 방문 · 가장 잘 풀었을 때 (조우 0회로 가능한 최소 턴, 24턴 안) · 굴 선반 / 나머지 모두 · 방 0 오락실 1 복도 3 관리실 -1 떠남");
        sb.AppendLine(ObjectLine(w));
        var sets = new (string name, int[] objs)[] { ("도구 없음 {상자,탁자,복도 상자,선반}", new[] { 0, 1, 5, w.DenSite }), ("지렛대 {오락기,복도 상자,선반}", new[] { 2, 5, w.DenSite }) };
        var starts = new[] { (0, 0), (0, 2), (1, 1), (1, 2), (2, 0), (2, 2) };
        foreach (var (name, objs) in sets)
        {
            sb.AppendLine(name + " · " + string.Join(" · ", starts.Select(st => { var r = Best(w, st.Item1, st.Item2, objs); return $"({st.Item1},{st.Item2}) 선반 {(r.shelf < 0 ? "불가" : r.shelf + "턴")} / 나머지 {(r.all < 0 ? "불가" : r.all + "턴")}"; })));
            foreach (var st in new[] { (0, 0), (1, 2) }) { var r = Best(w, st.Item1, st.Item2, objs); if (r.shelf > 0) sb.AppendLine($"  ({st.Item1},{st.Item2}) 선반 수순: " + r.shelfPath); }
        }
        return sb.ToString();
    }

    // ---- loot: expected finds from the real tables, per object, per visit and per board turn ----
    // Roles as Plan; only 조명 changes the chance (+LightBonus). The legacy pace term of ChanceFor is gone for new searches (pace 1).
    static int BonusOf(World w, int role) => role == 2 ? w.Light : 0;
    static double Expected(ExpeditionLootPanel.Site site, int bonus) => site.Drops == null ? 0 : site.Drops.Sum(d => d.Count * ExpeditionLootPanel.ChanceFor(d, bonus) / 100.0);
    // The opening intro grants cloth 1 before any search (SettlementIntroduction.Act, step 1).
    const int IntroCloth = 1;
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

    public static string Loot() => LootReport(Load());
    static string LootReport(World w)
    {
        var sb = new StringBuilder(); var sites = w.Sites; var roles = new[] { 0, 1, 2, 3 };
        string Set(int[] set) => "{" + string.Join(",", set) + "}";
        int TurnsOf(int[] set, int role) => set.Sum(i => Plan(w, i, role).turns);
        double Items(int[] set, int role) => set.Sum(i => Expected(sites[i], BonusOf(w, role)));

        // 1. Per object: expected items and items per search turn under each role.
        sb.AppendLine("## 수색 보상 기대값 · SettlementScreen.prefab 실제 표 · ExpeditionLootPanel.ChanceFor · 2인 (조명 +" + w.Light + " · 함께·망보기·혼자 보정 없음)");
        sb.AppendLine(ObjectLine(w));
        sb.AppendLine("### 사물별 · 기대 개수 (수색 턴당) · 확정 = 100% 행 합계 · 역할: " + string.Join(" · ", roles.Select(r => RoleNames[r])));
        for (int i = 0; i < sites.Length; i++)
        {
            var site = sites[i]; if (site.Room < 0 || site.Drops == null || site.Drops.Length == 0) continue;
            string cells = string.Join(" · ", roles.Select(r => { double e = Expected(site, BonusOf(w, r)); var p = Plan(w, i, r); return $"{e:0.00} ({e / p.turns:0.00}, {p.turns}턴 소음 {p.noise})"; }));
            sb.AppendLine($"[{i}] {w.Name(i)}{(w.Tool(i) ? " · " + site.RequiredTool : "")} · {cells} · 확정 {site.Drops.Where(d => d.Chance >= 100).Sum(d => d.Count)}");
        }

        // 2. Visit totals (items found, before bag slots): items, search turns, noise.
        sb.AppendLine("### 방문 합계 (찾은 물건 · 수색 턴 · 소음 합 · 가방 칸 제한 전)");
        foreach (var set in new[] { new[] { 0, 1, 5 }, new[] { 2, 4, 6, 7 } })
            sb.AppendLine(Set(set) + " · " + string.Join(" · ", roles.Select(r => $"{RoleNames[r]} {Items(set, r):0.0}개 {TurnsOf(set, r)}턴 소음 {TurnNoises(w, set, r).Sum()}")));

        // 3. A board visit: turns from Visit() (hush when needed, room steps, the storage lock), items per board turn.
        sb.AppendLine($"### 두 번째 방문 · 장소판 턴 (시작 위험도·게이지 {Starts.Length}종 평균, 필요할 때 숨죽이기, 한 턴에 한 사물, 방 이동 1턴, 보관실 첫 진입 {1 + w.UnlockTurns}턴·소음 {w.UnlockNoise}, 귀환 이동 제외, GoneTurns {w.Rules.GoneTurns})");
        foreach (var set in new[] { new[] { 0, 1, 5 }, new[] { 1, 5 }, new[] { 1, 5, 6, 7 }, new[] { 2, 4, 6, 7 }, new[] { 1, 2, 4, 5, 6, 7 } })
        {
            bool prybar = set.Any(i => w.Tool(i) || w.Room(i) == S); var perTurn = new double[4]; var cells = new List<string>();
            foreach (int r in roles)
            {
                var runs = Starts.Select(st => Visit(w, st.danger, st.gauge, r, true, false, set, prybar, 60)).ToArray();
                double turns = runs.Average(x => x.Turns), met = runs.Average(x => x.Met), loud = runs.Average(x => x.Loud), items = Items(set, r); perTurn[r] = items / turns;
                cells.Add($"{RoleNames[r]} {turns:0.0}턴 조우{met:0.0} 큰{loud:0.0} {items:0.0}개 턴당 {perTurn[r]:0.00}{(runs.Any(x => x.Timeout) ? " 시간초과" : "")}");
            }
            sb.AppendLine(Set(set) + (prybar ? " (지렛대)" : "") + " · " + string.Join(" · ", cells));
            sb.AppendLine($"  턴당 비 함께 / 조명 = {perTurn[0] / perTurn[2]:0.00} (함께는 시간, 조명은 +{w.Light}%p)");
        }

        // 4. Facilities after visit 2: 창고·작업대·잠자리·조리대 + the prybar chain (needed to open the storage), from the real recipes. Same role on both visits.
        var core = new (string id, int times)[] { ("build-stock", 1), ("build-bench", 1), ("build-bed", 1), ("build-cooker", 1), ("rope", 1), ("nails", 2), ("prybar", 1) };
        var desk = new (string id, int times)[] { ("build-stock", 1), ("build-bench", 1), ("build-bed", 1), ("build-cooker", 1), ("build-research", 1), ("rope", 1), ("nails", 2), ("prybar", 1) };
        var needCore = RawNeed(w.Recipes, core); var needDesk = RawNeed(w.Recipes, desk);
        string Need(Dictionary<string, int> n) => string.Join(" ", n.Select(kv => kv.Key + " " + kv.Value));
        sb.AppendLine($"### 방문 2 뒤 핵심 시설 수급 확률 (목표 80%+) · 핵심(창고·작업대·잠자리·조리대·지렛대 체인) {Need(needCore)} / 연구대 포함 {Need(needDesk)} · 도입 천 {IntroCloth} 차감 · 찾은 양 기준(가방 칸 무시)");
        var plans = new (string label, int[] first, int[] second)[] {
            ("튜토리얼 상자 → 보관실", new[] { 0 }, new[] { 6, 7 }), ("상자 → 탁자+보관실", new[] { 0 }, new[] { 1, 6, 7 }), ("상자 → 복도 상자+보관실", new[] { 0 }, new[] { 5, 6, 7 }),
            ("상자 → 탁자+복도 상자+보관실", new[] { 0 }, new[] { 1, 5, 6, 7 }), ("상자 → 지렛대 사물+보관실", new[] { 0 }, new[] { 2, 4, 6, 7 }), ("무료 세 곳 → 보관실", new[] { 0, 1, 5 }, new[] { 6, 7 }) };
        foreach (var plan in plans)
        {
            var rows = plan.first.Concat(plan.second).SelectMany(i => sites[i].Drops ?? new ExpeditionLootPanel.Drop[0]).ToArray();
            sb.AppendLine(plan.label + " " + Set(plan.first) + "→" + Set(plan.second) + " · " + string.Join(" · ", new[] { ("보정 없음(함께·망보기·혼자)", 0), ("조명", w.Light) }.Select(p =>
            {
                Func<ExpeditionLootPanel.Drop, int> chance = d => ExpeditionLootPanel.ChanceFor(d, p.Item2);
                return $"{p.Item1} {Afford(rows, chance, needCore) * 100:0}% / {Afford(rows, chance, needDesk) * 100:0}%";
            })));
        }
        sb.AppendLine("(값: 핵심 / 연구대 포함. 지렛대 체인은 물자 상자 확정분으로 방문 1에 보장 — BuildBalance1 가드)");
        return sb.ToString();
    }
}
