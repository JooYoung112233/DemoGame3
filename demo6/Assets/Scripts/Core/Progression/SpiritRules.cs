using System;

namespace Demo6.Core.Progression
{
    /// <summary>
    /// 투지(기획/스킬-자원-트리-1차.md 1장, 사용자 결정 2026-10-07 '투지'): 싸우며 차고 스킬이 쓰는 자원. 0~100.
    /// 차는 곳: 일반 공격·무기 행동 피해(공격력 100%어치 피해마다 +6, 한 번에 +20까지), 받아치기(패링) +15, 처치 +4,
    /// 마무리 일격 칸을 배웠으면 콤보 마무리가 맞을 때 +8, 피의 회오리는 회오리가 맞힐 때마다 +6(한 번에 +24까지). 끓는 피는 얻는 양 × (1 + 0.1 × 랭크).
    /// 빠지는 곳: 싸움이 끝나고(가까이 깬 적 없음) 마지막 싸움 동작에서 3초가 지나면 초당 −8. 원정 시작·층 시작·다시 서기는 0.
    /// 쓰는 곳: 회오리 40, 검풍 50. 아껴 쓰는 맛은 투지가 맡고 재사용 대기는 연타만 막는다(회오리 2초, 검풍 3초).
    /// </summary>
    public static class SpiritRules
    {
        public const float Max = 100f;
        public const float WhirlCost = 40f;
        public const float WaveCost = 50f;
        /// <summary>공격력 100%어치 피해마다 얻는 투지.</summary>
        public const float PerHundredPercent = 6f;
        /// <summary>피해 한 번에서 얻는 투지 상한(기 모으기 놓아 베기·치명 몫이 한꺼번에 차지 않게).</summary>
        public const float HitCap = 20f;
        public const float ParryGain = 15f;
        public const float KillGain = 4f;
        public const float FinisherGain = 8f;
        public const float BloodWhirlPerHit = 6f;
        public const float BloodWhirlCap = 24f;
        public const float DecayPerSecond = 8f;
        public const float DecayDelay = 3f;
        /// <summary>끓는 피 랭크당 얻는 양 +10%.</summary>
        public const float BoilingPerRank = 0.1f;
        public const float WhirlCooldown = 2f;
        public const float WaveCooldown = 3f;

        /// <summary>일반 공격·무기 행동 피해 한 번에서 얻는 투지(끓는 피 전). 공격력이 없으면 0.</summary>
        public static float FromDamage(int amount, int attack)
        {
            if (amount <= 0 || attack <= 0) return 0f;
            return Math.Min(HitCap, PerHundredPercent * amount / (float)attack);
        }

        /// <summary>끓는 피 랭크를 곱한 얻는 양.</summary>
        public static float Scaled(float gain, int boilingRank) =>
            gain <= 0f ? 0f : gain * (1f + BoilingPerRank * SkillDef.ClampRank(boilingRank));

        /// <summary>더하고 0~Max로 자른다.</summary>
        public static float Add(float value, float gain) => Math.Max(0f, Math.Min(Max, value + gain));

        public static bool CanPay(float value, float cost) => value + 0.0001f >= cost;

        /// <summary>싸움이 끝난 뒤 빠지기: 싸우는 중이거나 마지막 싸움 동작에서 3초가 안 지났으면 그대로.</summary>
        public static float Decay(float value, bool inCombat, float sinceCombat, float dt)
        {
            if (inCombat || sinceCombat < DecayDelay || dt <= 0f) return value;
            return Math.Max(0f, value - DecayPerSecond * dt);
        }
    }
}
