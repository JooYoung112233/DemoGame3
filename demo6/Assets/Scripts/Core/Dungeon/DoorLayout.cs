using System;

namespace Demo6.Core.Dungeon
{
    /// <summary>
    /// 문 자리 비틀기(기획/시야와-문-1차.md 2-1~2-4). 칸 28×16·문 폭 4는 그대로 두고 문 가운데만 벽을 따라 정수 유닛만큼 옮긴다.
    /// 길 하나의 비틀기를 양쪽 칸이 함께 쓴다(문틈은 하나). 방향: 오른쪽 길(세로 벽)은 위(+y), 위쪽 길(가로 벽)은 오른쪽(+x)이 양수다.
    /// 돌방(MapAnchors.IsFixed)에 닿은 문·판자벽·자물쇠 문은 늘 가운데다(2-2). 금 간 벽은 옮긴다.
    /// 생성기의 난수 흐름을 쓰지 않는다(2-3): 다 지은 지도와 씨앗만 보고 정하므로 고른 씨앗 지도(1층 손 지도 씨앗 0, 2층 씨앗 30)의
    /// 칸·길·내용물과 생성기 시험이 그대로다. 같은 지도·같은 씨앗이면 늘 같은 문 자리다.
    /// 겹침 검사(2-4)는 지도에 실제로 놓인 자리 표시를 읽으므로 자리 표시가 더해지거나 옮겨져도 문이 저절로 비켜 간다.
    /// 세계에 놓는 일은 DungeonWorld가 ShiftAll로 읽어 한다(10-3).
    /// </summary>
    public static class DoorLayout
    {
        /// <summary>문 폭(그대로).</summary>
        public const float DoorWidth = 4f;
        /// <summary>공터·계단 앞의 왼·오른 벽(세로, 길이 16) 한도 ±3.</summary>
        public const float SideWallLimit = 3f;
        /// <summary>공터·계단 앞의 위·아래 벽(가로, 길이 28) 한도 ±5.</summary>
        public const float EndWallLimit = 5f;
        /// <summary>통로·막다른 방·숨은 방 한도 ±1(문 앞 길 폭 6 안에 폭 4 문이 들어가는 만큼).</summary>
        public const float NarrowLimit = 1f;
        /// <summary>그 밖 자리 표시(궤짝·벽 등잔·말뚝·광맥·계단·사건·작은 물건·보스 자리)에서 떨어질 거리(= PieceSlots.DoorClearance).</summary>
        public const float Clearance = 3f;
        /// <summary>무리·둥지 가운데에서 떨어질 거리(졸개가 둘레 1.2~1.8에 퍼진다).</summary>
        public const float BigClearance = 4f;
        /// <summary>분필 그림·바닥 긁은 글에서 떨어질 거리(바닥 그림이라 밟아도 된다).</summary>
        public const float DecalClearance = 1.5f;
        /// <summary>조각 기둥(PieceSlots.PillarsIn)에서 떨어질 거리(기둥 1.2 + 지나갈 틈).</summary>
        public const float PillarClearance = 2.5f;
        /// <summary>문틈 끝과 벽 모서리 사이 여유(문틈은 늘 벽 안).</summary>
        public const float CornerMargin = 1.5f;

        /// <summary>걸을 수 있는 안쪽 반폭·반높이(칸 28×16, 벽 두께 1을 경계 가운데에 놓음 → x ±13.5, y ±7.5).</summary>
        public const float InnerHalfWidth = 13.5f;
        public const float InnerHalfHeight = 7.5f;

        /// <summary>칸 가운데에서 경계까지(문 가운데 자리, PieceSlots.DoorCenter와 같다).</summary>
        const float CellHalfWidth = 14f;
        const float CellHalfHeight = 8f;
        /// <summary>거리 비교 여유(같은 거리는 통과).</summary>
        const float Eps = 1e-4f;
        /// <summary>
        /// 섞기 고정 소금(다른 씨앗 쓰임새와 겹치지 않게). 1층 손 지도(씨앗 0)에서 크게 옮길 수 있는 공터 문 셋이 모두 눈에 띄게 비켜 서고
        /// (첫 공터|쥐굴 공터 −3, 첫 공터–갈림 공터 −2, 막다른 공터|첫 공터 +3), 통로 문 7개 가운데 6개가 1만큼 비키는 값으로 골랐다(2-6, 11-2 확인 1).
        /// 바꾸면 손 지도 문 자리가 모두 바뀐다.
        /// </summary>
        const ulong Salt = 0xFF5721F589FE4393UL;

        /// <summary>
        /// 조각·벽 방향별 한도(2-1 표). 공터·계단 앞은 옆벽 3·위아래 5, 통로·막다른 방·숨은 방은 1,
        /// 승강장·광업소 사무실·보스방·보스방 앞 쉼터(돌방)는 0. 모르는 조각도 0.
        /// </summary>
        public static float Limit(PieceKind piece, Side side)
        {
            switch (piece)
            {
                case PieceKind.Clearing:
                case PieceKind.StairsRoom:
                    return IsSideWall(side) ? SideWallLimit : EndWallLimit;
                case PieceKind.Corridor:
                case PieceKind.Room:
                case PieceKind.Hidden:
                    return NarrowLimit;
                case PieceKind.Entrance:
                case PieceKind.Office:
                case PieceKind.BossRoom:
                case PieceKind.BossFront:
                    return 0f;
                default:
                    return 0f;
            }
        }

        /// <summary>늘 가운데인 길인가(2-2): 돌방에 닿음, 판자벽, 자물쇠 문. 길이 없으면 참.</summary>
        public static bool Pinned(MapEdge edge) =>
            edge == null || MapAnchors.IsFixed(edge.A) || MapAnchors.IsFixed(edge.B) ||
            edge.Kind == EdgeKind.Plank || edge.Kind == EdgeKind.Locked;

        /// <summary>길의 한도: 늘 가운데면 0, 아니면 두 칸 한도 가운데 작은 값(공터–통로 ±1, 공터–공터 ±3 또는 ±5).</summary>
        public static float EdgeLimit(MapEdge edge)
        {
            if (edge == null || edge.A == null || edge.B == null || edge.A.Def == null || edge.B.Def == null) return 0f;
            if (Pinned(edge)) return 0f;
            return Math.Min(Limit(edge.A.Piece, edge.SideFromA), Limit(edge.B.Piece, MapEdge.Opposite(edge.SideFromA)));
        }

        /// <summary>비튼 문 가운데(칸 가운데 기준, 2-4): 오른쪽 (14, s), 위 (s, 8), 왼쪽 (−14, s), 아래 (s, −8).</summary>
        public static Offset DoorCenter(Side side, float shift)
        {
            switch (side)
            {
                case Side.Right: return new Offset(CellHalfWidth, shift);
                case Side.Up: return new Offset(shift, CellHalfHeight);
                case Side.Left: return new Offset(-CellHalfWidth, shift);
                default: return new Offset(shift, -CellHalfHeight);
            }
        }

        /// <summary>왼·오른 벽(세로 벽, 길이 16)인가.</summary>
        public static bool IsSideWall(Side side) => side == Side.Left || side == Side.Right;

        /// <summary>
        /// 문틈이 벽 안에 있는가(2-1): 세로 벽 |s| + 2 ≤ 7.5 − 1.5, 가로 벽 |s| + 2 ≤ 13.5 − 1.5.
        /// </summary>
        public static bool InWall(Side side, float shift)
        {
            float half = IsSideWall(side) ? InnerHalfHeight : InnerHalfWidth;
            return Math.Abs(shift) + DoorWidth * 0.5f <= half - CornerMargin + Eps;
        }

        /// <summary>
        /// 자리 표시 종류별로 문 가운데에서 떨어질 거리(2-4 표): 무리·둥지 4, 분필·긁은 글 1.5, 그 밖 3.
        /// 새 종류(낙석·가시 덫 같은 '1·2층 탐험 맛'의 덧칠)는 저절로 3이다.
        /// </summary>
        public static float ClearanceFor(FeatureKind kind)
        {
            switch (kind)
            {
                case FeatureKind.Group:
                case FeatureKind.Nest:
                    return BigClearance;
                case FeatureKind.Chalk:
                case FeatureKind.Scrawl:
                    return DecalClearance;
                default:
                    return Clearance;
            }
        }

        /// <summary>
        /// 이 칸의 그 쪽 문을 shift만큼 비틀어도 되는가(2-4): 문틈이 벽 안이고, 비튼 문 가운데에서 그 칸 자리 표시(ClearanceFor)와
        /// 조각 기둥(PieceSlots.PillarsIn, 2.5)이 모두 떨어져 있다. 같은 거리는 통과. 칸이 없으면 벽 안인지만 본다.
        /// </summary>
        public static bool Clear(MapCell cell, Side side, float shift)
        {
            if (!InWall(side, shift)) return false;
            if (cell == null || cell.Def == null) return true;
            var door = DoorCenter(side, shift);
            var features = cell.Def.Features;
            if (features != null)
            {
                foreach (var f in features)
                {
                    if (f == null) continue;
                    if (Closer(f.Local, door, ClearanceFor(f.Kind))) return false;
                }
            }
            foreach (var p in PieceSlots.PillarsIn(cell.Def.Piece, cell.Def.Pillars))
                if (Closer(p, door, PillarClearance)) return false;
            return true;
        }

        /// <summary>
        /// 길 하나의 비틀기(2-3, 정수). 후보 −L..+L(L = EdgeLimit) 가운데 A칸(SideFromA)·B칸(반대쪽) 모두 Clear인 것만 남기고,
        /// 번호 = ExpeditionSeeds.Mix(씨앗 ^ 고정 소금 ^ (층·A칸 x·y·방향을 곱해 섞은 값)) % 통과한 수. 0(가운데)도 후보다.
        /// 통과한 후보가 없으면 0. 생성기 난수 흐름은 쓰지 않는다.
        /// </summary>
        public static float Shift(FloorMap map, MapEdge edge, ulong seed)
        {
            int limit = (int)Math.Floor(EdgeLimit(edge) + Eps);
            if (limit <= 0) return 0f;
            Side sideA = edge.SideFromA;
            Side sideB = MapEdge.Opposite(sideA);
            int passed = 0;
            for (int s = -limit; s <= limit; s++)
                if (Fits(edge, sideA, sideB, s)) passed++;
            if (passed == 0) return 0f;

            int floor = map != null ? map.Floor : 0;
            ulong h = ExpeditionSeeds.Mix(seed ^ Salt ^ Key(floor, edge.A.X, edge.A.Y, sideA));
            int pick = (int)(h % (ulong)passed);
            for (int s = -limit; s <= limit; s++)
            {
                if (!Fits(edge, sideA, sideB, s)) continue;
                if (pick == 0) return s;
                pick--;
            }
            return 0f;
        }

        /// <summary>지도 전체 비틀기(map.Edges 차례). 지도가 없으면 빈 배열.</summary>
        public static float[] ShiftAll(FloorMap map, ulong seed)
        {
            if (map == null) return Array.Empty<float>();
            var edges = map.Edges;
            var result = new float[edges.Count];
            for (int i = 0; i < result.Length; i++) result[i] = Shift(map, edges[i], seed);
            return result;
        }

        static bool Fits(MapEdge edge, Side sideA, Side sideB, int shift) =>
            Clear(edge.A, sideA, shift) && Clear(edge.B, sideB, shift);

        static bool Closer(Offset a, Offset b, float distance)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return dx * dx + dy * dy < distance * distance - Eps;
        }

        /// <summary>층·칸 자리·방향을 곱해 섞은 값(같은 씨앗에서도 길마다 다른 번호).</summary>
        static ulong Key(int floor, int x, int y, Side side)
        {
            unchecked
            {
                return (ulong)(uint)floor * 0xE7037ED1A0B428DBUL
                    ^ (ulong)(uint)x * 0x8EBC6AF09C88C6E3UL
                    ^ (ulong)(uint)y * 0x589965CC75374CC3UL
                    ^ ((ulong)(uint)(int)side + 1UL) * 0x1D8E4E27C47D124FUL;
            }
        }
    }
}
