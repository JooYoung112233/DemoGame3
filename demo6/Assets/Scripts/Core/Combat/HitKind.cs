namespace Demo6.Core.Combat
{
    /// <summary>
    /// 적 공격 종류(기획/세-무기-우클릭-소켓-1차.md 2-7). 방패 막기·패링 표(ShieldRule)가 이 값으로 갈린다.
    /// 종류를 넘기지 않는 옛 호출(PlayerController.ReceiveHit 기본값)은 Melee다.
    /// 멧돼지 돌진은 무기 행동 Charge(기 모으기)와 헷갈리지 않게 Rush, 오우거 돌진은 BossRush로 부른다(0-3의 2).
    /// </summary>
    public enum HitKind
    {
        /// <summary>근접(굴쥐 물기, 멧돼지 머리치기·뒷발차기).</summary>
        Melee,
        /// <summary>멧돼지 돌진(진행 방향 기준 앞 반원).</summary>
        Rush,
        /// <summary>보통 화살.</summary>
        Arrow,
        /// <summary>꿰뚫는 화살.</summary>
        PierceArrow,
        /// <summary>오우거 휩쓸기.</summary>
        BossSweep,
        /// <summary>오우거 내려찍기(앞이면 막지만 늘 깨짐, 패링 안 됨).</summary>
        BossSlam,
        /// <summary>오우거 돌진(진행 방향 기준, 패링 안 됨).</summary>
        BossRush,
        /// <summary>위에서 떨어짐(낙석). 못 막음.</summary>
        FromAbove,
        /// <summary>덫(가시 덫). 못 막음.</summary>
        Trap,
    }

    /// <summary>
    /// PlayerController.ReceiveHit 한 번의 결과(LastHitResult, 2-7). 반환값은 Hit·Blocked면 true, Parried·Avoided면 false.
    /// None은 아직 아무 공격도 받지 않은 처음 값이다.
    /// </summary>
    public enum HitResult
    {
        None,
        /// <summary>맞음(막지 못함).</summary>
        Hit,
        /// <summary>방패로 막음(피해·밀림 줄임).</summary>
        Blocked,
        /// <summary>패링(튕겨 냄, 피해·밀림 0).</summary>
        Parried,
        /// <summary>피함(구르기 무적·피격 무적·쓰러짐).</summary>
        Avoided,
    }
}
