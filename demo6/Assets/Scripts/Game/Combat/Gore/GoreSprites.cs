using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 피·조각 임시 그림(원화 전 절차 텍스처, 기획/다크판타지-분위기-1차.md '리소스 단계로 넘길 것'의 자리 견본).
    /// 모두 흰색 바탕(색은 렌더러 색으로 곱함)이고 중심 기준, 크기 1유닛. 변형마다 고정 씨앗이라 늘 같은 모양이다.
    /// 처음 쓸 때 한 번 만들고, ShapeSprites처럼 유니티 null 검사로 사라졌으면 다시 만든다(플레이마다 새로 만들지 않아 누수 없음).
    /// </summary>
    public static class GoreSprites
    {
        public const int SplatVariants = 4;
        public const int PoolVariants = 3;
        public const int StreakVariants = 2;
        public const int ChunkVariants = 4;

        static Sprite _drop;
        static Sprite[] _splats;
        static Sprite[] _pools;
        static Sprite[] _streaks;
        static Sprite[] _chunks;
        static Texture2D _edge;

        /// <summary>날아가는 피 방울(부드러운 원 32px). 속도 쪽으로 늘여 그린다.</summary>
        public static Sprite Drop
        {
            get
            {
                if (!_drop) _drop = MakeDrop();
                return _drop;
            }
        }

        /// <summary>바닥 피 얼룩: 들쭉날쭉한 덩이 + 둘레 잔방울, 가장자리가 조금 진하다.</summary>
        public static Sprite Splat(int index)
        {
            if (_splats == null || !_splats[0])
            {
                _splats = new Sprite[SplatVariants];
                for (int i = 0; i < SplatVariants; i++) _splats[i] = MakeSplat(1100 + i * 37);
            }
            return _splats[Wrap(index, SplatVariants)];
        }

        /// <summary>피 웅덩이: 부드럽게 퍼진 큰 덩이(가운데가 더 진함).</summary>
        public static Sprite Pool(int index)
        {
            if (_pools == null || !_pools[0])
            {
                _pools = new Sprite[PoolVariants];
                for (int i = 0; i < PoolVariants; i++) _pools[i] = MakePool(2200 + i * 41);
            }
            return _pools[Wrap(index, PoolVariants)];
        }

        /// <summary>날아와 떨어진 방울 자국: +x로 머리, 뒤로 꼬리(가로 1 × 세로 0.5유닛).</summary>
        public static Sprite Streak(int index)
        {
            if (_streaks == null || !_streaks[0])
            {
                _streaks = new Sprite[StreakVariants];
                for (int i = 0; i < StreakVariants; i++) _streaks[i] = MakeStreak(3300 + i * 43);
            }
            return _streaks[Wrap(index, StreakVariants)];
        }

        /// <summary>살 조각: 모난 덩이, 가운데는 몸 색 그대로 가장자리는 붉게.</summary>
        public static Sprite Chunk(int index)
        {
            if (_chunks == null || !_chunks[0])
            {
                _chunks = new Sprite[ChunkVariants];
                for (int i = 0; i < ChunkVariants; i++) _chunks[i] = MakeChunk(4400 + i * 47);
            }
            return _chunks[Wrap(index, ChunkVariants)];
        }

        /// <summary>화면 가장자리 붉음(가운데 투명, 모서리 진함). 화면 전체에 늘여 그린다.</summary>
        public static Texture2D EdgeTexture
        {
            get
            {
                if (!_edge) _edge = MakeEdge();
                return _edge;
            }
        }

        /// <summary>첫 처치에서 멈칫하지 않게 미리 만든다(GoreSystem.Awake).</summary>
        public static void Prewarm()
        {
            _ = Drop;
            Splat(0);
            Pool(0);
            Streak(0);
            Chunk(0);
            _ = EdgeTexture;
        }

        static int Wrap(int i, int n) => ((i % n) + n) % n;

        static Sprite MakeDrop()
        {
            const int size = 32;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Clamp01((0.92f - r) * 7f);
                px[y * size + x] = new Color32(255, 255, 255, ToByte(a));
            }
            return ToSprite("gore_drop", size, size, px, size);
        }

        static Sprite MakeSplat(int seed)
        {
            const int size = 128;
            var rng = new System.Random(seed);
            float p1 = R(rng, 0f, 6.28f), p2 = R(rng, 0f, 6.28f), p3 = R(rng, 0f, 6.28f), p4 = R(rng, 0f, 6.28f);
            float m1 = R(rng, 0f, 6.28f), m2 = R(rng, 0f, 6.28f);
            int satCount = 7 + rng.Next(6);
            var satX = new float[satCount];
            var satY = new float[satCount];
            var satR = new float[satCount];
            for (int i = 0; i < satCount; i++)
            {
                float ang = R(rng, 0f, 6.28f);
                float d = R(rng, 0.6f, 0.9f);
                satX[i] = Mathf.Cos(ang) * d;
                satY[i] = Mathf.Sin(ang) * d;
                satR[i] = R(rng, 0.03f, 0.085f);
            }
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float th = Mathf.Atan2(v, u);
                float edge = 0.5f + 0.08f * Mathf.Sin(3f * th + p1) + 0.05f * Mathf.Sin(5f * th + p2)
                             + 0.035f * Mathf.Sin(9f * th + p3) + 0.02f * Mathf.Sin(13f * th + p4);
                float inside = edge - r;
                float a = Mathf.Clamp01(inside * 40f);
                for (int i = 0; i < satCount; i++)
                {
                    float dx = u - satX[i];
                    float dy = v - satY[i];
                    float s = Mathf.Clamp01((satR[i] - Mathf.Sqrt(dx * dx + dy * dy)) * 60f);
                    if (s > a) a = s;
                }
                // 얼룩진 속(옅은 얼룩무늬)과 마른 가장자리(커피 고리처럼 진함).
                float mottle = 0.5f + 0.5f * Mathf.Sin(u * 11f + m1) * Mathf.Sin(v * 13f + m2);
                float rim = inside > 0f ? Mathf.Clamp01(1f - inside * 9f) : 1f;
                float lum = (0.86f + 0.14f * mottle) * (1f - 0.28f * rim);
                a *= 0.84f + 0.16f * mottle;
                byte l = ToByte(lum);
                px[y * size + x] = new Color32(l, l, l, ToByte(a));
            }
            return ToSprite("gore_splat", size, size, px, size);
        }

        static Sprite MakePool(int seed)
        {
            const int size = 128;
            var rng = new System.Random(seed);
            float p1 = R(rng, 0f, 6.28f), p2 = R(rng, 0f, 6.28f), p3 = R(rng, 0f, 6.28f);
            const int lobes = 3;
            var lx = new float[lobes];
            var ly = new float[lobes];
            var lr = new float[lobes];
            for (int i = 0; i < lobes; i++)
            {
                float ang = R(rng, 0f, 6.28f);
                float d = R(rng, 0.25f, 0.42f);
                lx[i] = Mathf.Cos(ang) * d;
                ly[i] = Mathf.Sin(ang) * d;
                lr[i] = R(rng, 0.32f, 0.5f);
            }
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float th = Mathf.Atan2(v, u);
                float edge = 0.62f + 0.06f * Mathf.Sin(2f * th + p1) + 0.045f * Mathf.Sin(3f * th + p2) + 0.03f * Mathf.Sin(5f * th + p3);
                float inside = edge - r;
                for (int i = 0; i < lobes; i++)
                {
                    float dx = u - lx[i];
                    float dy = v - ly[i];
                    float li = lr[i] - Mathf.Sqrt(dx * dx + dy * dy);
                    if (li > inside) inside = li;
                }
                // 가장자리를 0.98 안쪽에서 끝내 사각형 테두리가 잘려 보이지 않게 한다.
                inside = Mathf.Min(inside, 0.98f - r);
                float a = Mathf.Clamp01(inside * 16f);
                float lum = Mathf.Lerp(0.78f, 1f, Mathf.Clamp01(inside * 3f));
                byte l = ToByte(lum);
                px[y * size + x] = new Color32(l, l, l, ToByte(a));
            }
            return ToSprite("gore_pool", size, size, px, size);
        }

        static Sprite MakeStreak(int seed)
        {
            const int w = 128;
            const int h = 64;
            var rng = new System.Random(seed);
            float headX = R(rng, 0.2f, 0.28f);
            float headR = R(rng, 0.11f, 0.14f);
            float tailEnd = R(rng, -0.47f, -0.38f);
            float bend = R(rng, -0.03f, 0.03f);
            const int sats = 3;
            var sx = new float[sats];
            var sy = new float[sats];
            var sr = new float[sats];
            for (int i = 0; i < sats; i++)
            {
                sx[i] = R(rng, headX + headR + 0.03f, 0.47f);
                sy[i] = R(rng, -0.08f, 0.08f);
                sr[i] = R(rng, 0.012f, 0.03f);
            }
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // 유닛 좌표: x -0.5..0.5, y -0.25..0.25.
                float ux = (x + 0.5f) / w - 0.5f;
                float uy = ((y + 0.5f) / h - 0.5f) * 0.5f;
                float dxh = ux - headX;
                float a = Mathf.Clamp01((headR - Mathf.Sqrt(dxh * dxh + uy * uy)) * 150f);
                if (ux > tailEnd && ux < headX)
                {
                    float k = (ux - tailEnd) / (headX - tailEnd);
                    float half = headR * Mathf.Pow(k, 1.6f);
                    float cy = bend * (1f - k) * (1f - k);
                    float t = Mathf.Clamp01((half - Mathf.Abs(uy - cy)) * 150f);
                    if (t > a) a = t;
                }
                for (int i = 0; i < sats; i++)
                {
                    float dx = ux - sx[i];
                    float dy = uy - sy[i];
                    float s = Mathf.Clamp01((sr[i] - Mathf.Sqrt(dx * dx + dy * dy)) * 150f);
                    if (s > a) a = s;
                }
                px[y * w + x] = new Color32(255, 255, 255, ToByte(a));
            }
            return ToSprite("gore_streak", w, h, px, w);
        }

        static Sprite MakeChunk(int seed)
        {
            const int size = 32;
            var rng = new System.Random(seed);
            float p1 = R(rng, 0f, 6.28f), p2 = R(rng, 0f, 6.28f), p3 = R(rng, 0f, 6.28f);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float th = Mathf.Atan2(v, u);
                float edge = 0.66f + 0.16f * Mathf.Sin(2f * th + p1) + 0.1f * Mathf.Sin(3f * th + p2) + 0.07f * Mathf.Sin(5f * th + p3);
                edge = Mathf.Min(edge, 0.95f);
                float inside = edge - r;
                float a = Mathf.Clamp01(inside * 14f);
                // 가운데 살빛(흰색 = 몸 색 그대로), 가장자리 붉음(몸 색에 곱해져 검붉어짐).
                float rim = Mathf.Clamp01(1f - inside * 4.5f);
                float speck = 0.9f + 0.1f * Mathf.Sin(u * 23f + p1) * Mathf.Sin(v * 19f + p2);
                float rr = speck;
                float gg = Mathf.Lerp(1f, 0.25f, rim * 0.9f) * speck;
                float bb = Mathf.Lerp(1f, 0.22f, rim * 0.9f) * speck;
                px[y * size + x] = new Color32(ToByte(rr), ToByte(gg), ToByte(bb), ToByte(a));
            }
            return ToSprite("gore_chunk", size, size, px, size);
        }

        static Texture2D MakeEdge()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "gore_edge",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                // 화면비로 늘여 그리므로 타원 거리. 가운데 0.5 안은 비우고 모서리로 갈수록 진하게.
                float r = Mathf.Sqrt(u * u * 0.85f + v * v);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1.25f, r));
                a = Mathf.Pow(a, 1.3f);
                px[y * size + x] = new Color32(255, 255, 255, ToByte(a));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static Sprite ToSprite(string name, int w, int h, Color32[] px, float pixelsPerUnit)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            // 사각 메쉬: 읽을 수 없는 텍스처라도 만들 수 있고, 같은 재질끼리 잘 묶인다.
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }

        static float R(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        static byte ToByte(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
    }
}
