using System.Collections;
using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Time;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 첫 보스 '갱도 오우거'(기획/전투-보스-무기-다듬기-1차.md 3장, 결정 ④ = 전투 시험장 '보스' 프리셋 먼저).
    /// BoarBrain 틀: 상태 Sleep(먹는 중)/Wake/Walk/Slam(A)/Charge(B)/Sweep(D)/Roar(C)/Transition/Broken. 수치·고르기는 BossRules, 반원 판정은 SectorMath.
    /// 넉백 저항 1, 준비가 끊기지 않음(Interruptible false, ThinkWhileBusy true), 공격 기회를 쓰지 않음(큰 예고는 StrongAttackSchedule로 0.3초 엇갈림),
    /// 보스 피해 능력치를 받음(IsBoss). 스폰 뒤 OnSpawned에서 SetupBoss(BossRules.Poise(층), 3.0초, 질량 50)·체력 BossRules.Hp(층)·공격 BossRules.Attack(층)을 다시 넣는다.
    /// 모든 패턴은 빨간 바닥 예고가 다 차는 순간에 판정한다(예고 진행을 두뇌가 직접 넣어 어긋나지 않음). 패턴마다 CombatEvents.RaiseTelegraph(Avoidable, hit).
    /// 고르기는 쉬는 동안에만(숨 고르기 0.9~1.3초, 2단계 −0.3). A·D는 플레이어가 몸 앞 50° 안일 때만 시작하고, 밖이면 몸을 돌리며(초당 120°/160°) 기다린다.
    /// 2단계: 체력 50% 아래로 내려간 뒤 지금 패턴·무너짐이 끝나면 1.2초 전환(포효·흔들림 → 0.4초 느린 화면·흰 눈·회백 테두리 → 1.0초 첫 C).
    /// 무너짐 3초(받는 피해 +30%, Enemy가 넣음): 기습(깬 뒤 처음 무너짐이 기습 타 6초 안)·기둥(B 마지막 돌진이 벽·기둥에 박힘)·압박(그 밖)으로 원인을 적는다.
    /// 몸은 도형 임시판(OgreLook), 소리는 OgreSounds(코드 임시음), 보상은 BossReward(시험장은 NoReward), 한 판 기록은 BossFightLog.
    /// 시험 패널(꾸러미 ⑧)이 부르는 공개 값·함수: Current, Phase, CurrentPattern, StateLabel, ForcePattern, SetHpFraction, ResetFight.
    /// 방패(기획/세-무기-우클릭-소켓-1차.md 2-7): 내려찍기 BossSlam, 돌진 BossRush(진행 방향 = 돌진 방향), 휩쓸기 BossSweep(진행 방향 = 쓸어 내는 쪽), 낙석 FromAbove로 넘긴다.
    /// 휩쓸기만 패링되고, 끊기지 않고 버팀 6%만 깎는다(Parried).
    /// </summary>
    public sealed class OgreBrain : Enemy
    {
        /// <summary>깬 뒤 처음 무너짐을 '기습' 덕으로 셀 시간(기습 타가 버팀 70%를 깎고 이어지는 타가 무너뜨린다).</summary>
        const float AmbushCredit = 6f;
        /// <summary>걷다가 멈추는 거리(몸 반지름 1.2 + 플레이어 0.4 + 조금, BossRules.StopDistance). D(2.5)·A(3.0) 안이다.</summary>
        const float StopDistance = BossRules.StopDistance;
        /// <summary>몸이 플레이어를 이 각도 안으로 보면 제 속도로 걷는다(밖이면 0.35배로 돌며 걷는다).</summary>
        const float WalkFacing = 25f;
        /// <summary>돌진 벽 판정: 몸 반지름 × 0.9 원을 앞으로 쏘고, 정면에 가까운(법선이 −방향과 69° 안) 벽만 박힘으로 본다.</summary>
        const float WallHeadOn = -0.35f;
        const float WallProbe = 0.06f;
        /// <summary>걷기 앞길 살피기 거리(기둥에 막히면 옆으로 비켜 돎).</summary>
        const float WalkProbe = 3f;
        /// <summary>깨어난 뒤 첫 고르기까지(깨어남 1.0초 뒤).</summary>
        const float FirstRest = 0.2f;
        /// <summary>몽둥이 휘두름: 빠른 획(초당 도)과 자세 사이 옮김.</summary>
        const float ClubStrikeSpeed = 1800f;
        const float ClubPoseSpeed = 420f;

        /// <summary>지금 살아 있는 오우거(시험 패널·이름표가 읽음). 없으면 null.</summary>
        public static OgreBrain Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOgreStatics()
        {
            Current = null;
            PillarBroken = null;
        }

        /// <summary>오우거가 기둥·벽에 박혀 무너짐(무너짐 원인 '기둥'). 의뢰 '기둥 앞에 서라'(QuestTracker)가 듣는다(묶음 3 나-10).</summary>
        public static event System.Action PillarBroken;

        public override bool IsBoss => true;

        enum State
        {
            Sleep,
            Wake,
            Walk,
            Approach,
            Slam,
            Charge,
            Sweep,
            Roar,
            Transition,
            Broken,
            Down,
        }

        enum Step
        {
            None,
            Windup,
            Recover,
            Aim,
            Run,
            Drag,
            Stumble,
            Breath,
            RoarUp,
            Rocks,
        }

        enum BreakCause
        {
            None,
            Ambush,
            Pillar,
            Pressure,
        }

        /// <summary>단계 1·2(체력 50% 아래로 내려간 뒤 패턴·무너짐이 끝나면 2).</summary>
        public int Phase { get; private set; } = 1;
        /// <summary>지금 쓰는 패턴(쉬는 중·걷는 중이면 null).</summary>
        public BossPattern? CurrentPattern { get; private set; }
        /// <summary>깨어날 때마다 1씩 오르는 판 번호(이름표가 처음 깰 때 연출을 이 값으로 고른다). 아직 안 깼으면 0.</summary>
        public int FightId { get; private set; }
        /// <summary>지금 판이 진행 중인가(깬 뒤 처치·다시 도전 전까지).</summary>
        public bool FightActive => _fightActive;
        /// <summary>2단계 전환 연출 중인가(0.0~1.0초, 첫 C 전까지). 전환이 무너짐으로 끊기면 false.</summary>
        public bool InTransition => _state == State.Transition;
        /// <summary>
        /// 2단계 전환 연출이 시작될 때(0.0초, 몽둥이를 꽂고 포효). 단계 사건(CombatEvents.BossPhaseChanged)은 0.4초(느린 화면)에 나가므로,
        /// 3-5 '0.3초에 대각선 등잔이 꺼짐'은 이 사건부터 센다(BossArena). 이 오우거 물체가 사라지면 함께 사라지는 사건이라 정적 비우기가 필요 없다.
        /// </summary>
        public event System.Action<OgreBrain> TransitionStarted;
        /// <summary>이번 판 시간(초, 게임 시간). 판이 없으면 0.</summary>
        public float FightTime => _fightActive ? Time.time - _fightStart : 0f;
        /// <summary>이번 판 무너짐 횟수.</summary>
        public int FightBreaks => _stats.Breaks;
        /// <summary>
        /// 이번(또는 마지막) 판에 플레이어가 가장 많이 맞은 패턴(같으면 돌진 → 내려찍기 → 휩쓸기 → 포효 차례). 맞은 적이 없으면 null.
        /// 쓰러졌을 때 무진 반응 줄(rx:ogre_lost, 전투 문서 3-7 '가장 많이 맞은 패턴을 가리킴')을 고르는 데 쓴다.
        /// </summary>
        public BossPattern? MostHitPattern => _stats.MostHit();

        // ── 몸 그림(OgreLook)이 읽는 자세 값 ──

        /// <summary>몽둥이 방향(몸 앞 기준 도, +가 왼쪽). 오른 어깨 피벗에서 돌린다.</summary>
        public float ClubAngle { get; private set; } = -35f;
        /// <summary>몽둥이가 보이는 길이 비율(1 = 바닥과 나란함, 머리 위로 들면 짧아짐).</summary>
        public float ClubReach { get; private set; } = 1f;
        /// <summary>왼팔·몸을 드는 정도(0~1, 포효·내려찍기 준비).</summary>
        public float ArmLift { get; private set; }
        /// <summary>앞으로 숙인 정도(0~1, 돌진).</summary>
        public float Lean { get; private set; }

        /// <summary>시험 패널 한 줄용 상태 이름(예: '먹는 중', '내려찍기 예고').</summary>
        public string StateLabel
        {
            get
            {
                if (Dead || _state == State.Down) return "쓰러짐";
                if (!Aware) return IsEating ? "먹는 중" : "잠듦";
                if (Broken) return "무너짐";
                switch (_state)
                {
                    case State.Wake: return "깨어남";
                    case State.Walk: return _rest > 0f ? "숨 고르기" : "다가감";
                    case State.Approach: return "다가가며 내려찍기";
                    case State.Slam: return _step == Step.Windup ? "내려찍기 예고" : "몽둥이 빼는 중";
                    case State.Charge:
                        switch (_step)
                        {
                            case Step.Aim: return _chargeLast ? "마지막 돌진 조준" : "돌진 조준";
                            case Step.Run: return _chargeLast ? "마지막 돌진" : "돌진";
                            case Step.Drag: return "발 끌기";
                            case Step.Stumble: return "비틀거림";
                            default: return "돌진 뒤 숨 고르기";
                        }
                    case State.Sweep: return _step == Step.Recover ? "휩쓸기 틈" : "휩쓸기 예고";
                    case State.Roar: return _step == Step.Rocks ? "낙석" : "포효";
                    case State.Transition: return "2단계로";
                    case State.Broken: return "무너짐";
                    default: return "가만히 섬";
                }
            }
        }

        State _state = State.Sleep;
        Step _step;
        float _timer;
        float _rest;
        float _cdSlam;
        float _cdCharge;
        float _cdSweep;
        float _cdRoar;
        BossPattern? _last;
        BossPattern? _forced;
        bool _phasePending;
        bool _rocksDropped;
        bool _slowDone;

        Telegraph _telegraph;
        Telegraph _telegraph2;
        float _reserved1 = -1f;
        float _reserved2 = -1f;

        Vector2 _slamCenter;
        float _windup;

        int _chargeIndex;
        int _chargeCount;
        bool _chargeLast;
        bool _chargeHit;
        float _chargeTraveled;
        Vector2 _chargeDir;
        Vector2 _chargeStart;

        Vector2 _sweepDir1;
        Vector2 _sweepDir2;
        float _sweep2Start;
        float _sweep2Time;
        bool _sweep1Done;
        bool _sweep2Started;
        float _recoverAt;
        /// <summary>이번 휩쓸기 타가 방패에 튕겼다(Parried(BossSweep)). 판정 자리의 계측·다음 상태가 정해진 뒤 버팀 6%를 넣는다(FlushSweepParry).</summary>
        bool _sweepParried;

        readonly List<Telegraph> _rocks = new List<Telegraph>(BossRules.RockCount);
        readonly List<bool> _rockThreat = new List<bool>(BossRules.RockCount);
        readonly List<Vector2> _rockSpots = new List<Vector2>(BossRules.RockCount);
        readonly List<Enemy> _rats = new List<Enemy>(BossRules.MaxRats);

        OgreLook _look;
        bool _fightActive;
        float _fightStart;
        float _ambushAt = -999f;
        BreakCause _pendingCause;
        float _breakStart = -1f;
        int _lastPotions;
        float _liveAt;
        readonly FightStats _stats = new FightStats();

        /// <summary>패턴 강제(시험 패널 3-10): 다음 고르기부터 이 패턴만 쓴다(null이면 표대로). Tuning.BossForcePattern(≥ 0)과 같은 뜻이고 이 값이 앞선다.</summary>
        public void ForcePattern(BossPattern? pattern) => _forced = pattern;

        /// <summary>체력을 최대치의 이 비율로 맞춘다(시험 패널 '체력 50%로'). 50%에 맞추면 다음 한 대에 2단계 조건(50% 아래)이 된다.</summary>
        public void SetHpFraction(float fraction)
        {
            if (Dead || Health == null) return;
            Health.SetCurrent(Mathf.RoundToInt(Health.Max * Mathf.Clamp01(fraction)));
        }

        /// <summary>
        /// 처음 상태로 되돌린다(재도전: 체력·버팀·단계·쥐 처음, 제자리에서 다시 먹는 중). 진행 중이던 판은 '그만둠' 한 줄로 남긴다.
        /// 남은 출혈(도끼)도 거둔다: 남겨 두면 먹는 중인 몸에 틱이 들어가 기습처럼 깨우고(Enemy.TakeHit), 깬 포효·이름표 연출과 '그만둠' 줄이 한 번 더 나온다.
        /// </summary>
        public void ResetFight()
        {
            if (Dead) return;
            if (_fightActive) FinishFight(false);
            CancelAll();
            RemoveRats();
            EnemyBleed.Clear(this);
            bool wasPhase2 = Phase >= 2;
            // 칸 규칙(OnSpawned에서 넓게 묶음)의 다시 섬: 하던 것·무너짐을 지우고 제자리로 옮겨 다시 쉰다(먹던 그대로).
            ResetToHome();
            SetupBoss(BossRules.Poise(Floor), BossRules.BreakSeconds, BossRules.Mass);
            Health.Init(BossRules.Hp(Floor), 0);
            SetBaseMoveSpeed(BossRules.WalkSpeed);
            Phase = 1;
            _phasePending = false;
            _cdSlam = _cdCharge = _cdSweep = _cdRoar = 0f;
            _last = null;
            _state = State.Sleep;
            _step = Step.None;
            CurrentPattern = null;
            _ambushAt = -999f;
            _pendingCause = BreakCause.None;
            _breakStart = -1f;
            ClubAngle = -35f;
            ClubReach = 1f;
            ArmLift = 0f;
            Lean = 0f;
            Eat(HomeFacing);
            if (wasPhase2) CombatEvents.RaiseBossPhaseChanged(this, 1);
        }

        /// <summary>
        /// 재도전(3-8): ResetFight에 더해 플레이어를 시작 자리((−9, 0) 쪽)로 옮기고 체력을 채운다.
        /// 물약은 Tuning.BossRetryFullPotions가 켜져 있으면 3병, 꺼져 있으면 남은 그대로.
        /// </summary>
        public void ResetFight(bool resetPlayer)
        {
            ResetFight();
            var p = Player;
            if (!resetPlayer || !p || Dead) return;
            int potions = p.Potions;
            p.Refill();
            if (!Tuning.BossRetryFullPotions) p.RestoreVitals(-1, potions);
            p.Teleport(HomePosition + new Vector2(BossRules.PlayerStartX - BossRules.StartX, 0f));
        }

        protected override bool Interruptible => false;

        protected override bool ThinkWhileBusy => true;

        protected override void OnSpawned()
        {
            Current = this;
            SetupBoss(BossRules.Poise(Floor), BossRules.BreakSeconds, BossRules.Mass);
            Health.Init(BossRules.Hp(Floor), 0);
            SetAttackPower(BossRules.Attack(Floor));
            SetBaseMoveSpeed(BossRules.WalkSpeed);
            // 다시 섬(ResetToHome)을 쓰려고 칸에 묶는다. 경계는 아주 넓게 두어 놓아주기·경계 밖 회피는 걸리지 않는다(보스는 문이 막혀 나갈 수 없음, 3-6).
            BindToTerritory(new Rect(Position - new Vector2(200f, 200f), new Vector2(400f, 400f)), Position, Vector2.right);
            // 3-6: 오른쪽 벽을 보고 돌을 씹는 중(먹는 중 = 잠 규칙: 앞 6, 등 뒤 3 × 소음 배율, 0.5초 뒤 깸, 맞으면 바로 깸).
            Eat(Vector2.right);
            // 전투 시험장은 보상 없이 기록만(3-8).
            if (CombatTestRoot.Instance) NoReward = true;
            Health.Died += OnOgreDied;
            Health.Damaged += OnOgreDamaged;
            CombatEvents.PlayerDowned += OnPlayerDowned;

            var lookGo = new GameObject("OgreLook");
            lookGo.layer = gameObject.layer;
            lookGo.transform.SetParent(transform, false);
            _look = lookGo.AddComponent<OgreLook>();
            _look.Bind(this);
        }

        protected override void OnRemoved()
        {
            CombatEvents.PlayerDowned -= OnPlayerDowned;
            if (_fightActive && !Dead) FinishFight(false);
            if (Current == this) Current = null;
        }

        void OnOgreDamaged(int amount, bool crit)
        {
            // 기습 타: 맞기 전에 쉬고 있었다(깨우기는 TakeHit 끝에서 일어난다).
            if (!Aware) _ambushAt = Time.time;
        }

        void OnPlayerDowned()
        {
            if (_fightActive) _stats.Downs++;
        }

        protected override void Think(float dt)
        {
            var player = Player;
            bool playerUp = player && !player.IsDown;
            Vector2 to = playerUp ? player.Position - Position : Vector2.zero;
            float dist = to.magnitude;
            ExtraJitter = Vector2.zero;

            _cdSlam -= dt;
            _cdCharge -= dt;
            _cdSweep -= dt;
            _cdRoar -= dt;
            TrackFight(player);
            CheckPhase();

            if (_state == State.Sleep) BeginWake();
            else if (_state == State.Broken) StandUp();

            switch (_state)
            {
                case State.Wake:
                    SetPose(EnemyPose.Windup);
                    DesiredVelocity = Vector2.zero;
                    _timer += dt;
                    if (playerUp) TurnTowards(to, dt, 1.5f);
                    Club(-35f, 1f, ClubPoseSpeed, dt);
                    ArmLift = Mathf.MoveTowards(ArmLift, _timer < BossRules.WakeTime * 0.7f ? 1f : 0f, dt * 4f);
                    if (_timer >= BossRules.WakeTime) EnterRest(FirstRest);
                    break;

                case State.Walk:
                    TickWalk(dt, playerUp, to, dist);
                    break;

                case State.Approach:
                    SetPose(EnemyPose.Locomotion);
                    _timer += dt;
                    if (playerUp)
                    {
                        TurnTowards(to, dt, 1f);
                        DesiredVelocity = dist > StopDistance ? Facing * MoveSpeed : Vector2.zero;
                    }
                    else DesiredVelocity = Vector2.zero;
                    Club(-35f, 1f, ClubPoseSpeed, dt);
                    if (_timer >= BossRules.ApproachSeconds)
                    {
                        if (playerUp) BeginSlam(to);
                        else EnterRest(0f);
                    }
                    break;

                case State.Slam:
                    TickSlam(dt, player, playerUp);
                    break;

                case State.Charge:
                    TickCharge(dt, player, playerUp, to);
                    break;

                case State.Sweep:
                    TickSweep(dt, player, playerUp, to);
                    break;

                case State.Roar:
                    TickRoar(dt, player, playerUp);
                    break;

                case State.Transition:
                    TickTransition(dt);
                    break;

                default:
                    DesiredVelocity = Vector2.zero;
                    break;
            }
        }

        // ───────────────────────── 깸·쉼·걷기 ─────────────────────────

        void BeginWake()
        {
            _state = State.Wake;
            _step = Step.None;
            _timer = 0f;
            CurrentPattern = null;
            if (!_fightActive) StartFight();
            OgreSounds.Play(OgreSound.Roar, 0.9f);
            ScreenShake.Add(0.08f, 0.5f);
        }

        void StartFight()
        {
            _fightActive = true;
            _fightStart = Time.time;
            FightId++;
            _stats.Reset();
            var p = Player;
            _lastPotions = p ? p.Potions : 0;
            _liveAt = 0f;
        }

        void EnterRest(float rest)
        {
            _state = State.Walk;
            _step = Step.None;
            _timer = 0f;
            _rest = rest;
            CurrentPattern = null;
        }

        void EndPattern()
        {
            EnterRest(BossRules.Rest(Phase >= 2, Random.value));
        }

        void TickWalk(float dt, bool playerUp, Vector2 to, float dist)
        {
            ArmLift = Mathf.MoveTowards(ArmLift, 0f, dt * 3f);
            Lean = Mathf.MoveTowards(Lean, 0f, dt * 3f);
            if (!playerUp)
            {
                DesiredVelocity = Vector2.zero;
                SetPose(EnemyPose.Idle);
                Club(-35f, 1f, ClubPoseSpeed, dt);
                return;
            }
            float angle = TurnTowards(to, dt, 1f);
            SetPose(EnemyPose.Locomotion);
            DesiredVelocity = dist > StopDistance ? WalkDirection(to, dist) * (MoveSpeed * (angle <= WalkFacing ? 1f : 0.35f)) : Vector2.zero;
            // 걸을 때 몽둥이가 조금 흔들린다.
            float sway = DesiredVelocity.sqrMagnitude > 0.01f ? Mathf.Sin(Time.time * 5.5f) * 6f : 0f;
            Club(-35f + sway, 1f, ClubPoseSpeed, dt);

            // 2단계는 지금 패턴·무너짐이 끝나는 대로 넘어간다(숨 고르기를 기다리지 않음, 3-5).
            if (_phasePending)
            {
                BeginTransition();
                return;
            }
            _rest -= dt;
            if (_rest > 0f) return;
            var choice = Choose(dist);
            if (!choice.Pattern.HasValue) return;
            if (choice.ApproachThenSlam)
            {
                // 창 뒷걸음 찌르기 꼼수 막기: 0.4초 다가간 뒤 A.
                _state = State.Approach;
                _step = Step.None;
                _timer = 0f;
                CurrentPattern = BossPattern.Slam;
                return;
            }
            switch (choice.Pattern.Value)
            {
                case BossPattern.Slam:
                    if (angle <= BossRules.FrontGate) BeginSlam(to);
                    break;
                case BossPattern.Sweep:
                    if (angle <= BossRules.FrontGate) BeginSweep(to);
                    break;
                case BossPattern.Charge:
                    BeginCharge(to);
                    break;
                default:
                    BeginRoar();
                    break;
            }
        }

        /// <summary>
        /// 걷는 방향: 보통은 몸이 보는 쪽. 기둥·벽이 플레이어 쪽 앞길을 막으면 그 옆으로 비켜 돈다
        /// (기둥 뒤 3~4 거리에 선 플레이어와 서로 마주 밀기만 하며 멈추는 교착 방지, 통합 플레이 확인에서 발견). 몸은 그대로 플레이어를 본다.
        /// 기둥 가운데의 반대쪽을 먼저 보고, 그쪽이 벽·다른 기둥에 막혀 몸(지름 2.4)이 못 지나가면 다른 쪽으로 돈다(마무리 검토 ⑦).
        /// </summary>
        Vector2 WalkDirection(Vector2 to, float dist)
        {
            if (dist < 0.01f) return Facing;
            Vector2 n = to / dist;
            var hit = Physics2D.CircleCast(Position, Radius * 0.8f, n, Mathf.Min(dist, WalkProbe), Layers.WallMask);
            if (!hit.collider) return Facing;
            var bounds = hit.collider.bounds;
            Vector2 c = (Vector2)bounds.center - Position;
            Vector2 perp = new Vector2(-n.y, n.x);
            float side = n.x * c.y - n.y * c.x >= 0f ? -1f : 1f;
            if (!SideOpen(bounds, n, side) && SideOpen(bounds, n, -side)) side = -side;
            return (perp * side + n * 0.25f).normalized;
        }

        /// <summary>
        /// 막은 것(기둥) 옆 side 쪽으로 몸이 지나갈 자리가 비었는가: 그 옆(막은 것 가장자리 + 몸 반지름 + 0.1)에 몸 원을 놓아 벽·다른 기둥과 겹치지 않으면 빈 것.
        /// </summary>
        bool SideOpen(Bounds obstacle, Vector2 n, float side)
        {
            Vector2 perp = new Vector2(-n.y, n.x);
            Vector2 rel = (Vector2)obstacle.center - Position;
            float half = Mathf.Abs(perp.x) * obstacle.extents.x + Mathf.Abs(perp.y) * obstacle.extents.y;
            Vector2 pass = Position + n * Vector2.Dot(rel, n) + perp * (Vector2.Dot(rel, perp) + side * (half + Radius + 0.1f));
            return !Physics2D.OverlapCircle(pass, Radius * 0.95f, Layers.WallMask);
        }

        BossChoice Choose(float dist)
        {
            var input = new BossPickInput
            {
                Phase2 = Phase >= 2,
                Distance = dist,
                SlamReady = _cdSlam <= 0f,
                ChargeReady = _cdCharge <= 0f,
                SweepReady = _cdSweep <= 0f,
                RoarReady = _cdRoar <= 0f,
                Last = _last,
            };
            var forced = Forced;
            return forced.HasValue ? BossRules.PickForced(forced.Value, input) : BossRules.Pick(input);
        }

        BossPattern? Forced
        {
            get
            {
                if (_forced.HasValue) return _forced;
                int f = Tuning.BossForcePattern;
                return f >= 0 && f <= (int)BossPattern.Roar ? (BossPattern)f : (BossPattern?)null;
            }
        }

        /// <summary>몸을 초당 120°(2단계 160°) × scale까지만 돌린다.</summary>
        /// <returns>돈 뒤 플레이어와의 각(도).</returns>
        float TurnTowards(Vector2 to, float dt, float scale)
        {
            if (to.sqrMagnitude < 0.0001f) return 0f;
            float current = Mathf.Atan2(Facing.y, Facing.x) * Mathf.Rad2Deg;
            float target = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            float next = Mathf.MoveTowardsAngle(current, target, BossRules.Turn(Phase >= 2) * scale * dt);
            float r = next * Mathf.Deg2Rad;
            FaceTowards(new Vector2(Mathf.Cos(r), Mathf.Sin(r)));
            return Mathf.Abs(Mathf.DeltaAngle(next, target));
        }

        /// <summary>A·D 시작: 몸 앞 50° 안이면 플레이어 쪽으로 마저 돌린다.</summary>
        void FaceIfFront(Vector2 to)
        {
            if (to.sqrMagnitude > 0.0001f && Vector2.Angle(Facing, to) <= BossRules.FrontGate) FaceTowards(to);
        }

        void StartPattern(BossPattern pattern)
        {
            CurrentPattern = pattern;
            _last = pattern;
            float cd = BossRules.Cooldown(pattern);
            switch (pattern)
            {
                case BossPattern.Slam: _cdSlam = cd; break;
                case BossPattern.Charge: _cdCharge = cd; break;
                case BossPattern.Sweep: _cdSweep = cd; break;
                default: _cdRoar = cd; break;
            }
        }

        bool Phase2 => Phase >= 2;

        void Club(float angle, float reach, float speed, float dt)
        {
            ClubAngle = Mathf.MoveTowards(ClubAngle, angle, speed * dt);
            ClubReach = Mathf.MoveTowards(ClubReach, reach, speed / 180f * dt);
        }

        // ───────────────────────── A 내려찍기 ─────────────────────────

        void BeginSlam(Vector2 to)
        {
            FaceIfFront(to);
            StartPattern(BossPattern.Slam);
            _state = State.Slam;
            _step = Step.Windup;
            _timer = 0f;
            DesiredVelocity = Vector2.zero;
            _slamCenter = Position + Facing * BossRules.SlamOffset;
            _windup = StrongAttackSchedule.Reserve(BossRules.Telegraph(BossPattern.Slam, Phase2, Floor), out _reserved1);
            _telegraph = Telegraph.Circle(_slamCenter, BossRules.SlamRadius, _windup);
            MarkAvoidable(_telegraph);
            SetPose(EnemyPose.Windup, _windup, _windup);
            // 예고 소리(한 방 10% 넘음): 짧고 높은 끙.
            OgreSounds.Play(OgreSound.Roar, 0.55f, 1.3f);
        }

        void TickSlam(float dt, PlayerController player, bool playerUp)
        {
            DesiredVelocity = Vector2.zero;
            _timer += dt;
            if (_step == Step.Windup)
            {
                if (_telegraph) _telegraph.Drive(_timer);
                // 버팀목을 오른 어깨 뒤로 들어 올렸다가(짧게 보임), 마지막 0.1초에 몸 앞 원 가운데로 내리친다.
                float strikeAt = Mathf.Max(0f, _windup - 0.1f);
                if (_timer < strikeAt)
                {
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_timer / Mathf.Max(0.05f, strikeAt * 0.7f)));
                    ClubAngle = Mathf.Lerp(-35f, -170f, k);
                    ClubReach = Mathf.Lerp(1f, 0.4f, k);
                    ArmLift = k;
                }
                else
                {
                    float k = Mathf.Clamp01((_timer - strikeAt) / Mathf.Max(0.01f, _windup - strikeAt));
                    ClubAngle = Mathf.Lerp(-170f, SlamClubAngle(), k);
                    ClubReach = Mathf.Lerp(0.4f, 1f, k);
                    ArmLift = 1f - k;
                }
                if (_timer < _windup) return;
                // 내려찍기(방패 표 2-7): 앞이면 막지만 늘 깨지고 패링은 안 된다. 넉백은 원 가운데에서 밀고, 막기 방향은 진행 방향(오우거 → 플레이어)으로 본다.
                // 원 가운데(오우거 앞 1.8)는 붙어 선 플레이어 몸 0.4 안이거나 몸 뒤라, 진행 방향이 없으면 오우거를 보고 서도 못 막거나 '뒤'로 갈렸다(0-3의 25).
                bool hit = playerUp && (player.Position - _slamCenter).magnitude <= BossRules.SlamRadius + PlayerController.Radius
                    && player.ReceiveHit(AttackPower, BossRules.SlamPercent, _slamCenter, BossRules.SlamKnockback, true, HitKind.BossSlam, player.Position - Position, this);
                ScreenShake.Add(hit ? BossRules.SlamShake : BossRules.SlamShake * 0.6f, BossRules.SlamShake);
                OgreSounds.Play(OgreSound.Slam);
                ClubAngle = SlamClubAngle();
                ClubReach = 1f;
                ArmLift = 0f;
                GetComponent<OgrePatternVfx>()?.Slam(_slamCenter); // v030 cosmetic cue
                Resolve(ref _telegraph, ref _reserved1, hit, BossPattern.Slam, true);
                _step = Step.Recover;
                _timer = 0f;
                SetPose(EnemyPose.Attack);
                return;
            }
            // 굳음 1.2초: 버팀목이 바닥에 박혀 빼는 동작(마지막 0.25초에 뽑아 든다).
            if (_timer >= BossRules.SlamRecover - 0.25f) Club(-35f, 1f, ClubPoseSpeed * 1.6f, dt);
            SetPose(_timer < 0.2f ? EnemyPose.Attack : EnemyPose.Idle);
            if (_timer >= BossRules.SlamRecover) EndPattern();
        }

        /// <summary>오른 어깨(몸 앞 0.15, 오른쪽 0.95)에서 A 원 가운데(몸 앞 1.8)로 향하는 몽둥이 각.</summary>
        static float SlamClubAngle() => Mathf.Atan2(OgreLook.ShoulderSide, BossRules.SlamOffset - OgreLook.ShoulderForward) * Mathf.Rad2Deg;

        // ───────────────────────── B 돌진 ─────────────────────────

        void BeginCharge(Vector2 to)
        {
            StartPattern(BossPattern.Charge);
            _state = State.Charge;
            _chargeIndex = 0;
            _chargeCount = BossRules.Charges(Phase2);
            AimCharge(to, true);
        }

        void AimCharge(Vector2 to, bool first)
        {
            _step = Step.Aim;
            _timer = 0f;
            DesiredVelocity = Vector2.zero;
            _chargeDir = to.sqrMagnitude > 0.0001f ? to.normalized : Facing;
            FaceTowards(_chargeDir);
            _chargeLast = _chargeIndex >= _chargeCount - 1;
            float baseTime = first ? BossRules.Telegraph(BossPattern.Charge, Phase2, Floor) : BossRules.ChargeReaimTelegraph(Floor);
            _windup = StrongAttackSchedule.Reserve(baseTime, out _reserved1);
            // 띠: 폭 2.0 × 길이 9 + 1.2(몸 앞끝까지). 빨강에 닿으면 맞는다(판정은 같은 띠를 지나간 만큼만).
            _telegraph = Telegraph.Rect(Position, _chargeDir, BossRules.ChargeLength + BossRules.ChargeTelegraphExtra, BossRules.ChargeWidth, _windup);
            // 마지막 돌진은 진한 빨강과 더 낮은 포효로 '이번이 마지막'을 알린다.
            if (_chargeLast) _telegraph.Lock();
            MarkAvoidable(_telegraph);
            SetPose(EnemyPose.Windup, _windup, _windup);
            OgreSounds.Play(OgreSound.Roar, _chargeLast ? 1f : 0.7f, _chargeLast ? 0.75f : 1f);
        }

        void TickCharge(float dt, PlayerController player, bool playerUp, Vector2 to)
        {
            switch (_step)
            {
                case Step.Aim:
                    DesiredVelocity = Vector2.zero;
                    _timer += dt;
                    if (_telegraph) _telegraph.Drive(_timer);
                    // 발을 구르며 버팀목을 앞세운다.
                    ExtraJitter = Random.insideUnitCircle * 0.05f;
                    Club(8f, 1f, ClubPoseSpeed * 2f, dt);
                    Lean = Mathf.MoveTowards(Lean, 1f, dt * 3f);
                    if (_timer >= _windup) StartRun();
                    break;

                case Step.Run:
                    RunStep(dt, player, playerUp);
                    break;

                case Step.Drag:
                case Step.Stumble:
                {
                    DesiredVelocity = Vector2.zero;
                    _timer += dt;
                    bool stumble = _step == Step.Stumble;
                    if (stumble) ExtraJitter = Random.insideUnitCircle * 0.09f;
                    Club(stumble ? -20f + Mathf.Sin(_timer * 40f) * 15f : -25f, 1f, ClubPoseSpeed, dt);
                    Lean = Mathf.MoveTowards(Lean, 0.4f, dt * 3f);
                    if (_timer < (stumble ? BossRules.ChargeStumble : BossRules.ChargeDrag)) break;
                    _chargeIndex++;
                    if (!playerUp)
                    {
                        EndPattern();
                        break;
                    }
                    AimCharge(to, false);
                    break;
                }

                default:
                    // 박지 않은 마지막 돌진 뒤 숨 고르기 1.0초(반격 틈).
                    DesiredVelocity = Vector2.zero;
                    SetPose(EnemyPose.Idle);
                    _timer += dt;
                    Club(-35f, 1f, ClubPoseSpeed, dt);
                    Lean = Mathf.MoveTowards(Lean, 0f, dt * 2f);
                    if (_timer >= BossRules.ChargeBreath) EndPattern();
                    break;
            }
        }

        void StartRun()
        {
            ExtraJitter = Vector2.zero;
            ClearStagger();
            _step = Step.Run;
            _timer = 0f;
            _chargeStart = Position;
            _chargeHit = false;
            _chargeTraveled = 0f;
            SetLayer(Layers.EnemyCharging);
            SetPose(EnemyPose.Attack);
            OgreSounds.Play(OgreSound.Charge);
            GetComponent<OgrePatternVfx>()?.StartRun(); // v030 cosmetic cue
            // 돌진 속도 11은 고정(적 걸음 배율·느려짐을 받지 않음): 빨간 띠 길이와 맞춘다.
            DesiredVelocity = _chargeDir * BossRules.ChargeSpeed;
        }

        void RunStep(float dt, PlayerController player, bool playerUp)
        {
            _timer += dt;
            Club(0f, 1f, ClubPoseSpeed * 2f, dt);
            Lean = 1f;
            float traveled = Vector2.Dot(Position - _chargeStart, _chargeDir);
            if (!_chargeHit && playerUp)
            {
                // 판정은 빨간 띠 그대로(옆으로 반폭 + 반지름 안)이고, 이번 프레임에 몸(앞끝·뒤끝)이 지나간 자리만 본다.
                // 이미 지나간 자리에 남은 플레이어는 구르기 무적이 끝나도 맞지 않는다.
                Vector2 rel = player.Position - _chargeStart;
                float along = Vector2.Dot(rel, _chargeDir);
                float cross = _chargeDir.x * rel.y - _chargeDir.y * rel.x;
                float pad = PlayerController.Radius;
                float back = Mathf.Max(-pad, _chargeTraveled - Radius - pad);
                if (Mathf.Abs(cross) <= BossRules.ChargeWidth * 0.5f + pad && along >= back && along <= traveled + Radius + pad)
                {
                    // 옆으로 넉백 2.0: 띠 가운데 선에서 플레이어 쪽으로 민다.
                    Vector2 side = cross >= 0f ? new Vector2(-_chargeDir.y, _chargeDir.x) : new Vector2(_chargeDir.y, -_chargeDir.x);
                    // 돌진(2-7): 진행 방향 기준 앞 반원으로 막고, 패링은 안 된다.
                    _chargeHit = player.ReceiveHit(AttackPower, BossRules.ChargePercent, player.Position - side, BossRules.ChargeSideKnockback,
                        true, HitKind.BossRush, _chargeDir, this);
                }
            }
            _chargeTraveled = traveled;
            if (WallAhead())
            {
                EndRun(true);
                return;
            }
            float remaining = BossRules.ChargeLength - traveled;
            // 마지막 물리 단계에서 9를 넘지 않게 속도를 줄인다.
            DesiredVelocity = _chargeDir * Mathf.Min(BossRules.ChargeSpeed, Mathf.Max(0f, remaining) / Time.fixedDeltaTime);
            if (remaining <= 0.02f || _timer > BossRules.ChargeLength / BossRules.ChargeSpeed + 0.3f) EndRun(false);
        }

        /// <summary>앞에 벽·기둥이 정면으로 있는가(이번 물리 단계에 갈 거리 + 여유).</summary>
        bool WallAhead()
        {
            var hit = Physics2D.CircleCast(Position, Radius * 0.9f, _chargeDir, BossRules.ChargeSpeed * Time.fixedDeltaTime + WallProbe, Layers.WallMask);
            return hit.collider && Vector2.Dot(hit.normal, _chargeDir) < WallHeadOn;
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (_state != State.Charge || _step != Step.Run || collision.gameObject.layer != Layers.Wall) return;
            // 몸 앞쪽에 닿았을 때만 박힘(옆으로 스친 벽은 미끄러져 지나간다).
            if (collision.contactCount <= 0) return;
            Vector2 contact = collision.GetContact(0).point;
            if (Vector2.Dot(contact - Position, _chargeDir) > Radius * 0.45f) EndRun(true);
        }

        void EndRun(bool wall)
        {
            if (_step != Step.Run) return;
            GetComponent<OgrePatternVfx>()?.EndRun(wall); // v030 cosmetic cue
            SetLayer(Layers.Enemy);
            DesiredVelocity = Vector2.zero;
            Resolve(ref _telegraph, ref _reserved1, _chargeHit, BossPattern.Charge, true);
            if (wall)
            {
                ScreenShake.Add(0.16f, 0.25f);
                OgreSounds.Play(OgreSound.Slam, 0.8f, 0.85f);
                if (_chargeLast)
                {
                    // 3-4 무너지는 길 2: 마지막 돌진이 벽·기둥에 박히면 버팀이 즉시 0 → 무너짐 3.0초('기둥').
                    _pendingCause = BreakCause.Pillar;
                    if (BreakNow()) return;
                    _pendingCause = BreakCause.None;
                    _step = Step.Breath;
                    _timer = 0f;
                    return;
                }
                _step = Step.Stumble;
                _timer = 0f;
                SetPose(EnemyPose.Hit);
                return;
            }
            _step = _chargeLast ? Step.Breath : Step.Drag;
            _timer = 0f;
            SetPose(EnemyPose.Idle);
        }

        // ───────────────────────── D 휩쓸기 2연 ─────────────────────────

        void BeginSweep(Vector2 to)
        {
            FaceIfFront(to);
            StartPattern(BossPattern.Sweep);
            _state = State.Sweep;
            _step = Step.Windup;
            _timer = 0f;
            DesiredVelocity = Vector2.zero;
            _windup = StrongAttackSchedule.Reserve(BossRules.Telegraph(BossPattern.Sweep, Phase2, Floor), out _reserved1);
            _sweepDir1 = Facing;
            _telegraph = Telegraph.HalfDisc(Position, _sweepDir1, BossRules.SweepRadius, _windup);
            MarkAvoidable(_telegraph);
            // 2타 예고는 1타 판정 0.1초 전에 시작한다(판정 간격 0.5초, 예고 0.6초).
            _sweep2Start = Mathf.Max(0f, _windup - BossRules.SweepSecondLead);
            _sweep2Started = false;
            _sweep1Done = false;
            _sweep2Time = BossRules.SweepSecondTelegraph(Floor);
            SetPose(EnemyPose.Windup, _windup, _windup);
            OgreSounds.Play(OgreSound.Roar, 0.5f, 1.35f);
        }

        void TickSweep(float dt, PlayerController player, bool playerUp, Vector2 to)
        {
            DesiredVelocity = Vector2.zero;
            _timer += dt;
            if (!_sweep2Started && _timer >= _sweep2Start)
            {
                // 2타는 몸을 돌릴 수 있는 만큼만 플레이어 쪽으로 튼다(등 뒤로 돌기가 정답으로 남게).
                Vector2 dir2 = _sweepDir1;
                if (playerUp && to.sqrMagnitude > 0.0001f)
                {
                    float current = Mathf.Atan2(_sweepDir1.y, _sweepDir1.x) * Mathf.Rad2Deg;
                    float target = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
                    float next = Mathf.MoveTowardsAngle(current, target, BossRules.Turn(Phase2) * _sweep2Start) * Mathf.Deg2Rad;
                    dir2 = new Vector2(Mathf.Cos(next), Mathf.Sin(next));
                }
                _sweepDir2 = dir2;
                _sweep2Time = StrongAttackSchedule.Reserve(BossRules.SweepSecondTelegraph(Floor), out _reserved2);
                _telegraph2 = Telegraph.HalfDisc(Position, dir2, BossRules.SweepRadius, _sweep2Time);
                MarkAvoidable(_telegraph2);
                _sweep2Started = true;
            }
            if (_telegraph) _telegraph.Drive(_timer);
            if (_telegraph2) _telegraph2.Drive(_timer - _sweep2Start);

            // 몽둥이: 오른쪽 뒤로 감았다가(−120°) 1타에 왼쪽(+120°)으로, 2타에 다시 오른쪽으로 쓸어 낸다.
            if (_step == Step.Windup)
            {
                if (!_sweep1Done)
                {
                    float strikeAt = Mathf.Max(0f, _windup - 0.1f);
                    ClubAngle = _timer < strikeAt ? Mathf.MoveTowards(ClubAngle, -120f, ClubPoseSpeed * 1.5f * dt) : Mathf.Lerp(-120f, 120f, Mathf.Clamp01((_timer - strikeAt) / 0.1f));
                }
                else
                {
                    float hit2 = _sweep2Start + _sweep2Time;
                    float strikeAt = hit2 - 0.1f;
                    ClubAngle = _timer < strikeAt ? 120f : Mathf.Lerp(120f, -120f, Mathf.Clamp01((_timer - strikeAt) / 0.1f));
                }
                ClubReach = Mathf.MoveTowards(ClubReach, 0.95f, dt * 4f);
            }

            if (!_sweep1Done && _timer >= _windup)
            {
                _sweep1Done = true;
                // 휩쓸기(2-7): 쓸어 내는 쪽(_sweepDir1)이 진행 방향. 패링이면 끊기지 않고 버팀 6%(Parried 덮어씀).
                bool hit = playerUp && SectorMath.InHalfDisc(Position.x, Position.y, _sweepDir1.x, _sweepDir1.y, BossRules.SweepRadius, player.Position.x, player.Position.y, PlayerController.Radius)
                    && player.ReceiveHit(AttackPower, BossRules.SweepPercent, Position, BossRules.SweepKnockback, true, HitKind.BossSweep, _sweepDir1, this);
                OgreSounds.Play(OgreSound.Sweep);
                GetComponent<OgrePatternVfx>()?.Sweep(); // v030 cosmetic cue
                ScreenShake.Add(0.06f, 0.12f);
                Resolve(ref _telegraph, ref _reserved1, hit, BossPattern.Sweep, true);
                FaceTowards(_sweepDir2.sqrMagnitude > 0.0001f ? _sweepDir2 : _sweepDir1);
                SetPose(EnemyPose.Attack);
                FlushSweepParry();
            }
            if (_step == Step.Windup && _sweep1Done && _sweep2Started && _timer >= _sweep2Start + _sweep2Time)
            {
                bool hit = playerUp && SectorMath.InHalfDisc(Position.x, Position.y, _sweepDir2.x, _sweepDir2.y, BossRules.SweepRadius, player.Position.x, player.Position.y, PlayerController.Radius)
                    && player.ReceiveHit(AttackPower, BossRules.SweepPercent, Position, BossRules.SweepKnockback, true, HitKind.BossSweep, _sweepDir2, this);
                OgreSounds.Play(OgreSound.Sweep, 1f, 0.9f);
                GetComponent<OgrePatternVfx>()?.Sweep(); // v030 cosmetic cue
                ScreenShake.Add(0.06f, 0.12f);
                Resolve(ref _telegraph2, ref _reserved2, hit, BossPattern.Sweep, true);
                _step = Step.Recover;
                _recoverAt = _timer;
                SetPose(EnemyPose.Attack);
                FlushSweepParry();
            }
            if (_step == Step.Recover)
            {
                // 틈 0.8초.
                if (_timer - _recoverAt > 0.25f) Club(-35f, 1f, ClubPoseSpeed, dt);
                SetPose(_timer - _recoverAt < 0.2f ? EnemyPose.Attack : EnemyPose.Idle);
                if (_timer - _recoverAt >= BossRules.SweepRecover) EndPattern();
            }
        }

        // ───────────────────────── C 포효·낙석·소환 ─────────────────────────

        void BeginRoar()
        {
            StartPattern(BossPattern.Roar);
            _state = State.Roar;
            _step = Step.RoarUp;
            _timer = 0f;
            DesiredVelocity = Vector2.zero;
            SetPose(EnemyPose.Windup, BossRules.RoarTime, BossRules.RoarTime);
            OgreSounds.Play(OgreSound.Roar, 1f, 0.9f);
            GetComponent<OgrePatternVfx>()?.Roar(); // v030 cosmetic cue
            ScreenShake.Add(0.1f, BossRules.RoarTime);
        }

        void TickRoar(float dt, PlayerController player, bool playerUp)
        {
            DesiredVelocity = Vector2.zero;
            _timer += dt;
            // 몽둥이를 바닥에 꽂고 두 팔을 벌린다.
            Club(-25f, 0.75f, ClubPoseSpeed, dt);
            ArmLift = Mathf.MoveTowards(ArmLift, _step == Step.RoarUp ? 1f : 0.3f, dt * 4f);
            if (_step == Step.RoarUp)
            {
                ExtraJitter = Random.insideUnitCircle * 0.04f;
                if (_timer >= BossRules.RoarTime) DropRocks();
                return;
            }
            for (int i = 0; i < _rocks.Count; i++)
                if (_rocks[i]) _rocks[i].Drive(_timer);
            if (_timer < BossRules.RockWindup) return;
            ResolveRocks(player, playerUp);
            EndPattern();
        }

        /// <summary>포효 뒤: 원 7개(무작위 6 + 발밑 1, 반경 1.2, 서로 2.8 이상, 기둥에서 1.6 이상)와 굴쥐 4(벽 틈 4곳).</summary>
        void DropRocks()
        {
            _state = State.Roar;
            _step = Step.Rocks;
            _timer = 0f;
            _rocksDropped = true;
            CurrentPattern = BossPattern.Roar;
            SpawnRocks();
            SummonRats();
            OgreSounds.Play(OgreSound.Rockfall, 0.6f, 0.85f);
        }

        void SpawnRocks()
        {
            ClearRocks();
            _rockSpots.Clear();
            var player = Player;
            bool playerUp = player && !player.IsDown;
            if (playerUp) _rockSpots.Add(player.Position);
            Rect room = RoomRect();
            float r = BossRules.RockRadius;
            for (int attempt = 0; attempt < 120 && _rockSpots.Count < BossRules.RockCount; attempt++)
            {
                var p = new Vector2(Random.Range(room.xMin + r, room.xMax - r), Random.Range(room.yMin + r, room.yMax - r));
                bool near = false;
                for (int i = 0; i < _rockSpots.Count; i++)
                    if ((_rockSpots[i] - p).sqrMagnitude < BossRules.RockGap * BossRules.RockGap)
                    {
                        near = true;
                        break;
                    }
                if (near || Physics2D.OverlapCircle(p, BossRules.RockPillarGap, Layers.WallMask)) continue;
                _rockSpots.Add(p);
            }
            for (int i = 0; i < _rockSpots.Count; i++)
            {
                var t = Telegraph.Circle(_rockSpots[i], r, BossRules.RockWindup);
                GetComponent<OgrePatternVfx>()?.RockWarning(_rockSpots[i], BossRules.RockWindup); // v030 cosmetic cue
                bool threat = playerUp && t.Contains(player.Position, PlayerController.Radius);
                t.Avoidable = threat && player.DodgeReady;
                _rocks.Add(t);
                _rockThreat.Add(threat);
            }
        }

        void ResolveRocks(PlayerController player, bool playerUp)
        {
            bool any = false;
            for (int i = 0; i < _rocks.Count; i++)
            {
                var t = _rocks[i];
                if (!t) continue;
                // 낙석(2-7): 위에서 떨어져 방패로 못 막는다(무기 행동은 끊긴다).
                bool hit = playerUp && (player.Position - t.Origin).magnitude <= BossRules.RockRadius + PlayerController.Radius
                    && player.ReceiveHit(AttackPower, BossRules.RockPercent, t.Origin, BossRules.RockKnockback, true, HitKind.FromAbove);
                // 계측은 플레이어를 노린 원(발밑 원, 시작 때 안에 있던 원)과 맞은 원만 센다(먼 원 6개가 피함 비율을 부풀리지 않게).
                if (_rockThreat[i] || hit)
                {
                    CombatEvents.RaiseTelegraph(t.Avoidable, hit);
                    _stats.Record(BossPattern.Roar, hit, true);
                }
                GetComponent<OgrePatternVfx>()?.RockImpact(t.Origin); // v030 cosmetic cue
                t.Resolve();
                any = true;
            }
            _rocks.Clear();
            _rockThreat.Clear();
            if (!any) return;
            OgreSounds.Play(OgreSound.Rockfall);
            ScreenShake.Add(0.12f, 0.25f);
        }

        void SummonRats()
        {
            for (int i = _rats.Count - 1; i >= 0; i--)
                if (!_rats[i] || _rats[i].Dead) _rats.RemoveAt(i);
            int count = Mathf.Min(BossRules.SummonRats, BossRules.MaxRats - _rats.Count);
            if (count <= 0) return;
            Rect room = RoomRect();
            float top = room.yMax - BossRules.RatHoleInset;
            float bottom = room.yMin + BossRules.RatHoleInset;
            float near = Mathf.Clamp(room.center.x + BossRules.RatHoleNearX, room.xMin + 1f, room.xMax - 1f);
            float far = Mathf.Clamp(room.center.x + BossRules.RatHoleFarX, room.xMin + 1f, room.xMax - 1f);
            for (int i = 0; i < count; i++)
            {
                // 벽 틈 4곳: (−2, ±7)·(10, ±7)의 방 가장자리 안쪽.
                var pos = new Vector2(i < 2 ? near : far, (i & 1) == 0 ? top : bottom);
                if (Physics2D.OverlapCircle(pos, 0.3f, Layers.WallMask)) pos = Vector2.MoveTowards(pos, room.center, 1.2f);
                var rat = EnemySpawner.Create(MonsterKind.Rat, Floor, pos);
                rat.NoReward = true;
                rat.GroupId = GroupId;
                EnemySpawner.Track(rat);
                _rats.Add(rat);
            }
        }

        /// <summary>낙석·쥐 구멍을 둘 방 안쪽: 전투 시험장 소환기의 방, 없으면 시작 자리 (7, 0) 기준 26×14.</summary>
        Rect RoomRect()
        {
            var inner = EnemySpawner.ActiveInner;
            if (inner.HasValue) return inner.Value;
            Vector2 c = HomePosition - new Vector2(BossRules.StartX, 0f);
            return new Rect(c.x - 13f, c.y - 7f, 26f, 14f);
        }

        // ───────────────────────── 2단계 전환 ─────────────────────────

        void CheckPhase()
        {
            if (!Tuning.BossPhase2On)
            {
                _phasePending = false;
                return;
            }
            // 전환 중(Phase가 2가 되기 전 0.4초)에는 다시 걸지 않는다. 걸면 첫 C 뒤에 전환·낙석·소환이 한 번 더 일어난다.
            if (Phase == 1 && !_phasePending && _state != State.Transition && !Dead && Health.Fraction < BossRules.PhaseThreshold) _phasePending = true;
        }

        void BeginTransition()
        {
            _phasePending = false;
            _state = State.Transition;
            _step = Step.None;
            _timer = 0f;
            _slowDone = false;
            _rocksDropped = false;
            CurrentPattern = null;
            DesiredVelocity = Vector2.zero;
            // 0.0초: 몽둥이를 바닥에 꽂고 포효, 흔들림 0.15/1.0.
            OgreSounds.Play(OgreSound.Roar, 1f, 0.7f);
            GetComponent<OgrePatternVfx>()?.Roar(); // v030 cosmetic cue
            ScreenShake.Add(BossRules.TransitionShake, BossRules.TransitionShakeSeconds);
            SetPose(EnemyPose.Windup, BossRules.TransitionTime, BossRules.TransitionTime);
            TransitionStarted?.Invoke(this);
        }

        void TickTransition(float dt)
        {
            DesiredVelocity = Vector2.zero;
            _timer += dt;
            ExtraJitter = Random.insideUnitCircle * 0.05f;
            Club(-25f, 0.75f, ClubPoseSpeed, dt);
            ArmLift = Mathf.MoveTowards(ArmLift, 1f, dt * 4f);
            if (!_slowDone && _timer >= BossRules.TransitionSlowAt)
            {
                // 0.4초: 느린 화면 0.6초 × 0.5(보스 2단계 순위), 눈이 흰색으로·회백 테두리(OgreLook이 Phase를 읽음), 걷기 × 1.2.
                _slowDone = true;
                TimeScaleService.SlowMotion(BossRules.TransitionSlowSeconds, BossRules.TransitionSlowScale, SlowPriority.BossPhase);
                Phase = 2;
                SetBaseMoveSpeed(BossRules.WalkSpeed * BossRules.Phase2SpeedScale);
                _stats.PhaseAt = FightTime;
                CombatEvents.RaiseBossPhaseChanged(this, 2);
            }
            if (_timer < BossRules.TransitionRoarAt) return;
            // 1.0초: 첫 C(전환 포효가 첫 C의 포효를 겸한다).
            StartPattern(BossPattern.Roar);
            DropRocks();
        }

        // ───────────────────────── 무너짐·처치 ─────────────────────────

        /// <summary>무너짐: 하던 예고·낙석을 거두고 원인을 적는다. Think는 무너짐이 끝날 때까지 불리지 않는다(Enemy).</summary>
        protected override void OnBroken()
        {
            bool inTransition = _state == State.Transition;
            CancelAll();
            if (inTransition && Phase == 1) _phasePending = true;
            // 전환 0.4초 뒤에 무너져 첫 C가 나오지 못했으면 일어난 뒤 가장 먼저 C(재사용 0).
            if (inTransition && Phase >= 2 && !_rocksDropped) _cdRoar = 0f;
            var cause = _pendingCause;
            if (cause == BreakCause.None) cause = _stats.Breaks == 0 && Time.time - _ambushAt <= AmbushCredit ? BreakCause.Ambush : BreakCause.Pressure;
            _pendingCause = BreakCause.None;
            _stats.Break(cause);
            _breakStart = Time.time;
            _state = State.Broken;
            _step = Step.None;
            CurrentPattern = null;
            DesiredVelocity = Vector2.zero;
            ArmLift = 0f;
            Lean = 0f;
            // 무릎 꿇음(깨지는 소리·히트스톱 0.08·흔들림·'무너짐!'은 Enemy가 낸다).
            OgreSounds.Play(OgreSound.Kneel);
        }

        /// <summary>무너짐이 끝난 첫 Think: 일어나 1.0초 쉰다(2단계가 기다리고 있으면 바로 전환).</summary>
        void StandUp()
        {
            if (_breakStart >= 0f)
            {
                _stats.BrokenTime += Time.time - _breakStart;
                _breakStart = -1f;
            }
            if (!_fightActive)
            {
                BeginWake();
                return;
            }
            if (_phasePending)
            {
                BeginTransition();
                return;
            }
            EnterRest(BossRules.StandUpRest);
        }

        void OnOgreDied()
        {
            _state = State.Down;
            CurrentPattern = null;
            // 3-7 처치: 예산 안 최대 히트스톱 0.2초(그동안 다른 멈춤 요청은 무시) → 1.0초 × 0.3 느린 화면(보스 처치 순위).
            TimeScaleService.HitStopLocked(BossRules.KillHitStop, BossRules.KillHitStop + BossRules.KillSlowSeconds);
            TimeScaleService.SlowMotion(BossRules.KillSlowSeconds, BossRules.KillSlowScale, SlowPriority.BossKill);
            OgreSounds.Play(OgreSound.Roar, 0.9f, 0.6f);
            OgreSounds.Play(OgreSound.Kneel, 1f, 0.8f);
            ScreenShake.Add(0.2f, 0.6f);
            // 앞으로 쓰러지는 몸은 OgreLook이 남긴다: 도형 몸을 숨기고 Enemy 시체(원)를 남기지 않는다.
            if (_look) _look.Fall();
            Dismember();
            ScatterRats();
            if (_breakStart >= 0f)
            {
                _stats.BrokenTime += Time.time - _breakStart;
                _breakStart = -1f;
            }
            bool first = !BossReward.HasCleared(Floor);
            if (_fightActive) FinishFight(true);
            BossReward.OnBossKilled(this, first);
        }

        /// <summary>남은 소환 굴쥐는 겁먹고 흩어졌다가 1.5초 뒤 사라진다(벽 틈으로 도망침).</summary>
        void ScatterRats()
        {
            PackFear.Scatter(Position, 40f, BossRules.SummonVanish);
            for (int i = 0; i < _rats.Count; i++)
            {
                var rat = _rats[i];
                if (rat && !rat.Dead) rat.StartCoroutine(VanishLater(rat, BossRules.SummonVanish));
            }
            _rats.Clear();
        }

        static IEnumerator VanishLater(Enemy rat, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                yield return null;
            }
            if (rat && !rat.Dead) rat.Remove();
        }

        void RemoveRats()
        {
            for (int i = 0; i < _rats.Count; i++)
                if (_rats[i] && !_rats[i].Dead) _rats[i].Remove();
            _rats.Clear();
        }

        // ───────────────────────── 예고·정리 ─────────────────────────

        static void MarkAvoidable(Telegraph t)
        {
            var player = Player;
            if (!t) return;
            t.Avoidable = player && !player.IsDown && t.Contains(player.Position, PlayerController.Radius) && player.DodgeReady;
        }

        /// <summary>판정 순간: 계측 사건을 내고 예고를 진하게 보인 뒤 지운다. 예약 자리는 지난 시각이라 그대로 둔다(BoarBrain과 같음).</summary>
        void Resolve(ref Telegraph t, ref float reserved, bool hit, BossPattern pattern, bool threatened)
        {
            reserved = -1f;
            if (!t) return;
            CombatEvents.RaiseTelegraph(t.Avoidable, hit);
            _stats.Record(pattern, hit, threatened);
            t.Resolve();
            t = null;
        }

        void ClearRocks()
        {
            for (int i = 0; i < _rocks.Count; i++)
                if (_rocks[i]) _rocks[i].Cancel();
            _rocks.Clear();
            _rockThreat.Clear();
        }

        /// <summary>예고·낙석·예약을 모두 거둔다(무너짐·죽음·지움·다시 섬). 거둔 예고는 바로 사라진다(공정 규칙).</summary>
        void CancelAll()
        {
            GetComponent<OgrePatternVfx>()?.Clear(); // v030 cosmetic cue
            _sweepParried = false;
            if (_telegraph) _telegraph.Cancel();
            _telegraph = null;
            if (_telegraph2) _telegraph2.Cancel();
            _telegraph2 = null;
            ClearRocks();
            StrongAttackSchedule.Release(ref _reserved1);
            StrongAttackSchedule.Release(ref _reserved2);
            if (this) SetLayer(Layers.Enemy);
            ExtraJitter = Vector2.zero;
        }

        /// <summary>Interruptible이 false라 넉백으로는 불리지 않는다. 죽거나 지워질 때만 정리한다.</summary>
        protected override void OnInterrupted() => CancelAll();

        /// <summary>
        /// 방패 패링(기획/세-무기-우클릭-소켓-1차.md 2-6): 휩쓸기만 튕길 수 있고(내려찍기·돌진·낙석은 ShieldRule이 패링을 막음),
        /// 보스 규칙대로 끊기지 않고 버팀 최대치의 6%(ShieldRule.BossSweepPoiseFraction)만 깎는다. 휩쓸기 판정 자리에서는
        /// 계측·다음 상태가 정해진 뒤에 넣는다(그 6%로 무너지면 OnBroken이 정리하고, 판정 자리가 무너짐 위에 덮어쓰지 않게).
        /// </summary>
        public override void Parried(HitKind kind)
        {
            if (Dead || kind != HitKind.BossSweep) return;
            if (_state == State.Sweep)
            {
                _sweepParried = true;
                return;
            }
            SweepParryPoise();
        }

        /// <summary>
        /// 반격 창은 휩쓸기 마지막 타(2타)를 튕겼을 때만 연다(0-3의 23). 1타를 튕겨도 오우거는 끊기지 않고 0.5초 뒤 2타가 와서,
        /// 반격 찌르기(밀쳐 내기 0.15 + 판정 0.386초)가 2타에 맞는 함정이 된다. 2타 판정 순간은 _sweep2Start + _sweep2Time 뒤다.
        /// </summary>
        public override bool ParryOpensRiposte(HitKind kind) =>
            base.ParryOpensRiposte(kind) && kind == HitKind.BossSweep && _state == State.Sweep && _sweep1Done && _sweep2Started && _timer >= _sweep2Start + _sweep2Time;

        void FlushSweepParry()
        {
            if (!_sweepParried) return;
            _sweepParried = false;
            SweepParryPoise();
        }

        /// <summary>휩쓸기 패링 몫: 버팀 6%와 짧은 몸 흔들림(그림만). 버팀이 0이 되면 무너진다(원인은 '압박').</summary>
        void SweepParryPoise()
        {
            if (Dead || Poise == null) return;
            ApplyPoiseHit(Poise.Max * ShieldRule.BossSweepPoiseFraction, SweepParryShake);
        }

        /// <summary>휩쓸기 패링 때 몸 흔들림 시간(그림만, 준비·패턴은 끊지 않음).</summary>
        const float SweepParryShake = 0.1f;

        /// <summary>다시 섬(ResetToHome): 하던 것을 거두고 처음(쉬는 중)으로.</summary>
        protected override void ResetBehaviour()
        {
            CancelAll();
            _state = State.Sleep;
            _step = Step.None;
            _timer = 0f;
            CurrentPattern = null;
            DesiredVelocity = Vector2.zero;
        }

        // ───────────────────────── 한 판 기록(3-10) ─────────────────────────

        void TrackFight(PlayerController player)
        {
            if (!_fightActive) return;
            if (player)
            {
                int p = player.Potions;
                if (p < _lastPotions) _stats.Potions += _lastPotions - p;
                _lastPotions = p;
            }
            if (Time.unscaledTime < _liveAt) return;
            _liveAt = Time.unscaledTime + 0.25f;
            BossFightLog.SetLive(LiveLine());
        }

        string LiveLine()
        {
            var poise = Poise;
            return "싸움 " + FightTime.ToString("0.0") + "초 · " + Phase + "단계 · 체력 " + Mathf.RoundToInt(Health.Fraction * 100f) + "%"
                + (poise != null ? " · 버팀 " + Mathf.RoundToInt((float)poise.Current) + "/" + Mathf.RoundToInt((float)poise.Max) : "")
                + " · 무너짐 " + _stats.Breaks + " · " + _stats.HitDodgeText();
        }

        void FinishFight(bool killed)
        {
            _fightActive = false;
            float time = Mathf.Max(0.001f, Time.time - _fightStart);
            string line = (killed ? "처치 " : "그만둠 ") + time.ToString("0.0") + "초 · " + Floor + "층 · " + _stats.HitDodgeText()
                + " · 무너짐 " + _stats.Breaks + " (기습 " + _stats.AmbushBreaks + "·기둥 " + _stats.PillarBreaks + "·압박 " + _stats.PressureBreaks + ")"
                + " · 무너진 시간 " + Mathf.RoundToInt(100f * _stats.BrokenTime / time) + "%"
                + " · 물약 " + _stats.Potions + " · 쓰러짐 " + _stats.Downs
                + (_stats.PhaseAt >= 0f ? " · 2단계 " + _stats.PhaseAt.ToString("0") + "초" : "")
                + (TestRunFlag.Marked ? " · " + TestRunFlag.Label : ""); // 시험 판 표시(검토 1차 Q7)
            BossFightLog.Add(line);
            BossFightLog.SetLive("");
            Debug.Log("[보스] " + line);
        }

        /// <summary>판마다 센 값: 패턴별 맞음/피함, 무너짐 횟수·원인·시간, 물약, 쓰러짐, 2단계 시각.</summary>
        sealed class FightStats
        {
            readonly int[] _hit = new int[4];
            readonly int[] _dodged = new int[4];
            public int Breaks;
            public int AmbushBreaks;
            public int PillarBreaks;
            public int PressureBreaks;
            public int Potions;
            public int Downs;
            public float BrokenTime;
            public float PhaseAt = -1f;

            public void Reset()
            {
                for (int i = 0; i < 4; i++)
                {
                    _hit[i] = 0;
                    _dodged[i] = 0;
                }
                Breaks = AmbushBreaks = PillarBreaks = PressureBreaks = 0;
                Potions = 0;
                Downs = 0;
                BrokenTime = 0f;
                PhaseAt = -1f;
            }

            public void Record(BossPattern pattern, bool hit, bool threatened)
            {
                if (hit) _hit[(int)pattern]++;
                else if (threatened) _dodged[(int)pattern]++;
            }

            /// <summary>가장 많이 맞은 패턴(같으면 돌진 → 내려찍기 → 휩쓸기 → 포효). 맞은 적이 없으면 null.</summary>
            public BossPattern? MostHit()
            {
                BossPattern[] order = { BossPattern.Charge, BossPattern.Slam, BossPattern.Sweep, BossPattern.Roar };
                BossPattern? best = null;
                int most = 0;
                foreach (var p in order)
                {
                    int n = _hit[(int)p];
                    if (n <= most) continue;
                    most = n;
                    best = p;
                }
                return best;
            }

            public void Break(BreakCause cause)
            {
                Breaks++;
                if (cause == BreakCause.Ambush) AmbushBreaks++;
                else if (cause == BreakCause.Pillar)
                {
                    PillarBreaks++;
                    PillarBroken?.Invoke();
                }
                else PressureBreaks++;
            }

            /// <summary>"맞음/피함 A 1/3 B 0/6 D 2/2 C 1/2" (문서 글자 차례 A·B·D·C).</summary>
            public string HitDodgeText() =>
                "맞음/피함 A " + _hit[(int)BossPattern.Slam] + "/" + _dodged[(int)BossPattern.Slam]
                + " B " + _hit[(int)BossPattern.Charge] + "/" + _dodged[(int)BossPattern.Charge]
                + " D " + _hit[(int)BossPattern.Sweep] + "/" + _dodged[(int)BossPattern.Sweep]
                + " C " + _hit[(int)BossPattern.Roar] + "/" + _dodged[(int)BossPattern.Roar];
        }
    }

    /// <summary>
    /// 보스 한 판 기록(3-10 계측): 처치 시간, 패턴별 맞음/피함, 무너짐 횟수와 원인(기습·기둥·압박), 물약 수, 사망. 시험 패널(⑧)이 Lines를 한 줄씩 보인다.
    /// 판이 끝날 때(처치·다시 도전·지움) 한 줄을 더하고 콘솔에도 '[보스] …'로 남긴다. 최근 30줄만 둔다.
    /// </summary>
    public static class BossFightLog
    {
        const int MaxLines = 30;
        static readonly List<string> _lines = new List<string>();
        static string _live = "";

        /// <summary>끝난 판마다 한 줄(최근 것이 끝).</summary>
        public static IReadOnlyList<string> Lines => _lines;
        /// <summary>지금 판 진행 한 줄(없으면 빈 글). 0.25초마다 새로 만든다.</summary>
        public static string Live => _live;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStatics()
        {
            _lines.Clear();
            _live = "";
        }

        /// <summary>시험 패널 '기록 지우기'.</summary>
        public static void Clear() => ResetStatics();

        internal static void Add(string line)
        {
            _lines.Add(line);
            if (_lines.Count > MaxLines) _lines.RemoveAt(0);
        }

        internal static void SetLive(string line) => _live = line ?? "";
    }
}
