// V045 visual clip selector. Presentation only.
// Reads existing player states and clocks. Never starts an action or changes combat rules.
using System;
using System.Collections.Generic;
using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    public sealed class CharacterMotionV045
    {
        [Serializable] public sealed class Library { public Clip[] clips; }
        [Serializable] public sealed class Clip
        {
            public string id, weapon;
            public float duration, @base, tip;
            public float[] hits;
            public int width, height, ppu, rootX, rootY;
            public string[] layers;
            public bool hideFeet, down, loop;
            public Frame[] frames;
        }
        [Serializable] public sealed class Frame
        {
            public float t, twist, lean, spin, capeTurn, headTurn;
            public bool weaponReleased;
            public float releaseBlend;
            public float[] bodyOrigin, capeOrigin, right, left;
            public Sockets sockets;
            public Part[] parts;
        }
        [Serializable] public sealed class Sockets
        {
            public float[] shoulderR, elbowR, wristR, gripR, shoulderL, elbowL, wristL, gripL;
        }
        [Serializable] public sealed class Part
        {
            public int layer;
            public string name, resource, gearRole;
            public float x, y, opacity;
        }
        public struct Sample
        {
            public Clip clip;
            public Frame frame;
            public float time;
            public int index;
            public bool recovering;
            // Native cels already include spin. Do not rotate them by frame.spin again.
        }

        readonly Dictionary<string, Clip> _clips = new Dictionary<string, Clip>();
        PlayerPose _priorPose;
        string _priorWeapon, _returnClip;
        float _returnAge;
        bool _initialized;

        public CharacterMotionV045(string json)
        {
            var data = JsonUtility.FromJson<Library>(json);
            if (data?.clips == null) throw new ArgumentException("Missing motion clips");
            foreach (var clip in data.clips)
            {
                if (clip.frames == null || clip.frames.Length == 0 || clip.ppu != 160)
                    throw new ArgumentException("Invalid motion clip " + clip.id);
                _clips.Add(clip.id, clip);
            }
        }

        public void Reset()
        {
            _initialized = false; _returnClip = null; _returnAge = 0; _priorWeapon = null;
        }

        public bool TrySample(PlayerController player, float visualDeltaTime, out Sample result)
        {
            result = default;
            string weapon = player ? player.Weapon?.id : null;
            string prefix = weapon == "wpn_longsword" ? "sword" :
                weapon == "wpn_greatsword" ? "great" : weapon == "wpn_twinblades" ? "twin" : null;
            if (prefix == null) { Reset(); return false; }
            var pose = player.Pose;
            bool locomotion = pose == PlayerPose.Idle || pose == PlayerPose.Move;
            bool weaponChanged = _priorWeapon != weapon;
            if (weaponChanged) { _returnClip = null; _returnAge = 0; }
            if (_initialized && !weaponChanged && locomotion && _priorPose != pose &&
                _priorPose != PlayerPose.Idle && _priorPose != PlayerPose.Move)
            {
                _returnClip = _priorPose == PlayerPose.WaveCast ? prefix + "-wave-return" :
                    _priorPose == PlayerPose.Whirl ? prefix + "-whirl-return" :
                    _priorPose == PlayerPose.Down ? prefix + "-stand" : null;
                _returnAge = 0;
            }
            _initialized = true; _priorPose = pose; _priorWeapon = weapon;
            string id = null; float time = 0; bool mapSwing = false, recovery = false;
            if (player.ExecutionPoseActive)
            {
                // ExecutionPoseHitTime=.06 is the pull. The art's downward apex is .16.
                id = prefix + "-execution"; time = player.ExecutionPoseTime;
            }
            else if (player.InWeaponAct)
            {
                time = player.ActPhaseTime;
                switch (player.ActPhase)
                {
                    case WeaponActPhase.Raise: id = "sword-guard-raise"; break;
                    case WeaponActPhase.Hold: id = "sword-guard-hold"; time = 0; break;
                    case WeaponActPhase.Lower: id = "sword-guard-lower"; break;
                    case WeaponActPhase.Recoil:
                        id = player.GuardRecoilBoss ? "sword-guard-boss-recoil" : "sword-guard-recoil"; break;
                    case WeaponActPhase.ParryPush: id = "sword-parry-push"; break;
                    case WeaponActPhase.Break: id = "sword-guard-break"; break;
                    case WeaponActPhase.Flinch: id = prefix + "-flinch"; break;
                    case WeaponActPhase.Charging: id = "great-charge"; time = player.ChargeHeldTime; break;
                    case WeaponActPhase.Release: id = "great-release-" + player.ReleaseLevel; break;
                    case WeaponActPhase.Flurry: id = "twin-flurry"; break;
                }
            }
            else if (pose == PlayerPose.Attack)
            {
                if (player.RiposteActive) id = "sword-parry-riposte";
                else if (player.ComboIndex == 0 && prefix == "sword") id = "sword-first-slash";
                else if (player.ComboIndex == 0 && prefix == "twin") id = "twin-first-right-return";
                else if (player.ComboIndex > 0) id = prefix + "-combo-" + (player.ComboIndex + 1);
                // Greatsword combo zero remains on the approved V044 path.
                time = player.PoseTime; mapSwing = true;
            }
            else if (pose == PlayerPose.WaveCast) { id = prefix + "-wave"; time = player.PoseTime; }
            else if (pose == PlayerPose.Whirl) { id = prefix + "-whirl"; time = player.PoseTime; }
            else if (pose == PlayerPose.Dodge) { id = prefix + "-dodge"; time = player.PoseTime; }
            else if (pose == PlayerPose.Hurt) { id = prefix + "-hurt"; time = player.PoseTime; }
            else if (pose == PlayerPose.Down) { id = prefix + "-down"; time = player.PoseTime; }
            else if (locomotion && _returnClip != null)
            {
                id = _returnClip; time = _returnAge; recovery = true;
                _returnAge += Mathf.Max(0, visualDeltaTime);
            }
            if (!locomotion) _returnClip = null;
            if (id == null || !_clips.TryGetValue(id, out var clip)) return false;
            if (recovery && time >= clip.duration) { _returnClip = null; return false; }
            if (mapSwing)
            {
                // Every actual hit is mapped separately. Attack speed leaves later hit intervals fixed.
                var plan = player.CurrentSwingPlan;
                if (visualDeltaTime <= 0)
                    for (int i = 0; i < plan.Hits; i++)
                    {
                        float h = plan.HitTime(i);
                        if (time >= h && time - h < .04f) time = h;
                    }
                time = MapSwingTime(time, plan, clip);
            }
            time = Mathf.Clamp(time, 0, clip.duration);
            int frame = 0;
            while (frame + 1 < clip.frames.Length && clip.frames[frame + 1].t <= time + .00001f) frame++;
            result = new Sample { clip = clip, frame = clip.frames[frame], time = time, index = frame, recovering = recovery };
            return true;
        }

        public static float MapSwingTime(float time, SwingPlan plan, Clip clip)
        {
            float from = 0, to = 0;
            int count = Math.Min(plan.Hits, clip.hits.Length);
            for (int i = 0; i < count; i++)
            {
                float nextFrom = plan.HitTime(i), nextTo = clip.hits[i];
                if (time <= nextFrom) return Mathf.Lerp(to, nextTo, Mathf.InverseLerp(from, nextFrom, time));
                from = nextFrom; to = nextTo;
            }
            return Mathf.Lerp(to, clip.duration, Mathf.InverseLerp(from, plan.Duration, time));
        }
    }
}
