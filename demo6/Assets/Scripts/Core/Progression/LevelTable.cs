using System;

namespace Demo6.Core.Progression
{
    /// <summary>
    /// 캐릭터 레벨 표(3차 초안 4-3). 최대 Lv 20.
    /// 레벨 L → L+1 필요 경험치 = 335 × 1.16^(L − 1)을 10 단위로 반올림(0.5는 올림), L = 1~18. 마지막 칸(19 → 20)은 4,500으로 고정.
    /// 이 규칙으로 누계가 기획 표와 정확히 같다: Lv 5 1,700 / Lv 10 5,880 / Lv 15 14,640 / Lv 20 32,700.
    /// (식 그대로면 마지막 칸이 4,840이라 누계 33,040이 되므로, 기획대로 마지막 칸만 4,500으로 바꾼다.)
    /// </summary>
    public static class LevelTable
    {
        public const int MaxLevel = 20;
        public const double StepBase = 335.0;
        public const double StepGrowth = 1.16;
        public const int LastStep = 4500;

        /// <summary>Steps[L] = 레벨 L에서 L+1로 가는 필요 경험치(L = 1~19). Steps[0]은 쓰지 않는다.</summary>
        static readonly int[] Steps = BuildSteps();
        /// <summary>Totals[L] = 레벨 L이 되는 누계 경험치(Totals[1] = 0).</summary>
        static readonly int[] Totals = BuildTotals();

        static int[] BuildSteps()
        {
            var steps = new int[MaxLevel];
            for (int level = 1; level <= MaxLevel - 2; level++)
                steps[level] = RoundToTen(StepBase * Math.Pow(StepGrowth, level - 1));
            steps[MaxLevel - 1] = LastStep;
            return steps;
        }

        static int[] BuildTotals()
        {
            var totals = new int[MaxLevel + 1];
            for (int level = 2; level <= MaxLevel; level++)
                totals[level] = totals[level - 1] + Steps[level - 1];
            return totals;
        }

        /// <summary>10 단위 반올림(0.5는 올림). 335 → 340.</summary>
        public static int RoundToTen(double value) => (int)Math.Floor(value / 10.0 + 0.5 + 1e-9) * 10;

        static int Clamp(int level) => Math.Max(1, Math.Min(MaxLevel, level));

        /// <summary>레벨 level에서 다음 레벨까지 필요 경험치. 최대 레벨이면 0.</summary>
        public static int Required(int level)
        {
            int l = Clamp(level);
            return l >= MaxLevel ? 0 : Steps[l];
        }

        /// <summary>레벨 level이 되는 데 필요한 누계 경험치(Lv 1 = 0).</summary>
        public static int Cumulative(int level) => Totals[Clamp(level)];

        /// <summary>누계 경험치로 도달한 레벨.</summary>
        public static int LevelFor(int totalXp)
        {
            int level = 1;
            while (level < MaxLevel && totalXp >= Totals[level + 1]) level++;
            return level;
        }
    }

    /// <summary>
    /// 레벨 체력(3차 초안 4-3·4-5, 계약서 '레벨 체력'): 최대 체력 = 장비 기준 체력 + (120 + 15 × 질긴 몸 랭크) × (레벨 − 1).
    /// 레벨은 공격력을 주지 않는다. 질긴 몸은 지난 레벨에도 소급한다.
    /// </summary>
    public static class LevelHp
    {
        /// <summary>1층 장비 기준 체력(2차 4-8 표 2,400).</summary>
        public const int BaseHp = 2400;
        public const int PerLevel = 120;
        /// <summary>질긴 몸 랭크당 레벨당 체력 +15(4랭크면 +180).</summary>
        public const int ToughPerRank = 15;

        /// <summary>레벨당 오르는 체력(질긴 몸 랭크 반영).</summary>
        public static int PerLevelWith(int toughRank) => PerLevel + ToughPerRank * Math.Max(0, Math.Min(SkillDef.MaxRank, toughRank));

        public static int MaxHp(int level, int toughRank, int baseHp = BaseHp)
        {
            int l = Math.Max(1, Math.Min(LevelTable.MaxLevel, level));
            return baseHp + PerLevelWith(toughRank) * (l - 1);
        }
    }
}
