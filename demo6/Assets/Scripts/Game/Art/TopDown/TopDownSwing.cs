using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 정수리 시점에서 무기 한 자루의 손 자세. 몸 틀 기준(+x = 바라보는 쪽, +y = 왼쪽), 위치는 월드 유닛.
    /// Length는 칼날 길이 배율(음수면 머리 위로 넘어가 뒤를 향한 투영), Size는 카메라 쪽으로 들려 커 보이는 배율.
    /// </summary>
    public struct TopDownHand
    {
        public Vector2 Pos;
        public float Angle;
        public float Length;
        public float Size;

        public static TopDownHand At(Vector2 pos, float angle) => new TopDownHand { Pos = pos, Angle = angle, Length = 1f, Size = 1f };

        /// <summary>각도는 그대로 선형 보간한다(휘두르는 방향이 뒤집히지 않게).</summary>
        public static TopDownHand Lerp(TopDownHand a, TopDownHand b, float t) => new TopDownHand
        {
            Pos = Vector2.Lerp(a.Pos, b.Pos, t),
            Angle = Mathf.Lerp(a.Angle, b.Angle, t),
            Length = Mathf.Lerp(a.Length, b.Length, t),
            Size = Mathf.Lerp(a.Size, b.Size, t),
        };

        /// <summary>자세 사이를 부드럽게 따라간다(각도는 가까운 쪽으로). 행동이 아닌 때(대기·이동·피격·쓰러짐)에 쓴다.</summary>
        public static TopDownHand Follow(TopDownHand a, TopDownHand b, float k) => new TopDownHand
        {
            Pos = Vector2.Lerp(a.Pos, b.Pos, k),
            Angle = Mathf.LerpAngle(a.Angle, b.Angle, k),
            Length = Mathf.Lerp(a.Length, b.Length, k),
            Size = Mathf.Lerp(a.Size, b.Size, k),
        };
    }

    /// <summary>
    /// 무기마다 다른 휘두르기 겉모습(TopDownWeaponLook.Style, 기획/전투-보스-무기-다듬기-1차.md 2-2·2-7). 판정 순간·동작 길이·판정 범위는 바꾸지 않는다.
    /// 기본값(Default)이면 기존 3종 자세와 비트까지 같다.
    /// </summary>
    public struct TopDownSwingStyle
    {
        /// <summary>찌르기 뻗음 기본값(장검·대검·쌍검·단검, 예전 값 그대로).</summary>
        public const float DefaultReach = 0.55f;
        /// <summary>찌르기(직선 단계) 판정 순간에 손이 앞으로 나가는 거리. 창만 길다(0 이하면 기본값).</summary>
        public float ThrustReach;
        /// <summary>오른쪽 → 왼쪽으로 쓰는 모습이 기본인 그림을 왼쪽 → 오른쪽으로 쓸 때 위아래로 뒤집는가(큰 낫 되거두기 등).</summary>
        public bool FlipOnReverse;
        /// <summary>앞쪽 원 내려치기의 준비 동안 몸을 뒤로 싣는 양(쇠망치·도끼, 유닛). 0이면 없음. 대검은 예전 식(모든 단계 0.065)을 그대로 쓴다.</summary>
        public float SlamWindupLean;
        /// <summary>사슬 철퇴: 내 주변 원 단계에서 몸은 회오리처럼 돌지 않고, 손이 어깨 앞에서 작게 돌며 쇠공이 몸 둘레를 돈다.</summary>
        public bool Flail;
        /// <summary>사슬 철퇴 관성(PlayerController.InertiaActive)이 붙은 동작: 쇠공이 손보다 더 늦게 따라오고 조금 더 멀리 돈다(그림만).</summary>
        public bool Inertia;

        public static TopDownSwingStyle Default => new TopDownSwingStyle { ThrustReach = DefaultReach };
    }

    /// <summary>휘두르기 부가 결과: 그림 위아래 뒤집기(큰 낫)와 사슬 철퇴 쇠공 자리(몸 틀, 유닛).</summary>
    public struct TopDownSwingExtra
    {
        /// <summary>그림 위아래 뒤집기 정도: 0 = 그대로, 1 = 뒤집힘, 사이 = 손잡이 축으로 돌려 쥐는 중(위아래 배율 1 − 2 × Flip).</summary>
        public float Flip;
        /// <summary>쇠공 자리를 휘두르기 계산이 정했는가(사슬 철퇴 내 주변 원). 아니면 리그가 손잡이 끝에서 사슬을 이어 놓는다.</summary>
        public bool HasBall;
        public Vector2 Ball;
    }

    /// <summary>
    /// 정수리 시점 무기 휘두르기 계산(순수 함수). 콤보 단계 모양·시간(ComboStep)과 PlayerController의 자세 시간만 보고 손 자세를 낸다.
    /// 판정 순간(PoseHitTime)에 칼날이 휘두르는 방향 한가운데(각도 0)를 지나거나(부채꼴), 가장 멀리 뻗거나(찌르기), 앞으로 내리찍히거나(앞쪽 원),
    /// 앞을 지나게(내 주변 원·회오리, 사슬 철퇴는 쇠공이 앞을 지남) 맞춘다. 손맛 수치는 읽기만 한다.
    /// </summary>
    public static class TopDownSwing
    {
        /// <summary>
        /// 콤보 단계 하나의 무기 자세(몸 비틀기·몸 위치·몸 크기 포함). 모양별: 부채꼴 = 쓸기(단계·타마다 쓰는 방향이 번갈아),
        /// 직선 = 찌르기, 앞쪽 원 = 머리 위에서 내리찍기, 내 주변 원 = 몸을 축으로 한 바퀴 이상. 쌍검 부채꼴 여러 타는 두 손이 번갈아
        /// (홀수 '단계 + 타'는 오른손이 오른쪽 → 왼쪽, 짝수는 왼손이 왼쪽 → 오른쪽). hit·duration은 PlayerController.PoseHitTime·PoseDuration(초).
        /// 기존 3종 자세(기본 모양)다. 무기 겉모습 차이는 style을 받는 겹쳐 받기를 쓴다.
        /// </summary>
        public static void Attack(WeaponAttackRule weapon, int comboIndex, float t, float duration, float hit, TopDownHand restRight, TopDownHand restLeft, bool twin,
            ref TopDownHand right, ref TopDownHand left, ref float twist, ref Vector2 offset, ref Vector2 scale) =>
            Attack(weapon, comboIndex, t, duration, hit, restRight, restLeft, twin, TopDownSwingStyle.Default,
                ref right, ref left, ref twist, ref offset, ref scale, out _);

        /// <summary>
        /// 무기 겉모습(style)을 더한 휘두르기. 창 찌르기는 ThrustReach만큼 뻗고, 큰 낫은 왼쪽 → 오른쪽 쓸기에서 그림을 뒤집으며(extra.Flip),
        /// 쇠망치·도끼 내려치기는 준비 동작에 몸을 뒤로 싣고, 사슬 철퇴 내 주변 원은 쇠공이 몸 둘레를 돌아 판정 순간에 앞을 지난다(extra.Ball).
        /// 판정 순간·시간·도달 범위는 기본 모양과 같다.
        /// </summary>
        public static void Attack(WeaponAttackRule weapon, int comboIndex, float t, float duration, float hit, TopDownHand restRight, TopDownHand restLeft, bool twin,
            TopDownSwingStyle style, ref TopDownHand right, ref TopDownHand left, ref float twist, ref Vector2 offset, ref Vector2 scale, out TopDownSwingExtra extra)
        {
            extra = default;
            int index = Mathf.Clamp(comboIndex, 0, weapon.combo.Length - 1);
            var step = weapon.combo[index];
            int stepNumber = index + 1;
            if (duration <= 0f) duration = Mathf.Max(0.1f, step.duration);
            if (hit < 0f) hit = duration * step.hitMoment;

            switch (step.shape)
            {
                case ComboShape.Line:
                {
                    float reach = style.ThrustReach > 0f ? style.ThrustReach : TopDownSwingStyle.DefaultReach;
                    right = Thrust(restRight, t, hit, duration, reach, out twist, out float lean);
                    offset = new Vector2(lean, 0f);
                    break;
                }

                case ComboShape.Circle when step.centerOffset > 0.01f:
                {
                    right = Slam(restRight, t, hit, duration, out float lean, out float squash);
                    if (twin) left = TopDownHand.Lerp(restLeft, right, 0.5f);
                    offset = new Vector2(lean, 0f);
                    scale = new Vector2(1f + 0.04f * squash, 1f - 0.05f * squash);
                    twist = 0f;
                    // 쇠망치·도끼: 들어 올리는 동안 몸을 뒤로 싣는다(대검과 같은 모양, 판정 순간에는 0).
                    if (style.SlamWindupLean > 0f && t < hit && hit > 0f)
                        offset.x -= style.SlamWindupLean * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / hit));
                    break;
                }

                case ComboShape.Circle when style.Flail:
                {
                    float dir = stepNumber % 2 == 1 ? 1f : -1f;
                    right = FlailSwing(restRight, t, hit, duration, dir, style.Inertia, out extra.Ball, out twist, out float env);
                    extra.HasBall = true;
                    // 왼손은 균형을 잡으려 옆으로 조금 벌린다(회오리처럼 두 팔을 다 뻗지 않음).
                    left = TopDownHand.Follow(restLeft, TopDownHand.At(new Vector2(0.1f, 0.37f), 50f), env);
                    break;
                }

                case ComboShape.Circle:
                {
                    float spin = Spin(t, hit, step.hits, step.hitInterval, twin ? 2 : 1, duration);
                    twist = spin;
                    // 감는 동안 팔을 쉬는 자세에서 옆으로 벌린다(판정 전에 다 벌어짐).
                    float open = Smooth(t / Mathf.Max(1e-4f, hit - Mathf.Min(0.1f, hit * 0.5f)));
                    right = TopDownHand.Lerp(Tucked(restRight, spin), Extended(spin, -1f), open);
                    left = TopDownHand.Lerp(Tucked(restLeft, spin), Extended(spin, 1f), open);
                    break;
                }

                default:
                {
                    float half = step.arcDeg * 0.5f;
                    int hits = Mathf.Max(1, step.hits);
                    if (!twin)
                    {
                        int k = NearestHit(t, hit, hits, step.hitInterval);
                        float dir = (stepNumber + k) % 2 == 1 ? 1f : -1f;
                        right = Arc(restRight, t, hit + k * step.hitInterval, duration, dir, half, -1f, out twist);
                        // 큰 낫: 그림은 오른쪽 → 왼쪽 쓸기(dir +1)에 맞춰 날이 −y로 늘어지므로 반대로 쓸 때 위아래를 뒤집는다.
                        // 한 프레임에 튀지 않게 들어 올리는 동안 손잡이 축으로 돌려 쥐고(판정 순간에는 다 뒤집힘), 제자리로 돌아오는 동안 되돌린다.
                        if (style.FlipOnReverse && dir < 0f) extra.Flip = TurnOver(t, hit + k * step.hitInterval, duration);
                        break;
                    }
                    int rk = HandHit(t, hit, hits, step.hitInterval, stepNumber, true);
                    int lk = HandHit(t, hit, hits, step.hitInterval, stepNumber, false);
                    float twR = 0f, twL = 0f;
                    if (rk >= 0) right = Arc(restRight, t, hit + rk * step.hitInterval, duration, 1f, half, -1f, out twR);
                    if (lk >= 0) left = Arc(restLeft, t, hit + lk * step.hitInterval, duration, -1f, half, 1f, out twL);
                    int nearest = NearestHit(t, hit, hits, step.hitInterval);
                    twist = nearest == rk ? twR : twL;
                    break;
                }
            }
            // 대검은 준비 동작에서 뒤로 무게를 싣는다. 판정 순간·시간·도달 범위는 원래 값 그대로다.
            if (weapon.id == "wpn_greatsword" && t < hit && hit > 0f)
                offset.x -= .065f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / hit));
        }

        /// <summary>
        /// 큰 낫 날 뒤집기 정도(0 → 1 → 0). Arc와 같은 구간 경계를 쓴다: 들어 올림 끝(t1)까지 다 뒤집고, 따라 흐름 끝(t3) 뒤 제자리로 돌아오며 되돌린다.
        /// </summary>
        static float TurnOver(float t, float hit, float duration)
        {
            float s = Mathf.Min(Mathf.Clamp(hit * 0.45f, 0.045f, 0.11f), hit * 0.9f);
            float t1 = hit - s;
            float t2 = hit + s;
            float t3 = Mathf.Min(Mathf.Max(duration, t2), t2 + Mathf.Max(0.06f, (duration - t2) * 0.35f));
            if (t < t3) return Smooth(t / Mathf.Max(1e-4f, t1));
            return 1f - Smooth((t - t3) / Mathf.Max(1e-4f, duration - t3));
        }

        static int NearestHit(float t, float hit, int hits, float interval)
        {
            int best = 0;
            float bestGap = float.MaxValue;
            for (int k = 0; k < hits; k++)
            {
                float gap = Mathf.Abs(t - (hit + k * interval));
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = k;
                }
            }
            return best;
        }

        /// <summary>이 손이 맡은 타 가운데 지금에 가장 가까운 타(없으면 −1).</summary>
        static int HandHit(float t, float hit, int hits, float interval, int stepNumber, bool rightHand)
        {
            int best = -1;
            float bestGap = float.MaxValue;
            for (int k = 0; k < hits; k++)
            {
                bool usesRight = (stepNumber + k) % 2 == 1;
                if (usesRight != rightHand) continue;
                float gap = Mathf.Abs(t - (hit + k * interval));
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = k;
                }
            }
            return best;
        }

        /// <summary>
        /// 부채꼴 베기 한 번: 반대쪽으로 들어 올림 → 판정 순간 한가운데를 지나는 빠른 쓸기 → 따라 흐름 → 제자리.
        /// dir = +1이면 오른쪽 → 왼쪽(반시계), −1이면 왼쪽 → 오른쪽. side = −1 오른손, +1 왼손. twist는 몸 비틀기(도).
        /// </summary>
        public static TopDownHand Arc(TopDownHand rest, float t, float hit, float duration, float dir, float halfArc, float side, out float twist)
        {
            float a = halfArc + 25f;
            float s = Mathf.Min(Mathf.Clamp(hit * 0.45f, 0.045f, 0.11f), hit * 0.9f);
            float t1 = hit - s;
            float t2 = hit + s;
            float t3 = Mathf.Min(Mathf.Max(duration, t2), t2 + Mathf.Max(0.06f, (duration - t2) * 0.35f));
            float wind = -dir * a;
            float end = dir * (a + 12f);
            if (t < t1)
            {
                float u = EaseOut(t / Mathf.Max(1e-4f, t1));
                twist = -dir * 5f * u;
                return TopDownHand.Lerp(rest, OnArc(wind, side), u);
            }
            if (t < t2)
            {
                // 대칭 곡선이라 u = 0.5(판정 순간)에서 정확히 각도 0을 지난다.
                float u = Smooth((t - t1) / (t2 - t1));
                twist = Mathf.Lerp(-5f * dir, 10f * dir, u);
                return OnArc(Mathf.Lerp(wind, -wind, u), side);
            }
            if (t < t3)
            {
                float u = EaseOut((t - t2) / Mathf.Max(1e-4f, t3 - t2));
                twist = 10f * dir;
                return OnArc(Mathf.Lerp(-wind, end, u), side);
            }
            float back = Smooth(Mathf.Clamp01((t - t3) / Mathf.Max(1e-4f, duration - t3)));
            twist = Mathf.Lerp(10f * dir, 0f, back);
            return TopDownHand.Lerp(OnArc(end, side), rest, back);
        }

        /// <summary>몸 둘레 호 위의 손: 칼날 각도 θ에 따라 손이 어깨 앞을 돈다. 한가운데에서 팔이 가장 길게 뻗는다.</summary>
        static TopDownHand OnArc(float theta, float side)
        {
            float handAngle = theta * 0.75f + side * 12f;
            float r = 0.3f + 0.1f * Mathf.Cos(theta * Mathf.Deg2Rad);
            float rad = handAngle * Mathf.Deg2Rad;
            return TopDownHand.At(new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * r, theta);
        }

        /// <summary>찌르기: 뒤로 당겼다가 판정 순간에 가장 멀리(앞으로 reach) 뻗고, 잠깐 버틴 뒤 제자리. lean은 몸을 앞뒤로 옮기는 양.</summary>
        public static TopDownHand Thrust(TopDownHand rest, float t, float hit, float duration, float reach, out float twist, out float lean)
        {
            float pull = Mathf.Max(hit * 0.6f, hit - 0.08f);
            float hold = Mathf.Min(Mathf.Max(duration, hit), hit + 0.14f);
            var back = TopDownHand.At(new Vector2(-0.03f, -0.24f), -6f);
            var full = TopDownHand.At(new Vector2(-0.03f + reach, -0.1f), -2f);
            if (t < pull)
            {
                float u = EaseOut(t / Mathf.Max(1e-4f, pull));
                twist = -8f * u;
                lean = -0.03f * u;
                return TopDownHand.Lerp(rest, back, u);
            }
            if (t < hit)
            {
                // 가속하며 뻗어 판정 순간에 끝까지 닿는다.
                float u = EaseIn((t - pull) / Mathf.Max(1e-4f, hit - pull));
                twist = Mathf.Lerp(-8f, 8f, u);
                lean = Mathf.Lerp(-0.03f, 0.06f, u);
                return TopDownHand.Lerp(back, full, u);
            }
            if (t < hold)
            {
                twist = 8f;
                lean = 0.06f;
                return full;
            }
            float r = Smooth(Mathf.Clamp01((t - hold) / Mathf.Max(1e-4f, duration - hold)));
            twist = Mathf.Lerp(8f, 0f, r);
            lean = Mathf.Lerp(0.06f, 0f, r);
            return TopDownHand.Lerp(full, rest, r);
        }

        /// <summary>
        /// 앞쪽 원 내려찍기(대검 마무리 등): 오른 어깨 위로 돌려 머리 위로 들어 올리고(칼날이 뒤를 향하고 카메라 쪽으로 들려 커짐) 판정 순간에 앞으로 내리찍는다.
        /// 내리찍기는 세로 휘두르기를 위에서 본 투영: 칼날 길이 = cos(들림 각), 크기 = 1 + 0.5·sin(들림 각). squash는 내리찍은 뒤 몸 눌림(0~1).
        /// </summary>
        public static TopDownHand Slam(TopDownHand rest, float t, float hit, float duration, out float lean, out float squash)
        {
            float raiseEnd = Mathf.Max(hit * 0.6f, hit - 0.1f);
            float impactEnd = Mathf.Min(Mathf.Max(duration, hit), hit + 0.24f);
            var top = new Vector2(-0.02f, -0.06f);
            var down = new Vector2(0.42f, -0.05f);
            float lift;
            TopDownHand pose;
            squash = 0f;
            if (t < raiseEnd)
            {
                // 들어 올리기는 오른 어깨 위로 돌려 뒤를 향하게(칼날이 납작한 토막으로 오래 보이지 않게), 끝에서 내리찍기 투영과 이어진다.
                float u = EaseInOut(t / Mathf.Max(1e-4f, raiseEnd));
                float top125 = Mathf.Sin(125f * Mathf.Deg2Rad);
                pose = TopDownHand.At(Vector2.Lerp(rest.Pos, top, u), Mathf.Lerp(rest.Angle, -180f, u));
                pose.Length = Mathf.Lerp(1f, -Mathf.Cos(125f * Mathf.Deg2Rad), u);
                pose.Size = 1f + 0.5f * top125 * u;
                lean = -0.05f * u;
                return pose;
            }
            else if (t < hit)
            {
                float u = EaseIn((t - raiseEnd) / Mathf.Max(1e-4f, hit - raiseEnd));
                lift = Mathf.Lerp(125f, 0f, u);
                pose = TopDownHand.At(Vector2.Lerp(top, down, u), 0f);
                lean = Mathf.Lerp(-0.05f, 0.07f, u);
            }
            else if (t < impactEnd)
            {
                lift = 0f;
                pose = TopDownHand.At(down, 0f);
                lean = 0.07f;
                squash = 1f - (t - hit) / Mathf.Max(1e-4f, impactEnd - hit);
            }
            else
            {
                float u = Smooth(Mathf.Clamp01((t - impactEnd) / Mathf.Max(1e-4f, duration - impactEnd)));
                lean = Mathf.Lerp(0.07f, 0f, u);
                return TopDownHand.Lerp(TopDownHand.At(down, 0f), rest, u);
            }
            float rad = lift * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            pose.Length = (c < 0f ? -1f : 1f) * Mathf.Max(Mathf.Abs(c), 0.12f);
            pose.Size = 1f + 0.5f * Mathf.Sin(rad);
            return pose;
        }

        /// <summary>
        /// 내 주변 원(쌍검 회전베기 등): 몸 전체 회전 각(도). 반대로 살짝 감았다가 가속해 첫 판정 순간에 오른손 칼날(몸 −90°)이 앞을 지나고,
        /// 다음 판정마다 다음 칼날이 앞을 지난다(칼날 blades개가 360/blades 간격). 마지막 판정 뒤 감속해 정면(360의 배수)에서 멈춘다.
        /// </summary>
        public static float Spin(float t, float hit, int hits, float interval, int blades, float duration)
        {
            float per = 360f / Mathf.Max(1, blades);
            float lead = Mathf.Min(0.1f, hit * 0.5f);
            const float first = 90f;
            if (t < hit - lead) return -30f * EaseOut(t / Mathf.Max(1e-4f, hit - lead));
            if (t < hit) return Mathf.Lerp(-30f, first, EaseIn((t - (hit - lead)) / Mathf.Max(1e-4f, lead)));
            int last = Mathf.Max(0, hits - 1);
            float hitLast = hit + last * interval;
            if (t < hitLast) return first + (t - hit) / Mathf.Max(1e-4f, interval) * per;
            float aLast = first + last * per;
            float target = Mathf.Ceil((aLast + 120f) / 360f) * 360f;
            float span = Mathf.Max(0.05f, Mathf.Min(0.32f, duration - hitLast - 0.02f));
            return Mathf.Lerp(aLast, target, EaseOut(Mathf.Clamp01((t - hitLast) / span)));
        }

        /// <summary>
        /// 회오리 베기(0.6초, 판정 0.1·0.3·0.5초): 몸 회전 각. 오른손 칼날(몸 −90°)이 판정마다 정확히 앞을 지나고, 첫 판정 뒤로는 0.2초에 한 바퀴.
        /// 시작은 0°에서 멈춘 채로 출발해 첫 판정까지 회전 속도를 0에서 한 바퀴/0.2초까지 고르게 올린다(시작 때 몸이 90° 한 번에 돌지 않게, 모두 약 세 바퀴).
        /// period는 PlayerController.WhirlTicks 간격(0.2초).
        /// </summary>
        public static float Whirl(float t, float firstTick, float period)
        {
            float rate = 360f / Mathf.Max(1e-4f, period);
            if (t >= firstTick) return 90f + rate * (t - firstTick);
            // 가속 구간: 각 = a·u². 첫 판정에서 90°(+360°의 배수)에 닿고, 끝 속도(2a/firstTick)가 회전 속도와 가장 가깝게 a를 고른다.
            float lead = Mathf.Max(1e-4f, firstTick);
            float natural = 0.5f * rate * lead;
            float a = 90f + 360f * Mathf.Max(0f, Mathf.Round((natural - 90f) / 360f));
            float u = Mathf.Max(0f, t) / lead;
            return a * u * u;
        }

        // ───────────── 사슬 철퇴(기획/전투-보스-무기-다듬기-1차.md 2-2 '0줄 + 사슬 선', 2-7) ─────────────

        /// <summary>쉬는 자세에서 손잡이 끝 고리부터 쇠공까지 늘어진 사슬 길이(유닛).</summary>
        public const float FlailChainRest = 0.2f;
        /// <summary>휘두를 때(내리꽂기·회오리·검풍) 팽팽해진 사슬 길이. 내리꽂기 판정 순간 쇠공이 앞 약 1.39(판정 원 중심 1.4)에 닿는다.</summary>
        public const float FlailChainSwing = 0.62f;
        /// <summary>돌려 치기 동안 쇠공이 도는 반지름(몸 중심에서, 판정 원 반지름 2.1 안). 관성이면 조금 더 멀리 끌려 돈다.</summary>
        public const float FlailOrbit = 1.45f;
        public const float FlailOrbitInertia = 1.55f;
        /// <summary>판정 뒤 쇠공이 감속하며 더 도는 각(도).</summary>
        const float FlailFollow = 150f;

        /// <summary>손잡이 끝에서 사슬을 손잡이 방향으로 곧게 이은 쇠공 자리(몸 틀). 길이 배율·크기(내리꽂기 투영)를 같이 받는다.</summary>
        public static Vector2 FlailTaut(TopDownHand hand, float tip, float chain)
        {
            float rad = hand.Angle * Mathf.Deg2Rad;
            return hand.Pos + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * ((tip + chain) * hand.Length * hand.Size);
        }

        /// <summary>
        /// 돌려 치기·되돌려 치기(사슬 철퇴 내 주변 원). 쇠공 각 θ(몸 틀, 0 = 앞)는 쉬는 자리(오른쪽 앞에 늘어진 쇠공)에서 출발해 한 바퀴 가까이 가속하며 돌고,
        /// 판정 순간에 정확히 앞(0°, 반지름 FlailOrbit)을 지난 뒤 감속해 150° 더 돌고, 남은 시간에 쉬는 자리로 늘어진다.
        /// 손은 어깨 앞에서 작은 원(반지름 0.13)을 그리며 쇠공보다 앞서 돌고(관성이면 더 앞서고 쇠공이 더 멀리 끌려 옴), 몸은 쇠공 쪽으로 조금만 비튼다.
        /// 회오리(몸이 세 바퀴 돌고 두 팔을 뻗음)와 겉보기가 겹치지 않게 몸은 돌지 않는다. dir +1 = 반시계(오른쪽 → 앞 → 왼쪽), −1 = 시계.
        /// env는 휘두르는 정도(0 쉼 → 1). ball은 쇠공 자리(몸 틀, 유닛).
        /// </summary>
        public static TopDownHand FlailSwing(TopDownHand rest, float t, float hit, float duration, float dir, bool inertia, out Vector2 ball, out float twist, out float env)
        {
            hit = Mathf.Max(1e-3f, hit);
            Vector2 restBall = FlailTaut(rest, TopDownSprites.FlailHandleTip, FlailChainRest);
            float restAngle = Mathf.Atan2(restBall.y, restBall.x) * Mathf.Rad2Deg;
            float start = dir > 0f ? restAngle - 360f : restAngle;
            float atHit = dir > 0f ? 0f : -360f;
            float sweep = Mathf.Max(1f, Mathf.Abs(atHit - start));
            float span = Mathf.Min(FlailFollow * hit / sweep, Mathf.Max(0.02f, duration - hit));
            float orbit = inertia ? FlailOrbitInertia : FlailOrbit;
            float theta, radius, back = 0f;
            if (t < hit)
            {
                // 가속하며 돌아(EaseIn) 판정 순간에 가장 빠르게 앞을 지난다. 사슬은 처음 0.6 동안 다 펴진다.
                float u = Mathf.Max(0f, t) / hit;
                theta = start + (atHit - start) * EaseIn(u);
                radius = Mathf.Lerp(restBall.magnitude, orbit, Smooth(u / 0.6f));
                env = Smooth(u / 0.35f);
            }
            else
            {
                // 판정 순간의 빠르기를 이어 받아 감속(EaseOut)하고, 남은 시간에 쉬는 자리로 늘어진다.
                theta = atHit + dir * FlailFollow * EaseOut((t - hit) / span);
                radius = orbit;
                back = Smooth(Mathf.Clamp01((t - hit - span) / Mathf.Max(1e-4f, duration - hit - span)));
                env = 1f - back;
            }
            float rad = theta * Mathf.Deg2Rad;
            Vector2 orbitBall = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
            ball = Vector2.Lerp(orbitBall, restBall, back);
            float lead = inertia ? 55f : 35f;
            float handRad = (theta + dir * lead) * Mathf.Deg2Rad;
            Vector2 handPos = new Vector2(0.14f, -0.16f) + new Vector2(Mathf.Cos(handRad), Mathf.Sin(handRad)) * 0.13f;
            Vector2 toBall = orbitBall - handPos;
            // 손잡이 각은 θ에 이어 붙여(±180° 넘김 없이) 쇠공 쪽보다 조금 앞을 가리킨다.
            float handle = theta + Mathf.DeltaAngle(theta, Mathf.Atan2(toBall.y, toBall.x) * Mathf.Rad2Deg) + dir * 12f;
            twist = 16f * Mathf.Sin(rad) * env;
            return TopDownHand.Follow(rest, TopDownHand.At(handPos, handle), env);
        }

        /// <summary>팔을 뻗은 채 몸과 함께 도는 손(side −1 오른손, +1 왼손). 칼날은 바깥을 향한다.</summary>
        public static TopDownHand Extended(float bodySpin, float side, float reach = 0.38f)
        {
            float a = bodySpin + side * 90f;
            float rad = a * Mathf.Deg2Rad;
            return TopDownHand.At(new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * reach, a);
        }

        /// <summary>쉬는 손 자세를 몸과 함께 돌린다(구르기·회전 시작).</summary>
        public static TopDownHand Tucked(TopDownHand rest, float spin, float shrink = 1f)
        {
            var h = rest;
            h.Pos = Rotate(rest.Pos * shrink, spin);
            h.Angle = rest.Angle + spin;
            return h;
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float cs = Mathf.Cos(r), sn = Mathf.Sin(r);
            return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
        }

        /// <summary>검풍 시전: 당겼다가 시전 끝(검풍이 나가는 순간)에 앞으로 짧게 찌른다.</summary>
        public static TopDownHand WaveThrust(TopDownHand rest, float t, float duration, out float twist)
        {
            const float pull = 0.11f;
            var back = TopDownHand.At(new Vector2(0f, -0.22f), -4f);
            var full = TopDownHand.At(new Vector2(0.36f, -0.08f), -2f);
            if (t < pull)
            {
                float u = EaseOut(t / pull);
                twist = -6f * u;
                return TopDownHand.Lerp(rest, back, u);
            }
            float k = EaseIn(Mathf.Clamp01((t - pull) / Mathf.Max(1e-4f, duration - pull)));
            twist = Mathf.Lerp(-6f, 6f, k);
            return TopDownHand.Lerp(back, full, k);
        }

        public static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        public static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        public static float EaseIn(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t;
        }

        public static float EaseInOut(float t) => Smooth(t);
    }
}
