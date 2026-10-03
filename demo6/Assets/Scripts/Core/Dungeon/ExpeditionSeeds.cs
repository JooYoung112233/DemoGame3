namespace Demo6.Core.Dungeon
{
    /// <summary>
    /// 원정 앞 하룻밤의 사건(매판 새 탐험 1차 2-2 밤 카드, 2-5 차례 10). 생성기 입력이자 밤 카드 글의 열쇠다.
    /// </summary>
    public enum NightEvent
    {
        /// <summary>밤이 없었다(첫 원정).</summary>
        None,
        /// <summary>첫 귀환의 밤: 아홉 해 만에 갱도가 울림. 값 조정 없음.</summary>
        FirstNight,
        /// <summary>무너짐: 막다른 곳 +1.</summary>
        Collapse,
        /// <summary>새 쥐굴: 둥지 +1, 통로 +1.</summary>
        RatBurrow,
        /// <summary>드러남: 쇠 궤짝 +1.</summary>
        Upheaval,
    }

    /// <summary>
    /// 씨앗(같은 값이면 같은 지도가 나오는 시작 수, 2-5 '씨앗'). 섞기(프로필 소금, 층, 원정 번호).
    /// 층을 처음 밟는 원정은 층 예산 표의 고른 씨앗(1층은 0 = 손 지도), 그 뒤는 원정 번호로 정해지는 새 씨앗.
    /// 같은 원정 안에서는 같은 층이 같은 지도다. Pcg32Random 흐름은 지도·내용물·장식 셋으로 나눠 서로의 난수 순서를 밀지 않게 한다.
    /// </summary>
    public static class ExpeditionSeeds
    {
        /// <summary>Pcg32Random 흐름 번호: ① 지도(칸·길) ② 내용물(자리 슬롯 고르기) ③ 장식(기둥 모양 등).</summary>
        public const ulong MapStream = 1;
        public const ulong ContentStream = 2;
        public const ulong DecorStream = 3;

        /// <summary>이 원정에서 이 층을 지을 씨앗. 처음 밟는 층이면 고른 씨앗.</summary>
        public static ulong Choose(FloorRecipe recipe, bool firstVisit, ulong profileSalt, int expedition)
        {
            if (recipe != null && firstVisit) return recipe.ChosenSeed;
            int floor = recipe != null ? recipe.Floor : 1;
            return ForExpedition(profileSalt, floor, expedition);
        }

        /// <summary>다시 연 층의 씨앗. 0(1층 손 지도)은 나오지 않는다.</summary>
        public static ulong ForExpedition(ulong profileSalt, int floor, int expedition)
        {
            unchecked
            {
                ulong s = Mix(Mix(profileSalt ^ 0xA0761D6478BD642FUL) ^ (ulong)(uint)floor * 0xE7037ED1A0B428DBUL) ^ (ulong)(uint)expedition * 0x8EBC6AF09C88C6E3UL;
                s = Mix(s);
                return s == 0UL ? 1UL : s;
            }
        }

        /// <summary>
        /// 이 원정 앞의 밤. 원정 1은 밤이 없고(None), 원정 2는 첫 귀환의 밤(FirstNight), 그 뒤는 무너짐·새 쥐굴·드러남 가운데 하나.
        /// </summary>
        public static NightEvent NightBefore(ulong profileSalt, int expedition)
        {
            if (expedition <= 1) return NightEvent.None;
            if (expedition == 2) return NightEvent.FirstNight;
            ulong r = Mix(profileSalt ^ 0x9E3779B97F4A7C15UL ^ unchecked((ulong)(uint)expedition * 0xD1B54A32D192ED03UL));
            switch ((int)(r % 3UL))
            {
                case 0: return NightEvent.Collapse;
                case 1: return NightEvent.RatBurrow;
                default: return NightEvent.Upheaval;
            }
        }

        /// <summary>SplitMix64 한 걸음(값을 고르게 섞음).</summary>
        public static ulong Mix(ulong x)
        {
            unchecked
            {
                x += 0x9E3779B97F4A7C15UL;
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                return x ^ (x >> 31);
            }
        }
    }
}
