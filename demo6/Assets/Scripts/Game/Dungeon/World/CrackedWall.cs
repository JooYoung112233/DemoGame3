using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 금 간 바위벽(3차 초안 2-3 '#', 4-2 곡괭이). 처음부터 밝은 금이 보여(빛을 받는 그림) 곡괭이를 쥔 순간 '저기!'가 떠오르게 한다.
    /// 곡괭이가 없으면 "곡괭이가 필요하다"만 보이고, 있으면 F 1.5초로 깬다. 깨는 소리(반경 12)가 가장 가까운 무리 하나를 깨운다.
    /// 문틈 가운데에서 거리 2.2라 양쪽 어디서나 쓸 수 있다. 깨면 지름길 발견(3U, 경험치는 PlayerProgress).
    /// </summary>
    public sealed class CrackedWall : MonoBehaviour
    {
        const float HoldTime = 1.5f;
        const float UseRange = 2.2f;
        const float NoiseRadius = 12f;

        static readonly Color CrackColor = new Color(0.68f, 0.63f, 0.56f);
        static readonly Color CrackDark = new Color(0.2f, 0.18f, 0.16f);
        static readonly Color RockDebris = new Color(0.45f, 0.42f, 0.38f);

        DungeonEdge _edge;
        string _id;

        /// <summary>문틈을 채우는 Wall 레이어 물체를 만든다.</summary>
        public static GameObject Create(DungeonEdge edge)
        {
            var go = DungeonWorld.Block(DungeonRoot.Instance ? DungeonRoot.Instance.transform : null, "CrackedWall", edge.DoorCenter, edge.DoorSize, Palette.Wall, false);
            var wall = go.AddComponent<CrackedWall>();
            wall.Setup(edge);
            return go;
        }

        void Setup(DungeonEdge edge)
        {
            _edge = edge;
            _id = "f" + WorldProps.Floor + ".cracked." + edge.A.Id + "-" + edge.B.Id;
            var state = WorldProps.State;
            if (state != null) state.Register(_id, DiscoveryKind.Shortcut, IconCell(edge), edge.DoorCenter, "금 간 벽");
            DrawCracks();
            var use = gameObject.AddComponent<WorldInteraction>();
            use.Setup("곡괭이로 깨기", HoldTime, UseRange,
                () => _edge != null && !_edge.Opened,
                () => WorldProps.HasPickaxe ? null : "곡괭이가 필요하다",
                Smash);
        }

        /// <summary>지도 아이콘을 둘 칸: 막다른 방(곡괭이방 등)이 아닌 쪽.</summary>
        static DungeonCell IconCell(DungeonEdge edge)
        {
            bool aInner = edge.A.Piece == PieceKind.Room || edge.A.Piece == PieceKind.Hidden || edge.A.Piece == PieceKind.Office;
            return aInner ? edge.B : edge.A;
        }

        /// <summary>바위 위 지그재그 금(밝은 줄 + 가는 어두운 속줄). 빛을 받아 등잔 빛 안에서 보인다.</summary>
        void DrawCracks()
        {
            var e = _edge;
            Vector2[] main =
            {
                WorldProps.DoorLocal(e, -1.85f, 0.05f), WorldProps.DoorLocal(e, -1.1f, -0.18f), WorldProps.DoorLocal(e, -0.5f, 0.15f),
                WorldProps.DoorLocal(e, 0.1f, -0.1f), WorldProps.DoorLocal(e, 0.7f, 0.2f), WorldProps.DoorLocal(e, 1.3f, -0.12f),
                WorldProps.DoorLocal(e, 1.85f, 0.06f),
            };
            for (int i = 0; i < main.Length - 1; i++)
            {
                WorldProps.Stroke(transform, "Crack", main[i], main[i + 1], 0.09f, CrackColor, WorldProps.WallDetailOrder, false);
                WorldProps.Stroke(transform, "CrackCore", main[i], main[i + 1], 0.035f, CrackDark, WorldProps.WallDetailOrder + 1, false);
            }
            WorldProps.Stroke(transform, "Crack", main[2], WorldProps.DoorLocal(e, -0.3f, 0.4f), 0.07f, CrackColor, WorldProps.WallDetailOrder, false);
            WorldProps.Stroke(transform, "Crack", main[4], WorldProps.DoorLocal(e, 0.95f, 0.42f), 0.07f, CrackColor, WorldProps.WallDetailOrder, false);
            WorldProps.Stroke(transform, "Crack", main[3], WorldProps.DoorLocal(e, 0.28f, -0.4f), 0.07f, CrackColor, WorldProps.WallDetailOrder, false);
            WorldProps.Stroke(transform, "Crack", main[1], WorldProps.DoorLocal(e, -1.3f, -0.42f), 0.06f, CrackColor, WorldProps.WallDetailOrder, false);
        }

        void Smash()
        {
            if (_edge == null || _edge.Opened || !WorldProps.HasPickaxe) return;
            Vector2 center = _edge.DoorCenter;
            Sfx.Play(SfxKind.Pick);
            Sfx.Play(SfxKind.WallBreak);
            ScreenShake.Add(0.15f, 0.18f);
            var player = PlayerController.Instance;
            Vector2 dir = player ? center - player.Position : WorldProps.DoorNormal(_edge);
            WorldDebris.Burst(center, _edge.DoorSize, dir, RockDebris, Palette.Wall, 18);
            _edge.Open();
            DungeonEvents.RaiseNoise(center, NoiseRadius);
            var state = WorldProps.State;
            if (state != null) state.Complete(_id, DiscoveryKind.Shortcut, center, "금 간 벽");
            DungeonEvents.Say("금 간 벽을 깨고 지름길을 열었다");
        }
    }
}
