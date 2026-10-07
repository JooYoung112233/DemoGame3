using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 바닥 긁은 글(FeatureKind.Scrawl, 보스방 앞 쉼터 "여기서부터 돌방이다. 문 너머에서 씹는 소리가 난다."). 빛을 받는 바닥 긁힘 자국(도형 임시판)이고,
    /// 플레이어가 2.0 안에 처음 들어오면 장면에 한 번 글을 띄운다(DungeonEvents.Say). 충돌 없음.
    /// 자국은 곡괭이 끝으로 돌바닥을 긁은 글자 줄 둘(밝게 긁힌 획 + 아래 그늘 획)과 끝에 그은 화살 하나다. 글자를 읽을 수 있게 그리지는 않는다.
    /// </summary>
    public sealed class FloorScrawl : MonoBehaviour
    {
        /// <summary>글을 띄우는 거리.</summary>
        const float ReadRadius = 2.0f;

        static readonly Color ScratchColor = new Color(0.62f, 0.58f, 0.5f, 0.9f);
        static readonly Color GrooveColor = new Color(0.08f, 0.07f, 0.06f, 0.75f);

        string _text = "";
        bool _read;

        /// <summary>읽힐 글.</summary>
        public string Text => _text;
        /// <summary>이 장면에서 이미 읽혔는가.</summary>
        public bool Read => _read;

        public static FloorScrawl Create(DungeonCell cell, CellFeature f, Vector2 pos)
        {
            var go = WorldProps.Root("Scrawl " + (f != null ? f.Id : ""), pos);
            var scrawl = go.AddComponent<FloorScrawl>();
            scrawl._text = f != null && !string.IsNullOrEmpty(f.Label) ? f.Label : "";
            scrawl.Build(f != null ? f.Id : "");
            return scrawl;
        }

        /// <summary>긁힌 획 묶음(칸마다 같은 모양: id로 정한 난수).</summary>
        void Build(string id)
        {
            var rng = new System.Random(ShapeSprites.Seed(id ?? "", 4271));
            int order = WorldProps.FloorDecalOrder;
            // 두 줄, 줄마다 글자 덩이 4~5개(획 2~3개).
            for (int line = 0; line < 2; line++)
            {
                float y = 0.22f - line * 0.42f;
                int glyphs = 4 + (line == 0 ? 1 : 0);
                float x = -0.95f + line * 0.12f;
                for (int g = 0; g < glyphs; g++)
                {
                    int strokes = 2 + rng.Next(2);
                    for (int s = 0; s < strokes; s++)
                    {
                        Vector2 a = new Vector2(x + Range(rng, -0.05f, 0.18f), y + Range(rng, -0.13f, 0.13f));
                        Vector2 b = a + WorldProps.Rotate(Vector2.right, Range(rng, -70f, 250f)) * Range(rng, 0.14f, 0.28f);
                        Scratch(a, b, order);
                    }
                    x += Range(rng, 0.34f, 0.44f);
                }
            }
            // 끝에 그은 화살(문 쪽 = 오른쪽): 긴 획 + 두 깃.
            Vector2 tail = new Vector2(-0.55f, -0.62f);
            Vector2 head = new Vector2(0.75f, -0.62f);
            Scratch(tail, head, order);
            Scratch(head, head + new Vector2(-0.2f, 0.14f), order);
            Scratch(head, head + new Vector2(-0.2f, -0.14f), order);
        }

        void Scratch(Vector2 a, Vector2 b, int order)
        {
            // 그늘 획을 조금 아래에 먼저, 긁힌 밝은 획을 위에.
            WorldProps.Stroke(transform, "Groove", a + new Vector2(0.015f, -0.025f), b + new Vector2(0.015f, -0.025f), 0.05f, GrooveColor, order, false);
            WorldProps.Stroke(transform, "Scratch", a, b, 0.03f, ScratchColor, order + 1, false);
        }

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        void Update()
        {
            if (_read) return;
            if (WorldProps.PlayerDistance(transform.position) > ReadRadius) return;
            _read = true;
            if (!string.IsNullOrEmpty(_text)) DungeonEvents.Say(_text);
            enabled = false;
        }
    }
}
