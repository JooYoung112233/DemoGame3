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
using Random = System.Random;

// Creature combat rules: every wind-up marks cells, every strike lands on whoever stayed, dodging works,
// the forecast (and the move preview) always equals what the strike then does.
public static class VerifyCreatureBattle
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static int r;
    static BattleCreature Make(CreatureAttack attack, CreatureGait gait = CreatureGait.Walk, int health = 6, int damage = 2, int start = -1, int hold = 0)
        => new BattleCreature { Id = attack.ToString(), Name = attack.ToString(), Attack = attack, Gait = gait, Health = health, Damage = damage, StartDepth = start, HoldDepth = hold, HitChance = 70, AttackName = attack.ToString(), WindupName = "w" };
    static FieldBattleRules Quiet() => new FieldBattleRules { ReinforcementNoise = 99, CriticalChance = 0 };
    // allies: (depth, lane) each; the creature is placed after construction.
    static FieldBattleState Board(BattleCreature c, int ed, int el, params (int d, int l)[] allies)
    {
        var party = allies.Select((a, i) => new Adventurer("A" + i, "", "", 4, 0)).ToArray();
        var s = new FieldBattleState(party, 1, p => 9, p => true, () => r, Quiet(), new[] { c });
        for (int i = 0; i < allies.Length; i++) { s.Units[i].Depth = allies[i].d; s.Units[i].Lane = allies[i].l; }
        var e = s.Units.Last(); e.Depth = ed; e.Lane = el; return s;
    }
    // End every ally turn without leaving anyone on guard (guard changes strike damage).
    static void Pass(FieldBattleState s) { while (s.Outcome == FieldBattleOutcome.Playing && !s.Current.Enemy) { int a = s.Actor; s.Guard(); s.Units[a].Guarding = false; } }
    static int Cell(int d, int l) => FieldBattleState.CellOf(d, l);
    static BattleEvent Strike(FieldBattleState s) => s.Events.Single(e => e.Kind == BattleEventKind.Strike);
    // One enemy phase: forecast, act, and check the forecast matched the act.
    static void Enemy(FieldBattleState s, string label)
    {
        while (s.Outcome == FieldBattleOutcome.Playing && s.Current.Enemy)
        {
            var plan = s.PredictIntents().First(); s.EnemyStep();
            var strike = s.Events.FirstOrDefault(e => e.Kind == BattleEventKind.Strike);
            if (plan.Kind == EnemyIntentKind.Strike)
            {
                Check(strike != null, label + ": forecast strike but got " + string.Join(",", s.Events.Select(e => e.Kind)));
                if (plan.Attack != CreatureAttack.Swarm)
                    Check(Same(plan.Hits, strike.Hits), label + ": forecast hits " + Show(plan.Hits) + " but strike " + Show(strike.Hits));
            }
            if (plan.Kind == EnemyIntentKind.Windup) Check(s.Events.Any(e => e.Kind == BattleEventKind.Windup) && s.Units[plan.Enemy].Pending != null && s.Units[plan.Enemy].Pending.Cells.SequenceEqual(plan.Cells), label + ": windup mismatch");
        }
    }
    static bool Same(List<BattleHit> a, List<BattleHit> b) => a.Count == b.Count && a.Zip(b, (x, y) => x.Target == y.Target && x.Damage == y.Damage && x.Pushed == y.Pushed && x.Collided == y.Collided && x.ToDepth == y.ToDepth && x.Killed == y.Killed).All(v => v);
    static string Show(List<BattleHit> hits) => "[" + string.Join(" ", hits.Select(h => h.Target + ":" + h.Damage + (h.Pushed ? "p" : "") + (h.Collided ? "c" : ""))) + "]";

    public static string Rules()
    {
        var done = new List<string>();
        r = 99;

        // 문지기: marks its lane; the front-most ally at strike time takes it, slides back or knocks into the one behind.
        var door = Make(CreatureAttack.Shove, CreatureGait.Slow, 6, 1); door.FrontArmor = 2; door.Cover = true;
        var s = Board(door, 0, 1, (0, 1), (1, 1));
        Pass(s); Enemy(s, "door windup");
        var e = s.Units[2]; Check(e.Pending != null && e.Pending.Cells.SequenceEqual(new[] { Cell(0, 1), Cell(1, 1), Cell(2, 1) }) && s.DangerCells().Count() == 3, "Door marks its whole lane");
        Check(s.ArmorOf(2) == 2 && s.HitFactors(2, true).Count > 0, "Door facing the same lane: armor 2");
        s.Units[0].Lane = 1; Check(s.ArmorOf(2, true) == 2, "Front armor"); Pass(s); Enemy(s, "door strike");
        var hit = Strike(s).Hits.Single(); Check(hit.Target == 0 && hit.Collided && hit.Damage == 2 && s.Units[0].Health == 2 && s.Units[0].Depth == 0, "Door shove into a friend: +1 collision, no slide");
        s = Board(door, 0, 1, (0, 1), (2, 2)); Pass(s); Enemy(s, "door 2");
        Check(s.CanMove(0, 0) && s.Move(0, 0), "Step out of the marked lane"); s.Guard(); s.Units[0].Guarding = false; Pass(s); Enemy(s, "door dodge");
        Check(Strike(s).Whiff && s.Units[0].Health == 4 && s.Units[2].Stagger == 1, "Door hits nothing after a sidestep and is left open");
        s = Board(door, 0, 1, (0, 1), (2, 0)); s.Units[0].Guarding = false; Pass(s); Enemy(s, "door 3");
        Check(s.Current == s.Units[0] && s.ArmorOf(2) == 2, "Front lane armor"); s.Units[0].Lane = 0; Check(s.ArmorOf(2) == 0, "Flank ignores the door");
        s.Units[0].Lane = 1; Pass(s); Enemy(s, "door slide");
        hit = Strike(s).Hits.Single(); Check(hit.Pushed && !hit.Collided && hit.Damage == 1 && s.Units[0].Depth == 1, "Free cell behind: the shove slides the ally back");
        var cover = Board(door, 0, 1, (0, 0)); var behind = new FieldBattleState.Unit { Name = "x", Enemy = true, Health = 3, Maximum = 3, Depth = 2, Lane = 1 };
        cover.Units.Add(behind); Check(cover.Covered(2) && cover.HitFactors(2, true).Any(f => f.Key == "엄폐"), "Door covers the one behind it");
        done.Add("문지기 lane shove/slide/collision/dodge/front armor/cover");

        // 식탁밑: leap reach is two rows in total; stepping back out of reach makes it land on an empty cell.
        var chair = Make(CreatureAttack.Pounce, CreatureGait.Walk, 3, 2); chair.Armor = 1;
        s = Board(chair, 1, 1, (1, 1)); Pass(s); Enemy(s, "pounce windup");
        Check(s.Units[1].Pending.Cells.Single() == Cell(1, 1), "Pounce marks the ally in reach");
        s.Move(2, 1); s.Guard(); s.Units[0].Guarding = false; Enemy(s, "pounce dodge"); Check(Strike(s).Whiff && s.Units[0].Health == 4 && s.Units[1].Depth == 0 && s.Units[1].Stagger == 1, "Back step dodges the pounce; it lands in front, open");
        s = Board(chair, 1, 1, (2, 1)); Pass(s); Enemy(s, "out of reach"); Check(s.Units[1].Pending == null, "Row 1 + row 2 is out of leap reach");
        s = Board(chair, 0, 1, (1, 2)); Pass(s); Enemy(s, "pounce 2"); Pass(s); Enemy(s, "pounce lands");
        Check(Strike(s).Hits.Single().Damage == 2 && s.Units[0].Health == 2, "Pounce lands for 2");
        done.Add("식탁밑 reach/dodge/landing");

        // 틈새개: fast (two steps), marks its lane from any row, the first body in the lane takes 2 and is knocked back; opening after.
        var hound = Make(CreatureAttack.Charge, CreatureGait.Fast, 3, 2);
        s = Board(hound, 2, 0, (2, 2)); Pass(s); Enemy(s, "hound run");
        Check(s.Units[1].Lane == 2 && s.Events.Count(x => x.Kind == BattleEventKind.Shift || x.Kind == BattleEventKind.Advance) == 2, "Fast gait: two steps in one turn");
        Pass(s); Enemy(s, "hound windup"); Check(s.Units[1].Pending != null, "Hound lines up");
        Pass(s); Enemy(s, "hound strike");
        var ev = Strike(s); Check(ev.Hits.Single().Damage == 2 && !ev.Hits[0].Pushed && ev.Staggered && s.Units[1].Depth == 0 && s.Units[1].Stagger == 1, "Charge from the back row hits, ends in front, opens up");
        Check(s.HitChance(1, true) > 0 && s.HitFactors(1, true).Any(f => f.Key == "빈틈" && f.Value == s.Rules.StaggerHitBonus) && s.ArmorOf(1) == 0, "Opening: bonus to hit, no armor");
        Pass(s); Enemy(s, "hound recover"); Check(s.Events.Any(x => x.Kind == BattleEventKind.Recover) && s.Units[1].Stagger == 0, "Opening costs the hound its next turn");
        s = Board(hound, 1, 1, (0, 1)); Pass(s); Enemy(s, "hound 2"); s.Move(0, 0); s.Guard(); s.Units[0].Guarding = false; Enemy(s, "hound whiff");
        Check(Strike(s).Whiff && s.Units[1].Stagger == 1 && s.Units[0].Health == 4, "Sidestep: empty dash, still open");
        done.Add("틈새개 fast gait/lane dash/knockback/opening/whiff");

        // 귀기울임: a quiet party only gets groped (1); whoever shoots gets their cell marked; moving away makes the slam miss.
        var listener = Make(CreatureAttack.Listen, CreatureGait.Walk, 4, 2);
        s = Board(listener, 0, 0, (0, 0), (2, 2)); r = 0; Pass(s); Enemy(s, "grope"); r = 99;
        var grope = s.Events.Single(x => x.Kind == BattleEventKind.Attack); Check(grope.Damage == s.Rules.EnemyDamage, "Quiet: 1-damage grope, not the slam");
        s = Board(listener, 2, 2, (0, 0), (2, 1)); s.Attack(2, true); Check(s.Units[0].Loud > 0, "Shooting is loud"); s.Guard(); s.Units[1].Guarding = false;
        Enemy(s, "listen windup"); Check(s.Units[2].Pending?.Cells.Single() == Cell(0, 0), "Listener marks the shooter's cell");
        Check(s.Round == 2 && s.Units[0].Loud == 0, "A new round is quiet again");
        s.Move(1, 0); s.Guard(); s.Units[0].Guarding = false; Pass(s); Enemy(s, "slam whiff");
        Check(Strike(s).Whiff && s.Units[2].Stagger == 1, "Moved shooter: slam misses and opens it");
        s = Board(listener, 2, 2, (0, 0)); s.Attack(1, true); Enemy(s, "listen 2"); Pass(s); Enemy(s, "slam");
        Check(Strike(s).Hits.Single().Damage == 2 && s.Units[0].Health == 2 && s.Units[1].Stagger == 0, "Shooter stayed: slam lands for 2, no opening");
        done.Add("귀기울임 grope/loud mark/quiet round/whiff opening/slam");

        // 널린것: sweeps a lane: the front takes 1, everyone in it aims worse on their next turn.
        var sheet = Make(CreatureAttack.Veil, CreatureGait.Walk, 4, 1);
        s = Board(sheet, 1, 1, (0, 1), (2, 1), (0, 0)); Pass(s); Enemy(s, "veil walk"); Check(s.Units[3].Depth == 0 && s.Units[3].Pending == null, "Veil only from the front row");
        s = Board(sheet, 0, 1, (0, 1), (2, 1), (0, 0)); Pass(s); Enemy(s, "veil windup"); Check(s.Units[3].Pending.Lane == 1, "Veil picks the busiest lane");
        Pass(s); Enemy(s, "veil"); ev = Strike(s);
        Check(ev.Hits.Count == 2 && ev.Hits[0].Target == 0 && ev.Hits[0].Damage == 1 && ev.Hits[1].Damage == 0 && ev.Hits.All(x => x.Veiled) && s.Units[2].Veiled == 0, "Front takes 1, the lane is veiled");
        int normal = new FieldBattleState(new[] { new Adventurer("A", "", "", 4, 0) }, 1, p => 9, p => true, () => 99, Quiet()).HitChance(1, true);
        Check(s.Actor == 0 && s.HitFactors(3, true).Any(f => f.Key == "시야 가림"), "Veiled aim");
        s.Guard(); Check(s.Units[0].Veiled == 0, "Veil lasts one turn");
        done.Add("널린것 lane veil");

        // 계량원: holds the middle row, wires the two touching cells with the most people.
        var meter = Make(CreatureAttack.Wire, CreatureGait.Slow, 4, 2, 2, 0);
        s = Board(meter, 2, 1, (0, 0), (0, 1), (2, 2)); Pass(s); Enemy(s, "meter walk"); Check(s.Units[3].Depth == 1 && s.Units[3].Tired, "Slow step to its row");
        Pass(s); Enemy(s, "wire"); var wired = s.Units[3].Pending.Cells; Check(wired.Contains(Cell(0, 0)) && wired.Contains(Cell(0, 1)), "Wires the pair holding two allies");
        s.Move(1, 0); s.Guard(); s.Units[0].Guarding = false; Pass(s); Enemy(s, "zap");
        ev = Strike(s); Check(ev.Hits.Single().Target == 1 && ev.Hits[0].Damage == 2 && s.Units[0].Health == 4, "Only the one who stayed is shocked");
        Pass(s); Enemy(s, "meter steps in"); Check(s.Units[3].Depth == 0 && s.Units[3].Pending == null && !s.Units[3].Recharging, "After a discharge it steps closer before wiring again");
        Pass(s); Enemy(s, "meter rewires"); Check(s.Units[3].Pending != null, "Rewires from the front");
        done.Add("계량원 hold row/pair choice/discharge");

        // 먼지둥지: rolls on the target and every ally beside it; shots aim worse at it.
        var moth = Make(CreatureAttack.Swarm, CreatureGait.Fast, 3, 1); moth.ShotEvasion = 20;
        s = Board(moth, 0, 1, (0, 1), (0, 2), (1, 1)); r = 0; Pass(s); Enemy(s, "swarm"); r = 99;
        ev = Strike(s); Check(ev.Hits.Count == 2 && ev.Hits.All(x => x.Hit && x.Damage == 1) && s.Units[2].Health == 4, "Target and neighbour, not the one behind");
        Check(s.HitFactors(3, true).Any(f => f.Key == "흩어짐" && f.Value == -20), "Scattered: -20 to shots");
        done.Add("먼지둥지 splash/evasion");

        // 고인사람: marks the front ally's cell; if it stays, 1 damage and it cannot move next turn.
        var puddle = Make(CreatureAttack.Grab, CreatureGait.Slow, 5, 1); puddle.ShotArmor = 1;
        s = Board(puddle, 0, 0, (0, 0), (2, 2)); Pass(s); Enemy(s, "grab windup"); Pass(s); Enemy(s, "grab");
        Check(Strike(s).Hits.Single().Bound && s.Units[0].Bound == 1 && !s.CanMove(1, 0), "Bound: no move this turn");
        Check(s.ExpectedDamage(2, true) == s.Rules.ShotDamage - 1 && s.ExpectedDamage(2, false) == s.Rules.MeleeDamage, "Shots lose 1 in the water");
        s.Guard(); Check(s.Units[0].Bound == 0, "Bound for one turn");
        done.Add("고인사람 grab/bind/shot armor");

        // 계단등: topples toward the side with more people onto two front cells; 2 each; open (no armor) for a turn.
        var stairs = Make(CreatureAttack.Collapse, CreatureGait.Slow, 6, 2); stairs.Armor = 2;
        s = Board(stairs, 0, 1, (0, 0), (0, 1), (2, 2)); Check(s.ArmorOf(3) == 2, "Hard shell");
        Pass(s); Enemy(s, "lean"); Check(s.Units[3].Pending.Side == -1, "Leans toward the side with more people");
        Pass(s); Enemy(s, "collapse"); ev = Strike(s);
        Check(ev.Hits.Count == 2 && ev.Hits.All(x => x.Damage == 2) && ev.Staggered && s.Units[3].Stagger == 1, "Two front cells, 2 each, then open");
        Check(s.Actor == 0 && s.ArmorOf(3) == 0, "Shell open after the collapse");
        done.Add("계단등 side choice/two cells/opening armor 0");

        // 겹친이웃: the second head covers the cell the first victim would step back into.
        var twin = Make(CreatureAttack.Twin, CreatureGait.Walk, 5, 1);
        s = Board(twin, 0, 1, (0, 1)); Pass(s); Enemy(s, "twin windup"); Check(s.Units[1].Pending.Cells.SequenceEqual(new[] { Cell(0, 1), Cell(1, 1) }), "Second head watches the step back");
        s.Move(1, 1); s.Guard(); s.Units[0].Guarding = false; Enemy(s, "twin"); Check(Strike(s).Hits.Single().Damage == 1 && s.Units[0].Health == 3, "Stepping back walks into the second blow");
        s = Board(twin, 0, 1, (0, 1), (0, 2)); Pass(s); Enemy(s, "twin 2"); Check(s.Units[2].Pending.Cells[1] == Cell(0, 2), "Second head prefers a neighbour");
        Pass(s); Enemy(s, "twin both"); Check(Strike(s).Hits.Count == 2, "Both heads land");
        done.Add("겹친이웃 dodge cell/neighbour");

        // 수신목: never moves; tunes one turn, broadcasts the next (noise, which can call a reinforcement).
        var radio = Make(CreatureAttack.Broadcast, CreatureGait.Rooted, 3, 0, 2);
        var loud = new FieldBattleRules { ReinforcementNoise = 3, CriticalChance = 0 };
        s = new FieldBattleState(new[] { new Adventurer("A", "", "", 4, 0) }, 1, p => 0, p => false, () => 99, loud, new[] { radio }, new[] { hound });
        Check(s.Units[1].Depth == 2, "Starts at the back");
        var lone = new FieldBattleState(new[] { new Adventurer("A", "", "", 4, 0) }, 1, p => 0, p => false, () => 99, new FieldBattleRules { MaxReinforcements = 0 }, new[] { radio });
        lone.Guard(); Check(lone.Outcome == FieldBattleOutcome.Victory, "A broadcaster with nobody left to call ends the fight");
        Pass(s); Enemy(s, "tune"); Check(s.Units[1].Pending != null && !s.DangerCells().Any() && s.Units[1].Depth == 2, "Tuning marks nothing, does not move");
        // The tuning turn is the warning: the broadcast raises the alarm and the called creature joins as the next round opens.
        Pass(s); Enemy(s, "broadcast"); Check(s.Noise == 3 && s.Events.Any(x => x.Kind == BattleEventKind.Alarm) && s.Events.Any(x => x.Kind == BattleEventKind.Reinforce), "Broadcast noise calls a reinforcement");
        Check(s.Units.Count == 3 && s.Units[2].Creature == hound && s.Units[2].Reinforcement, "Reinforcement drawn from the pool");
        done.Add("수신목 rooted/tune/broadcast/pool reinforcement");

        // 빈수레: the whole lane takes 1 and slides back; the one against the wall (or a stuck friend) takes the knock.
        var cart = Make(CreatureAttack.Ram, CreatureGait.Walk, 5, 1); cart.Armor = 1;
        s = Board(cart, 1, 2, (0, 2), (1, 2)); Pass(s); Enemy(s, "ram windup"); Pass(s); Enemy(s, "ram");
        ev = Strike(s); Check(ev.Hits.Count == 2 && ev.Hits.All(x => x.Pushed && x.Damage == 1) && s.Units[0].Depth == 1 && s.Units[1].Depth == 2 && s.Units[2].Depth == 0 && ev.Staggered, "Lane bowled back one row");
        s = Board(cart, 1, 2, (1, 2), (2, 2)); Pass(s); Enemy(s, "ram 2"); Pass(s); Enemy(s, "ram wall");
        ev = Strike(s); Check(ev.Hits.All(x => x.Collided && x.Damage == 2), "Wall and a stuck friend: 2 each");
        done.Add("빈수레 lane push/wall");

        // Guard against a committed strike: -1 and no push.
        s = Board(door, 0, 1, (0, 1)); Pass(s); Enemy(s, "door g"); s.Guard(); Enemy(s, "guarded");
        hit = Strike(s).Hits.Single(); Check(hit.Braced && hit.Damage == 0 && !hit.Pushed && s.Units[0].Depth == 0, "Guard braces: -1 and stands firm");
        done.Add("guard braces");

        // Names, start rows, lineup cap.
        s = new FieldBattleState(new[] { new Adventurer("A", "", "", 4, 0) }, 3, p => 0, p => false, () => 99, Quiet(), new[] { hound, hound, meter });
        Check(s.Units.Count(u => u.Enemy) == 3 && s.Units[1].Name == "Charge" && s.Units[2].Name == "Charge 2" && s.Units[1].Depth == s.Rules.FirstEnemyDepth && s.Units[2].Depth == s.Rules.OtherEnemyDepth && s.Units[3].Depth == 2, "Unique names, start rows (rules rows unless a type fixes its own)");
        done.Add("names/start rows");

        // Forecast = result, including the move preview, over random boards for every committed type.
        var rng = new Random(7); int trials = 0;
        var types = new[] { door, chair, hound, listener, sheet, meter, puddle, stairs, twin, cart };
        foreach (var type in types)
            for (int t = 0; t < 120; t++)
            {
                var cells = Enumerable.Range(0, 9).OrderBy(_ => rng.Next()).Take(rng.Next(1, 4)).Select(x => (x % 3, x / 3)).ToArray();
                s = Board(type, rng.Next(0, 3), rng.Next(0, 3), cells);
                if (type == listener) s.Units[0].Loud = 1;
                Pass(s); Enemy(s, type.Name + " windup " + t);
                if (s.Outcome != FieldBattleOutcome.Playing) continue;
                // Preview a legal move for the first ally, then make it and compare.
                var mover = s.Actor; var moves = new[] { (0, 1), (0, -1), (1, 0), (-1, 0) }.Select(m => (d: s.Current.Depth + m.Item1, l: s.Current.Lane + m.Item2)).Where(m => s.CanMove(m.d, m.l)).ToList();
                if (moves.Count > 0 && rng.Next(2) == 0)
                {
                    var m = moves[rng.Next(moves.Count)]; var preview = s.PredictIntents(mover, m.d, m.l);
                    s.Move(m.d, m.l); var now = s.PredictIntents();
                    Check(preview.Count == now.Count && preview.Zip(now, (a, b) => a.Kind == b.Kind && (a.Hits == null) == (b.Hits == null) && (a.Hits == null || Same(a.Hits, b.Hits))).All(v => v), type.Name + ": move preview differs from the board after moving");
                }
                if (rng.Next(3) == 0) s.Guard(); else { int a = s.Actor; s.Guard(); s.Units[a].Guarding = false; }
                Pass(s); Enemy(s, type.Name + " strike " + t); trials++;
            }
        done.Add("forecast = strike and move preview = board after the move (" + trials + " random boards)");
        return "PASS: " + string.Join("; ", done);
    }

    // ---------------- Play mode: the real battle UI against creatures (run VerifyFieldBattle.Enter + BeginPreview first) ----------------
    static async Task Tap(Button button, Vector2? local = null)
    {
        Check(button.IsActive() && button.IsInteractable(), "Unavailable " + button.name); Canvas.ForceUpdateCanvases(); var rt = (RectTransform)button.transform;
        var data = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(button.GetComponentInParent<Canvas>().worldCamera, rt.TransformPoint(local ?? rt.rect.center)), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits); Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, "Blocked " + button.name + " by " + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler); await Task.Delay(70);
    }
    static Task TapCell(ExpeditionBattlePanel b, bool enemy, int depth, int lane) { var p = ExpeditionBattlePanel.Point(enemy, depth, lane); return Tap(b.CellButtons[(enemy ? 9 : 0) + lane * 3 + depth], new Vector2(p.x, -p.y)); }
    static async Task Until(Func<bool> done, int milliseconds, string what) { var watch = Stopwatch.StartNew(); while (!done()) { if (watch.ElapsedMilliseconds > milliseconds) throw new Exception("Timeout: " + what); await Task.Delay(30); } }
    static void Rig(Func<int[], bool> accept, int count) { for (int seed = 1; seed < 200000; seed++) { UnityEngine.Random.InitState(seed); var v = new int[count]; for (int i = 0; i < count; i++) v[i] = UnityEngine.Random.Range(0, 100); if (accept(v)) { UnityEngine.Random.InitState(seed); return; } } throw new Exception("No seed"); }
    static void Bounds(GameObject root) { Canvas.ForceUpdateCanvases(); foreach (var t in root.GetComponentsInChildren<Text>()) if (t.horizontalOverflow == HorizontalWrapMode.Wrap && !t.resizeTextForBestFit) Check(t.preferredHeight <= t.rectTransform.rect.height + 1, "Text overflow " + t.name + " " + t.preferredHeight + " / " + t.rectTransform.rect.height + ": " + t.text); }
    static ExpeditionArrivalPanel Arrival => Object.FindAnyObjectByType<SettlementController>().ArrivalPanel;
    static BattleCreatureRoster Roster(ExpeditionBattlePanel b) { Check(b.Creatures && b.Creatures.Creatures.Count == 12, "Roster not wired (BuildCreatureBattle.Run)"); return b.Creatures; }
    // Restart the open battle with one creature, everyone healed and restocked, units on the given cells.
    static async Task Stage(ExpeditionBattlePanel b, BattleCreature c, (int d, int l) enemy, params (int d, int l)[] allies)
    {
        var a = Arrival; foreach (var p in a.Participants) { p.Health = p.MaxHealth; int ammo = a.Inventory.CountFor(p, "ammo"); if (ammo < 3) a.Inventory.TransferField(p, "ammo", 3 - ammo, true); }
        b.Restage(new[] { c }); await Task.Delay(200);
        var s = b.State; Check(s.Units.Count(u => u.Enemy) == 1 && s.Units.Last().Creature == c, "Restaged " + c.Name);
        for (int i = 0; i < allies.Length && i < s.Units.Count - 1; i++) { s.Units[i].Depth = allies[i].d; s.Units[i].Lane = allies[i].l; }
        var e = s.Units.Last(); e.Depth = enemy.d; e.Lane = enemy.l; b.ReviewSync(); await Task.Delay(150);
    }
    static int ActiveLines(ExpeditionBattlePanel b) => b.HudLayer.GetComponentsInChildren<BattleThreatLine>().Length;

    public static async Task<string> Flow()
    {
        var a = Arrival; var b = a.Encounter.Battle; Check(b.IsOpen, "Open a battle first (VerifyFieldBattle.BeginPreview)"); var roster = Roster(b);
        float speed = b.Presentation.Speed, pause = b.ActionPause; var before = UnityEngine.Random.state; b.Presentation.Speed = 4; b.ActionPause = .05f;
        try
        {
            var hound = roster.Find("04-seam-hound"); await Stage(b, hound, (1, 1), (0, 1), (1, 0)); var s = b.State;
            await Until(() => !b.Busy, 4000, "first turn");
            Check(b.PhaseBanner && b.Round.text.Contains(b.AllyPhaseTitle), "Round tag names the party's phase");
            Check(b.CellButtons.Take(9).All(x => x.interactable), "Board open on our phase");
            // Round 1: both guard; the hound lines up during the enemy phase, announced by the banner.
            await Tap(b.Guard); await Until(() => !b.Busy, 4000, "ally 2"); await Tap(b.Guard);
            await Until(() => b.PhaseBanner.gameObject.activeSelf && b.PhaseBanner.Title.text == b.EnemyPhaseTitle, 3000, "enemy banner");
            Check(b.Round.text.Contains(b.EnemyPhaseTitle), "Enemy phase tag");
            await Until(() => !b.Busy, 8000, "enemy phase"); await Task.Delay(150);
            var e = s.Units.Last(); Check(e.Pending != null && e.Pending.Lane == 1, "Hound marked lane 1");
            var huds = b.HudLayer.GetComponentsInChildren<BattlePawnHud>(); var enemyHud = huds.First(h => h.name.StartsWith(e.Name)); var allyHud = huds.First(h => h.name.StartsWith(s.Units[0].Name));
            Check(enemyHud.IntentLabel.text == hound.AttackName + " -" + hound.Damage && allyHud.Danger.activeSelf && allyHud.DangerLabel.text == hound.AttackName + " -" + hound.Damage, "Matching tags: " + enemyHud.IntentLabel.text + " / " + allyHud.DangerLabel.text);
            Check(ActiveLines(b) == 0, "No threat line without a pointer");
            int enemyCell = 9 + e.Lane * 3 + e.Depth; b.Hover(enemyCell, true); await Task.Delay(120); Check(ActiveLines(b) == 1, "Pointer on the hound links its target"); b.Hover(enemyCell, false); await Task.Delay(80);
            int allyCell = s.Units[0].Lane * 3 + s.Units[0].Depth; b.Hover(allyCell, true); await Task.Delay(120); Check(ActiveLines(b) == 1, "Pointer on the ally links its attacker"); b.Hover(allyCell, false);
            await Tap(b.Shoot); b.Hover(enemyCell, true); await Task.Delay(120); Check(b.Aiming && ActiveLines(b) == 0, "Aiming never shows enemy lines"); b.Hover(enemyCell, false); b.Escape();
            b.Hover(0, true); await Task.Delay(80); Check(b.Hint.text.Contains("공격받지 않습니다"), "Preview: stepping to lane 0 is safe: " + b.Hint.text); b.Hover(0, false);
            await TapCell(b, false, 0, 0); await Until(() => !b.Busy, 4000, "dodge"); Check(s.Units[0].Lane == 0, "Dodged out of the lane");
            await Tap(b.Guard); await Until(() => !b.Busy, 4000, "ally 2 turn"); await Tap(b.Guard);
            await Until(() => !b.Busy, 8000, "strike");
            Check(e.Stagger == 1 && s.Units[0].Health == s.Units[0].Maximum, "Empty dash: nobody hurt, hound open");
            Check(b.HudLayer.GetComponentsInChildren<BattlePawnHud>().First(h => h.name.StartsWith(e.Name)).IntentLabel.text == "빈틈", "Opening tag");
            Bounds(b.View);
            return "PASS: phase banner/tag per side, windup marks lane, matching attacker/victim tags, no lines at rest, pointer links (enemy->ally, ally<-enemy), none while aiming, safe-move preview, dodge -> empty dash -> opening, text bounds.";
        }
        finally { UnityEngine.Random.state = before; b.Presentation.Speed = speed; b.ActionPause = pause; }
    }

    // ---------------- review captures: one clip per creature (wind-up still, then the strike at real speed) ----------------
    sealed class Recorder
    {
        public readonly List<Texture2D> Frames = new List<Texture2D>(); public readonly List<float> Times = new List<float>(); public bool Running;
        public IEnumerator Run(int width, float interval)
        {
            Running = true; float next = 0; RenderTexture full = null, small = null;
            while (Running)
            {
                yield return new WaitForEndOfFrame();
                if (Time.unscaledTime < next) continue; next = Time.unscaledTime + interval;
                if (full == null || full.width != Screen.width || full.height != Screen.height) { if (full) full.Release(); full = new RenderTexture(Screen.width, Screen.height, 0); }
                int height = Mathf.RoundToInt(width * (float)Screen.height / Screen.width); if (small == null) small = new RenderTexture(width, height, 0);
                ScreenCapture.CaptureScreenshotIntoRenderTexture(full); Upright(full, small);
                var previous = RenderTexture.active; RenderTexture.active = small; var tex = new Texture2D(small.width, small.height, TextureFormat.RGB24, false); tex.ReadPixels(new UnityEngine.Rect(0, 0, small.width, small.height), 0, 0); tex.Apply(); RenderTexture.active = previous;
                Frames.Add(tex); Times.Add(Time.unscaledTime);
            }
            if (full) Object.Destroy(full); if (small) Object.Destroy(small);
        }
        public int Save(string folder)
        {
            Directory.CreateDirectory(folder); foreach (var f in Directory.GetFiles(folder, "*.png")) File.Delete(f);
            for (int i = 0; i < Frames.Count; i++) { File.WriteAllBytes(Path.Combine(folder, "frame-" + i.ToString("000") + ".png"), Frames[i].EncodeToPNG()); Object.Destroy(Frames[i]); }
            File.WriteAllLines(Path.Combine(folder, "times.txt"), Times.Select(t => t.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)));
            int n = Frames.Count; Frames.Clear(); Times.Clear(); return n;
        }
    }
    static void Upright(RenderTexture source, RenderTexture target) { if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(source, target, new Vector2(1, -1), new Vector2(0, 1)); else Graphics.Blit(source, target); }
    static string Shots => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "CreatureCapture"));
    static Task Still(ExpeditionBattlePanel b, string name) { var done = new TaskCompletionSource<bool>(); b.StartCoroutine(StillRoutine(name, done)); return done.Task; }
    static IEnumerator StillRoutine(string name, TaskCompletionSource<bool> done)
    {
        yield return new WaitForEndOfFrame();
        var raw = new RenderTexture(Screen.width, Screen.height, 0); var rt = new RenderTexture(Screen.width, Screen.height, 0); ScreenCapture.CaptureScreenshotIntoRenderTexture(raw); Upright(raw, rt);
        var previous = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new UnityEngine.Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); RenderTexture.active = previous; Object.Destroy(raw); Object.Destroy(rt);
        Directory.CreateDirectory(Shots); File.WriteAllBytes(Path.Combine(Shots, name + ".png"), tex.EncodeToPNG()); Object.Destroy(tex); done.SetResult(true);
    }
    static async Task<int> Record(ExpeditionBattlePanel b, string clip, Func<Task> action)
    {
        var recorder = new Recorder(); b.StartCoroutine(recorder.Run(800, 1 / 24f));
        try { await Task.Delay(200); await action(); await Until(() => !b.Busy, 20000, "clip " + clip); await Task.Delay(900); }
        catch { recorder.Running = false; await Task.Delay(120); foreach (var f in recorder.Frames) Object.Destroy(f); recorder.Frames.Clear(); throw; }
        recorder.Running = false; await Task.Delay(120); return recorder.Save(Path.Combine(Shots, clip));
    }
    // Stage per type: creature cell, the ally who stays (index 0), the ally who steps aside (index 1).
    static readonly Dictionary<string, ((int, int) enemy, (int, int) stay, (int, int) other)> Stages = new Dictionary<string, ((int, int), (int, int), (int, int))>
    {
        ["01-door-bearer"] = ((0, 1), (0, 1), (0, 0)), ["02-listener"] = ((1, 1), (0, 1), (1, 0)), ["03-under-table"] = ((0, 1), (0, 1), (1, 2)),
        ["04-seam-hound"] = ((1, 1), (1, 1), (0, 0)), ["05-laundry"] = ((1, 1), (0, 1), (2, 1)), ["06-meter-keeper"] = ((1, 1), (0, 1), (0, 2)),
        ["07-moth-nest"] = ((0, 1), (0, 1), (0, 2)), ["08-puddle"] = ((0, 1), (0, 1), (1, 0)), ["09-stairback"] = ((0, 1), (0, 1), (0, 0)),
        ["10-twin-coat"] = ((0, 1), (0, 1), (1, 2)), ["11-root-receiver"] = ((2, 1), (0, 1), (1, 0)), ["12-bellied-cart"] = ((1, 2), (0, 2), (1, 2)),
    };
    // Ally turn used in the clips: shoot the creature (a rigged hit, not a critical, or a rigged miss) or guard when no shot is possible.
    static async Task Shoot(ExpeditionBattlePanel b, bool hit)
    {
        var s = b.State; int e = s.Units.FindIndex(u => u.Enemy && u.Alive);
        if (e < 0 || !s.CanAttack(e, true)) { await Tap(b.Guard); return; }
        var u = s.Units[e]; if (!b.Ranged) { await Tap(b.Shoot); b.Escape(); }
        await TapCell(b, true, u.Depth, u.Lane); if (!b.Execute.interactable) { await Tap(b.Guard); return; }
        int chance = s.HitChance(e, true); Rig(v => hit ? v[0] < chance && v[1] >= s.Rules.CriticalChance : v[0] >= chance, 2); await Tap(b.Execute);
    }
    public static async Task<string> Gallery(string only = null)
    {
        var a = Arrival; var b = a.Encounter.Battle; Check(b.IsOpen, "Open a battle first"); var roster = Roster(b); var log = new List<string>();
        float speed = b.Presentation.Speed, pause = b.ActionPause; var before = UnityEngine.Random.state;
        try
        {
            foreach (var c in roster.Creatures)
            {
                if (only != null && !only.Split(',').Contains(c.Id)) continue;
                var stage = Stages[c.Id]; b.Presentation.Speed = 5; b.ActionPause = .02f;
                await Stage(b, c, stage.enemy, stage.stay, stage.other); var s = b.State; await Until(() => !b.Busy, 4000, "turn");
                bool tracking = !c.Committed;
                if (!tracking)
                {
                    // Round 1 (fast): the listener needs a shot to hear; everyone else just holds. The creature winds up.
                    if (c.Attack == CreatureAttack.Listen) await Shoot(b, false); else await Tap(b.Guard);
                    await Until(() => !b.Busy, 6000, "ally 2"); await Tap(b.Guard); await Until(() => !b.Busy, 12000, "windup phase");
                    Check(s.Units.Last().Pending != null, c.Name + " did not wind up");
                    b.Presentation.Speed = 1; b.ActionPause = .3f; await Task.Delay(1400); await Still(b, c.Id + "-a-windup");
                }
                else { b.Presentation.Speed = 1; b.ActionPause = .3f; await Task.Delay(1400); await Still(b, c.Id + "-a-ready"); }
                // Round 2 at real speed: the ally in the marked cell stays and shoots, the other steps aside; the creature strikes.
                int frames = await Record(b, c.Id, async () =>
                {
                    await Shoot(b, true); await Until(() => !b.Busy, 8000, "ally 2");
                    if (s.Outcome != FieldBattleOutcome.Playing) return;
                    var me = s.Current; var aside = new[] { (0, -1), (0, 1), (1, 0), (-1, 0) }.Select(m => (d: me.Depth + m.Item1, l: me.Lane + m.Item2)).FirstOrDefault(m => s.CanMove(m.d, m.l) && !s.PredictIntents(s.Actor, m.d, m.l).Any(p => p.Hits != null && p.Hits.Any(h => h.Target == s.Actor)));
                    if (!tracking && s.CanMove(aside.d, aside.l) && s.DangerCells().Contains(FieldBattleState.CellOf(me.Depth, me.Lane))) { await TapCell(b, false, aside.d, aside.l); await Until(() => !b.Busy, 4000, "aside"); }
                    await Tap(b.Guard);
                });
                log.Add(c.Name + " " + frames);
                await Task.Delay(200); await Still(b, c.Id + "-b-after");
            }
            return "Captured to " + Shots + " · " + string.Join(", ", log);
        }
        finally { UnityEngine.Random.state = before; b.Presentation.Speed = speed; b.ActionPause = pause; }
    }
}
