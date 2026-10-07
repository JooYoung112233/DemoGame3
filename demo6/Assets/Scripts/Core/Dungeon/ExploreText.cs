using System.Collections.Generic;
using Demo6.Core.Town;

namespace Demo6.Core.Dungeon
{
    /// <summary>
    /// '1·2층 탐험 맛 1차' 화면 글 한 곳(기획/1-2층-탐험-맛-1차.md 9장 글 모음, 4-1~4-11). 글은 모두 초안이고 바꿀 때는 여기만 고친다.
    /// 엿듣기(DoorListenRules)·낙석·가시 덫·품삯·유품·흙 묻은 쇠 궤짝·측량 넘침·계단 밑 기척이 여기서 글을 가져간다.
    /// 사람에게 보이는 글의 멧돼지는 '돌충이'다(코드 이름 MonsterKind.Boar는 그대로). 주민 이름은 쓰지 않는다.
    /// 새 안내 글에 키 이름을 넣지 않는다('[F]'는 상호작용 안내가 붙인다, 7장 키 배치).
    /// 시험: CornerLootTests(AllTexts에 '멧돼지'·주민 이름 없음).
    /// </summary>
    public static class ExploreText
    {
        // ── 4-4 문틈 엿듣기 ──
        /// <summary>엿듣기 글 머리. 마디는 DoorListenRules.Line이 붙인다.</summary>
        public const string ListenHead = "문틈 너머 — ";
        /// <summary>건너편에 기척이 없을 때.</summary>
        public const string ListenQuiet = ListenHead + "조용하다";
        /// <summary>마디 사이.</summary>
        public const string ListenSeparator = ", ";
        /// <summary>처음 하는 사람 안내 초안(7장 묶음 6 안내 목록에 넣을 글).</summary>
        public const string ListenHint = "웅크린 채 문 앞에 가만히 있으면 건너편을 엿들을 수 있다";

        /// <summary>마디(위험한 차례): 정예.</summary>
        public const string PhraseElite = "거친 숨소리(큰 놈)";
        /// <summary>마디: 깨어 있는 적.</summary>
        public const string PhraseAwake = "숨죽인 기척";
        /// <summary>마디: 순찰.</summary>
        public const string PhrasePatrol = "오가는 발소리";
        /// <summary>마디: 가시 궁수.</summary>
        public const string PhraseArcher = "시위 삐걱임";
        /// <summary>마디: 돌충이.</summary>
        public const string PhraseBoar = "껍데기 긁는 소리";
        /// <summary>마디: 굴쥐 여럿(DoorListenRules.ManyRats 이상).</summary>
        public const string PhraseManyRats = "발톱 소리 여럿";
        /// <summary>마디: 굴쥐 몇 마리.</summary>
        public const string PhraseFewRats = "작은 발톱 소리";
        /// <summary>마디: 굴쥐 둥지.</summary>
        public const string PhraseNest = "흙 속 꿈틀거림";
        /// <summary>마디: 먹는 중.</summary>
        public const string PhraseEating = "씹는 소리";

        // ── 4-5 낙석 ──
        /// <summary>장면에 한 번 띄우는 알림.</summary>
        public const string RockfallFirst = "머리 위에서 돌이 쏟아진다";
        /// <summary>낙석에 맞은 적 위 글(그 적이 보일 때만).</summary>
        public const string RockfallEnemy = "낙석!";
        /// <summary>자리 표시 이름(큰 지도·조사율에 넣지 않는다).</summary>
        public const string RockfallLabel = "낙석 자리";

        // ── 4-6 가시 덫 ──
        /// <summary>자리 표시 이름.</summary>
        public const string SpikesLabel = "가시 덫";
        /// <summary>밟았을 때 알림.</summary>
        public const string SpikeStepped = "가시 덫 — 쇳소리가 울린다";
        /// <summary>걷어 내기 안내(키 이름은 상호작용 안내가 붙인다).</summary>
        public const string SpikePrompt = "가시 덫 걷어 내기";
        /// <summary>서 있을 때 막힘 글.</summary>
        public const string SpikeNeedCrouch = "웅크려야 걷어 낼 수 있다";
        /// <summary>걷어 냄(장면에 한 번 강화석 1).</summary>
        public const string SpikeDisarmedStone = "가시 덫을 걷어 냈다 — 쓸 만한 쇠붙이 · 강화석 +1";
        /// <summary>걷어 냄(두 번째부터).</summary>
        public const string SpikeDisarmed = "가시 덫을 걷어 냈다";
        /// <summary>밟았을 때 머리 위 소리 글(4-6 '찰칵', FloorSpikes).</summary>
        public const string SpikeClick = "찰칵";

        // ── 4-7 구석까지 갈 이유 ──
        /// <summary>막다른 곳 나무 궤짝 이름(CornerLoot.WageParam).</summary>
        public const string WageLabel = "광부 품삯 궤짝";
        /// <summary>숨은 방 나무 궤짝 이름(CornerLoot.KeepsakeParam).</summary>
        public const string KeepsakeLabel = "광부 유품 상자";
        /// <summary>궤짝 안내(지금 궤짝 안내는 이름 + ' 열기').</summary>
        public const string WagePrompt = WageLabel + " 열기";
        public const string KeepsakePrompt = KeepsakeLabel + " 열기";
        /// <summary>품삯 궤짝을 열 때.</summary>
        public const string WageOpened = "꼬깃한 품삯 봉투 — 끝내 받아 가지 못한 돈이다";
        /// <summary>유품 상자를 열 때 하나(KeepsakeLine이 씨앗으로 고른다).</summary>
        public static IReadOnlyList<string> KeepsakeLines { get; } = new[]
        {
            "이름이 긁혀 나간 수저 한 벌",
            "손때 묻은 나무 부적",
            "닳은 담배쌈지 — 아직 냄새가 남았다",
            "아이가 그린 듯한 서툰 그림 한 장",
            "끝이 뭉툭해진 몽당연필",
        };
        /// <summary>측량 3장을 다 채운 층에서 처음 갈 수 있는 칸을 다 밟았을 때.</summary>
        public const string SurveyOverflow = "측량 — 도면은 이미 다 그렸다. 버려진 측량 못을 챙겼다 · 강화석 +1";

        // ── 4-9 밤 사건: 드러남 ──
        /// <summary>흙 묻은 쇠 궤짝 이름(CornerLoot.BuriedParam).</summary>
        public const string BuriedLabel = "흙 묻은 쇠 궤짝";
        /// <summary>파내기 안내.</summary>
        public const string BuriedPrompt = BuriedLabel + " 파내기";
        /// <summary>파낸 뒤 알림.</summary>
        public const string BuriedDug = "흙더미가 무너지는 소리가 갱도에 울린다";

        // ── 4-11 2층 계단 밑 기척 ──
        /// <summary>그 장면에서 처음 들릴 때 한 번.</summary>
        public const string DenRumbleFirst = "계단 아래에서 돌 씹는 소리가 올라온다";

        // ── 4-1·4-2 무리 이름(9장) ──
        // 1층 후보(첫 공터 빼고).
        public const string PackRatSwarm = "굴쥐 떼";
        public const string PackSleepingBoar = "잠든 돌충이";
        public const string PackEatingBoar = "먹는 돌충이";
        public const string PackRatPatrol = "굴쥐 순찰";
        public const string PackTwoBoars = "돌충이 둘";
        public const string PackBoarPatrol = "돌충이 순찰";
        // 2층 후보(첫 공터 빼고). '돌충이 순찰'·'굴쥐 떼'는 1층과 같은 글.
        public const string PackEatingBoarArcher = "먹는 돌충이와 궁수";
        public const string PackSleepingBoarArcher = "잠든 돌충이와 궁수";
        public const string PackTwoArchers = "궁수 둘";
        public const string PackArcherPatrol = "궁수 순찰";
        /// <summary>정예 무리(4-1 계단 앞, 4-2 다시 연 층 정예 0~1).</summary>
        public const string PackElite = "단단한 정예 돌충이";
        // 2층 층 예산 무리(첫 공터 '궁수와 굴쥐', 둘째 '돌충이와 궁수', 셋째 PackElite).
        public const string PackArcherRat = "궁수와 굴쥐";
        public const string PackBoarArcher = "돌충이와 궁수";

        /// <summary>유품 글 하나를 씨앗으로 고른다(같은 궤짝 씨앗이면 같은 글).</summary>
        public static string KeepsakeLine(ulong seed) =>
            KeepsakeLines[(int)(ExpeditionSeeds.Mix(seed) % (ulong)KeepsakeLines.Count)];

        // ── 시험 ──

        /// <summary>
        /// 이 설계의 화면 글 전부(이 파일 상수, 엿듣기 보기 글, 무리 후보 표 이름·바꾼 이름, 층 예산 무리 이름, 거센 울림 밤 카드).
        /// 시험이 '멧돼지'·주민 이름·빈 글을 찾는다.
        /// </summary>
        public static IEnumerable<string> AllTexts
        {
            get
            {
                yield return ListenQuiet;
                yield return ListenHint;
                yield return PhraseElite;
                yield return PhraseAwake;
                yield return PhrasePatrol;
                yield return PhraseArcher;
                yield return PhraseBoar;
                yield return PhraseManyRats;
                yield return PhraseFewRats;
                yield return PhraseNest;
                yield return PhraseEating;
                yield return DoorListenRules.Line(new ListenTally { Elites = 1, Rats = 2 });
                yield return DoorListenRules.Line(new ListenTally { Boars = 1, Rats = 3, Patrolling = 4 });
                yield return RockfallFirst;
                yield return RockfallEnemy;
                yield return RockfallLabel;
                yield return SpikesLabel;
                yield return SpikeStepped;
                yield return SpikePrompt;
                yield return SpikeNeedCrouch;
                yield return SpikeDisarmedStone;
                yield return SpikeDisarmed;
                yield return SpikeClick;
                yield return WageLabel;
                yield return KeepsakeLabel;
                yield return WagePrompt;
                yield return KeepsakePrompt;
                yield return WageOpened;
                foreach (var line in KeepsakeLines) yield return line;
                yield return SurveyOverflow;
                yield return BuriedLabel;
                yield return BuriedPrompt;
                yield return BuriedDug;
                yield return DenRumbleFirst;
                yield return PackRatSwarm;
                yield return PackSleepingBoar;
                yield return PackEatingBoar;
                yield return PackRatPatrol;
                yield return PackTwoBoars;
                yield return PackBoarPatrol;
                yield return PackEatingBoarArcher;
                yield return PackSleepingBoarArcher;
                yield return PackTwoArchers;
                yield return PackArcherPatrol;
                yield return PackElite;
                yield return PackArcherRat;
                yield return PackBoarArcher;
                // 무리 후보 표(PackTable)의 실제 글: 이름과 순찰을 못 할 때 바꾸는 이름.
                for (int floor = 1; floor <= FloorRecipe.MaxTestFloor; floor++)
                {
                    foreach (var option in PackTable.Options(floor))
                    {
                        if (option == null) continue;
                        yield return option.Label;
                        if (!string.IsNullOrEmpty(option.FallbackLabel)) yield return option.FallbackLabel;
                    }
                    var elite = PackTable.EliteOption(floor);
                    if (elite != null)
                    {
                        yield return elite.Label;
                        if (!string.IsNullOrEmpty(elite.FallbackLabel)) yield return elite.FallbackLabel;
                    }
                    var recipe = FloorRecipe.For(floor);
                    if (recipe != null)
                        foreach (var g in recipe.Groups)
                            if (g != null) yield return g.Label;
                }
                // 4-9 거센 울림 밤 카드(글은 TownNight).
                yield return TownNight.NightLine;
                yield return TownNight.RumbleLine;
            }
        }
    }
}
