namespace Demo6.Core.Combat
{
    /// <summary>
    /// 사슬 철퇴 관성(기획/전투-보스-무기-다듬기-1차.md 2-3, 결정 ② 6종): 마무리 동작이 끝난 뒤 0.5초 안에 다시 치면 ①② 단계(ComboStep.inertia)의
    /// 동작 길이 × 0.9(계수 1.46 → 1.55). 스킬(회오리·검풍)·구르기를 쓰면 관성이 사라진다. 판정 비율(hitMoment)은 그대로라 판정 순간도 같이 당겨진다.
    /// PlayerController(꾸러미 ⑤)가 StartSwing에서 SwingTiming.Plan(step, 공속, DurationScale(...))으로 쓰고, 그림(꾸러미 ⑦)은 PlayerController.InertiaActive를 본다.
    /// 계약(꾸러미 ⑥이 채우고 InertiaRuleTests로 지킨다). 계약 단계에서 식은 구현돼 있다.
    /// </summary>
    public static class InertiaRule
    {
        public const float Window = 0.5f;
        public const float DurationScale = 0.9f;

        /// <summary>관성이 붙는가: 관성 단계이고, 마무리 끝에서 Window 안이고, 그 사이 스킬·구르기를 쓰지 않았다.</summary>
        public static bool Active(bool inertiaStep, float sinceFinisherEnd, bool brokenBySkillOrDodge) =>
            inertiaStep && !brokenBySkillOrDodge && sinceFinisherEnd >= 0f && sinceFinisherEnd <= Window;

        /// <summary>동작 길이 배율(관성이면 0.9, 아니면 정확히 1).</summary>
        public static float Scale(bool inertiaStep, float sinceFinisherEnd, bool brokenBySkillOrDodge) =>
            Active(inertiaStep, sinceFinisherEnd, brokenBySkillOrDodge) ? DurationScale : 1f;
    }
}
