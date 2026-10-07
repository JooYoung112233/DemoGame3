using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Demo6.Core.Random;

namespace Demo6.Core.Dungeon
{
    /// <summary>생성기 입력(매판 새 탐험 1차 2-5 '입력').</summary>
    public sealed class GeneratorInput
    {
        public int Floor = 1;
        /// <summary>ExpeditionSeeds.Choose로 고른 씨앗. 1층 0 = 손 지도.</summary>
        public ulong Seed;
        /// <summary>이 층을 처음 밟는 원정인가(2-6 보상 감쇠: 다시 연 층은 쇠 궤짝을 능력 문 뒤 1개로).</summary>
        public bool FirstVisit = true;
        /// <summary>지금까지 도달한 가장 깊은 층(2-6 나무 궤짝·광맥 감쇠).</summary>
        public int DeepestFloor = 1;
        public bool HasPickaxe;
        public bool HasKey;
        /// <summary>이미 받은 '한 번 받는 것' id(FloorRecipe.OnceItems). 받은 것은 놓지 않는다.</summary>
        public ICollection<string> OnceDone = Array.Empty<string>();
        /// <summary>이 원정 앞의 밤 사건(2-5 차례 10, 층 예산 상한 안에서만 값 조정).</summary>
        public NightEvent Night;
    }

    /// <summary>
    /// 생성 결과: 지금과 같은 '글자 지도 문자열 + 범례'(FloorMap.Parse 그대로). 콘솔·시험 패널에 찍어 AI 도우미가 바로 읽고 고친다(2-5 차례 14).
    /// </summary>
    public sealed class GeneratedFloor
    {
        public int Floor;
        public string Name = "";
        /// <summary>요청한 씨앗.</summary>
        public ulong RequestedSeed;
        /// <summary>실제로 지은 씨앗(검사에 떨어지면 +1로 다시 굴림).</summary>
        public ulong Seed;
        /// <summary>지은 횟수(1 = 첫 굴림에 합격).</summary>
        public int Attempts = 1;
        /// <summary>50번 안에 합격하지 못해 그 층의 고른 지도로 대신했는가.</summary>
        public bool FellBack;
        /// <summary>손 지도(1층 씨앗 0)인가.</summary>
        public bool HandMap;
        /// <summary>지난 원정 지도와 너무 닮아 씨앗을 섞어 다시 지은 횟수(FloorGenerator.GenerateUnlike, 0 = 처음 씨앗 그대로).</summary>
        public int FreshRerolls;
        public string Glyphs = "";
        public CellDef[] Legend = Array.Empty<CellDef>();
        /// <summary>마지막 검사 결과.</summary>
        public RuleReport Report = new RuleReport();
        /// <summary>생성 덧칠 결과(1-2층 탐험 맛 1차 FloorSpice: 정예·순찰·낙석·가시 덫·구석 보상). 손 지도는 빈 보고.</summary>
        public SpiceReport Spice = new SpiceReport();

        /// <summary>FloorMap으로 읽는다(부를 때마다 새 객체).</summary>
        public FloorMap Build() => FloorMap.Parse(Floor, Name, Glyphs, Legend);

        /// <summary>콘솔·시험 패널용: 씨앗, 다시 굴림, 검사 결과, 글자 지도, 끝에 덧칠 한 줄(SpiceReport).</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.Append($"[{Floor}층 {Name}] 씨앗 {Seed}");
            if (Seed != RequestedSeed || Attempts > 1) sb.Append($" (요청 {RequestedSeed}, {Attempts}번째)");
            if (FreshRerolls > 0) sb.Append($" · 지난 원정과 닮아 {FreshRerolls}번 다시 굴림");
            if (FellBack) sb.Append(" · 고른 지도로 대신");
            if (HandMap) sb.Append(" · 손 지도");
            sb.Append('\n');
            if (Report != null) sb.Append(Report).Append('\n');
            sb.Append(Glyphs);
            if (Spice != null)
            {
                if (Glyphs.Length > 0 && Glyphs[Glyphs.Length - 1] != '\n') sb.Append('\n');
                sb.Append(Spice);
            }
            return sb.ToString();
        }

        /// <summary>칸마다 한 줄: 글자, 이름, 조각(기둥 번호), 자리 표시. 사람이 씨앗 지도를 눈으로 거를 때 쓴다.</summary>
        public string DescribeCells()
        {
            var sb = new StringBuilder();
            foreach (var d in Legend)
            {
                sb.Append(d.Glyph).Append(' ').Append(d.Name).Append(" (").Append(d.Piece);
                if (d.Piece == PieceKind.Clearing) sb.Append(" 기둥 ").Append(d.Pillars);
                sb.Append(')');
                for (int i = 0; i < d.Features.Length; i++)
                {
                    var f = d.Features[i];
                    sb.Append(i == 0 ? ": " : ", ");
                    if (f.Kind == FeatureKind.Group)
                    {
                        sb.Append("무리");
                        if (f.Elite) sb.Append(" 정예");
                        if (f.Boars > 0) sb.Append(" 돌충이").Append(f.Boars);
                        if (f.Archers > 0) sb.Append(" 궁수").Append(f.Archers);
                        if (f.Rats > 0) sb.Append(" 굴쥐").Append(f.Rats);
                        // 순찰은 오갈 칸을 붙인다(1-2층 탐험 맛 1차 4-3: 이웃 칸 id, 빈 글이면 제 칸 안).
                        sb.Append(f.State == GroupState.Sleep ? "(잠, "
                                : f.State == GroupState.Eat ? "(먹음, "
                                : "(순찰→" + (string.IsNullOrEmpty(f.PatrolCell) ? "제 칸" : f.PatrolCell) + ", ")
                            .Append(((int)f.FacingDeg).ToString(CultureInfo.InvariantCulture)).Append("°)");
                    }
                    else sb.Append(string.IsNullOrEmpty(f.Label) ? f.Kind.ToString() : f.Label);
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 층 생성기(매판 새 탐험 1차 2-5 '지도 만들기' 차례 1~14). 손으로 만든 조각을 칸 격자에 골라 잇고 조각 슬롯에 내용물을 담는다.
    /// 같은 입력이면 같은 지도(Pcg32Random 흐름 셋: ExpeditionSeeds.MapStream·ContentStream·DecorStream).
    /// 1층 씨앗 0은 FloorOneMap과 글자·범례가 똑같다. 검사(FloorRules.Check)에 떨어지면 씨앗 +1로 최대 50번, 그래도 떨어지면 고른 지도.
    /// 칸 id: 승강장 'E', 랜드마크는 범례 그대로(1층 'O'), 계단 칸 'S', 숨은 방 'H', 나머지는 겹치지 않게. 자리 표시 id는 "f{층}.{칸 id}.{이름}",
    /// 한 번 받는 물건은 FloorRecipe.OnceItems의 고정 id. 말뚝 역할은 칸 조각으로 정한다(MapAnchors.RoleOf).
    /// 층 첫 방문(FirstVisit)은 밤 사건·감쇠를 받지 않아 고른 씨앗이 늘 같은 지도가 된다(FloorBudget). 가진 능력은 지도 모양을 바꾸지 않는다:
    /// 금 간 벽은 곡괭이가 있으면 지름길·곁방 문, 없으면 맛보기 문이다.
    /// </summary>
    public static class FloorGenerator
    {
        public const int MaxAttempts = 50;

        /// <summary>손 지도를 쓰는 입력인가(1층 씨앗 0).</summary>
        public static bool IsHandMap(int floor, ulong seed) => floor == 1 && seed == 0UL;

        /// <summary>
        /// 지도를 짓는다. 예외를 던지지 않는다. 1층 씨앗 0은 손 지도, 층 예산이 없는 층은 손 지도로 대신하고 검사 결과에 적는다.
        /// </summary>
        public static GeneratedFloor Generate(GeneratorInput input)
        {
            if (input == null) input = new GeneratorInput();
            var recipe = FloorRecipe.For(input.Floor);
            if (recipe == null)
            {
                var none = HandMapResult(input, input.Floor, true);
                none.Report.Fail($"{input.Floor}층 예산 없음: 1층 손 지도로 대신");
                return none;
            }
            if (IsHandMap(input.Floor, input.Seed)) return HandMapResult(input, input.Floor, false);

            for (int i = 0; i < MaxAttempts; i++)
            {
                ulong seed = unchecked(input.Seed + (ulong)i);
                var built = TryBuild(recipe, input, seed);
                if (built == null || !built.Report.Passed) continue;
                built.RequestedSeed = input.Seed;
                built.Attempts = i + 1;
                return built;
            }
            var fallback = Fallback(recipe, input);
            fallback.RequestedSeed = input.Seed;
            fallback.Attempts = MaxAttempts;
            fallback.FellBack = true;
            return fallback;
        }

        /// <summary>
        /// 보스 굴(OgreDen 손 지도 "P-X", 묶음 7)을 짓는다. 씨앗·밤 사건·감쇠를 받지 않는 고정 돌방이라 늘 같은 결과다(HandMap = true, 씨앗 0).
        /// 검사는 FloorRules.CheckDen. 예외를 던지지 않는다(검사 중 오류는 실패 한 줄). floor는 굴이 딸린 층(2층).
        /// </summary>
        public static GeneratedFloor GenerateDen(int floor)
        {
            var g = new GeneratedFloor
            {
                Floor = floor,
                Name = OgreDen.Name,
                RequestedSeed = 0UL,
                Seed = 0UL,
                Attempts = 1,
                HandMap = true,
                Glyphs = OgreDen.Glyphs,
                Legend = OgreDen.Legend(),
            };
            try
            {
                g.Report = FloorRules.CheckDen(g.Build());
            }
            catch (Exception e)
            {
                g.Report = new RuleReport();
                g.Report.Fail("굴 검사 중 오류: " + e.Message);
            }
            return g;
        }

        /// <summary>지난 원정과 너무 닮은 지도를 피해 다시 굴리는 최대 횟수.</summary>
        public const int MaxFreshRerolls = 8;
        /// <summary>지난 원정과 견준 흔적(흙더미·갓 판 흙·깨진 돌)이 이보다 적으면 '바뀌었다'가 땅에서 잘 안 보여 다시 굴린다.</summary>
        public const int MinTraces = 2;

        /// <summary>
        /// 다시 연 층의 지도(2-3 단계 7, 2-5 차례 13): 지난 원정의 같은 층 글자 지도(previousGlyphs)와 너무 닮았으면 씨앗을 섞어 다시 짓는다
        /// (최대 MaxFreshRerolls번). 너무 닮음 = 칸 자리·길·벽 모양이 같음, 계단 칸이 같음(주 길이 같은 경우 포함), 흔적이 MinTraces개보다 적음.
        /// 모두 닮았으면 가장 덜 닮은 지도(MapDiff.Difference가 큰 것, 같으면 먼저 지은 것)를 쓴다. 같은 입력·같은 지난 지도면 늘 같은 결과다.
        /// 지난 지도가 없으면 Generate와 같다. 층 첫 방문(고른 지도)과 시험 패널이 정한 씨앗에는 쓰지 않는다(DungeonRoot).
        /// </summary>
        public static GeneratedFloor GenerateUnlike(GeneratorInput input, string previousGlyphs)
        {
            var first = Generate(input);
            if (input == null || string.IsNullOrEmpty(previousGlyphs)) return first;
            int want = MapDiff.StairsMovedScore + MinTraces;
            var best = first;
            int bestScore = MapDiff.Difference(previousGlyphs, first.Glyphs);
            ulong seed = input.Seed;
            for (int i = 1; i <= MaxFreshRerolls && bestScore < want; i++)
            {
                ulong next = ExpeditionSeeds.Mix(seed ^ unchecked((ulong)i * 0x9E3779B97F4A7C15UL));
                var g = Generate(WithSeed(input, next != 0UL ? next : 1UL));
                g.FreshRerolls = i;
                int score = MapDiff.Difference(previousGlyphs, g.Glyphs);
                if (score <= bestScore) continue;
                best = g;
                bestScore = score;
            }
            return best;
        }

        /// <summary>씨앗만 바꾼 입력 사본.</summary>
        static GeneratorInput WithSeed(GeneratorInput input, ulong seed) => new GeneratorInput
        {
            Floor = input.Floor,
            Seed = seed,
            FirstVisit = input.FirstVisit,
            DeepestFloor = input.DeepestFloor,
            HasPickaxe = input.HasPickaxe,
            HasKey = input.HasKey,
            OnceDone = input.OnceDone,
            Night = input.Night,
        };

        /// <summary>50번 안에 합격하지 못했을 때: 그 층의 고른 지도(1층은 손 지도).</summary>
        static GeneratedFloor Fallback(FloorRecipe recipe, GeneratorInput input)
        {
            if (IsHandMap(recipe.Floor, recipe.ChosenSeed)) return HandMapResult(input, recipe.Floor, true);
            var chosen = TryBuild(recipe, input, recipe.ChosenSeed);
            if (chosen != null && chosen.Report.Passed) return chosen;
            // 이번 입력으로는 떨어지면 층 첫 방문 입력으로 지은 고른 지도(테스트가 합격을 보장). 검사 결과는 이번 입력 기준으로 다시 적는다.
            var baseInput = FloorRules.FirstVisitInput(recipe);
            baseInput.OnceDone = input.OnceDone;
            baseInput.HasPickaxe = input.HasPickaxe;
            baseInput.HasKey = input.HasKey;
            var first = TryBuild(recipe, baseInput, recipe.ChosenSeed);
            if (first != null)
            {
                first.Report = SafeCheck(first, recipe, input);
                return first;
            }
            if (chosen != null) return chosen;
            var hand = HandMapResult(input, recipe.Floor, true);
            hand.Report.Fail("고른 지도도 짓지 못해 1층 손 지도로 대신");
            return hand;
        }

        static GeneratedFloor HandMapResult(GeneratorInput input, int floor, bool fellBack)
        {
            var g = new GeneratedFloor
            {
                Floor = floor,
                Name = FloorOneMap.Name,
                RequestedSeed = input.Seed,
                Seed = 0UL,
                Attempts = 1,
                FellBack = fellBack,
                HandMap = true,
                Glyphs = FloorOneMap.Glyphs,
                Legend = FloorOneMap.Legend(),
            };
            var recipe = FloorRecipe.For(FloorOneMap.Floor);
            // 손 지도는 층 첫 방문 지도라 첫 방문 개수로 검사한다. 대신 쓴 경우에는 이번 입력 기준으로 적는다(떨어지면 그대로 보임).
            g.Report = floor == FloorOneMap.Floor ? SafeCheck(g, recipe, fellBack ? input : FloorRules.FirstVisitInput(recipe)) : new RuleReport();
            return g;
        }

        static RuleReport SafeCheck(GeneratedFloor g, FloorRecipe recipe, GeneratorInput input)
        {
            try
            {
                return FloorRules.Check(g.Build(), recipe, input);
            }
            catch (Exception e)
            {
                var r = new RuleReport();
                r.Fail("검사 중 오류: " + e.Message);
                return r;
            }
        }

        /// <summary>
        /// 씨앗 하나로 한 번 짓는다. 모양을 못 지으면 null(다음 씨앗으로). 예외도 실패로 친다.
        /// 검사에 합격한 지도에만 생성 덧칠(1-2층 탐험 맛 1차 FloorSpice: 정예·순찰·함정·구석 보상)을 얹는다. 덧칠은 제 난수 흐름만 써서 지도·내용물은 그대로다.
        /// </summary>
        static GeneratedFloor TryBuild(FloorRecipe recipe, GeneratorInput input, ulong seed)
        {
            try
            {
                var built = new Builder(recipe, input, seed).Run();
                if (built != null && built.Report.Passed) built.Spice = FloorSpice.Apply(built, recipe, input);
                return built;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>한 씨앗의 지도 짓기(2-5 차례 1~12). 지도 흐름 → 조각 → 글자 → 장식 흐름(기둥) → 내용물 흐름 → 장식 흐름(이름) → 검사.</summary>
        sealed class Builder
        {
            enum Role
            {
                Landing,
                Main,
                Branch,
                Stairs,
                Hidden,
                Gated,
                Landmark,
            }

            /// <summary>칸 안에 놓은 자리(겹침 막기). Big = 무리·둥지 가운데, Decal = 바닥 분필.</summary>
            struct Placed
            {
                public float X;
                public float Y;
                public bool Big;
                public bool Decal;
            }

            sealed class Node
            {
                public int X;
                public int Y;
                public Role Role;
                /// <summary>(int)Side 쪽 문 종류. 없으면 null.</summary>
                public readonly EdgeKind?[] Doors = new EdgeKind?[4];
                /// <summary>지은 주 길에서 떨어진 칸 수(갈래 줄기 2칸 이하를 지키려고).</summary>
                public int DistMain;
                /// <summary>검사와 같은 주 길(FloorRules.MainPath) 위인가.</summary>
                public bool OnMain;
                /// <summary>승강장에서 들어오는 문 쪽((int)Side, 모르면 -1).</summary>
                public int EntrySide = -1;
                public PieceKind Piece;
                public bool Burrow;
                public int Pillars = -1;
                public char Glyph;
                public string Id = "";
                public string Name = "";
                public CellDef Def;
                public readonly List<CellFeature> Features = new List<CellFeature>();
                public readonly List<Placed> Used = new List<Placed>();

                public int Degree
                {
                    get
                    {
                        int n = 0;
                        foreach (var d in Doors)
                            if (d.HasValue) n++;
                        return n;
                    }
                }

                /// <summary>
                /// 곡괭이·열쇠 없이 지나갈 수 있는 문 수(열린 길·판자벽). 금 간 벽·자물쇠는 세지 않는다.
                /// 1이면 플레이어 눈에 막다른 곳이라 보상이 있어야 한다(2-5 '막다른 곳 빈손 0', FloorRules.CheckDeadEnds와 같은 기준).
                /// </summary>
                public int Exits
                {
                    get
                    {
                        int n = 0;
                        foreach (var d in Doors)
                            if (d == EdgeKind.Open || d == EdgeKind.Plank) n++;
                        return n;
                    }
                }

                public bool Generic => Role == Role.Main || Role == Role.Branch;
                public bool HasDoor(Side s) => Doors[(int)s].HasValue;

                public bool Has(FeatureKind kind)
                {
                    foreach (var f in Features)
                        if (f.Kind == kind) return true;
                    return false;
                }

                public bool HasReward()
                {
                    foreach (var f in Features)
                        if (FloorRules.IsReward(f.Kind)) return true;
                    return false;
                }

                public bool HasEncounter()
                {
                    foreach (var f in Features)
                        if (FloorRules.IsEncounter(f.Kind)) return true;
                    return false;
                }

                public bool HasChest() => Has(FeatureKind.WoodChest) || Has(FeatureKind.IronChest);
            }

            static readonly int[] Dx = { 1, 0, -1, 0 };
            static readonly int[] Dy = { 0, 1, 0, -1 };
            const string GlyphPool = "ABCDFGJKLMNPQRTUVWXYZ123456789";
            static readonly string[] ClearingNames = { "버팀목 공터", "흙더미 공터", "젖은 공터", "낮은 공터" };
            static readonly string[] CorridorNames = { "버팀목 통로", "좁은 갈래", "젖은 통로" };
            static readonly string[] RoomNames = { "무너진 곁방", "막다른 곁방", "버려진 곁방" };
            static readonly string[] GatedNames = { "무너진 곁방", "버려진 곁방", "연장 곁방" };

            readonly FloorRecipe _recipe;
            readonly GeneratorInput _input;
            readonly FloorBudget _budget;
            readonly ulong _seed;
            readonly int _floor;
            readonly int _w;
            readonly int _h;
            readonly Pcg32Random _map;
            readonly Pcg32Random _content;
            readonly Pcg32Random _decor;
            readonly Node[,] _grid;
            readonly List<Node> _nodes = new List<Node>();
            readonly List<Node> _gated = new List<Node>();
            readonly HashSet<string> _ids = new HashSet<string>();
            readonly List<OnceItem> _hiddenOnce = new List<OnceItem>();
            readonly List<OnceItem> _branchOnce = new List<OnceItem>();
            readonly List<OnceItem> _roomOnce = new List<OnceItem>();
            Node _landing;
            Node _stairs;
            Node _hidden;
            Node _landmark;
            Node _first;
            Node _plankNeighbor;
            /// <summary>랜드마크 자물쇠 문 앞에 일부러 뻗은 갈래 칸(주 길이 그 자리를 지나지 않을 때). 숨은 방이 여기 몰리지 않게 뺀다.</summary>
            Node _lockBranch;
            Node _burrow;
            List<Node> _main = new List<Node>();
            /// <summary>지은 주 길(승강장 → 계단). 고리를 더해도 이 길이 하나뿐인 가장 짧은 길로 남게 지킨다.</summary>
            readonly List<Node> _path = new List<Node>();
            /// <summary>숨은 방에 한 번 받는 것이 없을 때 놓을 궤짝(첫 방문 쇠, 다시 연 층 나무). 있으면 null.</summary>
            FeatureKind? _hiddenGeneral;
            int _gatedIron;
            int _gatedWood;
            int _ironExtra;
            int _woodFree;
            /// <summary>막다른 곳에 줄 수 있는 한 번 받는 보상: 명패처럼 주 길에서 갈래 1칸에 놓는 것, 막다른 방에 놓는 것.</summary>
            int _branchRewards;
            int _roomRewards;

            public Builder(FloorRecipe recipe, GeneratorInput input, ulong seed)
            {
                _recipe = recipe;
                _input = input;
                _budget = FloorBudget.For(recipe, input);
                _seed = seed;
                _floor = recipe.Floor;
                _w = Math.Max(1, recipe.MaxWidth);
                _h = Math.Max(1, recipe.MaxHeight);
                _grid = new Node[_w, _h];
                _map = new Pcg32Random(seed, ExpeditionSeeds.MapStream);
                _content = new Pcg32Random(seed, ExpeditionSeeds.ContentStream);
                _decor = new Pcg32Random(seed, ExpeditionSeeds.DecorStream);
            }

            public GeneratedFloor Run()
            {
                if (!PlanRewards()) return null;
                if (!Layout()) return null;
                if (!AssignPieces()) return null;
                AssignGlyphs();
                var legend = BuildLegend();
                string glyphs = WriteGlyphs();
                var map = FloorMap.Parse(_floor, _recipe.Name, glyphs, legend);
                if (!ReadMainPath(map)) return null;
                ChoosePillars();
                if (!PlaceContent()) return null;
                NameCells();
                foreach (var n in _nodes)
                {
                    if (n.Role == Role.Landmark) continue;
                    n.Def.Name = n.Name;
                    n.Def.Pillars = n.Pillars;
                    n.Def.Features = n.Features.ToArray();
                }
                return new GeneratedFloor
                {
                    Floor = _floor,
                    Name = _recipe.Name,
                    RequestedSeed = _seed,
                    Seed = _seed,
                    Glyphs = glyphs,
                    Legend = legend,
                    Report = FloorRules.Check(map, _recipe, _input),
                };
            }

            // ── 보상 계획(2-6): 숨은 방·능력 문 뒤·막다른 곳에 줄 궤짝 수를 먼저 나눈다 ──

            bool PlanRewards()
            {
                foreach (var o in _recipe.OnceItems)
                {
                    if (FloorBudget.Done(_input, o.Id)) continue;
                    switch (o.Place)
                    {
                        case OncePlace.HiddenRoom: _hiddenOnce.Add(o); break;
                        case OncePlace.BranchOffMainPath: _branchOnce.Add(o); break;
                        case OncePlace.DeadEndRoom: _roomOnce.Add(o); break;
                    }
                }
                int ironHidden = 0, woodHidden = 0, onceIronElsewhere = 0;
                foreach (var o in _hiddenOnce)
                    if (o.Kind == FeatureKind.IronChest) ironHidden++;
                if (_hiddenOnce.Count == 0)
                {
                    _hiddenGeneral = _input.FirstVisit ? FeatureKind.IronChest : FeatureKind.WoodChest;
                    if (_hiddenGeneral == FeatureKind.IronChest) ironHidden++;
                    else woodHidden++;
                }
                foreach (var o in _branchOnce)
                {
                    if (o.Kind == FeatureKind.IronChest) onceIronElsewhere++;
                    if (FloorRules.IsReward(o.Kind)) _branchRewards++;
                }
                foreach (var o in _roomOnce)
                {
                    if (o.Kind == FeatureKind.IronChest) onceIronElsewhere++;
                    if (FloorRules.IsReward(o.Kind)) _roomRewards++;
                }
                int gated = _budget.Gated;
                _gatedIron = Math.Min(gated, Math.Max(0, _budget.IronChests - ironHidden - onceIronElsewhere));
                _gatedWood = gated - _gatedIron;
                _ironExtra = _budget.IronChests - ironHidden - onceIronElsewhere - _gatedIron;
                _woodFree = _budget.WoodChests - woodHidden - _gatedWood;
                return _ironExtra >= 0 && _woodFree >= 0;
            }

            // ── 차례 1~7: 앵커, 계단, 주 길, 갈래, 숨은 방, 능력 문, 랜드마크 자물쇠, 고리 (지도 흐름) ──

            bool Layout()
            {
                _landing = Add(_recipe.LandingX, _recipe.LandingY, Role.Landing);
                if (_landing == null) return false;
                if (_recipe.Landmark != null)
                {
                    _landmark = Add(_recipe.LandmarkX, _recipe.LandmarkY, Role.Landmark);
                    if (_landmark == null) return false;
                }
                int reach = _budget.StartReachable;
                int pMin = Math.Max(4, (2 * reach + 4) / 5), pMax = reach / 2;
                if (pMin > pMax) return false;

                var path = ChooseMainPath(pMin, pMax, 3 + _budget.Gated);
                if (path == null) return false;
                Node prev = _landing;
                _path.Add(_landing);
                for (int i = 1; i < path.Count; i++)
                {
                    var n = Add(path[i][0], path[i][1], i == path.Count - 1 ? Role.Stairs : Role.Main);
                    if (n == null) return false;
                    Connect(prev, n, EdgeKind.Open);
                    _path.Add(n);
                    prev = n;
                }
                _stairs = prev;
                _first = At(path[1][0], path[1][1]);

                // 칸 수: 처음 갈 수 있는 칸 = 주 길 + 갈래 + 숨은 방(판자벽은 처음부터 부숨).
                int branches = reach - path.Count - 1;
                if (branches < 0) return false;
                if (_landmark != null && !HasLockTarget())
                {
                    if (branches == 0 || !AddBranch(_landmark)) return false;
                    branches--;
                }
                // 고리 먼저: 주 길 옆에 네모 고리를 일부러 만든다(칸이 모자라면 아래에서 이웃 칸을 잇는다).
                // 막다른 곳에 줄 궤짝이 거의 없으면(다시 연 깊은 층) 갈래를 되도록 모두 고리로 붙인다.
                while ((CountLoops() < _recipe.MinLoops || TightRewards) && branches > 0)
                {
                    int used = AddLoopSquare(branches);
                    if (used > 0)
                    {
                        branches -= used;
                        continue;
                    }
                    if (CountLoops() >= _recipe.MinLoops) break;
                    // 자리가 없으면 갈래 하나를 먼저 뻗고 다시 찾아본다(고리 몫으로 하나는 남김).
                    if (branches < 2 || !AddBranch(null)) break;
                    branches--;
                }
                // 문 하나뿐인 칸(숨은 방·능력 문 뒤 곁방)은 주 길 가까이 자리를 먼저 잡는다.
                // 막다른 곳에 줄 궤짝이 거의 없으면 갈래를 먼저 뻗고, 그 막다른 끝에 매단다.
                if (TightRewards)
                {
                    for (; branches > 0; branches--)
                        if (!AddBranch(null)) return false;
                }
                _hidden = AddLeaf(Role.Hidden, EdgeKind.Plank);
                if (_hidden == null) return false;
                for (int i = 0; i < _budget.Gated; i++)
                {
                    var g = AddLeaf(Role.Gated, EdgeKind.Cracked);
                    if (g == null) return false;
                    _gated.Add(g);
                }
                for (int i = 0; i < branches; i++)
                    if (!AddBranch(null)) return false;
                if (_landmark != null && !LockLandmark()) return false;
                while (CountLoops() < _recipe.MinLoops)
                    if (!AddLoop(false)) return false;
                AddShortcuts();

                // 막다른 곳: 보상을 줄 수 있는 만큼만 남기고 나머지는 고리로 잇는다(무너짐 밤에는 하나 더 남김).
                int want = _map.NextInt(1, 3) + _budget.ExtraDeadEnds;
                for (int guard = 0; guard < 12; guard++)
                {
                    int free = FreeDeadEnds().Count;
                    if (free <= want && free <= DeadEndCapacity()) break;
                    if (!AddLoop(true) && !RehangLeaf() && !RelocateDeadEnd()) break;
                }
                return FreeDeadEnds().Count <= DeadEndCapacity();
            }

            List<int[]> StairsSpots(int distance)
            {
                var list = new List<int[]>();
                if (distance < 3) return list;
                for (int x = 0; x < _w; x++)
                for (int y = 0; y < _h; y++)
                {
                    if (Math.Abs(x - _landing.X) + Math.Abs(y - _landing.Y) != distance) continue;
                    if (_grid[x, y] != null) continue;
                    list.Add(new[] { x, y });
                }
                return list;
            }

            /// <summary>
            /// 계단 자리와 주 길(2-5 차례 2·3). 길이 pMin~pMax(계단은 승강장에서 격자 거리 3 이상)인 되돌아가지 않는 길을 모두 모으고,
            /// 옆 빈칸(갈래·숨은 방·곁방 붙일 자리)이 roomNeeded − 1 이상인 길만 남긴다(그런 길이 없으면 가장 넉넉한 길).
            /// 그 가운데 계단 자리 → 승강장 출구 방향 → 길 차례로 뽑는다. 처음 맞는 길에서 멈추지 않으므로
            /// 출구·계단·주 길이 한쪽(곧은 가로 줄)에 몰리지 않는다(통합 검토: 2층 계단 한 자리 41%, 출구 늘 오른쪽).
            /// </summary>
            List<int[]> ChooseMainPath(int pMin, int pMax, int roomNeeded)
            {
                var paths = new List<List<int[]>>();
                var rooms = new List<int>();
                int best = -1;
                for (int p = pMin; p <= pMax; p++)
                    foreach (var spot in StairsSpots(p - 1))
                        foreach (var path in MonotonePaths(_landing.X, _landing.Y, spot[0], spot[1]))
                        {
                            int room = SideRoom(path);
                            if (room < 0) continue;
                            paths.Add(path);
                            rooms.Add(room);
                            best = Math.Max(best, room);
                        }
                if (paths.Count == 0) return null;
                int enough = Math.Min(roomNeeded - 1, best);
                // 계단 자리를 고르게 뽑고, 그 자리로 가는 출구 방향은 '그 출구로 갈 수 있는 계단 자리 수'의 제곱에 반비례해 뽑는다
                // (2층 위쪽 출구처럼 갈 곳이 적은 출구도 몫을 받는다 — 2층 출구 오른쪽 약 2/3, 위 1/3). 마지막으로 그 출구·자리의 길 가운데 하나.
                var ends = new List<int>();
                var exitEnds = new Dictionary<int, List<int>>();
                for (int i = 0; i < paths.Count; i++)
                {
                    if (rooms[i] < enough) continue;
                    int end = EndOf(paths[i]), exit = SideOf(paths[i]);
                    if (!ends.Contains(end)) ends.Add(end);
                    if (!exitEnds.TryGetValue(exit, out var list)) exitEnds[exit] = list = new List<int>();
                    if (!list.Contains(end)) list.Add(end);
                }
                ends.Sort();
                int pickEnd = ends[_map.NextInt(0, ends.Count)];
                var exits = new List<int>();
                var weights = new List<int>();
                int total = 0;
                for (int s = 0; s < 4; s++)
                {
                    if (!exitEnds.TryGetValue(s, out var list) || !list.Contains(pickEnd)) continue;
                    int weight = 840 / (list.Count * list.Count);
                    exits.Add(s);
                    weights.Add(weight);
                    total += weight;
                }
                int roll = _map.NextInt(0, total), index = 0;
                for (; index < exits.Count - 1; index++)
                {
                    if (roll < weights[index]) break;
                    roll -= weights[index];
                }
                int pickExit = exits[index];
                var options = new List<List<int[]>>();
                for (int i = 0; i < paths.Count; i++)
                    if (rooms[i] >= enough && EndOf(paths[i]) == pickEnd && SideOf(paths[i]) == pickExit) options.Add(paths[i]);
                return options[_map.NextInt(0, options.Count)];
            }

            /// <summary>길 끝(계단 자리) 열쇠.</summary>
            static int EndOf(List<int[]> path)
            {
                var last = path[path.Count - 1];
                return last[0] * 64 + last[1];
            }

            /// <summary>길의 승강장 출구 방향((int)Side).</summary>
            static int SideOf(List<int[]> path)
            {
                int dx = path[1][0] - path[0][0], dy = path[1][1] - path[0][1];
                for (int s = 0; s < 4; s++)
                    if (Dx[s] == dx && Dy[s] == dy) return s;
                return 0;
            }

            /// <summary>승강장에서 계단 자리까지 되돌아가지 않는 길 모두(가로 걸음 먼저 차례, 빈 칸만 지남 — 랜드마크는 피함).</summary>
            List<List<int[]>> MonotonePaths(int sx, int sy, int tx, int ty)
            {
                var result = new List<List<int[]>>();
                var path = new List<int[]> { new[] { sx, sy } };
                Walk(sx, sy);
                return result;

                void Walk(int x, int y)
                {
                    if (x == tx && y == ty)
                    {
                        result.Add(new List<int[]>(path));
                        return;
                    }
                    int nx = x + Math.Sign(tx - x), ny = y + Math.Sign(ty - y);
                    if (x != tx && Empty(nx, y))
                    {
                        path.Add(new[] { nx, y });
                        Walk(nx, y);
                        path.RemoveAt(path.Count - 1);
                    }
                    if (y != ty && Empty(x, ny))
                    {
                        path.Add(new[] { x, ny });
                        Walk(x, ny);
                        path.RemoveAt(path.Count - 1);
                    }
                }
            }

            /// <summary>
            /// 주 길 가운데 칸(승강장·계단 제외) 옆 빈칸 수(갈래·숨은 방·곁방을 붙일 자리). 랜드마크가 있으면 자물쇠 문 앞 칸이
            /// 주 길 가운데이거나 그 옆 빈칸이어야 하고(아니면 -1 = 못 쓰는 길), 옆 빈칸이면 자물쇠 갈래 몫이라 세지 않는다.
            /// </summary>
            int SideRoom(List<int[]> path)
            {
                var onPath = new HashSet<int>();
                foreach (var p in path) onPath.Add(p[0] * 16 + p[1]);
                var room = new HashSet<int>();
                for (int i = 1; i < path.Count - 1; i++)
                {
                    for (int s = 0; s < 4; s++)
                    {
                        int x = path[i][0] + Dx[s], y = path[i][1] + Dy[s];
                        if (!Empty(x, y) || onPath.Contains(x * 16 + y)) continue;
                        room.Add(x * 16 + y);
                    }
                }
                if (_landmark == null) return room.Count;
                var lockSpot = LockSpot();
                int key = lockSpot[0] * 16 + lockSpot[1];
                if (onPath.Contains(key))
                {
                    // 주 길 가운데 칸이면 그대로 자물쇠 문 앞(승강장·계단 칸은 갈래·주 길 칸이 아니라 안 됨).
                    var first = path[0];
                    var last = path[path.Count - 1];
                    if (key == first[0] * 16 + first[1] || key == last[0] * 16 + last[1]) return -1;
                    return room.Count;
                }
                if (!room.Remove(key)) return -1;
                return room.Count;
            }

            /// <summary>랜드마크 자물쇠 문 앞 칸 자리(층 예산 LandmarkDoor 쪽 이웃, 1층은 손 지도와 같은 오른쪽).</summary>
            int[] LockSpot()
            {
                int s = (int)_recipe.LandmarkDoor;
                return new[] { _landmark.X + Dx[s], _landmark.Y + Dy[s] };
            }

            /// <summary>자물쇠 문 앞 칸이 이미 갈래·주 길 칸(주 길에서 1칸 이하)인가.</summary>
            bool HasLockTarget()
            {
                var spot = LockSpot();
                var n = At(spot[0], spot[1]);
                return n != null && n.Generic && n.DistMain <= 1;
            }

            /// <summary>갈래 칸 하나(주 길에서 2칸 이하). lockFor가 있으면 그 칸(랜드마크)의 자물쇠 문 앞 자리에, 주 길에 바로 붙여 만든다.</summary>
            bool AddBranch(Node lockFor)
            {
                var cells = new List<int[]>();
                var weights = new List<int>();
                int total = 0;
                var lockSpot = lockFor != null ? LockSpot() : null;
                for (int x = 0; x < _w; x++)
                for (int y = 0; y < _h; y++)
                {
                    if (_grid[x, y] != null) continue;
                    if (lockSpot != null && (x != lockSpot[0] || y != lockSpot[1])) continue;
                    int attach = 0, generic = 0;
                    for (int s = 0; s < 4; s++)
                    {
                        var n = At(x + Dx[s], y + Dy[s]);
                        if (n == null || !n.Generic) continue;
                        generic++;
                        if (n.DistMain <= (lockFor != null ? 0 : 1)) attach++;
                    }
                    if (attach == 0) continue;
                    // 이웃 갈래·주 길이 둘 이상인 자리를 더 자주 골라 고리가 생길 틈을 만든다(막다른 곳에 줄 궤짝이 거의 없으면 더 세게).
                    int weight = generic >= 2 ? (TightRewards ? 8 : 3) : 1;
                    cells.Add(new[] { x, y });
                    weights.Add(weight);
                    total += weight;
                }
                if (cells.Count == 0) return false;
                int pick = _map.NextInt(0, total), index = 0;
                for (; index < cells.Count - 1; index++)
                {
                    if (pick < weights[index]) break;
                    pick -= weights[index];
                }
                var spot = cells[index];
                var attaches = new List<Node>();
                for (int s = 0; s < 4; s++)
                {
                    var n = At(spot[0] + Dx[s], spot[1] + Dy[s]);
                    if (n != null && n.Generic && n.DistMain <= (lockFor != null ? 0 : 1)) attaches.Add(n);
                }
                var a = attaches[_map.NextInt(0, attaches.Count)];
                var node = Add(spot[0], spot[1], Role.Branch);
                node.DistMain = a.DistMain + 1;
                Connect(a, node, EdgeKind.Open);
                if (lockFor != null) _lockBranch = node;
                return true;
            }

            /// <summary>
            /// 문 하나뿐인 칸(숨은 방·능력 문 뒤 곁방)을 주 길에서 1칸 이하인 칸에 붙인다. 빈 자리를 먼저 고르게 뽑고 그 자리 이웃 가운데 하나에 붙인다
            /// (이웃이 많은 자리에 몰리지 않게). 숨은 방(판자벽)은 막다른 갈래 끝에 먼저 붙여 막다른 곳을 줄이되, 랜드마크 자물쇠 문 앞 갈래는 뺀다
            /// (숨은 방이 사무실 문 옆에 몰렸음 — 통합 검토 1층 (1,3) 46%). 금 간 벽은 막다른 곳을 줄이지 못하므로 자리를 가리지 않는다.
            /// </summary>
            Node AddLeaf(Role role, EdgeKind kind)
            {
                var spots = new List<int[]>();
                var parents = new List<List<Node>>();
                var endSpots = new List<int[]>();
                var endParents = new List<List<Node>>();
                bool plank = kind == EdgeKind.Plank;
                for (int x = 0; x < _w; x++)
                for (int y = 0; y < _h; y++)
                {
                    if (_grid[x, y] != null) continue;
                    var near = new List<Node>();
                    var ends = new List<Node>();
                    for (int s = 0; s < 4; s++)
                    {
                        var n = At(x + Dx[s], y + Dy[s]);
                        if (n == null || !n.Generic || n.DistMain > 1) continue;
                        near.Add(n);
                        if (plank && n.Exits == 1 && n != _lockBranch) ends.Add(n);
                    }
                    if (near.Count == 0) continue;
                    spots.Add(new[] { x, y });
                    parents.Add(near);
                    if (ends.Count == 0) continue;
                    endSpots.Add(new[] { x, y });
                    endParents.Add(ends);
                }
                if (endSpots.Count > 0)
                {
                    spots = endSpots;
                    parents = endParents;
                }
                if (spots.Count == 0) return null;
                int i = _map.NextInt(0, spots.Count);
                var a = parents[i][_map.NextInt(0, parents[i].Count)];
                var leaf = Add(spots[i][0], spots[i][1], role);
                leaf.DistMain = a.DistMain + 1;
                Connect(a, leaf, kind);
                if (role == Role.Hidden) _plankNeighbor = a;
                return leaf;
            }

            /// <summary>랜드마크 자물쇠 문: 층 예산이 정한 쪽(LandmarkDoor) 이웃 칸과만 잇는다(돌로 지은 방이라 문 자리가 원정마다 같다).</summary>
            bool LockLandmark()
            {
                var spot = LockSpot();
                var a = At(spot[0], spot[1]);
                if (a == null || !a.Generic || a.DistMain > 1) return false;
                Connect(a, _landmark, EdgeKind.Locked);
                _landmark.DistMain = a.DistMain + 1;
                return true;
            }

            /// <summary>
            /// 이웃한 갈래·주 길 칸 사이에 열린 길을 더한다. deadEndsOnly면 막다른 칸을 잇는 길만.
            /// 주 길과 같은 길이의 다른 길이 생기면 되돌린다(검사가 읽는 주 길이 지은 주 길과 같게 — 갈래 줄기 2칸 이하가 지켜짐).
            /// </summary>
            bool AddLoop(bool deadEndsOnly)
            {
                var pairs = new List<Node[]>();
                foreach (var a in _nodes)
                {
                    if (!a.Generic) continue;
                    for (int s = 0; s < 2; s++)
                    {
                        var b = At(a.X + Dx[s], a.Y + Dy[s]);
                        if (b == null || !b.Generic || a.Doors[s].HasValue) continue;
                        if (deadEndsOnly && a.Exits != 1 && b.Exits != 1) continue;
                        pairs.Add(new[] { a, b });
                    }
                }
                Shuffle(pairs, _map);
                foreach (var pick in pairs)
                {
                    Connect(pick[0], pick[1], EdgeKind.Open);
                    if (UniqueMain()) return true;
                    Disconnect(pick[0], pick[1]);
                }
                return false;
            }

            /// <summary>고리 자리 후보: 모서리(빈 칸 하나를 대각선 두 칸에 이음) 또는 옆(이어진 두 칸 옆에 나란히 빈 칸 둘).</summary>
            sealed class LoopSpot
            {
                public Node A;
                public Node B;
                public int[] C;
                public int[] E;
            }

            /// <summary>
            /// 네모 고리 하나를 새 칸으로 만든다(2-5 차례 5). 쓴 칸 수(1~2)를 돌려주고, 자리가 없으면 0.
            /// 새 칸은 주 길에서 2칸 이하, 주 길과 같은 길이의 다른 길은 만들지 않는다.
            /// </summary>
            int AddLoopSquare(int budget)
            {
                var spots = new List<LoopSpot>();
                for (int x = 0; x < _w; x++)
                for (int y = 0; y < _h; y++)
                {
                    if (_grid[x, y] != null) continue;
                    var near = new List<Node>();
                    for (int s = 0; s < 4; s++)
                    {
                        var n = At(x + Dx[s], y + Dy[s]);
                        if (n != null && n.Generic && n.DistMain <= 1) near.Add(n);
                    }
                    for (int i = 0; i < near.Count; i++)
                    for (int j = i + 1; j < near.Count; j++)
                        if (near[i].X != near[j].X && near[i].Y != near[j].Y)
                            spots.Add(new LoopSpot { A = near[i], B = near[j], C = new[] { x, y } });
                }
                if (budget >= 2)
                {
                    foreach (var a in _nodes)
                    {
                        if (!a.Generic || a.DistMain > 1) continue;
                        for (int s = 0; s < 2; s++)
                        {
                            var b = At(a.X + Dx[s], a.Y + Dy[s]);
                            if (b == null || !b.Generic || b.DistMain > 1 || a.Doors[s] != EdgeKind.Open) continue;
                            // a–b와 나란히 옆(수직 방향 두 쪽)에 빈 칸 둘.
                            for (int t = 0; t < 4; t++)
                            {
                                if (t % 2 == s % 2) continue;
                                int cx = a.X + Dx[t], cy = a.Y + Dy[t], ex = b.X + Dx[t], ey = b.Y + Dy[t];
                                if (!Empty(cx, cy) || !Empty(ex, ey)) continue;
                                spots.Add(new LoopSpot { A = a, B = b, C = new[] { cx, cy }, E = new[] { ex, ey } });
                            }
                        }
                    }
                }
                Shuffle(spots, _map);
                foreach (var spot in spots)
                {
                    var c = Add(spot.C[0], spot.C[1], Role.Branch);
                    if (spot.E == null)
                    {
                        c.DistMain = Math.Min(spot.A.DistMain, spot.B.DistMain) + 1;
                        Connect(spot.A, c, EdgeKind.Open);
                        Connect(spot.B, c, EdgeKind.Open);
                        if (UniqueMain()) return 1;
                        Remove(c);
                        continue;
                    }
                    var e = Add(spot.E[0], spot.E[1], Role.Branch);
                    c.DistMain = spot.A.DistMain + 1;
                    e.DistMain = spot.B.DistMain + 1;
                    Connect(spot.A, c, EdgeKind.Open);
                    Connect(c, e, EdgeKind.Open);
                    Connect(e, spot.B, EdgeKind.Open);
                    if (UniqueMain()) return 2;
                    Remove(e);
                    Remove(c);
                }
                return budget >= 3 ? AddLoopBlock() : 0;
            }

            /// <summary>
            /// 주 길 칸 하나에 붙인 2×2 고리(새 칸 3: 옆 둘 + 대각선 하나). 모서리·옆 고리 자리가 없을 때만(작은 층의 꺾인 주 길).
            /// </summary>
            int AddLoopBlock()
            {
                var spots = new List<int[]>();
                foreach (var a in _nodes)
                {
                    if (!a.Generic || a.DistMain != 0) continue;
                    for (int h = 0; h < 4; h += 2)
                    for (int v = 1; v < 4; v += 2)
                    {
                        int cx = a.X + Dx[h], cy = a.Y + Dy[h], ex = a.X + Dx[v], ey = a.Y + Dy[v];
                        if (Empty(cx, cy) && Empty(ex, ey) && Empty(cx + Dx[v], cy + Dy[v])) spots.Add(new[] { a.X, a.Y, h, v });
                    }
                }
                Shuffle(spots, _map);
                foreach (var s in spots)
                {
                    var a = At(s[0], s[1]);
                    int h = s[2], v = s[3];
                    var c = Add(a.X + Dx[h], a.Y + Dy[h], Role.Branch);
                    var e = Add(a.X + Dx[v], a.Y + Dy[v], Role.Branch);
                    var d = Add(c.X + Dx[v], c.Y + Dy[v], Role.Branch);
                    c.DistMain = 1;
                    e.DistMain = 1;
                    d.DistMain = 2;
                    Connect(a, c, EdgeKind.Open);
                    Connect(a, e, EdgeKind.Open);
                    Connect(c, d, EdgeKind.Open);
                    Connect(e, d, EdgeKind.Open);
                    if (UniqueMain()) return 3;
                    Remove(d);
                    Remove(e);
                    Remove(c);
                }
                return 0;
            }

            /// <summary>열린 길만으로 승강장 → 계단 가장 짧은 길이 지은 주 길 하나뿐인가(길이도 그대로).</summary>
            bool UniqueMain()
            {
                var dist = new Dictionary<Node, int> { [_landing] = 0 };
                var ways = new Dictionary<Node, int> { [_landing] = 1 };
                var queue = new Queue<Node>();
                queue.Enqueue(_landing);
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    for (int s = 0; s < 4; s++)
                    {
                        if (c.Doors[s] != EdgeKind.Open) continue;
                        var n = At(c.X + Dx[s], c.Y + Dy[s]);
                        if (n == null) continue;
                        if (!dist.TryGetValue(n, out int d))
                        {
                            dist[n] = dist[c] + 1;
                            ways[n] = ways[c];
                            queue.Enqueue(n);
                        }
                        else if (d == dist[c] + 1) ways[n] = Math.Min(2, ways[n] + ways[c]);
                    }
                }
                return dist.TryGetValue(_stairs, out int steps) && steps == _path.Count - 1 && ways[_stairs] == 1;
            }

            /// <summary>
            /// 능력 문(금 간 벽) 수를 AbilityDoorsMin~Max로 맞춘다. 곁방 문(능력 문 뒤 칸) 말고 남은 문은 지름길:
            /// 이웃한 두 칸을 잇되 열린 길로는 3걸음 이상 떨어진 곳(2칸 넘게 줄임). 2층부터는 하나를 꼭 둔다.
            /// </summary>
            void AddShortcuts()
            {
                int doors = _gated.Count;
                int want = _map.NextInt(_recipe.AbilityDoorsMin, _recipe.AbilityDoorsMax + 1);
                if (_floor >= 2 && _recipe.AbilityDoorsMax > doors) want = Math.Max(want, doors + 1);
                while (doors < want)
                {
                    var pairs = new List<Node[]>();
                    var preferred = new List<Node[]>();
                    foreach (var a in _nodes)
                    {
                        if (!a.Generic && a.Role != Role.Stairs) continue;
                        for (int s = 0; s < 2; s++)
                        {
                            var b = At(a.X + Dx[s], a.Y + Dy[s]);
                            if (b == null || (!b.Generic && b.Role != Role.Stairs) || a.Doors[s].HasValue) continue;
                            if (OpenSteps(a, b) < 3) continue;
                            var pair = new[] { a, b };
                            pairs.Add(pair);
                            if (a.DistMain == 0 || b.DistMain == 0) preferred.Add(pair);
                        }
                    }
                    var from = preferred.Count > 0 ? preferred : pairs;
                    if (from.Count == 0) return;
                    var pick = from[_map.NextInt(0, from.Count)];
                    Connect(pick[0], pick[1], EdgeKind.Cracked);
                    doors++;
                }
            }

            /// <summary>막다른 곳에 줄 보상이 하나 이하(다시 연 깊은 층 감쇠 등)라 막다른 갈래를 거의 못 남기는가.</summary>
            bool TightRewards => _woodFree + _ironExtra + _branchRewards + _roomRewards <= 1;

            /// <summary>막다른 곳에 줄 수 있는 보상 수: 남는 나무·쇠 궤짝 + 주 길 바로 옆 막다른 칸에 놓을 명패 + 막다른 방에 놓을 것.</summary>
            int DeadEndCapacity()
            {
                int near = 0;
                foreach (var n in FreeDeadEnds())
                    if (n.DistMain == 1 && TouchesPathByOpen(n)) near++;
                return _woodFree + _ironExtra + Math.Min(_branchRewards, near) + _roomRewards;
            }

            /// <summary>주 길 칸(승강장·계단 포함)과 열린 길로 바로 이어진 칸인가('주 길에서 갈래 1칸').</summary>
            bool TouchesPathByOpen(Node n)
            {
                for (int s = 0; s < 4; s++)
                {
                    if (n.Doors[s] != EdgeKind.Open) continue;
                    var m = At(n.X + Dx[s], n.Y + Dy[s]);
                    if (m != null && (m.Role == Role.Main || m.Role == Role.Landing || m.Role == Role.Stairs)) return true;
                }
                return false;
            }

            /// <summary>
            /// 숨은 방의 판자벽 문을 옆에 있는 막다른 갈래 칸 쪽으로 옮겨 단다. 막다른 칸이 하나 줄고(그 칸은 이제 지나갈 문 둘),
            /// 원래 붙어 있던 칸이 새로 막다르게 되면 하지 않는다. 금 간 벽 곁방은 옮겨도 막다른 곳이 줄지 않고(곡괭이 없이는 못 지나감),
            /// 랜드마크 자물쇠 문은 자리가 정해져 있어 옮기지 않는다.
            /// </summary>
            bool RehangLeaf()
            {
                var leaf = _hidden;
                if (leaf == null) return false;
                int from = -1;
                for (int s = 0; s < 4; s++)
                    if (leaf.Doors[s].HasValue) from = s;
                if (from < 0) return false;
                var parent = At(leaf.X + Dx[from], leaf.Y + Dy[from]);
                if (parent == null || (parent.Generic && parent.Role == Role.Branch && parent.Exits <= 2)) return false;
                for (int s = 0; s < 4; s++)
                {
                    var d = At(leaf.X + Dx[s], leaf.Y + Dy[s]);
                    if (d == null || d == parent || !d.Generic || d.Exits != 1 || d.DistMain > 1 || d == _lockBranch) continue;
                    var kind = leaf.Doors[from].Value;
                    Disconnect(leaf, parent);
                    Connect(d, leaf, kind);
                    leaf.DistMain = d.DistMain + 1;
                    _plankNeighbor = d;
                    return true;
                }
                return false;
            }

            /// <summary>
            /// 고리로 이을 수 없는 막다른 갈래 칸 하나를 떼어, 갈래·주 길 칸 둘에 이웃한 빈칸으로 옮겨 고리로 만든다(칸 수 그대로). 못 옮기면 되돌린다.
            /// </summary>
            bool RelocateDeadEnd()
            {
                var ends = FreeDeadEnds();
                Shuffle(ends, _map);
                foreach (var d in ends)
                {
                    // 금 간 벽·자물쇠 문이 달린 칸은 옮기면 그 문 뒤 칸이 떨어져 나가므로 문 하나뿐인 칸만 옮긴다.
                    if (d.Role != Role.Branch || d.Degree != 1) continue;
                    Node parent = null;
                    for (int s = 0; s < 4; s++)
                        if (d.Doors[s].HasValue) parent = At(d.X + Dx[s], d.Y + Dy[s]);
                    if (parent == null) continue;
                    int ox = d.X, oy = d.Y, odist = d.DistMain;
                    Remove(d);
                    var spots = new List<int[]>();
                    for (int x = 0; x < _w; x++)
                    for (int y = 0; y < _h; y++)
                    {
                        if (!Empty(x, y) || (x == ox && y == oy)) continue;
                        int near = 0, close = 0;
                        for (int s = 0; s < 4; s++)
                        {
                            var n = At(x + Dx[s], y + Dy[s]);
                            if (n == null || !n.Generic) continue;
                            near++;
                            if (n.DistMain <= 1) close++;
                        }
                        if (near >= 2 && close >= 1) spots.Add(new[] { x, y });
                    }
                    Shuffle(spots, _map);
                    foreach (var spot in spots)
                    {
                        var n = Add(spot[0], spot[1], Role.Branch);
                        Node primary = null, second = null;
                        for (int s = 0; s < 4; s++)
                        {
                            var m = At(spot[0] + Dx[s], spot[1] + Dy[s]);
                            if (m == null || !m.Generic || m == n) continue;
                            if (primary == null && m.DistMain <= 1) primary = m;
                            else if (second == null) second = m;
                        }
                        if (primary != null && second != null)
                        {
                            n.DistMain = primary.DistMain + 1;
                            Connect(primary, n, EdgeKind.Open);
                            Connect(second, n, EdgeKind.Open);
                            if (UniqueMain()) return true;
                        }
                        Remove(n);
                    }
                    var back = Add(ox, oy, Role.Branch);
                    back.DistMain = odist;
                    Connect(parent, back, EdgeKind.Open);
                }
                return false;
            }

            /// <summary>막다른 갈래·주 길 칸: 곡괭이·열쇠 없이 지나갈 문(열린 길·판자벽)이 하나뿐(금 간 벽·자물쇠 문이 더 있어도 막다른 곳).</summary>
            List<Node> FreeDeadEnds()
            {
                var list = new List<Node>();
                foreach (var n in _nodes)
                    if (n.Generic && n.Exits == 1) list.Add(n);
                return list;
            }

            int OpenSteps(Node from, Node to)
            {
                var dist = new Dictionary<Node, int> { [from] = 0 };
                var queue = new Queue<Node>();
                queue.Enqueue(from);
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    if (c == to) return dist[c];
                    for (int s = 0; s < 4; s++)
                    {
                        if (c.Doors[s] != EdgeKind.Open) continue;
                        var n = At(c.X + Dx[s], c.Y + Dy[s]);
                        if (n == null || dist.ContainsKey(n)) continue;
                        dist[n] = dist[c] + 1;
                        queue.Enqueue(n);
                    }
                }
                return int.MaxValue;
            }

            int CountLoops()
            {
                int open = 0;
                foreach (var n in _nodes)
                    for (int s = 0; s < 4; s++)
                        if (n.Doors[s] == EdgeKind.Open) open++;
                open /= 2;
                var seen = new HashSet<Node>();
                int parts = 0;
                foreach (var start in _nodes)
                {
                    if (!seen.Add(start)) continue;
                    parts++;
                    var queue = new Queue<Node>();
                    queue.Enqueue(start);
                    while (queue.Count > 0)
                    {
                        var c = queue.Dequeue();
                        for (int s = 0; s < 4; s++)
                        {
                            if (c.Doors[s] != EdgeKind.Open) continue;
                            var n = At(c.X + Dx[s], c.Y + Dy[s]);
                            if (n != null && seen.Add(n)) queue.Enqueue(n);
                        }
                    }
                }
                return open - _nodes.Count + parts;
            }

            // ── 차례 8: 조각 (문 1개 = 막다른 방·막다른 공터, 3~4개 = 공터, 2개 = 통로나 공터) ──

            bool AssignPieces()
            {
                foreach (var n in _nodes)
                {
                    switch (n.Role)
                    {
                        case Role.Landing: n.Piece = PieceKind.Entrance; break;
                        case Role.Stairs: n.Piece = PieceKind.StairsRoom; break;
                        case Role.Hidden: n.Piece = PieceKind.Hidden; break;
                        case Role.Gated: n.Piece = PieceKind.Room; break;
                        case Role.Landmark: n.Piece = _recipe.Landmark.Piece; break;
                        default:
                        {
                            int degree = n.Degree;
                            if (n == _first || degree >= 3) n.Piece = PieceKind.Clearing;
                            else if (degree == 1) n.Piece = _map.NextInt(0, 2) == 0 ? PieceKind.Room : PieceKind.Clearing;
                            else n.Piece = _map.NextInt(0, 10) < (n.Role == Role.Main ? 6 : 4) ? PieceKind.Corridor : PieceKind.Clearing;
                            break;
                        }
                    }
                }
                // 새 쥐굴 밤: 늘어난 둥지를 문 두 개짜리 칸에 통로로 둔다('통로 +1'). 그런 칸이 없으면 공터에 둔다.
                if (_budget.BurrowCorridor && _budget.Nests > 0)
                {
                    var options = new List<Node>();
                    foreach (var n in _nodes)
                        if (n.Generic && n != _first && n.Degree == 2) options.Add(n);
                    if (options.Count > 0)
                    {
                        _burrow = options[_map.NextInt(0, options.Count)];
                        _burrow.Piece = PieceKind.Corridor;
                        _burrow.Burrow = true;
                    }
                }
                // 첫 공터 무리 밖의 무리·둥지는 공터 하나에 하나씩. 주 길 위 마주침 칸이 3칸 연달지 않게 모자라면 주 길 밖 칸부터 공터로 바꾼다.
                int need = _budget.Groups + _budget.Nests - (FirstMix() != null ? 1 : 0) - (_burrow != null ? 1 : 0);
                while (true)
                {
                    int room = MainEncounterRoom();
                    if (room < 0) return false;
                    if (OffMainClearings() + room >= need) return true;
                    var off = new List<Node>();
                    var on = new List<Node>();
                    foreach (var n in _nodes)
                        if (n.Generic && n != _first && !n.Burrow && n.Piece != PieceKind.Clearing) (n.Role == Role.Main ? on : off).Add(n);
                    var from = off.Count > 0 ? off : on;
                    if (from.Count == 0) return false;
                    from[_map.NextInt(0, from.Count)].Piece = PieceKind.Clearing;
                }
            }

            int OffMainClearings()
            {
                int count = 0;
                foreach (var n in _nodes)
                    if (n.Role == Role.Branch && n.Piece == PieceKind.Clearing) count++;
                return count;
            }

            /// <summary>주 길 위 공터에 무리·둥지를 몇 칸까지 둘 수 있나(첫 공터·쥐굴 통로는 늘 마주침, 3칸 연달음 금지). 안 되면 -1.</summary>
            int MainEncounterRoom()
            {
                int run = 0, room = 0;
                for (int i = 0; i < _path.Count; i++)
                {
                    var n = _path[i];
                    bool nextForced = i + 1 < _path.Count && (_path[i + 1] == _first || _path[i + 1] == _burrow);
                    if (n == _first || n == _burrow)
                    {
                        run++;
                        if (run > FloorRules.MaxEncounterRun) return -1;
                    }
                    else if (n.Generic && n.Piece == PieceKind.Clearing && run + 1 < FloorRules.MaxEncounterRun + (nextForced ? 0 : 1))
                    {
                        run++;
                        room++;
                    }
                    else run = 0;
                }
                return room;
            }

            GroupMix FirstMix()
            {
                foreach (var g in _recipe.Groups)
                    if (g.FirstClearing) return g;
                return _recipe.Groups.Length > 0 ? _recipe.Groups[0] : null;
            }

            // ── 글자·범례·글자 지도 ──

            void AssignGlyphs()
            {
                int next = 0;
                char landmarkGlyph = _landmark != null ? _recipe.Landmark.Glyph : '\0';
                foreach (var n in ReadingOrder())
                {
                    switch (n.Role)
                    {
                        case Role.Landing: n.Glyph = 'E'; break;
                        case Role.Stairs: n.Glyph = 'S'; break;
                        case Role.Hidden: n.Glyph = 'H'; break;
                        case Role.Landmark: n.Glyph = landmarkGlyph; break;
                        default:
                            while (next < GlyphPool.Length && GlyphPool[next] == landmarkGlyph) next++;
                            n.Glyph = next < GlyphPool.Length ? GlyphPool[next++] : '?';
                            break;
                    }
                    n.Id = n.Role == Role.Landmark ? _recipe.Landmark.Id : n.Glyph.ToString();
                }
            }

            CellDef[] BuildLegend()
            {
                var list = new List<CellDef>();
                foreach (var n in ReadingOrder())
                {
                    n.Def = n.Role == Role.Landmark
                        ? CloneDef(_recipe.Landmark)
                        : new CellDef { Glyph = n.Glyph, Id = n.Id, Name = "", Piece = n.Piece };
                    if (n.Role == Role.Landmark)
                        foreach (var f in n.Def.Features)
                            _ids.Add(f.Id);
                    list.Add(n.Def);
                }
                return list.ToArray();
            }

            static CellDef CloneDef(CellDef d)
            {
                var features = new CellFeature[d.Features.Length];
                for (int i = 0; i < features.Length; i++)
                {
                    var f = d.Features[i];
                    features[i] = new CellFeature
                    {
                        Kind = f.Kind, Local = f.Local, Id = f.Id, Label = f.Label, Boars = f.Boars, Archers = f.Archers, Rats = f.Rats,
                        State = f.State, FacingDeg = f.FacingDeg, Param = f.Param,
                    };
                }
                return new CellDef { Glyph = d.Glyph, Id = d.Id, Name = d.Name, Piece = d.Piece, Pillars = d.Pillars, Features = features };
            }

            /// <summary>FloorMap.Parse가 읽는 글자 지도. 맨 윗줄이 가장 높은 y, 아랫줄(y 0)은 비어 있어도 적어 칸 자리가 원정마다 같게 한다.</summary>
            string WriteGlyphs()
            {
                int width = 0, height = 0;
                foreach (var n in _nodes)
                {
                    width = Math.Max(width, n.X + 1);
                    height = Math.Max(height, n.Y + 1);
                }
                var sb = new StringBuilder();
                for (int row = 0; row < height * 2 - 1; row++)
                {
                    var line = new char[width * 2 - 1];
                    for (int i = 0; i < line.Length; i++) line[i] = '.';
                    if (row % 2 == 0)
                    {
                        int y = height - 1 - row / 2;
                        for (int x = 0; x < width; x++)
                        {
                            var n = At(x, y);
                            if (n == null) continue;
                            line[x * 2] = n.Glyph;
                            if (n.Doors[(int)Side.Right].HasValue && x * 2 + 1 < line.Length)
                                line[x * 2 + 1] = EdgeGlyph(n.Doors[(int)Side.Right].Value, false);
                        }
                    }
                    else
                    {
                        int y = height - 1 - (row + 1) / 2;
                        for (int x = 0; x < width; x++)
                        {
                            var n = At(x, y);
                            if (n != null && n.Doors[(int)Side.Up].HasValue) line[x * 2] = EdgeGlyph(n.Doors[(int)Side.Up].Value, true);
                        }
                    }
                    int end = line.Length;
                    while (end > 1 && line[end - 1] == '.') end--;
                    sb.Append(line, 0, end).Append('\n');
                }
                return sb.ToString();
            }

            static char EdgeGlyph(EdgeKind kind, bool vertical)
            {
                switch (kind)
                {
                    case EdgeKind.Plank: return ':';
                    case EdgeKind.Cracked: return '#';
                    case EdgeKind.Locked: return '=';
                    default: return vertical ? '|' : '-';
                }
            }

            /// <summary>검사와 같은 주 길을 읽고, 승강장에서 들어오는 문 쪽을 칸마다 적는다(열린 길·판자벽 먼저, 그 밖은 하나뿐인 문).</summary>
            bool ReadMainPath(FloorMap map)
            {
                var path = FloorRules.MainPath(map);
                if (path.Count < 2) return false;
                _main = new List<Node>();
                foreach (var c in path)
                {
                    var n = At(c.X, c.Y);
                    if (n == null) return false;
                    n.OnMain = true;
                    _main.Add(n);
                }
                if (_main.Count != _path.Count) return false;
                for (int i = 0; i < _main.Count; i++)
                    if (_main[i] != _path[i]) return false;
                var queue = new Queue<Node>();
                var seen = new HashSet<Node> { _landing };
                queue.Enqueue(_landing);
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    for (int s = 0; s < 4; s++)
                    {
                        var kind = c.Doors[s];
                        if (kind != EdgeKind.Open && kind != EdgeKind.Plank) continue;
                        var n = At(c.X + Dx[s], c.Y + Dy[s]);
                        if (n == null || !seen.Add(n)) continue;
                        n.EntrySide = (s + 2) % 4;
                        queue.Enqueue(n);
                    }
                }
                foreach (var n in _nodes)
                {
                    if (n.EntrySide >= 0 || n == _landing) continue;
                    for (int s = 0; s < 4; s++)
                        if (n.Doors[s].HasValue) n.EntrySide = s;
                }
                return true;
            }

            void ChoosePillars()
            {
                foreach (var n in ReadingOrder())
                    if (n.Piece == PieceKind.Clearing && n.Role != Role.Landmark) n.Pillars = _decor.NextInt(0, PieceSlots.PillarSetCount);
            }

            // ── 차례 9~11: 내용물 (내용물 흐름) ──

            bool PlaceContent()
            {
                // 승강장: 늘 같은 돌방이라 첫 자리(말뚝·등잔)를 쓴다.
                var stakeSlot = FirstSlot(_landing, SlotKind.Stake);
                var lampSlot = FirstSlot(_landing, SlotKind.Lamp);
                if (stakeSlot == null || lampSlot == null) return false;
                _ids.Add(FloorRules.LandingStakeId(_floor));
                Put(_landing, stakeSlot.Value, FeatureKind.Stake, FloorRules.LandingStakeId(_floor), "승강장 말뚝");
                Put(_landing, lampSlot.Value, FeatureKind.WallLamp, NewId(_landing, "lamp"), "벽 등잔");

                // 계단 앞: 계단은 문에서 가장 먼 자리, 말뚝은 들어오는 문 가까이.
                var stairsSlot = BestSlot(_stairs, SlotKind.Stairs, false, false, s => MinDoorDistance(_stairs, s));
                if (stairsSlot == null) return false;
                Put(_stairs, stairsSlot.Value, FeatureKind.Stairs, NewId(_stairs, "stairs"), (_floor + 1) + "층 계단");
                var stairsStake = BestSlot(_stairs, SlotKind.Stake, false, false, s => -EntryDistance(_stairs, s));
                if (stairsStake == null) return false;
                Put(_stairs, stairsStake.Value, FeatureKind.Stake, NewId(_stairs, "stake"), "계단 말뚝");
                if (!PutRandom(_stairs, SlotKind.Lamp, FeatureKind.WallLamp, "lamp", "벽 등잔")) return false;

                // 숨은 방: 아직 안 받은 곡괭이·보장 상자, 다 받았으면 첫 방문은 쇠 궤짝, 다시 연 층은 나무 궤짝.
                foreach (var o in _hiddenOnce)
                    if (!PutOnce(_hidden, o)) return false;
                if (_hiddenGeneral == FeatureKind.IronChest && !PutRandom(_hidden, SlotKind.Chest, FeatureKind.IronChest, "iron", "쇠 궤짝")) return false;
                if (_hiddenGeneral == FeatureKind.WoodChest && !PutRandom(_hidden, SlotKind.Chest, FeatureKind.WoodChest, "wood", "나무 궤짝")) return false;

                // 1층 분필 3개: 판자벽 문 가운데에서 3~6유닛, 숨은 방 이웃 칸에, 판자벽 문을 가리킴.
                int plankSide = _plankNeighbor != null ? SideTo(_plankNeighbor, _hidden) : -1;
                if (_floor == 1 && plankSide >= 0)
                {
                    var door = PieceSlots.DoorCenter((Side)plankSide);
                    for (int i = 1; i <= 3; i++)
                    {
                        var slot = RandomSlot(_plankNeighbor, SlotKind.Chalk, false, true, s => s.DoorSide == plankSide);
                        if (slot == null) return false;
                        float deg = (float)(Math.Atan2(door.Y - slot.Value.Local.Y, door.X - slot.Value.Local.X) * 180.0 / Math.PI);
                        int rounded = (int)Math.Round(deg);
                        var chalk = Put(_plankNeighbor, slot.Value, FeatureKind.Chalk, NewId(_plankNeighbor, "chalk" + i), "분필 그림",
                            rounded.ToString(CultureInfo.InvariantCulture));
                        chalk.FacingDeg = rounded;
                    }
                }

                // 숨은 방 단서: 판자벽 문 14유닛 안 등잔(불꽃이 판자 틈 쪽으로 기움).
                // 층 첫 방문은 늘, 다시 연 층은 70%만(1-2층 탐험 맛 1차 4-8, 굴림은 따로 흐름 5). 못 두면 그 등잔은 아래에서 다른 칸에 놓인다.
                int lampsLeft = _budget.Lamps - 2;
                bool clueRolled = FloorSpice.ClueLampRoll(_input, _seed);
                if (lampsLeft > 0 && plankSide >= 0 && clueRolled)
                {
                    var door = PieceSlots.DoorCenter((Side)plankSide);
                    var clue = RandomSlot(_plankNeighbor, SlotKind.Lamp, false, false, s => Distance(s.Local, door) <= 12.5f) ??
                               BestSlot(_plankNeighbor, SlotKind.Lamp, false, false, s => -Distance(s.Local, door));
                    if (clue != null)
                    {
                        Put(_plankNeighbor, clue.Value, FeatureKind.WallLamp, NewId(_plankNeighbor, "lamp"), "벽 등잔");
                        lampsLeft--;
                    }
                }

                if (!PlaceEncounters()) return false;

                // 명패: 주 길에서 갈래 1칸(막다른 곳 먼저). 쪽지: 막다른 방(능력 문 뒤 곁방 포함)·숨은 방 먼저.
                foreach (var o in _branchOnce)
                {
                    var options = new List<Node>();
                    var deadEnds = new List<Node>();
                    foreach (var n in _nodes)
                    {
                        if (!n.Generic || n.OnMain || !TouchesPathByOpen(n)) continue;
                        options.Add(n);
                        if (n.Exits == 1 && !n.HasReward()) deadEnds.Add(n);
                    }
                    if (!PutOnceSomewhere(deadEnds.Count > 0 ? deadEnds : options, o)) return false;
                }
                foreach (var o in _roomOnce)
                {
                    var rooms = new List<Node>(_gated);
                    foreach (var n in _nodes)
                        if (n.Generic && n.Piece == PieceKind.Room && n.Degree == 1) rooms.Add(n);
                    var tiers = new List<List<Node>> { rooms, new List<Node> { _hidden }, FreeDeadEnds(), GenericNodes() };
                    bool placed = false;
                    foreach (var tier in tiers)
                        if (!placed && tier.Count > 0 && PutOnceSomewhere(tier, o)) placed = true;
                    if (!placed) return false;
                }

                // 능력 문 뒤 곁방: 쇠 궤짝(모자라면 나무 궤짝).
                for (int i = 0; i < _gated.Count; i++)
                {
                    bool iron = i < _gatedIron;
                    if (!PutRandom(_gated[i], SlotKind.Chest, iron ? FeatureKind.IronChest : FeatureKind.WoodChest, iron ? "iron" : "wood",
                            iron ? "쇠 궤짝" : "나무 궤짝")) return false;
                }

                // 막다른 곳 보상 100%: 나무 궤짝 먼저, 모자라면 남는 쇠 궤짝.
                int woodLeft = _woodFree, ironLeft = _ironExtra;
                foreach (var n in Shuffled(FreeDeadEnds()))
                {
                    if (n.HasReward()) continue;
                    if (woodLeft > 0 && PutRandom(n, SlotKind.Chest, FeatureKind.WoodChest, "wood", "나무 궤짝")) woodLeft--;
                    else if (ironLeft > 0 && PutRandom(n, SlotKind.Chest, FeatureKind.IronChest, "iron", "쇠 궤짝")) ironLeft--;
                    else return false;
                }

                // 광맥(공터·곁방 구석), 사건(통로 먼저), 남은 등잔, 남은 궤짝.
                for (int i = 0; i < _budget.Ores; i++)
                {
                    var options = new List<Node>();
                    foreach (var n in GenericNodes())
                        if ((n.Piece == PieceKind.Clearing || n.Piece == PieceKind.Room) && !n.Has(FeatureKind.Ore)) options.Add(n);
                    if (!PutInOne(options, SlotKind.Ore, FeatureKind.Ore, "ore", "광맥")) return false;
                }
                for (int i = 0; i < _budget.Events; i++)
                {
                    var corridors = new List<Node>();
                    var quiet = new List<Node>();
                    foreach (var n in GenericNodes())
                    {
                        if (n.Has(FeatureKind.Lunchbox)) continue;
                        if (n.Piece == PieceKind.Corridor) corridors.Add(n);
                        else if (!n.HasEncounter()) quiet.Add(n);
                    }
                    if (!PutInOne(corridors, SlotKind.Event, FeatureKind.Lunchbox, "lunchbox", "광부 도시락통") &&
                        !PutInOne(quiet, SlotKind.Event, FeatureKind.Lunchbox, "lunchbox", "광부 도시락통") &&
                        !PutInOne(GenericNodes(), SlotKind.Event, FeatureKind.Lunchbox, "lunchbox", "광부 도시락통")) return false;
                }
                for (; lampsLeft > 0; lampsLeft--)
                {
                    // 단서 등잔 굴림에 떨어진 다시 연 층은 남은 등잔도 숨은 방 이웃 칸을 비켜 간다(4-8·시험 8: 이웃 칸 등잔 60~80%).
                    // 다른 칸에 못 두면 이웃 칸도 쓴다. 굴림에 붙은 층(첫 방문 포함)은 예전과 같은 후보·같은 난수 차례다.
                    var options = new List<Node>();
                    var anywhere = new List<Node>();
                    foreach (var n in GenericNodes())
                        if (!n.Has(FeatureKind.WallLamp))
                        {
                            anywhere.Add(n);
                            if (clueRolled || n != _plankNeighbor) options.Add(n);
                        }
                    if (!PutInOne(options, SlotKind.Lamp, FeatureKind.WallLamp, "lamp", "벽 등잔") &&
                        (options.Count == anywhere.Count || !PutInOne(anywhere, SlotKind.Lamp, FeatureKind.WallLamp, "lamp", "벽 등잔"))) return false;
                }
                for (; woodLeft > 0; woodLeft--)
                    if (!PutChestAnywhere(FeatureKind.WoodChest, "wood", "나무 궤짝")) return false;
                for (; ironLeft > 0; ironLeft--)
                    if (!PutChestAnywhere(FeatureKind.IronChest, "iron", "쇠 궤짝")) return false;
                return true;
            }

            /// <summary>
            /// 무리·둥지(2-5 차례 9): 승강장 옆 첫 칸은 첫 공터 무리(자는 중, 승강장 반대쪽을 봄). 나머지는 공터 하나에 하나씩,
            /// 주 길 위 마주침 칸이 3칸 연달아 서지 않게 고른다. 새 쥐굴 둥지는 쥐굴 통로에.
            /// </summary>
            bool PlaceEncounters()
            {
                var firstMix = FirstMix();
                var others = new List<GroupMix>();
                foreach (var g in _recipe.Groups)
                    if (g != firstMix) others.Add(g);
                if (firstMix != null && !PutGroup(_first, firstMix, true)) return false;
                int nests = _budget.Nests;
                if (_burrow != null && nests > 0)
                {
                    if (!PutNest(_burrow)) return false;
                    nests--;
                }
                int need = others.Count + nests;
                var clearings = new List<Node>();
                foreach (var n in ReadingOrder())
                    if (n.Generic && n != _first && n.Piece == PieceKind.Clearing) clearings.Add(n);
                if (clearings.Count < need) return false;
                List<Node> chosen = null;
                for (int tries = 0; tries < 12 && chosen == null; tries++)
                {
                    var order = Shuffled(clearings);
                    var pick = order.GetRange(0, need);
                    if (MainRunOk(pick)) chosen = pick;
                }
                if (chosen == null)
                {
                    // 주 길 밖 공터부터 채우고, 모자라면 주 길 공터를 연달음이 생기지 않는 만큼만 더한다.
                    chosen = new List<Node>();
                    var off = new List<Node>();
                    var on = new List<Node>();
                    foreach (var n in clearings) (n.OnMain ? on : off).Add(n);
                    foreach (var n in Shuffled(off))
                        if (chosen.Count < need) chosen.Add(n);
                    foreach (var n in Shuffled(on))
                    {
                        if (chosen.Count >= need) break;
                        chosen.Add(n);
                        if (!MainRunOk(chosen)) chosen.Remove(n);
                    }
                    if (chosen.Count < need) return false;
                    chosen = Shuffled(chosen);
                }
                for (int i = 0; i < chosen.Count; i++)
                {
                    bool ok = i < others.Count ? PutGroup(chosen[i], others[i], false) : PutNest(chosen[i]);
                    if (!ok) return false;
                }
                return true;
            }

            bool MainRunOk(List<Node> pick)
            {
                int run = 0;
                foreach (var n in _main)
                {
                    bool encounter = n == _first || n == _burrow || pick.Contains(n);
                    run = encounter ? run + 1 : 0;
                    if (run > FloorRules.MaxEncounterRun) return false;
                }
                return true;
            }

            bool PutGroup(Node n, GroupMix mix, bool first)
            {
                int entry = n.EntrySide;
                var slots = Candidates(n, SlotKind.Group, true, false, null);
                if (slots.Count == 0) return false;
                // 들어오는 문에서 먼 자리 절반 가운데 하나(문 앞에 몰려 있지 않게, 등 뒤로 다가갈 틈).
                if (entry >= 0)
                {
                    var door = PieceSlots.DoorCenter((Side)entry);
                    slots.Sort((a, b) => Distance(b.Local, door).CompareTo(Distance(a.Local, door)));
                }
                var slot = slots[_content.NextInt(0, (slots.Count + 1) / 2)];
                var state = first ? GroupState.Sleep : mix.State;
                float facing;
                if (entry < 0) facing = SideDeg(_content.NextInt(0, 4));
                else if (state == GroupState.Sleep) facing = SideDeg((entry + 2) % 4);
                else
                {
                    int turn = _content.NextInt(1, 4);
                    facing = SideDeg((entry + turn) % 4);
                }
                int boars = mix.Boars, archers = mix.Archers;
                if (_budget.WeakGroups && boars + archers > 1)
                {
                    if (boars > 0)
                    {
                        boars = 1;
                        archers = 0;
                    }
                    else archers = 1;
                }
                var feature = Put(n, slot, FeatureKind.Group, NewId(n, "group"), mix.Label);
                feature.Boars = boars;
                feature.Archers = archers;
                feature.Rats = mix.Rats;
                feature.State = state;
                feature.FacingDeg = facing;
                // 정예 무리(1-2층 탐험 맛 1차 4-1: 2층 계단 앞). 다시 연 층은 FloorSpice가 무리마다 다시 정한다.
                feature.Elite = mix.Elite;
                return true;
            }

            bool PutNest(Node n)
            {
                var slot = RandomSlot(n, SlotKind.Nest, true, false, null);
                if (slot == null) return false;
                Put(n, slot.Value, FeatureKind.Nest, NewId(n, "nest"), "굴쥐 둥지");
                return true;
            }

            bool PutOnce(Node n, OnceItem o)
            {
                var kind = o.Kind == FeatureKind.IronChest || o.Kind == FeatureKind.WoodChest ? SlotKind.Chest : SlotKind.Small;
                var slot = RandomSlot(n, kind, false, false, null);
                if (slot == null) return false;
                _ids.Add(o.Id);
                Put(n, slot.Value, o.Kind, o.Id, o.Label, o.Param);
                return true;
            }

            bool PutOnceSomewhere(List<Node> options, OnceItem o)
            {
                foreach (var n in Shuffled(options))
                    if (PutOnce(n, o)) return true;
                return false;
            }

            bool PutInOne(List<Node> options, SlotKind slotKind, FeatureKind kind, string name, string label)
            {
                foreach (var n in Shuffled(options))
                    if (PutRandom(n, slotKind, kind, name, label)) return true;
                return false;
            }

            bool PutChestAnywhere(FeatureKind kind, string name, string label)
            {
                var empty = new List<Node>();
                foreach (var n in GenericNodes())
                    if (!n.HasChest()) empty.Add(n);
                return PutInOne(empty, SlotKind.Chest, kind, name, label) || PutInOne(GenericNodes(), SlotKind.Chest, kind, name, label);
            }

            bool PutRandom(Node n, SlotKind slotKind, FeatureKind kind, string name, string label)
            {
                var slot = RandomSlot(n, slotKind, false, false, null);
                if (slot == null) return false;
                Put(n, slot.Value, kind, NewId(n, name), label);
                return true;
            }

            CellFeature Put(Node n, PieceSlot slot, FeatureKind kind, string id, string label, string param = "")
            {
                var feature = new CellFeature { Kind = kind, Local = slot.Local, Id = id, Label = label, Param = param ?? "" };
                n.Features.Add(feature);
                n.Used.Add(new Placed
                {
                    X = slot.Local.X,
                    Y = slot.Local.Y,
                    Big = kind == FeatureKind.Group || kind == FeatureKind.Nest,
                    Decal = kind == FeatureKind.Chalk,
                });
                return feature;
            }

            /// <summary>쓸 수 있는 자리: 조각·기둥 벌의 슬롯 가운데 이 칸 문 짜임에 맞고, 이미 놓은 것과 겹치지 않는 자리.</summary>
            List<PieceSlot> Candidates(Node n, SlotKind kind, bool big, bool decal, Func<PieceSlot, bool> filter)
            {
                var list = new List<PieceSlot>();
                foreach (var s in PieceSlots.SlotsFor(n.Piece, n.Pillars))
                {
                    if (s.Kind != kind || !PieceSlots.Usable(s, n.HasDoor)) continue;
                    if (filter != null && !filter(s)) continue;
                    if (!SpaceFree(n, s.Local, big, decal)) continue;
                    list.Add(s);
                }
                return list;
            }

            PieceSlot? RandomSlot(Node n, SlotKind kind, bool big, bool decal, Func<PieceSlot, bool> filter)
            {
                var list = Candidates(n, kind, big, decal, filter);
                if (list.Count == 0) return null;
                return list[_content.NextInt(0, list.Count)];
            }

            PieceSlot? BestSlot(Node n, SlotKind kind, bool big, bool decal, Func<PieceSlot, float> score)
            {
                PieceSlot? best = null;
                float bestScore = float.MinValue;
                foreach (var s in Candidates(n, kind, big, decal, null))
                {
                    float v = score(s);
                    if (best != null && v <= bestScore) continue;
                    best = s;
                    bestScore = v;
                }
                return best;
            }

            static PieceSlot? FirstSlot(Node n, SlotKind kind)
            {
                foreach (var s in PieceSlots.SlotsFor(n.Piece, n.Pillars))
                    if (s.Kind == kind) return s;
                return null;
            }

            static bool SpaceFree(Node n, Offset p, bool big, bool decal)
            {
                foreach (var u in n.Used)
                {
                    // 바닥 분필은 무엇과도 1.2, 무리·둥지 가운데는 졸개가 둘레에 퍼지므로 3, 그 밖은 2.
                    float need = decal || u.Decal ? 1.2f : big || u.Big ? 3f : 2f;
                    float dx = u.X - p.X, dy = u.Y - p.Y;
                    if (dx * dx + dy * dy < need * need) return false;
                }
                return true;
            }

            static float Distance(Offset a, Offset b)
            {
                float dx = a.X - b.X, dy = a.Y - b.Y;
                return (float)Math.Sqrt(dx * dx + dy * dy);
            }

            static float MinDoorDistance(Node n, PieceSlot s)
            {
                float best = float.MaxValue;
                for (int side = 0; side < 4; side++)
                    if (n.Doors[side].HasValue) best = Math.Min(best, Distance(s.Local, PieceSlots.DoorCenter((Side)side)));
                return best;
            }

            static float EntryDistance(Node n, PieceSlot s) =>
                n.EntrySide >= 0 ? Distance(s.Local, PieceSlots.DoorCenter((Side)n.EntrySide)) : 0f;

            static float SideDeg(int side) => side * 90f;

            /// <summary>a에서 이웃 b 쪽 문 방향((int)Side). 이웃이 아니면 -1.</summary>
            static int SideTo(Node a, Node b)
            {
                for (int s = 0; s < 4; s++)
                    if (a.X + Dx[s] == b.X && a.Y + Dy[s] == b.Y) return s;
                return -1;
            }

            string NewId(Node n, string name)
            {
                string baseId = "f" + _floor + "." + n.Id + "." + name;
                string id = baseId;
                for (int k = 2; _ids.Contains(id) || FloorRecipe.IsOnceItem(id) || id == FloorRules.LandingStakeId(_floor); k++) id = baseId + k;
                _ids.Add(id);
                return id;
            }

            // ── 장식 흐름: 칸 이름(짧은 한국어, 인물 이름 없음) ──

            void NameCells()
            {
                foreach (var n in ReadingOrder())
                {
                    switch (n.Role)
                    {
                        case Role.Landing: n.Name = "승강장"; break;
                        case Role.Stairs: n.Name = "계단 앞"; break;
                        case Role.Hidden: n.Name = "숨은 방"; break;
                        case Role.Landmark: break;
                        case Role.Gated: n.Name = Pick(GatedNames); break;
                        default:
                            if (n.Piece == PieceKind.Clearing)
                            {
                                if (n == _first) n.Name = "첫 공터";
                                else if (n.Has(FeatureKind.Nest)) n.Name = "쥐굴 공터";
                                else if (n.Has(FeatureKind.Ore)) n.Name = "광맥 공터";
                                else if (n.Degree == 1) n.Name = "막다른 공터";
                                else if (n.Degree >= 3) n.Name = "갈림 공터";
                                else n.Name = Pick(ClearingNames);
                            }
                            else if (n.Piece == PieceKind.Corridor)
                            {
                                bool straight = (n.HasDoor(Side.Left) && n.HasDoor(Side.Right)) || (n.HasDoor(Side.Up) && n.HasDoor(Side.Down));
                                n.Name = n.Burrow ? "새 쥐굴" : straight ? Pick(CorridorNames) : "꺾인 통로";
                            }
                            else n.Name = Pick(RoomNames);
                            break;
                    }
                }
            }

            string Pick(string[] names) => names[_decor.NextInt(0, names.Length)];

            // ── 격자 도우미 ──

            Node At(int x, int y) => x >= 0 && y >= 0 && x < _w && y < _h ? _grid[x, y] : null;

            bool Empty(int x, int y) => x >= 0 && y >= 0 && x < _w && y < _h && _grid[x, y] == null;

            Node Add(int x, int y, Role role)
            {
                if (!Empty(x, y)) return null;
                var n = new Node { X = x, Y = y, Role = role };
                _grid[x, y] = n;
                _nodes.Add(n);
                return n;
            }

            /// <summary>되돌리기: 칸과 그 칸의 문을 지운다.</summary>
            void Remove(Node n)
            {
                for (int s = 0; s < 4; s++)
                {
                    if (!n.Doors[s].HasValue) continue;
                    var m = At(n.X + Dx[s], n.Y + Dy[s]);
                    if (m != null) m.Doors[(s + 2) % 4] = null;
                    n.Doors[s] = null;
                }
                _grid[n.X, n.Y] = null;
                _nodes.Remove(n);
            }

            void Connect(Node a, Node b, EdgeKind kind)
            {
                int s = SideTo(a, b);
                if (s < 0) throw new InvalidOperationException("이웃이 아닌 칸을 이으려 했다");
                a.Doors[s] = kind;
                b.Doors[(s + 2) % 4] = kind;
            }

            void Disconnect(Node a, Node b)
            {
                int s = SideTo(a, b);
                if (s < 0) return;
                a.Doors[s] = null;
                b.Doors[(s + 2) % 4] = null;
            }

            static void Shuffle<T>(List<T> list, IRandom rng)
            {
                for (int i = list.Count - 1; i > 0; i--)
                {
                    int j = rng.NextInt(0, i + 1);
                    var t = list[i];
                    list[i] = list[j];
                    list[j] = t;
                }
            }

            /// <summary>위 줄부터 왼쪽에서 오른쪽(글자 지도 읽는 차례).</summary>
            List<Node> ReadingOrder()
            {
                var list = new List<Node>();
                for (int y = _h - 1; y >= 0; y--)
                for (int x = 0; x < _w; x++)
                    if (_grid[x, y] != null) list.Add(_grid[x, y]);
                return list;
            }

            List<Node> GenericNodes()
            {
                var list = new List<Node>();
                foreach (var n in ReadingOrder())
                    if (n.Generic) list.Add(n);
                return list;
            }

            /// <summary>내용물 흐름으로 섞은 새 목록.</summary>
            List<Node> Shuffled(List<Node> source)
            {
                var list = new List<Node>(source);
                Shuffle(list, _content);
                return list;
            }
        }
    }
}
