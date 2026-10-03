using System;
using System.Collections.Generic;
using Demo6.Core.Random;

namespace Demo6.Core.Loot
{
    /// <summary>
    /// 옵션·고유 능력치 종류 15가지(장비 문서 5-3 '종류는 15가지 그대로'). 이름은 꾸러미 글에 그대로 쓰므로 바꾸지 않는다(뒤에 더하기만).
    /// % 종류는 ‰ 정수, 고정값 종류는 10배 단위 정수로 저장한다.
    /// </summary>
    public enum OptionKind
    {
        /// <summary>공격력+ (고정).</summary>
        AttackFlat,
        /// <summary>공격력% (‰).</summary>
        AttackPercent,
        /// <summary>치명타 확률 (‰).</summary>
        CritChance,
        /// <summary>치명타 피해 (‰).</summary>
        CritDamage,
        /// <summary>공격 속도 (‰, 기본공격 동작 길이를 1 + 이 값으로 나눔).</summary>
        AttackSpeed,
        /// <summary>보스 피해 (‰).</summary>
        BossDamage,
        /// <summary>체력 흡수 (‰, 기본공격·스킬 피해만).</summary>
        LifeSteal,
        /// <summary>처치 시 체력 회복 (고정).</summary>
        OnKillHeal,
        /// <summary>체력+ (고정).</summary>
        HpFlat,
        /// <summary>체력% (‰).</summary>
        HpPercent,
        /// <summary>방어+ (고정).</summary>
        DefenseFlat,
        /// <summary>스킬 재사용 감소 (‰).</summary>
        CooldownReduction,
        /// <summary>스킬 피해 (‰).</summary>
        SkillDamage,
        /// <summary>이동 속도 (‰, 전투 걸음만).</summary>
        MoveSpeed,
        /// <summary>초당 체력 재생 (고정).</summary>
        HpRegen,
    }

    /// <summary>옵션 줄 하나(장비 문서 5-2 저장: 종류, 값, 단계). 값은 등급·iLv 배율을 이미 곱한 최종 정수다.</summary>
    public readonly struct GearOption : IEquatable<GearOption>
    {
        public readonly OptionKind Kind;
        /// <summary>% 종류는 ‰, 고정값 종류는 10배 단위 정수.</summary>
        public readonly int Value;
        /// <summary>단계 Ⅰ~Ⅳ(1~4): 굴린 값이 범위의 몇 번째 1/4에 들었는가(5-4, 표시용).</summary>
        public readonly int Tier;

        public GearOption(OptionKind kind, int value, int tier)
        {
            Kind = kind;
            Value = value;
            Tier = Math.Max(1, Math.Min(4, tier));
        }

        public bool Equals(GearOption other) => Kind == other.Kind && Value == other.Value && Tier == other.Tier;
        public override bool Equals(object obj) => obj is GearOption o && Equals(o);
        public override int GetHashCode() => ((int)Kind * 397 ^ Value) * 31 + Tier;
        public override string ToString() => OptionKinds.Format(Kind, Value);
    }

    /// <summary>종류 고유 능력치 한 줄(장비 문서 4-3 '종류 고유 (고정)'). iLv·등급·강화와 무관.</summary>
    public readonly struct StatBonus
    {
        public readonly OptionKind Kind;
        public readonly int Value;

        public StatBonus(OptionKind kind, int value)
        {
            Kind = kind;
            Value = value;
        }
    }

    /// <summary>옵션 풀 한 줄(장비 문서 5-3): 부위, 종류, 고급·iLv1 기준 범위, iLv 배율 적용 여부, 가중치.</summary>
    public readonly struct OptionRule
    {
        public readonly GearPart Part;
        public readonly OptionKind Kind;
        public readonly int Min;
        public readonly int Max;
        /// <summary>고정값 옵션은 굴릴 때 iLv 배율도 곱한다.</summary>
        public readonly bool ScalesWithItemLevel;
        public readonly int Weight;

        public OptionRule(GearPart part, OptionKind kind, int min, int max, bool scalesWithItemLevel, int weight)
        {
            Part = part;
            Kind = kind;
            Min = min;
            Max = max;
            ScalesWithItemLevel = scalesWithItemLevel;
            Weight = weight;
        }
    }

    /// <summary>옵션 종류 이름·표시(게임 안 글은 쉬운 한국어).</summary>
    public static class OptionKinds
    {
        public const int Count = 15;

        static readonly string[] Names =
        {
            "공격력", "공격력", "치명타 확률", "치명타 피해", "공격 속도", "보스 피해", "체력 흡수", "처치 시 회복",
            "체력", "체력", "방어", "스킬 재사용 감소", "스킬 피해", "이동 속도", "초당 체력 재생",
        };

        /// <summary>‰로 저장하는 종류인가(공격력%·치명·공속·보스·흡수·체력%·재사용·스킬 피해·이동).</summary>
        public static bool IsPercent(OptionKind kind)
        {
            switch (kind)
            {
                case OptionKind.AttackFlat:
                case OptionKind.OnKillHeal:
                case OptionKind.HpFlat:
                case OptionKind.DefenseFlat:
                case OptionKind.HpRegen:
                    return false;
                default:
                    return true;
            }
        }

        public static string Name(OptionKind kind) => Names[(int)kind];

        /// <summary>옵션 줄 글(예: '+6.2% 공격력', '+25 공격력'). ‰는 소수 한 자리 %로 보인다.</summary>
        public static string Format(OptionKind kind, int value)
        {
            string sign = value >= 0 ? "+" : "−";
            int abs = Math.Abs(value);
            if (!IsPercent(kind)) return sign + abs + " " + Name(kind);
            string pct = (abs / 10) + (abs % 10 != 0 ? "." + (abs % 10) : "");
            return sign + pct + "% " + Name(kind);
        }
    }

    /// <summary>
    /// 부위별 옵션 풀 32줄과 등급별 옵션 수·배율(장비 문서 5-1·5-2·5-3).
    /// 표 값은 문서 5-3 그대로다(고급·iLv1 기준 범위, 전설 최대 = 위끝 × 1.5). 부위 전용 옵션(이동은 장화만, 공격 속도는 무기·장갑만,
    /// 치명 확률은 무기·장갑·반지만, 치명 피해는 무기·목걸이만, 재사용 감소는 투구·목걸이만, 체력%는 갑옷만)으로 상한을 지킨다(7장).
    /// </summary>
    public static class OptionTable
    {
        /// <summary>등급별 옵션 수: 일반 0, 고급 1, 희귀 2, 영웅 3, 전설 3(+ 고유 효과).</summary>
        static readonly int[] CountByGrade = { 0, 1, 2, 3, 3 };
        /// <summary>등급별 옵션 수치 배율(‰): 고급 ×1.00, 희귀 ×1.15, 영웅 ×1.30, 전설 ×1.50.</summary>
        static readonly int[] ValueByGrade = { 0, 1000, 1150, 1300, 1500 };

        static readonly OptionRule[] Rules =
        {
            // 무기 8줄(2차 그대로)
            new OptionRule(GearPart.Weapon, OptionKind.AttackFlat, 20, 40, true, 10),
            new OptionRule(GearPart.Weapon, OptionKind.AttackPercent, 40, 80, false, 10),
            new OptionRule(GearPart.Weapon, OptionKind.CritChance, 20, 40, false, 8),
            new OptionRule(GearPart.Weapon, OptionKind.CritDamage, 100, 200, false, 8),
            new OptionRule(GearPart.Weapon, OptionKind.AttackSpeed, 40, 80, false, 8),
            new OptionRule(GearPart.Weapon, OptionKind.BossDamage, 60, 120, false, 5),
            new OptionRule(GearPart.Weapon, OptionKind.LifeSteal, 4, 8, false, 4),
            new OptionRule(GearPart.Weapon, OptionKind.OnKillHeal, 20, 40, true, 5),
            // 갑옷 4줄
            new OptionRule(GearPart.Armor, OptionKind.HpFlat, 60, 120, true, 10),
            new OptionRule(GearPart.Armor, OptionKind.HpPercent, 40, 80, false, 8),
            new OptionRule(GearPart.Armor, OptionKind.DefenseFlat, 30, 60, true, 10),
            new OptionRule(GearPart.Armor, OptionKind.OnKillHeal, 10, 20, true, 5),
            // 투구 4줄(스킬)
            new OptionRule(GearPart.Helm, OptionKind.HpFlat, 30, 60, true, 10),
            new OptionRule(GearPart.Helm, OptionKind.CooldownReduction, 20, 40, false, 8),
            new OptionRule(GearPart.Helm, OptionKind.SkillDamage, 30, 60, false, 8),
            new OptionRule(GearPart.Helm, OptionKind.OnKillHeal, 10, 20, true, 5),
            // 장갑 4줄(공격 리듬·치명)
            new OptionRule(GearPart.Gloves, OptionKind.AttackSpeed, 30, 60, false, 8),
            new OptionRule(GearPart.Gloves, OptionKind.CritChance, 20, 40, false, 8),
            new OptionRule(GearPart.Gloves, OptionKind.AttackFlat, 10, 20, true, 8),
            new OptionRule(GearPart.Gloves, OptionKind.LifeSteal, 2, 4, false, 4),
            // 장화 4줄(이동은 여기만)
            new OptionRule(GearPart.Boots, OptionKind.MoveSpeed, 40, 80, false, 10),
            new OptionRule(GearPart.Boots, OptionKind.HpFlat, 30, 60, true, 10),
            new OptionRule(GearPart.Boots, OptionKind.DefenseFlat, 15, 30, true, 8),
            new OptionRule(GearPart.Boots, OptionKind.HpRegen, 6, 12, true, 5),
            // 반지 4줄(자리마다)
            new OptionRule(GearPart.Ring, OptionKind.AttackFlat, 10, 20, true, 10),
            new OptionRule(GearPart.Ring, OptionKind.AttackPercent, 15, 30, false, 8),
            new OptionRule(GearPart.Ring, OptionKind.CritChance, 10, 20, false, 8),
            new OptionRule(GearPart.Ring, OptionKind.HpFlat, 50, 100, true, 6),
            // 목걸이 4줄(스킬·치명 피해)
            new OptionRule(GearPart.Amulet, OptionKind.CritDamage, 100, 200, false, 8),
            new OptionRule(GearPart.Amulet, OptionKind.CooldownReduction, 30, 60, false, 6),
            new OptionRule(GearPart.Amulet, OptionKind.SkillDamage, 30, 60, false, 6),
            new OptionRule(GearPart.Amulet, OptionKind.HpFlat, 50, 100, true, 8),
        };

        /// <summary>32줄 전체.</summary>
        public static IReadOnlyList<OptionRule> All => Rules;

        /// <summary>그 부위의 풀(차례 유지, 새 목록).</summary>
        public static List<OptionRule> ForPart(GearPart part)
        {
            var list = new List<OptionRule>();
            foreach (var r in Rules)
                if (r.Part == part) list.Add(r);
            return list;
        }

        public static int CountFor(Grade grade) => CountByGrade[Clamp(grade)];

        /// <summary>등급 옵션 수치 배율(‰). 일반은 0(옵션 없음).</summary>
        public static int ValuePermilleFor(Grade grade) => ValueByGrade[Clamp(grade)];

        /// <summary>원값의 단위: 범위를 천분 단위로 굴린다(20~40이면 20,000~40,000).</summary>
        public const int RawScale = 1000;

        /// <summary>그 부위·종류의 줄(부위마다 종류는 한 줄뿐). 없으면 false.</summary>
        public static bool TryGetRule(GearPart part, OptionKind kind, out OptionRule rule)
        {
            foreach (var r in Rules)
                if (r.Part == part && r.Kind == kind)
                {
                    rule = r;
                    return true;
                }
            rule = default;
            return false;
        }

        /// <summary>
        /// 옵션 한 줄을 굴린다(5-2, 난수 1번): 원값 = rng.NextInt(Min × 1000, Max × 1000 + 1)(천분 단위),
        /// 값 = 원값 × 등급 옵션 배율‰(× 고정값 옵션이면 iLv 배율‰)을 마지막에 한 번 반올림(0.5 올림). 단계는 원값의 1/4 구간(값을 제한하지 않음).
        /// 일반 등급은 옵션이 없지만 직접 부르면 ×1.00으로 굴린다.
        /// </summary>
        public static GearOption Roll(OptionRule rule, Grade grade, int itemLevel, IRandom rng)
        {
            int lo = RawMin(rule);
            int hi = RawMax(rule);
            int raw = rng.NextInt(lo, hi + 1);
            return FromRaw(rule, grade, itemLevel, raw);
        }

        /// <summary>원값(천분 단위) 하나를 옵션 줄로 만든다. Roll의 난수 뒤 계산과 같다(시험·밸런스 계산용).</summary>
        public static GearOption FromRaw(OptionRule rule, Grade grade, int itemLevel, int raw) =>
            new GearOption(rule.Kind, ScaleRaw(rule, grade, itemLevel, raw), TierOf(rule, raw));

        /// <summary>
        /// 원값(천분 단위) × 등급 옵션 배율‰ × (고정값이면 iLv 배율‰) ÷ 10⁹(고정값) 또는 ÷ 10⁶(%)을 한 번만 반올림(0.5 올림).
        /// </summary>
        public static int ScaleRaw(OptionRule rule, Grade grade, int itemLevel, int raw)
        {
            long numerator = (long)raw * GradePermille(grade);
            long denominator = (long)RawScale * 1000L;
            if (rule.ScalesWithItemLevel)
            {
                numerator *= GearMath.ItemLevelPermille(itemLevel);
                denominator *= 1000L;
            }
            if (numerator >= 0) return (int)((numerator + denominator / 2) / denominator);
            return -(int)((-numerator + denominator / 2) / denominator);
        }

        /// <summary>
        /// 단계 Ⅰ~Ⅳ(1~4): 원값이 범위의 몇 번째 1/4에 드는가(5-4). 아래끝 = 1, 위끝 = 4.
        /// 경계는 아래 구간에 넣지 않는다(20~40이면 25,000부터 2단계, 30,000부터 3단계, 35,000부터 4단계).
        /// </summary>
        public static int TierOf(OptionRule rule, int raw)
        {
            long lo = RawMin(rule);
            long width = (long)RawMax(rule) - lo;
            if (width <= 0) return 1;
            long tier = 1 + 4L * Math.Max(0L, raw - lo) / width;
            return (int)Math.Max(1L, Math.Min(4L, tier));
        }

        /// <summary>그 등급·iLv에서 나올 수 있는 가장 작은 값(원값 아래끝).</summary>
        public static int MinValue(OptionRule rule, Grade grade, int itemLevel) => ScaleRaw(rule, grade, itemLevel, RawMin(rule));

        /// <summary>그 등급·iLv에서 나올 수 있는 가장 큰 값(원값 위끝). 전설 iLv1이면 위끝 × 1.5(5-3 '전설 최대').</summary>
        public static int MaxValue(OptionRule rule, Grade grade, int itemLevel) => ScaleRaw(rule, grade, itemLevel, RawMax(rule));

        /// <summary>
        /// 저장된 값으로 원래 굴림 위치(0~1, 2차 6-6 '품질')를 되짚는다. 반올림 때문에 조금 어긋날 수 있어 0~1로 자른다.
        /// 그 부위에 그 종류 줄이 없으면 단계로 어림한다((단계 − 1) ÷ 3).
        /// </summary>
        public static double QualityOf(GearPart part, GearOption option, Grade grade, int itemLevel)
        {
            if (!TryGetRule(part, option.Kind, out var rule) || rule.Max <= rule.Min) return (option.Tier - 1) / 3.0;
            double scale = GradePermille(grade) / 1000.0;
            if (rule.ScalesWithItemLevel) scale *= GearMath.ItemLevelPermille(itemLevel) / 1000.0;
            double raw = option.Value / scale;
            double q = (raw - rule.Min) / (rule.Max - rule.Min);
            return Math.Max(0.0, Math.Min(1.0, q));
        }

        /// <summary>
        /// 장비 하나의 옵션들(5-2·8-1): CountFor(등급) 줄을 가중치로 뽑는다. 뽑은 줄은 풀에서 빼서 한 장비 안 중복을 막는다.
        /// 줄마다 난수 2번(줄 고르기 → 값 굴리기). 풀이 다 떨어지면 거기서 멈춘다.
        /// </summary>
        public static List<GearOption> RollAll(GearPart part, Grade grade, int itemLevel, IRandom rng)
        {
            int count = CountFor(grade);
            var result = new List<GearOption>(count);
            if (count <= 0) return result;
            var pool = ForPart(part);
            for (int n = 0; n < count && pool.Count > 0; n++)
            {
                int total = 0;
                foreach (var r in pool) total += Math.Max(0, r.Weight);
                int index = pool.Count - 1;
                if (total > 0)
                {
                    int pick = rng.NextInt(0, total);
                    for (int i = 0; i < pool.Count; i++)
                    {
                        pick -= Math.Max(0, pool[i].Weight);
                        if (pick < 0)
                        {
                            index = i;
                            break;
                        }
                    }
                }
                else
                {
                    index = rng.NextInt(0, pool.Count);
                }
                var rule = pool[index];
                pool.RemoveAt(index);
                result.Add(Roll(rule, grade, itemLevel, rng));
            }
            return result;
        }

        static int RawMin(OptionRule rule) => rule.Min * RawScale;
        static int RawMax(OptionRule rule) => rule.Max * RawScale;

        /// <summary>굴릴 때 쓰는 등급 배율(‰). 일반은 ×1.00으로 본다.</summary>
        static int GradePermille(Grade grade) => Math.Max(1000, ValuePermilleFor(grade));

        static int Clamp(Grade grade) => Math.Max(0, Math.Min(GradeRules.Count - 1, (int)grade));
    }
}
