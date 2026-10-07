using System;

namespace Demo6.Core.Loot
{
    /// <summary>
    /// 분해 강화석(재화 쓸 곳 1차 4-1, 2차 9-2, 장비 문서 8-4). 순수 규칙. 시험: SalvageRulesTests.
    /// 분해 강화석 = 등급 기본값(일반 1, 고급 2, 희귀 6, 영웅 15, 전설 40) + 강화 환급.
    /// 강화 환급 = 2차 9-2 환급(무기 기준) × 부위 비용 배율(반올림, 최소 0, GearMath.ScaleCost).
    /// 실제 투자는 늘 환급보다 많아 '강화한 뒤 분해'로 이득을 볼 수 없다. 끼운 룬은 분해할 때 룬 주머니로 돌아간다(부르는 쪽 몫).
    /// </summary>
    public static class SalvageRules
    {
        /// <summary>등급 기본값: 일반 1, 고급 2, 희귀 6, 영웅 15, 전설 40.</summary>
        static readonly int[] BaseByGrade = { 1, 2, 6, 15, 40 };
        /// <summary>2차 9-2 강화 환급(무기 기준): +0 0, +1~+15 = 0,1,1,2,3,4,6,10,16,25,37,56,82,121,178.</summary>
        static readonly int[] Refunds = { 0, 0, 1, 1, 2, 3, 4, 6, 10, 16, 25, 37, 56, 82, 121, 178 };

        /// <summary>한 번 더 묻기가 풀리는 시간(실제 시간 초, 4-3). 다른 장비를 고르거나 창을 닫아도 풀린다.</summary>
        public const float ConfirmSeconds = 4f;

        /// <summary>등급 기본 분해 강화석.</summary>
        public static int BaseStones(Grade grade) => BaseByGrade[Math.Max(0, Math.Min(BaseByGrade.Length - 1, (int)grade))];

        /// <summary>2차 9-2 환급(무기 기준). +0 이하는 0, +15를 넘으면 +15 값.</summary>
        public static int RefundBase(int enhance) => enhance <= 0 ? 0 : Refunds[Math.Min(GearMath.MaxEnhance, enhance)];

        /// <summary>분해 강화석 = 기본 + GearMath.ScaleCost(부위, 환급, 0). 예: 일반 장검 +5 = 1 + 3 = 4, 희귀 갑옷 +7 = 6 + 3 = 9. 없으면 0.</summary>
        public static int Stones(GearItem item) =>
            item == null ? 0 : BaseStones(item.Grade) + GearMath.ScaleCost(item.Part, RefundBase(item.Enhance), 0);

        /// <summary>한 번 더 묻는가(4-3): 희귀 이상, 또는 강화 +1 이상. 되사기(분해 취소)가 아직 없어서다.</summary>
        public static bool NeedsConfirm(GearItem item) => item != null && (item.Grade >= Grade.Rare || item.Enhance >= 1);

        /// <summary>모루 창 '일반 모두 분해' 대상인가(4-5): 일반이면서 강화 +0.</summary>
        public static bool InBulkCommon(GearItem item) => item != null && item.Grade == Grade.Common && item.Enhance == 0;
    }
}
