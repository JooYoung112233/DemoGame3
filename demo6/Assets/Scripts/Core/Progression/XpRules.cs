using System;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;

namespace Demo6.Core.Progression
{
    /// <summary>
    /// 경험치 규칙(3차 초안 4-3). 모든 경험치는 '층 단위 U'의 배수로 정한다.
    /// U(층) = 10 × 1.15^(층 − 1)을 정수로 반올림한 값: 10, 12, 13, 15, 17, 20, 23, 27, 31, 35.
    /// 처치·탐험 값은 U 배수(units)로 두고, 실제 경험치 = round(units × U(층) × 감쇠).
    /// </summary>
    public static class XpRules
    {
        public const double UnitBase = 10.0;
        public const double UnitGrowth = 1.15;

        /// <summary>감쇠(4-3 '감쇠는 한 줄'): 캐릭터 레벨이 그 층 권장 레벨보다 이만큼 이상 높으면 처치 경험치를 줄인다.</summary>
        public const int DecayLevelGap = 3;
        /// <summary>감쇠 때 처치 경험치 몫(25%).</summary>
        public const double DecayFactor = 0.25;

        /// <summary>처치(4-3): 졸개 굴쥐 0.5U, 궁수 2U, 멧돼지 3U, 정예 10U, 둥지 정리 4U. 둥지가 부른 굴쥐(보상 없음)는 0.</summary>
        public const double RatUnits = 0.5;
        public const double ArcherUnits = 2.0;
        public const double BoarUnits = 3.0;
        public const double EliteUnits = 10.0;
        public const double NestClearUnits = 4.0;

        /// <summary>층 1~10 권장 레벨(4-3). 10층보다 깊으면 마지막 값을 쓴다.</summary>
        static readonly int[] RecommendedLevels = { 1, 3, 5, 7, 8, 10, 12, 13, 15, 16 };

        /// <summary>
        /// 경험치 단위 U(층) = 10 × 1.15^(층 − 1), 정수로 반올림(0.5는 올림).
        /// 2층 값 11.5는 부동소수로 11.4999…가 되므로 아주 작은 여유(1e-6)를 더해 기획 표(12)와 맞춘다.
        /// </summary>
        public static int Unit(int floor)
        {
            int f = Math.Max(1, floor);
            return (int)Math.Floor(UnitBase * Math.Pow(UnitGrowth, f - 1) + 0.5 + 1e-6);
        }

        /// <summary>
        /// 탐험 경험치(4-3, U 배수): 새 칸·벽 등잔·나무 궤짝 1, 쇠 궤짝 2, 말뚝·지름길·이야기 물건 3, 숨은 방 5, 사건 10, 층 완전 탐험 12.
        /// 곡괭이(능력)는 이야기 물건과 같이 3(계약서). 광맥·금고는 기록용이라 0.
        /// </summary>
        public static double DiscoveryUnits(DiscoveryKind kind)
        {
            switch (kind)
            {
                case DiscoveryKind.NewCell:
                case DiscoveryKind.WallLamp:
                case DiscoveryKind.WoodChest:
                    return 1.0;
                case DiscoveryKind.IronChest:
                    return 2.0;
                case DiscoveryKind.Stake:
                case DiscoveryKind.Shortcut:
                case DiscoveryKind.Story:
                case DiscoveryKind.Ability:
                    return 3.0;
                case DiscoveryKind.HiddenRoom:
                    return 5.0;
                case DiscoveryKind.Event:
                    return 10.0;
                case DiscoveryKind.FloorComplete:
                    return 12.0;
                default:
                    return 0.0;
            }
        }

        /// <summary>
        /// 처치 경험치(4-3, U 배수). 보상 없음(둥지·무리 거느린 정예가 부른 굴쥐)이면 0, 정예면 종류와 관계없이 10.
        /// 둥지 자체를 쓰러뜨린 것은 0이다(둥지 정리 4U를 GroupCleared에서 따로 준다).
        /// </summary>
        public static double KillUnits(MonsterKind kind, bool elite, bool noReward)
        {
            if (noReward) return 0.0;
            if (elite) return EliteUnits;
            switch (kind)
            {
                case MonsterKind.Rat: return RatUnits;
                case MonsterKind.Archer: return ArcherUnits;
                case MonsterKind.Boar: return BoarUnits;
                default: return 0.0;
            }
        }

        /// <summary>그 층 권장 레벨(4-3: 1, 3, 5, 7, 8, 10, 12, 13, 15, 16).</summary>
        public static int RecommendedLevel(int floor)
        {
            int f = Math.Max(1, floor);
            return RecommendedLevels[Math.Min(f, RecommendedLevels.Length) - 1];
        }

        /// <summary>처치 경험치 감쇠 여부: 캐릭터 레벨 ≥ 권장 레벨 + 3.</summary>
        public static bool KillDecays(int level, int floor) => level >= RecommendedLevel(floor) + DecayLevelGap;

        /// <summary>처치 경험치에 곱할 값(감쇠면 0.25, 아니면 1).</summary>
        public static double KillFactor(int level, int floor) => KillDecays(level, floor) ? DecayFactor : 1.0;

        /// <summary>U 배수 → 실제 경험치. round(units × U(층) × factor), 0.5는 올림. 0 이하면 0.</summary>
        public static int Amount(double units, int floor, double factor = 1.0)
        {
            if (units <= 0.0 || factor <= 0.0) return 0;
            return (int)Math.Floor(units * Unit(floor) * factor + 0.5 + 1e-9);
        }

        /// <summary>발견 경험치(감쇠 없음: 발견·사건은 프로필에 한 번만 받는다).</summary>
        public static int ForDiscovery(DiscoveryKind kind, int floor) => Amount(DiscoveryUnits(kind), floor);

        /// <summary>처치 경험치(감쇠 포함).</summary>
        public static int ForKill(MonsterKind kind, bool elite, bool noReward, int floor, int level) =>
            Amount(KillUnits(kind, elite, noReward), floor, KillFactor(level, floor));

        /// <summary>둥지 정리 경험치(4U, 처치처럼 감쇠).</summary>
        public static int ForNestClear(int floor, int level) => Amount(NestClearUnits, floor, KillFactor(level, floor));
    }
}
