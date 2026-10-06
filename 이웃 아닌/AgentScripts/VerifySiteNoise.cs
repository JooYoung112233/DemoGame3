using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 속도 삭제와 사물 소음 (기획/탐험-수색쪽지와-협동-1차.md '규칙 변경' · '추가 결정', 2026-09-25).
//  Rules  (edit mode, pure): SearchNoise / RequiredOf; the three roles on objects of noise 0-3 and 0-3 turns (함께 one turn sooner,
//         망보기 noise −1 and never offered on a silent object, 조명 +20%p, no pace); running and older-save searches keep what they stored;
//         a noise-3 turn is loud (remembered), 망보기 makes it 2 (not); the first-visit encounter by search turns (static formulas).
//  Data   (edit mode, after BuildSiteNoise.Run): what SettlementScreen.prefab gives the game: object noise/turns, the lock 3, the encounter
//         values, the 07 window without the pace row (the noise line in its place), the texts without 빠름/보통/정밀/속도.
//  FirstVisit (play mode, a fresh game: Enter): the crate is silent (0 noise, 함께 finishes it in one turn, no 망보기); the machine
//         (prybar fixture) makes 3 (망보기 offered: 3→2); two search turns warn, the next search's chance by search turns (+ noise);
//         a running 망보기 search goes on alone after its watcher is down (full noise), and a lone member is not offered a new 망보기.
//  Board  (play mode, another fresh game: Enter): 함께 finishes the 2-turn table in one turn; 망보기 on the machine: 3→2 (preview only);
//         the machine alone while the other member listens: noise 3, loud, the arcade remembered (위험도 2); the save round trip of the
//         one-turn 함께 search (the old 'Required == Pace + 1' check refused it).
public static class VerifySiteNoise
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    const int A = FieldSiteState.Arcade, C = FieldSiteState.Corridor;
    static readonly string[] PaceWords = { "빠름", "정밀", "속도", "수색 방식", "빠른 수색", "보통 수색", "정밀 수색", "발견 보정", "협력 +" };
    static string PaceWord(string s) => s == null ? null : PaceWords.FirstOrDefault(s.Contains);

    sealed class Fake : IFieldPlanFacts
    {
        public bool[] Up, Lamp; public readonly Dictionary<int, FieldSiteFacts> Sites = new Dictionary<int, FieldSiteFacts>();
        public Fake(int n) { Up = Enumerable.Repeat(true, n).ToArray(); Lamp = new bool[n]; }
        public int Members => Up.Length;
        public int LightBonus => 20;
        public bool Alive(int m) => m >= 0 && m < Up.Length && Up[m];
        public bool HasLight(int m) => Alive(m) && Lamp[m];
        public bool HasTool(int site, int m) => true;
        public FieldSiteFacts Site(int site) => Sites.TryGetValue(site, out var f) ? f : new FieldSiteFacts { InRoom = true, Searchable = true };
    }
    static FieldSiteFacts Obj(int noise, int turns) => new FieldSiteFacts { InRoom = true, Searchable = true, Noise = noise, Turns = turns };

    // ---- edit mode ----
    public static string Rules()
    {
        var done = new List<string>();
        // 1. The formulas.
        for (int n = 0; n <= 3; n++) Check(FieldTurnPlan.SearchNoise(n, false) == n && FieldTurnPlan.SearchNoise(n, true) == Math.Max(0, n - 1), "SearchNoise " + n);
        Check(FieldTurnPlan.DefaultTurns == 2 && FieldTurnPlan.RequiredOf(0, false) == 2 && FieldTurnPlan.RequiredOf(0, true) == 1 && FieldTurnPlan.RequiredOf(1, true) == 1
            && FieldTurnPlan.RequiredOf(2, true) == 1 && FieldTurnPlan.RequiredOf(3, true) == 2 && FieldTurnPlan.RequiredOf(3, false) == 3, "RequiredOf");
        done.Add("SearchNoise / RequiredOf");

        // 2. Roles per object (two members, the partner holds a lamp). The pace of an order changes nothing.
        int cases = 0;
        for (int n = 0; n <= 3; n++)
            for (int turns = 0; turns <= 3; turns++)
                for (int duty = 0; duty < 3; duty++)
                {
                    var f = new Fake(2); f.Lamp[1] = true; f.Sites[1] = Obj(n, turns);
                    var plan = new FieldTurnPlan(); plan.Assign(new FieldOrder { Site = 1, Lead = 0, Duty = duty });
                    bool offered = plan.CanOffer(duty, 1, 0, f), watched = duty == 1 && n > 0; var r = plan.Check(f).RunFor(1);
                    string at = $"noise {n} turns {turns} duty {duty}: ";
                    Check(offered == (duty != 1 || n > 0) && plan.CanOffer(duty, 1, 0, 0, f) == offered && plan.CanOffer(duty, 1, 0, 2, f) == offered, at + "offered " + offered);
                    Check(r != null && r.Starts && r.Pace == 1 && r.Support == (offered ? 1 : -1) && r.Watched == watched, at + "support " + r?.Support + " watched " + r?.Watched);
                    Check(r.Noise == FieldTurnPlan.SearchNoise(n, watched) && r.Required == FieldTurnPlan.RequiredOf(turns, duty == 0) && r.Bonus == (duty == 2 ? 20 : 0) && r.Completes == (r.Required == 1), at + "noise " + r.Noise + " required " + r.Required + " bonus " + r.Bonus);
                    foreach (int pace in new[] { 0, 2 })
                    {
                        var p2 = new FieldTurnPlan(); p2.Assign(new FieldOrder { Site = 1, Lead = 0, Duty = duty, Pace = pace }); var q = p2.Check(f).RunFor(1);
                        Check(q.Noise == r.Noise && q.Required == r.Required && q.Bonus == r.Bonus && q.Pace == 1, at + "pace " + pace + " changed the run");
                    }
                    cases++;
                }
        done.Add("roles × object noise × turns (" + cases + ")");
        // A lone member: nobody to help, so 함께 gives the base turns; 망보기 has nobody either.
        var lone = new Fake(1); lone.Sites[1] = Obj(3, 2); var solo = new FieldTurnPlan(); solo.Assign(new FieldOrder { Site = 1, Lead = 0 });
        var lr = solo.Check(lone).RunFor(1); Check(lr.Support < 0 && lr.Required == 2 && lr.Noise == 3 && !solo.CanOffer(1, 1, 0, lone), "Lone member: 2 turns, noise 3, no 망보기");
        done.Add("lone member");

        // 3. A running search keeps what it stored; the object's noise still applies every turn.
        var g = new Fake(2); g.Sites[1] = new FieldSiteFacts { InRoom = true, Searchable = true, Opened = true, Progress = 1, Required = 3, Pace = 1, Duty = 0, Noise = 1, Turns = 3 };
        var run = new FieldTurnPlan(); run.Assign(new FieldOrder { Site = 1, Lead = 0 }); var rr = run.Check(g).RunFor(1);
        Check(!rr.Starts && rr.Required == 3 && rr.Support < 0 && rr.Noise == 1 && !rr.Forfeits && rr.After == 2 && !rr.Completes, "Running 함께 search: stored turns, binds nobody");
        g.Sites[1] = new FieldSiteFacts { InRoom = true, Searchable = true, Opened = true, Progress = 1, Required = 2, Pace = 1, Duty = 1, Noise = 2, Turns = 2 };
        rr = run.Check(g).RunFor(1); Check(rr.Support == 1 && rr.Watched && rr.Noise == 1 && rr.Completes, "Running 망보기 keeps its watcher: 2→1");
        g.Sites[1] = new FieldSiteFacts { InRoom = true, Searchable = true, Opened = true, Progress = 1, Required = 3, Pace = 2, Duty = 1, Noise = 0, Turns = 2 };
        rr = run.Check(g).RunFor(1); Check(rr.Support < 0 && !rr.Watched && rr.Noise == 0 && rr.Required == 3 && rr.Pace == 2, "An older save's 망보기 on a silent object: no watcher, stored pace/turns kept");
        // An older save's 함께 bonus: honoured, lost on a turn the living partner does something else.
        g.Sites[1] = new FieldSiteFacts { InRoom = true, Searchable = true, Opened = true, Progress = 1, Required = 2, Pace = 1, Duty = 0, Bonus = 10, Noise = 0, Turns = 2 };
        run = new FieldTurnPlan(); run.Assign(new FieldOrder { Site = 1, Lead = 0 }); rr = run.Check(g).RunFor(1);
        Check(rr.Support == 1 && rr.Bonus == 10 && !rr.Forfeits, "Older-save 함께 bonus kept with the partner");
        run.Assign(new FieldOrder { Site = 0, Lead = 1, Solo = true }); rr = run.Check(g).RunFor(1);
        Check(rr.Forfeits && rr.Lost == 10 && rr.Bonus == 0, "Older-save 함께 bonus lost without the partner");
        done.Add("running / older saves");

        // 4. Loud: the machine's 3 in a turn at 위험도 1 is remembered; 망보기 (2) is not.
        var loud = new FieldSiteState(new FieldSiteRules(), () => 0, danger: 1, gauge: 1); loud.EndTurn(FieldTurnPlan.SearchNoise(3, false), false);
        Check(loud.Noted && loud.Remembered == A && loud.LastNoise == 3 && loud.Danger == 2 && loud.Gauge == 0, "Machine alone: loud, remembered, 위험도 2");
        var quiet = new FieldSiteState(new FieldSiteRules(), () => 0, danger: 1, gauge: 1); quiet.EndTurn(FieldTurnPlan.SearchNoise(3, true), false);
        Check(!quiet.Noted && quiet.Remembered < 0 && quiet.LastNoise == 2 && quiet.Danger == 1 && quiet.Gauge == 3, "Machine watched: 2, not loud");
        var silent = new FieldSiteState(new FieldSiteRules(), () => 0, danger: 1, gauge: 1); silent.EndTurn(FieldTurnPlan.SearchNoise(0, false), false);
        Check(!silent.Noted && silent.Gauge == 1, "A silent search adds nothing (and is not a hush)");
        done.Add("loud 3 / watched 2 / silent 0");

        // 5. The first-visit encounter by search turns (BuildSiteNoise: warn after 2, then 40 + 5/search + 5/noise, at most 45).
        Check(!ExpeditionEncounterPanel.WarnsAt(1, 9, 2, 0) && ExpeditionEncounterPanel.WarnsAt(2, 0, 2, 0) && !ExpeditionEncounterPanel.WarnsAt(2, 3, 2, 4) && ExpeditionEncounterPanel.WarnsAt(1, 0, 1, 0), "WarnsAt");
        Check(ExpeditionEncounterPanel.ChanceAt(3, 0, 2, 40, 5, 5, 45) == 40 && ExpeditionEncounterPanel.ChanceAt(4, 0, 2, 40, 5, 5, 45) == 45 && ExpeditionEncounterPanel.ChanceAt(3, 3, 2, 40, 5, 5, 45) == 45
            && ExpeditionEncounterPanel.ChanceAt(5, 2, 2, 40, 5, 5, 100) == 60 && ExpeditionEncounterPanel.ChanceAt(3, 0, 2, 40, 5, 5, 0) == 0, "ChanceAt");
        done.Add("encounter by search turns");
        return "PASS rules · " + string.Join("; ", done);
    }

    // What the game reads (SettlementScreen.prefab). Run after BuildSiteNoise.Run.
    public static string Data()
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab"); var c = go ? go.GetComponent<SettlementController>() : null;
        var a = c ? c.ArrivalPanel : null; Check(a && a.Loot && a.Rooms && a.Encounter && a.Search && a.Threat && a.Threat.Planner, "SettlementScreen references");
        var done = new List<string>(); var sites = a.Loot.Sites;
        int[] noise = { 0, 0, 3, 0, 2, 1, 0, 1, 1 }, turns = { 2, 2, 2, 0, 2, 2, 2, 2, 2 };
        Check(sites.Length == 9 && sites.Select(x => x.Noise).SequenceEqual(noise) && sites.Select(x => x.Turns).SequenceEqual(turns), "Object noise " + string.Join(",", sites.Select(x => x.Noise)) + " / turns " + string.Join(",", sites.Select(x => x.Turns)) + " (run BuildSiteNoise.Run)");
        for (int i = 0; i < 9; i++) Check(a.Loot.SiteNoise(i) == noise[i] && a.Loot.SiteTurns(i) == (turns[i] > 0 ? turns[i] : 2) && a.Loot.CanWatch(i) == (noise[i] > 0), "Loot helpers for " + i);
        Check(a.Rooms.UnlockNoise == 3, "Lock noise " + a.Rooms.UnlockNoise);
        var e = a.Encounter;
        Check(e.WarnSearches == 2 && e.BaseChance == 40 && e.ChancePerSearch == 5 && e.ChancePerNoise == 5 && e.MaximumChance == 45 && e.NoiseThreshold == 0 && e.GraceSearches == 3,
            "Encounter " + e.WarnSearches + "/" + e.BaseChance + "/" + e.ChancePerSearch + "/" + e.ChancePerNoise + "/" + e.MaximumChance + "/" + e.NoiseThreshold + "/" + e.GraceSearches);
        done.Add("noise 0,0,3,–,2,1,0,1,1 · 2 turns · lock 3 · encounter 2/40/5/5/45");

        // The 07 window: the noise line where the pace heading was, no pace buttons, the rows moved up.
        var s = a.Search; var ws = s.Workspace.transform;
        float Y(Transform t) => -((RectTransform)t).anchoredPosition.y;
        Check(s.ObjectNoise && s.ObjectNoise.name == "ObjectNoise" && s.ObjectNoise.gameObject.activeSelf && Mathf.Abs(Y(s.ObjectNoise.transform) - 433) < .5f, "Noise line (run BuildSiteNoise.Run)");
        Check(ws.Find("PaceHeading") == null && (s.Paces == null || s.Paces.All(b => !b || !b.gameObject.activeSelf)), "Pace row hidden");
        Check(Mathf.Abs(Y(ws.Find("WorkerHeading")) - 478) < .5f && Mathf.Abs(Y(ws.Find("Workers")) - 518) < .5f && Mathf.Abs(Y(ws.Find("DutyHeading")) - 656) < .5f && s.Duties.All(d => Mathf.Abs(Y(d.transform) - 695) < .5f), "Rows moved up");
        bool Shown(Transform t) { for (; t && t != s.transform; t = t.parent) if (!t.gameObject.activeSelf) return false; return true; }
        var words = s.GetComponentsInChildren<Text>(true).Where(t => Shown(t.transform) && PaceWord(t.text) != null).Select(t => t.name + " '" + t.text + "'").ToList();
        Check(words.Count == 0, "Pace words in the 07 window: " + string.Join(", ", words));
        done.Add("07 window: noise line, no pace row, rows moved up");

        // Texts: FieldTurnTexts, the panel, the preview and the warning line carry no pace words; the owned ones equal the code defaults.
        var tx = a.Threat.Planner.Texts; var want = new FieldTurnTexts();
        foreach (var name in new[] { "CostTogether", "CostWatch", "CostTogetherBonus", "CostWatchSilent", "NoticeLocked", "ReviewTitle" })
        { var f = typeof(FieldTurnTexts).GetField(name); Check(f != null && (string)f.GetValue(tx) == (string)f.GetValue(want), "FieldTurnTexts." + name + " '" + f?.GetValue(tx) + "' (run BuildSiteNoise.Run)"); }
        Check(typeof(FieldTurnTexts).GetField("PaceNames") == null, "PaceNames still a field");
        var stray = new List<string>();
        void Scan(object o, string owner) { foreach (var f in o.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)) { if (f.FieldType == typeof(string) && f.Name != "CostTogetherBonus" && PaceWord((string)f.GetValue(o)) != null) stray.Add(owner + "." + f.Name); if (f.FieldType == typeof(string[]) && ((string[])f.GetValue(o) ?? new string[0]).Any(x => PaceWord(x) != null)) stray.Add(owner + "." + f.Name); } }
        Scan(tx, "FieldTurnTexts"); Scan(s, "ExpeditionSearchPanel"); Scan(s.DropPreview, "SearchDropPreview");
        var w = a.GetComponentInChildren<FieldTurnWarning>(true); Check(w, "FieldTurnWarning"); Scan(w, "FieldTurnWarning");
        Check(stray.Count == 0, "Pace words: " + string.Join(", ", stray));
        Check(!w.LineFirstVisit.Contains("소리가 커서") && w.LineFirstVisit == "오래 뒤지는 사이 무언가 다가올 수 있습니다", "First-visit warning line: " + w.LineFirstVisit);
        Check(string.Format(tx.CostWatch, "B", 3, 2).Contains("3→2") && string.Format(tx.CostTogether, "B", 0).Contains("1턴 빨리"), "Role lines: " + tx.CostWatch + " / " + tx.CostTogether);
        done.Add("texts without 빠름/보통/정밀/속도");
        return "PASS data · " + string.Join("; ", done);
    }

    // ---- play mode ----
    static async Task Tap(Button button)
    {
        Check(button && button.IsActive() && button.IsInteractable(), "Unavailable " + (button ? button.name : "null"));
        var r = (RectTransform)button.transform; Canvas.ForceUpdateCanvases();
        var data = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(button.GetComponentInParent<Canvas>().worldCamera, r.TransformPoint(r.rect.center)), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Selectable>() == button, "Blocked " + button.name + " by " + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler); await Task.Delay(100);
    }
    static async Task Until(Func<bool> done, int ms, string what) { var w = Stopwatch.StartNew(); while (!done()) { if (w.ElapsedMilliseconds > ms) throw new Exception("Timeout: " + what); await Task.Delay(30); } }
    static string Label(Button b) => b.GetComponentInChildren<Text>().text;
    static SettlementController Owner() { var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Run VerifySiteNoise.Enter first (a fresh game)"); return c; }
    static async Task<ExpeditionArrivalPanel> Depart(SettlementController c)
    {
        await Tap(c.Exit); foreach (var card in c.ExpeditionPanel.Cards) if (!card.Check.gameObject.activeSelf) await Tap(card.Button);
        await Tap(c.ExpeditionPanel.Pack); await Tap(c.PackingPanel.Ready); await Tap(c.PackingPanel.Depart); await Task.Delay(1100);
        Check(c.ArrivalPanel.IsOpen && !c.ArrivalPanel.InTransit, "Arrived"); return c.ArrivalPanel;
    }
    static async Task CloseLoot(ExpeditionArrivalPanel a) { if (a.Loot.IsOpen) { await Tap(a.Loot.Back); await Task.Delay(120); if (a.Loot.LeaveReview.activeSelf) await Tap(a.Loot.LeaveConfirm); await Task.Delay(150); } }
    // The 07 window (read only since 말 놓기: what a right press on the object opens).
    static async Task Open07(ExpeditionArrivalPanel a, int site) { a.Inspect(site); await Until(() => a.Search.IsOpen, 2000, "07 window " + site); await Task.Delay(150); Check(a.Search.ReadOnly, "07 is read only"); }
    static async Task Ready(ExpeditionArrivalPanel a) => await Until(() => FieldPawnTest.Ready(a), 3000, "board ready");
    // '턴 진행' (a member left idle hushes: AutoAccept).
    static async Task Turn(ExpeditionArrivalPanel a) { await Ready(a); await Task.Delay(250); await Tap(a.Threat.Planner.TurnButton); await Task.Delay(300); }
    static FieldRun Run(ExpeditionArrivalPanel a, int site) => FieldPawnTest.Check(a)?.RunFor(site);
    // No pace word on any shown text of the 07 window (its review and the finds preview included).
    static void NoPace(ExpeditionSearchPanel s, string when)
    {
        var words = s.View.GetComponentsInChildren<Text>().Where(t => PaceWord(t.text) != null).Select(t => t.name + " '" + t.text.Replace("\n", " / ") + "'").ToList();
        Check(words.Count == 0, when + ": pace words " + string.Join(", ", words));
        Check(s.Paces == null || s.Paces.All(b => !b || !b.gameObject.activeInHierarchy), when + ": pace buttons shown");
    }
    static void Prybar(SettlementController c, ExpeditionArrivalPanel a, int member) { if (c.InventoryPanel.CountFor(a.Participants[member], "prybar") == 0) Check(c.InventoryPanel.TransferField(a.Participants[member], "prybar", 1, true), "Prybar fixture (" + a.Participants[member].Name + ")"); }

    // New game → settlement (the fixed opening pair), intro lifted (as VerifyFieldTurnPlan.Enter).
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

    // First visit (it sleeps): pawns placed on objects, '턴 진행' = one search turn (말 놓기); the 07 window only shows the object's noise.
    public static async Task<string> FirstVisit()
    {
        FieldIdleConfirm.AutoAccept = true;
        var c = Owner(); var a = await Depart(c); var t = a.Threat; var pl = t.Planner; var s = a.Search; var e = a.Encounter; var px = pl.PlaceTexts; var log = new List<string>();
        if (c.Opening) c.Opening.State.Enabled = false; // fixture: the tutorial guide stays out of the way (the search rules are the same)
        Check(!pl.Active && pl.Placing && t.State != null && t.State.Asleep && a.Rooms.CurrentRoom == A, "First visit asleep in the arcade (a fresh game: Enter)");
        Check(a.Participants.Count == 2 && a.Participants.All(p => p.Health > 0), "Fixture: two members up");
        Check(e.WarnSearches == 2 && e.NoiseThreshold == 0 && e.Searches == 0 && !e.Warned, "Encounter by search turns (run BuildSiteNoise.Run): warn " + e.WarnSearches + " threshold " + e.NoiseThreshold);
        await Ready(a);
        // 1. The crate: silent, 함께 finishes its 2 turns in one, 망보기 greyed out ('소음 없음').
        int noise = a.Rooms.Noise, turns = a.Rooms.Turns;
        await Open07(a, 0);
        Check(s.ObjectNoise && s.ObjectNoise.text == string.Format(s.NoiseQuietLine, 0, 2), "Crate noise line: " + (s.ObjectNoise ? s.ObjectNoise.text : "missing (run BuildSiteNoise.Run)"));
        NoPace(s, "crate"); await Tap(s.Back);
        var lead = FieldPawnTest.Option(a, 0, FieldPawnTest.SearchKey(0), FieldSpotKind.Lead);
        Check(lead != null && lead.Label == string.Format(px.Lead, 2, 0), "Crate pin: " + (lead != null ? lead.Label : "none"));
        Check(FieldPawnTest.Lead(a, 0, 0), "A on the crate");
        var join = FieldPawnTest.Option(a, 1, FieldPawnTest.SearchKey(0), FieldSpotKind.Join);
        Check(join != null && join.Label == string.Format(px.JoinTogether, 1), "Co-op pin: " + (join != null ? join.Label : "none"));
        Check(FieldPawnTest.Join(a, 1, 0), "B beside A");
        var chips = FieldPawnTest.Rules(a).ChipsFor(1);
        Check(chips.Count == 3 && chips[0].On && !chips[1].Enabled && chips[1].Why == px.WhyNoNoise, "Silent crate: 함께, 망보기 off ('" + (chips.Count > 1 ? chips[1].Why : "") + "')");
        var run = Run(a, 0); Check(run != null && run.Role == FieldAction.Together && run.Required == 1 && run.Noise == 0, "Crate co-op: 1 turn, silent");
        await Open07(a, 0);
        Check(s.DropPreview.Summary.text.StartsWith(string.Format(s.DropPreview.SummaryQuiet, 1, a.Rooms.MinutesPerTurn)) && s.PlacedLine.Contains(a.Participants[1].Name), "Crate preview: " + s.DropPreview.Summary.text + " / " + s.PlacedLine);
        NoPace(s, "crate placed"); await Tap(s.Back);
        await Turn(a);
        var crate = a.Loot.State(0);
        Check(a.Rooms.Turns == turns + 1 && a.Rooms.Noise == noise && crate.Complete && crate.Progress == 1 && crate.Required == 1 && crate.Pace == 1, "Crate: one turn, no noise, done (" + crate.Progress + "/" + crate.Required + ", noise " + (a.Rooms.Noise - noise) + ")");
        Check(e.Searches == 1 && !e.Warned && FieldTurnWarning.NextSearchChance(a) == 0, "One search turn: no warning yet");
        await CloseLoot(a); if (s.IsOpen) await Tap(s.Back);
        log.Add("crate: noise +0, 1 turn with 함께, no 망보기");
        // 2. The machine (prybar fixture): 3 every turn; 망보기 offered (3→2, two turns), 함께 one loud turn.
        Prybar(c, a, 0); noise = a.Rooms.Noise; turns = a.Rooms.Turns; await Ready(a);
        await Open07(a, 2);
        Check(s.ObjectNoise.text == string.Format(s.NoiseLine, 3, 2) + s.NoiseLoudSuffix, "Machine noise line: " + s.ObjectNoise.text);
        NoPace(s, "machine"); await Tap(s.Back);
        Check(FieldPawnTest.Lead(a, 0, 2) && FieldPawnTest.Join(a, 1, 2, 1), "A on the machine, B watching");
        run = Run(a, 2); Check(run != null && run.Role == FieldAction.Watch && run.Noise == 2 && run.Required == 2, "망보기 on the machine: noise " + (run != null ? run.Noise : -1) + ", " + (run != null ? run.Required : -1) + " turns");
        Check(FieldPawnTest.Choose(a, 1, 0), "Back to 함께"); run = Run(a, 2);
        Check(run != null && run.Role == FieldAction.Together && run.Noise == 3 && run.Required == 1, "함께 on the machine: one loud turn");
        await Open07(a, 2); Check(s.DropPreview.Summary.text.StartsWith(string.Format(s.DropPreview.SummaryLine, 1, a.Rooms.MinutesPerTurn, 3)), "Machine preview: " + s.DropPreview.Summary.text); await Tap(s.Back);
        await Turn(a);
        Check(a.Rooms.Turns == turns + 1 && a.Rooms.Noise == noise + 3 && a.Loot.State(2).Complete, "Machine: one turn, noise +3 (" + (a.Rooms.Noise - noise) + ")");
        log.Add("machine: noise +3 (망보기 would be 2)");
        // 3. Two search turns: warned (that turn never rolls); the next search's chance by search turns plus the noise so far.
        Check(e.Searches == 2 && e.Warned && e.Rolls == 0 && !e.IsOpen, "Warned after two search turns, no roll yet (" + e.Searches + ", rolls " + e.Rolls + ")");
        int chance = FieldTurnWarning.NextSearchChance(a), want = Mathf.Clamp(e.BaseChance + a.Rooms.Noise * e.ChancePerNoise, 0, e.MaximumChance);
        Check(chance == want && chance == e.ChanceAt(3, a.Rooms.Noise) && chance >= e.BaseChance, "Next search " + chance + "% (want " + want + ")");
        await CloseLoot(a); if (s.IsOpen) await Tap(s.Back);
        c.InventoryPanel.TransferField(a.Participants[0], "prybar", 1, false);
        log.Add("warned after 2 search turns, next " + chance + "%");
        // 4. A running 망보기 search goes on after its watcher is down (a first-visit fight can knock the partner out): the lead's pawn
        //    stays and searches on alone at the object's full noise; a new 망보기 is not offered to a lone member.
        //    Fixtures: the table made noisy (1; the corridor crate is a room away), no encounter roll (MaximumChance 0), the partner down.
        var l = a.Loot; var watcher = a.Participants[1]; int tableNoise = l.Sites[1].Noise, maxChance = e.MaximumChance, health = watcher.Health;
        try
        {
            l.Sites[1].Noise = 1; e.MaximumChance = 0; noise = a.Rooms.Noise; turns = a.Rooms.Turns; await Ready(a);
            Check(FieldPawnTest.Lead(a, 0, 1) && FieldPawnTest.Join(a, 1, 1, 1) && l.CanOfferWatch(1), "Noisy table: B watches with two members up");
            run = Run(a, 1); Check(run != null && run.Role == FieldAction.Watch && run.Noise == 0 && run.Required == 2, "망보기 on the table: noise 0, 2 turns");
            await Turn(a);
            var table = l.State(1);
            Check(!e.IsOpen && table.Progress == 1 && table.Required == 2 && table.Duty == 1 && !table.Complete && a.Rooms.Noise == noise, "망보기 table 1/2, noise +0 (" + table.Progress + "/" + table.Required + ", +" + (a.Rooms.Noise - noise) + ")");
            watcher.Health = 0; a.RefreshFieldBags(); await Task.Delay(150);
            run = Run(a, 1);
            Check(run != null && run.Lead == 0 && run.Support < 0 && run.Noise == 1 && l.NoiseFor(1, 1) == 1 && !l.CanOfferWatch(1), "Watcher down: the running 망보기 search goes on alone at noise 1 · " + FieldPawnTest.Describe(a));
            await Turn(a);
            Check(table.Complete && table.Progress == 2 && a.Rooms.Noise == noise + 1 && a.Rooms.Turns == turns + 2, "Finished alone: 2/2, noise +1 (" + table.Progress + "/" + table.Required + ", +" + (a.Rooms.Noise - noise) + ")");
            await CloseLoot(a); if (s.IsOpen) await Tap(s.Back);
        }
        finally { watcher.Health = health; l.Sites[1].Noise = tableNoise; e.MaximumChance = maxChance; a.RefreshFieldBags(); }
        log.Add("망보기 after the watcher is down: finished alone at the full noise");
        return "PASS first visit · " + string.Join(" · ", log);
    }

    // Site board (a later visit, 위험도 1, gauge 1), the opening pair.
    // A JSON copy the way the game loads it: JsonUtility writes a missing report as an empty one, and loading drops that again.
    static CampaignSaveData Clone(CampaignSaveData d)
    {
        var x = JsonUtility.FromJson<CampaignSaveData>(JsonUtility.ToJson(d)); var r = x.ReturnReport;
        if (r != null && string.IsNullOrEmpty(r.Destination) && string.IsNullOrEmpty(r.Body) && (r.Members == null || r.Members.Length == 0)) x.ReturnReport = null;
        return x;
    }
    public static async Task<string> Board()
    {
        FieldIdleConfirm.AutoAccept = true; // members left idle hush without the question (VerifyIdleConfirm tests it)
        var c = Owner(); var baseSave = CampaignPersistence.Capture(c); // a valid current save (Capture refuses during a field trip)
        var a = await Depart(c); var t = a.Threat; var pl = t.Planner; var s = a.Search; var tx = pl.Texts; var log = new List<string>();
        if (c.Opening) c.Opening.State.Enabled = false; // a later visit comes after the opening chapter
        t.ReviewWake(1, 1, 0); await Task.Delay(250);
        Check(pl.Active && a.Rooms.CurrentRoom == A && a.Participants.Count == 2 && a.Participants.All(p => p.Health > 0), "Board awake in the arcade with two members (a fresh game: Enter)");
        string nameA = a.Participants[0].Name, nameB = a.Participants[1].Name;

        await Ready(a); var px = pl.PlaceTexts;
        // 1. The silent crate: 망보기 greyed out under the helper, the noise line says why.
        await Open07(a, 0);
        Check(s.ObjectNoise.text == string.Format(s.NoiseQuietLine, 0, 2), "Crate noise line · " + s.ObjectNoise.text);
        NoPace(s, "board crate"); await Tap(s.Back);
        Check(FieldPawnTest.Lead(a, 0, 0) && FieldPawnTest.Join(a, 1, 0), "Two pawns on the crate");
        var chips = FieldPawnTest.Rules(a).ChipsFor(1); Check(chips.Count == 3 && !chips[1].Enabled && chips[1].Why == px.WhyNoNoise, "Crate: no 망보기");
        FieldPawnTest.Clear(a);

        // 2. 함께 finishes the 2-turn table in one turn (A leads, B helps: everyone busy).
        int turns = a.Rooms.Turns;
        Check(FieldPawnTest.Coop(a, 1), "Two pawns on the table"); var run = Run(a, 1);
        Check(run != null && run.Lead == 0 && run.Support == 1 && run.Role == FieldAction.Together && run.Required == 1 && run.Noise == 0 && FieldIdleConfirm.IdleMembers(pl).Count == 0, "Table 함께: 1 turn, silent, nobody idle");
        await Open07(a, 1); Check(s.PlacedLine.Contains(nameA) && s.PlacedLine.Contains(nameB), "07 names both: " + s.PlacedLine); NoPace(s, "table"); await Tap(s.Back);
        await Turn(a); await CloseLoot(a); if (s.IsOpen) await Tap(s.Back);
        a.Loot.Peek(1, out var table);
        Check(a.Rooms.Turns == turns + 1 && table != null && table.Complete && table.Progress == 1 && table.Required == 1 && table.Pace == 1 && table.Duty == 0 && table.Bonus == 0 && t.State.LastNoise == 0 && pl.LastMismatch == "", "Table: 1 turn with 함께, silent");
        log.Add("함께: the table in 1 turn");

        // 3. 망보기 on the machine: 3→2 (the plan's preview; not run).
        Prybar(c, a, 0); Prybar(c, a, 1); turns = a.Rooms.Turns; await Ready(a);
        await Open07(a, 2); Check(s.ObjectNoise.text == string.Format(s.NoiseLine, 3, 2) + s.NoiseLoudSuffix, "Machine noise line · " + s.ObjectNoise.text); await Tap(s.Back);
        Check(FieldPawnTest.Lead(a, 0, 2) && FieldPawnTest.Join(a, 1, 2, 1), "A on the machine, B watching");
        run = Run(a, 2); Check(run != null && run.Role == FieldAction.Watch && run.Noise == 2 && run.Required == 2, "망보기 3→2");
        await Open07(a, 2); Check(s.DropPreview.Summary.text.StartsWith(string.Format(s.DropPreview.SummaryLine, 2, 2 * a.Rooms.MinutesPerTurn, 2)), "망보기 preview: " + s.DropPreview.Summary.text); await Tap(s.Back);
        Check(pl.Chip.text.Contains(string.Format(tx.ChipNoise, 2, "")) && !pl.Chip.text.Contains(tx.ChipNoted), "망보기 chip: " + pl.Chip.text);
        FieldPawnTest.Clear(a);
        Check(pl.Plan.Find(2) == null && a.Rooms.Turns == turns, "Preview only: nothing placed, no time");
        log.Add("망보기: the machine 3→2, not loud");

        // 4. The machine alone while the other member's pawn listens at the corridor door: noise 3, loud, the arcade remembered.
        Check(FieldPawnTest.Listen(a, 1, C) && pl.Plan.Listens.Count == 1, "B listens at the exit"); int ear = pl.Plan.Listens[0].Member, lead = 1 - ear;
        Check(FieldPawnTest.Lead(a, lead, 2), "A alone on the machine"); run = Run(a, 2);
        Check(run != null && run.Support < 0 && run.Noise == 3 && pl.Chip.text.Contains(string.Format(tx.ChipNoise, 3, "")) && pl.Chip.text.Contains(tx.ChipNoted), "Loud chip: " + pl.Chip.text);
        await Turn(a); await CloseLoot(a); if (s.IsOpen) await Tap(s.Back);
        var st = t.State; a.Loot.Peek(2, out var machine);
        Check(a.Rooms.Turns == turns + 1 && st.LastNoise == 3 && st.Remembered == A && st.Danger == 2 && st.Gauge == 0 && pl.LastMismatch == "", "Loud turn: noise " + st.LastNoise + " remembered " + st.Remembered + " 위험도 " + st.Danger + " gauge " + st.Gauge + " " + pl.LastMismatch);
        Check(machine != null && machine.Progress == 1 && machine.Required == 2 && machine.Duty == 0 && machine.Pace == 1 && !machine.Complete, "Machine 1/2");
        log.Add("machine alone: noise 3, remembered, 위험도 2");

        // 5. The save round trip of the one-turn 함께 search (and the running machine).
        var save = Clone(baseSave); save.Searches = a.Loot.ExportSearches();
        var saved = save.Searches.Single(r => r.Id == CampaignPersistence.SiteIds[1]);
        Check(saved.Progress == 1 && saved.Required == 1 && saved.Complete && saved.Pace == 1 && saved.Required != saved.Pace + 1, "Saved table " + saved.Progress + "/" + saved.Required + " pace " + saved.Pace);
        CampaignPersistence.Validate(save, c);
        var back = Clone(save); CampaignPersistence.Validate(back, c);
        var bad = Clone(save); bad.Searches.Single(r => r.Id == CampaignPersistence.SiteIds[2]).Required = 0;
        bool refused = false; try { CampaignPersistence.Validate(bad, c); } catch (InvalidOperationException) { refused = true; }
        Check(refused, "A started search without turns was accepted");
        a.Loot.RestoreSaved(back);
        Check(a.Loot.State(1).Complete && a.Loot.State(1).Required == 1 && a.Loot.State(2).Progress == 1 && a.Loot.State(2).Required == 2 && !a.Loot.State(2).Complete, "Restored searches");
        log.Add("save: 함께 table 1/1 valid and restored, machine 1/2");
        return "PASS board · " + string.Join(" · ", log);
    }
}
