using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Balance pass 1 deferred items on the real screen (기획/밸런스-1차.md §6 → applied), play mode after VerifyFieldBattle.Enter:
// the packing checklist (탄약·붕대·빈 칸), the bag-use hint (1턴, first visit / site board warning), the new search tables in the preview,
// '모두 담기' (all fit / some left) and the leave text. BattleRetreat() runs after VerifyFieldBattle.BeginPreview: the arcade retreat goes home.
public static class VerifyDeferred
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    public static async Task<string> Showcase()
    {
        FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Run VerifyFieldBattle.Enter first");
        var log = new List<string>();
        if (c.Opening) c.Opening.State.Enabled = false;

        // 1. Packing: two rounds in stock (the intro find), none packed → the checklist counts bags, the notice names the stock left behind.
        if (c.InventoryPanel.StockCount("ammo") == 0) Check(c.Campaign.AddSettlementAmmo(2), "Stock ammo fixture"); c.RefreshResources();
        await Tap(c.Exit); var plan = c.ExpeditionPanel; plan.Markers[Array.FindIndex(plan.Destinations, d => d.Id == "mall")].onClick.Invoke(); await Task.Delay(100);
        foreach (var card in plan.Cards) if (!card.Check.gameObject.activeSelf) await Tap(card.Button);
        await Tap(plan.Pack); var pack = c.PackingPanel; await Task.Delay(200);
        // Mock 03 rows: 식량·물 / 치료 / 탄약·도구 gauges (packed ÷ packed + stock); the ammo sits in stock, so its gauge is empty.
        Check(pack.CheckRows.Length == 3 && pack.CheckRows.All(r => r.Name && r.Fill && r.Name.text == r.Label && r.Name.isActiveAndEnabled), "Checklist rows");
        var ammoRow = pack.CheckRows.First(r => r.Items.Contains("ammo")); Check(ammoRow.Fill.anchorMax.x == 0, "Ammo gauge empty while it stays in stock: " + ammoRow.Fill.anchorMax.x);
        Check(pack.NoticeTitle && pack.NoticeTitle.text == pack.NoticeTitleText && pack.Notice.text == pack.NoticeNoAmmo, "Notice names the ammo left in stock: " + pack.Notice.text);
        Fits(pack.CheckRows.Select(r => r.Name).Concat(new[] { pack.NoticeTitle, pack.Notice }).ToArray());
        await Still(c, "01-짐싸기-체크리스트");
        // Pack both rounds: the gauge fills and the notice falls back to the general reminder; then put them back (the rest of the fixture expects them in stock).
        await Tap(pack.StockRows.First(r => r.Label.text == "탄약").Button); await Tap(pack.Plus); await Tap(pack.ToBag); await Task.Delay(150);
        Check(ammoRow.Fill.anchorMax.x == 1 && pack.Notice.text == pack.NoticeDefault, "Packed ammo fills its gauge: " + ammoRow.Fill.anchorMax.x + " / " + pack.Notice.text);
        await Still(c, "01b-짐싸기-탄약챙김");
        await Tap(pack.BagRows.First(r => r.Label.text == "탄약").Button); await Tap(pack.Plus); await Tap(pack.ToStock); await Task.Delay(150);
        Check(ammoRow.Fill.anchorMax.x == 0, "Ammo back in stock");
        await Tap(pack.Ready); await Tap(pack.Depart); await Task.Delay(1100);
        var a = c.ArrivalPanel; Check(a.IsOpen && !a.InTransit && a.Threat.State.Asleep, "First visit");
        log.Add("packing checklist 탄약/붕대/빈 칸");

        // 2. Bag hint on the first visit (말 놓기: the same as later visits): the member's action for the next '턴 진행'.
        Adventurer p0 = a.Participants[0], p1 = a.Participants[1];
        Check(c.InventoryPanel.TransferField(p0, "bandage", 1, true), "Bandage fixture");
        if (p0.Health >= p0.MaxHealth) p0.Health = p0.MaxHealth - 1; // a bandage only helps someone hurt (a new game starts unhurt since 2026-09-25)
        a.FieldBags.Open(0); await Task.Delay(200); var bags = a.FieldBags;
        await Tap(bags.LeftRows.First(r => r.Label.text == "붕대").Button); await Task.Delay(100);
        var hint = bags.UseHint.text.Split('\n');
        Check(hint.Length == 2 && hint[0].EndsWith(bags.QueueSuffix) && hint[1] == bags.QueueUseLine && bags.Use.GetComponentInChildren<Text>().text.EndsWith("· " + bags.QueueSuffix), "First-visit hint: " + bags.UseHint.text);
        Fits(bags.UseHint);
        await Still(a, "02-가방-첫방문-다음턴행동");
        await Tap(bags.Back); await Task.Delay(150);
        log.Add("bag hint first visit");

        // 3. The new tables in the search preview (the read-only 07 window): the crate's sure rows + extra rows, the table's rows alone and
        //    with two pawns (no pace since 2026-09-25; 망보기 is greyed out under the helper on the silent table).
        int encMax = a.Encounter.MaximumChance; a.Encounter.MaximumChance = 0;
        try
        {
            var s = a.Search;
            await Until(() => FieldPawnTest.Ready(a), 3000, "board ready");
            Check(FieldPawnTest.Coop(a, 0), "Two pawns on the crate"); a.Inspect(0); await Until(() => s.IsOpen, 2000, "search 0"); await Task.Delay(150); Check(s.ReadOnly, "07 read only");
            Fits(s.View.GetComponentsInChildren<Text>().ToArray());
            await Still(a, "03-미리보기-물자상자-함께");
            await Tap(s.Back); await Task.Delay(150); FieldPawnTest.Clear(a);
            a.Inspect(1); await Until(() => s.IsOpen, 2000, "search 1"); await Task.Delay(150); Check(!a.Loot.CanWatch(1), "The table is silent");
            await Still(a, "04-미리보기-낡은탁자"); await Tap(s.Back); await Task.Delay(150);
            Check(FieldPawnTest.Coop(a, 1), "Two pawns on the table"); var chips = FieldPawnTest.Rules(a).ChipsFor(FieldPawnTest.Check(a).RunFor(1).Support);
            Check(chips.Count == 3 && !chips[1].Enabled && chips[1].Why == a.Threat.Planner.PlaceTexts.WhyNoNoise, "망보기 offered on the silent table");
            a.Inspect(1); await Until(() => s.IsOpen, 2000, "search 1"); await Task.Delay(150); Fits(s.View.GetComponentsInChildren<Text>().ToArray());
            await Still(a, "05-미리보기-낡은탁자-함께");
            await Tap(s.Back); await Task.Delay(150); FieldPawnTest.Clear(a);
            log.Add("previews");

            // 4. '모두 담기': the crate (3 kinds) all fits; the table (4 kinds) with one bag nearly full leaves one kind on the object.
            var crate = a.Loot.Sites[0].Drops; var keep = crate.Select(d => d.Chance).ToArray();
            try { for (int i = 0; i < crate.Length; i++) crate[i].Chance = 100; Check(FieldPawnTest.Coop(a, 0) && await FieldPawnTest.Turn(a) && a.Loot.Peek(0, out var done0) && done0.Complete, "Crate search (two pawns, one turn)"); }
            finally { for (int i = 0; i < crate.Length; i++) crate[i].Chance = keep[i]; }
            await Task.Delay(200); if (!a.Loot.IsOpen) a.Loot.Open(0); await Task.Delay(250); var l = a.Loot;
            Check(l.IsOpen && l.TakeAll && l.TakeAll.interactable && l.FieldRows.Count == 3, "Loot open with 3 kinds");
            await Still(a, "06-발견물-모두담기-전");
            int turns = a.Rooms.Turns, noise = a.Rooms.Noise, minute = c.Campaign.MinuteOfDay;
            var before = new[] { "wood", "scrap", "cloth" }.ToDictionary(id => id, id => l.State(0).Loot[id] + a.Participants.Sum(p => c.InventoryPanel.CountFor(p, id)));
            await Tap(l.TakeAll); await Task.Delay(150);
            Check(l.FieldRows.Count == 0 && l.State(0).Loot.Values.All(n => n == 0), "Everything taken");
            Check(before.All(kv => a.Participants.Sum(p => c.InventoryPanel.CountFor(p, kv.Key)) == kv.Value), "Totals kept");
            Check(a.Rooms.Turns == turns && a.Rooms.Noise == noise && c.Campaign.MinuteOfDay == minute, "Take all is free");
            Fits(l.Message);
            await Still(a, "07-발견물-모두담기-후");
            await Tap(l.Back); await Task.Delay(200); Check(!l.IsOpen, "Empty field closes without the leave review");

            // Leave one free slot in total: the table's 4 kinds (food, water, wood, scrap) → wood and scrap join their holders, food takes the last slot, water stays.
            while (c.InventoryPanel.SlotsFor(p1) < p1.BagCapacity) Check(c.InventoryPanel.TransferField(p1, new[] { "ration", "can", "meal" }.First(id => c.InventoryPanel.CountFor(p1, id) == 0 && c.InventoryPanel.Items.Any(i => i.Id == id)), 1, true), "Fill p1");
            var table = a.Loot.Sites[1].Drops; keep = table.Select(d => d.Chance).ToArray();
            try { await Until(() => FieldPawnTest.Ready(a), 3000, "board ready"); for (int i = 0; i < table.Length; i++) table[i].Chance = 100; Check(FieldPawnTest.Coop(a, 1) && await FieldPawnTest.Turn(a) && a.Loot.Peek(1, out var done1) && done1.Complete, "Table search (two pawns, one turn)"); }
            finally { for (int i = 0; i < table.Length; i++) table[i].Chance = keep[i]; }
            await Task.Delay(200); if (!a.Loot.IsOpen) a.Loot.Open(1); await Task.Delay(250);
            int kinds = l.FieldRows.Count; await Tap(l.TakeAll); await Task.Delay(150);
            Check(l.FieldRows.Count > 0 && l.FieldRows.Count < kinds && l.Message.text.Contains(l.FieldRows.Count + "종"), "Some kinds left: " + l.Message.text);
            Fits(l.Message);
            await Still(a, "08-발견물-일부남김");
            await Tap(l.Back); await Task.Delay(150);
            Check(l.LeaveReview.activeSelf && l.LeaveBody.text == l.LeaveText, "Leave text: " + l.LeaveBody.text);
            Fits(l.LeaveBody);
            await Still(a, "09-두고가기-문구");
            await Tap(l.LeaveConfirm); await Task.Delay(200);
            log.Add("take all (all / some left) and leave text");
        }
        finally { a.Encounter.MaximumChance = encMax; }

        // 5. Bag use on the site board while it is hunting and steps in next turn: queued, '턴 진행''s chip says the turn meets it.
        a.Threat.ReviewWake(3, 0, 0); await Task.Delay(150); a.Rooms.SpendTurn(0, true); await Task.Delay(200);
        Check(a.Threat.State.Incoming, "Incoming fixture");
        a.FieldBags.Open(0); await Task.Delay(200);
        await Tap(bags.LeftRows.First(r => r.Label.text == "붕대").Button); await Task.Delay(100);
        hint = bags.UseHint.text.Split('\n');
        Check(hint.Length == 2 && hint[1] == bags.QueueUseLine, "Board hint: " + bags.UseHint.text);
        Fits(bags.UseHint);
        int hp0 = p0.Health; p0.Health = Math.Max(1, p0.MaxHealth - 1); a.RefreshFieldBags(); await Task.Delay(100);
        await Tap(bags.Use); var pl = a.Threat.Planner; await Task.Delay(150);
        Check(pl.UseQueued(0, "bandage") && pl.Chip.text.Contains("이번 턴 조우"), "Queued use: the chip warns of the meeting · " + pl.Chip.text);
        await Still(a, "10-가방-장소판-경고");
        await Tap(bags.Use); Check(!pl.UseQueued(0, "bandage"), "The same item again takes the use back"); p0.Health = hp0; a.RefreshFieldBags();
        await Tap(bags.Back); await Task.Delay(150);
        a.Threat.ReviewWake(0, 0, 0); await Task.Delay(150);
        log.Add("bag hint board warning");
        return "PASS deferred · " + string.Join(" · ", log) + " · stills " + Shots;
    }

    // After VerifyFieldBattle.Enter + BeginPreview (a fight in the arcade): the retreat goes home.
    public static async Task<string> BattleRetreat()
    {
        FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c = Object.FindAnyObjectByType<SettlementController>(); var a = c.ArrivalPanel; var b = a.Encounter.Battle; Check(b.IsOpen && b.RetreatsHome, "BeginPreview first (arcade)");
        float speed = b.Presentation.Speed; b.Presentation.Speed = 8;
        try
        {
            await Until(() => !b.Busy, 4000, "first turn");
            await Still(a, "11-전투-출구귀환-버튼");
            await Tap(b.Retreat); await Task.Delay(200); Fits(b.RetreatBody);
            Check(b.RetreatConfirm.GetComponentInChildren<Text>(true).text == b.RetreatHomeConfirmLabel, "Confirm reads the home return: " + b.RetreatConfirm.GetComponentInChildren<Text>(true).text);
            await Still(a, "12-전투-출구귀환-확인");
            await Tap(b.RetreatConfirm); await Until(() => b.Result.activeSelf, 8000, "result"); await Task.Delay(400);
            await Still(a, "13-전투-귀환결과");
            await Tap(b.ResultContinue); await Task.Delay(700);
            Check(!a.IsOpen && c.ReturnPanel.IsOpen, "Home with the return report");
            return "PASS battle retreat from the arcade → home · stills " + Shots;
        }
        finally { b.Presentation.Speed = speed; }
    }

    // Edit mode: a retreat's battle noise lands on the board without a turn (gauge, 위험도, remembered room, re-planned step).
    public static string HearRules()
    {
        var s = new FieldSiteState(new FieldSiteRules(), () => 0, false, 1, 2, FieldSiteState.Nowhere, 0); s.MoveParty(FieldSiteState.Corridor); s.EndTurn(0, false);
        int turns = s.TurnsUsed; s.Hear(3);
        Check(s.TurnsUsed == turns && s.Gauge == 1 && s.Danger == 2 && s.Remembered == FieldSiteState.Corridor && s.Next == FieldSiteState.Corridor, $"Loud retreat: gauge {s.Gauge} danger {s.Danger} remembered {s.Remembered} next {s.Next}");
        s.Hear(0); Check(s.Gauge == 1 && s.Danger == 2, "Silent retreat changes nothing");
        return "PASS hear: noise 3 at 위험도 1 · 게이지 2 → 위험도 2 · 게이지 1, the corridor remembered and its next step toward it, no turn spent";
    }

    // Play mode after VerifyFieldBattle.Enter: a fight on the board in the corridor, retreat → the battle noise reaches the gauge, as the result says.
    public static async Task<string> BoardRetreat()
    {
        FieldIdleConfirm.AutoAccept=true;/* idle members hush without the question (VerifyIdleConfirm tests it) */var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Run VerifyFieldBattle.Enter first");
        if (c.Opening) c.Opening.State.Enabled = false;
        await Tap(c.Exit); var plan = c.ExpeditionPanel; plan.Markers[Array.FindIndex(plan.Destinations, d => d.Id == "mall")].onClick.Invoke(); await Task.Delay(100);
        foreach (var card in plan.Cards) if (!card.Check.gameObject.activeSelf) await Tap(card.Button);
        await Tap(plan.Pack); await Tap(c.PackingPanel.Ready); await Tap(c.PackingPanel.Depart); await Task.Delay(1100);
        var a = c.ArrivalPanel; var t = a.Threat; var e = a.Encounter;
        a.Rooms.AskMove(); await Task.Delay(150); await Tap(a.ReturnConfirm); await Until(() => !a.InTransit, 6000, "move"); await Task.Delay(300);
        Check(a.Rooms.CurrentRoom == FieldSiteState.Corridor, "In the corridor");
        t.ReviewWake(3, 0, 0); await Task.Delay(150);
        for (int i = 0; i < 4 && !e.IsOpen; i++) { a.Rooms.SpendTurn(0, false); await Task.Delay(150); }
        Check(e.IsOpen, "Met on the board");
        await Tap(e.Fight); var b = e.Battle; await Until(() => b.IsOpen && !b.Busy, 6000, "battle");
        Check(!b.RetreatsHome, "Corridor fight retreats to a room");
        typeof(FieldBattleState).GetProperty("Noise").GetSetMethod(true).Invoke(b.State, new object[] { 4 }); // two shots' worth
        await Tap(b.Retreat); await Task.Delay(150);
        Check(b.RetreatConfirm.GetComponentInChildren<Text>(true).text == b.RetreatConfirmLabel, "Confirm reads the room retreat");
        await Tap(b.RetreatConfirm); await Until(() => b.Result.activeSelf, 8000, "result"); await Task.Delay(300);
        int noise = b.State.ResultNoise; var cons = b.Result.GetComponent<BattleResultSummary>().Consequences.text;
        Check(noise > 0 && cons.Contains("+" + noise), "Result names the noise: " + noise + " / " + cons);
        int gauge = t.State.Gauge, size = t.Rules.GaugeSize;
        await Still(a, "16-장소판-복도-물러나기-결과");
        await Tap(b.ResultContinue); await Until(() => !b.IsOpen && !a.InTransit, 8000, "retreat move"); await Task.Delay(300);
        Check(t.State.Gauge == (gauge + noise) % size && t.State.Danger == 3, $"Board heard the fight: gauge {gauge}+{noise} → {t.State.Gauge}");
        await Still(a, "17-장소판-물러난-뒤-소음");
        int after = t.State.Gauge; t.ReviewWake(0, 0, 0);
        return $"PASS board retreat · result '+{noise}' reached the gauge ({gauge} → {after}) · confirm label · stills " + Shots;
    }

    // ---- helpers (as VerifyBalance1) ----
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
    static string Shots => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "DeferredCapture"));
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
}
