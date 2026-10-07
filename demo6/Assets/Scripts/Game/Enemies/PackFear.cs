using System.Collections.Generic;
using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 무리 공포(기획/전투-보스-무기-다듬기-1차.md 4-2 [2], FearRule): CombatEvents.EnemyKilled(무거운 적·정예·보스)와 EnemyExecuted(처형 2.5초)를 듣고
    /// 반경 6 안 굴쥐를 겁먹게 한다(RatBrain의 겁먹음 상태). '무리 거느린' 정예가 죽으면 졸개 4초 + 50% 사라짐(PackLeader), 둥지 굴쥐 1.0초.
    /// Tuning.FearOn이 꺼져 있으면 아무것도 하지 않는다. PlayerController.Create가 플레이어에 붙인다(두 시험장 공통).
    /// 처형은 처치 사건과 처형 사건이 같은 프레임에 함께 오므로 한 프레임 모아 LateUpdate에서 한 번만 센다(처형이면 2.5초).
    /// 겁먹을 때마다 CombatEvents.EnemyFrightened를 낸다. 글자는 띄우지 않고 몸 떨림(RatBrain)과 찍찍 소리만 쓴다.
    /// 찍찍은 던전에서는 LurkSounds(꾸러미 ④)가 EnemyFrightened를 듣고 내고, LurkSounds가 없는 전투 시험장에서만 여기서 코드 임시음을 낸다.
    /// </summary>
    public sealed class PackFear : MonoBehaviour
    {
        struct Pending
        {
            public Enemy Enemy;
            public Vector2 At;
            public bool Killed;
            public bool Execution;
            public PackLeader Leader;
        }

        /// <summary>찍찍 사이 최소 간격(실제 시간). 한 번에 여러 마리가 겁먹어도 한두 번만 난다.</summary>
        const float SqueakGap = 0.15f;
        const float SqueakVolume = 0.35f;

        static PackFear _instance;
        static AudioClip _squeakClip;
        static float _lastSqueak = -999f;
        static readonly List<RatBrain> Scratch = new List<RatBrain>();

        readonly List<Pending> _pending = new List<Pending>();
        AudioSource _voice;

        /// <summary>플레이 시작부터 겁먹인 횟수(시험 패널·eval 확인용).</summary>
        public static int FrightenedCount { get; private set; }
        /// <summary>마지막으로 겁먹인 시간(초).</summary>
        public static float LastSeconds { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
            _squeakClip = null;
            _lastSqueak = -999f;
            Scratch.Clear();
            FrightenedCount = 0;
            LastSeconds = 0f;
        }

        void OnEnable()
        {
            _instance = this;
            CombatEvents.EnemyKilled += OnKilled;
            CombatEvents.EnemyExecuted += OnExecuted;
        }

        void OnDisable()
        {
            CombatEvents.EnemyKilled -= OnKilled;
            CombatEvents.EnemyExecuted -= OnExecuted;
            _pending.Clear();
            if (_instance == this) _instance = null;
        }

        void OnKilled(Enemy e) => Queue(e, true, false);

        void OnExecuted(Enemy e, bool ambush) => Queue(e, false, true);

        void Queue(Enemy e, bool killed, bool execution)
        {
            if (!Tuning.FearOn || !e || !FearRule.Triggers(e.Class)) return;
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].Enemy != e) continue;
                var p = _pending[i];
                p.Killed |= killed;
                p.Execution |= execution;
                _pending[i] = p;
                return;
            }
            _pending.Add(new Pending
            {
                Enemy = e,
                At = e.Position,
                Killed = killed,
                Execution = execution,
                Leader = e.GetComponent<PackLeader>(),
            });
        }

        void LateUpdate()
        {
            if (_pending.Count == 0) return;
            for (int i = 0; i < _pending.Count; i++)
            {
                var p = _pending[i];
                // 처형 사건만 오고 쓰러지지 않았으면(기습으로 무너지기만 한 멧돼지) 공포는 없다.
                bool dead = p.Killed || (p.Enemy && p.Enemy.Dead);
                if (dead && Tuning.FearOn) Spread(p);
            }
            _pending.Clear();
        }

        /// <summary>한 마리의 죽음: 거느린 졸개(4초 + 사라짐)와 반경 6 안 굴쥐(1.5초, 처형 2.5초, 둥지 굴쥐 1.0초).</summary>
        static void Spread(in Pending p)
        {
            if (p.Leader) p.Leader.ScatterPack(FearRule.PackLeaderSeconds, FearRule.PackVanishChance, p.At);
            foreach (var rat in Collect(p.At, FearRule.Radius))
            {
                if (p.Leader && p.Leader.Has(rat)) continue;
                Frighten(rat, p.At, FearRule.Seconds(p.Execution, false, rat.FromNest), false);
            }
        }

        /// <summary>
        /// 그 자리 반경 안 깨어 있는(곧 깰) 굴쥐. 잠든 굴쥐는 아직 모르므로 겁먹지 않는다.
        /// 겁먹이며 사건을 내는 동안 적 목록이 바뀌어도 되게 복사본을 돌려준다(죽음 한 번에 한 번이라 할당은 작다).
        /// </summary>
        static RatBrain[] Collect(Vector2 center, float radius)
        {
            Scratch.Clear();
            float r2 = radius * radius;
            foreach (var e in Enemy.All)
                if (e is RatBrain rat && !rat.Dead && (rat.Aware || rat.WakePending) && (rat.Position - center).sqrMagnitude <= r2)
                    Scratch.Add(rat);
            var rats = Scratch.ToArray();
            Scratch.Clear();
            return rats;
        }

        /// <summary>
        /// 굴쥐 한 마리를 seconds 동안 겁먹게 하고 CombatEvents.EnemyFrightened를 낸다(겁먹음은 이 입구로 넣는다).
        /// vanish면 달아나다 흐려져 사라진다(보상 없는 졸개만). 겁먹였으면 true.
        /// </summary>
        public static bool Frighten(RatBrain rat, Vector2 awayFrom, float seconds, bool vanish)
        {
            if (!rat || rat.Dead || seconds <= 0f) return false;
            rat.Frighten(awayFrom, seconds, vanish);
            FrightenedCount++;
            LastSeconds = seconds;
            CombatEvents.RaiseEnemyFrightened(rat, seconds);
            return true;
        }

        /// <summary>그 자리 반경 안 굴쥐를 seconds 동안 겁먹게 한다(보스 처치 뒤 소환 굴쥐 흩어짐 등, 다른 꾸러미가 부를 수 있음). Tuning.FearOn과 상관없이 부른 쪽이 정한다.</summary>
        public static void Scatter(Vector2 center, float radius, float seconds)
        {
            if (radius <= 0f || seconds <= 0f) return;
            foreach (var rat in Collect(center, radius)) Frighten(rat, center, seconds, false);
        }

        /// <summary>
        /// 겁먹은 굴쥐 찍찍(RatBrain.Frighten이 새로 겁먹을 때 부른다). 실제 시간 0.15초에 한 번.
        /// 던전(LurkSounds가 있음)에서는 LurkSounds가 EnemyFrightened로 내므로 여기서는 내지 않는다.
        /// </summary>
        public static void Squeak()
        {
            if (!_instance || !Tuning.Sound) return;
            float now = Time.unscaledTime;
            if (now - _lastSqueak < SqueakGap) return;
            _lastSqueak = now;
            if (FindAnyObjectByType<LurkSounds>()) return;
            _instance.PlaySqueak();
        }

        void PlaySqueak()
        {
            if (!_voice)
            {
                var go = new GameObject("FearSqueak");
                go.transform.SetParent(transform, false);
                _voice = go.AddComponent<AudioSource>();
                _voice.playOnAwake = false;
                _voice.spatialBlend = 0f;
            }
            if (!_squeakClip) _squeakClip = BuildSqueak();
            _voice.pitch = Random.Range(0.92f, 1.12f);
            _voice.PlayOneShot(_squeakClip, Mathf.Clamp01(Tuning.SoundVolume * SqueakVolume));
        }

        /// <summary>코드 임시음: 짧은 '찍' 두 번(위로 꺾였다 내려오는 높은 소리 + 숨소리 조금).</summary>
        static AudioClip BuildSqueak()
        {
            const int rate = 44100;
            const float chirp = 0.065f;
            const float secondStart = 0.09f;
            int n = Mathf.CeilToInt(rate * (secondStart + chirp));
            var data = new float[n];
            var noise = new System.Random(7);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float local;
                if (t < chirp) local = t / chirp;
                else if (t >= secondStart) local = (t - secondStart) / chirp;
                else continue;
                local = Mathf.Clamp01(local);
                float freq = 2900f + 1100f * Mathf.Sin(local * Mathf.PI);
                phase += freq / rate;
                phase -= Mathf.Floor(phase);
                float env = Mathf.Sin(local * Mathf.PI);
                env *= env;
                float tone = Mathf.Sin(phase * 2f * Mathf.PI) + 0.25f * Mathf.Sin(phase * 4f * Mathf.PI);
                float breath = ((float)noise.NextDouble() * 2f - 1f) * 0.12f;
                data[i] = (tone * 0.8f + breath) * env * 0.5f;
            }
            var clip = AudioClip.Create("RatSqueak", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
