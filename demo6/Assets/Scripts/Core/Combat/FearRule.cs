namespace Demo6.Core.Combat
{
    /// <summary>
    /// 무리 공포(기획/전투-보스-무기-다듬기-1차.md 4-2 [2]): 무거운 적이나 정예가 죽으면 반경 6 안 굴쥐가 겁먹는다(처형이면 더 길게).
    /// 겁먹은 굴쥐는 플레이어 반대쪽으로 속도 110%로 달아나고, 공격 기회를 내놓고, 하던 예고를 취소한다. 칸 경계에서는 웅크리기만 한다.
    /// '무리 거느린' 정예가 죽으면 졸개는 4초 겁먹고 50%는 칸 밖 어둠으로 사라진다(보상 없는 졸개). 둥지 굴쥐는 1.0초로 짧다.
    /// 글자는 띄우지 않고 몸 떨림과 찍찍 소리만 쓴다. 보스 처치 뒤 소환 굴쥐가 벽 틈으로 도망치는 연출도 이 규칙을 쓴다.
    /// 계약(꾸러미 ③이 채우고 FearRuleTests로 지킨다).
    /// </summary>
    public static class FearRule
    {
        /// <summary>Tuning.FearOn 기본값.</summary>
        public const bool DefaultOn = true;
        public const float Radius = 6f;
        /// <summary>무거운 적·정예 처치 1.5초, 처형 2.5초, 무리 거느린 정예 4.0초, 둥지 굴쥐 1.0초.</summary>
        public const float HeavyDeathSeconds = 1.5f;
        public const float ExecutionSeconds = 2.5f;
        public const float PackLeaderSeconds = 4.0f;
        public const float NestRatSeconds = 1.0f;
        /// <summary>무리 거느린 정예가 죽으면 졸개가 사라질 확률.</summary>
        public const float PackVanishChance = 0.5f;
        /// <summary>달아나는 속도 배율(굴쥐 걸음 × 1.1).</summary>
        public const float FleeSpeedScale = 1.1f;

        /// <summary>이 적의 죽음이 공포를 부르는가: 무거운 적·정예·보스(소환 굴쥐 흩어짐). 허수아비·둥지는 아님.</summary>
        public static bool Triggers(TargetClass dead) => !dead.Dummy && !dead.Nest && (dead.Weight == WeightClass.Heavy || dead.Elite || dead.Boss);

        /// <summary>겁먹는 시간(초). 둥지 굴쥐면 짧게, 무리 거느린 정예의 졸개면 길게, 처형이면 2.5.</summary>
        public static float Seconds(bool execution, bool packLeaderDied, bool nestRat)
        {
            if (nestRat) return NestRatSeconds;
            if (packLeaderDied) return PackLeaderSeconds;
            return execution ? ExecutionSeconds : HeavyDeathSeconds;
        }
    }
}
