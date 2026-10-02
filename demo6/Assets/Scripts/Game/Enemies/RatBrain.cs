using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 기획 4-3 굴쥐: 직선으로 쫓되 서로 0.6 떨어지려는 힘으로 퍼져서 둘러싼다.
    /// 거리 0.7 안에서 0.25초 움찔(예고) → 앞쪽 90° 물기 → 1.0초 쉼(이동속도 50%).
    /// </summary>
    public sealed class RatBrain : Enemy
    {
        /// <summary>거리는 플레이어 몸 가장자리 기준이다(멈춤·예고·물기 모두 같은 기준).</summary>
        const float AttackRange = 0.7f;
        const float StopDistance = AttackRange - 0.1f;
        const float WindupTime = 0.25f;
        const float RestTime = 1.0f;
        const float BiteArc = 90f;
        const float SeparationDistance = 0.6f;

        enum State
        {
            Chase,
            Windup,
            Rest,
        }

        State _state;
        float _timer;
        Vector2 _biteDir;
        bool _hasToken;

        protected override void Think(float dt)
        {
            var player = Player;
            if (!player || player.IsDown)
            {
                DesiredVelocity = Vector2.zero;
                return;
            }
            Vector2 to = player.Position - Position;
            float dist = to.magnitude;
            float edge = dist - PlayerController.Radius;

            switch (_state)
            {
                case State.Chase:
                    SetPose(EnemyPose.Locomotion);
                    FaceTowards(to);
                    DesiredVelocity = ChaseVelocity(to, dist, edge, 1f);
                    if (edge <= AttackRange && AttackTokens.TryAcquire(Kind))
                    {
                        _hasToken = true;
                        _state = State.Windup;
                        _timer = 0f;
                        _biteDir = to.sqrMagnitude > 0.0001f ? to.normalized : Facing;
                        DesiredVelocity = Vector2.zero;
                        SetPose(EnemyPose.Windup, WindupTime, WindupTime);
                    }
                    break;

                case State.Windup:
                    DesiredVelocity = Vector2.zero;
                    _timer += dt;
                    // 움찔: 몸이 부풀었다 줄어든다.
                    Sprite.transform.localScale = Vector3.one * (MonsterRule.Rat.Diameter * (1f + 0.3f * Mathf.Sin(_timer / WindupTime * Mathf.PI)));
                    if (_timer >= WindupTime)
                    {
                        Bite(player);
                        EndWindup();
                        _state = State.Rest;
                        _timer = 0f;
                    }
                    break;

                case State.Rest:
                    _timer += dt;
                    SetPose(_timer < 0.2f ? EnemyPose.Attack : EnemyPose.Locomotion);
                    FaceTowards(to);
                    DesiredVelocity = ChaseVelocity(to, dist, edge, 0.5f);
                    if (_timer >= RestTime) _state = State.Chase;
                    break;
            }
        }

        Vector2 ChaseVelocity(Vector2 to, float dist, float edge, float speedScale)
        {
            Vector2 toDir = to / Mathf.Max(dist, 0.0001f);
            Vector2 move = edge > StopDistance ? toDir : Vector2.zero;
            // 뒤쪽 굴쥐에 밀려 플레이어 몸 안으로 들어가지 않게, 너무 가까우면 바깥으로 민다.
            if (edge < StopDistance - 0.15f) move -= toDir * Mathf.Clamp01((StopDistance - edge) / StopDistance);
            Vector2 steer = move + Separation(SeparationDistance) * 1.5f;
            if (steer.sqrMagnitude > 1f) steer.Normalize();
            return steer * (MoveSpeed * speedScale);
        }

        void Bite(PlayerController player)
        {
            Vector2 to = player.Position - Position;
            bool inReach = to.magnitude <= AttackRange + PlayerController.Radius;
            bool inArc = to.sqrMagnitude < 0.0001f || Vector2.Angle(_biteDir, to) <= BiteArc * 0.5f;
            if (inReach && inArc) player.ReceiveHit(AttackPower, 100f, Position, 0.4f);
        }

        void EndWindup()
        {
            Sprite.transform.localScale = Vector3.one * MonsterRule.Rat.Diameter;
            if (_hasToken)
            {
                _hasToken = false;
                AttackTokens.Release(Kind);
            }
        }

        protected override void OnInterrupted()
        {
            if (_state == State.Windup) _state = State.Chase;
            EndWindup();
        }

        /// <summary>놓아주기·다시 섬(3차 초안 2-6): 물기 준비를 거두고 공격 기회를 돌려준 뒤 쫓기부터 다시.</summary>
        protected override void ResetBehaviour()
        {
            OnInterrupted();
            _state = State.Chase;
            _timer = 0f;
        }
    }
}
