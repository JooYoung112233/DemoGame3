using UnityEngine;
using C = Demo6.Game.TopDownCanvas;

namespace Demo6.Game
{
    /// <summary>
    /// 정수리 시점 시험판의 임시 그림(코드로 그림). 모두 오른쪽(+x)을 바라보고, 몸 그림은 '몸 지름 = 1' 단위(가운데 기준),
    /// 무기 그림은 월드 유닛(주먹 = 손잡이 가운데 기준)이다. 디아블로 톤(기획/다크판타지-분위기-1차.md): 채도를 뺀 어두운 색, 등급색은 쓰지 않는다.
    /// 돌아가는 그림이라 한쪽에서 비추는 빛 대신 가운데가 밝은 둥근 명암과 가장자리 얇은 밝은 띠로 바닥과 떼어 읽히게 한다.
    /// 처음 쓸 때 한 번 만들어 둔다(그림 자원 캐시: 실행마다 같은 결과라 비울 필요 없음, ShapeSprites와 같은 방식).
    /// 그림 칸(CombatArtSet.topDown, 기획/타격감-리소스-명세.md)이 비어 있을 때 쓰는 대체 그림이다. 칸의 그림은 모두 유닛 그림(PPU 256)이라
    /// 몸 그림만 단위가 다르다(임시 몸 = 몸 지름 1, 칸 몸 = 유닛). 무기·부품은 둘 다 유닛이고 피벗(손잡이·가운데)이 같다.
    /// </summary>
    public static class TopDownSprites
    {
        static Sprite _player;
        static Sprite _longsword;
        static Sprite _greatsword;
        static Sprite _twinblade;
        static Sprite _rat;
        static Sprite _ratDummy;
        static Sprite _boar;
        static Sprite _archer;
        static Sprite _nest;
        static Sprite _woodDummy;
        static Sprite _shadow;

        /// <summary>몸 그림 테두리(아주 어두운 남색 검정).</summary>
        static readonly Color Outline = new Color(0.045f, 0.04f, 0.05f, 1f);

        public static Sprite PlayerBody => _player ? _player : _player = BuildPlayer();
        public static Sprite Longsword => _longsword ? _longsword : _longsword = BuildLongsword();
        public static Sprite Greatsword => _greatsword ? _greatsword : _greatsword = BuildGreatsword();
        public static Sprite Twinblade => _twinblade ? _twinblade : _twinblade = BuildTwinblade();
        public static Sprite Rat => _rat ? _rat : _rat = BuildRat(false);
        public static Sprite RatDummy => _ratDummy ? _ratDummy : _ratDummy = BuildRat(true);
        public static Sprite Boar => _boar ? _boar : _boar = BuildBoar();
        public static Sprite Archer => _archer ? _archer : _archer = BuildArcher();
        public static Sprite Nest => _nest ? _nest : _nest = BuildNest();
        public static Sprite WoodDummy => _woodDummy ? _woodDummy : _woodDummy = BuildWoodDummy();
        /// <summary>몸 밑 둥근 그림자(지름 1, 가장자리로 갈수록 옅음).</summary>
        public static Sprite Shadow => _shadow ? _shadow : _shadow = BuildShadow();

        /// <summary>모든 그림을 한 번에 만든다(TopDownView.Awake: 장면을 여는 동안). 이미 있으면 그대로 둔다.</summary>
        public static void Prewarm()
        {
            _ = PlayerBody;
            _ = Longsword;
            _ = Greatsword;
            _ = Twinblade;
            _ = Rat;
            _ = RatDummy;
            _ = Boar;
            _ = Archer;
            _ = Nest;
            _ = WoodDummy;
            _ = Shadow;
            _ = Limb;
        }

        /// <summary>궁수 활 끝(몸 단위). 시위 부품이 여기서 당김 점까지 이어진다.</summary>
        public static readonly Vector2 ArcherBowTip = new Vector2(0.3f, 0.4f);
        /// <summary>궁수 오른팔 팔꿈치(몸 단위). 아래팔 부품이 여기서 당김 손까지 이어진다.</summary>
        public static readonly Vector2 ArcherElbow = new Vector2(0.1f, -0.21f);

        // ───────────────────────── 검사 ─────────────────────────

        static Sprite BuildPlayer()
        {
            var cv = new C(-0.66f, 0.66f, -0.66f, 0.66f, 110f);
            var cloak = new Color(0.17f, 0.19f, 0.27f);
            var hood = new Color(0.23f, 0.25f, 0.34f);
            var leather = new Color(0.35f, 0.25f, 0.16f);
            var iron = new Color(0.29f, 0.28f, 0.28f);
            var bone = new Color(0.6f, 0.56f, 0.5f);
            var skin = new Color(0.62f, 0.47f, 0.37f);
            var hair = new Color(0.15f, 0.11f, 0.085f);
            var hairLight = new Color(0.33f, 0.25f, 0.18f);

            // 가죽 웃옷(어깨 앞쪽).
            cv.Draw(p => C.Ellipse(p, new Vector2(0.04f, 0f), 0.24f, 0.41f),
                (p, d) => C.Shade(leather, C.Dome(d, 0.2f) * (0.92f + 0.16f * C.Fbm(p, 0.06f, 11)) * C.Rim(d, 0.03f, 0.25f)),
                0.025f, Outline);
            // 망토: 등 뒤 절반을 덮고 목에서 퍼지는 주름.
            cv.Draw(p => C.Ellipse(p, new Vector2(-0.19f, 0f), 0.3f, 0.46f),
                (p, d) =>
                {
                    float ang = Mathf.Atan2(p.y, p.x + 0.04f);
                    float fold = 1f + 0.11f * Mathf.Sin(ang * 9f + C.Noise(p, 0.12f, 12) * 2.5f);
                    float k = C.Dome(d, 0.24f) * fold * (0.94f + 0.12f * C.Fbm(p, 0.035f, 13)) * C.Rim(d, 0.03f, 0.3f);
                    return C.Shade(cloak, k);
                },
                0.025f, Outline);
            // 쇠 어깨받이 둘 + 뼈색 징.
            for (int s = -1; s <= 1; s += 2)
            {
                float side = s;
                cv.Draw(p => C.Ellipse(p, new Vector2(0.01f, 0.33f * side), 0.13f, 0.105f, -12f * side),
                    (p, d) => C.Shade(iron, C.Dome(d, 0.07f) * (0.9f + 0.2f * C.Noise(p, 0.03f, 14)) * C.Rim(d, 0.02f, 0.4f)),
                    0.02f, Outline);
                cv.Draw(p => C.Circle(p, new Vector2(0.06f, 0.33f * side), 0.014f), bone);
            }
            // 목 뒤에 내려놓은 두건 주름.
            cv.Draw(p => C.Ellipse(p, new Vector2(-0.12f, 0f), 0.15f, 0.22f),
                (p, d) => C.Shade(hood, C.Dome(d, 0.1f) * (1f + 0.1f * Mathf.Sin(p.y * 60f)) * C.Rim(d, 0.02f, 0.3f)),
                0.02f, Outline);
            // 이마·코끝(바라보는 쪽 표시).
            cv.Draw(p => Mathf.Min(C.Ellipse(p, new Vector2(0.14f, 0f), 0.1f, 0.12f), C.Ellipse(p, new Vector2(0.245f, 0f), 0.035f, 0.03f)),
                (p, d) => C.Shade(skin, C.Dome(d, 0.05f)),
                0.016f, Outline);
            // 정수리 머리카락: 가마에서 뻗는 결 + 윤기 고리.
            var crown = new Vector2(-0.04f, 0.02f);
            cv.Draw(p => C.Circle(p, new Vector2(0.02f, 0f), 0.178f),
                (p, d) =>
                {
                    Vector2 q = p - crown;
                    float r = q.magnitude;
                    float ang = Mathf.Atan2(q.y, q.x);
                    float strand = C.Noise(new Vector2(ang * 3.2f, r * 6f), 0.35f, 15);
                    float sheen = 1f + 0.55f * Mathf.Exp(-Mathf.Pow((r - 0.1f) / 0.035f, 2f));
                    var c = Color.Lerp(hair, hairLight, Mathf.Clamp01(strand * 0.7f * sheen - 0.15f));
                    return C.Shade(c, C.Dome(d, 0.12f) * C.Rim(d, 0.02f, 0.35f));
                },
                0.02f, Outline);
            return cv.ToSprite("정수리 검사 몸", Vector2.zero);
        }

        // ───────────────────────── 무기(월드 유닛, 주먹 = 손잡이 가운데) ─────────────────────────

        static readonly Color SteelMid = new Color(0.56f, 0.58f, 0.61f);
        static readonly Color SteelEdge = new Color(0.33f, 0.34f, 0.37f);
        static readonly Color SteelBright = new Color(0.9f, 0.91f, 0.93f);
        static readonly Color DarkIron = new Color(0.22f, 0.21f, 0.21f);
        static readonly Color GripLeather = new Color(0.2f, 0.13f, 0.08f);
        static readonly Color Glove = new Color(0.24f, 0.17f, 0.12f);

        /// <summary>칼날 칠: 가장자리가 어둡고 가운데 홈이 밝으며, 끝(tipFrom 이후)으로 갈수록 하이라이트.</summary>
        static Color Blade(Vector2 p, float halfWidth, float from, float tipFrom, float to, int seed)
        {
            float across = Mathf.Clamp01(Mathf.Abs(p.y) / Mathf.Max(1e-4f, halfWidth));
            var c = Color.Lerp(SteelMid, SteelEdge, across * across);
            float fuller = Mathf.Exp(-Mathf.Pow(p.y / (halfWidth * 0.22f), 2f)) * Mathf.Clamp01((tipFrom - p.x) / 0.08f);
            c = Color.Lerp(c, SteelBright, fuller * 0.45f);
            float tip = Mathf.Clamp01((p.x - tipFrom) / Mathf.Max(1e-4f, to - tipFrom));
            c = Color.Lerp(c, SteelBright, tip * tip * 0.75f);
            float wear = 0.93f + 0.14f * C.Noise(new Vector2(p.x * 0.25f, p.y * 3f), 0.02f, seed);
            return C.Shade(c, wear);
        }

        static void Grip(C cv, float x0, float x1, float halfWidth)
        {
            cv.Draw(p => C.Box(p, new Vector2((x0 + x1) * 0.5f, 0f), (x1 - x0) * 0.5f, halfWidth, halfWidth * 0.5f),
                (p, d) => C.Shade(GripLeather, 0.85f + 0.3f * Mathf.Abs(Mathf.Sin(p.x * 140f))), 0.008f, Outline);
        }

        static void Guard(C cv, float x, float halfThick, float halfSpan)
        {
            cv.Draw(p => C.Box(p, new Vector2(x, 0f), halfThick, halfSpan, halfThick * 0.6f),
                (p, d) => C.Shade(DarkIron, C.Dome(d, halfThick) * C.Rim(d, 0.01f, 0.6f)), 0.008f, Outline);
        }

        static void Fist(C cv, Vector2 at, float r)
        {
            cv.Draw(p => C.Circle(p, at, r),
                (p, d) => C.Shade(Glove, C.Dome(d, r) * (0.93f + 0.14f * C.Noise(p, 0.012f, 21)) * C.Rim(d, 0.01f, 0.3f)), 0.01f, Outline);
            // 엄지 마디.
            cv.Draw(p => C.Circle(p, at + new Vector2(r * 0.55f, r * 0.55f), r * 0.38f), (p, d) => C.Shade(Glove, 1.12f), 0.006f, Outline);
        }

        static Sprite BuildLongsword()
        {
            var cv = new C(-0.16f, 1.01f, -0.13f, 0.13f, 170f);
            cv.Draw(p => C.Circle(p, new Vector2(-0.12f, 0f), 0.03f), (p, d) => C.Shade(DarkIron, C.Dome(d, 0.03f) * 1.2f), 0.008f, Outline);
            Grip(cv, -0.11f, 0.07f, 0.021f);
            const float hw = 0.03f;
            cv.Draw(p => Mathf.Min(C.Box(p, new Vector2(0.5f, 0f), 0.4f, hw), C.Triangle(p, new Vector2(0.9f, hw), new Vector2(0.9f, -hw), new Vector2(0.985f, 0f))),
                (p, d) => Blade(p, hw, 0.1f, 0.8f, 0.985f, 22), 0.008f, Outline);
            Guard(cv, 0.085f, 0.017f, 0.105f);
            Fist(cv, Vector2.zero, 0.052f);
            return cv.ToSprite("정수리 장검", Vector2.zero);
        }

        static Sprite BuildGreatsword()
        {
            var cv = new C(-0.23f, 1.43f, -0.18f, 0.18f, 160f);
            cv.Draw(p => C.Circle(p, new Vector2(-0.19f, 0f), 0.04f), (p, d) => C.Shade(DarkIron, C.Dome(d, 0.04f) * 1.2f), 0.009f, Outline);
            Grip(cv, -0.17f, 0.11f, 0.03f);
            const float hw = 0.066f;
            cv.Draw(p => Mathf.Min(C.Box(p, new Vector2(0.71f, 0f), 0.57f, hw, 0.004f), C.Triangle(p, new Vector2(1.27f, hw), new Vector2(1.27f, -hw), new Vector2(1.405f, 0f))),
                (p, d) =>
                {
                    var c = Blade(p, hw, 0.14f, 1.12f, 1.405f, 23);
                    // 이 빠진 자국 몇 군데.
                    float nick = C.Hash01(Mathf.FloorToInt(p.x * 22f), p.y > 0f ? 1 : 0, 24);
                    if (nick > 0.86f && Mathf.Abs(p.y) > hw * 0.8f) c = C.Shade(c, 0.6f);
                    return c;
                }, 0.009f, Outline);
            // 날 밑동(리카소).
            cv.Draw(p => C.Box(p, new Vector2(0.19f, 0f), 0.05f, 0.05f, 0.01f), (p, d) => C.Shade(SteelEdge, C.Dome(d, 0.04f)), 0.008f, Outline);
            Guard(cv, 0.12f, 0.025f, 0.155f);
            Fist(cv, new Vector2(-0.105f, 0f), 0.055f);
            Fist(cv, Vector2.zero, 0.056f);
            return cv.ToSprite("정수리 대검", Vector2.zero);
        }

        static Sprite BuildTwinblade()
        {
            var cv = new C(-0.11f, 0.64f, -0.085f, 0.085f, 190f);
            cv.Draw(p => C.Circle(p, new Vector2(-0.08f, 0f), 0.022f), (p, d) => C.Shade(DarkIron, C.Dome(d, 0.022f) * 1.2f), 0.006f, Outline);
            Grip(cv, -0.075f, 0.04f, 0.018f);
            const float hw = 0.026f;
            cv.Draw(p => Mathf.Min(C.Box(p, new Vector2(0.29f, 0f), 0.235f, hw), C.Triangle(p, new Vector2(0.52f, hw), new Vector2(0.52f, -hw), new Vector2(0.62f, 0f))),
                (p, d) => Blade(p, hw, 0.055f, 0.46f, 0.62f, 25), 0.006f, Outline);
            Guard(cv, 0.047f, 0.012f, 0.062f);
            Fist(cv, Vector2.zero, 0.046f);
            return cv.ToSprite("정수리 쌍검 한 자루", Vector2.zero);
        }

        // ───────────────────────── 굴쥐(허수아비는 짚 색) ─────────────────────────

        static Sprite BuildRat(bool dummy)
        {
            var cv = new C(-0.84f, 0.7f, -0.44f, 0.44f, 150f);
            Color fur = dummy ? new Color(0.56f, 0.47f, 0.3f) : new Color(0.31f, 0.26f, 0.215f);
            Color spine = dummy ? new Color(0.42f, 0.34f, 0.21f) : new Color(0.19f, 0.155f, 0.13f);
            Color pink = dummy ? new Color(0.5f, 0.42f, 0.28f) : new Color(0.55f, 0.41f, 0.39f);
            Color tail = dummy ? new Color(0.45f, 0.37f, 0.24f) : new Color(0.47f, 0.37f, 0.35f);
            int seed = dummy ? 40 : 30;

            // 꼬리(몸 뒤로 휘어 나감).
            cv.Draw(p => C.Curve(p, new Vector2(-0.36f, 0f), new Vector2(-0.64f, -0.05f), new Vector2(-0.79f, 0.2f), 0.046f, 0.012f),
                (p, d) => C.Shade(tail, 0.9f + 0.18f * Mathf.Abs(Mathf.Sin((p.x + p.y) * 70f))), 0.014f, Outline);
            // 몸통: 길쭉한 털 덩이, 등줄기가 어둡다.
            cv.Draw(p => C.Ellipse(p, new Vector2(-0.06f, 0f), 0.36f, 0.235f),
                (p, d) => FurPaint(p, d, fur, spine, 0.07f, seed, dummy), 0.02f, Outline);
            // 머리: 몸에서 주둥이로 가늘어진다.
            cv.Draw(p => C.Capsule(p, new Vector2(0.26f, 0f), new Vector2(0.53f, 0f), 0.16f, 0.045f),
                (p, d) => FurPaint(p, d, C.Shade(fur, 1.08f), spine, 0.04f, seed + 1, dummy), 0.018f, Outline);
            // 귀 둘(안쪽 분홍).
            for (int s = -1; s <= 1; s += 2)
            {
                float side = s;
                cv.Draw(p => C.Circle(p, new Vector2(0.22f, 0.15f * side), 0.07f), (p, d) => C.Shade(fur, C.Dome(d, 0.05f)), 0.014f, Outline);
                cv.Draw(p => C.Circle(p, new Vector2(0.225f, 0.155f * side), 0.042f), pink);
                // 작은 눈(검은 구슬 + 반짝).
                cv.Draw(p => C.Circle(p, new Vector2(0.4f, 0.083f * side), 0.022f), dummy ? new Color(0.15f, 0.12f, 0.08f) : new Color(0.05f, 0.03f, 0.03f));
                if (!dummy) cv.Draw(p => C.Circle(p, new Vector2(0.407f, 0.09f * side), 0.007f), new Color(0.75f, 0.7f, 0.65f));
                // 수염.
                if (!dummy)
                    cv.Draw(p => C.Capsule(p, new Vector2(0.5f, 0.03f * side), new Vector2(0.62f, 0.15f * side), 0.0045f), new Color(0.7f, 0.66f, 0.6f, 0.6f));
            }
            cv.Draw(p => C.Circle(p, new Vector2(0.56f, 0f), 0.03f), pink, 0.008f, Outline);
            if (dummy)
            {
                // 짚 인형 바느질 자국(등줄기를 따라 X자).
                for (int i = 0; i < 6; i++)
                {
                    float x = -0.36f + i * 0.12f;
                    var stitch = new Color(0.2f, 0.15f, 0.09f);
                    cv.Draw(p => Mathf.Min(C.Capsule(p, new Vector2(x - 0.03f, -0.03f), new Vector2(x + 0.03f, 0.03f), 0.007f),
                        C.Capsule(p, new Vector2(x - 0.03f, 0.03f), new Vector2(x + 0.03f, -0.03f), 0.007f)), stitch);
                }
            }
            return cv.ToSprite(dummy ? "정수리 쥐 허수아비" : "정수리 굴쥐", Vector2.zero);
        }

        /// <summary>털 칠: 길이 방향 결, 등줄기 어둡게, 둥근 명암, 가장자리 밝은 띠. 짚 인형은 짚 결.</summary>
        static Color FurPaint(Vector2 p, float d, Color fur, Color spine, float spineWidth, int seed, bool straw)
        {
            float streak = C.Noise(new Vector2(p.x * (straw ? 0.6f : 1.4f), p.y * 7f), 0.06f, seed);
            float s = Mathf.Exp(-Mathf.Pow(p.y / spineWidth, 2f));
            var c = Color.Lerp(fur, spine, s * 0.55f);
            return C.Shade(c, C.Dome(d, 0.14f) * (0.86f + 0.28f * streak) * C.Rim(d, 0.025f, 0.3f));
        }

        // ───────────────────────── 뿔멧돼지 ─────────────────────────

        static Sprite BuildBoar()
        {
            var cv = new C(-0.62f, 0.79f, -0.57f, 0.57f, 165f);
            var fur = new Color(0.24f, 0.19f, 0.14f);
            var furTip = new Color(0.37f, 0.3f, 0.22f);
            var mane = new Color(0.11f, 0.085f, 0.07f);
            var snout = new Color(0.47f, 0.36f, 0.33f);
            var tusk = new Color(0.82f, 0.76f, 0.62f);
            var tuskBase = new Color(0.5f, 0.44f, 0.35f);

            // 몸통: 큰 타원, 등에서 옆구리로 뻗는 뻣뻣한 털.
            cv.Draw(p => C.Ellipse(p, new Vector2(-0.1f, 0f), 0.46f, 0.37f),
                (p, d) => BristlePaint(p, d, fur, furTip, 0.26f, 50), 0.026f, Outline);
            // 귀(머리 뒤쪽 양옆으로 젖힘): 머리 밑에 깔고 끝만 밖으로 나오게.
            for (int s = -1; s <= 1; s += 2)
            {
                float side = s;
                cv.Draw(p => C.Triangle(p, new Vector2(0.17f, 0.16f * side), new Vector2(0.29f, 0.2f * side), new Vector2(0.11f, 0.35f * side)),
                    (p, d) => C.Shade(fur, 0.7f + 0.2f * Mathf.Clamp01(-d / 0.03f)), 0.014f, Outline);
            }
            // 머리: 몸 앞에서 주둥이로 가늘어진다.
            cv.Draw(p => C.Capsule(p, new Vector2(0.26f, 0f), new Vector2(0.56f, 0f), 0.24f, 0.12f),
                (p, d) => BristlePaint(p, d, C.Shade(fur, 1.05f), furTip, 0.14f, 51), 0.022f, Outline);
            // 등 갈기: 등줄기를 따라 가늘고 들쭉날쭉한 검은 털, 가장자리 털끝은 조금 밝다.
            cv.Draw(p => C.Capsule(p, new Vector2(-0.5f, 0f), new Vector2(0.3f, 0f), 0.035f, 0.06f) + (C.Noise(new Vector2(p.x * 1.5f, p.y * 0.3f), 0.012f, 52) - 0.5f) * 0.05f,
                (p, d) => Color.Lerp(C.Shade(mane, 0.85f + 0.4f * C.Noise(new Vector2(p.x * 0.4f, p.y * 3f), 0.02f, 53)), furTip * 0.7f, Mathf.Clamp01(1f + d / 0.018f) * 0.6f),
                0.01f, Outline);
            // 엄니 둘(주둥이 옆에서 앞·바깥으로 휨).
            for (int s = -1; s <= 1; s += 2)
            {
                float side = s;
                cv.Draw(p => C.Curve(p, new Vector2(0.53f, 0.1f * side), new Vector2(0.69f, 0.13f * side), new Vector2(0.7f, 0.29f * side), 0.036f, 0.007f),
                    (p, d) => Color.Lerp(tuskBase, tusk, Mathf.Clamp01((p.x - 0.52f) / 0.12f + Mathf.Abs(p.y) * 1.5f)), 0.013f, Outline);
                // 작은 눈.
                cv.Draw(p => C.Circle(p, new Vector2(0.4f, 0.13f * side), 0.026f), new Color(0.09f, 0.04f, 0.03f));
            }
            // 주둥이 판과 콧구멍.
            cv.Draw(p => C.Ellipse(p, new Vector2(0.6f, 0f), 0.06f, 0.11f), (p, d) => C.Shade(snout, C.Dome(d, 0.05f)), 0.014f, Outline);
            for (int s = -1; s <= 1; s += 2)
            {
                float side = s;
                cv.Draw(p => C.Ellipse(p, new Vector2(0.625f, 0.042f * side), 0.016f, 0.022f), new Color(0.12f, 0.07f, 0.06f));
            }
            return cv.ToSprite("정수리 뿔멧돼지", Vector2.zero);
        }

        static Color BristlePaint(Vector2 p, float d, Color fur, Color tip, float depth, int seed)
        {
            float ang = Mathf.Atan2(p.y, p.x + 0.1f);
            float bristle = C.Noise(new Vector2(ang * 5f, p.magnitude * 1.2f), 0.06f, seed);
            float fine = C.Noise(p, 0.018f, seed + 3);
            var c = Color.Lerp(fur, tip, Mathf.Clamp01(bristle * 0.8f + fine * 0.35f - 0.3f));
            return C.Shade(c, C.Dome(d, depth) * C.Rim(d, 0.03f, 0.28f));
        }

        // ───────────────────────── 가시 궁수 ─────────────────────────

        static Sprite BuildArcher()
        {
            var cv = new C(-0.68f, 0.76f, -0.64f, 0.64f, 175f);
            var cloak = new Color(0.15f, 0.23f, 0.215f);
            var hood = new Color(0.2f, 0.3f, 0.28f);
            var sleeve = new Color(0.19f, 0.27f, 0.25f);
            var leather = new Color(0.32f, 0.23f, 0.15f);
            var thorn = new Color(0.1f, 0.08f, 0.06f);
            var thornTip = new Color(0.36f, 0.3f, 0.22f);
            var wood = new Color(0.34f, 0.23f, 0.13f);
            var feather = new Color(0.62f, 0.58f, 0.5f);

            // 등에 멘 화살통과 깃.
            cv.Draw(p => C.Box(p, new Vector2(-0.3f, 0.12f), 0.07f, 0.19f, 0.03f, 25f),
                (p, d) => C.Shade(leather, C.Dome(d, 0.06f)), 0.016f, Outline);
            for (int i = 0; i < 3; i++)
            {
                var at = new Vector2(-0.3f, 0.12f) + C.Rotate(new Vector2(-0.03f + i * 0.03f, 0.2f), 25f);
                cv.Draw(p => C.Ellipse(p, at, 0.022f, 0.035f, 25f), feather, 0.006f, Outline);
            }
            // 어깨·망토.
            cv.Draw(p => C.Ellipse(p, new Vector2(-0.04f, 0f), 0.24f, 0.44f),
                (p, d) =>
                {
                    float fold = 1f + 0.1f * Mathf.Sin(Mathf.Atan2(p.y, p.x + 0.1f) * 8f + C.Noise(p, 0.1f, 61) * 2f);
                    return C.Shade(cloak, C.Dome(d, 0.2f) * fold * (0.94f + 0.12f * C.Fbm(p, 0.04f, 62)) * C.Rim(d, 0.03f, 0.3f));
                }, 0.025f, Outline);
            // 오른 위팔(아래팔은 당김 손과 함께 따로 움직이는 부품).
            cv.Draw(p => C.Capsule(p, new Vector2(0.0f, -0.3f), ArcherElbow, 0.07f, 0.06f), (p, d) => C.Shade(sleeve, C.Dome(d, 0.05f)), 0.016f, Outline);
            // 왼팔(활 든 팔).
            cv.Draw(p => C.Capsule(p, new Vector2(0.04f, 0.3f), new Vector2(0.43f, 0.035f), 0.068f, 0.055f), (p, d) => C.Shade(sleeve, C.Dome(d, 0.05f)), 0.016f, Outline);
            // 뾰족한 두건(앞쪽은 얼굴 그늘).
            cv.Draw(p => Mathf.Min(C.Circle(p, new Vector2(0.02f, 0f), 0.19f), C.Triangle(p, new Vector2(-0.04f, 0.16f), new Vector2(-0.04f, -0.16f), new Vector2(-0.37f, 0f))),
                (p, d) => C.Shade(hood, C.Dome(d, 0.12f) * (0.94f + 0.12f * C.Fbm(p, 0.03f, 63)) * C.Rim(d, 0.022f, 0.35f)), 0.02f, Outline);
            cv.Draw(p => C.Ellipse(p, new Vector2(0.15f, 0f), 0.065f, 0.1f), new Color(0.05f, 0.05f, 0.05f));
            // 가시: 어깨와 두건에 돋은 검은 가시(이름 그대로).
            for (int s = -1; s <= 1; s += 2)
            {
                float side = s;
                Thorn(cv, new Vector2(-0.12f, 0.41f * side), new Vector2(-0.3f, 1f * side), thorn, thornTip);
                Thorn(cv, new Vector2(0.06f, 0.42f * side), new Vector2(0.15f, 1f * side), thorn, thornTip);
                Thorn(cv, new Vector2(-0.24f, 0.32f * side), new Vector2(-0.7f, 0.7f * side), thorn, thornTip);
                Thorn(cv, new Vector2(-0.16f, 0.1f * side), new Vector2(-0.4f, 1f * side), thorn, thornTip);
            }
            // 활: 손잡이를 앞으로 내밀고 양 끝이 뒤로 휜다(끝 = ArcherBowTip).
            Vector2 tipA = ArcherBowTip, tipB = new Vector2(ArcherBowTip.x, -ArcherBowTip.y);
            cv.Draw(p => C.Curve(p, tipA, new Vector2(0.62f, 0f), tipB, 0.022f, 0.022f),
                (p, d) => C.Shade(wood, 0.85f + 0.3f * C.Noise(new Vector2(p.x * 3f, p.y * 0.5f), 0.02f, 64)), 0.012f, Outline);
            cv.Draw(p => Mathf.Min(C.Circle(p, tipA, 0.022f), C.Circle(p, tipB, 0.022f)), C.Shade(wood, 0.6f), 0.008f, Outline);
            cv.Draw(p => C.Box(p, new Vector2(0.46f, 0f), 0.03f, 0.055f, 0.015f), C.Shade(leather, 0.8f), 0.008f, Outline);
            // 활 든 손.
            cv.Draw(p => C.Circle(p, new Vector2(0.455f, 0.02f), 0.055f), (p, d) => C.Shade(Glove, C.Dome(d, 0.05f)), 0.012f, Outline);
            return cv.ToSprite("정수리 가시 궁수", Vector2.zero);
        }

        static void Thorn(C cv, Vector2 at, Vector2 outward, Color dark, Color tip)
        {
            Vector2 n = outward.normalized;
            Vector2 side = new Vector2(-n.y, n.x) * 0.028f;
            Vector2 end = at + n * 0.09f;
            cv.Draw(p => C.Triangle(p, at + side, at - side, end),
                (p, d) => Color.Lerp(dark, tip, Mathf.Clamp01(Vector2.Dot(p - at, n) / 0.09f)), 0.008f, Outline);
        }

        // ───────────────────────── 굴쥐 둥지(위에서 본 흙더미, 돌리지 않음) ─────────────────────────

        static Sprite BuildNest()
        {
            var cv = new C(-0.64f, 0.64f, -0.64f, 0.64f, 165f);
            var dirtDark = new Color(0.2f, 0.15f, 0.11f);
            var dirt = new Color(0.33f, 0.26f, 0.19f);
            var dirtLight = new Color(0.45f, 0.37f, 0.27f);
            var hole = new Color(0.04f, 0.03f, 0.025f);
            var bone = new Color(0.67f, 0.61f, 0.51f);
            var twig = new Color(0.27f, 0.19f, 0.12f);
            var pebble = new Color(0.37f, 0.35f, 0.32f);

            // 흙더미: 울퉁불퉁한 가장자리, 가운데가 높아 밝다.
            cv.Draw(p =>
                {
                    float ang = Mathf.Atan2(p.y, p.x);
                    float wobble = C.Noise(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 3f, 1f, 70) * 0.09f;
                    return p.magnitude - (0.47f + wobble);
                },
                (p, d) =>
                {
                    float height = Mathf.Clamp01(-d / 0.4f);
                    var c = Color.Lerp(dirtDark, dirt, height);
                    float clod = C.Fbm(p, 0.05f, 71);
                    c = Color.Lerp(c, dirtLight, Mathf.Clamp01(clod - 0.55f) * 1.6f * height);
                    float ridge = 1f + 0.08f * Mathf.Sin(p.magnitude * 55f + clod * 4f);
                    return C.Shade(c, ridge * (0.85f + 0.3f * C.Noise(p, 0.015f, 72)));
                }, 0.03f, Outline);
            // 굴 구멍 셋(가운데가 가장 어둡고 테두리 흙이 밝다).
            Hole(cv, new Vector2(0.05f, -0.04f), 0.13f, 0.11f, hole, dirtLight);
            Hole(cv, new Vector2(-0.24f, 0.19f), 0.065f, 0.055f, hole, dirtLight);
            Hole(cv, new Vector2(0.22f, 0.26f), 0.055f, 0.05f, hole, dirtLight);
            // 할퀸 자국.
            for (int i = 0; i < 3; i++)
            {
                float o = i * 0.03f;
                cv.Draw(p => C.Capsule(p, new Vector2(0.2f + o, -0.2f), new Vector2(0.3f + o, -0.32f), 0.006f), C.Shade(dirtDark, 0.7f));
            }
            // 작은 뼈·잔가지·자갈.
            Bone(cv, new Vector2(-0.3f, -0.18f), new Vector2(-0.13f, -0.28f), bone);
            Bone(cv, new Vector2(0.3f, 0.02f), new Vector2(0.38f, 0.16f), bone);
            Bone(cv, new Vector2(-0.06f, 0.33f), new Vector2(0.06f, 0.36f), bone);
            float[] twigs = { 0.4f, 1.9f, 3.1f, 4.4f, 5.6f };
            for (int i = 0; i < twigs.Length; i++)
            {
                float a = twigs[i];
                Vector2 from = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.33f;
                Vector2 to = from + new Vector2(Mathf.Cos(a + 1.2f), Mathf.Sin(a + 1.2f)) * 0.16f;
                cv.Draw(p => C.Capsule(p, from, to, 0.012f, 0.007f), twig, 0.006f, Outline);
            }
            for (int i = 0; i < 7; i++)
            {
                float a = i * 0.9f + 0.3f;
                float r = 0.2f + C.Hash01(i, 0, 73) * 0.22f;
                Vector2 at = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                float size = 0.018f + C.Hash01(i, 1, 73) * 0.02f;
                cv.Draw(p => C.Circle(p, at, size), (p, d) => C.Shade(pebble, C.Dome(d, size) * 1.1f), 0.006f, Outline);
            }
            return cv.ToSprite("정수리 굴쥐 둥지", Vector2.zero);
        }

        static void Hole(C cv, Vector2 at, float rx, float ry, Color dark, Color rim)
        {
            cv.Draw(p => C.Ellipse(p, at, rx * 1.35f, ry * 1.35f), (p, d) => new Color(rim.r, rim.g, rim.b, Mathf.Clamp01(-d / (rx * 0.3f)) * 0.55f));
            cv.Draw(p => C.Ellipse(p, at, rx, ry), (p, d) => C.Shade(dark, 1f + Mathf.Clamp01(1f + d / (rx * 0.5f)) * 2.5f));
        }

        static void Bone(C cv, Vector2 a, Vector2 b, Color color)
        {
            cv.Draw(p => Mathf.Min(C.Capsule(p, a, b, 0.014f), Mathf.Min(C.Circle(p, a, 0.024f), C.Circle(p, b, 0.024f))),
                (p, d) => C.Shade(color, C.Dome(d, 0.02f)), 0.007f, Outline);
        }

        // ───────────────────────── 나무 허수아비 ─────────────────────────

        static Sprite BuildWoodDummy()
        {
            var cv = new C(-0.66f, 0.66f, -0.66f, 0.66f, 130f);
            var plank = new Color(0.44f, 0.33f, 0.21f);
            var burlap = new Color(0.52f, 0.43f, 0.29f);
            var straw = new Color(0.66f, 0.56f, 0.33f);
            var rope = new Color(0.3f, 0.23f, 0.14f);

            // 양팔 가로대(나뭇결) + 끝의 짚 다발.
            for (int s = -1; s <= 1; s += 2)
            {
                float side = s;
                for (int i = 0; i < 4; i++)
                {
                    float x = -0.07f + i * 0.045f;
                    cv.Draw(p => C.Capsule(p, new Vector2(x, 0.52f * side), new Vector2(x + 0.02f, 0.63f * side), 0.012f), straw);
                }
            }
            cv.Draw(p => C.Box(p, Vector2.zero, 0.085f, 0.56f, 0.02f),
                (p, d) => C.Shade(plank, C.Dome(d, 0.07f) * (0.85f + 0.3f * C.Noise(new Vector2(p.x * 8f, p.y * 0.6f), 0.05f, 80))), 0.02f, Outline);
            // 짚 자루 머리(올 짜임).
            cv.Draw(p => C.Circle(p, Vector2.zero, 0.25f),
                (p, d) =>
                {
                    float weave = 0.9f + 0.1f * Mathf.Sign(Mathf.Sin(p.x * 120f)) * Mathf.Sign(Mathf.Sin(p.y * 120f));
                    return C.Shade(burlap, C.Dome(d, 0.2f) * weave * (0.92f + 0.16f * C.Noise(p, 0.04f, 81)) * C.Rim(d, 0.025f, 0.25f));
                }, 0.022f, Outline);
            // 묶은 끈과 정수리 매듭.
            cv.Draw(p => C.Capsule(p, new Vector2(-0.17f, -0.17f), new Vector2(0.17f, 0.17f), 0.014f), rope);
            cv.Draw(p => C.Capsule(p, new Vector2(-0.17f, 0.17f), new Vector2(0.17f, -0.17f), 0.014f), rope);
            cv.Draw(p => C.Circle(p, Vector2.zero, 0.045f), C.Shade(rope, 1.2f), 0.01f, Outline);
            return cv.ToSprite("정수리 나무 허수아비", Vector2.zero);
        }

        // ───────────────────────── 팔(막대) ─────────────────────────

        static Sprite _limb;

        /// <summary>
        /// 팔·아래팔 막대(1×1 단위, 가운데 기준, 길이 방향 +x). 부품이 두 점 사이에 (길이, 굵기)로 늘려 놓는다. 흰색이라 색은 렌더러가 입힌다.
        /// </summary>
        public static Sprite Limb => _limb ? _limb : _limb = BuildLimb();

        static Sprite BuildLimb()
        {
            var cv = new C(-0.5f, 0.5f, -0.5f, 0.5f, 48f);
            cv.Draw(p => C.Box(p, Vector2.zero, 0.42f, 0.32f, 0.26f),
                (p, d) => C.Shade(Color.white, Mathf.Lerp(0.62f, 1f, Mathf.Clamp01(1f - Mathf.Abs(p.y) / 0.32f))), 0.08f, new Color(0.1f, 0.09f, 0.1f));
            return cv.ToSprite("정수리 팔", Vector2.zero);
        }

        // ───────────────────────── 그림자 ─────────────────────────

        static Sprite BuildShadow()
        {
            var cv = new C(-0.5f, 0.5f, -0.5f, 0.5f, 64f);
            cv.Draw(p => p.magnitude - 0.5f, (p, d) => new Color(0f, 0f, 0f, Mathf.Pow(Mathf.Clamp01(-d / 0.5f), 0.8f)));
            return cv.ToSprite("정수리 그림자", Vector2.zero);
        }
    }
}
