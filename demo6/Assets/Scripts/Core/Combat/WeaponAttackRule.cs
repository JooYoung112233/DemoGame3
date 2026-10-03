using System;

namespace Demo6.Core.Combat
{
    public enum ComboShape
    {
        /// <summary>앞쪽 부채꼴. size = 사거리.</summary>
        Arc,
        /// <summary>앞쪽 직선 띠(관통). size = 길이, width = 폭.</summary>
        Line,
        /// <summary>원. size = 반지름, centerOffset = 앞쪽으로 옮긴 거리(0이면 내 주변).</summary>
        Circle,
    }

    /// <summary>콤보 한 단계. 시간은 초, 배율은 %.</summary>
    [Serializable]
    public sealed class ComboStep
    {
        public string name;
        public ComboShape shape;
        public float arcDeg;
        public float size;
        public float width;
        public float centerOffset;
        public int maxTargets;
        /// <summary>이 단계 동작 길이(다음 단계가 이어질 수 있는 때까지).</summary>
        public float duration;
        /// <summary>첫 판정이 나가는 시점(동작 길이 대비 비율).</summary>
        public float hitMoment = 0.35f;
        public int hits = 1;
        public float hitInterval;
        public float hitPercent;
        public float knockback;
        /// <summary>여러 타 중 마지막 타에만 넉백을 준다.</summary>
        public bool knockbackOnLastHitOnly;
        /// <summary>동작 중 이동속도 배율.</summary>
        public float moveScale = 0.4f;
        /// <summary>동작을 시작하며 앞으로 내딛는 거리.</summary>
        public float advance;
        public bool finisher;
        /// <summary>맞혔을 때 히트스톱(초). 치명·처치는 0.06 이상으로 올린다.</summary>
        public float hitStop = 0.04f;
        /// <summary>맞혔을 때 화면 흔들림 세기(0이면 없음).</summary>
        public float shake;
        /// <summary>3차 초안 3-4: 타마다 깎는 버팀(숨은 숫자, 치명 ×1.5). 마무리가 '무너뜨리기'를 맡는다.</summary>
        public float poiseDamage;

        /// <summary>한 대상이 이 단계에서 받는 배율 합(%).</summary>
        public float PercentPerTarget => hits * hitPercent;

        /// <summary>플레이어 중심에서 판정이 닿는 가장 먼 거리.</summary>
        public float Reach
        {
            get
            {
                switch (shape)
                {
                    case ComboShape.Line: return size;
                    case ComboShape.Circle: return centerOffset + size;
                    default: return size;
                }
            }
        }
    }

    /// <summary>
    /// 기획 3-2 무기 종류. 같은 코드에 데이터만 다르다.
    /// M0a 판정(2026-10-02) 뒤 단계 콤보 + 마무리로 바꿨다. 콤보 한 바퀴의 단일 대상 초당 계수는 기획 값(1.50 / 1.45 / 1.60)을 지킨다.
    /// </summary>
    [Serializable]
    public sealed class WeaponAttackRule
    {
        public string id;
        public string displayName;
        public ComboStep[] combo;
        /// <summary>이 시간 동안 다음 단계를 시작하지 않으면 1단계로 돌아간다.</summary>
        public float comboResetTime = 0.5f;

        /// <summary>자동 조준이 대상을 찾는 기준 사거리(일반 단계 중 가장 긴 것).</summary>
        public float BaseRange
        {
            get
            {
                float r = 0f;
                foreach (var s in combo)
                    if (!s.finisher && s.Reach > r) r = s.Reach;
                return r > 0f ? r : combo[0].Reach;
            }
        }

        public float CycleSeconds
        {
            get
            {
                float t = 0f;
                foreach (var s in combo) t += s.duration;
                return t;
            }
        }

        /// <summary>단일 대상 초당 계수 = 콤보 한 바퀴 배율 합 ÷ 한 바퀴 시간.</summary>
        public double SingleTargetCoefficient
        {
            get
            {
                double percent = 0;
                foreach (var s in combo) percent += s.PercentPerTarget;
                return percent / 100.0 / CycleSeconds;
            }
        }

        // ── 카드에 보이는 무기 성격(장비 문서 3-1). 읽기 전용, 데이터는 그대로. 계약: 꾸러미 ①이 시험한다 ──

        /// <summary>초당 휘두르기(공격 속도 0) = 콤보 동작 수 ÷ 한 바퀴 시간. 장검 1.40, 대검 0.97, 쌍검 1.54.</summary>
        public double SwingsPerSecond => combo.Length / CycleSeconds;

        /// <summary>
        /// 한 방 세기(타 평균 배율 %) = 한 바퀴 배율 합 ÷ 한 바퀴 타 수. 장검 107(90·90·140), 대검 150(115·115·220), 쌍검 46(타당).
        /// </summary>
        public double AverageHitPercent
        {
            get
            {
                double percent = 0;
                int hits = 0;
                foreach (var s in combo)
                {
                    percent += s.PercentPerTarget;
                    hits += Math.Max(1, s.hits);
                }
                return hits > 0 ? percent / hits : 0;
            }
        }

        /// <summary>무너뜨리기(숨은 수) = 한 바퀴 버팀 합 ÷ 한 바퀴 시간. 장검 56 ÷ 2.15 = 26, 대검 96 ÷ 3.10 = 31, 쌍검 60 ÷ 2.60 = 23. 공격 속도와 무관(3-4).</summary>
        public double PoisePerSecond
        {
            get
            {
                double poise = 0;
                foreach (var s in combo) poise += s.poiseDamage * Math.Max(1, s.hits);
                return poise / CycleSeconds;
            }
        }

        /// <summary>무너뜨리기 글('약함'·'보통'·'강함', 화면에는 숫자 대신 이것만). 25 미만 약함, 30 이상 강함.</summary>
        public string PoiseWord
        {
            get
            {
                double p = PoisePerSecond;
                return p < 25 ? "약함" : p >= 30 ? "강함" : "보통";
            }
        }

        /// <summary>한 번에 최대(일반 단계 가장 큰 값)와 마무리 최대. 장검 5(8), 대검 7(12), 쌍검 3(8).</summary>
        public int MaxTargets
        {
            get
            {
                int m = 0;
                foreach (var s in combo)
                    if (!s.finisher && s.maxTargets > m) m = s.maxTargets;
                return m;
            }
        }

        public int FinisherMaxTargets
        {
            get
            {
                int m = 0;
                foreach (var s in combo)
                    if (s.finisher && s.maxTargets > m) m = s.maxTargets;
                return m;
            }
        }
    }

    public static class WeaponPresets
    {
        /// <summary>장검: 횡베기 → 역베기 → 찌르기(직선 관통, 앞으로 한 발). 일정한 리듬.</summary>
        public static readonly WeaponAttackRule Longsword = new WeaponAttackRule
        {
            id = "wpn_longsword",
            displayName = "장검",
            combo = new[]
            {
                new ComboStep { name = "횡베기", shape = ComboShape.Arc, arcDeg = 120f, size = 2.0f, maxTargets = 5, duration = 0.6f, hitPercent = 90f, knockback = 0.5f, poiseDamage = 10f },
                new ComboStep { name = "역베기", shape = ComboShape.Arc, arcDeg = 120f, size = 2.0f, maxTargets = 5, duration = 0.6f, hitPercent = 90f, knockback = 0.5f, poiseDamage = 10f },
                new ComboStep
                {
                    name = "찌르기", shape = ComboShape.Line, size = 3.4f, width = 1.0f, maxTargets = 8, duration = 0.95f, hitMoment = 0.4f,
                    hitPercent = 140f, knockback = 1.4f, moveScale = 0.15f, advance = 0.6f, finisher = true, hitStop = 0.07f, shake = 0.06f, poiseDamage = 36f,
                },
            },
        };

        /// <summary>대검: 큰 베기 → 되베기 → 내려찍기(앞쪽 원형 충격파). 느리지만 마무리가 묵직하다.</summary>
        public static readonly WeaponAttackRule Greatsword = new WeaponAttackRule
        {
            id = "wpn_greatsword",
            displayName = "대검",
            combo = new[]
            {
                new ComboStep { name = "큰 베기", shape = ComboShape.Arc, arcDeg = 150f, size = 2.4f, maxTargets = 7, duration = 0.9f, hitMoment = 0.4f, hitPercent = 115f, knockback = 0.8f, moveScale = 0.35f, hitStop = 0.05f, poiseDamage = 18f },
                new ComboStep { name = "되베기", shape = ComboShape.Arc, arcDeg = 150f, size = 2.4f, maxTargets = 7, duration = 0.9f, hitMoment = 0.4f, hitPercent = 115f, knockback = 0.8f, moveScale = 0.35f, hitStop = 0.05f, poiseDamage = 18f },
                new ComboStep
                {
                    name = "내려찍기", shape = ComboShape.Circle, size = 2.2f, centerOffset = 1.3f, maxTargets = 12, duration = 1.3f, hitMoment = 0.5f,
                    hitPercent = 220f, knockback = 1.6f, moveScale = 0.1f, advance = 0.3f, finisher = true, hitStop = 0.09f, shake = 0.14f, poiseDamage = 60f,
                },
            },
        };

        /// <summary>쌍검: 2연타 세 번(갈수록 빨라짐) → 회전베기(내 주변 3연타).</summary>
        public static readonly WeaponAttackRule Twinblades = new WeaponAttackRule
        {
            id = "wpn_twinblades",
            displayName = "쌍검",
            combo = new[]
            {
                new ComboStep { name = "연타", shape = ComboShape.Arc, arcDeg = 90f, size = 1.7f, maxTargets = 3, duration = 0.62f, hitMoment = 0.3f, hits = 2, hitInterval = 0.1f, hitPercent = 52f, knockback = 0.3f, knockbackOnLastHitOnly = true, poiseDamage = 4f },
                new ComboStep { name = "연타", shape = ComboShape.Arc, arcDeg = 90f, size = 1.7f, maxTargets = 3, duration = 0.56f, hitMoment = 0.3f, hits = 2, hitInterval = 0.1f, hitPercent = 52f, knockback = 0.3f, knockbackOnLastHitOnly = true, poiseDamage = 4f },
                new ComboStep { name = "연타", shape = ComboShape.Arc, arcDeg = 90f, size = 1.7f, maxTargets = 3, duration = 0.5f, hitMoment = 0.3f, hits = 2, hitInterval = 0.1f, hitPercent = 52f, knockback = 0.3f, knockbackOnLastHitOnly = true, poiseDamage = 4f },
                new ComboStep
                {
                    name = "회전베기", shape = ComboShape.Circle, size = 2.0f, centerOffset = 0f, maxTargets = 8, duration = 0.92f, hitMoment = 0.3f, hits = 3, hitInterval = 0.1f,
                    hitPercent = 34f, knockback = 0.9f, knockbackOnLastHitOnly = true, moveScale = 0.5f, finisher = true, hitStop = 0.06f, shake = 0.06f, poiseDamage = 12f,
                },
            },
        };

        public static readonly WeaponAttackRule[] All = { Longsword, Greatsword, Twinblades };
    }
}
