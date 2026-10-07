namespace Demo6.Core.Combat.Stance
{
    /// <summary>모션을 새로 만든 세 무기(기획/세-무기-우클릭-소켓-1차.md 1장). 나머지 6종은 None(지금 TopDownSwing 그대로).</summary>
    public enum StanceWeapon
    {
        None,
        /// <summary>한손검과 방패(wpn_longsword).</summary>
        SwordShield,
        /// <summary>대검(wpn_greatsword).</summary>
        Greatsword,
        /// <summary>쌍검(wpn_twinblades).</summary>
        Twinblades,
    }

    /// <summary>
    /// 무기 행동 자세 입력(PlayerController 읽기 값을 그대로 옮김). Phase·ActTime(행동 시작부터, 한 행동 안에서 줄지 않음)·PhaseTime(이 단계 시작부터).
    /// ChargeLevel = 기 모으기 단계(0~3), ReleaseLevel = 놓아 베기 단계(1~3, 아니면 0), HeldTime = 대검 모은 시간(떨림·빛), BossRecoil = 막기 반동이 보스 타였나,
    /// FlinchKind = 끊긴 행동(Flinch 단계에서 대검이면 3-8 끊김 자세, 아니면 맞음 자세).
    /// </summary>
    public struct ActStance
    {
        public WeaponActPhase Phase;
        public float ActTime;
        public float PhaseTime;
        public int ChargeLevel;
        public int ReleaseLevel;
        public float HeldTime;
        public bool BossRecoil;
        public WeaponActKind FlinchKind;
    }

    /// <summary>
    /// 세 무기 자세 한 입구(Core, 할당 없음). Game 쪽 WeaponStanceLook이 이것만 부른다. 무기마다 몸은 SwordShieldPoses·GreatswordPoses·TwinbladePoses.
    /// 키는 spec.json 3차(2026-10-05, 기획/참고-모션)이고 한손검·대검은 StanceKeyTables(설계 키 그대로), 쌍검은 설계 식을 옮겼다.
    /// 3차: 세 무기 모두 칼 그림 길이·크기 배율 변화가 없다(Length·Size·BladeScale 늘 1, Flip 0). 칼이 서거나 칼끝이 땅을 향하는 것은 HandPose.Tilt(기울기).
    /// 시간 인자: t = 그 자세 시작부터(초), duration·hit = 공격 속도가 반영된 실제 길이·판정 순간(콤보), 설계 키는 StanceMath.ToBaseTime으로 옮겨 읽는다.
    /// spinDeg = 몸 회전(회오리·구르기, 리그가 계산), 회오리 t = 시작부터 시간(팔 벌림 키), k = 맞음 세기(1 → 0), pushX·pushY = 몸이 밀린 만큼(몸 틀), f = 쓰러짐 진행(0~1).
    /// 회오리·구르기는 몸 틀 자세를 회전만큼 돌린 리그 틀 값과 비틀기 = 회전을 돌려준다(WeaponKeepOut이 다시 몸 틀로 옮겨 잼).
    /// 맞음은 손·몸을 밀린 만큼 같이 옮기고 LeanX·LeanY에 밀림을 넣는다. 처형도 칼 배율 1(3차, 2차의 1.25배는 없앰).
    /// 규칙 ② 검사는 StanceKeepOutTests, 판정 맞춤·쉬는 자세 시작·끝은 StanceHitAlignTests가 지킨다.
    /// 동작과 판정은 따로다(0-1의 10, 2026-10-05 사용자 원문): 판정 맞춤은 '때'(판정 순간에 칼이 앞을 지나거나 가장 멀리 뻗음)만 맞추고,
    /// 칼 궤적이 판정 범위(부채꼴 각·사거리·직선 길이)를 덮게 만들지 않는다. 판정 수치는 WeaponPresets·GreatswordCharge·TwinFlurry가 정하고,
    /// 여기 동작은 멋과 머리 위 금지·몸 관통 금지(규칙 ②)를 먼저 지킨다. 그래서 칼끝보다 판정 사거리가 길어도 맞다.
    /// </summary>
    public static class WeaponStances
    {
        public static StanceWeapon Of(string weaponId)
        {
            switch (weaponId)
            {
                case WeaponActRules.SwordShieldId: return StanceWeapon.SwordShield;
                case WeaponActRules.GreatswordId: return StanceWeapon.Greatsword;
                case WeaponActRules.TwinbladesId: return StanceWeapon.Twinblades;
                default: return StanceWeapon.None;
            }
        }

        /// <summary>쉬는 자세(대기·걷기 바탕, TopDownWeaponLook 쉬는 손 값의 출처). None이면 기본값.</summary>
        public static StancePose Rest(StanceWeapon w)
        {
            switch (w)
            {
                case StanceWeapon.SwordShield: return SwordShieldPoses.Rest;
                case StanceWeapon.Greatsword: return GreatswordPoses.Rest;
                case StanceWeapon.Twinblades: return TwinbladePoses.Rest;
                default: return StancePose.Of(HandPose.At(0.18f, -0.34f, -28f), HandPose.At(0.18f, 0.34f, 28f));
            }
        }

        /// <summary>왼쪽 콤보 step(0부터) 자세.</summary>
        public static StancePose Combo(StanceWeapon w, int step, float t, float duration, float hit)
        {
            switch (w)
            {
                case StanceWeapon.SwordShield: return SwordShieldPoses.Combo(step, t, duration, hit);
                case StanceWeapon.Greatsword: return GreatswordPoses.Combo(step, t, duration, hit);
                case StanceWeapon.Twinblades: return TwinbladePoses.Combo(step, t, duration, hit);
                default: return Rest(w);
            }
        }

        /// <summary>오른쪽 클릭 무기 행동 자세(막기·반동·밀쳐 내기·깨짐 / 기 모으기·놓아 베기 / 난사 / 끊김).</summary>
        public static StancePose Act(StanceWeapon w, in ActStance act)
        {
            switch (w)
            {
                case StanceWeapon.SwordShield: return SwordShieldPoses.Act(act);
                case StanceWeapon.Greatsword: return GreatswordPoses.Act(act);
                case StanceWeapon.Twinblades: return TwinbladePoses.Act(act);
                default: return Rest(w);
            }
        }

        /// <summary>한손검 패링 반격 베기(막기 자세에서 ③ 마무리 베기 키로, 찌르기 없음). 다른 무기는 Combo와 같다.</summary>
        public static StancePose Riposte(StanceWeapon w, float t, float duration, float hit) =>
            w == StanceWeapon.SwordShield ? SwordShieldPoses.Riposte(t, duration, hit) : Combo(w, 2, t, duration, hit);

        /// <summary>검풍(Q, 0.25초).</summary>
        public static StancePose Wave(StanceWeapon w, float t, float duration)
        {
            switch (w)
            {
                case StanceWeapon.SwordShield: return SwordShieldPoses.Wave(t, duration);
                case StanceWeapon.Greatsword: return GreatswordPoses.Wave(t, duration);
                case StanceWeapon.Twinblades: return TwinbladePoses.Wave(t, duration);
                default: return Rest(w);
            }
        }

        /// <summary>회오리(E) 도형 판: 몸 회전 spinDeg, 회오리 시작부터 t초(팔 벌림 키: 한손검 0.06, 대검 0.08, 쌍검 0.12초).</summary>
        public static StancePose Whirl(StanceWeapon w, float spinDeg, float t)
        {
            switch (w)
            {
                case StanceWeapon.SwordShield: return SwordShieldPoses.Whirl(spinDeg, t);
                case StanceWeapon.Greatsword: return GreatswordPoses.Whirl(spinDeg, t);
                case StanceWeapon.Twinblades: return TwinbladePoses.Whirl(spinDeg, t);
                default: return Rest(w);
            }
        }

        /// <summary>회오리 팔 벌림이 끝나는 시각(그 뒤는 몸 틀 자세 그대로 몸과 같이 돎).</summary>
        public static float WhirlOpenTime(StanceWeapon w) =>
            w == StanceWeapon.Greatsword ? GreatswordPoses.WhirlOpenTime : w == StanceWeapon.Twinblades ? TwinbladePoses.WhirlOpenTime : SwordShieldPoses.WhirlOpenTime;

        // ── 쉬는 자세로 되돌아가기(spec 검사_요약 '되돌아가기', WeaponStanceLook.ReturnToRest) ──

        /// <summary>한손검·쌍검 되돌아가기 길이(몸 틀 Smooth 곧게 섞기).</summary>
        public const float ReturnTime = 0.2f;

        /// <summary>
        /// 대검 되돌아가기가 칼끝 땅 경유 길을 쓰는가: 끝난 자세가 이미 쉬는 자세 가까이(손 0.06 안, 몸 틀 날 12° 안, 기울기 10° 안)면 곧게 섞는다
        /// (콤보 끝 자세는 쉬는 자세와 거의 같아 칼끝을 다시 떨구면 덜컹거림). 그 밖(끊긴 행동·회오리·검풍 뒤)은 경유 길.
        /// </summary>
        public static bool ReturnViaGround(StanceWeapon w, in StancePose from, in StancePose to)
        {
            if (w != StanceWeapon.Greatsword) return false;
            var a = StanceKeys.Rel(from.Right, from.Twist, from.LeanX);
            var b = StanceKeys.Rel(to.Right, to.Twist, to.LeanX);
            float dx = a.X - b.X, dy = a.Y - b.Y;
            if (dx * dx + dy * dy > 0.06f * 0.06f) return true;
            if (System.Math.Abs(StanceMath.WrapDeg(a.Angle - b.Angle)) > 12f) return true;
            return System.Math.Abs(a.Tilt - b.Tilt) > 10f;
        }

        /// <summary>되돌아가기 길이(초): 대검 경유 길 0.25, 그 밖 0.2.</summary>
        public static float ReturnDuration(StanceWeapon w, in StancePose from, in StancePose to) =>
            ReturnViaGround(w, from, to) ? GreatswordPoses.ReturnTime : ReturnTime;

        /// <summary>
        /// 회오리 뒤 되돌아가기(u = 0~1, ReturnTime 0.2초): 팔 벌림 키를 거꾸로 거둔다(openT = 끝난 순간의 회오리 시간, 벌림 키 길이로 자름).
        /// 몸 틀 자세는 벌림 키 그대로(규칙 ② 검사를 지난 자세)이고 비틀기만 끝난 회전에서 쉬는 자세 비틀기로 가까운 쪽으로 푼다.
        /// 곧게 섞으면 팔을 편 자세(칼 −90°)에서 쉬는 자세로 오며 자루가 어깨받이를 지난다(한손검 −0.031, 쌍검 −0.040).
        /// </summary>
        public static StancePose ReturnFromWhirl(StanceWeapon w, float openT, float fromTwist, in StancePose to, float u)
        {
            float k = StanceMath.Smooth(StanceMath.Clamp01(u));
            float open = w == StanceWeapon.SwordShield ? SwordShieldPoses.WhirlSpreadTime : WhirlOpenTime(w);
            float s = (openT < open ? (openT > 0f ? openT : 0f) : open) * (1f - k);
            float tw = fromTwist + StanceMath.WrapDeg(to.Twist - fromTwist) * k;
            return Whirl(w, tw, s);
        }

        /// <summary>
        /// 되돌아가기 자세(u = 0~1): 한손검·쌍검(과 쉬는 자세 가까이에서 끝난 대검)은 몸 틀에서 Smooth로 곧게 섞고(spec '되돌아가기' 한손검·쌍검 통과),
        /// 대검은 칼끝을 땅으로 떨군 뒤 오른 허리 앞을 거쳐 쉬는 자세로(GreatswordPoses.ReturnPath, 곧게 섞으면 자루가 투구·몸을 지남).
        /// </summary>
        public static StancePose ReturnPose(StanceWeapon w, in StancePose from, in StancePose to, float u)
        {
            u = StanceMath.Clamp01(u);
            if (ReturnViaGround(w, from, to)) return GreatswordPoses.ReturnPath(from, to, u);
            return StanceKeys.Blend(from, to, StanceMath.Smooth(u), w);
        }

        /// <summary>처형(0.40초, 판정 0.16, 칼 배율 1). 머리 위 내려찍기(Slam)를 쓰지 않는다.</summary>
        public static StancePose Execution(StanceWeapon w, float t, float duration, float hit)
        {
            switch (w)
            {
                case StanceWeapon.SwordShield: return SwordShieldPoses.Execution(t, duration, hit);
                case StanceWeapon.Greatsword: return GreatswordPoses.Execution(t, duration, hit);
                case StanceWeapon.Twinblades: return TwinbladePoses.Execution(t, duration, hit);
                default: return Rest(w);
            }
        }

        /// <summary>구르기: 쉬는 자세를 몸과 같이 돌림(줄임 1.0).</summary>
        public static StancePose Dodge(StanceWeapon w, float spinDeg)
        {
            switch (w)
            {
                case StanceWeapon.SwordShield: return SwordShieldPoses.Dodge(spinDeg);
                case StanceWeapon.Greatsword: return GreatswordPoses.Dodge(spinDeg);
                case StanceWeapon.Twinblades: return TwinbladePoses.Dodge(spinDeg);
                default: return Rest(w);
            }
        }

        /// <summary>맞음: 쉬는 자세 ±10°, 두 손을 몸이 밀린 만큼(pushX·pushY) 같이 옮긴다(0-3의 13).</summary>
        public static StancePose Hurt(StanceWeapon w, float k, float pushX, float pushY)
        {
            switch (w)
            {
                case StanceWeapon.SwordShield: return SwordShieldPoses.Hurt(k, pushX, pushY);
                case StanceWeapon.Greatsword: return GreatswordPoses.Hurt(k, pushX, pushY);
                case StanceWeapon.Twinblades: return TwinbladePoses.Hurt(k, pushX, pushY);
                default: return Rest(w);
            }
        }

        /// <summary>쓰러짐(무기를 옆 바닥에 떨어뜨림, 방패는 눕힘).</summary>
        public static StancePose Down(StanceWeapon w, float f)
        {
            switch (w)
            {
                case StanceWeapon.SwordShield: return SwordShieldPoses.Down(f);
                case StanceWeapon.Greatsword: return GreatswordPoses.Down(f);
                case StanceWeapon.Twinblades: return TwinbladePoses.Down(f);
                default: return Rest(w);
            }
        }
    }
}
