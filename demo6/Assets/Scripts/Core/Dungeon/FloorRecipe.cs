using System;
using System.Collections.Generic;

namespace Demo6.Core.Dungeon
{
    /// <summary>'층에 한 번' 물건이 놓일 자리 규칙(매판 새 탐험 1차 2-5 차례 6·9).</summary>
    public enum OncePlace
    {
        /// <summary>숨은 방(판자벽 뒤): 곡괭이, 희귀 이상 무기 보장 상자.</summary>
        HiddenRoom,
        /// <summary>주 길에서 갈래 1칸(명패).</summary>
        BranchOffMainPath,
        /// <summary>막다른 방·숨은 방 먼저(쪽지).</summary>
        DeadEndRoom,
        /// <summary>랜드마크 안 고정(1층 사무실 금고·쪽지 ②). 생성기는 옮기지 않는다.</summary>
        Landmark,
    }

    /// <summary>
    /// 프로필에 한 번 받는 물건(2-4 '층에 한 번 받는 것'). 받기 전까지는 원정마다 새 자리에 다시 나오고, 받으면 영구다.
    /// id는 칸과 상관없이 고정이다(예: 보장 상자 'f1.H.iron'은 어느 칸에 놓여도 같은 id). 꾸러미(CarryData.OnceDone)가 이 id를 들고 다닌다.
    /// </summary>
    public sealed class OnceItem
    {
        public string Id;
        public FeatureKind Kind;
        public string Label = "";
        public string Param = "";
        public OncePlace Place;
    }

    /// <summary>무리 하나의 구성(3차 초안 3-1 마주침 틀).</summary>
    public sealed class GroupMix
    {
        public int Boars;
        public int Archers;
        public int Rats;
        public GroupState State = GroupState.Sleep;
        public string Label = "";
        /// <summary>승강장 옆 첫 칸의 무리: 승강장 반대쪽을 보고 자서 첫 1분 기습을 배운다(2-5 차례 9). 2층은 궁수 첫 공터.</summary>
        public bool FirstClearing;
    }

    /// <summary>
    /// 층 예산 표(매판 새 탐험 1차 2-5 '입력', 3차 초안 2-2·2-5·3-2). 생성기 입력이자 고른 씨앗을 적어 두는 곳.
    /// 숫자는 층 첫 방문 기준이다. 다시 연 층의 감쇠(2-6)는 생성기가 GeneratorInput.FirstVisit·DeepestFloor로 적용한다.
    /// 첫 시험판은 1층 + 이야기 없는 2층 9칸(5장)이다.
    /// </summary>
    public sealed class FloorRecipe
    {
        /// <summary>첫 시험판에서 갈 수 있는 가장 깊은 층.</summary>
        public const int MaxTestFloor = 2;

        public int Floor;
        public string Name = "";
        /// <summary>격자 상한(큰 지도가 세로 4칸까지 안전, 2-5 '격자').</summary>
        public int MaxWidth = 6;
        public int MaxHeight = 4;
        /// <summary>칸 수와 처음 갈 수 있는 칸 수(자물쇠·금 간 벽 뒤 제외).</summary>
        public int Cells;
        public int StartReachable;
        /// <summary>승강장 칸 자리(층마다 고정).</summary>
        public int LandingX;
        public int LandingY;
        /// <summary>랜드마크 칸(고정). null이면 없음. 자물쇠 문은 승강장이 아닌 이웃 칸 하나로 난다.</summary>
        public CellDef Landmark;
        public int LandmarkX;
        public int LandmarkY;
        /// <summary>
        /// 랜드마크 자물쇠 문이 난 쪽(고정). 돌로 지은 방이라 원정마다 문 자리가 같고, 이어지는 길만 바뀐다(2-4 '자리·안 내용 그대로').
        /// 1층 광업소 사무실은 손 지도와 같은 오른쪽.
        /// </summary>
        public Side LandmarkDoor = Side.Right;
        /// <summary>일반 무리 구성(목록 길이 = 일반 마주침 수).</summary>
        public GroupMix[] Groups = Array.Empty<GroupMix>();
        public int Nests;
        public int WoodChests;
        /// <summary>쇠 궤짝(보장 상자 포함).</summary>
        public int IronChests;
        public int Ores;
        public int Lamps;
        /// <summary>말뚝 수(승강장 + 계단 앞 [+ 가운데]).</summary>
        public int Stakes;
        /// <summary>사건 수(1~5층은 광부 도시락통).</summary>
        public int Events;
        /// <summary>한 바퀴 도는 길 최소 수(1~4층 1, 5층부터 2).</summary>
        public int MinLoops = 1;
        /// <summary>가진 능력의 문 수(2층부터 그중 하나는 주 길을 약 2칸 줄이는 지름길) + 아직 없는 능력의 맛보기 문 0~1.</summary>
        public int AbilityDoorsMin = 1;
        public int AbilityDoorsMax = 2;
        /// <summary>층 첫 방문에 쓰는 고른 씨앗. 1층은 0(= 손 지도 FloorOneMap).</summary>
        public ulong ChosenSeed;
        /// <summary>프로필에 한 번 받는 물건(받은 것은 생성기가 빼고 놓는다).</summary>
        public OnceItem[] OnceItems = Array.Empty<OnceItem>();

        /// <summary>이 층의 예산. 없으면 null.</summary>
        public static FloorRecipe For(int floor) => floor >= 1 && floor <= Table.Length ? Table[floor - 1] : null;

        /// <summary>첫 시험판에서 갈 수 있는 층인가.</summary>
        public static bool Exists(int floor) => floor >= 1 && floor <= MaxTestFloor && For(floor) != null;

        /// <summary>이 id가 프로필에 한 번 받는 물건인가(꾸러미에 담을 것).</summary>
        public static bool IsOnceItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (var r in Table)
                foreach (var o in r.OnceItems)
                    if (o.Id == id) return true;
            return false;
        }

        static readonly FloorRecipe[] Table = { FloorOne(), FloorTwo() };

        static FloorRecipe FloorOne()
        {
            CellDef office = null;
            foreach (var d in FloorOneMap.Legend())
                if (d.Piece == PieceKind.Office) office = d;
            return new FloorRecipe
            {
                Floor = 1,
                Name = FloorOneMap.Name,
                Cells = 12,
                StartReachable = 10,
                LandingX = 0,
                LandingY = 1,
                Landmark = office,
                LandmarkX = 0,
                LandmarkY = 2,
                LandmarkDoor = Side.Right,
                Groups = new[]
                {
                    new GroupMix { Boars = 1, Rats = 3, State = GroupState.Sleep, Label = "멧돼지와 굴쥐", FirstClearing = true },
                    new GroupMix { Boars = 1, Rats = 2, State = GroupState.Eat, Label = "멧돼지와 굴쥐" },
                    new GroupMix { Rats = 4, State = GroupState.Eat, Label = "굴쥐 무리" },
                    new GroupMix { Boars = 1, Rats = 2, State = GroupState.Sleep, Label = "멧돼지와 굴쥐" },
                },
                Nests = 1,
                WoodChests = 3,
                IronChests = 2,
                Ores = 1,
                Lamps = 4,
                Stakes = 2,
                Events = 1,
                MinLoops = 1,
                AbilityDoorsMin = 1,
                AbilityDoorsMax = 2,
                ChosenSeed = 0UL,
                OnceItems = new[]
                {
                    new OnceItem { Id = "f1.H.pickaxe", Kind = FeatureKind.Pickaxe, Label = "쓰러진 광부의 곡괭이", Place = OncePlace.HiddenRoom },
                    new OnceItem { Id = "f1.H.iron", Kind = FeatureKind.IronChest, Label = "쇠 궤짝", Param = "rare-weapon", Place = OncePlace.HiddenRoom },
                    new OnceItem { Id = "f1.T.nameplate", Kind = FeatureKind.Nameplate, Label = "광부 명패", Place = OncePlace.BranchOffMainPath },
                    new OnceItem { Id = "f1.K.note1", Kind = FeatureKind.Note, Label = "쪽지 ①", Param = "note1", Place = OncePlace.DeadEndRoom },
                    new OnceItem { Id = "f1.O.note2", Kind = FeatureKind.Note, Label = "쪽지 ②", Param = "note2", Place = OncePlace.Landmark },
                    new OnceItem { Id = "f1.O.safe", Kind = FeatureKind.Safe, Label = "광업소 금고", Place = OncePlace.Landmark },
                },
            };
        }

        /// <summary>
        /// 이야기 없는 2층 9칸(3차 초안 7-2·10-1 질문 1 B). 개수는 3차 2층(15칸) 예산을 9칸으로 줄인 가안이다.
        /// 처음 갈 수 있는 칸 8이라 주 길은 정확히 4칸(50%)이다. 씨앗 500개 시험에서 모두 합격해 개수는 그대로 두고 능력 문만 1~2로 넓혔다.
        /// 고른 지도(씨앗 30, 글자 지도는 FloorGeneratorTests.ChosenFloorTwo):
        /// <code>
        /// ....A
        /// ....#
        /// E-B-C-S
        /// ..|.|.#
        /// H:D-F-G
        /// </code>
        /// </summary>
        static FloorRecipe FloorTwo() => new FloorRecipe
        {
            Floor = 2,
            Name = "버팀목 길",
            Cells = 9,
            StartReachable = 8,
            LandingX = 0,
            LandingY = 1,
            Landmark = null,
            Groups = new[]
            {
                new GroupMix { Archers = 2, Rats = 1, State = GroupState.Sleep, Label = "궁수와 굴쥐", FirstClearing = true },
                new GroupMix { Boars = 1, Archers = 1, Rats = 2, State = GroupState.Eat, Label = "멧돼지와 궁수" },
                new GroupMix { Boars = 1, Rats = 2, State = GroupState.Sleep, Label = "멧돼지와 굴쥐" },
            },
            Nests = 1,
            WoodChests = 2,
            IronChests = 2,
            Ores = 1,
            Lamps = 3,
            Stakes = 2,
            Events = 1,
            MinLoops = 1,
            // 곁방 문 1 + 지름길 1(2층부터, 2-5 차례 7). 9칸이라 주 길(4칸)은 늘 가장 짧은 길이어서 지름길은 고리를 2칸 넘게 줄인다.
            AbilityDoorsMin = 1,
            AbilityDoorsMax = 2,
            // 씨앗 1~40 가운데 30(통합 검토 뒤 생성기를 고쳐 다시 고름, 글자 모양은 예전 고른 지도와 같다): 서→동 주 길(잠든 궁수 첫 공터 →
            // 먹는 멧돼지·궁수)에 아래 고리(도시락통·둥지)가 붙고, 계단 앞에서 막다른 공터(잠든 멧돼지·나무 궤짝)로 금 간 벽 지름길,
            // 주 길에서 보이는 금 간 벽 곁방(쇠 궤짝), 구석 숨은 방(단서 등잔)이 다 들어 있다. 막다른 곳은 모두 보상이 있다.
            ChosenSeed = 30UL,
            OnceItems = Array.Empty<OnceItem>(),
        };

        /// <summary>디버그·시험 패널용 한 줄 요약.</summary>
        public override string ToString() =>
            $"{Floor}층 {Name}: 칸 {Cells}({StartReachable}), 무리 {Groups.Length}+둥지 {Nests}, 나무 {WoodChests}, 쇠 {IronChests}, 광맥 {Ores}, 등잔 {Lamps}, 말뚝 {Stakes}, 사건 {Events}";

        /// <summary>모든 층의 한 번 받는 물건.</summary>
        public static IEnumerable<OnceItem> AllOnceItems()
        {
            foreach (var r in Table)
                foreach (var o in r.OnceItems)
                    yield return o;
        }

        /// <summary>이 id의 한 번 받는 물건(없으면 null).</summary>
        public static OnceItem FindOnceItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var r in Table)
                foreach (var o in r.OnceItems)
                    if (o.Id == id) return o;
            return null;
        }

        /// <summary>능력 문(금 간 벽) 뒤에만 있는 칸 수 = 칸 − 처음 갈 수 있는 칸 − 랜드마크(자물쇠 뒤).</summary>
        public int GatedCells => Math.Max(0, Cells - StartReachable - (Landmark != null ? 1 : 0));
    }

    /// <summary>
    /// 이번 원정에 이 층에 놓을 개수(매판 새 탐험 1차 2-5 차례 9·10, 2-6). 층 예산 표(FloorRecipe)를 이번 입력에 맞춰 센다.
    /// 생성기(FloorGenerator)와 검사기(FloorRules.Check ⑨)가 같은 값을 쓴다.
    /// 층 첫 방문은 고른 지도라 예산 그대로다(밤 사건·감쇠를 받지 않음 — 고른 씨앗이 프로필마다 같은 지도가 되어 첫 탐험 시간표·경제 기준이 그대로 선다).
    /// 다시 연 층: 나무 궤짝은 최고 도달 층과 그 바로 위층은 그대로, 최고 − 2 이하 층은 1개(숨은 방에 놓는 나무 궤짝도 이 안에서 센다).
    /// 쇠 궤짝은 능력 문 뒤 1개 + 아직 안 받은 보장 상자. 광맥은 최고 − 2 이하 층에서 0. 최고 − 3 이하 층은 무리마다 강한 적 1마리 이하.
    /// 밤 사건은 칸 수·격자를 넘지 않는 안에서만: 무너짐 막다른 곳 +1(지도 모양), 새 쥐굴 둥지 +1·통로 +1(둥지를 통로에), 드러남 쇠 궤짝 +1.
    /// </summary>
    public sealed class FloorBudget
    {
        public int Cells;
        public int StartReachable;
        /// <summary>능력 문 뒤에만 있는 칸 수(FloorRecipe.GatedCells).</summary>
        public int Gated;
        public int Groups;
        public int Nests;
        public int WoodChests;
        public int IronChests;
        public int Ores;
        public int Lamps;
        public int Stakes;
        public int Events;
        /// <summary>무너짐: 막다른 곳을 하나 더 남긴다(보상을 줄 수 있는 만큼만).</summary>
        public int ExtraDeadEnds;
        /// <summary>새 쥐굴: 늘어난 둥지 하나를 통로 조각에 둔다('통로 +1').</summary>
        public bool BurrowCorridor;
        /// <summary>최고 − 3 이하 다시 연 층: 무리마다 강한 적(멧돼지·궁수) 1마리 이하.</summary>
        public bool WeakGroups;

        public static FloorBudget For(FloorRecipe recipe, GeneratorInput input)
        {
            var b = new FloorBudget();
            if (recipe == null) return b;
            if (input == null) input = new GeneratorInput { Floor = recipe.Floor, DeepestFloor = recipe.Floor };
            b.Cells = recipe.Cells;
            b.StartReachable = recipe.StartReachable;
            b.Gated = recipe.GatedCells;
            b.Groups = recipe.Groups.Length;
            b.Nests = recipe.Nests;
            b.WoodChests = recipe.WoodChests;
            b.IronChests = recipe.IronChests;
            b.Ores = recipe.Ores;
            b.Lamps = recipe.Lamps;
            b.Stakes = recipe.Stakes;
            b.Events = recipe.Events;
            if (input.FirstVisit) return b;

            int floor = recipe.Floor;
            int deepest = input.DeepestFloor;
            if (floor < deepest - 1) b.WoodChests = Math.Min(1, recipe.WoodChests);
            if (floor <= deepest - 2) b.Ores = 0;
            b.WeakGroups = floor <= deepest - 3;

            // 쇠 궤짝: 능력 문 뒤 1개 + 아직 안 받은 보장 상자(한 번 받는 쇠 궤짝).
            int pendingIron = 0;
            foreach (var o in recipe.OnceItems)
                if (o.Kind == FeatureKind.IronChest && o.Place != OncePlace.Landmark && !Done(input, o.Id)) pendingIron++;
            b.IronChests = Math.Min(1, b.Gated) + pendingIron;

            switch (input.Night)
            {
                case NightEvent.Collapse:
                    b.ExtraDeadEnds = 1;
                    break;
                case NightEvent.RatBurrow:
                    b.Nests += 1;
                    b.BurrowCorridor = true;
                    break;
                case NightEvent.Upheaval:
                    b.IronChests += 1;
                    break;
            }
            return b;
        }

        /// <summary>이 id를 이미 받았는가(입력의 OnceDone).</summary>
        public static bool Done(GeneratorInput input, string id) =>
            input != null && input.OnceDone != null && id != null && input.OnceDone.Contains(id);

        public override string ToString() =>
            $"칸 {Cells}({StartReachable}), 무리 {Groups}+둥지 {Nests}, 나무 {WoodChests}, 쇠 {IronChests}, 광맥 {Ores}, 등잔 {Lamps}, 말뚝 {Stakes}, 사건 {Events}";
    }
}
