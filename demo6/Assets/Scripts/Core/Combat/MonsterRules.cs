using System;

namespace Demo6.Core.Combat
{
    public enum MonsterKind
    {
        Rat,
        Boar,
        Archer,
        /// <summary>굴쥐 둥지(3차 초안 3-4). 움직이지 않고 굴쥐를 부른다.</summary>
        Nest,
        /// <summary>첫 보스 갱도 오우거(기획/전투-보스-무기-다듬기-1차.md 3장). 수치는 BossRules가 식으로 낸다(OgreBrain이 스폰 뒤 다시 넣음).</summary>
        Ogre,
    }

    /// <summary>기획 4-2 몬스터 기본값(1층, 10배 단위).</summary>
    public readonly struct MonsterRule
    {
        public readonly MonsterKind Kind;
        public readonly string DisplayName;
        public readonly int Hp;
        public readonly int Attack;
        public readonly float MoveSpeed;
        public readonly float Diameter;
        public readonly float KnockbackResist;

        public MonsterRule(MonsterKind kind, string displayName, int hp, int attack, float moveSpeed, float diameter, float knockbackResist)
        {
            Kind = kind;
            DisplayName = displayName;
            Hp = hp;
            Attack = attack;
            MoveSpeed = moveSpeed;
            Diameter = diameter;
            KnockbackResist = knockbackResist;
        }

        public static readonly MonsterRule Rat = new MonsterRule(MonsterKind.Rat, "굴쥐", 160, 60, 4.2f, 0.5f, 0f);
        /// <summary>공격 300은 돌진. 머리치기는 돌진의 60%.</summary>
        public static readonly MonsterRule Boar = new MonsterRule(MonsterKind.Boar, "돌충이", 900, 300, 2.2f, 1.1f, 0.5f);
        public static readonly MonsterRule Archer = new MonsterRule(MonsterKind.Archer, "가시 궁수", 400, 100, 3.0f, 0.6f, 0f);

        /// <summary>3차 초안 3-3: 적게·강하게. 굴쥐는 그대로, 궁수 600/150, 멧돼지 2,000/400.</summary>
        public static readonly MonsterRule ArcherV3 = new MonsterRule(MonsterKind.Archer, "가시 궁수", 600, 150, 3.0f, 0.6f, 0f);
        public static readonly MonsterRule BoarV3 = new MonsterRule(MonsterKind.Boar, "돌충이", 2000, 400, 2.2f, 1.1f, 0.5f);
        /// <summary>멧돼지가 너무 질기면 먼저 내리는 값(초안 3-4 'M0a 손맛 지키기').</summary>
        public static readonly MonsterRule BoarV3Soft = new MonsterRule(MonsterKind.Boar, "돌충이", 1600, 400, 2.2f, 1.1f, 0.5f);
        /// <summary>껍질이 깨진 뒤 체력 1,200 × 층 배율. 넉백 무시.</summary>
        public static readonly MonsterRule Nest = new MonsterRule(MonsterKind.Nest, "굴쥐 둥지", 1200, 0, 0f, 1.6f, 1f);
        /// <summary>갱도 오우거 기본값(10층판 체력 32,000 · 공격 300 · 걷기 2.8 · 지름 2.4 · 넉백 저항 1). 층별 체력·공격·버팀은 BossRules.</summary>
        public static readonly MonsterRule Ogre = new MonsterRule(MonsterKind.Ogre, BossRules.DisplayName, BossRules.BaseHp, BossRules.BaseAttack, BossRules.WalkSpeed, BossRules.Diameter, 1f);

        public static MonsterRule Of(MonsterKind kind) => Of(kind, CombatRuleset.M0a);

        public static MonsterRule Of(MonsterKind kind, CombatRuleset rules, bool softBoar = false)
        {
            bool v3 = rules == CombatRuleset.V3;
            switch (kind)
            {
                case MonsterKind.Rat: return Rat;
                case MonsterKind.Boar: return v3 ? (softBoar ? BoarV3Soft : BoarV3) : Boar;
                case MonsterKind.Archer: return v3 ? ArcherV3 : Archer;
                case MonsterKind.Nest: return Nest;
                case MonsterKind.Ogre: return Ogre;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        /// <summary>3차 버팀(초안 3-4). 굴쥐 없음, 궁수 30, 멧돼지 90(약하게 70), 둥지 없음(껍질).</summary>
        public static double PoiseOf(MonsterKind kind, bool softBoar = false)
        {
            switch (kind)
            {
                case MonsterKind.Archer: return 30;
                case MonsterKind.Boar: return softBoar ? 70 : 90;
                // 시험판(2층) 값. 층별 값은 BossRules.Poise(층)이고 OgreBrain이 보스 모드 버팀으로 다시 넣는다.
                case MonsterKind.Ogre: return BossRules.Poise(2);
                default: return 0;
            }
        }
    }

    /// <summary>M0a(전투 시험 1차) 값과 3차 초안(적게·강하게) 값.</summary>
    public enum CombatRuleset
    {
        M0a,
        V3,
    }

    public readonly struct PlayerBaseline
    {
        public readonly int Floor;
        public readonly int Attack;
        public readonly int MaxHp;
        public readonly int Defense;

        public PlayerBaseline(int floor, int attack, int maxHp, int defense)
        {
            Floor = floor;
            Attack = attack;
            MaxHp = maxHp;
            Defense = defense;
        }
    }

    /// <summary>기획 4-8 층 배율. 굴쥐 체력만 따로 성장한다.</summary>
    public static class FloorScaling
    {
        public const int MinFloor = 1;
        public const int MaxFloor = 10;
        public const double HpGrowth = 1.27;
        public const double AttackGrowth = 1.24;
        public const double RatHpGrowth = 1.25;

        /// <summary>2차 기획 4-8 '기준 장비'(그 층 첫 진입 때 능력치 중앙값).</summary>
        static readonly PlayerBaseline[] Baselines =
        {
            new PlayerBaseline(1, 200, 2400, 120),
            new PlayerBaseline(2, 288, 2400, 120),
            new PlayerBaseline(3, 331, 2770, 210),
            new PlayerBaseline(4, 412, 3288, 335),
            new PlayerBaseline(5, 524, 3796, 500),
            new PlayerBaseline(6, 640, 4298, 628),
            new PlayerBaseline(7, 805, 4802, 810),
            new PlayerBaseline(8, 972, 5340, 989),
            new PlayerBaseline(9, 1193, 6137, 1270),
            new PlayerBaseline(10, 1462, 7076, 1600),
        };

        public static int Clamp(int floor) => Math.Max(MinFloor, Math.Min(MaxFloor, floor));

        public static PlayerBaseline Baseline(int floor) => Baselines[Clamp(floor) - 1];

        public static double HpMultiplier(MonsterKind kind, int floor) =>
            Math.Pow(kind == MonsterKind.Rat ? RatHpGrowth : HpGrowth, Clamp(floor) - 1);

        public static double AttackMultiplier(int floor) => Math.Pow(AttackGrowth, Clamp(floor) - 1);

        public static int MonsterHp(MonsterKind kind, int floor) =>
            DamageMath.RoundHalfUp(MonsterRule.Of(kind).Hp * HpMultiplier(kind, floor));

        public static int MonsterAttack(MonsterKind kind, int floor) =>
            DamageMath.RoundHalfUp(MonsterRule.Of(kind).Attack * AttackMultiplier(floor));

        public static int MonsterHp(MonsterRule rule, int floor) =>
            DamageMath.RoundHalfUp(rule.Hp * HpMultiplier(rule.Kind, floor));

        public static int MonsterAttack(MonsterRule rule, int floor) =>
            DamageMath.RoundHalfUp(rule.Attack * AttackMultiplier(floor));

        /// <summary>6층부터 궁수 3갈래.</summary>
        public static bool ArcherTripleShot(int floor) => floor >= 6;

        /// <summary>8층부터 멧돼지 2연속 돌진.</summary>
        public static bool BoarDoubleCharge(int floor) => floor >= 8;
    }
}
