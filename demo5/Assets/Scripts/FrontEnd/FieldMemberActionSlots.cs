using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Per-member action slots on the site board (기획/탐험-화면정리와-행동칸-1차.md §3 · 시안 01~03), from the planner's own check
    // (the same Plan.Check the preview and the turn use): an assigned lead / support / listen / observe is '<할 일> · 행동 완료',
    // an unassigned living member '행동 남음' (they would hush) with a gold ring at their pawn's feet, a downed member '행동 없음',
    // a paused assignment '멈춤 · <까닭>'. A reserved move takes everyone ('<방>로 이동'). '턴 진행' turns green when nobody is
    // left (or a move is reserved), ochre with 'N명 행동 남음' otherwise — written after the planner and the move view write
    // the subtitle. Both visits (FieldTurnPlanner.Placing). The old tag/role lines that say the same thing
    // are only made invisible (their objects and texts stay: other code and the verify scripts read them).
    // 말 놓기 (FieldPawnBoard · 기획/탐험-말놓기-조작-재설계.md): the held pawn wears the strong ring (placed or not), and a press on an
    // object with nothing held makes the free members' rings pulse (PawnBoard.Flashing). Presentation only: never changes the plan.
    [DefaultExecutionOrder(350)]
    public sealed class FieldMemberActionSlots : MonoBehaviour
    {
        public ExpeditionArrivalPanel Arrival;
        [Tooltip("대원 말 놓기 판 (든 말의 발밑 고리를 진하게 · 말 없이 사물을 누르면 고리 반짝임)")] public FieldPawnBoard PawnBoard;
        // Retired (the old FieldQuickAssign); kept only so older builders and checks that wire it still compile. Not read.
        [HideInInspector] public MonoBehaviour Quick;
        [Header("행동 칸 문구")]
        [Tooltip("수색 담당 ({0}: 사물)")] public string SearchLead = "{0} 수색";
        [Tooltip("함께 수색 ({0}: 사물)")] public string SearchTogether = "{0} 협동";
        [Tooltip("망보기 ({0}: 사물)")] public string SearchWatch = "{0} 망보기";
        [Tooltip("조명 지원 ({0}: 사물)")] public string SearchLight = "{0} 조명";
        [Tooltip("문에 귀 대기 ({0}: 문 너머 방)")] public string Listen = "{0} 문 귀 대기";
        [Tooltip("흔적 관찰 ({0}: 흔적)")] public string Observe = "{0} 관찰";
        [Tooltip("이동 예약 ({0}: 갈 방)")] public string Move = "{0}로 이동";
        [Tooltip("문에 모여 다른 대원을 기다림 ({0}: 갈 방)")] public string Gather = "{0}로 이동 대기";
        [Tooltip("가방 물건 쓰기 · 다음 턴 ({0}: 물건)")] public string UseItem = "{0} 사용";
        [Tooltip("맡긴 대원 둘째 줄")] public string Done = "행동 완료";
        [Tooltip("안 맡긴 대원 첫 줄")] public string FreeTitle = "행동 남음";
        [Tooltip("안 맡긴 대원 둘째 줄")] public string FreeDetail = "말을 누르고 사물을 누르세요";
        [Tooltip("쓰러진 대원 첫 줄")] public string DownTitle = "쓰러짐 · 행동 없음";
        [Tooltip("쓰러진 대원 둘째 줄 (비우면 한 줄)")] public string DownDetail = "";
        [Tooltip("멈춘 배정 첫 줄 ({0}: 까닭)")] public string PausedTitle = "멈춤 · {0}";
        [Tooltip("멈춘 배정 둘째 줄")] public string PausedDetail = "이번 턴 숨죽임";
        [Header("겹치는 옛 표시 (보이지만 않게 · 글은 그대로)")]
        [Tooltip("카드 오른쪽 위 행동 이름표")] public bool HideOldTag = true;
        [Tooltip("카드 역할 줄 (예: '물자 상자 0/3')")] public bool HideOldRole = true;
        [Header("발밑 고리 · 행동 남은 대원")]
        public bool PawnGlow = true;
        [Tooltip("고리 색 (알파 = 가장 진할 때)")] public Color PawnGlowColor = new Color(1f, .8f, .3f, .85f);
        [Tooltip("고른 대원의 고리 색")] public Color PawnGlowChosen = new Color(1f, .88f, .42f, 1f);
        [Tooltip("고리 폭 (받침대 폭의 배수)")] [Range(1, 2.5f)] public float PawnGlowSize = 1.4f;
        [Tooltip("맥동 속도")] [Min(0)] public float PawnGlowSpeed = 3f;
        [Tooltip("맥동 때 옅어지는 정도")] [Range(0, 1)] public float PawnGlowDepth = .4f;
        [Header("'턴 진행' 버튼")]
        public bool TintTurn = true;
        [Tooltip("모두 맡겼거나 이동을 예약했을 때")] public Color ReadyTint = new Color(.62f, .8f, .5f, 1);
        [Tooltip("행동 남은 대원이 있을 때")] public Color PendingTint = new Color(.93f, .77f, .4f, 1);
        [Tooltip("부제 · 행동 남은 대원이 있을 때 ({0}: 인원)")] public string SubtitlePending = "{0}명 행동 남음";
        [Tooltip("부제 · 모두 맡겼을 때 ({0}: 한 턴의 분)")] public string SubtitleReady = "{0}분 흐름";
        [Tooltip("다시 읽는 최소 간격 (초 · 가방 도구·체력 변화 대비)")] [Min(.05f)] public float PollSeconds = .5f;

        public bool Board { get; private set; }
        // Living members with nothing to do this turn (the same count as the idle-member question).
        public int FreeCount { get; private set; }
        // What this view wrote into the '턴 진행' subtitle ("" while the planner's or the move view's own text shows).
        public string ComposedSubtitle { get; private set; } = "";
        public FieldPlanCheck LastCheck => check;

        readonly Dictionary<ExpeditionMemberCard, FieldMemberActionSlot> slots = new Dictionary<ExpeditionMemberCard, FieldMemberActionSlot>();
        readonly HashSet<Graphic> faded = new HashSet<Graphic>();
        FieldPlanCheck check; (int, int, int, int, bool, FieldSiteState, int, int, int) key; bool haveKey; float nextPoll;
        Image turnPaper; Color turnHome; bool turnRead;

        public FieldMemberActionSlot SlotOf(ExpeditionMemberCard card)
        {
            if (!card) return null;
            if (!slots.TryGetValue(card, out var s) || !s) { s = card.GetComponentInChildren<FieldMemberActionSlot>(true); slots[card] = s; }
            return s;
        }
        public FieldMemberActionSlot SlotOf(int member) => Arrival && member >= 0 && member < Arrival.Cards.Count ? SlotOf(Arrival.Cards[member]) : null;

        // Verify scripts: the '턴 진행' subtitle is the planner's own text, or this view's line when it writes one.
        public static bool SubtitleMatches(FieldTurnPlanner planner, string plannerText)
        {
            if (!planner || !planner.TurnSubtitle) return false;
            var a = planner.GetComponent<ExpeditionArrivalPanel>(); var v = a && a.Main ? a.Main.GetComponentInChildren<FieldMemberActionSlots>(true) : null;
            string want = v && v.isActiveAndEnabled && !string.IsNullOrEmpty(v.ComposedSubtitle) ? v.ComposedSubtitle : plannerText;
            return planner.TurnSubtitle.text == want;
        }

        // The job a member has in this check ('물자 상자 수색', '복도 문 귀 대기', …); "" when none.
        public string TaskLabel(FieldPlanCheck k, int m)
        {
            if (k == null || m < 0 || m >= k.Actions.Length) return "";
            switch (k.Actions[m])
            {
                case FieldAction.Lead: return string.Format(SearchLead, SiteName(k.SiteOf[m]));
                case FieldAction.Together: return string.Format(SearchTogether, SiteName(k.SiteOf[m]));
                case FieldAction.Watch: return string.Format(SearchWatch, SiteName(k.SiteOf[m]));
                case FieldAction.Light: return string.Format(SearchLight, SiteName(k.SiteOf[m]));
                case FieldAction.Listen: return string.Format(Listen, RoomName(k.DoorOf[m]));
                case FieldAction.Observe: return string.Format(Observe, ObserveName(k.ObserveOf[m]));
                case FieldAction.Gather: return string.Format(Gather, RoomName(k.GatherOf[m]));
                case FieldAction.Use: return string.Format(UseItem, ItemName(k.UseOf[m]));
                case FieldAction.Paused:
                    return !string.IsNullOrEmpty(k.ObserveOf[m]) ? string.Format(Observe, ObserveName(k.ObserveOf[m]))
                        : k.DoorOf[m] >= 0 ? string.Format(Listen, RoomName(k.DoorOf[m])) : k.GatherOf[m] >= 0 ? string.Format(Gather, RoomName(k.GatherOf[m]))
                        : string.Format(SearchLead, SiteName(k.SiteOf[m]));
                default: return "";
            }
        }
        public static ActionGlyph.Kind GlyphOf(FieldPlanCheck k, int m)
        {
            if (k == null || m < 0 || m >= k.Actions.Length) return ActionGlyph.Kind.None;
            switch (k.Actions[m])
            {
                case FieldAction.Lead: case FieldAction.Together: return ActionGlyph.Kind.Search;
                case FieldAction.Watch: return ActionGlyph.Kind.Watch;
                case FieldAction.Light: return ActionGlyph.Kind.Light;
                case FieldAction.Listen: return ActionGlyph.Kind.Listen;
                case FieldAction.Observe: return ActionGlyph.Kind.Observe;
                case FieldAction.Gather: return ActionGlyph.Kind.Move;
                case FieldAction.Paused: return !string.IsNullOrEmpty(k.ObserveOf[m]) ? ActionGlyph.Kind.Observe : k.DoorOf[m] >= 0 ? ActionGlyph.Kind.Listen : k.GatherOf[m] >= 0 ? ActionGlyph.Kind.Move : ActionGlyph.Kind.Search;
                default: return ActionGlyph.Kind.None;
            }
        }
        string SiteName(int site) => Arrival && Arrival.ObjectNames != null && site >= 0 && site < Arrival.ObjectNames.Length ? Arrival.ObjectNames[site] : "";
        static string RoomName(int room) => room >= 0 && room < FieldSiteState.RoomNames.Length ? FieldSiteState.RoomNames[room] : "";
        string ObserveName(string id) { var s = Arrival ? Arrival.Story : null; return s ? s.ObserveLabel(id) : "흔적"; }
        string ItemName(string id) { var inv = Arrival ? Arrival.Inventory : null; return inv != null ? System.Array.Find(inv.Items, i => i.Id == id)?.Name ?? id : id; }

        void LateUpdate()
        {
            var a = Arrival; var t = a ? a.Threat : null; var pl = t ? t.Planner : null; var rooms = a ? a.Rooms : null;
            bool board = a && a.IsOpen && pl && pl.Placing && t.State != null && rooms;
            if (!board) { if (Board) Restore(); Board = false; check = null; haveKey = false; FreeCount = 0; ComposedSubtitle = ""; return; }
            Board = true;
            bool encounter = a.Encounter && (a.Encounter.IsOpen || a.Encounter.Battle && a.Encounter.Battle.IsOpen);
            bool queued = rooms.HasQueuedMove;
            var now = Key(a, t, pl, rooms);
            if (check == null || !haveKey || !now.Equals(key) || Time.unscaledTime >= nextPoll) { key = now; haveKey = true; nextPoll = Time.unscaledTime + PollSeconds; check = pl.Plan.Check(pl.Facts()); }
            FreeCount = queued ? 0 : FieldIdleConfirm.IdleMembers(pl).Count;
            int chosen = PawnBoard ? PawnBoard.Held : -1; bool flash = PawnBoard && PawnBoard.Flashing;

            for (int i = 0; i < a.Cards.Count; i++)
            {
                var card = a.Cards[i]; if (!card) continue;
                var slot = SlotOf(card);
                Fade(card.Action, HideOldTag); Fade(card.Role, HideOldRole);
                if (!slot) continue;
                if (encounter) { slot.Hide(); continue; }
                bool alive = i < a.Participants.Count && a.Participants[i] != null && a.Participants[i].Health > 0;
                if (!alive || i >= check.Actions.Length || check.Actions[i] == FieldAction.Down) slot.Show(FieldMemberActionSlot.State.Down, ActionGlyph.Kind.None, DownTitle, DownDetail);
                else if (queued) slot.Show(FieldMemberActionSlot.State.Used, ActionGlyph.Kind.Move, string.Format(Move, RoomName(rooms.QueuedRoom)), Done);
                else if (check.Actions[i] == FieldAction.Hush) slot.Show(FieldMemberActionSlot.State.Free, ActionGlyph.Kind.None, FreeTitle, FreeDetail);
                else if (check.Actions[i] == FieldAction.Paused) slot.Show(FieldMemberActionSlot.State.Paused, GlyphOf(check, i), string.Format(PausedTitle, PauseReason(pl, check, i)), PausedDetail);
                else slot.Show(FieldMemberActionSlot.State.Used, GlyphOf(check, i), TaskLabel(check, i), Done);
            }

            // Free members: a ring at their pawn's feet (the pawns stand in member order); the held pawn: the strong ring, placed or not.
            for (int i = 0; i < a.PartyPawns.Count; i++)
            {
                var pawn = a.PartyPawns[i]; if (!pawn) continue;
                bool alive = i < check.Actions.Length && i < a.Participants.Count && a.Participants[i].Health > 0, free = alive && !queued && check.Actions[i] == FieldAction.Hush;
                bool on = PawnGlow && !encounter && !a.InTransit && alive && (free || i == chosen);
                var glow = pawn.GetComponent<FieldPawnGlow>();
                if (!glow) { if (!on) continue; glow = pawn.AddComponent<FieldPawnGlow>(); }
                bool strong = on && (i == chosen || flash && free);
                glow.Set(on, strong ? PawnGlowChosen : PawnGlowColor, PawnGlowSize, PawnGlowSpeed, PawnGlowDepth, strong);
            }

            // '턴 진행': green when nobody is left or a move is reserved (the move view keeps its subtitle), ochre with the count otherwise.
            ComposedSubtitle = "";
            if (!TintTurn || !pl.TurnButton) return;
            if (!turnRead) { turnPaper = pl.TurnButton.GetComponent<Image>(); if (turnPaper) turnHome = turnPaper.color; turnRead = true; }
            bool ready = queued || FreeCount == 0;
            if (turnPaper) { var c = ready ? ReadyTint : PendingTint; if (turnPaper.color != c) turnPaper.color = c; }
            if (queued || !pl.TurnSubtitle) return;
            ComposedSubtitle = FreeCount > 0 ? string.Format(SubtitlePending, FreeCount) : string.Format(SubtitleReady, rooms.MinutesPerTurn);
            if (pl.TurnSubtitle.text != ComposedSubtitle) pl.TurnSubtitle.text = ComposedSubtitle;
        }
        (int, int, int, int, bool, FieldSiteState, int, int, int) Key(ExpeditionArrivalPanel a, ExpeditionSiteThreat t, FieldTurnPlanner pl, ExpeditionRoomNavigation rooms)
        {
            int alive = 0; for (int i = 0; i < a.Participants.Count; i++) if (a.Participants[i] != null && a.Participants[i].Health > 0) alive |= 1 << i;
            var story = a.Story; int stage = story ? story.State.Stage : -1;
            return (pl.Plan.Version, rooms.Turns, rooms.CurrentRoom, rooms.HasQueuedMove ? rooms.QueuedRoom : -1, a.InTransit, t.State, t.State.TurnsUsed, alive, stage);
        }
        static string PauseReason(FieldTurnPlanner pl, FieldPlanCheck k, int m)
        {
            if (!string.IsNullOrEmpty(k.ObserveOf[m])) return pl.PauseShort(-1, k.ObservePauseFor(m));
            if (k.DoorOf[m] >= 0) return pl.PauseShort(-1, k.ListenPauseFor(m));
            if (k.GatherOf[m] >= 0) return pl.PauseShort(-1, k.GatherPauseFor(m));
            int site = k.SiteOf[m]; return pl.PauseShort(site, k.PauseFor(site));
        }
        // Invisible, not inactive: the object, its text and its layout stay (canvas renderer alpha only).
        void Fade(Graphic g, bool hide)
        {
            if (!g) return; float want = hide ? 0 : 1;
            if (!Mathf.Approximately(g.canvasRenderer.GetAlpha(), want)) g.canvasRenderer.SetAlpha(want);
            if (hide) faded.Add(g); else faded.Remove(g);
        }
        void Restore()
        {
            foreach (var g in faded) if (g) g.canvasRenderer.SetAlpha(1);
            faded.Clear();
            if (Arrival)
            {
                foreach (var card in Arrival.Cards) { var s = SlotOf(card); if (s) s.Hide(); }
                foreach (var pawn in Arrival.PartyPawns) { var glow = pawn ? pawn.GetComponent<FieldPawnGlow>() : null; if (glow) glow.Set(false, PawnGlowColor, PawnGlowSize, PawnGlowSpeed, PawnGlowDepth, false); }
            }
            if (turnPaper && turnRead) turnPaper.color = turnHome;
        }
        void OnDisable() { if (Board) Restore(); Board = false; haveKey = false; }
    }
}
