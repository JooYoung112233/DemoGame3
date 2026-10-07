namespace Demo6.Game
{
    /// <summary>
    /// 지갑(재화 쓸 곳 1차 11-6): 강화석·골드를 읽고 쓰는 한 곳. 던전이면 DungeonRoot.State, 마을이면 꾸러미(ProfileCarry.Ensure())다.
    /// 마을 = DungeonRoot.Instance가 없음(마을·전투 시험장). 마을에서는 꾸러미가 지갑의 원본이라 바꾸면 바로 꾸러미에 남는다.
    /// 정적 값을 들고 있지 않아(읽을 때마다 원본을 찾음) 장면을 바꿔도 비울 것이 없다.
    /// </summary>
    public static class Wallet
    {
        /// <summary>마을인가(DungeonRoot.Instance가 없음).</summary>
        public static bool InTown => DungeonRoot.Instance == null;

        /// <summary>지금 강화석(던전 State, 마을 꾸러미).</summary>
        public static int Stones
        {
            get
            {
                var state = State;
                return state != null ? state.Stones : ProfileCarry.Ensure().Stones;
            }
        }

        /// <summary>지금 골드(던전 State, 마을 꾸러미).</summary>
        public static int Gold
        {
            get
            {
                var state = State;
                return state != null ? state.Gold : ProfileCarry.Ensure().Gold;
            }
        }

        /// <summary>강화석 n개를 쓴다(강화 시도, 2-4). 모자라거나 n이 음수면 아무것도 바꾸지 않고 false. 0이면 쓰는 것 없이 true.</summary>
        public static bool TrySpendStones(int n)
        {
            if (n < 0) return false;
            if (n == 0) return true;
            var state = State;
            if (state != null)
            {
                if (state.Stones < n) return false;
                state.Stones -= n;
                return true;
            }
            var carry = ProfileCarry.Ensure();
            if (carry.Stones < n) return false;
            carry.Stones -= n;
            return true;
        }

        /// <summary>
        /// 분해로 얻은 강화석이 들어오는 유일한 입구(4-1: 가방 분해·모루 분해·저절로 분해·떠날 때 거두기 분해).
        /// 묶음 5(D1 주머니)는 원정 강화석에서 '던전 안 분해 강화석'을 빼고 셀 때 여기서 따로 센다(10장). 다른 길로 분해 강화석을 넣지 않는다.
        /// </summary>
        public static void AddSalvageStones(int n)
        {
            if (n <= 0) return;
            var state = State;
            if (state != null) state.Stones += n;
            else ProfileCarry.Ensure().Stones += n;
        }

        /// <summary>던전 상태(마을이면 null).</summary>
        static DungeonState State
        {
            get
            {
                var root = DungeonRoot.Instance;
                return root != null ? root.State : null;
            }
        }
    }
}
