using System;
using System.Collections.Generic;
using System.Text;
using Demo6.Core.Combat;

namespace Demo6.Core.Loot
{
    /// <summary>
    /// 부위 공통 장비 하나(장비 문서 12장 단계 1, 2차 11-4): 종류 id, 등급, 아이템 레벨, 굴림‰, 강화, 옵션 [(종류, 값, 단계)], 전설 id·굴림.
    /// 바뀌지 않는 값이다(강화·종류 바꾸기는 새 장비를 돌려줌). 같은 장비인지는 참조로 가린다(Equals를 덮지 않음).
    /// 최종 능력치는 저장하지 않고 매번 GearMath로 계산한다.
    /// 공개 이름·뜻은 능력치 합산·장착·꾸러미 글·겉모습·전설이 그대로 쓴다.
    /// </summary>
    public sealed class GearItem
    {
        static readonly GearOption[] NoOptions = Array.Empty<GearOption>();

        readonly GearOption[] _options;

        /// <summary>종류 id(GearBaseTable). 모르는 id는 장검으로 고친다(WeaponItem과 같은 규칙).</summary>
        public string BaseId { get; }
        public GearBase Base { get; }
        public Grade Grade { get; }
        public int ItemLevel { get; }
        /// <summary>기본 능력치 굴림(900~1100‰).</summary>
        public int RollPermille { get; }
        /// <summary>강화 단계(0~등급 상한).</summary>
        public int Enhance { get; }
        /// <summary>옵션 줄(굴린 차례).</summary>
        public IReadOnlyList<GearOption> Options => _options;
        /// <summary>전설 효과 id(LegendaryTable). 없으면 null.</summary>
        public string LegendaryId { get; }
        /// <summary>전설 효과 세기 굴림(‰, 0~1000 = 수치 범위 안 위치). 효과가 없으면 0.</summary>
        public int LegendaryRollPermille { get; }

        public GearItem(string baseId, Grade grade, int itemLevel, int rollPermille, int enhance = 0,
            IEnumerable<GearOption> options = null, string legendaryId = null, int legendaryRollPermille = 0)
        {
            Base = GearBaseTable.Get(baseId) ?? GearBaseTable.Get(GearBaseTable.Longsword);
            BaseId = Base.Id;
            Grade = grade;
            ItemLevel = Math.Max(1, Math.Min(GearMath.MaxItemLevel, itemLevel));
            RollPermille = GearMath.ClampRoll(rollPermille);
            Enhance = Math.Max(0, Math.Min(GearMath.EnhanceCap(grade), enhance));
            _options = options != null ? new List<GearOption>(options).ToArray() : NoOptions;
            bool legendary = !string.IsNullOrEmpty(legendaryId) && LegendaryTable.Get(legendaryId) != null;
            LegendaryId = legendary ? legendaryId : null;
            LegendaryRollPermille = legendary ? Math.Max(0, Math.Min(1000, legendaryRollPermille)) : 0;
        }

        public GearPart Part => Base.Part;
        public bool IsWeapon => Base.IsWeapon;
        public bool IsLegendary => LegendaryId != null;
        /// <summary>전설 효과(없으면 null).</summary>
        public LegendaryEffect? Legendary => IsLegendary ? LegendaryTable.Get(LegendaryId).Effect : (LegendaryEffect?)null;
        /// <summary>무기면 공격 규칙, 아니면 null.</summary>
        public WeaponAttackRule WeaponRule => Base.WeaponRule;
        public int EnhanceCap => GearMath.EnhanceCap(Grade);

        /// <summary>기본 공격력(맨몸 100은 빼고). 무기·반지만 0이 아니다.</summary>
        public int Attack => Base.Attack > 0 ? GearMath.BaseStat(Base.Attack, ItemLevel, Grade, RollPermille, Enhance) : 0;
        /// <summary>기본 방어(갑옷·투구·장갑·장화).</summary>
        public int Defense => Base.Defense > 0 ? GearMath.BaseStat(Base.Defense, ItemLevel, Grade, RollPermille, Enhance) : 0;
        /// <summary>기본 체력(방어구·목걸이).</summary>
        public int Hp => Base.Hp > 0 ? GearMath.BaseStat(Base.Hp, ItemLevel, Grade, RollPermille, Enhance) : 0;

        /// <summary>그 종류 옵션 값 합(한 장비 안에서는 같은 종류가 한 줄뿐이다).</summary>
        public int OptionTotal(OptionKind kind)
        {
            int sum = 0;
            foreach (var o in _options)
                if (o.Kind == kind) sum += o.Value;
            return sum;
        }

        /// <summary>그 종류의 종류 고유 값(고정).</summary>
        public int IntrinsicTotal(OptionKind kind) => Base.IntrinsicOf(kind);

        /// <summary>화면 이름 = 등급 + 종류(5-4 결정. 예: '일반 장검', '희귀 핏빛 반지').</summary>
        public string DisplayName => GearNaming.DisplayName(this);

        /// <summary>강화 단계만 바꾼 새 장비.</summary>
        public GearItem WithEnhance(int enhance) => new GearItem(BaseId, Grade, ItemLevel, RollPermille, enhance, _options, LegendaryId, LegendaryRollPermille);

        /// <summary>
        /// 종류만 바꾼 새 장비(시험 키 1·2·3: 등급·iLv·굴림·강화·옵션·전설을 그대로 복사, 장비 문서 8-6 아래).
        /// 부위가 다른 종류면 바꾸지 않고 자신을 돌려준다.
        /// </summary>
        public GearItem WithBase(string baseId)
        {
            var b = GearBaseTable.Get(baseId);
            if (b == null || b.Part != Part || b.Id == BaseId) return this;
            return new GearItem(b.Id, Grade, ItemLevel, RollPermille, Enhance, _options, LegendaryId, LegendaryRollPermille);
        }

        /// <summary>옛 무기(꾸러미 v1 글, M0b 무기 굴림)를 장비로. 옵션·전설·강화 없음.</summary>
        public static GearItem FromWeapon(WeaponItem weapon) =>
            weapon == null ? null : new GearItem(weapon.WeaponId, weapon.Grade, weapon.ItemLevel, weapon.RollPermille);

        /// <summary>무기면 옛 무기 보기(등급·iLv·굴림만), 아니면 null. 옮기는 동안만 쓰는 다리.</summary>
        public WeaponItem ToWeapon() => IsWeapon ? new WeaponItem(BaseId, Grade, ItemLevel, RollPermille) : null;

        /// <summary>시작 장비(4-4): 무기·갑옷·투구·장갑·장화는 일반 iLv1 굴림 1000‰(장검·가죽), 반지·목걸이 자리는 null.</summary>
        public static GearItem Starting(GearSlot slot)
        {
            switch (slot)
            {
                case GearSlot.Weapon: return new GearItem(GearBaseTable.Longsword, Grade.Common, 1, 1000);
                case GearSlot.Armor: return new GearItem(GearBaseTable.LeatherArmor, Grade.Common, 1, 1000);
                case GearSlot.Helm: return new GearItem(GearBaseTable.LeatherHelm, Grade.Common, 1, 1000);
                case GearSlot.Gloves: return new GearItem(GearBaseTable.LeatherGloves, Grade.Common, 1, 1000);
                case GearSlot.Boots: return new GearItem(GearBaseTable.LeatherBoots, Grade.Common, 1, 1000);
                default: return null;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(DisplayName).Append(" iLv").Append(ItemLevel).Append(" +").Append(Enhance).Append(" (").Append(RollPermille).Append("‰)");
            foreach (var o in _options) sb.Append(" · ").Append(o);
            if (IsLegendary) sb.Append(" · ★").Append(LegendaryTable.Get(LegendaryId).Name);
            return sb.ToString();
        }
    }
}
