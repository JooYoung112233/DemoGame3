using Demo6.Core.Combat;
using Demo6.Core.Random;

namespace Demo6.Core.Loot
{
    /// <summary>
    /// 구석까지 갈 이유(기획/1-2층-탐험-맛-1차.md 4-7, 4-9 드러남). 생성 덧칠(FloorSpice)이 궤짝 Param에 표시를 달고, 궤짝(Chest)이 이 굴림을 부른다.
    /// ① 광부 품삯 궤짝(다시 연 층 막다른 곳의 일반 나무 궤짝): 나무 궤짝과 같은 굴림 차례(장비 10%, 강화석 25%), 골드 2배, 쥐 궤짝 없음, 회복 구슬 1 확정.
    /// ② 광부 유품 상자(다시 연 층 숨은 방 나무 궤짝): 장비 30%, 강화석 1 확정 + 나무 궤짝 몫(25%로 1 더), 골드는 나무 궤짝과 같음, 쥐 궤짝 없음, 유품 글 한 줄.
    /// ③ 측량 넘침 강화석 1, ④ 덫 쇠붙이 강화석 1(층 방문에 한 번). ⑤ 흙 묻은 쇠 궤짝(드러남 밤): 2.0초(웅크리면 3.0초) 파내고 보상은 쇠 궤짝 그대로.
    /// 경험치는 늘리지 않는다. 글은 Demo6.Core.Dungeon.ExploreText.
    /// </summary>
    public static class CornerLoot
    {
        /// <summary>궤짝 Param: 광부 품삯 궤짝(나무).</summary>
        public const string WageParam = "wage";
        /// <summary>궤짝 Param: 광부 유품 상자(나무).</summary>
        public const string KeepsakeParam = "keepsake";
        /// <summary>궤짝 Param: 흙 묻은 쇠 궤짝(쇠, 보상은 LootRules.RollChest 쇠 그대로).</summary>
        public const string BuriedParam = "buried";

        /// <summary>품삯 궤짝 골드 = 나무 궤짝 골드 × 2(1층 12, 2층 16).</summary>
        public const int WageGoldFactor = 2;
        /// <summary>품삯 궤짝 회복 구슬(확정). 묶음 5가 들어오면 '기름 병 또는 회복 구슬'.</summary>
        public const int WageHealOrbs = 1;
        /// <summary>유품 상자 장비 확률(‰, 나무 궤짝 100).</summary>
        public const int KeepsakeGearPermille = 300;
        /// <summary>유품 상자 확정 강화석(그 위에 나무 궤짝 몫 25%로 1 더).</summary>
        public const int KeepsakeStones = 1;
        /// <summary>측량 넘침 강화석(발밑에 떨굼).</summary>
        public const int SurveyOverflowStones = 1;
        /// <summary>가시 덫 걷어 내기 강화석(층 방문에 한 번, TrapRules.SpikeStonesPerScene).</summary>
        public const int SpikeStones = 1;
        /// <summary>흙 묻은 쇠 궤짝 파내는 시간(초): 서서 2.0, 웅크려 3.0.</summary>
        public const float BuriedDigSeconds = 2f;
        public const float BuriedDigCrouchSeconds = 3f;
        /// <summary>파낼 때 큰 소리 반경(웅크리면 지금 규칙대로 × 0.3).</summary>
        public const float BuriedNoiseRadius = 12f;

        /// <summary>
        /// 광부 품삯 궤짝. 같은 씨앗의 나무 궤짝(LootRules.RollChest(false, …))과 같은 난수 차례로 장비·강화석을 굴린 뒤
        /// 쥐 궤짝을 지우고 골드를 나무 궤짝 골드 × 2로 다시 나눈다. 회복 구슬(WageHealOrbs)은 부르는 쪽이 떨군다.
        /// </summary>
        public static LootBundle RollWage(int floor, IRandom rng, GearRollContext context)
        {
            int f = FloorScaling.Clamp(floor);
            var bundle = LootRules.RollChest(false, f, "", rng, context);
            bundle.RatChest = false;
            bundle.GoldPiles.Clear();
            LootRules.SplitGold(bundle, LootRules.WoodGold(f) * WageGoldFactor, f);
            return bundle;
        }

        /// <summary>
        /// 광부 유품 상자. 난수 차례: 장비(‰ &lt; 300) → 강화석 하나 더(‰ &lt; 250) → 장비면 LootRules.RollGear(일반 이상).
        /// 강화석 = 1 + 더, 골드 = 나무 궤짝 골드(무더기로 나눔), 쥐 궤짝 없음. 유품 글은 ExploreText.KeepsakeLine(궤짝 씨앗).
        /// </summary>
        public static LootBundle RollKeepsake(int floor, IRandom rng, GearRollContext context)
        {
            var bundle = new LootBundle();
            int f = FloorScaling.Clamp(floor);
            bool gear = rng.NextInt(0, 1000) < KeepsakeGearPermille;
            bool extra = rng.NextInt(0, 1000) < LootRules.WoodStonePermille;
            if (gear) bundle.Gear.Add(LootRules.RollGear(f, rng, Grade.Common, context));
            bundle.Stones = KeepsakeStones + (extra ? 1 : 0);
            LootRules.SplitGold(bundle, LootRules.WoodGold(f), f);
            return bundle;
        }

        /// <summary>이 Param이 구석 보상 표시(품삯·유품·흙 묻은 쇠 궤짝)인가.</summary>
        public static bool IsCornerParam(string param) =>
            param == WageParam || param == KeepsakeParam || param == BuriedParam;
    }
}
