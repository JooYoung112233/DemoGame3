namespace Demo6.Core.Combat
{
    /// <summary>벽 박기 결과 종류.</summary>
    public enum WallSlamKind
    {
        /// <summary>아무 일 없음(보스·둥지·허수아비, 같은 적 0.5초 안).</summary>
        None,
        /// <summary>그 타 피해 50% 추가 + 벽 피 자국(굴쥐).</summary>
        ExtraDamage,
        /// <summary>바로 무너짐(궁수, 쇠망치 땅 울리기에 박힌 멧돼지).</summary>
        Break,
        /// <summary>버팀 피해 = 최대치 35% + 0.15초 움찔(멧돼지, 정예는 × 0.5).</summary>
        PoiseHit,
    }

    /// <summary>벽 박기 결과 하나(WallSlamRule.Resolve).</summary>
    public readonly struct WallSlamResult
    {
        public readonly WallSlamKind Kind;
        /// <summary>ExtraDamage: 그 타 피해에 곱해 더할 몫(0.5).</summary>
        public readonly float ExtraDamageFraction;
        /// <summary>PoiseHit: 버팀 최대치에 곱할 몫(0.35, 정예 0.175).</summary>
        public readonly float PoiseFractionOfMax;
        /// <summary>PoiseHit: 움찔 시간(초).</summary>
        public readonly float FlinchSeconds;
        /// <summary>벽 피 자국을 남기는가.</summary>
        public readonly bool BloodMark;

        public WallSlamResult(WallSlamKind kind, float extraDamageFraction, float poiseFractionOfMax, float flinchSeconds, bool bloodMark)
        {
            Kind = kind;
            ExtraDamageFraction = extraDamageFraction;
            PoiseFractionOfMax = poiseFractionOfMax;
            FlinchSeconds = flinchSeconds;
            BloodMark = bloodMark;
        }

        public static WallSlamResult Nothing => new WallSlamResult(WallSlamKind.None, 0f, 0f, 0f, false);
    }

    /// <summary>
    /// 벽·기둥 박기 공용 규칙 하나(기획/전투-보스-무기-다듬기-1차.md 4-2 [1]). 멧돼지 자기 돌진(BoarBrain, 지금처럼 바로 무너짐),
    /// 오우거 B 마지막 돌진(OgreBrain 안 판정, 버팀 즉시 0), 쇠망치 땅 울리기(단계 깃발 wallBreak = 한 칸 위 결과), 일반 넉백이 이 표 하나를 쓴다.
    /// 판정: 살아 있는 적이 넉백으로 실제 MinPush(0.5) 이상 밀리는 동안(0.1초 안) 벽·기둥에 닿음. 같은 적은 Cooldown(0.5초)에 한 번.
    /// 넉백 저항 1인 적(돌진 중 멧돼지, 정예 멧돼지, 보스)은 밀리지 않아 자연히 빠진다. 벽 닿음 판정은 Enemy 넉백 자리 한 곳(꾸러미 ⑧)이 하고
    /// CombatEvents.EnemyWallSlam을 낸다. 결과 적용·연출은 Game의 WallSlam(꾸러미 ③)이 한다.
    /// 계약(꾸러미 ③이 채우고 WallSlamRuleTests로 지킨다). 계약 단계의 Resolve는 표 그대로 구현돼 있다.
    /// </summary>
    public static class WallSlamRule
    {
        /// <summary>Tuning.WallSlamOn 기본값(무너짐 공급원은 하나씩 켠다, 4-3).</summary>
        public const bool DefaultOn = true;
        /// <summary>이만큼 이상 밀리는 넉백만 벽 박기가 된다(저항·배율을 곱한 실제 거리).</summary>
        public const float MinPush = 0.5f;
        /// <summary>같은 적은 이 시간에 한 번.</summary>
        public const float Cooldown = 0.5f;
        /// <summary>굴쥐: 그 타 피해 50% 추가.</summary>
        public const float LightExtraDamage = 0.5f;
        /// <summary>멧돼지: 버팀 최대치 35%, 정예는 × 0.5.</summary>
        public const float HeavyPoiseFraction = 0.35f;
        public const float EliteFactor = 0.5f;
        public const float FlinchSeconds = 0.15f;
        /// <summary>연출: 히트스톱 0.05, 흙먼지 6알, 둔탁한 '쿵'(타격음 음높이 0.7).</summary>
        public const float HitStop = 0.05f;
        public const int DustCount = 6;
        public const float ThudPitch = 0.7f;

        /// <summary>
        /// 넉백 벽 박기 결과(4-2 [1] 표). wallBreak = 쇠망치 땅 울리기(한 칸 위: 멧돼지도 바로 무너짐, 정예·보스·둥지·허수아비는 올리지 않음).
        /// </summary>
        public static WallSlamResult Resolve(TargetClass target, bool wallBreak)
        {
            if (target.Immune) return WallSlamResult.Nothing;
            if (target.Elite)
                return new WallSlamResult(WallSlamKind.PoiseHit, 0f, HeavyPoiseFraction * EliteFactor, FlinchSeconds, true);
            switch (target.Weight)
            {
                case WeightClass.Light:
                    return new WallSlamResult(WallSlamKind.ExtraDamage, LightExtraDamage, 0f, 0f, true);
                case WeightClass.Medium:
                    return new WallSlamResult(WallSlamKind.Break, 0f, 0f, 0f, true);
                default:
                    return wallBreak
                        ? new WallSlamResult(WallSlamKind.Break, 0f, 0f, 0f, true)
                        : new WallSlamResult(WallSlamKind.PoiseHit, 0f, HeavyPoiseFraction, FlinchSeconds, true);
            }
        }

        /// <summary>벽 박기가 될 만큼 밀렸는가(실제 밀린 거리, 저항·배율 곱한 값).</summary>
        public static bool PushedEnough(float pushDistance) => pushDistance >= MinPush;

        /// <summary>같은 적 0.5초 제한: 마지막 벽 박기 시각에서 now까지 Cooldown 이상이면 true(처음이면 lastSlam에 음의 무한대).</summary>
        public static bool Ready(double lastSlam, double now) => now - lastSlam >= Cooldown;
    }
}
