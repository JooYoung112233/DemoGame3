using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Where a member pawn can stand (기획/탐험-말놓기-조작-재설계.md): lead or join a search, listen at a door, gather at a door
    // (everyone living at one door = the move next turn), observe a trace.
    public enum FieldSpotKind { Lead, Join, Listen, Gather, Observe }

    // One standing spot: Room + Key ("search:N", "door:R" = the room behind the door, "observe:ID") + Slot (0 lead / listener /
    // first gatherer, 1 helper / next gatherers; doors up to FieldPlacement.DoorSlots). The world position is
    // ExplorationRoomPresentation.TrySpot(Room, Key, Slot).
    public readonly struct FieldSpotRef : IEquatable<FieldSpotRef>
    {
        public readonly int Room; public readonly string Key; public readonly int Slot;
        public FieldSpotRef(int room, string key, int slot) { Room = room; Key = key ?? ""; Slot = slot; }
        public bool IsValid => !string.IsNullOrEmpty(Key);
        public bool Equals(FieldSpotRef o) => Room == o.Room && Slot == o.Slot && (Key ?? "") == (o.Key ?? "");
        public override bool Equals(object o) => o is FieldSpotRef r && Equals(r);
        public override int GetHashCode() => (Room, Key ?? "", Slot).GetHashCode();
        public override string ToString() => Room + "/" + Key + "#" + Slot;
        public static string SearchKey(int site) => "search:" + site;
        public static string DoorKey(int behind) => "door:" + behind;
        public static string ObserveKey(string id) => "observe:" + id;
    }

    // One place the member can be put now (a silhouette + its pin). A disabled one is only a grey pin with its reason (Blocked).
    // Anchor: the hotspot the pin sits on (the object, the door, the trace). Door: the room behind the door. Built by
    // FieldPlacement.OptionsFor on a copy of the plan; FieldPlacement.Place applies it.
    public sealed class FieldPlaceOption
    {
        public FieldSpotKind Kind; public int Member = -1, Site = -1, Door = -1; public string Id = "";
        public FieldSpotRef Spot; public ActionGlyph.Kind Glyph; public string Label = ""; public bool Enabled = true; public string Blocked = ""; public Button Anchor;
        public string Key => Spot.Key ?? "";
        // A lead on a started light search that waits (paused: 손전등 동료 필요) until a member with a flashlight joins it.
        public bool Dark;
        internal Action<FieldTurnPlan> Ops;
    }
    // A chip under the helper pawn (함께 · 망보기 · 조명), or under a lone member at a door (이동 · 귀 대기). Why: the reason it is off.
    public sealed class FieldRoleChip { public string Label = "", Why = ""; public bool On, Enabled; }

    // Every string of placing pawns (Inspector: ExpeditionArrivalPanel.prefab → FieldTurnPlanner → PlaceTexts).
    [Serializable] public sealed class FieldPlacementTexts
    {
        [Header("실루엣 핀")]
        [Tooltip("담당 없는 사물 ({0}: 남은 수색 턴, {1}: 이번 턴 소음)")] public string Lead = "수색 {0}턴 · 소음 {1}";
        [Tooltip("관리실 선반 · 굴이 빈 동안만 ({0}: 남은 턴, {1}: 소음)")] public string DenShelf = "선반 수색 {0}턴 · 소음 {1}";
        [Tooltip("조명으로 시작한 수색 · 담당만 서면 멈추고 손전등 든 동료가 옆에 서야 이어 감 ({0}: 남은 수색 턴)")] public string LeadDark = "조명 수색 {0}턴 · 손전등 동료 필요";
        [Tooltip("협동 · 함께 ({0}: 함께 뒤지면 남는 턴)")] public string JoinTogether = "협동 · 함께 · {0}턴";
        [Tooltip("협동 · 망보기 ({0}: 사물 소음, {1}: 망보기 뒤 소음)")] public string JoinWatch = "협동 · 망보기 · 소음 {0}→{1}";
        [Tooltip("협동 · 조명 ({0}: 발견 보정 %p)")] public string JoinLight = "협동 · 조명 +{0}%p";
        [Tooltip("빈 문 · 귀 대기")] public string Listen = "귀 대기";
        [Tooltip("문에 모이는 중 ({0}: 놓으면 모인 인원, {1}: 살아 있는 인원)")] public string Gather = "모두 모이면 이동 ({0}/{1})";
        [Tooltip("마지막 한 명 ({0}: 갈 방)")] public string GatherLast = "다음 턴 이동 · {0}";
        [Tooltip("잠긴 문 · 마지막 한 명 ({0}: 갈 방, {1}: 걸리는 턴, {2}: 소음)")] public string GatherUnlock = "문 따고 {0}로 · {1}턴 · 소음 {2}";
        [Tooltip("흔적")] public string Observe = "흔적 관찰 · 1턴";
        [Header("못 놓는 자리 (회색 핀의 까닭)")]
        [Tooltip("도구 없음 ({0}: 도구 이름)")] public string NeedTool = "{0} 필요";
        [Tooltip("조명 수색을 이어 갈 손전등 동료가 없음")] public string NeedLight = "손전등 동료 필요";
        [Tooltip("그 밖의 까닭으로 지금 못 함")] public string Blocked = "지금은 할 수 없음";
        [Header("역할 칩")]
        [Tooltip("협동 역할 (0 함께 · 1 망보기 · 2 조명)")] public string[] Roles = { "함께", "망보기", "조명" };
        [Tooltip("못 고르는 까닭: 사물 소음 없음")] public string WhyNoNoise = "소음 없음";
        [Tooltip("못 고르는 까닭: 손전등 없음")] public string WhyNoLight = "손전등 없음";
        [Tooltip("못 고르는 까닭: 진행 중이라 역할 고정")] public string WhyLocked = "진행 중 · 역할 고정";
        [Tooltip("혼자 남은 대원이 문에 섰을 때 (0 이동 · 1 귀 대기)")] public string[] DoorRoles = { "이동", "귀 대기" };
        [Tooltip("못 고르는 까닭: 이 문은 지금 들을 수 없음")] public string WhyNoListen = "들을 수 없음";
        [Header("상황판 (놓은 뒤 · 시간 흐르지 않음)")]
        [Tooltip("수색 ({0}: 대원, {1}: 사물)")] [TextArea(2, 3)] public string StatusLead = "{0} · {1} 수색 배정\n'턴 진행'으로 함께 진행합니다.";
        [Tooltip("조명 수색 담당만 섰을 때 ({0}: 대원, {1}: 사물)")] [TextArea(2, 3)] public string StatusLeadDark = "{0} · {1} 수색 배정\n손전등을 든 동료를 옆에 놓아야 진행합니다.";
        [Tooltip("협동 ({0}: 대원, {1}: 사물, {2}: 역할)")] [TextArea(2, 3)] public string StatusJoin = "{0} · {1} {2}\n'턴 진행'으로 함께 진행합니다.";
        [Tooltip("귀 대기 ({0}: 대원, {1}: 문 너머 방)")] [TextArea(2, 3)] public string StatusListen = "{0} · {1} 문 귀 대기\n'턴 진행'마다 문 너머를 듣습니다.";
        [Tooltip("문에 모임 ({0}: 대원, {1}: 갈 방, {2}: 모인 인원, {3}: 살아 있는 인원)")] [TextArea(2, 3)] public string StatusGather = "{0} · {1} 쪽 문에 모임 ({2}/{3})\n모두 모이면 다음 턴 이동합니다.";
        [Tooltip("모두 모임 ({0}: 갈 방)")] [TextArea(2, 3)] public string StatusGathered = "{0}로 이동 예약\n'턴 진행'을 누르면 이동합니다.";
        [Tooltip("모두 모임 · 잠긴 문 ({0}: 갈 방, {1}: 걸리는 턴)")] [TextArea(2, 3)] public string StatusGatheredUnlock = "문 따고 {0}로 · {1}턴 예약\n'턴 진행'을 누르면 문을 엽니다.";
        [Tooltip("흔적 관찰 ({0}: 대원, {1}: 흔적)")] [TextArea(2, 3)] public string StatusObserve = "{0} · {1} 조사 배정\n'턴 진행'으로 다른 대원과 함께 행동합니다.";
        [Tooltip("다른 일에서 옮겼을 때 둘째 줄 ({0}: 하던 일)")] public string StatusMoved = "{0}에서 옮겼습니다.";
        [Tooltip("할 일에서 뺐을 때 ({0}: 대원, {1}: 하던 일)")] [TextArea(2, 3)] public string StatusUnassigned = "{0} · {1}에서 뺐습니다.\n시간은 흐르지 않았습니다.";
        [Tooltip("이동 예약이 풀렸을 때 둘째 줄")] public string StatusMoveCancelled = "이동 예약을 취소했습니다.";
        [Tooltip("협동 역할 이름 (상황판 · 할 일)")] public string RoleTogether = "함께 수색", RoleWatch = "망보기", RoleLight = "조명 지원";
        [Header("할 일 · 말 이름표 (마우스를 올릴 때)")]
        [Tooltip("이름표 ({0}: 대원, {1}: 할 일)")] public string Tag = "{0} · {1}";
        [Tooltip("수색 ({0}: 사물)")] public string TaskSearch = "{0} 수색";
        [Tooltip("수색 담당 진행 ({0}: 진행, {1}: 필요 턴)")] public string TaskProgress = " {0}/{1}";
        [Tooltip("협동 ({0}: 사물, {1}: 역할)")] public string TaskHelper = "{0} {1}";
        [Tooltip("귀 대기 ({0}: 문 너머 방)")] public string TaskListen = "{0} 문 귀 대기";
        [Tooltip("문에 모임 ({0}: 갈 방)")] public string TaskGather = "{0}로 이동 대기";
        [Tooltip("이동 예약 ({0}: 갈 방)")] public string TaskMove = "{0}로 이동";
        [Tooltip("흔적 관찰 ({0}: 흔적)")] public string TaskObserve = "{0} 관찰";
        [Tooltip("가방 물건 쓰기 · 다음 턴 ({0}: 물건)")] public string TaskUse = "{0} 사용";
        [Tooltip("멈춘 할 일 ({0}: 할 일, {1}: 까닭)")] public string TaskPaused = "{0} · 멈춤 ({1})";
        [Tooltip("할 일 없음 · 쓰러짐")] public string TaskFree = "행동 남음", TaskDown = "쓰러짐";
    }

    // 말 놓기의 규칙 (the rules half of placing pawns; FieldPawnBoard is the view). One per arrival panel, over the planner's own plan
    // (FieldTurnPlanner.Plan, the same Check the preview and the turn use) — no time passes here, the turn is '턴 진행' only.
    // Moved from FieldQuickAssign: the lead / join targets checked on a copy of the plan (LeadTarget, JoinTarget, Checked, Valid,
    // Release) and the door helpers (SiteButton, DoorButton, Behind, Topic). New: listen / gather at doors, the pawn rules for taking
    // a member off (a lead's bound helper leads on alone, a helper leaves a new search solo, a listener's first gatherer listens),
    // the helper's role chips, the pawn's spot and its hover line. Both visits (FieldTurnPlanner.Placing); the first visit (it sleeps)
    // has no listening: doors only gather. Door gatherers are the plan's (FieldTurnPlan.AssignGather): everyone living at one door is the
    // move next turn (ExpeditionRoomNavigation.Queue.cs reads it from the plan), so taking any pawn away cancels it. The pawn rules for
    // taking a member off are FieldTurnPlan.Unassign; a bag item queued for the turn (FieldTurnPlanner.QueueUse) is a task too.
    public sealed class FieldPlacement
    {
        public const int DoorSlots = 6;
        static readonly List<FieldPlacement> live = new List<FieldPlacement>();
        static readonly FieldPlacementTexts defaults = new FieldPlacementTexts();

        public static FieldPlacement Of(ExpeditionArrivalPanel a)
        {
            if (!a) return null;
            live.RemoveAll(p => !p.arrival);
            foreach (var p in live) if (p.arrival == a) return p;
            var n = new FieldPlacement(a); live.Add(n); return n;
        }

        readonly ExpeditionArrivalPanel arrival;
        FieldPlacement(ExpeditionArrivalPanel a) { arrival = a; }
        public ExpeditionArrivalPanel Arrival => arrival;
        public FieldPlacementTexts Texts { get { var p = Planner; return p && p.PlaceTexts != null ? p.PlaceTexts : defaults; } }
        // The status paper line the last placing wrote.
        public string LastStatus { get; private set; } = "";
        FieldTurnPlanner Planner => arrival && arrival.Threat ? arrival.Threat.Planner : null;

        // Placing is possible now: the room is open on either visit and nothing holds the screen (a window, the popup, a walk, a turn
        // resolving, a meeting or battle, the missing-person or trace window: ExpeditionSiteThreat.CanAct).
        public bool Ready
        {
            get
            {
                var a = arrival; var pl = Planner; var t = a ? a.Threat : null;
                if (!a || !a.IsOpen || !pl || !pl.Placing || !a.Rooms || a.InTransit || pl.Resolving || !t.CanAct || Meeting(a)) return false;
                return !a.Main || a.Main.interactable && a.Main.blocksRaycasts;
            }
        }

        // ---- version (views re-read only when it changes) ----
        struct Snap
        {
            public int Plan, Room, Turns, Queued, Alive, Stage, Clock, Kit, Clears, Resident;
            public bool Placing, Clue, Unlocked, Asleep, DenEmpty, DenStays, Meeting;
            public FieldSiteState State;
            public bool Same(in Snap o) => Plan == o.Plan && Room == o.Room && Turns == o.Turns && Queued == o.Queued && Alive == o.Alive && Stage == o.Stage
                && Clock == o.Clock && Kit == o.Kit && Clears == o.Clears && Resident == o.Resident && Placing == o.Placing && Clue == o.Clue && Unlocked == o.Unlocked
                && Asleep == o.Asleep && DenEmpty == o.DenEmpty && DenStays == o.DenStays && Meeting == o.Meeting && ReferenceEquals(State, o.State);
        }
        Snap snap; bool haveSnap; int version;
        readonly Dictionary<int, List<FieldPlaceOption>> options = new Dictionary<int, List<FieldPlaceOption>>();
        readonly Dictionary<int, List<FieldRoleChip>> chips = new Dictionary<int, List<FieldRoleChip>>();
        FieldPlanCheck check;

        // Bumps on the plan version, room, turns, the gathered door, the alive mask, the trace's stage, StorageUnlocked, the site
        // clock and the den, the members' tools and lamps.
        public int Version { get { Poll(); return version; } }
        void Poll()
        {
            var s = Take(); if (haveSnap && s.Same(snap)) return;
            snap = s; haveSnap = true; version++; options.Clear(); chips.Clear(); check = null;
        }
        void Touch() { haveSnap = false; Poll(); }
        Snap Take()
        {
            var a = arrival; var pl = Planner; var t = a ? a.Threat : null; var rooms = a ? a.Rooms : null; var s = t ? t.State : null; var story = a ? a.Story : null;
            var n = new Snap { Plan = pl ? pl.Plan.Version : -1, Placing = pl && pl.Placing, Clears = pl ? pl.Clears : 0, State = s, Queued = -1, Stage = -1 };
            if (rooms) { n.Room = rooms.CurrentRoom; n.Turns = rooms.Turns; n.Queued = rooms.HasQueuedMove ? rooms.QueuedRoom : -1; n.Unlocked = rooms.StorageUnlocked; }
            if (s != null) { n.Clock = s.TurnsUsed; n.Asleep = s.Asleep; n.DenEmpty = s.DenEmpty; n.DenStays = s.DenStaysEmpty; n.Resident = s.ResidentRoom; }
            if (a) { n.Alive = AliveMask(a); n.Meeting = Meeting(a); n.Kit = Kit(); }
            if (story) { n.Stage = story.State != null ? story.State.Stage : -1; n.Clue = story.Clue && story.Clue.gameObject.activeInHierarchy; }
            return n;
        }
        // Who carries a lamp, the room's tools and the lock's tool (a bag change offers or greys out places).
        int Kit()
        {
            var a = arrival; var f = Facts; if (!a || f == null || !a.Loot) return 0;
            int h = 17;
            for (int m = 0; m < a.Participants.Count; m++)
            {
                if (!Up(a, m)) continue;
                h = h * 31 + (f.HasLight(m) ? 1 : 2);
                for (int site = 0; site < a.Loot.Sites.Length; site++)
                    if (a.Loot.IsSiteInCurrentRoom(site) && !string.IsNullOrEmpty(a.Loot.Sites[site].RequiredTool)) h = h * 31 + (f.HasTool(site, m) ? 3 : 5);
            }
            if (a.Rooms) h = h * 31 + (a.Rooms.CanUnlock ? 7 : 11);
            return h;
        }

        // ---- what a member can take now ----
        // Every place for this member (enabled ones checked on a copy of the plan; disabled ones carry their reason). For one object
        // a member gets a lead or a join, never both; a place the member already holds is not offered.
        public IReadOnlyList<FieldPlaceOption> OptionsFor(int member)
        {
            Poll(); if (options.TryGetValue(member, out var list)) return list;
            list = Build(member); options[member] = list; return list;
        }
        List<FieldPlaceOption> Build(int m)
        {
            var list = new List<FieldPlaceOption>(); var a = arrival; var pl = Planner; var rooms = a ? a.Rooms : null; var t = a ? a.Threat : null;
            if (!a || !pl || !pl.Placing || !rooms || !t || t.State == null || !Up(a, m) || Facts == null) return list;
            var plan = pl.Plan; var facts = Facts; var k = CheckNow(); var tx = Texts; int room = rooms.CurrentRoom;
            // Searches (the den shelf only while the den stays empty: its spot is at the office door).
            for (int site = 0; a.Loot && site < a.Loot.Sites.Length; site++)
            {
                var button = SiteButton(a, site); if (!button || !button.gameObject.activeInHierarchy) continue;
                var f = facts.Site(site); if (!f.InRoom || f.Complete || !f.Searchable) continue;
                var run = k.RunFor(site); var standing = plan.Find(site);
                if (run != null && (run.Lead == m || run.Support == m) || run == null && standing != null && standing.Lead == m) continue;
                // A started light search waiting for its lamp (paused NoLight, e.g. its helper was taken off): the lamp joins it.
                bool dark = run == null && standing != null && k.PauseFor(site) == FieldPause.NoLight;
                var o = run != null ? JoinOption(plan, facts, site, m, run)
                    : (dark ? LampOption(plan, facts, site, m, standing) : null) ?? LeadOption(plan, facts, site, m, f) ?? (dark ? null : DarkLead(plan, facts, site, m, f)) ?? BlockedLead(plan, facts, site, m);
                if (o == null) continue;
                o.Anchor = button; o.Spot = new FieldSpotRef(room, FieldSpotRef.SearchKey(site), o.Kind == FieldSpotKind.Join ? 1 : 0); list.Add(o);
            }
            // Doors: the first pawn listens (awake board, a door that can be heard); at a move door the others gather.
            int alive = AliveCount();
            foreach (int door in Behind(room))
            {
                var button = DoorButton(a, room, door); if (!button || !button.gameObject.activeInHierarchy) continue;
                var at = AtDoorRaw(door); if (at.Contains(m)) continue;
                bool move = IsMoveDoor(a, room, door), hear = Listenable(door);
                FieldPlaceOption o = null;
                if (at.Count == 0 && hear && (alive > 1 || !move)) o = Checked(plan, facts, new FieldPlaceOption { Kind = FieldSpotKind.Listen, Member = m, Door = door, Glyph = ActionGlyph.Kind.Listen, Label = tx.Listen, Ops = p => p.AssignListen(m, door) });
                else if (move) o = GatherOption(m, door, at.Count, alive);
                if (o == null) continue;
                o.Anchor = button; o.Spot = new FieldSpotRef(room, FieldSpotRef.DoorKey(door), Mathf.Min(at.Count, DoorSlots - 1)); list.Add(o);
            }
            // The trace while it can be observed and nobody is on it.
            var story = a.Story; string id = ExpeditionNpcStory.ObservationId;
            if (story && story.Clue && story.Clue.gameObject.activeInHierarchy && story.ObserveBlock(id) == FieldPause.None && plan.ObserveAt(id) == null)
            {
                var o = Checked(plan, facts, new FieldPlaceOption { Kind = FieldSpotKind.Observe, Member = m, Id = id, Glyph = ActionGlyph.Kind.Observe, Label = tx.Observe, Ops = p => p.AssignObserve(m, id) });
                if (o != null) { o.Anchor = story.Clue; o.Spot = new FieldSpotRef(room, FieldSpotRef.ObserveKey(id), 0); list.Add(o); }
            }
            return list;
        }
        // Lead alone (a standing but stalled order keeps its role; a started search keeps its own).
        FieldPlaceOption LeadOption(FieldTurnPlan plan, IFieldPlanFacts facts, int site, int m, FieldSiteFacts f)
        {
            var standing = plan.Find(site); var tries = new List<FieldOrder>();
            if (standing != null) tries.Add(new FieldOrder { Site = site, Lead = m, Pace = standing.Pace, Duty = standing.Duty, Solo = standing.Solo, Support = standing.Support });
            tries.Add(f.Progress > 0 ? new FieldOrder { Site = site, Lead = m, Pace = f.Pace, Duty = f.Duty } : new FieldOrder { Site = site, Lead = m, Pace = 1, Duty = 0, Solo = true });
            foreach (var order in tries)
            {
                var o = order;
                var opt = Checked(plan, facts, new FieldPlaceOption { Kind = FieldSpotKind.Lead, Member = m, Site = site, Glyph = ActionGlyph.Kind.Search, Ops = p => p.Assign(o) });
                if (opt == null) continue;
                var r = Draft(plan, facts, opt.Ops).RunFor(site);
                opt.Label = string.Format(IsDen(site) ? Texts.DenShelf : Texts.Lead, r != null ? Math.Max(0, r.Required - r.Before) : 0, r != null ? r.Noise : 0);
                return opt;
            }
            return null;
        }
        // Join a running search as its helper (함께 / 망보기 / 조명, whatever it needs); a search with a helper already is full.
        FieldPlaceOption JoinOption(FieldTurnPlan plan, IFieldPlanFacts facts, int site, int m, FieldRun run)
        {
            if (run.Support >= 0 || run.Lead == m) return null;
            var standing = plan.Find(site); if (standing == null || standing.Lead != run.Lead) return null;
            bool fresh = run.Starts;
            var duties = new List<int> { standing.Duty }; if (fresh && standing.Duty != 0) duties.Add(0);
            foreach (int duty in duties)
            {
                int d = duty;
                Action<FieldTurnPlan> ops = p =>
                {
                    Release(p, m, site);
                    var o = p.Find(site); if (o == null) return; o = o.Copy(); o.Prefer = m;
                    if (fresh) { o.Solo = false; o.Duty = d; }
                    p.Assign(o);
                };
                var opt = Checked(plan, facts, new FieldPlaceOption { Kind = FieldSpotKind.Join, Member = m, Site = site, Ops = ops });
                if (opt == null) continue;
                var r = Draft(plan, facts, ops).RunFor(site); var tx = Texts;
                opt.Glyph = r != null && r.Role == FieldAction.Watch ? ActionGlyph.Kind.Watch : r != null && r.Role == FieldAction.Light ? ActionGlyph.Kind.Light : ActionGlyph.Kind.Search;
                opt.Label = r == null ? "" : r.Role == FieldAction.Watch ? string.Format(tx.JoinWatch, SiteNoise(site), r.Noise)
                    : r.Role == FieldAction.Light ? string.Format(tx.JoinLight, r.Bonus) : string.Format(tx.JoinTogether, Math.Max(0, r.Required - r.Before));
                return opt;
            }
            return null;
        }
        // A started light search whose order waits for a lamp (paused NoLight): a member with a flashlight joins as its light helper;
        // one without takes the lead when the waiting lead has one (the lead then holds the lamp). Null when neither fits.
        FieldPlaceOption LampOption(FieldTurnPlan plan, IFieldPlanFacts facts, int site, int m, FieldOrder standing)
        {
            int lead = standing.Lead; if (lead == m) return null;
            bool join = facts.HasLight(m); if (!join && !facts.HasLight(lead)) return null;
            Action<FieldTurnPlan> ops = join
                ? (Action<FieldTurnPlan>)(p => { Release(p, m, site); var o = p.Find(site); if (o == null) return; o = o.Copy(); o.Prefer = m; o.Support = -1; p.Assign(o); })
                : p => { var o = p.Find(site); if (o == null) return; o = o.Copy(); o.Prefer = o.Lead; o.Lead = m; o.Support = -1; p.Assign(o); };
            var opt = Checked(plan, facts, new FieldPlaceOption { Kind = join ? FieldSpotKind.Join : FieldSpotKind.Lead, Member = m, Site = site, Glyph = join ? ActionGlyph.Kind.Light : ActionGlyph.Kind.Search, Ops = ops });
            if (opt == null) return null;
            var r = Draft(plan, facts, ops).RunFor(site); var tx = Texts;
            opt.Label = r == null ? "" : join ? string.Format(tx.JoinLight, r.Bonus) : string.Format(IsDen(site) ? tx.DenShelf : tx.Lead, Math.Max(0, r.Required - r.Before), r.Noise);
            return opt;
        }
        // A started light search nobody leads: the lead may stand there (paused, '손전등 동료 필요') until a member with a flashlight
        // joins (LampOption) — offered while someone could hold the lamp for them (another living member with one, or this member has
        // one and someone else can lead). The search keeps its stored role and progress either way.
        FieldPlaceOption DarkLead(FieldTurnPlan plan, IFieldPlanFacts facts, int site, int m, FieldSiteFacts f)
        {
            if (f.Progress <= 0 || f.Duty != 2) return null;
            bool lamp = false; for (int j = 0; j < facts.Members && !lamp; j++) lamp = j != m && facts.Alive(j) && (facts.HasLight(j) || facts.HasLight(m));
            if (!lamp) return null;
            var order = new FieldOrder { Site = site, Lead = m, Pace = f.Pace, Duty = f.Duty };
            var opt = Checked(plan, facts, new FieldPlaceOption { Kind = FieldSpotKind.Lead, Member = m, Site = site, Glyph = ActionGlyph.Kind.Search, Dark = true, Ops = p => p.Assign(order) });
            if (opt != null) opt.Label = string.Format(Texts.LeadDark, Math.Max(0, f.Required - f.Progress));
            return opt;
        }
        // No lead would run here: a grey pin with the reason (the tool, the lamp), or nothing when the object is simply not open now.
        FieldPlaceOption BlockedLead(FieldTurnPlan plan, IFieldPlanFacts facts, int site, int m)
        {
            var d = plan.Clone(); d.Assign(new FieldOrder { Site = site, Lead = m, Pace = 1, Solo = true });
            var tx = Texts; string why;
            switch (d.Check(facts).PauseFor(site))
            {
                case FieldPause.NoTool: why = string.Format(tx.NeedTool, Planner.ToolName(site)); break;
                case FieldPause.NoLight: why = tx.NeedLight; break;
                case FieldPause.DenClosed: why = tx.Blocked; break;
                default: return null;
            }
            return new FieldPlaceOption { Kind = FieldSpotKind.Lead, Member = m, Site = site, Glyph = ActionGlyph.Kind.Search, Enabled = false, Blocked = why, Label = why };
        }
        // Wait at a move door (the last one needed makes the move: '다음 턴 이동 · {방}'); a lock nobody can open is a grey pin.
        FieldPlaceOption GatherOption(int m, int door, int there, int alive)
        {
            var rooms = arrival.Rooms; var tx = Texts; int k = there + 1; bool last = k >= alive, locked = door == FieldSiteState.Storage && !rooms.StorageUnlocked;
            var o = new FieldPlaceOption { Kind = FieldSpotKind.Gather, Member = m, Door = door, Glyph = ActionGlyph.Kind.Move, Ops = p => p.AssignGather(m, door) };
            o.Label = !last ? string.Format(tx.Gather, k, alive) : locked ? string.Format(tx.GatherUnlock, RoomName(door), rooms.MoveTurns(door), rooms.UnlockNoise) : string.Format(tx.GatherLast, RoomName(door));
            if (locked && !rooms.CanUnlock) { o.Enabled = false; o.Blocked = string.Format(tx.NeedTool, rooms.UnlockToolName); o.Label = o.Blocked; return o; }
            return Checked(Planner.Plan, Facts, o);
        }
        static FieldPlaceOption Checked(FieldTurnPlan plan, IFieldPlanFacts facts, FieldPlaceOption o) { var d = plan.Clone(); o.Ops(d); return Valid(d.Check(facts), o) ? o : null; }
        static FieldPlanCheck Draft(FieldTurnPlan plan, IFieldPlanFacts facts, Action<FieldTurnPlan> ops) { var d = plan.Clone(); ops(d); return d.Check(facts); }
        static bool Valid(FieldPlanCheck k, FieldPlaceOption o)
        {
            int m = o.Member; if (m < 0 || m >= k.Actions.Length) return false;
            switch (o.Kind)
            {
                case FieldSpotKind.Lead:
                    {
                        var r = k.RunFor(o.Site); if (r != null && r.Lead == m) return true;
                        if (o.Dark) foreach (var p in k.Paused) if (p.Site == o.Site && p.Lead == m && p.Reason == FieldPause.NoLight) return true;
                        return false;
                    }
                case FieldSpotKind.Join: { var r = k.RunFor(o.Site); return r != null && r.Support == m; }
                case FieldSpotKind.Listen: return k.Actions[m] == FieldAction.Listen && k.DoorOf[m] == o.Door;
                case FieldSpotKind.Observe: return k.Actions[m] == FieldAction.Observe && k.ObserveOf[m] == o.Id;
                case FieldSpotKind.Gather: return k.Actions[m] == FieldAction.Gather && k.GatherOf[m] == o.Door;
                default: return false;
            }
        }
        // A helper first leaves what they did (a lead's own search goes with them; a door; a trace). Planner APIs only.
        static void Release(FieldTurnPlan p, int m, int site)
        {
            var led = p.SiteLedBy(m); if (led != null && led.Site != site) p.Release(led.Site);
            var heard = p.ListenBy(m); if (heard != null) p.ReleaseListen(heard.Door);
            var watched = p.ObserveBy(m); if (watched != null) p.ReleaseObserve(watched.Id);
        }

        // ---- placing (no time) ----
        // Put the option's member there: they leave what they did first (the pawn rules of Unassign), the plan keeps the helpers the
        // player was shown, the object counts as looked at ('?' mark · visit record), and the status paper says what changed.
        public bool Place(FieldPlaceOption o)
        {
            if (o == null || !o.Enabled || !Ready) return false;
            int m = o.Member; var fresh = Match(OptionsFor(m), o); if (fresh == null || !fresh.Enabled) return false; o = fresh;
            var a = arrival; var pl = Planner; var plan = pl.Plan; var facts = Facts; var tx = Texts;
            var before = CheckNow(); string was = Busy(before, m) ? TaskName(before, m) : ""; bool wasMoving = a.Rooms.HasQueuedMove;
            // The member leaves what they did first (the pawn rules), unless that would undo this place (then last assignment wins).
            var draft = plan.Clone(); draft.Unassign(m, facts); o.Ops(draft);
            if (Valid(draft.Check(facts), o)) { plan.Unassign(m, facts); o.Ops(plan); }
            else if (Checked(plan, facts, o) != null) o.Ops(plan);
            else { Touch(); return false; }
            plan.Keep(plan.Check(facts));
            if (o.Site >= 0 && a.Rooms && !a.Rooms.Inspected.Contains(o.Site)) { string keep = a.Status ? a.Status.text : null; a.Rooms.MarkInspected(o.Site); if (keep != null) a.Status.text = keep; }
            Touch(); bool moving = a.Rooms.HasQueuedMove, dropped = wasMoving && !moving;
            string name = a.Participants[m].Name, line;
            switch (o.Kind)
            {
                case FieldSpotKind.Lead: line = string.Format(o.Dark && CheckNow().RunFor(o.Site) == null ? tx.StatusLeadDark : tx.StatusLead, name, SiteName(o.Site)); break;
                case FieldSpotKind.Join: { var r = CheckNow().RunFor(o.Site); line = string.Format(tx.StatusJoin, name, SiteName(o.Site), r == null ? tx.RoleTogether : RoleName(r.Role)); break; }
                case FieldSpotKind.Listen: line = string.Format(tx.StatusListen, name, RoomName(o.Door)); break;
                case FieldSpotKind.Gather: line = string.Format(tx.StatusGather, name, RoomName(o.Door), AtDoorRaw(o.Door).Count, AliveCount()); break;
                default: line = string.Format(tx.StatusObserve, name, ObserveName(o.Id)); break;
            }
            if (moving && (o.Kind == FieldSpotKind.Gather || o.Kind == FieldSpotKind.Listen)) line = GatheredLine();
            else if (dropped) line = FirstLine(line) + "\n" + tx.StatusMoveCancelled;
            else if (was.Length > 0) line = FirstLine(line) + "\n" + string.Format(tx.StatusMoved, was);
            Say(line); return true;
        }
        // Take the member off whatever they do (a placed pawn dropped on the floor or right-pressed). False when they had nothing.
        public bool Unassign(int member)
        {
            if (!Ready || !Up(arrival, member)) return false;
            var plan = Planner.Plan; var k = CheckNow(); if (!Busy(k, member) && plan.GatherBy(member) == null && plan.UseBy(member) == null) return false;
            string was = TaskName(k, member); var use = plan.UseBy(member); if (was.Length == 0 && use != null) was = string.Format(Texts.TaskUse, ItemName(use.Item));
            bool wasMoving = arrival.Rooms.HasQueuedMove;
            if (!plan.Unassign(member, Facts)) return false;
            plan.Keep(plan.Check(Facts)); Touch(); bool cancelled = wasMoving && !arrival.Rooms.HasQueuedMove;
            var tx = Texts; string line = string.Format(tx.StatusUnassigned, arrival.Participants[member].Name, was);
            if (cancelled) line = FirstLine(line) + "\n" + tx.StatusMoveCancelled;
            Say(line); return true;
        }
        // After a pawn went to a move door that still needs others: the next living member (after it, in member order) who is not
        // there yet, for the board to pick up (추가 결정: 문에 모이기). −1 when nobody is needed.
        public int NextAfter(FieldPlaceOption placed)
        {
            var a = arrival; if (placed == null || placed.Kind != FieldSpotKind.Listen && placed.Kind != FieldSpotKind.Gather || placed.Door < 0 || !a || !a.Rooms) return -1;
            if (!IsMoveDoor(a, a.Rooms.CurrentRoom, placed.Door) || GatheredDoor == placed.Door) return -1;
            var at = AtDoorRaw(placed.Door); int n = a.Participants.Count;
            for (int i = 1; i <= n; i++) { int j = (placed.Member + i) % n; if (Up(a, j) && !at.Contains(j)) return j; }
            return -1;
        }
        static FieldPlaceOption Match(IReadOnlyList<FieldPlaceOption> list, FieldPlaceOption o)
        {
            foreach (var x in list) if (x.Kind == o.Kind && x.Member == o.Member && x.Site == o.Site && x.Door == o.Door && x.Id == o.Id) return x;
            return null;
        }
        // ---- where each pawn stands ----
        // The spot the member's plan puts them at (a paused assignment keeps its spot); false = free (or down, or not in this room).
        public bool TryGetSpot(int member, out FieldSpotRef spot)
        {
            spot = default; var a = arrival; var pl = Planner; var rooms = a ? a.Rooms : null;
            if (!rooms || !pl || !pl.Placing || !Up(a, member)) return false;
            var k = CheckNow(); if (k == null || member >= k.Actions.Length) return false;
            int room = rooms.CurrentRoom;
            switch (k.Actions[member])
            {
                case FieldAction.Lead: spot = new FieldSpotRef(room, FieldSpotRef.SearchKey(k.SiteOf[member]), 0); return true;
                case FieldAction.Together: case FieldAction.Watch: case FieldAction.Light: spot = new FieldSpotRef(room, FieldSpotRef.SearchKey(k.SiteOf[member]), 1); return true;
                case FieldAction.Listen: spot = DoorSpot(room, k.DoorOf[member], member); return true;
                case FieldAction.Observe: spot = new FieldSpotRef(room, FieldSpotRef.ObserveKey(k.ObserveOf[member]), 0); return true;
                case FieldAction.Gather: spot = DoorSpot(room, k.GatherOf[member], member); return true;
                case FieldAction.Paused:
                    if (!string.IsNullOrEmpty(k.ObserveOf[member]))
                    {
                        if (Gone(k.ObservePauseFor(member))) return false;
                        spot = new FieldSpotRef(room, FieldSpotRef.ObserveKey(k.ObserveOf[member]), 0); return true;
                    }
                    if (k.DoorOf[member] >= 0)
                    {
                        if (Gone(k.ListenPauseFor(member))) return false;
                        spot = DoorSpot(room, k.DoorOf[member], member); return true;
                    }
                    if (k.GatherOf[member] >= 0)
                    {
                        if (Gone(k.GatherPauseFor(member))) return false;
                        spot = DoorSpot(room, k.GatherOf[member], member); return true;
                    }
                    { int site = k.SiteOf[member]; if (site < 0 || Gone(k.PauseFor(site))) return false; spot = new FieldSpotRef(room, FieldSpotRef.SearchKey(site), 0); return true; }
                default: return false;
            }
        }
        static bool Gone(FieldPause why) => why == FieldPause.OtherRoom || why == FieldPause.Complete || why == FieldPause.Downed;
        FieldSpotRef DoorSpot(int room, int door, int m) { int i = AtDoorRaw(door).IndexOf(m); return new FieldSpotRef(room, FieldSpotRef.DoorKey(door), Mathf.Clamp(i, 0, DoorSlots - 1)); }
        // Everyone at a door in slot order: the listener first, then those waiting, as they came (the plan's order).
        public IReadOnlyList<int> AtDoor(int door) { Poll(); return AtDoorRaw(door); }
        List<int> AtDoorRaw(int door)
        {
            var list = new List<int>(); var pl = Planner; if (!pl) return list;
            var l = pl.Plan.ListenAt(door); if (l != null && Up(arrival, l.Member)) list.Add(l.Member);
            foreach (var g in pl.Plan.Gathers) if (g.Door == door && Up(arrival, g.Member) && !list.Contains(g.Member)) list.Add(g.Member);
            return list;
        }
        // The door every living member stands at (its listener counts, except a lone member listening), else -1: the plan's check.
        public int GatheredDoor { get { var k = CheckNow(); return k != null ? k.GatheredDoor : -1; } }

        // ---- the helper's role chips (and a lone member at a door) ----
        public IReadOnlyList<FieldRoleChip> ChipsFor(int member)
        {
            Poll(); if (chips.TryGetValue(member, out var list)) return list;
            list = BuildChips(member); chips[member] = list; return list;
        }
        List<FieldRoleChip> BuildChips(int m)
        {
            var list = new List<FieldRoleChip>(); var a = arrival; var pl = Planner;
            if (!a || !pl || !pl.Placing || !a.Rooms || !Up(a, m) || Facts == null) return list;
            var k = CheckNow(); if (k == null || m >= k.Actions.Length) return list; var tx = Texts; var x = k.Actions[m];
            if (x == FieldAction.Together || x == FieldAction.Watch || x == FieldAction.Light)
            {
                int site = k.SiteOf[m]; var run = k.RunFor(site); var order = pl.Plan.Find(site); if (run == null || order == null) return list;
                for (int d = 0; d < 3; d++)
                {
                    var c = new FieldRoleChip { Label = NameAt(tx.Roles, d), On = run.Duty == d };
                    if (!run.Starts) { c.Enabled = false; c.Why = tx.WhyLocked; }
                    else
                    {
                        int duty = d; var r = Draft(pl.Plan, Facts, p => p.Assign(RoleOrder(order, duty, m))).RunFor(site);
                        c.Enabled = Fits(r, d, m);
                        if (!c.Enabled) c.Why = d == 2 ? tx.WhyNoLight : d == 1 && SiteNoise(site) == 0 ? tx.WhyNoNoise : tx.Blocked;
                    }
                    list.Add(c);
                }
                return list;
            }
            int door = x == FieldAction.Gather ? k.GatherOf[m] : x == FieldAction.Listen ? k.DoorOf[m] : -1;
            if (door >= 0 && AliveCount() == 1 && IsMoveDoor(a, a.Rooms.CurrentRoom, door))
            {
                bool locked = door == FieldSiteState.Storage && !a.Rooms.StorageUnlocked, can = !locked || a.Rooms.CanUnlock, hear = Listenable(door);
                list.Add(new FieldRoleChip { Label = NameAt(tx.DoorRoles, 0), On = x == FieldAction.Gather, Enabled = can, Why = can ? "" : string.Format(tx.NeedTool, a.Rooms.UnlockToolName) });
                list.Add(new FieldRoleChip { Label = NameAt(tx.DoorRoles, 1), On = x == FieldAction.Listen, Enabled = hear, Why = hear ? "" : tx.WhyNoListen });
            }
            return list;
        }
        // Change the helper's role (a copy of the plan must bind them in it), or a lone member's move / listen at their door. No time.
        public bool Choose(int member, int chip)
        {
            if (!Ready) return false;
            var list = ChipsFor(member); if (chip < 0 || chip >= list.Count) return false;
            var c = list[chip]; if (c.On) return true; if (!c.Enabled) return false;
            var a = arrival; var plan = Planner.Plan; var facts = Facts; var k = CheckNow(); var tx = Texts; var x = k.Actions[member];
            string name = a.Participants[member].Name, line;
            if (x == FieldAction.Together || x == FieldAction.Watch || x == FieldAction.Light)
            {
                int site = k.SiteOf[member]; var order = plan.Find(site); if (order == null) return false;
                plan.Assign(RoleOrder(order, chip, member)); plan.Keep(plan.Check(facts));
                line = string.Format(tx.StatusJoin, name, SiteName(site), RoleName(chip));
            }
            else
            {
                int door = x == FieldAction.Gather ? k.GatherOf[member] : k.DoorOf[member]; if (door < 0) return false;
                if (chip == 0) plan.AssignGather(member, door); else plan.AssignListen(member, door);
                plan.Keep(plan.Check(facts)); Touch(); bool moving = a.Rooms.HasQueuedMove;
                line = moving ? GatheredLine() : string.Format(tx.StatusListen, name, RoomName(door));
            }
            Say(line); return true;
        }
        static FieldOrder RoleOrder(FieldOrder order, int duty, int helper) { var o = order.Copy(); o.Duty = duty; o.Solo = false; o.Prefer = helper; o.Support = helper; return o; }
        static bool Fits(FieldRun r, int duty, int helper) => r != null && r.Support == helper && r.Role == (duty == 1 ? FieldAction.Watch : duty == 2 ? FieldAction.Light : FieldAction.Together);

        // ---- the hover line ----
        // '윤서진 · 물자 상자 수색 1/2', '한해인 · 행동 남음', '… · 복도로 이동'.
        public string Describe(int member)
        {
            var a = arrival; if (!a || member < 0 || member >= a.Participants.Count || a.Participants[member] == null) return "";
            var tx = Texts; string name = a.Participants[member].Name, task; var rooms = a.Rooms; var k = CheckNow();
            if (!Up(a, member)) task = tx.TaskDown;
            else if (rooms && rooms.HasQueuedMove && AtDoorRaw(rooms.QueuedRoom).Contains(member)) task = string.Format(tx.TaskMove, RoomName(rooms.QueuedRoom));
            else if (k == null || member >= k.Actions.Length) task = tx.TaskFree;
            else
            {
                task = TaskName(k, member); var x = k.Actions[member];
                if (x == FieldAction.Lead) { var r = k.RunFor(k.SiteOf[member]); if (r != null) task += string.Format(tx.TaskProgress, r.Before, r.Required); }
                else if (x == FieldAction.Paused) task = string.Format(tx.TaskPaused, task, PauseReason(k, member));
                if (string.IsNullOrEmpty(task)) task = tx.TaskFree;
            }
            return string.Format(tx.Tag, name, task);
        }
        // What the member does, without numbers ("" when free).
        string TaskName(FieldPlanCheck k, int m)
        {
            var tx = Texts;
            if (k == null || m < 0 || m >= k.Actions.Length) return "";
            switch (k.Actions[m])
            {
                case FieldAction.Gather: return string.Format(tx.TaskGather, RoomName(k.GatherOf[m]));
                case FieldAction.Use: return string.Format(tx.TaskUse, ItemName(k.UseOf[m]));
                case FieldAction.Lead: return string.Format(tx.TaskSearch, SiteName(k.SiteOf[m]));
                case FieldAction.Together: case FieldAction.Watch: case FieldAction.Light: return string.Format(tx.TaskHelper, SiteName(k.SiteOf[m]), RoleName(k.Actions[m]));
                case FieldAction.Listen: return string.Format(tx.TaskListen, RoomName(k.DoorOf[m]));
                case FieldAction.Observe: return string.Format(tx.TaskObserve, ObserveName(k.ObserveOf[m]));
                case FieldAction.Paused:
                    return !string.IsNullOrEmpty(k.ObserveOf[m]) ? string.Format(tx.TaskObserve, ObserveName(k.ObserveOf[m]))
                        : k.DoorOf[m] >= 0 ? string.Format(tx.TaskListen, RoomName(k.DoorOf[m])) : k.GatherOf[m] >= 0 ? string.Format(tx.TaskGather, RoomName(k.GatherOf[m]))
                        : string.Format(tx.TaskSearch, SiteName(k.SiteOf[m]));
                default: return "";
            }
        }
        string PauseReason(FieldPlanCheck k, int m)
        {
            var pl = Planner; if (!pl) return "";
            if (!string.IsNullOrEmpty(k.ObserveOf[m])) return pl.PauseShort(-1, k.ObservePauseFor(m));
            if (k.DoorOf[m] >= 0) return pl.PauseShort(-1, k.ListenPauseFor(m));
            if (k.GatherOf[m] >= 0) return pl.PauseShort(-1, k.GatherPauseFor(m));
            int site = k.SiteOf[m]; return pl.PauseShort(site, k.PauseFor(site));
        }
        static bool Busy(FieldPlanCheck k, int m) => k != null && m >= 0 && m < k.Actions.Length && k.Actions[m] != FieldAction.Hush && k.Actions[m] != FieldAction.Down;
        // The plan's check now (the same one the preview and the turn use), cached per Version.
        public FieldPlanCheck CheckNow()
        {
            Poll(); var pl = Planner; var f = Facts; if (!pl || f == null) return null;
            return check ?? (check = pl.Plan.Check(f));
        }

        string GatheredLine()
        {
            var rooms = arrival.Rooms; var tx = Texts; int to = rooms.QueuedRoom;
            return to == FieldSiteState.Storage && !rooms.StorageUnlocked ? string.Format(tx.StatusGatheredUnlock, RoomName(to), rooms.QueuedTurns) : string.Format(tx.StatusGathered, RoomName(to));
        }
        // The first visit (it sleeps): nothing to hear, doors only gather. Otherwise the planner's own rule (adjacent, an opened lock,
        // the den while home).
        bool Listenable(int door)
        {
            var t = arrival ? arrival.Threat : null; if (!t || t.State == null || t.State.Asleep) return false;
            return !(Facts is IFieldListenFacts lf) || lf.ListenBlock(door) == FieldPause.None;
        }

        // ---- small helpers ----
        IFieldPlanFacts Facts { get { var pl = Planner; return pl ? pl.Facts() : null; } }
        void Say(string line)
        {
            var a = arrival; LastStatus = line ?? "";
            if (a && a.Status) a.Status.text = LastStatus;
            if (a && a.Threat) a.Threat.Refresh();
            Touch();
        }
        static string FirstLine(string s) { int cut = s.IndexOf('\n'); return cut >= 0 ? s.Substring(0, cut) : s; }
        static bool Up(ExpeditionArrivalPanel a, int m) => a && m >= 0 && m < a.Participants.Count && a.Participants[m] != null && a.Participants[m].Health > 0;
        static int AliveMask(ExpeditionArrivalPanel a) { int mask = 0; for (int i = 0; i < a.Participants.Count && i < 31; i++) if (Up(a, i)) mask |= 1 << i; return mask; }
        int AliveCount() { int n = 0; var a = arrival; if (a) for (int i = 0; i < a.Participants.Count; i++) if (Up(a, i)) n++; return n; }
        static bool Meeting(ExpeditionArrivalPanel a) => a && a.Encounter && (a.Encounter.IsOpen || a.Encounter.Battle && a.Encounter.Battle.IsOpen);
        bool IsDen(int site) => arrival && arrival.Threat && site == arrival.Threat.DenSite;
        int SiteNoise(int site) => arrival && arrival.Loot ? arrival.Loot.SiteNoise(site) : 0;
        string SiteName(int site) => arrival && arrival.ObjectNames != null && site >= 0 && site < arrival.ObjectNames.Length ? arrival.ObjectNames[site] : "";
        string ObserveName(string id) { var s = arrival ? arrival.Story : null; return s ? s.ObserveLabel(id) : "흔적"; }
        string RoleName(int duty) { var tx = Texts; return duty == 1 ? tx.RoleWatch : duty == 2 ? tx.RoleLight : tx.RoleTogether; }
        string RoleName(FieldAction role) => RoleName(role == FieldAction.Watch ? 1 : role == FieldAction.Light ? 2 : 0);
        static string NameAt(string[] names, int i) => names != null && i >= 0 && i < names.Length ? names[i] : "";
        static string RoomName(int room) => room >= 0 && room < FieldSiteState.RoomNames.Length ? FieldSiteState.RoomNames[room] : "";
        string ItemName(string id) { var inv = arrival ? arrival.Inventory : null; return inv != null ? inv.Items.FirstOrDefault(i => i.Id == id)?.Name ?? id : id; }

        // ---- rooms, doors, objects (moved from FieldQuickAssign) ----
        // The button that stands for a search site (the den shelf: the office door; never the arcade door).
        public static Button SiteButton(ExpeditionArrivalPanel a, int site)
        {
            if (!a) return null; var t = a.Threat; if (t && site == t.DenSite) return a.Rooms ? a.Rooms.OfficeDoor : null;
            if (a.Objects == null || site < 0 || site >= a.Objects.Length) return null;
            var b = a.Objects[site]; return b == DoorButton(a, FieldSiteState.Arcade, FieldSiteState.Corridor) ? null : b;
        }
        // The rooms behind the doors of `room`.
        public static IEnumerable<int> Behind(int room)
        {
            if (room == FieldSiteState.Arcade) yield return FieldSiteState.Corridor;
            else if (room == FieldSiteState.Corridor) { yield return FieldSiteState.Arcade; yield return FieldSiteState.Storage; yield return FieldSiteState.Den; }
            else if (room == FieldSiteState.Storage) yield return FieldSiteState.Corridor;
        }
        // The door of `room` that opens onto `behind` (the same doors as the room navigation and the markers).
        public static Button DoorButton(ExpeditionArrivalPanel a, int room, int behind)
        {
            var r = a ? a.Rooms : null; if (!r) return null;
            if (room == FieldSiteState.Arcade && behind == FieldSiteState.Corridor) return a.Objects != null && a.Objects.Length > 3 ? a.Objects[3] : null;
            if (room == FieldSiteState.Corridor) return behind == FieldSiteState.Arcade ? r.CorridorBack : behind == FieldSiteState.Storage ? r.LockedDoor : behind == FieldSiteState.Den ? r.OfficeDoor : null;
            return room == FieldSiteState.Storage && behind == FieldSiteState.Corridor ? r.StorageBack : null;
        }
        // A door the whole party can walk through (the den never is).
        public static bool IsMoveDoor(ExpeditionArrivalPanel a, int room, int behind) => behind != FieldSiteState.Den && FieldSiteState.Adjacent(room, behind) && DoorButton(a, room, behind);
        // Which door of the current room a button is (ExpeditionRoomNavigation.DoorPressed, and Objects[3] through Pressed(3)).
        public static bool TryDoor(ExpeditionArrivalPanel a, Button b, out int behind)
        {
            behind = -1; if (!a || !b || !a.Rooms) return false; int room = a.Rooms.CurrentRoom;
            foreach (int d in Behind(room)) if (DoorButton(a, room, d) == b) { behind = d; return true; }
            return false;
        }
        // 은 / 는 after the last syllable.
        public static string Topic(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            char c = name[name.Length - 1];
            return name + (c < 0xAC00 || c > 0xD7A3 ? "은(는)" : (c - 0xAC00) % 28 != 0 ? "은" : "는");
        }
    }
}
