using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 전투 손맛 시험장. 씬에는 카메라·조명과 이 컴포넌트만 두고, 방·플레이어·소환기·화면은 실행할 때 만든다.
    /// 방은 기획 4-6 큰 전투방(벽 포함 26×14, 안쪽 25×13)에 기둥 3개. 카메라는 고정이고 화면비로 크기를 계산한다.
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
        }

        public static bool IsEncounterPreset(Preset p) => p == Preset.FrontBack || p == Preset.Elite || p == Preset.Nest || p == Preset.Sleeping;

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

        Camera _cam;
        ScreenShake _shake;

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
            gameObject.AddComponent<CombatHud>();

            BuildRoom();
            Player = PlayerController.Create(new Vector2(0f, -1f));
            // 허수아비 측정은 무기가 바뀌면 다시 시작한다(기록이 섞이지 않게).
            Player.WeaponChanged += _ =>
            {
                if (CurrentPreset == Preset.Dummies) Stats.ResetDummyWindow();
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

        void Update() => FitCamera();

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
            Floor = FloorScaling.Clamp(floor);
            Player.ApplyBaseline(Floor);
            Spawner.Floor = Floor;
            // 층이 바뀌면 지금 있는 적도 새 배율로 다시 세운다.
            if (CurrentPreset == Preset.Dummies)
            {
                Spawner.SetDummyMode(true);
                Stats.ResetDummyWindow();
            }
            else if (IsEncounterPreset(CurrentPreset)) Spawner.RestartEncounter();
            else Spawner.ClearAll();
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
                default: return new EncounterSpec { Name = "잠든 적", Boars = 1, Archers = 1, Rats = 2, Sleeping = true };
            }
        }

        public void ApplyPreset(Preset preset)
        {
            CurrentPreset = preset;
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
            for (int i = 0; i < Pillars.Length; i++)
                Block(room, "Pillar " + (i + 1), Pillars[i], new Vector2(1.2f, 1.2f), Palette.Pillar, true);
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
}
