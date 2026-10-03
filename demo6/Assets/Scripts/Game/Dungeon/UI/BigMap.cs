using System.Collections.Generic;
using Demo6.Core.Dungeon;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demo6.Game
{
    /// <summary>
    /// 큰 지도(M)·칸 안개·조사율(3차 초안 2-4). 지도는 큰 지도 하나이고 여는 동안 시간이 멈춘다(DungeonUi 창 "map").
    /// 안개는 칸 단위다: 들어가 본 칸만 채워 그리고, 가 본 칸에서 보이는 길(열린 길·금 간 벽·자물쇠 문, 판자벽은 부순 뒤)로 이어진 칸은 '?'(안 간 출구)로 그린다.
    /// 가 본 칸 안에는 말뚝·벽 등잔(켜짐/꺼짐), 안 연 궤짝, 안 주운 이야기, 사건, 곡괭이, 계단을 적고, 능력 문에는 필요한 능력(곡·열)을 적는다.
    /// 층의 벽 등잔을 모두 켜면 숨은 방 칸에 흐린 '?'가 뜬다(2-5). 작은 지도·분필 표시는 M6에서 넣는다.
    /// </summary>
    public sealed class BigMap : MonoBehaviour
    {
        public const string MapModal = "map";

        const float Pad = 24f;
        const float HeaderHeight = 70f;
        const float LegendHeight = 70f;
        const float Gap = 56f;
        const float EdgeThickness = 4f;
        const float IconLine = 17f;

        // 다크 판타지 1차: 칸은 검게 그을린 양피지, 테는 바랜 뼈색, 지금 칸은 횃불 호박색. 능력 문(곡·열)·켜짐 색 뜻은 그대로.
        static readonly Color VisitedFill = new Color(0.17f, 0.15f, 0.125f, 1f);
        static readonly Color RevealedFill = new Color(0.1f, 0.09f, 0.08f, 1f);
        static readonly Color CellBorder = new Color(0.56f, 0.5f, 0.41f, 1f);
        static readonly Color CurrentBorder = new Color(0.93f, 0.72f, 0.4f, 1f);
        static readonly Color UnknownBorder = new Color(0.55f, 0.5f, 0.42f, 0.85f);
        static readonly Color PathColor = new Color(0.52f, 0.47f, 0.38f, 1f);
        static readonly Color HereColor = new Color(0.95f, 0.78f, 0.48f, 1f);
        static readonly Color NeedColor = new Color(0.9f, 0.62f, 0.4f, 1f);
        static readonly Color ReadyColor = new Color(0.55f, 0.9f, 0.5f, 1f);
        static readonly Color WoodColor = new Color(0.78f, 0.56f, 0.3f, 1f);
        static readonly Color IronColor = new Color(0.72f, 0.78f, 0.85f, 1f);
        static readonly Color LitColor = new Color(1f, 0.88f, 0.55f, 1f);
        static readonly Color UnlitColor = new Color(0.45f, 0.42f, 0.38f, 1f);
        static readonly Color StoryColor = new Color(0.95f, 0.95f, 1f, 1f);
        static readonly Color EventColor = new Color(0.92f, 0.5f, 0.42f, 1f);
        static readonly Color AbilityColor = new Color(0.6f, 0.88f, 0.55f, 1f);
        static readonly Color StairsColor = new Color(0.6f, 0.8f, 1f, 1f);

        public static BigMap Instance { get; private set; }

        /// <summary>시험 패널(F1) '지도 전부 보기': 안개를 무시하고 모든 칸·길·물건을 그린다.</summary>
        public bool RevealAll { get; set; }
        public bool IsOpen => DungeonUi.Modal == MapModal;

        readonly List<(int order, Color color, string text)> _icons = new List<(int, Color, string)>();
        GUIStyle _iconStyle;
        GUIStyle _bigQuestion;
        GUIStyle _rightTitle;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !DungeonRoot.Instance) return;
            // 멈춘 동안에도 들어야 하므로 키보드를 직접 읽는다(계약 10).
            if (kb.mKey.wasPressedThisFrame)
            {
                if (IsOpen) DungeonUi.Close(MapModal);
                else if (!DungeonUi.ModalOpen) DungeonUi.TryOpen(MapModal);
            }
            else if (kb.escapeKey.wasPressedThisFrame && IsOpen)
            {
                DungeonUi.Close(MapModal);
            }
        }

        /// <summary>한 번만 받는 것이 놓인 칸(등록할 때 칸이 없었으면 자리로 찾는다).</summary>
        public static DungeonCell CellOf(OneTimeEntry e)
        {
            if (e == null) return null;
            if (e.Cell != null) return e.Cell;
            var root = DungeonRoot.Instance;
            return root && root.World != null ? root.World.CellAt(e.Position) : null;
        }

        /// <summary>지도에 보이는 길인가: 판자벽은 일반 벽과 똑같이 보여(2-5) 부순 뒤에만 보인다.</summary>
        bool EdgeVisible(DungeonEdge e) => RevealAll || e.Kind != EdgeKind.Plank || e.Opened;

        /// <summary>안 간 출구: 가 본 칸에서 보이는 길로 이어진 안 간 칸.</summary>
        bool IsFrontier(DungeonCell c)
        {
            if (c.Visited) return false;
            foreach (var e in c.Edges)
                if (EdgeVisible(e) && e.Other(c).Visited) return true;
            return false;
        }

        /// <summary>층의 벽 등잔을 모두 켰는가(숨은 방 흐린 '?', 2-5).</summary>
        static bool AllLampsLit(DungeonState state)
        {
            int lamps = 0;
            foreach (var e in state.OneTime.Values)
            {
                if (e.Kind != DiscoveryKind.WallLamp) continue;
                lamps++;
                if (!e.Done) return false;
            }
            return lamps > 0;
        }

        void OnGUI()
        {
            if (!IsOpen) return;
            var root = DungeonRoot.Instance;
            if (!root || root.World == null || root.Map == null || root.State == null) return;
            DungeonUi.Begin();
            GUI.depth = -10;
            if (_iconStyle == null)
            {
                _iconStyle = new GUIStyle(DungeonUi.Small) { wordWrap = false, clipping = TextClipping.Clip };
                _bigQuestion = new GUIStyle(DungeonUi.BigCenter) { fontSize = 34 };
                _rightTitle = new GUIStyle(DungeonUi.Title) { alignment = TextAnchor.UpperRight };
            }
            Draw(root);
        }

        void Draw(DungeonRoot root)
        {
            var world = root.World;
            var state = root.State;
            int cols = Mathf.Max(1, root.Map.Width);
            int rows = Mathf.Max(1, root.Map.Height);

            float reserved = ExplorationLog.Instance ? ExplorationLog.Instance.PanelReservedWidth : 0f;
            float availW = Mathf.Max(400f, DungeonUi.Width - reserved - 40f);
            float cellW = Mathf.Clamp((availW - Pad * 2f - Gap * (cols - 1)) / cols, 110f, 190f);
            float cellH = Mathf.Max(90f, Mathf.Round(cellW * 0.6f));
            float gridW = cols * cellW + (cols - 1) * Gap;
            float gridH = rows * cellH + (rows - 1) * Gap;
            float boxW = gridW + Pad * 2f;
            float boxH = HeaderHeight + gridH + LegendHeight + Pad * 2f;
            float boxX = Mathf.Max(10f, (DungeonUi.Width - reserved - boxW) * 0.5f);
            float boxY = Mathf.Max(10f, (DungeonUi.Height - boxH) * 0.5f);
            var origin = new Vector2(boxX + Pad, boxY + Pad + HeaderHeight);

            Rect CellRect(DungeonCell c) =>
                new Rect(origin.x + c.Map.X * (cellW + Gap), origin.y + (rows - 1 - c.Map.Y) * (cellH + Gap), cellW, cellH);

            var screen = new Rect(0f, 0f, DungeonUi.Width, DungeonUi.Height);
            DungeonUi.Fill(screen, new Color(0f, 0f, 0f, 0.55f));
            DungeonUi.Vignette(screen, new Color(0f, 0f, 0f, 0.6f));
            DungeonUi.Box(new Rect(boxX, boxY, boxW, boxH), 0.96f);

            // 머리: 층 이름, 조사율, 가진 능력.
            float survey = state.Survey(world.Cells.Count);
            GUI.Label(new Rect(boxX + Pad, boxY + 14f, boxW * 0.6f, 30f), $"제{root.Floor}층 — {root.Map.Name}", DungeonUi.Title);
            GUI.Label(new Rect(boxX + boxW * 0.5f, boxY + 14f, boxW * 0.5f - Pad, 30f), $"조사 {survey * 100f:0}%", _rightTitle);
            string abilities = state.HasPickaxe ? (state.HasKey ? "곡괭이, 열쇠" : "곡괭이") : (state.HasKey ? "열쇠" : "없음");
            GUI.Label(new Rect(boxX + Pad, boxY + 44f, boxW - Pad * 2f, 20f),
                $"가진 능력: {abilities}   ·   M·Esc 닫기 (여는 동안 시간이 멈춘다)" + (RevealAll ? "   ·   시험: 지도 전부 보기" : ""), DungeonUi.Small);

            // 길(칸 아래에 먼저 그린다).
            foreach (var e in world.Edges)
            {
                if (!EdgeVisible(e)) continue;
                if (!RevealAll && !e.A.Visited && !e.B.Visited) continue;
                DrawEdge(e, CellRect(e.A), CellRect(e.B), state);
            }

            // 칸.
            bool hiddenHint = AllLampsLit(state);
            var current = root.CurrentCell;
            foreach (var c in world.Cells)
            {
                var r = CellRect(c);
                if (c.Visited || RevealAll) DrawKnownCell(c, r, c == current, state);
                else if (IsFrontier(c)) DrawQuestion(r, 0.85f, true);
                else if (hiddenHint && c.Piece == PieceKind.Hidden) DrawQuestion(r, 0.3f, false);
            }

            // 지금 자리.
            var player = root.Player;
            if (player && current != null)
            {
                var r = CellRect(current);
                var b = current.Bounds;
                Vector2 p = player.Position;
                float u = Mathf.Clamp01((p.x - b.xMin) / b.width);
                float v = Mathf.Clamp01((p.y - b.yMin) / b.height);
                var at = new Vector2(r.x + u * r.width, r.y + (1f - v) * r.height);
                float s = 11f + 2f * Mathf.Sin(Time.unscaledTime * 6f);
                DungeonUi.Fill(new Rect(at.x - s * 0.5f - 2f, at.y - s * 0.5f - 2f, s + 4f, s + 4f), new Color(0f, 0f, 0f, 0.9f));
                DungeonUi.Fill(new Rect(at.x - s * 0.5f, at.y - s * 0.5f, s, s), HereColor);
            }

            // 범례.
            float ly = origin.y + gridH + 16f;
            float lw = boxW - Pad * 2f;
            GUI.Label(new Rect(boxX + Pad, ly, lw, 20f),
                "■ 가 본 칸   ? 안 간 출구   ● 지금 자리   ─ 열린 길   곡 금 간 벽(곡괭이)   열 자물쇠 문(열쇠)   초록 = 지금 열 수 있음", DungeonUi.Small);
            GUI.Label(new Rect(boxX + Pad, ly + 20f, lw, 20f),
                "말뚝·등잔은 켜짐/꺼짐, 궤짝·이야기·사건·곡괭이는 아직 안 받은 것만 적는다. 조사율은 가 본 칸과 한 번만 받는 것을 센다.", DungeonUi.Small);
            if (hiddenHint)
            {
                var prev = GUI.color;
                GUI.color = LitColor;
                GUI.Label(new Rect(boxX + Pad, ly + 40f, lw, 20f), "모든 등잔이 타오른다 — 흐린 '?' 자리에 아직 찾지 못한 곳이 있다.", DungeonUi.Small);
                GUI.color = prev;
            }
        }

        void DrawKnownCell(DungeonCell c, Rect r, bool current, DungeonState state)
        {
            DungeonUi.Fill(r, c.Visited ? VisitedFill : RevealedFill);
            DungeonUi.Outline(r, current ? CurrentBorder : CellBorder, current ? 3f : 2f);
            var prev = GUI.color;
            GUI.color = c.Visited ? DungeonUi.Bone : new Color(DungeonUi.Bone.r, DungeonUi.Bone.g, DungeonUi.Bone.b, 0.6f);
            GUI.Label(new Rect(r.x + 8f, r.y + 4f, r.width - 16f, 22f), c.Visited ? c.Name : c.Name + " (안 감)", DungeonUi.Bold);
            GUI.color = prev;

            CollectIcons(c, state);
            float y = r.y + 28f;
            foreach (var icon in _icons)
            {
                if (y + IconLine > r.yMax - 2f) break;
                DungeonUi.Fill(new Rect(r.x + 9f, y + 4f, 9f, 9f), icon.color);
                prev = GUI.color;
                GUI.color = icon.color;
                GUI.Label(new Rect(r.x + 23f, y, r.width - 30f, IconLine + 2f), icon.text, _iconStyle);
                GUI.color = prev;
                y += IconLine;
            }
        }

        void DrawQuestion(Rect r, float alpha, bool outline)
        {
            var c = UnknownBorder;
            c.a *= alpha;
            DungeonUi.Outline(r, c, outline ? 2f : 1f);
            var prev = GUI.color;
            GUI.color = new Color(0.86f, 0.79f, 0.66f, alpha);
            GUI.Label(r, "?", _bigQuestion);
            GUI.color = prev;
        }

        /// <summary>칸 안 표시: 계단, 말뚝, 등잔, 안 연 궤짝, 안 주운 이야기, 사건, 곡괭이, 금고.</summary>
        void CollectIcons(DungeonCell c, DungeonState state)
        {
            _icons.Clear();
            if (c.Map.Has(FeatureKind.Stairs)) _icons.Add((0, StairsColor, "계단"));
            foreach (var e in state.OneTime.Values)
            {
                if (CellOf(e) != c) continue;
                switch (e.Kind)
                {
                    case DiscoveryKind.Stake:
                    {
                        bool lit = e.Done || state.ActiveStakes.Contains(e.Id);
                        _icons.Add((1, lit ? LitColor : UnlitColor, lit ? "말뚝 켜짐" : "말뚝 꺼짐"));
                        break;
                    }
                    case DiscoveryKind.WallLamp:
                        _icons.Add((2, e.Done ? LitColor : UnlitColor, e.Done ? "등잔 켜짐" : "등잔 꺼짐"));
                        break;
                    case DiscoveryKind.WoodChest:
                        if (!e.Done) _icons.Add((3, WoodColor, "나무 궤짝"));
                        break;
                    case DiscoveryKind.IronChest:
                        if (!e.Done) _icons.Add((3, IronColor, "쇠 궤짝"));
                        break;
                    case DiscoveryKind.Story:
                        if (!e.Done) _icons.Add((4, StoryColor, string.IsNullOrEmpty(e.Label) ? "이야기" : "이야기 · " + e.Label));
                        break;
                    case DiscoveryKind.Event:
                        if (!e.Done) _icons.Add((5, EventColor, "사건"));
                        break;
                    case DiscoveryKind.Ability:
                        if (!e.Done) _icons.Add((6, AbilityColor, "곡괭이"));
                        break;
                    case DiscoveryKind.Safe:
                        if (!e.Done) _icons.Add((7, IronColor, "금고"));
                        break;
                }
            }
            _icons.Sort((a, b) => a.order.CompareTo(b.order));
        }

        /// <summary>칸 사이 길: 열린 길은 선, 막힌 능력 문은 점선 + 필요한 능력 글자(곡·열, 가졌으면 초록).</summary>
        void DrawEdge(DungeonEdge e, Rect ra, Rect rb, DungeonState state)
        {
            bool horizontal = Mathf.Abs(ra.center.y - rb.center.y) < 1f;
            Rect seg;
            if (horizontal)
            {
                var left = ra.x < rb.x ? ra : rb;
                var right = ra.x < rb.x ? rb : ra;
                seg = new Rect(left.xMax, left.center.y - EdgeThickness * 0.5f, right.x - left.xMax, EdgeThickness);
            }
            else
            {
                var top = ra.y < rb.y ? ra : rb;
                var bottom = ra.y < rb.y ? rb : ra;
                seg = new Rect(top.center.x - EdgeThickness * 0.5f, top.yMax, EdgeThickness, bottom.y - top.yMax);
            }

            if (e.Opened)
            {
                DungeonUi.Fill(seg, PathColor);
                return;
            }

            string need;
            bool ready;
            switch (e.Kind)
            {
                case EdgeKind.Cracked:
                    need = "곡";
                    ready = state.HasPickaxe;
                    break;
                case EdgeKind.Locked:
                    need = "열";
                    ready = state.HasKey;
                    break;
                case EdgeKind.Plank:
                    need = "판";
                    ready = true;
                    break;
                default:
                    need = "";
                    ready = true;
                    break;
            }
            var color = ready ? ReadyColor : NeedColor;
            DrawDashed(seg, horizontal, new Color(color.r, color.g, color.b, 0.75f));
            if (need.Length == 0) return;
            var label = new Rect(seg.center.x - 14f, seg.center.y - 12f, 28f, 24f);
            DungeonUi.Fill(label, new Color(0.08f, 0.07f, 0.06f, 1f));
            DungeonUi.Outline(label, color, 1f);
            var prev = GUI.color;
            GUI.color = color;
            GUI.Label(label, need, DungeonUi.Center);
            GUI.color = prev;
        }

        static void DrawDashed(Rect seg, bool horizontal, Color color)
        {
            const float Dash = 6f;
            const float Space = 5f;
            float length = horizontal ? seg.width : seg.height;
            for (float t = 0f; t < length; t += Dash + Space)
            {
                float l = Mathf.Min(Dash, length - t);
                var part = horizontal
                    ? new Rect(seg.x + t, seg.y, l, seg.height)
                    : new Rect(seg.x, seg.y + t, seg.width, l);
                DungeonUi.Fill(part, color);
            }
        }
    }
}
