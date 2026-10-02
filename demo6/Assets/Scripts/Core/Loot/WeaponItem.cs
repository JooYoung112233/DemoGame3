using System;
using Demo6.Core.Combat;

namespace Demo6.Core.Loot
{
    /// <summary>
    /// 장비 수치 공식(2차 6-1). M0b는 강화가 없어 '× (1 + 누적 강화 상승)'을 뺀다.
    /// 기본 능력치 = 1단계 값 × 아이템 레벨 배율 × 등급 배율 × 굴림(90~110%).
    /// 배율은 ‰ 정수로 곱하고 마지막에 한 번 반올림한다(0.5는 올림). 실수 오차를 피하기 위해서다.
    /// </summary>
    public static class GearMath
    {
        /// <summary>무기 공격력 1단계 값(2차 6-3, 10배 단위). 세 무기 모두 같다.</summary>
        public const int WeaponBaseAttack = 100;
        /// <summary>맨몸 공격력(2차 6-1·계약서 '낀 무기'): 플레이어 공격력 = 100 + 무기 공격력.</summary>
        public const int BareHandAttack = 100;
        public const int RollMinPermille = 900;
        public const int RollMaxPermille = 1100;
        /// <summary>아이템 레벨 상한(보스 전용 11).</summary>
        public const int MaxItemLevel = 11;

        /// <summary>아이템 레벨 배율(‰) = 1000 + 150 × (iLv − 1). iLv1 1.00, iLv10 2.35, iLv11 2.50.</summary>
        public static int ItemLevelPermille(int itemLevel)
        {
            int lv = Math.Max(1, Math.Min(MaxItemLevel, itemLevel));
            return 1000 + 150 * (lv - 1);
        }

        /// <summary>굴림을 900~1100‰ 안으로 자른다.</summary>
        public static int ClampRoll(int rollPermille) => Math.Max(RollMinPermille, Math.Min(RollMaxPermille, rollPermille));

        /// <summary>
        /// 1단계 값 × iLv‰ × 등급‰ × 굴림‰ ÷ 10⁹ 를 한 번만 반올림(0.5 올림).
        /// 예: 10층 전설 굴림 1000‰ = 100 × 2350 × 2300 × 1000 ÷ 10⁹ = 540.5 → 541.
        /// </summary>
        public static int BaseStat(int stageOneValue, int itemLevel, Grade grade, int rollPermille)
        {
            long numerator = (long)stageOneValue * ItemLevelPermille(itemLevel) * GradeRules.MultiplierPermille(grade) * ClampRoll(rollPermille);
            const long Denominator = 1000L * 1000L * 1000L;
            return (int)((numerator + Denominator / 2) / Denominator);
        }

        public static int WeaponAttack(int itemLevel, Grade grade, int rollPermille) =>
            BaseStat(WeaponBaseAttack, itemLevel, grade, rollPermille);
    }

    /// <summary>
    /// 무기 한 자루(2차 11-4 장비 인스턴스의 M0b 최소판: 종류 id, 등급, 아이템 레벨, 굴림‰만).
    /// 최종 공격력은 저장하지 않고 매번 GearMath로 계산한다. 옵션·전설 효과·강화는 M0b에서 뺀다.
    /// </summary>
    public sealed class WeaponItem
    {
        public const string LongswordId = "wpn_longsword";
        public const string GreatswordId = "wpn_greatsword";
        public const string TwinbladesId = "wpn_twinblades";

        /// <summary>WeaponPresets id(wpn_longsword / wpn_greatsword / wpn_twinblades).</summary>
        public string WeaponId { get; }
        public Grade Grade { get; }
        public int ItemLevel { get; }
        /// <summary>기본 능력치 굴림(900~1100‰).</summary>
        public int RollPermille { get; }

        public WeaponItem(string weaponId, Grade grade, int itemLevel, int rollPermille)
        {
            WeaponId = string.IsNullOrEmpty(weaponId) ? LongswordId : weaponId;
            Grade = grade;
            ItemLevel = Math.Max(1, Math.Min(GearMath.MaxItemLevel, itemLevel));
            RollPermille = GearMath.ClampRoll(rollPermille);
        }

        /// <summary>무기 공격력(맨몸 100은 빼고). 1층 일반 굴림 1000‰ = 100.</summary>
        public int Attack => GearMath.WeaponAttack(ItemLevel, Grade, RollPermille);

        /// <summary>낀 뒤 플레이어 공격력(맨몸 100 + 무기).</summary>
        public int PlayerAttack => GearMath.BareHandAttack + Attack;

        /// <summary>무기 종류 공격 규칙(WeaponPresets). 모르는 id면 장검.</summary>
        public WeaponAttackRule Rule => RuleOf(WeaponId);

        /// <summary>종류 이름(장검·대검·쌍검).</summary>
        public string WeaponName => Rule.displayName;

        /// <summary>화면 이름 예: "희귀 대검".</summary>
        public string DisplayName => GradeRules.Name(Grade) + " " + WeaponName;

        /// <summary>시작 무기: 1층 일반 장검 굴림 1000‰ = 공격 100(플레이어 공격력 200, 2차 5-3).</summary>
        public static WeaponItem Starting() => new WeaponItem(LongswordId, Grade.Common, 1, 1000);

        public static WeaponAttackRule RuleOf(string weaponId)
        {
            foreach (var w in WeaponPresets.All)
                if (w != null && w.id == weaponId) return w;
            return WeaponPresets.Longsword;
        }

        public override string ToString() => DisplayName + " 공격 " + Attack + " (iLv" + ItemLevel + ", " + RollPermille + "‰)";
    }
}
