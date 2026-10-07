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
            if (TryDrawPngDoor()) return;
            var e = _edge;
            int order = WorldProps.WallDetailOrder;
            for (int i = -1; i <= 1; i++)
            {
                float u = i * 1f;
                WorldProps.Stroke(transform, "Seam", WorldProps.DoorLocal(e, u, -0.45f), WorldProps.DoorLocal(e, u, 0.45f), 0.06f, Seam, order, false);
            }
            WorldProps.Stroke(transform, "Band", WorldProps.DoorLocal(e, -1.95f, 0.28f), WorldProps.DoorLocal(e, 1.95f, 0.28f), 0.1f, Iron, order + 1, false);
            WorldProps.Stroke(transform, "Band", WorldProps.DoorLocal(e, -1.95f, -0.28f), WorldProps.DoorLocal(e, 1.95f, -0.28f), 0.1f, Iron, order + 1, false);
            // Lock hardware is seen on its narrow top plane and follows the door's own axes.
            Vector2 along = WorldProps.DoorAxis(e);
            float lockAngle = Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg;
            WorldProps.Shape(transform, "Lock", WorldProps.DoorLocal(e, 0.5f, 0f), new Vector2(0.34f, 0.14f), ShapeSprites.Square, Brass, order + 2, false, lockAngle);
            WorldProps.Shape(transform, "Shackle", WorldProps.DoorLocal(e, 0.5f, 0.10f), new Vector2(0.24f, 0.12f), ShapeSprites.Ring, Brass, order + 2, false, lockAngle);
            WorldProps.Shape(transform, "Keyhole", WorldProps.DoorLocal(e, 0.5f, 0f), new Vector2(0.07f, 0.04f), ShapeSprites.Square, Seam, order + 3, false, lockAngle);
        }

        bool TryDrawPngDoor()
        {
            var oldVisual = transform.Find("Visual");
            var oldRenderer = oldVisual ? oldVisual.GetComponent<SpriteRenderer>() : null;
            if (!oldRenderer || !PropsV062Art.TryLoad(new[] {"door-leaf", "door-jamb", "door-lock", "door-threshold"}, out var sprites)) return false;
            float width = Mathf.Max(_edge.DoorSize.x, _edge.DoorSize.y);
            float thickness = Mathf.Min(_edge.DoorSize.x, _edge.DoorSize.y);
            int leafOrder = PropsV062Art.SolidOrder(transform.position.y);
            PropsV062Art.DoorPart(transform, "Door leaf PNG", sprites[0], _edge, Vector2.zero,
                new Vector2(width, thickness), leafOrder);
            var lockView = PropsV062Art.DoorPart(transform, "Door lock PNG", sprites[2], _edge, new Vector2(.5f, .045f),
                new Vector2(.34f, .23f), leafOrder + 3);
            bool timberFrame = DungeonPassageFramesV063.TryBuildLockedFrame(_edge, transform.parent);
            // Keep the lock readable beside the overhead beam; the legacy frame retains its previous pose.
            if (timberFrame) lockView.Carrier.localPosition = WorldProps.DoorLocal(_edge, .5f, -.32f);
            if (!timberFrame)
            {
                // Edge.Open destroys only the blocker. Frame and ground threshold remain in the same dungeon scene.
                var frame = new GameObject("Locked door frame PNG").transform;
                frame.SetParent(transform.parent, false);
                frame.position = transform.position;
                const float jambWidth = .6f;
                foreach (float side in new[] {-1f, 1f})
                {
                    var at = new Vector2(side * (width + jambWidth) * .5f, 0f);
                    float groundY = frame.TransformPoint(WorldProps.DoorLocal(_edge, at.x, at.y)).y;
                    PropsV062Art.DoorPart(frame, side < 0f ? "Jamb A" : "Jamb B", sprites[1], _edge,
                        at, new Vector2(jambWidth, jambWidth), PropsV062Art.SolidOrder(groundY));
                }
                PropsV062Art.DoorPart(frame, "Ground threshold", sprites[3], _edge, Vector2.zero,
                    new Vector2(width, thickness), WorldProps.FloorDecalOrder);
                DungeonPassageFramesV063.RegisterLegacyFrame(_edge, frame);
            }
            // Keep the original collider, wall layer and ShadowCaster2D. Replace only its visible placeholder.
            oldRenderer.enabled = false;
            return true;
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
