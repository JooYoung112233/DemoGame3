using System.Collections;
using System.Collections.Generic;
using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>3차 시험장 마주침 구성(초안 3-1 마주침 틀). 정리되면 잠시 뒤 같은 무리를 다시 놓는다.</summary>
    public sealed class EncounterSpec
    {
        public string Name;
        public int Rats;
        public int Boars;
        public int Archers;
        public bool Nest;
        public bool EliteBoar;
        public EliteAffix Affixes;
        /// <summary>잠든 채로 놓고 플레이어를 반대편에 세운다(기습 연습).</summary>
        public bool Sleeping;
    }

    /// <summary>
    /// 전투 시험장 소환기.
    /// 계속 모드(M0a): 종류별 목표 수를 계속 유지한다. 바닥에 소환 표시가 0.8초 나타난 뒤 적이 나오고, 플레이어와 최소 4유닛 떨어진 곳에만 나온다.
    /// 마주침 모드(3차): 정해진 무리를 한 번에 놓고, 모두 쓰러지면 '마주침 정리' 뒤 2.5초에 다시 놓는다.
    /// 허수아비 모드에서는 나무 허수아비 1개와 쥐 허수아비 8개를 세운다.
    /// </summary>
    public sealed class EnemySpawner : MonoBehaviour
    {
        const float MarkerTime = 0.8f;
        const float MinPlayerDistance = 4f;
        const float DummyRespawn = 3f;
        const float EncounterRespawn = 2.5f;
        const int MaxRats = 24;
        const int KindCount = 3;

        static EnemySpawner _instance;

        public Rect Inner;
        public int Floor = 1;
        /// <summary>한 마리가 쓰러진 뒤 같은 종류가 다시 나오기까지(초). 굴쥐 떼는 짧게 둔다.</summary>
        public float RespawnDelay = 1.0f;
        /// <summary>굴쥐를 한 자리에 3~5마리씩 무리로 소환한다.</summary>
        public bool PackSpawn;
        public bool DummyMode { get; private set; }
        public EncounterSpec Encounter { get; private set; }
        public int EncounterGroup { get; private set; } = -1;
        public int EncountersCleared { get; private set; }

        readonly int[] _target = new int[KindCount];
        readonly int[] _alive = new int[KindCount];
        readonly int[] _pending = new int[KindCount];
        readonly float[] _nextAllowed = new float[KindCount];
        readonly List<GameObject> _markers = new List<GameObject>();
        readonly List<Enemy> _group = new List<Enemy>();
        int _generation;
        int _groupCounter;
        float _encounterRespawnAt = -1f;

        static readonly Vector2 WoodDummySpot = new Vector2(6f, 0f);
        static readonly Vector2[] RatDummySpots =
        {
            new Vector2(-8f, 1.5f), new Vector2(-6.8f, 1.5f), new Vector2(-5.6f, 1.5f), new Vector2(-4.4f, 1.5f),
            new Vector2(-8f, -1.5f), new Vector2(-6.8f, -1.5f), new Vector2(-5.6f, -1.5f), new Vector2(-4.4f, -1.5f),
        };

        public int Target(MonsterKind kind) => (int)kind < KindCount ? _target[(int)kind] : 0;
        public int Alive(MonsterKind kind) => (int)kind < KindCount ? _alive[(int)kind] : 0;

        void Awake() => _instance = this;

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>둥지·무리 거느린 정예가 부른 굴쥐를 지금 마주침에 넣는다(정리 판정에 포함).</summary>
        public static void Track(Enemy enemy)
        {
            if (!_instance || !enemy || _instance.Encounter == null) return;
            if (enemy.GroupId != _instance.EncounterGroup) return;
            _instance._group.Add(enemy);
        }

        public void SetTargets(int rats, int boars, int archers)
        {
            Encounter = null;
            _target[(int)MonsterKind.Rat] = Mathf.Clamp(rats, 0, MaxRats);
            _target[(int)MonsterKind.Boar] = Mathf.Clamp(boars, 0, 6);
            _target[(int)MonsterKind.Archer] = Mathf.Clamp(archers, 0, 6);
        }

        public void SetTarget(MonsterKind kind, int count)
        {
            if ((int)kind >= KindCount) return;
            _target[(int)kind] = Mathf.Clamp(count, 0, kind == MonsterKind.Rat ? MaxRats : 6);
        }

        /// <summary>적과 허수아비를 모두 지운다. 진행 중인 소환 표시도 취소한다.</summary>
        public void ClearAll()
        {
            _generation++;
            StopAllCoroutines();
            foreach (var m in _markers)
                if (m) Destroy(m);
            _markers.Clear();
            for (int i = Enemy.All.Count - 1; i >= 0; i--)
                if (Enemy.All[i]) Enemy.All[i].Remove();
            Enemy.All.Clear();
            AttackTokens.ResetStatics();
            StrongAttackSchedule.ResetStatics();
            // 지난 구성의 덫·날아가는 화살·회복 구슬이 다음 구성과 기록에 섞이지 않게 함께 지운다.
            SpikeTrap.DestroyAll();
            Arrow.DiscardAll();
            HealOrb.DestroyAll();
            _group.Clear();
            _encounterRespawnAt = -1f;
            for (int i = 0; i < KindCount; i++)
            {
                _alive[i] = 0;
                _pending[i] = 0;
                _nextAllowed[i] = 0f;
            }
        }

        public void SetDummyMode(bool on)
        {
            ClearAll();
            Encounter = null;
            DummyMode = on;
            if (!on) return;
            SpawnWoodDummy();
            for (int i = 0; i < RatDummySpots.Length; i++) SpawnRatDummy(RatDummySpots[i]);
        }

        /// <summary>3차 마주침 모드로 바꾸고 바로 무리를 놓는다.</summary>
        public void StartEncounters(EncounterSpec spec)
        {
            ClearAll();
            DummyMode = false;
            for (int i = 0; i < KindCount; i++) _target[i] = 0;
            Encounter = spec;
            EncountersCleared = 0;
            SpawnEncounter();
        }

        /// <summary>지금 마주침을 처음부터 다시 놓는다(규칙·층을 바꿨을 때).</summary>
        public void RestartEncounter()
        {
            if (Encounter == null) return;
            var spec = Encounter;
            ClearAll();
            Encounter = spec;
            SpawnEncounter();
        }

        void Update()
        {
            if (DummyMode) return;
            if (!PlayerController.Instance) return;
            if (Encounter != null)
            {
                UpdateEncounter();
                return;
            }
            for (int k = 0; k < KindCount; k++)
            {
                int missing = _target[k] - _alive[k] - _pending[k];
                if (missing <= 0) continue;
                if (Time.time < _nextAllowed[k]) continue;
                if (!TryFindSpawnPoint(out var pos)) continue;
                if (PackSpawn && k == (int)MonsterKind.Rat && missing >= 3)
                {
                    // 무리: 한 자리 둘레에 3~5마리.
                    int count = Mathf.Min(missing, Random.Range(3, 6));
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 p = i == 0 ? pos : pos + Random.insideUnitCircle * 1.1f;
                        if (i > 0 && (!Inner.Contains(p) || Physics2D.OverlapCircle(p, 0.4f, Layers.WallMask))) p = pos;
                        StartCoroutine(SpawnAfterMarker((MonsterKind)k, p, _generation));
                    }
                    _nextAllowed[k] = Time.time + 0.35f;
                    continue;
                }
                _nextAllowed[k] = Time.time + 0.15f;
                StartCoroutine(SpawnAfterMarker((MonsterKind)k, pos, _generation));
            }
        }

        void UpdateEncounter()
        {
            if (_encounterRespawnAt > 0f)
            {
                if (Time.time >= _encounterRespawnAt)
                {
                    _encounterRespawnAt = -1f;
                    SpawnEncounter();
                }
                return;
            }
            _group.RemoveAll(e => !e);
            foreach (var e in _group)
                if (!e.Dead) return;
            // 모두 쓰러짐: 마주침 정리(2차 7-6 '방 정리' 연출을 무리 단위로).
            EncountersCleared++;
            CombatEvents.RaiseEncounterCleared(EncounterGroup);
            var player = PlayerController.Instance;
            WorldOverlay.Text((player ? player.Position : Vector2.zero) + Vector2.up * 1.4f, "마주침 정리", Palette.HealthBar);
            _group.Clear();
            _encounterRespawnAt = Time.time + EncounterRespawn;
        }

        void SpawnEncounter()
        {
            var spec = Encounter;
            var player = PlayerController.Instance;
            EncounterGroup = ++_groupCounter;
            Vector2 center;
            if (spec.Sleeping)
            {
                // 기습 연습: 무리는 오른쪽에서 등을 돌리고 자고, 플레이어는 왼쪽에서 시작한다.
                center = new Vector2(7f, 0f);
                if (player) player.Teleport(new Vector2(-9f, 0f));
            }
            else
            {
                center = FarthestPoint(player ? player.Position : Vector2.zero);
            }

            var members = new List<Enemy>();
            if (spec.Nest)
            {
                members.Add(Create(MonsterKind.Nest, Floor, center));
            }
            if (spec.EliteBoar)
            {
                var elite = Create(MonsterKind.Boar, Floor, center);
                elite.MakeElite(spec.Affixes);
                members.Add(elite);
                if ((spec.Affixes & EliteAffix.Pack) != 0)
                {
                    var pack = new List<Enemy>();
                    for (int i = 0; i < 4; i++)
                    {
                        var rat = Create(MonsterKind.Rat, Floor, SafeAround(center, 1.6f, i, 4));
                        rat.NoReward = true;
                        pack.Add(rat);
                        members.Add(rat);
                    }
                    elite.gameObject.AddComponent<PackLeader>().Bind(elite, pack);
                }
            }
            // 앞잡이(멧돼지)는 플레이어 쪽, 뒷줄(궁수)은 반대쪽(3차 초안 3-1 역할 조합).
            Vector2 away = player ? center - player.Position : Vector2.right;
            away = away.sqrMagnitude > 0.01f ? away.normalized : Vector2.right;
            for (int i = 0; i < spec.Boars; i++) members.Add(Create(MonsterKind.Boar, Floor, SafeAround(center - away * 1.2f, 0.9f, i, Mathf.Max(1, spec.Boars))));
            for (int i = 0; i < spec.Archers; i++) members.Add(Create(MonsterKind.Archer, Floor, SafeAround(center + away * 2.6f, 1.6f, i, Mathf.Max(1, spec.Archers))));
            for (int i = 0; i < spec.Rats; i++) members.Add(Create(MonsterKind.Rat, Floor, SafeAround(center - away * 0.4f, 1.4f, i, Mathf.Max(1, spec.Rats))));

            foreach (var e in members)
            {
                e.GroupId = EncounterGroup;
                if (spec.Sleeping) e.Sleep(Vector2.right);
                _group.Add(e);
            }
            CombatEvents.RaiseEncounterSpawned(EncounterGroup, spec.EliteBoar);
        }

        /// <summary>방 안 후보 지점 중 플레이어에게서 가장 먼 곳(최소 4유닛).</summary>
        Vector2 FarthestPoint(Vector2 from)
        {
            Vector2[] candidates =
            {
                new Vector2(7f, 0f), new Vector2(-7f, 0f), new Vector2(7f, 3.5f), new Vector2(-7f, -3.5f),
                new Vector2(0f, -4f), new Vector2(-3f, 3.5f), new Vector2(7f, -3.8f), new Vector2(-8f, 3.5f),
            };
            Vector2 best = candidates[0];
            float bestD = -1f;
            foreach (var c in candidates)
            {
                if (Physics2D.OverlapCircle(c, 1.2f, Layers.WallMask)) continue;
                float d = (c - from).sqrMagnitude;
                if (d > bestD)
                {
                    bestD = d;
                    best = c;
                }
            }
            return best;
        }

        /// <summary>중심 둘레에 고르게 놓되, 벽·방 밖이면 중심 쪽으로 당긴다.</summary>
        Vector2 SafeAround(Vector2 center, float radius, int index, int count)
        {
            float a = (index + 0.5f) / count * Mathf.PI * 2f + 0.3f;
            for (float r = radius; r >= 0f; r -= 0.4f)
            {
                Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                Rect inner = new Rect(Inner.xMin + 0.8f, Inner.yMin + 0.8f, Inner.width - 1.6f, Inner.height - 1.6f);
                if (inner.Contains(p) && !Physics2D.OverlapCircle(p, 0.45f, Layers.WallMask)) return p;
            }
            return center;
        }

        IEnumerator SpawnAfterMarker(MonsterKind kind, Vector2 pos, int generation)
        {
            int k = (int)kind;
            _pending[k]++;
            var marker = new GameObject("SpawnMarker");
            marker.transform.position = pos;
            var sr = marker.AddComponent<SpriteRenderer>();
            sr.sprite = ShapeSprites.Ring;
            sr.color = Palette.SpawnMarker;
            sr.sortingOrder = -70;
            _markers.Add(marker);
            float diameter = MonsterRule.Of(kind).Diameter;
            float t = 0f;
            while (t < MarkerTime)
            {
                t += Time.deltaTime;
                if (marker) marker.transform.localScale = Vector3.one * Mathf.Lerp(diameter * 2.2f, diameter, t / MarkerTime);
                yield return null;
            }
            if (generation != _generation) yield break;
            _markers.Remove(marker);
            if (marker) Destroy(marker);
            _pending[k]--;
            var enemy = Create(kind, Floor, pos);
            _alive[k]++;
            enemy.Removed += e =>
            {
                if (generation != _generation) return;
                _alive[k] = Mathf.Max(0, _alive[k] - 1);
                _nextAllowed[k] = Time.time + RespawnDelay;
            };
        }

        public static Enemy Create(MonsterKind kind, int floor, Vector2 pos)
        {
            switch (kind)
            {
                case MonsterKind.Rat: return Enemy.Spawn<RatBrain>(kind, floor, pos, ShapeSprites.Circle, Palette.Rat, false);
                case MonsterKind.Boar: return Enemy.Spawn<BoarBrain>(kind, floor, pos, ShapeSprites.Square, Palette.Boar, true);
                case MonsterKind.Nest: return Enemy.Spawn<NestBrain>(kind, floor, pos, ShapeSprites.Circle, Palette.Nest, false);
                default: return Enemy.Spawn<ArcherBrain>(kind, floor, pos, ShapeSprites.Triangle, Palette.Archer, true);
            }
        }

        void SpawnWoodDummy()
        {
            // 지름 1.1은 멧돼지와 같아 그 종류 틀을 빌린다. 허수아비라 처치·계측에서 빠진다.
            var dummy = Enemy.Spawn<DummyBrain>(MonsterKind.Boar, Floor, WoodDummySpot, ShapeSprites.Square, Palette.WoodDummy, false);
            dummy.MakeDummy(true);
            dummy.name = "나무 허수아비";
        }

        void SpawnRatDummy(Vector2 spot)
        {
            var dummy = Enemy.Spawn<DummyBrain>(MonsterKind.Rat, Floor, spot, ShapeSprites.Circle, Palette.RatDummy, false);
            dummy.MakeDummy(false);
            dummy.name = "쥐 허수아비";
            int generation = _generation;
            dummy.Removed += _ =>
            {
                if (generation == _generation && DummyMode && this) StartCoroutine(RespawnRatDummy(spot, generation));
            };
        }

        IEnumerator RespawnRatDummy(Vector2 spot, int generation)
        {
            float t = 0f;
            while (t < DummyRespawn)
            {
                t += Time.deltaTime;
                yield return null;
            }
            if (generation == _generation && DummyMode) SpawnRatDummy(spot);
        }

        bool TryFindSpawnPoint(out Vector2 pos)
        {
            var player = PlayerController.Instance;
            Vector2 playerPos = player ? player.Position : Vector2.zero;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                pos = new Vector2(Random.Range(Inner.xMin + 1f, Inner.xMax - 1f), Random.Range(Inner.yMin + 1f, Inner.yMax - 1f));
                if ((pos - playerPos).sqrMagnitude < MinPlayerDistance * MinPlayerDistance) continue;
                if (Physics2D.OverlapCircle(pos, 0.9f, Layers.WallMask)) continue;
                bool crowded = false;
                foreach (var e in Enemy.All)
                    if (e && (e.Position - pos).sqrMagnitude < 0.8f * 0.8f)
                    {
                        crowded = true;
                        break;
                    }
                if (!crowded) return true;
            }
            pos = default;
            return false;
        }
    }
}
