using System;
using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>원화 전 임시 도형. 모두 크기 1유닛(부채꼴은 반지름 1유닛), 중심 기준, 오른쪽(+x)을 바라본다.</summary>
    public static class ShapeSprites
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
