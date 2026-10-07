using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 3차 초안 2-6 칸에 묶인 무리(잠·먹는 중·깨어남·감지 경계·놓아주기·큰 소리·정리·다시 놓기).
    /// 무리는 처음부터 자기 칸에 놓여 있고(잠·먹는 중), 감지·추격·돌아가기의 경계는 그 칸 안쪽 + 문 1유닛이다(Enemy.Territory).
    /// 깨어난 적이 모두 쓰러지면 '마주침 정리'. 같은 원정 안에서는 다시 생기지 않고, 원정을 다시 시작하면 처음처럼 다시 놓는다.
    /// 쓰러져 다시 서면 정리되지 않은 깨어 있는 무리는 제자리·가득 찬 체력으로 돌아가 다시 쉰다(3차 초안 2-7 M0b판).
    /// 큰 소리(곡괭이·광맥·금고, 반경 12)는 가장 가까운 잠든 무리 하나를 깨워 소리 쪽 문 안쪽에서 기다리게 한다.
    /// 1-2층 탐험 맛 1차: 정예 무리는 첫 돌충이를 '단단한 정예'로 놓고(4-1·4-2), 순찰 무리는 이웃 칸까지 잠든 채 오가며(4-3, PatrolWalker),
    /// 문틈 엿듣기(4-4, DoorListen)를 이 물체에 붙인다. 보는 칸 밖 무리의 '우적'은 띄우지 않는다(시야와 문 1차 10-7 H2).
    /// </summary>
    public sealed class CellEncounters : MonoBehaviour
    {
        /// <summary>무리 id 시작 값. 전투 시험장 마주침 번호(1부터)와 겹치지 않게 1000부터 쓴다.</summary>
        public const int FirstGroupId = 1000;
        /// <summary>졸개 굴쥐는 강한 적 둘레 반경 1.2~1.8 고리에 놓는다.</summary>
        const float RatRingMin = 1.2f;
        const float RatRingMax = 1.8f;
        /// <summary>멧돼지가 여럿이면 가운데 둘레 이 반경에 놓는다.</summary>
        const float BoarSpread = 1.1f;
        /// <summary>궁수는 무리 뒤쪽(바라보는 반대편) 이 거리에 놓는다(뒷줄, 3차 초안 3-1 역할 조합).</summary>
        const float ArcherBack = 1.8f;
        const float ArcherSpread = 1.3f;
        /// <summary>놓을 자리가 벽·기둥과 떨어져야 하는 거리.</summary>
        const float WallClearance = 0.45f;
        /// <summary>무리끼리 겹쳐 놓지 않는 거리.</summary>
        const float MemberSpacing = 0.8f;
        /// <summary>칸 안쪽 가장자리에서 띄우는 여유.</summary>
        const float InnerMargin = 0.8f;
        /// <summary>큰 소리에 깬 무리가 기다리는 자리: 소리 쪽 문에서 칸 안쪽으로 이만큼.</summary>
        const float GuardDepth = 2f;
        const float GuardLateral = 1.2f;
        const float GuardRowStep = 1.2f;
        /// <summary>먹는 무리 '냠' 표시: 이 거리 안에 플레이어가 있을 때만(어둠 속 소리 단서).</summary>
        const float NomHearDistance = 10f;
        const float ClearTextNearPlayer = 15f;

        static readonly Color NomColor = new Color(0.95f, 0.86f, 0.62f, 0.9f);

        public static CellEncounters Instance { get; private set; }

        /// <summary>
        /// 바로 가기 시험 메뉴 '적 없음'(TestLaunchSession만 켠다): 켜져 있으면 SpawnAll이 무리·둥지를 놓지 않는다(보스방 오우거는 BossArena가 따로 놓음).
        /// 장면을 바꿔도 남고(SceneStatics.Reset이 비우지 않음) 플레이를 새로 시작하면 꺼진다.
        /// </summary>
        public static bool SkipSpawnForTest { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => SkipSpawnForTest = false;

        /// <summary>자리 표시 하나에서 나온 무리(멧돼지·궁수·굴쥐 또는 둥지).</summary>
        sealed class Encounter
        {
            public int Id;
            public DungeonCell Cell;
            public CellFeature Feature;
            public Vector2 Pos;
            public bool IsNest;
            /// <summary>처음 놓은 무리(둥지가 부른 굴쥐는 Enemy.All에서 같은 GroupId로 찾는다).</summary>
            public readonly List<Enemy> Members = new List<Enemy>();
            public bool Spawned;
            public bool Cleared;
            /// <summary>이번 싸움의 GroupAwake를 알렸는가. 무리가 다시 모두 쉬면 비운다.</summary>
            public bool AwakeRaised;
            public Vector2 LastDeath;
            public bool HasDeath;
            public float NomTimer;
            /// <summary>순찰 길을 붙여 놓았다(1-2층 탐험 맛 1차 4-3). 길을 못 만든 순찰 무리는 거짓(먹는 중으로 둠).</summary>
            public bool Patrolling;
            /// <summary>정예 돌충이를 놓았다(1-2층 탐험 맛 1차 4-1·4-2).</summary>
            public bool Elite;
        }

        readonly List<Encounter> _groups = new List<Encounter>();
        readonly Dictionary<int, Encounter> _byId = new Dictionary<int, Encounter>();
        readonly List<Enemy> _scratch = new List<Enemy>();
        int _nextId = FirstGroupId;

        /// <summary>놓인 무리 수(둥지 포함).</summary>
        public int GroupCount => _groups.Count;

        /// <summary>이번 원정에서 정리한 무리 수.</summary>
        public int ClearedCount
        {
            get
            {
                int n = 0;
                foreach (var g in _groups)
                    if (g.Cleared) n++;
                return n;
            }
        }

        /// <summary>이 무리 id를 이번 원정에서 정리했는가.</summary>
        public bool IsCleared(int groupId) => _byId.TryGetValue(groupId, out var g) && g.Cleared;

        /// <summary>이 무리 id가 칸에 묶인 무리인가.</summary>
        public bool IsCellGroup(int groupId) => _byId.ContainsKey(groupId);

        /// <summary>순찰 길을 붙여 놓은 무리 수(1-2층 탐험 맛 1차 4-3, 확인용).</summary>
        public int PatrolGroupCount
        {
            get
            {
                int n = 0;
                foreach (var g in _groups)
                    if (g.Patrolling) n++;
                return n;
            }
        }

        /// <summary>정예 돌충이를 놓은 무리 수(1-2층 탐험 맛 1차 4-1·4-2, 확인용).</summary>
        public int EliteGroupCount
        {
            get
            {
                int n = 0;
                foreach (var g in _groups)
                    if (g.Elite) n++;
                return n;
            }
        }

        void Awake()
        {
            Instance = this;
            // 1-2층 탐험 맛 1차 4-4 문틈 엿듣기: 같은 물체에 붙인다(웅크려 문 앞 1초 → 건너편 기척 한 줄).
            if (!GetComponent<DoorListen>()) gameObject.AddComponent<DoorListen>();
            // DungeonRoot.Awake가 사건 구독을 비운 뒤 이 컴포넌트를 붙이므로 여기서 구독한다.
            CombatEvents.EnemyWoke += OnEnemyWoke;
            CombatEvents.EnemyKilled += OnEnemyKilled;
            DungeonEvents.PlayerRespawned += OnPlayerRespawned;
            DungeonEvents.ExpeditionRestarted += OnExpeditionRestarted;
            DungeonEvents.Noise += OnNoise;
        }

        void OnDestroy()
        {
            CombatEvents.EnemyWoke -= OnEnemyWoke;
            CombatEvents.EnemyKilled -= OnEnemyKilled;
            DungeonEvents.PlayerRespawned -= OnPlayerRespawned;
            DungeonEvents.ExpeditionRestarted -= OnExpeditionRestarted;
            DungeonEvents.Noise -= OnNoise;
            if (Instance == this) Instance = null;
        }

        /// <summary>지도를 만들 때 무리·둥지 자리 표시마다 부른다.</summary>
        public void AddGroup(DungeonCell cell, CellFeature f, Vector2 pos)
        {
            if (cell == null || f == null) return;
            if (f.Kind != FeatureKind.Group && f.Kind != FeatureKind.Nest) return;
            var g = new Encounter
            {
                Id = _nextId++,
                Cell = cell,
                Feature = f,
                Pos = pos,
                IsNest = f.Kind == FeatureKind.Nest,
                NomTimer = Random.Range(0.5f, 2.5f),
            };
            _groups.Add(g);
            _byId[g.Id] = g;
        }

        /// <summary>플레이어를 만든 뒤 한 번 부른다(모든 무리를 놓음). 정리한 무리와 이미 놓은 무리는 건너뛴다.</summary>
        public void SpawnAll()
        {
            // 시험 메뉴 '적 없음': 무리·둥지를 놓지 않는다(보스방 오우거는 BossArena가 따로 놓음).
            if (SkipSpawnForTest) return;
            foreach (var g in _groups)
                if (!g.Spawned && !g.Cleared) Spawn(g);
        }

        // ───────────────────────── 놓기 ─────────────────────────

        void Spawn(Encounter g)
        {
            int floor = DungeonRoot.Instance ? DungeonRoot.Instance.Floor : 1;
            var f = g.Feature;
            Vector2 facing = FacingOf(f.FacingDeg);
            // 1-2층 탐험 맛 1차 4-3: 순찰 무리는 제 칸과 이웃 칸(이웃이 없으면 제 칸 안 두 열린 문 사이)을 오가고, 경계는 두 칸을 합친 네모다.
            // 길을 못 만들면 '먹는 중'으로 둔다(FloorSpice가 순찰할 곳 없는 무리를 먹는 중으로 바꾸는 것과 같은 뜻).
            PatrolPath path = null;
            g.Patrolling = !g.IsNest && f.State == GroupState.Patrol && TryPatrolPath(g, out path);
            Rect territory = g.Patrolling ? PatrolTerritory(g.Cell, path.To) : g.Cell.Territory;
            Rect inner = Inset(g.Cell.Inner, InnerMargin);
            var used = new List<Vector2>();
            g.Members.Clear();
            g.Cleared = false;
            g.AwakeRaised = false;
            g.HasDeath = false;
            g.Elite = false;

            if (g.IsNest)
            {
                Vector2 spot = FindSpot(g.Pos, g.Pos, inner, used, 0.8f);
                var nest = EnemySpawner.Create(MonsterKind.Nest, floor, spot);
                Prepare(g, nest, spot, facing, territory);
            }
            else
            {
                // 앞잡이(멧돼지)는 가운데, 뒷줄(궁수)은 바라보는 반대편, 졸개(굴쥐)는 둘레 고리(3차 초안 3-1 역할 조합).
                for (int i = 0; i < f.Boars; i++)
                {
                    Vector2 want = f.Boars == 1 ? g.Pos : g.Pos + Around(i, f.Boars, 0.4f) * BoarSpread;
                    Vector2 spot = FindSpot(want, g.Pos, inner, used, 0.6f);
                    var boar = EnemySpawner.Create(MonsterKind.Boar, floor, spot);
                    // 1-2층 탐험 맛 1차 4-1·4-2: 정예 무리는 첫 돌충이를 '단단한 정예'로 만든다(체력·공격·보상·숨소리는 Enemy 정예 규칙 그대로).
                    // '무리 거느린' 정예는 쓰지 않는다(부른 굴쥐가 칸 경계를 받지 않아 칸 밖까지 쫓아옴, PackLeader).
                    if (i == 0 && f.Elite)
                    {
                        boar.MakeElite(EliteAffix.Hardened);
                        g.Elite = true;
                    }
                    Prepare(g, boar, spot, facing, territory);
                }
                Vector2 side = new Vector2(-facing.y, facing.x);
                for (int i = 0; i < f.Archers; i++)
                {
                    float lateral = f.Archers == 1 ? 0f : (i - (f.Archers - 1) * 0.5f) * ArcherSpread;
                    Vector2 want = g.Pos - facing * (f.Boars > 0 ? ArcherBack : 0f) + side * lateral;
                    Vector2 spot = FindSpot(want, g.Pos, inner, used, 0.5f);
                    Prepare(g, EnemySpawner.Create(MonsterKind.Archer, floor, spot), spot, facing, territory);
                }
                // 굴쥐만 있는 무리는 첫 마리를 가운데에 두고 나머지를 고리에 놓는다.
                bool centerRat = f.Boars + f.Archers == 0;
                int ringCount = Mathf.Max(1, centerRat ? f.Rats - 1 : f.Rats);
                float start = Random.Range(0f, Mathf.PI * 2f);
                for (int i = 0; i < f.Rats; i++)
                {
                    Vector2 want;
                    if (centerRat && i == 0)
                    {
                        want = g.Pos;
                    }
                    else
                    {
                        int k = centerRat ? i - 1 : i;
                        float a = start + (k + 0.5f) / ringCount * Mathf.PI * 2f + Random.Range(-0.25f, 0.25f);
                        want = g.Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(RatRingMin, RatRingMax);
                    }
                    Vector2 spot = FindRatSpot(want, g.Pos, inner, used);
                    Prepare(g, EnemySpawner.Create(MonsterKind.Rat, floor, spot), spot, facing, territory);
                }
            }
            if (g.Patrolling) StartPatrol(g, path);
            g.Spawned = true;
        }

        /// <summary>무리 번호·칸 경계·제자리를 붙이고 처음 모습(잠·먹는 중·순찰)으로 둔다.</summary>
        void Prepare(Encounter g, Enemy e, Vector2 home, Vector2 facing, Rect territory)
        {
            e.GroupId = g.Id;
            e.BindToTerritory(territory, home, facing);
            // 순찰(1-2층 탐험 맛 1차 4-3)은 잠든 채로 놓고, 다 놓은 뒤 길을 붙인다(StartPatrol). 길을 못 만든 순찰은 '먹는 중'과 같이 둔다.
            e.IsPatrolling = g.Patrolling;
            if (g.Patrolling || g.Feature.State == GroupState.Sleep || g.IsNest) e.Sleep(facing);
            else e.Eat(facing);
            g.Members.Add(e);
        }

        // ───────────────────────── 순찰(1-2층 탐험 맛 1차 4-3) ─────────────────────────

        /// <summary>순찰 길에서 무리가 함께 쓰는 부분. 대원마다 제자리·끝점만 다르다.</summary>
        sealed class PatrolPath
        {
            /// <summary>오갈 이웃 칸(제 칸 순찰이면 제 칸).</summary>
            public DungeonCell To;
            /// <summary>제 칸 안 순찰(이웃 없음, 두 열린 문 사이).</summary>
            public bool InCell;
            /// <summary>먼저 지나는 문 안쪽 점(이웃 순찰: 내 칸 쪽 문 안쪽 2, 제 칸 순찰: 무리에 가까운 문 안쪽 2).</summary>
            public Vector2 NearIn;
            /// <summary>이웃 순찰: 이웃 쪽 문 안쪽 2.</summary>
            public Vector2 FarIn;
            /// <summary>제 칸 순찰: 끝 문 가운데와 그 문에서 칸 안쪽 방향(대원마다 GuardSpot 자리로 나눠 선다).</summary>
            public Vector2 EndDoor;
            public Vector2 EndInward;
        }

        /// <summary>순찰 무리의 칸 경계(쫓기·놓아주기): 두 칸 경계(칸 + 문 1유닛)를 합친 네모. 같은 칸이면 그 칸 경계.</summary>
        public static Rect PatrolTerritory(DungeonCell a, DungeonCell b)
        {
            if (a == null) return b != null ? b.Territory : default;
            Rect ra = a.Territory;
            if (b == null || b == a) return ra;
            Rect rb = b.Territory;
            return Rect.MinMaxRect(Mathf.Min(ra.xMin, rb.xMin), Mathf.Min(ra.yMin, rb.yMin), Mathf.Max(ra.xMax, rb.xMax), Mathf.Max(ra.yMax, rb.yMax));
        }

        /// <summary>
        /// 순찰 길에서 무리가 함께 쓰는 부분을 만든다. 이웃 = World.Find(PatrolCell)(빈 글이면 제 칸).
        /// 이웃 순찰은 두 칸 사이에 열린 길이 있어야 하고, 제 칸 순찰은 열린 문이 둘 이상이어야 한다(가장 멀리 떨어진 두 문).
        /// 문 자리는 비튼 실제 자리(DungeonEdge.DoorCenter, 시야와 문 1차 2-5)를 읽는다. 못 만들면 false.
        /// </summary>
        bool TryPatrolPath(Encounter g, out PatrolPath path)
        {
            path = null;
            var world = DungeonRoot.Instance ? DungeonRoot.Instance.World : null;
            if (world == null || g.Cell == null) return false;
            string id = g.Feature.PatrolCell;
            var to = string.IsNullOrEmpty(id) ? g.Cell : world.Find(id);
            if (to == null) return false;
            if (to != g.Cell)
            {
                DungeonEdge door = null;
                foreach (var e in g.Cell.Edges)
                    if (e.Kind == EdgeKind.Open && e.Other(g.Cell) == to)
                    {
                        door = e;
                        break;
                    }
                if (door == null) return false;
                Vector2 inward = Inward(door.SideFrom(g.Cell));
                path = new PatrolPath
                {
                    To = to,
                    NearIn = DoorInPoint(door.DoorCenter, inward),
                    FarIn = DoorInPoint(door.DoorCenter, -inward),
                };
                return true;
            }
            // 제 칸 순찰: 열린 문 가운데 가장 멀리 떨어진 두 곳. 무리 자리에 가까운 문을 먼저 지나 먼 문 안쪽에서 둘러본다.
            DungeonEdge first = null;
            DungeonEdge end = null;
            float best = -1f;
            var edges = g.Cell.Edges;
            for (int i = 0; i < edges.Count; i++)
            {
                if (edges[i].Kind != EdgeKind.Open) continue;
                for (int j = i + 1; j < edges.Count; j++)
                {
                    if (edges[j].Kind != EdgeKind.Open) continue;
                    float d = (edges[i].DoorCenter - edges[j].DoorCenter).sqrMagnitude;
                    if (d <= best) continue;
                    best = d;
                    first = edges[i];
                    end = edges[j];
                }
            }
            if (first == null || end == null) return false;
            if ((end.DoorCenter - g.Pos).sqrMagnitude < (first.DoorCenter - g.Pos).sqrMagnitude) (first, end) = (end, first);
            path = new PatrolPath
            {
                To = g.Cell,
                InCell = true,
                NearIn = DoorInPoint(first.DoorCenter, Inward(first.SideFrom(g.Cell))),
                EndDoor = end.DoorCenter,
                EndInward = Inward(end.SideFrom(g.Cell)),
            };
            return true;
        }

        /// <summary>문 가운데에서 inward 쪽으로 PatrolWalker.DoorInset(2) 들어간 순찰 길 점. 벽·기둥에 걸리면 조금 더 안쪽으로.</summary>
        static Vector2 DoorInPoint(Vector2 doorCenter, Vector2 inward)
        {
            for (int k = 0; k < 4; k++)
            {
                Vector2 p = doorCenter + inward * (PatrolWalker.DoorInset + k * 0.6f);
                if (!Physics2D.OverlapCircle(p, 0.4f, Layers.WallMask)) return p;
            }
            return doorCenter + inward * PatrolWalker.DoorInset;
        }

        /// <summary>
        /// 대원마다 순찰 길을 붙인다. 길 = [제자리, 내 칸 쪽 문 안쪽 2, 이웃 쪽 문 안쪽 2, 이웃 가운데 + (제자리 − 무리 자리)]
        /// (제 칸 순찰이면 [제자리, 가까운 문 안쪽 2, 먼 문 안쪽 2 둘레 대원 자리]). 끝점은 벽·기둥·다른 대원 끝점을 비켜 고른다.
        /// 걸음 = 가장 느린 대원 종류 속도 × 적 걸음 배율(Tuning.EnemyMoveScale) × 0.5. 첫 문에 가까운 대원부터 0.7초씩 늦게 출발해 문을 한 줄로 지난다.
        /// </summary>
        void StartPatrol(Encounter g, PatrolPath path)
        {
            var members = new List<Enemy>();
            float slowest = float.MaxValue;
            foreach (var m in g.Members)
            {
                if (!m || m.Dead) continue;
                members.Add(m);
                slowest = Mathf.Min(slowest, m.BaseMoveSpeed);
            }
            if (members.Count == 0) return;
            float speed = slowest * Tuning.EnemyMoveScale * PatrolWalker.SpeedScale;
            Vector2 near = path.NearIn;
            members.Sort((x, y) => (x.HomePosition - near).sqrMagnitude.CompareTo((y.HomePosition - near).sqrMagnitude));
            Rect farInner = Inset(path.To.Inner, InnerMargin);
            Vector2 endLateral = new Vector2(-path.EndInward.y, path.EndInward.x);
            var farUsed = new List<Vector2>();
            var route = new List<Vector2>(4);
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                route.Clear();
                route.Add(m.HomePosition);
                route.Add(path.NearIn);
                if (path.InCell)
                {
                    route.Add(GuardSpot(path.EndDoor, path.EndInward, endLateral, i));
                }
                else
                {
                    route.Add(path.FarIn);
                    Vector2 want = path.To.Center + (m.HomePosition - g.Pos);
                    route.Add(FindSpot(want, path.To.Center, farInner, farUsed, m.Radius + 0.2f));
                }
                PatrolWalker.Attach(m, route, speed, i * PatrolWalker.StartGap);
            }
        }

        /// <summary>굴쥐 자리: 고리 위 다른 각도도 돌려 본다.</summary>
        static Vector2 FindRatSpot(Vector2 want, Vector2 center, Rect inner, List<Vector2> used)
        {
            if (SpotFree(want, inner, used, 0.4f) && !Physics2D.Linecast(center, want, Layers.WallMask))
            {
                used.Add(want);
                return want;
            }
            Vector2 offset = want - center;
            float radius = Mathf.Max(RatRingMin, offset.magnitude);
            float baseAngle = Mathf.Atan2(offset.y, offset.x);
            for (int k = 1; k <= 8; k++)
            {
                float a = baseAngle + (k % 2 == 1 ? 1f : -1f) * ((k + 1) / 2) * 0.45f;
                Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                if (!SpotFree(p, inner, used, 0.4f) || Physics2D.Linecast(center, p, Layers.WallMask)) continue;
                used.Add(p);
                return p;
            }
            return FindSpot(want, center, inner, used, 0.4f);
        }

        /// <summary>바라는 자리가 벽·기둥·다른 무리와 겹치거나 칸 밖이면 가운데 쪽으로 당겨 빈자리를 찾는다.</summary>
        static Vector2 FindSpot(Vector2 want, Vector2 center, Rect inner, List<Vector2> used, float spacing)
        {
            for (int step = 0; step <= 6; step++)
            {
                Vector2 p = Vector2.Lerp(want, center, step / 6f);
                if (!SpotFree(p, inner, used, spacing)) continue;
                used.Add(p);
                return p;
            }
            // 가운데마저 막혀 있으면 둘레를 돌며 찾는다.
            for (int ring = 1; ring <= 4; ring++)
            for (int k = 0; k < 8; k++)
            {
                float a = k / 8f * Mathf.PI * 2f;
                Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (ring * 0.7f);
                if (!SpotFree(p, inner, used, spacing)) continue;
                used.Add(p);
                return p;
            }
            Vector2 fallback = new Vector2(Mathf.Clamp(want.x, inner.xMin, inner.xMax), Mathf.Clamp(want.y, inner.yMin, inner.yMax));
            used.Add(fallback);
            return fallback;
        }

        static bool SpotFree(Vector2 p, Rect inner, List<Vector2> used, float spacing)
        {
            if (!inner.Contains(p)) return false;
            if (Physics2D.OverlapCircle(p, WallClearance, Layers.WallMask)) return false;
            float min = Mathf.Max(spacing, MemberSpacing);
            foreach (var u in used)
                if ((u - p).sqrMagnitude < min * min) return false;
            return true;
        }

        static Vector2 Around(int index, int count, float phase)
        {
            float a = (index + phase) / Mathf.Max(1, count) * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        static Vector2 FacingOf(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        static Rect Inset(Rect r, float margin) =>
            new Rect(r.xMin + margin, r.yMin + margin, Mathf.Max(0.1f, r.width - margin * 2f), Mathf.Max(0.1f, r.height - margin * 2f));

        // ───────────────────────── 매 프레임: 정리·다시 잠듦·'냠' ─────────────────────────

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var g in _groups)
            {
                if (!g.Spawned || g.Cleared) continue;
                CollectAlive(g, _scratch);
                if (_scratch.Count == 0)
                {
                    if (g.Members.Count > 0) ClearGroup(g);
                    continue;
                }
                bool anyAwake = false;
                foreach (var e in _scratch)
                    if (e.Aware || e.WakePending)
                    {
                        anyAwake = true;
                        break;
                    }
                if (dt > 0f && !anyAwake) UpdateNom(g, dt);
            }
        }

        /// <summary>먹는 무리는 가끔 '냠'(어둠 속에서 먼저 알아챌 소리 단서). 플레이어가 가까울 때만.</summary>
        void UpdateNom(Encounter g, float dt)
        {
            if (g.IsNest || g.Feature.State == GroupState.Sleep) return;
            g.NomTimer -= dt;
            if (g.NomTimer > 0f) return;
            g.NomTimer = Random.Range(2.2f, 3.6f);
            var player = PlayerController.Instance;
            if (!player) return;
            Enemy pick = null;
            int seen = 0;
            foreach (var m in g.Members)
            {
                if (!m || m.Dead || !m.IsEating) continue;
                if ((m.Position - player.Position).sqrMagnitude > NomHearDistance * NomHearDistance) continue;
                seen++;
                if (Random.Range(0, seen) == 0) pick = m;
            }
            // 시야와 문 1차 10-7 H2: 보는 칸 밖(벽 너머·문 건너편) 무리의 '우적'은 띄우지 않는다(검은 안개 위 글이 자리를 알리지 않게). 시야가 없거나 꺼졌으면 예전처럼.
            var vision = VisionSystem.Instance;
            if (pick && (!vision || !vision.VisionOn || vision.InViewCell(pick.Position)))
                WorldOverlay.Text(pick.Position + Vector2.up * (pick.Radius + 0.35f), "우적", NomColor);
        }

        /// <summary>무리 id가 같은 살아 있는 적(둥지가 부른 굴쥐 포함).</summary>
        static void CollectAlive(Encounter g, List<Enemy> into)
        {
            into.Clear();
            foreach (var e in Enemy.All)
                if (e && e.GroupId == g.Id && !e.Dead) into.Add(e);
        }

        /// <summary>깨어난 적이 모두 쓰러짐: '마주침 정리'(2차 7-6 '방 정리' 연출을 무리 단위로, 3차 초안 2-6).</summary>
        void ClearGroup(Encounter g)
        {
            g.Cleared = true;
            if (!g.AwakeRaised)
            {
                // 한 방 기습 처치처럼 깨는 알림 없이 끝난 싸움도 시작을 한 번 알린다(기록·화면이 짝을 맞추게).
                g.AwakeRaised = true;
                DungeonEvents.RaiseGroupAwake(g.Id);
            }
            Vector2 pos = g.IsNest || !g.HasDeath ? g.Pos : g.LastDeath;
            var player = PlayerController.Instance;
            Vector2 textAt = player && (player.Position - pos).sqrMagnitude <= ClearTextNearPlayer * ClearTextNearPlayer
                ? player.Position + Vector2.up * 1.4f
                : pos + Vector2.up * 1.2f;
            WorldOverlay.Text(textAt, "잠잠해졌다", Palette.HealthBar);
            DungeonEvents.RaiseGroupCleared(g.Id, g.IsNest, pos);
        }

        // ───────────────────────── 사건 ─────────────────────────

        void OnEnemyWoke(Enemy e)
        {
            if (!e || !_byId.TryGetValue(e.GroupId, out var g) || g.Cleared || g.AwakeRaised) return;
            g.AwakeRaised = true;
            DungeonEvents.RaiseGroupAwake(g.Id);
        }

        void OnEnemyKilled(Enemy e)
        {
            if (!e || !_byId.TryGetValue(e.GroupId, out var g)) return;
            g.LastDeath = e.Position;
            g.HasDeath = true;
        }

        /// <summary>
        /// 쓰러져 다시 섬(3차 초안 2-7 M0b판): 정리되지 않았고 깨어 있는 적이 있는 무리는 부른 굴쥐를 거두고,
        /// 살아 있는 무리를 제자리·가득 찬 체력·버팀으로 되돌려 다시 재운다(쓰러진 적은 그대로).
        /// </summary>
        void OnPlayerRespawned()
        {
            foreach (var g in _groups)
            {
                if (!g.Spawned || g.Cleared) continue;
                CollectAlive(g, _scratch);
                bool anyAwake = false;
                foreach (var e in _scratch)
                    if (e.Aware || e.WakePending)
                    {
                        anyAwake = true;
                        break;
                    }
                if (!anyAwake) continue;
                RemoveSummoned(g);
                foreach (var m in g.Members)
                    if (m && !m.Dead) m.ResetToHome();
            }
        }

        /// <summary>칸에 묶이지 않은 적(쥐 궤짝·도시락통 굴쥐). 원정 다시 시작·다시 섬 때 지운다.</summary>
        static bool IsLoose(Enemy e) => e.GroupId < 0 && !e.HasTerritory;

        /// <summary>둥지·무리 거느린 정예가 부른 굴쥐(보상 없음)를 지운다.</summary>
        void RemoveSummoned(Encounter g)
        {
            _scratch.Clear();
            foreach (var e in Enemy.All)
                if (e && e.GroupId == g.Id && e.NoReward) _scratch.Add(e);
            foreach (var e in _scratch) e.Remove();
            _scratch.Clear();
        }

        /// <summary>원정 다시 시작(3차 초안 2-6 '새 원정이면 다시 놓임'): 무리 적을 모두 지우고 정리한 무리까지 처음처럼 다시 놓는다.</summary>
        void OnExpeditionRestarted()
        {
            _scratch.Clear();
            foreach (var e in Enemy.All)
                if (e && !e.IsDummy && (_byId.ContainsKey(e.GroupId) || IsLoose(e))) _scratch.Add(e);
            foreach (var e in _scratch) e.Remove();
            _scratch.Clear();
            foreach (var g in _groups)
            {
                g.Members.Clear();
                g.Spawned = false;
                g.Cleared = false;
                g.AwakeRaised = false;
                g.HasDeath = false;
            }
            SpawnAll();
        }

        /// <summary>
        /// 큰 소리(곡괭이·광맥·금고, 3차 초안 2-6): 반경 안에서 가장 가까운 잠든 무리 하나가 깨어
        /// 소리 쪽 문에서 칸 안쪽 2유닛 자리로 가 밖을 보며 기다린다. 플레이어가 칸 경계에 들어오면 싸운다.
        /// 플레이어 몸이 낸 소리(DungeonEvents.NoiseFromPlayer: 곡괭이·광맥·금고·파내기)는 웅크렸으면 반경에 소음 배율(PlayerController.NoiseScale, 웅크림 0.3)을 곱한다(결정 ③, 12 → 3.6).
        /// 함정 소리(낙석·가시 덫, 1-2층 탐험 맛 1차 4-5·4-6)는 웅크림과 상관없이 나므로 곱하지 않는다 — 웅크려 밟아도 '진짜 벌은 들킴'이 남게.
        /// </summary>
        void OnNoise(Vector2 pos, float radius)
        {
            var player = PlayerController.Instance;
            if (player && DungeonEvents.NoiseFromPlayer) radius *= player.NoiseScale;
            Encounter best = null;
            float bestD = radius * radius;
            foreach (var g in _groups)
            {
                if (!g.Spawned || g.Cleared || !AllResting(g)) continue;
                float d = (g.Pos - pos).sqrMagnitude;
                // 순찰 무리(1-2층 탐험 맛 1차 4-3)는 제자리를 떠나 이웃 칸까지 걸으므로 지금 대원 자리(AllResting이 채운 _scratch)로도 잰다.
                // 그래야 이웃 칸에서 걷던 순찰이 바로 옆 낙석·덫·파내기 소리에 깬다(제자리만 재면 12 밖이라 못 들음).
                if (g.Patrolling)
                    foreach (var e in _scratch)
                        d = Mathf.Min(d, (e.Position - pos).sqrMagnitude);
                if (d > bestD) continue;
                bestD = d;
                best = g;
            }
            if (best == null) return;
            WakeToGuard(best, pos);
            DungeonEvents.Say("어둠 너머에서 무언가 깨어났다");
        }

        /// <summary>살아 있는 적이 있고 모두 쉬는(잠·먹는 중) 무리인가.</summary>
        bool AllResting(Encounter g)
        {
            CollectAlive(g, _scratch);
            if (_scratch.Count == 0) return false;
            foreach (var e in _scratch)
                if (e.Aware || e.WakePending) return false;
            return true;
        }

        void WakeToGuard(Encounter g, Vector2 noise)
        {
            CollectAlive(g, _scratch);
            var members = new List<Enemy>(_scratch);
            _scratch.Clear();
            // 강한 적이 앞줄 가운데, 굴쥐가 그 옆·뒤.
            members.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
            DungeonEdge door = NearestDoor(g.Cell, noise);
            Vector2 inward = door != null ? Inward(door.SideFrom(g.Cell)) : Vector2.zero;
            Vector2 lateral = new Vector2(-inward.y, inward.x);
            int slot = 0;
            foreach (var e in members)
            {
                if (door == null || e.Kind == MonsterKind.Nest || e.MoveSpeed <= 0f)
                {
                    // 움직이지 않는 둥지(또는 문 없는 칸): 제자리에서 소리 쪽을 보며 기다린다(부르기도 멈춤).
                    Vector2 look = noise - e.HomePosition;
                    e.GuardAt(e.HomePosition, look);
                }
                else
                {
                    e.GuardAt(GuardSpot(door.DoorCenter, inward, lateral, slot), -inward);
                    slot++;
                }
                e.Wake(false);
            }
        }

        static int Rank(Enemy e) => e.Kind == MonsterKind.Rat ? 1 : 0;

        static Vector2 GuardSpot(Vector2 doorCenter, Vector2 inward, Vector2 lateral, int slot)
        {
            int row = slot / 3;
            int col = slot % 3;
            float side = col == 0 ? 0f : col == 1 ? GuardLateral : -GuardLateral;
            Vector2 basePoint = doorCenter + inward * (GuardDepth + row * GuardRowStep) + lateral * side;
            for (int k = 0; k < 4; k++)
            {
                Vector2 p = basePoint + inward * (k * 0.6f);
                if (!Physics2D.OverlapCircle(p, 0.4f, Layers.WallMask)) return p;
            }
            return doorCenter + inward * (GuardDepth + row * GuardRowStep);
        }

        /// <summary>소리에 가장 가까운 문(열린 길·막힌 길 모두. 막힌 벽 너머에서 곡괭이질하면 그 벽 안쪽에서 기다린다).</summary>
        static DungeonEdge NearestDoor(DungeonCell cell, Vector2 noise)
        {
            DungeonEdge best = null;
            float bestD = float.MaxValue;
            foreach (var e in cell.Edges)
            {
                float d = (e.DoorCenter - noise).sqrMagnitude;
                if (d >= bestD) continue;
                bestD = d;
                best = e;
            }
            return best;
        }

        /// <summary>그 변의 문에서 칸 안쪽 방향.</summary>
        static Vector2 Inward(Side side)
        {
            switch (side)
            {
                case Side.Right: return Vector2.left;
                case Side.Left: return Vector2.right;
                case Side.Up: return Vector2.down;
                default: return Vector2.up;
            }
        }
    }
}
