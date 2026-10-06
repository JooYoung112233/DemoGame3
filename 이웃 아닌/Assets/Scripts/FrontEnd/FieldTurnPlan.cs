using System;
using System.Collections.Generic;
using System.Linq;

namespace Demo5.FrontEnd
{
    // What a member does this turn on the site board. Members are indices into ExpeditionArrivalPanel.Participants (fixed for a visit).
    // Gather: standing at a move door (everyone living there = the move next turn; silent, not idle). Use: a bag item used when the
    // turn resolves (silent, not a hush). Neither is saved (a visit is never saved).
    public enum FieldAction { Hush, Lead, Together, Watch, Light, Down, Paused, Listen, Observe, Gather, Use }
    public enum FieldPause { None, Downed, Complete, OtherRoom, DenClosed, NoTool, NoLight, DenEmpty, NoItem }
    public enum FieldMoveKind { LeadMoved, SupportMoved, ListenMoved }
    // Site: the object an assignment left (-1 for a door). Door: the room behind the door a listener left.
    public readonly struct FieldMoveNote
    {
        public readonly FieldMoveKind Kind; public readonly int Site, Member, Door;
        public FieldMoveNote(FieldMoveKind kind, int site, int member, int door = -1) { Kind = kind; Site = site; Member = member; Door = door; }
    }

    // 문에 귀 대기: one member at one door (Door = the room behind it). Silent, but not hushing.
    public sealed class FieldListen
    {
        public int Door = -1, Member = -1;
        public FieldListen Copy() => (FieldListen)MemberwiseClone();
    }
    // Optional: whether a door can be listened at now (None / OtherRoom / DenEmpty). Facts without it allow every door.
    public interface IFieldListenFacts { FieldPause ListenBlock(int door); }

    // One named trace, one member, one turn. Reading its revealed conversation is a separate, free action.
    public sealed class FieldObserve
    {
        public string Id; public int Member = -1;
        public FieldObserve Copy() => (FieldObserve)MemberwiseClone();
    }
    public interface IFieldObservationFacts { FieldPause ObserveBlock(string id); }

    // 문에 모이기 (기획/탐험-말놓기-조작-재설계.md): one member waiting at a move door (Door = the room behind it). Silent and not idle;
    // when the listener there plus everyone waiting are all the living members, the party moves on the next turn (GatheredDoor).
    public sealed class FieldGather
    {
        public int Door = -1, Member = -1;
        public FieldGather Copy() => (FieldGather)MemberwiseClone();
    }
    // Optional: whether the party can go through that door from here (None / OtherRoom: not a move door here / NoTool: a lock nobody
    // can open). Facts without it allow every door.
    public interface IFieldGatherFacts { FieldPause GatherBlock(int door); }

    // 가방 물건 쓰기 (추가 결정): the member's action for the next '턴 진행' is using one item from their own bag. One turn, no noise.
    public sealed class FieldUse
    {
        public string Item; public int Member = -1;
        public FieldUse Copy() => (FieldUse)MemberwiseClone();
    }
    // Optional: whether the member can use it when the turn resolves (None / NoItem: not enough in the bag / Complete: nothing to heal).
    public interface IFieldUseFacts { FieldPause UseBlock(int member, string item); }

    // One search order: a lead and which support role (the supporter is bound when the plan is checked).
    // Pace is legacy (search pace was removed 2026-09-25): Check ignores it and new searches store 1; kept so older callers compile.
    public sealed class FieldOrder
    {
        public int Site, Lead = -1, Support = -1, Prefer = -1, Pace = 1, Duty;
        public bool Solo;
        public FieldOrder Copy() => (FieldOrder)MemberwiseClone();
        public bool SameAs(FieldOrder o) => o != null && o.Site == Site && o.Lead == Lead && o.Duty == Duty && o.Solo == Solo;
    }

    // Noise: the object's noise on every turn it is searched (0-3, ExpeditionLootPanel.Site.Noise). Turns: its base search turns
    // (Site.Turns, 0 = FieldTurnPlan.DefaultTurns). Progress..Bonus: the running search as stored (Pace and a 함께 Bonus only in older saves).
    public struct FieldSiteFacts { public bool InRoom, Searchable, Complete, Opened; public int Progress, Required, Pace, Duty, Bonus, Noise, Turns; }
    // What the plan needs to know about the party and the objects (the screen supplies it; tests fake it).
    public interface IFieldPlanFacts
    {
        int Members { get; }
        int LightBonus { get; }
        bool Alive(int member);
        bool HasLight(int member);
        bool HasTool(int site, int member);
        FieldSiteFacts Site(int site);
    }

    // One object's progress this turn, exactly as it will be applied.
    public sealed class FieldRun
    {
        public int Site, Lead, Support = -1, Pace, Duty, Bonus, Lost, Before, After, Required, Noise;
        public bool Starts, Completes, Watched, Forfeits;
        public FieldAction Role;
    }

    public sealed class FieldPlanCheck
    {
        public readonly List<FieldRun> Runs = new List<FieldRun>();
        public readonly List<(int Site, int Lead, FieldPause Reason)> Paused = new List<(int, int, FieldPause)>();
        public readonly List<(int Door, int Member)> Listens = new List<(int, int)>();
        public readonly List<(int Door, int Member, FieldPause Reason)> ListenPaused = new List<(int, int, FieldPause)>();
        public readonly List<(string Id, int Member)> Observations = new List<(string, int)>();
        public readonly List<(string Id, int Member, FieldPause Reason)> ObservePaused = new List<(string, int, FieldPause)>();
        // Waiting at a move door (in the order they came) / waiting at a door the party cannot go through now.
        public readonly List<(int Door, int Member)> Gathers = new List<(int, int)>();
        public readonly List<(int Door, int Member, FieldPause Reason)> GatherPaused = new List<(int, int, FieldPause)>();
        // Bag items used when the turn resolves / uses that cannot happen (the member hushes instead).
        public readonly List<(string Item, int Member)> Uses = new List<(string, int)>();
        public readonly List<(string Item, int Member, FieldPause Reason)> UsePaused = new List<(string, int, FieldPause)>();
        public FieldAction[] Actions = new FieldAction[0];
        public int[] SiteOf = new int[0], DoorOf = new int[0], GatherOf = new int[0];
        public string[] ObserveOf = new string[0], UseOf = new string[0];
        public int Noise, Hushing;
        public bool Full;
        // The door every living member stands at (its listener counts, a lone member listening does not): the move next turn. −1 none.
        public int GatheredDoor = -1;
        // An observation or a bag item makes no noise, but still spends an action rather than hushing. Waiting at a door is silent.
        public bool HushedAll => Runs.Count == 0 && Listens.Count == 0 && Observations.Count == 0 && Uses.Count == 0;
        public FieldRun RunFor(int site) => Runs.Find(r => r.Site == site);
        public FieldPause PauseFor(int site) { foreach (var p in Paused) if (p.Site == site) return p.Reason; return FieldPause.None; }
        public int ListenerAt(int door) { foreach (var l in Listens) if (l.Door == door) return l.Member; return -1; }
        public FieldPause ListenPauseFor(int member) { foreach (var l in ListenPaused) if (l.Member == member) return l.Reason; return FieldPause.None; }
        public FieldPause ObservePauseFor(int member) { foreach (var o in ObservePaused) if (o.Member == member) return o.Reason; return FieldPause.None; }
        public FieldPause GatherPauseFor(int member) { foreach (var g in GatherPaused) if (g.Member == member) return g.Reason; return FieldPause.None; }
        public int GatherersAt(int door) { int n = 0; foreach (var g in Gathers) if (g.Door == door) n++; return n; }
    }

    // Per-member action slots (기획/탐험-대원별행동배정-1차-구현.md): every living member has one slot per turn;
    // unassigned members hush. Check() is pure and is what the preview, the panels and the turn itself all use.
    // 말 놓기 (기획/탐험-말놓기-조작-재설계.md): with AutoFill off (the planner's plan) a helper is only a member the player placed there;
    // Unassign is the pawn rule for taking a member off; gathers and bag uses are member actions too.
    public sealed class FieldTurnPlan
    {
        // TogetherBonus / BaseNoise: the rules before 2026-09-25 (a 함께 bonus locked in an older save is still honoured until that search ends).
        public const int TogetherBonus = 10, BaseNoise = 3, WatchRelief = 1, DefaultTurns = 2;
        // On: an order that wants a helper binds the lowest free member when none was placed or kept (the 07 panel and the note).
        // Off (FieldTurnPlanner.AutoFillHelpers, the pawn board): only a placed (Prefer) or kept (Support) helper is bound; a helper who
        // leaves a new search leaves it solo, and a running 함께 search keeps its helper bound (the pawn still stands there).
        public bool AutoFill = true;
        readonly List<FieldOrder> orders = new List<FieldOrder>();
        readonly List<FieldListen> listens = new List<FieldListen>();
        readonly List<FieldObserve> observations = new List<FieldObserve>();
        readonly List<FieldGather> gathers = new List<FieldGather>();
        readonly List<FieldUse> uses = new List<FieldUse>();
        public IReadOnlyList<FieldOrder> Orders => orders;
        public IReadOnlyList<FieldListen> Listens => listens;
        public IReadOnlyList<FieldObserve> Observations => observations;
        public IReadOnlyList<FieldGather> Gathers => gathers;
        public IReadOnlyList<FieldUse> Uses => uses;
        // Something that acts this turn (a search, a door listened at, a trace, a bag item). Waiting at a door is not counted (HasGathers).
        public bool HasAssignments => orders.Count > 0 || listens.Count > 0 || observations.Count > 0 || uses.Count > 0;
        public bool HasGathers => gathers.Count > 0;
        public FieldObserve ObserveAt(string id) => observations.Find(o => o.Id == id);
        public FieldObserve ObserveBy(int member) => observations.Find(o => o.Member == member);
        public FieldListen ListenAt(int door) => listens.Find(l => l.Door == door);
        public FieldListen ListenBy(int member) => listens.Find(l => l.Member == member);
        public FieldGather GatherBy(int member) => gathers.Find(g => g.Member == member);
        public FieldUse UseBy(int member) => uses.Find(u => u.Member == member);
        public int Version { get; private set; }

        // 속도 삭제와 사물 소음 (기획/탐험-수색쪽지와-협동-1차.md, 2026-09-25): an object makes its own noise on every turn it is searched and
        // 망보기 lowers it by 1 (min 0); 함께 수색 finishes the search one turn sooner (min 1), fixed when the search starts.
        public static int SearchNoise(int objectNoise, bool watched) => Math.Max(0, objectNoise - (watched ? WatchRelief : 0));
        public static int RequiredOf(int turns, bool together) => Math.Max(1, (turns > 0 ? turns : DefaultTurns) - (together ? 1 : 0));
        // The old pace table (fast 3, a lookout 2, normal and precise 1). No rule uses it any more.
        [Obsolete("속도 삭제 (2026-09-25): FieldTurnPlan.SearchNoise(사물 소음, 망보기)를 쓰세요.")]
        public static int NoiseOf(int pace, bool watched) => pace <= 0 ? Math.Max(1, BaseNoise - (watched ? WatchRelief : 0)) : 1;
        public FieldOrder Find(int site) => orders.Find(o => o.Site == site);
        public FieldOrder SiteLedBy(int member) => orders.Find(o => o.Lead == member);
        public FieldTurnPlan Clone()
        {
            var p = new FieldTurnPlan { Version = Version, AutoFill = AutoFill };
            foreach (var o in orders) p.orders.Add(o.Copy()); foreach (var l in listens) p.listens.Add(l.Copy()); foreach (var o in observations) p.observations.Add(o.Copy());
            foreach (var g in gathers) p.gathers.Add(g.Copy()); foreach (var u in uses) p.uses.Add(u.Copy());
            return p;
        }
        public void Clear() { if (!HasAssignments && !HasGathers) return; orders.Clear(); listens.Clear(); observations.Clear(); gathers.Clear(); uses.Clear(); Version++; }
        public bool ReleaseListen(int door) { bool removed = listens.RemoveAll(l => l.Door == door) > 0; if (removed) Version++; return removed; }
        public bool ReleaseObserve(string id) { bool removed = observations.RemoveAll(o => o.Id == id) > 0; if (removed) Version++; return removed; }
        public bool ReleaseGather(int member) { bool removed = gathers.RemoveAll(g => g.Member == member) > 0; if (removed) Version++; return removed; }
        // Everyone waiting at doors goes back (a listener stays): the reserved move is off.
        public bool ReleaseGathers() { bool removed = gathers.Count > 0; if (removed) { gathers.Clear(); Version++; } return removed; }
        public bool ReleaseUse(int member) { bool removed = uses.RemoveAll(u => u.Member == member) > 0; if (removed) Version++; return removed; }

        // A member leaves the orders they support (the order they lead is the caller's business). With AutoFill off an order a
        // helper leaves turns solo (a new search then runs alone instead of binding someone else; a running one keeps its stored role).
        void LeaveSupport(int member, List<FieldMoveNote> notes)
        {
            foreach (var o in orders)
            {
                bool left = o.Support == member || !AutoFill && o.Prefer == member;
                if (o.Support == member) { o.Support = -1; notes?.Add(new FieldMoveNote(FieldMoveKind.SupportMoved, o.Site, member)); }
                if (o.Prefer == member) o.Prefer = -1;
                if (left && !AutoFill) { o.Solo = true; o.Duty = 0; }
            }
        }
        // A member stops waiting at a door and drops a queued bag item (every other assignment takes them off both).
        void LeaveQuiet(int member) { gathers.RemoveAll(g => g.Member == member); uses.RemoveAll(u => u.Member == member); }

        public List<FieldMoveNote> AssignObserve(int member, string id)
        {
            var notes = new List<FieldMoveNote>(); if (member < 0 || string.IsNullOrWhiteSpace(id)) return notes;
            var led = orders.Find(o => o.Lead == member);
            if (led != null) { orders.Remove(led); notes.Add(new FieldMoveNote(FieldMoveKind.LeadMoved, led.Site, member)); }
            LeaveSupport(member, notes);
            var heard = listens.Find(l => l.Member == member);
            if (heard != null) { listens.Remove(heard); notes.Add(new FieldMoveNote(FieldMoveKind.ListenMoved, -1, member, heard.Door)); }
            LeaveQuiet(member);
            observations.RemoveAll(o => o.Member == member || o.Id == id);
            observations.Add(new FieldObserve { Id = id, Member = member });
            observations.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id)); Version++;
            return notes;
        }
        // Observations are one-shot: release both the completed actions and invalidated assignments after resolving.
        public List<(string Id, int Member, FieldPause Reason)> PruneObservations(FieldPlanCheck k)
        {
            var gone = new List<(string, int, FieldPause)>();
            foreach (var o in k.Observations)
                if (observations.RemoveAll(x => x.Id == o.Id && x.Member == o.Member) > 0) gone.Add((o.Id, o.Member, FieldPause.Complete));
            foreach (var o in k.ObservePaused)
                if (observations.RemoveAll(x => x.Id == o.Id && x.Member == o.Member) > 0) gone.Add(o);
            if (gone.Count > 0) Version++;
            return gone;
        }

        // Put a member at a door (last assignment wins): they leave a search they led or supported, or another door;
        // whoever listened at this door before goes back to hushing.
        public List<FieldMoveNote> AssignListen(int member, int door)
        {
            var notes = new List<FieldMoveNote>(); if (member < 0 || door < 0) return notes;
            observations.RemoveAll(o => o.Member == member);
            var led = orders.Find(o => o.Lead == member);
            if (led != null) { orders.Remove(led); notes.Add(new FieldMoveNote(FieldMoveKind.LeadMoved, led.Site, member)); }
            LeaveSupport(member, notes);
            LeaveQuiet(member);
            var before = listens.Find(l => l.Member == member);
            if (before != null) { listens.Remove(before); if (before.Door != door) notes.Add(new FieldMoveNote(FieldMoveKind.ListenMoved, -1, member, before.Door)); }
            listens.RemoveAll(l => l.Door == door);
            listens.Add(new FieldListen { Door = door, Member = member }); listens.Sort((a, b) => a.Door.CompareTo(b.Door)); Version++;
            return notes;
        }
        // Listens that could not run this turn are released (with the reason).
        public List<(int Door, int Member, FieldPause Reason)> PruneListens(FieldPlanCheck k)
        {
            var gone = new List<(int, int, FieldPause)>();
            foreach (var p in k.ListenPaused) if (listens.RemoveAll(l => l.Door == p.Door && l.Member == p.Member) > 0) gone.Add(p);
            if (gone.Count > 0) Version++;
            return gone;
        }
        // Put a member at a move door to wait for the others (last assignment wins, in the order they came).
        public List<FieldMoveNote> AssignGather(int member, int door)
        {
            var notes = new List<FieldMoveNote>(); if (member < 0 || door < 0) return notes;
            observations.RemoveAll(o => o.Member == member);
            var led = orders.Find(o => o.Lead == member);
            if (led != null) { orders.Remove(led); notes.Add(new FieldMoveNote(FieldMoveKind.LeadMoved, led.Site, member)); }
            LeaveSupport(member, notes);
            var heard = listens.Find(l => l.Member == member);
            if (heard != null) { listens.Remove(heard); notes.Add(new FieldMoveNote(FieldMoveKind.ListenMoved, -1, member, heard.Door)); }
            uses.RemoveAll(u => u.Member == member);
            var before = gathers.Find(g => g.Member == member);
            if (before != null && before.Door == door) return notes;
            if (before != null) gathers.Remove(before);
            gathers.Add(new FieldGather { Door = door, Member = member }); Version++;
            return notes;
        }
        // Waiting at a door the party cannot go through any more (the lock's tool left, another room) goes after the turn.
        public List<(int Door, int Member, FieldPause Reason)> PruneGathers(FieldPlanCheck k)
        {
            var gone = new List<(int, int, FieldPause)>();
            foreach (var p in k.GatherPaused) if (gathers.RemoveAll(g => g.Door == p.Door && g.Member == p.Member) > 0) gone.Add(p);
            if (gone.Count > 0) Version++;
            return gone;
        }
        // The member's action for the next turn is using this item (last assignment wins).
        public List<FieldMoveNote> AssignUse(int member, string item)
        {
            var notes = new List<FieldMoveNote>(); if (member < 0 || string.IsNullOrWhiteSpace(item)) return notes;
            observations.RemoveAll(o => o.Member == member);
            var led = orders.Find(o => o.Lead == member);
            if (led != null) { orders.Remove(led); notes.Add(new FieldMoveNote(FieldMoveKind.LeadMoved, led.Site, member)); }
            LeaveSupport(member, notes);
            var heard = listens.Find(l => l.Member == member);
            if (heard != null) { listens.Remove(heard); notes.Add(new FieldMoveNote(FieldMoveKind.ListenMoved, -1, member, heard.Door)); }
            gathers.RemoveAll(g => g.Member == member);
            uses.RemoveAll(u => u.Member == member);
            uses.Add(new FieldUse { Item = item, Member = member }); Version++;
            return notes;
        }
        // Bag uses are one-shot: every use of the resolved turn goes (applied, or no longer possible).
        public List<(string Item, int Member, FieldPause Reason)> PruneUses(FieldPlanCheck k)
        {
            var gone = new List<(string, int, FieldPause)>();
            foreach (var u in k.Uses) if (uses.RemoveAll(x => x.Member == u.Member && x.Item == u.Item) > 0) gone.Add((u.Item, u.Member, FieldPause.Complete));
            foreach (var u in k.UsePaused) if (uses.RemoveAll(x => x.Member == u.Member && x.Item == u.Item) > 0) gone.Add(u);
            if (gone.Count > 0) Version++;
            return gone;
        }

        // Who goes to a door by default: the highest-numbered member who is hushing, else one listening elsewhere, else a helper,
        // else a light helper. Search leads are never taken. (The 07 panel picks the lowest free member as lead, so the two differ.)
        public static int PickListener(FieldPlanCheck k)
        {
            int Rank(int m)
            {
                var a = k.Actions[m];
                if (a == FieldAction.Hush) return 0;
                if (a == FieldAction.Listen || a == FieldAction.Paused && k.DoorOf[m] >= 0) return 1;
                if (a == FieldAction.Together || a == FieldAction.Watch) return 2;
                return a == FieldAction.Light ? 3 : -1;
            }
            int best = -1, rank = int.MaxValue;
            for (int m = k.Actions.Length - 1; m >= 0; m--) { int r = Rank(m); if (r >= 0 && r < rank) { rank = r; best = m; } }
            return best;
        }
        public bool Release(int site) { bool removed = orders.RemoveAll(o => o.Site == site) > 0; if (removed) Version++; return removed; }

        // Last assignment wins: the object's old order is replaced, the new lead leaves any other order.
        public List<FieldMoveNote> Assign(FieldOrder order)
        {
            var notes = new List<FieldMoveNote>(); if (order == null || order.Lead < 0) return notes;
            var o = order.Copy(); if (o.Support == o.Lead) o.Support = -1; if (o.Prefer == o.Lead) o.Prefer = -1;
            observations.RemoveAll(x => x.Member == o.Lead);
            var heard = listens.Find(l => l.Member == o.Lead);
            if (heard != null) { listens.Remove(heard); notes.Add(new FieldMoveNote(FieldMoveKind.ListenMoved, -1, o.Lead, heard.Door)); }
            LeaveQuiet(o.Lead);
            if (o.Prefer >= 0 && !AutoFill) LeaveQuiet(o.Prefer); // a placed helper stops waiting at a door / drops a bag item
            orders.RemoveAll(x => x.Site == o.Site);
            foreach (var x in orders.ToArray())
            {
                if (x.Lead == o.Lead) { orders.Remove(x); notes.Add(new FieldMoveNote(FieldMoveKind.LeadMoved, x.Site, o.Lead)); continue; }
                bool left = false;
                if (x.Support == o.Lead) { x.Support = -1; left = true; notes.Add(new FieldMoveNote(FieldMoveKind.SupportMoved, x.Site, o.Lead)); }
                else if (o.Prefer >= 0 && x.Support == o.Prefer) { x.Support = -1; left = true; notes.Add(new FieldMoveNote(FieldMoveKind.SupportMoved, x.Site, o.Prefer)); }
                if (x.Prefer >= 0 && (x.Prefer == o.Lead || x.Prefer == o.Prefer)) { x.Prefer = -1; left = true; }
                if (left && !AutoFill) { x.Solo = true; x.Duty = 0; }
            }
            orders.Add(o); orders.Sort((a, b) => a.Site.CompareTo(b.Site)); Version++;
            return notes;
        }

        // 말 놓기 · 빼기: take the member off whatever they do (a placed pawn dropped on the floor or right-pressed, or put somewhere else):
        // a lead's bound helper leads on alone (a search not yet started turns solo; a running one keeps its stored role, and a light
        // search without its lamp then pauses), a helper leaves a new search solo, a listener's first gatherer at that door listens
        // instead (a door that can be heard, never a lone member), a door wait or a bag item simply goes. False when they had nothing.
        public bool Unassign(int member, IFieldPlanFacts f)
        {
            if (member < 0 || f == null) return false;
            int version = Version; var k = Check(f);
            var led = SiteLedBy(member);
            if (led != null)
            {
                var r = k.RunFor(led.Site); int helper = r != null ? r.Support : -1;
                orders.Remove(led);
                if (helper >= 0 && helper != member)
                {
                    var o = led.Copy(); o.Lead = helper; o.Support = -1; o.Prefer = -1;
                    if (f.Site(led.Site).Progress == 0) { o.Solo = true; o.Duty = 0; }
                    orders.Add(o); orders.Sort((a, b) => a.Site.CompareTo(b.Site));
                }
                Version++;
            }
            foreach (var x in orders)
            {
                if (x.Lead == member) continue;
                var r = k.RunFor(x.Site); bool bound = r != null && r.Support == member;
                if (!bound && x.Support != member && x.Prefer != member) continue;
                if (x.Support == member) x.Support = -1; if (x.Prefer == member) x.Prefer = -1;
                if ((bound || !AutoFill) && f.Site(x.Site).Progress == 0) { x.Solo = true; x.Duty = 0; }
                Version++;
            }
            int door = -1; var heard = ListenBy(member); if (heard != null) { door = heard.Door; listens.Remove(heard); Version++; }
            if (observations.RemoveAll(o => o.Member == member) > 0) Version++;
            if (gathers.RemoveAll(g => g.Member == member) > 0) Version++;
            if (uses.RemoveAll(u => u.Member == member) > 0) Version++;
            if (door >= 0 && Living(f) > 1 && (!(f is IFieldListenFacts lf) || lf.ListenBlock(door) == FieldPause.None))
            {
                var next = gathers.Find(g => g.Door == door && f.Alive(g.Member));
                if (next != null) { gathers.Remove(next); listens.Add(new FieldListen { Door = door, Member = next.Member }); listens.Sort((a, b) => a.Door.CompareTo(b.Door)); Version++; }
            }
            return Version != version;
        }
        static int Living(IFieldPlanFacts f) { int n = 0; for (int m = 0; m < f.Members; m++) if (f.Alive(m)) n++; return n; }

        // Support an order needs: none, optional (함께 / 망보기) or required (조명). A running search keeps the role it started with.
        // 망보기 needs a noisy object (a silent one binds nobody: '소음 없음'). A running 함께 search binds nobody either (its saved turn is
        // already in Required), except one started under the old rules with a 함께 bonus — or, with AutoFill off (the pawn board), one
        // that started with its helper (Required below the object's own turns): the helper standing there stays co-op, not idle.
        static FieldAction Need(FieldOrder o, FieldSiteFacts s, bool keepRunningTogether, out bool required)
        {
            required = false;
            if (s.Progress == 0)
            {
                if (o.Solo) return FieldAction.Hush;
                if (o.Duty == 2) { required = true; return FieldAction.Light; }
                return o.Duty == 1 ? (s.Noise > 0 ? FieldAction.Watch : FieldAction.Hush) : FieldAction.Together;
            }
            if (s.Duty == 2) { required = true; return FieldAction.Light; }
            if (s.Duty == 1) return s.Noise > 0 ? FieldAction.Watch : FieldAction.Hush;
            return s.Bonus > 0 || keepRunningTogether && s.Required < RequiredOf(s.Turns, false) ? FieldAction.Together : FieldAction.Hush;
        }

        public FieldPlanCheck Check(IFieldPlanFacts f)
        {
            int n = Math.Max(0, f.Members);
            var k = new FieldPlanCheck { Actions = new FieldAction[n], SiteOf = new int[n], DoorOf = new int[n], GatherOf = new int[n], ObserveOf = new string[n], UseOf = new string[n] };
            int alive = 0;
            for (int m = 0; m < n; m++) { bool up = f.Alive(m); k.Actions[m] = up ? FieldAction.Hush : FieldAction.Down; k.SiteOf[m] = k.DoorOf[m] = k.GatherOf[m] = -1; if (up) alive++; }
            // A lead of any order (running or paused) is never bound as a supporter.
            var leads = new HashSet<int>(orders.Where(o => o.Lead >= 0).Select(o => o.Lead));
            // Listeners (running or paused) are never bound as supporters either; a door that cannot be listened at pauses its listener.
            var listeners = new HashSet<int>(); var lf = f as IFieldListenFacts;
            foreach (var l in listens)
            {
                int m = l.Member; if (m < 0 || m >= n || leads.Contains(m) || !listeners.Add(m)) continue;
                var why = !f.Alive(m) ? FieldPause.Downed : lf != null ? lf.ListenBlock(l.Door) : FieldPause.None;
                if (why != FieldPause.None) { k.ListenPaused.Add((l.Door, m, why)); if (why != FieldPause.Downed) { k.Actions[m] = FieldAction.Paused; k.DoorOf[m] = l.Door; } continue; }
                k.Listens.Add((l.Door, m)); k.Actions[m] = FieldAction.Listen; k.DoorOf[m] = l.Door;
            }
            // Observers, including paused observers, cannot also be picked as automatic search helpers.
            var observers = new HashSet<int>(); var of = f as IFieldObservationFacts;
            foreach (var o in observations)
            {
                int m = o.Member;
                if (m < 0 || m >= n) { k.ObservePaused.Add((o.Id, m, FieldPause.Downed)); continue; }
                if (leads.Contains(m) || listeners.Contains(m) || !observers.Add(m)) continue;
                k.ObserveOf[m] = o.Id;
                var why = !f.Alive(m) ? FieldPause.Downed : of != null ? of.ObserveBlock(o.Id) : FieldPause.OtherRoom;
                if (why != FieldPause.None) { k.ObservePaused.Add((o.Id, m, why)); if (why != FieldPause.Downed) k.Actions[m] = FieldAction.Paused; continue; }
                k.Observations.Add((o.Id, m)); k.Actions[m] = FieldAction.Observe;
            }
            // Waiting at a door: silent, never a helper. A door the party cannot go through pauses them (they stand there, hushing).
            var gatherers = new HashSet<int>(); var gf = f as IFieldGatherFacts;
            foreach (var g in gathers)
            {
                int m = g.Member; if (m < 0 || m >= n || leads.Contains(m) || listeners.Contains(m) || observers.Contains(m) || !gatherers.Add(m)) continue;
                var why = !f.Alive(m) ? FieldPause.Downed : gf != null ? gf.GatherBlock(g.Door) : FieldPause.None;
                if (why != FieldPause.None) { k.GatherPaused.Add((g.Door, m, why)); if (why != FieldPause.Downed) { k.Actions[m] = FieldAction.Paused; k.GatherOf[m] = g.Door; } continue; }
                k.Gathers.Add((g.Door, m)); k.Actions[m] = FieldAction.Gather; k.GatherOf[m] = g.Door;
            }
            // A bag item: silent, not a hush, never a helper. One that cannot happen leaves the member hushing (they are told when it goes).
            var users = new HashSet<int>(); var uf = f as IFieldUseFacts;
            foreach (var u in uses)
            {
                int m = u.Member; if (m < 0 || m >= n || leads.Contains(m) || listeners.Contains(m) || observers.Contains(m) || gatherers.Contains(m) || users.Contains(m)) continue;
                var why = !f.Alive(m) ? FieldPause.Downed : uf != null ? uf.UseBlock(m, u.Item) : FieldPause.None;
                if (why != FieldPause.None) { k.UsePaused.Add((u.Item, m, why)); continue; }
                users.Add(m); k.Uses.Add((u.Item, m)); k.Actions[m] = FieldAction.Use; k.UseOf[m] = u.Item;
            }
            var live = new List<(FieldOrder o, FieldSiteFacts s, FieldAction need, bool required)>();
            foreach (var o in orders)
            {
                var s = f.Site(o.Site); var reason = FieldPause.None;
                if (o.Lead < 0 || o.Lead >= n || !f.Alive(o.Lead)) reason = FieldPause.Downed;
                else if (s.Complete) reason = FieldPause.Complete;
                else if (!s.InRoom) reason = FieldPause.OtherRoom;
                else if (!s.Searchable) reason = FieldPause.DenClosed;
                else if (!s.Opened && !f.HasTool(o.Site, o.Lead)) reason = FieldPause.NoTool;
                if (reason != FieldPause.None) { Pause(k, o, reason); continue; }
                live.Add((o, s, Need(o, s, !AutoFill, out bool required), required));
            }
            // Binding: tapped preferences, then the supporters already kept, then (AutoFill) fill from the lowest member number
            // (required light first, so an optional helper never leaves a light search without its lamp). Never steal.
            var taken = new HashSet<int>(); var bound = new Dictionary<int, int>();
            bool Candidate(int m, int lead, FieldAction need) => m >= 0 && m < n && m != lead && f.Alive(m) && !leads.Contains(m) && !listeners.Contains(m) && !observers.Contains(m)
                && !gatherers.Contains(m) && !users.Contains(m) && !taken.Contains(m) && (need != FieldAction.Light || f.HasLight(m));
            void Bind(FieldOrder o, int m) { bound[o.Site] = m; taken.Add(m); }
            foreach (var l in live) if (l.need != FieldAction.Hush && Candidate(l.o.Prefer, l.o.Lead, l.need)) Bind(l.o, l.o.Prefer);
            foreach (var l in live) if (l.need != FieldAction.Hush && !bound.ContainsKey(l.o.Site) && Candidate(l.o.Support, l.o.Lead, l.need)) Bind(l.o, l.o.Support);
            if (AutoFill)
                foreach (var l in live.OrderBy(x => x.required ? 0 : 1))
                {
                    if (l.need == FieldAction.Hush || bound.ContainsKey(l.o.Site)) continue;
                    for (int m = 0; m < n; m++) if (Candidate(m, l.o.Lead, l.need)) { Bind(l.o, m); break; }
                }
            foreach (var l in live)
            {
                int sup = bound.TryGetValue(l.o.Site, out int b) ? b : -1;
                if (l.required && sup < 0) { Pause(k, l.o, FieldPause.NoLight); continue; }
                var s = l.s; bool starts = s.Progress == 0;
                var r = new FieldRun { Site = l.o.Site, Lead = l.o.Lead, Support = sup, Starts = starts, Role = sup >= 0 ? l.need : FieldAction.Hush };
                // A new search: pace 1 (legacy field), 함께 with a bound helper one turn sooner, 조명 +LightBonus. A running one keeps what it stored.
                r.Pace = starts ? 1 : s.Pace;
                r.Duty = starts ? (l.o.Solo ? 0 : l.o.Duty) : s.Duty;
                r.Required = starts ? RequiredOf(s.Turns, sup >= 0 && r.Duty == 0) : s.Required;
                r.Bonus = starts ? (sup >= 0 && r.Duty == 2 ? f.LightBonus : 0) : s.Bonus;
                // Older saves only: a locked 함께 bonus is lost on a turn run without a helper while someone else is alive to help.
                r.Forfeits = !starts && r.Duty == 0 && s.Bonus > 0 && sup < 0 && alive > 1;
                if (r.Forfeits) { r.Lost = r.Bonus; r.Bonus = 0; }
                r.Watched = r.Duty == 1 && sup >= 0;
                r.Noise = SearchNoise(s.Noise, r.Watched);
                r.Before = s.Progress; r.After = s.Progress + 1; r.Completes = r.After >= r.Required;
                k.Runs.Add(r); k.Noise += r.Noise;
                k.Actions[r.Lead] = FieldAction.Lead; k.SiteOf[r.Lead] = r.Site;
                if (sup >= 0) { k.Actions[sup] = l.need; k.SiteOf[sup] = r.Site; }
            }
            k.Hushing = k.Actions.Count(a => a == FieldAction.Hush || a == FieldAction.Paused);
            k.Full = alive > 0 && k.Actions.All(a => a == FieldAction.Down || a == FieldAction.Lead || a == FieldAction.Together || a == FieldAction.Watch || a == FieldAction.Light
                || a == FieldAction.Listen || a == FieldAction.Observe || a == FieldAction.Gather || a == FieldAction.Use);
            // Everyone at one door: those waiting there, plus its listener while more than one member lives (a lone listener only listens).
            foreach (int door in k.Gathers.Select(g => g.Door).Distinct())
            {
                int there = k.GatherersAt(door) + (alive > 1 && k.ListenerAt(door) >= 0 ? 1 : 0);
                if (alive > 0 && there >= alive) { k.GatheredDoor = door; break; }
            }
            return k;
        }
        static void Pause(FieldPlanCheck k, FieldOrder o, FieldPause reason)
        {
            k.Paused.Add((o.Site, o.Lead, reason));
            if (reason != FieldPause.Downed && o.Lead >= 0 && o.Lead < k.Actions.Length) { k.Actions[o.Lead] = FieldAction.Paused; k.SiteOf[o.Lead] = o.Site; }
        }

        // Whether a support role can be offered for this object (someone free would actually be bound; never 망보기 on a silent object).
        // The pace argument of the 5-argument form is legacy and ignored. CanOfferHelper: whether that member (a placed pawn) would be bound in that role.
        public bool CanOffer(int duty, int site, int lead, int pace, IFieldPlanFacts f) => CanOffer(duty, site, lead, f);
        public bool CanOffer(int duty, int site, int lead, IFieldPlanFacts f)
        {
            var d = Clone(); var standing = d.Find(site);
            d.Assign(new FieldOrder { Site = site, Lead = lead, Duty = duty, Support = standing != null ? standing.Support : -1, Prefer = standing != null ? standing.Prefer : -1 });
            var r = d.Check(f).RunFor(site); return r != null && r.Support >= 0;
        }
        public bool CanOfferHelper(int duty, int site, int lead, int helper, IFieldPlanFacts f)
        {
            if (helper < 0 || helper == lead) return false;
            var d = Clone(); d.Assign(new FieldOrder { Site = site, Lead = lead, Duty = duty, Prefer = helper });
            var r = d.Check(f).RunFor(site); return r != null && r.Support == helper;
        }

        // A saved (not yet run) plan keeps the supporters the player was shown.
        public void Keep(FieldPlanCheck k)
        {
            foreach (var o in orders) { var r = k.RunFor(o.Site); if (r != null && r.Support >= 0) o.Support = r.Support; o.Prefer = -1; }
            Version++;
        }
        // After a resolved turn: remember who supported, drop one-turn preferences.
        public void Adopt(FieldPlanCheck k)
        {
            foreach (var o in orders) { var r = k.RunFor(o.Site); if (r != null) o.Support = r.Support; o.Prefer = -1; }
            Version++;
        }
        // Completed orders and orders that could not run this turn are released (with the reason).
        public List<(int Site, FieldPause Reason)> Prune(FieldPlanCheck k)
        {
            var gone = new List<(int, FieldPause)>();
            foreach (var r in k.Runs) if (r.Completes && Release(r.Site)) gone.Add((r.Site, FieldPause.Complete));
            foreach (var p in k.Paused) if (Release(p.Site)) gone.Add((p.Site, p.Reason));
            return gone;
        }
    }
}
