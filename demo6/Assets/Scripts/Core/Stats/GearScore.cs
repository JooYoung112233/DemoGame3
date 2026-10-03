using System;
using Demo6.Core.Loot;

namespace Demo6.Core.Stats
{
    /// <summary>비교 표시(장비 문서 8-5): 문턱 이상 초록 ▲, ±문턱 안 회색 =, −문턱 이하 빨강 ▼.</summary>
    public enum CompareMark
    {
        Down = -1,
        Same = 0,
        Up = 1,
    }

    /// <summary>판단 지수 셋(2차 6-6, 장비 문서 2-2): 공격 A, 생존 S, 이동 M.</summary>
    public readonly struct GearIndices
    {
        public readonly double A;
        public readonly double S;
        public readonly double M;

        public GearIndices(double a, double s, double m)
        {
            A = a;
            S = s;
            M = m;
        }
    }

    /// <summary>
    /// 장비 판단 값(장비 문서 2-2·8-5, 2차 6-6). 전설 효과는 점수에 넣지 않는다.
    /// A = 공격력 × (1 + 치명 확률 × (치명 피해 − 1)) × [0.65 × 콤보 계수 × (1 + 공격 속도) ÷ 1.49 + 0.35 × (1 + 스킬 피해) ÷ (1 − 재사용 감소)] × (1 + 0.2 × 보스 피해)
    /// S = 체력 × (1 + 방어 ÷ 1000) + 체력 흡수 × A × 10 + 초당 재생 × 10 + 처치 시 회복 × 5, M = 1 + 이동 속도.
    /// 종합 변화율 = 0.6 × (A'/A − 1) + 0.3 × (S'/S − 1) + 0.1 × (M'/M − 1).
    /// 계약(꾸러미 ①이 채우고 시험, ④가 카드·G 반지 고르기에서 씀).
    /// </summary>
    public static class GearScore
    {
        /// <summary>A 공식의 콤보 계수 기준(장검 단일 대상 계수, 치명 뺌).</summary>
        public const double ComboCoefficientBase = 1.49;

        public static GearIndices Of(StatSheet s)
        {
            if (s == null) return new GearIndices(1, 1, 1);
            var rule = s.WeaponRule;
            double coef = rule != null ? rule.SingleTargetCoefficient : ComboCoefficientBase;
            double crit = 1 + s.CritChancePermille / 1000.0 * (s.CritDamagePermille / 1000.0 - 1);
            double cdr = Math.Min(0.99, s.CooldownReductionPermille / 1000.0);
            double a = s.Attack * crit
                       * (0.65 * coef * (1 + s.AttackSpeedPermille / 1000.0) / ComboCoefficientBase + 0.35 * (1 + s.SkillDamagePermille / 1000.0) / (1 - cdr))
                       * (1 + 0.2 * s.BossDamagePermille / 1000.0);
            double sv = s.MaxHp * (1 + s.Defense / 1000.0) + s.LifeStealPermille / 1000.0 * a * 10 + s.HpRegen * 10.0 + s.OnKillHeal * 5.0;
            double m = 1 + s.MoveSpeedPermille / 1000.0;
            return new GearIndices(a, sv, m);
        }

        public static double Composite(GearIndices before, GearIndices after) =>
            0.6 * (Ratio(after.A, before.A) - 1) + 0.3 * (Ratio(after.S, before.S) - 1) + 0.1 * (Ratio(after.M, before.M) - 1);

        /// <summary>종합 변화율(‰, 반올림). 같은 시트면 0.</summary>
        public static int CompositePermille(StatSheet before, StatSheet after) => CompositePermille(Of(before), Of(after));

        /// <summary>종합 변화율(‰, 반올림, 0.5는 0에서 먼 쪽).</summary>
        public static int CompositePermille(GearIndices before, GearIndices after) =>
            (int)Math.Round(Composite(before, after) * 1000.0, MidpointRounding.AwayFromZero);

        /// <summary>
        /// 부위 문턱(GearSlots.CompareThresholdPermille: 무기 30, 갑옷 20, 나머지 10‰)으로 ▲ = ▼.
        /// 문턱 이상 ▲, 문턱 안(−문턱 &lt; 값 &lt; 문턱) =, −문턱 이하 ▼.
        /// </summary>
        public static CompareMark Mark(int compositePermille, GearPart part)
        {
            int t = GearSlots.CompareThresholdPermille(part);
            if (compositePermille >= t) return CompareMark.Up;
            return compositePermille <= -t ? CompareMark.Down : CompareMark.Same;
        }

        static double Ratio(double a, double b) => b > 0 ? a / b : 1;
    }
}
