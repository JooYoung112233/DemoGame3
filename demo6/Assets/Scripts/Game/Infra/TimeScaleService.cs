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

        public static void HitStop(float seconds)
        {
            if (!_instance || !Tuning.HitStopEnabled || seconds <= 0f) return;
            _instance._arbiter.RequestHitStop(seconds * Tuning.HitStopScale);
            _instance.Apply();
        }

        public static void SlowMotion(float seconds, float scale)
        {
            if (!_instance) return;
            _instance._arbiter.RequestSlowMotion(seconds, scale);
            _instance.Apply();
        }

        public static void ResetStatics()
        {
            GameSpeed = 1f;
            Paused = false;
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
