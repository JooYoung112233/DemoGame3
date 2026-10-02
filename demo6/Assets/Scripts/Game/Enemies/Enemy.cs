using System;
using System.Collections;
using System.Collections.Generic;
using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>3차 초안 3-4 무게. 가벼움은 모든 타격에 끊기고, 보통은 마무리·스킬·무너짐에만, 무거움은 무너짐에만 끊긴다.</summary>
    public enum EnemyWeight
    {
        Light,
        Medium,
        Heavy,
    }

    [Flags]
    public enum EliteAffix
    {
        None = 0,
        /// <summary>단단한: 버팀 ×2, 무너짐 +1.0초, 무너짐 중 받는 피해 +60%.</summary>
        Hardened = 1,
        /// <summary>무리 거느린: 굴쥐 4와 함께, 12초마다 2마리 더(최대 6, 보상 없음).</summary>
        Pack = 2,
    }

    /// <summary>
    /// 몬스터 공통: 체력, 넉백(0.1초 밀림 + 경직 0.15초), 피격 번쩍임, 처치 연출, 목록 등록.
    /// 3차 값(Tuning.Ruleset = V3)에서는 버팀·무너짐, 잠·기습, 정예, 회복 구슬을 더한다.
    /// 종류별 행동은 Think에서 정한다(enum 상태 + switch, 기획 11-6).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class Enemy : MonoBehaviour
    {
        public static readonly List<Enemy> All = new List<Enemy>();

        const float KnockDuration = 0.1f;
        const float StaggerDuration = 0.15f;
        const float BreakDuration = 2.0f;
        const float GroupWakeDelay = 0.8f;
        /// <summary>
        /// 잠든 적이 플레이어를 감지하고 깨어나기까지('!'). 등 뒤 감지 3이 장검 사거리보다 길어,
        /// 이 틈이 없으면 가장 가까운 적에게는 근접 기습이 불가능하다(M0b 시험값).
        /// </summary>
        public const float NoticeDelay = 0.5f;
        const float SeeFront = 6f;
        const float SeeBehind = 3f;
        const float EliteScale = 1.35f;
        /// <summary>3차 초안 2-6 놓아주기: 플레이어가 칸 경계 밖에 4초 있으면 제자리로 돌아간다.</summary>
        public const float LeashDelay = 4f;
        /// <summary>3차 초안 2-6: 돌아가는 동안 버팀은 가득, 체력은 초당 최대치의 5%를 되찾는다.</summary>
        const float ReturnHealRate = 0.05f;
        /// <summary>기둥에 걸려 제자리에 못 가면 이 시간 뒤 제자리로 옮긴다(영원히 깨어 있는 적이 남지 않게).</summary>
        const float ReturnGiveUp = 12f;
        const float HomeArrive = 0.25f;
        const float GuardArrive = 0.3f;
        /// <summary>큰 소리에 깨어 입구에서 기다리다 플레이어가 끝내 오지 않으면 제자리로 돌아가는 시간.</summary>
        const float GuardGiveUp = 20f;
        /// <summary>곧장 못 가면 이 각도들로 틀어 본다(기둥 비켜 가기). 바뀌지 않는 값이라 플레이 시작 때 비울 필요가 없다.</summary>
        static readonly float[] SteerAngles = { 35f, -35f, 70f, -70f, 105f, -105f };

        public MonsterKind Kind { get; private set; }
        public int Floor { get; private set; }
        public bool IsDummy { get; protected set; }
        public float Radius { get; private set; }
        public int AttackPower { get; private set; }
        public float MoveSpeed { get; private set; }
        public Health Health { get; private set; }
        public Vector2 Position => _body ? _body.position : (Vector2)transform.position;
        public bool Dead => Health == null || Health.Dead;
        public event Action<Enemy> Removed;

        /// <summary>스폰할 때의 규칙(M0a / 3차). 도중에 바꾸면 다시 세운다.</summary>
        public bool IsV3 { get; private set; }

        /// <summary>허수아비: 규칙과 상관없이 버팀·무너짐·무게 반응 없이 맞기만 한다(초당 피해 측정이 규칙에 흔들리지 않게).</summary>
        protected void UseMeasurementRules()
        {
            IsV3 = false;
            Poise = null;
            Health.DamageTakenMultiplier = 1f;
        }
        public EnemyWeight Weight { get; protected set; }
        public PoiseMeter Poise { get; private set; }
        public bool Broken => _breakUntil > Time.time;
        public float BreakRemaining => Mathf.Max(0f, _breakUntil - Time.time);
        public bool Aware { get; private set; } = true;
        public int GroupId { get; set; } = -1;
        public bool IsElite { get; private set; }
        public EliteAffix Affixes { get; private set; }
        /// <summary>둥지·무리 거느린 정예가 부른 굴쥐: 회복 구슬 등 보상 없음.</summary>
        public bool NoReward { get; set; }
        /// <summary>시험 패널·체력바용: 그림이 없을 때 도형 지름.</summary>
        public float ShapeDiameter { get; private set; }
        /// <summary>몸 흔들림(무거운 적 피격, 멧돼지 앞발 긁기). 그림·도형 모두 이 값을 더해 그린다.</summary>
        public Vector2 VisualJitter { get; private set; }
        /// <summary>던전 시야(VisionSystem): 지금 그리지 않는가(시야 밖이거나 빛 밖). 체력바·'z z'도 숨긴다.</summary>
        public bool VisionHidden { get; set; }
        /// <summary>던전 시야: 시야 다각형 안인가(빛과 관계없이). 빛 밖이면 눈 두 점만 보인다.</summary>
        public bool VisionInSight { get; set; } = true;

        /// <summary>
        /// 3차 초안 2-6 칸에 묶인 무리: 감지·추격·돌아가기 경계(칸 안쪽 + 문 1유닛).
        /// 없으면(전투 시험장, 궤짝·도시락통 굴쥐) M0a처럼 경계 없이 플레이어를 쫓는다. 아래 칸 규칙은 모두 이 값이 있을 때만 돈다.
        /// </summary>
        public Rect? Territory { get; private set; }
        public bool HasTerritory => Territory.HasValue;
        /// <summary>처음 놓인 자리. 놓아주기·쓰러져 다시 섬 때 여기로 돌아가 잔다.</summary>
        public Vector2 HomePosition { get; private set; }
        public Vector2 HomeFacing { get; private set; } = Vector2.down;
        /// <summary>플레이어를 놓아주고 제자리로 걸어가는 중(버팀 가득, 체력 초당 5%).</summary>
        public bool IsReturning => _returning;
        /// <summary>큰 소리에 깨어 칸 입구에서 기다리는 중(플레이어가 들어오면 싸운다).</summary>
        public bool IsGuarding => _guarding;
        /// <summary>먹는 중(3차 초안 2-6 '먹는 중'). 잠과 같은 감지 규칙이고 겉모습만 다르다.</summary>
        public bool IsEating { get; private set; }
        /// <summary>쉬는 중이지만 곧 깬다('!' 알아채기 0.5초 또는 무리 반응 0.8초를 기다리는 중).</summary>
        public bool WakePending => !Aware && _wakeAt > 0f;
        /// <summary>제자리에 닿으면 자지 않고 사라진다(둥지가 부른 굴쥐는 굴로 돌아간다).</summary>
        public bool DespawnAtHome { get; set; }

        protected Rigidbody2D Body => _body;
        protected SpriteRenderer Sprite { get; private set; }
        protected SpriteFlash Flash { get; private set; }
        protected Vector2 DesiredVelocity;
        protected Vector2 Facing = Vector2.down;
        protected float BaseKnockbackResist;
        protected bool Busy => _knockTime > 0f || _staggerTime > 0f;
        /// <summary>정예는 큰 공격 예고 +0.1초.</summary>
        protected float TelegraphBonus => IsElite ? 0.1f : 0f;
        /// <summary>브레인이 앞발 긁기 같은 떨림을 직접 넣는 값.</summary>
        protected Vector2 ExtraJitter;
        protected static PlayerController Player => PlayerController.Instance;

        Rigidbody2D _body;
        CircleCollider2D _collider;
        EnemyVisual _visual;
        SpriteRenderer _eliteRing;
        EnemyPose _pose = EnemyPose.Locomotion;
        float _poseStart;
        float _poseDuration;
        float _poseHitTime = -1f;
        float _hitStart;
        float _deathTime;
        float _knockTime;
        float _staggerTime;
        Vector2 _knockVelocity;
        Vector2 _deathPush;
        bool _removed;
        float _breakUntil;
        bool _wasBroken;
        float _breakTaken = 1.3f;
        float _breakLength = BreakDuration;
        float _wakeAt = -1f;
        bool _noticing;
        float _shakeUntil;
        float _shakeAmp;
        float _sizeScale = 1f;
        bool _rotateVisual;
        bool _returning;
        float _returnTime;
        float _healCarry;
        float _guardTime;
        float _lastEvadeText = -999f;
        float _outsideTime;
        bool _wasOutsideArea;
        bool _guarding;
        Vector2 _guardPoint;
        Vector2 _guardFacing = Vector2.down;
        /// <summary>쉬는 모습이 '먹는 중'인가(돌아가 다시 쉴 때 되살린다).</summary>
        bool _restEating;

        public static void ResetStatics() => All.Clear();

        /// <summary>그림 고르기용 자세. 죽음, 무너짐, 잠, 넉백·경직(준비·공격 중 제외)이 브레인이 정한 자세보다 앞선다.</summary>
        public EnemyPose CurrentPose
        {
            get
            {
                if (Dead) return EnemyPose.Dead;
                if (Broken) return EnemyPose.Hit;
                if (!Aware) return EnemyPose.Idle;
                if (Busy && _pose != EnemyPose.Windup && _pose != EnemyPose.Attack) return EnemyPose.Hit;
                return _pose;
            }
        }

        public float PoseTime
        {
            get
            {
                var pose = CurrentPose;
                float start = pose == EnemyPose.Dead ? _deathTime : pose == EnemyPose.Hit && _pose != EnemyPose.Hit ? _hitStart : _poseStart;
                return Time.time - start;
            }
        }

        public float PoseDuration => _poseDuration;
        public float PoseHitTime => _poseHitTime;
        public Vector2 FacingDirection => Facing;
        public bool IsMoving => DesiredVelocity.sqrMagnitude > 0.01f;
        /// <summary>그림 크기 배율(정예 1.35).</summary>
        public float SizeScale => _sizeScale;

        /// <summary>브레인이 매 프레임 지금 자세를 알린다. 같은 자세면 시작 시각을 유지한다.</summary>
        protected void SetPose(EnemyPose pose, float duration = 0f, float hitTime = -1f)
        {
            if (pose != _pose)
            {
                _pose = pose;
                _poseStart = Time.time;
            }
            _poseDuration = duration;
            _poseHitTime = hitTime;
        }

        public static T Spawn<T>(MonsterKind kind, int floor, Vector2 position, Sprite shape, Color color, bool rotateWithFacing)
            where T : Enemy
        {
            bool v3 = Tuning.Ruleset == CombatRuleset.V3;
            var rule = MonsterRule.Of(kind, Tuning.Ruleset, Tuning.SoftBoar);
            var go = new GameObject(rule.DisplayName);
            go.layer = Layers.Enemy;
            go.transform.position = position;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.mass = kind == MonsterKind.Boar ? 4f : 1f;
            if (kind == MonsterKind.Nest) body.bodyType = RigidbodyType2D.Kinematic;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = rule.Diameter * 0.5f;

            var visual = new GameObject("Body");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = Vector3.one * rule.Diameter;
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = shape;
            sr.color = color;

            var flash = go.AddComponent<SpriteFlash>();
            flash.target = sr;
            flash.baseColor = color;

            var health = go.AddComponent<Health>();
            health.Init(FloorScaling.MonsterHp(rule, floor), 0);

            go.AddComponent<YSort>();
            var visualComp = go.AddComponent<EnemyVisual>();

            var enemy = go.AddComponent<T>();
            enemy._body = body;
            enemy._collider = col;
            enemy.Sprite = sr;
            enemy.Flash = flash;
            enemy.Health = health;
            enemy.Kind = kind;
            enemy.Floor = floor;
            enemy.IsV3 = v3;
            enemy.Radius = rule.Diameter * 0.5f;
            enemy.ShapeDiameter = rule.Diameter;
            enemy.AttackPower = FloorScaling.MonsterAttack(rule, floor);
            enemy.MoveSpeed = rule.MoveSpeed;
            enemy.BaseKnockbackResist = rule.KnockbackResist;
            enemy.Weight = kind == MonsterKind.Rat ? EnemyWeight.Light : kind == MonsterKind.Archer ? EnemyWeight.Medium : EnemyWeight.Heavy;
            double poise = MonsterRule.PoiseOf(kind, Tuning.SoftBoar);
            if (v3 && poise > 0) enemy.Poise = new PoiseMeter(poise);
            enemy._rotateVisual = rotateWithFacing;
            enemy._visual = visualComp;
            visualComp.Bind(enemy, sr, flash);
            health.Died += enemy.OnDied;
            All.Add(enemy);
            enemy.OnSpawned();
            return enemy;
        }

        /// <summary>
        /// 정예(3차 초안 3-4): 체력 ×3, 공격 ×1.2, 크기 ×1.35, 버팀 ×1.5, 넉백 저항 +50%, 큰 공격 예고 +0.1초, 흰 테두리 맥동.
        /// </summary>
        public void MakeElite(EliteAffix affixes)
        {
            IsElite = true;
            Affixes = affixes;
            Weight = EnemyWeight.Heavy;
            Health.Init(Health.Max * 3, Health.Defense);
            AttackPower = Mathf.RoundToInt(AttackPower * 1.2f);
            BaseKnockbackResist = Mathf.Min(1f, BaseKnockbackResist + 0.5f);
            _sizeScale = EliteScale;
            Radius *= EliteScale;
            ShapeDiameter *= EliteScale;
            _collider.radius *= EliteScale;
            Sprite.transform.localScale *= EliteScale;
            double poise = MonsterRule.PoiseOf(Kind, Tuning.SoftBoar) * 1.5;
            if ((affixes & EliteAffix.Hardened) != 0)
            {
                poise *= 2;
                _breakLength = BreakDuration + 1f;
                _breakTaken = 1.6f;
            }
            Poise = new PoiseMeter(Math.Max(1, poise));
            name = "정예 " + name;

            var ring = new GameObject("EliteRing");
            ring.transform.SetParent(transform, false);
            ring.transform.localScale = Vector3.one * (ShapeDiameter * 1.18f);
            _eliteRing = ring.AddComponent<SpriteRenderer>();
            // 3차 초안 2-4: 정예 테두리는 빛 밖에서도 보인다.
            RenderMaterials.MakeUnlit(_eliteRing);
            _eliteRing.sprite = ShapeSprites.Ring;
            _eliteRing.color = Color.white;
            _eliteRing.sortingOrder = 1;
            GetComponent<YSort>()?.Refresh();
        }

        /// <summary>잠든 채로 둔다(기습 연습). 깨면 같은 무리가 0.8초 뒤 따라 깬다.</summary>
        public void Sleep(Vector2 facing)
        {
            _restEating = false;
            Rest(facing);
        }

        /// <summary>먹는 채로 둔다(3차 초안 2-6 '먹는 중'). 감지·기습은 잠과 같다.</summary>
        public void Eat(Vector2 facing)
        {
            _restEating = true;
            Rest(facing);
        }

        /// <summary>쉬는 상태(잠·먹는 중)로 들어간다. 돌아가기·입구 지키기·놓아주기 시간도 비운다.</summary>
        void Rest(Vector2 facing)
        {
            Aware = false;
            IsEating = _restEating;
            _wakeAt = -1f;
            _noticing = false;
            _returning = false;
            _guarding = false;
            _outsideTime = 0f;
            _wasOutsideArea = false;
            DesiredVelocity = Vector2.zero;
            if (facing.sqrMagnitude > 0.0001f) FaceTowards(facing);
        }

        public void Wake(bool alertGroup)
        {
            // 한 방에 쓰러져도(기습 처치) 싸움 소리는 무리를 깨운다.
            if (Aware) return;
            Aware = true;
            IsEating = false;
            _wakeAt = -1f;
            _noticing = false;
            _outsideTime = 0f;
            if (!Dead) CombatEvents.RaiseWoke(this);
            if (alertGroup) AlertGroup();
        }

        /// <summary>같은 무리의 쉬는 적을 0.8초 뒤 깨운다(무리 반응 늦음, 3차 초안 2-6).</summary>
        void AlertGroup()
        {
            if (GroupId < 0) return;
            foreach (var other in All)
                if (other && other != this && other.GroupId == GroupId && !other.Aware && other._wakeAt < 0f)
                    other._wakeAt = Time.time + GroupWakeDelay;
        }

        /// <summary>
        /// 칸에 묶는다(3차 초안 2-6). 쉬는 동안은 경계 안의 플레이어만 알아채고, 깨어서는 경계 밖으로 나가지 않으며,
        /// 플레이어가 경계 밖에 4초 있으면 제자리로 걸어가 다시 쉰다.
        /// </summary>
        public void BindToTerritory(Rect territory, Vector2 home, Vector2 homeFacing)
        {
            Territory = territory;
            HomePosition = home;
            if (homeFacing.sqrMagnitude > 0.0001f) HomeFacing = homeFacing.normalized;
        }

        /// <summary>
        /// 큰 소리에 깨어 칸 입구(소리 쪽 문 안쪽)에서 기다린다(3차 초안 2-6). 플레이어가 경계 밖에 있는 동안은 놓아주기 4초를 세지 않고,
        /// 들어오면 바로 싸운다. 칸이 없는 적은 무시한다.
        /// </summary>
        public void GuardAt(Vector2 point, Vector2 faceOut)
        {
            if (Dead || !Territory.HasValue) return;
            _guarding = true;
            _guardTime = 0f;
            _guardPoint = point;
            if (faceOut.sqrMagnitude > 0.0001f) _guardFacing = faceOut.normalized;
            _returning = false;
            _outsideTime = 0f;
        }

        /// <summary>
        /// 쓰러져 다시 섬(3차 초안 2-7 M0b판): 하던 공격을 거두고 제자리로 옮겨 체력·버팀을 채운 뒤 다시 쉰다(먹던 무리는 다시 먹는다).
        /// 칸이 없는 적과 쓰러진 적은 그대로 둔다.
        /// </summary>
        public void ResetToHome()
        {
            if (Dead || !Territory.HasValue) return;
            ResetBehaviour();
            _knockTime = 0f;
            _staggerTime = 0f;
            _breakUntil = 0f;
            _wasBroken = false;
            _shakeUntil = 0f;
            ExtraJitter = Vector2.zero;
            Health.DamageTakenMultiplier = 1f;
            Health.Heal(Health.Max);
            Poise?.Refill();
            PlaceAt(HomePosition);
            Rest(HomeFacing);
        }

        /// <summary>
        /// 놓아주기·칸 밖에서 돌아오기·다시 섬 때 부른다: 준비 동작·예고를 거두고 공격 기회를 돌려준 뒤 기본 상태(걷기·쫓기)로.
        /// 기본은 끊기 처리(OnInterrupted)와 같다. 상태가 남는 브레인(멧돼지 돌진 등)은 덮어써서 처음 상태로 되돌린다.
        /// </summary>
        protected virtual void ResetBehaviour() => OnInterrupted();

        protected virtual void OnSpawned() { }

        protected abstract void Think(float dt);

        /// <summary>넉백이 들어왔을 때 준비 동작을 끊을 수 있는가. 가벼운 적만 끊긴다(3차). 멧돼지는 늘 안 끊긴다.</summary>
        protected virtual bool Interruptible => true;

        protected virtual float CurrentKnockbackResist => BaseKnockbackResist;

        /// <summary>
        /// 넉백·경직 중에도 Think를 부를까. 준비 동작이 끊기지 않는 적(멧돼지)은 예고 시간이 기획대로 흘러야 한다.
        /// 이동은 FixedUpdate가 넉백·경직 속도로 덮어쓴다.
        /// </summary>
        protected virtual bool ThinkWhileBusy => false;

        /// <summary>껍질처럼 지금 피해를 받지 않는가(둥지).</summary>
        protected virtual bool CanBeDamaged => true;

        /// <summary>넉백·경직을 바로 끝낸다(돌진 시작처럼 몸이 움직여야 하는 순간).</summary>
        protected void ClearStagger()
        {
            _knockTime = 0f;
            _staggerTime = 0f;
        }

        /// <summary>준비 동작 취소, 공격 기회 반납.</summary>
        protected virtual void OnInterrupted() { }

        /// <summary>무너질 때. 기본은 준비 동작 취소.</summary>
        protected virtual void OnBroken() => OnInterrupted();

        protected virtual void OnRemoved() { }

        /// <summary>껍질에 막혔을 때(둥지).</summary>
        protected virtual void OnBlocked() { }

        /// <returns>실제로 들어간 피해.</returns>
        public int TakeHit(int amount, bool crit, DamageSource source) =>
            TakeHit(amount, crit, source, 0f, false, 1f, out _, out _);

        /// <param name="poiseDamage">버팀 피해(치명이면 ×1.5). 잠든 적은 기습 배율.</param>
        /// <param name="finisher">콤보 마무리. 보통 무게는 마무리·스킬에만 준비가 끊기고, 무거운 적은 움찔한다.</param>
        /// <param name="poiseMultiplier">회피 반격 ×2 등.</param>
        /// <param name="broke">이 타격으로 무너졌는가.</param>
        /// <param name="wasBroken">맞기 전에 이미 무너져 있었는가.</param>
        public int TakeHit(int amount, bool crit, DamageSource source, float poiseDamage, bool finisher, float poiseMultiplier, out bool broke, out bool wasBroken)
        {
            broke = false;
            wasBroken = Broken;
            if (Dead) return 0;
            if (!CanBeDamaged)
            {
                OnBlocked();
                if (!Aware) Wake(true);
                return 0;
            }
            // 3차 초안 2-6 '치고 빠지기 꼼수는 막음': 깨어 있는 칸 무리는 경계 밖(문 너머)에서 오는 공격을 피하고 제자리로 돌아간다.
            // 쉬는 무리를 밖에서 먼저 치는 기습은 그대로 들어간다. 싸우려면 칸 안으로 들어와야 한다.
            if (Aware && Territory.HasValue && Player && !Territory.Value.Contains(Player.Position))
            {
                if (Time.unscaledTime - _lastEvadeText > 0.6f)
                {
                    _lastEvadeText = Time.unscaledTime;
                    if (VisionInSight) WorldOverlay.Text(Position + Vector2.up * (Radius + 0.4f), "회피", Palette.HealthBar);
                }
                if (!_returning)
                {
                    _guarding = false;
                    BeginReturn();
                }
                return 0;
            }
            bool ambush = !Aware;
            int applied = Health.ApplyDamage(amount, crit);
            if (applied <= 0) return 0;
            if (!Dead) Flash.Flash(Color.white, 0.06f);
            WorldOverlay.Number(Position + Vector2.up * Radius, applied, crit ? NumberKind.Crit : NumberKind.Normal);
            CombatEvents.RaiseDealt(new DamageDealt(this, applied, source, crit, Dead));

            if (!Dead && IsV3)
            {
                bool melee = source == DamageSource.Basic;
                bool skill = source == DamageSource.Whirlwind || source == DamageSource.SwordWave;
                if (Poise != null && !wasBroken && poiseDamage > 0f)
                {
                    double p = poiseDamage * (crit ? 1.5 : 1.0) * poiseMultiplier;
                    if (ambush) p = PoiseMeter.AmbushDamage(poiseDamage * (crit ? 1.5 : 1.0) * poiseMultiplier, melee, Poise.Max);
                    if (Poise.Apply(p, Time.time))
                    {
                        Break();
                        broke = true;
                    }
                }
                if (!broke && Weight == EnemyWeight.Heavy)
                {
                    // 무거운 적: 마무리·검풍에는 0.15초 움찔, 일반 타격은 몸만 흔들림(준비는 안 끊김).
                    bool flinch = finisher || source == DamageSource.SwordWave;
                    Shake(flinch ? 0.15f : 0.08f, flinch ? 0.09f : 0.05f);
                }
                if (!broke && Weight == EnemyWeight.Medium && (finisher || skill)) OnInterrupted();
            }
            if (ambush) Wake(true);
            return applied;
        }

        void Break()
        {
            _breakUntil = Time.time + _breakLength;
            _wasBroken = true;
            Health.DamageTakenMultiplier = _breakTaken;
            DesiredVelocity = Vector2.zero;
            OnBroken();
            // 새 손맛 순간(3차 초안 3-4): 히트스톱 0.08초, 흔들림, 깨지는 소리.
            TimeScaleService.HitStop(0.08f);
            ScreenShake.Add(0.08f, 0.1f);
            Sfx.Play(SfxKind.Break);
            Flash.Flash(Color.white, 0.1f);
            // 던전 시야 밖의 적은 글자로 자리를 드러내지 않는다(전투 시험장에서는 늘 시야 안).
            if (VisionInSight) WorldOverlay.Text(Position + Vector2.up * (Radius + 0.6f), "무너짐!", Palette.NumberCrit);
            CombatEvents.RaiseBroken(this);
        }

        /// <summary>벽·기둥 박기처럼 버팀과 관계없이 바로 무너뜨린다(3차 멧돼지).</summary>
        protected void ForceBreak()
        {
            if (Broken || Dead) return;
            Poise?.Apply(Poise.Current, Time.time);
            Break();
        }

        void Shake(float seconds, float amplitude)
        {
            bool active = Time.time < _shakeUntil;
            _shakeAmp = active ? Mathf.Max(_shakeAmp, amplitude) : amplitude;
            _shakeUntil = Mathf.Max(_shakeUntil, Time.time + seconds);
        }

        public void ApplyKnockback(Vector2 direction, float distance)
        {
            if (distance <= 0f || direction.sqrMagnitude < 0.0001f) return;
            float raw = distance;
            if (Dead)
            {
                // 쓰러진 적은 처치 연출 동안 몸을 민다(물리는 이미 꺼짐). '처치 날림'을 켜면 맞은 방향으로 멀리 날아간다.
                // 돌진 중 저항·정예 +0.5는 살아 있을 때만: 쓰러지면 바탕 저항(최대 0.5)으로 난다. 둥지·허수아비는 날지 않는다.
                // 3차: 무거운 적은 처치 날림 ×1.5로 한 마리의 무게를 갚아 준다.
                float deathResist = Kind == MonsterKind.Nest || IsDummy ? 1f : Mathf.Min(BaseKnockbackResist, 0.5f);
                if (deathResist >= 1f) return;
                Vector2 dir = direction.normalized;
                float heavy = IsV3 && Weight == EnemyWeight.Heavy ? 1.5f : 1f;
                float push = Tuning.KillFling
                    ? Mathf.Min(5f * heavy, (1.2f + 2.2f * raw) * (1f - deathResist) * Tuning.KillFlingScale * heavy)
                    : raw * (1f - deathResist) * Tuning.KnockbackScale;
                if (push <= 0.001f) return;
                var wall = Physics2D.CircleCast(Position, 0.2f, dir, push, Layers.WallMask);
                if (wall.collider) push = Mathf.Max(0f, wall.distance - 0.05f);
                _deathPush = dir * push;
                return;
            }
            float resist = CurrentKnockbackResist;
            if (resist >= 1f) return;
            distance *= (1f - resist) * Tuning.KnockbackScale;
            if (distance <= 0.001f) return;
            _knockVelocity = direction.normalized * (distance / KnockDuration);
            if (!Busy) _hitStart = Time.time;
            _knockTime = KnockDuration;
            _staggerTime = 0f;
            if (Interruptible) OnInterrupted();
        }

        protected void FaceTowards(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            Facing = dir.normalized;
            // 그림은 좌우로만 뒤집으므로 몸을 돌리지 않는다.
            if (_rotateVisual && !(_visual && _visual.ArtShown)) Sprite.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(Facing.y, Facing.x) * Mathf.Rad2Deg);
        }

        protected void SetLayer(int layer) => gameObject.layer = layer;

        void Update()
        {
            if (Dead) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdateJitter();
            if (_eliteRing)
            {
                var c = _eliteRing.color;
                c.a = 0.55f + 0.45f * Mathf.Sin(Time.time * Mathf.PI * 2f / 0.8f);
                _eliteRing.color = c;
            }

            if (!Aware)
            {
                DesiredVelocity = Vector2.zero;
                if (_wakeAt > 0f && Time.time >= _wakeAt) Wake(_noticing);
                // 칸에 묶인 적은 경계(칸 안쪽 + 문 1유닛) 안의 플레이어만 알아챈다(3차 초안 2-6).
                else if (_wakeAt < 0f && Player && !Player.IsDown && PlayerInTerritory(Player) && DetectsPlayer(Player))
                {
                    // 알아챘다: 0.5초 뒤 깨어나 무리를 깨운다. 그 전에 먼저 치면 기습이다.
                    _noticing = true;
                    _wakeAt = Time.time + NoticeDelay;
                    if (VisionInSight) WorldOverlay.Text(Position + Vector2.up * (Radius + 0.5f), "!", Palette.NumberCrit);
                }
                return;
            }

            if (Broken)
            {
                DesiredVelocity = Vector2.zero;
                return;
            }
            if (_wasBroken)
            {
                // 무너짐에서 일어남: 최대 버팀 +25%, 받는 피해 원래대로.
                _wasBroken = false;
                Health.DamageTakenMultiplier = 1f;
                Poise?.Recover();
            }
            Poise?.Tick(Time.time, dt);

            bool busy = _knockTime > 0f || _staggerTime > 0f;
            if (_knockTime <= 0f && _staggerTime > 0f) _staggerTime -= dt;
            // 칸 규칙(돌아가기·입구 지키기·칸 밖에서 돌아오기)이 이번 프레임을 맡으면 브레인은 쉰다. 칸이 없으면 늘 false.
            if (UpdateTerritory(dt, busy)) return;
            if (busy)
            {
                if (ThinkWhileBusy)
                {
                    Think(dt);
                    KeepInsideTerritory();
                }
                else DesiredVelocity = Vector2.zero;
                return;
            }
            Think(dt);
            KeepInsideTerritory();
        }

        bool PlayerInTerritory(PlayerController p) => !Territory.HasValue || Territory.Value.Contains(p.Position);

        /// <summary>
        /// 3차 초안 2-6 칸 규칙. 플레이어가 경계 밖에 4초 있으면 하던 공격을 거두고 제자리로 걸어간다(버팀 가득, 체력 초당 5%).
        /// 돌아가는 중·입구에서 기다리는 중 플레이어가 다시 들어오면 그대로 싸운다. 돌진·넉백으로 경계 밖에 나가면 안으로 돌아온다.
        /// </summary>
        /// <returns>이번 프레임 움직임을 칸 규칙이 정했는가(브레인 Think를 건너뜀).</returns>
        bool UpdateTerritory(float dt, bool busy)
        {
            if (!Territory.HasValue) return false;
            Rect area = Territory.Value;
            var p = Player;
            bool playerIn = p && !p.IsDown && area.Contains(p.Position);

            if (playerIn)
            {
                _outsideTime = 0f;
                if (_returning || _guarding)
                {
                    _returning = false;
                    _guarding = false;
                    // 먼저 돌아가 잠든 동료도 다시 깨운다(0.8초 늦게).
                    AlertGroup();
                }
            }
            else if (_guarding)
            {
                _guardTime += dt;
                if (_guardTime >= GuardGiveUp)
                {
                    _guarding = false;
                    BeginReturn();
                }
                else
                {
                    if (!busy) GuardStep();
                    return true;
                }
            }
            else if (!_returning)
            {
                _outsideTime += dt;
                if (_outsideTime >= LeashDelay) BeginReturn();
            }

            if (_returning)
            {
                ReturnStep(dt, busy);
                return true;
            }

            if (!area.Contains(Position))
            {
                // 돌진·넉백으로 경계 밖에 나감: 하던 공격을 거두고 제자리 쪽으로 걸어 들어온다.
                if (!_wasOutsideArea)
                {
                    _wasOutsideArea = true;
                    ResetBehaviour();
                }
                if (!busy)
                {
                    Vector2 dir = SteerTo(HomePosition);
                    DesiredVelocity = dir * MoveSpeed;
                    FaceTowards(dir);
                    SetPose(EnemyPose.Locomotion);
                }
                return true;
            }
            _wasOutsideArea = false;
            return false;
        }

        /// <summary>깨어 있는 동안 경계 밖으로 나가는 속도 성분을 지운다(칸 사이 추격은 판정 뒤 후보, 3차 초안 2-6).</summary>
        void KeepInsideTerritory()
        {
            if (!Territory.HasValue) return;
            Rect area = Territory.Value;
            Vector2 pos = Position;
            Vector2 v = DesiredVelocity;
            float m = Radius;
            if (pos.x <= area.xMin + m && v.x < 0f) v.x = 0f;
            if (pos.x >= area.xMax - m && v.x > 0f) v.x = 0f;
            if (pos.y <= area.yMin + m && v.y < 0f) v.y = 0f;
            if (pos.y >= area.yMax - m && v.y > 0f) v.y = 0f;
            DesiredVelocity = v;
        }

        void BeginReturn()
        {
            _returning = true;
            _returnTime = 0f;
            _healCarry = 0f;
            ResetBehaviour();
            Poise?.Refill();
        }

        void ReturnStep(float dt, bool busy)
        {
            _returnTime += dt;
            Poise?.Refill();
            _healCarry += Health.Max * ReturnHealRate * dt;
            int heal = Mathf.FloorToInt(_healCarry);
            if (heal > 0)
            {
                _healCarry -= heal;
                Health.Heal(heal);
            }
            if (busy) return;
            float dist = (HomePosition - Position).magnitude;
            if (dist <= HomeArrive || _returnTime >= ReturnGiveUp)
            {
                if (dist > HomeArrive) PlaceAt(HomePosition);
                ArriveHome();
                return;
            }
            Vector2 dir = SteerTo(HomePosition);
            DesiredVelocity = dir * MoveSpeed;
            FaceTowards(dir);
            SetPose(EnemyPose.Locomotion);
        }

        void ArriveHome()
        {
            DesiredVelocity = Vector2.zero;
            if (DespawnAtHome)
            {
                Remove();
                return;
            }
            // 놓아주기가 끝나면 싸움도 처음부터: 체력·버팀을 채운다(밖에서 한 대씩 치고 빠져 깎는 꼼수를 막음).
            Health.Heal(Health.Max);
            Poise?.Refill();
            Rest(HomeFacing);
        }

        void GuardStep()
        {
            Vector2 to = _guardPoint - Position;
            if (to.magnitude > GuardArrive && MoveSpeed > 0f)
            {
                Vector2 dir = SteerTo(_guardPoint);
                DesiredVelocity = dir * MoveSpeed;
                FaceTowards(dir);
                SetPose(EnemyPose.Locomotion);
                return;
            }
            DesiredVelocity = Vector2.zero;
            FaceTowards(_guardFacing);
            SetPose(EnemyPose.Idle);
        }

        /// <summary>목표 쪽 방향. 곧장 가면 벽·기둥에 막히면 조금씩 틀어 비켜 간다(길찾기 없음).</summary>
        Vector2 SteerTo(Vector2 target)
        {
            Vector2 to = target - Position;
            float d = to.magnitude;
            if (d < 0.0001f) return Vector2.zero;
            Vector2 dir = to / d;
            float probe = Mathf.Min(d, 0.8f);
            float r = Radius * 0.8f;
            if (!Physics2D.CircleCast(Position, r, dir, probe, Layers.WallMask)) return dir;
            foreach (float a in SteerAngles)
            {
                Vector2 alt = Turned(dir, a);
                if (!Physics2D.CircleCast(Position, r, alt, probe, Layers.WallMask)) return alt;
            }
            return dir;
        }

        static Vector2 Turned(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>몸을 바로 옮긴다(다시 섬·돌아가기 포기).</summary>
        void PlaceAt(Vector2 pos)
        {
            DesiredVelocity = Vector2.zero;
            if (_body)
            {
                _body.position = pos;
                _body.linearVelocity = Vector2.zero;
            }
            transform.position = pos;
        }

        void UpdateJitter()
        {
            Vector2 j = ExtraJitter;
            if (Time.time < _shakeUntil) j += UnityEngine.Random.insideUnitCircle * _shakeAmp;
            if (Broken) j += new Vector2(Mathf.Sin(Time.time * 40f) * 0.04f, 0f);
            VisualJitter = j;
        }

        void LateUpdate()
        {
            if (Dead || !Sprite) return;
            // 도형일 때만 여기서 흔든다. 그림은 EnemyVisual이 위치 보정에 더한다.
            if (!(_visual && _visual.ArtShown)) Sprite.transform.localPosition = VisualJitter;
        }

        /// <summary>잠든 적이 플레이어를 알아채는가. 기본: 앞 반경 6(±60°), 등 뒤 3, 벽 너머 못 봄(3차 초안 2-6).</summary>
        protected virtual bool DetectsPlayer(PlayerController p)
        {
            Vector2 to = p.Position - Position;
            float d = to.magnitude;
            if (d > SeeFront) return false;
            bool front = Vector2.Angle(Facing, to) <= 60f;
            if (!front && d > SeeBehind) return false;
            return HasLineOfSight(Position, p.Position);
        }

        void FixedUpdate()
        {
            if (Dead)
            {
                _body.linearVelocity = Vector2.zero;
                return;
            }
            if (_knockTime > 0f)
            {
                _body.linearVelocity = _knockVelocity;
                _knockTime -= Time.fixedDeltaTime;
                if (_knockTime <= 0f) _staggerTime = StaggerDuration;
                return;
            }
            _body.linearVelocity = _staggerTime > 0f || Broken || !Aware ? Vector2.zero : DesiredVelocity;
        }

        void OnDied()
        {
            _deathTime = Time.time;
            OnInterrupted();
            if (!IsDummy) CombatEvents.RaiseKilled(this);
            _collider.enabled = false;
            _body.linearVelocity = Vector2.zero;
            _body.simulated = false;
            if (_eliteRing) _eliteRing.enabled = false;
            DropOrbs();
            StartCoroutine(DeathRoutine());
        }

        /// <summary>3차 초안 3-6: 회복 구슬 궁수·멧돼지 15%, 정예 2개, 졸개(굴쥐) 0.</summary>
        void DropOrbs()
        {
            if (!IsV3 || IsDummy || NoReward) return;
            int count = IsElite ? 2 : (Kind == MonsterKind.Boar || Kind == MonsterKind.Archer) && UnityEngine.Random.value < 0.15f ? 1 : 0;
            for (int i = 0; i < count; i++) HealOrb.Spawn(Position + UnityEngine.Random.insideUnitCircle * 0.4f);
        }

        IEnumerator DeathRoutine()
        {
            if (Tuning.KillFling)
            {
                yield return FlingRoutine();
                Remove();
                yield break;
            }
            // 0.15초 흰색으로 번쩍인 뒤 줄어들며 흩어진다(기획 3-6).
            Flash.Flash(Color.white, 0.15f);
            Vector3 from = transform.position;
            float t = 0f;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                if (_deathPush != Vector2.zero)
                    transform.position = from + (Vector3)(_deathPush * Mathf.Clamp01(t / 0.1f));
                yield return null;
            }
            Vector3 start = Sprite.transform.localScale;
            t = 0f;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / 0.2f);
                Sprite.transform.localScale = start * k;
                var c = Sprite.color;
                c.a = k;
                Sprite.color = c;
                yield return null;
            }
            Remove();
        }

        /// <summary>처치 날림: 짧게 번쩍인 뒤 맞은 방향으로 굴러가듯 날아가며 사라진다(0.32초).</summary>
        IEnumerator FlingRoutine()
        {
            const float Duration = 0.32f;
            Flash.Flash(Color.white, 0.06f);
            Vector3 from = transform.position;
            float spin = (UnityEngine.Random.value < 0.5f ? -1f : 1f) * UnityEngine.Random.Range(540f, 900f);
            float angle = Sprite.transform.eulerAngles.z;
            Vector3 scale = Sprite.transform.localScale;
            float t = 0f;
            while (t < Duration)
            {
                float dt = Time.deltaTime;
                t += dt;
                float k = Mathf.Clamp01(t / Duration);
                float ease = 1f - (1f - k) * (1f - k);
                transform.position = from + (Vector3)(_deathPush * ease);
                angle += spin * dt * (1f - k);
                Sprite.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                Sprite.transform.localScale = scale * Mathf.Lerp(1f, 0.7f, k);
                var c = Sprite.color;
                c.a = k < 0.5f ? 1f : 1f - (k - 0.5f) / 0.5f;
                Sprite.color = c;
                yield return null;
            }
        }

        public void Remove()
        {
            if (_removed) return;
            _removed = true;
            OnInterrupted();
            OnRemoved();
            All.Remove(this);
            Removed?.Invoke(this);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            All.Remove(this);
            if (!_removed)
            {
                _removed = true;
                OnRemoved();
                Removed?.Invoke(this);
            }
        }

        /// <summary>같은 종류끼리 0.6유닛 안에서 밀어내는 힘(굴쥐가 퍼져서 둘러싸게).</summary>
        protected Vector2 Separation(float distance)
        {
            Vector2 push = Vector2.zero;
            Vector2 me = Position;
            foreach (var other in All)
            {
                if (other == this || other.Dead || other.Kind != Kind) continue;
                Vector2 d = me - other.Position;
                float m = d.magnitude;
                if (m <= 0.0001f || m >= distance) continue;
                push += d / m * ((distance - m) / distance);
            }
            return push;
        }

        protected static bool HasLineOfSight(Vector2 from, Vector2 to) =>
            !Physics2D.Linecast(from, to, Layers.WallMask);
    }

    /// <summary>
    /// 공격 기회 제한. 기획 4-1(M0a): 굴쥐 4, 그 밖 3. 3차 초안 2-6: 굴쥐 3, 그 밖 2.
    /// </summary>
    public static class AttackTokens
    {
        static int _rats;
        static int _heavy;

        public static int RatMax => Tuning.Ruleset == CombatRuleset.V3 ? 3 : 4;
        public static int HeavyMax => Tuning.Ruleset == CombatRuleset.V3 ? 2 : 3;
        public static int Rats => _rats;
        public static int Heavy => _heavy;

        public static bool TryAcquire(MonsterKind kind)
        {
            if (kind == MonsterKind.Rat)
            {
                if (_rats >= RatMax) return false;
                _rats++;
                return true;
            }
            if (_heavy >= HeavyMax) return false;
            _heavy++;
            return true;
        }

        public static void Release(MonsterKind kind)
        {
            if (kind == MonsterKind.Rat) _rats = Mathf.Max(0, _rats - 1);
            else _heavy = Mathf.Max(0, _heavy - 1);
        }

        public static void ResetStatics()
        {
            _rats = 0;
            _heavy = 0;
        }
    }

    /// <summary>
    /// 3차 초안 2-6: 강한 공격 두 개의 예고가 0.3초 안에 함께 끝나면 늦게 시작한 쪽을 0.3초 미룬다(동시에 읽을 예고는 2개).
    /// </summary>
    public static class StrongAttackSchedule
    {
        const float Gap = 0.3f;
        static readonly List<float> Ends = new List<float>();

        /// <returns>미룬 만큼 늘어난 예고 시간.</returns>
        public static float Reserve(float duration) => Reserve(duration, out _);

        /// <param name="reservedEnd">예약한 끝 시각(끊기면 Release로 돌려준다). 3차가 아니면 -1.</param>
        public static float Reserve(float duration, out float reservedEnd)
        {
            reservedEnd = -1f;
            if (Tuning.Ruleset != CombatRuleset.V3) return duration;
            float now = Time.time;
            Ends.RemoveAll(e => e < now - 0.05f);
            float end = now + duration;
            for (int guard = 0; guard < 8; guard++)
            {
                bool moved = false;
                foreach (var e in Ends)
                    if (Mathf.Abs(e - end) < Gap)
                    {
                        end = e + Gap;
                        moved = true;
                    }
                if (!moved) break;
            }
            Ends.Add(end);
            reservedEnd = end;
            return end - now;
        }

        /// <summary>끊긴 예고(무너짐·마무리 끊기·죽음)의 자리를 돌려준다.</summary>
        public static void Release(ref float reservedEnd)
        {
            if (reservedEnd >= 0f) Ends.Remove(reservedEnd);
            reservedEnd = -1f;
        }

        /// <summary>경직으로 멈췄던 예고가 실제로 끝날 시각으로 자리를 옮긴다(뒤에 예약하는 강한 공격이 이 시각과 0.3초 엇갈리게).</summary>
        public static void Move(ref float reservedEnd, float newEnd)
        {
            if (reservedEnd < 0f || Mathf.Abs(newEnd - reservedEnd) < 0.02f) return;
            Ends.Remove(reservedEnd);
            Ends.Add(newEnd);
            reservedEnd = newEnd;
        }

        public static void ResetStatics() => Ends.Clear();
    }
}
