using System;

namespace Demo6.Core.Combat
{
    /// <summary>맞힌 자리(PositionalHitRule.Side). 등 뒤와 정면은 겹치지 않는다(등 뒤 ±60° = 120° 이상, 정면 ±45° = 45° 이하).</summary>
    public enum HitSide
    {
        /// <summary>옆, 또는 규칙이 듣지 않는 대상(둥지·허수아비)·규칙 꺼짐.</summary>
        None,
        /// <summary>적 등 뒤 ±60°(BackstabRule.IsBehind, 기습 처형·단검 등 찌르기와 같은 판정).</summary>
        Back,
        /// <summary>적 정면 ±45°.</summary>
        Front,
    }

    /// <summary>머리 위 글자(PositionalHitRule.TagOf).</summary>
    public enum HitTag
    {
        None,
        /// <summary>'백어택'(흐린 은색).</summary>
        Back,
        /// <summary>'헤드어택'(짙은 금색).</summary>
        Head,
    }

    /// <summary>
    /// 백어택·헤드어택(2026-10-04 사용자 요청 "백어택이면 백어택이라고 떠도 좋을듯? 앞에서 치명타는 헤드어택느낌으로 가도좋아보이네 전략적 느낌으로?").
    /// 백어택 = 적 등 뒤 ±60°(BackstabRule.IsBehind, 기습 처형·단검 등 찌르기와 같은 각도 함수)에서 맞힘: 피해 +20%, 치명 확률 +100‰(상한 500‰ 밖).
    /// 단검 등 찌르기(+400‰)와는 치명 보너스를 더하지 않고 큰 쪽 하나만 쓴다(그래서 단검은 등 뒤 치명 확률이 예전과 같고 피해 +20%만 붙는다).
    /// 헤드어택 = 적 정면 ±45°에서 터진 치명: 버팀 피해 × 1.5(치명 × 1.5, 회피 반격 × 2와 곱함). 버팀이 없거나 이미 무너진 적에게는 걸지 않는다(깎을 버팀이 없음).
    /// 둥지·허수아비는 듣지 않는다(TargetClass 규칙 반응 없음과 같음, 허수아비 초당 피해 측정을 흔들지 않음). 보스는 둘 다 듣는다.
    /// 글자는 처형이 난 타(기습 처형 즉사·무너짐, 무너짐 처형)에는 띄우지 않는다.
    /// 피해 +20%는 타 배율에 곱해 DamageMath.ToMonster에 넣는다(반올림은 ToMonster 안 한 번, 회피 반격 +20%와 같은 방식). 치명 연출 단계 무게(CritTiers.Of)에는 넣지 않는다.
    /// 숫자는 Tuning 손잡이(BackAttackOn·HeadAttackOn·BackAttackDamagePermille·BackAttackCritPermille·HeadAttackPoiseScale)로 바꾼다. 기본값은 여기 상수.
    /// 쓰는 곳: PlayerController(기본공격·회오리·검풍), WorldOverlay(글자). PositionalHitTests가 지킨다.
    /// </summary>
    public static class PositionalHitRule
    {
        public const bool BackDefaultOn = true;
        public const bool HeadDefaultOn = true;

        /// <summary>등 뒤 반각(도). 기습 처형·단검 등 찌르기와 같은 값.</summary>
        public const float BackHalfAngle = BackstabRule.HalfAngle;
        /// <summary>정면 반각(도).</summary>
        public const float FrontHalfAngle = 45f;

        /// <summary>백어택 피해 보너스(‰). 200 = +20%.</summary>
        public const int BackDamageBonusPermille = 200;
        /// <summary>백어택 치명 확률 보너스(‰, 상한 밖). 100 = +10%p.</summary>
        public const int BackCritBonusPermille = 100;
        /// <summary>헤드어택 버팀 피해 배율.</summary>
        public const float HeadPoiseScale = 1.5f;

        public const string BackWord = "백어택";
        public const string HeadWord = "헤드어택";

        /// <summary>정면인가: 적이 보는 방향과 (적 → 공격자) 사이 각이 반각 이하. 한쪽 길이가 0이면 정면이 아님.</summary>
        public static bool IsFront(float facingX, float facingY, float toAttackerX, float toAttackerY, float halfAngle = FrontHalfAngle)
        {
            if (toAttackerX * toAttackerX + toAttackerY * toAttackerY < 1e-12f) return false;
            if (facingX * facingX + facingY * facingY < 1e-12f) return false;
            return SectorMath.AngleBetween(facingX, facingY, toAttackerX, toAttackerY) <= halfAngle;
        }

        /// <summary>이 대상이 백어택·헤드어택을 듣는가. 둥지·허수아비는 아니고 보스는 듣는다.</summary>
        public static bool Applies(TargetClass target) => !target.Nest && !target.Dummy;

        /// <summary>
        /// 맞힌 자리. 적이 보는 방향(facing)과 적 → 공격자 방향(toAttacker)을 넣는다. 둥지·허수아비는 늘 None.
        /// backOn·headOn이 꺼진 쪽은 None으로 본다.
        /// </summary>
        public static HitSide Side(TargetClass target, float facingX, float facingY, float toAttackerX, float toAttackerY, bool backOn = BackDefaultOn, bool headOn = HeadDefaultOn)
        {
            if (!Applies(target)) return HitSide.None;
            if (backOn && BackstabRule.IsBehind(facingX, facingY, toAttackerX, toAttackerY, BackHalfAngle)) return HitSide.Back;
            if (headOn && IsFront(facingX, facingY, toAttackerX, toAttackerY)) return HitSide.Front;
            return HitSide.None;
        }

        /// <summary>
        /// 백어택 타 배율(%): 백어택이면 × (1 + 보너스‰ ÷ 1000), 아니면 그대로(곱하지 않아 예전과 비트까지 같음).
        /// 200‰면 정확히 × 1.2(회피 반격 CounterRule.DamageScale과 같은 double)다.
        /// </summary>
        public static double DamagePercent(double hitPercent, bool back, int bonusPermille = BackDamageBonusPermille) =>
            back && bonusPermille != 0 ? hitPercent * ((1000 + bonusPermille) / 1000.0) : hitPercent;

        /// <summary>치명 확률 보너스(‰, 상한 밖): 백어택 + backBonus, 단검 등 찌르기(등 뒤) +400. 둘 다면 큰 쪽 하나. 둘 다 아니면 0.</summary>
        public static int CritBonusPermille(bool back, bool daggerBehind, int backBonus = BackCritBonusPermille) =>
            Math.Max(back ? Math.Max(0, backBonus) : 0, daggerBehind ? BackstabRule.CritBonusPermille : 0);

        /// <summary>
        /// 치명 확률(‰): 보너스가 있으면 시트 값(상한 적용된 값, 음수는 0) + 보너스를 1000에서 자른다. 보너스가 없으면 시트 값 그대로.
        /// 단검만(백어택 아님)이면 BackstabRule.CritChancePermille와 같다.
        /// </summary>
        public static int CritChancePermille(int sheetChancePermille, bool back, bool daggerBehind, int backBonus = BackCritBonusPermille)
        {
            int bonus = CritBonusPermille(back, daggerBehind, backBonus);
            return bonus > 0 ? Math.Min(1000, Math.Max(0, sheetChancePermille) + bonus) : sheetChancePermille;
        }

        /// <summary>헤드어택인가: 정면에서 터진 치명이고, 버팀이 있으며 맞기 전에 무너져 있지 않은 대상.</summary>
        public static bool IsHeadAttack(HitSide side, bool crit, bool hasPoise, bool wasBroken) =>
            side == HitSide.Front && crit && hasPoise && !wasBroken;

        /// <summary>버팀 배율: 헤드어택이면 scale(0 이하면 1), 아니면 1.</summary>
        public static float PoiseScale(bool headAttack, float scale = HeadPoiseScale) => headAttack && scale > 0f ? scale : 1f;

        /// <summary>치명 버팀 배율(Enemy.TakeHit가 치명에 곱하는 1.5). 기대값 계산에만 쓴다.</summary>
        public const double CritPoise = 1.5;

        /// <summary>
        /// 정면 타의 기대 버팀 배율(헤드어택이 있을 때 ÷ 없을 때). 치명 확률 p = critPermille ÷ 1000:
        /// ((1 − p) + p × 1.5 × scale) ÷ ((1 − p) + p × 1.5). 치명 10% 1.07, 25% 1.17, 상한 50% 1.30.
        /// 보스 압박 무너짐(장검 초당 26, 12~15초에 한 번)이 정면에서 그만큼 빨라진다(3-4 무너뜨리기 창 확인용).
        /// </summary>
        public static double ExpectedFrontPoiseGain(int critPermille, float scale = HeadPoiseScale)
        {
            double p = Math.Min(1000, Math.Max(0, critPermille)) / 1000.0;
            double s = scale > 0f ? scale : 1.0;
            return ((1 - p) + p * CritPoise * s) / ((1 - p) + p * CritPoise);
        }

        /// <summary>
        /// 이 타의 글자. 처형이 난 타(executed: 기습 처형 즉사·멧돼지 바로 무너짐, 무너짐 처형)는 None.
        /// 헤드어택이 먼저(헤드어택은 늘 정면이라 백어택과 함께 나지 않음).
        /// </summary>
        public static HitTag TagOf(bool back, bool headAttack, bool executed)
        {
            if (executed) return HitTag.None;
            if (headAttack) return HitTag.Head;
            return back ? HitTag.Back : HitTag.None;
        }

        /// <summary>글자 내용(쉬운 한국어).</summary>
        public static string Word(HitTag tag) => tag == HitTag.Head ? HeadWord : tag == HitTag.Back ? BackWord : "";
    }
}
