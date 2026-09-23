using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Demo5.FrontEnd
{
    // Replays battle events as short board-game feedback: lean, strike, hit-stop, flash, wobble, numbers, sound.
    // Standees only move, tilt and tint (no frame animation). Every timing and strength here is an Inspector knob.
    public sealed partial class BattlePresentation : MonoBehaviour
    {
        [Header("전체 속도 · 검증 때만 올린다")]
        [Min(.1f)] public float Speed = 1;
        [Header("이동")]
        public float MoveDuration = .24f;
        public float MoveHop = .22f, AdvanceDuration = .36f, AdvanceHop = .06f, AdvanceLean = 7, PushDuration = .2f;
        [Header("공격 동작")]
        public float WindUp = .13f;
        public float LungeDuration = .09f, RecoverDuration = .2f, MaxLunge = 1.5f, RecoilDistance = .22f;
        [Range(0, 1)] public float LungeReach = .5f;
        [Header("타격")]
        public float HitStop = .075f;
        public float CriticalHitStop = .16f, HitTilt = 13, CriticalTilt = 24, KnockDistance = .2f, FlashDuration = .07f, TintDuration = .3f;
        [Header("화면 흔들림 · 작게 유지")]
        public float ShotShake = .025f;
        public float HitShake = .035f, CriticalShake = .07f, KillShake = .05f, ShakeDuration = .16f;
        [Header("쓰러짐")]
        public float ToppleDuration = .34f;
        public float FadeDuration = .45f;
        [Header("글자와 효과")]
        public BattleFloatingText FloatingTextPrefab;
        public BattleBurstGraphic BurstPrefab;
        public BattleSlashGraphic SlashPrefab;
        public BattleTracerGraphic TracerPrefab;
        public BattleShieldIcon ShieldPrefab;
        public BattleEdgeFlash EdgeFlash;
        public Material FlashMaterial;
        public float FloatRise = 74, FloatDuration = .9f;
        [Range(0, 1)] public float EdgeAlpha = .28f;
        [Header("색")]
        public Color Cream = new Color(.97f, .93f, .82f);
        public Color Ochre = new Color(1, .78f, .38f), Danger = new Color(.86f, .42f, .33f), Guarded = new Color(.62f, .8f, .66f), Muted = new Color(.74f, .74f, .7f);
        [Header("효과음 · 교체 가능")]
        public AudioSource Audio;
        public AudioClip Swing, Impact, Critical, Gunshot, Miss, Bite, Block, Fall, Step, Turn, Growl, Heal;
        public BattleCrossGraphic CrossPrefab;
        [Range(0, 1)] public float Volume = .85f;
        [Range(0, .2f)] public float PitchJitter = .06f;

        public Vector3 ShakeOffset { get; private set; }
        ExpeditionBattlePanel panel;
        readonly List<GameObject> spawned = new List<GameObject>();
        readonly List<AudioSource> voices = new List<AudioSource>();
        int voice;
        float frozenUntil, shakeStart, shakeEnd, shakeStrength;
        // Cosmetic jitter has its own generator so it never shifts the hit rolls.
        readonly System.Random cosmetic = new System.Random(4907);
        float Jitter(float min, float max) => min + (float)cosmetic.NextDouble() * (max - min);

        public void Bind(ExpeditionBattlePanel owner)
        {
            panel = owner;
            if (!Audio) Audio = GetComponent<AudioSource>();
            if (voices.Count == 0 && Audio)
            {
                voices.Add(Audio);
                for (int i = 0; i < 3; i++)
                {
                    var extra = gameObject.AddComponent<AudioSource>(); extra.playOnAwake = false; extra.spatialBlend = 0;
                    extra.outputAudioMixerGroup = Audio.outputAudioMixerGroup; voices.Add(extra);
                }
            }
        }
        public void Clear()
        {
            StopAllCoroutines();
            foreach (var g in spawned) if (g) Destroy(g);
            spawned.Clear(); ShakeOffset = Vector3.zero; frozenUntil = shakeEnd = 0;
            if (EdgeFlash) EdgeFlash.color = Color.clear;
        }

        public IEnumerator Play(IList<BattleEvent> events) { foreach (var e in events) yield return PlayEvent(e); }
        IEnumerator PlayEvent(BattleEvent e)
        {
            switch (e.Kind)
            {
                case BattleEventKind.Move: yield return Hop(e.Actor, e.ToDepth, e.ToLane, MoveDuration, MoveHop, 0); break;
                case BattleEventKind.Advance:
                case BattleEventKind.Shift: yield return Hop(e.Actor, e.ToDepth, e.ToLane, AdvanceDuration, AdvanceHop, AdvanceLean); break;
                case BattleEventKind.Attack: yield return e.Ranged ? Shot(e) : Strike(e); break;
                case BattleEventKind.Guard: yield return Brace(e.Actor); break;
                case BattleEventKind.Wait: yield return Lurk(e.Actor, e.Resting); break;
                case BattleEventKind.Windup: yield return Windup(e); break;
                case BattleEventKind.Strike: yield return CreatureStrike(e); break;
                case BattleEventKind.Recover: yield return Recover(e); break;
                case BattleEventKind.Alarm: yield return Alarm(); break;
                case BattleEventKind.Reinforce: yield return Arrive(e); break;
                case BattleEventKind.Item: yield return Treat(e); break;
            }
        }

        // Phase change sting: a light paper flick for the party, a low growl as the other side takes over.
        public void PhaseSound(bool enemy) { if (enemy) Sound(Growl, .55f, .85f); else Sound(Turn, .7f, .9f); }
        public void TurnStart(int unit)
        {
            var v = panel ? panel.PawnView(unit) : null; if (!v || v.Enemy) return;
            Sound(Turn, .55f); StartCoroutine(TurnPulse(v));
        }
        IEnumerator TurnPulse(BattlePawnView v) { yield return Tween(.34f, u => v.BasePulse = Mathf.Sin(u * Mathf.PI) * .16f); v.BasePulse = 0; }
        public void Select(int unit)
        {
            var v = panel ? panel.PawnView(unit) : null; if (!v) return;
            Sound(Turn, .3f, 1.35f); StartCoroutine(TurnPulse(v));
        }

        // ---- movement ----
        IEnumerator Hop(int unit, int depth, int lane, float seconds, float height, float lean)
        {
            var v = panel.PawnView(unit); if (!v) yield break;
            var from = v.Home + v.Offset; v.Depth = depth; v.Lane = lane;
            v.Home = panel.CellHome(v.Enemy, depth, lane); v.Offset = from - v.Home; var start = v.Offset;
            v.SetSorting(lane); float forward = v.Enemy ? 1 : -1;
            yield return Tween(seconds, u =>
            {
                float k = InOut(u); v.Offset = start * (1 - k); v.Lift = Mathf.Sin(u * Mathf.PI) * height;
                v.Angle = forward * lean * Mathf.Sin(u * Mathf.PI);
            });
            v.Offset = Vector3.zero; v.Lift = 0; v.Angle = 0;
            Sound(Step, .8f, v.Enemy ? .75f : 1);
            StartCoroutine(Squash(v, .88f));
        }
        IEnumerator Squash(BattlePawnView v, float amount) { yield return Tween(.18f, u => v.Squash = Mathf.LerpUnclamped(amount, 1, OutBack(u))); v.Squash = 1; }

        // ---- attacks ----
        IEnumerator Strike(BattleEvent e)
        {
            var a = panel.PawnView(e.Actor); var t = panel.PawnView(e.Target); if (!a || !t) yield break;
            float forward = a.Enemy ? 1 : -1;
            var dir = (t.Home + t.Offset) - (a.Home + a.Offset); var back = -dir.normalized * .08f;
            var lunge = dir.normalized * Mathf.Min(dir.magnitude * LungeReach, MaxLunge) * (e.Retreating ? .6f : 1);
            // Anticipation: lean back and rise, like lifting a game piece before the tap.
            yield return Tween(a.Enemy ? WindUp * 1.35f : WindUp, u => { float k = Out(u); a.Angle = -forward * 9 * k; a.Lift = .07f * k; a.Offset = back * k; });
            Sound(Swing, .8f, a.Enemy ? .78f : 1);
            yield return Tween(LungeDuration, u => { float k = u * u; a.Offset = Vector3.Lerp(back, lunge, k); a.Angle = Mathf.Lerp(-forward * 9, forward * 12, k); a.Lift = .07f * (1 - k); });
            yield return PlayImpact(e, a, t, dir);
            var held = a.Offset; float heldAngle = a.Angle;
            yield return Tween(RecoverDuration, u => { float k = Cubic(u); a.Offset = held * (1 - k); a.Angle = heldAngle * (1 - k); a.Lift = 0; });
            a.Offset = Vector3.zero; a.Angle = 0;
            yield return Aftermath(e, t, dir);
        }
        IEnumerator Shot(BattleEvent e)
        {
            var a = panel.PawnView(e.Actor); var t = panel.PawnView(e.Target); if (!a || !t) yield break;
            float forward = a.Enemy ? 1 : -1;
            var dir = (t.Home + t.Offset) - (a.Home + a.Offset);
            yield return Tween(WindUp, u => { float k = Out(u); a.Angle = -forward * 4 * k; a.Lift = .05f * k; });
            var muzzle = panel.FxPoint(a.Chest + dir.normalized * .42f + Vector3.up * .12f); var chest = panel.FxPoint(t.Chest); var end = chest;
            if (!e.Hit) { var d = (chest - muzzle).normalized; end = chest + new Vector2(-d.y, d.x) * (cosmetic.NextDouble() < .5 ? 64 : -54) + d * 140; }
            Sound(Gunshot); Shake(ShotShake);
            StartCoroutine(BurstRoutine(muzzle, Ochre, .75f, .12f)); StartCoroutine(TracerRoutine(muzzle, end));
            a.Offset = -dir.normalized * RecoilDistance; a.Angle = -forward * 11;
            yield return Wait(.035f);
            yield return PlayImpact(e, a, t, dir);
            var held = a.Offset; float heldAngle = a.Angle, heldLift = a.Lift;
            yield return Tween(RecoverDuration * 1.2f, u => { float k = Cubic(u); a.Offset = held * (1 - k); a.Angle = heldAngle * (1 - k); a.Lift = heldLift * (1 - k); });
            a.Offset = Vector3.zero; a.Angle = 0; a.Lift = 0;
            yield return Aftermath(e, t, dir);
        }
        IEnumerator PlayImpact(BattleEvent e, BattlePawnView a, BattlePawnView t, Vector3 dir)
        {
            var chest = panel.FxPoint(t.Chest); var above = chest + Vector2.up * 48; float away = t.Enemy ? -1 : 1;
            panel.RevealOutcome();
            if (e.Blocked)
            {
                Sound(Block); Text("막음", Guarded, above, 1); StartCoroutine(BurstRoutine(chest, Guarded, 1, .25f)); StartCoroutine(ShieldPop(t));
                StartCoroutine(Wobble(t, away, HitTilt * .45f, dir, KnockDistance * .4f)); yield return Freeze(HitStop * .6f); yield break;
            }
            if (!e.Hit)
            {
                Sound(Miss, .9f); Text("빗나감", Muted, above, .9f); StartCoroutine(Dodge(t, dir, away)); yield break;
            }
            if (e.Damage <= 0)
            {
                Sound(Impact, .5f); Text("버팀", Cream, above, .95f); StartCoroutine(Wobble(t, away, HitTilt * .6f, dir, KnockDistance * .5f)); yield break;
            }
            bool critical = e.Critical;
            Sound(critical ? Critical : a.Enemy ? Bite : Impact, 1);
            if (critical && a.Enemy == false) Sound(Impact, .6f, .8f);
            if (!e.Ranged) StartCoroutine(SlashRoutine(chest, a.Enemy ? Danger : Cream, a.Enemy ? -58 : 34, critical ? 1.35f : 1));
            StartCoroutine(BurstRoutine(chest, e.Ranged ? Ochre : a.Enemy ? Danger : Cream, critical ? 1.5f : 1, .28f));
            StartCoroutine(FlashRoutine(t, critical));
            Text((critical ? "급소 " : "") + "-" + e.Damage, a.Enemy ? Danger : critical ? Ochre : Cream, above, critical ? 1.45f : 1.12f);
            panel.ShowHealth(e.Target, e.Health, true);
            if (!t.Enemy) StartCoroutine(EdgeRoutine());
            Shake(critical ? CriticalShake : e.Killed ? KillShake : HitShake);
            if (e.Killed) t.Angle = away * 10;
            else StartCoroutine(Wobble(t, away, critical ? CriticalTilt : HitTilt, dir, KnockDistance * (critical ? 1.6f : 1)));
            yield return Freeze(critical ? CriticalHitStop : HitStop);
        }
        IEnumerator Aftermath(BattleEvent e, BattlePawnView t, Vector3 dir)
        {
            if (e.Pushed) { Text("밀려남", Cream, panel.FxPoint(t.Head), .85f); yield return Hop(e.Target, e.ToDepth, e.ToLane, PushDuration, .05f, 0); }
            if (e.Killed) yield return Topple(e.Target, t, dir);
            else yield return Wait(.08f);
        }
        IEnumerator Topple(int unit, BattlePawnView v, Vector3 dir)
        {
            panel.PawnDown(unit); float away = v.Enemy ? -1 : 1;
            // Below the damage number so the two words never stack.
            Text(v.Enemy ? "쓰러짐" : "행동 불능", v.Enemy ? Cream : Danger, panel.FxPoint(v.transform.position) + Vector2.up * 18, .9f);
            float start = v.Angle; v.Sway = 0; v.Shove = Vector3.zero; var drift = dir.normalized * .18f;
            yield return Tween(ToppleDuration, u => { float k = u * u; v.Angle = Mathf.Lerp(start, away * 86, k); v.Lift = -.04f * k; v.Offset = drift * k; });
            Sound(Fall); Shake(KillShake * .5f); StartCoroutine(BurstRoutine(panel.FxPoint(v.transform.position), Muted, .9f, .3f));
            yield return Tween(FadeDuration, u => v.Alpha = 1 - u);
            v.gameObject.SetActive(false);
        }
        IEnumerator Brace(int unit)
        {
            var v = panel.PawnView(unit); if (!v) yield break;
            Sound(Block, .75f, 1.12f); Text("방어", Guarded, panel.FxPoint(v.Head), .95f); StartCoroutine(ShieldPop(v));
            yield return Tween(.14f, u => { v.Lift = .06f * Out(u); v.Squash = Mathf.Lerp(1, .93f, u); });
            yield return Tween(.18f, u => { v.Lift = .06f * (1 - u * u); v.Squash = Mathf.LerpUnclamped(.93f, 1, OutBack(u)); });
            v.Lift = 0; v.Squash = 1;
        }
        IEnumerator Lurk(int unit, bool resting = false)
        {
            var v = panel.PawnView(unit); if (!v) yield break;
            Sound(Growl, .4f, resting ? .8f : 1.15f); Text(resting ? "숨 고름" : "…", Muted, panel.FxPoint(v.Head), .9f);
            yield return Tween(.42f, u => v.Sway = Mathf.Sin(u * Mathf.PI * 3) * 4 * (1 - u)); v.Sway = 0;
        }
        IEnumerator Alarm()
        {
            Sound(Growl, .75f, .82f);
            if (panel.NoiseGauge) Text("증원 접근", Danger, panel.FxPoint(panel.NoiseGauge) + Vector2.down * 6, .8f);
            yield return Wait(.4f);
        }
        IEnumerator Arrive(BattleEvent e)
        {
            var v = panel.SpawnPawn(e.Actor); if (!v) yield break;
            v.Lift = 3.2f; v.Alpha = 0; Sound(Growl, .9f, .95f);
            yield return Tween(.38f, u => { v.Lift = 3.2f * (1 - u * u); v.Alpha = Mathf.Clamp01(u * 3); });
            v.Lift = 0; v.Alpha = 1; Sound(Step, 1, .7f); Shake(HitShake);
            StartCoroutine(BurstRoutine(panel.FxPoint(v.transform.position), Muted, 1.1f, .3f)); StartCoroutine(Squash(v, .8f));
            Text("증원", Danger, panel.FxPoint(v.Head), .95f);
            yield return Wait(.25f);
        }
        // Treatment: the actor leans in (or kneels for itself), the target glows sage, a cross pops and +N rises.
        IEnumerator Treat(BattleEvent e)
        {
            var a = panel.PawnView(e.Actor); var t = panel.PawnView(e.Target); if (!a || !t) yield break;
            var reach = a == t ? Vector3.zero : Vector3.ClampMagnitude((t.Home - a.Home) * .35f, .9f);
            yield return Tween(.18f, u => { float k = Out(u); a.Offset = reach * k; a.Squash = Mathf.Lerp(1, .9f, k); a.Angle = (a == t ? 0 : Mathf.Sign(reach.x) * -6) * k; });
            panel.RevealOutcome(); Sound(Heal ? Heal : Block, .9f);
            var chest = panel.FxPoint(t.Chest);
            StartCoroutine(CrossPop(chest)); StartCoroutine(BurstRoutine(chest, Guarded, .9f, .35f));
            Text("+" + e.Damage, Guarded, chest + Vector2.up * 48, 1.15f);
            panel.ShowHealth(e.Target, e.Health, true);
            t.FlashColor = Guarded;
            yield return Tween(.45f, u => { t.FlashAmount = Mathf.Sin(u * Mathf.PI) * .7f; t.Lift = Mathf.Sin(u * Mathf.PI) * .05f; });
            t.FlashAmount = 0; t.Lift = 0;
            var held = a.Offset; float angle = a.Angle;
            yield return Tween(.2f, u => { float k = Cubic(u); a.Offset = held * (1 - k); a.Angle = angle * (1 - k); a.Squash = Mathf.Lerp(.9f, 1, k); });
            a.Offset = Vector3.zero; a.Angle = 0; a.Squash = 1;
        }
        IEnumerator CrossPop(Vector2 at)
        {
            var c = Spawn(CrossPrefab, at); if (!c) yield break;
            c.color = Guarded;
            yield return Tween(.16f, u => c.transform.localScale = Vector3.one * Mathf.LerpUnclamped(.3f, 1.1f, OutBack(u)));
            yield return Tween(.4f, u => { var k = Guarded; k.a = 1 - u; c.color = k; c.rectTransform.anchoredPosition = at + Vector2.up * 30 * u; });
            Destroy(c.gameObject);
        }
        // Declined click (target out of reach, no ammo): a low, short brush.
        public void Refuse() { Sound(Miss, .35f, .6f); }
        public IEnumerator Withdraw(IEnumerable<int> allies)
        {
            var list = allies.Select(panel.PawnView).Where(v => v && v.gameObject.activeSelf).ToList();
            Sound(Step, .8f, .9f);
            yield return Tween(.45f, u => { foreach (var v in list) { v.Offset = Vector3.left * 2.6f * u * u; v.Alpha = 1 - u; v.Lift = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 3)) * .06f; } });
        }

        // ---- reactions ----
        IEnumerator Wobble(BattlePawnView v, float away, float degrees, Vector3 dir, float knock)
        {
            var push = dir.normalized * knock; push.y *= .4f;
            yield return Tween(.5f, u => { float decay = Mathf.Exp(-5.5f * u); v.Sway = away * degrees * decay * Mathf.Cos(u * 17); v.Shove = push * Mathf.Exp(-7 * u); });
            v.Sway = 0; v.Shove = Vector3.zero;
        }
        IEnumerator Dodge(BattlePawnView v, Vector3 dir, float away)
        {
            var side = new Vector3(-dir.y, dir.x, 0).normalized; if (side.y < 0) side = -side;
            var slip = side * .16f + dir.normalized * .08f;
            yield return Tween(.12f, u => { float k = Out(u); v.Shove = slip * k; v.Sway = away * 7 * k; });
            yield return Tween(.24f, u => { float k = InOut(u); v.Shove = slip * (1 - k); v.Sway = away * 7 * (1 - k); });
            v.Shove = Vector3.zero; v.Sway = 0;
        }
        IEnumerator FlashRoutine(BattlePawnView v, bool critical, Color? tint = null)
        {
            v.FlashColor = Color.white; v.FlashAmount = 1;
            yield return Wait(FlashDuration * (critical ? 1.6f : 1));
            var after = tint ?? Danger;
            yield return Tween(TintDuration, u => { v.FlashColor = Color.Lerp(Color.white, after, Out(u)); v.FlashAmount = (1 - u) * .85f; });
            v.FlashAmount = 0;
        }
        IEnumerator EdgeRoutine()
        {
            if (!EdgeFlash) yield break;
            yield return Tween(.38f, u => { var c = Danger; c.a = EdgeAlpha * (1 - u); EdgeFlash.color = c; });
        }

        // ---- 2D effects on the FX layer ----
        T Spawn<T>(T prefab, Vector2 at) where T : Component
        {
            if (!prefab || !panel.FxLayer) return null;
            var item = Instantiate(prefab, panel.FxLayer); item.gameObject.SetActive(true);
            ((RectTransform)item.transform).anchoredPosition = at; spawned.Add(item.gameObject); return item;
        }
        void Text(string text, Color color, Vector2 at, float scale)
        {
            var label = Spawn(FloatingTextPrefab, at); if (label) label.Play(text, color, at, scale, FloatRise, D(FloatDuration));
        }
        IEnumerator BurstRoutine(Vector2 at, Color color, float size, float seconds)
        {
            var b = Spawn(BurstPrefab, at); if (!b) yield break;
            b.Reseed(cosmetic.Next(0, 9999)); b.transform.localRotation = Quaternion.Euler(0, 0, Jitter(0, 360));
            yield return Tween(seconds, u => { b.transform.localScale = Vector3.one * size * Mathf.Lerp(.45f, 1.15f, Cubic(u)); var c = color; c.a *= 1 - u * u; b.color = c; });
            Destroy(b.gameObject);
        }
        IEnumerator SlashRoutine(Vector2 at, Color color, float angle, float size)
        {
            var s = Spawn(SlashPrefab, at); if (!s) yield break;
            s.Angle = angle; s.color = color; s.transform.localScale = Vector3.one * size; s.SetProgress(0);
            yield return Tween(.08f, u => s.SetProgress(Out(u)));
            yield return Tween(.22f, u => { var c = color; c.a *= 1 - u; s.color = c; });
            Destroy(s.gameObject);
        }
        IEnumerator TracerRoutine(Vector2 from, Vector2 to, Color? color = null, float seconds = .13f)
        {
            var t = Spawn(TracerPrefab, Vector2.zero); if (!t) yield break;
            t.Set(from, to); var tint = color ?? Ochre;
            yield return Tween(seconds, u => { var c = tint; c.a = 1 - u; t.color = c; });
            Destroy(t.gameObject);
        }
        IEnumerator ShieldPop(BattlePawnView v)
        {
            var s = Spawn(ShieldPrefab, panel.FxPoint(v.Chest)); if (!s) yield break;
            s.color = Guarded;
            yield return Tween(.12f, u => s.transform.localScale = Vector3.one * Mathf.LerpUnclamped(.4f, 1.1f, OutBack(u)));
            yield return Tween(.3f, u => { var c = Guarded; c.a = 1 - u; s.color = c; s.transform.localScale = Vector3.one * (1.1f + .12f * u); });
            Destroy(s.gameObject);
        }

        // ---- timing ----
        float D(float seconds) => seconds / Mathf.Max(.1f, Speed);
        bool Frozen => Time.unscaledTime < frozenUntil;
        IEnumerator Tween(float seconds, Action<float> step)
        {
            float d = D(seconds), t = 0;
            while (t < d) { if (!Frozen) t += Time.unscaledDeltaTime; step(Mathf.Clamp01(t / Mathf.Max(.0001f, d))); yield return null; }
            step(1);
        }
        IEnumerator Wait(float seconds) { float d = D(seconds), t = 0; while (t < d) { if (!Frozen) t += Time.unscaledDeltaTime; yield return null; } }
        // Hit-stop: every tween holds its pose for a beat so the impact reads.
        IEnumerator Freeze(float seconds) { frozenUntil = Time.unscaledTime + D(seconds); while (Frozen) yield return null; }
        void Shake(float strength)
        {
            if (strength <= 0) return;
            float now = Time.unscaledTime; shakeStrength = now < shakeEnd ? Mathf.Max(strength, shakeStrength) : strength;
            shakeStart = now; shakeEnd = now + D(ShakeDuration);
        }
        void LateUpdate()
        {
            float now = Time.unscaledTime;
            ShakeOffset = now < shakeEnd ? (Vector3)(new Vector2(Jitter(-1, 1), Jitter(-1, 1)) * shakeStrength * (shakeEnd - now) / Mathf.Max(.001f, shakeEnd - shakeStart)) : Vector3.zero;
        }
        void Sound(AudioClip clip, float volume = 1, float pitch = 1)
        {
            if (!clip || voices.Count == 0 || Speed > 3) return;
            var source = voices[voice++ % voices.Count];
            source.pitch = pitch * (1 + Jitter(-PitchJitter, PitchJitter)); source.PlayOneShot(clip, Volume * volume);
        }
        static float Out(float u) => 1 - (1 - u) * (1 - u);
        static float Cubic(float u) => 1 - (1 - u) * (1 - u) * (1 - u);
        static float InOut(float u) => u < .5f ? 2 * u * u : 1 - 2 * (1 - u) * (1 - u);
        static float OutBack(float u) { const float c = 1.9f; float x = u - 1; return 1 + (c + 1) * x * x * x + c * x * x; }
    }
}
