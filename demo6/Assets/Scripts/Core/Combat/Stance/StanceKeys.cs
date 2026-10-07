using System;

namespace Demo6.Core.Combat.Stance
{
    /// <summary>
    /// 키 하나(리그 틀 값). Right = 오른손(기울기 Tilt 포함), Left = 방패 손(x, y, 방패각, 방패깊이) 또는 쌍검 왼칼 손(대검은 쓰지 않음, 둘째 손은 오른손에서 셈).
    /// Curve = 앞 키에서 이 키로 오는 곡선(spec '곡선'). Height = 대검 손 높이(칼 그림자). 할당 없음(readonly struct).
    /// </summary>
    public readonly struct StanceKey
    {
        public readonly float T;
        public readonly StanceCurve Curve;
        public readonly HandPose Right;
        public readonly HandPose Left;
        public readonly float Flip;
        public readonly float Twist;
        public readonly float Lean;
        public readonly float ScaleX;
        public readonly float ScaleY;
        public readonly float Blade;
        public readonly float Height;

        public StanceKey(float t, StanceCurve curve, HandPose right, HandPose left, float flip, float twist, float lean, float scaleX, float scaleY, float blade,
            float height = 0f)
        {
            T = t;
            Curve = curve;
            Right = right;
            Left = left;
            Flip = flip;
            Twist = twist;
            Lean = lean;
            ScaleX = scaleX;
            ScaleY = scaleY;
            Blade = blade;
            Height = height;
        }

        /// <summary>같은 값을 다른 시간·곡선으로.</summary>
        public StanceKey At(float t, StanceCurve curve) => new StanceKey(t, curve, Right, Left, Flip, Twist, Lean, ScaleX, ScaleY, Blade, Height);

        /// <summary>자세 하나를 키로(쌍검·방패 손은 Left 그대로, 방패는 Left.Angle·Length를 ShieldAngle·ShieldDepth로 바꿔 넣음).</summary>
        public static StanceKey FromPose(in StancePose p, StanceWeapon w, float t = 0f, StanceCurve curve = StanceCurve.Smooth)
        {
            var left = p.Left;
            if (w == StanceWeapon.SwordShield) left = HandPose.At(p.Left.X, p.Left.Y, p.ShieldAngle, p.ShieldDepth, 1f);
            return new StanceKey(t, curve, p.Right, left, p.Flip, p.Twist, p.LeanX, p.ScaleX > 0f ? p.ScaleX : 1f, p.ScaleY > 0f ? p.ScaleY : 1f,
                p.BladeScale > 0f ? p.BladeScale : 1f, p.Height);
        }
    }

    /// <summary>
    /// 키 표 읽기(spec '보간', 3차 2026-10-05): 키 사이는 몸 틀(비틀기·앞뒤를 뺀 틀)에서 곧게 섞는다. 손 자리는 회전(−비틀기)(손 − (앞뒤, 0))로 옮겨 섞고
    /// 날 각은 (날 − 비틀기)를 섞은 뒤 되돌린다. 기울기·손 높이도 곧게 섞는다. 2차의 길이 부호 넘김(135° 규칙, 칼이 길이 0을 지나 줄었다 늘어남)은 없앴다:
    /// 날 각 차이가 180°를 넘을 때만 가까운 쪽 표기로 옮긴다. 쌍검·대검·한손검 모두 이 식(설계 render.py interp와 같음). 할당 없음.
    /// </summary>
    public static class StanceKeys
    {
        /// <summary>키 표를 시각 t에서 읽는다(처음 키 앞은 처음 키, 끝 키 뒤는 끝 키).</summary>
        public static StancePose Eval(StanceKey[] keys, float t, StanceWeapon w)
        {
            if (t <= keys[0].T) return FromKey(keys[0], w);
            int last = keys.Length - 1;
            if (t >= keys[last].T) return FromKey(keys[last], w);
            int i = 1;
            while (i < last && t > keys[i].T) i++;
            var a = keys[i - 1];
            var b = keys[i];
            float span = b.T - a.T;
            float u = StanceCurves.Apply(b.Curve, span > 1e-6f ? (t - a.T) / span : 1f);
            return Interp(a, b, u, w);
        }

        public static StancePose FromKey(in StanceKey k, StanceWeapon w) => Interp(k, k, 0f, w);

        /// <summary>두 키를 몸 틀에서 u(0~1)만큼 섞는다.</summary>
        public static StancePose Interp(in StanceKey a, in StanceKey b, float u, StanceWeapon w)
        {
            float tw = StanceMath.Lerp(a.Twist, b.Twist, u);
            float off = StanceMath.Lerp(a.Lean, b.Lean, u);
            var p = new StancePose
            {
                Twist = tw,
                LeanX = off,
                ScaleX = StanceMath.Lerp(a.ScaleX, b.ScaleX, u),
                ScaleY = StanceMath.Lerp(a.ScaleY, b.ScaleY, u),
                BladeScale = StanceMath.Lerp(a.Blade, b.Blade, u),
                Flip = StanceMath.Lerp(a.Flip, b.Flip, u),
                Height = StanceMath.Lerp(a.Height, b.Height, u),
            };
            p.Right = Canon(Unrel(LerpHand(Rel(a.Right, a.Twist, a.Lean), Rel(b.Right, b.Twist, b.Lean), u), tw, off));
            switch (w)
            {
                case StanceWeapon.SwordShield:
                {
                    // 방패 손: 자리·방패각·깊이를 몸 틀에서 곧게(넘김 규칙 없음).
                    var sa = Rel(a.Left, a.Twist, a.Lean);
                    var sb = Rel(b.Left, b.Twist, b.Lean);
                    var s = Unrel(HandPose.Lerp(sa, sb, u), tw, off);
                    p.Left = HandPose.At(s.X, s.Y, s.Angle, s.Length, 1f);
                    p.ShieldAngle = s.Angle;
                    p.ShieldDepth = s.Length;
                    break;
                }
                case StanceWeapon.Twinblades:
                    p.Left = Canon(Unrel(LerpHand(Rel(a.Left, a.Twist, a.Lean), Rel(b.Left, b.Twist, b.Lean), u), tw, off));
                    break;
                case StanceWeapon.Greatsword:
                    p.Left = GreatswordPoses.SecondHand(p.Right, p.BladeScale);
                    break;
            }
            return p;
        }

        /// <summary>리그 틀 손 → 몸 틀(앞뒤를 빼고 −비틀기만큼 돌림, 날 − 비틀기).</summary>
        public static HandPose Rel(HandPose h, float twist, float lean)
        {
            StanceMath.Rotate(h.X - lean, h.Y, -twist, out float x, out float y);
            h.X = x;
            h.Y = y;
            h.Angle -= twist;
            return h;
        }

        /// <summary>몸 틀 손 → 리그 틀.</summary>
        public static HandPose Unrel(HandPose h, float twist, float lean)
        {
            StanceMath.Rotate(h.X, h.Y, twist, out float x, out float y);
            h.X = x + lean;
            h.Y = y;
            h.Angle += twist;
            return h;
        }

        /// <summary>
        /// 손 섞기(3차): 날 차이가 180°를 넘으면 b를 가까운 쪽 표기(날 − 360k)로 옮겨 섞는다. 그 안은 감지 않고 곧게(설계와 같음). 길이 부호는 바꾸지 않는다.
        /// </summary>
        public static HandPose LerpHand(HandPose a, HandPose b, float u)
        {
            float d = b.Angle - a.Angle;
            if (Math.Abs(d) > 180f) b.Angle -= 360f * (float)Math.Round(d / 360.0);
            return HandPose.Lerp(a, b, u);
        }

        /// <summary>늘 길이 ≥ 0 꼴, 날 각 (−180, 180].</summary>
        public static HandPose Canon(HandPose h)
        {
            h = StanceMath.NonNegativeLength(h);
            h.Angle = StanceMath.WrapDeg(h.Angle);
            return h;
        }

        /// <summary>
        /// 리그 틀 자세 둘을 몸 틀에서 섞는다(되돌아가기용). 비틀기는 가까운 쪽(회오리 뒤 큰 값), 손은 Interp와 같은 식.
        /// 시작·끝 자세의 비틀기가 360°를 넘어도 몸 틀 손 자리는 같으므로 결과 손은 그대로다.
        /// </summary>
        public static StancePose Blend(in StancePose from, in StancePose to, float u, StanceWeapon w)
        {
            var a = StanceKey.FromPose(from, w);
            var b = StanceKey.FromPose(to, w);
            // 비틀기 차이를 가까운 쪽으로: b의 비틀기를 a 근처 표기로 옮긴다(몸 틀 손 값은 비틀기 표기와 무관).
            float twB = a.Twist + StanceMath.WrapDeg(b.Twist - a.Twist);
            float shift = twB - b.Twist;
            if (Math.Abs(shift) > 1e-4f)
            {
                var r = b.Right; r.Angle += shift;
                StanceMath.Rotate(b.Right.X - b.Lean, b.Right.Y, shift, out float rx, out float ry);
                r.X = rx + b.Lean; r.Y = ry;
                var l = b.Left; l.Angle += shift;
                StanceMath.Rotate(b.Left.X - b.Lean, b.Left.Y, shift, out float lx, out float ly);
                l.X = lx + b.Lean; l.Y = ly;
                b = new StanceKey(b.T, b.Curve, r, l, b.Flip, twB, b.Lean, b.ScaleX, b.ScaleY, b.Blade, b.Height);
            }
            // 날 각 표기: 몸 틀 날 차이를 (−180, 180]로 맞춘다(감지 않음 규칙은 같은 자세 표기끼리).
            b = AlignAngles(a, b, w);
            return Interp(a, b, u, w);
        }

        static StanceKey AlignAngles(in StanceKey a, in StanceKey b, StanceWeapon w)
        {
            var r = b.Right;
            float dr = (r.Angle - b.Twist) - (a.Right.Angle - a.Twist);
            r.Angle -= dr - StanceMath.WrapDeg(dr);
            var l = b.Left;
            float dl = (l.Angle - b.Twist) - (a.Left.Angle - a.Twist);
            l.Angle -= dl - StanceMath.WrapDeg(dl);
            return new StanceKey(b.T, b.Curve, r, l, b.Flip, b.Twist, b.Lean, b.ScaleX, b.ScaleY, b.Blade, b.Height);
        }
    }
}
