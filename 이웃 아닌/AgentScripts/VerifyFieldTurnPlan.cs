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

// Member action slots 1차: pure plan rules (edit mode), the first visit and a real-screen showcase (play mode, after Enter).
// 말 놓기 (2026-09-25, 기획/탐험-말놓기-조작-재설계.md): members are placed as pawns (FieldPawnTest, the board's own rules) and a turn
// passes only by '턴 진행'; the 07 window only shows (a right press). The first visit has the same controls (no hush button: nobody
// placed = the hush turn).
public static class VerifyFieldTurnPlan
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    const int A = FieldSiteState.Arcade, C = FieldSiteState.Corridor, S = FieldSiteState.Storage, D = FieldSiteState.Den;

    sealed class Fake : IFieldPlanFacts
    {
        public bool[] Up, Lamp; public readonly Dictionary<int, FieldSiteFacts> Sites = new Dictionary<int, FieldSiteFacts>(); public readonly HashSet<(int, int)> NoTool = new HashSet<(int, int)>();
        public Fake(int n) { Up = Enumerable.Repeat(true, n).ToArray(); Lamp = new bool[n]; }
        public int Members => Up.Length;
        public int LightBonus => 20;
        public bool Alive(int m) => m >= 0 && m < Up.Length && Up[m];
        public bool HasLight(int m) => Alive(m) && Lamp[m];
        public bool HasTool(int site, int m) => !NoTool.Contains((site, m));
        public FieldSiteFacts Site(int site) => Sites.TryGetValue(site, out var f) ? f : new FieldSiteFacts { InRoom = true, Searchable = true };
    }
    static FieldOrder O(int site, int lead, int pace = 1, int duty = 0, bool solo = false, int prefer = -1) => new FieldOrder { Site = site, Lead = lead, Pace = pace, Duty = duty, Solo = solo, Prefer = prefer };
    static string Snap(FieldTurnPlan p) => p.Version + ":" + string.Join("|", p.Orders.Select(o => $"{o.Site},{o.Lead},{o.Support},{o.Prefer},{o.Pace},{o.Duty},{o.Solo}"));
    static string Snap(FieldSiteState s) => $"{s.PartyRoom},{s.TurnsUsed},{s.Gauge},{s.Danger},{s.Floor},{s.Remembered},{s.ResidentRoom},{s.Resident},{s.Next},{s.Asleep},{s.Moved},{s.MovedFrom},{s.PassedBy},{s.Encounter},{s.Surprise},{s.Noticed},{s.Noted},{s.LastNoise}";

    public static string Rules()
    {
        var done = new List<string>();
        // 1. Nobody assigned: everyone hushes; a downed member has no slot.
        var f = new Fake(2); var plan = new FieldTurnPlan(); var k = plan.Check(f);
        Check(k.Runs.Count == 0 && k.HushedAll && !k.Full && k.Hushing == 2 && k.Actions.All(a => a == FieldAction.Hush), "Empty plan: all hush");
        f.Up[1] = false; k = plan.Check(f); Check(k.Actions[1] == FieldAction.Down && k.Hushing == 1, "Downed has no slot"); f.Up[1] = true;
        done.Add("hush/down");

        // 2. The 2-member default: the partner is bound as 동행, exactly like the old implicit partner.
        plan.Assign(O(1, 0)); k = plan.Check(f); var r = k.RunFor(1);
        // 함께 (2026-09-25): one turn sooner, no find bonus; the fake object is silent with the default 2 turns.
        Check(r != null && r.Support == 1 && r.Role == FieldAction.Together && r.Bonus == 0 && r.Noise == 0 && r.Required == 1 && r.Completes && k.Full && k.Actions[1] == FieldAction.Together, "Default pair");
        // 3. Solo and a parallel search: both lead, noise sums, nobody hushes.
        plan = new FieldTurnPlan(); plan.Assign(O(1, 0, solo: true)); k = plan.Check(f);
        Check(k.RunFor(1).Support < 0 && k.RunFor(1).Bonus == 0 && k.Actions[1] == FieldAction.Hush && !k.Full, "Solo leaves the partner hushing");
        f.Sites[0] = new FieldSiteFacts { InRoom = true, Searchable = true, Noise = 2 }; f.Sites[1] = new FieldSiteFacts { InRoom = true, Searchable = true, Noise = 1 }; // each object's own noise
        plan.Assign(O(0, 1)); k = plan.Check(f);
        Check(k.Runs.Count == 2 && k.Full && k.Noise == 3 && !k.HushedAll && k.RunFor(0).Support < 0, "Parallel: sums noise, no free helper");
        done.Add("pair/solo/parallel");

        // 4. Last assignment wins.
        plan = new FieldTurnPlan(); plan.Assign(O(1, 0)); plan.Adopt(plan.Check(f)); Check(plan.Find(1).Support == 1, "Adopt stores the supporter");
        var notes = plan.Assign(O(0, 1)); Check(notes.Any(n => n.Kind == FieldMoveKind.SupportMoved && n.Site == 1 && n.Member == 1) && plan.Find(1).Support == -1, "Supporter moved to lead");
        notes = plan.Assign(O(2, 0)); Check(notes.Any(n => n.Kind == FieldMoveKind.LeadMoved && n.Site == 1) && plan.Find(1) == null && plan.Orders.Count == 2, "Lead moved");
        done.Add("last-wins");

        // 5. Binding with 3: preference, lowest free, never stealing a kept supporter.
        f = new Fake(3); plan = new FieldTurnPlan(); plan.Assign(O(1, 0, prefer: 2)); Check(plan.Check(f).RunFor(1).Support == 2, "Prefer");
        plan = new FieldTurnPlan(); plan.Assign(O(1, 0)); Check(plan.Check(f).RunFor(1).Support == 1, "Lowest free");
        f.Sites[1] = new FieldSiteFacts { InRoom = true, Searchable = true, Noise = 3 }; // 망보기 needs a noisy object
        plan = new FieldTurnPlan(); plan.Assign(O(1, 1, duty: 1)); k = plan.Check(f); Check(k.RunFor(1).Support == 0, "Lowest free (0)");
        plan = new FieldTurnPlan(); plan.Assign(O(1, 1, duty: 1, prefer: 2)); plan.Adopt(plan.Check(f)); plan.Assign(O(0, 0)); k = plan.Check(f);
        Check(k.RunFor(1).Support == 2 && k.RunFor(1).Watched && k.RunFor(0).Support < 0 && k.RunFor(0).Bonus == 0, "Kept supporter is not stolen");
        // Required light is bound before an optional helper; a tapped preference takes a kept supporter (and says so).
        f = new Fake(3); f.Lamp[2] = true; plan = new FieldTurnPlan(); plan.Assign(O(0, 0)); plan.Assign(O(1, 1, duty: 2)); k = plan.Check(f);
        Check(k.RunFor(1) != null && k.RunFor(1).Support == 2 && k.RunFor(0).Support < 0, "Light before optional");
        f = new Fake(3); plan = new FieldTurnPlan(); plan.Assign(O(1, 0)); plan.Keep(plan.Check(f)); Check(plan.Find(1).Support == 1, "Keep stores the shown supporter");
        notes = plan.Assign(O(0, 2, prefer: 1)); k = plan.Check(f);
        Check(notes.Any(n => n.Kind == FieldMoveKind.SupportMoved && n.Site == 1 && n.Member == 1) && k.RunFor(0).Support == 1 && k.RunFor(1).Support < 0, "Preference takes with a note");
        done.Add("binding");

        // 6. Light: a flashlight in a non-lead's bag; otherwise the search pauses (no time for it, lead counts as hushing).
        f = new Fake(2); f.Lamp[0] = true; plan = new FieldTurnPlan(); plan.Assign(O(1, 0, duty: 2)); k = plan.Check(f);
        Check(k.Runs.Count == 0 && k.PauseFor(1) == FieldPause.NoLight && k.Actions[0] == FieldAction.Paused && k.HushedAll && k.Hushing == 2, "Lead's own lamp does not count");
        f.Lamp[1] = true; k = plan.Check(f); Check(k.RunFor(1).Support == 1 && k.RunFor(1).Bonus == 20 && k.Actions[1] == FieldAction.Light, "Light bound");
        f.NoTool.Add((1, 0)); k = plan.Check(f); Check(k.PauseFor(1) == FieldPause.NoTool, "Tool in the lead's bag"); f.NoTool.Clear();
        done.Add("light/tool");

        // 7. Check is pure.
        string before = Snap(plan); plan.Check(f); plan.Check(f); Check(Snap(plan) == before, "Check mutated the plan");
        done.Add("pure");

        // 8. Object noise × role for two members (2026-09-25, no pace): noise = the object's, 망보기 −1 (not offered on a silent object),
        //    함께 one turn sooner (the default 2 → 1), 조명 +20%p, no 함께 bonus.
        for (int n = 0; n <= 3; n++)
            for (int duty = 0; duty < 3; duty++)
            {
                f = new Fake(2); f.Lamp[1] = true; f.Sites[1] = new FieldSiteFacts { InRoom = true, Searchable = true, Noise = n };
                plan = new FieldTurnPlan(); plan.Assign(O(1, 0, duty: duty)); r = plan.Check(f).RunFor(1); bool bound = duty != 1 || n > 0;
                Check(r != null && r.Support == (bound ? 1 : -1) && r.Bonus == (duty == 2 ? 20 : 0) && r.Noise == Math.Max(0, n - (duty == 1 ? 1 : 0)) && r.Required == (duty == 0 ? 1 : 2) && plan.CanOffer(1, 1, 0, f) == (n > 0), $"Object noise {n} duty {duty}");
            }
        done.Add("object noise 0-3 × role");

        // 9. A locked 함께 bonus is lost only when a living partner does not help; a sole survivor keeps it.
        f = new Fake(2); f.Sites[1] = new FieldSiteFacts { InRoom = true, Searchable = true, Opened = true, Progress = 1, Required = 2, Pace = 1, Duty = 0, Bonus = 10 };
        plan = new FieldTurnPlan(); plan.Assign(O(1, 0)); plan.Assign(O(0, 1)); r = plan.Check(f).RunFor(1);
        Check(r.Forfeits && r.Bonus == 0 && r.Lost == 10, "Forfeit when the partner leads elsewhere");
        plan.Release(0); f.Up[1] = false; r = plan.Check(f).RunFor(1); Check(!r.Forfeits && r.Bonus == 10, "Sole survivor keeps it");
        // 10. 망보기 lowers a noisy object's noise by 1 only while a watcher is bound; on a silent object it is not offered and the partner hushes.
        f = new Fake(2); f.Sites[1] = new FieldSiteFacts { InRoom = true, Searchable = true, Noise = 3 }; plan = new FieldTurnPlan(); plan.Assign(O(1, 0, duty: 1)); k = plan.Check(f);
        Check(k.RunFor(1).Noise == 2 && k.RunFor(1).Support == 1, "Watched −1");
        plan.Assign(O(0, 1)); Check(plan.Check(f).RunFor(1).Noise == 3, "No watcher, full noise");
        f.Sites[1] = new FieldSiteFacts { InRoom = true, Searchable = true, Noise = 0 }; plan = new FieldTurnPlan(); plan.Assign(O(1, 0, duty: 1)); k = plan.Check(f);
        Check(k.RunFor(1).Noise == 0 && k.RunFor(1).Support < 0 && k.Actions[1] == FieldAction.Hush && !plan.CanOffer(1, 1, 0, f), "Silent object: no watch, partner hushes");
        done.Add("forfeit/watch");

        // 11. A run applies once.
        f = new Fake(2); plan = new FieldTurnPlan(); plan.Assign(O(1, 0)); r = plan.Check(f).RunFor(1); // 함께: the 2-turn object in one
        var s = new ExpeditionLootPanel.SearchState(); Check(ExpeditionLootPanel.Apply(s, r) && s.Progress == 1 && s.Complete && s.Bonus == 0 && s.Required == 1 && s.Pace == 1, "Apply");
        Check(!ExpeditionLootPanel.Apply(s, r) && s.Progress == 1, "Second apply refused");
        done.Add("apply once");

        // 12. Pruning: completed and paused orders go, others stay.
        f = new Fake(3); f.Lamp[2] = false; plan = new FieldTurnPlan(); plan.Assign(O(1, 0, 0)); plan.Assign(O(2, 1, 1, 2)); k = plan.Check(f);
        var gone = plan.Prune(k); Check(gone.Any(g => g.Site == 1 && g.Reason == FieldPause.Complete) && gone.Any(g => g.Site == 2 && g.Reason == FieldPause.NoLight) && plan.Orders.Count == 0, "Prune");
        done.Add("prune");

        // 13. Fuzz: random parties, plans and site states. Invariants hold, the forecast copy does not touch the real state
        //     and equals the real turn, and Apply matches the run.
        var rng = new System.Random(11); int turns = 0;
        for (int t = 0; t < 2000; t++)
        {
            int n = rng.Next(1, 5); f = new Fake(n);
            for (int m = 0; m < n; m++) { f.Up[m] = rng.Next(6) > 0; f.Lamp[m] = rng.Next(3) == 0; }
            for (int site = 0; site < 5; site++)
            {
                int pace = rng.Next(3), req = pace + 1, prog = rng.Next(3) == 0 ? rng.Next(req) : 0; bool complete = rng.Next(12) == 0;
                f.Sites[site] = new FieldSiteFacts { InRoom = rng.Next(8) > 0, Searchable = rng.Next(10) > 0, Opened = prog > 0 || rng.Next(2) == 0, Complete = complete, Progress = complete ? req : prog, Required = prog > 0 || complete ? req : 0, Pace = prog > 0 || complete ? pace : 0, Duty = prog > 0 ? rng.Next(3) : 0, Bonus = prog > 0 ? new[] { 0, 10, 20 }[rng.Next(3)] : 0, Noise = rng.Next(4), Turns = rng.Next(4) };
                if (rng.Next(6) == 0) f.NoTool.Add((site, rng.Next(n)));
            }
            plan = new FieldTurnPlan();
            for (int j = rng.Next(0, 4); j > 0; j--) plan.Assign(O(rng.Next(5), rng.Next(n), rng.Next(3), rng.Next(3), rng.Next(3) == 0, rng.Next(-1, n)));
            if (rng.Next(3) == 0) plan.Adopt(plan.Check(f));
            before = Snap(plan); k = plan.Check(f); Check(Snap(plan) == before, "Fuzz: Check mutated");
            // one slot each; supporters are not leads; light holds a lamp; totals add up
            var sup = k.Runs.Where(x => x.Support >= 0).Select(x => x.Support).ToList();
            Check(sup.Distinct().Count() == sup.Count && sup.All(m => !plan.Orders.Any(o => o.Lead == m)) && k.Runs.All(x => x.Duty != 2 || x.Starts && x.Support < 0 || f.HasLight(x.Support)), "Fuzz: binding " + t);
            Check(k.Noise == k.Runs.Sum(x => x.Noise) && k.HushedAll == (k.Runs.Count == 0) && k.Runs.Select(x => x.Lead).Distinct().Count() == k.Runs.Count, "Fuzz: totals " + t);
            // each run: the object's own noise (망보기 −1 only on a noisy object), a new search's turns from the object (함께 −1), a running one's stored
            Check(k.Runs.All(x => x.Noise == FieldTurnPlan.SearchNoise(f.Site(x.Site).Noise, x.Watched) && (!x.Watched || f.Site(x.Site).Noise > 0)
                && x.Required == (x.Starts ? FieldTurnPlan.RequiredOf(f.Site(x.Site).Turns, x.Support >= 0 && x.Duty == 0) : f.Site(x.Site).Required) && (!x.Starts || x.Pace == 1)), "Fuzz: object noise/turns " + t);
            for (int m = 0; m < n; m++) Check((k.Actions[m] == FieldAction.Down) == !f.Alive(m), "Fuzz: down " + t);
            foreach (var run in k.Runs)
            {
                var sf = f.Site(run.Site);
                var st = new ExpeditionLootPanel.SearchState { Progress = sf.Progress, Required = sf.Required, Pace = sf.Pace, Duty = sf.Duty, Bonus = sf.Bonus, Opened = sf.Opened, Complete = sf.Complete };
                Check(ExpeditionLootPanel.Apply(st, run) && st.Progress == run.After && st.Complete == run.Completes && st.Bonus == run.Bonus && st.Required == run.Required && st.Opened, "Fuzz: apply " + t);
            }
            // the site board: a random evolved state, forecast on a copy, then the real turn
            var site0 = new FieldSiteState(new FieldSiteRules(), () => 0, false, rng.Next(0, 3), rng.Next(0, 4), rng.Next(-1, 3), rng.Next(0, 20));
            int room = A;
            for (int j = rng.Next(0, 7); j > 0; j--)
            {
                if (rng.Next(3) == 0) { var near = new[] { A, C, S, D }.Where(x => x != D && FieldSiteState.Adjacent(room, x)).ToArray(); if (near.Length > 0) { room = near[rng.Next(near.Length)]; site0.MoveParty(room); } }
                site0.EndTurn(rng.Next(0, 5), rng.Next(3) == 0);
                if (site0.Encounter && rng.Next(2) == 0) site0.Hidden(); else if (site0.Encounter) site0.Driven();
            }
            string real = Snap(site0); var o = site0.Copy(); o.MoveParty(room); o.EndTurn(k.Noise, k.HushedAll);
            Check(Snap(site0) == real, "Fuzz: forecast touched the real state " + t);
            site0.MoveParty(room); site0.EndTurn(k.Noise, k.HushedAll); Check(Snap(site0) == Snap(o), "Fuzz: forecast ≠ real " + t + " " + Snap(site0) + " / " + Snap(o));
            turns++;
        }
        done.Add("fuzz " + turns + " turns");

        // 14. Noted: 보통 ∥ 보통 (4) at 위험도 1 remembers the room; 3 at 위험도 0 without overflow does not.
        var b = new FieldSiteState(new FieldSiteRules(), () => 0, danger: 1); b.EndTurn(4, false); Check(b.Noted && b.Remembered == A, "Loud pair noted");
        b = new FieldSiteState(new FieldSiteRules(), () => 0); b.EndTurn(3, false); Check(!b.Noted && b.Remembered < 0 && b.Danger == 0, "Not noted at 0");
        // 15. Met (it walked in) / Noticed (it was already here) / Surprise (we walked in).
        b = new FieldSiteState(new FieldSiteRules(), () => 0, danger: 2, remembered: A); b.EndTurn(0, false); b.EndTurn(0, false); Check(b.Incoming, "Incoming");
        var met = b.Copy(); met.EndTurn(0, false); Check(met.Encounter && !met.Surprise && !met.Noticed, "Met");
        b.EndTurn(0, true); Check(b.PassedBy && b.Visible && b.Resident == ResidentState.Staying, "Passed, staying");
        var noticed = b.Copy(); noticed.EndTurn(2, false); Check(noticed.Encounter && noticed.Noticed && !noticed.Surprise, "Noticed");
        b = new FieldSiteState(new FieldSiteRules(), () => 0, danger: 2, remembered: C); b.EndTurn(0, false); b.EndTurn(0, false); b.MoveParty(C); b.EndTurn(0, false);
        Check(b.Encounter && b.Surprise && !b.Noticed, "Surprise");
        // A loud turn on its last stay turn: the memory is wiped the same turn, so nothing is noted.
        b = new FieldSiteState(new FieldSiteRules(), () => 0, danger: 2, remembered: A); b.MoveParty(S);
        for (int i = 0; i < 4; i++) b.EndTurn(0, true);
        Check(b.Resident == ResidentState.Staying && b.ResidentRoom == A, "Staying in the arcade");
        b.EndTurn(3, false); Check(!b.Noted && b.Remembered < 0, "Wiped memory is not noted");
        done.Add("noted/met/noticed/surprise");
        return "PASS: " + string.Join("; ", done);
    }

    // ---- Play mode ----
    static async Task Tap(Button button)
    {
        for (int i = 0; i < 20 && button && !(button.IsActive() && button.IsInteractable()); i++) await Task.Delay(50); // enabled on the next LateUpdate
        Check(button && button.IsActive() && button.IsInteractable(), "Unavailable " + (button ? button.name : "null"));
        for (int i = 0; i < 20 && !Hits(button); i++) await Task.Delay(50); // a window that just opened lays out over a few frames
        Hit(button);
        var r = (RectTransform)button.transform; var data = new PointerEventData(EventSystem.current) { position = Screen(r), button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler); await Task.Delay(100);
    }
    static Vector2 Screen(RectTransform r) { Canvas.ForceUpdateCanvases(); return RectTransformUtility.WorldToScreenPoint(r.GetComponentInParent<Canvas>().worldCamera, r.TransformPoint(r.rect.center)); }
    static bool Hits(Selectable target) { var data = new PointerEventData(EventSystem.current) { position = Screen((RectTransform)target.transform) }; var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits); return hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Selectable>() == target; }
    // The topmost thing under the centre belongs to this button (no click).
    static void Hit(Selectable target)
    {
        var data = new PointerEventData(EventSystem.current) { position = Screen((RectTransform)target.transform) }; var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Selectable>() == target, "Blocked " + target.name + " by " + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
    }
    static async Task Until(Func<bool> done, int ms, string what) { var w = Stopwatch.StartNew(); while (!done()) { if (w.ElapsedMilliseconds > ms) throw new Exception("Timeout: " + what); await Task.Delay(30); } }
    static string Shots => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "FieldTurnPlanCapture"));
    static Task Still(MonoBehaviour host, string name) { var done = new TaskCompletionSource<bool>(); host.StartCoroutine(StillRoutine(name, done)); return done.Task; }
    static IEnumerator StillRoutine(string name, TaskCompletionSource<bool> done)
    {
        yield return new WaitForEndOfFrame();
        var raw = new RenderTexture(UnityEngine.Screen.width, UnityEngine.Screen.height, 0); var rt = new RenderTexture(raw.width, raw.height, 0); ScreenCapture.CaptureScreenshotIntoRenderTexture(raw);
        if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(raw, rt, new Vector2(1, -1), new Vector2(0, 1)); else Graphics.Blit(raw, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new UnityEngine.Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); RenderTexture.active = prev; Object.Destroy(raw); Object.Destroy(rt);
        Directory.CreateDirectory(Shots); File.WriteAllBytes(Path.Combine(Shots, name + ".png"), tex.EncodeToPNG()); Object.Destroy(tex); done.SetResult(true);
    }
    // Rect in Main space (1920×1080, y from the top).
    static Rect OnMain(ExpeditionArrivalPanel a, RectTransform r)
    {
        var main = (RectTransform)a.Main.transform; var c = new Vector3[4]; r.GetWorldCorners(c);
        Vector3 lo = main.InverseTransformPoint(c[0]), hi = main.InverseTransformPoint(c[2]);
        return new Rect(lo.x - main.rect.xMin, main.rect.yMax - hi.y, hi.x - lo.x, hi.y - lo.y);
    }
    static void Near(Rect r, float x, float y, float w, float h, string what) => Check(Mathf.Abs(r.x - x) < 1.5f && Mathf.Abs(r.y - y) < 1.5f && Mathf.Abs(r.width - w) < 1.5f && Mathf.Abs(r.height - h) < 1.5f, what + " rect " + r);
    static void Fits(params Text[] texts) { Canvas.ForceUpdateCanvases(); foreach (var t in texts) if (t && t.isActiveAndEnabled && !t.resizeTextForBestFit) Check(t.preferredHeight <= t.rectTransform.rect.height + 1, "Overflow " + t.name + ": " + t.text); }
    static string Label(Button b) => b.GetComponentInChildren<Text>().text;
    static async Task<ExpeditionArrivalPanel> Depart(SettlementController c)
    {
        await Tap(c.Exit); foreach (var card in c.ExpeditionPanel.Cards) if (!card.Check.gameObject.activeSelf) await Tap(card.Button);
        await Tap(c.ExpeditionPanel.Pack); await Tap(c.PackingPanel.Ready); await Tap(c.PackingPanel.Depart); await Task.Delay(1100);
        Check(c.ArrivalPanel.IsOpen && !c.ArrivalPanel.InTransit, "Arrived"); return c.ArrivalPanel;
    }
    static async Task CloseLoot(ExpeditionArrivalPanel a) { if (a.Loot.IsOpen) { await Tap(a.Loot.Back); await Task.Delay(120); if (a.Loot.LeaveReview.activeSelf) await Tap(a.Loot.LeaveConfirm); } }
    // A move: every living pawn at the door onto the next room (the last one sets the move), then '턴 진행'.
    static async Task Move(ExpeditionArrivalPanel a)
    {
        int next = a.Rooms.CurrentRoom == A ? C : A;
        Check(FieldPawnTest.Gather(a, next), "Everyone at the door to " + FieldSiteState.RoomNames[next] + ": " + FieldPawnTest.Describe(a));
        await Tap(a.Threat.Planner.TurnButton); await Until(() => !a.InTransit, 6000, "move"); await Task.Delay(400);
    }
    static async Task Ready(ExpeditionArrivalPanel a) => await Until(() => FieldPawnTest.Ready(a), 3000, "board ready");

    // New game → settlement (works with the fixed opening pair and with a free pick of two), intro lifted like VerifyFieldBattle.Enter.
    public static async Task<string> Enter()
    {
        Check(Application.isPlaying, "Play first"); UnityEngine.SceneManagement.SceneManager.LoadScene("PartySelection"); await Task.Delay(800);
        var p = Object.FindAnyObjectByType<PartySelectionController>(); if (PartySelectionSession.Selected.Count != 2) { PartySelectionSession.Clear(); p.Refresh(); } await Task.Delay(100); // the fixed opening pair is preselected
        foreach (var card in p.Cards.Where(x => x.gameObject.activeInHierarchy && x.Button.IsInteractable()))
        {
            if (PartySelectionSession.Selected.Count >= 2) break;
            await Tap(card.Button);
        }
        await Tap(p.Continue); await Task.Delay(800);
        var h = Object.FindAnyObjectByType<HomeSelectionController>(); await Tap(h.Cards[0].Button); await Tap(h.Continue); await Task.Delay(900);
        var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Settlement missing");
        if (c.Introduction) c.Introduction.Restore(10);
        // The tutorial story beats have their own test (VerifyTutorialStory): mark them seen so they never cover this fixture.
        var story = c.GetComponent<SettlementTutorialNarrative>(); if (story) story.Restore(new SavedTutorialNarrative { SeenMask = SavedTutorialNarrative.AllSeen, PendingBeat = -1 });
        await Task.Delay(300);
        return "Settlement " + c.Campaign.Home.Name + " · " + string.Join(", ", c.Campaign.Party.Select(x => x.Name));
    }

    // First visit (it sleeps): the same controls as a later visit (말 놓기): '턴 진행' and its chip show, the hush button is gone and the
    // 07 window only shows; two pawns on one object and '턴 진행' = exactly one turn (co-op: one turn sooner), placing costs no time.
    public static async Task<string> FirstVisit()
    {
        FieldIdleConfirm.AutoAccept = true; // a member left idle hushes without the question (VerifyIdleConfirm tests it)
        var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Run VerifyFieldTurnPlan.Enter first");
        var a = await Depart(c); var t = a.Threat; var pl = t.Planner; await Ready(a);
        Check(pl && pl.Placing && !pl.Active && t.State.Asleep && !pl.Plan.HasAssignments, "First visit: pawns are placed, it sleeps, nothing placed");
        Check(pl.TurnButton.gameObject.activeSelf && pl.Chip.gameObject.activeSelf && (!t.Hush || !t.Hush.gameObject.activeSelf), "'턴 진행' and its chip, no hush button");
        Fits(a.Cards.Select(x => x.State).ToArray());
        await Still(a, "40-first-visit-cards");
        int site = Enumerable.Range(0, 3).FirstOrDefault(i => a.Loot.IsSiteInCurrentRoom(i) && string.IsNullOrEmpty(a.Loot.Sites[i].RequiredTool) && !(a.Loot.Peek(i, out var s) && s.Progress > 0));
        Check(FieldPawnTest.Detail(a, site) && a.Search.IsOpen && a.Search.ReadOnly && !(a.Search.Choose.IsActive() && a.Search.Choose.IsInteractable()), "The 07 window only shows");
        await Tap(a.Search.Back);
        int turns = a.Rooms.Turns, minute = c.Campaign.MinuteOfDay;
        Check(FieldPawnTest.Coop(a, site) && FieldPawnTest.Role(a, site) == FieldAction.Together, "Two pawns on " + a.ObjectNames[site] + ": " + FieldPawnTest.Describe(a));
        Check(a.Rooms.Turns == turns && c.Campaign.MinuteOfDay == minute, "Placing costs no time");
        await Tap(pl.TurnButton); await Task.Delay(300);
        Check(a.Rooms.Turns == turns + 1 && c.Campaign.MinuteOfDay == minute + a.Rooms.MinutesPerTurn && pl.LastMismatch == "", "One turn");
        var run = pl.LastCheck != null ? pl.LastCheck.RunFor(site) : null;
        Check(run != null && run.Support >= 0 && run.Required == FieldTurnPlan.RequiredOf(a.Loot.SiteTurns(site), true), "Co-op on the first visit (one turn sooner)");
        await CloseLoot(a); if (a.Search.IsOpen) await Tap(a.Search.Back);
        return "PASS first visit: '턴 진행' and chip, no hush button, 07 read only, placing free, co-op pawns + '턴 진행' = 1 turn (" + a.ObjectNames[site] + ")";
    }
    // The old name (the first visit had no slots before 말 놓기): the same check now.
    public static Task<string> FirstVisitGuard() => FirstVisit();

    public static async Task<string> Showcase()
    {
        FieldIdleConfirm.AutoAccept = true; // members left idle hush without the question (VerifyIdleConfirm tests it)
        var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Run VerifyFieldTurnPlan.Enter first");
        var a = await Depart(c); var t = a.Threat; var pl = t.Planner; var s = a.Search; var log = new List<string>(); var rules = FieldPawnTest.Rules(a);
        Check(!pl.Active, "First visit asleep");
        // A. Place, take off and move (no time passes for any of it; the move is everyone at the door, then '턴 진행').
        // Review fixture: a later visit comes after the opening chapter, so its guide banner is off.
        if (c.Opening) c.Opening.State.Enabled = false;
        t.ReviewWake(0, 0, 0); await Task.Delay(200); await Ready(a); Check(pl.Active && pl.TurnButton.gameObject.activeSelf, "Board on");
        int turns = a.Rooms.Turns, minute = c.Campaign.MinuteOfDay;
        Check(FieldPawnTest.Lead(a, 0, 1) && pl.Plan.Find(1) != null && pl.Plan.Find(1).Lead == 0 && pl.Plan.Find(1).Solo, "A leads " + a.ObjectNames[1] + " alone");
        Check(FieldPawnTest.Detail(a, 1) && s.ReadOnly && s.PlacedLine.Contains(a.Participants[0].Name), "07 shows who is placed: " + s.PlacedLine); await Tap(s.Back);
        Check(FieldPawnTest.Unassign(a, 0) && pl.Plan.Orders.Count == 0 && rules.LastStatus.Contains(a.Participants[0].Name), "Taken off: " + rules.LastStatus);
        Check(a.Rooms.Turns == turns && c.Campaign.MinuteOfDay == minute, "No time for placing or taking off");
        Check(FieldPawnTest.Lead(a, 0, 1), "A again");
        Check(FieldPawnTest.Gather(a, C) && a.Rooms.HasQueuedMove && pl.Plan.Orders.Count == 0 && a.Rooms.Turns == turns && c.Campaign.MinuteOfDay == minute, "Everyone at the door: A left the table, the move waits for the turn, no time");
        await Tap(pl.TurnButton); await Until(() => !a.InTransit, 6000, "move"); await Task.Delay(400);
        Check(pl.Plan.Orders.Count == 0 && !pl.Plan.HasGathers && a.Cards.All(x => x.Action.gameObject.activeSelf && x.Action.text == pl.Texts.TagHush), "Move cleared the plan");
        await Move(a); Check(a.Rooms.CurrentRoom == A, "Back in the arcade");
        log.Add("place/take off free, move by gathering");

        // B. The room screen at 위험도 1, gauge 1 (D's loud machine turn, noise 3, overflows it: 위험도 2).
        t.ReviewWake(1, 1, 0); await Task.Delay(250); await Ready(a);
        Near(OnMain(a, (RectTransform)pl.TurnButton.transform), 1370, 834, 300, 130, "턴 진행"); Near(OnMain(a, pl.Chip.rectTransform), 944, 890, 372, 68, "chip"); // exploration tray: 행동 / 이번 턴 panels
        Check(!a.Main.transform.Find("Hush") || !a.Main.transform.Find("Hush").gameObject.activeSelf, "No hush button");
        foreach (var target in new Selectable[] { a.Return, pl.TurnButton }.Concat(a.Cards.Select(x => (Selectable)x.Button))) Hit(target);
        Fits(pl.TurnTitle, pl.TurnSubtitle);
        Fits(a.Cards.SelectMany(x => new[] { x.Action, x.State, x.Health }).ToArray());
        Check(a.Cards.All(x => x.Action.text == pl.Texts.TagHush) && FieldMemberActionSlots.SubtitleMatches(pl, pl.Texts.TurnHushSubtitle), "All free");
        await Still(a, "41-plan-room");

        // C. A leads the crate alone (a 3-turn fixture, as the old 정밀): placed, no time. Fixtures for D (restored at the end): 오락기 뒤판
        //    takes 1 turn (as the old fast table) and B carries the prybar.
        int crateTurns = a.Loot.Sites[1].Turns, machineTurns = a.Loot.Sites[2].Turns; a.Loot.Sites[1].Turns = 3; a.Loot.Sites[2].Turns = 1;
        if (c.InventoryPanel.CountFor(a.Participants[1], "prybar") == 0) Check(c.InventoryPanel.TransferField(a.Participants[1], "prybar", 1, true), "Prybar fixture (B)");
        turns = a.Rooms.Turns; minute = c.Campaign.MinuteOfDay; int noise = a.Rooms.Noise;
        Check(FieldPawnTest.Lead(a, 0, 1) && pl.Plan.Find(1).Solo, "A alone on the crate");
        Check(FieldPawnTest.Detail(a, 1) && s.PlacedLine == string.Format(s.ReadPlaced, string.Format(s.ReadLead, a.Participants[0].Name)), "07: A alone (" + s.PlacedLine + ")");
        await Still(a, "42-search-solo"); await Tap(s.Back);
        Check(a.Rooms.Turns == turns && c.Campaign.MinuteOfDay == minute && a.Rooms.Noise == noise && (!a.Loot.Peek(1, out var p1) || p1.Progress == 0), "Placed without time");
        Check(a.Cards[0].Action.text == pl.Texts.TagLead && a.Cards[1].Action.text == pl.Texts.TagHush && FieldMemberActionSlots.SubtitleMatches(pl, pl.Texts.TurnSubtitle), "Tags after placing");
        await Still(a, "43-plan-saved");

        // D. B leads 오락기 뒤판 (noise 3, prybar) in the same turn: both run together, noise 0 + 3 (loud) → 위험도 2, the arcade remembered.
        var machine = FieldPawnTest.Option(a, 1, FieldPawnTest.SearchKey(2), FieldSpotKind.Lead); Check(machine != null && machine.Enabled, "B can lead the machine (prybar in B's bag)");
        Check(FieldPawnTest.Lead(a, 1, 2), "B on the machine");
        var k = pl.Current; Check(k.Runs.Count == 2 && k.Noise == a.Loot.SiteNoise(1) + a.Loot.SiteNoise(2), "Two searches this turn: noise " + k.Noise);
        await Still(a, "44-parallel-planned");
        await Tap(pl.TurnButton); await Task.Delay(300); var st = t.State;
        a.Loot.Peek(2, out var p0); a.Loot.Peek(1, out p1);
        Check(a.Rooms.Turns == turns + 1 && c.Campaign.MinuteOfDay == minute + a.Rooms.MinutesPerTurn, "Exactly one turn");
        Check(p0.Complete && p0.Required == 1 && p1.Progress == 1 && p1.Required == 3, "Machine done, crate 1/3");
        Check(st.LastNoise == 3 && st.Gauge == 0 && st.Danger == 2 && st.Remembered == A && st.Next == C && pl.LastMismatch == "", "Board: " + st.LastNoise + "/" + st.Gauge + "/" + st.Danger + " " + pl.LastMismatch);
        if (s.IsOpen) await Tap(s.Back); await Task.Delay(300); await CloseLoot(a);
        await Still(a, "45-parallel-resolved");
        log.Add("parallel noise " + st.LastNoise + " → gauge " + st.Gauge);

        // E. Nobody placed = the hush turn (twice in one frame = one turn); the crate keeps its progress on the object.
        await Ready(a);
        Check(pl.Plan.Orders.Count == 1 && FieldPawnTest.Unassign(a, 0) && !pl.Plan.HasAssignments, "A taken off the crate: nobody placed");
        turns = a.Rooms.Turns; pl.TurnButton.onClick.Invoke(); pl.TurnButton.onClick.Invoke(); await Task.Delay(200);
        Check(a.Rooms.Turns == turns + 1 && st.Gauge == 0 && pl.LastCheck.HushedAll && p1.Progress == 1 && st.ResidentRoom == C && st.Heard, "Hush turn, progress kept");
        await Still(a, "46-hush-turn");
        await Task.Delay(350); await Tap(pl.TurnButton); await Task.Delay(200);
        // 말 놓기: nobody placed = the hush turn, so the preview already shows it passing by ('무언가가 지나감').
        Check(st.Incoming && (pl.Chip.text.Contains(pl.Texts.ChipPass) || pl.Chip.text.Contains(pl.Texts.ChipMeetPass)), "Incoming chip: " + pl.Chip.text);
        await Still(a, "47-incoming");
        await Task.Delay(350); await Tap(pl.TurnButton); await Task.Delay(300);
        Check(st.PassedBy && st.Visible && !a.Encounter.IsOpen, "Passed by the hushed party");
        await Still(a, "48-passed-by");
        log.Add("hush ×3 by '턴 진행' with nobody placed, passed by");

        // F. A back on the crate while it stands in the room: one turn, the crate progresses, and it notices us (the plan ends).
        await Ready(a); Check(FieldPawnTest.Lead(a, 0, 1), "A on the running crate");
        await Task.Delay(350); turns = a.Rooms.Turns;
        pl.TurnButton.onClick.Invoke(); pl.TurnButton.onClick.Invoke(); await Task.Delay(400);
        a.Loot.Peek(2, out p0); a.Loot.Peek(1, out p1);
        Check(a.Rooms.Turns == turns + 1 && a.Encounter.IsOpen && pl.Plan.Orders.Count == 0 && !a.Loot.IsOpen, "One turn into an encounter");
        Check(p1.Progress == 2 && st.Noticed && !st.Surprise && pl.LastMismatch == "", "Noticed; progress kept");
        Check(a.Encounter.View.GetComponentsInChildren<Text>(true).Any(x => x.text.Contains(t.NoticedBody.Split('\n')[0])), "Noticed text");
        await Still(a, "49-noticed-encounter");
        log.Add("encounter noticed, crate 2/3 kept");
        a.Loot.Sites[1].Turns = crateTurns; a.Loot.Sites[2].Turns = machineTurns; // fixtures back (the running crate keeps its stored 3 turns)
        return "PASS showcase · " + string.Join(" · ", log) + " · stills " + Shots;
    }
}
