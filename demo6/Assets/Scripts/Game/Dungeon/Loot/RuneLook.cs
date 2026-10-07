using Demo6.Core.Loot;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 룬 임시 그림(원화 전, 기획/세-무기-우클릭-소켓-1차.md 6-4·8-2 위험 12): 바닥 룬 = 작은 돌판 + 룬 색 점(버팀 룬 청록 #5FD6C8)과 옅은 빛.
    /// 강화석처럼 빛을 무시해 어둠 속에서도 보인다. 화면 글 색(가방 ◆·카드 룬 홈 줄·줍기 글)도 여기서 RuneDef.Rgb로 만든다.
    /// 원화 칸(아이콘·바닥 그림)은 원화 단계에서 따로 잡는다(Assets/Art·Resources는 이번에 고치지 않음).
    /// </summary>
    public static class RuneLook
    {
        static readonly Color Slab = new Color(0.40f, 0.38f, 0.35f);
        static readonly Color SlabEdge = new Color(0.20f, 0.19f, 0.17f);

        /// <summary>룬 색(RuneDef.Rgb 0xRRGGBB). 모르는 룬은 흰색.</summary>
        public static Color ColorOf(RuneDef def)
        {
            if (def == null) return Color.white;
            int rgb = def.Rgb;
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        /// <summary>룬 id의 색. 모르는 룬은 흰색.</summary>
        public static Color ColorOf(string runeId) => ColorOf(RuneTable.Get(runeId));

        /// <summary>바닥 룬 도형을 parent 아래에 만든다(옅은 빛 → 돌판 테 → 돌판 → 룬 색 점, 빛 무시).</summary>
        public static void Build(Transform parent, string runeId)
        {
            var c = ColorOf(runeId);
            int order = LootVisuals.GlintOrder;
            LootVisuals.Unlit(parent, "Glow", ShapeSprites.Circle, new Color(c.r, c.g, c.b, 0.22f), order - 1, Vector2.zero, new Vector2(0.46f, 0.46f));
            LootVisuals.Unlit(parent, "SlabEdge", ShapeSprites.Square, SlabEdge, order, Vector2.zero, new Vector2(0.30f, 0.23f), 8f);
            LootVisuals.Unlit(parent, "Slab", ShapeSprites.Square, Slab, order + 1, Vector2.zero, new Vector2(0.25f, 0.18f), 8f);
            LootVisuals.Unlit(parent, "Dot", ShapeSprites.Circle, c, order + 2, Vector2.zero, new Vector2(0.09f, 0.09f));
        }
    }
}
