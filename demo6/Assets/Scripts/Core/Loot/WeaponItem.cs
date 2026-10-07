using System;
using Demo6.Core.Combat;

namespace Demo6.Core.Loot
{
    /// <summary>
    /// 장비 수치 공식(2차 6-1, 장비 문서 4-3): 기본 능력치 = 1단계 값 × 아이템 레벨 배율 × 등급 배율 × 굴림(90~110%) × (1 + 누적 강화).
    /// 강화 0이면 4인자 BaseStat(M0b 무기 공식)과 같은 값이다. 강화 비용·계승·장비 점수도 여기서 계산한다(8-4, 2차 6-6).
    /// 배율은 ‰ 정수로 곱하고 마지막에 한 번 반올림한다(0.5는 올림). 실수 오차를 피하기 위해서다.
    /// </summary>
    public static class GearMath
    {
        /// <summary>무기 공격력 1단계 값(2차 6-3, 10배 단위). 아홉 무기 모두 같다(무기 차이는 리듬·모양·치명·고유 규칙으로만).</summary>
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

        // ── 강화(장비 문서 8-4, 2차 8-1). 시험: EnhanceCostTests ──

        /// <summary>2차 8-1 누적 상승(‰): +1 50 … +5 250, +6 320, +7 390, +8 490, +9 590, +10 690, +11 820, +12 950, +13 1130, +14 1310, +15 1490.</summary>
        static readonly int[] EnhanceCumulative = { 0, 50, 100, 150, 200, 250, 320, 390, 490, 590, 690, 820, 950, 1130, 1310, 1490 };
        /// <summary>2차 8-1 시도 비용(강화석, 무기 = 배율 1000‰ 기준): +1~+15.</summary>
        static readonly int[] EnhanceBaseCost = { 0, 2, 2, 3, 3, 4, 5, 6, 10, 12, 14, 18, 22, 46, 59, 72 };
        /// <summary>2차 6-2 강화 상한: 일반 +5, 고급 +7, 희귀 +10, 영웅 +12, 전설 +15.</summary>
        static readonly int[] EnhanceCaps = { 5, 7, 10, 12, 15 };

        public const int MaxEnhance = 15;

        /// <summary>강화 단계의 누적 상승(‰, +0 = 0).</summary>
        public static int EnhancePermille(int enhance) => EnhanceCumulative[Math.Max(0, Math.Min(MaxEnhance, enhance))];

        /// <summary>등급 강화 상한.</summary>
        public static int EnhanceCap(Grade grade) => EnhanceCaps[Math.Max(0, Math.Min(EnhanceCaps.Length - 1, (int)grade))];

        /// <summary>
        /// 강화를 넣은 기본 능력치 = 1단계 값 × iLv‰ × 등급‰ × 굴림‰ × (1000 + 누적 강화‰) ÷ 10¹² 를 한 번만 반올림(0.5 올림).
        /// 강화 0이면 BaseStat(4인자)와 같은 값이다.
        /// </summary>
        public static int BaseStat(int stageOneValue, int itemLevel, Grade grade, int rollPermille, int enhance)
        {
            if (enhance <= 0) return BaseStat(stageOneValue, itemLevel, grade, rollPermille);
            long numerator = (long)stageOneValue * ItemLevelPermille(itemLevel) * GradeRules.MultiplierPermille(grade) * ClampRoll(rollPermille)
                             * (1000 + EnhancePermille(enhance));
            const long Denominator = 1000L * 1000L * 1000L * 1000L;
            return (int)((numerator + Denominator / 2) / Denominator);
        }

        /// <summary>2차 시도 비용(무기 기준, 강화석). target = 1~15, 범위 밖이면 0.</summary>
        public static int EnhanceBaseCostOf(int target) => target < 1 || target > MaxEnhance ? 0 : EnhanceBaseCost[target];

        /// <summary>
        /// 강화 시도 비용(강화석) = 2차 시도 비용 × 부위 비용 배율 ÷ 1000, 반올림(0.5 올림), 최소 1석(장비 문서 8-4).
        /// target = 시도해서 오를 단계(1~15). 범위 밖이면 0.
        /// 표: 무기 2·2·3·3·4·5·6·10·12·14·18·22·46·59·72, 갑옷 1·1·2·2·2·3·3·5·6·7·9·11·23·30·36,
        /// 목걸이 1·1·1·1·2·2·2·4·5·6·7·9·18·24·29, 반지 1·1·1·1·1·2·2·3·4·4·5·7·14·18·22,
        /// 투구 1·1·1·1·1·1·1·2·2·3·4·4·9·12·14, 장갑·장화 1·1·1·1·1·1·1·2·2·2·3·3·7·9·11.
        /// </summary>
        public static int EnhanceCost(GearPart part, int target)
        {
            if (target < 1 || target > MaxEnhance) return 0;
            return ScaleCost(part, EnhanceBaseCost[target], 1);
        }

        /// <summary>
        /// 부위 비용 배율을 곱한 강화석 수(8-4): 2차 값 × 부위 배율‰ ÷ 1000을 반올림(0.5 올림)하고 minimum 밑으로 내리지 않는다.
        /// 강화 시도·계승 비용은 minimum 1, 강화 환급은 minimum 0으로 부른다. 2차 값이 0 이하면(비용·환급 없음) 0.
        /// </summary>
        public static int ScaleCost(GearPart part, int baseCost, int minimum = 1)
        {
            if (baseCost <= 0) return 0;
            int scaled = (int)(((long)baseCost * GearSlots.EnhanceCostPermille(part) + 500) / 1000);
            return Math.Max(minimum, scaled);
        }

        /// <summary>
        /// 계승할 수 있는 짝인가(8-4·8-6): 같은 부위끼리만. 반지 ↔ 반지는 되고(자리 무관) 반지 ↔ 목걸이는 안 된다.
        /// 종류는 달라도 된다(장검 → 대검). 같은 장비이거나 한쪽이 없으면 false.
        /// </summary>
        public static bool CanInherit(GearItem source, GearItem target) =>
            source != null && target != null && !ReferenceEquals(source, target) && source.Part == target.Part;

        /// <summary>
        /// 2차 6-6 장비 점수(디버그·부위 보정·상점 '가장 약한 부위' 입력): 100 × iLv 배율 × 등급 배율 × (1 + 누적 강화) + 옵션 줄마다 (10 + 10 × 품질) + 전설이면 50.
        /// 품질 = 굴린 값의 범위 안 위치(0~1, OptionTable.QualityOf). 빈 자리는 0.
        /// 예: 시작 장검 100, 전설 iLv10 +0 옵션 3줄 모두 위끝이면 100 × 2.35 × 2.30 + 3 × 20 + 50 = 650.5.
        /// </summary>
        public static double ItemScore(GearItem item)
        {
            if (item == null) return 0;
            double score = 100.0 * ItemLevelPermille(item.ItemLevel) / 1000.0 * GradeRules.MultiplierPermille(item.Grade) / 1000.0
                           * (1 + EnhancePermille(item.Enhance) / 1000.0);
            foreach (var o in item.Options) score += 10 + 10 * OptionTable.QualityOf(item.Part, o, item.Grade, item.ItemLevel);
            if (item.IsLegendary) score += 50;
            return score;
        }
    }

    /// <summary>
    /// 무기 한 자루(2차 11-4 장비 인스턴스의 M0b 최소판: 종류 id, 등급, 아이템 레벨, 굴림‰만).
    /// 최종 공격력은 저장하지 않고 매번 GearMath로 계산한다. 옵션·전설 효과·강화는 M0b에서 뺀다.
    /// 7칸 장비(장비 문서 12장)부터 게임 쪽은 GearItem을 쓴다. 이 클래스는 옛 무기 보기로만 남는다
    /// (꾸러미 v1 글 읽기, 옛 시험, GearItem.FromWeapon·ToWeapon 다리). 새 코드에서 쓰지 않는다.
    /// </summary>
    public sealed class WeaponItem
    {
        public const string LongswordId = GearBaseTable.Longsword;
        public const string GreatswordId = GearBaseTable.Greatsword;
        public const string TwinbladesId = GearBaseTable.Twinblades;
        // 새 무기 6종(전투·보스·무기 다듬기 1차 2장). 옛 굴림(LootRules.RollWeapon)에서는 나오지 않지만 GearItem.ToWeapon 다리로는 지나간다.
        public const string MaulId = GearBaseTable.Maul;
        public const string SpearId = GearBaseTable.Spear;
        public const string ScytheId = GearBaseTable.Scythe;
        public const string AxeId = GearBaseTable.Axe;
        public const string DaggerId = GearBaseTable.Dagger;
        public const string FlailId = GearBaseTable.Flail;

        /// <summary>WeaponPresets id(WeaponPresets.All 9종 가운데 하나, 모르면 장검).</summary>
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

        /// <summary>종류 이름(장검·대검·쌍검·쇠망치·창·큰 낫·도끼·단검·사슬 철퇴). GearBase.Name과 같다.</summary>
        public string WeaponName => Rule.displayName;

        /// <summary>화면 이름 예: "희귀 대검".</summary>
        public string DisplayName => GradeRules.Name(Grade) + " " + WeaponName;

        /// <summary>시작 무기: 1층 일반 장검 굴림 1000‰ = 공격 100(플레이어 공격력 200, 2차 5-3).</summary>
        public static WeaponItem Starting() => new WeaponItem(LongswordId, Grade.Common, 1, 1000);

        /// <summary>id의 공격 규칙(WeaponPresets.All 9종을 돈다, 장비 표 GearBase.WeaponRule·StatSheet.WeaponRule도 이것을 씀). 모르는 id면 장검.</summary>
        public static WeaponAttackRule RuleOf(string weaponId)
        {
            foreach (var w in WeaponPresets.All)
                if (w != null && w.id == weaponId) return w;
            return WeaponPresets.Longsword;
        }

        public override string ToString() => DisplayName + " 공격 " + Attack + " (iLv" + ItemLevel + ", " + RollPermille + "‰)";
    }
}
