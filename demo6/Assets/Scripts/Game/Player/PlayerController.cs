using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Loot;
using Demo6.Core.Random;
using Demo6.Core.Stats;
using UnityEngine;

namespace Demo6.Game
{
    public enum PlayerPose
    {
        Idle,
        Move,
        Attack,
        Dodge,
        Whirl,
        WaveCast,
        Hurt,
        Down,
    }

    /// <summary>
    /// 검사. 기획 3장: 기본공격(누르고 있으면 반복), 회오리 베기(우클릭), 검풍(Q), 구르기(Space), 물약(R).
    /// 기본공격은 무기별 단계 콤보 + 마무리(M0a 판정 반영). 판정이 나간 뒤에는 구르기로 끊을 수 있고, 스킬은 동작이 끝나면 나간다.
    /// 장비 능력치(장비 문서 2·3장)는 ApplyStats 한 입구로 받는다: 공격 속도는 SwingTiming(동작 길이·판정 순간·이월 상한)과 타당 버팀에만,
    /// 치명은 전용 난수 흐름으로 굴리고 연출을 3단계(가벼움·보통·무거움, 무거움 0.5초 제한)로 낸다. 공격 속도 0이면 콤보 시간은 예전과 비트까지 같다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(PlayerInputReader))]
    public sealed class PlayerController : MonoBehaviour
    {
        public const float Radius = 0.4f;
        /// <summary>시작 장비(가죽 한 벌)의 이동 몫(‰). 능력치를 넣기 전 걸음 속도에만 쓴다(넣은 뒤에는 StatSheet.MoveSpeed).</summary>
        const int StartingMovePermille = 60;
        /// <summary>
        /// 배율(Tuning.MoveSpeedScale)을 곱하기 전 시작 장비 걸음 속도(5.3). ApplyStats 전에 쓰는 값이고 화면 비교 글에도 쓴다.
        /// StatSheet.MoveSpeed(5.0 × (1 + ‰ ÷ 1000))와 같은 float 식이라 시작 장비 능력치를 넣어도 비트까지 같다.
        /// </summary>
        public const float EquippedWalkSpeed = StatBase.MoveSpeed * (1f + StartingMovePermille / 1000f);
        /// <summary>걷기 가감속(Tuning.MoveInertia): 멈춘 데서 다 빨라지기까지, 다 빠른 데서 서기까지 걸리는 시간(초).</summary>
        public const float WalkAccelTime = 0.16f;
        public const float WalkDecelTime = 0.10f;

        const float DodgeTime = 0.22f;
        const float DodgeDistance = 3.5f;
        const float DodgeInvulnerable = 0.18f;
        const float DodgeCooldownTime = 2.0f;

        const float WhirlTime = 0.6f;
        const float WhirlRadius = 2.6f;
        const float WhirlPercent = 90f;
        const float WhirlKnockback = 0.4f;
        const float WhirlCooldownTime = 6f;
        /// <summary>3차 버팀 피해(초안 3-4): 회오리 타마다 8, 검풍 40.</summary>
        const float WhirlPoise = 8f;
        const float WavePoise = 40f;
        /// <summary>회피 반격: 예고 공격을 구르기로 피하고 이 시간 안의 첫 타는 버팀 ×2.</summary>
        const float CounterWindow = 1.0f;
        const float WhirlMoveScale = 0.7f;
        static readonly float[] WhirlTicks = { 0.1f, 0.3f, 0.5f };

        const float WaveCastTime = 0.25f;
        const float WaveCooldownTime = 9f;
        const float WaveCastMoveScale = 0.2f;

        const int MaxPotions = 3;
        const float PotionHeal = 0.4f;
        const float PotionCooldownTime = 3f;

        const float HurtFlash = 0.1f;
        const float HurtInvulnerable = 0.5f;
        const float ReviveDelay = 1.5f;

        enum State
        {
            Free,
            Swing,
            Dodge,
            Whirl,
            WaveCast,
            Down,
        }

        public static PlayerController Instance { get; private set; }

        public int Attack { get; private set; } = 200;
        public float CritChance { get; set; } = (float)DamageMath.BaseCritChance;
        public float CritDamage { get; set; } = (float)DamageMath.BaseCritDamage;
        public WeaponAttackRule Weapon { get; private set; } = WeaponPresets.Longsword;
        public Health Health => _health;
        public Vector2 Position => _body ? _body.position : (Vector2)transform.position;
        /// <summary>지금 걷기 속도: 장비 속도(WalkSpeed, 시작 5.3) 또는 덮어쓰기(탐험 걸음 6.5)에 Tuning.MoveSpeedScale(기본 0.86)을 곱한 값.</summary>
        public float MoveSpeed => (SpeedOverride > 0f ? SpeedOverride : WalkSpeed) * Tuning.MoveSpeedScale;
        /// <summary>장비 전투 걸음 속도(배율 곱하기 전, 장비 문서 2-1): StatSheet.MoveSpeed. 능력치를 넣기 전에는 시작 장비 값 5.3.</summary>
        public float WalkSpeed => Sheet != null ? Sheet.MoveSpeed : EquippedWalkSpeed;

        /// <summary>던전 '탐험 걸음'(3차 초안 2-8, 6.5) 같은 이동 속도 덮어쓰기(배율 곱하기 전 값). 0 이하면 장비 속도.</summary>
        public float SpeedOverride { get; set; }
        /// <summary>마지막으로 공격·스킬을 쓰거나 맞은 시각(탐험 걸음 해제, 전투 중 판정).</summary>
        public float LastCombatActionTime { get; private set; } = -999f;
        /// <summary>마지막으로 맞은 공격이 온 자리(적·화살·덫). 피격 피가 반대쪽으로 튄다.</summary>
        public Vector2 LastHitFrom { get; private set; }
        /// <summary>쓰러지면 1.5초 뒤 제자리에서 일어나는가(전투 시험장). 던전은 끄고 말뚝에서 다시 세운다.</summary>
        public bool AutoRevive { get; set; } = true;
        /// <summary>스킬 1줄(3차 초안 4-5): 넓은 회오리 반경 +, 날 선 바람 검풍 계수 +%p, 마무리 일격 마무리 피해 +비율.</summary>
        public float WhirlRadiusBonus { get; set; }
        public float WavePercentBonus { get; set; }
        public float FinisherDamageBonus { get; set; }
        public float WhirlRadiusNow => WhirlRadius + WhirlRadiusBonus;
        public float WavePercentNow => SwordWave.Percent + WavePercentBonus;
        public bool IsDown => _state == State.Down;
        public bool IsSwinging => _state == State.Swing;
        /// <summary>지금(또는 마지막) 콤보 단계 번호(1부터)와 이름. 콤보가 끊겼으면 0.</summary>
        public int ComboStepNumber => _state == State.Swing || Time.time <= _comboExpire ? _comboStepNumber : 0;
        public string ComboStepName => _step != null ? _step.name : "";
        /// <summary>최근 연속 처치 수(2초 안에 다음 처치가 이어지면 계속).</summary>
        public int KillStreak { get; private set; }

        /// <summary>그림 고르기용 자세와 그 자세의 시간 정보.</summary>
        public PlayerPose Pose
        {
            get
            {
                switch (_state)
                {
                    case State.Swing: return PlayerPose.Attack;
                    case State.Dodge: return PlayerPose.Dodge;
                    case State.Whirl: return PlayerPose.Whirl;
                    case State.WaveCast: return PlayerPose.WaveCast;
                    case State.Down: return PlayerPose.Down;
                }
                if (Time.time - _hurtTime < 0.2f) return PlayerPose.Hurt;
                return IsMoving ? PlayerPose.Move : PlayerPose.Idle;
            }
        }

        public float PoseTime => _state == State.Down ? _downTimer : _state == State.Free ? Time.time - _hurtTime : _stateTime;

        public float PoseDuration
        {
            get
            {
                switch (_state)
                {
                    case State.Swing: return _swingDuration;
                    case State.Dodge: return DodgeTime;
                    case State.Whirl: return WhirlTime;
                    case State.WaveCast: return WaveCastTime;
                    default: return 0f;
                }
            }
        }

        public float PoseHitTime => _state == State.Swing && _step != null ? HitTime(0) : _state == State.Whirl ? WhirlTicks[0] : -1f;
        public int ComboIndex => Mathf.Max(0, _comboStepNumber - 1);
        public Vector2 FacingDirection => _facing;
        public bool IsMoving => _body && _body.linearVelocity.sqrMagnitude > 0.04f;
        /// <summary>회피 반격 창이 열려 있는가(시험 패널 표시용).</summary>
        public bool CounterReady => Tuning.DodgeCounter && Time.time <= _counterUntil;

        float CounterMultiplier => CounterReady ? 2f : 1f;

        /// <summary>장비 공격력(맨몸 100 + 무기)을 넣는다.</summary>
        public void SetAttack(int attack) => Attack = Mathf.Max(1, attack);

        /// <summary>마지막으로 ApplyStats로 넣은 능력치(장비 문서 2-3). 아직 넣지 않았으면 null(예전 상수 그대로 돎).</summary>
        public StatSheet Sheet { get; private set; }

        /// <summary>지금 공격 속도(‰). SwingTiming·조준 회전(TopDownPlayerRig)·타당 버팀이 쓴다. 넣기 전 0.</summary>
        public int AttackSpeedPermille => Sheet != null ? Sheet.AttackSpeedPermille : 0;

        /// <summary>
        /// 능력치 한 입구(장비 문서 2-3). 부르는 곳: 던전 = Inventory(장착·레벨·스킬이 바뀔 때), 전투 시험장 = CombatTestRoot(무기·손잡이·층이 바뀔 때).
        /// 공격력·치명 확률·치명 피해·최대 체력(SetMax: 늘어난 만큼 지금 체력도)·방어·무기 종류를 넣고, 나머지는 Sheet에서 바로 읽는다:
        /// 공격 속도(다음 StartSwing의 SwingTiming·타당 버팀), 이동(WalkSpeed), 재사용(× CooldownFactor, 남은 재사용은 새 최대로 자름),
        /// 스킬·보스 피해(DamageMath 인자), 체력 흡수(기본공격·스킬), 초당 재생, 처치 시 회복.
        /// 다시 불러도 같은 값이면 아무것도 바뀌지 않는다(체력·재사용·무기 그대로).
        /// </summary>
        public void ApplyStats(StatSheet sheet)
        {
            if (sheet == null) return;
            Sheet = sheet;
            Attack = Mathf.Max(1, sheet.Attack);
            CritChance = sheet.CritChance;
            CritDamage = sheet.CritDamage;
            if (_health)
            {
                _health.SetMax(sheet.MaxHp);
                _health.Defense = sheet.Defense;
            }
            // 재사용 감소가 늘면 돌고 있는 재사용도 새 최대를 넘지 않게 자른다(같은 값이면 그대로).
            _whirlCooldown = Mathf.Min(_whirlCooldown, WhirlCooldownMax);
            _waveCooldown = Mathf.Min(_waveCooldown, WaveCooldownMax);
            var rule = sheet.WeaponRule;
            if (rule != null) SetWeapon(rule);
        }

        /// <summary>스킬 재사용 배율(1 − 재사용 감소). 능력치를 넣기 전에는 1.</summary>
        public float CooldownFactor => Sheet != null ? Sheet.CooldownFactor : 1f;
        /// <summary>스킬 피해 보너스(0.1 = +10%). 회오리·검풍 피해에만 곱한다.</summary>
        double SkillDamageBonus => Sheet != null ? Sheet.SkillDamagePermille / 1000.0 : 0.0;
        /// <summary>보스 피해 보너스(0.1 = +10%). Enemy.IsBoss인 적에게만 곱한다.</summary>
        double BossDamageBonus => Sheet != null ? Sheet.BossDamagePermille / 1000.0 : 0.0;
        int LifeStealPermille => Sheet != null ? Sheet.LifeStealPermille : 0;
        int HpRegenPerSecond => Sheet != null ? Sheet.HpRegen : 0;
        int OnKillHealAmount => Sheet != null ? Sheet.OnKillHeal : 0;

        /// <summary>지금 휘두르는(또는 마지막) 동작의 시간표(SwingTiming.Plan). 시험 기록용.</summary>
        public SwingPlan CurrentSwingPlan => _plan;
        /// <summary>기본공격 동작 번호(StartSwing마다 1씩 오름). BasicHitInfo.ActionId와 같다.</summary>
        public int ActionId => _actionId;

        /// <summary>치명 굴림 전용 난수의 씨앗(판마다 다름, 장비 문서 3-4 '치명 난수'). 시험 기록에 함께 적는다.</summary>
        public ulong CritSeed { get; private set; }

        /// <summary>치명 굴림 난수를 이 씨앗으로 다시 시작한다(시험 재현용). 피해 굴림 흐름(_rng)은 건드리지 않는다.</summary>
        public void ReseedCrit(ulong seed)
        {
            CritSeed = seed;
            _critRng = new Pcg32Random(seed, CritStream);
        }

        /// <summary>지금 치명 확률로 치명을 한 번 굴린다(치명 전용 흐름). 플레이어 몫 피해를 따로 넣는 곳(전설 연쇄 번개 등)이 쓴다.</summary>
        public bool RollCrit() => _critRng.NextDouble() < CritChance;

        /// <summary>플레이어 겉모습 id 4개(장비 문서 9-1). 그림 쪽이 LookChanged를 듣고 몸·투구·주먹·장화를 고른다.</summary>
        public GearLook Look { get; private set; } = GearLook.Starting;
        public event System.Action LookChanged;

        /// <summary>겉모습을 바꾼다(Inventory가 갑옷·투구·장갑·장화를 바꿀 때). 같으면 알리지 않는다.</summary>
        public void SetLook(GearLook look)
        {
            if (look.Equals(Look)) return;
            Look = look;
            LookChanged?.Invoke();
        }

        /// <summary>레벨·스킬로 최대 체력이 바뀔 때. 늘어난 만큼 지금 체력도 늘린다.</summary>
        public void SetMaxHp(int max) => _health.SetMax(max);

        /// <summary>던전: 쓰러진 뒤 말뚝에서 다시 선다(물약·재사용 채움, 2초 무적).</summary>
        public void ReviveAt(Vector2 position)
        {
            Teleport(position);
            _whirlCooldown = 0f;
            _waveCooldown = 0f;
            _dodgeCooldown = 0f;
            _potionCooldown = 0f;
            _health.Revive();
            StandUp(true);
        }

        /// <summary>시험장 배치용 순간 이동.</summary>
        public void Teleport(Vector2 position)
        {
            _body.position = position;
            transform.position = position;
            _knockTime = 0f;
            _staggerTime = 0f;
            _walkVelocity = Vector2.zero;
            _walkSnap = false;
        }
        public float KillStreakTime { get; private set; } = -999f;
        public int BestKillStreak { get; private set; }
        public bool DodgeReady => _dodgeCooldown <= 0f && _state != State.Whirl && _state != State.WaveCast && _state != State.Down;
        public float WhirlCooldown => _whirlCooldown;
        public float WaveCooldown => _waveCooldown;
        public float DodgeCooldown => _dodgeCooldown;
        /// <summary>회오리 6초·검풍 9초 × (1 − 재사용 감소)(장비 문서 2-1). 감소 0이면 6·9 그대로.</summary>
        public float WhirlCooldownMax => WhirlCooldownTime * CooldownFactor;
        public float WaveCooldownMax => WaveCooldownTime * CooldownFactor;
        public float DodgeCooldownMax => DodgeCooldownTime;
        public int Potions { get; private set; } = MaxPotions;
        public float PotionCooldown => _potionCooldown;
        public int Downs { get; private set; }
        public event System.Action<WeaponAttackRule> WeaponChanged;

        Rigidbody2D _body;
        Health _health;
        PlayerInputReader _input;
        SpriteRenderer _bodySprite;
        SpriteFlash _flash;
        Transform _facingMark;
        Camera _cam;
        /// <summary>피해 굴림(0.92~1.08)과 몬스터가 때리는 피해 굴림. 예전 씨앗·흐름 그대로.</summary>
        readonly IRandom _rng = new Pcg32Random(20261002, 7);
        /// <summary>치명 굴림 전용 흐름 번호(피해 굴림 7, 처치 보상 23, 궤짝 41과 겹치지 않음).</summary>
        const ulong CritStream = 31;
        /// <summary>치명 굴림 전용(장비 문서 3-4): 치명 확률이 바뀌어도 피해 굴림 순서가 밀리지 않는다. 씨앗은 판마다 다르다(Awake).</summary>
        IRandom _critRng;
        readonly List<Collider2D> _overlap = new List<Collider2D>(32);
        readonly List<(Enemy enemy, float dist)> _targets = new List<(Enemy, float)>(16);
        readonly HashSet<Enemy> _seen = new HashSet<Enemy>();
        ContactFilter2D _enemyFilter;

        State _state;
        float _stateTime;
        Vector2 _aimWorld;
        Vector2 _aimDir = Vector2.right;
        Vector2 _facing = Vector2.right;

        Vector2 _swingDir;
        float _swingDuration;
        /// <summary>이 동작의 시간표(StartSwing에서 한 번 구함): 길이·판정 순간·이월 상한.</summary>
        SwingPlan _plan;
        ComboStep _step;
        int _comboIndex;
        int _comboStepNumber;
        float _comboExpire = -999f;
        int _hitsDone;
        int _actionId;
        bool _swingStopped;
        /// <summary>이 동작에서 보통·무거운 치명이 났는가(마지막 타에만 넉백을 주는 단계의 넉백 0.9 올림). 가벼운 치명은 보통 타와 같다.</summary>
        bool _swingStrongCrit;
        /// <summary>이 동작에서 낸 치명 연출 단계(동작마다 처음 치명 때 한 번 정함, 장비 문서 3-4).</summary>
        CritTier _swingCritShown;
        /// <summary>마지막으로 무거운 치명 연출을 낸 게임 시각(0.5초 제한).</summary>
        double _lastHeavyCrit = double.NegativeInfinity;
        /// <summary>체력 흡수·초당 재생의 1 미만 나머지(체력은 정수라 모았다가 넣는다).</summary>
        float _lifeStealCarry;
        float _regenCarry;
        /// <summary>휘두르는 중에 누른 구르기·스킬. 버퍼 0.15초가 지나도 쓸 수 있는 순간까지 보존한다.</summary>
        bool _pendingDodge;
        bool _pendingSkill1;
        bool _pendingSkill2;
        Enemy _lastTarget;
        float _lastAttackTime = -999f;
        float _lungeTime;
        Vector2 _lungeVelocity;

        Vector2 _dodgeDir;
        int _whirlTicksDone;
        Vector2 _waveDir;

        float _dodgeCooldown;
        float _whirlCooldown;
        float _waveCooldown;
        float _potionCooldown;
        float _knockTime;
        float _staggerTime;
        Vector2 _knockVelocity;
        float _downTimer;
        float _hurtTime = -999f;
        float _counterUntil = -999f;
        /// <summary>직접 걷는 속도(가감속을 거친 값). 휘두르기·스킬 중 걷기도 적어 두어 끝난 뒤 그 속도에서 이어 붙는다.</summary>
        Vector2 _walkVelocity;
        /// <summary>구르기·내딛기 직후 첫 걷기는 가감속 없이 바로 목표 속도(지금처럼 미끄러지지 않게).</summary>
        bool _walkSnap;

        public static PlayerController Create(Vector2 position)
        {
            var go = new GameObject("검사");
            go.layer = Layers.Player;
            go.transform.position = position;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = Radius;

            var visual = new GameObject("Body");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = Vector3.one * (Radius * 2f);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = ShapeSprites.Circle;
            sr.color = Palette.Player;

            var mark = new GameObject("Facing");
            mark.transform.SetParent(go.transform, false);
            var markSprite = mark.AddComponent<SpriteRenderer>();
            markSprite.sprite = ShapeSprites.Triangle;
            markSprite.color = Palette.Player;
            markSprite.sortingOrder = 1;
            var markVisual = mark.transform;
            markVisual.localScale = Vector3.one * 0.32f;

            var flash = go.AddComponent<SpriteFlash>();
            flash.target = sr;
            flash.baseColor = Palette.Player;

            go.AddComponent<Health>();
            go.AddComponent<PlayerInputReader>();
            go.AddComponent<YSort>();
            var player = go.AddComponent<PlayerController>();
            player._facingMark = markVisual;
            go.AddComponent<PlayerVisual>().Bind(player, sr, flash, markVisual);
            // 전설 고유 효과 3종(장비 문서 6장). 켜진 효과가 없으면 아무것도 하지 않는다.
            go.AddComponent<LegendEffects>();
            return player;
        }

        void Awake()
        {
            Instance = this;
            _body = GetComponent<Rigidbody2D>();
            _health = GetComponent<Health>();
            _input = GetComponent<PlayerInputReader>();
            _flash = GetComponent<SpriteFlash>();
            _bodySprite = _flash ? _flash.target : GetComponentInChildren<SpriteRenderer>();
            _enemyFilter = Layers.EnemyFilter();
            // 치명 굴림 씨앗은 판마다 다르다(시계). 시험 기록에는 CritSeed를 함께 적는다.
            ReseedCrit((ulong)System.DateTime.UtcNow.Ticks);
            _health.Init(2400, 120);
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 체력·물약을 채운다. 능력치를 아직 넣지 않았으면(ApplyStats 전) 예전처럼 층 기준 장비(공격·체력·방어)로 맞추고,
        /// 넣은 뒤에는 Sheet의 최대 체력·방어로 채우기만 한다(공격·체력·방어는 ApplyStats 한 입구가 정함, 장비 문서 2-3).
        /// </summary>
        public void ApplyBaseline(int floor)
        {
            if (Sheet != null)
            {
                _health.Init(Sheet.MaxHp, Sheet.Defense);
            }
            else
            {
                var b = FloorScaling.Baseline(floor);
                Attack = b.Attack;
                _health.Init(b.MaxHp, b.Defense);
            }
            _regenCarry = 0f;
            _lifeStealCarry = 0f;
            Potions = MaxPotions;
            if (_state == State.Down) StandUp(false);
        }

        public void SetWeapon(WeaponAttackRule weapon)
        {
            if (weapon == null || weapon == Weapon) return;
            Weapon = weapon;
            ResetCombo();
            if (_state == State.Swing) EnterFree();
            WeaponChanged?.Invoke(weapon);
        }

        public void Refill()
        {
            _health.Revive();
            Potions = MaxPotions;
            _potionCooldown = 0f;
            _whirlCooldown = 0f;
            _waveCooldown = 0f;
            _dodgeCooldown = 0f;
            if (_state == State.Down) StandUp(false);
        }

        /// <summary>
        /// 계단으로 내려온 새 장면에서 원정 몫(매판 새 탐험 1차 3-3)의 체력·물약을 잇는다. 레벨 체력을 넣은 뒤 부른다.
        /// 값이 음수면(가득) 그대로 둔다.
        /// </summary>
        public void RestoreVitals(int hp, int potions)
        {
            if (hp > 0) _health.SetCurrent(hp);
            if (potions >= 0) Potions = Mathf.Clamp(potions, 0, MaxPotions);
        }

        /// <summary>쓰러짐에서 일어난다. 자연 부활이면 물약을 채우고 2초 무적과 깜빡임을 준다.</summary>
        void StandUp(bool naturalRevive)
        {
            _bodySprite.transform.localScale = Vector3.one * (Radius * 2f);
            if (_health.Dead) _health.Revive();
            EnterFree();
            if (naturalRevive)
            {
                Potions = MaxPotions;
                _health.GrantInvulnerability(2f);
                _flash.Blink(2f);
            }
        }

        public void ResetDowns()
        {
            Downs = 0;
            BestKillStreak = 0;
            KillStreak = 0;
        }

        void ResetCombo()
        {
            _comboIndex = 0;
            _comboExpire = -999f;
        }

        /// <summary>몬스터가 플레이어를 때릴 때 부른다.</summary>
        /// <returns>맞았는가(무적·쓰러짐이면 false).</returns>
        /// <param name="opensCounter">구르기로 피하면 회피 반격 창을 여는 공격인가(적의 예고 공격). 놓여 있는 덫은 false.</param>
        public bool ReceiveHit(int monsterAttack, float patternPercent, Vector2 from, float knockback, bool opensCounter = true)
        {
            if (_state == State.Down) return false;
            if (_health.IsInvulnerable)
            {
                // 구르기 무적으로 피했다: 회피 반격 창을 연다(선택 규칙).
                if (opensCounter && _health.ExtraInvulnerable && Tuning.DodgeCounter) _counterUntil = Time.time + CounterWindow + DodgeTime;
                return false;
            }
            int damage = DamageMath.ToPlayer(monsterAttack, patternPercent, DamageMath.Roll(_rng), _health.Defense);
            // 피격 연출(피 튀는 방향)이 실제로 때린 쪽을 쓰게 피해를 넣기 전에 적어 둔다.
            LastHitFrom = from;
            if (Tuning.Invincible)
            {
                WorldOverlay.Number(Position + Vector2.up * Radius, 0, NumberKind.Taken);
                _health.GrantInvulnerability(HurtInvulnerable);
                _flash.Flash(Palette.NumberTaken, HurtFlash);
                _flash.Blink(HurtInvulnerable);
                return true;
            }
            int applied = _health.ApplyDamage(damage, false);
            if (applied <= 0) return false;
            // 회오리 베기 중에는 넉백·경직 면역(피해는 받음).
            if (_state != State.Whirl && knockback > 0f)
            {
                Vector2 dir = Position - from;
                if (dir.sqrMagnitude < 0.0001f) dir = -_facing;
                _knockVelocity = dir.normalized * (knockback * Tuning.KnockbackScale / 0.1f);
                _knockTime = 0.1f;
                _staggerTime = 0f;
            }
            return true;
        }

        void OnDamaged(int amount, bool crit)
        {
            _hurtTime = Time.time;
            LastCombatActionTime = Time.time;
            Sfx.Play(SfxKind.Hurt);
            WorldOverlay.Number(Position + Vector2.up * Radius, amount, NumberKind.Taken);
            CombatEvents.RaisePlayerDamaged(amount);
            _health.GrantInvulnerability(HurtInvulnerable);
            _flash.Flash(Palette.NumberTaken, HurtFlash);
            _flash.Blink(HurtInvulnerable);
            ScreenShake.Add(0.12f, 0.12f);
        }

        void OnDied()
        {
            ResetCombo();
            _state = State.Down;
            _downTimer = 0f;
            _health.ExtraInvulnerable = false;
            Downs++;
            CombatEvents.RaisePlayerDowned();
            _bodySprite.transform.localScale = new Vector3(Radius * 2f, Radius * 0.9f, 1f);
        }

        void Update()
        {
            if (TimeScaleService.Paused) return;
            float dt = Time.deltaTime;

            if (!_cam) _cam = Camera.main;
            if (_cam)
            {
                Vector3 sp = _input.PointerScreen;
                sp.z = -_cam.transform.position.z;
                _aimWorld = _cam.ScreenToWorldPoint(sp);
                Vector2 toAim = _aimWorld - Position;
                if (toAim.sqrMagnitude > 0.0001f) _aimDir = toAim.normalized;
            }

            if (_input.WeaponSelected >= 0) SetWeapon(WeaponPresets.All[_input.WeaponSelected]);

            _dodgeCooldown = Mathf.Max(0f, _dodgeCooldown - dt);
            _whirlCooldown = Mathf.Max(0f, _whirlCooldown - dt);
            _waveCooldown = Mathf.Max(0f, _waveCooldown - dt);
            _potionCooldown = Mathf.Max(0f, _potionCooldown - dt);
            Regenerate(dt);

            if (_state == State.Down)
            {
                _downTimer += dt;
                if (AutoRevive && _downTimer >= ReviveDelay)
                {
                    _whirlCooldown = 0f;
                    _waveCooldown = 0f;
                    _dodgeCooldown = 0f;
                    _potionCooldown = 0f;
                    StandUp(true);
                }
                return;
            }

            if (_input.PotionPressed) TryDrinkPotion();

            switch (_state)
            {
                case State.Free:
                    TryStartAction(true);
                    break;

                case State.Swing:
                    _stateTime += dt;
                    _lastAttackTime = Time.time;
                    HoldPendingInputs();
                    while (_hitsDone < _step.hits && _stateTime >= HitTime(_hitsDone))
                        SwingHit(_hitsDone++);
                    // 기획 3-2: 판정이 나간 뒤에는 구르기로 동작을 끊을 수 있다. 스킬은 휘두르기가 끝나면 바로 나간다.
                    if (_hitsDone >= 1 && _pendingDodge && _dodgeCooldown <= 0f)
                    {
                        StartDodge();
                        break;
                    }
                    if (_stateTime >= _swingDuration)
                    {
                        float carry = _stateTime - _swingDuration;
                        // 마무리까지 갔으면 1단계로, 아니면 잠깐 동안 다음 단계를 이어 갈 수 있다.
                        _comboExpire = Time.time + Weapon.comboResetTime;
                        if (_step.finisher) _comboIndex = 0;
                        if (_pendingSkill1 && _whirlCooldown <= 0f) StartWhirl();
                        else if (_pendingSkill2 && _waveCooldown <= 0f) StartWave();
                        else if (_pendingDodge && _dodgeCooldown <= 0f) StartDodge();
                        else if (_input.AttackHeld)
                        {
                            // 넘친 시간을 다음 휘두르기로 넘겨 프레임에 따라 공격 속도가 줄지 않게 한다(상한 = 새 동작 길이 × 0.5, 장비 문서 3-3 규칙 5).
                            StartSwing();
                            _stateTime = Mathf.Min(carry, _plan.CarryCap);
                            while (_hitsDone < _step.hits && _stateTime >= HitTime(_hitsDone))
                                SwingHit(_hitsDone++);
                        }
                        else EnterFree();
                    }
                    break;

                case State.Dodge:
                    _stateTime += dt;
                    _health.ExtraInvulnerable = _stateTime < DodgeInvulnerable;
                    if (_stateTime >= DodgeTime) EnterFree();
                    break;

                case State.Whirl:
                    _stateTime += dt;
                    while (_whirlTicksDone < WhirlTicks.Length && _stateTime >= WhirlTicks[_whirlTicksDone])
                    {
                        WhirlTick();
                        _whirlTicksDone++;
                    }
                    if (_stateTime >= WhirlTime) EnterFree();
                    break;

                case State.WaveCast:
                    _stateTime += dt;
                    if (_stateTime >= WaveCastTime)
                    {
                        SwordWave.Spawn(Position + _waveDir * (Radius + 0.1f), _waveDir, this);
                        EnterFree();
                    }
                    break;
            }

            UpdateFacing();
        }

        /// <param name="allowAttack">자유 상태에서는 기본공격도 시작할 수 있다.</param>
        void HoldPendingInputs()
        {
            if (_input.DodgeBuffered)
            {
                _pendingDodge = true;
                _input.ConsumeDodge();
            }
            if (_input.Skill1Buffered)
            {
                _pendingSkill1 = true;
                _input.ConsumeSkill1();
            }
            if (_input.Skill2Buffered)
            {
                _pendingSkill2 = true;
                _input.ConsumeSkill2();
            }
        }

        void ClearPending()
        {
            _pendingDodge = false;
            _pendingSkill1 = false;
            _pendingSkill2 = false;
        }

        bool TryStartAction(bool allowAttack)
        {
            if (_input.DodgeBuffered && _dodgeCooldown <= 0f)
            {
                StartDodge();
                return true;
            }
            if (_input.Skill1Buffered && _whirlCooldown <= 0f)
            {
                StartWhirl();
                return true;
            }
            if (_input.Skill2Buffered && _waveCooldown <= 0f)
            {
                StartWave();
                return true;
            }
            if (allowAttack && _input.AttackHeld)
            {
                StartSwing();
                return true;
            }
            return false;
        }

        void FixedUpdate()
        {
            float fdt = Time.fixedDeltaTime;
            Vector2 velocity;
            Vector2 move = _input.Move;
            if (move.sqrMagnitude > 1f) move.Normalize();

            if (_knockTime > 0f)
            {
                velocity = _knockVelocity;
                _knockTime -= fdt;
                // 기획 11-6: 넉백 뒤 경직 0.15초 동안은 이동 입력으로 덮어쓰지 않는다.
                if (_knockTime <= 0f) _staggerTime = 0.15f;
                StopWalk();
            }
            else if (_staggerTime > 0f && _state != State.Dodge)
            {
                velocity = Vector2.zero;
                _staggerTime -= fdt;
                StopWalk();
            }
            else
            {
                switch (_state)
                {
                    case State.Swing:
                        if (_lungeTime > 0f)
                        {
                            velocity = _lungeVelocity;
                            _lungeTime -= fdt;
                            _walkSnap = true;
                        }
                        else velocity = ActionWalk(move * (MoveSpeed * (_step != null ? _step.moveScale : 0.4f)));
                        break;
                    case State.Dodge:
                        velocity = _dodgeDir * (DodgeDistance / DodgeTime);
                        _walkSnap = true;
                        break;
                    case State.Whirl:
                        velocity = ActionWalk(move * (MoveSpeed * WhirlMoveScale));
                        break;
                    case State.WaveCast:
                        velocity = ActionWalk(move * (MoveSpeed * WaveCastMoveScale));
                        break;
                    case State.Down:
                        velocity = Vector2.zero;
                        StopWalk();
                        break;
                    default:
                        velocity = FreeWalk(move * MoveSpeed, fdt);
                        break;
                }
            }
            if (TimeScaleService.Paused) velocity = Vector2.zero;
            _body.linearVelocity = velocity;
        }

        /// <summary>넉백·경직·쓰러짐: 몸이 멈췄으니 다음 걷기는 멈춘 데서 다시 붙는다.</summary>
        void StopWalk()
        {
            _walkVelocity = Vector2.zero;
            _walkSnap = false;
        }

        /// <summary>휘두르기·회오리·검풍 중 걷기: 지금처럼 바로 그 속도. 끝난 뒤 자유 걷기가 이 속도에서 이어 붙게 적어 둔다.</summary>
        Vector2 ActionWalk(Vector2 velocity)
        {
            _walkVelocity = velocity;
            _walkSnap = false;
            return velocity;
        }

        /// <summary>자유 걷기. 가감속(Tuning.MoveInertia)을 켜면 목표 속도로 천천히 붙는다. 구르기·내딛기 직후 첫 걸음은 지금처럼 바로 목표 속도.</summary>
        Vector2 FreeWalk(Vector2 target, float dt)
        {
            if (!Tuning.MoveInertia || _walkSnap) _walkVelocity = target;
            else _walkVelocity = ApproachWalk(_walkVelocity, target, MoveSpeed, dt);
            _walkSnap = false;
            return _walkVelocity;
        }

        /// <summary>
        /// 가는 쪽 성분은 다 빨라지기까지 WalkAccelTime, 넘치거나 반대로 가는 성분과 옆 성분은 서기까지 WalkDecelTime 속도로 붙인다.
        /// 돌아설 때 먼저 멈췄다가(0.10초) 다시 붙어(0.16초) 몸이 무겁게 느껴진다.
        /// </summary>
        static Vector2 ApproachWalk(Vector2 current, Vector2 target, float maxSpeed, float dt)
        {
            if (maxSpeed <= 0f) return target;
            float accel = maxSpeed / WalkAccelTime * dt;
            float decel = maxSpeed / WalkDecelTime * dt;
            float targetSpeed = target.magnitude;
            if (targetSpeed < 0.0001f) return Vector2.MoveTowards(current, Vector2.zero, decel);
            Vector2 dir = target / targetSpeed;
            float along = Vector2.Dot(current, dir);
            Vector2 side = current - dir * along;
            if (along < 0f) along = Mathf.Min(0f, along + decel);
            else if (along < targetSpeed) along = Mathf.Min(targetSpeed, along + accel);
            else along = Mathf.Max(targetSpeed, along - decel);
            side = Vector2.MoveTowards(side, Vector2.zero, decel);
            return dir * along + side;
        }

        void EnterFree()
        {
            _state = State.Free;
            _stateTime = 0f;
            _lungeTime = 0f;
            _health.ExtraInvulnerable = false;
            ClearPending();
        }

        /// <summary>k번째 판정 시각 = 첫 판정 + k × 연타 간격(SwingTiming.Plan, 공격 속도 0이면 예전 식과 비트까지 같음).</summary>
        float HitTime(int index) => _plan.HitTime(index);

        void StartSwing()
        {
            LastCombatActionTime = Time.time;
            if (Time.time > _comboExpire || _comboIndex >= Weapon.combo.Length) _comboIndex = 0;
            _step = Weapon.combo[_comboIndex];
            _comboStepNumber = _comboIndex + 1;
            _comboIndex = (_comboIndex + 1) % Weapon.combo.Length;
            _comboExpire = float.MaxValue;

            _state = State.Swing;
            _stateTime = 0f;
            _hitsDone = 0;
            _actionId++;
            _swingStopped = false;
            _swingStrongCrit = false;
            _swingCritShown = CritTier.None;
            ClearPending();
            // 공격 속도는 동작 길이·판정 순간·이월 상한에만 쓴다(장비 문서 3-3). 다가가기·내딛기 0.1, 콤보 끊김, 입력 버퍼는 그대로.
            _plan = SwingTiming.Plan(_step, AttackSpeedPermille);
            _swingDuration = _plan.Duration;
            _lungeTime = 0f;

            float reach = _step.Reach;
            var target = AutoTargeter.Pick(Position, _aimWorld, reach, _input.Move, _lastTarget, Time.time - _lastAttackTime);
            float lunge = 0f;
            if (target)
            {
                Vector2 to = target.Position - Position;
                _swingDir = to.sqrMagnitude > 0.0001f ? to.normalized : _aimDir;
                float gap = to.magnitude - target.Radius - reach;
                // 2차 규칙: 사거리 밖이면 0.1초 동안 최대 1.5유닛 다가가서 휘두른다.
                if (Tuning.SmartTargeting && gap > 0f) lunge = Mathf.Min(AutoTargeter.ExtraSearch, gap + 0.2f);
            }
            else
            {
                _swingDir = _aimDir;
            }
            // 마무리 동작은 앞으로 한 발 내딛는다(찌르기·내려찍기). 플레이어와 적은 서로 밀지 않으므로 대상 몸 앞에서 멈춘다.
            float advance = _step.advance;
            if (target) advance = Mathf.Min(advance, Mathf.Max(0f, (target.Position - Position).magnitude - target.Radius - Radius));
            lunge = Mathf.Max(lunge, advance);
            if (lunge > 0f)
            {
                _lungeVelocity = _swingDir * (lunge / 0.1f);
                _lungeTime = 0.1f;
            }
            _lastTarget = target;
            _lastAttackTime = Time.time;
            _facing = _swingDir;
        }

        void SwingHit(int index)
        {
            var step = _step;
            bool last = index == step.hits - 1;
            CollectStepTargets(step, Position, _swingDir);
            SwingVisual.ShowStep(Weapon, _comboStepNumber - 1, step, Position, _swingDir, index);
            var stepArt = StepArtNow();
            Sfx.Play(SfxKind.Swing, stepArt?.swingSound);

            bool anyHit = false;
            bool anyCrit = false;
            bool heavyKill = false;
            bool finisherOnBroken = false;
            int kills = 0;
            int targetsHit = 0;
            Enemy firstTarget = null;
            float counter = CounterMultiplier;
            float percent = step.finisher ? step.hitPercent * (1f + FinisherDamageBonus) : step.hitPercent;
            // 치명이면 이 타의 무게 단계(장비 문서 3-4: 그 타 배율 × 치명 피해). 한 동작의 타는 배율이 같아 단계도 같다.
            var critTier = CritTiers.Of(percent, CritDamage);
            // 타당 버팀 = 단계 버팀 ÷ (1 + 공격 속도)(3-4: 초당 버팀 깎기를 무기마다 고정). 공격 속도 0이면 그대로.
            float poise = step.poiseDamage * SwingTiming.PoiseScale(AttackSpeedPermille);
            double bossBonus = BossDamageBonus;
            foreach (var (enemy, _) in _targets)
            {
                // 치명 굴림은 전용 흐름, 피해 굴림은 예전 흐름(장비 문서 3-4 '치명 난수').
                bool crit = RollCrit();
                int damage = DamageMath.ToMonster(Attack, percent, crit, CritDamage, DamageMath.Roll(_rng), 0, 0.0, false, bossBonus, enemy.IsBoss);
                // 치명 숫자 크기(가벼움 1.2배, 보통·무거움 1.4배)를 이 한 번에만 알린다.
                if (crit) WorldOverlay.SetNextCritTier(critTier);
                int applied = enemy.TakeHit(damage, crit, DamageSource.Basic, poise, step.finisher, counter, out _, out bool wasBroken);
                if (crit) WorldOverlay.ClearNextCritTier();
                if (applied <= 0) continue;
                anyHit = true;
                targetsHit++;
                if (!firstTarget) firstTarget = enemy;
                StealLife(applied);
                var shown = CritTier.None;
                if (crit)
                {
                    anyCrit = true;
                    shown = DecideCrit(ref _swingCritShown, critTier, step.finisher);
                }
                bool strong = shown >= CritTier.Normal;
                _swingStrongCrit |= strong;
                if (enemy.Dead)
                {
                    kills++;
                    heavyKill |= enemy.IsV3 && enemy.Weight == EnemyWeight.Heavy;
                }
                bool onBroken = wasBroken && step.finisher;
                finisherOnBroken |= onBroken;
                Vector2 away = step.shape == ComboShape.Line ? _swingDir : enemy.Position - HitCenter(step, Position, _swingDir);
                if (away.sqrMagnitude < 0.0001f) away = _swingDir;
                // 마지막 타에만 넉백을 주는 단계는 첫 타가 치명이어도 밀지 않고, 마지막 타를 0.9로 올린다.
                // 넉백 최소 0.9는 보통·무거운 치명만(가벼운 치명은 보통 타와 같음, 장비 문서 3-4).
                float knock = step.knockbackOnLastHitOnly && !last ? 0f : step.knockback;
                if (knock > 0f && (strong || (step.knockbackOnLastHitOnly && _swingStrongCrit))) knock = Mathf.Max(knock, 0.9f);
                enemy.ApplyKnockback(away, knock);
                // 무너진 적에게 마무리: 파편 12개(3차 초안 3-4).
                HitEffects.OnHit(enemy, away, shown, step.finisher, onBroken ? Mathf.Max(0, 12 - (strong ? 10 : 8)) : 0);
            }

            CombatEvents.RaisePlayerSwing(Position, _swingDir, step, anyHit);
            if (!anyHit) return;
            _counterUntil = -999f;
            // 이 판정의 치명 연출 단계(동작에서 정한 하나). 이 판정에 치명이 없으면 없음.
            var tierNow = anyCrit ? _swingCritShown : CritTier.None;
            if (stepArt != null && stepArt.impactSound)
            {
                // 단계 전용 타격음 위에 치명·처치 소리를 겹친다.
                Sfx.Play(SfxKind.Hit, stepArt.impactSound);
                if (kills > 0) Sfx.Play(SfxKind.Kill);
                else if (anyCrit) Sfx.Play(CritSound(tierNow));
            }
            else Sfx.Play(kills > 0 ? SfxKind.Kill : anyCrit ? CritSound(tierNow) : SfxKind.Hit);
            // 히트스톱은 동작 1번에 1회만 준다. 마무리는 더 길다. 무거운 치명·처치만 0.06 이상(가벼움·보통 치명은 보통 타와 같음).
            if (!_swingStopped)
            {
                _swingStopped = true;
                TimeScaleService.HitStop(tierNow == CritTier.Heavy || kills > 0 ? Mathf.Max(step.hitStop, 0.06f) : step.hitStop);
            }
            // 3차: 무너진 적에게 마무리 0.1초, 무거운 적 처치 0.12초(한 마리의 무게를 마지막에 갚아 줌). 더 길 때만 덮어쓴다.
            if (finisherOnBroken) TimeScaleService.HitStop(0.1f);
            if (heavyKill)
            {
                TimeScaleService.HitStop(0.12f);
                ScreenShake.Add(0.1f, 0.12f);
            }
            if (step.shake > 0f) ScreenShake.Add(step.shake, 0.1f);
            CritShake(tierNow);
            OnKills(kills);
            // 전설 연쇄 번개의 발동 자리(장비 문서 6장): 맞힌 판정마다 한 번. 번개는 동작 번호가 바뀐 첫 사건만 굴린다.
            CombatEvents.RaiseBasicHit(new BasicHitInfo(_actionId, index, step.finisher, firstTarget, targetsHit, anyCrit, Position, _swingDir));
        }

        /// <summary>
        /// 치명 연출 단계를 한 번 정한다(장비 문서 3-4). shown이 비어 있으면 무거움 0.5초 제한(마무리 예외)을 거쳐 정하고 RaiseCritShown을 한 번 낸다.
        /// 이미 정했으면(같은 동작·회오리 한 타·검풍 한 번 안의 두 번째 치명부터) 그 단계를 그대로 쓴다.
        /// </summary>
        CritTier DecideCrit(ref CritTier shown, CritTier tier, bool finisher)
        {
            if (shown != CritTier.None) return shown;
            shown = CritTiers.Gate(tier, Time.time, ref _lastHeavyCrit, finisher);
            CombatEvents.RaiseCritShown(shown, finisher);
            return shown;
        }

        /// <summary>치명 소리: 가벼움은 짧은 치명 소리, 보통·무거움은 치명 소리(장비 문서 3-4).</summary>
        static SfxKind CritSound(CritTier tier) => tier == CritTier.Light ? SfxKind.CritLight : SfxKind.Crit;

        /// <summary>치명 흔들림: 보통 0.04/0.06, 무거움 0.06/0.08(예전 치명 연출). 가벼움은 보통 타와 같아 없음.</summary>
        static void CritShake(CritTier tier)
        {
            if (tier == CritTier.Heavy) ScreenShake.Add(0.06f, 0.08f);
            else if (tier == CritTier.Normal) ScreenShake.Add(0.04f, 0.06f);
        }

        /// <summary>체력 흡수(장비 문서 2-1): 기본공격·스킬이 실제로 넣은 피해 × ‰. 전설 피해는 여기를 지나지 않는다. 1 미만은 모았다가 넣는다.</summary>
        void StealLife(int applied)
        {
            int permille = LifeStealPermille;
            if (permille <= 0 || applied <= 0 || _health.Dead) return;
            _lifeStealCarry += applied * permille / 1000f;
            if (_lifeStealCarry < 1f) return;
            int heal = Mathf.FloorToInt(_lifeStealCarry);
            _lifeStealCarry -= heal;
            _health.Heal(heal);
        }

        /// <summary>초당 체력 재생(장비 문서 2-1): 게임 시간으로 모아 정수만큼 넣는다. 쓰러진 동안과 가득 찬 동안은 모으지 않는다.</summary>
        void Regenerate(float dt)
        {
            int perSecond = HpRegenPerSecond;
            if (perSecond <= 0 || _state == State.Down || _health.Dead || _health.Current >= _health.Max)
            {
                _regenCarry = 0f;
                return;
            }
            if (dt <= 0f) return;
            _regenCarry += perSecond * dt;
            if (_regenCarry < 1f) return;
            int heal = Mathf.FloorToInt(_regenCarry);
            _regenCarry -= heal;
            _health.Heal(heal);
        }

        StepArt StepArtNow()
        {
            var set = ArtRuntime.Active;
            return set ? set.Weapon(Weapon.id)?.Step(ComboIndex) : null;
        }

        /// <summary>
        /// 플레이어 몫 처치를 센다(전설 효과처럼 PlayerController 밖에서 쓰러뜨린 적). 연속 처치 수는 늘 올리고,
        /// juice면 여러 마리 처치 연출(히트스톱 0.08·느린 화면)도 낸다. 연쇄 폭발은 연쇄 하나에 처음 한 번만 juice = true(장비 문서 6장).
        /// </summary>
        public void AddKills(int kills, bool juice)
        {
            if (kills <= 0) return;
            if (juice) OnKills(kills);
            else CountKills(kills);
        }

        void CountKills(int kills)
        {
            KillStreak = Time.time - KillStreakTime <= 2f ? KillStreak + kills : kills;
            KillStreakTime = Time.time;
            if (KillStreak > BestKillStreak) BestKillStreak = KillStreak;
            // 처치 시 체력 회복(장비 문서 2-1): 처치마다(전설 효과로 쓰러뜨린 적도 플레이어 처치로 센다).
            int heal = OnKillHealAmount;
            if (heal > 0 && !_health.Dead) _health.Heal(heal * kills);
        }

        /// <summary>한 번에 여러 마리를 쓰러뜨렸을 때의 보상 연출과 연속 처치 수.</summary>
        void OnKills(int kills)
        {
            if (kills <= 0) return;
            CountKills(kills);
            if (!Tuning.MultiKillJuice) return;
            if (kills >= 5)
            {
                TimeScaleService.HitStop(0.08f);
                TimeScaleService.SlowMotion(0.18f, 0.35f);
                ScreenShake.Add(0.14f, 0.16f);
            }
            else if (kills >= 3)
            {
                TimeScaleService.HitStop(0.08f);
                ScreenShake.Add(0.1f, 0.12f);
            }
        }

        static Vector2 HitCenter(ComboStep step, Vector2 origin, Vector2 dir) =>
            step.shape == ComboShape.Circle ? origin + dir * step.centerOffset : origin;

        /// <summary>콤보 단계 모양(부채꼴·직선·원) 안의 적을 가까운 순으로 담는다.</summary>
        void CollectStepTargets(ComboStep step, Vector2 origin, Vector2 dir)
        {
            switch (step.shape)
            {
                case ComboShape.Line:
                    CollectLineTargets(origin, dir, step.size, step.width, step.maxTargets);
                    break;
                case ComboShape.Circle:
                    CollectTargets(HitCenter(step, origin, dir), step.size, 360f, dir, step.maxTargets);
                    break;
                default:
                    CollectTargets(origin, step.size, step.arcDeg, dir, step.maxTargets);
                    break;
            }
        }

        void CollectLineTargets(Vector2 origin, Vector2 dir, float length, float width, int maxTargets)
        {
            _targets.Clear();
            _overlap.Clear();
            _seen.Clear();
            Vector2 side = new Vector2(-dir.y, dir.x);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Physics2D.OverlapBox(origin + dir * (length * 0.5f), new Vector2(length + 2.6f, width + 2.6f), angle, _enemyFilter, _overlap);
            foreach (var col in _overlap)
            {
                var enemy = col ? col.GetComponent<Enemy>() : null;
                if (!enemy || enemy.Dead || !_seen.Add(enemy)) continue;
                Vector2 local = enemy.Position - origin;
                float along = Vector2.Dot(local, dir);
                float across = Mathf.Abs(Vector2.Dot(local, side));
                if (along < -enemy.Radius || along > length + enemy.Radius || across > width * 0.5f + enemy.Radius) continue;
                if (Physics2D.Linecast(origin, enemy.Position, Layers.WallMask)) continue;
                _targets.Add((enemy, along));
            }
            _targets.Sort((a, b) => a.dist.CompareTo(b.dist));
            if (_targets.Count > maxTargets) _targets.RemoveRange(maxTargets, _targets.Count - maxTargets);
        }

        /// <summary>부채꼴 안의 적을 가까운 순으로 maxTargets개까지 _targets에 담는다. arcDeg ≥ 360이면 원.</summary>
        void CollectTargets(Vector2 center, float range, float arcDeg, Vector2 dir, int maxTargets)
        {
            _targets.Clear();
            _overlap.Clear();
            _seen.Clear();
            Physics2D.OverlapCircle(center, range + 1.3f, _enemyFilter, _overlap);
            foreach (var col in _overlap)
            {
                var enemy = col ? col.GetComponent<Enemy>() : null;
                if (!enemy || enemy.Dead || !_seen.Add(enemy)) continue;
                Vector2 to = enemy.Position - center;
                float centerDist = to.magnitude;
                float edgeDist = centerDist - enemy.Radius;
                if (edgeDist > range) continue;
                if (arcDeg < 360f && centerDist > enemy.Radius + Radius)
                {
                    float slack = Mathf.Asin(Mathf.Clamp01(enemy.Radius / centerDist)) * Mathf.Rad2Deg;
                    if (Vector2.Angle(dir, to) > arcDeg * 0.5f + slack) continue;
                }
                if (Physics2D.Linecast(center, enemy.Position, Layers.WallMask)) continue;
                _targets.Add((enemy, centerDist));
            }
            _targets.Sort((a, b) => a.dist.CompareTo(b.dist));
            if (_targets.Count > maxTargets) _targets.RemoveRange(maxTargets, _targets.Count - maxTargets);
        }

        void StartDodge()
        {
            _input.ConsumeDodge();
            Vector2 move = _input.Move;
            _dodgeDir = move.sqrMagnitude > 0.01f ? move.normalized : _aimDir;
            _state = State.Dodge;
            _stateTime = 0f;
            _dodgeCooldown = DodgeCooldownTime;
            _knockTime = 0f;
            _staggerTime = 0f;
            _lungeTime = 0f;
            _health.ExtraInvulnerable = true;
            _facing = _dodgeDir;
            ClearPending();
            ResetCombo();
            Sfx.Play(SfxKind.Dodge);
        }

        void StartWhirl()
        {
            LastCombatActionTime = Time.time;
            _input.ConsumeSkill1();
            _state = State.Whirl;
            _stateTime = 0f;
            _whirlTicksDone = 0;
            _whirlCooldown = WhirlCooldownMax;
            _lungeTime = 0f;
            _knockTime = 0f;
            _staggerTime = 0f;
            ClearPending();
            ResetCombo();
        }

        void WhirlTick()
        {
            CollectTargets(Position, WhirlRadiusNow, 360f, Vector2.right, 64);
            SwingVisual.ShowRing(Position, WhirlRadiusNow);
            Sfx.Play(SfxKind.Swing);
            bool anyHit = false;
            bool anyCrit = false;
            bool heavyKill = false;
            int kills = 0;
            // 회오리 한 타의 치명 단계(1타 90% × 치명 피해, 장비 문서 3-4). 연출은 이 한 타에 하나.
            var critTier = CritTiers.Of(WhirlPercent, CritDamage);
            var shownTier = CritTier.None;
            double skillBonus = SkillDamageBonus;
            double bossBonus = BossDamageBonus;
            foreach (var (enemy, _) in _targets)
            {
                bool crit = RollCrit();
                int damage = DamageMath.ToMonster(Attack, WhirlPercent, crit, CritDamage, DamageMath.Roll(_rng), 0, skillBonus, true, bossBonus, enemy.IsBoss);
                if (crit) WorldOverlay.SetNextCritTier(critTier);
                int applied = enemy.TakeHit(damage, crit, DamageSource.Whirlwind, WhirlPoise, false, CounterMultiplier, out _, out _);
                if (crit) WorldOverlay.ClearNextCritTier();
                if (applied <= 0) continue;
                anyHit = true;
                StealLife(applied);
                var shown = CritTier.None;
                if (crit)
                {
                    anyCrit = true;
                    shown = DecideCrit(ref shownTier, critTier, false);
                }
                if (enemy.Dead)
                {
                    kills++;
                    heavyKill |= enemy.IsV3 && enemy.Weight == EnemyWeight.Heavy;
                }
                Vector2 away = enemy.Position - Position;
                // 보통·무거운 치명만 넉백 0.9(가벼운 치명은 보통 타와 같음).
                enemy.ApplyKnockback(away, shown >= CritTier.Normal ? 0.9f : WhirlKnockback);
                HitEffects.OnHit(enemy, away, shown, false);
            }
            CombatEvents.RaisePlayerWhirl(Position, WhirlRadiusNow, anyHit);
            if (!anyHit) return;
            _counterUntil = -999f;
            // 기획 3-6: 회오리 1타 0.02, 무거운 치명·마지막 일격 0.06.
            TimeScaleService.HitStop(shownTier == CritTier.Heavy || kills > 0 ? 0.06f : 0.02f);
            if (heavyKill)
            {
                TimeScaleService.HitStop(0.12f);
                ScreenShake.Add(0.1f, 0.12f);
            }
            Sfx.Play(kills > 0 ? SfxKind.Kill : anyCrit ? CritSound(shownTier) : SfxKind.Hit);
            CritShake(shownTier);
            OnKills(kills);
        }

        void StartWave()
        {
            LastCombatActionTime = Time.time;
            _input.ConsumeSkill2();
            _state = State.WaveCast;
            _stateTime = 0f;
            _waveCooldown = WaveCooldownMax;
            _lungeTime = 0f;
            ClearPending();
            ResetCombo();
            Sfx.Play(SfxKind.Swing);
            // 커서 1.5 안에 적이 있으면 그 적 방향.
            Enemy near = null;
            float best = AutoTargeter.CursorRadius;
            foreach (var e in Enemy.All)
            {
                if (!e || e.Dead || (e.VisionHidden && !e.VisionInSight)) continue;
                float d = (e.Position - _aimWorld).magnitude - e.Radius;
                if (d <= best)
                {
                    best = d;
                    near = e;
                }
            }
            Vector2 to = near ? near.Position - Position : _aimDir;
            _waveDir = to.sqrMagnitude > 0.0001f ? to.normalized : _aimDir;
            _facing = _waveDir;
        }

        void TryDrinkPotion()
        {
            if (Potions <= 0 || _potionCooldown > 0f || _health.Current >= _health.Max) return;
            int healed = _health.Heal(Mathf.RoundToInt(_health.Max * PotionHeal));
            Potions--;
            _potionCooldown = PotionCooldownTime;
            WorldOverlay.Number(Position + Vector2.up * Radius, healed, NumberKind.Heal);
        }

        void UpdateFacing()
        {
            if (_state == State.Free || _state == State.Whirl) _facing = _aimDir;
            if (!_facingMark) return;
            _facingMark.localPosition = _facing * (Radius + 0.12f);
            _facingMark.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_facing.y, _facing.x) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// 검풍이 맞힌 적에게 피해를 준다. waveShown은 이 검풍 한 번의 치명 연출 단계(SwordWave가 들고 있음):
        /// 비어 있으면 첫 치명에서 정하고(무거움 0.5초 제한) RaiseCritShown을 한 번 낸다. crit은 치명이었는가, 단계는 waveShown으로 읽는다.
        /// </summary>
        public bool HitWithWave(Enemy enemy, Vector2 direction, ref CritTier waveShown, out bool crit, out bool killed)
        {
            crit = RollCrit();
            killed = false;
            float percent = WavePercentNow;
            var critTier = CritTiers.Of(percent, CritDamage);
            int damage = DamageMath.ToMonster(Attack, percent, crit, CritDamage, DamageMath.Roll(_rng), 0, SkillDamageBonus, true, BossDamageBonus, enemy.IsBoss);
            if (crit) WorldOverlay.SetNextCritTier(critTier);
            int applied = enemy.TakeHit(damage, crit, DamageSource.SwordWave, WavePoise, false, CounterMultiplier, out _, out _);
            if (crit) WorldOverlay.ClearNextCritTier();
            if (applied <= 0) return false;
            _counterUntil = -999f;
            StealLife(applied);
            var shown = crit ? DecideCrit(ref waveShown, critTier, false) : CritTier.None;
            killed = enemy.Dead;
            if (killed && enemy.IsV3 && enemy.Weight == EnemyWeight.Heavy) TimeScaleService.HitStop(0.12f);
            enemy.ApplyKnockback(direction, SwordWave.Knockback);
            HitEffects.OnHit(enemy, direction, shown, true);
            if (killed) OnKills(1);
            return true;
        }
    }
}
