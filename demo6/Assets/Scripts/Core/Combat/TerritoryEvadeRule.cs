namespace Demo6.Core.Combat
{
    /// <summary>
    /// 3차 초안 2-6 '치고 빠지기 꼼수는 막음': 깨어 있는 칸 무리는 경계 밖(문 너머)에서 오는 공격을 피하고('회피') 제자리로 돌아간다.
    /// 쉬는 무리를 밖에서 먼저 치는 기습은 그대로 들어간다. 싸우려면 칸 안으로 들어와야 한다.
    /// 이미 들어간 한 방의 뒷부분(followUp)은 피하지 않는다:
    /// 출혈 틱(도끼)과 처형(기습 처형·무너짐 처형, Enemy.Execute)이다. 기습 처형은 첫 타가 적을 깨운 직후 같은 판정 안에서 이어지므로,
    /// 긴 무기(대검 2.4·창 3.2·큰 낫 2.5)로 칸 밖에서 친 기습이 '회피'로 빠지던 문제를 막는다(기획/시스템-컨텐츠-다듬기-검토-1차.md 4장 Q5).
    /// 쓰는 곳: Enemy.TakeHit 한 곳.
    /// </summary>
    public static class TerritoryEvadeRule
    {
        /// <param name="aware">맞는 적이 깨어 있는가(쉬는 중이면 기습이라 피하지 않음).</param>
        /// <param name="hasTerritory">칸에 묶였는가(전투 시험장·궤짝 굴쥐처럼 칸이 없으면 피하지 않음).</param>
        /// <param name="attackerOutside">공격한 플레이어가 칸 경계 밖에 있는가(플레이어가 없으면 false).</param>
        /// <param name="followUp">이미 들어간 한 방의 뒷부분인가(출혈 틱, 처형).</param>
        /// <returns>이 타를 '회피'로 막고 제자리로 돌아가는가.</returns>
        public static bool Evades(bool aware, bool hasTerritory, bool attackerOutside, bool followUp) =>
            !followUp && aware && hasTerritory && attackerOutside;
    }
}
