using System.Collections;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 탐험 시험(M0b ②) 던전 1층. 씬에는 카메라와 이 컴포넌트만 두고, 지도·조명·플레이어·적·물체는 실행할 때 만든다.
    /// 전투는 M0a/M0b ① 코드를 그대로 쓰고(3차 값), 탐험 쪽(칸·문·등잔·궤짝·말뚝·레벨·큰 지도)을 더한다.
    /// 3차 초안 2장(한 층의 모습)과 7-2(넣는 것)를 따른다. 마을·디스크 저장·2층은 뺀다.
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
        public int Floor => Map != null ? Map.Floor : 1;
        /// <summary>플레이어가 지금 있는 칸(벽·문틈에 걸쳐 있으면 마지막 칸).</summary>
        public DungeonCell CurrentCell { get; private set; }

        float _fade;
        bool _busy;

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

            State = new DungeonState { ExpeditionStartTime = 0f, RunSalt = (ulong)System.DateTime.UtcNow.Ticks };
            Map = FloorOneMap.Build();
            World = DungeonWorld.Build(Map, transform);
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

            foreach (var cell in World.Cells)
                foreach (var f in cell.Map.Features)
                    DungeonContent.Spawn(this, cell, f);

            var startCell = World.Find("E");
            string startStake = "f1.E.stake";
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

            Inventory.Init(Player);
            Progress.Init(Player);
            Encounters.SpawnAll();
            CombatEvents.PlayerDowned += OnPlayerDowned;
        }

        void OnDestroy()
        {
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

        void OnPlayerDowned()
        {
            if (!_busy) StartCoroutine(ReviveRoutine());
        }

        /// <summary>쓰러지면 1.5초 뒤 마지막 말뚝에서 다시 선다. 깨어 있던 무리는 제자리로 돌아가 잔다(3차 초안 2-7 M0b판).</summary>
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
            Vector2 at = (State.StakePosition(State.LastStakeId) ?? World.Find("E").Center) + new Vector2(2f, 0f);
            Player.ReviveAt(at);
            if (CameraRig) CameraRig.Snap();
            RemoveLooseEnemies();
            DungeonEvents.RaisePlayerRespawned();
            DungeonEvents.Say("말뚝 곁에서 다시 정신이 들었다");
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

        /// <summary>켠 말뚝으로 옮긴다(1초 암전, 원정 계속). 말뚝 메뉴가 부른다.</summary>
        public void TravelToStake(string stakeId)
        {
            if (_busy) return;
            var pos = State.StakePosition(stakeId);
            if (pos == null) return;
            StartCoroutine(TravelRoutine(pos.Value, stakeId, false));
        }

        /// <summary>
        /// '원정 다시 시작'(M0b의 귀환): 고른 말뚝에서 체력·물약을 채우고 적과 광맥을 다시 놓는다.
        /// 지도, 켠 말뚝·등잔, 연 궤짝·벽, 능력, 레벨은 남는다(3차 초안 2-7 '영구로 남는 것').
        /// </summary>
        public void RestartExpedition(string stakeId)
        {
            if (_busy) return;
            var pos = State.StakePosition(stakeId) ?? World.Find("E").Center;
            StartCoroutine(TravelRoutine(pos, stakeId, true));
        }

        IEnumerator TravelRoutine(Vector2 stakePos, string stakeId, bool restart)
        {
            _busy = true;
            yield return Fade(1f);
            Player.Teleport(stakePos + new Vector2(2f, 0f));
            State.LastStakeId = stakeId;
            if (restart)
            {
                Player.Refill();
                State.Expedition++;
                State.ExpeditionStartTime = Time.time;
                DungeonEvents.RaiseExpeditionRestarted();
                DungeonEvents.Say($"원정 {State.Expedition}번째 — 적과 광맥이 다시 놓였다");
            }
            if (CameraRig) CameraRig.Snap();
            yield return Fade(0f);
            _busy = false;
            // 암전 중에 쓰러졌으면 그 죽음을 놓치지 않고 말뚝에서 다시 세운다.
            if (Player && Player.IsDown) StartCoroutine(ReviveRoutine());
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
