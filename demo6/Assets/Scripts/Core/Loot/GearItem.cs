using System;
using System.Collections.Generic;
using System.Text;
using Demo6.Core.Combat;

namespace Demo6.Core.Loot
{
    /// <summary>
    /// 부위 공통 장비 하나(장비 문서 12장 단계 1, 2차 11-4): 종류 id, 등급, 아이템 레벨, 굴림‰, 강화, 옵션 [(종류, 값, 단계)], 전설 id·굴림,
    /// 끼운 룬(기획/세-무기-우클릭-소켓-1차.md 6장, 무기만, 홈 수는 부위·등급으로 계산), 강화 실패 수(재화 쓸 곳 1차 2-5).
    /// 바뀌지 않는 값이다(강화·종류 바꾸기는 새 장비를 돌려줌). 같은 장비인지는 참조로 가린다(Equals를 덮지 않음).
    /// 최종 능력치는 저장하지 않고 매번 GearMath로 계산한다.
    /// 공개 이름·뜻은 능력치 합산·장착·꾸러미 글·겉모습·전설이 그대로 쓴다.
    /// </summary>
    public sealed class GearItem
    {
        static readonly GearOption[] NoOptions = Array.Empty<GearOption>();
        static readonly string[] NoRunes = Array.Empty<string>();

        readonly GearOption[] _options;
        readonly string[] _runes;

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

        /// <summary>강화 실패 수 상한(재화 쓸 곳 1차 2-5). +7까지 천장은 2번이라 넉넉하다.</summary>
        public const int MaxEnhanceFails = 9;
        /// <summary>
        /// 지금 단계에서 다음 단계로 가다 실패한 수(재화 쓸 곳 1차 2-5, 0~9로 자름). 강화 성공(WithEnhance)이면 0, 룬·종류만 바꾼 새 장비는 그대로 들고 간다.
        /// +7까지는 단계가 내려가지 않아 이 수 하나로 천장·보정(EnhanceRules)이 모두 돈다. 꾸러미 장비 글 9번째 칸(0이면 쓰지 않음).
        /// </summary>
        public int EnhanceFails { get; }

        /// <summary>
        /// runes = 끼운 룬 id(기획/세-무기-우클릭-소켓-1차.md 6-1·6-5, 기본 null = 빈 홈). 차례대로 RuneRules.CanInsert가 되는 것만 남긴다
        /// (모르는 id·같은 룬 두 번·홈 수를 넘는 룬·무기가 아닌 부위는 조용히 거름). 거른 룬을 주머니로 보내야 하면 부르는 쪽이 RuneRules.Fit을 먼저 쓴다(꾸러미 글 읽기).
        /// enhanceFails = 강화 실패 수(재화 쓸 곳 1차 2-5, 기본 0, 0~9로 자름).
        /// </summary>
        public GearItem(string baseId, Grade grade, int itemLevel, int rollPermille, int enhance = 0,
            IEnumerable<GearOption> options = null, string legendaryId = null, int legendaryRollPermille = 0,
            IEnumerable<string> runes = null, int enhanceFails = 0)
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
            EnhanceFails = Math.Max(0, Math.Min(MaxEnhanceFails, enhanceFails));
            _runes = NoRunes;
            if (runes != null)
            {
                var kept = new List<string>(RuneRules.MaxSockets);
                RuneRules.Fit(Base.Part, grade, runes, kept, null);
                if (kept.Count > 0) _runes = kept.ToArray();
            }
        }

        /// <summary>끼운 룬 id(끼운 차례, 빈 홈이면 빈 목록). 능력치에는 들지 않는다(룬 효과는 낀 무기의 룬만 PlayerController가 센다).</summary>
        public IReadOnlyList<string> Runes => _runes;
        /// <summary>룬 홈 칸 수(RuneRules.SocketCount: 무기 일반·고급·희귀 1, 영웅·전설 2, 그 밖 0). 저장하지 않고 매번 계산한다.</summary>
        public int SocketCount => RuneRules.SocketCount(Part, Grade);
        /// <summary>빈 룬 홈 수.</summary>
        public int FreeSockets => Math.Max(0, SocketCount - _runes.Length);

        /// <summary>그 룬을 끼웠는가.</summary>
        public bool HasRune(string runeId) => runeId != null && Array.IndexOf(_runes, runeId) >= 0;

        /// <summary>룬만 바꾼 새 장비(다른 값은 그대로, 강화 실패 수도). 들어가지 못하는 룬은 거른다(생성자 규칙). null이면 빈 홈.</summary>
        public GearItem WithRunes(IEnumerable<string> runes) =>
            new GearItem(BaseId, Grade, ItemLevel, RollPermille, Enhance, _options, LegendaryId, LegendaryRollPermille, runes, EnhanceFails);

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

        /// <summary>강화 단계만 바꾼 새 장비(룬은 그대로, 강화 실패 수는 0 — 재화 쓸 곳 1차 2-5).</summary>
        public GearItem WithEnhance(int enhance) => new GearItem(BaseId, Grade, ItemLevel, RollPermille, enhance, _options, LegendaryId, LegendaryRollPermille, _runes);

        /// <summary>강화 실패 수만 바꾼 새 장비(다른 값은 그대로, 0~9로 자름, 재화 쓸 곳 1차 2-4 '실패 = 실패 수 +1').</summary>
        public GearItem WithEnhanceFails(int fails) =>
            new GearItem(BaseId, Grade, ItemLevel, RollPermille, Enhance, _options, LegendaryId, LegendaryRollPermille, _runes, fails);

        /// <summary>
        /// 종류만 바꾼 새 장비(시험 키 1·2·3: 등급·iLv·굴림·강화·옵션·전설·룬·강화 실패 수를 그대로 복사, 장비 문서 8-6 아래).
        /// 룬 홈 수는 부위·등급으로만 정해져 무기끼리 바꿔도 룬이 넘치지 않는다(6-1). 부위가 다른 종류면 바꾸지 않고 자신을 돌려준다.
        /// </summary>
        public GearItem WithBase(string baseId)
        {
            var b = GearBaseTable.Get(baseId);
            if (b == null || b.Part != Part || b.Id == BaseId) return this;
            return new GearItem(b.Id, Grade, ItemLevel, RollPermille, Enhance, _options, LegendaryId, LegendaryRollPermille, _runes, EnhanceFails);
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
            foreach (var r in _runes) sb.Append(" · ◆").Append(RuneTable.Get(r)?.Name ?? r);
            return sb.ToString();
        }
    }
}
