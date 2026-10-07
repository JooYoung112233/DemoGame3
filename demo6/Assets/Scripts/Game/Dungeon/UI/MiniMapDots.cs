using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 작은 지도 색 점(1-2층 탐험 맛 1차 4-10): 가 본 칸 아래쪽에 4×4 점을 칸마다 4개까지 한 줄로 찍는다.
    /// 차례는 계단(파랑, 큰 지도 계단 색) → 켠 말뚝(짙은 호박색, StakeColor) → 안 연 궤짝(나무·쇠·품삯·유품, 갈색) → 아직 고르지 않은 사건(도시락통, 붉은색).
    /// 함정과 적은 찍지 않는다(숨은 위험). 안 가 본 칸은 찍지 않는다(작은 지도가 지금처럼 '?'만 그림).
    /// 읽기만 한다: 칸의 계단 자리 표시, DungeonState.OneTime의 Cell·Kind·Done, 켠 말뚝 목록(큰 지도 아이콘과 같은 기준).
    /// DungeonMiniMap이 가 본 칸마다 한 줄로 부른다. 점 배치 다듬기는 Unity 개발 단계로 둔다(기능만).
    /// </summary>
    public static class MiniMapDots
    {
        /// <summary>칸마다 찍는 점 수 상한.</summary>
        public const int MaxDots = 4;
        /// <summary>점 한 변(화면 점).</summary>
        public const float DotSize = 4f;
        const float Gap = 2f;
        /// <summary>칸 아래 테두리에서 띄우는 거리.</summary>
        const float Bottom = 2f;

        /// <summary>계단: 큰 지도 계단 색.</summary>
        public static readonly Color StairsColor = new Color(0.6f, 0.8f, 1f, 1f);
        /// <summary>
        /// 켠 말뚝: 짙은 호박색(노란 쪽, 물결 3 고침). 궤짝 갈색(색조 약 33°)과 색조(약 48°)·밝기가 갈리고, 지금 칸 테두리(DungeonUi.Ember)에 묻히지 않는다.
        /// 큰 지도 켠 말뚝 색(BigMap.LitColor 1, 0.88, 0.55)을 그대로 쓰지 않은 까닭: 작은 지도의 내 자리 점(0.98, 0.87, 0.58)과 거의 같은 색이라
        /// 지금 칸에서 말뚝 점과 내 자리를 가리기 어렵다. 같은 노란 쪽에서 채도를 높여 셋(궤짝·테두리·내 자리)과 모두 갈리게 했다.
        /// </summary>
        public static readonly Color StakeColor = new Color(1f, 0.84f, 0.2f, 1f);
        /// <summary>안 연 궤짝(나무·쇠·품삯·유품): 큰 지도 나무 궤짝 색.</summary>
        public static readonly Color ChestColor = new Color(0.78f, 0.56f, 0.3f, 1f);
        /// <summary>아직 고르지 않은 사건(도시락통): 큰 지도 사건 색.</summary>
        public static readonly Color EventColor = new Color(0.92f, 0.5f, 0.42f, 1f);

        /// <summary>가 본 칸 하나의 색 점을 칸 사각형(at) 아래쪽 가운데에 한 줄로 그린다. 그리기 이벤트에서만 센다.</summary>
        public static void Draw(Rect at, DungeonCell cell, DungeonRoot root)
        {
            if (cell == null || !cell.Visited || !root || root.State == null) return;
            var ev = Event.current;
            if (ev == null || ev.type != EventType.Repaint) return;
            Count(cell, root.State, root.World, out bool stairs, out int stakes, out int chests, out int events);
            int total = Mathf.Min(MaxDots, (stairs ? 1 : 0) + stakes + chests + events);
            if (total <= 0) return;
            float x = at.center.x - (total * DotSize + (total - 1) * Gap) * 0.5f;
            float y = at.yMax - Bottom - DotSize;
            int i = 0;
            if (stairs) Dot(ref i, total, x, y, StairsColor);
            for (int k = 0; k < stakes; k++) Dot(ref i, total, x, y, StakeColor);
            for (int k = 0; k < chests; k++) Dot(ref i, total, x, y, ChestColor);
            for (int k = 0; k < events; k++) Dot(ref i, total, x, y, EventColor);
        }

        /// <summary>
        /// 칸 하나의 점 거리: 계단 자리 표시가 있나, 켠 말뚝(끝냄 또는 이번 장면에 켬) 수, 안 연 나무·쇠 궤짝 수(품삯·유품·흙 묻은 궤짝도 나무·쇠로 등록됨),
        /// 아직 고르지 않은 사건 수. 낙석·가시 덫·적은 한 번 받는 것으로 등록되지 않고, 등록돼도 여기서 세지 않는다.
        /// </summary>
        static void Count(DungeonCell cell, DungeonState state, DungeonWorld world, out bool stairs, out int stakes, out int chests, out int events)
        {
            stairs = cell.Map != null && cell.Map.Has(FeatureKind.Stairs);
            stakes = chests = events = 0;
            foreach (var e in state.OneTime.Values)
            {
                if (e == null) continue;
                switch (e.Kind)
                {
                    case DiscoveryKind.Stake:
                        if ((e.Done || state.ActiveStakes.Contains(e.Id)) && CellOf(e, world) == cell) stakes++;
                        break;
                    case DiscoveryKind.WoodChest:
                    case DiscoveryKind.IronChest:
                        if (!e.Done && CellOf(e, world) == cell) chests++;
                        break;
                    case DiscoveryKind.Event:
                        if (!e.Done && CellOf(e, world) == cell) events++;
                        break;
                }
            }
        }

        /// <summary>한 번 받는 것이 놓인 칸(등록할 때 칸이 없었으면 자리로 찾는다, 큰 지도 CellOf와 같은 기준).</summary>
        static DungeonCell CellOf(OneTimeEntry e, DungeonWorld world)
        {
            if (e.Cell != null) return e.Cell;
            return world != null ? world.CellAt(e.Position) : null;
        }

        static void Dot(ref int i, int total, float x, float y, Color color)
        {
            if (i >= total) return;
            DungeonUi.Fill(new Rect(x + i * (DotSize + Gap), y, DotSize, DotSize), color);
            i++;
        }
    }
}
