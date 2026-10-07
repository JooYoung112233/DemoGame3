namespace Demo6.Core.Combat
{
    /// <summary>기습 처형 결과(4-2 [3] '기습 처형', 결정 ③).</summary>
    public enum AmbushOutcome
    {
        /// <summary>조건이 맞지 않음(깨어 있음·들킴·등 뒤가 아님·웅크리지 않음·근접 기본공격이 아님·둥지·허수아비).</summary>
        None,
        /// <summary>굴쥐·궁수: 바로 죽음.</summary>
        Kill,
        /// <summary>멧돼지: 바로 무너짐(피해 2배는 넣지 않음).</summary>
        Break,
        /// <summary>정예·보스: 지금 기습 규칙(버팀 × 3, 상한 70%) 그대로.</summary>
        PoiseOnly,
    }

    /// <summary>
    /// 처형(기획/전투-보스-무기-다듬기-1차.md 4-2 [3], 묶음 5).
    /// 무너짐 처형: 무너진 적에게 마무리 타나 검풍이 맞고, 그 타로 죽거나 남은 체력이 문턱 이하일 때. 둥지·허수아비·보스는 처형하지 않는다.
    /// 기습 처형(결정 ③): 웅크린 채, 들키지 않은(잠든·먹는 중, 알아채기 전) 적의 등 뒤 ±60°(BackstabRule.IsBehind)에서 친 근접 기본공격 첫 타.
    /// 연출(ExecutionFx): 0.06초에 0.6유닛 당겨 붙음, 대검 내려찍기 자세를 칼 1.25배로, 히트스톱 0.10, 흔들림 0.12/0.14,
    /// 무거운 적도 조각 4~6개·피 웅덩이 1.4배, 느린 화면 0.3초 × 0.35는 마주침의 마지막 적·정예만. '처형' 글자는 띄우지 않는다.
    /// 처형하면 반경 6 안 졸개가 2.5초 겁먹는다(FearRule, CombatEvents.EnemyExecuted). 체력 3% 회복은 토글로 두고 기본 끔.
    /// 히트스톱: 처형 0.10이 무너진 적 마무리 0.1을 대신하고 무거운 적 처치 0.12는 생략한다(무너짐 0.08 + 처형 0.10 = 0.18, 예산 0.2 안).
    /// 쓰는 곳: PlayerController.SwingHit·HitWithWave(꾸러미 ⑤), ExecutionFx. ExecutionRuleTests가 지킨다.
    /// </summary>
    public static class ExecutionRule
    {
        /// <summary>Tuning.ExecutionOn / AmbushExecutionOn / ExecutionHealOn 기본값.</summary>
        public const bool DefaultOn = true;
        public const bool AmbushDefaultOn = true;
        public const bool HealDefaultOn = false;

        /// <summary>문턱(최대 체력 대비 남은 체력): 보통 무게(궁수) 30%, 무거움(멧돼지) 20%, 정예 12%, 이름난 정예 10%.</summary>
        public const float ThresholdMedium = 0.30f;
        public const float ThresholdHeavy = 0.20f;
        public const float ThresholdElite = 0.12f;
        public const float ThresholdNamed = 0.10f;
        /// <summary>문턱 비교 여유(float 문턱과 double 체력 비율의 경계 오차만 덮음).</summary>
        public const double ThresholdEpsilon = 1e-6;

        /// <summary>기습 처형 등 뒤 반각(±60°). 단검 등 찌르기와 같은 값(BackstabRule.HalfAngle).</summary>
        public const float AmbushHalfAngle = BackstabRule.HalfAngle;

        // ── 연출 값(ExecutionFx) ──
        public const float PullDistance = 0.6f;
        public const float PullSeconds = 0.06f;
        public const float SwordScale = 1.25f;
        public const float HitStop = 0.10f;
        public const float ShakeAmplitude = 0.12f;
        public const float ShakeSeconds = 0.14f;
        public const float SlowSeconds = 0.3f;
        public const float SlowScale = 0.35f;
        public const int GorePiecesMin = 4;
        public const int GorePiecesMax = 6;
        public const float BloodPoolScale = 1.4f;
        public const float HealFraction = 0.03f;

        /// <summary>
        /// 처형 자세(대검 내려찍기 자세를 칼 1.25배로) 길이(초). 0초 = 처형 순간(당겨 붙기 시작), PullSeconds(0.06초) = 내려찍는 순간.
        /// 근접 처형(기본공격)에만 쓰고, 휘두르는 동작이 그보다 먼저 끝나면 자세도 함께 끝난다. 검풍 처형은 자세가 없다(멀리서 맞힘).
        /// </summary>
        public const float PoseSeconds = 0.4f;

        /// <summary>무리 번호가 없는 적이 '마주침의 마지막 적'인지 볼 때 둘레(12유닛 안에 깨어 있는 다른 적이 없음, 아는 길 판정과 같은 12).</summary>
        public const float LastEnemyRange = Demo6.Core.Dungeon.ExplorePace.EnemyRange;

        /// <summary>잠든 무리 '뒤척임' 손잡이(2~4초마다 몸을 돌릴 확률). 기본 0, '숨어 다니기만' 보이면 0.3.</summary>
        public const float SleeperTurnDefault = 0f;
        public const float SleeperTurnMinSeconds = 2f;
        public const float SleeperTurnMaxSeconds = 4f;
        /// <summary>뒤척일 때 몸을 돌리는 각(도, 왼쪽·오른쪽 무작위).</summary>
        public const float SleeperTurnMinDegrees = 90f;
        public const float SleeperTurnMaxDegrees = 180f;

        /// <summary>무너짐 처형 문턱(0이면 처형하지 않는 대상). 보스·둥지·허수아비·가벼운 적은 0.</summary>
        public static float Threshold(TargetClass target, float medium = ThresholdMedium, float heavy = ThresholdHeavy, float elite = ThresholdElite)
        {
            if (target.Immune) return 0f;
            if (target.Named) return ThresholdNamed;
            if (target.Elite) return elite;
            switch (target.Weight)
            {
                case WeightClass.Medium: return medium;
                case WeightClass.Heavy: return heavy;
                default: return 0f;
            }
        }

        /// <summary>
        /// 무너짐 처형인가: 맞기 전에 무너져 있었고(wasBroken), 마무리 타나 검풍이며(finisherOrWave), 그 타로 죽었거나 남은 체력 비율이 문턱 이하.
        /// </summary>
        public static bool ShouldExecute(TargetClass target, bool wasBroken, bool finisherOrWave, bool killed, double hpFractionAfter,
            float medium = ThresholdMedium, float heavy = ThresholdHeavy, float elite = ThresholdElite)
        {
            if (!wasBroken || !finisherOrWave) return false;
            float t = Threshold(target, medium, heavy, elite);
            if (t <= 0f) return false;
            // 문턱은 float(0.12f 등)라 double 비율과 바로 견주면 경계가 흔들린다. 백만분의 1 여유로 '문턱과 같으면 처형'을 지킨다.
            return killed || hpFractionAfter <= t + ThresholdEpsilon;
        }

        /// <summary>
        /// 기습 처형 결과. asleep = 깨어 있지 않음(잠·먹는 중), noticed = 알아채기·무리 반응을 기다리는 중(들킴), crouching = 휘두를 때 웅크렸나,
        /// meleeBasic = 근접 기본공격(검풍·회오리 아님), behind = BackstabRule.IsBehind(적이 보는 쪽, 적 → 플레이어).
        /// </summary>
        public static AmbushOutcome Ambush(TargetClass target, bool asleep, bool noticed, bool crouching, bool meleeBasic, bool behind)
        {
            if (target.Nest || target.Dummy) return AmbushOutcome.None;
            if (!asleep || noticed || !crouching || !meleeBasic || !behind) return AmbushOutcome.None;
            if (target.Boss || target.Elite) return AmbushOutcome.PoiseOnly;
            return target.Weight == WeightClass.Heavy ? AmbushOutcome.Break : AmbushOutcome.Kill;
        }

        /// <summary>
        /// 처형 느린 화면(0.3초 × 0.35)을 쓰는가: 그 마주침의 마지막 적이거나 정예(이름난 정예 포함)일 때만(4-2 [3]). 나머지는 히트스톱과 피로 처리한다.
        /// 마지막 적 = 같은 무리 번호의 살아 있는 다른 적이 없음, 무리 번호가 없으면 LastEnemyRange 안에 깨어 있는 다른 적이 없음(Game이 정함).
        /// </summary>
        public static bool UsesSlowMotion(TargetClass target, bool lastOfEncounter)
        {
            if (target.Immune) return false;
            return lastOfEncounter || target.Elite || target.Named;
        }

        /// <summary>당겨 붙는 거리 = min(0.6, 몸 사이 틈). 틈이 없거나 음수면 0(몸에 겹치지 않음).</summary>
        public static float PullTo(float gap) => gap <= 0f ? 0f : gap < PullDistance ? gap : PullDistance;
    }

    /// <summary>
    /// 회피 반격 다듬기(4-2 [4], 결정 8-1 #5 = 추천 '켜기'). 판정은 그대로(구르기 무적 0.18초 동안 예고 공격을 몸으로 받아 냄),
    /// 창 1.0초, 첫 타 버팀 × 2. 더하는 것: 그 타 피해 +20%, 느린 화면 0.12초 × 0.4, 흰 잔상 0.3초, '스릉' 소리, 칼 흰 번쩍 0.15초,
    /// 창이 열린 동안 칼이 빛을 받지 않는 흰빛. 구르기 재사용 환급은 넣지 않는다.
    /// 피해 +20%는 타 배율에 곱해 DamageMath.ToMonster에 넣는다(반올림은 ToMonster 안 한 번). 창이 닫혀 있으면 배율을 그대로 돌려줘 예전과 비트까지 같다.
    /// </summary>
    public static class CounterRule
    {
        /// <summary>Tuning.DodgeCounter 기본값(결정 8-1 #5 '켜기').</summary>
        public const bool DefaultOn = true;
        public const float Window = 1.0f;
        public const float PoiseMultiplier = 2f;
        public const float DamageBonus = 0.2f;
        /// <summary>피해 배율 = 1 + DamageBonus(정확히 1.2, float 0.2f를 넓히면 생기는 오차 없이 곱한다).</summary>
        public const double DamageScale = 1.2;
        public const float SlowSeconds = 0.12f;
        public const float SlowScale = 0.4f;
        public const float AfterimageSeconds = 0.3f;
        public const float FlashSeconds = 0.15f;

        /// <summary>회피 반격 첫 타의 배율(%): 창이 열렸으면 × (1 + 0.2), 아니면 그대로(곱하지 않음).</summary>
        public static double DamagePercent(double hitPercent, bool counterReady) =>
            counterReady ? hitPercent * DamageScale : hitPercent;

        /// <summary>회피 반격 첫 타의 버팀 배율: 창이 열렸으면 2, 아니면 1.</summary>
        public static float PoiseScale(bool counterReady) => counterReady ? PoiseMultiplier : 1f;
    }
}
