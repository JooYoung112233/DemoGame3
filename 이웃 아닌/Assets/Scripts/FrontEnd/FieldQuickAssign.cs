using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    public enum FieldQuickKind { Lead, Join, Listen, Observe }
    // One place the chosen member can take right now. Ops is the plan change, the same one the 07 window / door popup / trace
    // window make through the planner (it was checked on a copy of the plan before the target was offered).
    public sealed class FieldQuickTarget
    {
        public FieldQuickKind Kind; public int Member = -1, Site = -1, Door = -1; public string Id = "", Key = "";
        public Button Button; public ActionGlyph.Kind Glyph;
        internal Action<FieldTurnPlan> Ops;
    }

    // 맡기기 · 대원 먼저 (기획/탐험-화면정리와-행동칸-1차.md §2 · 시안 02): on the site board a press on a member card chooses that
    // member (card lifted, gold outline, the room dims a little) and every place they can take right now glows with its action
    // (돋보기 수색 · 귀 문에 귀 대기 · 눈 흔적 관찰; a place that already shows a bubble gets a gold ring around it). A press on a
    // glowing place — or dropping the card's portrait on it — assigns through the planner's own plan APIs (search: lead alone at
    // the default pace, or join the standing search as its helper; listen; observe), costs no time, and chooses the next member
    // with nothing to do. The same card again, a right press, Esc or the empty room lets go. With nobody chosen the object opens
    // its search note (FieldSearchNote, 기획/탐험-수색쪽지와-협동-1차.md) and the door its popup; pressing a bubble does what pressing
    // its target does. A search assigned here opens that object's note (the role is changed there); the note's slots take a
    // dropped portrait (lead slot = lead, helper slot = join) through LeadFor / JoinFor / Commit, the same checked targets.
    // Everything drawn here lives under this node (Main's last child but the note): nothing is added under the hotspot buttons.
    // After the plan markers (400): the bubble rings it rings and presses are this frame's.
    [DefaultExecutionOrder(420)]
    public sealed class FieldQuickAssign : MonoBehaviour, IPointerClickHandler
    {
        public ExpeditionArrivalPanel Arrival;
        [Tooltip("배정 말풍선 (말풍선 둘레 고리, 말풍선 누르기)")] public FieldPlanTargetMarkers Markers;
        [Tooltip("행동 칸 (문구 · 대원 상태)")] public FieldMemberActionSlots Slots;
        [Tooltip("수색 쪽지 (사물 옆 쪽지 · 칸에 끌어 놓기)")] public FieldSearchNote Note;
        [Tooltip("수색을 맡기면 그 사물의 쪽지를 엽니다 (역할 확인 · 변경)")] public bool OpenNoteAfterAssign = true;
        [Header("고르는 동안 방")]
        [Tooltip("방을 살짝 어둡게 하고 누름을 받는 면 (방 영역만 · 아래 판은 덮지 않음)")] public Image Veil;
        [Tooltip("어둡게 하는 색")] public Color VeilColor = new Color(0, 0, 0, .22f);
        [Header("빛나는 곳")]
        [Tooltip("빛나는 곳 표시 (FieldTargetGlow · 모자라면 첫 칸을 복제)")] public FieldTargetGlow[] Glows;
        [Tooltip("핀 꼬리 끝과 사물 표식 윗변 사이 (px)")] public float PinGap = 2;
        [Header("말풍선 누르기")]
        [Tooltip("말풍선을 누르면 그 대상 창 (수색 창 · 문 확인창 · 흔적 창)")] public bool ClickBubbles = true;
        [Tooltip("말풍선 위 누름 면 (투명 · 모자라면 첫 칸을 복제)")] public Graphic[] BubbleHits;
        [Tooltip("누름 면 크기 (말풍선 고리 대비)")] [Range(.3f, 1)] public float BubbleHitSize = .8f;
        [Header("끌기")]
        [Tooltip("끌 때 포인터를 따라가는 초상")] public RectTransform DragToken;
        public Image DragPortrait;
        [Tooltip("포인터에서 초상 중심까지 (px)")] public Vector2 DragOffset = new Vector2(0, 28);
        [Header("안내")]
        [Tooltip("고른 대원 안내 (카드 줄 오른쪽 빈자리, 없으면 고른 카드 위)")] public RectTransform Hint;
        public Text HintWho, HintWhere, HintHow, HintCancel;
        [Tooltip("안내와 카드 사이 (px)")] public float HintGap = 16;
        [Header("고른 카드")]
        [Tooltip("들어 올리는 높이 (px)")] public float Lift = 6;
        [Tooltip("들어 올리는 시간 (초)")] [Min(0)] public float LiftSeconds = .12f;
        [Header("맡기는 방식")]
        [Tooltip("새로 맡기는 수색의 속도 (0 빠름 · 1 보통 · 2 정밀)")] [Range(0, 2)] public int DefaultPace = 1;
        [Tooltip("새 수색은 혼자 (함께는 두 번째 대원을 같은 곳에 놓아서 · 속도·역할은 수색 창에서)")] public bool SoloLead = true;
        [Header("안내 문구")]
        [Tooltip("첫 줄 ({0}: 이름 + 은/는)")] public string HintWhoFormat = "{0}";
        public string HintWhereText = "어디로?";
        [TextArea(2, 3)] public string HintHowText = "빛나는 곳을\n누르세요";
        public string HintCancelText = "우클릭 · 취소";
        [TextArea(2, 3)] public string HintNoneText = "지금 맡길 곳이\n없습니다";
        [TextArea(2, 3)] public string HintMovingText = "이동 예약 중\n문을 다시 누르면 취소";
        [Header("상황판 문구")]
        [Tooltip("수색 ({0}: 대원, {1}: 사물)")] [TextArea(2, 3)] public string StatusLead = "{0} · {1} 수색 배정\n'턴 진행'으로 함께 진행합니다.";
        [Tooltip("함께 수색 ({0}: 대원, {1}: 사물, {2}: 역할)")] [TextArea(2, 3)] public string StatusJoin = "{0} · {1} {2}\n'턴 진행'으로 함께 진행합니다.";
        [Tooltip("귀 대기 ({0}: 대원, {1}: 문 너머 방)")] [TextArea(2, 3)] public string StatusListen = "{0} · {1} 문 귀 대기\n'턴 진행'마다 문 너머를 듣습니다.";
        [Tooltip("흔적 관찰 ({0}: 대원, {1}: 흔적)")] [TextArea(2, 3)] public string StatusObserve = "{0} · {1} 조사 배정\n'턴 진행'으로 다른 대원과 함께 행동합니다.";
        [Tooltip("다른 일에서 옮겼을 때 둘째 줄 ({0}: 하던 일)")] public string StatusMoved = "{0}에서 옮겼습니다.";
        public string RoleTogether = "함께 수색", RoleWatch = "망보기", RoleLight = "조명 지원";

        public int Selected { get; private set; } = -1;
        public bool Dragging { get; private set; }
        // Board on and the room free (no window, no walk, no turn resolving): choosing works only then.
        public bool Free { get; private set; }
        public IReadOnlyList<FieldQuickTarget> Targets => targets;
        public string LastStatus { get; private set; } = "";
        public FieldQuickTarget TargetFor(string key) { foreach (var t in targets) if (t.Key == key) return t; return null; }
        public FieldTargetGlow GlowFor(string key) { foreach (var g in glowPool) if (g && g.Visible && g.Key == key) return g; return null; }
        public int ShownBubbleHits { get { int n = 0; foreach (var h in hitPool) if (h && h.gameObject.activeSelf) n++; return n; } }

        readonly List<FieldQuickTarget> targets = new List<FieldQuickTarget>();
        readonly List<FieldTargetGlow> glowPool = new List<FieldTargetGlow>();
        readonly List<Graphic> hitPool = new List<Graphic>();
        readonly List<string> hitKeys = new List<string>();
        readonly Dictionary<RectTransform, (Vector2 written, float lift)> lifted = new Dictionary<RectTransform, (Vector2, float)>();
        readonly List<RectTransform> liftDone = new List<RectTransform>();
        static readonly List<RaycastResult> hits = new List<RaycastResult>();
        readonly Vector3[] corners = new Vector3[4];
        (int, int, int, int, int, FieldSiteState, int, int, int, bool) key; bool haveKey; float nextPoll;
        int seenTurns = -1, seenRoom = -1; bool popupBefore; Vector2 dragAt;

        Camera Cam { get { var c = GetComponentInParent<Canvas>(); return c ? c.rootCanvas.worldCamera : null; } }
        FieldTurnPlanner Planner => Arrival && Arrival.Threat ? Arrival.Threat.Planner : null;

        // ---- choosing ----
        public bool Select(int member)
        {
            var a = Arrival;
            if (!Free || !a || member < 0 || member >= a.Participants.Count || a.Participants[member] == null || a.Participants[member].Health <= 0) return false;
            if (Selected != member) { Selected = member; haveKey = false; }
            Rebuild(); return true;
        }
        public void Cancel()
        {
            Selected = -1; Dragging = false; targets.Clear(); haveKey = false;
            if (DragToken && DragToken.gameObject.activeSelf) DragToken.gameObject.SetActive(false);
        }
        // A press on a member card (FieldMemberCardInput).
        public void OnCard(ExpeditionMemberCard card)
        {
            int m = Arrival ? Arrival.Cards.IndexOf(card) : -1; if (m < 0) return;
            if (m == Selected) { Cancel(); return; }
            if (!Select(m)) Cancel();
        }

        // ---- presses in the room (the veil, a bubble) ----
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right) { if (Selected >= 0) Cancel(); return; }
            if (e.button != PointerEventData.InputButton.Left || !Free) return;
            if (Selected >= 0)
            {
                var t = Resolve(e.position, out var below);
                if (t != null) { Take(t); return; }
                Cancel();
                // A button that happens to sit under the veil still gets the press (not a room object: that only lets go).
                if (below && below.GetComponentInParent<Selectable>() && !below.GetComponentInParent<ExplorationHotspot>()) ExecuteEvents.ExecuteHierarchy(below, e, ExecuteEvents.pointerClickHandler);
                return;
            }
            string bubble = BubbleAt(e.position); if (bubble != null) OpenTarget(bubble);
        }
        // The target under a screen point: the open note's slot (the rest of its paper takes nothing), a glow, a bubble that is
        // a target, or the room object's own raycast under this layer and the note.
        public FieldQuickTarget Resolve(Vector2 screen, out GameObject below)
        {
            below = null; var cam = Cam;
            if (Note && Note.IsOpen && Note.Covers(screen)) return Selected >= 0 ? Note.DropTarget(screen, Selected) : null;
            foreach (var g in glowPool) if (g && g.Visible && RectTransformUtility.RectangleContainsScreenPoint(g.HitArea, screen, cam)) { var t = TargetFor(g.Key); if (t != null) return t; }
            // A bubble is its own target or nothing (never the object that happens to lie under it).
            string bubble = BubbleAt(screen); if (bubble != null) return TargetFor(bubble);
            below = Below(screen); if (!below) return null;
            foreach (var t in targets) if (t.Button && below.transform.IsChildOf(t.Button.transform)) return t;
            return null;
        }
        GameObject Below(Vector2 screen)
        {
            var es = EventSystem.current; if (!es) return null;
            hits.Clear(); es.RaycastAll(new PointerEventData(es) { position = screen }, hits);
            foreach (var h in hits) if (h.gameObject && !h.gameObject.transform.IsChildOf(transform) && !(Note && h.gameObject.transform.IsChildOf(Note.transform))) return h.gameObject;
            return null;
        }
        // The bubble under a screen point (its key), or null.
        public string BubbleKeyAt(Vector2 screen) => BubbleAt(screen);
        string BubbleAt(Vector2 screen)
        {
            if (!ClickBubbles) return null; var cam = Cam;
            for (int i = 0; i < hitPool.Count && i < hitKeys.Count; i++) { var h = hitPool[i]; if (h && h.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(h.rectTransform, screen, cam)) return hitKeys[i]; }
            return null;
        }
        // Nobody chosen: a bubble opens its target, as pressing the target itself does today.
        public bool OpenTarget(string bubbleKey)
        {
            var a = Arrival; if (!a || string.IsNullOrEmpty(bubbleKey)) return false;
            Button b = null;
            if (bubbleKey == FieldPlanTargetMarkers.MoveKey) b = a.Rooms && a.Rooms.QueuedDoor ? a.Rooms.QueuedDoor.GetComponent<Button>() : null;
            else if (bubbleKey.StartsWith("search:") && int.TryParse(bubbleKey.Substring(7), out int site)) b = SiteButton(a, site);
            else if (bubbleKey.StartsWith("listen:") && int.TryParse(bubbleKey.Substring(7), out int door)) b = DoorButton(a, a.Rooms ? a.Rooms.CurrentRoom : -1, door);
            else if (bubbleKey.StartsWith("observe:")) b = a.Story ? a.Story.Clue : null;
            if (!b || !b.gameObject.activeInHierarchy || !b.IsInteractable()) return false;
            b.onClick.Invoke(); return true;
        }

        // ---- dragging the card ----
        public bool BeginDrag(ExpeditionMemberCard card, Vector2 screen)
        {
            int m = Arrival ? Arrival.Cards.IndexOf(card) : -1;
            if (m < 0 || !Select(m)) return false;
            Dragging = true; dragAt = screen;
            if (DragToken)
            {
                if (DragPortrait && card.Portrait) DragPortrait.sprite = card.Portrait.sprite;
                DragToken.gameObject.SetActive(true); MoveToken(screen);
            }
            return true;
        }
        public void Drag(Vector2 screen) { if (!Dragging) return; dragAt = screen; MoveToken(screen); }
        public void EndDrag(Vector2 screen)
        {
            if (!Dragging) return;
            Dragging = false; if (DragToken) DragToken.gameObject.SetActive(false);
            var t = Free && Selected >= 0 ? Resolve(screen, out _) : null;
            if (t == null || !Take(t)) Cancel();
        }
        void MoveToken(Vector2 screen)
        {
            if (!DragToken || !(DragToken.parent is RectTransform parent)) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, Cam, out var local)) DragToken.localPosition = new Vector3(local.x + DragOffset.x, local.y + DragOffset.y, 0);
        }

        // ---- assigning (the planner's plan APIs only; no time) ----
        // One of the glowing targets, for the chosen member.
        public bool Assign(FieldQuickTarget t)
        {
            if (!Free || t == null || !targets.Contains(t) || t.Member != Selected) return false;
            return Commit(t);
        }
        // A glowing target, or one of the note's slot targets for the chosen member.
        bool Take(FieldQuickTarget t) => targets.Contains(t) ? Assign(t) : t.Member == Selected && Commit(t);
        // Any checked target (the glows, the note's slots and picks): applied only if a copy of the plan shows it taking.
        // The member who takes it hands the choice on to the next member with nothing to do; a search opens its object's note.
        public bool Commit(FieldQuickTarget t)
        {
            var a = Arrival; var pl = Planner;
            if (!Free || t == null || t.Ops == null || !pl || !a || t.Member < 0 || t.Member >= a.Participants.Count) return false;
            var facts = pl.Facts(); int m = t.Member;
            var before = pl.Plan.Check(facts); string was = Slots && Kept(before.Actions[m]) && !Valid(before, t) ? Slots.TaskLabel(before, m) : "";
            var draft = pl.Plan.Clone(); t.Ops(draft);
            if (!Valid(draft.Check(facts), t)) { haveKey = false; Rebuild(); return false; }
            t.Ops(pl.Plan); pl.Plan.Keep(pl.Plan.Check(facts));
            var after = pl.Plan.Check(facts);
            if (!Valid(after, t)) Debug.LogError("FieldQuickAssign: " + t.Key + " did not take for member " + m);
            string name = a.Participants[m].Name, line;
            switch (t.Kind)
            {
                case FieldQuickKind.Lead: line = string.Format(StatusLead, name, SiteName(t.Site)); break;
                case FieldQuickKind.Join:
                    { var r = after.RunFor(t.Site); var role = r == null ? RoleTogether : r.Role == FieldAction.Watch ? RoleWatch : r.Role == FieldAction.Light ? RoleLight : RoleTogether; line = string.Format(StatusJoin, name, SiteName(t.Site), role); break; }
                case FieldQuickKind.Listen: line = string.Format(StatusListen, name, RoomName(t.Door)); break;
                default: line = string.Format(StatusObserve, name, a.Story ? a.Story.ObserveLabel(t.Id) : "흔적"); break;
            }
            if (was.Length > 0) { int cut = line.IndexOf('\n'); line = (cut >= 0 ? line.Substring(0, cut) : line) + "\n" + string.Format(StatusMoved, was); }
            LastStatus = line; a.Status.text = line; a.Threat.Refresh();
            // The chosen member took it: the next member with nothing to do, after this one; nobody left → let go.
            if (Selected == m)
            {
                int n = after.Actions.Length, next = -1;
                for (int i = 1; i <= n && next < 0; i++) { int j = (m + i) % n; if (j != m && after.Actions[j] == FieldAction.Hush) next = j; }
                if (next >= 0 && Select(next)) { } else Cancel();
            }
            else if (Selected >= 0) haveKey = false;
            if (Note && OpenNoteAfterAssign && (t.Kind == FieldQuickKind.Lead || t.Kind == FieldQuickKind.Join)) Note.Open(t.Site);
            return true;
        }
        static bool Kept(FieldAction a) => a != FieldAction.Hush && a != FieldAction.Down;
        static bool Valid(FieldPlanCheck k, FieldQuickTarget t)
        {
            int m = t.Member; if (m < 0 || m >= k.Actions.Length) return false;
            switch (t.Kind)
            {
                case FieldQuickKind.Lead: { var r = k.RunFor(t.Site); return r != null && r.Lead == m; }
                case FieldQuickKind.Join: { var r = k.RunFor(t.Site); return r != null && r.Support == m; }
                case FieldQuickKind.Listen: return k.Actions[m] == FieldAction.Listen && k.DoorOf[m] == t.Door;
                default: return k.Actions[m] == FieldAction.Observe && k.ObserveOf[m] == t.Id;
            }
        }

        // ---- what the chosen member can take now (each checked on a copy of the plan) ----
        void Rebuild()
        {
            var a = Arrival; var t = a ? a.Threat : null; var pl = Planner; var rooms = a ? a.Rooms : null;
            if (!a || !pl || !rooms || Selected < 0) { targets.Clear(); return; }
            var now = KeyOf(a, t, pl, rooms);
            if (haveKey && now.Equals(key) && Time.unscaledTime < nextPoll) return;
            key = now; haveKey = true; nextPoll = Time.unscaledTime + .5f;
            targets.Clear(); int m = Selected;
            if (rooms.HasQueuedMove || m >= a.Participants.Count || a.Participants[m].Health <= 0) return;
            var plan = pl.Plan; var facts = pl.Facts(); var k = plan.Check(facts);
            // Search: every object of this room not finished (the den shelf only while it can be searched), lead or join.
            for (int site = 0; a.Loot && site < a.Loot.Sites.Length; site++)
            {
                var button = SiteButton(a, site); if (!button || !button.gameObject.activeInHierarchy || !button.IsInteractable()) continue;
                var f = facts.Site(site); if (!f.InRoom || f.Complete || !f.Searchable) continue;
                var run = k.RunFor(site);
                if (run != null && (run.Lead == m || run.Support == m)) continue;
                var target = run != null ? JoinTarget(plan, facts, site, m, run) : LeadTarget(plan, facts, site, m, f);
                if (target != null) { target.Button = button; targets.Add(target); }
            }
            // Listen: a door of this room that can be listened at and has nobody at it.
            var lf = facts as IFieldListenFacts;
            foreach (int door in Behind(rooms.CurrentRoom))
            {
                var button = DoorButton(a, rooms.CurrentRoom, door); if (!button || !button.gameObject.activeInHierarchy) continue;
                if (lf != null && lf.ListenBlock(door) != FieldPause.None || plan.ListenAt(door) != null) continue;
                int d = door; var target = Checked(plan, facts, new FieldQuickTarget { Kind = FieldQuickKind.Listen, Member = m, Door = d, Key = FieldPlanTargetMarkers.ListenKey(d), Glyph = ActionGlyph.Kind.Listen, Ops = p => p.AssignListen(m, d) });
                if (target != null) { target.Button = button; targets.Add(target); }
            }
            // Observe: the trace while it can be observed and nobody is on it.
            var story = a.Story; string id = ExpeditionNpcStory.ObservationId;
            if (story && story.Clue && story.Clue.gameObject.activeInHierarchy && story.ObserveBlock(id) == FieldPause.None && plan.ObserveAt(id) == null)
            {
                var target = Checked(plan, facts, new FieldQuickTarget { Kind = FieldQuickKind.Observe, Member = m, Id = id, Key = FieldPlanTargetMarkers.ObserveKey(id), Glyph = ActionGlyph.Kind.Observe, Ops = p => p.AssignObserve(m, id) });
                if (target != null) { target.Button = story.Clue; targets.Add(target); }
            }
        }
        (int, int, int, int, int, FieldSiteState, int, int, int, bool) KeyOf(ExpeditionArrivalPanel a, ExpeditionSiteThreat t, FieldTurnPlanner pl, ExpeditionRoomNavigation rooms)
        {
            int alive = 0; for (int i = 0; i < a.Participants.Count; i++) if (a.Participants[i] != null && a.Participants[i].Health > 0) alive |= 1 << i;
            var story = a.Story; bool clue = story && story.Clue && story.Clue.gameObject.activeInHierarchy;
            return (Selected, pl.Plan.Version, rooms.Turns, rooms.CurrentRoom, rooms.HasQueuedMove ? rooms.QueuedRoom : -1, t ? t.State : null, t && t.State != null ? t.State.TurnsUsed : 0, alive, story ? story.State.Stage : -1, clue);
        }
        FieldQuickTarget Checked(FieldTurnPlan plan, IFieldPlanFacts facts, FieldQuickTarget t)
        {
            var d = plan.Clone(); t.Ops(d); return Valid(d.Check(facts), t) ? t : null;
        }
        // Lead alone at the default pace (a standing but stalled order keeps its pace and role; a started search keeps its own).
        FieldQuickTarget LeadTarget(FieldTurnPlan plan, IFieldPlanFacts facts, int site, int m, FieldSiteFacts f)
        {
            var standing = plan.Find(site);
            var tries = new List<FieldOrder>();
            if (standing != null) tries.Add(new FieldOrder { Site = site, Lead = m, Pace = standing.Pace, Duty = standing.Duty, Solo = standing.Solo, Support = standing.Support });
            tries.Add(f.Progress > 0 ? new FieldOrder { Site = site, Lead = m, Pace = f.Pace, Duty = f.Duty } : new FieldOrder { Site = site, Lead = m, Pace = DefaultPace, Duty = 0, Solo = SoloLead });
            foreach (var o in tries)
            {
                var order = o;
                var t = Checked(plan, facts, new FieldQuickTarget { Kind = FieldQuickKind.Lead, Member = m, Site = site, Key = FieldPlanTargetMarkers.SearchKey(site), Glyph = ActionGlyph.Kind.Search, Ops = p => p.Assign(order) });
                if (t != null) return t;
            }
            return null;
        }
        // For the search note: the same checked targets for any living member (lead: the glow's lead; join: the glow's join, and
        // also in place of a helper already there — the note's helper slot swaps them). Null when the plan would not take it.
        public FieldQuickTarget LeadFor(int site, int member)
        {
            var a = Arrival; var pl = Planner; if (!a || !pl || !a.Loot || site < 0 || site >= a.Loot.Sites.Length) return null;
            var facts = pl.Facts(); if (!facts.Alive(member)) return null;
            var f = facts.Site(site); if (!f.InRoom || f.Complete || !f.Searchable) return null;
            var t = LeadTarget(pl.Plan, facts, site, member, f); if (t != null) t.Button = SiteButton(a, site); return t;
        }
        public FieldQuickTarget JoinFor(int site, int member)
        {
            var a = Arrival; var pl = Planner; if (!a || !pl) return null;
            var facts = pl.Facts(); if (!facts.Alive(member)) return null;
            var run = pl.Plan.Check(facts).RunFor(site); if (run == null) return null;
            var t = JoinTarget(pl.Plan, facts, site, member, run, true); if (t != null) t.Button = SiteButton(a, site); return t;
        }
        // Join a running search as its helper (함께 / 망보기 / 조명, whatever it needs); a search that already has one is full
        // (unless `swap`: the new member takes the helper's place).
        FieldQuickTarget JoinTarget(FieldTurnPlan plan, IFieldPlanFacts facts, int site, int m, FieldRun run, bool swap = false)
        {
            if (run.Support >= 0 && !swap || run.Support == m || run.Lead == m) return null;
            var standing = plan.Find(site); if (standing == null || standing.Lead != run.Lead) return null;
            bool fresh = run.Starts;
            var duties = new List<int> { standing.Duty }; if (fresh && standing.Duty != 0) duties.Add(0);
            foreach (int duty in duties)
            {
                int dutyNow = duty;
                Action<FieldTurnPlan> ops = p =>
                {
                    Release(p, m, site);
                    var o = p.Find(site); if (o == null) return; o = o.Copy(); o.Prefer = m;
                    if (fresh) { o.Solo = false; o.Duty = dutyNow; }
                    p.Assign(o);
                };
                var t = Checked(plan, facts, new FieldQuickTarget { Kind = FieldQuickKind.Join, Member = m, Site = site, Key = FieldPlanTargetMarkers.SearchKey(site), Ops = ops });
                if (t == null) continue;
                var r = Draft(plan, facts, ops).RunFor(site);
                t.Glyph = r != null && r.Role == FieldAction.Watch ? ActionGlyph.Kind.Watch : r != null && r.Role == FieldAction.Light ? ActionGlyph.Kind.Light : ActionGlyph.Kind.Search;
                return t;
            }
            return null;
        }
        static FieldPlanCheck Draft(FieldTurnPlan plan, IFieldPlanFacts facts, Action<FieldTurnPlan> ops) { var d = plan.Clone(); ops(d); return d.Check(facts); }
        // A helper first leaves what they did (a lead's own search goes with them; a door; a trace). Planner APIs only.
        static void Release(FieldTurnPlan p, int m, int site)
        {
            var led = p.SiteLedBy(m); if (led != null && led.Site != site) p.Release(led.Site);
            var heard = p.ListenBy(m); if (heard != null) p.ReleaseListen(heard.Door);
            var watched = p.ObserveBy(m); if (watched != null) p.ReleaseObserve(watched.Id);
        }

        // ---- rooms, doors, objects ----
        // The button that stands for a search site (the den shelf: the office door).
        public static Button SiteButtonOf(ExpeditionArrivalPanel a, int site) => a ? SiteButton(a, site) : null;
        static Button SiteButton(ExpeditionArrivalPanel a, int site)
        {
            var t = a.Threat; if (t && site == t.DenSite) return a.Rooms ? a.Rooms.OfficeDoor : null;
            if (a.Objects == null || site < 0 || site >= a.Objects.Length) return null;
            var b = a.Objects[site]; return b == DoorButton(a, FieldSiteState.Arcade, FieldSiteState.Corridor) ? null : b;
        }
        static IEnumerable<int> Behind(int room)
        {
            if (room == FieldSiteState.Arcade) yield return FieldSiteState.Corridor;
            else if (room == FieldSiteState.Corridor) { yield return FieldSiteState.Arcade; yield return FieldSiteState.Storage; yield return FieldSiteState.Den; }
            else if (room == FieldSiteState.Storage) yield return FieldSiteState.Corridor;
        }
        // The door of `room` that opens onto `behind` (the same doors as the room navigation and the markers).
        static Button DoorButton(ExpeditionArrivalPanel a, int room, int behind)
        {
            var r = a.Rooms; if (!r) return null;
            if (room == FieldSiteState.Arcade && behind == FieldSiteState.Corridor) return a.Objects != null && a.Objects.Length > 3 ? a.Objects[3] : null;
            if (room == FieldSiteState.Corridor) return behind == FieldSiteState.Arcade ? r.CorridorBack : behind == FieldSiteState.Storage ? r.LockedDoor : behind == FieldSiteState.Den ? r.OfficeDoor : null;
            return room == FieldSiteState.Storage && behind == FieldSiteState.Corridor ? r.StorageBack : null;
        }
        string SiteName(int site) => Arrival && Arrival.ObjectNames != null && site >= 0 && site < Arrival.ObjectNames.Length ? Arrival.ObjectNames[site] : "";
        static string RoomName(int room) => room >= 0 && room < FieldSiteState.RoomNames.Length ? FieldSiteState.RoomNames[room] : "";
        // 은 / 는 after the last syllable.
        public static string Topic(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            char c = name[name.Length - 1];
            return name + (c < 0xAC00 || c > 0xD7A3 ? "은(는)" : (c - 0xAC00) % 28 != 0 ? "은" : "는");
        }

        // ---- every frame ----
        // SettlementScreen adds room hotspots (corridor, storage) to Main after the prefab's own children: keep the bubbles, the turn
        // layer and this one above them, or a chosen member's press on those objects would open their window instead of assigning.
        // The search note stays over this layer (its slots take presses and drops while a member is chosen).
        void Start()
        {
            var main = transform.parent; if (!main) return;
            if (Markers && Markers.transform.parent == main) Markers.transform.SetAsLastSibling();
            var flow = main.Find("TurnFlow"); if (flow) flow.SetAsLastSibling();
            transform.SetAsLastSibling();
            if (Note && Note.transform.parent == main) Note.transform.SetAsLastSibling();
        }
        void Update()
        {
            // Esc lets go of the chosen member. The arrival panel reads the same key first and asks about going home:
            // that question was not meant, so it is put away in the same frame (before anything draws or reacts to it).
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame || Selected < 0 && !Dragging || !Arrival) return;
            bool asked = Arrival.Popup && Arrival.Popup.activeSelf && !popupBefore;
            Cancel(); if (asked) Arrival.ClosePopup();
        }
        void LateUpdate()
        {
            var a = Arrival; var t = a ? a.Threat : null; var pl = Planner; var rooms = a ? a.Rooms : null;
            bool board = a && a.IsOpen && pl && pl.Active && t.State != null && rooms;
            bool encounter = a && a.Encounter && (a.Encounter.IsOpen || a.Encounter.Battle && a.Encounter.Battle.IsOpen);
            Free = board && !encounter && !a.InTransit && t.CanAct && !pl.Resolving && a.Main && a.Main.interactable && a.Main.blocksRaycasts;
            if (board && (rooms.Turns != seenTurns || rooms.CurrentRoom != seenRoom)) { if (Selected >= 0 && seenTurns >= 0) Cancel(); seenTurns = rooms.Turns; seenRoom = rooms.CurrentRoom; }
            if (!Free && (Selected >= 0 || Dragging)) Cancel();
            if (Selected >= 0 && (Selected >= a.Participants.Count || a.Participants[Selected].Health <= 0)) Cancel();
            if (Selected >= 0) Rebuild();

            // Card inputs: on for the live cards on the board (the first visit keeps the card button: the bag).
            if (a)
                for (int i = 0; i < a.Cards.Count; i++)
                {
                    var card = a.Cards[i]; if (!card) continue;
                    var slot = Slots ? Slots.SlotOf(card) : card.GetComponentInChildren<FieldMemberActionSlot>(true); if (!slot) continue;
                    if (slot.Input) slot.Input.SetHit(board && !encounter);
                    slot.SetSelected(i == Selected);
                    LiftCard((RectTransform)card.transform, i == Selected ? Lift : 0);
                }
            SettleLifts();

            bool choosing = Selected >= 0;
            if (Veil)
            {
                if (Veil.gameObject.activeSelf != choosing) Veil.gameObject.SetActive(choosing);
                if (choosing && Veil.color != VeilColor) Veil.color = VeilColor;
            }
            PlaceGlows(choosing);
            PlaceBubbleHits(Free && ClickBubbles && !Dragging && Markers && Markers.Free);
            PlaceHint(choosing);
            if (Dragging) MoveToken(dragAt); else if (DragToken && DragToken.gameObject.activeSelf) DragToken.gameObject.SetActive(false);
            popupBefore = a && a.Popup && a.Popup.activeSelf;
        }

        void PlaceGlows(bool choosing)
        {
            if (glowPool.Count == 0 && Glows != null) foreach (var g in Glows) if (g) glowPool.Add(g);
            int count = choosing ? targets.Count : 0;
            while (count > glowPool.Count && glowPool.Count > 0 && glowPool[0])
            {
                var copy = Instantiate(glowPool[0].gameObject, glowPool[0].transform.parent); copy.name = "Glow_" + glowPool.Count; copy.SetActive(false); glowPool.Add(copy.GetComponent<FieldTargetGlow>());
            }
            Vector2 pointer = Dragging ? dragAt : Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-1e5f, -1e5f); var cam = Cam;
            for (int i = 0; i < glowPool.Count; i++)
            {
                var g = glowPool[i]; if (!g) continue;
                if (i >= count) { g.Hide(); continue; }
                var t = targets[i]; var space = (RectTransform)g.transform.parent;
                if (Markers && Markers.TryGet(t.Key, out var shown) && shown.Bubble && shown.Bubble.Visible) g.ShowAround(t.Key, t.Glyph, Convert(Markers.transform, shown.Ring, space));
                else
                {
                    var anchor = MarkerOf(t.Button); if (!anchor) { g.Hide(); continue; }
                    var r = RectIn(anchor, space); g.ShowPin(t.Key, t.Glyph, new Vector2(r.center.x, r.yMax + PinGap));
                }
                g.Hover = RectTransformUtility.RectangleContainsScreenPoint(g.HitArea, pointer, cam);
            }
        }
        void PlaceBubbleHits(bool on)
        {
            if (hitPool.Count == 0 && BubbleHits != null) foreach (var h in BubbleHits) if (h) hitPool.Add(h);
            hitKeys.Clear(); int n = 0;
            if (on && Markers)
                foreach (var s in Markers.Placed)
                {
                    if (!s.Bubble || !s.Bubble.Visible) continue;
                    while (n >= hitPool.Count && hitPool.Count > 0 && hitPool[0])
                    {
                        var copy = Instantiate(hitPool[0].gameObject, hitPool[0].transform.parent); copy.name = "Hit_" + hitPool.Count; copy.SetActive(false); hitPool.Add(copy.GetComponent<Graphic>());
                    }
                    if (n >= hitPool.Count) break;
                    var h = hitPool[n]; var space = (RectTransform)h.transform.parent; var ring = Convert(Markers.transform, s.Ring, space);
                    float size = Mathf.Max(ring.width, ring.height) * BubbleHitSize;
                    if (!h.gameObject.activeSelf) h.gameObject.SetActive(true);
                    // Follows the bubble: centred on its ring (centre pivot from the builder), sized to it.
                    var hr = h.rectTransform; var at = new Vector3(ring.center.x, ring.center.y, 0); if (hr.localPosition != at) hr.localPosition = at;
                    var dim = new Vector2(size, size); if (hr.sizeDelta != dim) hr.sizeDelta = dim;
                    hitKeys.Add(s.Key); n++;
                }
            for (int i = n; i < hitPool.Count; i++) if (hitPool[i] && hitPool[i].gameObject.activeSelf) hitPool[i].gameObject.SetActive(false);
        }
        // Beside the last card if the member row has room there (시안 02), else just above the chosen card.
        void PlaceHint(bool choosing)
        {
            if (!Hint) return;
            if (Hint.gameObject.activeSelf != choosing) Hint.gameObject.SetActive(choosing);
            if (!choosing) return;
            var a = Arrival; var member = Selected < a.Cards.Count ? a.Cards[Selected] : null; if (!member) return;
            string name = a.Participants[Selected].Name;
            if (HintWho) Set(HintWho, string.Format(HintWhoFormat, Topic(name)));
            if (HintWhere) Set(HintWhere, HintWhereText);
            if (HintHow) Set(HintHow, a.Rooms && a.Rooms.HasQueuedMove ? HintMovingText : targets.Count > 0 ? HintHowText : HintNoneText);
            if (HintCancel) Set(HintCancel, HintCancelText);
            var space = (RectTransform)Hint.parent; var size = Hint.rect.size; var pivot = Hint.pivot;
            var row = a.MemberContent ? RectIn((RectTransform)a.MemberContent.parent, space) : default;
            RectTransform last = null; foreach (var c in a.Cards) if (c && c.gameObject.activeInHierarchy) last = (RectTransform)c.transform;
            Vector2 center;
            var lastRect = last ? RectIn(last, space) : default;
            if (last && row.width > 0 && lastRect.xMax + HintGap + size.x <= row.xMax) center = new Vector2(lastRect.xMax + HintGap + size.x * .5f, lastRect.center.y);
            else { var card = RectIn((RectTransform)member.transform, space); center = new Vector2(card.center.x, card.yMax + HintGap + size.y * .5f); }
            var bounds = space.rect;
            center.x = Mathf.Clamp(center.x, bounds.xMin + size.x * .5f, bounds.xMax - size.x * .5f);
            center.y = Mathf.Clamp(center.y, bounds.yMin + size.y * .5f, bounds.yMax - size.y * .5f);
            var at = new Vector3(center.x + (pivot.x - .5f) * size.x, center.y + (pivot.y - .5f) * size.y, 0);
            if (Hint.localPosition != at) Hint.localPosition = at;
        }
        static void Set(Text t, string s) { if (t.text != s) t.text = s; }

        // The chosen card rises a little; a layout pass that puts it back is followed on the next frame.
        void LiftCard(RectTransform r, float want)
        {
            if (!r) return;
            lifted.TryGetValue(r, out var s); if (s.lift == 0 && want == 0) return;
            var pos = r.anchoredPosition;
            var home = s.lift != 0 && (pos - s.written).sqrMagnitude < .01f ? pos - new Vector2(0, s.lift) : pos;
            float step = LiftSeconds <= 0 ? float.MaxValue : Mathf.Abs(Lift) * Time.unscaledDeltaTime / LiftSeconds;
            float lift = Mathf.MoveTowards(s.lift, want, step);
            var next = home + new Vector2(0, lift); if (next != pos) r.anchoredPosition = next;
            if (Mathf.Approximately(lift, 0) && want == 0) liftDone.Add(r); else lifted[r] = (next, lift);
        }
        void SettleLifts()
        {
            foreach (var kv in lifted) if (!kv.Key || !Arrival || !Arrival.Cards.Exists(c => c && c.transform == kv.Key)) liftDone.Add(kv.Key);
            foreach (var r in liftDone) { if (r && lifted.TryGetValue(r, out var s) && s.lift != 0 && (r.anchoredPosition - s.written).sqrMagnitude < .01f) r.anchoredPosition -= new Vector2(0, s.lift); lifted.Remove(r); }
            liftDone.Clear();
        }
        void OnDisable()
        {
            Cancel();
            foreach (var kv in lifted) if (kv.Key && (kv.Key.anchoredPosition - kv.Value.written).sqrMagnitude < .01f) kv.Key.anchoredPosition -= new Vector2(0, kv.Value.lift);
            lifted.Clear(); Free = false;
            if (Veil) Veil.gameObject.SetActive(false); if (Hint) Hint.gameObject.SetActive(false);
            foreach (var g in glowPool) if (g) g.Hide(); foreach (var h in hitPool) if (h) h.gameObject.SetActive(false);
        }

        // ---- geometry ----
        static RectTransform MarkerOf(Component button)
        {
            if (!button || !button.gameObject.activeInHierarchy) return null;
            var marker = button.transform.Find("ExplorationMarkerPaper") as RectTransform;
            return marker && marker.gameObject.activeInHierarchy ? marker : button.transform as RectTransform;
        }
        Rect RectIn(RectTransform r, RectTransform space)
        {
            r.GetWorldCorners(corners); var a = space.InverseTransformPoint(corners[0]); var b = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }
        static Rect Convert(Transform from, Rect r, RectTransform to)
        {
            var a = to.InverseTransformPoint(from.TransformPoint(new Vector3(r.xMin, r.yMin, 0))); var b = to.InverseTransformPoint(from.TransformPoint(new Vector3(r.xMax, r.yMax, 0)));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }
    }
}
