using System.Collections.Generic;
using Demo6.Core.Dungeon;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Demo6.Game
{
    /// <summary>던전 칸 하나(28×16유닛). 글자 지도 칸에 월드 좌표와 문을 붙인다.</summary>
    public sealed class DungeonCell
    {
        public MapCell Map;
        public Rect Bounds;
        public Vector2 Center;
        public readonly List<DungeonEdge> Edges = new List<DungeonEdge>();

        public string Id => Map.Id;
        public string Name => Map.Def.Name;
        public PieceKind Piece => Map.Piece;
        public bool Visited;

        /// <summary>칸 안쪽(벽 두께를 뺀 걸을 수 있는 범위).</summary>
        public Rect Inner => new Rect(Bounds.xMin + DungeonWorld.WallThickness * 0.5f, Bounds.yMin + DungeonWorld.WallThickness * 0.5f,
            Bounds.width - DungeonWorld.WallThickness, Bounds.height - DungeonWorld.WallThickness);

        /// <summary>무리의 감지·추격·돌아가기 경계: 칸 안쪽 + 문 1유닛(3차 초안 2-6).</summary>
        public Rect Territory => new Rect(Bounds.xMin - 1f, Bounds.yMin - 1f, Bounds.width + 2f, Bounds.height + 2f);

        public Vector2 World(Offset local) => Center + new Vector2(local.X, local.Y);

        public bool Contains(Vector2 p) => Bounds.Contains(p);

        public DungeonEdge EdgeOn(Side side)
        {
            foreach (var e in Edges)
                if (e.SideFrom(this) == side) return e;
            return null;
        }
    }

    /// <summary>칸 사이 문(폭 4). 열린 길이 아니면 막는 물체(판자벽·금 간 벽·자물쇠 문)가 문틈을 채운다.</summary>
    public sealed class DungeonEdge
    {
        public MapEdge Map;
        public DungeonCell A;
        public DungeonCell B;
        public Vector2 DoorCenter;
        /// <summary>문틈 크기(벽 방향 두께 × 폭 4).</summary>
        public Vector2 DoorSize;
        public GameObject Blocker;
        public bool Opened;

        public EdgeKind Kind => Map.Kind;
        public DungeonCell Other(DungeonCell c) => c == A ? B : A;
        public Side SideFrom(DungeonCell c) => Map.SideFrom(c.Map);

        /// <summary>막힌 길을 연다(막는 물체를 지우고 EdgeOpened를 알림).</summary>
        public void Open()
        {
            if (Opened) return;
            Opened = true;
            if (Blocker) Object.Destroy(Blocker);
            Blocker = null;
            DungeonEvents.RaiseEdgeOpened(this);
        }
    }

    /// <summary>
    /// 글자 지도에서 칸 바닥·벽·문·조각 안쪽 벽을 만든다(3차 초안 2-1). 칸 (x, y)의 가운데는 (28x, 16y).
    /// 벽 두께 1, 문은 칸 네 변 가운데 폭 4. 벽과 기둥은 등잔 빛에 그림자를 드리운다.
    /// 그림(기획/다크판타지-분위기-1차.md '땅·벽'): 바닥은 거친 흙·돌, 벽은 돌 블록·모르타르, 채운 바위는 거친 바위, 기둥은 나무 버팀목. 칸 구조·충돌·문은 그대로다.
    /// </summary>
    public sealed class DungeonWorld
    {
        public const float CellWidth = 28f;
        public const float CellHeight = 16f;
        public const float WallThickness = 1f;
        public const float DoorWidth = 4f;
        /// <summary>통로 조각 길 폭.</summary>
        public const float CorridorWidth = 6f;

        public FloorMap Map { get; private set; }
        public Rect Bounds { get; private set; }
        public IReadOnlyList<DungeonCell> Cells => _cells;
        public IReadOnlyList<DungeonEdge> Edges => _edges;

        readonly List<DungeonCell> _cells = new List<DungeonCell>();
        readonly List<DungeonEdge> _edges = new List<DungeonEdge>();
        readonly Dictionary<MapCell, DungeonCell> _byMap = new Dictionary<MapCell, DungeonCell>();
        /// <summary>벽·바위·기둥·문 막이 사각형(바닥 구석 그늘용).</summary>
        readonly List<Rect> _wallRects = new List<Rect>();
        DungeonCell[,] _grid;
        Transform _root;

        public static DungeonWorld Build(FloorMap map, Transform parent)
        {
            var w = new DungeonWorld { Map = map };
            w._root = new GameObject("Floor " + map.Floor).transform;
            w._root.SetParent(parent, false);
            w._grid = new DungeonCell[map.Width, map.Height];
            foreach (var mc in map.Cells)
            {
                var c = new DungeonCell
                {
                    Map = mc,
                    Center = new Vector2(mc.X * CellWidth, mc.Y * CellHeight),
                };
                c.Bounds = new Rect(c.Center.x - CellWidth * 0.5f, c.Center.y - CellHeight * 0.5f, CellWidth, CellHeight);
                w._cells.Add(c);
                w._byMap[mc] = c;
                w._grid[mc.X, mc.Y] = c;
            }
            foreach (var me in map.Edges)
            {
                var a = w._byMap[me.A];
                var b = w._byMap[me.B];
                var e = new DungeonEdge { Map = me, A = a, B = b, Opened = me.Kind == EdgeKind.Open };
                if (me.SideFromA == Side.Right)
                {
                    e.DoorCenter = new Vector2(a.Bounds.xMax, a.Center.y);
                    e.DoorSize = new Vector2(WallThickness, DoorWidth);
                }
                else
                {
                    e.DoorCenter = new Vector2(a.Center.x, a.Bounds.yMax);
                    e.DoorSize = new Vector2(DoorWidth, WallThickness);
                }
                a.Edges.Add(e);
                b.Edges.Add(e);
                w._edges.Add(e);
            }
            var min = new Vector2(-CellWidth * 0.5f, -CellHeight * 0.5f);
            w.Bounds = new Rect(min, new Vector2(map.Width * CellWidth, map.Height * CellHeight));
            w.BuildGeometry();
            return w;
        }

        public DungeonCell Find(string id)
        {
            foreach (var c in _cells)
                if (c.Id == id) return c;
            return null;
        }

        public DungeonCell CellAt(Vector2 p)
        {
            int x = Mathf.RoundToInt(p.x / CellWidth);
            int y = Mathf.RoundToInt(p.y / CellHeight);
            if (x < 0 || y < 0 || x >= Map.Width || y >= Map.Height) return null;
            var c = _grid[x, y];
            return c != null && c.Bounds.Contains(p) ? c : null;
        }

        void BuildGeometry()
        {
            var cellRoots = new Transform[_cells.Count];
            for (int i = 0; i < _cells.Count; i++)
            {
                var c = _cells[i];
                var cellRoot = new GameObject("Cell " + c.Id + " (" + c.Name + ")").transform;
                cellRoot.SetParent(_root, false);
                cellRoots[i] = cellRoot;
                BuildBoundary(c, Side.Right, cellRoot);
                BuildBoundary(c, Side.Up, cellRoot);
                if (Neighbor(c, Side.Left) == null) BuildBoundary(c, Side.Left, cellRoot);
                if (Neighbor(c, Side.Down) == null) BuildBoundary(c, Side.Down, cellRoot);
                BuildPiece(c, cellRoot);
            }
            foreach (var e in _edges)
            {
                if (e.Kind == EdgeKind.Open) continue;
                e.Blocker = DungeonContent.CreateBlocker(e);
                _wallRects.Add(new Rect(e.DoorCenter - e.DoorSize * 0.5f, e.DoorSize));
            }
            // 바닥은 벽을 다 세운 뒤에 그린다(벽·바위 가까이 구석 그늘을 넣으려고).
            for (int i = 0; i < _cells.Count; i++) BuildFloor(_cells[i], cellRoots[i]);
        }

        /// <summary>
        /// 칸 바닥: 바둑판 대신 거친 흙·돌 절차 텍스처(기획/다크판타지-분위기-1차.md '땅·벽'). 흙 결은 월드 좌표에 맞춰 문틈에서 이웃 칸과 이어지고,
        /// 칸 시드로 진흙·바닥돌·자갈·그을음 자리가 칸마다 다르다. 빛을 받는 기본 재질이라 등잔 빛 안에서만 보인다.
        /// </summary>
        void BuildFloor(DungeonCell c, Transform cellRoot)
        {
            var floor = new GameObject("Floor");
            floor.transform.SetParent(cellRoot, false);
            floor.transform.position = c.Center;
            var fs = floor.AddComponent<SpriteRenderer>();
            var sprite = ShapeSprites.DirtFloor(c.Bounds, CellSeed(c), _wallRects);
            fs.sprite = sprite;
            fs.sortingOrder = -1000;
            // 픽셀 반올림이 있어도 그림이 칸 크기에 꼭 맞게.
            Vector2 native = sprite.bounds.size;
            floor.transform.localScale = new Vector3(CellWidth / Mathf.Max(0.01f, native.x), CellHeight / Mathf.Max(0.01f, native.y), 1f);
            RuntimeAssetOwner.Own(floor, sprite);
        }

        /// <summary>칸마다 다르고 실행마다 같은 시드(층 + 칸 id). 바닥 무늬와 바닥 장식(DungeonDecor)이 같이 쓴다.</summary>
        public int CellSeed(DungeonCell c) => ShapeSprites.Seed(c.Id, Map.Floor * 7919);

        DungeonCell Neighbor(DungeonCell c, Side side)
        {
            int x = c.Map.X + (side == Side.Right ? 1 : side == Side.Left ? -1 : 0);
            int y = c.Map.Y + (side == Side.Up ? 1 : side == Side.Down ? -1 : 0);
            if (x < 0 || y < 0 || x >= Map.Width || y >= Map.Height) return null;
            return _grid[x, y];
        }

        /// <summary>칸의 한 변 벽. 이웃과 길로 이어져 있으면 가운데 폭 4를 비운다(막는 물체는 따로).</summary>
        void BuildBoundary(DungeonCell c, Side side, Transform parent)
        {
            bool door = c.EdgeOn(side) != null;
            float t = WallThickness;
            if (side == Side.Right || side == Side.Left)
            {
                float x = side == Side.Right ? c.Bounds.xMax : c.Bounds.xMin;
                float y0 = c.Bounds.yMin - t * 0.5f;
                float y1 = c.Bounds.yMax + t * 0.5f;
                if (!door) Wall(parent, "Wall " + side, new Vector2(x, c.Center.y), new Vector2(t, y1 - y0), Palette.Wall, false);
                else
                {
                    float gap0 = c.Center.y - DoorWidth * 0.5f;
                    float gap1 = c.Center.y + DoorWidth * 0.5f;
                    Wall(parent, "Wall " + side + " a", new Vector2(x, (y0 + gap0) * 0.5f), new Vector2(t, gap0 - y0), Palette.Wall, false);
                    Wall(parent, "Wall " + side + " b", new Vector2(x, (gap1 + y1) * 0.5f), new Vector2(t, y1 - gap1), Palette.Wall, false);
                }
            }
            else
            {
                float y = side == Side.Up ? c.Bounds.yMax : c.Bounds.yMin;
                float x0 = c.Bounds.xMin - t * 0.5f;
                float x1 = c.Bounds.xMax + t * 0.5f;
                if (!door) Wall(parent, "Wall " + side, new Vector2(c.Center.x, y), new Vector2(x1 - x0, t), Palette.Wall, false);
                else
                {
                    float gap0 = c.Center.x - DoorWidth * 0.5f;
                    float gap1 = c.Center.x + DoorWidth * 0.5f;
                    Wall(parent, "Wall " + side + " a", new Vector2((x0 + gap0) * 0.5f, y), new Vector2(gap0 - x0, t), Palette.Wall, false);
                    Wall(parent, "Wall " + side + " b", new Vector2((gap1 + x1) * 0.5f, y), new Vector2(x1 - gap1, t), Palette.Wall, false);
                }
            }
        }

        /// <summary>조각 안쪽 벽. 공터는 기둥, 통로는 문과 가운데를 잇는 폭 6 길만 남기고, 막다른 방은 문 없는 쪽을 좁힌다.</summary>
        void BuildPiece(DungeonCell c, Transform parent)
        {
            var inner = c.Inner;
            switch (c.Piece)
            {
                case PieceKind.Clearing:
                    foreach (var p in PillarsFor(c))
                        Wall(parent, "Pillar", c.Center + p, new Vector2(1.2f, 1.2f), Palette.Pillar, true);
                    break;
                case PieceKind.Corridor:
                {
                    float h = CorridorWidth * 0.5f;
                    float[] xs = { inner.xMin, c.Center.x - h, c.Center.x + h, inner.xMax };
                    float[] ys = { inner.yMin, c.Center.y - h, c.Center.y + h, inner.yMax };
                    for (int ix = 0; ix < 3; ix++)
                    for (int iy = 0; iy < 3; iy++)
                    {
                        if (ix == 1 && iy == 1) continue;
                        bool arm = (ix == 1) != (iy == 1);
                        if (arm)
                        {
                            Side side = ix == 0 ? Side.Left : ix == 2 ? Side.Right : iy == 0 ? Side.Down : Side.Up;
                            if (c.EdgeOn(side) != null) continue;
                        }
                        var r = Rect.MinMaxRect(xs[ix], ys[iy], xs[ix + 1], ys[iy + 1]);
                        Wall(parent, "Rock", r.center, r.size, Palette.Wall, true);
                    }
                    break;
                }
                case PieceKind.Room:
                case PieceKind.Office:
                    RoomMargins(c, parent, 7f, 4.5f);
                    break;
                case PieceKind.Hidden:
                    RoomMargins(c, parent, 6f, 4f);
                    break;
                case PieceKind.Entrance:
                case PieceKind.StairsRoom:
                    // 입구·계단 앞은 트인 방. 구석 버팀목 기둥 두 개만.
                    Wall(parent, "Pillar", c.Center + new Vector2(-10f, -5f), new Vector2(1.2f, 1.2f), Palette.Pillar, true);
                    Wall(parent, "Pillar", c.Center + new Vector2(10f, 5f), new Vector2(1.2f, 1.2f), Palette.Pillar, true);
                    break;
            }
        }

        /// <summary>문 없는 쪽만 안으로 좁힌다(halfW, halfH = 방 반폭·반높이).</summary>
        void RoomMargins(DungeonCell c, Transform parent, float halfW, float halfH)
        {
            var inner = c.Inner;
            float l = c.EdgeOn(Side.Left) != null ? inner.xMin : c.Center.x - halfW;
            float r = c.EdgeOn(Side.Right) != null ? inner.xMax : c.Center.x + halfW;
            float b = c.EdgeOn(Side.Down) != null ? inner.yMin : c.Center.y - halfH;
            float t = c.EdgeOn(Side.Up) != null ? inner.yMax : c.Center.y + halfH;
            void Fill(Rect rect)
            {
                if (rect.width > 0.05f && rect.height > 0.05f) Wall(parent, "Rock", rect.center, rect.size, Palette.Wall, true);
            }
            // 문이 있는 쪽은 문 폭 4 + 양옆 여유를 남기고 나머지를 채운다.
            Fill(Rect.MinMaxRect(inner.xMin, inner.yMin, l, inner.yMax));
            Fill(Rect.MinMaxRect(r, inner.yMin, inner.xMax, inner.yMax));
            Fill(Rect.MinMaxRect(l, inner.yMin, r, b));
            Fill(Rect.MinMaxRect(l, t, r, inner.yMax));
            // 문 쪽이 트여 있으면 문틈 양옆을 막아 문에서 방으로 들어오는 길(폭 6)만 남긴다.
            float h = CorridorWidth * 0.5f;
            if (c.EdgeOn(Side.Up) != null && t >= inner.yMax - 0.01f && halfH < inner.height * 0.5f)
            {
                float top = c.Center.y + halfH;
                Fill(Rect.MinMaxRect(l, top, c.Center.x - h, inner.yMax));
                Fill(Rect.MinMaxRect(c.Center.x + h, top, r, inner.yMax));
            }
            if (c.EdgeOn(Side.Down) != null && b <= inner.yMin + 0.01f && halfH < inner.height * 0.5f)
            {
                float bottom = c.Center.y - halfH;
                Fill(Rect.MinMaxRect(l, inner.yMin, c.Center.x - h, bottom));
                Fill(Rect.MinMaxRect(c.Center.x + h, inner.yMin, r, bottom));
            }
            if (c.EdgeOn(Side.Left) != null && l <= inner.xMin + 0.01f && halfW < inner.width * 0.5f)
            {
                float left = c.Center.x - halfW;
                Fill(Rect.MinMaxRect(inner.xMin, b, left, c.Center.y - h));
                Fill(Rect.MinMaxRect(inner.xMin, c.Center.y + h, left, t));
            }
            if (c.EdgeOn(Side.Right) != null && r >= inner.xMax - 0.01f && halfW < inner.width * 0.5f)
            {
                float right = c.Center.x + halfW;
                Fill(Rect.MinMaxRect(right, b, inner.xMax, c.Center.y - h));
                Fill(Rect.MinMaxRect(right, c.Center.y + h, inner.xMax, t));
            }
        }

        /// <summary>공터 기둥 2~4개(칸마다 다르게, 자리 표시와 문 앞을 피함).</summary>
        static IEnumerable<Vector2> PillarsFor(DungeonCell c)
        {
            switch (c.Id)
            {
                case "c1": return new[] { new Vector2(-6f, 2.5f), new Vector2(6.5f, -2.5f), new Vector2(1f, 4.5f) };
                case "A": return new[] { new Vector2(-5f, -3f), new Vector2(-2f, 4f), new Vector2(9f, -3.5f) };
                case "V": return new[] { new Vector2(-6f, -3f), new Vector2(5f, 4.5f) };
                case "B": return new[] { new Vector2(-7f, -3.5f), new Vector2(6f, -3f), new Vector2(-4f, 4.5f), new Vector2(8f, 3.5f) };
                case "T": return new[] { new Vector2(-5f, 2.5f), new Vector2(5f, 2.5f) };
                default: return new[] { new Vector2(-6f, 2.5f), new Vector2(6.5f, -2.5f) };
            }
        }

        /// <summary>칸 벽·바위·기둥 한 덩이. 바닥 구석 그늘을 그리려고 사각형을 적어 둔다.</summary>
        GameObject Wall(Transform parent, string name, Vector2 position, Vector2 size, Color color, bool sortByY)
        {
            _wallRects.Add(new Rect(position - size * 0.5f, size));
            return Block(parent, name, position, size, color, sortByY);
        }

        /// <summary>
        /// 벽 한 덩이(Wall 레이어 충돌 + 그림 + 그림자). 그림은 기획/다크판타지-분위기-1차.md '땅·벽'을 따른다:
        /// Palette.Wall 덩이는 월드 좌표에 맞춘 돌 블록·모르타르(이름이 "Rock"이면 거친 바위), Palette.Pillar는 나무 버팀목,
        /// 그 밖의 색(자물쇠 문 등)은 예전처럼 단색 네모. 판자벽·금 간 벽도 Palette.Wall이라 이어진 벽과 무늬가 이음매 없이 같다(숨은 방 규칙).
        /// 충돌·크기·정렬·그림자는 예전과 같다.
        /// </summary>
        public static GameObject Block(Transform parent, string name, Vector2 position, Vector2 size, Color color, bool sortByY)
        {
            var go = new GameObject(name);
            go.layer = Layers.Wall;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            Sprite owned = null;
            if (color == Palette.Wall)
            {
                var rect = new Rect(position - size * 0.5f, size);
                owned = name == "Rock" ? ShapeSprites.RoughRock(rect) : ShapeSprites.StoneWall(rect);
                sr.sprite = owned;
                sr.color = Color.white;
                // 픽셀 반올림이 있어도 그림이 충돌 상자 크기에 꼭 맞게.
                Vector2 native = owned.bounds.size;
                visual.transform.localScale = new Vector3(size.x / Mathf.Max(0.01f, native.x), size.y / Mathf.Max(0.01f, native.y), 1f);
            }
            else if (color == Palette.Pillar)
            {
                int variant = Mathf.Abs(Mathf.RoundToInt(position.x * 3f) + Mathf.RoundToInt(position.y * 7f));
                sr.sprite = ShapeSprites.TimberPost(variant);
                sr.color = Color.white;
                visual.transform.localScale = new Vector3(size.x, size.y, 1f);
            }
            else
            {
                sr.sprite = ShapeSprites.Square;
                sr.color = color;
                visual.transform.localScale = new Vector3(size.x, size.y, 1f);
            }
            sr.sortingOrder = sortByY ? 1000 - Mathf.RoundToInt(position.y * 20f) : -900;
            // 그림·크기를 정한 뒤에 붙여야 그림자 모양이 그림 크기를 따른다.
            var shadow = visual.AddComponent<ShadowCaster2D>();
            shadow.selfShadows = false;
            if (owned) RuntimeAssetOwner.Own(go, owned);
            return go;
        }
    }
}
