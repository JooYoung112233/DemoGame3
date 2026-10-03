using System.Collections;
using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Demo6.Game
{
    /// <summary>
    /// 탐험 시험(M0b ②) 던전. 씬에는 카메라와 이 컴포넌트만 두고, 지도·조명·플레이어·적·물체는 실행할 때 만든다.
    /// 전투는 M0a/M0b ① 코드를 그대로 쓰고(3차 값), 탐험 쪽(칸·문·등잔·궤짝·말뚝·레벨·큰 지도)을 더한다.
    /// 3차 초안 2장(한 층의 모습)과 7-2(넣는 것), 매판 새 탐험 1차(원정마다 새 갱도)를 따른다. 마을·디스크 저장은 뺀다.
    /// 매판 새 탐험 흐름: 층·원정 번호 → 씨앗 → 지도(FloorGenerator), 바구니로 올라가기(Ascend) → 결과 창 → 밤 카드 뒤에서 장면을 다시 불러옴
    /// → 승강장 고르기 → 내려감, 계단(Descend) → 아래층으로 장면을 다시 불러옴. 장면 사이에는 꾸러미(ProfileCarry)만 들고 간다.
    /// 같은 장면 안에서 일부만 갈아 끼우지 않는다(안개 판 겹침·옛 벽 자리 찾기 같은 버그를 처음부터 피함, 3-3).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class DungeonRoot : MonoBehaviour
    {
        const float ReviveDelay = 1.5f;
        const float FadeTime = 0.5f;

        public static DungeonRoot Instance { get; private set; }

        [SerializeField, Tooltip("빛을 무시하는 스프라이트 재질(예고·효과). 비어 있으면 실행할 때 셰이더를 찾아 만든다.")]
        Material unlitMaterial;

        [SerializeField, Tooltip("전투 시험장과 공유하는 그림·소리 묶음. 빈 칸은 도형으로 표시한다.")]
        CombatArtSet projectArt;

        public CombatArtSet ProjectArt => projectArt;

        public FloorMap Map { get; private set; }
        public DungeonWorld World { get; private set; }
        public DungeonState State { get; private set; }
        public PlayerController Player { get; private set; }
        public DungeonLighting Lighting { get; private set; }
        /// <summary>좀보이드식 시야(벽에 가림, 바라보는 쪽 부채꼴, 기억 안개).</summary>
        public VisionSystem Vision { get; private set; }
        public DungeonCamera CameraRig { get; private set; }
        public CellEncounters Encounters { get; private set; }
        public Inventory Inventory { get; private set; }
        public PlayerProgress Progress { get; private set; }
        public ExplorationLog Log { get; private set; }
        public DungeonAtmosphere Atmosphere { get; private set; }
        public DungeonAudio Audio { get; private set; }
        public int Floor => Map != null ? Map.Floor : 1;
        /// <summary>플레이어가 지금 있는 칸(벽·문틈에 걸쳐 있으면 마지막 칸).</summary>
        public DungeonCell CurrentCell { get; private set; }

        // ── 매판 새 탐험 1차 ──
        /// <summary>이 층을 처음 밟는 원정인가(고른 지도). 층 이름 카드·큰 지도 첫 열기 글·보상 감쇠가 본다.</summary>
        public bool FirstVisit { get; private set; } = true;
        /// <summary>이번 장면에 어떻게 왔나.</summary>
        public ArrivalKind Arrival { get; private set; } = ArrivalKind.FirstStart;
        /// <summary>이번 지도를 지은 씨앗(1층 0 = 손 지도).</summary>
        public ulong Seed { get; private set; }
        /// <summary>이번 지도의 글자(꾸러미 LastGlyphs에 남겨 다음 원정 흔적 비교에 쓴다).</summary>
        public string Glyphs { get; private set; } = "";
        /// <summary>지난 원정과 비교한 흔적(MapDiff.Compare). 처음 밟는 층이면 비어 있다.</summary>
        public IReadOnlyList<TraceSpot> Traces { get; private set; } = System.Array.Empty<TraceSpot>();
        /// <summary>승강장 칸(입구 조각, MapAnchors.FindLanding).</summary>
        public DungeonCell Landing { get; private set; }
        /// <summary>승강장 말뚝 id(하드코딩 "f1.E.stake" 대신).</summary>
        public string LandingStakeId { get; private set; }
        /// <summary>이번 원정 번호.</summary>
        public int Expedition => State != null ? State.Expedition : 1;
        /// <summary>아래층이 시험판에 있는가(계단·계단 앞 말뚝 '한 층 더 내려가기').</summary>
        public bool CanDescend => FloorRecipe.Exists(Floor + 1);
        /// <summary>생성기 결과(씨앗·다시 굴림·고른 지도로 대신했는지·글자 지도). 시험 패널이 찍는다.</summary>
        public GeneratedFloor Generated { get; private set; }
        /// <summary>이번 원정에서 이 장면 전까지의 몫(계단으로 왔으면 위층들 기록, 아니면 빈 기록). 이 층 몫은 LegWithThisFloor.</summary>
        public ExpeditionLeg Leg { get; private set; }
        /// <summary>이 장면에서 쓰러뜨린 것(허수아비 제외).</summary>
        public int KillsHere { get; private set; }
        /// <summary>이 장면에서 연 궤짝(나무·쇠).</summary>
        public int ChestsHere { get; private set; }
        /// <summary>층에 들어섰는가(밤 카드·승강장 고르기가 끝나고 BeginPlay를 지남).</summary>
        public bool Playing => _begun;
        /// <summary>장면을 다시 불러오는 데 걸린 실제 시간(초, 옛 장면이 불러오기를 부른 때 → 이 장면 Awake 끝). 새 플레이면 -1.</summary>
        public float ReloadSeconds { get; private set; } = -1f;

        float _fade;
        bool _busy;
        /// <summary>BeginPlay를 지남(층에 들어섬).</summary>
        bool _begun;
        /// <summary>올라가기·내려가기·다시 짓기로 이 장면을 떠나는 중.</summary>
        bool _leaving;
        bool _reloading;
        bool _nightStarted;
        /// <summary>밤 카드·승강장 고르기 동안 시간과 입력을 멈춰 둠.</summary>
        bool _heldForNight;
        /// <summary>이 장면이 새 원정의 첫 장면인가(원정 몫을 새로 시작).</summary>
        bool _newLeg;
        TripPlan _trip;
        /// <summary>흔적을 견준 지난 원정 지도(없으면 null). 시험 패널 다시 짓기가 새 장면에 넘긴다.</summary>
        string _traceBase;
        NightCard _nightCard;
        int _stonesAtStart;
        int _goldAtStart;
        /// <summary>승강장에서 열린 길·판자벽만 지나 처음 갈 수 있는 칸 id(결과 창 '밝힌 칸 a/b'의 b).</summary>
        readonly HashSet<string> _startReachable = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            Enemy.ResetStatics();
            AttackTokens.ResetStatics();
            StrongAttackSchedule.ResetStatics();
            CombatEvents.ResetStatics();
            TimeScaleService.ResetStatics();
            CombatHud.ResetStatics();
            ArtRuntime.ResetStatics();
            DungeonEvents.ResetStatics();
            Interactable.ResetStatics();
            DungeonUi.ResetStatics();
            RenderMaterials.ResetStatics();
            VisionSystem.ResetStatics();
            DungeonAtmosphere.ResetStatics();
            DungeonAudio.ResetStatics();
            GoreSystem.ResetStatics();
            TopDownView.ResetStatics();
        }

        void Awake()
        {
            ResetStatics();
            Instance = this;
            Tuning.Ruleset = CombatRuleset.V3;
            RenderMaterials.SetUnlit(unlitMaterial);
            ArtRuntime.Mode = projectArt && projectArt.HasAnyContent ? ArtMode.Project : ArtMode.Shapes;
            Layers.ApplyCollisionMatrix();

            gameObject.AddComponent<TimeScaleService>();
            gameObject.AddComponent<WorldOverlay>();
            gameObject.AddComponent<Sfx>();
            new GameObject("HitEffects").AddComponent<HitEffects>();

            // 꾸러미와 장면 사이 쪽지(매판 새 탐험 1차 3-3). 쪽지가 없으면 새 플레이: 1층, 첫 출발.
            var carry = ProfileCarry.Ensure();
            _trip = ProfileCarry.Trip;
            int floor = _trip != null && FloorRecipe.Exists(_trip.Floor) ? _trip.Floor : 1;
            Arrival = _trip != null ? _trip.Arrival : ArrivalKind.FirstStart;
            // 시험 패널 다시 짓기는 옛 장면의 첫 방문 여부를 잇는다(옛 장면 BeginPlay가 이미 밟은 층으로 적었어도 같은 예산·카드 글).
            bool rebuild = Arrival == ArrivalKind.Rebuild && _trip != null;
            FirstVisit = rebuild && _trip.FirstVisit.HasValue ? _trip.FirstVisit.Value : !carry.VisitedFloors.Contains(floor);

            State = new DungeonState { ExpeditionStartTime = 0f, RunSalt = carry.ProfileSalt, Expedition = carry.Expedition };
            // 지도와 씨앗·글자·흔적은 세계를 짓기 전에 정한다(바닥 무늬가 씨앗을 쓸 수 있음).
            BuildMap(carry, floor);
            var landingMap = MapAnchors.FindLanding(Map);
            var landingStake = MapAnchors.StakeIn(landingMap);
            LandingStakeId = landingStake != null ? landingStake.Id : null;
            // 프로필 몫(재화·능력·받은 것·켠 승강장)은 자리 표시보다 먼저: Register가 ProfileDone을 보고 끝낸 것으로 적는다.
            ProfileCarry.ApplyState(this);
            _stonesAtStart = State.Stones;
            _goldAtStart = State.Gold;
            bool sameExpedition = Arrival == ArrivalKind.Stairs || Arrival == ArrivalKind.Rebuild;
            _newLeg = !sameExpedition || carry.Leg == null;
            Leg = _newLeg ? new ExpeditionLeg() : carry.Leg.Clone();
            if (landingMap != null)
                foreach (var c in Map.Reachable(landingMap, FloorMap.StartPassable))
                    _startReachable.Add(c.Id);

            World = DungeonWorld.Build(Map, transform);
            Landing = landingMap != null ? World.Find(landingMap.Id) : null;
            if (Landing == null) Landing = World.Cells[0];
            Lighting = gameObject.AddComponent<DungeonLighting>();
            Vision = gameObject.AddComponent<VisionSystem>();
            Vision.Init(World.Bounds);

            // 기능 모듈. 각자 Awake에서 사건을 구독한다.
            Encounters = gameObject.AddComponent<CellEncounters>();
            Inventory = gameObject.AddComponent<Inventory>();
            Progress = gameObject.AddComponent<PlayerProgress>();
            Log = gameObject.AddComponent<ExplorationLog>();
            gameObject.AddComponent<InteractionSystem>();
            gameObject.AddComponent<WorldReactions>();
            gameObject.AddComponent<DarkVision>();
            gameObject.AddComponent<ExploreWalk>();
            gameObject.AddComponent<BigMap>();
            gameObject.AddComponent<DungeonHud>();
            // 다크 판타지 분위기(기획/다크판타지-분위기-1차.md): 화면·소리·피.
            Atmosphere = gameObject.AddComponent<DungeonAtmosphere>();
            Audio = gameObject.AddComponent<DungeonAudio>();
            gameObject.AddComponent<GoreSystem>();
            // 정수리 시점 시험판과 한 마리 RPG 요소(대상 이름표·바닥 장비 이름표).
            gameObject.AddComponent<TopDownView>();
            gameObject.AddComponent<TargetPlate>();
            gameObject.AddComponent<LootLabels>();
            // 매판 새 탐험: 결과 창·밤 카드·승강장 고르기.
            _nightCard = gameObject.AddComponent<NightCard>();

            foreach (var cell in World.Cells)
                foreach (var f in cell.Map.Features)
                    DungeonContent.Spawn(this, cell, f);
            // 지난 원정과 달라진 곳의 흔적·승강장 막힌 아치(바닥 장식보다 먼저).
            ExpeditionTraces.Build(this, Traces);

            var startCell = Landing;
            string startStake = LandingStakeId;
            Vector2 start = (State.StakePosition(startStake) ?? startCell.Center) + new Vector2(2.5f, 0f);
            State.LastStakeId = startStake;
            Player = PlayerController.Create(start);
            Player.ApplyBaseline(Floor);
            Player.AutoRevive = false;
            Lighting.AttachPlayer(Player.transform);

            var cam = Camera.main;
            if (cam)
            {
                cam.backgroundColor = Color.black;
                cam.clearFlags = CameraClearFlags.SolidColor;
                CameraRig = cam.GetComponent<DungeonCamera>();
                if (!CameraRig) CameraRig = cam.gameObject.AddComponent<DungeonCamera>();
                CameraRig.Bind(Player.transform, World.Bounds);
            }

            // 능력치 한 입구(장비 문서 2-3): ApplyBaseline은 체력·물약을 채우는 몫만 남고, 공격·체력·방어는
            // Inventory.RecomputeStats(StatCalc → ApplyStats)가 장착 8자리·레벨로 덮어쓴다(방어는 층 기준표가 아니라 장비 방어).
            Inventory.Init(Player);
            Progress.Init(Player);
            // 레벨·스킬·장비(와 계단이면 체력·물약)를 꾸러미에서 덮어쓴다. 풀 때마다 RecomputeStats가 다시 넣는다.
            ProfileCarry.Apply(this);
            // 다시 짓기 전에 이 층 측량을 받았으면 같은 원정에 또 주지 않는다.
            if (rebuild && _trip.SurveyDone) Progress.MarkSurveyed();
            Atmosphere.Init(this);
            Audio.Init(Player);
            Encounters.SpawnAll();
            CombatEvents.PlayerDowned += OnPlayerDowned;
            CombatEvents.EnemyKilled += OnEnemyKilled;
            DungeonEvents.Discovered += OnDiscovered;

            // 장면을 다시 불러와 왔으면 검은 화면에서 시작해 BeginPlay에서 밝아진다.
            if (_trip != null)
            {
                _fade = 1f;
                if (_trip.RequestedRealtime > 0f) ReloadSeconds = Time.realtimeSinceStartup - _trip.RequestedRealtime;
            }
        }

        /// <summary>
        /// 층·씨앗에 맞는 지도를 짓는다(2-5): 쪽지에 씨앗이 있으면 그것(시험 패널), 아니면 처음 밟는 층은 고른 씨앗(1층 0 = 손 지도),
        /// 다시 연 층은 원정 번호로 정해지는 새 씨앗. 지난 원정의 같은 층 글자 지도가 있으면 흔적을 비교한다.
        /// 다시 연 층은 지난 원정과 너무 닮은 지도(모양·계단 칸이 같거나 흔적이 거의 없음)를 피해 씨앗을 섞어 다시 짓는다(FloorGenerator.GenerateUnlike).
        /// 시험 패널 다시 짓기는 옛 장면이 견준 지난 원정 지도(TripPlan.TraceBase)와 견준다.
        /// </summary>
        void BuildMap(CarryData carry, int floor)
        {
            var recipe = FloorRecipe.For(floor);
            bool forced = _trip != null && _trip.ForcedSeed.HasValue;
            ulong seed = forced
                ? _trip.ForcedSeed.Value
                : ExpeditionSeeds.Choose(recipe, FirstVisit, carry.ProfileSalt, carry.Expedition);
            string prev = null;
            if (Arrival == ArrivalKind.Rebuild && _trip != null) prev = _trip.TraceBase;
            else if (!FirstVisit) carry.LastGlyphs.TryGetValue(floor, out prev);
            _traceBase = prev;
            var input = new GeneratorInput
            {
                Floor = floor,
                Seed = seed,
                FirstVisit = FirstVisit,
                DeepestFloor = carry.DeepestFloor,
                HasPickaxe = carry.HasPickaxe,
                HasKey = carry.HasKey,
                OnceDone = new HashSet<string>(carry.OnceDone),
                Night = carry.Night,
            };
            // 같은 원정·같은 꾸러미면 같은 결과다. 시험 패널이 정한 씨앗은 그대로 둔다.
            var gen = forced || string.IsNullOrEmpty(prev) ? FloorGenerator.Generate(input) : FloorGenerator.GenerateUnlike(input, prev);
            Generated = gen;
            try
            {
                Map = gen.Build();
            }
            catch (System.Exception e)
            {
                // 생성 결과를 읽지 못하면 손 지도로 대신한다(층 번호만 바꿈). 흐름은 멈추지 않는다.
                Debug.LogError($"[매판 새 탐험] {floor}층 지도(씨앗 {gen.Seed})를 읽지 못해 손 지도로 대신한다: {e.Message}");
                gen = new GeneratedFloor
                {
                    Floor = floor, Name = recipe != null ? recipe.Name : FloorOneMap.Name, RequestedSeed = seed, Seed = seed,
                    FellBack = true, HandMap = true, Glyphs = FloorOneMap.Glyphs, Legend = FloorOneMap.Legend(),
                };
                Generated = gen;
                Map = gen.Build();
            }
            Glyphs = gen.Glyphs ?? "";
            Seed = gen.Seed;
            Debug.Log($"[매판 새 탐험] 원정 {carry.Expedition} · {Arrival} · " + (FirstVisit ? "첫 방문 · " : "") + gen.Describe());

            Traces = string.IsNullOrEmpty(prev) ? (IReadOnlyList<TraceSpot>)System.Array.Empty<TraceSpot>() : MapDiff.Compare(prev, Glyphs);
        }

        /// <summary>
        /// 시작: 바구니로 왔으면 옛 장면이 시작한 밤 카드를 남은 시간만큼 잇고 승강장을 고른 뒤 들어선다. 그 밖에는 바로 들어선다.
        /// 밤 카드·고르기 동안 화면은 검고 시간과 입력은 멈춘다.
        /// </summary>
        void Start()
        {
            if (ReloadSeconds >= 0f) Debug.Log($"[매판 새 탐험] 장면 다시 불러오기 {ReloadSeconds * 1000f:0}ms ({Arrival})");
            var trip = _trip;
            float now = Time.realtimeSinceStartup;
            if (trip == null || (!trip.PickLanding && trip.NightUntil <= now))
            {
                BeginPlay();
                return;
            }
            HoldForNight(true);
            if (_nightCard && trip.NightUntil > now) _nightCard.ContinueNight(trip.NightLines ?? System.Array.Empty<string>(), trip.NightUntil, AfterNight);
            else AfterNight();
        }

        void AfterNight()
        {
            if (_begun || _leaving) return;
            if (_trip != null && _trip.PickLanding && _nightCard)
            {
                var carry = ProfileCarry.Ensure();
                _nightCard.ShowLandingPicker(ProfileCarry.LandingOptions(), carry.RopeEnd(FloorRecipe.MaxTestFloor), ChooseLanding);
            }
            else BeginPlay();
        }

        void HoldForNight(bool hold)
        {
            if (hold)
            {
                _heldForNight = true;
                _busy = true;
                TimeScaleService.Paused = true;
                PlayerInputReader.Blocked = true;
                return;
            }
            if (!_heldForNight) return;
            _heldForNight = false;
            // 승강장 고르기 창이 고른 뒤에 닫혀도 멈춤이 남지 않게 여기서 푼다(창이 멈춤을 걸지 않았으므로 닫을 때 풀지 않는다).
            TimeScaleService.Paused = false;
            PlayerInputReader.Blocked = DungeonUi.ModalOpen;
        }

        /// <summary>
        /// 층에 들어섬(밤 카드·승강장 고르기 뒤, 아니면 바로): 꾸러미에 이 층을 밟았다고 적고(밟은 층·지도 글자·가장 깊은 층·켠 승강장·줄 깊이),
        /// 첫 방문이면 발견 주머니를 잰다. 화면이 밝아지며 LandingLit(처음 켠 승강장)·FloorEntered를 알린다.
        /// 층 이름 카드·승강장 도착 글은 FloorEntered를 듣는 쪽이 띄운다.
        /// </summary>
        void BeginPlay()
        {
            if (_begun) return;
            _begun = true;
            var carry = ProfileCarry.Ensure();
            int floor = Floor;
            carry.VisitedFloors.Add(floor);
            carry.LastGlyphs[floor] = Glyphs;
            if (floor > carry.DeepestFloor) carry.DeepestFloor = floor;
            bool newLanding = carry.LitLandings.Add(floor);
            carry.RopeDepth = Mathf.Max(carry.RopeDepth, floor);
            if (FirstVisit && !carry.DiscoveryPouch.ContainsKey(floor) && Progress)
                carry.DiscoveryPouch[floor] = Progress.ComputeDiscoveryPouch(this);
            if (_newLeg)
            {
                Leg.StartRealtime = Time.realtimeSinceStartup;
                State.ExpeditionStartTime = Time.time;
            }
            ProfileCarry.SetTrip(null);

            HoldForNight(false);
            if (_fade > 0.001f) StartCoroutine(EnterRoutine());
            else _busy = false;

            if (newLanding) DungeonEvents.RaiseLandingLit(floor);
            DungeonEvents.RaiseFloorEntered(floor, FirstVisit, Arrival);
        }

        IEnumerator EnterRoutine()
        {
            _busy = true;
            if (CameraRig) CameraRig.Snap();
            yield return Fade(0f);
            _busy = false;
            if (Player && Player.IsDown) StartCoroutine(ReviveRoutine());
        }

        void OnDestroy()
        {
            CombatEvents.PlayerDowned -= OnPlayerDowned;
            CombatEvents.EnemyKilled -= OnEnemyKilled;
            DungeonEvents.Discovered -= OnDiscovered;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!Player) return;
            var cell = World.CellAt(Player.Position);
            if (cell == null || cell == CurrentCell) return;
            CurrentCell = cell;
            bool first = !cell.Visited;
            if (first)
            {
                cell.Visited = true;
                State.VisitedCells.Add(cell.Id);
                DungeonEvents.RaiseDiscovered(DiscoveryKind.NewCell, cell.Center, cell.Name);
            }
            DungeonEvents.RaiseCellEntered(cell, first);
        }

        /// <summary>이번 원정 몫 세기: 쓰러뜨린 것(허수아비 제외).</summary>
        void OnEnemyKilled(Enemy e)
        {
            if (e && !e.IsDummy) KillsHere++;
        }

        /// <summary>이번 원정 몫 세기: 연 궤짝(처음 연 것만 Discovered가 온다).</summary>
        void OnDiscovered(DiscoveryKind kind, Vector2 pos, string label)
        {
            if (kind == DiscoveryKind.WoodChest || kind == DiscoveryKind.IronChest) ChestsHere++;
        }

        /// <summary>이 층에서 처음 갈 수 있는 칸 가운데 밟은 칸 수와 전체.</summary>
        public void CountStartCells(out int visited, out int total)
        {
            total = _startReachable.Count;
            visited = 0;
            if (State == null) return;
            foreach (var id in _startReachable)
                if (State.VisitedCells.Contains(id)) visited++;
        }

        /// <summary>
        /// 이번 원정 몫 + 이 층 몫(밝힌 칸·처음 갈 수 있는 칸·처치·궤짝·강화석·골드, 밟은 층)을 더한 새 기록. 부를 때마다 새로 센다.
        /// 계단으로 내려갈 때 꾸러미에 담고(ProfileCarry.Capture), 바구니로 올라갈 때 결과 창에 쓴다.
        /// countCells가 false면(시험 패널 다시 짓기) 이 층 칸 수는 더하지 않는다(다시 지은 장면이 이 층을 새로 센다).
        /// </summary>
        public ExpeditionLeg LegWithThisFloor(bool countCells = true)
        {
            var leg = Leg != null ? Leg.Clone() : new ExpeditionLeg();
            leg.Kills += KillsHere;
            leg.ChestsOpened += ChestsHere;
            if (countCells)
            {
                CountStartCells(out int visited, out int total);
                leg.CellsVisited += visited;
                leg.CellsTotal += total;
            }
            if (State != null)
            {
                leg.StonesGained += Mathf.Max(0, State.Stones - _stonesAtStart);
                leg.GoldGained += Mathf.Max(0, State.Gold - _goldAtStart);
            }
            if (leg.Floors.Count == 0 || leg.Floors[leg.Floors.Count - 1] != Floor) leg.Floors.Add(Floor);
            return leg;
        }

        /// <summary>결과 창 숫자(2-3 M0b: "3번째 원정 끝 — 밝힌 칸 9/10 · 궤짝 4 · 쓰러뜨린 것 13 · 레벨 3").</summary>
        public ExpeditionSummary BuildSummary()
        {
            var leg = LegWithThisFloor();
            return new ExpeditionSummary
            {
                Expedition = Expedition,
                CellsVisited = leg.CellsVisited,
                CellsTotal = leg.CellsTotal,
                Chests = leg.ChestsOpened,
                Kills = leg.Kills,
                Level = Progress ? Progress.Level : 1,
                DeepestFloorThisTrip = Mathf.Max(leg.DeepestFloor, Floor),
                StonesGained = leg.StonesGained,
                GoldGained = leg.GoldGained,
            };
        }

        void OnPlayerDowned()
        {
            if (!_busy) StartCoroutine(ReviveRoutine());
        }

        /// <summary>
        /// 쓰러지면 1.5초 뒤 이번 원정에서 마지막으로 켠 말뚝에서 다시 선다. 깨어 있던 무리는 제자리로 돌아가 잔다(3차 초안 2-7 M0b판).
        /// 갱도는 그대로 이어진다(매판 새 탐험 1차 결정 2 A).
        /// </summary>
        IEnumerator ReviveRoutine()
        {
            _busy = true;
            float t = 0f;
            while (t < ReviveDelay)
            {
                if (!TimeScaleService.Paused) t += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return Fade(1f);
            Vector2 at = (State.StakePosition(State.LastStakeId) ?? Landing.Center) + new Vector2(2f, 0f);
            Player.ReviveAt(at);
            if (CameraRig) CameraRig.Snap();
            RemoveLooseEnemies();
            DungeonEvents.RaisePlayerRespawned();
            DungeonEvents.Say("피 맛이 남은 채, 말뚝 곁에서 눈을 떴다");
            yield return Fade(0f);
            _busy = false;
        }

        /// <summary>칸에 묶이지 않은 적(쥐 궤짝·도시락통 굴쥐)을 지운다. 다시 선 플레이어를 말뚝까지 쫓아오지 않게.</summary>
        static void RemoveLooseEnemies()
        {
            var loose = new System.Collections.Generic.List<Enemy>();
            foreach (var e in Enemy.All)
                if (e && !e.Dead && !e.IsDummy && e.GroupId < 0 && !e.HasTerritory) loose.Add(e);
            foreach (var e in loose) e.Remove();
        }

        /// <summary>같은 층의 켠 말뚝으로 옮긴다(1초 암전, 원정 계속). 말뚝 메뉴가 부른다.</summary>
        public void TravelToStake(string stakeId)
        {
            if (_busy || _leaving) return;
            var pos = State.StakePosition(stakeId);
            if (pos == null) return;
            StartCoroutine(TravelRoutine(pos.Value, stakeId));
        }

        /// <summary>
        /// 옛 '원정 다시 시작'(같은 지도에 적·광맥만 되돌림). 매판 새 탐험 1차부터 원정은 바구니로 올라가 새 갱도로 다시 내려가므로
        /// 바구니로 올라가기(Ascend)와 같다. 부르는 곳(시험 패널)이 남아 있어 지우지 않는다.
        /// </summary>
        public void RestartExpedition(string stakeId) => Ascend();

        IEnumerator TravelRoutine(Vector2 stakePos, string stakeId)
        {
            _busy = true;
            yield return Fade(1f);
            Player.Teleport(stakePos + new Vector2(2f, 0f));
            State.LastStakeId = stakeId;
            if (CameraRig) CameraRig.Snap();
            yield return Fade(0f);
            _busy = false;
            // 암전 중에 쓰러졌으면 그 죽음을 놓치지 않고 말뚝에서 다시 세운다.
            if (Player && Player.IsDown) StartCoroutine(ReviveRoutine());
        }

        // ── 매판 새 탐험 1차 흐름 ──

        /// <summary>지금 올라가기·내려가기를 받는가(층에 들어섰고, 암전·떠나는 중이 아니고, 쓰러져 있지 않음). 시험 스크립트가 기다릴 때도 본다.</summary>
        public bool CanLeave => _begun && !_busy && !_leaving && Player && !Player.IsDown;

        /// <summary>
        /// 바구니로 올라가기(말뚝 메뉴): 암전 → ExpeditionEnding(바닥 재화 자동 수거) → 꾸러미 담기 → 결과 창 → 원정 번호 +1·밤 사건 →
        /// 밤 카드(뒤에서 줄 끝 층으로 장면을 다시 불러옴) → 승강장 고르기 → 내려감.
        /// </summary>
        public void Ascend()
        {
            if (!CanLeave) return;
            StartCoroutine(AscendRoutine());
        }

        /// <summary>
        /// 떠나는 암전(올라가기·내려가기): 시간과 입력을 멈춘다. 떠나기로 한 뒤 검은 화면 뒤에서 맞아 쓰러지는 일이 없게 한다
        /// (예전에는 계단 암전 0.5초 사이에 쓰러지면 꾸러미가 체력을 '가득'으로 담아 아래층에 체력이 가득 찬 채 도착했다).
        /// 멈춤은 장면을 다시 불러오면 ResetStatics가 푼다.
        /// </summary>
        void HoldForLeaving()
        {
            TimeScaleService.Paused = true;
            PlayerInputReader.Blocked = true;
        }

        IEnumerator AscendRoutine()
        {
            _busy = true;
            _leaving = true;
            HoldForLeaving();
            yield return Fade(1f);
            // 재화가 State에 들어간 뒤에 담는다(LootPickup이 날아가는 중이어도 바로 거둠).
            DungeonEvents.RaiseExpeditionEnding();
            var summary = BuildSummary();
            ProfileCarry.Capture(this, false);
            Debug.Log("[매판 새 탐험] " + summary.Line());
            if (_nightCard) _nightCard.ShowResults(summary, AfterResults);
            else AfterResults();
        }

        /// <summary>결과 창을 닫음: 원정 번호 +1, 이번 밤 사건, 밤 카드가 화면을 덮으면 줄 끝 층으로 장면을 다시 불러온다.</summary>
        void AfterResults()
        {
            if (_nightStarted) return;
            _nightStarted = true;
            var carry = ProfileCarry.Ensure();
            carry.Expedition++;
            carry.Night = ExpeditionSeeds.NightBefore(carry.ProfileSalt, carry.Expedition);
            var lines = NightCard.NightLines(carry.Night) ?? System.Array.Empty<string>();
            float until = Time.realtimeSinceStartup + NightCard.NightSeconds;
            var trip = new TripPlan
            {
                Floor = carry.RopeEnd(FloorRecipe.MaxTestFloor),
                Arrival = ArrivalKind.Basket,
                NightUntil = until,
                NightLines = lines,
                PickLanding = true,
            };
            void Covered()
            {
                ProfileCarry.SetTrip(trip);
                ReloadScene();
            }
            if (_nightCard) _nightCard.ShowNight(lines, NightCard.NightSeconds, Covered);
            else Covered();
        }

        /// <summary>
        /// 계단·계단 앞 말뚝 '한 층 더 내려가기': 같은 원정, 원정 몫(체력·물약·기록)을 들고 아래층 승강장으로 장면을 다시 불러온다.
        /// 아래층이 시험판에 없으면 막혔다는 글만 띄운다.
        /// </summary>
        public void Descend()
        {
            if (!CanDescend)
            {
                DungeonEvents.Say("아래는 돌무더기로 막혀 있다 — 다음 시험에서.");
                return;
            }
            if (!CanLeave) return;
            StartCoroutine(DescendRoutine());
        }

        IEnumerator DescendRoutine()
        {
            _busy = true;
            _leaving = true;
            HoldForLeaving();
            yield return Fade(1f);
            // 탐험 기록이 층 시간을 적고, 바닥 재화를 거둔다(LootPickup).
            DungeonEvents.RaiseStairsUsed();
            ProfileCarry.Capture(this, true);
            ProfileCarry.SetTrip(new TripPlan { Floor = Floor + 1, Arrival = ArrivalKind.Stairs });
            ReloadScene();
        }

        /// <summary>
        /// 승강장 고르기 결과(NightCard가 부름). 지은 층이면 바로 들어서고, 다르면 그 층으로 장면을 다시 불러온다
        /// (이 장면 지도는 보지 않았으므로 LastGlyphs에 남지 않고 흔적 비교에 쓰이지 않는다).
        /// </summary>
        public void ChooseLanding(int floor)
        {
            if (_begun || _leaving) return;
            if (floor == Floor || !FloorRecipe.Exists(floor))
            {
                BeginPlay();
                return;
            }
            _leaving = true;
            ProfileCarry.SetTrip(new TripPlan { Floor = floor, Arrival = ArrivalKind.Basket });
            ReloadScene();
        }

        /// <summary>
        /// 시험 패널 '이 씨앗으로 다시'·'씨앗 +1': 같은 원정·같은 층을 이 씨앗으로 다시 짓는다(Arrival = Rebuild, 체력·물약·원정 기록 이어짐).
        /// 옛 장면과 같은 조건(첫 방문 여부 → 같은 층 예산, 견줄 지난 원정 지도, 받은 측량)을 넘기고, 바닥 재화를 거둔 뒤 담는다.
        /// 이 층 칸 수는 다시 지은 장면이 센다. 암전·쓰러짐·떠나는 중에는 받지 않는다(CanLeave).
        /// </summary>
        public void RebuildWithSeed(ulong seed)
        {
            if (!CanLeave) return;
            _leaving = true;
            LootPickup.CollectAllNow();
            ProfileCarry.Capture(this, true, true);
            ProfileCarry.SetTrip(new TripPlan
            {
                Floor = Floor,
                Arrival = ArrivalKind.Rebuild,
                ForcedSeed = seed,
                FirstVisit = FirstVisit,
                TraceBase = _traceBase,
                SurveyDone = Progress && Progress.FloorCompleted,
            });
            ReloadScene();
        }

        /// <summary>
        /// 같은 장면을 다시 불러온다(빌드 장면 목록 번호, 없으면 편집기에서는 경로로). 정적 상태는 새 Awake의 ResetStatics가 비우고,
        /// 꾸러미·쪽지(ProfileCarry)만 남는다.
        /// </summary>
        void ReloadScene()
        {
            if (_reloading) return;
            _reloading = true;
            if (ProfileCarry.Trip != null) ProfileCarry.Trip.RequestedRealtime = Time.realtimeSinceStartup;
            var scene = gameObject.scene;
            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex, LoadSceneMode.Single);
                return;
            }
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.name, LoadSceneMode.Single);
#endif
        }

        IEnumerator Fade(float to)
        {
            while (!Mathf.Approximately(_fade, to))
            {
                _fade = Mathf.MoveTowards(_fade, to, Time.unscaledDeltaTime / FadeTime);
                yield return null;
            }
        }

        void OnGUI()
        {
            if (_fade <= 0.001f) return;
            GUI.depth = -100;
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, _fade);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
