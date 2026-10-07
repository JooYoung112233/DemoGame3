using System;
using System.Collections.Generic;

namespace Demo6.Core.Dungeon
{
    /// <summary>조각 안 자리 슬롯에 담을 수 있는 것(매판 새 탐험 1차 2-5 차례 9 '조각별 자리 슬롯').</summary>
    public enum SlotKind
    {
        /// <summary>무리 가운데(멧돼지·궁수·굴쥐가 둘레에 퍼짐, 반경 약 2.5 비어 있어야 함).</summary>
        Group,
        /// <summary>굴쥐 둥지.</summary>
        Nest,
        /// <summary>궤짝(나무·쇠). 벽 가까이.</summary>
        Chest,
        /// <summary>벽 등잔(벽 면 바로 안쪽).</summary>
        Lamp,
        /// <summary>권양기 말뚝.</summary>
        Stake,
        /// <summary>계단(계단 조각).</summary>
        Stairs,
        /// <summary>광맥(구석).</summary>
        Ore,
        /// <summary>사건(도시락통 등).</summary>
        Event,
        /// <summary>작은 물건: 명패·쪽지·곡괭이.</summary>
        Small,
        /// <summary>분필 그림(바닥).</summary>
        Chalk,
    }

    /// <summary>조각 안 자리 하나(칸 가운데 기준 유닛 좌표). 기둥·문 앞 길과 겹치지 않게 손으로 적는다.</summary>
    public readonly struct PieceSlot
    {
        public readonly Offset Local;
        public readonly SlotKind Kind;
        /// <summary>
        /// 이 자리가 기대는 문 쪽((int)Side, -1 = 없음). 통로 팔 위 자리는 그 쪽 문이 있을 때만 길 안이다(문이 없으면 바위 속).
        /// 분필 자리는 그 쪽 문 가운데에서 3~6유닛 떨어진 자리다(판자벽 문을 가리킬 때만 쓴다).
        /// </summary>
        public readonly int DoorSide;

        public PieceSlot(float x, float y, SlotKind kind)
        {
            Local = new Offset(x, y);
            Kind = kind;
            DoorSide = -1;
        }

        public PieceSlot(float x, float y, SlotKind kind, Side doorSide)
        {
            Local = new Offset(x, y);
            Kind = kind;
            DoorSide = (int)doorSide;
        }

        /// <summary>문 쪽에 기대는 자리인가.</summary>
        public bool NeedsDoor => DoorSide >= 0;
    }

    /// <summary>
    /// 조각별 자리 슬롯과 공터 기둥 5벌(매판 새 탐험 1차 2-5 차례 8·9, 6장 위험 1 '슬롯은 손 지도 좌표').
    /// 좌표를 아무렇게나 고르면 바위 속에 묻히므로 생성기는 여기 적힌 자리에서만 고른다.
    /// 기둥 5벌은 지금 손 지도 공터 c1·A·V·B·T의 기둥이다(DungeonWorld.PillarsFor가 칸 id 표 대신 이 번호를 쓴다).
    /// 칸은 28×16(안쪽 x ±13.5, y ±7.5), 문은 네 변 가운데 폭 4(가운데 (±14, 0), (0, ±8)). 손 지도 자리 표시 좌표를 출발점으로 적었다.
    /// 공터 자리는 다섯 벌 모두에 같은 후보를 적고, 그 벌의 기둥(반경 1.5, 무리·둥지 가운데는 2.5) 안에 드는 자리는 뺀다.
    /// 오우거 굴의 보스방·보스방 앞 쉼터(전투·보스 문서 3-6·3-8, 묶음 7)는 생성기가 쓰지 않는 손 지도 칸이라 자리 슬롯이 없고,
    /// 보스방 기둥은 굴 표(OgreDen.Pillars, 1.6 × 1.6 돌 기둥 4개)를 따른다.
    /// </summary>
    public static class PieceSlots
    {
        /// <summary>손으로 만든 공터 기둥 벌 수(0 = c1, 1 = A, 2 = V, 3 = B, 4 = T).</summary>
        public const int PillarSetCount = 5;

        /// <summary>기둥에서 떨어질 거리: 작은 것 1.5, 무리·둥지 가운데 2.5(졸개가 둘레에 퍼짐).</summary>
        public const float PillarClearance = 1.5f;
        public const float GroupPillarClearance = 2.5f;
        /// <summary>문 앞 길: 문 가운데 반경 3 안에는 (분필 말고) 아무것도 두지 않는다.</summary>
        public const float DoorClearance = 3f;
        /// <summary>분필 그림과 판자벽 문 가운데 사이(2-5 차례 11).</summary>
        public const float ChalkMinDistance = 3f;
        public const float ChalkMaxDistance = 6f;

        static readonly Offset[][] PillarSets =
        {
            new[] { new Offset(-6f, 2.5f), new Offset(6.5f, -2.5f), new Offset(1f, 4.5f) },
            new[] { new Offset(-5f, -3f), new Offset(-2f, 4f), new Offset(9f, -3.5f) },
            new[] { new Offset(-6f, -3f), new Offset(5f, 4.5f) },
            new[] { new Offset(-7f, -3.5f), new Offset(6f, -3f), new Offset(-4f, 4.5f), new Offset(8f, 3.5f) },
            new[] { new Offset(-5f, 2.5f), new Offset(5f, 2.5f) },
        };

        /// <summary>모양 번호가 없을 때(-1) 쓰는 기본 기둥 2개.</summary>
        static readonly Offset[] DefaultPillars = { new Offset(-6f, 2.5f), new Offset(6.5f, -2.5f) };

        /// <summary>입구(승강장)·계단 앞의 구석 버팀목 기둥(DungeonWorld.BuildPiece와 같은 자리).</summary>
        static readonly Offset[] CornerPillars = { new Offset(-10f, -5f), new Offset(10f, 5f) };

        /// <summary>공터 기둥(칸 가운데 기준). 번호가 범위 밖이면 기본 2개.</summary>
        public static IReadOnlyList<Offset> PillarSet(int set) =>
            set >= 0 && set < PillarSets.Length ? PillarSets[set] : DefaultPillars;

        /// <summary>조각 안 기둥(공터는 모양 번호, 입구·계단 앞은 구석 두 개, 보스방은 굴 표의 돌 기둥 4개, 그 밖은 없음).</summary>
        public static IReadOnlyList<Offset> PillarsIn(PieceKind piece, int pillarSet)
        {
            switch (piece)
            {
                case PieceKind.Clearing: return PillarSet(pillarSet);
                case PieceKind.Entrance:
                case PieceKind.StairsRoom: return CornerPillars;
                case PieceKind.BossRoom: return OgreDen.Pillars;
                default: return Array.Empty<Offset>();
            }
        }

        /// <summary>문 가운데(칸 가운데 기준): 오른쪽 (14, 0), 위 (0, 8), 왼쪽 (-14, 0), 아래 (0, -8).</summary>
        public static Offset DoorCenter(Side side)
        {
            switch (side)
            {
                case Side.Right: return new Offset(14f, 0f);
                case Side.Up: return new Offset(0f, 8f);
                case Side.Left: return new Offset(-14f, 0f);
                default: return new Offset(0f, -8f);
            }
        }

        /// <summary>
        /// 이 칸의 문 짜임에서 쓸 수 있는 자리인가. 통로 팔 위 자리(DoorSide)는 그 쪽 문이 있어야 하고, 분필 자리는 가리킬 문이 있어야 한다.
        /// </summary>
        public static bool Usable(PieceSlot slot, Func<Side, bool> hasDoor)
        {
            if (!slot.NeedsDoor) return true;
            return hasDoor != null && hasDoor((Side)slot.DoorSide);
        }

        /// <summary>
        /// 조각·기둥 벌에 맞는 자리 슬롯. 문 앞 길(문 가운데 반경 3)과 기둥(반경 1.5)을 피한 자리만 적는다.
        /// 공터가 아닌 조각은 기둥 벌을 무시한다. 통로 팔 위 자리와 분필 자리는 DoorSide가 붙어 있어 Usable로 걸러 쓴다.
        /// </summary>
        public static IReadOnlyList<PieceSlot> SlotsFor(PieceKind piece, int pillarSet)
        {
            switch (piece)
            {
                case PieceKind.Clearing:
                    int index = pillarSet >= 0 && pillarSet < PillarSetCount ? pillarSet + 1 : 0;
                    return ClearingBySet[index];
                case PieceKind.Corridor: return Corridor;
                case PieceKind.Room:
                case PieceKind.Office: return Room;
                case PieceKind.Hidden: return Hidden;
                case PieceKind.Entrance: return Entrance;
                case PieceKind.StairsRoom: return StairsRoom;
                // 굴 칸은 손 지도 고정 자리(OgreDen 표)만 쓰고 생성기는 이 조각을 쓰지 않는다.
                case PieceKind.BossRoom:
                case PieceKind.BossFront: return Array.Empty<PieceSlot>();
                default: return Array.Empty<PieceSlot>();
            }
        }

        // ── 공터(26×14, 기둥 2~4개). 손 지도 c1 무리 (3, 0.5), A 둥지 (4, 1)·나무 궤짝 (-10, 5), V 무리 (1, 2)·나무 궤짝 (10, 5)·광맥 (9, -5),
        //    B 무리 (-1, 1.5)·등잔 (6, 7), T 무리 (0, -1)·명패 (-9, -4)·나무 궤짝 (9, -4)를 모두 담았다.
        static readonly PieceSlot[] ClearingBase = Concat(
            Many(SlotKind.Group,
                3f, 0.5f, 1f, 2f, -1f, 1.5f, 0f, -1f, 4f, 1f, -3f, -1f, -4f, 1f, 2f, -2f,
                5f, -1f, -2f, 0.5f, 1f, -3f, -1f, -2.5f, 4f, -0.5f, -4.5f, -1f, -3f, 2.5f, 3f, 2.5f),
            Many(SlotKind.Nest,
                4f, 1f, 3f, 0.5f, 1f, 2f, -1f, 1.5f, 0f, -1f, -3f, -1f, -4f, 1f, 2f, -2f, -2f, 0.5f, 1f, -3f, -4.5f, -1f),
            Many(SlotKind.Chest,
                -10f, 5f, 10f, 5f, 9f, -4f, -10f, -5f, 10f, -5f, -9f, 4f, -6f, 6f, 6f, 6f, -6f, -6f, 6f, -6f,
                -12f, 4f, 12f, -4f, -12f, -4f, 12f, 4f),
            Many(SlotKind.Lamp,
                6f, 7f, -9f, 7f, -5f, 7f, 9f, 7f, -9f, -7f, -5f, -7f, 5f, -7f, 9f, -7f,
                -13f, 4.5f, 13f, 4.5f, -13f, -4.5f, 13f, -4.5f),
            Many(SlotKind.Ore,
                9f, -5f, -9f, -5f, -9f, 5f, 9f, 5f, 11.5f, -5.5f, -11.5f, 5.5f, 11.5f, 5.5f, -11.5f, -5.5f),
            Many(SlotKind.Event,
                -8f, 1f, 8f, -1f, -3f, -5f, 3f, 5f, -9f, -2f, 9f, 2f, -7f, 3.5f, 7f, -3.5f),
            Many(SlotKind.Small,
                -9f, -4f, 9f, 4f, -9f, 4f, 9f, -4f, -4f, -5.5f, 4f, 5.5f, -4f, 5.5f, 4f, -5.5f),
            ChalkAround(Side.Right, false),
            ChalkAround(Side.Up, false),
            ChalkAround(Side.Left, false),
            ChalkAround(Side.Down, false));

        /// <summary>[0] = 기본 기둥(-1), [1..5] = 기둥 벌 0~4. 그 벌의 기둥과 겹치는 자리를 뺀 목록.</summary>
        static readonly PieceSlot[][] ClearingBySet = BuildClearingSets();

        // ── 통로: 가운데 6×6과 문 쪽으로 뻗은 폭 6 팔만 길이다. 가운데 자리는 늘, 팔 위 자리는 그 쪽 문이 있을 때만.
        //    손 지도 c2 도시락통 (-7, 0)(왼쪽 팔), D 등잔 (-6, 3)(왼쪽 팔 위 벽 면).
        static readonly PieceSlot[] Corridor = Concat(
            Many(SlotKind.Lamp, -2.6f, 2.6f, 2.6f, 2.6f, -2.6f, -2.6f, 2.6f, -2.6f),
            Many(SlotKind.Small, -1.5f, 1.5f, 1.5f, -1.5f),
            Many(SlotKind.Event, 1f, -1f, -1f, 1f),
            Many(SlotKind.Chest, -1.8f, -1.8f, 1.8f, 1.8f),
            Many(SlotKind.Nest, 0f, 0f),
            Arm(Side.Left,
                SlotKind.Lamp, -7f, 2.6f, SlotKind.Lamp, -10f, -2.6f, SlotKind.Event, -7f, 0f,
                SlotKind.Small, -9f, -1.5f, SlotKind.Chest, -10f, 1.8f),
            Arm(Side.Right,
                SlotKind.Lamp, 7f, -2.6f, SlotKind.Lamp, 10f, 2.6f, SlotKind.Event, 7f, 0f,
                SlotKind.Small, 9f, 1.5f, SlotKind.Chest, 10f, -1.8f),
            Arm(Side.Up,
                SlotKind.Lamp, 2.6f, 5.5f, SlotKind.Lamp, -2.6f, 5f, SlotKind.Event, 0f, 5f,
                SlotKind.Small, -1.5f, 5f, SlotKind.Chest, 1.8f, 4.5f),
            Arm(Side.Down,
                SlotKind.Lamp, -2.6f, -5.5f, SlotKind.Lamp, 2.6f, -5f, SlotKind.Event, 0f, -5f,
                SlotKind.Small, 1.5f, -5f, SlotKind.Chest, -1.8f, -4.5f),
            ChalkAround(Side.Right, true),
            ChalkAround(Side.Up, true),
            ChalkAround(Side.Left, true),
            ChalkAround(Side.Down, true));

        // ── 막다른 방·광업소 사무실(가운데 14×9, 문 쪽은 폭 6 길). 손 지도 K 쪽지 (-4, -3). 문 쪽 길 입구((±7, 0), (0, ±4.5))를 비운다.
        static readonly PieceSlot[] Room = Concat(
            Many(SlotKind.Chest, -5f, -3f, 5f, -3f, -5f, 3f, 5f, 3f, -3f, -3.3f, 3f, 3.3f),
            Many(SlotKind.Small, -4f, -3f, 4f, 3f, -4f, 3f, 4f, -3f),
            Many(SlotKind.Lamp, -5f, 4f, 5f, 4f, -5f, -4f, 5f, -4f),
            Many(SlotKind.Ore, -5.5f, -3.2f, 5.5f, 3.2f, 5.5f, -3.2f, -5.5f, 3.2f),
            Many(SlotKind.Event, -2.5f, -2.5f, 2.5f, 2.5f, -2.5f, 2.5f, 2.5f, -2.5f));

        // ── 숨은 방(가운데 12×8). 손 지도 H 곡괭이 (-3, -2), 쇠 궤짝 (3, -2).
        static readonly PieceSlot[] Hidden = Concat(
            Many(SlotKind.Chest, 3f, -2f, -3f, 2f, 3f, 2f, -3f, -2f),
            Many(SlotKind.Small, -3f, -2f, 3f, 2f, -3f, 2f, 3f, -2f));

        // ── 승강장(트인 돌방, 구석 기둥 (-10, -5)·(10, 5)). 손 지도 E 말뚝 (-8, -2), 등잔 (-5, 7). 승강장은 늘 같은 돌방이라 생성기는 첫 자리를 쓴다.
        static readonly PieceSlot[] Entrance = Concat(
            Many(SlotKind.Stake, -8f, -2f, 8f, 2f, -8f, 2f, 8f, -2f),
            Many(SlotKind.Lamp, -5f, 7f, 5f, 7f, -5f, -7f, 5f, -7f));

        // ── 계단 앞(트인 방, 구석 기둥 같음). 손 지도 S 말뚝 (-8, 2), 등잔 (-3, 7), 계단 (8, 0).
        static readonly PieceSlot[] StairsRoom = Concat(
            Many(SlotKind.Stairs, 8f, 0f, -8f, 0f, 0f, 4f, 0f, -4f),
            Many(SlotKind.Stake, -8f, 2f, 8f, -2f, -8f, -2f, 8f, 2f),
            Many(SlotKind.Lamp, -3f, 7f, 3f, 7f, -5f, -7f, 5f, -7f));

        static PieceSlot[][] BuildClearingSets()
        {
            var sets = new PieceSlot[PillarSetCount + 1][];
            for (int i = 0; i < sets.Length; i++)
            {
                var pillars = PillarSet(i - 1);
                var list = new List<PieceSlot>();
                foreach (var s in ClearingBase)
                {
                    float clear = s.Kind == SlotKind.Group || s.Kind == SlotKind.Nest ? GroupPillarClearance : PillarClearance;
                    bool ok = true;
                    foreach (var p in pillars)
                    {
                        float dx = s.Local.X - p.X, dy = s.Local.Y - p.Y;
                        if (dx * dx + dy * dy < clear * clear - 0.0001f) ok = false;
                    }
                    if (ok) list.Add(s);
                }
                sets[i] = list.ToArray();
            }
            return sets;
        }

        /// <summary>판자벽 문을 가리키는 분필 자리(문 가운데에서 3~6유닛). 통로면 그 쪽 팔 안(폭 6)에만 적는다.</summary>
        static PieceSlot[] ChalkAround(Side side, bool corridor)
        {
            // 오른쪽 문 기준으로 적고 돌린다. (x, y) = 문 가운데에서 안쪽으로 들어온 깊이 · 옆으로 비킨 거리.
            float[] pts = corridor
                ? new[] { 3.5f, 0f, 4f, 1.5f, 4.5f, -2f, 3f, -1.5f, 5f, 2.2f, 5.5f, -0.5f }
                : new[] { 3.5f, 0f, 4f, 1.5f, 4.5f, -2f, 3f, -1.5f, 5f, 2.5f, 5.5f, -0.5f, 4f, -3.5f, 3.5f, 3f };
            var result = new PieceSlot[pts.Length / 2];
            var door = DoorCenter(side);
            for (int i = 0; i < result.Length; i++)
            {
                float depth = pts[i * 2], lateral = pts[i * 2 + 1];
                float x, y;
                switch (side)
                {
                    case Side.Right: x = door.X - depth; y = lateral; break;
                    case Side.Left: x = door.X + depth; y = -lateral; break;
                    case Side.Up: x = -lateral; y = door.Y - depth; break;
                    default: x = lateral; y = door.Y + depth; break;
                }
                // 위·아래 문은 칸 높이가 낮아 깊이를 그대로 써도 안쪽(y ±7.5)이다. 통로 팔은 폭 6(|옆| < 3)이라 옆을 2.2까지만 적었다.
                result[i] = new PieceSlot(x, y, SlotKind.Chalk, side);
            }
            return result;
        }

        static PieceSlot[] Many(SlotKind kind, params float[] xy)
        {
            var result = new PieceSlot[xy.Length / 2];
            for (int i = 0; i < result.Length; i++) result[i] = new PieceSlot(xy[i * 2], xy[i * 2 + 1], kind);
            return result;
        }

        /// <summary>통로 팔 위 자리(종류, x, y를 되풀이). 그 쪽 문이 있을 때만 쓴다.</summary>
        static PieceSlot[] Arm(Side side, params object[] items)
        {
            var result = new PieceSlot[items.Length / 3];
            for (int i = 0; i < result.Length; i++)
                result[i] = new PieceSlot((float)items[i * 3 + 1], (float)items[i * 3 + 2], (SlotKind)items[i * 3], side);
            return result;
        }

        static PieceSlot[] Concat(params PieceSlot[][] parts)
        {
            var list = new List<PieceSlot>();
            foreach (var p in parts) list.AddRange(p);
            return list.ToArray();
        }
    }
}
