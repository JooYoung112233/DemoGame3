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

        // ── 새 무기 고유 규칙 깃발(기획/전투-보스-무기-다듬기-1차.md 2장). 모두 기본 false·0이라 기존 3종은 비트까지 같다 ──

        /// <summary>창 끊어 찌르기: 맞으면 보통 무게 적의 준비 동작을 끊고(마무리처럼), 무거운 적은 0.15초 움찔. 같은 적은 1.5초에 한 번(Enemy.TryStaggerInterrupt).</summary>
        public bool staggers;
        /// <summary>쇠망치 땅 울리기 벽 박기: 이 단계 넉백으로 벽·기둥에 박히면 WallSlamRule 한 칸 위 결과(멧돼지도 바로 무너짐).</summary>
        public bool wallBreak;
        /// <summary>큰 낫 끌어당김: 넉백 방향을 몸 쪽으로 뒤집는다. 끌리는 거리 = min(넉백 × (1 − 저항), 몸 사이 거리 − 0.15).</summary>
        public bool pull;
        /// <summary>도끼 출혈 합(%, 3초 동안 0.5초마다 나눠 들어감, BleedRule). 0이면 없음. 계수(PercentPerTarget)에 넣는다.</summary>
        public float bleedPercent;
        /// <summary>단검 등 찌르기: 적의 등 뒤 ±60°(BackstabRule)에서 맞히면 치명 확률 +400‰(상한 밖).</summary>
        public bool backstab;
        /// <summary>사슬 철퇴 관성: 마무리 뒤 0.5초 안에 다시 치면 이 단계 동작 길이 × 0.9(InertiaRule).</summary>
        public bool inertia;
        /// <summary>
        /// 둔탁한 타격(한손검과 방패 ② 방패 치기, 기획/세-무기-우클릭-소켓-1차.md 2-3): 소리 '퉁' + 타격음, 피 튀김 줄임. 판정·피해는 그대로.
        /// 기본 false라 다른 단계는 비트까지 같다.
        /// </summary>
        public bool blunt;

        /// <summary>한 대상이 이 단계에서 받는 배율 합(%) = 타 × 배율 + 출혈 합(도끼). 출혈이 없으면 예전 값과 같다.</summary>
        public float PercentPerTarget => hits * hitPercent + bleedPercent;

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
    /// M0a 판정(2026-10-02) 뒤 단계 콤보 + 마무리로 바꿨다. 세 무기 콤보는 기획/세-무기-우클릭-소켓-1차.md(2026-10-04)로 다시 짰고,
    /// 단일 대상 초당 계수는 한손검과 방패 1.472 / 대검 1.452 / 쌍검 1.588로 띠(1.45~1.60) 안이다(차례 쌍검 > 한손검 > 대검은 예전과 같음).
    /// 새 무기 6종(전투·보스·무기 다듬기 1차 2장)도 같은 띠 안이다.
    /// </summary>
    [Serializable]
    public sealed class WeaponAttackRule
    {
        public string id;
        public string displayName;
        /// <summary>카드 설명 한 줄(짧고 음울하게, 인물 이름 금지). 없으면 null.</summary>
        public string flavor;
        /// <summary>카드 '방식 다름'에 더하는 고유 규칙 한 줄(예: '벽에 박으면 무너뜨림'). 없으면 null.</summary>
        public string traitLine;
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

        /// <summary>초당 휘두르기(공격 속도 0) = 콤보 동작 수 ÷ 한 바퀴 시간. 한손검과 방패 1.42, 대검 0.97, 쌍검 1.57.</summary>
        public double SwingsPerSecond => combo.Length / CycleSeconds;

        /// <summary>
        /// 한 방 세기(타 평균 배율 %) = 한 바퀴 배율 합 ÷ 한 바퀴 타 수. 한손검과 방패 104(92·90·130), 대검 150(115·120·215), 쌍검 45(타당, 405 ÷ 9타).
        /// 출혈(bleedPercent)은 넣지 않는다(도끼 110 = 95·95·140).
        /// </summary>
        public double AverageHitPercent
        {
            get
            {
                double percent = 0;
                int hits = 0;
                foreach (var s in combo)
                {
                    percent += s.hits * s.hitPercent;
                    hits += Math.Max(1, s.hits);
                }
                return hits > 0 ? percent / hits : 0;
            }
        }

        /// <summary>무너뜨리기(숨은 수) = 한 바퀴 버팀 합 ÷ 한 바퀴 시간. 한손검과 방패 60 ÷ 2.12 = 28, 대검 96 ÷ 3.10 = 31, 쌍검 62 ÷ 2.55 = 24. 공격 속도와 무관(3-4).</summary>
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

        /// <summary>한 번에 최대(일반 단계 가장 큰 값)와 마무리 최대. 한손검과 방패 4(6), 대검 7(12), 쌍검 3(8).</summary>
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
        // ── 세 무기(기획/세-무기-우클릭-소켓-1차.md 2-3·3-4·4-4, 사용자 원문 3 '콤보 바뀌어도 된다'). 저장 id는 그대로다. ──
        // 판정 순간(초, 공격 속도 0): 한손검과 방패 0.2088 / 0.2356 / 0.3864, 대검 0.352 / 0.3864 / 0.65,
        // 쌍검 0.156·0.256 / 0.156·0.256 / 0.1914·0.2514 / 0.3069·0.3569·0.4069. 그림 키(Core/Combat/Stance, spec.json 2차)가 이 순간에 맞춘다.

        /// <summary>
        /// 한손검과 방패(저장 id wpn_longsword): 베기(오른 → 왼) → 되베기(왼 → 오른) → 마무리 베기(오른 어깨 앞에 세웠다가 비껴 내려 벰, 마무리). 찌르기는 없다
        /// (2026-10-05 사용자 원문 "찌르넣지말고 베기 3연타로 가자 찌르기는 빼고 어색하니까"). 패링 뒤 반격도 ③ 마무리 베기 데이터(반격 베기).
        /// 한 바퀴 2.12초, 배율 312%(92 + 90 + 130), 버팀 60(8 + 14 + 38) → 계수 1.472(치명 포함 1.496), 초당 휘두르기 1.42, 한 방 104, 무너뜨리기 28(보통),
        /// 최대 4(마무리 6). ② 되베기 90%는 기준 장비로 굴쥐를 한 방에 잡는다(장비 문서 4-8, 10층 최소 89%). 둔탁한 타격(blunt)을 쓰는 단계는 없다.
        /// </summary>
        public static readonly WeaponAttackRule Longsword = new WeaponAttackRule
        {
            id = "wpn_longsword",
            displayName = "한손검과 방패",
            combo = new[]
            {
                new ComboStep
                {
                    name = "베기", shape = ComboShape.Arc, arcDeg = 110f, size = 2.0f, maxTargets = 4, duration = 0.58f, hitMoment = 0.36f,
                    hitPercent = 92f, knockback = 0.5f, moveScale = 0.45f, advance = 0f, hitStop = 0.04f, shake = 0f, poiseDamage = 8f,
                },
                new ComboStep
                {
                    name = "되베기", shape = ComboShape.Arc, arcDeg = 110f, size = 2.0f, maxTargets = 4, duration = 0.62f, hitMoment = 0.38f,
                    hitPercent = 90f, knockback = 0.6f, moveScale = 0.40f, advance = 0.15f, hitStop = 0.05f, shake = 0.02f, poiseDamage = 14f,
                },
                new ComboStep
                {
                    name = "마무리 베기", shape = ComboShape.Arc, arcDeg = 140f, size = 2.3f, maxTargets = 6, duration = 0.92f, hitMoment = 0.42f,
                    hitPercent = 130f, knockback = 1.4f, moveScale = 0.15f, advance = 0.5f, finisher = true, hitStop = 0.07f, shake = 0.06f, poiseDamage = 38f,
                },
            },
        };

        /// <summary>
        /// 대검: 걷어 베기 → 치켜 베기 → 내려 쪼개기(앞쪽 원형, 마무리). 느리고 넓게, 마무리가 묵직하다. 머리 위로 들지 않고 몸 옆 세로면에서 친다.
        /// 한 바퀴 3.10초, 배율 450%, 버팀 96 → 계수 1.452(치명 포함 1.487), 초당 휘두르기 0.97, 한 방 150, 무너뜨리기 31(강함), 최대 7(마무리 12).
        /// 오른쪽 클릭 놓아 베기는 GreatswordCharge.Release.
        /// </summary>
        public static readonly WeaponAttackRule Greatsword = new WeaponAttackRule
        {
            id = "wpn_greatsword",
            displayName = "대검",
            combo = new[]
            {
                new ComboStep
                {
                    name = "걷어 베기", shape = ComboShape.Arc, arcDeg = 150f, size = 2.4f, maxTargets = 7, duration = 0.88f, hitMoment = 0.40f,
                    hitPercent = 115f, knockback = 0.8f, moveScale = 0.30f, advance = 0f, hitStop = 0.05f, shake = 0f, poiseDamage = 18f,
                },
                new ComboStep
                {
                    name = "치켜 베기", shape = ComboShape.Arc, arcDeg = 70f, size = 2.7f, maxTargets = 4, duration = 0.92f, hitMoment = 0.42f,
                    hitPercent = 120f, knockback = 1.0f, moveScale = 0.25f, advance = 0.25f, hitStop = 0.06f, shake = 0.04f, poiseDamage = 20f,
                },
                new ComboStep
                {
                    name = "내려 쪼개기", shape = ComboShape.Circle, size = 2.2f, centerOffset = 1.3f, maxTargets = 12, duration = 1.30f, hitMoment = 0.50f,
                    hitPercent = 215f, knockback = 1.6f, moveScale = 0.10f, advance = 0.3f, finisher = true, hitStop = 0.09f, shake = 0.14f, poiseDamage = 58f,
                },
            },
        };

        /// <summary>
        /// 쌍검: 우측 베기 → 좌측 베기 → 엇베기(각 2타) → 가위 가르기(바깥에서 안으로 닫는 3타, 마무리). 넉백은 단계마다 마지막 타만.
        /// 2026-10-05 사용자 원문 "콤보는 우측 -> 좌측 -> 엇베기 -> 가위가르기 이런식이좋아보이는데". ①·②는 한 손 '베고 되베기' 2타(한 타면 89% 이상이 되어 치명이 '보통'이 됨).
        /// 한 바퀴 2.55초, 배율 405%(92 + 92 + 104 + 117), 버팀 62 → 계수 1.588(치명 포함 1.591), 초당 휘두르기 1.57, 한 방 45(타당), 무너뜨리기 24(약함), 최대 3(마무리 8).
        /// ④ 가위 가르기는 앞 부채꼴 150°(뒤쪽 360°는 E 회오리가 맡음).
        /// </summary>
        public static readonly WeaponAttackRule Twinblades = new WeaponAttackRule
        {
            id = "wpn_twinblades",
            displayName = "쌍검",
            combo = new[]
            {
                new ComboStep
                {
                    name = "우측 베기", shape = ComboShape.Arc, arcDeg = 100f, size = 1.7f, maxTargets = 3, duration = 0.52f, hitMoment = 0.30f, hits = 2, hitInterval = 0.10f,
                    hitPercent = 46f, knockback = 0.3f, knockbackOnLastHitOnly = true, moveScale = 0.50f, advance = 0f, hitStop = 0.04f, shake = 0f, poiseDamage = 4f,
                },
                new ComboStep
                {
                    name = "좌측 베기", shape = ComboShape.Arc, arcDeg = 100f, size = 1.7f, maxTargets = 3, duration = 0.52f, hitMoment = 0.30f, hits = 2, hitInterval = 0.10f,
                    hitPercent = 46f, knockback = 0.3f, knockbackOnLastHitOnly = true, moveScale = 0.50f, advance = 0f, hitStop = 0.04f, shake = 0f, poiseDamage = 4f,
                },
                new ComboStep
                {
                    name = "엇베기", shape = ComboShape.Arc, arcDeg = 120f, size = 1.8f, maxTargets = 3, duration = 0.58f, hitMoment = 0.33f, hits = 2, hitInterval = 0.06f,
                    hitPercent = 52f, knockback = 0.4f, knockbackOnLastHitOnly = true, moveScale = 0.40f, advance = 0.15f, hitStop = 0.05f, shake = 0.03f, poiseDamage = 5f,
                },
                new ComboStep
                {
                    name = "가위 가르기", shape = ComboShape.Arc, arcDeg = 150f, size = 2.0f, maxTargets = 8, duration = 0.93f, hitMoment = 0.33f, hits = 3, hitInterval = 0.05f,
                    hitPercent = 39f, knockback = 0.9f, knockbackOnLastHitOnly = true, moveScale = 0.35f, advance = 0.25f, finisher = true, hitStop = 0.06f, shake = 0.06f, poiseDamage = 12f,
                },
            },
        };

        // ── 새 무기 6종(기획/전투-보스-무기-다듬기-1차.md 2-3, 결정 ② = 6종 모두). 기존 모양(부채꼴·직선·원)만 쓴다. ──
        // All 뒤에 차례(숫자키 4~9)대로 붙는다. 계수(치명 뺌) 1.485 / 1.518 / 1.463 / 1.526(출혈 포함, 빼면 1.325) / 1.540 / 1.458(관성 1.552).
        // 고유 규칙 깃발(staggers·wallBreak·pull·bleedPercent·backstab·inertia)만 다르고 휘두르기 코드는 같다. 고유 상수는 WeaponTraitRules·BleedRule·BackstabRule·InertiaRule.

        /// <summary>쇠망치: 내려치기 → 후려치기 → 땅 울리기(벽 박기). 가장 느리고 가장 무겁다. 한 바퀴 3.40초, 배율 505%, 버팀 122.</summary>
        public static readonly WeaponAttackRule Maul = new WeaponAttackRule
        {
            id = "wpn_maul",
            displayName = "쇠망치",
            flavor = "바위를 깨던 쇠망치. 무너진 것은 다시 일어나지 못한다.",
            traitLine = "벽에 박으면 무너뜨림",
            combo = new[]
            {
                new ComboStep
                {
                    name = "내려치기", shape = ComboShape.Circle, size = 1.1f, centerOffset = 1.0f, maxTargets = 3, duration = 1.00f, hitMoment = 0.5f,
                    hitPercent = 135f, knockback = 1.0f, moveScale = 0.25f, advance = 0.3f, hitStop = 0.06f, shake = 0.05f, poiseDamage = 26f,
                },
                new ComboStep
                {
                    name = "후려치기", shape = ComboShape.Arc, arcDeg = 110f, size = 2.0f, maxTargets = 3, duration = 0.95f, hitMoment = 0.42f,
                    hitPercent = 130f, knockback = 1.2f, moveScale = 0.30f, hitStop = 0.06f, poiseDamage = 26f,
                },
                new ComboStep
                {
                    name = "땅 울리기", shape = ComboShape.Circle, size = 2.0f, centerOffset = 1.2f, maxTargets = 8, duration = 1.45f, hitMoment = 0.55f,
                    hitPercent = 240f, knockback = 2.0f, moveScale = 0.10f, advance = 0.3f, finisher = true, hitStop = 0.10f, shake = 0.18f, poiseDamage = 70f,
                    wallBreak = true,
                },
            },
        };

        /// <summary>창: 찌르기 → 찌르기(끊어 찌르기) → 휘둘러 밀기. 멀리서 먼저 찌르고 밀어내 거리를 만든다. 한 바퀴 2.24초, 배율 340%, 버팀 50.</summary>
        public static readonly WeaponAttackRule Spear = new WeaponAttackRule
        {
            id = "wpn_spear",
            displayName = "창",
            flavor = "갱도 경비가 들던 창. 다가오기 전에 끝낸다.",
            traitLine = "준비 동작을 끊음",
            combo = new[]
            {
                new ComboStep
                {
                    name = "찌르기", shape = ComboShape.Line, size = 3.2f, width = 0.7f, maxTargets = 3, duration = 0.62f, hitMoment = 0.4f,
                    hitPercent = 100f, knockback = 0.8f, moveScale = 0.45f, hitStop = 0.04f, poiseDamage = 10f, staggers = true,
                },
                new ComboStep
                {
                    name = "찌르기", shape = ComboShape.Line, size = 3.2f, width = 0.7f, maxTargets = 3, duration = 0.62f, hitMoment = 0.4f,
                    hitPercent = 100f, knockback = 0.8f, moveScale = 0.45f, hitStop = 0.04f, poiseDamage = 10f, staggers = true,
                },
                new ComboStep
                {
                    name = "휘둘러 밀기", shape = ComboShape.Arc, arcDeg = 180f, size = 2.7f, maxTargets = 6, duration = 1.00f, hitMoment = 0.42f,
                    hitPercent = 140f, knockback = 2.2f, moveScale = 0.20f, finisher = true, hitStop = 0.07f, shake = 0.08f, poiseDamage = 30f,
                },
            },
        };

        /// <summary>큰 낫: 거두기 → 되거두기 → 크게 거두기(모두 끌어당김). 넓게 거두고 몸 쪽으로 끌어온다. 한 바퀴 2.70초, 배율 395%, 버팀 42.</summary>
        public static readonly WeaponAttackRule Scythe = new WeaponAttackRule
        {
            id = "wpn_scythe",
            displayName = "큰 낫",
            flavor = "굴 입구 풀을 베던 낫. 이제는 다른 것을 거둔다.",
            traitLine = "몸 쪽으로 끌어옴",
            combo = new[]
            {
                new ComboStep
                {
                    name = "거두기", shape = ComboShape.Arc, arcDeg = 180f, size = 2.5f, maxTargets = 5, duration = 0.80f, hitMoment = 0.42f,
                    hitPercent = 115f, knockback = 0.8f, moveScale = 0.35f, hitStop = 0.04f, poiseDamage = 8f, pull = true,
                },
                new ComboStep
                {
                    name = "되거두기", shape = ComboShape.Arc, arcDeg = 180f, size = 2.5f, maxTargets = 5, duration = 0.80f, hitMoment = 0.42f,
                    hitPercent = 115f, knockback = 0.8f, moveScale = 0.35f, hitStop = 0.04f, poiseDamage = 8f, pull = true,
                },
                new ComboStep
                {
                    name = "크게 거두기", shape = ComboShape.Arc, arcDeg = 220f, size = 2.8f, maxTargets = 8, duration = 1.10f, hitMoment = 0.45f,
                    hitPercent = 165f, knockback = 1.4f, moveScale = 0.15f, finisher = true, hitStop = 0.07f, shake = 0.08f, poiseDamage = 26f, pull = true,
                },
            },
        };

        /// <summary>도끼: 비껴 찍기 → 되찍기 → 쪼개기(출혈 50%). 베인 자리는 오래 흐른다. 한 바퀴 2.49초, 배율 330% + 출혈 50%, 버팀 68.</summary>
        public static readonly WeaponAttackRule Axe = new WeaponAttackRule
        {
            id = "wpn_axe",
            displayName = "도끼",
            flavor = "버팀목을 찍던 도끼. 베인 자리는 오래 흐른다.",
            traitLine = "쪼개면 피가 흐름",
            combo = new[]
            {
                new ComboStep
                {
                    name = "비껴 찍기", shape = ComboShape.Arc, arcDeg = 100f, size = 1.9f, maxTargets = 3, duration = 0.72f, hitMoment = 0.4f,
                    hitPercent = 95f, knockback = 0.6f, moveScale = 0.40f, hitStop = 0.05f, poiseDamage = 14f,
                },
                new ComboStep
                {
                    name = "되찍기", shape = ComboShape.Arc, arcDeg = 100f, size = 1.9f, maxTargets = 3, duration = 0.72f, hitMoment = 0.4f,
                    hitPercent = 95f, knockback = 0.6f, moveScale = 0.40f, hitStop = 0.05f, poiseDamage = 14f,
                },
                new ComboStep
                {
                    name = "쪼개기", shape = ComboShape.Circle, size = 1.0f, centerOffset = 1.0f, maxTargets = 2, duration = 1.05f, hitMoment = 0.5f,
                    hitPercent = 140f, knockback = 1.0f, moveScale = 0.15f, advance = 0.3f, finisher = true, hitStop = 0.08f, shake = 0.10f, poiseDamage = 40f,
                    bleedPercent = 50f,
                },
            },
        };

        /// <summary>단검: 두 번 긋기 → 두 번 긋기 → 급소 찌르기(모두 등 찌르기). 등 뒤에서 쓰는 칼. 한 바퀴 2.00초, 배율 308%, 버팀 36.</summary>
        public static readonly WeaponAttackRule Dagger = new WeaponAttackRule
        {
            id = "wpn_dagger",
            displayName = "단검",
            flavor = "등 뒤에서 쓰는 칼.",
            traitLine = "등 뒤에서 치명 확률 +40%",
            combo = new[]
            {
                new ComboStep
                {
                    name = "두 번 긋기", shape = ComboShape.Arc, arcDeg = 80f, size = 1.6f, maxTargets = 2, duration = 0.60f, hitMoment = 0.3f, hits = 2, hitInterval = 0.12f,
                    hitPercent = 45f, knockback = 0.2f, knockbackOnLastHitOnly = true, moveScale = 0.70f, hitStop = 0.04f, poiseDamage = 4f, backstab = true,
                },
                new ComboStep
                {
                    name = "두 번 긋기", shape = ComboShape.Arc, arcDeg = 80f, size = 1.6f, maxTargets = 2, duration = 0.60f, hitMoment = 0.3f, hits = 2, hitInterval = 0.12f,
                    hitPercent = 45f, knockback = 0.2f, knockbackOnLastHitOnly = true, moveScale = 0.70f, hitStop = 0.04f, poiseDamage = 4f, backstab = true,
                },
                new ComboStep
                {
                    name = "급소 찌르기", shape = ComboShape.Line, size = 2.0f, width = 0.6f, maxTargets = 1, duration = 0.80f, hitMoment = 0.45f,
                    hitPercent = 128f, knockback = 0.6f, moveScale = 0.30f, advance = 0.5f, finisher = true, hitStop = 0.07f, shake = 0.05f, poiseDamage = 20f,
                    backstab = true,
                },
            },
        };

        /// <summary>사슬 철퇴: 돌려 치기 → 되돌려 치기(관성) → 내리꽂기. 멈추지 않으면 점점 무거워진다. 한 바퀴 2.95초(관성 2.77초), 배율 430%, 버팀 68.</summary>
        public static readonly WeaponAttackRule Flail = new WeaponAttackRule
        {
            id = "wpn_flail",
            displayName = "사슬 철퇴",
            flavor = "멈추지 않으면 점점 무거워진다.",
            traitLine = "이어 치면 빨라짐",
            combo = new[]
            {
                new ComboStep
                {
                    name = "돌려 치기", shape = ComboShape.Circle, size = 2.1f, centerOffset = 0f, maxTargets = 4, duration = 0.95f, hitMoment = 0.45f,
                    hitPercent = 120f, knockback = 0.7f, moveScale = 0.55f, hitStop = 0.04f, poiseDamage = 14f, inertia = true,
                },
                new ComboStep
                {
                    name = "되돌려 치기", shape = ComboShape.Circle, size = 2.1f, centerOffset = 0f, maxTargets = 4, duration = 0.85f, hitMoment = 0.4f,
                    hitPercent = 120f, knockback = 0.7f, moveScale = 0.55f, hitStop = 0.04f, poiseDamage = 14f, inertia = true,
                },
                new ComboStep
                {
                    name = "내리꽂기", shape = ComboShape.Circle, size = 1.6f, centerOffset = 1.4f, maxTargets = 6, duration = 1.15f, hitMoment = 0.5f,
                    hitPercent = 190f, knockback = 1.4f, moveScale = 0.15f, advance = 0.2f, finisher = true, hitStop = 0.08f, shake = 0.12f, poiseDamage = 40f,
                },
            },
        };

        /// <summary>M0a 승인 3종(옛 무기 굴림 LootRules.RollWeapon·꾸러미 v1 읽기용 고정 목록). 차례를 바꾸지 않는다.</summary>
        public static readonly WeaponAttackRule[] Legacy3 = { Longsword, Greatsword, Twinblades };

        /// <summary>새 무기 6종(숫자키 4~9 차례: 쇠망치·창·큰 낫·도끼·단검·사슬 철퇴).</summary>
        public static readonly WeaponAttackRule[] New6 = { Maul, Spear, Scythe, Axe, Dagger, Flail };

        /// <summary>
        /// 플레이어가 쓰는 무기 전체 9종(차례 = 숫자키 1~9 = 시험 패널 무기 단추 차례): 한손검과 방패·대검·쌍검(Legacy3) 뒤에 New6.
        /// 장비 종류 표(GearBaseTable 무기 줄)와 같은 차례다. 옛 씨앗 결과가 필요한 곳(LootRules.RollWeapon)은 Legacy3를 쓴다.
        /// </summary>
        public static readonly WeaponAttackRule[] All = { Longsword, Greatsword, Twinblades, Maul, Spear, Scythe, Axe, Dagger, Flail };
    }

    /// <summary>
    /// 새 무기 고유 규칙 가운데 따로 규칙 파일이 없는 것의 상수(기획/전투-보스-무기-다듬기-1차.md 2-3).
    /// 출혈은 BleedRule, 등 찌르기는 BackstabRule, 관성은 InertiaRule, 벽 박기는 WallSlamRule이 가진다.
    /// </summary>
    public static class WeaponTraitRules
    {
        /// <summary>
        /// 창 끊어 찌르기(ComboStep.staggers): 같은 적은 이 시간(초)에 한 번만 끊는다(Enemy.TryStaggerInterrupt 인자, PlayerController가 이 값을 그대로 씀).
        /// 시각은 실제로 준비를 끊었을 때만 적는다(걷는 궁수를 찔러 제한을 써 버리지 않음). 궁수가 끊긴 뒤 쉬는 1.0초와 조준 0.8초(1.8초)는 이 제한보다 길어
        /// 제한만으로는 계속 묶어 둘 수 있으므로, 찌르기에 끊긴 궁수는 한 발을 쏘기 전까지 다시 찔러 끊을 수 없다(ArcherBrain).
        /// </summary>
        public const float StaggerCooldown = 1.5f;
        /// <summary>끊어 찌르기에 맞은 무거운 적의 움찔(초). 준비 동작은 끊기지 않는다.</summary>
        public const float HeavyFlinchSeconds = 0.15f;
        /// <summary>큰 낫 끌어당김(ComboStep.pull): 끌려온 적과 내 몸 사이에 남기는 틈.</summary>
        public const float PullGap = 0.15f;

        /// <summary>
        /// 끌어당김 거리 = min(넉백 × (1 − 넉백 저항), 적과 내 몸 사이 거리 − 0.15). 0 아래로 내려가지 않는다(몸에 겹치지 않음).
        /// gapBetweenBodies = 두 몸 둘레 사이 거리(중심 거리 − 두 반지름).
        /// </summary>
        public static float PullDistance(float knockback, float knockResistance, float gapBetweenBodies)
        {
            float resist = Math.Max(0f, Math.Min(1f, knockResistance));
            float pushed = Math.Max(0f, knockback) * (1f - resist);
            return Math.Max(0f, Math.Min(pushed, gapBetweenBodies - PullGap));
        }
    }
}
