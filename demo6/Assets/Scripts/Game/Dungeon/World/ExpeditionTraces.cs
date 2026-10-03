using System.Collections.Generic;
using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 지난 원정과 달라진 곳의 흔적(매판 새 탐험 1차 2-2·2-5 차례 13, 도형 임시판 — 새 그림 파일을 만들지 않는다).
    /// 흙더미 + 부러진 버팀목(F 살피기 "지난번엔 이리로 지나갔다. 지금은 흙과 부러진 버팀목뿐이다."),
    /// 갓 긁어 낸 흙(처음 밟으면 "갓 긁어 낸 흙. 발톱 자국이 아직 축축하다."), 깨진 돌 조각("밤새 무너져 길이 됐다."),
    /// 그리고 승강장의 안 쓰는 문 자리마다 흙으로 막힌 아치(2-2 '남는 곳은 눈으로 구분된다').
    /// 입력은 DungeonRoot.Traces(Core MapDiff.Compare 결과). 충돌체를 두지 않는다(벽이 이미 막음).
    /// 모두 빛을 받는 기본 재질이라 등잔 빛 안에서만 보인다. 숨은 방 판자벽 자리는 MapDiff가 이미 빼고, 숨은 방 칸 안의 흙더미도 여기서 뺀다
    /// (F 안내가 벽 너머로 떠 숨은 방을 들키게 하지 않게).
    /// </summary>
    public static class ExpeditionTraces
    {
        public const string HolderName = "Expedition Traces";
        public const string ArchHolderName = "Landing Arches";
        public const string RubbleLine = "지난번엔 이리로 지나갔다. 지금은 흙과 부러진 버팀목뿐이다.";
        public const string FreshDigLine = "갓 긁어 낸 흙. 발톱 자국이 아직 축축하다.";
        public const string BrokenStoneLine = "밤새 무너져 길이 됐다.";
        /// <summary>흙더미 가운데: 칸 안쪽 벽 면에서 칸 안으로 이만큼(경계 벽이면 문 자리에서 약 1.2).</summary>
        const float RubbleFromFace = 0.7f;
        const float InspectRange = 2f;
        /// <summary>갓 판 흙 얼룩: 문틈 가운데에서 양쪽으로 이만큼.</summary>
        const float FreshSide = 1.6f;
        /// <summary>막힌 아치 앞 바닥에 흘러내린 흙(문 자리에서 칸 안으로).</summary>
        const float ArchSpill = 0.9f;

        static readonly Color DirtShade = new Color(0.16f, 0.12f, 0.09f, 0.9f);
        static readonly Color DirtLoose = new Color(0.35f, 0.27f, 0.2f);
        static readonly Color DirtTop = new Color(0.44f, 0.35f, 0.26f);
        static readonly Color DirtFresh = new Color(0.09f, 0.065f, 0.05f, 0.88f);
        static readonly Color ClawScrape = new Color(0.42f, 0.33f, 0.24f, 0.72f);
        static readonly Color Pebble = new Color(0.36f, 0.34f, 0.31f);
        static readonly Color StoneDust = new Color(0.52f, 0.5f, 0.47f, 0.28f);
        static readonly Color StoneLight = new Color(0.58f, 0.56f, 0.52f);
        static readonly Color StoneMid = new Color(0.44f, 0.42f, 0.39f);
        static readonly Color StoneDark = new Color(0.29f, 0.27f, 0.25f);
        /// <summary>승강장 막힌 아치 돌 테(차갑게 칠한 돌, DungeonWorld.ColdStone 벽과 어울림).</summary>
        static readonly Color RimStone = new Color(0.5f, 0.53f, 0.58f);
        static readonly Color RimDark = new Color(0.34f, 0.36f, 0.4f);
        static readonly Color RimLight = new Color(0.62f, 0.65f, 0.7f);

        /// <summary>
        /// 흔적과 승강장 막힌 아치를 놓는다. 자리 표시를 다 놓은 뒤, 바닥 장식(DungeonDecor) 전에 DungeonRoot.Awake가 부른다.
        /// 만든 흔적·아치 수를 돌려준다. 시험에서 다시 불러도 아치는 장면마다 한 벌만 놓는다.
        /// </summary>
        public static int Build(DungeonRoot root, IReadOnlyList<TraceSpot> spots)
        {
            if (!root || root.World == null) return 0;
            var holder = new GameObject(HolderName).transform;
            holder.SetParent(root.transform, false);
            int made = 0;
            ExpeditionTraceWatch watch = null;
            if (spots != null)
            {
                foreach (var spot in spots)
                {
                    var cell = CellOf(root.World, spot);
                    if (!Wanted(cell, spot)) continue;
                    Vector2 at = PlaceOf(cell, spot);
                    var rng = new System.Random(Hash(spot));
                    switch (spot.Kind)
                    {
                        case TraceKind.Rubble:
                            BuildRubble(holder, spot, at, rng);
                            break;
                        case TraceKind.FreshDig:
                            BuildFreshDig(holder, spot, at, rng);
                            if (!watch) watch = holder.gameObject.AddComponent<ExpeditionTraceWatch>();
                            watch.Add(at);
                            break;
                        case TraceKind.BrokenStone:
                            BuildBrokenStone(holder, spot, at, rng);
                            break;
                    }
                    made++;
                }
            }
            if (!root.transform.Find(ArchHolderName)) made += BuildLandingArches(root, spots);
            return made;
        }

        // ── 자리 계산(DungeonDecor가 같은 함수로 흔적 둘레를 비운다) ──────────

        /// <summary>칸 (x, y)의 side 변 가운데(문 자리, 벽 두께 가운데). 칸 가운데 (28x, 16y)에서 가로 변 ±14, 세로 변 ±8.</summary>
        public static Vector2 DoorPoint(int x, int y, Side side)
        {
            var center = new Vector2(x * DungeonWorld.CellWidth, y * DungeonWorld.CellHeight);
            bool horizontal = side == Side.Right || side == Side.Left;
            return center + SideVector(side) * ((horizontal ? DungeonWorld.CellWidth : DungeonWorld.CellHeight) * 0.5f);
        }

        /// <summary>
        /// 흔적 하나의 그림 가운데: 흙더미는 그 칸 안쪽 벽 면 앞(경계 벽이면 문 자리에서 칸 안으로 약 1.2, 통로·막다른 방은 바위 면 앞),
        /// 갓 판 흙·깨진 돌은 문틈 가운데.
        /// </summary>
        public static Vector2 PlaceOf(DungeonCell cell, TraceSpot spot)
        {
            if (spot.Kind != TraceKind.Rubble || cell == null) return DoorPoint(spot.X, spot.Y, spot.Side);
            return cell.Center + SideVector(spot.Side) * (FaceDistance(cell, spot.Side) - RubbleFromFace);
        }

        /// <summary>흔적·막힌 아치가 차지한 자리들(바닥 장식이 반경 2 안을 비운다). root.Traces로 다시 계산한다.</summary>
        public static void CollectPlaces(DungeonRoot root, List<Vector2> into)
        {
            if (!root || root.World == null || into == null) return;
            var spots = root.Traces;
            if (spots != null)
            {
                foreach (var spot in spots)
                {
                    var cell = CellOf(root.World, spot);
                    if (!Wanted(cell, spot)) continue;
                    Vector2 at = PlaceOf(cell, spot);
                    into.Add(at);
                    if (spot.Kind == TraceKind.Rubble) continue;
                    // 갓 판 흙·깨진 돌은 문틈 양쪽 바닥까지 퍼진다.
                    Vector2 n = SideVector(spot.Side);
                    into.Add(at + n * FreshSide);
                    into.Add(at - n * FreshSide);
                }
            }
            var landing = root.Landing;
            if (landing == null) return;
            foreach (var side in ArchSides(root))
                into.Add(DoorPoint(landing.Map.X, landing.Map.Y, side) - SideVector(side) * ArchSpill);
        }

        /// <summary>
        /// 승강장의 안 쓰는 문 자리: 문이 없고 격자 안에 이웃 자리가 있는 변(2-5 예 'E 위·아래 문틈은 흙으로 막힘').
        /// 격자 밖(지도 가장자리)은 문이 날 수 없는 자리라 아치를 두지 않는다.
        /// </summary>
        public static List<Side> ArchSides(DungeonRoot root)
        {
            var sides = new List<Side>(4);
            var landing = root ? root.Landing : null;
            var map = root ? root.Map : null;
            if (landing == null || map == null) return sides;
            for (int i = 0; i < 4; i++)
            {
                var side = (Side)i;
                if (landing.EdgeOn(side) != null) continue;
                Vector2 step = SideVector(side);
                int x = landing.Map.X + Mathf.RoundToInt(step.x);
                int y = landing.Map.Y + Mathf.RoundToInt(step.y);
                if (x < 0 || y < 0 || x >= map.Width || y >= map.Height) continue;
                sides.Add(side);
            }
            return sides;
        }

        static DungeonCell CellOf(DungeonWorld world, TraceSpot spot) =>
            world.CellAt(new Vector2(spot.X * DungeonWorld.CellWidth, spot.Y * DungeonWorld.CellHeight));

        /// <summary>놓을 흔적인가: 칸이 있어야 하고, 흙더미는 지금 문이 없는 변에만, 숨은 방 칸 안에는 두지 않는다.</summary>
        static bool Wanted(DungeonCell cell, TraceSpot spot)
        {
            if (cell == null) return false;
            if (spot.Kind != TraceKind.Rubble) return true;
            return cell.Piece != PieceKind.Hidden && cell.EdgeOn(spot.Side) == null;
        }

        /// <summary>칸 가운데에서 그 변 쪽 걸을 수 있는 면까지(DungeonWorld 조각 안쪽 벽과 같은 값).</summary>
        static float FaceDistance(DungeonCell cell, Side side)
        {
            bool horizontal = side == Side.Right || side == Side.Left;
            switch (cell.Piece)
            {
                case PieceKind.Corridor:
                    return DungeonWorld.CorridorWidth * 0.5f;
                case PieceKind.Room:
                case PieceKind.Office:
                    return horizontal ? DungeonWorld.RoomHalfWidth : DungeonWorld.RoomHalfHeight;
                case PieceKind.Hidden:
                    return horizontal ? DungeonWorld.HiddenHalfWidth : DungeonWorld.HiddenHalfHeight;
                default:
                    return (horizontal ? DungeonWorld.CellWidth : DungeonWorld.CellHeight) * 0.5f - DungeonWorld.WallThickness * 0.5f;
            }
        }

        static Vector2 SideVector(Side side)
        {
            switch (side)
            {
                case Side.Right: return Vector2.right;
                case Side.Up: return Vector2.up;
                case Side.Left: return Vector2.left;
                default: return Vector2.down;
            }
        }

        /// <summary>흔적마다 실행마다 같은 모양(자리·종류로 정한 시드).</summary>
        static int Hash(TraceSpot spot)
        {
            unchecked
            {
                int h = spot.X * 73856093 ^ spot.Y * 19349663 ^ ((int)spot.Side + 1) * 83492791 ^ ((int)spot.Kind + 1) * 2654435;
                return h & 0x7FFFFFFF;
            }
        }

        // ── 그림(도형 임시판) ─────────────────────────────────

        static Transform NewTrace(Transform holder, string name, Vector2 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(holder, false);
            go.transform.position = pos;
            return go.transform;
        }

        /// <summary>
        /// 흙더미: 벽에 기대 쏟아진 흙 덩이(그늘 → 몸통 → 덩이 → 밝은 윗면) + 꺾인 버팀목 두 토막(서로 비스듬히) + 잔돌.
        /// F "흙더미 살피기"(거리 2).
        /// </summary>
        static void BuildRubble(Transform holder, TraceSpot spot, Vector2 at, System.Random rng)
        {
            var t = NewTrace(holder, "Rubble " + spot, at);
            Vector2 inward = -SideVector(spot.Side);
            Vector2 along = new Vector2(-inward.y, inward.x);
            float ang = Angle(along);
            int o = WorldProps.FloorDecalOrder;
            Vector2 L(float u, float v) => along * u + inward * v;

            WorldProps.Shape(t, "DirtShade", L(0f, -0.12f), new Vector2(4f, 1.95f), ShapeSprites.Circle, DirtShade, o + 4, false, ang);
            WorldProps.Shape(t, "Dirt", L(0f, -0.1f), new Vector2(3.3f, 1.5f), ShapeSprites.Circle, Vary(DirtLoose, rng, 0.06f), o + 5, false, ang);
            float[,] lumps =
            {
                { -1.2f, -0.05f, 1.35f, 1.05f }, { 1.2f, -0.15f, 1.25f, 1f }, { 0.15f, 0.3f, 1.5f, 0.95f },
                { -0.55f, 0.62f, 0.85f, 0.55f }, { 0.95f, 0.55f, 0.65f, 0.45f },
            };
            for (int i = 0; i < lumps.GetLength(0); i++)
            {
                Vector2 jitter = new Vector2(Rand(rng, -0.12f, 0.12f), Rand(rng, -0.08f, 0.08f));
                WorldProps.Shape(t, "Lump", L(lumps[i, 0] + jitter.x, lumps[i, 1] + jitter.y), new Vector2(lumps[i, 2], lumps[i, 3]),
                    ShapeSprites.Circle, Vary(DirtLoose, rng, 0.1f), o + 6, false, ang + Rand(rng, -15f, 15f));
            }
            for (int i = 0; i < 4; i++)
                WorldProps.Shape(t, "DirtTop", L(Rand(rng, -1.3f, 1.3f), Rand(rng, -0.35f, 0.25f)), new Vector2(Rand(rng, 0.3f, 0.6f), Rand(rng, 0.2f, 0.35f)),
                    ShapeSprites.Circle, Vary(DirtTop, rng, 0.08f), o + 7, false, ang + Rand(rng, -30f, 30f));

            // 꺾인 버팀목 두 토막: 하나는 흙에 반쯤 묻혀 칸 안쪽으로 비스듬히 튀어나오고, 다른 하나는 반대로 기운다.
            var timber = Palette.DungeonTimber;
            var timberDark = Palette.DungeonTimberDark;
            Vector2 a0 = L(-1.75f + Rand(rng, -0.1f, 0.1f), -0.45f);
            Vector2 a1 = L(-0.15f + Rand(rng, -0.1f, 0.1f), 0.42f + Rand(rng, -0.08f, 0.08f));
            Vector2 b0 = L(0.3f + Rand(rng, -0.1f, 0.1f), 0.66f + Rand(rng, -0.08f, 0.08f));
            Vector2 b1 = L(1.8f + Rand(rng, -0.1f, 0.1f), -0.32f);
            WorldProps.Stroke(t, "Timber", a0, a1, 0.27f, timber, o + 8, false);
            WorldProps.Stroke(t, "Timber", b0, b1, 0.24f, Vary(timber, rng, 0.08f), o + 8, false);
            WorldProps.Stroke(t, "Grain", Vector2.Lerp(a0, a1, 0.08f), Vector2.Lerp(a0, a1, 0.9f), 0.06f, timberDark, o + 9, false);
            WorldProps.Stroke(t, "Grain", Vector2.Lerp(b0, b1, 0.1f), Vector2.Lerp(b0, b1, 0.92f), 0.05f, timberDark, o + 9, false);
            // 부러진 끝의 가시.
            WorldProps.Stroke(t, "Splinter", a1, a1 + L(0.28f, 0.16f), 0.08f, timber, o + 9, false);
            WorldProps.Stroke(t, "Splinter", a1, a1 + L(0.22f, -0.12f), 0.07f, timberDark, o + 9, false);
            WorldProps.Stroke(t, "Splinter", b0, b0 + L(-0.26f, 0.14f), 0.08f, timber, o + 9, false);
            WorldProps.Stroke(t, "Splinter", b0, b0 + L(-0.2f, -0.13f), 0.07f, timberDark, o + 9, false);

            int pebbles = 5 + rng.Next(3);
            for (int i = 0; i < pebbles; i++)
            {
                float s = Rand(rng, 0.14f, 0.3f);
                bool square = rng.NextDouble() < 0.4;
                WorldProps.Shape(t, "Pebble", L(Rand(rng, -1.9f, 1.9f), Rand(rng, 0.4f, 1.2f)), new Vector2(s, s * Rand(rng, 0.7f, 1f)),
                    square ? ShapeSprites.Square : ShapeSprites.Circle, Vary(Pebble, rng, 0.12f), o + 6, false, Rand(rng, 0f, 360f));
            }

            var use = t.gameObject.AddComponent<WorldInteraction>();
            use.Setup("흙더미 살피기", 0f, InspectRange, null, null, () => DungeonEvents.Say(RubbleLine));
        }

        /// <summary>갓 판 흙: 문틈 가운데와 양쪽 바닥에 젖은 짙은 흙 얼룩 + 발톱 긁힌 줄 + 흙덩이. 첫 진입 글·소리는 ExpeditionTraceWatch.</summary>
        static void BuildFreshDig(Transform holder, TraceSpot spot, Vector2 at, System.Random rng)
        {
            var t = NewTrace(holder, "FreshDig " + spot, at);
            Vector2 normal = SideVector(spot.Side);
            Vector2 along = new Vector2(-normal.y, normal.x);
            float ang = Angle(along);
            int o = WorldProps.FloorDecalOrder;
            Vector2 L(float u, float v) => along * u + normal * v;

            WorldProps.Shape(t, "Fresh", Vector2.zero, new Vector2(3.4f, 2f), ShapeSprites.Circle, DirtFresh, o + 1, false, ang);
            for (int k = -1; k <= 1; k += 2)
            {
                WorldProps.Shape(t, "Fresh", L(Rand(rng, -0.25f, 0.25f), k * FreshSide), new Vector2(3.8f, 2.4f), ShapeSprites.Circle, DirtFresh, o + 1, false,
                    ang + Rand(rng, -8f, 8f));
                for (int i = 0; i < 2; i++)
                    WorldProps.Shape(t, "Fresh", L((i == 0 ? -1f : 1f) * Rand(rng, 1.1f, 1.5f), k * Rand(rng, 1.7f, 2.4f)), new Vector2(Rand(rng, 1.2f, 1.7f), Rand(rng, 0.8f, 1.2f)),
                        ShapeSprites.Circle, DirtFresh, o + 1, false, ang + Rand(rng, -25f, 25f));

                // 발톱 자국: 굴 파는 쪽(문을 지나는 방향)으로 긁힌 나란한 줄 셋, 한두 무리.
                int groups = 1 + rng.Next(2);
                for (int g = 0; g < groups; g++)
                {
                    Vector2 c = L(Rand(rng, -1.2f, 1.2f), k * Rand(rng, 0.9f, 2f));
                    float dirDeg = Angle(normal) + Rand(rng, -28f, 28f);
                    Vector2 dir = WorldProps.Rotate(Vector2.right, dirDeg);
                    Vector2 side = new Vector2(-dir.y, dir.x);
                    float len = Rand(rng, 0.8f, 1.15f);
                    for (int s = -1; s <= 1; s++)
                    {
                        Vector2 mid = c + side * (s * 0.17f) + dir * Rand(rng, -0.08f, 0.08f);
                        WorldProps.Stroke(t, "Claw", mid - dir * (len * 0.5f), mid + dir * (len * 0.5f), 0.05f, ClawScrape, o + 2, false);
                    }
                }
            }
            int clods = 4 + rng.Next(3);
            for (int i = 0; i < clods; i++)
            {
                float s = Rand(rng, 0.12f, 0.26f);
                WorldProps.Shape(t, "Clod", L(Rand(rng, -1.8f, 1.8f), Rand(rng, -2.2f, 2.2f)), new Vector2(s, s * Rand(rng, 0.7f, 1f)), ShapeSprites.Circle,
                    Vary(DirtLoose, rng, 0.1f), o + 2, false, Rand(rng, 0f, 360f));
            }
        }

        /// <summary>깨진 돌: 문틈 바닥에 옅은 돌가루 + 회색 돌 조각 여럿 + 문설주 자리의 큰 덩이 둘. F "깨진 돌 살피기"(거리 2).</summary>
        static void BuildBrokenStone(Transform holder, TraceSpot spot, Vector2 at, System.Random rng)
        {
            var t = NewTrace(holder, "BrokenStone " + spot, at);
            Vector2 normal = SideVector(spot.Side);
            Vector2 along = new Vector2(-normal.y, normal.x);
            float ang = Angle(along);
            int o = WorldProps.FloorDecalOrder;
            Vector2 L(float u, float v) => along * u + normal * v;

            WorldProps.Shape(t, "StoneDust", Vector2.zero, new Vector2(4.4f, 2.9f), ShapeSprites.Circle, StoneDust, o + 1, false, ang);
            for (int k = -1; k <= 1; k += 2)
            {
                Vector2 c = L(k * Rand(rng, 1.75f, 2f), Rand(rng, -0.55f, 0.55f));
                float s = Rand(rng, 0.55f, 0.75f);
                WorldProps.Shape(t, "ChunkShade", c + L(0.06f, -0.08f), new Vector2(s * 1.15f, s), ShapeSprites.Circle, DirtShade, o + 2, false);
                WorldProps.Shape(t, "Chunk", c, new Vector2(s, s * Rand(rng, 0.75f, 0.95f)), ShapeSprites.Square, Vary(StoneMid, rng, 0.08f), o + 3, false, Rand(rng, 0f, 90f));
                WorldProps.Shape(t, "ChunkTop", c + L(-0.06f, 0.06f), new Vector2(s * 0.45f, s * 0.35f), ShapeSprites.Triangle, StoneLight, o + 4, false, Rand(rng, 0f, 360f));
            }
            int shards = 10 + rng.Next(4);
            for (int i = 0; i < shards; i++)
            {
                float s = Rand(rng, 0.16f, 0.48f);
                double pick = rng.NextDouble();
                var color = pick < 0.35 ? StoneLight : pick < 0.75 ? StoneMid : StoneDark;
                WorldProps.Shape(t, "Shard", L(Rand(rng, -2f, 2f), Rand(rng, -1.5f, 1.5f)), new Vector2(s, s * Rand(rng, 0.55f, 1f)),
                    rng.NextDouble() < 0.6 ? ShapeSprites.Triangle : ShapeSprites.Square, Vary(color, rng, 0.08f), o + 2 + rng.Next(2), false, Rand(rng, 0f, 360f));
            }

            var use = t.gameObject.AddComponent<WorldInteraction>();
            use.Setup("깨진 돌 살피기", 0f, InspectRange, null, null, () => DungeonEvents.Say(BrokenStoneLine));
        }

        /// <summary>
        /// 승강장 안 쓰는 문 자리마다 흙으로 막힌 아치(벽 면 위 WallDetailOrder): 차가운 돌 테(문설주 둘 + 칸 쪽 아치돌) + 흙 채움,
        /// 앞 바닥에 흘러내린 흙. 지난 원정 흙더미가 같은 변에 있으면 흘러내린 흙은 뺀다(겹치지 않게).
        /// </summary>
        static int BuildLandingArches(DungeonRoot root, IReadOnlyList<TraceSpot> spots)
        {
            var landing = root.Landing;
            if (landing == null) return 0;
            var sides = ArchSides(root);
            if (sides.Count == 0) return 0;
            var holder = new GameObject(ArchHolderName).transform;
            holder.SetParent(root.transform, false);
            foreach (var side in sides)
            {
                bool rubbleHere = false;
                if (spots != null)
                    foreach (var s in spots)
                        if (s.Kind == TraceKind.Rubble && s.X == landing.Map.X && s.Y == landing.Map.Y && s.Side == side) rubbleHere = true;
                var rng = new System.Random(Hash(new TraceSpot(landing.Map.X, landing.Map.Y, side, TraceKind.Rubble)) ^ 0x51ED27);
                BuildArch(holder, DoorPoint(landing.Map.X, landing.Map.Y, side), side, !rubbleHere, rng);
            }
            return sides.Count;
        }

        static void BuildArch(Transform holder, Vector2 door, Side side, bool spill, System.Random rng)
        {
            var t = NewTrace(holder, "Arch " + side, door);
            Vector2 inward = -SideVector(side);
            Vector2 along = new Vector2(-inward.y, inward.x);
            float ang = Angle(along);
            int w = WorldProps.WallDetailOrder;
            Vector2 L(float u, float v) => along * u + inward * v;
            float half = DungeonWorld.DoorWidth * 0.5f;

            // 흙 채움(벽 두께 안).
            WorldProps.Shape(t, "ArchFill", Vector2.zero, new Vector2(half * 2f - 0.3f, DungeonWorld.WallThickness * 0.92f), ShapeSprites.Square, DirtLoose, w, false, ang);
            for (int i = 0; i < 6; i++)
                WorldProps.Shape(t, "ArchDirt", L(Rand(rng, -1.45f, 1.45f), Rand(rng, -0.22f, 0.22f)), new Vector2(Rand(rng, 0.35f, 0.55f), Rand(rng, 0.25f, 0.4f)),
                    ShapeSprites.Circle, rng.NextDouble() < 0.5 ? DirtShade : Vary(DirtTop, rng, 0.1f), w + 1, false, ang + Rand(rng, -20f, 20f));
            // 돌 테: 문설주 둘, 칸 쪽 면에 아치돌(가운데 이맛돌).
            for (int k = -1; k <= 1; k += 2)
            {
                WorldProps.Shape(t, "Jamb", L(k * (half + 0.12f), 0f), new Vector2(0.62f, 1.3f), ShapeSprites.Square, RimStone, w + 2, false, ang);
                WorldProps.Stroke(t, "JambEdge", L(k * (half - 0.18f), -0.6f), L(k * (half - 0.18f), 0.6f), 0.08f, RimDark, w + 3, false);
            }
            const int Stones = 6;
            for (int i = 0; i < Stones; i++)
            {
                float u = Mathf.Lerp(-half + 0.3f, half - 0.3f, i / (float)(Stones - 1));
                WorldProps.Shape(t, "ArchStone", L(u, 0.42f), new Vector2(0.56f, 0.32f), ShapeSprites.Square, (i & 1) == 0 ? RimStone : RimDark, w + 3, false,
                    ang + u * 4f);
            }
            WorldProps.Shape(t, "Keystone", L(0f, 0.46f), new Vector2(0.6f, 0.46f), ShapeSprites.Square, RimLight, w + 4, false, ang);

            if (!spill) return;
            int o = WorldProps.FloorDecalOrder;
            WorldProps.Shape(t, "Spill", L(0f, ArchSpill), new Vector2(3f, 1.1f), ShapeSprites.Circle, Vary(DirtLoose, rng, 0.06f), o + 4, false, ang);
            WorldProps.Shape(t, "Spill", L(-1f, ArchSpill + 0.3f), new Vector2(1.1f, 0.7f), ShapeSprites.Circle, Vary(DirtLoose, rng, 0.1f), o + 4, false, ang + Rand(rng, -20f, 20f));
            WorldProps.Shape(t, "Spill", L(0.95f, ArchSpill + 0.35f), new Vector2(0.9f, 0.6f), ShapeSprites.Circle, Vary(DirtLoose, rng, 0.1f), o + 4, false, ang + Rand(rng, -20f, 20f));
            for (int i = 0; i < 4; i++)
            {
                float s = Rand(rng, 0.12f, 0.24f);
                WorldProps.Shape(t, "Pebble", L(Rand(rng, -1.6f, 1.6f), Rand(rng, ArchSpill + 0.3f, ArchSpill + 0.9f)), new Vector2(s, s), ShapeSprites.Circle,
                    Vary(Pebble, rng, 0.12f), o + 5, false);
            }
        }

        static float Angle(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        static float Rand(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        /// <summary>색 밝기만 조금 흔든다(알파는 그대로).</summary>
        static Color Vary(Color c, System.Random rng, float amount)
        {
            float k = 1f + Rand(rng, -amount, amount);
            return new Color(c.r * k, c.g * k, c.b * k, c.a);
        }
    }

    /// <summary>
    /// 갓 판 흙 자리 지킴이(장면마다 하나, 흔적 묶음에 붙음): 플레이어가 자리 2유닛 안에 처음 들어오면 작은 흙 흘러내리는 소리와
    /// "갓 긁어 낸 흙. 발톱 자국이 아직 축축하다."를 낸다. 자리마다 한 번이고, 글은 20초 안에 겹쳐 띄우지 않는다(소리만 낸다).
    /// 정적 상태가 없어 장면을 다시 불러오면 저절로 새로 시작한다.
    /// </summary>
    public sealed class ExpeditionTraceWatch : MonoBehaviour
    {
        const float Range = 2f;
        const float SayGap = 20f;

        readonly List<Vector2> _spots = new List<Vector2>();
        readonly List<bool> _done = new List<bool>();
        float _lastSay = -999f;

        public void Add(Vector2 pos)
        {
            _spots.Add(pos);
            _done.Add(false);
        }

        void Update()
        {
            var player = PlayerController.Instance;
            if (!player || player.IsDown) return;
            Vector2 p = player.Position;
            for (int i = 0; i < _spots.Count; i++)
            {
                if (_done[i] || (_spots[i] - p).sqrMagnitude > Range * Range) continue;
                _done[i] = true;
                Sfx.PlayScaled(SfxKind.Break, 0.3f, 0.55f);
                if (Time.time - _lastSay < SayGap) continue;
                _lastSay = Time.time;
                DungeonEvents.Say(ExpeditionTraces.FreshDigLine);
            }
        }
    }
}
