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

// Balance pass 1 (기획/밸런스-1차.md) on the real screen, play mode after VerifyFieldTurnPlan.Enter:
// the unknown convenience store, the first-visit retreat from the arcade (home), the one-time site board intro on the second visit,
// the 탄약·붕대 HUD, the danger/hunt status lines, the incoming warning in the move popup, hiding on the board as a hushed turn,
// the den shelf closed on the turn the thing comes home, and rest for a downed member.
// 말 놓기 (2026-09-25): the move is every pawn at the door (FieldPawnTest), whose move preview warns of a meeting; the den shelf is a
// silhouette at the office door only while the den stays empty (no door popup).
// Loot() (edit mode) checks the loot tables BuildBalance1 owns, as SettlementScreen.prefab resolves them.
public static class VerifyBalance1
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    const int A = FieldSiteState.Arcade, C = FieldSiteState.Corridor;

    public static async Task<string> Showcase()
    {
        FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Run VerifyFieldTurnPlan.Enter first");
        var log = new List<string>();

        // 1. The convenience store: no route yet, so packing is not offered.
        await Tap(c.Exit); var plan = c.ExpeditionPanel; Check(plan.IsOpen, "Plan open");
        int store = Array.FindIndex(plan.Destinations, d => d.Id == "store"), mall = Array.FindIndex(plan.Destinations, d => d.Id == "mall");
        plan.Markers[store].onClick.Invoke(); await Task.Delay(150);
        Check(plan.Travel.text == "이동 경로 미확인" && plan.Risk.text.Contains("미확인") && !plan.Pack.interactable, "Store closed: " + plan.Travel.text + " / " + plan.Risk.text);
        await Still(c, "01-편의점-경로미확인");
        plan.Markers[mall].onClick.Invoke(); await Task.Delay(100); await Tap(plan.Back); await Task.Delay(150);
        log.Add("store unknown");

        // 2. First visit: the HUD and a meeting in the arcade, left through the exit (home, finds kept).
        var a = await Depart(c); var t = a.Threat; var e = a.Encounter;
        Check(t.State.Asleep && a.Resources.text.Contains("탄약") && a.Resources.text.Contains("붕대"), "HUD: " + a.Resources.text);
        e.OpenThreat("가까운 곳에서 무언가 움직입니다.\n수색을 멈추고 몸을 낮춥니다.", "무언가 1", null); await Task.Delay(200);
        var retreat = e.Retreat.GetComponentsInChildren<Text>(true).OrderByDescending(x => x.fontSize).Select(x => x.text).ToArray();
        Check(retreat[0] == "출구로 빠져나가기" && retreat[1].StartsWith("거점으로 귀환") && e.EnemyCount == 1, "Arcade retreat: " + string.Join(" / ", retreat));
        await Still(a, "02-첫방문-조우-출구");
        await Tap(e.Retreat); Check(e.ReviewBody.text.StartsWith("출구로 빠져나가 거점으로"), "Retreat review: " + e.ReviewBody.text);
        await Still(a, "03-출구-확인");
        await Tap(e.Confirm); await Task.Delay(600);
        Check(!a.IsOpen && c.ReturnPanel && c.ReturnPanel.View.activeSelf, "Home after the exit");
        await Still(c, "04-출구-귀환보고");
        await Tap(c.ReturnPanel.Back); await Task.Delay(200);
        log.Add("arcade exit → home");

        // 3. Second visit: the site board explains itself once.
        if (c.Opening) c.Opening.State.Enabled = false;
        a = await Depart(c); await Until(() => a.Popup.activeSelf, 2000, "intro");
        Check(!t.State.Asleep && t.Planner.Active && a.PopupTitle.text == t.IntroTitle, "Intro: " + a.PopupTitle.text);
        Fits(a.PopupTitle, a.PopupBody);
        await Still(a, "05-두번째방문-장소판-안내");
        await Tap(a.PopupBack); await Task.Delay(250); Check(!a.Popup.activeSelf, "Intro once");
        log.Add("intro once");

        // 4. Status lines: 위험도 rises (what it means now), then the hunt.
        t.ReviewWake(1, 2, 0); await Task.Delay(150); a.Rooms.SpendTurn(2, false); await Task.Delay(250);
        Check(t.State.Danger == 2 && a.Status.text.Contains("위험도가 올랐습니다") && a.Status.text.Contains("기억한 방이 있으면"), "Danger line: " + a.Status.text);
        await Still(a, "06-위험도-오름");
        t.ReviewWake(2, 3, 0); await Task.Delay(150); a.Rooms.SpendTurn(1, false); await Task.Delay(250);
        Check(t.State.Danger == 3 && a.Status.text == t.HuntLine, "Hunt line: " + a.Status.text);
        await Still(a, "07-추적-시작");
        // Hunting, it steps into the corridor and heads here: everyone at the door, the move's preview says it leads straight into it.
        a.Rooms.SpendTurn(0, true); await Task.Delay(200); Check(t.State.ResidentRoom == C && t.State.Incoming, "Incoming from the corridor");
        await Until(() => FieldPawnTest.Ready(a), 3000, "board ready"); Check(FieldPawnTest.Gather(a, C), "Everyone at the door: " + FieldPawnTest.Describe(a)); await Task.Delay(200);
        var view = a.Main.GetComponentInChildren<FieldMoveQueueView>(true);
        Check(view && a.Rooms.QueuedMoveOutlook().Encounter && t.Planner.Chip.text.Contains(view.ChipMeets), "Move warns: " + t.Planner.Chip.text);
        await Still(a, "08-이동-경고");
        FieldPawnTest.Clear(a); await Task.Delay(150); Check(!a.Rooms.HasQueuedMove, "Pawns taken off the door: no move");
        log.Add("danger/hunt/move warning");

        // 5. A meeting on the board: hiding is one hushed turn (no dice); at 위험도 2 it passes by.
        t.ReviewWake(1, 0, 0); await Task.Delay(150);
        a.Rooms.SpendTurn(4, false); Check(t.State.Danger == 2 && t.State.Remembered == A && t.State.Next == C, "Lured");
        for (int i = 0; i < 4 && !e.IsOpen; i++) { a.Rooms.SpendTurn(0, false); await Task.Delay(120); }
        Check(e.IsOpen && e.Hint.text.Contains("위험도 2 이하면 지나감"), "Board meeting: " + e.Hint.text);
        await Still(a, "09-장소판-조우");
        int turns = a.Rooms.Turns; await Tap(e.Wait); Check(e.ReviewBody.text.StartsWith("숨죽여 기다리기 · 1턴"), "Hide review: " + e.ReviewBody.text);
        await Still(a, "10-숨기-확인");
        await Tap(e.Confirm); await Task.Delay(300);
        Check(!e.IsOpen && a.Rooms.Turns == turns + 1 && t.State.PassedBy && t.State.Visible, "Passed by after one hushed turn");
        await Still(a, "11-숨기-지나감");
        log.Add("board hide = hushed turn, passed by");

        // 6. The den shelf: open while it is away, closed on the turn it comes home.
        t.ReviewWake(0, 0, 0); await Task.Delay(150); await Move(a); Check(a.Rooms.CurrentRoom == C, "In the corridor");
        t.State.Driven(); t.Refresh(); int site = t.DenSite;
        for (int i = 0; i < t.Rules.GoneTurns - 2; i++) { a.Rooms.SpendTurn(0, true); await Task.Delay(100); }
        Check(t.CanSearchSite(site) && FieldPawnTest.Option(a, 0, FieldPawnTest.SearchKey(site))?.Enabled == true, "Shelf open while it is away (its silhouette at the office door)");
        a.Rooms.SpendTurn(0, true); await Task.Delay(200);
        Check(t.State.DenEmpty && !t.CanSearchSite(site) && a.Status.text == t.DenReturning, "Shelf closed on its way home: " + a.Status.text);
        Check(FieldPawnTest.Option(a, 0, FieldPawnTest.SearchKey(site)) == null && !a.Popup.activeSelf && !a.Search.IsOpen, "No shelf silhouette while it comes home");
        Check(a.Main.GetComponentsInChildren<FieldThreatMarker>(false).Any(m => m.GetComponentsInChildren<Text>(false).Any(x => x.text == t.DenComing)), "Den marker says it is coming home");
        foreach (var m in a.Main.GetComponentsInChildren<FieldThreatMarker>(false)) if (m.Label && m.Paper) Check(m.Label.preferredWidth <= m.Paper.rectTransform.rect.width - 8, "Marker overflow: " + m.Label.text + " " + m.Label.preferredWidth + "/" + m.Paper.rectTransform.rect.width);
        await Still(a, "12-관리실-돌아오는중");
        a.Rooms.SpendTurn(0, true); await Task.Delay(150); Check(!t.State.DenEmpty, "Home again");
        log.Add("den shelf closed on the returning turn");

        // 7. Home: a downed member can rest (back to 체력 1).
        Check(a.FinishReturn(), "Return"); await Task.Delay(500); if (c.ReturnPanel.View.activeSelf) await Tap(c.ReturnPanel.Back);
        var downed = c.Campaign.Party.ElementAt(1); int health = downed.Health; downed.Health = 0; c.RefreshMembers();
        try
        {
            c.WorkPanel.Open(); await Task.Delay(300);
            var row = c.WorkPanel.GetComponentsInChildren<Text>(false).First(x => x.text == "중상");
            await Tap(row.GetComponentInParent<Button>()); await Task.Delay(200);
            var texts = c.WorkPanel.GetComponentsInChildren<Text>(false).Select(x => x.text).ToArray();
            Fits(c.WorkPanel.GetComponentsInChildren<Text>(false).Where(x => x.text == "중상" || x.text == t.DenComing).ToArray());
            Check(texts.Contains("중상 · 휴식하면 체력 1로 회복") && texts.Any(x => x.Contains("완료 시 체력 +1")), "Downed rest: " + string.Join(" | ", texts.Where(x => x.Contains("체력") || x.Contains("중상"))));
            await Still(c, "13-중상-휴식");
            c.WorkPanel.Close();
        }
        finally { downed.Health = health; c.RefreshMembers(); }
        log.Add("downed member can rest");
        return "PASS balance 1 · " + string.Join(" · ", log) + " · stills " + Shots;
    }

    // Edit mode (no Play): the loot tables as the game reads them from SettlementScreen.prefab (BuildBalance1 step 3 and 5, 기획/밸런스-1차.md §3-3).
    // Run after BuildBalance1.Run and after any re-apply chain that touches loot.
    public static string Loot()
    {
        const string path = "Assets/Prefabs/Settlement/SettlementScreen.prefab";
        var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path); var c = go ? go.GetComponent<SettlementController>() : null;
        Check(c && c.ArrivalPanel && c.ArrivalPanel.Loot && c.CraftPanel && c.InventoryPanel, "SettlementScreen references");
        var a = c.ArrivalPanel; var sites = a.Loot.Sites;
        Check(sites != null && sites.Length == 9, "Sites.Length " + (sites == null ? 0 : sites.Length));
        int[] rooms = { 0, 0, 0, -1, 1, 1, 2, 2, 1 };
        Check(sites.Select(s => s.Room).SequenceEqual(rooms), "Rooms " + string.Join(",", sites.Select(s => s.Room)));
        for (int i = 0; i < sites.Length; i++) Check(i == 2 || i == 4 ? sites[i].RequiredTool == "prybar" : string.IsNullOrEmpty(sites[i].RequiredTool), "RequiredTool[" + i + "] " + sites[i].RequiredTool);
        var want = new Dictionary<int, string>
        {
            { 0, "wood 9@100 wood 2@50 scrap 7@100 scrap 1@50 cloth 3@100" },
            { 1, "food 2@100 food 2@70 water 1@100 water 2@70 wood 2@75 scrap 2@60" },
            { 2, "scrap 3@85 ammo 2@55 nails 2@60" },            // 오락기 (ExpeditionArrivalPanel, unchanged)
            { 3, "" },
            { 4, "scrap 3@85 nails 3@75" },
            { 5, "wood 4@75 scrap 3@75 cloth 3@70 raw-water 3@75" },
            { 6, "can 3@85 water 3@80 bandage 1@55 ammo 1@35" },
            { 7, "wood 8@90 rope 2@80 nails 3@75 cloth 2@65" },
            { 8, "rope 2@75 nails 2@60 bandage 2@70 ammo 2@70" }, // 관리실 선반 (ExpeditionArrivalPanel, unchanged)
        };
        foreach (var kv in want) Check(Drops(sites[kv.Key].Drops) == kv.Value, "Sites[" + kv.Key + "] " + Drops(sites[kv.Key].Drops) + " ≠ " + kv.Value);
        // SettlementScreen must not own the Arrival-level sites.
        var yaml = File.ReadAllText(path);
        Check(!yaml.Contains("Sites.Array.data[2].") && !yaml.Contains("Sites.Array.data[8]."), "SettlementScreen overrides Sites[2]/[8] (owned by ExpeditionArrivalPanel)");

        // 속도 삭제와 사물 소음 (2026-09-25, BuildSiteNoise.Run): each object's noise per search turn and base turns, the lock, the first-visit encounter.
        int[] noise = { 0, 0, 3, 0, 2, 1, 0, 1, 1 }, turns = { 2, 2, 2, 0, 2, 2, 2, 2, 2 };
        Check(sites.Select(s => s.Noise).SequenceEqual(noise) && sites.Select(s => s.Turns).SequenceEqual(turns), "Noise " + string.Join(",", sites.Select(s => s.Noise)) + " / turns " + string.Join(",", sites.Select(s => s.Turns)) + " (run BuildSiteNoise.Run)");
        Check(a.Rooms && a.Rooms.UnlockNoise == 3, "UnlockNoise " + (a.Rooms ? a.Rooms.UnlockNoise : -1));
        var enc = a.Encounter;
        Check(enc && enc.WarnSearches == 2 && enc.BaseChance == 40 && enc.ChancePerSearch == 5 && enc.ChancePerNoise == 5 && enc.MaximumChance == 45 && enc.NoiseThreshold == 0 && enc.GraceSearches == 3,
            "First-visit encounter " + (enc ? enc.WarnSearches + "/" + enc.BaseChance + "/" + enc.ChancePerSearch + "/" + enc.ChancePerNoise + "/" + enc.MaximumChance + "/" + enc.NoiseThreshold + "/" + enc.GraceSearches : "missing"));

        // The opening chain (창고·작업대 복구, 밧줄, 못 ×2, 지렛대) from the real recipes: raw materials only, covered by the crate's 100% rows.
        var chain = new (string id, int times)[] { ("build-stock", 1), ("build-bench", 1), ("rope", 1), ("nails", 2), ("prybar", 1) };
        var made = chain.Select(x => x.id).ToArray(); var need = new Dictionary<string, int>();
        foreach (var (id, times) in chain)
        {
            var recipe = c.CraftPanel.Recipes.FirstOrDefault(r => r.Id == id); Check(recipe != null, "Recipe " + id);
            foreach (var cost in recipe.Costs) if (!made.Contains(cost.MaterialId)) need[cost.MaterialId] = (need.TryGetValue(cost.MaterialId, out int v) ? v : 0) + cost.Count * times;
        }
        foreach (var kv in need) { int sure = sites[0].Drops.Where(d => d.Id == kv.Key && d.Chance >= 100).Sum(d => d.Count); Check(sure >= kv.Value, "Opening chain " + kv.Key + " " + sure + "/" + kv.Value); }

        var ids = new HashSet<string>(c.InventoryPanel.Items.Select(x => x.Id));
        var unknown = sites.Where(s => s.Drops != null).SelectMany(s => s.Drops).Select(d => d.Id).Where(id => !ids.Contains(id)).Distinct().ToArray();
        Check(unknown.Length == 0, "Unknown item ids: " + string.Join(",", unknown));
        var texts = a.ObjectDescriptions;
        Check(texts.Length == 9 && texts[1] == "낡은 탁자 위에 먹을 것과 물이 남아 있다.\n판자와 고철도 뜯어낼 수 있겠다." && texts[5] == "도구 없이 상자를 열어\n자재와 물을 살펴봅니다." && texts[6] == "남겨진 통조림과 물, 붕대를\n선반에서 찾아봅니다.", "Descriptions [1]/[5]/[6]: " + string.Join(" | ", texts));
        return "PASS loot tables · 9 sites, rooms, prybar sites 2/4, tables 0-8, object noise 0,0,3,–,2,1,0,1,1 / 2 turns, lock 3, encounter 2/40/5/5/45, Sites[2]/[8] not overridden in SettlementScreen, crate covers the opening chain (" + string.Join(" ", need.Select(kv => kv.Key + " " + kv.Value)) + "), item ids, descriptions [1]/[5]/[6]";
    }
    static string Drops(ExpeditionLootPanel.Drop[] d) => d == null ? "" : string.Join(" ", d.Select(x => x.Id + " " + x.Count + "@" + x.Chance));

    // ---- helpers (as VerifyFieldTurnPlan) ----
    static async Task Tap(Button button)
    {
        Check(button && button.IsActive() && button.IsInteractable(), "Unavailable " + (button ? button.name : "null")); Hit(button);
        var r = (RectTransform)button.transform; var data = new PointerEventData(EventSystem.current) { position = Screen(r), button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler); await Task.Delay(100);
    }
    static Vector2 Screen(RectTransform r) { Canvas.ForceUpdateCanvases(); return RectTransformUtility.WorldToScreenPoint(r.GetComponentInParent<Canvas>().worldCamera, r.TransformPoint(r.rect.center)); }
    static void Hit(Selectable target)
    {
        var data = new PointerEventData(EventSystem.current) { position = Screen((RectTransform)target.transform) }; var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Selectable>() == target, "Blocked " + target.name + " by " + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
    }
    static async Task Until(Func<bool> done, int ms, string what) { var w = Stopwatch.StartNew(); while (!done()) { if (w.ElapsedMilliseconds > ms) throw new Exception("Timeout: " + what); await Task.Delay(30); } }
    static string Shots => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "Balance1Capture"));
    static Task Still(MonoBehaviour host, string name) { var done = new TaskCompletionSource<bool>(); host.StartCoroutine(StillRoutine(name, done)); return done.Task; }
    static IEnumerator StillRoutine(string name, TaskCompletionSource<bool> done)
    {
        yield return new WaitForEndOfFrame();
        var raw = new RenderTexture(UnityEngine.Screen.width, UnityEngine.Screen.height, 0); var rt = new RenderTexture(raw.width, raw.height, 0); ScreenCapture.CaptureScreenshotIntoRenderTexture(raw);
        if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(raw, rt, new Vector2(1, -1), new Vector2(0, 1)); else Graphics.Blit(raw, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new UnityEngine.Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); RenderTexture.active = prev; Object.Destroy(raw); Object.Destroy(rt);
        Directory.CreateDirectory(Shots); File.WriteAllBytes(Path.Combine(Shots, name + ".png"), tex.EncodeToPNG()); Object.Destroy(tex); done.SetResult(true);
    }
    static void Fits(params Text[] texts) { Canvas.ForceUpdateCanvases(); foreach (var x in texts) if (x && x.isActiveAndEnabled && !x.resizeTextForBestFit) Check(x.preferredHeight <= x.rectTransform.rect.height + 1, "Overflow " + x.name + ": " + x.text); }
    static async Task<ExpeditionArrivalPanel> Depart(SettlementController c)
    {
        await Tap(c.Exit); foreach (var card in c.ExpeditionPanel.Cards) if (!card.Check.gameObject.activeSelf) await Tap(card.Button);
        await Tap(c.ExpeditionPanel.Pack); await Tap(c.PackingPanel.Ready); await Tap(c.PackingPanel.Depart); await Task.Delay(1100);
        Check(c.ArrivalPanel.IsOpen && !c.ArrivalPanel.InTransit, "Arrived"); return c.ArrivalPanel;
    }
    // Every pawn at the door onto the next room, '턴 진행', the walk.
    static async Task Move(ExpeditionArrivalPanel a)
    {
        await Until(() => FieldPawnTest.Ready(a), 3000, "board ready"); int next = a.Rooms.CurrentRoom == A ? C : A;
        Check(FieldPawnTest.Gather(a, next), "Everyone at the door: " + FieldPawnTest.Describe(a)); await Tap(a.Threat.Planner.TurnButton);
        await Until(() => !a.InTransit, 6000, "move"); await Task.Delay(400);
    }
}
