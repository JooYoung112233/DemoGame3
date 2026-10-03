using System;

namespace Demo6.Core.Combat
{
    /// <summary>
    /// 기본공격 한 동작의 시간표(장비 문서 3-3). PlayerController.StartSwing이 이것 하나로 동작 길이·판정 순간·이월 상한을 정한다.
    /// 그림은 PoseDuration·PoseHitTime으로 따라온다. 시간은 초(float, 예전 PlayerController 계산과 같은 float 식).
    /// </summary>
    public readonly struct SwingPlan
    {
        /// <summary>동작 길이 d'(다음 단계가 이어질 수 있는 때까지).</summary>
        public readonly float Duration;
        /// <summary>첫 판정 h'.</summary>
        public readonly float FirstHit;
        /// <summary>연타 간격(데이터 그대로, 쌍검 0.1).</summary>
        public readonly float HitInterval;
        public readonly int Hits;

        // 판정 시각 = _hitBase × _hitMoment + k × 간격. 공격 속도 0이면 (동작 길이, hitMoment) 그대로라 예전 식
        // '_swingDuration * _step.hitMoment + index * _step.hitInterval'과 모양까지 같다(실행기가 float를 넓게 계산해도 같은 비트).
        // 공격 속도가 있으면 (h', 1) — × 1은 어떤 정밀도에서도 정확하다.
        readonly float _hitBase;
        readonly float _hitMoment;

        public SwingPlan(float duration, float firstHit, float hitInterval, int hits) : this(duration, firstHit, 1f, hitInterval, hits)
        {
        }

        /// <summary>첫 판정 = hitBase × hitMoment(공격 속도 0이면 동작 길이 × hitMoment).</summary>
        internal SwingPlan(float duration, float hitBase, float hitMoment, float hitInterval, int hits)
        {
            Duration = duration;
            _hitBase = hitBase;
            _hitMoment = hitMoment;
            FirstHit = hitBase * hitMoment;
            HitInterval = hitInterval;
            Hits = Math.Max(1, hits);
        }

        /// <summary>k번째 판정 시각 = h' + k × 간격(규칙 3).</summary>
        public float HitTime(int index) => _hitBase * _hitMoment + index * HitInterval;
        /// <summary>마지막 판정 시각(Hits가 0인 기본값이면 첫 판정).</summary>
        public float LastHit => HitTime(Math.Max(0, Hits - 1));
        /// <summary>마지막 판정 뒤 회수 시간.</summary>
        public float Recovery => Duration - LastHit;
        /// <summary>넘친 시간 이월 상한 = d' × 0.5(규칙 5).</summary>
        public float CarryCap => Duration * SwingTiming.CarryCapFraction;
    }

    /// <summary>
    /// 공격 속도를 콤보 단계 시간에 넣는 순수 함수(장비 문서 3-3). s = 1 + 공격 속도.
    /// ① d' = d ÷ s ② h' = (d × hitMoment) ÷ (1 + 공격 속도 ÷ 2)(준비 동작은 절반만, 나머지는 회수에서) ③ 연타 = h' + k × 간격(고정)
    /// ④ 회수 보장 d' ≥ 마지막 판정 + 0.08 ⑤ 이월 상한 d' × 0.5 ⑥ 조준 회전 35 × (1 + 공격 속도 ÷ 2)
    /// ⑦ 그대로 두는 절대 시간: 연타 간격, 다가가기·내딛기 0.1, 콤보 끊김 0.5, 입력 버퍼 0.15, 히트스톱.
    /// 공격 속도 0(또는 음수)이면 예전 식(d = max(0.1, duration), h = d × hitMoment + k × hitInterval, 이월 d × 0.5)과 float 비트까지 같다
    /// (M0a 손맛 보호, SwingTimingTests가 세 무기 모든 단계·모든 판정으로 고정).
    /// 상한(+300‰)은 StatCalc가 자른다. 이 함수는 시험 범위(0~400‰)와 시험장 손잡이를 위해 0~1000‰로만 자른다.
    /// 0~400‰에서는 세 무기 모든 단계가 규칙 4에 걸리지 않는다(가장 짧은 회수 = 쌍검 ③ 연타 0.132초).
    /// </summary>
    public static class SwingTiming
    {
        public const float MinDuration = 0.1f;
        /// <summary>규칙 4 회수 보장(초).</summary>
        public const float MinRecovery = 0.08f;
        /// <summary>규칙 5 이월 상한 비율.</summary>
        public const float CarryCapFraction = 0.5f;
        /// <summary>조준해서 휘두르는 동작의 기본 회전 빠르기(TopDownPlayerRig.AimTurnRate와 같은 값).</summary>
        public const float BaseAimTurnRate = 35f;
        public const int MaxInputPermille = 1000;

        static int Clamp(int attackSpeedPermille) => Math.Max(0, Math.Min(MaxInputPermille, attackSpeedPermille));

        /// <summary>s = 1 + 공격 속도.</summary>
        public static float SpeedFactor(int attackSpeedPermille) => 1f + Clamp(attackSpeedPermille) / 1000f;

        public static SwingPlan Plan(ComboStep step, int attackSpeedPermille)
        {
            if (step == null) return new SwingPlan(MinDuration, 0f, 0f, 1);
            float d = Math.Max(MinDuration, step.duration);
            int a = Clamp(attackSpeedPermille);
            if (a == 0) return new SwingPlan(d, d, step.hitMoment, step.hitInterval, step.hits);
            float s = 1f + a / 1000f;
            float duration = d / s;
            float firstHit = d * step.hitMoment / (1f + a / 2000f);
            float lastHit = firstHit + Math.Max(0, step.hits - 1) * step.hitInterval;
            if (duration < lastHit + MinRecovery) duration = lastHit + MinRecovery;
            return new SwingPlan(duration, firstHit, step.hitInterval, step.hits);
        }

        /// <summary>규칙 6: 기본공격 중 몸 회전 빠르기(그림만, 판정 방향은 StartSwing이 정함).</summary>
        public static float AimTurnRate(int attackSpeedPermille) => BaseAimTurnRate * (1f + Clamp(attackSpeedPermille) / 2000f);

        /// <summary>타당 버팀 배율 = 1 ÷ (1 + 공격 속도)(3-4: 초당 버팀 깎기를 무기마다 고정). 0이면 정확히 1.</summary>
        public static float PoiseScale(int attackSpeedPermille) => 1f / SpeedFactor(attackSpeedPermille);
    }
}
