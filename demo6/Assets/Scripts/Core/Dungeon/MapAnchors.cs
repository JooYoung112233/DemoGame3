namespace Demo6.Core.Dungeon
{
    /// <summary>
    /// 권양기 말뚝의 역할(매판 새 탐험 1차 2-3·2-4). 칸 조각 종류로 정한다(자리 표시에 따로 적지 않는다).
    /// </summary>
    public enum StakeRole
    {
        /// <summary>승강장 말뚝(입구 조각). 켜면 영구, 바구니가 닿는 출발점.</summary>
        Landing,
        /// <summary>계단 앞 말뚝(계단 조각). 이번 원정에만 켜짐, 켜면 아래층 승강장까지 줄이 닿음(영구).</summary>
        StairsFront,
        /// <summary>가운데 말뚝(5~10층). 이번 원정에만 켜짐. 5·10층은 보스 앞 쉼터라 영구(첫 시험판에는 없음).</summary>
        Middle,
        /// <summary>보스방 앞 쉼터 말뚝(오우거 굴 P, 전투·보스 문서 3-8). 처음 닿으면 켜지고 영구(꾸러미 BossStakes, BossLedger.LightStake).</summary>
        BossFront,
    }

    /// <summary>
    /// 층마다 고정인 칸(앵커)과 말뚝 찾기(매판 새 탐험 1차 2-4·2-5 차례 1, 3-3 '하드코딩 "E"·"f1.E.stake" 없애기').
    /// 칸 id 대신 조각 종류로 찾으므로 손 지도와 생성 지도에 똑같이 쓴다.
    /// </summary>
    public static class MapAnchors
    {
        /// <summary>승강장 칸(입구 조각). 층마다 하나. 없으면 null.</summary>
        public static MapCell FindLanding(FloorMap map) => FirstOf(map, PieceKind.Entrance);

        /// <summary>랜드마크 칸(1층 광업소 사무실 조각). 없으면 null.</summary>
        public static MapCell FindLandmark(FloorMap map) => FirstOf(map, PieceKind.Office);

        /// <summary>계단이 있는 칸. 없으면 계단 조각 칸, 그것도 없으면 null.</summary>
        public static MapCell FindStairsCell(FloorMap map)
        {
            if (map == null) return null;
            foreach (var c in map.Cells)
                if (c.Has(FeatureKind.Stairs)) return c;
            return FirstOf(map, PieceKind.StairsRoom);
        }

        /// <summary>숨은 방 칸(판자벽 뒤). 없으면 null.</summary>
        public static MapCell FindHidden(FloorMap map) => FirstOf(map, PieceKind.Hidden);

        /// <summary>보스방 칸(오우거 굴 X). 없으면 null.</summary>
        public static MapCell FindBossRoom(FloorMap map) => FirstOf(map, PieceKind.BossRoom);

        /// <summary>보스방 앞 쉼터 칸(오우거 굴 P, 굴 앞 말뚝). 없으면 null.</summary>
        public static MapCell FindBossFront(FloorMap map) => FirstOf(map, PieceKind.BossFront);

        /// <summary>승강장 말뚝 자리 표시. 없으면 null.</summary>
        public static CellFeature LandingStake(FloorMap map) => StakeIn(FindLanding(map));

        /// <summary>칸 안 첫 말뚝 자리 표시. 없으면 null.</summary>
        public static CellFeature StakeIn(MapCell cell)
        {
            if (cell == null) return null;
            foreach (var f in cell.Features)
                if (f.Kind == FeatureKind.Stake) return f;
            return null;
        }

        /// <summary>이 칸에 놓인 말뚝의 역할.</summary>
        public static StakeRole RoleOf(MapCell cell) => RoleOf(cell != null ? cell.Piece : PieceKind.Corridor);

        public static StakeRole RoleOf(PieceKind piece)
        {
            switch (piece)
            {
                case PieceKind.Entrance: return StakeRole.Landing;
                case PieceKind.StairsRoom: return StakeRole.StairsFront;
                case PieceKind.BossFront: return StakeRole.BossFront;
                default: return StakeRole.Middle;
            }
        }

        /// <summary>원정마다 바뀌지 않는 돌 칸인가(승강장·랜드마크·보스방·보스방 앞 쉼터).</summary>
        public static bool IsFixed(MapCell cell) => cell != null && IsFixed(cell.Piece);

        public static bool IsFixed(PieceKind piece) =>
            piece == PieceKind.Entrance || piece == PieceKind.Office || piece == PieceKind.BossRoom || piece == PieceKind.BossFront;

        static MapCell FirstOf(FloorMap map, PieceKind piece)
        {
            if (map == null) return null;
            foreach (var c in map.Cells)
                if (c.Piece == piece) return c;
            return null;
        }
    }
}
