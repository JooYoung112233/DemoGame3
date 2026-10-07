using System.Collections.Generic;
using Demo6.Core.Town;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 마을 광장 그림·충돌(기획/마을-의뢰-첫판.md 2장, 도형 임시판 — 원화는 10장 목록). 좌표·크기는 모두 Core TownLayout에서 읽는다.
    /// 땅: 칠하는 땅(어두운 바깥 흙) 위에 걷는 땅(흙 마당, 건물 가까이 구석 그늘)과 돌판 길. 던전 바닥·벽과 같은 절차 텍스처(ShapeSprites)를 쓴다.
    /// 충돌: 바깥 테두리·건물·수레·울타리(TownLayout.Solids)를 Wall 레이어 상자로 둔다. 그림자는 끈다(ShadowCaster2D 없음). 주민·모루·게시판·화로는 충돌이 없다.
    /// 건물은 위에서 본 지붕(어두운 나무)과 돌 테두리, 창·화덕·화로·등불은 빛을 무시하는 따뜻한 점(빛은 TownLighting).
    /// 색은 갈색·베이지·돌 회색·흑백 계열만 쓴다(등급색·공격 예고 빨강·전설 주황 금지).
    /// 권양기 틀 위 등불 12개: 켜진 수(12 − 맡긴 명패)만큼 등잔빛, 나머지는 #2A2520(SetLitLamps).
    /// 정렬: 땅 −1010·−1000, 바닥 자국 −950, 건물 −900대, 키 있는 소품은 Y 정렬(WorldProps.SortY, 플레이어·주민과 같은 식).
    /// </summary>
    public sealed class TownProps : MonoBehaviour
    {
        const int OuterGroundOrder = -1010;
        const int GroundOrder = -1000;
        const int PathOrder = -990;
        const int BuildingOrder = -900;

        static readonly Color Roof = Hex(0x3B2B1E);
        static readonly Color RoofDark = Hex(0x2A1D12);
        static readonly Color Planks = Hex(0x4A3826);
        static readonly Color Timber = Palette.DungeonTimber;
        static readonly Color TimberDark = Palette.DungeonTimberDark;
        static readonly Color Iron = Hex(0x2E2B28);
        static readonly Color StoneGrey = new Color(0.42f, 0.40f, 0.37f, 1f);
        static readonly Color Chalk = new Color(0.82f, 0.79f, 0.72f, 0.55f);
        static readonly Color Paper = new Color(0.80f, 0.76f, 0.66f, 1f);
        /// <summary>창 불빛·등불 심지(빛 무시). 기존 등잔빛 색(Palette.TorchLight) 계열.</summary>
        static readonly Color WindowGlow = new Color(1f, 0.82f, 0.58f, 0.9f);
        static readonly Color GlowHalo = new Color(1f, 0.82f, 0.6f, 0.28f);
        static readonly Color LampOff = Hex(0x2A2520);

        readonly List<SpriteRenderer> _lampCores = new List<SpriteRenderer>(TownLayout.LampCount);
        readonly List<SpriteRenderer> _lampGlows = new List<SpriteRenderer>(TownLayout.LampCount);

        /// <summary>지금 켜진 권양기 등불 수.</summary>
        public int LitLamps { get; private set; } = -1;

        public static TownProps Build(Transform parent, int litLamps)
        {
            var go = new GameObject("Town Props");
            go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;
            var props = go.AddComponent<TownProps>();
            props.BuildAll();
            props.SetLitLamps(litLamps);
            return props;
        }

        /// <summary>켜진 등불 수를 바꾼다(앞에서부터 켬). 꺼진 등불은 어두운 점만 남는다.</summary>
        public void SetLitLamps(int lit)
        {
            lit = Mathf.Clamp(lit, 0, TownLayout.LampCount);
            LitLamps = lit;
            for (int i = 0; i < _lampCores.Count; i++)
            {
                bool on = i < lit;
                // 꺼진 등불도 빛을 무시하는 재질이라 빛 세기와 상관없이 #2A2520 그대로 보인다.
                if (_lampCores[i]) _lampCores[i].color = on ? Palette.TorchLight : LampOff;
                if (i < _lampGlows.Count && _lampGlows[i]) _lampGlows[i].enabled = on;
            }
        }

        void BuildAll()
        {
            TownModularArtV058.BuildAll(transform, _lampCores, _lampGlows);
        }

        // ── 땅(2-1: 걷는 땅 30×20, 칠하는 땅 둘레 5유닛 더) ──────────

        void BuildGround(Transform t)
        {
            var paint = ToRect(TownLayout.Paint);
            var outer = ShapeSprites.DirtFloor(paint, 9001, System.Array.Empty<Rect>());
            Textured(t, "Outer ground", paint, outer, new Color(0.5f, 0.48f, 0.46f, 1f), OuterGroundOrder);

            var occluders = new List<Rect>();
            foreach (var b in TownLayout.Solids()) occluders.Add(ToRect(b));
            var walk = ToRect(TownLayout.Walk);
            var plaza = ShapeSprites.DirtFloor(walk, 9002, occluders);
            Textured(t, "Yard ground", walk, plaza, Color.white, GroundOrder);

            // 돌판 길: 남쪽 수레길 → 권양기, 대장간 앞 → 울타리 문.
            var stone = new Color(0.40f, 0.38f, 0.35f, 0.38f);
            float gx = (TownLayout.SouthGapXMin + TownLayout.SouthGapXMax) * 0.5f;
            float pathW = TownLayout.SouthGapXMax - TownLayout.SouthGapXMin;
            float y0 = TownLayout.Walk.YMin, y1 = TownLayout.Winch.YMin;
            Flat(t, "Path north", new Vector2(gx, (y0 + y1) * 0.5f), new Vector2(pathW, y1 - y0), stone, PathOrder);
            float cy = TownLayout.ReturnPoint.Y - 1f;
            float cx0 = TownLayout.Smithy.XMax + 1f, cx1 = TownLayout.Fence.XMin;
            Flat(t, "Path east", new Vector2((cx0 + cx1) * 0.5f, cy), new Vector2(cx1 - cx0, 2.2f), new Color(stone.r, stone.g, stone.b, 0.3f), PathOrder);
            // 돌판 틈(가로 줄).
            var seam = new Color(0.12f, 0.11f, 0.10f, 0.35f);
            for (float y = y0 + 1.2f; y < y1 - 0.4f; y += 1.4f)
                WorldProps.Stroke(t, "Path seam", new Vector2(gx - pathW * 0.5f + 0.1f, y), new Vector2(gx + pathW * 0.5f - 0.1f, y + 0.08f), 0.05f, seam, PathOrder + 1, false);
        }

        // ── 충돌 상자 + 바깥 벽 그림 ──────────────────────────

        void BuildSolids(Transform t)
        {
            foreach (var b in TownLayout.Solids())
            {
                if (TownApprovedArtV057.Available && TownApprovedArtV057.IsRoom(b)) TownApprovedArtV057.RoomSolids(t, b);
                else Solid(t, b);
            }
            // 바깥 테두리: 북쪽은 언덕(거친 바위), 나머지는 돌담.
            foreach (var b in TownLayout.OuterWalls())
            {
                var r = ToRect(b);
                bool north = b.YMin >= TownLayout.Walk.YMax - 0.01f;
                var sprite = north ? ShapeSprites.RoughRock(r) : ShapeSprites.StoneWall(r);
                Textured(t, "Wall " + b.Name, r, sprite, new Color(0.75f, 0.74f, 0.72f, 1f), BuildingOrder);
            }
        }

        /// <summary>충돌 상자 하나(Wall 레이어, 그림자 없음). 마을에는 시야 계산이 없어 시야 등록도 하지 않는다.</summary>
        static BoxCollider2D Solid(Transform parent, TownBox b)
        {
            var go = new GameObject("Solid " + b.Name);
            go.layer = Layers.Wall;
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector2(b.Center.X, b.Center.Y);
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(b.Width, b.Height);
            return col;
        }

        /// <summary>건물 한 채(위에서 본 모습): 돌 테두리 + 안쪽 나무 지붕 + 용마루. 충돌은 BuildSolids가 이미 둠.</summary>
        void Building(Transform t, TownBox b, Color roof)
        {
            if (TownApprovedArtV057.TryBuild(t, b)) return;
            var r = ToRect(b);
            Textured(t, b.Name + " walls", r, ShapeSprites.StoneWall(r), new Color(0.7f, 0.68f, 0.66f, 1f), BuildingOrder);
            const float rim = 0.38f;
            var inner = new Rect(r.xMin + rim, r.yMin + rim, r.width - rim * 2f, r.height - rim * 2f);
            Flat(t, b.Name + " roof", inner.center, inner.size, roof, BuildingOrder + 1);
            // 용마루(긴 쪽)와 지붕 널 줄.
            bool wide = inner.width >= inner.height;
            var ridgeA = wide ? new Vector2(inner.xMin, inner.center.y) : new Vector2(inner.center.x, inner.yMin);
            var ridgeB = wide ? new Vector2(inner.xMax, inner.center.y) : new Vector2(inner.center.x, inner.yMax);
            WorldProps.Stroke(t, b.Name + " ridge", ridgeA, ridgeB, 0.16f, RoofDark, BuildingOrder + 2, false);
            var line = new Color(RoofDark.r, RoofDark.g, RoofDark.b, 0.55f);
            if (wide)
                for (float x = inner.xMin + 0.9f; x < inner.xMax - 0.3f; x += 0.9f)
                    WorldProps.Stroke(t, b.Name + " shingle", new Vector2(x, inner.yMin), new Vector2(x, inner.yMax), 0.04f, line, BuildingOrder + 2, false);
            else
                for (float y = inner.yMin + 0.9f; y < inner.yMax - 0.3f; y += 0.9f)
                    WorldProps.Stroke(t, b.Name + " shingle", new Vector2(inner.xMin, y), new Vector2(inner.xMax, y), 0.04f, line, BuildingOrder + 2, false);
        }

        /// <summary>창 불빛(빛 무시): 작은 따뜻한 띠 + 흐린 무리.</summary>
        static void Window(Transform t, string name, Vector2 at, Vector2 size, Vector2 haloOffset)
        {
            var sr = Flat(t, name, at, size, WindowGlow, BuildingOrder + 3);
            RenderMaterials.MakeUnlit(sr);
            WorldProps.Shape(t, name + " glow", at + haloOffset, new Vector2(size.x + 1.4f, 1.3f), WorldProps.SoftDot, GlowHalo, BuildingOrder + 4, true);
        }

        // ── 권양기 승강장(x 38~46, y 46~49) ─────────────────────

        void BuildWinch(Transform t)
        {
            var b = TownLayout.Winch;
            var r = ToRect(b);
            Flat(t, "Winch deck", r.center, r.size, Planks, BuildingOrder);
            var seam = new Color(TimberDark.r, TimberDark.g, TimberDark.b, 0.7f);
            for (float x = r.xMin + 0.5f; x < r.xMax - 0.2f; x += 0.5f)
                WorldProps.Stroke(t, "Deck plank", new Vector2(x, r.yMin + 0.05f), new Vector2(x, r.yMax - 0.05f), 0.04f, seam, BuildingOrder + 1, false);
            // 틀: 기둥 둘 + 등불을 단 가로대(y 48.6).
            float ly = TownLayout.LampY;
            float px0 = TownLayout.LampX0 - 0.45f;
            float px1 = TownLayout.LampX0 + TownLayout.LampStep * (TownLayout.LampCount - 1) + 0.45f;
            foreach (float px in new[] { px0, px1 })
                Flat(t, "Winch post", new Vector2(px, ly), new Vector2(0.42f, 0.42f), TimberDark, WorldProps.SortY(ly, 1));
            Flat(t, "Winch beam", new Vector2((px0 + px1) * 0.5f, ly), new Vector2(px1 - px0, 0.2f), Timber, WorldProps.SortY(ly));
            // 바퀴·밧줄·바구니.
            var wheelAt = new Vector2(42f, 47.55f);
            WorldProps.Shape(t, "Winch wheel", wheelAt, new Vector2(1.5f, 1.5f), ShapeSprites.Ring, Hex(0x5C554C), WorldProps.SortY(wheelAt.y, 2), false);
            for (int i = 0; i < 4; i++)
            {
                var d = WorldProps.Rotate(Vector2.right * 0.7f, 45f * i);
                WorldProps.Stroke(t, "Wheel spoke", wheelAt - d, wheelAt + d, 0.06f, Hex(0x4A433C), WorldProps.SortY(wheelAt.y, 2), false);
            }
            var basketAt = new Vector2(TownLayout.Gate.Pos.X, b.YMin + 0.55f);
            WorldProps.Stroke(t, "Rope", wheelAt, basketAt + new Vector2(0f, 0.3f), 0.06f, Hex(0xBFB39A), WorldProps.SortY(basketAt.y, 3), false);
            Flat(t, "Basket", basketAt, new Vector2(1.3f, 0.9f), Hex(0x6E5236), WorldProps.SortY(basketAt.y, 1));
            Flat(t, "Basket inside", basketAt, new Vector2(0.95f, 0.58f), TimberDark, WorldProps.SortY(basketAt.y, 2));

            // 등불 12개(켬 = 등잔빛, 꺼짐 = #2A2520).
            for (int i = 0; i < TownLayout.LampCount; i++)
            {
                var p = TownLayout.LampPos(i);
                var at = new Vector2(p.X, p.Y);
                var glow = WorldProps.Shape(t, "Lamp glow " + i, at, new Vector2(0.75f, 0.75f), WorldProps.SoftDot, new Color(1f, 0.82f, 0.6f, 0.45f), WorldProps.SortY(ly, 3), true);
                var core = WorldProps.Shape(t, "Lamp " + i, at, new Vector2(0.2f, 0.2f), ShapeSprites.Circle, Palette.TorchLight, WorldProps.SortY(ly, 4), true);
                _lampGlows.Add(glow);
                _lampCores.Add(core);
            }
        }

        // ── 대장간(x 28~35, y 40~47)·모루 ───────────────────────

        void BuildSmithy(Transform t)
        {
            if (TownApprovedArtV057.TryBuild(t, TownLayout.Smithy))
            {
                TownApprovedArtV057.TryAnvil(t);
                return;
            }
            Building(t, TownLayout.Smithy, Roof);
            var s = TownLayout.Smithy;
            // 굴뚝(북서 구석).
            Flat(t, "Chimney", new Vector2(s.XMin + 1.4f, s.YMax - 1.3f), new Vector2(0.95f, 0.95f), Iron, BuildingOrder + 3);
            Flat(t, "Chimney mouth", new Vector2(s.XMin + 1.4f, s.YMax - 1.3f), new Vector2(0.55f, 0.55f), Hex(0x15120F), BuildingOrder + 4);
            // 화덕 아궁이(남쪽 벽, 화덕 불빛 자리 x).
            float fx = TownLayout.ForgeGlow.X;
            var mouth = new Vector2(fx, s.YMin + 0.25f);
            Flat(t, "Forge mouth", mouth, new Vector2(2.0f, 0.5f), Hex(0x1A1512), BuildingOrder + 3);
            var coals = Flat(t, "Forge coals", mouth + new Vector2(0f, 0.02f), new Vector2(1.5f, 0.24f), new Color(1f, 0.86f, 0.55f, 0.85f), BuildingOrder + 4);
            RenderMaterials.MakeUnlit(coals);
            WorldProps.Shape(t, "Forge glow", mouth + new Vector2(0f, -0.35f), new Vector2(3.2f, 1.6f), WorldProps.SoftDot, new Color(1f, 0.8f, 0.55f, 0.32f), BuildingOrder + 5, true);

            // 모루(충돌 없음, F 자리 그대로).
            var a = new Vector2(TownLayout.Anvil.Pos.X, TownLayout.Anvil.Pos.Y);
            int order = WorldProps.SortY(a.y);
            Flat(t, "Anvil stump", a + new Vector2(0f, -0.12f), new Vector2(0.62f, 0.5f), TimberDark, order - 1);
            Flat(t, "Anvil", a, new Vector2(0.78f, 0.34f), Hex(0x3A3633), order);
            WorldProps.Shape(t, "Anvil horn", a + new Vector2(0.5f, 0f), new Vector2(0.32f, 0.26f), ShapeSprites.Triangle, Hex(0x3A3633), order, false);
            Flat(t, "Anvil face", a + new Vector2(-0.05f, 0.08f), new Vector2(0.6f, 0.08f), Hex(0x6A645C), order + 1);
        }

        // ── 주막(x 28~37, y 29~35)·의뢰 게시판 ───────────────────

        void BuildTavern(Transform t)
        {
            Building(t, TownLayout.Tavern, Roof);
            float wallY = TownLayout.Tavern.YMax - 0.17f;
            foreach (var w in TownLayout.TavernWindows) Window(t, "Tavern window", new Vector2(w.X, wallY), new Vector2(0.9f, 0.22f), new Vector2(0f, 0.55f));
            // 덧문(문 자리 = 덧문 F 자리, 불빛이 틈으로 샘).
            var door = new Vector2(TownLayout.TavernDoor.X, wallY);
            Flat(t, "Tavern shutter", door, new Vector2(1.05f, 0.34f), TimberDark, BuildingOrder + 3);
            var crack = Flat(t, "Shutter crack", door + new Vector2(0f, 0.02f), new Vector2(0.05f, 0.28f), WindowGlow, BuildingOrder + 4);
            RenderMaterials.MakeUnlit(crack);

            // 의뢰 게시판(기둥 둘 + 판 + 종이 셋, 충돌 없음).
            var b = new Vector2(TownLayout.Board.Pos.X, TownLayout.Board.Pos.Y);
            int order = WorldProps.SortY(b.y - 0.4f);
            Flat(t, "Board post", b + new Vector2(-0.6f, -0.25f), new Vector2(0.14f, 0.8f), TimberDark, order);
            Flat(t, "Board post", b + new Vector2(0.6f, -0.25f), new Vector2(0.14f, 0.8f), TimberDark, order);
            Flat(t, "Board", b + new Vector2(0f, 0.12f), new Vector2(1.5f, 0.78f), Timber, order + 1);
            Flat(t, "Notice", b + new Vector2(-0.42f, 0.16f), new Vector2(0.32f, 0.38f), Paper, order + 2, -6f);
            Flat(t, "Notice", b + new Vector2(0.02f, 0.1f), new Vector2(0.34f, 0.42f), Paper, order + 2, 3f);
            Flat(t, "Notice", b + new Vector2(0.44f, 0.18f), new Vector2(0.3f, 0.34f), new Color(Paper.r * 0.9f, Paper.g * 0.9f, Paper.b * 0.88f, 1f), order + 2, 8f);
        }

        // ── 경비 초소(x 51~57, y 44.5~49)·화로 ───────────────────

        void BuildGuardPost(Transform t)
        {
            Building(t, TownLayout.GuardPost, Hex(0x34281D));
            var p = new Vector2(TownLayout.Brazier.X, TownLayout.Brazier.Y);
            int order = WorldProps.SortY(p.y);
            for (int i = 0; i < 3; i++)
            {
                var leg = WorldProps.Rotate(new Vector2(0.42f, 0f), 90f + 120f * i);
                WorldProps.Stroke(t, "Brazier leg", p, p + leg, 0.07f, Iron, order - 1, false);
            }
            WorldProps.Shape(t, "Brazier", p, new Vector2(0.8f, 0.8f), ShapeSprites.Circle, Hex(0x2A2622), order, false);
            WorldProps.Shape(t, "Brazier coals", p, new Vector2(0.5f, 0.5f), ShapeSprites.Circle, new Color(1f, 0.86f, 0.55f, 0.85f), order + 1, true);
            WorldProps.Shape(t, "Brazier glow", p, new Vector2(1.8f, 1.8f), WorldProps.SoftDot, new Color(1f, 0.8f, 0.55f, 0.35f), order + 2, true);
        }

        // ── 훈련 울타리(x 51~57, y 35~43, 서쪽 문 y 38~40)·쓰러진 허수아비 ──

        void BuildFence(Transform t)
        {
            foreach (var seg in TownLayout.FenceSegments())
            {
                var r = ToRect(seg);
                Flat(t, "Fence rail", r.center, r.size, Timber, WorldProps.SortY(r.yMin));
                bool vertical = r.height > r.width;
                float len = vertical ? r.height : r.width;
                int posts = Mathf.Max(2, Mathf.RoundToInt(len / 1.5f) + 1);
                for (int i = 0; i < posts; i++)
                {
                    float k = posts == 1 ? 0.5f : i / (float)(posts - 1);
                    var at = vertical ? new Vector2(r.center.x, Mathf.Lerp(r.yMin + 0.15f, r.yMax - 0.15f, k)) : new Vector2(Mathf.Lerp(r.xMin + 0.15f, r.xMax - 0.15f, k), r.center.y);
                    Flat(t, "Fence post", at, new Vector2(0.34f, 0.34f), TimberDark, WorldProps.SortY(at.y, 1));
                }
            }
            // 쓰러진 허수아비(장식, 첫 판은 훈련장 전투 없음).
            var d = new Vector2(TownLayout.Dummy.X, TownLayout.Dummy.Y);
            int order = WorldProps.SortY(d.y);
            Flat(t, "Dummy post", d, new Vector2(1.7f, 0.24f), Palette.WoodDummy, order, 24f);
            Flat(t, "Dummy arms", d + WorldProps.Rotate(new Vector2(0.25f, 0f), 24f), new Vector2(0.22f, 1.0f), Palette.WoodDummy, order, 24f);
            WorldProps.Shape(t, "Dummy head", d + WorldProps.Rotate(new Vector2(0.95f, 0f), 24f), new Vector2(0.44f, 0.44f), ShapeSprites.Circle, Palette.RatDummy, order + 1, false);
        }

        // ── 집 1채(x 46~53, y 29~33) ─────────────────────────

        void BuildHouse(Transform t)
        {
            Building(t, TownLayout.House, Hex(0x3F2F21));
            float wallY = TownLayout.House.YMax - 0.17f;
            foreach (var w in TownLayout.HouseWindows) Window(t, "House window", new Vector2(w.X, wallY), new Vector2(0.8f, 0.22f), new Vector2(0f, 0.55f));
        }

        // ── 남쪽 수레길을 막은 수레 ──────────────────────────────

        void BuildCart(Transform t)
        {
            var c = new Vector2(TownLayout.CartPos.X, TownLayout.CartPos.Y);
            var box = ToRect(TownLayout.CartBox);
            int order = WorldProps.SortY(c.y);
            float wx = box.width * 0.28f;
            foreach (float sx in new[] { -wx, wx })
            foreach (float sy in new[] { -0.52f, 0.52f })
                Flat(t, "Cart wheel", c + new Vector2(sx, sy), new Vector2(0.7f, 0.16f), TimberDark, order - 1);
            Flat(t, "Cart bed", c, new Vector2(box.width - 0.3f, box.height - 0.1f), Hex(0x4F3B27), order);
            for (float x = box.xMin + 0.45f; x < box.xMax - 0.3f; x += 0.45f)
                WorldProps.Stroke(t, "Cart plank", new Vector2(x, c.y - box.height * 0.42f), new Vector2(x, c.y + box.height * 0.42f), 0.04f, TimberDark, order + 1, false);
            Flat(t, "Cart shaft", c + new Vector2(box.width * 0.5f + 0.4f, 0f), new Vector2(0.9f, 0.1f), Timber, order);
        }

        // ── 귀환 지점(42, 41) 돌 원·등 ────────────────────────────

        void BuildReturnPoint(Transform t)
        {
            var p = new Vector2(TownLayout.ReturnPoint.X, TownLayout.ReturnPoint.Y);
            WorldProps.Shape(t, "Return ring", p, new Vector2(2.1f, 2.1f), ShapeSprites.Ring, new Color(StoneGrey.r, StoneGrey.g, StoneGrey.b, 0.85f), WorldProps.FloorDecalOrder, false);
            for (int i = 0; i < 8; i++)
            {
                var at = p + WorldProps.Rotate(new Vector2(1.05f, 0f), 22.5f + 45f * i);
                WorldProps.Shape(t, "Return stone", at, new Vector2(0.26f, 0.22f), ShapeSprites.Circle, Hex(0x5C554C), WorldProps.FloorDecalOrder + 1, false);
            }
            var lamp = new Vector2(TownLayout.ReturnLamp.X, TownLayout.ReturnLamp.Y);
            int order = WorldProps.SortY(lamp.y - 0.3f);
            Flat(t, "Return lamp post", lamp + new Vector2(0f, -0.3f), new Vector2(0.14f, 0.7f), Iron, order);
            WorldProps.Shape(t, "Return lamp", lamp + new Vector2(0f, 0.08f), new Vector2(0.24f, 0.24f), ShapeSprites.Circle, Palette.TorchLight, order + 1, true);
            WorldProps.Shape(t, "Return lamp glow", lamp + new Vector2(0f, 0.08f), new Vector2(1.0f, 1.0f), WorldProps.SoftDot, new Color(1f, 0.82f, 0.6f, 0.4f), order + 2, true);
        }

        // ── 분필 화살표 3개(새 플레이 시작 → 마당) ──────────────────

        void BuildChalkArrows(Transform t)
        {
            foreach (var a in TownLayout.ChalkArrows)
            {
                var p = new Vector2(a.X, a.Y);
                WorldProps.Stroke(t, "Chalk arrow", p + new Vector2(0f, -0.45f), p + new Vector2(0f, 0.38f), 0.07f, Chalk, WorldProps.FloorDecalOrder, false);
                WorldProps.Stroke(t, "Chalk arrow", p + new Vector2(-0.28f, 0.1f), p + new Vector2(0f, 0.4f), 0.07f, Chalk, WorldProps.FloorDecalOrder, false);
                WorldProps.Stroke(t, "Chalk arrow", p + new Vector2(0.28f, 0.1f), p + new Vector2(0f, 0.4f), 0.07f, Chalk, WorldProps.FloorDecalOrder, false);
            }
        }

        // ── 도움 함수 ─────────────────────────────────────────

        static SpriteRenderer Flat(Transform t, string name, Vector2 at, Vector2 size, Color color, int order, float rotationDeg = 0f) =>
            WorldProps.Shape(t, name, at, size, ShapeSprites.Square, color, order, false, rotationDeg);

        /// <summary>절차 텍스처 한 장을 사각형에 꼭 맞게 깐다(그림·텍스처는 물체와 함께 지움).</summary>
        static SpriteRenderer Textured(Transform t, string name, Rect r, Sprite sprite, Color tint, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(t, false);
            go.transform.position = r.center;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = tint;
            sr.sortingOrder = order;
            Vector2 native = sprite ? (Vector2)sprite.bounds.size : Vector2.one;
            go.transform.localScale = new Vector3(r.width / Mathf.Max(0.01f, native.x), r.height / Mathf.Max(0.01f, native.y), 1f);
            RuntimeAssetOwner.Own(go, sprite);
            return sr;
        }

        static Rect ToRect(TownBox b) => Rect.MinMaxRect(b.XMin, b.YMin, b.XMax, b.YMax);

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
    }
}
