using System;
using Demo6.Core.Combat;
using Demo6.Core.Random;

namespace Demo6.Core.Loot
{
    /// <summary>장비 등급(2차 6-2). 값이 클수록 높은 등급이다.</summary>
    public enum Grade
    {
        /// <summary>일반(회색).</summary>
        Common,
        /// <summary>고급(초록).</summary>
        Uncommon,
        /// <summary>희귀(파랑).</summary>
        Rare,
        /// <summary>영웅(보라).</summary>
        Epic,
        /// <summary>전설(주황).</summary>
        Legendary,
    }

    /// <summary>
    /// 등급표(2차 6-2)와 층별 등급 확률(2차 7-2, ‰). 배율은 ‰ 정수로 두고 곱셈은 GearMath가 한다.
    /// 3차 초안 4-6 '그대로': 등급표·등급 확률(층 기준)은 2차 값을 쓴다.
    /// </summary>
    public static class GradeRules
    {
        public const int Count = 5;

        static readonly string[] Names = { "일반", "고급", "희귀", "영웅", "전설" };

        /// <summary>2차 6-2 기본 능력치 배율(‰): ×1.00 / ×1.25 / ×1.55 / ×1.90 / ×2.30.</summary>
        static readonly int[] MultiplierPermilles = { 1000, 1250, 1550, 1900, 2300 };

        /// <summary>2차 6-2 등급색(#RRGGBB): 회색 / 초록 / 파랑 / 보라 / 주황.</summary>
        static readonly int[] ColorRgb = { 0x9E9E9E, 0x4CAF50, 0x2F80ED, 0x9B51E0, 0xF2994A };

        /// <summary>2차 7-2 일반 몬스터·상자·정예의 등급 확률(‰). [층 − 1, 등급]. 전설은 5층부터.</summary>
        static readonly int[,] FloorTable =
        {
            { 820, 160, 20, 0, 0 },
            { 740, 210, 50, 0, 0 },
            { 660, 250, 80, 10, 0 },
            { 580, 280, 120, 20, 0 },
            { 500, 300, 155, 40, 5 },
            { 440, 310, 180, 60, 10 },
            { 380, 320, 210, 75, 15 },
            { 320, 330, 240, 90, 20 },
            { 270, 330, 260, 115, 25 },
            { 220, 330, 280, 140, 30 },
        };

        /// <summary>표에 있는 층 수(1~10층).</summary>
        public static int TableFloors => FloorTable.GetLength(0);

        public static string Name(Grade grade) => Names[Index(grade)];

        /// <summary>기본 능력치 등급 배율(‰).</summary>
        public static int MultiplierPermille(Grade grade) => MultiplierPermilles[Index(grade)];

        /// <summary>등급색 #RRGGBB 정수(Core는 UnityEngine을 모른다. 화면 쪽은 DungeonUi.GradeColor를 쓴다).</summary>
        public static int Rgb(Grade grade) => ColorRgb[Index(grade)];

        /// <summary>그 층에서 이 등급이 나올 확률(‰). 1층 밑·10층 위는 끝 층 값.</summary>
        public static int FloorPermille(int floor, Grade grade) => FloorTable[FloorScaling.Clamp(floor) - 1, Index(grade)];

        /// <summary>그 층 한 줄(복사본). 합은 1000.</summary>
        public static int[] FloorRow(int floor)
        {
            int row = FloorScaling.Clamp(floor) - 1;
            var result = new int[Count];
            for (int g = 0; g < Count; g++) result[g] = FloorTable[row, g];
            return result;
        }

        /// <summary>층 표대로 등급 하나를 굴린다(난수 1번).</summary>
        public static Grade Roll(int floor, IRandom rng) => RollAtLeast(floor, Grade.Common, rng);

        /// <summary>
        /// min 이상 등급만 남긴 표에서 굴린다(난수 1번). 쇠 궤짝 '희귀 이상 무기' 보장(3차 초안 2-5, 2차 7-4)에 쓴다.
        /// 남은 칸이 모두 0이면 min을 돌려준다.
        /// </summary>
        public static Grade RollAtLeast(int floor, Grade min, IRandom rng)
        {
            int row = FloorScaling.Clamp(floor) - 1;
            int start = Index(min);
            int total = 0;
            for (int g = start; g < Count; g++) total += FloorTable[row, g];
            if (total <= 0) return min;
            int r = rng.NextInt(0, total);
            for (int g = start; g < Count; g++)
            {
                r -= FloorTable[row, g];
                if (r < 0) return (Grade)g;
            }
            return (Grade)(Count - 1);
        }

        static int Index(Grade grade) => Math.Max(0, Math.Min(Count - 1, (int)grade));
    }
}
