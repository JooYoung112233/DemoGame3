using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Demo5.FrontEnd
{
    // Creature wind-ups and strikes. Each type keeps its own rhythm and weight, but all read the same way:
    // the wind-up turn holds a pose and marks cells, the strike turn lands on whoever stayed, big moves leave an opening.
    // Standees still only move, tilt, squash and tint.
    public sealed partial class BattlePresentation
    {
        [Header("크리쳐 · 공통")]
        public AudioClip Collide;
        public Color VeilColor = new Color(.74f, .76f, .8f), ShockColor = new Color(.74f, .95f, 1f);
        [Min(0)] public float WindupHold = .5f, CreatureRecover = .32f;

        BattleCreature CreatureOf(int unit) => panel && panel.State != null && unit >= 0 && unit < panel.State.Units.Count ? panel.State.Units[unit].Creature : null;
        static Color AccentOf(BattleCreature c, Color fallback) => c != null ? c.Accent : fallback;
        void CreatureSound(BattleCreature c, AudioClip clip, AudioClip fallback, float volume = 1, float pitch = 1) => Sound(clip ? clip : fallback, volume, pitch * (c != null ? c.Pitch : 1));
        static AudioClip WindupOf(BattleCreature c) => c != null ? c.WindupSound : null;
        static AudioClip StrikeOf(BattleCreature c) => c != null ? c.StrikeSound : null;
        static AudioClip ImpactOf(BattleCreature c) => c != null ? c.ImpactSound : null;
        Vector3 CellLocal(int cell) => panel.CellHome(false, cell % 3, cell / 3);
        Vector2 CellFx(int cell) => panel.FxPoint(panel.CellWorld(false, cell % 3, cell / 3));
        IEnumerator SoundAfter(float seconds, BattleCreature c, AudioClip clip, AudioClip fallback, float volume = 1) { yield return Wait(seconds); CreatureSound(c, clip, fallback, volume); }
        // Runs routines side by side and waits for the slowest.
        IEnumerator Together(IEnumerable<IEnumerator> routines)
        {
            int running = 0;
            foreach (var r in routines) { running++; StartCoroutine(Track(r, () => running--)); }
            while (running > 0) yield return null;
        }
        IEnumerator Track(IEnumerator routine, Action done) { yield return routine; done(); }

        // ---- held poses ----
        public void WindupPose(BattlePawnView v, CreatureAttack attack, BattleCreature c, bool snap = false)
        {
            var glow = AccentOf(c, Ochre);
            switch (attack)
            {
                case CreatureAttack.Shove: v.SetPose(-9, .95f, 0, .4f, 5, 0, glow, snap); break;          // plants the door, leans back
                case CreatureAttack.Listen: v.SetPose(20, 1, 0, .8f, 5, 0, glow, snap); break;            // head tipped toward the sound
                case CreatureAttack.Pounce: v.SetPose(5, .8f, -.02f, 1.2f, 22, 0, glow, snap); break;     // legs drawn in, shivering
                case CreatureAttack.Charge: v.SetPose(11, .88f, 0, 1, 26, 0, glow, snap); break;          // nose down, restless
                case CreatureAttack.Veil: v.SetPose(0, .88f, .12f, 2.4f, 3, 0, glow, snap); break;        // spread wide, billowing
                case CreatureAttack.Wire: v.SetPose(-6, 1, 0, .5f, 30, .35f, glow, snap); break;          // cable raised, humming
                case CreatureAttack.Grab: v.SetPose(0, .84f, -.03f, .7f, 4, 0, glow, snap); break;        // spreading flat
                case CreatureAttack.Collapse: v.SetPose(12, 1, 0, 1, 13, 0, glow, snap); break;           // creaking over
                case CreatureAttack.Twin: v.SetPose(0, 1, 0, 6, 2.2f, 0, glow, snap); break;              // heads pulling apart
                case CreatureAttack.Broadcast: v.SetPose(0, 1, 0, 2.2f, 34, .3f, glow, snap); break;      // antennae buzzing
                case CreatureAttack.Ram: v.SetPose(-8, .97f, 0, 1.4f, 36, 0, glow, snap); break;          // wheels rattling
            }
        }
        public void StaggerPose(BattlePawnView v, CreatureAttack attack, bool snap = false)
        {
            switch (attack)
            {
                case CreatureAttack.Collapse: v.SetPose(24, .97f, -.02f, 0, 18, 0, Color.white, snap); break;
                case CreatureAttack.Ram: v.SetPose(-7, .95f, 0, .6f, 7, 0, Color.white, snap); break;
                case CreatureAttack.Charge: v.SetPose(9, .9f, 0, .5f, 9, 0, Color.white, snap); break;
                default: v.SetPose(14, .95f, 0, 0, 18, 0, Color.white, snap); break;
            }
        }

        IEnumerator Windup(BattleEvent e)
        {
            var v = panel.PawnView(e.Actor); if (!v) yield break; var c = CreatureOf(e.Actor);
            panel.RevealOutcome();
            CreatureSound(c, WindupOf(c), Growl, .8f);
            Text(c != null ? c.WindupName : "준비", AccentOf(c, Danger), panel.FxPoint(v.Head), .9f);
            WindupPose(v, e.Attack, c);
            if (e.Cells.Count > 0) panel.BlinkCells(e.Cells);
            yield return Wait(WindupHold);
        }
        IEnumerator Recover(BattleEvent e)
        {
            var v = panel.PawnView(e.Actor); if (!v) yield break;
            Sound(Step, .6f, .8f); Text("자세 회복", Muted, panel.FxPoint(v.Head), .8f); v.ClearPose();
            yield return Wait(.35f);
        }
        IEnumerator CreatureStrike(BattleEvent e)
        {
            var a = panel.PawnView(e.Actor); if (!a) yield break; var c = CreatureOf(e.Actor);
            a.ClearPose();
            switch (e.Attack)
            {
                case CreatureAttack.Shove: yield return Shove(e, a, c); break;
                case CreatureAttack.Listen: yield return Slam(e, a, c); break;
                case CreatureAttack.Pounce: yield return Pounce(e, a, c); break;
                case CreatureAttack.Charge: yield return Charge(e, a, c); break;
                case CreatureAttack.Veil: yield return Veil(e, a, c); break;
                case CreatureAttack.Wire: yield return Zap(e, a, c); break;
                case CreatureAttack.Swarm: yield return Flurry(e, a, c); break;
                case CreatureAttack.Grab: yield return Grab(e, a, c); break;
                case CreatureAttack.Collapse: yield return Collapse(e, a, c); break;
                case CreatureAttack.Twin: yield return Twin(e, a, c); break;
                case CreatureAttack.Broadcast: yield return Broadcast(e, a, c); break;
                case CreatureAttack.Ram: yield return Ram(e, a, c); break;
            }
            if (e.Staggered) { StaggerPose(a, e.Attack); Text("빈틈", Ochre, panel.FxPoint(a.Head), .95f); }
        }

        // ---- one ally reached by a creature ----
        void React(BattleHit h, Vector3 dir, BattleCreature c, float weight, float slashAngle = float.NaN, Color? tint = null, bool wobble = true)
        {
            var t = panel.PawnView(h.Target); if (!t) return;
            var chest = panel.FxPoint(t.Chest); var above = chest + Vector2.up * 48; const float away = 1;
            if (!h.Hit)
            {
                if (h.Braced) { Sound(Block); Text("막음", Guarded, above, 1); StartCoroutine(ShieldPop(t)); StartCoroutine(Wobble(t, away, HitTilt * .45f, dir, KnockDistance * .4f)); }
                else { Sound(Miss, .9f); Text("빗나감", Muted, above, .9f); StartCoroutine(Dodge(t, dir, away)); }
                return;
            }
            if (h.Damage <= 0)
            {
                if (h.Braced) { Sound(Block); Text("버팀", Guarded, above, .95f); StartCoroutine(ShieldPop(t)); }
                else Sound(Swing, .5f, .8f);
                if (h.Veiled) Text("시야 가림", VeilColor, chest + Vector2.down * 26, .85f);
                if (wobble) StartCoroutine(Wobble(t, away, HitTilt * .5f, dir, KnockDistance * .4f));
                return;
            }
            CreatureSound(c, ImpactOf(c), Bite, 1);
            if (!float.IsNaN(slashAngle)) StartCoroutine(SlashRoutine(chest, Danger, slashAngle, weight));
            StartCoroutine(BurstRoutine(chest, tint ?? Danger, weight, .28f));
            StartCoroutine(FlashRoutine(t, weight > 1.25f, tint));
            Text("-" + h.Damage, Danger, above, 1.05f + .15f * weight);
            if (h.Braced) { Text("방어로 -" + panel.State.Rules.GuardStrikeReduction, Guarded, chest + Vector2.down * 26, .75f); StartCoroutine(ShieldPop(t)); }
            else if (h.Bound && !h.Killed) Text("묶임", AccentOf(c, Muted), chest + Vector2.down * 26, .85f);
            else if (h.Veiled && !h.Killed) Text("시야 가림", VeilColor, chest + Vector2.down * 26, .85f);
            panel.ShowHealth(h.Target, h.Health, true);
            StartCoroutine(EdgeRoutine());
            Shake(HitShake * weight);
            if (h.Killed) t.Angle = away * 10;
            else if (wobble) StartCoroutine(Wobble(t, away, HitTilt * weight, dir, KnockDistance * weight));
        }
        // After the blow: slide pushed allies, knock the ones that hit a wall or a friend, topple the fallen.
        IEnumerator Settle(BattleEvent e, Vector3 dir)
        {
            var slides = new List<IEnumerator>();
            foreach (var h in e.Hits.Where(h => h.Pushed))
            {
                var t = panel.PawnView(h.Target); if (!t) continue;
                Text("밀려남", Cream, panel.FxPoint(t.Head), .8f); slides.Add(Hop(h.Target, h.ToDepth, h.ToLane, PushDuration, .05f, 0));
            }
            if (slides.Count > 0) yield return Together(slides);
            bool knocked = false;
            foreach (var h in e.Hits.Where(h => h.Collided))
            {
                var t = panel.PawnView(h.Target); if (!t) continue; knocked = true;
                Text("부딪힘", Danger, panel.FxPoint(t.Head), .85f); StartCoroutine(Wobble(t, -1, HitTilt * .8f, -dir, KnockDistance * .6f));
                StartCoroutine(BurstRoutine(panel.FxPoint(t.transform.position) + Vector2.up * 20, Muted, .8f, .25f));
            }
            if (knocked) { Sound(Collide ? Collide : Impact, .9f, .85f); Shake(HitShake); yield return Freeze(HitStop); }
            var falls = e.Hits.Where(h => h.Killed && panel.PawnView(h.Target)).Select(h => Topple(h.Target, panel.PawnView(h.Target), dir)).ToList();
            if (falls.Count > 0) yield return Together(falls); else yield return Wait(.08f);
        }
        void Whiff(Vector3 local, BattleCreature c)
        {
            Sound(Miss, .8f, .7f); var fx = panel.FxPoint(panel.LocalToWorld(local));
            Text("빗나감", Muted, fx + Vector2.up * 80, .95f); StartCoroutine(BurstRoutine(fx, Muted, 1.1f, .3f)); Shake(ShotShake);
        }
        IEnumerator Return(BattlePawnView a, float seconds)
        {
            var held = a.Offset; float angle = a.Angle, lift = a.Lift, squash = a.Squash;
            yield return Tween(seconds, u => { float k = Cubic(u); a.Offset = held * (1 - k); a.Angle = angle * (1 - k); a.Lift = lift * (1 - k); a.Squash = Mathf.Lerp(squash, 1, k); });
            a.Offset = Vector3.zero; a.Angle = 0; a.Lift = 0; a.Squash = 1;
        }

        // ---- 문지기: slow heavy shove down its lane; the victim slides back, or slams into whatever is behind ----
        IEnumerator Shove(BattleEvent e, BattlePawnView a, BattleCreature c)
        {
            var first = e.Hits.FirstOrDefault(); var t = first != null ? panel.PawnView(first.Target) : null;
            int lane = e.Cells.Count > 0 ? e.Cells[0] / 3 : a.Lane;
            var aim = t ? t.Home + t.Offset : CellLocal(FieldBattleState.CellOf(0, lane));
            var dir = aim - (a.Home + a.Offset); var n = dir.normalized;
            CreatureSound(c, WindupOf(c), Swing, .55f, .85f);
            yield return Tween(.24f, u => { float k = Out(u); a.Angle = -14 * k; a.Squash = Mathf.Lerp(1, .92f, k); a.Offset = -n * .14f * k; });
            var lunge = n * Mathf.Min(dir.magnitude * .58f, 2.4f);
            CreatureSound(c, StrikeOf(c), Swing, .8f, .7f);
            yield return Tween(.12f, u => { float k = u * u; a.Offset = Vector3.Lerp(-n * .14f, lunge, k); a.Angle = Mathf.Lerp(-14, 16, k); a.Squash = Mathf.Lerp(.92f, 1.04f, k); });
            panel.RevealOutcome();
            if (first == null) Whiff(aim, c);
            else { React(first, dir, c, 1.45f); Shake(CriticalShake * .85f); yield return Freeze(HitStop * 1.4f); }
            yield return Settle(e, dir);
            yield return Return(a, CreatureRecover);
        }
        // ---- 귀기울임: rises tall, then drops a long arm onto the marked cell ----
        IEnumerator Slam(BattleEvent e, BattlePawnView a, BattleCreature c)
        {
            int cell = e.Cells.Count > 0 ? e.Cells[0] : FieldBattleState.CellOf(0, a.Lane);
            var first = e.Hits.FirstOrDefault(); var floor = CellLocal(cell); var dir = floor - (a.Home + a.Offset);
            Sound(Swing, .6f, .7f);
            StartCoroutine(SoundAfter(.17f, c, StrikeOf(c), Impact));
            yield return Tween(.26f, u => { float k = Out(u); a.Lift = .5f * k; a.Angle = -10 * k; a.Squash = Mathf.Lerp(1, 1.06f, k); });
            var reach = dir * .7f;
            yield return Tween(.09f, u => { float k = u * u * u; a.Offset = reach * k; a.Lift = .5f * (1 - k); a.Angle = Mathf.Lerp(-10, 26, k); a.Squash = Mathf.Lerp(1.06f, .96f, k); });
            panel.RevealOutcome();
            var fx = CellFx(cell);
            StartCoroutine(SlashRoutine(fx + Vector2.up * 90, AccentOf(c, Danger), 90, 1.6f));
            StartCoroutine(BurstRoutine(fx, Muted, 1.4f, .4f));
            if (first != null) { React(first, dir, c, 1.5f); Shake(CriticalShake * .8f); yield return Freeze(CriticalHitStop * .8f); }
            else { Text("빗나감", Muted, fx + Vector2.up * 80, .95f); Shake(HitShake); yield return Freeze(HitStop * .6f); }
            yield return Settle(e, dir);
            yield return Return(a, CreatureRecover * 1.2f);
        }
        // ---- 식탁밑: crouches, then a short high leap that lands on the marked cell ----
        IEnumerator Pounce(BattleEvent e, BattlePawnView a, BattleCreature c)
        {
            int cell = e.Cells.Count > 0 ? e.Cells[0] : FieldBattleState.CellOf(0, a.Lane);
            var first = e.Hits.FirstOrDefault(); var floor = CellLocal(cell);
            var dir = floor - (a.Home + a.Offset); var land = dir - dir.normalized * .45f;
            yield return Tween(.1f, u => { a.Squash = Mathf.Lerp(1, .74f, Out(u)); a.Angle = 6 * Out(u); });
            CreatureSound(c, StrikeOf(c), Swing, .8f, 1.2f);
            yield return Tween(.3f, u => { a.Offset = land * InOut(u); a.Lift = Mathf.Sin(u * Mathf.PI) * 1.05f; a.Angle = Mathf.Lerp(6, 20, u); a.Squash = Mathf.Lerp(.74f, 1.06f, Out(u)); });
            a.Lift = 0; panel.RevealOutcome();
            CreatureSound(c, ImpactOf(c), Impact, 1);
            StartCoroutine(Squash(a, .76f)); StartCoroutine(BurstRoutine(CellFx(cell), Muted, 1.1f, .3f));
            if (first != null) { React(first, dir, c, 1.3f, -30); Shake(HitShake * 1.3f); yield return Freeze(HitStop * 1.2f); }
            else { Text("빗나감", Muted, CellFx(cell) + Vector2.up * 80, .95f); Shake(ShotShake); yield return Wait(.1f); }
            yield return Settle(e, dir);
            // Hops back only as far as its own front row (the rules moved it there).
            float angle = a.Angle;
            yield return Tween(.1f, u => a.Angle = angle * (1 - u));
            yield return Hop(e.Actor, e.ToDepth, e.ToLane, .28f, .45f, 0);
        }
        // ---- 틈새개: a very fast low dash down the lane; the first body in the way is knocked back ----
        IEnumerator Charge(BattleEvent e, BattlePawnView a, BattleCreature c)
        {
            int lane = e.Cells.Count > 0 ? e.Cells[0] / 3 : a.Lane;
            var first = e.Hits.FirstOrDefault(); var t = first != null ? panel.PawnView(first.Target) : null;
            var aim = t ? t.Home + t.Offset : CellLocal(FieldBattleState.CellOf(2, lane));
            var dir = aim - (a.Home + a.Offset); var n = dir.normalized;
            var stop = t ? dir - n * .5f : dir + n * .3f;
            CreatureSound(c, WindupOf(c), Growl, .5f, 1.3f);
            yield return Tween(.12f, u => { float k = Out(u); a.Angle = 16 * k; a.Squash = Mathf.Lerp(1, .86f, k); a.Offset = -n * .1f * k; });
            CreatureSound(c, StrikeOf(c), Swing, 1);
            StartCoroutine(TracerRoutine(panel.FxPoint(a.Chest), panel.FxPoint(panel.LocalToWorld(a.Home + stop) + Vector3.up * a.Height * .4f), AccentOf(c, Cream), .24f));
            yield return Tween(.11f, u => { float k = u * u; a.Offset = Vector3.Lerp(-n * .1f, stop, k); a.Squash = Mathf.Lerp(.86f, 1.03f, k); });
            panel.RevealOutcome();
            if (first != null) { React(first, dir, c, 1.4f, -24); Shake(CriticalShake * .8f); yield return Freeze(HitStop * 1.3f); }
            else { Whiff(aim, c); var over = a.Offset; yield return Tween(.14f, u => a.Offset = Vector3.Lerp(over, over + n * .25f, Out(u))); }
            yield return Settle(e, dir);
            float angle = a.Angle, squash = a.Squash;
            yield return Tween(.12f, u => { a.Angle = angle * (1 - u); a.Squash = Mathf.Lerp(squash, 1, u); });
            StaggerPose(a, e.Attack);
            yield return Hop(e.Actor, e.ToDepth, e.ToLane, .28f, .1f, 0);
        }
        // ---- 널린것: spreads wide and sweeps a sheet down the whole lane; the first takes a lash, all lose their sight ----
        IEnumerator Veil(BattleEvent e, BattlePawnView a, BattleCreature c)
        {
            int lane = e.Cells.Count > 0 ? e.Cells[0] / 3 : a.Lane;
            var mid = CellLocal(FieldBattleState.CellOf(1, lane)); var dir = mid - (a.Home + a.Offset);
            yield return Tween(.2f, u => { float k = Out(u); a.Squash = Mathf.Lerp(1, .82f, k); a.Lift = .14f * k; a.Angle = -6 * k; });
            CreatureSound(c, StrikeOf(c), Swing, .9f);
            StartCoroutine(SlashRoutine(CellFx(FieldBattleState.CellOf(1, lane)) + Vector2.up * 70, AccentOf(c, VeilColor), -6, 2.6f));
            yield return Tween(.14f, u => { float k = u * u; a.Offset = dir * .3f * k; a.Angle = Mathf.Lerp(-6, 12, k); a.Squash = Mathf.Lerp(.82f, 1.04f, k); });
            panel.RevealOutcome();
            foreach (var h in e.Hits) { React(h, dir, c, h.Damage > 0 ? 1 : .6f, float.NaN, VeilColor); yield return Wait(.06f); }
            if (e.Hits.Count == 0) Whiff(mid, c);
            yield return Freeze(HitStop * .6f);
            yield return Settle(e, dir);
            yield return Return(a, CreatureRecover * 1.2f);
        }
        // ---- 계량원: a crackling discharge down the cable into both marked cells ----
        IEnumerator Zap(BattleEvent e, BattlePawnView a, BattleCreature c)
        {
            var dir = e.Cells.Count > 0 ? CellLocal(e.Cells[0]) - a.Home : Vector3.left;
            yield return Tween(.12f, u => a.Angle = -8 * Out(u));
            CreatureSound(c, StrikeOf(c), Gunshot, .9f);
            var from = panel.FxPoint(a.Chest); var color = AccentOf(c, ShockColor);
            for (int flick = 0; flick < 3; flick++)
            {
                foreach (int cell in e.Cells)
                {
                    var to = CellFx(cell) + new Vector2(Jitter(-20, 20), Jitter(20, 90));
                    StartCoroutine(TracerRoutine(from + new Vector2(Jitter(-8, 8), Jitter(-8, 8)), to, color, .07f));
                    if (flick == 0) StartCoroutine(BurstRoutine(CellFx(cell) + Vector2.up * 40, color, 1.1f, .3f));
                }
                if (flick == 0)
                {
                    panel.RevealOutcome(); a.Angle = 6;
                    foreach (var h in e.Hits) React(h, dir, c, 1.2f, float.NaN, ShockColor, false);
                    if (e.Hits.Count == 0 && e.Cells.Count > 0) Text("빗나감", Muted, CellFx(e.Cells[0]) + Vector2.up * 80, .95f);
                }
                Shake(ShotShake);
                yield return Wait(.06f);
            }
            foreach (var h in e.Hits) { var t = panel.PawnView(h.Target); if (t && !h.Killed && h.Damage > 0) StartCoroutine(Twitch(t)); }
            yield return Freeze(HitStop);
            yield return Settle(e, dir);
            yield return Return(a, CreatureRecover);
        }
        IEnumerator Twitch(BattlePawnView v) { yield return Tween(.36f, u => v.Sway = Mathf.Sin(u * 90) * 4 * (1 - u)); v.Sway = 0; }
        // ---- 먼지둥지: three quick pecks of the swarm over the target and whoever stands beside it ----
        IEnumerator Flurry(BattleEvent e, BattlePawnView a, BattleCreature c)
        {
            var first = e.Hits.FirstOrDefault(); var t = first != null ? panel.PawnView(first.Target) : null; if (!t) yield break;
            var dir = (t.Home + t.Offset) - (a.Home + a.Offset);
            CreatureSound(c, StrikeOf(c), Swing, .85f);
            for (int k = 0; k < 3; k++)
            {
                var from = a.Offset; var jab = dir * (.36f + .06f * k) + new Vector3(0, Jitter(-.12f, .12f), 0);
                yield return Tween(.07f, u => { a.Offset = Vector3.Lerp(from, jab, Out(u)); a.Angle = 8 + 4 * k; });
                foreach (var h in e.Hits)
                {
                    var v = panel.PawnView(h.Target); if (!v) continue;
                    StartCoroutine(BurstRoutine(panel.FxPoint(v.Chest) + new Vector2(Jitter(-40, 40), Jitter(-50, 50)), AccentOf(c, Muted), .45f, .2f));
                    v.Sway = Jitter(-4, 4);
                }
                if (k == 0) panel.RevealOutcome();
                yield return Tween(.06f, u => a.Offset = Vector3.Lerp(jab, jab * .5f, u));
            }
            foreach (var h in e.Hits) React(h, dir, c, .9f);
            Shake(HitShake * .7f);
            yield return Freeze(HitStop * .7f);
            yield return Settle(e, dir);
            yield return Return(a, CreatureRecover);
        }
        // ---- 고인사람: the puddle seeps under the marked cell and drags the ally down by the ankles ----
        IEnumerator Grab(BattleEvent e, BattlePawnView a, BattleCreature c)
        {
            int cell = e.Cells.Count > 0 ? e.Cells[0] : FieldBattleState.CellOf(0, a.Lane);
            var first = e.Hits.FirstOrDefault(); var floor = CellLocal(cell); var dir = floor - (a.Home + a.Offset);
            yield return Tween(.2f, u => { float k = Out(u); a.Angle = 12 * k; a.Squash = Mathf.Lerp(1, .8f, k); a.Offset = dir * .22f * k; });
            CreatureSound(c, StrikeOf(c), Bite, .9f);
            panel.RevealOutcome();
            var fx = CellFx(cell);
            StartCoroutine(BurstRoutine(fx, AccentOf(c, Muted), 1.2f, .45f));
            StartCoroutine(RingAfter(.14f, fx, AccentOf(c, Muted), 1.7f));
            if (first != null)
            {
                React(first, dir, c, 1.1f);
                var t = panel.PawnView(first.Target);
                if (t && !first.Killed) yield return Tween(.22f, u => t.Lift = -.1f * Out(u) * (1 - u));
                yield return Freeze(HitStop);
            }
            else { Text("빗나감", Muted, fx + Vector2.up * 80, .95f); yield return Wait(.12f); }
            yield return Settle(e, dir);
            yield return Return(a, CreatureRecover * 1.3f);
        }
        IEnumerator RingAfter(float seconds, Vector2 at, Color color, float size) { yield return Wait(seconds); yield return BurstRoutine(at, color, size, .5f); }
        // ---- 계단등: creaks over, then crashes onto the two marked front cells and stays down for a turn ----
        IEnumerator Collapse(BattleEvent e, BattlePawnView a, BattleCreature c)
        {
            var cells = e.Cells.Count > 0 ? e.Cells : new List<int> { FieldBattleState.CellOf(0, a.Lane) };
            var mid = cells.Aggregate(Vector3.zero, (s, x) => s + CellLocal(x)) / cells.Count;
            var dir = mid - (a.Home + a.Offset);
            CreatureSound(c, WindupOf(c), Step, .6f, .7f);
            yield return Tween(.24f, u => a.Angle = 10 * Out(u));
            CreatureSound(c, StrikeOf(c), Fall, 1);
            yield return Tween(.14f, u => { float k = u * u; a.Angle = Mathf.Lerp(10, 32, k); a.Offset = dir.normalized * .5f * k; a.Lift = -.05f * k; });
            panel.RevealOutcome(); Shake(CriticalShake * .9f);
            foreach (int cell in cells) StartCoroutine(BurstRoutine(CellFx(cell), Muted, 1.4f, .45f));
            foreach (var h in e.Hits) React(h, dir, c, 1.35f);
            if (e.Hits.Count == 0) Text("빗나감", Muted, CellFx(cells[0]) + Vector2.up * 80, .95f);
            yield return Freeze(HitStop * 1.4f);
            yield return Settle(e, dir);
            StaggerPose(a, e.Attack);
            yield return Return(a, .3f);
        }
        // ---- 겹친이웃: one head strikes the first cell, the other the cell you would step into ----
        IEnumerator Twin(BattleEvent e, BattlePawnView a, BattleCreature c)
        {
            var dir = Vector3.left;
            for (int k = 0; k < e.Cells.Count; k++)
            {
                int cell = e.Cells[k]; float sign = k == 0 ? 1 : -1;
                var h = e.Hits.FirstOrDefault(x => FieldBattleState.CellOf(x.FromDepth, x.FromLane) == cell);
                var floor = CellLocal(cell); dir = floor - a.Home; var n = dir.normalized;
                CreatureSound(c, StrikeOf(c), Swing, .8f, k == 0 ? .9f : 1.12f);
                var start = a.Offset; float angle0 = a.Angle;
                yield return Tween(.09f, u => { float q = Out(u); a.Angle = Mathf.Lerp(angle0, -8 * sign, q); a.Offset = Vector3.Lerp(start, -n * .08f, q); });
                var lunge = n * Mathf.Min(dir.magnitude * .5f, 1.6f) + Vector3.up * .1f * sign;
                yield return Tween(.08f, u => { float q = u * u; a.Offset = Vector3.Lerp(-n * .08f, lunge, q); a.Angle = Mathf.Lerp(-8 * sign, 14 * sign, q); });
                if (k == 0) panel.RevealOutcome();
                if (h != null) { React(h, dir, c, 1.05f, 40 * sign); yield return Freeze(HitStop); }
                else { Whiff(floor, c); yield return Wait(.05f); }
                var held = a.Offset; float heldAngle = a.Angle;
                yield return Tween(.1f, u => { float q = Cubic(u); a.Offset = held * (1 - q * .6f); a.Angle = heldAngle * (1 - q); });
            }
            yield return Settle(e, dir);
            yield return Return(a, CreatureRecover);
        }
        // ---- 수신목: the antennae buzz and a broadcast rolls out; the noise gauge takes it ----
        IEnumerator Broadcast(BattleEvent e, BattlePawnView a, BattleCreature c)
        {
            CreatureSound(c, StrikeOf(c), Growl, .9f);
            var head = panel.FxPoint(a.Head) + Vector2.down * 30; var color = AccentOf(c, Muted);
            StartCoroutine(Tween(.5f, u => a.Sway = Mathf.Sin(u * 70) * 4 * (1 - u)));
            for (int i = 0; i < 3; i++) { StartCoroutine(BurstRoutine(head, color, 1 + i * .6f, .55f)); yield return Wait(.12f); }
            panel.RevealOutcome();
            if (panel.NoiseGauge) Text("소음 +" + e.Noise, Ochre, panel.FxPoint(panel.NoiseGauge) + Vector2.down * 6, .9f);
            yield return Wait(.3f); a.Sway = 0;
        }
        // ---- 빈수레: rattles down the lane, bowling everyone in it back; stops dead against the far end ----
        IEnumerator Ram(BattleEvent e, BattlePawnView a, BattleCreature c)
        {
            int lane = e.Cells.Count > 0 ? e.Cells[0] / 3 : a.Lane;
            var end = CellLocal(FieldBattleState.CellOf(2, lane)); var dir = end - a.Home; var n = dir.normalized;
            CreatureSound(c, WindupOf(c), Step, .7f);
            yield return Tween(.16f, u => { float k = Out(u); a.Angle = -10 * k; a.Offset = -n * .12f * k; });
            CreatureSound(c, StrikeOf(c), Step, .9f);
            foreach (var h in e.Hits.OrderBy(x => x.FromDepth))
            {
                var point = CellLocal(FieldBattleState.CellOf(h.FromDepth, h.FromLane)) - n * .55f - a.Home;
                yield return Roll(a, point, .12f);
                if (h == e.Hits.OrderBy(x => x.FromDepth).First()) panel.RevealOutcome();
                React(h, dir, c, 1.2f);
                yield return Freeze(HitStop);
            }
            if (e.Hits.Count == 0) { yield return Roll(a, end - a.Home, .22f); panel.RevealOutcome(); Whiff(end, c); }
            float lean = a.Angle;
            yield return Tween(.1f, u => a.Angle = Mathf.Lerp(lean, 16, Out(u)));
            Shake(HitShake);
            yield return Settle(e, dir);
            float angle = a.Angle;
            yield return Tween(.14f, u => { a.Angle = angle * (1 - u); a.Lift = 0; });
            StaggerPose(a, e.Attack);
            yield return Hop(e.Actor, e.ToDepth, e.ToLane, .3f, .06f, 0);
        }
        IEnumerator Roll(BattlePawnView a, Vector3 offset, float seconds)
        {
            var from = a.Offset; float angle = a.Angle;
            yield return Tween(seconds, u => { a.Offset = Vector3.Lerp(from, offset, u); a.Lift = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 3)) * .04f; a.Angle = Mathf.Lerp(angle, 6, u); });
            a.Lift = 0;
        }
    }
}
