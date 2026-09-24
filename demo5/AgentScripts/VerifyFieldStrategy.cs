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

// Site board 1차: rules of the thing that lives in 관리실, and a real-screen run of the lure → hush → den-shelf play.
public static class VerifyFieldStrategy
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    const int A = FieldSiteState.Arcade, C = FieldSiteState.Corridor, S = FieldSiteState.Storage, D = FieldSiteState.Den;

    public static string Rules()
    {
        var done = new List<string>();
        // Asleep (first visit): danger never above 1, never leaves the den, never meets anyone.
        var s = new FieldSiteState(new FieldSiteRules(), () => 0, asleep: true);
        for (int i = 0; i < 30; i++) s.EndTurn(3, false);
        Check(s.Danger <= 1 && s.ResidentRoom == D && !s.Encounter && s.Remembered < 0, "Asleep: stays home, danger capped");
        done.Add("asleep");

        // Noise fills the gauge, overflow raises danger; at 위험도 0 a loud room is not remembered, at 1+ it is.
        s = new FieldSiteState(new FieldSiteRules(), () => 0);
        s.EndTurn(3, false); Check(s.Gauge == 3 && s.Danger == 0 && s.Remembered < 0, "Gauge 3/4, not yet awake");
        s.EndTurn(3, false); Check(s.Gauge == 2 && s.Danger == 1 && s.Remembered == A, "Overflow → 위험도 1, remembers the loud room");
        Check(s.ResidentRoom == D && s.Next == D, "위험도 1 keeps it home");
        s.EndTurn(1, false); s.EndTurn(1, false); Check(s.Danger == 2 && s.Next == C, "위험도 2 with a memory: it sets off (announced)");
        s.EndTurn(0, false); Check(s.ResidentRoom == C && s.Moved && s.Heard && s.DenEmpty && s.Next == C, "Out into the corridor, heard next door, pauses to listen");
        s.EndTurn(0, false); Check(s.Next == A && s.Incoming, "Next step into our room is announced a turn early");
        s.EndTurn(0, true); Check(s.PassedBy && !s.Encounter && s.ResidentRoom == A && s.Remembered < 0, "Everyone hushed: it passes by, memory spent");
        s.MoveParty(C); s.EndTurn(0, false); Check(!s.Encounter && s.DenEmpty && s.PartyRoom == C, "We slip into the corridor while it stays in the arcade");
        done.Add("gauge/memory/leave/pause/telegraph/pass-by");

        // Staying, then heading home the same way.
        s.EndTurn(0, false); s.EndTurn(0, false); Check(s.Next == C || s.ResidentRoom == C, "After staying it heads home");
        done.Add("stay/return");

        // Not hushed → meeting when it walks in.
        s = new FieldSiteState(new FieldSiteRules(), () => 0, danger: 2, remembered: A);
        s.EndTurn(0, false); s.EndTurn(0, false); Check(s.Incoming, "Coming");
        s.EndTurn(0, false); Check(s.Encounter && !s.Surprise, "Walks in on us: a met encounter");
        s.Hidden(); Check(s.ResidentRoom == D && s.Remembered < 0, "Hiding sends it home");
        s.Driven(); Check(s.ResidentRoom < 0 && s.DenEmpty, "Driven off: gone, den empty");
        for (int i = 0; i < 3; i++) s.EndTurn(0, false); Check(s.ResidentRoom == D, "Back in the den after the gone turns");
        done.Add("met/hidden/driven");

        // Walking into its room without warning is a surprise; crossing in a doorway is a meeting.
        s = new FieldSiteState(new FieldSiteRules(), () => 0, danger: 2, remembered: C);
        s.EndTurn(0, false); s.EndTurn(0, false); Check(s.ResidentRoom == C && s.Resident == ResidentState.Staying, "It sits in the corridor");
        s.MoveParty(C); s.EndTurn(0, false); Check(s.Encounter && s.Surprise, "Walking in unheard: surprise");
        s = new FieldSiteState(new FieldSiteRules(), () => 0, danger: 2, remembered: A);
        s.EndTurn(0, false); s.EndTurn(0, false); Check(s.ResidentRoom == C && s.Next == A, "Heading for the arcade");
        s.MoveParty(C); s.EndTurn(0, false); Check(s.Encounter, "Crossing through the same door is a meeting");
        done.Add("surprise/crossing");

        // Hunting (위험도 3): comes every turn, hushing does not help.
        s = new FieldSiteState(new FieldSiteRules(), () => 0, danger: 3);
        s.EndTurn(0, true); s.EndTurn(0, true); Check(s.ResidentRoom == A && s.Encounter && !s.PassedBy, "Hunting: no pass-by");
        done.Add("hunt");

        // Site clock: 오래 머묾 at 8 and 16 raises the floor; past 24 the floor is 3. Danger never falls on site.
        s = new FieldSiteState(new FieldSiteRules(), () => 0);
        for (int i = 0; i < 8; i++) s.EndTurn(0, true); Check(s.Danger == 1 && s.Floor == 1, "Linger 1");
        for (int i = 0; i < 8; i++) s.EndTurn(0, true); Check(s.Danger == 2, "Linger 2");
        for (int i = 0; i < 9; i++) s.EndTurn(0, true); Check(s.Danger == 3 && s.TurnsLeft == 0, "Past the clock: 3");
        s = new FieldSiteState(new FieldSiteRules(), () => 0, clock: 17); Check(s.Danger == 2 && s.TurnsLeft == 7, "A later visit carries the clock");
        done.Add("site clock/linger");

        // Preview equals the next turn's gauge and danger.
        var rng = new System.Random(3); int trials = 0;
        for (int t = 0; t < 300; t++)
        {
            s = new FieldSiteState(new FieldSiteRules(), () => 0, danger: rng.Next(0, 3), gauge: rng.Next(0, 4), clock: rng.Next(0, 20));
            for (int k = 0; k < 6; k++)
            {
                int noise = rng.Next(0, 4); bool hush = rng.Next(3) == 0; var p = s.Preview(noise, hush);
                s.EndTurn(noise, hush); trials++;
                Check(p.gauge == s.Gauge && p.danger == s.Danger, "Preview " + p + " vs " + s.Gauge + "/" + s.Danger);
            }
        }
        done.Add("preview = result (" + trials + " turns)");
        return "PASS: " + string.Join("; ", done);
    }

    // ---- Play mode: real screens (run VerifyFieldBattle.Enter first) ----
    static async Task Tap(Button button)
    {
        Check(button && button.IsActive() && button.IsInteractable(), "Unavailable " + (button ? button.name : "null")); Canvas.ForceUpdateCanvases(); var r = (RectTransform)button.transform;
        var data = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(button.GetComponentInParent<Canvas>().worldCamera, r.TransformPoint(r.rect.center)), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits); Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, "Blocked " + button.name + " by " + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler); await Task.Delay(90);
    }
    static async Task Until(Func<bool> done, int ms, string what) { var w = Stopwatch.StartNew(); while (!done()) { if (w.ElapsedMilliseconds > ms) throw new Exception("Timeout: " + what); await Task.Delay(30); } }
    static string Shots => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "FieldStrategyCapture"));
    static void Upright(RenderTexture source, RenderTexture target) { if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(source, target, new Vector2(1, -1), new Vector2(0, 1)); else Graphics.Blit(source, target); }
    static Task Still(MonoBehaviour host, string name) { var done = new TaskCompletionSource<bool>(); host.StartCoroutine(StillRoutine(name, done)); return done.Task; }
    static IEnumerator StillRoutine(string name, TaskCompletionSource<bool> done)
    {
        yield return new WaitForEndOfFrame();
        var raw = new RenderTexture(Screen.width, Screen.height, 0); var rt = new RenderTexture(Screen.width, Screen.height, 0); ScreenCapture.CaptureScreenshotIntoRenderTexture(raw); Upright(raw, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new UnityEngine.Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); RenderTexture.active = prev; Object.Destroy(raw); Object.Destroy(rt);
        Directory.CreateDirectory(Shots); File.WriteAllBytes(Path.Combine(Shots, name + ".png"), tex.EncodeToPNG()); Object.Destroy(tex); done.SetResult(true);
    }
    static async Task Search(ExpeditionArrivalPanel a, int site, int pace)
    {
        a.Inspect(site); await Until(() => a.Search.IsOpen, 2000, "search panel " + site); await Task.Delay(150);
        var s = a.Search; await Tap(s.Cards[0].Button); if (a.Loot.State(site).Progress == 0) await Tap(s.Paces[pace]);
        await Tap(s.Choose); await Task.Delay(120); await Tap(s.Confirm); await Task.Delay(250);
        if (a.Loot.IsOpen) { await Tap(a.Loot.Back); await Task.Delay(120); if (a.Loot.LeaveReview.activeSelf) await Tap(a.Loot.LeaveConfirm); }
        if (a.Search.IsOpen) { await Tap(a.Search.Back); }
        await Task.Delay(200);
    }
    static async Task Move(ExpeditionArrivalPanel a)
    {
        a.Rooms.AskMove(); await Task.Delay(150); await Tap(a.ReturnConfirm);
        if (a.Rooms.HasQueuedMove) await Tap(a.Threat.Planner.TurnButton); // site board: the door reserves the move, '턴 진행' makes it
        await Until(() => !a.InTransit, 6000, "move"); await Task.Delay(400);
    }
    public static async Task<string> Showcase()
    {
        var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Run VerifyFieldBattle.Enter first");
        var log = new List<string>();
        // Real departure path (as the battle fixture does), no forced encounter.
        var people = c.Campaign.Party.ToArray();
        await Tap(c.Exit); foreach (var card in c.ExpeditionPanel.Cards) if (!card.Check.gameObject.activeSelf) await Tap(card.Button);
        await Tap(c.ExpeditionPanel.Pack); await Tap(c.PackingPanel.Ready); await Tap(c.PackingPanel.Depart); await Task.Delay(1100);
        var a = c.ArrivalPanel; var t = a.Threat; Check(a.IsOpen && t && t.State != null, "Arrived with the site board");
        Check(t.State.Asleep && !t.Active && !t.Hush.gameObject.activeSelf && a.Rooms.TurnLabel.text.StartsWith("탐험"), "First visit: it sleeps, the old turn count and no hush");
        await Still(a, "00-first-visit-asleep");
        // Review fixture: continue as a later visit (위험도 1, noise 2/4, site clock 6).
        if (c.Opening) c.Opening.State.Enabled = false; // review fixture: a later visit comes after the opening chapter
        t.ReviewWake(1, 2, 6); await Task.Delay(300);
        Check(t.Active && t.Hush.gameObject.activeSelf && a.Rooms.TurnLabel.text.Contains("18"), "Awake: clock and hush shown: " + a.Rooms.TurnLabel.text);
        await Still(a, "01-later-visit-hud");
        // T1: a fast search in the arcade is loud: the gauge overflows (위험도 2) and it remembers this room.
        await Search(a, 0, 0); var s = t.State; log.Add("T1 noise " + s.LastNoise + " 위험도 " + s.Danger + " 기억 " + s.Remembered + " next " + s.Next);
        Check(s.Danger == 2 && s.Remembered == A && s.Next == C, "Loud search lures it out");
        await Still(a, "02-loud-search-lures");
        // T2: hush; it leaves the den into the corridor — footsteps at our door.
        await Tap(t.Hush); await Task.Delay(400); log.Add("T2 room " + s.ResidentRoom + " heard " + s.Heard);
        Check(s.ResidentRoom == C && s.Heard, "Heard next door");
        await Still(a, "03-footsteps-next-door");
        // T3: hush; it stops to listen, and its next step into the arcade is announced.
        await Tap(t.Hush); await Task.Delay(400); log.Add("T3 incoming " + s.Incoming);
        Check(s.Incoming, "Announced a turn early");
        await Still(a, "04-announced-next-turn");
        // T4: everyone hushed: it walks in and passes by. It is here now, seen, and will stay a while.
        await Tap(t.Hush); await Task.Delay(600); log.Add("T4 passed " + s.PassedBy + " visible " + s.Visible);
        Check(s.PassedBy && s.Visible && !a.Encounter.IsOpen, "Passed by the hushed party");
        await Still(a, "05-passed-by");
        // T5: slip into the corridor; the den is empty.
        await Move(a); log.Add("T5 room " + a.Rooms.CurrentRoom + " den empty " + s.DenEmpty);
        Check(a.Rooms.CurrentRoom == C && s.DenEmpty && !a.Encounter.IsOpen && a.Status.text.StartsWith("관리실이 비었습니다"), "In the corridor with the den empty: " + a.Status.text);
        await Still(a, "06-den-empty");
        // T6: the office door opens the den shelf search.
        await Tap(a.Rooms.OfficeDoor); await Until(() => a.Search.IsOpen, 2000, "den search"); await Task.Delay(300);
        Check(a.Loot.CanSearch(t.DenSite, a.Participants[0]), "Den shelf searchable");
        await Still(a, "07-den-shelf");
        await Tap(a.Search.Back);
        return "PASS showcase · " + string.Join(" · ", log) + " · stills " + Shots;
    }
}
