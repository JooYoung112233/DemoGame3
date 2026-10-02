using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 기획 4-3 뿔멧돼지.
    /// 거리 3~7: 바닥 빨간 직선(폭 1.2, 길이 7)으로 0.7초 예고 → 방향 고정, 최대 7유닛 돌진(초당 12) → 0.8초 숨 고르기.
    /// 돌진 중에는 EnemyCharging 레이어로 다른 적과 부딪히지 않는다. 가까우면 머리치기(예고 0.4초, 반경 1.2).
    /// 8층부터 첫 돌진 뒤 0.4초 예고로 다시 조준해 한 번 더 돌진. 준비 동작은 넉백으로 끊기지 않는다.
    /// M0a: 벽·기둥에 박히면 1.5초 기절(받는 피해 +30%), 재사용 3.5초.
    /// 3차(초안 3-4): 체력 2,000·공격 400, 재사용 3.0초, 벽·기둥 박기 = 바로 무너짐, 몸을 천천히 돌리고(초당 150°)
    /// 등 뒤 100°·거리 2 안에 1.0초 넘게 있으면 뒷발차기(예고 0.4초, 60%). 큰 공격 예고는 정예 +0.1초, 겹치면 0.3초 미룸.
    /// </summary>
    public sealed class BoarBrain : Enemy
    {
        const float ChargeMinDistance = 3f;
        const float ChargeMaxDistance = 7f;
        const float ChargeTelegraphTime = 0.7f;
        const float ChargeSpeed = 12f;
        const float ChargeLength = 7f;
        const float ChargeWidth = 1.2f;
        const float RecoverTime = 0.8f;
        const float StunTime = 1.5f;
        const float StunDamageTaken = 1.3f;
        const float HeadbuttWindup = 0.4f;
        const float HeadbuttRadius = 1.2f;
        /// <summary>머리치기 원은 몸 앞쪽에 둔다(기획은 '반경 1.2'만 정함). 앞끝이 2.2까지 닿는다.</summary>
        const float HeadbuttOffset = 1.0f;
        /// <summary>머리치기를 시작하는 거리. 원의 앞끝(2.2)보다 조금 안쪽에서 멈춘다(사거리 − 0.1에 가까움).</summary>
        const float HeadbuttTrigger = 2.0f;
        const float HeadbuttRecover = 0.5f;
        const float HeadbuttPercent = 60f;
        const float ReaimTime = 0.4f;
        const float KickWindup = 0.4f;
        const float KickRecover = 0.4f;
        const float KickRange = 2.0f;
        const float KickArc = 100f;
        const float KickNeedBehind = 1.0f;
        const float KickPercent = 60f;
        const float TurnRateV3 = 150f;

        enum State
        {
            Walk,
            ChargeTelegraph,
            Charging,
            Recover,
            Stunned,
            HeadbuttWindup,
            HeadbuttRecover,
            KickWindup,
            KickRecover,
        }

        State _state;
        float _timer;
        float _cooldown = 1.0f;
        float _telegraphTime;
        float _windupTime;
        float _reservedEnd = -1f;
        float _behindTime;
        Vector2 _chargeDir;
        Vector2 _chargeStart;
        Vector2 _lastChargePos;
        bool _hitPlayerThisCharge;
        bool _hasToken;
        bool _secondChargeUsed;
        Telegraph _telegraph;
        SpriteRenderer _stunMark;

        float AttackCooldown => IsV3 ? 3.0f : 3.5f;

        protected override bool Interruptible => false;

        protected override bool ThinkWhileBusy => true;

        protected override float CurrentKnockbackResist => _state == State.Charging ? 1f : BaseKnockbackResist;

        public bool IsStunned => _state == State.Stunned;

        protected override void OnSpawned()
        {
            var mark = new GameObject("Stun");
            mark.transform.SetParent(transform, false);
            mark.transform.localPosition = new Vector3(0f, Radius + 0.3f, 0f);
            mark.transform.localScale = Vector3.one * 0.7f;
            _stunMark = mark.AddComponent<SpriteRenderer>();
            RenderMaterials.MakeUnlit(_stunMark);
            _stunMark.sprite = ShapeSprites.Ring;
            _stunMark.color = Palette.Stun;
            _stunMark.sortingOrder = 5;
            _stunMark.enabled = false;
            GetComponent<YSort>()?.Refresh();
        }

        protected override void Think(float dt)
        {
            var player = Player;
            bool playerUp = player && !player.IsDown;
            Vector2 to = playerUp ? player.Position - Position : Vector2.zero;
            float dist = to.magnitude;
            ExtraJitter = Vector2.zero;
            TrackBehind(playerUp, to, dist, dt);

            switch (_state)
            {
                case State.Walk:
                    SetPose(EnemyPose.Locomotion);
                    _cooldown -= dt;
                    if (!playerUp)
                    {
                        DesiredVelocity = Vector2.zero;
                        break;
                    }
                    bool facingPlayer = Turn(to, dt);
                    DesiredVelocity = dist > HeadbuttTrigger ? (IsV3 ? Facing * (facingPlayer ? 1f : 0.35f) : to / Mathf.Max(dist, 0.0001f)) * MoveSpeed : Vector2.zero;
                    // 뒷발차기는 공격 재사용과 따로 본다: 회복·박기 뒤 등 뒤에 1초 넘게 붙어 있으면 바로 찬다(등 뒤 1초가 곧 간격).
                    if (IsV3 && _behindTime >= KickNeedBehind && AcquireToken())
                    {
                        BeginKick();
                        break;
                    }
                    if (_cooldown > 0f) break;
                    if (dist >= ChargeMinDistance && dist <= ChargeMaxDistance && facingPlayer && HasLineOfSight(Position, player.Position) && AcquireToken())
                    {
                        _secondChargeUsed = false;
                        BeginChargeTelegraph(to, ChargeTelegraphTime + TelegraphBonus);
                    }
                    else if (dist <= HeadbuttTrigger && facingPlayer && AcquireToken())
                    {
                        _state = State.HeadbuttWindup;
                        _timer = 0f;
                        DesiredVelocity = Vector2.zero;
                        _windupTime = StrongAttackSchedule.Reserve(HeadbuttWindup + TelegraphBonus, out _reservedEnd);
                        SetPose(EnemyPose.Windup, _windupTime, _windupTime);
                        _telegraph = Telegraph.Circle(HeadbuttCenter(), HeadbuttRadius, _windupTime);
                        _telegraph.Avoidable = _telegraph.Contains(player.Position, PlayerController.Radius) && player.DodgeReady;
                    }
                    break;

                case State.ChargeTelegraph:
                    DesiredVelocity = Vector2.zero;
                    _timer += dt;
                    if (_telegraph) _telegraph.SetRect(Position, _chargeDir);
                    // 앞발 긁기: 몸을 잘게 떤다.
                    ExtraJitter = Random.insideUnitCircle * 0.05f;
                    if (_timer >= _telegraphTime)
                    {
                        ExtraJitter = Vector2.zero;
                        ClearStagger();
                        _state = State.Charging;
                        _timer = 0f;
                        _chargeStart = Position;
                        _lastChargePos = Position;
                        _hitPlayerThisCharge = false;
                        SetLayer(Layers.EnemyCharging);
                        SetPose(EnemyPose.Attack);
                        Sfx.Play(SfxKind.BoarCharge);
                        DesiredVelocity = _chargeDir * ChargeSpeed;
                    }
                    break;

                case State.Charging:
                {
                    _timer += dt;
                    Vector2 now = Position;
                    // 지나온 선분으로 판정해 프레임이 낮아도 플레이어를 건너뛰지 않는다.
                    if (!_hitPlayerThisCharge && playerUp &&
                        SegmentDistance(_lastChargePos, now, player.Position) <= Radius + PlayerController.Radius)
                        _hitPlayerThisCharge = player.ReceiveHit(AttackPower, 100f, now, 0.4f);
                    _lastChargePos = now;
                    float remaining = ChargeLength - (now - _chargeStart).magnitude;
                    // 마지막 물리 단계에서 7을 넘지 않게 속도를 줄인다.
                    DesiredVelocity = _chargeDir * Mathf.Min(ChargeSpeed, Mathf.Max(0f, remaining) / Time.fixedDeltaTime);
                    if (remaining <= 0.02f || _timer > ChargeLength / ChargeSpeed + 0.3f) EndCharge(false);
                    break;
                }

                case State.Recover:
                    SetPose(EnemyPose.Idle);
                    DesiredVelocity = Vector2.zero;
                    _timer += dt;
                    if (_timer >= RecoverTime) BackToWalk();
                    break;

                case State.Stunned:
                    SetPose(EnemyPose.Hit);
                    DesiredVelocity = Vector2.zero;
                    _timer += dt;
                    _stunMark.transform.localRotation = Quaternion.Euler(0f, 0f, _timer * 360f);
                    if (_timer >= StunTime)
                    {
                        _stunMark.enabled = false;
                        Health.DamageTakenMultiplier = 1f;
                        BackToWalk();
                    }
                    break;

                case State.HeadbuttWindup:
                    DesiredVelocity = Vector2.zero;
                    _timer += dt;
                    if (_telegraph) _telegraph.SetCenter(HeadbuttCenter());
                    if (_timer >= _windupTime)
                    {
                        bool hit = false;
                        if (playerUp && (player.Position - HeadbuttCenter()).magnitude <= HeadbuttRadius + PlayerController.Radius)
                            hit = player.ReceiveHit(AttackPower, HeadbuttPercent, Position, 0.4f);
                        ResolveTelegraph(hit);
                        _state = State.HeadbuttRecover;
                        _timer = 0f;
                    }
                    break;

                case State.HeadbuttRecover:
                    DesiredVelocity = Vector2.zero;
                    _timer += dt;
                    SetPose(_timer < 0.2f ? EnemyPose.Attack : EnemyPose.Idle);
                    if (_timer >= HeadbuttRecover) BackToWalk();
                    break;

                case State.KickWindup:
                    DesiredVelocity = Vector2.zero;
                    _timer += dt;
                    if (_telegraph) _telegraph.SetCenter(KickCenter());
                    if (_timer >= _windupTime)
                    {
                        bool hit = false;
                        if (playerUp && (player.Position - KickCenter()).magnitude <= KickRange * 0.6f + PlayerController.Radius)
                            hit = player.ReceiveHit(AttackPower, KickPercent, Position, 1.0f);
                        ResolveTelegraph(hit);
                        _state = State.KickRecover;
                        _timer = 0f;
                        _behindTime = 0f;
                    }
                    break;

                case State.KickRecover:
                    DesiredVelocity = Vector2.zero;
                    _timer += dt;
                    SetPose(_timer < 0.2f ? EnemyPose.Attack : EnemyPose.Idle);
                    if (_timer >= KickRecover) BackToWalk();
                    break;
            }
        }

        /// <summary>3차: 몸을 초당 150°까지만 돌린다(등 뒤로 돌아 들어갈 틈). M0a: 바로 돈다.</summary>
        /// <returns>플레이어를 거의(25° 안) 바라보는가.</returns>
        bool Turn(Vector2 to, float dt)
        {
            if (to.sqrMagnitude < 0.0001f) return true;
            if (!IsV3)
            {
                FaceTowards(to);
                return true;
            }
            float current = Mathf.Atan2(Facing.y, Facing.x) * Mathf.Rad2Deg;
            float target = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            float next = Mathf.MoveTowardsAngle(current, target, TurnRateV3 * dt);
            float r = next * Mathf.Deg2Rad;
            FaceTowards(new Vector2(Mathf.Cos(r), Mathf.Sin(r)));
            return Mathf.Abs(Mathf.DeltaAngle(next, target)) <= 25f;
        }

        void TrackBehind(bool playerUp, Vector2 to, float dist, float dt)
        {
            if (!IsV3 || !playerUp || dist > KickRange || _state == State.Charging)
            {
                _behindTime = 0f;
                return;
            }
            bool behind = Vector2.Angle(-Facing, to) <= KickArc * 0.5f;
            _behindTime = behind ? _behindTime + dt : 0f;
        }

        Vector2 HeadbuttCenter() => Position + Facing * HeadbuttOffset;

        Vector2 KickCenter() => Position - Facing * (KickRange * 0.55f);

        void BeginKick()
        {
            _state = State.KickWindup;
            _timer = 0f;
            DesiredVelocity = Vector2.zero;
            _windupTime = StrongAttackSchedule.Reserve(KickWindup + TelegraphBonus, out _reservedEnd);
            SetPose(EnemyPose.Windup, _windupTime, _windupTime);
            _telegraph = Telegraph.Circle(KickCenter(), KickRange * 0.6f, _windupTime);
            var player = Player;
            _telegraph.Avoidable = player && _telegraph.Contains(player.Position, PlayerController.Radius) && player.DodgeReady;
        }

        void BeginChargeTelegraph(Vector2 to, float duration)
        {
            FaceTowards(to);
            _chargeDir = to.sqrMagnitude > 0.0001f ? to.normalized : Facing;
            _state = State.ChargeTelegraph;
            _timer = 0f;
            _telegraphTime = StrongAttackSchedule.Reserve(duration, out _reservedEnd);
            SetPose(EnemyPose.Windup, _telegraphTime, _telegraphTime);
            DesiredVelocity = Vector2.zero;
            // 몸 앞끝이 닿는 범위(중심 이동 7 + 반지름)까지 그려 '빨강에 닿음 = 맞음'이 되게 한다.
            // 폭도 몸에 맞춘다(정예는 몸이 1.35배): 빨강 가장자리 + 플레이어 반지름 ≥ 몸 반지름 + 플레이어 반지름.
            _telegraph = Telegraph.Rect(Position, _chargeDir, ChargeLength + Radius, Mathf.Max(ChargeWidth, Radius * 2f), _telegraphTime);
            var player = Player;
            _telegraph.Avoidable = player && _telegraph.Contains(player.Position, PlayerController.Radius) && player.DodgeReady;
        }

        void EndCharge(bool hitWall)
        {
            SetLayer(Layers.Enemy);
            DesiredVelocity = Vector2.zero;
            ResolveTelegraph(_hitPlayerThisCharge);
            if (hitWall)
            {
                ScreenShake.Add(0.08f, 0.1f);
                if (IsV3)
                {
                    // 3차: 벽·기둥 박기 = 바로 무너짐(무너짐 2초, 받는 피해 +30%).
                    BackToWalk();
                    ForceBreak();
                    return;
                }
                _state = State.Stunned;
                _timer = 0f;
                Health.DamageTakenMultiplier = StunDamageTaken;
                _stunMark.enabled = true;
                return;
            }
            var player = Player;
            if (FloorScaling.BoarDoubleCharge(Floor) && !_secondChargeUsed && player && !player.IsDown)
            {
                // 8층부터: 0.4초 예고로 다시 조준해 한 번 더 돌진한다.
                _secondChargeUsed = true;
                BeginChargeTelegraph(player.Position - Position, ReaimTime + TelegraphBonus);
                return;
            }
            _state = State.Recover;
            _timer = 0f;
        }

        void ResolveTelegraph(bool hit)
        {
            _reservedEnd = -1f;
            if (!_telegraph) return;
            CombatEvents.RaiseTelegraph(_telegraph.Avoidable, hit);
            _telegraph.Resolve();
            _telegraph = null;
        }

        void BackToWalk()
        {
            _state = State.Walk;
            _timer = 0f;
            _cooldown = AttackCooldown;
            ReleaseToken();
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

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (_state == State.Charging && collision.gameObject.layer == Layers.Wall) EndCharge(true);
        }

        static float SegmentDistance(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 0.000001f) return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>무너지면 준비·돌진을 멈추고 일어난 뒤 걷기부터 다시 한다.</summary>
        protected override void OnBroken()
        {
            if (_telegraph) _telegraph.Cancel();
            _telegraph = null;
            StrongAttackSchedule.Release(ref _reservedEnd);
            if (_state == State.Charging) SetLayer(Layers.Enemy);
            ExtraJitter = Vector2.zero;
            _state = State.Walk;
            _timer = 0f;
            _cooldown = 0.6f;
            _behindTime = 0f;
            ReleaseToken();
        }

        /// <summary>Interruptible이 false라 넉백으로는 불리지 않는다. 죽거나 지워질 때만 정리한다.</summary>
        protected override void OnInterrupted()
        {
            if (_telegraph) _telegraph.Cancel();
            _telegraph = null;
            StrongAttackSchedule.Release(ref _reservedEnd);
            ReleaseToken();
            if (this) SetLayer(Layers.Enemy);
        }

        /// <summary>
        /// 놓아주기·칸 밖에서 돌아오기·다시 섬(3차 초안 2-6): 예고·돌진을 거두고 공격 기회를 돌려준 뒤 걷기부터 다시.
        /// OnInterrupted만으로는 상태가 남아(돌진 예고 → 돌진) 공격 기회 없이 이어지므로 상태도 되돌린다.
        /// </summary>
        protected override void ResetBehaviour()
        {
            OnInterrupted();
            ExtraJitter = Vector2.zero;
            if (_state == State.Stunned) Health.DamageTakenMultiplier = 1f;
            if (_stunMark) _stunMark.enabled = false;
            _state = State.Walk;
            _timer = 0f;
            _behindTime = 0f;
            _secondChargeUsed = false;
            _cooldown = Mathf.Max(_cooldown, 1.0f);
            DesiredVelocity = Vector2.zero;
        }
    }
}
