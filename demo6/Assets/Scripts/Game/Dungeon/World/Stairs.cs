using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 계단(3차 초안 2-3 S, 매판 새 탐험 1차 5장 단계 4). F로 DungeonRoot.Descend: 같은 원정 그대로 체력·물약을 들고 아래층 승강장으로 장면을 다시 불러온다.
    /// 아래층이 시험판에 없으면(2층) Descend가 막혔다는 글만 띄운다. 오른쪽으로 내려가는 계단 단을 바닥 그림으로 그린다(빛을 받음).
    /// </summary>
    public sealed class Stairs : Interactable
    {
        const int Steps = 5;
        const float Width = 2.6f;
        const float Depth = 2.4f;

        static readonly Color Rim = new Color(0.36f, 0.32f, 0.28f);
        static readonly Color StepTop = new Color(0.46f, 0.41f, 0.36f);
        static readonly Color Hole = new Color(0.05f, 0.045f, 0.04f);

        string _prompt = "아래로 내려가기";

        public override string Prompt => _prompt;
        public override float Range => 2.2f;

        public static Stairs Create(DungeonCell cell, CellFeature f, Vector2 pos)
        {
            var go = WorldProps.Root("Stairs " + f.Id, pos);
            var stairs = go.AddComponent<Stairs>();
            stairs._prompt = (WorldProps.Floor + 1) + "층으로 내려가기";
            stairs.Build();
            return stairs;
        }

        void Build()
        {
            int order = WorldProps.FloorDecalOrder;
            WorldProps.Shape(transform, "Rim", Vector2.zero, new Vector2(Width + 0.3f, Depth + 0.3f), ShapeSprites.Square, Rim, order, false);
            WorldProps.Shape(transform, "Hole", Vector2.zero, new Vector2(Width, Depth), ShapeSprites.Square, Hole, order + 1, false);
            // 왼쪽(들어오는 쪽)에서 오른쪽으로 갈수록 낮고 어두운 단.
            float stepW = Width / Steps;
            for (int i = 0; i < Steps; i++)
            {
                float k = i / (float)(Steps - 1);
                var c = Color.Lerp(StepTop, Hole, k * 0.85f);
                float x = -Width * 0.5f + stepW * (i + 0.5f);
                WorldProps.Shape(transform, "Step", new Vector2(x, 0f), new Vector2(stepW * 0.82f, Depth - 0.1f), ShapeSprites.Square, c, order + 2 + i, false);
            }
        }

        public override void Interact()
        {
            var root = DungeonRoot.Instance;
            if (root) root.Descend();
        }
    }
}
