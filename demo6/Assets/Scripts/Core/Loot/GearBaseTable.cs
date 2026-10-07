using System;
using System.Collections.Generic;
using Demo6.Core.Combat;

namespace Demo6.Core.Loot
{
    /// <summary>
    /// 기본 장비 종류 하나(장비 문서 4-3). 1단계 기본 능력치(10배 단위)와 종류 고유 능력치(고정).
    /// 기본 능력치 = 1단계 값 × iLv 배율 × 등급 배율 × 굴림 × (1 + 누적 강화)(GearMath.BaseStat).
    /// </summary>
    public sealed class GearBase
    {
        /// <summary>종류 id(꾸러미 글·아이콘·그림 칸 열쇠). 무기는 WeaponPresets id와 같다.</summary>
        public readonly string Id;
        public readonly GearPart Part;
        /// <summary>기본 이름(한손검과 방패, 가죽 갑옷 …). 화면 이름 = 등급 + 이 이름(5-4).</summary>
        public readonly string Name;
        public readonly ArmorWeight Weight;
        /// <summary>1단계 공격력(무기 100, 반지 20~25).</summary>
        public readonly int Attack;
        /// <summary>1단계 방어(갑옷·투구·장갑·장화).</summary>
        public readonly int Defense;
        /// <summary>1단계 체력(방어구·목걸이).</summary>
        public readonly int Hp;
        /// <summary>종류 고유 능력치(고정, iLv·등급·강화와 무관).</summary>
        public readonly StatBonus[] Intrinsic;
        /// <summary>
        /// 이 종류가 드랍·궤짝에 나오기 시작하는 층(전투·보스·무기 다듬기 1차 2-6). 기본 1. LootRules.RollGear가 ForPart(부위, 층)으로 거른다.
        /// 이미 가진 장비·꾸러미 글·시험 키는 층과 상관없이 쓴다(표에서 지우는 것이 아님).
        /// </summary>
        public readonly int UnlockFloor;

        public GearBase(string id, GearPart part, string name, ArmorWeight weight, int attack, int defense, int hp, params StatBonus[] intrinsic)
            : this(id, part, name, weight, attack, defense, hp, 1, intrinsic)
        {
        }

        /// <param name="unlockFloor">처음 나오는 층(1 미만은 1).</param>
        public GearBase(string id, GearPart part, string name, ArmorWeight weight, int attack, int defense, int hp, int unlockFloor, params StatBonus[] intrinsic)
        {
            Id = id;
            Part = part;
            Name = name;
            Weight = weight;
            Attack = attack;
            Defense = defense;
            Hp = hp;
            UnlockFloor = Math.Max(1, unlockFloor);
            Intrinsic = intrinsic ?? Array.Empty<StatBonus>();
        }

        /// <summary>그 층에서 나올 수 있는가(UnlockFloor ≤ 층).</summary>
        public bool UnlockedAt(int floor) => UnlockFloor <= floor;

        public bool IsWeapon => Part == GearPart.Weapon;

        /// <summary>아이콘 id(Resources/UI/Items/{id}). 종류 id와 같다(그림은 UI 그림 쪽이 나중에 더함).</summary>
        public string IconId => Id;

        /// <summary>무기면 공격 규칙(WeaponPresets), 아니면 null.</summary>
        public WeaponAttackRule WeaponRule => IsWeapon ? WeaponItem.RuleOf(Id) : null;

        /// <summary>이 종류 고유의 그 종류 값 합(없으면 0).</summary>
        public int IntrinsicOf(OptionKind kind)
        {
            int sum = 0;
            foreach (var b in Intrinsic)
                if (b.Kind == kind) sum += b.Value;
            return sum;
        }
    }

    /// <summary>
    /// 기본 장비 27종(장비 문서 4-3): 무기 9종(한손검과 방패·대검·쌍검 + 새 6종) + 방어구·장신구 6부위 × 3.
    /// 부위 안 종류는 그 층에 풀린 것끼리 같은 확률이다(1층 무기 1/3, 2층부터 1/9, 다른 부위는 늘 1/3).
    /// 2차 id acc_ring·acc_necklace·acc_charm은 쓰지 않는다. id 문자열은 꾸러미 글·그림 칸·아이콘이 쓰므로 바꾸지 않는다.
    /// </summary>
    public static class GearBaseTable
    {
        public const string Longsword = "wpn_longsword";
        public const string Greatsword = "wpn_greatsword";
        public const string Twinblades = "wpn_twinblades";
        /// <summary>
        /// 새 무기 6종 id(기획/전투-보스-무기-다듬기-1차.md 2-6, WeaponPresets id와 같음). 공격 100, 종류 고유 치명만 다르다.
        /// 모두 NewWeaponUnlockFloor(2층)부터 나온다.
        /// </summary>
        public const string Maul = "wpn_maul";
        public const string Spear = "wpn_spear";
        public const string Scythe = "wpn_scythe";
        public const string Axe = "wpn_axe";
        public const string Dagger = "wpn_dagger";
        public const string Flail = "wpn_flail";
        public const string LeatherArmor = "arm_leather";
        public const string ChainArmor = "arm_chain";
        public const string PlateArmor = "arm_plate";
        public const string LeatherHelm = "hlm_leather";
        public const string ChainHelm = "hlm_chain";
        public const string PlateHelm = "hlm_plate";
        public const string LeatherGloves = "glv_leather";
        public const string ChainGloves = "glv_chain";
        public const string PlateGloves = "glv_plate";
        public const string LeatherBoots = "bts_leather";
        public const string ChainBoots = "bts_chain";
        public const string PlateBoots = "bts_plate";
        public const string IronRing = "rng_iron";
        public const string BloodRing = "rng_blood";
        public const string FangRing = "rng_fang";
        public const string FangAmulet = "amu_fang";
        public const string CharmAmulet = "amu_charm";
        public const string AmberAmulet = "amu_amber";

        /// <summary>
        /// 새 무기 6종이 처음 나오는 층. 문서 2-6 차례는 창 2층·쇠망치 3층·큰 낫 4층(나머지는 그 뒤)이지만,
        /// 시험판 최대 층이 2라 지금은 6종 모두 2층에서 푼다. 층이 늘면 종류마다 이 값을 나눠 준다.
        /// </summary>
        public const int NewWeaponUnlockFloor = 2;

        static StatBonus B(OptionKind kind, int value) => new StatBonus(kind, value);

        /// <summary>새 무기 줄: 공격 100, 종류 고유 치명, NewWeaponUnlockFloor부터.</summary>
        static GearBase NewWeapon(string id, string name, params StatBonus[] intrinsic) =>
            new GearBase(id, GearPart.Weapon, name, ArmorWeight.None, 100, 0, 0, NewWeaponUnlockFloor, intrinsic);

        static readonly GearBase[] Bases =
        {
            new GearBase(Longsword, GearPart.Weapon, "한손검과 방패", ArmorWeight.None, 100, 0, 0, B(OptionKind.CritChance, 20), B(OptionKind.CritDamage, 100)),
            new GearBase(Greatsword, GearPart.Weapon, "대검", ArmorWeight.None, 100, 0, 0, B(OptionKind.CritDamage, 500)),
            new GearBase(Twinblades, GearPart.Weapon, "쌍검", ArmorWeight.None, 100, 0, 0, B(OptionKind.CritChance, 40), B(OptionKind.CritDamage, -200)),
            // 새 무기 6종(전투·보스·무기 다듬기 1차 2-4·2-6). 기대 치명 1.045 / 1.042 / 1.040 / 1.049 / 1.054 / 1.048.
            NewWeapon(Maul, "쇠망치", B(OptionKind.CritDamage, 400)),
            NewWeapon(Spear, "창", B(OptionKind.CritChance, 10), B(OptionKind.CritDamage, 200)),
            NewWeapon(Scythe, "큰 낫", B(OptionKind.CritChance, 30)),
            NewWeapon(Axe, "도끼", B(OptionKind.CritChance, 20), B(OptionKind.CritDamage, 200)),
            NewWeapon(Dagger, "단검", B(OptionKind.CritChance, 40), B(OptionKind.CritDamage, 100)),
            NewWeapon(Flail, "사슬 철퇴", B(OptionKind.CritChance, 10), B(OptionKind.CritDamage, 300)),

            new GearBase(LeatherArmor, GearPart.Armor, "가죽 갑옷", ArmorWeight.Light, 0, 60, 200, B(OptionKind.MoveSpeed, 30)),
            new GearBase(ChainArmor, GearPart.Armor, "사슬 갑옷", ArmorWeight.Medium, 0, 80, 300),
            new GearBase(PlateArmor, GearPart.Armor, "판금 갑옷", ArmorWeight.Heavy, 0, 120, 200, B(OptionKind.MoveSpeed, -30)),

            new GearBase(LeatherHelm, GearPart.Helm, "가죽 두건", ArmorWeight.Light, 0, 24, 80),
            new GearBase(ChainHelm, GearPart.Helm, "사슬 두건", ArmorWeight.Medium, 0, 32, 120, B(OptionKind.CooldownReduction, 30)),
            new GearBase(PlateHelm, GearPart.Helm, "판금 투구", ArmorWeight.Heavy, 0, 48, 80, B(OptionKind.BossDamage, 50)),

            new GearBase(LeatherGloves, GearPart.Gloves, "가죽 장갑", ArmorWeight.Light, 0, 18, 60),
            new GearBase(ChainGloves, GearPart.Gloves, "사슬 장갑", ArmorWeight.Medium, 0, 24, 90, B(OptionKind.AttackSpeed, 30)),
            new GearBase(PlateGloves, GearPart.Gloves, "판금 장갑", ArmorWeight.Heavy, 0, 36, 60, B(OptionKind.CritDamage, 100)),

            new GearBase(LeatherBoots, GearPart.Boots, "가죽 장화", ArmorWeight.Light, 0, 18, 60, B(OptionKind.MoveSpeed, 30)),
            new GearBase(ChainBoots, GearPart.Boots, "사슬 장화", ArmorWeight.Medium, 0, 24, 90),
            new GearBase(PlateBoots, GearPart.Boots, "판금 장화", ArmorWeight.Heavy, 0, 36, 60, B(OptionKind.MoveSpeed, -30)),

            new GearBase(IronRing, GearPart.Ring, "쇠 반지", ArmorWeight.None, 25, 0, 0),
            new GearBase(BloodRing, GearPart.Ring, "핏빛 반지", ArmorWeight.None, 20, 0, 0, B(OptionKind.CritChance, 20)),
            new GearBase(FangRing, GearPart.Ring, "송곳 반지", ArmorWeight.None, 20, 0, 0, B(OptionKind.CritDamage, 150)),

            new GearBase(FangAmulet, GearPart.Amulet, "이빨 목걸이", ArmorWeight.None, 0, 0, 200, B(OptionKind.CooldownReduction, 50)),
            new GearBase(CharmAmulet, GearPart.Amulet, "부적 목걸이", ArmorWeight.None, 0, 0, 200, B(OptionKind.MoveSpeed, 40)),
            new GearBase(AmberAmulet, GearPart.Amulet, "검은 호박 목걸이", ArmorWeight.None, 0, 0, 200, B(OptionKind.SkillDamage, 100)),
        };

        /// <summary>시작 장비 id 5개(4-4): 일반 한손검과 방패(id wpn_longsword) + 가죽 갑옷·가죽 두건·가죽 장갑·가죽 장화(모두 iLv1, 굴림 1000‰). 반지·목걸이는 비어 있다.</summary>
        public static readonly string[] StartingIds = { Longsword, LeatherArmor, LeatherHelm, LeatherGloves, LeatherBoots };

        /// <summary>27종 전체(부위 차례, 부위 안 문서 차례. 무기는 WeaponPresets.All 차례).</summary>
        public static IReadOnlyList<GearBase> All => Bases;

        /// <summary>id로 찾는다. 없으면 null.</summary>
        public static GearBase Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var b in Bases)
                if (b.Id == id) return b;
            return null;
        }

        /// <summary>그 부위의 모든 종류(층과 상관없이, 문서 차례, 새 목록). 무기 9종, 다른 부위 3종.</summary>
        public static List<GearBase> ForPart(GearPart part)
        {
            var list = new List<GearBase>(9);
            foreach (var b in Bases)
                if (b.Part == part) list.Add(b);
            return list;
        }

        /// <summary>
        /// 그 층에서 나올 수 있는 그 부위 종류(UnlockFloor ≤ 층, 문서 차례, 새 목록). 1층 무기 3종(한손검과 방패·대검·쌍검), 2층부터 9종.
        /// 드랍·궤짝(LootRules.RollGear)이 쓴다.
        /// </summary>
        public static List<GearBase> ForPart(GearPart part, int floor)
        {
            var list = new List<GearBase>(9);
            foreach (var b in Bases)
                if (b.Part == part && b.UnlockedAt(floor)) list.Add(b);
            return list;
        }

        /// <summary>그 부위의 첫 종류(가죽·한손검과 방패·쇠 반지·이빨 목걸이). 모르는 id를 고칠 때 쓴다.</summary>
        public static GearBase FirstOf(GearPart part)
        {
            foreach (var b in Bases)
                if (b.Part == part) return b;
            return Bases[0];
        }
    }
}
