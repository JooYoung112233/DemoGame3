using System;
using Demo6.Core.Combat;

namespace Demo6.Core.Dungeon
{
    /// <summary>
    /// 바닥 함정 규칙(기획/1-2층-탐험-맛-1차.md 4-5 낙석, 4-6 가시 덫). 수와 자리는 생성 덧칠(FloorSpice)이, 세상 물체는 물결 3 Game이 쓴다.
    /// 낙석: 바닥 잔돌(반경 2.2)이 단서. 서서 걸어 몸 가운데가 반경 1.1 안에 들면 걸리고, 웅크려 지나가면 걸리지 않는다. 적은 걸지 않는다.
    /// 걸리면 낙석 자리 가운데에 반경 1.6 빨간 원이 1.0초 차오르고(천장 삐걱 소리) 떨어진다. '위에서 떨어짐'(HitKind.FromAbove)이라 방패로 못 막고 구르기로 피한다.
    /// 피해는 그 층 돌충이 공격력의 90%, 밀림 0.6. 원 안의 적도 맞는다(굴쥐 즉사, 궁수 40%, 돌충이 25% + 무너짐, 정예 12% + 버팀 50%, 둥지·오우거 없음).
    /// 떨어지면 반경 12 큰 소리. 한 원정에 한 번. 큰 지도·조사율·경험치에 넣지 않는다.
    /// 가시 덫: 빛과 시야 안에서만 보인다. 밟으면 그 층 궁수 공격력의 100%, 밀림 0.4(HitKind.Trap), 반경 8 큰 소리, 덫은 사라진다.
    /// 웅크린 채 1.2초 눌러 걷어 내면 강화석 1(층 방문에 한 번, CornerLoot.SpikeStones). 적은 밟지 않는다.
    /// 한 방 몫: 낙석 1층 13.4%·2층 15.1%(예고 규칙상 0.6초와 소리가 필요해 1.0초로 넉넉히), 가시 1층 5.6%·2층 6.3%.
    /// </summary>
    public static class TrapRules
    {
        // ── 4-5 낙석 ──
        /// <summary>걸림 반경(몸 가운데가 이 안에 들면, 서 있을 때만).</summary>
        public const float RockTriggerRadius = 1.1f;
        /// <summary>떨어지는 원 반경(빨간 원 예고).</summary>
        public const float RockRadius = 1.6f;
        /// <summary>예고(초). 예고 규칙 최소(0.6초)보다 넉넉히.</summary>
        public const float RockWindup = 1.0f;
        /// <summary>피해 = 그 층 돌충이 공격력의 90%.</summary>
        public const int RockPercentOfBoar = 90;
        /// <summary>밀림.</summary>
        public const float RockKnockback = 0.6f;
        /// <summary>떨어질 때 큰 소리 반경(가까운 잠든 무리 하나가 깨어 문 안에서 기다린다).</summary>
        public const float RockNoiseRadius = 12f;
        /// <summary>단서 잔돌이 흩어진 반경.</summary>
        public const float RockClueRadius = 2.2f;
        /// <summary>단서 잔돌 수(7~10개).</summary>
        public const int RockClueStonesMin = 7;
        public const int RockClueStonesMax = 10;
        /// <summary>천장 흙 부스러기 간격(4~6초).</summary>
        public const float RockDustMinSeconds = 4f;
        public const float RockDustMaxSeconds = 6f;
        /// <summary>자리: 무리·둥지 가운데에서 이 거리 이상.</summary>
        public const float RockMinGroupGap = 4f;
        /// <summary>자리: 다른 물건에서 이 거리 이상.</summary>
        public const float RockMinThingGap = 2.5f;
        /// <summary>적이 맞는 몫(최대 체력 대비): 굴쥐 즉사, 궁수 40%, 돌충이 25% + 바로 무너짐, 정예 12% + 버팀 50%.</summary>
        public const double RockRatFraction = 1.0;
        public const double RockArcherFraction = 0.4;
        public const double RockBoarFraction = 0.25;
        public const double RockEliteFraction = 0.12;
        /// <summary>정예가 맞으면 버팀을 이 몫만큼 깎는다(최대 버팀 대비).</summary>
        public const double RockElitePoiseFraction = 0.5;
        /// <summary>1층 다시 연 층 낙석 1개 확률(‰, 0~1개).</summary>
        public const int RockFloorOnePermille = 500;
        /// <summary>무너짐 밤(NightEvent.Collapse) 다시 연 층 낙석 +1(4-9).</summary>
        public const int CollapseExtraRockfalls = 1;

        // ── 4-6 가시 덫 ──
        /// <summary>밟음 반경.</summary>
        public const float SpikeRadius = 0.6f;
        /// <summary>피해 = 그 층 궁수 공격력의 100%.</summary>
        public const int SpikePercentOfArcher = 100;
        /// <summary>밀림.</summary>
        public const float SpikeKnockback = 0.4f;
        /// <summary>'찰칵' 쇳소리 큰 소리 반경.</summary>
        public const float SpikeNoiseRadius = 8f;
        /// <summary>웅크린 채 눌러 걷어 내는 시간(초).</summary>
        public const float SpikeDisarmSeconds = 1.2f;
        /// <summary>자리: 무리·둥지 가운데에서 이 거리 이상.</summary>
        public const float SpikeMinGroupGap = 3f;
        /// <summary>자리: 다른 물건에서 이 거리 이상.</summary>
        public const float SpikeMinThingGap = 1.5f;
        /// <summary>걷어 내기 강화석은 한 장면(층 방문)에 이 번까지.</summary>
        public const int SpikeStonesPerScene = 1;
        /// <summary>2층 다시 연 층 가시 덫 2개 확률(‰, 1~2개).</summary>
        public const int SpikeFloorTwoPermille = 500;

        /// <summary>낙석 피해의 바탕 공격력 = 그 층 돌충이 공격(3차 값, 1층 400·2층 496).</summary>
        public static int RockAttack(int floor) =>
            FloorScaling.MonsterAttack(MonsterRule.Of(MonsterKind.Boar, CombatRuleset.V3), floor);

        /// <summary>가시 덫 피해의 바탕 공격력 = 그 층 궁수 공격(3차 값, 1층 150·2층 186).</summary>
        public static int SpikeAttack(int floor) =>
            FloorScaling.MonsterAttack(MonsterRule.Of(MonsterKind.Archer, CombatRuleset.V3), floor);

        /// <summary>낙석 한 방이 그 층 기준 플레이어 최대 체력에서 차지하는 몫(TelegraphRule, 1층 0.134·2층 0.151).</summary>
        public static double RockHitFraction(int floor) =>
            TelegraphRule.HitFraction(RockAttack(floor), RockPercentOfBoar, floor);

        /// <summary>가시 덫 한 방 몫(1층 0.056·2층 0.063).</summary>
        public static double SpikeHitFraction(int floor) =>
            TelegraphRule.HitFraction(SpikeAttack(floor), SpikePercentOfArcher, floor);

        /// <summary>낙석이 걸리는가: 서서 걸을 때만(웅크려 지나가면 걸리지 않음).</summary>
        public static bool RockTriggers(bool crouching) => !crouching;

        /// <summary>원 안의 적이 맞는 몫(최대 체력 대비). 둥지·오우거는 0, 정예는 종류와 상관없이 12%.</summary>
        public static double RockEnemyFraction(MonsterKind kind, bool elite)
        {
            if (kind == MonsterKind.Nest || kind == MonsterKind.Ogre) return 0;
            if (elite) return RockEliteFraction;
            switch (kind)
            {
                case MonsterKind.Rat: return RockRatFraction;
                case MonsterKind.Archer: return RockArcherFraction;
                case MonsterKind.Boar: return RockBoarFraction;
                default: return 0;
            }
        }

        /// <summary>맞으면 바로 무너지는가(정예 아닌 돌충이만).</summary>
        public static bool RockBreaks(MonsterKind kind, bool elite) => kind == MonsterKind.Boar && !elite;

        /// <summary>
        /// 이번 원정 이 층의 낙석 수. 층 첫 방문은 recipe.FirstVisitRockfalls(1층 0, 2층 가르치는 낙석 1) — 밤 사건을 받지 않는다.
        /// 다시 연 층: 1층 0~1(roll1000 &lt; 500이면 1), 2층 1. 무너짐 밤이면 +1. roll1000은 부르는 쪽이 굴린 0~999.
        /// </summary>
        public static int RockfallCount(int floor, bool firstVisit, NightEvent night, int roll1000, FloorRecipe recipe)
        {
            if (firstVisit) return recipe != null ? Math.Max(0, recipe.FirstVisitRockfalls) : 0;
            int count = floor <= 1 ? (roll1000 < RockFloorOnePermille ? 1 : 0) : 1;
            if (night == NightEvent.Collapse) count += CollapseExtraRockfalls;
            return count;
        }

        /// <summary>이번 원정 이 층의 가시 덫 수. 첫 방문 0, 1층 다시 연 층 1, 2층 다시 연 층 1~2(roll1000 &lt; 500이면 2).</summary>
        public static int SpikeCount(int floor, bool firstVisit, int roll1000)
        {
            if (firstVisit) return 0;
            if (floor <= 1) return 1;
            return roll1000 < SpikeFloorTwoPermille ? 2 : 1;
        }
    }
}
