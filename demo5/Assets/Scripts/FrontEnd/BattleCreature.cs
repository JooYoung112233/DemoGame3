using System;
using UnityEngine;

namespace Demo5.FrontEnd
{
    // How a creature hurts people.
    // Bite and Swarm track their target and roll to hit. Every other kind is a committed strike:
    // on one enemy turn the creature winds up and marks ally cells, on its next turn it hits whoever still stands there.
    public enum CreatureAttack { Bite, Shove, Listen, Pounce, Charge, Veil, Wire, Swarm, Grab, Collapse, Twin, Broadcast, Ram }
    // Walk: one cell a turn. Fast: two. Slow: rests after every move. Rooted: never moves.
    public enum CreatureGait { Walk, Fast, Slow, Rooted }

    // A tactical description, not a second AI. Attack/Gait remain the source of battle behaviour.
    public enum CreatureRole { Melee = 0, Defender = 1, Assault = 2, Ranged = 3, Disruptor = 4, Support = 5 }

    public static class CreatureRoles
    {
        // The original infected has no BattleCreature asset.
        public static CreatureRole For(BattleCreature creature) => creature?.Role ?? CreatureRole.Melee;
        public static string Name(CreatureRole role)
        {
            switch (role)
            {
                case CreatureRole.Defender: return "방어형";
                case CreatureRole.Assault: return "공격형";
                case CreatureRole.Ranged: return "원거리형";
                case CreatureRole.Disruptor: return "방해형";
                case CreatureRole.Support: return "지원형";
                default: return "근접형";
            }
        }
        public static string Summary(CreatureRole role)
        {
            switch (role)
            {
                case CreatureRole.Defender: return "체력과 방어가 높음 · 측면이나 공격 뒤 빈틈 공략";
                case CreatureRole.Assault: return "예고 뒤 강한 공격 · 표시된 칸에서 이탈";
                case CreatureRole.Ranged: return "떨어진 칸도 공격 · 사거리보다 예고 칸 확인";
                case CreatureRole.Disruptor: return "낮은 피해와 방해 효과 · 밀집을 피하고 예고 확인";
                case CreatureRole.Support: return "직접 피해 없음 · 소음으로 증원을 부름";
                default: return "다가와 물기 · 거리와 방어로 대응";
            }
        }
    }

    // One creature type. Numbers are provisional and live in Assets/Data/BattleCreatures.asset (Inspector).
    [Serializable] public sealed class BattleCreature
    {
        [Header("이름과 그림")]
        public string Id = "creature";
        public string Name = "무언가";
        public Sprite Body;
        [Tooltip("몸이 보이는 원본 픽셀 범위(아래가 0). 비우면 스프라이트 전체")]
        public Rect Visible;
        [Tooltip("받침대 위 몸 높이(월드 단위). 기존 대원은 1.72")]
        [Min(.3f)] public float Height = 1.72f;
        [Tooltip("받침대 위로 떠 있는 높이. 날거나 떠 있는 종만")]
        [Min(0)] public float Hover;

        [Header("전투 역할 · 수치")]
        public CreatureRole Role = CreatureRole.Melee;
        [Min(1)] public int Health = 4;
        [Tooltip("모든 공격의 피해를 줄인다")]
        [Min(0)] public int Armor;
        [Tooltip("같은 가로줄에서 정면으로 친 공격만 추가로 줄인다 (문짝)")]
        [Min(0)] public int FrontArmor;
        [Tooltip("사격 피해만 추가로 줄인다")]
        [Min(0)] public int ShotArmor;
        [Tooltip("사격 명중률에서 빼는 값")]
        [Range(0, 60)] public int ShotEvasion;
        [Min(0)] public int Damage = 1;
        [Tooltip("물기·군집처럼 굴려서 맞히는 공격의 기본 명중률")]
        [Range(0, 100)] public int HitChance = 70;
        [Tooltip("-1이면 전투 규칙의 등장 줄을 쓴다")]
        [Range(-1, 2)] public int StartDepth = -1;
        [Tooltip("이 줄보다 앞으로 나오지 않는다 (0 전열)")]
        [Range(0, 2)] public int HoldDepth;
        public CreatureAttack Attack;
        public CreatureGait Gait;
        [Tooltip("같은 가로줄 바로 뒤의 적이 사격을 덜 맞는다")]
        public bool Cover;
        [Tooltip("조우 뽑기 가중치. 0이면 나오지 않는다")]
        [Min(0)] public int Weight = 1;
        [Tooltip("무작위 조우를 허용할 지역 단계. 0은 폐상가 도입, 1 이상은 향후 지역. 고정 조우의 Find에는 적용하지 않는다.")]
        [Min(0)] public int MinRegionTier = 1;
        [Tooltip("이 개체 하나만 지정한 고정 조우에서는 소음 증원을 부르지 않는다")]
        public bool SuppressReinforcements;

        [Header("문구")]
        public string AttackName = "물기";
        public string WindupName = "준비";
        [Tooltip("대상 정보와 조준 카드에 쓰는 한 줄 특징")]
        public string Trait = "";
        [Tooltip("대응 요령 한 줄")]
        public string Counter = "";

        [Header("손맛")]
        public Color Accent = new Color(.86f, .42f, .33f);
        public AudioClip WindupSound, StrikeSound, ImpactSound;
        [Range(.5f, 1.5f)] public float Pitch = 1;

        public bool Committed => Attack != CreatureAttack.Bite && Attack != CreatureAttack.Swarm;
        public bool LaneAttack => Attack == CreatureAttack.Shove || Attack == CreatureAttack.Charge || Attack == CreatureAttack.Ram || Attack == CreatureAttack.Veil;
        public string RoleName => CreatureRoles.Name(Role);
        public string RoleSummary => CreatureRoles.Summary(Role);
        public bool AvailableInRegion(int regionTier) => Weight > 0 && regionTier >= Math.Max(0, MinRegionTier);
    }
}
