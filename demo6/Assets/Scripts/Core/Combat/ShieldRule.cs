using System;

namespace Demo6.Core.Combat
{
    /// <summary>
    /// 방패로 받은 한 타의 결과(ShieldRule.Resolve). Result: Hit(못 막음) · Blocked(막음) · Parried(튕김).
    /// DamageScale·KnockScale은 피해·밀림에 곱하는 값(못 막으면 1), MeterAfter는 그 타 뒤 방어 게이지(깨지면 0), Broke는 이 타로 막기가 깨졌는가.
    /// </summary>
    public readonly struct GuardOutcome
    {
        public readonly HitResult Result;
        public readonly float DamageScale;
        public readonly float KnockScale;
        public readonly float MeterAfter;
        public readonly bool Broke;

        public GuardOutcome(HitResult result, float damageScale, float knockScale, float meterAfter, bool broke)
        {
            Result = result;
            DamageScale = damageScale;
            KnockScale = knockScale;
            MeterAfter = meterAfter;
            Broke = broke;
        }

        public bool Stopped => Result == HitResult.Blocked || Result == HitResult.Parried;
    }

    /// <summary>
    /// ShieldRule.Resolve 입력. 방향은 몸 틀이 아니라 월드 값이다.
    /// FacingX/Y = 몸이 보는 쪽, ToSourceX/Y = 플레이어에서 공격이 온 쪽(= −공격 진행 방향, 진행 방향이 없으면 때린 자리 − 내 위치).
    /// SourceDistance = 때린 자리까지 거리(진행 방향이 있으면 아무 값, HasTravel = true). 몸 반지름 0.4 안이면 방향을 몰라 못 막는다.
    /// </summary>
    public struct GuardQuery
    {
        public HitKind Kind;
        /// <summary>막기가 켜져 있나(들기·막기·반동·밀쳐 내기, 내리기 앞 0.06초).</summary>
        public bool Guarding;
        /// <summary>패링 창이 열려 있나(들기 시작부터 Tuning.ParryWindow 안, 다시 들기 잠금 없음).</summary>
        public bool ParryOpen;
        public float FacingX, FacingY;
        public float ToSourceX, ToSourceY;
        /// <summary>공격 진행 방향을 알았나(돌진·화살·휩쓸기). 모르면 SourceDistance로 몸 안 타를 가린다.</summary>
        public bool HasTravel;
        public float SourceDistance;
        /// <summary>그 공격 배율(%). 근접 방어 게이지 깎기 = 배율 ÷ 4.</summary>
        public float PatternPercent;
        /// <summary>지금 방어 게이지(0~MeterMax).</summary>
        public float Meter;
        /// <summary>일반 감소(근접·멧돼지 돌진·꿰뚫는 화살 피해 배율, 시험 손잡이 Tuning.GuardDamageScale, 기본 0.25).</summary>
        public float GeneralDamageScale;
        /// <summary>방어 게이지 최대치(시험 손잡이 Tuning.GuardMeterMax). 0 이하면 ShieldRule.GuardMax(100).</summary>
        public float MeterMax;
        /// <summary>패링 환급(시험 손잡이 Tuning.GuardParryRefund). null이면 ShieldRule.ParryRefund(20). 음수는 0으로 본다.</summary>
        public float? Refund;
    }

    /// <summary>패링 밀쳐 내기가 끝날 때 갈 곳(ShieldRule.AfterParryPush).</summary>
    public enum ParryPushEnd
    {
        /// <summary>자유 상태(버튼을 뗐음).</summary>
        Free,
        /// <summary>같은 누름을 누르고 있어 막기를 이어 감(새 패링 창 없음).</summary>
        Hold,
        /// <summary>반격 창 안에서 밀쳐 내기 동안 왼쪽 클릭을 눌러 반격 베기로.</summary>
        Riposte,
    }

    /// <summary>
    /// 한손검과 방패 막기·패링 규칙(기획/세-무기-우클릭-소켓-1차.md 2-5~2-7). 순수 함수와 상수만 있다(UnityEngine 없음).
    /// 계약 단계 값(확정)이고 꾸러미 ① 규칙이 시험한다(ShieldRuleTests). PlayerController(꾸러미 ③)는 Resolve 한 입구로 막기를 가른다.
    /// </summary>
    public static class ShieldRule
    {
        // ── 시간(초) ──
        public const float RaiseTime = 0.12f;
        public const float LowerTime = 0.12f;
        /// <summary>내리기 앞 이 시간까지는 막는다.</summary>
        public const float LowerBlockTime = 0.06f;
        /// <summary>톡 눌러도 이만큼은 든다.</summary>
        public const float MinHold = 0.25f;
        /// <summary>패링 창(들기 시작부터). 시험 손잡이 Tuning.ParryWindow(0.10~0.30)가 덮어쓴다.</summary>
        public const float ParryWindow = 0.18f;
        /// <summary>직전 들기 시작에서 이 시간이 안 지났으면 이번 들기에는 패링 창이 없다(패링 성공이면 바로 풀림).</summary>
        public const float ParryRearm = 0.6f;
        /// <summary>
        /// 패링 뒤 밀쳐 내기. 끝날 때 그 누름을 아직 누르고 있으면 막기를 이어 간다(새 패링 창은 없음, AfterParryPush, 0-3의 20).
        /// </summary>
        public const float ParryPushTime = 0.15f;
        /// <summary>막은 반동 그림 길이: 일반·보스.</summary>
        public const float RecoilTime = 0.12f;
        public const float BossRecoilTime = 0.25f;
        /// <summary>밀린 뒤 경직(넉백 0.1초와 합쳐 걸음 0.25초 멈춤).</summary>
        public const float BlockStagger = 0.15f;
        /// <summary>막기 무적(같은 타 겹침 방지). 깜빡임·피격 자세·흔들림·피격 무적 0.5초는 없다.</summary>
        public const float BlockInvulnerable = 0.12f;
        /// <summary>막기 깨짐: 걸음·행동 멈춤(BreakStagger), 다시 들 수 없음(BreakLock, 그리고 게이지가 RaiseAfterBreakFraction까지 찰 때까지).</summary>
        public const float BreakStagger = 0.8f;
        public const float BreakLock = 1.5f;
        /// <summary>막지 못한 타로 끊기면 맞은 순간부터 이만큼 다시 들 수 없다(새로 눌러야 함).</summary>
        public const float HurtLock = 0.4f;

        // ── 방향 ──
        /// <summary>몸 방향 ±90°(앞 반원)만 막는다.</summary>
        public const float FrontHalfAngle = 90f;
        /// <summary>때린 자리가 이 반지름 안이고 진행 방향이 없으면 방향을 몰라 못 막는다(PlayerController.Radius).</summary>
        public const float BodyRadius = 0.4f;

        // ── 걸음·돌기 ──
        public const float GuardMoveScale = WeaponActRules.GuardMoveScale;
        public const float ParryPushMoveScale = WeaponActRules.ParryPushMoveScale;
        public const float GuardTurnDegPerSec = WeaponActRules.GuardTurnDegPerSec;

        // ── 방어 게이지(예전 숨은 '방패 버팀', 2026-10-05부터 캐릭터 밑 막대로 보임, 0-3의 28). 상태는 GuardGauge가 든다 ──
        /// <summary>최대치 기본값(시험 손잡이 Tuning.GuardMeterMax 50~200).</summary>
        public const float GuardMax = 100f;
        /// <summary>막지 않을 때: 마지막 막기 0.5초 뒤부터 초당 50(0에서 가득까지 2.5초).</summary>
        public const float RegenIdleDelay = 0.5f;
        public const float RegenIdlePerSecond = 50f;
        /// <summary>막는 중: 마지막 막기 1.0초 뒤부터 초당 10(아주 느리게, 쉴 때의 1/5). 막대가 거의 멈춰 '내려야 찬다'가 읽힌다.</summary>
        public const float RegenGuardDelay = 1.0f;
        public const float RegenGuardPerSecond = 10f;
        /// <summary>패링 성공이면 깎지 않고 돌려줌(시험 손잡이 Tuning.GuardParryRefund 0~50).</summary>
        public const float ParryRefund = 20f;
        /// <summary>이 몫 아래면 방패 색 × 0.8, 0.02 떨림, 게이지 막대가 붉어짐(깨짐 직전).</summary>
        public const float LowMeterFraction = 0.35f;
        /// <summary>
        /// 깨진 뒤에는 게이지가 최대치의 이 몫까지 다시 차야 들 수 있다(BreakLock 1.5초도 함께 지나야 함).
        /// 기본 빠르기에서는 0.5초 쉼 + 50 ÷ 50 = 1.5초로 BreakLock과 같은 순간이라, 다시 드는 때가 막대에 보인다.
        /// </summary>
        public const float RaiseAfterBreakFraction = 0.5f;

        // ── 패링 공통 효과 ──
        public const float ParryHitStop = 0.06f;
        public const float ParryShakeAmount = 0.04f;
        public const float ParryShakeTime = 0.06f;
        public const int ParrySparks = 8;
        /// <summary>
        /// 반격 창(회피 반격과 같은 칼빛). 이 안 왼쪽 클릭이면 ③ 마무리 베기('반격 베기', 찌르기 없음) 피해 × 1.5, 버팀 × 2, 마무리로 친다.
        /// 창은 튕긴 적이 틈을 보인 패링에서만 열리고(Enemy.ParryOpensRiposte: 화살·오우거 휩쓸기 1타는 아님), 배율은 그 적에게만 준다(0-3의 23).
        /// </summary>
        public const float RiposteWindow = 1.0f;
        public const float RiposteDamageScale = 1.5f;
        public const float RipostePoiseScale = 2f;
        /// <summary>반격 베기로 쓰는 콤보 단계 번호(1부터, 한손검 ③ 마무리 베기).</summary>
        public const int RiposteStepNumber = 3;

        // ── 적 쪽 패링 효과(Enemy.Parried, 2-6) ──
        /// <summary>가벼움(굴쥐): 준비 끊김, 휘청(멈춤·생각 멈춤), 밀림, 공격 기회 반납.</summary>
        public const float LightStaggerSeconds = 1.2f;
        public const float LightParryPush = 0.8f;
        /// <summary>보통(궁수): 준비 끊김, 휘청.</summary>
        public const float MediumStaggerSeconds = 0.6f;
        /// <summary>무거움(멧돼지 머리치기·뒷발): 버팀을 깎고(ParryPoise) 0.5초 흔들림(준비는 안 끊김).</summary>
        public const float HeavyShakeSeconds = 0.5f;
        /// <summary>멧돼지 돌진 패링: 벽 박기와 같은 결과(3차는 바로 무너짐, M0a는 기절 1.5초).</summary>
        public const float BoarRushStunSeconds = 1.5f;
        /// <summary>오우거 휩쓸기 패링: 끊기지 않음, 버팀 최대치의 6%.</summary>
        public const float BossSweepPoiseFraction = 0.06f;

        // ── 패링 보상(2026-10-05 사용자 원문 "방패는 좋아보여 다만 패링하면 상대 그로기 게이지가 까이고 데미지가 어느정도들어가게 하자") ──
        /// <summary>
        /// 패링 피해(공격력의 %, 치명 없이 피해 굴림만, 보스 피해 보너스는 받음, 시험 손잡이 Tuning.ParryDamagePercent 0~100).
        /// 30%: 1~10층 최대 굴림도 굴쥐를 한 방에 죽이지 않고(마무리는 반격 베기 몫), 멧돼지 한 주기(3.9초) 실효 계수 1.61로 띠 위 끝(1.60)에 가장 가깝다.
        /// 한 번 패링의 피해 = 반격 베기 195% + 패링 30% = 225%.
        /// </summary>
        public const float ParryDamagePercent = 30f;
        /// <summary>패링 그로기(버팀) 깎기: 일반 적(보통·무거움) 버팀 최대치의 50%(전 35%, 시험 손잡이 Tuning.ParryPoiseFraction).</summary>
        public const float ParryPoiseFraction = 0.5f;
        /// <summary>정예는 줄인 값 35%(정예 멧돼지 135 → 47.25, 굳은 정예 270 → 94.5). 보스(오우거 휩쓸기)는 BossSweepPoiseFraction 6% 그대로.</summary>
        public const float ParryElitePoiseFraction = 0.35f;

        /// <summary>
        /// 패링으로 깎는 버팀(그로기 게이지) 양. poiseMax = 그 적 버팀 최대치(없으면 0 → 0, 굴쥐). 보스(또는 bossSweep)는 6%,
        /// 정예는 eliteFraction(기본 35%), 그 밖 일반 적(궁수·멧돼지 머리치기·뒷발)은 normalFraction(기본 50%). 음수 몫은 0.
        /// 멧돼지 돌진은 이 값 대신 바로 무너진다(BoarBrain, 벽 박기와 같은 결과).
        /// </summary>
        public static double ParryPoise(in TargetClass target, double poiseMax, float normalFraction = ParryPoiseFraction,
            float eliteFraction = ParryElitePoiseFraction, bool bossSweep = false)
        {
            if (poiseMax <= 0) return 0;
            double f = bossSweep || target.Boss ? BossSweepPoiseFraction : target.Elite ? eliteFraction : normalFraction;
            return f > 0 ? poiseMax * f : 0;
        }

        /// <summary>
        /// 패링 피해(정수, 1 이상, percent ≤ 0이면 0): 공격력 × percent% × (보스면 1 + 보스 피해 보너스) × 굴림(0.9~1.1) × 방어 배율. 치명은 굴리지 않는다
        /// (DamageMath.ToMonster와 같은 식, 시험이 1~10층 최대 굴림을 굴쥐 체력과 견준다).
        /// </summary>
        public static int ParryDamage(double attack, float percent, double roll, int targetDefense = 0, double bossDamageBonus = 0, bool isBoss = false)
        {
            if (percent <= 0f || attack <= 0) return 0;
            return DamageMath.ToMonster(attack, percent, false, 1.0, roll, targetDefense, 0, false, bossDamageBonus, isBoss);
        }

        /// <summary>막은 타 피해 배율(2-7 표). 일반(근접·멧돼지 돌진·꿰뚫는 화살)은 general(기본 0.25).</summary>
        public static float DamageScale(HitKind kind, float general = 0.25f)
        {
            switch (kind)
            {
                case HitKind.Melee:
                case HitKind.Rush:
                case HitKind.PierceArrow: return general;
                case HitKind.Arrow: return 0f;
                case HitKind.BossSweep:
                case HitKind.BossRush: return 0.5f;
                case HitKind.BossSlam: return 0.6f;
                default: return 1f;
            }
        }

        /// <summary>막은 타 밀림 배율(2-7 표).</summary>
        public static float KnockScale(HitKind kind)
        {
            switch (kind)
            {
                case HitKind.Melee:
                case HitKind.Rush:
                case HitKind.PierceArrow: return 0.3f;
                case HitKind.Arrow: return 0f;
                case HitKind.BossSweep: return 0.5f;
                default: return 1f;
            }
        }

        /// <summary>
        /// 막은 타가 깎는 방어 게이지(2-5): 막은 공격의 세기. 근접 = 배율 ÷ 4(굴쥐 25, 멧돼지 머리치기·뒷발 15), 나머지는 표 값. 못 막는 종류는 0.
        /// 실제 피해가 아니라 공격 배율로 정해 층·장비가 바뀌어도 같은 공격은 같은 만큼 깎는다(0-3의 28). 내려찍기는 최대치와 상관없이 늘 깨진다(Resolve).
        /// </summary>
        public static float GuardCost(HitKind kind, float patternPercent)
        {
            switch (kind)
            {
                case HitKind.Melee: return Math.Max(0f, patternPercent) / 4f;
                case HitKind.Rush: return 30f;
                case HitKind.Arrow: return 10f;
                case HitKind.PierceArrow: return 50f;
                case HitKind.BossSweep: return 40f;
                case HitKind.BossRush: return 50f;
                case HitKind.BossSlam: return GuardMax;
                default: return 0f;
            }
        }

        /// <summary>방패로 막을 수 있는 종류인가(낙석·덫은 못 막음).</summary>
        public static bool Blockable(HitKind kind) => kind != HitKind.FromAbove && kind != HitKind.Trap;

        /// <summary>패링할 수 있는 종류인가(오우거 내려찍기·돌진, 낙석·덫은 안 됨).</summary>
        public static bool Parryable(HitKind kind)
        {
            switch (kind)
            {
                case HitKind.Melee:
                case HitKind.Rush:
                case HitKind.Arrow:
                case HitKind.PierceArrow:
                case HitKind.BossSweep: return true;
                default: return false;
            }
        }

        /// <summary>앞에서 막아도 늘 깨지는 종류(오우거 내려찍기).</summary>
        public static bool AlwaysBreaks(HitKind kind) => kind == HitKind.BossSlam;

        /// <summary>
        /// 앞 반원 안인가: 몸 방향과 '공격이 온 쪽' 사이 각이 FrontHalfAngle(90°) 이하(89.9 막음, 90.1 못 막음). 어느 한쪽 길이가 0이면 false.
        /// </summary>
        public static bool IsFront(float facingX, float facingY, float toSourceX, float toSourceY)
        {
            double fl = Math.Sqrt((double)facingX * facingX + (double)facingY * facingY);
            double sl = Math.Sqrt((double)toSourceX * toSourceX + (double)toSourceY * toSourceY);
            if (fl < 1e-6 || sl < 1e-6) return false;
            double cos = ((double)facingX * toSourceX + (double)facingY * toSourceY) / (fl * sl);
            if (cos > 1) cos = 1;
            else if (cos < -1) cos = -1;
            double deg = Math.Acos(cos) * 180.0 / Math.PI;
            return deg <= FrontHalfAngle + 1e-4;
        }

        /// <summary>
        /// 막기 가르기 한 입구(2-5~2-7). 막지 못하면(막기 꺼짐·못 막는 종류·방향 모름·뒤 반원) Hit(배율 1, 게이지 그대로).
        /// 패링 창 안이고 패링되는 종류면 Parried(피해·밀림 0, 게이지 + 환급, 최대치에서 자름). 아니면 Blocked(표 배율, 게이지 − GuardCost, 0 이하거나 내려찍기면 Broke·게이지 0).
        /// 최대치·환급은 질의 값(MeterMax·Refund)을 쓰고, 비어 있으면 GuardMax·ParryRefund. 깎는 양(GuardCost)은 최대치와 상관없이 같다.
        /// </summary>
        public static GuardOutcome Resolve(in GuardQuery q)
        {
            float max = q.MeterMax > 0f ? q.MeterMax : GuardMax;
            float meter = Math.Max(0f, Math.Min(max, q.Meter));
            bool directionKnown = q.HasTravel || q.SourceDistance > BodyRadius;
            if (!q.Guarding || !Blockable(q.Kind) || !directionKnown || !IsFront(q.FacingX, q.FacingY, q.ToSourceX, q.ToSourceY))
                return new GuardOutcome(HitResult.Hit, 1f, 1f, meter, false);
            if (q.ParryOpen && Parryable(q.Kind))
            {
                float refund = Math.Max(0f, q.Refund ?? ParryRefund);
                return new GuardOutcome(HitResult.Parried, 0f, 0f, Math.Min(max, meter + refund), false);
            }
            float after = meter - GuardCost(q.Kind, q.PatternPercent);
            bool broke = AlwaysBreaks(q.Kind) || after <= 0f;
            return new GuardOutcome(HitResult.Blocked, DamageScale(q.Kind, q.GeneralDamageScale), KnockScale(q.Kind), broke ? 0f : after, broke);
        }

        // ── 시간 판단(순수 함수, PlayerController가 GuardQuery.ParryOpen·Guarding을 채울 때 쓴다) ──

        /// <summary>
        /// 이번 들기에 패링 창이 있나(2-6 연타 막기): 직전 들기 시작에서 ParryRearm(0.6초)이 지났거나, 그 뒤 패링에 성공해 잠금이 풀렸으면 있다.
        /// 처음 드는 것이면 sinceLastRaiseStart에 float.PositiveInfinity를 넣는다. 0.5초면 없음, 0.6초면 있음.
        /// </summary>
        public static bool ParryArmed(float sinceLastRaiseStart, bool parriedSinceLastRaise) =>
            parriedSinceLastRaise || sinceLastRaiseStart >= ParryRearm;

        /// <summary>
        /// 이번 들기에 패링 창이 있나(2-6, 0-3의 20). 패링한 그 누름을 계속 누르고 있어 다시 드는 것(samePressAsParry, 반격 베기 뒤)은 창이 없다.
        /// 새로 누른 들기는 ParryArmed와 같다(패링 성공 뒤라 잠금 없이 창이 열림). 같은 누름으로 다시 드는 것은 연타 잠금 시각도 바꾸지 않는다(부르는 쪽).
        /// </summary>
        public static bool ParryArmedOnRaise(bool samePressAsParry, float sinceLastRaiseStart, bool parriedSinceLastRaise) =>
            !samePressAsParry && ParryArmed(sinceLastRaiseStart, parriedSinceLastRaise);

        /// <summary>
        /// 패링 밀쳐 내기(0.15초)가 끝날 때 갈 곳(2-6, 0-3의 20). 반격 창이 열려 있고 밀쳐 내기 동안 왼쪽 클릭을 눌렀으면 반격 베기,
        /// 아니고 패링한 그 누름을 아직 누르고 있으면 막기를 이어 감(새 패링 창 없음, 막기 구멍 없음), 아니면 자유 상태.
        /// </summary>
        public static ParryPushEnd AfterParryPush(bool samePressHeld, bool attackPressed, bool riposteOpen)
        {
            if (attackPressed && riposteOpen) return ParryPushEnd.Riposte;
            return samePressHeld ? ParryPushEnd.Hold : ParryPushEnd.Free;
        }

        /// <summary>
        /// 지금 패링 창이 열려 있나: 이번 들기에 창이 있고(armed) 들기 시작부터 window초까지(기본 0.18, 시험 손잡이 Tuning.ParryWindow).
        /// 0.179초면 튕김, 0.181초면 그냥 막기.
        /// </summary>
        public static bool ParryOpenAt(float sinceRaiseStart, bool armed, float window = ParryWindow) =>
            armed && sinceRaiseStart >= 0f && sinceRaiseStart <= window;

        /// <summary>버튼을 뗐을 때 내리기를 시작해도 되나(톡 눌러도 MinHold 0.25초는 든다).</summary>
        public static bool CanLower(float raisedFor) => raisedFor >= MinHold;

        /// <summary>내리는 중에도 막나(내리기 앞 LowerBlockTime 0.06초까지).</summary>
        public static bool LowerStillBlocks(float lowerTime) => lowerTime <= LowerBlockTime;

        /// <summary>
        /// 방어 게이지 회복(2-5). sinceLastBlock = 마지막으로 막거나 튕긴 뒤 지난 시간. 막는 중이면 1.0초 뒤부터 초당 10, 아니면 0.5초 뒤부터 초당 50. GuardMax에서 자른다.
        /// </summary>
        public static float Regen(float meter, float sinceLastBlock, bool guarding, float dt) =>
            Regen(meter, sinceLastBlock, guarding, dt, GuardMax, 1f);

        /// <summary>
        /// 방어 게이지 회복(시험 손잡이 판): 최대치 max(0 이하면 GuardMax)에서 자르고, 회복 빠르기 rateScale(Tuning.GuardRegenScale)을 두 빠르기에 곱한다.
        /// 쉬는 시간(0.5·1.0초)은 빠르기와 상관없이 같다. 이미 최대치 위면 최대치로 내린다.
        /// </summary>
        public static float Regen(float meter, float sinceLastBlock, bool guarding, float dt, float max, float rateScale)
        {
            if (max <= 0f) max = GuardMax;
            if (meter > max) return max;
            if (dt <= 0f) return meter;
            float delay = guarding ? RegenGuardDelay : RegenIdleDelay;
            if (sinceLastBlock < delay) return meter;
            float rate = (guarding ? RegenGuardPerSecond : RegenIdlePerSecond) * Math.Max(0f, rateScale);
            return Math.Min(max, meter + rate * dt);
        }
    }
}
