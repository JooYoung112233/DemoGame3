using System;
using System.Collections.Generic;
using System.Linq;

namespace Demo5.FrontEnd
{
    // What a member does this turn on the site board. Members are indices into ExpeditionArrivalPanel.Participants (fixed for a visit).
    public enum FieldAction { Hush, Lead, Together, Watch, Light, Down, Paused, Listen, Observe }
    public enum FieldPause { None, Downed, Complete, OtherRoom, DenClosed, NoTool, NoLight, DenEmpty }
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

    // One search order: a lead, how fast, which support role (the supporter is bound when the plan is checked).
    public sealed class FieldOrder
    {
        public int Site, Lead = -1, Support = -1, Prefer = -1, Pace = 1, Duty;
        public bool Solo;
        public FieldOrder Copy() => (FieldOrder)MemberwiseClone();
        public bool SameAs(FieldOrder o) => o != null && o.Site == Site && o.Lead == Lead && o.Pace == Pace && o.Duty == Duty && o.Solo == Solo;
    }

    public struct FieldSiteFacts { public bool InRoom, Searchable, Complete, Opened; public int Progress, Required, Pace, Duty, Bonus; }
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
        public FieldAction[] Actions = new FieldAction[0];
        public int[] SiteOf = new int[0], DoorOf = new int[0];
        public string[] ObserveOf = new string[0];
        public int Noise, Hushing;
        public bool Full;
        // An observation makes no noise, but still spends an action rather than hushing.
        public bool HushedAll => Runs.Count == 0 && Listens.Count == 0 && Observations.Count == 0;
        public FieldRun RunFor(int site) => Runs.Find(r => r.Site == site);
        public FieldPause PauseFor(int site) { foreach (var p in Paused) if (p.Site == site) return p.Reason; return FieldPause.None; }
        public int ListenerAt(int door) { foreach (var l in Listens) if (l.Door == door) return l.Member; return -1; }
        public FieldPause ListenPauseFor(int member) { foreach (var l in ListenPaused) if (l.Member == member) return l.Reason; return FieldPause.None; }
        public FieldPause ObservePauseFor(int member) { foreach (var o in ObservePaused) if (o.Member == member) return o.Reason; return FieldPause.None; }
    }

    // Per-member action slots (기획/탐험-대원별행동배정-1차-구현.md): every living member has one slot per turn;
    // unassigned members hush. Check() is pure and is what the preview, the panels and the turn itself all use.
    public sealed class FieldTurnPlan
    {
        public const int TogetherBonus = 10, BaseNoise = 3, WatchRelief = 1;
        readonly List<FieldOrder> orders = new List<FieldOrder>();
        readonly List<FieldListen> listens = new List<FieldListen>();
        readonly List<FieldObserve> observations = new List<FieldObserve>();
        public IReadOnlyList<FieldOrder> Orders => orders;
        public IReadOnlyList<FieldListen> Listens => listens;
        public IReadOnlyList<FieldObserve> Observations => observations;
        public bool HasAssignments => orders.Count > 0 || listens.Count > 0 || observations.Count > 0;
        public FieldObserve ObserveAt(string id) => observations.Find(o => o.Id == id);
        public FieldObserve ObserveBy(int member) => observations.Find(o => o.Member == member);
        public FieldListen ListenAt(int door) => listens.Find(l => l.Door == door);
        public FieldListen ListenBy(int member) => listens.Find(l => l.Member == member);
        public int Version { get; private set; }

        // Noise per search turn (밸런스 1차): fast 3 (a lookout makes it 2), normal and precise 1. A lookout only helps a fast search.
        public static int NoiseOf(int pace, bool watched) => pace <= 0 ? Math.Max(1, BaseNoise - (watched ? WatchRelief : 0)) : 1;
        public FieldOrder Find(int site) => orders.Find(o => o.Site == site);
        public FieldOrder SiteLedBy(int member) => orders.Find(o => o.Lead == member);
        public FieldTurnPlan Clone() { var p = new FieldTurnPlan { Version = Version }; foreach (var o in orders) p.orders.Add(o.Copy()); foreach (var l in listens) p.listens.Add(l.Copy()); foreach (var o in observations) p.observations.Add(o.Copy()); return p; }
        public void Clear() { if (!HasAssignments) return; orders.Clear(); listens.Clear(); observations.Clear(); Version++; }
        public bool ReleaseListen(int door) { bool removed = listens.RemoveAll(l => l.Door == door) > 0; if (removed) Version++; return removed; }
        public bool ReleaseObserve(string id) { bool removed = observations.RemoveAll(o => o.Id == id) > 0; if (removed) Version++; return removed; }

        public List<FieldMoveNote> AssignObserve(int member, string id)
        {
            var notes = new List<FieldMoveNote>(); if (member < 0 || string.IsNullOrWhiteSpace(id)) return notes;
            var led = orders.Find(o => o.Lead == member);
            if (led != null) { orders.Remove(led); notes.Add(new FieldMoveNote(FieldMoveKind.LeadMoved, led.Site, member)); }
            foreach (var o in orders)
            {
                if (o.Support == member) { o.Support = -1; notes.Add(new FieldMoveNote(FieldMoveKind.SupportMoved, o.Site, member)); }
                if (o.Prefer == member) o.Prefer = -1;
            }
            var heard = listens.Find(l => l.Member == member);
            if (heard != null) { listens.Remove(heard); notes.Add(new FieldMoveNote(FieldMoveKind.ListenMoved, -1, member, heard.Door)); }
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
            foreach (var o in orders)
            {
                if (o.Support == member) { o.Support = -1; notes.Add(new FieldMoveNote(FieldMoveKind.SupportMoved, o.Site, member)); }
                if (o.Prefer == member) o.Prefer = -1;
            }
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
            orders.RemoveAll(x => x.Site == o.Site);
            foreach (var x in orders.ToArray())
            {
                if (x.Lead == o.Lead) { orders.Remove(x); notes.Add(new FieldMoveNote(FieldMoveKind.LeadMoved, x.Site, o.Lead)); continue; }
                if (x.Support == o.Lead) { x.Support = -1; notes.Add(new FieldMoveNote(FieldMoveKind.SupportMoved, x.Site, o.Lead)); }
                else if (o.Prefer >= 0 && x.Support == o.Prefer) { x.Support = -1; notes.Add(new FieldMoveNote(FieldMoveKind.SupportMoved, x.Site, o.Prefer)); }
                if (x.Prefer == o.Lead || x.Prefer == o.Prefer) x.Prefer = -1;
            }
            orders.Add(o); orders.Sort((a, b) => a.Site.CompareTo(b.Site)); Version++;
            return notes;
        }

        // Support an order needs: none, optional (함께 / 망보기) or required (조명). A running search keeps the role it started with.
        static FieldAction Need(FieldOrder o, FieldSiteFacts s, out bool required)
        {
            required = false;
            if (s.Progress == 0)
            {
                if (o.Solo) return FieldAction.Hush;
                if (o.Duty == 2) { required = true; return FieldAction.Light; }
                return o.Duty == 1 ? (o.Pace <= 0 ? FieldAction.Watch : FieldAction.Hush) : FieldAction.Together;
            }
            if (s.Duty == 2) { required = true; return FieldAction.Light; }
            if (s.Duty == 1) return s.Pace <= 0 ? FieldAction.Watch : FieldAction.Hush;
            return s.Bonus > 0 ? FieldAction.Together : FieldAction.Hush;
        }

        public FieldPlanCheck Check(IFieldPlanFacts f)
        {
            int n = Math.Max(0, f.Members); var k = new FieldPlanCheck { Actions = new FieldAction[n], SiteOf = new int[n], DoorOf = new int[n], ObserveOf = new string[n] };
            int alive = 0;
            for (int m = 0; m < n; m++) { bool up = f.Alive(m); k.Actions[m] = up ? FieldAction.Hush : FieldAction.Down; k.SiteOf[m] = k.DoorOf[m] = -1; if (up) alive++; }
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
                live.Add((o, s, Need(o, s, out bool required), required));
            }
            // Binding: tapped preferences, then the supporters already kept, then fill from the lowest member number
            // (required light first, so an optional helper never leaves a light search without its lamp). Never steal.
            var taken = new HashSet<int>(); var bound = new Dictionary<int, int>();
            bool Candidate(int m, int lead, FieldAction need) => m >= 0 && m < n && m != lead && f.Alive(m) && !leads.Contains(m) && !listeners.Contains(m) && !observers.Contains(m) && !taken.Contains(m) && (need != FieldAction.Light || f.HasLight(m));
            void Bind(FieldOrder o, int m) { bound[o.Site] = m; taken.Add(m); }
            foreach (var l in live) if (l.need != FieldAction.Hush && Candidate(l.o.Prefer, l.o.Lead, l.need)) Bind(l.o, l.o.Prefer);
            foreach (var l in live) if (l.need != FieldAction.Hush && !bound.ContainsKey(l.o.Site) && Candidate(l.o.Support, l.o.Lead, l.need)) Bind(l.o, l.o.Support);
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
                r.Pace = starts ? Math.Max(0, Math.Min(2, l.o.Pace)) : s.Pace;
                r.Duty = starts ? (l.o.Solo ? 0 : l.o.Duty) : s.Duty;
                r.Required = starts ? r.Pace + 1 : s.Required;
                r.Bonus = starts ? (sup >= 0 && r.Duty == 2 ? f.LightBonus : sup >= 0 && r.Duty == 0 ? TogetherBonus : 0) : s.Bonus;
                // A locked 함께 bonus is lost on a turn run without a helper while someone else is alive to help.
                r.Forfeits = !starts && r.Duty == 0 && s.Bonus > 0 && sup < 0 && alive > 1;
                if (r.Forfeits) { r.Lost = r.Bonus; r.Bonus = 0; }
                r.Watched = r.Duty == 1 && sup >= 0;
                r.Noise = NoiseOf(r.Pace, r.Watched);
                r.Before = s.Progress; r.After = s.Progress + 1; r.Completes = r.After >= r.Required;
                k.Runs.Add(r); k.Noise += r.Noise;
                k.Actions[r.Lead] = FieldAction.Lead; k.SiteOf[r.Lead] = r.Site;
                if (sup >= 0) { k.Actions[sup] = l.need; k.SiteOf[sup] = r.Site; }
            }
            k.Hushing = k.Actions.Count(a => a == FieldAction.Hush || a == FieldAction.Paused);
            k.Full = alive > 0 && k.Actions.All(a => a == FieldAction.Down || a == FieldAction.Lead || a == FieldAction.Together || a == FieldAction.Watch || a == FieldAction.Light || a == FieldAction.Listen || a == FieldAction.Observe);
            return k;
        }
        static void Pause(FieldPlanCheck k, FieldOrder o, FieldPause reason)
        {
            k.Paused.Add((o.Site, o.Lead, reason));
            if (reason != FieldPause.Downed && o.Lead >= 0 && o.Lead < k.Actions.Length) { k.Actions[o.Lead] = FieldAction.Paused; k.SiteOf[o.Lead] = o.Site; }
        }

        // Whether a support role can be offered for this object (someone free would actually be bound).
        public bool CanOffer(int duty, int site, int lead, int pace, IFieldPlanFacts f)
        {
            var d = Clone(); var standing = d.Find(site);
            d.Assign(new FieldOrder { Site = site, Lead = lead, Pace = pace, Duty = duty, Support = standing != null ? standing.Support : -1, Prefer = standing != null ? standing.Prefer : -1 });
            var r = d.Check(f).RunFor(site); return r != null && r.Support >= 0;
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
