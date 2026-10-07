using System;
using Demo6.Core.Dungeon;

namespace Demo6.Core.Town
{
    /// <summary>권양기에서 떠날 때 카드(TownNight.Departure).</summary>
    public readonly struct TownDeparture
    {
        public readonly string[] Lines;
        public readonly float Seconds;
        /// <summary>하룻밤을 지남(첫 출발은 없음).</summary>
        public readonly bool HasNight;
        /// <summary>첫 출발(원정 1): 던전 도착 종류 FirstStart, 그 밖은 Basket.</summary>
        public readonly bool FirstStart;

        public TownDeparture(string[] lines, float seconds, bool hasNight, bool firstStart)
        {
            Lines = lines ?? Array.Empty<string>();
            Seconds = seconds;
            HasNight = hasNight;
            FirstStart = firstStart;
        }
    }

    /// <summary>
    /// 마을 밤 카드 글과 길이(기획/마을-의뢰-첫판.md 1-7). 순서는 저녁 마을 → 하룻밤 → 갱도 입구 → 내려감(12장 결정 7 A).
    /// 올라갈 때(늘) "바구니가 덜컹이며 올라간다." 1.4초 / 첫 출발(원정 1, 밤 없음) "바구니가 덜컹이며 내려간다." 1.6초 /
    /// 첫 귀환 뒤 출발(첫 밤) "그날 밤, 아홉 해 만에 갱도가 울렸다." + "새벽, 바구니가 덜컹이며 내려간다."(가안) 2.5초 /
    /// 그 뒤 출발 "밤새 갱도가 울렸다." + 밤 사건 한 줄(매판 1차 2-2 글 그대로, NightCard.NightLines와 같은 글) 2.5초.
    /// 마을에서 carry.Night는 '오늘 밤 사건'이다. 원정 번호와 밤 사건은 올라갈 때 AdvanceForAscend 한 곳에서만 바꾸고 마을은 읽기만 한다.
    /// </summary>
    public static class TownNight
    {
        public const float AscendSeconds = 1.4f;
        public const float FirstDescentSeconds = 1.6f;
        public const float NightSeconds = 2.5f;

        public const string AscendLine = "바구니가 덜컹이며 올라간다.";
        public const string DescendLine = "바구니가 덜컹이며 내려간다.";
        public const string FirstNightLine = "그날 밤, 아홉 해 만에 갱도가 울렸다.";
        /// <summary>둘째 줄 가안.</summary>
        public const string FirstNightDawnLine = "새벽, 바구니가 덜컹이며 내려간다.";
        public const string NightLine = "밤새 갱도가 울렸다.";

        public const string CollapseLine = "새벽까지 흙 쏟아지는 소리가 길게 났다.";
        public const string RatBurrowLine = "아래서 긁는 소리가 그치지 않았다.";
        public const string UpheavalLine = "크게 울린 밤이다. 묻혔던 것이 올라왔을지 모른다.";
        /// <summary>거센 울림(1-2층 탐험 맛 1차 4-9): 다음 원정 다시 연 1·2층에 정예 무리 1.</summary>
        public const string RumbleLine = "땅 밑에서 큰 것이 몸을 뒤척였다.";

        /// <summary>밤 사건 글 전부(시험).</summary>
        public static readonly string[] EventLines = { CollapseLine, RatBurrowLine, UpheavalLine, RumbleLine };

        /// <summary>밤 사건 한 줄(무너짐·새 쥐굴·드러남·거센 울림, 그 밖은 null).</summary>
        public static string EventLine(NightEvent night)
        {
            switch (night)
            {
                case NightEvent.Collapse: return CollapseLine;
                case NightEvent.RatBurrow: return RatBurrowLine;
                case NightEvent.Upheaval: return UpheavalLine;
                case NightEvent.Rumble: return RumbleLine;
                default: return null;
            }
        }

        /// <summary>올라가는 카드 글(늘 같음, 1.4초).</summary>
        public static string[] AscendLines() => new[] { AscendLine };

        /// <summary>
        /// 권양기에서 떠날 때 카드(꾸러미를 바꾸지 않는다). 원정 1이면 첫 출발(밤 없음), 오늘 밤이 첫 밤이면 첫 밤 글, 그 밖은 밤 사건 글.
        /// </summary>
        public static TownDeparture Departure(CarryData carry)
        {
            if (carry == null || carry.Expedition <= 1)
                return new TownDeparture(new[] { DescendLine }, FirstDescentSeconds, false, true);
            if (carry.Night == NightEvent.FirstNight)
                return new TownDeparture(new[] { FirstNightLine, FirstNightDawnLine }, NightSeconds, true, false);
            string ev = EventLine(carry.Night);
            var lines = ev != null ? new[] { NightLine, ev } : new[] { NightLine };
            return new TownDeparture(lines, NightSeconds, true, false);
        }

        /// <summary>권양기 출발: town.departures +1 뒤 카드(1-3 단계 4: 가방·스킬을 꾸러미에 담은 다음 부른다).</summary>
        public static TownDeparture Depart(CarryData carry)
        {
            TownSave.BumpDepartures(carry);
            return Departure(carry);
        }

        /// <summary>지난밤 사건(주민의 "어젯밤에도 울리더라" 등): ExpeditionSeeds.NightBefore(소금, 원정 − 1).</summary>
        public static NightEvent LastNight(CarryData carry) =>
            carry == null ? NightEvent.None : ExpeditionSeeds.NightBefore(carry.ProfileSalt, carry.Expedition - 1);

        /// <summary>
        /// 바구니로 올라갈 때 원정 번호 +1과 오늘 밤 사건을 정한다(1-4 단계 2, 지금 DungeonRoot.AfterResults 계산 그대로).
        /// 올라가기 한 번에 한 번만 부른다. 마을 쪽 코드는 부르지 않는다(11장 위험 4).
        /// </summary>
        public static void AdvanceForAscend(CarryData carry)
        {
            if (carry == null) return;
            carry.Expedition++;
            carry.Night = ExpeditionSeeds.NightBefore(carry.ProfileSalt, carry.Expedition);
        }
    }
}
