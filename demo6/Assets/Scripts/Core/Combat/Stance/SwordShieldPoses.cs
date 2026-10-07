namespace Demo6.Core.Combat.Stance
{
    /// <summary>
    /// 한손검과 방패 자세(2차, 2026-10-05 사용자 원문 "처음 들고있는 자세가 약간 칼을 세우서 들고있어야 하고 찌르넣지말고 베기 3연타로 가자").
    /// 키는 StanceKeyTables(spec.json 2차 설계 키 그대로)이고 StanceKeys.Eval이 몸 틀에서 섞어 읽는다. 검 = Right, 방패 손 = Left(자리, Angle = 방패각,
    /// Length = 방패깊이) + ShieldAngle·ShieldDepth. 쉬는 자세는 가슴 앞 오른쪽에 칼을 앞으로 든다. 3차(2026-10-05 "검들이 다늘어나고 압축되는데 이부분이
    /// 제일어색한것같다"): 칼 길이·크기 배율(쉬는 자세 0.62·1.06, 막기 0.80·1.12, ③ 감기 0.42·1.12, 처형 1.25배)을 없애 칼은 늘 제 크기다.
    /// 콤보 = ① 베기(오른 → 왼) · ② 되베기(왼 → 오른) · ③ 마무리 베기(오른 어깨 앞에 세웠다가 비껴 내려 벰). 찌르기는 없다(반격·처형·검풍도 베기).
    /// 공격 속도는 StanceMath.ToBaseTime으로 설계 시간에 옮겨 읽어 판정 키가 늘 실제 판정 순간에 온다. 할당 없음.
    /// </summary>
    public static class SwordShieldPoses
    {
        const StanceWeapon W = StanceWeapon.SwordShield;

        /// <summary>쉬는 자세: 검 (0.41, −0.32) +6°(제 길이에서 자루 머리가 어깨받이에 닿지 않게 2차 (0.37, −0.30)에서 손만 옮김) / 방패 손 (0.28, 0.38), 방패각 32°, 깊이 0.34.</summary>
        public static readonly StancePose Rest = StanceKeys.FromKey(StanceKeyTables.SwordRest[0], W);

        /// <summary>방패 최소 깊이(곧게 세우면 두께 약 0.08의 판).</summary>
        public const float MinShieldDepth = 0.14f;
        /// <summary>구르기 방패 깊이(줄임 1.0, 몸과 같이 돎).</summary>
        public const float DodgeShieldDepth = 0.2f;

        // ── 콤보 설계 시간(공격 속도 0, WeaponPresets.Longsword와 같아야 함) ──
        public const float Step1Duration = 0.58f;
        public const float Step1Hit = 0.2088f;
        public const float Step2Duration = 0.62f;
        public const float Step2Hit = 0.2356f;
        public const float Step3Duration = 0.92f;
        public const float Step3Hit = 0.3864f;
        /// <summary>단계 수.</summary>
        public const int StepCount = 3;

        /// <summary>처형(0.40초, ③ 감기 0.096, 판정 0.16).</summary>
        public const float ExecutionDuration = 0.40f;
        public const float ExecutionHit = 0.16f;
        /// <summary>검풍(Q) 길이(끝 = 판정).</summary>
        public const float WaveDuration = 0.25f;
        /// <summary>회오리 팔 벌림 키 길이(0.06초에 다 벌리고 0.12초까지 그대로).</summary>
        public const float WhirlOpenTime = 0.12f;
        /// <summary>회오리 팔이 다 벌어지는 시각(그 뒤 0.12초까지 같은 자세).</summary>
        public const float WhirlSpreadTime = 0.06f;
        /// <summary>맞음 밀림 시간(리그 k = 1 − t ÷ 0.2).</summary>
        public const float HurtTime = 0.2f;

        /// <summary>단계(0부터)의 설계 길이·판정.</summary>
        public static void DesignTime(int step, out float duration, out float hit)
        {
            switch (step <= 0 ? 0 : step >= 2 ? 2 : 1)
            {
                case 0: duration = Step1Duration; hit = Step1Hit; break;
                case 1: duration = Step2Duration; hit = Step2Hit; break;
                default: duration = Step3Duration; hit = Step3Hit; break;
            }
        }

        static float BaseTime(float t, float duration, float hit, float baseDuration, float baseHit)
        {
            float bt = StanceMath.ToBaseTime(t, hit, duration, baseHit, baseDuration);
            return bt < 0f ? 0f : bt > baseDuration ? baseDuration : bt;
        }

        /// <summary>콤보 ① 베기 0.58 / ② 되베기 0.62 / ③ 마무리 베기 0.92(설계 길이·판정은 WeaponPresets.Longsword).</summary>
        public static StancePose Combo(int step, float t, float duration, float hit)
        {
            DesignTime(step, out float bd, out float bh);
            float bt = BaseTime(t, duration, hit, bd, bh);
            if (step <= 0) return StanceKeys.Eval(StanceKeyTables.SwordStep1, bt, W);
            return StanceKeys.Eval(step == 1 ? StanceKeyTables.SwordStep2 : StanceKeyTables.SwordStep3, bt, W);
        }

        /// <summary>막기 들기·막기·내리기·반동·밀쳐 내기·깨짐·끊김(맞음 자세).</summary>
        public static StancePose Act(in ActStance act)
        {
            float pt = act.PhaseTime < 0f ? 0f : act.PhaseTime;
            switch (act.Phase)
            {
                case WeaponActPhase.Raise: return StanceKeys.Eval(StanceKeyTables.SwordRaise, pt, W);
                case WeaponActPhase.Hold: return Guard;
                case WeaponActPhase.Lower: return StanceKeys.Eval(StanceKeyTables.SwordLower, pt, W);
                case WeaponActPhase.Recoil: return StanceKeys.Eval(act.BossRecoil ? StanceKeyTables.SwordBossRecoil : StanceKeyTables.SwordRecoil, pt, W);
                case WeaponActPhase.ParryPush: return StanceKeys.Eval(StanceKeyTables.SwordParryPush, pt, W);
                case WeaponActPhase.Break: return StanceKeys.Eval(StanceKeyTables.SwordBreak, pt, W);
                case WeaponActPhase.Flinch: return Hurt(1f - StanceMath.Clamp01(pt / HurtTime), 0f, 0f);
                default: return Rest;
            }
        }

        /// <summary>막기 자세(들기 끝·반동 끝·밀쳐 내기 끝): 검 (0.30, −0.50) −12° | 방패 (0.36, 0.04) 0° 깊이 0.18 | 비틀기 −6 | 앞뒤 +0.02.</summary>
        public static readonly StancePose Guard = StanceKeys.FromKey(StanceKeyTables.SwordGuard[0], W);

        /// <summary>패링 반격 베기(막기 자세에서 ③ 키로, 공격 속도는 ③ 시간으로 옮김). 찌르기 없음.</summary>
        public static StancePose Riposte(float t, float duration, float hit) =>
            StanceKeys.Eval(StanceKeyTables.SwordRiposte, BaseTime(t, duration, hit, Step3Duration, Step3Hit), W);

        /// <summary>검풍(Q, 0.25초): 0.10초에 오른쪽 바깥(−95°)으로 감고 0.25초에 날 0°(짧은 가로 베기). 방패는 쉬는 자세. 빛 줄은 리그와 같은 식.</summary>
        public static StancePose Wave(float t, float duration)
        {
            float bt = duration > 0f ? t * WaveDuration / duration : t;
            var p = StanceKeys.Eval(StanceKeyTables.SwordWave, bt, W);
            p.Glow = StanceMath.Smooth((t - 0.06f) / 0.12f);
            return p;
        }

        /// <summary>
        /// 회오리(E) 도형 판: 시작부터 t초의 팔 벌림(쉬는 자세 → (0.27, −0.55) −40° → 0.06초 (0, −0.68) −90°, 방패 (0, 0.56) 90° 깊이 0.14, 몸 틀)을
        /// 몸 회전 spinDeg만큼 같이 돌린다. 비틀기 = 회전.
        /// </summary>
        public static StancePose Whirl(float spinDeg, float t) => SpinPose(StanceKeys.Eval(StanceKeyTables.SwordWhirl, t < 0f ? 0f : t, W), spinDeg);

        static StancePose SpinPose(StancePose p, float spinDeg)
        {
            p.Right = StanceMath.Spun(p.Right, spinDeg);
            p.Left = StanceMath.Spun(p.Left, spinDeg);
            p.ShieldAngle = p.Left.Angle;
            p.Twist += spinDeg;
            return p;
        }

        /// <summary>
        /// 처형(0.40초, 판정 0.16): 머리 위 Slam 대신 ③ 베기 키. 0.096초 ③ 감기, 0.16초 ③ 판정 키, 그 뒤 몸 눌림 (1.05, 0.94) → 1. 칼 배율 없음(3차).
        /// </summary>
        public static StancePose Execution(float t, float duration, float hit) =>
            StanceKeys.Eval(StanceKeyTables.SwordExecution,
                BaseTime(t, duration > 0f ? duration : ExecutionDuration, hit >= 0f ? hit : ExecutionHit, ExecutionDuration, ExecutionHit), W);

        /// <summary>구르기: 쉬는 자세를 몸과 같이 돌림(줄임 1.0), 방패 깊이 0.2. 비틀기 = 회전.</summary>
        public static StancePose Dodge(float spinDeg)
        {
            var p = Rest;
            p.Left.Length = DodgeShieldDepth;
            p.ShieldDepth = DodgeShieldDepth;
            return SpinPose(p, spinDeg);
        }

        /// <summary>맞음: 쉬는 자세에서 검 −10k°·방패 +10k°, 두 손을 몸이 밀린 만큼 같이 옮김(0-3의 13), 몸 1 − 0.04k.</summary>
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
            p.ShieldAngle = p.Left.Angle;
            p.LeanX = pushX;
            p.LeanY = pushY;
            p.ScaleX = p.ScaleY = 1f - 0.04f * k;
            return p;
        }

        /// <summary>쓰러짐: 검은 오른쪽 바닥에, 방패는 왼쪽 바닥에 눕힘((−0.05, 0.75) 110°, 깊이 1, 리그가 몸 밑에 그림).</summary>
        public static StancePose Down(float f)
        {
            var p = StancePose.Of(HandPose.At(0.06f, -0.6f, -110f), HandPose.At(-0.05f, 0.75f, 110f, 1f));
            p.ShieldAngle = 110f;
            p.ShieldDepth = 1f;
            return p;
        }
    }
}
