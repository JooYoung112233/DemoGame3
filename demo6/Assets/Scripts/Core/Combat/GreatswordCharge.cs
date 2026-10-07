namespace Demo6.Core.Combat
{
    /// <summary>
    /// 대검 오른쪽 클릭: 기 모으기 → 놓아 베기(기획/세-무기-우클릭-소켓-1차.md 3-6~3-8). 단계 시각은 절대 시간이고 공격 속도와 무관하다(0-3의 7).
    /// 놓아 베기는 ComboStep 3개(판정은 PlayerController 공용 DealHit). 계약 단계 값(확정), 꾸러미 ① 규칙이 시험한다(GreatswordChargeTests).
    /// </summary>
    public static class GreatswordCharge
    {
        /// <summary>단계에 닿는 시각(누른 뒤 초).</summary>
        public const float L1 = 0.40f;
        public const float L2 = 0.90f;
        public const float L3 = 1.50f;
        /// <summary>이 시각에 3단계로 저절로 놓는다(벌 없음).</summary>
        public const float MaxHold = 2.5f;
        /// <summary>이 시각부터 날 떨림 ±3°(그림).</summary>
        public const float WarnAt = 2.2f;
        /// <summary>몸이 커서 쪽으로 도는 최대 속도(초당 도).</summary>
        public const float TurnDegPerSec = WeaponActRules.ChargeTurnDegPerSec;
        /// <summary>놓아 베기 자동 조준: 사거리 안이고 바라보는 쪽 ±이 각 안인 대상만(1.5 다가가기 없음).</summary>
        public const float AimCone = 30f;

        /// <summary>걸음 배율: 1단계 전 0.16(0.68), 1단계 0.13, 2단계 0.11, 3단계 0.09(0.38).</summary>
        public const float MoveBefore = 0.16f;
        public const float Move1 = 0.13f;
        public const float Move2 = 0.11f;
        public const float Move3 = 0.09f;

        /// <summary>빛 줄 세기(그림): 1단계 전 0 → 0.15, 1단계 0.35, 2단계 0.60, 3단계 0.90(±0.1, 초당 6번). 단계에 닿을 때 +0.4가 0.08초.</summary>
        public static readonly float[] GlowByLevel = { 0.15f, 0.35f, 0.60f, 0.90f };
        public const float GlowFlash = 0.4f;
        public const float GlowFlashTime = 0.08f;
        /// <summary>단계에 닿을 때 짧은 쇠 울림 음 높이(1·2·3단계).</summary>
        public static readonly float[] LevelPitch = { 0.8f, 0.95f, 1.1f };

        static readonly ComboStep[] ReleaseSteps =
        {
            new ComboStep
            {
                name = "모아 베기", shape = ComboShape.Arc, arcDeg = 150f, size = 2.5f, maxTargets = 7, duration = 0.62f, hitMoment = 0.275f,
                hitPercent = 150f, knockback = 1.0f, moveScale = 0.15f, advance = 0f, finisher = false, hitStop = 0.06f, shake = 0.05f, poiseDamage = 26f,
            },
            new ComboStep
            {
                name = "힘껏 베기", shape = ComboShape.Arc, arcDeg = 170f, size = 2.8f, maxTargets = 9, duration = 0.68f, hitMoment = 0.25f,
                hitPercent = 250f, knockback = 1.6f, moveScale = 0.10f, advance = 0.30f, finisher = true, hitStop = 0.08f, shake = 0.10f, poiseDamage = 50f,
            },
            new ComboStep
            {
                name = "온 힘 베기", shape = ComboShape.Arc, arcDeg = 200f, size = 3.1f, maxTargets = 12, duration = 0.80f, hitMoment = 0.24f,
                hitPercent = 380f, knockback = 2.2f, moveScale = 0.05f, advance = 0.45f, finisher = true, hitStop = 0.11f, shake = 0.16f, poiseDamage = 85f,
            },
        };

        /// <summary>모은 시간(초)의 단계: 0.40 전 0, 0.90 전 1, 1.50 전 2, 그 뒤 3.</summary>
        public static int LevelAt(float held)
        {
            if (held < L1) return 0;
            if (held < L2) return 1;
            if (held < L3) return 2;
            return 3;
        }

        /// <summary>그 단계에 닿는 시각(1~3). 범위 밖은 L1·L3로 자른다.</summary>
        public static float LevelTime(int level) => level <= 1 ? L1 : level == 2 ? L2 : L3;

        /// <summary>모으는 동안 걸음 배율(단계 0~3).</summary>
        public static float MoveScale(int level)
        {
            switch (level)
            {
                case 0: return MoveBefore;
                case 1: return Move1;
                case 2: return Move2;
                default: return level < 0 ? MoveBefore : Move3;
            }
        }

        /// <summary>놓았을 때 나갈 단계: 0.40초 전에 놓아도 1단계(0.40까지 마저 모은 뒤 나감, 취소 불가).</summary>
        public static int ReleaseLevel(float held)
        {
            int level = LevelAt(held);
            return level < 1 ? 1 : level;
        }

        /// <summary>놓은 시각 held에서 실제로 베기가 시작되는 시각(L1보다 이르면 L1).</summary>
        public static float ReleaseStartAt(float held) => held < L1 ? L1 : held;

        /// <summary>이 시각이면 저절로 놓는다(3단계).</summary>
        public static bool AutoRelease(float held) => held >= MaxHold;

        /// <summary>단계(1~3)의 놓아 베기. 범위 밖은 1·3으로 자른다. 같은 인스턴스를 돌려준다(고치지 말 것).</summary>
        public static ComboStep Release(int level)
        {
            int i = level <= 1 ? 0 : level >= 3 ? 2 : level - 1;
            return ReleaseSteps[i];
        }

        /// <summary>
        /// 단계별 단일 대상 초당 계수(모은 시간 포함) = 배율 ÷ (단계 시각 + 베기 길이): 1.47 / 1.58 / 1.65(상한 1.45 × 1.15 = 1.67 안).
        /// </summary>
        public static double CoefficientPerSecond(int level)
        {
            var step = Release(level);
            return step.PercentPerTarget / 100.0 / (LevelTime(level) + step.duration);
        }

        /// <summary>상한(콤보 띠 1.45 × 1.15).</summary>
        public const double CoefficientCap = 1.45 * 1.15;

        /// <summary>
        /// 놓아 베기는 마무리 일격 피해 보너스(PlayerController.FinisherDamageBonus)를 받지 않는다(0-3의 22). 2·3단계는 마무리라 무너진 적 처형·버팀은 그대로다(난사와 같은 처리).
        /// 받으면 버팀 룬(끊김 없음)과 겹쳐 3단계가 380 × 1.24 ÷ 2.3 = 초당 2.05로 상한 1.67을 위험 없이 넘는다.
        /// </summary>
        public static bool TakesFinisherDamageBonus => false;

        /// <summary>
        /// 마무리 일격 피해 보너스(finisherBonus, 4랭크 = 0.24)를 넣었을 때 단계별 단일 대상 초당 계수. 놓아 베기는 보너스를 받지 않아(TakesFinisherDamageBonus)
        /// 보너스와 상관없이 CoefficientPerSecond(level)와 같고 늘 상한 안이다.
        /// </summary>
        public static double CoefficientPerSecond(int level, double finisherBonus)
        {
            var step = Release(level);
            double scale = step.finisher && TakesFinisherDamageBonus ? 1.0 + finisherBonus : 1.0;
            return step.PercentPerTarget * scale / 100.0 / (LevelTime(level) + step.duration);
        }

        /// <summary>
        /// 맞으면 끊기는가(3-8): 모으는 동안, 그리고 놓은 뒤 판정 전(hitsDone == 0)이면 끊긴다. 판정 뒤 회수 중은 보통 피격(밀리기만).
        /// superArmor(버팀 룬 또는 시험 손잡이)면 끊기지 않는다.
        /// </summary>
        public static bool BreaksOnHit(WeaponActPhase phase, int hitsDone, bool superArmor)
        {
            if (superArmor) return false;
            if (phase == WeaponActPhase.Charging) return true;
            return phase == WeaponActPhase.Release && hitsDone <= 0;
        }
    }
}
