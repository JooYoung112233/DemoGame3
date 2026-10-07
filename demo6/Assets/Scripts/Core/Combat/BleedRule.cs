namespace Demo6.Core.Combat
{
    /// <summary>
    /// 출혈(기획/전투-보스-무기-다듬기-1차.md 2-3 도끼, 결정 ② 6종). 도끼 ③ 쪼개기(ComboStep.bleedPercent = 50)가 건다.
    /// 3초 동안 0.5초마다 공격력 × (합 ÷ 6)%(50%면 8.33%), 합 50%. 겹치지 않고 새로 걸리면 시간만 다시 센다(남은 틱 6으로 되돌림).
    /// 치명·히트스톱·체력 흡수·연쇄 번개 없음. 보스 피해 능력치와 무너짐 받는 피해 +30%는 받는다. 버팀 피해 없음. 피 방울 자국을 남긴다.
    /// 계수에는 넣는다(ComboStep.PercentPerTarget = 타 × 배율 + 출혈 %): 도끼 1.53(안 넣으면 1.33으로 1.45~1.60 띠 밖).
    /// 피해 출처는 DamageSource.Bleed. 적 상태는 Game의 EnemyBleed(꾸러미 ⑥)가 들고 있다.
    /// 계약(꾸러미 ⑥이 채우고 BleedRuleTests로 지킨다).
    /// </summary>
    public static class BleedRule
    {
        public const float Duration = 3f;
        public const float TickInterval = 0.5f;
        public const int Ticks = 6;
        public const bool CanCrit = false;

        /// <summary>틱 한 번 배율(%) = 합 ÷ 틱 수. 50 → 8.333….</summary>
        public static double TickPercent(double totalPercent) => totalPercent / Ticks;
    }
}
