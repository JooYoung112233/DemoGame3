using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Loot;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 전투 시험 계측. 기획 10장 훈련장 숫자(30초 초당 피해, 기본공격 가동률, 출처별 몫, 치명 비율)와
    /// 12장 합격 기준(피할 수 있었던 예고 공격에 맞은 비율)을 시험장에서 바로 본다.
    /// 장비 문서 6장: 출처별 몫에 '전설' 줄(Shares 전설판)과 효과별 발동 수·맞힌 수·피해·처치·가장 큰 발동(LegendProcs 등, LegendShares)을 더한다.
    /// 전투·보스·무기 다듬기 1차: 출혈·환경 몫(Shares 여섯 몫 판), 벽 박기·겁먹음·처형(기습·무너짐)·회피 반격(열림·적중)·보스 단계 계수,
    /// 처형으로 끝난 무거운 적 비율(HeavyExecutedFraction), 보스·연습 마주침을 따로 센다.
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

        /// <summary>전설 피해 한 번(효과별 몫을 나누려고 따로 적음).</summary>
        readonly struct LegendHit
        {
            public readonly float Time;
            public readonly int Amount;
            public readonly LegendaryEffect Effect;
            public readonly bool Dummy;
            public readonly bool Wood;

            public LegendHit(float time, int amount, LegendaryEffect effect, bool dummy, bool wood)
            {
                Time = time;
                Amount = amount;
                Effect = effect;
                Dummy = dummy;
                Wood = wood;
            }
        }

        readonly List<Hit> _hits = new List<Hit>(4096);
        readonly List<LegendHit> _legendHits = new List<LegendHit>(512);
        /// <summary>효과별 발동 수·맞힌 수·피해·처치·가장 큰 발동(번개 = 튕김 수, 폭발 = 연쇄 길이). 시험 패널 '다시 재기'까지 쌓는다.</summary>
        readonly int[] _legendProcs = new int[LegendaryTable.Count];
        readonly int[] _legendHitCount = new int[LegendaryTable.Count];
        readonly long[] _legendDamage = new long[LegendaryTable.Count];
        readonly int[] _legendKills = new int[LegendaryTable.Count];
        readonly int[] _legendLargest = new int[LegendaryTable.Count];
        readonly Queue<(float time, float dt, bool swinging)> _uptime = new Queue<(float, float, bool)>();
        /// <summary>종류별 처치 수(MonsterKind 차례). 오우거가 더해져 종류 수로 잡는다.</summary>
        readonly int[] _kills = new int[System.Enum.GetValues(typeof(MonsterKind)).Length];
        readonly Dictionary<string, float> _weaponRecords = new Dictionary<string, float>();

        /// <summary>
        /// 마주침 종류. 7-3 기준은 보통 마주침(시간·체력 소모·첫 5초 무너짐)과 정예전(무너뜨림)을 따로 본다.
        /// 보스(갱도 오우거, 3-10 처치 시간 45~65초)와 연습(기둥 옆 멧돼지)은 보통 기록에 섞이지 않게 따로 센다.
        /// </summary>
        public enum EncounterKind
        {
            Normal,
            Elite,
            Nest,
            Boss,
            Practice,
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

        // ── 전투·보스·무기 다듬기 1차 사건 계수(기획/전투-보스-무기-다듬기-1차.md 3-10·4-4 확인 지표). '통계 초기화'까지 쌓는다 ──

        /// <summary>벽·기둥 박기 수(CombatEvents.EnemyWallSlam).</summary>
        public int WallSlams { get; private set; }
        /// <summary>겁먹음 수(CombatEvents.EnemyFrightened, 무리 공포).</summary>
        public int Frightened { get; private set; }
        /// <summary>기습 처형 수(잠든 적 등 뒤 첫 타)와 무너짐 처형 수(CombatEvents.EnemyExecuted).</summary>
        public int AmbushExecutions { get; private set; }
        public int BreakExecutions { get; private set; }
        /// <summary>회피 반격 창이 열린 수와 첫 타가 들어간 수(4-4 기준 1분에 1~3번).</summary>
        public int CountersOpened { get; private set; }
        public int CountersLanded { get; private set; }
        /// <summary>마지막으로 알려진 보스 단계(0 = 아직 없음, 1·2)와 2단계로 넘어간 수(CombatEvents.BossPhaseChanged).</summary>
        public int BossPhase { get; private set; }
        public int BossPhase2Entries { get; private set; }
        /// <summary>쓰러뜨린 무거운 적(멧돼지·정예, 보스·허수아비 빼고)과 그 가운데 처형으로 끝난 수.</summary>
        public int HeavyKills { get; private set; }
        public int HeavyExecuted { get; private set; }
        /// <summary>무거운 적이 처형으로 끝나는 비율(4-4 기준 50~70%). 무거운 적을 아직 못 쓰러뜨렸으면 0.</summary>
        public float HeavyExecutedFraction => HeavyKills > 0 ? Mathf.Clamp01((float)HeavyExecuted / HeavyKills) : 0f;
        public int Kills(MonsterKind kind) => _kills[(int)kind];
        public IReadOnlyDictionary<string, float> WeaponRecords => _weaponRecords;

        void Awake()
        {
            CombatEvents.PlayerDealtDamage += OnDealt;
            CombatEvents.EnemyKilled += e =>
            {
                int k = (int)e.Kind;
                if (k >= 0 && k < _kills.Length) _kills[k]++;
                if (CountsAsHeavy(e)) HeavyKills++;
            };
            // 규칙이 듣지 않는 대상(보스·둥지·허수아비)의 닿음은 벽 박기로 세지 않는다(WallSlamRule.Resolve = 없음).
            CombatEvents.EnemyWallSlam += (e, hit) =>
            {
                if (e && !e.Class.Immune) WallSlams++;
            };
            CombatEvents.EnemyFrightened += (e, seconds) => Frightened++;
            CombatEvents.EnemyExecuted += (e, ambush) =>
            {
                if (ambush) AmbushExecutions++;
                else BreakExecutions++;
                // 무거운 적의 끝: 무너짐 처형은 늘 끝이고, 기습 처형은 멧돼지를 무너뜨리기만 하므로 쓰러졌을 때만 센다.
                if (CountsAsHeavy(e) && (!ambush || e.Dead)) HeavyExecuted++;
            };
            CombatEvents.CounterOpened += () => CountersOpened++;
            CombatEvents.CounterLanded += e => CountersLanded++;
            CombatEvents.BossPhaseChanged += (boss, phase) =>
            {
                if (phase >= 2 && BossPhase < 2) BossPhase2Entries++;
                BossPhase = phase;
            };
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
                var preset = root ? root.CurrentPreset : CombatTestRoot.Preset.FrontBack;
                var kind = elite ? EncounterKind.Elite
                    : preset == CombatTestRoot.Preset.Nest ? EncounterKind.Nest
                    : preset == CombatTestRoot.Preset.Boss ? EncounterKind.Boss
                    : preset == CombatTestRoot.Preset.PillarBoar ? EncounterKind.Practice
                    : EncounterKind.Normal;
                // 새로 놓인 오우거는 1단계에서 시작한다(재도전 포함).
                if (kind == EncounterKind.Boss) BossPhase = 1;
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
            CombatEvents.LegendTriggered += OnLegendTriggered;
            CombatEvents.LegendDealt += OnLegendDealt;
        }

        /// <summary>'처형으로 끝난 무거운 적 비율'에 넣는 적: 무거운 적(멧돼지·정예)이고 보스·허수아비가 아님.</summary>
        static bool CountsAsHeavy(Enemy e) => e && e.Weight == EnemyWeight.Heavy && !e.IsBoss && !e.IsDummy;

        void OnLegendTriggered(LegendaryEffect effect, int size)
        {
            int i = (int)effect;
            if (i < 0 || i >= LegendaryTable.Count) return;
            _legendProcs[i]++;
            if (size > _legendLargest[i]) _legendLargest[i] = size;
        }

        void OnLegendDealt(LegendaryEffect effect, DamageDealt d)
        {
            int i = (int)effect;
            if (i < 0 || i >= LegendaryTable.Count) return;
            _legendHitCount[i]++;
            _legendDamage[i] += d.Amount;
            if (d.Killed) _legendKills[i]++;
            bool dummy = d.Target && d.Target.IsDummy;
            bool wood = d.Target is DummyBrain db && db.IsWood;
            _legendHits.Add(new LegendHit(Time.time, d.Amount, effect, dummy, wood));
            if (_legendHits.Count > 8000) _legendHits.RemoveRange(0, 2000);
        }

        /// <summary>그 효과가 발동한 수(번개 = 튕김이 난 발동, 발자국 = 깐 불길, 폭발 = 폭발 하나).</summary>
        public int LegendProcs(LegendaryEffect effect) => _legendProcs[(int)effect];
        /// <summary>그 효과가 적을 맞힌 수.</summary>
        public int LegendHits(LegendaryEffect effect) => _legendHitCount[(int)effect];
        public long LegendDamage(LegendaryEffect effect) => _legendDamage[(int)effect];
        public int LegendKills(LegendaryEffect effect) => _legendKills[(int)effect];
        /// <summary>한 번 발동의 가장 큰 크기: 연쇄 번개 = 가장 많이 튕긴 수(최대 4), 연쇄 폭발 = 가장 긴 연쇄(최대 12), 불꽃 발자국 = 1.</summary>
        public int LegendLargest(LegendaryEffect effect) => _legendLargest[(int)effect];

        /// <summary>최근 30초 전설 피해를 효과별로 나눈 몫(전설 피해 합 = 1). 전설 피해가 없으면 모두 0.</summary>
        public void LegendShares(out float lightning, out float flame, out float blast, bool woodOnly)
        {
            float from = Time.time - DummyWindow;
            long l = 0, f = 0, b = 0;
            for (int i = _legendHits.Count - 1; i >= 0 && _legendHits[i].Time >= from; i--)
            {
                var h = _legendHits[i];
                if (woodOnly ? !h.Wood : h.Dummy) continue;
                switch (h.Effect)
                {
                    case LegendaryEffect.ChainLightning: l += h.Amount; break;
                    case LegendaryEffect.FlameSteps: f += h.Amount; break;
                    case LegendaryEffect.ChainBlast: b += h.Amount; break;
                }
            }
            float total = l + f + b;
            if (total <= 0f)
            {
                lightning = flame = blast = 0f;
                return;
            }
            lightning = l / total;
            flame = f / total;
            blast = b / total;
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
            WallSlams = 0;
            Frightened = 0;
            AmbushExecutions = 0;
            BreakExecutions = 0;
            CountersOpened = 0;
            CountersLanded = 0;
            BossPhase2Entries = 0;
            HeavyKills = 0;
            HeavyExecuted = 0;
            _woodFirstHit = -1f;
            _woodLockedDps = -1f;
            _encounters.Clear();
            _legendHits.Clear();
            for (int i = 0; i < LegendaryTable.Count; i++)
            {
                _legendProcs[i] = 0;
                _legendHitCount[i] = 0;
                _legendDamage[i] = 0;
                _legendKills[i] = 0;
                _legendLargest[i] = 0;
            }
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
            _legendHits.RemoveAll(h => h.Wood);
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

        /// <summary>최근 30초 출처별 몫(기본공격, 회오리, 검풍)과 치명 비율. 전설 피해는 분모·치명 비율에 넣지 않는다(예전 값 그대로).</summary>
        public void Shares(out float basic, out float whirl, out float wave, out float critRate, bool woodOnly) =>
            Shares(out basic, out whirl, out wave, out _, out critRate, woodOnly, false);

        /// <summary>
        /// 최근 30초 출처별 몫(기본공격, 회오리, 검풍, 전설)과 치명 비율(장비 문서 6장 '출처별 몫 계측에 전설 줄').
        /// withLegend가 false면 전설 피해를 분모와 치명 비율에서 뺀다(legend = 0). 효과별 몫은 LegendShares.
        /// </summary>
        public void Shares(out float basic, out float whirl, out float wave, out float legend, out float critRate, bool woodOnly, bool withLegend = true)
        {
            Tally(woodOnly, withLegend, out long b, out long w, out long s, out long l, out _, out _, out int count, out int crits);
            float total = Mathf.Max(1, b + w + s + l);
            basic = b / total;
            whirl = w / total;
            wave = s / total;
            legend = l / total;
            critRate = count > 0 ? (float)crits / count : 0f;
        }

        /// <summary>
        /// 최근 30초 출처별 몫에 출혈(도끼)·환경(벽 박기 추가 피해 등) 줄을 더한 판(전투·보스·무기 다듬기 1차). 여섯 몫의 합 = 1.
        /// 출혈·환경은 치명이 없어 치명 비율의 분모에 넣지 않는다(위 판과 같은 치명 비율).
        /// </summary>
        public void Shares(out float basic, out float whirl, out float wave, out float legend, out float bleed, out float environment, out float critRate, bool woodOnly)
        {
            Tally(woodOnly, true, out long b, out long w, out long s, out long l, out long bl, out long env, out int count, out int crits);
            float total = Mathf.Max(1, b + w + s + l + bl + env);
            basic = b / total;
            whirl = w / total;
            wave = s / total;
            legend = l / total;
            bleed = bl / total;
            environment = env / total;
            critRate = count > 0 ? (float)crits / count : 0f;
        }

        /// <summary>최근 30초 출처별 피해 합. 치명 비율 분모(count)는 기본·회오리·검풍(·전설)만 센다(출혈·환경은 치명이 없음).</summary>
        void Tally(bool woodOnly, bool withLegend, out long b, out long w, out long s, out long l, out long bleed, out long env, out int count, out int crits)
        {
            float from = Time.time - DummyWindow;
            b = w = s = l = bleed = env = 0;
            count = crits = 0;
            for (int i = _hits.Count - 1; i >= 0 && _hits[i].Time >= from; i--)
            {
                var h = _hits[i];
                if (woodOnly ? !h.Wood : h.Dummy) continue;
                if (!withLegend && h.Source == DamageSource.Legend) continue;
                switch (h.Source)
                {
                    case DamageSource.Bleed: bleed += h.Amount; continue;
                    case DamageSource.Environment: env += h.Amount; continue;
                    case DamageSource.Basic: b += h.Amount; break;
                    case DamageSource.Whirlwind: w += h.Amount; break;
                    case DamageSource.SwordWave: s += h.Amount; break;
                    case DamageSource.Legend: l += h.Amount; break;
                }
                count++;
                if (h.Crit) crits++;
            }
        }
    }
}
