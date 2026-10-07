using System;
using System.Collections.Generic;
using Demo6.Core.Loot;

namespace Demo6.Core.Combat.Stance
{
    /// <summary>
    /// 한 자세의 규칙 ② 여유(기획/세-무기-우클릭-소켓-1차.md 0-2). Head = 무기 모든 점·방패판이 투구 원 밖으로 떨어진 거리,
    /// Blade = 날·날밑·방패판이 몸 덩어리 밖으로 떨어진 거리, Hilt = 자루·자루 머리가 몸 덩어리 밖으로 떨어진 거리(주먹 안·대검 두 주먹 사이 자루는 뺌),
    /// Arm = 어깨 (0, ±0.27)에서 손까지 가장 긴 팔, SecondArm = 대검 둘째 손 팔, WeaponSize = 칼 크기 배율(Size × BladeScale).
    /// 잴 점이 없으면 그 여유는 NoPoint(9)다.
    /// </summary>
    public struct KeepOutReport
    {
        public float Head;
        public float Blade;
        public float Hilt;
        public float Arm;
        public float SecondArm;
        public float WeaponSize;

        /// <summary>0-2 기준을 모두 지키는가(execution이면 크기 상한이 칼 × 1.25).</summary>
        public bool Pass(bool execution) =>
            Head >= WeaponKeepOut.HeadMarginMin - 1e-6f
            && Blade >= WeaponKeepOut.BladeMarginMin - 1e-6f
            && Hilt >= WeaponKeepOut.HiltMarginMin - 1e-6f
            && Arm <= WeaponKeepOut.ArmMax + 1e-6f
            && SecondArm <= WeaponKeepOut.SecondArmMax + 1e-6f
            && WeaponSize <= (execution ? WeaponKeepOut.ExecutionSizeMax : WeaponKeepOut.SizeMax) + 1e-6f;
    }

    /// <summary>
    /// 규칙 ②(무기는 머리 위로 가지 않고 몸을 뚫지 않는다)를 숫자로 재는 검사(0-2). 몸 모형은 spec.json '몸_모형'(2026-10-05): 게임이 실제로 그리는
    /// 몸 그림(가죽 = TopDownPilotV9 Hero, 사슬·판금 = PlayerV4)의 불투명 영역을 덮는 타원(몸통·어깨받이 둘·투구 원은 갑옷마다, 망토는 같음).
    /// 무기 모양은 spec 3차 '무기_모양'(대검은 넓은 날 임시 모양: 날 폭 0.66, 자루 끝 ~ 칼끝 1.635, 두께 0.07). 재는 법은 설계 render.py check_pose와 같다:
    /// 무기 그림 모양(자루 머리 원, 자루 네모, 날 다섯 모서리, 날밑 네모)의 테두리를 0.003 간격 점으로 잡고, 칼 기울기(Tilt)는 실제로 돌린 모양으로
    /// 날 방향 거리 × cos(기울기), 날·날밑 점은 날 방향으로 ± 두께/2 × |sin(기울기)|(두께 띠) 두 벌을 잰다(3차: 칼 그림은 늘이거나 줄이지 않음).
    /// 점 q = 회전(−비틀기)(점 − (앞뒤, 옆 밀림)) ÷ 몸 크기, 타원까지 여유 = (√타원 값 − 1) × 짧은 반지름, 머리 여유 = |q − 투구 중심| − 반지름.
    /// 방패판은 바깥 테두리 160점. 갑옷 무게(가죽·사슬·판금 = ArmorWeight.Light·Medium·Heavy, None은 가죽)마다 그 갑옷의 몸통·어깨받이·투구와 망토를 잰다
    /// (세 무게를 다 재면 설계의 세 벌 합친 검사와 같은 값). 무기 점 배치는 처음 한 번만 만든다(매 호출 할당 없음).
    /// 시험(StanceKeepOutTests)이 세 무기 모든 움직임을 1/480초로 표집해 spec.json 3차 '검사_요약'(51개 통과)과 가장 작은 여유를 맞춘다.
    /// </summary>
    public static class WeaponKeepOut
    {
        /// <summary>기준(0-2 표, spec 2차 '검사_기준').</summary>
        public const float HeadMarginMin = 0.02f;
        public const float BladeMarginMin = 0.01f;
        public const float HiltMarginMin = 0f;
        public const float ArmMax = 0.74f;
        /// <summary>대검 둘째 손(왼손, 배 앞을 건넘) 팔 한도(두 마디 팔 0.84 안).</summary>
        public const float SecondArmMax = 0.82f;
        public const float SizeMax = 1.15f;
        public const float ExecutionSizeMax = 1.15f * 1.25f;
        /// <summary>어깨 자리(몸 틀, ±y).</summary>
        public const float ShoulderX = 0f;
        public const float ShoulderY = 0.27f;
        /// <summary>주먹 반지름(자루 검사에서 뺌, 그린 주먹 지름 0.12).</summary>
        public const float FistRadius = 0.06f;
        /// <summary>잴 점이 없을 때의 여유.</summary>
        public const float NoPoint = 9f;
        /// <summary>테두리 점 간격.</summary>
        public const float SampleStep = 0.003f;

        /// <summary>갑옷 한 벌의 몸 모형: 몸통 타원(중심 x, y, 반지름 x, y), 어깨받이(중심 x, ±y, 반지름 x, y, 기울기°), 투구 원(중심 x, y, 반지름).</summary>
        sealed class Armor
        {
            public float TunicX, TunicY, TunicRx, TunicRy;
            public float PadX, PadY, PadRx, PadRy, PadTilt;
            public float HeadX, HeadY, HeadR;
            public float PadCos, PadSin;

            public Armor Init()
            {
                PadCos = (float)Math.Cos(PadTilt * Math.PI / 180.0);
                PadSin = (float)Math.Sin(PadTilt * Math.PI / 180.0);
                return this;
            }
        }

        /// <summary>가죽·사슬·판금(spec '몸_모형.갑옷').</summary>
        static readonly Armor[] Armors =
        {
            new Armor { TunicX = 0.003f, TunicY = 0f, TunicRx = 0.304f, TunicRy = 0.249f, PadX = -0.028f, PadY = 0.308f, PadRx = 0.243f, PadRy = 0.176f, PadTilt = 20f,
                HeadX = 0.10f, HeadY = 0f, HeadR = 0.205f }.Init(),
            new Armor { TunicX = -0.03f, TunicY = 0f, TunicRx = 0.24f, TunicRy = 0.26f, PadX = 0.01f, PadY = 0.248f, PadRx = 0.214f, PadRy = 0.129f, PadTilt = 5f,
                HeadX = -0.025f, HeadY = 0f, HeadR = 0.224f }.Init(),
            new Armor { TunicX = -0.03f, TunicY = 0f, TunicRx = 0.24f, TunicRy = 0.26f, PadX = 0.01f, PadY = 0.278f, PadRx = 0.225f, PadRy = 0.165f, PadTilt = 10f,
                HeadX = -0.02f, HeadY = 0f, HeadR = 0.221f }.Init(),
        };

        /// <summary>망토(세 벌 같음): 중심 (−0.55, 0), 반지름 0.44 × 0.42.</summary>
        const float CapeX = -0.55f, CapeY = 0f, CapeRx = 0.44f, CapeRy = 0.42f;

        /// <summary>투구 원 하나(가죽·사슬·판금 = 0·1·2). 그림·시험이 읽는다.</summary>
        public static void Head(ArmorWeight armor, out float x, out float y, out float r)
        {
            var a = Armors[Index(armor)];
            x = a.HeadX;
            y = a.HeadY;
            r = a.HeadR;
        }

        static int Index(ArmorWeight armor) => armor == ArmorWeight.Medium ? 1 : armor == ArmorWeight.Heavy ? 2 : 0;

        /// <summary>무기 모양 점(쥔 점 기준: 날 방향 U, 가로 V, 날 쪽인가).</summary>
        sealed class Shape
        {
            public float[] U;
            public float[] V;
            public bool[] Blade;
            /// <summary>칼끝(날 방향 거리).</summary>
            public float Tip;
            /// <summary>날 두께의 반(기울면 날 방향으로 ± 이만큼 × |sin 기울기| 보임).</summary>
            public float HalfThick;

            /// <summary>자루 머리 원(중심, 반지름) · 자루 [시작, 끝, 반폭] · 날밑(자리, 반두께, 반폭) · 날 [시작, 칼끝 세모 시작, 칼끝, 반폭(밑), 반폭(세모 시작)].</summary>
            public static Shape Of(float pommelC, float pommelR, float grip0, float grip1, float gripHw, float guardC, float guardHt, float guardHw,
                float blade0, float bladeTri, float tip, float hw0, float hw1, float thickness)
            {
                var u = new List<float>(4096);
                var v = new List<float>(4096);
                var b = new List<bool>(4096);
                // 그리는 순서와 같은 차례(자루 머리, 자루, 날, 날밑).
                var pommel = new float[64];
                for (int i = 0; i < 32; i++)
                {
                    double a = 2.0 * Math.PI * i / 32.0;
                    pommel[2 * i] = (float)(pommelC + pommelR * Math.Cos(a));
                    pommel[2 * i + 1] = (float)(pommelR * Math.Sin(a));
                }
                Sample(pommel, false, u, v, b);
                Sample(Rect(grip0, grip1, gripHw), false, u, v, b);
                Sample(new[] { blade0, -hw0, bladeTri, -hw1, tip, 0f, bladeTri, hw1, blade0, hw0 }, true, u, v, b);
                Sample(Rect(guardC - guardHt, guardC + guardHt, guardHw), true, u, v, b);
                return new Shape { U = u.ToArray(), V = v.ToArray(), Blade = b.ToArray(), Tip = tip, HalfThick = thickness * 0.5f };
            }

            static float[] Rect(float u0, float u1, float hw) => new[] { u0, -hw, u1, -hw, u1, hw, u0, hw };

            /// <summary>다각형 테두리를 0.003 간격으로(각 변 시작점 포함, 끝점 뺌, 설계 sample_poly와 같음).</summary>
            static void Sample(float[] poly, bool blade, List<float> u, List<float> v, List<bool> b)
            {
                int n = poly.Length / 2;
                for (int i = 0; i < n; i++)
                {
                    double ax = poly[2 * i], ay = poly[2 * i + 1];
                    int j = (i + 1) % n;
                    double bx = poly[2 * j], by = poly[2 * j + 1];
                    double len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                    int m = Math.Max(1, (int)Math.Ceiling(len / SampleStep - 1e-9));
                    for (int k = 0; k < m; k++)
                    {
                        u.Add((float)(ax + (bx - ax) * k / m));
                        v.Add((float)(ay + (by - ay) * k / m));
                        b.Add(blade);
                    }
                }
            }
        }

        // spec 3차 '무기_모양'(한손검·쌍검은 1차와 같고, 대검은 넓은 날 임시 모양 GreatswordPoses 치수: 날 폭 0.66, 길이 1.635, 두께 0.07).
        static readonly Shape Sword = Shape.Of(-0.12f, 0.03f, -0.11f, 0.07f, 0.021f, 0.085f, 0.017f, 0.105f, 0.10f, 0.90f, 0.985f, 0.03f, 0.03f, 0.02f);
        static readonly Shape Great = Shape.Of(GreatswordPoses.PommelCenter, GreatswordPoses.PommelRadius, GreatswordPoses.GripFrom, GreatswordPoses.GripTo,
            GreatswordPoses.GripHalfWidth, GreatswordPoses.GuardCenter, GreatswordPoses.GuardHalfThick, GreatswordPoses.GuardHalfWidth,
            GreatswordPoses.BladeFrom, GreatswordPoses.TipTaperFrom, GreatswordPoses.TipAt, GreatswordPoses.BladeHalfWidth, GreatswordPoses.BladeHalfWidthAtTaper,
            GreatswordPoses.BladeThickness);
        static readonly Shape Twin = Shape.Of(-0.08f, 0.022f, -0.075f, 0.04f, 0.018f, 0.047f, 0.012f, 0.062f, 0.055f, 0.525f, 0.62f, 0.026f, 0.026f, 0.016f);

        /// <summary>대검 자루 끝(자루 머리 원 뒤 끝) ~ 칼끝 길이(유닛): 1.635(원화 길이로 되돌림).</summary>
        public static float GreatswordTotalLength => Great.Tip - (GreatswordPoses.PommelCenter - GreatswordPoses.PommelRadius);

        /// <summary>방패판: 반지름 0.28(지름 0.56), 중심 = 왼손 + 0.05 × 방패 방향, 방패 방향 반지름 0.28 × max(0.14, 깊이). 바깥 테두리 160점.</summary>
        const float ShieldRadius = 0.28f;
        const float ShieldOffset = 0.05f;
        const int ShieldRing = 160;
        static readonly float[] RingCos = MakeRing(true);
        static readonly float[] RingSin = MakeRing(false);

        static float[] MakeRing(bool cos)
        {
            var a = new float[ShieldRing];
            for (int i = 0; i < ShieldRing; i++)
            {
                double ph = 2.0 * Math.PI * i / ShieldRing;
                a[i] = (float)(cos ? Math.Cos(ph) : Math.Sin(ph));
            }
            return a;
        }

        /// <summary>한 번 재는 동안의 몸 틀 값(할당 없음).</summary>
        struct Frame
        {
            public float Cos, Sin, OffX, OffY, ScX, ScY;
            public Armor Armor;
            public float FistAx, FistAy, FistBx, FistBy;
            public bool Between;
            public float G2x, G2y, Vx, Vy, VLen2;
        }

        /// <summary>
        /// 한 자세를 잰다. weapon = 무기, armor = 갑옷 무게(None이면 Light로 본다), execution = 처형 자세(크기 상한 × 1.25).
        /// 자세는 리그 틀이다(비틀기·앞뒤·옆 밀림·몸 크기를 빼서 몸 틀로 옮겨 잼). 방패 무기는 Left 자리 + ShieldAngle·ShieldDepth로 방패판을 잰다.
        /// </summary>
        public static KeepOutReport Check(StanceWeapon weapon, in StancePose pose, ArmorWeight armor, bool execution = false)
        {
            var rep = new KeepOutReport { Head = NoPoint, Blade = NoPoint, Hilt = NoPoint };
            float ws = pose.BladeScale > 0f ? pose.BladeScale : 1f;
            float scx = pose.ScaleX > 0f ? pose.ScaleX : 1f;
            float scy = pose.ScaleY > 0f ? pose.ScaleY : 1f;
            var f = new Frame
            {
                Cos = StanceMath.Cos(-pose.Twist),
                Sin = StanceMath.Sin(-pose.Twist),
                OffX = pose.LeanX,
                OffY = pose.LeanY,
                ScX = scx,
                ScY = scy,
                Armor = Armors[Index(armor)],
                FistAx = pose.Right.X,
                FistAy = pose.Right.Y,
            };
            var right = pose.Right;
            float leftX, leftY;
            switch (weapon)
            {
                case StanceWeapon.Greatsword:
                {
                    var g2 = GreatswordPoses.SecondHand(right, ws);
                    f.FistBx = g2.X;
                    f.FistBy = g2.Y;
                    f.Between = true;
                    f.G2x = g2.X;
                    f.G2y = g2.Y;
                    f.Vx = right.X - g2.X;
                    f.Vy = right.Y - g2.Y;
                    f.VLen2 = f.Vx * f.Vx + f.Vy * f.Vy;
                    leftX = g2.X;
                    leftY = g2.Y;
                    Measure(Great, right, pose.Flip, ws, ref f, ref rep);
                    rep.WeaponSize = right.Size * ws;
                    break;
                }
                case StanceWeapon.Twinblades:
                {
                    var left = pose.Left;
                    f.FistBx = left.X;
                    f.FistBy = left.Y;
                    leftX = left.X;
                    leftY = left.Y;
                    Measure(Twin, left, 0f, ws, ref f, ref rep);
                    Measure(Twin, right, 0f, ws, ref f, ref rep);
                    rep.WeaponSize = Math.Max(right.Size, left.Size) * ws;
                    break;
                }
                case StanceWeapon.SwordShield:
                {
                    leftX = pose.Left.X;
                    leftY = pose.Left.Y;
                    f.FistBx = leftX;
                    f.FistBy = leftY;
                    Measure(Sword, right, 0f, ws, ref f, ref rep);
                    MeasureShield(pose.Left.X, pose.Left.Y, pose.ShieldAngle, pose.ShieldDepth, ref f, ref rep);
                    rep.WeaponSize = right.Size * ws;
                    break;
                }
                default:
                    leftX = pose.Left.X;
                    leftY = pose.Left.Y;
                    f.FistBx = leftX;
                    f.FistBy = leftY;
                    Measure(Sword, right, 0f, ws, ref f, ref rep);
                    rep.WeaponSize = right.Size * ws;
                    break;
            }

            // 팔: 어깨 = 회전(비틀기)((0, ±0.27 × 몸 세로 크기)) + (앞뒤, 옆).
            float c = StanceMath.Cos(pose.Twist), s = StanceMath.Sin(pose.Twist);
            float sy = ShoulderY * scy;
            float srx = sy * s + f.OffX, sry = -sy * c + f.OffY;
            float slx = -sy * s + f.OffX, sly = sy * c + f.OffY;
            float armR = Dist(right.X - srx, right.Y - sry);
            float armL = Dist(leftX - slx, leftY - sly);
            if (weapon == StanceWeapon.Greatsword)
            {
                rep.Arm = armR;
                rep.SecondArm = armL;
            }
            else
            {
                rep.Arm = Math.Max(armR, armL);
                rep.SecondArm = 0f;
            }
            return rep;
        }

        static float Dist(float x, float y) => (float)Math.Sqrt(x * x + y * y);

        static void Measure(Shape shape, in HandPose hand, float flip, float ws, ref Frame f, ref KeepOutReport rep)
        {
            // 기울기: 날 방향 거리 × cos(기울기), 날·날밑은 두께 띠 ± 두께/2 × |sin(기울기)| 두 벌(설계 render.py weapon_points와 같음).
            float scale = hand.Size * ws;
            float along = hand.Length * scale * StanceMath.Cos(hand.Tilt);
            float across = scale * (1f - 2f * flip);
            float e = shape.HalfThick * Math.Abs(StanceMath.Sin(hand.Tilt)) * scale;
            float ca = StanceMath.Cos(hand.Angle), sa = StanceMath.Sin(hand.Angle);
            int n = shape.U.Length;
            var u = shape.U;
            var v = shape.V;
            var bl = shape.Blade;
            bool band = e > 1e-6f;
            for (int i = 0; i < n; i++)
            {
                float pu = u[i] * along;
                float pv = v[i] * across;
                if (band && bl[i])
                {
                    float p1 = pu + e, p2 = pu - e;
                    Point(hand.X + p1 * ca - pv * sa, hand.Y + p1 * sa + pv * ca, true, ref f, ref rep);
                    Point(hand.X + p2 * ca - pv * sa, hand.Y + p2 * sa + pv * ca, true, ref f, ref rep);
                }
                else Point(hand.X + pu * ca - pv * sa, hand.Y + pu * sa + pv * ca, bl[i], ref f, ref rep);
            }
        }

        static void MeasureShield(float x, float y, float angle, float depth, ref Frame f, ref KeepOutReport rep)
        {
            float ax = StanceMath.Cos(angle), ay = StanceMath.Sin(angle);
            float tx = -ay, ty = ax;
            float cx = x + ax * ShieldOffset, cy = y + ay * ShieldOffset;
            float a = ShieldRadius * Math.Max(0.14f, depth);
            float b = ShieldRadius;
            for (int i = 0; i < ShieldRing; i++)
            {
                float pa = a * RingCos[i];
                float pb = b * RingSin[i];
                Point(cx + ax * pa + tx * pb, cy + ay * pa + ty * pb, true, ref f, ref rep);
            }
        }

        static void Point(float px, float py, bool blade, ref Frame f, ref KeepOutReport rep)
        {
            float dx = px - f.OffX, dy = py - f.OffY;
            float qx = (dx * f.Cos - dy * f.Sin) / f.ScX;
            float qy = (dx * f.Sin + dy * f.Cos) / f.ScY;
            var arm = f.Armor;
            float head = Dist(qx - arm.HeadX, qy - arm.HeadY) - arm.HeadR;
            if (head < rep.Head) rep.Head = head;
            if (!blade)
            {
                if (Near(px, py, f.FistAx, f.FistAy)) return;
                if (Near(px, py, f.FistBx, f.FistBy)) return;
                if (f.Between && f.VLen2 > 1e-9f)
                {
                    float t = ((px - f.G2x) * f.Vx + (py - f.G2y) * f.Vy) / f.VLen2;
                    if (t >= -0.05f && t <= 1.05f) return;
                }
            }
            float g = BodyClear(qx, qy, arm);
            if (blade) { if (g < rep.Blade) rep.Blade = g; }
            else if (g < rep.Hilt) rep.Hilt = g;
        }

        static bool Near(float x, float y, float fx, float fy)
        {
            float dx = x - fx, dy = y - fy;
            return dx * dx + dy * dy < FistRadius * FistRadius;
        }

        /// <summary>몸 틀 점 q에서 그 갑옷 몸 덩어리(몸통·어깨받이 둘·망토)까지 가장 작은 여유.</summary>
        public static float BodyClear(float qx, float qy, ArmorWeight armor) => BodyClear(qx, qy, Armors[Index(armor)]);

        static float BodyClear(float qx, float qy, Armor a)
        {
            float best = Ellipse(qx - a.TunicX, qy - a.TunicY, a.TunicRx, a.TunicRy);
            best = Math.Min(best, Ellipse(qx - CapeX, qy - CapeY, CapeRx, CapeRy));
            // 왼쪽 어깨받이(+y)는 −기울기만큼 돌린 타원: 점을 +기울기 돌려 잰다. 오른쪽(−y)은 +기울기 타원: 점을 −기울기 돌려 잰다.
            float lx = qx - a.PadX, ly = qy - a.PadY;
            best = Math.Min(best, Ellipse(lx * a.PadCos - ly * a.PadSin, lx * a.PadSin + ly * a.PadCos, a.PadRx, a.PadRy));
            float rx = qx - a.PadX, ry = qy + a.PadY;
            best = Math.Min(best, Ellipse(rx * a.PadCos + ry * a.PadSin, -rx * a.PadSin + ry * a.PadCos, a.PadRx, a.PadRy));
            return best;
        }

        /// <summary>축 맞춘 타원까지 여유 = (√((x/rx)² + (y/ry)²) − 1) × 짧은 반지름.</summary>
        static float Ellipse(float x, float y, float rx, float ry)
        {
            float a = x / rx, b = y / ry;
            return ((float)Math.Sqrt(a * a + b * b) - 1f) * Math.Min(rx, ry);
        }

        /// <summary>몸 틀 점이 그 갑옷 투구 원 밖으로 떨어진 거리.</summary>
        public static float HeadClear(float qx, float qy, ArmorWeight armor)
        {
            var a = Armors[Index(armor)];
            return Dist(qx - a.HeadX, qy - a.HeadY) - a.HeadR;
        }
    }
}
