using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 기획 4-3 가시 궁수: 5~7 거리를 유지하고, 3 안으로 들어오면 1.5초 동안 뒤로 물러난다.
    /// 얇은 빨간 조준선이 0.8초 따라오다가 마지막 0.25초에 고정되고 화살을 쏜다(초당 10, 사거리 12, 벽에 막힘).
    /// 재사용 2.2초. 6층부터 15° 간격 3갈래.
    /// 3차(초안 3-4): 체력 600·공격 150, 보통 무게(일반 타격엔 조준이 멈췄다 이어지고, 마무리·스킬·무너짐에만 끊김),
    /// 세 번째 사격마다 꿰뚫는 화살(굵은 조준선 1.4초, 200%), 거리 2.5 안이면 뒤로 3.0 뛰며 가시 덫(재사용 5초).
    /// 던전(3차 초안 2-4): 화면 가장자리에서 1유닛 안쪽에 들어와야 조준한다.
    /// </summary>
    public sealed class ArcherBrain : Enemy
    {
        const float KeepMin = 5f;
        const float KeepMax = 7f;
        const float FleeDistance = 3f;
        const float FleeTime = 1.5f;
        const float AimTime = 0.8f;
        const float PierceAimTime = 1.4f;
        const float LockTime = 0.25f;
        const float ShotCooldown = 2.2f;
        const float InterruptedCooldown = 1.0f;
        const float AimLineWidth = 0.12f;
        const float PierceLineWidth = 0.3f;
        const float PiercePercent = 200f;
        const float Spread = 15f;
        const float JumpTrigger = 2.5f;
        /// <summary>뒤로 뛰기(0.25초에 3.0)는 덫 자리와 거리를 지키려고 느려짐(Enemy.ApplySlow)을 받지 않는다. 거리 두기·물러나기는 MoveSpeed를 써서 느려진다.</summary>
        const float JumpDistance = 3.0f;
        const float JumpTime = 0.25f;
        const float JumpCooldown = 5f;
        /// <summary>3차 초안 2-4 공정 규칙: 화면 가장자리에서 이만큼 안쪽에 들어와야 조준한다.</summary>
        const float ScreenEdgeInset = 1f;

        enum State
        {
            Move,
            Aim,
            Flee,
            Jump,
        }

        State _state;
        float _timer;
        float _cooldown = 1.2f;
        float _jumpCooldown;
        float _aimTime = AimTime;
        float _reservedEnd = -1f;
        float _strafeSign = 1f;
        float _strafeFlipTimer;
        int _shots;
        bool _pierce;
        Vector2 _aimDir;
        Vector2 _jumpDir;
        bool _locked;
        bool _hasToken;
        bool _triple;
        Telegraph _telegraph;
        float _lastShot = -999f;
        /// <summary>6층부터 ±15° 옆 화살에도 조준선을 보인다.</summary>
        readonly Telegraph[] _sideTelegraphs = new Telegraph[2];

        /// <summary>M0a: 넉백이 조준을 끊는다. 3차: 일반 타격엔 멈췄다 이어지고, 마무리·스킬·무너짐에만 끊긴다.</summary>
        protected override bool Interruptible => !IsV3;

        /// <summary>
        /// 창 끊어 찌르기에 끊긴 뒤 아직 한 발도 쏘지 않았는가. 그동안의 조준은 찌르기로 다시 끊기지 않는다(쏘면 풀림).
        /// 끊긴 뒤 쉬기 1.0초 + 조준 0.8초(꿰뚫는 화살 1.4초) = 1.8초 이상이라 1.5초 제한만으로는 계속 묶어 둘 수 있었다(마무리 검토 ③, 문서 2-3 '계속 묶어 두기' 막기).
        /// 마무리(③)·스킬·무너짐은 예전처럼 제한 없이 끊는다.
        /// </summary>
        bool _staggerGuard;

        /// <summary>창 끊어 찌르기로 끊을 것: 3차 규칙에서 조준 중이고, 찌르기에 끊긴 뒤 한 발을 쏜 다음이다.</summary>
        protected override bool StaggerWindupOpen => IsV3 && _state == State.Aim && !_staggerGuard;

        protected override void OnStaggerInterrupted() => _staggerGuard = true;

        protected override void Think(float dt)
        {
            var player = Player;
            if (!player || player.IsDown)
            {
                DesiredVelocity = Vector2.zero;
                if (_state == State.Aim) CancelAim(0.5f);
                return;
            }
            Vector2 to = player.Position - Position;
            float dist = to.magnitude;
            Vector2 toDir = dist > 0.0001f ? to / dist : Facing;
            _jumpCooldown -= dt;

            switch (_state)
            {
                case State.Move:
                    SetPose(Time.time - _lastShot < 0.2f ? EnemyPose.Attack : EnemyPose.Locomotion);
                    _cooldown -= dt;
                    FaceTowards(to);
                    if (IsV3 && dist < JumpTrigger && _jumpCooldown <= 0f && TryJump(toDir)) break;
                    if (dist < FleeDistance)
                    {
                        _state = State.Flee;
                        _timer = 0f;
                        break;
                    }
                    DesiredVelocity = KeepDistanceVelocity(toDir, dist, dt);
                    if (_cooldown <= 0f && dist <= Arrow.Range - 1f && HasLineOfSight(Position, player.Position) && InsideCameraView() && AcquireToken())
                        BeginAim(player, toDir);
                    break;

                case State.Aim:
                    DesiredVelocity = Vector2.zero;
                    _timer += dt;
                    // 3차: 경직으로 조준이 멈췄다 이어지면 실제로 쏠 시각으로 강한 공격 자리를 옮긴다.
                    if (IsV3) StrongAttackSchedule.Move(ref _reservedEnd, Time.time + Mathf.Max(0f, _aimTime - _timer));
                    if (!_locked)
                    {
                        _aimDir = toDir;
                        FaceTowards(_aimDir);
                        if (_telegraph) _telegraph.SetRect(Position, _aimDir);
                        if (_sideTelegraphs[0]) _sideTelegraphs[0].SetRect(Position, Rotate(_aimDir, Spread));
                        if (_sideTelegraphs[1]) _sideTelegraphs[1].SetRect(Position, Rotate(_aimDir, -Spread));
                        if (_timer >= _aimTime - LockTime)
                        {
                            _locked = true;
                            if (_telegraph) _telegraph.Lock();
                            foreach (var side in _sideTelegraphs)
                                if (side) side.Lock();
                        }
                    }
                    // 3차: 예고를 직접 채운다. 경직으로 Think가 멈추면 예고도 함께 멈춘다.
                    if (IsV3)
                    {
                        if (_telegraph) _telegraph.Drive(_timer);
                        foreach (var side in _sideTelegraphs)
                            if (side) side.Drive(_timer);
                    }
                    if (_timer >= _aimTime) Fire();
                    break;

                case State.Flee:
                    SetPose(EnemyPose.Locomotion);
                    _timer += dt;
                    _cooldown -= dt;
                    FaceTowards(to);
                    // 물러나는 중에도 2.5 안으로 붙으면 바로 뒤로 뛰며 덫을 놓는다.
                    if (IsV3 && dist < JumpTrigger && _jumpCooldown <= 0f && TryJump(toDir)) break;
                    DesiredVelocity = -toDir * MoveSpeed;
                    if (_timer >= FleeTime) _state = State.Move;
                    break;

                case State.Jump:
                    SetPose(EnemyPose.Attack);
                    _timer += dt;
                    _cooldown -= dt;
                    DesiredVelocity = _jumpDir * (JumpDistance / JumpTime);
                    if (_timer >= JumpTime)
                    {
                        _state = State.Move;
                        DesiredVelocity = Vector2.zero;
                    }
                    break;
            }
        }

        void BeginAim(PlayerController player, Vector2 toDir)
        {
            _state = State.Aim;
            _timer = 0f;
            _locked = false;
            _aimDir = toDir;
            DesiredVelocity = Vector2.zero;
            // 3차: 세 번째 사격마다 꿰뚫는 화살(굵은 조준선 1.4초).
            _pierce = IsV3 && (_shots + 1) % 3 == 0;
            float baseTime = (_pierce ? PierceAimTime : AimTime) + TelegraphBonus;
            // 꿰뚫는 화살은 3차 예고 규칙(검토 1차 Q6, TelegraphRule)으로 최소 예고를 맞댄다. 1.4초가 늘 더 길어 시간은 그대로이고(2층 12.6% → 최소 0.6초),
            // 한 방이 그 층 기준 체력 10%를 넘으면 조준 시작에 시위 소리를 낸다(1~6·8층, 정예는 1~10층).
            if (_pierce) baseTime = RuleTelegraph(baseTime, PiercePercent);
            _aimTime = StrongAttackSchedule.Reserve(baseTime, out _reservedEnd);
            SetPose(EnemyPose.Windup, _aimTime, _aimTime);
            _telegraph = Telegraph.Rect(Position, _aimDir, Arrow.Range, _pierce ? PierceLineWidth : AimLineWidth, _aimTime);
            // 기획 12장: 예고가 시작될 때 범위 안(조준선이 플레이어를 향함)이고 구르기를 쓸 수 있었는가.
            _telegraph.Avoidable = player.DodgeReady;
            if (_pierce && TelegraphNeedsCue(PiercePercent)) Telegraph.PlayStartCue(TelegraphCue.ArcherDraw);
            _triple = FloorScaling.ArcherTripleShot(Floor) && !_pierce;
            if (_triple)
            {
                _sideTelegraphs[0] = Telegraph.Rect(Position, Rotate(_aimDir, Spread), Arrow.Range, AimLineWidth, _aimTime);
                _sideTelegraphs[1] = Telegraph.Rect(Position, Rotate(_aimDir, -Spread), Arrow.Range, AimLineWidth, _aimTime);
            }
        }

        /// <summary>3차: 뒤로 3.0 뛰며 원래 자리에 가시 덫을 놓는다. 뒤가 벽이면 옆으로 뛴다.</summary>
        bool TryJump(Vector2 toDir)
        {
            Vector2 away = -toDir;
            Vector2[] options = { away, Rotate(away, 50f), Rotate(away, -50f) };
            foreach (var dir in options)
            {
                if (Physics2D.CircleCast(Position, Radius, dir, JumpDistance, Layers.WallMask)) continue;
                SpikeTrap.Place(Position, AttackPower, Floor);
                _jumpDir = dir;
                _state = State.Jump;
                _timer = 0f;
                _jumpCooldown = JumpCooldown;
                return true;
            }
            return false;
        }

        Vector2 KeepDistanceVelocity(Vector2 toDir, float dist, float dt)
        {
            if (dist < KeepMin) return -toDir * MoveSpeed;
            if (dist > KeepMax) return toDir * MoveSpeed;
            // 적정 거리에서는 옆으로 천천히 돈다. 벽이 앞에 있거나 가끔씩 방향을 바꾼다.
            _strafeFlipTimer -= dt;
            Vector2 side = new Vector2(-toDir.y, toDir.x) * _strafeSign;
            if (_strafeFlipTimer <= 0f || Physics2D.CircleCast(Position, Radius, side, 0.6f, Layers.WallMask))
            {
                _strafeSign = -_strafeSign;
                _strafeFlipTimer = Random.Range(1.5f, 3f);
                side = -side;
            }
            return side * (MoveSpeed * 0.5f);
        }

        void Fire()
        {
            bool triple = _triple;
            _lastShot = Time.time;
            _shots++;
            _staggerGuard = false;
            Sfx.Play(SfxKind.ArcherShot);
            var volley = new ArrowVolley(_telegraph ? _telegraph.Avoidable : false, triple ? 3 : 1);
            Vector2 origin = Position + _aimDir * (Radius + 0.1f);
            Arrow.Spawn(origin, _aimDir, AttackPower, volley, _pierce ? PiercePercent : 100f, _pierce);
            if (triple)
            {
                Arrow.Spawn(origin, Rotate(_aimDir, Spread), AttackPower, volley);
                Arrow.Spawn(origin, Rotate(_aimDir, -Spread), AttackPower, volley);
            }
            if (_telegraph) _telegraph.Resolve();
            _telegraph = null;
            _reservedEnd = -1f;
            for (int i = 0; i < _sideTelegraphs.Length; i++)
            {
                if (_sideTelegraphs[i]) _sideTelegraphs[i].Resolve();
                _sideTelegraphs[i] = null;
            }
            ReleaseToken();
            _cooldown = ShotCooldown;
            _state = State.Move;
            _timer = 0f;
        }

        void CancelAim(float cooldown)
        {
            if (_telegraph) _telegraph.Cancel();
            _telegraph = null;
            StrongAttackSchedule.Release(ref _reservedEnd);
            for (int i = 0; i < _sideTelegraphs.Length; i++)
            {
                if (_sideTelegraphs[i]) _sideTelegraphs[i].Cancel();
                _sideTelegraphs[i] = null;
            }
            ReleaseToken();
            if (_state == State.Aim)
            {
                _state = State.Move;
                _cooldown = Mathf.Max(_cooldown, cooldown);
            }
        }

        /// <summary>
        /// 3차 초안 2-4 공정 규칙: 따라가는 카메라(던전)에서는 궁수가 화면 가장자리에서 1유닛 안쪽에 들어와야 조준한다(화면 밖 화살 방지).
        /// 전투 시험장은 방 전체가 한 화면에 보여 따지지 않는다(M0a 동작 그대로).
        /// </summary>
        bool InsideCameraView()
        {
            if (!DungeonRoot.Instance) return true;
            var cam = Camera.main;
            if (!cam || !cam.orthographic) return true;
            float halfH = cam.orthographicSize - ScreenEdgeInset;
            float halfW = cam.orthographicSize * cam.aspect - ScreenEdgeInset;
            Vector2 d = Position - (Vector2)cam.transform.position;
            return Mathf.Abs(d.x) <= halfW && Mathf.Abs(d.y) <= halfH;
        }

        /// <summary>놓아주기·칸 밖에서 돌아오기·다시 섬(3차 초안 2-6): 조준을 거두고 공격 기회를 돌려준 뒤 거리 두기부터 다시.</summary>
        protected override void ResetBehaviour()
        {
            CancelAim(InterruptedCooldown);
            _state = State.Move;
            _timer = 0f;
            DesiredVelocity = Vector2.zero;
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r);
            float s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        bool AcquireToken()
        {
            if (_hasToken) return true;
            _hasToken = AttackTokens.TryAcquire(Kind);
            return _hasToken;
        }

        void ReleaseToken()
        {
            if (!_hasToken) return;
            _hasToken = false;
            AttackTokens.Release(Kind);
        }

        protected override void OnInterrupted() => CancelAim(InterruptedCooldown);

        protected override void OnBroken()
        {
            CancelAim(InterruptedCooldown);
            if (_state == State.Jump) _state = State.Move;
        }
    }

    /// <summary>한 번에 쏜 화살 묶음. 전부 끝나면 예고 결과를 한 번만 기록한다.</summary>
    public sealed class ArrowVolley
    {
        readonly bool _avoidable;
        int _remaining;
        bool _hit;

        public ArrowVolley(bool avoidable, int count)
        {
            _avoidable = avoidable;
            _remaining = count;
        }

        public void Report(bool hit)
        {
            _hit |= hit;
            if (--_remaining == 0) CombatEvents.RaiseTelegraph(_avoidable, _hit);
        }
    }
}
