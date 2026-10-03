using System;
using Demo6.Core.Loot;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 보상 연출의 임시 도형(원화 전, 3차 초안 7-2 '그림·소리 리소스는 다음 단계').
    /// 빛기둥·반짝임·고리는 빛을 무시하는 재질(RenderMaterials.MakeUnlit), 바닥 장비 몸통(부위별 7종)과 궤짝은 빛을 받는다(어둠 속에서는 빛기둥만 보임).
    /// 정렬: 빛기둥·고리는 공격 예고(-60)보다 아래, 무기 몸통은 바닥 물체 높이(2차 7-6 '예고 도형은 늘 빛기둥 위').
    /// </summary>
    public static class LootVisuals
    {
        public const int BeamOrder = -66;
        public const int RingOrder = -65;
        public const int BodyOrder = -52;
        public const int GlintOrder = -50;

        /// <summary>칼날 쇠 색(빛을 받음).</summary>
        public static readonly Color Steel = new Color(0.79f, 0.81f, 0.84f);
        public static readonly Color Grip = new Color(0.25f, 0.19f, 0.14f);
        /// <summary>골드 무더기: 전설 주황 #F2994A와 구별되는 밝은 노랑(2차 7-6).</summary>
        public static readonly Color Gold = new Color(1f, 0.88f, 0.3f);
        public static readonly Color GoldDark = new Color(0.85f, 0.66f, 0.12f);
        /// <summary>강화석: 등급색과 겹치지 않는 옅은 청회색.</summary>
        public static readonly Color Stone = new Color(0.68f, 0.77f, 0.84f);

        static Sprite _beam;

        /// <summary>바닥에서 위로 서는 빛기둥(아래 가운데가 기준점, 가운데가 밝고 위로 갈수록 옅어짐). 크기는 1×1유닛.</summary>
        public static Sprite Beam => _beam ? _beam : _beam = Make(32, 32, new Vector2(0.5f, 0f), (u, v) =>
        {
            float side = Mathf.Clamp01(1f - u * u);
            float core = Mathf.Exp(-(u / 0.28f) * (u / 0.28f));
            float bottom = Mathf.Clamp01(v / 0.04f);
            float top = Mathf.Pow(Mathf.Clamp01(1f - v), 0.8f);
            return Mathf.Clamp01(side * side * 0.55f + core * 0.6f) * bottom * top;
        });

        /// <summary>등급색(2차 6-2). DungeonUi.GradeColor와 같다.</summary>
        public static Color GradeColor(Grade grade) => DungeonUi.GradeColor((int)grade);

        /// <summary>빛을 무시하는 도형 하나.</summary>
        public static SpriteRenderer Unlit(Transform parent, string name, Sprite sprite, Color color, int order, Vector2 localPos, Vector2 scale, float angleDeg = 0f)
        {
            var sr = Part(parent, name, sprite, color, order, localPos, scale, angleDeg);
            RenderMaterials.MakeUnlit(sr);
            return sr;
        }

        /// <summary>빛을 받는 도형 하나(기본 스프라이트 재질).</summary>
        public static SpriteRenderer Part(Transform parent, string name, Sprite sprite, Color color, int order, Vector2 localPos, Vector2 scale, float angleDeg = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>원화 전 임시 무게 색(장비 문서 9-1): 가죽 갈색, 사슬 회청, 판금 은색.</summary>
        public static readonly Color LeatherColor = new Color(0.46f, 0.32f, 0.19f);
        public static readonly Color ChainColor = new Color(0.47f, 0.53f, 0.58f);
        public static readonly Color PlateColor = new Color(0.8f, 0.82f, 0.85f);
        /// <summary>반지·목걸이 띠(빛을 받는 놋쇠).</summary>
        public static readonly Color Brass = new Color(0.66f, 0.53f, 0.3f);

        /// <summary>방어구 무게 색(무게가 없으면 가죽).</summary>
        public static Color WeightColor(ArmorWeight weight) =>
            weight == ArmorWeight.Heavy ? PlateColor : weight == ArmorWeight.Medium ? ChainColor : LeatherColor;

        /// <summary>
        /// 바닥 장비 모양(장비 문서 9-1 '바닥 장비 모양은 부위별 코드 도형', 그림 필요 없음). 무기 3종은 BuildWeapon 그대로,
        /// 갑옷(몸통+어깨)·투구(둥근 머리+챙)·장갑(벙어리 장갑 한 켤레)·장화(ㄴ자 한 켤레)·반지(고리+보석)·목걸이(줄+펜던트)는 새 도형이다.
        /// 방어구 몸은 무게 색, 등급은 한 곳(띠·보석·펜던트)에 등급색. 돌려주는 Transform 아래 도형은 모두 빛을 받는다.
        /// </summary>
        public static Transform BuildGear(Transform parent, GearItem item)
        {
            if (item == null) return BuildWeapon(parent, GearBaseTable.Longsword, Grade.Common);
            if (item.IsWeapon) return BuildWeapon(parent, item.BaseId, item.Grade);
            var root = new GameObject(item.Part.ToString()).transform;
            root.SetParent(parent, false);
            var grade = GradeColor(item.Grade);
            var metal = WeightColor(item.Base.Weight);
            var dark = Color.Lerp(metal, Color.black, 0.35f);
            bool heavy = item.Base.Weight == ArmorWeight.Heavy;
            switch (item.Part)
            {
                case GearPart.Armor:
                    // 몸통 + 어깨(판금은 넓은 어깨받이) + 등급색 허리띠.
                    Part(root, "Torso", ShapeSprites.Square, metal, BodyOrder, new Vector2(0f, 0f), new Vector2(0.46f, 0.5f));
                    float shoulder = heavy ? 0.24f : 0.17f;
                    Part(root, "ShoulderL", ShapeSprites.Circle, dark, BodyOrder + 1, new Vector2(-0.26f, 0.17f), new Vector2(shoulder, shoulder * 0.8f));
                    Part(root, "ShoulderR", ShapeSprites.Circle, dark, BodyOrder + 1, new Vector2(0.26f, 0.17f), new Vector2(shoulder, shoulder * 0.8f));
                    Part(root, "Belt", ShapeSprites.Square, grade, BodyOrder + 1, new Vector2(0f, -0.13f), new Vector2(0.48f, 0.07f));
                    break;
                case GearPart.Helm:
                    // 둥근 머리 + 챙 + 등급색 이마 보석.
                    Part(root, "Dome", ShapeSprites.Circle, metal, BodyOrder, new Vector2(0f, 0.05f), new Vector2(0.38f, 0.34f));
                    Part(root, "Brim", ShapeSprites.Square, dark, BodyOrder + 1, new Vector2(0f, -0.1f), new Vector2(heavy ? 0.46f : 0.4f, 0.08f));
                    Part(root, "Gem", ShapeSprites.Circle, grade, BodyOrder + 2, new Vector2(0f, 0.02f), new Vector2(0.09f, 0.09f));
                    break;
                case GearPart.Gloves:
                    // 한 켤레: 손바닥(원) + 등급색 손목 띠.
                    for (int i = 0; i < 2; i++)
                    {
                        float side = i == 0 ? -1f : 1f;
                        var glove = new GameObject(i == 0 ? "GloveL" : "GloveR").transform;
                        glove.SetParent(root, false);
                        glove.localPosition = new Vector2(side * 0.13f, 0f);
                        glove.localRotation = Quaternion.Euler(0f, 0f, side * -14f);
                        Part(glove, "Palm", ShapeSprites.Circle, metal, BodyOrder, new Vector2(0f, 0.05f), new Vector2(0.2f, 0.24f));
                        Part(glove, "Thumb", ShapeSprites.Circle, dark, BodyOrder + 1, new Vector2(-side * 0.09f, 0.03f), new Vector2(0.08f, 0.1f));
                        Part(glove, "Cuff", ShapeSprites.Square, grade, BodyOrder + 1, new Vector2(0f, -0.1f), new Vector2(0.19f, 0.07f));
                    }
                    break;
                case GearPart.Boots:
                    // 한 켤레: 목(세로) + 발(가로, 발끝 바깥) + 등급색 목 띠.
                    for (int i = 0; i < 2; i++)
                    {
                        float side = i == 0 ? -1f : 1f;
                        var boot = new GameObject(i == 0 ? "BootL" : "BootR").transform;
                        boot.SetParent(root, false);
                        boot.localPosition = new Vector2(side * 0.12f, 0f);
                        Part(boot, "Shaft", ShapeSprites.Square, metal, BodyOrder, new Vector2(0f, 0.04f), new Vector2(0.12f, 0.26f));
                        Part(boot, "Foot", ShapeSprites.Square, dark, BodyOrder, new Vector2(side * 0.05f, -0.11f), new Vector2(0.22f, 0.1f));
                        Part(boot, "Cuff", ShapeSprites.Square, grade, BodyOrder + 1, new Vector2(0f, 0.16f), new Vector2(0.14f, 0.05f));
                    }
                    break;
                case GearPart.Ring:
                    // 놋쇠 고리 + 등급색 보석.
                    Part(root, "Band", ShapeSprites.Ring, Brass, BodyOrder, new Vector2(0f, -0.02f), new Vector2(0.28f, 0.28f));
                    Part(root, "Gem", ShapeSprites.Circle, grade, BodyOrder + 1, new Vector2(0f, 0.12f), new Vector2(0.12f, 0.12f));
                    break;
                default:
                    // 목걸이: 줄(납작한 고리) + 아래로 향한 등급색 펜던트.
                    Part(root, "Chain", ShapeSprites.Ring, Color.Lerp(Brass, Color.black, 0.2f), BodyOrder, new Vector2(0f, 0.06f), new Vector2(0.42f, 0.34f));
                    Part(root, "Pendant", ShapeSprites.Triangle, grade, BodyOrder + 1, new Vector2(0f, -0.15f), new Vector2(0.18f, 0.16f), -90f);
                    break;
            }
            return root;
        }

        /// <summary>
        /// 옛 무기 보기로 무기 모양을 만든다(옮기는 동안 쓰는 다리, 새 코드는 BuildGear).
        /// </summary>
        public static Transform BuildWeapon(Transform parent, WeaponItem item) =>
            BuildWeapon(parent, item != null ? item.WeaponId : WeaponItem.LongswordId, item != null ? item.Grade : Grade.Common);

        /// <summary>
        /// 바닥에 놓인 무기 모양(장검: 가는 칼, 대검: 넓고 긴 칼, 쌍검: 짧은 칼 두 자루를 엇갈림). 코등이에 등급색.
        /// 돌려주는 Transform 아래 도형은 모두 빛을 받는다.
        /// </summary>
        public static Transform BuildWeapon(Transform parent, string weaponId, Grade gradeOf)
        {
            var root = new GameObject("Weapon").transform;
            root.SetParent(parent, false);
            var grade = GradeColor(gradeOf);
            switch (weaponId)
            {
                case GearBaseTable.Greatsword:
                    Sword(root, Vector2.zero, -35f, 0.2f, 0.8f, 0.42f, grade);
                    break;
                case GearBaseTable.Twinblades:
                    Sword(root, new Vector2(-0.08f, 0f), -60f, 0.08f, 0.44f, 0.22f, grade);
                    Sword(root, new Vector2(0.08f, 0f), -120f, 0.08f, 0.44f, 0.22f, grade);
                    break;
                default:
                    Sword(root, Vector2.zero, -35f, 0.1f, 0.66f, 0.3f, grade);
                    break;
            }
            return root;
        }

        /// <summary>칼 한 자루: 칼날(쇠) + 코등이(등급색) + 손잡이. angleDeg 0이면 칼끝이 위.</summary>
        static void Sword(Transform root, Vector2 pos, float angleDeg, float bladeWidth, float bladeLength, float guardWidth, Color grade)
        {
            var sword = new GameObject("Sword").transform;
            sword.SetParent(root, false);
            sword.localPosition = pos;
            sword.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
            float gripLength = bladeLength * 0.28f;
            float guardY = -bladeLength * 0.5f + gripLength;
            Part(sword, "Blade", ShapeSprites.Square, Steel, BodyOrder, new Vector2(0f, guardY + bladeLength * 0.5f - gripLength * 0.5f), new Vector2(bladeWidth, bladeLength - gripLength));
            Part(sword, "Guard", ShapeSprites.Square, grade, BodyOrder + 1, new Vector2(0f, guardY), new Vector2(guardWidth, bladeWidth * 0.7f + 0.03f));
            Part(sword, "Grip", ShapeSprites.Square, Grip, BodyOrder, new Vector2(0f, guardY - gripLength * 0.5f), new Vector2(bladeWidth * 0.6f, gripLength));
        }

        /// <summary>골드 무더기: 밝은 노랑 동전 셋(빛 무시, 빛기둥 없음).</summary>
        public static void BuildGoldPile(Transform parent)
        {
            Unlit(parent, "CoinA", ShapeSprites.Circle, GoldDark, GlintOrder, new Vector2(-0.11f, -0.02f), new Vector2(0.2f, 0.16f));
            Unlit(parent, "CoinB", ShapeSprites.Circle, GoldDark, GlintOrder, new Vector2(0.11f, -0.02f), new Vector2(0.2f, 0.16f));
            Unlit(parent, "CoinC", ShapeSprites.Circle, Gold, GlintOrder + 1, new Vector2(0f, 0.06f), new Vector2(0.22f, 0.18f));
        }

        /// <summary>강화석: 작은 마름모(빛 무시).</summary>
        public static void BuildStone(Transform parent)
        {
            Unlit(parent, "Stone", ShapeSprites.Square, Stone, GlintOrder, Vector2.zero, new Vector2(0.18f, 0.18f), 45f);
            Unlit(parent, "Shine", ShapeSprites.Square, Color.white, GlintOrder + 1, new Vector2(-0.03f, 0.03f), new Vector2(0.05f, 0.05f), 45f);
        }

        /// <summary>네 갈래 반짝임(가는 막대 둘). 일반 등급 '작은 회색 반짝임', 쇠 궤짝의 희미한 빛.</summary>
        public static Transform BuildSparkle(Transform parent, string name, Color color, int order, Vector2 localPos, float size)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;
            Unlit(root, "A", ShapeSprites.Square, color, order, Vector2.zero, new Vector2(size * 0.12f, size));
            Unlit(root, "B", ShapeSprites.Square, color, order, Vector2.zero, new Vector2(size, size * 0.12f));
            return root;
        }

        /// <summary>그 렌더러들의 알파를 한꺼번에 바꾼다(색은 그대로).</summary>
        public static void SetAlpha(SpriteRenderer[] renderers, float alpha)
        {
            if (renderers == null) return;
            foreach (var sr in renderers)
            {
                if (!sr) continue;
                var c = sr.color;
                c.a = alpha;
                sr.color = c;
            }
        }

        /// <param name="alpha">u는 -1..1(가로), v는 0..1(아래 → 위).</param>
        static Sprite Make(int width, int height, Vector2 pivot, Func<float, float, float> alpha)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width * 2f - 1f;
                float v = (y + 0.5f) / height;
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(u, v)) * 255f);
                pixels[y * width + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), pivot, width);
        }
    }

    /// <summary>
    /// 포물선 튀어나오기(2차 7-6): 기다림 뒤 0.45초 동안 높이 1.2로 날아 내려앉는다. 게임 시간으로 흐른다(창이 열리면 멈춤).
    /// </summary>
    internal sealed class LootArc
    {
        public const float Duration = 0.45f;
        public const float Height = 1.2f;

        readonly Vector2 _from;
        readonly Vector2 _to;
        readonly float _delay;
        float _time;

        public LootArc(Vector2 from, Vector2 to, float delay)
        {
            _from = from;
            _to = to;
            _delay = Mathf.Max(0f, delay);
        }

        /// <summary>아직 튀어나오기 전(숨겨 둠).</summary>
        public bool Waiting => _time < _delay;
        public bool Done { get; private set; }
        /// <summary>날아가는 비율 0..1.</summary>
        public float Progress => Mathf.Clamp01((_time - _delay) / Duration);
        public Vector2 Target => _to;

        /// <summary>시간을 흘리고 바닥 자리와 높이를 돌려준다. 내려앉은 그 프레임에만 true.</summary>
        public bool Step(float dt, out Vector2 ground, out float height)
        {
            _time += Mathf.Max(0f, dt);
            float k = Progress;
            ground = Vector2.Lerp(_from, _to, k);
            height = 4f * Height * k * (1f - k);
            if (Done || k < 1f) return false;
            Done = true;
            return true;
        }
    }
}
