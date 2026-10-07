using System.Collections;
using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 보스방 런타임(기획/전투-보스-무기-다듬기-1차.md 3-5~3-8, 6-1 묶음 7 '오우거 굴' X). 보스 자리 표시(FeatureKind.Boss)에서 DungeonContent가 만든다.
    /// 하는 일: 벽 등잔 4개(ArenaLamp, 처음 꺼짐)·쥐 구멍·장식(도형 임시판) → 보스 소환(OgreDen.BossStart, 먹는 중, 같은 원정에 이미 잡았으면 쓰러진 채)
    /// → 보스가 깨고 플레이어가 방 안이면 문 막기("등 뒤에서 입구가 무너졌다.")·카메라 고정(OgreDen.CameraSize)·예고 어둠 위(Telegraph)·BossEngaged
    /// → 2단계 전환 0.3초(OgreBrain.TransitionStarted부터 셈)에 대각선 등잔 둘 꺼짐(F 0.5초로 다시 켬) → 이기면(DungeonEvents.BossDefeated) 문 열기·등잔 다시 켜기·줄 끝 말뚝·살펴보기·카메라 풀기
    /// → 지면(쓰러진 뒤 말뚝에서 다시 섬, DungeonEvents.PlayerRespawned) 보스·문·등잔·쥐·카메라를 처음으로(OgreBrain.ResetFight, BossReset).
    /// 꾸러미 기록은 BossLedger(처치는 BossReward, 쓰러짐은 여기). 시야(VisionSystem)는 ArenaLamp 빛을 등잔 빛으로 센다.
    /// 만들기(Create)는 정리만 하고, 짓기는 Start(DungeonRoot.Awake가 끝나 플레이어·카메라·꾸러미가 있을 때)에서 한다.
    /// 플레이어가 방 밖(쉼터 쪽)에서 깨웠으면 문을 막지 않고 보스를 다시 먹는 중으로 돌린다(보스가 문 밖으로 나오지 않게).
    /// </summary>
    public sealed class BossArena : MonoBehaviour
    {
        /// <summary>문 막이 크기(문틈 1 × 4보다 조금 크게 덮음).</summary>
        const float DoorBlockThick = 1.2f;
        const float DoorBlockLong = 4.4f;
        /// <summary>방 안으로 보는 문틈 여유: 문 가운데 x − 0.5(문틈 바깥 면)부터 방 안이다.</summary>
        const float InsideMargin = 0.5f;
        /// <summary>문틈에 걸친 플레이어를 밀어 넣을 때 y를 이 안으로 자른다(문 폭 4 안쪽).</summary>
        const float InsideClampY = 1.5f;
        /// <summary>문이 막힐 때 흔들림(작게).</summary>
        const float SealShake = 0.12f;
        const float SealShakeSeconds = 0.35f;
        /// <summary>처치 뒤 문을 열기까지(처치 히트스톱 0.2 + 느린 화면 1.0초, 실제 시간).</summary>
        const float VictoryHold = 1.2f;
        /// <summary>문 막이가 무너지는 시간(실제 시간).</summary>
        const float CollapseSeconds = 0.5f;
        /// <summary>처치 뒤 등잔을 하나씩 켜는 간격(실제 시간).</summary>
        const float RelightStagger = 0.15f;
        /// <summary>흙더미가 쏟아져 쌓이는 시간(실제 시간).</summary>
        const float RubblePop = 0.3f;

        // 도형 임시판 색(빛을 받음).
        static readonly Color RubbleDirt = new Color(0.24f, 0.2f, 0.16f, 0.95f);
        static readonly Color RubbleStone = new Color(0.46f, 0.43f, 0.4f);
        static readonly Color HoleColor = new Color(0.03f, 0.025f, 0.02f, 0.95f);
        static readonly Color CrackColor = new Color(0.1f, 0.09f, 0.08f, 0.85f);
        static readonly Color ChipColor = new Color(0.5f, 0.47f, 0.43f);
        static readonly Color BasketWood = new Color(0.36f, 0.27f, 0.17f);
        static readonly Color BasketDark = new Color(0.12f, 0.09f, 0.07f);
        static readonly Color RopeColor = new Color(0.45f, 0.38f, 0.26f);
        static readonly Color IronColor = new Color(0.5f, 0.47f, 0.44f);
        static readonly Color ClubWood = new Color(0.33f, 0.24f, 0.15f);

        /// <summary>이 장면의 보스방(없으면 null).</summary>
        public static BossArena Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Instance = null;

        /// <summary>장면 정적 비우기(SceneStatics.Reset이 부른다).</summary>
        public static void ResetStatics() => Instance = null;

        readonly List<ArenaLamp> _lamps = new List<ArenaLamp>();

        DungeonRoot _root;
        DungeonEdge _door;
        GameObject _doorBlock;
        Transform _rubble;
        Transform _props;
        bool _built;
        bool _subscribed;
        /// <summary>방 밖에서 깨웠다: 다음 프레임에 다시 먹는 중으로(깨우는 사건 안에서 체력을 되돌리지 않게).</summary>
        bool _resetPending;
        /// <summary>봉인 중에 쓰러졌다(다시 서면 보스전을 처음으로).</summary>
        bool _downInFight;
        int _potionsAtDown = -1;
        /// <summary>2단계 등잔 꺼짐까지 남은 시간(게임 시간). 음수면 없음.</summary>
        float _lampsOutTimer = -1f;
        Vector2 _remainsAt;
        Stake _endStake;
        OgreRemains _remains;

        /// <summary>보스방 칸(X).</summary>
        public DungeonCell Cell { get; private set; }
        /// <summary>꾸러미 보스 기록 열쇠(자리 표시 Param, 없으면 OgreDen.BossId).</summary>
        public string BossId { get; private set; } = OgreDen.BossId;
        /// <summary>지금 보스(아직 없거나 이번 원정에 이미 잡았으면 null).</summary>
        public Enemy Boss { get; private set; }
        /// <summary>문이 막혀 있는가(보스전 중).</summary>
        public bool Sealed { get; private set; }
        /// <summary>보스전 중인가(문이 막혔고 보스가 살아 있음).</summary>
        public bool FightActive => Sealed && Boss && !Boss.Dead;
        /// <summary>이 장면에서 보스를 쓰러뜨렸는가(같은 원정에 이미 잡아 쓰러진 채 시작한 경우 포함).</summary>
        public bool Defeated { get; private set; }
        /// <summary>벽 등잔 4개(OgreDen.Lamps 차례).</summary>
        public IReadOnlyList<ArenaLamp> Lamps => _lamps;

        /// <summary>보스방 안쪽(월드, 26×14, 방 가운데 기준). 칸이 없으면 이 물체 자리 기준.</summary>
        public Rect Inner
        {
            get
            {
                Vector2 c = Center;
                return new Rect(c.x - OgreDen.InnerHalfWidth, c.y - OgreDen.InnerHalfHeight, OgreDen.InnerWidth, OgreDen.InnerHeight);
            }
        }

        /// <summary>쉼터와 이어진 문 가운데(월드). 문 칸이 없으면 OgreDen.Door 바로 바깥(칸 경계).</summary>
        public Vector2 DoorPoint => _door != null ? _door.DoorCenter : Center + new Vector2(OgreDen.Door.X - DungeonWorld.WallThickness, OgreDen.Door.Y);

        Vector2 Center => Cell != null ? Cell.Center : (Vector2)transform.position;

        Vector2 At(Offset o) => Center + new Vector2(o.X, o.Y);

        /// <summary>보스 자리 표시에서 보스방을 만든다(DungeonContent.Spawn, FeatureKind.Boss). 예외를 던지지 않는다.</summary>
        public static BossArena Create(DungeonRoot root, DungeonCell cell, CellFeature f, Vector2 pos)
        {
            var go = new GameObject("BossArena " + (f != null ? f.Id : ""));
            if (root) go.transform.SetParent(root.transform, false);
            go.transform.position = cell != null ? (Vector3)cell.Center : (Vector3)pos;
            var arena = go.AddComponent<BossArena>();
            arena.Cell = cell;
            if (f != null && !string.IsNullOrEmpty(f.Param)) arena.BossId = f.Param;
            return arena;
        }

        void Awake()
        {
            Instance = this;
            CombatEvents.EnemyWoke += OnEnemyWoke;
            CombatEvents.BossPhaseChanged += OnBossPhase;
            CombatEvents.PlayerDowned += OnPlayerDowned;
            DungeonEvents.PlayerRespawned += OnPlayerRespawned;
            DungeonEvents.BossDefeated += OnBossDefeated;
            _subscribed = true;
        }

        void OnDestroy()
        {
            if (_subscribed)
            {
                CombatEvents.EnemyWoke -= OnEnemyWoke;
                CombatEvents.BossPhaseChanged -= OnBossPhase;
                CombatEvents.PlayerDowned -= OnPlayerDowned;
                DungeonEvents.PlayerRespawned -= OnPlayerRespawned;
                DungeonEvents.BossDefeated -= OnBossDefeated;
                _subscribed = false;
            }
            if (Boss is OgreBrain ogre && ogre) ogre.TransitionStarted -= OnTransitionStarted;
            if (Sealed)
            {
                Telegraph.AboveDark = false;
                var rig = _root ? _root.CameraRig : null;
                if (rig) rig.ClearFixed(0f);
            }
            if (Instance == this) Instance = null;
        }

        // ───────────────────────── 짓기 ─────────────────────────

        void Start()
        {
            _root = DungeonRoot.Instance;
            if (Cell != null) _door = Cell.EdgeOn(OgreDen.DoorSide);
            _props = new GameObject("Den props").transform;
            _props.SetParent(transform, false);
            BuildLamps();
            BuildRatHoles();
            BuildDecor();
            var carry = ProfileCarry.Ensure();
            int expedition = _root ? _root.Expedition : 1;
            if (BossLedger.Present(carry, BossId, expedition)) SpawnBoss();
            else ShowFallen();
            _built = true;
        }

        void BuildLamps()
        {
            _lamps.Clear();
            for (int i = 0; i < OgreDen.Lamps.Length; i++)
                _lamps.Add(ArenaLamp.Create(transform, OgreDen.ArenaLampIdPrefix + (i + 1), At(OgreDen.Lamps[i])));
        }

        /// <summary>오우거: 시작 자리에서 오른쪽 벽을 보고 돌을 씹는 중(OgreBrain.OnSpawned). 보상은 BossReward(NoReward 끔).</summary>
        void SpawnBoss()
        {
            int floor = BossRules.SpawnFloor(_root ? _root.Floor : OgreDen.Floor);
            Boss = EnemySpawner.Create(MonsterKind.Ogre, floor, At(OgreDen.BossStart));
            // 2단계 등잔 꺼짐은 전환 시작(0.0초)부터 센다. 오우거 물체의 사건이라 오우거와 함께 사라진다.
            if (Boss is OgreBrain ogre) ogre.TransitionStarted += OnTransitionStarted;
        }

        /// <summary>같은 원정에 이미 잡았다: 보스 없음, 문 열림, 등잔 켜짐, 쓰러진 몸·줄 끝 말뚝·살펴보기.</summary>
        void ShowFallen()
        {
            Defeated = true;
            Boss = null;
            foreach (var lamp in _lamps)
                if (lamp) lamp.LightUpQuiet();
            Vector2 at = At(OgreDen.BossStart);
            BuildCorpse(at);
            _remainsAt = at;
            SpawnEndStake();
            SpawnRemains();
        }

        // ───────────────────────── 매 프레임 ─────────────────────────

        void Update()
        {
            if (!_built) return;
            if (_resetPending)
            {
                _resetPending = false;
                if (Boss && !Boss.Dead && !Sealed && !Defeated) ResetBoss();
            }
            // 깨는 사건을 놓친 경우(구독 전에 깸 등)도 살핀다.
            if (!Sealed && !Defeated && Boss && !Boss.Dead && Boss.Aware) TryEngage();
            else if (FightActive) CheckLeft();
            TickLampsOut();
        }

        /// <summary>봉인 중인데 플레이어가 방 밖에 있으면(시험 패널 칸 이동 같은 순간 이동) 보스전을 처음으로 돌린다.</summary>
        void CheckLeft()
        {
            var player = PlayerController.Instance;
            if (!player || player.IsDown || Inside(player.Position)) return;
            ResetBoss();
            DungeonEvents.RaiseBossReset(Boss);
        }

        void TickLampsOut()
        {
            if (_lampsOutTimer < 0f) return;
            // 보스전이 끝났거나, 0.3초 전에 전환이 무너짐으로 끊겼으면 거둔다(일어난 뒤 전환을 다시 시작하면 다시 센다).
            if (!FightActive || (Boss is OgreBrain ogre && !ogre.InTransition))
            {
                _lampsOutTimer = -1f;
                return;
            }
            _lampsOutTimer -= Time.deltaTime;
            if (_lampsOutTimer > 0f) return;
            _lampsOutTimer = -1f;
            foreach (int i in OgreDen.Phase2LampsOut)
                if (i >= 0 && i < _lamps.Count && _lamps[i] && _lamps[i].Lit) _lamps[i].Extinguish();
        }

        // ───────────────────────── 봉인 ─────────────────────────

        void OnEnemyWoke(Enemy e)
        {
            if (!_built || !e || e != Boss || Sealed || Defeated) return;
            TryEngage();
        }

        /// <summary>보스가 깼다: 플레이어가 방 안이면 문을 막고 보스전을 시작한다. 밖이면 다음 프레임에 다시 먹는 중으로.</summary>
        void TryEngage()
        {
            if (Sealed || Defeated || !Boss || Boss.Dead) return;
            var player = PlayerController.Instance;
            // 쓰러져 있으면 다시 설 때(PlayerRespawned) 처음으로 돌린다.
            if (!player || player.IsDown) return;
            Vector2 p = player.Position;
            if (!Inside(p))
            {
                _resetPending = true;
                return;
            }
            if (InDoorway(p))
            {
                Vector2 inside = At(OgreDen.PlayerInside);
                inside.y = Mathf.Clamp(p.y, Center.y - InsideClampY, Center.y + InsideClampY);
                player.Teleport(inside);
            }
            Seal();
        }

        /// <summary>방 안인가(문틈 포함: 문 가운데 x − 0.5부터, 칸 세로 범위 안).</summary>
        bool Inside(Vector2 p)
        {
            float doorX = DoorPoint.x;
            float halfH = DungeonWorld.CellHeight * 0.5f;
            float right = Center.x + DungeonWorld.CellWidth * 0.5f;
            return p.x >= doorX - InsideMargin && p.x <= right && Mathf.Abs(p.y - Center.y) <= halfH;
        }

        /// <summary>문 막이 자리에 몸이 걸쳐 있는가.</summary>
        bool InDoorway(Vector2 p) => p.x - PlayerController.Radius < DoorPoint.x + DoorBlockThick * 0.5f + 0.1f;

        void Seal()
        {
            Sealed = true;
            _resetPending = false;
            BuildDoorBlock();
            ScreenShake.Add(SealShake, SealShakeSeconds);
            OgreSounds.Play(OgreSound.Rockfall, 0.8f, 0.9f);
            DungeonEvents.Say(OgreDen.SealLine);
            var rig = _root ? _root.CameraRig : null;
            if (rig)
            {
                var cam = rig.GetComponent<Camera>();
                rig.SetFixed(Center, OgreDen.CameraSize(cam ? cam.aspect : 16f / 9f), OgreDen.CameraBlend);
            }
            Telegraph.AboveDark = true;
            DungeonEvents.RaiseBossEngaged(Boss);
        }

        /// <summary>문틈을 바위로 막고(Wall 충돌·그림자) 방 쪽에 흙더미를 쏟는다(충돌 없음).</summary>
        void BuildDoorBlock()
        {
            RemoveDoorBlock();
            Vector2 door = DoorPoint;
            bool tall = _door == null || _door.DoorSize.y >= _door.DoorSize.x;
            Vector2 size = tall ? new Vector2(DoorBlockThick, DoorBlockLong) : new Vector2(DoorBlockLong, DoorBlockThick);
            _doorBlock = DungeonWorld.Block(transform, "Rock", door, size, Palette.Wall, false);

            Vector2 inward = tall ? Vector2.right : Vector2.up;
            if (Vector2.Dot(Center - door, inward) < 0f) inward = -inward;
            Vector2 along = new Vector2(-inward.y, inward.x);
            _rubble = new GameObject("Door rubble").transform;
            _rubble.SetParent(transform, false);
            _rubble.position = door + inward * 0.75f;
            WorldProps.Shape(_rubble, "Dirt", Vector2.zero, tall ? new Vector2(1.9f, 4.6f) : new Vector2(4.6f, 1.9f), ShapeSprites.Circle, RubbleDirt, DungeonDecor.StainOrder, false);
            int order = WorldProps.SortY(_rubble.position.y);
            float[] spots = { -1.5f, -0.55f, 0.4f, 1.35f };
            for (int i = 0; i < spots.Length; i++)
            {
                Vector2 local = along * spots[i] + inward * (0.15f + 0.25f * (i & 1));
                float s = 0.75f + 0.1f * ((i * 7) % 3);
                WorldProps.Shape(_rubble, "Rubble", local, new Vector2(s, s), ShapeSprites.Rubble(i), RubbleStone, order + i, false, i * 47f);
            }
            for (int i = 0; i < 6; i++)
            {
                Vector2 local = along * (-1.8f + i * 0.72f) + inward * (0.7f + 0.3f * ((i * 5) % 3));
                WorldProps.Shape(_rubble, "Chip", local, new Vector2(0.16f, 0.12f), ShapeSprites.Square, ChipColor, DungeonDecor.PieceOrder, false, i * 31f);
            }
            StartCoroutine(PopIn(_rubble));
        }

        static IEnumerator PopIn(Transform t)
        {
            float time = 0f;
            while (t && time < RubblePop)
            {
                time += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(time / RubblePop);
                float s = Mathf.Lerp(0.25f, 1f, 1f - (1f - k) * (1f - k));
                t.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (t) t.localScale = Vector3.one;
        }

        void RemoveDoorBlock()
        {
            if (_doorBlock) Destroy(_doorBlock);
            _doorBlock = null;
            if (_rubble) Destroy(_rubble.gameObject);
            _rubble = null;
        }

        // ───────────────────────── 2단계 ─────────────────────────

        /// <summary>
        /// 2단계 전환이 시작되면(0.0초 포효) 0.3초 뒤 대각선 등잔 둘(켜져 있으면)이 눕다가 꺼진다(3-5 연출 시간표: 0.3초 등잔 → 0.4초 느린 화면 → 1.0초 첫 C).
        /// 단계 사건(0.4초)이 아니라 전환 시작부터 세어, 어둠이 느린 화면 전에 바뀌고 첫 낙석 예고와 겹치지 않게 한다.
        /// </summary>
        void OnTransitionStarted(OgreBrain ogre)
        {
            if (!ogre || ogre != Boss || !Sealed || Defeated) return;
            _lampsOutTimer = OgreDen.LampsOutDelay;
        }

        /// <summary>1단계로 돌아가면(다시 도전) 남은 등잔 꺼짐을 거둔다.</summary>
        void OnBossPhase(Enemy e, int phase)
        {
            if (!e || e != Boss) return;
            if (phase < 2) _lampsOutTimer = -1f;
        }

        // ───────────────────────── 지면 ─────────────────────────

        void OnPlayerDowned()
        {
            if (!FightActive) return;
            var carry = ProfileCarry.Ensure();
            BossLedger.RecordLoss(carry, BossId);
            _downInFight = true;
            var player = PlayerController.Instance;
            _potionsAtDown = player ? player.Potions : -1;
        }

        /// <summary>
        /// 말뚝에서 다시 섬(DungeonRoot.ReviveRoutine, 화면이 검을 때): 보스전을 처음으로 — 보스 체력·버팀·단계·쥐 처음(다시 먹는 중),
        /// 문 열림, 등잔 모두 꺼짐, 카메라 풀림, 물약은 Tuning.BossRetryFullPotions(켜면 가득, 끄면 쓰러질 때 남은 그대로).
        /// </summary>
        void OnPlayerRespawned()
        {
            bool fought = _downInFight;
            _downInFight = false;
            if (Defeated || !Boss || Boss.Dead) return;
            if (!fought && !Sealed && !Boss.Aware && !_resetPending) return;
            ResetBoss();
            var player = PlayerController.Instance;
            if (fought && player)
            {
                if (Tuning.BossRetryFullPotions) player.Refill();
                else if (_potionsAtDown >= 0) player.RestoreVitals(-1, _potionsAtDown);
            }
            _potionsAtDown = -1;
            DungeonEvents.RaiseBossReset(Boss);
        }

        /// <summary>보스·문·등잔·카메라·예고를 처음으로(다시 먹는 중).</summary>
        void ResetBoss()
        {
            _resetPending = false;
            _lampsOutTimer = -1f;
            if (Boss is OgreBrain ogre) ogre.ResetFight();
            else if (Boss) Boss.ResetToHome();
            bool wasSealed = Sealed;
            RemoveDoorBlock();
            Sealed = false;
            if (!wasSealed) return;
            foreach (var lamp in _lamps)
                if (lamp) lamp.ResetUnlit();
            var rig = _root ? _root.CameraRig : null;
            if (rig)
            {
                rig.ClearFixed(0f);
                rig.Snap();
            }
            Telegraph.AboveDark = false;
        }

        // ───────────────────────── 이기면 ─────────────────────────

        void OnBossDefeated(Enemy e, bool first)
        {
            if (!e || e != Boss || Defeated) return;
            Defeated = true;
            _lampsOutTimer = -1f;
            _resetPending = false;
            Vector2 facing = e.FacingDirection;
            _remainsAt = e.Position + (facing.sqrMagnitude > 0.0001f ? facing.normalized * 0.6f : Vector2.zero);
            StartCoroutine(VictoryRoutine());
        }

        /// <summary>처치 느린 화면 뒤: 카메라 풀기·예고 바닥으로, 등잔 하나씩 켜기, 문 막이 무너짐 → 열림 글, 줄 끝 말뚝, 살펴보기.</summary>
        IEnumerator VictoryRoutine()
        {
            float t = 0f;
            while (t < VictoryHold)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Telegraph.AboveDark = false;
            var rig = _root ? _root.CameraRig : null;
            if (rig) rig.ClearFixed(OgreDen.CameraBlend);
            StartCoroutine(RelightAll());
            if (_doorBlock) yield return CollapseDoor();
            Sealed = false;
            DungeonEvents.Say(OgreDen.OpenLine);
            SpawnEndStake();
            SpawnRemains();
        }

        IEnumerator RelightAll()
        {
            foreach (var lamp in _lamps)
            {
                if (lamp && !lamp.Lit)
                {
                    lamp.LightUp();
                    float t = 0f;
                    while (t < RelightStagger)
                    {
                        t += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
            }
        }

        /// <summary>문 막이가 떨리며 무너져 내린다(그림이 흐려짐) → 지우고 길이 열렸다고 알린다(흙먼지·시야 모서리). 흙더미는 바닥에 남는다.</summary>
        IEnumerator CollapseDoor()
        {
            ScreenShake.Add(0.1f, 0.4f);
            OgreSounds.Play(OgreSound.Rockfall, 0.7f, 1.15f);
            var block = _doorBlock;
            var visual = block ? block.transform.Find("Visual") : null;
            var sr = visual ? visual.GetComponent<SpriteRenderer>() : null;
            Color baseColor = sr ? sr.color : Color.white;
            float t = 0f;
            while (block && t < CollapseSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / CollapseSeconds);
                if (visual) visual.localPosition = (Vector3)(Random.insideUnitCircle * 0.07f * (1f - k)) + Vector3.down * (0.3f * k);
                if (sr) sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - k));
                yield return null;
            }
            if (block) Destroy(block);
            if (_doorBlock == block) _doorBlock = null;
            if (_rubble) _rubble.localScale = new Vector3(1f, 0.8f, 1f);
            if (_door != null) DungeonEvents.RaiseEdgeOpened(_door);
        }

        /// <summary>줄 끝 말뚝(X 오른쪽 벽 가까이, 켜진 채): 바구니로 올라가기. 한 번 받는 것이 아니라 경험치·발견 없이 켠다.</summary>
        void SpawnEndStake()
        {
            if (_endStake) return;
            var f = new CellFeature { Kind = FeatureKind.Stake, Id = OgreDen.EndStakeId, Local = OgreDen.EndStake, Label = OgreDen.EndStakeLabel };
            Vector2 pos = At(OgreDen.EndStake);
            var state = _root ? _root.State : null;
            if (state != null) state.Register(f.Id, DiscoveryKind.Stake, Cell, pos, f.Label).Done = true;
            _endStake = Stake.Create(Cell, f, pos);
        }

        void SpawnRemains()
        {
            if (_remains) return;
            var go = new GameObject("Ogre remains");
            go.transform.SetParent(transform, false);
            go.transform.position = _remainsAt;
            _remains = go.AddComponent<OgreRemains>();
        }

        // ───────────────────────── 도형 임시판 ─────────────────────────

        /// <summary>쥐 구멍 4곳: 벽 면에 걸친 검은 틈, 둘레 금, 바닥에 갉아 낸 돌 부스러기(빛 받음, 충돌 없음).</summary>
        void BuildRatHoles()
        {
            float face = DungeonWorld.CellHeight * 0.5f - DungeonWorld.WallThickness * 0.5f;
            for (int i = 0; i < OgreDen.RatHoles.Length; i++)
            {
                var o = OgreDen.RatHoles[i];
                float sign = o.Y >= 0f ? 1f : -1f;
                var root = new GameObject("Rat hole " + (i + 1)).transform;
                root.SetParent(_props, false);
                root.position = new Vector2(Center.x + o.X, Center.y + sign * face);
                WorldProps.Shape(root, "Hole", new Vector2(0f, -sign * 0.05f), new Vector2(0.95f, 0.5f), ShapeSprites.Circle, HoleColor, WorldProps.WallDetailOrder + 1, false);
                WorldProps.Stroke(root, "Crack", new Vector2(-0.35f, sign * 0.1f), new Vector2(-0.6f, sign * 0.45f), 0.05f, CrackColor, WorldProps.WallDetailOrder, false);
                WorldProps.Stroke(root, "Crack", new Vector2(0.3f, sign * 0.12f), new Vector2(0.55f, sign * 0.4f), 0.05f, CrackColor, WorldProps.WallDetailOrder, false);
                WorldProps.Stroke(root, "Crack", new Vector2(0.05f, sign * 0.22f), new Vector2(0.1f, sign * 0.6f), 0.04f, CrackColor, WorldProps.WallDetailOrder, false);
                for (int k = 0; k < 4; k++)
                {
                    Vector2 chip = new Vector2(-0.45f + k * 0.3f, -sign * (0.45f + 0.12f * (k & 1)));
                    WorldProps.Shape(root, "Chip", chip, new Vector2(0.1f, 0.08f), ShapeSprites.Square, ChipColor, DungeonDecor.PieceOrder, false, k * 40f);
                }
            }
        }

        /// <summary>장식 4가지(3-6): 부서진 권양기 바구니, 사슬 더미, 뼈 더미, 갉아 먹힌 벽 자국. 빛 받음, 충돌 없음.</summary>
        void BuildDecor()
        {
            // 부서진 권양기 바구니: 기운 나무 상자, 살 셋, 떨어져 나간 판자, 끊긴 줄.
            var basket = new GameObject("Broken basket").transform;
            basket.SetParent(_props, false);
            basket.position = At(OgreDen.BasketDecor);
            int order = WorldProps.SortY(basket.position.y);
            WorldProps.Shape(basket, "Frame", Vector2.zero, new Vector2(1.25f, 0.9f), ShapeSprites.Square, BasketWood, order, false, 14f);
            WorldProps.Shape(basket, "Inside", new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.62f), ShapeSprites.Square, BasketDark, order + 1, false, 14f);
            for (int i = 0; i < 3; i++)
            {
                Vector2 a = WorldProps.Rotate(new Vector2(-0.45f + i * 0.45f, -0.32f), 14f);
                Vector2 b = WorldProps.Rotate(new Vector2(-0.45f + i * 0.45f, 0.32f), 14f);
                WorldProps.Stroke(basket, "Slat", a, b, 0.07f, BasketWood, order + 2, false);
            }
            WorldProps.Shape(basket, "Loose plank", new Vector2(0.95f, -0.55f), Vector2.one, ShapeSprites.BrokenPlank, BasketWood, order + 2, false, -28f);
            WorldProps.Stroke(basket, "Rope", new Vector2(-0.55f, 0.4f), new Vector2(-1.1f, 0.85f), 0.06f, RopeColor, order + 2, false);
            WorldProps.Stroke(basket, "Rope", new Vector2(-1.1f, 0.85f), new Vector2(-1.25f, 1.45f), 0.06f, RopeColor, order + 2, false);

            // 사슬 더미: 사슬 토막 셋이 엉키고 쇠고리 하나.
            var chain = new GameObject("Chain heap").transform;
            chain.SetParent(_props, false);
            chain.position = At(OgreDen.ChainDecor);
            WorldProps.Shape(chain, "Chain", new Vector2(-0.2f, 0f), new Vector2(1.1f, 1.1f), ShapeSprites.Chain, IronColor, DungeonDecor.PieceOrder, false, 20f);
            WorldProps.Shape(chain, "Chain", new Vector2(0.25f, -0.15f), Vector2.one, ShapeSprites.Chain, IronColor, DungeonDecor.PieceOrder + 1, false, -35f);
            WorldProps.Shape(chain, "Chain", new Vector2(0.05f, 0.2f), new Vector2(0.9f, 0.9f), ShapeSprites.Chain, IronColor, DungeonDecor.PieceOrder + 1, false, 95f);
            WorldProps.Shape(chain, "Shackle", new Vector2(0.65f, 0.15f), new Vector2(0.38f, 0.38f), ShapeSprites.Ring, IronColor, DungeonDecor.PieceOrder + 2, false);

            // 뼈 더미: 뼈 다섯과 해골 하나(굴의 먹이 자리).
            var bones = new GameObject("Bone pile").transform;
            bones.SetParent(_props, false);
            bones.position = At(OgreDen.BoneDecor);
            float[] angles = { 15f, 120f, 70f, 160f, 40f };
            Vector2[] spots = { new Vector2(-0.4f, 0.1f), new Vector2(0.1f, 0.25f), new Vector2(0.35f, -0.2f), new Vector2(-0.15f, -0.3f), new Vector2(0.55f, 0.3f) };
            for (int i = 0; i < angles.Length; i++)
                WorldProps.Shape(bones, "Bone", spots[i], new Vector2(0.9f, 0.9f), ShapeSprites.Bone, Palette.DungeonBone, DungeonDecor.PieceOrder + (i & 1), false, angles[i]);
            WorldProps.Shape(bones, "Skull", new Vector2(-0.6f, -0.35f), Vector2.one, ShapeSprites.Skull, Palette.DungeonBone, DungeonDecor.PieceOrder + 2, false, 200f);

            // 갉아 먹힌 벽 자국: 아래 벽 면에 반달 이빨 자국이 줄지어 있고 바닥에 돌 부스러기.
            var gnaw = new GameObject("Gnawed wall").transform;
            gnaw.SetParent(_props, false);
            Vector2 g = At(OgreDen.GnawDecor);
            float sign = OgreDen.GnawDecor.Y >= 0f ? 1f : -1f;
            float face = DungeonWorld.CellHeight * 0.5f - DungeonWorld.WallThickness * 0.5f;
            gnaw.position = new Vector2(g.x, Center.y + sign * face);
            for (int i = 0; i < 5; i++)
            {
                Vector2 bite = new Vector2(-1.1f + i * 0.55f, sign * 0.02f);
                WorldProps.Shape(gnaw, "Bite", bite, new Vector2(0.42f, 0.26f), ShapeSprites.Circle, CrackColor, WorldProps.WallDetailOrder, false);
            }
            for (int i = 0; i < 6; i++)
            {
                Vector2 chip = new Vector2(-1.2f + i * 0.48f, -sign * (0.35f + 0.18f * (i % 3)));
                WorldProps.Shape(gnaw, "Chip", chip, new Vector2(0.13f, 0.1f), ShapeSprites.Square, ChipColor, DungeonDecor.PieceOrder, false, i * 33f);
            }
        }

        /// <summary>같은 원정에 이미 잡은 오우거의 쓰러진 몸(도형 임시판): 앞으로 엎어진 몸, 머리, 등에 박힌 곡괭이 자루 셋, 옆에 버팀목 몽둥이.</summary>
        void BuildCorpse(Vector2 at)
        {
            var body = new GameObject("Fallen ogre").transform;
            body.SetParent(_props, false);
            body.position = at;
            int order = WorldProps.SortY(at.y);
            WorldProps.Shape(body, "Shadow", new Vector2(0.1f, -0.15f), new Vector2(3.0f, 2.2f), ShapeSprites.Circle, new Color(0f, 0f, 0f, 0.35f), DungeonDecor.StainOrder, false);
            WorldProps.Shape(body, "Body", Vector2.zero, new Vector2(2.5f, 2.1f), ShapeSprites.Circle, GoreColors.OgreSkinDark, order, false);
            WorldProps.Shape(body, "Head", new Vector2(1.25f, 0.05f), new Vector2(0.95f, 0.9f), ShapeSprites.Circle, GoreColors.OgreSkin, order + 1, false);
            WorldProps.Stroke(body, "Arm", new Vector2(0.6f, 0.75f), new Vector2(1.7f, 1.05f), 0.38f, GoreColors.OgreSkin, order + 1, false);
            WorldProps.Stroke(body, "Arm", new Vector2(0.6f, -0.75f), new Vector2(1.7f, -1.05f), 0.38f, GoreColors.OgreSkin, order + 1, false);
            for (int i = 0; i < 3; i++)
            {
                Vector2 a = new Vector2(-0.55f + i * 0.2f, -0.45f + i * 0.45f);
                WorldProps.Stroke(body, "Pick handle", a, a + new Vector2(-0.75f, 0.12f - i * 0.1f), 0.1f, ClubWood, order + 2, false);
            }
            WorldProps.Stroke(body, "Club", new Vector2(1.4f, -1.6f), new Vector2(3.8f, -2.1f), 0.35f, ClubWood, order - 1, false);
        }
    }

    /// <summary>쓰러진 오우거 '살펴보기'(3-1): F마다 OgreDen.RemainsLines를 차례로 띄운다. 몸이 커서 조금 멀리서도 쓴다.</summary>
    sealed class OgreRemains : Interactable
    {
        int _next;

        public override float Range => 2.6f;
        public override string Prompt => OgreDen.RemainsPrompt;

        public override void Interact()
        {
            var lines = OgreDen.RemainsLines;
            if (lines == null || lines.Length == 0) return;
            DungeonEvents.Say(lines[_next % lines.Length]);
            _next++;
        }
    }
}
