using Demo6.Core.Combat;
using Demo6.Core.Combat.Stance;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 세 무기(한손검과 방패·대검·쌍검) 겉모습 한 입구(기획/세-무기-우클릭-소켓-1차.md 0-3의 3, 7-2, 키는 기획/참고-모션 spec.json 3차 2026-10-05).
    /// TopDownPlayerRig.Drive가 처형 블록 뒤(_flip = extra.Flip 다음, BlendActionStart 앞)에서 한 번 부른다. 세 무기가 아니면 false를 돌려주고 아무것도 바꾸지 않는다.
    /// 세 무기면 Core 자세(WeaponStances)를 TopDownHand로 옮겨 걷기·콤보·무기 행동·검풍·회오리·처형·구르기·맞음·쓰러짐을 덮어쓴다.
    /// 대기·걷기는 리그가 넣은 쉬는 값(TopDownWeaponLook 쉬는 손 = Core 쉬는 자세)·걷기 흔들림을 두고, 대검·쌍검은 손 앞뒤 흔들림(대검 ±0.02 두 손 같이,
    /// 쌍검 ±0.03 좌우 반대, 비틀기 ÷ 6 = 걸음 위상)을 더한다. 3차(2026-10-05): 칼 그림은 늘이거나 줄이지 않는다(세움 0, 칼 배율 1).
    /// 칼 기울기(Tilt)·대검 손 높이(Height)·대검 내려찍기 닿은 횟수(ImpactCount)는 TopDownHand에 칸이 없어 이 클래스의 값으로 WeaponStanceArms에 넘긴다.
    /// 맞음은 두 손을 몸이 밀린 만큼 옮긴다(리그 offset이 곧 밀린 만큼). direct는 대기·이동·맞음·쓰러짐(과 맞음 자세로 그리는 끊김)을 뺀 자세에서 true.
    /// 반환값 = 이 프레임 빛 줄을 자세가 정하는가(검풍, 대검 기 모으기·놓아 베기·끊김, 그 밖에 빛이 있는 자세). true면 glow가 그 값이고,
    /// false면 리그가 지금처럼 빛을 0.15초에 걸쳐 끈다(검풍이 끝난 뒤 꺼짐 보존). 두 마디 팔·칼 그림 배율 1·대검 기울기 그림·둘째 주먹·칼 그림자·
    /// 바닥 효과는 리그가 놓은 뒤 WeaponStanceArms가 고쳐 그린다(여기서 플레이어에 한 번 붙임). TopDownSwing.cs는 고치지 않는다. 매 프레임 할당 없음.
    /// 귀여운 판(CuteHeroRigV15, 한손검만, 다른 세션 몫)은 오른손 길이·크기를 손 그림 늘림으로 쓰므로 길이·크기를 1로 둔다(세운 칼은 귀여운 판에서는 안 보임).
    /// </summary>
    public static class WeaponStanceLook
    {
        /// <summary>맞음 밀림(리그 Hurt와 같은 값): 0.2초에 걸쳐 0.06 → 0.</summary>
        const float HurtTime = 0.2f;
        const float HurtPush = 0.06f;

        /// <summary>이 프레임 자세의 칼 배율. 3차부터 세 무기는 처형에서도 늘 1(WeaponStanceArms가 리그의 처형 1.25배를 지움).</summary>
        public static float BladeScale { get; private set; } = 1f;
        /// <summary>이 프레임 오른손 칼 기울기(도, + 칼끝 카메라 쪽, − 땅 쪽). 쌍검 왼칼·한손검은 0.</summary>
        public static float Tilt { get; private set; }
        /// <summary>기울기를 곧바로 쓰는가(행동 자세). false면 WeaponStanceArms가 리그 따라가기처럼 부드럽게 따라간다.</summary>
        public static bool TiltDirect { get; private set; }
        /// <summary>대검 손 높이(칼 그림자, 0이면 그림자 없음).</summary>
        public static float Height { get; private set; }
        /// <summary>대검 세로 내려찍기(③ 내려 쪼개기·처형)가 바닥에 닿은 횟수(판정 순간을 지날 때 1씩). WeaponStanceArms가 바뀌면 바닥 효과를 낸다.</summary>
        public static int ImpactCount { get; private set; }

        static float s_impactPrevT = -1f;
        static bool s_slamThisFrame;

        static void Record(in StancePose s, bool direct)
        {
            Tilt = s.Right.Tilt;
            Height = s.Height;
            TiltDirect = direct;
        }

        /// <summary>대검 세로 내려찍기 자세: 판정 순간(h)을 지나면 닿은 횟수 +1(새 행동이면 t가 줄어 다시 셈).</summary>
        static void Slam(float t, float h)
        {
            s_slamThisFrame = true;
            if (h > 0f && s_impactPrevT >= 0f && s_impactPrevT < h && t >= h) ImpactCount++;
            s_impactPrevT = t;
        }

        /// <param name="p">플레이어(ActPhase·ActTime·ChargeLevel·RiposteActive 등을 읽음).</param>
        /// <param name="look">지금 무기 겉모습(Id로 무기를 가림, 쉬는 손 값).</param>
        /// <param name="pose">PlayerController.Pose.</param>
        /// <param name="execution">처형 자세가 켜졌나(리그가 처형 시간으로 t·h를 바꿔 넘김).</param>
        /// <param name="cute">귀여운 원화 시험판이 켜졌나(회오리는 귀여운 판 몫이라 방패 손만 몸과 같이 돌림).</param>
        /// <param name="t">자세 시간(PoseTime 또는 처형 시간).</param>
        /// <param name="d">자세 길이(PoseDuration. 처형이면 ExecutionPoseDuration을 다시 읽음).</param>
        /// <param name="h">판정 순간(PoseHitTime 또는 처형 판정, 없으면 −1).</param>
        /// <returns>이 프레임 빛 줄을 자세가 정하면 true(glow가 그 값). 세 무기가 아니면 늘 false이고 ref 값을 바꾸지 않는다.</returns>
        public static bool Override(PlayerController p, TopDownWeaponLook look, PlayerPose pose, bool execution, bool cute, float t, float d, float h,
            ref TopDownHand right, ref TopDownHand left, ref float twist, ref Vector2 offset, ref Vector2 scale, ref float flip, ref bool direct, ref float glow)
        {
            BladeScale = 1f;
            s_slamThisFrame = false;
            bool owns = PoseOf(p, look, pose, execution, cute, t, d, h, ref right, ref left, ref twist, ref offset, ref scale, ref flip, ref direct, ref glow);
            if (!s_slamThisFrame) s_impactPrevT = -1f;
            ReturnToRest(p, look, pose, execution, cute, t, ref right, ref left, ref twist, ref offset, ref scale, ref flip, ref direct);
            if (look != null && !ReferenceEquals(p, null) && WeaponStances.Of(look.Id) != StanceWeapon.None)
            {
                if (cute)
                {
                    // 귀여운 판은 오른손 길이·크기를 팔·칼 그림 늘림으로 쓴다(세운 칼 0.62면 팔이 가로로 찌그러짐). 각만 쓴다.
                    right.Length = 1f;
                    right.Size = 1f;
                }
                WeaponStanceArms.Ensure(p);
            }
            return owns;
        }

        // ── 쉬는 자세로 돌아가기(spec 검사_요약 '되돌아가기') ──
        // 행동 자세(direct)가 끝나고 대기·걷기로 넘어갈 때 리그 따라가기(손 자리 곧게 섞기·각 가까운 쪽)는 회오리 끝(몸 −90° 비틂, 손 몸과 같이 돎)이나
        // 대검 검풍 끝(칼끝 앞)에서 쉬는 자세로 갈 때 칼날·자루가 몸·머리를 가로지른다. 그래서 세 무기만 이 시간을 직접 그린다:
        // 한손검·쌍검은 몸 틀(비틀기를 뺀 틀)에서 Smooth로 곧게 0.2초(비틀기는 가까운 쪽), 대검은 첫 기울기(앞·왼쪽 누운 칼은 칼끝을 땅 쪽, 선 칼은 40°)
        // → 바깥 (0.42, −0.54) → 오른 허리 옆을 거쳐 0.25초
        // (끝난 자세가 이미 쉬는 자세 가까이면 곧게, WeaponStances.ReturnPose). 회오리 뒤는 팔 벌림 키를 거꾸로 0.2초(WeaponStances.ReturnFromWhirl).
        // 끝나면 리그 따라가기로 넘긴다. 귀여운 판(다른 세션 몫)은 건드리지 않는다.

        static PlayerController s_owner;
        static string s_lookId;
        static bool s_lastAction;
        static StancePose s_last;
        static float s_returnStart = -1f;
        static float s_returnTime = WeaponStances.ReturnTime;
        static StancePose s_from;
        static bool s_lastWhirl, s_fromWhirl;
        static float s_lastWhirlT, s_fromWhirlT;

        static void ReturnToRest(PlayerController p, TopDownWeaponLook look, PlayerPose pose, bool execution, bool cute, float t,
            ref TopDownHand right, ref TopDownHand left, ref float twist, ref Vector2 offset, ref Vector2 scale, ref float flip, ref bool direct)
        {
            var w = look != null ? WeaponStances.Of(look.Id) : StanceWeapon.None;
            if (ReferenceEquals(p, null) || w == StanceWeapon.None)
            {
                s_lastAction = false;
                s_returnStart = -1f;
                return;
            }
            if (!ReferenceEquals(p, s_owner) || look.Id != s_lookId)
            {
                s_owner = p;
                s_lookId = look.Id;
                s_lastAction = false;
                s_returnStart = -1f;
            }
            bool shield = w == StanceWeapon.SwordShield;
            bool rest = !execution && (pose == PlayerPose.Idle || pose == PlayerPose.Move);
            if (rest && !cute)
            {
                var to = ToPose(right, left, twist, offset, scale, flip, shield);
                if (s_lastAction)
                {
                    s_returnStart = Time.time;
                    s_from = s_last;
                    s_fromWhirl = s_lastWhirl;
                    s_fromWhirlT = s_lastWhirlT;
                    s_returnTime = s_fromWhirl ? WeaponStances.ReturnTime : WeaponStances.ReturnDuration(w, s_from, to);
                }
                if (s_returnStart >= 0f)
                {
                    float u = (Time.time - s_returnStart) / s_returnTime;
                    if (u >= 1f) s_returnStart = -1f;
                    else
                    {
                        var r = s_fromWhirl
                            ? WeaponStances.ReturnFromWhirl(w, s_fromWhirlT, s_from.Twist, to, u)
                            : WeaponStances.ReturnPose(w, s_from, to, u);
                        right = ToHand(r.Right);
                        left = shield ? ToShieldHand(r) : ToHand(r.Left);
                        twist = r.Twist;
                        offset = new Vector2(r.LeanX, r.LeanY);
                        scale = new Vector2(r.ScaleX, r.ScaleY);
                        flip = r.Flip;
                        direct = true;
                        Record(r, true);
                    }
                }
            }
            else s_returnStart = -1f;
            // 이 프레임에 그릴 값(리그가 direct면 그대로 씀)을 적어 둔다. 대기·걷기·맞음·쓰러짐(따라가기)은 행동이 아니다.
            s_lastAction = !rest && direct && !cute;
            s_lastWhirl = s_lastAction && !execution && pose == PlayerPose.Whirl;
            s_lastWhirlT = t;
            s_last = ToPose(right, left, twist, offset, scale, flip, shield);
        }

        /// <summary>리그 값 → Core 자세(방패 손은 Pos·Angle = 방패각·Length = 방패깊이).</summary>
        static StancePose ToPose(in TopDownHand right, in TopDownHand left, float twist, Vector2 offset, Vector2 scale, float flip, bool shield)
        {
            var s = StancePose.Of(FromHand(right), FromHand(left));
            if (shield)
            {
                s.ShieldAngle = left.Angle;
                s.ShieldDepth = left.Length;
                s.Left.Size = 1f;
            }
            s.Twist = twist;
            s.LeanX = offset.x;
            s.LeanY = offset.y;
            s.ScaleX = scale.x;
            s.ScaleY = scale.y;
            s.Flip = flip;
            // 리그 손에는 기울기·높이 칸이 없어 이 프레임에 적어 둔 값을 붙인다.
            s.Right.Tilt = Tilt;
            s.Height = Height;
            return s;
        }

        static HandPose FromHand(in TopDownHand h) => HandPose.At(h.Pos.x, h.Pos.y, h.Angle, h.Length, h.Size);

        static bool PoseOf(PlayerController p, TopDownWeaponLook look, PlayerPose pose, bool execution, bool cute, float t, float d, float h,
            ref TopDownHand right, ref TopDownHand left, ref float twist, ref Vector2 offset, ref Vector2 scale, ref float flip, ref bool direct, ref float glow)
        {
            if (look == null || ReferenceEquals(p, null)) return false;
            var w = WeaponStances.Of(look.Id);
            if (w == StanceWeapon.None) return false;
            bool shield = w == StanceWeapon.SwordShield;
            StancePose s;

            if (execution)
            {
                float ed = p.ExecutionPoseDuration;
                if (ed <= 0f) ed = ExecutionRule.PoseSeconds;
                s = WeaponStances.Execution(w, t, ed, h);
                if (w == StanceWeapon.Greatsword) Slam(t, h);
                return Direct(s, shield, ref right, ref left, ref twist, ref offset, ref scale, ref flip, ref direct, ref glow, false);
            }

            switch (pose)
            {
                case PlayerPose.Idle:
                case PlayerPose.Move:
                {
                    // 쉬는 손·걷기 흔들림은 리그 값 그대로(TopDownWeaponLook 쉬는 손 = Core 쉬는 자세). 3차: 세움 없음.
                    // 대검·쌍검은 손 앞뒤 흔들림을 더한다: 리그 걷기 비틀기 = 6 × sin(걸음) × 걸음 세기라 비틀기 ÷ 6이 걸음 위상(귀여운 판은 비틀기를 줄이므로 뺌).
                    flip = 0f;
                    if (!cute)
                    {
                        float sway = twist / 6f;
                        if (w == StanceWeapon.Greatsword) right.Pos.x += GreatswordPoses.WalkSway * sway;
                        else if (w == StanceWeapon.Twinblades)
                        {
                            right.Pos.x += TwinbladePoses.WalkSway * sway;
                            left.Pos.x -= TwinbladePoses.WalkSway * sway;
                        }
                    }
                    Record(WeaponStances.Rest(w), false);
                    direct = false;
                    return false;
                }

                case PlayerPose.Attack:
                    s = p.RiposteActive ? WeaponStances.Riposte(w, t, d, h) : WeaponStances.Combo(w, p.ComboIndex, t, d, h);
                    // 대검 ③ 내려 쪼개기: 판정 순간 = 칼이 바닥에 닿는 순간(바닥 효과).
                    if (w == StanceWeapon.Greatsword && !p.RiposteActive && p.ComboIndex >= GreatswordPoses.StepCount - 1) Slam(t, h);
                    return Direct(s, shield, ref right, ref left, ref twist, ref offset, ref scale, ref flip, ref direct, ref glow, false);

                case PlayerPose.WeaponAct:
                {
                    var act = new ActStance
                    {
                        Phase = p.ActPhase,
                        ActTime = p.ActTime,
                        PhaseTime = p.ActPhaseTime,
                        ChargeLevel = p.ChargeLevel,
                        ReleaseLevel = p.ReleaseLevel,
                        HeldTime = p.ChargeHeldTime,
                        BossRecoil = p.GuardRecoilBoss,
                        FlinchKind = p.ActKind,
                    };
                    if (act.Phase == WeaponActPhase.Flinch && w != StanceWeapon.Greatsword)
                    {
                        // 막기·난사 끊김 = 맞음 자세(5-6): 맞은 반대쪽으로 몸과 손을 같이 밀고, 리그 따라가기(가까운 각)로 그린다.
                        float k = Mathf.Clamp01(1f - act.PhaseTime / HurtTime);
                        Vector2 push = PushAway(p, k);
                        s = WeaponStances.Hurt(w, k, push.x, push.y);
                        right = ToHand(s.Right);
                        left = shield ? ToShieldHand(s) : ToHand(s.Left);
                        offset = push;
                        scale = new Vector2(s.ScaleX, s.ScaleY);
                        flip = s.Flip;
                        direct = false;
                        Record(s, false);
                        return false;
                    }
                    s = WeaponStances.Act(w, act);
                    return Direct(s, shield, ref right, ref left, ref twist, ref offset, ref scale, ref flip, ref direct, ref glow,
                        w == StanceWeapon.Greatsword);
                }

                case PlayerPose.WaveCast:
                    s = WeaponStances.Wave(w, t, d > 0f ? d : SwordShieldPoses.WaveDuration);
                    return Direct(s, shield, ref right, ref left, ref twist, ref offset, ref scale, ref flip, ref direct, ref glow, true);

                case PlayerPose.Whirl:
                {
                    if (cute)
                    {
                        // 귀여운 판 회오리(다른 세션 몫): 칼 손·몸은 리그 값 그대로, 방패 손만 몸 비틀기만큼 같이 돌린다.
                        var rest = WeaponStances.Rest(w);
                        if (shield)
                        {
                            rest.Left = StanceMath.Spun(rest.Left, twist);
                            rest.ShieldAngle = rest.Left.Angle;
                            left = ToShieldHand(rest);
                        }
                        Record(rest, false);
                        return false;
                    }
                    // 회전 = 리그가 넣은 비틀기(TopDownSwing.Whirl), 팔 벌림 = 시작부터 t초의 키(무기마다 0.06~0.12초).
                    s = WeaponStances.Whirl(w, twist, t);
                    return Direct(s, shield, ref right, ref left, ref twist, ref offset, ref scale, ref flip, ref direct, ref glow, false);
                }

                case PlayerPose.Dodge:
                {
                    float dd = d > 0f ? d : 0.22f;
                    float spin = 360f * StanceMath.Smooth(t / dd);
                    s = WeaponStances.Dodge(w, spin);
                    // 몸 눌림(구르기 부풀기)은 리그 값 그대로.
                    right = ToHand(s.Right);
                    left = shield ? ToShieldHand(s) : ToHand(s.Left);
                    twist = spin;
                    flip = s.Flip;
                    direct = true;
                    glow = s.Glow;
                    Record(s, true);
                    return false;
                }

                case PlayerPose.Hurt:
                {
                    // 리그가 넣은 밀림(offset)·몸 크기는 그대로, 두 손을 같은 만큼 옮긴다(0-3의 13).
                    float k = Mathf.Clamp01(1f - t / HurtTime);
                    s = WeaponStances.Hurt(w, k, offset.x, offset.y);
                    right = ToHand(s.Right);
                    left = shield ? ToShieldHand(s) : ToHand(s.Left);
                    flip = s.Flip;
                    direct = false;
                    Record(s, false);
                    return false;
                }

                case PlayerPose.Down:
                {
                    // 쓰러짐: 비틀기·밀림·밝기는 리그 값 그대로, 무기만 바닥에 떨어뜨린다(방패는 눕힘, 리그가 몸 밑에 그림).
                    s = WeaponStances.Down(w, StanceMath.Smooth(t / 0.3f));
                    right = ToHand(s.Right);
                    left = shield ? ToShieldHand(s) : ToHand(s.Left);
                    flip = s.Flip;
                    direct = false;
                    Record(s, false);
                    return false;
                }

                default:
                    return false;
            }
        }

        /// <summary>Core 자세 전체를 리그 값으로 옮기고 direct = true. 빛 줄을 자세가 정하면(ownsGlow 또는 빛이 있음) true.</summary>
        static bool Direct(in StancePose s, bool shield, ref TopDownHand right, ref TopDownHand left, ref float twist, ref Vector2 offset, ref Vector2 scale,
            ref float flip, ref bool direct, ref float glow, bool ownsGlow)
        {
            right = ToHand(s.Right);
            left = shield ? ToShieldHand(s) : ToHand(s.Left);
            twist = s.Twist;
            offset = new Vector2(s.LeanX, s.LeanY);
            scale = new Vector2(s.ScaleX, s.ScaleY);
            flip = s.Flip;
            direct = true;
            glow = s.Glow;
            Record(s, true);
            return ownsGlow || s.Glow > 0f;
        }

        /// <summary>맞은 반대쪽으로 몸이 밀린 만큼(몸 틀, 리그 Hurt와 같은 식 0.06k). 맞은 자리를 모르면 뒤로.</summary>
        static Vector2 PushAway(PlayerController p, float k)
        {
            Vector2 facing = p.FacingDirection;
            Vector2 away = (Vector2)p.transform.position - p.LastHitFrom;
            if (away.sqrMagnitude < 0.0001f) away = -facing;
            float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            return TopDownCanvas.Rotate(away.normalized, -angle) * (HurtPush * k);
        }

        /// <summary>Core 손 자세를 리그 손으로(같은 뜻, 몸 틀).</summary>
        public static TopDownHand ToHand(HandPose h) => new TopDownHand { Pos = new Vector2(h.X, h.Y), Angle = h.Angle, Length = h.Length, Size = h.Size };

        /// <summary>
        /// 방패 손: 위치는 Left, 각도 = ShieldAngle, 길이 = ShieldDepth(ShieldPart가 Length를 방패 깊이로 읽음, TopDownWeaponLook RestLeft와 같은 꼴).
        /// </summary>
        public static TopDownHand ToShieldHand(in StancePose s) => new TopDownHand
        {
            Pos = new Vector2(s.Left.X, s.Left.Y),
            Angle = s.ShieldAngle,
            Length = s.ShieldDepth,
            Size = s.Left.Size > 0f ? s.Left.Size : 1f,
        };
    }
}
