using System;
using System.Collections.Generic;
using System.Globalization;

namespace Demo6.Core.Progression
{
    /// <summary>스킬 1줄 4갈래(3차 초안 4-5). 2줄은 M0b에서 뺀다(7-2).</summary>
    public enum SkillId
    {
        /// <summary>회오리 갈래: 넓은 회오리.</summary>
        WideWhirl,
        /// <summary>검풍 갈래: 날 선 바람.</summary>
        SharpWind,
        /// <summary>마무리 갈래: 마무리 일격.</summary>
        Finisher,
        /// <summary>몸 갈래: 질긴 몸.</summary>
        ToughBody,
    }

    /// <summary>
    /// 스킬 칸 하나. 효과 값 = 기본값 + 랭크당 값 × 랭크. 랭크 0이면 M0a 그대로(4-5 '스킬 0점 = M0a 그대로').
    /// </summary>
    public sealed class SkillDef
    {
        public const int MaxRank = 4;
        /// <summary>1줄은 Lv 2부터(4-5).</summary>
        public const int RequiredLevel = 2;

        public readonly SkillId Id;
        /// <summary>문자열 id(시험 패널·기록용).</summary>
        public readonly string Key;
        /// <summary>갈래 이름(회오리, 검풍, 마무리, 몸).</summary>
        public readonly string Branch;
        public readonly string Name;
        /// <summary>랭크 0 값(M0a 값).</summary>
        public readonly double BaseValue;
        public readonly double PerRank;
        /// <summary>효과 문구 틀. {0}에 값이 들어간다.</summary>
        public readonly string Format;

        public SkillDef(SkillId id, string key, string branch, string name, double baseValue, double perRank, string format)
        {
            Id = id;
            Key = key;
            Branch = branch;
            Name = name;
            BaseValue = baseValue;
            PerRank = perRank;
            Format = format;
        }

        public static int ClampRank(int rank) => Math.Max(0, Math.Min(MaxRank, rank));

        /// <summary>랭크에서 더해지는 값(랭크 0이면 0).</summary>
        public double BonusAt(int rank) => PerRank * ClampRank(rank);

        /// <summary>랭크에서의 효과 값(기본값 + 더해지는 값).</summary>
        public double ValueAt(int rank) => BaseValue + BonusAt(rank);

        /// <summary>화면에 쓸 효과 문구(예: '회오리 반경 3.0').</summary>
        public string EffectText(int rank) => string.Format(CultureInfo.InvariantCulture, Format, ValueAt(rank));

        /// <summary>랭크당 효과 설명(예: '+0.2/랭크').</summary>
        public string PerRankText => Id switch
        {
            SkillId.WideWhirl => "랭크당 반경 +0.2",
            SkillId.SharpWind => "랭크당 +25%p",
            SkillId.Finisher => "랭크당 마무리 피해 +6%",
            _ => "랭크당 레벨당 체력 +15 (지난 레벨에도)",
        };
    }

    /// <summary>
    /// 스킬 1줄 표(3차 초안 4-5):
    /// 넓은 회오리 반경 2.6 → 3.4(+0.2/랭크), 날 선 바람 검풍 350 → 450%(+25%p/랭크),
    /// 마무리 일격 마무리 피해 +6%/랭크(4랭크 +24%), 질긴 몸 레벨당 체력 +120 → +180(+15/랭크, 지난 레벨에도).
    /// </summary>
    public static class SkillTree
    {
        public static readonly SkillDef WideWhirl =
            new SkillDef(SkillId.WideWhirl, "skill.wide_whirl", "회오리", "넓은 회오리", 2.6, 0.2, "회오리 반경 {0:0.0}");
        public static readonly SkillDef SharpWind =
            new SkillDef(SkillId.SharpWind, "skill.sharp_wind", "검풍", "날 선 바람", 350.0, 25.0, "검풍 피해 {0:0}%");
        public static readonly SkillDef Finisher =
            new SkillDef(SkillId.Finisher, "skill.finisher", "마무리", "마무리 일격", 0.0, 6.0, "마무리 피해 +{0:0}%");
        public static readonly SkillDef ToughBody =
            new SkillDef(SkillId.ToughBody, "skill.tough_body", "몸", "질긴 몸", LevelHp.PerLevel, LevelHp.ToughPerRank, "레벨당 체력 +{0:0}");

        /// <summary>표시 순서(회오리, 검풍, 마무리, 몸).</summary>
        public static readonly IReadOnlyList<SkillDef> All = new[] { WideWhirl, SharpWind, Finisher, ToughBody };

        public const int Count = 4;

        public static SkillDef Get(SkillId id)
        {
            switch (id)
            {
                case SkillId.WideWhirl: return WideWhirl;
                case SkillId.SharpWind: return SharpWind;
                case SkillId.Finisher: return Finisher;
                case SkillId.ToughBody: return ToughBody;
                default: throw new ArgumentOutOfRangeException(nameof(id));
            }
        }

        /// <summary>문자열 id로 찾는다. 없으면 null.</summary>
        public static SkillDef Find(string key)
        {
            foreach (var d in All)
                if (d.Key == key) return d;
            return null;
        }

        /// <summary>
        /// 랭크를 올릴 수 있는가: 점수 1점 이상, 레벨 2 이상, 4랭크 미만.
        /// </summary>
        public static bool CanRankUp(int rank, int level, int skillPoints) =>
            skillPoints > 0 && level >= SkillDef.RequiredLevel && rank < SkillDef.MaxRank;

        /// <summary>회오리 반경에 더할 값(랭크당 0.2유닛).</summary>
        public static double WhirlRadiusBonus(int rank) => WideWhirl.BonusAt(rank);

        /// <summary>검풍 배율에 더할 %p(랭크당 25).</summary>
        public static double WavePercentBonus(int rank) => SharpWind.BonusAt(rank);

        /// <summary>마무리 피해에 곱해 더할 비율(랭크당 0.06 = +6%).</summary>
        public static double FinisherDamageBonus(int rank) => Finisher.BonusAt(rank) / 100.0;

        /// <summary>레벨당 체력(랭크 0 = 120, 4 = 180).</summary>
        public static int HpPerLevel(int rank) => LevelHp.PerLevelWith(rank);
    }
}
