namespace Demo6.Core.Combat
{
    /// <summary>
    /// 쌍검 오른쪽 클릭: 난사(기획/세-무기-우클릭-소켓-1차.md 4-6). 한 번 누르면 1.10초 끝까지 간다(공격 속도 무관). 동작은 빠른 엇베기 반복이고
    /// 타마다 베인 자국·번쩍임이 여러 방향으로 나 여러 번 때리는 것처럼 보인다(그림만, EffectDirDeg).
    /// 판정 8번: 작은 타 7번(0.18~0.72, 간격 0.09) + 마지막 X 0.84. 피해 종류는 기본공격(DamageSource.Basic).
    /// 계약 단계 값(확정), 꾸러미 ① 규칙이 시험한다(TwinFlurryRuleTests).
    /// </summary>
    public static class TwinFlurry
    {
        public const float Duration = 1.10f;
        /// <summary>판정 시각(초, 오름차순). 마지막이 X 마무리 타.</summary>
        public static readonly float[] HitTimes = { 0.18f, 0.27f, 0.36f, 0.45f, 0.54f, 0.63f, 0.72f, 0.84f };
        public static int HitCount => HitTimes.Length;

        public const float MoveScale = WeaponActRules.FlurryMoveScale;
        public const float TurnRateDeg = WeaponActRules.FlurryTurnDegPerSec;
        /// <summary>시작할 때 자동 조준 사거리.</summary>
        public const float AimRange = 1.9f;
        /// <summary>대상이 사거리 밖이면 첫 ApproachTime초 동안 최대 이만큼 다가간다(몸 앞까지).</summary>
        public const float MaxApproach = 1.0f;
        public const float ApproachTime = 0.1f;
        /// <summary>재사용: 시작할 때 6초, 끊기면 3초로 다시 센다. 재사용 감소 옵션은 받지 않는다(0-3의 8).</summary>
        public const float Cooldown = 6.0f;
        public const float InterruptedCooldown = 3.0f;
        /// <summary>작은 타는 히트스톱 0. 처치가 나면 그 동작에서 한 번만 이만큼.</summary>
        public const float KillHitStop = 0.06f;

        /// <summary>
        /// 난사 재사용(초): 시작할 때 6초, 끊기면 3초로 다시 센다. 재사용 감소 옵션(‰)은 받지 않는다(0-3의 8: 주면 6초 단위 상한 +10%를 넘음).
        /// cooldownReductionPermille은 받아도 쓰지 않는다는 규칙을 이름으로 남기려고 둔 인자다.
        /// </summary>
        public static float CooldownAfter(bool interrupted, int cooldownReductionPermille = 0) => interrupted ? InterruptedCooldown : Cooldown;

        /// <summary>단일 대상 초당 계수(공격 속도 무관) = 합계 배율 ÷ 길이 = 2.66 ÷ 1.10 = 2.42.</summary>
        public static double CoefficientPerSecond => TotalPercent / 100.0 / Duration;

        /// <summary>
        /// 6초 단위(재사용 한 번) 단일 대상 피해 증가 몫: (난사 1번 + 남은 시간 콤보) ÷ (6초 콤보) − 1.
        /// comboCoefficient = 그 무기 콤보 단일 대상 초당 계수(쌍검 1.588이면 약 0.096, 상한 0.10).
        /// </summary>
        public static double SixSecondGain(double comboCoefficient)
        {
            if (comboCoefficient <= 0) return 0;
            double withFlurry = TotalPercent / 100.0 + comboCoefficient * (Cooldown - Duration);
            return withFlurry / (comboCoefficient * Cooldown) - 1.0;
        }

        /// <summary>6초 단위 증가 상한(+10%).</summary>
        public const double SixSecondGainCap = 0.10;

        /// <summary>
        /// 작은 타 쓸기 중심 각(°, 그림용): 작은 타 1~7번째. 오른손 1·3·5·7번째, 왼손 2·4·6번째.
        /// 빠른 엇베기(2026-10-05 사용자 원문 "모션은 엇베기랑비슷한데 이펙트로 여러번때리는것처럼"): 타 시각 0.035초 전 바깥(c ∓ 45) → 타 순간 c →
        /// 0.035초 뒤 반대로 22° 넘김(c ± 22), 넘긴 칼은 다른 손의 다음 판정까지 버텨 X를 만든다. 판정(시각·배율·부채꼴)은 그대로.
        /// </summary>
        public static readonly float[] SweepCenterDeg = { 5f, -5f, -15f, 15f, 15f, -15f, -5f };
        public const float SweepHalfTime = 0.035f;
        /// <summary>작은 타 펼침 각(타 0.035초 전 바깥으로 벌린 각).</summary>
        public const float SweepOpenDeg = 45f;
        /// <summary>작은 타 넘김 각(타 0.035초 뒤 반대로 넘긴 각, X를 만듦).</summary>
        public const float SweepPastDeg = 22f;

        /// <summary>
        /// 난사 이펙트 방향 표(°, 그림만): 작은 타 1~7번째의 베인 자국·칼 그림 방향. 동작(SweepCenterDeg)과 따로 두어 타마다 여러 방향으로 난다.
        /// 판정 방향·범위와는 상관없다(동작·판정 분리).
        /// </summary>
        public static readonly float[] EffectDirDeg = { -35f, 30f, -10f, 45f, -50f, 20f, -20f };

        /// <summary>X가 생기는 작은 타(2·4·6번째, 왼손 타)와 마지막 타에 번쩍임을 낸다.</summary>
        public static bool FlashOn(int index) => IsFinal(index) || (index & 1) == 1;

        /// <summary>작은 타 하나(앞 부채꼴 100°, 1.9, 4명, 28%, 버팀 2, 넉백 0, 히트스톱 0, 준비 동작을 끊지 않음).</summary>
        public static readonly ComboStep Small = new ComboStep
        {
            name = "난사", shape = ComboShape.Arc, arcDeg = 100f, size = 1.9f, maxTargets = 4, duration = Duration, hitMoment = 0.18f / Duration,
            hitPercent = 28f, knockback = 0f, moveScale = MoveScale, hitStop = 0f, shake = 0f, poiseDamage = 2f,
        };

        /// <summary>마지막 X(부채꼴 120°, 2.0, 5명, 70%, 버팀 10, 넉백 0.8, 히트스톱 0.06, 흔들림 0.05, 끊기 = 창 끊어 찌르기와 같은 처리, 무너짐 처형 없음).</summary>
        public static readonly ComboStep Final = new ComboStep
        {
            name = "난사 마무리", shape = ComboShape.Arc, arcDeg = 120f, size = 2.0f, maxTargets = 5, duration = Duration, hitMoment = 0.84f / Duration,
            hitPercent = 70f, knockback = 0.8f, moveScale = MoveScale, hitStop = 0.06f, shake = 0.05f, poiseDamage = 10f, staggers = true,
        };

        /// <summary>시각 t(행동 시작부터)까지 나갔어야 할 판정 수(0~8).</summary>
        public static int HitsDue(float t)
        {
            int n = 0;
            while (n < HitTimes.Length && t >= HitTimes[n]) n++;
            return n;
        }

        /// <summary>i번째(0부터) 판정이 마지막 X인가.</summary>
        public static bool IsFinal(int index) => index == HitTimes.Length - 1;

        /// <summary>i번째 판정 단계(작은 타 또는 마지막).</summary>
        public static ComboStep StepOf(int index) => IsFinal(index) ? Final : Small;

        /// <summary>i번째 작은 타가 오른손인가(0·2·4·6번째 = 오른손). 마지막 X는 두 손.</summary>
        public static bool RightHand(int index) => (index & 1) == 0;

        public static float PercentOf(int index) => StepOf(index).hitPercent;
        public static float PoiseOf(int index) => StepOf(index).poiseDamage;

        /// <summary>적 하나당 합계 배율(%) = 28 × 7 + 70 = 266.</summary>
        public static float TotalPercent
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < HitTimes.Length; i++) sum += PercentOf(i);
                return sum;
            }
        }

        /// <summary>적 하나당 합계 버팀 = 2 × 7 + 10 = 24.</summary>
        public static float TotalPoise
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < HitTimes.Length; i++) sum += PoiseOf(i);
                return sum;
            }
        }
    }
}
