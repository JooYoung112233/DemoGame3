using System.Collections.Generic;
using Demo6.Core.Progression;
using Demo6.Core.Town;
using UnityEngine;
namespace Demo6.Game
{
    /// <summary>
    /// 스킬 트리 창 그리기(기획/스킬-자원-트리-1차.md 4장). 왼쪽은 가로로 뻗는 나무(열 = 레벨 단계, 줄 = 갈래)를 안쪽 스크롤로 보이고
    /// (휠·스크롤 막대·빈 곳 끌기), 오른쪽은 고른 칸 설명과 점수 쓰기 단추. 그림은 기존 틀(window-frame·action-frame)과 아이콘만 쓴다.
    /// 뒤쪽 두 열(Lv 10·15)은 더 깊은 갱도에서 열릴 자리의 빈 틀이다(효과 없음).
    /// </summary>
    public sealed partial class SkillPanel
    {
        const float NodeSize = 76f;
        const float ColGap = 240f;
        const float RowGap = 150f;
        const float ContentLeft = 150f;
        const float ContentTop = 70f;
        const int Columns = 5;

        static readonly string[] ColumnLabels = { "Lv 2 · 배우기", "랭크 올리기", "Lv 4~5 · 갈림", "Lv 10", "Lv 15" };
        static readonly (string Name, float From, float To)[] Branches = { ("회오리", 0f, 1f), ("검풍", 2f, 3f), ("몸", 4f, 5f) };
        /// <summary>뒤쪽 빈 틀 자리(열, 줄).</summary>
        static readonly Vector2[] Placeholders = { new Vector2(3, .5f), new Vector2(3, 2.5f), new Vector2(3, 4.5f), new Vector2(4, 1.5f), new Vector2(4, 3.5f) };

        readonly List<SkillNodeCanvasV5.Node> _v5Nodes = new List<SkillNodeCanvasV5.Node>();
        readonly List<SkillNodeCanvasV5.Link> _v5Links = new List<SkillNodeCanvasV5.Link>();
        Vector2 _v5CanvasScroll;
        bool _dragging;

        static Vector2 NodePos(float col, float row) => new Vector2(ContentLeft + col * ColGap, ContentTop + row * RowGap);
        static Rect NodeRect(float col, float row) { var p = NodePos(col, row); return new Rect(p.x, p.y, NodeSize, NodeSize); }
        static Vector2 ContentSize => new Vector2(ContentLeft + (Columns - 1) * ColGap + NodeSize + 110f, ContentTop + 5 * RowGap + NodeSize + 70f);

        static string IconOf(SkillNodeDef n)
        {
            switch (n.Glyph)
            {
                case 0: return "whirl";
                case 1: return "wave";
                case 2: return "wpn_longsword";
                case 3: return "arm_plate";
                default: return null;
            }
        }

        SkillNodeCanvasV5.NodeState StateOf(PlayerProgress p, SkillNodeDef n, out int rank)
        {
            rank = p.Rank(n.Id);
            if (rank >= n.MaxRank) return n.MaxRank > 1 ? SkillNodeCanvasV5.NodeState.Maxed : SkillNodeCanvasV5.NodeState.Taken;
            var check = p.Check(n.Id, _teach);
            if (check.Ok) return SkillNodeCanvasV5.NodeState.Available;
            if (check.NeedsTrainer) return SkillNodeCanvasV5.NodeState.NeedsTrainer;
            return rank > 0 ? SkillNodeCanvasV5.NodeState.Taken : SkillNodeCanvasV5.NodeState.Locked;
        }

        static string CaptionOf(SkillNodeDef n, int rank)
        {
            if (n.Kind == SkillNodeKind.Rank) return "랭크 " + rank + " / " + n.MaxRank;
            if (rank > 0) return n.Kind == SkillNodeKind.Learn ? "배움" : "고름";
            return n.Kind == SkillNodeKind.Learn ? "배우기 · 1점" : "둘 중 하나 · 1점";
        }

        void DrawApprovedSkillNodesV5()
        {
            if (DungeonUi.Modal != ModalName) return;
            var p = _progress ? _progress : PlayerProgress.Instance; if (!p) return;
            var matrix = GUI.matrix; var color = GUI.color; bool enabled = GUI.enabled;
            DungeonUi.Begin(); GUI.depth = -20;
            try
            {
                var carry = ProfileCarry.Data;
                string teachAt = SpeakerIdentity.TeachAt(carry);
                var r = new Rect((DungeonUi.Width - 1280) * .5f, 50, 1280, 800);
                DungeonUi.Fill(new Rect(0, 0, DungeonUi.Width, DungeonUi.Height), new Color(0, 0, 0, .55f));
                ApprovedUiV5.Image(r, "window-frame");
                bool knowsTeacher = carry != null && SpeakerIdentity.Knows(carry, NpcTable.Trainer);
                string title = _teach ? "기술 배우기 — " + (knowsTeacher ? NpcTable.Get(NpcTable.Trainer).DisplayName : NpcTable.Get(NpcTable.Trainer).Place) : "스킬 트리";
                ApprovedUiV5.TitleLabel(new Rect(r.x + 92, r.y + 58, 640, 44), title, 28, ApprovedUiV5.Light, TextAnchor.MiddleLeft, true);
                string sub = _teach
                    ? SpeakerIdentity.Label(carry, NpcTable.Trainer) + "  “점수만큼만 가르쳐 주지. 나머진 갱도에서 몸으로 익혀.”"
                    : "배우기·갈림 칸은 마을 " + teachAt + " 배운다 · 랭크 칸은 어디서나 K";
                ApprovedUiV5.Label(new Rect(r.x + 94, r.y + 100, 760, 26), sub, 15, ApprovedUiV5.Muted, TextAnchor.MiddleLeft);
                ApprovedUiV5.Label(new Rect(r.x + 820, r.y + 62, 330, 40), "레벨 " + p.Level + "  ·  남은 점수 " + p.SkillPoints, 20, ApprovedUiV5.Gold, TextAnchor.MiddleRight, true);
                var close = new Rect(r.x + 1182, r.y + 59, 58, 58);
                if (GUI.Button(close, new GUIContent("", "닫기 · Esc"), GUIStyle.none)) DungeonUi.Close(ModalName);
                ApprovedUiV5.Label(close, "×", 30, ApprovedUiV5.Light, TextAnchor.MiddleCenter, true);

                var view = new Rect(r.x + 70, r.y + 140, 740, 560);
                DungeonUi.Fill(view, new Color(0, 0, 0, .28f));
                var size = ContentSize;
                var e = Event.current;
                // 빈 곳 끌기(왼쪽 단추는 칸 밖에서 누른 경우만, 가운데·오른쪽 단추는 어디서나).
                if (e.type == EventType.MouseDown && view.Contains(e.mousePosition))
                {
                    var local = e.mousePosition - view.position + _v5CanvasScroll;
                    _dragging = e.button != 0 || !HitsNode(local);
                }
                if (e.type == EventType.MouseUp) _dragging = false;
                if (_dragging && e.type == EventType.MouseDrag)
                {
                    _v5CanvasScroll -= e.delta;
                    _v5CanvasScroll.x = Mathf.Clamp(_v5CanvasScroll.x, 0, Mathf.Max(0, size.x - view.width + 16));
                    _v5CanvasScroll.y = Mathf.Clamp(_v5CanvasScroll.y, 0, Mathf.Max(0, size.y - view.height + 16));
                    e.Use();
                }
                _v5CanvasScroll = GUI.BeginScrollView(view, _v5CanvasScroll, new Rect(0, 0, size.x, size.y), true, true);
                DrawRulerAndBands(size);
                BuildNodes(p);
                SkillNodeCanvasV5.Draw(_v5Nodes, _v5Links, i => { if (i < SkillTree.Nodes.Count) _selected = SkillTree.Nodes[i].Id; });
                DrawOrMarks();
                GUI.EndScrollView();

                DrawDetail(p, new Rect(r.x + 840, r.y + 140, 360, 560), teachAt);
                ApprovedUiV5.Label(new Rect(r.x + 92, r.y + 722, 1080, 28),
                    "클릭 고르기 · 빈 곳 끌어 옮기기 · 방향키 고르기 · Enter " + (_teach ? "배우기" : "강화") + " · K · Esc 닫기",
                    15, ApprovedUiV5.Gold, TextAnchor.MiddleLeft);
            }
            finally { GUI.matrix = matrix; GUI.color = color; GUI.enabled = enabled; }
        }

        static bool HitsNode(Vector2 local)
        {
            foreach (var n in SkillTree.Nodes)
                if (NodeRect(n.Col, n.Row).Contains(local)) return true;
            return false;
        }

        /// <summary>열 머리(레벨 단계)와 갈래 띠(회오리·검풍·몸).</summary>
        static void DrawRulerAndBands(Vector2 size)
        {
            for (int b = 0; b < Branches.Length; b++)
            {
                var (name, from, to) = Branches[b];
                float y0 = NodePos(0, from).y - 26f, y1 = NodePos(0, to).y + NodeSize + 54f;
                DungeonUi.Fill(new Rect(0, y0, size.x, y1 - y0), b % 2 == 0 ? new Color(1f, .9f, .7f, .03f) : new Color(0, 0, 0, .12f));
                ApprovedUiV5.Label(new Rect(18, y0, 110, y1 - y0), name, 20, ApprovedUiV5.Gold, TextAnchor.MiddleLeft, true);
            }
            for (int c = 0; c < Columns; c++)
            {
                float x = NodePos(c, 0).x + NodeSize * .5f;
                bool future = c >= 3;
                ApprovedUiV5.Label(new Rect(x - 90, 10, 180, 24), ColumnLabels[c], 14, future ? new Color(.5f, .47f, .41f, 1f) : ApprovedUiV5.Light, TextAnchor.MiddleCenter, true);
                DungeonUi.Fill(new Rect(x - 40, 36, 80, 1), future ? new Color(.4f, .37f, .32f, .6f) : ApprovedUiV5.Edge);
            }
        }

        void BuildNodes(PlayerProgress p)
        {
            _v5Nodes.Clear();
            _v5Links.Clear();
            var index = new Dictionary<SkillNodeId, int>();
            foreach (var n in SkillTree.Nodes)
            {
                var state = StateOf(p, n, out int rank);
                index[n.Id] = _v5Nodes.Count;
                _v5Nodes.Add(new SkillNodeCanvasV5.Node
                {
                    Id = n.Key, Title = n.Name, Icon = IconOf(n), Glyph = n.Glyph, Caption = CaptionOf(n, rank),
                    Bounds = NodeRect(n.Col, n.Row), Selected = n.Id == _selected, State = state,
                    Rank = rank, MaxRank = n.MaxRank,
                });
            }
            foreach (var n in SkillTree.Nodes)
            {
                if (!n.Parent.HasValue) continue;
                var child = _v5Nodes[index[n.Id]];
                var c = child.State == SkillNodeCanvasV5.NodeState.Taken || child.State == SkillNodeCanvasV5.NodeState.Maxed ? SkillNodeCanvasV5.LinkTaken
                    : child.State == SkillNodeCanvasV5.NodeState.Locked ? SkillNodeCanvasV5.LinkLocked : SkillNodeCanvasV5.LinkOpen;
                _v5Links.Add(new SkillNodeCanvasV5.Link { From = index[n.Parent.Value], To = index[n.Id], Color = c });
            }
            // 뒤쪽 빈 틀: 갈래 끝(갈림 칸 또는 끓는 피)에서 점선으로 잇는다.
            int[] roots = { index[SkillNodeId.PullWhirl], index[SkillNodeId.ThreeWave], index[SkillNodeId.BoilingBlood] };
            for (int i = 0; i < Placeholders.Length; i++)
            {
                int at = _v5Nodes.Count;
                _v5Nodes.Add(new SkillNodeCanvasV5.Node
                {
                    Id = "future." + i, Title = "더 깊은 갱도에서", Caption = "아직 열리지 않음",
                    Bounds = NodeRect(Placeholders[i].x, Placeholders[i].y), State = SkillNodeCanvasV5.NodeState.Placeholder, MaxRank = 1,
                });
                int from = i < 3 ? roots[i] : at - 3;
                _v5Links.Add(new SkillNodeCanvasV5.Link { From = from, To = at, Color = SkillNodeCanvasV5.LinkLocked, Dashed = true });
            }
        }

        /// <summary>갈림 두 칸 사이 '또는'.</summary>
        static void DrawOrMarks()
        {
            foreach (var n in SkillTree.Nodes)
            {
                if (n.Kind != SkillNodeKind.Choice || !n.Exclusive.HasValue || n.Exclusive.Value < n.Id) continue;
                var other = SkillTree.Node(n.Exclusive.Value);
                var a = NodeRect(n.Col, n.Row); var b = NodeRect(other.Col, other.Row);
                // 두 칸 가운데 높이, 꺾인 가지 세로줄 바로 왼쪽(칸 이름·설명 줄과 겹치지 않게). 세로줄 자리는 SkillNodeCanvasV5.Branch와 같은 셈.
                float y = (a.center.y + b.center.y) * .5f;
                var parent = n.Parent.HasValue ? SkillTree.Node(n.Parent.Value) : n;
                float px = NodeRect(parent.Col, parent.Row).xMax + 2f;
                float midX = Mathf.Lerp(px, a.x - 2f, .45f);
                ApprovedUiV5.Label(new Rect(midX - 58, y - 27, 52, 22), "또는", 14, ApprovedUiV5.Muted, TextAnchor.MiddleRight, true);
            }
        }

        void DrawDetail(PlayerProgress p, Rect panel, string teachAt)
        {
            bool enabled = GUI.enabled;
            var n = SkillTree.Node(_selected);
            int rank = p.Rank(n.Id);
            float x = panel.x, y = panel.y, w = panel.width;
            ApprovedUiV5.Label(new Rect(x, y, w, 40), n.Name, 26, null, TextAnchor.UpperLeft, true, true);
            string kind = n.Kind == SkillNodeKind.Learn ? "새 기술 배우기" : n.Kind == SkillNodeKind.Choice ? "갈림 — 둘 중 하나만" : "랭크 " + rank + " / " + n.MaxRank;
            ApprovedUiV5.Label(new Rect(x, y + 44, w, 26), n.Branch + " · " + kind, 17, ApprovedUiV5.Gold);
            ApprovedUiV5.Label(new Rect(x, y + 80, w, 70), n.Desc, 19, null, TextAnchor.UpperLeft, false, true);
            float ly = y + 156;
            if (n.Id == SkillNodeId.LearnWhirl) { ApprovedUiV5.Label(new Rect(x, ly, w, 26), "E · 투지 " + SpiritRules.WhirlCost + " · 재사용 " + SpiritRules.WhirlCooldown + "초", 16, ApprovedUiV5.Light); ly += 30; }
            if (n.Id == SkillNodeId.LearnWave) { ApprovedUiV5.Label(new Rect(x, ly, w, 26), "Q · 투지 " + SpiritRules.WaveCost + " · 재사용 " + SpiritRules.WaveCooldown + "초", 16, ApprovedUiV5.Light); ly += 30; }
            if (n.RankSkill.HasValue)
            {
                var def = SkillTree.Get(n.RankSkill.Value);
                ApprovedUiV5.Label(new Rect(x, ly, w, 26), "지금  " + (rank > 0 || def.BaseValue != 0 ? def.EffectText(rank) : "없음"), 17, null);
                ApprovedUiV5.Label(new Rect(x, ly + 28, w, 26), rank < n.MaxRank ? "다음  " + def.EffectText(rank + 1) : "최대 단계", 17, ApprovedUiV5.Gold, TextAnchor.UpperLeft, true);
                ly += 62;
            }
            var need = new List<string>();
            if (n.RequiredLevel > 1) need.Add("레벨 " + n.RequiredLevel);
            if (n.Parent.HasValue) need.Add(SkillTree.Node(n.Parent.Value).Name + (n.ParentRank > 1 || SkillTree.Node(n.Parent.Value).Kind == SkillNodeKind.Rank ? " " + n.ParentRank + "랭크" : ""));
            if (n.Trainer) need.Add("마을 " + teachAt + " 배움");
            if (need.Count > 0) ApprovedUiV5.Label(new Rect(x, ly, w, 46), "조건  " + string.Join(" · ", need), 15, ApprovedUiV5.Muted, TextAnchor.UpperLeft, false, true);

            var check = p.Check(n.Id, _teach);
            string reason = check.NeedsTrainer ? "마을 " + teachAt + " 배운다" : check.Reason;
            ApprovedUiV5.Label(new Rect(x, y + 330, w, 28), reason, 16, check.Ok ? new Color32(167, 193, 132, 255) : ApprovedUiV5.Gold);
            GUI.enabled = enabled && check.Ok;
            string verb = rank >= n.MaxRank ? (n.Kind == SkillNodeKind.Rank ? "최대 단계" : "배움")
                : n.Kind == SkillNodeKind.Learn ? "1점으로 배우기 · Enter" : n.Kind == SkillNodeKind.Choice ? "1점으로 고르기 · Enter" : "1점으로 강화 · Enter";
            if (ApprovedUiV5.Button(new Rect(x, y + 366, w, 48), verb, true)) TakeSelected();
            GUI.enabled = enabled;

            DungeonUi.Fill(new Rect(x, y + 440, w, 1), ApprovedUiV5.Edge);
            ApprovedUiV5.Label(new Rect(x, y + 450, w, 100),
                "투지 — 일반 공격·무기 행동·받아치기·처치로 차고, 싸움이 끝나면 빠진다. 회오리 " + SpiritRules.WhirlCost + " · 검풍 " + SpiritRules.WaveCost + ".",
                14, ApprovedUiV5.Muted, TextAnchor.UpperLeft, false, true);
        }
    }
}
