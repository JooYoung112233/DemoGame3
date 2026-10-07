using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 층을 떠나며 바닥 장비를 거둔 결과 한 줄(시스템·컨텐츠 다듬기 검토 1차 Q3, Core BagSweep.ArrivalLine)을 다음 장면의 도착 글까지 들고 가는 쪽지.
    /// 장면을 바꿔도 살아남고(SceneStatics.Reset이 비우지 않음 — TownTravel 도착 쪽지와 같은 방식), 플레이를 새로 시작하면 비워진다.
    /// 계단·다시 짓기·옛 흐름 올라가기(마을 없음)는 다음 던전 장면의 BeginPlay가 꺼내 알림으로 띄우고(TakeForDungeon),
    /// 마을로 올라갔으면 마을 도착 카드가 꺼낸다(TakeForTown). 던전에 들어서면 남은 마을 몫은 버린다(늦게 뜨지 않게).
    /// </summary>
    public static class FloorSweepNote
    {
        /// <summary>아직 꺼내지 않은 글(없으면 null).</summary>
        public static string Line { get; private set; }
        /// <summary>마을 도착 카드 몫인가(바구니로 마을에 올라감).</summary>
        public static bool ToTown { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Clear();

        public static void Clear()
        {
            Line = null;
            ToTown = false;
        }

        /// <summary>떠나는 암전 뒤 DungeonRoot가 둔다. 글이 없으면(거둔 것도 두고 온 것도 없음) 지난 쪽지도 지운다.</summary>
        public static void Set(string line, bool toTown)
        {
            Line = string.IsNullOrEmpty(line) ? null : line;
            ToTown = Line != null && toTown;
        }

        /// <summary>마을 도착 카드가 꺼낸다(한 번만). 던전 몫이면 null.</summary>
        public static string TakeForTown()
        {
            if (Line == null || !ToTown) return null;
            var line = Line;
            Clear();
            return line;
        }

        /// <summary>던전 장면이 층에 들어설 때 꺼낸다(한 번만). 마을 몫이 남아 있으면 버리고 null.</summary>
        public static string TakeForDungeon()
        {
            var line = ToTown ? null : Line;
            Clear();
            return line;
        }
    }
}
