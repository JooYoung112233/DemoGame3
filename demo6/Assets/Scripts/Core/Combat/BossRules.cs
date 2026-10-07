using System;
using Demo6.Core.Progression;

namespace Demo6.Core.Combat
{
    /// <summary>갱도 오우거 패턴(기획/전투-보스-무기-다듬기-1차.md 3-3). 글자는 문서 이름(A·B·D·C). 차례는 Tuning.BossForcePattern 값(0~3)과 같다.</summary>
    public enum BossPattern
    {
        /// <summary>A 버팀목 내려찍기(앞쪽 원, 정답 구르기).</summary>
        Slam,
        /// <summary>B 버팀목 앞세운 돌진(띠, 1단계 2연·2단계 3연, 마지막이 벽·기둥에 박히면 무너짐).</summary>
        Charge,
        /// <summary>D 휩쓸기 2연(앞 반원).</summary>
        Sweep,
        /// <summary>C 포효·낙석·소환(2단계만).</summary>
        Roar,
    }

    /// <summary>패턴 고르기 입력(쉬는 중에만 고른다).</summary>
    public struct BossPickInput
    {
        public bool Phase2;
        /// <summary>몸 중심 사이 거리(유닛).</summary>
        public float Distance;
        public bool SlamReady;
        public bool ChargeReady;
        public bool SweepReady;
        public bool RoarReady;
        /// <summary>직전 패턴(없으면 null). D는 두 번 잇지 않는다.</summary>
        public BossPattern? Last;
    }

    /// <summary>패턴 고르기 결과. Pattern이 null이면 걸어서 다가간다. ApproachThenSlam이면 0.4초 다가간 뒤 A(창 뒷걸음 찌르기 꼼수 막기).</summary>
    public readonly struct BossChoice
    {
        public readonly BossPattern? Pattern;
        public readonly bool ApproachThenSlam;

        public BossChoice(BossPattern? pattern, bool approachThenSlam)
        {
            Pattern = pattern;
            ApproachThenSlam = approachThenSlam;
        }

        public static BossChoice Walk => new BossChoice(null, false);
    }

    /// <summary>
    /// 첫 보스 '갱도 오우거' 수치와 규칙(3장, 결정 ④ = 전투 시험장 '보스' 프리셋 먼저). 숫자는 식으로 둔다(손으로 정한 숫자를 따로 두지 않는다).
    /// 시험판 = 10층판 패턴 + 2층 숫자. 모든 피해는 빨간 바닥 예고(어둠 위에서도 100%)이고 보스는 공격 기회(AttackTokens)를 쓰지 않는다.
    /// 오우거에게는 밀기·끊기·끌기·벽 박기가 듣지 않는다(넉백 저항 1, 준비가 끊기지 않음).
    /// BossRuleTests가 지킨다: 체력 2·5·10층, 예고 규칙(층마다 기준 플레이어로 셈), 걸어서 피하기 셈(걷기 배율 인자), 패턴 고르기 표, 반원 경계, 버팀 250 → 500.
    /// </summary>
    public static class BossRules
    {
        public const string DisplayName = "갱도 오우거";

        // ── 3-2 수치 ──

        public const int BaseHp = 32000;
        public const int BaseAttack = 300;
        /// <summary>보스가 나오는 가장 얕은 층(시험판 2층). 전투 시험장이 1층이어도 보스는 2층 값으로 세운다(3-2 '전투 시험장 Lv 1에서는 약 10% 더 아프다').</summary>
        public const int TrialFloor = 2;
        /// <summary>체력 계수: 시험판(1~4층) 0.65, 5층판(5~9층) 0.55, 10층판 1.0. 2층 26,416 / 5층 45,785 / 10층 275,032.</summary>
        public const double TrialHpFactor = 0.65;
        public const double MidHpFactor = 0.55;
        /// <summary>
        /// 공격 계수: 시험판(1~4층) 0.8(약 300 = 300 × 1.24 × 0.8). 5층 이상은 3차 식의 레벨 체력 보정(3차 부록: (장비 기준 체력 + 120 × (권장 Lv − 1)) ÷ 장비 기준 체력
        /// = ReferencePlayerHp ÷ 장비 기준 체력)이라 손으로 정한 숫자가 없다(5층 1.221 → 866, 10층 1.254 → 2,608 '약 2,600').
        /// </summary>
        public const double TrialAttackFactor = 0.8;
        public const float BreakSeconds = 3.0f;
        /// <summary>무너짐 중 받는 피해 +30%(기존 규칙, Enemy가 넣는다).</summary>
        public const float BrokenDamageTaken = 1.3f;
        /// <summary>2단계 문턱(체력 50%). 지금 패턴이나 무너짐이 끝난 뒤 넘어간다.</summary>
        public const float PhaseThreshold = 0.5f;
        public const float Diameter = 2.4f;
        public const float Mass = 50f;
        /// <summary>걷기(기획, 적 배율 곱하기 전) 1단계 2.8, 2단계 × 1.2 = 3.36. 돌진 11은 적 배율을 받지 않는다.</summary>
        public const float WalkSpeed = 2.8f;
        public const float Phase2SpeedScale = 1.2f;
        public const float ChargeSpeed = 11f;
        /// <summary>몸 돌기(초당 도): 1단계 120, 2단계 160.</summary>
        public const float TurnRate = 120f;
        public const float TurnRatePhase2 = 160f;
        /// <summary>숨 고르기(쉬는 시간) 범위: 1단계 0.9~1.3, 2단계 0.6~1.0.</summary>
        public const float RestMin = 0.9f;
        public const float RestMax = 1.3f;
        public const float RestPhase2Reduce = 0.3f;
        /// <summary>일어난 뒤 다음 패턴까지(피해 없음).</summary>
        public const float StandUpRest = 1.0f;
        /// <summary>깨어나며 몸을 돌리고 포효하는 시간(피해 없음). 기습한 쪽이 몇 대 더 넣는 틈이다.</summary>
        public const float WakeTime = 1.0f;
        /// <summary>A·D는 플레이어가 몸 앞 이 각도(도) 안일 때만 시작한다. 밖이면 몸을 돌리며 기다린다(등 뒤로 돌기가 정답인 이유).</summary>
        public const float FrontGate = 50f;
        /// <summary>이름표 Lv = 층 + 1.</summary>
        public static int PlateLevel(int floor) => FloorScaling.Clamp(floor) + 1;

        /// <summary>보스를 세울 층(2층보다 얕으면 시험판 2층).</summary>
        public static int SpawnFloor(int floor) => Math.Max(TrialFloor, FloorScaling.Clamp(floor));

        // ── 3-3 패턴 ──

        /// <summary>A 내려찍기: 거리 3.0 안, 재사용 4초, 몸 앞 1.8에 반경 3.0 원, 예고 1.0/0.9초, 250%, 넉백 3.0, 흔들림 0.3/0.3, 굳음 1.2초.</summary>
        public const float SlamRange = 3.0f;
        public const float SlamCooldown = 4f;
        public const float SlamOffset = 1.8f;
        public const float SlamRadius = 3.0f;
        public const float SlamWindup = 1.0f;
        public const float SlamPercent = 250f;
        public const float SlamKnockback = 3.0f;
        public const float SlamShake = 0.3f;
        public const float SlamRecover = 1.2f;
        /// <summary>A 빈틈 막기: 거리 3.0~4.0이고 B가 재사용 중이면 0.4초 다가간 뒤 A.</summary>
        public const float ApproachSeconds = 0.4f;

        /// <summary>B 돌진: 거리 4 이상, 재사용 10초, 띠 폭 2.0 × 길이 9 + 1.2, 첫 조준 0.7/0.6, 다시 조준 0.7, 150%, 옆으로 넉백 2.0, 1단계 2연·2단계 3연.</summary>
        public const float ChargeMinDistance = 4f;
        public const float ChargeCooldown = 10f;
        public const float ChargeWidth = 2.0f;
        public const float ChargeLength = 9f;
        public const float ChargeTelegraphExtra = 1.2f;
        public const float ChargeAim = 0.7f;
        /// <summary>
        /// 다시 조준 0.7초(문서 3-3 바탕값 0.6에서 올림, 2026-10-04 마무리 검토 ①). 다시 조준은 거리 조건 없이 바로 이어져 오우거가 바로 옆에서 조준할 수 있다.
        /// 멈춘 데서 걸어 비키면 가속(0.16초)을 넣어 0.6초에는 1.14밖에 못 가(필요 1.4) 2단계 3연(간격 약 1.8초 &lt; 구르기 재사용 2.0초)을 피할 길이 없었다.
        /// 0.7초면 1.57이라 B 걸음 4.24에서 걸어서 비킨다(판금 3.76은 1.39로 알려진 위험, 7장 #5).
        /// </summary>
        public const float ChargeReaim = 0.7f;
        public const float ChargePercent = 150f;
        public const float ChargeSideKnockback = 2.0f;
        public const int ChargeCount = 2;
        public const int ChargeCountPhase2 = 3;
        /// <summary>돌진 사이 발 끌기 0.35, 마지막이 아닌 돌진이 벽에 박히면 비틀 0.3, 박지 않으면 숨 고르기 1.0.</summary>
        public const float ChargeDrag = 0.35f;
        public const float ChargeStumble = 0.3f;
        public const float ChargeBreath = 1.0f;

        /// <summary>D 휩쓸기 2연: 거리 2.5 안, 재사용 5초, 직전이 D가 아님, 앞 반원 반경 3.2, 1타 예고 0.6, 2타 예고는 1타 판정 0.1초 전 시작해 0.6(판정 간격 0.5), 110% × 2, 넉백 1.0, 틈 0.8.</summary>
        public const float SweepRange = 2.5f;
        public const float SweepCooldown = 5f;
        public const float SweepRadius = 3.2f;
        public const float SweepWindup = 0.6f;
        public const float SweepSecondLead = 0.1f;
        public const float SweepInterval = 0.5f;
        public const float SweepPercent = 110f;
        public const float SweepKnockback = 1.0f;
        public const float SweepRecover = 0.8f;

        /// <summary>C 포효·낙석·소환(2단계만): 재사용 14초, 포효 1.0초 → 원 7개(무작위 6 + 발밑 1, 반경 1.2, 서로 2.8 이상) 1.0초, 120%, 굴쥐 4(동시 최대 6, 보상 없음).</summary>
        public const float RoarCooldown = 14f;
        public const float RoarTime = 1.0f;
        public const int RockCount = 7;
        public const float RockRadius = 1.2f;
        public const float RockGap = 2.8f;
        public const float RockPillarGap = 1.6f;
        public const float RockDoorGap = 2.0f;
        public const float RockWindup = 1.0f;
        public const float RockPercent = 120f;
        /// <summary>낙석 넉백(문서에 값이 없어 가볍게 둔다).</summary>
        public const float RockKnockback = 0.8f;
        public const int SummonRats = 4;
        public const int MaxRats = 6;

        /// <summary>2단계에서 예고를 줄이는 몫(A와 B 첫 조준에만).</summary>
        public const float Phase2TelegraphCut = 0.1f;

        // ── 3-5 2단계 전환 연출(1.2초) ──

        public const float TransitionTime = 1.2f;
        /// <summary>0.4초에 느린 화면 0.6초 × 0.5(순위 BossPhase), 눈이 흰색으로, 회백 테두리가 켜진다.</summary>
        public const float TransitionSlowAt = 0.4f;
        public const float TransitionSlowSeconds = 0.6f;
        public const float TransitionSlowScale = 0.5f;
        /// <summary>1.0초에 첫 C(전환 포효가 첫 C의 포효를 겸한다).</summary>
        public const float TransitionRoarAt = 1.0f;
        public const float TransitionShake = 0.15f;
        public const float TransitionShakeSeconds = 1.0f;

        // ── 3-7 처치 ──

        /// <summary>처치 멈춤: 히트스톱 예산 안 최대 0.2초, 이어서 1.0초 × 0.3 느린 화면(순위 BossKill). 소환 굴쥐는 흩어져 1.5초 뒤 사라진다.</summary>
        public const float KillHitStop = 0.2f;
        public const float KillSlowSeconds = 1.0f;
        public const float KillSlowScale = 0.3f;
        public const float SummonVanish = 1.5f;

        // ── 3-6 보스방·잠 ──

        /// <summary>시작: 오른쪽 벽을 보고 돌을 씹는 중(먹는 중 = 잠 규칙). 등 뒤 3 안에 들어오면 0.5초 뒤 깸, 맞으면 바로 깸.</summary>
        public const float WakeBehind = 3f;
        public const float WakeDelay = 0.5f;
        /// <summary>
        /// 보스방(문서 3-6 안쪽 26×14, 가운데 (0, 0)): 오우거 (7, 0), 플레이어 (−9, 0), 쥐 구멍 (−2, ±7)·(10, ±7).
        /// 전투 시험장 방은 안쪽 25×13이라 쥐 구멍은 방 가장자리에서 RatHoleInset 안쪽으로 잘라 쓰고, 기둥 y는 CombatTestRoot가 벽 틈을 문서와 같게 맞춘다.
        /// </summary>
        public const float StartX = 7f;
        public const float PlayerStartX = -9f;
        public const float RatHoleNearX = -2f;
        public const float RatHoleFarX = 10f;
        /// <summary>쥐 구멍은 벽(방 가장자리) 안쪽 이만큼에서 굴쥐를 내보낸다.</summary>
        public const float RatHoleInset = 0.7f;

        // ── 3-8 보상(시험판 첫 처치) ──

        public const int TrialFirstGear = 3;
        public const int TrialFirstStones = 10;
        public const int TrialFirstGold = 40;
        public const int TrialRepeatGear = 1;
        public const int TrialRepeatStones = 4;
        public const int TrialRepeatGold = 15;

        // ── 걸어서 피하기 셈(3-3 표, 7장 #5) ──

        /// <summary>판정 여유 = 플레이어 반지름(PlayerController.Radius와 같음).</summary>
        public const float PlayerRadius = 0.4f;
        /// <summary>반응 시간: 예고를 보고 움직이기 시작할 때까지.</summary>
        public const float ReactionTime = 0.25f;
        /// <summary>구르기 거리(3.5유닛/0.22초, 재사용 2.0초).</summary>
        public const float DodgeDistance = 3.5f;
        public const float DodgeCooldown = 2.0f;
        /// <summary>문서 기준 걸음(장비 5.3, ExplorePace.EquipWalk와 같음). 실제 걸음 = 이 값 × 걷기 배율(B 0.80 → 4.24).</summary>
        public const float ReferenceWalk = 5.3f;
        /// <summary>판금 한 벌(맨몸 5.0 × (1 − 0.06)). B 배율이면 3.76.</summary>
        public const float PlateWalk = 4.7f;
        /// <summary>걷기 가속: 멈춘 데서 다 빨라지기까지(PlayerController.WalkAccelTime과 같음, Tuning.MoveInertia 기본 켜짐). 다 빨라질 때까지 0.5 × 걷기 × 0.16만큼 덜 간다.</summary>
        public const float WalkAccelTime = 0.16f;
        /// <summary>오우거가 걷다가 멈추는 거리(몸 반지름 1.2 + 플레이어 0.4 + 조금, OgreBrain). D(2.5)·A(3.0) 안이라 D는 대개 이 거리에서 나온다.</summary>
        public const float StopDistance = 2.2f;

        /// <summary>A 원 최악 탈출 거리(원 중심에서 반경 + 플레이어 반지름) = 3.4.</summary>
        public static float SlamEscape => SlamRadius + PlayerRadius;
        /// <summary>B 띠 옆으로 비키기(반폭 + 플레이어 반지름) = 1.4. 띠는 조준할 때 플레이어 쪽을 향하므로 늘 띠 가운데에서 시작한다.</summary>
        public static float ChargeEscape => ChargeWidth * 0.5f + PlayerRadius;
        /// <summary>D 반원에서 물러나기: 거리 fromDistance에서 반경 + 반지름 밖까지. 고르는 거리 끝 2.5에서 1.1, 오우거가 멈추는 2.2에서 1.4.</summary>
        public static float SweepEscape(float fromDistance = SweepRange) => SweepRadius + PlayerRadius - fromDistance;
        /// <summary>C 발밑 원에서 걸어 나가기(반경 + 반지름) = 1.6.</summary>
        public static float RockEscape => RockRadius + PlayerRadius;

        /// <summary>
        /// 예고 동안 멈춘 데서 걸어서 가는 거리: 반응(0.25초) 뒤 가속(WalkAccelTime 동안 0 → 걷기)을 넣는다.
        /// 다 빨라진 뒤면 걷기 × (예고 − 반응 − 가속 ÷ 2), 그 전이면 걷기 × t² ÷ (2 × 가속)(t = 예고 − 반응). 가속 0이면 예전 식(걷기 × t)과 같다.
        /// </summary>
        public static float WalkDistance(float walkSpeed, float telegraph, float reaction = ReactionTime, float accelTime = WalkAccelTime)
        {
            float t = Math.Max(0f, telegraph - reaction);
            if (accelTime <= 0f) return walkSpeed * t;
            if (t <= accelTime) return walkSpeed * t * t / (2f * accelTime);
            return walkSpeed * (t - accelTime * 0.5f);
        }

        /// <summary>
        /// 돌진이 거리 distance(몸 중심 사이)에 선 플레이어에게 닿기까지 예고 뒤에 더 걸리는 시간 = (거리 − 몸 반지름 1.2 − 플레이어 반지름) ÷ 11.
        /// 첫 조준은 거리 4 이상에서만 고르므로 적어도 0.22초가 더 있다. 다시 조준은 거리 조건이 없어 바로 옆(0초)이 최악이다.
        /// </summary>
        public static float ChargeArrival(float distance) => Math.Max(0f, distance - Diameter * 0.5f - PlayerRadius) / ChargeSpeed;

        // ── 식 ──

        static double HpFactor(int floor)
        {
            int f = FloorScaling.Clamp(floor);
            return f >= 10 ? 1.0 : f >= 5 ? MidHpFactor : TrialHpFactor;
        }

        /// <summary>공격 계수: 1~4층 시험판 0.8, 5층 이상 레벨 체력 보정(그 층 기준 플레이어 체력 ÷ 장비 기준 체력).</summary>
        public static double AttackFactor(int floor)
        {
            int f = FloorScaling.Clamp(floor);
            return f >= 5 ? (double)ReferencePlayerHp(f) / FloorScaling.Baseline(f).MaxHp : TrialAttackFactor;
        }

        /// <summary>체력 = 32,000 × 1.27^(층 − 1) × 계수(0.5 올림). 2층 26,416, 5층 45,785, 10층 275,032.</summary>
        public static int Hp(int floor) =>
            DamageMath.RoundHalfUp(BaseHp * Math.Pow(FloorScaling.HpGrowth, FloorScaling.Clamp(floor) - 1) * HpFactor(floor));

        /// <summary>공격 = 300 × 1.24^(층 − 1) × 계수(0.5 올림). 2층 298, 5층 866, 10층 2,608.</summary>
        public static int Attack(int floor) =>
            DamageMath.RoundHalfUp(BaseAttack * FloorScaling.AttackMultiplier(floor) * AttackFactor(floor));

        /// <summary>버팀(층 배율을 받지 않음): 시험판 250, 5층판 300, 10층판 400. 무너졌다 일어날 때마다 +25%(2배 상한, PoiseMeter 보스 모드).</summary>
        public static double Poise(int floor)
        {
            int f = FloorScaling.Clamp(floor);
            return f >= 10 ? 400 : f >= 5 ? 300 : 250;
        }

        /// <summary>그 층 기준 플레이어 체력 = 2차 4-8 기준 장비 체력 + 레벨 체력(권장 레벨, 질긴 몸 없음). 2층 2,640(Lv 3), 5층 4,636(Lv 8), 10층 8,876(Lv 16).</summary>
        public static int ReferencePlayerHp(int floor) =>
            LevelHp.MaxHp(XpRules.RecommendedLevel(FloorScaling.Clamp(floor)), 0, FloorScaling.Baseline(floor).MaxHp);

        /// <summary>그 층 기준 플레이어 방어(2층 120 → 피해 × 0.893).</summary>
        public static int ReferencePlayerDefense(int floor) => FloorScaling.Baseline(floor).Defense;

        /// <summary>
        /// 패턴 한 방(굴림 평균 1.0)이 그 층 기준 플레이어 최대 체력에서 차지하는 몫. 2층: A 25.2%, B 15.1%, D 11.1%, C 12.1%.
        /// 셈은 일반 적과 같은 TelegraphRule.HitFraction(검토 1차 Q6에서 옮김).
        /// </summary>
        public static double HitFraction(BossPattern pattern, int floor) =>
            TelegraphRule.HitFraction(Attack(floor), Percent(pattern), floor);

        /// <summary>
        /// 3차 예고 규칙(플레이어 최대 체력 대비 한 방): 5% 이하 0.25초, 5~10% 0.4초, 10~16% 0.6초(소리 필수), 16~20% 0.8초, 20% 넘음은 보스만 0.9초.
        /// 계산은 일반 적도 함께 쓰도록 TelegraphRule.MinSeconds로 옮겼다(검토 1차 Q6). 이 이름은 보스 쪽 호출·시험을 위해 남긴다.
        /// </summary>
        public static float MinTelegraph(double hitFractionOfPlayerHp) => TelegraphRule.MinSeconds(hitFractionOfPlayerHp);

        /// <summary>패턴 예고 시간(문서 바탕값, 시험판 2층). 2단계면 A·B 첫 조준만 −0.1초. Charge는 첫 조준, 다시 조준은 ChargeReaim 그대로. Roar는 낙석 원.</summary>
        public static float Telegraph(BossPattern pattern, bool phase2)
        {
            switch (pattern)
            {
                case BossPattern.Slam: return SlamWindup - (phase2 ? Phase2TelegraphCut : 0f);
                case BossPattern.Charge: return ChargeAim - (phase2 ? Phase2TelegraphCut : 0f);
                case BossPattern.Sweep: return SweepWindup;
                default: return RockWindup;
            }
        }

        /// <summary>
        /// 그 층에서 쓰는 예고 시간 = 문서 바탕값과 3차 규칙(MinTelegraph(그 층 한 방)) 가운데 긴 쪽.
        /// 2층(시험판)은 바탕값 그대로다. 5층 B(공격 866, 한 방 18.7%)·10층 B(2,608, 17.0%)는 규칙상 0.8초라 첫 조준·다시 조준 모두 0.8로 올라간다
        /// (문서 3-3은 시험판 숫자로만 셈했다. 5층판 공격을 식으로 내면서 5층에도 같은 충돌이 드러남).
        /// </summary>
        public static float Telegraph(BossPattern pattern, bool phase2, int floor) =>
            Math.Max(Telegraph(pattern, phase2), MinTelegraph(HitFraction(pattern, floor)));

        /// <summary>B 다시 조준 예고(0.7초, 규칙에 걸리면 올림).</summary>
        public static float ChargeReaimTelegraph(int floor) =>
            Math.Max(ChargeReaim, MinTelegraph(HitFraction(BossPattern.Charge, floor)));

        /// <summary>D 2타 예고(0.6초, 1타 판정 0.1초 전에 시작, 규칙에 걸리면 올림).</summary>
        public static float SweepSecondTelegraph(int floor) =>
            Math.Max(SweepWindup, MinTelegraph(HitFraction(BossPattern.Sweep, floor)));

        /// <summary>패턴 배율(%).</summary>
        public static float Percent(BossPattern pattern)
        {
            switch (pattern)
            {
                case BossPattern.Slam: return SlamPercent;
                case BossPattern.Charge: return ChargePercent;
                case BossPattern.Sweep: return SweepPercent;
                default: return RockPercent;
            }
        }

        /// <summary>패턴 재사용(초). 재사용은 패턴을 시작할 때부터 센다.</summary>
        public static float Cooldown(BossPattern pattern)
        {
            switch (pattern)
            {
                case BossPattern.Slam: return SlamCooldown;
                case BossPattern.Charge: return ChargeCooldown;
                case BossPattern.Sweep: return SweepCooldown;
                default: return RoarCooldown;
            }
        }

        /// <summary>돌진 수(1단계 2연, 2단계 3연).</summary>
        public static int Charges(bool phase2) => phase2 ? ChargeCountPhase2 : ChargeCount;

        /// <summary>몸 돌기(초당 도).</summary>
        public static float Turn(bool phase2) => phase2 ? TurnRatePhase2 : TurnRate;

        /// <summary>숨 고르기 범위(2단계 −0.3초). t는 0~1 무작위 값.</summary>
        public static float Rest(bool phase2, float t)
        {
            float cut = phase2 ? RestPhase2Reduce : 0f;
            float u = Math.Max(0f, Math.Min(1f, t));
            return RestMin - cut + (RestMax - RestMin) * u;
        }

        /// <summary>
        /// 걸어서 피하기 셈(3-3 표): 멈춘 데서 예고 동안 걸어서 가는 거리(반응 0.25초, 가속 0.16초, WalkDistance) ≥ 탈출 거리이면 걸어서 피함.
        /// 아니면 구르기 3.5 ≥ 탈출 거리를 따로 본다. walkSpeed는 배율을 곱한 실제 걸음(B 4.24, 판금 3.76).
        /// </summary>
        public static bool WalkEscapes(float walkSpeed, float telegraph, float escapeDistance, float reaction = ReactionTime) =>
            WalkDistance(walkSpeed, telegraph, reaction) >= escapeDistance;

        /// <summary>구르기 3.5로 빠져나가는가.</summary>
        public static bool DodgeEscapes(float escapeDistance, float dodgeDistance = DodgeDistance) => dodgeDistance >= escapeDistance;

        /// <summary>
        /// 패턴 고르기(3-3, 쉬는 중에만): ① 2단계이고 C 준비 → C ② 거리 4 이상이고 B 준비 → B ③ 거리 2.5 안이고 D 준비(직전이 D 아님) → D
        /// ④ 거리 3.0 안 → A ⑤ 거리 3.0~4.0이고 B 재사용 중 → 0.4초 다가간 뒤 A ⑥ 그 밖에는 걸어서 다가감.
        /// A 재사용 중이면 ④·⑤는 걷기로 넘긴다.
        /// </summary>
        public static BossChoice Pick(in BossPickInput input)
        {
            if (input.Phase2 && input.RoarReady) return new BossChoice(BossPattern.Roar, false);
            if (input.Distance >= ChargeMinDistance && input.ChargeReady) return new BossChoice(BossPattern.Charge, false);
            if (input.Distance <= SweepRange && input.SweepReady && input.Last != BossPattern.Sweep) return new BossChoice(BossPattern.Sweep, false);
            if (input.Distance <= SlamRange && input.SlamReady) return new BossChoice(BossPattern.Slam, false);
            if (input.Distance > SlamRange && input.Distance < ChargeMinDistance && !input.ChargeReady && input.SlamReady)
                return new BossChoice(BossPattern.Slam, true);
            return BossChoice.Walk;
        }

        /// <summary>
        /// 패턴 강제(시험 패널 3-10): 재사용·단계·직전 패턴을 보지 않고 그 패턴만 쓴다. A는 거리 3.0 안, D는 2.5 안에 들어올 때까지 걸어서 다가가고,
        /// B·C는 거리와 관계없이 바로 쓴다.
        /// </summary>
        public static BossChoice PickForced(BossPattern forced, in BossPickInput input)
        {
            switch (forced)
            {
                case BossPattern.Slam: return input.Distance <= SlamRange ? new BossChoice(BossPattern.Slam, false) : BossChoice.Walk;
                case BossPattern.Sweep: return input.Distance <= SweepRange ? new BossChoice(BossPattern.Sweep, false) : BossChoice.Walk;
                default: return new BossChoice(forced, false);
            }
        }
    }
}
