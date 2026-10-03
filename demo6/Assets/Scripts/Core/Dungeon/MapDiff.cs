using System;
using System.Collections.Generic;

namespace Demo6.Core.Dungeon
{
    /// <summary>지난 원정과 비교한 흔적 종류(매판 새 탐험 1차 2-2·2-5 차례 13).</summary>
    public enum TraceKind
    {
        /// <summary>있던 열린 길(또는 금 간 벽)이 없어짐: 막힌 문틈 앞 흙더미 + 부러진 버팀목(F 살피기).</summary>
        Rubble,
        /// <summary>없던 열린 길이 생김: 문틈 바닥에 갓 긁어 낸 짙은 흙(첫 진입 글).</summary>
        FreshDig,
        /// <summary>금 간 벽 자리가 열린 길이 됨: 바닥에 깨진 돌 조각.</summary>
        BrokenStone,
    }

    /// <summary>
    /// 흔적 하나: 칸 (X, Y)의 Side 변 문틈. 세계 쪽은 칸 가운데 (28X, 16Y)에서 그 변 가운데로 옮겨 놓는다.
    /// 흙더미는 막힌 벽의 양쪽 가운데 지금 지도에 있는 칸마다 하나(그 칸에서 본 변), 갓 판 흙·깨진 돌은 문 하나에 하나(왼쪽·아래 칸에서 본 Right·Up).
    /// </summary>
    public readonly struct TraceSpot
    {
        public readonly int X;
        public readonly int Y;
        public readonly Side Side;
        public readonly TraceKind Kind;

        public TraceSpot(int x, int y, Side side, TraceKind kind)
        {
            X = x;
            Y = y;
            Side = side;
            Kind = kind;
        }

        public override string ToString() => $"{Kind} ({X},{Y}) {Side}";
    }

    /// <summary>
    /// 지난 원정의 같은 층 글자 지도와 이번 글자 지도를 칸 자리로 비교한다(범례 없이 글자만 읽음, 순수 함수, 예외를 던지지 않음).
    /// 읽는 규칙은 FloorMap.Parse와 같다: 짝수 열·행 = 칸, 홀수 = 길, '.'·' ' 빈칸, '-' '|' 열린 길, ':' 판자벽, '#' 금 간 벽, '=' 자물쇠 문.
    /// y는 맨 아랫줄이 0이라 두 지도의 높이가 달라도 같은 자리끼리 비교된다.
    /// 문 자리 (x, y, Right|Up)마다:
    /// 지금 판자벽이면 건너뜀(흔적이 숨은 방을 들키게 하면 안 됨, 2-2), 옛 판자벽·옛/지금 자물쇠도 건너뜀(랜드마크 자물쇠는 그대로 남음).
    /// 옛 열린 길·금 간 벽 → 지금 길 없음 = 흙더미(지금 지도에 있는 양쪽 칸마다 하나, 그 칸에서 본 변),
    /// 옛 없음 → 지금 열린 길 = 갓 판 흙, 옛 금 간 벽 → 지금 열린 길 = 깨진 돌(둘 다 왼쪽·아래 칸에서 본 Right·Up). 그 밖은 흔적 없음.
    /// </summary>
    public static class MapDiff
    {
        /// <summary>문 자리 하나의 상태.</summary>
        enum Door
        {
            None,
            Open,
            Plank,
            Cracked,
            Locked,
            /// <summary>모르는 글자: 그 문 자리는 비교하지 않는다.</summary>
            Unknown,
        }

        /// <summary>범례 없이 읽은 글자 지도(칸 자리와 문 자리만).</summary>
        sealed class Layout
        {
            public int Width;
            public int Height;
            public readonly HashSet<(int x, int y)> Cells = new HashSet<(int, int)>();
            public readonly Dictionary<(int x, int y, Side side), Door> Doors = new Dictionary<(int, int, Side), Door>();

            public bool HasCell(int x, int y) => Cells.Contains((x, y));

            public Door DoorAt(int x, int y, Side side) => Doors.TryGetValue((x, y, side), out var d) ? d : Door.None;
        }

        /// <summary>
        /// 흔적 목록(아래 줄부터, 왼쪽부터, Right 다음 Up 차례). 지난 지도나 이번 지도가 없으면(null·빈 글) 늘 빈 목록.
        /// </summary>
        public static List<TraceSpot> Compare(string previousGlyphs, string currentGlyphs)
        {
            var spots = new List<TraceSpot>();
            if (string.IsNullOrEmpty(previousGlyphs) || string.IsNullOrEmpty(currentGlyphs)) return spots;
            var prev = Read(previousGlyphs);
            var cur = Read(currentGlyphs);
            if (prev.Height == 0 || cur.Height == 0) return spots;
            int width = Math.Max(prev.Width, cur.Width);
            int height = Math.Max(prev.Height, cur.Height);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                CompareDoor(prev, cur, x, y, Side.Right, spots);
                CompareDoor(prev, cur, x, y, Side.Up, spots);
            }
            return spots;
        }

        /// <summary>두 글자 지도가 칸 자리·길·벽 모양까지 같은가(칸 글자는 보지 않음, 끝 빈 줄·빈칸은 무시).</summary>
        public static bool SameShape(string a, string b)
        {
            if (a == null || b == null) return false;
            var ta = a.Replace("\r", "").TrimEnd('\n', '.', ' ');
            var tb = b.Replace("\r", "").TrimEnd('\n', '.', ' ');
            if (ta.Length != tb.Length) return false;
            for (int i = 0; i < ta.Length; i++)
            {
                char x = ta[i], y = tb[i];
                if (char.IsLetterOrDigit(x) && char.IsLetterOrDigit(y)) continue;
                if (x != y) return false;
            }
            return true;
        }

        /// <summary>계단 칸 자리(글자 'S' — 손 지도·생성 지도 공통, 맨 아랫줄 y 0). 없으면 null.</summary>
        public static (int x, int y)? StairsCell(string glyphs)
        {
            if (string.IsNullOrEmpty(glyphs)) return null;
            var lines = new List<string>();
            foreach (var raw in glyphs.Replace("\r", "").Split('\n'))
            {
                string line = raw.TrimEnd();
                if (line.Length > 0) lines.Add(line);
            }
            int height = (lines.Count + 1) / 2;
            for (int row = 0; row < lines.Count; row += 2)
            {
                int col = lines[row].IndexOf('S');
                if (col >= 0 && col % 2 == 0) return (col / 2, height - 1 - row / 2);
            }
            return null;
        }

        /// <summary>
        /// 지난 지도와 견준 '달라진 정도'(클수록 덜 닮음): 모양이 같으면 -1, 아니면 흔적 수 + 계단 칸이 다르면 StairsMovedScore.
        /// 다시 연 층이 지난 원정과 너무 닮지 않게 생성기가 고를 때 쓴다(FloorGenerator.GenerateUnlike).
        /// </summary>
        public static int Difference(string previousGlyphs, string currentGlyphs)
        {
            if (SameShape(previousGlyphs, currentGlyphs)) return -1;
            int score = Compare(previousGlyphs, currentGlyphs).Count;
            var a = StairsCell(previousGlyphs);
            var b = StairsCell(currentGlyphs);
            if (a == null || b == null || a.Value != b.Value) score += StairsMovedScore;
            return score;
        }

        /// <summary>Difference에서 계단 칸이 옮겨 간 몫(흔적 수보다 늘 크게).</summary>
        public const int StairsMovedScore = 1000;

        static void CompareDoor(Layout prev, Layout cur, int x, int y, Side side, List<TraceSpot> spots)
        {
            var was = prev.DoorAt(x, y, side);
            var now = cur.DoorAt(x, y, side);
            if (was == now) return;
            if (was == Door.Unknown || now == Door.Unknown) return;
            // 숨은 방 판자벽 자리와 랜드마크 자물쇠 자리에는 흔적을 두지 않는다.
            if (now == Door.Plank || was == Door.Plank) return;
            if (now == Door.Locked || was == Door.Locked) return;

            if (now == Door.None && (was == Door.Open || was == Door.Cracked))
            {
                int nx = side == Side.Right ? x + 1 : x;
                int ny = side == Side.Up ? y + 1 : y;
                if (cur.HasCell(x, y)) spots.Add(new TraceSpot(x, y, side, TraceKind.Rubble));
                if (cur.HasCell(nx, ny)) spots.Add(new TraceSpot(nx, ny, MapEdge.Opposite(side), TraceKind.Rubble));
            }
            else if (now == Door.Open && was == Door.None)
            {
                spots.Add(new TraceSpot(x, y, side, TraceKind.FreshDig));
            }
            else if (now == Door.Open && was == Door.Cracked)
            {
                spots.Add(new TraceSpot(x, y, side, TraceKind.BrokenStone));
            }
        }

        /// <summary>FloorMap.Parse와 같은 배치 규칙으로 칸·문 자리만 읽는다(끝 공백을 자르고 빈 줄은 건너뜀).</summary>
        static Layout Read(string glyphs)
        {
            var layout = new Layout();
            var lines = new List<string>();
            foreach (var raw in glyphs.Replace("\r", "").Split('\n'))
            {
                string line = raw.TrimEnd();
                if (line.Length > 0) lines.Add(line);
            }
            if (lines.Count == 0) return layout;
            int height = (lines.Count + 1) / 2;
            int maxLen = 0;
            foreach (var l in lines) maxLen = Math.Max(maxLen, l.Length);
            layout.Height = height;
            layout.Width = (maxLen + 1) / 2;

            for (int row = 0; row < lines.Count; row++)
            {
                string line = lines[row];
                if (row % 2 == 0)
                {
                    int y = height - 1 - row / 2;
                    for (int col = 0; col < line.Length; col++)
                    {
                        char g = line[col];
                        if (IsEmpty(g)) continue;
                        if (col % 2 == 0) layout.Cells.Add((col / 2, y));
                        else layout.Doors[((col - 1) / 2, y, Side.Right)] = DoorOf(g);
                    }
                }
                else
                {
                    int yTop = height - 1 - (row - 1) / 2;
                    for (int col = 0; col < line.Length; col += 2)
                    {
                        char g = line[col];
                        if (IsEmpty(g)) continue;
                        layout.Doors[(col / 2, yTop - 1, Side.Up)] = DoorOf(g);
                    }
                }
            }
            return layout;
        }

        static bool IsEmpty(char c) => c == '.' || c == ' ';

        static Door DoorOf(char c)
        {
            switch (c)
            {
                case '-':
                case '|':
                    return Door.Open;
                case ':': return Door.Plank;
                case '#': return Door.Cracked;
                case '=': return Door.Locked;
                default: return Door.Unknown;
            }
        }
    }
}
