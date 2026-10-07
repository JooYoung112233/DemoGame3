using System;

namespace Demo6.Core.Combat
{
    /// <summary>치명 연출 세기(장비 문서 3-4). 한 방 무게 = 그 타 배율 × 치명 피해. 차례가 곧 무게 차례다(Max가 씀, 바꾸지 않음).</summary>
    public enum CritTier
    {
        /// <summary>치명 아님.</summary>
        None,
        /// <summary>100% 미만: 노란 숫자 1.2배, 짧은 치명 소리, 불꽃 3개. 넉백·흔들림·히트스톱은 보통 타와 같음.</summary>
        Light,
        /// <summary>100~200%: 노란 숫자 1.4배, 치명 소리, 파편 10개, 강한 베인 자국, 피 +30%, 넉백 최소 0.9, 흔들림 0.04/0.06.</summary>
        Normal,
        /// <summary>200% 이상: 보통 전부 + 히트스톱 최소 0.06 + 흔들림 0.06/0.08(예전 치명 연출).</summary>
        Heavy,
    }

    /// <summary>
    /// 치명 연출 단계 고르기(장비 문서 3-4). 한 동작에서 여러 대상이 치명이면 가장 무거운 단계 하나(Max).
    /// 무거움은 0.5초에 한 번(마무리 단계는 예외). 0.5초 안에 또 나면 보통으로 낸다.
    /// 예(치명 피해 = 무기 시작값): 한손검과 방패 베기 147% · 되베기 144% 보통 · 마무리 베기 208% 무거움, 대검 걷어 베기 230% · 내려 쪼개기 430% 무거움,
    /// 쌍검 우측·좌측 베기 60% · 엇베기 68% · 가위 가르기 51% 가벼움, 회오리 1타 144% 보통, 검풍 560% 무거움. 치명 피해 240%면 한손검과 방패 베기도 221% 무거움.
    /// 계약(꾸러미 ①이 채우고 시험, ③이 PlayerController에서 씀).
    /// </summary>
    public static class CritTiers
    {
        /// <summary>이 무게(‰) 미만은 가벼움.</summary>
        public const int LightBelowPermille = 1000;
        /// <summary>이 무게(‰) 이상은 무거움.</summary>
        public const int HeavyFromPermille = 2000;
        /// <summary>무거움 사이 최소 간격(초, 게임 시간).</summary>
        public const double HeavyInterval = 0.5;
        /// <summary>Gate의 lastHeavy 처음 값(아직 무거움이 없음).</summary>
        public const double NoHeavyYet = double.NegativeInfinity;

        /// <summary>한 방 무게(‰) = 타 배율(%) × 치명 피해 × 10, 가까운 정수. 예: 한손검과 방패 마무리 베기 130% × 1.6 = 2,080‰.</summary>
        public static int WeightPermille(double hitPercent, double critDamage) => (int)Math.Round(hitPercent * critDamage * 10.0);

        /// <summary>무게로 단계(치명일 때). 한손검과 방패 베기 92 × 1.6 = 1,472 보통, 쌍검 엇베기 52 × 1.3 = 676 가벼움, 대검 걷어 베기 115 × 2.0 = 2,300 무거움.</summary>
        public static CritTier Of(double hitPercent, double critDamage) => OfWeight(WeightPermille(hitPercent, critDamage));

        /// <summary>무게(‰)로 단계: 1,000 미만 가벼움, 1,000~1,999 보통, 2,000 이상 무거움.</summary>
        public static CritTier OfWeight(int weightPermille)
        {
            if (weightPermille < LightBelowPermille) return CritTier.Light;
            return weightPermille < HeavyFromPermille ? CritTier.Normal : CritTier.Heavy;
        }

        /// <summary>둘 중 무거운 단계(한 동작 안 여러 대상).</summary>
        public static CritTier Max(CritTier a, CritTier b) => (int)a >= (int)b ? a : b;

        /// <summary>
        /// 무거움 0.5초 제한. 무거움이 lastHeavy에서 0.5초 안이면 보통으로 낮춘다(마무리 단계는 예외로 늘 무거움).
        /// 무거움을 내면(마무리 포함) lastHeavy = now. 낮춘 무거움과 다른 단계는 lastHeavy를 바꾸지 않는다.
        /// lastHeavy 처음 값은 음의 무한대(NoHeavyYet). now는 게임 시간(초)이다.
        /// </summary>
        public static CritTier Gate(CritTier tier, double now, ref double lastHeavy, bool finisher)
        {
            if (tier != CritTier.Heavy) return tier;
            if (!finisher && now - lastHeavy < HeavyInterval) return CritTier.Normal;
            lastHeavy = now;
            return CritTier.Heavy;
        }

        /// <summary>
        /// 한 동작이 여러 묶음으로 나눠 맞힐 때(검풍이 날아가며 새 적을 훑음) 이 묶음이 낼 단계. 무거움은 한 동작에 한 번만 낸다:
        /// 처음 무거움이면 heavyUsed를 켜고 무거움, 그 뒤 묶음의 무거움은 보통으로 낮춘다(3-4 '한 동작에 가장 무거운 단계 하나',
        /// '무거움은 0.5초에 한 번'. 검풍은 0.5초 안에 끝나므로 두 번째 무거움은 늘 그 창 안이다). 다른 단계는 그대로.
        /// </summary>
        public static CritTier OncePerAction(CritTier tier, ref bool heavyUsed)
        {
            if (tier != CritTier.Heavy) return tier;
            if (heavyUsed) return CritTier.Normal;
            heavyUsed = true;
            return CritTier.Heavy;
        }
    }
}
