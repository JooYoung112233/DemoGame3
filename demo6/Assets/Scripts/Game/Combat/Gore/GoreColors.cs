using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 맞았을 때 튀는 것의 재질(기획/다크판타지-분위기-1차.md '잔혹'). 살 있는 적(굴쥐·멧돼지·궁수·갱도 오우거)은 피,
    /// 굴쥐 둥지는 흙·고름, 허수아비(나무·측정용)는 아무것도 튀지 않는다(손맛 측정이 흔들리지 않게).
    /// </summary>
    public enum GoreMatter
    {
        None,
        Flesh,
        Nest,
    }

    /// <summary>
    /// 피·흙·고름 색. 기획 '지키는 것': 피는 등급색 빨강·주황과 헷갈리지 않게 검붉게(#4A0606~#8A1010).
    /// 바뀌지 않는 값이라 플레이 시작 때 비울 필요가 없다.
    /// </summary>
    public static class GoreColors
    {
        /// <summary>날아가는 피 방울 밝은 쪽(#8A1010, 피 색 범위 맨 위).</summary>
        public static readonly Color BloodBright = Hex(0x8A1010);
        public static readonly Color BloodMid = Hex(0x6E0C0C);
        /// <summary>마른 피·웅덩이(#4A0606, 피 색 범위 맨 아래).</summary>
        public static readonly Color BloodDark = Hex(0x4A0606);
        /// <summary>바닥 얼룩(빛을 받으므로 횃불 곁에서만 붉게 보인다).</summary>
        public static readonly Color Stain = new Color(0x58 / 255f, 0x08 / 255f, 0x08 / 255f, 0.86f);
        /// <summary>피 웅덩이: 가장 검붉고 거의 불투명.</summary>
        public static readonly Color Pool = new Color(0x4A / 255f, 0x06 / 255f, 0x06 / 255f, 0.94f);
        /// <summary>맞은 자리에 잠깐 번지는 피 안개.</summary>
        public static readonly Color Puff = new Color(0x7A / 255f, 0x0E / 255f, 0x0E / 255f, 0.85f);
        /// <summary>화면 가장자리 붉음(플레이어 피격·빈사 맥동).</summary>
        public static readonly Color Edge = Hex(0x8A1010);

        /// <summary>둥지 흙.</summary>
        public static readonly Color Dirt = Hex(0x4A3A2A);
        public static readonly Color DirtLight = Hex(0x5E4A34);
        /// <summary>둥지 고름: 탁한 누런 풀빛(등급색 초록과 멀게 채도를 낮춤).</summary>
        public static readonly Color Pus = Hex(0x6E6A2E);
        public static readonly Color PusDark = new Color(0x4A / 255f, 0x47 / 255f, 0x20 / 255f, 0.9f);

        /// <summary>도형 그림이 없을 때 그림 모드 시체에 곱하는 어두운 살빛.</summary>
        public static readonly Color ArtCorpse = new Color(0.5f, 0.4f, 0.38f, 1f);

        /// <summary>
        /// 갱도 오우거 살갗(기획/전투-보스-무기-다듬기-1차.md 3-1·3-9): 젖은 돌과 석탄 가루 회색. 몸 도형(EnemySpawner)·조각·시체의 바탕이고
        /// 정수리 몸(OgreLook)은 머리·어깨를 밝게(Light), 등을 어둡게(Dark) 칠한다. 등급색·예고 빨강·밝은 금빛과 멀다.
        /// </summary>
        public static readonly Color OgreSkin = Hex(0x585A5C);
        public static readonly Color OgreSkinLight = Hex(0x7B7D7E);
        public static readonly Color OgreSkinDark = Hex(0x343538);

        /// <summary>적 종류별 재질. 허수아비는 없음.</summary>
        public static GoreMatter MatterOf(Enemy enemy)
        {
            if (!enemy || enemy.IsDummy) return GoreMatter.None;
            switch (enemy.Kind)
            {
                case MonsterKind.Rat:
                case MonsterKind.Boar:
                case MonsterKind.Archer:
                case MonsterKind.Ogre:
                    return GoreMatter.Flesh;
                case MonsterKind.Nest:
                    return GoreMatter.Nest;
                default:
                    return GoreMatter.None;
            }
        }

        /// <summary>도형 몸 색(Enemies/EnemySpawner와 같은 표). 조각·시체 색의 바탕.</summary>
        public static Color BodyColor(Enemy enemy)
        {
            switch (enemy.Kind)
            {
                case MonsterKind.Rat: return enemy.IsDummy ? Palette.RatDummy : Palette.Rat;
                case MonsterKind.Boar: return enemy.IsDummy ? Palette.WoodDummy : Palette.Boar;
                case MonsterKind.Nest: return Palette.Nest;
                case MonsterKind.Ogre: return OgreSkin;
                default: return Palette.Archer;
            }
        }

        /// <summary>시체·조각: 몸 색을 어둡게 하고 붉은 기를 남긴다(빛을 받으면 횃불 곁에서 검붉은 살덩이로 보임).</summary>
        public static Color Darken(Color body) =>
            new Color(body.r * 0.45f + 0.04f, body.g * 0.32f + 0.01f, body.b * 0.3f + 0.01f, 1f);

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
    }
}
