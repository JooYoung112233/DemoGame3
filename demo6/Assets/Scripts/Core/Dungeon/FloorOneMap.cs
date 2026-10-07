namespace Demo6.Core.Dungeon
{
    /// <summary>
    /// 1층 '입구 갱도'(3차 초안 2-3 뼈대). 칸 하나는 28×16유닛(가운데 기준 좌표, 안쪽은 대략 x ±13, y ±7).
    /// 개수: 마주침 4 + 둥지 1, 나무 궤짝 3, 쇠 궤짝 2, 광맥 1, 벽 등잔 4, 말뚝 2, 사건 1.
    /// 주 길 E → c1 → B → D → S(5칸, 처음 갈 수 있는 10칸의 50%). 고리 c1–A–c2–B, 곡괭이 뒤 c2–V–D–B.
    /// </summary>
    public static class FloorOneMap
    {
        public const int Floor = 1;
        public const string Name = "입구 갱도";

        /// <summary>
        /// 글자 지도. 1 = c1, 2 = c2. '-' '|' 열린 길, ':' 판자벽, '#' 금 간 벽, '=' 자물쇠 문.
        /// </summary>
        public const string Glyphs =
            "O=A-2-V\n" +
            "..|.|.#\n" +
            "E-1-B-D-S\n" +
            "..|.#.:\n" +
            "..T.K.H\n";

        public static FloorMap Build() => FloorMap.Parse(Floor, Name, Glyphs, Legend());

        static CellFeature F(FeatureKind kind, string id, float x, float y, string label = "", string param = "") =>
            new CellFeature { Kind = kind, Id = "f1." + id, Local = new Offset(x, y), Label = label, Param = param };

        static CellFeature Group(string id, float x, float y, int boars, int archers, int rats, GroupState state, float facing, string label) =>
            new CellFeature
            {
                Kind = FeatureKind.Group, Id = "f1." + id, Local = new Offset(x, y), Boars = boars, Archers = archers, Rats = rats,
                State = state, FacingDeg = facing, Label = label,
            };

        public static CellDef[] Legend() => new[]
        {
            new CellDef
            {
                // 승강장(돌로 쌓은 본갱). 원정마다 같은 돌방이라 이름·말뚝 글도 생성 지도(FloorGenerator)와 같다.
                Glyph = 'E', Id = "E", Name = "승강장", Piece = PieceKind.Entrance,
                Features = new[]
                {
                    F(FeatureKind.Stake, "E.stake", -8f, -2f, "승강장 말뚝"),
                    F(FeatureKind.WallLamp, "E.lamp", -5f, 7f, "벽 등잔"),
                },
            },
            new CellDef
            {
                // 첫 마주침: 멧돼지 1 + 굴쥐 3. 입구 쪽(왼쪽)에 등을 돌리고 자서 첫 기습을 배운다.
                Glyph = '1', Id = "c1", Name = "첫 공터", Piece = PieceKind.Clearing, Pillars = 0,
                Features = new[] { Group("c1.group", 3f, 0.5f, 1, 0, 3, GroupState.Sleep, 0f, "돌충이와 굴쥐") },
            },
            new CellDef
            {
                Glyph = 'A', Id = "A", Name = "쥐굴 공터", Piece = PieceKind.Clearing, Pillars = 1,
                Features = new[]
                {
                    F(FeatureKind.Nest, "A.nest", 4f, 1f, "굴쥐 둥지"),
                    F(FeatureKind.WoodChest, "A.wood", -10f, 5f, "나무 궤짝"),
                },
            },
            new CellDef
            {
                Glyph = '2', Id = "c2", Name = "버팀목 통로", Piece = PieceKind.Corridor,
                Features = new[] { F(FeatureKind.Lunchbox, "c2.lunchbox", -7f, 0f, "광부 도시락통") },
            },
            new CellDef
            {
                Glyph = 'V', Id = "V", Name = "광맥 공터", Piece = PieceKind.Clearing, Pillars = 2,
                Features = new[]
                {
                    Group("V.group", 1f, 2f, 1, 0, 2, GroupState.Eat, 270f, "돌충이와 굴쥐"),
                    F(FeatureKind.WoodChest, "V.wood", 10f, 5f, "나무 궤짝"),
                    F(FeatureKind.Ore, "V.ore", 9f, -5f, "광맥"),
                },
            },
            new CellDef
            {
                // 배우는 층이라 굴쥐만. 아래 K로 가는 금 간 벽이 처음부터 보인다.
                Glyph = 'B', Id = "B", Name = "갈림 공터", Piece = PieceKind.Clearing, Pillars = 3,
                Features = new[]
                {
                    Group("B.group", -1f, 1.5f, 0, 0, 4, GroupState.Eat, 90f, "굴쥐 무리"),
                    F(FeatureKind.WallLamp, "B.lamp", 6f, 7f, "벽 등잔"),
                },
            },
            new CellDef
            {
                // 다래가 몰래 그린 분필 그림 3개가 아래 판자벽(숨은 방 H)을 가리킨다.
                Glyph = 'D', Id = "D", Name = "분필 갈림길", Piece = PieceKind.Corridor,
                Features = new[]
                {
                    F(FeatureKind.WallLamp, "D.lamp", -6f, 3f, "벽 등잔"),
                    F(FeatureKind.Chalk, "D.chalk1", -8f, -1f, "분필 그림", "-39"),
                    F(FeatureKind.Chalk, "D.chalk2", 8f, -1f, "분필 그림", "-141"),
                    F(FeatureKind.Chalk, "D.chalk3", 0f, 2f, "분필 그림", "-90"),
                },
            },
            new CellDef
            {
                Glyph = 'S', Id = "S", Name = "계단 앞", Piece = PieceKind.StairsRoom,
                Features = new[]
                {
                    F(FeatureKind.Stake, "S.stake", -8f, 2f, "계단 말뚝"),
                    F(FeatureKind.WallLamp, "S.lamp", -3f, 7f, "벽 등잔"),
                    F(FeatureKind.Stairs, "S.stairs", 8f, 0f, "2층 계단"),
                },
            },
            new CellDef
            {
                // 막다른 공터: 위(c1)에서 내려오면 등 뒤로 들어간다.
                Glyph = 'T', Id = "T", Name = "막다른 공터", Piece = PieceKind.Clearing, Pillars = 4,
                Features = new[]
                {
                    Group("T.group", 0f, -1f, 1, 0, 2, GroupState.Sleep, 270f, "돌충이와 굴쥐"),
                    F(FeatureKind.Nameplate, "T.nameplate", -9f, -4f, "광부 명패"),
                    F(FeatureKind.WoodChest, "T.wood", 9f, -4f, "나무 궤짝"),
                },
            },
            new CellDef
            {
                // 곡괭이방(금 간 벽 뒤). 말뚝 ②에서 3칸, 무리 B를 지나야 한다.
                Glyph = 'K', Id = "K", Name = "곡괭이방", Piece = PieceKind.Room,
                Features = new[]
                {
                    F(FeatureKind.IronChest, "K.iron", 0f, -2f, "쇠 궤짝"),
                    F(FeatureKind.Note, "K.note1", -4f, -3f, "쪽지 ①", "note1"),
                },
            },
            new CellDef
            {
                // 숨은 방(판자벽 뒤). 쓰러진 광부 자리에서 곡괭이, 쇠 궤짝은 희귀 이상 무기 보장(2차 7-4 첫 상자 보장을 옮김).
                Glyph = 'H', Id = "H", Name = "숨은 방", Piece = PieceKind.Hidden,
                Features = new[]
                {
                    F(FeatureKind.Pickaxe, "H.pickaxe", -3f, -2f, "쓰러진 광부의 곡괭이"),
                    F(FeatureKind.IronChest, "H.iron", 3f, -2f, "쇠 궤짝", "rare-weapon"),
                },
            },
            new CellDef
            {
                // 광업소 사무실. 열쇠는 약 78분에 받으므로 M0b에서는 잠긴 채로 둔다.
                Glyph = 'O', Id = "O", Name = "광업소 사무실", Piece = PieceKind.Office,
                Features = new[]
                {
                    F(FeatureKind.Safe, "O.safe", -5f, 0f, "광업소 금고"),
                    F(FeatureKind.Note, "O.note2", 4f, -3f, "쪽지 ②", "note2"),
                },
            },
        };
    }
}
