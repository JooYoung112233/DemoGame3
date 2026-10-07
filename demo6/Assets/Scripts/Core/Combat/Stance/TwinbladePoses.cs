namespace Demo6.Core.Combat.Stance
{
    /// <summary>
    /// 쌍검 자세(3차, 2026-10-05 사용자 원문 "칼잡느 자세가 앞으로 하고 콤보는 우측 -> 좌측 -> 엇베기 -> 가위가르기 이런식이좋아보이는데",
    /// "단검 너무 무슨 뾰족이마냥 닾에 들고있지말고 자연스럽게 손에쥐고 있는느낌으로 바꿔줘").
    /// 쉬는 자세 = 두 칼을 자연스럽게 쥠: 두 손은 몸 양옆 허리~가슴 앞(오른손 (0.30, −0.48), 왼손 (0.28, 0.47)), 칼끝은 앞·바깥 아래로 느슨하게
    /// (오른칼 −40°, 왼칼 +33°, 좌우 조금 다름). 칼 길이·크기는 늘 1(3차, 늘이기·줄이기 없음). 콤보 = ① 우측 베기(오른칼 베고 되베기) · ② 좌측 베기(거울) ·
    /// ③ 엇베기(두 칼 X) · ④ 가위 가르기(바깥에서 안으로 닫으며 마무리). 오른쪽 클릭 난사 = 빠른 엇베기 반복.
    /// 손이 가는 호(spec '호_손'): 바깥 각 e = s × θ(오른손 s = −1, 왼손 +1), 손각 φ = 0.62θ + 16s, 반지름 r = 0.50 + 0.0007e, 손 = r(cos φ, sin φ)
    /// (오른손 y ≤ −0.065, 왼손 y ≥ +0.065: X에서 두 주먹이 겹치지 않음), 날 = θ, 길이·크기 1. 식은 설계 _tw4.py를 그대로 옮겼다.
    /// 쓸기(판정 ±0.05초)와 타 사이 간격은 공격 속도와 상관없이 고정이고 감기·거두기만 늘고 준다(실제 길이·첫 판정을 그대로 받음). 할당 없음.
    /// </summary>
    public static class TwinbladePoses
    {
        /// <summary>쉬는 자세: 오른손 (0.30, −0.48) −40°, 왼손 (0.28, 0.47) +33°, 길이·크기 1(2차 (0.38, ∓0.25) ∓8° 길이 0.9는 두 갈래 가시처럼 보였음).</summary>
        public static readonly HandPose RestRight = HandPose.At(0.30f, -0.48f, -40f);
        public static readonly HandPose RestLeft = HandPose.At(0.28f, 0.47f, 33f);
        /// <summary>걷기: 두 손이 걸음에 맞춰 좌우 반대로 앞뒤 ±0.03(게임 WeaponStanceLook이 비틀기 ÷ 6 = 걸음 위상으로 넣음).</summary>
        public const float WalkSway = 0.03f;
        public static readonly StancePose Rest = StancePose.Of(RestRight, RestLeft);

        /// <summary>날 각 범위(회오리 밖): 오른칼 −90° ~ +40°, 왼칼 −40° ~ +90°(④가 회전에서 가위로 바뀌어 몸 둘레를 돌지 않음. 난사 넘김 15 + 22 = 37°까지).</summary>
        public const float RightMinDeg = -90f;
        public const float RightMaxDeg = 40f;
        public const float LeftMinDeg = -40f;
        public const float LeftMaxDeg = 90f;
        /// <summary>호 손 반지름(0.415면 게임 몸 투구 앞에 자루 머리가 닿음)·몸 가운데 선 제한.</summary>
        public const float ArcRadius = 0.50f;
        public const float MidLine = 0.065f;

        // ── 콤보 설계 시간(공격 속도 0, WeaponPresets.Twinblades와 같아야 함) ──
        public static readonly float[] StepDuration = { 0.52f, 0.52f, 0.58f, 0.93f };
        public static readonly float[] StepHitMoment = { 0.30f, 0.30f, 0.33f, 0.33f };
        public static readonly int[] StepHits = { 2, 2, 2, 3 };
        public static readonly float[] StepInterval = { 0.10f, 0.10f, 0.06f, 0.05f };
        public const int StepCount = 4;

        /// <summary>쓸기 반 폭(판정 앞뒤 0.05초, 공격 속도 무관).</summary>
        public const float SweepHalf = 0.05f;
        /// <summary>바깥으로 벌린 각(감기·거두기 끝)·X로 모은 각(③·난사 마지막 X).</summary>
        public const float WideDeg = 70f;
        public const float CrossDeg = 18f;
        /// <summary>④ 가위 가르기: 크게 벌린 각·꽉 닫힌 X 각.</summary>
        public const float ScissorWideDeg = 85f;
        public const float ScissorCrossDeg = 24f;
        /// <summary>① 우측·② 좌측 베기: 1타 뒤 반대로 넘겨 멈칫하는 각, 2타 뒤 따라 흐르는 각, 반대 손이 비켜 주는 각.</summary>
        public const float BackCutFarDeg = 25f;
        public const float BackCutFollowDeg = 35f;
        public const float OffHandDeg = 18f;
        /// <summary>난사: 감기 0.12초(∓55°), 작은 타 펼침 45°·넘김 22°(TwinFlurry), 마지막 X 버팀 0.92초까지.</summary>
        public const float FlurryWind = 0.12f;
        public const float FlurryWindDeg = 55f;
        public const float FlurryCrossHoldEnd = 0.92f;
        /// <summary>처형·검풍.</summary>
        public const float ExecutionDuration = 0.40f;
        public const float ExecutionHit = 0.16f;
        public const float WaveDuration = 0.25f;
        /// <summary>회오리 팔 벌림 길이(쉬는 자세 → 호 ∓60 → (0.14, ∓0.63) ∓85° → (0, ∓0.62) ∓90°).</summary>
        public const float WhirlOpenTime = 0.12f;

        static float Max(float a, float b) => a > b ? a : b;
        static float Min(float a, float b) => a < b ? a : b;

        /// <summary>호 위 손. side: 오른손 −1, 왼손 +1. 날 각 theta(도).</summary>
        public static HandPose ArcHand(float theta, float side)
        {
            float e = side * theta;
            float phi = 0.62f * theta + 16f * side;
            float r = ArcRadius + 0.0007f * e;
            float x = r * StanceMath.Cos(phi);
            float y = r * StanceMath.Sin(phi);
            y = side < 0f ? Min(y, -MidLine) : Max(y, MidLine);
            return HandPose.At(x, y, theta);
        }

        static HandPose RestOf(float side) => side < 0f ? RestRight : RestLeft;

        /// <summary>키 값(구간 Smooth, 설계 keys1과 같음).</summary>
        static float Keys(float t, float t0, float v0, float t1, float v1, float t2, float v2, float t3, float v3, float t4, float v4)
        {
            if (t <= t0) return v0;
            if (t <= t1) return Seg(t, t0, v0, t1, v1);
            if (t <= t2) return Seg(t, t1, v1, t2, v2);
            if (t <= t3) return Seg(t, t2, v2, t3, v3);
            if (t <= t4) return Seg(t, t3, v3, t4, v4);
            return v4;
        }

        static float Keys(float t, float t0, float v0, float t1, float v1, float t2, float v2, float t3, float v3)
        {
            if (t <= t0) return v0;
            if (t <= t1) return Seg(t, t0, v0, t1, v1);
            if (t <= t2) return Seg(t, t1, v1, t2, v2);
            if (t <= t3) return Seg(t, t2, v2, t3, v3);
            return v3;
        }

        static float Seg(float t, float t0, float v0, float t1, float v1) => StanceMath.Lerp(v0, v1, StanceMath.Smooth((t - t0) / Max(1e-4f, t1 - t0)));

        static float Keys(float[] ts, float[] vs, float t)
        {
            if (t <= ts[0]) return vs[0];
            for (int i = 1; i < ts.Length; i++)
                if (t <= ts[i]) return Seg(t, ts[i - 1], vs[i - 1], ts[i], vs[i]);
            return vs[vs.Length - 1];
        }

        /// <summary>반대 손: 베는 칼이 지나갈 자리를 비켜 쉬는 자리에서 칼끝만 바깥으로 18° 더 벌렸다가 거두기에 돌아옴.</summary>
        static HandPose OffHand(float side, float t, float h, float d)
        {
            var rest = RestOf(side);
            float s = side < 0f ? -1f : 1f;
            var opened = HandPose.At(rest.X, rest.Y, rest.Angle + s * OffHandDeg);
            float rs = Max(h + 0.15f, d - 0.22f);
            if (t < h) return HandPose.Lerp(rest, opened, StanceMath.Smooth(t / h));
            if (t < rs) return opened;
            return HandPose.Lerp(opened, rest, StanceMath.Smooth((t - rs) / Max(1e-4f, d - rs)));
        }

        /// <summary>한 손 '베고 되베기': 쉬는 자세 → 바깥 70° 감기 → h에 0° → 반대로 25° 넘겨 멈칫 → h + iv에 0°로 되벰 → 35° 따라 흐름 → 쉬는 자세.</summary>
        static HandPose BackCut(float side, float t, float h, float iv, float d)
        {
            var rest = RestOf(side);
            float s = side < 0f ? 1f : -1f;
            float aWind = -s * WideDeg, aFar = s * BackCutFarDeg, aBack = -s * BackCutFollowDeg;
            float h2 = h + iv, t1 = h - SweepHalf;
            if (t < t1) return HandPose.Lerp(rest, ArcHand(aWind, side), StanceMath.EaseOut(t / Max(1e-4f, t1)));
            if (t < h) return ArcHand(StanceMath.Lerp(aWind, 0f, StanceMath.Smooth((t - t1) / SweepHalf)), side);
            float mid = (h + h2) * 0.5f;
            if (t < mid) return ArcHand(StanceMath.Lerp(0f, aFar, StanceMath.EaseOut((t - h) / (mid - h))), side);
            if (t < h2) return ArcHand(StanceMath.Lerp(aFar, 0f, StanceMath.EaseIn((t - mid) / (h2 - mid))), side);
            float fe = h2 + SweepHalf;
            if (t < fe) return ArcHand(StanceMath.Lerp(0f, aBack, StanceMath.EaseOut((t - h2) / SweepHalf)), side);
            float rs = Max(fe + 0.03f, d - 0.20f);
            if (t < rs) return ArcHand(aBack, side);
            return HandPose.Lerp(ArcHand(aBack, side), rest, StanceMath.Smooth((t - rs) / Max(1e-4f, d - rs)));
        }

        /// <summary>엇베기 한 손: 감기(windEnd까지) → hit에 0° → ±18° X → rs부터 쉬는 자세.</summary>
        static HandPose XCut(float side, float t, float hit, float rs, float d, float windEnd)
        {
            var rest = RestOf(side);
            float s = side < 0f ? 1f : -1f;
            float aFrom = -s * WideDeg, aTo = s * CrossDeg;
            float t1 = hit - SweepHalf;
            if (t < windEnd) return HandPose.Lerp(rest, ArcHand(aFrom, side), StanceMath.EaseOut(t / Max(1e-4f, windEnd)));
            if (t < t1) return ArcHand(aFrom, side);
            if (t < hit) return ArcHand(StanceMath.Lerp(aFrom, 0f, StanceMath.Smooth((t - t1) / SweepHalf)), side);
            if (t < hit + SweepHalf) return ArcHand(StanceMath.Lerp(0f, aTo, StanceMath.Smooth((t - hit) / SweepHalf)), side);
            if (t < rs) return ArcHand(aTo, side);
            return HandPose.Lerp(ArcHand(aTo, side), rest, StanceMath.Smooth((t - rs) / Max(1e-4f, d - rs)));
        }

        /// <summary>가위 가르기 한 손: w까지 ∓85°로 크게 벌림 → closeAt에 0°(닫힐수록 빨라짐, EaseIn) → h3에 ±24° 꽉 닫힌 X → rs부터 쉬는 자세.</summary>
        static HandPose ScissorHand(float side, float t, float w, float closeAt, float h3, float rs, float d)
        {
            var rest = RestOf(side);
            float s = side < 0f ? 1f : -1f;
            if (t < w) return HandPose.Lerp(rest, ArcHand(-s * ScissorWideDeg, side), StanceMath.EaseOut(t / Max(1e-4f, w)));
            if (t < closeAt) return ArcHand(StanceMath.Lerp(-s * ScissorWideDeg, 0f, StanceMath.EaseIn((t - w) / (closeAt - w))), side);
            if (t < h3) return ArcHand(StanceMath.Lerp(0f, s * ScissorCrossDeg, StanceMath.Smooth((t - closeAt) / (h3 - closeAt))), side);
            if (t < rs) return ArcHand(s * ScissorCrossDeg, side);
            return HandPose.Lerp(ArcHand(s * ScissorCrossDeg, side), rest, StanceMath.Smooth((t - rs) / Max(1e-4f, d - rs)));
        }

        /// <summary>
        /// 콤보 ① 우측 베기 0.52 / ② 좌측 베기 0.52 / ③ 엇베기 0.58 / ④ 가위 가르기 0.93. duration·hit = 실제 길이·첫 판정(공격 속도 반영), 타 간격은 고정.
        /// </summary>
        public static StancePose Combo(int step, float t, float duration, float hit)
        {
            int i = step <= 0 ? 0 : step >= StepCount - 1 ? StepCount - 1 : step;
            if (duration <= 0f) duration = StepDuration[i];
            if (hit < 0f) hit = StepDuration[i] * StepHitMoment[i];
            float iv = StepInterval[i];
            float d = duration, h = hit;
            var p = Rest;
            switch (i)
            {
                case 0:
                    p.Right = BackCut(-1f, t, h, iv, d);
                    p.Left = OffHand(1f, t, h, d);
                    p.Twist = Keys(t, 0f, 0f, h - SweepHalf, -6f, h + iv * 0.5f, 6f, h + iv + SweepHalf, -3f, d, 0f);
                    p.LeanX = Keys(t, 0f, 0f, h - SweepHalf, -0.02f, h, 0.03f, d, 0f);
                    break;
                case 1:
                    p.Left = BackCut(1f, t, h, iv, d);
                    p.Right = OffHand(-1f, t, h, d);
                    p.Twist = Keys(t, 0f, 0f, h - SweepHalf, 6f, h + iv * 0.5f, -6f, h + iv + SweepHalf, 3f, d, 0f);
                    p.LeanX = Keys(t, 0f, 0f, h - SweepHalf, -0.02f, h, 0.03f, d, 0f);
                    break;
                case 2:
                {
                    float hR = h, hL = h + iv;
                    float rs = Max(hL + SweepHalf + 0.04f, d - 0.30f);
                    p.Right = XCut(-1f, t, hR, rs, d, hR - SweepHalf);
                    p.Left = XCut(1f, t, hL, rs, d, hR - SweepHalf);
                    p.Twist = Keys(t, 0f, 0f, hR - SweepHalf, -6f, hR + SweepHalf, 6f, hL + SweepHalf, -4f, d, 0f);
                    p.LeanX = Keys(t, 0f, 0f, hR - SweepHalf, -0.03f, hL, 0.04f, rs, 0.03f, d, 0f);
                    break;
                }
                default:
                {
                    float h1 = h, h2 = h + iv, h3 = h + 2f * iv;
                    float w = h - 0.06f;
                    float rs = Max(h3 + 0.15f, d - 0.30f);
                    p.Right = ScissorHand(-1f, t, w, h1, h3, rs, d);
                    p.Left = ScissorHand(1f, t, w, h2, h3, rs, d);
                    p.LeanX = Keys(t, 0f, 0f, w, -0.04f, h3, 0.05f, rs, 0.04f, d, 0f);
                    p.ScaleX = Keys(t, 0f, 1f, w, 1.03f, h3, 1.04f, d, 1f);
                    p.ScaleY = Keys(t, 0f, 1f, w, 0.96f, h3, 0.95f, d, 1f);
                    break;
                }
            }
            return p;
        }

        // 난사 몸: 크기 (1.02, 0.96)로 낮춤, 앞뒤, 타마다 비틀기 ±5°(설계 flurry와 같음).
        static readonly float[] FlScaleT = { 0f, FlurryWind, 0.84f, TwinFlurry.Duration };
        static readonly float[] FlScaleX = { 1f, 1.02f, 1.02f, 1f };
        static readonly float[] FlScaleY = { 1f, 0.96f, 0.96f, 1f };
        static readonly float[] FlLeanT = { 0f, FlurryWind, 0.18f, 0.78f, 0.84f, FlurryCrossHoldEnd, TwinFlurry.Duration };
        static readonly float[] FlLeanV = { 0f, -0.03f, 0.02f, 0.02f, 0.05f, 0.05f, 0f };
        static readonly float[] FlTwistT = MakeFlurryTwistT();
        static readonly float[] FlTwistV = MakeFlurryTwistV();

        static float[] MakeFlurryTwistT()
        {
            var a = new float[11];
            a[0] = 0f;
            a[1] = FlurryWind;
            for (int k = 0; k < 7; k++) a[2 + k] = TwinFlurry.HitTimes[k];
            a[9] = TwinFlurry.HitTimes[7];
            a[10] = TwinFlurry.Duration;
            return a;
        }

        static float[] MakeFlurryTwistV()
        {
            var a = new float[11];
            for (int k = 0; k < 7; k++) a[2 + k] = (k & 1) == 0 ? 5f : -5f;
            return a;
        }

        /// <summary>
        /// 난사 한 손(빠른 엇베기 반복): 0.12초까지 ∓55°로 감고, 작은 타마다 그 손이 바깥(중심 ∓45°) → 타 순간 중심 c → 0.035초 뒤 반대로 22° 넘김,
        /// 넘긴 칼은 다른 손의 다음 판정(+0.09초)까지 버텨 X를 만든 뒤 다시 벌림. 마지막: ∓70° → 0.84 둘 다 0° → 0.87 ±18° X → 0.92 → 1.10 쉬는 자세.
        /// </summary>
        static HandPose FlurryHand(float side, float t)
        {
            var rest = RestOf(side);
            float sgn = side < 0f ? 1f : -1f;
            if (t < FlurryWind) return HandPose.Lerp(rest, ArcHand(-sgn * FlurryWindDeg, side), StanceMath.EaseOut(t / FlurryWind));
            float prevTh = -sgn * FlurryWindDeg;
            float prevT = FlurryWind;
            int first = side < 0f ? 0 : 1;
            float half = TwinFlurry.SweepHalfTime;
            for (int k = first; k < 7; k += 2)
            {
                float hk = TwinFlurry.HitTimes[k];
                float c = TwinFlurry.SweepCenterDeg[k];
                float a0 = c - sgn * TwinFlurry.SweepOpenDeg;
                float a1 = c + sgn * TwinFlurry.SweepPastDeg;
                float t1 = hk - half;
                float t2 = hk + half;
                if (t < t1) return ArcHand(StanceMath.Lerp(prevTh, a0, StanceMath.Smooth((t - prevT) / Max(1e-4f, t1 - prevT))), side);
                if (t < hk) return ArcHand(StanceMath.Lerp(a0, c, StanceMath.Smooth((t - t1) / half)), side);
                if (t < t2) return ArcHand(StanceMath.Lerp(c, a1, StanceMath.Smooth((t - hk) / half)), side);
                float holdEnd = Min(hk + 0.09f, 0.80f);
                if (t < holdEnd) return ArcHand(a1, side);
                prevTh = a1;
                prevT = holdEnd;
            }
            float hX = TwinFlurry.HitTimes[7];
            float tw = hX - SweepHalf;
            float wide = -sgn * WideDeg;
            float cross = sgn * CrossDeg;
            if (t < tw) return ArcHand(StanceMath.Lerp(prevTh, wide, StanceMath.Smooth((t - prevT) / Max(1e-4f, tw - prevT))), side);
            if (t < hX) return ArcHand(StanceMath.Lerp(wide, 0f, StanceMath.Smooth((t - tw) / SweepHalf)), side);
            if (t < hX + 0.03f) return ArcHand(StanceMath.Lerp(0f, cross, StanceMath.Smooth((t - hX) / 0.03f)), side);
            if (t < FlurryCrossHoldEnd) return ArcHand(cross, side);
            float u = (t - FlurryCrossHoldEnd) / (TwinFlurry.Duration - FlurryCrossHoldEnd);
            return HandPose.Lerp(ArcHand(cross, side), rest, StanceMath.Smooth(u));
        }

        /// <summary>난사(행동 시작부터 t초, 공격 속도 무관).</summary>
        public static StancePose Flurry(float t)
        {
            var p = Rest;
            p.Right = FlurryHand(-1f, t);
            p.Left = FlurryHand(1f, t);
            p.ScaleX = Keys(FlScaleT, FlScaleX, t);
            p.ScaleY = Keys(FlScaleT, FlScaleY, t);
            p.LeanX = Keys(FlLeanT, FlLeanV, t);
            p.Twist = Keys(FlTwistT, FlTwistV, t);
            return p;
        }

        /// <summary>난사(Flurry: PhaseTime 0 ~ 1.10)와 끊김(Flinch: 맞음 자세).</summary>
        public static StancePose Act(in ActStance act)
        {
            switch (act.Phase)
            {
                case WeaponActPhase.Flurry: return Flurry(act.PhaseTime < 0f ? 0f : act.PhaseTime);
                case WeaponActPhase.Flinch: return Hurt(1f - StanceMath.Clamp01(act.PhaseTime / 0.2f), 0f, 0f);
                default: return Rest;
            }
        }

        /// <summary>검풍(Q, 0.25초): 오른칼만. 0.11초에 호 −60으로 감고(EaseOut), 0.25초에 앞 (0.52, −0.14) −4°로 뻗음(EaseIn). 왼칼은 쉬는 자세.</summary>
        public static StancePose Wave(float t, float duration)
        {
            float bt = duration > 0f ? t * WaveDuration / duration : t;
            var pull = ArcHand(-60f, -1f);
            var full = HandPose.At(0.52f, -0.14f, -4f);
            var p = Rest;
            if (bt < 0.11f)
            {
                float u = StanceMath.EaseOut(bt / 0.11f);
                p.Right = HandPose.Lerp(RestRight, pull, u);
                p.Twist = StanceMath.Lerp(0f, -6f, u);
                p.LeanX = StanceMath.Lerp(0f, -0.02f, u);
            }
            else
            {
                float u = StanceMath.EaseIn((bt - 0.11f) / (WaveDuration - 0.11f));
                p.Right = HandPose.Lerp(pull, full, u);
                p.Twist = StanceMath.Lerp(-6f, 6f, u);
                p.LeanX = StanceMath.Lerp(-0.02f, 0.04f, u);
            }
            p.Glow = StanceMath.Smooth((t - 0.06f) / 0.12f);
            return p;
        }

        /// <summary>
        /// 회오리(E) 도형 판: 시작부터 t초의 팔 벌림(0.12초, 쉬는 자세 → 호 ∓60 → (0.14, ∓0.63) ∓85° → (0, ∓0.62) ∓90°, 곧게 섞으면 자루가 어깨받이를 뚫음)을
        /// 몸 회전 spinDeg만큼 같이 돌린다. 비틀기 = 회전.
        /// </summary>
        public static StancePose Whirl(float spinDeg, float t)
        {
            float o = StanceMath.Smooth(t / WhirlOpenTime) * 3f;
            int seg = o >= 2f ? 2 : o >= 1f ? 1 : 0;
            float u = StanceMath.Smooth(o - seg);
            var r = HandPose.Lerp(WhirlPoint(seg), WhirlPoint(seg + 1), u);
            // 쉬는 자세가 좌우 다르므로(3차) 왼손은 왼손 쉬는 자세에서 출발하고 그 뒤 경유점은 오른손의 거울.
            var l = HandPose.Lerp(WhirlPointLeft(seg), WhirlPointLeft(seg + 1), u);
            var p = Rest;
            p.Right = StanceMath.Spun(r, spinDeg);
            p.Left = StanceMath.Spun(l, spinDeg);
            p.Twist = spinDeg;
            return p;
        }

        static HandPose WhirlPointLeft(int i)
        {
            if (i == 0) return RestLeft;
            var r = WhirlPoint(i);
            return HandPose.At(r.X, -r.Y, -r.Angle, r.Length, r.Size);
        }

        static HandPose WhirlPoint(int i)
        {
            switch (i)
            {
                case 0: return RestRight;
                case 1: return ArcHand(-60f, -1f);
                case 2: return HandPose.At(0.14f, -0.63f, -85f);
                default: return HandPose.At(0f, -0.62f, -90f);
            }
        }

        /// <summary>
        /// 처형(0.40초, 판정 0.16): 두 칼을 호 ∓70으로 벌리고(0.06) 판정에 함께 0°, 0.22에 ±12° X로 모아 누름. 칼 배율 없음(3차, 늘 1).
        /// 몸 눌림 (1 + 0.05q, 1 − 0.06q)은 판정부터(q = 1 → 0). 앞뒤 최대 0.02. 머리 위 Slam을 쓰지 않는다.
        /// </summary>
        public static StancePose Execution(float t, float duration, float hit)
        {
            float bt = StanceMath.ToBaseTime(t, hit >= 0f ? hit : ExecutionHit, duration > 0f ? duration : ExecutionDuration, ExecutionHit, ExecutionDuration);
            bt = bt < 0f ? 0f : bt > ExecutionDuration ? ExecutionDuration : bt;
            var p = Rest;
            if (bt < 0.06f)
            {
                float u = StanceMath.EaseOut(bt / 0.06f);
                p.Right = HandPose.Lerp(RestRight, ArcHand(-WideDeg, -1f), u);
                p.Left = HandPose.Lerp(RestLeft, ArcHand(WideDeg, 1f), u);
                p.LeanX = StanceMath.Lerp(0f, -0.03f, u);
                return p;
            }
            if (bt < ExecutionHit)
            {
                float u = StanceMath.EaseIn((bt - 0.06f) / (ExecutionHit - 0.06f));
                float th = StanceMath.Lerp(WideDeg, 0f, u);
                p.Right = ArcHand(-th, -1f);
                p.Left = ArcHand(th, 1f);
                p.LeanX = StanceMath.Lerp(-0.03f, 0.02f, u);
                return p;
            }
            float cross = 12f * StanceMath.Smooth((bt - ExecutionHit) / 0.06f);
            p.Right = ArcHand(cross, -1f);
            p.Left = ArcHand(-cross, 1f);
            p.LeanX = 0.02f;
            float q = StanceMath.Clamp01(1f - (bt - ExecutionHit) / (ExecutionDuration - ExecutionHit));
            p.ScaleX = 1f + 0.05f * q;
            p.ScaleY = 1f - 0.06f * q;
            return p;
        }

        /// <summary>구르기: 쉬는 자세를 몸과 같이 돌림(줄임 1.0). 비틀기 = 회전.</summary>
        public static StancePose Dodge(float spinDeg)
        {
            var p = Rest;
            p.Right = StanceMath.Spun(p.Right, spinDeg);
            p.Left = StanceMath.Spun(p.Left, spinDeg);
            p.Twist = spinDeg;
            return p;
        }

        /// <summary>맞음: 쉬는 자세에서 날 ∓10k°(바깥으로), 손도 몸과 같이 밀림, 몸 1 − 0.04k.</summary>
        public static StancePose Hurt(float k, float pushX, float pushY)
        {
            k = StanceMath.Clamp01(k);
            var p = Rest;
            p.Right.X += pushX;
            p.Right.Y += pushY;
            p.Right.Angle -= 10f * k;
            p.Left.X += pushX;
            p.Left.Y += pushY;
            p.Left.Angle += 10f * k;
            p.LeanX = pushX;
            p.LeanY = pushY;
            p.ScaleX = p.ScaleY = 1f - 0.04f * k;
            return p;
        }

        /// <summary>쓰러짐: 두 칼을 양옆 바닥에 떨어뜨림(리그가 몸 밑에 그림).</summary>
        public static StancePose Down(float f) => StancePose.Of(HandPose.At(0.06f, -0.6f, -110f), HandPose.At(0f, 0.6f, 110f));
    }
}
