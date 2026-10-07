using System;
using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>원화 전 임시 도형. 모두 크기 1유닛(부채꼴은 반지름 1유닛), 중심 기준, 오른쪽(+x)을 바라본다.</summary>
    public static class StyleV4ShapeSprites
    {
        static Sprite _square;
        static Sprite _circle;
        static Sprite _ring;
        static Sprite _triangle;
        static readonly Dictionary<int, Sprite> Sectors = new Dictionary<int, Sprite>();

        public static Sprite Square => _square ? _square : _square = Make(8, 8, 8, (u, v) => 1f, FilterMode.Point);

        public static Sprite Circle => _circle ? _circle : _circle = Make(128, 128, 128, (u, v) =>
        {
            float d = Mathf.Sqrt(u * u + v * v);
            return Mathf.Clamp01((1f - d) * 64f);
        });

        public static Sprite Ring => _ring ? _ring : _ring = Make(128, 128, 128, (u, v) =>
        {
            float d = Mathf.Sqrt(u * u + v * v);
            float outer = Mathf.Clamp01((1f - d) * 64f);
            float inner = Mathf.Clamp01((d - 0.84f) * 64f);
            return Mathf.Min(outer, inner);
        });

        public static Sprite Triangle => _triangle ? _triangle : _triangle = Make(128, 128, 128, (u, v) =>
        {
            // 꼭짓점 (1,0), (-0.8,0.85), (-0.8,-0.85)
            float back = u + 0.8f;
            float side = 0.85f * (1f - u) / 1.8f - Mathf.Abs(v);
            return Mathf.Clamp01(Mathf.Min(back, side) * 64f);
        });

        /// <summary>오른쪽을 가운데로 하는 부채꼴. 바깥쪽이 더 진하다.</summary>
        public static Sprite Sector(float arcDeg)
        {
            int key = Mathf.RoundToInt(arcDeg);
            if (Sectors.TryGetValue(key, out var cached) && cached) return cached;
            float half = arcDeg * 0.5f;
            var sprite = Make(256, 256, 128, (u, v) =>
            {
                float d = Mathf.Sqrt(u * u + v * v);
                if (d > 1f) return 0f;
                float ang = Mathf.Abs(Mathf.Atan2(v, u) * Mathf.Rad2Deg);
                if (ang > half) return 0f;
                float edgeFade = Mathf.Clamp01((1f - d) * 40f) * Mathf.Clamp01((half - ang) * 0.5f);
                return edgeFade * Mathf.Lerp(0.25f, 1f, d * d);
            });
            Sectors[key] = sprite;
            return sprite;
        }

        /// <summary>바닥 체크무늬(1유닛 칸). 이동감이 보이게 한다.</summary>
        public static Sprite Checker(int width, int height, Color a, Color b)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = ((x + y) & 1) == 0 ? a : b;
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 1f);
        }

        // ───────────── 다크 판타지 절차 텍스처(기획/다크판타지-분위기-1차.md '땅·벽'·'공기', 계약서 A) ─────────────
        // 원화 전 견본: 흙·돌 바닥, 돌 블록 벽, 거친 바위, 나무 버팀목, 바닥 장식(뼈·해골·사슬·나무·돌무더기·핏자국·거미줄), 불티 점.
        // 주기 원본과 장식 그림은 실행마다 같은 결과라 상태가 아닌 그림 자원 캐시다(비울 필요 없음, 위 도형과 같은 방식).
        // 칸 바닥·벽 덩이마다 새로 만드는 그림(DirtFloor·StoneWall·RoughRock)은 만든 쪽이 RuntimeAssetOwner로 지운다.

        /// <summary>바닥·벽 텍스처 해상도(유닛당 16픽셀). 카메라 크기 8(1080p에서 유닛당 약 68화소)에서 픽셀 하나가 약 4화소다.</summary>
        public const int GroundPixelsPerUnit = 16;
        /// <summary>바닥 원본 주기(256픽셀 = 16유닛).</summary>
        const int FloorPeriod = 256;
        /// <summary>벽·바위 원본 주기(128픽셀 = 8유닛).</summary>
        const int WallPeriod = 128;

        static Color32[] _floorSource;
        static Color32[] _stoneSource;
        static Color32[] _rockSource;
        static Sprite _bone;
        static Sprite _skull;
        static Sprite _chain;
        static Sprite _plank;
        static Sprite _cobweb;
        static Sprite _glow;
        static readonly Dictionary<int, Sprite> Timbers = new Dictionary<int, Sprite>();
        static readonly Dictionary<int, Sprite> Rubbles = new Dictionary<int, Sprite>();
        static readonly Dictionary<int, Sprite> Stains = new Dictionary<int, Sprite>();

        /// <summary>글자에서 실행마다 같은 시드(FNV-1a). 칸 id로 바닥 무늬·장식 자리를 정한다.</summary>
        public static int Seed(string text, int salt)
        {
            unchecked
            {
                uint h = 2166136261u ^ ((uint)salt * 16777619u);
                if (text != null)
                {
                    for (int i = 0; i < text.Length; i++)
                    {
                        h ^= text[i];
                        h *= 16777619u;
                    }
                }
                return (int)(h & 0x7FFFFFFF);
            }
        }

        /// <summary>
        /// 칸 바닥(world = 칸 월드 사각형, 유닛당 16픽셀). 흙 결·자갈은 월드 좌표에 맞춘 주기 원본에서 가져오고 큰 얼룩도 월드 격자 노이즈라
        /// 문틈에서 이웃 칸과 이음매 없이 이어진다. 칸 시드로 젖은 진흙·드러난 바닥돌·자갈 더미·그을음 자리를 칸 안쪽에만 그려 칸마다 무늬가 다르다.
        /// occluders(벽·바위 사각형) 가까이는 어둡게(구석 그늘). 빛을 받는 기본 재질로 그린다.
        /// </summary>
        public static Sprite DirtFloor(Rect world, int cellSeed, IReadOnlyList<Rect> occluders)
        {
            const int ppu = GroundPixelsPerUnit;
            int w = Mathf.Max(1, Mathf.RoundToInt(world.width * ppu));
            int h = Mathf.Max(1, Mathf.RoundToInt(world.height * ppu));
            int x0 = Mathf.RoundToInt(world.xMin * ppu);
            int y0 = Mathf.RoundToInt(world.yMin * ppu);
            var src = FloorSource();
            int count = w * h;
            var big = new float[count];
            var mid = new float[count];
            WorldNoise(big, w, h, x0, y0, 4.5f, 21);
            WorldNoise(mid, w, h, x0, y0, 1.4f, 22);
            var r = new float[count];
            var g = new float[count];
            var b = new float[count];
            var cols = new int[w];
            for (int x = 0; x < w; x++) cols[x] = Wrap(x0 + x, FloorPeriod);
            const float inv = 1f / 255f;
            for (int y = 0; y < h; y++)
            {
                int srow = Wrap(y0 + y, FloorPeriod) * FloorPeriod;
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int i = row + x;
                    var s = src[srow + cols[x]];
                    // 큰 얼룩(4.5유닛)은 밝기를 크게, 중간 결(1.4유닛)은 조금 흔든다.
                    float k = (0.74f + big[i] * 0.45f) * (0.97f + mid[i] * 0.06f);
                    r[i] = s.r * inv * k;
                    g[i] = s.g * inv * k;
                    b[i] = s.b * inv * k;
                }
            }
            PaintFloorPatches(r, g, b, mid, w, h, x0, y0, cellSeed);
            var ao = OcclusionField(world, w, h, occluders);
            var px = new Color32[count];
            for (int i = 0; i < count; i++)
            {
                float k = ao[i];
                px[i] = new Color32(ToByte(r[i] * k), ToByte(g[i] * k), ToByte(b[i] * k), 255);
            }
            return GroundSprite(px, w, h, "Dungeon floor");
        }

        /// <summary>돌 블록·모르타르 벽(월드 좌표에 맞춘 무늬). 이어진 벽 토막과 문틈을 막은 판자벽·금 간 벽이 이음매 없이 같은 벽으로 보인다(숨은 방 규칙).</summary>
        public static Sprite StoneWall(Rect world) => WorldAligned(world, StoneSource(), "Dungeon wall");

        /// <summary>통로·방을 채운 거친 바위(월드 좌표에 맞춘 무늬).</summary>
        public static Sprite RoughRock(Rect world) => WorldAligned(world, RockSource(), "Dungeon rock");

        /// <summary>
        /// 나무 버팀목 기둥 윗면(1유닛 = 48픽셀, 크기는 물체 배율로 맞춘다): 나이테 단면 + 갈라진 틈 + 깎은 모서리 + 쇠못 둘.
        /// variant 0~2마다 나이테 중심·틈 방향이 다르다. 그림자 모양이 네모가 되게 불투명하다.
        /// </summary>
        public static Sprite TimberPost(int variant)
        {
            variant = Wrap(variant, 3);
            if (Timbers.TryGetValue(variant, out var cached) && cached) return cached;
            const int n = 48;
            var px = new Color32[n * n];
            var rng = new System.Random(900 + variant);
            float cx = n * (0.38f + (float)rng.NextDouble() * 0.24f);
            float cy = n * (0.38f + (float)rng.NextDouble() * 0.24f);
            float a0 = (float)rng.NextDouble() * 360f;
            float a1 = a0 + 110f + (float)rng.NextDouble() * 70f;
            float a2 = a1 + 90f + (float)rng.NextDouble() * 60f;
            Color wood = Palette.DungeonTimber;
            Color dark = Palette.DungeonTimberDark;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                float wobble = Mathf.Sin((ang + variant * 40f) * Mathf.Deg2Rad * 3f) * 0.6f + (TileNoise(x / 6f, y / 6f, 8, 51 + variant) - 0.5f) * 1.8f;
                float ring = Mathf.Repeat((d + wobble * 0.5f) / 7.5f, 1f);
                float ringK = ring < 0.14f ? 0.5f : 0.79f + ring * 0.13f;
                var c = Color.Lerp(dark, wood, ringK);
                float k = 1f + (Hash01(x, y, 52 + variant) - 0.5f) * 0.025f;
                // 갈라진 틈: 나이테 중심에서 바깥으로 뻗는 가는 금 셋.
                if (d > 3f && (CrackNear(ang, d, a0) || CrackNear(ang, d, a1) || (CrackNear(ang, d, a2) && d < n * 0.32f))) k *= 0.6f;
                // 깎은 모서리: 바깥 1픽셀 어두운 테, 그 안 2픽셀 빗면(왼쪽·위는 밝게, 오른쪽·아래는 어둡게).
                int edge = Mathf.Min(Mathf.Min(x, n - 1 - x), Mathf.Min(y, n - 1 - y));
                if (edge == 0) k *= 0.4f;
                else if (edge <= 2) k *= x <= 2 || y >= n - 3 ? 1.25f : 0.72f;
                px[y * n + x] = new Color32(ToByte(c.r * k), ToByte(c.g * k), ToByte(c.b * k), 255);
            }
            // 쇠못 둘(대각선 모서리).
            StampNail(px, n, 7.5f, n - 7.5f);
            StampNail(px, n, n - 7.5f, 7.5f);
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Timber post" };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect);
            Timbers[variant] = sprite;
            return sprite;
        }

        /// <summary>뼈 한 대(1 × 0.25유닛): 가는 대 + 양끝 마디. 흰·회색 음영이라 색은 SpriteRenderer.color(Palette.DungeonBone)로 입힌다.</summary>
        public static Sprite Bone => _bone ? _bone : _bone = BuildBone();

        /// <summary>위에서 본 해골(0.5유닛): 둥근 머리뼈, 어두운 눈구멍·콧구멍, 이 한 줄.</summary>
        public static Sprite Skull => _skull ? _skull : _skull = BuildSkull();

        /// <summary>녹슨 사슬 한 토막(1 × 0.25유닛): 둥근 고리와 옆으로 누운 고리가 번갈아 이어진다.</summary>
        public static Sprite Chain => _chain ? _chain : _chain = BuildChain();

        /// <summary>부서진 판자(1.5 × 0.31유닛): 나뭇결, 옹이, 못 구멍, 한쪽 끝이 들쭉날쭉 부러짐.</summary>
        public static Sprite BrokenPlank => _plank ? _plank : _plank = BuildPlank();

        /// <summary>구석 거미줄(1.6유닛, 피벗 왼쪽 아래 = 구석). 오른쪽 위로 퍼진다. 돌려서 네 구석에 쓴다.</summary>
        public static Sprite Cobweb => _cobweb ? _cobweb : _cobweb = BuildCobweb();

        /// <summary>불티·먼지 점(1유닛): 밝은 심 + 부드러운 테.</summary>
        public static Sprite Glow => _glow ? _glow : _glow = BuildGlow();

        /// <summary>돌무더기(1.33유닛, variant 0~3): 크고 작은 돌 7~10개와 그 아래 그늘.</summary>
        public static Sprite Rubble(int variant)
        {
            variant = Wrap(variant, 4);
            if (Rubbles.TryGetValue(variant, out var cached) && cached) return cached;
            var sprite = BuildRubble(variant);
            Rubbles[variant] = sprite;
            return sprite;
        }

        /// <summary>오래된 핏자국(1.6유닛, variant 0~3): 들쭉날쭉한 덩이 + 튄 방울 + 끌린 자국, 마른 가장자리는 더 어둡다.</summary>
        public static Sprite OldStain(int variant)
        {
            variant = Wrap(variant, 4);
            if (Stains.TryGetValue(variant, out var cached) && cached) return cached;
            var sprite = BuildStain(variant);
            Stains[variant] = sprite;
            return sprite;
        }

        // ── 원본 만들기 ──

        /// <summary>
        /// 흙바닥 원본(256×256 = 16유닛 주기, 이어 붙여도 이음매 없음): 다져진 흙의 굵은 얼룩·잔결 + 박힌 자갈(왼쪽 위 빛, 오른쪽 아래 그늘)
        /// + 큰 납작돌 + 걸음에 갈라진 가는 금.
        /// </summary>
        static Color32[] FloorSource()
        {
            if (_floorSource != null) return _floorSource;
            const int n = FloorPeriod;
            var lum = new float[n * n];
            var warm = new float[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                float f = TileFbm(x / 64f, y / 64f, n / 64, 2, 11);
                float grain = Hash01(x, y, 12) - 0.5f;
                lum[i] = 0.48f + (f - 0.5f) * 0.7f + grain * 0.025f;
                warm[i] = TileNoise(x / 64f, y / 64f, n / 64, 13) - 0.5f;
            }
            var rng = new System.Random(1701);
            for (int k = 0; k < 10; k++)
            {
                float rx = 6f + (float)rng.NextDouble() * 4f;
                StampPebble(lum, n, (float)rng.NextDouble() * n, (float)rng.NextDouble() * n, rx, rx * (0.55f + (float)rng.NextDouble() * 0.3f),
                    0.5f + (float)rng.NextDouble() * 0.2f);
            }
            for (int k = 0; k < 48; k++)
            {
                float rx = 2f + (float)rng.NextDouble() * 3f;
                StampPebble(lum, n, (float)rng.NextDouble() * n, (float)rng.NextDouble() * n, rx, rx * (0.6f + (float)rng.NextDouble() * 0.4f),
                    0.4f + (float)rng.NextDouble() * 0.18f);
            }
            for (int k = 0; k < 9; k++)
            {
                float px = (float)rng.NextDouble() * n;
                float py = (float)rng.NextDouble() * n;
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                int len = 8 + rng.Next(30);
                for (int s = 0; s < len; s++)
                {
                    ang += ((float)rng.NextDouble() - 0.5f) * 0.9f;
                    px += Mathf.Cos(ang);
                    py += Mathf.Sin(ang);
                    lum[Wrap(Mathf.FloorToInt(py), n) * n + Wrap(Mathf.FloorToInt(px), n)] -= 0.1f;
                }
            }
            var pixels = new Color32[n * n];
            Color dark = Palette.DungeonDirtDark;
            Color light = Palette.DungeonDirtLight;
            for (int i = 0; i < n * n; i++)
            {
                var c = Color.Lerp(dark, light, Mathf.Clamp01(lum[i]));
                float wv = warm[i] * 0.14f;
                pixels[i] = new Color32(ToByte(c.r * (1f + wv)), ToByte(c.g), ToByte(c.b * (1f - wv)), 255);
            }
            _floorSource = pixels;
            return pixels;
        }

        /// <summary>둥근 돌 하나를 밝기 판에 찍는다(주기 감김). 왼쪽 위가 밝고, 오른쪽 아래로 조금 비낀 그늘을 두른다.</summary>
        static void StampPebble(float[] lum, int n, float cx, float cy, float rx, float ry, float tone)
        {
            int xa = Mathf.FloorToInt(cx - rx - 2f), xb = Mathf.CeilToInt(cx + rx + 2f);
            int ya = Mathf.FloorToInt(cy - ry - 2f), yb = Mathf.CeilToInt(cy + ry + 2f);
            float soft = Mathf.Min(rx, ry) * 1.5f;
            for (int y = ya; y <= yb; y++)
            for (int x = xa; x <= xb; x++)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                int i = Wrap(y, n) * n + Wrap(x, n);
                if (d >= 1f)
                {
                    float sx = (x + 0.5f - cx - 0.8f) / (rx + 0.8f), sy = (y + 0.5f - cy + 0.8f) / (ry + 0.8f);
                    float sd = Mathf.Sqrt(sx * sx + sy * sy);
                    if (sd < 1f) lum[i] -= 0.12f * (1f - sd);
                    continue;
                }
                float cover = Mathf.Clamp01((1f - d) * soft);
                float shade = tone + (-dx * 0.35f + dy * 0.45f) * 0.25f;
                lum[i] = Mathf.Lerp(lum[i], shade, cover);
            }
        }

        /// <summary>
        /// 칸마다 다른 자리 2~4곳(칸 가장자리 2.5유닛 안쪽): 0 젖은 진흙(자갈을 덮고 가운데 윤기), 1 드러난 바닥돌(거친 바위 결, 벽 바위로 읽히지 않게 바닥 밝기로 낮춤),
        /// 2 자갈 더미(밝고 어두운 알갱이), 3 그을음(오래된 불 자리). 가장자리는 중간 결 노이즈로 흐트러뜨린다.
        /// </summary>
        static void PaintFloorPatches(float[] r, float[] g, float[] b, float[] mid, int w, int h, int x0, int y0, int seed)
        {
            var rng = new System.Random(seed);
            int count = 2 + rng.Next(3);
            const float ppu = GroundPixelsPerUnit;
            var rock = RockSource();
            Color mud = Palette.DungeonMud;
            const float inv = 1f / 255f;
            for (int p = 0; p < count; p++)
            {
                int kind = rng.Next(4);
                float rx = (1.6f + (float)rng.NextDouble() * 2.2f) * ppu;
                float ry = (1.1f + (float)rng.NextDouble() * 1.4f) * ppu;
                float margin = 2.5f * ppu;
                float cxp = Mathf.Clamp(Mathf.Lerp(margin + rx * 0.6f, w - margin - rx * 0.6f, (float)rng.NextDouble()), 0f, w);
                float cyp = Mathf.Clamp(Mathf.Lerp(margin + ry * 0.6f, h - margin - ry * 0.6f, (float)rng.NextDouble()), 0f, h);
                float ang = (float)rng.NextDouble() * Mathf.PI;
                float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                float reach = Mathf.Max(rx, ry) * 1.4f;
                int bx0 = Mathf.Max(0, Mathf.FloorToInt(cxp - reach)), bx1 = Mathf.Min(w - 1, Mathf.CeilToInt(cxp + reach));
                int by0 = Mathf.Max(0, Mathf.FloorToInt(cyp - reach)), by1 = Mathf.Min(h - 1, Mathf.CeilToInt(cyp + reach));
                for (int y = by0; y <= by1; y++)
                for (int x = bx0; x <= bx1; x++)
                {
                    float dx = x + 0.5f - cxp, dy = y + 0.5f - cyp;
                    float u = (dx * cs + dy * sn) / rx;
                    float v = (-dx * sn + dy * cs) / ry;
                    int i = y * w + x;
                    float d = Mathf.Sqrt(u * u + v * v) + (mid[i] - 0.5f) * 0.45f;
                    float m = 1f - Mathf.Clamp01((d - 0.7f) / 0.35f);
                    if (m <= 0f) continue;
                    m = Smooth(m);
                    switch (kind)
                    {
                        case 0:
                        {
                            float sheen = 1f + 0.25f * Smooth(Mathf.Clamp01(1f - d * 1.6f));
                            float k = (0.85f + mid[i] * 0.3f) * sheen;
                            float t = m * 0.85f;
                            r[i] = Mathf.Lerp(r[i], mud.r * k, t);
                            g[i] = Mathf.Lerp(g[i], mud.g * k, t);
                            b[i] = Mathf.Lerp(b[i], mud.b * k, t);
                            break;
                        }
                        case 1:
                        {
                            var s = rock[Wrap(y0 + y, WallPeriod) * WallPeriod + Wrap(x0 + x, WallPeriod)];
                            float t = m * 0.8f;
                            r[i] = Mathf.Lerp(r[i], s.r * inv * 0.8f, t);
                            g[i] = Mathf.Lerp(g[i], s.g * inv * 0.8f, t);
                            b[i] = Mathf.Lerp(b[i], s.b * inv * 0.8f, t);
                            break;
                        }
                        case 2:
                        {
                            float hsh = Hash01((x0 + x) >> 1, (y0 + y) >> 1, seed + 7);
                            float k = hsh > 0.82f ? 1.12f : hsh < 0.18f ? 0.88f : 1f;
                            float f = Mathf.Lerp(1f, k, m * 0.9f);
                            r[i] *= f;
                            g[i] *= f;
                            b[i] *= f;
                            break;
                        }
                        default:
                        {
                            float gray = (r[i] + g[i] + b[i]) * (1f / 3f);
                            float k = Mathf.Lerp(1f, 0.5f, m * 0.85f);
                            float t = m * 0.6f;
                            r[i] = Mathf.Lerp(r[i], gray, t) * k;
                            g[i] = Mathf.Lerp(g[i], gray, t) * k;
                            b[i] = Mathf.Lerp(b[i], gray, t) * k;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>벽·바위 가까이(0.8유닛) 바닥에 접지 그늘을 만든다(최대 52%). 0.25유닛 성긴 격자에서 거리를 재고 픽셀은 격자 사이를 이어 붙인다.</summary>
        static float[] OcclusionField(Rect world, int w, int h, IReadOnlyList<Rect> occluders)
        {
            var ao = new float[w * h];
            for (int i = 0; i < ao.Length; i++) ao[i] = 1f;
            if (occluders == null || occluders.Count == 0) return ao;
            const float range = 0.8f;
            const float strength = 0.52f;
            var grow = new Rect(world.xMin - range, world.yMin - range, world.width + range * 2f, world.height + range * 2f);
            var near = new List<Rect>();
            for (int i = 0; i < occluders.Count; i++)
                if (occluders[i].Overlaps(grow)) near.Add(occluders[i]);
            if (near.Count == 0) return ao;
            const int step = 4;
            int gw = w / step + 2, gh = h / step + 2;
            var grid = new float[gw * gh];
            float unit = step / (float)GroundPixelsPerUnit;
            for (int gy = 0; gy < gh; gy++)
            for (int gx = 0; gx < gw; gx++)
            {
                float wx = world.xMin + gx * unit;
                float wy = world.yMin + gy * unit;
                float best = range;
                for (int k = 0; k < near.Count; k++)
                {
                    float d = RectDistance(near[k], wx, wy);
                    if (d < best) best = d;
                }
                grid[gy * gw + gx] = 1f - strength * (1f - Smooth(Mathf.Clamp01(best / range)));
            }
            for (int y = 0; y < h; y++)
            {
                float fy = (y + 0.5f) / step;
                int gy = (int)fy;
                float ty = fy - gy;
                for (int x = 0; x < w; x++)
                {
                    float fx = (x + 0.5f) / step;
                    int gx = (int)fx;
                    float tx = fx - gx;
                    int i0 = gy * gw + gx, i1 = i0 + gw;
                    float a = grid[i0] + (grid[i0 + 1] - grid[i0]) * tx;
                    float c = grid[i1] + (grid[i1 + 1] - grid[i1]) * tx;
                    ao[y * w + x] = a + (c - a) * ty;
                }
            }
            return ao;
        }

        static float RectDistance(Rect r, float x, float y)
        {
            float dx = Mathf.Max(Mathf.Max(r.xMin - x, 0f), x - r.xMax);
            float dy = Mathf.Max(Mathf.Max(r.yMin - y, 0f), y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 돌 블록·모르타르 벽 원본(128×128 = 8유닛 주기). 0.5유닛 높이 줄마다 폭 0.6~1.25유닛 돌을 엇갈려 쌓는다.
        /// 돌마다 밝기·색이 조금 다르고, 윗모서리는 밝게·아랫모서리는 어둡게(깎은 돌), 1픽셀 모르타르 줄, 잔결과 이 빠진 자국.
        /// 평균 밝기가 Palette.Wall(= DungeonStone)과 비슷해 벽 파편 색과 맞는다.
        /// </summary>
        static Color32[] StoneSource()
        {
            if (_stoneSource != null) return _stoneSource;
            const int n = WallPeriod;
            const int course = 16;
            const int rows = n / course;
            var pixels = new Color32[n * n];
            var stoneId = new int[n];
            var localX = new int[n];
            var stoneW = new int[n];
            var rng = new System.Random(4242);
            Color stone = Palette.DungeonStone;
            Color mortar = Palette.DungeonMortar;
            for (int row = 0; row < rows; row++)
            {
                int pos = 0, id = 0;
                while (pos < n)
                {
                    int wdt = 18 + rng.Next(17);
                    if (n - pos - wdt < 8) wdt = n - pos;
                    for (int k = 0; k < wdt; k++)
                    {
                        stoneId[pos + k] = id;
                        localX[pos + k] = k;
                        stoneW[pos + k] = wdt;
                    }
                    pos += wdt;
                    id++;
                }
                int offset = rng.Next(n);
                for (int ly = 0; ly < course; ly++)
                {
                    int y = row * course + ly;
                    for (int x = 0; x < n; x++)
                    {
                        int sx = (x + offset) % n;
                        int sid = stoneId[sx];
                        int lx = localX[sx];
                        int sw = stoneW[sx];
                        float grain = TileFbm(x / 16f, y / 16f, n / 16, 3, 33) - 0.5f;
                        if (ly == 0 || lx == 0)
                        {
                        float mk = 0.9f + grain * 0.2f;
                            pixels[y * n + x] = new Color32(ToByte(mortar.r * mk), ToByte(mortar.g * mk), ToByte(mortar.b * mk), 255);
                            continue;
                        }
                        float tone = 0.98f + Hash01(row, sid, 31) * 0.30f;
                        float bevel = 0f;
                        if (ly >= course - 2) bevel += 0.25f;
                        else if (ly <= 2) bevel -= 0.25f;
                        if (lx <= 2) bevel += 0.10f;
                        else if (lx >= sw - 2) bevel -= 0.16f;
                        float speck = (Hash01(x, y, 34) - 0.5f) * 0.018f;
                        float chip = Hash01(x >> 1, y >> 1, 35) > 0.985f ? -0.12f : 0f;
                        float k2 = tone + bevel + grain * 0.14f + speck + chip;
                        float hue = (Hash01(row, sid, 36) - 0.5f) * 0.08f;
                        pixels[y * n + x] = new Color32(ToByte(stone.r * (1f + hue) * k2), ToByte(stone.g * k2), ToByte(stone.b * (1f - hue) * k2), 255);
                    }
                }
            }
            _stoneSource = pixels;
            return pixels;
        }

        /// <summary>
        /// 거친 바위 원본(128×128 = 8유닛 주기): 1유닛 격자마다 흔든 점으로 나눈 덩이(보로노이). 덩이 사이 틈은 어둡고,
        /// 덩이마다 밝기가 다르며 가운데가 불룩하게 왼쪽 위에서 빛을 받는다.
        /// </summary>
        static Color32[] RockSource()
        {
            if (_rockSource != null) return _rockSource;
            const int n = WallPeriod;
            const int cells = 4;
            const float cellPx = n / (float)cells;
            var pixels = new Color32[n * n];
            Color rock = Palette.DungeonRock;
            Color crack = Palette.DungeonRockCrack;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float fx = (x + 0.5f) / cellPx, fy = (y + 0.5f) / cellPx;
                int ci = (int)fx, cj = (int)fy;
                float f1 = 9f, f2 = 9f, ox = 0f, oy = 0f;
                int id = 0;
                for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    int gi = ci + di, gj = cj + dj;
                    int wi = Wrap(gi, cells), wj = Wrap(gj, cells);
                    float fpx = gi + 0.12f + 0.76f * Hash01(wi, wj, 41);
                    float fpy = gj + 0.12f + 0.76f * Hash01(wi, wj, 42);
                    float dx = fx - fpx, dy = fy - fpy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d < f1)
                    {
                        f2 = f1;
                        f1 = d;
                        id = wj * cells + wi;
                        ox = dx;
                        oy = dy;
                    }
                    else if (d < f2) f2 = d;
                }
                float crackK = 1f - Smooth(Mathf.Clamp01((f2 - f1 - 0.03f) / 0.1f));
                float tone = 0.88f + Hash01(id, 0, 43) * 0.25f;
                float dome = 1.1f - f1 * 0.35f + (oy * 0.45f - ox * 0.3f) * 0.62f;
                float grain = TileFbm(x / 8f, y / 8f, n / 8, 3, 44) - 0.5f;
                float k = tone * dome + grain * 0.12f + (Hash01(x, y, 45) - 0.5f) * 0.018f;
                float t = crackK * 0.85f;
                pixels[y * n + x] = new Color32(
                    ToByte(Mathf.Lerp(rock.r * k, crack.r, t)),
                    ToByte(Mathf.Lerp(rock.g * k, crack.g, t)),
                    ToByte(Mathf.Lerp(rock.b * k, crack.b, t)), 255);
            }
            _rockSource = pixels;
            return pixels;
        }

        /// <summary>월드 좌표 그대로 주기 원본을 잘라 붙인 그림(유닛당 16픽셀). 이웃한 덩이끼리 무늬가 이어진다.</summary>
        static Sprite WorldAligned(Rect world, Color32[] src, string name)
        {
            const int ppu = GroundPixelsPerUnit;
            int w = Mathf.Max(1, Mathf.RoundToInt(world.width * ppu));
            int h = Mathf.Max(1, Mathf.RoundToInt(world.height * ppu));
            int x0 = Mathf.RoundToInt(world.xMin * ppu);
            int y0 = Mathf.RoundToInt(world.yMin * ppu);
            var px = new Color32[w * h];
            var cols = new int[w];
            for (int x = 0; x < w; x++) cols[x] = Wrap(x0 + x, WallPeriod);
            for (int y = 0; y < h; y++)
            {
                int srow = Wrap(y0 + y, WallPeriod) * WallPeriod;
                int row = y * w;
                for (int x = 0; x < w; x++) px[row + x] = src[srow + cols[x]];
            }
            return GroundSprite(px, w, h, name);
        }

        static Sprite GroundSprite(Color32[] px, int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = name };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), GroundPixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }

        static bool CrackNear(float angDeg, float dist, float crackDeg)
        {
            float off = Mathf.Abs(Mathf.DeltaAngle(angDeg, crackDeg)) * Mathf.Deg2Rad * dist;
            return off < 0.7f;
        }

        static void StampNail(Color32[] px, int n, float cx, float cy)
        {
            for (int y = Mathf.FloorToInt(cy - 3f); y <= Mathf.CeilToInt(cy + 3f); y++)
            for (int x = Mathf.FloorToInt(cx - 3f); x <= Mathf.CeilToInt(cx + 3f); x++)
            {
                if (x < 0 || y < 0 || x >= n || y >= n) continue;
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 2.3f) continue;
                float k = 0.16f + Mathf.Clamp01(-dx * 0.25f + dy * 0.3f) * 0.22f;
                px[y * n + x] = new Color32(ToByte(k), ToByte(k * 0.95f), ToByte(k * 0.9f), 255);
            }
        }

        static Sprite BuildBone()
        {
            const int w = 64, h = 16;
            var shade = new float[w * h];
            var alpha = new float[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float shaft = SegmentDistance(px, py, 10f, 8f, 54f, 8f) - 2.6f;
                float k1 = Mathf.Min(CircleDistance(px, py, 7f, 5f, 3.6f), CircleDistance(px, py, 7f, 11f, 3.6f));
                float k2 = Mathf.Min(CircleDistance(px, py, 57f, 5f, 3.6f), CircleDistance(px, py, 57f, 11f, 3.6f));
                float sdf = Mathf.Min(shaft, Mathf.Min(k1, k2));
                float a = Mathf.Clamp01(0.5f - sdf);
                if (a <= 0f) continue;
                int i = y * w + x;
                float ny = (py - 8f) / 6f;
                float s = 0.8f + ny * 0.2f - Mathf.Abs(ny) * 0.12f + (Hash01(x, y, 61) - 0.5f) * 0.08f;
                if (px < 11f || px > 53f) s *= 0.92f;
                shade[i] = s;
                alpha[i] = a;
            }
            return GraySprite(shade, alpha, w, h, 64f, new Vector2(0.5f, 0.5f), "Bone");
        }

        static Sprite BuildSkull()
        {
            const int n = 48;
            var shade = new float[n * n];
            var alpha = new float[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float cranium = EllipseDistance(px, py, 24f, 28f, 15f, 14f);
                float face = BoxDistance(px, py, 24f, 15f, 9f, 7f) - 2.5f;
                float sdf = Mathf.Min(cranium, face);
                float a = Mathf.Clamp01(0.5f - sdf);
                if (a <= 0f) continue;
                int i = y * n + x;
                float nx = (px - 24f) / 15f, ny = (py - 24f) / 16f;
                float s = 0.82f + (ny * 0.7f - nx * 0.3f) * 0.22f + (Hash01(x, y, 62) - 0.5f) * 0.07f;
                if (EllipseDistance(px, py, 18f, 20f, 4.2f, 3.6f) < 0f || EllipseDistance(px, py, 30f, 20f, 4.2f, 3.6f) < 0f) s = 0.12f;
                else if (EllipseDistance(px, py, 24f, 13.5f, 1.8f, 2.6f) < 0f) s = 0.18f;
                else if (py > 8f && py < 10.5f && px > 17f && px < 31f) s = x % 3 == 0 ? 0.3f : 0.9f;
                // 금 간 자국 하나.
                if (SegmentDistance(px, py, 27f, 38f, 33f, 31f) < 0.5f) s *= 0.55f;
                shade[i] = s;
                alpha[i] = a;
            }
            return GraySprite(shade, alpha, n, n, 96f, new Vector2(0.5f, 0.5f), "Skull");
        }

        static Sprite BuildChain()
        {
            const int w = 96, h = 24;
            const float cy = 12f;
            var shade = new float[w * h];
            var alpha = new float[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float bestBar = 99f, bestRing = 99f;
                for (int k = 0; k < 7; k++)
                {
                    float lcx = 8f + k * 13f;
                    if ((k & 1) == 0)
                    {
                        float outer = EllipseDistance(px, py, lcx, cy, 7.5f, 6f);
                        float inner = EllipseDistance(px, py, lcx, cy, 4.2f, 2.6f);
                        bestRing = Mathf.Min(bestRing, Mathf.Max(outer, -inner));
                    }
                    else bestBar = Mathf.Min(bestBar, SegmentDistance(px, py, lcx - 6f, cy, lcx + 6f, cy) - 2.2f);
                }
                float aBar = Mathf.Clamp01(0.5f - bestBar);
                float aRing = Mathf.Clamp01(0.5f - bestRing);
                float a = Mathf.Max(aBar, aRing);
                if (a <= 0f) continue;
                int i = y * w + x;
                float ny = (py - cy) / (aBar > 0f ? 2.2f : 6f);
                float s = 0.62f + Mathf.Clamp(ny, -1f, 1f) * 0.28f + (Hash01(x, y, 63) - 0.5f) * 0.3f;
                if (aBar > 0f && aRing > 0f) s *= 1.08f;
                shade[i] = s;
                alpha[i] = a;
            }
            return GraySprite(shade, alpha, w, h, 96f, new Vector2(0.5f, 0.5f), "Chain");
        }

        static Sprite BuildPlank()
        {
            const int w = 96, h = 20;
            var shade = new float[w * h];
            var alpha = new float[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                if (py < 3f || py > 17f || px < 2f) continue;
                // 부러진 끝: 줄마다 들쭉날쭉, 가운데 가시 하나가 길게 남는다.
                float jag = 76f + Hash01(y >> 1, 0, 71) * 14f + (y == 10 ? 6f : 0f);
                if (px > jag) continue;
                int i = y * w + x;
                float lane = py * 0.42f + (TileNoise(px / 10f, py / 4f, 16, 72) - 0.5f) * 1.2f;
                float grain = Mathf.Repeat(lane, 1f);
                float s = 0.78f + (grain < 0.16f ? -0.25f : 0f) + (Hash01(x, y, 73) - 0.5f) * 0.1f;
                float knot = Mathf.Sqrt((px - 30f) * (px - 30f) + (py - 10f) * (py - 10f) * 2.2f);
                if (knot < 3.2f) s = 0.45f + Mathf.Repeat(knot, 1.2f) * 0.25f;
                if (y == 3) s *= 0.7f;
                else if (y == 16) s *= 1.12f;
                if (CircleDistance(px, py, 8f, 6.5f, 0.9f) < 0f || CircleDistance(px, py, 8f, 13.5f, 0.9f) < 0f) s = 0.15f;
                if (px > jag - 2f) s *= 0.75f;
                shade[i] = s;
                alpha[i] = 1f;
            }
            return GraySprite(shade, alpha, w, h, 64f, new Vector2(0.5f, 0.5f), "Broken plank");
        }

        static Sprite BuildRubble(int variant)
        {
            const int n = 64;
            var shade = new float[n * n];
            var alpha = new float[n * n];
            var rng = new System.Random(500 + variant);
            int count = 7 + rng.Next(4);
            float[] sx = new float[count], sy = new float[count], sr = new float[count], st = new float[count], p1 = new float[count], p2 = new float[count];
            for (int k = 0; k < count; k++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = (float)rng.NextDouble() * 17f;
                sx[k] = 32f + Mathf.Cos(a) * d;
                sy[k] = 32f + Mathf.Sin(a) * d;
                sr[k] = Mathf.Lerp(11f, 4f, Mathf.Clamp01(d / 17f)) * (0.7f + (float)rng.NextDouble() * 0.45f);
                st[k] = 0.6f + (float)rng.NextDouble() * 0.4f;
                p1[k] = (float)rng.NextDouble() * 6.28f;
                p2[k] = (float)rng.NextDouble() * 6.28f;
            }
            for (int k = 0; k < count; k++)
            {
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    int i = y * n + x;
                    float dx = px - sx[k], dy = py - sy[k];
                    float ang = Mathf.Atan2(dy, dx);
                    float rr = sr[k] * (1f + 0.16f * Mathf.Sin(3f * ang + p1[k]) + 0.08f * Mathf.Sin(5f * ang + p2[k]));
                    // 그늘(오른쪽 아래로 비낌).
                    float sdx = dx - 1.6f, sdy = dy + 1.6f;
                    float sd = Mathf.Sqrt(sdx * sdx + sdy * sdy) - rr - 0.6f;
                    float shadowA = Mathf.Clamp01(0.5f - sd) * 0.45f;
                    if (shadowA > 0f)
                    {
                        shade[i] = Mathf.Lerp(shade[i], 0.08f, shadowA);
                        alpha[i] = shadowA + alpha[i] * (1f - shadowA);
                    }
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - rr;
                    float cover = Mathf.Clamp01(0.5f - d);
                    if (cover <= 0f) continue;
                    float s = st[k] * (0.82f + (-dx * 0.4f + dy * 0.6f) / Mathf.Max(1f, rr) * 0.3f) + (Hash01(x, y, 74 + k) - 0.5f) * 0.12f;
                    shade[i] = Mathf.Lerp(shade[i], s, cover);
                    alpha[i] = cover + alpha[i] * (1f - cover);
                }
            }
            return GraySprite(shade, alpha, n, n, 48f, new Vector2(0.5f, 0.5f), "Rubble");
        }

        static Sprite BuildStain(int variant)
        {
            const int n = 64;
            var shade = new float[n * n];
            var alpha = new float[n * n];
            var rng = new System.Random(300 + variant);
            float radius = 13f + (float)rng.NextDouble() * 5f;
            float c0x = 32f + ((float)rng.NextDouble() - 0.5f) * 6f, c0y = 32f + ((float)rng.NextDouble() - 0.5f) * 6f;
            float q1 = (float)rng.NextDouble() * 6.28f, q2 = (float)rng.NextDouble() * 6.28f, q3 = (float)rng.NextDouble() * 6.28f;
            int drops = 8 + rng.Next(7);
            float[] dxs = new float[drops], dys = new float[drops], drs = new float[drops];
            for (int k = 0; k < drops; k++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = radius * (1.05f + (float)rng.NextDouble() * 0.65f);
                dxs[k] = c0x + Mathf.Cos(a) * d;
                dys[k] = c0y + Mathf.Sin(a) * d;
                drs[k] = 0.8f + (float)rng.NextDouble() * 2.4f;
            }
            float smear = (float)rng.NextDouble() * Mathf.PI * 2f;
            float sex = c0x + Mathf.Cos(smear) * radius * 1.75f, sey = c0y + Mathf.Sin(smear) * radius * 1.75f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float dx = px - c0x, dy = py - c0y;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                float rr = radius * (1f + 0.14f * Mathf.Sin(2f * ang + q1) + 0.1f * Mathf.Sin(3f * ang + q2) + 0.06f * Mathf.Sin(7f * ang + q3));
                float cover = Mathf.Clamp01((rr - d) / 1.2f + 0.5f);
                // 끌린 자국: 가운데에서 바깥으로 가늘어지는 꼬리.
                float along = Mathf.Clamp01(((px - c0x) * (sex - c0x) + (py - c0y) * (sey - c0y)) / ((sex - c0x) * (sex - c0x) + (sey - c0y) * (sey - c0y)));
                float tail = SegmentDistance(px, py, c0x, c0y, sex, sey) - Mathf.Lerp(radius * 0.35f, 0.6f, along);
                cover = Mathf.Max(cover, Mathf.Clamp01(0.5f - tail));
                for (int k = 0; k < drops; k++)
                    cover = Mathf.Max(cover, Mathf.Clamp01(0.5f - CircleDistance(px, py, dxs[k], dys[k], drs[k])));
                if (cover <= 0f) continue;
                int i = y * n + x;
                float edge = Mathf.Clamp01((rr - d) / 3f);
                float s = Mathf.Lerp(0.7f, 0.95f, edge) + (TileNoise(px / 5f, py / 5f, 13, 81 + variant) - 0.5f) * 0.16f;
                shade[i] = s;
                alpha[i] = cover * (0.62f + 0.3f * Mathf.Clamp01(1f - d / (radius * 1.7f)));
            }
            return GraySprite(shade, alpha, n, n, 40f, new Vector2(0.5f, 0.5f), "Old stain");
        }

        static Sprite BuildCobweb()
        {
            const int n = 64;
            var shade = new float[n * n];
            var alpha = new float[n * n];
            for (int i = 0; i < shade.Length; i++) shade[i] = 1f;
            var rng = new System.Random(77);
            const int strands = 8;
            var angles = new float[strands];
            var lengths = new float[strands];
            for (int k = 0; k < strands; k++)
            {
                float a = k / (float)(strands - 1) * 90f + ((float)rng.NextDouble() - 0.5f) * 8f;
                angles[k] = Mathf.Clamp(a, 2f, 88f) * Mathf.Deg2Rad;
                lengths[k] = 52f + (float)rng.NextDouble() * 9f;
                WebLine(alpha, n, 0.5f, 0.5f, Mathf.Cos(angles[k]) * lengths[k], Mathf.Sin(angles[k]) * lengths[k]);
            }
            for (int j = 1; j <= 7; j++)
            {
                float rj = 7.2f * j + ((float)rng.NextDouble() - 0.5f) * 2f;
                for (int k = 0; k < strands - 1; k++)
                {
                    if (rng.NextDouble() < 0.12) continue;
                    if (rj > lengths[k] || rj > lengths[k + 1]) continue;
                    float ax = Mathf.Cos(angles[k]) * rj, ay = Mathf.Sin(angles[k]) * rj;
                    float bx = Mathf.Cos(angles[k + 1]) * rj, by = Mathf.Sin(angles[k + 1]) * rj;
                    float mx = (ax + bx) * 0.5f * 0.92f, my = (ay + by) * 0.5f * 0.92f;
                    WebLine(alpha, n, ax, ay, mx, my);
                    WebLine(alpha, n, mx, my, bx, by);
                }
            }
            return GraySprite(shade, alpha, n, n, 40f, Vector2.zero, "Cobweb");
        }

        /// <summary>거미줄 한 가닥(두께 약 0.6픽셀). 구석에서 멀수록 옅다.</summary>
        static void WebLine(float[] alpha, int n, float ax, float ay, float bx, float by)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ax, bx) - 2f)), x1 = Mathf.Min(n - 1, Mathf.CeilToInt(Mathf.Max(ax, bx) + 2f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ay, by) - 2f)), y1 = Mathf.Min(n - 1, Mathf.CeilToInt(Mathf.Max(ay, by) + 2f));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float d = SegmentDistance(px, py, ax, ay, bx, by);
                float cover = Mathf.Clamp01(0.8f - d);
                if (cover <= 0f) continue;
                float fade = Mathf.Clamp01(1f - Mathf.Sqrt(px * px + py * py) / 66f);
                int i = y * n + x;
                alpha[i] = Mathf.Max(alpha[i], cover * (0.35f + 0.65f * fade));
            }
        }

        static Sprite BuildGlow()
        {
            const int n = 32;
            var shade = new float[n * n];
            var alpha = new float[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                float halo = Mathf.Clamp01(1f - d);
                float core = Mathf.Clamp01((0.38f - d) * 6f);
                int i = y * n + x;
                shade[i] = 1f;
                alpha[i] = Mathf.Max(halo * halo * 0.75f, core);
            }
            return GraySprite(shade, alpha, n, n, n, new Vector2(0.5f, 0.5f), "Glow");
        }

        /// <summary>흰·회색 음영 + 투명도 그림. 투명한 칸도 밝은 회색으로 채워 테두리가 검게 번지지 않게 한다.</summary>
        static Sprite GraySprite(float[] shade, float[] alpha, int w, int h, float ppu, Vector2 pivot, string name)
        {
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                float a = Mathf.Clamp01(alpha[i]);
                byte s = a > 0.001f ? ToByte(shade[i]) : (byte)180;
                px[i] = new Color32(s, s, s, ToByte(a));
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = name };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, ppu, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }

        // ── 노이즈·거리 도움 함수 ──

        static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        static float Smooth(float t) => t * t * (3f - 2f * t);

        static int Wrap(int a, int m)
        {
            int r = a % m;
            return r < 0 ? r + m : r;
        }

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

        static float Hash01(int x, int y, int seed) => (Hash(x, y, seed) >> 8) * (1f / 16777216f);

        /// <summary>주기 있는 값 노이즈(격자 period칸마다 되풀이, 0~1).</summary>
        static float TileNoise(float x, float y, int period, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float tx = Smooth(x - xi), ty = Smooth(y - yi);
            int xa = Wrap(xi, period), xb = Wrap(xi + 1, period);
            int ya = Wrap(yi, period), yb = Wrap(yi + 1, period);
            float a = Hash01(xa, ya, seed), b = Hash01(xb, ya, seed);
            float c = Hash01(xa, yb, seed), d = Hash01(xb, yb, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        /// <summary>주기 fBm(가장 굵은 격자 period칸, 옥타브마다 두 배로 잘게, 0~1).</summary>
        static float TileFbm(float x, float y, int period, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += TileNoise(x, y, period, seed + o * 131) * amp;
                norm += amp;
                x *= 2f;
                y *= 2f;
                period *= 2;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        /// <summary>월드 격자 값 노이즈를 픽셀 판(왼쪽 아래 픽셀 x0px, y0px)에 채운다. 격자 값이 월드 좌표로 정해져 칸 경계에서 이어진다.</summary>
        static void WorldNoise(float[] dst, int w, int h, int x0px, int y0px, float latticeUnits, int seed)
        {
            float step = 1f / (GroundPixelsPerUnit * latticeUnits);
            float fx0 = (x0px + 0.5f) * step, fy0 = (y0px + 0.5f) * step;
            int lx0 = Mathf.FloorToInt(fx0), ly0 = Mathf.FloorToInt(fy0);
            int lw = Mathf.FloorToInt(fx0 + w * step) - lx0 + 2;
            int lh = Mathf.FloorToInt(fy0 + h * step) - ly0 + 2;
            var lat = new float[lw * lh];
            for (int j = 0; j < lh; j++)
            for (int i = 0; i < lw; i++)
                lat[j * lw + i] = Hash01(lx0 + i, ly0 + j, seed);
            var ix = new int[w];
            var tx = new float[w];
            for (int x = 0; x < w; x++)
            {
                float f = fx0 + x * step - lx0;
                int i = Mathf.Clamp((int)f, 0, lw - 2);
                ix[x] = i;
                tx[x] = Smooth(Mathf.Clamp01(f - i));
            }
            for (int y = 0; y < h; y++)
            {
                float f = fy0 + y * step - ly0;
                int j = Mathf.Clamp((int)f, 0, lh - 2);
                float ty = Smooth(Mathf.Clamp01(f - j));
                int r0 = j * lw, r1 = r0 + lw;
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int i = ix[x];
                    float t = tx[x];
                    float a = lat[r0 + i] + (lat[r0 + i + 1] - lat[r0 + i]) * t;
                    float c = lat[r1 + i] + (lat[r1 + i + 1] - lat[r1 + i]) * t;
                    dst[row + x] = a + (c - a) * ty;
                }
            }
        }

        static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float abx = bx - ax, aby = by - ay;
            float t = Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / Mathf.Max(1e-5f, abx * abx + aby * aby));
            float dx = px - (ax + abx * t), dy = py - (ay + aby * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static float CircleDistance(float px, float py, float cx, float cy, float r)
        {
            float dx = px - cx, dy = py - cy;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        /// <summary>타원 거리 근사(픽셀 단위, 안쪽 음수).</summary>
        static float EllipseDistance(float px, float py, float cx, float cy, float rx, float ry)
        {
            float u = (px - cx) / rx, v = (py - cy) / ry;
            return (Mathf.Sqrt(u * u + v * v) - 1f) * Mathf.Min(rx, ry);
        }

        static float BoxDistance(float px, float py, float cx, float cy, float hx, float hy)
        {
            float dx = Mathf.Abs(px - cx) - hx, dy = Mathf.Abs(py - cy) - hy;
            float ox = Mathf.Max(dx, 0f), oy = Mathf.Max(dy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f);
        }

        /// <param name="alpha">u, v는 -1..1 정규화 좌표.</param>
        static Sprite Make(int width, int height, float pixelsPerUnit, Func<float, float, float> alpha, FilterMode filter = FilterMode.Bilinear)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = filter, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width * 2f - 1f;
                float v = (y + 0.5f) / height * 2f - 1f;
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(u, v)) * 255f);
                pixels[y * width + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }
    }
}
