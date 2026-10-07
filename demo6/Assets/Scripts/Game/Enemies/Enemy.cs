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
        /// <summary>벽 박기 판정이 이번 물리 단계 이동에 더해 보는 여유(벽에 붙어 있는 적도 잡음).</summary>
        const float WallSlamProbe = 0.05f;
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
        /// <summary>
        /// 정예 공격 배율. 3차 초안 3-4의 ×1.2를 ×1.18로 조금 낮췄다(2026-10-06, 1-2층 탐험 맛 1차 4-1): ×1.2면 2층 정예 돌충이 돌진 한 방이
        /// 그 층 기준 체력의 20.1%라 일반·정예 상한 20%를 넘었다(TelegraphRule). ×1.18이면 1~10층 모두 19.8% 이하다(2층 585 → 19.8%, 돌진 예고 0.7 + 정예 0.1 = 0.8초).
        /// </summary>
        const float EliteAttackScale = 1.18f;
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
        /// <summary>
        /// 지금 이동 속도(초당 유닛) = 종류 속도 × 느려짐 배율(ApplySlow) × 적 걸음 배율(Tuning.EnemyMoveScale, 전투·보스·무기 다듬기 1차 1-2 B = 0.93).
        /// 두뇌의 걷기·쫓기·물러나기·돌아가기가 모두 이 값을 써서 함께 느려진다
        /// (굴쥐 쫓기, 멧돼지 걷기, 궁수 거리 두기·물러나기). 멧돼지 돌진(초당 12)과 궁수 뒤로 뛰기(0.25초에 3.0), 오우거 돌진(11)은 예고 거리를 지키려고 이 값을 쓰지 않는다.
        /// </summary>
        public float MoveSpeed => _baseMoveSpeed * SlowFactor * Tuning.EnemyMoveScale;
        /// <summary>느려짐·적 걸음 배율을 뺀 종류 이동 속도.</summary>
        public float BaseMoveSpeed => _baseMoveSpeed;
        /// <summary>지금 이동 배율(1 = 보통, 0.7 = 불꽃 발자국 불 위). 느려짐이 끝났으면 1.</summary>
        public float SlowFactor => Time.time < _slowUntil ? _slowFactor : 1f;
        /// <summary>느려짐이 걸려 있는가(시험 패널·확인용).</summary>
        public bool Slowed => SlowFactor < 1f;
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
        /// <summary>무너짐 한 번의 길이(초, 단단한 정예는 더 길다). 대상 이름표의 남은 시간 막대 기준(읽기 전용).</summary>
        public float BreakLength => _breakLength;
        public bool Aware { get; private set; } = true;
        public int GroupId { get; set; } = -1;
        public bool IsElite { get; private set; }
        /// <summary>보스(갱도 오우거 등)인가. 보스 피해 능력치(장비 문서 2-2)가 이 적에게만 곱해진다. 지금은 보스가 없어 늘 false.</summary>
        public virtual bool IsBoss => false;
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
        /// <summary>
        /// 순찰 무리(1-2층 탐험 맛 1차 4-3, CellEncounters가 놓을 때 정함). 쉬는(잠) 동안 PatrolWalker가 천천히 걷게 하고 'z z'를 띄우지 않는다.
        /// 감지·기습은 잠과 같고 걷는 쪽이 앞이다. 깨어도 그대로 둔다(엿듣기 '오가는 발소리'가 무리 종류로 읽음).
        /// </summary>
        public bool IsPatrolling { get; set; }
        /// <summary>쉬는 중이지만 곧 깬다('!' 알아채기 0.5초 또는 무리 반응 0.8초를 기다리는 중).</summary>
        public bool WakePending => !Aware && _wakeAt > 0f;
        /// <summary>제자리에 닿으면 자지 않고 사라진다(둥지가 부른 굴쥐는 굴로 돌아간다).</summary>
        public bool DespawnAtHome { get; set; }
        /// <summary>
        /// 잔혹(기획/다크판타지-분위기-1차.md): 마지막 타격 직전 체력. GoreSystem이 '크게 넘치게 벤'(남은 체력의 2배 이상) 처치를 가린다.
        /// </summary>
        public int HpBeforeLastHit => _hpBeforeHit;
        /// <summary>잔혹: 크게 넘치게 베여 조각났다(GoreSystem). 처치 연출 동안 몸을 숨기고 시체를 남기지 않는다.</summary>
        public bool Dismembered { get; private set; }

        protected Rigidbody2D Body => _body;
        protected SpriteRenderer Sprite { get; private set; }
        protected SpriteFlash Flash { get; private set; }
        protected Vector2 DesiredVelocity;
        protected Vector2 Facing = Vector2.down;
        protected float BaseKnockbackResist;
        protected bool Busy => _knockTime > 0f || _staggerTime > 0f || Time.time < _holdStaggerUntil;
        /// <summary>정예는 큰 공격 예고 +0.1초.</summary>
        protected float TelegraphBonus => IsElite ? 0.1f : 0f;

        /// <summary>
        /// 3차 예고 규칙(TelegraphRule, 검토 1차 Q6): 바탕 예고(정예 +0.1초를 더해 넘김)와 '이 적의 공격력(층·정예 배율 포함) × percent% 한 방이
        /// 그 층 기준 플레이어 체력에서 차지하는 몫'의 최소 예고 가운데 긴 쪽. 보스(BossRules.Telegraph)와 같은 셈이다.
        /// 정예 바탕값에 최소 예고를 맞대는 까닭: 문서 3-3 표의 정예 멧돼지 돌진 0.8초(17.9%)가 바로 '바탕 0.7 + 정예 0.1'이 규칙을 채우는 값이다.
        /// M0a 값으로 세운 적(전투 시험장 비교 기준)은 바탕값 그대로.
        /// </summary>
        protected float RuleTelegraph(float baseSeconds, float percent) =>
            IsV3 ? TelegraphRule.Seconds(baseSeconds, AttackPower, percent, Floor) : baseSeconds;

        /// <summary>이 공격(percent%)이 그 층 기준 체력의 10%를 넘어 예고 시작 소리가 있어야 하는가(3차 규칙 '소리 필수'). M0a는 false.</summary>
        protected bool TelegraphNeedsCue(float percent) => IsV3 && TelegraphRule.NeedsSound(AttackPower, percent, Floor);
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
        /// <summary>쉬는 동안 걷는 속도(순찰, WalkWhileResting). 깨거나 곧 깨거나 다시 쉬면 0.</summary>
        Vector2 _restWalk;
        int _hpBeforeHit;
        float _baseMoveSpeed;
        float _slowFactor = 1f;
        float _slowUntil = -1f;
        /// <summary>창 끊어 찌르기가 이 적을 마지막으로 끊은 시각(같은 적 1.5초에 한 번).</summary>
        float _lastStaggerInterrupt = -999f;
        /// <summary>지금 넉백에 실린 정보와 저항·배율을 곱한 거리(벽 박기 판정 한 곳이 읽는다, 꾸러미 ⑧).</summary>
        KnockInfo _knockInfo;
        float _knockPush;
        /// <summary>이번 넉백에서 이미 벽 박기를 냈는가(넉백 한 번에 한 번).</summary>
        bool _knockSlammed;
        /// <summary>마지막 벽 박기 시각(같은 적은 WallSlamRule.Cooldown 0.5초에 한 번).</summary>
        float _lastWallSlam = -999f;
        /// <summary>
        /// 휘청(Stagger, 방패 패링 2-6)이 끝나는 시각. 이 시각 전에는 멈추고 생각도 멈춘다(넉백·경직 0.15초와 따로, ThinkWhileBusy 적도 멈춤).
        /// 한 번도 휘청하지 않은 적은 늘 지난 값이라 예전과 같다.
        /// </summary>
        float _holdStaggerUntil = -1f;
        /// <summary>휘청 시작 때 몸 흔들림(그림만, 판정과 무관).</summary>
        const float StaggerShakeSeconds = 0.25f;
        const float StaggerShakeAmplitude = 0.06f;

        public static void ResetStatics() => All.Clear();

        /// <summary>그림 고르기용 자세. 죽음, 무너짐, 잠, 넉백·경직(준비·공격 중 제외)이 브레인이 정한 자세보다 앞선다.</summary>
        public EnemyPose CurrentPose
        {
            get
            {
                if (Dead) return EnemyPose.Dead;
                if (Broken) return EnemyPose.Hit;
                if (!Aware) return EnemyPose.Idle;
                // 휘청(패링)은 끊긴 준비 자세에 멈춰 보이지 않게 늘 맞음 자세로 그린다.
                if (Time.time < _holdStaggerUntil) return EnemyPose.Hit;
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
            enemy._baseMoveSpeed = rule.MoveSpeed;
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
        /// 정예(3차 초안 3-4): 체력 ×3, 공격 ×1.18(EliteAttackScale, 처음 ×1.2), 크기 ×1.35, 버팀 ×1.5, 넉백 저항 +50%, 큰 공격 예고 +0.1초, 흰 테두리 맥동.
        /// </summary>
        public void MakeElite(EliteAffix affixes)
        {
            IsElite = true;
            Affixes = affixes;
            Weight = EnemyWeight.Heavy;
            Health.Init(Health.Max * 3, Health.Defense);
            AttackPower = Mathf.RoundToInt(AttackPower * EliteAttackScale);
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
            _restWalk = Vector2.zero;
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
            _restWalk = Vector2.zero;
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
            _holdStaggerUntil = -1f;
            _breakUntil = 0f;
            _wasBroken = false;
            _shakeUntil = 0f;
            ExtraJitter = Vector2.zero;
            Health.DamageTakenMultiplier = 1f;
            Health.Heal(Health.Max);
            Poise?.Refill();
            ClearSlow();
            PlaceAt(HomePosition);
            Rest(HomeFacing);
        }

        /// <summary>
        /// 이동을 느리게 한다(장비 문서 6장 불꽃 발자국 −30% = 배율 0.7). MoveSpeed가 이 배율을 곱한 값이 되어 두뇌가 그대로 느려진다.
        /// 겹치면 배율은 더 센 쪽, 끝 시각은 더 긴 쪽(LegendRules.MergeSlow). 쓰러진 적에는 걸지 않는다.
        /// </summary>
        /// <param name="factor">이동 배율(0~1).</param>
        /// <param name="seconds">지금부터 남는 시간(게임 시간).</param>
        public void ApplySlow(float factor, float seconds)
        {
            if (Dead || seconds <= 0f) return;
            float now = Time.time;
            LegendRules.MergeSlow(_slowFactor, _slowUntil, factor, now + seconds, now, out double f, out double until);
            _slowFactor = (float)f;
            _slowUntil = (float)until;
        }

        /// <summary>느려짐을 바로 푼다(다시 섬).</summary>
        public void ClearSlow()
        {
            _slowFactor = 1f;
            _slowUntil = -1f;
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
        public int TakeHit(int amount, bool crit, DamageSource source, float poiseDamage, bool finisher, float poiseMultiplier, out bool broke, out bool wasBroken) =>
            TakeHit(amount, crit, source, poiseDamage, finisher, poiseMultiplier, false, out broke, out wasBroken);

        /// <param name="followUp">이미 들어간 한 방의 뒷부분(처형, Execute). 칸 밖 공격 회피를 건너뛴다(TerritoryEvadeRule, 검토 1차 Q5).</param>
        int TakeHit(int amount, bool crit, DamageSource source, float poiseDamage, bool finisher, float poiseMultiplier, bool followUp, out bool broke, out bool wasBroken)
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
            // 출혈 틱(도끼)과 처형(기습 처형·무너짐 처형)은 이미 들어간 타의 뒤끝이라 피하지 않는다(문 밖으로 한 걸음 나가도 피해만 넣음, 귀환은 '경계 밖 4초' 규칙 몫).
            // 기습 처형은 첫 타가 적을 깨운 바로 뒤 같은 판정에서 이어지므로, 이 예외가 없으면 칸 밖 긴 무기 기습이 '회피'로 빠지고 무리를 깨웠다(검토 1차 Q5).
            bool attackerOutside = Territory.HasValue && Player && !Territory.Value.Contains(Player.Position);
            if (TerritoryEvadeRule.Evades(Aware, Territory.HasValue, attackerOutside, followUp || source == DamageSource.Bleed))
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
            _hpBeforeHit = Health.Current;
            int applied = Health.ApplyDamage(amount, crit);
            if (applied <= 0) return 0;
            if (!Dead) Flash.Flash(Color.white, 0.06f);
            // 시야와 문 1차 4-3: 직접 친 타(기본 공격·회오리·검풍)의 숫자는 늘, 출혈·전설·환경 피해 숫자는 보이는 적에게만(어둠 속 숫자가 자리를 알리지 않게).
            bool direct = source == DamageSource.Basic || source == DamageSource.Whirlwind || source == DamageSource.SwordWave;
            if (direct || VisionInSight) WorldOverlay.Number(Position + Vector2.up * Radius, applied, crit ? NumberKind.Crit : NumberKind.Normal);
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
                if (!broke && Weight == EnemyWeight.Heavy && source != DamageSource.Bleed)
                {
                    // 무거운 적: 마무리·검풍에는 0.15초 움찔, 일반 타격은 몸만 흔들림(준비는 안 끊김). 출혈 틱은 흔들지 않는다.
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

        // ── 전투·보스·무기 다듬기 1차 계약 훅(기획/전투-보스-무기-다듬기-1차.md). Enemy.cs는 꾸러미 ⑧이 소유하고, 다른 꾸러미는 아래 공개 훅만 쓴다 ──

        /// <summary>규칙 함수(WallSlamRule·FearRule·ExecutionRule)가 읽는 분류.</summary>
        public TargetClass Class => new TargetClass(Kind, (WeightClass)(int)Weight, IsElite, false, IsBoss, IsDummy);

        /// <summary>
        /// 버팀과 관계없이 바로 무너뜨린다(벽 박기 Break, 기습 처형한 멧돼지). 이미 무너졌거나 쓰러졌으면 false.
        /// 무너짐은 3차 규칙이라 M0a 값으로 세운 적과 허수아비(버팀·무너짐 없음)도 false다(멧돼지 자기 돌진 박기와 같은 규칙).
        /// 쓰는 곳: WallSlam(③), PlayerController 기습 처형(⑤).
        /// </summary>
        public bool BreakNow()
        {
            if (Dead || Broken || !IsV3) return false;
            ForceBreak();
            return true;
        }

        /// <summary>
        /// 처형: 남은 체력을 모두 깎아 쓰러뜨린다(받는 피해 배율을 거꾸로 셈해 넘치게). 허수아비(무한 체력)·이미 쓰러짐이면 0.
        /// 피해는 TakeHit 한 입구로 넣는다(피해 숫자·계측·처치 사건 그대로). 쓰는 곳: PlayerController 무너짐 처형·기습 처형(⑤).
        /// 처형은 같은 한 방의 뒷부분이라 칸 밖 공격 회피를 건너뛴다(followUp, 검토 1차 Q5): 칸 밖에서 긴 무기로 친 기습의 첫 타가 적을 깨워도
        /// 처형 피해가 '회피'로 빠져 무리를 깨우지 않는다. 칸 밖에서 깨어 있는 무리를 일반 공격으로 치는 회피는 그대로다.
        /// </summary>
        public int Execute(DamageSource source)
        {
            if (Dead || Health == null || Health.Infinite) return 0;
            float m = Mathf.Max(0.01f, Health.DamageTakenMultiplier);
            int amount = Mathf.CeilToInt(Health.Current / m) + 1;
            return TakeHit(amount, false, source, 0f, true, 1f, true, out _, out _);
        }

        /// <summary>
        /// 창 끊어 찌르기 제한: 마지막으로 끊긴 뒤 cooldown(1.5초)이 지났으면 지금 시각을 적고 true. 쓰는 곳: PlayerController(⑤)가
        /// staggers 단계 타를 마무리처럼 넣을지 정할 때(TakeHit의 finisher 인자).
        /// 보통 무게(궁수)는 지금 끊을 준비 동작이 있을 때만(StaggerWindupOpen) 끊고 시각을 적는다. 걷거나 물러나는 궁수를 찔러 제한만 써 버리지 않게 한다(마무리 검토 ③).
        /// </summary>
        public bool TryStaggerInterrupt(float cooldown)
        {
            if (Dead || Time.time - _lastStaggerInterrupt < cooldown) return false;
            if (Weight == EnemyWeight.Medium && !StaggerWindupOpen) return false;
            _lastStaggerInterrupt = Time.time;
            if (Weight == EnemyWeight.Medium) OnStaggerInterrupted();
            return true;
        }

        /// <summary>창 끊어 찌르기로 끊을 준비 동작이 지금 있는가(보통 무게만 봄). 기본은 없음.</summary>
        protected virtual bool StaggerWindupOpen => false;

        /// <summary>창 끊어 찌르기가 준비를 끊기로 정했을 때(끊기는 곧 TakeHit의 OnInterrupted로 일어난다). 궁수는 다음 한 발을 끊기지 않게 표시한다.</summary>
        protected virtual void OnStaggerInterrupted() { }

        /// <summary>
        /// 피해 없이 버팀만 깎는다(벽 박기 멧돼지 '최대치 35% + 0.15초 움찔', WallSlam ③). 3차 규칙이고 버팀이 있고 무너지지 않았을 때만.
        /// 0이 되면 무너진다(true). 무너지지 않았으면 flinchSeconds만큼 몸이 움찔한다(준비는 끊지 않음).
        /// </summary>
        public bool ApplyPoiseHit(double amount, float flinchSeconds)
        {
            if (Dead || !IsV3 || Poise == null || Broken || amount <= 0) return false;
            if (Poise.Apply(amount, Time.time))
            {
                Break();
                return true;
            }
            if (flinchSeconds > 0f) Shake(flinchSeconds, 0.09f);
            return false;
        }

        // ── 세 무기·오른쪽 클릭 계약(기획/세-무기-우클릭-소켓-1차.md 2-6). 부르는 곳: PlayerController.ReceiveHit(꾸러미 ③). 채우는 곳: 꾸러미 ④ 적 ──

        /// <summary>
        /// 플레이어 방패가 이 적의 공격(kind)을 튕겨 냈다(패링). 기본값은 무게별(상수는 Core ShieldRule):
        /// 가벼움(굴쥐) = 준비 끊김(OnInterrupted, 굴쥐는 여기서 공격 기회 AttackTokens도 반납) + 휘청 1.2초 + 플레이어 반대쪽으로 0.8 밀림,
        /// 보통(궁수) = 준비 끊김 + 휘청 0.6초 + 버팀(ParryPoise), 무거움(멧돼지 머리치기·뒷발, 정예) = 버팀(ParryPoise: 일반 50%, 정예 35%) + 0.5초 흔들림(준비는 안 끊김).
        /// 패링 피해(공격력 30%)는 PlayerController가 다음 프레임에 넣는다(이 함수는 적 두뇌의 공격 판정 안에서 불리므로 여기서 죽이지 않음).
        /// 멧돼지(돌진 = 벽 박기와 같은 결과)·오우거(휩쓸기만 버팀 6%)는 덮어쓴다. 부르는 곳: PlayerController.ReceiveHit(튕김일 때 source.Parried).
        /// 판정·피해 규칙은 바꾸지 않고 이 적의 반응만 정한다. 쓰러진 적은 무시한다.
        /// </summary>
        public virtual void Parried(HitKind kind)
        {
            if (Dead) return;
            switch (Weight)
            {
                case EnemyWeight.Light:
                {
                    OnInterrupted();
                    Stagger(ShieldRule.LightStaggerSeconds);
                    var player = Player;
                    Vector2 away = player ? Position - player.Position : -Facing;
                    if (away.sqrMagnitude < 0.0001f) away = -Facing;
                    // 밀림은 플레이어 몫 넉백이 아니다(KnockInfo 기본값): 벽 박기 판정(WallSlam)을 내지 않는다.
                    ApplyKnockback(away, ShieldRule.LightParryPush);
                    break;
                }
                case EnemyWeight.Medium:
                    OnInterrupted();
                    Stagger(ShieldRule.MediumStaggerSeconds);
                    // 패링 보상(2026-10-05): 그로기(버팀) 게이지도 깎는다(궁수 30 → 15). 휘청이 흔들림을 맡아 흔들림은 더하지 않는다.
                    if (Poise != null) ApplyPoiseHit(ParryPoiseAmount(false), 0f);
                    break;
                default:
                    ParriedHeavy();
                    break;
            }
        }

        /// <summary>
        /// 이 적의 공격(kind)을 튕겨 냈을 때 플레이어 반격 창(1.0초, 반격 베기)을 여는가(기획/세-무기-우클릭-소켓-1차.md 0-3의 23).
        /// 기본값은 살아 있으면 연다(굴쥐 휘청, 궁수 끊김, 멧돼지 돌진 무너짐·머리치기 회수 0.5초 + 흔들림). 오우거는 휩쓸기 마지막 타만(1타 뒤 0.5초에 2타가 옴).
        /// Parried보다 먼저 묻는다(적 반응이 상태를 바꾸기 전). 화살·덫처럼 때린 적이 없으면 PlayerController가 창을 열지 않는다.
        /// </summary>
        public virtual bool ParryOpensRiposte(HitKind kind) => !Dead;

        /// <summary>
        /// 무거움 패링 기본값: 버팀(그로기 게이지)을 ShieldRule.ParryPoise만큼(일반 50%, 정예 35%, 시험 손잡이 Tuning.ParryPoiseFraction·ParryElitePoiseFraction)
        /// 깎고 0.5초 흔들린다. 준비 동작은 끊지 않는다. 버팀이 없거나(M0a·허수아비) 이미 무너졌으면 흔들림만. 버팀이 0이 되면 무너진다(Enemy.ApplyPoiseHit 그대로).
        /// </summary>
        protected void ParriedHeavy()
        {
            if (Dead) return;
            double amount = ParryPoiseAmount(false);
            if (amount > 0.0 && ApplyPoiseHit(amount, ShieldRule.HeavyShakeSeconds)) return;
            if (!Broken) Shake(ShieldRule.HeavyShakeSeconds, 0.09f);
        }

        /// <summary>패링으로 깎을 버팀 양(ShieldRule.ParryPoise, 시험 손잡이 몫). bossSweep = 오우거 휩쓸기(6%).</summary>
        public double ParryPoiseAmount(bool bossSweep) =>
            Poise != null ? ShieldRule.ParryPoise(Class, Poise.Max, Tuning.ParryPoiseFraction, Tuning.ParryElitePoiseFraction, bossSweep) : 0.0;

        /// <summary>
        /// 휘청: seconds초 동안 멈추고 생각도 멈춘다(넉백 뒤 경직 0.15초와 따로 세고, 생각하며 맞는 적(ThinkWhileBusy)도 멈춘다).
        /// 그동안 맞음 자세로 그린다. 겹치면 더 늦게 끝나는 쪽. 준비 동작 끊김은 부르는 쪽이 정한다(Parried 기본값은 끊고 부름).
        /// 넉백은 그대로 돈다(휘청 중 밀림 가능). 쓰러진 적·0 이하 시간은 무시한다.
        /// </summary>
        public void Stagger(float seconds)
        {
            if (Dead || seconds <= 0f) return;
            if (!Busy) _hitStart = Time.time;
            _holdStaggerUntil = Mathf.Max(_holdStaggerUntil, Time.time + seconds);
            DesiredVelocity = Vector2.zero;
            Shake(Mathf.Min(seconds, StaggerShakeSeconds), StaggerShakeAmplitude);
        }

        /// <summary>휘청(Stagger) 중인가(시험 패널·브레인 확인용).</summary>
        public bool Staggered => !Dead && Time.time < _holdStaggerUntil;

        /// <summary>휘청 남은 시간(초).</summary>
        public float StaggerRemaining => Staggered ? _holdStaggerUntil - Time.time : 0f;

        /// <summary>잠든 무리 '뒤척임': 쉬는 중이고 아직 알아채지 않았으면 몸만 돌린다(깨우지 않음). 쓰는 곳: SleeperFidget(⑤).</summary>
        public void TurnWhileResting(Vector2 facing)
        {
            if (Dead || Aware || WakePending) return;
            FaceTowards(facing);
        }

        /// <summary>
        /// 1-2층 탐험 맛 1차 4-3 순찰: 쉬는(잠든) 채로 이 속도(초당 유닛)로 걷는다(깨우지 않음). 방향이 있으면 그쪽을 본다(걷는 쪽이 감지의 앞).
        /// 쓰러졌거나 깨었거나 곧 깨면('!'·무리 반응) 멈춘다. 깨면(Wake)·다시 쉬면(Rest) 걸음이 지워진다. 쓰는 곳: PatrolWalker(쉬는 동안 매 프레임).
        /// </summary>
        public void WalkWhileResting(Vector2 velocity)
        {
            if (Dead || Aware || WakePending)
            {
                _restWalk = Vector2.zero;
                return;
            }
            _restWalk = velocity;
            if (velocity.sqrMagnitude > 0.0001f) FaceTowards(velocity);
        }

        /// <summary>지금 넉백 중인가와 그 넉백 정보(벽 박기 판정·연출용, 읽기 전용).</summary>
        public bool Knocked => _knockTime > 0f;
        public KnockInfo CurrentKnock => _knockInfo;
        public float CurrentKnockPush => _knockPush;

        /// <summary>보스 설정(OgreBrain.OnSpawned, 꾸러미 ②): 보스 모드 버팀(4초 뒤 초당 10%), 무너짐 길이, 몸 질량, 무거움.</summary>
        protected void SetupBoss(double poiseMax, float breakSeconds, float bodyMass)
        {
            Poise = new PoiseMeter(Math.Max(1, poiseMax), boss: true);
            _breakLength = Mathf.Max(0.1f, breakSeconds);
            if (_body) _body.mass = Mathf.Max(0.1f, bodyMass);
            Weight = EnemyWeight.Heavy;
        }

        /// <summary>공격력을 바꾼다(보스 식 BossRules.Attack).</summary>
        protected void SetAttackPower(int value) => AttackPower = Mathf.Max(0, value);

        /// <summary>종류 걷기 속도를 바꾼다(보스 2단계 × 1.2). 적 걸음 배율·느려짐은 MoveSpeed가 곱한다.</summary>
        protected void SetBaseMoveSpeed(float speed) => _baseMoveSpeed = Mathf.Max(0f, speed);

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

        public void ApplyKnockback(Vector2 direction, float distance) => ApplyKnockback(direction, distance, default);

        /// <summary>
        /// 넉백(정보 포함). info.MaxDistance가 있으면 저항·배율을 곱한 뒤 거리를 그 값으로 자른다(큰 낫 끌어당김).
        /// info는 넉백이 끝날 때까지 들고 있다가 벽 박기 판정(꾸러미 ⑧)이 CombatEvents.EnemyWallSlam에 싣는다. 기본값이면 예전 넉백과 같다.
        /// </summary>
        public void ApplyKnockback(Vector2 direction, float distance, KnockInfo info)
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
                if (info.MaxDistance > 0f) push = Mathf.Min(push, info.MaxDistance);
                if (push <= 0.001f) return;
                var wall = Physics2D.CircleCast(Position, 0.2f, dir, push, Layers.WallMask);
                if (wall.collider) push = Mathf.Max(0f, wall.distance - 0.05f);
                _deathPush = dir * push;
                return;
            }
            float resist = CurrentKnockbackResist;
            if (resist >= 1f) return;
            distance *= (1f - resist) * Tuning.KnockbackScale;
            if (info.MaxDistance > 0f) distance = Mathf.Min(distance, info.MaxDistance);
            if (distance <= 0.001f) return;
            _knockInfo = info;
            _knockPush = distance;
            _knockSlammed = false;
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
                // 1-2층 탐험 맛 1차 4-3: 순찰은 쉬는 동안에도 걷는다(PatrolWalker → WalkWhileResting). 곧 깨는 중('!'·무리 반응 0.8초)이면 멈춘다.
                if (WakePending) _restWalk = Vector2.zero;
                DesiredVelocity = _restWalk;
                if (_wakeAt > 0f && Time.time >= _wakeAt) Wake(_noticing);
                // 칸에 묶인 적은 경계(칸 안쪽 + 문 1유닛) 안의 플레이어만 알아챈다(3차 초안 2-6).
                else if (_wakeAt < 0f && Player && !Player.IsDown && PlayerInTerritory(Player) && DetectsPlayer(Player))
                {
                    // 알아챘다: 0.5초 뒤 깨어나 무리를 깨운다. 그 전에 먼저 치면 기습이다.
                    _noticing = true;
                    _wakeAt = Time.time + NoticeDelay;
                    _restWalk = Vector2.zero;
                    DesiredVelocity = Vector2.zero;
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

            // 휘청(패링)은 넉백·경직과 따로 센다. 한 번도 휘청하지 않았으면 held는 늘 false라 예전과 같다.
            bool held = Time.time < _holdStaggerUntil;
            bool busy = _knockTime > 0f || _staggerTime > 0f || held;
            if (_knockTime <= 0f && _staggerTime > 0f) _staggerTime -= dt;
            // 칸 규칙(돌아가기·입구 지키기·칸 밖에서 돌아오기)이 이번 프레임을 맡으면 브레인은 쉰다. 칸이 없으면 늘 false.
            if (UpdateTerritory(dt, busy)) return;
            if (busy)
            {
                // 휘청 중에는 생각하며 맞는 적(멧돼지·오우거)도 생각을 멈춘다.
                if (ThinkWhileBusy && !held)
                {
                    // 시야와 문 1차 4-5: 생각하는 동안 만든 예고는 이 적이 주인(예외가 나도 다른 예고에 주인이 남지 않게 finally로 비움).
                    Telegraph.CreatingOwner = this;
                    try { Think(dt); }
                    finally { Telegraph.CreatingOwner = null; }
                    KeepInsideTerritory();
                }
                else DesiredVelocity = Vector2.zero;
                return;
            }
            Telegraph.CreatingOwner = this;
            try { Think(dt); }
            finally { Telegraph.CreatingOwner = null; }
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

        /// <summary>
        /// 잠든 적이 플레이어를 알아채는가. 기본: 앞 반경 6(±60°), 등 뒤 3, 벽 너머 못 봄(3차 초안 2-6).
        /// 등 뒤 감지는 듣기라 웅크리면 소음 배율(PlayerController.NoiseScale, 웅크림 0.3)을 몸 사이 거리에 곱한다(CrouchRules.HearCenterDistance):
        /// 굴쥐 1.36, 궁수 1.39, 멧돼지 1.57, 오우거 2.02(중심 거리). 중심 거리에 곱하면 0.9라 몸이 닿아도(멧돼지 0.95·오우거 1.6) 깨지 않아
        /// '덜 깸'이 아니라 '절대 안 깸'이 됐다(마무리 검토 ②). 서 있으면(배율 1) 예전과 같다. 앞 감지는 그대로.
        /// 웅크린 채 시작한 기본공격의 첫 판정 전(PlayerController.SneakStriking)에는 등 뒤에서 듣지 않는다: 쇠망치·도끼 내딛기처럼 공격 동작이 몸을 붙여도
        /// 그 타가 '들킴'이 되지 않게 한다(통합에서 고친 SneakWindup과 같은 뜻).
        /// </summary>
        protected virtual bool DetectsPlayer(PlayerController p)
        {
            Vector2 to = p.Position - Position;
            float d = to.magnitude;
            if (d > SeeFront) return false;
            bool front = Vector2.Angle(Facing, to) <= 60f;
            if (!front && (p.SneakStriking || d > Demo6.Core.Dungeon.CrouchRules.HearCenterDistance(SeeBehind, Radius + PlayerController.Radius, p.NoiseScale))) return false;
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
                CheckWallSlam();
                _knockTime -= Time.fixedDeltaTime;
                if (_knockTime <= 0f) _staggerTime = StaggerDuration;
                return;
            }
            // 쉬는 동안은 순찰 걸음(_restWalk, 순찰이 아니면 0)만 탄다(1-2층 탐험 맛 1차 4-3).
            _body.linearVelocity = _staggerTime > 0f || Time.time < _holdStaggerUntil || Broken ? Vector2.zero : !Aware ? _restWalk : DesiredVelocity;
        }

        /// <summary>
        /// 벽·기둥 박기 판정 한 곳(기획/전투-보스-무기-다듬기-1차.md 4-2 [1]). 살아 있는 적의 넉백 창(0.1초) 동안 물리 단계마다 본다.
        /// 플레이어 몫 넉백(KnockInfo.FromPlayer)이고 저항·배율을 곱한 거리가 WallSlamRule.MinPush(0.5) 이상일 때만,
        /// 이번 물리 단계에 갈 거리(속도 × 고정 간격 + 여유)를 몸 반지름 × 0.9 원으로 벽 층(기둥 포함)에 쏴서 닿으면 CombatEvents.EnemyWallSlam을 낸다.
        /// 넉백 한 번에 한 번, 같은 적은 WallSlamRule.Cooldown(0.5초)에 한 번. 쌍검 회전베기(0.45)처럼 짧은 넉백은 빠진다.
        /// 결과(굴쥐 피해 추가·무너짐·버팀 깎기)와 연출은 WallSlam(꾸러미 ③)이 사건을 듣고 정한다. 이 자리는 판정과 사건만 맡는다.
        /// 쓰러진 적의 날림(_deathPush)은 이 길을 지나지 않는다. Tuning.WallSlamOn을 끄면 사건도 내지 않는다.
        /// </summary>
        void CheckWallSlam()
        {
            if (_knockSlammed || !Tuning.WallSlamOn) return;
            if (!_knockInfo.FromPlayer || !WallSlamRule.PushedEnough(_knockPush)) return;
            if (!WallSlamRule.Ready(_lastWallSlam, Time.time)) return;
            float speed = _knockVelocity.magnitude;
            if (speed < 0.0001f) return;
            Vector2 dir = _knockVelocity / speed;
            var hit = Physics2D.CircleCast(_body.position, Radius * 0.9f, dir, speed * Time.fixedDeltaTime + WallSlamProbe, Layers.WallMask);
            if (!hit.collider) return;
            _knockSlammed = true;
            _lastWallSlam = Time.time;
            CombatEvents.RaiseEnemyWallSlam(this, new WallSlamHit(hit.point, hit.normal, _knockInfo, _knockPush));
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

        /// <summary>
        /// 잔혹(기획/다크판타지-분위기-1차.md): 크게 넘치게 베여 조각났다. GoreSystem이 조각을 흩뿌린 뒤 불러 쓰러지는 몸을 숨긴다.
        /// 처치 연출 시간·날림 거리는 그대로 흐르고(물체 제거 시각이 바뀌지 않게) 시체만 남기지 않는다.
        /// </summary>
        public void Dismember()
        {
            if (!Dead || Dismembered) return;
            Dismembered = true;
            if (Sprite) Sprite.enabled = false;
        }

        /// <summary>처치 연출이 끝난 자리에 시체를 남긴다(GoreSystem). 남겼으면 true.</summary>
        bool LeaveCorpse(Vector3 restScale) =>
            GoreSystem.LeaveCorpse(this, Sprite, restScale, Flash ? Flash.baseColor : Sprite.color, _visual && _visual.ArtShown);

        IEnumerator DeathRoutine()
        {
            // 시체 크기 기준: 날림·줄어듦이 바꾸기 전의 몸 크기.
            Vector3 restScale = Sprite.transform.lossyScale;
            if (Tuning.KillFling)
            {
                yield return FlingRoutine();
                // 잔혹: 날림이 끝난 자리에 시체가 남는다(날림 동안 몸은 흐려지지 않았다).
                LeaveCorpse(restScale);
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
            if (LeaveCorpse(restScale))
            {
                // 잔혹: 시체가 몸을 대신한다. 줄어드는 대신 숨기고 같은 시간(0.2초)을 기다려 제거 시각을 지킨다.
                Sprite.enabled = false;
                float wait = 0f;
                while (wait < 0.2f)
                {
                    wait += Time.deltaTime;
                    yield return null;
                }
                Remove();
                yield break;
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

        /// <summary>
        /// 처치 날림: 짧게 번쩍인 뒤 맞은 방향으로 굴러가듯 날아가며 사라진다(0.32초).
        /// 잔혹: 시체로 남을 몸(GoreSystem.KeepsCorpse)은 흐려지지 않고 조금만 줄어 끝난 자리에서 시체로 이어진다. 거리·시간·회전은 그대로.
        /// </summary>
        IEnumerator FlingRoutine()
        {
            const float Duration = 0.32f;
            bool keepBody = GoreSystem.KeepsCorpse(this);
            float endScale = keepBody ? 0.92f : 0.7f;
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
                Sprite.transform.localScale = scale * Mathf.Lerp(1f, endScale, k);
                var c = Sprite.color;
                c.a = keepBody || k < 0.5f ? 1f : 1f - (k - 0.5f) / 0.5f;
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
