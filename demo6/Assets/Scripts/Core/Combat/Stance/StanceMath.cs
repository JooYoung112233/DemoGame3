using System;

namespace Demo6.Core.Combat.Stance
{
    /// <summary>
    /// 손 하나의 자세(몸 틀 기준 월드 유닛, +x = 앞, +y = 왼쪽, 각도는 도). Game 쪽 TopDownHand와 같은 뜻이다(WeaponStanceLook이 옮김).
    /// Length·Size = 그림 배율 칸(3차 2026-10-05부터 세 무기는 늘 1: 칼 그림은 늘이거나 줄이지 않는다, 사용자 원문 "지금보니 검들이 다늘어나고 압축되는데
    /// 이부분이 제일어색한것같다"). Tilt = 칼 기울기(도, + = 칼끝이 카메라 쪽으로 듦, − = 칼끝이 땅 쪽, 0 = 수평으로 누움). 게임은 기울기별 그림을 갈아 끼우고
    /// 검사는 두께 있는 칼을 실제로 돌린 모양(날 방향 × cos 기울기)으로 잰다. 방패 손은 Angle·Length 대신 StancePose.ShieldAngle·ShieldDepth를 쓴다.
    /// </summary>
    public struct HandPose
    {
        public float X;
        public float Y;
        public float Angle;
        public float Length;
        public float Size;
        public float Tilt;

        public static HandPose At(float x, float y, float angle, float length = 1f, float size = 1f) =>
            new HandPose { X = x, Y = y, Angle = angle, Length = length, Size = size };

        /// <summary>제 크기 칼(길이·크기 1)을 기울기 tilt(도)로.</summary>
        public static HandPose Tilted(float x, float y, float angle, float tilt) =>
            new HandPose { X = x, Y = y, Angle = angle, Length = 1f, Size = 1f, Tilt = tilt };

        /// <summary>각도는 감지 않고 곧게 보간한다(2-4 '각도는 감지 않고 곧게'). 기울기도 곧게.</summary>
        public static HandPose Lerp(HandPose a, HandPose b, float t) => new HandPose
        {
            X = a.X + (b.X - a.X) * t,
            Y = a.Y + (b.Y - a.Y) * t,
            Angle = a.Angle + (b.Angle - a.Angle) * t,
            Length = a.Length + (b.Length - a.Length) * t,
            Size = a.Size + (b.Size - a.Size) * t,
            Tilt = a.Tilt + (b.Tilt - a.Tilt) * t,
        };
    }

    /// <summary>
    /// 한 순간의 무기 자세 전체(기획/세-무기-우클릭-소켓-1차.md 7-2). 리그(TopDownPlayerRig)의 손 둘·비틀기·앞뒤·몸 크기·날 세움·빛 줄에 그대로 옮겨진다.
    /// Right = 오른손(한손검 칼, 대검 날밑 쪽 손 — 둘째 손은 리그가 SecondGrip으로 자동, 쌍검 오른칼). Left = 쌍검 왼칼 또는 한손검 방패 손(대검은 쓰지 않음).
    /// ShieldAngle = 방패각(바깥면이 보는 쪽, 도), ShieldDepth = 방패 깊이(0.14 ~ 1, 위에서 본 두께 배율). Flip = 날 세움(리그 _flip, 3차부터 세 무기는 늘 0).
    /// Twist = 몸 비틀기(도), LeanX = 앞뒤(몸 틀 x 이동), ScaleX·ScaleY = 몸 크기, BladeScale = 칼 배율(3차부터 세 무기는 처형에서도 늘 1), Glow = 빛 줄(0~1).
    /// LeanY = 옆 밀림(몸 틀 y 이동, 맞음에서 몸이 옆으로 밀린 만큼, 그 밖에는 0). 꾸러미 ② 자세가 더했다(검사가 몸과 손을 같이 옮겨 잼).
    /// Height = 대검 손 높이(바닥에서, 칼 그림자용. 0이면 그림자 없음). 칼끝 높이 = Height + 칼끝 거리 × sin(Right.Tilt).
    /// </summary>
    public struct StancePose
    {
        public HandPose Right;
        public HandPose Left;
        public float ShieldAngle;
        public float ShieldDepth;
        public float Flip;
        public float Twist;
        public float LeanX;
        public float LeanY;
        public float ScaleX;
        public float ScaleY;
        public float BladeScale;
        public float Glow;
        public float Height;

        /// <summary>손 둘만 정하고 나머지는 기본값(몸 크기 1, 칼 배율 1, 나머지 0).</summary>
        public static StancePose Of(HandPose right, HandPose left) => new StancePose
        {
            Right = right,
            Left = left,
            ScaleX = 1f,
            ScaleY = 1f,
            BladeScale = 1f,
        };

        /// <summary>모든 값을 곧게 보간한다(각도도 감지 않음).</summary>
        public static StancePose Lerp(StancePose a, StancePose b, float t) => new StancePose
        {
            Right = HandPose.Lerp(a.Right, b.Right, t),
            Left = HandPose.Lerp(a.Left, b.Left, t),
            ShieldAngle = a.ShieldAngle + (b.ShieldAngle - a.ShieldAngle) * t,
            ShieldDepth = a.ShieldDepth + (b.ShieldDepth - a.ShieldDepth) * t,
            Flip = a.Flip + (b.Flip - a.Flip) * t,
            Twist = a.Twist + (b.Twist - a.Twist) * t,
            LeanX = a.LeanX + (b.LeanX - a.LeanX) * t,
            LeanY = a.LeanY + (b.LeanY - a.LeanY) * t,
            ScaleX = a.ScaleX + (b.ScaleX - a.ScaleX) * t,
            ScaleY = a.ScaleY + (b.ScaleY - a.ScaleY) * t,
            BladeScale = a.BladeScale + (b.BladeScale - a.BladeScale) * t,
            Glow = a.Glow + (b.Glow - a.Glow) * t,
            Height = a.Height + (b.Height - a.Height) * t,
        };
    }

    /// <summary>자세 계산 공통 식(구간 곡선, 공격 속도 시간 옮기기, 길이 ≥ 0 꼴). 할당 없음.</summary>
    public static class StanceMath
    {
        public static float Clamp01(float t) => t < 0f ? 0f : t > 1f ? 1f : t;

        /// <summary>3t² − 2t³(TopDownSwing.Smooth와 같은 식).</summary>
        public static float Smooth(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>판정으로 들어가는 구간: t².</summary>
        public static float EaseIn(float t)
        {
            t = Clamp01(t);
            return t * t;
        }

        /// <summary>판정에서 나오는 구간: 1 − (1 − t)².</summary>
        public static float EaseOut(float t)
        {
            t = Clamp01(t);
            float u = 1f - t;
            return 1f - u * u;
        }

        /// <summary>
        /// 공격 속도로 바뀐 실제 시각 t(판정 hit, 길이 duration)를 설계 키 시각(판정 baseHit, 길이 baseDuration)으로 되돌린다(2-4 시간 옮기기의 역).
        /// 판정 앞은 t × baseHit ÷ hit, 판정 뒤는 baseHit + (t − hit) × (baseDuration − baseHit) ÷ (duration − hit). 판정 순간은 늘 baseHit로 간다.
        /// 값이 0 이하이거나 맞지 않으면 t를 그대로 돌려준다.
        /// </summary>
        public static float ToBaseTime(float t, float hit, float duration, float baseHit, float baseDuration)
        {
            if (hit <= 0f || duration <= hit || baseHit <= 0f || baseDuration <= baseHit) return t;
            if (t <= hit) return t * baseHit / hit;
            return baseHit + (t - hit) * (baseDuration - baseHit) / (duration - hit);
        }

        /// <summary>길이가 음수면 날 + 180°, 길이 부호를 바꿔 늘 길이 ≥ 0 꼴로 만든다(3-3, 리그 섞기가 칼을 접었다 펴지 않게).</summary>
        public static HandPose NonNegativeLength(HandPose h)
        {
            if (h.Length >= 0f) return h;
            h.Length = -h.Length;
            h.Angle += 180f;
            return h;
        }

        /// <summary>각도를 (−180, 180]으로.</summary>
        public static float WrapDeg(float deg)
        {
            deg %= 360f;
            if (deg > 180f) deg -= 360f;
            else if (deg <= -180f) deg += 360f;
            return deg;
        }

        public static float Cos(float deg) => (float)Math.Cos(deg * Math.PI / 180.0);
        public static float Sin(float deg) => (float)Math.Sin(deg * Math.PI / 180.0);

        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>Smooth의 역(0~1 → 0~1): Smooth(x) = y인 x. 회오리 팔 벌림(open = Smooth(t ÷ 0.06))에서 시간을 되찾을 때 쓴다.</summary>
        public static float InverseSmooth(float y)
        {
            y = Clamp01(y);
            return (float)(0.5 - Math.Sin(Math.Asin(1.0 - 2.0 * y) / 3.0));
        }

        /// <summary>점 (x, y)를 deg만큼 돌린다(반시계).</summary>
        public static void Rotate(float x, float y, float deg, out float rx, out float ry)
        {
            double r = deg * Math.PI / 180.0;
            float c = (float)Math.Cos(r), s = (float)Math.Sin(r);
            rx = x * c - y * s;
            ry = x * s + y * c;
        }

        /// <summary>손 자세를 몸과 함께 deg만큼 돌린다(자리 회전 + 날 각 더함). 회오리·구르기·회전 가르기에서 몸 틀 자세를 리그 틀로 옮긴다.</summary>
        public static HandPose Spun(HandPose h, float deg)
        {
            Rotate(h.X, h.Y, deg, out float x, out float y);
            h.X = x;
            h.Y = y;
            h.Angle += deg;
            return h;
        }
    }

    /// <summary>키 구간 곡선(2-4: 판정으로 들어가는 구간 EaseIn, 판정에서 나오는 구간 EaseOut, 나머지 Smooth, 곧게 = Linear).</summary>
    public enum StanceCurve : byte
    {
        Smooth,
        EaseIn,
        EaseOut,
        Linear,
    }

    /// <summary>구간 곡선 계산(할당 없음).</summary>
    public static class StanceCurves
    {
        public static float Apply(StanceCurve curve, float s)
        {
            switch (curve)
            {
                case StanceCurve.EaseIn: return StanceMath.EaseIn(s);
                case StanceCurve.EaseOut: return StanceMath.EaseOut(s);
                case StanceCurve.Linear: return StanceMath.Clamp01(s);
                default: return StanceMath.Smooth(s);
            }
        }
    }
}
