using System;

namespace Demo6.Core.Combat.Stance
{
    /// <summary>
    /// 대검 자세(3차, 2026-10-05). 사용자 원문: "대검은 두께이야기한건데 길이말고 음 다좋은데 대검일때 약간 어색해보이긴하네",
    /// "대검을 세로로내려찍을때표현이약간 부족한것같긴하네", "대검그림을 압축하는게아니라 진짜로 칼탈을 돌려야 좀더 자연스러울것같아",
    /// "지금보니 검들이 다늘어나고 압축되는데 이부분이 제일어색한것같다".
    /// 키는 StanceKeyTables(spec.json 3차 설계 키 그대로)이고 StanceKeys.Eval이 몸 틀에서 섞어 읽는다. 오른손 = 날밑 쪽 손,
    /// 둘째 손(Left)은 SecondHand(오른손 + 회전((−0.18 × max(cos 기울기, 0.65) × 크기 × 칼 배율, 0), 날))로 놓는다(두 주먹 사이 자루가 보임).
    /// 칼은 넓은 날 임시 모양(날 폭 0.66 = 몸 폭 0.99의 2/3, 자루 끝 ~ 칼끝 1.635 = 원화 길이)이고 그림 배율은 늘 1이다. 칼이 서거나 칼끝이 땅을 향하는 것은
    /// 길이를 줄이지 않고 기울기(Right.Tilt, + 칼끝 카메라 쪽, − 땅 쪽)로 적고, 게임은 기울기별 그림(GreatswordTiltArt)을 갈아 끼운다.
    /// 쉬는 자세 = 두 손을 오른 허리 옆에 모으고 칼끝을 땅 쪽(−56°)으로 오른쪽 아래로 늘어뜨림. ③ 내려 쪼개기 = 몸 앞·오른쪽에서 칼을 카메라 쪽으로
    /// 곧게 세워(90°) 버틴 뒤 0.12초에 앞으로 눕혀 바닥에 쾅(기울기 0 = 판정 순간), 칼 그림자(Height)와 몸 숙임으로 높이를 보인다.
    /// </summary>
    public static class GreatswordPoses
    {
        const StanceWeapon W = StanceWeapon.Greatsword;

        /// <summary>쉬는 자세: 오른손 (0.16, −0.56), 날 −82°, 기울기 −56°(칼끝 땅 쪽), 길이·크기 1.</summary>
        public static readonly StancePose Rest = StanceKeys.FromKey(StanceKeyTables.GreatRest[0], W);

        /// <summary>둘째 손 자리 배율(TopDownWeaponLook.SecondGrip과 같음).</summary>
        public const float SecondGrip = -0.18f;
        /// <summary>둘째 손 거리의 기울기 바닥: 칼이 서서 자루가 짧아 보여도 두 주먹이 붙지 않게(max(cos 기울기, 0.65)).</summary>
        public const float SecondGripLengthFloor = 0.65f;

        // ── 넓은 날 임시 모양(spec 3차 '무기_모양.대검', 쥔 점 기준 날 방향 거리, 유닛). 게임 그림·검사가 같이 쓴다 ──
        /// <summary>자루 머리 중심·반지름.</summary>
        public const float PommelCenter = -0.27f;
        public const float PommelRadius = 0.035f;
        /// <summary>손잡이 [시작, 끝], 반폭.</summary>
        public const float GripFrom = -0.24f;
        public const float GripTo = 0.06f;
        public const float GripHalfWidth = 0.04f;
        /// <summary>날밑 자리·반두께·반폭(폭 0.80).</summary>
        public const float GuardCenter = 0.085f;
        public const float GuardHalfThick = 0.025f;
        public const float GuardHalfWidth = 0.40f;
        /// <summary>날 시작 ~ 칼끝, 반폭(밑 0.33 = 폭 0.66), 칼끝이 좁아지기 시작하는 자리(1.03, 반폭 0.29).</summary>
        public const float BladeFrom = 0.11f;
        public const float TipTaperFrom = 1.03f;
        public const float TipAt = 1.33f;
        public const float BladeHalfWidth = 0.33f;
        public const float BladeHalfWidthAtTaper = 0.29f;
        /// <summary>날 두께(가운데 등줄, 기울면 두께 띠로 보임).</summary>
        public const float BladeThickness = 0.07f;
        /// <summary>몸 폭(게임 화면 어깨받이 바깥 ~ 바깥, 08-크기-재기 172 ~ 393픽셀 ÷ 225) 0.99의 2/3 = 날 폭 0.66.</summary>
        public const float BodyWidth = 0.99f;

        /// <summary>
        /// 기울기별 그림 칸(도): 원화(Aseprite, 기울기마다 한 장)와 코드 임시 그림(GreatswordTiltArt)이 같은 차례로 채운다. 가장 가까운 칸을 쓴다
        /// (15° 밑은 0°, 15 ~ 37.5° 30°, ~ 52.5° 45°, ~ 70° 60°, 70° 위 80°, 칼끝을 내릴 때도 같은 간격).
        /// </summary>
        public static readonly float[] TiltBins = { -80f, -60f, -45f, -30f, 0f, 30f, 45f, 60f, 80f };

        /// <summary>기울기(도)에 가장 가까운 그림 칸 번호(할당 없음).</summary>
        public static int TiltBinOf(float tilt)
        {
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < TiltBins.Length; i++)
            {
                float d = Math.Abs(TiltBins[i] - tilt);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>걷기: 두 손이 걸음에 맞춰 앞뒤 ±0.02(게임 WeaponStanceLook이 비틀기 ÷ 6 = 걸음 위상으로 넣음).</summary>
        public const float WalkSway = 0.02f;

        /// <summary>쉬는 자세에서 떠나고 돌아오는 자리(오른 허리 옆, 날 −88°, 기울기 −54°): 자루 머리가 오른 어깨받이 위를 지나지 않게 손을 먼저 뺀다.</summary>
        public static readonly HandPose Leave = HandPose.Tilted(0.24f, -0.58f, -88f, -54f);

        // ── 콤보 설계 시간(공격 속도 0, WeaponPresets.Greatsword와 같아야 함) ──
        public const float Step1Duration = 0.88f;
        public const float Step1Hit = 0.352f;
        public const float Step2Duration = 0.92f;
        public const float Step2Hit = 0.3864f;
        public const float Step3Duration = 1.30f;
        public const float Step3Hit = 0.65f;
        public const int StepCount = 3;

        /// <summary>놓아 베기 단계(1~3)별 길이·판정(GreatswordCharge.Release와 같아야 함).</summary>
        static readonly float[] ReleaseDuration = { 0.62f, 0.68f, 0.80f };
        static readonly float[] ReleaseHitTime = { 0.1705f, 0.17f, 0.192f };
        /// <summary>끊김 자세 길이(0.45초 경직과 같음).</summary>
        public const float InterruptDuration = 0.45f;
        /// <summary>처형(0.40초, 판정 0.16).</summary>
        public const float ExecutionDuration = 0.40f;
        public const float ExecutionHit = 0.16f;
        public const float WaveDuration = 0.25f;
        /// <summary>회오리 팔 뻗기 키 길이.</summary>
        public const float WhirlOpenTime = 0.08f;
        /// <summary>떨림: 3단계부터 ±1.5°(초당 16번), 2.2초부터 ±3°.</summary>
        public const float TrembleDeg = 1.5f;
        public const float TrembleWarnDeg = 3f;
        public const float TrembleHz = 16f;
        /// <summary>놓아 베기 빛 줄: 판정 + 0.1초까지 켜고 0.15초에 걸쳐 끈다. 끊기면 0.1초에 꺼진다.</summary>
        public const float ReleaseGlowHold = 0.1f;
        public const float ReleaseGlowFade = 0.15f;
        public const float InterruptGlowFade = 0.1f;

        static StanceKey[] ReleaseKeys(int level) =>
            level <= 1 ? StanceKeyTables.GreatRelease1 : level >= 3 ? StanceKeyTables.GreatRelease3 : StanceKeyTables.GreatRelease2;

        /// <summary>
        /// 둘째 손(자루 끝 쪽 왼손): 오른손 + 회전((−0.18 × max(cos 기울기, 0.65) × 크기 × 칼 배율, 0), 날). 게임(WeaponStanceArms)·검사가 같은 식을 쓴다.
        /// </summary>
        public static HandPose SecondHand(HandPose right, float bladeScale = 1f)
        {
            float len = Math.Max(StanceMath.Cos(right.Tilt), SecondGripLengthFloor);
            float d = SecondGrip * len * right.Size * bladeScale;
            var h = right;
            h.X = right.X + d * StanceMath.Cos(right.Angle);
            h.Y = right.Y + d * StanceMath.Sin(right.Angle);
            return h;
        }

        static void DesignTime(int step, out float duration, out float hit)
        {
            switch (step <= 0 ? 0 : step >= 2 ? 2 : 1)
            {
                case 0: duration = Step1Duration; hit = Step1Hit; break;
                case 1: duration = Step2Duration; hit = Step2Hit; break;
                default: duration = Step3Duration; hit = Step3Hit; break;
            }
        }

        /// <summary>단계(0부터)의 설계 길이·판정(시험·문서 대조용).</summary>
        public static void ComboDesign(int step, out float duration, out float hit) => DesignTime(step, out duration, out hit);

        /// <summary>놓아 베기 단계(1~3)의 길이·판정 순간(초, 공격 속도 무관).</summary>
        public static void ReleaseDesign(int level, out float duration, out float hit)
        {
            int i = level <= 1 ? 0 : level >= 3 ? 2 : 1;
            duration = ReleaseDuration[i];
            hit = ReleaseHitTime[i];
        }

        /// <summary>콤보 ① 걷어 베기 0.88 / ② 치켜 베기 0.92 / ③ 내려 쪼개기 1.30.</summary>
        public static StancePose Combo(int step, float t, float duration, float hit)
        {
            DesignTime(step, out float bd, out float bh);
            float bt = StanceMath.ToBaseTime(t, hit, duration, bh, bd);
            bt = bt < 0f ? 0f : bt > bd ? bd : bt;
            var keys = step <= 0 ? StanceKeyTables.GreatStep1 : step >= 2 ? StanceKeyTables.GreatStep3 : StanceKeyTables.GreatStep2;
            return StanceKeys.Eval(keys, bt, W);
        }

        /// <summary>
        /// 기 모으기 자세(모은 시간 held): 몸을 오른쪽으로 점점 감고(비틀기 −16 → −28 → −36 → −44) 칼을 뒤로 끌어 수평에 가깝게(키 사이 Smooth).
        /// 3단계부터 날 떨림 ±1.5°(2.2초부터 ±3°, 초당 16번). 빛 줄은 단계대로.
        /// </summary>
        public static StancePose Charge(float held)
        {
            if (held < 0f) held = 0f;
            var p = StanceKeys.Eval(StanceKeyTables.GreatCharge, held, W);
            if (held >= GreatswordCharge.L3)
            {
                float lt = held - GreatswordCharge.L3;
                float amp = held >= GreatswordCharge.WarnAt ? TrembleWarnDeg : TrembleDeg;
                p.Right.Angle += amp * (float)Math.Sin(2.0 * Math.PI * TrembleHz * lt);
                p.Left = SecondHand(p.Right, p.BladeScale);
            }
            p.Glow = ChargeGlow(held);
            return p;
        }

        /// <summary>
        /// 기 모으기 빛 줄(3-6): 1단계 전 0 → 0.15, 1단계 0.35, 2단계 0.60, 3단계 0.90(±0.1, 초당 6번). 단계에 닿을 때 +0.4가 0.08초 번쩍. 0~1로 자름.
        /// </summary>
        public static float ChargeGlow(float held)
        {
            int level = GreatswordCharge.LevelAt(held);
            float g;
            if (level <= 0) g = GreatswordCharge.GlowByLevel[0] * StanceMath.Clamp01(held / GreatswordCharge.L1);
            else if (level < 3) g = GreatswordCharge.GlowByLevel[level];
            else g = GreatswordCharge.GlowByLevel[3] + 0.1f * (float)Math.Sin(2.0 * Math.PI * 6.0 * (held - GreatswordCharge.L3));
            if (level >= 1 && held - GreatswordCharge.LevelTime(level) < GreatswordCharge.GlowFlashTime) g += GreatswordCharge.GlowFlash;
            return StanceMath.Clamp01(g);
        }

        /// <summary>놓아 베기(단계 1~3, 시작부터 t초): ① 걷어 베기와 같은 쓸기, 같은 칼끝 땅 귀가. 빛 줄은 그 단계 세기로 판정 + 0.1초까지, 그 뒤 0.15초에 꺼진다.</summary>
        public static StancePose Release(int level, float t)
        {
            int i = level <= 1 ? 0 : level >= 3 ? 2 : 1;
            var p = StanceKeys.Eval(ReleaseKeys(i + 1), t < 0f ? 0f : t, W);
            float on = ReleaseHitTime[i] + ReleaseGlowHold;
            float g = GreatswordCharge.GlowByLevel[i + 1];
            p.Glow = t <= on ? g : g * StanceMath.Clamp01(1f - (t - on) / ReleaseGlowFade);
            return p;
        }

        /// <summary>
        /// 끊김 자세(3-8): 끊긴 순간 자세(모으는 중이면 그 모은 시간의 모으는 자세, 놓아 베기 중이면 그 단계 시작 자세)에서 0.12초 몸이 풀리며 칼끝이 땅 쪽으로
        /// 떨어진 키(비틀기 −8, 기울기 −50)로(EaseOut, 몸 틀), 0.30초 오른 허리 옆(Leave), 0.45초 쉬는 자세. 빛 줄은 0.1초에 꺼진다.
        /// </summary>
        public static StancePose Interrupted(float t, float held, int releaseLevel)
        {
            if (t < 0f) t = 0f;
            StancePose from = releaseLevel > 0 ? Release(releaseLevel, 0f) : Charge(held);
            float glow0 = releaseLevel > 0 ? GreatswordCharge.GlowByLevel[releaseLevel >= 3 ? 3 : releaseLevel] : ChargeGlow(held);
            var keys = StanceKeyTables.GreatInterrupt;
            StancePose p;
            if (t < keys[1].T)
            {
                var a = StanceKey.FromPose(from, W);
                p = StanceKeys.Interp(a, keys[1], StanceCurves.Apply(keys[1].Curve, t / keys[1].T), W);
            }
            else p = StanceKeys.Eval(keys, t, W);
            p.Glow = glow0 * StanceMath.Clamp01(1f - t / InterruptGlowFade);
            return p;
        }

        /// <summary>기 모으기(Charging: HeldTime), 놓아 베기(Release: ReleaseLevel·PhaseTime), 끊김(Flinch: 3-8 끊김 자세).</summary>
        public static StancePose Act(in ActStance act)
        {
            switch (act.Phase)
            {
                // 모은 시간이 비어 있으면(0) 이 단계 시간으로 본다(모으는 단계는 행동 시작과 같이 시작).
                case WeaponActPhase.Charging: return Charge(act.HeldTime > 0f ? act.HeldTime : act.PhaseTime);
                case WeaponActPhase.Release:
                    return Release(act.ReleaseLevel > 0 ? act.ReleaseLevel : act.ChargeLevel > 0 ? act.ChargeLevel : 1, act.PhaseTime);
                case WeaponActPhase.Flinch:
                    if (act.FlinchKind == WeaponActKind.Charge || act.FlinchKind == WeaponActKind.None)
                    {
                        // 모은 시간이 비어 있으면 끊긴 순간의 행동 시간(= 모은 시간, 모으기는 행동 시작부터)으로 본다.
                        float held = act.HeldTime > 0f ? act.HeldTime : Math.Max(0f, act.ActTime - act.PhaseTime);
                        return Interrupted(act.PhaseTime, held, act.ReleaseLevel);
                    }
                    return Hurt(1f - StanceMath.Clamp01(act.PhaseTime / 0.2f), 0f, 0f);
                default: return Rest;
            }
        }

        /// <summary>검풍(Q, 0.25초): ① 걷어 베기를 줄인 가로 쓸기(0.10초 감기 비틀기 −26, 0.25초에 날 0° = 판정). 빛 줄은 리그와 같은 식.</summary>
        public static StancePose Wave(float t, float duration)
        {
            float bt = duration > 0f ? t * WaveDuration / duration : t;
            var p = StanceKeys.Eval(StanceKeyTables.GreatWave, bt, W);
            p.Glow = StanceMath.Smooth((t - 0.06f) / 0.12f);
            return p;
        }

        /// <summary>
        /// 회오리(E) 도형 판: 시작부터 t초에 손을 먼저 오른 허리 옆으로 뺀 뒤(0.04초) 칼을 오른쪽 앞 바깥으로 눕힘(0.08초에 오른손 (0.40, −0.56) −75°, 기울기 0, 몸 틀)
        /// 그 자세를 몸 회전 spinDeg만큼 같이 돌린다. 둘째 손은 자루. 비틀기 = 회전.
        /// </summary>
        public static StancePose Whirl(float spinDeg, float t)
        {
            var p = StanceKeys.Eval(StanceKeyTables.GreatWhirl, t < 0f ? 0f : t, W);
            p.Right = StanceMath.Spun(p.Right, spinDeg);
            p.Twist += spinDeg;
            p.Left = SecondHand(p.Right, p.BladeScale);
            return p;
        }

        /// <summary>
        /// 처형(0.40초, 판정 0.16): ③을 줄인 내려 쪼개기. 0.07초에 몸 앞·오른쪽에서 칼을 카메라 쪽으로 세우고(기울기 85), 0.16초에 앞으로 눕혀 바닥에(0),
        /// 그 뒤 몸 눌림. 칼 배율 없음(3차, 칼 그림은 늘 제 크기). 머리 위 Slam을 쓰지 않는다.
        /// </summary>
        public static StancePose Execution(float t, float duration, float hit)
        {
            float bt = StanceMath.ToBaseTime(t, hit >= 0f ? hit : ExecutionHit, duration > 0f ? duration : ExecutionDuration, ExecutionHit, ExecutionDuration);
            bt = bt < 0f ? 0f : bt > ExecutionDuration ? ExecutionDuration : bt;
            return StanceKeys.Eval(StanceKeyTables.GreatExecution, bt, W);
        }

        /// <summary>구르기: 쉬는 자세를 몸과 같이 돌림(줄임 1.0). 비틀기 = 회전.</summary>
        public static StancePose Dodge(float spinDeg)
        {
            var p = Rest;
            p.Right = StanceMath.Spun(p.Right, spinDeg);
            p.Left = SecondHand(p.Right);
            p.Twist = spinDeg;
            return p;
        }

        /// <summary>맞음: 쉬는 자세에서 날 −10k°, 손도 몸과 같이 밀림(0-3의 13), 몸 1 − 0.04k.</summary>
        public static StancePose Hurt(float k, float pushX, float pushY)
        {
            k = StanceMath.Clamp01(k);
            var p = Rest;
            p.Right.X += pushX;
            p.Right.Y += pushY;
            p.Right.Angle -= 10f * k;
            p.Left = SecondHand(p.Right);
            p.LeanX = pushX;
            p.LeanY = pushY;
            p.ScaleX = p.ScaleY = 1f - 0.04f * k;
            return p;
        }

        /// <summary>쓰러짐: 칼을 오른쪽 바닥에 눕혀 떨어뜨림(기울기 0, 리그가 몸 밑에 그림).</summary>
        public static StancePose Down(float f)
        {
            var p = StancePose.Of(HandPose.At(0.06f, -0.6f, -110f), default);
            p.Left = SecondHand(p.Right);
            return p;
        }

        // ── 되돌아가기(spec 검사_요약 '대검_규칙', WeaponStanceLook.ReturnToRest) ──

        /// <summary>대검 되돌아가기 길이(첫 기울기 → 바깥 → 오른 허리 옆 → 쉬는 자세).</summary>
        public const float ReturnTime = 0.25f;
        /// <summary>손이 몸 앞·왼쪽(몸 틀 y &gt; −0.2)에 있고 칼끝이 수평 아래면 칼끝을 이 기울기까지 땅 쪽으로 떨군다.</summary>
        public const float ReturnDropTilt = -50f;
        /// <summary>칼끝이 이 기울기보다 이미 내려가 있으면 그대로 둔다.</summary>
        public const float ReturnKeepTilt = -15f;
        /// <summary>그 밖(칼이 수평 위·카메라 쪽)은 이 기울기로(자루가 짧아 보여 몸·머리 위를 지나지 않음).</summary>
        public const float ReturnUpTilt = 40f;
        /// <summary>몸 앞·왼쪽 판단 기준(몸 틀 손 y).</summary>
        public const float ReturnFrontY = -0.2f;
        /// <summary>되돌아가기 바깥 자리(리그 틀, 비틀기 0): (0.42, −0.54) −65°, 기울기는 첫 기울기 그대로.</summary>
        const float ReturnOutX = 0.42f, ReturnOutY = -0.54f, ReturnOutAngle = -65f;
        /// <summary>되돌아가기 오른 허리 옆 자세(Leave: (0.24, −0.58) −88°, 기울기 −54, 리그 틀, 비틀기 0).</summary>
        static readonly StanceKey ReturnCarry = new StanceKey(0f, StanceCurve.Smooth, HandPose.Tilted(0.24f, -0.58f, -88f, -54f), default, 0f, 0f, 0f, 1f, 1f, 1f);

        /// <summary>
        /// 대검 되돌아가기 u(0~1, 0.25초, spec 3차 '대검_규칙'): 0 ~ 0.35 손은 그대로 첫 기울기로(EaseOut) → ~ 0.70 바깥 (0.42, −0.54) −65°로 →
        /// ~ 0.88 오른 허리 옆 (0.24, −0.58) −88° 기울기 −54로 → 1 쉬는 자세(Smooth). 첫 기울기: 손이 몸 앞·왼쪽이고 칼끝이 수평 아래면 −50(땅 쪽),
        /// 이미 −15보다 내려가 있으면 그대로, 그 밖은 40°. 곧게 섞으면 넓은 날의 자루 머리가 어깨받이·투구를 지난다(spec 대검(곧게) 미달).
        /// 비틀기는 from·to 표기 그대로 둔다(부르는 쪽이 맞춤). 칼 길이는 늘 1.
        /// </summary>
        public static StancePose ReturnPath(in StancePose from, in StancePose to, float u)
        {
            var a = StanceKey.FromPose(from, W);
            var body = StanceKeys.Rel(a.Right, a.Twist, a.Lean);
            float tl = a.Right.Tilt;
            float t1 = body.Y > ReturnFrontY && tl <= 0f ? Math.Min(tl, ReturnDropTilt) : tl <= ReturnKeepTilt ? tl : ReturnUpTilt;
            var r1 = a.Right;
            r1.Tilt = t1;
            var first = new StanceKey(0f, StanceCurve.Smooth, r1, a.Left, 0f, a.Twist, a.Lean, a.ScaleX, a.ScaleY, a.Blade, a.Height);
            var rest = StanceKey.FromPose(to, W);
            // 가운데 자세는 비틀기 0(리그 틀)이라 from의 큰 비틀기(회오리 뒤)와 섞을 때 가까운 쪽 표기로 옮긴다.
            float tw = a.Twist - StanceMath.WrapDeg(a.Twist);
            var outK = Shift(new StanceKey(0f, StanceCurve.Smooth, HandPose.Tilted(ReturnOutX, ReturnOutY, ReturnOutAngle, t1), default, 0f, 0f, 0f, 1f, 1f, 1f), tw);
            var carry = Shift(ReturnCarry, tw);
            var restK = Shift(rest, tw + StanceMath.WrapDeg(rest.Twist) - rest.Twist);
            if (u < 0.35f) return StanceKeys.Interp(a, first, StanceMath.EaseOut(u / 0.35f), W);
            if (u < 0.70f) return StanceKeys.Interp(first, outK, StanceMath.Smooth((u - 0.35f) / 0.35f), W);
            if (u < 0.88f) return StanceKeys.Interp(outK, carry, StanceMath.Smooth((u - 0.70f) / 0.18f), W);
            return StanceKeys.Interp(carry, restK, StanceMath.Smooth((u - 0.88f) / 0.12f), W);
        }

        /// <summary>키를 비틀기 표기만 shift(360의 배수)만큼 옮긴다(몸 틀 값은 같음).</summary>
        static StanceKey Shift(in StanceKey k, float shift)
        {
            if (Math.Abs(shift) < 1e-4f) return k;
            var r = StanceMath.Spun(k.Right, shift);
            r.X = k.Right.X; r.Y = k.Right.Y;
            return new StanceKey(k.T, k.Curve, r, k.Left, k.Flip, k.Twist + shift, k.Lean, k.ScaleX, k.ScaleY, k.Blade, k.Height);
        }
    }
}
