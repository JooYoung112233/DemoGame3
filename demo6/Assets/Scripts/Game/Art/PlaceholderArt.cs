using System.Collections.Generic;
using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 시험용 그림(코드로 그림). 교체 자리가 동작하는지, 타격 프레임이 판정 순간에 맞는지 보는 용도이고 최종 그림이 아니다.
    /// 프레임 수와 타격 프레임 번호는 명세의 권장값(12fps 기준)과 같다. 칼날이 노란 프레임이 타격 프레임이다.
    /// 모든 그림은 오른쪽을 본다. 64 PPU.
    /// </summary>
    public static class PlaceholderArt
    {
        const float Ppu = 64f;
        static readonly Color Body = new Color(0.91f, 0.89f, 0.84f);
        static readonly Color Outline = new Color(0.25f, 0.22f, 0.2f);
        static readonly Color Blade = new Color(0.72f, 0.74f, 0.78f);
        static readonly Color KeyBlade = new Color(1f, 0.89f, 0.36f);
        static readonly List<Object> Made = new List<Object>();

        public static CombatArtSet Build()
        {
            var set = ScriptableObject.CreateInstance<CombatArtSet>();
            set.name = "시험용 그림";
            set.player = BuildPlayer();
            var weapons = new List<WeaponArt>();
            foreach (var w in WeaponPresets.All) weapons.Add(BuildWeapon(w));
            set.weapons = weapons.ToArray();
            set.rat = BuildEnemy(MonsterRule.Rat, 48, 48);
            set.boar = BuildEnemy(MonsterRule.Boar, 96, 96);
            set.archer = BuildEnemy(MonsterRule.Archer, 72, 96);
            set.effects = new EffectArt
            {
                sparks = new[] { Spark(), Spark(true) },
                slashMark = SlashMark(),
            };
            return set;
        }

        // ---------------- 검사 ----------------

        static PlayerArt BuildPlayer()
        {
            var art = new PlayerArt();
            art.idle = Clip(4f, -1, Frame(0f, 0f, false, 0f), Frame(0f, 0f, false, 0.03f));
            art.move = Clip(10f, -1, Frame(-20f, 0f, false, 0.05f), Frame(-10f, 0f, false, 0f), Frame(-20f, 0f, false, -0.05f), Frame(-10f, 0f, false, 0f));
            art.dodge = Clip(12f, -1, Squash(0.7f), Squash(0.55f), Squash(0.8f));
            var whirl = new List<Sprite>();
            for (int i = 0; i < 7; i++) whirl.Add(Frame(-90f + i * 60f, 1.0f, i == 1, 0f));
            art.whirl = Clip(12f, 1, whirl.ToArray());
            art.waveCast = Clip(12f, -1, Frame(150f, 1.0f, false, 0f), Frame(110f, 1.0f, false, 0f), Frame(0f, 1.3f, true, 0f));
            art.hurt = Clip(12f, -1, Frame(160f, 0.8f, false, 0f, new Color(1f, 0.6f, 0.6f)));
            art.down = Clip(12f, -1, Squash(0.45f));
            return art;
        }

        static WeaponArt BuildWeapon(WeaponAttackRule weapon)
        {
            var steps = new List<StepArt>();
            for (int i = 0; i < weapon.combo.Length; i++)
            {
                var s = weapon.combo[i];
                double hit = s.duration * s.hitMoment;
                ClipTiming.Recommend(s.duration, hit, out int frames, out int key);
                bool reverse = i % 2 == 1;
                var body = new List<Sprite>();
                for (int f = 0; f < frames; f++) body.Add(StepFrame(weapon, s, f, frames, key, reverse));
                steps.Add(new StepArt
                {
                    body = Clip(12f, key, body.ToArray()),
                    slash = SlashClip(s),
                    slashScale = 1f,
                    mirrorAlternate = s.shape == ComboShape.Arc,
                });
            }
            return new WeaponArt { weaponId = weapon.id, steps = steps.ToArray() };
        }

        /// <summary>칼날 각도: 준비(뒤로 젖힘) → 타격(앞) → 휘두른 뒤. 찌르기는 앞으로 뻗고, 회전베기는 한 바퀴.</summary>
        static Sprite StepFrame(WeaponAttackRule weapon, ComboStep s, int f, int frames, int key, bool reverse)
        {
            bool isKey = f == key;
            float sign = reverse ? -1f : 1f;
            bool twin = weapon.id == WeaponPresets.Twinblades.id;
            float length = BladeLength(weapon.id);
            if (s.shape == ComboShape.Line)
            {
                float reach = f < key ? Mathf.Lerp(0.5f, 0.35f, f / (float)Mathf.Max(1, key)) : f == key ? 1.45f : Mathf.Lerp(1.3f, 0.8f, (f - key) / (float)Mathf.Max(1, frames - key - 1));
                return Frame(0f, reach, isKey, 0f);
            }
            if (s.shape == ComboShape.Circle && s.centerOffset <= 0.01f)
            {
                float spin = f < key ? -60f * f / Mathf.Max(1, key) : (f - key) * (360f / Mathf.Max(1, frames - key));
                return Frame(spin, length, isKey, 0f, null, twin);
            }
            float angle;
            if (f < key) angle = Mathf.Lerp(110f, 140f, f / (float)Mathf.Max(1, key)) * sign;
            else if (f == key) angle = 0f;
            else angle = Mathf.Lerp(-55f, -80f, (f - key) / (float)Mathf.Max(1, frames - key - 1)) * sign;
            if (s.shape == ComboShape.Circle) angle = f < key ? 90f : f == key ? 0f : -10f;
            return Frame(angle, length, isKey, 0f, null, twin);
        }

        /// <summary>
        /// 옆모습 시험 그림의 무기 막대 길이(유닛). 기존 3종은 예전 값 그대로(대검 1.25, 쌍검 0.8, 장검 1.05)이고,
        /// 새 무기 6종은 정수리 그림 길이(기획/전투-보스-무기-다듬기-1차.md 2-7)에 비례해 정한다: 창이 가장 길고 단검이 가장 짧다.
        /// </summary>
        static float BladeLength(string weaponId)
        {
            switch (weaponId)
            {
                case "wpn_greatsword": return 1.25f;
                case "wpn_twinblades": return 0.8f;
                case "wpn_maul": return 1.2f;
                case "wpn_spear": return 1.6f;
                case "wpn_scythe": return 1.35f;
                case "wpn_axe": return 1.0f;
                case "wpn_dagger": return 0.55f;
                case "wpn_flail": return 1.1f;
                default: return 1.05f;
            }
        }

        static Sprite Frame(float bladeAngle, float bladeLength, bool key, float bob, Color? bodyTint = null, bool twin = false)
        {
            var c = new Canvas(128, 128);
            float cx = 64f, cy = 52f + bob * Ppu;
            // 그림자
            c.Ellipse(64f, 40f, 22f, 7f, new Color(0f, 0f, 0f, 0.25f));
            // 몸통과 머리
            c.Ellipse(cx, cy, 20f, 24f, Outline);
            c.Ellipse(cx, cy, 17f, 21f, bodyTint ?? Body);
            c.Circle(cx + 3f, cy + 26f, 13f, Outline);
            c.Circle(cx + 3f, cy + 26f, 10.5f, bodyTint ?? Body);
            c.Circle(cx + 8f, cy + 28f, 2.2f, Outline);
            if (bladeLength > 0f)
            {
                DrawBlade(c, cx, cy + 4f, bladeAngle, bladeLength, key);
                if (twin) DrawBlade(c, cx - 4f, cy, bladeAngle + 25f, bladeLength * 0.9f, key);
            }
            return c.ToSprite(new Vector2(0.5f, 40f / 128f));
        }

        static void DrawBlade(Canvas c, float x, float y, float angle, float length, bool key)
        {
            float r = angle * Mathf.Deg2Rad;
            float ex = x + Mathf.Cos(r) * length * Ppu;
            float ey = y + Mathf.Sin(r) * length * Ppu;
            c.Line(x, y, ex, ey, key ? 6f : 4f, Outline);
            c.Line(x, y, ex, ey, key ? 4f : 2.5f, key ? KeyBlade : Blade);
        }

        static Sprite Squash(float height)
        {
            var c = new Canvas(128, 128);
            c.Ellipse(64f, 40f, 24f, 7f, new Color(0f, 0f, 0f, 0.25f));
            c.Ellipse(64f, 40f + 24f * height, 24f, 24f * height, Outline);
            c.Ellipse(64f, 40f + 24f * height, 21f, 21f * height, Body);
            return c.ToSprite(new Vector2(0.5f, 40f / 128f));
        }

        // ---------------- 베기 이펙트 ----------------

        static SpriteClip SlashClip(ComboStep s)
        {
            var frames = new List<Sprite>();
            switch (s.shape)
            {
                case ComboShape.Line:
                {
                    int w = Mathf.CeilToInt(s.size * Ppu) + 8;
                    int h = Mathf.CeilToInt(s.width * Ppu) + 8;
                    for (int i = 0; i < 3; i++)
                    {
                        var c = new Canvas(w, h);
                        float len = (w - 8) * (i == 0 ? 0.7f : 1f);
                        float a = i == 2 ? 0.45f : 0.9f;
                        c.Taper(4f, h * 0.5f, len, (h - 8) * (i == 0 ? 0.35f : 0.5f), new Color(1f, 1f, 1f, a));
                        frames.Add(c.ToSprite(new Vector2(4f / w, 0.5f)));
                    }
                    break;
                }
                case ComboShape.Circle:
                {
                    int size = Mathf.CeilToInt(s.size * 2f * Ppu) + 8;
                    for (int i = 0; i < 3; i++)
                    {
                        var c = new Canvas(size, size);
                        float r = (size - 8) * 0.5f * (0.6f + 0.2f * i);
                        c.Ring(size * 0.5f, size * 0.5f, r, 6f - i, new Color(1f, 1f, 1f, 0.9f - 0.25f * i));
                        if (i == 0) c.Circle(size * 0.5f, size * 0.5f, r * 0.9f, new Color(1f, 1f, 1f, 0.25f));
                        frames.Add(c.ToSprite(new Vector2(0.5f, 0.5f)));
                    }
                    break;
                }
                default:
                {
                    int size = Mathf.CeilToInt(s.size * 2f * Ppu) + 8;
                    for (int i = 0; i < 3; i++)
                    {
                        var c = new Canvas(size, size);
                        float sweep = s.arcDeg * (i == 0 ? 0.55f : 1f);
                        c.Sector(size * 0.5f, size * 0.5f, (size - 8) * 0.5f * 0.55f, (size - 8) * 0.5f, -sweep * 0.5f, sweep * 0.5f, new Color(1f, 1f, 1f, i == 2 ? 0.35f : 0.75f));
                        frames.Add(c.ToSprite(new Vector2(0.5f, 0.5f)));
                    }
                    break;
                }
            }
            return Clip(24f, -1, frames.ToArray());
        }

        // ---------------- 몬스터 ----------------

        static EnemyArt BuildEnemy(MonsterRule rule, int w, int h)
        {
            Color tint = rule.Kind == MonsterKind.Rat ? Palette.Rat : rule.Kind == MonsterKind.Boar ? Palette.Boar : Palette.Archer;
            var art = new EnemyArt
            {
                idle = Clip(4f, -1, Monster(rule, w, h, tint, 1f, 1f, 0f), Monster(rule, w, h, tint, 1.03f, 0.97f, 0f)),
                move = Clip(10f, -1, Monster(rule, w, h, tint, 1.08f, 0.92f, 0f), Monster(rule, w, h, tint, 0.95f, 1.05f, 0f)),
                windup = Clip(12f, -1, Monster(rule, w, h, tint, 0.92f, 1.08f, -0.1f), Monster(rule, w, h, tint, 0.86f, 1.14f, -0.15f), Monster(rule, w, h, tint, 0.8f, 1.2f, -0.2f)),
                attack = Clip(12f, -1, Monster(rule, w, h, tint, 1.3f, 0.8f, 0.2f), Monster(rule, w, h, tint, 1.15f, 0.9f, 0.1f)),
                hit = Clip(12f, -1, Monster(rule, w, h, Color.Lerp(tint, Color.white, 0.25f), 0.85f, 1.1f, -0.1f), Monster(rule, w, h, tint, 0.95f, 1f, 0f)),
                death = Clip(12f, -1, Monster(rule, w, h, Color.Lerp(tint, Color.black, 0.3f), 1.1f, 0.7f, 0f)),
            };
            return art;
        }

        /// <summary>몸(타원) + 종류 표식. sx·sy로 늘이고 줄이며, lean만큼 앞(오른쪽)으로 기운다.</summary>
        static Sprite Monster(MonsterRule rule, int w, int h, Color tint, float sx, float sy, float lean)
        {
            var c = new Canvas(w, h);
            float rx = rule.Diameter * 0.5f * Ppu * 1.15f * sx;
            float ry = rule.Diameter * 0.5f * Ppu * 1.0f * sy;
            float cx = w * 0.5f + lean * rx;
            float foot = h * 0.3f;
            float cy = foot + ry * 0.6f;
            c.Ellipse(w * 0.5f, foot, rx * 1.05f, ry * 0.3f, new Color(0f, 0f, 0f, 0.25f));
            c.Ellipse(cx, cy, rx + 2f, ry + 2f, Outline);
            c.Ellipse(cx, cy, rx, ry, tint);
            switch (rule.Kind)
            {
                case MonsterKind.Rat:
                    c.Circle(cx + rx * 0.6f, cy + ry * 0.7f, 3.5f, Outline);
                    c.Circle(cx + rx * 0.75f, cy + ry * 0.15f, 2f, Outline);
                    break;
                case MonsterKind.Boar:
                    c.Line(cx + rx * 0.7f, cy - ry * 0.1f, cx + rx * 1.15f, cy + ry * 0.25f, 4f, new Color(0.95f, 0.92f, 0.82f));
                    c.Circle(cx + rx * 0.55f, cy + ry * 0.35f, 3f, Outline);
                    break;
                default:
                    c.Line(cx + rx * 0.5f, cy - ry * 0.8f, cx + rx * 0.9f, cy + ry * 0.9f, 3f, new Color(0.85f, 0.8f, 0.7f));
                    c.Circle(cx + rx * 0.35f, cy + ry * 0.5f, 3f, Outline);
                    break;
            }
            return c.ToSprite(new Vector2(0.5f, foot / h));
        }

        // ---------------- 공용 이펙트 ----------------

        static Sprite Spark(bool round = false)
        {
            var c = new Canvas(10, 10);
            if (round) c.Circle(5f, 5f, 4f, Color.white);
            else c.Diamond(5f, 5f, 4.5f, Color.white);
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        static Sprite SlashMark()
        {
            var c = new Canvas(64, 8);
            c.Taper(0f, 4f, 64f, 3.5f, Color.white, true);
            return c.ToSprite(new Vector2(0.5f, 0.5f));
        }

        static SpriteClip Clip(float fps, int key, params Sprite[] frames) => new SpriteClip { frames = frames, fps = fps, keyFrameNumber = key + 1 };

        /// <summary>작은 픽셀 그리기 도구(가장자리 1px 부드럽게).</summary>
        sealed class Canvas
        {
            readonly int _w;
            readonly int _h;
            readonly Color[] _px;

            public Canvas(int w, int h)
            {
                _w = w;
                _h = h;
                _px = new Color[w * h];
            }

            void Blend(int x, int y, Color c, float coverage)
            {
                if (x < 0 || y < 0 || x >= _w || y >= _h || coverage <= 0f) return;
                float a = c.a * Mathf.Clamp01(coverage);
                ref var d = ref _px[y * _w + x];
                float outA = a + d.a * (1f - a);
                if (outA <= 0f) return;
                d.r = (c.r * a + d.r * d.a * (1f - a)) / outA;
                d.g = (c.g * a + d.g * d.a * (1f - a)) / outA;
                d.b = (c.b * a + d.b * d.a * (1f - a)) / outA;
                d.a = outA;
            }

            public void Ellipse(float cx, float cy, float rx, float ry, Color c)
            {
                for (int y = Mathf.FloorToInt(cy - ry - 1); y <= Mathf.CeilToInt(cy + ry + 1); y++)
                for (int x = Mathf.FloorToInt(cx - rx - 1); x <= Mathf.CeilToInt(cx + rx + 1); x++)
                {
                    float dx = (x + 0.5f - cx) / Mathf.Max(rx, 0.01f);
                    float dy = (y + 0.5f - cy) / Mathf.Max(ry, 0.01f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    Blend(x, y, c, (1f - d) * Mathf.Min(rx, ry) + 0.5f);
                }
            }

            public void Circle(float cx, float cy, float r, Color c) => Ellipse(cx, cy, r, r, c);

            public void Ring(float cx, float cy, float r, float thickness, Color c)
            {
                for (int y = Mathf.FloorToInt(cy - r - 1); y <= Mathf.CeilToInt(cy + r + 1); y++)
                for (int x = Mathf.FloorToInt(cx - r - 1); x <= Mathf.CeilToInt(cx + r + 1); x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    Blend(x, y, c, thickness * 0.5f - Mathf.Abs(d - (r - thickness * 0.5f)) + 0.5f);
                }
            }

            public void Sector(float cx, float cy, float r0, float r1, float a0, float a1, Color c)
            {
                for (int y = Mathf.FloorToInt(cy - r1 - 1); y <= Mathf.CeilToInt(cy + r1 + 1); y++)
                for (int x = Mathf.FloorToInt(cx - r1 - 1); x <= Mathf.CeilToInt(cx + r1 + 1); x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (ang < a0 || ang > a1) continue;
                    float edge = Mathf.Min(d - r0, r1 - d) + 0.5f;
                    float fade = Mathf.InverseLerp(r0, r1, d);
                    var col = c;
                    col.a *= 0.35f + 0.65f * fade;
                    Blend(x, y, col, edge);
                }
            }

            public void Line(float x0, float y0, float x1, float y1, float thickness, Color c)
            {
                float minX = Mathf.Min(x0, x1) - thickness, maxX = Mathf.Max(x0, x1) + thickness;
                float minY = Mathf.Min(y0, y1) - thickness, maxY = Mathf.Max(y0, y1) + thickness;
                Vector2 a = new Vector2(x0, y0), b = new Vector2(x1, y1), ab = b - a;
                float len2 = Mathf.Max(ab.sqrMagnitude, 0.0001f);
                for (int y = Mathf.FloorToInt(minY); y <= Mathf.CeilToInt(maxY); y++)
                for (int x = Mathf.FloorToInt(minX); x <= Mathf.CeilToInt(maxX); x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    float d = (p - (a + ab * t)).magnitude;
                    Blend(x, y, c, thickness * 0.5f - d + 0.5f);
                }
            }

            /// <summary>왼쪽(x0)에서 오른쪽으로 길이 len만큼 뻗는 뾰족한 띠. both면 양끝이 뾰족하다.</summary>
            public void Taper(float x0, float cy, float len, float halfHeight, Color c, bool both = false)
            {
                for (int x = Mathf.FloorToInt(x0); x <= Mathf.CeilToInt(x0 + len); x++)
                {
                    float u = Mathf.Clamp01((x + 0.5f - x0) / Mathf.Max(len, 1f));
                    float hh = halfHeight * (both ? Mathf.Sin(u * Mathf.PI) : Mathf.Lerp(0.6f, 1f, Mathf.Min(1f, u * 3f)) * (1f - Mathf.Pow(u, 6f)));
                    for (int y = Mathf.FloorToInt(cy - hh - 1); y <= Mathf.CeilToInt(cy + hh + 1); y++)
                        Blend(x, y, c, hh - Mathf.Abs(y + 0.5f - cy) + 0.5f);
                }
            }

            public void Diamond(float cx, float cy, float r, Color c)
            {
                for (int y = Mathf.FloorToInt(cy - r - 1); y <= Mathf.CeilToInt(cy + r + 1); y++)
                for (int x = Mathf.FloorToInt(cx - r - 1); x <= Mathf.CeilToInt(cx + r + 1); x++)
                    Blend(x, y, c, r - (Mathf.Abs(x + 0.5f - cx) + Mathf.Abs(y + 0.5f - cy)) + 0.5f);
            }

            public Sprite ToSprite(Vector2 pivot)
            {
                var tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                tex.SetPixels(_px);
                tex.Apply();
                var sprite = Sprite.Create(tex, new Rect(0, 0, _w, _h), pivot, Ppu);
                Made.Add(tex);
                Made.Add(sprite);
                return sprite;
            }
        }
    }
}
