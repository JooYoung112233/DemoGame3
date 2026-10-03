using System.Collections.Generic;
using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 바닥 장식(기획/다크판타지-분위기-1차.md '땅·벽', 계약서 A '장식'): 칸마다 칸 시드로 정해져 실행마다 같은 자리에
    /// 뼈·해골 더미·녹슨 사슬·부서진 나무·돌무더기·오래된 핏자국을, 벽이 만나는 구석에는 거미줄을 도형 그림으로 놓는다.
    /// 충돌이 없고(걸음을 막지 않음), 문 앞 길·자리 표시(궤짝·말뚝·무리·둥지·계단·광맥 등)·벽 안을 피한다.
    /// 빛을 받는 기본 재질이라 어둠 속에서는 보이지 않고, 바닥(−1000) 바로 위·피 얼룩(−990 근처) 아래에 그린다.
    /// 상한 MaxSprites(스프라이트 수). 장면을 다시 불러올 때마다 새로 놓는다: 고정 칸(승강장·랜드마크)은 늘 같은 자리,
    /// 나머지 칸은 원정 씨앗이 섞인 칸 시드(DungeonWorld.CellSeed)라 원정마다 자리가 바뀐다(매판 새 탐험 1차).
    /// 지난 원정 흔적·승강장 막힌 아치 자리(ExpeditionTraces.CollectPlaces) 둘레 TraceClear 안은 비워 흔적 위에 겹치지 않게 한다.
    /// </summary>
    public static class DungeonDecor
    {
        /// <summary>던전 전체 장식 스프라이트 상한.</summary>
        public const int MaxSprites = 320;
        /// <summary>오래된 핏자국(가장 아래).</summary>
        public const int StainOrder = -998;
        /// <summary>뼈·사슬·나무·돌무더기.</summary>
        public const int PieceOrder = -996;
        /// <summary>구석 거미줄.</summary>
        public const int WebOrder = -994;
        /// <summary>장식끼리 최소 간격(뭉치지 않게).</summary>
        const float Spacing = 1.3f;
        /// <summary>문 가운데에서 이 반경 안, 그리고 문에서 칸 안쪽으로 2.5유닛 들어간 자리 둘레 2유닛은 비운다(문 앞 길).</summary>
        const float DoorClear = 3f;
        const float DoorLaneClear = 2f;
        /// <summary>흔적(흙더미·갓 판 흙·깨진 돌)·막힌 아치 자리에서 이 반경 안은 비운다.</summary>
        const float TraceClear = 2f;

        enum Kind
        {
            Bones,
            SkullPile,
            Chain,
            Wood,
            Rubble,
            Stain,
        }

        /// <summary>DungeonAtmosphere.Init이 부른다(벽·물체·자리 표시가 다 놓인 뒤). 만든 스프라이트 수를 돌려준다.</summary>
        public static int Build(DungeonWorld world, Transform parent)
        {
            if (world == null) return 0;
            Physics2D.SyncTransforms();
            var root = new GameObject("Dungeon Decor").transform;
            if (parent) root.SetParent(parent, false);
            int used = 0;
            var placed = new List<Vector2>(16);
            var traces = new List<Vector2>(16);
            ExpeditionTraces.CollectPlaces(DungeonRoot.Instance, traces);
            foreach (var cell in world.Cells)
            {
                if (used >= MaxSprites) break;
                var cellRoot = new GameObject("Decor " + cell.Id).transform;
                cellRoot.SetParent(root, false);
                var rng = new System.Random(world.CellSeed(cell) ^ 0x2F6B3A1);
                placed.Clear();
                used += Webs(cell, cellRoot, rng, MaxSprites - used, traces);
                used += Pieces(cell, cellRoot, rng, placed, MaxSprites - used, traces);
            }
            return used;
        }

        static int CountFor(PieceKind piece)
        {
            switch (piece)
            {
                case PieceKind.Clearing: return 9;
                case PieceKind.Corridor: return 5;
                case PieceKind.Entrance:
                case PieceKind.StairsRoom: return 6;
                default: return 5;
            }
        }

        /// <summary>조각마다 장식 고르기 비중(뼈, 해골 더미, 사슬, 나무, 돌무더기, 핏자국).</summary>
        static Kind Pick(PieceKind piece, System.Random rng)
        {
            float r = (float)rng.NextDouble();
            switch (piece)
            {
                case PieceKind.Clearing:
                    return Choose(r, 0.25f, 0.12f, 0.1f, 0.15f, 0.18f);
                case PieceKind.Corridor:
                    return Choose(r, 0.15f, 0f, 0.15f, 0.25f, 0.3f);
                case PieceKind.Entrance:
                case PieceKind.StairsRoom:
                    return Choose(r, 0.1f, 0f, 0.2f, 0.3f, 0.3f);
                case PieceKind.Hidden:
                    // 숨은 방: 쓰러진 광부 자리라 뼈·핏자국이 더 많다.
                    return Choose(r, 0.25f, 0.2f, 0.1f, 0.1f, 0.1f);
                default:
                    return Choose(r, 0.2f, 0.15f, 0.1f, 0.2f, 0.15f);
            }
        }

        /// <summary>누적 비중으로 고른다. 나머지는 핏자국.</summary>
        static Kind Choose(float r, float bones, float skull, float chain, float wood, float rubble)
        {
            if ((r -= bones) < 0f) return Kind.Bones;
            if ((r -= skull) < 0f) return Kind.SkullPile;
            if ((r -= chain) < 0f) return Kind.Chain;
            if ((r -= wood) < 0f) return Kind.Wood;
            if ((r -= rubble) < 0f) return Kind.Rubble;
            return Kind.Stain;
        }

        static int Pieces(DungeonCell cell, Transform parent, System.Random rng, List<Vector2> placed, int budget, List<Vector2> traces)
        {
            int want = CountFor(cell.Piece);
            var inner = cell.Inner;
            int used = 0;
            for (int attempt = 0; attempt < want * 10 && placed.Count < want && used < budget; attempt++)
            {
                var pos = new Vector2(
                    Mathf.Lerp(inner.xMin + 0.6f, inner.xMax - 0.6f, (float)rng.NextDouble()),
                    Mathf.Lerp(inner.yMin + 0.6f, inner.yMax - 0.6f, (float)rng.NextDouble()));
                var kind = Pick(cell.Piece, rng);
                float radius = Radius(kind);
                if (!Free(cell, pos, radius, placed, traces)) continue;
                placed.Add(pos);
                used += Place(kind, parent, pos, rng, budget - used);
            }
            return used;
        }

        static float Radius(Kind kind)
        {
            switch (kind)
            {
                case Kind.SkullPile: return 0.75f;
                case Kind.Wood: return 0.8f;
                case Kind.Stain: return 0.8f;
                case Kind.Rubble: return 0.65f;
                default: return 0.55f;
            }
        }

        /// <summary>벽 안·문 앞 길·자리 표시·흔적·다른 장식 가까이가 아니면 참.</summary>
        static bool Free(DungeonCell cell, Vector2 pos, float radius, List<Vector2> placed, List<Vector2> traces)
        {
            if (Physics2D.OverlapCircle(pos, radius + 0.15f, Layers.WallMask)) return false;
            foreach (var p in placed)
                if ((p - pos).sqrMagnitude < Spacing * Spacing) return false;
            if (NearTrace(pos, radius, traces)) return false;
            foreach (var e in cell.Edges)
            {
                if ((e.DoorCenter - pos).sqrMagnitude < DoorClear * DoorClear) return false;
                Vector2 inward = cell.Center - e.DoorCenter;
                Vector2 lane = e.DoorCenter + (inward.sqrMagnitude > 0.0001f ? inward.normalized : Vector2.zero) * 2.5f;
                if ((lane - pos).sqrMagnitude < DoorLaneClear * DoorLaneClear) return false;
            }
            foreach (var f in cell.Map.Features)
            {
                float keep = Keep(f.Kind) + radius;
                if ((cell.World(f.Local) - pos).sqrMagnitude < keep * keep) return false;
            }
            return true;
        }

        /// <summary>자리 표시마다 비워 둘 반경(무리는 싸움터, 말뚝은 다시 서는 자리).</summary>
        static float Keep(FeatureKind kind)
        {
            switch (kind)
            {
                case FeatureKind.Group: return 4.5f;
                case FeatureKind.Nest: return 3f;
                case FeatureKind.Stake: return 3.2f;
                case FeatureKind.Stairs: return 3.5f;
                case FeatureKind.WoodChest:
                case FeatureKind.IronChest: return 1.8f;
                case FeatureKind.Ore:
                case FeatureKind.Pickaxe:
                case FeatureKind.Safe: return 2.2f;
                case FeatureKind.WallLamp: return 1.5f;
                default: return 1.5f;
            }
        }

        static int Place(Kind kind, Transform parent, Vector2 pos, System.Random rng, int budget)
        {
            if (budget <= 0) return 0;
            float rot = (float)rng.NextDouble() * 360f;
            switch (kind)
            {
                case Kind.Bones:
                {
                    int n = Mathf.Min(budget, 1 + rng.Next(3));
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 at = pos + Jitter(rng, 0.35f);
                        Put(parent, "Bone", ShapeSprites.Bone, at, Tint(Palette.DungeonBone, rng, 0.12f), PieceOrder + (i & 1), (float)rng.NextDouble() * 360f,
                            0.55f + (float)rng.NextDouble() * 0.4f, rng);
                    }
                    return n;
                }
                case Kind.SkullPile:
                {
                    int n = Mathf.Min(budget, 3 + rng.Next(2));
                    Put(parent, "Skull", ShapeSprites.Skull, pos, Tint(Palette.DungeonBone, rng, 0.1f), PieceOrder + 1, rot - 90f + ((float)rng.NextDouble() - 0.5f) * 60f,
                        0.85f + (float)rng.NextDouble() * 0.3f, rng);
                    for (int i = 1; i < n; i++)
                    {
                        Vector2 at = pos + Jitter(rng, 0.55f);
                        Put(parent, "Bone", ShapeSprites.Bone, at, Tint(Palette.DungeonBone, rng, 0.12f), PieceOrder, (float)rng.NextDouble() * 360f,
                            0.5f + (float)rng.NextDouble() * 0.35f, rng);
                    }
                    return n;
                }
                case Kind.Chain:
                {
                    // 사슬 한두 토막이 꺾여 이어진다.
                    int n = Mathf.Min(budget, 1 + rng.Next(2));
                    Vector2 at = pos;
                    float ang = rot;
                    for (int i = 0; i < n; i++)
                    {
                        float scale = 0.8f + (float)rng.NextDouble() * 0.4f;
                        Put(parent, "Chain", ShapeSprites.Chain, at, Tint(Palette.DungeonRust, rng, 0.15f), PieceOrder, ang, scale, rng);
                        Vector2 dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
                        float turn = ((float)rng.NextDouble() - 0.5f) * 80f;
                        Vector2 end = at + dir * (0.5f * scale);
                        ang += turn;
                        dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
                        at = end + dir * (0.5f * scale);
                    }
                    return n;
                }
                case Kind.Wood:
                {
                    int n = Mathf.Min(budget, 1 + rng.Next(3));
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 at = pos + Jitter(rng, 0.45f);
                        float ang = rot + ((float)rng.NextDouble() - 0.5f) * 70f;
                        Put(parent, "Plank", ShapeSprites.BrokenPlank, at, Tint(Palette.DungeonRottenWood, rng, 0.15f), PieceOrder + (i & 1), ang,
                            0.55f + (float)rng.NextDouble() * 0.45f, rng);
                    }
                    return n;
                }
                case Kind.Rubble:
                    Put(parent, "Rubble", ShapeSprites.Rubble(rng.Next(4)), pos, Tint(Palette.DungeonRubble, rng, 0.1f), PieceOrder, rot,
                        0.6f + (float)rng.NextDouble() * 0.6f, rng);
                    return 1;
                default:
                    Put(parent, "OldStain", ShapeSprites.OldStain(rng.Next(4)), pos, Palette.DungeonOldBlood, StainOrder, rot,
                        0.7f + (float)rng.NextDouble() * 0.7f, rng);
                    return 1;
            }
        }

        /// <summary>흔적 자리 반경 TraceClear(+ 장식 반경) 안인가.</summary>
        static bool NearTrace(Vector2 pos, float radius, List<Vector2> traces)
        {
            if (traces == null) return false;
            float keep = TraceClear + radius;
            foreach (var t in traces)
                if ((t - pos).sqrMagnitude < keep * keep) return true;
            return false;
        }

        /// <summary>벽이 만나는 구석 1~2곳에 거미줄. 칸 안쪽 네 귀와 막다른 방 네 귀 가운데 실제 구석(두 벽이 막힌 곳)만 쓴다.</summary>
        static int Webs(DungeonCell cell, Transform parent, System.Random rng, int budget, List<Vector2> traces)
        {
            if (budget <= 0) return 0;
            int max = Mathf.Min(budget, 1 + rng.Next(2));
            int used = 0;
            var inner = cell.Inner;
            // 막다른 방(DungeonWorld.RoomMargins와 같은 반폭·반높이)의 귀도 후보로 넣는다.
            float hw = cell.Piece == PieceKind.Hidden ? DungeonWorld.HiddenHalfWidth : DungeonWorld.RoomHalfWidth;
            float hh = cell.Piece == PieceKind.Hidden ? DungeonWorld.HiddenHalfHeight : DungeonWorld.RoomHalfHeight;
            bool room = cell.Piece == PieceKind.Room || cell.Piece == PieceKind.Office || cell.Piece == PieceKind.Hidden;
            int start = rng.Next(8);
            for (int k = 0; k < 8 && used < max; k++)
            {
                int idx = (start + k) % 8;
                if (idx >= 4 && !room) continue;
                int sx = (idx & 1) == 0 ? -1 : 1;
                int sy = (idx & 2) == 0 ? -1 : 1;
                Vector2 corner = idx < 4
                    ? new Vector2(sx < 0 ? inner.xMin : inner.xMax, sy < 0 ? inner.yMin : inner.yMax)
                    : cell.Center + new Vector2(sx * hw, sy * hh);
                // 구석 바깥 두 쪽은 막혀 있고 안쪽은 비어 있어야 진짜 구석이다.
                Vector2 inward = new Vector2(-sx, -sy);
                if (Physics2D.OverlapCircle(corner + inward * 0.45f, 0.12f, Layers.WallMask)) continue;
                if (!Physics2D.OverlapPoint(corner + new Vector2(sx * 0.3f, -sy * 0.4f), Layers.WallMask)) continue;
                if (!Physics2D.OverlapPoint(corner + new Vector2(-sx * 0.4f, sy * 0.3f), Layers.WallMask)) continue;
                if (NearFeature(cell, corner, 1.2f)) continue;
                if (NearTrace(corner, 0.5f, traces)) continue;
                // 그림은 왼쪽 아래 구석에서 오른쪽 위로 퍼진다: 안쪽 방향에 맞춰 돌린다.
                float rot = inward.x > 0f ? (inward.y > 0f ? 0f : 270f) : (inward.y > 0f ? 90f : 180f);
                Put(parent, "Cobweb", ShapeSprites.Cobweb, corner, Palette.DungeonCobweb, WebOrder, rot,
                    0.75f + (float)rng.NextDouble() * 0.35f, null);
                used++;
            }
            return used;
        }

        static bool NearFeature(DungeonCell cell, Vector2 pos, float radius)
        {
            foreach (var f in cell.Map.Features)
                if ((cell.World(f.Local) - pos).sqrMagnitude < radius * radius) return true;
            return false;
        }

        static Vector2 Jitter(System.Random rng, float r) =>
            new Vector2(((float)rng.NextDouble() - 0.5f) * 2f * r, ((float)rng.NextDouble() - 0.5f) * 2f * r);

        /// <summary>색을 밝기만 조금 흔든다(알파는 그대로).</summary>
        static Color Tint(Color c, System.Random rng, float amount)
        {
            float k = 1f + ((float)rng.NextDouble() - 0.5f) * 2f * amount;
            return new Color(c.r * k, c.g * k, c.b * k, c.a);
        }

        /// <summary>빛을 받는 장식 스프라이트 하나(기본 재질). rng가 있으면 무작위로 뒤집는다(거미줄은 구석 방향이 틀어지므로 null).</summary>
        static SpriteRenderer Put(Transform parent, string name, Sprite sprite, Vector2 pos, Color color, int order, float rotationDeg, float scale, System.Random rng)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, rotationDeg);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (rng != null) sr.flipY = rng.NextDouble() < 0.5;
            return sr;
        }
    }
}
