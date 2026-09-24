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

// Play mode, after VerifyFieldBattle.Enter (a fresh settlement): the one-time battle lesson for the site board's resident (귀기울임),
// 2026-09-25 user decision '튜토에서 알려주는걸로하자'. Meets it on the awake board in the corridor (ReviewWake + SpendTurn, as VerifyDeferred.BoardRetreat),
// shoots it and checks the cause/instruction lines, the red frame on the marked cell, ONE gold frame + arrow on the recommended cell, the save flag,
// the whiff line after stepping aside, nothing on a second meeting, and the Guard-card fallback when the shooter cannot move.
// Stills: Temp/ResidentLessonCapture. Leaves the corridor fight open (Lanes() reuses it); run VerifyFieldBattle.Enter again before other battle flows.
public static class VerifyResidentLesson
{
    const string SafeMoveHint = "이 칸으로 옮기면 이번 적 차례에 공격받지 않습니다.";

    public static async Task<string> Run()
    {
        var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Run VerifyFieldBattle.Enter first");
        if (c.Opening) c.Opening.State.Enabled = false;
        await Tap(c.Exit); var plan = c.ExpeditionPanel; plan.Markers[Array.FindIndex(plan.Destinations, d => d.Id == "mall")].onClick.Invoke(); await Task.Delay(100);
        foreach (var card in plan.Cards) if (!card.Check.gameObject.activeSelf) await Tap(card.Button);
        await Tap(plan.Pack); await Tap(c.PackingPanel.Ready); await Tap(c.PackingPanel.Depart); await Task.Delay(1100);
        var a = c.ArrivalPanel; var t = a.Threat; var e = a.Encounter; Check(a.IsOpen && t && e, "Arrived with a site board");
        Check(!t.ResidentLessonShown, "Fresh game: the resident lesson has not been shown");
        // Member 0 carries two rounds (same fixture as VerifyFieldBattle.Begin: the ruined-start intro leaves no stock ammo).
        var p0 = a.Participants[0]; int ammo = c.InventoryPanel.CountFor(p0, "ammo"); if (ammo < 2) Check(c.InventoryPanel.TransferField(p0, "ammo", 2 - ammo, true), "Field ammo fixture");
        a.Rooms.AskMove(); await Task.Delay(150); await Tap(a.ReturnConfirm); await Until(() => !a.InTransit, 6000, "move"); await Task.Delay(300);
        Check(a.Rooms.CurrentRoom == FieldSiteState.Corridor, "In the corridor");
        t.ReviewWake(3, 0, 0); await Task.Delay(150);
        for (int i = 0; i < 4 && !e.IsOpen; i++) { a.Rooms.SpendTurn(0, false); await Task.Delay(150); }
        Check(e.IsOpen, "Met on the board");
        await Tap(e.Fight); var b = e.Battle; await Until(() => b.IsOpen && !b.Busy, 6000, "battle");
        Check(b.LessonMarks && b.LessonDangerFrame && b.LessonMoveFrame && b.LessonGuardFrame && b.LessonPointer, "Lesson marks wired (run FixBattleActionCardLines.Run)");
        var resident = b.State.Units.First(u => u.Enemy).Creature; Check(resident != null && resident.Id == t.ResidentId && resident.Attack == CreatureAttack.Listen, "The board fight is the resident");
        var log = new List<string>(); float speed = b.Presentation.Speed, pause = b.ActionPause; var random = UnityEngine.Random.state;
        b.Presentation.Speed = 4; b.ActionPause = .05f;
        try
        {
            // ---- Block A: first shot → the lesson, once ----
            var s = b.State; int res = s.Units.FindIndex(u => u.Enemy);
            Check(s.PlayerTurn && s.Actor == 0 && !b.LessonActive && NoMarks(b), "Quiet start: member 0 acts, no lesson marks");
            await ShootMiss(b); await Until(() => !b.Busy, 6000, "member 1 turn");
            Check(!b.LessonActive && NoMarks(b), "No lesson before the wind-up");
            await Tap(b.Guard);
            await Until(() => b.LessonActive, 10000, "lesson starts on the wind-up");
            string cause = string.Format(b.LessonFeedback, Subject(s.Units[res].Name), Subject(s.Units[0].Name));
            Check(t.ResidentLessonShown && b.LessonShooter == 0 && b.LessonCell == Cell(s.Units[0]), "Marked shown on the shooter's cell");
            await Still(b, "lesson-1-windup");
            await Until(() => !b.Busy, 10000, "party phase"); await Task.Delay(120);
            var shooter = s.Units[0]; int marked = Cell(shooter);
            Check(s.PlayerTurn && s.Actor == 0 && s.Units[res].Pending != null && s.Units[res].Pending.Cells.Count == 1 && s.Units[res].Pending.Cells[0] == marked, "The resident marked the shooter's cell");
            Check(b.PhaseBanner.Detail.text == string.Format(b.AllyPhaseLessonDetail, s.Current.Name), "Ally banner detail: " + b.PhaseBanner.Detail.text);
            Check(b.Feedback.text == cause, "Cause in the feedback strip: " + b.Feedback.text);
            Check(b.Hint.text == b.LessonMoveHint, "One instruction in the hint strip: " + b.Hint.text);
            Check(b.LessonDangerFrame.gameObject.activeSelf && Encloses(b, b.LessonDangerFrame.rectTransform, marked), "Red frame on the marked cell");
            int expected = FirstSafeCell(s); Check(expected >= 0, "Fixture: the shooter has a safe neighbour");
            Check(b.LessonMoveCell() == expected, "Recommended cell = first safe neighbour in the fixed order: " + b.LessonMoveCell() + " / " + expected);
            Check(b.LessonMoveFrame.gameObject.activeSelf && Encloses(b, b.LessonMoveFrame.rectTransform, expected) && !Encloses(b, b.LessonMoveFrame.rectTransform, marked), "Gold frame on the recommended cell only");
            var frame = In(b.LessonMarks, b.LessonMoveFrame.rectTransform); var arrow = In(b.LessonMarks, b.LessonPointer.rectTransform);
            Check(b.LessonPointer.gameObject.activeSelf && (Mathf.Abs(arrow.center.x - frame.center.x) < 2 && (arrow.yMin >= frame.yMax - 1 || arrow.yMax <= frame.yMin + 1) || Mathf.Abs(arrow.center.y - frame.center.y) < 2 && (arrow.xMin >= frame.xMax - 1 || arrow.xMax <= frame.xMin + 1)), "Arrow points at the gold frame: " + arrow + " / " + frame);
            Check(!b.LessonGuardFrame.gameObject.activeSelf, "No guard frame while a cell is recommended");
            Check(Hud(b, s.Units[0].Name).Danger.transform.localScale.x > 1.01f && Hud(b, s.Units[res].Name).Intent.transform.localScale.x > 1.01f, "Shooter's and resident's tags emphasized");
            b.Hover(expected, true); await Task.Delay(120); Check(b.Hint.text == SafeMoveHint, "Hovering the recommended cell previews no attack: " + b.Hint.text);
            b.Hover(expected, false); await Task.Delay(80); Check(b.Hint.text == b.LessonMoveHint, "Hint returns after the hover");
            await Until(() => !b.PhaseBanner.gameObject.activeSelf, 4000, "banner"); await Task.Delay(150);
            await Still(b, "lesson-2-move-target");
            log.Add("lesson on the wind-up, cell " + expected + " recommended");

            // Step aside: marks and lines leave with the step, the strike whiffs, the whiff line explains the opening.
            await TapCell(b, false, expected % 3, expected / 3); await Until(() => !b.Busy, 4000, "step"); await Task.Delay(120);
            Check(s.Moved && Cell(shooter) == expected, "Stepped onto the recommended cell");
            Check(NoMarks(b) && b.Hint.text != b.LessonMoveHint && b.Feedback.text != cause, "Marks and lesson lines leave with the step");
            b.Presentation.Speed = 2; // a longer window for the whiff line (LessonHold × .6 / speed)
            string whiff = string.Format(b.LessonWhiff, b.Rules.StaggerHitBonus); var health = s.Units.Where(u => !u.Enemy).Select(u => u.Health).ToArray();
            await Tap(b.Guard); await Until(() => !b.Busy, 6000, "member 1 turn"); await Tap(b.Guard);
            await Until(() => b.Feedback.text == whiff, 10000, "whiff line"); await Still(b, "lesson-3-whiff");
            await Until(() => !b.Busy, 10000, "after the strike"); await Task.Delay(120);
            Check(s.Units[res].Stagger == 1 && s.Units.Where(u => !u.Enemy).Select(u => u.Health).SequenceEqual(health), "Whiff: nobody hurt, the resident is open");
            Check(!b.LessonActive && NoMarks(b), "Lesson over after the strike");
            b.Presentation.Speed = 4;
            log.Add("step aside → whiff line");

            // Second meeting (same site, flag set): the wind-up plays with no pause, no marks and the normal lines.
            await Restage(b, resident); s = b.State; res = s.Units.FindIndex(u => u.Enemy);
            await ShootMiss(b); await Until(() => !b.Busy, 6000, "member 1 turn"); await Tap(b.Guard);
            bool seen = false; var watch = Stopwatch.StartNew();
            while ((b.Busy || watch.ElapsedMilliseconds < 200) && watch.ElapsedMilliseconds < 10000) { seen |= b.LessonActive || !NoMarks(b); await Task.Delay(20); }
            await Until(() => !b.Busy, 10000, "second wind-up"); await Task.Delay(120);
            Check(s.Units[res].Pending != null && s.Units[res].Pending.Cells[0] == Cell(s.Units[0]), "Second wind-up on the shooter");
            Check(!seen && !b.LessonActive && NoMarks(b), "Second meeting shows no lesson");
            Check(b.Feedback.text == s.Message && b.Hint.text != b.LessonMoveHint && b.PhaseBanner.Detail.text != string.Format(b.AllyPhaseLessonDetail, s.Current.Name), "Normal lines on the second meeting: " + b.Feedback.text + " / " + b.Hint.text);
            log.Add("second meeting quiet");

            // ---- Block B: the shooter cannot step aside → the Guard card ----
            t.ReviewResetResidentLesson();
            await Restage(b, resident); s = b.State; res = s.Units.FindIndex(u => u.Enemy);
            await ShootMiss(b); await Until(() => !b.Busy, 6000, "member 1 turn"); await Tap(b.Guard);
            await Until(() => b.LessonActive, 10000, "lesson again after the review reset"); await Until(() => !b.Busy, 10000, "party phase");
            Check(s.Actor == b.LessonShooter, "Shooter acts first");
            s.Current.Bound = 1; b.ReviewSync(); await Task.Delay(150);
            Check(b.LessonMoveCell() < 0 && b.Hint.text == string.Format(b.LessonGuardHint, b.Rules.GuardStrikeReduction), "Guard hint when the shooter cannot move: " + b.Hint.text);
            var guardBox = In((RectTransform)b.LessonGuardFrame.transform.parent, (RectTransform)b.LessonGuardFrame.transform);
            var guardCard = In((RectTransform)b.LessonGuardFrame.transform.parent, (RectTransform)b.Guard.transform);
            Check(b.LessonGuardFrame.gameObject.activeSelf && guardBox.xMin <= guardCard.xMin && guardBox.xMax >= guardCard.xMax && guardBox.yMin <= guardCard.yMin && guardBox.yMax >= guardCard.yMax, "Gold frame around the Guard card: " + guardBox + " / " + guardCard);
            Check(!b.LessonMoveFrame.gameObject.activeSelf && !b.LessonPointer.gameObject.activeSelf && b.LessonDangerFrame.gameObject.activeSelf, "No move frame or arrow; the marked cell stays red");
            await Until(() => !b.PhaseBanner.gameObject.activeSelf, 4000, "banner"); await Task.Delay(150);
            await Still(b, "lesson-4-guard");
            log.Add("guard fallback");

            Bounds(b.View);
            var detail = b.PhaseBanner.Detail; string longest = s.Units.Where(u => !u.Enemy).Select(u => string.Format(b.AllyPhaseLessonDetail, u.Name)).OrderByDescending(x => x.Length).First();
            string keep = detail.text; detail.text = longest; float width = detail.preferredWidth; detail.text = keep;
            Check(width <= detail.rectTransform.rect.width, "Banner detail fits: " + width + " / " + detail.rectTransform.rect.width + " '" + longest + "'");
            return "PASS resident lesson · " + string.Join(" · ", log) + " · banner detail " + width.ToString("0") + "/" + detail.rectTransform.rect.width + " px · stills " + Shots;
        }
        finally { UnityEngine.Random.state = random; b.Presentation.Speed = speed; b.ActionPause = pause; }
    }

    // After Run (the corridor fight still open): the marked cell in lanes 0, 1 and 2, to review overlap with head tags and the feedback paper.
    public static async Task<string> Lanes()
    {
        var a = Object.FindAnyObjectByType<SettlementController>().ArrivalPanel; var b = a.Encounter.Battle; var t = a.Threat;
        Check(b.IsOpen && b.State.Units.Any(u => u.Enemy && u.Creature != null && u.Creature.Id == t.ResidentId), "Run first (resident fight open)");
        var resident = b.State.Units.First(u => u.Enemy).Creature; var log = new List<string>();
        float speed = b.Presentation.Speed, pause = b.ActionPause; var random = UnityEngine.Random.state; b.Presentation.Speed = 4; b.ActionPause = .05f;
        try
        {
            for (int lane = 0; lane < 3; lane++)
            {
                t.ReviewResetResidentLesson(); await Restage(b, resident); var s = b.State;
                s.Units[0].Depth = 0; s.Units[0].Lane = lane; s.Units[1].Depth = 1; s.Units[1].Lane = (lane + 1) % 3; b.ReviewSync(); await Task.Delay(150);
                await ShootMiss(b); await Until(() => !b.Busy, 6000, "member 1 turn"); await Tap(b.Guard);
                await Until(() => b.LessonActive, 10000, "lesson lane " + lane); await Until(() => !b.Busy, 10000, "party phase");
                await Until(() => !b.PhaseBanner.gameObject.activeSelf, 4000, "banner"); await Task.Delay(200);
                Check(b.LessonDangerFrame.gameObject.activeSelf && Encloses(b, b.LessonDangerFrame.rectTransform, Cell(s.Units[0])), "Red frame lane " + lane);
                Check(b.LessonMoveFrame.gameObject.activeSelf != b.LessonGuardFrame.gameObject.activeSelf, "One gold mark lane " + lane);
                await Still(b, "lesson-lane-" + lane);
                log.Add("lane " + lane + (b.LessonMoveFrame.gameObject.activeSelf ? " → cell " + b.LessonMoveCell() : " → guard"));
            }
            return "Captured to " + Shots + " · " + string.Join(" · ", log);
        }
        finally { UnityEngine.Random.state = random; b.Presentation.Speed = speed; b.ActionPause = pause; }
    }

    // ---- helpers ----
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static int Cell(FieldBattleState.Unit u) => FieldBattleState.CellOf(u.Depth, u.Lane);
    // Korean subject particle, as the panel writes it (independent copy).
    static string Subject(string name) { char c = name[name.Length - 1]; return name + (c >= 0xAC00 && c <= 0xD7A3 && (c - 0xAC00) % 28 == 0 ? "가" : "이"); }
    static bool NoMarks(ExpeditionBattlePanel b) => !b.LessonDangerFrame.gameObject.activeSelf && !b.LessonMoveFrame.gameObject.activeSelf && !b.LessonGuardFrame.gameObject.activeSelf && !b.LessonPointer.gameObject.activeSelf;
    // The shooter's neighbours in the fixed order (lane-1, lane+1, depth+1, depth-1): free, not marked, and no attack on it in the forecast from there.
    static int FirstSafeCell(FieldBattleState s)
    {
        var me = s.Current; var danger = s.DangerCells().ToList();
        foreach (var (dd, dl) in new[] { (0, -1), (0, 1), (1, 0), (-1, 0) })
        {
            int d = me.Depth + dd, l = me.Lane + dl;
            if (!s.CanMove(d, l) || danger.Contains(FieldBattleState.CellOf(d, l))) continue;
            if (s.PredictIntents(s.Actor, d, l).Any(p => (p.Kind == EnemyIntentKind.Attack || p.Kind == EnemyIntentKind.Strike) && p.Hits != null && p.Hits.Any(h => h.Target == s.Actor))) continue;
            return FieldBattleState.CellOf(d, l);
        }
        return -1;
    }
    // The frame (in its parent's space) holds all four corners of the ally cell's polygon, within its padding and pulse.
    static bool Encloses(ExpeditionBattlePanel b, RectTransform frame, int cell)
    {
        var space = (RectTransform)frame.parent; var box = In(space, frame); var c = b.Cells[cell];
        var corners = new[] { c.TopLeft, c.TopRight, c.BottomRight, c.BottomLeft }.Select(p => (Vector2)space.InverseTransformPoint(c.rectTransform.TransformPoint(p))).ToList();
        var min = new Vector2(corners.Min(p => p.x), corners.Min(p => p.y)); var max = new Vector2(corners.Max(p => p.x), corners.Max(p => p.y));
        // 3 px tolerance for a fading screen shake (the frame follows the stage, the polygon does not).
        float slack = b.LessonPadding + b.LessonPulseGrow + 3;
        return box.xMin <= min.x + 3 && box.xMax >= max.x - 3 && box.yMin <= min.y + 3 && box.yMax >= max.y - 3
            && box.xMin >= min.x - slack && box.xMax <= max.x + slack && box.yMin >= min.y - slack && box.yMax <= max.y + slack;
    }
    static Rect In(RectTransform space, RectTransform r)
    {
        var w = new Vector3[4]; r.GetWorldCorners(w); var p = w.Select(x => (Vector2)space.InverseTransformPoint(x)).ToList();
        return Rect.MinMaxRect(p.Min(x => x.x), p.Min(x => x.y), p.Max(x => x.x), p.Max(x => x.y));
    }
    static BattlePawnHud Hud(ExpeditionBattlePanel b, string unit) => b.HudLayer.GetComponentsInChildren<BattlePawnHud>().First(h => h.name == unit + " HUD");
    static async Task Restage(ExpeditionBattlePanel b, BattleCreature resident)
    {
        var a = Object.FindAnyObjectByType<SettlementController>().ArrivalPanel;
        foreach (var p in a.Participants) { p.Health = p.MaxHealth; }
        var p0 = a.Participants[0]; int ammo = a.Inventory.CountFor(p0, "ammo"); if (ammo < 2) Check(a.Inventory.TransferField(p0, "ammo", 2 - ammo, true), "Restock");
        b.Restage(new[] { resident }); await Task.Delay(200); await Until(() => !b.Busy, 4000, "restaged");
        Check(b.State.PlayerTurn && b.State.Actor == 0 && !b.LessonActive && NoMarks(b), "Restaged quietly");
    }
    // Member on turn fires at the resident and misses (a rigged roll), so it stands and the shooter is heard.
    static async Task ShootMiss(ExpeditionBattlePanel b)
    {
        var s = b.State; int e = s.Units.FindIndex(u => u.Enemy && u.Alive);
        Check(e >= 0 && s.CanAttack(e, true), "Member " + s.Actor + " can shoot the resident");
        if (!b.Ranged) { await Tap(b.Shoot); b.Escape(); }
        await TapCell(b, true, s.Units[e].Depth, s.Units[e].Lane); Check(b.Execute.interactable, "Shot ready");
        int chance = s.HitChance(e, true); Rig(v => v[0] >= chance, 2); await Tap(b.Execute);
    }
    static void Rig(Func<int[], bool> accept, int count) { for (int seed = 1; seed < 200000; seed++) { UnityEngine.Random.InitState(seed); var v = new int[count]; for (int i = 0; i < count; i++) v[i] = UnityEngine.Random.Range(0, 100); if (accept(v)) { UnityEngine.Random.InitState(seed); return; } } throw new Exception("No seed"); }
    static async Task Tap(Button button, Vector2? local = null)
    {
        Check(button && button.IsActive() && button.IsInteractable(), "Unavailable " + (button ? button.name : "null")); Canvas.ForceUpdateCanvases(); var r = (RectTransform)button.transform;
        var data = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(button.GetComponentInParent<Canvas>().worldCamera, r.TransformPoint(local ?? r.rect.center)), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits); Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, "Blocked " + button.name + " by " + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler); await Task.Delay(100);
    }
    static Task TapCell(ExpeditionBattlePanel b, bool enemy, int depth, int lane) { var p = ExpeditionBattlePanel.Point(enemy, depth, lane); return Tap(b.CellButtons[(enemy ? 9 : 0) + lane * 3 + depth], new Vector2(p.x, -p.y)); }
    static async Task Until(Func<bool> done, int ms, string what) { var w = Stopwatch.StartNew(); while (!done()) { if (w.ElapsedMilliseconds > ms) throw new Exception("Timeout: " + what); await Task.Delay(20); } }
    static void Bounds(GameObject root) { Canvas.ForceUpdateCanvases(); foreach (var x in root.GetComponentsInChildren<Text>()) if (x.horizontalOverflow == HorizontalWrapMode.Wrap && !x.resizeTextForBestFit) Check(x.preferredHeight <= x.rectTransform.rect.height + 1, "Text overflow " + x.name + " " + x.preferredHeight + " / " + x.rectTransform.rect.height + ": " + x.text); }
    static string Shots => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "ResidentLessonCapture"));
    static Task Still(MonoBehaviour host, string name) { var done = new TaskCompletionSource<bool>(); host.StartCoroutine(StillRoutine(name, done)); return done.Task; }
    static IEnumerator StillRoutine(string name, TaskCompletionSource<bool> done)
    {
        yield return new WaitForEndOfFrame();
        var raw = new RenderTexture(Screen.width, Screen.height, 0); var rt = new RenderTexture(raw.width, raw.height, 0); ScreenCapture.CaptureScreenshotIntoRenderTexture(raw);
        if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(raw, rt, new Vector2(1, -1), new Vector2(0, 1)); else Graphics.Blit(raw, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); RenderTexture.active = prev; Object.Destroy(raw); Object.Destroy(rt);
        Directory.CreateDirectory(Shots); File.WriteAllBytes(Path.Combine(Shots, name + ".png"), tex.EncodeToPNG()); Object.Destroy(tex); done.SetResult(true);
    }
}
