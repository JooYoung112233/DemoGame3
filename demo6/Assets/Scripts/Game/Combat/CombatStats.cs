using System.Collections.Generic;
using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 전투 시험 계측. 기획 10장 훈련장 숫자(30초 초당 피해, 기본공격 가동률, 출처별 몫, 치명 비율)와
    /// 12장 합격 기준(피할 수 있었던 예고 공격에 맞은 비율)을 시험장에서 바로 본다.
    /// </summary>
    public sealed class CombatStats : MonoBehaviour
    {
        public const float ShortWindow = 5f;
        public const float DummyWindow = 30f;
        const float UptimeWindow = 30f;

        readonly struct Hit
        {
            public readonly float Time;
            public readonly int Amount;
            public readonly DamageSource Source;
            public readonly bool Crit;
            public readonly bool Dummy;
            public readonly bool Wood;

            public Hit(float time, int amount, DamageSource source, bool crit, bool dummy, bool wood)
            {
                Time = time;
                Amount = amount;
                Source = source;
                Crit = crit;
                Dummy = dummy;
                Wood = wood;
            }
        }

        readonly List<Hit> _hits = new List<Hit>(4096);
        readonly Queue<(float time, float dt, bool swinging)> _uptime = new Queue<(float, float, bool)>();
        readonly int[] _kills = new int[4];
        readonly Dictionary<string, float> _weaponRecords = new Dictionary<string, float>();

        /// <summary>마주침 종류. 7-3 기준은 보통 마주침(시간·체력 소모·첫 5초 무너짐)과 정예전(무너뜨림)을 따로 본다.</summary>
        public enum EncounterKind
        {
            Normal,
            Elite,
            Nest,
        }

        /// <summary>3차 초안 7-3 판정 기록: 마주침 한 번의 시간·체력 소모·무너짐.</summary>
        sealed class EncounterRecord
        {
            public EncounterKind Kind;
            public bool V3;
            public float Start = -1f;
            public float End = -1f;
            public int DamageTaken;
            public float HpLoss;
            public bool HeavyBreakInFirst5;
            public bool EliteBroken;
        }

        readonly Dictionary<int, EncounterRecord> _activeEncounters = new Dictionary<int, EncounterRecord>();
        readonly List<EncounterRecord> _encounters = new List<EncounterRecord>();
        float _uptimeTotal;
        float _uptimeSwinging;
        float _woodFirstHit = -1f;
        float _woodLockedDps = -1f;

        public int DamageTaken { get; private set; }
        public int HitsTaken { get; private set; }
        public int AvoidableTelegraphs { get; private set; }
        public int AvoidableTelegraphsHit { get; private set; }
        public int AllTelegraphs { get; private set; }
        public int AllTelegraphsHit { get; private set; }
        public int Kills(MonsterKind kind) => _kills[(int)kind];
        public IReadOnlyDictionary<string, float> WeaponRecords => _weaponRecords;

        void Awake()
        {
            CombatEvents.PlayerDealtDamage += OnDealt;
            CombatEvents.EnemyKilled += e => _kills[(int)e.Kind]++;
            CombatEvents.PlayerDamaged += amount =>
            {
                DamageTaken += amount;
                HitsTaken++;
                foreach (var rec in _activeEncounters.Values)
                {
                    if (rec.Start < 0f) rec.Start = Time.time;
                    rec.DamageTaken += amount;
                }
            };
            CombatEvents.EncounterSpawned += (group, elite) =>
            {
                // 시험장에는 마주침이 한 번에 하나다. 정리되지 않고 다시 세운 마주침은 기록하지 않는다.
                _activeEncounters.Clear();
                var root = CombatTestRoot.Instance;
                var kind = elite ? EncounterKind.Elite
                    : root && root.CurrentPreset == CombatTestRoot.Preset.Nest ? EncounterKind.Nest
                    : EncounterKind.Normal;
                _activeEncounters[group] = new EncounterRecord { Kind = kind, V3 = Tuning.Ruleset == CombatRuleset.V3 };
            };
            CombatEvents.EnemyBroken += e =>
            {
                if (!_activeEncounters.TryGetValue(e.GroupId, out var rec)) return;
                // 닿기 전에 무너져도(돌진이 기둥에 박힘) 그때부터 싸움이다.
                if (rec.Start < 0f) rec.Start = Time.time;
                if (e.IsElite) rec.EliteBroken = true;
                // 첫 5초 기준은 무거운 적(멧돼지·정예). 궁수는 마무리 한 번에 무너지는 게 정상이다.
                if (e.Weight == EnemyWeight.Heavy && Time.time - rec.Start <= 5f) rec.HeavyBreakInFirst5 = true;
            };
            CombatEvents.EncounterCleared += group =>
            {
                if (!_activeEncounters.TryGetValue(group, out var rec)) return;
                _activeEncounters.Remove(group);
                if (rec.Start < 0f) return;
                rec.End = Time.time;
                var player = PlayerController.Instance;
                rec.HpLoss = player && player.Health.Max > 0 ? (float)rec.DamageTaken / player.Health.Max : 0f;
                _encounters.Add(rec);
            };
            CombatEvents.TelegraphResolved += (avoidable, hit) =>
            {
                AllTelegraphs++;
                if (hit) AllTelegraphsHit++;
                if (!avoidable) return;
                AvoidableTelegraphs++;
                if (hit) AvoidableTelegraphsHit++;
            };
        }

        public void ResetAll()
        {
            _hits.Clear();
            _uptime.Clear();
            _uptimeTotal = 0f;
            _uptimeSwinging = 0f;
            for (int i = 0; i < _kills.Length; i++) _kills[i] = 0;
            DamageTaken = 0;
            HitsTaken = 0;
            AvoidableTelegraphs = 0;
            AvoidableTelegraphsHit = 0;
            AllTelegraphs = 0;
            AllTelegraphsHit = 0;
            _woodFirstHit = -1f;
            _woodLockedDps = -1f;
            _encounters.Clear();
        }

        /// <summary>3차 값으로 정리한 마주침 수(종류별).</summary>
        public int EncounterCount(EncounterKind kind)
        {
            int n = 0;
            foreach (var r in _encounters)
                if (r.V3 && r.Kind == kind) n++;
            return n;
        }

        /// <summary>3차 값 마주침 기록. 시간·체력 소모 중앙과 첫 5초 무거운 적 무너짐 비율은 그 종류 안에서, 정예전 무너뜨림은 정예 마주침에서.</summary>
        public void EncounterSummary(EncounterKind kind, out int count, out float medianSeconds, out float medianHpLoss, out float firstFiveBreakRate, out int eliteBroken)
        {
            var times = new List<float>();
            var losses = new List<float>();
            int early = 0;
            eliteBroken = 0;
            foreach (var r in _encounters)
            {
                if (!r.V3 || r.Kind != kind) continue;
                times.Add(r.End - r.Start);
                losses.Add(r.HpLoss);
                if (r.HeavyBreakInFirst5) early++;
                if (r.EliteBroken) eliteBroken++;
            }
            count = times.Count;
            medianSeconds = Median(times);
            medianHpLoss = Median(losses);
            firstFiveBreakRate = count > 0 ? (float)early / count : 0f;
        }

        static float Median(List<float> values)
        {
            if (values.Count == 0) return 0f;
            values.Sort();
            int m = values.Count / 2;
            return values.Count % 2 == 1 ? values[m] : (values[m - 1] + values[m]) * 0.5f;
        }

        public void ResetDummyWindow()
        {
            _hits.RemoveAll(h => h.Wood);
            _woodFirstHit = -1f;
            _woodLockedDps = -1f;
        }

        void OnDealt(DamageDealt d)
        {
            if (d.Target && _activeEncounters.TryGetValue(d.Target.GroupId, out var rec) && rec.Start < 0f) rec.Start = Time.time;
            bool dummy = d.Target && d.Target.IsDummy;
            bool wood = d.Target is DummyBrain db && db.IsWood;
            if (wood && _woodFirstHit < 0f) _woodFirstHit = Time.time;
            _hits.Add(new Hit(Time.time, d.Amount, d.Source, d.Crit, dummy, wood));
            if (_hits.Count > 20000) _hits.RemoveRange(0, 5000);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var player = PlayerController.Instance;
            bool swinging = player && player.IsSwinging;
            _uptime.Enqueue((Time.time, dt, swinging));
            _uptimeTotal += dt;
            if (swinging) _uptimeSwinging += dt;
            while (_uptime.Count > 0 && _uptime.Peek().time < Time.time - UptimeWindow)
            {
                var old = _uptime.Dequeue();
                _uptimeTotal -= old.dt;
                if (old.swinging) _uptimeSwinging -= old.dt;
            }
        }

        /// <summary>최근 5초 초당 피해(허수아비 제외).</summary>
        public float RecentDps
        {
            get
            {
                float from = Time.time - ShortWindow;
                long sum = 0;
                for (int i = _hits.Count - 1; i >= 0 && _hits[i].Time >= from; i--)
                    if (!_hits[i].Dummy) sum += _hits[i].Amount;
                return sum / ShortWindow;
            }
        }

        /// <summary>
        /// 나무 허수아비 초당 피해(기획 10장 30초 측정). 첫 타부터 30초 동안 합계를 30으로 나누고, 30초가 되면 값을 고정해 무기 기록에 저장한다.
        /// 무기나 층을 바꾸면 측정을 다시 시작한다.
        /// </summary>
        public float WoodDps(out float measuredSeconds, out bool done)
        {
            measuredSeconds = 0f;
            done = _woodLockedDps >= 0f;
            if (done)
            {
                measuredSeconds = DummyWindow;
                return _woodLockedDps;
            }
            if (_woodFirstHit < 0f) return 0f;
            float elapsed = Time.time - _woodFirstHit;
            float window = Mathf.Min(DummyWindow, elapsed);
            measuredSeconds = window;
            if (window < 1f) return 0f;
            float end = _woodFirstHit + window;
            long sum = 0;
            foreach (var h in _hits)
                if (h.Wood && h.Time >= _woodFirstHit && h.Time <= end) sum += h.Amount;
            float dps = sum / window;
            if (elapsed >= DummyWindow)
            {
                _woodLockedDps = dps;
                done = true;
                var p = PlayerController.Instance;
                var root = CombatTestRoot.Instance;
                if (p && root) RecordWeapon($"{p.Weapon.displayName} {root.Floor}층", dps);
            }
            return dps;
        }

        public void RecordWeapon(string weaponName, float dps)
        {
            if (dps > 0f) _weaponRecords[weaponName] = dps;
        }

        /// <summary>최근 30초 동안 휘두르고 있던 시간 비율.</summary>
        public float AttackUptime => _uptimeTotal > 0.5f ? _uptimeSwinging / _uptimeTotal : 0f;

        /// <summary>최근 30초 출처별 몫(기본공격, 회오리, 검풍)과 치명 비율.</summary>
        public void Shares(out float basic, out float whirl, out float wave, out float critRate, bool woodOnly)
        {
            float from = Time.time - DummyWindow;
            long b = 0, w = 0, s = 0;
            int count = 0, crits = 0;
            for (int i = _hits.Count - 1; i >= 0 && _hits[i].Time >= from; i--)
            {
                var h = _hits[i];
                if (woodOnly ? !h.Wood : h.Dummy) continue;
                count++;
                if (h.Crit) crits++;
                switch (h.Source)
                {
                    case DamageSource.Basic: b += h.Amount; break;
                    case DamageSource.Whirlwind: w += h.Amount; break;
                    case DamageSource.SwordWave: s += h.Amount; break;
                }
            }
            float total = Mathf.Max(1, b + w + s);
            basic = b / total;
            whirl = w / total;
            wave = s / total;
            critRate = count > 0 ? (float)crits / count : 0f;
        }
    }
}
