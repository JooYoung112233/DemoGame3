using System.Collections;
using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using Demo6.Core.Town;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Demo6.Game
{
    /// <summary>
    /// 탐험 시험(M0b ②) 던전. 씬에는 카메라와 이 컴포넌트만 두고, 지도·조명·플레이어·적·물체는 실행할 때 만든다.
    /// 전투는 M0a/M0b ① 코드를 그대로 쓰고(3차 값), 탐험 쪽(칸·문·등잔·궤짝·말뚝·레벨·큰 지도)을 더한다.
    /// 3차 초안 2장(한 층의 모습)과 7-2(넣는 것), 매판 새 탐험 1차(원정마다 새 갱도)를 따른다. 디스크 저장은 뺀다.
    /// 매판 새 탐험 흐름: 층·원정 번호 → 씨앗 → 지도(FloorGenerator), 바구니로 올라가기(Ascend) → 결과 창 → 밤 카드 뒤에서 장면을 다시 불러옴
    /// → 승강장 고르기 → 내려감, 계단(Descend) → 아래층으로 장면을 다시 불러옴. 장면 사이에는 꾸러미(ProfileCarry)만 들고 간다.
    /// 같은 장면 안에서 일부만 갈아 끼우지 않는다(안개 판 겹침·옛 벽 자리 찾기 같은 버그를 처음부터 피함, 3-3).
    /// 마을과 의뢰 첫 판(기획/마을-의뢰-첫판.md 1-4): Town 장면을 불러올 수 있으면(빌드 목록, 편집기에서는 장면 파일) 올라가기는 결과 창 대신 올라가는 카드 → 마을이고,
    /// 밤 카드·승강장 고르기는 마을 권양기에서 떠날 때 한다. 없으면 위 옛 흐름을 그대로 쓰고 경고를 남긴다. 의뢰 셈은 QuestTracker, 목표 HUD는 QuestHud.
    /// 오우거 굴(전투·보스 문서 3-8, 묶음 7): 2층 계단(아래층이 시험판에 없을 때)·승강장 고르기 '보스방 앞'·F1 시험 단추가 쪽지 TripPlan.Den으로
    /// 같은 층 번호의 굴 장면(손 지도 "P-X")을 불러온다. 시작은 굴 앞 말뚝, 보스방은 DungeonContent가 자리 표시에서 만든다(BossArena).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class DungeonRoot : MonoBehaviour
    {
        const float ReviveDelay = 1.5f;
        const float FadeTime = 0.5f;

        /// <summary>떠나기 전 확인 창(시스템·컨텐츠 다듬기 검토 1차 Q3): 넘침 칸까지 다 찼는데 바닥에 희귀 이상 장비가 남음. IMGUI 기능만.</summary>
        public const string LeaveConfirmModal = "leave-confirm";
        const string LeaveConfirmTitle = "가방이 넘친다";
        const string LeaveConfirmBody = "가방과 넘침 칸이 모두 찼다. 바닥에 남은 희귀 이상 장비 {0}개는 두고 가면 사라진다.";
        const string LeaveConfirmGo = "두고 떠나기";
        const string LeaveConfirmStay = "머물기";
        const string LeaveConfirmKeys = "[Esc] 머물기";

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
        /// <summary>
        /// 이 장면이 2층 계단 아래 '오우거 굴'(OgreDen 손 지도 "P-X", 장면 쪽지 TripPlan.Den)인가. 층 번호는 2층 그대로다(보스 수치·권장 레벨·아이템 레벨).
        /// 굴은 돌로 쌓은 고정 칸이라 씨앗·흔적·측량이 없고, 꾸러미에는 밟은 층 말고는 남기지 않는다(2층 지도 글자·승강장·발견 주머니를 덮지 않게).
        /// </summary>
        public bool IsDen { get; private set; }
        /// <summary>이 층 계단·계단 앞 말뚝 '내려가기'가 오우거 굴로 가는가(아래층이 시험판에 없고 굴이 있음).</summary>
        public bool DescendsToDen => !IsDen && OgreDen.DescendsToDen(Floor);
        /// <summary>아래층이 시험판에 있거나 계단 아래가 굴인가(계단·계단 앞 말뚝 '한 층 더 내려가기'). 굴 안에서는 내려가지 않는다.</summary>
        public bool CanDescend => !IsDen && (FloorRecipe.Exists(Floor + 1) || DescendsToDen);
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
        /// <summary>바구니로 올라가면 마을(Town 장면)로 가는가(빌드 목록, 편집기에서는 장면 파일). 없으면 옛 흐름(결과 창 → 밤 → 같은 던전). 패널·말뚝 글이 본다.</summary>
        public bool AscendsToTown { get; private set; }
        /// <summary>
        /// 바로 가기 시험 메뉴 '굴 안 바로 싸움'(TripPlan.DenFight, 굴 장면만)으로 보스방 문 안쪽에서 시작했는가. 말뚝 기록은 굴 앞 말뚝 그대로다.
        /// 오우거를 깨우는 것은 TestLaunchKnobs가 한다. 정식 흐름에서는 늘 false.
        /// </summary>
        public bool DenFightStart { get; private set; }

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
        /// <summary>쓰러질 때 남은 물약 수와 자리(묶음 5: 다시 설 때 물약, 피 묻은 주머니 자리).</summary>
        int _potionsAtDown = -1;
        Vector2 _downAt;
        /// <summary>떠나기 전 확인 창에서 '두고 떠나기'를 누르면 할 일(올라가기·내려가기). 창이 없으면 null.</summary>
        System.Action _pendingLeave;
        /// <summary>확인 창에 적을 두고 가는 희귀 이상 장비 수.</summary>
        int _pendingRareLeft;
        /// <summary>승강장에서 열린 길·판자벽만 지나 처음 갈 수 있는 칸 id(결과 창 '밝힌 칸 a/b'의 b).</summary>
        readonly HashSet<string> _startReachable = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            // 던전 목록 + 마을 정적을 한 곳에서 비운다(마을 Awake도 같은 함수를 불러 목록이 갈라지지 않게, 11장 위험 2).
            SceneStatics.Reset();
        }

        void Awake()
        {
            ResetStatics();
            Instance = this;
            Tuning.Ruleset = CombatRuleset.V3;
            RenderMaterials.SetUnlit(unlitMaterial);
            ArtRuntime.Mode = projectArt && projectArt.HasAnyContent ? ArtMode.Project : ArtMode.Shapes;
            Layers.ApplyCollisionMatrix();
            AscendsToTown = SceneTravel.CanLoad(SceneTravel.TownPath);

            gameObject.AddComponent<TimeScaleService>();
            gameObject.AddComponent<WorldOverlay>();
            gameObject.AddComponent<Sfx>();
            new GameObject("HitEffects").AddComponent<HitEffects>();

            // 꾸러미와 장면 사이 쪽지(매판 새 탐험 1차 3-3). 쪽지가 없으면 새 플레이: 1층, 첫 출발.
            var carry = ProfileCarry.Ensure();
            _trip = ProfileCarry.Trip;
            int floor = _trip != null && FloorRecipe.Exists(_trip.Floor) ? _trip.Floor : 1;
            Arrival = _trip != null ? _trip.Arrival : ArrivalKind.FirstStart;
            // 오우거 굴(전투·보스 문서 3-8): 쪽지가 굴이고 이 층 계단 아래가 굴일 때만. 아니면 쪽지의 층을 그대로 짓는다.
            IsDen = _trip != null && _trip.Den && OgreDen.IsBelow(floor);
            // 시험 패널 다시 짓기는 옛 장면의 첫 방문 여부를 잇는다(옛 장면 BeginPlay가 이미 밟은 층으로 적었어도 같은 예산·카드 글).
            bool rebuild = Arrival == ArrivalKind.Rebuild && _trip != null;
            // 굴은 원정마다 같은 돌방이라 늘 '고른 지도'로 본다(큰 지도 '새로 그린다' 글·흔적 없음).
            FirstVisit = IsDen || (rebuild && _trip.FirstVisit.HasValue ? _trip.FirstVisit.Value : !carry.VisitedFloors.Contains(floor));

            State = new DungeonState { ExpeditionStartTime = 0f, RunSalt = carry.ProfileSalt, Expedition = carry.Expedition };
            // 지도와 씨앗·글자·흔적은 세계를 짓기 전에 정한다(바닥 무늬가 씨앗을 쓸 수 있음). 굴을 읽지 못하면 일반 층으로 대신한다(IsDen = false).
            if (!IsDen || !BuildDenMap(floor))
            {
                if (IsDen)
                {
                    IsDen = false;
                    FirstVisit = !carry.VisitedFloors.Contains(floor);
                }
                BuildMap(carry, floor);
            }
            // 시작 칸: 승강장(입구 조각). 굴은 보스방 앞 쉼터(P)와 굴 앞 말뚝이다.
            var landingMap = IsDen ? MapAnchors.FindBossFront(Map) : MapAnchors.FindLanding(Map);
            var landingStake = MapAnchors.StakeIn(landingMap);
            LandingStakeId = landingStake != null ? landingStake.Id : IsDen ? OgreDen.FrontStakeId : null;
            // 프로필 몫(재화·능력·받은 것·켠 승강장)은 자리 표시보다 먼저: Register가 ProfileDone을 보고 끝낸 것으로 적는다.
            ProfileCarry.ApplyState(this);
            _stonesAtStart = State.Stones;
            _goldAtStart = State.Gold;
            bool sameExpedition = Arrival == ArrivalKind.Stairs || Arrival == ArrivalKind.Rebuild;
            _newLeg = !sameExpedition || carry.Leg == null;
            Leg = _newLeg ? new ExpeditionLeg() : carry.Leg.Clone();
            // 굴은 측량·결과 창 '밝힌 칸'에 넣지 않는다(0/0).
            if (landingMap != null && !IsDen)
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
            // 마을과 의뢰 첫 판 4-3: 의뢰 셈(던전 사건 → 꾸러미의 의뢰). 전투 시험장에는 붙이지 않는다.
            gameObject.AddComponent<QuestTracker>();
            QuestHud.Attach(gameObject, QuestHudMode.Dungeon);
            // 다크 판타지 분위기(기획/다크판타지-분위기-1차.md): 화면·소리·피.
            Atmosphere = gameObject.AddComponent<DungeonAtmosphere>();
            Audio = gameObject.AddComponent<DungeonAudio>();
            gameObject.AddComponent<GoreSystem>();
            gameObject.AddComponent<LurkSounds>();
            gameObject.AddComponent<GoreFootprints>();
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
            // 시험 메뉴 '굴 안 바로 싸움': 보스방 문 안쪽(PlayerInside에서 2유닛 더 안)에 선다. 말뚝 기록은 굴 앞 말뚝 그대로라 쓰러지면 쉼터에서 다시 선다.
            DenFightStart = IsDen && _trip != null && _trip.DenFight;
            if (DenFightStart)
            {
                var roomMap = MapAnchors.FindBossRoom(Map);
                var room = roomMap != null ? World.Find(roomMap.Id) : null;
                if (room != null) start = room.Center + new Vector2(OgreDen.PlayerInside.X + 2f, OgreDen.PlayerInside.Y);
                else DenFightStart = false;
            }
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
            // 다시 짓기 전에 이 층 측량을 받았으면 같은 원정에 또 주지 않는다. 굴은 측량이 없다(2층 측량 장 수를 굴이 채우지 않게).
            if ((rebuild && _trip.SurveyDone) || IsDen) Progress.MarkSurveyed();
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
        /// 오우거 굴 지도(FloorGenerator.GenerateDen, 손 지도 "P-X")를 짓는다. 돌방 고정 칸이라 씨앗 0·흔적 없음·견줄 지난 지도 없음이다.
        /// 검사(FloorRules.CheckDen)에 떨어지거나 읽지 못하거나 쉼터가 없으면 오류를 남기고 false(부르는 곳이 일반 층으로 대신한다).
        /// </summary>
        bool BuildDenMap(int floor)
        {
            var gen = FloorGenerator.GenerateDen(floor);
            FloorMap map = null;
            string problem = null;
            if (gen.Report != null && !gen.Report.Passed) problem = gen.Report.ToString();
            else
            {
                try
                {
                    map = gen.Build();
                    if (MapAnchors.FindBossFront(map) == null) problem = "보스방 앞 쉼터 칸이 없음";
                }
                catch (System.Exception e)
                {
                    problem = e.Message;
                }
            }
            if (problem != null)
            {
                Debug.LogError($"[오우거 굴] {floor}층 굴 지도를 짓지 못해 일반 {floor}층으로 대신한다: {problem}");
                return false;
            }
            Generated = gen;
            Map = map;
            Glyphs = gen.Glyphs ?? "";
            Seed = 0UL;
            Traces = System.Array.Empty<TraceSpot>();
            _traceBase = null;
            var carry = ProfileCarry.Data;
            Debug.Log($"[오우거 굴] 원정 {(carry != null ? carry.Expedition : 1)} · {Arrival} · 처치 {BossLedger.Kills(carry, OgreDen.BossId)} · " +
                      $"쉼터 말뚝 {(BossLedger.StakeLit(carry, OgreDen.FrontStakeId) ? "켬" : "아직")} · " + gen.Describe());
            return true;
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
        /// 오우거 굴은 밟은 층 말고는 적지 않는다: 2층 지도 글자(LastGlyphs, 다음 원정 흔적 비교)·켠 승강장·발견 주머니를 굴이 덮지 않게.
        /// 굴 앞 말뚝 기록은 말뚝(Stake)이 BossLedger에 적는다.
        /// </summary>
        void BeginPlay()
        {
            if (_begun) return;
            _begun = true;
            var carry = ProfileCarry.Ensure();
            int floor = Floor;
            carry.VisitedFloors.Add(floor);
            bool newLanding = false;
            if (!IsDen)
            {
                carry.LastGlyphs[floor] = Glyphs;
                if (floor > carry.DeepestFloor) carry.DeepestFloor = floor;
                newLanding = carry.LitLandings.Add(floor);
                carry.RopeDepth = Mathf.Max(carry.RopeDepth, floor);
                if (FirstVisit && !carry.DiscoveryPouch.ContainsKey(floor) && Progress)
                    carry.DiscoveryPouch[floor] = Progress.ComputeDiscoveryPouch(this);
            }
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
            ExpeditionLedger.OnFloorEntered(Player); // 원정 '남은 것' 시작점(묶음 3 가-4)
            NameplateSpots.PlaceForFloor(this); // 2층 계단방 명패(묶음 3 나-8)
            OilFlask.PlaceForFloor(this); // 막다른 칸 기름 병 하나(묶음 5-6)
            // 떠나며 바닥 장비를 거둔 글(검토 1차 Q3): 계단·다시 짓기·옛 흐름 올라가기(마을 없음)로 온 장면의 도착 알림 한 줄. 마을 몫이 남아 있으면 버린다.
            // 도착 순간 몰리는 알림 뒤에 띄운다(SaySweptLater).
            string swept = FloorSweepNote.TakeForDungeon();
            if (swept != null) StartCoroutine(SaySweptLater(swept));
        }

        /// <summary>
        /// 거둔 글을 띄우기까지 기다리는 시간(초). 도착 순간 권양기·칸 이름 알림이 몰리고 0.6초 뒤 DungeonHud 도착 글(승강장 불 켬 등)이 오는데,
        /// 알림 상한(DungeonHud ToastMax 2)에 밀려 거둔 글이 한 프레임도 안 보였다(검토 1차 Q3 플레이 확인). 그 알림들 뒤에 띄운다.
        /// </summary>
        const float SweptLineDelay = 1f;

        /// <summary>거둔 글을 SweptLineDelay 뒤에 띄운다. DungeonHud 알림처럼 멈춘 동안(지도·창)은 세지 않고, 한 프레임 시간은 0.1초로 자른다.</summary>
        IEnumerator SaySweptLater(string line)
        {
            float wait = SweptLineDelay;
            while (wait > 0f)
            {
                if (!TimeScaleService.Paused) wait -= Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                yield return null;
            }
            DungeonEvents.Say(line);
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
            UpdateLeaveConfirm();
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
            if (Player)
            {
                _potionsAtDown = Player.Potions;
                _downAt = Player.Position;
            }
            if (!_busy) StartCoroutine(ReviveRoutine());
        }

        /// <summary>
        /// 쓰러짐 대가(시스템-컨텐츠-다듬기-검토-1차.md 묶음 5, 결정 D1 '다'): 체력 60%·물약은 쓰러질 때 남은 수(0이면 1),
        /// 쓰러진 자리에 이번 원정 강화석·골드의 20%를 피 묻은 주머니로. 보스 재도전은 이 뒤 PlayerRespawned에서 보스방이 가득 채운다.
        /// </summary>
        void ApplyDownCost()
        {
            if (Leg != null) Leg.Downs++;
            if (!Player) return;
            if (DownTuning.Penalty && Player.Health)
            {
                int potions = _potionsAtDown >= 0 ? DownRules.RespawnPotions(_potionsAtDown, Player.PotionCapacity) : -1;
                Player.RestoreVitals(DownRules.RespawnHp(Player.Health.Max, DownTuning.HpFraction), potions);
            }
            _potionsAtDown = -1;
            if (State == null || DownTuning.PouchShare <= 0f) return;
            int floorStones = State.Stones - _stonesAtStart;
            int floorGold = State.Gold - _goldAtStart;
            int gainedStones = (Leg != null ? Leg.StonesGained : 0) + Mathf.Max(0, floorStones);
            int gainedGold = (Leg != null ? Leg.GoldGained : 0) + Mathf.Max(0, floorGold);
            int stones = DownRules.PouchAmount(gainedStones, State.Stones, DownTuning.PouchShare);
            int gold = DownRules.PouchAmount(gainedGold, State.Gold, DownTuning.PouchShare);
            if (stones <= 0 && gold <= 0) return;
            // 이 층에서 번 것보다 많이 떨구면 넘는 몫을 원정 몫과 층 시작 값에서 함께 뺀다(떨군 뒤·되찾은 뒤 '이번 원정 번 양'이 맞게).
            int exStones = DownRules.Excess(stones, floorStones);
            int exGold = DownRules.Excess(gold, floorGold);
            if (Leg != null)
            {
                Leg.StonesGained = Mathf.Max(0, Leg.StonesGained - exStones);
                Leg.GoldGained = Mathf.Max(0, Leg.GoldGained - exGold);
            }
            _stonesAtStart -= exStones;
            _goldAtStart -= exGold;
            State.Stones -= stones;
            State.Gold -= gold;
            BloodPouch.Drop(_downAt, stones, gold);
            DungeonEvents.Say(DownRules.PouchDropLine(stones, gold));
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
            // 말뚝보다 나중에 켠 벽 등잔이 있으면 그 곁에서(묶음 5-6 '작은 다시 서는 곳').
            Vector2 at = State.RespawnLamp ?? (State.StakePosition(State.LastStakeId) ?? Landing.Center) + new Vector2(2f, 0f);
            Player.ReviveAt(at);
            if (CameraRig) CameraRig.Snap();
            RemoveLooseEnemies();
            ApplyDownCost();
            DungeonEvents.RaisePlayerRespawned();
            ExpeditionLedger.NoteDeath();
            string where = State.RespawnLamp.HasValue ? "등잔 곁에서" : "말뚝 곁에서";
            DungeonEvents.Say(DownTuning.Penalty ? DownRules.RespawnLine.Replace("말뚝 곁에서", where) : "피 맛이 남은 채, " + where + " 눈을 떴다");
            yield return Fade(0f);
            _busy = false;
        }

        /// <summary>
        /// 칸에 묶이지 않은 적(쥐 궤짝·도시락통 굴쥐, 보스가 부른 굴쥐)을 지운다. 다시 선 플레이어를 말뚝까지 쫓아오지 않게.
        /// 보스는 지우지 않는다(보스방이 PlayerRespawned에서 체력 가득·처음 자리로 되돌림).
        /// </summary>
        static void RemoveLooseEnemies()
        {
            var loose = new System.Collections.Generic.List<Enemy>();
            foreach (var e in Enemy.All)
                if (e && !e.Dead && !e.IsDummy && !e.IsBoss && e.GroupId < 0 && !e.HasTerritory) loose.Add(e);
            foreach (var e in loose) e.Remove();
        }

        /// <summary>이번 원정에 아직 말뚝에서 쉬지 않았는가(묶음 5-2).</summary>
        public bool CanRest => Leg == null || !Leg.Rested;

        /// <summary>말뚝 쉬기(원정마다 한 번): 체력·물약 가득, 무기 행동 재사용·버팀도 채운다(Refill).</summary>
        public void RestAtStake()
        {
            if (!Player || Player.IsDown || !CanRest) return;
            Player.Refill();
            if (Leg != null) Leg.Rested = true;
            Sfx.Play(SfxKind.Pickup);
            DungeonEvents.Say(DownRules.RestLine);
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
        /// 바구니로 올라가기(말뚝 메뉴): 암전 → ExpeditionEnding(바닥 재화 자동 수거, 의뢰 '올라오기') → 바닥 장비 거두기(검토 1차 Q3) → 꾸러미 담기 →
        /// 마을이 있으면 원정 번호 +1·밤 사건 → 올라가는 카드(뒤에서 Town을 불러옴) → 마을 도착 카드.
        /// 없으면 옛 흐름: 결과 창 → 원정 번호 +1·밤 사건 → 밤 카드(뒤에서 줄 끝 층으로 장면을 다시 불러옴) → 승강장 고르기 → 내려감.
        /// 넘침 칸까지 다 찼는데 바닥에 희귀 이상 장비가 남으면 떠나기 전에 한 번 묻는다(AskBeforeLeaving).
        /// </summary>
        public void Ascend()
        {
            if (!CanLeave) return;
            if (AskBeforeLeaving(() => StartCoroutine(AscendRoutine()))) return;
            StartCoroutine(AscendRoutine());
        }

        // ── 떠날 때 바닥 장비 거두기(시스템·컨텐츠 다듬기 검토 1차 Q3, 2차 7-7) ──

        /// <summary>
        /// 떠나기 전 확인: 지금 떠나면 넘침 칸(6)까지 다 차서 바닥에 희귀 이상 장비가 남는가(Inventory.PlanFloorSweep). 남으면 확인 창을 열고 true
        /// (떠나기는 '두고 떠나기'를 눌렀을 때 leave로). 남지 않으면 false(부르는 곳이 바로 떠난다). 다른 창이 열려 있으면 떠나지 않고 true.
        /// 시험 단추(오우거 굴로·씨앗 다시 짓기)는 묻지 않는다.
        /// </summary>
        bool AskBeforeLeaving(System.Action leave)
        {
            var plan = Inventory ? Inventory.PlanFloorSweep() : null;
            if (plan == null || plan.RareLeft <= 0) return false;
            if (!DungeonUi.TryOpen(LeaveConfirmModal)) return true;
            _pendingLeave = leave;
            _pendingRareLeft = plan.RareLeft;
            Debug.Log($"[떠날 때 거두기] 넘침 칸까지 다 차 희귀 이상 {plan.RareLeft}개가 남는다 — 떠나기 전에 묻는다");
            return true;
        }

        /// <summary>확인 창 닫기: leave면 '두고 떠나기'(떠날 수 있을 때만), 아니면 '머물기'.</summary>
        void ResolveLeaveConfirm(bool leave)
        {
            var go = _pendingLeave;
            _pendingLeave = null;
            DungeonUi.Close(LeaveConfirmModal);
            if (leave && go != null && CanLeave) go();
        }

        /// <summary>확인 창이 떠 있으면 Esc = 머물기. 다른 곳이 창을 닫았으면 할 일을 비운다.</summary>
        void UpdateLeaveConfirm()
        {
            if (_pendingLeave == null) return;
            if (DungeonUi.Modal != LeaveConfirmModal)
            {
                _pendingLeave = null;
                return;
            }
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) ResolveLeaveConfirm(false);
        }

        /// <summary>
        /// 떠나는 암전 뒤(꾸러미 담기 전): 바닥 장비를 가방으로 거두고(Inventory.SweepFloor) 도착 글 한 줄을 돌려준다.
        /// 거둔 것도 두고 간 것도 없으면 null. 골드·강화석·룬은 LootPickup이 ExpeditionEnding·StairsUsed에서 거둔다.
        /// </summary>
        string SweepFloorGear()
        {
            if (!Inventory) return null;
            var plan = Inventory.SweepFloor();
            return Demo6.Core.Loot.BagSweep.ArrivalLine(plan, Inventory.Capacity);
        }

        /// <summary>확인 창(IMGUI 기능만, 배치는 Unity 개발 단계): 제목, 두고 가는 희귀 이상 수, [두고 떠나기] [머물기], Esc 안내.</summary>
        void DrawLeaveConfirm()
        {
            var prevMatrix = GUI.matrix;
            var prevColor = GUI.color;
            DungeonUi.Begin();
            GUI.depth = -20;
            const float width = 560f, pad = 22f, buttonHeight = 40f;
            float inner = width - pad * 2f;
            string body = string.Format(LeaveConfirmBody, _pendingRareLeft);
            float titleHeight = Mathf.Max(34f, DungeonUi.Title.CalcHeight(new GUIContent(LeaveConfirmTitle), inner));
            float bodyHeight = Mathf.Max(26f, DungeonUi.Label.CalcHeight(new GUIContent(body), inner));
            float height = pad + titleHeight + 10f + bodyHeight + 18f + buttonHeight + 10f + 24f + pad;
            var r = new Rect((DungeonUi.Width - width) * 0.5f, (DungeonUi.Height - height) * 0.5f, width, height);
            DungeonUi.Box(r, 0.95f);
            float x = r.x + pad;
            float y = r.y + pad;
            GUI.Label(new Rect(x, y, inner, titleHeight), LeaveConfirmTitle, DungeonUi.Title);
            y += titleHeight + 10f;
            GUI.Label(new Rect(x, y, inner, bodyHeight), body, DungeonUi.Label);
            y += bodyHeight + 18f;
            float half = (inner - 12f) * 0.5f;
            bool leave = GUI.Button(new Rect(x, y, half, buttonHeight), LeaveConfirmGo);
            bool stay = GUI.Button(new Rect(x + half + 12f, y, half, buttonHeight), LeaveConfirmStay);
            y += buttonHeight + 10f;
            GUI.color = DungeonUi.BoneDim;
            GUI.Label(new Rect(x, y, inner, 24f), LeaveConfirmKeys, DungeonUi.Small);
            GUI.matrix = prevMatrix;
            GUI.color = prevColor;
            if (leave) ResolveLeaveConfirm(true);
            else if (stay) ResolveLeaveConfirm(false);
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
            ExpeditionLedger.OnEnding(Player);
            // 바닥 장비도 가방으로 거둔다(검토 1차 Q3). 거둔 수는 마을 도착 카드(마을 없으면 다음 던전 장면 알림)에 한 줄로.
            string swept = SweepFloorGear();
            FloorSweepNote.Set(swept, AscendsToTown);
            var summary = BuildSummary();
            ProfileCarry.Capture(this, false);
            Debug.Log("[매판 새 탐험] " + summary.Line());
            // 마을과 의뢰 첫 판 1-4: 마을이 있으면 원정 번호 +1·밤 사건을 정하고 도착 쪽지를 둔 뒤 올라가는 카드 → 마을(TownTravel.TryAscend).
            // 없으면 옛 흐름(경고는 TryAscend가 남김).
            if (TownTravel.TryAscend(_nightCard, summary))
            {
                _nightStarted = true;
                yield break;
            }
            FloorSweepNote.Set(swept, false);
            if (_nightCard) _nightCard.ShowResults(summary, AfterResults);
            else AfterResults();
        }

        /// <summary>옛 흐름(마을 없음) 결과 창을 닫음: 원정 번호 +1, 이번 밤 사건, 밤 카드가 화면을 덮으면 줄 끝 층으로 장면을 다시 불러온다.</summary>
        void AfterResults()
        {
            if (_nightStarted) return;
            _nightStarted = true;
            var carry = ProfileCarry.Ensure();
            // 원정 번호 +1과 이번 밤 사건(마을 갈래와 같은 한 곳, TownNight.AdvanceForAscend).
            TownNight.AdvanceForAscend(carry);
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
        /// 계단 아래가 오우거 굴이면(DescendsToDen) 같은 원정으로 굴 장면(같은 층 번호, TripPlan.Den)을 불러온다.
        /// 아래층도 굴도 없으면 막혔다는 글만 띄운다. 넘침 칸까지 다 찼는데 바닥에 희귀 이상 장비가 남으면 떠나기 전에 한 번 묻는다(검토 1차 Q3).
        /// </summary>
        public void Descend()
        {
            if (!CanDescend)
            {
                DungeonEvents.Say("아래는 돌무더기로 막혀 있다 — 다음 시험에서.");
                return;
            }
            if (!CanLeave) return;
            bool toDen = DescendsToDen;
            if (AskBeforeLeaving(() => StartCoroutine(DescendRoutine(toDen)))) return;
            StartCoroutine(DescendRoutine(toDen));
        }

        /// <summary>
        /// F1 '오우거 굴로(시험)': 굴이 아닌 층에서 계단을 쓴 것처럼 같은 원정으로 굴 장면을 불러온다(체력·물약·원정 기록 이어짐).
        /// 굴 안이거나 떠날 수 없으면(암전·쓰러짐·떠나는 중) 아무것도 하지 않는다. 던전에 굴이 없으면 글만 띄운다.
        /// </summary>
        public void GoToDenForTest()
        {
            if (IsDen || !CanLeave) return;
            if (!OgreDen.InDungeon)
            {
                DungeonEvents.Say("시험: 이 시험판에는 오우거 굴이 없다");
                return;
            }
            StartCoroutine(DescendRoutine(true));
        }

        IEnumerator DescendRoutine(bool toDen)
        {
            _busy = true;
            _leaving = true;
            HoldForLeaving();
            yield return Fade(1f);
            // 탐험 기록이 층 시간을 적고, 바닥 재화를 거둔다(LootPickup).
            DungeonEvents.RaiseStairsUsed();
            // 바닥 장비도 가방으로 거둔다(검토 1차 Q3). 거둔 수는 아래층(굴) 도착 알림에 한 줄로.
            FloorSweepNote.Set(SweepFloorGear(), false);
            ProfileCarry.Capture(this, true);
            // 굴은 2층 계단 아래 따로 지은 돌방이라 층 번호는 그대로다(시험 단추는 지금 층과 상관없이 굴이 딸린 층).
            ProfileCarry.SetTrip(toDen
                ? new TripPlan { Floor = OgreDen.Floor, Arrival = ArrivalKind.Stairs, Den = true }
                : new TripPlan { Floor = Floor + 1, Arrival = ArrivalKind.Stairs });
            ReloadScene();
        }

        /// <summary>
        /// 승강장 고르기 결과(NightCard가 부름). 지은 층이면 바로 들어서고, 다르면 그 층으로 장면을 다시 불러온다
        /// (이 장면 지도는 보지 않았으므로 LastGlyphs에 남지 않고 흔적 비교에 쓰이지 않는다).
        /// '보스방 앞'(OgreDen.PickCode)은 굴 장면이면 바로 들어서고, 아니면 굴로 다시 불러온다. 굴 장면에서 일반 층을 고르면 같은 층이어도 다시 불러온다.
        /// </summary>
        public void ChooseLanding(int floor)
        {
            if (_begun || _leaving) return;
            if (OgreDen.IsPick(floor))
            {
                if (IsDen || !OgreDen.InDungeon)
                {
                    BeginPlay();
                    return;
                }
                _leaving = true;
                ProfileCarry.SetTrip(new TripPlan { Floor = OgreDen.Floor, Arrival = ArrivalKind.Basket, Den = true });
                ReloadScene();
                return;
            }
            if ((floor == Floor && !IsDen) || !FloorRecipe.Exists(floor))
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
        /// 오우거 굴은 고정 지도라 씨앗을 무시하고 같은 굴을 다시 짓는다(쪽지 Den 유지).
        /// </summary>
        public void RebuildWithSeed(ulong seed)
        {
            if (!CanLeave) return;
            _leaving = true;
            LootPickup.CollectAllNow();
            // 바닥 장비도 가방으로 거둔다(검토 1차 Q3, 시험 단추라 묻지 않음). 거둔 수는 다시 지은 장면 알림에 한 줄로.
            FloorSweepNote.Set(SweepFloorGear(), false);
            ProfileCarry.Capture(this, true, true);
            ProfileCarry.SetTrip(new TripPlan
            {
                Floor = Floor,
                Arrival = ArrivalKind.Rebuild,
                ForcedSeed = IsDen ? (ulong?)null : seed,
                FirstVisit = FirstVisit,
                TraceBase = IsDen ? null : _traceBase,
                SurveyDone = Progress && Progress.FloorCompleted,
                Den = IsDen,
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
            if (_pendingLeave != null && DungeonUi.Modal == LeaveConfirmModal) DrawLeaveConfirm();
            if (_fade <= 0.001f) return;
            GUI.depth = -100;
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, _fade);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
