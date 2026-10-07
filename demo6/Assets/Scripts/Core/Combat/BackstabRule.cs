using System;

namespace Demo6.Core.Combat
{
    /// <summary>
    /// 등 각도 판정(기획/전투-보스-무기-다듬기-1차.md 2-3 단검 '등 찌르기', 4-2 [3] 기습 처형, 2026-10-04 백어택 PositionalHitRule). 세 규칙이 이 함수 하나를 함께 쓴다.
    /// 등 뒤 = 적이 보는 방향의 뒤쪽 120°(±60°) 안에서 맞힘. Game에서는 Enemy.FacingDirection과 (플레이어 − 적)을 넣는다.
    /// 단검 등 찌르기: 치명 확률 +400‰를 상한 500‰ 밖에서 더한다(그래서 최대 900‰). 잠든 적 기습(버팀 × 3)과 겹친다.
    /// 계약(꾸러미 ⑥ 무기 데이터가 소유하고 BackstabRuleTests로 지킨다. 꾸러미 ⑤가 PlayerController에서 쓴다). 계약 단계에서 식은 구현돼 있다.
    /// </summary>
    public static class BackstabRule
    {
        /// <summary>등 뒤 반각(도).</summary>
        public const float HalfAngle = 60f;
        /// <summary>등 찌르기 치명 확률 보너스(‰, 상한 밖).</summary>
        public const int CritBonusPermille = 400;

        /// <summary>등 뒤인가(적이 보는 방향, 적 → 공격자 방향).</summary>
        public static bool IsBehind(float facingX, float facingY, float toAttackerX, float toAttackerY, float halfAngle = HalfAngle) =>
            SectorMath.IsBehind(facingX, facingY, toAttackerX, toAttackerY, halfAngle);

        /// <summary>등 찌르기 치명 확률(‰): 등 뒤면 시트 값(상한 적용된 값) + 400, 1000으로 자름. 아니면 그대로.</summary>
        public static int CritChancePermille(int sheetChancePermille, bool behind) =>
            behind ? Math.Min(1000, Math.Max(0, sheetChancePermille) + CritBonusPermille) : sheetChancePermille;
    }
}
