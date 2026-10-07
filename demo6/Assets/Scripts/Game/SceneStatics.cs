namespace Demo6.Game
{
    /// <summary>
    /// 장면 정적 상태 비우기 한 곳(기획/마을-의뢰-첫판.md 9-2 '장면을 바꿀 때 무엇이 어디서 풀리나', 11장 위험 2).
    /// 도메인 다시 불러오기가 꺼져 있어 정적 값이 플레이·장면을 넘어 남는다. 새 장면 루트(마을 TownRoot, 던전 DungeonRoot)가 Awake 첫 줄에서 부른다.
    /// 비우는 것: 던전 루트가 비우던 목록(적·공격 차례·전투 사건·시간 멈춤·창·입력 막힘·상호작용·그림 모드·던전 사건·재질·시야·분위기·소리·피·정수리 시점)
    /// + 마을 정적(TownRoot.Instance). 그래서 던전을 떠날 때 건 멈춤·입력 막힘·밤 카드 창은 마을의 이 함수가 풀고, 마을 → 던전에서는 던전 쪽이 마을 정적까지 비운다.
    /// 남기는 것(각자 플레이 시작 SubsystemRegistration 때만 비움): 꾸러미(ProfileCarry Data·Trip), 도착 쪽지(TownTravel), 장면 불러오기 시각(SceneTravel),
    /// 밤 카드가 이어 보여 줄 값(NightCard), F1 '오우거 굴 있음 가정'(TownRoot.OgreDenAssumed).
    /// 던전 쪽 목록과 갈라지지 않게 DungeonRoot.ResetStatics도 이 함수를 부르도록 바꾼다(꾸러미 4). 그 전까지는 아래 목록을 DungeonRoot.ResetStatics와 같게 둔다.
    /// </summary>
    public static class SceneStatics
    {
        public static void Reset()
        {
            // ── 던전 루트 목록(DungeonRoot.ResetStatics와 같은 차례) ──
            Enemy.ResetStatics();
            AttackTokens.ResetStatics();
            StrongAttackSchedule.ResetStatics();
            CombatEvents.ResetStatics();
            TimeScaleService.ResetStatics();
            CombatHud.ResetStatics();
            ArtRuntime.ResetStatics();
            DungeonEvents.ResetStatics();
            Interactable.ResetStatics();
            DungeonUi.ResetStatics();
            RenderMaterials.ResetStatics();
            VisionSystem.ResetStatics();
            DungeonAtmosphere.ResetStatics();
            DungeonAudio.ResetStatics();
            GoreSystem.ResetStatics();
            TopDownView.ResetStatics();
            // 오우거 굴(묶음 7): 보스방과 어둠 위 예고 테두리.
            BossArena.ResetStatics();
            Telegraph.AboveDark = false;

            // ── 마을 ──
            TownRoot.ResetStatics();
        }
    }
}
