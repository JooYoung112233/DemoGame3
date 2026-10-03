using System;
using Demo6.Core.Combat;
using Demo6.Core.Loot;

namespace Demo6.Core.Stats
{
    /// <summary>능력치 13가지(장비 문서 2-1, 2차 13개). 초당 휘두르기·무너뜨리기는 보이기만 하는 값이라 여기 없다(StatCalc 함수).</summary>
    public enum StatKind
    {
        MaxHp,
        Attack,
        Defense,
        /// <summary>‰.</summary>
        CritChance,
        /// <summary>‰(1500 = 150%).</summary>
        CritDamage,
        /// <summary>‰. 기본공격 동작 길이를 (1 + 이 값)으로 나눈다(3-3).</summary>
        AttackSpeed,
        /// <summary>‰. 전투 걸음 5.0 × (1 + 이 값).</summary>
        MoveSpeed,
        /// <summary>‰. 회오리 6초·검풍 9초 × (1 − 이 값).</summary>
        CooldownReduction,
        /// <summary>‰. 스킬 피해에만 곱함.</summary>
        SkillDamage,
        /// <summary>‰. 보스에게만 곱함.</summary>
        BossDamage,
        /// <summary>‰. 기본공격·스킬 피해에만(전설 피해 뺌).</summary>
        LifeSteal,
        /// <summary>초당 체력 재생(고정).</summary>
        HpRegen,
        /// <summary>처치 시 체력 회복(고정).</summary>
        OnKillHeal,
    }

    /// <summary>능력치 출처(출처별 몫). Weapon~Amulet은 GearSlot 차례 + 2다.</summary>
    public enum StatSource
    {
        /// <summary>맨몸(체력 2,000, 공격 100, 치명 50‰/1,500‰).</summary>
        Bare,
        /// <summary>레벨 체력(3차 4-3·4-5).</summary>
        Level,
        Weapon,
        Armor,
        Helm,
        Gloves,
        Boots,
        Ring1,
        Ring2,
        Amulet,
        /// <summary>전투 시험장 손잡이(StatOverrides).</summary>
        Test,
    }

    public static class StatSources
    {
        public const int Count = 11;

        public static StatSource Of(GearSlot slot) => (StatSource)(2 + (int)slot);
    }

    /// <summary>맨몸 값(장비 문서 2-1 '맨몸' 열).</summary>
    public static class StatBase
    {
        public const int BareHp = 2000;
        public const int BareAttack = 100;
        public const int CritChancePermille = 50;
        public const int CritDamagePermille = 1500;
        /// <summary>전투 걸음 기본 속도(초당 유닛, Tuning.MoveSpeedScale을 곱하기 전).</summary>
        public const float MoveSpeed = 5.0f;
    }

    /// <summary>상한(장비 문서 2-1·7장, 10배 단위·‰).</summary>
    public static class StatCaps
    {
        public const int CritChancePermille = 500;
        public const int CritDamagePermille = 3000;
        /// <summary>치명 피해 아래끝(치명이 보통 타보다 약해지지 않게).</summary>
        public const int CritDamageMinPermille = 1000;
        public const int AttackSpeedPermille = 300;
        public const int MoveSpeedPermille = 300;
        public const int CooldownReductionPermille = 400;
        public const int LifeStealPermille = 30;
        /// <summary>방어 상한 4,000(받는 피해 −80%).</summary>
        public const int Defense = 4000;
    }

    /// <summary>
    /// 한 곳에서 계산한 플레이어 능력치(장비 문서 2-3). StatCalc.Compute만 만들고, PlayerController.ApplyStats 한 입구로 넣는다.
    /// 값은 상한을 적용한 최종값이다. 출처별 몫(Flat·Percent)은 상한·% 곱하기 전에 더한 값이라 화면 능력치 표와 시험에 쓴다.
    /// 늘 성립: 종류마다 Uncapped = Σ Flat × (1 + Σ Percent)(체력·공격력, 0.5 올림 한 번) 또는 Σ Flat(나머지), 최종값 = 상한으로 자른 Uncapped.
    /// 전투 시험장 손잡이(StatOverrides)도 Test 몫으로 들어가므로 이 관계는 손잡이를 써도 깨지지 않는다(StatCalcTests).
    /// 계약(꾸러미 ①이 채우고 시험): 공개 이름·단위는 꾸러미 ③④⑥이 그대로 쓴다.
    /// </summary>
    public sealed class StatSheet
    {
        public const int KindCount = 13;

        readonly int[] _final = new int[KindCount];
        readonly int[] _uncapped = new int[KindCount];
        readonly int[,] _flat = new int[KindCount, StatSources.Count];
        readonly int[,] _percent = new int[KindCount, StatSources.Count];
        readonly int[] _legendary = { -1, -1, -1 };

        /// <summary>최종값(상한 적용).</summary>
        public int Get(StatKind kind) => _final[(int)kind];
        /// <summary>상한 적용 전 값(% 곱하기까지 한 값).</summary>
        public int Uncapped(StatKind kind) => _uncapped[(int)kind];
        /// <summary>그 출처가 더한 몫(고정값 또는 ‰).</summary>
        public int Flat(StatKind kind, StatSource source) => _flat[(int)kind, (int)source];
        /// <summary>그 출처가 더한 % 몫(‰, 체력%·공격력%만).</summary>
        public int Percent(StatKind kind, StatSource source) => _percent[(int)kind, (int)source];

        public int FlatSum(StatKind kind)
        {
            int sum = 0;
            for (int s = 0; s < StatSources.Count; s++) sum += _flat[(int)kind, s];
            return sum;
        }

        public int PercentSum(StatKind kind)
        {
            int sum = 0;
            for (int s = 0; s < StatSources.Count; s++) sum += _percent[(int)kind, s];
            return sum;
        }

        public int MaxHp => Get(StatKind.MaxHp);
        public int Attack => Get(StatKind.Attack);
        public int Defense => Get(StatKind.Defense);
        public int CritChancePermille => Get(StatKind.CritChance);
        public int CritDamagePermille => Get(StatKind.CritDamage);
        public int AttackSpeedPermille => Get(StatKind.AttackSpeed);
        public int MoveSpeedPermille => Get(StatKind.MoveSpeed);
        public int CooldownReductionPermille => Get(StatKind.CooldownReduction);
        public int SkillDamagePermille => Get(StatKind.SkillDamage);
        public int BossDamagePermille => Get(StatKind.BossDamage);
        public int LifeStealPermille => Get(StatKind.LifeSteal);
        public int HpRegen => Get(StatKind.HpRegen);
        public int OnKillHeal => Get(StatKind.OnKillHeal);

        /// <summary>낀 무기 종류 id(WeaponPresets id). 무기가 없으면 장검.</summary>
        public string WeaponId { get; internal set; } = GearBaseTable.Longsword;

        /// <summary>낀 무기 공격 규칙(모르는 id면 장검).</summary>
        public WeaponAttackRule WeaponRule => WeaponItem.RuleOf(WeaponId);

        /// <summary>작동하는 전설 효과 세기(‰, 0~1000). 없으면 -1(겹침 규칙 적용 뒤).</summary>
        public int LegendaryRollPermille(LegendaryEffect effect) => _legendary[(int)effect];
        public bool HasLegendary(LegendaryEffect effect) => _legendary[(int)effect] >= 0;

        // ── 게임 단위(PlayerController가 그대로 넣음) ──
        /// <summary>치명 확률(0~1).</summary>
        public float CritChance => CritChancePermille / 1000f;
        /// <summary>치명 피해 배율(1.6 = 160%).</summary>
        public float CritDamage => CritDamagePermille / 1000f;
        /// <summary>s = 1 + 공격 속도(3-3).</summary>
        public float AttackSpeedFactor => 1f + AttackSpeedPermille / 1000f;
        /// <summary>
        /// 전투 걸음 속도(Tuning.MoveSpeedScale 곱하기 전) = 5.0 × (1 + 이동‰ ÷ 1000). 시작 +60‰이면 예전 상수
        /// PlayerController.EquippedWalkSpeed(= 5.0f × (1f + 0.06f), 화면 5.3)와 비트까지 같다.
        /// 단계마다 float로 자르는 형변환은 실행기가 float를 더 넓은 정밀도로 계산해도 컴파일러가 접은 예전 상수와 같은 값을 내게 한다.
        /// </summary>
        public float MoveSpeed => StatBase.MoveSpeed * (float)(1f + (float)(MoveSpeedPermille / 1000f));
        /// <summary>스킬 재사용 배율 = 1 − 감소.</summary>
        public float CooldownFactor => 1f - CooldownReductionPermille / 1000f;

        internal void AddFlat(StatKind kind, StatSource source, int value) => _flat[(int)kind, (int)source] += value;
        internal void AddPercent(StatKind kind, StatSource source, int value) => _percent[(int)kind, (int)source] += value;

        internal void SetFinal(StatKind kind, int uncapped, int final)
        {
            _uncapped[(int)kind] = uncapped;
            _final[(int)kind] = final;
        }

        internal void SetLegendary(int[] rolls)
        {
            for (int i = 0; i < _legendary.Length; i++) _legendary[i] = rolls != null && i < rolls.Length ? rolls[i] : -1;
        }

        internal void SetLegendary(LegendaryEffect effect, int roll) => _legendary[(int)effect] = roll;

        public override string ToString() =>
            $"체력 {MaxHp} · 공격 {Attack} · 방어 {Defense} · 치명 {CritChancePermille}‰/{CritDamagePermille}‰ · 공속 {AttackSpeedPermille}‰ · 이동 {MoveSpeedPermille}‰";
    }
}
