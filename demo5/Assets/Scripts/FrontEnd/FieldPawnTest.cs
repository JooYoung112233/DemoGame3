#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // 말 놓기 test helpers (기획/탐험-말놓기-조작-재설계.md · design §4): the verify scripts place member pawns the way the board does
    // (FieldPawnBoard.Hold → Drop: FieldPlacement's own options, checked on a copy of the plan), pass turns with '턴 진행' only, and open
    // the read-only 07 window with a right press's call (Inspect). AgentScripts are compiled one file at a time, so the shared helpers live
    // here, in the game assembly, editor only (never in a build). No time passes in any helper except Turn / SearchToEnd / Move.
    public static class FieldPawnTest
    {
        public static FieldPawnBoard Board(ExpeditionArrivalPanel a) => FieldPawnBoard.For(a);
        public static FieldPlacement Rules(ExpeditionArrivalPanel a) => FieldPlacement.Of(a);
        public static FieldTurnPlanner Planner(ExpeditionArrivalPanel a) => a && a.Threat ? a.Threat.Planner : null;
        // Placing is possible now (both visits; no window, walk, turn or meeting holds the room).
        public static bool Ready(ExpeditionArrivalPanel a) { var r = Rules(a); return r != null && r.Ready; }
        public static bool Alive(ExpeditionArrivalPanel a, int m) => a && m >= 0 && m < a.Participants.Count && a.Participants[m] != null && a.Participants[m].Health > 0;
        public static IEnumerable<int> Living(ExpeditionArrivalPanel a) { for (int m = 0; a && m < a.Participants.Count; m++) if (Alive(a, m)) yield return m; }
        // The plan's check now (the same one the preview and the turn use).
        public static FieldPlanCheck Check(ExpeditionArrivalPanel a) { var r = Rules(a); return r != null ? r.CheckNow() : null; }
        // What the member does this turn (Hush = free).
        public static FieldAction ActionOf(ExpeditionArrivalPanel a, int m) { var k = Check(a); return k != null && m >= 0 && m < k.Actions.Length ? k.Actions[m] : FieldAction.Hush; }
        // The first living member with nothing to do (not `except`), else −1.
        public static int Free(ExpeditionArrivalPanel a, int except = -1) { foreach (int m in Living(a)) if (m != except && ActionOf(a, m) == FieldAction.Hush) return m; return -1; }

        // ---- the places a member can take (a silhouette each; a disabled one is only a grey pin with its reason) ----
        public static IReadOnlyList<FieldPlaceOption> Options(ExpeditionArrivalPanel a, int member) { var r = Rules(a); return r != null ? r.OptionsFor(member) : new List<FieldPlaceOption>(); }
        // key: "search:N", "door:R" (the room behind the door), "observe:ID"; kind narrows it (a door can be Listen or Gather).
        public static FieldPlaceOption Option(ExpeditionArrivalPanel a, int member, string key, FieldSpotKind? kind = null)
            => Options(a, member).FirstOrDefault(o => o.Key == key && (!kind.HasValue || o.Kind == kind.Value));
        public static string SearchKey(int site) => FieldSpotRef.SearchKey(site);
        public static string DoorKey(int behind) => FieldSpotRef.DoorKey(behind);
        public static string ObserveKey(string id = ExpeditionNpcStory.ObservationId) => FieldSpotRef.ObserveKey(id);

        // ---- placing (no time) ----
        // Put the member at that place as the board does (Hold + Drop: the status paper, the '?' mark, the walk); nothing stays held.
        // False when the place is not offered, is greyed out or the rules refused it.
        public static bool Place(ExpeditionArrivalPanel a, int member, string key, FieldSpotKind? kind = null)
        {
            var o = Option(a, member, key, kind); if (o == null || !o.Enabled) return false;
            var b = Board(a); bool ok;
            if (b && b.isActiveAndEnabled) { b.Cancel(); if (!b.Hold(member)) return false; ok = b.Drop(o); b.Cancel(); }
            else ok = Rules(a).Place(o);
            return ok;
        }
        public static bool Lead(ExpeditionArrivalPanel a, int member, int site) => Place(a, member, SearchKey(site), FieldSpotKind.Lead);
        // The second pawn on an object: co-op (duty 0 함께 · 1 망보기 · 2 조명: the role chip under it).
        public static bool Join(ExpeditionArrivalPanel a, int member, int site, int duty = 0)
        {
            if (!Place(a, member, SearchKey(site), FieldSpotKind.Join)) return false;
            return duty == 0 && Role(a, site) == FieldAction.Together || Choose(a, member, duty);
        }
        // Lead + helper on one object (lead: the member already leading it, else `lead`, else the first free member; helper: `helper`,
        // else the first other free member, else any other living member). True when the helper is bound with that role.
        public static bool Coop(ExpeditionArrivalPanel a, int site, int duty = 0, int lead = -1, int helper = -1)
        {
            var run = Check(a)?.RunFor(site);
            if (run == null)
            {
                if (lead < 0) lead = Free(a); if (lead < 0) lead = Living(a).FirstOrDefault();
                if (!Lead(a, lead, site)) return false;
                run = Check(a)?.RunFor(site); if (run == null) return false;
            }
            lead = run.Lead;
            if (run.Support >= 0) return (helper < 0 || run.Support == helper) && (run.Duty == duty || Choose(a, run.Support, duty));
            if (helper < 0) helper = Free(a, lead);
            if (helper < 0) foreach (int m in Living(a)) if (m != lead && ActionOf(a, m) != FieldAction.Lead) { helper = m; break; }
            if (helper < 0 || helper == lead) return false;
            return Join(a, helper, site, duty);
        }
        // The helper's role on that object (Together / Watch / Light), Hush when none.
        public static FieldAction Role(ExpeditionArrivalPanel a, int site) { var r = Check(a)?.RunFor(site); return r != null && r.Support >= 0 ? r.Role : FieldAction.Hush; }
        // A role chip under a helper (0 함께 · 1 망보기 · 2 조명), or 이동 / 귀 대기 under a lone member at a door.
        public static bool Choose(ExpeditionArrivalPanel a, int member, int chip) { var r = Rules(a); return r != null && r.Choose(member, chip); }
        public static bool Listen(ExpeditionArrivalPanel a, int member, int door) => Place(a, member, DoorKey(door), FieldSpotKind.Listen);
        public static bool Observe(ExpeditionArrivalPanel a, int member, string id = ExpeditionNpcStory.ObservationId) => Place(a, member, ObserveKey(id), FieldSpotKind.Observe);
        // Every living member at that door (the first may listen there on the awake board; the last one sets the move). True when the
        // move to `door` is then reserved (ExpeditionRoomNavigation.HasQueuedMove).
        public static bool Gather(ExpeditionArrivalPanel a, int door)
        {
            var r = Rules(a); if (r == null || !a.Rooms) return false;
            foreach (int m in Living(a).ToList())
            {
                if (r.AtDoor(door).Contains(m)) continue;
                var o = r.OptionsFor(m).FirstOrDefault(x => x.Door == door && x.Enabled && (x.Kind == FieldSpotKind.Gather || x.Kind == FieldSpotKind.Listen));
                if (o == null || !Place(a, m, o.Key, o.Kind)) return false;
            }
            return a.Rooms.HasQueuedMove && a.Rooms.QueuedRoom == door;
        }
        // Take the member off whatever they do (a right press on the pawn / the pawn dropped on the floor): the pawn rules apply.
        public static bool Unassign(ExpeditionArrivalPanel a, int member) { var b = Board(a); return b && b.isActiveAndEnabled ? b.Unassign(member) : Rules(a) != null && Rules(a).Unassign(member); }
        // Nothing placed (no time).
        public static void Clear(ExpeditionArrivalPanel a)
        {
            var pl = Planner(a); if (!pl) return; var b = Board(a); if (b) b.Cancel();
            pl.Plan.Clear(); if (a.Threat) a.Threat.Refresh();
        }

        // ---- time: '턴 진행' only ----
        // Press '턴 진행' once (a real button press; a member with nothing to do hushes without the question unless askIdle). Waits out the
        // planner's double-press guard first and a room move to its end. True when a turn passed (or a gathered move was made).
        public static async Task<bool> Turn(ExpeditionArrivalPanel a, bool askIdle = false)
        {
            var pl = Planner(a); if (!pl || !pl.TurnButton || !a.Rooms) return false;
            await Until(() => !a.InTransit && !pl.Resolving, 8000);
            await Task.Delay(Mathf.CeilToInt(pl.TurnCooldown * 1000) + 40);
            if (!pl.TurnButton.IsActive() || !pl.TurnButton.IsInteractable()) return false;
            int turns = a.Rooms.Turns; bool moving = a.Rooms.HasQueuedMove;
            if (askIdle) pl.TurnButton.onClick.Invoke(); else FieldIdleConfirm.Pass(() => pl.TurnButton.onClick.Invoke());
            if (moving && a.InTransit) await Until(() => !a.InTransit, 8000);
            await Task.Delay(60);
            return a.Rooms.Turns != turns;
        }
        // Place (co-op unless coop is false or only one member lives) and pass turns until the object completes (its finds open), a
        // meeting opens or no turn passes; the pawns stay placed between turns. The turns spent.
        public static async Task<int> SearchToEnd(ExpeditionArrivalPanel a, int site, bool coop = true, int maxTurns = 8)
        {
            int spent = 0;
            for (int i = 0; i < maxTurns; i++)
            {
                if (a.Loot.Peek(site, out var s) && s.Complete || a.Encounter && a.Encounter.IsOpen || a.Loot.IsOpen) break;
                var run = Check(a)?.RunFor(site);
                if (run == null)
                {
                    bool placed = coop && Living(a).Count() > 1 && Coop(a, site);
                    if (!placed) { int m = Free(a); if (m < 0) m = Living(a).FirstOrDefault(); placed = Lead(a, m, site) || Check(a)?.RunFor(site) != null; }
                    if (!placed) break;
                }
                int before = a.Rooms.Turns;
                if (!await Turn(a)) break;
                spent += a.Rooms.Turns - before;
            }
            return spent;
        }
        // Everyone to the door onto `next`, '턴 진행', and the walk to its end. True when the party stands in `next`.
        public static async Task<bool> Move(ExpeditionArrivalPanel a, int next)
        {
            if (!Gather(a, next)) return false;
            if (!await Turn(a)) return false;
            await Until(() => !a.InTransit, 8000); await Task.Delay(150);
            return a.Rooms.CurrentRoom == next;
        }

        // ---- reading ----
        // The 07 window (read only) or a finished object's finds: what a right press on the object opens (ExpeditionArrivalPanel.Inspect).
        public static bool Detail(ExpeditionArrivalPanel a, int index) { a.Inspect(index); return a.Search && a.Search.IsOpen || a.Loot && a.Loot.IsOpen || a.Popup.activeSelf; }
        // The board's Buttons (for real presses: the tutorial points at these). Null while hidden (nothing held / no such place).
        public static Button Handle(ExpeditionArrivalPanel a, int member) { var b = Board(a); return b ? b.HandleOf(member) : null; }
        public static Button Ghost(ExpeditionArrivalPanel a, string key, int slot = -1) { var b = Board(a); return b ? b.GhostOf(key, slot) : null; }
        public static Button Door(ExpeditionArrivalPanel a, int behind) => a && a.Rooms ? FieldPlacement.DoorButton(a, a.Rooms.CurrentRoom, behind) : null;
        // A line for failure messages: who does what now.
        public static string Describe(ExpeditionArrivalPanel a) { var r = Rules(a); return r == null ? "no rules" : string.Join(" / ", Living(a).Select(m => r.Describe(m))); }

        static async Task Until(Func<bool> done, int ms)
        {
            var w = Stopwatch.StartNew();
            while (!done()) { if (w.ElapsedMilliseconds > ms) throw new Exception("FieldPawnTest: timed out"); await Task.Delay(30); }
        }
    }
}
#endif
