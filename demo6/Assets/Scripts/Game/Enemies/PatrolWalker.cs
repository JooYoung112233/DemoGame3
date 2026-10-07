using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 1-2층 탐험 맛 1차 4-3 순찰 무리: 쉬는(잠든) 적 하나를 정해진 길로 천천히 왕복시킨다(적 하나에 하나, CellEncounters가 붙임).
    /// 길의 0번은 제자리다. 0 → 끝 → 0을 되짚고, 양 끝에서 EndPause(2.5초) 동안 좌우 ±70°로 둘러본다.
    /// 쉬는 동안에만 매 프레임 Enemy.WalkWhileResting으로 걸음을 넣는다. 깨거나 곧 깨면('!'·무리 반응) 손을 떼고 칸 규칙·두뇌에 맡긴다.
    /// 놓아주기·다시 서기로 다시 쉬면 제자리 가까울 때 ResumeDelay(1초, + 처음 출발 늦춤) 뒤 0번 다리부터 다시 걷는다.
    /// 벽·기둥은 CircleCast(벽 층, 몸 반지름 × 0.8)로 ±35°·±70° 비켜 간다(길찾기 없음). 3초 동안 0.1도 못 다가가면 다음 점으로 넘어가고,
    /// 12초 넘게 어느 점에도 못 닿으면 그 점으로 옮긴다(보이는 적은 보이지 않을 때까지 미룸). 시간은 게임 시간(Time.deltaTime)만 쓴다.
    /// 감지·기습은 잠과 같고 걷는 쪽이 앞이다(Enemy.DetectsPlayer). 그래서 순찰의 등 뒤에서는 기습할 수 있고, 내 등 뒤로 걸어오면 먼저 알아챈다.
    /// </summary>
    public sealed class PatrolWalker : MonoBehaviour
    {
        /// <summary>걸음 = 무리에서 가장 느린 종류 속도 × 적 걸음 배율 × 이 값(부르는 쪽이 셈해 넘김).</summary>
        public const float SpeedScale = 0.5f;
        /// <summary>길 양 끝에서 머무는 시간(초).</summary>
        public const float EndPause = 2.5f;
        /// <summary>끝에서 둘러보는 각도(도, 좌우).</summary>
        public const float LookTurnDeg = 70f;
        /// <summary>무리 안 출발 늦춤 간격(초, 한 줄로 문을 지나게).</summary>
        public const float StartGap = 0.7f;
        /// <summary>문 가운데에서 칸 안쪽으로 들어간 길 점까지 거리.</summary>
        public const float DoorInset = 2f;
        /// <summary>길 점에 닿았다고 보는 거리.</summary>
        public const float Arrive = 0.3f;
        /// <summary>이 시간 동안 0.1도 못 다가가면 다음 점으로 넘어간다.</summary>
        public const float StuckSkip = 3f;
        /// <summary>마지막으로 닿은 뒤 이 시간이 지나도록 어느 점에도 못 닿으면 지금 점으로 옮긴다.</summary>
        public const float StuckWarp = 12f;
        /// <summary>다시 쉰 뒤 걷기 시작까지(초).</summary>
        public const float ResumeDelay = 1f;

        /// <summary>'다가갔다'로 셀 만큼 줄어든 거리.</summary>
        const float StuckProgress = 0.1f;
        /// <summary>한 프레임에 이만큼 넘게 옮겨졌으면 다시 서기(Enemy.ResetToHome)로 제자리에 옮겨진 것으로 본다.</summary>
        const float JumpDistance = 1.5f;
        /// <summary>제자리에서 이 안이면 '제자리 가까움'(바로 0번 다리부터).</summary>
        const float HomeNear = 1f;
        /// <summary>벽 비켜 가기 앞쪽 살핌 거리.</summary>
        const float Probe = 0.8f;
        const float SteerSmall = 35f;
        const float SteerLarge = 70f;

        Enemy _enemy;
        Rigidbody2D _body;
        readonly List<Vector2> _route = new List<Vector2>();
        float _speed;
        float _startDelay;
        /// <summary>지금 가는 길 점 번호.</summary>
        int _target = 1;
        /// <summary>+1 = 끝 쪽으로, −1 = 제자리 쪽으로.</summary>
        int _step = 1;
        /// <summary>출발·다시 걷기 전 기다림(초).</summary>
        float _wait;
        /// <summary>끝에서 둘러보는 남은 시간(초).</summary>
        float _pause;
        float _pauseTime;
        Vector2 _lookBase = Vector2.down;
        Vector2 _lastDir;
        /// <summary>지금 점까지 가장 가까웠던 거리와 그 뒤로 못 다가간 시간.</summary>
        float _best;
        float _noProgress;
        /// <summary>마지막으로 길 점에 닿은 뒤 지난 시간(넘어가기로는 비우지 않음).</summary>
        float _sinceArrive;
        /// <summary>먼 끝에 닿았다(돌아와 제자리에 닿으면 한 바퀴).</summary>
        bool _lapOpen;
        /// <summary>깨었거나 곧 깨서 손을 뗐다(다시 쉬면 이어 걷기).</summary>
        bool _released;
        bool _hasLast;
        Vector2 _lastPos;

        /// <summary>지금 걸음을 넣고 있는가(쉬는 중이고 기다림·둘러봄이 아님).</summary>
        public bool Walking { get; private set; }

        /// <summary>제자리 → 먼 끝 → 제자리를 마친 횟수(확인용).</summary>
        public int Laps { get; private set; }

        /// <summary>순찰 길(0번 = 제자리, 확인용).</summary>
        public IReadOnlyList<Vector2> Route => _route;

        /// <summary>
        /// 적 e에 순찰을 붙인다(이미 있으면 길을 바꿔 처음부터). route[0]은 제자리(Enemy.HomePosition), 점이 둘 이상이어야 한다.
        /// speed는 초당 유닛(느려짐은 걸을 때 곱함), startDelay는 처음 출발 늦춤(무리 안 차례 × StartGap). 못 붙이면 null.
        /// </summary>
        public static PatrolWalker Attach(Enemy e, IReadOnlyList<Vector2> route, float speed, float startDelay)
        {
            if (!e || route == null || route.Count < 2) return null;
            var walker = e.GetComponent<PatrolWalker>();
            if (!walker) walker = e.gameObject.AddComponent<PatrolWalker>();
            walker._enemy = e;
            walker._body = e.GetComponent<Rigidbody2D>();
            walker._route.Clear();
            for (int i = 0; i < route.Count; i++) walker._route.Add(route[i]);
            walker._speed = Mathf.Max(0f, speed);
            walker._startDelay = Mathf.Max(0f, startDelay);
            walker._released = false;
            walker._hasLast = false;
            walker.Laps = 0;
            walker.BeginFromHome(walker._startDelay);
            return walker;
        }

        /// <summary>제자리에서 0번 다리(0 → 1)부터 delay초 뒤 걷는다.</summary>
        void BeginFromHome(float delay)
        {
            _target = 1;
            _step = 1;
            _wait = delay;
            _pause = 0f;
            _lapOpen = false;
            _sinceArrive = 0f;
            ResetLeg();
            Walking = false;
        }

        void ResetLeg()
        {
            _best = float.MaxValue;
            _noProgress = 0f;
        }

        void Update()
        {
            if (!_enemy || _enemy.Dead)
            {
                Walking = false;
                return;
            }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector2 pos = _enemy.Position;
            bool jumped = _hasLast && (pos - _lastPos).sqrMagnitude > JumpDistance * JumpDistance;
            _lastPos = pos;
            _hasLast = true;

            // 깨었거나 알아채는 중: 손을 뗀다(칸 규칙·두뇌가 맡는다). Enemy가 걸음을 이미 0으로 지웠다.
            if (_enemy.Aware || _enemy.WakePending)
            {
                _released = true;
                Walking = false;
                return;
            }
            // 다시 쉼(놓아주기 끝·다시 서기) 또는 다시 서기로 제자리에 옮겨짐: 제자리에서 다시 시작한다.
            if (_released || jumped)
            {
                _released = false;
                Resume(pos);
            }
            if (_route.Count < 2 || _speed <= 0f)
            {
                Stop();
                return;
            }
            if (_wait > 0f)
            {
                _wait -= dt;
                Stop();
                return;
            }
            if (_pause > 0f)
            {
                _pause -= dt;
                LookAround(dt);
                if (_pause > 0f) return;
                Advance();
            }
            WalkStep(pos, dt);
        }

        /// <summary>다시 쉴 때: 제자리 가까우면 ResumeDelay(+ 처음 출발 늦춤) 뒤 0번 다리부터, 멀면 먼저 제자리로 걸어간다.</summary>
        void Resume(Vector2 pos)
        {
            if ((pos - _route[0]).sqrMagnitude <= HomeNear * HomeNear)
            {
                BeginFromHome(ResumeDelay + _startDelay);
                return;
            }
            _target = 0;
            _step = -1;
            _wait = ResumeDelay;
            _pause = 0f;
            _lapOpen = false;
            _sinceArrive = 0f;
            ResetLeg();
        }

        void WalkStep(Vector2 pos, float dt)
        {
            Vector2 target = _route[_target];
            Vector2 to = target - pos;
            float d = to.magnitude;
            if (d <= Arrive)
            {
                Arrived();
                return;
            }
            _sinceArrive += dt;
            if (_sinceArrive >= StuckWarp && !Watched())
            {
                // 12초 넘게 어느 점에도 못 닿음: 지금 점으로 옮긴다(영원히 걸린 순찰이 남지 않게).
                Warp(target);
                Arrived();
                return;
            }
            if (d < _best - StuckProgress)
            {
                _best = d;
                _noProgress = 0f;
            }
            else
            {
                _noProgress += dt;
                if (_noProgress >= StuckSkip)
                {
                    // 3초 동안 못 다가감(기둥·동료에 막힘): 다음 점으로 넘어간다.
                    Advance();
                    Stop();
                    return;
                }
            }
            Vector2 dir = Steer(pos, to / d, Mathf.Min(d, Probe));
            _lastDir = dir;
            _enemy.WalkWhileResting(dir * (_speed * _enemy.SlowFactor));
            Walking = true;
        }

        /// <summary>점에 닿음: 끝이면 둘러보기, 가운데 점이면 다음 점으로.</summary>
        void Arrived()
        {
            _sinceArrive = 0f;
            if (IsEnd(_target))
            {
                if (_target == _route.Count - 1) _lapOpen = true;
                else if (_lapOpen)
                {
                    Laps++;
                    _lapOpen = false;
                }
                _pause = EndPause;
                _pauseTime = 0f;
                _lookBase = _lastDir.sqrMagnitude > 0.0001f ? _lastDir : _enemy.FacingDirection;
                Stop();
                return;
            }
            Advance();
        }

        /// <summary>다음 점으로(끝에서는 방향을 뒤집는다). 먼 끝을 건너뛰어도 한 바퀴로 센다.</summary>
        void Advance()
        {
            int last = _route.Count - 1;
            if (_target >= last)
            {
                _lapOpen = true;
                _step = -1;
            }
            else if (_target <= 0) _step = 1;
            _target = Mathf.Clamp(_target + _step, 0, last);
            ResetLeg();
        }

        bool IsEnd(int index) => index == 0 || index == _route.Count - 1;

        /// <summary>끝에서 머무는 동안 왔던 쪽을 가운데로 좌우 ±70°를 한 번 훑는다(쉬는 채로 몸만 돌림).</summary>
        void LookAround(float dt)
        {
            _pauseTime += dt;
            float a = LookTurnDeg * Mathf.Sin(_pauseTime / EndPause * Mathf.PI * 2f);
            _enemy.WalkWhileResting(Vector2.zero);
            _enemy.TurnWhileResting(Turned(_lookBase, a));
            Walking = false;
        }

        void Stop()
        {
            _enemy.WalkWhileResting(Vector2.zero);
            Walking = false;
        }

        /// <summary>목표 쪽 방향. 곧장 가면 벽·기둥에 막히면 ±35°·±70°로 틀어 비켜 간다(Enemy.SteerTo와 같은 방식, 길찾기 없음).</summary>
        Vector2 Steer(Vector2 pos, Vector2 dir, float probe)
        {
            float r = _enemy.Radius * 0.8f;
            if (!Physics2D.CircleCast(pos, r, dir, probe, Layers.WallMask)) return dir;
            for (int k = 0; k < 4; k++)
            {
                float a = (k < 2 ? SteerSmall : SteerLarge) * (k % 2 == 0 ? 1f : -1f);
                Vector2 alt = Turned(dir, a);
                if (!Physics2D.CircleCast(pos, r, alt, probe, Layers.WallMask)) return alt;
            }
            return dir;
        }

        /// <summary>던전 시야로 지금 그려지는 적인가(보는 앞에서는 옮기지 않는다). 시야가 없거나 꺼졌으면 아님.</summary>
        bool Watched()
        {
            var vision = VisionSystem.Instance;
            return vision && vision.VisionOn && _enemy.VisionInSight && !_enemy.VisionHidden;
        }

        void Warp(Vector2 p)
        {
            if (_body)
            {
                _body.position = p;
                _body.linearVelocity = Vector2.zero;
            }
            transform.position = p;
            _lastPos = p;
        }

        static Vector2 Turned(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        void OnDisable()
        {
            if (_enemy && !_enemy.Dead) _enemy.WalkWhileResting(Vector2.zero);
            Walking = false;
        }
    }
}
