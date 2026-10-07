using Demo6.Core.Time;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>Time.timeScale은 이 서비스만 바꾼다(기획 11-6).</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class TimeScaleService : MonoBehaviour
    {
        static TimeScaleService _instance;
        readonly TimeScaleArbiter _arbiter = new TimeScaleArbiter();

        /// <summary>시험용 게임 속도(×0.5, ×1, ×2). 멈춤이 아닐 때만 곱한다.</summary>
        public static float GameSpeed = 1f;
        public static bool Paused;

        /// <summary>히트스톱 요청 누계(초): 진행 중인 멈춤보다 더 늘려 달라고 한 몫만 센다(겹쳐서 늘릴 것이 없는 요청은 빼고).</summary>
        public static double HitStopRequested { get; private set; }
        /// <summary>히트스톱 허락 누계(초): TimeScaleArbiter.RequestHitStop가 실제로 늘린 시간.</summary>
        public static double HitStopGranted { get; private set; }
        /// <summary>
        /// 히트스톱이 1초 예산(0.2초)에 잘린 비율 = 1 − 허락 ÷ 요청(장비 문서 3-4·12장 기록: 5%를 넘으면 무거운 치명 간격을 0.8초로).
        /// 요청이 없으면 0.
        /// </summary>
        public static float HitStopTrimmedFraction => HitStopRequested > 1e-9 ? (float)System.Math.Max(0.0, 1.0 - HitStopGranted / HitStopRequested) : 0f;

        /// <summary>히트스톱 요청·허락 누계를 비운다(시험장 '통계 초기화').</summary>
        public static void ResetHitStopRecord()
        {
            HitStopRequested = 0;
            HitStopGranted = 0;
        }

        public static void HitStop(float seconds)
        {
            if (!_instance || !Tuning.HitStopEnabled || seconds <= 0f) return;
            // 잠금 중(보스 처치 순간)에는 다른 요청을 무시한다. 예산에 잘린 것이 아니므로 잘린 비율 계측에 넣지 않는다.
            if (_instance._arbiter.HitStopLocked) return;
            float want = seconds * Tuning.HitStopScale;
            double extension = want - _instance._arbiter.HitStopRemaining;
            double granted = _instance._arbiter.RequestHitStop(want);
            if (extension > 0)
            {
                HitStopRequested += extension;
                HitStopGranted += granted;
            }
            _instance.Apply();
        }

        public static void SlowMotion(float seconds, float scale)
        {
            if (!_instance) return;
            _instance._arbiter.RequestSlowMotion(seconds, scale);
            _instance.Apply();
        }

        /// <summary>
        /// 순위가 있는 느린 화면(기획/전투-보스-무기-다듬기-1차.md 4-3: 보스 처치 &gt; 보스 2단계 &gt; 처형 &gt; 회피 반격 &gt; 그 밖). 쓰는 곳: OgreBrain(②), ExecutionFx·CounterFx(⑤).
        /// 겹치면 높은 순위만 쓰고, 보스 순간(BossPhase·BossKill) 앞뒤 1.5초 안의 낮은 순위는 무시한다(TimeScaleArbiter). 순위 없는 SlowMotion은 Normal이다.
        /// </summary>
        public static void SlowMotion(float seconds, float scale, SlowPriority priority)
        {
            if (!_instance) return;
            _instance._arbiter.RequestSlowMotion(seconds, scale, priority);
            _instance.Apply();
        }

        /// <summary>
        /// 처치 순간 멈춤(4-3 #1 보스 처치): 1초 0.2초 예산 안에서 seconds를 요청하고, 지금부터 lockSeconds(실제 시간) 동안 다른 히트스톱 요청(연쇄 폭발 등)을 무시한다.
        /// 잘린 비율 계측(HitStopTrimmedFraction)은 HitStop과 같은 식으로 센다.
        /// </summary>
        public static void HitStopLocked(float seconds, float lockSeconds)
        {
            if (!_instance || !Tuning.HitStopEnabled || seconds <= 0f) return;
            float want = seconds * Tuning.HitStopScale;
            double extension = want - _instance._arbiter.HitStopRemaining;
            double granted = _instance._arbiter.RequestHitStopLocked(want, lockSeconds);
            if (extension > 0)
            {
                HitStopRequested += extension;
                HitStopGranted += granted;
            }
            _instance.Apply();
        }

        public static void ResetStatics()
        {
            GameSpeed = 1f;
            Paused = false;
            ResetHitStopRecord();
        }

        void Awake()
        {
            _instance = this;
            _arbiter.Reset();
            Apply();
        }

        void Update()
        {
            _arbiter.Advance(Time.unscaledDeltaTime);
            Apply();
        }

        void Apply()
        {
            _arbiter.Paused = Paused;
            double effective = _arbiter.Effective;
            Time.timeScale = (float)(effective > 0 ? effective * GameSpeed : 0);
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            Time.timeScale = 1f;
        }
    }

}
