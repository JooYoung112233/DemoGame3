using System.Collections;
using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 기획 4-3 굴쥐: 직선으로 쫓되 서로 0.6 떨어지려는 힘으로 퍼져서 둘러싼다.
    /// 거리 0.7 안에서 0.25초 움찔(예고) → 앞쪽 90° 물기 → 1.0초 쉼(이동속도 50%).
    /// 무리 공포(기획/전투-보스-무기-다듬기-1차.md 4-2 [2], FearRule): 겁먹으면 공격 기회를 내놓고 물기 예고를 거둔 뒤
    /// 플레이어 반대쪽으로 걸음 × 1.1로 달아나며 몸을 떤다. 칸 경계·벽에 몰리면 달아나지 않고 웅크리기만 한다. 글자는 띄우지 않는다.
    /// 겁먹음은 PackFear.Frighten 입구로 넣는다(사건 EnemyFrightened를 함께 낸다).
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
        /// <summary>둥지가 부른 굴쥐를 가려내는 거리(둥지는 제 둘레 1.2~1.8에 부른다).</summary>
        const float NestSpawnReach = 2.0f;
        /// <summary>칸 경계·벽에 막혀 달아나는 속도가 이 몫보다 작으면 웅크린다.</summary>
        const float CornerFraction = 0.35f;
        /// <summary>달아날 때 앞을 살피는 거리(벽·기둥이면 옆으로 튼다).</summary>
        const float FleeProbe = 0.6f;
        /// <summary>쓰러진 자리 반대쪽도 조금 섞는다(플레이어 반대쪽이 먼저, 무리는 시체에서 흩어진다).</summary>
        const float OriginWeight = 0.6f;
        const float FearJitter = 0.035f;
        const float CowerJitter = 0.055f;
        const float CowerScale = 0.82f;
        /// <summary>사라지는 졸개: 이만큼 달아난 뒤 흐려지기 시작해 VanishFade 동안 사라진다.</summary>
        const float VanishDelay = 0.6f;
        const float VanishFade = 1.0f;
        /// <summary>달아날 길이 막히면 이 각도들로 틀어 본다. 바뀌지 않는 값이라 플레이 시작 때 비울 필요가 없다.</summary>
        static readonly float[] FleeAngles = { 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f };

        enum State
        {
            Chase,
            Windup,
            Rest,
            Frightened,
        }

        State _state;
        float _timer;
        Vector2 _biteDir;
        bool _hasToken;
        float _fearUntil;
        Vector2 _fearFrom;
        bool _cowering;
        bool _vanishing;
        Coroutine _vanishRoutine;
        bool _nearNestAtSpawn;

        /// <summary>겁먹어 달아나는(또는 몰려 웅크린) 중.</summary>
        public bool IsFrightened => _state == State.Frightened;
        /// <summary>겁먹었지만 칸 경계·벽에 몰려 웅크린 중.</summary>
        public bool IsCowering => _state == State.Frightened && _cowering;
        /// <summary>달아나다 흐려져 사라지는 중('무리 거느린' 정예의 졸개 절반).</summary>
        public bool IsVanishing => _vanishing;
        /// <summary>겁먹음이 끝날 남은 시간(초).</summary>
        public float FrightenedRemaining => IsFrightened ? Mathf.Max(0f, _fearUntil - Time.time) : 0f;
        /// <summary>'무리 거느린' 정예의 졸개(PackLeader가 적는다).</summary>
        public bool InPack { get; set; }
        /// <summary>둥지가 부른 굴쥐(보상 없음 + 둥지 곁에서 태어남, 졸개 아님). 공포가 1.0초로 짧다.</summary>
        public bool FromNest => NoReward && _nearNestAtSpawn && !InPack;

        protected override void OnSpawned()
        {
            // 둥지는 굴쥐를 부른 바로 뒤 NoReward를 켜므로, 여기서는 태어난 자리 곁에 둥지가 있었는지만 적어 둔다.
            Vector2 at = transform.position;
            foreach (var e in All)
                if (e is NestBrain && !e.Dead && (e.Position - at).sqrMagnitude <= NestSpawnReach * NestSpawnReach)
                {
                    _nearNestAtSpawn = true;
                    break;
                }
        }

        protected override void Think(float dt)
        {
            var player = Player;
            if (_state == State.Frightened)
            {
                if (_vanishing || Time.time < _fearUntil)
                {
                    Flee(player);
                    return;
                }
                EndFear();
            }
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
                    // 정수리 그림 크기는 TopDownEnemyRig가 맡는다. 도형 배율을 덮으면 처치 때 그 작은 배율이 시체에 남는다.
                    if (!TopDownView.Active)
                        Sprite.transform.localScale = Vector3.one * (MonsterRule.Rat.Diameter * (1f + 0.3f * Mathf.Sin(_timer / WindupTime * Mathf.PI)));
                    if (_timer >= WindupTime)
                    {
                        Bite(player);
                        EndWindup();
                        _state = State.Rest;
                        // 방패에 튕겨 휘청했으면(Enemy.Parried) 깨어난 뒤 물기 자세(첫 0.2초)를 다시 보이지 않고 쉬기부터.
                        _timer = Staggered ? 0.2f : 0f;
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
            // 근접 물기(방패 표 2-7: 앞 반원 막기, 패링이면 이 굴쥐가 휘청 — Enemy.Parried 가벼움 기본값).
            if (inReach && inArc) player.ReceiveHit(AttackPower, 100f, Position, 0.4f, true, HitKind.Melee, default, this);
        }

        void EndWindup()
        {
            if (!TopDownView.Active) Sprite.transform.localScale = Vector3.one * MonsterRule.Rat.Diameter;
            if (_hasToken)
            {
                _hasToken = false;
                AttackTokens.Release(Kind);
            }
        }

        /// <summary>
        /// 겁먹는다(무리 공포). 공격 기회를 내놓고 물기 예고(움찔)를 거둔 뒤 seconds 동안 달아난다. 이미 겁먹었으면 더 긴 쪽으로 늘린다.
        /// awayFrom은 겁먹은 까닭이 된 자리(쓰러진 적, 보스 시체)다. 달아나는 방향은 플레이어 반대쪽이 먼저이고 이 자리 반대쪽을 조금 섞는다.
        /// 사건(EnemyFrightened)을 함께 내려면 PackFear.Frighten으로 넣는다.
        /// </summary>
        public void Frighten(Vector2 awayFrom, float seconds) => Frighten(awayFrom, seconds, false);

        /// <param name="vanish">달아나다 흐려져 사라진다('무리 거느린' 정예의 졸개, 보상 없음). 처치로 세지 않는다.</param>
        public void Frighten(Vector2 awayFrom, float seconds, bool vanish)
        {
            if (Dead || seconds <= 0f) return;
            bool fresh = _state != State.Frightened;
            if (fresh)
            {
                OnInterrupted();
                _state = State.Frightened;
                _timer = 0f;
                _cowering = false;
                _fearUntil = Time.time + seconds;
            }
            else _fearUntil = Mathf.Max(_fearUntil, Time.time + seconds);
            _fearFrom = awayFrom;
            if (vanish && !_vanishing)
            {
                _vanishing = true;
                _vanishRoutine = StartCoroutine(VanishRoutine());
            }
            if (fresh) PackFear.Squeak();
        }

        /// <summary>달아나기: 막히면 옆으로 틀고, 칸 경계 밖으로 나가는 쪽은 지운다. 남은 속도가 작으면 웅크린다.</summary>
        void Flee(PlayerController player)
        {
            bool playerUp = player && !player.IsDown;
            Vector2 away = Unit(Position - _fearFrom);
            if (playerUp) away = Unit(Position - player.Position) + away * OriginWeight;
            if (away.sqrMagnitude < 0.0001f) away = -Facing;
            away.Normalize();

            float speed = MoveSpeed * FearRule.FleeSpeedScale;
            Vector2 dir = SteerAway(away);
            Vector2 v = dir + Separation(SeparationDistance) * 0.5f;
            if (v.sqrMagnitude > 1f) v.Normalize();
            v = ClipToTerritory(v * speed);
            _cowering = dir.sqrMagnitude < 0.0001f || v.magnitude < speed * CornerFraction;
            if (_cowering)
            {
                // 칸 경계·벽에 몰림: 달아나지 않고 웅크려 떨며 위협 쪽을 본다.
                DesiredVelocity = Vector2.zero;
                SetPose(EnemyPose.Idle);
                if (playerUp) FaceTowards(player.Position - Position);
                ExtraJitter = Random.insideUnitCircle * CowerJitter;
                Sprite.transform.localScale = Vector3.one * (MonsterRule.Rat.Diameter * CowerScale);
                return;
            }
            DesiredVelocity = v;
            SetPose(EnemyPose.Locomotion);
            FaceTowards(v);
            ExtraJitter = Random.insideUnitCircle * FearJitter;
            Sprite.transform.localScale = Vector3.one * MonsterRule.Rat.Diameter;
        }

        static Vector2 Unit(Vector2 v) => v.sqrMagnitude > 0.0001f ? v.normalized : Vector2.zero;

        /// <summary>달아날 방향. 곧장 가면 벽·기둥에 막히면 조금씩 틀어 본다. 모두 막히면 0(웅크림).</summary>
        Vector2 SteerAway(Vector2 dir)
        {
            float r = Radius * 0.8f;
            if (!Physics2D.CircleCast(Position, r, dir, FleeProbe, Layers.WallMask)) return dir;
            foreach (float a in FleeAngles)
            {
                float rad = a * Mathf.Deg2Rad;
                float c = Mathf.Cos(rad);
                float s = Mathf.Sin(rad);
                var alt = new Vector2(dir.x * c - dir.y * s, dir.x * s + dir.y * c);
                if (!Physics2D.CircleCast(Position, r, alt, FleeProbe, Layers.WallMask)) return alt;
            }
            return Vector2.zero;
        }

        /// <summary>칸 경계(3차 초안 2-6) 밖으로 나가는 속도 성분을 지운다. 칸이 없으면 그대로.</summary>
        Vector2 ClipToTerritory(Vector2 v)
        {
            if (!Territory.HasValue) return v;
            Rect area = Territory.Value;
            Vector2 pos = Position;
            float m = Radius + 0.1f;
            if (pos.x <= area.xMin + m && v.x < 0f) v.x = 0f;
            if (pos.x >= area.xMax - m && v.x > 0f) v.x = 0f;
            if (pos.y <= area.yMin + m && v.y < 0f) v.y = 0f;
            if (pos.y >= area.yMax - m && v.y > 0f) v.y = 0f;
            return v;
        }

        /// <summary>겁먹음을 지우고 쫓기부터 다시(사라지는 중이면 그것도 멈춤).</summary>
        void EndFear()
        {
            if (_state == State.Frightened) _state = State.Chase;
            _timer = 0f;
            _cowering = false;
            _fearUntil = 0f;
            ExtraJitter = Vector2.zero;
            if (Sprite) Sprite.transform.localScale = Vector3.one * MonsterRule.Rat.Diameter;
        }

        /// <summary>달아나다 흐려져 사라진다. 투명도는 번쩍임(SpriteFlash.Update)이 칠한 뒤에 덮어쓴다(코루틴은 Update 뒤에 돈다).</summary>
        IEnumerator VanishRoutine()
        {
            float t = 0f;
            while (!Dead)
            {
                t += Time.deltaTime;
                _fearUntil = Mathf.Max(_fearUntil, Time.time + 0.1f);
                if (t >= VanishDelay)
                {
                    float k = Mathf.Clamp01((t - VanishDelay) / VanishFade);
                    var c = Sprite.color;
                    c.a = Mathf.Min(c.a, 1f - k);
                    Sprite.color = c;
                    if (k >= 1f)
                    {
                        _vanishRoutine = null;
                        Remove();
                        yield break;
                    }
                }
                yield return null;
            }
            _vanishRoutine = null;
        }

        protected override void OnInterrupted()
        {
            if (_state == State.Windup) _state = State.Chase;
            EndWindup();
        }

        /// <summary>놓아주기·다시 섬(3차 초안 2-6): 물기 준비를 거두고 공격 기회를 돌려준 뒤 쫓기부터 다시. 겁먹음·사라지기도 지운다.</summary>
        protected override void ResetBehaviour()
        {
            OnInterrupted();
            EndFear();
            if (_vanishRoutine != null) StopCoroutine(_vanishRoutine);
            _vanishRoutine = null;
            _vanishing = false;
            _state = State.Chase;
            _timer = 0f;
        }
    }
}
