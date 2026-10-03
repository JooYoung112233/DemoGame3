using System.Collections.Generic;

namespace Demo6.Core.Loot
{
    /// <summary>
    /// 장비 이름(장비 문서 5-4, 2026-10-03 결정 '등급 + 종류만'). 이름 = 등급 말 + 기본 이름(예: 일반 가죽 장갑, 희귀 핏빛 반지, 전설 대검).
    /// 꾸밈말·영웅 고유 이름·전설 칭호는 쓰지 않는다. 옵션은 이름이 아니라 카드의 옵션 줄로 읽고, 전설은 효과 이름을 카드 첫 옵션 줄(LegendaryLine)로 따로 보인다.
    /// 인물 이름은 쓰지 않는다. DisplayName 모양은 WeaponItem.DisplayName과 같다('희귀 대검').
    /// </summary>
    public static class GearNaming
    {
        static readonly string[] TierMarks = { "Ⅰ", "Ⅱ", "Ⅲ", "Ⅳ" };

        /// <summary>화면 이름 = 등급 + 기본 이름. 옵션·전설·강화와 무관하다.</summary>
        public static string DisplayName(GearItem item) => item == null ? "" : DisplayName(item.Grade, item.BaseId);

        /// <summary>등급과 종류 id로 화면 이름(모르는 id는 id 그대로 붙임).</summary>
        public static string DisplayName(Grade grade, string baseId) => GradeRules.Name(grade) + " " + BaseName(baseId);

        /// <summary>종류 id의 기본 이름(모르면 id 그대로).</summary>
        public static string BaseName(string baseId)
        {
            var b = GearBaseTable.Get(baseId);
            return b != null ? b.Name : baseId ?? "";
        }

        /// <summary>전설 효과 줄(예: '★ 연쇄 번개'). 전설이 아니면 빈 글.</summary>
        public static string LegendaryLine(GearItem item)
        {
            if (item == null || !item.IsLegendary) return "";
            return "★ " + LegendaryTable.Get(item.LegendaryId).Name;
        }

        /// <summary>옵션 줄 글(예: '+6.2% 공격력', '+25 공격력').</summary>
        public static string OptionLine(GearOption option) => OptionKinds.Format(option.Kind, option.Value);

        /// <summary>
        /// 카드 옵션 줄(굴린 차례). 전설이면 '★ 효과 이름'이 첫 줄이다(5-4). 단계 글자는 붙이지 않는다(쓸지는 15장에서 정함, TierMark).
        /// </summary>
        public static List<string> CardLines(GearItem item)
        {
            var lines = new List<string>();
            if (item == null) return lines;
            if (item.IsLegendary) lines.Add(LegendaryLine(item));
            foreach (var o in item.Options) lines.Add(OptionLine(o));
            return lines;
        }

        /// <summary>단계 글자 Ⅰ~Ⅳ(표시에 쓸지는 15장에서 정함).</summary>
        public static string TierMark(int tier) => TierMarks[System.Math.Max(1, System.Math.Min(4, tier)) - 1];
    }
}
