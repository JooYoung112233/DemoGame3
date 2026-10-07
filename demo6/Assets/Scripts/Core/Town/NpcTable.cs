using System;
using System.Collections.Generic;

namespace Demo6.Core.Town
{
    /// <summary>주민 도형 임시 그림의 표식(3-1).</summary>
    public enum NpcMark
    {
        /// <summary>지팡이(가는 줄).</summary>
        Staff,
        /// <summary>망치(T자) + 앞치마.</summary>
        Hammer,
        /// <summary>긴 막대.</summary>
        Pole,
    }

    /// <summary>
    /// 주민 한 명(기획/마을-의뢰-첫판.md 3-1). Id = 내부 ID(코드·저장·로그에만), DisplayName = 플레이어에게 보이는 이름.
    /// 이름은 SpeakerIdentity를 거쳐서만 화면에 나간다(이름을 알기 전에는 '?'). Role = 역할 낱말(건너뛰기 요약 '갱도지기 춘삼'),
    /// Place = 자리 이름(이름을 모를 때 '경비 초소에 알리기', '? (경비 초소)'). 색은 도형 임시 그림 값(갈색·베이지·돌 회색 계열만).
    /// </summary>
    public sealed class NpcDef
    {
        public string Id = "";
        public string DisplayName = "";
        public string Role = "";
        public string Place = "";
        public NpcMark Mark;
        public string BodyHex = "";
        public string MarkHex = "";
        /// <summary>앞치마 색(옥금만, 없으면 null).</summary>
        public string ApronHex;

        public override string ToString() => Id;
    }

    /// <summary>
    /// 주민 표(3-1). 표시 이름·역할 낱말·자리 이름은 이 한 곳에만 둔다. 첫 판 주민 3명: 춘삼(갱도지기)·옥금(대장장이)·무진(옛 경비대장).
    /// </summary>
    public static class NpcTable
    {
        public const string Gate = "npc.gate";
        public const string Smith = "npc.smith";
        public const string Trainer = "npc.trainer";

        static readonly NpcDef[] Table =
        {
            new NpcDef { Id = Gate, DisplayName = "춘삼", Role = "갱도지기", Place = "권양기", Mark = NpcMark.Staff, BodyHex = "#6B5A48", MarkHex = "#BFB39A" },
            new NpcDef { Id = Smith, DisplayName = "옥금", Role = "대장장이", Place = "대장간", Mark = NpcMark.Hammer, BodyHex = "#4E3A2A", MarkHex = "#5C554C", ApronHex = "#8A6A44" },
            new NpcDef { Id = Trainer, DisplayName = "무진", Role = "옛 경비대장", Place = "경비 초소", Mark = NpcMark.Pole, BodyHex = "#45403A", MarkHex = "#A0957F" },
        };

        public static IReadOnlyList<NpcDef> All => Table;

        /// <summary>이 id의 주민(없으면 null).</summary>
        public static NpcDef Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var n in Table)
                if (string.Equals(n.Id, id, StringComparison.Ordinal)) return n;
            return null;
        }

        public static bool Exists(string id) => Get(id) != null;
    }
}
