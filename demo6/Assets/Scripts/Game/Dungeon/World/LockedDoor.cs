using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 광업소 자물쇠 문(3차 초안 2-3 '=', 4-2 한길의 열쇠). 나무 문에 쇠띠와 자물쇠를 그린다.
    /// 1층 열쇠는 약 78분에 받으므로 M0b에서는 잠긴 채 이유("광업소 자물쇠 — 열쇠가 필요하다")만 보인다.
    /// 열쇠가 있으면(시험 패널 등) F 1초로 연다. 조사율에는 넣지 않는다(M0b에서 열 수 없음).
    /// </summary>
    public sealed class LockedDoor : MonoBehaviour
    {
        const float HoldTime = 1f;
        const float UseRange = 2.2f;

        static readonly Color DoorWood = new Color(0.42f, 0.31f, 0.21f);
        static readonly Color Seam = new Color(0.25f, 0.18f, 0.12f);
        static readonly Color Iron = new Color(0.3f, 0.3f, 0.32f);
        static readonly Color Brass = new Color(0.72f, 0.6f, 0.32f);

        DungeonEdge _edge;

        /// <summary>문틈을 채우는 Wall 레이어 물체를 만든다.</summary>
        public static GameObject Create(DungeonEdge edge)
        {
            var go = DungeonWorld.Block(DungeonRoot.Instance ? DungeonRoot.Instance.transform : null, "LockedDoor", edge.DoorCenter, edge.DoorSize, DoorWood, false);
            var door = go.AddComponent<LockedDoor>();
            door.Setup(edge);
            return go;
        }

        void Setup(DungeonEdge edge)
        {
            _edge = edge;
            DrawDoor();
            var use = gameObject.AddComponent<WorldInteraction>();
            use.Setup("자물쇠 열기", HoldTime, UseRange,
                () => _edge != null && !_edge.Opened,
                () => WorldProps.HasKey ? null : "광업소 자물쇠 — 열쇠가 필요하다",
                Unlock);
        }

        /// <summary>판자 이음매 3줄, 쇠띠 2줄, 가운데 자물쇠(놋쇠 몸 + 고리). 빛을 받는 그림.</summary>
        void DrawDoor()
        {
            var e = _edge;
            int order = WorldProps.WallDetailOrder;
            for (int i = -1; i <= 1; i++)
            {
                float u = i * 1f;
                WorldProps.Stroke(transform, "Seam", WorldProps.DoorLocal(e, u, -0.45f), WorldProps.DoorLocal(e, u, 0.45f), 0.06f, Seam, order, false);
            }
            WorldProps.Stroke(transform, "Band", WorldProps.DoorLocal(e, -1.95f, 0.28f), WorldProps.DoorLocal(e, 1.95f, 0.28f), 0.1f, Iron, order + 1, false);
            WorldProps.Stroke(transform, "Band", WorldProps.DoorLocal(e, -1.95f, -0.28f), WorldProps.DoorLocal(e, 1.95f, -0.28f), 0.1f, Iron, order + 1, false);
            WorldProps.Shape(transform, "Lock", WorldProps.DoorLocal(e, 0.5f, 0f), new Vector2(0.34f, 0.34f), ShapeSprites.Square, Brass, order + 2, false);
            WorldProps.Shape(transform, "Shackle", WorldProps.DoorLocal(e, 0.5f, 0f) + Vector2.up * 0.2f, new Vector2(0.28f, 0.28f), ShapeSprites.Ring, Brass, order + 2, false);
            WorldProps.Shape(transform, "Keyhole", WorldProps.DoorLocal(e, 0.5f, 0f), new Vector2(0.07f, 0.12f), ShapeSprites.Square, Seam, order + 3, false);
        }

        void Unlock()
        {
            if (_edge == null || _edge.Opened || !WorldProps.HasKey) return;
            Sfx.Play(SfxKind.Chest);
            _edge.Open();
            DungeonEvents.Say("녹슨 자물쇠가 비명을 지르며 풀렸다");
        }
    }
}
