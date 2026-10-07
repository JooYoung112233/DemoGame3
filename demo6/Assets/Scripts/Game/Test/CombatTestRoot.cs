using Demo6.Core.Combat;
using Demo6.Core.Loot;
using Demo6.Core.Stats;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 전투 손맛 시험장. 씬에는 카메라·조명과 이 컴포넌트만 두고, 방·플레이어·소환기·화면은 실행할 때 만든다.
    /// 방은 기획 4-6 큰 전투방(벽 포함 26×14, 안쪽 25×13)에 기둥 3개('보스' 프리셋은 보스방 돌 기둥 4개로 바꿔 지음). 카메라는 고정이고 화면비로 크기를 계산한다.
    /// 플레이어 능력치(장비 문서 2-3·3-4): '시험 장착'(시작 장비 + 고른 무기 종류)에 손잡이(StatOverrides: 층 기준 공격·체력·방어,
    /// 무기 고유 켜기·끄기, 공격 속도·치명 확률·치명 피해, 전설 3종)를 얹어 StatCalc → PlayerController.ApplyStats로 넣는다.
    /// 층·무기·손잡이가 바뀔 때마다 다시 넣는다(손잡이 값은 Tuning에 있어 ResetToDefaults로 되돌아간다).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class CombatTestRoot : MonoBehaviour
    {
        public const float RoomWidth = 26f;
        public const float RoomHeight = 14f;
        public const float WallThickness = 0.5f;
        public static readonly Rect Inner = new Rect(-12.5f, -6.5f, 25f, 13f);
        const float CameraOffsetY = -0.6f;
        static readonly Vector2[] Pillars = { new Vector2(-6f, 2.5f), new Vector2(6.5f, -2.5f), new Vector2(1f, 4f) };
        const float PillarSize = 1.2f;
        const float BossPillarSize = 1.6f;
        /// <summary>
        /// 보스방 기둥과 위아래 벽 사이 틈. 문서 3-6은 안쪽 26×14 방에서 기둥 y ±3.5로 이 틈이 2.7이다. 이 방은 안쪽 25×13이라 y ±3.5면 틈이 2.2로
        /// 오우거 지름 2.4보다 좁아 몸이 낀다(마무리 검토 ⑦). 그래서 틈을 문서와 같은 2.7로 두는 y(±3.0)에 짓는다.
        /// </summary>
        const float BossPillarWallGap = 2.7f;
        static readonly float BossPillarY = Inner.yMax - BossPillarWallGap - BossPillarSize * 0.5f;
        /// <summary>
        /// 보스방 돌 기둥 4개(기획/전투-보스-무기-다듬기-1차.md 3-6): (−4, ±3.0), (4.5, ±3.0), 1.6 × 1.6. 돌진을 확실히 박게 일반 기둥(1.2)보다 굵고,
        /// 기둥 사이 가운데 거리 가로 8.5·세로 6, 벽과 틈 2.7이라 지름 2.4인 몸이 끼지 않는다. '보스' 프리셋일 때만 기둥 3개 대신 짓는다.
        /// </summary>
        static readonly Vector2[] BossPillars = { new Vector2(-4f, BossPillarY), new Vector2(-4f, -BossPillarY), new Vector2(4.5f, BossPillarY), new Vector2(4.5f, -BossPillarY) };
        /// <summary>보스방 플레이어 시작 자리(왼쪽 문 쪽, 3-6). 오우거는 (7, 0)에서 오른쪽 벽을 보고 먹는 중이다(EnemySpawner, 꾸러미 ②).</summary>
        static readonly Vector2 BossPlayerStart = new Vector2(-9f, 0f);
        /// <summary>
        /// '기둥 옆 멧돼지'(3-1 묶음 3, 벽 박기 판정용): 기둥 (6.5, −2.5) 왼쪽 면에 붙은 멧돼지 1마리와 그 왼쪽(반대쪽)의 플레이어.
        /// EnemySpawner는 멧돼지를 무리 가운데에서 플레이어 쪽으로 1.2, 둘레로 0.9 옮겨 놓으므로 가운데를 기둥 오른쪽에 두면 멧돼지가 약 (5.1, −2.5)에 선다
        /// (몸 오른끝과 기둥 사이 약 0.25). 플레이어와 약 2.5 떨어져 있어 돌진(3~7)보다 머리치기·걷기로 시작한다.
        /// </summary>
        static readonly Vector2 PillarBoarCenter = new Vector2(7.16f, -2.17f);
        static readonly Vector2 PillarBoarPlayerStart = new Vector2(2.6f, -2.5f);

        public enum Preset
        {
            RatSwarm,
            M0Basic,
            Mixed,
            BoarPractice,
            ArcherPractice,
            Dummies,
            /// <summary>3차 마주침: 멧돼지 1 + 궁수 1 + 굴쥐 2.</summary>
            FrontBack,
            /// <summary>3차 마주침: 정예 멧돼지(+접두사) + 궁수 1.</summary>
            Elite,
            /// <summary>3차 마주침: 굴쥐 둥지.</summary>
            Nest,
            /// <summary>3차 마주침: 잠든 멧돼지 1 + 궁수 1 + 굴쥐 2(기습 연습).</summary>
            Sleeping,
            /// <summary>갱도 오우거 1마리(전투·보스·무기 다듬기 1차 3장, 결정 ④ 시험장 먼저). 기둥 4개(1.6)로 바꿔 짓고, 쓰러졌다 일어나면 처음부터 다시 놓는다.</summary>
            Boss,
            /// <summary>기둥 바로 옆 멧돼지 1마리(4-2 [1] 벽·기둥 박기 판정용, 묶음 3).</summary>
            PillarBoar,
        }

        public static bool IsEncounterPreset(Preset p) =>
            p == Preset.FrontBack || p == Preset.Elite || p == Preset.Nest || p == Preset.Sleeping || p == Preset.Boss || p == Preset.PillarBoar;

        public static CombatTestRoot Instance { get; private set; }

        [SerializeField, Tooltip("전투 그림·소리 교체 자리. 칸이 비어 있으면 도형과 코드 효과음을 쓴다.")]
        CombatArtSet projectArt;

        public CombatArtSet ProjectArt => projectArt;

        public int Floor { get; private set; } = 1;
        public Preset CurrentPreset { get; private set; } = Preset.FrontBack;
        /// <summary>정예 마주침의 접두사(3차 초안 3-4: M0b는 '단단한'·'무리 거느린').</summary>
        public bool EliteHardened { get; set; } = true;
        public bool ElitePack { get; set; }
        public PlayerController Player { get; private set; }
        public EnemySpawner Spawner { get; private set; }
        public CombatStats Stats { get; private set; }
        /// <summary>치명 연출 단계 기록(장비 문서 3-4·12장 '기록').</summary>
        public CritRecord Crits { get; private set; }
        /// <summary>고른 무기 종류 id(장검·대검·쌍검). 시험 장착 = 시작 장비(가죽 한 벌) + 이 무기 종류.</summary>
        public string TestWeaponId { get; private set; } = GearBaseTable.Longsword;

        /// <summary>지금 보스방 기둥 4개(1.6)로 지어져 있는가(아니면 일반 기둥 3개).</summary>
        public bool BossPillarsBuilt => _bossPillars;
        /// <summary>'보스' 프리셋에서 쓰러졌다 일어나 오우거를 처음부터 다시 놓은 수(재도전).</summary>
        public int BossRetries { get; private set; }

        Camera _cam;
        ScreenShake _shake;
        Transform _room;
        Transform _pillarRoot;
        bool _bossPillars;
        // 보스 재도전: 쓰러짐 → 일어남을 보고, 쓰러지기 직전 물약 수를 기억한다('남은 물약 그대로').
        bool _wasDown;
        int _potionsBeforeDown = -1;
        // 마지막으로 능력치를 넣을 때의 무기·층·손잡이(바뀌면 Update에서 다시 넣는다).
        string _appliedWeapon;
        int _appliedFloor = -1;
        bool _appliedIntrinsic;
        int _appliedAttackSpeed;
        int _appliedCritChance;
        int _appliedCritDamage;
        readonly int[] _appliedLegend = new int[3];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            Enemy.ResetStatics();
            AttackTokens.ResetStatics();
            CombatEvents.ResetStatics();
            TimeScaleService.ResetStatics();
            CombatHud.ResetStatics();
            ArtRuntime.ResetStatics();
            StrongAttackSchedule.ResetStatics();
            GoreSystem.ResetStatics();
            TopDownView.ResetStatics();
        }

        void Awake()
        {
            // 도메인 다시 불러오기가 꺼져 있어도(프로젝트 설정) 매 플레이를 깨끗하게 시작한다.
            ResetStatics();
            Instance = this;
            ArtRuntime.Mode = projectArt && projectArt.HasAnyContent ? ArtMode.Project : ArtMode.Shapes;
            Layers.ApplyCollisionMatrix();

            _cam = Camera.main;
            if (_cam)
            {
                _cam.orthographic = true;
                _cam.backgroundColor = Palette.Background;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                // 아래쪽 HUD가 방 아래 벽을 가리지 않게 방을 조금 위로 올려 보인다.
                _cam.transform.position = new Vector3(0f, CameraOffsetY, -10f);
                _shake = _cam.GetComponent<ScreenShake>();
                if (!_shake) _shake = _cam.gameObject.AddComponent<ScreenShake>();
                _shake.SetBase(_cam.transform.position);
                FitCamera();
            }

            gameObject.AddComponent<TimeScaleService>();
            gameObject.AddComponent<WorldOverlay>();
            gameObject.AddComponent<Sfx>();
            new GameObject("HitEffects").AddComponent<HitEffects>();
            gameObject.AddComponent<GoreSystem>();
            gameObject.AddComponent<TopDownView>();
            gameObject.AddComponent<TargetPlate>();
            Stats = gameObject.AddComponent<CombatStats>();
            Crits = new CritRecord();
            // ResetStatics로 구독을 비운 뒤라 여기서 듣는다.
            CombatEvents.PlayerCritShown += (tier, finisher) => Crits.Add(tier, finisher, Time.time);
            gameObject.AddComponent<CombatHud>();

            BuildRoom();
            Player = PlayerController.Create(new Vector2(0f, -1f));
            Player.WeaponChanged += rule =>
            {
                // 허수아비 측정은 무기가 바뀌면 다시 시작한다(기록이 섞이지 않게).
                if (CurrentPreset == Preset.Dummies) Stats.ResetDummyWindow();
                // 무기 키 1·2·3: 시험 장착의 무기 종류를 바꾸고 능력치(무기 고유 치명)를 다시 넣는다.
                if (rule != null && rule.id != TestWeaponId) SetTestWeapon(rule.id);
            };
            Spawner = new GameObject("EnemySpawner").AddComponent<EnemySpawner>();
            Spawner.Inner = Inner;

            SetFloor(1);
            ApplyPreset(Tuning.Ruleset == Demo6.Core.Combat.CombatRuleset.V3 ? Preset.FrontBack : Preset.Mixed);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_cam) _cam.rect = new Rect(0f, 0f, 1f, 1f);
        }

        void Update()
        {
            FitCamera();
            // 손잡이(패널·Tuning.ResetToDefaults·시험 명령)가 바뀌면 능력치를 다시 넣는다.
            if (Player && KnobsChanged())
            {
                RecomputeStats();
                if (CurrentPreset == Preset.Dummies) Stats.ResetDummyWindow();
            }
            WatchBossRetry();
        }

        /// <summary>
        /// 보스 재도전(3-8 시험판): '보스' 프리셋에서 플레이어가 쓰러졌다 일어나면 새 오우거를 놓아 다시 먹는 중으로 만들고 플레이어를 문 쪽 시작 자리에 세운다
        /// (Spawner.RestartEncounter, 기습 기회는 매번 있음). 물약은 Tuning.BossRetryFullPotions면 체력·물약 가득(Refill), 아니면 쓰러지기 직전 수로 되돌린다
        /// (자연 부활은 물약을 채우므로 기억한 수를 다시 넣는다).
        /// </summary>
        void WatchBossRetry()
        {
            if (!Player) return;
            bool down = Player.IsDown;
            if (CurrentPreset == Preset.Boss)
            {
                if (_wasDown && !down) RetryBoss();
                if (!down) _potionsBeforeDown = Player.Potions;
            }
            _wasDown = down;
        }

        void RetryBoss()
        {
            if (Tuning.BossRetryFullPotions) Player.Refill();
            else if (_potionsBeforeDown >= 0) Player.RestoreVitals(-1, _potionsBeforeDown);
            BossRetries++;
            Spawner.RestartEncounter();
        }

        /// <summary>시험 장착: 시작 장비(장검 + 가죽 한 벌, 반지·목걸이 빔)의 무기만 고른 종류로 바꾼다(등급·iLv·굴림 그대로).</summary>
        public static Loadout TestLoadout(string weaponId)
        {
            var loadout = Loadout.Starting();
            var weapon = loadout.Weapon;
            if (weapon != null)
            {
                var swapped = weapon.WithBase(weaponId);
                if (swapped != weapon) loadout.TryEquip(GearSlot.Weapon, swapped, out _);
            }
            return loadout;
        }

        /// <summary>지금 손잡이(Tuning)와 층으로 만든 덮어쓰기. 공격·체력·방어는 층 기준 장비(FloorScaling.Baseline)를 그대로 쓴다.</summary>
        public StatOverrides TestOverrides()
        {
            var b = FloorScaling.Baseline(Floor);
            var legend = new int[Tuning.TestLegendOn.Length];
            for (int i = 0; i < legend.Length; i++) legend[i] = LegendKnob(i);
            return new StatOverrides
            {
                WeaponIntrinsic = Tuning.TestWeaponIntrinsic,
                AttackSpeedPermille = Mathf.Clamp(Tuning.TestAttackSpeedPermille, 0, Tuning.TestAttackSpeedMax),
                CritChancePermille = Tuning.TestCritChancePermille >= 0 ? Tuning.TestCritChancePermille : (int?)null,
                CritDamagePermille = Tuning.TestCritDamagePermille >= 0 ? Tuning.TestCritDamagePermille : (int?)null,
                Attack = b.Attack,
                MaxHp = b.MaxHp,
                Defense = b.Defense,
                LegendaryRollPermille = legend,
            };
        }

        /// <summary>시험 장착 + 손잡이로 능력치를 계산해 플레이어에 넣는다(체력은 SetMax 규칙, 채우기는 ApplyBaseline).</summary>
        public void RecomputeStats()
        {
            if (!Player) return;
            RememberKnobs();
            var loadout = TestLoadout(TestWeaponId);
            Player.ApplyStats(StatCalc.Compute(loadout, 1, 0, TestOverrides()));
            Player.SetLook(loadout.Look);
        }

        /// <summary>무기 종류를 고른다(패널 단추·무기 키). 능력치를 다시 넣으면 PlayerController가 그 무기로 바꾼다.</summary>
        public void SetTestWeapon(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId) || GearBaseTable.Get(weaponId) == null) return;
            TestWeaponId = weaponId;
            RecomputeStats();
        }

        static int LegendKnob(int i) => Tuning.TestLegendOn[i] ? Mathf.Clamp(Tuning.TestLegendRoll[i], 0, 1000) : -1;

        void RememberKnobs()
        {
            _appliedWeapon = TestWeaponId;
            _appliedFloor = Floor;
            _appliedIntrinsic = Tuning.TestWeaponIntrinsic;
            _appliedAttackSpeed = Tuning.TestAttackSpeedPermille;
            _appliedCritChance = Tuning.TestCritChancePermille;
            _appliedCritDamage = Tuning.TestCritDamagePermille;
            for (int i = 0; i < _appliedLegend.Length; i++) _appliedLegend[i] = LegendKnob(i);
        }

        bool KnobsChanged()
        {
            if (_appliedWeapon != TestWeaponId || _appliedFloor != Floor || _appliedIntrinsic != Tuning.TestWeaponIntrinsic
                || _appliedAttackSpeed != Tuning.TestAttackSpeedPermille || _appliedCritChance != Tuning.TestCritChancePermille
                || _appliedCritDamage != Tuning.TestCritDamagePermille) return true;
            for (int i = 0; i < _appliedLegend.Length; i++)
                if (_appliedLegend[i] != LegendKnob(i)) return true;
            return false;
        }

        void FitCamera()
        {
            if (!_cam) return;
            // 기획 4-6: max((14 + 위아래 HUD 여백 2) ÷ 2, (26 + 0.5) ÷ (2 × 화면비)). 16:9는 8.0.
            float viewWidth = 1f - CombatHud.PanelScreenFraction;
            if (!Mathf.Approximately(_cam.rect.width, viewWidth)) _cam.rect = new Rect(0f, 0f, viewWidth, 1f);
            float aspect = Mathf.Max(0.1f, _cam.aspect);
            _cam.orthographicSize = Mathf.Max((RoomHeight + 2f) * 0.5f, (RoomWidth + 0.5f) / (2f * aspect));
        }

        public void SetFloor(int floor)
        {
            ApplyFloorStats(floor);
            // 층이 바뀌면 지금 있는 적도 새 배율로 다시 세운다.
            if (CurrentPreset == Preset.Dummies)
            {
                Spawner.SetDummyMode(true);
                Stats.ResetDummyWindow();
            }
            else if (IsEncounterPreset(CurrentPreset)) Spawner.RestartEncounter();
            else Spawner.ClearAll();
        }

        /// <summary>층만 바꾼다: 층 기준 공격·체력·방어를 능력치로 넣고(한 입구), 체력·물약을 채우고, 소환기 층을 맞춘다. 적은 다시 세우지 않는다(부르는 쪽 몫).</summary>
        void ApplyFloorStats(int floor)
        {
            Floor = FloorScaling.Clamp(floor);
            RecomputeStats();
            Player.ApplyBaseline(Floor);
            Spawner.Floor = Floor;
        }

        /// <summary>M0a 값 / 3차 값. 적 체력·패턴·공격 기회가 바뀌므로 지금 구성을 다시 세운다.</summary>
        public void SetRuleset(Demo6.Core.Combat.CombatRuleset rules)
        {
            if (Tuning.Ruleset == rules) return;
            Tuning.Ruleset = rules;
            ApplyPreset(CurrentPreset);
        }

        /// <summary>멧돼지 약하게·정예 접두사처럼 적 수치가 바뀌는 손잡이를 바꾼 뒤 부른다.</summary>
        public void Respawn() => ApplyPreset(CurrentPreset);

        EncounterSpec SpecFor(Preset preset)
        {
            switch (preset)
            {
                case Preset.FrontBack: return new EncounterSpec { Name = "앞뒤", Boars = 1, Archers = 1, Rats = 2 };
                case Preset.Elite:
                    var affixes = (EliteHardened ? EliteAffix.Hardened : EliteAffix.None) | (ElitePack ? EliteAffix.Pack : EliteAffix.None);
                    return new EncounterSpec { Name = "정예", EliteBoar = true, Affixes = affixes, Archers = ElitePack ? 0 : 1 };
                case Preset.Nest: return new EncounterSpec { Name = "둥지", Nest = true };
                case Preset.Boss: return new EncounterSpec { Name = "보스", Boss = true, PlayerStart = BossPlayerStart };
                case Preset.PillarBoar: return new EncounterSpec { Name = "기둥 옆 돌충이", Boars = 1, Center = PillarBoarCenter, PlayerStart = PillarBoarPlayerStart };
                default: return new EncounterSpec { Name = "잠든 적", Boars = 1, Archers = 1, Rats = 2, Sleeping = true };
            }
        }

        public void ApplyPreset(Preset preset)
        {
            bool enteringBoss = preset == Preset.Boss && CurrentPreset != Preset.Boss;
            CurrentPreset = preset;
            // 기둥은 무리를 놓기 전에 바꿔 짓는다(놓을 자리 고르기가 벽·기둥을 피한다).
            BuildPillars(preset == Preset.Boss);
            // '보스'로 들어올 때는 판정 기준(3-10 '2층 장비', 처치 45~65초)대로 2층 장비로 맞춘다. 그 뒤 층 단추로 5·10층을 보는 것은 그대로 둔다.
            if (enteringBoss && Floor != BossRules.TrialFloor) ApplyFloorStats(BossRules.TrialFloor);
            _wasDown = Player && Player.IsDown;
            _potionsBeforeDown = Player ? Player.Potions : -1;
            Spawner.RespawnDelay = preset == Preset.RatSwarm ? 0.25f : 1.0f;
            Spawner.PackSpawn = preset == Preset.RatSwarm;
            if (IsEncounterPreset(preset))
            {
                Spawner.StartEncounters(SpecFor(preset));
                return;
            }
            switch (preset)
            {
                case Preset.RatSwarm:
                    Spawner.SetDummyMode(false);
                    Spawner.SetTargets(18, 0, 0);
                    break;
                case Preset.M0Basic:
                    Spawner.SetDummyMode(false);
                    Spawner.SetTargets(8, 1, 0);
                    break;
                case Preset.Mixed:
                    Spawner.SetDummyMode(false);
                    Spawner.SetTargets(7, 2, 2);
                    break;
                case Preset.BoarPractice:
                    Spawner.SetDummyMode(false);
                    Spawner.SetTargets(0, 2, 0);
                    break;
                case Preset.ArcherPractice:
                    Spawner.SetDummyMode(false);
                    Spawner.SetTargets(2, 0, 3);
                    break;
                case Preset.Dummies:
                    Spawner.SetTargets(0, 0, 0);
                    Spawner.SetDummyMode(true);
                    Stats.ResetDummyWindow();
                    break;
            }
        }

        public void ChangeCount(MonsterKind kind, int delta)
        {
            if (CurrentPreset == Preset.Dummies || IsEncounterPreset(CurrentPreset)) ApplyPreset(Preset.Mixed);
            Spawner.SetTarget(kind, Spawner.Target(kind) + delta);
        }

        public void ClearEnemies()
        {
            if (CurrentPreset == Preset.Dummies) Spawner.SetDummyMode(true);
            else if (IsEncounterPreset(CurrentPreset)) Spawner.RestartEncounter();
            else Spawner.ClearAll();
        }

        void BuildRoom()
        {
            var room = new GameObject("Room").transform;
            _room = room;

            var floor = new GameObject("Floor");
            floor.transform.SetParent(room, false);
            var floorSprite = floor.AddComponent<SpriteRenderer>();
            floorSprite.sprite = ShapeSprites.Checker(Mathf.RoundToInt(Inner.width), Mathf.RoundToInt(Inner.height), Palette.FloorA, Palette.FloorB);
            floorSprite.sortingOrder = -1000;

            float halfW = RoomWidth * 0.5f - WallThickness * 0.5f;
            float halfH = RoomHeight * 0.5f - WallThickness * 0.5f;
            Block(room, "Wall N", new Vector2(0f, halfH), new Vector2(RoomWidth, WallThickness), Palette.Wall);
            Block(room, "Wall S", new Vector2(0f, -halfH), new Vector2(RoomWidth, WallThickness), Palette.Wall);
            Block(room, "Wall W", new Vector2(-halfW, 0f), new Vector2(WallThickness, RoomHeight), Palette.Wall);
            Block(room, "Wall E", new Vector2(halfW, 0f), new Vector2(WallThickness, RoomHeight), Palette.Wall);
            BuildPillars(false);
        }

        /// <summary>
        /// 기둥만 다시 짓는다: 보스방이면 돌 기둥 4개(1.6), 아니면 일반 기둥 3개(1.2). 이미 그 모양이면 그대로 둔다.
        /// 옛 기둥은 바로 꺼서(충돌체가 이번 프레임 안에 빠짐) 새 무리 자리 고르기·벽 박기 판정에 남지 않게 한다.
        /// </summary>
        void BuildPillars(bool boss)
        {
            if (!_room) return;
            if (_pillarRoot && _bossPillars == boss) return;
            if (_pillarRoot)
            {
                _pillarRoot.gameObject.SetActive(false);
                Destroy(_pillarRoot.gameObject);
            }
            _bossPillars = boss;
            _pillarRoot = new GameObject(boss ? "Pillars (보스방)" : "Pillars").transform;
            _pillarRoot.SetParent(_room, false);
            var spots = boss ? BossPillars : Pillars;
            float size = boss ? BossPillarSize : PillarSize;
            for (int i = 0; i < spots.Length; i++)
                Block(_pillarRoot, "Pillar " + (i + 1), spots[i], new Vector2(size, size), Palette.Pillar, true);
        }

        static void Block(Transform parent, string name, Vector2 position, Vector2 size, Color color, bool sortByY = false)
        {
            var go = new GameObject(name);
            go.layer = Layers.Wall;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = ShapeSprites.Square;
            sr.color = color;
            sr.sortingOrder = sortByY ? 1000 - Mathf.RoundToInt(position.y * 20f) : -900;
        }
    }

    /// <summary>
    /// 전투 시험장 치명 연출 기록(장비 문서 3-4 '기록', 12장 판정 표): 단계별 치명 수와 무거운 치명 사이 간격(게임 시간).
    /// CombatEvents.PlayerCritShown(동작·회오리 한 타·검풍 한 번마다 하나)을 CombatTestRoot가 넣는다.
    /// </summary>
    public sealed class CritRecord
    {
        readonly int[] _counts = new int[4];
        float _lastHeavy = -1f;

        /// <summary>마무리 단계에서 낸 무거운 치명 수(0.5초 제한 예외).</summary>
        public int FinisherHeavy { get; private set; }
        /// <summary>무거운 치명 사이 간격 개수·합·최소, 0.5초보다 짧았던 간격 수(마무리 예외로만 생긴다).</summary>
        public int HeavyGaps { get; private set; }
        public float HeavyGapSum { get; private set; }
        public float HeavyGapMin { get; private set; } = float.PositiveInfinity;
        public int HeavyGapsUnderHalf { get; private set; }
        public float HeavyGapAverage => HeavyGaps > 0 ? HeavyGapSum / HeavyGaps : 0f;
        public int Total => _counts[1] + _counts[2] + _counts[3];

        public int Count(CritTier tier) => _counts[(int)tier];

        public void Add(CritTier tier, bool finisher, float now)
        {
            if (tier == CritTier.None) return;
            _counts[(int)tier]++;
            if (tier != CritTier.Heavy) return;
            if (finisher) FinisherHeavy++;
            if (_lastHeavy >= 0f)
            {
                float gap = now - _lastHeavy;
                HeavyGaps++;
                HeavyGapSum += gap;
                if (gap < HeavyGapMin) HeavyGapMin = gap;
                if (gap < (float)CritTiers.HeavyInterval) HeavyGapsUnderHalf++;
            }
            _lastHeavy = now;
        }

        public void Reset()
        {
            for (int i = 0; i < _counts.Length; i++) _counts[i] = 0;
            _lastHeavy = -1f;
            FinisherHeavy = 0;
            HeavyGaps = 0;
            HeavyGapSum = 0f;
            HeavyGapMin = float.PositiveInfinity;
            HeavyGapsUnderHalf = 0;
        }
    }
}
