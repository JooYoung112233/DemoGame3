namespace Demo6.Core.Dungeon
{
    /// <summary>
    /// 꾸러미의 보스 기록 규칙(기획/전투-보스-무기-다듬기-1차.md 3-8, 묶음 7). CarryData의 보스 칸(BossKills·BossKillExpedition·BossLosses·BossStakes)은
    /// 이 한 곳에서만 읽고 쓴다. 꾸러미가 null이면 아무것도 적지 않고 '처음'으로 본다(전투 시험장·다른 시험 장면).
    /// 재등장: 굴은 돌방이라 원정마다 보스가 다시 나온다. 같은 원정 안에서 이미 잡았으면(장면을 다시 지어도) 쓰러진 채로 둔다.
    /// 첫 처치(처치 수 0 → 1)만 첫 처치 보상이고, 그 뒤는 '다시 잡을 때' 보상이다(BossLoot).
    /// 승강장 고르기 '보스방 앞'은 굴 앞 말뚝을 켰고 아직 첫 처치 전일 때만 보인다.
    /// </summary>
    public static class BossLedger
    {
        /// <summary>이 보스를 잡은 수(영구).</summary>
        public static int Kills(CarryData c, string bossId) =>
            c != null && !string.IsNullOrEmpty(bossId) && c.BossKills.TryGetValue(bossId, out int n) ? n : 0;

        /// <summary>한 번이라도 잡았는가.</summary>
        public static bool Cleared(CarryData c, string bossId) => Kills(c, bossId) > 0;

        /// <summary>마지막으로 잡은 원정 번호(없으면 0).</summary>
        public static int LastKillExpedition(CarryData c, string bossId) =>
            c != null && !string.IsNullOrEmpty(bossId) && c.BossKillExpedition.TryGetValue(bossId, out int n) ? n : 0;

        /// <summary>이 원정에 이미 잡았는가.</summary>
        public static bool KilledThisExpedition(CarryData c, string bossId, int expedition) =>
            Cleared(c, bossId) && LastKillExpedition(c, bossId) == expedition;

        /// <summary>이번에 보스가 나오는가(원정마다 다시, 같은 원정 안에서는 한 번).</summary>
        public static bool Present(CarryData c, string bossId, int expedition) => !KilledThisExpedition(c, bossId, expedition);

        /// <summary>그 보스방에서 쓰러진 횟수.</summary>
        public static int Losses(CarryData c, string bossId) =>
            c != null && !string.IsNullOrEmpty(bossId) && c.BossLosses.TryGetValue(bossId, out int n) ? n : 0;

        /// <summary>처치를 적는다(처치 수 +1, 마지막 처치 원정). 첫 처치면 true. 꾸러미가 없으면 false.</summary>
        public static bool RecordKill(CarryData c, string bossId, int expedition)
        {
            if (c == null || string.IsNullOrEmpty(bossId)) return false;
            int before = Kills(c, bossId);
            c.BossKills[bossId] = before + 1;
            c.BossKillExpedition[bossId] = expedition;
            return before == 0;
        }

        /// <summary>보스방에서 쓰러짐을 적는다.</summary>
        public static void RecordLoss(CarryData c, string bossId)
        {
            if (c == null || string.IsNullOrEmpty(bossId)) return;
            c.BossLosses[bossId] = Losses(c, bossId) + 1;
        }

        /// <summary>이 보스방 앞 쉼터 말뚝을 켠 적이 있는가(영구).</summary>
        public static bool StakeLit(CarryData c, string stakeId) =>
            c != null && !string.IsNullOrEmpty(stakeId) && c.BossStakes.Contains(stakeId);

        /// <summary>쉼터 말뚝을 켰다고 적는다. 처음 적으면 true.</summary>
        public static bool LightStake(CarryData c, string stakeId) =>
            c != null && !string.IsNullOrEmpty(stakeId) && c.BossStakes.Add(stakeId);

        /// <summary>승강장 고르기에 '보스방 앞'(오우거 굴 쉼터)을 보이는가: 굴이 있고, 굴 앞 말뚝을 켰고, 첫 처치 전.</summary>
        public static bool FrontLandingOpen(CarryData c) =>
            OgreDen.InDungeon && StakeLit(c, OgreDen.FrontStakeId) && !Cleared(c, OgreDen.BossId);
    }
}
