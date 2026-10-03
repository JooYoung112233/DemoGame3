using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 정수리 시점 임시 그림을 코드로 그리는 작은 화판. 모양은 '안쪽이 음수인 거리'로 주고, 가장자리를 1픽셀 부드럽게 덮어 칠한다.
    /// 처음 한 번 그림을 만들 때만 쓰므로(TopDownSprites 캐시) 매 프레임 비용·할당이 없다. 좌표는 그림 단위(몸 지름 1 또는 월드 유닛)다.
    /// </summary>
    public sealed class TopDownCanvas
    {
        public delegate float Shape(Vector2 p);
        public delegate Color Paint(Vector2 p, float d);

        public readonly int Width;
        public readonly int Height;
        readonly float _x0;
        readonly float _y0;
        readonly float _unit;
        readonly float _ppu;
        readonly Color[] _px;

        /// <param name="pixelsPerUnit">그림 단위 1에 들어가는 픽셀 수(스프라이트 PPU와 같다).</param>
        public TopDownCanvas(float xMin, float xMax, float yMin, float yMax, float pixelsPerUnit)
        {
            _ppu = pixelsPerUnit;
            Width = Mathf.Max(4, Mathf.CeilToInt((xMax - xMin) * pixelsPerUnit));
            Height = Mathf.Max(4, Mathf.CeilToInt((yMax - yMin) * pixelsPerUnit));
            _x0 = xMin;
            _y0 = yMin;
            _unit = 1f / pixelsPerUnit;
            _px = new Color[Width * Height];
        }

        /// <summary>픽셀 한 칸의 그림 단위 크기(가장자리 부드럽게 하는 폭).</summary>
        public float Pixel => _unit;

        Vector2 Pos(int x, int y) => new Vector2(_x0 + (x + 0.5f) * _unit, _y0 + (y + 0.5f) * _unit);

        /// <summary>모양을 칠한다. outline &gt; 0이면 먼저 그 폭만큼 바깥에 테두리 색을 깐다(배경과 떼어 읽히게).</summary>
        public void Draw(Shape shape, Paint paint, float outline = 0f, Color outlineColor = default)
        {
            float aa = _unit;
            // 8×8 칸 묶음 가운데에서 먼저 재 보고 모양에서 멀면 건너뛴다(근사 거리는 실제보다 작게 나오므로 넉넉히 둔 여유로 안전).
            const int block = 8;
            float skip = outline + aa + block * _unit * 0.7071f * 1.5f + 0.04f;
            for (int by = 0; by < Height; by += block)
            for (int bx = 0; bx < Width; bx += block)
            {
                int x1 = Mathf.Min(Width, bx + block);
                int y1 = Mathf.Min(Height, by + block);
                var center = new Vector2(_x0 + (bx + x1) * 0.5f * _unit, _y0 + (by + y1) * 0.5f * _unit);
                if (shape(center) > skip) continue;
                for (int y = by; y < y1; y++)
                for (int x = bx; x < x1; x++)
                {
                    var p = Pos(x, y);
                    float d = shape(p);
                    if (d > outline + aa) continue;
                    int i = y * Width + x;
                    if (outline > 0f) Blend(i, outlineColor, Coverage(d - outline, aa));
                    float c = Coverage(d, aa);
                    if (c > 0f) Blend(i, paint(p, d), c);
                }
            }
        }

        /// <summary>한 가지 색으로 칠한다.</summary>
        public void Draw(Shape shape, Color color, float outline = 0f, Color outlineColor = default) =>
            Draw(shape, (p, d) => color, outline, outlineColor);

        static float Coverage(float d, float aa) => Mathf.Clamp01(0.5f - d / aa);

        void Blend(int i, Color src, float cover)
        {
            float a = src.a * cover;
            if (a <= 0f) return;
            var dst = _px[i];
            float outA = a + dst.a * (1f - a);
            if (outA <= 0.0001f) return;
            float k = dst.a * (1f - a);
            _px[i] = new Color(
                (src.r * a + dst.r * k) / outA,
                (src.g * a + dst.g * k) / outA,
                (src.b * a + dst.b * k) / outA,
                outA);
        }

        /// <summary>
        /// 스프라이트로 굽는다. pivot은 그림 단위 좌표(보통 0,0 = 몸 중심·손잡이). 밉맵을 만들어 작게 보일 때 지글거림을 줄인다.
        /// 완전히 투명한 픽셀은 검정 RGB라 가장자리가 어둡게 번진다(밝은 테두리 번짐 방지).
        /// </summary>
        public Sprite ToSprite(string name, Vector2 pivot)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, true)
            {
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = name,
            };
            var px32 = new Color32[_px.Length];
            for (int i = 0; i < _px.Length; i++) px32[i] = _px[i];
            tex.SetPixels32(px32);
            tex.Apply(true, true);
            var pivot01 = new Vector2((pivot.x - _x0) / (Width * _unit), (pivot.y - _y0) / (Height * _unit));
            var sprite = Sprite.Create(tex, new Rect(0, 0, Width, Height), pivot01, _ppu, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }

        // ───────────── 모양(거리) ─────────────

        public static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        /// <summary>타원(근사 거리). angle은 도 단위 회전.</summary>
        public static float Ellipse(Vector2 p, Vector2 c, float rx, float ry, float angle = 0f)
        {
            Vector2 q = p - c;
            if (angle != 0f) q = Rotate(q, -angle);
            float k = new Vector2(q.x / rx, q.y / ry).magnitude;
            return (k - 1f) * Mathf.Min(rx, ry);
        }

        /// <summary>두 점 사이 굵기가 ra → rb로 바뀌는 막대.</summary>
        public static float Capsule(Vector2 p, Vector2 a, Vector2 b, float ra, float rb)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(1e-6f, ba.sqrMagnitude));
            return (pa - ba * h).magnitude - Mathf.Lerp(ra, rb, h);
        }

        public static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r) => Capsule(p, a, b, r, r);

        /// <summary>둥근 모서리 사각형(중심, 반폭·반높이, 회전 도).</summary>
        public static float Box(Vector2 p, Vector2 c, float hx, float hy, float round = 0f, float angle = 0f)
        {
            Vector2 q = p - c;
            if (angle != 0f) q = Rotate(q, -angle);
            Vector2 d = new Vector2(Mathf.Abs(q.x) - hx + round, Mathf.Abs(q.y) - hy + round);
            Vector2 outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - round;
        }

        /// <summary>삼각형(꼭짓점 셋, 순서 무관). 근사 거리.</summary>
        public static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float s = Cross(b - a, c - a) < 0f ? -1f : 1f;
            float d0 = EdgeDist(p, a, b, s);
            float d1 = EdgeDist(p, b, c, s);
            float d2 = EdgeDist(p, c, a, s);
            return Mathf.Max(d0, Mathf.Max(d1, d2));
        }

        static float EdgeDist(Vector2 p, Vector2 a, Vector2 b, float s)
        {
            Vector2 e = b - a;
            float len = Mathf.Max(1e-6f, e.magnitude);
            return -s * Cross(e, p - a) / len;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        /// <summary>2차 곡선(a → 조절점 c → b)을 따라 굵기 ra → rb인 띠. 12토막으로 잰다.</summary>
        public static float Curve(Vector2 p, Vector2 a, Vector2 c, Vector2 b, float ra, float rb)
        {
            const int n = 12;
            float best = float.MaxValue;
            Vector2 prev = a;
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                Vector2 cur = Bezier(a, c, b, t);
                float t0 = (i - 1) / (float)n;
                float d = Capsule(p, prev, cur, Mathf.Lerp(ra, rb, t0), Mathf.Lerp(ra, rb, t));
                if (d < best) best = d;
                prev = cur;
            }
            return best;
        }

        public static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * c + t * t * b;
        }

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float cs = Mathf.Cos(r), sn = Mathf.Sin(r);
            return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
        }

        // ───────────── 결(노이즈)·명암 ─────────────

        static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u + 0x7F4A7C15u;
                h ^= (uint)x * 0x85EBCA77u;
                h = (h << 13) | (h >> 19);
                h ^= (uint)y * 0xC2B2AE3Du;
                h *= 0x27D4EB2Fu;
                h ^= h >> 15;
                h *= 0x165667B1u;
                h ^= h >> 13;
                return h;
            }
        }

        public static float Hash01(int x, int y, int seed) => (Hash(x, y, seed) >> 8) * (1f / 16777216f);

        /// <summary>값 노이즈(0~1). scale은 격자 한 칸 크기(그림 단위).</summary>
        public static float Noise(Vector2 p, float scale, int seed)
        {
            float x = p.x / scale, y = p.y / scale;
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float tx = Smooth(x - xi), ty = Smooth(y - yi);
            float a = Hash01(xi, yi, seed), b = Hash01(xi + 1, yi, seed);
            float c = Hash01(xi, yi + 1, seed), d = Hash01(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        /// <summary>두 겹 노이즈(굵은 결 + 잔결, 0~1).</summary>
        public static float Fbm(Vector2 p, float scale, int seed) =>
            Noise(p, scale, seed) * 0.65f + Noise(p, scale * 0.45f, seed + 7) * 0.35f;

        /// <summary>
        /// 둥근 덩이 명암: 가운데가 밝고 가장자리가 어둡다(위에서 내리비추는 빛). 돌아가는 그림이라 한쪽 방향 빛은 넣지 않는다.
        /// d는 모양 거리(안쪽 음수), depth는 가장자리에서 가운데까지 대략의 깊이.
        /// </summary>
        public static float Dome(float d, float depth) => Mathf.Lerp(0.72f, 1.08f, Mathf.Clamp01(-d / Mathf.Max(1e-4f, depth)));

        /// <summary>가장자리 안쪽 얇은 띠를 밝힌다(어두운 바닥에서 윤곽이 읽히게).</summary>
        public static float Rim(float d, float width, float amount) => 1f + amount * Mathf.Clamp01(1f - (-d) / Mathf.Max(1e-4f, width)) * (d < 0f ? 1f : 0f);

        public static Color Shade(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);

        public static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
