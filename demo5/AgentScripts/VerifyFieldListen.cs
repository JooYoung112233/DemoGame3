using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 문에 귀 대기 1차: pure rules (edit mode) and real screens (play mode, after VerifyFieldListen.Enter or VerifyFieldTurnPlan.Enter).
// 말 놓기 (2026-09-25, 기획/탐험-말놓기-조작-재설계.md): listening is a member's pawn placed at a door (FieldPawnTest: the board's own
// rules); the door popup has no listen button, the heard lines are the door's log (right press) and its markers; the first visit has no
// listening (doors only gather); a hush turn is '턴 진행' with nobody placed.
public static class VerifyFieldListen
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    const int A = FieldSiteState.Arcade, C = FieldSiteState.Corridor, S = FieldSiteState.Storage, D = FieldSiteState.Den, N = FieldSiteState.Nowhere;

    sealed class Fake : IFieldPlanFacts, IFieldListenFacts
    {
        public bool[] Up, Lamp; public int Room = A; public bool DenEmpty, StorageOpen = true;
        public readonly Dictionary<int, FieldSiteFacts> Sites = new Dictionary<int, FieldSiteFacts>();
        public Fake(int n) { Up = Enumerable.Repeat(true, n).ToArray(); Lamp = new bool[n]; }
        public int Members => Up.Length;
        public int LightBonus => 20;
        public bool Alive(int m) => m >= 0 && m < Up.Length && Up[m];
        public bool HasLight(int m) => Alive(m) && Lamp[m];
        public bool HasTool(int site, int m) => true;
        public FieldSiteFacts Site(int site) => Sites.TryGetValue(site, out var f) ? f : new FieldSiteFacts { InRoom = true, Searchable = true };
        public FieldPause ListenBlock(int door) => !FieldSiteState.Adjacent(Room, door) || door == S && !StorageOpen ? FieldPause.OtherRoom : door == D && DenEmpty ? FieldPause.DenEmpty : FieldPause.None;
    }
    static FieldOrder O(int site, int lead, int pace = 1, int duty = 0, bool solo = false, int prefer = -1) => new FieldOrder { Site = site, Lead = lead, Pace = pace, Duty = duty, Solo = solo, Prefer = prefer };
    static string Snap(FieldTurnPlan p) => p.Version + ":" + string.Join("|", p.Orders.Select(o => $"{o.Site},{o.Lead},{o.Support},{o.Prefer},{o.Pace},{o.Duty},{o.Solo}")) + "/" + string.Join("|", p.Listens.Select(l => l.Door + "," + l.Member));
    static string Snap(FieldSiteState s) => $"{s.PartyRoom},{s.TurnsUsed},{s.Gauge},{s.Danger},{s.Floor},{s.Remembered},{s.ResidentRoom},{s.Resident},{s.Next},{s.Moved},{s.MovedFrom},{s.PassedBy},{s.Encounter},{s.Surprise},{s.Noticed},{s.Noted},{s.LastNoise}";
    static FieldSiteState Site(int danger = 0, int gauge = 0, int remembered = N, int clock = 0) => new FieldSiteState(new FieldSiteRules(), () => 0, false, danger, gauge, remembered, clock);

    public static string Rules()
    {
        var done = new List<string>();
        // §1 One slot, silent, not hushing.
        var f = new Fake(2); var plan = new FieldTurnPlan(); plan.AssignListen(1, C); var k = plan.Check(f);
        Check(k.Actions[1] == FieldAction.Listen && k.DoorOf[1] == C && k.Noise == 0 && k.Hushing == 1 && !k.HushedAll && !k.Full && k.ListenerAt(C) == 1, "Listen slot");
        plan.Assign(O(1, 0, solo: true)); k = plan.Check(f);
        Check(k.Full && k.Runs.Count == 1 && k.RunFor(1).Support < 0 && k.Noise == FieldTurnPlan.SearchNoise(f.Site(1).Noise, false), "Search + listen = full");
        var one = new Fake(1); plan = new FieldTurnPlan(); plan.AssignListen(0, C); Check(plan.Check(one).Full, "A lone listener is full");
        var s = Site(gauge: 2); var h = s.Copy(); s.EndTurn(0, false); h.EndTurn(0, true); Check(s.Gauge == 2 && h.Gauge == 1, "Listening gives no gauge relief");
        s = Site(danger: 2, remembered: A); s.EndTurn(0, false); s.EndTurn(0, false); Check(s.Incoming, "Incoming");
        var listenOnly = s.Copy(); listenOnly.EndTurn(0, false); var hushOnly = s.Copy(); hushOnly.EndTurn(0, true);
        Check(listenOnly.Encounter && !hushOnly.Encounter && hushOnly.PassedBy, "Listening breaks the pass-by");
        done.Add("slot/silent/not hushing");

        // §2 Last assignment wins.
        f = new Fake(2); plan = new FieldTurnPlan(); plan.Assign(O(1, 0)); plan.Keep(plan.Check(f)); int v = plan.Version;
        var notes = plan.AssignListen(1, C); Check(notes.Any(n => n.Kind == FieldMoveKind.SupportMoved && n.Site == 1) && plan.Find(1).Support == -1 && plan.Version == v + 1, "Supporter to the door");
        notes = plan.AssignListen(0, C); Check(notes.Any(n => n.Kind == FieldMoveKind.LeadMoved && n.Site == 1) && plan.Find(1) == null && plan.ListenAt(C).Member == 0 && plan.ListenBy(1) == null, "Lead to the door replaces the old listener");
        f.Room = C; plan = new FieldTurnPlan(); plan.AssignListen(1, A); notes = plan.AssignListen(1, D);
        Check(notes.Count == 1 && notes[0].Kind == FieldMoveKind.ListenMoved && notes[0].Site == -1 && notes[0].Door == A && plan.Listens.Count == 1, "Door to door");
        notes = plan.Assign(O(5, 1)); Check(notes.Any(n => n.Kind == FieldMoveKind.ListenMoved && n.Door == D) && plan.Listens.Count == 0, "Listener made a lead");
        plan = new FieldTurnPlan(); plan.AssignListen(1, A); plan.Assign(O(5, 0, prefer: 1)); k = plan.Check(f);
        Check(plan.Listens.Count == 1 && k.RunFor(5).Support < 0 && k.Actions[1] == FieldAction.Listen, "A preference never pulls a listener");
        var copy = plan.Clone(); copy.ReleaseListen(A); Check(plan.Listens.Count == 1 && copy.Listens.Count == 0, "Clone is separate");
        v = plan.Version; plan.ReleaseListen(A); Check(plan.Version == v + 1 && !plan.ReleaseListen(A) && plan.Version == v + 1, "Release once");
        v = plan.Version; plan.Clear(); Check(plan.Version == v + 1 && plan.Orders.Count == 0, "Clear");
        done.Add("last-wins");

        // §3 Supporters: a listener is never bound.
        f = new Fake(2); plan = new FieldTurnPlan(); plan.AssignListen(1, C);
        Check(!plan.CanOffer(0, 1, 0, 1, f) && !plan.CanOffer(1, 1, 0, 1, f) && !plan.CanOffer(2, 1, 0, 1, f), "No helper while the other listens");
        f = new Fake(3); plan = new FieldTurnPlan(); plan.AssignListen(1, C); plan.Assign(O(1, 0)); Check(plan.Check(f).RunFor(1).Support == 2, "Third member helps");
        f = new Fake(2); f.Lamp[1] = true; plan = new FieldTurnPlan(); plan.AssignListen(1, C); plan.Assign(O(1, 0, duty: 2)); Check(plan.Check(f).PauseFor(1) == FieldPause.NoLight, "Lamp at the door");
        f.Sites[1] = new FieldSiteFacts { InRoom = true, Searchable = true, Opened = true, Progress = 1, Required = 2, Pace = 1, Duty = 0, Bonus = 10 };
        plan = new FieldTurnPlan(); plan.Assign(O(1, 0)); plan.AssignListen(1, C); Check(plan.Check(f).RunFor(1).Forfeits, "Forfeit when the helper listens");
        done.Add("supporters");

        // §4 Blocked doors, downed listeners, pruning, purity.
        f = new Fake(2) { Room = C, DenEmpty = true }; plan = new FieldTurnPlan(); plan.AssignListen(1, D); k = plan.Check(f);
        Check(k.ListenPaused.Count == 1 && k.ListenPaused[0].Reason == FieldPause.DenEmpty && k.Actions[1] == FieldAction.Paused && k.DoorOf[1] == D && k.HushedAll, "Den empty pauses");
        f = new Fake(2) { Room = C, StorageOpen = false }; plan = new FieldTurnPlan(); plan.AssignListen(1, S); Check(plan.Check(f).ListenPauseFor(1) == FieldPause.OtherRoom, "Locked storage");
        f = new Fake(2) { Room = A }; plan = new FieldTurnPlan(); plan.AssignListen(1, D); Check(plan.Check(f).ListenPauseFor(1) == FieldPause.OtherRoom, "Not a door of this room");
        f = new Fake(2); f.Up[1] = false; plan = new FieldTurnPlan(); plan.AssignListen(1, C); k = plan.Check(f); Check(k.Actions[1] == FieldAction.Down && k.ListenPauseFor(1) == FieldPause.Downed, "Downed");
        var gone = plan.PruneListens(k); Check(gone.Count == 1 && plan.Listens.Count == 0, "Prune");
        f = new Fake(3); plan = new FieldTurnPlan(); plan.AssignListen(2, C); plan.Assign(O(1, 0)); string before = Snap(plan); plan.Check(f); plan.Check(f); Check(Snap(plan) == before, "Check is pure");
        done.Add("blocked/pruned/pure");

        // §5 Default listener.
        FieldPlanCheck K(params FieldAction[] a) => new FieldPlanCheck { Actions = a, DoorOf = a.Select(x => x == FieldAction.Listen ? C : -1).ToArray(), SiteOf = new int[a.Length] };
        Check(FieldTurnPlan.PickListener(K(FieldAction.Hush, FieldAction.Hush)) == 1 && FieldTurnPlan.PickListener(K(FieldAction.Lead, FieldAction.Hush)) == 1, "Pick hush, highest");
        Check(FieldTurnPlan.PickListener(K(FieldAction.Lead, FieldAction.Together)) == 1 && FieldTurnPlan.PickListener(K(FieldAction.Hush, FieldAction.Watch, FieldAction.Light)) == 0, "Pick order");
        Check(FieldTurnPlan.PickListener(K(FieldAction.Lead, FieldAction.Listen)) == 1 && FieldTurnPlan.PickListener(K(FieldAction.Lead, FieldAction.Lead)) == -1 && FieldTurnPlan.PickListener(K(FieldAction.Paused, FieldAction.Hush)) == 1, "Pick edge");
        done.Add("default listener");

        // §6 What a door tells.
        s = Site(danger: 2, remembered: A); var r = s.ListenAt(C); Check(!r.Present && !r.Left && r.Next == N && r.Then == N, "(a) nothing yet");
        s.EndTurn(0, true); r = s.ListenAt(C); Check(r.Present && r.Resident == ResidentState.Out && r.Next == C && r.Then == A && r.ThenIn && !r.NextIn, "(a) out, pausing, then here: " + r);
        s.EndTurn(0, true); r = s.ListenAt(C); Check(r.NextIn && r.Then == N && s.Incoming, "(a) next here");
        s = Site(danger: 2); s.MoveParty(C); r = s.ListenAt(D); Check(r.Present && r.Resident == ResidentState.Den && r.Next == D && r.Then == D, "(b) home: " + r);
        s.Driven(); Check(!s.ListenAt(D).Present && !s.ListenAt(A).Present, "(d) gone");
        s = Site(danger: 2); Check(!s.ListenAt(D).Present && s.ListenAt(D).Next == N, "(f) not a door of the room");
        s = Site(danger: 2, remembered: C); s.EndTurn(0, true); r = s.ListenAt(C); Check(r.Present && r.Resident == ResidentState.Staying && r.Next == C && r.Then == C, "(e) staying: " + r);
        s = Site(danger: 2, remembered: A); s.MoveParty(S); for (int i = 0; i < 3; i++) s.EndTurn(0, true); r = s.ListenAt(C); Check(!r.Present && r.Left, "(g) just left: " + r + " / " + Snap(s));
        before = Snap(s); s.ListenAt(C); s.ListenAt(A); Check(Snap(s) == before, "(h) listening changes nothing");
        done.Add("reports");

        // §7 Fuzz (40,000 states, each report kind at least 150 times): a report never lies. Stay: a present thing not coming in goes exactly to Next; quiet: then Then; ThenIn: here.
        //    Move in: when present, it is met exactly when Next is that room or ours.
        var rng = new System.Random(29); var seen = new Dictionary<string, int>();
        void Saw(string key) => seen[key] = seen.TryGetValue(key, out int c0) ? c0 + 1 : 1;
        for (int t = 0; t < 40000; t++)
        {
            s = Site(rng.Next(0, 3), rng.Next(0, 4), rng.Next(-1, 3), rng.Next(0, 24)); int room = A; s.MoveParty(room);
            for (int j = rng.Next(0, 8); j > 0; j--)
            {
                if (rng.Next(3) == 0) { var near = new[] { A, C, S }.Where(x => FieldSiteState.Adjacent(room, x)).ToArray(); if (near.Length > 0) { room = near[rng.Next(near.Length)]; s.MoveParty(room); } }
                s.EndTurn(rng.Next(0, 5), rng.Next(3) == 0);
                if (s.Encounter) { if (rng.Next(2) == 0) s.Hidden(); else s.Driven(); }
            }
            foreach (int door in new[] { A, C, S, D }.Where(x => FieldSiteState.Adjacent(room, x)))
            {
                before = Snap(s); r = s.ListenAt(door); Check(Snap(s) == before, "Fuzz: mutated");
                if (!r.Present) { Check(r.Next == N && r.Then == N, "Fuzz: absent has no steps"); Saw(r.Left ? "left" : "absent"); continue; }
                Check(r.Next == s.Next && r.NextIn == s.Incoming && (r.Then == N) == r.NextIn, "Fuzz: present " + r);
                Saw(r.NextIn ? "nextIn" : r.ThenIn ? "thenIn" : r.Next == door ? (r.Resident == ResidentState.Out ? "stop" : "stay") : "leave");
                int noise = rng.Next(0, 6); var stay = s.Copy(); stay.EndTurn(noise, rng.Next(2) == 0);
                if (!r.NextIn) Check(!stay.Encounter && stay.ResidentRoom == r.Next, "Fuzz: goes to Next " + r);
                if (!r.NextIn && noise == 0) Check(stay.Next == r.Then, "Fuzz: quiet then " + r + " got " + stay.Next);
                if (r.ThenIn) Check(stay.Next == room, "Fuzz: then in, noise " + noise);
                if (door != D) // the party never walks into the den
                {
                    var walk = s.Copy(); walk.MoveParty(door); walk.EndTurn(rng.Next(0, 6), false);
                    Check(walk.Encounter == (r.Next == door || r.Next == room), "Fuzz: walk in " + r + " enc " + walk.Encounter);
                    if (!walk.Encounter) Check(walk.ResidentRoom == r.Next, "Fuzz: walk in, it went on");
                }
            }
        }
        foreach (var key in new[] { "absent", "left", "stop", "stay", "leave", "thenIn", "nextIn" }) Check(seen.TryGetValue(key, out int c1) && c1 >= 150, "Fuzz coverage " + key + " " + (seen.TryGetValue(key, out int c2) ? c2 : 0));
        done.Add("fuzz 40000 (" + string.Join(" ", seen.OrderBy(x => x.Key).Select(x => x.Key + " " + x.Value)) + ")");

        // §8 Plans with listeners: one role each; listeners are neither leads nor helpers; hushed only when nobody searched or listened.
        for (int t = 0; t < 2000; t++)
        {
            int n = rng.Next(1, 5); f = new Fake(n) { Room = new[] { A, C, S }[rng.Next(3)], DenEmpty = rng.Next(3) == 0, StorageOpen = rng.Next(2) == 0 };
            for (int m = 0; m < n; m++) { f.Up[m] = rng.Next(6) > 0; f.Lamp[m] = rng.Next(3) == 0; }
            plan = new FieldTurnPlan();
            for (int j = rng.Next(0, 6); j > 0; j--)
                if (rng.Next(2) == 0) plan.AssignListen(rng.Next(n), new[] { A, C, S, D }[rng.Next(4)]); else plan.Assign(O(rng.Next(5), rng.Next(n), rng.Next(3), rng.Next(3), rng.Next(3) == 0, rng.Next(-1, n)));
            before = Snap(plan); k = plan.Check(f); Check(Snap(plan) == before, "Plan fuzz: mutated");
            var leads = k.Runs.Select(x => x.Lead).ToList(); var helpers = k.Runs.Where(x => x.Support >= 0).Select(x => x.Support).ToList(); var ears = k.Listens.Select(x => x.Member).ToList();
            Check(leads.Concat(helpers).Concat(ears).Distinct().Count() == leads.Count + helpers.Count + ears.Count, "Plan fuzz: one role each " + t);
            Check(k.HushedAll == (k.Runs.Count == 0 && k.Listens.Count == 0) && k.Noise == k.Runs.Sum(x => x.Noise), "Plan fuzz: totals " + t);
            Check(k.Listens.All(l => f.ListenBlock(l.Door) == FieldPause.None && f.Alive(l.Member)) && k.Listens.Select(l => l.Door).Distinct().Count() == k.Listens.Count, "Plan fuzz: doors " + t);
            Check(k.Full == (k.Actions.Any(a => a != FieldAction.Down) && k.Actions.All(a => a == FieldAction.Down || a == FieldAction.Lead || a == FieldAction.Together || a == FieldAction.Watch || a == FieldAction.Light || a == FieldAction.Listen)), "Plan fuzz: full " + t);
        }
        done.Add("plan fuzz 2000");
        return "PASS: " + string.Join("; ", done);
    }

    // ---- Play mode ----
    static async Task Tap(Button button)
    {
        for (int i = 0; i < 20 && button && !(button.IsActive() && button.IsInteractable()); i++) await Task.Delay(50); // enabled on the next LateUpdate
        Check(button && button.IsActive() && button.IsInteractable(), "Unavailable " + (button ? button.name : "null")); Hit(button);
        var data = new PointerEventData(EventSystem.current) { position = ScreenPoint((RectTransform)button.transform), button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler); await Task.Delay(110);
    }
    static Vector2 ScreenPoint(RectTransform r) { Canvas.ForceUpdateCanvases(); return RectTransformUtility.WorldToScreenPoint(r.GetComponentInParent<Canvas>().worldCamera, r.TransformPoint(r.rect.center)); }
    static void Hit(Selectable target)
    {
        var data = new PointerEventData(EventSystem.current) { position = ScreenPoint((RectTransform)target.transform) }; var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Selectable>() == target, "Blocked " + target.name + " by " + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
    }
    static async Task Until(Func<bool> done, int ms, string what) { var w = Stopwatch.StartNew(); while (!done()) { if (w.ElapsedMilliseconds > ms) throw new Exception("Timeout: " + what); await Task.Delay(30); } }
    static string Shots => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "FieldListenCapture"));
    static Task Still(MonoBehaviour host, string name) { var done = new TaskCompletionSource<bool>(); host.StartCoroutine(StillRoutine(name, done)); return done.Task; }
    static IEnumerator StillRoutine(string name, TaskCompletionSource<bool> done)
    {
        yield return new WaitForEndOfFrame();
        var raw = new RenderTexture(Screen.width, Screen.height, 0); var rt = new RenderTexture(raw.width, raw.height, 0); ScreenCapture.CaptureScreenshotIntoRenderTexture(raw);
        if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(raw, rt, new Vector2(1, -1), new Vector2(0, 1)); else Graphics.Blit(raw, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new UnityEngine.Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); RenderTexture.active = prev; Object.Destroy(raw); Object.Destroy(rt);
        Directory.CreateDirectory(Shots); File.WriteAllBytes(Path.Combine(Shots, name + ".png"), tex.EncodeToPNG()); Object.Destroy(tex); done.SetResult(true);
    }
    // Rect in canvas space (1920×1080, y from the top).
    static Rect OnScreen(RectTransform r)
    {
        var canvas = (RectTransform)r.GetComponentInParent<Canvas>().rootCanvas.transform; var c = new Vector3[4]; r.GetWorldCorners(c);
        Vector3 lo = canvas.InverseTransformPoint(c[0]), hi = canvas.InverseTransformPoint(c[2]);
        return new Rect(lo.x - canvas.rect.xMin, canvas.rect.yMax - hi.y, hi.x - lo.x, hi.y - lo.y);
    }
    static void Near(Rect r, float x, float y, float w, float h, string what) => Check(Mathf.Abs(r.x - x) < 2 && Mathf.Abs(r.y - y) < 2 && Mathf.Abs(r.width - w) < 2 && Mathf.Abs(r.height - h) < 2, what + " rect " + r);
    static void Fits(params Text[] texts) { Canvas.ForceUpdateCanvases(); foreach (var t in texts) if (t && t.isActiveAndEnabled) Check(t.preferredHeight <= t.rectTransform.rect.height + 1 && (t.horizontalOverflow == HorizontalWrapMode.Wrap || t.preferredWidth <= t.rectTransform.rect.width + 1), "Overflow " + t.name + ": " + t.text); }
    static string Label(Button b) => b.GetComponentInChildren<Text>().text;
    static FieldThreatMarker Marker(ExpeditionSiteThreat t, int room, int from) => t.DoorMarkers.First(m => m && m.Room == room && m.From == from);

    public static async Task<string> Enter()
    {
        Check(Application.isPlaying, "Play first"); UnityEngine.SceneManagement.SceneManager.LoadScene("PartySelection"); await Task.Delay(800);
        var p = Object.FindAnyObjectByType<PartySelectionController>(); if (PartySelectionSession.Selected.Count != 2) { PartySelectionSession.Clear(); p.Refresh(); } await Task.Delay(100);
        foreach (var card in p.Cards.Where(x => x.gameObject.activeInHierarchy && x.Button.IsInteractable())) { if (PartySelectionSession.Selected.Count >= 2) break; await Tap(card.Button); }
        await Tap(p.Continue); await Task.Delay(800);
        var h = Object.FindAnyObjectByType<HomeSelectionController>(); await Tap(h.Cards[0].Button); await Tap(h.Continue); await Task.Delay(900);
        var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Settlement missing");
        if (c.Introduction) c.Introduction.Restore(10);
        // The tutorial story beats have their own test (VerifyTutorialStory): mark them seen so they never cover this fixture.
        var story = c.GetComponent<SettlementTutorialNarrative>(); if (story) story.Restore(new SavedTutorialNarrative { SeenMask = SavedTutorialNarrative.AllSeen, PendingBeat = -1 });
        await Task.Delay(300);
        return "Settlement " + c.Campaign.Home.Name + " · " + string.Join(", ", c.Campaign.Party.Select(x => x.Name));
    }
    static async Task<ExpeditionArrivalPanel> Depart(SettlementController c)
    {
        await Tap(c.Exit); foreach (var card in c.ExpeditionPanel.Cards) if (!card.Check.gameObject.activeSelf) await Tap(card.Button);
        await Tap(c.ExpeditionPanel.Pack); await Tap(c.PackingPanel.Ready); await Tap(c.PackingPanel.Depart); await Task.Delay(1100);
        Check(c.ArrivalPanel.IsOpen && !c.ArrivalPanel.InTransit, "Arrived"); return c.ArrivalPanel;
    }
    static async Task CloseLoot(ExpeditionArrivalPanel a) { if (a.Loot.IsOpen) { await Tap(a.Loot.Back); await Task.Delay(120); if (a.Loot.LeaveReview.activeSelf) await Tap(a.Loot.LeaveConfirm); await Task.Delay(150); } }
    static async Task Ready(ExpeditionArrivalPanel a) => await Until(() => FieldPawnTest.Ready(a), 3000, "board ready");
    // One search turn on the object: member 0's pawn leads it (solo) or two pawns (co-op), then '턴 진행' (no pace since 2026-09-25).
    static async Task Search(ExpeditionArrivalPanel a, int site, bool solo)
    {
        await Ready(a);
        if (FieldPawnTest.Check(a)?.RunFor(site) == null) Check(solo ? FieldPawnTest.Lead(a, 0, site) : FieldPawnTest.Coop(a, site), "Pawns on " + a.ObjectNames[site] + ": " + FieldPawnTest.Describe(a));
        await Tap(a.Threat.Planner.TurnButton); await Task.Delay(350);
        await CloseLoot(a); if (a.Search.IsOpen) await Tap(a.Search.Back); await Task.Delay(150);
    }
    // The hush turn: nobody placed, '턴 진행' (AutoAccept: no idle question).
    static async Task Hush(ExpeditionArrivalPanel a) { await Ready(a); FieldPawnTest.Clear(a); await Tap(a.Threat.Planner.TurnButton); await Task.Delay(300); }
    // Every living pawn at the door onto `next`, '턴 진행', the walk.
    static async Task Move(ExpeditionArrivalPanel a, int next) { await Ready(a); Check(FieldPawnTest.Gather(a, next), "Everyone at the door: " + FieldPawnTest.Describe(a)); await Tap(a.Threat.Planner.TurnButton); await Until(() => a.Encounter.IsOpen || !a.InTransit && a.Rooms.CurrentRoom == next, 6000, "move"); await Task.Delay(400); }
    // A right press on a door hotspot: its heard log (FieldPawnBoard.RightPress), never the move popup.
    static async Task DoorLog(ExpeditionArrivalPanel a, Button door)
    {
        var data = new PointerEventData(EventSystem.current) { position = ScreenPoint((RectTransform)door.transform), button = PointerEventData.InputButton.Right };
        ExecuteEvents.Execute(door.gameObject, data, ExecuteEvents.pointerClickHandler); await Task.Delay(200); Check(a.Popup.activeSelf && !a.ReturnConfirm.gameObject.activeSelf, "Door log popup (no confirm)");
    }

    // First visit (it sleeps): no listening anywhere (doors only gather), the door log says so, the den door offers nothing.
    public static async Task<string> FirstVisitGuard()
    {
        var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Run Enter first");
        var a = await Depart(c); var t = a.Threat; var pl = t.Planner; Check(!pl.Active && pl.Placing, "Asleep, pawns placed"); await Ready(a);
        foreach (int m in FieldPawnTest.Living(a))
        {
            var door = FieldPawnTest.Options(a, m).Where(o => o.Door >= 0).ToList();
            Check(door.Count == 1 && door[0].Kind == FieldSpotKind.Gather && door[0].Door == C, "Member " + m + ": the exit only gathers (" + string.Join(",", door.Select(o => o.Kind)) + ")");
        }
        var b = FieldPawnTest.Board(a); await DoorLog(a, a.Objects[3]);
        Check(a.PopupBody.text.Contains(b.Texts.DoorLogAsleep.Split('\n')[0]), "Door log on the first visit: " + a.PopupBody.text);
        await Still(a, "50-first-visit-door"); a.ClosePopup(); await Task.Delay(100);
        Check(pl.Plan.Listens.Count == 0 && pl.Reports.Count == 0 && pl.DoorLog.Count == 0 && t.DoorMarkers.All(m => !m.gameObject.activeSelf || m.Label.text != t.ListenPending), "No listening");
        await Move(a, C);
        Check(FieldPawnTest.Options(a, 0).All(o => o.Door != D && o.Key != FieldPawnTest.SearchKey(t.DenSite)), "Sleeping den: no listening, no shelf");
        return "PASS first visit: doors only gather, the door log says it sleeps, no listening, nothing at the den";
    }

    public static async Task<string> Showcase()
    {
        FieldIdleConfirm.AutoAccept = true; // members left idle hush without the question (VerifyIdleConfirm tests it)
        var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Run Enter first");
        var a = await Depart(c); var t = a.Threat; var pl = t.Planner; var tx = pl.Texts; var px = pl.PlaceTexts; var log = new List<string>();
        if (c.Opening) c.Opening.State.Enabled = false; // review fixture: a later visit comes after the opening chapter
        t.ReviewWake(1, 2, 6); await Task.Delay(250); Check(pl.Active, "Awake"); await Ready(a);
        string B = a.Participants[1].Name; var exit = Marker(t, A, C);
        // S1 the exit offers B's pawn a listening place (the first pawn at an empty door).
        var offer = FieldPawnTest.Option(a, 1, FieldPawnTest.DoorKey(C), FieldSpotKind.Listen);
        Check(offer != null && offer.Enabled && offer.Label == px.Listen && offer.Glyph == ActionGlyph.Kind.Listen, "Listen place at the exit: " + (offer != null ? offer.Label : "none"));
        // S2 place: no time.
        int turns = a.Rooms.Turns, clock = t.State.TurnsUsed, minute = c.Campaign.MinuteOfDay;
        Check(FieldPawnTest.Listen(a, 1, C), "B's pawn at the exit"); await Task.Delay(150);
        Check(!a.Popup.activeSelf && a.Rooms.Turns == turns && t.State.TurnsUsed == clock && c.Campaign.MinuteOfDay == minute, "Placing is free");
        Check(a.Cards[1].Action.text == tx.TagListen && a.Cards[1].Role.text == string.Format(tx.RoleListen, "복도") && exit.Label.text == t.ListenPending && pl.Chip.text.Contains(string.Format(tx.ChipListening, 1)), "Listening shown: " + exit.Label.text + " / " + pl.Chip.text);
        await Still(a, "52-listening-set");
        // S3 A searches 오락기 뒤판 (noise 3, prybar fixture) while B listens: everyone has a job, one loud turn.
        if (c.InventoryPanel.CountFor(a.Participants[0], "prybar") == 0) Check(c.InventoryPanel.TransferField(a.Participants[0], "prybar", 1, true), "Prybar fixture");
        // Fixture: the machine done in one turn (a single loud turn, as the old one-turn lure), so the listen turn below is quiet.
        int machineTurns = a.Loot.Sites[2].Turns; a.Loot.Sites[2].Turns = 1;
        Check(FieldPawnTest.Lead(a, 0, 2) && FieldIdleConfirm.IdleMembers(pl).Count == 0, "A on the machine: nobody idle");
        Check(FieldPawnTest.Detail(a, 2) && a.Search.PlacedLine.Contains(a.Participants[0].Name), "07 (read only) names A: " + a.Search.PlacedLine);
        await Still(a, "53-search-with-listener"); await Tap(a.Search.Back);
        await Tap(pl.TurnButton); await Task.Delay(350); await CloseLoot(a); if (a.Search.IsOpen) await Tap(a.Search.Back); a.Loot.Sites[2].Turns = machineTurns;
        var st = t.State; Check(a.Rooms.Turns == turns + 1 && st.Danger == 2 && st.Gauge == 1 && st.Remembered == A && st.Next == C && pl.LastMismatch == "" && pl.Plan.Listens.Count == 1, "After the loud turn " + st.Danger + "/" + st.Gauge + " " + pl.LastMismatch);
        Check(pl.TryReport(C, out var r, out _) && !r.Present && exit.Label.text == t.HeardQuiet, "Heard nothing yet: " + exit.Label.text);
        log.Add("placing free, everyone busy, quiet");
        // S4 listen turn: it comes out into the corridor and will stop; the step after is into our room.
        var e = st.Copy(); e.MoveParty(A); e.EndTurn(0, false); var want = e.ListenAt(C);
        await Ready(a); await Tap(pl.TurnButton); await Task.Delay(300);
        Check(pl.TryReport(C, out r, out int who) && r.Equals(want) && who == 1 && r.Present && r.Next == C && r.ThenIn && st.Gauge == 1 && pl.LastMismatch == "", "Heard: " + r + " want " + want);
        Check(exit.Label.text == t.HeardSoon && exit.Paper.color == exit.SoonPaper && a.Status.text == string.Format(t.StatusHeard, "복도", t.NextStop) + "\n" + t.StatusHeardHere && pl.Chip.text.Contains(tx.ChipSoon), "Soon: " + exit.Label.text + " / " + a.Status.text + " / " + pl.Chip.text);
        Fits(a.Status, exit.Label);
        await Still(a, "54-heard-soon");
        // S5 the door's log (right press) keeps both turns, newest first; looking is free.
        var entries = pl.DoorLog.For(A, C); Check(entries.Count == 2 && entries[0].Member == 1 && pl.DoorLog.Line(entries[0], st.TurnsUsed).StartsWith(pl.DoorLog.AgeNow), "Log: " + string.Join(" / ", entries.Select(x => pl.DoorLog.Line(x, st.TurnsUsed))));
        turns = a.Rooms.Turns; await DoorLog(a, a.Objects[3]);
        Check(a.PopupBody.text.Contains(pl.DoorLog.Line(entries[0], st.TurnsUsed)) && a.PopupBody.text.Contains(B), "Log popup: " + a.PopupBody.text);
        Fits(a.PopupBody); await Still(a, "55-door-log"); a.ClosePopup(); Check(a.Rooms.Turns == turns, "Looking is free");
        // S6/S7 hush twice (B's pawn taken off: nobody placed): the red one-turn warning, then it passes by. The log stays.
        Check(FieldPawnTest.Unassign(a, 1) && pl.Plan.Listens.Count == 0 && (!exit.gameObject.activeSelf || exit.Label.text != t.ListenPending), "B off the door");
        await Hush(a);
        Check(exit.Label.text == t.Incoming && !pl.TryReport(C, out _, out _) && (pl.Chip.text.Contains(tx.ChipPass) || pl.Chip.text.Contains(tx.ChipMeetPass)), "Incoming (nobody placed = the hush, so it passes by): " + exit.Label.text + " / " + pl.Chip.text);
        await Still(a, "56-incoming-hush");
        await Hush(a);
        Check(st.PassedBy && st.Visible && st.Gauge == 0 && pl.DoorLog.For(A, C).Count == 2, "Passed by; the log kept");
        await Still(a, "57-passed-by");
        log.Add("soon/log/hush/pass-by");
        // S9 the den: a pawn at its door listens while it is home (listen only: the den is never a move).
        await Move(a, C);
        t.ReviewWake(2, 0, 0); await Task.Delay(250); await Ready(a);
        var den = FieldPawnTest.Options(a, 1).Where(o => o.Door == D).ToList();
        Check(den.Count == 1 && den[0].Kind == FieldSpotKind.Listen, "Den door: listen only (" + string.Join(",", den.Select(o => o.Kind)) + ")");
        Check(FieldPawnTest.Listen(a, 1, D), "B at the den door"); await Still(a, "58-den-listen");
        await Tap(pl.TurnButton); await Task.Delay(300);
        var denMark = Marker(t, C, D);
        Check(pl.TryReport(D, out r, out _) && r.Present && r.Resident == ResidentState.Den && r.Next == D && r.Then == D && denMark.Label.text == t.HeardStay
            && a.Status.text == string.Format(t.StatusHeard, "관리실", t.NextStay) + "\n" + string.Format(t.StatusHeardThen, t.ThenStay), "Den heard: " + r + " / " + denMark.Label.text + " / " + a.Status.text);
        await Still(a, "59-den-heard");
        var back = FieldPawnTest.Option(a, 0, FieldPawnTest.DoorKey(A), FieldSpotKind.Listen);
        Check(back != null && back.Enabled, "The back door offers A a listening place");
        Check(!FieldPawnTest.Options(a, 0).Any(o => o.Door == S && o.Kind == FieldSpotKind.Listen), "Locked storage: nothing to listen at");
        log.Add("den home heard, back door offers A, locked door none");
        return "PASS showcase · " + string.Join(" · ", log) + " · stills " + Shots;
    }

    // Without a listener the chip does not reveal the step after next; walking into a room just heard to hold it says so.
    public static async Task<string> KnownWalkIn()
    {
        FieldIdleConfirm.AutoAccept = true; // members left idle hush without the question (VerifyIdleConfirm tests it)
        var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Run Enter first");
        var a = await Depart(c); var t = a.Threat; var pl = t.Planner; var tx = pl.Texts;
        if (c.Opening) c.Opening.State.Enabled = false;
        t.ReviewWake(1, 2, 6); await Task.Delay(250);
        // 오락기 뒤판 (noise 3, prybar fixture) alone: its first turn is loud and it stays unfinished for the second lure below.
        if (c.InventoryPanel.CountFor(a.Participants[0], "prybar") == 0) Check(c.InventoryPanel.TransferField(a.Participants[0], "prybar", 1, true), "Prybar fixture");
        await Search(a, 2, true); var st = t.State; Check(st.Danger == 2 && st.Next == C, "Lured");
        await Hush(a);
        // Nobody listens: the point is no free two-turn warning.
        Check(pl.Forecast(pl.Plan).outlook.Incoming && !pl.Chip.text.Contains(tx.ChipSoon), "No free two-turn warning: " + pl.Chip.text);
        await Still(a, "60-no-free-warning");
        // Start over for the walk-in: lure, then listen as it steps into the corridor and stops.
        t.ReviewWake(1, 2, 6); await Task.Delay(250); pl.Reset();
        await Search(a, 2, true); st = t.State; Check(st.Danger == 2 && st.Next == C, "Lured again (the machine's second turn) " + st.Danger + " " + st.Next);
        await Ready(a); Check(FieldPawnTest.Listen(a, 1, C), "B at the exit"); await Tap(pl.TurnButton); await Task.Delay(300);
        Check(pl.TryReport(C, out var r, out _) && r.Present && r.Next == C, "Heard it stop: " + r);
        await Still(a, "61-heard-stop");
        // Everyone at the exit: the move's preview warns (the move view's second line), '턴 진행' walks in.
        await Ready(a); Check(FieldPawnTest.Gather(a, C), "Everyone at the exit"); await Task.Delay(200);
        var view = a.Main.GetComponentInChildren<FieldMoveQueueView>(true); Check(view && pl.Chip.text.Contains(view.ChipMeets), "Warned: " + pl.Chip.text);
        await Tap(pl.TurnButton); await Until(() => a.Encounter.IsOpen || !a.InTransit && a.Rooms.CurrentRoom == C, 6000, "walk in"); await Task.Delay(400);
        Check(a.Encounter.IsOpen && st.Surprise && a.Encounter.View.GetComponentsInChildren<Text>(true).Any(x => x.text.Contains(t.HeardSurpriseBody.Split('\n')[0])), "Known walk-in");
        await Still(a, "62-known-walk-in");
        return "PASS known walk-in: no free two-turn warning; heard, warned, walked in, met as heard";
    }

}
