using Demo6.Core.Combat.Stance;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 대검 기울기별 임시 그림(3차, 2026-10-05). 사용자 원문: "대검그림을 압축하는게아니라 진짜로 칼탈을 돌려야 좀더 자연스러울것같아",
    /// "한장의 이미즐 ㄹ반복 쓰기보단 asprite 기능으로 한장씩처리할꺼라", "대검은 두께이야기한건데 길이말고".
    /// 넓은 날 대검(날 폭 0.66, 자루 끝 ~ 칼끝 1.635, 두께 0.07, GreatswordPoses 치수)을 두께 있는 칼로 보고 기울기(−80·−60·−45·−30·0·30·45·60·80°)마다
    /// 실제로 돌린 모양을 계산해 한 장씩 그린다: 날 방향 길이 × cos(기울기), 날 아랫면은 + 두께/2 × sin(기울기) 쪽으로 밀려 두께 띠(칼등·날 끝 두께)가 보이고,
    /// 윗면은 기울수록 어두워지며 두께 띠가 밝아진다(빛 받는 면이 바뀜). 그림 배율은 늘 1이고 게임(WeaponStanceArms)이 기울기에 가장 가까운 그림을 갈아 끼운다.
    /// 칼끝을 들면 자루가 아래(카메라에서 멀리)라 자루 → 날밑 → 날 차례, 칼끝을 내리면 날 → 날밑 → 자루 차례로 그린다.
    /// 피벗 = 손잡이 쥔 점(0, 0), +x = 칼끝, PPU 256(새 무기 임시 그림과 같음). 처음 쓸 때 한 번만 만든다(매 프레임 할당 없음).
    /// 원화 칸 설계(메모만, CombatArtSet.cs는 다른 세션 변경이 섞여 고치지 않음): TopDownWeaponArt에 greatswordTilt(Sprite[], TiltBins와 같은 차례,
    /// 0° 칸은 greatsword 칸과 같은 그림)를 두고 칸이 차 있으면 For()가 그 그림을 고른다. Aseprite에서 기울기마다 한 장씩 그린 프레임을 그대로 넣는다
    /// (기획/타격감-리소스-명세.md 13장, 같은 피벗·같은 유닛 크기, 한 장을 늘이거나 눌러 만들지 않음).
    /// 칼 그림자 띠(ShadowBand)와 바닥 균열(Crack)도 임시 그림이다(균열 원화 칸은 아직 없음).
    /// </summary>
    public static class GreatswordTiltArt
    {
        /// <summary>그린 기울기(도, GreatswordPoses.TiltBins와 같음). 가장 가까운 그림을 쓴다(15° 밑은 0°, 70° 위는 80°).</summary>
        public static float[] TiltBins => GreatswordPoses.TiltBins;

        const float Ppu = 256f;
        const float OutlineWidth = 2f / Ppu;
        static readonly Color Outline = new Color(0.043f, 0.039f, 0.051f);
        static readonly Color SteelMid = new Color(0.561f, 0.580f, 0.612f);
        static readonly Color SteelEdge = new Color(0.329f, 0.341f, 0.369f);
        static readonly Color SteelBright = new Color(0.902f, 0.910f, 0.929f);
        static readonly Color Leather = new Color(0.2f, 0.129f, 0.078f);
        static readonly Color DarkIron = new Color(0.25f, 0.24f, 0.26f);

        static readonly Sprite[] s_cache = new Sprite[9];
        static Sprite s_shadow;
        static Sprite s_crack;

        /// <summary>기울기에 가장 가까운 그림 칸 번호.</summary>
        public static int BinOf(float tilt) => GreatswordPoses.TiltBinOf(tilt);

        /// <summary>기울기(도)에 맞는 그림(가장 가까운 칸).</summary>
        public static Sprite For(float tilt)
        {
            int i = BinOf(tilt);
            return s_cache[i] ? s_cache[i] : s_cache[i] = Build(GreatswordPoses.TiltBins[i]);
        }

        static float Convex(Vector2 p, Vector2[] pts)
        {
            Vector2 a0 = pts[1] - pts[0], b0 = pts[2] - pts[0];
            float s = a0.x * b0.y - a0.y * b0.x < 0f ? -1f : 1f;
            float best = float.MinValue;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 a = pts[i], e = pts[(i + 1) % pts.Length] - a;
                float d = -s * (e.x * (p.y - a.y) - e.y * (p.x - a.x)) / Mathf.Max(1e-6f, e.magnitude);
                if (d > best) best = d;
            }
            return best;
        }

        static Vector2[] BladePoly(float c, float e) => new[]
        {
            new Vector2(GreatswordPoses.BladeFrom * c + e, -GreatswordPoses.BladeHalfWidth),
            new Vector2(GreatswordPoses.TipTaperFrom * c + e, -GreatswordPoses.BladeHalfWidthAtTaper),
            new Vector2(GreatswordPoses.TipAt * c + e, 0f),
            new Vector2(GreatswordPoses.TipTaperFrom * c + e, GreatswordPoses.BladeHalfWidthAtTaper),
            new Vector2(GreatswordPoses.BladeFrom * c + e, GreatswordPoses.BladeHalfWidth),
        };

        static Sprite Build(float tilt)
        {
            float c = Mathf.Cos(tilt * Mathf.Deg2Rad), s = Mathf.Sin(tilt * Mathf.Deg2Rad), sa = Mathf.Abs(s);
            float halfT = GreatswordPoses.BladeThickness * 0.5f;
            float x0 = Mathf.Min(GreatswordPoses.PommelCenter * c - GreatswordPoses.PommelRadius, GreatswordPoses.GripFrom * c - GreatswordPoses.GripHalfWidth,
                GreatswordPoses.BladeFrom * c - halfT * sa) - 0.04f;
            float x1 = GreatswordPoses.TipAt * c + halfT * sa + 0.04f;
            float hy = GreatswordPoses.GuardHalfWidth + 0.04f;
            var cv = new TopDownCanvas(x0, Mathf.Max(x1, GreatswordPoses.GuardCenter * c + 0.12f), -hy, hy, Ppu);
            if (s > 0.05f)
            {
                Hilt(cv, c, sa);
                Guard(cv, c, sa, halfT);
                Blade(cv, c, s, halfT);
            }
            else
            {
                Blade(cv, c, s, halfT);
                Guard(cv, c, sa, halfT);
                Hilt(cv, c, sa);
            }
            return cv.ToSprite("정수리 대검 기울기 " + tilt.ToString("0"), Vector2.zero);
        }

        static void Hilt(TopDownCanvas cv, float c, float sa)
        {
            float g0 = GreatswordPoses.GripFrom * c, g1 = GreatswordPoses.GripTo * c, r = GreatswordPoses.GripHalfWidth;
            float half = (g1 - g0) * 0.5f + r * sa;
            var mid = new Vector2((g0 + g1) * 0.5f, 0f);
            cv.Draw(p => TopDownCanvas.Box(p, mid, Mathf.Max(half, r * 0.6f), r, r * 0.5f),
                (p, d) => TopDownCanvas.Shade(Leather, 0.9f + 0.25f * Mathf.Abs(Mathf.Sin((p.x / Mathf.Max(0.2f, c)) * 70f))), OutlineWidth, Outline);
            var pc = new Vector2(GreatswordPoses.PommelCenter * c, 0f);
            cv.Draw(p => TopDownCanvas.Circle(p, pc, GreatswordPoses.PommelRadius),
                (p, d) => TopDownCanvas.Shade(DarkIron, TopDownCanvas.Dome(d, GreatswordPoses.PommelRadius) * 1.25f), OutlineWidth, Outline);
        }

        static void Guard(TopDownCanvas cv, float c, float sa, float halfT)
        {
            float hl = GreatswordPoses.GuardHalfThick * c + halfT * sa;
            var mid = new Vector2(GreatswordPoses.GuardCenter * c, 0f);
            cv.Draw(p => TopDownCanvas.Box(p, mid, Mathf.Max(hl, 0.012f), GreatswordPoses.GuardHalfWidth, 0.012f),
                (p, d) =>
                {
                    // 날밑 윗면(가운데 줄)이 밝고 두께 쪽은 어둡다.
                    float top = Mathf.Clamp01(1f - Mathf.Abs(p.x - mid.x) / Mathf.Max(0.004f, GreatswordPoses.GuardHalfThick * c + 0.004f));
                    return TopDownCanvas.Shade(DarkIron, (0.85f + 0.45f * top * c) * TopDownCanvas.Rim(d, 0.008f, 0.4f));
                }, OutlineWidth, Outline);
        }

        static void Blade(TopDownCanvas cv, float c, float s, float halfT)
        {
            float sa = Mathf.Abs(s);
            float faceK = 0.62f + 0.38f * c;
            float bandK = 0.72f + 0.45f * sa;
            if (sa > 0.01f)
            {
                // 아랫면(두께 띠): + 두께/2 × sin 쪽. 칼끝을 들면 칼끝 쪽, 내리면 손 쪽에 보인다.
                var under = BladePoly(c, halfT * s);
                cv.Draw(p => Convex(p, under), (p, d) => TopDownCanvas.Shade(Color.Lerp(SteelEdge, SteelMid, 0.35f), bandK), OutlineWidth, Outline);
            }
            float e = -halfT * s;
            var face = BladePoly(c, e);
            float f0 = (GreatswordPoses.BladeFrom + 0.07f) * c + e, f1 = (GreatswordPoses.TipTaperFrom - 0.04f) * c + e;
            cv.Draw(p => Convex(p, face),
                (p, d) =>
                {
                    float ay = Mathf.Abs(p.y);
                    // 큰 색면: 위쪽 반은 밝고 아래쪽 반은 조금 어둡게(날 면 두 쪽), 가운데 홈은 어둡게, 날 가장자리 벼린 줄은 밝게.
                    var col = p.y >= 0f ? Color.Lerp(SteelMid, SteelBright, 0.35f) : SteelMid;
                    if (ay < 0.04f && p.x > f0 && p.x < f1) col = Color.Lerp(SteelEdge, SteelMid, 0.2f);
                    else if (-d < 0.035f) col = Color.Lerp(col, SteelBright, 0.55f);
                    return TopDownCanvas.Shade(col, faceK);
                }, OutlineWidth, Outline);
        }

        /// <summary>칼 그림자 띠(흰색, 게임이 검게 물들임): 1 × 1 유닛, 둥근 끝, 가장자리가 부드럽다. 길이·폭은 그림자 띠를 놓을 때 정한다(그림자는 칼 그림이 아님).</summary>
        public static Sprite ShadowBand
        {
            get
            {
                if (s_shadow) return s_shadow;
                var cv = new TopDownCanvas(-0.5f, 0.5f, -0.5f, 0.5f, 64f);
                cv.Draw(p => TopDownCanvas.Box(p, Vector2.zero, 0.5f, 0.5f, 0.48f), (p, d) => new Color(1f, 1f, 1f, Mathf.Clamp01(-d / 0.3f)));
                return s_shadow = cv.ToSprite("대검 칼 그림자", Vector2.zero);
            }
        }

        /// <summary>바닥 균열 임시 그림(지름 약 1.1 유닛, 가운데가 피벗): 가운데 눌린 자국 + 갈라진 금 7줄.</summary>
        public static Sprite Crack
        {
            get
            {
                if (s_crack) return s_crack;
                var cv = new TopDownCanvas(-0.6f, 0.6f, -0.6f, 0.6f, 128f);
                var dark = new Color(0.05f, 0.04f, 0.035f, 0.85f);
                cv.Draw(p => TopDownCanvas.Ellipse(p, Vector2.zero, 0.2f, 0.14f), (p, d) => new Color(0.06f, 0.05f, 0.04f, 0.45f * Mathf.Clamp01(-d / 0.08f)));
                const int lines = 7;
                for (int k = 0; k < lines; k++)
                {
                    float a0 = (k * 360f / lines + 20f * (TopDownCanvas.Hash01(k, 1, 77) - 0.5f)) * Mathf.Deg2Rad;
                    var pts = new Vector2[5];
                    float len = 0.34f + 0.2f * TopDownCanvas.Hash01(k, 2, 77);
                    for (int j = 0; j < pts.Length; j++)
                    {
                        float r = 0.08f + len * j / (pts.Length - 1);
                        float wob = (TopDownCanvas.Hash01(k, 10 + j, 77) - 0.5f) * 0.35f;
                        pts[j] = new Vector2(Mathf.Cos(a0 + wob) * r, Mathf.Sin(a0 + wob) * r);
                    }
                    var seg = pts;
                    cv.Draw(p =>
                    {
                        float best = float.MaxValue;
                        for (int j = 0; j < seg.Length - 1; j++)
                        {
                            float w0 = Mathf.Lerp(0.024f, 0.006f, j / (float)(seg.Length - 1));
                            float w1 = Mathf.Lerp(0.024f, 0.006f, (j + 1) / (float)(seg.Length - 1));
                            best = Mathf.Min(best, TopDownCanvas.Capsule(p, seg[j], seg[j + 1], w0, w1));
                        }
                        return best;
                    }, dark);
                }
                return s_crack = cv.ToSprite("대검 바닥 균열", Vector2.zero);
            }
        }
    }
}
