using System;
using System.Collections.Generic;

namespace Demo6.Core.Dungeon
{
    /// <summary>칸 사이 길. 3차 초안 2-3 뼈대 표시: '-' '|' 열린 길, ':' 판자벽(처음부터 부숨), '#' 금 간 바위벽(곡괭이), '=' 자물쇠 문(열쇠).</summary>
    public enum EdgeKind
    {
        Open,
        Plank,
        Cracked,
        Locked,
    }

    /// <summary>칸 조각 종류(3차 초안 2-1). 조각마다 안쪽 벽 배치가 다르다.</summary>
    public enum PieceKind
    {
        /// <summary>전투 공터: 26×14에 기둥 2~4개(M0a 큰 전투방).</summary>
        Clearing,
        /// <summary>통로: 문과 문을 잇는 폭 6 길만 열려 있다.</summary>
        Corridor,
        /// <summary>막다른 방: 가운데 작은 방.</summary>
        Room,
        Entrance,
        StairsRoom,
        /// <summary>숨은 방: 판자벽 뒤 작은 방.</summary>
        Hidden,
        /// <summary>광업소 사무실: 자물쇠 문 뒤.</summary>
        Office,
        /// <summary>
        /// 보스방(전투·보스 문서 3-6, 묶음 7 '오우거 굴' X): 안쪽 26×14 돌방, 왼쪽 벽 가운데 문, 돌 기둥 4개(OgreDen.Pillars).
        /// 돌로 쌓은 고정 칸(MapAnchors.IsFixed)이라 원정마다 남는다.
        /// </summary>
        BossRoom,
        /// <summary>보스방 앞 쉼터(3-8 P): 돌방, 굴 앞 말뚝(켜면 영구), 벽 등잔 2, 바닥 긁은 글. 고정 칸.</summary>
        BossFront,
    }

    public enum FeatureKind
    {
        /// <summary>칸에 묶인 적 무리(멧돼지·궁수·굴쥐 수, 상태, 바라보는 방향).</summary>
        Group,
        /// <summary>굴쥐 둥지.</summary>
        Nest,
        WoodChest,
        IronChest,
        WallLamp,
        /// <summary>권양기 말뚝.</summary>
        Stake,
        Stairs,
        /// <summary>광맥(곡괭이, F 1.8초).</summary>
        Ore,
        /// <summary>사건 '광부 도시락통'.</summary>
        Lunchbox,
        /// <summary>광부 명패(이야기 지점).</summary>
        Nameplate,
        /// <summary>쪽지.</summary>
        Note,
        /// <summary>다래가 몰래 그린 분필 그림(Param = 가리키는 각도, 도).</summary>
        Chalk,
        /// <summary>쓰러진 광부 자리의 곡괭이.</summary>
        Pickaxe,
        /// <summary>광업소 금고(열쇠, F 2초).</summary>
        Safe,
        /// <summary>보스 자리(묶음 7): 보스방 런타임(BossArena)이 방·문·등잔·보스를 만든다. Param = 보스 id(OgreDen.BossId), FacingDeg = 처음 보는 쪽.</summary>
        Boss,
        /// <summary>바닥 긁은 글(Label = 글). 다가가면 한 번 읽힌다(보스방 앞 쉼터).</summary>
        Scrawl,
        /// <summary>
        /// 낙석 자리(1-2층 탐험 맛 1차 4-5, 생성 덧칠 FloorSpice). 바닥 잔돌이 단서, 서서 밟으면 1.0초 예고 뒤 떨어진다.
        /// 숨은 위험이라 층 검사 개수·발견 주머니·큰 지도에 들지 않는다.
        /// </summary>
        RockfallTrap,
        /// <summary>바닥 가시 덫(1-2층 탐험 맛 1차 4-6, FloorSpice). 빛 안에서만 보인다. 층 검사 개수·발견 주머니에 들지 않는다.</summary>
        FloorSpikes,
    }

    /// <summary>무리가 처음 놓일 때의 모습(3차 초안 2-6: 자는 중, 먹는 중, 순찰 중).</summary>
    public enum GroupState
    {
        Sleep,
        Eat,
        Patrol,
    }

    /// <summary>경험치·조사율 기록 종류(3차 초안 4-3 탐험 경험치).</summary>
    public enum DiscoveryKind
    {
        NewCell,
        WallLamp,
        WoodChest,
        IronChest,
        Stake,
        Shortcut,
        Story,
        HiddenRoom,
        Event,
        Ability,
        Ore,
        Safe,
        FloorComplete,
    }

    public readonly struct Offset
    {
        public readonly float X;
        public readonly float Y;

        public Offset(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>칸 안 자리 표시 하나. 위치는 칸 가운데 기준(유닛).</summary>
    public sealed class CellFeature
    {
        public FeatureKind Kind;
        public Offset Local;
        /// <summary>프로필 안에서 한 번만 받는 것의 id. 층 + 칸 + 이름으로 짓는다.</summary>
        public string Id;
        public string Label = "";
        public int Boars;
        public int Archers;
        public int Rats;
        public GroupState State = GroupState.Sleep;
        /// <summary>무리가 바라보는 방향(도, 0 = 오른쪽). 분필 그림이 가리키는 방향에도 쓴다.</summary>
        public float FacingDeg;
        /// <summary>쇠 궤짝 '희귀 이상 무기 보장'(1층 H) 같은 표시.</summary>
        public string Param = "";
        /// <summary>정예 무리(1-2층 탐험 맛 1차 4-1·4-2: '단단한 정예 돌충이'). 무리에만 쓴다.</summary>
        public bool Elite;
        /// <summary>
        /// 순찰 무리가 오갈 이웃 칸 id(1-2층 탐험 맛 1차 4-3, FloorSpice가 정함). 빈 글이면 제 칸 안 열린 문 두 곳 사이를 오간다.
        /// 상태가 순찰(GroupState.Patrol)일 때만 뜻이 있다.
        /// </summary>
        public string PatrolCell = "";
    }

    public sealed class CellDef
    {
        public char Glyph;
        public string Id;
        public string Name;
        public PieceKind Piece;
        public CellFeature[] Features = Array.Empty<CellFeature>();
        /// <summary>
        /// 공터 기둥 모양 번호(매판 새 탐험 1차 2-5 차례 8: 손으로 만든 5벌 c1·A·V·B·T = 0~4, PieceSlots.PillarSet).
        /// -1이면 기본 2개. 공터가 아닌 조각은 쓰지 않는다. 생성기가 씨앗으로 고른다.
        /// </summary>
        public int Pillars = -1;
    }

    public sealed class MapCell
    {
        public CellDef Def;
        public int X;
        public int Y;
        public readonly List<MapEdge> Edges = new List<MapEdge>();

        public string Id => Def.Id;
        public PieceKind Piece => Def.Piece;
        public IReadOnlyList<CellFeature> Features => Def.Features;

        public MapEdge EdgeTo(MapCell other)
        {
            foreach (var e in Edges)
                if (e.Other(this) == other) return e;
            return null;
        }

        public bool Has(FeatureKind kind)
        {
            foreach (var f in Def.Features)
                if (f.Kind == kind) return true;
            return false;
        }
    }

    /// <summary>칸 방향(그리드 기준, 위 = y 증가).</summary>
    public enum Side
    {
        Right,
        Up,
        Left,
        Down,
    }

    public sealed class MapEdge
    {
        public MapCell A;
        public MapCell B;
        public EdgeKind Kind;
        /// <summary>A에서 본 B 쪽 방향(Right 또는 Up).</summary>
        public Side SideFromA;

        public MapCell Other(MapCell c) => c == A ? B : A;

        public Side SideFrom(MapCell c) => c == A ? SideFromA : Opposite(SideFromA);

        public static Side Opposite(Side s) => (Side)(((int)s + 2) % 4);
    }

    /// <summary>
    /// 손으로 만든 글자 지도(3차 초안 2-1). 짝수 열·행은 칸 글자, 홀수 열은 가로 길, 홀수 행은 세로 길이다. '.'과 공백은 비어 있음.
    /// 맨 윗줄이 가장 높은 y다. 칸 글자는 범례(CellDef)에서 이름·조각·자리 표시를 찾는다.
    /// </summary>
    public sealed class FloorMap
    {
        public int Floor { get; }
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<MapCell> Cells => _cells;
        public IReadOnlyList<MapEdge> Edges => _edges;

        readonly List<MapCell> _cells = new List<MapCell>();
        readonly List<MapEdge> _edges = new List<MapEdge>();
        readonly MapCell[,] _grid;

        FloorMap(int floor, string name, int width, int height)
        {
            Floor = floor;
            Name = name;
            Width = width;
            Height = height;
            _grid = new MapCell[width, height];
        }

        public MapCell At(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height ? _grid[x, y] : null;

        public MapCell Find(string id)
        {
            foreach (var c in _cells)
                if (c.Id == id) return c;
            return null;
        }

        public static FloorMap Parse(int floor, string name, string glyphs, IEnumerable<CellDef> legend)
        {
            var defs = new Dictionary<char, CellDef>();
            foreach (var d in legend) defs[d.Glyph] = d;

            var lines = new List<string>();
            foreach (var raw in glyphs.Replace("\r", "").Split('\n'))
            {
                string line = raw.TrimEnd();
                if (line.Length > 0) lines.Add(line);
            }
            if (lines.Count == 0 || lines.Count % 2 == 0) throw new FormatException("글자 지도 줄 수는 홀수여야 한다(칸 줄과 세로 길 줄이 번갈아).");
            int height = (lines.Count + 1) / 2;
            int maxLen = 0;
            foreach (var l in lines) maxLen = Math.Max(maxLen, l.Length);
            int width = (maxLen + 1) / 2;
            var map = new FloorMap(floor, name, width, height);

            for (int row = 0; row < lines.Count; row += 2)
            {
                int y = height - 1 - row / 2;
                string line = lines[row];
                for (int col = 0; col < line.Length; col += 2)
                {
                    char g = line[col];
                    if (IsEmpty(g)) continue;
                    if (!defs.TryGetValue(g, out var def)) throw new FormatException($"범례에 없는 칸 글자 '{g}'");
                    var cell = new MapCell { Def = def, X = col / 2, Y = y };
                    map._grid[cell.X, y] = cell;
                    map._cells.Add(cell);
                }
            }

            for (int row = 0; row < lines.Count; row++)
            {
                string line = lines[row];
                if (row % 2 == 0)
                {
                    int y = height - 1 - row / 2;
                    for (int col = 1; col < line.Length; col += 2)
                    {
                        if (IsEmpty(line[col])) continue;
                        map.Connect(map.At((col - 1) / 2, y), map.At((col + 1) / 2, y), ParseEdge(line[col]), Side.Right, line[col]);
                    }
                }
                else
                {
                    int yTop = height - 1 - (row - 1) / 2;
                    for (int col = 0; col < line.Length; col += 2)
                    {
                        if (IsEmpty(line[col])) continue;
                        map.Connect(map.At(col / 2, yTop - 1), map.At(col / 2, yTop), ParseEdge(line[col]), Side.Up, line[col]);
                    }
                }
            }
            return map;
        }

        static bool IsEmpty(char c) => c == '.' || c == ' ';

        static EdgeKind ParseEdge(char c)
        {
            switch (c)
            {
                case '-':
                case '|':
                    return EdgeKind.Open;
                case ':': return EdgeKind.Plank;
                case '#': return EdgeKind.Cracked;
                case '=': return EdgeKind.Locked;
                default: throw new FormatException($"알 수 없는 길 글자 '{c}'");
            }
        }

        void Connect(MapCell a, MapCell b, EdgeKind kind, Side sideFromA, char glyph)
        {
            if (a == null || b == null) throw new FormatException($"길 '{glyph}' 양쪽에 칸이 없다");
            var e = new MapEdge { A = a, B = b, Kind = kind, SideFromA = sideFromA };
            a.Edges.Add(e);
            b.Edges.Add(e);
            _edges.Add(e);
        }

        /// <summary>허용한 길만 지나 갈 수 있는 칸들(시작 칸 포함).</summary>
        public HashSet<MapCell> Reachable(MapCell from, Func<EdgeKind, bool> allowed)
        {
            var seen = new HashSet<MapCell> { from };
            var queue = new Queue<MapCell>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var e in c.Edges)
                {
                    if (!allowed(e.Kind)) continue;
                    var n = e.Other(c);
                    if (seen.Add(n)) queue.Enqueue(n);
                }
            }
            return seen;
        }

        /// <summary>허용한 길만 지나는 가장 짧은 길(양 끝 칸 포함). 없으면 빈 목록.</summary>
        public List<MapCell> ShortestPath(MapCell from, MapCell to, Func<EdgeKind, bool> allowed)
        {
            var prev = new Dictionary<MapCell, MapCell> { [from] = null };
            var queue = new Queue<MapCell>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (c == to) break;
                foreach (var e in c.Edges)
                {
                    if (!allowed(e.Kind)) continue;
                    var n = e.Other(c);
                    if (prev.ContainsKey(n)) continue;
                    prev[n] = c;
                    queue.Enqueue(n);
                }
            }
            var path = new List<MapCell>();
            if (!prev.ContainsKey(to)) return path;
            for (var c = to; c != null; c = prev[c]) path.Add(c);
            path.Reverse();
            return path;
        }

        /// <summary>처음부터 갈 수 있는 길(열린 길 + 판자벽).</summary>
        public static bool StartPassable(EdgeKind k) => k == EdgeKind.Open || k == EdgeKind.Plank;

        public static bool OpenOnly(EdgeKind k) => k == EdgeKind.Open;
    }
}
