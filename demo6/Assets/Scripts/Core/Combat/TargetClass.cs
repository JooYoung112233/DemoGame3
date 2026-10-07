namespace Demo6.Core.Combat
{
    /// <summary>적 무게(Game의 EnemyWeight와 같은 차례). 가벼움 = 굴쥐, 보통 = 궁수, 무거움 = 멧돼지·정예·보스.</summary>
    public enum WeightClass
    {
        Light,
        Medium,
        Heavy,
    }

    /// <summary>
    /// 규칙 함수가 읽는 적 분류(엔진을 모름). Game의 Enemy.Class가 만든다.
    /// 벽 박기(WallSlamRule)·무리 공포(FearRule)·처형(ExecutionRule)이 같은 분류를 쓴다.
    /// 계약(꾸러미 ③이 소유, 다른 꾸러미는 읽기만): 필드 이름·뜻은 바꾸지 않는다.
    /// </summary>
    public readonly struct TargetClass
    {
        public readonly MonsterKind Kind;
        public readonly WeightClass Weight;
        public readonly bool Elite;
        /// <summary>이름난 정예(지금은 없음, 처형 문턱 10%).</summary>
        public readonly bool Named;
        public readonly bool Boss;
        /// <summary>허수아비(나무·쥐). 규칙 반응 없음.</summary>
        public readonly bool Dummy;

        public TargetClass(MonsterKind kind, WeightClass weight, bool elite, bool named, bool boss, bool dummy)
        {
            Kind = kind;
            Weight = weight;
            Elite = elite;
            Named = named;
            Boss = boss;
            Dummy = dummy;
        }

        public bool Nest => Kind == MonsterKind.Nest;

        /// <summary>규칙이 듣지 않는 대상(보스·둥지·허수아비).</summary>
        public bool Immune => Boss || Nest || Dummy;
    }
}
